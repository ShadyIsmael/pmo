"use client";

import React from "react";
import { useLocale, useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { RefreshCw, AlertTriangle, DollarSign } from "lucide-react";
import { useCostEntries, type CostEntryDto } from "@/lib/api/costs";
import { formatCurrency, formatDate } from "@/lib/utils";
import { TablePagination } from "@/components/ui/table-pagination";
import { getLookupTextClass } from "@/lib/status-presentation";

type CurrencyTotals = {
  currency: string;
  amount: number;
  tax: number;
};

const costStatusKeys: Record<string, string> = {
  draft: "draft",
  posted: "posted",
  verified: "verified",
  cancelled: "cancelled",
};

function groupCurrencyTotals(costs: readonly CostEntryDto[]): CurrencyTotals[] {
  const totals = new Map<string, CurrencyTotals>();

  for (const cost of costs) {
    const currency = cost.currency?.trim().toUpperCase() ?? "";
    const key = currency || "__unknown__";
    const current = totals.get(key) ?? { currency, amount: 0, tax: 0 };
    current.amount += cost.amount;
    current.tax += cost.taxAmount ?? 0;
    totals.set(key, current);
  }

  return Array.from(totals.values());
}

function formatAmount(amount: number, currency: string, locale: string, unavailable: string): string {
  if (!currency) return unavailable;
  try {
    return formatCurrency(amount, currency, locale);
  } catch {
    return unavailable;
  }
}

function displayText(value: string | null | undefined, unavailable: string): string {
  const trimmed = value?.trim();
  return trimmed || unavailable;
}

function costStatusLabel(
  tActuals: ReturnType<typeof useTranslations>,
  status: string | null | undefined,
  unavailable: string,
): string {
  const trimmed = status?.trim();
  if (!trimmed) return unavailable;
  const key = costStatusKeys[trimmed.toLowerCase()];
  return key ? tActuals(`costStatuses.${key}` as never) : trimmed;
}

export default function ActualsPage() {
  const t = useTranslations("costs");
  const tActuals = useTranslations("costs.actuals");
  const locale = useLocale();
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const [page, setPage] = React.useState(1);
  const pageSize = 20;

  const costQuery = useCostEntries({ page, pageSize });
  const costs = costQuery.data?.items ?? [];
  const totalPages = costQuery.data?.totalPages ?? 0;
  const totalCount = costQuery.data?.totalCount ?? 0;
  const currencyTotals = groupCurrencyTotals(costs);

  return (
    <div className="space-y-6">
      <PageHeader title={tActuals("title")} description={tActuals("description")} />

      {costQuery.isLoading ? (
        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <Card key={i}>
                <CardHeader className="pb-2"><Skeleton className="h-4 w-24" /></CardHeader>
                <CardContent><Skeleton className="h-8 w-20" /></CardContent>
              </Card>
            ))}
          </div>
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full" />
          ))}
        </div>
      ) : costQuery.isError ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
            <AlertTriangle className="h-10 w-10 text-destructive" />
            <h2 className="font-medium">{t("loadFailed")}</h2>
            <button onClick={() => void costQuery.refetch()} className="inline-flex items-center gap-2 text-sm text-primary hover:underline">
              <RefreshCw className="h-4 w-4" />{t("retry")}
            </button>
          </CardContent>
        </Card>
      ) : costs.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
            <DollarSign className="h-10 w-10 text-muted-foreground" />
            <h2 className="font-medium">{t("noData")}</h2>
          </CardContent>
        </Card>
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {currencyTotals.map(({ currency, amount, tax }) => (
              <Card key={currency || "unknown"}>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium text-muted-foreground">
                    {currency || tActuals("unavailable")}
                  </CardTitle>
                  <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-blue">
                    <DollarSign className="h-4 w-4 text-fin-blue" />
                  </span>
                </CardHeader>
                <CardContent className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <p className="text-xs text-muted-foreground">{tActuals("pageActualCost")}</p>
                    <div className="text-xl font-bold text-fin-blue">{formatAmount(amount, currency, dateLocale, tActuals("unavailable"))}</div>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">{tActuals("pageTax")}</p>
                    <div className="text-xl font-bold">{formatAmount(tax, currency, dateLocale, tActuals("unavailable"))}</div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          <div className="nexus-table-surface">
            <div className="nexus-table-header grid grid-cols-[repeat(14,minmax(0,1fr))] gap-2">
              <div className="col-span-1">{tActuals("date")}</div>
              <div className="col-span-2">{tActuals("project")}</div>
              <div className="col-span-1">{tActuals("category")}</div>
              <div className="col-span-2">{tActuals("description_label")}</div>
              <div className="col-span-2">{tActuals("vendor")}</div>
              <div className="col-span-1">{tActuals("reference")}</div>
              <div className="col-span-1">{tActuals("amount")}</div>
              <div className="col-span-1">{tActuals("taxAmount")}</div>
              <div className="col-span-1">{tActuals("status")}</div>
              <div className="col-span-1">{tActuals("type")}</div>
              <div className="col-span-1">{tActuals("currency")}</div>
            </div>
            {costs.map((cost) => (
              <CostRow
                key={cost.id}
                cost={cost}
                unavailable={tActuals("unavailable")}
                dateLocale={dateLocale}
                statusLabel={costStatusLabel(tActuals, cost.status, tActuals("unavailable"))}
              />
            ))}
          </div>

          <TablePagination
            page={page}
            totalPages={totalPages}
            summary={t("pageSummary", { page, totalPages, total: totalCount })}
            previousLabel={t("previous")}
            nextLabel={t("next")}
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  );
}

function CostRow({
  cost,
  unavailable,
  dateLocale,
  statusLabel,
}: {
  cost: CostEntryDto;
  unavailable: string;
  dateLocale: string;
  statusLabel: string;
}) {
  return (
    <div className="nexus-table-row grid grid-cols-[repeat(14,minmax(0,1fr))] gap-2 px-4 py-3 text-sm">
      <div className="col-span-1 text-muted-foreground">{formatDate(cost.incurredDate, dateLocale)}</div>
      <div className="col-span-2 truncate">{displayText(cost.projectId ? `${cost.projectId.slice(0, 8)}...` : undefined, unavailable)}</div>
      <div className={`col-span-1 truncate ${getLookupTextClass(cost.category)}`}>{displayText(cost.category, unavailable)}</div>
      <div className="col-span-2 truncate">{displayText(cost.description, unavailable)}</div>
      <div className="col-span-2 truncate">{displayText(cost.vendorName, unavailable)}</div>
      <div className="col-span-1 truncate">{displayText(cost.reference, unavailable)}</div>
      <div className="col-span-1 font-semibold text-fin-blue">{formatAmount(cost.amount, cost.currency?.trim().toUpperCase() ?? "", dateLocale, unavailable)}</div>
      <div className="col-span-1">{cost.taxAmount != null ? formatAmount(cost.taxAmount, cost.currency?.trim().toUpperCase() ?? "", dateLocale, unavailable) : unavailable}</div>
      <div className="col-span-1"><Badge status={cost.status}>{statusLabel}</Badge></div>
      <div className={`col-span-1 truncate ${getLookupTextClass(cost.type)}`}>{displayText(cost.type, unavailable)}</div>
      <div className={`col-span-1 ${getLookupTextClass(cost.currency)}`}>{displayText(cost.currency?.trim().toUpperCase(), unavailable)}</div>
    </div>
  );
}
