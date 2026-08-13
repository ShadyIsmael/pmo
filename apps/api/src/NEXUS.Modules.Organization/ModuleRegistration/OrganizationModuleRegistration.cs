using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Infrastructure;

namespace NEXUS.Modules.Organization.ModuleRegistration;

public static class OrganizationModuleRegistration
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            return new OrganizationService(context);
        });
        services.AddScoped<IBusinessUnitService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            return new BusinessUnitService(context);
        });
        services.AddScoped<IDepartmentService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            return new DepartmentService(context);
        });
        services.AddScoped<ICurrencyService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            var httpContextAccessor = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            return new CurrencyService(context, httpContextAccessor);
        });
        return services;
    }
}
