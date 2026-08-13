"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, api, endpoints } from "@/lib/api";

export interface CurrencyDto {
  id: string;
  code: string;
  name: string;
  symbol: string;
  exchangeRate: number;
  isBase: boolean;
}

export interface SaveCurrencyDto {
  code: string;
  name: string;
  symbol: string;
  isBase: boolean;
}

export const currencyQueryKeys = {
  all: () => ["reference-data", "currencies"] as const,
};

function shouldRetryCurrencyQuery(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
  return failureCount < 1;
}

export function useCurrencies() {
  return useQuery({
    queryKey: currencyQueryKeys.all(),
    queryFn: () => api.get<CurrencyDto[]>(endpoints.currencies.root),
    staleTime: 300_000,
    refetchOnWindowFocus: false,
    retry: shouldRetryCurrencyQuery,
  });
}

export function useCreateCurrency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: SaveCurrencyDto) => api.post<CurrencyDto>(endpoints.currencies.root, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: currencyQueryKeys.all() }),
  });
}

export function useUpdateCurrency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: SaveCurrencyDto }) => api.put<CurrencyDto>(endpoints.currencies.detail(id), data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: currencyQueryKeys.all() }),
  });
}

export function useDeleteCurrency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.currencies.detail(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: currencyQueryKeys.all() }),
  });
}
