import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AnnualBudgetReviewDetailsSheet } from "@features/my-work/AnnualBudgetReviewDetailsSheet";

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  decide: vi.fn(),
  onDecision: vi.fn(),
  onOpenChange: vi.fn(),
}));

vi.mock("@api/client", () => ({ api: { get: mocks.get } }));
vi.mock("@features/projects/api/project-annual-budgets", () => ({
  useDecideProjectAnnualBudgetReview: () => ({ mutateAsync: mocks.decide, isPending: false, isError: false }),
}));
vi.mock("@i18n/messages", () => ({
  useI18n: () => ({
    locale: "en",
    t: (key: string, params?: Record<string, string | number>) => {
      const values: Record<string, string> = {
        "common.close": "Close",
        "common.loading": "Loading",
        "common.retry": "Retry",
        "common.saving": "Saving",
        "common.confirm": "Confirm",
        "common.cancel": "Cancel",
        "approvals.reviewSheet.title": "Annual Budget Review Details",
        "approvals.reviewSheet.description": "Review the request and approval progress.",
        "approvals.reviewSheet.loadFailed": "The review could not be loaded.",
        "approvals.reviewSheet.requestNotes": "Request notes",
        "approvals.reviewSheet.descriptionLabel": "Description",
        "approvals.reviewSheet.submissionComment": "Submission comment",
        "approvals.reviewSheet.justification": "Justification",
        "approvals.reviewSheet.decisionComment": "Decision comment",
        "approvals.reviewSheet.decisionCommentPlaceholder": "Add comments",
        "approvals.reviewSheet.rejectReasonRequired": "A reason is required.",
        "approvals.reviewSheet.readOnly": "Read-only review.",
        "approvals.reviewSheet.actionUnavailable": "This review cannot be acted on.",
        "approvals.reviewSheet.decisionFailed": "The decision failed.",
        "approvals.reviewSheet.reject": "Reject",
        "approvals.reviewSheet.approve": "Approve",
        "approvals.reviewSheet.confirmTitle": "Confirm decision",
        "approvals.reviewSheet.rejectConfirmTitle": "Confirm rejection",
        "approvals.reviewSheet.approveConfirmTitle": "Confirm approval",
        "approvals.reviewSheet.rejectConfirmDescription": "Reject this request?",
        "approvals.reviewSheet.approveConfirmDescription": "Approve this request?",
        "approvals.reviewSheet.requestType": "Request type",
        "approvals.reviewSheet.fiscalYear": "Fiscal year",
        "approvals.reviewSheet.fiscalPeriod": "Fiscal period",
        "approvals.reviewSheet.fiscalYearValue": `FY ${params?.year}`,
        "approvals.requestedBy": "Requested by",
        "approvals.requestedAt": "Requested at",
        "approvals.reviewSheet.budgetStatus": "Budget status",
        "approvals.reviewSheet.financialOverview": "Financial overview",
        "approvals.reviewSheet.lifetimeProjectBudget": "Lifetime Project Budget",
        "approvals.reviewSheet.lifetimeDescription": "Project-wide financial ceiling",
        "approvals.reviewSheet.annualBudgetUnderReview": "Annual Budget Under Review",
        "approvals.reviewSheet.newFunding": "New Funding",
        "approvals.reviewSheet.approvedCarryForward": "Approved Carry Forward",
        "approvals.reviewSheet.approvedAdjustments": "Approved Adjustments",
        "approvals.reviewSheet.totalAnnualBudget": "Total Annual Budget Requested",
        "approvals.reviewSheet.approvalProgress": "Approval progress",
        "approvals.reviewSheet.sequentialMethod": "Sequential approvals",
        "approvals.reviewSheet.parallelMethod": "Parallel approvals",
        "approvals.reviewSheet.activeReviewers": "Currently active reviewers",
        "approvals.reviewSheet.allReviewers": "Other assigned reviewers",
        "approvals.reviewSheet.sequentialTimeline": "Sequential approval timeline",
        "approvals.reviewSheet.waitingCount": `${params?.count} waiting`,
        "approvals.reviewSheet.youAwaitingDecision": "You — Awaiting Your Decision",
        "approvals.reviewSheet.waiting": "Waiting",
        "approvals.reviewSheet.active": "Active",
        "approvals.reviewSheet.stepNumber": `Step ${params?.number}`,
        "approvals.reviewSheet.decidedAt": `Decided ${params?.date}`,
        "approvals.reviewSheet.you": "You",
        "approvals.reviewSheet.unavailable": "Unavailable",
        "approvals.reviewSheet.initialRequest": "Initial allocation",
        "approvals.reviewSheet.revisionRequest": "Revision",
        "approvals.reviewSheet.status.Pending": "Pending",
        "approvals.reviewSheet.status.Approved": "Approved",
        "approvals.reviewSheet.status.Rejected": "Rejected",
        "approvals.reviewSheet.status.Returned": "Returned",
        "approvals.reviewSheet.status.Cancelled": "Cancelled",
        "approvals.reviewSheet.status.Completed": "Completed",
        "approvals.reviewSheet.status.unavailable": "Unavailable",
        "approvals.reviewSheet.requirement.Mandatory": "Mandatory",
        "approvals.reviewSheet.status.Draft": "Draft",
        "approvals.reviewSheet.disabled.request_completed": "This review is complete.",
      };
      return values[key] ?? key;
    },
    formatDate: (value: string) => value,
    formatDateTime: (value: string) => value,
    formatCurrency: (amount: number, currency: string) => `${currency} ${amount.toLocaleString("en-US")}`,
  }),
}));

