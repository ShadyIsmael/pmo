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
import { useManagement } from "@api/management";
import { useBudgets, useCosts, useCreateBudget, useCreateCost } from "./api/finance";
import { CostsForecastsPage } from "./CostsForecastsPage";
import { CostsOverviewPage } from "./CostsOverviewPage";

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
  const { canMutate, effectiveReadOnly } = useManagement();
  const canWrite = Boolean(user && hasPermission(user.permissions, Permissions.Finances.Write)) && canMutate && !effectiveReadOnly;
  const tab = useTab();
  const budgets = useBudgets({}, tab === "budgets");
  const costs = useCosts({}, tab === "actuals");
  const createBudget = useCreateBudget();
  const createCost = useCreateCost();
  const [budgetOpen, setBudgetOpen] = React.useState(false);
  const [costOpen, setCostOpen] = React.useState(false);
  const [budgetForm, setBudgetForm] = React.useState({ projectId: "", name: "", category: "", plannedAmount: "", currency: "USD", fiscalYear: new Date().getFullYear() });
  const [costForm, setCostForm] = React.useState({ projectId: "", description: "", amount: "", currency: "USD", incurredDate: new Date().toISOString().slice(0, 10) });

  if (tab === "forecasts") return <CostsForecastsPage />;
  if (tab === "overview") return <CostsOverviewPage />;

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
      {tab === "budgets" ? (
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
      {tab === "actuals" ? (
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
        <form onSubmit={(e) => { e.preventDefault(); createBudget.mutate({ ...budgetForm, plannedAmount: Number(budgetForm.plannedAmount) || 0, approvedAmount: Number(budgetForm.plannedAmount) || 0, forecastAmount: Number(budgetForm.plannedAmount) || 0, fiscalYear: Number(budgetForm.fiscalYear), status: "Draft" }, { onSuccess: () => setBudgetOpen(false) }); }} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5"><Label>{t("costs.project")} *</Label><Input value={budgetForm.projectId} onChange={(e) => setBudgetForm({ ...budgetForm, projectId: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>{t("costs.name")} *</Label><Input value={budgetForm.name} onChange={(e) => setBudgetForm({ ...budgetForm, name: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>{t("costs.budgets.category")} *</Label><Input value={budgetForm.category} onChange={(e) => setBudgetForm({ ...budgetForm, category: e.target.value })} required /></div>
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
