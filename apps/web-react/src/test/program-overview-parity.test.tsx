import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { ManagedPortfolioDetailPage, ManagedProgramDetailPage } from "../features/delivery/PortfolioProgramPages";

const fixtures = vi.hoisted(() => ({
  overview: {
    program: { id: "program-1", name: "Core Renewal", code: "PGM-001", status: "Active", health: "Amber", programManagerName: "Alex Manager", portfolioName: "Technology", currency: "USD", startDate: "2026-01-01", endDate: "2026-12-31", budget: 125000 },
    summary: { totalProjects: 8, activeProjects: 5, completedProjects: 2, projectsAtRisk: 3, criticalProjects: 1, averageProjectProgress: 42, projectHealthDistribution: { Green: 4, Amber: 3, Red: 1 } },
    delivery: { projectsBehindSchedule: 2, projectsWithScheduleData: 7, totalMilestones: 12, overdueMilestones: 1, dueSoonMilestones: 2, onTrackMilestones: 7, completedMilestones: 2 },
    financials: { baseCurrency: "USD", hasMixedCurrencies: false, groups: [{ currency: "USD", isBaseCurrency: true, budget: 125000, actual: 42000, committed: 10000, forecast: 130000, remaining: 73000, projects: [{ projectId: "finance-sample", projectName: "Finance sample", budget: 100, actual: -20, committed: 10, forecast: -5 }] }] },
    risks: { openCount: 4, overdueCount: 1, bySeverity: { High: 2, Medium: 2 } },
    issues: { openCount: 2, overdueCount: 1, bySeverity: { High: 1, Low: 1 } },
    resources: { activeAllocationCount: 9, activeResourceCount: 6, sharedResourceCount: 1, overAllocatedResourceCount: 1, allocatedHoursPerWeek: 210, pressure: [] },
    statusReports: { publishedProjectCount: 6, currentCount: 4, staleCount: 1, missingCount: 1, freshnessDays: 30, latestPublished: [] },
    projects: [], projectsPage: 1, projectsTotalPages: 1,
    milestones: [{ id: "milestone-1", projectId: "project-1", projectName: "Core Project", name: "Schedule baseline", state: "Overdue" }],
    topRisks: [], topIssues: [],
    attentionItems: [{ level: "Critical", kind: "Milestone", message: "Schedule baseline needs recovery", projectId: "project-1", itemId: "milestone-1" }],
    upcomingMeetings: [{ id: "meeting-1", title: "Weekly review", startTime: "2026-03-12T10:00:00Z", projectId: "project-1", projectName: "Core Project" }],
  },
  portfolioOverview: {
    portfolio: { id: "portfolio-1", name: "Technology", code: "PF-001", status: "Active", health: "Green", currency: "USD", ownerName: "Alex Manager", strategicObjective: "Modernize platforms", priority: "High", startDate: "2026-01-01", endDate: "2026-12-31" },
    summary: { totalPrograms: 1, totalProjects: 2, programContainedProjects: 1, directPortfolioProjects: 1, activeProjects: 1, completedProjects: 0, atRiskProjects: 1, criticalProjects: 0, averageProjectProgress: 50, programHealthDistribution: { Green: 1 }, projectHealthDistribution: { Amber: 1 } },
    delivery: { programDeliveryConditions: { OnTrack: 1 }, projectScheduleConditions: { Delayed: 1 }, delayedProjects: 1, delayedPrograms: 0, projectsWithScheduleData: 2, totalMilestones: 1, overdueMilestones: 1, dueSoonMilestones: 0, onTrackMilestones: 0, completedMilestones: 0, projectProgressDistribution: {} },
    financials: { baseCurrency: "USD", hasMixedCurrencies: false, groups: [{ currency: "USD", isBaseCurrency: true, projectCount: 2, budget: 50000, allocated: 25000, actual: 12000, committed: 3000, forecast: 52000, remaining: 10000, variance: -2000, contributions: [] }] },
    risks: { sourceScope: "Project", openCount: 1, criticalCount: 0, highCount: 1, overdueCount: 0, byProgram: {}, byProject: {} },
    issues: { openCount: 1, criticalCount: 0, overdueCount: 0, byProgram: {}, byProject: {} },
    resources: { activeAllocationCount: 2, activeResourceCount: 2, sharedResourceCount: 0, crossProgramResourceCount: 0, overAllocatedResourceCount: 0, allocatedHoursPerWeek: 40, pressure: [] },
    statusReports: { publishedProjectCount: 1, currentCount: 1, staleCount: 0, missingCount: 1, freshnessDays: 30, programsWithReportingGaps: [], projectsWithReportingGaps: [], latestPublished: [] },
    programs: [], projects: [], milestones: [], topRisks: [], topIssues: [], attentionItems: [], upcomingMeetings: [],
    programsPage: 1, programsTotalPages: 1, projectsPage: 1, projectsTotalPages: 1,
  },
  portfolioRecord: { id: "portfolio-1", name: "Technology", code: "PF-001", status: "Active", rowVersion: [1] },
  programRecord: { id: "program-1", name: "Core Renewal", code: "PGM-001", status: "Active", portfolioId: "portfolio-1", rowVersion: [1] },
  archive: vi.fn(),
  reactivate: vi.fn(),
  permissions: [] as string[],
  canMutate: false,
  effectiveReadOnly: false,
}));

