using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Validators;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.ModuleRegistration;

public static class ResourceManagementModuleRegistration
{
    public static IServiceCollection AddResourceManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateResourceDto>, CreateResourceValidator>();
        services.AddScoped<IValidator<UpdateResourceDto>, UpdateResourceValidator>();
        services.AddScoped<ResourceService>();
        services.AddScoped<IResourceService>(sp => sp.GetRequiredService<ResourceService>());
        services.AddScoped<IResourceReferenceLookup>(sp => sp.GetRequiredService<ResourceService>());
        services.AddScoped<IResourceIdentityLinker>(sp => sp.GetRequiredService<ResourceService>());
        services.AddScoped<IAllocationService, AllocationService>();
        services.AddScoped<ICapacityService, CapacityService>();
        services.AddScoped<IResourceFinderService, ResourceFinderService>();
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IVendorCategoryService, VendorCategoryService>();
        services.AddScoped<IResourceCostProfileService, ResourceCostProfileService>();
        services.AddScoped<IProjectRoleService, ProjectRoleService>();
        services.AddScoped<ResourceLaborCostCalculator>();
        services.AddScoped<IProjectEvmLaborCostLookup, ProjectEvmLaborCostLookup>();
        services.AddScoped<ICostUtilizationAnalyticsService, CostUtilizationAnalyticsService>();
        return services;
    }
}
