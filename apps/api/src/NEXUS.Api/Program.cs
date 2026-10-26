using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NEXUS.Modules.AIPlanning.ModuleRegistration;
using NEXUS.Modules.Approvals.ModuleRegistration;
using NEXUS.Modules.Audit.ModuleRegistration;
using NEXUS.Modules.CostManagement.ModuleRegistration;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.ModuleRegistration;
using NEXUS.Modules.Identity.ModuleRegistration;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Integrations.ModuleRegistration;
using NEXUS.Modules.IssueManagement.ModuleRegistration;
using NEXUS.Modules.Meetings.ModuleRegistration;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Notifications.ModuleRegistration;
using NEXUS.Modules.Organization.ModuleRegistration;
using NEXUS.Modules.PluginManagement.ModuleRegistration;
using NEXUS.Modules.Portfolio.ModuleRegistration;
using NEXUS.Modules.ProjectManagement.ModuleRegistration;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectPlanning.ModuleRegistration;
using NEXUS.Modules.Reporting.ModuleRegistration;
using NEXUS.Modules.ResourceManagement.ModuleRegistration;
using NEXUS.Modules.RiskManagement.ModuleRegistration;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.ModuleRegistration;
using NEXUS.Modules.WorkflowEngine.ModuleRegistration;
using NEXUS.Persistence;
using NEXUS.Persistence.Management;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using NEXUS.Api.Management;
using NEXUS.Api.ProgramManagement;
using NEXUS.Api.PortfolioManagement;
using NEXUS.Api.Governance;

var builder = WebApplication.CreateBuilder(args);
var seedOnly = args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase);
var skipDevelopmentDatabaseInitialization = args.Contains("--skip-development-database-initialization", StringComparer.OrdinalIgnoreCase);

if (seedOnly && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The --seed-only option is available only in the Development environment.");
}

if (skipDevelopmentDatabaseInitialization && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The --skip-development-database-initialization option is available only in the Development environment.");
}

