import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export interface ProjectFinancialFoundation {
  projectId: string;
  projectCurrency: string | null;
  approvedBudget: number | null;
  approvedPaymentRequestAllocations: number | null;
  availableBudget: number | null;
  actualPaid: number | null;
  outstandingPaymentBalance: number | null;
  financeApprovalMode: "Sequential" | "Parallel" | "Both" | null;
  rowVersion: string;
}

export interface ProjectFinancialActivityEvent {
  id: string;
  entityType: string;
  action: string;
  actorId: string | null;
  actorName: string | null;
  timestampUtc: string;
  description: string | null;
  changes: Record<string, { from: string | null; to: string | null }>;
  detailsRestricted: boolean;
}

export interface ProjectFinancialActivityResponse {
  items: ProjectFinancialActivityEvent[];
  page: number;
  pageSize: number;
  total: number;
  hasMore: boolean;
}

export interface ProjectFinancialActivityFacets {
  eventTypes: string[];
  actors: { id: string; name: string }[];
  coverageStartUtc: string | null;
}

export interface FinancialActivityFilters {
  page: number;
  pageSize: number;
  eventType?: string;
  actorId?: string;
  from?: string;
  to?: string;
  reference?: string;
}

export interface PaymentRequestApprover {
  userId: string;
  displayName: string;
  position: number | null;
  requirement: "Mandatory" | "Optional";
  decisionStatus: string;
  decidedAt: string | null;
  comment: string | null;
  isActive: boolean;
  canDecide: boolean;
}

export interface ProjectPaymentRequest {
  id: string;
  projectId: string;
  reference: string;
  title: string;
  amount: number;
  currency: string;
  reason: string;
  projectPlanMilestoneId: string | null;
  projectPlanMilestoneName: string | null;
  status: string;
  requesterId: string;
  requesterName: string;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  completedAtUtc: string | null;
  approvalMode: "Sequential" | "Parallel" | null;
  approvers: PaymentRequestApprover[];
  rowVersion: string;
  canEdit: boolean;
  canSubmit: boolean;
  canDecide: boolean;
  canCancel: boolean;
  annualBudgetId: string | null;
  annualBudgetFiscalYear: number | null;
  annualBudgetReference: string | null;
}

export interface ProjectPaymentRequestListItem extends Omit<ProjectPaymentRequest, "projectId" | "reason" | "projectPlanMilestoneId" | "projectPlanMilestoneName" | "approvers"> {
  reservesBudget: boolean;
}

export interface ProjectPaymentRequestPage {
  items: ProjectPaymentRequestListItem[];
  page: number;
  pageSize: number;
  total: number;
  budgetAvailability: ProjectPaymentRequestBudgetAvailability;
}

export interface ProjectPaymentRequestBudgetAvailability {
  annualBudgetId: string | null;
  fiscalYear: number;
  fiscalYearLabel: string;
  fiscalYearStartDate: string;
  fiscalYearEndDate: string;
  annualBudgetReference: string | null;
  currency: string;
  annualBudgetStatus: "Draft" | "Approved" | null;
  approvedAnnualBudget: number | null;
  approvedReservations: number;
  availableAmount: number | null;
  legacyUnassociatedApprovedRequests: number;
  availabilityStatus: "AnnualBudgetMissing" | "AnnualBudgetDraft" | "LegacyReconciliationRequired" | "CurrencyOrPeriodMismatch" | "FiscalYearClosed" | "NoAvailableBalance" | "Ready";
  canCreate: boolean;
  canSubmit: boolean;
}

export interface ProjectPaymentRequestSettlement {
  id: string;
  paymentRequestId: string;
  projectId: string;
  paymentRequestReference: string;
  settlementReference: string;
  amount: number;
  currency: string;
  paymentDate: string;
  reportingFiscalYear: number;
  description: string | null;
  createdAtUtc: string;
  createdBy: string | null;
}

export interface ProjectPaymentRequestSettlementSummary {
  paymentRequestId: string;
  paymentRequestReference: string;
  requestStatus: string;
  approvedAmount: number;
  totalSettled: number;
  totalReleased: number;
  effectiveActiveAllocation: number;
  outstandingAmount: number;
  settlementStatus: "Not Settled" | "Partially Settled" | "Fully Settled";
  currency: string;
  annualBudgetId: string | null;
  annualBudgetFiscalYear: number | null;
  annualBudgetReference: string | null;
}

