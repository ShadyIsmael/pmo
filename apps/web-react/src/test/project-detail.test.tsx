import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { AuthProvider, useAuth } from "../auth/AuthProvider";
import { ManagementProvider } from "../api/management";
import { ProjectDetailPage } from "../features/projects/ProjectDetailPage";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";
import { ProjectChangeTrackerTab } from "../features/projects/ProjectGovernanceTabs";
import { setTokens } from "../api/client";

const adminUser = { id: "admin", email: "admin@nexus.com", firstName: "Alex", lastName: "Admin", fullName: "Alex Admin", roles: [{ id: "r1", name: "Administrator" }], permissions: ["projects.read", "Project.View", "Project.Create", "Project.Edit", "Project.Delete", "ChangeRequest.Manage", "Handover.Manage", "Resource.Assign", "finances.read", "finances.write", "Stakeholders.Read", "Stakeholders.Manage"] };

adminUser.permissions.push("ChangeRequest.View");

const shell = {
  id: "p1", code: "P1", name: "Project 1", status: "Active", priority: "High", phase: "Execution",
  health: "Green", currency: "USD", progress: 40, plannedValue: 100, earnedValue: 80,
  members: [], projectManagerName: "A Lead", businessCase: "BC", notes: "N",
};

const overview = {
  progress: 40,
  program: { id: "pr1", name: "Program 1", code: "PR-1" },
  currentBaseline: { id: "b1", name: "Baseline 1", version: 2, isCurrent: true },
  hasBudgetData: true,
  hasCostEntryData: true,
  hasPendingRevision: true,
  originalApprovedBudget: 900,
  risks: { openCount: 2, highCriticalOpenCount: 1, topRisks: [{ id: "r1", title: "Risk 1", riskLevel: "High", status: "Open" }] },
  issues: { openIssueCount: 1, overdueOpenIssueCount: 0, topIssues: [{ id: "i1", title: "Issue 1", status: "Open", priority: "High", dueDate: "2026-11-01" }] },
  milestones: [{ id: "m1", name: "M1", dueDate: "2026-12-01", status: "Active", progress: 10 }],
  pendingChangeRequestCount: 1,
  pendingHandoverCount: 1,
  allocations: { activeAllocationCount: 2, activeResourceCount: 2, allocatedHoursPerWeek: 40 },
  meetings: { meetingCount: 3, finalMinutesCount: 1 },
  plannedValue: 100, earnedValue: 80, actualCost: 90, currency: "USD",
  scheduleVariance: -5, costVariance: -2, schedulePerformanceIndex: 0.9, costPerformanceIndex: 0.95,
  approvedBudget: 1000, remainingBudget: 100,
  statusReports: { totalCount: 1, publishedCount: 1, latestPublishedReport: { id: "sr1", reportDate: "2026-09-01", periodFrom: "2026-08-01", periodTo: "2026-08-31", reportState: "Published", projectLifecycleStatusAtReport: "OnHold", overallHealth: "Amber", overallProgressPercent: 35, executiveSummary: "Exec summary", publishedAt: "2026-09-02" } },
};

