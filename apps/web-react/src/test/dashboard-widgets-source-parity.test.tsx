import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { I18nProvider } from "../i18n/messages";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import type { WorkItemDto } from "../api/workItemsFull";
import type { MeetingListDto } from "../api/meetingsFull";
import { OverdueTasksWidget } from "../features/home/OverdueTasksWidget";
import { UpcomingMeetingsWidget } from "../features/home/UpcomingMeetingsWidget";
import { TopRisksWidget } from "../components/ui/TopRisksWidget";
import { getRiskLevelTextClass } from "../lib/status-presentation";
import {
  dashboardMeetingsQueryParams,
  dashboardWidgetNeedsWorkItems,
  formatDashboardMeetingDateTime,
  formatDashboardMoney,
  getDashboardApprovalAccess,
  scopeDashboardProjects,
  scopeDashboardRecordsByProject,
  scopeDashboardResources,
  shouldLoadDashboardManagementOverview,
  summarizeDashboardResourceCapacity,
} from "../features/home/dashboard-widget-utils";

function workItem(overrides: Partial<WorkItemDto>): WorkItemDto {
  return {
    id: "item",
    title: "Work item",
    type: "Task",
    priority: "High",
    status: "InProgress",
    source: "Manual",
    createdAt: "2026-01-01T00:00:00Z",
    createdBy: "user-1",
    ...overrides,
  };
}

