import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { ManagedPortfolioDetailPage, ManagedProgramDetailPage } from "../features/delivery/PortfolioProgramPages";

const fixtures = vi.hoisted(() => ({
  overview: {
    program: { id: "program-1", name: "Core Renewal", code: "PGM-001", status: "Active", health: "Amber", programManagerName: "Alex Manager", portfolioName: "Technology", currency: "USD" },
    summary: { totalProjects: 8, activeProjects: 5, completedProjects: 2, projectsAtRisk: 3, criticalProjects: 1, averageProjectProgress: 42, projectHealthDistribution: { Green: 4, Amber: 3, Red: 1 } },
    delivery: { projectsBehindSchedule: 2, projectsWithScheduleData: 7, totalMilestones: 12, overdueMilestones: 1, dueSoonMilestones: 2, onTrackMilestones: 7, completedMilestones: 2 },
    risks: { openCount: 4, overdueCount: 1, bySeverity: { High: 2, Medium: 2 } },
    issues: { openCount: 2, overdueCount: 1, bySeverity: { High: 1, Low: 1 } },
    resources: { activeAllocationCount: 9, activeResourceCount: 6, sharedResourceCount: 1, overAllocatedResourceCount: 1, allocatedHoursPerWeek: 210, pressure: [] },
    statusReports: { publishedProjectCount: 6, currentCount: 4, staleCount: 1, missingCount: 1, freshnessDays: 30, latestPublished: [] },
    projects: [], projectsPage: 1, projectsTotalPages: 1,
    milestones: [{ id: "milestone-1", projectId: "project-1", publicId: "project-1", projectName: "Core Project", name: "Schedule baseline", state: "Overdue" }],
    topRisks: [], topIssues: [],
    attentionItems: [{ level: "Critical", kind: "Milestone", message: "Schedule baseline needs recovery", projectId: "project-1", publicId: "project-1", itemId: "milestone-1" }],
    upcomingMeetings: [{ id: "meeting-1", title: "Weekly review", startTime: "2026-03-12T10:00:00Z", projectId: "project-1", publicId: "project-1", projectName: "Core Project" }],
  },
  portfolioOverview: {
    portfolio: { id: "portfolio-1", name: "Technology", code: "PF-001", status: "Active", health: "Green", currency: "USD", ownerName: "Alex Manager", strategicObjective: "Modernize platforms", priority: "High" },
    summary: { totalPrograms: 1, totalProjects: 2, programContainedProjects: 1, directPortfolioProjects: 1, activeProjects: 1, completedProjects: 0, atRiskProjects: 1, criticalProjects: 0, averageProjectProgress: 50, programHealthDistribution: { Green: 1 }, projectHealthDistribution: { Amber: 1 } },
    delivery: { programDeliveryConditions: { OnTrack: 1 }, projectScheduleConditions: { Delayed: 1 }, delayedProjects: 1, delayedPrograms: 0, projectsWithScheduleData: 2, totalMilestones: 1, overdueMilestones: 1, dueSoonMilestones: 0, onTrackMilestones: 0, completedMilestones: 0, projectProgressDistribution: {} },
    risks: { sourceScope: "Project", openCount: 1, criticalCount: 0, highCount: 1, overdueCount: 0, byProgram: {}, byProject: {} },
    issues: { openCount: 1, criticalCount: 0, overdueCount: 0, byProgram: {}, byProject: {} },
    resources: { activeAllocationCount: 2, activeResourceCount: 2, sharedResourceCount: 0, crossProgramResourceCount: 0, overAllocatedResourceCount: 0, allocatedHoursPerWeek: 40, pressure: [] },
    statusReports: { publishedProjectCount: 1, currentCount: 1, staleCount: 0, missingCount: 1, freshnessDays: 30, programsWithReportingGaps: [], projectsWithReportingGaps: [], latestPublished: [] },
    programs: [], projects: [], milestones: [], topRisks: [], topIssues: [], attentionItems: [], upcomingMeetings: [],
    programsPage: 1, programsTotalPages: 1, projectsPage: 1, projectsTotalPages: 1,
  },
  portfolioRecord: { id: "portfolio-1", name: "Technology", code: "PF-001", status: "Active", ownerId: "portfolio-manager-1", ownerName: "Alex Manager", rowVersion: [1] },
  portfolioRecordLoading: false,
  portfolioRecordError: false,
  programRecord: { id: "program-1", name: "Core Renewal", code: "PGM-001", status: "Active", portfolioId: "portfolio-1", rowVersion: [1] },
  archive: vi.fn(),
  reactivate: vi.fn(),
  permissions: [] as string[],
  userId: "portfolio-manager-1",
  canMutate: false,
  effectiveReadOnly: false,
  capabilities: {
    canViewRequests: true,
    canViewHistory: true,
    canInitiate: true,
    canAdminInitiate: false,
    canAssignInitial: false,
    canCancelOwnPending: true,
    canApproveIncoming: true,
    currentManagerId: "portfolio-manager-1",
    currentManagerName: "Alex Manager",
    hasPendingRequest: false,
  },
}));