describe("detail freeze repro", () => {
  it("renders full overview with EVM + milestones without hanging", async () => {
    setTokens("a", "r");
    const changeRequests: Array<Record<string, unknown>> = [];
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify(adminUser), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ nativePerspectives: ["ProjectManager"], availablePerspectives: [{ perspective: "ProjectManager", isNative: true, requiresEntityContext: false, contexts: [] }], defaultPerspective: "ProjectManager", canViewAs: false, scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/projects/p1/shell")) return Promise.resolve(new Response(JSON.stringify(shell), { status: 200 }));
      if (input.includes("/api/projects/p1/overview")) return Promise.resolve(new Response(JSON.stringify(overview), { status: 200 }));
      if (input.includes("/api/projects/p1/handover-managers")) return Promise.resolve(new Response(JSON.stringify([
        { id: "manager-2", fullName: "New Manager", email: "new.manager@example.com", managerTypes: ["ProjectManager"] },
        { id: "manager-3", fullName: "Portfolio Lead", email: "portfolio.manager@example.com", managerTypes: ["PortfolioManager"] },
        { id: "manager-4", fullName: "Program Lead", email: "program.manager@example.com", managerTypes: ["ProgramManager"] },
      ]), { status: 200 }));
      if (input.includes("/api/projects/p1/handover-requests") && init?.method === "POST") return Promise.resolve(new Response(JSON.stringify({ id: "handover-1", status: "Pending" }), { status: 201 }));
      if (input.includes("/api/projects/p1/handover-requests")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      if (input.includes("/api/project-change-requests/") && input.endsWith("/cancel") && init?.method === "POST") {
        const requestId = input.split("/").at(-2);
        const request = changeRequests.find((candidate) => candidate.id === requestId);
        if (request) request.status = "Cancelled";
        return Promise.resolve(new Response(JSON.stringify(request ?? {}), { status: 200 }));
      }
      if (input.includes("/api/projects/p1/change-requests") && init?.method === "POST") {
        const body = JSON.parse(String(init.body));
        const request = { ...body, id: `change-${changeRequests.length + 1}`, number: changeRequests.length + 1, projectName: "Project 1", status: "Pending", requestedBy: "admin", requestedByName: "Alex Admin", requestedAt: "2026-10-08T10:00:00Z", createdAt: "2026-10-08T10:00:00Z", createdBy: "admin" };
        changeRequests.push(request);
        return Promise.resolve(new Response(JSON.stringify(request), { status: 201 }));
      }
      if (input.includes("/api/projects/p1/change-requests")) return Promise.resolve(new Response(JSON.stringify(changeRequests), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    function Bridge({ children }: { children: React.ReactNode }) {
      const { user } = useAuth();
      return <ManagementProvider user={user}>{children}</ManagementProvider>;
    }
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <AuthProvider>
            <Bridge>
              <MemoryRouter initialEntries={["/projects/p1/overview"]}>
                <Routes>
                  <Route path="/projects/:id" element={<ProjectDetailPage />}>
                    <Route path="overview" element={<ProjectOverviewTab />} />
                    <Route path="change-tracker" element={<ProjectChangeTrackerTab />} />
                  </Route>
                </Routes>
              </MemoryRouter>
            </Bridge>
          </AuthProvider>
        </I18nProvider>
      </QueryClientProvider>
    );
    await waitFor(() => expect(screen.getByText("Project 1")).toBeInTheDocument(), { timeout: 15000 });
    const projectHeading = screen.getByRole("heading", { name: "Project 1" });
    const projectTabs = screen.getByRole("tablist");
    expect(projectHeading.compareDocumentPosition(projectTabs) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(projectTabs).toHaveClass("w-full", "justify-between");
    expect(projectTabs.parentElement).toHaveClass("nexus-fullbleed-tabs");
    await waitFor(() => expect(screen.getByText("On Hold")).toBeInTheDocument(), { timeout: 15000 });
    expect(screen.getByText((_, element) => element?.tagName === "P" && Boolean(element.textContent?.includes("P1 · Execution")))).toBeInTheDocument();
    expect(screen.getAllByText("At Risk").length).toBeGreaterThanOrEqual(1);
    await waitFor(() => expect(screen.getByText("Program 1 · PR-1")).toBeInTheDocument(), { timeout: 15000 });
    await waitFor(() => expect(screen.getAllByText("M1").length).toBeGreaterThanOrEqual(1), { timeout: 15000 });
    const performanceChart = screen.getByRole("img", { name: /project performance/i });
    await waitFor(() => expect(performanceChart.querySelector("svg.recharts-surface")).toBeInTheDocument());
    const initialOverviewCalls = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects/p1/overview")).length;
    const shellCalls = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects/p1/shell")).length;
    expect(initialOverviewCalls).toBeLessThanOrEqual(2);
    expect(shellCalls).toBeLessThanOrEqual(2);

    const user = userEvent.setup();
    await user.click(screen.getByRole("tab", { name: "Change Tracker" }));
    expect(screen.queryByRole("button", { name: /^Change Request$/ })).not.toBeInTheDocument();
    expect(await screen.findByText("No change requests", { exact: true })).toBeInTheDocument();
    expect(screen.getByText("Change requests submitted for this project will appear here.")).toBeInTheDocument();
    await user.click(screen.getByRole("tab", { name: "Overview" }));
    await user.click(screen.getByRole("button", { name: "Change Request" }));
    const changeSheet = await screen.findByRole("dialog", { name: "Project Change Request" });
    expect(changeSheet.querySelector('label[for="project-change-type"]')).toHaveTextContent("*");
    expect(changeSheet.querySelector('label[for="project-change-reason"]')).toHaveTextContent("*");
    await user.type(within(changeSheet).getByLabelText("Reason", { exact: false }), "   ");
    await user.click(within(changeSheet).getByRole("button", { name: "Submit Change Request" }));
    expect(await within(changeSheet).findByRole("alert")).toHaveTextContent("Enter a reason for the change request.");
    expect(fetchMock.mock.calls.filter(([input, init]) => String(input).includes("/api/projects/p1/change-requests") && init?.method === "POST")).toHaveLength(0);
    await user.clear(within(changeSheet).getByLabelText("Reason", { exact: false }));
    await user.type(within(changeSheet).getByLabelText("Reason", { exact: false }), "Update delivery baseline");
    await user.type(within(changeSheet).getByLabelText("Attachments"), "baseline-review.pdf");
    await user.type(within(changeSheet).getByLabelText("Budget impact amount"), "100");
    await user.click(within(changeSheet).getByRole("button", { name: "Submit Change Request" }));
    expect(await within(changeSheet).findByRole("alert")).toHaveTextContent("Set an approved project budget before requesting a budget change.");
    expect(fetchMock.mock.calls.filter(([input, init]) => String(input).includes("/api/projects/p1/change-requests") && init?.method === "POST")).toHaveLength(0);
    await user.clear(within(changeSheet).getByLabelText("Budget impact amount"));
    await user.click(within(changeSheet).getByRole("button", { name: "Submit Change Request" }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([input, init]) => String(input).includes("/api/projects/p1/change-requests") && init?.method === "POST")).toBe(true));
    const changeCall = fetchMock.mock.calls.find(([input, init]) => String(input).includes("/api/projects/p1/change-requests") && init?.method === "POST");
    expect(JSON.parse(String(changeCall?.[1]?.body))).toMatchObject({ projectId: "p1", type: "Administrative", reason: "Update delivery baseline", attachments: "baseline-review.pdf" });

    await user.click(screen.getByRole("tab", { name: "Change Tracker" }));
    await screen.findByRole("columnheader", { name: "Change Type" });
    expect(screen.getAllByRole("columnheader").slice(0, 2).map((header) => header.textContent)).toEqual(["Change Type", "Number"]);
    expect(screen.queryByRole("button", { name: /^Change Request$/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Submit Change Request" })).not.toBeInTheDocument();
    const cancelButtons = await screen.findAllByRole("button", { name: "Cancel Request" });
    await user.click(cancelButtons[0]);
    const cancelDialog = await screen.findByRole("dialog", { name: "Cancel change request" });
    await user.click(within(cancelDialog).getByRole("button", { name: "Confirm cancellation" }));
    await waitFor(() => expect(changeRequests[0]?.status).toBe("Cancelled"));
    expect(await screen.findByText("Cancelled")).toBeInTheDocument();

    await user.click(screen.getByRole("tab", { name: "Overview" }));
    await user.click(screen.getByRole("button", { name: "Handover" }));
    const handoverSheet = await screen.findByRole("dialog", { name: "Project Manager Handover" });
    await user.click(within(handoverSheet).getByRole("combobox"));
    expect(await screen.findByRole("option", { name: "New Manager · Project Manager · new.manager@example.com" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Program Lead · Program Manager · program.manager@example.com" })).toBeInTheDocument();
    await user.click(screen.getByRole("option", { name: "Portfolio Lead · Portfolio Manager · portfolio.manager@example.com" }));
    fireEvent.change(within(handoverSheet).getByLabelText("Effective Handover Date"), { target: { value: "2026-12-15" } });
    const handoverReason = within(handoverSheet).getByLabelText("Handover Reason");
    fireEvent.change(handoverReason, { target: { value: "   " } });
    await user.click(within(handoverSheet).getByRole("button", { name: "Submit Handover Request" }));
    expect(await within(handoverSheet).findByText("Handover reason is required when changing the project manager")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) => String(input).includes("/api/projects/p1/handover-requests") && init?.method === "POST")).toBe(false);
    fireEvent.change(handoverReason, { target: { value: "Ownership change" } });
    await user.click(within(handoverSheet).getByRole("button", { name: "Submit Handover Request" }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([input, init]) => String(input).includes("/api/projects/p1/handover-requests") && init?.method === "POST")).toBe(true));
    const handoverCall = fetchMock.mock.calls.find(([input, init]) => String(input).includes("/api/projects/p1/handover-requests") && init?.method === "POST");
    expect(JSON.parse(String(handoverCall?.[1]?.body))).toMatchObject({ newProjectManagerId: "manager-3", effectiveHandoverDate: "2026-12-15", reason: "Ownership change" });
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/projects/p1/handover-managers"))).toBe(true);

    await new Promise((r) => setTimeout(r, 2000));
  }, 30000);
});
