using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.CostManagement;

/// <summary>
/// P0-04 regression tests (source remediation). Execution deferred to consolidated verification.
/// Validates confirmed financial data-integrity invariants:
/// - D1: Pending (submitted) reservations must be treated as budget-consuming for availability/over-submission prevention.
/// - D2: BudgetAuthority uses authoritative Project.Budget lifetime ceiling and rejects missing ceiling.
/// - B: Calculation paths must not silently substitute GBP; missing authoritative currency is surfaced.
/// </summary>
public sealed class P004FinancialDataIntegrityTests
{
    [Fact]
    public void PaymentRequestWorkflowPolicy_ReservesBudget_For_Pending_And_Approved_Only()
    {
        Assert.True(PaymentRequestWorkflowPolicy.ReservesBudget(PaymentRequestStatuses.Pending));
        Assert.True(PaymentRequestWorkflowPolicy.ReservesBudget(PaymentRequestStatuses.Approved));
        Assert.False(PaymentRequestWorkflowPolicy.ReservesBudget(PaymentRequestStatuses.Draft));
        Assert.False(PaymentRequestWorkflowPolicy.ReservesBudget(PaymentRequestStatuses.Rejected));
        Assert.False(PaymentRequestWorkflowPolicy.ReservesBudget(PaymentRequestStatuses.Cancelled));
        Assert.False(PaymentRequestWorkflowPolicy.ReservesBudget(PaymentRequestStatuses.Reversed));
    }

    [Fact]
    public async Task BudgetAuthority_Allocation_Rejects_When_Project_Ceiling_Missing()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var projects = new FakeProjectLookup(new ProjectReference(Guid.NewGuid(), "P", "USD", CurrentApprovedProjectBudget: null));

        var error = await BudgetAuthority.ValidateAllocationAsync(db, projects, projects.Project.Id, null, 100m, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("ceiling", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BudgetAuthority_Allocation_Rejects_When_Exceeding_Authoritative_Ceiling()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var projects = new FakeProjectLookup(new ProjectReference(Guid.NewGuid(), "P", "USD", CurrentApprovedProjectBudget: 500m));

        var error = await BudgetAuthority.ValidateAllocationAsync(db, projects, projects.Project.Id, null, 600m, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Contains("exceeds", error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GBP", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<NEXUS.Modules.CostManagement.Domain.Budget> Budgets => Set<NEXUS.Modules.CostManagement.Domain.Budget>();
        public DbSet<NEXUS.Modules.CostManagement.Domain.CostEntry> CostEntries => Set<NEXUS.Modules.CostManagement.Domain.CostEntry>();
    }

    private sealed class FakeProjectLookup(ProjectReference project) : IProjectReferenceLookup
    {
        public ProjectReference Project { get; } = project;
        public Task<ProjectReference?> FindAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(projectId == Project.Id ? Project : null);
        public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
        public Task<IReadOnlyList<ProjectReference>> GetFinancialReferencesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectReference>>([Project]);
        public Task<IReadOnlyDictionary<Guid, ProjectReference>> GetReferencesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, ProjectReference>>(projectIds.Contains(Project.Id)
                ? new Dictionary<Guid, ProjectReference> { [Project.Id] = Project }
                : new Dictionary<Guid, ProjectReference>());
    }
}
