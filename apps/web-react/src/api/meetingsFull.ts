import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

type PagedResult<T> = { items: T[]; totalCount: number; page?: number; pageSize?: number; totalPages?: number };
export interface MeetingListDto { id: string; title: string; projectId?: string | null; projectName?: string | null; organizerId?: string | null; organizerName?: string | null; startTime: string; endTime: string; status?: string | null; attendeeCount: number; minutesState?: "Draft" | "Final" | null; }
export interface MeetingAttendeeDto { id: string; meetingId: string; userId?: string | null; displayName?: string | null; email: string; isInternalUser: boolean; }
export interface MeetingDetailDto extends MeetingListDto { objective?: string | null; location?: string | null; meetingLink?: string | null; attendees: readonly MeetingAttendeeDto[]; rowVersion: string; }
export interface MeetingMinutesDto { id: string; meetingId: string; summary?: string | null; discussionNotes?: string | null; decisions?: string | null; outcomes?: string | null; state: "Draft" | "Final"; recordedBy?: string | null; recordedAt: string; rowVersion: string; }
export interface ChecklistItemDto { id: string; meetingMinutesId: string; title: string; isCompleted: boolean; sortOrder: number; }
export interface MeetingAttachmentDto { id: string; meetingMinutesId: string; fileName: string; contentType: string; fileSize: number; createdAt: string; }
export interface MeetingCommentDto { id: string; meetingMinutesId: string; parentCommentId?: string | null; commentText: string; createdBy: string; authorName?: string | null; createdAt: string; }
export interface MeetingInput { title: string; objective?: string; projectId?: string; startTime: string; endTime: string; location?: string; meetingLink?: string; organizerId: string; attendees: readonly { userId?: string; email?: string; displayName?: string }[]; }

export const meetingKeys = { all: ["meetings"] as const, list: (params: Record<string, unknown>) => ["meetings", "list", params] as const, detail: (id: string) => ["meetings", "detail", id] as const, minutes: (id: string) => ["meetings", "minutes", id] as const, checklist: (id: string) => ["meetings", "minutes", id, "checklist"] as const, attachments: (id: string) => ["meetings", "minutes", id, "attachments"] as const, comments: (id: string) => ["meetings", "minutes", id, "comments"] as const };
export const meetingsApiFull = {
  list: (params: Record<string, string | number | boolean | undefined>) => api.get<PagedResult<MeetingListDto>>(endpoints.meetings.root, params),
  detail: (id: string) => api.get<MeetingDetailDto>(endpoints.meetings.detail(id)),
  create: (body: MeetingInput) => api.post<MeetingDetailDto>(endpoints.meetings.root, body),
  update: (id: string, body: MeetingInput & { rowVersion?: string; status?: string }) => api.put<MeetingDetailDto>(endpoints.meetings.detail(id), body),
  minutes: (id: string) => api.get<MeetingMinutesDto | null>(endpoints.meetings.minutes(id)),
  createMinutes: (id: string, body: Record<string, string>) => api.post<MeetingMinutesDto>(endpoints.meetings.minutes(id), body),
  updateMinutes: (id: string, body: Record<string, string | undefined>) => api.put<MeetingMinutesDto>(endpoints.meetings.minutes(id), body),
  finalizeMinutes: (id: string, rowVersion?: string) => api.post<MeetingMinutesDto>(endpoints.meetings.finalizeMinutes(id), rowVersion ? { rowVersion } : {}),
  checklist: (id: string) => api.get<ChecklistItemDto[]>(endpoints.meetings.checklist(id)),
  createChecklist: (id: string, title: string) => api.post<ChecklistItemDto>(endpoints.meetings.checklist(id), { title }),
  updateChecklist: (meetingId: string, itemId: string, body: { title: string; isCompleted: boolean }) => api.put<ChecklistItemDto>(endpoints.meetings.checklistItem(meetingId, itemId), body),
  deleteChecklist: (meetingId: string, itemId: string) => api.delete<void>(endpoints.meetings.checklistItem(meetingId, itemId)),
  reorderChecklist: (id: string, itemIds: string[]) => api.put<ChecklistItemDto[]>(endpoints.meetings.checklistReorder(id), { itemIds }),
  attachments: (id: string) => api.get<MeetingAttachmentDto[]>(endpoints.meetings.attachments(id)),
  uploadAttachments: (id: string, files: File[]) => { const form = new FormData(); files.forEach((file) => form.append("files", file, file.name)); return api.upload<MeetingAttachmentDto[]>(endpoints.meetings.attachments(id), form); },
  downloadAttachment: (meetingId: string, attachmentId: string) => api.download<Blob>(endpoints.meetings.attachmentContent(meetingId, attachmentId)),
  deleteAttachment: (meetingId: string, attachmentId: string) => api.delete<void>(endpoints.meetings.attachment(meetingId, attachmentId)),
  comments: (id: string) => api.get<MeetingCommentDto[]>(endpoints.meetings.comments(id)),
  addComment: (id: string, body: { commentText: string; parentCommentId?: string }) => api.post<MeetingCommentDto>(endpoints.meetings.comments(id), body),
};

