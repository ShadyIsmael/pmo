import type { ApiClient } from "./client";
import type { AIPlanRequest, AIPlanResponse } from "@nexus/shared-types";

export function createAiPlanningApi(client: ApiClient) {
  return {
    generatePlan: (data: AIPlanRequest) =>
      client.post<AIPlanResponse>("/ai/plan/generate", data),

    reviewPlan: (planId: string, feedback: string) =>
      client.post<AIPlanResponse>(`/ai/plan/${planId}/review`, { feedback }),

    regeneratePlan: (planId: string, data: Partial<AIPlanRequest>) =>
      client.post<AIPlanResponse>(`/ai/plan/${planId}/regenerate`, data),
  };
}
