"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowLeft, ArrowRight, CalendarDays, CheckCircle2, Clock3, FolderKanban, History, Loader2, MessageSquare, Pencil, Plus, Save, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Textarea } from "@/components/ui/textarea";
import { useResources } from "@/hooks/resources/useResources";
import { useUserLookup } from "@/hooks/projects/useProjectLookups";
import { useAddRiskActionUpdate, useCreateRiskAction, useRisk, useRiskActionUpdates, useRiskActions, useRiskImpacts, useUpdateRisk, useUpdateRiskAction, useUpdateRiskStatus } from "@/hooks/risks/useRisks";
import { ApiError } from "@/lib/api/client";
import type { RiskActionDto, RiskActionStatus, RiskLevel, RiskStatus } from "@/lib/api/risks";
import { RISK_ACTION_STATUSES, RISK_LEVELS, RiskActionStatusHelper, RiskLevelHelper } from "@/lib/enums";
import { formatDate, formatDateTime, toDateInput } from "@/lib/utils";
import { riskActionPrimaryButtonClass, riskCloseButtonClass, riskEditButtonClass, riskMitigateButtonClass, riskSelectedActionClass } from "@/lib/risk-presentation";
import { getStatusTextClass, getStatusTone, statusToneDotClasses } from "@/lib/status-presentation";
import { RiskImpactTypeText, RiskLevelText, RiskStatusText } from "./RiskValueText";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";

type ActionDraft = { title: string; description: string; ownerResourceId: string; targetDate: string; nextFollowUpDate: string; status: RiskActionStatus };
const emptyAction: ActionDraft = { title: "", description: "", ownerResourceId: "", targetDate: "", nextFollowUpDate: "", status: "Planned" };

