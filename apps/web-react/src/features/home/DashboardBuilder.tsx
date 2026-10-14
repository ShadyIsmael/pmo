import * as React from "react";
import { Link } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { BarChart3, Check, ChevronDown, ChevronUp, Copy, Eye, Filter, GripVertical, Layers, LayoutDashboard, Lock, Pencil, Plus, RefreshCw, Settings2, Star, Trash2, Users } from "lucide-react";
import { useHomeSummary, type HomeSummary } from "@api/home";
import { useManagement } from "@api/management";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { HoursThisWeekWidget } from "./HoursThisWeekWidget";
import { OverdueTasksWidget } from "./OverdueTasksWidget";
import { UpcomingMeetingsWidget } from "./UpcomingMeetingsWidget";
import { dashboardMeetingsQueryParams, dashboardWidgetNeedsWorkItems, formatDashboardMoney, getDashboardApprovalAccess, scopeDashboardProjects, scopeDashboardRecordsByProject, scopeDashboardResources, shouldLoadDashboardManagementOverview, summarizeDashboardResourceCapacity } from "./dashboard-widget-utils";
import { Permissions } from "@lib/permissions";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Badge } from "@components/ui/Badge";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ProjectHealthChart } from "@components/ui/ProjectHealthChart";
import { AttentionRequiredWidget } from "@components/ui/AttentionRequiredWidget";
import { TopRisksWidget } from "@components/ui/TopRisksWidget";
import { BudgetOverviewWidget } from "@components/ui/BudgetOverviewWidget";
import { KpiCard } from "@components/ui/KpiCard";
import { useFullMeetings, type MeetingListDto } from "@api/meetingsFull";
import { useFullWorkItems, type WorkItemDto } from "@api/workItemsFull";
import { usePortfolios, usePrograms } from "@features/delivery/api/delivery";
import { useProjects, type ProjectSummaryDto } from "@features/projects/api/projects";
import { useCapacityMatrix, type CapacityMatrixDto } from "@features/resources/api/capacity";
import { useResources, type ResourceDto } from "@features/resources/api/resources";
import { useRisks, type RiskDto } from "@features/risks/api/risks";
import { useNotifications, type NotificationListItem } from "@api/notifications";
import { usePortfolioManagementOverview, useProgramManagementOverview } from "@features/delivery/api/delivery";
import { type ManagementPerspective } from "@lib/perspective";
import {
  usePendingHandoverRequestsForMe,
  usePendingManagerHandovers,
  usePendingProjectChangeRequests,
  type HandoverRequestDto,
  type ManagerHandoverRequestDto,
  type ProjectChangeRequestDto,
} from "@features/my-work/api/myWork";
import { dashboardViewsStorageKey, loadDashboardViews, saveDashboardViews } from "./dashboardStorage";
import { createDashboardWidgetCopies, type DashboardStartMode } from "./dashboard-view-creation";
import { canDuplicateDashboardView, canEditDashboardView, canMutateDashboard, dashboardViewsForPov, defaultDashboardViewIdForPov } from "./dashboard-view-access";
import { DASHBOARD_DATE_RANGES, dashboardSummaryFilters, normalizeDashboardFilters, type DashboardFilters } from "./dashboardFilters";
import {
  DASHBOARD_WIDGET_CATEGORIES,
  dashboardWidgetCategoryTranslationKeys,
  dashboardWidgetHasRecordLimit,
  dashboardWidgetPovs,
  dashboardWidgetRecordLimitFor,
  filterAvailableDashboardWidgets,
  type DashboardWidgetCategory,
  type DashboardWidgetPointOfView,
} from "./dashboard-widget-availability";

export type WidgetType = "active-projects" | "projects-at-risk" | "open-risks" | "critical-risks" | "projects-by-health" | "projects-attention" | "top-risks" | "budget-vs-actual" | "my-work" | "my-todo" | "upcoming-milestones" | "upcoming-meetings" | "pending-approvals" | "recent-notifications" | "total-budget" | "forecast-variance" | "overdue-tasks" | "projects-by-status" | "resource-utilization" | "risk-trend" | "tasks-by-status" | "resource-capacity" | "overallocated-resources" | "planned-vs-actual" | "cost-variance" | "risk-heat-map" | "status-report-freshness" | "my-timesheet-status" | "hours-this-week" | "missing-timesheets" | "timesheet-approval-inbox" | "jira-sync" | "ms-project-import" | "integration-health" | "ai-recommendations" | "delayed-predictions";
export interface WidgetPosition { x: number; y: number; w: number; h: number }
export interface WidgetInstance { id: string; type: WidgetType; title?: string; maxRecords?: number; collapsed?: boolean; position: WidgetPosition }
interface WidgetDefinition {
  type: WidgetType;
  titleKey: string;
  descriptionKey: string;
  category: DashboardWidgetCategory;
  defaultTitle: string;
  supportedPOVs: DashboardWidgetPointOfView[];
  requiredPermission?: string;
  requiredAnyPermissions?: readonly string[];
  requiredPlugin?: string;
}
type DashboardPointOfView = DashboardWidgetPointOfView;
type DashboardType = "personal" | "shared";
type DashboardAccess = "view" | "duplicate" | "edit" | "manage-sharing";
export interface DashboardView { id: string; name: string; pov: DashboardPointOfView; ownerId: string; ownerName: string; type: DashboardType; access: DashboardAccess; sharedWith: string[]; widgets: WidgetInstance[]; filters?: DashboardFilters; isDefault: boolean }

const dashboardWidgetCatalog: Omit<WidgetDefinition, "supportedPOVs">[] = [
  { type: "active-projects", titleKey: "dashboard.activeProjects", descriptionKey: "dashboard.activeProjects", category: "Overview", defaultTitle: "Active Projects" },
  { type: "projects-at-risk", titleKey: "dashboard.projectsAtRisk", descriptionKey: "dashboard.projectsAtRisk", category: "Overview", defaultTitle: "Projects At Risk" },
  { type: "open-risks", titleKey: "dashboard.openRisks", descriptionKey: "dashboard.openRisks", category: "Risks", defaultTitle: "Open Risks" },
  { type: "critical-risks", titleKey: "dashboard.critical", descriptionKey: "dashboard.critical", category: "Risks", defaultTitle: "Critical Risks" },
  { type: "projects-by-health", titleKey: "dashboard.projectsByHealth", descriptionKey: "dashboard.projectsByHealthDescription", category: "Projects", defaultTitle: "Projects by Health" },
  { type: "projects-attention", titleKey: "dashboard.projectsRequiringAttention", descriptionKey: "dashboard.projectsRequiringAttentionDescription", category: "Projects", defaultTitle: "Projects Requiring Attention" },
  { type: "top-risks", titleKey: "dashboard.topRisksWidget", descriptionKey: "dashboard.topRisksDescription", category: "Risks", defaultTitle: "Top Risks" },
  { type: "budget-vs-actual", titleKey: "dashboard.budgetVsActualWidget", descriptionKey: "dashboard.budgetVsActualDescription", category: "Costs", defaultTitle: "Budget vs Actual" },
  { type: "my-work", titleKey: "nav.myWork", descriptionKey: "dashboard.noWork", category: "My Work", defaultTitle: "My Work" },
  { type: "upcoming-milestones", titleKey: "dashboard.upcomingMilestones", descriptionKey: "dashboard.noMilestones", category: "Schedule", defaultTitle: "Upcoming Delivery Items" },
  { type: "upcoming-meetings", titleKey: "dashboard.upcomingMeetings", descriptionKey: "dashboard.noMeetings", category: "Meetings", defaultTitle: "Upcoming Meetings" },
  { type: "my-todo", titleKey: "dashboard.myTodo", descriptionKey: "dashboard.noWork", category: "My Work", defaultTitle: "My Work" },
  { type: "pending-approvals", titleKey: "dashboard.pendingApprovalsWidget", descriptionKey: "dashboard.noDataDescription", category: "Approvals", defaultTitle: "Pending Approvals", requiredAnyPermissions: [Permissions.Handover.Approve, Permissions.ChangeRequest.Approve] },
  { type: "recent-notifications", titleKey: "dashboard.recentNotificationsWidget", descriptionKey: "dashboard.noDataDescription", category: "Notifications", defaultTitle: "Recent Notifications" },
  { type: "total-budget", titleKey: "dashboard.totalBudget", descriptionKey: "dashboard.groupedByCurrency", category: "Overview", defaultTitle: "Total Budget" },
  { type: "forecast-variance", titleKey: "dashboard.costVariance", descriptionKey: "dashboard.groupedByCurrency", category: "Overview", defaultTitle: "Cost Variance" },
  { type: "overdue-tasks", titleKey: "dashboard.overdueTasks", descriptionKey: "dashboard.noWork", category: "My Work", defaultTitle: "Overdue Tasks" },
  { type: "projects-by-status", titleKey: "dashboard.projectsByStatus", descriptionKey: "dashboard.projectsByStatus", category: "Projects", defaultTitle: "Projects by Status" },
  { type: "resource-utilization", titleKey: "dashboard.resourceUtilization", descriptionKey: "dashboard.resourceUtilization", category: "Resources", defaultTitle: "Resource Utilization" },
  { type: "risk-trend", titleKey: "dashboard.riskDistributionWidget", descriptionKey: "dashboard.riskLevelsOnly", category: "Risks", defaultTitle: "Risk Distribution" },
  { type: "tasks-by-status", titleKey: "dashboard.tasksByStatus", descriptionKey: "dashboard.tasksByStatus", category: "My Work", defaultTitle: "Work Items by Status" },
  { type: "resource-capacity", titleKey: "dashboard.resourceCapacity", descriptionKey: "dashboard.resourceCapacity", category: "Resources", defaultTitle: "Resource Capacity" },
  { type: "overallocated-resources", titleKey: "dashboard.overallocatedResources", descriptionKey: "dashboard.overallocatedResources", category: "Resources", defaultTitle: "Overallocated Resources" },
  { type: "planned-vs-actual", titleKey: "dashboard.plannedVsActual", descriptionKey: "dashboard.plannedVsActual", category: "Timesheets", defaultTitle: "Planned vs Actual Work" },
  { type: "cost-variance", titleKey: "dashboard.projectCostComparisonWidget", descriptionKey: "dashboard.groupedByCurrency", category: "Costs", defaultTitle: "Project Cost Comparison" },
  { type: "risk-heat-map", titleKey: "dashboard.riskHeatMap", descriptionKey: "dashboard.riskLevelsOnly", category: "Risks", defaultTitle: "Risk Level Distribution" },
  { type: "status-report-freshness", titleKey: "dashboard.statusReportFreshness", descriptionKey: "dashboard.noDataDescription", category: "Overview", defaultTitle: "Status Report Freshness" },
  { type: "my-timesheet-status", titleKey: "dashboard.myTimesheetStatus", descriptionKey: "dashboard.sourceUnavailable", category: "Timesheets", defaultTitle: "My Timesheet Status" },
  { type: "hours-this-week", titleKey: "dashboard.hoursThisWeek", descriptionKey: "dashboard.hoursThisWeekDescription", category: "Timesheets", defaultTitle: "Hours This Week", requiredAnyPermissions: ["TimeTracking.ViewOwn", "TimeTracking.ManageOwn", "TimeTracking.ViewTeam", "TimeTracking.ManageTeam"] },
  { type: "missing-timesheets", titleKey: "dashboard.missingTimesheets", descriptionKey: "dashboard.sourceUnavailable", category: "Timesheets", defaultTitle: "Missing Timesheets" },
  { type: "timesheet-approval-inbox", titleKey: "dashboard.timesheetApprovalInbox", descriptionKey: "dashboard.sourceUnavailable", category: "Timesheets", defaultTitle: "Timesheet Approval Inbox" },
  { type: "jira-sync", titleKey: "dashboard.jiraSync", descriptionKey: "dashboard.sourceUnavailable", category: "Integrations", defaultTitle: "Jira Sync" },
  { type: "ms-project-import", titleKey: "dashboard.msProjectImport", descriptionKey: "dashboard.sourceUnavailable", category: "Integrations", defaultTitle: "Microsoft Project Import" },
  { type: "integration-health", titleKey: "dashboard.integrationHealth", descriptionKey: "dashboard.sourceUnavailable", category: "Integrations", defaultTitle: "Integration Health" },
  { type: "ai-recommendations", titleKey: "dashboard.aiRecommendations", descriptionKey: "dashboard.sourceUnavailable", category: "AI", defaultTitle: "AI Recommendations" },
  { type: "delayed-predictions", titleKey: "dashboard.delayedPredictions", descriptionKey: "dashboard.sourceUnavailable", category: "AI", defaultTitle: "Delay Predictions" },
];

