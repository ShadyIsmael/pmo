import { useOutletContext } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useBudgets, useCosts } from "@features/finance/api/finance";

export function ProjectFinanceTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t, formatCurrency } = useI18n();
  // Visible-first: finance loads only when tab active.
  const budgets = useBudgets({ projectId }, Boolean(projectId));
  const costs = useCosts({ projectId }, Boolean(projectId));
  if (budgets.isLoading || costs.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  return (
    <div className="space-y-4">
      <Card><CardHeader><CardTitle>{t("costs.budgets")}</CardTitle></CardHeader><CardContent className="p-0">
        {(budgets.data?.items ?? []).length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("costs.name")}</TableHead><TableHead>{t("costs.planned")}</TableHead></TableRow></TableHeader>
            <TableBody>{(budgets.data?.items ?? []).map((b) => <TableRow key={b.id}><TableCell className="font-medium">{b.name}</TableCell><TableCell className="tabular-nums">{formatCurrency(b.plannedAmount, b.currency)}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
      <Card><CardHeader><CardTitle>{t("costs.actuals")}</CardTitle></CardHeader><CardContent className="p-0">
        {(costs.data?.items ?? []).length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("costs.description")}</TableHead><TableHead>{t("costs.amount")}</TableHead></TableRow></TableHeader>
            <TableBody>{(costs.data?.items ?? []).map((c) => <TableRow key={c.id}><TableCell className="font-medium">{c.description}</TableCell><TableCell className="tabular-nums">{formatCurrency(c.amount, c.currency)}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}
