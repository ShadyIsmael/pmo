import { useI18n } from "@i18n/messages";

interface BudgetData {
  approved: number;
  actual: number;
  currency: string;
}

export function BudgetOverviewWidget({ data }: { data?: BudgetData }) {
  const { t, formatCurrency } = useI18n();

  if (!data) {
    return <div className="py-8 text-center text-sm text-muted-foreground">{t("dashboard.noFinancialData")}</div>;
  }

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <div className="rounded-lg bg-muted/30 p-3">
          <p className="text-xs text-muted-foreground">{t("dashboard.totalBudget")}</p>
          <p className="text-lg font-semibold tabular-nums">{formatCurrency(data.approved, data.currency)}</p>
        </div>
        <div className="rounded-lg bg-muted/30 p-3">
          <p className="text-xs text-muted-foreground">{t("costs.totalActualCost")}</p>
          <p className="text-lg font-semibold tabular-nums">{formatCurrency(data.actual, data.currency)}</p>
        </div>
      </div>
    </div>
  );
}
