import { api } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";

export type ProjectStakeholderStatus = "Active" | "ContactOnly" | "Revoked" | string;

export interface ProjectStakeholderDto {
  id: string;
  projectId: string;
  userId?: string;
  name: string;
  email?: string;
  role: string;
  interest?: string;
  influence?: string;
  type: "Internal" | "ContactOnly" | string;
  status: ProjectStakeholderStatus;
  rowVersion: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AddInternalProjectStakeholderDto { userId: string; role: string; interest?: string; influence?: string; }
export interface AddContactProjectStakeholderDto { name: string; email?: string; role: string; interest?: string; influence?: string; }
export interface UpdateProjectStakeholderDto { name?: string; email?: string; role?: string; interest?: string; influence?: string; rowVersion: string; }

export const stakeholderKeys = {
  all: ["project-stakeholders"] as const,
  project: (projectId: string) => ["project-stakeholders", projectId] as const,
};

export const stakeholderApi = {
  list: (projectId: string) => api.get<ProjectStakeholderDto[]>(endpoints.projects.stakeholders(projectId)),
  addInternal: (projectId: string, body: AddInternalProjectStakeholderDto) => api.post<ProjectStakeholderDto>(endpoints.projects.addInternalStakeholder(projectId), body),
  addContact: (projectId: string, body: AddContactProjectStakeholderDto) => api.post<ProjectStakeholderDto>(endpoints.projects.addContactStakeholder(projectId), body),
  update: (projectId: string, id: string, body: UpdateProjectStakeholderDto) => api.put<ProjectStakeholderDto>(endpoints.projects.stakeholder(projectId, id), body),
  revoke: (projectId: string, id: string, rowVersion: string) => api.post<ProjectStakeholderDto>(endpoints.projects.revokeStakeholder(projectId, id), { rowVersion }),
};
