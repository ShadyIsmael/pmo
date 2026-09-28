import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export interface StakeholderPublishedStatusReport {
  id: string;
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  projectLifecycleStatus?: string;
  overallHealth?: string;
  overallProgressPercent?: number;
  scheduleHealth?: string;
  costHealth?: string;
  scopeHealth?: string;
  qualityHealth?: string;
  resourceHealth?: string;
  executiveSummary?: string;
  keyAchievements?: string;
  challengesConcerns?: string;
  nextPeriodPriorities?: string;
  preparedByName?: string;
  publishedByName?: string;
  publishedAt?: string;
}

export interface StakeholderPublishedStatusHistory {
  id: string;
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  projectLifecycleStatus?: string;
  overallHealth?: string;
  overallProgressPercent?: number;
  publishedAt?: string;
}

export interface StakeholderProjectMilestone {
  id: string;
  name: string;
  dueDate?: string;
  completedDate?: string;
  status?: string;
  progress?: number;
}

export interface StakeholderProjectView {
  id: string;
  code: string;
  name: string;
  description?: string;
  status: string;
  health: string;
  progress: number;
  startDate?: string;
  endDate?: string;
  projectManagerName?: string;
  sponsorName?: string;
  latestPublishedStatusReport?: StakeholderPublishedStatusReport;
  publishedStatusHistory: StakeholderPublishedStatusHistory[];
  milestones: StakeholderProjectMilestone[];
}

export const stakeholderKeys = {
  all: ["stakeholder-projects"] as const,
  list: (page: number, pageSize: number) => ["stakeholder-projects", "list", page, pageSize] as const,
  detail: (projectId: string) => ["stakeholder-projects", "detail", projectId] as const,
};

export const stakeholderApi = {
  list: <T>(params?: Record<string, unknown>) => api.get<T>(endpoints.stakeholderProjects.root, params as never),
  detail: <T>(projectId: string) => api.get<T>(endpoints.stakeholderProjects.detail(projectId)),
};
