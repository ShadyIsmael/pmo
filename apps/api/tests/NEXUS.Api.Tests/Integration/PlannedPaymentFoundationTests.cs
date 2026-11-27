using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Sprint 2 Task 01 coverage for the Unified Planned Payment foundation. A minimal
/// SQLite model is used so serializable transactions and the unique idempotency
/// index are real, while unrelated Development business data is never touched.
/// </summary>
public sealed class PlannedPaymentFoundationTests : IDisposable
{
    private const string View = "PaymentObligation.View";
    private const string Create = "PaymentObligation.Create";
    private const string Edit = "PaymentObligation.Edit";

    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly PlannedPaymentTestContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _milestoneId = Guid.NewGuid();
    private readonly TestManagementContextAccessor _management = new();
    private readonly Mock<IHttpContextAccessor> _http = new();

    public PlannedPaymentFoundationTests()
    {
        _connectionString = $"Data Source=file:planned-payments-{Guid.NewGuid():N}?mode=memory&cache=shared;Default Timeout=30";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        _http.Setup(item => item.HttpContext).Returns(new DefaultHttpContext());
        _db = new PlannedPaymentTestContext(new DbContextOptionsBuilder<PlannedPaymentTestContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        SetManagementScope(_projectId);
        SetPermissions(View, Create, Edit);
    }

    [Fact]
    public async Task Registration_reserves_lifetime_budget_immediately_and_reports_accurate_balance()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        await SeedObligationSettlementAsync(1_000m, "USD");

        var registered = await RegisterAsync(NewCommand(2_500m, 2));

        AssertSuccess(registered);
        Assert.Equal(2_500m, registered.Value!.TotalAmount);
        Assert.Equal(2, registered.Value.Installments.Count);
        Assert.Equal(new[] { 1, 2 }, registered.Value.Installments.Select(item => item.Sequence));
        Assert.Equal(2_500m, (await GetReservationAsync(registered.Value.Id)).ReservedAmount);

        var budget = await GetBudgetAsync();
        AssertSuccess(budget);
        Assert.Equal(10_000m, budget.Value!.LifetimeCeiling);
        Assert.Equal(1_000m, budget.Value.LifetimeConsumed);
        Assert.Equal(2_500m, budget.Value.LifetimeReserved);
        Assert.Equal(6_500m, budget.Value.AvailableLifetimeBudget);
        Assert.True(budget.Value.CanRegister);

        var audit = await _db.AuditEntries.SingleAsync(item => item.EntityId == registered.Value.Id);
        Assert.Equal("PlannedPayment", audit.EntityType);
        Assert.Equal("Registered", audit.Action);
    }

    [Fact]
    public async Task Registration_joins_coordinator_transaction_and_replay_rolls_back_without_orphans()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var command = NewCommand(750m, 1);
        await using var coordinatorTransaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var first = await RegisterAsync(command);
        var replay = await RegisterAsync(command);

