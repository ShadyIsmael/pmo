using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.WorkflowEngine.Api;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Guards;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class WorkflowTrustedGuardMetadataTests
{
    private static readonly Guid TenantId = Guid.Parse("a1100000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Controller_UsesRefreshedSingularPermissionAndRoleClaims_AndIgnoresStalePluralClaims()
    {
        var controller = CreateController(Principal(
            new Claim("tenantId", TenantId.ToString()),
            new Claim("permissions", "[\"Workflow.Approve\"]"),
            new Claim("roles", "[\"Approver\"]"),
            new Claim("permission", "WorkItem.View"),
            new Claim(ClaimTypes.Role, "Reader")));

        var context = BuildExecutionContext(controller);
        var permissionCheck = await EvaluatePermission(context, "Workflow.Approve");
        var roleCheck = await EvaluateRole(context, "Approver");

        Assert.Equal("[\"WorkItem.View\"]", context.Metadata!["permissions"]);
        Assert.Equal("[\"Reader\"]", context.Metadata["roles"]);
        Assert.True(permissionCheck.IsFailure);
        Assert.True(roleCheck.IsFailure);
    }

    [Fact]
    public async Task Controller_RefreshedClaimsGrantMatchingWorkflowPermissionAndRole()
    {
        var controller = CreateController(Principal(
            new Claim("tenantId", TenantId.ToString()),
            new Claim("permissions", "[\"Workflow.Admin\"]"),
            new Claim("roles", "[\"PlatformAdmin\"]"),
            new Claim("permission", "Workflow.Approve"),
            new Claim(ClaimTypes.Role, "Approver")));

        var context = BuildExecutionContext(controller);

        Assert.True((await EvaluatePermission(context, "Workflow.Approve")).IsSuccess);
        Assert.True((await EvaluateRole(context, "Approver")).IsSuccess);
    }

    [Fact]
    public async Task RequestMetadataCannotOverrideTrustedSecurityKeys_AndOrdinaryMetadataIsPreserved()
    {
        var trusted = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["permissions"] = "[\"Workflow.Approve\"]",
            ["roles"] = "[\"Approver\"]",
            ["source"] = "refreshed-claims"
        };
        var request = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PERMISSIONS"] = "[\"Workflow.Admin\"]",
            ["rOlEs"] = "[\"PlatformAdmin\"]",
            ["businessReference"] = "safe-request-value"
        };

        var merged = MergeTransitionMetadata(trusted, request);
        var context = ExecutionContext(merged);

        Assert.Equal("[\"Workflow.Approve\"]", merged["permissions"]);
        Assert.Equal("[\"Approver\"]", merged["roles"]);
        Assert.Equal("safe-request-value", merged["businessReference"]);
        Assert.True((await EvaluatePermission(context, "Workflow.Approve")).IsSuccess);
        Assert.True((await EvaluateRole(context, "Approver")).IsSuccess);
    }

    [Fact]
    public async Task RequestMetadataCannotCreateTrustedPermissionsOrRolesWhenServerClaimsAreAbsent()
    {
        var request = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["permissions"] = "[\"Workflow.Admin\"]",
            ["ROLES"] = "[\"PlatformAdmin\"]"
        };

        var merged = MergeTransitionMetadata(null, request);
        var context = ExecutionContext(merged);
        var safeHistoryMetadata = GetSafeRequestMetadata(request);

        Assert.DoesNotContain("permissions", merged.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles", merged.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("permissions", safeHistoryMetadata.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles", safeHistoryMetadata.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.True((await EvaluatePermission(context, "Workflow.Admin")).IsFailure);
        Assert.True((await EvaluateRole(context, "PlatformAdmin")).IsFailure);
    }

    private static WorkflowEntityController CreateController(ClaimsPrincipal principal)
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase($"workflow-controller-{Guid.NewGuid():N}")
            .Options;
        var db = new DbContext(options);
        var controller = new WorkflowEntityController(
            Mock.Of<IWorkflowInstanceService>(),
            Mock.Of<IWorkflowTransitionQueryService>(),
            Mock.Of<IWorkflowExecutionService>(),
            db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
        return controller;
    }

    private static WorkflowExecutionContext BuildExecutionContext(WorkflowEntityController controller)
    {
        var method = typeof(WorkflowEntityController).GetMethod("BuildExecutionContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (WorkflowExecutionContext)method.Invoke(controller, null)!;
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    private static WorkflowExecutionContext ExecutionContext(IReadOnlyDictionary<string, string?> metadata) =>
        new(TenantId, "user-1", Metadata: metadata);

    private static Task<Result> EvaluatePermission(WorkflowExecutionContext execution, string requiredPermission)
    {
        using var configuration = JsonDocument.Parse(JsonSerializer.Serialize(new { permission = requiredPermission }));
        return new RequirePermissionGuardHandler().EvaluateAsync(TransitionContext(execution), configuration.RootElement);
    }

    private static Task<Result> EvaluateRole(WorkflowExecutionContext execution, string requiredRole)
    {
        using var configuration = JsonDocument.Parse(JsonSerializer.Serialize(new { role = requiredRole }));
        return new RequireRoleGuardHandler().EvaluateAsync(TransitionContext(execution), configuration.RootElement);
    }

    private static WorkflowTransitionContext TransitionContext(WorkflowExecutionContext execution) => new(
        execution,
        Guid.NewGuid(),
        new WorkflowEntitySnapshot("TestEntity", Guid.NewGuid(), new Dictionary<string, object?>()),
        new WorkflowState(),
        new WorkflowState(),
        new WorkflowTransition(),
        Mock.Of<IWorkflowEntityAdapter>());

    private static IReadOnlyDictionary<string, string?> MergeTransitionMetadata(
        IReadOnlyDictionary<string, string?>? trusted,
        IReadOnlyDictionary<string, string?>? request)
    {
        var method = typeof(WorkflowExecutionService).GetMethod("MergeTransitionMetadata", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<string, string?>)method.Invoke(null, new object?[] { trusted, request })!;
    }

    private static IReadOnlyDictionary<string, string?> GetSafeRequestMetadata(
        IReadOnlyDictionary<string, string?>? request)
    {
        var method = typeof(WorkflowExecutionService).GetMethod("GetSafeRequestMetadata", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<string, string?>)method.Invoke(null, new object?[] { request })!;
    }
}
