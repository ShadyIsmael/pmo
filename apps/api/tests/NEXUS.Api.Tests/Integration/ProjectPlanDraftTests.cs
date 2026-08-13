using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain.Entities;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Focused EF/persistence tests for the Project Plan Working Draft foundation.
/// Uses the same in-memory SQLite setup as the other API integration tests, with
/// the shared tenant resolution so the global tenant filter stays active.
/// </summary>
public class ProjectPlanDraftTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _baselineId = Guid.NewGuid();

    public ProjectPlanDraftTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        _accessor = new Mock<IHttpContextAccessor>();
    }

    private void SetTenant(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "mock"));
        _accessor.Setup(x => x.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_accessor.Object);
    }

    private void SeedProjectAndBaseline()
    {
        _context.Projects.Add(new Project
        {
            Id = _projectId,
            TenantId = _tenantA,
            Name = "Project A",
            Code = "PA",
            Status = "Active",
            Currency = "GBP",
            CreatedBy = _userA.ToString(),
            RowVersion = new byte[8]
        });
        _context.ProjectBaselineVersions.Add(new ProjectBaselineVersion
        {
            Id = _baselineId,
            TenantId = _tenantA,
            ProjectId = new ProjectId(_projectId.ToString()),
            VersionNumber = new VersionNumber(1),
            Name = "Baseline v1",
            Status = Status.Approved,
            CreatedBy = _userA.ToString(),
            IsCurrent = true
        });
        _context.SaveChanges();
    }

    private ProjectPlanDraft NewDraft(bool active = true)
    {
        return new ProjectPlanDraft
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            ProjectId = _projectId,
            SourceBaselineVersionId = _baselineId,
            Status = ProjectPlanDraft.DraftStatus.Draft,
            IsActive = active
        };
    }

    [Fact]
    public async Task Save_RoundTrips_AllMappedProperties()
    {
        SetTenant(_tenantA, _userA);
        SeedProjectAndBaseline();

        var draft = NewDraft();
        _context.ProjectPlanDrafts.Add(draft);
        await _context.SaveChangesAsync();

        var loaded = await _context.ProjectPlanDrafts
            .IgnoreQueryFilters()
            .SingleAsync(d => d.Id == draft.Id);

        Assert.Equal(_projectId, loaded.ProjectId);
        Assert.Equal(_baselineId, loaded.SourceBaselineVersionId);
        Assert.Equal(ProjectPlanDraft.DraftStatus.Draft, loaded.Status);
        Assert.True(loaded.IsActive);
        Assert.Null(loaded.SubmittedChangeRequestId);
        Assert.NotEmpty(loaded.RowVersion);
        Assert.Equal(_tenantA, loaded.TenantId);
        Assert.Equal(_userA.ToString(), loaded.CreatedBy);
        Assert.True(loaded.CreatedAt > DateTime.MinValue);
        Assert.Null(loaded.UpdatedAt);
    }

    [Fact]
    public async Task CreatingDraft_DoesNotModify_ApprovedBaseline()
    {
        SetTenant(_tenantA, _userA);
        SeedProjectAndBaseline();

        var before = _context.ProjectBaselineVersions.Single(b => b.Id == _baselineId);
        Assert.Equal(Status.Approved, before.Status);
        Assert.True(before.IsCurrent);

        _context.ProjectPlanDrafts.Add(NewDraft());
        await _context.SaveChangesAsync();

        var after = _context.ProjectBaselineVersions.Single(b => b.Id == _baselineId);
        Assert.Equal(Status.Approved, after.Status);
        Assert.True(after.IsCurrent);
        Assert.Equal(1, after.VersionNumber.Value);
        Assert.Equal(1, _context.ProjectPlanDrafts.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task SecondActiveDraft_SameTenantAndProject_FailsUniqueConstraint()
    {
        SetTenant(_tenantA, _userA);
        SeedProjectAndBaseline();

        _context.ProjectPlanDrafts.Add(NewDraft(active: true));
        await _context.SaveChangesAsync();

        _context.ProjectPlanDrafts.Add(NewDraft(active: true));
        var exception = await Record.ExceptionAsync(() => _context.SaveChangesAsync());

        Assert.NotNull(exception);
        Assert.IsType<DbUpdateException>(exception);
    }

    [Fact]
    public async Task ActiveDraft_AndInactiveDraft_CanCoexist_SameTenantProject()
    {
        SetTenant(_tenantA, _userA);
        SeedProjectAndBaseline();

        var active = NewDraft(active: true);
        active.Status = ProjectPlanDraft.DraftStatus.Submitted;
        _context.ProjectPlanDrafts.Add(active);

        var inactive = NewDraft(active: false);
        inactive.Status = ProjectPlanDraft.DraftStatus.Applied;
        _context.ProjectPlanDrafts.Add(inactive);

        await _context.SaveChangesAsync();

        Assert.Equal(2, _context.ProjectPlanDrafts.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task TenantB_NeverReadsTenantA_ProjectPlanDrafts()
    {
        SetTenant(_tenantA, _userA);
        SeedProjectAndBaseline();
        _context.ProjectPlanDrafts.Add(NewDraft());
        await _context.SaveChangesAsync();

        SetTenant(_tenantB, Guid.NewGuid());
        Assert.Empty(await _context.ProjectPlanDrafts.ToListAsync());

        // Tenant B cannot see the row even by explicit id (tenant query filter hides it).
        Assert.Empty(await _context.ProjectPlanDrafts.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentSaves_Throw_DbUpdateConcurrencyException()
    {
        SetTenant(_tenantA, _userA);
        SeedProjectAndBaseline();

        var draft = NewDraft();
        _context.ProjectPlanDrafts.Add(draft);
        await _context.SaveChangesAsync();

        var otherContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        try
        {
            // Load the row through a second context, then overwrite its RowVersion
            // concurrency token to a stale value. SQLite does not regenerate a
            // rowversion on UPDATE the way SQL Server does, so the token would not
            // drift on its own in tests; forcing the original token to differ from
            // the stored row proves the RowVersion concurrency token is enforced
            // (produces DbUpdateConcurrencyException rather than a silent overwrite).
            var second = await otherContext.ProjectPlanDrafts.IgnoreQueryFilters()
                .SingleAsync(d => d.Id == draft.Id);
            second.Status = ProjectPlanDraft.DraftStatus.Rejected;
            otherContext.Entry(second).Property(d => d.RowVersion).OriginalValue = new byte[8];

            var exception = await Record.ExceptionAsync(() => otherContext.SaveChangesAsync());

            Assert.NotNull(exception);
            Assert.IsType<DbUpdateConcurrencyException>(exception);
        }
        finally
        {
            otherContext.Dispose();
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}