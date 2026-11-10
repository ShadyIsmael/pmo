import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export interface ProjectAnnualBudget {
  id: string;
  projectId: string;
  fiscalYear: number;
  fiscalYearLabel: string;
  fiscalYearStartDate: string;
  fiscalYearEndDate: string;
  financialReference: string;
  currency: string;
  newApprovedFunding: number;
  approvedCarryForward: number;
  approvedReservedCarryForward: number;
  approvedFreeCarryForward: number;
  approvedAdjustments: number;
  totalAnnualBudget: number;
  status: "Draft" | "Approved";
  approvedBy: string | null;
  approvedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
  closedByClosingId: string | null;
  closedAtUtc: string | null;
  closedBy: string | null;
  isClosed: boolean;
  canEdit: boolean;
  canDeleteDraft?: boolean;
  deleteDraftBlockReason?: string | null;
  canWithdrawSubmission?: boolean;
  canApprove: boolean;
  reviewStatus: "Draft" | "Pending" | "Returned" | "Rejected" | "Approved" | "RevisionPending" | "RevisionReturned";
  approvalMode: "Sequential" | "Parallel" | null;
  reviewRequestId: string | null;
  reviewRowVersion: string | null;
  submittedBy: string | null;
  submittedAtUtc: string | null;
  canSubmit: boolean;
  canReview: boolean;
  canRevise: boolean;
  reviewHistory: { event: string; actorName: string | null; timestampUtc: string; reason: string | null; approvalLevel: number | null }[];
}

export interface ProjectAnnualBudgetPage {
  currentFiscalYear: number;
  fiscalYearStartMonth: number;
  items: ProjectAnnualBudget[];
  totalProjectFinancialCeiling: number | null;
  projectCurrency: string;
  totalApprovedNewAllocations: number;
  pendingSubmittedAllocations: number;
  remainingUnallocatedCeiling: number | null;
  suggestedFiscalYear: number | null;
  suggestedNewFunding: number | null;
  suggestedNewFundingSource: string | null;
  missingClosingFiscalYear: number | null;
}

export interface ProjectAnnualBudgetEligibleApprover {
  userId: string;
  displayName: string;
  eligibleRoleCategories?: string[];
}

export interface ProjectAnnualBudgetConfiguredApprover extends ProjectAnnualBudgetEligibleApprover {
  position: number | null;
  isEligible: boolean;
  issueCode: string | null;
}

export interface ProjectAnnualBudgetApprovalConfiguration {
  projectId: string;
  projectName: string;
  configurationSource: "ProjectSettings";
  financeApprovalMode: "Sequential" | "Parallel" | "Both" | null;
  approvalMode: "Sequential" | "Parallel" | null;
  allowedModes: ("Sequential" | "Parallel")[];
  configuredApprovers: ProjectAnnualBudgetConfiguredApprover[];
  eligibleApprovers: ProjectAnnualBudgetEligibleApprover[];
  rowVersion: string | null;
  projectSettingsRowVersion: string;
  isConfigured: boolean;
  isValid: boolean;
  canSubmitWithDefaults: boolean;
  validationIssues: string[];
  canOverride: boolean;
}

export interface ProjectAnnualBudgetDraftApprovalConfiguration {
  projectId: string;
  projectName: string;
  configurationSource: "ProjectSettings" | "DraftOverride";
  projectApprovalMode: "Sequential" | "Parallel" | "Both" | null;
  approvalMode: "Sequential" | "Parallel" | null;
  allowedModes: ("Sequential" | "Parallel")[];
  configuredApprovers: ProjectAnnualBudgetConfiguredApprover[];
  eligibleApprovers: (ProjectAnnualBudgetEligibleApprover & { selectionBlockReason?: string | null })[];
  budgetRowVersion: string;
  projectSettingsRowVersion: string;
  projectSettingsConfigurationRowVersion: string | null;
  overrideReason: string | null;
  updatedAtUtc?: string | null;
  isValid: boolean;
  validationIssues: string[];
  canSave: boolean;
}

export interface AnnualBudgetApproverSelection {
  userId: string;
  position: number | null;
  requirement: "Mandatory" | "Optional";
}

export interface CreateProjectAnnualBudgetInput {
  fiscalYear: number;
  newApprovedFunding: number;
  approvedAdjustments: number;
}

const annualBudgetsKey = (projectId: string) => ["project-annual-budgets", projectId] as const;
const approvalConfigurationKey = (projectId: string) => ["project-annual-budget-approval-configuration", projectId] as const;

function invalidateAnnualBudgetViews(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: annualBudgetsKey(projectId) }),
    queryClient.invalidateQueries({ queryKey: approvalConfigurationKey(projectId) }),
    queryClient.invalidateQueries({ queryKey: ["project-annual-budget-draft-approval-configuration", projectId] }),
    queryClient.invalidateQueries({ queryKey: ["project-payment-requests", projectId] }),
    queryClient.invalidateQueries({ queryKey: ["project-financial-foundation", projectId] }),
    queryClient.invalidateQueries({ queryKey: ["project-annual-budget-closings", projectId] }),
    queryClient.invalidateQueries({ queryKey: ["project-financial-activity", projectId] }),
    queryClient.invalidateQueries({ queryKey: ["project-financial-activity-facets", projectId] }),
  ]);
}

