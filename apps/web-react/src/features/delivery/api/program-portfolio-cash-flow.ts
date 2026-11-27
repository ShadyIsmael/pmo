import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";

export type CashFlowGranularity = "Day" | "Month";
export type CashFlowDataQualityStatus = "Available" | "Partial" | "Unavailable";
export type CashFlowMeasure = "PlannedResidual" | "CommittedUnpaid" | "ActualGross" | "ExecutedAdjustment";

export interface CashFlowQualityIssue {
  code: string;
  severity: CashFlowDataQualityStatus;
  sourceType: string | null;
  sourceId: string | null;
  installmentId: string | null;
  paymentRequestId: string | null;
  paymentObligationId: string | null;
  currency: string | null;
}

export interface CashFlowLine {
  measure: CashFlowMeasure;
  sourceType: string;
  sourceId: string;
  installmentId: string | null;
  paymentRequestId: string | null;
  paymentObligationId: string | null;
  originalSettlementId: string | null;
  description: string | null;
  status: string | null;
  eventDate: string | null;
  expectedDate: string | null;
  timingVarianceDays: number | null;
  amount: number | null;
  currency: string | null;
  qualityCodes: string[];
}

export interface CashFlowBucket {
  periodStart: string;
  plannedResidual: number | null;
  committedUnpaid: number | null;
  actualGross: number | null;
  executedAdjustments: number | null;
  actualNet: number | null;
  items: CashFlowLine[];
}

export interface CashFlowUndatedTotals {
  plannedResidual: number | null;
  committedUnpaid: number | null;
  actualGross: number | null;
  executedAdjustments: number | null;
  actualNet: number | null;
  items: CashFlowLine[];
}

export interface ProjectCashFlowCurrencyGroup {
  currency: string | null;
  dataQualityStatus: CashFlowDataQualityStatus;
  buckets: CashFlowBucket[];
  undated: CashFlowUndatedTotals;
}

export interface ProjectCashFlow {
  projectId: string;
  periodFrom: string | null;
  periodTo: string | null;
  granularity: CashFlowGranularity;
  dataQualityStatus: CashFlowDataQualityStatus;
  dataQualityIssues: CashFlowQualityIssue[];
  currencyGroups: ProjectCashFlowCurrencyGroup[];
}

export interface ProgramPortfolioCashFlowItem {
  projectId: string;
  projectPublicId: string;
  projectName: string;
  programId: string | null;
  programName: string | null;
  isDirectProject: boolean;
  line: CashFlowLine;
}

export interface ProgramPortfolioCashFlowBucket {
  periodStart: string;
  plannedResidual: number | null;
  committedUnpaid: number | null;
  actualGross: number | null;
  executedAdjustments: number | null;
  actualNet: number | null;
  dataQualityStatus: CashFlowDataQualityStatus;
  qualityCodes: string[];
  items: ProgramPortfolioCashFlowItem[];
}

export interface ProgramPortfolioCashFlowUndated {
  plannedResidual: number | null;
  committedUnpaid: number | null;
  actualGross: number | null;
  executedAdjustments: number | null;
  actualNet: number | null;
  dataQualityStatus: CashFlowDataQualityStatus;
  qualityCodes: string[];
  items: ProgramPortfolioCashFlowItem[];
}

export interface ProgramPortfolioCashFlowCurrencyGroup {
  currency: string | null;
  contributingProjectCount: number;
  dataQualityStatus: CashFlowDataQualityStatus;
  buckets: ProgramPortfolioCashFlowBucket[];
  undated: ProgramPortfolioCashFlowUndated;
  qualityCodes: string[];
}

export interface ProgramPortfolioCashFlowProjectContribution {
  projectId: string;
  projectPublicId: string;
  projectName: string;
  currency: string | null;
  programId: string | null;
  programName: string | null;
  isDirectProject: boolean;
  cashFlow: ProjectCashFlow;
}

export interface ProgramPortfolioCashFlowQualityIssue {
  projectId: string;
  projectName: string;
  issue: CashFlowQualityIssue;
}

export interface ProgramPortfolioCashFlow {
  entityType: "Program" | "Portfolio";
  entityId: string;
  periodFrom: string | null;
  periodTo: string | null;
  granularity: CashFlowGranularity;
  dataQualityStatus: CashFlowDataQualityStatus;
  partialCoverage: boolean;
  includedProjectCount: number;
  projectsWithUnavailableData: number;
  hasMixedCurrencies: boolean;
  currencyGroups: ProgramPortfolioCashFlowCurrencyGroup[];
  projects: ProgramPortfolioCashFlowProjectContribution[];
  dataQualityIssues: ProgramPortfolioCashFlowQualityIssue[];
}

export interface ProgramPortfolioCashFlowRequest {
  from: string;
  to: string;
  granularity: CashFlowGranularity;
}

function endpoint(entityType: "Program" | "Portfolio", entityId: string): string {
  const id = encodeURIComponent(entityId);
  return entityType === "Program"
    ? `/api/program-management/programs/${id}/cash-flow`
    : `/api/portfolio-management/portfolios/${id}/cash-flow`;
}

export function useProgramPortfolioCashFlow(
  entityType: "Program" | "Portfolio",
  entityId: string,
  params: ProgramPortfolioCashFlowRequest,
  enabled = true,
  scopeKey = "",
) {
  return useQuery({
    queryKey: [entityType === "Program" ? "program-management" : "portfolio-management", "cash-flow", scopeKey, entityId, params],
    queryFn: () => api.get<ProgramPortfolioCashFlow>(endpoint(entityType, entityId), {
      from: params.from,
      to: params.to,
      granularity: params.granularity,
    }),
    enabled: Boolean(entityId) && enabled,
    staleTime: 30_000,
  });
}
