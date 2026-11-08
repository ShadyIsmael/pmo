using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NEXUS.Api.Security;
using NEXUS.Modules.AIPlanning.ModuleRegistration;
using NEXUS.Modules.Approvals.ModuleRegistration;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.ModuleRegistration;
using NEXUS.Modules.CostManagement.ModuleRegistration;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.ModuleRegistration;
using NEXUS.Modules.Identity.ModuleRegistration;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Integrations.ModuleRegistration;
using NEXUS.Modules.Integrations.Infrastructure;
using NEXUS.Modules.IssueManagement.ModuleRegistration;
using NEXUS.Modules.Meetings.ModuleRegistration;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Notifications.ModuleRegistration;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Infrastructure;
using NEXUS.Modules.Organization.ModuleRegistration;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.PluginManagement.ModuleRegistration;
using NEXUS.Modules.Portfolio.ModuleRegistration;
using NEXUS.Modules.ProjectManagement.ModuleRegistration;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectPlanning.ModuleRegistration;
using NEXUS.Modules.ProjectPlanning.MppAdapter.ModuleRegistration;
using NEXUS.Modules.Reporting.ModuleRegistration;
using NEXUS.Modules.ResourceManagement.ModuleRegistration;
using NEXUS.Modules.RiskManagement.ModuleRegistration;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.ModuleRegistration;
using NEXUS.Modules.WorkflowEngine.ModuleRegistration;
using NEXUS.Persistence;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Persistence.Audit;
using NEXUS.Persistence.Lookups;
using NEXUS.Persistence.Management;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using NEXUS.SharedKernel.Security;
using NEXUS.Api.Management;
using NEXUS.Api.ProgramManagement;
using NEXUS.Api.PortfolioManagement;
using NEXUS.Api.Governance;
using NEXUS.Api.Notifications;
using NEXUS.Api.Finance;
using NEXUS.Api.Account;
using NEXUS.Api.Development;
using NEXUS.Api.Middleware;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

if (args.Contains("--supports-seed-minimal-only", StringComparer.OrdinalIgnoreCase))
{
    var aspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    var dotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    if (args.Length != 1
        || !string.Equals(aspNetEnvironment, "Development", StringComparison.OrdinalIgnoreCase)
        || (dotNetEnvironment is not null && !string.Equals(dotNetEnvironment, "Development", StringComparison.OrdinalIgnoreCase)))
    {
        Environment.ExitCode = 2;
        return;
    }

    Console.WriteLine("NEXUS_MINIMAL_SEED_MODE=1");
    return;
}

var builder = WebApplication.CreateBuilder(args);
var seedOnly = args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase);
var seedIdentity = args.Contains("--seed-identity", StringComparer.OrdinalIgnoreCase);
var seedMinimalOnly = args.Contains("--seed-minimal-only", StringComparer.OrdinalIgnoreCase);
var bootstrapPermissionDelegation = args.Contains("--bootstrap-permission-delegation", StringComparer.OrdinalIgnoreCase);
var rebuildCanonicalPermissions = args.Contains("--rebuild-canonical-permissions", StringComparer.OrdinalIgnoreCase);
var recoverDevelopmentAdminPassword = args.Contains("--recover-development-admin-password", StringComparer.OrdinalIgnoreCase);
var permissionRebuildExpectedServer = GetOptionValue(args, "--expected-sql-server");
var permissionRebuildExpectedDatabase = GetOptionValue(args, "--expected-sql-database");
var delegationBootstrapExpectedServer = GetOptionValue(args, "--delegation-expected-sql-server");
var delegationBootstrapExpectedDatabase = GetOptionValue(args, "--delegation-expected-sql-database");
var permissionRebuildBackupDirectory = GetOptionValue(args, "--permission-rebuild-backup-directory");
var adminRecoveryExpectedServer = GetOptionValue(args, "--admin-recovery-expected-sql-server");
var adminRecoveryExpectedDatabase = GetOptionValue(args, "--admin-recovery-expected-sql-database");
var adminRecoveryBackupDirectory = GetOptionValue(args, "--admin-recovery-backup-directory");
var adminRecoveryConnectionString = Environment.GetEnvironmentVariable("NEXUS_EF_DEVELOPMENT_CONNECTION_STRING");
var explicitlySkipDevelopmentDatabaseInitialization = args.Contains("--skip-development-database-initialization", StringComparer.OrdinalIgnoreCase);
var skipDevelopmentDatabaseInitialization =
    explicitlySkipDevelopmentDatabaseInitialization
    || (builder.Environment.IsDevelopment() && !seedOnly && !seedIdentity && !seedMinimalOnly && !bootstrapPermissionDelegation && !rebuildCanonicalPermissions && !recoverDevelopmentAdminPassword);

if ((seedOnly || seedIdentity || seedMinimalOnly || bootstrapPermissionDelegation || rebuildCanonicalPermissions || recoverDevelopmentAdminPassword) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Development seed, permission delegation bootstrap, permission reconciliation, and Admin recovery options are available only in the Development environment.");
}

if (skipDevelopmentDatabaseInitialization && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The --skip-development-database-initialization option is available only in the Development environment.");
}

