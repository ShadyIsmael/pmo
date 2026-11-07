using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectPeopleAuthorizationPolicyTests
{
    [Fact]
    public void Reading_project_people_requires_the_canonical_project_view_policy()
    {
        var get = typeof(ProjectPeopleController).GetMethod(nameof(ProjectPeopleController.Get))!;

        Assert.Contains(get.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(),
            attribute => attribute.Policy == "Project.View");
    }

    [Theory]
    [InlineData("Sponsor", "Stakeholders.Manage", true)]
    [InlineData("Other", "Stakeholders.Manage", true)]
    [InlineData("Sponsor", "Resource.Assign", false)]
    [InlineData("Stakeholder", "Resource.Assign", false)]
    [InlineData("TeamMember", "Resource.Assign", true)]
    [InlineData("TeamMember", "Stakeholders.Manage", false)]
    public async Task Managing_people_requires_the_permission_that_matches_the_relationship(
        string relationship,
        string grantedPolicy,
        bool expected)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) =>
                policy == grantedPolicy ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var controller = new ProjectPeopleController(Mock.Of<IProjectPeopleService>(), authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var method = typeof(ProjectPeopleController).GetMethod("CanManageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var allowed = await (Task<bool>)method.Invoke(controller, new object?[] { relationship })!;

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public async Task Removing_a_team_group_requires_team_view_from_the_persisted_assignment()
    {
        var projectId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var service = new Mock<IProjectPeopleService>();
        service.Setup(projectPeople => projectPeople.IsTeamGroupAssignmentAsync(projectId, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(auth => auth.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, string policy) =>
                policy == "Resource.Assign" ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var controller = new ProjectPeopleController(service.Object, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Remove(projectId, memberId, new RemoveProjectPersonDto { Source = "TeamMember" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        service.Verify(projectPeople => projectPeople.RevokeOrRemoveAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
