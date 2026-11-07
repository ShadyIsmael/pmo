import { describe, expect, it } from "vitest";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import {
  DASHBOARD_WIDGET_CATEGORIES,
  DASHBOARD_WIDGETS_WITH_RECORD_LIMIT,
  DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT,
  dashboardWidgetCategoryTranslationKeys,
  dashboardWidgetHasRecordLimit,
  dashboardWidgetRecordLimitOrDefault,
  dashboardWidgetRecordLimitFor,
  filterAvailableDashboardWidgets,
  type DashboardWidgetAvailabilityDefinition,
} from "../features/home/dashboard-widget-availability";
import { dashboardWidgetDefinitions, shouldLoadDashboardHomeSummary, unavailableWidgetTypes } from "../features/home/DashboardBuilder";
import { Permissions } from "../lib/permissions";

function hasMessage(messages: unknown, key: string): boolean {
  const value = key.split(".").reduce<unknown>((current, part) => {
    if (!current || typeof current !== "object" || !(part in current)) return undefined;
    return (current as Record<string, unknown>)[part];
  }, messages);
  return typeof value === "string";
}

describe("dashboard widget availability", () => {
  it("filters the catalog by the active point of view", () => {
    const projectManagerWidgets = filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", []);
    const projectManagerTypes = projectManagerWidgets.map(({ type }) => type);
    expect(projectManagerTypes).toContain("projects-attention");
    expect(projectManagerTypes).not.toContain("projects-by-status");
    expect(projectManagerTypes).not.toContain("jira-sync");
    expect(projectManagerTypes).not.toContain("critical-risks");
    expect(projectManagerTypes).not.toContain("my-work");

    const programManagerTypes = filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Program Manager", []).map(({ type }) => type);
    expect(programManagerTypes).toContain("projects-by-status");
  });

  it("honors required permissions and hides widgets whose plugin is not connected", () => {
    const definitions: DashboardWidgetAvailabilityDefinition[] = [
      { type: "open", supportedPOVs: ["Project Manager"] },
      { type: "permissioned", supportedPOVs: ["Project Manager"], requiredPermission: "StatusReport.Export" },
      { type: "plugin", supportedPOVs: ["Project Manager"], requiredPlugin: "jira" },
    ];

    expect(filterAvailableDashboardWidgets(definitions, "Project Manager", []).map(({ type }) => type)).toEqual(["open"]);
    expect(filterAvailableDashboardWidgets(definitions, "Project Manager", ["StatusReport.Export"]).map(({ type }) => type)).toEqual(["open", "permissioned"]);
  });

  it("requires one valid time-tracking permission before offering Hours This Week", () => {
    const widget = dashboardWidgetDefinitions.find(({ type }) => type === "hours-this-week");
    const viewPermissions = [Permissions.TimeTracking.ViewOwn, Permissions.TimeTracking.ViewTeam];
    expect(widget?.requiredAnyPermissions).toEqual(viewPermissions);
    expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", []).map(({ type }) => type)).not.toContain("hours-this-week");
    for (const permission of viewPermissions) {
      expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", [permission]).map(({ type }) => type)).toContain("hours-this-week");
    }
    for (const permission of [Permissions.TimeTracking.ManageOwn, Permissions.TimeTracking.ManageTeam]) {
      expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", [permission]).map(({ type }) => type)).not.toContain("hours-this-week");
    }
  });

  it("offers Pending Approvals only to users allowed to approve at least one included request type", () => {
    const widget = dashboardWidgetDefinitions.find(({ type }) => type === "pending-approvals");
    const requiredPermissions = [Permissions.Handover.Approve, Permissions.ChangeRequest.Approve];
    expect(widget?.requiredAnyPermissions).toEqual(requiredPermissions);
    expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", []).map(({ type }) => type)).not.toContain("pending-approvals");
    for (const permission of requiredPermissions) {
      expect(filterAvailableDashboardWidgets(dashboardWidgetDefinitions, "Project Manager", [permission]).map(({ type }) => type)).toContain("pending-approvals");
    }
  });

  it("does not fetch Home summary data for widgets whose source data is unavailable", () => {
    for (const type of unavailableWidgetTypes) {
      expect(shouldLoadDashboardHomeSummary(type), `${type} should not request the Home summary`).toBe(false);
    }
    expect(shouldLoadDashboardHomeSummary("active-projects")).toBe(true);
    expect(shouldLoadDashboardHomeSummary("hours-this-week")).toBe(false);
    expect(shouldLoadDashboardHomeSummary("pending-approvals")).toBe(false);
  });

  it("keeps record limits for configurable widgets", () => {
    expect(DASHBOARD_WIDGETS_WITH_RECORD_LIMIT).toEqual([
      "projects-attention",
      "my-todo",
      "upcoming-milestones",
      "top-risks",
      "upcoming-meetings",
      "pending-approvals",
      "recent-notifications",
    ]);
    expect(DASHBOARD_WIDGETS_WITH_RECORD_LIMIT.every((type) => dashboardWidgetHasRecordLimit(type))).toBe(true);
    expect(dashboardWidgetHasRecordLimit("projects-by-health")).toBe(false);
    expect(dashboardWidgetHasRecordLimit("overdue-tasks")).toBe(false);
    expect(DEFAULT_DASHBOARD_WIDGET_RECORD_LIMIT).toBe(10);
    expect(dashboardWidgetRecordLimitOrDefault(undefined)).toBe(10);
    expect(dashboardWidgetRecordLimitOrDefault(undefined, 8)).toBe(8);
    expect(dashboardWidgetRecordLimitOrDefault(0)).toBe(10);
    expect(dashboardWidgetRecordLimitOrDefault(4)).toBe(4);
    expect(dashboardWidgetRecordLimitFor("upcoming-meetings", undefined)).toBe(8);
    expect(dashboardWidgetRecordLimitFor("upcoming-meetings", 4)).toBe(4);
    expect(dashboardWidgetRecordLimitFor("projects-attention", undefined)).toBe(10);
  });

  it("keeps the dashboard category order and resolves every dynamic widget label in EN and AR", () => {
    expect(DASHBOARD_WIDGET_CATEGORIES).toEqual([
      "Overview", "Projects", "Portfolios", "My Work", "Schedule", "Resources", "Timesheets",
      "Costs", "Risks", "Meetings", "Approvals", "Notifications", "Integrations", "AI",
    ]);
    expect(dashboardWidgetDefinitions.find(({ type }) => type === "projects-at-risk")?.category).toBe("Overview");
    expect(dashboardWidgetDefinitions.find(({ type }) => type === "budget-vs-actual")?.category).toBe("Costs");

    const dynamicKeys = [
      ...Object.values(dashboardWidgetCategoryTranslationKeys),
      ...dashboardWidgetDefinitions.flatMap(({ titleKey, descriptionKey }) => [titleKey, descriptionKey]),
    ];
    for (const key of dynamicKeys) {
      expect(hasMessage(enMessages, key), `missing EN widget-library message: ${key}`).toBe(true);
      expect(hasMessage(arMessages, key), `missing AR widget-library message: ${key}`).toBe(true);
    }
  });
});
