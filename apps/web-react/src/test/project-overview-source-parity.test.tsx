import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";

const fixtures = vi.hoisted(() => ({
  query: { isLoading: false, isError: false, data: undefined as unknown, refetch: vi.fn() },
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjectOverview: () => fixtures.query,
}));

const project = {
  id: "project-1",
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
  });

  it("localizes risk level and latest published health and links top issues to their work item", () => {
    renderOverview();

    expect(screen.getByText("High")).toHaveAttribute("title", "Identified");
    expect(screen.getByText("At Risk")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Deployment issue/i })).toHaveAttribute("href", "/work-items/issue%2F1");
  });

  it("keeps risk and health labels localized in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderOverview();

    expect(screen.getByText("مرتفع")).toHaveAttribute("title", "تم التعرف عليه");
    expect(screen.getByText("في خطر")).toBeInTheDocument();
  });

  it("uses project progress when the overview has no plan progress, matching Next", () => {
    fixtures.query = { isLoading: false, isError: false, data: { ...overview, progress: 78, planProgress: null }, refetch: vi.fn() };
    renderOverview();

    expect(screen.getByRole("img", { name: "Overall Progress: 35%" })).toBeInTheDocument();
  });

  it("links the empty-state status reports button to the status-reports tab, not the dashboard", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: { ...overview, statusReports: { totalCount: 0, publishedCount: 0, latestPublishedReport: undefined } },
      refetch: vi.fn(),
    };
    renderOverview();

    expect(screen.getAllByText("No status reports").length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: "Status Reports" })).toHaveAttribute("href", "/projects/project-1/status-reports");
  });

  it("renders the complete golden-copy overview information and reporting sections", () => {
    renderOverview();

    const renderedText = document.body.textContent?.toLocaleLowerCase() ?? "";
    for (const heading of [
      "Project Information", "KPI Summary", "Project Performance", "Attention Required", "Milestones",
      "Latest Published Status", "Resource & Allocation", "Governance & Reporting",
    ]) {
      expect(renderedText).toContain(heading.toLocaleLowerCase());
    }
    expect(screen.getByRole("img", { name: /^Project Performance$/i })).toBeInTheDocument();
    expect(screen.getByText("Original Approved Budget")).toBeInTheDocument();
    expect(screen.getByText("Pending Revision", { exact: true })).toBeInTheDocument();
  });

  it("explains SPI and CPI with concise localized tooltips", () => {
    renderOverview();

    const summary = screen.getByRole("region", { name: "KPI Summary" });
    expect(within(summary).queryByText("SPI")).not.toBeInTheDocument();
    expect(within(summary).queryByText("CPI")).not.toBeInTheDocument();
    expect(within(summary).queryByText("Next milestone")).not.toBeInTheDocument();
    expect(within(summary).getByText("Schedule Variance")).toBeInTheDocument();
    expect(within(summary).getByText("Cost Variance")).toBeInTheDocument();
    expect(within(summary).getByText("Critical attention")).toBeInTheDocument();
    expect(within(summary).getByText("Team signal")).toBeInTheDocument();

    const spiHint = screen.getByLabelText("SPI = Earned Value ÷ Planned Value. Above 1: ahead of schedule; below 1: behind; 1: on plan.");
    const cpiHint = screen.getByLabelText("CPI = Earned Value ÷ Actual Cost. Above 1: under budget; below 1: over budget; 1: on budget.");
    expect(spiHint).toHaveAttribute("title", spiHint.getAttribute("aria-label"));
    expect(cpiHint).toHaveAttribute("title", cpiHint.getAttribute("aria-label"));
    expect(spiHint.parentElement).toHaveTextContent("SPI");
    expect(cpiHint.parentElement).toHaveTextContent("CPI");
    expect(screen.getByText("0.90")).toBeInTheDocument();
    expect(screen.getByText("0.95")).toBeInTheDocument();
  });

  it("explains SPI and CPI in Arabic and reports why unavailable values are missing", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
        plannedValue: null,
        earnedValue: null,
        actualCost: null,
        scheduleVariance: null,
        costVariance: null,
        schedulePerformanceIndex: null,
        costPerformanceIndex: null,
        plannedValueUnavailableReason: "NO_APPROVED_BASELINE",
        earnedValueUnavailableReason: "NO_APPROVED_BASELINE",
        actualCostUnavailableReason: "NO_ACTUAL_COST_DATA",
        schedulePerformanceIndexUnavailableReason: "NO_APPROVED_BASELINE",
        costPerformanceIndexUnavailableReason: "NO_APPROVED_BASELINE",
        costVarianceUnavailableReason: "NON_LABOR_COSTS_NOT_BASELINED",
      },
      refetch: vi.fn(),
    };
    renderOverview();

    const summary = screen.getByRole("region", { name: "ملخص مؤشرات الأداء" });
    expect(within(summary).queryByText("مؤشر الأداء الزمني")).not.toBeInTheDocument();
    expect(within(summary).queryByText("مؤشر الأداء التكلفي")).not.toBeInTheDocument();
    expect(within(summary).queryByText("المعلم التالي")).not.toBeInTheDocument();
    expect(screen.getByLabelText("SPI = القيمة المكتسبة ÷ القيمة المخططة. أكبر من 1: متقدم عن الجدول؛ أقل من 1: متأخر؛ 1: حسب الخطة.")).toBeInTheDocument();
    expect(screen.getByLabelText("CPI = القيمة المكتسبة ÷ التكلفة الفعلية. أكبر من 1: أقل من الميزانية؛ أقل من 1: تجاوز الميزانية؛ 1: مطابق للميزانية.")).toBeInTheDocument();
    expect(screen.getAllByText("لا يوجد خط أساس معتمد للخطة.").length).toBeGreaterThan(0);
    expect(screen.getAllByText("انحراف التكلفة ومؤشر CPI غير متاحين لأن التكاليف المباشرة غير المرتبطة بالعمل لم تُربط بمهام ممولة في خط الأساس المعتمد.").length).toBeGreaterThan(0);
  });

  it("does not fall back to manually stored project EVM values when canonical overview values are absent", () => {
    fixtures.query = {
      isLoading: false,
      isError: false,
      data: {
        ...overview,
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

    expect(screen.getAllByText("No approved plan baseline is available.").length).toBeGreaterThan(0);
    expect(screen.queryByText("$1,234.00")).not.toBeInTheDocument();
    expect(screen.queryByText("$1,000.00")).not.toBeInTheDocument();
    expect(screen.queryByText("$456.00")).not.toBeInTheDocument();
  });
});