export const dashboardWidgetDefinitions: WidgetDefinition[] = dashboardWidgetCatalog.map((definition) => ({
  ...definition,
  supportedPOVs: dashboardWidgetPovs[definition.type] ?? [],
}));

export const unavailableWidgetTypes = new Set<WidgetType>([
  "my-timesheet-status", "missing-timesheets", "timesheet-approval-inbox", "jira-sync", "ms-project-import", "integration-health", "ai-recommendations", "delayed-predictions",
]);

export function shouldLoadDashboardHomeSummary(type: WidgetType): boolean {
  const listWidgets: readonly WidgetType[] = ["my-work", "my-todo", "upcoming-milestones", "upcoming-meetings"];
  return !unavailableWidgetTypes.has(type)
    && !listWidgets.includes(type)
    && type !== "overdue-tasks"
    && type !== "recent-notifications"
    && type !== "pending-approvals"
    && type !== "status-report-freshness"
    && type !== "hours-this-week";
}

const defaultWidgetSizes: Partial<Record<WidgetType, Pick<WidgetPosition, "w" | "h">>> = {
  "active-projects": { w: 2, h: 2 },
  "projects-at-risk": { w: 2, h: 2 },
  "open-risks": { w: 2, h: 2 },
  "total-budget": { w: 2, h: 2 },
  "overdue-tasks": { w: 2, h: 2 },
  "status-report-freshness": { w: 3, h: 2 },
  "projects-by-health": { w: 3, h: 3 },
  "budget-vs-actual": { w: 3, h: 3 },
  "projects-attention": { w: 3, h: 3 },
  "top-risks": { w: 3, h: 3 },
  "resource-utilization": { w: 3, h: 3 },
  "upcoming-meetings": { w: 3, h: 3 },
};

const defaultWidgetSizesByPov: Partial<Record<DashboardPointOfView, Partial<Record<WidgetType, Pick<WidgetPosition, "w" | "h">>>>> = {
  "Project Manager": { "resource-utilization": { w: 2, h: 2 } },
  "Resource Manager": { "resource-utilization": { w: 2, h: 2 } },
};

function defaultWidgetPosition(type: WidgetType, index: number, pov?: DashboardPointOfView): WidgetPosition {
  const povSize = pov ? defaultWidgetSizesByPov[pov]?.[type] : undefined;
  const size = povSize ?? defaultWidgetSizes[type] ?? { w: 3, h: 3 };
  return { x: (index * 3) % 12, y: Math.floor(index / 4) * 3, w: size.w, h: size.h };
}

function normalizeWidgetPosition(type: WidgetType, index: number, position?: Partial<WidgetPosition>, columns = 12): WidgetPosition {
  const fallback = defaultWidgetPosition(type, index);
  const w = Math.max(1, Math.min(columns, Number.isFinite(position?.w) ? Number(position?.w) : fallback.w));
  const h = Math.max(2, Math.min(12, Number.isFinite(position?.h) ? Number(position?.h) : fallback.h));
  const x = Math.max(0, Math.min(columns - w, Number.isFinite(position?.x) ? Number(position?.x) : fallback.x));
  const y = Math.max(0, Number.isFinite(position?.y) ? Number(position?.y) : fallback.y);
  return { x, y, w, h };
}

function migrateWidget(widget: WidgetInstance, index: number): WidgetInstance {
  return { ...widget, position: normalizeWidgetPosition(widget.type, index, widget.position) };
}

function widgetsOverlap(left: WidgetPosition, right: WidgetPosition): boolean {
  return left.x < right.x + right.w && left.x + left.w > right.x && left.y < right.y + right.h && left.y + left.h > right.y;
}

export function resolveDashboardLayout(widgets: WidgetInstance[], widgetId: string, requestedPosition: WidgetPosition): WidgetInstance[] {
  const target = widgets.find((widget) => widget.id === widgetId);
  if (!target) return widgets;
  const nextPosition = normalizeWidgetPosition(target.type, widgets.indexOf(target), requestedPosition);
  if (nextPosition.x === target.position.x && nextPosition.y === target.position.y && nextPosition.w === target.position.w && nextPosition.h === target.position.h) return widgets;
  const moved = { ...target, position: nextPosition };
  const ordered = [moved, ...widgets.filter((widget) => widget.id !== widgetId)];
  const placed: WidgetInstance[] = [];
  for (const widget of ordered) {
    let position = widget.position;
    if (widget.id !== widgetId) {
      let conflicts = placed.filter((item) => widgetsOverlap(position, item.position));
      while (conflicts.length) {
        position = { ...position, y: Math.max(...conflicts.map((item) => item.position.y + item.position.h)) };
        conflicts = placed.filter((item) => widgetsOverlap(position, item.position));
      }
    }
    placed.push({ ...widget, position });
  }
  const byId = new Map(placed.map((widget) => [widget.id, widget]));
  return widgets.map((widget) => byId.get(widget.id) ?? widget);
}

const defaultWidgetTypesByPov: Record<DashboardPointOfView, WidgetType[]> = {
  Executive: ["active-projects", "projects-at-risk", "open-risks", "total-budget", "projects-by-health", "budget-vs-actual", "projects-attention", "top-risks", "status-report-freshness", "resource-utilization", "upcoming-meetings"],
  "Project Manager": ["projects-attention", "my-todo", "upcoming-milestones", "overdue-tasks", "open-risks", "resource-utilization", "budget-vs-actual", "upcoming-meetings", "status-report-freshness"],
  "Program Manager": ["active-projects", "projects-at-risk", "open-risks", "total-budget", "projects-by-health", "budget-vs-actual", "projects-attention", "top-risks", "status-report-freshness", "resource-utilization", "upcoming-meetings"],
  "Portfolio Manager": ["active-projects", "projects-at-risk", "open-risks", "total-budget", "projects-by-health", "budget-vs-actual", "projects-attention", "top-risks", "status-report-freshness", "resource-utilization", "upcoming-meetings"],
  "Team Member": ["my-todo", "upcoming-milestones", "upcoming-meetings", "overdue-tasks"],
  "Resource Manager": ["resource-utilization", "resource-capacity", "overallocated-resources", "planned-vs-actual"],
  "Finance Controller": ["total-budget", "budget-vs-actual", "cost-variance", "top-risks"],
  "PMO Administrator": ["active-projects", "projects-at-risk", "open-risks", "total-budget", "projects-by-health", "budget-vs-actual", "projects-attention", "top-risks", "status-report-freshness", "resource-utilization", "upcoming-meetings"],
};

export function defaultDashboardWidgetsForPov(pov: DashboardPointOfView): WidgetInstance[] {
  return (defaultWidgetTypesByPov[pov] ?? defaultWidgetTypesByPov["Portfolio Manager"]).map((type, index) => ({ id: `${type}-${pov.toLowerCase().replaceAll(" ", "-")}`, type, position: defaultWidgetPosition(type, index, pov) }));
}

export const defaultDashboardWidgets: WidgetInstance[] = defaultDashboardWidgetsForPov("Portfolio Manager");

function definition(type: WidgetType) { return dashboardWidgetDefinitions.find((item) => item.type === type) ?? dashboardWidgetDefinitions[0]; }

function initialPointOfView(perspective?: string): DashboardPointOfView {
  const values: Record<string, DashboardPointOfView> = { Executive: "Executive", ProjectManager: "Project Manager", ProgramManager: "Program Manager", PortfolioManager: "Portfolio Manager", TeamMember: "Team Member", ResourceManager: "Resource Manager", FinanceController: "Finance Controller", PMOAdministrator: "PMO Administrator" };
  return values[perspective ?? ""] ?? "Portfolio Manager";
}

function defaultDashboardView(pov: DashboardPointOfView, ownerId = "current-user", ownerName = "You"): DashboardView {
  return { id: `default-${pov.toLowerCase().replaceAll(" ", "-")}`, name: "My Dashboard", pov, ownerId, ownerName, type: "personal", access: "manage-sharing", sharedWith: [], widgets: defaultDashboardWidgetsForPov(pov), filters: normalizeDashboardFilters(), isDefault: true };
}

