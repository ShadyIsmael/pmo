import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { AcceptInvitationPage, StakeholderProjectDetailPage, StakeholderProjectsPage } from "@features/stakeholder/StakeholderPages";

const stakeholderMocks = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), ApiError: class ApiError extends Error {} }));

vi.mock("@api/client", () => ({ api: { get: stakeholderMocks.get, post: stakeholderMocks.post }, ApiError: stakeholderMocks.ApiError, setApiErrorLocale: () => undefined }));
vi.mock("@features/projects/lib/project-labels", () => ({
  useProjectEnumLabels: () => ({
    statusLabel: (value?: string | null) => value ? `status:${value}` : "—",
    healthLabel: (value?: string | null) => value ? `health:${value}` : "—",
  }),
}));

const project = {
  id: "project-1",
  publicId: "public-project-1",
  code: "PRJ-001",
  name: "Core Platform",
  description: "Platform modernization",
  status: "Active",
  health: "OnTrack",
  progress: 42,
  startDate: "2026-01-01T00:00:00Z",
  endDate: "2026-12-31T00:00:00Z",
  projectManagerName: "Alex Morgan",
  sponsorName: "Jordan Lee",
  latestPublishedStatusReport: {
    id: "report-1",
    reportDate: "2026-02-01T00:00:00Z",
    periodFrom: "2026-01-01T00:00:00Z",
    periodTo: "2026-01-31T00:00:00Z",
    projectLifecycleStatus: "Active",
    overallHealth: "OnTrack",
    overallProgressPercent: 42,
    scheduleHealth: "OnTrack",
    executiveSummary: "Delivery remains on plan.",
  },
  publishedStatusHistory: [{
    id: "report-1",
    reportDate: "2026-02-01T00:00:00Z",
    periodFrom: "2026-01-01T00:00:00Z",
    periodTo: "2026-01-31T00:00:00Z",
    projectLifecycleStatus: "Active",
    overallHealth: "OnTrack",
    overallProgressPercent: 42,
  }],
  milestones: [{ id: "milestone-1", name: "Architecture approved", dueDate: "2026-03-01T00:00:00Z", status: "Completed", progress: 100 }],
};

