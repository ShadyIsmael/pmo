using Microsoft.AspNetCore.Authorization;
using NEXUS.Modules.ProjectManagement.Api;

namespace NEXUS.Api.Authorization;

public static class WorkflowLookupPolicyRegistration
{
    public const string ProjectPortfolios = "WorkflowLookup.Project.Portfolios";
    public const string ProjectPrograms = "WorkflowLookup.Project.Programs";
    public const string ProjectOrganizationalUnits = "WorkflowLookup.Project.OrganizationalUnits";
    public const string ProjectCurrencies = "WorkflowLookup.Project.Currencies";
    public const string ProjectUsers = "WorkflowLookup.Project.Users";
    public const string ResourceOrganizationalUnits = "WorkflowLookup.Resource.OrganizationalUnits";
    public const string ResourceBaseCalendars = "WorkflowLookup.Resource.BaseCalendars";
    public const string ResourceReferenceData = "WorkflowLookup.Resource.ReferenceData";
    public const string ResourceVendors = "WorkflowLookup.Resource.Vendors";
    public const string BusinessCurrencies = "WorkflowLookup.Business.Currencies";
    public const string MeetingUsers = "WorkflowLookup.Meeting.Users";
    public const string MeetingProjects = "WorkflowLookup.Meeting.Projects";
    public const string WorkItemUsers = "WorkflowLookup.WorkItem.Users";
    public const string RiskUsers = "WorkflowLookup.Risk.Users";
    public const string ResourceUsers = "WorkflowLookup.Resource.Users";
    public const string ManagerAssignmentUsers = "WorkflowLookup.Handover.ManagerCandidates";
    public const string ProgramCreatePortfolios = "WorkflowLookup.ProgramCreate.Portfolios";
    public const string ProgramEditPortfolios = "WorkflowLookup.ProgramEdit.Portfolios";
    public const string PortfolioManagerCandidates = "WorkflowLookup.Portfolio.Managers";
    public const string ProgramManagerCandidates = "WorkflowLookup.Program.Managers";
    public const string PaymentObligationProjects = PaymentObligationProjectLookupController.PolicyName;

    public static void AddWorkflowLookupPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        AddLookupPolicy(options, ProjectPortfolios, "Portfolio.View", "Project.Create", "Project.Edit");
        AddLookupPolicy(options, ProjectPrograms, "Program.View", "Project.Create", "Project.Edit");
        AddLookupPolicy(options, ProjectOrganizationalUnits, "Settings.View", "Project.Create", "Project.Edit");
        AddLookupPolicy(options, ProjectCurrencies, "ReferenceData.View", "Project.Create", "Project.Edit");
        AddLookupPolicy(options, ProjectUsers, "Project.View", "Project.Create", "Project.Edit");
        AddLookupPolicy(options, ResourceOrganizationalUnits, "Settings.View", "Resource.View", "Resource.Create", "Resource.Edit");
        AddLookupPolicy(options, ResourceBaseCalendars, "Settings.View", "Resource.View", "Resource.Create", "Resource.Edit");
        AddLookupPolicy(options, ResourceReferenceData, "Resource.View", "Resource.Create", "Resource.Edit");
        AddLookupPolicy(options, ResourceVendors, "Resource.View", "Resource.Create", "Resource.Edit");
        AddLookupPolicy(options, BusinessCurrencies, "ReferenceData.View", "Portfolio.Create", "Portfolio.Edit", "Program.Create", "Program.Edit", "Resource.View", "Resource.Create", "Resource.Edit", "Resource.ViewCostRate", "Resource.EditCostRate");
        AddLookupPolicy(options, MeetingUsers, "Meeting.View", "Meeting.Create", "Meeting.Edit", "Meeting.Manage");
        AddLookupPolicy(options, WorkItemUsers, "WorkItem.View", "WorkItem.Create", "WorkItem.Edit");
        AddLookupPolicy(options, RiskUsers, "Risk.View", "Risk.Create", "Risk.Edit");
        AddLookupPolicy(options, ResourceUsers, "Resource.View", "Resource.Create", "Resource.Edit");
        AddLookupPolicy(options, ManagerAssignmentUsers, "Handover.AdminInitiate");
        AddOperationLookupPolicy(options, ProgramCreatePortfolios, "Program.Create");
        AddOperationLookupPolicy(options, ProgramEditPortfolios, "Program.Edit");
        AddLookupPolicy(options, MeetingProjects, "Meeting.View", "Meeting.Create", "Meeting.Edit");
        AddOperationLookupPolicy(options, PortfolioManagerCandidates, "Portfolio.Create", "Portfolio.Edit", "Handover.AdminInitiate");
        AddOperationLookupPolicy(options, ProgramManagerCandidates, "Program.Create", "Program.Edit", "Handover.AdminInitiate");
        AddOperationLookupPolicy(options, PaymentObligationProjects, "PaymentObligation.View", "PaymentObligation.Create", "PaymentObligation.Edit");
    }

    private static void AddOperationLookupPolicy(AuthorizationOptions options, string policyName, params string[] operationPermissions)
    {
        options.AddPolicy(policyName, policy => policy.RequireAssertion(context =>
            operationPermissions.Any(permission => HasPermission(context.User, permission))));
    }

    private static void AddLookupPolicy(AuthorizationOptions options, string policyName, string viewPermission, params string[] operationPermissions)
    {
        options.AddPolicy(policyName, policy => policy.RequireAssertion(context =>
            HasPermission(context.User, viewPermission)
            || operationPermissions.Any(permission => HasPermission(context.User, permission))));
    }

    private static bool HasPermission(System.Security.Claims.ClaimsPrincipal principal, string permission) =>
        principal.HasClaim("permission", permission);
}
