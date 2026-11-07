using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Approvals.Application;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Modules.Approvals.Tests;

public class ApprovalServiceMutationTests
{
    [Fact]
    public async Task Submit_SetsSubmittedStatus()
    {
        var (context, connection) = TestDbContext.CreateSqlite();
        await using var _ = connection;
        await using var __ = context;
        var requester = Guid.NewGuid();
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Title = "Test",
            Status = ApprovalRequest.StatusDraft,
            RequestedBy = requester,
            CurrentApproverId = Guid.NewGuid(),
            ApprovalLevel = 1,
            CreatedBy = requester.ToString()
        };
        context.ApprovalRequests.Add(request);
        await context.SaveChangesAsync();

        var service = TestDbContext.CreateService(context, request.TenantId, includeSubmitPermission: true);
        var result = await service.SubmitAsync(request.Id, requester, "please review", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(ApprovalRequest.StatusSubmitted, result.Value!.Status);
    }

    [Fact]
    public async Task Submit_ByNonRequester_ReturnsForbidden()
    {
        var (context, connection) = TestDbContext.CreateSqlite();
        await using var _ = connection;
        await using var __ = context;
        var requester = Guid.NewGuid();
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = ApprovalRequest.StatusDraft,
            RequestedBy = requester,
            CurrentApproverId = Guid.NewGuid(),
            ApprovalLevel = 1,
            CreatedBy = requester.ToString()
        };
        context.ApprovalRequests.Add(request);
        await context.SaveChangesAsync();

        var service = TestDbContext.CreateService(context, request.TenantId, includeSubmitPermission: true);
        var result = await service.SubmitAsync(request.Id, Guid.NewGuid(), null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        // Hidden-resource semantics: non-requester/non-visible caller receives NotFound (no existence leak).
        Assert.Equal(404, result.Error!.StatusCode);
    }
}

/// <summary>
/// SQLite-backed test DbContext for Approvals tests. Gives realistic transaction/SaveChanges behavior.
/// RowVersion is modeled as a concurrency token with test-managed values (production SQL Server rowversion unchanged).
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();

    public static (TestDbContext context, SqliteConnection connection) CreateSqlite(string? name = null)
    {
        var connection = new SqliteConnection(name is null ? "Data Source=:memory:" : $"Data Source={name};Mode=Memory;Cache=Shared");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }

    public static ApprovalService CreateService(TestDbContext context, Guid tenantId, bool includeSubmitPermission = false, IApprovalDelegationEligibilityLookup? eligibility = null)
    {
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var http = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim> { new("tenantId", tenantId.ToString()), new("permission", "ApprovalRequest.View") };
        if (includeSubmitPermission) claims.Add(new("permission", "ApprovalRequest.Submit"));
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) });
        return new ApprovalService(context, auditMock.Object, http.Object, eligibility ?? Mock.Of<IApprovalDelegationEligibilityLookup>());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApprovalRequest>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.RowVersion).IsRequired(false).IsConcurrencyToken(false).ValueGeneratedNever();
            b.HasMany(e => e.Actions).WithOne(a => a.ApprovalRequest).HasForeignKey(a => a.ApprovalRequestId);
            b.Navigation(e => e.Actions).UsePropertyAccessMode(PropertyAccessMode.Property);
        });
        modelBuilder.Entity<ApprovalAction>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.RowVersion).IsRequired(false).IsConcurrencyToken(false).ValueGeneratedNever();
            b.Property(a => a.ApprovalRequestId).ValueGeneratedNever();
        });
        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.RowVersion).IsRequired(false).IsConcurrencyToken(false).ValueGeneratedNever();
        });
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            foreach (var prop in entityType.GetProperties().Where(p => p.IsConcurrencyToken).ToList())
                prop.IsConcurrencyToken = false;
    }
}