if (explicitlySkipDevelopmentDatabaseInitialization && (seedOnly || seedIdentity || seedMinimalOnly || bootstrapPermissionDelegation || rebuildCanonicalPermissions || recoverDevelopmentAdminPassword))
{
    throw new InvalidOperationException("The database initialization skip option cannot be combined with a seed, permission delegation bootstrap, permission reconciliation, or Admin recovery option.");
}

if (new[] { seedOnly, seedIdentity, seedMinimalOnly, bootstrapPermissionDelegation, rebuildCanonicalPermissions, recoverDevelopmentAdminPassword }.Count(option => option) > 1)
{
    throw new InvalidOperationException("Choose only one Development database operation: seed-only, seed-identity, seed-minimal-only, bootstrap-permission-delegation, rebuild-canonical-permissions, or recover-development-admin-password.");
}

var hasDelegationBootstrapArguments = delegationBootstrapExpectedServer is not null || delegationBootstrapExpectedDatabase is not null;
if (bootstrapPermissionDelegation
    && (string.IsNullOrWhiteSpace(delegationBootstrapExpectedServer) || string.IsNullOrWhiteSpace(delegationBootstrapExpectedDatabase)))
{
    throw new InvalidOperationException("--bootstrap-permission-delegation requires --delegation-expected-sql-server and --delegation-expected-sql-database.");
}
if (!bootstrapPermissionDelegation && hasDelegationBootstrapArguments)
    throw new InvalidOperationException("Delegation bootstrap target options require --bootstrap-permission-delegation.");

var hasPermissionRebuildArguments = permissionRebuildExpectedServer is not null
    || permissionRebuildExpectedDatabase is not null
    || permissionRebuildBackupDirectory is not null;
if (rebuildCanonicalPermissions
    && (string.IsNullOrWhiteSpace(permissionRebuildExpectedServer)
        || string.IsNullOrWhiteSpace(permissionRebuildExpectedDatabase)
        || string.IsNullOrWhiteSpace(permissionRebuildBackupDirectory)))
{
    throw new InvalidOperationException("--rebuild-canonical-permissions requires --expected-sql-server, --expected-sql-database, and --permission-rebuild-backup-directory.");
}
if (!rebuildCanonicalPermissions && hasPermissionRebuildArguments)
    throw new InvalidOperationException("Permission rebuild target and backup options require --rebuild-canonical-permissions.");

var hasAdminRecoveryArguments = adminRecoveryExpectedServer is not null
    || adminRecoveryExpectedDatabase is not null
    || adminRecoveryBackupDirectory is not null;
if (recoverDevelopmentAdminPassword
    && (string.IsNullOrWhiteSpace(adminRecoveryExpectedServer)
        || string.IsNullOrWhiteSpace(adminRecoveryExpectedDatabase)
        || string.IsNullOrWhiteSpace(adminRecoveryBackupDirectory)
        || string.IsNullOrWhiteSpace(adminRecoveryConnectionString)))
{
    throw new InvalidOperationException("Admin recovery requires NEXUS_EF_DEVELOPMENT_CONNECTION_STRING, --admin-recovery-expected-sql-server, --admin-recovery-expected-sql-database, and --admin-recovery-backup-directory.");
}
if (!recoverDevelopmentAdminPassword && hasAdminRecoveryArguments)
    throw new InvalidOperationException("Admin recovery target and backup options require --recover-development-admin-password.");
if (recoverDevelopmentAdminPassword)
    builder.Configuration["ConnectionStrings:DefaultConnection"] = adminRecoveryConnectionString;

builder.Services.AddControllers(options => options.Filters.AddService<ApiErrorResponseFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The supplied value is invalid."
                            : error.ErrorMessage)
                        .ToArray(),
                    StringComparer.Ordinal);
            return new BadRequestObjectResult(ApiProblemDetails.Create(
                StatusCodes.Status400BadRequest,
                context.HttpContext.Request.Path,
                context.HttpContext.TraceIdentifier,
                errors: errors))
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });
builder.Services.AddBrowserCsrfProtection();
builder.Services.AddScoped<ApiErrorResponseFilter>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var statusCode = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Extensions.TryAdd("code", ApiProblemDetails.CodeForStatus(statusCode));
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
        if (string.IsNullOrWhiteSpace(context.ProblemDetails.Detail))
            context.ProblemDetails.Detail = ApiProblemDetails.Create(statusCode, null, null).Detail;
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

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

// Security: no fallback signing key is compiled into the application. Development may run with
// an ephemeral process-local key so no secret is ever committed; every other environment must
// supply Jwt:Secret through a secret store and the host fails fast when it is missing.
if (string.IsNullOrWhiteSpace(builder.Configuration[JwtSigningKey.ConfigurationKey]))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:Secret must be configured outside the Development environment. " +
            "Supply it through a secret store (for example the Jwt__Secret environment variable).");
    }

    builder.Configuration[JwtSigningKey.ConfigurationKey] = JwtSigningKey.CreateEphemeral();
}

