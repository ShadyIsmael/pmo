import * as React from "react";
import { Link } from "react-router-dom";
import { ArrowDown, ArrowUp, Check, Info, ListFilter, Loader2, Pencil, Plus, Power, Search, ShieldCheck, Star, X, type LucideIcon } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { getStatusTextClass } from "@lib/status-presentation";
import {
  useCreateWorkItemStatus,
  useUpdateWorkItemStatus,
  useWorkItemStatuses,
  type SaveWorkItemStatusInput,
  type WorkItemStatusDto,
} from "./api/admin";

const WORK_ITEM_STATUS_COLORS = ["slate", "blue", "amber", "violet", "emerald", "rose", "orange"] as const;
type WorkItemStatusColor = (typeof WORK_ITEM_STATUS_COLORS)[number];
type ViewFilter = "all" | "active" | "inactive";

const COLOR_CLASSES: Record<WorkItemStatusColor, { swatch: string; text: string }> = {
  slate: { swatch: "bg-slate-500", text: "text-slate-600 dark:text-slate-300" },
  blue: { swatch: "bg-blue-500", text: "text-blue-700 dark:text-blue-300" },
  amber: { swatch: "bg-amber-500", text: "text-amber-700 dark:text-amber-300" },
  violet: { swatch: "bg-violet-500", text: "text-violet-700 dark:text-violet-300" },
  emerald: { swatch: "bg-emerald-500", text: "text-emerald-700 dark:text-emerald-300" },
  rose: { swatch: "bg-rose-500", text: "text-rose-700 dark:text-rose-300" },
  orange: { swatch: "bg-orange-500", text: "text-orange-700 dark:text-orange-300" },
};

type StatusForm = SaveWorkItemStatusInput;
const EMPTY_FORM: StatusForm = { code: "", nameEn: "", nameAr: "", displayOrder: 10, isActive: true, color: "blue", isDefault: false };

function colorOf(value?: string): WorkItemStatusColor {
  return WORK_ITEM_STATUS_COLORS.find((color) => color === value) ?? "slate";
}

function requestFor(status: WorkItemStatusDto, changes: Partial<StatusForm> = {}): StatusForm {
  return {
    code: status.code,
    nameEn: status.nameEn,
    nameAr: status.nameAr,
    displayOrder: status.displayOrder,
    isActive: status.isActive,
    color: status.color ?? "slate",
    isDefault: status.isDefault,
    ...changes,
  };
}

function saveError(error: unknown, t: (key: string) => string): string {
  const message = error instanceof Error ? error.message.toLowerCase() : "";
  if (message.includes("currently used") && message.includes("deactivated")) return t("administration.workItemStatuses.errors.usedStatus");
  if (message.includes("used") || message.includes("cannot be changed")) return t("administration.workItemStatuses.errors.usedCode");
  if (message.includes("active work item status")) return t("administration.workItemStatuses.errors.finalActive");
  if (message.includes("default") && message.includes("active")) return t("administration.workItemStatuses.errors.invalidDefault");
  if (message.includes("already exists") || message.includes("duplicate")) return t("administration.workItemStatuses.errors.conflict");
  return t("administration.workItemStatuses.errors.unknown");
}

function SummaryCard({ label, value, icon: Icon, tone }: { label: string; value: string | number; icon: LucideIcon; tone: string }) {
  return <Card className="shadow-none"><CardContent className="flex items-center gap-3 p-4"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-muted"><Icon className={`h-4 w-4 ${tone}`} /></div><div className="min-w-0"><p className="truncate text-xs text-muted-foreground">{label}</p><p className={`mt-1 text-xl font-semibold tabular-nums ${tone}`}>{value}</p></div></CardContent></Card>;
}

