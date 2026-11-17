using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>Runs the complete manager handover sequence against isolated in-memory SQLite.</summary>
[Collection("Static tenant resolution")]
public sealed class ProjectHandoverLifecyclePersistenceTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid ProjectId = Guid.Parse("51000000-0000-0000-0000-000000000002");
    private static readonly Guid ManagerA = Guid.Parse("51000000-0000-0000-0000-000000000011");
    private static readonly Guid ManagerB = Guid.Parse("51000000-0000-0000-0000-000000000012");
    private static readonly Guid ManagerC = Guid.Parse("51000000-0000-0000-0000-000000000013");
    private static readonly Guid ManagerD = Guid.Parse("51000000-0000-0000-0000-000000000014");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;
    private readonly HttpContextAccessor _http = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IUserEffectivePermissionService> _permissions = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly MutableTimeProvider _clock = new();
    private readonly ProjectHandoverRequestService _service;

    public ProjectHandoverLifecyclePersistenceTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        SetActor(ManagerA);
        AppDbContext.ConfigureTenantResolution(_http);
        _context.Database.EnsureCreated();

        _permissions
            .Setup(service => service.GetEffectivePermissionsAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal)
            {
                "Handover.Manage", "Handover.Approve", "Handover.View", "Handover.AdminInitiate"
            });
        _notifications
            .Setup(service => service.SendAsync(It.IsAny<NEXUS.Modules.Notifications.Domain.Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _audit
            .Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var snapshots = new ProjectSnapshotService(
            _context,
            _audit.Object,
            _http,
            new ProjectManagerMembershipSynchronizer(_context),
            _clock);
        _service = new ProjectHandoverRequestService(
            _context,
            snapshots,
            _notifications.Object,
            _http,
            _permissions.Object,
            timeProvider: _clock);

        SeedProjectAndManagers();
    }

    [Fact]
    public async Task ProjectCanCompleteThreeSequentialHandoversWithOneSnapshotEach()
    {
        var today = _clock.GetUtcNow().UtcDateTime.Date;
        var first = await CreateAsync(ManagerA, ManagerB, today, "A to B");
        SetActor(ManagerB);

        var firstReview = await _service.GetApprovalReviewAsync(first.Id, ManagerB.ToString());
        Assert.True(firstReview.IsSuccess);
        Assert.Equal(first.Id, firstReview.Value!.ApprovalId);
        Assert.Equal((await _context.Projects.SingleAsync(item => item.Id == ProjectId)).PublicId, firstReview.Value.BusinessObjectPublicId);
        Assert.NotEqual(ProjectId, firstReview.Value.BusinessObjectPublicId);
        Assert.Equal(404, (await _service.GetApprovalReviewAsync(ProjectId, ManagerB.ToString())).Error?.StatusCode);

        var firstAccepted = await _service.AcceptAsync(first.Id, ManagerB.ToString());
        Assert.True(firstAccepted.IsSuccess, firstAccepted.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, firstAccepted.Value!.Status);
        Assert.NotNull(firstAccepted.Value.SnapshotId);
        Assert.Equal(ManagerB, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(403, (await CreateAsResultAsync(ManagerA, ManagerC, today, "Old manager cannot initiate")).Error?.StatusCode);
        Assert.True((await _service.GetCapabilitiesAsync(ProjectId, ManagerB.ToString())).Value!.CanInitiate);

        var secondEffectiveDate = today.AddDays(1);
        var second = await CreateAsync(ManagerB, ManagerC, secondEffectiveDate, "B to C");
        SetActor(ManagerC);
        var secondReview = await _service.GetApprovalReviewAsync(second.Id, ManagerC.ToString());
        Assert.True(secondReview.IsSuccess, secondReview.Error?.Message);
        Assert.Equal(second.Id, secondReview.Value!.ApprovalId);
        Assert.Equal(ManagerB, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(404, (await _service.GetApprovalReviewAsync(second.Id, ManagerB.ToString())).Error?.StatusCode);

        var secondApproved = await _service.AcceptAsync(second.Id, ManagerC.ToString());
        Assert.True(secondApproved.IsSuccess, secondApproved.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.ApprovedScheduled, secondApproved.Value!.Status);
        Assert.Null(secondApproved.Value.SnapshotId);
        Assert.Equal(ManagerB, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(1, await _context.ProjectSnapshots.CountAsync());
        Assert.False((await _service.GetCapabilitiesAsync(ProjectId, ManagerB.ToString())).Value!.CanInitiate);
        Assert.Equal(409, (await CreateAsResultAsync(ManagerB, ManagerD, secondEffectiveDate.AddDays(2), "Overlapping request")).Error?.StatusCode);
        SetActor(ManagerC);
        Assert.Equal(409, (await _service.AcceptAsync(second.Id, ManagerC.ToString())).Error?.StatusCode);
        Assert.Equal(404, (await _service.GetApprovalReviewAsync(second.Id, ManagerC.ToString())).Error?.StatusCode);

        _clock.SetUtcNow(new DateTimeOffset(DateTime.SpecifyKind(secondEffectiveDate, DateTimeKind.Utc)));
        Assert.Equal(1, await _service.ProcessDueApprovedHandoversAsync());
        Assert.Equal(0, await _service.ProcessDueApprovedHandoversAsync());
        var secondPersisted = await _context.ProjectHandoverRequests.SingleAsync(item => item.Id == second.Id);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, secondPersisted.Status);
        Assert.NotNull(secondPersisted.SnapshotId);
        Assert.Equal(ManagerC, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(2, await _context.ProjectSnapshots.CountAsync());
        var secondSnapshot = await _context.ProjectSnapshots.SingleAsync(item => item.Id == secondPersisted.SnapshotId);
        Assert.Equal(secondEffectiveDate, secondSnapshot.EffectiveEndDate.Date);
        Assert.Equal(secondEffectiveDate, secondSnapshot.SnapshotDate.Date);
        Assert.Equal(ManagerB, secondSnapshot.PreviousProjectManagerId);
        Assert.Equal(ManagerC, secondSnapshot.NewProjectManagerId);
        Assert.True((await _service.GetCapabilitiesAsync(ProjectId, ManagerC.ToString())).Value!.CanInitiate);

        var third = await CreateAsync(ManagerC, ManagerD, secondEffectiveDate, "C to D");
        SetActor(ManagerD);
        Assert.True((await _service.GetApprovalReviewAsync(third.Id, ManagerD.ToString())).IsSuccess);
        var thirdAccepted = await _service.AcceptAsync(third.Id, ManagerD.ToString());
        Assert.True(thirdAccepted.IsSuccess, thirdAccepted.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, thirdAccepted.Value!.Status);
        Assert.NotNull(thirdAccepted.Value.SnapshotId);

        var history = await _context.ProjectHandoverRequests.AsNoTracking().ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.Equal(new Guid?[] { ManagerA, ManagerB, ManagerC }, new[]
        {
            history.Single(item => item.Id == first.Id).CurrentProjectManagerId,
            history.Single(item => item.Id == second.Id).CurrentProjectManagerId,
            history.Single(item => item.Id == third.Id).CurrentProjectManagerId
        });
        Assert.All(history, item => Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, item.Status));
        Assert.Equal(3, await _context.ProjectSnapshots.CountAsync());
        Assert.Equal(3, history.Select(item => item.SnapshotId).Distinct().Count());
        Assert.All(history, item => Assert.Single(_context.ProjectSnapshots.Where(snapshot => snapshot.Id == item.SnapshotId)));

        var assignments = await _context.ProjectManagerAssignments.AsNoTracking()
            .Where(item => item.ProjectId == ProjectId).OrderBy(item => item.StartDate).ToListAsync();
        Assert.Equal(4, assignments.Count);
        Assert.Equal(ManagerD, assignments.Single(item => item.IsCurrent).ProjectManagerId);
        Assert.Equal(new[] { ManagerA, ManagerB, ManagerC }, assignments.Where(item => !item.IsCurrent).Select(item => item.ProjectManagerId));
        Assert.Equal(ManagerD, (await _context.Projects.SingleAsync(item => item.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(409, (await _service.AcceptAsync(first.Id, ManagerB.ToString())).Error?.StatusCode);
    }

    [Fact]
    public async Task BackdatedHandoverIsRejectedWithoutCreatingARequest()
    {
        var yesterday = _clock.GetUtcNow().UtcDateTime.Date.AddDays(-1);
        var result = await CreateAsResultAsync(ManagerA, ManagerB, yesterday, "No historical reconstruction");

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Empty(await _context.ProjectHandoverRequests.ToListAsync());
        Assert.Empty(await _context.ProjectSnapshots.ToListAsync());
    }

    [Fact]
    public async Task InitialAssignmentUsesDistinctPendingAndScheduledConflictCodes()
    {
        await PrepareUnassignedProjectAsync();
        var today = _clock.GetUtcNow().UtcDateTime.Date;
        var first = await CreateInitialAssignmentAsync(ManagerA, ManagerB, today.AddDays(2), "Initial assignment");

        var pendingDuplicate = await CreateInitialAssignmentAsResultAsync(ManagerA, ManagerC, today.AddDays(3), "Duplicate pending assignment");
        Assert.True(pendingDuplicate.IsFailure);
        Assert.Equal(409, pendingDuplicate.Error?.StatusCode);
        Assert.Equal("HANDOVER_REQUEST_ALREADY_PENDING", pendingDuplicate.Error?.Code);

        SetActor(ManagerB);
        var scheduled = await _service.AcceptAsync(first.Id, ManagerB.ToString());
        Assert.True(scheduled.IsSuccess, scheduled.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.ApprovedScheduled, scheduled.Value!.Status);
        Assert.Null((await _context.Projects.SingleAsync(project => project.Id == ProjectId)).ProjectManagerId);

        var scheduledDuplicate = await CreateInitialAssignmentAsResultAsync(ManagerA, ManagerC, today.AddDays(4), "Duplicate scheduled assignment");
        Assert.True(scheduledDuplicate.IsFailure);
        Assert.Equal(409, scheduledDuplicate.Error?.StatusCode);
        Assert.Equal("HANDOVER_REQUEST_ALREADY_SCHEDULED", scheduledDuplicate.Error?.Code);
        Assert.Equal(1, await _context.ProjectHandoverRequests.CountAsync());
    }

    [Fact]
    public async Task AcceptedInitialAssignmentCreatesOneProjectManagerMembershipForLinkedResource()
    {
        await PrepareUnassignedProjectAsync();
        var resourceId = Guid.Parse("51000000-0000-0000-0000-000000000021");
        _context.Set<Resource>().Add(new Resource
        {
            Id = resourceId,
            TenantId = TenantId,
            UserId = ManagerB,
            Name = "Manager B",
            Type = "Employee",
            Status = "Active",
            IsActive = true,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        var today = _clock.GetUtcNow().UtcDateTime.Date;
        var request = await CreateInitialAssignmentAsync(ManagerA, ManagerB, today, "Initial assignment with Resource");
        SetActor(ManagerB);
        var accepted = await _service.AcceptAsync(request.Id, ManagerB.ToString());

        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, accepted.Value!.Status);
        Assert.Equal(ManagerB, (await _context.Projects.SingleAsync(project => project.Id == ProjectId)).ProjectManagerId);
        Assert.Equal(ManagerB, (await _context.ProjectManagerAssignments.SingleAsync(assignment => assignment.ProjectId == ProjectId && assignment.IsCurrent)).ProjectManagerId);

        var managerMemberships = await _context.Set<ProjectMember>().AsNoTracking()
            .Where(member => member.ProjectId == ProjectId && member.ResourceId == resourceId
                && !member.IsDeleted && member.Role == "ProjectManager")
            .ToListAsync();
        var managerMembership = Assert.Single(managerMemberships);
        Assert.Equal("Active", managerMembership.Status);
    }

    [Fact]
    public async Task AcceptedInitialAssignmentWithoutResourceUsesUserGovernanceProjectionAndCreatesNoIdentity()
    {
        await PrepareUnassignedProjectAsync();
        var request = await CreateInitialAssignmentAsync(ManagerA, ManagerB, _clock.GetUtcNow().UtcDateTime.Date, "Initial assignment without Resource");
        SetActor(ManagerB);
        var accepted = await _service.AcceptAsync(request.Id, ManagerB.ToString());

        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, accepted.Value!.Status);
        Assert.Equal(ManagerB, (await _context.Projects.SingleAsync(project => project.Id == ProjectId)).ProjectManagerId);
        Assert.Empty(await _context.Set<Resource>().Where(resource => resource.UserId == ManagerB && !resource.IsDeleted).ToListAsync());
        Assert.Empty(await _context.Set<ProjectMember>().Where(member => member.ProjectId == ProjectId && !member.IsDeleted).ToListAsync());

        var resourceLookup = new Mock<IResourceReferenceLookup>();
        resourceLookup.Setup(lookup => lookup.GetDetailsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        resourceLookup.Setup(lookup => lookup.GetDetailsByUserIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        var people = CreateProjectPeopleService(resourceLookup.Object);
        var peopleResult = await people.GetAsync(ProjectId);

        Assert.True(peopleResult.IsSuccess, peopleResult.Error?.Message);
        var managerProjection = Assert.Single(peopleResult.Value!, person => person.UserId == ManagerB);
        Assert.Equal("ProjectManager", managerProjection.RelationshipType);
        Assert.Null(managerProjection.ResourceId);
        Assert.Equal("Manager B", managerProjection.Name);
    }

    [Fact]
    public async Task ProjectPeopleMergesGovernanceManagerWithExistingTeamMembership()
    {
        var resourceId = Guid.Parse("51000000-0000-0000-0000-000000000022");
        var resourceReference = new ResourceReference(
            resourceId, ManagerB, "Manager B", "manager.b@example.test", null, "Employee", "Active", true);
        var project = await _context.Projects.SingleAsync(item => item.Id == ProjectId);
        project.ProjectManagerId = ManagerB;
        _context.Set<Resource>().Add(new Resource
        {
            Id = resourceId,
            TenantId = TenantId,
            UserId = ManagerB,
            Name = "Manager B",
            Type = "Employee",
            Status = "Active",
            IsActive = true,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        _context.Set<ProjectMember>().Add(new ProjectMember
        {
            TenantId = TenantId,
            ProjectId = ProjectId,
            ResourceId = resourceId,
            Role = ProjectRelationshipType.TeamMember,
            AllocationPercentage = 50m,
            StartDate = _clock.GetUtcNow().UtcDateTime.Date,
            EndDate = _clock.GetUtcNow().UtcDateTime.Date.AddMonths(1),
            Status = "Active",
            CreatedBy = ManagerA.ToString()
        });
        _context.Set<ProjectMember>().Add(new ProjectMember
        {
            TenantId = TenantId,
            ProjectId = ProjectId,
            ResourceId = resourceId,
            Role = ProjectRelationshipType.TeamMember,
            AllocationPercentage = 25m,
            StartDate = _clock.GetUtcNow().UtcDateTime.Date,
            EndDate = _clock.GetUtcNow().UtcDateTime.Date.AddMonths(1),
            Status = "Active",
            CreatedBy = ManagerA.ToString()
        });
        _context.Set<ProjectStakeholder>().Add(new ProjectStakeholder
        {
            TenantId = TenantId,
            ProjectId = ProjectId,
            UserId = ManagerB,
            ResourceId = resourceId,
            Name = "Manager B",
            Email = "manager.b@example.test",
            Role = "Delivery stakeholder",
            RelationshipType = ProjectRelationshipType.Stakeholder,
            Status = ProjectStakeholderStatus.Active,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        var resourceLookup = new Mock<IResourceReferenceLookup>();
        resourceLookup.Setup(lookup => lookup.GetDetailsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference> { [resourceId] = resourceReference });
        resourceLookup.Setup(lookup => lookup.GetDetailsByUserIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference> { [ManagerB] = resourceReference });
        var people = CreateProjectPeopleService(resourceLookup.Object);

        var peopleResult = await people.GetAsync(ProjectId);

        Assert.True(peopleResult.IsSuccess, peopleResult.Error?.Message);
        var manager = Assert.Single(peopleResult.Value!, person => person.UserId == ManagerB);
        Assert.Equal("TeamMember", manager.Source);
        Assert.Equal(ProjectRelationshipType.ProjectManager, manager.RelationshipType);
        Assert.Contains(ProjectRelationshipType.TeamMember, manager.AdditionalRelationshipTypes);
        Assert.Contains(ProjectRelationshipType.Stakeholder, manager.AdditionalRelationshipTypes);
        Assert.Equal(ProjectRelationshipType.TeamMember, manager.Role);
        Assert.Single(await _context.Set<Resource>().Where(resource => resource.UserId == ManagerB && !resource.IsDeleted).ToListAsync());
        Assert.Single(await _context.Users.Where(user => user.Id == ManagerB && !user.IsDeleted).ToListAsync());
    }

    [Fact]
    public async Task ProjectPeopleReadDoesNotReturnAProjectOutsideTheCurrentTenant()
    {
        var foreignTenantId = Guid.Parse("52000000-0000-0000-0000-000000000001");
        var foreignProjectId = Guid.Parse("52000000-0000-0000-0000-000000000002");
        SetActor(ManagerA, foreignTenantId);
        _context.Projects.Add(new Project
        {
            Id = foreignProjectId,
            TenantId = foreignTenantId,
            Name = "Foreign project",
            Code = "FOREIGN",
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        SetActor(ManagerA, TenantId);
        var resourceLookup = new Mock<IResourceReferenceLookup>();
        var people = CreateProjectPeopleService(resourceLookup.Object);

        var result = await people.GetAsync(foreignProjectId);

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ProjectPeopleDoesNotProjectAManagerWhoseUserIsMissing()
    {
        var missingManagerId = Guid.Parse("51000000-0000-0000-0000-000000000099");
        var project = await _context.Projects.SingleAsync(item => item.Id == ProjectId);
        project.ProjectManagerId = missingManagerId;
        await _context.SaveChangesAsync();

        var resourceLookup = new Mock<IResourceReferenceLookup>();
        resourceLookup.Setup(lookup => lookup.GetDetailsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        resourceLookup.Setup(lookup => lookup.GetDetailsByUserIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        var people = CreateProjectPeopleService(resourceLookup.Object);

        var result = await people.GetAsync(ProjectId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.DoesNotContain(result.Value!, person => person.UserId == missingManagerId);
        Assert.Empty(await _context.Users.Where(user => user.Id == missingManagerId).ToListAsync());
        Assert.Empty(await _context.Set<Resource>().Where(resource => resource.UserId == missingManagerId).ToListAsync());
    }

    [Fact]
    public async Task ProjectPeopleRejectsSameTenantProjectsOutsideManagementProjectScope()
    {
        var hiddenProjectId = Guid.Parse("51000000-0000-0000-0000-000000000099");
        var hiddenTeamId = Guid.Parse("51000000-0000-0000-0000-000000000098");
        var memberId = Guid.Parse("51000000-0000-0000-0000-000000000097");
        _context.Projects.Add(new Project
        {
            Id = hiddenProjectId,
            TenantId = TenantId,
            Name = "Out of scope project",
            Code = "HANDOVER-HIDDEN",
            SponsorId = ManagerB,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        _context.Teams.Add(new Team
        {
            Id = hiddenTeamId,
            TenantId = TenantId,
            Name = "Hidden project team",
            Code = "TEAM-HIDDEN",
            IsActive = true,
            RowVersion = new byte[8]
        });
        _context.Set<ProjectMember>().Add(new ProjectMember
        {
            Id = memberId,
            TenantId = TenantId,
            ProjectId = hiddenProjectId,
            TeamId = hiddenTeamId,
            Role = "TeamMember",
            AllocationPercentage = 60m,
            StartDate = _clock.GetUtcNow().UtcDateTime.Date,
            EndDate = _clock.GetUtcNow().UtcDateTime.Date.AddMonths(1),
            Status = "Active",
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        var resourceLookup = new Mock<IResourceReferenceLookup>();
        resourceLookup.Setup(lookup => lookup.GetDetailsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        resourceLookup.Setup(lookup => lookup.GetDetailsByUserIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        var people = CreateProjectPeopleService(resourceLookup.Object);

        var read = await people.GetAsync(hiddenProjectId);
        var isTeamGroup = await people.IsTeamGroupAssignmentAsync(hiddenProjectId, memberId);
        var add = await people.AddAsync(hiddenProjectId, new CreateProjectPersonDto
        {
            TeamId = hiddenTeamId,
            RelationshipType = ProjectRelationshipType.TeamMember,
            AllocationPercentage = 20m,
            StartDate = _clock.GetUtcNow().UtcDateTime.Date,
            EndDate = _clock.GetUtcNow().UtcDateTime.Date.AddMonths(1)
        }, ManagerA.ToString());
        var update = await people.UpdateAsync(hiddenProjectId, memberId, new UpdateProjectPersonDto
        {
            Source = "TeamMember",
            AllocationPercentage = 10m
        }, ManagerA.ToString());
        var remove = await people.RevokeOrRemoveAsync(hiddenProjectId, memberId,
            "TeamMember", null, ManagerA.ToString());
        var clearSponsor = await people.ClearSponsorAsync(hiddenProjectId, ManagerA.ToString());

        Assert.Equal(404, read.Error?.StatusCode);
        Assert.False(isTeamGroup);
        Assert.Equal(404, add.Error?.StatusCode);
        Assert.Equal(404, update.Error?.StatusCode);
        Assert.Equal(404, remove.Error?.StatusCode);
        Assert.Equal(404, clearSponsor.Error?.StatusCode);
        var persistedProject = await _context.Projects.SingleAsync(project => project.Id == hiddenProjectId);
        Assert.Equal("Out of scope project", persistedProject.Name);
        Assert.Equal(ManagerB, persistedProject.SponsorId);
        var persistedMember = await _context.ProjectMembers.SingleAsync(member => member.Id == memberId);
        Assert.Equal(60m, persistedMember.AllocationPercentage);
        Assert.False(persistedMember.IsDeleted);
        Assert.Equal(1, await _context.ProjectMembers.CountAsync(member => member.ProjectId == hiddenProjectId));
    }

    [Fact]
    public async Task ProjectPeopleRejectsProjectRolesFromAnotherTenant()
    {
        var teamId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var foreignRoleId = Guid.NewGuid();
        _context.Teams.Add(new Team
        {
            Id = teamId,
            TenantId = TenantId,
            Name = "Project people role validation team",
            Code = "TEAM-ROLE-VALIDATION",
            IsActive = true,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        _context.Set<Lookup>().Add(new Lookup
        {
            Id = foreignRoleId,
            TenantId = foreignTenantId,
            Category = "ProjectRole",
            Code = "FOREIGN-ROLE",
            NameEn = "Foreign role",
            NameAr = "دور خارجي",
            IsActive = true,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        SetActor(ManagerA, TenantId);
        var resourceLookup = new Mock<IResourceReferenceLookup>();
        resourceLookup.Setup(lookup => lookup.GetDetailsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResourceReference>());
        var people = CreateProjectPeopleService(resourceLookup.Object);
        var result = await people.AddAsync(ProjectId, new CreateProjectPersonDto
        {
            TeamId = teamId,
            RelationshipType = ProjectRelationshipType.TeamMember,
            AllocationPercentage = 20m,
            ProjectRoleIds = new List<Guid> { foreignRoleId }
        }, ManagerA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Validation("Select active Project Roles from the same tenant.").Code, result.Error?.Code);
        Assert.Empty(await _context.ProjectMembers.Where(member => member.ProjectId == ProjectId && member.TeamId == teamId).ToListAsync());
        Assert.Empty(await _context.Set<ProjectMemberRole>().Where(role => role.ProjectRoleId == foreignRoleId).ToListAsync());
    }

    [Fact]
    public async Task CompletedHandoverEndsPreviousManagerMembershipAndSynchronizesNewManagerIdempotently()
    {
        var today = _clock.GetUtcNow().UtcDateTime.Date;
        var previousResourceId = Guid.Parse("51000000-0000-0000-0000-000000000031");
        var newResourceId = Guid.Parse("51000000-0000-0000-0000-000000000032");
        _context.Set<Resource>().AddRange(
            new Resource
            {
                Id = previousResourceId,
                TenantId = TenantId,
                UserId = ManagerA,
                Name = "Manager A",
                Type = "Employee",
                Status = "Active",
                IsActive = true,
                CreatedBy = ManagerA.ToString(),
                RowVersion = new byte[8]
            },
            new Resource
            {
                Id = newResourceId,
                TenantId = TenantId,
                UserId = ManagerB,
                Name = "Manager B",
                Type = "Employee",
                Status = "Active",
                IsActive = true,
                CreatedBy = ManagerA.ToString(),
                RowVersion = new byte[8]
            });

        var previousProjectManagerMembershipId = Guid.Parse("51000000-0000-0000-0000-000000000041");
        var independentTeamMembershipId = Guid.Parse("51000000-0000-0000-0000-000000000042");
        var originalCreatedAt = today.AddDays(-20);
        _context.Set<ProjectMember>().AddRange(
            new ProjectMember
            {
                Id = previousProjectManagerMembershipId,
                TenantId = TenantId,
                ProjectId = ProjectId,
                ResourceId = previousResourceId,
                Role = "ProjectManager",
                AllocationPercentage = 100m,
                HoursPerWeek = 40m,
                StartDate = originalCreatedAt,
                EndDate = today.AddYears(1),
                Status = "Active",
                CreatedAt = originalCreatedAt,
                CreatedBy = ManagerA.ToString(),
                RowVersion = new byte[8]
            },
            new ProjectMember
            {
                Id = independentTeamMembershipId,
                TenantId = TenantId,
                ProjectId = ProjectId,
                ResourceId = previousResourceId,
                Role = "TeamMember",
                AllocationPercentage = 25m,
                HoursPerWeek = 10m,
                StartDate = originalCreatedAt,
                EndDate = today.AddYears(1),
                Status = "Active",
                CreatedAt = originalCreatedAt,
                CreatedBy = ManagerA.ToString(),
                RowVersion = new byte[8]
            });
        await _context.SaveChangesAsync();

        var request = await CreateAsync(ManagerA, ManagerB, today, "Manager handover membership synchronization");
        SetActor(ManagerB);
        var accepted = await _service.AcceptAsync(request.Id, ManagerB.ToString());

        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Accepted, accepted.Value!.Status);
        var previousMembership = await _context.Set<ProjectMember>().AsNoTracking()
            .SingleAsync(member => member.Id == previousProjectManagerMembershipId);
        Assert.Equal("Inactive", previousMembership.Status);
        Assert.Equal(today, previousMembership.EndDate.Date);
        Assert.False(previousMembership.IsDeleted);
        Assert.Equal(originalCreatedAt, previousMembership.CreatedAt);
        Assert.Equal(ManagerA.ToString(), previousMembership.CreatedBy);

        var independentMembership = await _context.Set<ProjectMember>().AsNoTracking()
            .SingleAsync(member => member.Id == independentTeamMembershipId);
        Assert.Equal("Active", independentMembership.Status);

        var synchronizer = new ProjectManagerMembershipSynchronizer(_context);
        var project = await _context.Projects.SingleAsync(item => item.Id == ProjectId);
        await synchronizer.SynchronizeAsync(project, ManagerA, ManagerB, today, ManagerB.ToString());
        await _context.SaveChangesAsync();

        var activeIncomingManagerMemberships = await _context.Set<ProjectMember>().AsNoTracking()
            .Where(member => member.TenantId == TenantId && member.ProjectId == ProjectId
                && member.ResourceId == newResourceId && member.Role == "ProjectManager"
                && member.Status == "Active" && !member.IsDeleted)
            .ToListAsync();
        var incomingMembership = Assert.Single(activeIncomingManagerMemberships);
        Assert.Equal("Active", incomingMembership.Status);
        Assert.Equal(ManagerB, (await _context.ProjectManagerAssignments.SingleAsync(
            assignment => assignment.ProjectId == ProjectId && assignment.IsCurrent)).ProjectManagerId);
    }

    [Fact]
    public async Task RejectedAndCancelledInitialAssignmentsDoNotBlockEligibleRetries()
    {
        await PrepareUnassignedProjectAsync();
        var today = _clock.GetUtcNow().UtcDateTime.Date;

        var rejectedRequest = await CreateInitialAssignmentAsync(ManagerA, ManagerB, today, "First candidate");
        SetActor(ManagerB);
        var rejected = await _service.RejectAsync(rejectedRequest.Id,
            new RejectProjectHandoverRequestDto { RejectionReason = "Cannot accept this assignment" }, ManagerB.ToString());
        Assert.True(rejected.IsSuccess, rejected.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Rejected, rejected.Value!.Status);

        var afterRejection = await CreateInitialAssignmentAsync(ManagerA, ManagerC, today, "Second candidate");
        SetActor(ManagerA);
        var cancelled = await _service.CancelAsync(afterRejection.Id, ManagerA.ToString());
        Assert.True(cancelled.IsSuccess, cancelled.Error?.Message);
        Assert.Equal(ProjectHandoverRequest.Statuses.Cancelled, cancelled.Value!.Status);

        var afterCancellation = await CreateInitialAssignmentAsync(ManagerA, ManagerD, today, "Third candidate");
        Assert.Equal(ProjectHandoverRequest.Statuses.Pending, afterCancellation.Status);
        Assert.Equal(3, await _context.ProjectHandoverRequests.CountAsync());
        Assert.Equal(2, await _context.ProjectHandoverRequests.CountAsync(request =>
            request.Status == ProjectHandoverRequest.Statuses.Rejected || request.Status == ProjectHandoverRequest.Statuses.Cancelled));
    }

    [Fact]
    public async Task SoftDeletedPendingRequestDoesNotBlockNewInitialAssignment()
    {
        await PrepareUnassignedProjectAsync();
        var staleRequest = new ProjectHandoverRequest
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProjectId = ProjectId,
            NewProjectManagerId = ManagerB,
            RequestedBy = ManagerA,
            RequestedAt = _clock.GetUtcNow().UtcDateTime.AddDays(-2),
            EffectiveHandoverDate = _clock.GetUtcNow().UtcDateTime.Date,
            InitiationType = ManagerHandoverInitiationType.InitialAssignment,
            Status = ProjectHandoverRequest.Statuses.Pending,
            Reason = "Soft-deleted stale request",
            IsDeleted = true,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        };
        _context.ProjectHandoverRequests.Add(staleRequest);
        await _context.SaveChangesAsync();

        var created = await CreateInitialAssignmentAsync(ManagerA, ManagerC, _clock.GetUtcNow().UtcDateTime.Date, "Retry after stale record");

        Assert.Equal(ProjectHandoverRequest.Statuses.Pending, created.Status);
        var allRequests = await _context.ProjectHandoverRequests.IgnoreQueryFilters().OrderBy(request => request.RequestedAt).ToListAsync();
        Assert.Equal(2, allRequests.Count);
        Assert.Contains(allRequests, request => request.Id == staleRequest.Id && request.IsDeleted);
        Assert.Contains(allRequests, request => request.Id == created.Id && !request.IsDeleted && request.Status == ProjectHandoverRequest.Statuses.Pending);
    }

    [Fact]
    public async Task ParticipantDetailsAreTenantBoundAndRequireRequestParticipationAndViewPermission()
    {
        await PrepareUnassignedProjectAsync();
        var request = await CreateInitialAssignmentAsync(ManagerA, ManagerB, _clock.GetUtcNow().UtcDateTime.Date.AddDays(1), "Participant detail test");

        var requesterDetails = await _service.GetExistingRequestDetailsAsync(request.Id, ManagerA.ToString());
        Assert.True(requesterDetails.IsSuccess, requesterDetails.Error?.Message);
        Assert.Equal(request.Id, requesterDetails.Value!.Id);
        Assert.Equal(ProjectId, requesterDetails.Value.ProjectId);

        var assigneeDetails = await _service.GetExistingRequestDetailsAsync(request.Id, ManagerB.ToString());
        Assert.True(assigneeDetails.IsSuccess, assigneeDetails.Error?.Message);
        Assert.Equal(request.Id, assigneeDetails.Value!.Id);

        var wrongId = await _service.GetExistingRequestDetailsAsync(ProjectId, ManagerB.ToString());
        Assert.True(wrongId.IsFailure);
        Assert.Equal(404, wrongId.Error?.StatusCode);

        var unrelated = await _service.GetExistingRequestDetailsAsync(request.Id, ManagerC.ToString());
        Assert.True(unrelated.IsFailure);
        Assert.Equal(404, unrelated.Error?.StatusCode);

        _permissions.Setup(service => service.GetEffectivePermissionsAsync(ManagerB, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        var denied = await _service.GetExistingRequestDetailsAsync(request.Id, ManagerB.ToString());
        Assert.True(denied.IsFailure);
        Assert.Equal(403, denied.Error?.StatusCode);

        var foreignTenantId = Guid.Parse("52000000-0000-0000-0000-000000000001");
        var foreignActorId = Guid.Parse("52000000-0000-0000-0000-000000000011");
        var foreignUser = NewUser(foreignActorId, "Foreign", "Viewer");
        foreignUser.TenantId = foreignTenantId;
        _context.Users.Add(foreignUser);
        await _context.SaveChangesAsync();
        _permissions.Setup(service => service.GetEffectivePermissionsAsync(foreignActorId, foreignTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.View" });
        SetActor(foreignActorId, foreignTenantId);
        var foreignTenant = await _service.GetExistingRequestDetailsAsync(request.Id, foreignActorId.ToString());
        Assert.True(foreignTenant.IsFailure);
        Assert.Equal(404, foreignTenant.Error?.StatusCode);

        var persistedRequest = await _context.ProjectHandoverRequests.IgnoreQueryFilters().SingleAsync(item => item.Id == request.Id);
        persistedRequest.IsDeleted = true;
        await _context.SaveChangesAsync();
        SetActor(ManagerA);
        var deletedRequest = await _service.GetExistingRequestDetailsAsync(request.Id, ManagerA.ToString());
        Assert.True(deletedRequest.IsFailure);
        Assert.Equal(404, deletedRequest.Error?.StatusCode);
    }

    [Fact]
    public void ParticipantDetailsRouteRequiresCanonicalHandoverViewPolicy()
    {
        var action = typeof(HandoverRequestController).GetMethods()
            .Single(method => method.GetCustomAttributes<HttpGetAttribute>()
                .Any(attribute => attribute.Template == "{id:guid}/details"));

        Assert.Equal(typeof(Guid), action.GetParameters()[0].ParameterType);
        Assert.Contains(action.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Policy == "Handover.View");
    }

    private async Task<ProjectHandoverRequestDto> CreateAsync(Guid actorId, Guid incomingId, DateTime effectiveDate, string reason)
    {
        var result = await CreateAsResultAsync(actorId, incomingId, effectiveDate, reason);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task<Result<ProjectHandoverRequestDto>> CreateAsResultAsync(Guid actorId, Guid incomingId, DateTime effectiveDate, string reason)
    {
        SetActor(actorId);
        return await _service.CreateAsync(ProjectId, new CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = incomingId,
            EffectiveHandoverDate = effectiveDate,
            Reason = reason
        }, actorId.ToString());
    }

    private async Task<Result<ProjectHandoverRequestDto>> CreateInitialAssignmentAsResultAsync(Guid actorId, Guid incomingId, DateTime effectiveDate, string reason)
    {
        SetActor(actorId);
        return await _service.CreateInitialAssignmentAsync(ProjectId, new CreateProjectHandoverRequestDto
        {
            NewProjectManagerId = incomingId,
            EffectiveHandoverDate = effectiveDate,
            Reason = reason
        }, actorId.ToString());
    }

    private async Task<ProjectHandoverRequestDto> CreateInitialAssignmentAsync(Guid actorId, Guid incomingId, DateTime effectiveDate, string reason)
    {
        var result = await CreateInitialAssignmentAsResultAsync(actorId, incomingId, effectiveDate, reason);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task PrepareUnassignedProjectAsync()
    {
        var project = await _context.Projects.SingleAsync(item => item.Id == ProjectId);
        project.ProjectManagerId = null;
        var currentAssignment = await _context.ProjectManagerAssignments.SingleAsync(item => item.ProjectId == ProjectId && item.IsCurrent);
        currentAssignment.IsCurrent = false;
        currentAssignment.EndDate = _clock.GetUtcNow().UtcDateTime.Date;
        await _context.SaveChangesAsync();
    }

    private void SeedProjectAndManagers()
    {
        var role = new Role { Id = Guid.NewGuid(), TenantId = TenantId, Name = "project.manager", RowVersion = new byte[8] };
        var users = new[] { NewUser(ManagerA, "Manager", "A"), NewUser(ManagerB, "Manager", "B"), NewUser(ManagerC, "Manager", "C"), NewUser(ManagerD, "Manager", "D") };
        _context.Roles.Add(role);
        _context.Users.AddRange(users);
        _context.UserRoles.AddRange(users.Select(user => new UserRole
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = user.Id, RoleId = role.Id, RowVersion = new byte[8]
        }));

        var today = _clock.GetUtcNow().UtcDateTime.Date;
        _context.Projects.Add(new Project
        {
            Id = ProjectId,
            TenantId = TenantId,
            Name = "Sequential handover test project",
            Code = "HANDOVER-SEQ",
            ProjectManagerId = ManagerA,
            StartDate = today.AddDays(-30),
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        _context.ProjectManagerAssignments.Add(new ProjectManagerAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ProjectId = ProjectId,
            ProjectManagerId = ManagerA,
            StartDate = today.AddDays(-30),
            IsCurrent = true,
            CreatedBy = ManagerA.ToString(),
            RowVersion = new byte[8]
        });
        _context.SaveChanges();
    }

    private static User NewUser(Guid id, string firstName, string lastName) => new()
    {
        Id = id,
        TenantId = TenantId,
        Email = $"{firstName}.{lastName}@example.test",
        PasswordHash = "test-hash",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true,
        RowVersion = new byte[8]
    };

    private void SetActor(Guid userId, Guid? tenantId = null)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", (tenantId ?? TenantId).ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"Manager {userId.ToString()[^1]}")
        }, "handover-lifecycle-test"));
        _http.HttpContext = context;
    }

    private ProjectPeopleService CreateProjectPeopleService(IResourceReferenceLookup resources)
    {
        var actorId = Guid.Parse(_http.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var tenantId = Guid.Parse(_http.HttpContext.User.FindFirst("tenantId")!.Value);
        var projectIds = tenantId == TenantId ? new HashSet<Guid> { ProjectId } : new HashSet<Guid>();
        var management = new Mock<IManagementContextAccessor>();
        management.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectIds)));
        return new ProjectPeopleService(_context, resources, _audit.Object, managementContext: management.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void SetUtcNow(DateTimeOffset value) => _now = value.ToUniversalTime();
    }
}