function detail(overrides: Record<string, unknown> = {}) {
  return {
    requestId: "request-1",
    budgetId: "budget-1",
    projectPublicId: "11111111-1111-4111-8111-111111111111",
    projectName: "Core Modernization",
    projectCode: "PRJ-104",
    requestType: "ProjectAnnualBudgetReview",
    requestTitle: "Review Annual Budget FY2026",
    requestDescription: "Initial annual allocation submission.",
    status: "Pending",
    budgetStatus: "Draft",
    fiscalYear: 2026,
    fiscalYearStartDate: "2026-01-01",
    fiscalYearEndDate: "2026-12-31",
    requestedByName: "Finance Lead",
    requestedAtUtc: "2026-11-10T09:00:00Z",
    submissionComment: "First-year funding request",
    reason: "Delivery is scheduled for FY2026",
    newFunding: 100_000,
    approvedCarryForward: 5_000,
    approvedAdjustments: 20_000,
    totalAnnualBudget: 125_000,
    currency: "EGP",
    projectBudget: 500_000,
    projectCurrency: "EGP",
    approvalMode: "Sequential",
    steps: [
      {
        assignmentId: "assignment-1",
        userId: "reviewer-1",
        displayName: "Reviewer One",
        position: 1,
        requirement: "Mandatory",
        state: "Approved",
        decision: "Approved",
        decidedAtUtc: "2026-11-10T10:00:00Z",
        comment: "Reviewed and supported.",
        isCurrentUser: false,
        isActionable: false,
        workflowState: "Completed",
      },
      {
        assignmentId: "assignment-2",
        userId: "reviewer-2",
        displayName: "Current Reviewer",
        position: 2,
        requirement: "Mandatory",
        state: "Pending",
        decision: null,
        decidedAtUtc: null,
        comment: null,
        isCurrentUser: true,
        isActionable: true,
        workflowState: "Active",
      },
      {
        assignmentId: "assignment-3",
        userId: "reviewer-3",
        displayName: "Later Reviewer",
        position: 3,
        requirement: "Mandatory",
        state: "Pending",
        decision: null,
        decidedAtUtc: null,
        comment: null,
        isCurrentUser: false,
        isActionable: false,
        workflowState: "Waiting",
      },
    ],
    budgetRowVersion: "AQID",
    approvalRowVersion: "BAUG",
    canDecide: true,
    disabledReason: null,
    ...overrides,
  };
}

function renderSheet(overrides: Partial<{ readOnly: boolean; open: boolean; requestId: string }> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <AnnualBudgetReviewDetailsSheet
        requestId={overrides.requestId ?? "request-1"}
        open={overrides.open ?? true}
        readOnly={overrides.readOnly ?? false}
        onOpenChange={mocks.onOpenChange}
        onDecision={mocks.onDecision}
      />
    </QueryClientProvider>,
  );
}

