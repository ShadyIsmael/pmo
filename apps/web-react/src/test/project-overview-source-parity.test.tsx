import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import userEvent from "@testing-library/user-event";
import { I18nProvider } from "../i18n/messages";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";
import { ApiError } from "../api/client";

const fixtures = vi.hoisted(() => ({
  query: { isLoading: false, isError: false, data: undefined as unknown, refetch: vi.fn() },
  reportingConfiguration: { isLoading: false, isError: false, data: undefined as unknown, refetch: vi.fn() },
  performance: { isLoading: false, isError: false, data: undefined as unknown },
  teamCapacity: { isLoading: false, isError: false, data: undefined as unknown, refetch: vi.fn() },
  capacityHook: vi.fn(),
  permissions: ["Planning.View", "Project.View", "Resource.View"] as string[],
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjectOverview: () => fixtures.query,
  useProjectPerformance: () => fixtures.performance,
  useProjectReportingConfiguration: () => fixtures.reportingConfiguration,
  useProjectTeamCapacity: (...args: unknown[]) => {
    fixtures.capacityHook(...args);
    return fixtures.teamCapacity;
  },
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "viewer-1", permissions: fixtures.permissions } }) }));

const project = {
  id: "project-1",
  publicId: "project-public-1",
  code: "PRJ-0042",
  name: "Cloud Modernization",
  status: "Active",
  priority: "High",
  health: "Amber",
  ragStatus: "Amber",
  currency: "USD",
  progress: 35,
  members: [],
  createdAt: "2026-10-01T00:00:00.000Z",
};

const overview = {
  progress: 35,
  program: { id: "program-1", name: "Modernization", code: "PRG-1" },
  currentBaseline: { id: "baseline-1", name: "Approved baseline", version: 1, isCurrent: true },
  hasBudgetData: true,
  hasCostEntryData: true,
  risks: {
    openCount: 1,
    highCriticalOpenCount: 1,
    statusCounts: [{ status: "Identified", count: 1 }],
    levelCounts: [{ riskLevel: "High", count: 1, displayOrder: 2 }],
    topRisks: [{ id: "risk-1", title: "Supplier delay", riskLevel: "High", status: "Identified" }],
  },
  issues: {
    openIssueCount: 1,
    overdueOpenIssueCount: 0,
    topIssues: [{ id: "issue/1", title: "Deployment issue", status: "Open", priority: "High", dueDate: "2026-11-01" }],
  },
  milestones: [{ id: "milestone-1", name: "Pilot launch", dueDate: "2026-12-01", status: "Active", progress: 10 }],
  pendingChangeRequestCount: 0,
  pendingHandoverCount: 0,
  allocations: { activeAllocationCount: 1, activeResourceCount: 1, allocatedHoursPerWeek: 20 },
  meetings: { meetingCount: 1, finalMinutesCount: 0 },
  plannedValue: 100,
  earnedValue: 80,
  actualCost: 90,
  currency: "USD",
  scheduleVariance: -5,
  costVariance: -2,
  schedulePerformanceIndex: 0.9,
  costPerformanceIndex: 0.95,
  approvedBudget: 1000,
  originalApprovedBudget: 1100,
  committedCost: 40,
  forecastAtCompletion: 980,
  remainingBudget: 100,
  budgetUtilizationPercentage: 90,
  hasPendingRevision: true,
  statusReports: {
    totalCount: 1,
    publishedCount: 1,
    latestPublishedReport: {
      id: "report-1",
      reportDate: "2026-09-01",
      reportState: "Published",
      periodFrom: "2026-08-01",
      periodTo: "2026-08-31",
      projectLifecycleStatusAtReport: "Active",
      overallHealth: "Amber",
      overallProgressPercent: 35,
      executiveSummary: "Delivery remains under control.",
      publishedAt: "2026-09-02",
    },
  },
};

