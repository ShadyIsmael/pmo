import { api, type PagedResult } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export type RiskLevel = "Low" | "Medium" | "High" | "Critical";
export type RiskStatus = "Identified" | "ActionRequired" | "FollowUp" | "Mitigated" | "Closed";
export type RiskActionStatus = "Planned" | "InProgress" | "Completed" | "Cancelled";
export type RiskImpactType = "Schedule" | "Budget" | "Scope" | "Quality" | "Resources" | "Procurement" | "Compliance" | "Reputation" | "Operations" | "Other";

export interface RiskDto { id: string; projectId: string; projectName: string; title: string; description: string; riskLevel: RiskLevel; status: RiskStatus; impactTypes?: RiskImpactType[]; lastActionUpdateText?: string; lastActionUpdateAt?: string; lastActionUpdateCreatedBy?: string; ownerResourceId?: string; ownerResourceName?: string; createdAt: string; updatedAt?: string; createdBy: string; updatedBy?: string; rowVersion: number[]; }
export interface CreateRiskImpactAssessmentDto { impactTypes: RiskImpactType[]; description: string; }
export interface CreateRiskDto { projectId: string; title: string; description: string; riskLevel: RiskLevel; ownerResourceId?: string; impactAssessment?: CreateRiskImpactAssessmentDto; }
export interface UpdateRiskDto { title: string; description: string; riskLevel: RiskLevel; ownerResourceId?: string; rowVersion?: number[]; }
export interface RiskImpactAssessmentDto extends CreateRiskImpactAssessmentDto { id: string; riskId: string; createdAt: string; updatedAt?: string; createdBy: string; rowVersion: number[]; }
export interface UpdateRiskImpactAssessmentDto extends CreateRiskImpactAssessmentDto { rowVersion?: number[]; }
export interface RiskActionDto { id: string; riskId: string; title: string; description: string; ownerResourceId?: string; ownerResourceName?: string; targetDate?: string; nextFollowUpDate?: string; status: RiskActionStatus; sortOrder: number; createdAt: string; updatedAt?: string; rowVersion: number[]; updateCount: number; }
export interface CreateRiskActionDto { title: string; description: string; ownerResourceId?: string; targetDate?: string; nextFollowUpDate?: string; status?: RiskActionStatus; sortOrder?: number; }
export interface UpdateRiskActionDto extends CreateRiskActionDto { status: RiskActionStatus; rowVersion?: number[]; }
export type RiskActionTimelineEntryType = "Comment" | "SystemChange";
export interface RiskActionChangeDto { field: string; oldValue?: string | null; newValue?: string | null; }
export interface RiskActionUpdateDto { id: string; riskActionId: string; updateText: string; createdBy: string; createdAt: string; entryType: RiskActionTimelineEntryType; changes: RiskActionChangeDto[]; }
export interface CreateRiskActionUpdateDto { updateText: string; }

export const riskKeys = {
  all: ["risks"] as const,
  list: (filters: { projectId?: string; page?: number; pageSize?: number; status?: RiskStatus; ownerResourceId?: string } = {}) => ["risks", "list", filters] as const,
  dashboard: () => riskKeys.list(),
  detail: (id: string) => ["risks", "detail", id] as const,
  project: (projectId: string) => ["risks", "project", projectId] as const,
  impacts: (riskId: string) => ["risks", "impacts", riskId] as const,
  actions: (riskId: string) => ["risks", "actions", riskId] as const,
  updates: (riskId: string, actionId: string) => ["risks", "updates", riskId, actionId] as const,
};

export const riskApi = {
  list: (params?: { projectId?: string; page?: number; pageSize?: number; status?: RiskStatus; ownerResourceId?: string }) => api.get<PagedResult<RiskDto>>(endpoints.risks.root, params),
  project: (projectId: string, params?: { page?: number; pageSize?: number; ownerResourceId?: string }) => api.get<PagedResult<RiskDto>>(endpoints.risks.byProject(projectId), params),
  detail: (id: string) => api.get<RiskDto>(endpoints.risks.detail(id)),
  create: (data: CreateRiskDto) => api.post<RiskDto>(endpoints.risks.root, data),
  update: (id: string, data: UpdateRiskDto) => api.put<RiskDto>(endpoints.risks.detail(id), data),
  updateStatus: (id: string, data: { status: RiskStatus; rowVersion?: number[] }) => api.put<RiskDto>(endpoints.risks.status(id), data),
  impacts: (id: string) => api.get<RiskImpactAssessmentDto[]>(endpoints.risks.impacts(id)),
  createImpact: (id: string, data: CreateRiskImpactAssessmentDto) => api.post<RiskImpactAssessmentDto>(endpoints.risks.impacts(id), data),
  updateImpact: (riskId: string, impactId: string, data: UpdateRiskImpactAssessmentDto) => api.put<RiskImpactAssessmentDto>(endpoints.risks.impact(riskId, impactId), data),
  deleteImpact: (riskId: string, impactId: string) => api.delete<void>(endpoints.risks.impact(riskId, impactId)),
  actions: (id: string) => api.get<RiskActionDto[]>(endpoints.risks.actions(id)),
  createAction: (id: string, data: CreateRiskActionDto) => api.post<RiskActionDto>(endpoints.risks.actions(id), data),
  updateAction: (riskId: string, actionId: string, data: UpdateRiskActionDto) => api.put<RiskActionDto>(endpoints.risks.action(riskId, actionId), data),
  deleteAction: (riskId: string, actionId: string) => api.delete<void>(endpoints.risks.action(riskId, actionId)),
  updates: (riskId: string, actionId: string) => api.get<RiskActionUpdateDto[]>(endpoints.risks.updates(riskId, actionId)),
  addUpdate: (riskId: string, actionId: string, data: CreateRiskActionUpdateDto) => api.post<RiskActionUpdateDto>(endpoints.risks.updates(riskId, actionId), data),
};