if (skipDevelopmentDatabaseInitialization && (seedOnly || args.Contains("--seed-identity", StringComparer.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException("The database initialization skip option cannot be combined with a seed option.");
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NEXUS PMO API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "NEXUS-Demo-Secret-Key-Minimum-16-Chars!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "NEXUS",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "NEXUS-API",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Resource.View", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Resource.View") || context.User.HasClaim("permission", "Resource.Allocate") || context.User.HasClaim("permission", "Resource.Manage")));
    options.AddPolicy("Resource.Assign", policy => policy.RequireClaim("permission", "Resource.Allocate", "Resource.Manage"));
    options.AddPolicy("Resource.Create", policy => policy.RequireClaim("permission", "Resource.Create", "Resource.Manage"));
    options.AddPolicy("Resource.Edit", policy => policy.RequireClaim("permission", "Resource.Edit", "Resource.Manage"));
    options.AddPolicy("Resource.Delete", policy => policy.RequireClaim("permission", "Resource.Delete", "Resource.Manage"));
    options.AddPolicy("Resource.Manage", policy => policy.RequireClaim("permission", "Resource.Manage"));
    options.AddPolicy("Resource.ViewCostRate", policy => policy.RequireClaim("permission", "Resource.ViewCostRate", "Resource.EditCostRate", "Resource.ManageRates", "Resource.Manage"));
    options.AddPolicy("Resource.EditCostRate", policy => policy.RequireClaim("permission", "Resource.EditCostRate", "Resource.ManageRates", "Resource.Manage"));
    options.AddPolicy("Team.View", policy => policy.RequireClaim("permission", "Team.View", "Team.Manage"));
    options.AddPolicy("Team.Create", policy => policy.RequireClaim("permission", "Team.Create", "Team.Manage"));
    options.AddPolicy("Team.Edit", policy => policy.RequireClaim("permission", "Team.Edit", "Team.Manage"));
    options.AddPolicy("Team.Delete", policy => policy.RequireClaim("permission", "Team.Delete", "Team.Manage"));
    options.AddPolicy("Team.Manage", policy => policy.RequireClaim("permission", "Team.Manage"));
    options.AddPolicy("Users.Read", policy => policy.RequireClaim("permission", "users.read", "users.write"));
    options.AddPolicy("Users.Write", policy => policy.RequireClaim("permission", "users.write"));
    options.AddPolicy("Users.ResetPassword", policy => policy.RequireClaim("permission", "users.reset-password"));
    options.AddPolicy("Settings.Read", policy => policy.RequireClaim("permission", "settings.read", "settings.write"));
    options.AddPolicy("Settings.Write", policy => policy.RequireClaim("permission", "settings.write"));
    options.AddPolicy("Directory.View", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Directory.View")
        || context.User.HasClaim("permission", "Directory.Manage")
        || context.User.HasClaim("permission", "Directory.TestConnection")
        || context.User.HasClaim("permission", "Directory.PreviewSync")
        || context.User.HasClaim("permission", "Directory.ExecuteSync")
        || context.User.HasClaim("permission", "Directory.ManageMappings")
        || context.User.HasClaim("permission", "Directory.ManageProvisioningPolicy")));
    options.AddPolicy("Directory.Manage", policy => policy.RequireClaim("permission", "Directory.Manage"));
    options.AddPolicy("Directory.TestConnection", policy => policy.RequireClaim("permission", "Directory.TestConnection", "Directory.Manage"));
    options.AddPolicy("Directory.PreviewSync", policy => policy.RequireClaim("permission", "Directory.PreviewSync", "Directory.Manage"));
    options.AddPolicy("Directory.GroupCatalog", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Directory.PreviewSync")
        || context.User.HasClaim("permission", "Directory.ManageMappings")
        || context.User.HasClaim("permission", "Directory.Manage")));
    options.AddPolicy("Directory.ExecuteSync", policy => policy.RequireClaim("permission", "Directory.ExecuteSync", "Directory.Manage"));
    options.AddPolicy("Directory.ManageMappings", policy => policy.RequireClaim("permission", "Directory.ManageMappings", "Directory.Manage"));
    options.AddPolicy("Directory.ManageProvisioningPolicy", policy => policy.RequireClaim("permission", "Directory.ManageProvisioningPolicy", "Directory.Manage"));
    options.AddPolicy("Finances.Read", policy => policy.RequireClaim("permission", "finances.read", "finances.write"));
    options.AddPolicy("Finances.Write", policy => policy.RequireClaim("permission", "finances.write"));

    options.AddPolicy("Project.View", policy => policy.RequireClaim("permission", "Project.View"));
    options.AddPolicy("Project.Create", policy => policy.RequireClaim("permission", "Project.Create"));
    options.AddPolicy("Project.Edit", policy => policy.RequireClaim("permission", "Project.Edit"));
    options.AddPolicy("Project.Delete", policy => policy.RequireClaim("permission", "Project.Delete"));
    options.AddPolicy("StatusReport.View", policy => policy.RequireClaim("permission", "StatusReport.View"));
    options.AddPolicy("StatusReport.Create", policy => policy.RequireClaim("permission", "StatusReport.Create"));
    options.AddPolicy("StatusReport.Edit", policy => policy.RequireClaim("permission", "StatusReport.Edit"));
    options.AddPolicy("StatusReport.Generate", policy => policy.RequireClaim("permission", "StatusReport.Generate"));
    options.AddPolicy("StatusReport.Submit", policy => policy.RequireClaim("permission", "StatusReport.Submit"));
    options.AddPolicy("StatusReport.Approve", policy => policy.RequireClaim("permission", "StatusReport.Approve"));
    options.AddPolicy("StatusReport.Publish", policy => policy.RequireClaim("permission", "StatusReport.Publish"));
    options.AddPolicy("StatusReport.Export", policy => policy.RequireClaim("permission", "StatusReport.Export"));
    options.AddPolicy("StatusReport.ManageConfiguration", policy => policy.RequireClaim("permission", "StatusReport.ManageConfiguration"));
    options.AddPolicy("Stakeholders.Read", policy => policy.RequireClaim("permission", "Stakeholders.Read"));
    options.AddPolicy("Stakeholders.Manage", policy => policy.RequireClaim("permission", "Stakeholders.Manage"));
    options.AddPolicy("ChangeRequest.View", policy => policy.RequireClaim("permission", "ChangeRequest.View"));
    options.AddPolicy("ChangeRequest.Manage", policy => policy.RequireClaim("permission", "ChangeRequest.Manage"));
    options.AddPolicy("ChangeRequest.Approve", policy => policy.RequireClaim("permission", "ChangeRequest.Approve"));

    options.AddPolicy("Handover.View", policy => policy.RequireClaim("permission", "Handover.View"));
    options.AddPolicy("Handover.Manage", policy => policy.RequireClaim("permission", "Handover.Manage"));
    options.AddPolicy("Handover.Approve", policy => policy.RequireClaim("permission", "Handover.Approve"));
    options.AddPolicy("Handover.AdminInitiate", policy => policy.RequireClaim("permission", "Handover.AdminInitiate"));
    options.AddPolicy("Handover.Initiate", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Handover.Manage")
        || context.User.HasClaim("permission", "Handover.AdminInitiate")));
    options.AddPolicy("Handover.Access", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Handover.View")
        || context.User.HasClaim("permission", "Handover.Manage")
        || context.User.HasClaim("permission", "Handover.AdminInitiate")));
    options.AddPolicy("Handover.Capabilities", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Handover.View")
        || context.User.HasClaim("permission", "Handover.Manage")
        || context.User.HasClaim("permission", "Handover.AdminInitiate")
        || context.User.HasClaim("permission", "Handover.Approve")));

    options.AddPolicy("Risk.View", policy => policy.RequireClaim("permission", "Risk.View"));
    options.AddPolicy("Risk.Create", policy => policy.RequireClaim("permission", "Risk.Create"));
    options.AddPolicy("Risk.Edit", policy => policy.RequireClaim("permission", "Risk.Edit"));
    options.AddPolicy("Risk.Delete", policy => policy.RequireClaim("permission", "Risk.Delete"));
    options.AddPolicy("Risk.Mitigate", policy => policy.RequireClaim("permission", "Risk.Mitigate"));

    options.AddPolicy("Planning.View", policy => policy.RequireClaim("permission", "Planning.View"));
    options.AddPolicy("Planning.Manage", policy => policy.RequireClaim("permission", "Planning.Manage"));
    options.AddPolicy("Planning.Lifecycle", policy => policy.RequireClaim("permission", "Planning.Lifecycle"));

    options.AddPolicy("Portfolio.View", policy => policy.RequireClaim("permission", "Portfolio.View"));
    options.AddPolicy("Portfolio.Manage", policy => policy.RequireClaim("permission", "Portfolio.Manage"));
    options.AddPolicy("Program.View", policy => policy.RequireClaim("permission", "Program.View"));
    options.AddPolicy("Program.Manage", policy => policy.RequireClaim("permission", "Program.Manage"));
    options.AddPolicy("WorkItem.View", policy => policy.RequireClaim("permission", "WorkItem.View"));
    options.AddPolicy("WorkItem.Create", policy => policy.RequireClaim("permission", "WorkItem.Create"));
    options.AddPolicy("WorkItem.Edit", policy => policy.RequireClaim("permission", "WorkItem.Edit"));
    options.AddPolicy("WorkItem.Delete", policy => policy.RequireClaim("permission", "WorkItem.Delete"));
    options.AddPolicy("TimeTracking.View", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", TimeTrackingPermissions.ViewOwn)
        || context.User.HasClaim("permission", TimeTrackingPermissions.ManageOwn)
        || context.User.HasClaim("permission", TimeTrackingPermissions.ViewTeam)
        || context.User.HasClaim("permission", TimeTrackingPermissions.ManageTeam)));
    options.AddPolicy("TimeTracking.Manage", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", TimeTrackingPermissions.ManageOwn)
        || context.User.HasClaim("permission", TimeTrackingPermissions.ManageTeam)));
    options.AddPolicy(MeetingPermissions.View, policy => policy.RequireClaim("permission", MeetingPermissions.View));
    options.AddPolicy(MeetingPermissions.Create, policy => policy.RequireClaim("permission", MeetingPermissions.Create));
    options.AddPolicy(MeetingPermissions.Edit, policy => policy.RequireClaim("permission", MeetingPermissions.Edit));
    options.AddPolicy(MeetingPermissions.Manage, policy => policy.RequireClaim("permission", MeetingPermissions.Manage));
    options.AddPolicy(MeetingPermissions.Delete, policy => policy.RequireClaim("permission", MeetingPermissions.Delete));
    options.AddPolicy(DocumentPermissions.View, policy => policy.RequireClaim("permission", DocumentPermissions.View));
    options.AddPolicy(DocumentPermissions.Create, policy => policy.RequireClaim("permission", DocumentPermissions.Create));
    options.AddPolicy(DocumentPermissions.Edit, policy => policy.RequireClaim("permission", DocumentPermissions.Edit));
    options.AddPolicy(DocumentPermissions.Archive, policy => policy.RequireClaim("permission", DocumentPermissions.Archive));
    options.AddPolicy(DocumentPermissions.TemplateManage, policy => policy.RequireClaim("permission", DocumentPermissions.TemplateManage));
    options.AddPolicy("WorkItemStatus.View", policy => policy.RequireClaim("permission", "WorkItem.View", "settings.read", "settings.write"));
    options.AddPolicy("WorkItemStatus.Manage", policy => policy.RequireClaim("permission", "settings.write"));
    options.AddPolicy("VendorCategory.View", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", "Resource.View") || context.User.HasClaim("permission", "Resource.Allocate") || context.User.HasClaim("permission", "Resource.Manage") || context.User.HasClaim("permission", "settings.read") || context.User.HasClaim("permission", "settings.write")));
    options.AddPolicy("VendorCategory.Manage", policy => policy.RequireClaim("permission", "settings.write"));
    options.AddPolicy("Workflow.View", policy => policy.RequireClaim("permission", "Workflow.View"));
    options.AddPolicy("Workflow.Manage", policy => policy.RequireClaim("permission", "Workflow.Manage"));
    options.AddPolicy("Workflow.Publish", policy => policy.RequireClaim("permission", "Workflow.Publish"));
    options.AddPolicy(AssignmentRecoveryPermissions.View, policy => policy.RequireClaim("permission", AssignmentRecoveryPermissions.View));
    options.AddPolicy(AssignmentRecoveryPermissions.Initiate, policy => policy.RequireClaim("permission", AssignmentRecoveryPermissions.Initiate));
    options.AddPolicy(AssignmentRecoveryPermissions.Manage, policy => policy.RequireClaim("permission", AssignmentRecoveryPermissions.Manage));
    options.AddPolicy("AssignmentRecovery.Act", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", AssignmentRecoveryPermissions.Initiate)
        || context.User.HasClaim("permission", AssignmentRecoveryPermissions.Manage)));
    options.AddPolicy("AssignmentRecovery.Discover", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", AssignmentRecoveryPermissions.View)
        || context.User.HasClaim("permission", AssignmentRecoveryPermissions.Initiate)
        || context.User.HasClaim("permission", AssignmentRecoveryPermissions.Manage)));
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Database=NexusPmo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    options.UseSqlServer(connectionString);
});
builder.Services.Configure<TimeTrackingOptions>(builder.Configuration.GetSection("TimeTracking"));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IWorkItemCommentAccess, WorkItemCommentAccess>();
builder.Services.AddScoped<IWorkItemCommentAuthorLookup, WorkItemCommentAuthorLookup>();
builder.Services.AddScoped<IWorkItemAssigneeLookup, WorkItemAssigneeLookup>();
builder.Services.AddScoped<IWorkItemStatusReferenceLookup, WorkItemStatusReferenceLookup>();
builder.Services.AddScoped<IWorkItemStatusUsageLookup, WorkItemStatusUsageLookup>();
builder.Services.Configure<WorkItemAttachmentOptions>(builder.Configuration.GetSection("WorkItemAttachments"));
builder.Services.AddScoped<IWorkItemAttachmentStorage, WorkItemAttachmentStorage>();
builder.Services.AddScoped<IMeetingMinutesAttachmentStorage, MeetingMinutesAttachmentStorage>();
builder.Services.AddScoped<IMeetingMinutesAttachmentPolicy, MeetingMinutesAttachmentPolicy>();
// An isolated Development host used to verify an existing database can serve requests
// without running migrations, seeders, or unrelated background writes on startup.
if (!skipDevelopmentDatabaseInitialization)
    builder.Services.AddHostedService<MeetingReminderScheduler>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

