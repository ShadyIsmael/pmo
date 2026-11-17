// Canonical permission names as emitted by the API (`/api/auth/me` and JWT claims).
// Each value is the `{Resource}.{Action}` pair stored in the `Permissions` table and
// returned verbatim by the backend. Keep these in sync with NEXUS.Persistence seed data.

export const Permissions = {
  Resources: {
    View: "Resource.View",
    Assign: "Resource.Assign",
    Create: "Resource.Create",
    Edit: "Resource.Edit",
    Delete: "Resource.Delete",
    ViewCostRate: "Resource.ViewCostRate",
    EditCostRate: "Resource.EditCostRate",
  },
  Project: {
    View: "Project.View",
    Create: "Project.Create",
    Edit: "Project.Edit",
    Delete: "Project.Delete",
  },
  ProjectPhase: {
    Change: "ProjectPhase.Change",
  },
  ProjectLifecyclePhase: {
    View: "ProjectLifecyclePhase.View",
    Create: "ProjectLifecyclePhase.Create",
    Edit: "ProjectLifecyclePhase.Edit",
    ActivateDeactivate: "ProjectLifecyclePhase.ActivateDeactivate",
  },
  ProjectLifecycleStatus: {
    View: "ProjectLifecycleStatus.View",
    Create: "ProjectLifecycleStatus.Create",
    Edit: "ProjectLifecycleStatus.Edit",
    ActivateDeactivate: "ProjectLifecycleStatus.ActivateDeactivate",
  },
  ProjectStatus: {
    Change: "ProjectStatus.Change",
  },
  ProjectSettings: {
    View: "ProjectSettings.View",
    Edit: "ProjectSettings.Edit",
  },
  Issue: {
    View: "Issue.View",
  },
  Plugin: {
    View: "Plugin.View",
  },
  StatusReport: {
    View: "StatusReport.View",
    Create: "StatusReport.Create",
    Edit: "StatusReport.Edit",
    Publish: "StatusReport.Publish",
    Export: "StatusReport.Export",
    ManageConfiguration: "StatusReport.ManageConfiguration",
  },
  Team: {
    View: "Team.View",
    Create: "Team.Create",
    Edit: "Team.Edit",
    Delete: "Team.Delete",
  },
  Stakeholders: {
    Read: "Stakeholders.Read",
    Manage: "Stakeholders.Manage",
  },
  ChangeRequest: {
    View: "ChangeRequest.View",
    Manage: "ChangeRequest.Manage",
    Approve: "ChangeRequest.Approve",
  },
  Handover: {
    View: "Handover.View",
    Manage: "Handover.Manage",
    AdminInitiate: "Handover.AdminInitiate",
    Approve: "Handover.Approve",
  },
  Risk: {
    View: "Risk.View",
    Create: "Risk.Create",
    Edit: "Risk.Edit",
    Delete: "Risk.Delete",
    Mitigate: "Risk.Mitigate",
  },
  Planning: {
    View: "Planning.View",
    Edit: "Planning.Edit",
    Lifecycle: "Planning.Lifecycle",
  },
  ProjectPlan: {
    Create: "ProjectPlan.Create",
  },
  Portfolio: {
    View: "Portfolio.View",
    Create: "Portfolio.Create",
    Edit: "Portfolio.Edit",
    Archive: "Portfolio.Archive",
    Reactivate: "Portfolio.Reactivate",
  },
  Program: {
    View: "Program.View",
    Create: "Program.Create",
    CreateAcrossPortfolios: "Program.CreateAcrossPortfolios",
    Edit: "Program.Edit",
    Archive: "Program.Archive",
    Reactivate: "Program.Reactivate",
  },
  WorkItem: {
    View: "WorkItem.View",
    Create: "WorkItem.Create",
    Edit: "WorkItem.Edit",
    Delete: "WorkItem.Delete",
  },
  Meeting: {
    View: "Meeting.View",
    Create: "Meeting.Create",
    Edit: "Meeting.Edit",
    Manage: "Meeting.Manage",
    Delete: "Meeting.Delete",
  },
  TimeTracking: {
    ViewOwn: "TimeTracking.ViewOwn",
    ManageOwn: "TimeTracking.ManageOwn",
    ViewTeam: "TimeTracking.ViewTeam",
    ManageTeam: "TimeTracking.ManageTeam",
  },
  Document: {
    View: "Document.View",
    Create: "Document.Create",
    Edit: "Document.Edit",
    Archive: "Document.Archive",
    TemplateManage: "Document.TemplateManage",
  },
  DocumentCategory: {
    View: "DocumentCategory.View",
    Create: "DocumentCategory.Create",
    Edit: "DocumentCategory.Edit",
    ActivateDeactivate: "DocumentCategory.ActivateDeactivate",
  },
  Users: {
    View: "Users.View",
    ResetPassword: "Users.ResetPassword",
    ManageAccounts: "Users.ManageAccounts",
    SetStatus: "Users.SetStatus",
    AssignRoles: "Users.AssignRoles",
    ManageOverrides: "UserPermissions.Manage",
  },
  Roles: {
    View: "Roles.View",
    Create: "Roles.Create",
    EditDetails: "Roles.EditDetails",
    ConfigurePermissions: "Roles.ConfigurePermissions",
    Delete: "Roles.Delete",
  },
  TenantAdministration: {
    ManageScope: "TenantAdministration.ManageScope",
  },
  ProjectFinance: {
    View: "ProjectFinance.View",
  },
  ProjectFinancialFoundation: {
    View: "ProjectFinancialFoundation.View",
  },
  ProgramFinancialRollup: {
    View: "ProgramFinancialRollup.View",
  },
  PortfolioFinancialRollup: {
    View: "PortfolioFinancialRollup.View",
  },
  CostUtilization: {
    Export: "CostUtilization.Export",
  },
  Budget: {
    View: "Budget.View",
    Create: "Budget.Create",
    Edit: "Budget.Edit",
    Delete: "Budget.Delete",
  },
  Cost: {
    View: "Cost.View",
    Create: "Cost.Create",
    Edit: "Cost.Edit",
    Delete: "Cost.Delete",
  },
  Commitment: {
    View: "Commitment.View",
    Create: "Commitment.Create",
    Edit: "Commitment.Edit",
    Close: "Commitment.Close",
    Cancel: "Commitment.Cancel",
    Delete: "Commitment.Delete",
  },
  PaymentObligation: {
    View: "PaymentObligation.View",
    Create: "PaymentObligation.Create",
    Edit: "PaymentObligation.Edit",
    Delete: "PaymentObligation.Delete",
  },
  PaymentObligationSettlement: {
    Record: "PaymentObligationSettlement.Record",
  },
  ProjectAnnualBudget: {
    View: "ProjectAnnualBudget.View",
    Create: "ProjectAnnualBudget.Create",
    EditDraft: "ProjectAnnualBudget.EditDraft",
    ConfigureDraftApproval: "ProjectAnnualBudget.ConfigureDraftApproval",
    SubmitForReview: "ProjectAnnualBudget.SubmitForReview",
    Delete: "ProjectAnnualBudget.Delete",
    Withdraw: "ProjectAnnualBudget.Withdraw",
    Approve: "ProjectAnnualBudget.Approve",
    ReviseApproved: "ProjectAnnualBudget.ReviseApproved",
    ManageApprovalConfiguration: "ProjectAnnualBudget.ManageApprovalConfiguration",
    Close: "ProjectAnnualBudget.Close",
    CloseApprove: "ProjectAnnualBudget.CloseApprove",
    CarryForward: "ProjectAnnualBudget.CarryForward",
  },
  ProjectPaymentRequest: {
    View: "ProjectPaymentRequest.View",
    Create: "ProjectPaymentRequest.Create",
    EditDraft: "ProjectPaymentRequest.EditDraft",
    Submit: "ProjectPaymentRequest.Submit",
    Cancel: "ProjectPaymentRequest.Cancel",
    Approve: "ProjectPaymentRequest.Approve",
  },
  ProjectApproval: {
    Override: "ProjectApproval.Override",
  },
  BudgetRevision: {
    View: "BudgetRevision.View",
    Create: "BudgetRevision.Create",
    EditDraft: "BudgetRevision.EditDraft",
    SubmitForReview: "BudgetRevision.SubmitForReview",
    Approve: "BudgetRevision.Approve",
    Cancel: "BudgetRevision.Cancel",
    Delete: "BudgetRevision.Delete",
  },
  ProjectFinancialCeiling: {
    ApproveChange: "ProjectFinancialCeiling.ApproveChange",
  },
  ProjectSettlement: {
    View: "ProjectSettlement.View",
  },
  ProjectAnnualBudgetClosing: {
    View: "ProjectAnnualBudgetClosing.View",
  },
  ProjectFinancialAuditTrail: {
    View: "ProjectFinancialAuditTrail.View",
  },
  PaymentSettlement: {
    Record: "PaymentSettlement.Record",
  },
  ProjectPaymentReversal: {
    Create: "ProjectPaymentReversal.Create",
    Approve: "ProjectPaymentReversal.Approve",
  },
  Report: {
    View: "Report.View",
  },
  Dashboard: {
    View: "Dashboard.View",
  },
  Settings: {
    View: "Settings.View",
    ManageTenantConfiguration: "Settings.ManageTenantConfiguration",
  },
  OrganizationConfiguration: {
    View: "OrganizationConfiguration.View",
    Manage: "OrganizationConfiguration.Manage",
  },
  EmailNotifications: {
    View: "EmailNotifications.View",
    Manage: "EmailNotifications.Manage",
    Test: "EmailNotifications.Test",
    Logs: "EmailNotifications.Logs",
  },
  ReferenceData: {
    View: "ReferenceData.View",
    Create: "ReferenceData.Create",
    Edit: "ReferenceData.Edit",
    Delete: "ReferenceData.Delete",
  },
  Directory: {
    View: "Directory.View",
    TestConnection: "Directory.TestConnection",
    PreviewSync: "Directory.PreviewSync",
    ExecuteSync: "Directory.ExecuteSync",
    ManageMappings: "Directory.ManageMappings",
    ManageProvisioningPolicy: "Directory.ManageProvisioningPolicy",
    ManageConnection: "Directory.ManageConnection",
  },
  Workflow: {
    View: "Workflow.View",
    Manage: "Workflow.Manage",
    Publish: "Workflow.Publish",
  },
  AssignmentRecovery: {
    View: "Governance.AssignmentRecovery.View",
    Initiate: "Governance.AssignmentRecovery.Initiate",
    Manage: "Governance.AssignmentRecovery.Manage",
  },
  Audit: {
    View: "Audit.View",
  },
} as const;

