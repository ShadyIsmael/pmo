import * as React from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, CalendarCheck, CheckCircle2, ExternalLink, Eye, FileText, Filter, Pencil, Plus, Search, Save, Trash2, UserRound, Users, X, XCircle } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Badge } from "@components/ui/Badge";
import { Pagination } from "@components/ui/Pagination";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { ApiError } from "@api/client";
import { useProjects } from "@features/projects/api/projects";
import { hasPermission, Permissions } from "@lib/permissions";
import { getStatusTextClass } from "@lib/status-presentation";
import { useFullMeeting, useFullMeetings, useMeetingFullMutations, useMeetingMinutes, type MeetingListDto } from "@api/meetingsFull";
import { MeetingCreateSheet } from "./MeetingCreateSheet";
import { MeetingMinutesCollaboration } from "./MeetingMinutesCollaboration";
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

export function MeetingsPageFull() {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canQuery = Boolean(user && canView);
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create) && canMutate && !effectiveReadOnly);
  const canManageMinutes = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage) && canMutate && !effectiveReadOnly);
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState<MeetingStatus | "all">("all");
  const [projectId, setProjectId] = React.useState("");
  const [dateFrom, setDateFrom] = React.useState("");
  const [dateTo, setDateTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [formOpen, setFormOpen] = React.useState(false);
  const deferredSearch = React.useDeferredValue(search);
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, canQuery);
  const filters = React.useMemo(() => ({
    page,
    pageSize: MEETINGS_PAGE_SIZE,
    search: deferredSearch.trim() || undefined,
    status: status === "all" ? undefined : status,
    projectId: projectId || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo ? `${dateTo}T23:59:59` : undefined,
  }), [dateFrom, dateTo, deferredSearch, page, projectId, status]);
  const query = useFullMeetings(filters, canQuery);
  const projects = projectsQuery.data?.items ?? [];
  const selectedProject = projects.find((project) => project.id === projectId);
  const meetings = query.data?.items ?? [];
  const totalPages = query.data?.totalPages ?? Math.ceil((query.data?.totalCount ?? 0) / MEETINGS_PAGE_SIZE);

  if (user && !canView) return <ErrorMessage text={t("meetings.permissionDenied")} />;

  return <div className="space-y-6">
    <PageHeader title={t("meetings.title")} description={t("meetings.description", { count: query.data?.totalCount ?? 0 })} actions={canCreate ? <Button size="sm" onClick={() => setFormOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("meetings.createMeeting")}</Button> : undefined} />
    <Card><CardContent className="space-y-3 p-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
        <label className="w-full space-y-1.5 lg:w-60"><span className="text-xs font-medium text-muted-foreground">{t("common.project")}</span><SearchableSelect options={projects.map((project) => ({ value: project.id, label: `${project.name} · ${project.code}`, keywords: [project.code] }))} value={projectId || undefined} onValueChange={(value) => { setPage(1); setProjectId(value); }} placeholder={t("meetings.allProjects")} searchPlaceholder={t("meetings.searchProjects")} emptyText={t("meetings.noProjects")} loading={projectsQuery.isLoading} /></label>
        <div className="w-full space-y-1.5 lg:w-48"><Label htmlFor="meeting-status-filter">{t("common.status")}</Label><Select value={status} onValueChange={(value) => { if (isMeetingStatus(value)) { setPage(1); setStatus(value); } }}><SelectTrigger id="meeting-status-filter"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("common.all")}</SelectItem>{MEETING_STATUSES.map((value) => <SelectItem key={value} value={value}>{getMeetingStatusLabel(value, t)}</SelectItem>)}</SelectContent></Select></div>
        <div className="min-w-0 flex-1 space-y-1.5"><Label htmlFor="meeting-search">{t("common.search")}</Label><div className="relative"><Search className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input id="meeting-search" value={search} onChange={(event) => { setPage(1); setSearch(event.target.value); }} placeholder={t("meetings.searchPlaceholder")} className="ps-9" /></div></div>
        <div className="grid w-full grid-cols-2 gap-2 lg:w-64"><div className="space-y-1.5"><Label htmlFor="meeting-date-from">{t("meetings.from")}</Label><Input id="meeting-date-from" type="date" value={dateFrom} onChange={(event) => { setPage(1); setDateFrom(event.target.value); }} /></div><div className="space-y-1.5"><Label htmlFor="meeting-date-to">{t("meetings.to")}</Label><Input id="meeting-date-to" type="date" value={dateTo} onChange={(event) => { setPage(1); setDateTo(event.target.value); }} /></div></div>
        <Button type="button" variant="ghost" className="self-end" onClick={() => { setPage(1); setSearch(""); setStatus("all"); setProjectId(""); setDateFrom(""); setDateTo(""); }}><Filter className="me-1.5 h-4 w-4" />{t("meetings.clearFilters")}</Button>
      </div>
      {selectedProject ? <div className="flex flex-wrap items-center gap-2 border-t pt-3 text-sm"><span className="text-muted-foreground">{t("meetings.project")}:</span><span className="font-medium">{selectedProject.name}</span><span className="text-xs text-muted-foreground">{selectedProject.code}</span><Button type="button" variant="ghost" size="sm" className="ms-auto h-7 gap-1 px-2 text-muted-foreground" onClick={() => { setPage(1); setProjectId(""); }}><X className="h-3.5 w-3.5" />{t("meetings.clearProject")}</Button></div> : null}
    </CardContent></Card>
    {query.isLoading ? <div className="space-y-2">{[1, 2, 3, 4].map((row) => <Skeleton key={row} className="h-16 w-full" />)}</div> : query.isError ? query.error instanceof ApiError && query.error.status === 403 ? <ErrorMessage text={t("meetings.permissionDenied")} /> : <div className="flex flex-wrap items-center justify-center gap-3 rounded-lg border border-destructive/25 bg-destructive/5 p-6 text-sm text-destructive"><span>{t("meetings.loadError")}</span><Button type="button" variant="outline" size="sm" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div> : meetings.length === 0 ? <Card><CardContent className="p-8 text-center"><CalendarCheck className="mx-auto mb-3 h-8 w-8 text-muted-foreground/60" /><h2 className="font-medium">{t("meetings.emptyTitle")}</h2><p className="mt-1 text-sm text-muted-foreground">{t("meetings.emptyDescription")}</p></CardContent></Card> : <MeetingsTable meetings={meetings} showProject={!projectId} canManageMinutes={canManageMinutes} formatDate={formatDate} />}
    {!query.isLoading && !query.isError && meetings.length > 0 ? <Pagination page={page} totalPages={totalPages} total={query.data?.totalCount ?? 0} pageSize={MEETINGS_PAGE_SIZE} onChange={setPage} /> : null}
    <MeetingCreateSheet open={formOpen} onOpenChange={(open) => { setFormOpen(open); }} fixedProjectId={selectedProject?.id} initialProject={selectedProject} projectLocked={Boolean(selectedProject)} onCreated={() => setPage(1)} />
  </div>;
}

