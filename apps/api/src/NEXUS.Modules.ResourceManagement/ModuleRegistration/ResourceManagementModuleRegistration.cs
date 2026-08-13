using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.ResourceManagement.Application;

namespace NEXUS.Modules.ResourceManagement.ModuleRegistration;

public static class ResourceManagementModuleRegistration
{
    public static IServiceCollection AddResourceManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IAllocationService, AllocationService>();
        services.AddScoped<ICapacityService, CapacityService>();
        services.AddScoped<IResourceFinderService, ResourceFinderService>();
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<IVendorService, VendorService>();
        return services;
    }
}