export function WorkItemStatusesPage() {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write));
  const statusesQuery = useWorkItemStatuses(true);
  const createMutation = useCreateWorkItemStatus();
  const updateMutation = useUpdateWorkItemStatus();
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<WorkItemStatusDto | null>(null);
  const [form, setForm] = React.useState<StatusForm>(EMPTY_FORM);
  const [formError, setFormError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [pendingAction, setPendingAction] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState("");
  const [viewFilter, setViewFilter] = React.useState<ViewFilter>("all");

  const statuses = React.useMemo(
    () => [...(statusesQuery.data ?? [])].sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)),
    [statusesQuery.data],
  );
  const visibleStatuses = React.useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    return statuses.filter((status) => {
      const matchesFilter = viewFilter === "all" || (viewFilter === "active" ? status.isActive : !status.isActive);
      const matchesSearch = !query || [status.code, status.nameEn, status.nameAr].some((value) => value.toLocaleLowerCase().includes(query));
      return matchesFilter && matchesSearch;
    });
  }, [search, statuses, viewFilter]);
  const activeCount = statuses.filter((status) => status.isActive).length;
  const inactiveCount = statuses.length - activeCount;
  const defaultStatus = statuses.find((status) => status.isDefault);
  const defaultStatusName = defaultStatus ? (locale === "ar" ? defaultStatus.nameAr : defaultStatus.nameEn) : "—";
  const isSaving = createMutation.isPending || updateMutation.isPending;

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (statuses.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setActionError(null);
    setSheetOpen(true);
  }

  function openEdit(status: WorkItemStatusDto) {
    setEditing(status);
    setForm(requestFor(status));
    setFormError(null);
    setActionError(null);
    setSheetOpen(true);
  }

  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) {
      setEditing(null);
      setFormError(null);
    }
  }

  function updateForm<K extends keyof StatusForm>(key: K, value: StatusForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing && !form.code.trim()) return setFormError(t("administration.workItemStatuses.validation.codeRequired"));
    if (!form.nameEn.trim()) return setFormError(t("administration.workItemStatuses.validation.nameEnRequired"));
    if (!form.nameAr.trim()) return setFormError(t("administration.workItemStatuses.validation.nameArRequired"));
    if (!Number.isInteger(form.displayOrder) || form.displayOrder < 0) return setFormError(t("administration.workItemStatuses.validation.orderRequired"));

    setFormError(null);
    const body = { ...form, code: form.code.trim(), nameEn: form.nameEn.trim(), nameAr: form.nameAr.trim(), color: colorOf(form.color) };
    try {
      if (editing) await updateMutation.mutateAsync({ id: editing.id, input: body });
      else await createMutation.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      setFormError(saveError(error, t));
    }
  }

  async function updateStatus(status: WorkItemStatusDto, changes: Partial<StatusForm>, actionId: string) {
    setActionError(null);
    setPendingAction(actionId);
    try {
      await updateMutation.mutateAsync({ id: status.id, input: requestFor(status, changes) });
    } catch (error) {
      setActionError(saveError(error, t));
    } finally {
      setPendingAction(null);
    }
  }

  async function toggleActive(status: WorkItemStatusDto) {
    if (status.isActive && !window.confirm(t("administration.workItemStatuses.deactivateConfirm", { name: locale === "ar" ? status.nameAr : status.nameEn }))) return;
    await updateStatus(status, { isActive: !status.isActive, isDefault: status.isActive ? false : status.isDefault }, `active-${status.id}`);
  }

  async function setDefault(status: WorkItemStatusDto) {
    if (!status.isActive || status.isDefault) return;
    await updateStatus(status, { isDefault: true }, `default-${status.id}`);
  }

  async function moveStatus(status: WorkItemStatusDto, direction: -1 | 1) {
    const index = statuses.findIndex((candidate) => candidate.id === status.id);
    const neighbor = statuses[index + direction];
    if (!neighbor) return;
    setActionError(null);
    setPendingAction(`order-${status.id}`);
    try {
      await updateMutation.mutateAsync({ id: status.id, input: requestFor(status, { displayOrder: neighbor.displayOrder }) });
      await updateMutation.mutateAsync({ id: neighbor.id, input: requestFor(neighbor, { displayOrder: status.displayOrder }) });
    } catch (error) {
      setActionError(saveError(error, t));
    } finally {
      setPendingAction(null);
    }
  }

  const tabs = [
    { to: "/administration/reference-data/work-item-statuses", label: t("administration.workItemStatuses.tabs.workItemStatuses"), active: true },
    { to: "/administration/currencies", label: t("administration.workItemStatuses.tabs.currencies"), active: false },
    { to: "/administration/reference-data/vendor-categories", label: t("administration.workItemStatuses.tabs.vendorCategories"), active: false },
  ];

  return (
    <div className="space-y-6 pb-8">
      <PageHeader title={t("administration.workItemStatuses.title")} description={t("administration.workItemStatuses.description")} actions={canManage ? <Button size="sm" onClick={openCreate}><Plus className="me-2 h-4 w-4" />{t("administration.workItemStatuses.add")}</Button> : undefined} />
      <nav role="tablist" aria-label={t("administration.workItemStatuses.referenceDataTitle")} className="flex w-fit max-w-full flex-wrap gap-1 rounded-xl border bg-muted/20 p-1">
        {tabs.map((tab) => <Link key={tab.to} to={tab.to} role="tab" aria-selected={tab.active} className={`rounded-lg px-3 py-2 text-sm transition-colors ${tab.active ? "bg-background font-medium text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"}`}>{tab.active ? <ListFilter className="me-1.5 inline h-3.5 w-3.5 text-primary" aria-hidden="true" /> : null}{tab.label}</Link>)}
      </nav>

      <Card className="overflow-hidden border-primary/15 bg-gradient-to-br from-primary/[0.06] via-background to-background shadow-none"><CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div className="flex items-start gap-3"><div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><ListFilter className="h-5 w-5" /></div><div><p className="text-sm font-semibold">{t("administration.workItemStatuses.overviewTitle")}</p><p className="mt-1 max-w-2xl text-sm text-muted-foreground">{t("administration.workItemStatuses.overviewDescription")}</p></div></div>
        <div className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground"><ShieldCheck className="h-4 w-4 text-success" /><span>{t("administration.workItemStatuses.protectedTitle")}</span></div>
      </CardContent></Card>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard label={t("administration.workItemStatuses.totalStatuses")} value={statuses.length} icon={ListFilter} tone="text-primary" />
        <SummaryCard label={t("administration.workItemStatuses.activeStatuses")} value={activeCount} icon={Check} tone="text-success" />
        <SummaryCard label={t("administration.workItemStatuses.inactiveStatuses")} value={inactiveCount} icon={Power} tone="text-muted-foreground" />
        <SummaryCard label={t("administration.workItemStatuses.defaultStatus")} value={defaultStatusName} icon={Star} tone="text-warning" />
      </div>

      <Card className="shadow-none">
        <CardHeader className="gap-3 border-b border-border/60 pb-4 sm:flex-row sm:items-start sm:justify-between"><div><CardTitle>{t("administration.workItemStatuses.listTitle")}</CardTitle><CardDescription className="mt-1">{t("administration.workItemStatuses.protectedDescription")}</CardDescription></div><span className="text-xs font-medium text-muted-foreground">{visibleStatuses.length} / {statuses.length}</span></CardHeader>
        <CardContent className="space-y-4 p-4 sm:p-5">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="relative w-full max-w-md"><Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("administration.workItemStatuses.searchPlaceholder")} className="ps-9" aria-label={t("administration.workItemStatuses.searchPlaceholder")} />{search ? <button type="button" onClick={() => setSearch("")} className="absolute end-2 top-1/2 -translate-y-1/2 rounded p-1 text-muted-foreground hover:bg-muted hover:text-foreground" aria-label={t("administration.workItemStatuses.clearSearch")}><X className="h-3.5 w-3.5" /></button> : null}</div>
            <div className="flex flex-wrap gap-2" role="group" aria-label={t("administration.workItemStatuses.filterStatuses")}>
              {(["all", "active", "inactive"] as const).map((filter) => <Button key={filter} type="button" size="sm" variant={viewFilter === filter ? "default" : "outline"} aria-pressed={viewFilter === filter} onClick={() => setViewFilter(filter)}>{filter === "all" ? t("administration.workItemStatuses.filterAll") : filter === "active" ? t("administration.workItemStatuses.filterActive") : t("administration.workItemStatuses.filterInactive")}</Button>)}
            </div>
          </div>

          {actionError ? <div role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2.5 text-sm text-destructive"><Info className="mt-0.5 h-4 w-4 shrink-0" /><span>{actionError}</span></div> : null}
          {statusesQuery.isLoading ? <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">{t("administration.workItemStatuses.loading")}</div> : statusesQuery.isError ? <div className="flex flex-col items-center gap-3 rounded-lg border border-destructive/20 bg-destructive/[0.03] p-10 text-center"><p role="alert" className="text-sm text-destructive">{t("administration.workItemStatuses.error")}</p><Button type="button" size="sm" variant="outline" onClick={() => void statusesQuery.refetch()}>{t("administration.workItemStatuses.retry")}</Button></div> : (
            <Table>
              <TableHeader><TableRow><TableHead className="min-w-56">{t("administration.workItemStatuses.nameEn")}</TableHead><TableHead>{t("administration.workItemStatuses.code")}</TableHead><TableHead>{t("administration.workItemStatuses.color")}</TableHead><TableHead className="text-center">{t("administration.workItemStatuses.displayOrder")}</TableHead><TableHead>{t("administration.workItemStatuses.default")}</TableHead><TableHead>{t("administration.workItemStatuses.active")}</TableHead>{canManage ? <TableHead className="text-end">{t("administration.workItemStatuses.actions")}</TableHead> : null}</TableRow></TableHeader>
              <TableBody>
                {visibleStatuses.length ? visibleStatuses.map((status) => {
                  const statusIndex = statuses.findIndex((candidate) => candidate.id === status.id);
                  const color = colorOf(status.color);
                  const colorClasses = COLOR_CLASSES[color];
                  const rowBusy = Boolean(pendingAction?.endsWith(status.id)) || updateMutation.isPending;
                  return <TableRow key={status.id}>
                    <TableCell><div className="flex items-center gap-2.5"><span className={`h-3 w-3 shrink-0 rounded-full ${colorClasses.swatch}`} aria-hidden="true" /><div className="min-w-0"><p className="truncate font-medium">{locale === "ar" ? status.nameAr : status.nameEn}</p><p className="truncate text-[11px] text-muted-foreground">{locale === "ar" ? status.nameEn : status.nameAr}</p></div></div></TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">{status.code}</TableCell>
                    <TableCell><span className={`text-xs font-medium ${colorClasses.text}`}>{t(`administration.workItemStatuses.colors.${color}`)}</span></TableCell>
                    <TableCell className="text-center tabular-nums text-muted-foreground">{status.displayOrder}</TableCell>
                    <TableCell>{status.isDefault ? <span className="inline-flex items-center gap-1.5 text-xs font-medium text-primary"><Check className="h-3.5 w-3.5" />{t("administration.workItemStatuses.default")}</span> : <span className="text-muted-foreground">—</span>}</TableCell>
                    <TableCell><span className={`text-xs font-medium ${getStatusTextClass(status.isActive ? "Active" : "Inactive")}`}>{status.isActive ? t("administration.workItemStatuses.active") : t("administration.workItemStatuses.inactive")}</span></TableCell>
                    {canManage ? <TableCell><div className="flex items-center justify-end gap-0.5">
                      <Button type="button" size="icon" variant="ghost" onClick={() => openEdit(status)} aria-label={`${t("administration.workItemStatuses.edit")} ${status.nameEn}`} title={t("administration.workItemStatuses.edit")} disabled={Boolean(pendingAction)}><Pencil className="h-3.5 w-3.5" /></Button>
                      <Button type="button" size="icon" variant="ghost" disabled={rowBusy || !status.isActive || status.isDefault} onClick={() => void setDefault(status)} aria-label={t("administration.workItemStatuses.setDefault")} title={t("administration.workItemStatuses.setDefault")}><Star className={`h-3.5 w-3.5 ${status.isDefault ? "fill-current" : ""}`} /></Button>
                      <Button type="button" size="icon" variant="ghost" disabled={rowBusy || statusIndex === 0} onClick={() => void moveStatus(status, -1)} aria-label={t("administration.workItemStatuses.moveUp")} title={t("administration.workItemStatuses.moveUp")}><ArrowUp className="h-3.5 w-3.5" /></Button>
                      <Button type="button" size="icon" variant="ghost" disabled={rowBusy || statusIndex === statuses.length - 1} onClick={() => void moveStatus(status, 1)} aria-label={t("administration.workItemStatuses.moveDown")} title={t("administration.workItemStatuses.moveDown")}><ArrowDown className="h-3.5 w-3.5" /></Button>
                      <Button type="button" size="sm" variant="ghost" className="ms-1 h-8 gap-1.5 text-xs" disabled={rowBusy} onClick={() => void toggleActive(status)}><Power className={`h-3.5 w-3.5 ${status.isActive ? "text-destructive" : "text-success"}`} />{status.isActive ? t("administration.workItemStatuses.deactivate") : t("administration.workItemStatuses.activate")}</Button>
                    </div></TableCell> : null}
                  </TableRow>;
                }) : <TableRow><TableCell colSpan={canManage ? 7 : 6} className="h-32 text-center"><div className="flex flex-col items-center gap-2"><Search className="h-6 w-6 text-muted-foreground" /><p className="text-sm text-muted-foreground">{search || viewFilter !== "all" ? t("administration.workItemStatuses.noFilteredStatuses") : t("administration.workItemStatuses.empty")}</p>{search || viewFilter !== "all" ? <Button type="button" size="sm" variant="outline" onClick={() => { setSearch(""); setViewFilter("all"); }}>{t("administration.workItemStatuses.clearFilters")}</Button> : null}</div></TableCell></TableRow>}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
      <div className="flex items-start gap-3 rounded-lg border border-border/70 bg-muted/20 px-4 py-3 text-sm text-muted-foreground"><ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-success" /><p>{t("administration.workItemStatuses.noDelete")}</p></div>

      <SideSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t("administration.workItemStatuses.edit") : t("administration.workItemStatuses.add")} description={editing ? t("administration.workItemStatuses.codeHint") : t("administration.workItemStatuses.description")} size="md">
        <form onSubmit={(event) => void save(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-6">
            {formError ? <div role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2.5 text-sm text-destructive"><Info className="mt-0.5 h-4 w-4 shrink-0" /><span>{formError}</span></div> : null}
            <section className="space-y-4"><div><h2 className="text-sm font-semibold">{t("administration.workItemStatuses.formBasics")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("administration.workItemStatuses.description")}</p></div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2"><Label htmlFor="work-item-status-name-en">{t("administration.workItemStatuses.nameEn")}</Label><Input id="work-item-status-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required /></div>
                <div className="space-y-2"><Label htmlFor="work-item-status-name-ar">{t("administration.workItemStatuses.nameAr")}</Label><Input id="work-item-status-name-ar" dir="rtl" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} required /></div>
                <div className="space-y-2"><Label htmlFor="work-item-status-code">{t("administration.workItemStatuses.code")}</Label><Input id="work-item-status-code" value={form.code} onChange={(event) => updateForm("code", event.target.value)} disabled={Boolean(editing)} required={!editing} aria-describedby={editing ? "work-item-status-code-hint" : undefined} />{editing ? <p id="work-item-status-code-hint" className="text-xs text-muted-foreground">{t("administration.workItemStatuses.codeHint")}</p> : null}</div>
                <div className="space-y-2"><Label htmlFor="work-item-status-order">{t("administration.workItemStatuses.displayOrder")}</Label><Input id="work-item-status-order" type="number" min="0" step="1" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} required /><p className="text-xs text-muted-foreground">{t("administration.workItemStatuses.orderHint")}</p></div>
              </div>
            </section>
            <section className="space-y-4 rounded-xl border bg-muted/20 p-4"><div><h2 className="text-sm font-semibold">{t("administration.workItemStatuses.statusPreview")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("administration.workItemStatuses.colorPreview")}</p></div>
              <div className="flex items-center gap-3 rounded-lg border bg-background px-3 py-3"><span className={`h-3 w-3 shrink-0 rounded-full ${COLOR_CLASSES[colorOf(form.color)].swatch}`} aria-hidden="true" /><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{locale === "ar" ? form.nameAr || t("administration.workItemStatuses.nameAr") : form.nameEn || t("administration.workItemStatuses.nameEn")}</p><p className="text-xs text-muted-foreground">{form.code || t("administration.workItemStatuses.code")}</p></div><span className={`text-xs font-medium ${COLOR_CLASSES[colorOf(form.color)].text}`}>{t(`administration.workItemStatuses.colors.${colorOf(form.color)}`)}</span></div>
              <div className="space-y-2"><Label htmlFor="work-item-status-color">{t("administration.workItemStatuses.color")}</Label><select id="work-item-status-color" className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={colorOf(form.color)} onChange={(event) => updateForm("color", colorOf(event.target.value))}>{WORK_ITEM_STATUS_COLORS.map((color) => <option key={color} value={color}>{t(`administration.workItemStatuses.colors.${color}`)}</option>)}</select></div>
            </section>
            <section className="space-y-4"><div><h2 className="text-sm font-semibold">{t("administration.workItemStatuses.formBehavior")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("administration.workItemStatuses.protectedDescription")}</p></div>
              <div className="space-y-3">
                <label htmlFor="work-item-status-active" className="flex cursor-pointer items-start gap-3 rounded-lg border p-3"><input id="work-item-status-active" type="checkbox" checked={form.isActive} onChange={(event) => { updateForm("isActive", event.target.checked); if (!event.target.checked) updateForm("isDefault", false); }} className="mt-1 h-4 w-4 accent-primary" /><span><span className="block text-sm font-medium">{t("administration.workItemStatuses.active")}</span><span className="mt-1 block text-xs text-muted-foreground">{t("administration.workItemStatuses.activeHint")}</span></span></label>
                <label htmlFor="work-item-status-default" className={`flex items-start gap-3 rounded-lg border p-3 ${form.isActive ? "cursor-pointer" : "opacity-60"}`}><input id="work-item-status-default" type="checkbox" checked={form.isDefault} onChange={(event) => updateForm("isDefault", event.target.checked)} disabled={!form.isActive} className="mt-1 h-4 w-4 accent-primary" /><span><span className="block text-sm font-medium">{t("administration.workItemStatuses.default")}</span><span className="mt-1 block text-xs text-muted-foreground">{t("administration.workItemStatuses.defaultHint")}</span></span></label>
              </div>
            </section>
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("administration.workItemStatuses.cancel")}</Button><Button type="submit" disabled={isSaving}>{isSaving ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{t("administration.workItemStatuses.saving")}</> : t("administration.workItemStatuses.save")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
