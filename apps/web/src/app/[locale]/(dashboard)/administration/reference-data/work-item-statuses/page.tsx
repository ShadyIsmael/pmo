"use client";

import { useMemo, useState, type FormEvent } from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import {
  ArrowDown,
  ArrowUp,
  Check,
  Info,
  ListFilter,
  Loader2,
  Pencil,
  Plus,
  Power,
  Search,
  ShieldCheck,
  Star,
  X,
  type LucideIcon,
} from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  useWorkItemStatusMutations,
  useWorkItemStatuses,
  type SaveWorkItemStatusRequest,
  type WorkItemStatusReference,
} from "@/lib/api/reference-data";
import {
  getStatusTextClass,
  getWorkItemStatusColor,
  getWorkItemStatusColorClasses,
  WORK_ITEM_STATUS_COLORS,
} from "@/lib/status-presentation";

type StatusForm = SaveWorkItemStatusRequest;
type ViewFilter = "all" | "active" | "inactive";

const EMPTY_FORM: StatusForm = {
  code: "",
  nameEn: "",
  nameAr: "",
  displayOrder: 10,
  isActive: true,
  color: "blue",
  isDefault: false,
};

function getSaveError(error: unknown, translate: (_key: string) => string): string {
  const message = error instanceof Error ? error.message.toLowerCase() : "";
  if (message.includes("used") || message.includes("cannot be changed")) return translate("errors.usedCode");
  if (message.includes("currently used") && message.includes("deactivated")) return translate("errors.usedStatus");
  if (message.includes("active work item status")) return translate("errors.finalActive");
  if (message.includes("default") && message.includes("active")) return translate("errors.invalidDefault");
  if (message.includes("already exists") || message.includes("duplicate")) return translate("errors.conflict");
  return translate("errors.unknown");
}

function requestFor(status: WorkItemStatusReference, changes: Partial<StatusForm> = {}): StatusForm {
  return {
    code: status.code,
    nameEn: status.nameEn,
    nameAr: status.nameAr,
    displayOrder: status.displayOrder,
    isActive: status.isActive,
    color: status.color,
    isDefault: status.isDefault,
    ...changes,
  };
}

function SummaryCard({ label, value, icon: Icon, tone }: { label: string; value: string | number; icon: LucideIcon; tone: string }) {
  return (
    <Card className="shadow-none">
      <CardContent className="flex items-center gap-3 p-4">
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-muted">
          <Icon className={`h-4 w-4 ${tone}`} />
        </div>
        <div className="min-w-0">
          <p className="truncate text-xs text-muted-foreground">{label}</p>
          <p className={`mt-1 text-xl font-semibold tabular-nums ${tone}`}>{value}</p>
        </div>
      </CardContent>
    </Card>
  );
}

