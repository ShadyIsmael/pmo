import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@api/client";
import { I18nProvider } from "../i18n/messages";
import { ProjectAnnualBudgetWorkspace } from "../features/projects/ProjectAnnualBudgetWorkspace";

const mocks = vi.hoisted(() => ({
  annualBudgets: vi.fn(),
  closings: vi.fn(),
  paymentRequests: vi.fn(),
  approvalConfiguration: vi.fn(),
  draftApprovalConfiguration: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  deleteDraft: vi.fn(),
  withdrawSubmission: vi.fn(),
  submitReview: vi.fn(),
  revise: vi.fn(),
  decideReview: vi.fn(),
  saveDraftApprovalConfiguration: vi.fn(),
  onOpenClosing: vi.fn(),
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
  useProjectAnnualBudgetApprovalConfiguration: mocks.approvalConfiguration,
  useProjectAnnualBudgetDraftApprovalConfiguration: mocks.draftApprovalConfiguration,
  useSaveProjectAnnualBudgetDraftApprovalConfiguration: mocks.saveDraftApprovalConfiguration,
}));

vi.mock("../features/projects/api/project-financials", () => ({
  useProjectAnnualBudgetClosings: mocks.closings,
  useProjectPaymentRequests: mocks.paymentRequests,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "submitter-1" } }) }));

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

const draftApprovalConfig = (overrides: Record<string, unknown> = {}) => ({
  projectId: "project-1",
  projectName: "Delivery Project",
  configurationSource: "ProjectSettings",
  projectApprovalMode: "Sequential",
  approvalMode: "Sequential",
  allowedModes: ["Sequential", "Parallel"],
  configuredApprovers: [{ userId: "reviewer-one", displayName: "Team Reviewer One", position: 1, isEligible: true, issueCode: null }],
  eligibleApprovers: [
    { userId: "submitter-1", displayName: "Tenant Admin Sponsor", eligibleRoleCategories: ["System"], selectionBlockReason: null },
    { userId: "reviewer-one", displayName: "Team Reviewer One", eligibleRoleCategories: ["Management"], selectionBlockReason: null },
    { userId: "reviewer-two", displayName: "Team Reviewer Two", eligibleRoleCategories: ["System", "Management"], selectionBlockReason: null },
  ],
  budgetRowVersion: "AQID",
  projectSettingsRowVersion: "BAUG",
  projectSettingsConfigurationRowVersion: "CAgI",
  overrideReason: null,
  updatedAtUtc: null,
  isValid: true,
  validationIssues: [],
  canSave: true,
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
  canSubmit?: boolean;
  canDelete?: boolean;
  canEdit?: boolean;
  canConfigureDraftApproval?: boolean;
  canWithdraw?: boolean;
  canViewClosing?: boolean;
  canPrepareClosing?: boolean;
  onOpenClosing?: () => void;
  onFiscalYearChange?: (year: number) => void;
  locale?: "en" | "ar";
} = {}) {
  if (options.pageData) mocks.annualBudgets.mockReturnValue(query(page(options.pageData)));
  window.localStorage.setItem("nexus-locale", options.locale ?? "en");
  return render(<I18nProvider><ProjectAnnualBudgetWorkspace
    projectId="project-1"
    canCreate={options.canCreate ?? false}
    canEdit={options.canEdit ?? false}
    canConfigureDraftApproval={options.canConfigureDraftApproval ?? false}
    canDelete={options.canDelete ?? false}
    canWithdraw={options.canWithdraw ?? false}
    canSubmit={options.canSubmit ?? false}
    canRevise={false}
    canApprove={false}
    canViewPaymentRequests={false}
    canViewAnnualBudgetClosing={options.canViewClosing ?? false}
    canPrepareClosing={options.canPrepareClosing ?? false}
    onOpenClosing={options.onOpenClosing ?? mocks.onOpenClosing}
    projectCurrency={options.currency === undefined ? "USD" : options.currency}
    enabled
    selectedFiscalYear={options.fiscalYear ?? 2026}
    onFiscalYearChange={options.onFiscalYearChange}
  /></I18nProvider>);
}

