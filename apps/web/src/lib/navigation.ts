import {
  LayoutDashboard,
  Briefcase,
  FolderKanban,
  Users,
  Banknote,
  AlertTriangle,
  CalendarCheck,
  ClipboardList,
  ShieldCheck,
  Settings,
  Scale,
  Activity,
  Coins,
  type LucideIcon,
} from "lucide-react";
import { Permissions } from "@/lib/permissions";

export interface NavItem {
  id: string;
  labelKey: string;
  href: string;
  icon: LucideIcon;
  items?: NavItem[];
  requiredPermission?: string;
  requiredPlugin?: string;
}

export interface NavSection {
  id: string;
  labelKey: string;
  icon: LucideIcon;
  items: NavItem[];
  requiredPermission?: string;
}

export const navigationConfig: NavSection[] = [
  {
    id: "main",
    labelKey: "sections.main",
    icon: LayoutDashboard,
    items: [
      {
        id: "home",
        labelKey: "home",
        href: "/home",
        icon: LayoutDashboard,
      },
    ],
  },
  {
    id: "portfolio-management",
    labelKey: "sections.portfolioManagement",
    icon: Briefcase,
    items: [
      {
        id: "portfolios",
        labelKey: "portfolios",
        href: "/portfolios",
        icon: Briefcase,
        requiredPermission: Permissions.Portfolios.Read,
      },
      {
        id: "programs",
        labelKey: "programs",
        href: "/portfolios/programs",
        icon: Briefcase,
      },
      {
        id: "projects",
        labelKey: "projects",
        href: "/projects",
        icon: FolderKanban,
        requiredPermission: Permissions.Projects.Read,
      },
    ],
  },
  {
    id: "work-management",
    labelKey: "sections.workManagement",
    icon: ClipboardList,
    items: [
      {
        id: "tasks",
        labelKey: "tasks",
        href: "/my-work/tasks",
        icon: ClipboardList,
      },
      {
        id: "meetings",
        labelKey: "meetings",
        href: "/meetings",
        icon: CalendarCheck,
      },
    ],
  },
  {
    id: "resource-management",
    labelKey: "sections.resourceManagement",
    icon: Users,
    items: [
      {
        id: "resources",
        labelKey: "resources",
        href: "/resources",
        icon: Users,
        requiredPermission: Permissions.Resources.Read,
      },
      {
        id: "capacity",
        labelKey: "capacity",
        href: "/resources/capacity",
        icon: Users,
        requiredPermission: Permissions.Resources.Read,
      },
      {
        id: "utilization",
        labelKey: "utilization",
        href: "/resources/utilization",
        icon: Users,
        requiredPermission: Permissions.Resources.Read,
      },
      {
        id: "outsourced-resources",
        labelKey: "outsourcedResources",
        href: "/resources/outsourced",
        icon: Users,
        requiredPermission: Permissions.Resources.Read,
      },
    ],
  },
  {
    id: "governance",
    labelKey: "sections.governance",
    icon: Scale,
    items: [
      {
        id: "risks",
        labelKey: "risks",
        href: "/risks-issues/risks",
        icon: AlertTriangle,
      },
      {
        id: "issues",
        labelKey: "issues",
        href: "/risks-issues/issues",
        icon: AlertTriangle,
      },
      {
        id: "approvals",
        labelKey: "approvals",
        href: "/my-work/approvals",
        icon: ClipboardList,
      },
    ],
  },
  {
    id: "financial-management",
    labelKey: "sections.financialManagement",
    icon: Banknote,
    items: [
      {
        id: "budgets",
        labelKey: "budgets",
        href: "/costs/budgets",
        icon: Banknote,
        requiredPermission: Permissions.Finances.Read,
      },
      {
        id: "costs",
        labelKey: "costs",
        href: "/costs",
        icon: Banknote,
        requiredPermission: Permissions.Finances.Read,
      },
    ],
  },
  {
    id: "administration",
    labelKey: "sections.administration",
    icon: Settings,
    items: [
      {
        id: "users",
        labelKey: "users",
        href: "/administration/users",
        icon: Settings,
        requiredPermission: Permissions.Users.Read,
      },
      {
        id: "roles",
        labelKey: "roles",
        href: "/administration/roles",
        icon: Settings,
        requiredPermission: Permissions.Users.Read,
      },
      {
        id: "currencies",
        labelKey: "currencies",
        href: "/administration/currencies",
        icon: Coins,
        requiredPermission: Permissions.Settings.Read,
      },
      {
        id: "settings",
        labelKey: "settings",
        href: "/administration/settings",
        icon: Settings,
        requiredPermission: Permissions.Settings.Read,
      },
    ],
  },
  {
    id: "monitoring",
    labelKey: "sections.monitoring",
    icon: Activity,
    items: [
      {
        id: "audit-log",
        labelKey: "auditLog",
        href: "/security-audit/audit-log",
        icon: ShieldCheck,
      },
    ],
  },
];
