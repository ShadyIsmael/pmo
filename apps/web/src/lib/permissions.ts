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