export function useFullMeetings(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) { return useQuery({ queryKey: meetingKeys.list(params), queryFn: () => meetingsApiFull.list(params), enabled, staleTime: 30_000 }); }
export function useFullMeeting(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.detail(id ?? ""), queryFn: () => meetingsApiFull.detail(id!), enabled: Boolean(id) && enabled, staleTime: 30_000 }); }
export function useMeetingMinutes(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.minutes(id ?? ""), queryFn: () => meetingsApiFull.minutes(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingMinutesChecklist(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.checklist(id ?? ""), queryFn: () => meetingsApiFull.checklist(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingMinutesAttachments(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.attachments(id ?? ""), queryFn: () => meetingsApiFull.attachments(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingMinutesComments(id?: string, enabled = true) { return useQuery({ queryKey: meetingKeys.comments(id ?? ""), queryFn: () => meetingsApiFull.comments(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useMeetingFullMutations(id?: string) {
  const client = useQueryClient();
  const invalidate = () => { void client.invalidateQueries({ queryKey: meetingKeys.all }); if (id) void client.invalidateQueries({ queryKey: meetingKeys.detail(id) }); };
  return {
    create: useMutation({ mutationFn: meetingsApiFull.create, onSuccess: invalidate }),
    update: useMutation({ mutationFn: ({ body }: { body: MeetingInput & { rowVersion?: string; status?: string } }) => meetingsApiFull.update(id!, body), onSuccess: invalidate }),
    createMinutes: useMutation({ mutationFn: (body: Record<string, string>) => meetingsApiFull.createMinutes(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.minutes(id ?? "") }) }),
    updateMinutes: useMutation({ mutationFn: (body: Record<string, string | undefined>) => meetingsApiFull.updateMinutes(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.minutes(id ?? "") }) }),
    finalizeMinutes: useMutation({ mutationFn: (rowVersion?: string) => meetingsApiFull.finalizeMinutes(id!, rowVersion), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.minutes(id ?? "") }) }),
    createChecklist: useMutation({ mutationFn: (title: string) => meetingsApiFull.createChecklist(id!, title), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    updateChecklist: useMutation({ mutationFn: ({ itemId, body }: { itemId: string; body: { title: string; isCompleted: boolean } }) => meetingsApiFull.updateChecklist(id!, itemId, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    deleteChecklist: useMutation({ mutationFn: (itemId: string) => meetingsApiFull.deleteChecklist(id!, itemId), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    reorderChecklist: useMutation({ mutationFn: (itemIds: string[]) => meetingsApiFull.reorderChecklist(id!, itemIds), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.checklist(id ?? "") }) }),
    uploadAttachments: useMutation({ mutationFn: (files: File[]) => meetingsApiFull.uploadAttachments(id!, files), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.attachments(id ?? "") }) }),
    deleteAttachment: useMutation({ mutationFn: (attachmentId: string) => meetingsApiFull.deleteAttachment(id!, attachmentId), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.attachments(id ?? "") }) }),
    addComment: useMutation({ mutationFn: (body: { commentText: string; parentCommentId?: string }) => meetingsApiFull.addComment(id!, body), onSuccess: () => client.invalidateQueries({ queryKey: meetingKeys.comments(id ?? "") }) }),
  };
}