vi.mock("../features/delivery/api/delivery", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/delivery/api/delivery")>();
  return {
    ...actual,
    useProgramManagementOverview: () => ({ data: fixtures.overview, isLoading: false, isError: false }),
    usePortfolioManagementOverview: () => ({ data: fixtures.portfolioOverview, isLoading: false, isError: false }),
    useProgram: () => ({ data: fixtures.programRecord, isLoading: false, isError: false }),
    usePortfolio: () => ({ data: fixtures.portfolioRecord, isLoading: false, isError: false }),
    useArchiveProgram: () => ({ mutateAsync: fixtures.archive, isPending: false }),
    useReactivateProgram: () => ({ mutateAsync: fixtures.reactivate, isPending: false }),
    useArchivePortfolio: () => ({ mutateAsync: fixtures.archive, isPending: false }),
    useReactivatePortfolio: () => ({ mutateAsync: fixtures.reactivate, isPending: false }),
  };
});

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
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
    fixtures.overview.financials.groups[0].projects = [];
    fixtures.archive.mockReset().mockResolvedValue(undefined);
    fixtures.reactivate.mockReset().mockResolvedValue(undefined);
    fixtures.permissions = [];
    fixtures.canMutate = false;
    fixtures.effectiveReadOnly = false;
  });

  it("shows all source summary metrics, API-backed attention items, and overview widgets", () => {
    renderProgram();

    expect(screen.getByText("Completed Projects")).toBeInTheDocument();
    expect(screen.getByText("Schedule baseline needs recovery")).toHaveAttribute("href", "/projects/project-1");
    const dashboard = screen.getByRole("region", { name: "Dashboard" });
    expect(dashboard.querySelectorAll("[data-widget-id]")).toHaveLength(7);
    expect(dashboard.querySelector('[data-widget-id="program.health-overview"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="program.delivery-milestones"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="program.risk-issues"]')).toBeInTheDocument();
    expect(dashboard.querySelectorAll('[class*="min-h-40"]')).toHaveLength(0);
    expect(screen.getByText("Health overview")).toBeInTheDocument();
    expect(screen.getByText("Delivery & milestones")).toBeInTheDocument();
    expect(screen.getByText("Risks & issues")).toBeInTheDocument();
    expect(screen.getAllByText("Status Reports").length).toBeGreaterThanOrEqual(2);
  });

  it("uses dark blue for aggregated financial totals and sign colors for project amounts", () => {
    fixtures.overview.financials.groups[0].projects = [{ projectId: "finance-sample", projectName: "Finance sample", budget: 100, actual: -20, committed: 10, forecast: -5 }];
    renderProgram();

    fireEvent.click(screen.getByRole("tab", { name: "Financials" }));

    const budgetLabel = screen.getAllByText("Budget", { exact: true })[0];
    expect(budgetLabel.parentElement?.querySelector("p.mt-1")).toHaveClass("text-financial-total");
    const row = screen.getByRole("row", { name: /Finance sample/ });
    const cells = within(row).getAllByRole("cell");
    expect(cells[1]).toHaveClass("text-success");
    expect(cells[2]).toHaveClass("text-destructive");
  });

  it("localizes the grouped overview card titles in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderProgram();

    expect(screen.getByText("نظرة عامة على الصحة")).toBeInTheDocument();
    expect(screen.getByText("التنفيذ والمعالم")).toBeInTheDocument();
    expect(screen.getAllByText("المخاطر والقضايا")).toHaveLength(2);
  });

  it("renders seven compact, grouped Portfolio overview cards", () => {
    renderPortfolio();

    const dashboard = screen.getByRole("region", { name: "Overview" });
    expect(dashboard.querySelectorAll("[data-widget-id]")).toHaveLength(7);
    expect(dashboard.querySelector('[data-widget-id="portfolio.health-overview"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="portfolio.delivery-milestones"]')).toBeInTheDocument();
    expect(dashboard.querySelector('[data-widget-id="portfolio.risk-issues"]')).toBeInTheDocument();
    expect(dashboard.querySelectorAll('[class*="min-h-40"]')).toHaveLength(0);
  });

  it("keeps the program sections as accessible tabs", () => {
    renderProgram();

    const projectsTab = screen.getByRole("tab", { name: "Projects" });
    expect(projectsTab).toHaveAttribute("aria-selected", "false");
    fireEvent.click(projectsTab);
    expect(projectsTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("columnheader", { name: "Project" })).toBeInTheDocument();
  });

  it("supports edit and reversible archive with Program.Manage in mutable context", async () => {
    fixtures.permissions = ["Program.Manage"];
    fixtures.canMutate = true;
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    renderProgram();

    fireEvent.click(screen.getByRole("button", { name: "Edit Program" }));
    expect(screen.getByRole("dialog", { name: "Edit Program" })).toHaveTextContent("Core Renewal");
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));
    await waitFor(() => expect(fixtures.archive).toHaveBeenCalledWith("program-1"));
    expect(screen.getByText("Programs list")).toBeInTheDocument();
    confirm.mockRestore();
  });

  it("reactivates archived programs and hides mutations in read-only or unauthorized contexts", async () => {
    fixtures.overview.program.status = "Archived";
    fixtures.permissions = ["Program.Manage"];
    fixtures.canMutate = true;
    const { unmount } = renderProgram();
    fireEvent.click(screen.getByRole("button", { name: "Reactivate" }));
    await waitFor(() => expect(fixtures.reactivate).toHaveBeenCalledWith("program-1"));

    unmount();
    fixtures.permissions = ["Program.Manage"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = true;
    renderProgram();
    expect(screen.queryByRole("button", { name: "Edit Program" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reactivate" })).not.toBeInTheDocument();
  });
});
