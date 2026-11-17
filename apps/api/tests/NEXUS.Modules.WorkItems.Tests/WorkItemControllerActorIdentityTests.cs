using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.WorkItems.Api;
using NEXUS.Modules.WorkItems.Application;
using Xunit;

namespace NEXUS.Modules.WorkItems.Tests;

public sealed class WorkItemControllerActorIdentityTests
{
    [Fact]
    public async Task ProvisionWorkflow_AuthenticatedTenantOnlyPrincipalIsRejectedWithoutServiceMutation()
    {
        var workItems = new Mock<IWorkItemService>(MockBehavior.Strict);
        var workflowProvisioning = new Mock<IWorkItemWorkflowProvisioningService>(MockBehavior.Strict);
        var controller = new WorkItemController(workItems.Object, workflowProvisioning.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "WorkItem.Edit")
                    ], "test"))
                }
            }
        };

        var response = await controller.ProvisionWorkflow(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        workItems.VerifyNoOtherCalls();
        workflowProvisioning.VerifyNoOtherCalls();
    }
}
