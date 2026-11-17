import {
  Home, FolderKanban, Layers3, LayoutGrid, CalendarDays, ListTodo, SquareCheck, CircleAlert,
  Users, UsersRound, Building2, BarChart3, Coins, TriangleAlert,
  FileText, Settings2, ShieldCheck, BriefcaseBusiness, BadgeCheck, ClipboardCheck,
  GitBranch, ListFilter, Plug, Shield, Workflow as WorkflowIcon, User as UserIcon,
  type LucideIcon,
} from "lucide-react";
import { DirectoryAccessPermissions, Permissions, ProjectLifecycleCatalogAccessPermissions } from "./permissions";

export type NavigationReadiness = "ready" | "context" | "deferred";
export type NavigationMatch = "exact" | "prefix";

export interface NavItem {
  id: string;
  labelKey: string;
  href: string;
  icon: LucideIcon;
  items?: NavItem[];
  requiredPermission?: string;
  requiredAnyPermission?: string[];
  requiredPlatformAccess?: boolean;
  requiredOrganizationAccess?: boolean;
  requiredTenantAccess?: boolean;
  requiredOrganizationAccessForPermissions?: string[];
  requiredPlugin?: string;
  readiness?: NavigationReadiness;
  match?: NavigationMatch;
}

export interface NavSection {
  id: string;
  labelKey: string;
  icon: LucideIcon;
  items: NavItem[];
  requiredPermission?: string;
  order?: number;
}

const item = (config: Omit<NavItem, "match"> & { match?: NavigationMatch }): NavItem => ({
  match: "prefix",
  ...config,
});

export interface AdministrationCategory {
  id: string;
  href: string;
  labelKey: string;
  icon: LucideIcon;
  requiredPermissions: string[];
  requiredPlatformAccess?: boolean;
  requiredOrganizationAccess?: boolean;
  requiredTenantAccess?: boolean;
  requiredOrganizationAccessForPermissions?: string[];
}

export const administrationCategoryConfig: AdministrationCategory[] = [
  { id: "organization", href: "/administration/organization", labelKey: "nav.organization", icon: Building2, requiredPermissions: [Permissions.Settings.View], requiredTenantAccess: true },
  { id: "tenant-settings", href: "/administration/tenant-settings", labelKey: "nav.tenantSettings", icon: Settings2, requiredPermissions: [Permissions.Settings.View], requiredTenantAccess: true },
  { id: "organization-settings", href: "/administration/organization-settings", labelKey: "nav.organizationSettings", icon: Building2, requiredPermissions: [Permissions.Settings.View, Permissions.OrganizationConfiguration.View], requiredOrganizationAccess: true },
  { id: "access", href: "/administration/access", labelKey: "nav.accessSecurity", icon: Shield, requiredPermissions: [Permissions.Users.View, Permissions.Users.ManageAccounts, Permissions.Users.SetStatus, Permissions.Users.AssignRoles, Permissions.Users.ManageOverrides, Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete] },
  { id: "reference-data", href: "/administration/reference-data", labelKey: "nav.referenceData", icon: ListFilter, requiredPermissions: [Permissions.ReferenceData.View, Permissions.DocumentCategory.View, ...ProjectLifecycleCatalogAccessPermissions, Permissions.OrganizationConfiguration.View, Permissions.OrganizationConfiguration.Manage], requiredOrganizationAccessForPermissions: [Permissions.OrganizationConfiguration.View, Permissions.OrganizationConfiguration.Manage] },
  { id: "platform-settings", href: "/administration/settings", labelKey: "nav.platformSettings", icon: Settings2, requiredPermissions: [], requiredPlatformAccess: true },
  { id: "integrations", href: "/administration/integrations", labelKey: "nav.integrations", icon: Plug, requiredPermissions: [Permissions.Settings.View, ...DirectoryAccessPermissions] },
  { id: "governance", href: "/administration/governance", labelKey: "nav.governance", icon: GitBranch, requiredPermissions: [Permissions.Settings.View, Permissions.Workflow.View, Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage] },
];