vi.mock("../features/delivery/api/delivery", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/delivery/api/delivery")>();
  return {
    ...actual,
    useProgramManagementOverview: () => ({ data: fixtures.overview, isLoading: false, isError: false }),
    usePortfolioManagementOverview: () => ({ data: fixtures.portfolioOverview, isLoading: false, isError: false }),
    useProgram: () => ({ data: fixtures.programRecord, isLoading: false, isError: false }),
    usePortfolio: () => ({ data: fixtures.portfolioRecordLoading ? undefined : fixtures.portfolioRecord, isLoading: fixtures.portfolioRecordLoading, isError: fixtures.portfolioRecordError, refetch: vi.fn() }),
    useArchiveProgram: () => ({ mutateAsync: fixtures.archive, isPending: false }),
    useReactivateProgram: () => ({ mutateAsync: fixtures.reactivate, isPending: false }),
    useArchivePortfolio: () => ({ mutateAsync: fixtures.archive, isPending: false }),
    useReactivatePortfolio: () => ({ mutateAsync: fixtures.reactivate, isPending: false }),
  };
});

vi.mock("../features/delivery/FinancialRollupView", () => ({
  FinancialRollupView: ({ kind }: { kind: "Program" | "Portfolio" }) => <div>{kind} calculated financial rollup</div>,
}));vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: fixtures.userId, permissions: fixtures.permissions } }) }));
vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@components/ui/ConfirmProvider")>()),
  useConfirm: () => vi.fn().mockResolvedValue(true),
}));
vi.mock("../features/delivery/api/managerHandovers", () => {
  const emptyListQuery = () => ({ data: [], isLoading: false, isError: false, error: null, refetch: vi.fn() });
  const capabilitiesQuery = () => {
    const currentManagerId = fixtures.portfolioRecord.ownerId === "00000000-0000-0000-0000-000000000000" ? undefined : fixtures.portfolioRecord.ownerId;
    return {
      data: {
        ...fixtures.capabilities,
        canInitiate: fixtures.userId === currentManagerId,
        currentManagerId,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    };
  };
  return {
    useManagerHandoverRequests: emptyListQuery,
    useManagerHandoverCapabilities: capabilitiesQuery,
    useSelfPendingManagerHandover: emptyListQuery,
    useManagerAssignmentHistory: emptyListQuery,
    useManagerHandoverSnapshots: emptyListQuery,
    useEligibleManagerHandoverManagers: emptyListQuery,
    useAcceptManagerHandover: () => ({ mutateAsync: vi.fn(), isPending: false }),
    useCancelManagerHandover: () => ({ mutateAsync: vi.fn(), isPending: false }),
    useCreateManagerHandover: () => ({ mutateAsync: vi.fn(), isPending: false }),
    useRejectManagerHandover: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});
vi.mock("../features/delivery/FinancialRollupView", () => ({
  FinancialRollupView: ({ kind }: { kind: "Program" | "Portfolio" }) => <div>{kind} calculated financial rollup</div>,
}));
vi.mock("@api/management", () => ({
  managementCacheScopeKey: (userId?: string) => userId ?? "anonymous",
  useManagement: () => ({ context: null, effectiveReadOnly: fixtures.effectiveReadOnly, loading: false, canMutate: fixtures.canMutate }),
}));
vi.mock("../features/delivery/ProgramSheet", () => ({
  ProgramSheet: ({ open, program }: { open: boolean; program?: { name: string } }) => open ? <div role="dialog" aria-label="Edit Program">{program?.name}</div> : null,
}));
vi.mock("../features/delivery/PortfolioSheet", () => ({ PortfolioSheet: () => null }));

function renderProgram() {
  return render(<I18nProvider><MemoryRouter initialEntries={["/portfolios/programs/program-1"]}><Routes><Route path="/portfolios/programs/:id" element={<ManagedProgramDetailPage />} /><Route path="/portfolios/programs" element={<p>Programs list</p>} /></Routes></MemoryRouter></I18nProvider>);
}

function renderPortfolio() {
  return render(<I18nProvider><MemoryRouter initialEntries={["/portfolios/portfolio-1"]}><Routes><Route path="/portfolios/:id" element={<ManagedPortfolioDetailPage />} /></Routes></MemoryRouter></I18nProvider>);
}

describe("program management overview source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.overview.program.status = "Active";
    fixtures.archive.mockReset().mockResolvedValue(undefined);
    fixtures.reactivate.mockReset().mockResolvedValue(undefined);
    fixtures.permissions = [];
    fixtures.userId = "portfolio-manager-1";
    fixtures.portfolioRecord.ownerId = "portfolio-manager-1";
    fixtures.portfolioRecord.ownerName = "Alex Manager";
    fixtures.portfolioRecordLoading = false;
    fixtures.portfolioRecordError = false;
    fixtures.canMutate = false;
    fixtures.effectiveReadOnly = false;
  });

  it("shows all source summary metrics, API-backed attention items, and overview widgets", () => {
    renderProgram();

    expect(screen.getByText("Completed Projects")).toBeInTheDocument();
    expect(screen.getByText("Schedule baseline needs recovery")).toHaveAttribute("href", "/projects/project-1");
    const dashboard = screen.getByRole("region", { name: "Dashboard" });
    expect(dashboard.querySelectorAll("[data-widget-id]")).toHaveLength(6);
    expect(dashboard.querySelector('[data-widget-id="program.health-overview"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="program.delivery-milestones"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="program.risk-issues"]')).toBeInTheDocument();
    expect(dashboard.querySelectorAll('[class*="min-h-40"]')).toHaveLength(0);
    expect(screen.getByText("Health overview")).toBeInTheDocument();
    expect(screen.getByText("Delivery & milestones")).toBeInTheDocument();
    expect(screen.getByText("Risks & issues")).toBeInTheDocument();
    expect(screen.getAllByText("Status Reports").length).toBeGreaterThanOrEqual(2);
  });

  it("uses the authorized calculated financial rollup instead of overview totals", () => {
    fixtures.permissions = [
      "Program.View",
      "ProgramFinancialRollup.View",
    ];
    renderProgram();

    fireEvent.click(screen.getByRole("tab", { name: "Financials" }));
    expect(screen.getByText("Program calculated financial rollup")).toBeInTheDocument();
    expect(screen.queryByText("125000")).not.toBeInTheDocument();
  });

  it("does not let ProjectFinance.View shell access expose Program rollups", () => {
    fixtures.permissions = ["Program.View", "ProjectFinance.View"];
    renderProgram();

    expect(screen.queryByRole("tab", { name: "Financials" })).not.toBeInTheDocument();
  });

  it("localizes the grouped overview card titles in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderProgram();

    expect(screen.getByText("نظرة عامة على الصحة")).toBeInTheDocument();
    expect(screen.getByText("التنفيذ والمعالم")).toBeInTheDocument();
    expect(screen.getAllByText("المخاطر والقضايا")).toHaveLength(1);
  });

  it("renders seven compact, grouped Portfolio overview cards", () => {
    renderPortfolio();

    const dashboard = screen.getByRole("region", { name: "Overview" });
    expect(dashboard.querySelectorAll("[data-widget-id]")).toHaveLength(6);
    expect(dashboard.querySelector('[data-widget-id="portfolio.health-overview"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="portfolio.delivery-milestones"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="portfolio.risk-issues"]')).toBeInTheDocument();
    expect(dashboard.querySelectorAll('[class*="min-h-40"]')).toHaveLength(0);
  });

  it("uses the canonical Portfolio owner ID to show the request action to its current manager", () => {
    fixtures.permissions = ["Handover.View", "Handover.Manage"];
    fixtures.canMutate = true;
    fixtures.userId = "portfolio-manager-1";
    fixtures.portfolioRecord.ownerId = "portfolio-manager-1";
    renderPortfolio();

    fireEvent.click(screen.getByRole("tab", { name: "Manager handover" }));

    expect(screen.getByRole("button", { name: "Request handover" })).toBeInTheDocument();
    expect(screen.queryByText("This entity has no current manager, so a handover cannot be initiated.")).not.toBeInTheDocument();
  });

  it("keeps the request action hidden from an admin who is not the current manager", () => {
    fixtures.permissions = ["Handover.View", "Handover.Manage"];
    fixtures.canMutate = true;
    fixtures.userId = "unrelated-admin";
    fixtures.portfolioRecord.ownerId = "portfolio-manager-1";
    renderPortfolio();

    fireEvent.click(screen.getByRole("tab", { name: "Manager handover" }));

    expect(screen.queryByRole("button", { name: "Request handover" })).not.toBeInTheDocument();
    expect(screen.getByText("Only the current manager (Alex Manager) can initiate a handover.")).toBeInTheDocument();
  });

  it("does not report an unassigned portfolio while its manager details are loading", () => {
    fixtures.permissions = ["Handover.View", "Handover.Manage"];
    fixtures.canMutate = true;
    fixtures.portfolioRecordLoading = true;
    renderPortfolio();

    fireEvent.click(screen.getByRole("tab", { name: "Manager handover" }));

    expect(screen.getByText("Loading...", { exact: true })).toBeInTheDocument();
    expect(screen.queryByText("This entity has no current manager, so a handover cannot be initiated.")).not.toBeInTheDocument();
  });

  it("treats the API empty GUID as an unassigned Portfolio manager", () => {
    fixtures.permissions = ["Handover.View", "Handover.Manage"];
    fixtures.canMutate = true;
    fixtures.userId = "unrelated-admin";
    fixtures.portfolioRecord.ownerId = "00000000-0000-0000-0000-000000000000";
    renderPortfolio();

    fireEvent.click(screen.getByRole("tab", { name: "Manager handover" }));

    expect(screen.queryByRole("button", { name: "Request handover" })).not.toBeInTheDocument();
    expect(screen.getByText("This entity has no current manager, so a handover cannot be initiated.")).toBeInTheDocument();
  });

  it("keeps the program sections as accessible tabs", () => {
    renderProgram();

    const projectsTab = screen.getByRole("tab", { name: "Projects" });
    expect(projectsTab).toHaveAttribute("aria-selected", "false");
    fireEvent.click(projectsTab);
    expect(projectsTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("columnheader", { name: "Project" })).toBeInTheDocument();
  });

  it("supports edit and reversible archive with explicit Program actions in mutable context", async () => {
    fixtures.permissions = ["Program.Edit", "Program.Archive", "Program.Reactivate"];
    fixtures.canMutate = true;
    renderProgram();

    fireEvent.click(screen.getByRole("button", { name: "Edit Program" }));
    expect(screen.getByRole("dialog", { name: "Edit Program" })).toHaveTextContent("Core Renewal");
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    await waitFor(() => expect(fixtures.archive).toHaveBeenCalledWith("program-1"));
    expect(screen.getByText("Programs list")).toBeInTheDocument();
  });

  it("reactivates archived programs and hides mutations in read-only or unauthorized contexts", async () => {
    fixtures.overview.program.status = "Archived";
    fixtures.permissions = ["Program.Edit", "Program.Archive", "Program.Reactivate"];
    fixtures.canMutate = true;
    const { unmount } = renderProgram();
    fireEvent.click(screen.getByRole("button", { name: "Reactivate" }));
    await waitFor(() => expect(fixtures.reactivate).toHaveBeenCalledWith("program-1"));

    unmount();
    fixtures.permissions = ["Program.Edit", "Program.Archive", "Program.Reactivate"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = true;
    renderProgram();
    expect(screen.queryByRole("button", { name: "Edit Program" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reactivate" })).not.toBeInTheDocument();
  });
});
