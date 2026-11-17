using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class OrganizationApiAuthorizationScopeTests
{
    [Theory]
    [InlineData(nameof(OrganizationController.GetById))]
    [InlineData(nameof(OrganizationController.GetPaged))]
    public void Organization_directory_reads_require_tenant_configuration_management(string actionName)
    {
        AssertActionPolicy(typeof(OrganizationController), actionName, "Settings.ManageTenantConfiguration");
    }

    [Fact]
    public void Business_unit_reads_require_tenant_configuration_management()
    {
        var authorization = Assert.Single(typeof(BusinessUnitController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Equal("Settings.ManageTenantConfiguration", authorization.Policy);
    }

    [Fact]
    public async Task Department_lookup_allows_team_read_without_tenant_configuration_management()
    {
        var service = new Mock<IDepartmentService>();
        service.Setup(item => item.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<DepartmentResponseDto>(Array.Empty<DepartmentResponseDto>(), 1, 200, 0));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(item => item.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object? __, string policy) => policy == "Team.View"
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        var controller = new DepartmentController(service.Object, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetPaged();

        Assert.IsType<OkObjectResult>(result);
        service.Verify(item => item.GetPagedAsync(1, 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Department_lookup_rejects_users_without_a_directory_read_permission()
    {
        var service = new Mock<IDepartmentService>();
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(item => item.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        var controller = new DepartmentController(service.Object, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetPaged();

        Assert.IsType<ForbidResult>(result);
        service.Verify(item => item.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Organization_directory_projection_contains_only_lookup_fields()
    {
        var propertyNames = typeof(OrganizationDirectoryItemDto)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Id", "Name" }, propertyNames);
    }

    private static void AssertActionPolicy(Type controllerType, string actionName, string expectedPolicy)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);
        var authorization = Assert.Single(action!.GetCustomAttributes<AuthorizeAttribute>(inherit: false));
        Assert.Equal(expectedPolicy, authorization.Policy);
    }
}
