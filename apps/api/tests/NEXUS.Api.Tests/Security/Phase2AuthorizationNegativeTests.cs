using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using NEXUS.Modules.Approvals.Application;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Api;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Modules.WorkItems.Api;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Phase 2 authorization regression coverage. Each test reproduces a specific bypass that
/// existed before remediation: object-level project scope, cost-rate leakage, audit/role/
/// user-administration reachability, approval visibility, workflow transition authorization,
/// governance-people privilege breadth, and invitation-only external stakeholder consent.
/// </summary>
public sealed class Phase2AuthorizationNegativeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _projectA = Guid.NewGuid();
    private readonly Guid _projectB = Guid.NewGuid();
    private readonly Guid _resourceId = Guid.NewGuid();

    public Phase2AuthorizationNegativeTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SqliteLiteralCompatibleModelCustomizer>()
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        ConfigureCaller(_tenantId, _userId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static void ConfigureCaller(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor { HttpContext = httpContext });
    }

    private StubManagementContext ScopeTo(params Guid[] projectIds) => new(projectIds);

    private void SeedResources()
    {
        _context.Set<Resource>().Add(new Resource
        {
            Id = _resourceId,
            TenantId = _tenantId,
            Name = "Scoped Resource",
            Type = "Employee",
            Status = "Active",
            IsActive = true,
            CreatedBy = "test"
        });
        _context.SaveChanges();
    }

    private ResourceAllocation SeedAllocation(Guid projectId, string projectName)
    {
        var allocation = new ResourceAllocation
        {
            TenantId = _tenantId,
            ResourceId = _resourceId,
            ProjectId = projectId,
            ProjectName = projectName,
            AllocationPercentage = 50m,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            Status = ResourceAllocation.StatusConfirmed,
            CreatedBy = "test"
        };
        _context.Set<ResourceAllocation>().Add(allocation);
        _context.SaveChanges();
        return allocation;
    }

    private static Risk SeedRisk(Guid tenantId, Guid projectId) => new()
    {
        TenantId = tenantId,
        Title = "Scoped risk",
        Description = "Scoped risk",
        ProjectId = projectId,
        Impact = "High",
        Likelihood = "High",
        RiskLevel = RiskLevel.High,
        Status = RiskStatus.Identified,
        CreatedBy = "test"
    };

    private static WorkflowTransitionContext Transition(WorkflowEntitySnapshot snapshot, IWorkflowEntityAdapter adapter, Guid tenantId, Guid userId) =>
        new(
            new WorkflowExecutionContext(tenantId, userId.ToString()),
            snapshot.EntityId,
            snapshot,
            new WorkflowState { Code = "Identified" },
            new WorkflowState { Code = "Mitigated" },
            new WorkflowTransition { Code = "MITIGATE" },
            adapter);

    // ---------------------------------------------------------------- project scope

    [Fact]
    public async Task Allocation_reads_are_limited_to_the_callers_project_scope()
    {
        SeedResources();
        var inScope = SeedAllocation(_projectA, "Project A");
        var outOfScope = SeedAllocation(_projectB, "Project B");

        var service = new AllocationService(_context, Mock.Of<IAuditService>(), ScopeTo(_projectA));

        var page = await service.GetPagedAsync(1, 50);
        Assert.Equal(new[] { inScope.Id }, page.Items.Select(item => item.Id));
        Assert.True((await service.GetByIdAsync(inScope.Id)).IsSuccess);
        Assert.True((await service.GetByIdAsync(outOfScope.Id)).IsFailure);
    }

    [Fact]
    public async Task Empty_project_scope_hides_project_allocations_instead_of_falling_open()
    {
        SeedResources();
        SeedAllocation(_projectA, "Project A");

        var service = new AllocationService(_context, Mock.Of<IAuditService>(), ScopeTo());

        Assert.Empty((await service.GetPagedAsync(1, 50)).Items);
    }

    [Fact]
    public async Task Allocation_writes_cannot_target_an_out_of_scope_project()
    {
        SeedResources();
        var outOfScope = SeedAllocation(_projectB, "Project B");
        var service = new AllocationService(_context, Mock.Of<IAuditService>(), ScopeTo(_projectA));

        var create = await service.CreateAsync(new CreateAllocationDto
        {
            ResourceId = _resourceId,
            ProjectId = _projectB,
            ProjectName = "Project B",
            AllocationPercentage = 10m,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(5)
        }, _userId.ToString());

        Assert.True(create.IsFailure);
        Assert.True((await service.DeleteAsync(outOfScope.Id, _userId.ToString())).IsFailure);
        Assert.False((await _context.Set<ResourceAllocation>().IgnoreQueryFilters()
            .SingleAsync(item => item.Id == outOfScope.Id)).IsDeleted);
    }

    [Fact]
    public async Task Payment_settlement_is_not_possible_on_an_out_of_scope_project()
    {
        var obligationId = Guid.NewGuid();
        _context.Set<PaymentObligation>().Add(new PaymentObligation
        {
            Id = obligationId,
            TenantId = _tenantId,
            ProjectId = _projectB,
            ProjectName = "Project B",
            PaymentReference = "PO-1",
            Purpose = "Milestone payment",
            Description = "Milestone payment",
            Amount = 1000m,
            Currency = "GBP",
            DueDate = DateTime.UtcNow.Date.AddDays(7),
            CreatedBy = "test"
        });
        _context.SaveChanges();

        var service = new PaymentObligationService(
            _context,
            Mock.Of<IAuditService>(),
            Mock.Of<IProjectReferenceLookup>(),
            Mock.Of<IProjectMilestoneReferenceLookup>(),
            new HttpContextAccessor { HttpContext = CallerContext(_tenantId, _userId) },
            ScopeTo(_projectA));

        var settlement = await service.RegisterSettlementAsync(obligationId, new RecordPaymentSettlementDto
        {
            IdempotencyKey = Guid.NewGuid(),
            Amount = 100m,
            SettlementDate = DateTime.UtcNow.Date,
            PaymentReference = "SETTLE-1"
        }, _userId.ToString());

        Assert.True(settlement.IsFailure);
        Assert.Empty(_context.Set<PaymentSettlement>().IgnoreQueryFilters().ToList());
    }

    // ---------------------------------------------------------------- cost rates

    [Fact]
    public async Task Capacity_matrix_redacts_cost_rates_without_a_cost_rate_permission()
    {
        SeedResources();
        _context.Set<ResourceCostProfile>().Add(new ResourceCostProfile
        {
            TenantId = _tenantId,
            ResourceId = _resourceId,
            EffectiveFrom = DateTime.UtcNow.Date.AddMonths(-1),
            CompensationType = "Salary",
            Amount = 125m,
            Currency = "GBP",
            CreatedBy = "test"
        });
        _context.SaveChanges();

        var service = new CapacityService(_context, new HttpContextAccessor
        {
            HttpContext = CallerContext(_tenantId, _userId, "Resource.View")
        });

        var matrix = await service.GetMatrixAsync(new CapacityFilterDto());

        Assert.True(matrix.IsSuccess);
        var row = Assert.Single(matrix.Value!.Rows);
        Assert.Null(row.CostPerHour);
        Assert.Null(row.Currency);
        Assert.All(row.Cells.SelectMany(cell => cell.Allocations), allocation => Assert.Null(allocation.PlannedLaborCost));
    }

    [Fact]
    public async Task Capacity_matrix_keeps_cost_rates_for_callers_holding_the_cost_rate_permission()
    {
        SeedResources();
        _context.Set<ResourceCostProfile>().Add(new ResourceCostProfile
        {
            TenantId = _tenantId,
            ResourceId = _resourceId,
            EffectiveFrom = DateTime.UtcNow.Date.AddMonths(-1),
            CompensationType = "Salary",
            Amount = 125m,
            Currency = "GBP",
            CreatedBy = "test"
        });
        _context.SaveChanges();

        var service = new CapacityService(_context, new HttpContextAccessor
        {
            HttpContext = CallerContext(_tenantId, _userId, "Resource.View", "Resource.ViewCostRate")
        });

        var row = Assert.Single((await service.GetMatrixAsync(new CapacityFilterDto())).Value!.Rows);
        Assert.Equal(125m, row.CostPerHour);
        Assert.Equal("GBP", row.Currency);
    }

    [Fact]
    public async Task System_administrator_role_without_cost_rate_permission_does_not_expose_capacity_rates()
    {
        SeedResources();
        _context.Set<ResourceCostProfile>().Add(new ResourceCostProfile
        {
            TenantId = _tenantId,
            ResourceId = _resourceId,
            EffectiveFrom = DateTime.UtcNow.Date.AddMonths(-1),
            CompensationType = "Salary",
            Amount = 125m,
            Currency = "GBP",
            CreatedBy = "test"
        });
        _context.SaveChanges();
        var caller = CallerContext(_tenantId, _userId, "Resource.View");
        caller.User.AddIdentity(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "SystemAdministrator") }, "test-role"));

        var service = new CapacityService(_context, new HttpContextAccessor { HttpContext = caller });
        var row = Assert.Single((await service.GetMatrixAsync(new CapacityFilterDto())).Value!.Rows);

        Assert.Null(row.CostPerHour);
        Assert.Null(row.Currency);
        Assert.All(row.Cells.SelectMany(cell => cell.Allocations), allocation => Assert.Null(allocation.PlannedLaborCost));
    }

    // ---------------------------------------------------------------- audit / administration surfaces

    [Fact]
    public void Audit_endpoint_requires_the_audit_view_policy()
    {
        var policy = typeof(AuditController).GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy);
        Assert.Contains("Audit.View", policy);
    }

    [Fact]
    public async Task Audit_service_clamps_paging_so_the_whole_tenant_log_cannot_be_streamed()
    {
        for (var index = 0; index < 3; index++)
        {
            _context.Set<NEXUS.Modules.Audit.Domain.AuditEntry>().Add(new NEXUS.Modules.Audit.Domain.AuditEntry
            {
                TenantId = _tenantId,
                EntityType = "Project",
                EntityId = _projectA,
                Action = "Update",
                Timestamp = DateTime.UtcNow,
                CreatedBy = "test"
            });
        }
        _context.SaveChanges();

        var page = await new AuditService(_context).GetPagedAsync(0, 100_000);

        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public void Role_directory_requires_the_dedicated_roles_view_permission()
    {
        var policy = typeof(RolesController).GetMethod(nameof(RolesController.GetAll))!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy);
        Assert.Contains("Roles.View", policy);
    }

    [Fact]
    public void Work_item_comments_and_attachments_require_the_edit_policy()
    {
        var controller = typeof(WorkItemController);
        foreach (var name in new[] { nameof(WorkItemController.AddComment), nameof(WorkItemController.AddAttachments), nameof(WorkItemController.DeleteAttachment) })
        {
            var policy = controller.GetMethod(name)!
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Select(attribute => attribute.Policy);
            Assert.Contains("WorkItem.Edit", policy);
        }
    }

    // ---------------------------------------------------------------- approvals

    [Fact]
    public async Task Approval_requests_are_visible_only_to_the_requester_and_approvers()
    {
        var own = new ApprovalRequest
        {
            TenantId = _tenantId,
            Title = "Own request",
            EntityType = "BudgetRevision",
            EntityId = Guid.NewGuid(),
            Type = "Budget",
            Status = "Pending",
            Priority = "Normal",
            RequestedBy = _userId,
            CreatedBy = "test"
        };
        var foreign = new ApprovalRequest
        {
            TenantId = _tenantId,
            Title = "Foreign request",
            EntityType = "BudgetRevision",
            EntityId = Guid.NewGuid(),
            Type = "Budget",
            Status = "Pending",
            Priority = "Normal",
            Amount = 500_000m,
            RequestedBy = _otherUserId,
            CreatedBy = "test"
        };
        var approvedByCaller = new ApprovalRequest
        {
            TenantId = _tenantId,
            Title = "Decided by caller",
            EntityType = "BudgetRevision",
            EntityId = Guid.NewGuid(),
            Type = "Budget",
            Status = "Approved",
            Priority = "Normal",
            RequestedBy = _otherUserId,
            CreatedBy = "test"
        };
        _context.Set<ApprovalRequest>().AddRange(own, foreign, approvedByCaller);
        _context.SaveChanges();
        _context.Set<ApprovalAction>().Add(new ApprovalAction
        {
            TenantId = _tenantId,
            ApprovalRequestId = approvedByCaller.Id,
            ApproverId = _userId,
            Action = "Approved",
            CreatedBy = "test"
        });
        _context.SaveChanges();

        // Secured ApprovalService contract: requires DbContext, IAuditService, IHttpContextAccessor, delegation eligibility.
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.AppendAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var http = new Mock<IHttpContextAccessor>();
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", _tenantId.ToString()),
            new Claim("permission", "ApprovalRequest.View"),
            new Claim("permission", "ApprovalRequest.Submit"),
            new Claim("permission", "ApprovalRequest.Approve"),
            new Claim("permission", "ApprovalRequest.Reject"),
            new Claim("permission", "ApprovalRequest.Delegate"),
            new Claim("permission", "ApprovalRequest.Cancel"),
        }, "test"));
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext { User = claims });
        var service = new ApprovalService(_context, auditMock.Object, http.Object, Mock.Of<IApprovalDelegationEligibilityLookup>());
        var pageResult = await service.GetPagedAsync(1, 50, _userId);
        Assert.True(pageResult.IsSuccess);
        var page = pageResult.Value!;

        Assert.Equal(new[] { own.Id, approvedByCaller.Id }.OrderBy(id => id), page.Items.Select(item => item.Id).OrderBy(id => id));
        Assert.True((await service.GetByIdAsync(foreign.Id, _userId)).IsFailure);
        Assert.True((await service.GetByIdAsync(own.Id, _userId)).IsSuccess);
        // No tenant context / unauthorized caller semantics moved to 401/403; secured service rejects Guid.Empty caller.
        Assert.True((await service.GetByIdAsync(own.Id, Guid.Empty)).IsFailure);
        var boundedPage = await service.GetPagedAsync(1, int.MaxValue, _userId);
        Assert.True(boundedPage.IsSuccess);
        Assert.Equal(100, boundedPage.Value!.PageSize);
    }

    // ---------------------------------------------------------------- workflow transitions

    [Fact]
    public async Task Risk_transition_requires_the_mitigation_permission_not_view_access()
    {
        var risk = SeedRisk(_tenantId, _projectA);
        _context.Set<Risk>().Add(risk);
        _context.SaveChanges();

        var viewOnly = new RiskWorkflowAdapter(
            _context,
            new HttpContextAccessor { HttpContext = CallerContext(_tenantId, _userId, "Risk.View") },
            ScopeTo(_projectA));
        var snapshot = new WorkflowEntitySnapshot(RiskWorkflow.EntityType, risk.Id, new Dictionary<string, object?>());

        Assert.True((await viewOnly.AuthorizeTransitionAsync(Transition(snapshot, viewOnly, _tenantId, _userId))).IsFailure);

        var mitigator = new RiskWorkflowAdapter(
            _context,
            new HttpContextAccessor { HttpContext = CallerContext(_tenantId, _userId, "Risk.View", "Risk.Mitigate") },
            ScopeTo(_projectA));

        Assert.True((await mitigator.AuthorizeTransitionAsync(Transition(snapshot, mitigator, _tenantId, _userId))).IsSuccess);
    }

    [Fact]
    public async Task Risk_transition_is_denied_when_the_project_is_outside_the_scope()
    {
        var risk = SeedRisk(_tenantId, _projectB);
        _context.Set<Risk>().Add(risk);
        _context.SaveChanges();

        var adapter = new RiskWorkflowAdapter(
            _context,
            new HttpContextAccessor { HttpContext = CallerContext(_tenantId, _userId, "Risk.Mitigate") },
            ScopeTo(_projectA));
        var snapshot = new WorkflowEntitySnapshot(RiskWorkflow.EntityType, risk.Id, new Dictionary<string, object?>());

        Assert.True((await adapter.AuthorizeTransitionAsync(Transition(snapshot, adapter, _tenantId, _userId))).IsFailure);
    }

    [Fact]
    public async Task System_administrator_role_without_work_item_permissions_cannot_read_or_transition_work_items()
    {
        var workItem = new NEXUS.Modules.WorkItems.Domain.WorkItem
        {
            TenantId = _tenantId,
            ProjectId = _projectA,
            Title = "Scoped work item",
            Type = "Task",
            Priority = "Medium",
            Status = "Open",
            CreatedBy = "test"
        };
        _context.Set<NEXUS.Modules.WorkItems.Domain.WorkItem>().Add(workItem);
        _context.SaveChanges();
        var caller = CallerContext(_tenantId, _userId);
        caller.User.AddIdentity(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "SystemAdministrator") }, "test-role"));
        var adapter = new WorkItemWorkflowAdapter(
            _context,
            new HttpContextAccessor { HttpContext = caller },
            ScopeTo(_projectA),
            Mock.Of<IWorkItemStatusReferenceLookup>());
        var snapshot = new WorkflowEntitySnapshot(WorkItemWorkflow.EntityType, workItem.Id, new Dictionary<string, object?>());

        var read = await adapter.AuthorizeAsync(new WorkflowExecutionContext(_tenantId, _userId.ToString()), workItem.Id);
        var transition = await adapter.AuthorizeTransitionAsync(Transition(snapshot, adapter, _tenantId, _userId));

        Assert.True(read.IsFailure);
        Assert.True(transition.IsFailure);
    }

    [Fact]
    public async Task Budget_revision_approval_requires_its_canonical_action_permission()
    {
        var budget = new Budget
        {
            TenantId = _tenantId,
            ProjectId = _projectA,
            Name = "Project A budget",
            Category = "Capex",
            PlannedAmount = 1000m,
            ApprovedAmount = 1000m,
            Currency = "GBP",
            FiscalYear = DateTime.UtcNow.Year,
            Status = "Approved",
            CreatedBy = "test"
        };
        _context.Set<Budget>().Add(budget);
        _context.SaveChanges();

        var revision = new BudgetRevision
        {
            TenantId = _tenantId,
            ProjectId = _projectA,
            BudgetId = budget.Id,
            PreviousApprovedAmount = 1000m,
            ProposedApprovedAmount = 1200m,
            Reason = "Rebaseline",
            Status = BudgetRevision.Statuses.Submitted,
            RequestedBy = _otherUserId,
            CreatedBy = "test"
        };
        _context.Set<BudgetRevision>().Add(revision);
        _context.SaveChanges();

        var broadFinanceUser = new FinanceWorkflowAdapter(
            _context,
            Mock.Of<IProjectReferenceLookup>(),
            new HttpContextAccessor { HttpContext = CallerContext(_tenantId, _userId, "ProjectFinance.View", "ProjectFinance.Manage") },
            ScopeTo(_projectA));
        var snapshot = new WorkflowEntitySnapshot(FinanceWorkflow.EntityType, revision.Id, new Dictionary<string, object?>());
        var fromState = new WorkflowState { Code = "Submitted" };
        var toState = new WorkflowState { Code = "Approved" };
        var approvalTransition = new WorkflowTransition { Code = "APPROVE" };
        var broadFinanceContext = new WorkflowTransitionContext(
            new WorkflowExecutionContext(_tenantId, _userId.ToString()),
            revision.Id,
            snapshot,
            fromState,
            toState,
            approvalTransition,
            broadFinanceUser);
        Assert.True((await broadFinanceUser.AuthorizeTransitionAsync(broadFinanceContext)).IsFailure);

        var canonicalApprover = new FinanceWorkflowAdapter(
            _context,
            Mock.Of<IProjectReferenceLookup>(),
            new HttpContextAccessor { HttpContext = CallerContext(_tenantId, _userId, "BudgetRevision.View", "BudgetRevision.Approve") },
            ScopeTo(_projectA));
        var canonicalApproverContext = broadFinanceContext with { EntityAdapter = canonicalApprover };
        Assert.True((await canonicalApprover.AuthorizeTransitionAsync(canonicalApproverContext)).IsSuccess);
    }

    // ---------------------------------------------------------------- governance people

    // ---------------------------------------------------------------- stakeholder invitations

    [Fact]
    public async Task External_stakeholder_acceptance_records_consent_without_creating_a_user()
    {
        const string rawToken = "opaque-one-time-token";
        var stakeholderId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var project = new Project
        {
            Id = _projectA,
            TenantId = _tenantId,
            Name = "External project",
            Code = "EXT-1",
            CreatedBy = "test"
        };
        var resource = new Resource
        {
            Id = _resourceId,
            TenantId = _tenantId,
            Name = "External reviewer",
            Email = "reviewer@example.test",
            Type = ResourceTypeConstants.Outsourced,
            Status = "Active",
            IsActive = true,
            CreatedBy = "test"
        };
        var stakeholder = new ProjectStakeholder
        {
            Id = stakeholderId,
            TenantId = _tenantId,
            ProjectId = project.Id,
            ResourceId = resource.Id,
            Name = resource.Name,
            Email = resource.Email,
            Role = "Reviewer",
            RelationshipType = ProjectRelationshipType.Stakeholder,
            Status = ProjectStakeholderStatus.Active,
            CreatedBy = "test"
        };
        var invitation = new StakeholderInvitation
        {
            Id = invitationId,
            TenantId = _tenantId,
            ProjectId = project.Id,
            ProjectStakeholderId = stakeholderId,
            ResourceId = resource.Id,
            Email = resource.Email!,
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))),
            ExpiresAt = DateTime.UtcNow.AddHours(12),
            Status = StakeholderInvitationStatus.Pending,
            RowVersion = new byte[8],
            CreatedBy = "test"
        };
        _context.AddRange(project, resource, stakeholder, invitation);
        await _context.SaveChangesAsync();

        var service = CreateInvitationService();
        var result = await service.AcceptAsync(rawToken, new NEXUS.Modules.ProjectManagement.Application.DTOs.AcceptStakeholderInvitationDto
        {
            AcceptParticipation = true,
            ReceiveProjectReports = true,
            ReceiveProjectNotifications = false
        });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Accepted", result.Value!.Status);
        Assert.True(result.Value.ReportsConsent);
        Assert.False(result.Value.NotificationsConsent);
        Assert.Empty(await _context.Set<User>().ToListAsync());

        var savedInvitation = await _context.Set<StakeholderInvitation>().SingleAsync(item => item.Id == invitationId);
        Assert.NotNull(savedInvitation.ConsentedAt);
        Assert.False(string.IsNullOrWhiteSpace(savedInvitation.ConsentVersion));
    }

    [Fact]
    public async Task Stakeholder_invitation_requires_explicit_participation_consent()
    {
        var service = CreateInvitationService();
        var result = await service.AcceptAsync("not-used", new NEXUS.Modules.ProjectManagement.Application.DTOs.AcceptStakeholderInvitationDto());

        Assert.True(result.IsFailure);
        Assert.Contains("accept", result.Error!.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task Accepted_external_stakeholder_can_withdraw_selected_email_consent_without_project_access()
    {
        const string rawToken = "opaque-consent-withdrawal-token";
        var stakeholderId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var acceptedAt = DateTime.UtcNow.AddDays(-2);
        var consentedAt = acceptedAt.AddSeconds(2);
        var project = new Project
        {
            Id = _projectA, TenantId = _tenantId, Name = "External project", Code = "EXT-2", CreatedBy = "test"
        };
        var resource = new Resource
        {
            Id = _resourceId, TenantId = _tenantId, Name = "External reviewer", Email = "reviewer@example.test",
            Type = ResourceTypeConstants.Outsourced, Status = "Active", IsActive = true, CreatedBy = "test"
        };
        var stakeholder = new ProjectStakeholder
        {
            Id = stakeholderId, TenantId = _tenantId, ProjectId = project.Id, ResourceId = resource.Id,
            Name = resource.Name, Email = resource.Email, Role = "Reviewer",
            RelationshipType = ProjectRelationshipType.Stakeholder, Status = ProjectStakeholderStatus.Active,
            CreatedBy = "test"
        };
        var invitation = new StakeholderInvitation
        {
            Id = invitationId, TenantId = _tenantId, ProjectId = project.Id, ProjectStakeholderId = stakeholder.Id,
            ResourceId = resource.Id, Email = resource.Email!,
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), Status = StakeholderInvitationStatus.Accepted,
            AcceptedAt = acceptedAt, ConsentedAt = consentedAt, ConsentVersion = "project-stakeholder-test-v1",
            ReportsConsent = true, NotificationsConsent = true, RowVersion = new byte[8], CreatedBy = "test"
        };
        _context.AddRange(project, resource, stakeholder, invitation);
        await _context.SaveChangesAsync();

        var service = CreateInvitationService();
        var result = await service.WithdrawConsentAsync(rawToken, new WithdrawStakeholderConsentDto
        {
            WithdrawReportsConsent = true
        });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Accepted", result.Value!.Status);
        Assert.False(result.Value.ReportsConsent);
        Assert.True(result.Value.NotificationsConsent);
        var savedInvitation = await _context.Set<StakeholderInvitation>().SingleAsync(item => item.Id == invitationId);
        Assert.Equal(StakeholderInvitationStatus.Accepted, savedInvitation.Status);
        Assert.Equal(acceptedAt, savedInvitation.AcceptedAt);
        Assert.Equal(consentedAt, savedInvitation.ConsentedAt);
        Assert.Equal("ExternalStakeholderConsentWithdrawal", savedInvitation.UpdatedBy);
        Assert.Empty(await _context.Set<User>().ToListAsync());

        var validation = await service.ValidateAsync(rawToken);
        Assert.True(validation.IsSuccess, validation.Error?.Message);
        Assert.Equal("Accepted", validation.Value!.Status);
        Assert.False(validation.Value.ReportsConsent);
        Assert.True(validation.Value.NotificationsConsent);
    }

    private StakeholderInvitationService CreateInvitationService() => new(
        _context,
        Mock.Of<IResourceReferenceLookup>(),
        Mock.Of<IEmailNotificationSender>(),
        new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
        Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
        Mock.Of<Microsoft.Extensions.Logging.ILogger<StakeholderInvitationService>>());

    private static DefaultHttpContext CallerContext(Guid tenantId, Guid userId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("tenantId", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
    }

    private sealed class StubManagementContext : IManagementContextAccessor
    {
        public StubManagementContext(IEnumerable<Guid> projectIds)
        {
            Current = new ManagementContext(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ManagementPerspective.ProjectManager,
                ManagementViewMode.Native,
                null,
                null,
                null,
                new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectIds.ToHashSet()));
        }

        public ManagementContext? Current { get; private set; }

        public void Set(ManagementContext context) => Current = context;
    }
}

