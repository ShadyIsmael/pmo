import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface PaymentSettlementDto {
  id: string;
  paymentObligationId: string;
  idempotencyKey: string;
  amount: number;
  currency: string | null;
  settlementDate: string;
  paymentReference: string;
  notes?: string;
  createdAt: string;
  rowVersion: string;
}

export interface PaymentObligationDto {
  id: string;
  projectId: string;
  projectName: string;
  milestoneId?: string;
  paymentReference: string;
  purpose: string;
  description: string;
  amount: number;
  currency: string;
  dueDate: string;
  notes?: string;
  settledAmount: number;
  outstandingAmount: number;
  settlementProgressStatus: "Not Paid" | "Partially Paid" | "Fully Paid";
  dueDateStatus: "Upcoming" | "Due" | "Overdue" | "Settled";
  lifecycleStatus: string;
  isDue: boolean;
  isOverdue: boolean;
  settlements: PaymentSettlementDto[];
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface CreatePaymentObligationDto {
  projectId: string;
  milestoneId?: string;
  paymentReference: string;
  purpose: string;
  description?: string;
  amount: number;
  currency: string;
  dueDate: string;
  notes?: string;
}

export interface UpdatePaymentObligationDto extends CreatePaymentObligationDto {
  rowVersion: string;
}

export interface RecordPaymentSettlementDto {
  idempotencyKey: string;
  amount: number;
  settlementDate: string;
  paymentReference: string;
  notes?: string;
}

export interface FinancePagedResult<T> {
  items: T[];
  totalCount: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
}

export type FinanceQueryParams = Record<string, string | number | boolean | undefined>;

export const financeKeys = {
  paymentObligations: (params: FinanceQueryParams = {}) => ["payment-obligations", params] as const,
  paymentSettlements: (id: string) => ["payment-obligations", id, "settlements"] as const,
};

export function usePaymentObligations(params: FinanceQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.paymentObligations(params),
    queryFn: () => api.get<FinancePagedResult<PaymentObligationDto>>(
      endpoints.paymentObligations.root,
      { pageSize: 100, ...params },
    ),
    enabled,
    staleTime: 15_000,
  });
}

export function usePaymentSettlements(paymentId: string, enabled = true) {
  return useQuery({
    queryKey: financeKeys.paymentSettlements(paymentId),
    queryFn: () => api.get<FinancePagedResult<PaymentSettlementDto>>(
      endpoints.paymentObligations.settlements(paymentId),
      { pageSize: 100 },
    ),
    enabled: Boolean(paymentId) && enabled,
    staleTime: 15_000,
  });
}

export function usePaymentObligationMutations() {
  const client = useQueryClient();
  const invalidate = async (paymentId?: string) => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ["payment-obligations"] }),
      paymentId
        ? client.invalidateQueries({ queryKey: financeKeys.paymentSettlements(paymentId) })
        : Promise.resolve(),
    ]);
  };

  return {
    create: useMutation({
      mutationFn: (body: CreatePaymentObligationDto) =>
        api.post<PaymentObligationDto>(endpoints.paymentObligations.root, body),
      onSuccess: () => invalidate(),
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: UpdatePaymentObligationDto }) =>
        api.put<PaymentObligationDto>(endpoints.paymentObligations.detail(id), body),
      onSuccess: () => invalidate(),
    }),
    delete: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) =>
        api.delete<void>(endpoints.paymentObligations.detail(id), { rowVersion }),
      onSuccess: () => invalidate(),
    }),
    settle: useMutation({
      mutationFn: ({ id, body }: { id: string; body: RecordPaymentSettlementDto }) =>
        api.post<PaymentSettlementDto>(endpoints.paymentObligations.settlements(id), body),
      onSuccess: (_data, variables) => invalidate(variables.id),
    }),
  };
}
