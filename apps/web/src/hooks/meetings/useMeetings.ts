"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api/client";
import { meetingApi, meetingKeys, type CreateMeetingMinutesChecklistItemRequest, type CreateMeetingMinutesCommentRequest, type CreateMeetingRequest, type CreateMeetingMinutesRequest, type MeetingFilters, type ReorderMeetingMinutesChecklistRequest, type UpdateMeetingMinutesChecklistItemRequest, type UpdateMeetingMinutesRequest, type UpdateMeetingRequest } from "@/lib/api/meetings";

function shouldRetry(failureCount: number, error: Error): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
  return failureCount < 2;
}

export function useMeetings(filters: MeetingFilters, enabled = true) {
  return useQuery({
    queryKey: meetingKeys.list(filters),
    queryFn: () => meetingApi.list(filters),
    enabled,
    retry: shouldRetry,
    staleTime: 30_000,
  });
}

export function useMeeting(id: string) {
  return useQuery({
    queryKey: meetingKeys.detail(id),
    queryFn: () => meetingApi.detail(id),
    enabled: Boolean(id),
    retry: shouldRetry,
    staleTime: 30_000,
  });
}

export function useMeetingAttendees(id: string, enabled = true) {
  return useQuery({
    queryKey: meetingKeys.attendees(id),
    queryFn: () => meetingApi.attendees(id),
    enabled: Boolean(id) && enabled,
    retry: shouldRetry,
    staleTime: 30_000,
  });
}

export function useMeetingMinutes(id: string, enabled = true) {
  return useQuery({
    queryKey: meetingKeys.minutes(id),
    queryFn: () => meetingApi.minutes(id),
    enabled: Boolean(id) && enabled,
    retry: shouldRetry,
    staleTime: 30_000,
  });
}

export function useCreateMeetingMinutes() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: CreateMeetingMinutesRequest }) => meetingApi.createMinutes(id, body),
    retry: false,
    onSuccess: (minutes) => {
      queryClient.setQueryData(meetingKeys.minutes(minutes.meetingId), minutes);
      queryClient.invalidateQueries({ queryKey: meetingKeys.detail(minutes.meetingId), exact: true });
      queryClient.invalidateQueries({ queryKey: meetingKeys.lists() });
    },
  });
}

export function useUpdateMeetingMinutes() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateMeetingMinutesRequest }) => meetingApi.updateMinutes(id, body),
    retry: false,
    onSuccess: (minutes) => {
      queryClient.setQueryData(meetingKeys.minutes(minutes.meetingId), minutes);
      queryClient.invalidateQueries({ queryKey: meetingKeys.detail(minutes.meetingId), exact: true });
      queryClient.invalidateQueries({ queryKey: meetingKeys.lists() });
    },
  });
}

export function useFinalizeMeetingMinutes() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: string }) => meetingApi.finalizeMinutes(id, rowVersion),
    retry: false,
    onSuccess: (minutes) => {
      queryClient.setQueryData(meetingKeys.minutes(minutes.meetingId), minutes);
      queryClient.invalidateQueries({ queryKey: meetingKeys.detail(minutes.meetingId), exact: true });
      queryClient.invalidateQueries({ queryKey: meetingKeys.lists() });
    },
  });
}

export function useMeetingMinutesChecklist(id: string, enabled = true) {
  return useQuery({ queryKey: meetingKeys.checklist(id), queryFn: () => meetingApi.checklist(id), enabled: Boolean(id) && enabled, retry: shouldRetry, staleTime: 30_000 });
}

export function useCreateMeetingMinutesChecklistItem(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateMeetingMinutesChecklistItemRequest) => meetingApi.createChecklistItem(id, body),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.checklist(id), exact: true }),
  });
}

export function useUpdateMeetingMinutesChecklistItem(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ itemId, body }: { itemId: string; body: UpdateMeetingMinutesChecklistItemRequest }) => meetingApi.updateChecklistItem(id, itemId, body),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.checklist(id), exact: true }),
  });
}

export function useDeleteMeetingMinutesChecklistItem(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (itemId: string) => meetingApi.deleteChecklistItem(id, itemId),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.checklist(id), exact: true }),
  });
}

export function useReorderMeetingMinutesChecklist(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ReorderMeetingMinutesChecklistRequest) => meetingApi.reorderChecklist(id, body),
    retry: false,
    onSuccess: (items) => queryClient.setQueryData(meetingKeys.checklist(id), items),
  });
}

export function useMeetingMinutesAttachments(id: string, enabled = true) {
  return useQuery({ queryKey: meetingKeys.attachments(id), queryFn: () => meetingApi.attachments(id), enabled: Boolean(id) && enabled, retry: shouldRetry, staleTime: 30_000 });
}

export function useUploadMeetingMinutesAttachments(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (files: File[]) => meetingApi.uploadAttachments(id, files),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.attachments(id), exact: true }),
  });
}

export function useDeleteMeetingMinutesAttachment(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachmentId: string) => meetingApi.deleteAttachment(id, attachmentId),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.attachments(id), exact: true }),
  });
}

export function useMeetingMinutesComments(id: string, enabled = true) {
  return useQuery({ queryKey: meetingKeys.comments(id), queryFn: () => meetingApi.comments(id), enabled: Boolean(id) && enabled, retry: shouldRetry, staleTime: 30_000 });
}

export function useCreateMeetingMinutesComment(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateMeetingMinutesCommentRequest) => meetingApi.addComment(id, body),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.comments(id), exact: true }),
  });
}

export function useCreateMeeting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateMeetingRequest) => meetingApi.create(body),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: meetingKeys.lists() }),
  });
}

export function useUpdateMeeting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateMeetingRequest }) => meetingApi.update(id, body),
    retry: false,
    onSuccess: (meeting) => {
      queryClient.invalidateQueries({ queryKey: meetingKeys.lists() });
      queryClient.invalidateQueries({ queryKey: meetingKeys.detail(meeting.id), exact: true });
    },
  });
}

export function useDeleteMeeting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => meetingApi.remove(id),
    retry: false,
    onSuccess: (_value, id) => {
      queryClient.invalidateQueries({ queryKey: meetingKeys.lists() });
      queryClient.removeQueries({ queryKey: meetingKeys.detail(id), exact: true });
    },
  });
}