        AssertSuccess(first);
        AssertSuccess(replay);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);
        Assert.NotNull(_db.Database.CurrentTransaction);
        Assert.Equal(1, await _db.Set<PlannedPayment>().CountAsync());
        Assert.Equal(1, await _db.Set<PlannedPaymentInstallment>().CountAsync());
        Assert.Equal(1, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
        Assert.Equal(3, await _db.AuditEntries.CountAsync(item => item.TenantId == _tenantId && item.ProjectId == _projectId));

        await coordinatorTransaction.RollbackAsync();
        _db.ChangeTracker.Clear();

        Assert.Equal(0, await _db.Set<PlannedPayment>().CountAsync());
        Assert.Equal(0, await _db.Set<PlannedPaymentInstallment>().CountAsync());
        Assert.Equal(0, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
        Assert.Equal(0, await _db.AuditEntries.CountAsync(item => item.TenantId == _tenantId && item.ProjectId == _projectId));
    }

    [Fact]
    public async Task Insufficient_lifetime_budget_is_rejected_and_the_transaction_rolls_back()
    {
        await SeedBaselineAsync(budget: 1_000m, currency: "USD");

        var result = await RegisterAsync(NewCommand(1_000.0001m));

        AssertFailure(result, "INSUFFICIENT_LIFETIME_BUDGET");
        Assert.Equal(0, await _db.Set<PlannedPayment>().CountAsync());
        Assert.Equal(0, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
        Assert.Equal(0, await _db.AuditEntries.CountAsync());
        Assert.Equal(1_000m, (await GetBudgetAsync()).Value!.AvailableLifetimeBudget);
    }

    [Fact]
    public async Task Idempotent_replay_returns_the_original_plan_without_a_second_reservation()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var key = Guid.NewGuid();
        var command = NewCommand(1_500m, key: key);

        var first = await RegisterAsync(command);
        var replay = await RegisterAsync(command);
        var reuse = await RegisterAsync(NewCommand(1_600m, key: key));

        AssertSuccess(first);
        AssertSuccess(replay);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);
        AssertFailure(reuse, expectedStatusCode: 409);
        Assert.Equal(1, await _db.Set<PlannedPayment>().CountAsync());
        Assert.Equal(1, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
        Assert.Equal(3, await _db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Installments_reconcile_and_request_settlements_do_not_double_consume_lifetime_budget()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var command = NewCommand(3_000m, 2);
        var registered = await RegisterAsync(command);
        AssertSuccess(registered);
        var installmentId = registered.Value!.Installments[0].Id;

        await SeedRequestSettlementAsync(installmentId, 1_200m, "USD");

        var budget = await GetBudgetAsync();
        AssertSuccess(budget);
        Assert.Equal(1_200m, budget.Value!.LifetimeConsumed);
        Assert.Equal(1_800m, budget.Value.LifetimeReserved);
        Assert.Equal(7_000m, budget.Value.AvailableLifetimeBudget);

        var detail = await GetPlanAsync(registered.Value.Id);
        Assert.Equal(1_200m, detail.Value!.SettledAmount);
        Assert.Equal(1_800m, detail.Value.OutstandingReservedAmount);
        Assert.Equal(1_200m, detail.Value.Installments[0].SettledAmount);
        Assert.Equal(1_800m, detail.Value.Installments[0].RemainingRequestableAmount
            + detail.Value.Installments[1].Amount);
        Assert.Equal(1_500m, detail.Value.Installments[1].RemainingRequestableAmount);
        Assert.Equal(1, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
    }

    [Fact]
    public async Task First_payment_request_locks_financial_terms_without_creating_another_reservation()
    {
        await SeedBaselineAsync(budget: 5_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(1_000m));
        AssertSuccess(registered);
        var reservationId = (await GetReservationAsync(registered.Value!.Id)).Id;

        var lockResult = await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, registered.Value.Installments[0].Id, Guid.NewGuid(), 250m,
            _actorId, "PM", CancellationToken.None);
        AssertSuccess(lockResult);
        // The workflow owns the surrounding transaction and persists this hook.
        await _db.SaveChangesAsync();

        var locked = await GetPlanAsync(registered.Value.Id);
        Assert.True(locked.Value!.IsLocked);
        Assert.Equal(PlannedPaymentStates.Locked, locked.Value.State);
        Assert.NotNull(locked.Value.FirstRequestAtUtc);
        Assert.Equal(1_000m, (await GetReservationAsync(registered.Value.Id)).ReservedAmount);
        Assert.Equal(reservationId, (await GetReservationAsync(registered.Value.Id)).Id);
        Assert.Equal(2, await _db.AuditEntries.CountAsync(item => item.EntityId == registered.Value.Id));

        // Replay from a request retry must not add another lock event or reservation.
        var replay = await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, registered.Value.Installments[0].Id, Guid.NewGuid(), 250m,
            _actorId, "PM", CancellationToken.None);
        AssertSuccess(replay);
        Assert.Equal(2, await _db.AuditEntries.CountAsync(item => item.EntityId == registered.Value.Id));
        Assert.Equal(1, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
    }

    [Fact]
    public async Task Payment_request_overcommitment_is_rejected_before_locking()
    {
        await SeedBaselineAsync(budget: 5_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(500m));
        AssertSuccess(registered);
        var requestId = Guid.NewGuid();
        await SeedRequestDetailAsync(requestId, registered.Value!.Installments[0].Id, 400m, ApprovalRequest.StatusApproved);

        var result = await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, registered.Value.Installments[0].Id, Guid.NewGuid(), 150m,
            _actorId, "PM", CancellationToken.None);

        AssertFailure(result, "INSTALLMENT_OVERCOMMITTED");
        Assert.Equal(PlannedPaymentStates.Registered, (await GetPlanAsync(registered.Value.Id)).Value!.State);
    }

    [Fact]
    public async Task Pre_lock_amendment_updates_installments_and_reservation_without_duplication()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(1_000m));
        AssertSuccess(registered);
        var rowVersion = await SetRowVersionAsync(registered.Value!.Id);
        var persistedVersion = await _db.Set<PlannedPayment>().AsNoTracking()
            .Where(item => item.Id == registered.Value.Id)
            .Select(item => item.RowVersion).SingleAsync();
        Assert.Equal(rowVersion, persistedVersion);
        _db.ChangeTracker.Clear();

        var amended = await CreateService().UpdateAsync(_tenantId, _projectId, registered.Value.Id, _actorId, "PM",
            new UpdatePlannedPaymentDto(
                "Amended works",
                [
                    new UpdatePlannedPaymentInstallmentDto(registered.Value.Installments[0].Id, 500m, Utc(2026, 12, 1), null, "First"),
                    new UpdatePlannedPaymentInstallmentDto(null, 1_000m, Utc(2026, 12, 15), null, "Second")
                ],
                Guid.NewGuid(),
                rowVersion),
            CancellationToken.None);

        AssertSuccess(amended);
        Assert.Equal("Amended works", amended.Value!.Description);
        Assert.Equal(1_500m, amended.Value.TotalAmount);
        Assert.Equal(2, amended.Value.Installments.Count);
        Assert.Equal(1_500m, (await GetReservationAsync(registered.Value.Id)).ReservedAmount);
        Assert.Equal(1, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
        Assert.Contains(await _db.AuditEntries.ToListAsync(), item => item.Action == "Amended");

        // SQLite has no database-generated rowversion token; advance the test token
        // explicitly so the stale-write path is exercised deterministically.
        await SetRowVersionAsync(registered.Value.Id, [9, 8, 7, 6, 5, 4, 3, 2]);
        _db.ChangeTracker.Clear();
        var stale = await CreateService().UpdateAsync(_tenantId, _projectId, registered.Value.Id, _actorId, "PM",
            new UpdatePlannedPaymentDto("Stale", amended.Value.Installments.Select(item =>
                new UpdatePlannedPaymentInstallmentDto(item.Id, item.Amount, item.DueDateUtc, item.PlanActivityId, null)).ToList(),
                Guid.NewGuid(), rowVersion), CancellationToken.None);
        AssertFailure(stale, expectedStatusCode: 409);
    }

    [Fact]
    public async Task Post_lock_financial_amendment_is_rejected()
    {
        await SeedBaselineAsync(budget: 5_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(1_000m));
        AssertSuccess(registered);
        var rowVersion = await SetRowVersionAsync(registered.Value!.Id);
        await CreateService().ApplyPaymentRequestAsync(_tenantId, _projectId, registered.Value.Installments[0].Id,
            Guid.NewGuid(), 100m, _actorId, "PM", CancellationToken.None);

        var result = await CreateService().UpdateAsync(_tenantId, _projectId, registered.Value.Id, _actorId, "PM",
            new UpdatePlannedPaymentDto("Changed after lock",
                [new UpdatePlannedPaymentInstallmentDto(registered.Value.Installments[0].Id, 1_000m, Utc(2026, 12, 1), null, null)],
                Guid.NewGuid(), rowVersion), CancellationToken.None);

        AssertFailure(result, expectedStatusCode: 409);
        Assert.Equal("Site works", (await GetPlanAsync(registered.Value.Id)).Value!.Description);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1.00001d)]
    public async Task Invalid_installment_amounts_are_rejected(double amount)
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");

        var result = await RegisterAsync(new CreatePlannedPaymentDto("Bad amount",
            [new CreatePlannedPaymentInstallmentDto((decimal)amount, Utc(2026, 12, 1), null, null)], Guid.NewGuid()));

        AssertFailure(result, expectedStatusCode: 400);
        Assert.Equal(0, await _db.Set<PlannedPayment>().CountAsync());
    }

    [Fact]
    public async Task Non_utc_or_missing_due_dates_are_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var local = DateTime.SpecifyKind(new DateTime(2026, 12, 1), DateTimeKind.Local);

        var nonUtc = await RegisterAsync(new CreatePlannedPaymentDto("Local date",
            [new CreatePlannedPaymentInstallmentDto(100m, local, null, null)], Guid.NewGuid()));
        var missing = await RegisterAsync(new CreatePlannedPaymentDto("No date",
            [new CreatePlannedPaymentInstallmentDto(100m, null, null, null)], Guid.NewGuid()));

        AssertFailure(nonUtc, expectedStatusCode: 400);
        AssertFailure(missing, expectedStatusCode: 400);
        Assert.Equal(0, await _db.Set<PlannedPayment>().CountAsync());
    }

    [Fact]
    public async Task Active_milestone_is_resolved_and_inactive_milestone_is_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var milestoneLookup = new Mock<IProjectMilestoneReferenceLookup>();
        milestoneLookup.Setup(item => item.IsActiveMilestoneForProjectAsync(_projectId, _milestoneId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        milestoneLookup.Setup(item => item.FindActiveMilestoneNameForProjectAsync(_projectId, _milestoneId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Foundation complete");
        var milestoneService = CreateService(milestoneLookup: milestoneLookup.Object);

        var registered = await milestoneService.RegisterAsync(_tenantId, _projectId, _actorId, "PM",
            new CreatePlannedPaymentDto("With milestone",
                [new CreatePlannedPaymentInstallmentDto(500m, Utc(2026, 12, 1), _milestoneId, null)], Guid.NewGuid()),
            CancellationToken.None);

        AssertSuccess(registered);
        Assert.Equal(_milestoneId, registered.Value!.Installments[0].PlanActivityId);
        Assert.Equal("Foundation complete", registered.Value.Installments[0].PlanActivityName);

        milestoneLookup.Setup(item => item.IsActiveMilestoneForProjectAsync(_projectId, _milestoneId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var rejected = await milestoneService.RegisterAsync(_tenantId, _projectId, _actorId, "PM",
            new CreatePlannedPaymentDto("With stale milestone",
                [new CreatePlannedPaymentInstallmentDto(500m, Utc(2026, 12, 1), _milestoneId, null)], Guid.NewGuid()),
            CancellationToken.None);
        AssertFailure(rejected, expectedStatusCode: 400);
    }

    [Fact]
    public async Task Unconfigured_or_changed_currency_is_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");

        var unconfigured = await CreateService(currencyOverride: "EUR").RegisterAsync(
            _tenantId, _projectId, _actorId, "PM", NewCommand(100m), CancellationToken.None);
        AssertFailure(unconfigured, expectedStatusCode: 409);

        var registered = await RegisterAsync(NewCommand(100m));
        AssertSuccess(registered);
        var rowVersion = await SetRowVersionAsync(registered.Value!.Id);
        var changed = await CreateService(currencyOverride: "EUR").UpdateAsync(
            _tenantId, _projectId, registered.Value.Id, _actorId, "PM",
            new UpdatePlannedPaymentDto("Changed currency",
                [new UpdatePlannedPaymentInstallmentDto(registered.Value.Installments[0].Id, 100m, Utc(2026, 12, 1), null, null)],
                Guid.NewGuid(), rowVersion), CancellationToken.None);
        AssertFailure(changed, expectedStatusCode: 409);
    }

    [Fact]
    public async Task Missing_permission_active_deny_and_non_manager_actor_are_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");

        SetPermissions(View);
        var noCreate = await RegisterAsync(NewCommand(100m));
        AssertFailure(noCreate, expectedStatusCode: 403);

        // Active Deny is resolved by the authoritative identity resolver; the
        // request claim set therefore contains every grant except the denied key.
        var effective = EffectivePermissionResolver.Resolve(
            [View, Create, Edit], [(Create, PermissionOverrideEffect.Deny)]);
        Assert.DoesNotContain(Create, effective);
        SetPermissions([.. effective]);
        var activeDeny = await RegisterAsync(NewCommand(100m));
        AssertFailure(activeDeny, expectedStatusCode: 403);

        SetPermissions(View, Create, Edit);
        var nonManager = await CreateService(isCurrentManager: false).RegisterAsync(
            _tenantId, _projectId, _actorId, "PM", NewCommand(100m), CancellationToken.None);
        AssertFailure(nonManager, expectedStatusCode: 403);

        Assert.Equal(0, await _db.Set<PlannedPayment>().CountAsync());
    }

    [Fact]
    public async Task Tenant_and_project_scope_boundaries_are_enforced()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(100m));
        AssertSuccess(registered);

        SetManagementScope(Guid.NewGuid());
        var wrongProjectScope = await GetBudgetAsync();
        AssertFailure(wrongProjectScope, expectedStatusCode: 404);

        var outsideOrganization = Guid.NewGuid();
        _management.Set(new ManagementContext(
            _actorId,
            _tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            _projectId,
            new ManagementScope(
                new HashSet<Guid>(),
                new HashSet<Guid>(),
                new HashSet<Guid> { _projectId })
            {
                OrganizationIds = new HashSet<Guid> { Guid.NewGuid() },
                ProjectOrganizationIds = new Dictionary<Guid, Guid> { [_projectId] = outsideOrganization }
            }));
        var wrongOrganizationScope = await GetBudgetAsync();
        AssertFailure(wrongOrganizationScope, expectedStatusCode: 404);

        SetManagementScope(_projectId);
        var foreignTenant = Guid.NewGuid();
        SetManagementScope(_projectId, foreignTenant);
        SetTenant(foreignTenant);
        var wrongTenant = await CreateService().GetBudgetAsync(foreignTenant, _projectId, CancellationToken.None);
        AssertFailure(wrongTenant, expectedStatusCode: 404);

        SetManagementScope(_projectId);
        SetTenant(_tenantId);
        var foreignPlan = await CreateService().GetByIdAsync(_tenantId, Guid.NewGuid(), registered.Value!.Id, CancellationToken.None);
        AssertFailure(foreignPlan, expectedStatusCode: 404);
    }

    [Fact]
    public async Task Concurrent_duplicate_registrations_create_only_one_plan_and_reservation()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var key = Guid.NewGuid();
        var command = NewCommand(750m, key: key);
        using var firstConnection = new SqliteConnection(_connectionString);
        firstConnection.Open();
        using var secondConnection = new SqliteConnection(_connectionString);
        secondConnection.Open();
        await using var firstContext = CreateContext(firstConnection);
        await using var secondContext = CreateContext(secondConnection);

        var results = await Task.WhenAll(
            CreateService(firstContext).RegisterAsync(_tenantId, _projectId, _actorId, "PM", command, CancellationToken.None),
            CreateService(secondContext).RegisterAsync(_tenantId, _projectId, _actorId, "PM", command, CancellationToken.None));

        Assert.All(results, result =>
            Assert.True(result.IsSuccess || result.Error?.StatusCode == 409, result.Error?.Message));
        Assert.Single(results.Where(result => result.IsSuccess)
            .Select(result => result.Value!.Id).Distinct());
        Assert.Equal(1, await _db.Set<PlannedPayment>().CountAsync());
        Assert.Equal(1, await _db.Set<PlannedPaymentLifetimeReservation>().CountAsync());
        Assert.Equal(3, await _db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Replace_remaining_commitment_creates_new_plan_without_double_consuming_budget()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(2_000m, 2));
        AssertSuccess(registered);
        var planId = registered.Value!.Id;
        var installment0 = registered.Value.Installments[0].Id;
        var installment1 = registered.Value.Installments[1].Id;

        await SeedRequestSettlementAsync(installment0, 500m, "USD");

        var lockResult = await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, installment0, Guid.NewGuid(), 500m, _actorId, "PM", CancellationToken.None);
        AssertSuccess(lockResult);
        await _db.SaveChangesAsync();

        var beforeBudget = await GetBudgetAsync();
        AssertSuccess(beforeBudget);
        var beforeAvailable = beforeBudget.Value!.AvailableLifetimeBudget;

        var replacementCommand = NewReplacementCommand(1_500m, 500m, 1_000m);
        var replaced = await CreateService().ReplaceRemainingCommitmentAsync(
            _tenantId, _projectId, planId, _actorId, "PM", replacementCommand, CancellationToken.None);
        AssertSuccess(replaced);
        var replacement = replaced.Value!;
        Assert.Equal(PlannedPaymentStates.Registered, replacement.State);
        Assert.Equal(1_500m, replacement.TotalAmount);
        Assert.Equal(planId, replacement.ReplacesPlannedPaymentId);
        Assert.Equal(2, replacement.Installments.Count);
        Assert.Equal(500m, replacement.Installments[0].Amount);
        Assert.Equal(1_000m, replacement.Installments[1].Amount);

        var oldPlan = await GetPlanAsync(planId);
        AssertSuccess(oldPlan);
        Assert.Equal(PlannedPaymentStates.Replaced, oldPlan.Value!.State);
        Assert.Equal(replacement.Id, oldPlan.Value.ReplacedByPlannedPaymentId);

        var afterBudget = await GetBudgetAsync();
        AssertSuccess(afterBudget);
        Assert.Equal(beforeAvailable, afterBudget.Value!.AvailableLifetimeBudget);
        Assert.Equal(500m, afterBudget.Value.LifetimeConsumed);

        var oldReservation = await GetReservationAsync(planId);
        Assert.Equal(1_500m, oldReservation.ReleasedAmount);
        var newReservation = await GetReservationAsync(replacement.Id);
        Assert.Equal(1_500m, newReservation.ReservedAmount);
        Assert.Equal(0m, newReservation.ReleasedAmount);
    }

    [Fact]
    public async Task Replacement_preview_uses_historical_settlements_and_calculates_variable_commitment_delta()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(2_000m));
        AssertSuccess(registered);
        var plan = registered.Value!;
        var installmentId = plan.Installments[0].Id;

        await SeedRequestSettlementAsync(installmentId, 500m, "USD");
        var lockResult = await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, installmentId, Guid.NewGuid(), 500m, _actorId, "PM", CancellationToken.None);
        AssertSuccess(lockResult);
        await _db.SaveChangesAsync();

        var preview = await CreateService().GetReplacementPreviewAsync(
            _tenantId, _projectId, plan.Id, 2_500m, _actorId, CancellationToken.None);

        AssertSuccess(preview);
        Assert.Equal(2_000m, preview.Value!.CurrentCommitment);
        Assert.Equal(500m, preview.Value.HistoricalPaid);
        Assert.Equal(1_500m, preview.Value.CurrentOutstandingCommitment);
        Assert.Equal(2_500m, preview.Value.NewRemainingCommitment);
        Assert.Equal(1_000m, preview.Value.NetLifetimeBudgetChange);
        // "Before" is the project's current availability: ceiling 10,000 less the plan's own
        // 2,000 active reservation (rule 68). Settlement moves value reserved -> consumed and
        // does not change availability, so the 500 settled row does not reduce it.
        // "After" is the availability once the replacement commits: the plan's eligible unpaid
        // 1,500 reservation is released and the new 2,500 commitment is reserved once, so the
        // project consumes a net +1,000 of lifetime budget: 8,000 - 1,000 = 7,000.
        Assert.Equal(8_000m, preview.Value.AvailableLifetimeBudgetBefore);
        Assert.Equal(7_000m, preview.Value.AvailableLifetimeBudgetAfter);
        Assert.True(preview.Value.HasSufficientLifetimeBudget);
    }

    [Fact]
    public async Task Replace_non_locked_plan_is_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(1_000m));
        AssertSuccess(registered);

        var result = await CreateService().ReplaceRemainingCommitmentAsync(
            _tenantId, _projectId, registered.Value!.Id, _actorId, "PM",
            NewReplacementCommand(1_000m, 1_000m), CancellationToken.None);
        AssertFailure(result, expectedStatusCode: 409);
        Assert.Equal(PlannedPaymentStates.Registered, (await GetPlanAsync(registered.Value.Id)).Value!.State);
    }

    [Fact]
    public async Task Replace_already_replaced_plan_returns_existing_replacement()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(2_000m));
        AssertSuccess(registered);
        var planId = registered.Value!.Id;
        var installmentId = registered.Value.Installments[0].Id;

        await SeedRequestSettlementAsync(installmentId, 500m, "USD");
        await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, installmentId, Guid.NewGuid(), 500m, _actorId, "PM", CancellationToken.None);
        await _db.SaveChangesAsync();

        var replacementCommand = NewReplacementCommand(1_500m, 1_500m);
        var first = await CreateService().ReplaceRemainingCommitmentAsync(
            _tenantId, _projectId, planId, _actorId, "PM", replacementCommand, CancellationToken.None);
        AssertSuccess(first);

        var second = await CreateService().ReplaceRemainingCommitmentAsync(
            _tenantId, _projectId, planId, _actorId, "PM", replacementCommand, CancellationToken.None);
        AssertSuccess(second);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(2, await _db.Set<PlannedPayment>().CountAsync());
    }

    [Fact]
    public async Task Replace_fully_settled_plan_is_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(1_000m));
        AssertSuccess(registered);
        var installmentId = registered.Value.Installments[0].Id;

        await SeedRequestSettlementAsync(installmentId, 1_000m, "USD");
        await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, installmentId, Guid.NewGuid(), 1_000m, _actorId, "PM", CancellationToken.None);
        await _db.SaveChangesAsync();

        var result = await CreateService().ReplaceRemainingCommitmentAsync(
            _tenantId, _projectId, registered.Value!.Id, _actorId, "PM",
            NewReplacementCommand(1_000m, 1_000m), CancellationToken.None);
        AssertFailure(result, expectedStatusCode: 409);
    }

    [Fact]
    public async Task Replace_with_active_payment_request_is_rejected()
    {
        await SeedBaselineAsync(budget: 10_000m, currency: "USD");
        var registered = await RegisterAsync(NewCommand(1_000m));
        AssertSuccess(registered);
        var installmentId = registered.Value.Installments[0].Id;
        var requestId = Guid.NewGuid();
        await SeedRequestDetailAsync(requestId, installmentId, 300m, PaymentRequestStatuses.Pending);

        await CreateService().ApplyPaymentRequestAsync(
            _tenantId, _projectId, installmentId, requestId, 300m, _actorId, "PM", CancellationToken.None);
        await _db.SaveChangesAsync();

        var result = await CreateService().ReplaceRemainingCommitmentAsync(
            _tenantId, _projectId, registered.Value!.Id, _actorId, "PM",
            NewReplacementCommand(1_000m, 1_000m), CancellationToken.None);
        AssertFailure(result, expectedStatusCode: 409);
        Assert.Equal(PlannedPaymentStates.Locked, (await GetPlanAsync(registered.Value.Id)).Value!.State);
        Assert.Equal(1, await _db.Set<PlannedPayment>().CountAsync());
    }

    private CreatePlannedPaymentDto NewCommand(decimal amount, int installmentCount = 1, Guid? key = null) =>
        new("Site works",
            Enumerable.Range(0, installmentCount)
                .Select(index => new CreatePlannedPaymentInstallmentDto(
                    amount / installmentCount,
                    Utc(2026, 12, 1 + index),
                    null,
                    $"Installment {index + 1}"))
                .ToList(),
            key ?? Guid.NewGuid());

    private ReplaceRemainingCommitmentDto NewReplacementCommand(decimal total, params decimal[] installmentAmounts) =>
        new(total,
            installmentAmounts.Select((amount, index) => new ReplacePlannedPaymentInstallmentDto(
                amount, Utc(2027, 1, index + 1), null, $"Replacement installment {index + 1}")).ToList(),
            "Approved remaining commitment change",
            Guid.NewGuid());

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private Task<Result<PlannedPaymentDto>> RegisterAsync(CreatePlannedPaymentDto command) =>
        CreateService().RegisterAsync(_tenantId, _projectId, _actorId, "PM", command, CancellationToken.None);

    private Task<Result<PlannedPaymentBudgetDto>> GetBudgetAsync() =>
        CreateService().GetBudgetAsync(_tenantId, _projectId, CancellationToken.None);

    private Task<Result<PlannedPaymentDto>> GetPlanAsync(Guid id) =>
        CreateService().GetByIdAsync(_tenantId, _projectId, id, CancellationToken.None);

    private async Task<PlannedPaymentLifetimeReservation> GetReservationAsync(Guid planId) =>
        await _db.Set<PlannedPaymentLifetimeReservation>().SingleAsync(item => item.PlannedPaymentId == planId);

    private async Task<byte[]> SetRowVersionAsync(Guid planId, byte[]? version = null)
    {
        var plan = await _db.Set<PlannedPayment>().SingleAsync(item => item.Id == planId);
        plan.RowVersion = version ?? new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await _db.SaveChangesAsync();
        return plan.RowVersion;
    }

    private async Task SeedBaselineAsync(decimal budget, string currency)
    {
        _db.Users.Add(new User
        {
            Id = _actorId, TenantId = _tenantId, Email = "pm@example.test",
            FirstName = "Project", LastName = "Manager", CreatedBy = _actorId.ToString("D")
        });
        _db.Set<Currency>().Add(new Currency
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = currency,
            Name = currency, Symbol = currency, CreatedBy = _actorId.ToString("D")
        });
        await _db.SaveChangesAsync();
        _budget = budget;
        _currency = currency;
    }

    private decimal _budget = 10_000m;
    private string _currency = "USD";

    private async Task SeedObligationSettlementAsync(decimal amount, string currency)
    {
        var obligationId = Guid.NewGuid();
        _db.Set<PaymentObligation>().Add(new PaymentObligation
        {
            Id = obligationId, TenantId = _tenantId, ProjectId = _projectId,
            PaymentReference = $"PO-{obligationId:N}", Purpose = "Settled work",
            Amount = amount, Currency = currency, DueDate = Utc(2026, 11, 1),
            CreatedBy = _actorId.ToString("D")
        });
        _db.Set<PaymentSettlement>().Add(new PaymentSettlement
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProjectId = _projectId,
            PaymentObligationId = obligationId, IdempotencyKey = Guid.NewGuid(),
            Amount = amount, Currency = currency, SettlementDate = Utc(2026, 11, 10),
            PaymentReference = $"SET-{Guid.NewGuid():N}", CreatedBy = _actorId.ToString("D")
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Guid> SeedRequestSettlementAsync(Guid installmentId, decimal amount, string currency)
    {
        var requestId = Guid.NewGuid();
        _db.Set<ApprovalRequest>().Add(new ApprovalRequest
        {
            Id = requestId, TenantId = _tenantId, CreatedBy = _actorId.ToString("D"),
            Title = "Payment request", EntityType = "Project", EntityId = _projectId,
            Type = "ProjectPaymentRequest", Status = ApprovalRequest.StatusApproved,
            Amount = amount, Currency = currency, RequestedAt = Utc(2026, 11, 1)
        });
        _db.Set<ProjectPaymentRequestDetail>().Add(new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId, TenantId = _tenantId, ProjectId = _projectId,
            PlannedPaymentInstallmentId = installmentId,
            RequestKind = ProjectPaymentRequestKinds.PaymentRequest,
            RowVersion = new byte[] { 1 }
        });
        _db.Set<PaymentSettlement>().Add(new PaymentSettlement
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ProjectId = _projectId,
            ProjectPaymentRequestId = requestId, IdempotencyKey = Guid.NewGuid(),
            Amount = amount, Currency = currency, SettlementDate = Utc(2026, 11, 12),
            PaymentReference = $"RSET-{Guid.NewGuid():N}", CreatedBy = _actorId.ToString("D")
        });
        await _db.SaveChangesAsync();
        return requestId;
    }

    private async Task SeedRequestDetailAsync(Guid requestId, Guid installmentId, decimal amount, string status)
    {
        _db.Set<ApprovalRequest>().Add(new ApprovalRequest
        {
            Id = requestId, TenantId = _tenantId, CreatedBy = _actorId.ToString("D"),
            Title = "Payment request", EntityType = "Project", EntityId = _projectId,
            Type = "ProjectPaymentRequest", Status = status, Amount = amount,
            Currency = _currency, RequestedAt = Utc(2026, 11, 1)
        });
        _db.Set<ProjectPaymentRequestDetail>().Add(new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId, TenantId = _tenantId, ProjectId = _projectId,
            PlannedPaymentInstallmentId = installmentId,
            RequestKind = ProjectPaymentRequestKinds.PaymentRequest,
            RowVersion = new byte[] { 1 }
        });
        await _db.SaveChangesAsync();
    }

    private PlannedPaymentService CreateService(
        DbContext? context = null,
        bool isCurrentManager = true,
        string? currencyOverride = null,
        IProjectMilestoneReferenceLookup? milestoneLookup = null)
    {
        var effectiveCurrency = currencyOverride ?? _currency;
        var projectLookup = new Mock<IProjectReferenceLookup>();
        projectLookup.Setup(item => item.FindAsync(_projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectReference(_projectId, "Test project", effectiveCurrency, _budget, default, "P-1", _tenantId));
        var managerLookup = new Mock<ICurrentProjectManagerLookup>();
        managerLookup.Setup(item => item.IsCurrentAsync(_tenantId, _projectId, _actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isCurrentManager);
        var milestones = milestoneLookup ?? Mock.Of<IProjectMilestoneReferenceLookup>(item =>
            item.IsActiveMilestoneForProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(true)
            && item.FindActiveMilestoneNameForProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<string?>("Milestone"));
        return new PlannedPaymentService(
            context ?? _db, projectLookup.Object, milestones, managerLookup.Object, _http.Object, _management);
    }

    private void SetPermissions(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("tenantId", _tenantId.ToString("D")),
            new(ClaimTypes.NameIdentifier, _actorId.ToString("D")),
            new(ClaimTypes.Name, "PM")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        _http.Object.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "planned-payment-test"));
    }

    private void SetTenant(Guid tenantId)
    {
        var identity = (ClaimsIdentity)_http.Object.HttpContext!.User.Identity!;
        foreach (var claim in identity.FindAll("tenantId").ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim("tenantId", tenantId.ToString("D")));
    }

    private void SetManagementScope(Guid projectId, Guid? tenantId = null) => _management.Set(new ManagementContext(
        _actorId,
        tenantId ?? _tenantId,
        ManagementPerspective.ProjectManager,
        ManagementViewMode.Native,
        null,
        null,
        projectId,
        new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));

    private PlannedPaymentTestContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<PlannedPaymentTestContext>().UseSqlite(connection).Options);

    private static void AssertSuccess<T>(Result<T> result, string? message = null)
    {
        Assert.True(result.IsSuccess, message ?? result.Error?.Message ?? "Expected success.");
        Assert.NotNull(result.Value);
    }

    private static void AssertSuccess(Result result, string? message = null) =>
        Assert.True(result.IsSuccess, message ?? result.Error?.Message ?? "Expected success.");

    private static void AssertFailure<T>(Result<T> result, string? errorCode = null, int? expectedStatusCode = null)
    {
        Assert.True(result.IsFailure, "Expected failure.");
        Assert.NotNull(result.Error);
        if (errorCode is not null) Assert.Equal(errorCode, result.Error!.Code);
        if (expectedStatusCode.HasValue) Assert.Equal(expectedStatusCode.Value, result.Error!.StatusCode);
    }

    private static void AssertFailure(Result result, string? errorCode = null, int? expectedStatusCode = null)
    {
        Assert.True(result.IsFailure, "Expected failure.");
        Assert.NotNull(result.Error);
        if (errorCode is not null) Assert.Equal(errorCode, result.Error!.Code);
        if (expectedStatusCode.HasValue) Assert.Equal(expectedStatusCode.Value, result.Error!.StatusCode);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class TestManagementContextAccessor : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; }
        public void Set(ManagementContext context) => Current = context;
    }

    /// <summary>Minimal SQLite model containing only the entities the service queries.</summary>
    private sealed class PlannedPaymentTestContext(DbContextOptions<PlannedPaymentTestContext> options) : DbContext(options)
    {
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlannedPayment>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.RowVersion).IsConcurrencyToken(false).ValueGeneratedNever();
                entity.HasMany(item => item.Installments)
                    .WithOne()
                    .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentId })
                    .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Reservation)
                    .WithOne()
                    .HasForeignKey<PlannedPaymentLifetimeReservation>(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentId })
                    .HasPrincipalKey<PlannedPayment>(item => new { item.TenantId, item.ProjectId, item.Id })
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.IdempotencyKey }).IsUnique();
                entity.HasIndex(item => new { item.TenantId, item.ProjectId, item.UpdateIdempotencyKey }).IsUnique();
            });
            modelBuilder.Entity<PlannedPaymentInstallment>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.RowVersion).IsConcurrencyToken(false).ValueGeneratedNever();
            });
            modelBuilder.Entity<PlannedPaymentLifetimeReservation>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.RowVersion).IsConcurrencyToken(false).ValueGeneratedNever();
            });
            modelBuilder.Entity<AuditEntry>().HasKey(item => item.Id);
            modelBuilder.Entity<AuditEntry>().Ignore(item => item.Activities);
            modelBuilder.Entity<User>().HasKey(item => item.Id);
            modelBuilder.Entity<User>().Ignore(item => item.UserRoles);
            modelBuilder.Entity<User>().Ignore(item => item.Sessions);
            modelBuilder.Entity<User>().Ignore(item => item.PermissionOverrides);
            modelBuilder.Entity<User>().Ignore(item => item.UserOrganizations);
            modelBuilder.Entity<Currency>().HasKey(item => item.Id);
            modelBuilder.Entity<ApprovalRequest>().HasKey(item => item.Id);
            modelBuilder.Entity<ApprovalRequest>().Ignore(item => item.Actions);
            modelBuilder.Entity<ProjectPaymentRequestDetail>().HasKey(item => item.ApprovalRequestId);
            modelBuilder.Entity<ProjectPaymentRequestDetail>().Ignore(item => item.Settlements);
            modelBuilder.Entity<ProjectPaymentRequestDetail>().Ignore(item => item.ReversesRequest);
            modelBuilder.Entity<PaymentObligation>().HasKey(item => item.Id);
            modelBuilder.Entity<PaymentObligation>().Ignore(item => item.Settlements);
            modelBuilder.Entity<PaymentSettlement>().HasKey(item => item.Id);
            modelBuilder.Entity<PaymentSettlement>().Ignore(item => item.PaymentObligation);
            modelBuilder.Entity<PaymentSettlement>().Ignore(item => item.ProjectPaymentRequestDetail);
            modelBuilder.Entity<PaymentSettlement>().Ignore(item => item.FundingTransfer);
        }
    }
}
