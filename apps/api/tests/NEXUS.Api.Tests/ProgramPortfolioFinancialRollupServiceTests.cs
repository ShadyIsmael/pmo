using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.ProgramManagement;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Api.Tests;

/// <summary>
/// Focused backend tests for the Program/Portfolio financial rollup. Annual-budget
/// lifecycle figures are asserted against the rollup's own reconciliation logic while
/// lifetime ceiling metrics are asserted against the authoritative Task 01
/// <see cref="NEXUS.Modules.CostManagement.Application.FinancialReportingCalculator"/>
/// that the rollup reuses. All data is seeded into an isolated InMemory database under a
/// resolved tenant so the EF global tenant filter behaves exactly as in production.
/// </summary>
[Collection("Static tenant resolution")]
public sealed class ProgramPortfolioFinancialRollupServiceTests : IDisposable
{
    private const int FiscalYear = 2026;
    private const string Usd = "USD";
    private const string Eur = "EUR";

    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _programId = Guid.NewGuid();
    private readonly Guid _portfolioId = Guid.NewGuid();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly ManagementContext _management;

    public ProgramPortfolioFinancialRollupServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rollup-{Guid.NewGuid():N}").Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        ConfigureTenant(_tenantId);
        _management = new ManagementContext(_userId, _tenantId, ManagementPerspective.ProgramManager,
            ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>()))
        {
            CanReadAllTenantPortfolioProgramData = true
        };
    }

    // ---- Scenario 1: Program financial totals ----------------------------------------

    [Fact]
    public async Task Program_totals_reflect_approved_annual_budget_and_reservations()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Program total project", Usd, budget: 1000m, programId: _programId);
        var budgetId = await SeedAnnualBudgetAsync(projectId, newFunding: 5000m);
        await SeedApprovedRequestAsync(projectId, amount: 1000m, annualBudgetId: budgetId);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Complete", result.Value!.Coverage);
        Assert.Equal(1, result.Value.IncludedProjectCount);
        var project = Assert.Single(result.Value.Projects);
        Assert.Equal("Available", project.DataStatus);
        Assert.Equal(5000m, project.ApprovedAnnualBudget);
        Assert.Equal(1000m, project.EffectiveActiveAllocations);
        Assert.Equal(1000m, project.ApprovedRequestAmount);
        Assert.Equal(0m, project.GrossActualPaid);
        Assert.Equal(1000m, project.LifetimeCeiling);
        Assert.Equal(0m, project.LifetimeReserved);
        Assert.Equal(1000m, project.AvailableLifetimeBudget);
        var group = Assert.Single(result.Value.CurrencyGroups);
        Assert.Equal(Usd, group.Currency);
        Assert.Equal(1, group.ProjectCount);
        Assert.Equal(5000m, group.ApprovedAnnualBudget);
        Assert.Equal(1000m, group.LifetimeCeiling);
    }

    // ---- Scenario 2: Portfolio direct projects ---------------------------------------

    [Fact]
    public async Task Portfolio_includes_direct_projects()
    {
        await SeedPortfolioAsync();
        await SeedProgramAsync(portfolioId: _portfolioId);
        var directId = await SeedProjectAsync("Direct project", Usd, budget: 1000m, programId: null, portfolioId: _portfolioId);
        await SeedAnnualBudgetAsync(directId, newFunding: 2000m);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetPortfolioAsync(_portfolioId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(directId, project.ProjectId);
        Assert.True(project.IsDirectProject);
        Assert.Null(project.ProgramId);
    }

    // ---- Scenario 3: Portfolio projects through programs -----------------------------

    [Fact]
    public async Task Portfolio_includes_projects_through_programs()
    {
        await SeedPortfolioAsync();
        await SeedProgramAsync(portfolioId: _portfolioId);
        var projectId = await SeedProjectAsync("Program project", Usd, budget: 1000m, programId: _programId, portfolioId: null);
        await SeedAnnualBudgetAsync(projectId, newFunding: 3000m);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetPortfolioAsync(_portfolioId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(projectId, project.ProjectId);
        Assert.False(project.IsDirectProject);
        Assert.Equal(_programId, project.ProgramId);
    }

    // ---- Scenario 4: Duplicate project prevention ------------------------------------

    [Fact]
    public async Task Project_reachable_via_direct_and_program_is_counted_once()
    {
        await SeedPortfolioAsync();
        await SeedProgramAsync(portfolioId: _portfolioId);
        // Same project satisfies both the direct PortfolioId and the program membership.
        var projectId = await SeedProjectAsync("Dual membership project", Usd, budget: 1000m, programId: _programId, portfolioId: _portfolioId);
        await SeedAnnualBudgetAsync(projectId, newFunding: 1000m);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetPortfolioAsync(_portfolioId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value!.IncludedProjectCount);
        Assert.Single(result.Value.Projects);
        Assert.Equal(1, result.Value.ProjectsTotalCount);
        Assert.Single(result.Value.CurrencyGroups);
    }

    // ---- Scenario 5: Currency grouping -----------------------------------------------

    [Fact]
    public async Task Currency_groups_separate_projects_by_currency()
    {
        await SeedProgramAsync();
        var usdProject = await SeedProjectAsync("USD project", Usd, budget: 1000m, programId: _programId);
        var eurProject = await SeedProjectAsync("EUR project", Eur, budget: 2000m, programId: _programId);
        await SeedAnnualBudgetAsync(usdProject, newFunding: 1000m, currency: Usd);
        await SeedAnnualBudgetAsync(eurProject, newFunding: 2000m, currency: Eur);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.HasMixedCurrencies);
        Assert.Equal(2, result.Value.CurrencyGroups.Count);
        Assert.Contains(result.Value.CurrencyGroups, group => group.Currency == Usd && group.ProjectCount == 1 && group.LifetimeCeiling == 1000m);
        Assert.Contains(result.Value.CurrencyGroups, group => group.Currency == Eur && group.ProjectCount == 1 && group.LifetimeCeiling == 2000m);
    }

    // ---- Scenario 6: Currency mismatch -----------------------------------------------

    [Fact]
    public async Task Currency_mismatch_marks_project_unavailable()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Mismatch project", Usd, budget: 1000m, programId: _programId);
        await SeedAnnualBudgetAsync(projectId, newFunding: 5000m, currency: Eur);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal("FinancialDataRequiresReconciliation", project.DataStatus);
        Assert.False(project.IsAvailable);
        Assert.Null(project.ApprovedAnnualBudget);
        Assert.Null(project.LifetimeCeiling);
        Assert.Empty(result.Value.CurrencyGroups);
    }

    // ---- Scenario 7: Fiscal-year filtering -------------------------------------------

    [Fact]
    public async Task Fiscal_year_selection_uses_the_requested_budget_year()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Fiscal year project", Usd, budget: 1000m, programId: _programId);
        await SeedAnnualBudgetAsync(projectId, newFunding: 5000m, fiscalYear: FiscalYear);
        await SeedAnnualBudgetAsync(projectId, newFunding: 9000m, fiscalYear: FiscalYear + 1);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(5000m, project.ApprovedAnnualBudget);
        Assert.Equal("Available", project.DataStatus);
    }

    // ---- Scenario 8: Lifetime reservation calculations -------------------------------

    [Fact]
    public async Task Lifetime_reserved_reflects_active_reservations()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Reservation project", Usd, budget: 1000m, programId: _programId);
        await SeedAnnualBudgetAsync(projectId, newFunding: 1000m);
        await SeedPlannedPaymentAsync(projectId, totalAmount: 400m, reservedAmount: 400m, releasedAmount: 200m);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(200m, project.LifetimeReserved);
    }

    // ---- Scenario 9: Outstanding commitments -----------------------------------------

    [Fact]
    public async Task Outstanding_commitments_reflect_unpaid_installments()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Commitment project", Usd, budget: 1000m, programId: _programId);
        await SeedAnnualBudgetAsync(projectId, newFunding: 1000m);
        await SeedPlannedPaymentAsync(projectId, totalAmount: 300m, reservedAmount: 300m, releasedAmount: 0m,
            installment: (amount: 300m, actualPaid: 0m));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(300m, project.OutstandingCommitments);
    }

    // ---- Scenario 10: Gross / net actual paid ----------------------------------------

    [Fact]
    public async Task Gross_and_net_actual_paid_count_confirmed_settlements_once()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Settlement project", Usd, budget: 1000m, programId: _programId);
        var budgetId = await SeedAnnualBudgetAsync(projectId, newFunding: 1000m);
        var requestId = await SeedApprovedRequestAsync(projectId, amount: 1000m, annualBudgetId: budgetId);
        await SeedSettlementAsync(projectId, requestId, amount: 600m, new DateTime(FiscalYear, 6, 15));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(600m, project.GrossActualPaid);
        Assert.Equal(600m, project.NetActualPaid);
        Assert.Equal(0m, project.ExecutedRefundsCorrections);
        Assert.Equal(400m, project.AvailableLifetimeBudget);
    }

    // ---- Scenario 11: Executed refunds / corrections ---------------------------------

    [Fact]
    public async Task Executed_refunds_and_corrections_reduce_net_position()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Refund project", Usd, budget: 1000m, programId: _programId);
        await SeedAnnualBudgetAsync(projectId, newFunding: 1000m);
        await SeedAdjustmentAsync(projectId, PaymentSettlementAdjustment.Refund, amount: 100m);
        await SeedAdjustmentAsync(projectId, PaymentSettlementAdjustment.Correction, amount: 40m);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        // Refund is negative, Correction is positive: -100 + 40 = -60.
        Assert.Equal(-60m, project.ExecutedRefundsCorrections);
    }

    // ---- Scenario 12: Budget line utilization ----------------------------------------

    [Fact]
    public async Task Budget_line_utilization_is_computed_from_aggregate_amounts()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Budget line project", Usd, budget: 1000m, programId: _programId);
        var budgetId = await SeedAnnualBudgetAsync(projectId, newFunding: 1000m);
        var lineId = await SeedBudgetLineAsync(projectId, budgetId, authorizedLimit: 1000m);
        var planId = await SeedPlannedPaymentAsync(projectId, totalAmount: 1000m, reservedAmount: 1000m, releasedAmount: 0m,
            installment: (amount: 1000m, actualPaid: 0m));
        var installmentId = (await _context.Set<PlannedPaymentInstallment>().AsNoTracking()
            .FirstAsync(item => item.PlannedPaymentId == planId)).Id;
        var requestId = await SeedApprovedRequestAsync(projectId, amount: 1000m, annualBudgetId: budgetId,
            installmentId: installmentId, budgetLineId: lineId);
        await SeedSettlementAsync(projectId, requestId, amount: 1m, new DateTime(FiscalYear, 6, 15), installmentId: installmentId);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var project = Assert.Single(result.Value!.Projects);
        Assert.Equal(100m, project.BudgetLineUtilization);
    }

    // ---- Scenario 13: Partial / unavailable data quality -----------------------------

    [Fact]
    public async Task Missing_annual_budget_reports_unavailable_quality()
    {
        await SeedProgramAsync();
        var projectId = await SeedProjectAsync("Missing budget project", Usd, budget: 1000m, programId: _programId);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value!.ProjectsMissingApprovedAnnualBudget);
        var project = Assert.Single(result.Value.Projects);
        Assert.Equal("MissingApprovedAnnualBudget", project.DataStatus);
        Assert.False(project.IsAvailable);
        Assert.Null(project.ApprovedAnnualBudget);
        // The lifetime ceiling is authoritative from Project.Budget and is independent of the
        // missing annual breakdown, so it remains available while annual figures are withheld.
        Assert.Equal(1000m, project.LifetimeCeiling);
    }

    // ---- Scenario 14: Tenant / organization isolation --------------------------------

    [Fact]
    public async Task Rollup_excludes_projects_from_another_tenant()
    {
        await SeedProgramAsync();
        var ownProject = await SeedProjectAsync("Own tenant project", Usd, budget: 1000m, programId: _programId);
        await SeedAnnualBudgetAsync(ownProject, newFunding: 1000m);

        // Seed a foreign-tenant project that points at the same program.
        ConfigureTenant(_otherTenantId);
        await SeedProjectAsync("Foreign tenant project", Usd, budget: 5000m, programId: _programId, tenantId: _otherTenantId);

        ConfigureTenant(_tenantId);
        var result = await CreateService().GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, result.Value!.IncludedProjectCount);
        var project = Assert.Single(result.Value.Projects);
        Assert.Equal(ownProject, project.ProjectId);
        Assert.Equal(1000m, project.LifetimeCeiling);
    }

    // ---- Scenario 15: Parent permission enforcement ----------------------------------

    [Fact]
    public async Task Missing_parent_permission_denies_rollup()
    {
        await SeedProgramAsync();

        var withoutParent = await CreateService(new[] { "ProgramFinancialRollup.View" })
            .GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);
        Assert.True(withoutParent.IsFailure);

        var withoutRollup = await CreateService(new[] { "Program.View" })
            .GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);
        Assert.True(withoutRollup.IsFailure);

        var withBoth = await CreateService(new[] { "Program.View", "ProgramFinancialRollup.View" })
            .GetProgramAsync(_programId, FiscalYear, 1, 25, CancellationToken.None);
        Assert.True(withBoth.IsSuccess, withBoth.Error?.Message);
    }

    // ---- Fixture helpers -------------------------------------------------------------

    private static string[] DefaultPermissions() =>
        ["Program.View", "ProgramFinancialRollup.View", "Portfolio.View", "PortfolioFinancialRollup.View"];

    private ProgramPortfolioFinancialRollupService CreateService(IEnumerable<string>? permissions = null)
    {
        var effective = (permissions ?? DefaultPermissions()).ToHashSet(StringComparer.Ordinal);
        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService
            .Setup(item => item.GetEffectivePermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(effective);

        var fiscalYearService = new Mock<ITenantFiscalYearService>();
        fiscalYearService
            .Setup(item => item.GetPreviewAsync(_tenantId, It.IsAny<int?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantFiscalYearPreview(
                FiscalYearSetting.DefaultStartMonth, "UTC", FiscalYear, $"FY {FiscalYear}",
                new DateOnly(FiscalYear, 1, 1), new DateOnly(FiscalYear, 12, 31), new DateOnly(FiscalYear + 1, 1, 1)));

        return new ProgramPortfolioFinancialRollupService(
            _context, new TestManagementContextAccessor(_management), permissionService.Object,
            fiscalYearService.Object, new FiscalYearCalendarService());
    }

    private void ConfigureTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        ], "rollup-test"));
        _http.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    private async Task SeedProgramAsync(Guid? portfolioId = null)
    {
        _context.Set<ProgramEntity>().Add(new ProgramEntity
        {
            Id = _programId, TenantId = _tenantId, CreatedBy = _userId.ToString(),
            PublicId = Guid.NewGuid(), Name = "Rollup program", PortfolioId = portfolioId ?? _portfolioId,
            Currency = Usd, Status = "Active"
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedPortfolioAsync()
    {
        _context.Set<PortfolioEntity>().Add(new PortfolioEntity
        {
            Id = _portfolioId, TenantId = _tenantId, CreatedBy = _userId.ToString(),
            PublicId = Guid.NewGuid(), Name = "Rollup portfolio", Code = $"PF-{Guid.NewGuid():N}", Currency = Usd
        });
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> SeedProjectAsync(
        string name, string currency, decimal? budget, Guid? programId, Guid? portfolioId = null, Guid? tenantId = null)
    {
        var id = Guid.NewGuid();
        _context.Projects.Add(new Project
        {
            Id = id, TenantId = tenantId ?? _tenantId, CreatedBy = _userId.ToString(), PublicId = Guid.NewGuid(),
            Name = name, Code = $"PRJ-{Guid.NewGuid():N}", ProgramId = programId, PortfolioId = portfolioId,
            Currency = currency, Budget = budget, Status = "Active"
        });
        await _context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedAnnualBudgetAsync(
        Guid projectId, decimal newFunding, string currency = Usd, int fiscalYear = FiscalYear)
    {
        var id = Guid.NewGuid();
        _context.ProjectAnnualBudgets.Add(new ProjectAnnualBudget
        {
            Id = id, TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
            FiscalYear = fiscalYear, FiscalYearStartDate = new DateOnly(fiscalYear, 1, 1),
            FiscalYearEndDate = new DateOnly(fiscalYear, 12, 31),
            FinancialReference = $"FY-{fiscalYear}-{Guid.NewGuid():N}", Currency = currency,
            NewApprovedFunding = newFunding, Status = ProjectAnnualBudgetStatuses.Approved
        });
        await _context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedApprovedRequestAsync(
        Guid projectId, decimal amount, Guid? annualBudgetId = null, Guid? installmentId = null, Guid? budgetLineId = null)
    {
        var requestId = Guid.NewGuid();
        _context.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = requestId, TenantId = _tenantId, CreatedBy = _userId.ToString(), Type = "ProjectPaymentRequest",
            EntityType = "Project", EntityId = projectId, Status = PaymentRequestStatuses.Approved,
            Amount = amount, Currency = Usd
        });
        _context.Set<ProjectPaymentRequestDetail>().Add(new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId, TenantId = _tenantId, ProjectId = projectId,
            RequestKind = ProjectPaymentRequestKinds.PaymentRequest, ProjectAnnualBudgetId = annualBudgetId,
            AnnualBudgetFiscalYear = annualBudgetId.HasValue ? FiscalYear : null,
            PlannedPaymentInstallmentId = installmentId, ProjectAnnualBudgetLineId = budgetLineId
        });
        await _context.SaveChangesAsync();
        return requestId;
    }

    private async Task<Guid> SeedPlannedPaymentAsync(
        Guid projectId, decimal totalAmount, decimal reservedAmount, decimal releasedAmount,
        (decimal amount, decimal actualPaid)? installment = null)
    {
        var planId = Guid.NewGuid();
        _context.Set<PlannedPayment>().Add(new PlannedPayment
        {
            Id = planId, TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
            Reference = $"PLAN-{Guid.NewGuid():N}", Description = "rollup fixture", Currency = Usd,
            TotalAmount = totalAmount, State = PlannedPaymentStates.Registered,
            Reservation = new PlannedPaymentLifetimeReservation
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
                PlannedPaymentId = planId, ReservedAmount = reservedAmount, ReleasedAmount = releasedAmount
            }
        });
        if (installment is { } value)
        {
            _context.Set<PlannedPaymentInstallment>().Add(new PlannedPaymentInstallment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
                PlannedPaymentId = planId, Sequence = 1, Amount = value.amount, ActualPaid = value.actualPaid
            });
        }
        await _context.SaveChangesAsync();
        return planId;
    }

    private async Task SeedSettlementAsync(
        Guid projectId, Guid requestId, decimal amount, DateTime settlementDate, string currency = Usd, Guid? installmentId = null)
    {
        _context.PaymentSettlements.Add(new PaymentSettlement
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
            ProjectPaymentRequestId = requestId, PlannedPaymentInstallmentId = installmentId,
            IdempotencyKey = Guid.NewGuid(), Amount = amount, Currency = currency,
            SettlementDate = settlementDate, PaymentReference = $"SET-{Guid.NewGuid():N}"
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedAdjustmentAsync(Guid projectId, string adjustmentType, decimal amount)
    {
        _context.Set<PaymentSettlementAdjustment>().Add(new PaymentSettlementAdjustment
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
            OriginalSettlementId = Guid.NewGuid(), AdjustmentType = adjustmentType, Amount = amount,
            Currency = Usd, Reason = "rollup fixture", EffectiveDate = new DateTime(FiscalYear, 6, 20),
            RecordedAt = new DateTime(FiscalYear, 6, 20), Status = PaymentSettlementAdjustment.Executed,
            ExecutionDate = new DateTime(FiscalYear, 6, 20)
        });
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> SeedBudgetLineAsync(Guid projectId, Guid annualBudgetId, decimal authorizedLimit)
    {
        var id = Guid.NewGuid();
        _context.Set<ProjectAnnualBudgetLine>().Add(new ProjectAnnualBudgetLine
        {
            Id = id, TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
            ProjectAnnualBudgetId = annualBudgetId, Reference = $"LINE-{Guid.NewGuid():N}",
            Description = "rollup fixture", AuthorizedLimit = authorizedLimit
        });
        await _context.SaveChangesAsync();
        return id;
    }

    public void Dispose()
    {
        _context.Dispose();
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
    }

    private sealed class TestManagementContextAccessor : IManagementContextAccessor
    {
        public TestManagementContextAccessor(ManagementContext context) => Current = context;
        public ManagementContext? Current { get; private set; }
        public void Set(ManagementContext context) => Current = context;
    }
}