const teamCapacityOverview = {
  startDate: "2026-10-01",
  endDate: "2026-11-01",
  activeResourceCount: 2,
  idleResourceCount: 1,
  totalCapacityHours: 80,
  utilizedHours: 40,
  availableHours: 40,
  overallocatedHours: 0,
  utilizedPercent: 50,
  availablePercent: 50,
  isCapacityDataAvailable: true,
  isAllocationDataAvailable: true,
};

function ProjectOutlet() {
  return <Outlet context={{ projectId: project.id, project }} />;
}

function renderOverview() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectOutlet />}>
            <Route path="overview" element={<ProjectOverviewTab />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("project overview source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.query = { isLoading: false, isError: false, data: overview, refetch: vi.fn() };
    fixtures.permissions = ["Planning.View", "Project.View", "Resource.View"];
    fixtures.reportingConfiguration = { isLoading: false, isError: false, data: undefined, refetch: vi.fn() };
    fixtures.performance = { isLoading: false, isError: false, data: undefined };
    fixtures.teamCapacity = { isLoading: false, isError: false, data: teamCapacityOverview, refetch: vi.fn() };
    fixtures.capacityHook.mockClear();
  });

  it("uses the public project identifier when opening the issue register", () => {
    renderOverview();

    const issueLink = screen.getAllByRole("link", { name: "View all" })
      .find((link) => link.getAttribute("href")?.startsWith("/risks-issues/issues?projectId="));

    expect(issueLink).toHaveAttribute("href", "/risks-issues/issues?projectId=project-public-1");
  });


  it("uses project progress when the overview has no plan progress, matching Next", () => {
    fixtures.query = { isLoading: false, isError: false, data: { ...overview, progress: 78, planProgress: null }, refetch: vi.fn() };
    renderOverview();

    expect(screen.getByRole("img", { name: "Project record progress: 35%" })).toBeInTheDocument();
  });

  it("distinguishes active-plan progress from the published snapshot", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        planProgress: 0,
        statusDate: "2026-10-19",
        statusReports: {
          ...overview.statusReports,
          latestPublishedReport: {
            ...overview.statusReports.latestPublishedReport,
            periodTo: "2026-10-31",
            overallProgressPercent: 73,
          },
        },
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByRole("img", { name: "Active Working plan progress: 0%" })).toBeInTheDocument();
    expect(screen.getByText("-73 pp")).toBeInTheDocument();
    expect(screen.getByText("vs published report (73%)")).toBeInTheDocument();
  });

  it("localizes the progress sources in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        planProgress: 0,
        statusDate: "2026-10-19",
        statusReports: {
          ...overview.statusReports,
          latestPublishedReport: {
            ...overview.statusReports.latestPublishedReport,
            periodTo: "2026-10-31",
            overallProgressPercent: 73,
          },
        },
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByRole("img", { name: "التقدم حسب خطة العمل النشطة: 0%" })).toBeInTheDocument();
    expect(screen.getByText("مقارنة بالتقرير المنشور (73%)")).toBeInTheDocument();
  });

  it("renders the complete golden-copy overview information sections", () => {
    renderOverview();

    const renderedText = document.body.textContent?.toLocaleLowerCase() ?? "";
    for (const heading of [
      "Project Information", "KPI Summary", "Milestones",
      "Team, resources & allocation", "Governance & Reporting", "Risks overview",
    ]) {
      expect(renderedText).toContain(heading.toLocaleLowerCase());
    }
  });

  it("shows one capacity donut and exactly four API-backed KPI cards", () => {
    renderOverview();

    const card = screen.getByTestId("project-team-capacity-kpis");
    expect(card.children).toHaveLength(4);
    expect(within(card).getByText("Team members")).toBeInTheDocument();
    expect(within(card).getByText("Assigned resources")).toBeInTheDocument();
    expect(within(card).getByText("Idle resources")).toBeInTheDocument();
    expect(within(card).getByText("No allocated work")).toBeInTheDocument();
    expect(screen.getByText("80.0 hours")).toBeInTheDocument();
    expect(within(card).getAllByText("50.0%", { exact: true })).toHaveLength(2);
    expect(screen.getByRole("img", { name: /Capacity distribution/ })).toBeInTheDocument();
    expect(within(card).getByText("Available capacity")).toBeInTheDocument();
  });

  it("keeps four KPI cards visible while capacity data is loading or unavailable", () => {
    fixtures.teamCapacity = { isLoading: true, isError: false, data: undefined, refetch: vi.fn() };
    const { rerender } = renderOverview();

    expect(screen.getByTestId("project-team-capacity-kpis").children).toHaveLength(4);
    expect(screen.getByRole("status", { name: "Loading..." })).toBeInTheDocument();

    fixtures.teamCapacity = { isLoading: false, isError: true, data: undefined, refetch: vi.fn() };
    rerender(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="overview" element={<ProjectOverviewTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );
    expect(screen.getByTestId("project-team-capacity-kpis").children).toHaveLength(4);
    expect(screen.getByText("Capacity data unavailable")).toBeInTheDocument();
  });

  it("does not request or render team capacity without Resource.View", () => {
    fixtures.permissions = ["Project.View"];
    fixtures.teamCapacity = { isLoading: false, isError: false, data: teamCapacityOverview, refetch: vi.fn() };
    renderOverview();

    expect(fixtures.capacityHook).toHaveBeenCalledWith("project-1", "Month", expect.any(String), expect.any(String), false);
    expect(screen.getByText("Team capacity is unavailable for your current access or project scope.")).toBeInTheDocument();
    expect(screen.getByTestId("project-team-capacity-kpis")).toHaveTextContent("—");
    expect(screen.queryByText("80.0 hours")).not.toBeInTheDocument();
  });

  it("handles a fresh server 403 without offering an unauthorized retry or showing cached capacity", () => {
    const refetch = vi.fn();
    fixtures.teamCapacity = {
      isLoading: false,
      isError: true,
      data: teamCapacityOverview,
      refetch,
    };
    Object.assign(fixtures.teamCapacity, { error: new ApiError(403, "Forbidden") });
    renderOverview();

    expect(screen.getByText("Team capacity is unavailable for your current access or project scope.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Retry" })).not.toBeInTheDocument();
    expect(screen.getByTestId("project-team-capacity-kpis")).toHaveTextContent("—");
    expect(screen.queryByText("80.0 hours")).not.toBeInTheDocument();
    expect(refetch).not.toHaveBeenCalled();
  });

  it("keeps the capacity donut visible as an empty ring when calendar data is incomplete", () => {
    fixtures.teamCapacity = {
      isLoading: false,
      isError: false,
      data: {
        ...teamCapacityOverview,
        totalCapacityHours: null,
        utilizedHours: null,
        availableHours: null,
        utilizedPercent: null,
        availablePercent: null,
        isCapacityDataAvailable: false,
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByRole("img", { name: "Capacity distribution unavailable" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Capacity data unavailable" }))
      .toHaveAttribute("aria-describedby", "project-team-capacity-unavailable-tooltip");
    expect(screen.getByRole("tooltip")).toHaveTextContent("A working calendar or allocation status is incomplete, so capacity totals and idle counts cannot be confirmed.");
    expect(screen.getByTestId("project-team-capacity-kpis")).toHaveTextContent("—");
    expect(screen.getByTestId("project-team-capacity-kpis").children).toHaveLength(4);
  });

  it("does not report idle resources when allocation data is incomplete", () => {
    fixtures.teamCapacity = {
      isLoading: false,
      isError: false,
      data: { ...teamCapacityOverview, idleResourceCount: null, isAllocationDataAvailable: false },
      refetch: vi.fn(),
    };
    renderOverview();

    const card = screen.getByTestId("project-team-capacity-kpis");
    const idleMetric = within(card).getByText("Idle resources").parentElement;
    expect(idleMetric).toHaveTextContent("—");
  });

  it("shows an explicit empty state and four zero-valued KPI cards when the project team is empty", () => {
    fixtures.teamCapacity = {
      isLoading: false,
      isError: false,
      data: {
        ...teamCapacityOverview,
        activeResourceCount: 0,
        idleResourceCount: 0,
        totalCapacityHours: 0,
        utilizedHours: 0,
        availableHours: 0,
        utilizedPercent: 0,
        availablePercent: 0,
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByText("No team members assigned to this project.")).toBeInTheDocument();
    const kpis = screen.getByTestId("project-team-capacity-kpis");
    expect(kpis.children).toHaveLength(4);
    expect(within(kpis).getAllByText("0", { exact: true })).toHaveLength(2);
  });

  it("caps the donut at total capacity and reports overallocation separately", () => {
    fixtures.teamCapacity = {
      isLoading: false,
      isError: false,
      data: {
        ...teamCapacityOverview,
        totalCapacityHours: 40,
        utilizedHours: 50,
        availableHours: 0,
        overallocatedHours: 10,
        utilizedPercent: 125,
        availablePercent: 0,
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByText(/Allocations exceed effective capacity by 10\.0 hours\./)).toBeInTheDocument();
    expect(screen.getByText("50.0 hours")).toBeInTheDocument();
    expect(screen.getByText("125.0%")).toBeInTheDocument();
    expect(screen.getByText("40.0 hours")).toBeInTheDocument();
  });

  it("requests capacity for the selected reporting period", async () => {
    const user = userEvent.setup();
    renderOverview();

    await user.click(screen.getByRole("button", { name: "Reporting Period" }));
    await user.click(screen.getByRole("option", { name: "This week" }));

    expect(fixtures.capacityHook).toHaveBeenLastCalledWith(
      project.id,
      "Week",
      expect.any(String),
      expect.any(String),
      true,
    );
  });

  it("keeps KPI Performance limited to the existing progress chart and published-state comparison", () => {
    renderOverview();

    const row = screen.getByTestId("project-overview-kpi-performance-row");
    const summary = within(row).getByRole("region", { name: "KPI Summary" });

    expect(within(summary).getByRole("img", { name: "Project record progress: 35%" })).toBeInTheDocument();
    expect(within(summary).getByText("+0 pp")).toBeInTheDocument();
    expect(within(summary).queryByText("Schedule Variance")).not.toBeInTheDocument();
    expect(within(summary).queryByText("Cost Variance")).not.toBeInTheDocument();
    expect(within(summary).queryByText("Overdue open issues")).not.toBeInTheDocument();
    expect(within(summary).queryByText("Critical attention")).not.toBeInTheDocument();
    expect(within(summary).queryByText("high/critical risks")).not.toBeInTheDocument();
  });

  it("shows issue totals, per-status counts, percentages, and configured chart colors", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        issues: {
          ...overview.issues,
          statusCounts: [
            { status: "ToDo", count: 3, nameEn: "To Do", nameAr: "للتنفيذ", displayOrder: 10, color: "blue" },
            { status: "InProgress", count: 1, nameEn: "In Progress", nameAr: "قيد التنفيذ", displayOrder: 20, color: "amber" },
            { status: "Done", count: 2, nameEn: "Done", nameAr: "مكتمل", displayOrder: 30, color: "emerald" },
          ],
        },
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByRole("img", { name: "Issues by status: Total issues 6" })).toBeInTheDocument();
    const legend = screen.getByRole("list", { name: "Issues by status" });
    const toDoRow = within(legend).getByText("To Do").closest("li");
    expect(toDoRow).toHaveTextContent(/3\s+\(50%\)/);
    expect(within(legend).getByText("In Progress").closest("li")).toHaveTextContent(/1\s+\(17%\)/);
    expect(within(legend).getByText("Done").closest("li")).toHaveTextContent(/2\s+\(33%\)/);
    expect(toDoRow?.querySelector("[aria-hidden='true']")).toHaveStyle({ backgroundColor: "hsl(var(--info))" });
  });

  it("offers project-scoped View all links for both Issues and Risks", () => {
    renderOverview();

    const viewAllLinks = screen.getAllByRole("link", { name: "View all" });
    const issueLink = viewAllLinks.find((link) => link.getAttribute("href")?.startsWith("/risks-issues/issues?projectId="));
    const riskLink = viewAllLinks.find((link) => link.getAttribute("href")?.startsWith("/risks-issues/risks?projectId="));

    expect(issueLink).toHaveAttribute("href", "/risks-issues/issues?projectId=project-public-1");
    expect(riskLink).toHaveAttribute("href", "/risks-issues/risks?projectId=project-public-1");
  });

  it("shows one non-closed risk status donut with count, percent, and mitigated risks", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        risks: {
          ...overview.risks,
          openCount: 3,
          statusCounts: [
            { status: "Identified", count: 1 },
            { status: "Mitigated", count: 2 },
          ],
          topRisks: [
            { id: "risk-1", title: "Supplier delay", riskLevel: "High", status: "Identified" },
            { id: "risk-2", title: "Mitigated but open", riskLevel: "Medium", status: "Mitigated" },
          ],
        },
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByRole("img", { name: "Risks by status: Non-closed risks 3" })).toBeInTheDocument();
    const legend = screen.getByRole("list", { name: "Risks by status" });
    expect(within(legend).getByText("Identified")).toBeInTheDocument();
    expect(within(legend).getByText("Mitigated")).toBeInTheDocument();
    expect(within(legend).getByText("67%")).toBeInTheDocument();
    expect(within(legend).queryByText("Closed")).not.toBeInTheDocument();
    expect(screen.getAllByRole("img", { name: /Risks by status/ })).toHaveLength(1);
  });

  it("localizes the risk status donut and follows RTL in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        risks: {
          ...overview.risks,
          openCount: 2,
          statusCounts: [
            { status: "Identified", count: 1 },
            { status: "Mitigated", count: 1 },
          ],
        },
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByRole("img", { name: /المخاطر حسب الحالة: المخاطر غير المغلقة/ })).toBeInTheDocument();
    const legend = screen.getByRole("list", { name: "المخاطر حسب الحالة" });
    expect(within(legend).getByText("تم التعرف عليه")).toBeInTheDocument();
    expect(within(legend).getByText("تمت المعالجة")).toBeInTheDocument();
  });

  it("shows an explicit empty state when every project risk is formally closed", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: { ...overview, risks: { ...overview.risks, openCount: 0, statusCounts: [], topRisks: [] } },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getByText("No risks remain open.")).toBeInTheDocument();
    expect(screen.queryByRole("img", { name: /Risks by status/ })).not.toBeInTheDocument();
  });

  it("hides Project Plan links when the viewer lacks Planning.View", () => {
    fixtures.permissions = [];
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        milestones: [{ id: "milestone-1", name: "Upcoming milestone", dueDate: "2026-12-01", progress: 0 }],
      },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(document.querySelector(`a[href="/projects/${project.id}/plan"]`)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Plan" })).not.toBeInTheDocument();
    expect(screen.queryAllByText("Upcoming milestone").length).toBeGreaterThan(0);
    expect(screen.queryByRole("link", { name: /Upcoming milestone/ })).not.toBeInTheDocument();
  });
  it("shows only the first five project-plan milestones and links each item to that plan", () => {
    const items = Array.from({ length: 6 }, (_, index) => ({
      id: `milestone-${index + 1}`,
      name: `Upcoming milestone ${index + 1}`,
      dueDate: `2026-12-${String(index + 1).padStart(2, "0")}`,
      progress: index === 0 ? 0 : 25,
    }));
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: { ...overview, statusDate: "2026-10-26", milestones: items },
      refetch: vi.fn(),
    };
    renderOverview();

    for (const item of items.slice(0, 5)) {
      const milestoneLink = screen.getByRole("link", { name: new RegExp(item.name) });
      expect(milestoneLink).toHaveAttribute("href", "/projects/project-1/plan");
    }
    expect(screen.queryByText("Upcoming milestone 6")).not.toBeInTheDocument();
  });
  it("places KPI Performance, Milestones, and Governance in one three-column row without Change Requests", () => {
    renderOverview();

    const panels = screen.getByTestId("project-overview-core-panels");

    expect(panels).toHaveClass("grid");
    expect(panels).toHaveClass("md:grid-cols-2");
    expect(panels).toHaveClass("xl:grid-cols-3");
    expect(panels.children).toHaveLength(3);
    expect(within(panels).getByRole("heading", { name: "KPI Summary" })).toBeInTheDocument();
    expect(within(panels).getByRole("heading", { name: "Milestones" })).toBeInTheDocument();
    expect(within(panels).getByRole("heading", { name: /^Governance & Reporting$/i })).toBeInTheDocument();
    expect(within(panels).queryByRole("heading", { name: "Change Requests" })).not.toBeInTheDocument();
    expect(within(panels).getAllByRole("link", { name: "View all" }).some((link) => link.getAttribute("href") === "/projects/project-1/plan")).toBe(true);
  });


  it("keeps EVM and issue details out of KPI Performance when comparison data is unavailable", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        planProgress: 0,
        plannedValue: null,
        earnedValue: null,
        scheduleVariance: null,
        costVariance: null,
        plannedValueUnavailableReason: "NO_BASELINE_ASSIGNMENTS",
        earnedValueUnavailableReason: "NO_BASELINE_ASSIGNMENTS",
        schedulePerformanceIndexUnavailableReason: "NO_BASELINE_ASSIGNMENTS",
      },
      refetch: vi.fn(),
    };
    renderOverview();

    const summary = screen.getByRole("region", { name: "KPI Summary" });
    expect(within(summary).getByRole("img", { name: "Active Working plan progress: 0%" })).toBeInTheDocument();
    expect(within(summary).queryByText("Schedule Variance")).not.toBeInTheDocument();
    expect(within(summary).queryByText("The approved baseline has no resource assignments.")).not.toBeInTheDocument();
  });

  it("preserves the chart-only KPI card in Arabic and RTL", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        planProgress: 0,
        plannedValue: null,
        scheduleVariance: null,
        schedulePerformanceIndexUnavailableReason: "NO_BASELINE_ASSIGNMENTS",
      },
      refetch: vi.fn(),
    };
    renderOverview();

    const summary = screen.getByRole("region", { name: "ملخص مؤشرات الأداء" });
    expect(within(summary).getByRole("img", { name: "التقدم حسب خطة العمل النشطة: 0%" })).toBeInTheDocument();
    expect(within(summary).queryByText("الانحراف عن الجدول")).not.toBeInTheDocument();
  });
  it("does not fall back to manually stored project EVM values when canonical overview values are absent", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        approvedBudget: 2000,
        plannedValue: null,
        earnedValue: null,
        actualCost: null,
        schedulePerformanceIndex: null,
        costPerformanceIndex: null,
        plannedValueUnavailableReason: "NO_APPROVED_BASELINE",
        earnedValueUnavailableReason: "NO_APPROVED_BASELINE",
        actualCostUnavailableReason: "NO_ACTUAL_COST_DATA",
        schedulePerformanceIndexUnavailableReason: "NO_APPROVED_BASELINE",
        costPerformanceIndexUnavailableReason: "NO_APPROVED_BASELINE",
      },
      refetch: vi.fn(),
    };
    const legacyValues = { ...project, plannedValue: 1234, earnedValue: 1000, actualCost: 456 };
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
          <Routes>
            <Route path="/projects/:id" element={<Outlet context={{ projectId: legacyValues.id, project: legacyValues }} />}>
              <Route path="overview" element={<ProjectOverviewTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.queryByText("$1,234.00")).not.toBeInTheDocument();
    expect(screen.queryByText("$1,000.00")).not.toBeInTheDocument();
    expect(screen.queryByText("$456.00")).not.toBeInTheDocument();
  });
});
