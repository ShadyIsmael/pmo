import type { ApiClient } from "./client";
import type { Meeting, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createMeetingsApi(client: ApiClient) {
  return {
    getMeetings: (projectId: string, params?: PaginationParams & { status?: string; meetingType?: string }) =>
      client.getPaged<Meeting>(`/projects/${projectId}/meetings`, params),

    getMeeting: (id: string) => client.get<Meeting>(`/meetings/${id}`),

    createMeeting: (projectId: string, data: Partial<Meeting>) =>
      client.post<Meeting>(`/projects/${projectId}/meetings`, data),
  };
}
