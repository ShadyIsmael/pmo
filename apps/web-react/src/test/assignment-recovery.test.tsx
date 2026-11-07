import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { EntityAssignmentRecoveryPage } from "@features/administration/EntityAssignmentRecoveryPage";
import { Permissions } from "@lib/permissions";
import { ApiError } from "@api/client";

const state = vi.hoisted(() => ({
  permissions: ["Governance.AssignmentRecovery.Manage"],
  items: [] as Array<Record<string, unknown>>,
  projectSummary: {
    publicId: "22222222-2222-4222-8222-222222222222",
    name: "Unassigned project",
    code: "REC-1",
    description: "Replace the regional water monitoring system.",
    startDate: "2026-01-15T00:00:00Z",
    endDate: "2026-08-30T00:00:00Z",
    sponsorName: "Nadia Hassan",
  } as Record<string, unknown> | null,
  projectSummaryLoading: false,
  projectSummaryError: false,
  projectSummarySuccess: true,
  retryProjectSummary: vi.fn(),
  existingRequestDetails: null as Record<string, unknown> | null,
  existingRequestLoading: false,
  existingRequestError: false,
  existingRequestErrorValue: null as unknown,
  retryExistingRequest: vi.fn(),
  createInitial: vi.fn(),
  createHandover: vi.fn(),
  assignSponsor: vi.fn(),
  reconcile: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "tenant-admin", permissions: state.permissions }, status: "authenticated" }),
}));

vi.mock("@api/management", () => ({ useManagement: () => ({ effectiveReadOnly: false }) }));

vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@components/ui/ConfirmProvider")>()),
  useConfirm: () => vi.fn().mockResolvedValue(false),
}));

vi.mock("@features/administration/api/assignmentRecovery", () => ({
  useAssignmentRecoveryPage: () => ({
    data: {
      items: state.items,
      page: 1,
      pageSize: 25,
      totalCount: state.items.length,
      totalPages: 1,
      summary: { unassignedManagers: 1, projectsWithoutSponsor: 1, pendingAssignments: 0, inconsistentAssignments: 0 },
    },
    isLoading: false,
    isError: false,
  }),
  useRecoveryManagerCandidates: () => ({
    data: [{ userId: "33333333-3333-4333-8333-333333333333", fullName: "Incoming Manager", email: "incoming@example.test", managerTypes: [] }],
    isLoading: false,
    isError: false,
  }),
  useRecoveryProjectSummary: () => ({
    data: state.projectSummary,
    isLoading: state.projectSummaryLoading,
    isError: state.projectSummaryError,
    isSuccess: state.projectSummarySuccess,
    refetch: state.retryProjectSummary,
  }),
  useRecoverySponsorCandidates: () => ({ data: [], isLoading: false, isError: false }),
  useAssignmentRecoveryHistory: () => ({ data: { items: [] }, isLoading: false, isError: false }),
  useRecoveryExistingRequestDetails: () => ({
    data: state.existingRequestDetails,
    isLoading: state.existingRequestLoading,
    isError: state.existingRequestError,
    error: state.existingRequestErrorValue,
    refetch: state.retryExistingRequest,
  }),
  useCreateInitialRecoveryAssignment: () => ({ mutateAsync: state.createInitial, isPending: false }),
  useCreateRecoveryHandover: () => ({ mutateAsync: state.createHandover, isPending: false }),
  useAssignRecoverySponsor: () => ({ mutateAsync: state.assignSponsor, isPending: false }),
  useReconcileRecoveryAssignment: () => ({ mutateAsync: state.reconcile, isPending: false }),
}));

function renderPage() {
  return render(<I18nProvider><MemoryRouter><EntityAssignmentRecoveryPage /></MemoryRouter></I18nProvider>);
}

function project(overrides: Record<string, unknown> = {}) {
  return {
    entityType: "Project",
    publicId: "22222222-2222-4222-8222-222222222222",
    name: "Unassigned project",
    code: "REC-1",
    managerId: null,
    managerName: null,
    managerAssignmentState: "Unassigned",
    sponsorId: null,
    sponsorName: null,
    sponsorState: "Unassigned",
    missingParentHierarchy: true,
    assignmentRequestStatus: null,
    createdAt: "2026-10-20T00:00:00Z",
    ...overrides,
  };
}

