using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NEXUS.Modules.AIPlanning.ModuleRegistration;
using NEXUS.Modules.Approvals.ModuleRegistration;
using NEXUS.Modules.Audit.ModuleRegistration;
using NEXUS.Modules.CostManagement.ModuleRegistration;
using NEXUS.Modules.Documents.ModuleRegistration;
using NEXUS.Modules.Identity.ModuleRegistration;
using NEXUS.Modules.Integrations.ModuleRegistration;
using NEXUS.Modules.IssueManagement.ModuleRegistration;
using NEXUS.Modules.Meetings.ModuleRegistration;
using NEXUS.Modules.Notifications.ModuleRegistration;
using NEXUS.Modules.Organization.ModuleRegistration;
using NEXUS.Modules.PluginManagement.ModuleRegistration;
using NEXUS.Modules.Portfolio.ModuleRegistration;
using NEXUS.Modules.ProjectManagement.ModuleRegistration;
using NEXUS.Modules.ProjectPlanning.ModuleRegistration;
using NEXUS.Modules.Reporting.ModuleRegistration;
using NEXUS.Modules.ResourceManagement.ModuleRegistration;
using NEXUS.Modules.RiskManagement.ModuleRegistration;
using NEXUS.Modules.WorkItems.ModuleRegistration;
using NEXUS.Modules.WorkflowEngine.ModuleRegistration;
using NEXUS.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
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
    options.AddPolicy("Resource.Manage", policy => policy.RequireClaim("permission", "Resource.Manage"));
    options.AddPolicy("Resource.ViewCostRate", policy => policy.RequireClaim("permission", "Resource.ViewCostRate", "Resource.ManageRates", "Resource.Manage"));
    options.AddPolicy("Resource.EditCostRate", policy => policy.RequireClaim("permission", "Resource.EditCostRate", "Resource.ManageRates", "Resource.Manage"));
    options.AddPolicy("Users.Read", policy => policy.RequireClaim("permission", "users.read", "users.write"));
    options.AddPolicy("Users.Write", policy => policy.RequireClaim("permission", "users.write"));
    options.AddPolicy("Settings.Read", policy => policy.RequireClaim("permission", "settings.read", "settings.write"));
    options.AddPolicy("Settings.Write", policy => policy.RequireClaim("permission", "settings.write"));
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Database=NexusPmo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    options.UseSqlServer(connectionString);
});
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

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

var app = builder.Build();

AppDbContext.ConfigureTenantResolution(app.Services.GetRequiredService<IHttpContextAccessor>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();

    if (args.Contains("--seed-identity"))
    {
        await SeedDataProvider.SeedIdentityOnlyAsync(context);
        Console.WriteLine("Identity seed completed.");
        return;
    }

    await SeedDataProvider.SeedAsync(context);
}

app.UseRouting();
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