export function RiskDetailsView({ riskId }: { riskId: string }) {
  const t = useTranslations("risks");
  const enums = useTranslations("enums");
  const locale = useLocale();
  const router = useRouter();
  const { user } = useAuth();
  const searchParams = useSearchParams();
  const projectId = searchParams.get("projectId");
  const fromProject = searchParams.get("from") === "project" && Boolean(projectId);
  const backHref = fromProject ? `/${locale}/projects/${encodeURIComponent(projectId ?? "")}?tab=risks-issues` : `/${locale}/risks-issues/risks`;
  const requestedActionId = searchParams.get("actionId") ?? "";
  const riskQuery = useRisk(riskId);
  const actionsQuery = useRiskActions(riskId, riskQuery.isSuccess);
  const impactsQuery = useRiskImpacts(riskId, riskQuery.isSuccess);
  const [selectedActionId, setSelectedActionId] = useState(requestedActionId);
  const [updateText, setUpdateText] = useState("");
  const [editingRisk, setEditingRisk] = useState(false);
  const [riskSubmitted, setRiskSubmitted] = useState(false);
  const [showActionForm, setShowActionForm] = useState(searchParams.get("addAction") === "1");
  const [editingAction, setEditingAction] = useState(false);
  const [actionSubmitted, setActionSubmitted] = useState(false);
  const [actionDraft, setActionDraft] = useState<ActionDraft>(emptyAction);
  const [riskDraft, setRiskDraft] = useState({ title: "", description: "", riskLevel: "Low" as RiskLevel, ownerResourceId: "" });
  const actions = useMemo(() => actionsQuery.data ?? [], [actionsQuery.data]);
  const selectedAction = actions.find((action) => action.id === selectedActionId);
  const usersQuery = useUserLookup(undefined, riskQuery.isSuccess && Boolean(selectedAction));
  const resourcesQuery = useResources({ page: 1, pageSize: 100, isActive: true }, riskQuery.isSuccess && (editingRisk || showActionForm || editingAction));
  const updatesQuery = useRiskActionUpdates(riskId, selectedActionId, riskQuery.isSuccess && Boolean(selectedAction));
  const updates = useMemo(() => [...(updatesQuery.data ?? [])].sort((left, right) => {
    const leftTime = Date.parse(left.createdAt);
    const rightTime = Date.parse(right.createdAt);
    if (Number.isNaN(leftTime) || Number.isNaN(rightTime)) return 0;
    return leftTime - rightTime;
  }), [updatesQuery.data]);
  const addUpdate = useAddRiskActionUpdate();
  const createAction = useCreateRiskAction();
  const updateAction = useUpdateRiskAction();
  const updateRisk = useUpdateRisk();
  const updateRiskStatus = useUpdateRiskStatus();
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  useEffect(() => {
    if (riskQuery.data) setRiskDraft({ title: riskQuery.data.title, description: riskQuery.data.description, riskLevel: riskQuery.data.riskLevel, ownerResourceId: riskQuery.data.ownerResourceId ?? "" });
  }, [riskQuery.data]);

  useEffect(() => {
    if (!actionsQuery.isSuccess || actionsQuery.isFetching) return;
    const requested = actions.find((action) => action.id === requestedActionId);
    const nextAction = requested ?? actions.find((action) => action.id === selectedActionId) ?? actions[0];
    if (nextAction?.id !== selectedActionId) setSelectedActionId(nextAction?.id ?? "");
  }, [actions, actionsQuery.isFetching, actionsQuery.isSuccess, requestedActionId, selectedActionId]);

  const selectAction = (action: RiskActionDto) => {
    setSelectedActionId(action.id);
    setUpdateText("");
    router.replace(`${window.location.pathname}?from=${fromProject ? "project" : "risks"}&${fromProject ? `projectId=${encodeURIComponent(projectId ?? "")}&` : ""}actionId=${encodeURIComponent(action.id)}`, { scroll: false });
  };

  const openAddAction = () => {
    setActionDraft(emptyAction);
    setActionSubmitted(false);
    setEditingAction(false);
    setShowActionForm(true);
  };

  const openEditAction = () => {
    if (!selectedAction) return;
    setActionDraft({ title: selectedAction.title, description: selectedAction.description, ownerResourceId: selectedAction.ownerResourceId ?? "", targetDate: toDateInput(selectedAction.targetDate), nextFollowUpDate: toDateInput(selectedAction.nextFollowUpDate), status: selectedAction.status });
    setActionSubmitted(false);
    setEditingAction(true);
    setShowActionForm(false);
  };

  const saveUpdate = async () => {
    if (!selectedActionId || !updateText.trim()) return;
    await addUpdate.mutateAsync({ riskId, actionId: selectedActionId, data: { updateText: updateText.trim() } });
    setUpdateText("");
  };

  const saveAction = async () => {
    setActionSubmitted(true);
    if (!actionDraft.title.trim()) return;
    const data = { ...actionDraft, ownerResourceId: actionDraft.ownerResourceId || undefined, targetDate: actionDraft.targetDate || undefined, nextFollowUpDate: actionDraft.nextFollowUpDate || undefined };
    if (editingAction && selectedAction) await updateAction.mutateAsync({ riskId, actionId: selectedAction.id, data: { ...data, sortOrder: selectedAction.sortOrder, rowVersion: selectedAction.rowVersion } });
    else {
      const saved = await createAction.mutateAsync({ riskId, data: { ...data, sortOrder: actions.length } });
      setSelectedActionId(saved.id);
    }
    setShowActionForm(false);
    setEditingAction(false);
    setActionDraft(emptyAction);
  };

  const saveRisk = async () => {
    setRiskSubmitted(true);
    if (!riskQuery.data || !riskDraft.title.trim() || !riskDraft.description.trim()) return;
    await updateRisk.mutateAsync({ id: riskId, data: { ...riskDraft, ownerResourceId: riskDraft.ownerResourceId || undefined, rowVersion: riskQuery.data.rowVersion } });
    setEditingRisk(false);
  };

  const transitionRisk = async (status: RiskStatus) => {
    if (!riskQuery.data || updateRiskStatus.isPending) return;
    await updateRiskStatus.mutateAsync({ id: riskId, status, rowVersion: riskQuery.data.rowVersion });
  };

  const closeActionForm = () => {
    setShowActionForm(false);
    setEditingAction(false);
    setActionSubmitted(false);
  };

  if (riskQuery.isLoading) return <Skeleton className="h-[720px]" />;
  if (riskQuery.isError || !riskQuery.data) {
    const message = riskQuery.error instanceof ApiError && riskQuery.error.status === 404 ? t("notFound") : t("loadError");
    return <Card><CardContent className="flex flex-col items-center gap-4 py-16 text-center"><p role="alert" className="text-sm text-muted-foreground">{message}</p><Button variant="outline" onClick={() => router.push(backHref)}><ArrowLeft className="me-2 h-4 w-4" />{fromProject ? t("backToProject") : t("backToRisks")}</Button></CardContent></Card>;
  }

  const risk = riskQuery.data;
  const impact = impactsQuery.data?.[0];
  const canMarkMitigated = risk.status === "Identified" || risk.status === "ActionRequired" || risk.status === "FollowUp";
  const canCloseRisk = risk.status === "Mitigated";
  const canEditRisk = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Edit));
  const canMitigateRisk = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Mitigate));
  const actionPending = createAction.isPending || updateAction.isPending;

  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <Button variant="ghost" size="sm" onClick={() => router.push(backHref)}><ArrowLeft className="me-2 h-4 w-4" />{fromProject ? t("backToProject") : t("backToRegister")}</Button>
      <div className="flex flex-wrap gap-2">
        {canEditRisk ? <Button size="sm" variant="outline" className={riskEditButtonClass} onClick={() => { setRiskSubmitted(false); setEditingRisk(true); }}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("editRisk")}</Button> : null}
        {canMarkMitigated && canMitigateRisk ? <Button size="sm" variant="outline" className={riskMitigateButtonClass} onClick={() => void transitionRisk("Mitigated")} disabled={updateRiskStatus.isPending}>{updateRiskStatus.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{updateRiskStatus.isPending ? t("saving") : t("markMitigated")}</Button> : null}
        {canCloseRisk && canMitigateRisk ? <Button size="sm" variant="outline" className={riskCloseButtonClass} onClick={() => void transitionRisk("Closed")} disabled={updateRiskStatus.isPending}>{updateRiskStatus.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{updateRiskStatus.isPending ? t("saving") : t("closeRisk")}</Button> : null}
      </div>
    </div>
    {updateRiskStatus.isError ? <p role="alert" className="text-sm text-destructive">{t("statusUpdateError")}</p> : null}

    <Card className="overflow-hidden border-primary/15 bg-card/80"><CardContent className="p-5 sm:p-6"><div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between"><div className="min-w-0"><p className="mb-2 text-xs font-medium uppercase tracking-[0.18em] text-primary">{t("riskOverview")}</p><h1 className="text-xl font-bold tracking-tight sm:text-2xl">{risk.title}</h1><Link href={`/${locale}/projects/${encodeURIComponent(risk.projectId)}?tab=risks-issues`} className="mt-1 inline-flex items-center gap-1.5 text-sm font-medium text-blue-700 hover:text-blue-800 hover:underline dark:text-blue-300 dark:hover:text-blue-200"><FolderKanban className="h-3.5 w-3.5" />{risk.projectName || "—"}</Link></div><div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm"><span><span className="text-muted-foreground">{t("riskLevel")}: </span><RiskLevelText value={risk.riskLevel} /></span><span><span className="text-muted-foreground">{t("status")}: </span><RiskStatusText value={risk.status} /></span></div></div></CardContent></Card>

    <Card><CardHeader><CardTitle className="text-base">{t("riskOverview")}</CardTitle></CardHeader><CardContent className="grid gap-6 pt-0 lg:grid-cols-[minmax(0,1fr)_minmax(260px,0.8fr)]"><div className="space-y-5"><InfoField label={t("descriptionField")} value={risk.description} multiline /><div className="grid gap-4 sm:grid-cols-2"><InfoField label={t("project")} value={<Link href={`/${locale}/projects/${encodeURIComponent(risk.projectId)}?tab=risks-issues`} className="inline-flex items-center gap-1.5 font-medium text-blue-700 hover:text-blue-800 hover:underline dark:text-blue-300 dark:hover:text-blue-200"><FolderKanban className="h-3.5 w-3.5" />{risk.projectName || "—"}</Link>} /><InfoField label={t("owner")} value={risk.ownerResourceName || "—"} /><InfoField label={t("identifiedDate")} value={formatDate(risk.createdAt, dateLocale)} /><InfoField label={t("lastUpdate")} value={formatDateTime(risk.updatedAt ?? risk.createdAt, dateLocale)} /></div></div><div className="rounded-xl border bg-muted/20 p-4"><p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("riskImpact")}</p>{impactsQuery.isError ? <p role="alert" className="mt-4 text-sm text-destructive">{t("loadError")}</p> : impact ? <div className="mt-4 space-y-4"><div className="flex flex-wrap gap-2">{impact.impactTypes.map((type) => <RiskImpactTypeText key={type} value={type} className="px-2.5 py-1 text-xs" />)}</div><InfoField label={t("impactDescription")} value={impact.description} multiline /></div> : <p className="mt-4 text-sm text-muted-foreground">{t("noImpacts")}</p>}</div></CardContent></Card>

    <div className="grid gap-5 lg:grid-cols-[minmax(280px,340px)_minmax(0,1fr)]"><Card className="h-fit"><CardHeader className="flex flex-row items-center justify-between space-y-0"><CardTitle className="text-base">{t("actions")}</CardTitle><Button size="sm" className={riskActionPrimaryButtonClass} onClick={openAddAction}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addAction")}</Button></CardHeader><CardContent className="space-y-2 pt-0">{actionsQuery.isLoading ? <Skeleton className="h-28" /> : actionsQuery.isError ? <p role="alert" className="px-2 py-6 text-sm text-destructive">{t("loadError")}</p> : actions.length ? actions.map((action) => <ActionListItem key={action.id} action={action} selected={action.id === selectedActionId} enums={enums} t={t} onSelect={() => selectAction(action)} />) : <div className="rounded-lg border border-dashed px-4 py-8 text-center"><p className="text-sm text-muted-foreground">{t("noActionsWithAdd")}</p><Button className={`mt-4 ${riskActionPrimaryButtonClass}`} size="sm" onClick={openAddAction}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addAction")}</Button></div>}</CardContent></Card><Card><CardHeader className="flex flex-row items-start justify-between gap-3"><div className="min-w-0">{selectedAction ? <><CardTitle className="truncate text-base">{selectedAction.title}</CardTitle><div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"><span className="inline-flex items-center gap-1"><RiskActionStatusText value={selectedAction.status} enums={enums} /></span><span className="inline-flex items-center gap-1"><UserRound className="h-3.5 w-3.5" />{selectedAction.ownerResourceName || "—"}</span>{selectedAction.targetDate ? <span className="inline-flex items-center gap-1"><CalendarDays className="h-3.5 w-3.5" /><span>{t("actionTargetDate")}:</span>{formatDate(selectedAction.targetDate, dateLocale)}</span> : null}{selectedAction.nextFollowUpDate ? <span className="inline-flex items-center gap-1"><Clock3 className="h-3.5 w-3.5" /><span>{t("actionFollowUpDate")}:</span>{formatDate(selectedAction.nextFollowUpDate, dateLocale)}</span> : null}</div></> : <CardTitle className="text-base">{t("selectAction")}</CardTitle>}</div>{selectedAction ? <Button size="sm" variant="outline" className={riskEditButtonClass} onClick={openEditAction}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("editAction")}</Button> : null}</CardHeader><CardContent className="space-y-6 pt-0">{selectedAction ? <><InfoField label={t("actionDescription")} value={selectedAction.description || "—"} multiline /><section className="space-y-3 border-t pt-5"><div className="flex items-center gap-2"><ActivityDot /><h3 className="text-sm font-semibold">{t("updates")}</h3></div>{updatesQuery.isLoading ? <Skeleton className="h-24" /> : updatesQuery.isError ? <p role="alert" className="text-sm text-destructive">{t("loadError")}</p> : updates.length ? <div className="space-y-4">{updates.map((update) => update.entryType === "SystemChange" ? <SystemTimelineEntry key={update.id} update={update} t={t} enums={enums} dateLocale={dateLocale} actorName={usersQuery.data?.find((user) => user.id.toLowerCase() === update.createdBy.toLowerCase())?.fullName ?? t("unknownUser")} /> : <article key={update.id} className="relative border-s ps-4 text-sm before:absolute before:-start-[5px] before:top-1 before:h-2 before:w-2 before:rounded-full before:bg-primary"><div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><MessageSquare className="h-3.5 w-3.5" /><span className="font-medium text-foreground">{usersQuery.data?.find((user) => user.id.toLowerCase() === update.createdBy.toLowerCase())?.fullName ?? t("unknownUser")}</span><span>·</span><time className="text-muted-foreground" dateTime={update.createdAt}>{formatDateTime(update.createdAt, dateLocale)}</time></div><p className="mt-2 whitespace-pre-wrap leading-6">{update.updateText}</p></article>)}</div> : <p className="text-sm text-muted-foreground">{t("noUpdates")}</p>}<div className="space-y-2 border-t pt-4"><Label htmlFor="risk-action-update" className="text-xs text-muted-foreground">{t("updateText")}</Label><Textarea id="risk-action-update" value={updateText} onChange={(event) => setUpdateText(event.target.value)} placeholder={t("writeUpdate")} rows={3} /><div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between"><div>{addUpdate.isError ? <p role="alert" className="text-xs text-destructive">{t("updateSaveError")}</p> : null}</div><Button className={riskActionPrimaryButtonClass} onClick={() => void saveUpdate()} disabled={addUpdate.isPending || !updateText.trim()}>{addUpdate.isPending ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : <Plus className="me-2 h-4 w-4" />}{addUpdate.isPending ? t("saving") : t("addUpdate")}</Button></div></div></section></> : <div className="flex min-h-56 items-center justify-center text-sm text-muted-foreground">{t("selectAction")}</div>}</CardContent></Card></div>

    <Dialog open={editingRisk} onOpenChange={(open) => { setEditingRisk(open); if (!open) setRiskSubmitted(false); }}><DialogContent><DialogHeader><DialogTitle>{t("editRisk")}</DialogTitle></DialogHeader><div className="space-y-4"><FormField label={t("titleField")}><Input value={riskDraft.title} aria-invalid={riskSubmitted && !riskDraft.title.trim()} onChange={(event) => setRiskDraft({ ...riskDraft, title: event.target.value })} />{riskSubmitted && !riskDraft.title.trim() ? <FieldError text={t("required")} /> : null}</FormField><FormField label={t("descriptionField")}><Textarea value={riskDraft.description} aria-invalid={riskSubmitted && !riskDraft.description.trim()} onChange={(event) => setRiskDraft({ ...riskDraft, description: event.target.value })} />{riskSubmitted && !riskDraft.description.trim() ? <FieldError text={t("required")} /> : null}</FormField><FormField label={t("riskLevel")}><Select value={riskDraft.riskLevel} onValueChange={(value) => setRiskDraft({ ...riskDraft, riskLevel: value as RiskLevel })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{RISK_LEVELS.map((value) => <SelectItem key={value} value={value}>{enums(RiskLevelHelper.getLabelKey(value) ?? "unknown")}</SelectItem>)}</SelectContent></Select></FormField><FormField label={t("owner")}><ResourceSelect value={riskDraft.ownerResourceId} resources={resourcesQuery.data?.items ?? []} noneLabel={t("noOwner")} onChange={(value) => setRiskDraft({ ...riskDraft, ownerResourceId: value })} /></FormField>{updateRisk.isError ? <p role="alert" className="text-sm text-destructive">{t("riskSaveError")}</p> : null}</div><DialogFooter><Button variant="outline" onClick={() => setEditingRisk(false)}>{t("cancel")}</Button><Button onClick={() => void saveRisk()} disabled={updateRisk.isPending}>{updateRisk.isPending ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : null}{updateRisk.isPending ? t("saving") : t("saveChanges")}</Button></DialogFooter></DialogContent></Dialog>

    <SideSheet open={showActionForm || editingAction} onOpenChange={(open) => { if (!open) closeActionForm(); }} title={editingAction ? t("editActionTitle") : t("addActionTitle")} size="md" closeLabel={t("cancel")}><SideSheetBody><ActionForm value={actionDraft} setValue={setActionDraft} resources={resourcesQuery.data?.items ?? []} enums={enums} t={t} submitted={actionSubmitted} />{resourcesQuery.isError ? <p role="alert" className="mt-4 text-sm text-destructive">{t("ownerLoadError")}</p> : null}{createAction.isError || updateAction.isError ? <p role="alert" className="mt-4 text-sm text-destructive">{t("actionSaveError")}</p> : null}</SideSheetBody><SideSheetFooter><Button variant="outline" onClick={closeActionForm}>{t("cancel")}</Button><Button className={riskActionPrimaryButtonClass} onClick={() => void saveAction()} disabled={actionPending}>{actionPending ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : <Save className="me-2 h-4 w-4" />}{actionPending ? t("saving") : editingAction ? t("saveChanges") : t("saveAction")}</Button></SideSheetFooter></SideSheet>
  </div>;
}

