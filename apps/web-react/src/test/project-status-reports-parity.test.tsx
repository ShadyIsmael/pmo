import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import type { ProjectFinancialSummaryDto, ProjectStatusReportDto, ProjectStatusReportListDto } from "../features/projects/api/projects";
import { ProjectStatusReportsTab } from "../features/projects/ProjectGovernanceTabs";

const reportMocks = vi.hoisted(() => ({
  reports: [] as ProjectStatusReportListDto[],
  details: {} as Record<string, ProjectStatusReportDto>,
  update: vi.fn(),
  create: vi.fn(),
  publish: vi.fn(),
  remove: vi.fn(),
  canEdit: true,
  canView: true,
  canExport: true,
  canPublish: true,
  canMutate: true,
  readOnly: false,
  currentProgressPercent: 42 as number | null,
  currentProgressLoading: false,
  currentProgressFetching: false,
  currentProgressError: false,
  currentProgressRefetch: vi.fn(),
  financialSummary: { data: undefined as ProjectFinancialSummaryDto | undefined, isLoading: false, isError: false },
}));

const lifecycleMock = vi.hoisted(() => ({
  options: {
    data: { phases: [], statuses: [
      { id: "status-mobilizing", code: "Mobilizing", nameEn: "Mobilizing", nameAr: "التجهيز", displayOrder: 10, isDefault: true, isActive: true, reportingCategory: "InProgress", attentionLevel: "None" },
      { id: "status-delivered", code: "Delivered", nameEn: "Delivered", nameAr: "تم التسليم", displayOrder: 20, isDefault: false, isActive: true, reportingCategory: "Completed", attentionLevel: "None" },
    ] },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  },
}));

vi.mock("../api/workItems", () => ({ useWorkItemStatuses: () => ({ data: [], isLoading: false }) }));
vi.mock("../features/risks/api/risks", () => ({ useRisks: () => ({ data: { items: [], totalCount: 0 }, isLoading: false, isError: false }) }));
vi.mock("../features/projects/api/projects", () => ({
  useProjectStatusReports: () => ({ data: { items: reportMocks.reports }, isLoading: false, isError: false, refetch: vi.fn() }),
  useProjectStatusReport: (_projectId: string, reportId: string) => ({ data: reportMocks.details[reportId], isLoading: false, isError: false }),
  useProjectStatusReportComparison: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectStatusReportCurrentProgress: () => ({ data: reportMocks.currentProgressPercent == null ? { projectPlanProgressPercent: null } : { projectPlanProgressPercent: reportMocks.currentProgressPercent }, isLoading: reportMocks.currentProgressLoading, isFetching: reportMocks.currentProgressFetching, isError: reportMocks.currentProgressError, refetch: reportMocks.currentProgressRefetch }),
  useCreateProjectStatusReport: () => ({ mutateAsync: reportMocks.create, isPending: false }),
  useUpdateProjectStatusReport: () => ({ mutateAsync: reportMocks.update, isPending: false }),
  usePublishProjectStatusReport: () => ({ mutateAsync: reportMocks.publish, isPending: false }),
  useDeleteProjectStatusReport: () => ({ mutateAsync: reportMocks.remove, isPending: false }),
  useProjectFinancialSummary: () => reportMocks.financialSummary,
  useProjectIssues: () => ({ data: [], isLoading: false, isError: false }),
}));

vi.mock("../auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", fullName: "Alex Admin", permissions: ["Project.Edit", "Project.View", "StatusReport.Export"] } }) }));
vi.mock("../api/management", () => ({ useManagement: () => ({ canMutate: reportMocks.canMutate, effectiveReadOnly: reportMocks.readOnly }) }));
vi.mock("../features/projects/lib/project-labels", () => ({ useProjectEnumLabels: () => ({ statusLabel: (value?: string | null) => value ?? "—", healthLabel: (value?: string | null) => value ?? "—" }) }));
vi.mock("../features/projects/api/project-lifecycle", () => ({
  useProjectLifecycleOptions: () => lifecycleMock.options,
  projectLifecycleLabel: (option: { nameEn?: string; nameAr?: string; code: string } | undefined, locale: string, fallback?: string | null) => option ? (locale === "ar" ? option.nameAr || option.nameEn || option.code : option.nameEn || option.nameAr || option.code) : fallback || "",
}));
vi.mock("../lib/permissions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/permissions")>();
  return { ...actual, hasPermission: (_permissions: readonly string[], permission: string) => permission === actual.Permissions.StatusReport.Create ? reportMocks.canEdit : permission === actual.Permissions.StatusReport.Edit ? reportMocks.canEdit : permission === actual.Permissions.StatusReport.Publish ? reportMocks.canPublish : permission === actual.Permissions.StatusReport.View ? reportMocks.canView : permission === actual.Permissions.StatusReport.Export ? reportMocks.canExport : false };
});

