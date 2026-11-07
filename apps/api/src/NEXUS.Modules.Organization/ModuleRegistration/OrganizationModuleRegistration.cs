using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Infrastructure;

namespace NEXUS.Modules.Organization.ModuleRegistration;

public static class OrganizationModuleRegistration
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services)
    {
        services.AddSingleton<ISettingDefinitionCatalog, SettingDefinitionCatalog>();
        services.AddSingleton<EffectiveConfigurationResolver>();
        services.AddSingleton<IFiscalYearCalendarService, FiscalYearCalendarService>();
        services.AddScoped<ISettingsRepository, EfSettingsRepository>();
        services.AddScoped<ITenantFiscalYearService, TenantFiscalYearService>();
        services.AddScoped<IConfigurationSettingsService, ConfigurationSettingsService>();
        services.AddScoped<IOrganizationSettingsAccessService, OrganizationSettingsAccessService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<NEXUS.SharedKernel.ITenantRegistryStatusValidator, TenantRegistryStatusValidator>();
        services.AddSingleton<IAuthorizationHandler, PlatformSettingsAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PlatformTenantProvisioningAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("PlatformSettings.Read", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new PlatformSettingsRequirement("read")));
            options.AddPolicy("PlatformSettings.Write", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new PlatformSettingsRequirement("write")));
            options.AddPolicy("PlatformTenants.Manage", policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new PlatformTenantProvisioningRequirement()));
        });

        services.AddScoped<IOrganizationService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            var httpContextAccessor = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var auditService = sp.GetRequiredService<IAuditService>();
            var organizationSettingsAccess = sp.GetRequiredService<IOrganizationSettingsAccessService>();
            return new OrganizationService(context, httpContextAccessor, auditService, organizationSettingsAccess);
        });
        services.AddScoped<IOrganizationalUnitService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            var httpContextAccessor = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var resourceLookup = sp.GetRequiredService<NEXUS.SharedKernel.IResourceReferenceLookup>();
            return new OrganizationalUnitService(context, httpContextAccessor, resourceLookup);
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
            var auditService = sp.GetRequiredService<NEXUS.Modules.Audit.Application.IAuditService>();
            return new CurrencyService(context, httpContextAccessor, auditService);
        });
        services.AddScoped<IWorkItemStatusReferenceService>(sp =>
        {
            var context = sp.GetRequiredService<DbContext>();
            var httpContextAccessor = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var usageLookup = sp.GetRequiredService<NEXUS.SharedKernel.IWorkItemStatusUsageLookup>();
            var auditService = sp.GetRequiredService<NEXUS.Modules.Audit.Application.IAuditService>();
            return new WorkItemStatusReferenceService(context, httpContextAccessor, usageLookup, auditService);
        });
        return services;
    }
}
