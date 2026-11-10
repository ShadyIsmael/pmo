using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Meetings.Api;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class MeetingMinutesControllerTests
{
    [Fact]
    public async Task GetByPublicId_Forwards_public_id_to_authoritative_service_lookup()
    {
        var publicId = Guid.NewGuid();
        var detail = new MeetingDetailDto { Id = Guid.NewGuid(), PublicId = publicId, Title = "Meeting" };
        var meetingService = new Mock<IMeetingService>();
        meetingService
            .Setup(service => service.GetByPublicIdAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MeetingDetailDto>.Success(detail));

        var controller = new MeetingController(meetingService.Object, Mock.Of<IMeetingWorkspaceService>());

        var response = await controller.GetByPublicId(publicId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(detail, ok.Value);
        meetingService.Verify(service => service.GetByPublicIdAsync(publicId, It.IsAny<CancellationToken>()), Times.Once);
        meetingService.Verify(service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetByPublicId_Requires_meeting_view_policy()
    {
        var endpoint = typeof(MeetingController).GetMethod(nameof(MeetingController.GetByPublicId));
        var route = Assert.Single(endpoint!.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true)
            .Cast<HttpGetAttribute>());
        Assert.Equal("{publicId:guid}", route.Template);

        var authorize = Assert.Single(endpoint!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());

        Assert.Equal(MeetingPermissions.View, authorize.Policy);
    }

    [Fact]
    public async Task GetMinutes_ReturnsJsonNullWithOkStatus_WhenMeetingExistsWithoutMinutes()
    {
        var meetingId = Guid.NewGuid();
        var meetingService = new Mock<IMeetingService>();
        meetingService
            .Setup(service => service.GetMinutesAsync(meetingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MeetingMinutesDto?>.Success(null));

        var controller = new MeetingController(meetingService.Object, Mock.Of<IMeetingWorkspaceService>());

        var response = await controller.GetMinutes(meetingId, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(response);
        Assert.Equal(200, json.StatusCode);
        Assert.Null(json.Value);
    }

    [Fact]
    public async Task GetMinutes_KeepsNotFound_WhenMeetingCannotBeResolvedInActorScope()
    {
        var meetingId = Guid.NewGuid();
        var meetingService = new Mock<IMeetingService>();
        meetingService
            .Setup(service => service.GetMinutesAsync(meetingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MeetingMinutesDto?>.Failure(Error.NotFound("Meeting not found.")));

        var controller = new MeetingController(meetingService.Object, Mock.Of<IMeetingWorkspaceService>());

        var response = await controller.GetMinutes(meetingId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(404, notFound.StatusCode);
    }
}