function renderRoute(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <I18nProvider>
          <Routes>
            <Route path="/stakeholder/accept-invitation" element={<AcceptInvitationPage />} />
            <Route path="/stakeholder/projects" element={<StakeholderProjectsPage />} />
            <Route path="/stakeholder/projects/:id" element={<StakeholderProjectDetailPage />} />
          </Routes>
        </I18nProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("stakeholder portal parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    stakeholderMocks.get.mockReset();
    stakeholderMocks.post.mockReset();
  });

  it("renders the source project table and retries failed list requests", async () => {
    stakeholderMocks.get.mockRejectedValueOnce(new Error("Temporary failure")).mockResolvedValue({ items: [project] });
    renderRoute("/stakeholder/projects");

    expect(await screen.findByRole("alert")).toHaveTextContent("Unable to load stakeholder projects");
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByRole("columnheader", { name: "Latest published update" })).toBeInTheDocument();
    expect(screen.getByText("PRJ-001")).toBeInTheDocument();
    expect(screen.getByText("status:Active")).toBeInTheDocument();
    expect(screen.getByText("health:OnTrack")).toBeInTheDocument();
    expect(screen.getByText("42 %")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "View" })).toHaveAttribute("href", "/stakeholder/projects/public-project-1");
    expect(stakeholderMocks.get).toHaveBeenLastCalledWith("/api/stakeholder/projects", { page: 1, pageSize: 20 });
  });

  it("renders the source-shaped public invitation loading state", () => {
    stakeholderMocks.post.mockReturnValue(new Promise(() => undefined));
    renderRoute("/stakeholder/accept-invitation#token=invite-token");

    expect(screen.getByRole("status", { name: "Loading..." })).toBeInTheDocument();
    expect(stakeholderMocks.post).toHaveBeenCalledWith("/api/stakeholder-invitations/validate", { token: "invite-token" });
  });

  it("shows localized explanatory text when an invitation token is missing", () => {
    renderRoute("/stakeholder/accept-invitation");

    expect(screen.getByRole("heading", { name: "This invitation is invalid or no longer available." })).toBeInTheDocument();
    expect(screen.getByText("The invitation could not be accepted.")).toBeInTheDocument();
    expect(stakeholderMocks.post).not.toHaveBeenCalled();
  });

  it("localizes expired invitation tokens", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    stakeholderMocks.post.mockRejectedValueOnce(new stakeholderMocks.ApiError("Invitation expired"));
    renderRoute("/stakeholder/accept-invitation?token=expired-token");
    expect(await screen.findByRole("heading", { name: "انتهت صلاحية هذه الدعوة." })).toBeInTheDocument();
  });

  it("localizes the missing invitation state in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderRoute("/stakeholder/accept-invitation");
    expect(screen.getByRole("heading", { name: "هذه الدعوة غير صالحة أو لم تعد متاحة." })).toBeInTheDocument();
    expect(screen.getByText("تعذر قبول الدعوة.")).toBeInTheDocument();
  });

  it("accepts a validated invitation with project-specific consent and no account credentials", async () => {
    stakeholderMocks.post
      .mockResolvedValueOnce({ resourceName: "Morgan Resource", email: "morgan@example.com", projectName: "Core Platform" })
      .mockResolvedValueOnce({ status: "Accepted", reportsConsent: true, notificationsConsent: false });
    renderRoute("/stakeholder/accept-invitation?token=invite-token");

    const heading = await screen.findByRole("heading", { name: "Join as an external project stakeholder" });
    expect(heading).toBeInTheDocument();
    expect(screen.getByText("Stakeholder")).toHaveClass("border-input", "bg-background");
    expect(screen.getByText("Morgan Resource")).toBeInTheDocument();

    expect(screen.getByText(/does not create a login or grant access to project APIs/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/password/i)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("checkbox", { name: "I accept participation as an external stakeholder for this project." }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Email me published status reports for this project." }));
    fireEvent.submit(heading.closest("main")!.querySelector("form")!);

    expect(await screen.findByRole("heading", { name: "Your acceptance was recorded for this project." })).toBeInTheDocument();
    expect(stakeholderMocks.post).toHaveBeenCalledWith("/api/stakeholder-invitations/accept", {
      token: "invite-token",
      acceptParticipation: true,
      receiveProjectReports: true,
      receiveProjectNotifications: false,
    });
    expect(stakeholderMocks.post.mock.calls[0]?.[1]).not.toHaveProperty("password");
    expect(screen.queryByRole("link", { name: /sign in/i })).not.toBeInTheDocument();
  });

  it("lets an accepted external stakeholder withdraw report consent without project access", async () => {
    stakeholderMocks.post
      .mockResolvedValueOnce({
        resourceName: "Morgan Resource", email: "morgan@example.com", projectName: "Core Platform",
        status: "Accepted", reportsConsent: true, notificationsConsent: true,
      })
      .mockResolvedValueOnce({ status: "Accepted", reportsConsent: false, notificationsConsent: true });
    renderRoute("/stakeholder/accept-invitation?token=invite-token");

    expect(await screen.findByRole("heading", { name: "Your acceptance was recorded for this project." })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Stop project report emails" }));

    await waitFor(() => expect(stakeholderMocks.post).toHaveBeenCalledWith(
      "/api/stakeholder-invitations/withdraw-consent",
      { token: "invite-token", withdrawReportsConsent: true, withdrawNotificationsConsent: false },
    ));
    expect(await screen.findByText("Off")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Stop project notification emails" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /sign in/i })).not.toBeInTheDocument();
  });

  it("declines a validated invitation without creating an account", async () => {
    stakeholderMocks.post
      .mockResolvedValueOnce({ resourceName: "Morgan Resource", email: "morgan@example.com", projectName: "Core Platform" })
      .mockResolvedValueOnce(undefined);
    renderRoute("/stakeholder/accept-invitation?token=decline-token");

    fireEvent.click(await screen.findByRole("button", { name: "Decline invitation" }));

    expect(await screen.findByRole("heading", { name: "You declined this project invitation." })).toBeInTheDocument();
    expect(stakeholderMocks.post).toHaveBeenCalledWith("/api/stakeholder-invitations/decline", { token: "decline-token" });
    expect(screen.getByText(/does not create a login/i)).toBeInTheDocument();
  });

  it("renders published details, milestone progress, and full status history", async () => {
    stakeholderMocks.get.mockResolvedValue(project);
    renderRoute("/stakeholder/projects/project-1");

    expect(await screen.findByRole("heading", { name: "Core Platform" })).toBeInTheDocument();
    expect(screen.getByText(/PRJ-001 · Platform modernization/)).toBeInTheDocument();
    expect(screen.getByText("Delivery remains on plan.")).toBeInTheDocument();
    expect(screen.getAllByText("health:OnTrack").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Architecture approved")).toBeInTheDocument();
    expect(screen.getAllByRole("columnheader", { name: "Progress" }).length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("100 %")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Reporting period" })).toBeInTheDocument();
    await waitFor(() => expect(stakeholderMocks.get).toHaveBeenCalledWith("/api/stakeholder/projects/project-1"));
  });
});