function defaultSharedDashboardView(pov: DashboardPointOfView): DashboardView {
  return {
    ...defaultDashboardView(pov, "pmo-admin", "PMO Office"),
    id: `shared-${pov.toLowerCase().replaceAll(" ", "-")}`,
    name: `${pov} Standard`,
    type: "shared",
    access: "duplicate",
    sharedWith: [pov],
    isDefault: false,
  };
}

export function defaultDashboardViewsForPov(pov: DashboardPointOfView, ownerId: string, ownerName: string): DashboardView[] {
  return [defaultDashboardView(pov, ownerId, ownerName), defaultSharedDashboardView(pov)];
}

export function ensureDashboardSharedTemplates(views: readonly DashboardView[]): DashboardView[] {
  const result = [...views];
  for (const pov of new Set(views.map((view) => view.pov))) {
    const sharedId = `shared-${pov.toLowerCase().replaceAll(" ", "-")}`;
    if (!result.some((view) => view.id === sharedId && view.type === "shared")) result.push(defaultSharedDashboardView(pov));
  }
  return result;
}

function migrateDashboardView(view: Partial<DashboardView>, pov: DashboardPointOfView, ownerId: string, ownerName: string): DashboardView | null {
  if (!Array.isArray(view.widgets) || view.widgets.some((item) => !dashboardWidgetDefinitions.some((definitionItem) => definitionItem.type === item.type))) return null;
  return { id: view.id ?? `view-${Date.now()}`, name: view.name ?? "My Dashboard", pov: view.pov ?? pov, ownerId: view.ownerId ?? ownerId, ownerName: view.ownerName ?? ownerName, type: view.type === "shared" ? "shared" : "personal", access: view.access ?? "manage-sharing", sharedWith: Array.isArray(view.sharedWith) ? view.sharedWith : [], widgets: view.widgets.map(migrateWidget), filters: normalizeDashboardFilters(view.filters), isDefault: Boolean(view.isDefault) };
}

function useDashboardState(initialPov: DashboardPointOfView, owner?: { id: string; fullName: string }, tenantId?: string) {
  const ownerId = owner?.id ?? "current-user";
  const ownerName = owner?.fullName ?? "You";
  const storageKey = dashboardViewsStorageKey(tenantId, owner?.id);
  const [views, setViews] = React.useState<DashboardView[]>(() => defaultDashboardViewsForPov(initialPov, ownerId, ownerName));
  const [currentViewId, setCurrentViewId] = React.useState(`default-${initialPov.toLowerCase().replaceAll(" ", "-")}`);
  const [pov, setPov] = React.useState<DashboardPointOfView>(initialPov);
  const [lastViewByPov, setLastViewByPov] = React.useState<Partial<Record<DashboardPointOfView, string>>>(() => ({ [initialPov]: `default-${initialPov.toLowerCase().replaceAll(" ", "-")}` }));
  const [loadedStorageKey, setLoadedStorageKey] = React.useState("");
  React.useEffect(() => {
    let active = true;
    setLoadedStorageKey("");
    try {
      const parsed = loadDashboardViews<{ views?: DashboardView[]; currentViewId?: string; pov?: DashboardPointOfView; lastViewByPov?: Partial<Record<DashboardPointOfView, string>> }>(tenantId, owner?.id);
      if (parsed) {
        const migratedViews = parsed.views?.map((view) => migrateDashboardView(view, initialPov, ownerId, ownerName)).filter((view): view is DashboardView => Boolean(view));
        const validViews = migratedViews?.length ? ensureDashboardSharedTemplates(migratedViews) : migratedViews;
        if (validViews?.length) {
          if (!active) return;
          const restoredPov = validViews.some((view) => view.pov === parsed.pov) ? parsed.pov! : initialPov;
          const rememberedId = parsed.lastViewByPov?.[restoredPov];
          const remembered = validViews.find((view) => view.id === rememberedId && view.pov === restoredPov);
          const savedCurrent = validViews.find((view) => view.id === parsed.currentViewId && view.pov === restoredPov);
          const selected = remembered ?? savedCurrent ?? validViews.find((view) => view.id === defaultDashboardViewIdForPov(validViews, restoredPov)) ?? validViews.find((view) => view.pov === restoredPov) ?? validViews[0]!;
          setViews(validViews);
          setCurrentViewId(selected.id);
          setPov(restoredPov);
          setLastViewByPov({ ...parsed.lastViewByPov, [restoredPov]: selected.id });
        } else {
          const initialViews = defaultDashboardViewsForPov(initialPov, ownerId, ownerName);
          setViews(initialViews);
          setCurrentViewId(initialViews[0]!.id);
          setPov(initialPov);
          setLastViewByPov({ [initialPov]: initialViews[0]!.id });
        }
      } else {
        const initialViews = defaultDashboardViewsForPov(initialPov, ownerId, ownerName);
        setViews(initialViews);
        setCurrentViewId(initialViews[0]!.id);
        setPov(initialPov);
        setLastViewByPov({ [initialPov]: initialViews[0]!.id });
      }
    } catch { /* preferences are optional; keep the canonical default layout */ }
    if (active) setLoadedStorageKey(storageKey);
    return () => { active = false; };
  }, [initialPov, ownerId, ownerName, owner?.id, tenantId, storageKey]);
  const loaded = loadedStorageKey === storageKey;
  React.useEffect(() => { if (loaded) saveDashboardViews(tenantId, owner?.id, { views, currentViewId, pov, lastViewByPov }); }, [loaded, tenantId, owner?.id, views, currentViewId, pov, lastViewByPov]);
  const currentView = views.find((view) => view.id === currentViewId && view.pov === pov) ?? views.find((view) => view.pov === pov) ?? views[0];
  function setWidgets(updater: React.SetStateAction<WidgetInstance[]>) { setViews((items) => items.map((view) => view.id === currentView?.id ? { ...view, widgets: typeof updater === "function" ? updater(view.widgets) : updater } : view)); }
  function switchView(id: string) { const next = views.find((view) => view.id === id); if (next) { setCurrentViewId(id); setPov(next.pov); setLastViewByPov((previous) => ({ ...previous, [next.pov]: id })); } }
  function changePov(nextPov: DashboardPointOfView) {
    const matching = views.find((view) => view.id === lastViewByPov[nextPov] && view.pov === nextPov)
      ?? views.find((view) => view.pov === nextPov && view.isDefault)
      ?? views.find((view) => view.pov === nextPov);
    setPov(nextPov);
    if (matching) {
      setCurrentViewId(matching.id);
      setLastViewByPov((previous) => ({ ...previous, [nextPov]: matching.id }));
      return;
    }
    if (!currentView) return;
    const nextViews = defaultDashboardViewsForPov(nextPov, ownerId, ownerName);
    setViews((items) => [...items, ...nextViews]);
    setCurrentViewId(nextViews[0]!.id);
    setLastViewByPov((previous) => ({ ...previous, [nextPov]: nextViews[0]!.id }));
  }
  function setFilters(filters: DashboardFilters) { setViews((items) => items.map((view) => view.id === currentView?.id ? { ...view, filters } : view)); }
  return { views, currentView, currentViewId, pov, loaded, setViews, setCurrentViewId, setPov, setWidgets, switchView, changePov, setFilters, lastViewByPov, setLastViewByPov };
}