describe("Project Annual Budget workflow recovery", () => {
  beforeEach(() => {
    mocks.annualBudgets.mockReset().mockReturnValue(query(page()));
    mocks.closings.mockReset().mockReturnValue(query({ currentFiscalYear: 2026, items: [] }));
    mocks.paymentRequests.mockReset().mockReturnValue(query(null));
    mocks.approvalConfiguration.mockReset().mockReturnValue(query(null));
    mocks.draftApprovalConfiguration.mockReset().mockReturnValue(query(null));
    mocks.create.mockReset().mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.update.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.deleteDraft.mockReset().mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.withdrawSubmission.mockReset().mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.submitReview.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.revise.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.decideReview.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.saveDraftApprovalConfiguration.mockReset().mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.onOpenClosing.mockReset();
  });

  it("prefills the first-year proposal from the lifetime ceiling, keeps it editable, and saves only a Draft", async () => {
    const createDraft = vi.fn().mockResolvedValue(undefined);
    mocks.create.mockReturnValue({ mutateAsync: createDraft, isPending: false });
    renderWorkspace({ canCreate: true, currency: null });

    fireEvent.click(screen.getAllByRole("button", { name: "Create draft" })[0]);
    const funding = screen.getByLabelText("New Funding *") as HTMLInputElement;
    expect(funding).toHaveValue(120000);
    expect(funding).not.toBeDisabled();
    expect(screen.getByText("Suggested first-year allocation")).toBeInTheDocument();
    expect(screen.getByText(/does not approve it or change the lifetime ceiling/)).toBeInTheDocument();

    fireEvent.change(funding, { target: { value: "95000" } });
    fireEvent.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => expect(createDraft).toHaveBeenCalledWith({
      fiscalYear: 2026,
      newApprovedFunding: 95_000,
      approvedAdjustments: 0,
    }));
  });

  it("requires the prior approved closing before opening the next year and routes authorized users to Closing", () => {
    mocks.annualBudgets.mockReturnValue(query(page({
      items: [{ fiscalYear: 2025, fiscalYearLabel: "FY 2025", totalAnnualBudget: 100_000, currency: "USD", status: "Approved", isClosed: false }],
      suggestedFiscalYear: 2026,
      missingClosingFiscalYear: 2025,
    })));
    const onFiscalYearChange = vi.fn();
    renderWorkspace({ canCreate: true, canViewClosing: true, canPrepareClosing: true, onFiscalYearChange });

    expect(screen.getByText("Approved closing for fiscal year 2025 is required before opening the next fiscal year.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Open Fiscal Year Closing" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create draft" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Open Fiscal Year Closing" }));
    expect(onFiscalYearChange).toHaveBeenCalledWith(2025);
    expect(mocks.onOpenClosing).toHaveBeenCalledTimes(1);
  });

  it("keeps the missing-close message but withholds the Closing link without Closing View", () => {
    mocks.annualBudgets.mockReturnValue(query(page({
      items: [{ fiscalYear: 2025, fiscalYearLabel: "FY 2025", totalAnnualBudget: 100_000, currency: "USD", status: "Approved", isClosed: false }],
      suggestedFiscalYear: 2026,
      missingClosingFiscalYear: 2025,
    })));
    renderWorkspace({ canCreate: true, canPrepareClosing: true, canViewClosing: false });

    expect(screen.getByRole("status")).toHaveTextContent("Approved closing for fiscal year 2025 is required");
    expect(screen.queryByRole("button", { name: "Open Fiscal Year Closing" })).not.toBeInTheDocument();
    expect(mocks.closings).toHaveBeenCalledWith("project-1", false);
  });

  it("renders later-year funding, carry forward, and adjustments exactly as the approved server budget returns them", () => {
    mocks.annualBudgets.mockReturnValue(query(page({
      items: [approvedBudget()],
      suggestedFiscalYear: 2027,
      suggestedNewFunding: null,
      suggestedNewFundingSource: null,
      missingClosingFiscalYear: null,
    })));
    mocks.closings.mockReturnValue(query({ currentFiscalYear: 2026, items: [{
      id: "closing-2025",
      fiscalYear: 2025,
      status: "Closed",
      destinationFiscalYear: 2026,
      approvedFreeCarryForward: 5_000,
      approvedReservedCarryForward: 2_500,
      nextFiscalYearNewFunding: 20_000,
      nextFiscalYearAdjustments: 500,
    }] }));

    renderWorkspace({ canCreate: false, fiscalYear: 2026 });

    expect(within(screen.getByText("New Funding").parentElement!).getByText("$20,000")).toBeInTheDocument();
    expect(within(screen.getByText("Approved carry forward").parentElement!).getByText("$7,500")).toBeInTheDocument();
    expect(within(screen.getByText("Approved adjustments").parentElement!).getByText("$500")).toBeInTheDocument();
    expect(within(screen.getByText("Total annual budget", { selector: "p" }).parentElement!).getByText("$28,000")).toBeInTheDocument();
  });

  it("keeps creation unavailable when authoritative Project currency is missing", () => {
    renderWorkspace({ canCreate: true, currency: null, pageData: { projectCurrency: "" } });

    expect(screen.getByRole("status")).toHaveTextContent("A valid project currency is unavailable");
    expect(screen.getAllByRole("button", { name: "Create draft" })[0]).toBeDisabled();
  });

  it("requires a lifetime ceiling to create a Draft while preserving a zero ceiling as configured", () => {
    const noCeilingView = renderWorkspace({ canCreate: true, pageData: { totalProjectFinancialCeiling: null } });

    expect(screen.getByRole("status")).toHaveTextContent("A Project lifetime ceiling is required before creating or submitting an Annual Budget for review.");
    expect(screen.getAllByRole("button", { name: "Create draft" })[0]).toBeDisabled();
    noCeilingView.unmount();

    renderWorkspace({ canCreate: true, pageData: {
      totalProjectFinancialCeiling: 0,
      remainingUnallocatedCeiling: 0,
      suggestedNewFunding: 0,
    } });
    const createButton = screen.getAllByRole("button", { name: "Create draft" })[0];
    expect(createButton).toBeEnabled();
    fireEvent.click(createButton);
    expect(screen.getByLabelText("New Funding *")).toHaveValue(0);
  });

  it("shows loading and retryable error states without inventing annual-budget data", () => {
    mocks.annualBudgets.mockReturnValue(query(undefined, { isLoading: true }));
    const view = renderWorkspace();
    expect(screen.getByRole("status")).toBeInTheDocument();

    mocks.annualBudgets.mockReturnValue(query(undefined, {
      isLoading: false,
      isError: true,
      error: new ApiError(403, "Forbidden"),
    }));
    view.rerender(<I18nProvider><ProjectAnnualBudgetWorkspace
      projectId="project-1" canCreate={false} canEdit={false} canSubmit={false} canRevise={false} canApprove={false}
      canViewPaymentRequests={false} canViewAnnualBudgetClosing={false} projectCurrency="USD" enabled selectedFiscalYear={2026}
    /></I18nProvider>);
    expect(screen.getByRole("alert")).toHaveTextContent("You do not have permission to view this financial information.");
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  it("shows the current-user candidate while preventing self-approval and preserves reviewer ordering", async () => {
    const submitReview = vi.fn().mockResolvedValue(undefined);
    mocks.submitReview.mockReturnValue({ mutateAsync: submitReview, isPending: false });
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Draft",
      canSubmit: true,
      canEdit: false,
      submittedBy: null,
      submittedAtUtc: null,
    })] })));
    mocks.approvalConfiguration.mockReturnValue(query({
      projectId: "project-1",
      projectName: "Delivery Project",
      configurationSource: "ProjectSettings",
      financeApprovalMode: "Sequential",
      approvalMode: "Sequential",
      allowedModes: ["Sequential", "Parallel"],
      configuredApprovers: [{ userId: "reviewer-one", displayName: "Team Reviewer One", position: 1, isEligible: true, issueCode: null }],
      eligibleApprovers: [
        { userId: "submitter-1", displayName: "Tenant Admin Sponsor", eligibleRoleCategories: ["System"] },
        { userId: "reviewer-one", displayName: "Team Reviewer One", eligibleRoleCategories: ["Management"] },
        { userId: "reviewer-two", displayName: "Team Reviewer Two", eligibleRoleCategories: ["System", "Management"] },
      ],
      rowVersion: "AQID",
      projectSettingsRowVersion: "BAUG",
      isConfigured: true,
      isValid: true,
      canSubmitWithDefaults: true,
      validationIssues: [],
      canOverride: true,
    }));
    renderWorkspace({ canSubmit: true });

    fireEvent.click(screen.getByRole("button", { name: "Submit for review" }));
    fireEvent.click(screen.getByRole("button", { name: "Customize for this submission" }));

    const sponsor = screen.getByRole("checkbox", { name: /Tenant Admin Sponsor/ });
    expect(sponsor).toBeVisible();
    expect(sponsor).toBeDisabled();
    expect(screen.getByText("(System)")).toBeInTheDocument();
    expect(screen.getByText("(System, Management)")).toBeInTheDocument();
    expect(screen.getByText("You cannot approve your own submission.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("checkbox", { name: /Team Reviewer Two/ }));
    fireEvent.click(screen.getByRole("button", { name: "Move Team Reviewer Two earlier in approval order" }));
    const preview = screen.getByRole("region", { name: "Approval Preview" });
    expect(within(preview).getAllByRole("listitem").map(item => item.textContent)).toEqual([
      "Team Reviewer Two",
      "Team Reviewer One",
    ]);

    fireEvent.change(screen.getByLabelText("Reason for this one-time change"), { target: { value: "Use the current eligible project team" } });
    const submitButtons = screen.getAllByRole("button", { name: "Submit for review" });
    fireEvent.click(submitButtons[submitButtons.length - 1]);

    await waitFor(() => expect(submitReview).toHaveBeenCalledWith(expect.objectContaining({
      approvalMode: "Sequential",
      approvers: [
        { userId: "reviewer-two", position: 1, requirement: "Mandatory" },
        { userId: "reviewer-one", position: 2, requirement: "Mandatory" },
      ],
      customizeForThisSubmission: true,
      overrideReason: "Use the current eligible project team",
    })));
  });

  it("saves and reopens a draft-specific approval plan without changing Project Settings", async () => {
    let persisted = draftApprovalConfig();
    const refetch = vi.fn(async () => ({ data: persisted }));
    mocks.draftApprovalConfiguration.mockImplementation(() => query(persisted, { refetch }));
    const save = vi.fn(async (input: Record<string, unknown>) => {
      const approvers = input.approverUserIds as string[];
      persisted = draftApprovalConfig({
        configurationSource: "DraftOverride",
        approvalMode: input.approvalMode,
        configuredApprovers: approvers.map((userId, index) => ({
          userId,
          displayName: userId === "reviewer-two" ? "Team Reviewer Two" : "Team Reviewer One",
          position: index + 1,
          isEligible: true,
          issueCode: null,
        })),
        budgetRowVersion: "saved-version",
        overrideReason: input.overrideReason,
      });
      return persisted;
    });
    mocks.saveDraftApprovalConfiguration.mockReturnValue({ mutateAsync: save, isPending: false });
    const submitSavedPlan = vi.fn().mockResolvedValue(undefined);
    mocks.submitReview.mockReturnValue({ mutateAsync: submitSavedPlan, isPending: false });
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Draft",
      canEdit: true,
      canSubmit: true,
      canDeleteDraft: true,
      deleteDraftBlockReason: null,
      submittedBy: null,
      submittedAtUtc: null,
    })] })));

    renderWorkspace({ canEdit: true, canConfigureDraftApproval: true, canSubmit: true });
    fireEvent.click(screen.getByRole("button", { name: "Configure Approval" }));
    expect(await screen.findByText("Current approval plan")).toBeInTheDocument();
    expect(screen.getByText(/Configuration source: Project Settings/)).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: /Tenant Admin Sponsor/ })).toBeEnabled();

    fireEvent.click(screen.getByRole("checkbox", { name: /Team Reviewer Two/ }));
    fireEvent.click(screen.getByRole("button", { name: "Move Team Reviewer Two earlier in approval order" }));
    fireEvent.change(screen.getByLabelText("Reason for this draft-specific configuration"), { target: { value: "Align the project team review order" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(save).toHaveBeenCalledWith({
      approvalMode: "Sequential",
      approverUserIds: ["reviewer-two", "reviewer-one"],
      budgetRowVersion: "AQID",
      projectSettingsRowVersion: "BAUG",
      projectSettingsConfigurationRowVersion: "CAgI",
      overrideReason: "Align the project team review order",
    }));
    expect(persisted.configurationSource).toBe("DraftOverride");
    expect(persisted.budgetRowVersion).toBe("saved-version");
    expect(screen.queryByRole("heading", { name: "Configure Approval" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Configure Approval" }));
    expect(await screen.findByText(/This Annual Budget draft/)).toBeInTheDocument();
    const currentPlan = screen.getByText("Current approval plan").closest("section");
    expect(currentPlan).not.toBeNull();
    expect(within(currentPlan!).getAllByRole("listitem").map(item => item.textContent)).toEqual([
      "Team Reviewer Two",
      "Team Reviewer One",
    ]);
    expect(refetch).toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    fireEvent.click(screen.getByRole("button", { name: "Submit for review" }));
    expect(await screen.findByText(/This Annual Budget draft/)).toBeInTheDocument();
    const submitButtons = screen.getAllByRole("button", { name: "Submit for review" });
    fireEvent.click(submitButtons[submitButtons.length - 1]);
    await waitFor(() => expect(submitSavedPlan).toHaveBeenCalledWith({ id: "budget-2026", rowVersion: "AQID" }));
  });

  it("allows an eligible rejected Draft to delete and configure approval", async () => {
    mocks.draftApprovalConfiguration.mockReturnValue(query(draftApprovalConfig()));
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Rejected",
      canEdit: true,
      canSubmit: true,
      canDeleteDraft: true,
      deleteDraftBlockReason: null,
      submittedBy: null,
      submittedAtUtc: null,
    })] })));

    renderWorkspace({ canDelete: true, canEdit: true, canConfigureDraftApproval: true });

    expect(screen.getByRole("button", { name: "Delete Draft" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Configure Approval" }));
    expect(await screen.findByText("Current approval plan")).toBeInTheDocument();
  });

  it("deletes only when authorized, confirms the fiscal year, and refreshes the annual-budget view", async () => {
    const deleteDraft = vi.fn().mockResolvedValue(undefined);
    const refetch = vi.fn().mockResolvedValue({ data: page({ items: [] }) });
    const onFiscalYearChange = vi.fn();
    mocks.deleteDraft.mockReturnValue({ mutateAsync: deleteDraft, isPending: false });
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Draft",
      canDeleteDraft: true,
      deleteDraftBlockReason: null,
      submittedBy: null,
      submittedAtUtc: null,
    })] }), { refetch }));

    renderWorkspace({ canDelete: true, onFiscalYearChange });

    fireEvent.click(screen.getByRole("button", { name: "Delete Draft" }));
    expect(screen.getByRole("alertdialog", { name: "Delete FY 2026 draft?" })).toHaveTextContent("does not change the Project lifetime ceiling");
    expect(deleteDraft).not.toHaveBeenCalled();
    fireEvent.click(screen.getAllByRole("button", { name: "Delete Draft" })[1]);

    await waitFor(() => expect(deleteDraft).toHaveBeenCalledWith({ id: "budget-2026", rowVersion: "AQID" }));
    expect(refetch).toHaveBeenCalledOnce();
    expect(onFiscalYearChange).toHaveBeenCalledWith(2026);
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("withdraws only a server-authorized pending submission and keeps the approval version", async () => {
    const withdrawSubmission = vi.fn().mockResolvedValue(undefined);
    mocks.withdrawSubmission.mockReturnValue({ mutateAsync: withdrawSubmission, isPending: false });
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Pending",
      canDeleteDraft: false,
      deleteDraftBlockReason: "pending-review",
      canWithdrawSubmission: true,
      reviewRequestId: "approval-1",
      reviewRowVersion: "BAUG",
    })] })));

    renderWorkspace({ canWithdraw: true });

    expect(screen.getByRole("button", { name: "Delete Draft" })).toBeDisabled();
    expect(screen.getByText("Withdraw the pending submission before deleting this draft.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Withdraw Submission" }));
    expect(screen.getByRole("alertdialog", { name: "Withdraw FY 2026 submission?" })).toHaveTextContent("Previous review and audit history will be retained");
    fireEvent.click(screen.getAllByRole("button", { name: "Withdraw Submission" })[1]);

    await waitFor(() => expect(withdrawSubmission).toHaveBeenCalledWith({
      id: "budget-2026",
      rowVersion: "AQID",
      approvalRowVersion: "BAUG",
    }));
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("allows withdrawal of a pending revision without changing the approved budget", async () => {
    const withdrawSubmission = vi.fn().mockResolvedValue(undefined);
    mocks.withdrawSubmission.mockReturnValue({ mutateAsync: withdrawSubmission, isPending: false });
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Approved",
      reviewStatus: "RevisionPending",
      canWithdrawSubmission: true,
      reviewRequestId: "revision-approval-1",
      reviewRowVersion: "BAUG",
    })] })));

    renderWorkspace({ canWithdraw: true });

    fireEvent.click(screen.getByRole("button", { name: "Withdraw Submission" }));
    const dialog = screen.getByRole("alertdialog", { name: "Withdraw pending FY 2026 revision?" });
    expect(dialog).toHaveTextContent("existing approved Annual Budget stays unchanged");
    fireEvent.click(screen.getAllByRole("button", { name: "Withdraw Submission" })[1]);

    await waitFor(() => expect(withdrawSubmission).toHaveBeenCalledWith({
      id: "budget-2026",
      rowVersion: "AQID",
      approvalRowVersion: "BAUG",
    }));
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("shows authoritative delete block reasons and keeps blocked actions disabled", () => {
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Draft",
      canDeleteDraft: false,
      deleteDraftBlockReason: "permission-required",
      submittedBy: null,
      submittedAtUtc: null,
    })] })));
    const draftView = renderWorkspace({ canDelete: false });
    expect(screen.getByRole("button", { name: "Delete Draft" })).toBeDisabled();
    expect(screen.getByText("Project Annual Budget View and Delete permissions are required for this project.")).toBeInTheDocument();
    draftView.unmount();

    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Pending",
      canWithdrawSubmission: true,
      reviewRequestId: "approval-1",
      reviewRowVersion: "BAUG",
    })] })));
    const pendingView = renderWorkspace({ canWithdraw: false });
    expect(screen.queryByRole("button", { name: "Withdraw Submission" })).not.toBeInTheDocument();
    pendingView.unmount();

    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      canDeleteDraft: true,
      deleteDraftBlockReason: "not-draft",
    })] })));
    renderWorkspace({ canDelete: true });
    expect(screen.getByRole("button", { name: "Delete Draft" })).toBeDisabled();
    expect(screen.getByText(/Approved budgets cannot be deleted\./)).toBeInTheDocument();
  });

  it("renders the deletion confirmation in Arabic for the selected fiscal year", () => {
    mocks.annualBudgets.mockReturnValue(query(page({ items: [approvedBudget({
      status: "Draft",
      reviewStatus: "Draft",
      canDeleteDraft: true,
      deleteDraftBlockReason: null,
      submittedBy: null,
      submittedAtUtc: null,
    })] })));

    renderWorkspace({ canDelete: true, locale: "ar" });
    const deleteAction = screen.getAllByRole("button").find(button => button.className.includes("border-destructive"));
    expect(deleteAction).toBeDefined();
    fireEvent.click(deleteAction!);

    const dialog = screen.getByRole("alertdialog");
    expect(dialog.getAttribute("aria-label")).toContain("2026");
    expect(dialog.getAttribute("aria-label")).not.toContain("Delete");
    expect(dialog).toHaveTextContent("2026");
  });
});
