using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel;
using Xunit;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Tests.Integration;

public sealed class ManagerHandoverSelfScopedPendingTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("30000000-0000-0000-0000-000000000011");
    private static readonly Guid IncomingId = Guid.Parse("30000000-0000-0000-0000-000000000012");
    private static readonly Guid UnrelatedId = Guid.Parse("30000000-0000-0000-0000-000000000013");
    private static readonly Guid NoPermId = Guid.Parse("30000000-0000-0000-0000-000000000014");
    private static readonly Guid NoPortfolioViewId = Guid.Parse("30000000-0000-0000-0000-000000000017");
    private static readonly Guid OtherTenantId = Guid.Parse("40000000-0000-0000-0000-000000000015");
    private static readonly Guid AdminId = Guid.Parse("30000000-0000-0000-0000-000000000016");
    private static readonly Guid NoManagementId = Guid.Parse("30000000-0000-0000-0000-000000000018");
    private static readonly Guid PortfolioId = Guid.Parse("30000000-0000-0000-0000-000000000021");
    private static readonly Guid ProgramId = Guid.Parse("30000000-0000-0000-0000-000000000022");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SelfScopedHandoverDbContext _context;
    private readonly Mock<IUserEffectivePermissionService> _permissions = new();
    private readonly Dictionary<Guid, IReadOnlySet<string>> _permissionMap;
    private readonly ManagerHandoverService _service;

    public ManagerHandoverSelfScopedPendingTests()
    {
        _connection.Open();
        _context = new SelfScopedHandoverDbContext(new DbContextOptionsBuilder<SelfScopedHandoverDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        SeedData();

        _permissionMap = new Dictionary<Guid, IReadOnlySet<string>>
        {
            [OwnerId] = new HashSet<string> { "Handover.Manage", "Handover.View" },
            [IncomingId] = new HashSet<string> { "Handover.Approve", "Portfolio.View", "Program.View" },
            [UnrelatedId] = new HashSet<string> { "Handover.Approve", "Portfolio.View", "Program.View" },
            [NoPermId] = new HashSet<string>(),
            [NoPortfolioViewId] = new HashSet<string> { "Handover.Approve" },
            [OtherTenantId] = new HashSet<string> { "Handover.Approve" },
            [AdminId] = new HashSet<string> { "Handover.AdminInitiate" },
            [NoManagementId] = new HashSet<string> { "Handover.Approve", "Portfolio.View", "Program.View" },
        };
        _permissions.Setup(x => x.GetEffectivePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid user, Guid tenant, CancellationToken ct) =>
                _permissionMap.TryGetValue(user, out var perms) ? perms : new HashSet<string>());

        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _service = new ManagerHandoverService(_context, notifications.Object, _permissions.Object);
    }

    [Fact]
    public async Task ApproveOnlyIncoming_CanFetchSelfPendingPortfolio()
    {
        var request = await CreatePortfolioRequestAsync();

        var result = await _service.GetPendingPortfolioForMeAsync(PortfolioId, IncomingId);

        Assert.True(result.IsSuccess);
        var pending = Assert.Single(result.Value!);
        Assert.Equal(request.Id, pending.Id);
        Assert.Equal(IncomingId, pending.NewManagerId);
        Assert.Equal(ManagerHandoverStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task ApproveOnlyIncoming_CanFetchSelfPendingProgram()
    {
        await CreateProgramRequestAsync();

        var result = await _service.GetPendingProgramForMeAsync(ProgramId, IncomingId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task ApproveOnlyUnrelatedUser_GetsEmptyList_NotAnotherUsersRequest()
    {
        await CreatePortfolioRequestAsync();

        var result = await _service.GetPendingPortfolioForMeAsync(PortfolioId, UnrelatedId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task UserWithoutApprovePermission_GetsForbiddenOnSelfPending()
    {
        await CreatePortfolioRequestAsync();

        var result = await _service.GetPendingPortfolioForMeAsync(PortfolioId, NoPermId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task CrossTenantUser_GetsNotFoundOnSelfPending()
    {
        await CreatePortfolioRequestAsync();

        var result = await _service.GetPendingPortfolioForMeAsync(PortfolioId, OtherTenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task AssignedIncomingManager_CanReadOnlyReviewProgramAndPortfolioApprovals()
    {
        var portfolioRequest = await CreatePortfolioRequestAsync();
        var programRequest = await CreateProgramRequestAsync();

        var portfolioReview = await _service.GetPortfolioApprovalReviewAsync(portfolioRequest.Id, IncomingId);
        var programReview = await _service.GetProgramApprovalReviewAsync(programRequest.Id, IncomingId);

        Assert.True(portfolioReview.IsSuccess);
        Assert.Equal("Portfolio A", portfolioReview.Value!.BusinessObjectName);
        Assert.Equal("Rebalance ownership", portfolioReview.Value.Reason);
        Assert.Equal("Owner Manager", portfolioReview.Value.CurrentAssigneeName);
        Assert.Equal("Incoming Manager", portfolioReview.Value.ProposedAssigneeName);
        Assert.True(portfolioReview.Value.IsReadOnly);
        Assert.True(portfolioReview.Value.CanDecide);
        Assert.True(programReview.IsSuccess);
        Assert.Equal("Program A", programReview.Value!.BusinessObjectName);
        Assert.True(programReview.Value.IsReadOnly);
    }

    [Fact]
    public async Task UnassignedApprover_CannotReadProgramOrPortfolioApprovalDetails()
    {
        var portfolioRequest = await CreatePortfolioRequestAsync();
        var programRequest = await CreateProgramRequestAsync();

        var portfolioReview = await _service.GetPortfolioApprovalReviewAsync(portfolioRequest.Id, UnrelatedId);
        var programReview = await _service.GetProgramApprovalReviewAsync(programRequest.Id, UnrelatedId);

        Assert.True(portfolioReview.IsFailure);
        Assert.Equal(404, portfolioReview.Error?.StatusCode);
        Assert.True(programReview.IsFailure);
        Assert.Equal(404, programReview.Error?.StatusCode);
    }

    [Fact]
    public async Task PortfolioEligibility_RequiresPortfolioView_ForCandidates()
    {
        var eligible = await _service.GetEligiblePortfolioManagersAsync(PortfolioId, OwnerId);

        Assert.True(eligible.IsSuccess);
        Assert.Contains(eligible.Value!, manager => manager.Id == IncomingId);
        Assert.DoesNotContain(eligible.Value!, manager => manager.Id == NoPortfolioViewId);
        Assert.DoesNotContain(eligible.Value!, manager => manager.Id == NoManagementId);
    }

    [Fact]
    public async Task AdminPortfolioHandover_RejectsCandidateWithoutPortfolioView()
    {
        var result = await _service.CreateAdminPortfolioAsync(PortfolioId, new CreateManagerHandoverRequestDto
        {
            NewManagerId = NoPortfolioViewId,
            EffectiveHandoverDate = DateTime.UtcNow.AddDays(7),
            Reason = "Administrative owner assignment"
        }, AdminId);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public async Task AdminPortfolioHandover_RejectsCandidateWithoutManagementCategoryRole()
    {
        var result = await _service.CreateAdminPortfolioAsync(PortfolioId, new CreateManagerHandoverRequestDto
        {
            NewManagerId = NoManagementId,
            EffectiveHandoverDate = DateTime.UtcNow.AddDays(7),
            Reason = "Administrative owner assignment"
        }, AdminId);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Empty(await _context.PortfolioHandoverRequests.ToListAsync());
    }

    [Fact]
    public async Task UnknownEntity_GetsNotFoundOnSelfPending()
    {
        var result = await _service.GetPendingPortfolioForMeAsync(Guid.NewGuid(), IncomingId);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task IncomingManager_CanAccept_AndOwnerIsUpdated()
    {
        var request = await CreatePortfolioRequestAsync();

        var accept = await _service.AcceptPortfolioAsync(request.Id, IncomingId);

        Assert.True(accept.IsSuccess);
        Assert.Equal(ManagerHandoverStatus.Accepted, accept.Value!.Status);
        var portfolio = await _context.Portfolios.AsNoTracking().SingleAsync(x => x.Id == PortfolioId);
        Assert.Equal(IncomingId, portfolio.OwnerId);
        Assert.NotNull(accept.Value.SnapshotId);
        var snapshot = await _context.PortfolioSnapshots.AsNoTracking().SingleAsync(x => x.Id == accept.Value.SnapshotId!.Value);
        Assert.Equal(OwnerId, snapshot.PreviousPortfolioManagerId);
        Assert.Equal(IncomingId, snapshot.NewPortfolioManagerId);

        var pending = await _service.GetPendingPortfolioForMeAsync(PortfolioId, IncomingId);
        Assert.True(pending.IsSuccess);
        Assert.Empty(pending.Value!);
    }

    [Fact]
    public async Task IncomingManager_CanAcceptProgram_AndSnapshotIsPersistedWithDecision()
    {
        var request = await CreateProgramRequestAsync();

        var accept = await _service.AcceptProgramAsync(request.Id, IncomingId);

        Assert.True(accept.IsSuccess);
        Assert.Equal(ManagerHandoverStatus.Accepted, accept.Value!.Status);
        Assert.Equal(IncomingId, (await _context.Programs.AsNoTracking().SingleAsync(x => x.Id == ProgramId)).ProgramManagerId);
        Assert.NotNull(accept.Value.SnapshotId);
        var snapshot = await _context.ProgramSnapshots.AsNoTracking().SingleAsync(x => x.Id == accept.Value.SnapshotId!.Value);
        Assert.Equal(OwnerId, snapshot.PreviousProgramManagerId);
        Assert.Equal(IncomingId, snapshot.NewProgramManagerId);
    }

    [Fact]
    public async Task AcceptPortfolioAndProgram_RequireIncomingManagerToRetainManagementCategoryRole()
    {
        var portfolioRequest = await CreatePortfolioRequestAsync();
        var programRequest = await CreateProgramRequestAsync();
        var role = await _context.Roles.SingleAsync(item => item.Name == "delivery.lead");
        role.Category = "Operation";
        await _context.SaveChangesAsync();

        var portfolioAccepted = await _service.AcceptPortfolioAsync(portfolioRequest.Id, IncomingId);
        var programAccepted = await _service.AcceptProgramAsync(programRequest.Id, IncomingId);

        Assert.True(portfolioAccepted.IsFailure);
        Assert.Equal(403, portfolioAccepted.Error?.StatusCode);
        Assert.True(programAccepted.IsFailure);
        Assert.Equal(403, programAccepted.Error?.StatusCode);
        Assert.Equal(OwnerId, (await _context.Portfolios.SingleAsync(item => item.Id == PortfolioId)).OwnerId);
        Assert.Equal(OwnerId, (await _context.Programs.SingleAsync(item => item.Id == ProgramId)).ProgramManagerId);
    }

    [Fact]
    public async Task UnrelatedUser_CannotAccept()
    {
        var request = await CreatePortfolioRequestAsync();

        var accept = await _service.AcceptPortfolioAsync(request.Id, UnrelatedId);

        Assert.True(accept.IsFailure);
        Assert.Equal(403, accept.Error?.StatusCode);
    }

    [Fact]
    public async Task DoubleAccept_ReturnsConflict()
    {
        var request = await CreatePortfolioRequestAsync();
        var first = await _service.AcceptPortfolioAsync(request.Id, IncomingId);
        Assert.True(first.IsSuccess);

        var second = await _service.AcceptPortfolioAsync(request.Id, IncomingId);

        Assert.True(second.IsFailure);
        Assert.Equal(409, second.Error?.StatusCode);
    }

    [Fact]
    public async Task Reject_RequiresReason_AndKeepsCurrentManager()
    {
        var request = await CreatePortfolioRequestAsync();

        var withoutReason = await _service.RejectPortfolioAsync(request.Id, new RejectManagerHandoverRequestDto { RejectionReason = " " }, IncomingId);
        Assert.True(withoutReason.IsFailure);
        Assert.Equal(400, withoutReason.Error?.StatusCode);

        var rejected = await _service.RejectPortfolioAsync(request.Id, new RejectManagerHandoverRequestDto { RejectionReason = "Capacity constraints" }, IncomingId);
        Assert.True(rejected.IsSuccess);
        Assert.Equal(ManagerHandoverStatus.Rejected, rejected.Value!.Status);
        var portfolio = await _context.Portfolios.AsNoTracking().SingleAsync(x => x.Id == PortfolioId);
        Assert.Equal(OwnerId, portfolio.OwnerId);
    }

    [Fact]
    public async Task SecondPendingRequest_IsBlocked()
    {
        await CreatePortfolioRequestAsync();

        var second = await _service.CreatePortfolioAsync(PortfolioId, new CreateManagerHandoverRequestDto
        {
            NewManagerId = UnrelatedId,
            EffectiveHandoverDate = DateTime.UtcNow.AddDays(7),
            Reason = "Second request"
        }, OwnerId);

        Assert.True(second.IsFailure);
        Assert.Equal(409, second.Error?.StatusCode);
    }

    [Fact]
    public async Task AdministratorCanRequestProgramAndPortfolioHandover_WithoutImmediateOwnerChange()
    {
        var portfolioResult = await _service.CreateAdminPortfolioAsync(PortfolioId, CreateRequest(), AdminId);
        var programResult = await _service.CreateAdminProgramAsync(ProgramId, CreateRequest(), AdminId);

        Assert.True(portfolioResult.IsSuccess);
        Assert.True(programResult.IsSuccess);
        Assert.Equal(ManagerHandoverInitiationType.AdminInitiated, portfolioResult.Value!.InitiationType);
        Assert.Equal(ManagerHandoverInitiationType.AdminInitiated, programResult.Value!.InitiationType);
        Assert.Equal(AdminId, portfolioResult.Value.RequestedBy);
        Assert.Equal(OwnerId, (await _context.Portfolios.SingleAsync(x => x.Id == PortfolioId)).OwnerId);
        Assert.Equal(OwnerId, (await _context.Programs.SingleAsync(x => x.Id == ProgramId)).ProgramManagerId);
    }

    [Fact]
    public async Task AdministratorCanRequestInitialAssignments_AndSnapshotsRecordNoPreviousManagerOnAcceptance()
    {
        var portfolio = await _context.Portfolios.SingleAsync(x => x.Id == PortfolioId);
        var program = await _context.Programs.SingleAsync(x => x.Id == ProgramId);
        portfolio.OwnerId = null;
        program.ProgramManagerId = null;
        await _context.SaveChangesAsync();

        var portfolioResult = await _service.CreateInitialPortfolioAssignmentAsync(PortfolioId, CreateRequest(), AdminId);
        var programResult = await _service.CreateInitialProgramAssignmentAsync(ProgramId, CreateRequest(), AdminId);

        Assert.True(portfolioResult.IsSuccess);
        Assert.True(programResult.IsSuccess);
        Assert.Equal(ManagerHandoverInitiationType.InitialAssignment, portfolioResult.Value!.InitiationType);
        Assert.Equal(ManagerHandoverInitiationType.InitialAssignment, programResult.Value!.InitiationType);
        Assert.Null(portfolioResult.Value.CurrentManagerId);
        Assert.Null(programResult.Value.CurrentManagerId);
        Assert.Null(portfolioResult.Value.SnapshotId);
        Assert.Null(programResult.Value.SnapshotId);
        Assert.Null(portfolio.OwnerId);
        Assert.Null(program.ProgramManagerId);
        Assert.Empty(await _context.PortfolioManagerAssignments.Where(x => x.PortfolioId == PortfolioId && x.IsCurrent).ToListAsync());
        Assert.Empty(await _context.ProgramManagerAssignments.Where(x => x.ProgramId == ProgramId && x.IsCurrent).ToListAsync());

        var portfolioAccepted = await _service.AcceptPortfolioAsync(portfolioResult.Value.Id, IncomingId);
        var programAccepted = await _service.AcceptProgramAsync(programResult.Value.Id, IncomingId);

        Assert.True(portfolioAccepted.IsSuccess);
        Assert.True(programAccepted.IsSuccess);
        Assert.Equal(IncomingId, (await _context.Portfolios.SingleAsync(x => x.Id == PortfolioId)).OwnerId);
        Assert.Equal(IncomingId, (await _context.Programs.SingleAsync(x => x.Id == ProgramId)).ProgramManagerId);
        Assert.NotNull(portfolioAccepted.Value!.SnapshotId);
        Assert.NotNull(programAccepted.Value!.SnapshotId);
        var portfolioSnapshot = await _context.PortfolioSnapshots.AsNoTracking().SingleAsync(x => x.Id == portfolioAccepted.Value.SnapshotId!.Value);
        var programSnapshot = await _context.ProgramSnapshots.AsNoTracking().SingleAsync(x => x.Id == programAccepted.Value.SnapshotId!.Value);
        Assert.Null(portfolioSnapshot.PreviousPortfolioManagerId);
        Assert.Null(programSnapshot.PreviousProgramManagerId);
        Assert.Equal(IncomingId, portfolioSnapshot.NewPortfolioManagerId);
        Assert.Equal(IncomingId, programSnapshot.NewProgramManagerId);
        Assert.Null((await _context.PortfolioManagerAssignments.SingleAsync(x => x.PortfolioId == PortfolioId && x.IsCurrent)).HandoverFromPortfolioManagerId);
        Assert.Null((await _context.ProgramManagerAssignments.SingleAsync(x => x.ProgramId == ProgramId && x.IsCurrent)).HandoverFromProgramManagerId);

        var portfolioSnapshots = await _service.GetPortfolioSnapshotsAsync(PortfolioId, AdminId);
        var programSnapshots = await _service.GetProgramSnapshotsAsync(ProgramId, AdminId);
        Assert.True(portfolioSnapshots.IsSuccess);
        Assert.True(programSnapshots.IsSuccess);
        Assert.Null(Assert.Single(portfolioSnapshots.Value!).PreviousManagerId);
        Assert.Null(Assert.Single(programSnapshots.Value!).PreviousManagerId);
    }

    [Fact]
    public async Task InitialPortfolioSnapshotFailure_RollsBackAssignmentAndAcceptance()
    {
        var unassignedPortfolio = await _context.Portfolios.SingleAsync(x => x.Id == PortfolioId);
        unassignedPortfolio.OwnerId = null;
        await _context.SaveChangesAsync();

        var request = await _service.CreateInitialPortfolioAssignmentAsync(PortfolioId, CreateRequest(), AdminId);
        Assert.True(request.IsSuccess);
        var requestId = request.Value!.Id;
        await _context.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectPortfolioSnapshot BEFORE INSERT ON PortfolioSnapshots BEGIN SELECT RAISE(ABORT, 'snapshot persistence rejected'); END;");

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.AcceptPortfolioAsync(requestId, IncomingId));

        var portfolio = await _context.Portfolios.AsNoTracking().SingleAsync(x => x.Id == PortfolioId);
        var persistedRequest = await _context.PortfolioHandoverRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
        Assert.Null(portfolio.OwnerId);
        Assert.Equal(ManagerHandoverStatus.Pending, persistedRequest.Status);
        Assert.Null(persistedRequest.SnapshotId);
        Assert.Empty(await _context.PortfolioSnapshots.AsNoTracking().ToListAsync());
        Assert.Empty(await _context.PortfolioManagerAssignments.AsNoTracking().Where(x => x.PortfolioId == PortfolioId && x.IsCurrent).ToListAsync());
    }

    [Fact]
    public async Task AdministratorCanCancelOnlyTheirOwnAdministrativeRequest()
    {
        var created = await _service.CreateAdminPortfolioAsync(PortfolioId, CreateRequest(), AdminId);
        Assert.True(created.IsSuccess);

        var denied = await _service.CancelPortfolioAsync(created.Value!.Id, OwnerId);
        var cancelled = await _service.CancelPortfolioAsync(created.Value.Id, AdminId);

        Assert.True(denied.IsFailure);
        Assert.Equal(403, denied.Error?.StatusCode);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(ManagerHandoverStatus.Cancelled, cancelled.Value!.Status);
        Assert.Equal(OwnerId, (await _context.Portfolios.SingleAsync(x => x.Id == PortfolioId)).OwnerId);
    }

    [Fact]
    public async Task FullRequestList_RemainsAvailable()
    {
        await CreatePortfolioRequestAsync();

        var result = await _service.GetPortfolioRequestsAsync(PortfolioId, OwnerId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<ManagerHandoverRequestDto> CreatePortfolioRequestAsync()
    {
        var created = await _service.CreatePortfolioAsync(PortfolioId, new CreateManagerHandoverRequestDto
        {
            NewManagerId = IncomingId,
            EffectiveHandoverDate = DateTime.UtcNow.AddDays(7),
            Reason = "Rebalance ownership",
            Notes = "Handle with care"
        }, OwnerId);
        Assert.True(created.IsSuccess);
        return created.Value!;
    }

    private static CreateManagerHandoverRequestDto CreateRequest() => new()
    {
        NewManagerId = IncomingId,
        EffectiveHandoverDate = DateTime.UtcNow.AddDays(7),
        Reason = "Administrative owner assignment"
    };

    private async Task<ManagerHandoverRequestDto> CreateProgramRequestAsync()
    {
        var created = await _service.CreateProgramAsync(ProgramId, new CreateManagerHandoverRequestDto
        {
            NewManagerId = IncomingId,
            EffectiveHandoverDate = DateTime.UtcNow.AddDays(7),
            Reason = "Rebalance ownership"
        }, OwnerId);
        Assert.True(created.IsSuccess);
        return created.Value!;
    }

    private void SeedData()
    {
        var users = new[]
        {
            NewUser(OwnerId, TenantA, "Owner", "Manager"),
            NewUser(IncomingId, TenantA, "Incoming", "Manager"),
            NewUser(UnrelatedId, TenantA, "Unrelated", "Manager"),
            NewUser(NoPermId, TenantA, "NoPerm", "User"),
            NewUser(NoPortfolioViewId, TenantA, "Project", "Manager"),
            NewUser(NoManagementId, TenantA, "Operations", "Lead"),
            NewUser(OtherTenantId, TenantB, "Other", "Tenant"),
            NewUser(AdminId, TenantA, "Tenant", "Administrator"),
        };
        var approvePermission = new Permission { Id = Guid.NewGuid(), TenantId = TenantA, Name = "Handover.Approve", Resource = "Handover", Action = "Approve", RowVersion = new byte[8] };
        var managerRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "delivery.lead", Category = "Management", RowVersion = new byte[8] };
        managerRole.Permissions.Add(approvePermission);
        var projectManagerRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "service.owner", Category = "Management", RowVersion = new byte[8] };
        projectManagerRole.Permissions.Add(approvePermission);
        var operationsRole = new Role { Id = Guid.NewGuid(), TenantId = TenantA, Name = "operations.lead", Category = "Operation", RowVersion = new byte[8] };
        var assignments = new[]
        {
            NewUserRole(IncomingId, TenantA, managerRole.Id),
            NewUserRole(UnrelatedId, TenantA, managerRole.Id),
            NewUserRole(NoPortfolioViewId, TenantA, projectManagerRole.Id),
            NewUserRole(NoManagementId, TenantA, operationsRole.Id),
        };
        var portfolio = new Portfolio { Id = PortfolioId, TenantId = TenantA, Name = "Portfolio A", Currency = "USD", OwnerId = OwnerId, RowVersion = new byte[8] };
        var program = new PortfolioProgram { Id = ProgramId, TenantId = TenantA, Name = "Program A", PortfolioId = PortfolioId, ProgramManagerId = OwnerId, RowVersion = new byte[8] };

        _context.Users.AddRange(users);
        _context.Permissions.Add(approvePermission);
        _context.Roles.AddRange(managerRole, projectManagerRole, operationsRole);
        _context.UserRoles.AddRange(assignments);
        _context.Portfolios.Add(portfolio);
        _context.Programs.Add(program);
        _context.SaveChanges();
    }

    private static User NewUser(Guid id, Guid tenantId, string firstName, string lastName) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.test",
        PasswordHash = "hash",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true,
        RowVersion = new byte[8]
    };

    private static UserRole NewUserRole(Guid userId, Guid tenantId, Guid roleId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserId = userId,
        RoleId = roleId,
        RowVersion = new byte[8]
    };

    private sealed class SelfScopedHandoverDbContext(DbContextOptions<SelfScopedHandoverDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserPermissionOverride> PermissionOverrides => Set<UserPermissionOverride>();
        public DbSet<Portfolio> Portfolios => Set<Portfolio>();
        public DbSet<PortfolioProgram> Programs => Set<PortfolioProgram>();
        public DbSet<ProgramHandoverRequest> ProgramHandoverRequests => Set<ProgramHandoverRequest>();
        public DbSet<PortfolioHandoverRequest> PortfolioHandoverRequests => Set<PortfolioHandoverRequest>();
        public DbSet<ProgramManagerAssignment> ProgramManagerAssignments => Set<ProgramManagerAssignment>();
        public DbSet<PortfolioManagerAssignment> PortfolioManagerAssignments => Set<PortfolioManagerAssignment>();
        public DbSet<ProgramSnapshot> ProgramSnapshots => Set<ProgramSnapshot>();
        public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    }
}