function VisibleWidget({ children, className, style }: { children: React.ReactNode; className?: string; style?: React.CSSProperties }) {
  const ref = React.useRef<HTMLDivElement>(null);
  const [visible, setVisible] = React.useState(false);
  React.useEffect(() => {
    const element = ref.current;
    if (!element || !("IntersectionObserver" in window)) { setVisible(true); return; }
    const observer = new IntersectionObserver(([entry]) => { if (entry?.isIntersecting) { setVisible(true); observer.disconnect(); } }, { rootMargin: "120px" });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  return <div ref={ref} className={`min-h-[10rem] ${className ?? ""}`} style={style}>{visible ? children : <div className="h-full min-h-[10rem] animate-pulse rounded-lg bg-muted/30" aria-label="Loading widget" />}</div>;
}

type DashboardApprovals = { handovers: readonly HandoverRequestDto[]; managerHandovers: readonly ManagerHandoverRequestDto[]; projectChanges: readonly ProjectChangeRequestDto[] };
type StatusReportFreshness = { currentCount: number; staleCount: number; missingCount: number };

function DashboardWidgetContent({ type, summary, workItems, meetings, projects, capacity, resources, risks, notifications, approvals, statusReports, permissions, maxRecords }: { type: WidgetType; summary?: HomeSummary; workItems?: readonly WorkItemDto[]; meetings?: readonly MeetingListDto[]; projects?: readonly ProjectSummaryDto[]; capacity?: CapacityMatrixDto; resources?: readonly ResourceDto[]; risks?: readonly RiskDto[]; notifications?: readonly NotificationListItem[]; approvals?: DashboardApprovals; statusReports?: StatusReportFreshness; permissions: readonly string[]; maxRecords: number }) {
  const { t, locale } = useI18n();
  if (unavailableWidgetTypes.has(type)) return <div className="flex min-h-32 items-center justify-center text-center text-sm text-muted-foreground">{t("dashboard.notAvailable")}</div>;
  if (type === "pending-approvals" && !getDashboardApprovalAccess(permissions).any) return <div className="flex min-h-32 items-center justify-center text-center text-sm text-muted-foreground">{t("dashboard.notAvailable")}</div>;
  if (type === "hours-this-week") return <HoursThisWeekWidget permissions={permissions} />;
  if (type === "overdue-tasks") return <OverdueTasksWidget workItems={workItems ?? []} />;
  if (type === "status-report-freshness") {
    return statusReports ? <div className="grid min-h-32 grid-cols-3 items-center gap-2 p-2 text-center"><div><div className="text-xl font-bold text-status-green">{statusReports.currentCount}</div><div className="text-[10px] text-muted-foreground">{t("dashboard.current")}</div></div><div><div className="text-xl font-bold text-status-amber">{statusReports.staleCount}</div><div className="text-[10px] text-muted-foreground">{t("dashboard.stale")}</div></div><div><div className="text-xl font-bold text-muted-foreground">{statusReports.missingCount}</div><div className="text-[10px] text-muted-foreground">{t("dashboard.missing")}</div></div></div> : <div className="flex min-h-32 items-center justify-center text-center text-sm text-muted-foreground">{t("dashboard.notAvailable")}</div>;
  }
  if (type === "recent-notifications") {
    const items = (notifications ?? []).slice(0, maxRecords);
    return items.length ? <div className="space-y-1.5">{items.map((notification) => <Link key={notification.id} to="/notifications" className="block rounded px-2 py-1.5 text-xs hover:bg-muted/50"><div className="flex items-center gap-2"><span className={`h-2 w-2 shrink-0 rounded-full ${notification.isRead ? "bg-muted-foreground/30" : "bg-primary"}`} /><span className="min-w-0 flex-1 truncate font-medium">{notification.title}</span><span className="shrink-0 text-muted-foreground">{new Date(notification.sentAt).toLocaleDateString()}</span></div>{notification.body ? <p className="mt-0.5 truncate ps-4 text-muted-foreground">{notification.body}</p> : null}</Link>)}</div> : <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t("dashboard.noData")}</div>;
  }
  if (type === "pending-approvals") {
    const rows = [
      ...(approvals?.handovers ?? []).map((item) => ({ id: `project-${item.id}`, label: t("approvals.handoverRequest"), name: item.projectName })),
      ...(approvals?.managerHandovers ?? []).map((item) => ({ id: `${item.entityType}-${item.id}`, label: `${item.entityType} ${t("approvals.handoverRequest")}`, name: item.entityName })),
      ...(approvals?.projectChanges ?? []).map((item) => ({ id: `change-${item.id}`, label: t("approvals.changeRequest"), name: item.projectName })),
    ].slice(0, maxRecords);
    return rows.length ? <div className="space-y-1.5">{rows.map((row) => <Link key={row.id} to="/my-work/approvals" className="flex items-center gap-2 rounded px-2 py-1.5 text-xs hover:bg-muted/50"><span className="h-2 w-2 shrink-0 rounded-full bg-warning" /><span className="min-w-0 flex-1 truncate font-medium">{row.name}</span><span className="shrink-0 text-muted-foreground">{row.label}</span></Link>)}</div> : <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t("dashboard.noData")}</div>;
  }
  if (type === "my-work" || type === "my-todo" || type === "upcoming-milestones") {
    const today = new Date().toISOString().slice(0, 10);
    const items = [...(workItems ?? [])].filter((item) => { const open = item.status.toLowerCase() !== "completed"; if (type === "upcoming-milestones") return open && Boolean(item.dueDate && item.dueDate.slice(0, 10) >= today); return open; }).sort((left, right) => { if (type === "upcoming-milestones") return new Date(left.dueDate ?? "9999-12-31").getTime() - new Date(right.dueDate ?? "9999-12-31").getTime(); return new Date(right.updatedAt ?? right.createdAt).getTime() - new Date(left.updatedAt ?? left.createdAt).getTime(); }).slice(0, maxRecords);
    return items.length ? <div className="space-y-1.5">{items.map((item) => <Link key={item.id} to={`/work-items/${encodeURIComponent(item.id)}`} className="flex items-center gap-2 rounded px-2 py-1.5 text-xs hover:bg-muted/50"><span className="h-3.5 w-3.5 shrink-0 rounded border border-muted-foreground/40" /><span className="min-w-0 flex-1 truncate font-medium">{item.title}</span><span className="shrink-0 rounded bg-muted px-1 py-0.5 text-[10px]">{item.priority}</span>{item.dueDate ? <span className="shrink-0 text-muted-foreground">{item.dueDate.slice(0, 10)}</span> : null}</Link>)}</div> : <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t(type === "upcoming-milestones" ? "dashboard.noMilestones" : "dashboard.noWork")}</div>;
  }
  if (type === "upcoming-meetings") return <UpcomingMeetingsWidget meetings={meetings ?? []} maxRecords={maxRecords} />;
  if (type === "projects-by-status") {
    const counts: Record<string, number> = {};
    for (const project of projects ?? []) counts[project.status] = (counts[project.status] ?? 0) + 1;
    return <div className="space-y-1.5">{Object.entries(counts).map(([status, count]) => <div key={status} className="flex items-center gap-2 px-2 py-0.5 text-xs"><span className="h-2 w-2 rounded-full bg-primary" /><span className="flex-1">{status || t("dashboard.unknown")}</span><span className="font-medium tabular-nums">{count}</span></div>)}{!Object.keys(counts).length ? <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t("dashboard.noData")}</div> : null}</div>;
  }
  if (type === "resource-utilization") {
    const utilization = capacity?.summary.overallUtilizationPercent ?? 0;
    return <div className="flex min-h-32 flex-col items-center justify-center"><div className="relative h-24 w-24"><svg className="h-24 w-24 -rotate-90" viewBox="0 0 100 100"><circle cx="50" cy="50" r="42" fill="none" stroke="currentColor" className="text-muted" strokeWidth="8" /><circle cx="50" cy="50" r="42" fill="none" stroke="currentColor" className="text-primary" strokeWidth="8" strokeDasharray={`${2 * Math.PI * 42}`} strokeDashoffset={`${2 * Math.PI * 42 * (1 - Math.min(utilization, 100) / 100)}`} strokeLinecap="round" /></svg><span className="absolute inset-0 flex items-center justify-center text-2xl font-bold">{Math.round(utilization)}%</span></div><span className="mt-1 text-xs text-muted-foreground">{t("dashboard.resourceUtilization")}</span></div>;
  }
  if (type === "risk-trend" || type === "risk-heat-map") {
    const counts: Record<string, number> = {};
    for (const risk of risks ?? []) if (!["closed", "mitigated"].includes(risk.status.toLowerCase())) counts[risk.riskLevel] = (counts[risk.riskLevel] ?? 0) + 1;
    return <div className="space-y-1.5">{["Critical", "High", "Medium", "Low"].map((level) => <div key={level} className="flex items-center gap-2 text-xs"><span className={`h-2 w-2 rounded-full ${level === "Critical" ? "bg-destructive" : level === "High" ? "bg-warning" : "bg-primary"}`} /><span className="flex-1">{level}</span><span className="font-medium">{counts[level] ?? 0}</span></div>)}<p className="pt-1 text-[10px] text-muted-foreground">{t(type === "risk-heat-map" ? "dashboard.riskLevelsOnly" : "dashboard.riskDistribution")}</p></div>;
  }
  if (type === "tasks-by-status") {
    const counts: Record<string, number> = {};
    for (const item of workItems ?? []) counts[item.status] = (counts[item.status] ?? 0) + 1;
    return <div className="space-y-1.5">{Object.entries(counts).map(([status, count]) => <div key={status} className="flex items-center gap-2 px-2 py-0.5 text-xs"><span className="h-2 w-2 rounded-full bg-primary" /><span className="flex-1">{status || t("dashboard.unknown")}</span><span className="font-medium tabular-nums">{count}</span></div>)}{!Object.keys(counts).length ? <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t("dashboard.noData")}</div> : null}</div>;
  }
  if (type === "resource-capacity") {
    const { resourceCount, utilization } = summarizeDashboardResourceCapacity(resources ?? []);
    return <div className="flex min-h-32 flex-col items-center justify-center gap-2"><span className="text-3xl font-bold">{utilization === null ? "—" : `${Math.round(utilization)}%`}</span><span className="text-xs text-muted-foreground">{t("dashboard.resourceUtilization")}</span><span className="text-[10px] text-muted-foreground">{resourceCount} {t("dashboard.resources")}</span></div>;
  }
  if (type === "overallocated-resources") {
    const overallocated = (resources ?? []).filter((resource) => (resource.currentUtilization ?? 0) > 100).slice(0, maxRecords);
    return <div className="space-y-1">{overallocated.map((resource) => <div key={resource.id} className="flex items-center gap-2 px-2 py-1.5 text-xs"><span className="h-2 w-2 rounded-full bg-destructive" /><span className="flex-1 truncate font-medium">{resource.name}</span><span className="text-destructive">{Math.round(resource.currentUtilization ?? 0)}%</span></div>)}{!overallocated.length ? <p className="py-4 text-center text-xs text-muted-foreground">{t("dashboard.noOverallocatedResources")}</p> : null}</div>;
  }
  if (type === "planned-vs-actual") {
    const items = (workItems ?? []).filter((item) => typeof item.estimatedHours === "number" || typeof item.actualHours === "number").slice(0, maxRecords);
    return <div className="space-y-1">{items.map((item) => <div key={item.id} className="flex items-center justify-between text-xs"><span className="truncate">{item.title}</span><span className="ms-2 tabular-nums text-muted-foreground">{item.actualHours ?? "—"}/{item.estimatedHours ?? "—"}h</span></div>)}{!items.length ? <p className="py-4 text-center text-xs text-muted-foreground">{t("dashboard.noData")}</p> : null}</div>;
  }
  if (type === "cost-variance") {
    const items = (projects ?? []).filter((project) => !["completed", "cancelled", "closed"].includes(project.status.toLowerCase()) && typeof project.budget === "number" && typeof project.actualCost === "number").slice(0, maxRecords);
    return <div className="space-y-1">{items.map((project) => <div key={project.id} className="flex items-center gap-2 px-2 py-1.5 text-xs"><span className="flex-1 truncate font-medium">{project.name}</span><span className="tabular-nums">{formatDashboardMoney(project.actualCost!, project.currency, locale)} / {formatDashboardMoney(project.budget!, project.currency, locale)}</span></div>)}{!items.length ? <p className="py-4 text-center text-xs text-muted-foreground">{t("dashboard.noFinancialData")}</p> : null}</div>;
  }
  if (!summary) return <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t("dashboard.noData")}</div>;
  if (type === "active-projects") return <KpiCard label={t("dashboard.activeProjects")} value={summary.activeProjectsCount} icon="folder" />;
  if (type === "projects-at-risk") return <KpiCard label={t("dashboard.projectsAtRisk")} value={summary.projectsAtRiskCount} icon="alert-triangle" variant="destructive" />;
  if (type === "open-risks") return <KpiCard label={t("dashboard.openRisks")} value={summary.openRisksCount} icon="alert-circle" />;
  if (type === "critical-risks") return <KpiCard label={t("dashboard.critical")} value={summary.criticalOpenRisksCount} icon="alert-triangle" variant="destructive" />;
  if (type === "projects-by-health") return <ProjectHealthChart data={summary.projectHealthCounts} />;
  if (type === "projects-attention") return <AttentionRequiredWidget projects={summary.attentionProjects.slice(0, maxRecords)} />;
  if (type === "top-risks") return <TopRisksWidget risks={summary.topRisks.slice(0, maxRecords)} />;
  if (type === "total-budget" || type === "forecast-variance") {
    const budget = summary.budgetByCurrency[0];
    return <BudgetOverviewWidget data={budget ? { approved: budget.budget, actual: budget.actualCost, currency: budget.currency } : undefined} />;
  }
  const budget = summary.budgetByCurrency[0];
  return <BudgetOverviewWidget data={budget ? { approved: budget.budget, actual: budget.actualCost, currency: budget.currency } : undefined} />;
}

