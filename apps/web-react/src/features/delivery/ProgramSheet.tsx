import * as React from "react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { Textarea } from "@components/ui/Textarea";
import { ApiError, getApiFieldError } from "@api/client";
import { useProgramWorkflowManagerLookup, useWorkflowCurrencyLookup, toUserSelectOptions } from "@features/projects/lib/project-lookups";
import { useAuth } from "@auth/AuthProvider";
import { managementCacheScopeKey, useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { useI18n } from "@i18n/messages";
import { useCreateProgram, useEditableProgramPortfolios, useEligibleProgramPortfolios, useUpdateProgram, type ProgramDto, type SaveProgramDto } from "./api/delivery";

const blankForm = (): SaveProgramDto => ({
  name: "", description: "", portfolioId: "", programManagerId: "", status: "Planning",
  currency: "",
});

export function ProgramSheet({ open, onOpenChange, canSave, canAssignInitialManager, program, cacheScope }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  canSave: boolean;
  canAssignInitialManager: boolean;
  program?: ProgramDto;
  cacheScope?: string;
}) {
  const { t } = useI18n();
  const { user } = useAuth();
  const { context, effectiveReadOnly, loading: managementLoading } = useManagement();
  const permissions = user?.permissions ?? [];
  const hasProgramCreate = hasPermission(permissions, Permissions.Program.Create);
  const hasProgramEdit = hasPermission(permissions, Permissions.Program.Edit);
  // The legacy edit-choice status branch below is now based on Program.Edit.
  const hasPortfolioView = hasProgramEdit;
  const scopeKey = cacheScope ?? managementCacheScopeKey(user?.id, context);
  const [form, setForm] = React.useState<SaveProgramDto>(blankForm);
  const [error, setError] = React.useState("");
  const [apiError, setApiError] = React.useState<unknown>(null);
  const hasAssignedManager = Boolean(program?.programManagerId && program.programManagerId !== "00000000-0000-0000-0000-000000000000");
  const usersQuery = useProgramWorkflowManagerLookup(open && canAssignInitialManager && (!program || !hasAssignedManager));
  const canLoadCreationPortfolios = open && !program && canSave && hasProgramCreate && !effectiveReadOnly && !managementLoading;
  const canLoadEditPortfolios = open && Boolean(program) && canSave && hasProgramEdit && !effectiveReadOnly && !managementLoading;
  const eligiblePortfoliosQuery = useEligibleProgramPortfolios(canLoadCreationPortfolios, scopeKey);
  const editPortfoliosQuery = useEditableProgramPortfolios(canLoadEditPortfolios, scopeKey);
  const currenciesQuery = useWorkflowCurrencyLookup(open && canSave);
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
      currency: program.currency ?? "",
      rowVersion: program.rowVersion,
    } : blankForm());
    setError("");
    setApiError(null);
  }, [open, program]);

  React.useEffect(() => {
    if (program || !eligiblePortfoliosQuery.data || eligiblePortfoliosQuery.isFetching) return;
    const stillEligible = eligiblePortfoliosQuery.data.items.some((portfolio) =>
      portfolio.id === form.portfolioId && (!portfolio.status || portfolio.status === "Active")
    );
    if (form.portfolioId && !stillEligible) {
      setForm((current) => current.portfolioId === form.portfolioId
        ? { ...current, portfolioId: "", currency: "" }
        : current);
    }
  }, [program, eligiblePortfoliosQuery.data, eligiblePortfoliosQuery.isFetching, form.portfolioId]);

  const eligiblePortfolios = eligiblePortfoliosQuery.isFetching ? [] : eligiblePortfoliosQuery.data?.items ?? [];
  const listedEditPortfolios = editPortfoliosQuery.data?.items ?? [];
  const activeEditPortfolios = listedEditPortfolios.filter((portfolio) =>
    !portfolio.status || portfolio.status === "Active" || portfolio.id === program?.portfolioId
  );
  const currentPortfolio = program?.portfolioId && !activeEditPortfolios.some((portfolio) => portfolio.id === program.portfolioId)
    ? [{ id: program.portfolioId, name: program.portfolioName || t("common.notAvailable"), code: undefined, status: "", currency: undefined }]
    : [];
  const portfolios = program ? [...activeEditPortfolios, ...currentPortfolio] : eligiblePortfolios;
  const portfolioLookupQuery = program ? editPortfoliosQuery : eligiblePortfoliosQuery;
  const portfoliosLoading = portfolioLookupQuery.isFetching;
  const hasEligibleSelectedPortfolio = portfolios.some((portfolio) => portfolio.id === form.portfolioId);
  const canSelectPortfolio = program
    ? Boolean(canLoadEditPortfolios && !editPortfoliosQuery.isError)
    : Boolean(canLoadCreationPortfolios && !eligiblePortfoliosQuery.isError);
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
  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");
    setApiError(null);
    if (!canSave) return;
    if (!form.name.trim()) {
      setError(t("programManager.programNameRequired"));
      return;
    }
    if (!form.portfolioId) {
      setError(t("programManager.programPortfolioRequired"));
      return;
    }
    if (!program && (!canLoadCreationPortfolios || eligiblePortfoliosQuery.isError || eligiblePortfoliosQuery.isFetching || !hasEligibleSelectedPortfolio)) {
      setError(t("programManager.portfolioNoLongerEligible"));
      return;
    }

    const currency = (program ? form.currency : portfolioCurrency || form.currency)?.trim().toUpperCase() ?? "";
    if (!/^[A-Z]{3}$/.test(currency)) {
      setError(t("programManager.currencyRequired"));
      return;
    }
    const payload: SaveProgramDto = {
      ...form,
      name: form.name.trim(),
      description: form.description?.trim() || undefined,
      programManagerId: hasAssignedManager ? program?.programManagerId : form.programManagerId || undefined,
      currency,
    };
    try {
      if (program) await update.mutateAsync({ id: program.id, dto: payload });
      else await create.mutateAsync(payload);
      onOpenChange(false);
    } catch (reason) {
      setApiError(reason);
      setError(reason instanceof Error ? reason.message : t("programManager.programSaveFailed"));
    }
  };

  return <SideSheet open={open} onOpenChange={onOpenChange} title={program ? t("programManager.editProgram") : t("programManager.createProgram")} description={t("programManager.programSheetDescription")} size="xl">
    <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_minmax(14rem,0.55fr)]">
          <div><Label htmlFor="program-name">{t("programManager.name")} *</Label><Input id="program-name" className="mt-2" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} required aria-invalid={Boolean(getApiFieldError(apiError, "name"))} aria-describedby={getApiFieldError(apiError, "name") ? "program-name-error" : undefined} /><ApiFieldError id="program-name-error" error={apiError} fieldName="name" /></div>
          <div><Label>{t("programManager.code")}</Label><p className="mt-2 flex min-h-10 items-center rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">{program?.code || t("programManager.generatedProgramCode")}</p></div>
        </div>
        <div><Label htmlFor="program-description">{t("programManager.description")}</Label><Textarea id="program-description" className="mt-2" value={form.description ?? ""} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></div>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <Label>{t("programManager.portfolio")} *</Label>
            {!program && !hasProgramCreate ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("programManager.programCreateRequired")}</p> : null}
            {!program && hasProgramCreate && effectiveReadOnly ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("programManager.programCreationReadOnly")}</p> : null}
            {!program && hasProgramCreate && !effectiveReadOnly && managementLoading ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("programManager.programPortfolioLoading")}</p> : null}
            {!program && canLoadCreationPortfolios && eligiblePortfoliosQuery.isError ? <div role="alert" className="mt-2 flex flex-wrap items-center gap-2 text-xs text-destructive"><span>{eligiblePortfoliosQuery.error instanceof ApiError && eligiblePortfoliosQuery.error.status === 403 ? t("programManager.eligiblePortfolioAccessDenied") : t("programManager.eligiblePortfolioLoadFailed")}</span><Button type="button" size="sm" variant="outline" onClick={() => void eligiblePortfoliosQuery.refetch()}>{t("programManager.retryPortfolioLookup")}</Button></div> : null}
            {!program && canLoadCreationPortfolios && eligiblePortfoliosQuery.isFetching ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("programManager.programPortfolioLoading")}</p> : null}
            {!program && canLoadCreationPortfolios && !eligiblePortfoliosQuery.isFetching && !eligiblePortfoliosQuery.isError && eligiblePortfoliosQuery.data && portfolios.length === 0 ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("programManager.noEligiblePortfolios")}</p> : null}
            {program && !hasProgramEdit ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("programManager.programEditRequired")}</p> : null}
            {program && hasPortfolioView && editPortfoliosQuery.isError ? <div role="alert" className="mt-2 flex flex-wrap items-center gap-2 text-xs text-destructive"><span>{t("programManager.portfolioLookupLoadFailed")}</span><Button type="button" size="sm" variant="outline" onClick={() => void editPortfoliosQuery.refetch()}>{t("programManager.retryPortfolioLookup")}</Button></div> : null}
            <div className="mt-2"><SearchableSelect aria-label={t("programManager.portfolio")} aria-invalid={Boolean(getApiFieldError(apiError, "portfolioId"))} aria-describedby={getApiFieldError(apiError, "portfolioId") ? "program-portfolio-error" : undefined} options={portfolioOptions} value={form.portfolioId} disabled={!canSelectPortfolio} onValueChange={(portfolioId) => {
              const selected = portfolios.find((portfolio) => portfolio.id === portfolioId);
              setForm((current) => ({ ...current, portfolioId, currency: program ? current.currency : selected?.currency ?? "" }));
            }} placeholder={t("programManager.selectPortfolio")} searchPlaceholder={t("programManager.searchPortfolios")} loading={portfoliosLoading} emptyText={t("programManager.noActivePortfolios")} /></div>
            <ApiFieldError id="program-portfolio-error" error={apiError} fieldName="portfolioId" />
          </div>
          <div>
            <Label>{t("programManager.manager")}</Label>
            {hasAssignedManager
              ? <><Input className="mt-2" value={program?.programManagerName || "—"} readOnly /><p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.managerChangeViaHandover")}</p></>
              : <><div className="mt-2"><SearchableSelect aria-label={t("programManager.manager")} aria-invalid={Boolean(getApiFieldError(apiError, "programManagerId"))} aria-describedby={getApiFieldError(apiError, "programManagerId") ? "program-manager-error" : undefined} options={userOptions} value={form.programManagerId} disabled={!canAssignInitialManager || usersQuery.isError} onValueChange={(programManagerId) => setForm((current) => ({ ...current, programManagerId }))} placeholder={t("programManager.selectProgramManager")} searchPlaceholder={t("programManager.searchProgramManagers")} loading={usersQuery.isLoading} emptyText={t("programManager.noProgramManagers")} /></div><ApiFieldError id="program-manager-error" error={apiError} fieldName="programManagerId" />{usersQuery.isError ? <div role="alert" className="mt-1 flex flex-wrap items-center justify-between gap-2 text-xs text-destructive"><span>{usersQuery.error instanceof ApiError && usersQuery.error.status === 403 ? t("programManager.managerLookupAccessDenied") : t("common.errorLoading")}</span><Button type="button" size="sm" variant="outline" onClick={() => void usersQuery.refetch()}>{t("common.retry")}</Button></div> : null}<p className="mt-1 text-xs text-muted-foreground">{t(canAssignInitialManager ? "managerHandover.setInitialManager" : "managerHandover.adminInitiateRequired")}</p></>}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <Label htmlFor="program-currency">{t("programManager.currency")}</Label>
            {currencies.length > 0
              ? <select id="program-currency" className="input-field mt-2" value={form.currency ?? ""} disabled={!program && Boolean(portfolioCurrency)} required aria-invalid={Boolean(getApiFieldError(apiError, "currency"))} aria-describedby={getApiFieldError(apiError, "currency") ? "program-currency-error" : undefined} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value }))}><option value="">{t("common.select")}</option>{currencies.map((currency) => <option key={currency.code} value={currency.code}>{currency.code} - {currency.name} {currency.symbol ? `(${currency.symbol})` : ""}</option>)}</select>
              : <Input id="program-currency" className="mt-2" value={form.currency ?? ""} maxLength={3} required aria-invalid={Boolean(getApiFieldError(apiError, "currency"))} aria-describedby={getApiFieldError(apiError, "currency") ? "program-currency-error" : undefined} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} />}
            <ApiFieldError id="program-currency-error" error={apiError} fieldName="currency" />
            <p className="mt-1 text-xs text-muted-foreground">{t("programManager.currencyDefaultHint", { currency: portfolioCurrency || t("common.notAvailable") })}</p>
          </div>
          <div><Label htmlFor="program-status">{t("programManager.status")}</Label><select id="program-status" className="input-field mt-2" value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))}>{statuses.map((status) => <option key={status.value} value={status.value}>{status.label}</option>)}</select></div>
        </div>
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>{t("common.cancel")}</Button>
        <Button type="submit" disabled={isPending || !canSave || !form.currency || (!program && (!canLoadCreationPortfolios || !hasEligibleSelectedPortfolio || eligiblePortfoliosQuery.isError || eligiblePortfoliosQuery.isFetching))}>{isPending ? t("programManager.saving") : t("programManager.saveProgram")}</Button>
      </SideSheetFooter>
    </form>
  </SideSheet>;
}
