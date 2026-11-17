using NEXUS.Api.Management;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using Xunit;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Tests.Unit;

public sealed class ProjectManagerUpdateControllerTests
{
    [Fact]
    public void UpdateDtoTracksWhetherManagerWasExplicitlyProvided()
    {
        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var omitted = System.Text.Json.JsonSerializer.Deserialize<UpdateProjectDto>("{}", jsonOptions);
        var explicitNull = System.Text.Json.JsonSerializer.Deserialize<UpdateProjectDto>("{\"projectManagerId\":null}", jsonOptions);

        Assert.False(omitted!.ProjectManagerIdWasProvided);
        Assert.True(explicitNull!.ProjectManagerIdWasProvided);
    }

    [Fact]
    public async Task Update_MapsCraftedManagerChangeConflictToHttp409()
    {
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var projectService = new Mock<IProjectService>();
        projectService
            .Setup(service => service.UpdateAsync(projectId, It.IsAny<UpdateProjectDto>(), actorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProjectDto>.Failure(Error.Conflict("Project Manager changes must use the authorized Handover workflow.")));

        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, ManagementScope.Empty));
        var controller = new ProjectController(
            projectService.Object,
            projectSnapshotService: null!,
            projectHandoverRequestService: null!,
            projectStatusReportService: null!,
            projectOverviewService: null!,
            projectReportingService: null!,
            managementContext: management);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim("tenantId", tenantId.ToString())
        }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var response = await controller.Update(projectId, new UpdateProjectDto
        {
            Name = "Crafted update",
            ProjectManagerId = Guid.NewGuid()
        }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        projectService.Verify(service => service.UpdateAsync(projectId, It.Is<UpdateProjectDto>(dto =>
            dto.Name == "Crafted update" && dto.ProjectManagerId.HasValue), actorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