export interface ProjectPaymentReversal {
  id: string;
  reference: string;
  originalPaymentRequestId: string;
  originalPaymentRequestReference: string;
  annualBudgetId: string | null;
  annualBudgetFiscalYear: number | null;
  annualBudgetReference: string | null;
  originalApprovedAmount: number;
  totalSettled: number;
  previouslyReleasedAmount: number;
  maximumReversibleAmount: number;
  amount: number;
  currency: string;
  reason: string;
  status: string;
  requesterId: string;
  requesterName: string;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  completedAtUtc: string | null;
  approvalMode: "Sequential" | "Parallel" | null;
  approvers: PaymentRequestApprover[];
  rowVersion: string;
  canEdit: boolean;
  canSubmit: boolean;
  canDecide: boolean;
  canCancel: boolean;
}

export interface ProjectPaymentReversalPage {
  originalPaymentRequestId: string;
  originalPaymentRequestReference: string;
  originalApprovedAmount: number;
  totalSettled: number;
  totalReleased: number;
  remainingActiveAllocation: number;
  remainingPayable: number;
  maximumReversibleAmount: number;
  currency: string;
  annualBudgetId: string | null;
  annualBudgetFiscalYear: number | null;
  annualBudgetReference: string | null;
  hasPendingReversal: boolean;
  canCreate: boolean;
  items: ProjectPaymentReversal[];
}

export interface ProjectPaymentRequestSettlementPage {
  summary: ProjectPaymentRequestSettlementSummary;
  items: ProjectPaymentRequestSettlement[];
  page: number;
  pageSize: number;
  total: number;
}

export interface PaymentRequestApproverCandidate { userId: string; displayName: string; }
export interface PaymentRequestMilestoneOption { id: string; name: string; endDate: string | null; }

export interface CreatePaymentRequestInput {
  title: string;
  amount: number;
  reason: string;
  projectPlanMilestoneId?: string;
  idempotencyKey: string;
}

export interface PaymentRequestApproverSelection {
  userId: string;
  position: number | null;
  requirement: "Mandatory" | "Optional";
}

const paymentRequestsKey = (projectId: string, status?: string, pageSize = 50) =>
  ["project-payment-requests", projectId, status ?? "all", pageSize] as const;
const paymentRequestKey = (projectId: string, requestId: string) => ["project-payment-request", projectId, requestId] as const;
const paymentRequestSettlementsKey = (projectId: string, requestId: string) => ["project-payment-request-settlements", projectId, requestId] as const;
const paymentRequestReversalsKey = (projectId: string, requestId: string) => ["project-payment-request-reversals", projectId, requestId] as const;

function invalidateProjectFinance(queryClient: ReturnType<typeof useQueryClient>, projectId: string, requestId?: string) {
  void queryClient.invalidateQueries({ queryKey: ["project-payment-requests", projectId] });
  if (requestId) void queryClient.invalidateQueries({ queryKey: paymentRequestKey(projectId, requestId) });
  if (requestId) void queryClient.invalidateQueries({ queryKey: paymentRequestSettlementsKey(projectId, requestId) });
  if (requestId) void queryClient.invalidateQueries({ queryKey: paymentRequestReversalsKey(projectId, requestId) });
  void queryClient.invalidateQueries({ queryKey: foundationKey(projectId) });
  void queryClient.invalidateQueries({ queryKey: ["project-financial-activity", projectId] });
  void queryClient.invalidateQueries({ queryKey: ["project-financial-activity-facets", projectId] });
  void queryClient.invalidateQueries({ queryKey: ["project-annual-budget-closings", projectId] });
  void queryClient.invalidateQueries({ queryKey: ["project-annual-budgets", projectId] });
}

