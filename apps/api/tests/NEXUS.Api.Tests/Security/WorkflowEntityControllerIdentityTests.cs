using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.WorkflowEngine.Api;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class WorkflowEntityControllerIdentityTests
{
    [Fact]
    public async Task Workflow_context_uses_NameIdentifier_and_matches_management_context()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        WorkflowExecutionContext? observedContext = null;
        var instanceService = new Mock<IWorkflowInstanceService>();
        instanceService.Setup(service => service.GetInstanceAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, WorkflowExecutionContext, CancellationToken>((_, _, context, _) => observedContext = context)
            .ReturnsAsync(NEXUS.SharedKernel.Result<WorkflowInstanceSummary?>.Success(null));

        var managementContext = new ManagementContextAccessor();
        managementContext.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, ManagementScope.Empty));
        var httpContext = CreateHttpContext(tenantId, actorId);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IManagementContextAccessor>(managementContext)
            .BuildServiceProvider();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"workflow-controller-identity-{Guid.NewGuid():N}")
            .Options);
        var controller = new WorkflowEntityController(
            instanceService.Object,
            Mock.Of<IWorkflowTransitionQueryService>(),
            Mock.Of<IWorkflowExecutionService>(),
            db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.GetInstance("BudgetRevision", Guid.NewGuid());

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(observedContext);
        Assert.Equal(actorId.ToString(), observedContext!.UserId);
        Assert.Equal(tenantId, observedContext.TenantId);
    }

    [Fact]
    public async Task Workflow_routes_reject_missing_actor_before_calling_services()
    {
        var tenantId = Guid.NewGuid();
        var instanceService = new Mock<IWorkflowInstanceService>();
        var httpContext = CreateHttpContext(tenantId, actorId: null);
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"workflow-controller-no-actor-{Guid.NewGuid():N}")
            .Options);
        var controller = new WorkflowEntityController(
            instanceService.Object,
            Mock.Of<IWorkflowTransitionQueryService>(),
            Mock.Of<IWorkflowExecutionService>(),
            db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.GetInstance("BudgetRevision", Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
        instanceService.Verify(service => service.GetInstanceAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Workflow_routes_reject_actor_mismatch_with_management_context()
    {
        var tenantId = Guid.NewGuid();
        var tokenActorId = Guid.NewGuid();
        var contextActorId = Guid.NewGuid();
        var managementContext = new ManagementContextAccessor();
        managementContext.Set(new ManagementContext(
            contextActorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, ManagementScope.Empty));
        var httpContext = CreateHttpContext(tenantId, tokenActorId);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IManagementContextAccessor>(managementContext)
            .BuildServiceProvider();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"workflow-controller-mismatched-actor-{Guid.NewGuid():N}")
            .Options);
        var instanceService = new Mock<IWorkflowInstanceService>();
        var controller = new WorkflowEntityController(
            instanceService.Object,
            Mock.Of<IWorkflowTransitionQueryService>(),
            Mock.Of<IWorkflowExecutionService>(),
            db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.GetInstance("BudgetRevision", Guid.NewGuid());

        Assert.IsType<ForbidResult>(result);
        instanceService.Verify(service => service.GetInstanceAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DefaultHttpContext CreateHttpContext(Guid tenantId, Guid? actorId)
    {
        var claims = new List<Claim> { new("tenantId", tenantId.ToString()) };
        if (actorId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, actorId.Value.ToString()));
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return context;
    }
}
