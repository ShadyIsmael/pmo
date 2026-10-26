import * as React from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, CalendarDays, Clock3, FolderKanban, History, Loader2, MessageSquare, Pencil, Plus, Save, Trash2, UserRound } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { getStatusTextClass } from "@lib/status-presentation";
import { useResources } from "@features/resources/api/resources";
import { useUserLookup } from "@features/projects/lib/project-lookups";
import {
  useAddRiskActionUpdate,
  useCreateRiskAction,
  useCreateRiskImpact,
  useDeleteRiskAction,
  useDeleteRiskImpact,
  useRisk,
  useRiskActionUpdates,
  useRiskActions,
  useRiskImpacts,
  useUpdateRisk,
  useUpdateRiskAction,
  useUpdateRiskImpact,
  useUpdateRiskStatus,
  type RiskActionDto,
  type RiskActionStatus,
  type RiskImpactType,
  type RiskLevel,
  type RiskStatus,
} from "./api/risks";

const RISK_LEVELS: RiskLevel[] = ["Low", "Medium", "High", "Critical"];
const RISK_ACTION_STATUSES: RiskActionStatus[] = ["Planned", "InProgress", "Completed", "Cancelled"];
const RISK_IMPACT_TYPES: RiskImpactType[] = ["Schedule", "Budget", "Scope", "Quality", "Resources", "Procurement", "Compliance", "Reputation", "Operations", "Other"];

type RiskDraft = { title: string; description: string; riskLevel: RiskLevel; ownerResourceId: string };
type ActionDraft = { title: string; description: string; ownerResourceId: string; targetDate: string; nextFollowUpDate: string; status: RiskActionStatus };
const emptyActionDraft: ActionDraft = { title: "", description: "", ownerResourceId: "", targetDate: "", nextFollowUpDate: "", status: "Planned" };

