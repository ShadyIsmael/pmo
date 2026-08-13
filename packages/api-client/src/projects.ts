import type { ApiClient } from "./client";
import type { Project, ProjectMember, Milestone, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createProjectsApi(client: ApiClient) {
  return {
    getProjects: (params?: PaginationParams & { portfolioId?: string; status?: string }) =>
      client.getPaged<Project>("/projects", params),

    getProject: (id: string) => client.get<Project>(`/projects/${id}`),

    createProject: (data: Partial<Project>) => client.post<Project>("/projects", data),

    updateProject: (id: string, data: Partial<Project>) => client.put<Project>(`/projects/${id}`, data),

    deleteProject: (id: string) => client.delete<void>(`/projects/${id}`),

    getProjectMilestones: (projectId: string, params?: PaginationParams) =>
      client.getPaged<Milestone>(`/projects/${projectId}/milestones`, params),

    getProjectTeam: (projectId: string) =>
      client.get<readonly ProjectMember[]>(`/projects/${projectId}/team`),
  };
}