const draft: ProjectStatusReportDto = {
  id: "report-draft",
  projectId: "project-1",
  projectNameSnapshot: "Apollo Migration",
  projectCodeSnapshot: "PRJ-100",
  reportDate: "2026-09-30",
  periodFrom: "2026-09-01",
  periodTo: "2026-09-30",
  statusDate: "2026-09-30",
  reportState: "Draft",
  projectLifecycleStatusAtReport: "Mobilizing",
  overallHealth: "Amber",
  overallProgressPercent: 42,
  scheduleHealth: "Amber",
  costHealth: "Green",
  scopeHealth: "Green",
  qualityHealth: "Green",
  resourceHealth: "Amber",
  executiveSummary: "Draft executive summary",
  preparedByNameSnapshot: "Alex Admin",
  sourceDataCapturedAt: "2026-09-30T12:00:00Z",
  rowVersion: "draft-row-version",
  risks: [{ sourceRiskId: "risk-1", rank: 1, title: "Vendor delay", riskLevel: "Medium", riskStatus: "Open", ownerNameSnapshot: "Jamie" }],
  issues: [],
  changeRequests: [],
};

const published: ProjectStatusReportDto = {
  ...draft,
  id: "report-published",
  reportDate: "2026-08-31",
  periodFrom: "2026-08-01",
  periodTo: "2026-08-31",
  reportState: "Published",
  currencyCodeSnapshot: "USD",
  publishedAt: "2026-08-31T14:00:00Z",
  publishedByNameSnapshot: "Alex Admin",
  budgetAmountSnapshot: 100000,
  actualCostAmountSnapshot: 46000,
  forecastCostAmountSnapshot: 94000,
};

const project = {
  id: "project-1",
  name: "Apollo Migration",
  code: "PRJ-100",
  status: "Mobilizing",
  health: "Amber",
  progress: 42 as number | null,
  projectManagerName: "Sam PM",
  currency: "USD",
  startDate: "2026-01-01",
  endDate: "2026-12-31",
};

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1", project }} />;
}

function renderStatusReports() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/project-1/status-reports"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectOutlet />}>
            <Route path="status-reports" element={<ProjectStatusReportsTab />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  window.localStorage.setItem("nexus-locale", "en");
  project.health = "Amber";
  reportMocks.reports = [draft, published];
  reportMocks.details = { [draft.id]: draft, [published.id]: published };
  reportMocks.update.mockReset().mockResolvedValue(undefined);
  reportMocks.create.mockReset().mockResolvedValue(undefined);
  reportMocks.publish.mockReset().mockResolvedValue(undefined);
  reportMocks.remove.mockReset().mockResolvedValue(undefined);
  reportMocks.canEdit = true;
  reportMocks.canView = true;
  reportMocks.canExport = true;
  reportMocks.canPublish = true;
  reportMocks.canMutate = true;
  reportMocks.readOnly = false;
  reportMocks.currentProgressPercent = 42;
  reportMocks.currentProgressLoading = false;
  reportMocks.currentProgressFetching = false;
  reportMocks.currentProgressError = false;
  reportMocks.currentProgressRefetch.mockReset();
  reportMocks.financialSummary = { data: undefined, isLoading: false, isError: false };
});

