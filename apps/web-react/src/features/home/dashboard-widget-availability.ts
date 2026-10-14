export type DashboardWidgetPointOfView =
  | "Executive"
  | "Project Manager"
  | "Program Manager"
  | "Portfolio Manager"
  | "Team Member"
  | "Resource Manager"
  | "Finance Controller"
  | "PMO Administrator";

export const DASHBOARD_WIDGET_CATEGORIES = [
  "Overview",
  "Projects",
  "Portfolios",
  "My Work",
  "Schedule",
  "Resources",
  "Timesheets",
  "Costs",
  "Risks",
  "Meetings",
  "Approvals",
  "Notifications",
  "Integrations",
  "AI",
] as const;

export const DASHBOARD_WIDGETS_WITH_RECORD_LIMIT = [
  "projects-attention",
  "my-todo",
  "upcoming-milestones",
  "top-risks",
  "upcoming-meetings",
  "pending-approvals",
  "recent-notifications",
] as const;

export const DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT = 10;

export function dashboardWidgetHasRecordLimit(type: string): boolean {
  return (DASHBOARD_WIDGETS_WITH_RECORD_LIMIT as readonly string[]).includes(type);
}

export function dashboardWidgetRecordLimitOrDefault(value: number | undefined, fallback = DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT): number {
  return value || fallback;
}

export function dashboardWidgetRecordLimitFor(type: string, value: number | undefined): number {
  return dashboardWidgetRecordLimitOrDefault(value, type === "upcoming-meetings" ? 8 : DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT);
}

export type DashboardWidgetCategory = (typeof DASHBOARD_WIDGET_CATEGORIES)[number];

export const dashboardWidgetCategoryTranslationKeys: Record<DashboardWidgetCategory | "All", string> = {
  All: "dashboard.widgetCategoryAll",
  Overview: "dashboard.widgetCategoryOverview",
  Projects: "dashboard.widgetCategoryProjects",
  Portfolios: "dashboard.widgetCategoryPortfolios",
  "My Work": "dashboard.widgetCategoryMyWork",
  Schedule: "dashboard.widgetCategorySchedule",
  Resources: "dashboard.widgetCategoryResources",
  Timesheets: "dashboard.widgetCategoryTimesheets",
  Costs: "dashboard.widgetCategoryCosts",
  Risks: "dashboard.widgetCategoryRisks",
  Meetings: "dashboard.widgetCategoryMeetings",
  Approvals: "dashboard.widgetCategoryApprovals",
  Notifications: "dashboard.widgetCategoryNotifications",
  Integrations: "dashboard.widgetCategoryIntegrations",
  AI: "dashboard.widgetCategoryAI",
};

export const dashboardWidgetPovs: Record<string, DashboardWidgetPointOfView[]> = {
  "active-projects": ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "projects-at-risk": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "total-budget": ["Executive", "Program Manager", "Portfolio Manager", "Finance Controller"],
  "forecast-variance": ["Executive", "Program Manager", "Portfolio Manager", "Finance Controller"],
  "open-risks": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager"],
  "overdue-tasks": ["Project Manager", "Program Manager", "Portfolio Manager", "Team Member"],
  "projects-attention": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "my-todo": ["Team Member", "Project Manager", "Program Manager", "Portfolio Manager"],
  "upcoming-milestones": ["Project Manager", "Program Manager", "Portfolio Manager", "Team Member", "Executive"],
  "top-risks": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "Finance Controller"],
  "upcoming-meetings": ["Team Member", "Project Manager", "Program Manager", "Portfolio Manager", "Executive"],
  "pending-approvals": ["Project Manager", "Program Manager", "Portfolio Manager", "Finance Controller", "PMO Administrator", "Team Member"],
  "recent-notifications": ["Team Member", "Project Manager", "Program Manager", "Portfolio Manager", "Resource Manager"],
  "projects-by-health": ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator", "Project Manager"],
  "projects-by-status": ["Program Manager", "Portfolio Manager", "PMO Administrator"],
  "budget-vs-actual": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "Finance Controller"],
  "resource-utilization": ["Resource Manager", "Project Manager", "Program Manager", "Portfolio Manager"],
  "risk-trend": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager"],
  "tasks-by-status": ["Project Manager", "Program Manager", "Portfolio Manager", "Team Member"],
  "resource-capacity": ["Resource Manager", "Project Manager", "Program Manager", "Portfolio Manager"],
  "overallocated-resources": ["Resource Manager", "Project Manager", "Program Manager", "Portfolio Manager"],
  "planned-vs-actual": ["Project Manager", "Program Manager", "Portfolio Manager", "Resource Manager"],
  "cost-variance": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "Finance Controller"],
  "risk-heat-map": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "status-report-freshness": ["Project Manager", "Program Manager", "Portfolio Manager"],
  "my-timesheet-status": ["Project Manager", "Program Manager", "Portfolio Manager", "Resource Manager", "Team Member"],
  "hours-this-week": ["Project Manager", "Program Manager", "Portfolio Manager", "Resource Manager", "Team Member"],
  "missing-timesheets": ["Project Manager", "Program Manager", "Portfolio Manager", "Resource Manager", "Team Member"],
  "timesheet-approval-inbox": ["Project Manager", "Program Manager", "Portfolio Manager", "Resource Manager", "Team Member"],
  "jira-sync": ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "ms-project-import": ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "integration-health": ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"],
  "ai-recommendations": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager"],
  "delayed-predictions": ["Executive", "Project Manager", "Program Manager", "Portfolio Manager"],
};

export interface DashboardWidgetAvailabilityDefinition {
  type: string;
  supportedPOVs: readonly string[];
  requiredPermission?: string;
  requiredAnyPermissions?: readonly string[];
  requiredPlugin?: string;
}

export function filterAvailableDashboardWidgets<T extends DashboardWidgetAvailabilityDefinition>(
  definitions: readonly T[],
  pov: string,
  userPermissions: readonly string[],
): T[] {
  return definitions.filter((definition) =>
    definition.supportedPOVs.includes(pov)
    && (!definition.requiredPermission || userPermissions.includes(definition.requiredPermission))
    && (!definition.requiredAnyPermissions || definition.requiredAnyPermissions.some((permission) => userPermissions.includes(permission)))
    && !definition.requiredPlugin,
  );
}
