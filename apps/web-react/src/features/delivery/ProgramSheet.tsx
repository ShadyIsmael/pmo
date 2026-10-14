import * as React from "react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useCurrencies } from "@features/administration/api/admin";
import { usePortfolioLookup, useUserLookup, toUserSelectOptions } from "@features/projects/lib/project-lookups";
import { useI18n } from "@i18n/messages";
import { useCreateProgram, useUpdateProgram, type ProgramDto, type SaveProgramDto } from "./api/delivery";

const blankForm = (): SaveProgramDto => ({
  name: "", description: "", portfolioId: "", programManagerId: "", status: "Planning",
  startDate: "", endDate: "", currency: "USD", budget: undefined, health: undefined,
});

export function ProgramSheet({ open, onOpenChange, canManage, program }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  canManage: boolean;
  program?: ProgramDto;
}) {
  const { t } = useI18n();
  const [form, setForm] = React.useState<SaveProgramDto>(blankForm);
  const [error, setError] = React.useState("");
  const hasAssignedManager = Boolean(program?.programManagerId && program.programManagerId !== "00000000-0000-0000-0000-000000000000");
  const usersQuery = useUserLookup(undefined, open && (!program || !hasAssignedManager));
  const portfoliosQuery = usePortfolioLookup(open);
  const currenciesQuery = useCurrencies(open);
  const create = useCreateProgram();
  const update = useUpdateProgram();
  const isPending = create.isPending || update.isPending;

  React.useEffect(() => {
    if (!open) return;
    setForm(program ? {
      name: program.name,
      description: program.description ?? "",
      portfolioId: program.portfolioId ?? "",
      programManagerId: program.programManagerId && program.programManagerId !== "00000000-0000-0000-0000-000000000000" ? program.programManagerId : "",
      status: program.status ?? "Planning",
      startDate: program.startDate?.slice(0, 10) ?? "",
      endDate: program.endDate?.slice(0, 10) ?? "",
      currency: program.currency || "USD",
      budget: program.budget,
      health: program.health,
      rowVersion: program.rowVersion,
    } : blankForm());
    setError("");
  }, [open, program]);

  const portfolios = (portfoliosQuery.data?.items ?? []).filter((portfolio) => !portfolio.status || portfolio.status === "Active");
  const portfolioOptions = portfolios.map((portfolio) => ({
    value: portfolio.id,
    label: `${portfolio.name}${portfolio.code ? ` (${portfolio.code})` : ""}`,
    keywords: [portfolio.code ?? ""],
  }));
  const userOptions = toUserSelectOptions(usersQuery.data);
  const currencies = currenciesQuery.data ?? [];
  const portfolioCurrency = portfolios.find((portfolio) => portfolio.id === form.portfolioId)?.currency;
  const statuses = [
    { value: "Planning", label: t("programManager.statuses.planning") },
    { value: "Active", label: t("programManager.statuses.active") },
    { value: "On Hold", label: t("programManager.statuses.onHold") },
    { value: "Completed", label: t("programManager.statuses.completed") },
    { value: "Archived", label: t("programManager.statuses.archived") },
  ];
  const healthOptions = [
    { value: "Green", label: t("programManager.healthOptions.green") },
    { value: "Amber", label: t("programManager.healthOptions.amber") },
    { value: "Red", label: t("programManager.healthOptions.red") },
    { value: "Blue", label: t("programManager.healthOptions.blue") },
  ];

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");
    if (!canManage) return;
    if (!form.name.trim()) {
      setError(t("programManager.programNameRequired"));
      return;
    }
    if (!form.portfolioId) {
      setError(t("programManager.programPortfolioRequired"));
      return;
    }

    const payload: SaveProgramDto = {
      ...form,
      name: form.name.trim(),
      description: form.description?.trim() || undefined,
      programManagerId: hasAssignedManager ? program?.programManagerId : form.programManagerId || undefined,
      startDate: form.startDate || undefined,
      endDate: form.endDate || undefined,
      currency: form.currency || "USD",
      budget: form.budget || undefined,
      health: form.health || undefined,
    };
    try {
      if (program) await update.mutateAsync({ id: program.id, dto: payload });
      else await create.mutateAsync(payload);
      onOpenChange(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("programManager.programSaveFailed"));
    }
  };

  return <SideSheet open={open} onOpenChange={onOpenChange} title={program ? t("programManager.editProgram") : t("programManager.createProgram")} description={t("programManager.programSheetDescription")} size="xl">
    <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(14rem,0.55fr)]">
          <div><Label htmlFor="program-name">{t("programManager.name")} *</Label><Input id="program-name" className="mt-2" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} required /></div>
          <div><Label>{t("programManager.code")}</Label><p className="mt-2 flex min-h-10 items-center rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">{program?.code || t("programManager.generatedProgramCode")}</p></div>
        </div>
        <div><Label htmlFor="program-description">{t("programManager.description")}</Label><Textarea id="program-description" className="mt-2" value={form.description ?? ""} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div><Label>{t("programManager.portfolio")} *</Label><div className="mt-2"><SearchableSelect options={portfolioOptions} value={form.portfolioId} onValueChange={(portfolioId) => {
            const selected = portfolios.find((portfolio) => portfolio.id === portfolioId);
            setForm((current) => ({ ...current, portfolioId, currency: program ? current.currency : selected?.currency || "USD" }));
          }} placeholder={t("programManager.selectPortfolio")} searchPlaceholder={t("programManager.searchPortfolios")} loading={portfoliosQuery.isLoading} emptyText={t("programManager.noActivePortfolios")} /></div></div>
          <div>
            <Label>{t("programManager.manager")}</Label>
            {hasAssignedManager
              ? <><Input className="mt-2" value={program?.programManagerName || "—"} readOnly /><p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.managerChangeViaHandover")}</p></>
              : <><div className="mt-2"><SearchableSelect options={userOptions} value={form.programManagerId} onValueChange={(programManagerId) => setForm((current) => ({ ...current, programManagerId }))} placeholder={t("programManager.selectProgramManager")} searchPlaceholder={t("programManager.searchProgramManagers")} loading={usersQuery.isLoading} emptyText={t("programManager.noProgramManagers")} /></div>{program ? <p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.setInitialManager")}</p> : null}</>}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div><Label htmlFor="program-start-date">{t("programManager.startDate")}</Label><Input id="program-start-date" className="mt-2" type="date" value={form.startDate ?? ""} onChange={(event) => setForm((current) => ({ ...current, startDate: event.target.value }))} /></div>
          <div><Label htmlFor="program-end-date">{t("programManager.endDate")}</Label><Input id="program-end-date" className="mt-2" type="date" value={form.endDate ?? ""} onChange={(event) => setForm((current) => ({ ...current, endDate: event.target.value }))} /></div>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="program-currency">{t("programManager.currency")}</Label>
            {currencies.length > 0
              ? <select id="program-currency" className="input-field mt-2" value={form.currency || "USD"} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value }))}>{currencies.map((currency) => <option key={currency.code} value={currency.code}>{currency.code} - {currency.name} {currency.symbol ? `(${currency.symbol})` : ""}</option>)}</select>
              : <Input id="program-currency" className="mt-2" value={form.currency || "USD"} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} />}
            <p className="mt-1 text-xs text-muted-foreground">{t("programManager.currencyDefaultHint", { currency: portfolioCurrency || "USD" })}</p>
          </div>
          <div><Label htmlFor="program-budget">{t("programManager.budget")}</Label><Input id="program-budget" className="mt-2" type="number" step="0.01" min="0" value={form.budget ?? ""} onChange={(event) => setForm((current) => ({ ...current, budget: event.target.value ? Number(event.target.value) : undefined }))} /></div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div><Label htmlFor="program-status">{t("programManager.status")}</Label><select id="program-status" className="input-field mt-2" value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))}>{statuses.map((status) => <option key={status.value} value={status.value}>{status.label}</option>)}</select></div>
          <div><Label htmlFor="program-health">{t("programManager.health")}</Label><select id="program-health" className="input-field mt-2" value={form.health ?? ""} onChange={(event) => setForm((current) => ({ ...current, health: event.target.value || undefined }))}><option value="">{t("programManager.selectHealth")}</option>{healthOptions.map((health) => <option key={health.value} value={health.value}>{health.label}</option>)}</select></div>
        </div>
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>{t("common.cancel")}</Button>
        <Button type="submit" disabled={isPending || !canManage}>{isPending ? t("programManager.saving") : t("programManager.saveProgram")}</Button>
      </SideSheetFooter>
    </form>
  </SideSheet>;
}
