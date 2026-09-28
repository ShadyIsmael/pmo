"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/auth";
import { projectKeys } from "@/lib/api/projects";
import {
  approveProjectChangeRequest,
  cancelProjectChangeRequest,
  createProjectChangeRequest,
  getPendingProjectChangeRequests,
  getProjectChangeRequest,
  getProjectChangeRequests,
  projectChangeRequestKeys,
  rejectProjectChangeRequest,
  type ChangeRequestDecisionDto,
  type CreateProjectChangeRequestDto,
  type ProjectChangeRequestDto,
} from "@/lib/api/project-change-requests";

function invalidateChangeRequestQueries(queryClient: ReturnType<typeof useQueryClient>, request: ProjectChangeRequestDto) {
  queryClient.invalidateQueries({ queryKey: projectChangeRequestKeys.pending });
  queryClient.invalidateQueries({ queryKey: projectChangeRequestKeys.detail(request.id) });
  queryClient.invalidateQueries({ queryKey: projectChangeRequestKeys.byProject(request.projectId) });
  queryClient.invalidateQueries({ queryKey: projectKeys.detail(request.projectId), exact: true });
}

export function useProjectChangeRequests(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectChangeRequestKeys.byProject(projectId),
    queryFn: () => getProjectChangeRequests(projectId),
    enabled: !!projectId && enabled,
    staleTime: 30_000,
  });
}

export function useProjectChangeRequest(id: string) {
  return useQuery({
    queryKey: projectChangeRequestKeys.detail(id),
    queryFn: () => getProjectChangeRequest(id),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (dto: CreateProjectChangeRequestDto) => createProjectChangeRequest(projectId, dto),
    onSuccess: (created: ProjectChangeRequestDto) => {
      queryClient.invalidateQueries({ queryKey: projectChangeRequestKeys.byProject(projectId) });
      queryClient.invalidateQueries({ queryKey: projectChangeRequestKeys.detail(created.id) });
    },
  });
}

export function usePendingProjectChangeRequests() {
  const { user } = useAuth();
  return useQuery({
    queryKey: projectChangeRequestKeys.pending,
    queryFn: getPendingProjectChangeRequests,
    enabled: !!user,
    staleTime: 15_000,
  });
}

export function useApproveProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, dto }: { id: string; dto: ChangeRequestDecisionDto }) =>
      approveProjectChangeRequest(id, dto),
    onSuccess: (request) => {
      invalidateChangeRequestQueries(queryClient, request);
    },
  });
}

export function useRejectProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, decisionNotes }: { id: string; decisionNotes: string }) =>
      rejectProjectChangeRequest(id, { decisionNotes }),
    onSuccess: (request) => {
      invalidateChangeRequestQueries(queryClient, request);
    },
  });
}

export function useCancelProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => cancelProjectChangeRequest(id),
    onSuccess: (request) => {
      invalidateChangeRequestQueries(queryClient, request);
    },
  });
}
