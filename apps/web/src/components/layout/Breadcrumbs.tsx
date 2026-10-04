"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronRight, Home } from "lucide-react";
import { cn } from "@/lib/utils";

const routeLabels: Record<string, string> = {
  home: "Home",
  "my-work": "My Work",
  tasks: "Tasks",
  approvals: "Approvals",
  timesheets: "Timesheets",
  portfolios: "Portfolios",
  programs: "Programs",
  optimization: "Optimization",
  projects: "Projects",
  gantt: "Gantt View",
  board: "Board View",
  matrix: "Resource Matrix",
  templates: "Templates",
  resources: "Resources",
  capacity: "Capacity Planning",
  utilization: "Utilization",
  outsourced: "Outsourced",
  costs: "Costs",
  budgets: "Budgets",
  forecasts: "Forecasts",
  actuals: "Actuals",
  "risks-issues": "Risks & Issues",
  risks: "Risk Register",
  issues: "Issue Log",
  meetings: "Meetings",
  "action-items": "Action Items",
  minutes: "Minutes",
  workflows: "Workflows",
  tickets: "Tickets",
  "test-management": "Test Management",
  integrations: "Integrations",
  "ai-workspace": "AI Workspace",
  "ai-copilot": "AI Copilot",
  "plan-generator": "Plan Generator",
  insights: "Insights",
  reports: "Reports",
  dashboard: "Dashboards",
  executive: "Executive Reports",
  custom: "Custom Reports",
  scheduled: "Scheduled Reports",
  documents: "Documents",
  notifications: "Notifications",
  account: "My Account",
  profile: "My Profile",
  preferences: "Preferences",
  security: "Security",
  organization: "Organization",
  access: "Access & Security",
  "reference-data": "Reference Data",
  governance: "Governance",
  plugins: "Plugins",
  administration: "Administration",
  users: "Users",
  roles: "Roles & Permissions",
  settings: "Platform Settings",
  personalization: "Personalization",
  license: "License",
  "security-audit": "Security & Audit",
  "audit-log": "Audit Log",
  policies: "Security Policies",
  "data-classification": "Data Classification",
};

export function Breadcrumbs() {
  const pathname = usePathname();
  const segments = pathname.split("/").filter(Boolean);

  if (segments.length === 0) return null;

  const crumbs = segments.map((segment, index) => {
    const href = "/" + segments.slice(0, index + 1).join("/");
    const label = routeLabels[segment] ?? segment.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
    const isLast = index === segments.length - 1;
    return { href, label, isLast };
  });

  return (
    <nav className="flex items-center gap-1.5 text-xs text-muted-foreground/70">
      <Link
        href="/home"
        className="flex items-center gap-1 hover:text-foreground transition-colors"
      >
        <Home className="h-3 w-3" />
      </Link>
      {crumbs.map((crumb) => (
        <React.Fragment key={crumb.href}>
          <ChevronRight className="h-3 w-3 text-muted-foreground/30" />
          {crumb.isLast ? (
            <span className="font-medium text-foreground/80">{crumb.label}</span>
          ) : (
            <Link
              href={crumb.href}
              className={cn(
                "hover:text-foreground transition-colors",
                crumb.label === "NEXUS PMO" && "hidden"
              )}
            >
              {crumb.label}
            </Link>
          )}
        </React.Fragment>
      ))}
    </nav>
  );
}
