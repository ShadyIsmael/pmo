import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";
import { ProjectChangeRequestStatus, ProjectChangeRequestType } from '@/lib/enums';

export interface CreateProjectChangeRequestDto {
  projectId: string;
  type: ProjectChangeRequestType;
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetChangeAmount?: number | null;
  attachments?: string | null;
}

export interface ProjectChangeRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  number: number;
  type: ProjectChangeRequestType;
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetBeforeChange?: number | null;
  budgetChangeAmount?: number | null;
  proposedProjectBudget?: number | null;
  attachments?: string | null;
  status: ProjectChangeRequestStatus;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  reviewedBy?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  decisionNotes?: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export const projectChangeRequestKeys = {
  all: ["project-change-requests"] as const,
  byProject: (projectId: string) => ["project-change-requests", "by-project", projectId] as const,
  detail: (id: string) => ["project-change-requests", "detail", id] as const,
  pending: ["project-change-requests", "pending"] as const,
};

export function createProjectChangeRequest(projectId: string, dto: CreateProjectChangeRequestDto) {
  return api.post<ProjectChangeRequestDto>(endpoints.projects.changeRequests(projectId), dto);
}

export function getProjectChangeRequests(projectId: string) {
  return api.get<ProjectChangeRequestDto[]>(endpoints.projects.changeRequests(projectId));
}

export function getProjectChangeRequest(id: string) {
  return api.get<ProjectChangeRequestDto>(endpoints.projectChangeRequests.detail(id));
}

export function getPendingProjectChangeRequests() {
  return api.get<ProjectChangeRequestDto[]>(endpoints.projectChangeRequests.pending);
}

export interface ChangeRequestDecisionDto {
  decisionNotes?: string | null;
}

export function approveProjectChangeRequest(id: string, dto: ChangeRequestDecisionDto) {
  return api.post<ProjectChangeRequestDto>(endpoints.projectChangeRequests.approve(id), dto);
}

export function rejectProjectChangeRequest(id: string, dto: { decisionNotes: string }) {
  return api.post<ProjectChangeRequestDto>(endpoints.projectChangeRequests.reject(id), dto);
}

export function cancelProjectChangeRequest(id: string) {
  return api.post<ProjectChangeRequestDto>(endpoints.projectChangeRequests.cancel(id), {});
}