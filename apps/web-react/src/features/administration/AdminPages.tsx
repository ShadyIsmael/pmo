import { Link } from "react-router-dom";
import { Coins, ListFilter, Tag, UsersRound, ArrowUpRight, Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useCurrencies, useCreateCurrency, useUpdateCurrency, useDeleteCurrency, usePlatformSettings, useSavePlatformSettings, type CurrencyDto, type SaveCurrencyInput } from "./api/admin";
import { useAuth } from "@auth/AuthProvider";
import { DirectoryAccessPermissions, hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import * as React from "react";

export function ReferenceDataPage() {
  const { t } = useI18n();
  const area = "administration.areas.referenceData";
  const items = [
    { id: "project-roles", href: "/administration/reference-data/project-roles", icon: UsersRound, title: t(`${area}.projectRoles.title`), description: t(`${area}.projectRoles.description`) },
    { id: "vendor-categories", href: "/administration/reference-data/vendor-categories", icon: Tag, title: t(`${area}.vendorCategories.title`), description: t(`${area}.vendorCategories.description`) },
    { id: "work-item-statuses", href: "/administration/reference-data/work-item-statuses", icon: ListFilter, title: t(`${area}.workItemStatuses.title`), description: t(`${area}.workItemStatuses.description`) },
    { id: "currencies", href: "/administration/currencies", icon: Coins, title: t(`${area}.currencies.title`), description: t(`${area}.currencies.description`) },
  ];
  return (
    <div className="space-y-6">
      <PageHeader title={t(`${area}.title`)} description={t(`${area}.description`)} />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {items.map(({ id, href, icon: Icon, title, description }) => <Link key={id} to={href} className="group">
          <Card className="h-full transition-all group-hover:-translate-y-0.5 group-hover:border-primary/30">
            <CardHeader className="gap-4"><div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-5 w-5" /></div><div><CardTitle className="text-base">{title}</CardTitle><CardDescription className="mt-1.5 text-xs leading-5">{description}</CardDescription></div></CardHeader>
            <CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("administration.home.open")}</span><ArrowUpRight className="h-4 w-4" /></div></CardContent>
          </Card>
        </Link>)}
      </div>
    </div>
  );
}

export function PlatformSettingsPage() {
  const { t } = useI18n();
  const settings = usePlatformSettings(true);
  const save = useSavePlatformSettings();
  const [appName, setAppName] = React.useState("");
  React.useEffect(() => {
    if (settings.data?.["app.name"]) setAppName(settings.data["app.name"]);
  }, [settings.data]);
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.settings.title")} description={t("administration.settings.description")} />
      <Card><CardHeader><CardTitle>{t("administration.settings.general")}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1.5"><Label>{t("administration.settings.appName")}</Label><Input value={appName} onChange={(e) => setAppName(e.target.value)} /></div>
          <Button disabled={save.isPending} onClick={() => save.mutate({ "app.name": appName })}>{t("common.save")}</Button>
        </CardContent>
      </Card>
    </div>
  );
}

export function IntegrationsPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const canViewDirectory = Boolean(user && hasAnyPermission(user.permissions, DirectoryAccessPermissions));
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.integrations.title")} description={t("administration.integrations.description")} />
      {canViewDirectory ? <Link to="/administration/integrations/directory" className="group block">
        <Card className="transition-colors group-hover:border-primary/40">
          <CardHeader className="flex-row items-center gap-4 space-y-0">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary/10 text-primary"><UsersRound className="h-5 w-5" /></div>
            <div className="min-w-0 flex-1"><CardTitle className="text-base">{t("administration.integrations.directory.title")}</CardTitle><CardDescription className="mt-1">{t("administration.integrations.directory.description")}</CardDescription></div>
            <ArrowUpRight className="h-4 w-4 text-muted-foreground transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" />
          </CardHeader>
        </Card>
      </Link> : <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("administration.integrations.empty")}</CardContent></Card>}
    </div>
  );
}

export function GovernancePage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const canRecoverAssignments = Boolean(user && hasAnyPermission(user.permissions, [Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage]));
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.governance.title")} description={t("administration.governance.description")} />
      {canRecoverAssignments ? <Link to="/administration/governance/entity-assignment-recovery" className="group block max-w-3xl">
        <Card className="transition-colors group-hover:border-primary/40">
          <CardHeader className="flex-row items-center gap-4 space-y-0">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary/10 text-primary"><UsersRound className="h-5 w-5" /></div>
            <div className="min-w-0 flex-1"><CardTitle className="text-base">{t("administration.governance.assignmentRecovery.title")}</CardTitle><CardDescription className="mt-1">{t("administration.governance.assignmentRecovery.description")}</CardDescription></div>
            <ArrowUpRight className="h-4 w-4 text-muted-foreground transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" />
          </CardHeader>
          <CardContent><div className="border-t pt-3 text-xs font-semibold text-primary">{t("administration.governance.assignmentRecovery.open")}</div></CardContent>
        </Card>
      </Link> : <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("administration.governance.empty")}</CardContent></Card>}
    </div>
  );
}

