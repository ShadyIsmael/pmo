import { api, type PagedResult } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";
import type { MeetingReminderOffset, MeetingStatus } from "@/lib/enums";

export type { MeetingStatus } from "@/lib/enums";

export interface MeetingListDto {
  id: string;
  title: string;
  projectId?: string | null;
  projectName?: string | null;
  organizerId?: string | null;
  organizerName?: string | null;
  startTime: string;
  endTime: string;
  status?: MeetingStatus | null;
  attendeeCount: number;
  minutesState?: "Draft" | "Final" | null;
  reminderOffsetMinutes?: MeetingReminderOffset;
}

export interface MeetingAttendeeDto {
  id: string;
  meetingId: string;
  userId?: string | null;
  displayName?: string | null;
  email: string;
  isInternalUser: boolean;
}

export interface MeetingAttendeeInput {
  userId?: string;
  email?: string;
  displayName?: string;
}

export interface MeetingDetailDto {
  id: string;
  title: string;
  objective?: string | null;
  projectId?: string | null;
  projectName?: string | null;
  organizerId?: string | null;
  organizerName?: string | null;
  startTime: string;
  endTime: string;
  location?: string | null;
  meetingLink?: string | null;
  status?: MeetingStatus | null;
  attendees: readonly MeetingAttendeeDto[];
  minutesState?: "Draft" | "Final" | null;
  reminderOffsetMinutes?: MeetingReminderOffset;
  rowVersion: string;
}

export interface MeetingMinutesDto {
  id: string;
  meetingId: string;
  summary?: string | null;
  discussionNotes?: string | null;
  decisions?: string | null;
  outcomes?: string | null;
  state: "Draft" | "Final";
  recordedBy?: string | null;
  recordedAt: string;
  rowVersion: string;
}

export interface CreateMeetingMinutesRequest {
  summary?: string;
  discussionNotes?: string;
  decisions?: string;
  outcomes?: string;
}

export interface UpdateMeetingMinutesRequest extends CreateMeetingMinutesRequest {
  rowVersion?: string;
}

export interface MeetingMinutesChecklistItemDto {
  id: string;
  meetingMinutesId: string;
  title: string;
  isCompleted: boolean;
  sortOrder: number;
}

export interface CreateMeetingMinutesChecklistItemRequest {
  title: string;
}

export interface UpdateMeetingMinutesChecklistItemRequest {
  title: string;
  isCompleted: boolean;
}

export interface ReorderMeetingMinutesChecklistRequest {
  itemIds: string[];
}

export interface MeetingMinutesAttachmentDto {
  id: string;
  meetingMinutesId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  createdAt: string;
}

export interface MeetingMinutesCommentDto {
  id: string;
  meetingMinutesId: string;
  parentCommentId?: string | null;
  commentText: string;
  createdBy: string;
  authorName?: string | null;
  createdAt: string;
}

export interface CreateMeetingMinutesCommentRequest {
  commentText: string;
  parentCommentId?: string | null;
}

export interface CreateMeetingRequest {
  title: string;
  objective?: string;
  projectId?: string;
  startTime: string;
  endTime: string;
  location?: string;
  meetingLink?: string;
  organizerId: string;
  attendees: readonly MeetingAttendeeInput[];
  reminderOffsetMinutes?: MeetingReminderOffset;
}

export interface UpdateMeetingRequest extends CreateMeetingRequest {
  rowVersion?: string;
  status?: MeetingStatus;
}

