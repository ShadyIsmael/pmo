using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ResourceManagement.Api;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class ResourceMutationActorBindingTests
{
    [Fact]
    public async Task Resource_mutation_controllers_reject_tenant_only_principals_without_service_calls()
    {
        var tenantId = Guid.NewGuid();
        var user = Principal(tenantId, userId: null);

        var allocationService = new Mock<IAllocationService>(MockBehavior.Strict);
        var allocation = Controller(new AllocationController(allocationService.Object), user);
        var allocationResponse = await allocation.Create(new CreateAllocationDto(), CancellationToken.None);

        var roleService = new Mock<IProjectRoleService>(MockBehavior.Strict);
        var role = Controller(new ProjectRoleController(roleService.Object), user);
        var roleResponse = await role.Create(new CreateLookupDto(), CancellationToken.None);

        var resourceService = new Mock<IResourceService>(MockBehavior.Strict);
        var resource = Controller(new ResourceController(
            resourceService.Object,
            new Mock<IValidator<CreateResourceDto>>(MockBehavior.Strict).Object,
            new Mock<IValidator<UpdateResourceDto>>(MockBehavior.Strict).Object), user);
        var resourceResponse = await resource.Create(new CreateResourceDto(), CancellationToken.None);

        var costProfileService = new Mock<IResourceCostProfileService>(MockBehavior.Strict);
        var costProfile = Controller(new ResourceCostProfileController(costProfileService.Object), user);
        var costProfileResponse = await costProfile.Create(Guid.NewGuid(), new CreateResourceCostProfileDto(), CancellationToken.None);

        var skillService = new Mock<ISkillService>(MockBehavior.Strict);
        var skill = Controller(new SkillController(skillService.Object), user);
        var skillResponse = await skill.Create(new CreateSkillDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(allocationResponse);
        Assert.IsType<UnauthorizedResult>(roleResponse);
        Assert.IsType<UnauthorizedResult>(resourceResponse);
        Assert.IsType<UnauthorizedResult>(costProfileResponse);
        Assert.IsType<UnauthorizedResult>(skillResponse);
        allocationService.VerifyNoOtherCalls();
        roleService.VerifyNoOtherCalls();
        resourceService.VerifyNoOtherCalls();
        costProfileService.VerifyNoOtherCalls();
        skillService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Allocation_mutation_requires_the_trusted_management_context()
    {
        var service = new Mock<IAllocationService>(MockBehavior.Strict);
        var tenantId = Guid.NewGuid();
        var controller = Controller(
            new AllocationController(service.Object),
            Principal(tenantId, Guid.NewGuid()));

        var response = await controller.Create(new CreateAllocationDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Business_mutation_controllers_reject_valid_actor_without_management_context()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var principal = Principal(tenantId, actorId);

        var roleService = new Mock<IProjectRoleService>(MockBehavior.Strict);
        var role = Controller(new ProjectRoleController(roleService.Object), principal);
        var roleResponse = await role.Create(new CreateLookupDto(), CancellationToken.None);

        var resourceService = new Mock<IResourceService>(MockBehavior.Strict);
        var resource = Controller(new ResourceController(
            resourceService.Object,
            new Mock<IValidator<CreateResourceDto>>(MockBehavior.Strict).Object,
            new Mock<IValidator<UpdateResourceDto>>(MockBehavior.Strict).Object), principal);
        var resourceResponse = await resource.Create(new CreateResourceDto(), CancellationToken.None);

        var profileService = new Mock<IResourceCostProfileService>(MockBehavior.Strict);
        var profile = Controller(new ResourceCostProfileController(profileService.Object), principal);
        var profileResponse = await profile.Create(Guid.NewGuid(), new CreateResourceCostProfileDto(), CancellationToken.None);

        var skillService = new Mock<ISkillService>(MockBehavior.Strict);
        var skill = Controller(new SkillController(skillService.Object), principal);
        var skillResponse = await skill.Create(new CreateSkillDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(roleResponse);
        Assert.IsType<ForbidResult>(resourceResponse);
        Assert.IsType<ForbidResult>(profileResponse);
        Assert.IsType<ForbidResult>(skillResponse);
        roleService.VerifyNoOtherCalls();
        resourceService.VerifyNoOtherCalls();
        profileService.VerifyNoOtherCalls();
        skillService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Project_role_mutation_is_denied_in_read_only_view_as()
    {
        var service = new Mock<IProjectRoleService>(MockBehavior.Strict);
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var management = new TestManagementContextAccessor(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs,
            null,
            null,
            null,
            ManagementScope.Empty));
        var controller = Controller(
            new ProjectRoleController(service.Object, management),
            Principal(tenantId, actorId));

        var response = await controller.Create(new CreateLookupDto(), CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Project_role_service_rejects_read_only_context_before_persistence()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var management = new TestManagementContextAccessor(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.ReadOnlyViewAs,
            null,
            null,
            null,
            ManagementScope.Empty));
        var service = new ProjectRoleService(
            new DbContext(new DbContextOptionsBuilder<DbContext>().Options),
            Mock.Of<IAuditService>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = Principal(tenantId, actorId) } },
            management);

        var result = await service.CreateAsync(new CreateLookupDto(), actorId.ToString());

        Assert.Equal(403, result.Error?.StatusCode);
    }

    private static T Controller<T>(T controller, ClaimsPrincipal principal) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid? userId)
    {
        var claims = new List<Claim>
        {
            new("tenantId", tenantId.ToString()),
            new("permission", "Resource.Assign"),
            new("permission", "Resource.Create"),
            new("permission", "ReferenceData.Create")
        };
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class TestManagementContextAccessor(ManagementContext? current) : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; } = current;

        public void Set(ManagementContext context) => Current = context;
    }
}