const today = new Date();
const todayDate = today.toISOString().slice(0, 10);
const yesterday = new Date(today.getTime() - 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

const overdueWorkItems = [
  workItem({ id: "task-critical", title: "Overdue critical task", priority: "Critical", dueDate: `${yesterday}T12:00:00Z` }),
  workItem({ id: "task-high", title: "Overdue high task", dueDate: yesterday }),
  workItem({ id: "issue-critical", title: "Overdue issue", type: "Issue", priority: "Critical", dueDate: yesterday }),
  workItem({ id: "task-completed", title: "Completed task", priority: "Critical", status: "Completed", dueDate: yesterday }),
  workItem({ id: "task-due-today", title: "Task due today", priority: "Critical", dueDate: todayDate }),
];

const meeting: MeetingListDto = {
  id: "meeting-1",
  title: "Quarterly review",
  projectId: "project-1",
  projectName: "ERP Modernization",
  startTime: "2026-04-01T10:30:00Z",
  endTime: "2026-04-01T11:00:00Z",
  attendeeCount: 0,
};

describe("dashboard widgets source parity", () => {
  it.each([
    { locale: "en", overdueLabel: enMessages.dashboard.overdueTasks, criticalLabel: enMessages.dashboard.critical },
    { locale: "ar", overdueLabel: arMessages.dashboard.overdueTasks, criticalLabel: arMessages.dashboard.critical },
  ])("renders task-only overdue KPI in $locale", ({ locale, overdueLabel, criticalLabel }) => {
    window.localStorage.setItem("nexus-locale", locale);
    render(<I18nProvider><OverdueTasksWidget workItems={overdueWorkItems} /></I18nProvider>);

    expect(screen.getByText("2", { exact: true })).toBeInTheDocument();
    expect(screen.getByText(overdueLabel)).toBeInTheDocument();
    expect(screen.getByText(`1 ${criticalLabel}`)).toBeInTheDocument();
    expect(screen.queryByText("Overdue critical task")).not.toBeInTheDocument();
  });

  it("requests only meetings from today through the next 90 days and keeps the project filter", () => {
    expect(dashboardMeetingsQueryParams("project-1", new Date("2026-01-01T12:00:00Z"))).toEqual({
      page: 1,
      pageSize: 50,
      dateFrom: "2026-01-01",
      dateTo: "2026-04-01",
      projectId: "project-1",
    });
  });

  it("does not request work items for the Upcoming Meetings widget", () => {
    expect(dashboardWidgetNeedsWorkItems("upcoming-meetings")).toBe(false);
    for (const type of ["my-work", "my-todo", "upcoming-milestones", "overdue-tasks", "tasks-by-status", "planned-vs-actual"]) {
      expect(dashboardWidgetNeedsWorkItems(type)).toBe(true);
    }
  });

  it("matches approval queries to the exact server authorization policies", () => {
    expect(getDashboardApprovalAccess([])).toEqual({ handovers: false, projectChanges: false, any: false });
    expect(getDashboardApprovalAccess(["Handover.View", "ChangeRequest.View"]).any).toBe(false);
    expect(getDashboardApprovalAccess(["Handover.Approve"])).toEqual({ handovers: true, projectChanges: false, any: true });
    expect(getDashboardApprovalAccess(["ChangeRequest.Approve"])).toEqual({ handovers: false, projectChanges: true, any: true });
  });

  it("matches source utilization averaging by requiring both a target and current utilization", () => {
    expect(summarizeDashboardResourceCapacity([
      { utilizationTarget: 80, currentUtilization: 60 },
      { currentUtilization: 140 },
      { utilizationTarget: 90 },
      { utilizationTarget: 70, currentUtilization: 120 },
    ])).toEqual({ utilization: 90, resourceCount: 2 });
    expect(summarizeDashboardResourceCapacity([{ currentUtilization: 25 }])).toEqual({ utilization: null, resourceCount: 0 });
  });

  it.each(["PortfolioManager", "ProgramManager"])("limits resource dashboard records to management pressure IDs for %s", (perspective) => {
    const resources = [{ id: "resource-1" }, { id: "resource-2" }];
    expect(scopeDashboardResources(resources, perspective, new Set(["resource-2"]))).toEqual([{ id: "resource-2" }]);
    expect(scopeDashboardResources(resources, "ResourceManager", new Set(["resource-2"]))).toEqual(resources);
  });

  it("scopes related dashboard records through the selected and managed project IDs", () => {
    const projects = [
      { id: "project-1", programId: "program-a" },
      { id: "project-2", programId: "program-b" },
      { id: "project-3" },
    ];
    const portfolioProgramProjects = scopeDashboardProjects(projects, undefined, "program-a", "PortfolioManager");
    expect(portfolioProgramProjects.map((project) => project.id)).toEqual(["project-1"]);
    expect(scopeDashboardProjects(projects, "project-2", undefined, "ProjectManager").map((project) => project.id)).toEqual(["project-2"]);
    expect(scopeDashboardProjects(projects, undefined, "program-a", "ProgramManager")).toEqual(projects);

    const projectIds = new Set(portfolioProgramProjects.map((project) => project.id));
    expect(scopeDashboardRecordsByProject([
      { id: "risk-1", projectId: "project-1" },
      { id: "risk-2", projectId: "project-2" },
      { id: "risk-unassigned", projectId: null },
    ], projectIds)).toEqual([{ id: "risk-1", projectId: "project-1" }]);
  });

  it("loads management scope read models only for supported widgets and perspectives", () => {
    expect(shouldLoadDashboardManagementOverview("resource-capacity", "PortfolioManager")).toBe(true);
    expect(shouldLoadDashboardManagementOverview("overallocated-resources", "ProgramManager")).toBe(true);
    expect(shouldLoadDashboardManagementOverview("status-report-freshness", "ProgramManager")).toBe(true);
    expect(shouldLoadDashboardManagementOverview("resource-capacity", "ResourceManager")).toBe(false);
    expect(shouldLoadDashboardManagementOverview("cost-variance", "PortfolioManager")).toBe(false);
  });

  it("formats native-currency comparisons compactly and uses Western digits in Arabic", () => {
    expect(formatDashboardMoney(1250.5, "USD", "en")).toBe("$1.3K");
    const arabicAmount = formatDashboardMoney(1250.5, "USD", "ar");
    expect(arabicAmount).toBe("\u200f1.3\u00a0ألف\u00a0US$");
    expect(arabicAmount).not.toMatch(/[٠-٩]/);
  });

  it.each(["en", "ar"] as const)("localizes upcoming meeting time in %s and shows its project", (locale) => {
    window.localStorage.setItem("nexus-locale", locale);
    const { rerender } = render(<I18nProvider key={locale}><MemoryRouter><UpcomingMeetingsWidget meetings={[meeting]} maxRecords={8} /></MemoryRouter></I18nProvider>);

    expect(screen.getByText(formatDashboardMeetingDateTime(meeting.startTime, locale))).toBeInTheDocument();
    expect(screen.getByText("ERP Modernization")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Quarterly review/ })).toHaveAttribute("href", "/meetings/meeting-1");

    const otherLocale = locale === "en" ? "ar" : "en";
    window.localStorage.setItem("nexus-locale", otherLocale);
    rerender(<I18nProvider key={otherLocale}><MemoryRouter><UpcomingMeetingsWidget meetings={[meeting]} maxRecords={8} /></MemoryRouter></I18nProvider>);
    expect(screen.getByText(formatDashboardMeetingDateTime(meeting.startTime, otherLocale))).toBeInTheDocument();
  });

  it("renders semantic risk-level colors as text without tinted backgrounds", () => {
    const risks = ["Critical", "High", "Medium", "Low"].map((riskLevel) => ({
      id: riskLevel,
      title: `${riskLevel} risk`,
      projectName: "Core Modernization",
      riskLevel,
      status: "Open",
    }));

    render(<I18nProvider><TopRisksWidget risks={risks} /></I18nProvider>);

    for (const risk of risks) {
      const level = screen.getByText(risk.riskLevel, { exact: true });
      expect(level).toHaveClass(getRiskLevelTextClass(risk.riskLevel));
      expect(level.className).not.toMatch(/\bbg-/);
    }
  });
});