function DashboardWidget({ widget, layoutSnapshot, filters, editMode, index, total, onMove, onRemove, onSettings, onToggleCollapse, onDuplicate, onResize }: { widget: WidgetInstance; layoutSnapshot: WidgetInstance[]; filters: DashboardFilters; editMode: boolean; index: number; total: number; onMove: (index: number, direction: -1 | 1) => void; onRemove: (id: string) => void; onSettings: (widget: WidgetInstance) => void; onToggleCollapse: (id: string) => void; onDuplicate: (id: string) => void; onResize: (id: string, position: WidgetPosition, layout?: WidgetInstance[]) => void }) {
  const { t } = useI18n();
  const [refreshKey, setRefreshKey] = React.useState(0);
  const resizeCleanup = React.useRef<(() => void) | null>(null);
  const current = definition(widget.type);
  const title = widget.title || t(current.titleKey);
  const position = widget.position;
  const resizeWidget = (event: React.PointerEvent<HTMLButtonElement>) => {
    if (!editMode) return;
    event.preventDefault();
    event.stopPropagation();
    resizeCleanup.current?.();
    const grid = event.currentTarget.closest<HTMLElement>("[data-dashboard-grid]");
    const gridWidth = grid?.getBoundingClientRect().width ?? 1200;
    const columns = window.innerWidth >= 1024 ? 12 : 6;
    const columnPitch = Math.max(56, (gridWidth - (columns - 1) * 12) / columns + 12);
    const rowPitch = 108;
    const direction = document.documentElement.dir === "rtl" ? -1 : 1;
    const startX = event.clientX;
    const startY = event.clientY;
    const startPosition = normalizeWidgetPosition(widget.type, index, position, columns);
    const startLayout = layoutSnapshot;
    const previousUserSelect = document.body.style.userSelect;
    const previousCursor = document.body.style.cursor;
    let finished = false;
    const cleanup = () => {
      if (finished) return;
      finished = true;
      document.removeEventListener("pointermove", onMove);
      document.removeEventListener("pointerup", cleanup);
      document.body.style.userSelect = previousUserSelect;
      document.body.style.cursor = previousCursor;
      if (resizeCleanup.current === cleanup) resizeCleanup.current = null;
    };
    const onMove = (moveEvent: PointerEvent) => {
      const widthDelta = Math.round(((moveEvent.clientX - startX) * direction) / columnPitch);
      const heightDelta = Math.round((moveEvent.clientY - startY) / rowPitch);
      onResize(widget.id, normalizeWidgetPosition(widget.type, index, { ...startPosition, w: startPosition.w + widthDelta, h: startPosition.h + heightDelta }, columns), startLayout);
    };
    resizeCleanup.current = cleanup;
    document.body.style.userSelect = "none";
    document.body.style.cursor = "nwse-resize";
    document.addEventListener("pointermove", onMove);
    document.addEventListener("pointerup", cleanup, { once: true });
  };
  const moveWidget = (event: React.PointerEvent<HTMLButtonElement>) => {
    if (!editMode) return;
    event.preventDefault();
    event.stopPropagation();
    resizeCleanup.current?.();
    const grid = event.currentTarget.closest<HTMLElement>("[data-dashboard-grid]");
    const gridWidth = grid?.getBoundingClientRect().width ?? 1200;
    const columns = window.innerWidth >= 1024 ? 12 : 6;
    const columnPitch = Math.max(56, (gridWidth - (columns - 1) * 12) / columns + 12);
    const direction = document.documentElement.dir === "rtl" ? -1 : 1;
    const startX = event.clientX;
    const startY = event.clientY;
    const startPosition = normalizeWidgetPosition(widget.type, index, position, columns);
    const startLayout = layoutSnapshot;
    const previousUserSelect = document.body.style.userSelect;
    const previousCursor = document.body.style.cursor;
    let finished = false;
    const cleanup = () => {
      if (finished) return;
      finished = true;
      document.removeEventListener("pointermove", onMove);
      document.removeEventListener("pointerup", cleanup);
      document.body.style.userSelect = previousUserSelect;
      document.body.style.cursor = previousCursor;
      if (resizeCleanup.current === cleanup) resizeCleanup.current = null;
    };
    const onMove = (moveEvent: PointerEvent) => {
      const columnDelta = Math.round(((moveEvent.clientX - startX) * direction) / columnPitch);
      const rowDelta = Math.round((moveEvent.clientY - startY) / 108);
      onResize(widget.id, normalizeWidgetPosition(widget.type, index, { ...startPosition, x: startPosition.x + columnDelta, y: startPosition.y + rowDelta }, columns), startLayout);
    };
    resizeCleanup.current = cleanup;
    document.body.style.userSelect = "none";
    document.body.style.cursor = "grabbing";
    document.addEventListener("pointermove", onMove);
    document.addEventListener("pointerup", cleanup, { once: true });
  };
  React.useEffect(() => () => resizeCleanup.current?.(), []);
  const widgetStyle = { "--dashboard-widget-column": position.x + 1, "--dashboard-widget-row": position.y + 1, "--dashboard-widget-span-md": Math.min(position.w, 6), "--dashboard-widget-span-xl": position.w, "--dashboard-widget-row-span": position.h, "--dashboard-widget-min-height": `${Math.max(2, position.h) * 6}rem` } as React.CSSProperties;
  return <VisibleWidget key={`${widget.id}-${refreshKey}`} className="dashboard-widget" style={widgetStyle}><div className="h-full">{widget.collapsed ? <Card className="h-full"><CardHeader className="flex flex-row items-center justify-between gap-2 py-3"><div className="flex min-w-0 items-center gap-2">{editMode ? <button type="button" className="dashboard-widget-move drag-handle cursor-move touch-none text-muted-foreground" aria-label={t("dashboard.moveWidget")} title={t("dashboard.moveWidget")} onPointerDown={moveWidget}><GripVertical className="h-3.5 w-3.5" /></button> : null}<CardTitle className="truncate text-sm">{title}</CardTitle></div><div className="flex items-center gap-1"><Button size="icon" variant="ghost" className="h-7 w-7" aria-label={t("administration.areas.organization.expand")} onClick={() => onToggleCollapse(widget.id)}><ChevronDown className="h-3.5 w-3.5" /></Button></div></CardHeader></Card> : <div className="relative h-full"><DashboardWidgetData widget={widget} filters={filters} title={title} editMode={editMode} index={index} total={total} onMove={onMove} onRemove={onRemove} onSettings={onSettings} onRefresh={() => setRefreshKey((value) => value + 1)} onToggleCollapse={onToggleCollapse} onDuplicate={onDuplicate} onMoveWidget={moveWidget} />{editMode ? <button type="button" className="absolute bottom-1 end-1 z-10 h-5 w-5 cursor-nwse-resize rounded border border-primary/40 bg-background/90 text-primary shadow-sm" aria-label={t("dashboard.resizeWidget")} title={t("dashboard.resizeWidget")} onPointerDown={resizeWidget} onClick={(event) => event.stopPropagation()}><span className="sr-only">{t("dashboard.resizeWidget")}</span><GripVertical className="mx-auto h-3 w-3 rotate-45" /></button> : null}</div>}</div></VisibleWidget>;
}