/** Single source of truth for enterprise navigation. */
export const navigationConfig: NavSection[] = [
  {
    id: "home",
    labelKey: "nav.sections.home",
    icon: Home,
    order: 10,
    items: [
      item({ id: "home", labelKey: "nav.home", href: "/home", icon: Home, match: "exact", requiredPermission: Permissions.Dashboard.View }),
      item({ id: "my-work", labelKey: "nav.myWork", href: "/my-work", icon: ClipboardCheck }),
      item({ id: "approvals", labelKey: "nav.approvals", href: "/my-work/approvals", icon: BadgeCheck }),
    ],
  },
  {
    id: "delivery-management",
    labelKey: "nav.sections.deliveryManagement",
    icon: BriefcaseBusiness,
    order: 20,
    items: [
      item({ id: "projects", labelKey: "nav.projects", href: "/projects", icon: FolderKanban, requiredPermission: Permissions.Project.View }),
      item({ id: "programs", labelKey: "nav.programs", href: "/portfolios/programs", icon: Layers3, requiredPermission: Permissions.Program.View }),
      item({ id: "portfolios", labelKey: "nav.portfolios", href: "/portfolios", icon: LayoutGrid, requiredPermission: Permissions.Portfolio.View }),
      item({ id: "meetings", labelKey: "nav.meetings", href: "/meetings", icon: CalendarDays, requiredPermission: Permissions.Meeting.View }),
    ],
  },
  {
    id: "work-management",
    labelKey: "nav.sections.workManagement",
    icon: ListTodo,
    order: 30,
    items: [
      item({ id: "work-items", labelKey: "nav.workItems", href: "/work-items", icon: ListTodo, requiredPermission: Permissions.WorkItem.View }),
      item({ id: "my-tasks", labelKey: "nav.tasks", href: "/my-work/tasks", icon: SquareCheck, requiredPermission: Permissions.WorkItem.View }),
      item({ id: "issues", labelKey: "nav.issues", href: "/risks-issues/issues", icon: CircleAlert, requiredPermission: Permissions.Issue.View }),
    ],
  },
  {
    id: "resources",
    labelKey: "nav.sections.resources",
    icon: UsersRound,
    order: 40,
    items: [
      item({ id: "resources", labelKey: "nav.peopleResources", href: "/resources", icon: UsersRound, requiredPermission: Permissions.Resources.View }),
      item({ id: "capacity", labelKey: "nav.capacity", href: "/resources/capacity", icon: BarChart3, requiredPermission: Permissions.Resources.View }),
      item({ id: "cost-utilization", labelKey: "nav.costUtilization", href: "/resources/cost-utilization", icon: Coins, requiredPermission: Permissions.Resources.View }),
      item({ id: "vendors", labelKey: "nav.vendors", href: "/resources/vendors", icon: Building2, requiredPermission: Permissions.Resources.View }),
      item({ id: "teams", labelKey: "nav.teams", href: "/resources/teams", icon: Users, requiredPermission: Permissions.Team.View }),
    ],
  },
  {
    id: "governance",
    labelKey: "nav.sections.governance",
    icon: ShieldCheck,
    order: 60,
    items: [
      item({ id: "risks", labelKey: "nav.risks", href: "/risks-issues/risks", icon: TriangleAlert, requiredPermission: Permissions.Risk.View }),
      item({ id: "documents", labelKey: "nav.documentCenter", href: "/documents", icon: FileText, requiredPermission: Permissions.Document.View }),
      item({ id: "workflows", labelKey: "nav.workflowManagement", href: "/workflows", icon: WorkflowIcon, requiredPermission: Permissions.Workflow.View }),
    ],
  },
  {
    id: "account",
    labelKey: "nav.sections.account",
    icon: UserIcon,
    order: 70,
    items: [
      item({ id: "my-account", labelKey: "nav.myAccount", href: "/account", icon: UserIcon }),
      item({ id: "preferences", labelKey: "nav.preferences", href: "/account/preferences", icon: Settings2 }),
    ],
  },
  {
    id: "administration",
    labelKey: "nav.sections.administration",
    icon: Settings2,
    order: 80,
    items: administrationCategoryConfig.map(({ id, href, labelKey, icon, requiredPermissions, requiredPlatformAccess, requiredOrganizationAccess, requiredTenantAccess, requiredOrganizationAccessForPermissions }) =>
      item({
        id,
        href,
        labelKey,
        icon,
        requiredAnyPermission: requiredPermissions.length > 0 ? requiredPermissions : undefined,
        requiredPlatformAccess,
        requiredOrganizationAccess,
        requiredTenantAccess,
        requiredOrganizationAccessForPermissions,
      })
    ),
  },
];

