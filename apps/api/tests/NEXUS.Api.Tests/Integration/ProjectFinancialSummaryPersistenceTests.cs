using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
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

[Collection("Static tenant resolution")]
public sealed class ProjectFinancialSummaryPersistenceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly TestManagementContextAccessor _management = new();
    private readonly Mock<IHttpContextAccessor> _http = new();

    public ProjectFinancialSummaryPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"financial-summary-{Guid.NewGuid():N}").Options;
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
    public void Annual_budget_reservation_remains_once_when_request_moves_from_pending_to_approved()
    {
        var budgetId = Guid.NewGuid();
        var pendingReservation = new AnnualBudgetRequestReservation(budgetId, PaymentRequestStatuses.Pending, 250m);
        var pendingTotal = AnnualBudgetRequestReservationCalculator.SumForBudget([pendingReservation], budgetId);
        var approvedReservation = pendingReservation with { Status = PaymentRequestStatuses.Approved };
        var approvedTotal = AnnualBudgetRequestReservationCalculator.SumForBudget([approvedReservation], budgetId);
        var otherStatusRequests = new[]
        {
            new AnnualBudgetRequestReservation(budgetId, PaymentRequestStatuses.Draft, 50m),
            new AnnualBudgetRequestReservation(budgetId, PaymentRequestStatuses.Rejected, 75m),
            new AnnualBudgetRequestReservation(budgetId, PaymentRequestStatuses.ReturnedForRevision, 30m),
            new AnnualBudgetRequestReservation(budgetId, PaymentRequestStatuses.Cancelled, 25m),
            new AnnualBudgetRequestReservation(Guid.NewGuid(), PaymentRequestStatuses.Approved, 90m)
        };

        Assert.Equal(250m, pendingTotal);
        Assert.Equal(pendingTotal, approvedTotal);
        Assert.Equal(0m, AnnualBudgetRequestReservationCalculator.SumForBudget(otherStatusRequests, budgetId));
        Assert.Equal(0m, AnnualBudgetRequestReservationCalculator.SumForBudget([pendingReservation], null));
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

        var result = await CreateService(fiscalYearStartMonth: 7).GetFinancialSummaryAsync(_projectId,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("USD", result.Value!.CurrencyCode);
        Assert.Equal(500_000m, result.Value.ApprovedBudget);
        Assert.Equal("approved", result.Value.BudgetStatus);
        Assert.Equal(350m, result.Value.Expenditure);
        Assert.Equal(300m, result.Value.ActualPaid);
        Assert.Equal(1_800m, result.Value.OutstandingPlannedPayments);
        Assert.Equal(1_800m, result.Value.PaymentsDueWithinPeriod);
        Assert.Equal(1_800m, result.Value.RemainingUnpaidAtPeriodEnd);
        Assert.True(result.Value.HasFinancialData);
        Assert.Equal("available", result.Value.DataStatus);
    }

    [Fact]
    public async Task Summary_counts_active_installments_and_linked_settlements_once_as_of_period_end()
    {
        await SeedProjectAsync();
        var planId = Guid.NewGuid();
        var firstInstallmentId = Guid.NewGuid();
        var secondInstallmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var laterRequestId = Guid.NewGuid();
        _context.Set<PlannedPayment>().Add(new PlannedPayment
        {
            Id = planId, TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
            Reference = "PLAN-001", Description = "Installment reporting fixture", Currency = "USD",
            TotalAmount = 1_000m, State = PlannedPaymentStates.Locked,
            Reservation = new PlannedPaymentLifetimeReservation
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                PlannedPaymentId = planId, ReservedAmount = 1_000m
            }
        });
        _context.Set<PlannedPaymentInstallment>().AddRange(
            new PlannedPaymentInstallment
            {
                Id = firstInstallmentId, TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                PlannedPaymentId = planId, Sequence = 1, Amount = 400m, DueDateUtc = new DateTime(2026, 11, 6)
            },
            new PlannedPaymentInstallment
            {
                Id = secondInstallmentId, TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                PlannedPaymentId = planId, Sequence = 2, Amount = 600m, DueDateUtc = new DateTime(2026, 11, 12)
            });
        _context.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = requestId, TenantId = _tenantId, CreatedBy = _userId.ToString(), Type = "ProjectPaymentRequest",
            EntityType = "Project", EntityId = _projectId, Status = PaymentRequestStatuses.Approved,
            Amount = 400m, Currency = "USD"
        });
        _context.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = laterRequestId, TenantId = _tenantId, CreatedBy = _userId.ToString(), Type = "ProjectPaymentRequest",
            EntityType = "Project", EntityId = _projectId, Status = PaymentRequestStatuses.Approved,
            Amount = 600m, Currency = "USD"
        });
        _context.Set<ProjectPaymentRequestDetail>().Add(new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId, TenantId = _tenantId, ProjectId = _projectId,
            PlannedPaymentInstallmentId = firstInstallmentId
        });
        _context.Set<ProjectPaymentRequestDetail>().Add(new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = laterRequestId, TenantId = _tenantId, ProjectId = _projectId,
            PlannedPaymentInstallmentId = secondInstallmentId
        });
        _context.PaymentSettlements.Add(new PaymentSettlement
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
            ProjectPaymentRequestId = requestId, PlannedPaymentInstallmentId = firstInstallmentId,
            IdempotencyKey = Guid.NewGuid(), Amount = 100m, Currency = "USD",
            SettlementDate = new DateTime(2026, 11, 7), PaymentReference = "SET-PLAN-001"
        });
        _context.PaymentSettlements.Add(new PaymentSettlement
        {
            TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
            ProjectPaymentRequestId = laterRequestId, PlannedPaymentInstallmentId = secondInstallmentId,
            IdempotencyKey = Guid.NewGuid(), Amount = 50m, Currency = "USD",
            SettlementDate = new DateTime(2026, 11, 12), PaymentReference = "SET-PLAN-002"
        });
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId,
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 8));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(100m, result.Value!.ActualPaid);
        Assert.Equal(900m, result.Value.OutstandingPlannedPayments);
        Assert.Equal(300m, result.Value.PaymentsDueWithinPeriod);
        Assert.Equal(300m, result.Value.RemainingUnpaidAtPeriodEnd);
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
    }

    private sealed class TestManagementContextAccessor : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; }
        public void Set(ManagementContext context) => Current = context;
    }
}
