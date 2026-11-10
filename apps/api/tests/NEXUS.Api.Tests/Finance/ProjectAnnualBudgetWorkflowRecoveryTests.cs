using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Api.Finance;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectAnnualBudgetWorkflowRecoveryTests
{
    [Fact]
    public async Task First_budget_guidance_uses_current_tenant_fiscal_year_and_editable_project_ceiling_proposal()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int currentFiscalYear = 2027;
        const decimal projectCeiling = 1_250_000m;

        await using var context = CreateContext();
        context.Projects.Add(Project(tenantId, projectId, projectCeiling));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.View");

        var result = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(currentFiscalYear, result.Value!.CurrentFiscalYear);
        Assert.Equal(startMonth, result.Value.FiscalYearStartMonth);
        Assert.Equal<int?>(currentFiscalYear, result.Value.SuggestedFiscalYear);
        Assert.Equal<decimal?>(projectCeiling, result.Value.SuggestedNewFunding);
        Assert.Equal("ProjectLifetimeCeiling", result.Value.SuggestedNewFundingSource);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task First_budget_is_saved_as_a_draft_without_approval_or_ceiling_changes()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int currentFiscalYear = 2027;
        const decimal projectCeiling = 1_250_000m;
        const decimal revisedAllocation = 900_000m;

        await using var context = CreateContext();
        var project = Project(tenantId, projectId, projectCeiling);
        context.AddRange(project, Currency(tenantId));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, "Budget Owner",
            new CreateProjectAnnualBudgetDto(currentFiscalYear, revisedAllocation, 0m), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetStatuses.Draft, result.Value!.Status);
        Assert.Equal(new DateOnly(2026, 7, 1), result.Value.FiscalYearStartDate);
        Assert.Equal(new DateOnly(2027, 6, 30), result.Value.FiscalYearEndDate);
        Assert.Equal(revisedAllocation, result.Value.NewApprovedFunding);
        Assert.Equal(0m, result.Value.ApprovedCarryForward);
        Assert.Null(result.Value.ApprovedAtUtc);
        Assert.False(result.Value.CanEdit);
        Assert.False(result.Value.CanSubmit);
        Assert.False(result.Value.CanApprove);
        Assert.False(result.Value.CanRevise);
        Assert.Equal(projectCeiling, project.Budget);

        var persisted = await context.ProjectAnnualBudgets.IgnoreQueryFilters().SingleAsync(item => item.ProjectId == projectId);
        Assert.Equal(ProjectAnnualBudgetStatuses.Draft, persisted.Status);
        Assert.Equal(revisedAllocation, persisted.NewApprovedFunding);
        Assert.Null(persisted.ApprovedAt);
        Assert.Equal(projectCeiling, (await context.Projects.IgnoreQueryFilters().SingleAsync(item => item.Id == projectId)).Budget);
    }

    [Fact]
    public async Task First_budget_must_use_tenants_configured_current_fiscal_year()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int currentFiscalYear = 2027;

        await using var context = CreateContext();
        context.AddRange(Project(tenantId, projectId, 1_000m), Currency(tenantId));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, null,
            new CreateProjectAnnualBudgetDto(currentFiscalYear + 1, 500m, 0m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains($"FY {currentFiscalYear}", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await context.ProjectAnnualBudgets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task First_budget_cannot_exceed_the_project_lifetime_ceiling()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int currentFiscalYear = 2027;

        await using var context = CreateContext();
        context.AddRange(Project(tenantId, projectId, 1_000m), Currency(tenantId));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, null,
            new CreateProjectAnnualBudgetDto(currentFiscalYear, 1_001m, 0m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains("lifetime financial ceiling", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.ProjectAnnualBudgets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Duplicate_project_fiscal_year_budget_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int currentFiscalYear = 2027;

        await using var context = CreateContext();
        context.AddRange(
            Project(tenantId, projectId, 1_000m),
            Currency(tenantId),
            AnnualBudget(tenantId, projectId, currentFiscalYear, startMonth, 500m, ProjectAnnualBudgetStatuses.Draft));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, null,
            new CreateProjectAnnualBudgetDto(currentFiscalYear, 500m, 0m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains("already exists", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await context.ProjectAnnualBudgets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Later_year_without_approved_prior_closing_is_rejected_without_synthesizing_carry_forward()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int priorFiscalYear = 2026;
        const int currentFiscalYear = 2027;

        await using var context = CreateContext();
        context.AddRange(
            Project(tenantId, projectId, 100_000m),
            Currency(tenantId),
            AnnualBudget(tenantId, projectId, priorFiscalYear, startMonth, 80_000m, ProjectAnnualBudgetStatuses.Approved));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, null,
            new CreateProjectAnnualBudgetDto(currentFiscalYear, 10_000m, 0m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains("approve and close", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"FY {priorFiscalYear}", result.Error.Message, StringComparison.Ordinal);
        Assert.Single(await context.ProjectAnnualBudgets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Closed_prior_budget_without_an_approved_closing_record_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int priorFiscalYear = 2026;
        const int nextFiscalYear = 2027;

        await using var context = CreateContext();
        var prior = AnnualBudget(tenantId, projectId, priorFiscalYear, startMonth, 80_000m,
            ProjectAnnualBudgetStatuses.Approved);
        prior.ClosedAtUtc = DateTime.UtcNow;
        prior.ClosedBy = actorId;
        context.AddRange(
            Project(tenantId, projectId, 100_000m),
            Currency(tenantId),
            prior);
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            nextFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, null,
            new CreateProjectAnnualBudgetDto(nextFiscalYear, 10_000m, 0m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains("no approved closing", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await context.ProjectAnnualBudgets.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Annual_budget_created_by_approved_closing_is_reused_without_recalculating_carry_forward()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;
        const int priorFiscalYear = 2026;
        const int nextFiscalYear = 2027;
        const decimal carryForward = 5_000m;
        const decimal newFunding = 25_000m;
        const decimal adjustments = 1_000m;

        await using var context = CreateContext();
        var source = AnnualBudget(tenantId, projectId, priorFiscalYear, startMonth, 80_000m,
            ProjectAnnualBudgetStatuses.Approved);
        var destination = AnnualBudget(tenantId, projectId, nextFiscalYear, startMonth, newFunding,
            ProjectAnnualBudgetStatuses.Draft);
        destination.ApprovedCarryForward = carryForward;
        destination.ApprovedReservedCarryForward = 2_000m;
        destination.ApprovedAdjustments = adjustments;
        var closing = new ProjectAnnualBudgetClosing
        {
            TenantId = tenantId,
            ProjectId = projectId,
            SourceAnnualBudgetId = source.Id,
            FiscalYear = priorFiscalYear,
            FiscalYearStartDate = source.FiscalYearStartDate,
            FiscalYearEndDate = source.FiscalYearEndDate,
            DestinationAnnualBudgetId = destination.Id,
            DestinationFiscalYear = nextFiscalYear,
            Currency = "USD",
            Status = ProjectAnnualBudgetClosingStatuses.Closed,
            ApprovedFreeCarryForward = 3_000m,
            ApprovedReservedCarryForward = 2_000m,
            NextFiscalYearNewFunding = newFunding,
            NextFiscalYearAdjustments = adjustments
        };
        source.ClosedAtUtc = DateTime.UtcNow;
        source.ClosedByClosingId = closing.Id;
        context.AddRange(Project(tenantId, projectId, 200_000m), Currency(tenantId), source, destination, closing);
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            nextFiscalYear, "ProjectAnnualBudget.Create");

        var result = await workflow.CreateAsync(tenantId, projectId, actorId, null,
            new CreateProjectAnnualBudgetDto(nextFiscalYear, 12_000m, 0m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains("approved prior-year closing", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await context.ProjectAnnualBudgets.IgnoreQueryFilters().CountAsync());
        var preservedOpening = await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == destination.Id);
        Assert.Equal(carryForward, preservedOpening.ApprovedCarryForward);
        Assert.Equal(2_000m, preservedOpening.ApprovedReservedCarryForward);
        Assert.Equal(newFunding, preservedOpening.NewApprovedFunding);
        Assert.Equal(adjustments, preservedOpening.ApprovedAdjustments);
    }

    [Theory]
    [InlineData(2026, 2027, 2026)]
    [InlineData(2027, 2027, 2027)]
    public async Task Missing_close_guidance_identifies_latest_fiscal_year_before_next_year_can_open(
        int latestFiscalYear, int currentFiscalYear, int? expectedMissingClosingFiscalYear)
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        const int startMonth = 7;

        await using var context = CreateContext();
        context.AddRange(
            Project(tenantId, projectId, 100_000m),
            AnnualBudget(tenantId, projectId, latestFiscalYear, startMonth, 80_000m, ProjectAnnualBudgetStatuses.Draft));
        await context.SaveChangesAsync();
        var workflow = CreateWorkflow(context, tenantId, projectId, actorId, startMonth,
            currentFiscalYear, "ProjectAnnualBudget.View");

        var result = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(expectedMissingClosingFiscalYear, result.Value!.MissingClosingFiscalYear);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        return new AppDbContext(options.Options);
    }

    private static ProjectAnnualBudgetWorkflow CreateWorkflow(
        AppDbContext context,
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        int fiscalYearStartMonth,
        int currentFiscalYear,
        params string[] permissions)
    {
        var calendar = new FiscalYearCalendarService();
        var currentPeriod = calendar.ForFiscalYear(currentFiscalYear, fiscalYearStartMonth);
        var tenantFiscalYear = new Mock<ITenantFiscalYearService>();
        tenantFiscalYear.Setup(service => service.GetStartMonthAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiscalYearStartMonth);
        tenantFiscalYear.Setup(service => service.ResolveForTimestampAsync(tenantId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentPeriod);

        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.ToHashSet(StringComparer.Ordinal));

        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        return new ProjectAnnualBudgetWorkflow(context, tenantFiscalYear.Object, calendar,
            permissionService.Object, management, audit.Object,
            new NoopProjectAnnualBudgetApprovalNotificationPublisher(),
            Moq.Mock.Of<NEXUS.SharedKernel.Management.IManagementScopeResolver>());
    }

    private static Project Project(Guid tenantId, Guid projectId, decimal? budget) => new()
    {
        Id = projectId,
        TenantId = tenantId,
        Name = "Annual Budget Recovery",
        Code = "ABR",
        Budget = budget,
        Currency = "USD"
    };

    private static Currency Currency(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Code = "USD",
        Name = "US Dollar",
        Symbol = "$",
        IsBase = true
    };

    private static ProjectAnnualBudget AnnualBudget(
        Guid tenantId, Guid projectId, int fiscalYear, int startMonth, decimal funding, string status)
    {
        var period = new FiscalYearCalendarService().ForFiscalYear(fiscalYear, startMonth);
        return new ProjectAnnualBudget
        {
            TenantId = tenantId,
            ProjectId = projectId,
            FiscalYear = fiscalYear,
            FiscalYearStartDate = period.StartDate,
            FiscalYearEndDate = period.EndDate,
            FinancialReference = $"ABR-FY{fiscalYear}-AB",
            Currency = "USD",
            NewApprovedFunding = funding,
            Status = status
        };
    }
}
