import * as React from "react";
import { Loader2, Plus } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Skeleton } from "@components/ui/Skeleton";
import { useI18n } from "@i18n/messages";
import { useCostProfiles, useCreateCostProfile, useResourceReferenceData, type ResourceDto } from "./api/resources";
import { useCurrencies } from "@features/administration/api/admin";

interface ResourceCostProfilesSheetProps {
  open: boolean;
  resource: ResourceDto | null;
  canView: boolean;
  canManage: boolean;
  onOpenChange: (open: boolean) => void;
}

interface CostProfileForm {
  amount: string;
  compensationType: string;
  currency: string;
  effectiveFrom: string;
  effectiveTo: string;
}

function emptyForm(): CostProfileForm {
  return {
    amount: "",
    compensationType: "",
    currency: "",
    effectiveFrom: new Date().toISOString().slice(0, 10),
    effectiveTo: "",
  };
}

function dateForDisplay(value: string, formatDate: (date: string) => string): string {
  const date = value.slice(0, 10);
  return formatDate(`${date}T12:00:00`);
}

export function ResourceCostProfilesSheet({ open, resource, canView, canManage, onOpenChange }: ResourceCostProfilesSheetProps) {
  const { t, formatCurrency, formatDate } = useI18n();
  const resourceId = resource?.id ?? "";
  const profilesQuery = useCostProfiles(resourceId, open && canView && Boolean(resourceId));
  const currenciesQuery = useCurrencies(open && canView && canManage);
  const referenceDataQuery = useResourceReferenceData(open && canView && canManage);
  const createMutation = useCreateCostProfile();
  const [form, setForm] = React.useState<CostProfileForm>(emptyForm);
  const [formError, setFormError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!open) return;
    setForm(emptyForm());
    setFormError(null);
    createMutation.reset();
  }, [open, resourceId]);

  React.useEffect(() => {
    if (!open || !canManage || form.currency || !currenciesQuery.data?.length) return;
    setForm((current) => ({ ...current, currency: currenciesQuery.data.find((currency) => currency.isBase)?.code ?? currenciesQuery.data[0].code }));
  }, [open, canManage, form.currency, currenciesQuery.data]);

  React.useEffect(() => {
    if (!open || !canManage || form.compensationType || !referenceDataQuery.data?.compensationTypes.length) return;
    setForm((current) => ({ ...current, compensationType: referenceDataQuery.data!.compensationTypes[0] }));
  }, [open, canManage, form.compensationType, referenceDataQuery.data]);

  const close = () => {
    setFormError(null);
    createMutation.reset();
    onOpenChange(false);
  };

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    const amount = Number(form.amount);
    const currency = form.currency.trim();

    if (!resourceId || !Number.isFinite(amount) || amount <= 0) {
      setFormError(t("resources.costProfiles.invalidAmount"));
      return;
    }
    if (!referenceDataQuery.data?.compensationTypes.includes(form.compensationType)) {
      setFormError(t("resources.costProfiles.invalidCompensationType"));
      return;
    }
    if (!currency || !currenciesQuery.data?.some((item) => item.code === currency)) {
      setFormError(t("resources.costProfiles.invalidCurrency"));
      return;
    }
    if (!form.effectiveFrom || (form.effectiveTo && form.effectiveTo < form.effectiveFrom)) {
      setFormError(t("resources.costProfiles.invalidDateRange"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        resourceId,
        body: {
          amount,
          compensationType: form.compensationType,
          currency: currency.toUpperCase(),
          effectiveFrom: form.effectiveFrom,
          effectiveTo: form.effectiveTo || undefined,
          overtimeEligible: false,
        },
      });
      close();
    } catch {
      // The mutation error is rendered in the sheet; keep the form open for correction/retry.
    }
  };

  const mutationError = createMutation.error instanceof Error
    ? createMutation.error.message
    : createMutation.error
      ? t("resources.costProfiles.saveFailed")
      : null;

  return (
    <SideSheet
      open={open && canView && Boolean(resource)}
      onOpenChange={(nextOpen) => { if (!nextOpen) close(); }}
      title={t("resources.costProfiles.title", { name: resource?.name ?? "" })}
      description={t("resources.costProfiles.description")}
    >
      <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-5">
          {!canView ? <p role="alert" className="text-sm text-destructive">{t("resources.costProfiles.permissionRequired")}</p> : null}

          {canManage ? (
            <section className="space-y-4 rounded-lg border p-4">
              <h3 className="text-sm font-semibold">{t("resources.costProfiles.addTitle")}</h3>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="resource-cost-amount">{t("resources.costProfiles.amount")}</Label>
                  <Input id="resource-cost-amount" type="number" min="0.01" step="0.01" required value={form.amount} onChange={(event) => setForm((current) => ({ ...current, amount: event.target.value }))} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="resource-cost-currency">{t("resources.costProfiles.currency")}</Label>
                  <Select value={form.currency} onValueChange={(value) => setForm((current) => ({ ...current, currency: value }))}>
                    <SelectTrigger id="resource-cost-currency" aria-required="true"><SelectValue placeholder={t("resources.costProfiles.selectCurrency")} /></SelectTrigger>
                    <SelectContent>
                      {currenciesQuery.data?.map((currency) => <SelectItem key={currency.id} value={currency.code}>{currency.code} · {currency.name}{currency.isBase ? ` · ${t("administration.currencies.baseCurrency")}` : ""}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  {currenciesQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("resources.costProfiles.loadingCurrencies")}</p> : null}
                  {currenciesQuery.isError ? <div className="flex items-center gap-2 text-xs text-destructive"><span>{t("resources.costProfiles.currenciesFailed")}</span><Button type="button" size="sm" variant="outline" onClick={() => void currenciesQuery.refetch()}>{t("common.retry")}</Button></div> : null}
                  {currenciesQuery.isSuccess && currenciesQuery.data.length === 0 ? <p className="text-xs text-muted-foreground">{t("resources.costProfiles.noCurrencies")}</p> : null}
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label>{t("resources.costProfiles.compensationType")}</Label>
                  <Select value={form.compensationType} onValueChange={(value) => setForm((current) => ({ ...current, compensationType: value }))}>
                    <SelectTrigger aria-label={t("resources.costProfiles.compensationType")}><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {referenceDataQuery.data?.compensationTypes.map((type) => <SelectItem key={type} value={type}>{t(`resources.costProfiles.${type.toLowerCase()}`)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  {referenceDataQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("common.loading")}</p> : null}
                  {referenceDataQuery.isError ? <div className="flex items-center gap-2 text-xs text-destructive"><span>{t("resources.costProfiles.referenceDataFailed")}</span><Button type="button" size="sm" variant="outline" onClick={() => void referenceDataQuery.refetch()}>{t("common.retry")}</Button></div> : null}
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="resource-cost-from">{t("resources.costProfiles.effectiveFrom")}</Label>
                  <Input id="resource-cost-from" type="date" required value={form.effectiveFrom} onChange={(event) => setForm((current) => ({ ...current, effectiveFrom: event.target.value }))} />
                </div>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="resource-cost-to">{t("resources.costProfiles.effectiveTo")}</Label>
                <Input id="resource-cost-to" type="date" min={form.effectiveFrom || undefined} value={form.effectiveTo} onChange={(event) => setForm((current) => ({ ...current, effectiveTo: event.target.value }))} />
                <p className="text-xs text-muted-foreground">{t("resources.costProfiles.effectiveToHint")}</p>
              </div>
              {formError || mutationError ? <p role="alert" className="text-sm text-destructive">{formError ?? mutationError}</p> : null}
              <Button type="submit" disabled={createMutation.isPending || !form.amount || !form.effectiveFrom || currenciesQuery.isLoading || currenciesQuery.isError || !currenciesQuery.data?.length || referenceDataQuery.isLoading || referenceDataQuery.isError || !referenceDataQuery.data?.compensationTypes.length}>
                {createMutation.isPending ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{t("common.saving")}</> : <><Plus className="me-2 h-4 w-4" />{t("resources.costProfiles.addAction")}</>}
              </Button>
            </section>
          ) : null}

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">{t("resources.costProfiles.historyTitle")}</h3>
            {profilesQuery.isLoading ? <div className="space-y-2">{[1, 2].map((item) => <Skeleton key={item} className="h-16 w-full" />)}</div> : null}
            {profilesQuery.isError ? <div role="alert" className="space-y-2 text-sm text-destructive"><p>{t("resources.costProfiles.loadFailed")}</p><Button type="button" variant="outline" size="sm" onClick={() => void profilesQuery.refetch()}>{t("common.retry")}</Button></div> : null}
            {profilesQuery.isSuccess && profilesQuery.data.length === 0 ? <p className="text-sm text-muted-foreground">{t("resources.costProfiles.empty")}</p> : null}
            {profilesQuery.data?.map((profile) => (
              <article key={profile.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
                <div>
                  <p className="font-medium text-success">{formatCurrency(profile.amount, profile.currency)} · {t(`resources.costProfiles.${profile.compensationType.toLowerCase()}`)}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {dateForDisplay(profile.effectiveFrom, formatDate)} – {profile.effectiveTo ? dateForDisplay(profile.effectiveTo, formatDate) : t("resources.costProfiles.openEnded")}
                  </p>
                </div>
              </article>
            ))}
          </section>
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={close}>{t("common.close")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  );
}
