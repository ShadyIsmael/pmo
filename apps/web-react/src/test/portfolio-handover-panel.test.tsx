import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ManagerHandoverPanel } from "../features/delivery/ManagerHandoverPanel";

const fixtures = vi.hoisted(() => ({
  userId: "user-current",
  requests: [] as unknown[],
  requestsLoading: false,
  requestsError: false as boolean | { status: number },
  selfPending: [] as unknown[],
  selfPendingLoading: false,
  selfPendingError: false as boolean,
  accept: vi.fn(),
  reject: vi.fn(),
  cancel: vi.fn(),
  create: vi.fn(),
  refreshSnapshot: vi.fn(),
  capabilities: {} as Record<string, unknown>,
}));

vi.mock("@features/delivery/api/managerHandovers", () => ({
  useManagerHandoverCapabilities: () => fixtures.capabilities,
  useManagerHandoverRequests: () => ({
    data: fixtures.requests,
    isLoading: fixtures.requestsLoading,
    isError: Boolean(fixtures.requestsError),
    error: typeof fixtures.requestsError === "object" ? fixtures.requestsError : null,
    refetch: vi.fn(),
  }),
  useSelfPendingManagerHandover: () => ({
    data: fixtures.selfPending,
    isLoading: fixtures.selfPendingLoading,
    isError: fixtures.selfPendingError,
    error: null,
    refetch: vi.fn(),
  }),
  useManagerAssignmentHistory: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
  useManagerHandoverSnapshots: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
  useEligibleManagerHandoverManagers: () => ({ data: [], isLoading: false, isError: false }),
  useCreateManagerHandover: () => ({ mutateAsync: fixtures.create, isPending: false }),
  useCancelManagerHandover: () => ({ mutateAsync: fixtures.cancel, isPending: false }),
  useAcceptManagerHandover: () => ({ mutateAsync: fixtures.accept, isPending: false }),
  useRejectManagerHandover: () => ({ mutateAsync: fixtures.reject, isPending: false }),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: fixtures.userId, permissions: [] as string[] } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ refreshSnapshot: fixtures.refreshSnapshot }),
}));

function baseRequest(overrides: Record<string, unknown> = {}) {
  return {
    id: "request-1",
    entityType: "Portfolio",
    entityId: "portfolio-1",
    entityName: "Delivery Transformation",
    currentManagerId: "user-current",
    currentManagerName: "Current Manager",
    newManagerId: "user-incoming",
    newManagerName: "Incoming Manager",
    effectiveHandoverDate: "2026-12-01T00:00:00Z",
    reason: "Rebalance ownership",
    status: "Pending",
    requestedBy: "user-current",
    requestedByName: "Current Manager",
    requestedAt: "2026-10-01T10:00:00Z",
    ...overrides,
  };
}

