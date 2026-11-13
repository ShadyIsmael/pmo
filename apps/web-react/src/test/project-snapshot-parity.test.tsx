import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { ProjectSnapshotDetailPage, ProjectSnapshotsTab } from "@features/projects/ProjectGovernanceTabs";

const mocks = vi.hoisted(() => ({ useProjectSnapshots: vi.fn(), useProjectSnapshot: vi.fn() }));

vi.mock("../features/projects/api/projects", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/projects/api/projects")>();
  return { ...actual, useProjectSnapshots: mocks.useProjectSnapshots, useProjectSnapshot: mocks.useProjectSnapshot };
});
vi.mock("../features/projects/api/project-lifecycle", () => ({
  useProjectLifecycleOptions: () => ({ data: { phases: [{ id: "phase-execution", code: "Execution", nameEn: "Execution stage", nameAr: "مرحلة التنفيذ", displayOrder: 1, isDefault: false, isActive: true }], statuses: [{ id: "status-active", code: "Active", nameEn: "In delivery", nameAr: "قيد التنفيذ", displayOrder: 1, isDefault: false, isActive: true }] }, isLoading: false, isError: false, refetch: vi.fn() }),
  projectLifecycleLabel: (option: { nameEn?: string; nameAr?: string; code: string } | undefined, locale: string, fallback?: string | null) => option ? (locale === "ar" ? option.nameAr || option.nameEn || option.code : option.nameEn || option.nameAr || option.code) : fallback || "",
}));

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1", project: { id: "project-1", name: "Migration", code: "PRJ-1" } }} />;
}

const snapshot = {
  id: "snapshot-1", projectId: "project-1", snapshotNumber: 4, snapshotName: "Handover baseline", snapshotType: "ManagerHandover",
  snapshotDate: "2026-10-01T00:00:00Z", effectiveEndDate: "2026-09-30T00:00:00Z", createdAt: "2026-10-01T00:00:00Z",
  name: "Migration", code: "PRJ-1", status: "Active", priority: "High", phase: "Execution", health: "Green", confidenceLevel: "High",
  currency: "USD", budget: 1000, actualCost: 250, progress: 45,
  data: {
    members: [{ originalId: "member-1", resourceName: "Jordan Lee", role: "Engineer", allocationPercentage: 60, startDate: "2026-01-01", endDate: "2026-12-31" }],
    allocations: [],
    phases: [{ originalId: "phase-1", name: "Build", status: "Active", progress: 45 }],
    milestones: [{ originalId: "milestone-1", name: "Pilot", status: "Completed", dueDate: "2026-09-20" }],
    deliverables: [], stakeholders: [], schedules: [], wbsItems: [], baselines: [],
    statusReports: [{ originalId: "report-1", summary: "Quarterly review", status: "Published", reportedAt: "2026-09-30" }],
    workItems: Array.from({ length: 13 }, (_, index) => ({
      originalId: `work-item-${index + 1}`,
      title: index === 12 ? "Task beyond initial page" : `Task ${index + 1}`,
      status: "Open",
    })),
    risks: [{ originalId: "risk-1", title: "Vendor delay", riskLevel: "High", status: "Active" }],
    issues: [{ originalId: "issue-1", title: "Missing test data", severity: "Critical", status: "Open" }],
    budgets: [{ originalId: "budget-1", name: "Delivery", approvedAmount: 800, currency: "USD" }],
    costEntries: [{ originalId: "cost-1", description: "Travel", amount: 25, currency: "USD", incurredDate: "2026-09-15" }],
    meetings: [{ originalId: "meeting-1", title: "Steering committee", startTime: "2026-09-25T10:00:00Z" }],
    documents: [{ originalId: "document-1", name: "Project charter", version: 3, status: "Active" }],
  },
};

describe("project snapshot report workspace", () => {
  it("renders captured fields, collection navigation, and unavailable states from the selected snapshot", () => {
    mocks.useProjectSnapshots.mockReturnValue({ data: [], isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useProjectSnapshot.mockReturnValue({ data: snapshot, isLoading: false, isError: false });

    const { container } = render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/snapshots/snapshot-1"]}>
          <Routes><Route path="/projects/:id/snapshots/:snapshotId" element={<ProjectSnapshotDetailPage />} /></Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    const preview = within(container.querySelector(".snapshot-screen-preview") as HTMLElement);
    expect(preview.getByRole("heading", { name: "Handover baseline" })).toBeInTheDocument();
    expect(preview.getByText("Historical snapshot for Migration (PRJ-1)")).toBeInTheDocument();
    expect(preview.getByText("Manager handover")).toBeInTheDocument();
    expect(preview.getByText("Read-only snapshot")).toBeInTheDocument();
    expect(preview.getAllByText("In delivery").length).toBeGreaterThan(0);
    expect(preview.getByText("Execution stage")).toBeInTheDocument();
    expect(preview.getByText("Jordan Lee")).toBeInTheDocument();
    expect(preview.getByText("Engineer")).toBeInTheDocument();
    expect(preview.getByText("60%")).toBeInTheDocument();
    expect(preview.getAllByText("45%").length).toBeGreaterThan(0);
    expect(preview.getAllByText("High").length).toBeGreaterThan(1);
    expect(preview.getByText("Critical")).toBeInTheDocument();
    expect(preview.getAllByText("Open").length).toBeGreaterThan(0);
    expect(preview.getByText("Travel")).toBeInTheDocument();
    expect(preview.getByText("Steering committee")).toBeInTheDocument();
    expect(preview.getByText("3")).toBeInTheDocument();
    expect(preview.getByText("Quarterly review")).toBeInTheDocument();
    expect(preview.getByText("Task beyond initial page")).toBeInTheDocument();
    expect(preview.getByRole("link", { name: /Work items/ })).toHaveAttribute("href", "#snapshot-section-workItems");
    expect(preview.getByText("Other sections (6)")).toBeInTheDocument();
    expect(preview.getAllByText(/Fields not recorded/).length).toBeGreaterThan(0);
    const printButton = preview.getByRole("button", { name: "Print snapshot" });
    expect(printButton).toBeInTheDocument();
    const printSpy = vi.spyOn(window, "print").mockImplementation(() => {});
    fireEvent.click(printButton);
    expect(printSpy).toHaveBeenCalledOnce();
    printSpy.mockRestore();
    expect(container.querySelector(".snapshot-print-document")?.textContent).toContain("Captured project records");
    expect(container.querySelector(".snapshot-print-document")?.textContent).toContain("Project snapshot document");
    expect(mocks.useProjectSnapshots).toHaveBeenCalledWith("project-1", false);
  });

  it("shows compact source metadata and navigates to immutable details", () => {
    mocks.useProjectSnapshots.mockReturnValue({ data: [snapshot], isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useProjectSnapshot.mockReturnValue({ data: undefined, isLoading: false, isError: false });

    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/snapshots"]}>
          <Routes>
            <Route path="/projects/:id/snapshots/:snapshotId" element={<ProjectSnapshotDetailPage />} />
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="snapshots" element={<ProjectSnapshotsTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByText("Handover baseline")).toBeInTheDocument();
    expect(screen.getByText("Effective End Date:")).toBeInTheDocument();
    expect(screen.getByText("Snapshot Date:")).toBeInTheDocument();
    expect(screen.getByText("Creator identity unavailable")).toBeInTheDocument();
    expect(screen.queryByText("Manager transition:")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /View snapshot/ })).toHaveAttribute("href", "/projects/project-1/snapshots/snapshot-1");
    expect(mocks.useProjectSnapshots).toHaveBeenCalledWith("project-1", true);
  });
});
