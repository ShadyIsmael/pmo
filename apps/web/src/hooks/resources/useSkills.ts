"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { skillKeys } from "@/lib/api/resources";

export interface SkillDto {
  id: string;
  name: string;
  category?: string;
  description?: string;
  isActive: boolean;
}

async function fetchSkills(search?: string): Promise<SkillDto[]> {
  const data = await api.get<{ items: SkillDto[] }>(endpoints.skills, { pageSize: 200, search });
  return data.items ?? [];
}

export function useSkills(search?: string) {
  return useQuery({
    queryKey: skillKeys.list(search),
    queryFn: () => fetchSkills(search),
    staleTime: 60_000,
  });
}