const MEETINGS_PAGE_SIZE = 20;
const MEETING_STATUSES = ["Scheduled", "Completed", "Cancelled"] as const;
type MeetingStatus = (typeof MEETING_STATUSES)[number];

function isMeetingStatus(value: string): value is MeetingStatus | "all" {
  return value === "all" || (MEETING_STATUSES as readonly string[]).includes(value);
}

function MeetingStatusBadge({ status, label }: { status?: string | null; label: string }) {
  return status?.trim()
    ? <Badge status={status}>{label}</Badge>
    : <span className="text-xs font-medium text-muted-foreground">{label}</span>;
}

function MeetingsTable({ meetings, showProject, canManageMinutes, formatDate }: { meetings: readonly MeetingListDto[]; showProject: boolean; canManageMinutes: boolean; formatDate: (date: Date | string, options?: Intl.DateTimeFormatOptions) => string }) {
  const { t } = useI18n();
  return <Card><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("meetings.columns.meeting")}</TableHead>{showProject ? <TableHead>{t("common.project")}</TableHead> : null}<TableHead>{t("meetings.organizer")}</TableHead><TableHead>{t("meetings.dateTime")}</TableHead><TableHead>{t("meetings.attendees")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("meetings.minutesTitle")}</TableHead><TableHead className="text-end">{t("common.actions")}</TableHead></TableRow></TableHeader><TableBody>{meetings.map((meeting) => {
    const hasMinutes = Boolean(meeting.minutesState);
    const minutesActionLabel = hasMinutes || !canManageMinutes ? t("meetings.viewMinutes") : t("meetings.enterMinutes");
    return <TableRow key={meeting.id} className="group"><TableCell className="min-w-[16rem]"><Link to={`/meetings/${encodeURIComponent(meeting.id)}`} className="block min-w-0"><p className="truncate font-semibold underline-offset-4 group-hover:text-primary group-hover:underline">{meeting.title}</p><p className="mt-1 truncate text-[11px] text-muted-foreground">{hasMinutes ? t(`meetings.minutes.${meeting.minutesState!.toLowerCase()}`) : t("meetings.minutes.notAvailable")}</p></Link></TableCell>{showProject ? <TableCell className="max-w-[14rem]"><span className="block truncate text-muted-foreground">{meeting.projectName ?? t("meetings.notAvailable")}</span></TableCell> : null}<TableCell className="max-w-[12rem]"><span className="block truncate text-muted-foreground">{meeting.organizerName ?? t("meetings.notAvailable")}</span></TableCell><TableCell className="whitespace-nowrap"><span className="flex items-center gap-1.5 text-muted-foreground"><CalendarCheck className="h-3.5 w-3.5 shrink-0" />{formatDate(meeting.startTime, { timeStyle: "short" })}</span></TableCell><TableCell><span className="inline-flex items-center gap-1.5 text-muted-foreground"><Users className="h-3.5 w-3.5" />{meeting.attendeeCount}</span></TableCell><TableCell><MeetingStatusBadge status={meeting.status} label={getMeetingStatusLabel(meeting.status, t)} /></TableCell><TableCell><MeetingStatusBadge status={meeting.minutesState} label={hasMinutes ? t(`meetings.minutes.${meeting.minutesState!.toLowerCase()}`) : t("meetings.minutes.notAvailable")} /></TableCell><TableCell className="text-end"><div className="nexus-table-actions flex justify-end gap-1"><Button asChild size="icon" variant="ghost" aria-label={t("meetings.viewMeeting")} title={t("meetings.viewMeeting")}><Link to={`/meetings/${encodeURIComponent(meeting.id)}`}><Eye className="h-4 w-4" /></Link></Button><Button asChild size="sm" variant={canManageMinutes && !hasMinutes ? "default" : "outline"} className="gap-1.5"><Link to={`/meetings/${encodeURIComponent(meeting.id)}/minutes`}>{canManageMinutes && !hasMinutes ? <Pencil className="h-3.5 w-3.5" /> : <FileText className="h-3.5 w-3.5" />}<span className="hidden xl:inline">{minutesActionLabel}</span><span className="sr-only xl:hidden">{minutesActionLabel}</span></Link></Button></div></TableCell></TableRow>;
  })}</TableBody></Table></CardContent></Card>;
}