export function useProjectPaymentRequests(projectId: string, enabled = true, status?: string, pageSize = 50) {
  return useQuery({
    queryKey: paymentRequestsKey(projectId, status, pageSize),
    queryFn: () => api.get<ProjectPaymentRequestPage>(
      `/api/projects/${encodeURIComponent(projectId)}/payment-requests`,
      { page: 1, pageSize, status }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
  });
}

export function useProjectPaymentRequest(projectId: string, requestId: string | null, enabled = true) {
  return useQuery({
    queryKey: requestId ? paymentRequestKey(projectId, requestId) : paymentRequestsKey(projectId),
    queryFn: () => api.get<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId!)}`),
    enabled: Boolean(projectId && requestId) && enabled,
  });
}

export function useProjectPaymentRequestSettlements(projectId: string, requestId: string | null, enabled = true) {
  return useQuery({
    queryKey: requestId ? paymentRequestSettlementsKey(projectId, requestId) : ["project-payment-request-settlements", projectId],
    queryFn: () => api.get<ProjectPaymentRequestSettlementPage>(
      `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId!)}/settlements`,
      { page: 1, pageSize: 100 }),
    enabled: Boolean(projectId && requestId) && enabled,
  });
}

export function useProjectPaymentReversals(projectId: string, requestId: string | null, enabled = true) {
  return useQuery({
    queryKey: requestId ? paymentRequestReversalsKey(projectId, requestId) : ["project-payment-request-reversals", projectId],
    queryFn: () => api.get<ProjectPaymentReversalPage>(
      `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId!)}/reversals`),
    enabled: Boolean(projectId && requestId) && enabled,
  });
}

export function useAssignedProjectPaymentReversalReview(projectId: string, requestId: string | null, reversalId: string | null, enabled = true) {
  return useQuery({
    queryKey: ["assigned-project-payment-reversal-review", projectId, requestId, reversalId],
    queryFn: () => api.get<ProjectPaymentReversal>(
      `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId!)}/reversals/${encodeURIComponent(reversalId!)}/review`),
    enabled: Boolean(projectId && requestId && reversalId) && enabled,
  });
}

export function useCreateProjectPaymentReversal(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { requestId: string; amount: number; reason: string; idempotencyKey: string }) => {
      const { requestId, ...body } = input;
      return api.post<ProjectPaymentReversal>(
        `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId)}/reversals`, body);
    },
    onSuccess: (reversal) => invalidateProjectFinance(queryClient, projectId, reversal.originalPaymentRequestId),
  });
}

export function useUpdateProjectPaymentReversal(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { requestId: string; reversalId: string; amount: number; reason: string; rowVersion: string }) => {
      const { requestId, reversalId, ...body } = input;
      return api.put<ProjectPaymentReversal>(
        `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId)}/reversals/${encodeURIComponent(reversalId)}`, body);
    },
    onSuccess: (reversal) => invalidateProjectFinance(queryClient, projectId, reversal.originalPaymentRequestId),
  });
}

export function useSubmitProjectPaymentReversal(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { requestId: string; reversalId: string; approvalMode: "Sequential" | "Parallel"; approvers: PaymentRequestApproverSelection[]; rowVersion: string }) => {
      const { requestId, reversalId, ...body } = input;
      return api.post<ProjectPaymentReversal>(
        `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId)}/reversals/${encodeURIComponent(reversalId)}/submit`, body);
    },
    onSuccess: (reversal) => invalidateProjectFinance(queryClient, projectId, reversal.originalPaymentRequestId),
  });
}

export function useDecideProjectPaymentReversal(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { requestId: string; reversalId: string; decision: "Approved" | "Rejected"; comment?: string; rowVersion: string }) => {
      const { requestId, reversalId, ...body } = input;
      return api.post<ProjectPaymentReversal>(
        `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId)}/reversals/${encodeURIComponent(reversalId)}/decisions`, body);
    },
    onSuccess: (reversal) => invalidateProjectFinance(queryClient, projectId, reversal.originalPaymentRequestId),
  });
}

export function useCancelProjectPaymentReversal(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { requestId: string; reversalId: string; rowVersion: string }) => {
      const { requestId, reversalId, ...body } = input;
      return api.post<ProjectPaymentReversal>(
        `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId)}/reversals/${encodeURIComponent(reversalId)}/cancel`, body);
    },
    onSuccess: (reversal) => invalidateProjectFinance(queryClient, projectId, reversal.originalPaymentRequestId),
  });
}

export function useRecordProjectPaymentRequestSettlement(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { requestId: string; idempotencyKey: string; amount: number; paymentDate: string; paymentReference: string; description: string }) => {
      const { requestId, ...body } = input;
      return api.post<ProjectPaymentRequestSettlement>(
        `/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(requestId)}/settlements`, body);
    },
    onSuccess: (settlement) => invalidateProjectFinance(queryClient, projectId, settlement.paymentRequestId),
  });
}

export function usePaymentRequestApprovers(projectId: string, enabled = true) {
  return useQuery({
    queryKey: ["project-payment-request-approvers", projectId],
    queryFn: () => api.get<PaymentRequestApproverCandidate[]>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/approvers`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectPaymentReversalApprovers(projectId: string, enabled = true) {
  return useQuery({
    queryKey: ["project-payment-reversal-approvers", projectId],
    queryFn: () => api.get<PaymentRequestApproverCandidate[]>(
      `/api/projects/${encodeURIComponent(projectId)}/payment-requests/reversal-approvers`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function usePaymentRequestMilestones(projectId: string, enabled = true) {
  return useQuery({
    queryKey: ["project-payment-request-milestones", projectId],
    queryFn: () => api.get<PaymentRequestMilestoneOption[]>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/milestones`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useCreateProjectPaymentRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreatePaymentRequestInput) => api.post<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests`, input),
    onSuccess: () => invalidateProjectFinance(queryClient, projectId),
  });
}

export function useUpdateProjectPaymentRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; title: string; amount: number; reason: string; projectPlanMilestoneId?: string; rowVersion: string }) => {
      const { id, ...body } = input;
      return api.put<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(id)}`, body);
    },
    onSuccess: (request) => invalidateProjectFinance(queryClient, projectId, request.id),
  });
}

export function useSubmitProjectPaymentRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; approvalMode: "Sequential" | "Parallel"; approvers: PaymentRequestApproverSelection[]; rowVersion: string }) => {
      const { id, ...body } = input;
      return api.post<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(id)}/submit`, body);
    },
    onSuccess: (request) => invalidateProjectFinance(queryClient, projectId, request.id),
  });
}

