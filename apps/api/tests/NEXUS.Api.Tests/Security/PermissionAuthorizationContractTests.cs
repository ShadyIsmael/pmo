using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using NEXUS.Api.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.CostManagement.Api;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Api;
using NEXUS.Modules.IssueManagement.Api;
using NEXUS.Modules.Organization.Api;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.PluginManagement.Api;
using NEXUS.Modules.Portfolio.Api;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.ProjectManagement.Api;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Api;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Api.Governance;
using NEXUS.SharedKernel;
using Moq;
using Xunit;
using AspNetAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace NEXUS.Api.Tests.Security;

public sealed class PermissionAuthorizationContractTests
{
    [Theory]
    [InlineData("ProjectPaymentRequest.Approve")]
    [InlineData("PaymentSettlement.Record")]
    public void Financial_action_policy_requires_only_its_exact_canonical_permission(string permissionKey)
    {
        var options = new AuthorizationOptions();
        options.AddCanonicalFinancialActionPolicies();

        var policy = options.GetPolicy(permissionKey);
        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal("permission", requirement.ClaimType);
        Assert.Equal(new[] { permissionKey }, requirement.AllowedValues);
    }

    [Theory]
    [InlineData(typeof(IssueController), "Issue.View")]
    [InlineData(typeof(PluginController), "Plugin.View")]
    public void Read_only_registry_controllers_require_their_canonical_permission(Type controllerType, string expectedPermission)
    {
        var authorization = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorization);
        Assert.Equal(expectedPermission, authorization!.Policy);
    }

    [Fact]
    public void Directory_connection_writes_require_a_dedicated_tenant_capability()
    {
        var entry = PermissionCatalog.ByKey["Directory.ManageConnection"];
        Assert.Equal(PermissionScope.Tenant, entry.Scope);
        Assert.Equal(PermissionLevel.Action, entry.Level);
        Assert.Contains("Directory.View", entry.RequiredPermissionKeys);

        foreach (var name in new[] { nameof(DirectoryIntegrationController.CreateConnection), nameof(DirectoryIntegrationController.UpdateConnection), nameof(DirectoryIntegrationController.SaveScope) })
        {
            var method = typeof(DirectoryIntegrationController).GetMethod(name);
            Assert.NotNull(method);
            Assert.Equal("Directory.ManageConnection", method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        }
    }

    [Fact]
    public void Status_report_export_endpoint_requires_the_server_export_permission()
    {
        var method = typeof(ProjectController).GetMethod(nameof(ProjectController.GetStatusReportForExport));
        Assert.NotNull(method);
        Assert.Equal("StatusReport.Export", method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(method.GetCustomAttributes<HttpGetAttribute>(), route => route.Template?.EndsWith("/export", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Settings_access_endpoint_checks_the_canonical_tenant_policy_names()
    {
        var requestedPolicies = new List<string>();
        var authorization = new Mock<AspNetAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .Callback<ClaimsPrincipal, object?, string>((_, _, policyName) => requestedPolicies.Add(policyName))
            .ReturnsAsync(AuthorizationResult.Success());

        var controller = new SettingsController(
            Mock.Of<IConfigurationSettingsService>(),
            authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.GetAccess(CancellationToken.None);

        Assert.Equal(
            ["Settings.View", "Settings.ManageTenantConfiguration", "PlatformSettings.Read", "PlatformSettings.Write"],
            requestedPolicies);
    }

    [Theory]
    [InlineData(typeof(ManagerHandoverController), "EligibleProgramManagers", "Handover.View")]
    [InlineData(typeof(ManagerHandoverController), "EligiblePortfolioManagers", "Handover.View")]
    [InlineData(typeof(ManagerHandoverController), "CancelProgram", "Handover.View")]
    [InlineData(typeof(ManagerHandoverController), "CancelPortfolio", "Handover.View")]
    [InlineData(typeof(HandoverRequestController), "Cancel", "Handover.View")]
    [InlineData(typeof(ProjectController), "GetEligibleHandoverManagers", "Handover.View")]
    [InlineData(typeof(AssignmentRecoveryController), "GetManagerCandidates", "Governance.AssignmentRecovery.View")]
    public void Shared_handover_lookup_routes_use_the_canonical_view_policy(Type controllerType, string actionName, string expectedPermission)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);
        Assert.Equal(expectedPermission, method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Cost_management_api_actions_use_exact_active_catalog_policies()
    {
        var controllers = new[]
        {
            typeof(BudgetController), typeof(BudgetRevisionController), typeof(CostController),
            typeof(CommitmentController), typeof(PaymentObligationController)
        };

        foreach (var controller in controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                     .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()))
        {
            var policy = action.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
            Assert.False(string.IsNullOrWhiteSpace(policy), $"{controller.Name}.{action.Name} must declare an action policy.");
            Assert.True(PermissionCatalog.ByKey.TryGetValue(policy!, out var definition) && definition.IsActive,
                $"{controller.Name}.{action.Name} references a non-canonical permission policy '{policy}'.");
            Assert.Equal(policy, definition!.Key);
        }
    }

    [Fact]
    public async Task Project_lookup_policies_allow_only_the_source_view_or_effective_project_operation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();
        var policyNames = new[]
        {
            WorkflowLookupPolicyRegistration.ProjectPortfolios,
            WorkflowLookupPolicyRegistration.ProjectPrograms,
            WorkflowLookupPolicyRegistration.ProjectOrganizationalUnits,
            WorkflowLookupPolicyRegistration.ProjectCurrencies,
            WorkflowLookupPolicyRegistration.ProjectUsers
        };

        foreach (var policyName in policyNames)
        {
            Assert.False(PermissionCatalog.ByKey.ContainsKey(policyName));
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Project.Create"), policyName)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Project.Edit"), policyName)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Unrelated.Permission"), policyName)).Succeeded);
        }

        var deniedProjectCreate = EffectivePermissionResolver.Resolve(
            ["Project.Create"],
            [("Project.Create", PermissionOverrideEffect.Deny)]);
        foreach (var policyName in policyNames)
            Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedProjectCreate), policyName)).Succeeded);

        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Project.View"), WorkflowLookupPolicyRegistration.ProjectUsers)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Program.Create"), WorkflowLookupPolicyRegistration.ProgramCreatePortfolios)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Project.Create"), WorkflowLookupPolicyRegistration.ProgramCreatePortfolios)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Portfolio.View"), WorkflowLookupPolicyRegistration.ProgramCreatePortfolios)).Succeeded);
        var deniedProgramCreate = EffectivePermissionResolver.Resolve(
            ["Program.Create"],
            [("Program.Create", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedProgramCreate), WorkflowLookupPolicyRegistration.ProgramCreatePortfolios)).Succeeded);

        Assert.False(PermissionCatalog.ByKey.ContainsKey(WorkflowLookupPolicyRegistration.ProgramEditPortfolios));
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Program.Edit"), WorkflowLookupPolicyRegistration.ProgramEditPortfolios)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Program.Create"), WorkflowLookupPolicyRegistration.ProgramEditPortfolios)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Portfolio.View"), WorkflowLookupPolicyRegistration.ProgramEditPortfolios)).Succeeded);
        var deniedProgramEdit = EffectivePermissionResolver.Resolve(
            ["Program.Edit"],
            [("Program.Edit", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedProgramEdit), WorkflowLookupPolicyRegistration.ProgramEditPortfolios)).Succeeded);
    }

    [Fact]
    public async Task Payment_obligation_project_lookup_policy_allows_each_payment_operation_without_project_view()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();

        foreach (var permission in new[] { "PaymentObligation.View", "PaymentObligation.Create", "PaymentObligation.Edit" })
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith(permission), WorkflowLookupPolicyRegistration.PaymentObligationProjects)).Succeeded);

        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Project.View"), WorkflowLookupPolicyRegistration.PaymentObligationProjects)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Users.View"), WorkflowLookupPolicyRegistration.PaymentObligationProjects)).Succeeded);

        var deniedEdit = EffectivePermissionResolver.Resolve(
            ["PaymentObligation.Edit"],
            [("PaymentObligation.Edit", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedEdit), WorkflowLookupPolicyRegistration.PaymentObligationProjects)).Succeeded);
    }

    [Fact]
    public async Task Resource_reference_lookup_policies_allow_resource_operations_without_view()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();
        var policies = new[]
        {
            WorkflowLookupPolicyRegistration.ResourceReferenceData,
            WorkflowLookupPolicyRegistration.ResourceVendors
        };

        foreach (var policy in policies)
        {
            Assert.False(PermissionCatalog.ByKey.ContainsKey(policy));
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.Create"), policy)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.Edit"), policy)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.View"), policy)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Users.View"), policy)).Succeeded);
        }

        var deniedEdit = EffectivePermissionResolver.Resolve(
            ["Resource.Edit"],
            [("Resource.Edit", PermissionOverrideEffect.Deny)]);
        foreach (var policy in policies)
            Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedEdit), policy)).Succeeded);
    }

    [Fact]
    public async Task Resource_base_calendar_lookup_allows_settings_view_and_honors_effective_denies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();
        var policy = WorkflowLookupPolicyRegistration.ResourceBaseCalendars;

        Assert.False(PermissionCatalog.ByKey.ContainsKey(policy));
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Settings.View"), policy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.View"), policy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.Create"), policy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Resource.Edit"), policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Settings.ManageTenantConfiguration"), policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Users.View"), policy)).Succeeded);

        var deniedSettingsView = EffectivePermissionResolver.Resolve(
            ["Settings.View"],
            [("Settings.View", PermissionOverrideEffect.Deny)]);
        var deniedResourceCreate = EffectivePermissionResolver.Resolve(
            ["Resource.Create"],
            [("Resource.Create", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedSettingsView), policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedResourceCreate), policy)).Succeeded);
    }

    [Fact]
    public void Resource_vendor_lookup_projection_contains_only_selection_fields()
    {
        Assert.Equal(
            ["Id", "Name", "Code", "IsActive"],
            typeof(ResourceVendorLookupDto).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Payment_obligation_project_lookup_route_uses_its_dedicated_policy()
    {
        var controller = typeof(PaymentObligationProjectLookupController);
        var authorization = controller.GetCustomAttribute<AuthorizeAttribute>();
        var method = controller.GetMethod(nameof(PaymentObligationProjectLookupController.GetPaymentObligationProjects));

        Assert.NotNull(authorization);
        Assert.Equal(WorkflowLookupPolicyRegistration.PaymentObligationProjects, authorization!.Policy);
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes<HttpGetAttribute>(),
            route => route.Template == "payment-obligations");
        Assert.Equal("api/projects/workflow-lookups", controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Fact]
    public async Task Payment_obligation_project_lookup_projects_only_minimal_selection_fields()
    {
        var projectId = Guid.NewGuid();
        var publicId = Guid.NewGuid();
        var cancellationToken = new CancellationTokenSource().Token;
        var projectLookup = new Mock<IProjectReferenceLookup>();
        projectLookup
            .Setup(lookup => lookup.GetFinancialReferencesAsync(cancellationToken))
            .ReturnsAsync(new[]
            {
                new ProjectReference(projectId, "Project Alpha", "USD", 9_876_543m, publicId, "PA-001")
            });
        var service = new PaymentObligationProjectLookupService(projectLookup.Object);

        var result = await service.GetProjectsAsync(cancellationToken);

        Assert.Equal(
            [new PaymentObligationProjectLookupDto(projectId, publicId, "Project Alpha", "PA-001", "USD")],
            result);
        Assert.Equal(
            new[] { nameof(PaymentObligationProjectLookupDto.Id), nameof(PaymentObligationProjectLookupDto.PublicId),
                nameof(PaymentObligationProjectLookupDto.Name), nameof(PaymentObligationProjectLookupDto.Code),
                nameof(PaymentObligationProjectLookupDto.Currency) },
            typeof(PaymentObligationProjectLookupDto).GetProperties().Select(property => property.Name));
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("CurrentApprovedProjectBudget", json);
        Assert.DoesNotContain("9876543", json);
        projectLookup.Verify(lookup => lookup.GetFinancialReferencesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Meeting_project_lookup_policy_accepts_only_canonical_meeting_view_or_operations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();

        Assert.False(PermissionCatalog.ByKey.ContainsKey(WorkflowLookupPolicyRegistration.MeetingProjects));
        foreach (var permission in new[] { "Meeting.View", "Meeting.Create", "Meeting.Edit" })
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith(permission), WorkflowLookupPolicyRegistration.MeetingProjects)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Portfolio.Create"), WorkflowLookupPolicyRegistration.MeetingProjects)).Succeeded);

        var deniedMeetingEdit = EffectivePermissionResolver.Resolve(
            ["Meeting.Edit"],
            [("Meeting.Edit", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedMeetingEdit), WorkflowLookupPolicyRegistration.MeetingProjects)).Succeeded);
    }

    [Fact]
    public async Task Portfolio_and_program_manager_candidate_policies_require_their_canonical_operations_or_handover_assignment_permission()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();

        Assert.False(PermissionCatalog.ByKey.ContainsKey(WorkflowLookupPolicyRegistration.PortfolioManagerCandidates));
        Assert.False(PermissionCatalog.ByKey.ContainsKey(WorkflowLookupPolicyRegistration.ProgramManagerCandidates));
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Portfolio.Create"), WorkflowLookupPolicyRegistration.PortfolioManagerCandidates)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Portfolio.Edit"), WorkflowLookupPolicyRegistration.PortfolioManagerCandidates)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Program.Create"), WorkflowLookupPolicyRegistration.ProgramManagerCandidates)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Program.Edit"), WorkflowLookupPolicyRegistration.ProgramManagerCandidates)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Handover.AdminInitiate"), WorkflowLookupPolicyRegistration.PortfolioManagerCandidates)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(PrincipalWith("Handover.AdminInitiate"), WorkflowLookupPolicyRegistration.ProgramManagerCandidates)).Succeeded);

        foreach (var permission in new[] { "Portfolio.View", "Program.View", "Users.View", "Unrelated.Permission" })
        {
            Assert.False((await authorization.AuthorizeAsync(PrincipalWith(permission), WorkflowLookupPolicyRegistration.PortfolioManagerCandidates)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(PrincipalWith(permission), WorkflowLookupPolicyRegistration.ProgramManagerCandidates)).Succeeded);
        }

        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Portfolio.Create"), WorkflowLookupPolicyRegistration.ProgramManagerCandidates)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Program.Create"), WorkflowLookupPolicyRegistration.PortfolioManagerCandidates)).Succeeded);

        var deniedPortfolioCreate = EffectivePermissionResolver.Resolve(
            ["Portfolio.Create"],
            [("Portfolio.Create", PermissionOverrideEffect.Deny)]);
        var deniedProgramEdit = EffectivePermissionResolver.Resolve(
            ["Program.Edit"],
            [("Program.Edit", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedPortfolioCreate), WorkflowLookupPolicyRegistration.PortfolioManagerCandidates)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedProgramEdit), WorkflowLookupPolicyRegistration.ProgramManagerCandidates)).Succeeded);
    }

    [Fact]
    public async Task Business_currency_lookup_accepts_resource_cost_rate_permissions_and_respects_effective_denies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddWorkflowLookupPolicies());
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<AspNetAuthorizationService>();

        foreach (var permission in new[] { "Resource.ViewCostRate", "Resource.EditCostRate" })
        {
            Assert.True(PermissionCatalog.ByKey.TryGetValue(permission, out var definition) && definition.IsActive);
            Assert.True((await authorization.AuthorizeAsync(PrincipalWith(permission), WorkflowLookupPolicyRegistration.BusinessCurrencies)).Succeeded);
        }

        Assert.False((await authorization.AuthorizeAsync(PrincipalWith("Unrelated.Permission"), WorkflowLookupPolicyRegistration.BusinessCurrencies)).Succeeded);

        var deniedViewCostRate = EffectivePermissionResolver.Resolve(
            ["Resource.ViewCostRate"],
            [("Resource.ViewCostRate", PermissionOverrideEffect.Deny)]);
        var deniedEditCostRate = EffectivePermissionResolver.Resolve(
            ["Resource.EditCostRate"],
            [("Resource.EditCostRate", PermissionOverrideEffect.Deny)]);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedViewCostRate), WorkflowLookupPolicyRegistration.BusinessCurrencies)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(PrincipalWith(deniedEditCostRate), WorkflowLookupPolicyRegistration.BusinessCurrencies)).Succeeded);
    }

    [Theory]
    [InlineData(nameof(UsersController.GetProjectManagerCandidates), "workflow-lookups/projects/managers", "WorkflowLookup.Project.Users")]
    [InlineData(nameof(UsersController.GetPortfolioManagerCandidates), "workflow-lookups/portfolios/managers", "WorkflowLookup.Portfolio.Managers")]
    [InlineData(nameof(UsersController.GetProgramManagerCandidates), "workflow-lookups/programs/managers", "WorkflowLookup.Program.Managers")]
    public void Manager_candidate_endpoints_use_purpose_specific_routes_and_policies(string actionName, string route, string policy)
    {
        var method = typeof(UsersController).GetMethod(actionName);
        Assert.NotNull(method);
        Assert.Equal(policy, method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(method.GetCustomAttributes<HttpGetAttribute>(), attribute => attribute.Template == route);
        Assert.DoesNotContain(method.GetParameters(), parameter => parameter.Name == "role");
    }

    [Theory]
    [InlineData(typeof(PortfolioController), "GetProjectLookup", WorkflowLookupPolicyRegistration.ProjectPortfolios)]
    [InlineData(typeof(ProgramController), "GetProjectLookup", WorkflowLookupPolicyRegistration.ProjectPrograms)]
    [InlineData(typeof(OrganizationalUnitController), "GetProjectLookup", WorkflowLookupPolicyRegistration.ProjectOrganizationalUnits)]
    [InlineData(typeof(CurrencyController), "GetProjectLookup", WorkflowLookupPolicyRegistration.ProjectCurrencies)]
    [InlineData(typeof(UsersController), "GetProjectWorkflowUsers", WorkflowLookupPolicyRegistration.ProjectUsers)]
    [InlineData(typeof(ProgramController), "GetEligiblePortfolios", WorkflowLookupPolicyRegistration.ProgramCreatePortfolios)]
    [InlineData(typeof(ProgramController), "GetEditablePortfolios", WorkflowLookupPolicyRegistration.ProgramEditPortfolios)]
    [InlineData(typeof(ResourceController), "GetBaseCalendars", WorkflowLookupPolicyRegistration.ResourceBaseCalendars)]
    [InlineData(typeof(ResourceController), "GetReferenceData", WorkflowLookupPolicyRegistration.ResourceReferenceData)]
    [InlineData(typeof(VendorController), "GetResourceVendorOptions", WorkflowLookupPolicyRegistration.ResourceVendors)]
    public void Workflow_lookup_endpoints_use_workflow_scoped_policies(Type controllerType, string actionName, string policyName)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);
        Assert.Equal(policyName, method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Theory]
    [InlineData(typeof(PortfolioController), "GetProjectLookup", "api/portfolios/project-lookup", WorkflowLookupPolicyRegistration.ProjectPortfolios)]
    [InlineData(typeof(ProgramController), "GetProjectLookup", "api/programs/project-lookup", WorkflowLookupPolicyRegistration.ProjectPrograms)]
    [InlineData(typeof(ProgramController), "GetEditablePortfolios", "api/programs/editable-portfolios", WorkflowLookupPolicyRegistration.ProgramEditPortfolios)]
    [InlineData(typeof(ResourceController), "GetBaseCalendars", "api/resources/base-calendars", WorkflowLookupPolicyRegistration.ResourceBaseCalendars)]
    [InlineData(typeof(ResourceController), "GetReferenceData", "api/resources/reference-data", WorkflowLookupPolicyRegistration.ResourceReferenceData)]
    [InlineData(typeof(VendorController), "GetResourceVendorOptions", "api/vendors/workflow-lookups/resource-options", WorkflowLookupPolicyRegistration.ResourceVendors)]
    [InlineData(typeof(OrganizationalUnitController), "GetProjectLookup", "api/organization/units/project-lookup", WorkflowLookupPolicyRegistration.ProjectOrganizationalUnits)]
    [InlineData(typeof(CurrencyController), "GetProjectLookup", "api/currencies/project-lookup", WorkflowLookupPolicyRegistration.ProjectCurrencies)]
    [InlineData(typeof(HandoverRequestController), "Review", "api/handover-requests/{id:guid}/review", "Handover.Approve")]
    public void Reported_404_routes_are_composed_as_expected_and_remain_protected(
        Type controllerType,
        string actionName,
        string expectedPath,
        string expectedPolicy)
    {
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template;
        var action = controllerType.GetMethod(actionName);

        Assert.False(string.IsNullOrWhiteSpace(controllerRoute));
        Assert.NotNull(action);
        Assert.Equal(expectedPolicy, action!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        var getRoute = Assert.Single(action.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal(expectedPath, $"{controllerRoute!.Trim('/')}/{getRoute.Template!.Trim('/')}");
    }

    [Fact]
    public async Task Handover_review_returns_success_for_an_assigned_request_and_404_for_an_unavailable_request()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var missingRequestId = Guid.NewGuid();
        var review = new ApprovalReviewDto { ApprovalId = requestId, ApprovalType = "ProjectHandover" };
        var service = new Mock<IProjectHandoverRequestService>();
        service.Setup(item => item.GetApprovalReviewAsync(requestId, userId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApprovalReviewDto>.Success(review));
        service.Setup(item => item.GetApprovalReviewAsync(missingRequestId, userId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is no longer available.")));
        var controller = new HandoverRequestController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
                }
            }
        };

        var success = await controller.Review(requestId, CancellationToken.None);
        Assert.Same(review, Assert.IsType<OkObjectResult>(success).Value);

        var missing = await controller.Review(missingRequestId, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(missing);
    }

    [Theory]
    [InlineData(typeof(PortfolioController), "GetPaged", "Portfolio.View")]
    [InlineData(typeof(ProgramController), "GetPaged", "Program.View")]
    [InlineData(typeof(OrganizationalUnitController), "GetUnits", "Settings.View")]
    [InlineData(typeof(CurrencyController), "GetAll", "ReferenceData.View")]
    public void Full_entity_lists_keep_their_existing_view_policies(Type controllerType, string actionName, string expectedPolicy)
    {
        var method = controllerType.GetMethod(actionName);
        Assert.NotNull(method);
        Assert.Equal(expectedPolicy, method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Program_creation_mutation_requires_only_its_canonical_action_permission()
    {
        var method = typeof(ProgramController).GetMethod(nameof(ProgramController.Create));
        Assert.NotNull(method);
        Assert.Equal("Program.Create", method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public async Task Project_creation_lookups_use_minimal_tenant_scoped_service_projections_without_view_permissions()
    {
        var context = ProjectCreateControllerContext();

        var portfolioLookup = new PagedResult<PortfolioLookupDto>(Array.Empty<PortfolioLookupDto>(), 1, 20, 0);
        var portfolios = new Mock<IPortfolioService>();
        portfolios.Setup(service => service.GetProjectLookupAsync(1, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolioLookup);
        var portfolioResult = await new PortfolioController(portfolios.Object) { ControllerContext = context }
            .GetProjectLookup(1, 20, null, null, CancellationToken.None);
        Assert.Same(portfolioLookup, Assert.IsType<OkObjectResult>(portfolioResult).Value);
        portfolios.Verify(service => service.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        var programLookup = new PagedResult<ProgramLookupDto>(Array.Empty<ProgramLookupDto>(), 1, 20, 0);
        var programs = new Mock<IProgramService>();
        programs.Setup(service => service.GetProjectLookupAsync(1, 20, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(programLookup);
        var programResult = await new ProgramController(programs.Object) { ControllerContext = ProjectCreateControllerContext() }
            .GetProjectLookup(1, 20, null, null, null, CancellationToken.None);
        Assert.Same(programLookup, Assert.IsType<OkObjectResult>(programResult).Value);
        programs.Verify(service => service.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);

        var organizationalUnits = new Mock<IOrganizationalUnitService>();
        organizationalUnits.Setup(service => service.GetProjectLookupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrganizationalUnitLookupDto>());
        await new OrganizationalUnitController(organizationalUnits.Object) { ControllerContext = ProjectCreateControllerContext() }
            .GetProjectLookup(CancellationToken.None);
        organizationalUnits.Verify(service => service.GetProjectLookupAsync(It.IsAny<CancellationToken>()), Times.Once);
        organizationalUnits.Verify(service => service.GetUnitsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        var currencies = new Mock<ICurrencyService>();
        currencies.Setup(service => service.GetProjectLookupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CurrencyLookupDto>());
        await new CurrencyController(currencies.Object) { ControllerContext = ProjectCreateControllerContext() }
            .GetProjectLookup(CancellationToken.None);
        currencies.Verify(service => service.GetProjectLookupAsync(It.IsAny<CancellationToken>()), Times.Once);
        currencies.Verify(service => service.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Sensitive_user_lookup_remains_gated_by_users_view()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.GetLookup));
        Assert.NotNull(method);
        Assert.Equal("Users.View", method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var handoverMethod = typeof(UsersController).GetMethod(nameof(UsersController.GetManagerAssignmentUsers));
        Assert.NotNull(handoverMethod);
        Assert.Equal("WorkflowLookup.Handover.ManagerCandidates", handoverMethod!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(handoverMethod.GetCustomAttributes<HttpGetAttribute>(),
            attribute => attribute.Template == "workflow-lookups/manager-assignments");
    }

    [Fact]
    public async Task Project_workflow_user_lookup_returns_only_minimal_fields_without_users_view()
    {
        var tenantId = Guid.NewGuid();
        var users = new[]
        {
            new User { Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Project", LastName = "Manager", Email = "private@example.test", IsActive = true }
        };
        var repository = new Mock<IUserRepository>();
        repository.Setup(item => item.GetActiveInTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        var controller = new UsersController(repository.Object, Mock.Of<IUserAdministrationService>(), Mock.Of<NEXUS.Modules.Identity.Application.IUserEffectivePermissionService>())
        {
            ControllerContext = ProjectCreateControllerContext(tenantId)
        };

        // Direct invocation skips the MVC authorization filter, so keep its policy contract explicit here.
        var methodPolicy = typeof(UsersController).GetMethod(nameof(UsersController.GetProjectWorkflowUsers))!
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy;
        Assert.Equal(WorkflowLookupPolicyRegistration.ProjectUsers, methodPolicy);

        var result = await controller.GetProjectWorkflowUsers(CancellationToken.None);
        var returnedUsers = Assert.IsType<List<WorkflowUserLookupDto>>(Assert.IsType<OkObjectResult>(result).Value);

        Assert.Equal([new WorkflowUserLookupDto(users[0].Id, "Project Manager")], returnedUsers);
        Assert.DoesNotContain("private@example.test", System.Text.Json.JsonSerializer.Serialize(returnedUsers));
        repository.Verify(item => item.GetActiveInTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Portfolio_and_program_manager_candidates_use_tenant_and_approved_eligibility_rules()
    {
        var tenantId = Guid.NewGuid();
        var portfolioManager = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Portfolio", LastName = "Manager",
            Email = "portfolio.private@example.test", IsActive = true
        };
        var portfolioWithoutView = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Portfolio", LastName = "Missing View",
            Email = "portfolio.no-view@example.test", IsActive = true
        };
        var portfolioWithoutApproval = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Portfolio", LastName = "Missing Approval",
            Email = "portfolio.no-approval@example.test", IsActive = true
        };
        // Program eligibility is permission-based; no program.manager role is seeded or required.
        var programManager = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Program", LastName = "Manager",
            Email = "program.private@example.test", IsActive = true
        };
        var programWithoutView = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Program", LastName = "Missing View",
            Email = "program.no-view@example.test", IsActive = true
        };
        var programWithoutApproval = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Program", LastName = "Missing Approval",
            Email = "program.no-approval@example.test", IsActive = true
        };
        var repository = new Mock<IUserRepository>();
        repository.SetupSequence(item => item.GetByRoleCategoryInTenantAsync("Management", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolioManager, portfolioWithoutView, portfolioWithoutApproval })
            .ReturnsAsync(new[] { programManager, programWithoutView, programWithoutApproval });

        var effectivePermissions = new Mock<IUserEffectivePermissionService>();
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(portfolioManager.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve", "Portfolio.View" });
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(portfolioWithoutView.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve" });
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(portfolioWithoutApproval.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Portfolio.View" });
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(programManager.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Program.View", "Handover.Approve" });
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(programWithoutView.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Handover.Approve" });
        effectivePermissions.Setup(item => item.GetEffectivePermissionsAsync(programWithoutApproval.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal) { "Program.View" });

        var controller = new UsersController(repository.Object, Mock.Of<IUserAdministrationService>(), effectivePermissions.Object)
        {
            ControllerContext = ManagerCandidateControllerContext(tenantId)
        };

        var portfolioResult = await controller.GetPortfolioManagerCandidates(CancellationToken.None);
        var programResult = await controller.GetProgramManagerCandidates(CancellationToken.None);
        var portfolioCandidates = Assert.IsType<List<WorkflowUserLookupDto>>(Assert.IsType<OkObjectResult>(portfolioResult).Value);
        var programCandidates = Assert.IsType<List<WorkflowUserLookupDto>>(Assert.IsType<OkObjectResult>(programResult).Value);

        Assert.Equal([new WorkflowUserLookupDto(portfolioManager.Id, "Portfolio Manager")], portfolioCandidates);
        Assert.Equal([new WorkflowUserLookupDto(programManager.Id, "Program Manager")], programCandidates);
        Assert.Equal(
            new[] { nameof(WorkflowUserLookupDto.Id), nameof(WorkflowUserLookupDto.FullName) },
            typeof(WorkflowUserLookupDto).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("private@example.test", System.Text.Json.JsonSerializer.Serialize(portfolioCandidates));
        Assert.DoesNotContain("private@example.test", System.Text.Json.JsonSerializer.Serialize(programCandidates));
        repository.Verify(item => item.GetByRoleCategoryInTenantAsync("Management", tenantId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(item => item.GetActiveInTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(item => item.GetByRoleNameInTenantAsync("program.manager", It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(item => item.GetByRoleNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(item => item.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
        effectivePermissions.Verify(item => item.GetEffectivePermissionsAsync(portfolioManager.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(item => item.GetEffectivePermissionsAsync(portfolioWithoutView.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(item => item.GetEffectivePermissionsAsync(portfolioWithoutApproval.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(item => item.GetEffectivePermissionsAsync(programManager.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(item => item.GetEffectivePermissionsAsync(programWithoutView.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        effectivePermissions.Verify(item => item.GetEffectivePermissionsAsync(programWithoutApproval.Id, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }
    private static ClaimsPrincipal PrincipalWith(string permission) => PrincipalWith([permission]);

    private static ClaimsPrincipal PrincipalWith(IEnumerable<string> permissions) =>
        new(new ClaimsIdentity(permissions.Select(permission => new Claim("permission", permission)), "test"));

    private static ControllerContext ManagerCandidateControllerContext(Guid tenantId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", tenantId.ToString())],
                "Test"))
        }
    };
    private static ControllerContext ProjectCreateControllerContext(Guid? tenantId = null)
    {
        var claims = new List<Claim> { new("permission", "Project.Create") };
        if (tenantId.HasValue)
            claims.Add(new Claim("tenantId", tenantId.Value.ToString()));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };
    }
}