describe("Annual Budget review details sheet", () => {
  beforeEach(() => {
    mocks.get.mockReset().mockResolvedValue(detail());
    mocks.decide.mockReset().mockResolvedValue({});
    mocks.onDecision.mockReset();
    mocks.onOpenChange.mockReset();
  });

  it("distinguishes the lifetime ceiling from the annual amount and shows persisted sequential progress", async () => {
    renderSheet();
    const sheet = await screen.findByRole("dialog", { name: "Annual Budget Review Details" });
    expect(await within(sheet).findByText("Lifetime Project Budget")).toBeInTheDocument();
    expect(within(sheet).getByText("EGP 500,000")).toBeInTheDocument();
    expect(within(sheet).getByText("Total Annual Budget Requested")).toBeInTheDocument();
    expect(within(sheet).getByText("EGP 125,000")).toBeInTheDocument();
    expect(within(sheet).getAllByText("You — Awaiting Your Decision")).toHaveLength(2);
    expect(within(sheet).getByText("Reviewed and supported.")).toBeInTheDocument();
    expect(within(sheet).getByText("Waiting")).toBeInTheDocument();
    expect(within(sheet).getAllByText(/Step [123]/)).toHaveLength(3);
  });

  it("requires a rejection reason and submits the existing secured decision contract", async () => {
    const user = userEvent.setup();
    renderSheet();
    const sheet = await screen.findByRole("dialog", { name: "Annual Budget Review Details" });
    await within(sheet).findByRole("button", { name: "Reject" });

    await user.click(within(sheet).getByRole("button", { name: "Reject" }));
    expect(within(sheet).getByText("A reason is required.")).toBeInTheDocument();
    expect(mocks.decide).not.toHaveBeenCalled();

    await user.type(within(sheet).getByLabelText("Decision comment"), "The request exceeds the annual plan.");
    await user.click(within(sheet).getByRole("button", { name: "Reject" }));
    const confirmation = await screen.findByRole("alertdialog", { name: "Confirm decision" });
    await user.click(within(confirmation).getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(mocks.decide).toHaveBeenCalledWith({
      id: "budget-1",
      requestId: "request-1",
      decision: "Rejected",
      reason: "The request exceeds the annual plan.",
      budgetRowVersion: "AQID",
      approvalRowVersion: "BAUG",
    }));
    expect(mocks.onDecision).toHaveBeenCalledOnce();
  });

  it("keeps completed or otherwise read-only reviews accessible without decision actions", async () => {
    mocks.get.mockResolvedValue(detail({ status: "Approved", canDecide: false, disabledReason: "request_completed" }));
    renderSheet({ readOnly: true });
    const sheet = await screen.findByRole("dialog", { name: "Annual Budget Review Details" });
    await within(sheet).findByText("This review is complete.");
    expect(within(sheet).getAllByRole("button", { name: "Close" }).length).toBeGreaterThan(0);
    expect(within(sheet).queryByRole("button", { name: "Approve" })).not.toBeInTheDocument();
    expect(within(sheet).queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
    expect(within(sheet).getByText("This review is complete.")).toBeInTheDocument();
  });

  it("shows parallel active reviewers without sequential step numbering", async () => {
    mocks.get.mockResolvedValue(detail({
      approvalMode: "Parallel",
      steps: detail().steps.map((step, index) => ({
        ...step,
        position: null,
        workflowState: index < 2 ? "Active" : "Waiting",
        isActionable: index === 1,
        isCurrentUser: index === 1,
      })),
    }));
    renderSheet();
    const sheet = await screen.findByRole("dialog", { name: "Annual Budget Review Details" });
    await within(sheet).findByText("Currently active reviewers");
    expect(within(sheet).getByText("Currently active reviewers")).toBeInTheDocument();
    expect(within(sheet).getByText("Reviewer One")).toBeInTheDocument();
    expect(within(sheet).getByText("Current Reviewer — You")).toBeInTheDocument();
    expect(within(sheet).queryByText(/Step [123]/)).not.toBeInTheDocument();
  });
});
