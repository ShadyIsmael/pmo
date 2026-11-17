using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectPeopleContactPrivacyTests
{
    [Fact]
    public async Task Project_view_does_not_receive_contact_emails_without_contact_permissions()
    {
        var teamMember = new ProjectPersonDto { Source = "TeamMember", Email = "resource@example.test" };
        var stakeholder = new ProjectPersonDto { Source = "Stakeholder", Email = "stakeholder@example.test" };
        var people = new List<ProjectPersonDto> { teamMember, stakeholder };
        var service = new Mock<IProjectPeopleService>();
        service.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ProjectPersonDto>>.Success(people));
        var controller = CreateController(service.Object, new HashSet<string>());

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(teamMember.Email);
        Assert.Null(stakeholder.Email);
    }

    [Fact]
    public async Task Resource_and_stakeholder_contact_permissions_are_applied_to_their_own_records()
    {
        var teamMember = new ProjectPersonDto { Source = "TeamMember", Email = "resource@example.test" };
        var stakeholder = new ProjectPersonDto { Source = "Stakeholder", Email = "stakeholder@example.test" };
        var people = new List<ProjectPersonDto> { teamMember, stakeholder };
        var service = new Mock<IProjectPeopleService>();
        service.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<ProjectPersonDto>>.Success(people));
        var controller = CreateController(service.Object, new HashSet<string> { "Resources.View" });

        await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("resource@example.test", teamMember.Email);
        Assert.Null(stakeholder.Email);
    }

    private static ProjectPeopleController CreateController(IProjectPeopleService service, IReadOnlySet<string> permissions)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(item => item.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? __, string policy) => permissions.Contains(policy)
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        return new ProjectPeopleController(service, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
