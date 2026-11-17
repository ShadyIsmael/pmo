using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class HandoverRequestControllerTests
{
    [Fact]
    public void Project_handover_listing_uses_View_while_initiation_uses_AdminInitiate()
    {
        var viewPolicy = Assert.Single(typeof(ProjectController).GetMethod(nameof(ProjectController.GetHandoverRequests))!
            .GetCustomAttributes<AuthorizeAttribute>());
        var adminPolicy = Assert.Single(typeof(ProjectController).GetMethod(nameof(ProjectController.CreateAdministrativeHandoverRequest))!
            .GetCustomAttributes<AuthorizeAttribute>());
        var initialAssignmentPolicy = Assert.Single(typeof(ProjectController).GetMethod(nameof(ProjectController.CreateInitialManagerAssignment))!
            .GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal("Handover.View", viewPolicy.Policy);
        Assert.Equal("Handover.AdminInitiate", adminPolicy.Policy);
        Assert.Equal("Handover.AdminInitiate", initialAssignmentPolicy.Policy);
    }

    [Fact]
    public void Pending_for_me_uses_organization_scoped_approval_authorization()
    {
        var method = typeof(HandoverRequestController).GetMethod(nameof(HandoverRequestController.GetPendingForMe))!;
        var policy = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        var scope = Assert.Single(method.GetCustomAttributes<OrganizationScopedCollectionPermissionAttribute>());

        Assert.Equal("Handover.Approve", policy.Policy);
        Assert.Equal(new[] { "Handover.Approve" }, scope.PermissionKeys);
    }

    [Fact]
    public void Project_request_routes_use_service_checks_for_request_specific_organization_scope()
    {
        foreach (var actionName in new[]
        {
            nameof(HandoverRequestController.GetDetails), nameof(HandoverRequestController.Review),
            nameof(HandoverRequestController.Accept), nameof(HandoverRequestController.Reject),
            nameof(HandoverRequestController.Cancel)
        })
        {
            var method = typeof(HandoverRequestController).GetMethod(actionName)!;
            Assert.Empty(method.GetCustomAttributes<AuthorizeAttribute>());
        }
    }

    [Fact]
    public async Task Pending_for_me_preserves_service_forbidden_status_as_403()
    {
        var userId = TestUserId.ToString("D");
        const string message = "The current user lacks project handover scope.";
        var service = new Mock<IProjectHandoverRequestService>();
        service.Setup(item => item.GetPendingForMeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ProjectHandoverRequestDto>>.Failure(Error.Forbidden(message)));
        var controller = CreateController(service, userId);

        var response = await controller.GetPendingForMe(CancellationToken.None);

        var objectResponse = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResponse.StatusCode);
        service.Verify(item => item.GetPendingForMeAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Pending_for_me_returns_200_and_the_service_result_when_authorized()
    {
        var userId = TestUserId.ToString("D");
        IReadOnlyList<ProjectHandoverRequestDto> requests = Array.Empty<ProjectHandoverRequestDto>();
        var service = new Mock<IProjectHandoverRequestService>();
        service.Setup(item => item.GetPendingForMeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ProjectHandoverRequestDto>>.Success(requests));
        var controller = CreateController(service, userId);

        var response = await controller.GetPendingForMe(CancellationToken.None);

        var okResponse = Assert.IsType<OkObjectResult>(response);
        Assert.Same(requests, okResponse.Value);
        service.Verify(item => item.GetPendingForMeAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    private static readonly Guid TestUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestTenantId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    private static HandoverRequestController CreateController(Mock<IProjectHandoverRequestService> service, string userId)
    {
        var management = new Mock<IManagementContextAccessor>();
        management.Setup(accessor => accessor.Current).Returns(new ManagementContext(
            TestUserId, TestTenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, ManagementScope.Empty));
        return new HandoverRequestController(service.Object, management.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim("tenantId", TestTenantId.ToString("D"))], "test"))
                }
            }
        };
    }
}
