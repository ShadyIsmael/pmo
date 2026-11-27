using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Sprint 1 Task 02 regression coverage: Actual Paid must be computed from
/// confirmed PaymentSettlement facts on both ownership rails
/// (PaymentObligation and ProjectPaymentRequest) exactly once, scoped to the
/// owning project and tenant, without rewriting historical snapshots.
/// </summary>
[Collection("Static tenant resolution")]
public sealed class ActualPaidReportingTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly TestManagementContextAccessor _management = new();
    private readonly Mock<IHttpContextAccessor> _http = new();

    private static readonly DateTime PeriodFrom = new(2026, 11, 1);
    private static readonly DateTime PeriodTo = new(2026, 11, 30);

    public ActualPaidReportingTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"actual-paid-{Guid.NewGuid():N}").Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        ], "actual-paid-test"));
        _http.Setup(item => item.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
        _management.Set(new ManagementContext(_userId, _tenantId, ManagementPerspective.ProjectManager,
            ManagementViewMode.Native, null, null, _projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { _projectId })));
    }

    [Fact]
    public async Task ActualPaid_includes_payment_request_settlements_without_obligation_settlements()
    {
        await SeedProjectAsync();
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_000m);
        _context.PaymentSettlements.Add(RequestSettlement(requestId, new DateTime(2026, 11, 10), 400m));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(400m, result.Value!.ActualPaid);
    }

    [Fact]
    public async Task ActualPaid_combines_both_settlement_rails_exactly_once()
    {
        await SeedProjectAsync();
        var obligation = SeedObligation(_projectId, amount: 2_000m);
        _context.PaymentObligations.Add(obligation);
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_500m);
        _context.PaymentSettlements.AddRange(
            ObligationSettlement(obligation.Id, _projectId, new DateTime(2026, 11, 5), 700m),
            ObligationSettlement(obligation.Id, _projectId, new DateTime(2026, 11, 20), 300m),
            RequestSettlement(requestId, new DateTime(2026, 11, 12), 600m),
            RequestSettlement(requestId, new DateTime(2026, 11, 25), 400m));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2_000m, result.Value!.ActualPaid);
        // Outstanding planned payments remain obligation-scoped: 2000 - 1000 = 1000.
        Assert.Equal(1_000m, result.Value.OutstandingPlannedPayments);
    }

    [Fact]
    public async Task ActualPaid_excludes_settlements_outside_the_reporting_period()
    {
        await SeedProjectAsync();
        var obligation = SeedObligation(_projectId, amount: 2_000m);
        _context.PaymentObligations.Add(obligation);
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_000m);
        _context.PaymentSettlements.AddRange(
            ObligationSettlement(obligation.Id, _projectId, new DateTime(2026, 10, 31), 900m),
            ObligationSettlement(obligation.Id, _projectId, new DateTime(2026, 11, 1), 100m),
            RequestSettlement(requestId, new DateTime(2026, 11, 30), 250m),
            RequestSettlement(requestId, new DateTime(2026, 12, 1), 700m));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(350m, result.Value!.ActualPaid);
    }

    [Fact]
    public async Task ActualPaid_excludes_settlements_owned_by_other_projects_and_tenants()
    {
        await SeedProjectAsync();
        var otherProjectId = Guid.NewGuid();
        _context.Projects.Add(new Project
        {
            Id = otherProjectId, TenantId = _tenantId, CreatedBy = _userId.ToString(),
            Name = "Other project", Code = "OTH-1", Currency = "USD"
        });
        var foreignTenantId = Guid.NewGuid();
        var foreignProjectId = Guid.NewGuid();
        var ownObligation = SeedObligation(_projectId, amount: 500m);
        var otherObligation = SeedObligation(otherProjectId, amount: 500m);
        _context.PaymentObligations.AddRange(ownObligation, otherObligation);
        var ownRequestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 500m);
        var otherRequestId = await SeedApprovedPaymentRequestAsync(otherProjectId, amount: 500m);
        _context.PaymentSettlements.AddRange(
            ObligationSettlement(ownObligation.Id, _projectId, new DateTime(2026, 11, 5), 100m),
            ObligationSettlement(otherObligation.Id, otherProjectId, new DateTime(2026, 11, 5), 200m),
            RequestSettlement(ownRequestId, new DateTime(2026, 11, 6), 50m, projectId: _projectId),
            RequestSettlement(otherRequestId, new DateTime(2026, 11, 6), 60m, projectId: otherProjectId));
        await _context.SaveChangesAsync();

        Guid foreignObligationId;
        Guid foreignRequestId;
        var originalUser = _http.Object.HttpContext!.User;
        try
        {
            _http.Object.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", foreignTenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
            ], "foreign-tenant-seed"));
            _context.Projects.Add(new Project
            {
                Id = foreignProjectId, TenantId = foreignTenantId, CreatedBy = _userId.ToString(),
                Name = "Foreign project", Code = "FRN-1", Currency = "USD"
            });
            var foreignObligation = new PaymentObligation
            {
                TenantId = foreignTenantId, CreatedBy = _userId.ToString(), ProjectId = foreignProjectId,
                ProjectName = "Foreign", PaymentReference = "FRN-PAY", Purpose = "Test",
                Amount = 500m, Currency = "USD", DueDate = new DateTime(2026, 11, 10)
            };
            _context.PaymentObligations.Add(foreignObligation);
            foreignRequestId = await SeedApprovedPaymentRequestAsync(foreignProjectId, amount: 500m, tenantId: foreignTenantId);
            _context.PaymentSettlements.AddRange(
                ObligationSettlement(foreignObligation.Id, foreignProjectId, new DateTime(2026, 11, 5), 300m, tenantId: foreignTenantId),
                RequestSettlement(foreignRequestId, new DateTime(2026, 11, 6), 70m, tenantId: foreignTenantId, projectId: foreignProjectId));
            await _context.SaveChangesAsync();
            foreignObligationId = foreignObligation.Id;
        }
        finally
        {
            _http.Object.HttpContext.User = originalUser;
        }
        Assert.NotEqual(Guid.Empty, foreignObligationId);

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(150m, result.Value!.ActualPaid);
    }

    [Fact]
    public async Task ActualPaid_is_zero_for_an_approved_request_without_settlement()
    {
        await SeedProjectAsync();
        await SeedApprovedPaymentRequestAsync(_projectId, amount: 2_000m);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0m, result.Value!.ActualPaid);
    }

    [Fact]
    public async Task ActualPaid_is_not_reduced_by_an_approved_unpaid_commitment_reversal()
    {
        await SeedProjectAsync();
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_000m);
        _context.PaymentSettlements.Add(RequestSettlement(requestId, new DateTime(2026, 11, 8), 400m));
        // An approved reversal releases only the unpaid commitment; it never
        // creates a negative settlement or rewrites the recorded cash fact.
        var reversalId = await SeedApprovedPaymentRequestAsync(
            _projectId, amount: 600m, kind: ProjectPaymentRequestKinds.ReversalRequest, reversesId: requestId);
        Assert.NotEqual(Guid.Empty, reversalId);
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(400m, result.Value!.ActualPaid);
    }

    [Fact]
    public async Task NetActualPaid_uses_immutable_adjustments_in_their_effective_reporting_period()
    {
        await SeedProjectAsync();
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_000m);
        var settlement = RequestSettlement(requestId, new DateTime(2026, 11, 8), 400m);
        _context.PaymentSettlements.Add(settlement);
        _context.PaymentSettlementAdjustments.AddRange(
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Refund, Amount = 100m, Currency = "USD",
                Reason = "Refund confirmed", EffectiveDate = new DateTime(2026, 11, 15),
                RecordedAt = new DateTime(2026, 11, 15), IdempotencyKey = Guid.NewGuid()
            },
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Correction, Amount = 25m, Currency = "USD",
                Reason = "Settlement correction", EffectiveDate = new DateTime(2026, 11, 20),
                RecordedAt = new DateTime(2026, 11, 20), IdempotencyKey = Guid.NewGuid()
            },
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Refund, Amount = 50m, Currency = "USD",
                Reason = "December refund", EffectiveDate = new DateTime(2026, 12, 1),
                RecordedAt = new DateTime(2026, 12, 1), IdempotencyKey = Guid.NewGuid()
            });
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(400m, result.Value!.ActualPaid);
        Assert.Equal(400m, result.Value.GrossActualPaid);
        Assert.Equal(325m, result.Value.NetActualPaid);
    }

    [Fact]
    public async Task NetActualPaid_changes_only_after_adjustment_execution_is_confirmed()
    {
        await SeedProjectAsync();
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_000m);
        var settlement = RequestSettlement(requestId, new DateTime(2026, 11, 8), 400m);
        _context.PaymentSettlements.Add(settlement);
        _context.PaymentSettlementAdjustments.AddRange(
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Refund, Amount = 50m, Currency = "USD",
                Reason = "Awaiting approval", EffectiveDate = new DateTime(2026, 11, 10),
                RecordedAt = new DateTime(2026, 11, 10), IdempotencyKey = Guid.NewGuid(),
                Status = PaymentSettlementAdjustment.PendingApproval
            },
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Correction, Amount = 20m, Currency = "USD",
                Reason = "Approved but not executed", EffectiveDate = new DateTime(2026, 11, 12),
                RecordedAt = new DateTime(2026, 11, 12), IdempotencyKey = Guid.NewGuid(),
                Status = PaymentSettlementAdjustment.Approved
            },
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Refund, Amount = 40m, Currency = "USD",
                Reason = "Executed in November", EffectiveDate = new DateTime(2026, 12, 1),
                ExecutionDate = new DateTime(2026, 11, 20), RecordedAt = new DateTime(2026, 11, 20),
                IdempotencyKey = Guid.NewGuid(), Status = PaymentSettlementAdjustment.Executed
            },
            new PaymentSettlementAdjustment
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                OriginalSettlementId = settlement.Id, ProjectPaymentRequestId = requestId,
                AdjustmentType = PaymentSettlementAdjustment.Correction, Amount = 15m, Currency = "USD",
                Reason = "Rejected", EffectiveDate = new DateTime(2026, 11, 21),
                RecordedAt = new DateTime(2026, 11, 21), IdempotencyKey = Guid.NewGuid(),
                Status = PaymentSettlementAdjustment.Rejected
            });
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(400m, result.Value!.ActualPaid);
        Assert.Equal(400m, result.Value.GrossActualPaid);
        Assert.Equal(360m, result.Value.NetActualPaid);
    }

    [Fact]
    public async Task ActualPaid_does_not_sum_settlements_in_a_different_currency()
    {
        await SeedProjectAsync();
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 1_000m);
        _context.PaymentSettlements.Add(RequestSettlement(requestId, new DateTime(2026, 11, 9), 300m, currency: "EUR"));
        _context.PaymentSettlements.Add(RequestSettlement(requestId, new DateTime(2026, 11, 10), 200m, currency: "USD"));
        await _context.SaveChangesAsync();

        var result = await CreateService().GetFinancialSummaryAsync(_projectId, PeriodFrom, PeriodTo);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(200m, result.Value!.ActualPaid);
        Assert.Equal("partial", result.Value.DataStatus);
    }

    [Fact]
    public async Task Shared_calculator_resolves_project_and_currency_through_both_owners()
    {
        await SeedProjectAsync();
        var obligation = SeedObligation(_projectId, amount: 800m, currency: "USD");
        _context.PaymentObligations.Add(obligation);
        var requestId = await SeedApprovedPaymentRequestAsync(_projectId, amount: 800m);
        _context.PaymentSettlements.AddRange(
            // Obligation settlement without its own currency resolves via the obligation.
            new PaymentSettlement
            {
                TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = _projectId,
                PaymentObligationId = obligation.Id, IdempotencyKey = Guid.NewGuid(),
                Amount = 250m, SettlementDate = new DateTime(2026, 11, 3), PaymentReference = "SET-OBL"
            },
            RequestSettlement(requestId, new DateTime(2026, 11, 4), 350m));
        await _context.SaveChangesAsync();

        var facts = await ActualPaidSettlementCalculator.LoadForProjectAsync(
            _context, _tenantId, _projectId, cancellationToken: CancellationToken.None);

        Assert.Equal(2, facts.Count);
        Assert.All(facts, fact => Assert.Equal(_projectId, fact.ProjectId));
        Assert.Equal(600m, ActualPaidSettlementCalculator.SumActualPaid(
            facts, ActualPaidSettlementCalculator.NormalizeCurrency("USD")));
        // The obligation settlement carries no currency of its own; the owning
        // obligation currency is the resolved reporting currency.
        var obligationFact = facts.Single(fact => fact.PaymentObligationId == obligation.Id);
        Assert.Equal("USD", ActualPaidSettlementCalculator.NormalizeCurrency(obligationFact.Currency));
    }

    private async Task<Guid> SeedApprovedPaymentRequestAsync(
        Guid projectId, decimal amount, string kind = ProjectPaymentRequestKinds.PaymentRequest,
        Guid? reversesId = null, Guid? tenantId = null, string currency = "USD")
    {
        var effectiveTenantId = tenantId ?? _tenantId;
        var requestId = Guid.NewGuid();
        _context.Set<ApprovalRequest>().Add(new ApprovalRequest
        {
            Id = requestId,
            TenantId = effectiveTenantId,
            CreatedBy = _userId.ToString(),
            Title = "Payment request",
            EntityType = "Project",
            EntityId = projectId,
            Type = kind == ProjectPaymentRequestKinds.ReversalRequest ? "ProjectPaymentReversal" : "ProjectPaymentRequest",
            Status = ApprovalRequest.StatusApproved,
            Amount = amount,
            Currency = currency,
            RequestedAt = new DateTime(2026, 10, 20)
        });
        _context.Set<ProjectPaymentRequestDetail>().Add(new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId,
            TenantId = effectiveTenantId,
            ProjectId = projectId,
            RequestKind = kind,
            ReversesApprovalRequestId = reversesId
        });
        await _context.SaveChangesAsync();
        return requestId;
    }

    private PaymentObligation SeedObligation(Guid projectId, decimal amount, string currency = "USD") => new()
    {
        TenantId = _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
        ProjectName = "Test project", PaymentReference = $"PAY-{Guid.NewGuid():N}", Purpose = "Test payment",
        Amount = amount, Currency = currency, DueDate = new DateTime(2026, 11, 6)
    };

    private PaymentSettlement ObligationSettlement(Guid obligationId, Guid projectId, DateTime date, decimal amount,
        Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId,
        PaymentObligationId = obligationId, IdempotencyKey = Guid.NewGuid(), Amount = amount,
        SettlementDate = date, PaymentReference = $"SET-{Guid.NewGuid():N}"
    };

    private PaymentSettlement RequestSettlement(Guid requestId, DateTime date, decimal amount,
        string currency = "USD", Guid? tenantId = null, Guid? projectId = null) => new()
    {
        TenantId = tenantId ?? _tenantId, CreatedBy = _userId.ToString(), ProjectId = projectId ?? _projectId,
        ProjectPaymentRequestId = requestId, IdempotencyKey = Guid.NewGuid(), Amount = amount,
        Currency = currency, Description = "Actual payment settlement",
        SettlementDate = date, PaymentReference = $"RSET-{Guid.NewGuid():N}"
    };

    private async Task SeedProjectAsync()
    {
        _context.Projects.Add(new Project
        {
            Id = _projectId, TenantId = _tenantId, CreatedBy = _userId.ToString(),
            Name = "Actual paid project", Code = "AP-1", Currency = "USD"
        });
        await _context.SaveChangesAsync();
    }

    private ProjectReportingService CreateService()
    {
        var laborLookup = new Mock<IProjectEvmLaborCostLookup>();
        laborLookup.Setup(item => item.GetAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<ProjectEvmAssignmentInput>>(), It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<bool>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new ProjectEvmLaborCostSummary(null, null, null, null, null, null, "NO_ACTUAL_LABOR_DATA", false));
        return new ProjectReportingService(_context, Mock.Of<IAuditService>(), Mock.Of<IProjectEvmPlanLookup>(), laborLookup.Object,
            new ProjectEvmDirectCostLookup(_context), _management, null, null);
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
