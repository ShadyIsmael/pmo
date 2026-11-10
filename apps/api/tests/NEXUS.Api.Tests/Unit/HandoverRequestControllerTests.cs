using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class HandoverRequestControllerTests
{
    [Fact]
    public async Task Pending_for_me_preserves_service_forbidden_status_as_403()
    {
        const string userId = "manager-user-1";
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
        const string userId = "manager-user-1";
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

    private static HandoverRequestController CreateController(Mock<IProjectHandoverRequestService> service, string userId) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
                }
            }
        };
}
