import * as React from "react";
import { Link, Navigate, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, CheckCircle2, ExternalLink, FileText, Pencil, Plus, Save, Trash2, UserRound, Users, XCircle } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Label } from "@components/ui/Label";
import { Skeleton } from "@components/ui/Skeleton";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { ApiError } from "@api/client";
import { hasPermission, Permissions } from "@lib/permissions";
import { safeHttpUrl } from "@lib/safe-url";
import { meetingPublicPath } from "@lib/meeting-navigation";
import { useFullMeeting, useMeetingFullMutations, useMeetingMinutes, type MeetingDetailDto } from "@api/meetingsFull";
import { MeetingCreateSheet } from "./MeetingCreateSheet";
import { MeetingsBrowser } from "./MeetingsBrowser";
import { MeetingStatusBadge } from "./MeetingsTable";
import { getMeetingStatusLabel, type MeetingTranslate } from "./meetings-labels";
import { MeetingWorkspaceSections } from "./MeetingWorkspaceSections";
import { MeetingMinutesCollaboration } from "./MeetingMinutesCollaboration";
import { MeetingSharingDialog } from "./MeetingSharingDialog";
import { buildMeetingUpdateBody } from "./meeting-form";
import { RichTextContent, RichTextEditor } from "@components/ui/rich-text-editor";

function Loading({ text }: { text: string }) { return <p className="py-8 text-center text-sm text-muted-foreground">{text}</p>; }
function ErrorMessage({ text }: { text: string }) { return <p role="alert" className="py-8 text-center text-sm text-destructive">{text}</p>; }
function MinutesPageSkeleton() {
  return <div className="space-y-5"><PageHeader title="" description="" actions={<Skeleton className="h-8 w-32" />} /><Card><CardContent className="space-y-5 p-5"><Skeleton className="h-24" /><Skeleton className="h-32" /><div className="grid gap-5 lg:grid-cols-2"><Skeleton className="h-32" /><Skeleton className="h-32" /></div></CardContent></Card></div>;
}
function StateCard({ title, description }: { title: string; description: string }) {
  return <Card><CardContent className="flex min-h-56 flex-col items-center justify-center gap-2 py-12 text-center"><FileText className="h-10 w-10 text-muted-foreground/60" /><h2 className="font-medium">{title}</h2><p className="text-sm text-muted-foreground">{description}</p></CardContent></Card>;
}
function MinutesQueryError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const { t } = useI18n();
  const status = error instanceof ApiError ? error.status : undefined;
  const title = status === 401
    ? t("meetings.minutesAuthenticationRequired")
    : status === 403
      ? t("meetings.permissionDenied")
      : status === 404
        ? t("meetings.minutesMeetingUnavailable")
        : t("meetings.minutesLoadError");
  const description = status === 401
    ? t("meetings.minutesAuthenticationDescription")
    : status === 403
      ? t("meetings.minutesAccessDeniedDescription")
      : status === 404
        ? t("meetings.minutesNotFoundDescription")
        : t("meetings.minutesLoadRetryDescription");
  return <Card><CardContent className="flex min-h-56 flex-col items-center justify-center gap-3 py-12 text-center"><FileText className="h-10 w-10 text-muted-foreground/60" /><h2 className="font-medium">{title}</h2><p role="alert" className="max-w-md text-sm text-muted-foreground">{description}</p><Button type="button" variant="outline" size="sm" onClick={onRetry}>{t("common.retry")}</Button></CardContent></Card>;
}

export function MeetingsPageFull() {
  const { t } = useI18n();
  const { user, status: authStatus } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create) && canMutate && !effectiveReadOnly);
  const canManageMinutes = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage) && canMutate && !effectiveReadOnly);

  return <div className="space-y-6">
    <PageHeader title={t("meetings.title")} description={t("meetings.descriptionAll")} actions={canCreate ? <Button asChild size="sm" className="min-w-max shrink-0"><Link to="/meetings/new" className="flex items-center gap-1.5 whitespace-nowrap"><Plus className="h-4 w-4 shrink-0" />{t("meetings.createMeeting")}</Link></Button> : undefined} />
    <MeetingsBrowser
      canView={canView}
      canManageMinutes={canManageMinutes}
      authLoading={authStatus === "loading"}
      permissionDeniedText={t("meetings.permissionDenied")}
      emptyTitle={t("meetings.emptyTitle")}
      emptyDescription={t("meetings.emptyDescription")}
    />
  </div>;
}

