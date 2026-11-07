using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectStatusReportSnapshotPersistenceTests : IDisposable
{
    private static readonly DateTime PeriodFrom = new(2026, 9, 1);
    private static readonly DateTime PeriodTo = new(2026, 9, 30);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectStatusReportSnapshotPersistenceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Name, "Snapshot Test User")
        ], "snapshot-test"));
        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(httpAccessor.Object);

        _context.Set<Project>().Add(new Project
        {
            Id = _projectId,
            TenantId = _tenantId,
            Name = "Snapshot Test Project",
            Code = "SNP-001",
            Currency = "USD",
            Budget = 100000m,
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 1)
        });

        var obligation1 = new PaymentObligation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProjectId = _projectId,
            ProjectName = "Snapshot Test Project",
            PaymentReference = "REF-001",
            Purpose = "Test",
            Description = "Test",
            Currency = "USD",
            Amount = 50000m,
            DueDate = new DateTime(2026, 9, 15),
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 1)
        };
        _context.Set<PaymentObligation>().Add(obligation1);

        var settlementBefore = new PaymentSettlement
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            PaymentObligationId = obligation1.Id,
            IdempotencyKey = Guid.NewGuid(),
            Amount = 20000m,
            SettlementDate = new DateTime(2026, 9, 10),
            PaymentReference = "SET-001",
            CreatedAt = new DateTime(2026, 9, 10),
            UpdatedAt = new DateTime(2026, 9, 10)
        };
        _context.Set<PaymentSettlement>().Add(settlementBefore);

        var settlementAfter = new PaymentSettlement
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            PaymentObligationId = obligation1.Id,
            IdempotencyKey = Guid.NewGuid(),
            Amount = 10000m,
            SettlementDate = new DateTime(2026, 10, 5),
            PaymentReference = "SET-002",
            CreatedAt = new DateTime(2026, 10, 5),
            UpdatedAt = new DateTime(2026, 10, 5)
        };
        _context.Set<PaymentSettlement>().Add(settlementAfter);

        _context.SaveChanges();
    }

    [Fact]
    public async Task Financial_snapshot_fields_persist_on_published_report()
    {
        var report = new ProjectStatusReport
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            TenantId = _tenantId,
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            ReportState = ProjectStatusReportState.Published,
            CurrencyCodeSnapshot = "USD",
            BudgetAmountSnapshot = 100000m,
            ActualPaidSnapshot = 20000m,
            OutstandingPlannedPaymentsSnapshot = 30000m,
            PaymentsDueWithinPeriodSnapshot = 20000m,
            RemainingUnpaidAtPeriodEndSnapshot = 30000m,
            ActionsNeeded = "Follow up with vendor",
            SupportNeeded = "Escalate to committee",
            PreparedByUserId = _userId,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Set<ProjectStatusReport>().Add(report);
        await _context.SaveChangesAsync();

        var stored = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == report.Id);

        Assert.Equal("USD", stored.CurrencyCodeSnapshot);
        Assert.Equal(100000m, stored.BudgetAmountSnapshot);
        Assert.Equal(20000m, stored.ActualPaidSnapshot);
        Assert.Equal(30000m, stored.OutstandingPlannedPaymentsSnapshot);
        Assert.Equal(20000m, stored.PaymentsDueWithinPeriodSnapshot);
        Assert.Equal(30000m, stored.RemainingUnpaidAtPeriodEndSnapshot);
        Assert.Equal("Follow up with vendor", stored.ActionsNeeded);
        Assert.Equal("Escalate to committee", stored.SupportNeeded);
        Assert.Equal(ProjectStatusReportState.Published, stored.ReportState);
    }

    [Fact]
    public async Task Published_report_snapshot_remains_stable_after_subsequent_payment_change()
    {
        var report = new ProjectStatusReport
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            TenantId = _tenantId,
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            ReportState = ProjectStatusReportState.Published,
            CurrencyCodeSnapshot = "USD",
            BudgetAmountSnapshot = 100000m,
            ActualPaidSnapshot = 20000m,
            OutstandingPlannedPaymentsSnapshot = 30000m,
            PaymentsDueWithinPeriodSnapshot = 20000m,
            RemainingUnpaidAtPeriodEndSnapshot = 30000m,
            PreparedByUserId = _userId,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Set<ProjectStatusReport>().Add(report);
        await _context.SaveChangesAsync();

        var postPeriodSettlement = _context.Set<PaymentSettlement>()
            .First(item => item.SettlementDate > PeriodTo);
        postPeriodSettlement.Amount = 99999m;
        await _context.SaveChangesAsync();

        var stored = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == report.Id);

        Assert.Equal(20000m, stored.ActualPaidSnapshot);
        Assert.Equal(30000m, stored.OutstandingPlannedPaymentsSnapshot);
        Assert.Equal(20000m, stored.PaymentsDueWithinPeriodSnapshot);
        Assert.Equal(30000m, stored.RemainingUnpaidAtPeriodEndSnapshot);
    }

    [Fact]
    public async Task Draft_report_actions_and_support_survive_create_update_read()
    {
        var draft = new ProjectStatusReport
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            TenantId = _tenantId,
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            ReportState = ProjectStatusReportState.Draft,
            ActionsNeeded = "Follow up with vendor on delay",
            SupportNeeded = "Escalate to steering committee",
            PreparedByUserId = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Set<ProjectStatusReport>().Add(draft);
        await _context.SaveChangesAsync();

        var storedDraft = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == draft.Id);
        Assert.Equal("Follow up with vendor on delay", storedDraft.ActionsNeeded);
        Assert.Equal("Escalate to steering committee", storedDraft.SupportNeeded);

        storedDraft.ActionsNeeded = "Revised: Follow up with vendor on delay";
        storedDraft.SupportNeeded = "Revised: Escalate to steering committee";
        storedDraft.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var updatedDraft = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == draft.Id);
        Assert.Equal("Revised: Follow up with vendor on delay", updatedDraft.ActionsNeeded);
        Assert.Equal("Revised: Escalate to steering committee", updatedDraft.SupportNeeded);
        Assert.Equal(ProjectStatusReportState.Draft, updatedDraft.ReportState);
    }

    [Fact]
    public async Task Created_at_is_generated_at_creation_and_unchanged_after_edit_and_publish()
    {
        var draft = new ProjectStatusReport
        {
            Id = Guid.NewGuid(),
            ProjectId = _projectId,
            TenantId = _tenantId,
            PeriodFrom = PeriodFrom,
            PeriodTo = PeriodTo,
            ReportState = ProjectStatusReportState.Draft,
            ActionsNeeded = "Original actions",
            SupportNeeded = "Original support",
            PreparedByUserId = _userId,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Set<ProjectStatusReport>().Add(draft);
        await _context.SaveChangesAsync();

        var createdAt = draft.CreatedAt;

        // Simulate edit: CreatedAt should not be touched by UpdateAsync
        var storedDraft = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == draft.Id);
        storedDraft.ActionsNeeded = "Edited actions";
        storedDraft.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var afterEdit = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == draft.Id);
        Assert.Equal(createdAt, afterEdit.CreatedAt);
        Assert.Equal("Edited actions", afterEdit.ActionsNeeded);

        // Simulate publish: CreatedAt should not be touched by PublishAsync
        afterEdit.ReportState = ProjectStatusReportState.Published;
        afterEdit.PublishedAt = DateTime.UtcNow;
        afterEdit.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var afterPublish = await _context.Set<ProjectStatusReport>()
            .FirstAsync(item => item.Id == draft.Id);
        Assert.Equal(createdAt, afterPublish.CreatedAt);
        Assert.Equal(ProjectStatusReportState.Published, afterPublish.ReportState);
        Assert.NotNull(afterPublish.PublishedAt);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
