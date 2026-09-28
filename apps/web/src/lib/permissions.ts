// Canonical permission names as emitted by the API (`/api/auth/me` and JWT claims).
// Each value is the `{Resource}.{Action}` pair stored in the `Permissions` table and
// returned verbatim by the backend. Keep these in sync with NEXUS.Persistence seed data.

export const Permissions = {
  Projects: {
    Read: "projects.read",
    Write: "projects.write",
    Delete: "projects.delete",
  },
  Portfolios: {
    Read: "portfolios.read",
    Write: "portfolios.write",
  },
  Resources: {
    Read: "resources.read",
    Write: "resources.write",
    View: "Resource.View",
    Assign: "Resource.Assign",
    Allocate: "Resource.Allocate",
    Manage: "Resource.Manage",
    ManageRates: "Resource.ManageRates",
    ViewCostRate: "Resource.ViewCostRate",
    EditCostRate: "Resource.EditCostRate",
  },
  Project: {
    View: "Project.View",
    Create: "Project.Create",
    Edit: "Project.Edit",
    Delete: "Project.Delete",
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
    Manage: "Planning.Manage",
    Lifecycle: "Planning.Lifecycle",
  },
  Portfolio: {
    View: "Portfolio.View",
    Manage: "Portfolio.Manage",
  },
  Program: {
    View: "Program.View",
    Manage: "Program.Manage",
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
  Document: {
    View: "Document.View",
    Create: "Document.Create",
    Edit: "Document.Edit",
    Archive: "Document.Archive",
    TemplateManage: "Document.TemplateManage",
  },
  Users: {
    Read: "users.read",
    Write: "users.write",
  },
  Finances: {
    Read: "finances.read",
    Write: "finances.write",
  },
  Reports: {
    Read: "reports.read",
    Export: "reports.export",
  },
  Settings: {
    Read: "settings.read",
    Write: "settings.write",
  },
  Workflow: {
    View: "Workflow.View",
    Manage: "Workflow.Manage",
    Publish: "Workflow.Publish",
  },
} as const;

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

export function usePermissions(): string[] {
  return getAllPermissions();
}