if (builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "CORS is not configured. Add 'Cors:AllowedOrigins' (e.g. http://localhost:4000) to appsettings.Development.json.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IManagementContextAccessor, ManagementContextAccessor>();
builder.Services.AddScoped<IManagementScopeResolver, ManagementScopeResolver>();
builder.Services.AddScoped<IProgramManagementService, ProgramManagementService>();
builder.Services.AddScoped<IPortfolioManagementService, PortfolioManagementService>();
builder.Services.AddScoped<AssignmentRecoveryService>();

builder.Services.AddIdentityModule();
builder.Services.AddOrganizationModule();
builder.Services.AddPortfolioModule();
builder.Services.AddProjectManagementModule();
builder.Services.AddWorkItemsModule();
builder.Services.AddResourceManagementModule();
builder.Services.AddCostManagementModule();
builder.Services.AddRiskManagementModule();
builder.Services.AddIssueManagementModule();
builder.Services.AddMeetingsModule();
builder.Services.AddWorkflowEngineModule();
builder.Services.AddApprovalsModule();
builder.Services.AddNotificationsModule();
builder.Services.AddDocumentsModule();
builder.Services.AddReportingModule();
builder.Services.AddAuditModule();
builder.Services.AddIntegrationsModule();
builder.Services.AddPluginManagementModule();
builder.Services.AddAIPlanningModule();
builder.Services.AddProjectPlanningModule();

if (skipDevelopmentDatabaseInitialization)
{
    // Keep isolated host validation side-effect free: do not reuse local key-ring files
    // and do not start database-backed background workers.
    var isolatedDataProtectionPath = Path.Combine(AppContext.BaseDirectory, ".isolated-data-protection");
    Directory.CreateDirectory(isolatedDataProtectionPath);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(isolatedDataProtectionPath));
    foreach (var hostedService in builder.Services.Where(service => service.ServiceType == typeof(IHostedService)).ToArray())
        builder.Services.Remove(hostedService);
}