function InfoField({ label, value, multiline = false }: { label: string; value: ReactNode; multiline?: boolean }) { return <div><Label className="text-xs text-muted-foreground">{label}</Label><p className={`mt-1 text-sm ${multiline ? "whitespace-pre-wrap leading-6" : "font-medium"}`}>{value}</p></div>; }
function FormField({ label, children }: { label: string; children: ReactNode }) { return <div className="space-y-1.5"><Label>{label}</Label>{children}</div>; }
function FieldError({ text }: { text: string }) { return <p className="text-xs text-destructive">{text}</p>; }
function ActivityDot() { return <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary"><CheckCircle2 className="h-3.5 w-3.5" /></span>; }
function SystemTimelineEntry({ update, t, enums, dateLocale, actorName }: { update: import("@/lib/api/risks").RiskActionUpdateDto; t: ReturnType<typeof useTranslations>; enums: ReturnType<typeof useTranslations>; dateLocale: string; actorName: string }) {
  return <article className="rounded-lg border border-primary/15 bg-primary/[0.035] p-3 text-sm"><div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><History className="h-3.5 w-3.5 text-primary" /><span className="font-semibold text-foreground">{t("systemUpdate")}</span><span>·</span><span>{actorName}</span><span>·</span><time dateTime={update.createdAt}>{formatDateTime(update.createdAt, dateLocale)}</time></div><p className="mt-2 text-xs text-muted-foreground">{t("actionUpdated")}</p><div className="mt-3 space-y-2">{update.changes.map((change) => <div key={`${update.id}-${change.field}`} className="grid gap-1 rounded-md border bg-background/70 px-2.5 py-2 sm:grid-cols-[minmax(100px,0.7fr)_minmax(0,1fr)_auto_minmax(0,1fr)] sm:items-center"><span className="text-xs font-semibold">{timelineFieldLabel(change.field, t)}</span><span className="min-w-0 break-words text-xs text-muted-foreground">{formatTimelineValue(change.field, change.oldValue, t, enums, dateLocale)}</span><ArrowRight className="hidden h-3.5 w-3.5 text-muted-foreground sm:block" /><span className="min-w-0 break-words text-xs font-medium">{formatTimelineValue(change.field, change.newValue, t, enums, dateLocale)}</span></div>)}</div></article>;
}
function timelineFieldLabel(field: string, t: ReturnType<typeof useTranslations>) { return ({ Title: t("titleField"), Description: t("descriptionField"), Owner: t("owner"), TargetDate: t("targetDate"), NextFollowUpDate: t("nextFollowUpDate"), Status: t("status"), SortOrder: t("sortOrder") } as Record<string, string>)[field] ?? field; }
function formatTimelineValue(field: string, value: string | null | undefined, t: ReturnType<typeof useTranslations>, enums: ReturnType<typeof useTranslations>, dateLocale: string) { if (!value) return t("notSet"); if (field === "Status") return enums(RiskActionStatusHelper.getLabelKey(value as RiskActionStatus) ?? "unknown"); if (field === "TargetDate" || field === "NextFollowUpDate") return formatDate(value, dateLocale); return value.length > 180 ? `${value.slice(0, 177)}…` : value; }
function RiskActionStatusText({ value, enums }: { value: RiskActionStatus; enums: ReturnType<typeof useTranslations> }) { return <span className={`font-semibold ${getStatusTextClass(value)}`}>{enums(RiskActionStatusHelper.getLabelKey(value) ?? "unknown")}</span>; }
function ResourceSelect({ value, resources, noneLabel, onChange }: { value: string; resources: readonly { id: string; name: string }[]; noneLabel: string; onChange: (_value: string) => void }) { return <Select value={value || "none"} onValueChange={(next) => onChange(next === "none" ? "" : next)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{noneLabel}</SelectItem>{resources.map((resource) => <SelectItem key={resource.id} value={resource.id}>{resource.name}</SelectItem>)}</SelectContent></Select>; }
function ActionListItem({ action, selected, enums, t, onSelect }: { action: RiskActionDto; selected: boolean; enums: ReturnType<typeof useTranslations>; t: ReturnType<typeof useTranslations>; onSelect: () => void }) { return <button type="button" onClick={onSelect} aria-pressed={selected} className={`w-full rounded-lg border p-3 text-start transition-colors ${selected ? riskSelectedActionClass : "hover:bg-muted/50"}`}><span className="block truncate text-sm font-semibold">{action.title}</span><span className="mt-1 block truncate text-xs text-muted-foreground"><span className={`font-semibold ${getStatusTextClass(action.status)}`}>{enums(RiskActionStatusHelper.getLabelKey(action.status) ?? "unknown")}</span>{action.ownerResourceName ? ` · ${t("actionOwner")}: ${action.ownerResourceName}` : ""}{action.targetDate ? ` · ${t("actionTargetDate")}: ${toDateInput(action.targetDate)}` : ""}{action.nextFollowUpDate ? ` · ${t("actionFollowUpDate")}: ${toDateInput(action.nextFollowUpDate)}` : ""}</span></button>; }
function ActionForm({ value, setValue, resources, enums, t, submitted }: { value: ActionDraft; setValue: (_value: ActionDraft) => void; resources: readonly { id: string; name: string }[]; enums: ReturnType<typeof useTranslations>; t: ReturnType<typeof useTranslations>; submitted: boolean }) { return <div className="space-y-4"><FormField label={t("actionTitle")}><Input value={value.title} aria-invalid={submitted && !value.title.trim()} onChange={(event) => setValue({ ...value, title: event.target.value })} />{submitted && !value.title.trim() ? <FieldError text={t("required")} /> : null}</FormField><FormField label={t("actionStatus")}><Select value={value.status} onValueChange={(status) => setValue({ ...value, status: status as RiskActionStatus })}><SelectTrigger className={`${getStatusTextClass(value.status)} font-medium`}><SelectValue /></SelectTrigger><SelectContent>{RISK_ACTION_STATUSES.map((status) => <SelectItem key={status} value={status}><span className="flex items-center gap-2"><span className={`h-1.5 w-1.5 rounded-full ${statusToneDotClasses[getStatusTone(status)]}`} />{enums(RiskActionStatusHelper.getLabelKey(status) ?? "unknown")}</span></SelectItem>)}</SelectContent></Select></FormField><FormField label={t("actionDescription")}><Textarea value={value.description} onChange={(event) => setValue({ ...value, description: event.target.value })} /></FormField><FormField label={t("owner")}><ResourceSelect value={value.ownerResourceId} resources={resources} noneLabel={t("noOwner")} onChange={(ownerResourceId) => setValue({ ...value, ownerResourceId })} /></FormField><div className="grid gap-4 sm:grid-cols-2"><FormField label={t("targetDate")}><Input type="date" value={value.targetDate} onChange={(event) => setValue({ ...value, targetDate: event.target.value })} /></FormField><FormField label={t("nextFollowUpDate")}><Input type="date" value={value.nextFollowUpDate} onChange={(event) => setValue({ ...value, nextFollowUpDate: event.target.value })} /></FormField></div></div>; }
