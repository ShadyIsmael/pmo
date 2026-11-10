using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectFinancialSummaryPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly TestManagementContextAccessor _management = new();
    private readonly Mock<IHttpContextAccessor> _http = new();

    public ProjectFinancialSummaryPersistenceTests()
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
            new Claim("permission", "StatusReport.View")
        ], "financial-summary-test"));
        _http.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
        _management.Set(new ManagementContext(_userId, _tenantId, ManagementPerspective.ProjectManager,
            ManagementViewMode.Native, null, null, _projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { _projectId })));
    }

    [Fact]
    public async Task Summary_uses_approved_fiscal_budget_and_only_records_inside_the_reporting_period()
    {
        await SeedProjectAsync();
        _context.ProjectAnnualBudgets.Add(AnnualBudget(2027, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30), 500_000m));
        _context.CostEntries.AddRange(
            Cost(new DateTime(2026, 10, 31), 700m),
            Cost(new DateTime(2026, 11, 1), 100m),
            Cost(new DateTime(2026, 11, 8), 250m),
            Cost(new DateTime(2026, 11, 9), 900m));
        _context.PurchaseCommitments.Add(new PurchaseCommitment
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
            PurchaseOrderNumber = "PO-001", Amount = 15_000m, Currency = "usd",
            CommitmentDate = new DateTime(2026, 10, 1), Status = "Open"
        });
        var obligation = new PaymentObligation
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
            ProjectName = "Test project", PaymentReference = "PAY-001", Purpose = "Test payment",
            Amount = 2_500m, Currency = "USD", DueDate = new DateTime(2026, 11, 6)
        };
        _context.PaymentObligations.Add(obligation);
        _context.PaymentSettlements.AddRange(
            Settlement(obligation.Id, new DateTime(2026, 10, 20), 400m),
            Settlement(obligation.Id, new DateTime(2026, 11, 5), 300m));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("USD", result.Value!.CurrencyCode);
        Assert.Equal(500_000m, result.Value.ApprovedBudget);
        Assert.Equal("approved", result.Value.BudgetStatus);
        Assert.Equal(350m, result.Value.Expenditure);
        Assert.Equal(15_000m, result.Value.CommittedCost);
        Assert.Equal(300m, result.Value.ActualPaid);
        Assert.Equal(1_800m, result.Value.OutstandingPlannedPayments);
        Assert.Equal(1_800m, result.Value.PaymentsDueWithinPeriod);
        Assert.Equal(1_800m, result.Value.RemainingUnpaidAtPeriodEnd);
        Assert.True(result.Value.HasFinancialData);
        Assert.Equal("available", result.Value.DataStatus);
    }

    [Fact]
    public async Task Summary_returns_explicit_empty_state_without_using_the_lifetime_project_ceiling()
    {
        await SeedProjectAsync(projectBudget: 900_000m);

        var result = await CreateService().GetFinancialSummaryAsync(_projectId,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value!.HasFinancialData);
        Assert.Equal("empty", result.Value.DataStatus);
        Assert.Null(result.Value.ApprovedBudget);
        Assert.Null(result.Value.Expenditure);
        Assert.Null(result.Value.CommittedCost);
    }

    [Fact]
    public async Task Summary_uses_configured_fiscal_boundaries_and_excludes_draft_budgets()
    {
        await SeedProjectAsync();
        _context.ProjectAnnualBudgets.AddRange(
            AnnualBudget(2027, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30), 80_000m),
            AnnualBudget(2028, new DateOnly(2027, 7, 1), new DateOnly(2028, 6, 30), 120_000m, status: ProjectAnnualBudgetStatuses.Draft));
        await _context.SaveChangesAsync();

        var result = await CreateService(fiscalYearStartMonth: 7).GetFinancialSummaryAsync(_projectId,
            new DateTime(2027, 6, 30), new DateTime(2027, 7, 1));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(80_000m, result.Value!.ApprovedBudget);
        Assert.Equal("partial", result.Value.BudgetStatus);
        Assert.Equal("partial", result.Value.DataStatus);
    }

    [Fact]
    public async Task Summary_combines_approved_budgets_when_a_reporting_period_crosses_configured_fiscal_years()
    {
        await SeedProjectAsync();
        _context.ProjectAnnualBudgets.AddRange(
            AnnualBudget(2027, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 30), 80_000m),
            AnnualBudget(2028, new DateOnly(2027, 7, 1), new DateOnly(2028, 6, 30), 120_000m));
        await _context.SaveChangesAsync();

        var result = await CreateService(fiscalYearStartMonth: 7).GetFinancialSummaryAsync(_projectId,
            new DateTime(2027, 6, 30), new DateTime(2027, 7, 1));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(200_000m, result.Value!.ApprovedBudget);
        Assert.Equal("approved", result.Value.BudgetStatus);
        Assert.Equal("available", result.Value.DataStatus);
    }

    [Fact]
    public async Task Summary_does_not_combine_foreign_currency_financial_records()
    {
        await SeedProjectAsync();
        _context.ProjectAnnualBudgets.Add(AnnualBudget(2027,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 700_000m, currency: "EUR"));
        _context.CostEntries.Add(new CostEntry
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
            Amount = 2_000m, Currency = "EUR", IncurredDate = new DateTime(2026, 11, 4),
            Type = "Actual", Status = "Posted", Category = "Materials"
        });
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("USD", result.Value!.CurrencyCode);
        Assert.Null(result.Value.ApprovedBudget);
        Assert.Null(result.Value.Expenditure);
        Assert.Equal("unavailable", result.Value.DataStatus);
    }

    [Fact]
    public async Task Summary_fails_closed_for_out_of_scope_and_cross_tenant_projects()
    {
        var outOfScopeId = Guid.NewGuid();
        var outsideScope = new Project
        {
            Id = outOfScopeId, TenantId = _tenantId, CreatedBy = _userId.ToString(),
            Name = "Out of scope", Code = "OUT-1", Currency = "USD"
        };
        _context.Projects.Add(outsideScope);
        await _context.SaveChangesAsync();
        _management.Set(_management.Current! with
        {
            Scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
        });

        var outOfScope = await CreateService().GetFinancialSummaryAsync(outOfScopeId,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));
        Assert.True(outOfScope.IsFailure);
        Assert.Equal(404, outOfScope.Error!.StatusCode);

        _management.Set(_management.Current! with
        {
            Scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { _projectId })
        });
        var foreignTenantProject = new Project
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CreatedBy = _userId.ToString(),
            Name = "Foreign tenant", Code = "FOREIGN-1", Currency = "USD"
        };
        var originalPrincipal = _http.Object.HttpContext!.User;
        try
        {
            _http.Object.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", foreignTenantProject.TenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
            ], "foreign-tenant-seed"));
            _context.Projects.Add(foreignTenantProject);
            await _context.SaveChangesAsync();
        }
        finally
        {
            _http.Object.HttpContext.User = originalPrincipal;
        }
        _management.Set(_management.Current! with
        {
            Scope = new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { foreignTenantProject.Id })
        });

        var crossTenant = await CreateService().GetFinancialSummaryAsync(foreignTenantProject.Id,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));
        Assert.True(crossTenant.IsFailure);
        Assert.Equal(404, crossTenant.Error!.StatusCode);
    }

    [Fact]
    public void Endpoint_requires_read_only_status_report_permission()
    {
        var method = typeof(ProjectController).GetMethod(nameof(ProjectController.GetFinancialSummary));
        Assert.NotNull(method);
        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("StatusReport.View", authorize?.Policy);
        Assert.DoesNotContain("StatusReport.Create", method.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy));
        Assert.DoesNotContain("StatusReport.Edit", method.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy));
        Assert.DoesNotContain("ProjectAnnualBudget.Approve", method.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy));
    }

    private async Task SeedProjectAsync(decimal? projectBudget = null)
    {
        _context.Projects.Add(new Project
        {
            Id = _projectId, TenantId = _tenantId, CreatedBy = _userId.ToString(),
            Name = "Financial summary project", Code = "FIN-1", Currency = "USD", Budget = projectBudget
        });
        await _context.SaveChangesAsync();
    }

    private ProjectAnnualBudget AnnualBudget(int fiscalYear, DateOnly start, DateOnly end, decimal amount,
        string currency = "USD", string status = ProjectAnnualBudgetStatuses.Approved) => new()
    {
        TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
        FiscalYear = fiscalYear, FiscalYearStartDate = start, FiscalYearEndDate = end,
        FinancialReference = $"FY-{fiscalYear}-{Guid.NewGuid():N}", Currency = currency, NewApprovedFunding = amount, Status = status
    };

    private CostEntry Cost(DateTime incurredDate, decimal amount) => new()
    {
        TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
        Amount = amount, Currency = "USD", IncurredDate = incurredDate, Type = "Actual", Status = "Posted",
        Category = "Materials"
    };

    private PaymentSettlement Settlement(Guid obligationId, DateTime date, decimal amount) => new()
    {
        TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
        PaymentObligationId = obligationId, IdempotencyKey = Guid.NewGuid(), Amount = amount, Currency = "USD",
        SettlementDate = date, PaymentReference = $"SET-{Guid.NewGuid():N}"
    };

    private ProjectReportingService CreateService(int fiscalYearStartMonth = 1)
    {
        var laborLookup = new Mock<IProjectEvmLaborCostLookup>();
        laborLookup.Setup(item => item.GetAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<ProjectEvmAssignmentInput>>(), It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<bool>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new ProjectEvmLaborCostSummary(null, null, null, null, null, null, "NO_ACTUAL_LABOR_DATA", false));
        var tenantFiscalYear = new Mock<ITenantFiscalYearService>();
        tenantFiscalYear.Setup(item => item.GetStartMonthAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiscalYearStartMonth);
        return new ProjectReportingService(_context, Mock.Of<IAuditService>(), Mock.Of<IProjectEvmPlanLookup>(), laborLookup.Object,
            new ProjectEvmDirectCostLookup(_context), _management, tenantFiscalYear.Object, new FiscalYearCalendarService());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class TestManagementContextAccessor : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; }
        public void Set(ManagementContext context) => Current = context;
    }
}