var app = builder.Build();

AppDbContext.ConfigureTenantResolution(app.Services.GetRequiredService<IHttpContextAccessor>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    if (skipDevelopmentDatabaseInitialization)
    {
        app.Logger.LogWarning("Development database migrations, seeding, and workflow provisioning were skipped by command-line option.");
    }
    else
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        if (args.Contains("--seed-identity", StringComparer.OrdinalIgnoreCase))
        {
            await SeedDataProvider.SeedIdentityOnlyAsync(context);
            return;
        }

        await SeedDataProvider.SeedAsync(context);

        var workItemWorkflowProvisioning = scope.ServiceProvider.GetRequiredService<IWorkItemWorkflowProvisioningService>();
        var workflowProvisioning = await workItemWorkflowProvisioning.EnsureCurrentTenantAsync("System", CancellationToken.None);
        if (workflowProvisioning.IsFailure)
            throw new InvalidOperationException(workflowProvisioning.Error?.Message ?? "Work Item workflow provisioning failed.");
        var workflowOnboarding = await workItemWorkflowProvisioning.OnboardCurrentTenantAsync("System", CancellationToken.None);
        if (workflowOnboarding.IsSuccess && workflowOnboarding.Value!.Exceptions.Count > 0)
            app.Logger.LogWarning("Work Item workflow onboarding completed with {Count} exceptions.", workflowOnboarding.Value.Exceptions.Count);

        if (seedOnly)
        {
            app.Logger.LogInformation("Database migration and development seeding completed; exiting before opening an HTTP listener.");
            return;
        }
    }
}

app.UseRouting();
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseMiddleware<InitialPasswordEnforcementMiddleware>();
app.UseMiddleware<UserPermissionClaimsMiddleware>();
app.UseMiddleware<ProjectPublicIdRoutingMiddleware>();
app.UseMiddleware<ManagementContextMiddleware>();
app.UseMiddleware<ProjectResourceScopeMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
