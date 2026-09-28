"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { ArrowDown, ArrowUp, Check, ListFilter, Loader2, Pencil, Plus, Power, Star } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { useWorkItemStatusMutations, useWorkItemStatuses, type SaveWorkItemStatusRequest, type WorkItemStatusReference } from "@/lib/api/reference-data";
import { getWorkItemStatusColor, getWorkItemStatusColorClasses, WORK_ITEM_STATUS_COLORS } from "@/lib/status-presentation";

type StatusForm = SaveWorkItemStatusRequest;

const EMPTY_FORM: StatusForm = { code: "", nameEn: "", nameAr: "", displayOrder: 10, isActive: true, color: "blue", isDefault: false };

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
  return { code: status.code, nameEn: status.nameEn, nameAr: status.nameAr, displayOrder: status.displayOrder, isActive: status.isActive, color: status.color, isDefault: status.isDefault, ...changes };
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

  const statuses = [...(statusesQuery.data ?? [])].sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code));
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
    <div className="space-y-6">
      <PageHeader title={t("referenceDataTitle")} description={t("description")} actions={<Button size="sm" onClick={openCreate}><Plus className="me-2 h-4 w-4" />{t("add")}</Button>} />
      <div role="tablist" aria-label={t("referenceDataTitle")} className="flex w-fit max-w-full flex-wrap gap-1 rounded-lg border bg-muted/20 p-1">
        <Link href={`/${locale}/administration/reference-data/work-item-statuses`} role="tab" aria-selected="true" className="rounded-md bg-background px-3 py-1.5 text-sm font-medium text-foreground shadow-sm"><ListFilter className="me-1.5 inline h-3.5 w-3.5" />{t("tabs.workItemStatuses")}</Link>
        <Link href={`/${locale}/administration/currencies`} role="tab" aria-selected="false" className="rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground">{t("tabs.currencies")}</Link>
        <Link href={`/${locale}/administration/reference-data/vendor-categories`} role="tab" aria-selected="false" className="rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground">{t("tabs.vendorCategories")}</Link>
      </div>
      <Card>
        <CardContent className="p-0">
          {statusesQuery.isLoading ? <div className="space-y-3 p-5"><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /></div> : statusesQuery.isError ? <div className="flex flex-col items-center gap-3 p-8 text-center"><p role="alert" className="text-sm text-destructive">{t("error")}</p><Button size="sm" variant="outline" onClick={() => statusesQuery.refetch()}>{t("retry")}</Button></div> : statuses.length === 0 ? <p className="p-8 text-center text-sm text-muted-foreground">{t("empty")}</p> : <div className="nexus-table-surface"><table className="w-full min-w-[900px] text-sm"><thead><tr className="nexus-table-header"><th className="text-start">{t("nameEn")}</th><th className="text-start">{t("nameAr")}</th><th className="text-start">{t("code")}</th><th className="text-start">{t("color")}</th><th className="text-start">{t("displayOrder")}</th><th className="text-start">{t("default")}</th><th className="text-start">{t("active")}</th><th className="text-start">{t("actions")}</th></tr></thead><tbody>{statuses.map((status, index) => { const color = getWorkItemStatusColor(status.color); const colorClasses = getWorkItemStatusColorClasses(status.color); const rowBusy = pendingAction?.endsWith(status.id) || mutations.update.isPending; return <tr key={status.id} className="nexus-table-row align-middle"><td className="nexus-table-cell font-medium">{status.nameEn}</td><td className="nexus-table-cell" dir="rtl">{status.nameAr}</td><td className="nexus-table-cell font-mono text-xs text-muted-foreground">{status.code}</td><td className="nexus-table-cell"><span className="inline-flex items-center gap-2"><span className={`h-3 w-3 rounded-full ${colorClasses.swatch}`} /><span>{t(`colors.${color}`)}</span></span></td><td className="nexus-table-cell tabular-nums text-muted-foreground">{status.displayOrder}</td><td className="nexus-table-cell">{status.isDefault ? <span className="inline-flex items-center gap-1.5 text-primary"><Check className="h-4 w-4" />{t("default")}</span> : <span className="text-muted-foreground">—</span>}</td><td className="nexus-table-cell"><span className={`rounded-full px-2 py-1 text-xs ${status.isActive ? "bg-success/10 text-success" : "bg-muted text-muted-foreground"}`}>{status.isActive ? t("active") : t("inactive")}</span></td><td className="nexus-table-cell"><div className="nexus-table-actions flex items-center gap-1"><Button type="button" size="sm" variant="outline" onClick={() => openEdit(status)}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("edit")}</Button><Button type="button" size="icon" variant="ghost" disabled={index === 0 || Boolean(pendingAction)} onClick={() => void moveStatus(status, -1)} aria-label={t("moveUp")} title={t("moveUp")}><ArrowUp className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" disabled={index === statuses.length - 1 || Boolean(pendingAction)} onClick={() => void moveStatus(status, 1)} aria-label={t("moveDown")} title={t("moveDown")}><ArrowDown className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" disabled={rowBusy || !status.isActive} onClick={() => void setDefault(status)} aria-label={t("setDefault")} title={t("setDefault")}><Star className={`h-4 w-4 ${status.isDefault ? "fill-current text-primary" : "text-muted-foreground"}`} /></Button><Button type="button" size="sm" variant="ghost" disabled={rowBusy} onClick={() => void toggleActive(status)}><Power className={`me-1.5 h-3.5 w-3.5 ${status.isActive ? "text-destructive" : "text-success"}`} />{status.isActive ? t("deactivate") : t("activate")}</Button></div></td></tr>; })}</tbody></table></div>}
          {actionError ? <p role="alert" className="border-t border-destructive/20 bg-destructive/5 px-5 py-3 text-sm text-destructive">{actionError}</p> : null}<p className="border-t bg-muted/20 px-5 py-3 text-xs text-muted-foreground">{t("noDelete")}</p>
        </CardContent>
      </Card>
      <FormSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t("edit") : t("add")} description={editing ? t("codeHint") : t("description")} size="md">
        <form onSubmit={save} className="flex min-h-0 flex-1 flex-col"><FormSheetBody className="space-y-5">{formError ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{formError}</p> : null}<div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label htmlFor="work-item-status-name-en">{t("nameEn")}</Label><Input id="work-item-status-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required /></div><div className="space-y-2"><Label htmlFor="work-item-status-name-ar">{t("nameAr")}</Label><Input id="work-item-status-name-ar" dir="rtl" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} required /></div><div className="space-y-2"><Label htmlFor="work-item-status-code">{t("code")}</Label><Input id="work-item-status-code" value={form.code} onChange={(event) => updateForm("code", event.target.value)} disabled={Boolean(editing)} required={!editing} aria-describedby={editing ? "work-item-status-code-hint" : undefined} />{editing ? <p id="work-item-status-code-hint" className="text-xs text-muted-foreground">{t("codeHint")}</p> : null}</div><div className="space-y-2"><Label htmlFor="work-item-status-order">{t("displayOrder")}</Label><Input id="work-item-status-order" type="number" min="0" step="1" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} required /></div><div className="space-y-2"><Label htmlFor="work-item-status-color">{t("color")}</Label><div className="flex items-center gap-2"><span className={`h-3 w-3 shrink-0 rounded-full ${getWorkItemStatusColorClasses(form.color).swatch}`} /><select id="work-item-status-color" className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={getWorkItemStatusColor(form.color)} onChange={(event) => updateForm("color", event.target.value)}>{WORK_ITEM_STATUS_COLORS.map((color) => <option key={color} value={color}>{t(`colors.${color}`)}</option>)}</select></div></div></div><div className="flex flex-wrap gap-6"><label className="flex items-center gap-2 text-sm"><Switch id="work-item-status-active" checked={form.isActive} onCheckedChange={(checked) => updateForm("isActive", checked)} />{t("active")}</label><label className="flex items-center gap-2 text-sm"><Switch id="work-item-status-default" checked={form.isDefault} onCheckedChange={(checked) => updateForm("isDefault", checked)} disabled={!form.isActive} />{t("default")}</label></div></FormSheetBody><FormSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)}>{t("cancel")}</Button><Button type="submit" disabled={isSaving}>{isSaving ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{t("saving")}</> : t("save")}</Button></FormSheetFooter></form>
      </FormSheet>
    </div>
  );
}