export function MeetingDetailPageFull() {
  const { id } = useParams<{ id: string }>();
  const { t, formatDate } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const isMinutesRoute = location.pathname.endsWith("/minutes");
  const query = useFullMeeting(id);
  const meetingId = query.data?.id;
  const mutations = useMeetingFullMutations(meetingId, id);
  const [editOpen, setEditOpen] = React.useState(false);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const minutesQuery = useMeetingMinutes(meetingId, canView && query.data?.status === "Completed");
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Edit) && canMutate && !effectiveReadOnly);
  const canManageMinutes = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage) && canMutate && !effectiveReadOnly);
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Delete) && canMutate && !effectiveReadOnly);
  if (query.isLoading) return <Loading text={t("common.loading")} />;
  if (query.isError || !query.data) {
    const status = query.error instanceof ApiError ? query.error.status : undefined;
    const message = status === 401
      ? t("meetings.authenticationRequired")
      : status === 403
        ? t("meetings.permissionDenied")
        : status === 404 || !query.error
          ? t("meetings.notFoundTitle")
          : t("meetings.loadError");
    return <ErrorMessage text={message} />;
  }
  if (user && !canView) {
    return isMinutesRoute
      ? <StateCard title={t("meetings.title")} description={t("meetings.permissionDenied")} />
      : <ErrorMessage text={t("meetings.permissionDenied")} />;
  }
  const meeting = query.data;
  const meetingPath = meetingPublicPath(meeting.publicId);
  if (!meetingPath) return <Navigate to="/meetings" replace />;
  const canonicalPath = `${meetingPath}${isMinutesRoute ? "/minutes" : ""}`;
  if (location.pathname !== canonicalPath) return <Navigate to={`${canonicalPath}${location.search}`} replace />;
  if (isMinutesRoute && meeting.status !== "Completed") return <Navigate to={meetingPath} replace />;
  if (isMinutesRoute && minutesQuery.isLoading) return <MinutesPageSkeleton />;
  if (isMinutesRoute && minutesQuery.isError) return <MinutesQueryError error={minutesQuery.error} onRetry={() => void minutesQuery.refetch()} />;
  const scheduled = meeting.status === "Scheduled";
  const inProgress = meeting.status === "InProgress";
  const busy = mutations.update.isPending || mutations.delete.isPending || mutations.start.isPending || mutations.end.isPending;
  const meetingLink = safeHttpUrl(meeting.meetingLink);

  function cancelMeeting() {
    const body = buildMeetingUpdateBody(meeting, "Cancelled");
    if (!body) {
      setActionError(t("meetings.actionError"));
      return;
    }
    setActionError(null);
    mutations.update.mutate({ body }, { onError: () => setActionError(t("meetings.actionError")) });
  }

  async function removeMeeting() {
    if (!await confirm({ description: t("meetings.deleteConfirm"), destructive: true })) return;
    setActionError(null);
    mutations.delete.mutate(meeting.id, {
      onSuccess: () => navigate("/meetings"),
      onError: () => setActionError(t("meetings.actionError")),
    });
  }

  const hasCommentDeepLink = Boolean(searchParams.get("comment") || searchParams.get("reply"));
  const activeCompletedTab = !hasCommentDeepLink && searchParams.get("tab") === "details" ? "details" : "minutes";
  const setCompletedTab = (tab: "minutes" | "details") => setSearchParams((current) => {
    const next = new URLSearchParams(current);
    if (tab === "details") {
      next.set("tab", "details");
      next.delete("comment");
      next.delete("reply");
    } else next.delete("tab");
    return next;
  });
  const completedFollowUp = <MeetingWorkspaceSections meetingId={meeting.id} projectId={meeting.projectId} meetingStatus={meeting.status ?? "Completed"} organizerId={meeting.organizerId} workspaceView="follow-up" />;

  return <div className="space-y-6">
    <PageHeader title={isMinutesRoute ? t("meetings.minutesTitle") : meeting.title} description={isMinutesRoute ? meeting.title : meeting.status === "Completed" ? `${meeting.projectName || t("meetings.noProject")} · ${formatDate(meeting.startTime)}` : t("meetings.details")} actions={isMinutesRoute ? <Link to={meetingPath} className="button-outline"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("meetings.backToMeeting")}</Link> : <div className="flex flex-wrap items-center gap-2">
      <Link to="/meetings" className="button-outline"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("meetings.backToMeetings")}</Link>
      {canEdit && scheduled ? <Button size="sm" variant="outline" onClick={() => setEditOpen(true)} disabled={busy}><Pencil className="me-1.5 h-4 w-4" />{t("meetings.editMeeting")}</Button> : null}
      {canEdit && scheduled ? <><Button size="sm" onClick={() => { setActionError(null); mutations.start.mutate(meeting.rowVersion, { onError: () => setActionError(t("meetings.actionError")) }); }} disabled={busy}>{mutations.start.isPending ? <CheckCircle2 className="me-1.5 h-4 w-4 animate-pulse" /> : <CheckCircle2 className="me-1.5 h-4 w-4" />}{t("meetings.startMeeting")}</Button><Button size="sm" variant="outline" onClick={cancelMeeting} disabled={busy}><XCircle className="me-1.5 h-4 w-4" />{t("meetings.cancelMeeting")}</Button></> : null}
      {canEdit && inProgress ? <Button size="sm" onClick={() => { setActionError(null); mutations.end.mutate(meeting.rowVersion, { onSuccess: () => setSearchParams((current) => { const next = new URLSearchParams(current); next.delete("tab"); return next; }, { replace: true }), onError: () => setActionError(t("meetings.actionError")) }); }} disabled={busy}>{mutations.end.isPending ? <CheckCircle2 className="me-1.5 h-4 w-4 animate-pulse" /> : <CheckCircle2 className="me-1.5 h-4 w-4" />}{t("meetings.endMeeting")}</Button> : null}
      {canDelete ? <Button size="sm" variant="destructive" onClick={removeMeeting} disabled={busy}><Trash2 className="me-1.5 h-4 w-4" />{t("meetings.delete")}</Button> : null}
      <MeetingStatusBadge status={meeting.status} label={getMeetingStatusLabel(meeting.status, t)} />
    </div>}>
      {isMinutesRoute ? <MeetingStatusBadge status={minutesQuery.data?.state ?? "Draft"} label={t(`meetings.minutes.${(minutesQuery.data?.state ?? "Draft").toLowerCase()}`)} /> : null}
    </PageHeader>
    {actionError ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{actionError}</p> : null}
    {isMinutesRoute ? <MeetingMinutesPanel meetingId={meeting.id} canManage={canManageMinutes} minutes={minutesQuery} /> : meeting.status === "Completed" ? <CompletedMeetingTabs
      activeTab={activeCompletedTab}
      onTabChange={setCompletedTab}
      minutesContent={<div className="space-y-6">{minutesQuery.isLoading ? <><MinutesPageSkeleton />{completedFollowUp}</> : minutesQuery.isError ? <><MinutesQueryError error={minutesQuery.error} onRetry={() => void minutesQuery.refetch()} />{completedFollowUp}</> : <><MeetingSharingDialog meeting={meeting} minutes={minutesQuery.data} canManage={canManageMinutes} /><MeetingMinutesPanel meetingId={meeting.id} canManage={canManageMinutes} minutes={minutesQuery} sideContent={completedFollowUp} /></>}</div>}
      detailsContent={<div className="space-y-5"><MeetingDetailsContent meeting={meeting} meetingLink={meetingLink} /><MeetingWorkspaceSections meetingId={meeting.id} projectId={meeting.projectId} meetingStatus={meeting.status} organizerId={meeting.organizerId} workspaceView="details" /></div>}
    /> : <><MeetingDetailsContent meeting={meeting} meetingLink={meetingLink} /><MeetingWorkspaceSections meetingId={meeting.id} projectId={meeting.projectId} meetingStatus={meeting.status ?? "Scheduled"} organizerId={meeting.organizerId} /></>}
    <MeetingCreateSheet open={editOpen} onOpenChange={setEditOpen} meeting={meeting} />
  </div>;
}

