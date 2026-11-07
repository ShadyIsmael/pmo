using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Governance;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.Portfolio.Domain;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class AssignmentRecoveryServiceTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("a1000000-0000-0000-0000-000000000010");
    private static readonly Guid SponsorId = Guid.Parse("a1000000-0000-0000-0000-000000000011");
    private static readonly Guid IncomingManagerId = Guid.Parse("a1000000-0000-0000-0000-000000000012");
    private static readonly Guid ForeignSponsorId = Guid.Parse("b1000000-0000-0000-0000-000000000010");
    private static readonly Guid ProjectId = Guid.Parse("a1000000-0000-0000-0000-000000000020");
    private static readonly Guid ForeignProjectId = Guid.Parse("b1000000-0000-0000-0000-000000000020");
    private static readonly Guid PortfolioId = Guid.Parse("a1000000-0000-0000-0000-000000000030");
    private static readonly Guid ProgramId = Guid.Parse("a1000000-0000-0000-0000-000000000031");
    private static readonly Guid ForeignPortfolioId = Guid.Parse("b1000000-0000-0000-0000-000000000030");
    private static readonly Guid ForeignProgramId = Guid.Parse("b1000000-0000-0000-0000-000000000031");
    private static readonly Guid ProjectPublicId = Guid.Parse("a2000000-0000-0000-0000-000000000020");
    private static readonly Guid ForeignProjectPublicId = Guid.Parse("b2000000-0000-0000-0000-000000000020");
    private static readonly Guid PortfolioPublicId = Guid.Parse("a2000000-0000-0000-0000-000000000030");
    private static readonly Guid ProgramPublicId = Guid.Parse("a2000000-0000-0000-0000-000000000031");
    private static readonly Guid ForeignPortfolioPublicId = Guid.Parse("b2000000-0000-0000-0000-000000000030");
    private static readonly Guid ForeignProgramPublicId = Guid.Parse("b2000000-0000-0000-0000-000000000031");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly Mock<IProjectHandoverRequestService> _projectHandovers = new();
    private readonly Mock<IManagerHandoverService> _managerHandovers = new();
    private readonly Mock<IUserEffectivePermissionService> _permissions = new();
    private IReadOnlySet<string> _effectivePermissions = new HashSet<string>
    {
        AssignmentRecoveryPermissions.View,
        AssignmentRecoveryPermissions.Initiate,
        AssignmentRecoveryPermissions.Manage,
        "Handover.AdminInitiate"
    };
    private readonly AssignmentRecoveryService _service;

    public AssignmentRecoveryServiceTests()
    {
        _connection.Open();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("tenantId", TenantA.ToString()) }, "tests"));
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _permissions.Setup(service => service.GetEffectivePermissionsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _effectivePermissions);

        _db.Users.AddRange(
            User(ActorId, TenantA, "admin@example.test", "Tenant", "Admin"),
            User(SponsorId, TenantA, "sponsor@example.test", "Project", "Sponsor"),
            User(IncomingManagerId, TenantA, "manager@example.test", "Incoming", "Manager"),
            User(ForeignSponsorId, TenantB, "foreign@example.test", "Foreign", "User"));
        _db.Projects.AddRange(
            Project(ProjectId, ProjectPublicId, TenantA, "Recovery Project", "REC-1"),
            Project(ForeignProjectId, ForeignProjectPublicId, TenantB, "Foreign Project", "REC-X"));
        _db.Portfolios.AddRange(
            new Portfolio { Id = PortfolioId, PublicId = PortfolioPublicId, TenantId = TenantA, Name = "Recovery Portfolio", Code = "REC-P", Status = "Active", CreatedBy = "tests", RowVersion = new byte[8] },
            new Portfolio { Id = ForeignPortfolioId, PublicId = ForeignPortfolioPublicId, TenantId = TenantB, Name = "Foreign Portfolio", Code = "REC-XP", Status = "Active", CreatedBy = "tests", RowVersion = new byte[8] });
        _db.Programs.AddRange(
            new PortfolioProgram { Id = ProgramId, PublicId = ProgramPublicId, TenantId = TenantA, Name = "Recovery Program", Code = "REC-G", PortfolioId = PortfolioId, Status = "Active", CreatedBy = "tests", RowVersion = new byte[8] },
            new PortfolioProgram { Id = ForeignProgramId, PublicId = ForeignProgramPublicId, TenantId = TenantB, Name = "Foreign Program", Code = "REC-XG", PortfolioId = ForeignPortfolioId, Status = "Active", CreatedBy = "tests", RowVersion = new byte[8] });
        _db.SaveChanges();

        _service = new AssignmentRecoveryService(
            _db,
            _projectHandovers.Object,
            _managerHandovers.Object,
            new ProjectSponsorMembershipSynchronizer(_db),
            new ProjectManagerMembershipSynchronizer(_db),
            _permissions.Object);
    }

    [Fact]
    public async Task Discovery_IsTenantBound_AndReturnsPublicIdInsteadOfInternalId()
    {
        var page = await _service.GetPageAsync(TenantA, ActorId, "projects", null, "All", 1, 25, default);

        var item = Assert.Single(page.Items);
        Assert.Equal(ProjectPublicId, item.PublicId);
        Assert.NotEqual(ProjectId, item.PublicId);
        Assert.Equal("Recovery Project", item.Name);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Summary.TotalProjects);
    }

    [Fact]
    public async Task Discovery_RequiresPersistedRecoveryCapability()
    {
        _effectivePermissions = new HashSet<string>();

        var error = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.GetPageAsync(TenantA, ActorId, "projects", null, "All", 1, 25, default));

        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void InitialProjectManagerSummary_RequiresInitiateAndHandoverPoliciesWithoutProjectViewOrEdit()
    {
        var action = typeof(AssignmentRecoveryController).GetMethod("GetInitialProjectManagerSummary");
        Assert.NotNull(action);

        var policies = action!.GetCustomAttributes<AuthorizeAttribute>().Select(attribute => attribute.Policy).ToArray();
        Assert.Equal(
            new[] { "Governance.AssignmentRecovery.Initiate", "Handover.AdminInitiate" }.OrderBy(policy => policy),
            policies.OrderBy(policy => policy));
        Assert.DoesNotContain("Project.View", policies);
        Assert.DoesNotContain("Project.Edit", policies);
    }

    [Fact]
    public async Task InitialProjectManagerSummary_ReturnsOnlyReadOnlyProjectContextWithinTenant()
    {
        var project = await _db.Projects.SingleAsync(item => item.Id == ProjectId);
        project.Description = "Replace the regional water monitoring system.";
        project.StartDate = new DateTime(2026, 1, 15);
        project.EndDate = new DateTime(2026, 8, 30);
        project.SponsorId = SponsorId;
        await _db.SaveChangesAsync();

        var summary = await _service.GetInitialProjectManagerSummaryAsync(TenantA, ActorId, ProjectPublicId, default);

        Assert.Equal(ProjectPublicId, summary.PublicId);
        Assert.Equal("Recovery Project", summary.Name);
        Assert.Equal("REC-1", summary.Code);
        Assert.Equal("Replace the regional water monitoring system.", summary.Description);
        Assert.Equal<DateTime?>(new DateTime(2026, 1, 15), summary.StartDate);
        Assert.Equal<DateTime?>(new DateTime(2026, 8, 30), summary.EndDate);
        Assert.Equal("Project Sponsor", summary.SponsorName);
        Assert.Equal(
            new[] { "Code", "Description", "EndDate", "Name", "PublicId", "SponsorName", "StartDate" },
            typeof(InitialProjectManagerSummaryDto).GetProperties().Select(property => property.Name).OrderBy(name => name));
        Assert.Null((await _db.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Empty(await _db.ProjectManagerAssignments.ToListAsync());
    }

    [Fact]
    public async Task InitialProjectManagerSummary_LeavesMissingOptionalFieldsUnavailable()
    {
        var summary = await _service.GetInitialProjectManagerSummaryAsync(TenantA, ActorId, ProjectPublicId, default);

        Assert.Null(summary.Description);
        Assert.Null(summary.StartDate);
        Assert.Null(summary.EndDate);
        Assert.Null(summary.SponsorName);
    }

    [Theory]
    [InlineData("Governance.AssignmentRecovery.Initiate")]
    [InlineData("Handover.AdminInitiate")]
    public async Task InitialProjectManagerSummary_HonorsEffectiveActiveDenyOnEitherRequiredPermission(string deniedPermission)
    {
        _effectivePermissions = new HashSet<string>
        {
            AssignmentRecoveryPermissions.Initiate,
            "Handover.AdminInitiate"
        };
        _effectivePermissions = _effectivePermissions.Where(permission => permission != deniedPermission).ToHashSet(StringComparer.Ordinal);

        var error = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.GetInitialProjectManagerSummaryAsync(TenantA, ActorId, ProjectPublicId, default));

        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task InitialProjectManagerSummary_HidesForeignTenantAndUnavailableProjects()
    {
        var foreignError = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.GetInitialProjectManagerSummaryAsync(TenantA, ActorId, ForeignProjectPublicId, default));
        var unavailableError = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.GetInitialProjectManagerSummaryAsync(TenantA, ActorId, Guid.NewGuid(), default));

        Assert.Equal(404, foreignError.StatusCode);
        Assert.Equal(404, unavailableError.StatusCode);
    }

    [Fact]
    public async Task InitialManagerRecovery_UsesCanonicalHandoverAndLeavesManagerUnsetUntilAcceptance()
    {
        var requestId = Guid.NewGuid();
        var requestedAt = DateTime.UtcNow;
        var expected = new ProjectHandoverRequestDto
        {
            Id = requestId,
            Status = "Pending",
            InitiationType = ManagerHandoverInitiationType.InitialAssignment,
            NewProjectManagerName = "Incoming Manager",
            RequestedByName = "Tenant Admin",
            RequestedAt = requestedAt
        };
        _projectHandovers.Setup(service => service.CreateInitialAssignmentAsync(
                ProjectId,
                It.Is<CreateProjectHandoverRequestDto>(input => input.NewProjectManagerId == IncomingManagerId
                    && input.Reason == "Recover ownership"),
                ActorId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProjectHandoverRequestDto>.Success(expected));

        var created = await _service.InitiateInitialAssignmentAsync(TenantA, ActorId, "project", ProjectPublicId,
            new CreateRecoveryAssignmentDto
            {
                ManagerId = IncomingManagerId,
                EffectiveDate = DateTime.UtcNow.Date,
                Reason = "Recover ownership"
            }, default);

        Assert.Equal("Pending", created.Status);
        Assert.Equal(ProjectPublicId, created.EntityPublicId);
        Assert.Null((await _db.Projects.SingleAsync(project => project.Id == ProjectId)).ProjectManagerId);
        _projectHandovers.VerifyAll();
    }

    [Fact]
    public async Task InitialAssignmentConflict_WhenProjectAlreadyHasManager_UsesStableBusinessCode()
    {
        (await _db.Projects.SingleAsync(project => project.Id == ProjectId)).ProjectManagerId = IncomingManagerId;
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.InitiateInitialAssignmentAsync(TenantA, ActorId, "project", ProjectPublicId,
                new CreateRecoveryAssignmentDto { ManagerId = SponsorId, EffectiveDate = DateTime.UtcNow.Date, Reason = "Recover ownership" }, default));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("PROJECT_ALREADY_ASSIGNED", error.Error.Code);
        _projectHandovers.Verify(service => service.CreateInitialAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<CreateProjectHandoverRequestDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitialAssignmentConflict_WhenActiveManagerHistoryNeedsReconciliation_UsesStableBusinessCode()
    {
        _db.ProjectManagerAssignments.Add(new ProjectManagerAssignment
        {
            TenantId = TenantA,
            ProjectId = ProjectId,
            ProjectManagerId = IncomingManagerId,
            IsCurrent = true,
            StartDate = DateTime.UtcNow.Date,
            CreatedBy = "tests",
            RowVersion = new byte[8]
        });
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.InitiateInitialAssignmentAsync(TenantA, ActorId, "project", ProjectPublicId,
                new CreateRecoveryAssignmentDto { ManagerId = SponsorId, EffectiveDate = DateTime.UtcNow.Date, Reason = "Recover ownership" }, default));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal("PROJECT_ASSIGNMENT_RECONCILIATION_REQUIRED", error.Error.Code);
        _projectHandovers.Verify(service => service.CreateInitialAssignmentAsync(
            It.IsAny<Guid>(), It.IsAny<CreateProjectHandoverRequestDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitialAssignment_IgnoresSoftDeletedCurrentManagerHistory()
    {
        _db.ProjectManagerAssignments.Add(new ProjectManagerAssignment
        {
            TenantId = TenantA,
            ProjectId = ProjectId,
            ProjectManagerId = IncomingManagerId,
            IsCurrent = true,
            IsDeleted = true,
            StartDate = DateTime.UtcNow.Date,
            CreatedBy = "tests",
            RowVersion = new byte[8]
        });
        await _db.SaveChangesAsync();

        var requestId = Guid.NewGuid();
        _projectHandovers.Setup(service => service.CreateInitialAssignmentAsync(
                ProjectId,
                It.Is<CreateProjectHandoverRequestDto>(input => input.NewProjectManagerId == SponsorId),
                ActorId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProjectHandoverRequestDto>.Success(new ProjectHandoverRequestDto
            {
                Id = requestId,
                Status = ProjectHandoverRequest.Statuses.Pending,
                InitiationType = ManagerHandoverInitiationType.InitialAssignment,
                NewProjectManagerName = "Project Sponsor",
                RequestedByName = "Tenant Admin",
                RequestedAt = DateTime.UtcNow
            }));

        var created = await _service.InitiateInitialAssignmentAsync(TenantA, ActorId, "project", ProjectPublicId,
            new CreateRecoveryAssignmentDto { ManagerId = SponsorId, EffectiveDate = DateTime.UtcNow.Date, Reason = "Recover ownership" }, default);

        Assert.Equal(requestId, created.RequestId);
        _projectHandovers.VerifyAll();
    }

    [Fact]
    public async Task RecoveryRow_IdentifiesRequestParticipantsAndLimitsExistingRequestAction()
    {
        _effectivePermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            AssignmentRecoveryPermissions.View,
            "Handover.View"
        };
        var request = new ProjectHandoverRequest
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            ProjectId = ProjectId,
            NewProjectManagerId = IncomingManagerId,
            RequestedBy = ActorId,
            RequestedAt = DateTime.UtcNow,
            EffectiveHandoverDate = DateTime.UtcNow.Date,
            InitiationType = ManagerHandoverInitiationType.InitialAssignment,
            Status = ProjectHandoverRequest.Statuses.Pending,
            Reason = "Recover ownership",
            CreatedBy = ActorId.ToString(),
            RowVersion = new byte[8]
        };
        _db.ProjectHandoverRequests.Add(request);
        await _db.SaveChangesAsync();

        var initiatorPage = await _service.GetPageAsync(TenantA, ActorId, "projects", null, "All", 1, 25, default);
        var initiatedRow = Assert.Single(initiatorPage.Items);
        Assert.Equal(request.Id, initiatedRow.RequestId);
        Assert.True(initiatedRow.CanViewExistingRequest);
        Assert.False(initiatedRow.CanReviewExistingRequest);
        Assert.True(initiatedRow.IsInitiatedByCurrentUser);
        Assert.False(initiatedRow.IsAssignedToCurrentUser);
        Assert.Equal(ProjectHandoverRequest.Statuses.Pending, initiatedRow.AssignmentRequestStatus);

        _effectivePermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            AssignmentRecoveryPermissions.View,
            "Handover.View",
            "Handover.Approve"
        };
        var incomingManagerPage = await _service.GetPageAsync(TenantA, IncomingManagerId, "projects", null, "All", 1, 25, default);
        var assignedRow = Assert.Single(incomingManagerPage.Items);
        Assert.Equal(request.Id, assignedRow.RequestId);
        Assert.True(assignedRow.CanViewExistingRequest);
        Assert.True(assignedRow.CanReviewExistingRequest);
        Assert.False(assignedRow.IsInitiatedByCurrentUser);
        Assert.True(assignedRow.IsAssignedToCurrentUser);

        var unrelatedPage = await _service.GetPageAsync(TenantA, SponsorId, "projects", null, "All", 1, 25, default);
        var unrelatedRow = Assert.Single(unrelatedPage.Items);
        Assert.False(unrelatedRow.CanViewExistingRequest);
        Assert.False(unrelatedRow.CanReviewExistingRequest);
        Assert.False(unrelatedRow.IsInitiatedByCurrentUser);
        Assert.False(unrelatedRow.IsAssignedToCurrentUser);
    }

    [Theory]
    [InlineData(ProjectHandoverRequest.Statuses.Pending)]
    [InlineData(ProjectHandoverRequest.Statuses.ApprovedScheduled)]
    [InlineData(ProjectHandoverRequest.Statuses.Accepted)]
    [InlineData(ProjectHandoverRequest.Statuses.Rejected)]
    [InlineData(ProjectHandoverRequest.Statuses.Cancelled)]
    public async Task RecoveryRowPreservesThePersistedHandoverLifecycleStatus(string requestStatus)
    {
        _db.ProjectHandoverRequests.Add(new ProjectHandoverRequest
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            ProjectId = ProjectId,
            NewProjectManagerId = IncomingManagerId,
            RequestedBy = ActorId,
            RequestedAt = DateTime.UtcNow,
            EffectiveHandoverDate = DateTime.UtcNow.Date,
            InitiationType = ManagerHandoverInitiationType.InitialAssignment,
            Status = requestStatus,
            Reason = "Lifecycle status projection",
            CreatedBy = ActorId.ToString(),
            RowVersion = new byte[8]
        });
        await _db.SaveChangesAsync();

        var page = await _service.GetPageAsync(TenantA, ActorId, "projects", null, "All", 1, 25, default);

        Assert.Equal(requestStatus, Assert.Single(page.Items).AssignmentRequestStatus);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("portfolio")]
    public async Task InitialManagerRecovery_UsesCanonicalPortfolioProgramHandoverAndLeavesManagerUnset(string entityType)
    {
        var request = new ManagerHandoverRequestDto
        {
            Id = Guid.NewGuid(), Status = "Pending", InitiationType = ManagerHandoverInitiationType.InitialAssignment,
            NewManagerId = IncomingManagerId, NewManagerName = "Incoming Manager", RequestedBy = ActorId,
            RequestedByName = "Tenant Admin", RequestedAt = DateTime.UtcNow
        };
        var publicId = entityType == "program" ? ProgramPublicId : PortfolioPublicId;
        if (entityType == "program")
        {
            _managerHandovers.Setup(service => service.CreateInitialProgramAssignmentAsync(
                    ProgramId,
                    It.Is<CreateManagerHandoverRequestDto>(input => input.NewManagerId == IncomingManagerId && input.Reason == "Recover ownership"),
                    ActorId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ManagerHandoverRequestDto>.Success(request));
        }
        else
        {
            _managerHandovers.Setup(service => service.CreateInitialPortfolioAssignmentAsync(
                    PortfolioId,
                    It.Is<CreateManagerHandoverRequestDto>(input => input.NewManagerId == IncomingManagerId && input.Reason == "Recover ownership"),
                    ActorId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ManagerHandoverRequestDto>.Success(request));
        }

        var created = await _service.InitiateInitialAssignmentAsync(TenantA, ActorId, entityType, publicId,
            new CreateRecoveryAssignmentDto { ManagerId = IncomingManagerId, EffectiveDate = DateTime.UtcNow.Date, Reason = "Recover ownership" }, default);

        Assert.Equal("Pending", created.Status);
        Assert.Equal(publicId, created.EntityPublicId);
        if (entityType == "program") Assert.Null((await _db.Programs.SingleAsync(item => item.Id == ProgramId)).ProgramManagerId);
        else Assert.Null((await _db.Portfolios.SingleAsync(item => item.Id == PortfolioId)).OwnerId);
        _managerHandovers.VerifyAll();
    }

    [Theory]
    [InlineData("program")]
    [InlineData("portfolio")]
    public async Task InitialManagerRecovery_CannotResolveForeignTenantPortfolioProgram(string entityType)
    {
        var publicId = entityType == "program" ? ForeignProgramPublicId : ForeignPortfolioPublicId;

        var error = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.InitiateInitialAssignmentAsync(TenantA, ActorId, entityType, publicId,
                new CreateRecoveryAssignmentDto { ManagerId = IncomingManagerId, EffectiveDate = DateTime.UtcNow.Date, Reason = "Recover ownership" }, default));

        Assert.Equal(404, error.StatusCode);
        _managerHandovers.Verify(service => service.CreateInitialProgramAssignmentAsync(It.IsAny<Guid>(), It.IsAny<CreateManagerHandoverRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _managerHandovers.Verify(service => service.CreateInitialPortfolioAssignmentAsync(It.IsAny<Guid>(), It.IsAny<CreateManagerHandoverRequestDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingGovernanceFilter_IncludesAnInactiveDirectManagerEvenWhenHistoryMatches()
    {
        var inactiveId = Guid.Parse("a1000000-0000-0000-0000-000000000013");
        var inactiveProjectId = Guid.Parse("a1000000-0000-0000-0000-000000000021");
        var inactiveManager = User(inactiveId, TenantA, "inactive@example.test", "Inactive", "Manager");
        inactiveManager.IsActive = false;
        var inactiveProject = Project(inactiveProjectId, Guid.NewGuid(), TenantA, "Inactive Manager Project", "REC-2");
        inactiveProject.ProjectManagerId = inactiveId;
        _db.Users.Add(inactiveManager);
        _db.Projects.Add(inactiveProject);
        _db.ProjectManagerAssignments.Add(new ProjectManagerAssignment
        {
            TenantId = TenantA, ProjectId = inactiveProjectId, ProjectManagerId = inactiveId, IsCurrent = true,
            StartDate = DateTime.UtcNow.Date, CreatedBy = "tests", RowVersion = new byte[8]
        });
        await _db.SaveChangesAsync();

        var page = await _service.GetPageAsync(TenantA, ActorId, "projects", null, "MissingGovernanceAssignment", 1, 25, default);

        var item = page.Items.Single(item => item.PublicId == inactiveProject.PublicId);
        Assert.Equal("ManagerInactive", item.ManagerAssignmentState);
        Assert.Equal(1, page.Summary.InconsistentAssignments);
    }

    [Fact]
    public async Task Reconcile_PreservesValidCurrentManagerWhenDirectManagerIsInactive()
    {
        var inactiveId = Guid.Parse("a1000000-0000-0000-0000-000000000013");
        var recoveryProjectId = Guid.Parse("a1000000-0000-0000-0000-000000000021");
        var recoveryProjectPublicId = Guid.NewGuid();
        var inactiveManager = User(inactiveId, TenantA, "inactive@example.test", "Inactive", "Manager");
        inactiveManager.IsActive = false;
        var entity = Project(recoveryProjectId, recoveryProjectPublicId, TenantA, "Reconcile Project", "REC-2");
        entity.ProjectManagerId = inactiveId;
        var prior = new ProjectManagerAssignment
        {
            TenantId = TenantA, ProjectId = recoveryProjectId, ProjectManagerId = IncomingManagerId, IsCurrent = true,
            StartDate = DateTime.UtcNow.Date.AddDays(-10), CreatedBy = "tests", RowVersion = new byte[8]
        };
        _db.Users.Add(inactiveManager);
        _db.Projects.Add(entity);
        _db.ProjectManagerAssignments.Add(prior);
        await _db.SaveChangesAsync();

        var result = await _service.ReconcileAsync(TenantA, ActorId, "project", recoveryProjectPublicId, default);

        Assert.Equal("Active", result.ManagerAssignmentState);
        Assert.Equal(IncomingManagerId, (await _db.Projects.SingleAsync(project => project.Id == recoveryProjectId)).ProjectManagerId);
        Assert.False(prior.IsCurrent);
        var activeAssignment = await _db.ProjectManagerAssignments.SingleAsync(assignment => assignment.ProjectId == recoveryProjectId && assignment.IsCurrent && !assignment.IsDeleted);
        Assert.Equal(IncomingManagerId, activeAssignment.ProjectManagerId);
        Assert.Single(await _db.Set<AuditEntry>().Where(entry => entry.EntityId == recoveryProjectId && entry.Action == "AssignmentRecoveryReconciled").ToListAsync());
    }

    [Fact]
    public async Task SponsorRecovery_IsAtomicAuditedAndDoesNotCreateSyntheticResource()
    {
        var result = await _service.AssignProjectSponsorAsync(TenantA, ActorId, ProjectPublicId, SponsorId, default);

        Assert.Equal(SponsorId, result.SponsorId);
        Assert.Equal(SponsorId, (await _db.Projects.SingleAsync(project => project.Id == ProjectId)).SponsorId);
        Assert.Equal(0, await _db.Resources.CountAsync());
        var audit = await _db.Set<AuditEntry>().SingleAsync(entry => entry.Action == "AssignmentRecoverySponsorAssigned");
        Assert.Equal(TenantA, audit.TenantId);
        Assert.Equal(ProjectId, audit.EntityId);
        Assert.Equal(ActorId, audit.UserId);
    }

    [Fact]
    public async Task ForeignTenantProjectAndSponsorCannotBeUsedInRecovery()
    {
        var projectError = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.AssignProjectSponsorAsync(TenantA, ActorId, ForeignProjectPublicId, SponsorId, default));
        var sponsorError = await Assert.ThrowsAsync<AssignmentRecoveryException>(() =>
            _service.AssignProjectSponsorAsync(TenantA, ActorId, ProjectPublicId, ForeignSponsorId, default));

        Assert.Equal(404, projectError.StatusCode);
        Assert.Equal(400, sponsorError.StatusCode);
        Assert.Null((await _db.Projects.SingleAsync(project => project.Id == ProjectId)).SponsorId);
        Assert.Empty(await _db.Set<AuditEntry>().Where(entry => entry.Action == "AssignmentRecoverySponsorAssigned").ToListAsync());
    }

    private static User User(Guid id, Guid tenantId, string email, string firstName, string lastName) => new()
    {
        Id = id, TenantId = tenantId, Email = email, FirstName = firstName, LastName = lastName,
        IsActive = true, PasswordHash = "test-hash", CreatedBy = "tests", RowVersion = new byte[8]
    };

    private static Project Project(Guid id, Guid publicId, Guid tenantId, string name, string code) => new()
    {
        Id = id, PublicId = publicId, TenantId = tenantId, Name = name, Code = code,
        Status = "Active", CreatedBy = "tests", RowVersion = new byte[8]
    };

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
