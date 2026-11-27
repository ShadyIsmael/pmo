import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";

export type CashFlowGranularity = "Day" | "Month";
export type CashFlowDataQualityStatus = "Available" | "Partial" | "Unavailable";
export type CashFlowMeasure = "PlannedResidual" | "CommittedUnpaid" | "ActualGross" | "ExecutedAdjustment";

export type ProjectCashFlowRequest = {
  from: string;
  to: string;
  granularity: CashFlowGranularity;
};

export type CashFlowDataQualityIssue = {
  code: string;
  severity: string;
  sourceType?: string | null;
  sourceId?: string | null;
  currency?: string | null;
};

export type CashFlowLineItem = {
  measure: CashFlowMeasure;
  sourceType: string;
  sourceId: string;
  installmentId?: string | null;
  paymentRequestId?: string | null;
  paymentObligationId?: string | null;
  originalSettlementId?: string | null;
  description?: string | null;
  status?: string | null;
  eventDate?: string | null;
  expectedDate?: string | null;
  timingVarianceDays?: number | null;
  amount?: number | null;
  currency?: string | null;
  qualityCodes: string[];
};

export type CashFlowBucketTotals = {
  plannedResidual: number | null;
  committedUnpaid: number | null;
  actualGross: number | null;
  executedAdjustments: number | null;
  actualNet: number | null;
  items: CashFlowLineItem[];
};

export type CashFlowDateBucket = CashFlowBucketTotals & {
  periodStart: string;
};

export type CashFlowCurrencyGroup = {
  currency: string | null;
  dataQualityStatus: CashFlowDataQualityStatus;
  buckets: CashFlowDateBucket[];
  undated: CashFlowBucketTotals;
};

export type ProjectCashFlowDto = {
  projectId: string;
  periodFrom: string;
  periodTo: string;
  granularity: CashFlowGranularity;
  dataQualityStatus: CashFlowDataQualityStatus;
  dataQualityIssues: CashFlowDataQualityIssue[];
  currencyGroups: CashFlowCurrencyGroup[];
};

const projectCashFlowKey = (projectId: string, request: ProjectCashFlowRequest) =>
  ["project-cash-flow", projectId, request.from, request.to, request.granularity] as const;

export function useProjectCashFlow(
  projectId: string,
  request: ProjectCashFlowRequest,
  enabled = true,
) {
  return useQuery({
    queryKey: projectCashFlowKey(projectId, request),
    queryFn: () => api.get<ProjectCashFlowDto>(
      `/api/projects/${encodeURIComponent(projectId)}/cash-flow`,
      {
        from: request.from,
        to: request.to,
        granularity: request.granularity,
      },
    ),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}
