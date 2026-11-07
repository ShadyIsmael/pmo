import * as React from "react";
import { Link } from "react-router-dom";
import { ArrowDown, ArrowUp, ArrowUpRight, Pencil, Plus, Save, Trash2, X } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Textarea } from "@components/ui/Textarea";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useMeetingActionItems, useMeetingAgenda, useMeetingDecisions, useMeetingParticipants, useMeetingWorkspaceMutations, type MeetingActionItemDto, type MeetingAttendanceStatus, type MeetingAgendaItemDto, type MeetingParticipantStatusDto } from "@api/meetingsFull";
import { useFullWorkItemMutations, useFullWorkItems } from "@api/workItemsFull";
import { useMeetingWorkflowUserLookup, type WorkflowUserLookupDto } from "@features/projects/lib/project-lookups";
import { hasPermission, Permissions } from "@lib/permissions";
import { useI18n } from "@i18n/messages";
import { RichTextContent, RichTextEditor } from "@components/ui/rich-text-editor";

const ATTENDANCE_STATUSES: readonly MeetingAttendanceStatus[] = ["Present", "Late", "Excused", "Absent"];
const ACTION_STATUSES: readonly MeetingActionItemDto["status"][] = ["Pending", "Completed"];

export function MeetingWorkspaceSections({ meetingId, projectId, meetingStatus, organizerId, workspaceView = "lifecycle" }: { meetingId: string; projectId?: string | null; meetingStatus: string; organizerId?: string | null; workspaceView?: "lifecycle" | "details" | "follow-up" }) {
  const { t } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const isScheduled = meetingStatus === "Scheduled";
  const isInProgress = meetingStatus === "InProgress";
  const isCompleted = meetingStatus === "Completed";
  const showDetailsSections = workspaceView !== "follow-up";
  const showFollowUpSections = workspaceView !== "details";
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Edit) && canMutate && !effectiveReadOnly);
  const canManageActions = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage) && canMutate && !effectiveReadOnly);
  const isOrganizer = Boolean(user && organizerId && user.id === organizerId);
  const canRecordAttendance = Boolean(isInProgress && canMutate && !effectiveReadOnly && (canEdit || isOrganizer));
  const canViewWorkItems = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.View));
  const canLinkWorkItems = Boolean(showFollowUpSections && isCompleted && projectId && canManageActions && canViewWorkItems);
  const canCreateWorkItems = Boolean(canLinkWorkItems && user && hasPermission(user.permissions, Permissions.WorkItem.Create));
  const agenda = useMeetingAgenda(meetingId, canView && (isScheduled || isInProgress || isCompleted));
  const participants = useMeetingParticipants(meetingId, canView && showDetailsSections && (isInProgress || isCompleted));
  const actions = useMeetingActionItems(meetingId, canView && showFollowUpSections && (isInProgress || isCompleted));
  const decisions = useMeetingDecisions(meetingId, canView && workspaceView === "lifecycle" && isInProgress);
  const users = useMeetingWorkflowUserLookup(showFollowUpSections && canManageActions && (isInProgress || isCompleted));
  const mutations = useMeetingWorkspaceMutations(meetingId);

  if (!isScheduled && !isInProgress && !isCompleted) return null;

  return <div className={workspaceView === "follow-up" ? "space-y-5" : "grid gap-5 xl:grid-cols-2"}>
    {showDetailsSections ? <AgendaCard meetingId={meetingId} items={agenda.data ?? []} loading={agenda.isLoading} failed={agenda.isError} canEdit={canEdit && isScheduled} /> : null}
    {showDetailsSections && (isInProgress || isCompleted) ? <AttendanceCard meetingId={meetingId} items={participants.data ?? []} loading={participants.isLoading} failed={participants.isError} canEdit={canRecordAttendance} isOrganizer={isOrganizer} /> : null}
    {workspaceView === "lifecycle" && isInProgress ? <MeetingDecisionsCard query={decisions} canManage={canManageActions} saving={mutations.saveDecisions.isPending} save={async (value) => {
      await mutations.saveDecisions.mutateAsync({ decisions: value.trim() || undefined, rowVersion: decisions.data?.rowVersion });
      await decisions.refetch();
    }} /> : null}
    {showFollowUpSections && (isInProgress || isCompleted) ? <ActionItemsCard meetingId={meetingId} projectId={projectId} agendaItems={agenda.data ?? []} items={actions.data ?? []} loading={actions.isLoading} failed={actions.isError} canManage={canManageActions} canLinkWorkItems={canLinkWorkItems} canCreateWorkItems={canCreateWorkItems} canViewWorkItems={isCompleted && canViewWorkItems} users={users.data ?? []} usersLoading={users.isLoading} usersError={users.isError} retryUsers={() => void users.refetch()} formCollapsible={workspaceView === "follow-up"} /> : null}
    <div className="sr-only" aria-live="polite">{mutations.createAgendaItem.isSuccess || mutations.updateAgendaItem.isSuccess || mutations.updateOrganizerAttendance.isSuccess || mutations.updateAttendance.isSuccess || mutations.saveDecisions.isSuccess || mutations.createActionItem.isSuccess || mutations.updateActionItem.isSuccess ? t("meetings.saved") : ""}</div>
  </div>;
}