function DashboardWidgetData({ widget, filters, title, editMode, index, total, onMove, onRemove, onSettings, onRefresh, onToggleCollapse, onDuplicate, onMoveWidget }: { widget: WidgetInstance; filters: DashboardFilters; title: string; editMode: boolean; index: number; total: number; onMove: (index: number, direction: -1 | 1) => void; onRemove: (id: string) => void; onSettings: (widget: WidgetInstance) => void; onRefresh: () => void; onToggleCollapse: (id: string) => void; onDuplicate: (id: string) => void; onMoveWidget: (event: React.PointerEvent<HTMLButtonElement>) => void }) {
  const { user } = useAuth();
  const { context } = useManagement();
  const approvalAccess = getDashboardApprovalAccess(user?.permissions ?? []);
  const needsNotifications = widget.type === "recent-notifications";
  const needsApprovals = widget.type === "pending-approvals";
  const needsProjects = widget.type === "projects-by-status" || widget.type === "cost-variance";
  const needsCapacity = widget.type === "resource-utilization";
  const needsResources = widget.type === "resource-capacity" || widget.type === "overallocated-resources";
  const needsRisks = widget.type === "risk-trend" || widget.type === "risk-heat-map";
  const needsWorkItems = dashboardWidgetNeedsWorkItems(widget.type);
  const needsMeetingProjectScope = widget.type === "upcoming-meetings";
  const needsProjectScope = needsProjects || needsRisks || needsWorkItems || needsMeetingProjectScope;
  const hasManagedProjectScope = context?.perspective === "PortfolioManager" || context?.perspective === "ProgramManager";
  const needsManagedProjectScope = needsProjectScope && hasManagedProjectScope;
  const needsManagementOverview = shouldLoadDashboardManagementOverview(widget.type, context?.perspective) || needsManagedProjectScope;
  const query = useHomeSummary(dashboardSummaryFilters(filters), shouldLoadDashboardHomeSummary(widget.type));
  const workItems = useFullWorkItems({ page: 1, pageSize: 200, myWork: widget.type === "my-work" || widget.type === "my-todo" ? true : undefined, sortBy: "dueDate", projectId: filters.projectId }, needsWorkItems);
  const meetings = useFullMeetings(dashboardMeetingsQueryParams(filters.projectId), widget.type === "upcoming-meetings");
  const projects = useProjects({ page: 1, pageSize: 200, portfolioId: filters.portfolioId }, needsProjects || (needsProjectScope && !hasManagedProjectScope));
  const capacity = useCapacityMatrix({ periodType: "Week" }, needsCapacity);
  const resources = useResources({ page: 1, pageSize: 200, isActive: true }, needsResources);
  const risks = useRisks({ projectId: filters.projectId }, needsRisks);
  const notifications = useNotifications({ page: 1, pageSize: 50 }, needsNotifications);
  const handovers = usePendingHandoverRequestsForMe(needsApprovals && approvalAccess.handovers);
  const managerHandovers = usePendingManagerHandovers(needsApprovals && approvalAccess.handovers);
  const projectChanges = usePendingProjectChangeRequests(needsApprovals && approvalAccess.projectChanges);
  const portfolioManagement = usePortfolioManagementOverview(context?.portfolioId ?? "", { programsPage: 1, projectsPage: 1, pageSize: 100 }, needsManagementOverview && context?.perspective === "PortfolioManager");
  const programManagement = useProgramManagementOverview(context?.programId ?? "", { projectsPage: 1, projectsPageSize: 100 }, needsManagementOverview && context?.perspective === "ProgramManager");
  const { t } = useI18n();
  const directQueries = [projects, capacity, resources, risks];
  const approvalLoading = (approvalAccess.handovers && (handovers.isLoading || managerHandovers.isLoading))
    || (approvalAccess.projectChanges && projectChanges.isLoading);
  const approvalError = (approvalAccess.handovers && (handovers.isError || managerHandovers.isError))
    || (approvalAccess.projectChanges && projectChanges.isError);
  const managementQuery = context?.perspective === "PortfolioManager" ? portfolioManagement : context?.perspective === "ProgramManager" ? programManagement : null;
  const statusReports = (managementQuery?.data?.statusReports ?? undefined) as StatusReportFreshness | undefined;
  const managementResourceIds = new Set(managementQuery?.data?.resources.pressure.map((resource) => resource.resourceId) ?? []);
  const widgetResources = scopeDashboardResources(resources.data?.items ?? [], context?.perspective, managementResourceIds);
  const managedProjects = context?.perspective === "PortfolioManager"
    ? portfolioManagement.data?.projects
    : context?.perspective === "ProgramManager"
      ? programManagement.data?.projects
      : undefined;
  const projectRowsForScope: readonly { id: string; programId?: string | null }[] = managedProjects ?? projects.data?.items ?? [];
  const scopedProjectRows = scopeDashboardProjects(projectRowsForScope, filters.projectId, filters.programId, context?.perspective);
  const scopedProjectIds = new Set(scopedProjectRows.map((project) => project.id));
  const widgetProjects = needsManagedProjectScope
    ? (projects.data?.items ?? []).filter((project) => scopedProjectIds.has(project.id))
    : scopeDashboardProjects(projects.data?.items ?? [], filters.projectId, filters.programId, context?.perspective);
  const widgetWorkItems = workItems.data ? scopeDashboardRecordsByProject(workItems.data.items, scopedProjectIds) : undefined;
  const widgetMeetings = meetings.data ? scopeDashboardRecordsByProject(meetings.data.items, scopedProjectIds) : undefined;
  const widgetRisks = risks.data ? scopeDashboardRecordsByProject(risks.data.items, scopedProjectIds) : undefined;
  const listLoading = workItems.isLoading || meetings.isLoading || notifications.isLoading || approvalLoading || directQueries.some((item) => item.isLoading) || Boolean(needsManagementOverview && managementQuery?.isLoading);
  const listError = workItems.isError || meetings.isError || notifications.isError || approvalError || directQueries.some((item) => item.isError) || Boolean(needsManagementOverview && managementQuery?.isError);
  const approvals: DashboardApprovals = {
    handovers: approvalAccess.handovers ? handovers.data ?? [] : [],
    managerHandovers: approvalAccess.handovers ? managerHandovers.data ?? [] : [],
    projectChanges: approvalAccess.projectChanges ? projectChanges.data ?? [] : [],
  };
  const maxRecords = dashboardWidgetRecordLimitFor(widget.type, widget.maxRecords);
  return <Card className="flex h-full min-h-0 flex-col"><CardHeader className="flex flex-row items-center justify-between gap-2 border-b py-3"><div className="flex min-w-0 items-center gap-2">{editMode ? <button type="button" className="dashboard-widget-move drag-handle shrink-0 cursor-move touch-none text-muted-foreground hover:text-foreground" aria-label={t("dashboard.moveWidget")} title={t("dashboard.moveWidget")} onPointerDown={onMoveWidget}><GripVertical className="h-4 w-4" /></button> : null}<div className="min-w-0"><CardTitle className="truncate text-sm">{title}</CardTitle><CardDescription className="truncate text-xs">{t(definition(widget.type).descriptionKey)}</CardDescription></div></div><div className="flex shrink-0 items-center gap-1"><Button size="icon" variant="ghost" className="h-7 w-7" aria-label={t("common.retry")} onClick={onRefresh} disabled={query.isFetching || listLoading}><RefreshCw className={`h-3.5 w-3.5 ${query.isFetching || listLoading ? "animate-spin" : ""}`} /></Button><Button size="icon" variant="ghost" className="h-7 w-7" aria-label={t("administration.areas.organization.collapse")} onClick={() => onToggleCollapse(widget.id)}><ChevronUp className="h-3.5 w-3.5" /></Button>{editMode ? <><Button size="icon" variant="ghost" className="h-7 w-7" aria-label={t("common.edit")} onClick={() => onSettings(widget)}><Settings2 className="h-3.5 w-3.5" /></Button><Button size="icon" variant="ghost" className="h-7 w-7" aria-label={t("dashboard.duplicateWidget")} onClick={() => onDuplicate(widget.id)}><Copy className="h-3.5 w-3.5" /></Button><Button size="icon" variant="ghost" className="h-7 w-7 text-destructive" aria-label={t("common.delete")} onClick={() => onRemove(widget.id)}><Trash2 className="h-3.5 w-3.5" /></Button></> : null}</div></CardHeader><CardContent className="min-h-0 flex-1 overflow-auto p-3">{query.isLoading || listLoading ? <div className="h-24 animate-pulse rounded bg-muted/30" /> : query.isError || listError ? <div className="flex min-h-24 items-center justify-center text-sm text-destructive">{t("dashboard.loadFailed")}</div> : <DashboardWidgetContent type={widget.type} summary={query.data} workItems={widgetWorkItems} meetings={widgetMeetings} projects={widgetProjects} capacity={capacity.data} resources={widgetResources} risks={widgetRisks} notifications={notifications.data?.items} approvals={approvals} statusReports={statusReports} permissions={user?.permissions ?? []} maxRecords={maxRecords} />}</CardContent>{editMode ? <div className="flex items-center justify-end gap-1 border-t px-3 py-1.5"><Button size="sm" variant="ghost" disabled={index === 0} onClick={() => onMove(index, -1)}><ChevronUp className="me-1 h-3.5 w-3.5" />{t("common.previous")}</Button><Button size="sm" variant="ghost" disabled={index === total - 1} onClick={() => onMove(index, 1)}><ChevronDown className="me-1 h-3.5 w-3.5" />{t("common.next")}</Button></div> : null}</Card>;
}

const dashboardAccessLabels: Record<DashboardAccess, string> = {
  view: "dashboard.viewOnly",
  duplicate: "dashboard.canDuplicate",
  edit: "dashboard.canEdit",
  "manage-sharing": "dashboard.manageSharing",
};

