import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export type RecoveryEntityType = "Project" | "Program" | "Portfolio";
export type RecoveryFilter = "All" | "NoManager" | "NoSponsor" | "MissingGovernanceAssignment" | "PendingInitialAssignment" | "Pending" | "Rejected" | "Assigned" | "MissingParentHierarchy";

export interface AssignmentRecoveryItem {
  entityType: RecoveryEntityType;
  publicId: string;
  name: string;
  code?: string | null;
  managerId?: string | null;
  managerName?: string | null;
  managerIsActive?: boolean | null;
  managerAssignmentState: "Unassigned" | "Active" | "ManagerInactive" | "MissingAssignment" | "Inconsistent" | string;
  sponsorId?: string | null;
  sponsorName?: string | null;
  sponsorIsActive?: boolean | null;
  sponsorState: "NotApplicable" | "Unassigned" | "Assigned" | "Inactive" | "Invalid" | string;
  parentProgramName?: string | null;
  parentPortfolioName?: string | null;
  missingParentHierarchy: boolean;
  assignmentRequestStatus?: string | null;
  assignmentInitiationType?: string | null;
  proposedManagerId?: string | null;
  proposedManagerName?: string | null;
  initiatedByName?: string | null;
  assignmentRequestedAt?: string | null;
  assignmentRespondedAt?: string | null;
  rejectionReason?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AssignmentRecoverySummary {
  totalProjects: number;
  totalPrograms: number;
  totalPortfolios: number;
  unassignedManagers: number;
  projectsWithoutSponsor: number;
  pendingAssignments: number;
  rejectedAssignments: number;
  inconsistentAssignments: number;
}

export interface AssignmentRecoveryPage {
  items: AssignmentRecoveryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  summary: AssignmentRecoverySummary;
}

export interface RecoveryCandidate { userId: string; fullName: string; email: string; jobTitle?: string | null; managerTypes: string[]; }
export interface RecoveryAssignmentInput { managerId: string; effectiveDate: string; reason: string; notes?: string; }
export interface RecoveryAssignmentCreated { requestId: string; entityType: RecoveryEntityType; entityPublicId: string; entityName: string; status: string; initiationType: string; proposedManagerName: string; initiatedByName: string; requestedAt: string; }
export interface RecoveryHistoryItem { recordType: string; status: string; previousManagerName?: string | null; managerName: string; initiatedByName?: string | null; requestedAt?: string | null; effectiveDate: string; respondedAt?: string | null; reason?: string | null; notes?: string | null; rejectionReason?: string | null; }
export interface RecoveryHistory { items: RecoveryHistoryItem[]; }

export function useAssignmentRecoveryPage(type: RecoveryEntityType, page: number, pageSize: number, search: string, filter: RecoveryFilter, enabled: boolean) {
  return useQuery({
    queryKey: ["administration", "assignment-recovery", type, page, pageSize, search, filter],
    queryFn: () => api.get<AssignmentRecoveryPage>(endpoints.assignmentRecovery.list(type), { page, pageSize, search: search || undefined, filter }),
    enabled,
    staleTime: 15_000,
  });
}

export function useRecoveryManagerCandidates(type: RecoveryEntityType, publicId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["administration", "assignment-recovery", "manager-candidates", type, publicId],
    queryFn: () => api.get<RecoveryCandidate[]>(endpoints.assignmentRecovery.managerCandidates(type, publicId!)),
    enabled: enabled && Boolean(publicId),
    staleTime: 30_000,
  });
}

export function useRecoverySponsorCandidates(publicId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["administration", "assignment-recovery", "sponsor-candidates", publicId],
    queryFn: () => api.get<RecoveryCandidate[]>(endpoints.assignmentRecovery.sponsorCandidates(publicId!)),
    enabled: enabled && Boolean(publicId),
    staleTime: 30_000,
  });
}

export function useAssignmentRecoveryHistory(type: RecoveryEntityType, publicId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["administration", "assignment-recovery", "history", type, publicId],
    queryFn: () => api.get<RecoveryHistory>(endpoints.assignmentRecovery.history(type, publicId!)),
    enabled: enabled && Boolean(publicId),
    staleTime: 0,
  });
}

function useRecoveryMutation<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["administration", "assignment-recovery"] });
      void client.invalidateQueries({ queryKey: ["my-work", "approvals"] });
      void client.invalidateQueries({ queryKey: ["management", "perspectives"] });
      void client.invalidateQueries({ queryKey: ["auth", "me"] });
    },
  });
}

export function useCreateInitialRecoveryAssignment() {
  return useRecoveryMutation<{ type: RecoveryEntityType; publicId: string; input: RecoveryAssignmentInput }, RecoveryAssignmentCreated>(
    ({ type, publicId, input }) => api.post(endpoints.assignmentRecovery.initialAssignment(type, publicId), input)
  );
}

export function useCreateRecoveryHandover() {
  return useRecoveryMutation<{ type: RecoveryEntityType; publicId: string; input: RecoveryAssignmentInput }, RecoveryAssignmentCreated>(
    ({ type, publicId, input }) => api.post(endpoints.assignmentRecovery.administrativeHandover(type, publicId), input)
  );
}

export function useAssignRecoverySponsor() {
  return useRecoveryMutation<{ publicId: string; sponsorUserId: string }, AssignmentRecoveryItem>(
    ({ publicId, sponsorUserId }) => api.post(endpoints.assignmentRecovery.sponsor(publicId), { sponsorUserId })
  );
}

export function useReconcileRecoveryAssignment() {
  return useRecoveryMutation<{ type: RecoveryEntityType; publicId: string }, { entityType: RecoveryEntityType; entityPublicId: string; managerAssignmentState: string; message: string }>(
    ({ type, publicId }) => api.post(endpoints.assignmentRecovery.reconcile(type, publicId))
  );
}
