using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class ProjectStatusReportWorkflowTests
{
    [Fact]
    public void Accepts_only_the_complete_report_workflow_graph()
    {
        var version = CreateVersion();

        Assert.True(ProjectStatusReportWorkflow.ValidateWorkflowVersion(version).IsSuccess);

        version.Transitions.Add(new WorkflowTransition
        {
            Code = "SKIP_REVIEW",
            FromState = version.States.Single(state => state.Code == "Draft"),
            ToState = version.States.Single(state => state.Code == "Published"),
            IsActive = true
        });

        Assert.True(ProjectStatusReportWorkflow.ValidateWorkflowVersion(version).IsFailure);
    }

    [Fact]
    public async Task Generate_transition_requires_a_persisted_snapshot_fact()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var adapter = CreateAdapter(db, "StatusReport.Generate");
        var transition = CreateTransitionContext(adapter, "GENERATE", "Draft", "Generated", new Dictionary<string, object?>
        {
            ["HasGeneratedSnapshot"] = false
        });

        var result = await adapter.ValidateTransitionAsync(transition);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Report_author_cannot_approve_their_own_snapshot()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var authorId = Guid.NewGuid();
        var adapter = CreateAdapter(db, "StatusReport.Approve");
        var facts = new Dictionary<string, object?>
        {
            ["HasGeneratedSnapshot"] = true,
            ["PreparedByUserId"] = authorId
        };

        var selfApproval = CreateTransitionContext(adapter, "APPROVE", "Submitted", "Approved", facts, authorId.ToString());
        var otherReviewer = CreateTransitionContext(adapter, "APPROVE", "Submitted", "Approved", facts, Guid.NewGuid().ToString());

        Assert.True((await adapter.ValidateTransitionAsync(selfApproval)).IsFailure);
        Assert.True((await adapter.ValidateTransitionAsync(otherReviewer)).IsSuccess);
    }

    [Fact]
    public async Task Role_name_alone_does_not_grant_status_report_workflow_permission()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "SystemAdministrator")], "unit-test");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        var adapter = new ProjectStatusReportWorkflowAdapter(db, accessor, Mock.Of<IManagementContextAccessor>());
        var transition = CreateTransitionContext(adapter, "SUBMIT", "Generated", "Submitted", new Dictionary<string, object?>
        {
            ["HasGeneratedSnapshot"] = true
        });

        var result = await adapter.ValidateTransitionAsync(transition);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
    }

    [Fact]
    public void Publication_requires_a_configured_section_but_accepts_legacy_snapshots()
    {
        var complete = System.Text.Json.JsonSerializer.Serialize(new NEXUS.Modules.ProjectManagement.Application.DTOs.ProjectPerformanceDto
        {
            IncludedReportSections = ["ExecutiveSummary", "PerformanceKPIs", "PlanPerformance", "Milestones", "RisksIssues", "DecisionsRequired"]
        });
        var partial = System.Text.Json.JsonSerializer.Serialize(new NEXUS.Modules.ProjectManagement.Application.DTOs.ProjectPerformanceDto
        {
            IncludedReportSections = ["ExecutiveSummary", "PerformanceKPIs"]
        });
        var legacy = "{\"ProjectId\":\"00000000-0000-0000-0000-000000000001\"}";

        Assert.True(ProjectStatusReportWorkflow.HasConfiguredSections(complete));
        Assert.True(ProjectStatusReportWorkflow.HasConfiguredSections(partial));
        Assert.True(ProjectStatusReportWorkflow.HasConfiguredSections(legacy));
        var empty = System.Text.Json.JsonSerializer.Serialize(new NEXUS.Modules.ProjectManagement.Application.DTOs.ProjectPerformanceDto
        {
            IncludedReportSections = []
        });
        Assert.False(ProjectStatusReportWorkflow.HasConfiguredSections(empty));
    }

    private static ProjectStatusReportWorkflowAdapter CreateAdapter(DbContext db, string permission)
    {
        var identity = new ClaimsIdentity([new Claim("permission", permission)], "unit-test");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return new ProjectStatusReportWorkflowAdapter(db, accessor, Mock.Of<IManagementContextAccessor>());
    }

    private static WorkflowTransitionContext CreateTransitionContext(
        ProjectStatusReportWorkflowAdapter adapter,
        string transitionCode,
        string fromCode,
        string toCode,
        IReadOnlyDictionary<string, object?> facts,
        string userId = "reviewer")
    {
        var from = new WorkflowState { Code = fromCode, IsActive = true };
        var to = new WorkflowState { Code = toCode, IsActive = true };
        var transition = new WorkflowTransition { Code = transitionCode, FromState = from, ToState = to, IsActive = true };
        var execution = new WorkflowExecutionContext(Guid.NewGuid(), userId);
        var entity = new WorkflowEntitySnapshot(ProjectStatusReportWorkflow.EntityType, Guid.NewGuid(), facts);
        return new WorkflowTransitionContext(execution, entity.EntityId, entity, from, to, transition, adapter);
    }

    private static WorkflowDefinitionVersion CreateVersion()
    {
        var version = new WorkflowDefinitionVersion();
        var states = new Dictionary<string, WorkflowState>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in new[] { "Draft", "Generated", "Submitted", "Approved", "Returned", "Published" })
        {
            var state = new WorkflowState
            {
                Code = code,
                IsInitial = code == "Draft",
                IsFinal = code == "Published",
                IsActive = true
            };
            states.Add(code, state);
            version.States.Add(state);
        }

        foreach (var (code, from, to) in new[]
        {
            ("GENERATE", "Draft", "Generated"),
            ("SUBMIT", "Generated", "Submitted"),
            ("APPROVE", "Submitted", "Approved"),
            ("RETURN", "Submitted", "Returned"),
            ("REOPEN", "Returned", "Draft"),
            ("PUBLISH", "Approved", "Published")
        })
        {
            version.Transitions.Add(new WorkflowTransition
            {
                Code = code,
                FromState = states[from],
                ToState = states[to],
                IsActive = true
            });
        }
        return version;
    }
}