function MeetingDecisionsCard({ query, canManage, saving, save }: { query: ReturnType<typeof useMeetingDecisions>; canManage: boolean; saving: boolean; save: (decisions: string) => Promise<unknown> }) {
  const { t } = useI18n();
  const [decisionsValue, setDecisionsValue] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [saved, setSaved] = React.useState(false);
  const richTextCopy = {
    formatting: t("meetings.formatting"),
    bold: t("meetings.bold"),
    italic: t("meetings.italic"),
    underline: t("meetings.underline"),
    bulletedList: t("meetings.bulletedList"),
    numberedList: t("meetings.numberedList"),
    emoji: t("meetings.emoji"),
  };

  React.useEffect(() => {
    if (query.isSuccess) setDecisionsValue(query.data?.decisions ?? "");
  }, [query.data, query.isSuccess]);

  async function persist() {
    setError(null); setSaved(false);
    try {
      await save(decisionsValue);
      setSaved(true);
    } catch {
      setError(t("meetings.decisionsSaveError"));
    }
  }

  return <Card>
    <CardHeader><CardTitle>{t("meetings.decisions")}</CardTitle><CardDescription>{t("meetings.decisionsDescription")}</CardDescription></CardHeader>
    <CardContent className="space-y-3">
      {query.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : query.isError ? <p role="alert" className="text-sm text-destructive">{t("meetings.decisionsLoadError")}</p> : canManage && query.data?.isEditable !== false ? <RichTextEditor id="meeting-live-decisions" value={decisionsValue} onChange={(value) => { setDecisionsValue(value); setSaved(false); }} placeholder={t("meetings.decisionsPlaceholder")} ariaLabel={t("meetings.decisions")} t={richTextCopy} minHeight="min-h-28" /> : decisionsValue.trim() ? <div className="min-h-20 rounded-lg border border-input bg-muted/20 px-3 py-3"><RichTextContent value={decisionsValue} /></div> : <p className="rounded-md border border-dashed px-3 py-5 text-center text-sm text-muted-foreground">{t("meetings.noDecisions")}</p>}
      {canManage && query.data?.isEditable !== false && !query.isLoading && !query.isError ? <div className="flex flex-wrap items-center justify-between gap-2">{error ? <p role="alert" className="text-sm text-destructive">{error}</p> : saved ? <p role="status" className="text-sm text-muted-foreground">{t("meetings.decisionsSaved")}</p> : <span />}<Button type="button" size="sm" onClick={() => void persist()} disabled={saving || query.isLoading}>{saving ? t("common.saving") : t("meetings.saveDecisions")}</Button></div> : null}
    </CardContent>
  </Card>;
}

