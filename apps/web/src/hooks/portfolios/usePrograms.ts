"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";

export interface ProgramDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
  portfolioId: string;
  portfolioName?: string;
  currency: string;
  status?: string;
  programManagerId?: string;
  programManagerName?: string;
  programManagerEmail?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  actualSpend?: number;
  health?: string;
  projectCount: number;
  projects?: ProgramProjectDto[];
  byCurrency?: Record<string, ProgramCurrencyFinancialSummary>;
  createdAt: string;
  updatedAt?: string;
  createdBy: string;
  updatedBy?: string;
  rowVersion?: number[];
}

export interface ProgramProjectDto { id: string; name: string; code: string; status: string; currency: string; budget?: number; forecast?: number; actualCost?: number; }
export interface ProgramCurrencyFinancialSummary { currency: string; isBaseCurrency: boolean; projectCount: number; budget: number; forecast: number; actualCost: number; }

export type SaveProgramDto = {
  name: string;
  description?: string;
  portfolioId: string;
  programManagerId?: string;
  status: string;
  startDate?: string;
  endDate?: string;
  currency?: string;
  budget?: number;
  health?: string;
  rowVersion?: number[];
};

export const programKeys = {
  all: ["programs"] as const,
  list: (params: Record<string, unknown>) => ["programs", "list", params] as const,
  detail: (id: string) => ["programs", "detail", id] as const,
};

export function usePrograms(params: { search?: string; status?: string; portfolioId?: string; page?: number; pageSize?: number } = {}, enabled = true) {
  const query = { page: 1, pageSize: 200, ...params };
  return useQuery({
    queryKey: programKeys.list(query),
    queryFn: () => api.get<PagedResult<ProgramDto>>(endpoints.programs.root, query),
    enabled,
    staleTime: 15_000,
  });
}

export function useProgram(id?: string) {
  return useQuery({
    queryKey: programKeys.detail(id ?? ""),
    queryFn: () => api.get<ProgramDto>(endpoints.programs.detail(id!)),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (dto: SaveProgramDto) => api.post<ProgramDto>(endpoints.programs.root, dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: programKeys.all }),
  });
}

export function useUpdateProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: SaveProgramDto }) => api.put<ProgramDto>(endpoints.programs.detail(id), dto),
    onSuccess: () => qc.invalidateQueries({ queryKey: programKeys.all }),
  });
}

export function useArchiveProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(endpoints.programs.archive(id)),
    onSuccess: () => qc.invalidateQueries({ queryKey: programKeys.all }),
  });
}

export function useReactivateProgram() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(endpoints.programs.reactivate(id)),
    onSuccess: () => qc.invalidateQueries({ queryKey: programKeys.all }),
  });
}
