"use client";

import { useQuery } from "@tanstack/react-query";
import { type PagedResult } from "@/lib/api";
import { stakeholderApi, stakeholderKeys, type StakeholderProjectView } from "@/lib/api/stakeholder";

export function useMyStakeholderProjects(page = 1, pageSize = 20, enabled = true) {
  return useQuery({
    queryKey: stakeholderKeys.list(page, pageSize),
    queryFn: () => stakeholderApi.list<PagedResult<StakeholderProjectView>>({ page, pageSize }),
    enabled,
    staleTime: 30_000,
  });
}

export function useMyStakeholderProject(projectId: string, enabled = true) {
  return useQuery({
    queryKey: stakeholderKeys.detail(projectId),
    queryFn: () => stakeholderApi.detail<StakeholderProjectView>(projectId),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}