function AgendaCard({ meetingId, items, loading, failed, canEdit }: { meetingId: string; items: readonly MeetingAgendaItemDto[]; loading: boolean; failed: boolean; canEdit: boolean }) {
  const { t } = useI18n();
  const confirm = useConfirm();
  const mutations = useMeetingWorkspaceMutations(meetingId);
  const [topic, setTopic] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [duration, setDuration] = React.useState("");
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const busy = mutations.createAgendaItem.isPending || mutations.updateAgendaItem.isPending || mutations.deleteAgendaItem.isPending || mutations.reorderAgenda.isPending;

  function clearForm() {
    setTopic(""); setDescription(""); setDuration(""); setEditingId(null); setError(null);
  }

  function edit(item: MeetingAgendaItemDto) {
    setEditingId(item.id); setTopic(item.topic); setDescription(item.description ?? ""); setDuration(item.durationMinutes?.toString() ?? ""); setError(null);
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const minutes = duration.trim() ? Number(duration) : undefined;
    if (!topic.trim() || (minutes !== undefined && (!Number.isInteger(minutes) || minutes < 1 || minutes > 1440))) {
      setError(t("meetings.agendaValidation"));
      return;
    }
    setError(null);
    try {
      const body = { topic: topic.trim(), description: description.trim() || undefined, durationMinutes: minutes };
      if (editingId) {
        const current = items.find((item) => item.id === editingId);
        await mutations.updateAgendaItem.mutateAsync({ itemId: editingId, body: { ...body, rowVersion: current?.rowVersion } });
      } else await mutations.createAgendaItem.mutateAsync(body);
      clearForm();
    } catch { setError(t("meetings.actionError")); }
  }

  async function remove(item: MeetingAgendaItemDto) {
    if (!await confirm({ description: t("meetings.deleteAgendaConfirm", { topic: item.topic }), destructive: true })) return;
    setError(null);
    try { await mutations.deleteAgendaItem.mutateAsync(item.id); if (editingId === item.id) clearForm(); }
    catch { setError(t("meetings.actionError")); }
  }

  async function move(index: number, offset: -1 | 1) {
    const reordered = [...items];
    const nextIndex = index + offset;
    if (nextIndex < 0 || nextIndex >= reordered.length) return;
    [reordered[index], reordered[nextIndex]] = [reordered[nextIndex], reordered[index]];
    setError(null);
    try { await mutations.reorderAgenda.mutateAsync(reordered.map((item) => item.id)); }
    catch { setError(t("meetings.actionError")); }
  }

  return <Card>
    <CardHeader><CardTitle>{t("meetings.agendaTitle")}</CardTitle><CardDescription>{t("meetings.agendaDescription")}</CardDescription></CardHeader>
    <CardContent className="space-y-4">
      {loading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : failed ? <p role="alert" className="text-sm text-destructive">{t("meetings.loadError")}</p> : items.length === 0 ? <p className="rounded-md border border-dashed px-3 py-5 text-center text-sm text-muted-foreground">{t("meetings.agendaEmpty")}</p> : <ol className="space-y-2">{items.map((item, index) => <li key={item.id} className="flex min-w-0 items-start gap-2 rounded-md border p-3">
        <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-muted-foreground">{index + 1}</span>
        <div className="min-w-0 flex-1"><p className="break-words text-sm font-medium">{item.topic}</p>{item.description ? <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">{item.description}</p> : null}{item.durationMinutes ? <p className="mt-1 text-xs text-muted-foreground">{t("meetings.agendaDuration", { count: item.durationMinutes })}</p> : null}</div>
        {canEdit ? <div className="flex shrink-0 flex-wrap justify-end gap-1"><Button type="button" size="icon" variant="outline" className="h-8 w-8" aria-label={t("meetings.moveAgendaUp", { topic: item.topic })} disabled={busy || index === 0} onClick={() => void move(index, -1)}><ArrowUp className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="outline" className="h-8 w-8" aria-label={t("meetings.moveAgendaDown", { topic: item.topic })} disabled={busy || index === items.length - 1} onClick={() => void move(index, 1)}><ArrowDown className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="outline" className="h-8 w-8" aria-label={t("meetings.editAgenda", { topic: item.topic })} disabled={busy} onClick={() => edit(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="outline" className="h-8 w-8 text-destructive" aria-label={t("meetings.deleteAgenda", { topic: item.topic })} disabled={busy} onClick={() => void remove(item)}><Trash2 className="h-3.5 w-3.5" /></Button></div> : null}
      </li>)}</ol>}
      {canEdit ? <form onSubmit={(event) => void save(event)} className="space-y-3 border-t pt-4">
        <div className="space-y-1.5"><Label htmlFor="meeting-agenda-topic">{t("meetings.agendaTopic")}</Label><Input id="meeting-agenda-topic" value={topic} maxLength={500} onChange={(event) => setTopic(event.target.value)} required /></div>
        <div className="space-y-1.5"><Label htmlFor="meeting-agenda-description">{t("meetings.agendaItemDescription")}</Label><Textarea id="meeting-agenda-description" value={description} maxLength={4000} rows={2} onChange={(event) => setDescription(event.target.value)} /></div>
        <div className="space-y-1.5 sm:max-w-48"><Label htmlFor="meeting-agenda-duration">{t("meetings.agendaDurationLabel")}</Label><Input id="meeting-agenda-duration" type="number" min={1} max={1440} value={duration} onChange={(event) => setDuration(event.target.value)} /></div>
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        <div className="flex flex-wrap justify-end gap-2">{editingId ? <Button type="button" size="sm" variant="outline" onClick={clearForm}><X className="me-1.5 h-4 w-4" />{t("common.cancel")}</Button> : null}<Button type="submit" size="sm" disabled={busy}>{editingId ? <Save className="me-1.5 h-4 w-4" /> : <Plus className="me-1.5 h-4 w-4" />}{busy ? t("common.saving") : editingId ? t("meetings.saveAgenda") : t("meetings.addAgenda")}</Button></div>
      </form> : null}
    </CardContent>
  </Card>;
}

function AttendanceCard({ meetingId, items, loading, failed, canEdit, isOrganizer }: { meetingId: string; items: readonly MeetingParticipantStatusDto[]; loading: boolean; failed: boolean; canEdit: boolean; isOrganizer: boolean }) {
  const { t } = useI18n();
  const mutations = useMeetingWorkspaceMutations(meetingId);
  const [error, setError] = React.useState<string | null>(null);
  const busy = mutations.updateOrganizerAttendance.isPending || mutations.updateAttendance.isPending;

  async function changeStatus(item: MeetingParticipantStatusDto, value: string) {
    if (!ATTENDANCE_STATUSES.includes(value as MeetingAttendanceStatus)) return;
    setError(null);
    try {
      if (isOrganizer) await mutations.updateOrganizerAttendance.mutateAsync({ participantId: item.id, status: value as MeetingAttendanceStatus, rowVersion: item.rowVersion });
      else if (canEdit) await mutations.updateAttendance.mutateAsync({ participantId: item.id, status: value as MeetingAttendanceStatus, rowVersion: item.rowVersion });
    } catch { setError(t("meetings.attendanceSaveError")); }
  }

  return <Card>
    <CardHeader className="pb-3"><CardTitle>{t("meetings.attendanceTitle")}</CardTitle><CardDescription>{t("meetings.attendanceDescription")}</CardDescription></CardHeader>
    <CardContent className="space-y-3">
      {loading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : failed ? <p role="alert" className="text-sm text-destructive">{t("meetings.loadError")}</p> : items.length === 0 ? <p className="rounded-md border border-dashed px-3 py-5 text-center text-sm text-muted-foreground">{t("meetings.noUsers")}</p> : <ul className="space-y-2">{items.map((item) => {
        return <li key={item.id} className="flex min-w-0 flex-wrap items-center justify-between gap-3 rounded-md border p-3">
          <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{item.displayName?.trim() || item.email}</p><p className="truncate text-xs text-muted-foreground">{item.email}</p></div>
          {canEdit ? <div className="w-full sm:w-44"><Label className="sr-only" htmlFor={`attendance-${item.id}`}>{t("meetings.attendanceFor", { name: item.displayName || item.email })}</Label><Select value={item.actualAttendanceStatus ?? undefined} onValueChange={(value) => void changeStatus(item, value)}><SelectTrigger id={`attendance-${item.id}`} disabled={busy}><SelectValue placeholder={t("meetings.attendance.notRecorded")} /></SelectTrigger><SelectContent>{ATTENDANCE_STATUSES.map((status) => <SelectItem key={status} value={status}>{t(`meetings.attendance.${status.toLowerCase()}`)}</SelectItem>)}</SelectContent></Select></div> : <span className="text-sm text-muted-foreground">{item.actualAttendanceStatus ? t(`meetings.attendance.${item.actualAttendanceStatus.toLowerCase()}`) : t("meetings.attendance.notRecorded")}</span>}
        </li>;
      })}</ul>}
      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    </CardContent>
  </Card>;
}

function ActionItemsCard({ meetingId, projectId, agendaItems, items, loading, failed, canManage, canLinkWorkItems, canCreateWorkItems, canViewWorkItems, users, usersLoading, usersError, retryUsers, formCollapsible }: { meetingId: string; projectId?: string | null; agendaItems: readonly MeetingAgendaItemDto[]; items: readonly MeetingActionItemDto[]; loading: boolean; failed: boolean; canManage: boolean; canLinkWorkItems: boolean; canCreateWorkItems: boolean; canViewWorkItems: boolean; users: readonly WorkflowUserLookupDto[]; usersLoading: boolean; usersError: boolean; retryUsers: () => void; formCollapsible: boolean }) {
  const { t, formatDate } = useI18n();
  const confirm = useConfirm();
  const mutations = useMeetingWorkspaceMutations(meetingId);
  const workItemMutations = useFullWorkItemMutations();
  const [workItemSearch, setWorkItemSearch] = React.useState("");
  const deferredWorkItemSearch = React.useDeferredValue(workItemSearch.trim());
  const tasks = useFullWorkItems({ projectId: projectId ?? undefined, type: "Task", search: deferredWorkItemSearch || undefined, page: 1, pageSize: 50 }, canLinkWorkItems);
  const issues = useFullWorkItems({ projectId: projectId ?? undefined, type: "Issue", search: deferredWorkItemSearch || undefined, page: 1, pageSize: 50 }, canLinkWorkItems);
  const linkableWorkItems = React.useMemo(() => [...(tasks.data?.items ?? []), ...(issues.data?.items ?? [])].sort((left, right) => left.title.localeCompare(right.title)), [issues.data?.items, tasks.data?.items]);
  const [title, setTitle] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [assigneeId, setAssigneeId] = React.useState("");
  const [dueDate, setDueDate] = React.useState("");
  const [agendaItemId, setAgendaItemId] = React.useState("");
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [selectedWorkItemIds, setSelectedWorkItemIds] = React.useState<Record<string, string>>({});
  const [linkBusyId, setLinkBusyId] = React.useState<string | null>(null);
  const [pendingCreatedWorkItem, setPendingCreatedWorkItem] = React.useState<{ actionItemId: string; workItemId: string } | null>(null);
  const [actionFormOpen, setActionFormOpen] = React.useState(!formCollapsible);
  const [error, setError] = React.useState<string | null>(null);
  const busy = mutations.createActionItem.isPending || mutations.updateActionItem.isPending || mutations.deleteActionItem.isPending || mutations.linkActionWorkItem.isPending || mutations.unlinkActionWorkItem.isPending || workItemMutations.create.isPending;

  function clearForm() { setTitle(""); setDescription(""); setAssigneeId(""); setDueDate(""); setAgendaItemId(""); setEditingId(null); setActionFormOpen(!formCollapsible); setError(null); }
  function edit(item: MeetingActionItemDto) {
    setEditingId(item.id); setActionFormOpen(true); setTitle(item.title); setDescription(item.description ?? ""); setAssigneeId(item.assigneeId ?? ""); setDueDate(item.dueDate?.slice(0, 10) ?? ""); setAgendaItemId(item.agendaItemId ?? ""); setError(null);
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!title.trim()) { setError(t("meetings.actionItemValidation")); return; }
    setError(null);
    try {
      const body = { title: title.trim(), description: description.trim() || undefined, assigneeId: assigneeId || undefined, dueDate: dueDate ? `${dueDate}T00:00:00` : undefined, agendaItemId: agendaItemId || undefined };
      if (editingId) {
        const current = items.find((item) => item.id === editingId);
        await mutations.updateActionItem.mutateAsync({ itemId: editingId, body: { ...body, status: current?.status ?? "Pending", rowVersion: current?.rowVersion } });
      } else await mutations.createActionItem.mutateAsync(body);
      clearForm();
    } catch { setError(t("meetings.actionError")); }
  }

  async function changeStatus(item: MeetingActionItemDto, status: MeetingActionItemDto["status"]) {
    setError(null);
    try { await mutations.updateActionItem.mutateAsync({ itemId: item.id, body: { title: item.title, description: item.description ?? undefined, assigneeId: item.assigneeId ?? undefined, dueDate: item.dueDate ?? undefined, agendaItemId: item.agendaItemId ?? undefined, status, rowVersion: item.rowVersion } }); }
    catch { setError(t("meetings.actionError")); }
  }

  async function remove(item: MeetingActionItemDto) {
    if (!await confirm({ description: t("meetings.deleteActionConfirm", { title: item.title }), destructive: true })) return;
    setError(null);
    try { await mutations.deleteActionItem.mutateAsync(item.id); if (editingId === item.id) clearForm(); }
    catch { setError(t("meetings.actionError")); }
  }

  async function linkWorkItem(actionItem: MeetingActionItemDto, workItemId: string) {
    if (!workItemId) return;
    setError(null);
    setLinkBusyId(actionItem.id);
    try {
      await mutations.linkActionWorkItem.mutateAsync({ itemId: actionItem.id, workItemId });
      setSelectedWorkItemIds((current) => ({ ...current, [actionItem.id]: "" }));
      setPendingCreatedWorkItem((current) => current?.actionItemId === actionItem.id ? null : current);
    } catch {
      setError(t("meetings.workItemLinkError"));
    } finally {
      setLinkBusyId(null);
    }
  }

  async function unlinkWorkItem(actionItem: MeetingActionItemDto) {
    setError(null);
    setLinkBusyId(actionItem.id);
    try {
      await mutations.unlinkActionWorkItem.mutateAsync(actionItem.id);
    } catch {
      setError(t("meetings.workItemLinkError"));
    } finally {
      setLinkBusyId(null);
    }
  }

  async function createLinkedWorkItem(actionItem: MeetingActionItemDto) {
    if (!projectId) return;
    setError(null);
    setLinkBusyId(actionItem.id);
    try {
      const created = await workItemMutations.create.mutateAsync({
        title: actionItem.title,
        description: actionItem.description ?? undefined,
        type: "Task",
        priority: "Medium",
        projectId,
        dueDate: actionItem.dueDate ?? undefined,
      });
      try {
        await mutations.linkActionWorkItem.mutateAsync({ itemId: actionItem.id, workItemId: created.id });
        setPendingCreatedWorkItem(null);
      } catch {
        setPendingCreatedWorkItem({ actionItemId: actionItem.id, workItemId: created.id });
        setError(t("meetings.workItemCreateLinkError"));
      }
    } catch {
      setError(t("meetings.workItemCreateError"));
    } finally {
      setLinkBusyId(null);
    }
  }

  return <Card>
    <CardHeader className="flex flex-col gap-3 border-b bg-muted/10 sm:flex-row sm:items-start sm:justify-between"><div className="min-w-0"><CardTitle className="text-base">{t("meetings.actionItemsTitle")}</CardTitle><CardDescription>{t("meetings.actionItemsDescription")}</CardDescription></div>{canManage && formCollapsible && !editingId ? <Button type="button" size="sm" variant="outline" className="shrink-0" aria-expanded={actionFormOpen} onClick={() => { setActionFormOpen((open) => !open); setError(null); }}><Plus className="me-1.5 h-4 w-4" />{t("meetings.addActionItem")}</Button> : null}</CardHeader>
    <CardContent className="space-y-5 p-5">
      {loading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : failed ? <p role="alert" className="text-sm text-destructive">{t("meetings.loadError")}</p> : items.length === 0 ? <p className="rounded-md border border-dashed px-3 py-5 text-center text-sm text-muted-foreground">{t("meetings.actionItemsEmpty")}</p> : <ul className="space-y-2">{items.map((item) => {
        const overdue = isMeetingActionOverdue(item);
        return <li key={item.id} className={`space-y-3 rounded-md border p-3 ${overdue ? "border-destructive/40 bg-destructive/5" : ""}`}>
        <div className="flex min-w-0 flex-wrap items-start justify-between gap-3"><div className="min-w-0 flex-1"><p className="break-words text-sm font-medium">{item.title}</p>{item.description ? <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">{item.description}</p> : null}<p className={`mt-1 text-xs ${overdue ? "font-medium text-destructive" : "text-muted-foreground"}`}>{item.assigneeName || t("meetings.unassigned")}{item.dueDate ? ` · ${formatDate(item.dueDate, { dateStyle: "medium" })}` : ""}{overdue ? ` · ${t("meetings.overdue")}` : ""}</p>{item.agendaItemTopic ? <p className="mt-1 text-xs text-muted-foreground">{t("meetings.agendaReference")}: {item.agendaItemTopic}</p> : null}</div>
          {canManage ? <div className="flex flex-wrap items-center justify-end gap-1"><div className="w-36"><Label className="sr-only" htmlFor={`action-status-${item.id}`}>{t("meetings.actionStatusFor", { title: item.title })}</Label><Select value={item.status} onValueChange={(value) => { if (ACTION_STATUSES.includes(value as MeetingActionItemDto["status"])) void changeStatus(item, value as MeetingActionItemDto["status"]); }}><SelectTrigger id={`action-status-${item.id}`} disabled={busy}><SelectValue /></SelectTrigger><SelectContent>{ACTION_STATUSES.map((status) => <SelectItem key={status} value={status}>{t(`meetings.actionStatus.${status.toLowerCase()}`)}</SelectItem>)}</SelectContent></Select></div><Button type="button" size="icon" variant="outline" className="h-8 w-8" aria-label={t("meetings.editAction", { title: item.title })} disabled={busy} onClick={() => edit(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="outline" className="h-8 w-8 text-destructive" aria-label={t("meetings.deleteAction", { title: item.title })} disabled={busy} onClick={() => void remove(item)}><Trash2 className="h-3.5 w-3.5" /></Button></div> : <span className="text-sm text-muted-foreground">{t(`meetings.actionStatus.${item.status.toLowerCase()}`)}</span>}
        </div>
        {item.workItemId ? <div className="flex flex-wrap items-center gap-2 border-t pt-2">
          {canViewWorkItems ? <Button asChild type="button" size="sm" variant="outline" className="max-w-full"><Link className="min-w-0 truncate" to={`/work-items/${encodeURIComponent(item.workItemId)}`}><ArrowUpRight className="me-1.5 h-4 w-4 shrink-0" />{item.workItemTitle || t("meetings.openLinkedWorkItem")}</Link></Button> : <span className="text-xs text-muted-foreground">{t("meetings.workItemLinked")}</span>}
          <span className="text-xs text-muted-foreground">{t("meetings.workItemLifecycleHint")}</span>
          {canManage ? <Button type="button" size="sm" variant="ghost" onClick={() => void unlinkWorkItem(item)} disabled={busy}>{t("meetings.unlinkWorkItem")}</Button> : null}
        </div> : null}
        {canLinkWorkItems && projectId && !item.workItemId ? <div className="space-y-2 border-t pt-3">
          <p className="text-sm font-medium">{t("meetings.linkWorkItem")}</p>
          {tasks.isError || issues.isError ? <p role="alert" className="text-xs text-destructive">{t("meetings.workItemsLoadError")}</p> : null}
          <div className="flex flex-col gap-2 sm:flex-row">
            <SearchableSelect aria-label={t("meetings.linkWorkItem")} options={linkableWorkItems.map((workItem) => ({ value: workItem.id, label: `${workItem.title} · ${workItem.type} · ${workItem.status}` }))} value={selectedWorkItemIds[item.id] || undefined} onValueChange={(value) => setSelectedWorkItemIds((current) => ({ ...current, [item.id]: value }))} onSearchChange={setWorkItemSearch} placeholder={t("meetings.selectWorkItem")} searchPlaceholder={t("meetings.searchWorkItems")} emptyText={t("meetings.noLinkableWorkItems")} loadingText={t("common.loading")} loading={tasks.isLoading || issues.isLoading} />
            <Button type="button" variant="outline" onClick={() => void linkWorkItem(item, selectedWorkItemIds[item.id] ?? "")} disabled={busy || linkBusyId === item.id || !selectedWorkItemIds[item.id]}>{linkBusyId === item.id ? t("common.saving") : t("meetings.linkWorkItem")}</Button>
            {canCreateWorkItems && pendingCreatedWorkItem?.actionItemId !== item.id ? <Button type="button" onClick={() => void createLinkedWorkItem(item)} disabled={busy || linkBusyId === item.id}>{t("meetings.createLinkedWorkItem")}</Button> : null}
          </div>
          {pendingCreatedWorkItem?.actionItemId === item.id ? <div role="status" className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><Link className="text-primary hover:underline" to={`/work-items/${encodeURIComponent(pendingCreatedWorkItem.workItemId)}`}>{t("meetings.openCreatedWorkItem")}</Link><Button type="button" size="sm" variant="outline" onClick={() => void linkWorkItem(item, pendingCreatedWorkItem.workItemId)} disabled={busy}>{t("meetings.retryWorkItemLink")}</Button></div> : null}
        </div> : null}
        {canManage && !projectId && !item.workItemId ? <p className="border-t pt-2 text-xs text-muted-foreground">{t("meetings.actionWorkItemRequiresProject")}</p> : null}
      </li>;})}</ul>}
      {canManage && (!formCollapsible || actionFormOpen) ? <form onSubmit={(event) => void save(event)} className="space-y-3 border-t pt-4">
        <div className="space-y-1.5"><Label htmlFor="meeting-action-title">{t("meetings.actionItemTitleField")}</Label><Input id="meeting-action-title" value={title} maxLength={500} onChange={(event) => setTitle(event.target.value)} required /></div>
        <div className="space-y-1.5"><Label htmlFor="meeting-action-description">{t("meetings.actionItemDescriptionField")}</Label><Textarea id="meeting-action-description" value={description} maxLength={4000} rows={2} onChange={(event) => setDescription(event.target.value)} /></div>
        <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-action-assignee">{t("meetings.actionAssignee")}</Label><Select value={assigneeId || "none"} onValueChange={(value) => setAssigneeId(value === "none" ? "" : value)}><SelectTrigger id="meeting-action-assignee" disabled={usersLoading || usersError}><SelectValue placeholder={t("meetings.unassigned")} /></SelectTrigger><SelectContent><SelectItem value="none">{t("meetings.unassigned")}</SelectItem>{users.map((person) => <SelectItem key={person.id} value={person.id}>{person.fullName}</SelectItem>)}</SelectContent></Select>{usersError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("meetings.usersLoadError")}</span><Button type="button" size="sm" variant="outline" onClick={retryUsers}>{t("common.retry")}</Button></div> : null}</div><div className="space-y-1.5"><Label htmlFor="meeting-action-due">{t("meetings.actionDueDate")}</Label><Input id="meeting-action-due" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} /></div></div>
        {agendaItems.length ? <div className="space-y-1.5"><Label htmlFor="meeting-action-agenda">{t("meetings.agendaReference")}</Label><Select value={agendaItemId || "none"} onValueChange={(value) => setAgendaItemId(value === "none" ? "" : value)}><SelectTrigger id="meeting-action-agenda"><SelectValue placeholder={t("meetings.noAgendaReference")} /></SelectTrigger><SelectContent><SelectItem value="none">{t("meetings.noAgendaReference")}</SelectItem>{agendaItems.map((agenda) => <SelectItem key={agenda.id} value={agenda.id}>{agenda.topic}</SelectItem>)}</SelectContent></Select></div> : null}
        <div className="flex flex-wrap justify-end gap-2">{editingId ? <Button type="button" size="sm" variant="outline" onClick={clearForm}><X className="me-1.5 h-4 w-4" />{t("common.cancel")}</Button> : null}<Button type="submit" size="sm" disabled={busy}>{editingId ? <Save className="me-1.5 h-4 w-4" /> : <Plus className="me-1.5 h-4 w-4" />}{busy ? t("common.saving") : editingId ? t("meetings.saveAction") : t("meetings.addActionItem")}</Button></div>
      </form> : null}
      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    </CardContent>
  </Card>;
}

function isMeetingActionOverdue(item: MeetingActionItemDto): boolean {
  if (!item.dueDate || item.status === "Completed") return false;
  const datePart = item.dueDate.slice(0, 10);
  const [year, month, day] = datePart.split("-").map(Number);
  if (!year || !month || !day) return false;
  const dueDay = new Date(year, month - 1, day);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return dueDay < today;
}
