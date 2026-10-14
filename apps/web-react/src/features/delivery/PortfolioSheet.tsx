import * as React from "react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Textarea } from "@components/ui/Textarea";
import { useCurrencies } from "@features/administration/api/admin";
import { useUserLookup, toUserSelectOptions } from "@features/projects/lib/project-lookups";
import { useI18n } from "@i18n/messages";
import { useCreatePortfolio, useUpdatePortfolio, type PortfolioDto, type SavePortfolioDto } from "./api/delivery";

const blankForm = (): SavePortfolioDto => ({ name: "", description: "", ownerId: "", status: "Active", currency: "USD" });

export function PortfolioSheet({ open, onOpenChange, portfolio, canManage }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  portfolio?: PortfolioDto;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const [form, setForm] = React.useState<SavePortfolioDto>(blankForm);
  const [error, setError] = React.useState("");
  const hasAssignedOwner = Boolean(portfolio?.ownerId && portfolio.ownerId !== "00000000-0000-0000-0000-000000000000");
  const usersQuery = useUserLookup("portfolio.manager", open && (!portfolio || !hasAssignedOwner));
  const currenciesQuery = useCurrencies(open);
  const create = useCreatePortfolio();
  const update = useUpdatePortfolio();
  const isPending = create.isPending || update.isPending;

  React.useEffect(() => {
    if (!open) return;
    setForm(portfolio ? {
      name: portfolio.name,
      description: portfolio.description ?? "",
      ownerId: portfolio.ownerId && portfolio.ownerId !== "00000000-0000-0000-0000-000000000000" ? portfolio.ownerId : "",
      status: portfolio.status === "Archived" ? "Archived" : "Active",
      currency: portfolio.currency || "USD",
    } : blankForm());
    setError("");
  }, [open, portfolio]);

  const userOptions = toUserSelectOptions(usersQuery.data);
  const currencyOptions = currenciesQuery.data ?? [];

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError("");
    if (!canManage) return;
    if (!form.name.trim() || !form.ownerId) {
      setError(t("portfolioManager.portfolioNameManagerRequired"));
      return;
    }
    const payload: SavePortfolioDto = { ...form, ownerId: hasAssignedOwner ? portfolio?.ownerId ?? form.ownerId : form.ownerId, name: form.name.trim(), currency: form.currency || "USD" };
    try {
      if (portfolio) await update.mutateAsync({ id: portfolio.id, dto: payload });
      else await create.mutateAsync(payload);
      onOpenChange(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("portfolioManager.portfolioSaveFailed"));
    }
  };

  return <SideSheet open={open} onOpenChange={onOpenChange} title={portfolio ? t("portfolioManager.editPortfolio") : t("portfolioManager.createPortfolio")} description={t("portfolioManager.portfolioSheetDescription")} size="md">
    <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        <div><Label htmlFor="portfolio-name">{t("portfolioManager.portfolioName")} *</Label><Input id="portfolio-name" className="mt-2" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} required /></div>
        <div><Label>{t("portfolioManager.code")}</Label><p className="mt-2 rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">{portfolio?.code || t("portfolioManager.generatedPortfolioCode")}</p></div>
        <div><Label htmlFor="portfolio-description">{t("portfolioManager.description")}</Label><Textarea id="portfolio-description" className="mt-2" value={form.description ?? ""} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></div>
        <div>
          <Label>{t("portfolioManager.owner")} *</Label>
          {hasAssignedOwner
            ? <><Input className="mt-2" value={portfolio?.ownerName || "—"} readOnly /><p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.managerChangeViaHandover")}</p></>
            : <><div className="mt-2"><SearchableSelect options={userOptions} value={form.ownerId} onValueChange={(ownerId) => setForm((current) => ({ ...current, ownerId }))} placeholder={t("portfolioManager.selectPortfolioManager")} searchPlaceholder={t("portfolioManager.searchPortfolioManagers")} loading={usersQuery.isLoading} emptyText={t("portfolioManager.noPortfolioManagers")} /></div>{portfolio ? <p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.setInitialManager")}</p> : null}</>}
        </div>
        <div>
          <Label htmlFor="portfolio-currency">{t("portfolioManager.currency")}</Label>
          {currencyOptions.length > 0
            ? <select id="portfolio-currency" className="input-field mt-2" value={form.currency || "USD"} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value }))}>{currencyOptions.map((currency) => <option key={currency.code} value={currency.code}>{currency.code} - {currency.name} {currency.symbol ? `(${currency.symbol})` : ""}</option>)}</select>
            : <Input id="portfolio-currency" className="mt-2" value={form.currency || "USD"} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} />}
        </div>
        <div><Label htmlFor="portfolio-status">{t("portfolioManager.status")}</Label><select id="portfolio-status" className="input-field mt-2" value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as SavePortfolioDto["status"] }))}><option value="Active">{t("portfolioManager.statuses.active")}</option><option value="Archived">{t("portfolioManager.statuses.archived")}</option></select></div>
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>{t("common.cancel")}</Button>
        <Button type="submit" disabled={isPending || !canManage}>{isPending ? t("portfolioManager.saving") : t("portfolioManager.savePortfolio")}</Button>
      </SideSheetFooter>
    </form>
  </SideSheet>;
}