function MeetingInfoField({ label, value, wide = false }: { label: string; value: React.ReactNode; wide?: boolean }) {
  return <div className={wide ? "space-y-1 sm:col-span-2" : "space-y-1"}>
    <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</div>
    <div className="break-words text-sm">{value}</div>
  </div>;
}

function MeetingDetailsContent({ meeting, meetingLink }: { meeting: MeetingDetailDto; meetingLink: string | undefined }) {
  const { t, formatDate } = useI18n();
  return <div className="grid gap-5 xl:grid-cols-2">
    <Card><CardHeader><CardTitle>{t("meetings.details")}</CardTitle></CardHeader><CardContent className="grid gap-5 sm:grid-cols-2">
      <MeetingInfoField label={t("meetings.objective")} value={meeting.objective?.trim() || t("meetings.noObjective")} wide />
      <MeetingInfoField label={t("meetings.project")} value={meeting.projectPublicId && meeting.projectName ? <Link className="font-medium text-primary hover:underline" to={`/projects/${encodeURIComponent(meeting.projectPublicId)}`}>{meeting.projectName}</Link> : t("meetings.noProject")} />
      <MeetingInfoField label={t("meetings.start")} value={formatDate(meeting.startTime)} />
      <MeetingInfoField label={t("meetings.end")} value={formatDate(meeting.endTime)} />
      <MeetingInfoField label={t("meetings.duration")} value={meeting.actualStartTime && meeting.actualEndTime ? formatMeetingDuration(meeting.actualStartTime, meeting.actualEndTime, t) : formatMeetingDuration(meeting.startTime, meeting.endTime, t)} />
      {meeting.actualStartTime ? <MeetingInfoField label={t("meetings.actualStart")} value={formatDate(meeting.actualStartTime)} /> : null}
      {meeting.actualEndTime ? <MeetingInfoField label={t("meetings.actualEnd")} value={formatDate(meeting.actualEndTime)} /> : null}
      <MeetingInfoField label={t("meetings.location")} value={meeting.location?.trim() || t("meetings.noLocation")} />
      <MeetingInfoField label={t("meetings.reminder")} value={getMeetingReminderLabel(meeting.reminderOffsetMinutes, t)} />
      <MeetingInfoField label={t("meetings.meetingLink")} value={meetingLink ? <a className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline" href={meetingLink} target="_blank" rel="noreferrer"><ExternalLink className="h-3.5 w-3.5" />{t("meetings.openMeetingLink")}</a> : "—"} />
    </CardContent></Card>
    <div className="space-y-5">
      <Card><CardHeader><CardTitle>{t("meetings.organizer")}</CardTitle></CardHeader><CardContent>{meeting.organizerName ? <div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-4 w-4" /></div><span className="text-sm font-medium">{meeting.organizerName}</span></div> : <p className="text-sm text-muted-foreground">—</p>}</CardContent></Card>
      <Card><CardHeader><CardTitle className="flex items-center gap-2"><Users className="h-4 w-4 text-muted-foreground" />{t("meetings.attendees")}<span className="text-xs font-normal text-muted-foreground">({meeting.attendees.length})</span></CardTitle></CardHeader><CardContent>{meeting.attendees.length ? <div className="space-y-3">{meeting.attendees.map((attendee) => <div key={attendee.id} className="flex min-w-0 items-center gap-3"><div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted"><UserRound className="h-3.5 w-3.5 text-muted-foreground" /></div><div className="min-w-0"><div className="flex flex-wrap items-center gap-1.5"><p className="truncate text-sm font-medium">{attendee.displayName?.trim() || attendee.email}</p><span className="rounded-full border px-1.5 py-0.5 text-[9px] font-medium text-muted-foreground">{attendee.isInternalUser ? t("meetings.internalUser") : t("meetings.externalAttendee")}</span></div><p className="truncate text-xs text-muted-foreground">{attendee.email}</p></div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("meetings.noUsers")}</p>}</CardContent></Card>
    </div>
  </div>;
}

type CompletedMeetingTab = "minutes" | "details";

function CompletedMeetingTabs({ activeTab, onTabChange, minutesContent, detailsContent }: { activeTab: CompletedMeetingTab; onTabChange: (tab: CompletedMeetingTab) => void; minutesContent: React.ReactNode; detailsContent: React.ReactNode }) {
  const { t, locale } = useI18n();
  const minutesTabRef = React.useRef<HTMLButtonElement>(null);
  const detailsTabRef = React.useRef<HTMLButtonElement>(null);
  const isRtl = locale === "ar";

  function handleTabKeyDown(event: React.KeyboardEvent<HTMLButtonElement>, current: CompletedMeetingTab) {
    const tabs: CompletedMeetingTab[] = ["minutes", "details"];
    const index = tabs.indexOf(current);
    let next: CompletedMeetingTab | null = null;
    if (event.key === "Home") next = tabs[0];
    else if (event.key === "End") next = tabs[tabs.length - 1];
    else if (event.key === "ArrowRight") next = tabs[(index + (isRtl ? -1 : 1) + tabs.length) % tabs.length];
    else if (event.key === "ArrowLeft") next = tabs[(index + (isRtl ? 1 : -1) + tabs.length) % tabs.length];
    if (!next) return;
    event.preventDefault();
    onTabChange(next);
    (next === "minutes" ? minutesTabRef : detailsTabRef).current?.focus();
  }

  return <div className="space-y-5">
    <div role="tablist" aria-label={t("meetings.completedWorkspaceTabs")} aria-orientation="horizontal" dir={isRtl ? "rtl" : "ltr"} className="flex w-full border-b">
      <button ref={minutesTabRef} type="button" role="tab" id="meeting-completed-minutes-tab" aria-controls="meeting-completed-minutes-panel" aria-selected={activeTab === "minutes"} tabIndex={activeTab === "minutes" ? 0 : -1} data-state={activeTab === "minutes" ? "active" : "inactive"} className={`relative min-w-0 flex-1 border-b-2 px-3 py-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${activeTab === "minutes" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`} onClick={() => onTabChange("minutes")} onKeyDown={(event) => handleTabKeyDown(event, "minutes")}>{t("meetings.completedMinutesFollowUpTab")}</button>
      <button ref={detailsTabRef} type="button" role="tab" id="meeting-completed-details-tab" aria-controls="meeting-completed-details-panel" aria-selected={activeTab === "details"} tabIndex={activeTab === "details" ? 0 : -1} data-state={activeTab === "details" ? "active" : "inactive"} className={`relative min-w-0 flex-1 border-b-2 px-3 py-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${activeTab === "details" ? "border-primary text-primary" : "border-transparent text-muted-foreground hover:text-foreground"}`} onClick={() => onTabChange("details")} onKeyDown={(event) => handleTabKeyDown(event, "details")}>{t("meetings.completedDetailsTab")}</button>
    </div>
    <div role="tabpanel" id="meeting-completed-minutes-panel" aria-labelledby="meeting-completed-minutes-tab" tabIndex={0} hidden={activeTab !== "minutes"} className="min-w-0 space-y-6">{minutesContent}</div>
    <div role="tabpanel" id="meeting-completed-details-panel" aria-labelledby="meeting-completed-details-tab" tabIndex={0} hidden={activeTab !== "details"} className="min-w-0 space-y-6">{detailsContent}</div>
  </div>;
}

function getMeetingReminderLabel(minutes: number | null | undefined, t: MeetingTranslate): string {
  switch (minutes) {
    case null:
    case undefined: return t("meetings.noReminder");
    case 15: return t("meetings.fifteenMinutesBefore");
    case 30: return t("meetings.thirtyMinutesBefore");
    case 60: return t("meetings.oneHourBefore");
    case 1440: return t("meetings.oneDayBefore");
    default: return t("meetings.noReminder");
  }
}

function formatMeetingDuration(start: string, end: string, t: MeetingTranslate): string {
  const startValue = new Date(start).getTime();
  const endValue = new Date(end).getTime();
  if (!Number.isFinite(startValue) || !Number.isFinite(endValue) || endValue <= startValue) return "—";
  const durationMinutes = Math.max(1, Math.round((endValue - startValue) / 60_000));
  const hours = Math.floor(durationMinutes / 60);
  const minutes = durationMinutes % 60;
  if (hours && minutes) return `${t("meetings.hours", { count: hours })} ${t("meetings.minutesShort", { count: minutes })}`;
  if (hours) return t("meetings.hours", { count: hours });
  return t("meetings.minutesShort", { count: minutes });
}

function MeetingMinutesPanel({ meetingId, canManage, minutes, sideContent }: { meetingId: string; canManage: boolean; minutes: ReturnType<typeof useMeetingMinutes>; sideContent?: React.ReactNode }) {
  const { t, locale } = useI18n();
  const mutations = useMeetingFullMutations(meetingId);
  const [draft, setDraft] = React.useState({ summary: "", discussionNotes: "", decisions: "", outcomes: "" });
  const [notice, setNotice] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const isFinal = minutes.data?.state === "Final";
  const isSaving = mutations.createMinutes.isPending || mutations.updateMinutes.isPending || mutations.finalizeMinutes.isPending;
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
    if (!minutes.isSuccess) return;
    setDraft(minutes.data ? {
      summary: minutes.data.summary ?? "",
      discussionNotes: minutes.data.discussionNotes ?? "",
      decisions: minutes.data.decisions ?? "",
      outcomes: minutes.data.outcomes ?? "",
    } : { summary: "", discussionNotes: "", decisions: "", outcomes: "" });
  }, [minutes.data, minutes.isSuccess]);

  function updateDraft(field: keyof typeof draft, value: string) {
    setDraft((current) => ({ ...current, [field]: value }));
    setNotice(null);
    setError(null);
  }

  async function persistDraft() {
    const body = Object.fromEntries(Object.entries(draft).map(([key, value]) => [key, value.trim() || undefined])) as Record<string, string | undefined>;
    if (minutes.data) return mutations.updateMinutes.mutateAsync({ ...body, rowVersion: minutes.data.rowVersion });
    return mutations.createMinutes.mutateAsync(body);
  }

  async function save() {
    setNotice(null);
    setError(null);
    try {
      await persistDraft();
      await minutes.refetch();
      setNotice(t("meetings.minutesSaved"));
    } catch {
      setError(t("meetings.minutesSaveError"));
    }
  }

  async function finalize() {
    setNotice(null);
    setError(null);
    try {
      const saved = await persistDraft();
      await mutations.finalizeMinutes.mutateAsync(saved.rowVersion);
      await minutes.refetch();
      setNotice(t("meetings.minutesFinalized"));
    } catch {
      setError(t("meetings.minutesFinalizeError"));
    }
  }

  if (!minutes.data && !canManage) return <Card><CardContent className="flex min-h-56 flex-col items-center justify-center gap-3 py-12 text-center"><FileText className="h-10 w-10 text-muted-foreground/60" /><h2 className="font-medium">{t("meetings.noMinutesTitle")}</h2><p className="max-w-md text-sm text-muted-foreground">{t("meetings.noMinutesDescription")}</p></CardContent></Card>;

  const mainContent = <Card><CardHeader className="border-b bg-muted/10"><CardTitle className="text-base">{t("meetings.minutesTitle")}</CardTitle><CardDescription>{isFinal ? t("meetings.minutesReadOnly") : t("meetings.minutesEditorDescription")}</CardDescription></CardHeader><CardContent className="space-y-5 p-5">
    {(["summary", "discussionNotes", "decisions", "outcomes"] as const).map((field) => <MinutesField key={field} id={`minutes-${field}`} label={t(`meetings.${field}`)} value={draft[field]} onChange={(value) => updateDraft(field, value)} disabled={!canManage || isFinal} placeholder={t(`meetings.${field}Placeholder`)} copy={richTextCopy} />)}
    {notice ? <p role="status" className="rounded-md border border-emerald-500/25 bg-emerald-500/5 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-300">{notice}</p> : null}
    {error ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
    <div className="flex flex-wrap justify-end gap-2 border-t pt-4">{canManage && !isFinal ? <><Button type="button" variant="outline" onClick={() => void save()} disabled={isSaving}><Save className="me-1.5 h-4 w-4" />{t("meetings.saveMinutes")}</Button><Button type="button" onClick={() => void finalize()} disabled={isSaving}><CheckCircle2 className="me-1.5 h-4 w-4" />{t("meetings.finalizeMinutes")}</Button></> : null}</div>
  </CardContent></Card>;

  return <MeetingMinutesCollaboration meetingId={meetingId} canManage={canManage} locale={locale} hasMinutes={Boolean(minutes.data)} mainContent={mainContent} sideContent={sideContent} />;
}

function MinutesField({ id, label, value, onChange, disabled, placeholder, copy }: { id: string; label: string; value: string; onChange: (_value: string) => void; disabled: boolean; placeholder: string; copy: Record<string, string> }) {
  return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{disabled ? <div className="min-h-24 rounded-lg border border-input bg-muted/20 px-3 py-3"><RichTextContent value={value} /></div> : <RichTextEditor id={id} value={value} onChange={onChange} placeholder={placeholder} ariaLabel={label} t={copy} minHeight={id.endsWith("summary") ? "min-h-32" : "min-h-24"} />}</div>;
}