export function MeetingDetailPageFull() {
  const { id } = useParams<{ id: string }>();
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const location = useLocation();
  const navigate = useNavigate();
  const isMinutesRoute = location.pathname.endsWith("/minutes");
  const query = useFullMeeting(id);
  const mutations = useMeetingFullMutations(id);
  const [editOpen, setEditOpen] = React.useState(false);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const minutesQuery = useMeetingMinutes(id, isMinutesRoute && canView);
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Edit) && canMutate && !effectiveReadOnly);
  const canManageMinutes = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage) && canMutate && !effectiveReadOnly);
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Delete) && canMutate && !effectiveReadOnly);
  if (query.isLoading) return isMinutesRoute ? <MinutesPageSkeleton /> : <Loading text={t("common.loading")} />;
  if (query.isError || !query.data) {
    return isMinutesRoute
      ? <StateCard title={t("meetings.loadError")} description={t("meetings.actionError")} />
      : <ErrorMessage text={t("meetings.notFoundTitle")} />;
  }
  if (user && !canView) {
    return isMinutesRoute
      ? <StateCard title={t("meetings.title")} description={t("meetings.permissionDenied")} />
      : <ErrorMessage text={t("meetings.permissionDenied")} />;
  }
  if (isMinutesRoute && minutesQuery.isLoading) return <MinutesPageSkeleton />;
  if (isMinutesRoute && minutesQuery.isError) return <StateCard title={t("meetings.minutesLoadError")} description={t("meetings.actionError")} />;
  const meeting = query.data;
  const scheduled = meeting.status === "Scheduled";
  const busy = mutations.update.isPending || mutations.delete.isPending;

  function changeStatus(status: "Completed" | "Cancelled") {
    const body = buildMeetingUpdateBody(meeting, status);
    if (!body) {
      setActionError(t("meetings.actionError"));
      return;
    }
    setActionError(null);
    mutations.update.mutate({ body }, { onError: () => setActionError(t("meetings.actionError")) });
  }

  function removeMeeting() {
    if (!window.confirm(t("meetings.deleteConfirm"))) return;
    setActionError(null);
    mutations.delete.mutate(meeting.id, {
      onSuccess: () => navigate("/meetings"),
      onError: () => setActionError(t("meetings.actionError")),
    });
  }

  return <div className="space-y-6">
    <PageHeader title={isMinutesRoute ? t("meetings.minutesTitle") : meeting.title} description={isMinutesRoute ? meeting.title : t("meetings.details")} actions={isMinutesRoute ? <Link to={`/meetings/${encodeURIComponent(meeting.id)}`} className="button-outline"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("meetings.backToMeeting")}</Link> : <div className="flex flex-wrap items-center gap-2">
      <Link to="/meetings" className="button-outline"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("meetings.backToMeetings")}</Link>
      {canEdit ? <Button size="sm" variant="outline" onClick={() => setEditOpen(true)} disabled={busy}><Pencil className="me-1.5 h-4 w-4" />{t("meetings.editMeeting")}</Button> : null}
      {canEdit && scheduled ? <><Button size="sm" onClick={() => changeStatus("Completed")} disabled={busy}>{mutations.update.isPending ? <CheckCircle2 className="me-1.5 h-4 w-4 animate-pulse" /> : <CheckCircle2 className="me-1.5 h-4 w-4" />}{t("meetings.markCompleted")}</Button><Button size="sm" variant="outline" onClick={() => changeStatus("Cancelled")} disabled={busy}><XCircle className="me-1.5 h-4 w-4" />{t("meetings.cancelMeeting")}</Button></> : null}
      {canDelete ? <Button size="sm" variant="destructive" onClick={removeMeeting} disabled={busy}><Trash2 className="me-1.5 h-4 w-4" />{t("meetings.delete")}</Button> : null}
      <MeetingStatusBadge status={meeting.status} label={getMeetingStatusLabel(meeting.status, t)} />
    </div>}>
      {isMinutesRoute ? <MeetingStatusBadge status={minutesQuery.data?.state ?? "Draft"} label={t(`meetings.minutes.${(minutesQuery.data?.state ?? "Draft").toLowerCase()}`)} /> : null}
    </PageHeader>
    {actionError ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{actionError}</p> : null}
    {isMinutesRoute ? <MeetingMinutesPanel meetingId={meeting.id} canManage={canManageMinutes} minutes={minutesQuery} /> : <>
    <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(280px,360px)]">
      <Card><CardHeader><CardTitle>{t("meetings.details")}</CardTitle></CardHeader><CardContent className="grid gap-5 sm:grid-cols-2">
        <MeetingInfoField label={t("meetings.objective")} value={meeting.objective?.trim() || t("meetings.noObjective")} wide />
        <MeetingInfoField label={t("meetings.project")} value={meeting.projectId && meeting.projectName ? <Link className="font-medium text-primary hover:underline" to={`/projects/${encodeURIComponent(meeting.projectId)}`}>{meeting.projectName}</Link> : t("meetings.noProject")} />
        <MeetingInfoField label={t("meetings.start")} value={formatDate(meeting.startTime)} />
        <MeetingInfoField label={t("meetings.end")} value={formatDate(meeting.endTime)} />
        <MeetingInfoField label={t("meetings.duration")} value={formatMeetingDuration(meeting.startTime, meeting.endTime, t)} />
        <MeetingInfoField label={t("meetings.location")} value={meeting.location?.trim() || t("meetings.noLocation")} />
        <MeetingInfoField label={t("meetings.reminder")} value={getMeetingReminderLabel(meeting.reminderOffsetMinutes, t)} />
        <MeetingInfoField label={t("meetings.meetingLink")} value={meeting.meetingLink ? <a className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline" href={meeting.meetingLink} target="_blank" rel="noreferrer"><ExternalLink className="h-3.5 w-3.5" />{t("meetings.openMeetingLink")}</a> : "—"} />
      </CardContent></Card>
      <div className="space-y-5">
        <Card><CardHeader><CardTitle>{t("meetings.organizer")}</CardTitle></CardHeader><CardContent>{meeting.organizerName ? <div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-4 w-4" /></div><span className="text-sm font-medium">{meeting.organizerName}</span></div> : <p className="text-sm text-muted-foreground">—</p>}</CardContent></Card>
        <Card><CardHeader><CardTitle className="flex items-center gap-2"><Users className="h-4 w-4 text-muted-foreground" />{t("meetings.attendees")}<span className="text-xs font-normal text-muted-foreground">({meeting.attendees.length})</span></CardTitle></CardHeader><CardContent>{meeting.attendees.length ? <div className="space-y-3">{meeting.attendees.map((attendee) => <div key={attendee.id} className="flex min-w-0 items-center gap-3"><div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted"><UserRound className="h-3.5 w-3.5 text-muted-foreground" /></div><div className="min-w-0"><div className="flex flex-wrap items-center gap-1.5"><p className="truncate text-sm font-medium">{attendee.displayName?.trim() || attendee.email}</p><span className="rounded-full border px-1.5 py-0.5 text-[9px] font-medium text-muted-foreground">{attendee.isInternalUser ? t("meetings.internalUser") : t("meetings.externalAttendee")}</span></div><p className="truncate text-xs text-muted-foreground">{attendee.email}</p></div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("meetings.noUsers")}</p>}</CardContent></Card>
      </div>
    </div>
    <Card><CardHeader><CardTitle className="flex items-center gap-2"><CalendarCheck className="h-4 w-4 text-muted-foreground" />{t("meetings.minutesTitle")}</CardTitle></CardHeader><CardContent className="flex flex-wrap items-center justify-between gap-3"><div><p className={`text-sm font-medium ${meeting.minutesState ? getStatusTextClass(meeting.minutesState) : "text-muted-foreground"}`}>{meeting.minutesState ? t(`meetings.minutes.${meeting.minutesState.toLowerCase()}`) : t("meetings.minutes.notAvailable")}</p><p className="mt-1 text-xs text-muted-foreground">{scheduled ? t("meetings.minutesEditorHint") : t("meetings.minutesDescription")}</p></div><Button asChild size="sm" variant={canManageMinutes && !meeting.minutesState ? "default" : "outline"}><Link to={`/meetings/${encodeURIComponent(meeting.id)}/minutes`}>{canManageMinutes && !meeting.minutesState ? <Pencil className="me-1.5 h-4 w-4" /> : <FileText className="me-1.5 h-4 w-4" />}{canManageMinutes && !meeting.minutesState ? t("meetings.enterMinutes") : t("meetings.viewMinutes")}</Link></Button></CardContent></Card>
    </>}
    <MeetingCreateSheet open={editOpen} onOpenChange={setEditOpen} meeting={meeting} />
  </div>;
}

type MeetingTranslate = (key: string, params?: Record<string, string | number>) => string;

function MeetingInfoField({ label, value, wide = false }: { label: string; value: React.ReactNode; wide?: boolean }) {
  return <div className={wide ? "space-y-1 sm:col-span-2" : "space-y-1"}>
    <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</div>
    <div className="break-words text-sm">{value}</div>
  </div>;
}

function getMeetingStatusLabel(status: string | null | undefined, t: MeetingTranslate): string {
  switch (status?.toLowerCase()) {
    case "scheduled": return t("meetings.status.scheduled");
    case "completed": return t("meetings.status.completed");
    case "cancelled":
    case "canceled": return t("meetings.status.cancelled");
    default: return t("meetings.status.unknown");
  }
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

function MeetingMinutesPanel({ meetingId, canManage, minutes }: { meetingId: string; canManage: boolean; minutes: ReturnType<typeof useMeetingMinutes> }) {
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

  const mainContent = <Card><CardHeader><CardTitle>{t("meetings.minutesTitle")}</CardTitle><CardDescription>{isFinal ? t("meetings.minutesReadOnly") : t("meetings.minutesEditorDescription")}</CardDescription></CardHeader><CardContent className="space-y-5">
    {(["summary", "discussionNotes", "decisions", "outcomes"] as const).map((field) => <MinutesField key={field} id={`minutes-${field}`} label={t(`meetings.${field}`)} value={draft[field]} onChange={(value) => updateDraft(field, value)} disabled={!canManage || isFinal} placeholder={t(`meetings.${field}Placeholder`)} copy={richTextCopy} />)}
    {notice ? <p role="status" className="rounded-md border border-emerald-500/25 bg-emerald-500/5 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-300">{notice}</p> : null}
    {error ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
    <div className="flex flex-wrap justify-end gap-2 border-t pt-4">{canManage && !isFinal ? <><Button type="button" variant="outline" onClick={() => void save()} disabled={isSaving}><Save className="me-1.5 h-4 w-4" />{t("meetings.saveMinutes")}</Button><Button type="button" onClick={() => void finalize()} disabled={isSaving}><CheckCircle2 className="me-1.5 h-4 w-4" />{t("meetings.finalizeMinutes")}</Button></> : null}</div>
  </CardContent></Card>;

  return <MeetingMinutesCollaboration meetingId={meetingId} canManage={canManage} locale={locale} mainContent={mainContent} />;
}

function MinutesField({ id, label, value, onChange, disabled, placeholder, copy }: { id: string; label: string; value: string; onChange: (_value: string) => void; disabled: boolean; placeholder: string; copy: Record<string, string> }) {
  return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{disabled ? <div className="min-h-28 rounded-lg border border-input bg-muted/20 px-3 py-3"><RichTextContent value={value} /></div> : <RichTextEditor id={id} value={value} onChange={onChange} placeholder={placeholder} ariaLabel={label} t={copy} minHeight={id.endsWith("summary") ? "min-h-40" : "min-h-28"} />}</div>;
}
