import * as React from "react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Plus } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { useBudgets, useCosts, useCostSummary, useCreateBudget, useCreateCost } from "./api/finance";

function useTab(): "overview" | "budgets" | "actuals" | "forecasts" {
  const path = window.location.pathname;
  if (path.endsWith("/budgets")) return "budgets";
  if (path.endsWith("/actuals")) return "actuals";
  if (path.endsWith("/forecasts")) return "forecasts";
  return "overview";
}

export function CostsPage() {
  const { t, formatCurrency } = useI18n();
  const { user } = useAuth();
  const canWrite = Boolean(user && hasPermission(user.permissions, Permissions.Finances.Write));
  const tab = useTab();
  const summary = useCostSummary(tab === "overview");
  const budgets = useBudgets({}, tab === "budgets" || tab === "overview");
  const costs = useCosts({}, tab === "actuals" || tab === "overview");
  const createBudget = useCreateBudget();
  const createCost = useCreateCost();
  const [budgetOpen, setBudgetOpen] = React.useState(false);
  const [costOpen, setCostOpen] = React.useState(false);
  const [budgetForm, setBudgetForm] = React.useState({ name: "", category: "", plannedAmount: "", currency: "USD", fiscalYear: new Date().getFullYear() });
  const [costForm, setCostForm] = React.useState({ projectId: "", description: "", amount: "", currency: "USD", incurredDate: new Date().toISOString().slice(0, 10) });

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("costs.title")}
        description={t("costs.description")}
        actions={
          <div className="flex gap-2">
            {canWrite ? <Button size="sm" variant="outline" onClick={() => setBudgetOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("costs.addBudget")}</Button> : null}
            {canWrite ? <Button size="sm" onClick={() => setCostOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("costs.addCost")}</Button> : null}
          </div>
        }
      />
      {tab === "overview" ? (
        <div className="grid gap-3 sm:grid-cols-3">
          <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("costs.totalBudget")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{summary.data ? formatCurrency(summary.data.totalBudget, summary.data.currency) : "—"}</p></CardContent></Card>
          <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("costs.totalActualCost")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{summary.data ? formatCurrency(summary.data.totalActual, summary.data.currency) : "—"}</p></CardContent></Card>
          <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("costs.totalForecast")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{summary.data ? formatCurrency(summary.data.totalForecast, summary.data.currency) : "—"}</p></CardContent></Card>
        </div>
      ) : null}
      {tab === "overview" || tab === "budgets" ? (
        <Card>
          <CardHeader><CardTitle>{t("costs.budgetTab")}</CardTitle></CardHeader>
          <CardContent className="p-0">
            {budgets.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
              : (budgets.data?.items ?? []).length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
              : (
                <Table>
                  <TableHeader><TableRow><TableHead>{t("costs.name")}</TableHead><TableHead>{t("costs.planned")}</TableHead><TableHead>{t("costs.approved")}</TableHead><TableHead>{t("costs.forecast")}</TableHead></TableRow></TableHeader>
                  <TableBody>
                    {(budgets.data?.items ?? []).slice(0, 50).map((b) => (
                      <TableRow key={b.id}><TableCell className="font-medium">{b.name}</TableCell><TableCell className="tabular-nums">{formatCurrency(b.plannedAmount, b.currency)}</TableCell><TableCell className="tabular-nums">{formatCurrency(b.approvedAmount, b.currency)}</TableCell><TableCell className="tabular-nums">{formatCurrency(b.forecastAmount, b.currency)}</TableCell></TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
          </CardContent>
        </Card>
      ) : null}
      {tab === "overview" || tab === "actuals" ? (
        <Card>
          <CardHeader><CardTitle>{t("costs.actualsTab")}</CardTitle></CardHeader>
          <CardContent className="p-0">
            {costs.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
              : (costs.data?.items ?? []).length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
              : (
                <Table>
                  <TableHeader><TableRow><TableHead>{t("costs.description")}</TableHead><TableHead>{t("costs.amount")}</TableHead><TableHead>{t("costs.date")}</TableHead></TableRow></TableHeader>
                  <TableBody>
                    {(costs.data?.items ?? []).slice(0, 50).map((c) => (
                      <TableRow key={c.id}><TableCell className="font-medium">{c.description}</TableCell><TableCell className="tabular-nums">{formatCurrency(c.amount, c.currency)}</TableCell><TableCell>{c.incurredDate.slice(0, 10)}</TableCell></TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
          </CardContent>
        </Card>
      ) : null}

      <SideSheet open={budgetOpen} onOpenChange={setBudgetOpen} title={t("costs.addBudget")}>
        <form onSubmit={(e) => { e.preventDefault(); createBudget.mutate({ ...budgetForm, plannedAmount: Number(budgetForm.plannedAmount) || 0, approvedAmount: Number(budgetForm.plannedAmount) || 0, forecastAmount: Number(budgetForm.plannedAmount) || 0, fiscalYear: Number(budgetForm.fiscalYear) }, { onSuccess: () => setBudgetOpen(false) }); }} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5"><Label>{t("costs.name")} *</Label><Input value={budgetForm.name} onChange={(e) => setBudgetForm({ ...budgetForm, name: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>{t("costs.planned")} *</Label><Input type="number" value={budgetForm.plannedAmount} onChange={(e) => setBudgetForm({ ...budgetForm, plannedAmount: e.target.value })} required /></div>
          </SideSheetBody>
          <SideSheetFooter><Button type="submit" disabled={createBudget.isPending}>{t("common.save")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
      <SideSheet open={costOpen} onOpenChange={setCostOpen} title={t("costs.addCost")}>
        <form onSubmit={(e) => { e.preventDefault(); createCost.mutate({ ...costForm, amount: Number(costForm.amount) || 0 }, { onSuccess: () => setCostOpen(false) }); }} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5"><Label>{t("costs.description")} *</Label><Input value={costForm.description} onChange={(e) => setCostForm({ ...costForm, description: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>{t("costs.amount")} *</Label><Input type="number" value={costForm.amount} onChange={(e) => setCostForm({ ...costForm, amount: e.target.value })} required /></div>
          </SideSheetBody>
          <SideSheetFooter><Button type="submit" disabled={createCost.isPending}>{t("common.save")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
