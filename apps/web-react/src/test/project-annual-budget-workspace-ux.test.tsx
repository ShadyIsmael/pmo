import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectAnnualBudgetWorkspace } from "../features/projects/ProjectAnnualBudgetWorkspace";

const mocks = vi.hoisted(() => ({
  annualBudgets: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  deleteDraft: vi.fn(),
  withdrawSubmission: vi.fn(),
  submitReview: vi.fn(),
  revise: vi.fn(),
  decideReview: vi.fn(),
  saveDraftApprovalConfiguration: vi.fn(),
  approvalConfiguration: vi.fn(),
  draftApprovalConfiguration: vi.fn(),
  closings: vi.fn(),
  paymentRequests: vi.fn(),
}));

vi.mock("../features/projects/api/project-annual-budgets", () => ({
  useProjectAnnualBudgets: mocks.annualBudgets,
  useCreateProjectAnnualBudget: mocks.create,
  useUpdateProjectAnnualBudget: mocks.update,
  useDeleteProjectAnnualBudgetDraft: mocks.deleteDraft,
  useWithdrawProjectAnnualBudgetSubmission: mocks.withdrawSubmission,
  useSubmitProjectAnnualBudgetReview: mocks.submitReview,
  useReviseApprovedProjectAnnualBudget: mocks.revise,
  useDecideProjectAnnualBudgetReview: mocks.decideReview,
  useSaveProjectAnnualBudgetDraftApprovalConfiguration: mocks.saveDraftApprovalConfiguration,
  useProjectAnnualBudgetApprovalConfiguration: mocks.approvalConfiguration,
  useProjectAnnualBudgetDraftApprovalConfiguration: mocks.draftApprovalConfiguration,
}));

vi.mock("../features/projects/api/project-financials", () => ({
  useProjectAnnualBudgetClosings: mocks.closings,
  useProjectPaymentRequests: mocks.paymentRequests,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1" } }) }));

const page = (overrides: Record<string, unknown> = {}) => ({
  currentFiscalYear: 2026,
  fiscalYearStartMonth: 1,
  items: [],
  totalProjectFinancialCeiling: 120_000,
  projectCurrency: "USD",
  totalApprovedNewAllocations: 0,
  pendingSubmittedAllocations: 0,
  remainingUnallocatedCeiling: 120_000,
  suggestedFiscalYear: 2026,
  suggestedNewFunding: 120_000,
  suggestedNewFundingSource: "ProjectLifetimeCeiling",
  missingClosingFiscalYear: null,
  ...overrides,
});

const approvedBudget = (overrides: Record<string, unknown> = {}) => ({
  id: "budget-2026",
  projectId: "project-1",
  fiscalYear: 2026,
  fiscalYearLabel: "FY 2026",
  fiscalYearStartDate: "2026-01-01",
  fiscalYearEndDate: "2026-12-31",
  financialReference: "PRJ-001-FY2026",
  currency: "USD",
  newApprovedFunding: 20_000,
  approvedCarryForward: 7_500,
  approvedReservedCarryForward: 2_500,
  approvedFreeCarryForward: 5_000,
  approvedAdjustments: 500,
  totalAnnualBudget: 28_000,
  status: "Approved",
  approvedBy: "approver-1",
  approvedAtUtc: "2026-01-01T00:00:00Z",
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: null,
  rowVersion: "AQID",
  closedByClosingId: null,
  closedAtUtc: null,
  closedBy: null,
  isClosed: false,
  canEdit: false,
  canDeleteDraft: false,
  deleteDraftBlockReason: "not-draft",
  canWithdrawSubmission: false,
  canApprove: false,
  reviewStatus: "Approved",
  approvalMode: "Sequential",
  reviewRequestId: null,
  reviewRowVersion: null,
  submittedBy: "submitter-1",
  submittedAtUtc: "2026-01-01T00:00:00Z",
  canSubmit: false,
  canReview: false,
  canRevise: false,
  reviewHistory: [],
  ...overrides,
});

function query(data: unknown, overrides: Record<string, unknown> = {}) {
  return { data, isLoading: false, isFetching: false, isError: false, error: null, refetch: vi.fn(), ...overrides };
}

function renderWorkspace(options: {
  pageData?: Record<string, unknown>;
  fiscalYear?: number;
  currency?: string | null;
  canCreate?: boolean;
  onFiscalYearChange?: (year: number) => void;
} = {}) {
  if (options.pageData) mocks.annualBudgets.mockReturnValue(query(page(options.pageData)));
  return render(<I18nProvider><ProjectAnnualBudgetWorkspace
    projectId="project-1"
    canCreate={options.canCreate ?? false}
    canEdit={false}
    canConfigureDraftApproval={false}
    canDelete={false}
    canWithdraw={false}
    canSubmit={false}
    canRevise={false}
    canApprove={false}
    canViewPaymentRequests={false}
    canViewAnnualBudgetClosing={false}
    canPrepareClosing={false}
    projectCurrency={options.currency === undefined ? "USD" : options.currency}
    enabled
    selectedFiscalYear={options.fiscalYear}
    onFiscalYearChange={options.onFiscalYearChange}
  /></I18nProvider>);
}

describe("Project Annual Budget workspace UX cleanup", () => {
  beforeEach(() => {
    mocks.annualBudgets.mockReturnValue(query(page()));
    mocks.closings.mockReturnValue(query({ currentFiscalYear: 2026, items: [] }));
    mocks.paymentRequests.mockReturnValue(query(null));
    mocks.create.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.update.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.deleteDraft.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.withdrawSubmission.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.submitReview.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.revise.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.decideReview.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.saveDraftApprovalConfiguration.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.approvalConfiguration.mockReturnValue(query(null));
    mocks.draftApprovalConfiguration.mockReturnValue(query(null));
  });

  it("does not render the redundant Annual Budget History table", () => {
    renderWorkspace({ canCreate: false, pageData: { items: [approvedBudget()] } });
    expect(screen.queryByText(/Annual budget history/i)).not.toBeInTheDocument();
  });

  it("keeps Annual Budgets accessible per fiscal year via the selector and detail metrics", () => {
    renderWorkspace({ canCreate: false, pageData: { items: [approvedBudget()] } });
    expect(screen.getByRole("combobox", { name: /Fiscal Year/i })).toBeInTheDocument();
    expect(within(screen.getByText("Total annual budget", { selector: "p" }).parentElement!).getByText("$28,000")).toBeInTheDocument();
  });

  it("keeps the Annual Budget approval lifecycle (review history) accessible", () => {
    mocks.annualBudgets.mockReturnValue(query(page({
      items: [approvedBudget({
        status: "Approved",
        reviewStatus: "Approved",
        reviewHistory: [
          { event: "Submitted", actorName: "submitter-1", timestampUtc: "2026-01-01T00:00:00Z", reason: "" },
          { event: "Approved", actorName: "approver-1", timestampUtc: "2026-01-02T00:00:00Z", reason: "" },
        ],
      })],
    })));
    renderWorkspace({ canCreate: false });
    expect(screen.getByText(/Review history/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Submitted for review/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Approved/i).length).toBeGreaterThanOrEqual(1);
  });
});