export function RiskDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { t, formatDate } = useI18n();
  const formatDateTime = (value: string) => formatDate(value, { dateStyle: "medium", timeStyle: "short" });
  const { user } = useAuth();
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Edit));
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Delete));
  const canMitigate = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Mitigate));
  const projectId = searchParams.get("projectId") ?? "";
  const fromProject = searchParams.get("from") === "project" && Boolean(projectId);
  const requestedActionId = searchParams.get("actionId") ?? "";
  const [selectedAction, setSelectedAction] = React.useState(requestedActionId);
  const [editingRisk, setEditingRisk] = React.useState(false);
  const [riskDraft, setRiskDraft] = React.useState<RiskDraft>({ title: "", description: "", riskLevel: "Low", ownerResourceId: "" });
  const [riskSubmitted, setRiskSubmitted] = React.useState(false);
  const [showActionForm, setShowActionForm] = React.useState(searchParams.get("addAction") === "1");
  const [editingAction, setEditingAction] = React.useState(false);
  const [actionSubmitted, setActionSubmitted] = React.useState(false);
  const [actionDraft, setActionDraft] = React.useState<ActionDraft>(emptyActionDraft);
  const [updateText, setUpdateText] = React.useState("");
  const [impactTypes, setImpactTypes] = React.useState<RiskImpactType[]>([]);
  const [impactDescription, setImpactDescription] = React.useState("");
  const [editingImpact, setEditingImpact] = React.useState(false);

  const query = useRisk(id, Boolean(id));
  const actions = useRiskActions(id, query.isSuccess);
  const impacts = useRiskImpacts(id, query.isSuccess);
  const updates = useRiskActionUpdates(id, selectedAction, Boolean(selectedAction));
  const createAction = useCreateRiskAction();
  const updateAction = useUpdateRiskAction();
  const deleteAction = useDeleteRiskAction();
  const addUpdate = useAddRiskActionUpdate();
  const createImpact = useCreateRiskImpact();
  const updateImpact = useUpdateRiskImpact();
  const deleteImpact = useDeleteRiskImpact();
  const updateRisk = useUpdateRisk();
  const updateStatus = useUpdateRiskStatus();
  const lookupsEnabled = editingRisk || showActionForm || editingAction;
  const resources = useResources({ page: 1, pageSize: 100, isActive: true }, lookupsEnabled);
  const users = useUserLookup(undefined, Boolean(selectedAction));
  const risk = query.data;
  const actionItems = React.useMemo(() => actions.data ?? [], [actions.data]);
  const selected = actionItems.find((action) => action.id === selectedAction);
  const impact = impacts.data?.[0];
  const actionUpdates = React.useMemo(() => [...(updates.data ?? [])].sort((left, right) => Date.parse(left.createdAt) - Date.parse(right.createdAt)), [updates.data]);

  React.useEffect(() => {
    if (risk) setRiskDraft({ title: risk.title, description: risk.description, riskLevel: risk.riskLevel, ownerResourceId: risk.ownerResourceId ?? "" });
  }, [risk]);

  React.useEffect(() => {
    if (impact) { setImpactTypes(impact.impactTypes); setImpactDescription(impact.description); }
  }, [impact]);

  React.useEffect(() => {
    if (!actions.isSuccess || actions.isFetching) return;
    const next = actionItems.find((action) => action.id === requestedActionId)
      ?? actionItems.find((action) => action.id === selectedAction)
      ?? actionItems[0];
    if (next?.id !== selectedAction) setSelectedAction(next?.id ?? "");
  }, [actions.isFetching, actions.isSuccess, actionItems, requestedActionId, selectedAction]);

  const backHref = fromProject && risk?.projectPublicId ? `/projects/${encodeURIComponent(risk.projectPublicId)}/risks-issues` : "/risks-issues/risks";
  const ownerName = (resourceId?: string) => resources.data?.items.find((resource) => resource.id === resourceId)?.name;
  const userName = (userId: string) => users.data?.find((item) => item.id.toLowerCase() === userId.toLowerCase())?.fullName ?? t("risks.unknownUser");
  const setActionUrl = (actionId?: string) => {
    const next = new URLSearchParams(searchParams);
    next.set("from", fromProject ? "project" : "risks");
    if (fromProject) next.set("projectId", projectId);
    else next.delete("projectId");
    next.delete("addAction");
    if (actionId) next.set("actionId", actionId);
    else next.delete("actionId");
    setSearchParams(next, { replace: true });
  };

  const openRiskEdit = () => {
    if (!risk) return;
    updateRisk.reset();
    setRiskSubmitted(false);
    setRiskDraft({ title: risk.title, description: risk.description, riskLevel: risk.riskLevel, ownerResourceId: risk.ownerResourceId ?? "" });
    setEditingRisk(true);
  };

  const openActionCreate = () => {
    createAction.reset();
    updateAction.reset();
    setActionDraft(emptyActionDraft);
    setActionSubmitted(false);
    setEditingAction(false);
    setShowActionForm(true);
  };

  const openActionEdit = (action: RiskActionDto | undefined = selected) => {
    if (!action) return;
    updateAction.reset();
    createAction.reset();
    setActionDraft({ title: action.title, description: action.description, ownerResourceId: action.ownerResourceId ?? "", targetDate: action.targetDate?.slice(0, 10) ?? "", nextFollowUpDate: action.nextFollowUpDate?.slice(0, 10) ?? "", status: action.status });
    setActionSubmitted(false);
    setEditingAction(true);
    setShowActionForm(false);
  };

  async function saveRisk(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setRiskSubmitted(true);
    if (!risk || !riskDraft.title.trim() || !riskDraft.description.trim()) return;
    try {
      await updateRisk.mutateAsync({ id: risk.id, data: { ...riskDraft, title: riskDraft.title.trim(), description: riskDraft.description.trim(), ownerResourceId: riskDraft.ownerResourceId || undefined, rowVersion: risk.rowVersion } });
      setEditingRisk(false);
    } catch { /* mutation state is rendered beside the form */ }
  }

  async function transitionRisk(status: RiskStatus) {
    if (!risk || updateStatus.isPending) return;
    try { await updateStatus.mutateAsync({ id: risk.id, status, rowVersion: risk.rowVersion }); } catch { /* mutation state is rendered on the page */ }
  }

  async function saveAction(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setActionSubmitted(true);
    if (!risk || !actionDraft.title.trim()) return;
    const payload = { title: actionDraft.title.trim(), description: actionDraft.description.trim(), ownerResourceId: actionDraft.ownerResourceId || undefined, targetDate: actionDraft.targetDate || undefined, nextFollowUpDate: actionDraft.nextFollowUpDate || undefined, status: actionDraft.status };
    try {
      let saved: RiskActionDto;
      if (editingAction && selected) {
        saved = await updateAction.mutateAsync({ riskId: risk.id, actionId: selected.id, data: { ...payload, sortOrder: selected.sortOrder, rowVersion: selected.rowVersion } });
      } else {
        saved = await createAction.mutateAsync({ riskId: risk.id, data: { ...payload, sortOrder: actionItems.length } });
      }
      setSelectedAction(saved.id);
      setActionUrl(saved.id);
      setShowActionForm(false);
      setEditingAction(false);
      setActionDraft(emptyActionDraft);
    } catch { /* mutation state is rendered in the action sheet */ }
  }

  async function saveUpdate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!risk || !selectedAction || !updateText.trim()) return;
    try {
      await addUpdate.mutateAsync({ riskId: risk.id, actionId: selectedAction, updateText: updateText.trim() });
      setUpdateText("");
    } catch { /* mutation state is rendered beside the update form */ }
  }

  async function saveImpact(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!risk || !impactTypes.length || !impactDescription.trim()) return;
    try {
      if (impact) {
        await updateImpact.mutateAsync({ riskId: risk.id, impactId: impact.id, data: { impactTypes, description: impactDescription.trim(), rowVersion: impact.rowVersion } });
        setEditingImpact(false);
      } else {
        await createImpact.mutateAsync({ riskId: risk.id, data: { impactTypes, description: impactDescription.trim() } });
        setImpactTypes([]);
        setImpactDescription("");
      }
    } catch { /* mutation state is rendered beside the impact form */ }
  }

  function openImpactEdit() {
    if (!impact) return;
    updateImpact.reset();
    createImpact.reset();
    setImpactTypes(impact.impactTypes);
    setImpactDescription(impact.description);
    setEditingImpact(true);
  }

  function cancelImpactEdit() {
    setEditingImpact(false);
    setImpactTypes([]);
    setImpactDescription("");
  }

  async function removeImpact() {
    if (!risk || !impact) return;
    try {
      await deleteImpact.mutateAsync({ riskId: risk.id, impactId: impact.id });
    } catch { /* mutation state is rendered beside the impact form */ }
  }

  async function removeAction(actionId: string) {
    if (!risk) return;
    try {
      await deleteAction.mutateAsync({ riskId: risk.id, actionId });
      if (selectedAction === actionId) {
        setSelectedAction("");
        setActionUrl(undefined);
      }
    } catch { /* mutation state is rendered in the action list */ }
  }

  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !risk) return <div className="space-y-4"><Button size="sm" variant="ghost" onClick={() => navigate(backHref)}><ArrowLeft className="me-1.5 h-4 w-4" />{fromProject ? t("risks.backToProject") : t("risks.backToRegister")}</Button><p role="alert" className="py-8 text-center text-sm text-destructive">{t("risks.notFound")}</p></div>;

  const canMarkMitigated = ["Identified", "ActionRequired", "FollowUp"].includes(risk.status);
  const canCloseRisk = risk.status === "Mitigated";
  const actionPending = createAction.isPending || updateAction.isPending;
  const impactPending = createImpact.isPending || updateImpact.isPending;
  const riskRequired = (value: string) => riskSubmitted && !value.trim() ? <p className="text-xs text-destructive">{t("risks.required")}</p> : null;

  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <Button size="sm" variant="ghost" onClick={() => navigate(backHref)}><ArrowLeft className="me-1.5 h-4 w-4" />{fromProject ? t("risks.backToProject") : t("risks.backToRegister")}</Button>
      <div className="flex flex-wrap gap-2">
        {canEdit ? <Button size="sm" variant="outline" onClick={openRiskEdit}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("risks.editRisk")}</Button> : null}
        {canMitigate && canMarkMitigated ? <Button size="sm" variant="outline" onClick={() => void transitionRisk("Mitigated")} disabled={updateStatus.isPending}>{updateStatus.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{t("risks.markMitigated")}</Button> : null}
        {canMitigate && canCloseRisk ? <Button size="sm" variant="outline" onClick={() => void transitionRisk("Closed")} disabled={updateStatus.isPending}>{updateStatus.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{t("risks.closeRisk")}</Button> : null}
      </div>
    </div>
    {updateStatus.isError ? <p role="alert" className="text-sm text-destructive">{t("risks.statusUpdateError")}</p> : null}

    <Card className="overflow-hidden border-primary/15 bg-card/80"><CardContent className="flex flex-col gap-4 p-5 sm:p-6 lg:flex-row lg:items-start lg:justify-between"><div className="min-w-0"><p className="mb-2 text-xs font-medium uppercase tracking-[0.18em] text-primary">{t("risks.riskOverview")}</p><h1 className="text-xl font-bold tracking-tight sm:text-2xl">{risk.title}</h1><Link to={`/projects/${encodeURIComponent(risk.projectPublicId)}/risks-issues`} className="mt-1 inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"><FolderKanban className="h-3.5 w-3.5" />{risk.projectName || "—"}</Link></div><div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm"><span><span className="text-muted-foreground">{t("risks.riskLevel")}: </span><span className={getStatusTextClass(risk.riskLevel)}>{t(`enums.riskLevel.${risk.riskLevel.toLowerCase()}`)}</span></span><span><span className="text-muted-foreground">{t("risks.status")}: </span><span className={getStatusTextClass(risk.status)}>{t(`enums.riskStatus.${risk.status.charAt(0).toLowerCase()}${risk.status.slice(1)}`)}</span></span></div></CardContent></Card>

    <Card>
      <CardHeader><CardTitle>{t("risks.overview")}</CardTitle></CardHeader>
      <CardContent className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(260px,0.8fr)]">
        <div className="space-y-5 text-sm">
          <p className="whitespace-pre-wrap leading-6 text-muted-foreground">{risk.description || "—"}</p>
          <div className="grid gap-4 sm:grid-cols-2">
            <div><Label className="text-xs text-muted-foreground">{t("risks.project")}</Label><p className="mt-1"><Link to={`/projects/${encodeURIComponent(risk.projectPublicId)}/risks-issues`} className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline"><FolderKanban className="h-3.5 w-3.5" />{risk.projectName || "—"}</Link></p></div>
            <div><Label className="text-xs text-muted-foreground">{t("risks.owner")}</Label><p className="mt-1 font-medium">{risk.ownerResourceName || "—"}</p></div>
            <div><Label className="text-xs text-muted-foreground">{t("risks.identifiedDate")}</Label><p className="mt-1 font-medium">{formatDate(risk.createdAt)}</p></div>
            <div><Label className="text-xs text-muted-foreground">{t("risks.lastUpdate")}</Label><p className="mt-1 font-medium">{formatDate(risk.updatedAt ?? risk.createdAt)}</p></div>
          </div>
        </div>
        <div className="rounded-xl border bg-muted/20 p-4">
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("risks.riskImpact")}</p>
          {impacts.isError ? <p role="alert" className="mt-4 text-sm text-destructive">{t("risks.loadError")}</p> : impacts.isLoading ? <p className="mt-4 text-sm text-muted-foreground">{t("common.loading")}</p> : impact ? <div className="mt-4 space-y-4"><div className="flex flex-wrap gap-2">{impact.impactTypes.map((value) => <span key={value} className="text-sm font-medium text-primary">{t(`enums.riskImpactType.${value.charAt(0).toLowerCase()}${value.slice(1)}`)}</span>)}</div><p className="whitespace-pre-wrap text-sm leading-6 text-muted-foreground">{impact.description || "—"}</p>{canEdit && !editingImpact ? <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" onClick={openImpactEdit}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("risks.editImpact")}</Button>{canDelete ? <Button size="sm" variant="ghost" onClick={() => void removeImpact()} disabled={deleteImpact.isPending}>{deleteImpact.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <Trash2 className="me-1.5 h-3.5 w-3.5" />}{t("risks.removeImpact")}</Button> : null}</div> : null}{deleteImpact.isError ? <p role="alert" className="text-sm text-destructive">{t("risks.riskSaveError")}</p> : null}</div> : <p className="mt-4 text-sm text-muted-foreground">{t("risks.noImpacts")}</p>}
          {canEdit && (!impact || editingImpact) ? <form onSubmit={(event) => void saveImpact(event)} className="mt-4 space-y-3 border-t pt-4"><Label>{t("risks.selectImpactTypes")}</Label><div className="grid gap-2 sm:grid-cols-2">{RISK_IMPACT_TYPES.map((impactType) => <label key={impactType} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={impactTypes.includes(impactType)} onChange={() => setImpactTypes((current) => current.includes(impactType) ? current.filter((item) => item !== impactType) : [...current, impactType])} />{t(`enums.riskImpactType.${impactType.charAt(0).toLowerCase()}${impactType.slice(1)}`)}</label>)}</div><Label htmlFor="risk-impact-description">{t("risks.impactDescription")}</Label><Textarea id="risk-impact-description" value={impactDescription} onChange={(event) => setImpactDescription(event.target.value)} rows={3} />{createImpact.isError || updateImpact.isError ? <p role="alert" className="text-sm text-destructive">{t("risks.riskSaveError")}</p> : null}<div className="flex flex-wrap gap-2"><Button size="sm" type="submit" disabled={impactPending || !impactTypes.length || !impactDescription.trim()}>{impactPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : editingImpact ? <Save className="me-1.5 h-3.5 w-3.5" /> : <Plus className="me-1.5 h-3.5 w-3.5" />}{editingImpact ? t("risks.saveChanges") : t("risks.addImpact")}</Button>{editingImpact ? <Button size="sm" type="button" variant="ghost" onClick={cancelImpactEdit}>{t("risks.cancel")}</Button> : null}</div></form> : null}
        </div>
      </CardContent>
    </Card>

    <div className="grid gap-4 lg:grid-cols-[minmax(260px,0.8fr)_minmax(0,1.4fr)]">
      <Card className="h-fit"><CardHeader className="flex flex-row items-center justify-between gap-2"><CardTitle>{t("risks.actions")}</CardTitle>{canEdit ? <Button size="sm" onClick={openActionCreate}><Plus className="me-1.5 h-3.5 w-3.5" />{t("risks.addAction")}</Button> : null}</CardHeader><CardContent className="space-y-2">
        {actions.isLoading ? <p className="py-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : actions.isError ? <p role="alert" className="py-6 text-sm text-destructive">{t("risks.loadError")}</p> : actionItems.length ? actionItems.map((action) => <div key={action.id} className={`rounded-lg border p-3 ${selectedAction === action.id ? "border-primary" : ""}`}><button type="button" className="w-full text-start" aria-pressed={selectedAction === action.id} onClick={() => { setSelectedAction(action.id); setUpdateText(""); setActionUrl(action.id); }}><span className="block truncate text-sm font-semibold">{action.title}</span><span className="mt-1 block truncate text-xs text-muted-foreground"><span className={getStatusTextClass(action.status)}>{t(`enums.riskActionStatus.${action.status.charAt(0).toLowerCase()}${action.status.slice(1)}`)}</span>{action.ownerResourceName ? ` · ${t("risks.actionOwner")}: ${action.ownerResourceName}` : ""}{action.targetDate ? ` · ${t("risks.actionTargetDate")}: ${formatDate(action.targetDate)}` : ""}{action.nextFollowUpDate ? ` · ${t("risks.actionFollowUpDate")}: ${formatDate(action.nextFollowUpDate)}` : ""}</span></button>{canEdit ? <div className="mt-2 flex justify-end gap-1"><Button size="sm" variant="ghost" onClick={() => { setSelectedAction(action.id); setActionUrl(action.id); openActionEdit(action); }}><Pencil className="me-1 h-3.5 w-3.5" />{t("risks.editAction")}</Button><Button size="sm" variant="ghost" onClick={() => void removeAction(action.id)} disabled={deleteAction.isPending}>{t("risks.deleteAction")}</Button></div> : null}</div>) : <div className="rounded-lg border border-dashed px-4 py-8 text-center"><p className="text-sm text-muted-foreground">{t("risks.noActionsWithAdd")}</p>{canEdit ? <Button className="mt-4" size="sm" onClick={openActionCreate}><Plus className="me-1.5 h-3.5 w-3.5" />{t("risks.addAction")}</Button> : null}</div>}
        {deleteAction.isError ? <p role="alert" className="text-xs text-destructive">{t("risks.actionSaveError")}</p> : null}
      </CardContent></Card>
      <Card><CardHeader className="flex flex-row items-start justify-between gap-3"><div className="min-w-0">{selected ? <><CardTitle className="truncate text-base">{selected.title}</CardTitle><div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"><span className={getStatusTextClass(selected.status)}>{t(`enums.riskActionStatus.${selected.status.charAt(0).toLowerCase()}${selected.status.slice(1)}`)}</span><span className="inline-flex items-center gap-1"><UserRound className="h-3.5 w-3.5" />{selected.ownerResourceName ?? t("risks.noOwner")}</span>{selected.targetDate ? <span className="inline-flex items-center gap-1"><CalendarDays className="h-3.5 w-3.5" />{t("risks.actionTargetDate")}: {formatDate(selected.targetDate)}</span> : null}{selected.nextFollowUpDate ? <span className="inline-flex items-center gap-1"><Clock3 className="h-3.5 w-3.5" />{t("risks.actionFollowUpDate")}: {formatDate(selected.nextFollowUpDate)}</span> : null}</div></> : <CardTitle className="text-base">{t("risks.selectAction")}</CardTitle>}</div>{selected && canEdit ? <Button size="sm" variant="outline" onClick={() => openActionEdit()}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("risks.editAction")}</Button> : null}</CardHeader><CardContent className="space-y-5">
        {selected ? <><p className="whitespace-pre-wrap text-sm text-muted-foreground">{selected.description || "—"}</p><section className="space-y-3 border-t pt-4"><h3 className="flex items-center gap-2 text-sm font-semibold"><MessageSquare className="h-4 w-4" />{t("risks.updates")}</h3>{updates.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : updates.isError ? <p role="alert" className="text-sm text-destructive">{t("risks.loadError")}</p> : actionUpdates.length ? <div className="space-y-3">{actionUpdates.map((entry) => entry.entryType === "SystemChange" ? <article key={entry.id} className="rounded-lg border border-primary/15 bg-primary/[0.035] p-3 text-sm"><div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><History className="h-3.5 w-3.5 text-primary" /><span className="font-semibold text-foreground">{t("risks.systemUpdate")}</span><span>·</span><span>{userName(entry.createdBy)}</span><span>·</span><time dateTime={entry.createdAt}>{formatDateTime(entry.createdAt)}</time></div><div className="mt-2 space-y-1">{(entry.changes ?? []).map((change) => <p key={`${entry.id}-${change.field}`} className="text-xs"><strong>{change.field}</strong>: {change.oldValue || t("risks.notSet")} → {change.newValue || t("risks.notSet")}</p>)}</div></article> : <article key={entry.id} className="rounded-lg border-s border-primary ps-3 text-sm"><div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span className="font-medium text-foreground">{userName(entry.createdBy)}</span><span>·</span><time dateTime={entry.createdAt}>{formatDateTime(entry.createdAt)}</time></div><p className="mt-1 whitespace-pre-wrap">{entry.updateText}</p></article>)}</div> : <p className="text-sm text-muted-foreground">{t("risks.noUpdates")}</p>}{canEdit ? <form onSubmit={(event) => void saveUpdate(event)} className="space-y-2 border-t pt-3"><Label htmlFor="risk-action-update">{t("risks.updateText")}</Label><Textarea id="risk-action-update" value={updateText} onChange={(event) => setUpdateText(event.target.value)} placeholder={t("risks.writeUpdate")} rows={3} /><div className="flex flex-wrap items-center justify-between gap-2">{addUpdate.isError ? <p role="alert" className="text-xs text-destructive">{t("risks.updateSaveError")}</p> : <span /> }<Button size="sm" type="submit" disabled={addUpdate.isPending || !updateText.trim()}>{addUpdate.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <Plus className="me-1.5 h-3.5 w-3.5" />}{t("risks.addUpdate")}</Button></div></form> : null}</section></> : <div className="flex min-h-56 items-center justify-center text-sm text-muted-foreground">{t("risks.selectAction")}</div>}
      </CardContent></Card>
    <SideSheet open={editingRisk} onOpenChange={(open) => { setEditingRisk(open); if (!open) setRiskSubmitted(false); }} title={t("risks.editRisk")} description={t("risks.editRiskDescription")} size="md"><form onSubmit={(event) => void saveRisk(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4"><div className="space-y-1.5"><Label htmlFor="risk-edit-title">{t("risks.titleField")}</Label><Input id="risk-edit-title" value={riskDraft.title} aria-invalid={riskSubmitted && !riskDraft.title.trim()} onChange={(event) => setRiskDraft((current) => ({ ...current, title: event.target.value }))} />{riskRequired(riskDraft.title)}</div><div className="space-y-1.5"><Label htmlFor="risk-edit-description">{t("risks.descriptionField")}</Label><Textarea id="risk-edit-description" value={riskDraft.description} aria-invalid={riskSubmitted && !riskDraft.description.trim()} onChange={(event) => setRiskDraft((current) => ({ ...current, description: event.target.value }))} />{riskRequired(riskDraft.description)}</div><div className="space-y-1.5"><Label htmlFor="risk-edit-level">{t("risks.riskLevel")}</Label><select id="risk-edit-level" className="input-field" value={riskDraft.riskLevel} onChange={(event) => setRiskDraft((current) => ({ ...current, riskLevel: event.target.value as RiskLevel }))}>{RISK_LEVELS.map((level) => <option key={level} value={level}>{t(`enums.riskLevel.${level.toLowerCase()}`)}</option>)}</select></div><div className="space-y-1.5"><Label htmlFor="risk-edit-owner">{t("risks.owner")}</Label><select id="risk-edit-owner" className="input-field" value={riskDraft.ownerResourceId} onChange={(event) => setRiskDraft((current) => ({ ...current, ownerResourceId: event.target.value }))}><option value="">{t("risks.noOwner")}</option>{riskDraft.ownerResourceId && !ownerName(riskDraft.ownerResourceId) ? <option value={riskDraft.ownerResourceId}>{risk?.ownerResourceName ?? riskDraft.ownerResourceId}</option> : null}{(resources.data?.items ?? []).map((resource) => <option key={resource.id} value={resource.id}>{resource.name}</option>)}</select>{resources.isError ? <p role="alert" className="text-xs text-destructive">{t("risks.ownerLoadError")}</p> : null}</div>{updateRisk.isError ? <p role="alert" className="text-sm text-destructive">{t("risks.riskSaveError")}</p> : null}</SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setEditingRisk(false)}>{t("risks.cancel")}</Button><Button type="submit" disabled={updateRisk.isPending}>{updateRisk.isPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{updateRisk.isPending ? t("risks.saving") : t("risks.saveChanges")}</Button></SideSheetFooter></form></SideSheet>

    <SideSheet open={showActionForm || editingAction} onOpenChange={(open) => { if (!open) { setShowActionForm(false); setEditingAction(false); setActionSubmitted(false); } }} title={editingAction ? t("risks.editActionTitle") : t("risks.addActionTitle")} description={t("risks.actionDescription")} size="md"><form onSubmit={(event) => void saveAction(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4"><div className="space-y-1.5"><Label htmlFor="risk-action-title">{t("risks.actionTitle")}</Label><Input id="risk-action-title" value={actionDraft.title} aria-invalid={actionSubmitted && !actionDraft.title.trim()} onChange={(event) => setActionDraft((current) => ({ ...current, title: event.target.value }))} />{actionSubmitted && !actionDraft.title.trim() ? <p className="text-xs text-destructive">{t("risks.required")}</p> : null}</div><div className="space-y-1.5"><Label htmlFor="risk-action-status">{t("risks.actionStatus")}</Label><select id="risk-action-status" className="input-field" value={actionDraft.status} onChange={(event) => setActionDraft((current) => ({ ...current, status: event.target.value as RiskActionStatus }))}>{RISK_ACTION_STATUSES.map((status) => <option key={status} value={status}>{t(`enums.riskActionStatus.${status.charAt(0).toLowerCase()}${status.slice(1)}`)}</option>)}</select></div><div className="space-y-1.5"><Label htmlFor="risk-action-description">{t("risks.actionDescription")}</Label><Textarea id="risk-action-description" value={actionDraft.description} onChange={(event) => setActionDraft((current) => ({ ...current, description: event.target.value }))} /></div><div className="space-y-1.5"><Label htmlFor="risk-action-owner">{t("risks.actionOwner")}</Label><select id="risk-action-owner" className="input-field" value={actionDraft.ownerResourceId} onChange={(event) => setActionDraft((current) => ({ ...current, ownerResourceId: event.target.value }))}><option value="">{t("risks.noOwner")}</option>{actionDraft.ownerResourceId && !ownerName(actionDraft.ownerResourceId) ? <option value={actionDraft.ownerResourceId}>{selected?.ownerResourceName ?? actionDraft.ownerResourceId}</option> : null}{(resources.data?.items ?? []).map((resource) => <option key={resource.id} value={resource.id}>{resource.name}</option>)}</select>{resources.isError ? <p role="alert" className="text-xs text-destructive">{t("risks.ownerLoadError")}</p> : null}</div><div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="risk-action-target">{t("risks.targetDate")}</Label><Input id="risk-action-target" type="date" value={actionDraft.targetDate} onChange={(event) => setActionDraft((current) => ({ ...current, targetDate: event.target.value }))} /></div><div className="space-y-1.5"><Label htmlFor="risk-action-follow-up">{t("risks.nextFollowUpDate")}</Label><Input id="risk-action-follow-up" type="date" value={actionDraft.nextFollowUpDate} onChange={(event) => setActionDraft((current) => ({ ...current, nextFollowUpDate: event.target.value }))} /></div></div>{createAction.isError || updateAction.isError ? <p role="alert" className="text-sm text-destructive">{t("risks.actionSaveError")}</p> : null}</SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => { setShowActionForm(false); setEditingAction(false); }}>{t("risks.cancel")}</Button><Button type="submit" disabled={actionPending}>{actionPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <Save className="me-1.5 h-3.5 w-3.5" />}{actionPending ? t("risks.saving") : editingAction ? t("risks.saveChanges") : t("risks.saveAction")}</Button></SideSheetFooter></form></SideSheet>
    </div>
  </div>;
}