export function useDecideProjectPaymentRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; decision: "Approved" | "Rejected"; comment?: string; rowVersion: string }) => {
      const { id, ...body } = input;
      return api.post<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(id)}/decisions`, body);
    },
    onSuccess: (request) => invalidateProjectFinance(queryClient, projectId, request.id),
  });
}

export function useCancelProjectPaymentRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; rowVersion: string }) => {
      const { id, ...body } = input;
      return api.post<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(id)}/cancel`, body);
    },
    onSuccess: (request) => invalidateProjectFinance(queryClient, projectId, request.id),
  });
}

const foundationKey = (projectId: string) => ["project-financial-foundation", projectId] as const;
const activityKey = (projectId: string, filters: FinancialActivityFilters) => ["project-financial-activity", projectId, filters] as const;

export function useProjectFinancialFoundation(projectId: string, enabled = true) {
  return useQuery({
    queryKey: foundationKey(projectId),
    queryFn: () => api.get<ProjectFinancialFoundation>(`/api/projects/${encodeURIComponent(projectId)}/financial-foundation`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useUpdateProjectFinanceApprovalMode(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { mode: ProjectFinancialFoundation["financeApprovalMode"]; rowVersion: string }) =>
      api.put<{ mode: ProjectFinancialFoundation["financeApprovalMode"]; rowVersion: string }>(
        `/api/projects/${encodeURIComponent(projectId)}/financial-foundation/approval-configuration`, input),
    onSuccess: async () => Promise.all([
      queryClient.invalidateQueries({ queryKey: foundationKey(projectId) }),
      queryClient.invalidateQueries({ queryKey: ["project-annual-budget-approval-configuration", projectId] }),
    ]),
  });
}

export function useProjectFinancialActivityFacets(projectId: string, enabled = true) {
  return useQuery({
    queryKey: ["project-financial-activity-facets", projectId],
    queryFn: () => api.get<ProjectFinancialActivityFacets>(`/api/projects/${encodeURIComponent(projectId)}/financial-activity/facets`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 60_000,
  });
}

export function useProjectFinancialActivity(projectId: string, filters: FinancialActivityFilters, enabled = true) {
  return useQuery({
    queryKey: activityKey(projectId, filters),
    queryFn: () => api.get<ProjectFinancialActivityResponse>(
      `/api/projects/${encodeURIComponent(projectId)}/financial-activity`,
      {
        page: filters.page,
        pageSize: filters.pageSize,
        eventType: filters.eventType || undefined,
        actorId: filters.actorId || undefined,
        from: filters.from || undefined,
        to: filters.to || undefined,
        reference: filters.reference || undefined,
      }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export interface ProjectAnnualBudgetClosingDecision {
  userId: string;
  userName: string | null;
  position: number | null;
  requirement: "Mandatory" | "Optional";
  state: string;
  decision: string | null;
  decidedAtUtc: string | null;
  comment: string | null;
}

export interface ProjectAnnualBudgetClosing {
  id: string;
  projectId: string;
  sourceAnnualBudgetId: string;
  fiscalYear: number;
  fiscalYearLabel: string;
  fiscalYearStartDate: string;
  fiscalYearEndDate: string;
  currency: string;
  status: "Review" | "PendingApproval" | "Rejected" | "Closed" | "Cancelled";
  approvalMode: "Sequential" | "Parallel" | null;
  approvalRequestId: string | null;
  destinationAnnualBudgetId: string | null;
  destinationFiscalYear: number | null;
  approvedAnnualBudget: number;
  newFunding: number;
  incomingCarryForward: number;
  approvedAdjustments: number;
  approvedPaymentRequestAllocations: number;
  approvedReversalReleases: number;
  effectiveActiveAllocations: number;
  actualSettlementsInFiscalYear: number;
  outstandingCommitments: number;
  freeAvailableBudget: number;
  totalUnspentBudget: number;
  proposedFreeCarryForward: number;
  carryOutstandingCommitments: boolean;
  approvedFreeCarryForward: number;
  approvedReservedCarryForward: number;
  nonCarriedFreeBalance: number;
  nextFiscalYearNewFunding: number;
  nextFiscalYearAdjustments: number;
  pendingPaymentRequestCount: number;
  pendingReversalRequestCount: number;
  unresolvedLegacyRequestCount: number;
  integrityIssueCount: number;
  integrityIssues: string[];
  approverDecisions: ProjectAnnualBudgetClosingDecision[];
  preparedBy: string | null;
  preparedAtUtc: string | null;
  submittedBy: string | null;
  submittedAtUtc: string | null;
  completedBy: string | null;
  completedAtUtc: string | null;
  rowVersion: string;
  canSubmit: boolean;
  canApprove: boolean;
  canCancel: boolean;
}

export interface ProjectAnnualBudgetClosingPage {
  currentFiscalYear: number;
  items: ProjectAnnualBudgetClosing[];
}

export interface PrepareProjectAnnualBudgetClosingInput {
  fiscalYear: number;
  proposedFreeCarryForward: number;
  carryOutstandingCommitments: boolean;
  nextFiscalYearNewFunding: number;
  nextFiscalYearAdjustments: number;
  idempotencyKey: string;
}

const annualBudgetClosingsKey = (projectId: string) => ["project-annual-budget-closings", projectId] as const;

export function useProjectAnnualBudgetClosings(projectId: string, enabled = true) {
  return useQuery({
    queryKey: annualBudgetClosingsKey(projectId),
    queryFn: () => api.get<ProjectAnnualBudgetClosingPage>(`/api/projects/${encodeURIComponent(projectId)}/annual-budget-closings`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useProjectAnnualBudgetClosingApprovers(projectId: string, enabled = true) {
  return useQuery({
    queryKey: ["project-annual-budget-closing-approvers", projectId],
    queryFn: () => api.get<PaymentRequestApproverCandidate[]>(
      `/api/projects/${encodeURIComponent(projectId)}/annual-budget-closings/approvers`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function usePrepareProjectAnnualBudgetClosing(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: PrepareProjectAnnualBudgetClosingInput) => api.post<ProjectAnnualBudgetClosing>(
      `/api/projects/${encodeURIComponent(projectId)}/annual-budget-closings`, input),
    onSuccess: () => invalidateProjectFinance(queryClient, projectId),
  });
}

export function useSubmitProjectAnnualBudgetClosing(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { closingId: string; approvalMode: "Sequential" | "Parallel"; approvers: PaymentRequestApproverSelection[]; rowVersion: string }) => {
      const { closingId, ...body } = input;
      return api.post<ProjectAnnualBudgetClosing>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budget-closings/${encodeURIComponent(closingId)}/submit`, body);
    },
    onSuccess: () => invalidateProjectFinance(queryClient, projectId),
  });
}

export function useDecideProjectAnnualBudgetClosing(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { closingId: string; decision: "Approved" | "Rejected"; comment?: string }) => {
      const { closingId, ...body } = input;
      return api.post<ProjectAnnualBudgetClosing>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budget-closings/${encodeURIComponent(closingId)}/decisions`, body);
    },
    onSuccess: () => invalidateProjectFinance(queryClient, projectId),
  });
}

export function useCancelProjectAnnualBudgetClosing(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { closingId: string; rowVersion: string }) => {
      const { closingId, ...body } = input;
      return api.post<ProjectAnnualBudgetClosing>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budget-closings/${encodeURIComponent(closingId)}/cancel`, body);
    },
    onSuccess: () => invalidateProjectFinance(queryClient, projectId),
  });
}
