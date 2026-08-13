"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";

export interface ProgramDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
  portfolioId: string;
  portfolio?: { id: string; name: string };
  status?: string;
  programManagerId?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  actualSpend?: number;
  health?: string;
}

export function useProgram(id: string) {
  return useQuery({ queryKey: ["programs", "detail", id], queryFn: () => api.get<ProgramDto>(endpoints.programDetail(id)), enabled: !!id, staleTime: 30_000 });
}