export const DirectoryAccessPermissions = [Permissions.Directory.View];
export const ReferenceDataAccessPermissions = [Permissions.ReferenceData.View];
export const ProjectLifecycleCatalogAccessPermissions = [
  Permissions.ProjectLifecyclePhase.View,
  Permissions.ProjectLifecyclePhase.Create,
  Permissions.ProjectLifecyclePhase.Edit,
  Permissions.ProjectLifecyclePhase.ActivateDeactivate,
  Permissions.ProjectLifecycleStatus.View,
  Permissions.ProjectLifecycleStatus.Create,
  Permissions.ProjectLifecycleStatus.Edit,
  Permissions.ProjectLifecycleStatus.ActivateDeactivate,
];

export type Permission = (typeof Permissions)[keyof typeof Permissions][keyof typeof Permissions[keyof typeof Permissions]];

export const Roles = {
  Executive: "Executive",
  PortfolioManager: "Portfolio Manager",
  ProgramManager: "Program Manager",
  ProjectManager: "Project Manager",
  TeamLead: "Team Lead",
  TeamMember: "Team Member",
  PMODirector: "PMO Director",
  PMOAnalyst: "PMO Analyst",
  ResourceManager: "Resource Manager",
  FinancialController: "Financial Controller",
  RiskManager: "Risk Manager",
  Administrator: "Administrator",
  Auditor: "Auditor",
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

export function getAllPermissions(): string[] {
  const all: string[] = [];
  for (const category of Object.values(Permissions)) {
    for (const perm of Object.values(category)) {
      all.push(perm);
    }
  }
  return all;
}

export function hasPermission(userPermissions: string[], permission: string): boolean {
  return userPermissions.includes(permission);
}

export function hasAnyPermission(userPermissions: string[], permissions: string[]): boolean {
  return permissions.some((permission) => hasPermission(userPermissions, permission));
}
