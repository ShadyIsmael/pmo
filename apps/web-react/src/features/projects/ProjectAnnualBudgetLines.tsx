import * as React from "react";
import { Check, Pencil, Plus, X } from "lucide-react";
import { ApiError } from "@api/client";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import {
  useProjectAnnualBudgetLines,
  useSaveProjectAnnualBudgetLine,
  type ProjectAnnualBudget,
  type ProjectAnnualBudgetLine,
} from "./api/project-annual-budgets";

export function ProjectAnnualBudgetLines({ projectId, budget, canManage, enabled = true }: {
  projectId: string;
  budget: ProjectAnnualBudget;
  canManage: boolean;
  enabled?: boolean;
}) {
  const { t, formatCurrency } = useI18n();
  const lines = useProjectAnnualBudgetLines(projectId, budget.id, enabled);
  const save = useSaveProjectAnnualBudgetLine(projectId, budget.id);
  const [editing, setEditing] = React.useState<ProjectAnnualBudgetLine | null>(null);
  const [formOpen, setFormOpen] = React.useState(false);
  const [description, setDescription] = React.useState("");
  const [authorizedLimit, setAuthorizedLimit] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const mutable = canManage && budget.status === "Draft" && budget.canEdit;
  const busy = save.isPending;

  const startNew = () => {
    setEditing(null);
    setFormOpen(true);
    setDescription("");
    setAuthorizedLimit("");
    setError(null);
  };

  const startEdit = (line: ProjectAnnualBudgetLine) => {
    setEditing(line);
    setFormOpen(true);
    setDescription(line.description);
    setAuthorizedLimit(String(line.authorizedLimit));
    setError(null);
  };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const amount = Number(authorizedLimit);
    if (!description.trim() || !Number.isFinite(amount) || amount <= 0) {
      setError(t("projects.annualBudgetLineValidation"));
      return;
    }
    setError(null);
    try {
      await save.mutateAsync({
        id: editing?.id,
        description: description.trim(),
        authorizedLimit: amount,
        rowVersion: editing?.rowVersion,
      });
      startNew();
      setFormOpen(false);
    } catch (cause) {
      const status = cause instanceof ApiError ? cause.status : undefined;
      setError(t(status === 409 ? "projects.annualBudgetLineChanged" : "projects.annualBudgetLineSaveFailed"));
    }
  };

  return <section className="space-y-3 rounded-md border p-4" aria-labelledby={`annual-budget-lines-${budget.id}`}>
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h4 id={`annual-budget-lines-${budget.id}`} className="text-sm font-semibold">{t("projects.annualBudgetLinesTitle")}</h4>
        <p className="mt-1 text-xs text-muted-foreground">{t(mutable ? "projects.annualBudgetLinesDraftHint" : "projects.annualBudgetLinesReadOnlyHint")}</p>
      </div>
      {mutable && !formOpen ? <Button type="button" size="sm" variant="outline" onClick={startNew}>
        <Plus className="me-1.5 h-4 w-4" />{t("projects.annualBudgetLineAdd")}
      </Button> : null}
    </div>

    {lines.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
      : lines.isError ? <div className="flex flex-wrap items-center justify-between gap-2">
        <p role="alert" className="text-sm text-destructive">{t("projects.annualBudgetLinesLoadFailed")}</p>
        <Button type="button" size="sm" variant="outline" onClick={() => void lines.refetch()}>{t("common.retry")}</Button>
      </div>
        : (lines.data ?? []).length === 0 ? <p role="status" className="rounded border border-dashed px-3 py-3 text-sm text-muted-foreground">{t("projects.annualBudgetLinesEmpty")}</p>
          : <ul className="divide-y rounded border">{lines.data!.map(line => <li key={line.id} className="flex flex-wrap items-center justify-between gap-3 px-3 py-2.5">
            <div className="min-w-0">
              <p className="text-sm font-medium">{line.reference} · {line.description}</p>
              <p className="mt-0.5 text-xs text-muted-foreground">
                {t("projects.annualBudgetLineLimit")}: {formatCurrency(line.authorizedLimit, line.currency)}
                <span className="mx-1.5">·</span>{t("projects.annualBudgetLineReserved")}: {formatCurrency(line.reservedAmount, line.currency)}
                <span className="mx-1.5">·</span>{t("projects.annualBudgetLineAvailable")}: {formatCurrency(line.availableAmount, line.currency)}
              </p>
            </div>
            {mutable && line.canEdit ? <Button type="button" size="sm" variant="ghost" onClick={() => startEdit(line)} aria-label={t("projects.annualBudgetLineEdit", { reference: line.reference })}>
              <Pencil className="h-4 w-4" />
            </Button> : null}
          </li>)}</ul>}

    {mutable && formOpen ? <form className="grid gap-3 rounded border bg-muted/20 p-3 sm:grid-cols-[minmax(0,1fr)_12rem_auto] sm:items-end" onSubmit={submit}>
      <div>
        <Label htmlFor={`annual-budget-line-description-${budget.id}`}>{t("projects.annualBudgetLineDescription")}</Label>
        <Input id={`annual-budget-line-description-${budget.id}`} value={description} onChange={event => setDescription(event.target.value)} maxLength={200} required />
      </div>
      <div>
        <Label htmlFor={`annual-budget-line-limit-${budget.id}`}>{t("projects.annualBudgetLineLimit")}</Label>
        <Input id={`annual-budget-line-limit-${budget.id}`} type="number" min="0.0001" step="0.0001" value={authorizedLimit} onChange={event => setAuthorizedLimit(event.target.value)} required />
      </div>
      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={busy}><Check className="me-1.5 h-4 w-4" />{busy ? t("common.saving") : t(editing ? "common.save" : "projects.annualBudgetLineAdd")}</Button>
        {editing ? <Button type="button" size="sm" variant="outline" disabled={busy} onClick={startNew}><X className="me-1.5 h-4 w-4" />{t("common.cancel")}</Button> : null}
      </div>
      {error ? <p role="alert" className="text-sm text-destructive sm:col-span-3">{error}</p> : null}
    </form> : null}
  </section>;
}
