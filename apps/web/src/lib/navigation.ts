import {
  BadgeCheck, BarChart3, Bell, BriefcaseBusiness,
  Building2, CalendarDays, BarChart3 as ChartNoAxesColumn, CircleAlert, ClipboardCheck,
  Coins, FolderKanban, Home, LayoutGrid, Layers3,
  FileText, ListTodo, Receipt, Settings2, ShieldCheck, SquareCheck,
  TriangleAlert, TrendingUp, UsersRound, GitBranch, type LucideIcon,
} from "lucide-react";
import { Permissions } from "@/lib/permissions";

export type NavigationReadiness = "ready" | "context" | "deferred";
export type NavigationMatch = "exact" | "prefix";

export interface NavItem {
  id: string;
  labelKey: string;
  href: string;
  icon: LucideIcon;
  items?: NavItem[];
  requiredPermission?: string;
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

const item = (config: Omit<NavItem, "match"> & { match?: NavigationMatch }): NavItem => ({ match: "prefix", ...config });

/** The single source of truth for enterprise-level navigation. */
export const navigationConfig: NavSection[] = [
  { id: "home", labelKey: "sections.home", icon: Home, order: 10, items: [
    item({ id: "home", labelKey: "home", href: "/home", icon: Home, match: "exact" }),
    item({ id: "my-work", labelKey: "myWork", href: "/my-work", icon: ClipboardCheck }),
    item({ id: "notifications", labelKey: "notifications", href: "/notifications", icon: Bell }),
  ] },
  { id: "delivery-management", labelKey: "sections.deliveryManagement", icon: BriefcaseBusiness, order: 20, items: [
    item({ id: "projects", labelKey: "projects", href: "/projects", icon: FolderKanban, requiredPermission: Permissions.Projects.Read }),
    item({ id: "programs", labelKey: "programs", href: "/portfolios/programs", icon: Layers3, requiredPermission: Permissions.Program.View }),
    item({ id: "portfolios", labelKey: "portfolios", href: "/portfolios", icon: LayoutGrid, requiredPermission: Permissions.Portfolio.View }),
    item({ id: "meetings", labelKey: "meetings", href: "/meetings", icon: CalendarDays, requiredPermission: Permissions.Meeting.View }),
  ] },
  { id: "work-management", labelKey: "sections.workManagement", icon: ListTodo, order: 30, items: [
    item({ id: "work-items", labelKey: "workItems", href: "/work-items", icon: ListTodo, requiredPermission: Permissions.WorkItem.View }),
    item({ id: "my-tasks", labelKey: "tasks", href: "/my-work/tasks", icon: SquareCheck, requiredPermission: Permissions.WorkItem.View }),
    item({ id: "issues", labelKey: "issues", href: "/risks-issues/issues", icon: CircleAlert, requiredPermission: Permissions.WorkItem.View }),
  ] },
  { id: "resources", labelKey: "sections.resources", icon: UsersRound, order: 40, items: [
    item({ id: "resources", labelKey: "peopleResources", href: "/resources", icon: UsersRound, requiredPermission: Permissions.Resources.View }),
    item({ id: "capacity", labelKey: "capacity", href: "/resources/capacity", icon: ChartNoAxesColumn, requiredPermission: Permissions.Resources.View }),
    item({ id: "cost-utilization", labelKey: "costUtilization", href: "/resources/cost-utilization", icon: Coins, requiredPermission: Permissions.Resources.View }),
    item({ id: "vendors", labelKey: "vendors", href: "/resources/vendors", icon: Building2, requiredPermission: Permissions.Resources.View }),
  ] },
  { id: "financial-management", labelKey: "sections.financialManagement", icon: Coins, order: 50, items: [
    item({ id: "financial-overview", labelKey: "financialOverview", href: "/costs", icon: BarChart3, requiredPermission: Permissions.Finances.Read }),
    item({ id: "budget-allocations", labelKey: "budgets", href: "/costs/budgets", icon: Coins, requiredPermission: Permissions.Finances.Read }),
    item({ id: "actual-costs", labelKey: "actuals", href: "/costs/actuals", icon: Receipt, requiredPermission: Permissions.Finances.Read }),
    item({ id: "forecasts", labelKey: "forecasts", href: "/costs/forecasts", icon: TrendingUp, requiredPermission: Permissions.Finances.Read }),
  ] },
  { id: "governance", labelKey: "sections.governance", icon: ShieldCheck, order: 60, items: [
    item({ id: "risks", labelKey: "risks", href: "/risks-issues/risks", icon: TriangleAlert, requiredPermission: Permissions.Risk.View }),
    item({ id: "documents", labelKey: "documentCenter", href: "/documents", icon: FileText, requiredPermission: Permissions.Document.View }),
    item({ id: "approvals", labelKey: "approvals", href: "/my-work/approvals", icon: BadgeCheck }),
  ] },
  { id: "reports-insights", labelKey: "sections.reportsInsights", icon: BarChart3, order: 70, items: [] },
  { id: "administration", labelKey: "sections.administration", icon: Settings2, order: 80, items: [
    item({ id: "users", labelKey: "users", href: "/administration/users", icon: UsersRound, requiredPermission: Permissions.Users.Read }),
    item({ id: "reference-data", labelKey: "referenceData", href: "/administration/reference-data/work-item-statuses", icon: ListTodo, requiredPermission: Permissions.Settings.Read }),
    item({ id: "audit-log", labelKey: "auditLog", href: "/security-audit/audit-log", icon: ShieldCheck, requiredPermission: Permissions.Settings.Read }),
    item({ id: "settings", labelKey: "settings", href: "/administration/settings", icon: Settings2, requiredPermission: Permissions.Settings.Read }),
    item({ id: "workflow-management", labelKey: "workflowManagement", href: "/workflows", icon: GitBranch, requiredPermission: Permissions.Workflow.View }),
  ] },
];

export const deferredNavigation = [
  { id: "allocations", labelKey: "allocations", reason: "No dedicated allocations route exists." },
  { id: "leave", labelKey: "leaveAvailability", reason: "No dedicated availability route exists." },
  { id: "analytics", labelKey: "analytics", reason: "No production-ready analytics route exists." },
  { id: "change-requests", labelKey: "changeRequests", reason: "Change requests are project-context operations." },
  { id: "status-reporting", labelKey: "statusReporting", reason: "Status reporting is project-context functionality." },
  { id: "handovers", labelKey: "handovers", reason: "Handovers are context-specific workflows." },
  { id: "reports", labelKey: "reports", reason: "The current Reports page uses static placeholder data." },
  { id: "integrations", labelKey: "integrations", reason: "The current Integration Hub uses static placeholder data." },
  { id: "global-commitments", labelKey: "commitments", reason: "Commitments are currently project-context financials." },
];
