import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { EntityAssignmentRecoveryPage } from "@features/administration/EntityAssignmentRecoveryPage";
import { Permissions } from "@lib/permissions";

const state = vi.hoisted(() => ({
  permissions: ["Governance.AssignmentRecovery.Manage"],
  items: [] as Array<Record<string, unknown>>,
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
  useRecoveryManagerCandidates: () => ({ data: [], isLoading: false, isError: false }),
  useRecoverySponsorCandidates: () => ({ data: [], isLoading: false, isError: false }),
  useAssignmentRecoveryHistory: () => ({ data: { items: [] }, isLoading: false, isError: false }),
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
      proposedManagerId: "33333333-3333-4333-8333-333333333333",
      proposedManagerName: "Incoming Manager",
    })];

    renderPage();

    expect(screen.getByText("Unassigned project")).toBeInTheDocument();
    expect(screen.getByText("Pending acceptance")).toBeInTheDocument();
    expect(screen.getByText("Proposed: Incoming Manager")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Assign manager" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open My Approvals" })).toHaveAttribute("href", "/my-work/approvals");
  });

  it("allows an authorized recovery administrator to start an assignment for an unassigned Project", () => {
    state.permissions = [Permissions.AssignmentRecovery.Initiate];
    state.items = [project()];

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Assign manager" }));

    expect(screen.getByText("Assign initial Manager")).toBeInTheDocument();
    expect(screen.getByLabelText("Incoming Manager")).toBeInTheDocument();
    expect(screen.getByLabelText("Effective date")).toBeInTheDocument();
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