export function CurrenciesPage() {
  const { t } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write));
  const query = useCurrencies(true);
  const create = useCreateCurrency();
  const update = useUpdateCurrency();
  const remove = useDeleteCurrency();
  const [isFormOpen, setIsFormOpen] = React.useState(false);
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [draft, setDraft] = React.useState<SaveCurrencyInput>({ code: "", name: "", symbol: "", isBase: false });
  const [formError, setFormError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const submitInFlight = React.useRef(false);
  const isSaving = create.isPending || update.isPending;
  const currencies = React.useMemo(() => [...(query.data ?? [])].sort((left, right) => Number(right.isBase) - Number(left.isBase) || left.code.localeCompare(right.code)), [query.data]);

  function beginCreate() {
    setIsFormOpen(true);
    setEditingId(null);
    setDraft({ code: "", name: "", symbol: "", isBase: false });
    setFormError(null);
    setActionError(null);
  }
  function beginEdit(currency: CurrencyDto) {
    setIsFormOpen(true);
    setEditingId(currency.id);
    setDraft({ code: currency.code, name: currency.name, symbol: currency.symbol, isBase: currency.isBase });
    setFormError(null);
    setActionError(null);
  }
  function cancelEdit() {
    setIsFormOpen(false);
    setEditingId(null);
    setDraft({ code: "", name: "", symbol: "", isBase: false });
    setFormError(null);
  }
  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage || submitInFlight.current) return;
    submitInFlight.current = true;
    setFormError(null);
    const input = { ...draft, code: draft.code.trim().toUpperCase(), name: draft.name.trim(), symbol: draft.symbol.trim() };
    try {
      if (editingId) await update.mutateAsync({ id: editingId, input });
      else await create.mutateAsync(input);
      cancelEdit();
    } catch (error) {
      setFormError(error instanceof Error ? error.message : t("administration.currencies.saveError"));
    } finally {
      submitInFlight.current = false;
    }
  }
  async function handleDelete(currency: CurrencyDto) {
    if (currency.isBase || !canManage || !await confirm({ description: t("administration.currencies.deleteConfirm", { code: currency.code }), destructive: true })) return;
    setActionError(null);
    try {
      await remove.mutateAsync(currency.id);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : t("administration.currencies.deleteError"));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.currencies.title")} description={t("administration.currencies.description")} actions={canManage ? <Button size="sm" onClick={beginCreate}><Plus className="me-2 h-4 w-4" />{t("administration.currencies.add")}</Button> : undefined} />
      {isFormOpen ? <Card>
        <CardHeader><CardTitle className="text-base">{editingId ? t("administration.currencies.edit") : t("administration.currencies.add")}</CardTitle><CardDescription>{t("administration.currencies.formDescription")}</CardDescription></CardHeader>
        <CardContent><form className="grid gap-4 md:grid-cols-4" onSubmit={(event) => void handleSubmit(event)}>
          <div className="space-y-2"><Label htmlFor="currency-code">{t("administration.currencies.code")}</Label><Input id="currency-code" value={draft.code} maxLength={3} onChange={(event) => setDraft((current) => ({ ...current, code: event.target.value.toUpperCase() }))} required /></div>
          <div className="space-y-2"><Label htmlFor="currency-name">{t("administration.currencies.name")}</Label><Input id="currency-name" value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} required /></div>
          <div className="space-y-2"><Label htmlFor="currency-symbol">{t("administration.currencies.symbol")}</Label><Input id="currency-symbol" value={draft.symbol} onChange={(event) => setDraft((current) => ({ ...current, symbol: event.target.value }))} required /></div>
          <label htmlFor="currency-base" className="flex items-center gap-3 pt-7"><input id="currency-base" type="checkbox" checked={draft.isBase} onChange={(event) => setDraft((current) => ({ ...current, isBase: event.target.checked }))} className="h-4 w-4 accent-primary" /><span className="text-sm">{t("administration.currencies.baseCurrency")}</span></label>
          {formError ? <p role="alert" className="text-sm text-destructive md:col-span-4">{formError}</p> : null}
          <div className="flex gap-2 md:col-span-4"><Button type="submit" disabled={isSaving}>{isSaving ? t("administration.currencies.saving") : t("administration.currencies.save")}</Button><Button type="button" variant="outline" onClick={cancelEdit} disabled={isSaving}>{t("administration.currencies.cancel")}</Button></div>
        </form></CardContent>
      </Card> : null}
      <Card><CardHeader><CardTitle className="text-base">{t("administration.currencies.listTitle")}</CardTitle></CardHeader><CardContent>
        {actionError ? <p role="alert" className="mb-3 text-sm text-destructive">{actionError}</p> : null}
        {query.isLoading ? <div className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</div> : query.isError ? <div className="flex flex-col items-center gap-3 py-8 text-center"><p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p><Button type="button" size="sm" variant="outline" onClick={() => void query.refetch()}>{t("administration.workItemStatuses.retry")}</Button></div> : currencies.length > 0 ? <div className="space-y-2">{currencies.map((currency) => <div key={currency.id} className="flex flex-col gap-3 rounded-lg border p-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-center gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-muted text-sm font-semibold"><Coins className="h-4 w-4" /></div><div className="min-w-0"><p className="truncate text-sm font-medium">{currency.code} · {currency.name}</p><p className="mt-1 text-xs text-muted-foreground">{currency.symbol}{currency.isBase ? ` · ${t("administration.currencies.baseCurrency")}` : ""}</p></div></div>
          {canManage ? <div className="flex items-center gap-2"><Button type="button" size="sm" variant="outline" onClick={() => beginEdit(currency)} disabled={isSaving || remove.isPending}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("administration.currencies.edit")}</Button><Button type="button" size="icon" variant="ghost" disabled={currency.isBase || remove.isPending} onClick={() => void handleDelete(currency)} aria-label={`${t("administration.currencies.delete")} ${currency.code}`} title={t("administration.currencies.delete")}><Trash2 className="h-4 w-4 text-destructive" /></Button></div> : null}
        </div>)}</div> : <p className="py-8 text-center text-sm text-muted-foreground">{t("administration.currencies.empty")}</p>}
      </CardContent></Card>
    </div>
  );
}