var jwtSecret = JwtSigningKey.Require(builder.Configuration[JwtSigningKey.ConfigurationKey]);
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
    options.FallbackPolicy = ApiAuthorizationDefaults.FallbackPolicy;
    options.AddPolicy("Issue.View", policy => policy.RequireClaim("permission", "Issue.View"));
    options.AddPolicy("Plugin.View", policy => policy.RequireClaim("permission", "Plugin.View"));
    options.AddPolicy("Resource.View", policy => policy.RequireClaim("permission", "Resource.View"));
    options.AddPolicy("Resource.Assign", policy => policy.RequireClaim("permission", "Resource.Assign"));
    options.AddPolicy("Resource.Create", policy => policy.RequireClaim("permission", "Resource.Create"));
    options.AddPolicy("Resource.Edit", policy => policy.RequireClaim("permission", "Resource.Edit"));
    options.AddPolicy("Resource.Delete", policy => policy.RequireClaim("permission", "Resource.Delete"));
    options.AddPolicy("Resource.ViewCostRate", policy => policy.RequireClaim("permission", "Resource.ViewCostRate"));
    options.AddPolicy("Resource.EditCostRate", policy => policy.RequireClaim("permission", "Resource.EditCostRate"));
    options.AddPolicy("Team.View", policy => policy.RequireClaim("permission", "Team.View"));
    options.AddPolicy("Team.Create", policy => policy.RequireClaim("permission", "Team.Create"));
    options.AddPolicy("Team.Edit", policy => policy.RequireClaim("permission", "Team.Edit"));
    options.AddPolicy("Team.Delete", policy => policy.RequireClaim("permission", "Team.Delete"));
    options.AddPolicy("Users.View", policy => policy.RequireClaim("permission", "Users.View"));
    options.AddPolicy("Users.ManageAccounts", policy => policy.RequireClaim("permission", "Users.ManageAccounts"));
    options.AddPolicy("Users.AssignRoles", policy => policy.RequireClaim("permission", "Users.AssignRoles"));
    options.AddPolicy("Users.SetStatus", policy => policy.RequireClaim("permission", "Users.SetStatus"));
    options.AddPolicy("UserPermissions.Manage", policy => policy.RequireClaim("permission", "UserPermissions.Manage"));
    options.AddPolicy("Roles.View", policy => policy.RequireClaim("permission", "Roles.View"));
    options.AddPolicy("Roles.Create", policy => policy.RequireClaim("permission", "Roles.Create"));
    options.AddPolicy("Roles.EditDetails", policy => policy.RequireClaim("permission", "Roles.EditDetails"));
    options.AddPolicy("Roles.ConfigurePermissions", policy => policy.RequireClaim("permission", "Roles.ConfigurePermissions"));
    options.AddPolicy("Roles.Delete", policy => policy.RequireClaim("permission", "Roles.Delete"));
    options.AddPolicy("Users.ResetPassword", policy => policy.RequireClaim("permission", "Users.ResetPassword"));
    options.AddPolicy("Settings.View", policy => policy.RequireClaim("permission", "Settings.View"));
    options.AddPolicy("Settings.ManageTenantConfiguration", policy => policy.RequireClaim("permission", "Settings.ManageTenantConfiguration"));
    options.AddPolicy("EmailNotifications.View", policy => policy.RequireClaim("permission", "EmailNotifications.View"));
    options.AddPolicy("EmailNotifications.Manage", policy => policy.RequireClaim("permission", "EmailNotifications.Manage"));
    options.AddPolicy("EmailNotifications.Test", policy => policy.RequireClaim("permission", "EmailNotifications.Test"));
    options.AddPolicy("EmailNotifications.Logs", policy => policy.RequireClaim("permission", "EmailNotifications.Logs"));
    options.AddPolicy("ReferenceData.View", policy => policy.RequireClaim("permission", "ReferenceData.View"));
    options.AddPolicy("ReferenceData.Create", policy => policy.RequireClaim("permission", "ReferenceData.Create"));
    options.AddPolicy("ReferenceData.Edit", policy => policy.RequireClaim("permission", "ReferenceData.Edit"));
    options.AddPolicy("ReferenceData.Delete", policy => policy.RequireClaim("permission", "ReferenceData.Delete"));
    options.AddWorkflowLookupPolicies();
    options.AddPolicy("Directory.View", policy => policy.RequireClaim("permission", "Directory.View"));
    options.AddPolicy("Directory.ManageConnection", policy => policy.RequireClaim("permission", "Directory.ManageConnection"));
    options.AddPolicy("Directory.TestConnection", policy => policy.RequireClaim("permission", "Directory.TestConnection"));
    options.AddPolicy("Directory.PreviewSync", policy => policy.RequireClaim("permission", "Directory.PreviewSync"));
    options.AddPolicy("Directory.ExecuteSync", policy => policy.RequireClaim("permission", "Directory.ExecuteSync"));
    options.AddPolicy("Directory.ManageMappings", policy => policy.RequireClaim("permission", "Directory.ManageMappings"));
    options.AddPolicy("Directory.ManageProvisioningPolicy", policy => policy.RequireClaim("permission", "Directory.ManageProvisioningPolicy"));
    options.AddPolicy("ProjectFinance.View", policy => policy.RequireClaim("permission", "ProjectFinance.View"));
    options.AddPolicy("Budget.View", policy => policy.RequireClaim("permission", "Budget.View"));
    options.AddPolicy("Budget.Create", policy => policy.RequireClaim("permission", "Budget.Create"));
    options.AddPolicy("Budget.Edit", policy => policy.RequireClaim("permission", "Budget.Edit"));
    options.AddPolicy("Budget.Delete", policy => policy.RequireClaim("permission", "Budget.Delete"));
    options.AddPolicy("Cost.View", policy => policy.RequireClaim("permission", "Cost.View"));
    options.AddPolicy("Cost.Create", policy => policy.RequireClaim("permission", "Cost.Create"));
    options.AddPolicy("Cost.Edit", policy => policy.RequireClaim("permission", "Cost.Edit"));
    options.AddPolicy("Cost.Delete", policy => policy.RequireClaim("permission", "Cost.Delete"));
    options.AddPolicy("Commitment.View", policy => policy.RequireClaim("permission", "Commitment.View"));
    options.AddPolicy("Commitment.Create", policy => policy.RequireClaim("permission", "Commitment.Create"));
    options.AddPolicy("Commitment.Edit", policy => policy.RequireClaim("permission", "Commitment.Edit"));
    options.AddPolicy("Commitment.Close", policy => policy.RequireClaim("permission", "Commitment.Close"));
    options.AddPolicy("Commitment.Cancel", policy => policy.RequireClaim("permission", "Commitment.Cancel"));
    options.AddPolicy("Commitment.Delete", policy => policy.RequireClaim("permission", "Commitment.Delete"));
    options.AddPolicy("PaymentObligation.View", policy => policy.RequireClaim("permission", "PaymentObligation.View"));
    options.AddPolicy("PaymentObligation.Create", policy => policy.RequireClaim("permission", "PaymentObligation.Create"));
    options.AddPolicy("PaymentObligation.Edit", policy => policy.RequireClaim("permission", "PaymentObligation.Edit"));
    options.AddPolicy("PaymentObligation.Delete", policy => policy.RequireClaim("permission", "PaymentObligation.Delete"));
    options.AddPolicy("PaymentObligationSettlement.Record", policy => policy.RequireClaim("permission", "PaymentObligationSettlement.Record"));
    options.AddPolicy("ProjectSettlement.View", policy => policy.RequireClaim("permission", "ProjectSettlement.View"));
    options.AddPolicy("ProjectFinancialAuditTrail.View", policy => policy.RequireClaim("permission", "ProjectFinancialAuditTrail.View"));
    options.AddPolicy("BudgetRevision.View", policy => policy.RequireClaim("permission", "BudgetRevision.View"));
    options.AddPolicy("BudgetRevision.Create", policy => policy.RequireClaim("permission", "BudgetRevision.Create"));
    options.AddPolicy("BudgetRevision.EditDraft", policy => policy.RequireClaim("permission", "BudgetRevision.EditDraft"));
    options.AddPolicy("BudgetRevision.SubmitForReview", policy => policy.RequireClaim("permission", "BudgetRevision.SubmitForReview"));
    options.AddPolicy("BudgetRevision.Approve", policy => policy.RequireClaim("permission", "BudgetRevision.Approve"));
    options.AddPolicy("BudgetRevision.Cancel", policy => policy.RequireClaim("permission", "BudgetRevision.Cancel"));
    options.AddPolicy("BudgetRevision.Delete", policy => policy.RequireClaim("permission", "BudgetRevision.Delete"));
    options.AddPolicy("ProjectAnnualBudget.View", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.View"));
    options.AddPolicy("ProjectFinancialFoundation.View", policy => policy.RequireClaim("permission", "ProjectFinancialFoundation.View"));
    options.AddPolicy("ProjectAnnualBudget.Create", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.Create"));
    options.AddPolicy("ProjectAnnualBudget.EditDraft", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.EditDraft"));
    options.AddPolicy("ProjectAnnualBudget.SubmitForReview", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.SubmitForReview"));
    options.AddPolicy("ProjectAnnualBudget.Approve", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.Approve"));
    options.AddPolicy("ProjectAnnualBudget.ReviseApproved", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.ReviseApproved"));
    options.AddPolicy("ProjectAnnualBudget.ManageApprovalConfiguration", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.ManageApprovalConfiguration"));
    options.AddPolicy("ProjectAnnualBudgetClosing.View", policy => policy.RequireClaim("permission", "ProjectAnnualBudgetClosing.View"));
    options.AddPolicy("ProjectAnnualBudget.Close", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.Close"));
    options.AddPolicy("ProjectAnnualBudget.CloseApprove", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.CloseApprove"));
    options.AddPolicy("ProjectAnnualBudget.CarryForward", policy => policy.RequireClaim("permission", "ProjectAnnualBudget.CarryForward"));
    options.AddPolicy("ProjectPaymentRequest.View", policy => policy.RequireClaim("permission", "ProjectPaymentRequest.View"));
    options.AddPolicy("ProjectPaymentRequest.ViewOrAssignedReviewer", policy => policy.RequireClaim("permission", "ProjectPaymentRequest.View", "ProjectPaymentRequest.Approve"));
    options.AddPolicy("ProjectPaymentRequest.Create", policy => policy.RequireClaim("permission", "ProjectPaymentRequest.Create"));
    options.AddPolicy("ProjectPaymentRequest.EditDraft", policy => policy.RequireClaim("permission", "ProjectPaymentRequest.EditDraft"));
    options.AddPolicy("ProjectPaymentRequest.Submit", policy => policy.RequireClaim("permission", "ProjectPaymentRequest.Submit"));
    options.AddPolicy("ProjectPaymentRequest.Cancel", policy => policy.RequireClaim("permission", "ProjectPaymentRequest.Cancel"));
    options.AddCanonicalFinancialActionPolicies();
    options.AddPolicy("ProjectPaymentReversal.Create", policy => policy.RequireClaim("permission", "ProjectPaymentReversal.Create"));
    options.AddPolicy("ProjectPaymentReversal.Approve", policy => policy.RequireClaim("permission", "ProjectPaymentReversal.Approve"));
    options.AddPolicy("ProjectFinancialCeiling.ApproveChange", policy => policy.RequireClaim("permission", "ProjectFinancialCeiling.ApproveChange"));
    options.AddPolicy("Report.View", policy => policy.RequireClaim("permission", "Report.View"));
    options.AddPolicy("Dashboard.View", policy => policy.RequireClaim("permission", "Dashboard.View"));
    options.AddPolicy("TenantAdministration.ManageScope", policy => policy.RequireClaim("permission", "TenantAdministration.ManageScope"));
    options.AddPolicy("Audit.View", policy => policy.RequireClaim("permission", "Audit.View"));

    options.AddPolicy("Project.View", policy => policy.RequireClaim("permission", "Project.View"));
    options.AddPolicy("ProjectSettings.View", policy => policy.RequireClaim("permission", "ProjectSettings.View"));
    options.AddPolicy("ProjectSettings.Edit", policy => policy.RequireClaim("permission", "ProjectSettings.Edit"));
    options.AddPolicy("Project.Create", policy => policy.RequireClaim("permission", "Project.Create"));
    options.AddPolicy("Project.Edit", policy => policy.RequireClaim("permission", "Project.Edit"));
    options.AddPolicy("Project.Delete", policy => policy.RequireClaim("permission", "Project.Delete"));
    options.AddPolicy("StatusReport.View", policy => policy.RequireClaim("permission", "StatusReport.View"));
    options.AddPolicy("StatusReport.Create", policy => policy.RequireClaim("permission", "StatusReport.Create"));
    options.AddPolicy("StatusReport.Edit", policy => policy.RequireClaim("permission", "StatusReport.Edit"));
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

    options.AddPolicy("Risk.View", policy => policy.RequireClaim("permission", "Risk.View"));
    options.AddPolicy("Risk.Create", policy => policy.RequireClaim("permission", "Risk.Create"));
    options.AddPolicy("Risk.Edit", policy => policy.RequireClaim("permission", "Risk.Edit"));
    options.AddPolicy("Risk.Delete", policy => policy.RequireClaim("permission", "Risk.Delete"));
    options.AddPolicy("Risk.Mitigate", policy => policy.RequireClaim("permission", "Risk.Mitigate"));

    options.AddPolicy("Planning.View", policy => policy.RequireClaim("permission", "Planning.View"));
    options.AddPolicy("Planning.Edit", policy => policy.RequireClaim("permission", "Planning.Edit"));
    options.AddPolicy("Planning.Lifecycle", policy => policy.RequireClaim("permission", "Planning.Lifecycle"));
    options.AddPolicy("ProjectPlan.Create", policy => policy.RequireClaim("permission", "ProjectPlan.Create"));

    options.AddPolicy("Portfolio.View", policy => policy.RequireClaim("permission", "Portfolio.View"));
    options.AddPolicy("Portfolio.Create", policy => policy.RequireClaim("permission", "Portfolio.Create"));
    options.AddPolicy("Portfolio.Edit", policy => policy.RequireClaim("permission", "Portfolio.Edit"));
    options.AddPolicy("Portfolio.Archive", policy => policy.RequireClaim("permission", "Portfolio.Archive"));
    options.AddPolicy("Portfolio.Reactivate", policy => policy.RequireClaim("permission", "Portfolio.Reactivate"));
    options.AddPolicy("Program.View", policy => policy.RequireClaim("permission", "Program.View"));
    options.AddPolicy("Program.Create", policy => policy.RequireClaim("permission", "Program.Create"));
    options.AddPolicy("Program.CreateAcrossPortfolios", policy => policy.RequireClaim("permission", "Program.CreateAcrossPortfolios"));
    options.AddPolicy("Program.Edit", policy => policy.RequireClaim("permission", "Program.Edit"));
    options.AddPolicy("Program.Archive", policy => policy.RequireClaim("permission", "Program.Archive"));
    options.AddPolicy("Program.Reactivate", policy => policy.RequireClaim("permission", "Program.Reactivate"));
    options.AddPolicy("WorkItem.View", policy => policy.RequireClaim("permission", "WorkItem.View"));
    options.AddPolicy("WorkItem.Create", policy => policy.RequireClaim("permission", "WorkItem.Create"));
    options.AddPolicy("WorkItem.Edit", policy => policy.RequireClaim("permission", "WorkItem.Edit"));
    options.AddPolicy("WorkItem.Delete", policy => policy.RequireClaim("permission", "WorkItem.Delete"));
    options.AddPolicy("TimeTracking.View", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("permission", TimeTrackingPermissions.ViewOwn)
        || context.User.HasClaim("permission", TimeTrackingPermissions.ViewTeam)));
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
    options.AddPolicy("Workflow.View", policy => policy.RequireClaim("permission", "Workflow.View"));
    options.AddPolicy("Workflow.Manage", policy => policy.RequireClaim("permission", "Workflow.Manage"));
    options.AddPolicy("Workflow.Publish", policy => policy.RequireClaim("permission", "Workflow.Publish"));
    options.AddPolicy(AssignmentRecoveryPermissions.View, policy => policy.RequireClaim("permission", AssignmentRecoveryPermissions.View));
    options.AddPolicy(AssignmentRecoveryPermissions.Initiate, policy => policy.RequireClaim("permission", AssignmentRecoveryPermissions.Initiate));
    options.AddPolicy(AssignmentRecoveryPermissions.Manage, policy => policy.RequireClaim("permission", AssignmentRecoveryPermissions.Manage));
});

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Database=NexusPmo;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true";
    options.UseSqlServer(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<IntegrationSecretProtectionInterceptor>());
    options.AddInterceptors(serviceProvider.GetRequiredService<EmailNotificationProfileSecretProtectionInterceptor>());
});
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IProjectMilestoneReferenceLookup, ProjectPlanMilestoneLookup>();
builder.Services.AddScoped<ProjectPaymentRequestWorkflow>();
builder.Services.AddScoped<ProjectAnnualBudgetWorkflow>();
builder.Services.AddScoped<IProjectAudienceLookup, ProjectAudienceLookup>();
builder.Services.AddScoped<IMeetingPublicIdLookup, MeetingPublicIdLookup>();
builder.Services.AddScoped<IProjectOrganizationLookup, ProjectOrganizationLookup>();
builder.Services.AddScoped<IMeetingNotificationPublisher, MeetingNotificationPublisher>();
builder.Services.Configure<TimeTrackingOptions>(builder.Configuration.GetSection("TimeTracking"));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IProjectAuditScopeProvider, ProjectAuditScopeProvider>();
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
{
    builder.Services.AddHostedService<MeetingReminderScheduler>();
    builder.Services.AddScoped<RiskActionFollowUpProcessor>();
    builder.Services.AddHostedService<RiskActionFollowUpWorker>();
}

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

if (builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "CORS is not configured. Add 'Cors:AllowedOrigins' (e.g. https://localhost:4100) to appsettings.Development.json.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Trusted forwarded headers: only accept X-Forwarded-For / X-Forwarded-Proto
// from loopback and known reverse-proxy ranges. Forwarded headers are not
// honored unless a trusted proxy is explicitly configured via ForwardedHeaders
// options; this prevents header spoofing from direct client connections.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    // Loopback is trusted for local development and reverse-proxy deployments.
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
    options.KnownProxies.Add(IPAddress.Parse("::1"));
    options.ForwardLimit = 2;
});

builder.Services.AddHealthChecks();

    // Centralized FOPX PM product version metadata, shared by the API,
    // build metadata, and user-facing About screens.
    builder.Services.AddSingleton(ProductVersion.Load());

    // Security: throttle credential-guessing against the anonymous authentication endpoints.
    // Anonymous AI-planning or integration traffic stays bounded by the global fallback limiter.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.AddPolicy("EmailNotificationTest", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub")
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IManagementContextAccessor, ManagementContextAccessor>();
builder.Services.AddScoped<IManagementScopeResolver, ManagementScopeResolver>();
builder.Services.AddScoped<IProgramManagementService, ProgramManagementService>();
builder.Services.AddScoped<IPortfolioManagementService, PortfolioManagementService>();
builder.Services.AddScoped<IProgramPortfolioFinancialRollupService, ProgramPortfolioFinancialRollupService>();
builder.Services.AddScoped<AssignmentRecoveryService>();

builder.Services.AddIdentityModule();
builder.Services.AddScoped<DevelopmentAdminPasswordRecoveryService>();
builder.Services.AddOrganizationModule();
builder.Services.AddScoped<IFiscalYearConfigurationChangeGuard, FiscalYearConfigurationChangeGuard>();
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
builder.Services.AddScoped<IEmailNotificationProfileStore, EmailNotificationProfileStore>();
builder.Services.AddScoped<IEmailNotificationOrganizationScopeAccess, EmailNotificationOrganizationScopeAccess>();
builder.Services.AddScoped<EmailNotificationProfileSecretProtectionInterceptor>();
builder.Services.AddPluginManagementModule();
builder.Services.AddAIPlanningModule();
builder.Services.AddProjectPlanningModule();
builder.Services.AddProjectPlanningMppAdapter();

if (!skipDevelopmentDatabaseInitialization)
    builder.Services.AddConfiguredDataProtection(builder.Configuration, builder.Environment);

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

// Honor forwarded headers only from trusted reverse proxies before any
// middleware that reads connection properties (authentication, rate limiting).
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

// HSTS is reserved for non-Development environments. HTTPS redirection also
// applies in Development so the Secure antiforgery cookie is never issued from
// an HTTP request.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// Security headers are applied to every response after forwarding is resolved
// but before routing, so all endpoints receive consistent protection.
app.UseMiddleware<SecurityHeadersMiddleware>();

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
        using var trustedTenantContext = NEXUS.SharedKernel.TrustedTenantExecutionContext.Enter(SeedDataProvider.TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Schema changes are applied through scripts/database/ef-database-gate.ps1
        // after the Development target and reviewed SQL have been verified. API startup
        // must not migrate a configured legacy Development database implicitly.

        if (seedIdentity)
        {
            var passwordPolicy = scope.ServiceProvider.GetRequiredService<IOptions<LocalPasswordPolicyOptions>>().Value;
            await SeedDataProvider.SeedIdentityOnlyAsync(context, passwordPolicy.TemporaryPasswordExpiryHours);
            return;
        }

        if (seedMinimalOnly)
        {
            var passwordPolicy = scope.ServiceProvider.GetRequiredService<IOptions<LocalPasswordPolicyOptions>>().Value;
            await SeedDataProvider.SeedMinimalAsync(context, passwordPolicy.TemporaryPasswordExpiryHours);
            return;
        }

        if (bootstrapPermissionDelegation)
        {
            var result = await SeedDataProvider.BootstrapInitialDelegationPermissionsAsync(
                context,
                app.Environment.IsDevelopment(),
                delegationBootstrapExpectedServer!,
                delegationBootstrapExpectedDatabase!);
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, result.AuthorizedAdminUserId.ToString()),
                    new System.Security.Claims.Claim("tenantId", SeedDataProvider.TenantId.ToString())
                ], "DevelopmentDelegationVerification"))
            };
            var roleAdministration = scope.ServiceProvider.GetRequiredService<IRoleAdministrationService>();
            var assignablePermissions = await roleAdministration.GetAssignablePermissionsAsync(result.AuthorizedAdminUserId, SeedDataProvider.TenantId);
            var expectedDelegableKeys = new[]
            {
                "ProjectSettings.View", "ProjectSettings.Edit",
                "ProjectAnnualBudget.View",
                "ProjectAnnualBudget.ManageApprovalConfiguration",
                "ProjectFinancialFoundation.View", "CostUtilization.Export",
                "ProgramFinancialRollup.View", "PortfolioFinancialRollup.View"
            };
            if (assignablePermissions.IsFailure
                || expectedDelegableKeys.Except(assignablePermissions.Value!.Select(permission => permission.Name), StringComparer.Ordinal).Any())
                throw new InvalidOperationException("The verified Development Admin cannot see all eight bootstrapped Project Settings and explicitly delegable financial permissions through RoleAdministrationService.");
            httpContextAccessor.HttpContext = null;

            app.Logger.LogInformation(
                "Scoped Development permission delegation bootstrap completed for SQL target {Server}/{Database}, database ID {DatabaseId}, Tenant {TenantId}: {AddedCatalogPermissions} canonical permission rows added and {AddedProjectSettingsAdminGrants} Project Settings grants added to the protected Admin role and {AuthorizedAdminUsers} active Admin users verified with effective delegation capabilities; RoleAdministrationService listed all six delegable catalog entries. No financial permission grants or account changes were made.",
                result.Server, result.Database, result.DatabaseId, SeedDataProvider.TenantId, result.AddedCatalogPermissions, result.AddedProjectSettingsAdminGrants, result.AuthorizedAdminUsers);
            return;
        }

        if (recoverDevelopmentAdminPassword)
        {
            await SeedDataProvider.VerifyDevelopmentAdminRecoveryTargetAsync(
                context,
                app.Environment.IsDevelopment(),
                adminRecoveryExpectedServer!,
                adminRecoveryExpectedDatabase!,
                adminRecoveryBackupDirectory!);

            var passwordPolicy = scope.ServiceProvider.GetRequiredService<IOptions<LocalPasswordPolicyOptions>>().Value;
            var passwords = DevelopmentAdminRecoveryConsole.PromptForRecoveryFlow(
                passwordPolicy.MinimumLength,
                passwordPolicy.MaximumLength);

            var verifiedBackupPath = await SeedDataProvider.CreateVerifiedDevelopmentAdminRecoveryBackupAsync(
                context,
                adminRecoveryExpectedServer!,
                adminRecoveryExpectedDatabase!,
                adminRecoveryBackupDirectory!);
            app.Logger.LogInformation(
                "Verified independent Development backup created for Admin recovery at {BackupPath}.",
                verifiedBackupPath);

            var recovery = scope.ServiceProvider.GetRequiredService<DevelopmentAdminPasswordRecoveryService>();
            var temporaryPasswordExpiresAt = await recovery.RecoverTemporaryPasswordAsync(passwords.Temporary);
            var authentication = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var tenantId = SeedDataProvider.TenantId;
            var temporaryLogin = await authentication.LoginAsync(new LoginCommand(
                "admin@nexus.com",
                passwords.Temporary,
                tenantId.ToString()));
            if (temporaryLogin.IsFailure
                || temporaryLogin.Value is null
                || !temporaryLogin.Value.PasswordChangeRequired
                || !temporaryLogin.Value.User.MustChangePassword
                || temporaryLogin.Value.User.Permissions.Count != 0
                || temporaryLogin.Value.User.Roles.Count != 0)
                throw new InvalidOperationException("The recovered Admin temporary-password login did not enforce the first-login password change.");

            var passwordChange = await authentication.ChangeInitialPasswordAsync(
                temporaryLogin.Value.User.Id,
                tenantId,
                new ChangeInitialPasswordCommand(passwords.Temporary, passwords.Permanent));
            if (passwordChange.IsFailure)
                throw new InvalidOperationException("The Admin first-login password change verification failed.");

            var permanentLogin = await authentication.LoginAsync(new LoginCommand(
                "admin@nexus.com",
                passwords.Permanent,
                tenantId.ToString()));
            if (permanentLogin.IsFailure
                || permanentLogin.Value is null
                || permanentLogin.Value.PasswordChangeRequired
                || permanentLogin.Value.User.MustChangePassword
                || permanentLogin.Value.User.TemporaryPasswordExpiresAt is not null)
                throw new InvalidOperationException("The Admin login with the new permanent password did not complete successfully.");

            Console.WriteLine("Development Admin recovery completed.");
            Console.WriteLine("Temporary-password login: verified; first-login password change: required and completed.");
            Console.WriteLine("Login with the new permanent password: verified.");
            Console.WriteLine($"Temporary-password expiry at recovery: {temporaryPasswordExpiresAt:O} UTC.");
            Console.WriteLine($"Verified backup: {verifiedBackupPath}");
            return;
        }

        if (rebuildCanonicalPermissions)
        {
            var result = await SeedDataProvider.RebuildCanonicalPermissionsAsync(
                context,
                permissionRebuildExpectedServer!,
                permissionRebuildExpectedDatabase!,
                permissionRebuildBackupDirectory!);
            app.Logger.LogInformation(
                "Canonical Development permission reconciliation completed for SQL target {Server}/{Database}, Tenant {TenantId}; verified backup {BackupPath}: {AddedCatalogPermissions} catalog permissions added, {NormalizedCatalogPermissions} normalized, {RetiredLegacyPermissions} retired, {RemovedLegacyRoleGrants} legacy role grants removed, {RetiredLegacyOverrides} legacy direct overrides retired, {AddedProtectedAdminRoleGrants} protected Admin grants added, {RemovedProtectedAdminDenyOverrides} protected Admin Deny overrides archived.",
                permissionRebuildExpectedServer,
                permissionRebuildExpectedDatabase,
                SeedDataProvider.TenantId,
                result.VerifiedBackupPath,
                result.AddedCatalogPermissions,
                result.NormalizedCatalogPermissions,
                result.RetiredLegacyPermissions,
                result.RemovedLegacyRoleGrants,
                result.RetiredLegacyOverrides,
                result.AddedProtectedAdminRoleGrants,
                result.RemovedProtectedAdminDenyOverrides);
            return;
        }

        var seedPasswordPolicy = scope.ServiceProvider.GetRequiredService<IOptions<LocalPasswordPolicyOptions>>().Value;
        await SeedDataProvider.SeedAsync(context, seedPasswordPolicy.TemporaryPasswordExpiryHours);
        await ConfiguredStandardWorkingCalendarInitializer.EnsureAsync(context, builder.Configuration, app.Logger);

        var workItemWorkflowProvisioning = scope.ServiceProvider.GetRequiredService<IWorkItemWorkflowProvisioningService>();
        var workflowProvisioning = await workItemWorkflowProvisioning.EnsureCurrentTenantAsync("System", CancellationToken.None);
        if (workflowProvisioning.IsFailure)
            throw new InvalidOperationException(workflowProvisioning.Error?.Message ?? "Work Item workflow provisioning failed.");
        var workflowOnboarding = await workItemWorkflowProvisioning.OnboardCurrentTenantAsync("System", CancellationToken.None);
        if (workflowOnboarding.IsSuccess && workflowOnboarding.Value!.Exceptions.Count > 0)
            app.Logger.LogWarning("Work Item workflow onboarding completed with {Count} exceptions.", workflowOnboarding.Value.Exceptions.Count);

        if (seedOnly)
        {
            app.Logger.LogInformation("Development seeding completed; exiting before opening an HTTP listener.");
            return;
        }
    }
}

if (!skipDevelopmentDatabaseInitialization && !app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ConfiguredStandardWorkingCalendarInitializer.EnsureAsync(context, builder.Configuration, app.Logger);
}

app.UseRouting();
app.UseRateLimiter();
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseMiddleware<InitialPasswordEnforcementMiddleware>();
app.UseMiddleware<UserPermissionClaimsMiddleware>();
app.UseMiddleware<ProjectPublicIdRoutingMiddleware>();
app.UseMiddleware<ManagementContextMiddleware>();
app.UseMiddleware<ProjectResourceScopeMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

static string? GetOptionValue(string[] arguments, string option)
{
    var index = Array.FindIndex(arguments, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
    if (index < 0) return null;
    if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        throw new InvalidOperationException($"Command-line option {option} requires a value.");
    return arguments[index + 1];
}