describe("project status report source parity", () => {
  it("keeps narrative and status inputs when switching sections and includes them in the draft", async () => {
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));
    const editor = screen.getByRole("dialog", { name: "New Report" });
    fireEvent.click(within(editor).getByRole("button", { name: "Report narrative" }));
    const summary = editor.querySelector<HTMLTextAreaElement>('textarea[name="executiveSummary"]')!;
    fireEvent.change(summary, { target: { value: "Delivery is progressing" } });
    fireEvent.click(within(editor).getByRole("button", { name: "Project status" }));
    fireEvent.change(editor.querySelector<HTMLInputElement>('input[name="overallProgressPercent"]')!, { target: { value: "55" } });
    fireEvent.click(within(editor).getByRole("button", { name: "Report narrative" }));
    expect(summary).toHaveValue("Delivery is progressing");
    fireEvent.click(within(editor).getByRole("button", { name: "Save Draft" }));
    await waitFor(() => expect(reportMocks.create).toHaveBeenCalledWith(expect.objectContaining({ executiveSummary: "Delivery is progressing", overallProgressPercent: 55 })));
  });
  it("edits report status using localized database catalog options", async () => {
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));
    const editor = screen.getByRole("dialog", { name: "New Report" });
    fireEvent.click(within(editor).getByRole("button", { name: "Project status" }));
    const status = editor.querySelector<HTMLSelectElement>('select[name="projectLifecycleStatusAtReport"]')!;
    expect(within(status).getByRole("option", { name: "Mobilizing" })).toHaveValue("Mobilizing");
    expect(within(status).getByRole("option", { name: "Delivered" })).toHaveValue("Delivered");
    expect(within(status).queryByRole("option", { name: "Active" })).not.toBeInTheDocument();
  });
  it("shows the authorized reporting-period finance summary and keeps its source currency", async () => {
    reportMocks.financialSummary = {
      data: {
        projectId: "project-1", periodFrom: "2026-11-01", periodTo: "2026-11-08", currencyCode: "EUR",
        approvedBudget: 500_000, budgetStatus: "approved", expenditure: 3_200, committedCost: 15_000,
        actualPaid: 1_100, outstandingPlannedPayments: 2_400, paymentsDueWithinPeriod: 800,
        remainingUnpaidAtPeriodEnd: 2_400, hasFinancialData: true, dataStatus: "available",
      },
      isLoading: false,
      isError: false,
    };
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));

    const editor = screen.getByRole("dialog", { name: "New Report" });
    expect(within(editor).getByText("Approved Annual Budget")).toBeInTheDocument();
    expect(within(editor).getByText("Expenditure")).toBeInTheDocument();
    expect(within(editor).getByText("Commitments")).toBeInTheDocument();
    expect(within(editor).getAllByText(/€/)).toHaveLength(7);
    expect(within(editor).getByText("Budget: Approved for the full period")).toBeInTheDocument();
  });

  it("distinguishes an empty finance period from an API failure", async () => {
    reportMocks.financialSummary = {
      data: {
        projectId: "project-1", periodFrom: "2026-11-01", periodTo: "2026-11-08", currencyCode: "USD",
        budgetStatus: "unavailable", hasFinancialData: false, dataStatus: "empty",
      },
      isLoading: false,
      isError: false,
    };
    const first = renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));
    expect(screen.getByText("No financial records are available for this reporting period.")).toBeInTheDocument();
    first.unmount();

    reportMocks.financialSummary = { data: undefined, isLoading: false, isError: true };
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));
    expect(screen.getByText("Could not load financial summary.")).toBeInTheDocument();
  });

  it("prefills editable project values once and saves the user's final snapshot values", async () => {
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));

    const editor = screen.getByRole("dialog", { name: "New Report" });
    const progress = editor.querySelector<HTMLInputElement>('input[name="overallProgressPercent"]')!;
    const health = editor.querySelector<HTMLSelectElement>('select[name="overallHealth"]')!;
    const status = editor.querySelector<HTMLSelectElement>('select[name="projectLifecycleStatusAtReport"]')!;
    await waitFor(() => expect(progress).toHaveValue(42));
    expect(progress).not.toHaveAttribute("readOnly");
    expect(health).toHaveValue("Amber");
    expect(status).toHaveValue("Mobilizing");

    fireEvent.change(progress, { target: { value: "63.5" } });
    fireEvent.change(health, { target: { value: "Red" } });
    reportMocks.currentProgressPercent = 88;
    fireEvent.change(editor.querySelector<HTMLTextAreaElement>('textarea[name="executiveSummary"]')!, { target: { value: "User reviewed current delivery." } });
    expect(progress).toHaveValue(63.5);

    fireEvent.click(within(editor).getByRole("button", { name: "Save Draft" }));
    await waitFor(() => expect(reportMocks.create).toHaveBeenCalledWith(expect.objectContaining({
      projectLifecycleStatusAtReport: "Mobilizing",
      overallHealth: "Red",
      overallProgressPercent: 63.5,
      executiveSummary: "User reviewed current delivery.",
    })));
  });

  it("leaves unavailable overall-health and progress values empty instead of inventing defaults", async () => {
    project.health = "";
    project.progress = null;
    reportMocks.currentProgressPercent = null;
    reportMocks.currentProgressError = true;
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));

    const editor = screen.getByRole("dialog", { name: "New Report" });
    await waitFor(() => expect(within(editor).getByText("Current progress could not be loaded. You can retry or enter a value manually.")).toBeInTheDocument());
    expect(editor.querySelector<HTMLInputElement>('input[name="overallProgressPercent"]')).toHaveValue(null);
    expect(editor.querySelector<HTMLSelectElement>('select[name="overallHealth"]')).toHaveValue("");
    project.health = "Amber";
    project.progress = 42;
    reportMocks.currentProgressError = false;
  });

  it("does not present cached progress as current while the opening request is still running", () => {
    project.progress = null;
    reportMocks.currentProgressFetching = true;
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "New Report" }));

    const editor = screen.getByRole("dialog", { name: "New Report" });
    const progress = editor.querySelector<HTMLInputElement>('input[name="overallProgressPercent"]')!;
    expect(progress).toHaveValue(null);
    expect(within(editor).getByText("Loading authoritative project progress…")).toBeInTheDocument();
    project.progress = 42;
    reportMocks.currentProgressFetching = false;
  });

  it("opens a wide report-detail dialog from View and launches draft editing from that dialog", async () => {
    renderStatusReports();

    expect(screen.getAllByRole("button", { name: "View" })).toHaveLength(2);
    expect(screen.queryByRole("button", { name: "Edit status report" })).not.toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "View" })[0]!);

    const details = screen.getByRole("dialog", { name: "Status Report Details" });
    expect(details.children.item(1)).toHaveClass("max-w-6xl", "sm:max-w-6xl");
    expect(within(details).getByRole("heading", { name: "Apollo Migration (PRJ-100)" })).toBeInTheDocument();
    expect(within(details).getByText("Draft executive summary")).toBeInTheDocument();
    expect(within(details).getByText("Vendor delay")).toBeInTheDocument();
    expect(within(details).getByRole("button", { name: "Publish Report" })).toBeInTheDocument();
    fireEvent.click(within(details).getByRole("button", { name: "Edit" }));

    const editor = screen.getByRole("dialog", { name: "Edit Status Report" });
    expect(screen.queryByRole("dialog", { name: "Status Report Details" })).not.toBeInTheDocument();
    expect(editor.querySelector<HTMLInputElement>('input[name="periodFrom"]')).toHaveValue("2026-09-01");
    expect(editor.querySelector<HTMLTextAreaElement>('textarea[name="executiveSummary"]')).toHaveValue("Draft executive summary");
    expect(editor.querySelector<HTMLInputElement>('input[name="overallProgressPercent"]')).toHaveValue(42);

    fireEvent.change(editor.querySelector<HTMLTextAreaElement>('textarea[name="executiveSummary"]')!, { target: { value: "Revised summary" } });
    fireEvent.click(within(editor).getByRole("button", { name: "Save Changes" }));

    await vi.waitFor(() => expect(reportMocks.update).toHaveBeenCalledWith(expect.objectContaining({ rowVersion: "draft-row-version", executiveSummary: "Revised summary" })));
  });

  it("keeps the published report read-only and preserves its financial snapshot and export action", () => {
    renderStatusReports();
    fireEvent.click(screen.getAllByRole("button", { name: "View" })[1]!);

    const details = screen.getByRole("dialog", { name: "Status Report Details" });
    expect(within(details).getAllByText("$100,000").length).toBeGreaterThanOrEqual(1);
    expect(within(details).getAllByText("$46,000").length).toBeGreaterThanOrEqual(1);
    expect(within(details).getByRole("button", { name: "Export PDF" })).toBeInTheDocument();
    expect(within(details).getByRole("button", { name: "Print Report" })).toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Publish Report" })).not.toBeInTheDocument();
  });

  it("renders only the sections frozen into a report configuration", () => {
    const configured = { ...draft, includedSectionsSnapshot: ["PerformanceKPIs"] };
    reportMocks.reports = [configured];
    reportMocks.details = { [configured.id]: configured };
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "View" }));
    const details = screen.getByRole("dialog", { name: "Status Report Details" });
    expect(within(details).getByText("Performance KPIs")).toBeInTheDocument();
    expect(within(details).queryByText("Draft executive summary")).not.toBeInTheDocument();
    expect(within(details).queryByText("Vendor delay")).not.toBeInTheDocument();
  });

  it("hides report mutations in a read-only management context while keeping view access", () => {
    reportMocks.canMutate = false;
    renderStatusReports();

    expect(screen.queryByRole("button", { name: "New Report" })).not.toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "View" })[0]!);
    const details = screen.getByRole("dialog", { name: "Status Report Details" });
    expect(within(details).queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Publish Report" })).not.toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("publishes a Draft directly with its final saved content and row version", async () => {
    renderStatusReports();
    fireEvent.click(screen.getAllByRole("button", { name: "View" })[0]!);
    fireEvent.click(within(screen.getByRole("dialog", { name: "Status Report Details" })).getByRole("button", { name: "Publish Report" }));
    fireEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Confirm" }));
    await waitFor(() => expect(reportMocks.publish).toHaveBeenCalledWith({
      reportId: draft.id,
      content: expect.objectContaining({
        executiveSummary: draft.executiveSummary,
        overallHealth: draft.overallHealth,
        overallProgressPercent: draft.overallProgressPercent,
        rowVersion: draft.rowVersion,
      }),
    }));
  });

  it("keeps a legacy intermediate-state report available as read-only history", () => {
    const legacy = { ...draft, reportState: "Generated" as const };
    reportMocks.reports = [legacy];
    reportMocks.details = { [legacy.id]: legacy };
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "View" }));
    const details = screen.getByRole("dialog", { name: "Status Report Details" });
    expect(within(details).getByText("This report uses a previous workflow state and is retained as historical, read-only data.")).toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Publish Report" })).not.toBeInTheDocument();
  });
});