function renderPanel(props: Partial<Parameters<typeof ManagerHandoverPanel>[0]> = {}) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ManagerHandoverPanel
          scope="portfolios"
          entityId="portfolio-1"
          entityName="Delivery Transformation"
          currentManagerId="user-current"
          currentManagerName="Current Manager"
          cacheScope="test"
          canView
          canManage
          canApprove={false}
          canMutate
          {...props}
        />
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("portfolio manager handover panel", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.userId = "user-current";
    fixtures.requests = [];
    fixtures.requestsLoading = false;
    fixtures.requestsError = false;
    fixtures.selfPending = [];
    fixtures.selfPendingLoading = false;
    fixtures.selfPendingError = false;
    fixtures.capabilities = {
      data: {
        currentManagerId: "user-current",
        currentManagerName: "Current Manager",
        canViewRequests: true,
        canInitiate: true,
        canAdminInitiate: false,
        canAssignInitial: false,
        canApproveIncoming: false,
        canCancelOwnPending: true,
        hasPendingRequest: false,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    };
    fixtures.accept.mockReset().mockResolvedValue({ id: "request-1" });
    fixtures.reject.mockReset().mockResolvedValue({ id: "request-1" });
    fixtures.cancel.mockReset().mockResolvedValue({ id: "request-1" });
    fixtures.create.mockReset().mockResolvedValue({ id: "request-1" });
    fixtures.refreshSnapshot.mockReset().mockResolvedValue(undefined);
  });

  it("shows the initiate action for the current manager with no pending request", () => {
    renderPanel();
    expect(screen.getByRole("button", { name: "Request handover" })).toBeInTheDocument();
    expect(screen.getByText("No manager handover requests have been submitted.")).toBeInTheDocument();
  });

  it("explains why an admin who is not the manager sees no initiate action", () => {
    fixtures.userId = "user-admin";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canInitiate: false,
        canAdminInitiate: false,
      },
    };
    renderPanel({ currentManagerId: "user-current", currentManagerName: "Current Manager" });
    expect(screen.queryByRole("button", { name: "Request handover" })).not.toBeInTheDocument();
    expect(screen.getByText("Only the current manager (Current Manager) can initiate a handover.")).toBeInTheDocument();
  });

  it("shows accept and reject to the incoming manager on a pending request", async () => {
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.requests = [baseRequest()];
    renderPanel({ canManage: false, canApprove: true });
    expect(await screen.findByRole("button", { name: "Accept" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reject" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel request" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Accept" }));
    await waitFor(() => expect(fixtures.accept).toHaveBeenCalledWith("request-1"));
  });

  it("requires a rejection reason before rejecting", async () => {
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.requests = [baseRequest()];
    renderPanel({ canManage: false, canApprove: true });
    fireEvent.click(await screen.findByRole("button", { name: "Reject" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "Confirm rejection" })).toBeDisabled();
    expect(fixtures.reject).not.toHaveBeenCalled();
    fireEvent.change(within(dialog).getByLabelText("Rejection reason *"), { target: { value: "Capacity constraints" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Confirm rejection" }));
    await waitFor(() => expect(fixtures.reject).toHaveBeenCalledWith({ requestId: "request-1", rejectionReason: "Capacity constraints" }));
  });

  it("shows cancel confirmation to the requester", async () => {
    fixtures.requests = [baseRequest()];
    renderPanel({ canApprove: false });
    fireEvent.click(await screen.findByRole("button", { name: "Cancel request" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("The pending request will be withdrawn. This cannot be undone.")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Confirm cancellation" }));
    await waitFor(() => expect(fixtures.cancel).toHaveBeenCalledWith("request-1"));
  });

  it("hides every mutation and explains read-only mode to ReadOnlyViewAs", async () => {
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.requests = [baseRequest()];
    renderPanel({ canManage: true, canApprove: true, canMutate: false });
    expect(await screen.findByText("Handover actions are disabled while viewing in read-only mode.")).toBeInTheDocument();
    expect(screen.queryByText("A pending request exists, but you have no actions on it.")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel request" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Request handover" })).not.toBeInTheDocument();
  });

  it("shows an unrelated user that they have no actions on a pending request", async () => {
    fixtures.userId = "user-unrelated";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: false,
        hasPendingRequest: true,
      },
    };
    fixtures.requests = [baseRequest()];
    renderPanel({ canManage: true, canApprove: true });
    expect(await screen.findByText("A pending request exists, but you have no actions on it.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancel request" })).not.toBeInTheDocument();
  });

  it("distinguishes a failed capability load from an empty queue", async () => {
    fixtures.requests = [];
    fixtures.requestsError = true;
    renderPanel();
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Unable to load handover details. Try again.");
    expect(within(alert).getByRole("button", { name: "Retry" })).toBeInTheDocument();
    expect(screen.queryByText("No manager handover requests have been submitted.")).not.toBeInTheDocument();
  });

  it("renders accept and reject in Arabic with RTL-safe layout", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.requests = [baseRequest()];
    renderPanel({ canManage: false, canApprove: true });
    expect(await screen.findByRole("button", { name: "قبول" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "رفض" })).toBeInTheDocument();
  });

  it("shows an approve-only incoming manager their pending request with accept and reject", async () => {
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canViewRequests: false,
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.selfPending = [baseRequest()];
    renderPanel({ canView: false, canManage: false, canApprove: true });
    expect(await screen.findByText("Pending approval for you")).toBeInTheDocument();
    expect(screen.getByText("Rebalance ownership")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Accept" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reject" })).toBeInTheDocument();
    expect(screen.getByText("Full handover history requires Handover.View permission.")).toBeInTheDocument();
    expect(screen.queryByText("Manager history")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Request handover" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Accept" }));
    await waitFor(() => expect(fixtures.accept).toHaveBeenCalledWith("request-1"));
  });

  it("tells an approve-only user with nothing pending that nothing is addressed to them", async () => {
    fixtures.userId = "user-unrelated";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canViewRequests: false,
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: false,
      },
    };
    renderPanel({ canView: false, canManage: false, canApprove: true });
    expect(await screen.findByText("No pending handover is addressed to you.")).toBeInTheDocument();
    expect(screen.getByText("Full handover history requires Handover.View permission.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
  });

  it("shows approve-only readonly viewers the details without mutation buttons", async () => {
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canViewRequests: false,
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.selfPending = [baseRequest()];
    renderPanel({ canView: false, canManage: false, canApprove: true, canMutate: false });
    expect(await screen.findByText("Pending approval for you")).toBeInTheDocument();
    expect(screen.getByText("Handover actions are disabled while viewing in read-only mode.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
  });

  it("shows an error with retry when the self-scoped request fails to load", async () => {
    fixtures.userId = "user-incoming";
    fixtures.selfPendingError = true;
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canViewRequests: false,
        canInitiate: false,
        canApproveIncoming: true,
      },
    };
    renderPanel({ canView: false, canManage: false, canApprove: true });
    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Unable to load handover details. Try again.");
    expect(within(alert).getByRole("button", { name: "Retry" })).toBeInTheDocument();
    expect(screen.queryByText("No pending handover is addressed to you.")).not.toBeInTheDocument();
  });

  it("falls back to the self-scoped request when the full list is forbidden", async () => {
    fixtures.userId = "user-incoming";
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canViewRequests: false,
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    fixtures.requestsError = { status: 403 };
    fixtures.selfPending = [baseRequest()];
    renderPanel({ canManage: false, canApprove: true });
    expect(await screen.findByText("Pending approval for you")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Accept" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reject" })).toBeInTheDocument();
  });

  it("renders the approve-only pending card in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.userId = "user-incoming";
    fixtures.selfPending = [baseRequest()];
    fixtures.capabilities = {
      ...fixtures.capabilities,
      data: {
        ...((fixtures.capabilities.data as Record<string, unknown>) ?? {}),
        canViewRequests: false,
        canInitiate: false,
        canCancelOwnPending: false,
        canApproveIncoming: true,
        hasPendingRequest: true,
      },
    };
    renderPanel({ canView: false, canManage: false, canApprove: true });
    expect(await screen.findByText("موافقة معلقة بانتظارك")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "قبول" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "رفض" })).toBeInTheDocument();
    expect(screen.getByText("يتطلب عرض سجل التسليم الكامل صلاحية عرض التسليمات.")).toBeInTheDocument();
  });
});