export default function WorkItemStatusesPage() {
  const locale = useLocale();
  const t = useTranslations("administration.workItemStatuses");
  const statusesQuery = useWorkItemStatuses(true);
  const mutations = useWorkItemStatusMutations();
  const [sheetOpen, setSheetOpen] = useState(false);
  const [editing, setEditing] = useState<WorkItemStatusReference | null>(null);
  const [form, setForm] = useState<StatusForm>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [viewFilter, setViewFilter] = useState<ViewFilter>("all");

  const statuses = useMemo(
    () => [...(statusesQuery.data ?? [])].sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)),
    [statusesQuery.data],
  );
  const visibleStatuses = useMemo(() => {
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
  const isSaving = mutations.create.isPending || mutations.update.isPending;

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (statuses.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setSheetOpen(true);
  }

  function openEdit(status: WorkItemStatusReference) {
    setEditing(status);
    setForm(requestFor(status));
    setFormError(null);
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

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing && !form.code.trim()) return setFormError(t("validation.codeRequired"));
    if (!form.nameEn.trim()) return setFormError(t("validation.nameEnRequired"));
    if (!form.nameAr.trim()) return setFormError(t("validation.nameArRequired"));
    if (!Number.isFinite(form.displayOrder) || form.displayOrder < 0) return setFormError(t("validation.orderRequired"));

    setFormError(null);
    try {
      const body = { ...form, code: form.code.trim(), nameEn: form.nameEn.trim(), nameAr: form.nameAr.trim() };
      if (editing) await mutations.update.mutateAsync({ id: editing.id, body });
      else await mutations.create.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      setFormError(getSaveError(error, t));
    }
  }

  async function updateStatus(status: WorkItemStatusReference, changes: Partial<StatusForm>, actionId: string) {
    setActionError(null);
    setPendingAction(actionId);
    try {
      await mutations.update.mutateAsync({ id: status.id, body: requestFor(status, changes) });
    } catch (error) {
      setActionError(getSaveError(error, t));
    } finally {
      setPendingAction(null);
    }
  }

  async function toggleActive(status: WorkItemStatusReference) {
    if (status.isActive && !window.confirm(t("deactivateConfirm", { name: locale === "ar" ? status.nameAr : status.nameEn }))) return;
    await updateStatus(status, { isActive: !status.isActive, isDefault: status.isActive ? false : status.isDefault }, `active-${status.id}`);
  }

  async function setDefault(status: WorkItemStatusReference) {
    if (!status.isActive || status.isDefault) return;
    await updateStatus(status, { isDefault: true }, `default-${status.id}`);
  }

  async function moveStatus(status: WorkItemStatusReference, direction: -1 | 1) {
    const index = statuses.findIndex((candidate) => candidate.id === status.id);
    const neighbor = statuses[index + direction];
    if (!neighbor) return;
    const actionId = `order-${status.id}`;
    setActionError(null);
    setPendingAction(actionId);
    try {
      await mutations.update.mutateAsync({ id: status.id, body: requestFor(status, { displayOrder: neighbor.displayOrder }) });
      await mutations.update.mutateAsync({ id: neighbor.id, body: requestFor(neighbor, { displayOrder: status.displayOrder }) });
    } catch (error) {
      setActionError(getSaveError(error, t));
    } finally {
      setPendingAction(null);
    }
  }

  return (
    <div className="space-y-6 pb-8">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={
          <Button size="sm" onClick={openCreate}>
            <Plus className="me-2 h-4 w-4" />
            {t("add")}
          </Button>
        }
      />

      <div role="tablist" aria-label={t("referenceDataTitle")} className="flex w-fit max-w-full flex-wrap gap-1 rounded-xl border bg-muted/20 p-1">
        <Link href={`/${locale}/administration/reference-data/work-item-statuses`} role="tab" aria-selected="true" className="inline-flex items-center gap-1.5 rounded-lg bg-background px-3 py-2 text-sm font-medium text-foreground shadow-sm">
          <ListFilter className="h-3.5 w-3.5 text-primary" />
          {t("tabs.workItemStatuses")}
        </Link>
        <Link href={`/${locale}/administration/currencies`} role="tab" aria-selected="false" className="rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground">
          {t("tabs.currencies")}
        </Link>
        <Link href={`/${locale}/administration/reference-data/vendor-categories`} role="tab" aria-selected="false" className="rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground">
          {t("tabs.vendorCategories")}
        </Link>
      </div>

      <Card className="overflow-hidden border-primary/15 bg-gradient-to-br from-primary/[0.06] via-background to-background shadow-none">
        <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
          <div className="flex items-start gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <ListFilter className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-semibold">{t("overviewTitle")}</p>
              <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{t("overviewDescription")}</p>
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground">
            <ShieldCheck className="h-4 w-4 text-success" />
            <span>{t("protectedTitle")}</span>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard label={t("totalStatuses")} value={statuses.length} icon={ListFilter} tone="text-primary" />
        <SummaryCard label={t("activeStatuses")} value={activeCount} icon={Check} tone="text-success" />
        <SummaryCard label={t("inactiveStatuses")} value={inactiveCount} icon={Power} tone="text-muted-foreground" />
        <SummaryCard label={t("defaultStatus")} value={defaultStatusName} icon={Star} tone="text-warning" />
      </div>

      <Card className="shadow-none">
        <CardHeader className="gap-3 border-b border-border/60 pb-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <CardTitle>{t("listTitle")}</CardTitle>
            <CardDescription className="mt-1">{t("protectedDescription")}</CardDescription>
          </div>
          <span className="text-xs font-medium text-muted-foreground">{visibleStatuses.length} / {statuses.length}</span>
        </CardHeader>
        <CardContent className="space-y-4 p-4 sm:p-5">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="relative w-full max-w-md">
              <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("searchPlaceholder")} className="ps-9" />
              {search ? <button type="button" onClick={() => setSearch("")} className="absolute end-2 top-1/2 -translate-y-1/2 rounded p-1 text-muted-foreground hover:bg-muted hover:text-foreground" aria-label={t("clearSearch")}><X className="h-3.5 w-3.5" /></button> : null}
            </div>
            <div className="flex flex-wrap gap-2" role="group" aria-label={t("filterStatuses")}>
              {(["all", "active", "inactive"] as const).map((filter) => (
                <Button key={filter} type="button" size="sm" variant={viewFilter === filter ? "default" : "outline"} aria-pressed={viewFilter === filter} onClick={() => setViewFilter(filter)}>
                  {filter === "all" ? t("filterAll") : filter === "active" ? t("filterActive") : t("filterInactive")}
                </Button>
              ))}
            </div>
          </div>

          {actionError ? <div role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2.5 text-sm text-destructive"><Info className="mt-0.5 h-4 w-4 shrink-0" /><span>{actionError}</span></div> : null}

          {statusesQuery.isLoading ? (
            <div className="space-y-2 rounded-lg border p-4">
              {Array.from({ length: 5 }).map((_, index) => <Skeleton key={index} className="h-12 w-full" />)}
            </div>
          ) : statusesQuery.isError ? (
            <div className="flex flex-col items-center gap-3 rounded-lg border border-destructive/20 bg-destructive/[0.03] p-10 text-center">
              <p role="alert" className="text-sm text-destructive">{t("error")}</p>
              <Button size="sm" variant="outline" onClick={() => void statusesQuery.refetch()}>{t("retry")}</Button>
            </div>
          ) : (
            <Table containerClassName="rounded-xl" density="compact">
              <TableHeader>
                <TableRow>
                  <TableHead className="min-w-56">{t("nameEn")}</TableHead>
                  <TableHead>{t("code")}</TableHead>
                  <TableHead>{t("color")}</TableHead>
                  <TableHead className="text-center">{t("displayOrder")}</TableHead>
                  <TableHead>{t("default")}</TableHead>
                  <TableHead>{t("active")}</TableHead>
                  <TableHead className="text-end">{t("actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {visibleStatuses.length ? visibleStatuses.map((status) => {
                  const statusIndex = statuses.findIndex((candidate) => candidate.id === status.id);
                  const color = getWorkItemStatusColor(status.color);
                  const colorClasses = getWorkItemStatusColorClasses(status.color);
                  const rowBusy = pendingAction?.endsWith(status.id) || mutations.update.isPending;
                  return (
                    <TableRow key={status.id}>
                      <TableCell>
                        <div className="flex items-center gap-2.5">
                          <span className={`h-3 w-3 shrink-0 rounded-full ${colorClasses.swatch}`} aria-hidden="true" />
                          <div className="min-w-0">
                            <p className="truncate font-medium">{locale === "ar" ? status.nameAr : status.nameEn}</p>
                            <p className="truncate text-[11px] text-muted-foreground">{locale === "ar" ? status.nameEn : status.nameAr}</p>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell className="font-mono text-xs text-muted-foreground">{status.code}</TableCell>
                      <TableCell><span className={`text-xs font-medium ${colorClasses.text}`}>{t(`colors.${color}`)}</span></TableCell>
                      <TableCell className="text-center tabular-nums text-muted-foreground">{status.displayOrder}</TableCell>
                      <TableCell>
                        {status.isDefault ? <span className="inline-flex items-center gap-1.5 text-xs font-medium text-primary"><Check className="h-3.5 w-3.5" />{t("default")}</span> : <span className="text-muted-foreground">—</span>}
                      </TableCell>
                      <TableCell><span className={`text-xs font-medium ${getStatusTextClass(status.isActive ? "Active" : "Inactive")}`}>{status.isActive ? t("active") : t("inactive")}</span></TableCell>
                      <TableCell>
                        <div className="flex items-center justify-end gap-0.5">
                          <Button type="button" size="icon" variant="ghost" className="h-8 w-8" onClick={() => openEdit(status)} aria-label={`${t("edit")} ${status.nameEn}`} title={t("edit")}><Pencil className="h-3.5 w-3.5" /></Button>
                          <Button type="button" size="icon" variant="ghost" className={`h-8 w-8 ${status.isDefault ? "text-warning" : ""}`} disabled={rowBusy || !status.isActive || status.isDefault} onClick={() => void setDefault(status)} aria-label={t("setDefault")} title={t("setDefault")}><Star className={`h-3.5 w-3.5 ${status.isDefault ? "fill-current" : ""}`} /></Button>
                          <Button type="button" size="icon" variant="ghost" className="h-8 w-8" disabled={rowBusy || statusIndex === 0} onClick={() => void moveStatus(status, -1)} aria-label={t("moveUp")} title={t("moveUp")}><ArrowUp className="h-3.5 w-3.5" /></Button>
                          <Button type="button" size="icon" variant="ghost" className="h-8 w-8" disabled={rowBusy || statusIndex === statuses.length - 1} onClick={() => void moveStatus(status, 1)} aria-label={t("moveDown")} title={t("moveDown")}><ArrowDown className="h-3.5 w-3.5" /></Button>
                          <Button type="button" size="sm" variant="ghost" className="ms-1 h-8 gap-1.5 text-xs" disabled={rowBusy} onClick={() => void toggleActive(status)}><Power className={`h-3.5 w-3.5 ${status.isActive ? "text-destructive" : "text-success"}`} />{status.isActive ? t("deactivate") : t("activate")}</Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                }) : <TableRow><TableCell colSpan={7} className="h-32 text-center"><div className="flex flex-col items-center gap-2"><Search className="h-6 w-6 text-muted-foreground" /><p className="text-sm text-muted-foreground">{search || viewFilter !== "all" ? t("noFilteredStatuses") : t("empty")}</p>{search || viewFilter !== "all" ? <Button type="button" size="sm" variant="outline" onClick={() => { setSearch(""); setViewFilter("all"); }}>{t("clearFilters")}</Button> : null}</div></TableCell></TableRow>}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <div className="flex items-start gap-3 rounded-lg border border-border/70 bg-muted/20 px-4 py-3 text-sm text-muted-foreground">
        <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-success" />
        <p>{t("noDelete")}</p>
      </div>

      <FormSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t("edit") : t("add")} description={editing ? t("codeHint") : t("description")} size="md" closeLabel={t("cancel")}>
        <form onSubmit={save} className="flex min-h-0 flex-1 flex-col">
          <FormSheetBody className="space-y-6">
            {formError ? <div role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2.5 text-sm text-destructive"><Info className="mt-0.5 h-4 w-4 shrink-0" /><span>{formError}</span></div> : null}
            <section className="space-y-4">
              <div><h2 className="text-sm font-semibold">{t("formBasics")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("description")}</p></div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2"><Label htmlFor="work-item-status-name-en">{t("nameEn")}</Label><Input id="work-item-status-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required /></div>
                <div className="space-y-2"><Label htmlFor="work-item-status-name-ar">{t("nameAr")}</Label><Input id="work-item-status-name-ar" dir="rtl" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} required /></div>
                <div className="space-y-2"><Label htmlFor="work-item-status-code">{t("code")}</Label><Input id="work-item-status-code" value={form.code} onChange={(event) => updateForm("code", event.target.value)} disabled={Boolean(editing)} required={!editing} aria-describedby={editing ? "work-item-status-code-hint" : undefined} />{editing ? <p id="work-item-status-code-hint" className="text-xs text-muted-foreground">{t("codeHint")}</p> : null}</div>
                <div className="space-y-2"><Label htmlFor="work-item-status-order">{t("displayOrder")}</Label><Input id="work-item-status-order" type="number" min="0" step="1" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} required /><p className="text-xs text-muted-foreground">{t("orderHint")}</p></div>
              </div>
            </section>

            <section className="space-y-4 rounded-xl border bg-muted/20 p-4">
              <div><h2 className="text-sm font-semibold">{t("statusPreview")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("colorPreview")}</p></div>
              <div className="flex items-center gap-3 rounded-lg border bg-background px-3 py-3">
                <span className={`h-3 w-3 shrink-0 rounded-full ${getWorkItemStatusColorClasses(form.color).swatch}`} aria-hidden="true" />
                <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{locale === "ar" ? form.nameAr || t("nameAr") : form.nameEn || t("nameEn")}</p><p className="text-xs text-muted-foreground">{form.code || t("code")}</p></div>
                <span className={`text-xs font-medium ${getWorkItemStatusColorClasses(form.color).text}`}>{t(`colors.${getWorkItemStatusColor(form.color)}`)}</span>
              </div>
              <div className="space-y-2"><Label htmlFor="work-item-status-color">{t("color")}</Label><select id="work-item-status-color" className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={getWorkItemStatusColor(form.color)} onChange={(event) => updateForm("color", event.target.value)}>{WORK_ITEM_STATUS_COLORS.map((color) => <option key={color} value={color}>{t(`colors.${color}`)}</option>)}</select></div>
            </section>

            <section className="space-y-4">
              <div><h2 className="text-sm font-semibold">{t("formBehavior")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("protectedDescription")}</p></div>
              <div className="space-y-3">
                <label className="flex items-start gap-3 rounded-lg border p-3"><Switch id="work-item-status-active" checked={form.isActive} onCheckedChange={(checked) => updateForm("isActive", checked)} /><span><span className="block text-sm font-medium">{t("active")}</span><span className="mt-1 block text-xs text-muted-foreground">{t("activeHint")}</span></span></label>
                <label className="flex items-start gap-3 rounded-lg border p-3"><Switch id="work-item-status-default" checked={form.isDefault} onCheckedChange={(checked) => updateForm("isDefault", checked)} disabled={!form.isActive} /><span><span className="block text-sm font-medium">{t("default")}</span><span className="mt-1 block text-xs text-muted-foreground">{t("defaultHint")}</span></span></label>
              </div>
            </section>
          </FormSheetBody>
          <FormSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)}>{t("cancel")}</Button><Button type="submit" disabled={isSaving}>{isSaving ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{t("saving")}</> : t("save")}</Button></FormSheetFooter>
        </form>
      </FormSheet>
    </div>
  );
}
