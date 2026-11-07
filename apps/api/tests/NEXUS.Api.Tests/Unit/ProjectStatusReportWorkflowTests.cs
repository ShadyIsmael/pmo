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
    public void Accepts_only_the_direct_draft_to_published_workflow_graph()
    {
        var version = CreateVersion();

        Assert.True(ProjectStatusReportWorkflow.ValidateWorkflowVersion(version).IsSuccess);

        version.States.Add(new WorkflowState { Code = "Generated", IsActive = true });

        Assert.True(ProjectStatusReportWorkflow.ValidateWorkflowVersion(version).IsFailure);
    }

    [Fact]
    public void Recognizes_the_legacy_graph_for_safe_versioning()
    {
        Assert.True(ProjectStatusReportWorkflow.IsLegacyWorkflowVersion(CreateLegacyVersion()));
        Assert.False(ProjectStatusReportWorkflow.IsLegacyWorkflowVersion(CreateVersion()));
    }

    [Fact]
    public async Task Direct_publish_requires_configured_report_sections()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var adapter = CreateAdapter(db, "StatusReport.Publish");
        var transition = CreateTransitionContext(adapter, "PUBLISH", "Draft", "Published", new Dictionary<string, object?>
        {
            ["HasConfiguredSections"] = false
        });

        var result = await adapter.ValidateTransitionAsync(transition);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Direct_publish_requires_explicit_permission_claim()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var adapter = CreateAdapter(db, "StatusReport.Edit");
        var transition = CreateTransitionContext(adapter, "PUBLISH", "Draft", "Published", new Dictionary<string, object?>
        {
            ["HasConfiguredSections"] = true
        });

        var result = await adapter.ValidateTransitionAsync(transition);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Role_name_alone_does_not_grant_status_report_workflow_permission()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "SystemAdministrator")], "unit-test");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        var adapter = new ProjectStatusReportWorkflowAdapter(db, accessor, Mock.Of<IManagementContextAccessor>());
        var transition = CreateTransitionContext(adapter, "PUBLISH", "Draft", "Published", new Dictionary<string, object?>
        {
            ["HasConfiguredSections"] = true
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
        foreach (var code in new[] { "Draft", "Published" })
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
            ("PUBLISH", "Draft", "Published")
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

    private static WorkflowDefinitionVersion CreateLegacyVersion()
    {
        var version = new WorkflowDefinitionVersion();
        var states = new Dictionary<string, WorkflowState>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in new[] { "Draft", "Generated", "Submitted", "Approved", "Returned", "Published" })
        {
            var state = new WorkflowState { Code = code, IsInitial = code == "Draft", IsFinal = code == "Published", IsActive = true };
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
            version.Transitions.Add(new WorkflowTransition { Code = code, FromState = states[from], ToState = states[to], IsActive = true });
        }
        return version;
    }
}