export const deferredNavigation = [
  { id: "allocations", labelKey: "nav.allocations", reason: "No dedicated allocations route exists." },
  { id: "leave", labelKey: "nav.leaveAvailability", reason: "No dedicated availability route exists." },
  { id: "analytics", labelKey: "nav.analytics", reason: "No production-ready analytics route exists." },
  { id: "change-requests", labelKey: "nav.changeRequests", reason: "Change requests are project-context operations." },
  { id: "status-reporting", labelKey: "nav.statusReporting", reason: "Status reporting is project-context functionality." },
  { id: "handovers", labelKey: "nav.handovers", reason: "Handovers are context-specific workflows." },
  { id: "global-commitments", labelKey: "nav.commitments", reason: "Commitments are currently project-context financials." },
];

export function filterSections(sections: NavSection[], permissions: string[], canReadPlatform = false, canReadOrganizations = false, canReadTenant = false): NavSection[] {
  // If no permissions loaded yet (still bootstrapping), show unrestricted items so shell never looks empty.
  return sections
    .map((section) => {
      const visibleItems = section.items.filter((navItem) => {
        if (navItem.requiredPermission && !permissions.includes(navItem.requiredPermission)) return false;
        if (navItem.requiredAnyPermission && !navItem.requiredAnyPermission.some((p) => permissions.includes(p))) return false;
        if (navItem.requiredPlatformAccess && !canReadPlatform) return false;
        if (navItem.requiredOrganizationAccess && !canReadOrganizations) return false;
        if (navItem.requiredTenantAccess && !canReadTenant) return false;
        const hasScopedOrganizationPermission = navItem.requiredOrganizationAccessForPermissions?.some((permission) => permissions.includes(permission)) ?? false;
        const hasOtherPermission = navItem.requiredAnyPermission?.some((permission) =>
          !navItem.requiredOrganizationAccessForPermissions?.includes(permission) && permissions.includes(permission)) ?? false;
        if (hasScopedOrganizationPermission && !hasOtherPermission && !canReadOrganizations) return false;
        return true;
      });
      if (visibleItems.length === 0) return null;
      return { ...section, items: visibleItems };
    })
    .filter((section): section is NavSection => section !== null);
}

export function normalizePathname(pathname: string): string {
  const parts = pathname.split("/").filter(Boolean);
  if (parts[0] === "en" || parts[0] === "ar") {
    return "/" + parts.slice(1).join("/");
  }
  return pathname || "/";
}

export function itemIsActive(navItem: NavItem, pathname: string): boolean {
  const normalized = normalizePathname(pathname);
  if (navItem.match === "exact") {
    return normalized === navItem.href;
  }
  if (normalized === navItem.href) return true;
  return normalized.startsWith(navItem.href.endsWith("/") ? navItem.href : `${navItem.href}/`);
}

export function findActiveItem(items: NavItem[], pathname: string): NavItem | null {
  let best: NavItem | null = null;
  for (const candidate of items) {
    if (itemIsActive(candidate, pathname)) {
      if (!best || candidate.href.length > best.href.length) best = candidate;
    }
    if (candidate.items) {
      const found = findActiveItem(candidate.items, pathname);
      if (found && (!best || found.href.length > best.href.length)) best = found;
    }
  }
  return best;
}

export function entityRoute(pathname: string): { type: string; id: string; href: string } | null {
  const normalized = normalizePathname(pathname);
  const match = normalized.match(/^\/(projects|programs|portfolios|resources|meetings)\/([^/]+)/);
  if (match) {
    return { type: match[1].slice(0, -1), id: match[2], href: normalized };
  }
  return null;
}
