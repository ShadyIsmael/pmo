import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import type { ProjectStatusReportDto, ProjectStatusReportListDto } from "../features/projects/api/projects";
import { ProjectStatusReportsTab } from "../features/projects/ProjectGovernanceTabs";

const reportMocks = vi.hoisted(() => ({
  reports: [] as ProjectStatusReportListDto[],
  details: {} as Record<string, ProjectStatusReportDto>,
  update: vi.fn(),
  create: vi.fn(),
  generate: vi.fn(),
  submit: vi.fn(),
  approve: vi.fn(),
  returnReport: vi.fn(),
  reopen: vi.fn(),
  publish: vi.fn(),
  remove: vi.fn(),
  canEdit: true,
  canView: true,
  canExport: true,
  canGenerate: true,
  canSubmit: true,
  canApprove: true,
  canPublish: true,
  canMutate: true,
  readOnly: false,
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjectStatusReports: () => ({ data: { items: reportMocks.reports }, isLoading: false, isError: false, refetch: vi.fn() }),
  useProjectStatusReport: (_projectId: string, reportId: string) => ({ data: reportMocks.details[reportId], isLoading: false, isError: false }),
  useProjectStatusReportComparison: () => ({ data: undefined, isLoading: false, isError: false }),
  useProjectStatusReportCurrentProgress: () => ({ data: { projectPlanProgressPercent: 42 }, isLoading: false }),
  useCreateProjectStatusReport: () => ({ mutateAsync: reportMocks.create, isPending: false }),
  useUpdateProjectStatusReport: () => ({ mutateAsync: reportMocks.update, isPending: false }),
  useGenerateProjectStatusReport: () => ({ mutateAsync: reportMocks.generate, isPending: false }),
  useSubmitProjectStatusReport: () => ({ mutateAsync: reportMocks.submit, isPending: false }),
  useApproveProjectStatusReport: () => ({ mutateAsync: reportMocks.approve, isPending: false }),
  useReturnProjectStatusReport: () => ({ mutateAsync: reportMocks.returnReport, isPending: false }),
  useReopenProjectStatusReport: () => ({ mutateAsync: reportMocks.reopen, isPending: false }),
  usePublishProjectStatusReport: () => ({ mutateAsync: reportMocks.publish, isPending: false }),
  useDeleteProjectStatusReport: () => ({ mutateAsync: reportMocks.remove, isPending: false }),
}));

vi.mock("../auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", fullName: "Alex Admin", permissions: ["Project.Edit", "Project.View", "Reports.Export"] } }) }));
vi.mock("../api/management", () => ({ useManagement: () => ({ canMutate: reportMocks.canMutate, effectiveReadOnly: reportMocks.readOnly }) }));
vi.mock("../features/projects/lib/project-labels", () => ({ useProjectEnumLabels: () => ({ statusLabel: (value?: string | null) => value ?? "—", healthLabel: (value?: string | null) => value ?? "—" }) }));
vi.mock("../lib/permissions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/permissions")>();
  return { ...actual, hasPermission: (_permissions: readonly string[], permission: string) => permission === actual.Permissions.StatusReport.Create || permission === actual.Permissions.StatusReport.Edit ? reportMocks.canEdit : permission === actual.Permissions.StatusReport.Generate ? reportMocks.canGenerate : permission === actual.Permissions.StatusReport.Submit ? reportMocks.canSubmit : permission === actual.Permissions.StatusReport.Approve ? reportMocks.canApprove : permission === actual.Permissions.StatusReport.Publish ? reportMocks.canPublish : permission === actual.Permissions.StatusReport.View ? reportMocks.canView : permission === actual.Permissions.StatusReport.Export ? reportMocks.canExport : false };
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
  projectLifecycleStatusAtReport: "Active",
  overallHealth: "Amber",
  overallProgressPercent: 42,
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
  status: "Active",
  health: "Amber",
  progress: 42,
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
  reportMocks.reports = [draft, published];
  reportMocks.details = { [draft.id]: draft, [published.id]: published };
  reportMocks.update.mockReset().mockResolvedValue(undefined);
  reportMocks.create.mockReset().mockResolvedValue(undefined);
  reportMocks.generate.mockReset().mockResolvedValue(undefined);
  reportMocks.submit.mockReset().mockResolvedValue(undefined);
  reportMocks.approve.mockReset().mockResolvedValue(undefined);
  reportMocks.returnReport.mockReset().mockResolvedValue(undefined);
  reportMocks.reopen.mockReset().mockResolvedValue(undefined);
  reportMocks.publish.mockReset().mockResolvedValue(undefined);
  reportMocks.remove.mockReset().mockResolvedValue(undefined);
  reportMocks.canEdit = true;
  reportMocks.canView = true;
  reportMocks.canExport = true;
  reportMocks.canGenerate = true;
  reportMocks.canSubmit = true;
  reportMocks.canApprove = true;
  reportMocks.canPublish = true;
  reportMocks.canMutate = true;
  reportMocks.readOnly = false;
});

describe("project status report source parity", () => {
  it("opens a wide report-detail dialog from View and launches draft editing from that dialog", async () => {
    renderStatusReports();

    expect(screen.getAllByRole("button", { name: "View" })).toHaveLength(2);
    expect(screen.queryByRole("button", { name: "Edit status report" })).not.toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "View" })[0]!);

    const details = screen.getByRole("dialog", { name: "Status Report Details" });
    expect(details.children.item(1)).toHaveClass("max-w-4xl", "sm:max-w-4xl");
    expect(within(details).getByRole("heading", { name: "Apollo Migration" })).toBeInTheDocument();
    expect(within(details).getByText("Draft executive summary")).toBeInTheDocument();
    expect(within(details).getByText("Vendor delay")).toBeInTheDocument();
    fireEvent.click(within(details).getByRole("button", { name: "Edit" }));

    const editor = screen.getByRole("dialog", { name: "Edit Status Report" });
    expect(screen.queryByRole("dialog", { name: "Status Report Details" })).not.toBeInTheDocument();
    expect(editor.querySelector<HTMLInputElement>('input[name="reportDate"]')).toHaveValue("2026-09-30");
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
    expect(within(details).getByText("$100,000")).toBeInTheDocument();
    expect(within(details).getByText("$46,000")).toBeInTheDocument();
    expect(within(details).getByRole("button", { name: "Export PDF" })).toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
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
    expect(within(details).queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
    expect(within(details).queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("routes generated reports through the workflow submit action", async () => {
    const generated = { ...draft, reportState: "Generated" as const };
    reportMocks.reports = [generated];
    reportMocks.details = { [generated.id]: generated };
    renderStatusReports();
    fireEvent.click(screen.getByRole("button", { name: "View" }));
    fireEvent.click(within(screen.getByRole("dialog", { name: "Status Report Details" })).getByRole("button", { name: "Submit for review" }));
    await waitFor(() => expect(reportMocks.submit).toHaveBeenCalledWith(generated.id));
  });
});
