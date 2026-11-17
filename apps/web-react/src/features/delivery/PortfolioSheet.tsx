import * as React from "react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { ApiError, getApiFieldError } from "@api/client";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Textarea } from "@components/ui/Textarea";
import { toUserSelectOptions, usePortfolioWorkflowManagerLookup, useWorkflowCurrencyLookup } from "@features/projects/lib/project-lookups";
import { useI18n } from "@i18n/messages";
import { useManagement } from "@api/management";
import { useCreatePortfolio, useUpdatePortfolio, type PortfolioDto, type SavePortfolioDto } from "./api/delivery";

const blankForm = (): SavePortfolioDto => ({ name: "", organizationId: "", description: "", ownerId: "", status: "Active", currency: "", strategicObjective: "", priority: "" });

export function PortfolioSheet({ open, onOpenChange, portfolio, canSave, canAssignInitialManager }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  portfolio?: PortfolioDto;
  canSave: boolean;
  canAssignInitialManager: boolean;
}) {
  const { t } = useI18n();
  const { snapshot } = useManagement();
  const organizationOptions = snapshot?.scope.organizationReferences ?? [];
  const [form, setForm] = React.useState<SavePortfolioDto>(blankForm);
  const [error, setError] = React.useState("");
  const [apiError, setApiError] = React.useState<unknown>(null);
  const hasAssignedOwner = Boolean(portfolio?.ownerId && portfolio.ownerId !== "00000000-0000-0000-0000-000000000000");
  const usersQuery = usePortfolioWorkflowManagerLookup(open && canAssignInitialManager && (!portfolio || !hasAssignedOwner));
  const currenciesQuery = useWorkflowCurrencyLookup(open && canSave);
  const create = useCreatePortfolio();
  const update = useUpdatePortfolio();
  const isPending = create.isPending || update.isPending;

  React.useEffect(() => {
    if (!open) {
      setForm(blankForm());
      setError("");
      setApiError(null);
      return;
    }
    setForm(portfolio ? {
      name: portfolio.name,
      organizationId: portfolio.organizationId ?? "",
      description: portfolio.description ?? "",
      ownerId: portfolio.ownerId && portfolio.ownerId !== "00000000-0000-0000-0000-000000000000" ? portfolio.ownerId : "",
      status: portfolio.status === "Archived" ? "Archived" : "Active",
      currency: portfolio.currency ?? "",
      strategicObjective: portfolio.strategicObjective ?? "",
      priority: portfolio.priority ?? "",
    } : { ...blankForm(), organizationId: organizationOptions.length === 1 ? organizationOptions[0].id : "" });
    setError("");
    setApiError(null);
  }, [open, portfolio, organizationOptions]);

  const userOptions = toUserSelectOptions(usersQuery.data);
  const currencyOptions = React.useMemo(() => currenciesQuery.data ?? [], [currenciesQuery.data]);
  const selectedCurrency = form.currency?.trim().toUpperCase() ?? "";
  const selectedCurrencyIsConfigured = currencyOptions.some((currency) => currency.code.toUpperCase() === selectedCurrency);
  const keepsExistingCurrency = Boolean(portfolio?.currency && portfolio.currency.trim().toUpperCase() === selectedCurrency);
  const canSaveCurrency = (selectedCurrencyIsConfigured && !currenciesQuery.isError) || keepsExistingCurrency;

  React.useEffect(() => {
    if (!open || portfolio || form.currency || currenciesQuery.isError) return;
    const baseCurrency = currencyOptions.find((currency) => currency.isBase)?.code;
    if (baseCurrency) setForm((current) => current.currency ? current : { ...current, currency: baseCurrency });
  }, [open, portfolio, form.currency, currencyOptions, currenciesQuery.isError]);

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      setForm(blankForm());
      setError("");
      setApiError(null);
    }
    onOpenChange(nextOpen);
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");
    setApiError(null);
    if (!canSave) return;
    if (!form.name.trim()) {
      setError(t("portfolioManager.portfolioNameRequired"));
      return;
    }
    if (!portfolio && !form.organizationId) {
      setError(t("portfolioManager.organizationRequired"));
      return;
    }
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      setError(t("portfolioManager.endDateAfterStartDate"));
      return;
    }
    const currency = form.currency?.trim().toUpperCase() ?? "";
    const currencyIsConfigured = currencyOptions.some((item) => item.code.toUpperCase() === currency);
    const currencyIsUnchanged = Boolean(portfolio?.currency && portfolio.currency.trim().toUpperCase() === currency);
    if ((!currencyIsConfigured || currenciesQuery.isError) && !currencyIsUnchanged) {
      setError(t("portfolioManager.currencyRequired"));
      return;
    }
    const payload: SavePortfolioDto = {
      ...form,
      organizationId: form.organizationId || undefined,
      ownerId: hasAssignedOwner ? portfolio?.ownerId : form.ownerId || undefined,
      name: form.name.trim(),
      description: form.description ?? "",
      currency,
      strategicObjective: form.strategicObjective?.trim() ? form.strategicObjective.trim() : undefined,
      priority: form.priority?.trim() ? form.priority.trim() : undefined,
    };
    try {
      if (portfolio) await update.mutateAsync({ id: portfolio.id, dto: payload });
      else await create.mutateAsync(payload);
      handleOpenChange(false);
    } catch (reason) {
      setApiError(reason);
      setError(reason instanceof Error ? reason.message : t("portfolioManager.portfolioSaveFailed"));
    }
  };

  return <SideSheet open={open} onOpenChange={handleOpenChange} title={portfolio ? t("portfolioManager.editPortfolio") : t("portfolioManager.createPortfolio")} description={t("portfolioManager.portfolioSheetDescription")} size="md">
    <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        {!portfolio ? <div>
          <Label htmlFor="portfolio-organization">{t("portfolioManager.organization")} *</Label>
          <select id="portfolio-organization" className="input-field mt-2" value={form.organizationId ?? ""} required disabled={isPending || organizationOptions.length === 0} aria-invalid={Boolean(getApiFieldError(apiError, "organizationId"))} aria-describedby={getApiFieldError(apiError, "organizationId") ? "portfolio-organization-error" : undefined} onChange={(event) => setForm((current) => ({ ...current, organizationId: event.target.value }))}>
            <option value="">{t("common.select")}</option>
            {organizationOptions.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}
          </select>
          <ApiFieldError id="portfolio-organization-error" error={apiError} fieldName="organizationId" />
          {organizationOptions.length === 0 ? <p role="alert" className="mt-1 text-xs text-destructive">{t("portfolioManager.noOrganizationScope")}</p> : null}
        </div> : <div>
          <Label>{t("portfolioManager.organization")}</Label>
          <Input className="mt-2" value={organizationOptions.find((organization) => organization.id === portfolio.organizationId)?.name ?? t("common.notAvailable")} readOnly />
        </div>}
        <div><Label htmlFor="portfolio-name">{t("portfolioManager.portfolioName")} *</Label><Input id="portfolio-name" className="mt-2" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} required aria-invalid={Boolean(getApiFieldError(apiError, "name"))} aria-describedby={getApiFieldError(apiError, "name") ? "portfolio-name-error" : undefined} /><ApiFieldError id="portfolio-name-error" error={apiError} fieldName="name" /></div>
        <div><Label>{t("portfolioManager.code")}</Label><p className="mt-2 rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">{portfolio?.code || t("portfolioManager.generatedPortfolioCode")}</p></div>
        <div><Label htmlFor="portfolio-description">{t("portfolioManager.description")}</Label><Textarea id="portfolio-description" className="mt-2" value={form.description ?? ""} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></div>
        <div>
          <Label>{t("portfolioManager.owner")}</Label>
          {hasAssignedOwner
            ? <><Input className="mt-2" value={portfolio?.ownerName || "—"} readOnly /><p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.managerChangeViaHandover")}</p></>
            : <><div className="mt-2"><SearchableSelect aria-label={t("portfolioManager.owner")} aria-invalid={Boolean(getApiFieldError(apiError, "ownerId"))} aria-describedby={getApiFieldError(apiError, "ownerId") ? "portfolio-owner-error" : undefined} options={userOptions} value={form.ownerId} disabled={!canAssignInitialManager || usersQuery.isError} onValueChange={(ownerId) => setForm((current) => ({ ...current, ownerId }))} placeholder={t("portfolioManager.selectPortfolioManager")} searchPlaceholder={t("portfolioManager.searchPortfolioManagers")} loading={usersQuery.isLoading} emptyText={t("portfolioManager.noPortfolioManagers")} /></div><ApiFieldError id="portfolio-owner-error" error={apiError} fieldName="ownerId" />{usersQuery.isError ? <div role="alert" className="mt-1 flex flex-wrap items-center justify-between gap-2 text-xs text-destructive"><span>{usersQuery.error instanceof ApiError && usersQuery.error.status === 403 ? t("portfolioManager.managerLookupAccessDenied") : t("common.errorLoading")}</span><Button type="button" size="sm" variant="outline" onClick={() => void usersQuery.refetch()}>{t("common.retry")}</Button></div> : null}{portfolio ? <p className="mt-1 text-xs text-muted-foreground">{t(canAssignInitialManager ? "managerHandover.setInitialManager" : "managerHandover.adminInitiateRequired")}</p> : !canAssignInitialManager ? <p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.adminInitiateRequired")}</p> : null}</>}
        </div>
        <div>
          <Label htmlFor="portfolio-strategic-objective">{t("portfolioManager.strategicObjective")}</Label>
          <Textarea id="portfolio-strategic-objective" className="mt-2" value={form.strategicObjective ?? ""} onChange={(event) => setForm((current) => ({ ...current, strategicObjective: event.target.value }))} />
        </div>
        <div><Label htmlFor="portfolio-priority">{t("portfolioManager.priority")}</Label><Input id="portfolio-priority" className="mt-2" value={form.priority ?? ""} onChange={(event) => setForm((current) => ({ ...current, priority: event.target.value }))} /></div>
        <div>
          <Label htmlFor="portfolio-currency">{t("portfolioManager.currency")}</Label>
          <select id="portfolio-currency" className="input-field mt-2" value={form.currency ?? ""} required disabled={isPending || currenciesQuery.isLoading || currenciesQuery.isError || currencyOptions.length === 0} aria-invalid={Boolean(getApiFieldError(apiError, "currency"))} aria-describedby={getApiFieldError(apiError, "currency") ? "portfolio-currency-error" : undefined} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value }))}>
            <option value="">{t("common.select")}</option>
            {portfolio?.currency && !currencyOptions.some((currency) => currency.code.toUpperCase() === portfolio.currency?.trim().toUpperCase())
              ? <option value={portfolio.currency} disabled>{portfolio.currency} ({t("administration.settingsWorkspace.currencyInactive")})</option>
              : null}
            {currencyOptions.map((currency) => <option key={currency.code} value={currency.code}>{currency.code} - {currency.name} {currency.symbol ? `(${currency.symbol})` : ""}</option>)}
          </select>
          <ApiFieldError id="portfolio-currency-error" error={apiError} fieldName="currency" />
          {currenciesQuery.isLoading ? <p role="status" className="mt-1 text-xs text-muted-foreground">{t("common.loading")}</p> : null}
          {currenciesQuery.isError ? <div role="alert" className="mt-1 flex flex-wrap items-center justify-between gap-2 text-xs text-destructive"><span>{t("common.errorLoading")}</span><Button type="button" size="sm" variant="outline" onClick={() => void currenciesQuery.refetch()}>{t("common.retry")}</Button></div> : null}
          {!currenciesQuery.isLoading && !currenciesQuery.isError && currencyOptions.length === 0 ? <p role="status" className="mt-1 text-xs text-muted-foreground">{t("administration.currencies.empty")}</p> : null}
        </div>
        <div><Label htmlFor="portfolio-status">{t("portfolioManager.status")}</Label><select id="portfolio-status" className="input-field mt-2" value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as SavePortfolioDto["status"] }))}><option value="Active">{t("portfolioManager.statuses.active")}</option><option value="Archived">{t("portfolioManager.statuses.archived")}</option></select></div>
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => handleOpenChange(false)} disabled={isPending}>{t("common.cancel")}</Button>
        <Button type="submit" disabled={isPending || !canSave || !canSaveCurrency || (!portfolio && !form.organizationId)}>{isPending ? t("portfolioManager.saving") : t("portfolioManager.savePortfolio")}</Button>
      </SideSheetFooter>
    </form>
  </SideSheet>;
}