afterEach(() => {
  state.permissions = ["Governance.AssignmentRecovery.Manage"];
  state.items = [];
  state.projectSummary = {
    publicId: "22222222-2222-4222-8222-222222222222",
    name: "Unassigned project",
    code: "REC-1",
    description: "Replace the regional water monitoring system.",
    startDate: "2026-01-15T00:00:00Z",
    endDate: "2026-08-30T00:00:00Z",
    sponsorName: "Nadia Hassan",
  };
  state.projectSummaryLoading = false;
  state.projectSummaryError = false;
  state.projectSummarySuccess = true;
  state.retryProjectSummary.mockReset();
  state.existingRequestDetails = null;
  state.existingRequestLoading = false;
  state.existingRequestError = false;
  state.existingRequestErrorValue = null;
  state.retryExistingRequest.mockReset();
  state.createInitial.mockReset();
  state.createHandover.mockReset();
  state.assignSponsor.mockReset();
  state.reconcile.mockReset();
});

describe("entity assignment recovery", () => {
  it("shows a pending initial assignment without implying that the manager is already active", () => {
    state.items = [project({
      assignmentRequestStatus: "Pending",
      assignmentInitiationType: "InitialAssignment",
      requestId: "55555555-5555-4555-8555-555555555555",
      canViewExistingRequest: true,
      canReviewExistingRequest: false,
      isInitiatedByCurrentUser: true,
      isAssignedToCurrentUser: false,
      proposedManagerId: "33333333-3333-4333-8333-333333333333",
      proposedManagerName: "Incoming Manager",
    })];

    renderPage();

    expect(screen.getByText("Unassigned project")).toBeInTheDocument();
    expect(screen.getByText("Pending acceptance")).toBeInTheDocument();
    expect(screen.getByText("Proposed: Incoming Manager")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Assign manager" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open My Approvals" })).toHaveAttribute("href", "/my-work/approvals");
    expect(screen.getByText("Initiated by you")).toBeInTheDocument();
    expect(screen.getByText("Awaiting Incoming Manager's acceptance")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "View Existing Request" })).toBeInTheDocument();
  });

  it("opens the existing request for its initiator without offering an approval action", () => {
    const requestId = "55555555-5555-4555-8555-555555555555";
    state.items = [project({
      assignmentRequestStatus: "Pending",
      requestId,
      canViewExistingRequest: true,
      canReviewExistingRequest: false,
      isInitiatedByCurrentUser: true,
      isAssignedToCurrentUser: false,
      proposedManagerName: "Incoming Manager",
    })];
    state.existingRequestDetails = {
      id: requestId,
      projectName: "Unassigned project",
      projectCode: "REC-1",
      status: "Pending",
      currentProjectManagerName: null,
      newProjectManagerName: "Incoming Manager",
      requestedByName: "Tenant Admin",
      requestedAt: "2026-10-20T00:00:00Z",
      effectiveHandoverDate: "2026-10-21T00:00:00Z",
      reason: "Recover ownership",
      notes: null,
    };

    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "View Existing Request" }));

    const dialog = screen.getByRole("dialog", { name: "Existing manager handover request" });
    expect(within(dialog).getByText("Pending acceptance")).toBeInTheDocument();
    expect(within(dialog).getByText("Recover ownership")).toBeInTheDocument();
    expect(within(dialog).getByText("Initiated by you")).toBeInTheDocument();
    expect(within(dialog).getByText("Awaiting Incoming Manager's acceptance")).toBeInTheDocument();
    expect(within(dialog).queryByRole("link", { name: "Open Approval Review" })).not.toBeInTheDocument();
  });

  it("treats an approved scheduled handover as an active blocker and shows its proposed manager", () => {
    state.items = [project({
      assignmentRequestStatus: "ApprovedScheduled",
      requestId: "99999999-9999-4999-8999-999999999999",
      canViewExistingRequest: true,
      canReviewExistingRequest: false,
      isInitiatedByCurrentUser: true,
      isAssignedToCurrentUser: false,
      proposedManagerName: "Incoming Manager",
    })];

    renderPage();

    expect(screen.getByText("Approved and scheduled")).toBeInTheDocument();
    expect(screen.getByText("Proposed: Incoming Manager")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Assign manager" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "View Existing Request" })).toBeInTheDocument();
  });

  it("offers approval review only to the authorized incoming manager", () => {
    const requestId = "66666666-6666-4666-8666-666666666666";
    state.items = [project({
      assignmentRequestStatus: "Pending",
      requestId,
      canViewExistingRequest: true,
      canReviewExistingRequest: true,
      isInitiatedByCurrentUser: false,
      isAssignedToCurrentUser: true,
      proposedManagerName: "Incoming Manager",
    })];
    state.existingRequestDetails = {
      id: requestId,
      projectName: "Unassigned project",
      projectCode: "REC-1",
      status: "Pending",
      newProjectManagerName: "Incoming Manager",
      requestedByName: "Tenant Admin",
      requestedAt: "2026-10-20T00:00:00Z",
      effectiveHandoverDate: "2026-10-21T00:00:00Z",
      reason: "Recover ownership",
    };

    renderPage();
    expect(screen.getByText("Assigned to you")).toBeInTheDocument();
    expect(screen.queryByText("Awaiting Incoming Manager's acceptance")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "View Existing Request" }));

    const dialog = screen.getByRole("dialog", { name: "Existing manager handover request" });
    expect(within(dialog).getByRole("link", { name: "Open Approval Review" })).toHaveAttribute(
      "href",
      `/my-work/approvals/review/project-handover/${requestId}`,
    );
  });

  it("does not expose existing request details when the backend marks the caller as unauthorized", () => {
    state.items = [project({
      assignmentRequestStatus: "Pending",
      requestId: "77777777-7777-4777-8777-777777777777",
      canViewExistingRequest: false,
      canReviewExistingRequest: false,
    })];

    renderPage();

    expect(screen.queryByRole("button", { name: "View Existing Request" })).not.toBeInTheDocument();
  });

  it("identifies rejected handover history as completed instead of waiting for another approver", () => {
    state.items = [project({
      assignmentRequestStatus: "Rejected",
      requestId: "88888888-8888-4888-8888-888888888888",
      canViewExistingRequest: true,
      canReviewExistingRequest: false,
      isInitiatedByCurrentUser: true,
      isAssignedToCurrentUser: false,
      proposedManagerName: "Incoming Manager",
    })];

    renderPage();

    expect(screen.getByText("Rejected")).toBeInTheDocument();
    expect(screen.getByText("Completed")).toBeInTheDocument();
    expect(screen.queryByText("Awaiting Incoming Manager's acceptance")).not.toBeInTheDocument();
  });

  it("shows the structured business conflict message instead of the generic save error", async () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate, "Handover.AdminInitiate"];
    state.items = [project()];
    state.createInitial.mockRejectedValueOnce(new ApiError(
      409,
      "A project manager assignment request is already pending for this project.",
      "HANDOVER_REQUEST_ALREADY_PENDING",
    ));

    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));
    fireEvent.change(screen.getByLabelText("Incoming Manager"), { target: { value: "33333333-3333-4333-8333-333333333333" } });
    fireEvent.change(screen.getByLabelText("Reason"), { target: { value: "Recover ownership" } });
    fireEvent.submit(screen.getByLabelText("Incoming Manager").closest("form")!);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "A project manager assignment request is already pending for this project.",
    );
    expect(screen.getByRole("alert")).not.toHaveTextContent("Unable to save the recovery action. Refresh and try again.");
  });

  it("allows an authorized recovery administrator to start an assignment for an unassigned Project", () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate, "Handover.AdminInitiate"];
    state.items = [project()];

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));

    expect(screen.getByText("Assign initial Manager")).toBeInTheDocument();
    expect(screen.getByLabelText("Incoming Manager")).toBeInTheDocument();
    expect(screen.getByLabelText("Effective date")).toBeInTheDocument();
  });

  it("shows the selected Project context as read-only before manager selection", () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate, "Handover.AdminInitiate"];
    state.items = [project()];

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));

    const summaryPanel = screen.getByRole("heading", { name: "Project summary" }).closest("[aria-labelledby='recovery-project-summary-title']") as HTMLElement;
    expect(within(summaryPanel).getAllByText("Read only")).toHaveLength(2);
    expect(within(summaryPanel).getByText("Unassigned project")).toBeInTheDocument();
    expect(within(summaryPanel).getByText("REC-1")).toBeInTheDocument();
    expect(within(summaryPanel).getByText("Replace the regional water monitoring system.")).toBeInTheDocument();
    expect(within(summaryPanel).getByText("Nadia Hassan")).toBeInTheDocument();
    expect(screen.getByLabelText("Incoming Manager")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "Project name" })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Description / purpose")).not.toBeInTheDocument();
  });

  it("uses localized missing-value labels and keeps submission disabled until the matching summary loads", () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate, "Handover.AdminInitiate"];
    state.items = [project({ proposedManagerId: "33333333-3333-4333-8333-333333333333" })];
    state.projectSummaryLoading = true;
    state.projectSummarySuccess = false;

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));

    expect(screen.getByRole("status")).toHaveTextContent("Loading project summary...");
    expect(screen.getByLabelText("Incoming Manager")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Submit for acceptance" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Reason"), { target: { value: "No longer missing" } });
    fireEvent.submit(screen.getByLabelText("Incoming Manager").closest("form")!);
    expect(state.createInitial).not.toHaveBeenCalled();

    state.projectSummaryLoading = false;
    state.projectSummarySuccess = true;
    state.projectSummary = { ...state.projectSummary!, description: null, startDate: null, endDate: null, sponsorName: null };
    // Opening a fresh sheet causes the mock hook to expose the completed lookup.
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));

    expect(screen.getAllByText("Not provided")).toHaveLength(3);
    expect(screen.getByText("Not assigned")).toBeInTheDocument();
  });

  it("shows an empty summary state and blocks assignment without a project summary", () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate, "Handover.AdminInitiate"];
    state.items = [project({ proposedManagerId: "33333333-3333-4333-8333-333333333333" })];
    state.projectSummary = null;

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));

    expect(screen.getByRole("status")).toHaveTextContent("The selected project summary is unavailable.");
    expect(screen.getByLabelText("Incoming Manager")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Submit for acceptance" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Reason"), { target: { value: "Assign the project manager" } });
    fireEvent.submit(screen.getByLabelText("Incoming Manager").closest("form")!);
    expect(state.createInitial).not.toHaveBeenCalled();
  });

  it("shows a retryable summary error and does not use a previous Project summary after switching Projects", () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate, "Handover.AdminInitiate"];
    const first = project({ description: "First Project purpose." });
    const second = project({ publicId: "44444444-4444-4444-8444-444444444444", name: "Second project", code: "REC-2" });
    state.items = [first, second];
    state.projectSummaryError = true;
    state.projectSummarySuccess = false;
    state.projectSummary = null;

    renderPage();

    fireEvent.click(screen.getAllByRole("button", { name: "Assign manager" })[0]);
    expect(screen.getByRole("alert")).toHaveTextContent("Unable to load this project summary. Try again.");
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(state.retryProjectSummary).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    state.projectSummaryError = false;
    state.projectSummarySuccess = true;
    state.projectSummary = {
      publicId: first.publicId,
      name: "Stale first Project",
      code: "REC-1",
      description: "First Project purpose.",
      startDate: null,
      endDate: null,
      sponsorName: null,
    };
    fireEvent.click(screen.getAllByRole("button", { name: "Assign manager" })[1]);

    expect(screen.queryByText("First Project purpose.")).not.toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("Unable to load this project summary. Try again.");
    expect(screen.getByLabelText("Incoming Manager")).toBeDisabled();
  });

  it("does not offer initial assignment to a Manage-only user without Initiate", () => {
    state.permissions = [Permissions.AssignmentRecovery.Manage];
    state.items = [project()];

    renderPage();

    expect(screen.queryByRole("button", { name: "Assign manager" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Assign Sponsor" })).toBeInTheDocument();
  });

  it("does not expose mutation controls to a discovery-only user", () => {
    state.permissions = [Permissions.AssignmentRecovery.View];
    state.items = [project()];

    renderPage();

    expect(screen.queryByRole("button", { name: "Assign manager" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Assign Sponsor" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "History" })).toBeInTheDocument();
  });
});
