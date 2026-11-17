using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Portfolio.Api;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PortfolioControllerActorBindingTests
{
    [Fact]
    public async Task Create_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeServiceCall()
    {
        var service = new Mock<IPortfolioService>(MockBehavior.Strict);
        var controller = new PortfolioController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", "Portfolio.Create")
                    ], "test"))
                }
            }
        };

        var response = await controller.Create(new SavePortfolioDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PortfolioService_ArchiveRejectsReadOnlyViewBeforeDatabaseAccess()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext();
        var service = CreateService(db, actorId, tenantId, actorId, ManagementViewMode.ReadOnlyViewAs);

        var result = await service.ArchiveAsync(Guid.NewGuid(), actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task PortfolioService_CreateRejectsMismatchedManagementActorBeforeDatabaseAccess()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        using var db = NewDbContext();
        var service = CreateService(db, actorId, tenantId, Guid.NewGuid(), ManagementViewMode.Native);

        var result = await service.CreateAsync(new SavePortfolioDto(), actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error?.StatusCode);
    }

    private static PortfolioService CreateService(DbContext db, Guid actorId, Guid tenantId, Guid contextActorId, ManagementViewMode viewMode)
    {
        var managementContext = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        managementContext.Setup(accessor => accessor.Current).Returns(new ManagementContext(
            contextActorId,
            tenantId,
            ManagementPerspective.PortfolioManager,
            viewMode,
            null,
            null,
            null,
            ManagementScope.Empty));

        return new PortfolioService(db, new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("tenantId", tenantId.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString())
                ], "test"))
            }
        }, managementContext.Object);
    }

    private static DbContext NewDbContext() => new(new DbContextOptionsBuilder<DbContext>().Options);
}
