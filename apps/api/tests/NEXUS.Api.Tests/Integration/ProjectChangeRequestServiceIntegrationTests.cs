using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public class ProjectChangeRequestServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ProjectChangeRequestWorkflowService _service;
    private readonly Mock<IProjectWorkflowConfigurationService> _mockProjectWorkflows;
    private readonly Mock<IProjectChangeRequestWorkflowProvisioningService> _mockWorkflowProvisioner;
    private readonly Mock<IWorkflowInstanceService> _mockWorkflowInstances;
    private readonly Mock<IWorkflowExecutionService> _mockWorkflowExecution;
    private readonly Mock<NEXUS.Modules.Identity.Application.IUserEffectivePermissionService> _mockPermissions;
    private readonly Mock<NEXUS.SharedKernel.Management.IManagementContextAccessor> _mockManagement;
    private readonly Mock<NEXUS.Modules.Notifications.Application.INotificationService> _mockNotifications;

    private static readonly Guid _tenantA_Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid _tenantB_Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid _userA_Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid _userB_Id = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid _assignedApprover_Id = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid _unassignedApprover_Id = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid _projectId_A = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid _projectId_B = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid _pcrId_A = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid _pcrId_B = Guid.Parse("20000000-0000-0000-0000-000000000004");

    public ProjectChangeRequestServiceIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);

        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        SeedData();

        _mockAuditService = new Mock<IAuditService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockProjectWorkflows = new Mock<IProjectWorkflowConfigurationService>();
        _mockWorkflowProvisioner = new Mock<IProjectChangeRequestWorkflowProvisioningService>();
        _mockWorkflowInstances = new Mock<IWorkflowInstanceService>();
        _mockWorkflowExecution = new Mock<IWorkflowExecutionService>();
        _mockPermissions = new Mock<NEXUS.Modules.Identity.Application.IUserEffectivePermissionService>();
        _mockManagement = new Mock<NEXUS.SharedKernel.Management.IManagementContextAccessor>();
        _mockManagement.SetupGet(item => item.Current).Returns(new ManagementContext(_userA_Id, _tenantA_Id,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { _projectId_A })));
        _mockNotifications = new Mock<NEXUS.Modules.Notifications.Application.INotificationService>();
        _service = new ProjectChangeRequestWorkflowService(_context, _mockAuditService.Object, _mockHttpContextAccessor.Object,
            _mockProjectWorkflows.Object, _mockWorkflowProvisioner.Object, _mockWorkflowInstances.Object, _mockWorkflowExecution.Object,
            _mockPermissions.Object, _mockManagement.Object, _mockNotifications.Object);
    }

    private void SeedData()
    {
        var users = new List<User>
        {
            new User { Id = _userA_Id, TenantId = _tenantA_Id, Email = "userA@tenantA.example", PasswordHash = "hash", FirstName = "UserA", LastName = "TenantA", RowVersion = new byte[8] },
            new User { Id = _userB_Id, TenantId = _tenantB_Id, Email = "userB@tenantB.example", PasswordHash = "hash", FirstName = "UserB", LastName = "TenantB", RowVersion = new byte[8] },
            new User { Id = _assignedApprover_Id, TenantId = _tenantA_Id, Email = "approver@tenantA.example", PasswordHash = "hash", FirstName = "Assigned", LastName = "Approver", RowVersion = new byte[8] },
            new User { Id = _unassignedApprover_Id, TenantId = _tenantA_Id, Email = "other.approver@tenantA.example", PasswordHash = "hash", FirstName = "Unassigned", LastName = "Approver", RowVersion = new byte[8] }
        };

        var projects = new List<Project>
        {
            new Project { Id = _projectId_A, TenantId = _tenantA_Id, Name = "Project A Name", Code = "PA", Description = "Description A", Status = "Active", Currency = "GBP", CreatedBy = _userA_Id.ToString(), RowVersion = new byte[8] },
            new Project { Id = _projectId_B, TenantId = _tenantB_Id, Name = "Project B Name", Code = "PB", Description = "Description B", Status = "Active", Currency = "GBP", CreatedBy = _userB_Id.ToString(), RowVersion = new byte[8] }
        };

        var pcrs = new List<ProjectChangeRequest>
        {
            new ProjectChangeRequest
            {
                Id = _pcrId_A,
                TenantId = _tenantA_Id,
                ProjectId = _projectId_A,
                Number = 1,
                Type = ProjectChangeRequest.ChangeTypes.Administrative,
                Reason = "Update project governance",
                Status = ProjectChangeRequest.Statuses.Pending,
                RequestedBy = _userA_Id,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = _userA_Id.ToString(),
                RowVersion = new byte[8]
            },
            new ProjectChangeRequest
            {
                Id = _pcrId_B,
                TenantId = _tenantB_Id,
                ProjectId = _projectId_B,
                Number = 1,
                Type = ProjectChangeRequest.ChangeTypes.Baseline,
                Reason = "Update project baseline",
                Status = ProjectChangeRequest.Statuses.Pending,
                RequestedBy = _userB_Id,
                RequestedAt = DateTime.UtcNow,
                CreatedBy = _userB_Id.ToString(),
                RowVersion = new byte[8]
            }
        };

        _context.Users.AddRange(users);
        _context.Projects.AddRange(projects);
        _context.ProjectChangeRequests.AddRange(pcrs);
        _context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private void SetupHttpContext(string tenantId, string userId)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim("tenantId", tenantId),
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"));
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        AppDbContext.ConfigureTenantResolution(_mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task CreateAsync_PersistsDraftAndDoesNotApplyBudgetBeforeApproval()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());
        var workflowDefinitionId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();
        SeedWorkflowInstance(workflowDefinitionId, workflowVersionId, workflowInstanceId);
        _context.Projects.Single(item => item.Id == _projectId_A).Budget = 1000m;
        await _context.SaveChangesAsync();
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_userA_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Manage" });
        _mockProjectWorkflows.Setup(item => item.ResolveForNewRecordAsync(_projectId_A, WorkflowDomains.ChangeRequest,
                ProjectChangeRequestWorkflow.EntityType, It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProjectWorkflowSelection?>.Success(new ProjectWorkflowSelection(WorkflowDomains.ChangeRequest,
                ProjectChangeRequestWorkflow.EntityType, false, workflowDefinitionId, workflowVersionId, "TEST_CHANGE_REQUEST", "Draft")));
        _mockWorkflowInstances.Setup(item => item.ProvisionAsync(ProjectChangeRequestWorkflow.EntityType, It.IsAny<Guid>(),
                workflowDefinitionId, workflowVersionId, It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WorkflowInstanceSummary>.Success(new WorkflowInstanceSummary(workflowInstanceId, workflowDefinitionId,
                workflowVersionId, "TEST_CHANGE_REQUEST", 1, ProjectChangeRequestWorkflow.EntityType, Guid.Empty,
                "Draft", "Draft", "مسودة", false, DateTime.UtcNow, _userA_Id.ToString(), null, true, new byte[8])));
        _mockAuditService.Setup(item => item.LogAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var dto = new CreateProjectChangeRequestDto
        {
            ProjectId = _projectId_A,
            Type = ProjectChangeRequest.ChangeTypes.Baseline,
            Reason = "Align baseline to budget",
            Justification = "Approved budget changes require a baseline update",
            BudgetChangeAmount = 250m
        };

        var result = await _service.CreateAsync(dto, _userA_Id.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Number);
        Assert.Equal(ProjectChangeRequest.Statuses.Draft, result.Value.Status);
        Assert.Equal(workflowInstanceId, result.Value.WorkflowInstanceId);
        Assert.Equal(1000m, (await _context.Projects.SingleAsync(item => item.Id == _projectId_A)).Budget);
        var persisted = await _service.GetByProjectIdAsync(_projectId_A);
        Assert.True(persisted.IsSuccess);
        Assert.Contains(persisted.Value!, request => request.Id == result.Value.Id && request.Status == ProjectChangeRequest.Statuses.Draft);
    }

    private void SeedWorkflowInstance(Guid definitionId, Guid versionId, Guid instanceId, Guid? entityId = null)
    {
        var stateId = Guid.NewGuid();
        var state = new WorkflowState
        {
            Id = stateId, TenantId = _tenantA_Id, WorkflowDefinitionVersionId = versionId,
            Code = "Draft", NameEn = "Draft", NameAr = "مسودة", IsInitial = true, CreatedBy = "tests"
        };
        var version = new WorkflowDefinitionVersion
        {
            Id = versionId, TenantId = _tenantA_Id, WorkflowDefinitionId = definitionId,
            VersionNumber = 1, LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published,
            PublishedAt = DateTime.UtcNow, PublishedBy = "tests", CreatedBy = "tests", States = new List<WorkflowState> { state }
        };
        var definition = new WorkflowDefinition
        {
            Id = definitionId, TenantId = _tenantA_Id, Code = "TEST_CHANGE_REQUEST", Name = "Test Change Request",
            Domain = WorkflowDomains.ChangeRequest, EntityType = ProjectChangeRequestWorkflow.EntityType,
            ActivePublishedVersionId = null,
            Versions = new List<WorkflowDefinitionVersion> { version }, CreatedBy = "tests"
        };
        _context.WorkflowDefinitions.Add(definition);
        _context.SaveChanges();
        definition.ActivePublishedVersionId = versionId;
        definition.ActivePublishedVersion = version;
        _context.SaveChanges();

        _context.WorkflowInstances.Add(new WorkflowInstance
        {
            Id = instanceId, TenantId = _tenantA_Id, WorkflowDefinitionId = definitionId, WorkflowDefinition = definition,
            WorkflowDefinitionVersionId = versionId, WorkflowDefinitionVersion = version, EntityType = ProjectChangeRequestWorkflow.EntityType,
            EntityId = entityId ?? Guid.NewGuid(), CurrentStateId = stateId, CurrentState = state,
            StartedBy = _userA_Id.ToString(), CreatedBy = "tests"
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByIdAsync_SameTenantAccess_Succeeds()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByIdAsync(_pcrId_A);

        Assert.True(result.IsSuccess);
        Assert.Equal(_pcrId_A, result.Value!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenantAccess_ReturnsNotFound()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByIdAsync(_pcrId_B);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound().Code, result.Error!.Code);
    }

    [Fact]
    public async Task ProjectChangeRequestGeneralReadsFailClosedWhenManagementProjectScopeIsEmpty()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());
        _mockManagement.SetupGet(item => item.Current).Returns(new ManagementContext(_userA_Id, _tenantA_Id,
            ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null, ManagementScope.Empty));

        var byId = await _service.GetByIdAsync(_pcrId_A);
        var byProject = await _service.GetByProjectIdAsync(_projectId_A);

        Assert.Equal(404, byId.Error?.StatusCode);
        Assert.Equal(404, byProject.Error?.StatusCode);
    }

    [Fact]
    public async Task GetApprovalOptionsAsync_IncludesOnlyActiveSameTenantExternalUsersWithEffectiveApprovalPermission()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_userA_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Manage" });
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_assignedApprover_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Approve" });
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_unassignedApprover_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var result = await _service.GetApprovalOptionsAsync(_projectId_A);

        Assert.True(result.IsSuccess);
        var externalApprover = Assert.Single(result.Value!.Approvers);
        Assert.Equal(_assignedApprover_Id, externalApprover.UserId);
        Assert.Equal("Business Approver", externalApprover.ProjectRole);
        Assert.False(externalApprover.IsSponsor);
        Assert.False(externalApprover.IsMandatory);
        Assert.DoesNotContain(result.Value.Approvers, item => item.UserId == _userB_Id);
        Assert.DoesNotContain(result.Value.Approvers, item => item.UserId == _userA_Id);
    }

    [Fact]
    public async Task ReassignApprovalAsync_CannotReplaceMandatorySponsorWithNonSponsor()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_userA_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Manage" });
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_assignedApprover_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Approve" });
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_unassignedApprover_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Approve" });

        var project = await _context.Projects.SingleAsync(item => item.Id == _projectId_A);
        project.SponsorId = _assignedApprover_Id;
        var assignmentId = Guid.NewGuid();
        _context.ProjectChangeRequestApprovals.Add(new ProjectChangeRequestApproval
        {
            Id = assignmentId,
            TenantId = _tenantA_Id,
            ProjectChangeRequestId = _pcrId_A,
            WorkflowInstanceId = Guid.NewGuid(),
            ProjectId = _projectId_A,
            OriginalApproverUserId = _assignedApprover_Id,
            OriginalApproverNameSnapshot = "Assigned Approver",
            AssignedToUserId = _assignedApprover_Id,
            ApproverNameSnapshot = "Assigned Approver",
            RoleSnapshot = "Project Sponsor",
            IsSponsor = true,
            IsMandatory = true,
            Sequence = 1,
            Status = ProjectChangeRequestApproval.Statuses.Pending,
            CreatedBy = _userA_Id.ToString(),
            RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        var result = await _service.ReassignApprovalAsync(_pcrId_A, assignmentId,
            new ReassignProjectChangeRequestApprovalDto { NewApproverUserId = _unassignedApprover_Id, Reason = "Sponsor unavailable" },
            _userA_Id.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        var persisted = await _context.ProjectChangeRequestApprovals.SingleAsync(item => item.Id == assignmentId);
        Assert.Equal(_assignedApprover_Id, persisted.AssignedToUserId);
        Assert.True(persisted.IsSponsor);
        Assert.True(persisted.IsMandatory);
        Assert.Empty(await _context.ProjectChangeRequestApprovalHistories.Where(item => item.ProjectChangeRequestApprovalId == assignmentId).ToListAsync());
    }

    [Fact]
    public async Task AssignedExternalApprover_CanReviewOutsideProjectAndManagementScope_ButCannotUseGeneralProjectChangeRequestRead()
    {
        var request = await _context.ProjectChangeRequests.SingleAsync(item => item.Id == _pcrId_A);
        var workflowId = Guid.NewGuid();
        SeedWorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), workflowId, request.Id);
        request.WorkflowInstanceId = workflowId;
        await _context.SaveChangesAsync();
        _context.ProjectChangeRequestApprovals.Add(new ProjectChangeRequestApproval
        {
            Id = Guid.NewGuid(), TenantId = _tenantA_Id, ProjectChangeRequestId = request.Id, ProjectId = _projectId_A,
            WorkflowInstanceId = workflowId, OriginalApproverUserId = _assignedApprover_Id,
            OriginalApproverNameSnapshot = "Assigned Approver", AssignedToUserId = _assignedApprover_Id,
            ApproverNameSnapshot = "Assigned Approver", RoleSnapshot = "Business Approver", IsSponsor = false,
            IsMandatory = false, Sequence = 1, Status = ProjectChangeRequestApproval.Statuses.Pending,
            CreatedBy = _userA_Id.ToString(), RowVersion = new byte[8]
        });
        await _context.SaveChangesAsync();

        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_assignedApprover_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Approve" });
        _mockPermissions.Setup(item => item.GetEffectivePermissionsAsync(_unassignedApprover_Id, _tenantA_Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ChangeRequest.Approve" });
        var outsideProjectScope = new ManagementContext(_assignedApprover_Id, _tenantA_Id, ManagementPerspective.ProjectManager,
            ManagementViewMode.Native, null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { Guid.NewGuid() }));
        _mockManagement.SetupGet(item => item.Current).Returns(outsideProjectScope);
        SetupHttpContext(_tenantA_Id.ToString(), _assignedApprover_Id.ToString());

        var review = await _service.GetApprovalReviewAsync(_pcrId_A);
        var generalRead = await _service.GetByIdAsync(_pcrId_A);
        var inbox = await _service.GetPendingAsync();

        Assert.True(review.IsSuccess);
        Assert.Equal("Project A Name", review.Value!.BusinessObjectName);
        Assert.Equal("Update project governance", review.Value.Reason);
        Assert.True(review.Value.IsReadOnly);
        Assert.True(review.Value.CanDecide);
        Assert.True(generalRead.IsFailure);
        Assert.Equal(Error.NotFound().Code, generalRead.Error!.Code);
        Assert.True(inbox.IsSuccess);
        var inboxItem = Assert.Single(inbox.Value!);
        Assert.Equal(_pcrId_A, inboxItem.Id);
        Assert.Equal("Project A Name", inboxItem.ProjectName);
        Assert.Equal("Update project governance", inboxItem.Reason);
        Assert.DoesNotContain("ProjectId", typeof(PendingProjectChangeApprovalDto).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("Approvals", typeof(PendingProjectChangeApprovalDto).GetProperties().Select(property => property.Name));

        SetupHttpContext(_tenantA_Id.ToString(), _unassignedApprover_Id.ToString());
        var unassignedReview = await _service.GetApprovalReviewAsync(_pcrId_A);
        Assert.True(unassignedReview.IsFailure);
        Assert.Equal(404, unassignedReview.Error?.StatusCode);
    }

    [Fact]
    public async Task GetByProjectIdAsync_SameTenantAccess_Succeeds()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByProjectIdAsync(_projectId_A);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Contains(result.Value!, pcr => pcr.Id == _pcrId_A);
    }

    [Fact]
    public async Task GetByProjectIdAsync_CrossTenantAccess_ReturnsNotFound()
    {
        SetupHttpContext(_tenantA_Id.ToString(), _userA_Id.ToString());

        var result = await _service.GetByProjectIdAsync(_projectId_B);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound().Code, result.Error!.Code);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