export interface MeetingFilters {
  page: number;
  pageSize: number;
  search?: string;
  status?: MeetingStatus;
  projectId?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const meetingKeys = {
  all: ["meetings"] as const,
  lists: () => [...meetingKeys.all, "list"] as const,
  list: (filters: MeetingFilters) => [...meetingKeys.lists(), filters] as const,
  detail: (id: string) => [...meetingKeys.all, "detail", id] as const,
  attendees: (id: string) => [...meetingKeys.detail(id), "attendees"] as const,
  minutes: (id: string) => [...meetingKeys.detail(id), "minutes"] as const,
  checklist: (id: string) => [...meetingKeys.minutes(id), "checklist"] as const,
  attachments: (id: string) => [...meetingKeys.minutes(id), "attachments"] as const,
  comments: (id: string) => [...meetingKeys.minutes(id), "comments"] as const,
};

export const meetingApi = {
  list: (filters: MeetingFilters) => api.get<PagedResult<MeetingListDto>>(endpoints.meetings.root, { ...filters }),
  detail: (id: string) => api.get<MeetingDetailDto>(endpoints.meetings.detail(id)),
  create: (body: CreateMeetingRequest) => api.post<MeetingDetailDto>(endpoints.meetings.root, body),
  update: (id: string, body: UpdateMeetingRequest) => api.put<MeetingDetailDto>(endpoints.meetings.detail(id), body),
  remove: (id: string) => api.delete<void>(endpoints.meetings.detail(id)),
  attendees: (id: string) => api.get<readonly MeetingAttendeeDto[]>(endpoints.meetings.attendees(id)),
  minutes: async (id: string) => (await api.get<MeetingMinutesDto | null>(endpoints.meetings.minutes(id))) ?? null,
  createMinutes: (id: string, body: CreateMeetingMinutesRequest) => api.post<MeetingMinutesDto>(endpoints.meetings.minutes(id), body),
  updateMinutes: (id: string, body: UpdateMeetingMinutesRequest) => api.put<MeetingMinutesDto>(endpoints.meetings.minutes(id), body),
  finalizeMinutes: (id: string, rowVersion?: string) => api.post<MeetingMinutesDto>(endpoints.meetings.finalizeMinutes(id), rowVersion ? { rowVersion } : {}),
  checklist: (id: string) => api.get<MeetingMinutesChecklistItemDto[]>(endpoints.meetings.checklist(id)),
  createChecklistItem: (id: string, body: CreateMeetingMinutesChecklistItemRequest) => api.post<MeetingMinutesChecklistItemDto>(endpoints.meetings.checklist(id), body),
  updateChecklistItem: (meetingId: string, itemId: string, body: UpdateMeetingMinutesChecklistItemRequest) => api.put<MeetingMinutesChecklistItemDto>(endpoints.meetings.checklistItem(meetingId, itemId), body),
  deleteChecklistItem: (meetingId: string, itemId: string) => api.delete<void>(endpoints.meetings.checklistItem(meetingId, itemId)),
  reorderChecklist: (id: string, body: ReorderMeetingMinutesChecklistRequest) => api.put<MeetingMinutesChecklistItemDto[]>(endpoints.meetings.checklistReorder(id), body),
  attachments: (id: string) => api.get<MeetingMinutesAttachmentDto[]>(endpoints.meetings.attachments(id)),
  uploadAttachments: (id: string, files: File[]) => { const form = new FormData(); files.forEach((file) => form.append("files", file, file.name)); return api.upload<MeetingMinutesAttachmentDto[]>(endpoints.meetings.attachments(id), form); },
  downloadAttachment: (meetingId: string, attachmentId: string) => api.download<Blob>(endpoints.meetings.attachmentContent(meetingId, attachmentId)),
  deleteAttachment: (meetingId: string, attachmentId: string) => api.delete<void>(endpoints.meetings.attachment(meetingId, attachmentId)),
  comments: (id: string) => api.get<MeetingMinutesCommentDto[]>(endpoints.meetings.comments(id)),
  addComment: (id: string, body: CreateMeetingMinutesCommentRequest) => api.post<MeetingMinutesCommentDto>(endpoints.meetings.comments(id), body),
  addAttendee: (meetingId: string, attendee: MeetingAttendeeInput) => api.post<MeetingAttendeeDto>(endpoints.meetings.attendees(meetingId), attendee),
  removeAttendee: (meetingId: string, participantId: string) => api.delete<void>(endpoints.meetings.attendee(meetingId, participantId)),
};
