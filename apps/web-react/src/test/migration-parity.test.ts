import { describe, expect, it } from "vitest";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import { dashboardWidgetDefinitions, defaultDashboardWidgets, defaultDashboardWidgetsForPov, resolveDashboardLayout, unavailableWidgetTypes } from "../features/home/DashboardBuilder";
import { ProjectBoardPage, ProjectGanttPage, ProjectResourceMatrixPage, ProjectTemplatesPage } from "../features/projects/ProjectWorkspaceViews";
import { RiskMatrixPage } from "../features/risks/RiskMatrixPage";
import { PortfolioOptimizationPage } from "../features/delivery/PortfolioOptimizationPage";
import { OrganizationPage } from "../features/organization/OrganizationPage";
import { navigationConfig, findActiveItem } from "../lib/navigation";

describe("React migration parity inventory", () => {
  it("keeps the original project workspace tabs available in both locales", () => {
    const required = ["overview", "plan", "team", "financials", "risksIssues", "statusReports", "meetings", "documents", "handover", "history", "snapshots", "changeTracker"];
    const enTabs = (enMessages.projects as typeof enMessages.projects).tabs as Record<string, string>;
    const arTabs = (arMessages.projects as typeof arMessages.projects).tabs as Record<string, string>;
    for (const key of required) {
      expect(typeof enTabs[key], `missing EN project tab: ${key}`).toBe("string");
      expect(typeof arTabs[key], `missing AR project tab: ${key}`).toBe("string");
    }
  });

  it("has a configurable widget registry and a persisted-layout default", () => {
    expect(dashboardWidgetDefinitions.length).toBeGreaterThanOrEqual(8);
    expect(new Set(defaultDashboardWidgets.map((widget) => widget.type)).size).toBe(defaultDashboardWidgets.length);
    expect(defaultDashboardWidgets.every((widget) => dashboardWidgetDefinitions.some((definition) => definition.type === widget.type))).toBe(true);
    expect(defaultDashboardWidgets.every((widget) => widget.position.w >= 1 && widget.position.h >= 2)).toBe(true);
    expect(defaultDashboardWidgets.some((widget) => widget.position.w > 1)).toBe(true);
  });

  it("moves widgets by saved grid coordinates and prevents overlap", () => {
    const first = { ...defaultDashboardWidgets[0]!, position: { x: 0, y: 0, w: 3, h: 2 } };
    const second = { ...defaultDashboardWidgets[1]!, position: { x: 3, y: 0, w: 3, h: 2 } };
    const layout = resolveDashboardLayout([first, second], first.id, { x: 3, y: 0, w: 3, h: 2 });
    expect(layout[0]?.position).toEqual({ x: 3, y: 0, w: 3, h: 2 });
    expect(layout[1]?.position).toEqual({ x: 3, y: 2, w: 3, h: 2 });
  });

  it("keeps the Next dashboard widget catalog represented in React", () => {
    const goldenMasterTypes = [
      "active-projects", "projects-at-risk", "total-budget", "forecast-variance", "open-risks", "overdue-tasks",
      "projects-attention", "my-todo", "upcoming-milestones", "top-risks", "upcoming-meetings", "pending-approvals",
      "recent-notifications", "projects-by-health", "projects-by-status", "budget-vs-actual", "resource-utilization",
      "risk-trend", "tasks-by-status", "resource-capacity", "overallocated-resources", "planned-vs-actual", "cost-variance",
      "risk-heat-map", "status-report-freshness", "my-timesheet-status", "hours-this-week", "missing-timesheets",
      "timesheet-approval-inbox", "jira-sync", "ms-project-import", "integration-health", "ai-recommendations", "delayed-predictions",
    ];
    const actual = new Set<string>(dashboardWidgetDefinitions.map((definition) => definition.type));
    for (const type of goldenMasterTypes) expect(actual.has(type), `missing React dashboard widget: ${type}`).toBe(true);
  });

  it("keeps POV-specific default dashboard layouts represented", () => {
    expect(defaultDashboardWidgetsForPov("Team Member").map((widget) => widget.type)).toEqual(["my-todo", "upcoming-milestones", "upcoming-meetings", "overdue-tasks"]);
    expect(defaultDashboardWidgetsForPov("Resource Manager").map((widget) => widget.type)).toEqual(["resource-utilization", "resource-capacity", "overallocated-resources", "planned-vs-actual"]);
    expect(defaultDashboardWidgetsForPov("Finance Controller").map((widget) => widget.type)).toEqual(["total-budget", "budget-vs-actual", "cost-variance", "top-risks"]);
  });

  it("connects source-backed Home widgets instead of marking them unavailable", () => {
    const sourceBacked = [
      "overdue-tasks", "projects-by-status", "resource-utilization", "risk-trend", "tasks-by-status",
      "resource-capacity", "overallocated-resources", "planned-vs-actual", "cost-variance", "risk-heat-map",
      "pending-approvals", "recent-notifications", "status-report-freshness",
    ];
    for (const type of sourceBacked) expect(unavailableWidgetTypes.has(type as never), `${type} should use a real source`).toBe(false);
  });

  it("keeps approvals in the side menu without duplicating notifications there", () => {
    const homeItems = navigationConfig.find((section) => section.id === "home")?.items ?? [];
    expect(homeItems.some((item) => item.id === "approvals")).toBe(true);
    expect(homeItems.some((item) => item.id === "notifications")).toBe(false);
    expect(findActiveItem(homeItems, "/my-work/approvals")?.id).toBe("approvals");
  });

  it("exposes real project workspace view components for the migrated deep links", () => {
    expect(typeof ProjectBoardPage).toBe("function");
    expect(typeof ProjectGanttPage).toBe("function");
    expect(typeof ProjectResourceMatrixPage).toBe("function");
    expect(typeof ProjectTemplatesPage).toBe("function");
  });

  it("exposes API-backed risk matrix and portfolio optimization route components", () => {
    expect(typeof RiskMatrixPage).toBe("function");
    expect(typeof PortfolioOptimizationPage).toBe("function");
    expect((enMessages.risks as Record<string, string>).matrixTitle).toBe("Risk Matrix");
    expect((arMessages.risks as Record<string, string>).matrixTitle).toBe("مصفوفة المخاطر");
  });

  it("keeps the Organization chart/table parity surface available in both locales", () => {
    expect(typeof OrganizationPage).toBe("function");
    const enOrganization = (enMessages.administration as typeof enMessages.administration).areas.organization as Record<string, string>;
    const arOrganization = (arMessages.administration as typeof arMessages.administration).areas.organization as Record<string, string>;
    for (const key of ["chartView", "tableView", "structureMode", "resourcesMode", "typeFilter", "resourceFilter"]) {
      expect(typeof enOrganization[key], `missing EN Organization key: ${key}`).toBe("string");
      expect(typeof arOrganization[key], `missing AR Organization key: ${key}`).toBe("string");
    }
  });
});
