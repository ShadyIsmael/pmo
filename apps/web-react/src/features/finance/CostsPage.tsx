import * as React from "react";
import { AlertTriangle } from "lucide-react";
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
import { hasAnyPermission, Permissions } from "@lib/permissions";
import { useManagement } from "@api/management";
import { useProjectFinancialFoundation } from "@features/projects/api/project-financials";
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

function useProjectCurrency(projectId: string): { currency: string | null; isLoading: boolean } {
  const foundation = useProjectFinancialFoundation(projectId, Boolean(projectId));
  const currency = foundation.data?.projectCurrency?.trim().toUpperCase() ?? null;
  return { currency, isLoading: foundation.isLoading };
}

export function CostsPage() {
  const { t, formatCurrency } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canWriteBudget = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Budget.Create])) && canMutate && !effectiveReadOnly;
  const canWriteCost = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Cost.Create])) && canMutate && !effectiveReadOnly;
  const tab = useTab();
  const budgets = useBudgets({}, tab === "budgets");
  const costs = useCosts({}, tab === "actuals");
  const createBudget = useCreateBudget();
  const createCost = useCreateCost();
  const [budgetOpen, setBudgetOpen] = React.useState(false);
  const [costOpen, setCostOpen] = React.useState(false);
  const [budgetForm, setBudgetForm] = React.useState({ projectId: "", name: "", category: "", plannedAmount: "", fiscalYear: new Date().getFullYear() });
  const [costForm, setCostForm] = React.useState({ projectId: "", description: "", amount: "", incurredDate: new Date().toISOString().slice(0, 10) });

  if (tab === "forecasts") return <CostsForecastsPage />;
  if (tab === "overview") return <CostsOverviewPage />;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("costs.title")}
        description={t("costs.description")}
        actions={
          <div className="flex gap-2">
            {canWriteBudget ? <Button size="sm" variant="outline" onClick={() => setBudgetOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("costs.addBudget")}</Button> : null}
            {canWriteCost ? <Button size="sm" onClick={() => setCostOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("costs.addCost")}</Button> : null}
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
                  <TableHeader><TableRow><TableHead>{t("costs.costDescription")}</TableHead><TableHead>{t("costs.amount")}</TableHead><TableHead>{t("costs.date")}</TableHead></TableRow></TableHeader>
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

      <BudgetSideSheet
        open={budgetOpen}
        onOpenChange={setBudgetOpen}
        form={budgetForm}
        onFormChange={setBudgetForm}
        createBudget={createBudget}
        t={t}
      />
      <CostSideSheet
        open={costOpen}
        onOpenChange={setCostOpen}
        form={costForm}
        onFormChange={setCostForm}
        createCost={createCost}
        t={t}
      />
    </div>
  );
}

type BudgetForm = { projectId: string; name: string; category: string; plannedAmount: string; fiscalYear: number };

function BudgetSideSheet({
  open, onOpenChange, form, onFormChange, createBudget, t,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  form: BudgetForm;
  onFormChange: React.Dispatch<React.SetStateAction<BudgetForm>>;
  createBudget: ReturnType<typeof useCreateBudget>;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const { currency, isLoading: currencyLoading } = useProjectCurrency(form.projectId);
  const currencyUnavailable = Boolean(form.projectId) && !currencyLoading && !currency;

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("costs.addBudget")}>
      <form onSubmit={(e) => {
        e.preventDefault();
        if (!currency) return;
        createBudget.mutate({
          projectId: form.projectId,
          name: form.name,
          category: form.category,
          plannedAmount: Number(form.plannedAmount) || 0,
          approvedAmount: Number(form.plannedAmount) || 0,
          forecastAmount: Number(form.plannedAmount) || 0,
          currency,
          fiscalYear: Number(form.fiscalYear),
          status: "Draft",
        }, { onSuccess: () => onOpenChange(false) });
      }} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-3">
          <div className="space-y-1.5"><Label>{t("costs.project")} *</Label><Input value={form.projectId} onChange={(e) => onFormChange({ ...form, projectId: e.target.value })} required /></div>
          <div className="space-y-1.5"><Label>{t("costs.name")} *</Label><Input value={form.name} onChange={(e) => onFormChange({ ...form, name: e.target.value })} required /></div>
          <div className="space-y-1.5"><Label>{t("costs.budgets.category")} *</Label><Input value={form.category} onChange={(e) => onFormChange({ ...form, category: e.target.value })} required /></div>
          <div className="space-y-1.5"><Label>{t("costs.planned")} *</Label><Input type="number" value={form.plannedAmount} onChange={(e) => onFormChange({ ...form, plannedAmount: e.target.value })} required /></div>
          <div className="space-y-1.5">
            <Label>{t("costs.currencyContext")}</Label>
            {currencyLoading ? <p className="text-xs text-muted-foreground">{t("costs.loading")}</p>
              : currency ? <p className="text-xs font-medium">{currency}</p>
              : currencyUnavailable ? <p className="flex items-center gap-1.5 text-xs font-medium text-destructive"><AlertTriangle className="h-3.5 w-3.5" />{t("projects.financialCurrencyUnavailable")}</p>
              : null}
          </div>
        </SideSheetBody>
        <SideSheetFooter><Button type="submit" disabled={createBudget.isPending || !currency}>{t("common.save")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  );
}

type CostForm = { projectId: string; description: string; amount: string; incurredDate: string };

function CostSideSheet({
  open, onOpenChange, form, onFormChange, createCost, t,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  form: CostForm;
  onFormChange: React.Dispatch<React.SetStateAction<CostForm>>;
  createCost: ReturnType<typeof useCreateCost>;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const { currency, isLoading: currencyLoading } = useProjectCurrency(form.projectId);
  const currencyUnavailable = Boolean(form.projectId) && !currencyLoading && !currency;

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("costs.addCost")}>
      <form onSubmit={(e) => {
        e.preventDefault();
        if (!currency) return;
        createCost.mutate({
          projectId: form.projectId,
          description: form.description,
          amount: Number(form.amount) || 0,
          currency,
          incurredDate: form.incurredDate,
        }, { onSuccess: () => onOpenChange(false) });
      }} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-3">
          <div className="space-y-1.5"><Label>{t("costs.project")} *</Label><Input value={form.projectId} onChange={(e) => onFormChange({ ...form, projectId: e.target.value })} required /></div>
          <div className="space-y-1.5"><Label>{t("costs.costDescription")} *</Label><Input value={form.description} onChange={(e) => onFormChange({ ...form, description: e.target.value })} required /></div>
          <div className="space-y-1.5"><Label>{t("costs.amount")} *</Label><Input type="number" value={form.amount} onChange={(e) => onFormChange({ ...form, amount: e.target.value })} required /></div>
          <div className="space-y-1.5">
            <Label>{t("costs.currencyContext")}</Label>
            {currencyLoading ? <p className="text-xs text-muted-foreground">{t("costs.loading")}</p>
              : currency ? <p className="text-xs font-medium">{currency}</p>
              : currencyUnavailable ? <p className="flex items-center gap-1.5 text-xs font-medium text-destructive"><AlertTriangle className="h-3.5 w-3.5" />{t("projects.financialCurrencyUnavailable")}</p>
              : null}
          </div>
        </SideSheetBody>
        <SideSheetFooter><Button type="submit" disabled={createCost.isPending || !currency}>{t("common.save")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  );
}