export function DashboardBuilder() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { context, snapshot, loading: managementLoading, canMutate, effectiveReadOnly, switchPerspective } = useManagement();
  const queryClient = useQueryClient();
  const defaultPov = initialPointOfView(context?.perspective);
  const { views: allViews, currentView, currentViewId, pov, loaded: dashboardLoaded, setViews, setCurrentViewId, setWidgets, switchView, changePov, setFilters, setLastViewByPov } = useDashboardState(defaultPov, user ? { id: user.id, fullName: user.fullName } : undefined, user?.tenantId);
  const views = dashboardViewsForPov(allViews, pov);
  const pointOfViewViews = views;
  const widgets = currentView?.widgets ?? [];
  const filters = normalizeDashboardFilters(currentView?.filters);
  const [editMode, setEditMode] = React.useState(false);
  const [libraryOpen, setLibraryOpen] = React.useState(false);
  const [viewsOpen, setViewsOpen] = React.useState(false);
  const [settingsWidget, setSettingsWidget] = React.useState<WidgetInstance | null>(null);
  const [search, setSearch] = React.useState("");
  const [widgetCategory, setWidgetCategory] = React.useState<DashboardWidgetCategory | "All">("All");
  const [viewName, setViewName] = React.useState("");
  const [viewType, setViewType] = React.useState<DashboardType>("personal");
  const [startMode, setStartMode] = React.useState<DashboardStartMode>("blank");
  const [sourceViewId, setSourceViewId] = React.useState("");
  const [editingViewId, setEditingViewId] = React.useState<string | null>(null);
  const [editingViewName, setEditingViewName] = React.useState("");
  const [filtersOpen, setFiltersOpen] = React.useState(false);
  const canEdit = canMutateDashboard(canMutate, managementLoading, effectiveReadOnly);
  const currentViewCanEdit = Boolean(currentView && canEdit && canEditDashboardView(currentView));
  const portfoliosQuery = usePortfolios(filtersOpen);
  const programsQuery = usePrograms(filtersOpen);
  const projectsQuery = useProjects({ page: 1, pageSize: 100 }, filtersOpen);
  const availablePovs = React.useMemo<DashboardPointOfView[]>(() => {
    const mapped = (snapshot?.availablePerspectives ?? []).map((item) => {
      const labels: Record<ManagementPerspective, DashboardPointOfView> = { ProjectManager: "Project Manager", ProgramManager: "Program Manager", PortfolioManager: "Portfolio Manager" };
      return labels[item.perspective];
    });
    return mapped.length ? mapped : ["Project Manager", "Program Manager", "Portfolio Manager"];
  }, [snapshot]);

  React.useEffect(() => {
    if (currentViewCanEdit) return;
    setEditMode(false);
    setLibraryOpen(false);
    setViewsOpen(false);
    setSettingsWidget(null);
  }, [currentViewCanEdit]);

  function move(index: number, direction: -1 | 1) { setWidgets((items) => { const targetIndex = index + direction; if (targetIndex < 0 || targetIndex >= items.length) return items; const active = items[index]; const target = items[targetIndex]; if (!active || !target) return items; const arranged = resolveDashboardLayout(items, active.id, { ...active.position, x: target.position.x, y: target.position.y }); const next = [...arranged]; [next[index], next[targetIndex]] = [next[targetIndex], next[index]]; return next; }); }
  function changePovAndScope(nextPov: DashboardPointOfView) {
    changePov(nextPov);
    const perspectives: Partial<Record<DashboardPointOfView, ManagementPerspective>> = { "Project Manager": "ProjectManager", "Program Manager": "ProgramManager", "Portfolio Manager": "PortfolioManager" };
    const perspective = perspectives[nextPov];
    const availability = perspective ? snapshot?.availablePerspectives.find((item) => item.perspective === perspective) : undefined;
    if (perspective && availability) switchPerspective(perspective, availability.contexts[0]?.id);
  }
  function add(type: WidgetType) { setWidgets((items) => [...items, { id: `${type}-${Date.now()}`, type, position: defaultWidgetPosition(type, items.length) }]); setLibraryOpen(false); }
  function resizeWidget(id: string, position: WidgetPosition, layoutSnapshot?: WidgetInstance[]) { setWidgets(layoutSnapshot ? resolveDashboardLayout(layoutSnapshot, id, position) : (items) => resolveDashboardLayout(items, id, position)); }
  function toggleWidgetCollapse(id: string) { setWidgets((items) => items.map((item) => item.id === id ? { ...item, collapsed: !item.collapsed } : item)); }
  function duplicateWidget(id: string) { setWidgets((items) => { const source = items.find((item) => item.id === id); if (!source) return items; const bottom = items.reduce((max, item) => Math.max(max, item.position.y + item.position.h), 0); const copy = { ...source, id: `${source.type}-${Date.now()}-${items.length}`, title: source.title ? `${source.title} (${t("common.copy")})` : undefined, collapsed: false, position: { ...source.position, x: 0, y: bottom } }; const index = items.findIndex((item) => item.id === id); return [...items.slice(0, index + 1), copy, ...items.slice(index + 1)]; }); }
  function refreshAll() { void Promise.all([["home"], ["notifications"], ["handovers"], ["managerHandovers"], ["projectChangeRequests"], ["portfolio-management"], ["program-management"], ["work-items"], ["meetings"], ["projects"], ["capacity"], ["resources"], ["risks"], ["timeEntries"]].map((queryKey) => queryClient.invalidateQueries({ queryKey }))); }
  function reset() { setWidgets(defaultDashboardWidgetsForPov(pov)); }
  function createView(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const name = viewName.trim();
    const sourceView = views.find((view) => view.id === sourceViewId && view.pov === pov);
    if (!name || !canEdit || (startMode === "duplicate" && !sourceView)) return;
    const templateWidgets = defaultDashboardWidgetsForPov(pov);
    const view: DashboardView = {
      id: `view-${Date.now()}`,
      name,
      pov,
      ownerId: user?.id ?? "current-user",
      ownerName: user?.fullName ?? "You",
      type: viewType,
      access: "manage-sharing",
      sharedWith: [],
      widgets: createDashboardWidgetCopies(startMode, templateWidgets, sourceView?.widgets ?? []),
      filters: normalizeDashboardFilters(startMode === "duplicate" && sourceView?.filters ? sourceView.filters : undefined),
      isDefault: false,
    };
    setViews((items) => [...items, view]);
    setCurrentViewId(view.id);
    setLastViewByPov((previous) => ({ ...previous, [pov]: view.id }));
    setViewName("");
    setViewType("personal");
    setStartMode("blank");
    setSourceViewId("");
  }
  function duplicateView(source: DashboardView) { if (!canDuplicateDashboardView(source)) return; const view: DashboardView = { ...source, id: `view-${Date.now()}`, name: `${source.name} (${t("common.copy")})`, ownerId: user?.id ?? "current-user", ownerName: user?.fullName ?? "You", type: "personal", access: "manage-sharing", sharedWith: [], isDefault: false, widgets: source.widgets.map((widget) => ({ ...widget, id: `${widget.type}-${Date.now()}-${Math.random().toString(36).slice(2, 6)}` })) }; setViews((items) => [...items, view]); setCurrentViewId(view.id); setLastViewByPov((previous) => ({ ...previous, [view.pov]: view.id })); }
  function deleteView(id: string) {
    const target = views.find((view) => view.id === id);
    if (!target || target.pov !== pov || pointOfViewViews.length <= 1 || !canEditDashboardView(target)) return;
    const remaining = allViews.filter((view) => view.id !== id);
    const fallbackId = defaultDashboardViewIdForPov(remaining, pov);
    setViews(remaining);
    if (currentViewId === id && fallbackId) setCurrentViewId(fallbackId);
    setLastViewByPov((previous) => previous[target.pov] === id && fallbackId ? { ...previous, [target.pov]: fallbackId } : previous);
  }
  function renameView(id: string) { const name = editingViewName.trim(); const target = views.find((view) => view.id === id); if (!name || !target || !canEditDashboardView(target)) return; setViews((items) => items.map((view) => view.id === id ? { ...view, name } : view)); setEditingViewId(null); setEditingViewName(""); }
  function setDefaultView(id: string) { const target = views.find((view) => view.id === id); if (!target || target.pov !== pov || !canEditDashboardView(target)) return; setViews((items) => items.map((view) => view.pov === pov ? { ...view, isDefault: view.id === id } : view)); }
  function updateSharing(id: string, access: DashboardAccess) { const target = views.find((view) => view.id === id); if (!target || target.type !== "shared" || target.access !== "manage-sharing") return; setViews((items) => items.map((view) => view.id === id ? { ...view, access } : view)); }
  const widgetSearch = search.trim().toLowerCase();
  const filteredDefinitions = filterAvailableDashboardWidgets(dashboardWidgetDefinitions, pov, user?.permissions ?? [])
    .filter((item) => widgetCategory === "All" || item.category === widgetCategory)
    .filter((item) => !widgetSearch || `${t(item.titleKey)} ${t(item.descriptionKey)}`.toLowerCase().includes(widgetSearch));
  const groupedDefinitions = DASHBOARD_WIDGET_CATEGORIES
    .map((category) => ({ category, definitions: filteredDefinitions.filter((item) => item.category === category) }))
    .filter((group) => group.definitions.length > 0);
  const sourceViews = pointOfViewViews;

  if (!dashboardLoaded) return <div className="space-y-3" role="status" aria-label={t("common.loading")}><div className="h-10 animate-pulse rounded bg-muted/30" /><div className="h-32 animate-pulse rounded bg-muted/30" /><div className="h-72 animate-pulse rounded bg-muted/30" /></div>;

  return <div className="space-y-4">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <div className="flex flex-wrap items-center gap-2">
          <LayoutDashboard className="h-5 w-5 text-primary" />
          <h1 className="text-2xl font-bold">{currentView?.name ?? t("dashboard.title")}</h1>
          {currentView ? <Badge variant={currentView.type === "shared" ? "secondary" : "outline"} className="h-5 text-[10px]">
            {currentView.type === "shared" ? <Users className="h-3 w-3" /> : <Lock className="h-3 w-3" />}
            {t(currentView.type === "shared" ? "dashboard.sharedView" : "dashboard.personalView")}
          </Badge> : null}
          {!managementLoading && currentView && (!canEdit || !currentViewCanEdit) ? <Badge variant="secondary" className="h-5 text-[10px]">
            {t(currentView.access === "duplicate" ? "dashboard.canDuplicate" : "dashboard.viewOnly")}
          </Badge> : null}
        </div>
        <p className="mt-1 text-sm text-muted-foreground">{t("dashboard.description")}</p>
        <p className="mt-1 text-xs text-muted-foreground">{pov}</p>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <select className="input-field h-9 text-sm" value={pov} onChange={(event) => changePovAndScope(event.target.value as DashboardPointOfView)} aria-label={t("dashboard.pointOfView")}>
          <option value="" disabled>{t("dashboard.pointOfView")}</option>
          {availablePovs.map((option) => <option key={option}>{option}</option>)}
        </select>
        <select className="input-field h-9 max-w-52 text-sm" value={currentViewId} onChange={(event) => switchView(event.target.value)} aria-label={t("dashboard.views")}>
          <option value="" disabled>{t("dashboard.views")}</option>
          {views.map((view) => <option key={view.id} value={view.id}>{view.name}</option>)}
        </select>
        <Button size="sm" variant="outline" onClick={refreshAll} aria-label={t("dashboard.refreshAll")}><RefreshCw className="me-1.5 h-4 w-4" />{t("dashboard.refreshAll")}</Button>
        <Button size="sm" variant={filtersOpen ? "default" : "outline"} onClick={() => setFiltersOpen((value) => !value)}><Filter className="me-1.5 h-4 w-4" />{t("dashboard.filters")}</Button>
        {canEdit ? <Button size="sm" variant="outline" onClick={() => setViewsOpen(true)}><Layers className="me-1.5 h-4 w-4" />{t("dashboard.manageViews")}</Button> : null}
        <Button size="sm" variant={editMode ? "default" : "outline"} onClick={() => { if (currentViewCanEdit) setEditMode((value) => !value); }} disabled={!currentViewCanEdit}>
          <Settings2 className="me-1.5 h-4 w-4" />{editMode ? t("common.done") : t("common.edit")}
        </Button>
        {editMode && currentViewCanEdit ? <>
          <Button size="sm" variant="outline" onClick={() => setLibraryOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("dashboard.addWidget")}</Button>
          <Button size="sm" variant="ghost" onClick={reset}>{t("dashboard.reset")}</Button>
        </> : null}
      </div>
    </div>
    {filtersOpen ? <div className="grid gap-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-2 lg:grid-cols-5">
      <select className="input-field" value={filters.dateRange} onChange={(event) => setFilters({ ...filters, dateRange: event.target.value as DashboardFilters["dateRange"] })} aria-label={t("dashboard.dateRange")}>
        {DASHBOARD_DATE_RANGES.map((range) => <option key={range} value={range}>{t(`dashboard.dateRanges.${range}`)}</option>)}
      </select>
      <select className="input-field" value={filters.portfolioId ?? ""} onChange={(event) => setFilters({ ...filters, portfolioId: event.target.value || undefined, programId: undefined, projectId: undefined })} aria-label={t("dashboard.portfolio")}><option value="">{t("dashboard.allPortfolios")}</option>{(portfoliosQuery.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
      <select className="input-field" value={filters.programId ?? ""} onChange={(event) => setFilters({ ...filters, programId: event.target.value || undefined, projectId: undefined })} aria-label={t("dashboard.program")}><option value="">{t("dashboard.allPrograms")}</option>{(programsQuery.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
      <select className="input-field" value={filters.projectId ?? ""} onChange={(event) => setFilters({ ...filters, projectId: event.target.value || undefined })} aria-label={t("dashboard.project")}><option value="">{t("dashboard.allProjects")}</option>{(projectsQuery.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
      <Button variant="outline" onClick={() => setFilters(normalizeDashboardFilters({ dateRange: filters.dateRange }))}>{t("common.clearFilters")}</Button>
    </div> : null}
    <div className="dashboard-widget-grid" data-dashboard-grid>{widgets.map((widget, index) => <DashboardWidget key={widget.id} widget={widget} layoutSnapshot={widgets} filters={filters} editMode={editMode && currentViewCanEdit} index={index} total={widgets.length} onMove={move} onRemove={(id) => setWidgets((items) => items.filter((item) => item.id !== id))} onSettings={setSettingsWidget} onToggleCollapse={toggleWidgetCollapse} onDuplicate={duplicateWidget} onResize={resizeWidget} />)}</div>
    {widgets.length === 0 ? <Card><CardContent className="flex flex-col items-center gap-3 p-12 text-center"><BarChart3 className="h-10 w-10 text-muted-foreground/40" /><p className="text-sm text-muted-foreground">{t("dashboard.noWidgets")}</p>{currentViewCanEdit ? <Button size="sm" onClick={() => setLibraryOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("dashboard.addWidget")}</Button> : null}</CardContent></Card> : null}
    <SideSheet open={viewsOpen} onOpenChange={setViewsOpen} title={t("dashboard.views")} description={t("dashboard.viewsDescription")}><SideSheetBody className="space-y-4"><form onSubmit={createView} className="space-y-3 rounded-lg border p-3"><div><Label>{t("dashboard.dashboardName")}</Label><Input value={viewName} onChange={(event) => setViewName(event.target.value)} placeholder={t("dashboard.viewNamePlaceholder")} required /></div><div className="grid gap-2 sm:grid-cols-2"><div><Label>{t("dashboard.dashboardType")}</Label><select className="input-field mt-1" value={viewType} onChange={(event) => setViewType(event.target.value as DashboardType)}><option value="personal">{t("dashboard.personalView")}</option><option value="shared">{t("dashboard.sharedView")}</option></select></div><div><Label>{t("dashboard.startFrom")}</Label><select className="input-field mt-1" value={startMode} onChange={(event) => { setStartMode(event.target.value as DashboardStartMode); setSourceViewId(""); }}><option value="blank">{t("dashboard.blankDashboard")}</option><option value="template">{t("dashboard.defaultTemplate")}</option><option value="duplicate">{t("dashboard.duplicateExisting")}</option></select></div></div>{startMode === "duplicate" ? <div><Label>{t("dashboard.selectDashboard")}</Label><select className="input-field mt-1 w-full" value={sourceViewId} onChange={(event) => setSourceViewId(event.target.value)} required aria-label={t("dashboard.selectDashboard")}><option value="">{t("dashboard.selectDashboard")}</option>{sourceViews.map((view) => <option key={view.id} value={view.id}>{view.name}</option>)}</select></div> : null}<Button type="submit" size="sm" disabled={startMode === "duplicate" && !sourceViewId}><Plus className="me-1.5 h-4 w-4" />{t("dashboard.createDashboard")}</Button></form><div className="space-y-2">{views.map((view) => { const viewCanEdit = canEditDashboardView(view); const viewCanDuplicate = canDuplicateDashboardView(view); const viewCanManageSharing = view.type === "shared" && view.access === "manage-sharing"; const isEditingName = editingViewId === view.id; return <div key={view.id} className={`rounded-lg border p-3 ${view.id === currentViewId ? "border-primary/40 bg-primary/5" : ""}`}><div className="flex items-center gap-2">{isEditingName ? <Input className="h-8 min-w-0 flex-1" value={editingViewName} onChange={(event) => setEditingViewName(event.target.value)} autoFocus /> : <button type="button" className="min-w-0 flex-1 text-start" onClick={() => { switchView(view.id); setViewsOpen(false); }}><p className="flex items-center gap-1 truncate text-sm font-medium">{view.name}{view.isDefault ? ` · ${t("dashboard.defaultView")}` : ""}</p><p className="text-xs text-muted-foreground">{view.pov} · {view.widgets.length} {t("dashboard.widgets")} · {t("dashboard.owner")}: {view.ownerName}</p></button>}{isEditingName ? <><Button size="icon" variant="ghost" onClick={() => renameView(view.id)} aria-label={t("common.save")}><Check className="h-4 w-4" /></Button><Button size="icon" variant="ghost" onClick={() => setEditingViewId(null)} aria-label={t("common.cancel")}><Trash2 className="h-3.5 w-3.5" /></Button></> : <>{viewCanEdit ? <Button size="icon" variant="ghost" onClick={() => { setEditingViewId(view.id); setEditingViewName(view.name); }} aria-label={t("common.edit")}><Pencil className="h-4 w-4" /></Button> : null}{viewCanDuplicate ? <Button size="icon" variant="ghost" onClick={() => duplicateView(view)} aria-label={t("dashboard.duplicateView")}><Copy className="h-4 w-4" /></Button> : null}{viewCanEdit && !view.isDefault ? <Button size="icon" variant="ghost" onClick={() => setDefaultView(view.id)} aria-label={t("dashboard.defaultView")}><Star className="h-4 w-4" /></Button> : null}{viewCanEdit && views.length > 1 ? <Button size="icon" variant="ghost" onClick={() => deleteView(view.id)} aria-label={t("common.delete")}><Trash2 className="h-4 w-4 text-destructive" /></Button> : null}</>}</div>{view.type === "shared" ? <div className="mt-2 flex items-center justify-between gap-2 border-t pt-2"><span className="flex items-center gap-1 text-xs text-muted-foreground"><Users className="h-3.5 w-3.5" />{t("dashboard.sharedDashboardAccess")}</span><select className="input-field h-8 w-40 text-xs" value={view.access} disabled={!viewCanManageSharing} onChange={(event) => updateSharing(view.id, event.target.value as DashboardAccess)}>{(Object.keys(dashboardAccessLabels) as DashboardAccess[]).map((access) => <option key={access} value={access}>{t(dashboardAccessLabels[access])}</option>)}</select></div> : null}</div>; })}</div></SideSheetBody></SideSheet>
    <SideSheet open={libraryOpen} onOpenChange={setLibraryOpen} title={t("dashboard.widgetLibrary")} description={t("dashboard.widgetLibraryDescription")}>
      <SideSheetBody className="space-y-4">
        <Input placeholder={t("dashboard.searchWidgets")} value={search} onChange={(event) => setSearch(event.target.value)} />
        <div className="mb-1 flex flex-wrap gap-1" aria-label={t("dashboard.widgetCategories")}>
          {(["All", ...DASHBOARD_WIDGET_CATEGORIES] as const).map((category) => (
            <button key={category} type="button" aria-pressed={widgetCategory === category} onClick={() => setWidgetCategory(category)} className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <Badge variant={widgetCategory === category ? "default" : "outline"} className="cursor-pointer text-[10px]">{t(dashboardWidgetCategoryTranslationKeys[category])}</Badge>
            </button>
          ))}
        </div>
        {groupedDefinitions.map(({ category, definitions }) => (
          <section key={category} className="space-y-2">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t(dashboardWidgetCategoryTranslationKeys[category])}</h3>
            {definitions.map((item) => (
              <div key={item.type} className="flex items-center gap-3 rounded-lg border p-3">
                <Eye className="h-4 w-4 text-primary" />
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{t(item.titleKey)}</p>
                  <p className="text-xs text-muted-foreground">{t(item.descriptionKey)}</p>
                </div>
                <Button size="icon" onClick={() => add(item.type)} aria-label={t("dashboard.addWidget")}><Plus className="h-4 w-4" /></Button>
              </div>
            ))}
          </section>
        ))}
        {filteredDefinitions.length === 0 ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p> : null}
      </SideSheetBody>
    </SideSheet>
    <SideSheet open={Boolean(settingsWidget)} onOpenChange={(open) => { if (!open) setSettingsWidget(null); }} title={t("dashboard.widgetSettings")}><WidgetSettings widget={settingsWidget} onSave={(value) => { if (value) setWidgets((items) => items.map((item) => item.id === value.id ? value : item)); setSettingsWidget(null); }} onCancel={() => setSettingsWidget(null)} /></SideSheet>
  </div>;
}

function WidgetSettings({ widget, onSave, onCancel }: { widget: WidgetInstance | null; onSave: (widget: WidgetInstance | null) => void; onCancel: () => void }) {
  const { t } = useI18n();
  const [title, setTitle] = React.useState(widget?.title ?? "");
  const [maxRecords, setMaxRecords] = React.useState(String(dashboardWidgetRecordLimitFor(widget?.type ?? "", widget?.maxRecords)));
  React.useEffect(() => { setTitle(widget?.title ?? ""); setMaxRecords(String(dashboardWidgetRecordLimitFor(widget?.type ?? "", widget?.maxRecords))); }, [widget]);
  function save() {
    if (!widget) return onSave(null);
    const updated = { ...widget, title: title.trim() || undefined };
    if (dashboardWidgetHasRecordLimit(widget.type)) updated.maxRecords = Number.parseInt(maxRecords, 10) || 0;
    onSave(updated);
  }
  return <><SideSheetBody className="space-y-4">{widget ? <><div><Label>{t("dashboard.widgetTitle")}</Label><Input className="mt-1" value={title} onChange={(event) => setTitle(event.target.value)} /></div>{dashboardWidgetHasRecordLimit(widget.type) ? <div><Label>{t("dashboard.maxRecords")}</Label><Input className="mt-1" type="number" value={maxRecords} onChange={(event) => setMaxRecords(event.target.value)} /></div> : null}</> : null}</SideSheetBody><SideSheetFooter><Button variant="outline" onClick={onCancel}>{t("common.cancel")}</Button><Button onClick={save}>{t("common.save")}</Button></SideSheetFooter></>;
}