export function useProjectAnnualBudgets(projectId: string, enabled = true) {
  return useQuery({
    queryKey: annualBudgetsKey(projectId),
    queryFn: () => api.get<ProjectAnnualBudgetPage>(`/api/projects/${encodeURIComponent(projectId)}/annual-budgets`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useCreateProjectAnnualBudget(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateProjectAnnualBudgetInput) =>
      api.post<ProjectAnnualBudget>(`/api/projects/${encodeURIComponent(projectId)}/annual-budgets`, input),
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useUpdateProjectAnnualBudget(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; newApprovedFunding: number; approvedAdjustments: number; rowVersion: string }) => {
      const { id, ...body } = input;
      return api.put<ProjectAnnualBudget>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(id)}`,
        body,
      );
    },
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useDeleteProjectAnnualBudgetDraft(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; rowVersion: string }) =>
      api.delete<void>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(input.id)}`,
        { rowVersion: input.rowVersion },
      ),
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useWithdrawProjectAnnualBudgetSubmission(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; rowVersion: string; approvalRowVersion: string }) => {
      const { id, ...body } = input;
      return api.post<ProjectAnnualBudget>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(id)}/withdraw`,
        body,
      );
    },
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useApproveProjectAnnualBudget(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; rowVersion: string }) => {
      const { id, ...body } = input;
      return api.post<ProjectAnnualBudget>(
        `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(id)}/approve`,
        body,
      );
    },
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useProjectAnnualBudgetApprovalConfiguration(projectId: string, enabled = true) {
  return useQuery({
    queryKey: approvalConfigurationKey(projectId),
    queryFn: () => api.get<ProjectAnnualBudgetApprovalConfiguration>(
      `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/approval-configuration`,
    ),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useSaveProjectAnnualBudgetApprovalConfiguration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      approvalMode: "Sequential" | "Parallel";
      approverUserIds: string[];
      rowVersion: string | null;
      projectSettingsRowVersion: string;
    }) => api.put<ProjectAnnualBudgetApprovalConfiguration>(
      `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/approval-configuration`,
      input,
    ),
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

const draftApprovalConfigurationKey = (projectId: string, itemId: string) =>
  ["project-annual-budget-draft-approval-configuration", projectId, itemId] as const;

export function useProjectAnnualBudgetDraftApprovalConfiguration(projectId: string, itemId: string, enabled = true) {
  return useQuery({
    queryKey: draftApprovalConfigurationKey(projectId, itemId),
    queryFn: () => api.get<ProjectAnnualBudgetDraftApprovalConfiguration>(
      `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(itemId)}/approval-configuration`,
    ),
    enabled: Boolean(projectId && itemId) && enabled,
    staleTime: 15_000,
  });
}

export function useSaveProjectAnnualBudgetDraftApprovalConfiguration(projectId: string, itemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      approvalMode: "Sequential" | "Parallel";
      approverUserIds: string[];
      budgetRowVersion: string;
      projectSettingsRowVersion: string;
      projectSettingsConfigurationRowVersion: string | null;
      overrideReason: string;
    }) => api.put<ProjectAnnualBudgetDraftApprovalConfiguration>(
      `/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(itemId)}/approval-configuration`,
      input,
    ),
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useSubmitProjectAnnualBudgetReview(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      id: string;
      approvalMode?: "Sequential" | "Parallel" | null;
      approvers?: AnnualBudgetApproverSelection[];
      rowVersion: string;
      customizeForThisSubmission?: boolean;
      overrideReason?: string | null;
      approvalConfigurationRowVersion?: string | null;
      projectSettingsRowVersion?: string | null;
    }) => {
      const { id, ...body } = input;
      return api.post<ProjectAnnualBudget>(`/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(id)}/submit`, body);
    },
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useDecideProjectAnnualBudgetReview(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; requestId: string; decision: "Approved" | "Rejected" | "Returned"; reason?: string; budgetRowVersion: string; approvalRowVersion: string }) => {
      const { id, requestId, ...body } = input;
      return api.post<ProjectAnnualBudget>(`/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(id)}/review/${encodeURIComponent(requestId)}`, body);
    },
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}

export function useReviseApprovedProjectAnnualBudget(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: {
      id: string;
      newApprovedFunding: number;
      approvedAdjustments: number;
      reason: string;
      approvalMode: "Sequential" | "Parallel" | null;
      approvers: AnnualBudgetApproverSelection[];
      rowVersion: string;
      customizeForThisSubmission: boolean;
      overrideReason: string | null;
      approvalConfigurationRowVersion: string | null;
      projectSettingsRowVersion: string | null;
    }) => {
      const { id, ...body } = input;
      return api.post<ProjectAnnualBudget>(`/api/projects/${encodeURIComponent(projectId)}/annual-budgets/${encodeURIComponent(id)}/revisions`, body);
    },
    onSuccess: () => invalidateAnnualBudgetViews(queryClient, projectId),
  });
}
