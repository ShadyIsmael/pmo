using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.ResourceManagement.Api;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class VendorActorIdentityTests
{
    [Fact]
    public async Task VendorCreate_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeServiceCall()
    {
        var service = new Mock<IVendorService>(MockBehavior.Strict);
        var controller = new VendorController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = TenantOnlyContext("Resource.Create") }
        };

        var response = await controller.Create(new CreateVendorDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VendorCategoryCreate_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeServiceCall()
    {
        var service = new Mock<IVendorCategoryService>(MockBehavior.Strict);
        var controller = new VendorCategoryController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = TenantOnlyContext("ReferenceData.Create") }
        };

        var response = await controller.Create(new CreateLookupDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VendorCreate_ValidActorWithoutManagementContextIsRejectedBeforeServiceCall()
    {
        var service = new Mock<IVendorService>(MockBehavior.Strict);
        var controller = new VendorController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = ActorContext("Resource.Create") }
        };

        var response = await controller.Create(new CreateVendorDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VendorCategoryCreate_ValidActorWithoutManagementContextIsRejectedBeforeServiceCall()
    {
        var service = new Mock<IVendorCategoryService>(MockBehavior.Strict);
        var controller = new VendorCategoryController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = ActorContext("ReferenceData.Create") }
        };

        var response = await controller.Create(new CreateLookupDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }

    private static DefaultHttpContext TenantOnlyContext(string permission) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", Guid.NewGuid().ToString()),
            new Claim("permission", permission)
        ], "test"))
    };

    private static DefaultHttpContext ActorContext(string permission) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenantId", Guid.NewGuid().ToString()),
            new Claim("permission", permission)
        ], "test"))
    };
}
