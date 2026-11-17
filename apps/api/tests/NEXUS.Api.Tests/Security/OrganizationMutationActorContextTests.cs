using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class OrganizationMutationActorContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Organizational_unit_mutations_forbid_missing_or_empty_actor(bool emptyActor)
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var claims = new List<Claim> { new("tenantId", tenantId.ToString("D")) };
        if (emptyActor) claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString("D")));
        var service = new Mock<IOrganizationalUnitService>(MockBehavior.Strict);
        var controller = CreateOrganizationalUnitController(service, claims, actorId, tenantId);
        var unit = new SaveOrganizationalUnitDto { Name = "Unit", TypeCode = "Division" };
        var type = new SaveOrganizationalUnitTypeDto { Code = "Division", NameEn = "Division", NameAr = "إدارة" };
        var membership = new SaveResourceOrganizationalUnitDto { ResourceId = Guid.NewGuid(), OrganizationalUnitId = Guid.NewGuid() };
        var reportingLine = new SaveResourceReportingLineDto { ResourceId = Guid.NewGuid(), ManagerResourceId = Guid.NewGuid() };
        var endDate = new EndEffectiveDateRequest { EffectiveTo = DateTime.UtcNow };
        var id = Guid.NewGuid();

        Assert.IsType<ForbidResult>(await controller.CreateUnit(unit, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.UpdateUnit(id, unit, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.DeactivateUnit(id, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.ReactivateUnit(id, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.DeleteUnit(id, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.CreateType(type, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.UpdateType(id, type, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.AssignMembership(membership, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.EndMembership(id, endDate, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.AssignReportingLine(reportingLine, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.EndReportingLine(id, endDate, CancellationToken.None));

        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Organizational_unit_mutations_forbid_a_management_context_that_does_not_match_the_claims(bool mismatchTenant)
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IOrganizationalUnitService>(MockBehavior.Strict);
        var controller = CreateOrganizationalUnitController(
            service,
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")), new Claim("tenantId", tenantId.ToString("D"))],
            mismatchTenant ? actorId : Guid.NewGuid(),
            mismatchTenant ? Guid.NewGuid() : tenantId);

        Assert.IsType<ForbidResult>(await controller.CreateUnit(new SaveOrganizationalUnitDto(), CancellationToken.None));

        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Organizational_unit_mutation_passes_the_claim_bound_actor_to_the_service()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var request = new SaveOrganizationalUnitDto { Name = "Unit", TypeCode = "Division" };
        var service = new Mock<IOrganizationalUnitService>(MockBehavior.Strict);
        service.Setup(item => item.CreateUnitAsync(request, actorId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NEXUS.SharedKernel.Result<OrganizationalUnitDto>.Success(new OrganizationalUnitDto()));
        var controller = CreateOrganizationalUnitController(
            service,
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")), new Claim("tenantId", tenantId.ToString("D"))],
            actorId,
            tenantId);

        Assert.IsType<OkObjectResult>(await controller.CreateUnit(request, CancellationToken.None));

        service.Verify(item => item.CreateUnitAsync(request, actorId.ToString("D"), It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Organizational_unit_mutations_forbid_read_only_view_as_context()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IOrganizationalUnitService>(MockBehavior.Strict);
        var controller = CreateOrganizationalUnitController(
            service,
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")), new Claim("tenantId", tenantId.ToString("D"))],
            actorId,
            tenantId,
            ManagementViewMode.ReadOnlyViewAs);

        Assert.IsType<ForbidResult>(await controller.CreateUnit(new SaveOrganizationalUnitDto(), CancellationToken.None));

        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Work_item_status_mutations_forbid_missing_or_empty_actor_and_read_only_context()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var service = new Mock<IWorkItemStatusReferenceService>(MockBehavior.Strict);
        var request = new SaveWorkItemStatusRequest { Code = "Active", NameEn = "Active", NameAr = "نشط" };
        var id = Guid.NewGuid();
        var controller = CreateStatusController(
            service,
            [new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString("D")), new Claim("tenantId", tenantId.ToString("D"))],
            actorId,
            tenantId,
            ManagementViewMode.Native);

        Assert.IsType<ForbidResult>(await controller.Create(request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Update(id, request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.Deactivate(id, CancellationToken.None));

        var validActorController = CreateStatusController(
            service,
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")), new Claim("tenantId", tenantId.ToString("D"))],
            actorId,
            tenantId,
            ManagementViewMode.ReadOnlyViewAs);
        Assert.IsType<ForbidResult>(await validActorController.Create(request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await validActorController.Update(id, request, CancellationToken.None));
        Assert.IsType<ForbidResult>(await validActorController.Deactivate(id, CancellationToken.None));

        service.VerifyNoOtherCalls();
    }

    private static OrganizationalUnitController CreateOrganizationalUnitController(
        Mock<IOrganizationalUnitService> service,
        IEnumerable<Claim> claims,
        Guid contextUserId,
        Guid contextTenantId,
        ManagementViewMode viewMode = ManagementViewMode.Native)
    {
        var managementContext = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        managementContext.SetupGet(accessor => accessor.Current).Returns(CreateContext(contextUserId, contextTenantId, viewMode));
        return new OrganizationalUnitController(service.Object, managementContext.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(claims) }
        };
    }

    private static WorkItemStatusReferenceController CreateStatusController(
        Mock<IWorkItemStatusReferenceService> service,
        IEnumerable<Claim> claims,
        Guid contextUserId,
        Guid contextTenantId,
        ManagementViewMode viewMode)
    {
        var managementContext = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        managementContext.SetupGet(accessor => accessor.Current).Returns(CreateContext(contextUserId, contextTenantId, viewMode));
        return new WorkItemStatusReferenceController(service.Object, managementContext.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(claims) }
        };
    }

    private static ManagementContext CreateContext(Guid userId, Guid tenantId, ManagementViewMode viewMode = ManagementViewMode.Native) =>
        new(userId, tenantId, ManagementPerspective.ProjectManager, viewMode, null, null, null, ManagementScope.Empty);

    private static DefaultHttpContext CreateHttpContext(IEnumerable<Claim> claims) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "authenticated-test"))
    };
}