/// <summary>
/// Adapts SQL Server-only schema metadata for this ephemeral SQLite authorization fixture:
/// max-length type names, NEWID defaults, Unicode literal prefixes, LEN, and the ISJSON check.
/// Other constraint/filter expressions remain intact; production mappings are unchanged.
/// </summary>
internal sealed class SqliteLiteralCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        if (!context.Database.IsSqlite()) return;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToArray())
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnType = property.GetColumnType();
                if (columnType?.EndsWith("(max)", StringComparison.OrdinalIgnoreCase) == true)
                    property.SetColumnType(columnType.StartsWith("varbinary", StringComparison.OrdinalIgnoreCase) ? "BLOB" : "TEXT");

                if (string.Equals(property.GetDefaultValueSql(), "NEWID()", StringComparison.OrdinalIgnoreCase))
                    property.SetDefaultValueSql(null);
            }

            foreach (var constraint in entityType.GetCheckConstraints().ToArray())
            {
                var name = constraint.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                entityType.RemoveCheckConstraint(name);
                if (constraint.Sql.Contains("ISJSON(", StringComparison.OrdinalIgnoreCase)) continue;
                var sql = constraint.Sql
                    .Replace("N'", "'", StringComparison.Ordinal)
                    .Replace("LEN(", "length(", StringComparison.OrdinalIgnoreCase);
                entityType.AddCheckConstraint(
                    name,
                    sql);
            }

            foreach (var index in entityType.GetIndexes())
            {
                var filter = index.GetFilter();
                if (filter?.Contains("N'", StringComparison.Ordinal) == true)
                    index.SetFilter(filter.Replace("N'", "'", StringComparison.Ordinal));
            }
        }
    }
}
