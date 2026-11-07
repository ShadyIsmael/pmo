import * as React from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { ArrowDown, ArrowLeft, ArrowUp, Info, Mail, Pencil, Plus, Trash2, UserRound, X } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useMeetingFullMutations, useMeetingRecurrencePreview, type MeetingRecurrenceRule } from "@api/meetingsFull";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Textarea } from "@components/ui/Textarea";
import { useMeetingWorkflowUserLookup, type WorkflowUserLookupDto } from "@features/projects/lib/project-lookups";
import { useMeetingProjectOptions } from "@api/meetingsFull";
import { hasPermission, Permissions } from "@lib/permissions";
import { useI18n } from "@i18n/messages";
import { createMeetingDraft, isValidMeetingEmail, toMeetingInput, validateMeetingDraft, type MeetingAttendeeDraft } from "./meeting-form";
import { meetingPublicPath } from "@lib/meeting-navigation";

interface AgendaDraft { key: string; topic: string; description: string; durationMinutes: string }

function addMinutesToLocalDateTime(value: string, minutes: number): string {
  if (!value) return "";
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return "";
  date.setTime(date.getTime() + minutes * 60_000);
  const pad = (part: number) => String(part).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function calculateDurationMinutes(startTime: string, endTime: string): number | null {
  const start = startTime ? new Date(startTime).getTime() : Number.NaN;
  const end = endTime ? new Date(endTime).getTime() : Number.NaN;
  return Number.isFinite(start) && Number.isFinite(end) && end > start
    ? Math.max(1, Math.round((end - start) / 60_000))
    : null;
}

export function MeetingCreatePage() {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create) && canMutate && !effectiveReadOnly);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const projectPublicId = searchParams.get("projectId")?.trim() ?? "";
  const projectsQuery = useMeetingProjectOptions({}, Boolean(user && canCreate));
  const usersQuery = useMeetingWorkflowUserLookup(Boolean(user && canCreate));
  const mutations = useMeetingFullMutations();
  const [draft, setDraft] = React.useState(() => createMeetingDraft());
  const [projectId, setProjectId] = React.useState("");
  const [attendeeSearch, setAttendeeSearch] = React.useState("");
  const [agendaDrafts, setAgendaDrafts] = React.useState<AgendaDraft[]>([]);
  const [agendaTopic, setAgendaTopic] = React.useState("");
  const [agendaDescription, setAgendaDescription] = React.useState("");
  const [agendaDuration, setAgendaDuration] = React.useState("");
  const [editingAgendaKey, setEditingAgendaKey] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [agendaError, setAgendaError] = React.useState<string | null>(null);
  const [recurring, setRecurring] = React.useState(false);
  const [recurrenceFrequency, setRecurrenceFrequency] = React.useState<MeetingRecurrenceRule["frequency"]>("Weekly");
  const [recurrenceStartDate, setRecurrenceStartDate] = React.useState("");
  const [recurrenceEndDate, setRecurrenceEndDate] = React.useState("");
  const [recurrenceWeekdays, setRecurrenceWeekdays] = React.useState<number[]>([]);
  const [recurrenceTimeZone, setRecurrenceTimeZone] = React.useState(() => Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC");
  const [debouncedRecurrenceRule, setDebouncedRecurrenceRule] = React.useState<MeetingRecurrenceRule | null>(null);
  const recurrenceRequestRef = React.useRef<{ fingerprint: string; requestId: string } | null>(null);
  const projects = React.useMemo(() => projectsQuery.data ?? [], [projectsQuery.data]);
  const users = React.useMemo(() => usersQuery.data ?? [], [usersQuery.data]);
  const scopedProject = projectPublicId ? projects.find((project) => project.publicId === projectPublicId) : undefined;

  React.useEffect(() => {
    if (scopedProject) setProjectId(scopedProject.id);
    else if (!projectPublicId) setProjectId("");
  }, [projectPublicId, scopedProject]);

  const projectOptions = React.useMemo(() => {
    const options = projects.map((project) => ({ value: project.id, label: project.code ? `${project.name} · ${project.code}` : project.name, keywords: project.code ? [project.code] : [] }));
    return options;
  }, [projects]);
  const userOptions = React.useMemo(() => users.map((person) => ({ value: person.id, label: person.fullName, keywords: [] })), [users]);
  const attendeeQuery = attendeeSearch.trim().toLocaleLowerCase();
  const visibleUsers = users.filter((candidate) => !attendeeQuery || candidate.fullName.toLocaleLowerCase().includes(attendeeQuery));
  const emailCandidate = attendeeSearch.trim();
  const canAddExternal = isValidMeetingEmail(emailCandidate)
    && !draft.attendees.some((attendee) => !attendee.userId && attendee.email.trim().toLocaleLowerCase() === emailCandidate.toLocaleLowerCase());
  const projectLookupUnavailable = Boolean(projectPublicId && !canCreate);
  const projectLookupFailed = Boolean(projectPublicId && (projectsQuery.isError || (projectsQuery.isSuccess && !scopedProject)));
  const projectLookupPending = Boolean(projectPublicId && canCreate && projectsQuery.isLoading);

  function toggleAttendee(person: WorkflowUserLookupDto) {
    const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
    setDraft((current) => ({ ...current, attendees: selected
      ? current.attendees.filter((attendee) => attendee.userId !== person.id)
      : [...current.attendees, { userId: person.id, email: "", displayName: person.fullName }] }));
  }

  function addExternalAttendee() {
    if (!canAddExternal) return;
    setDraft((current) => ({ ...current, attendees: [...current.attendees, { email: emailCandidate }] }));
    setAttendeeSearch("");
  }

  function removeAttendee(attendee: MeetingAttendeeDraft) {
    setDraft((current) => ({ ...current, attendees: current.attendees.filter((candidate) => candidate.userId !== attendee.userId || candidate.email !== attendee.email) }));
  }

  function updateExternalName(email: string, displayName: string) {
    setDraft((current) => ({ ...current, attendees: current.attendees.map((attendee) => attendee.email === email && !attendee.userId ? { ...attendee, displayName } : attendee) }));
  }

  function addAgendaItem() {
    const minutes = agendaDuration.trim() ? Number(agendaDuration) : undefined;
    if (!agendaTopic.trim() || agendaTopic.trim().length > 500 || (minutes !== undefined && (!Number.isInteger(minutes) || minutes < 1 || minutes > 1440))) {
      setAgendaError(t("meetings.agendaValidation"));
      return;
    }
    const updatedItem = { key: editingAgendaKey ?? crypto.randomUUID(), topic: agendaTopic.trim(), description: agendaDescription.trim(), durationMinutes: minutes?.toString() ?? "" };
    setAgendaDrafts((current) => editingAgendaKey
      ? current.map((item) => item.key === editingAgendaKey ? updatedItem : item)
      : [...current, updatedItem]);
    clearAgendaEditor();
  }

  function editAgendaItem(item: AgendaDraft) {
    setEditingAgendaKey(item.key);
    setAgendaTopic(item.topic);
    setAgendaDescription(item.description);
    setAgendaDuration(item.durationMinutes);
    setAgendaError(null);
  }

  function clearAgendaEditor() {
    setAgendaTopic(""); setAgendaDescription(""); setAgendaDuration(""); setEditingAgendaKey(null); setAgendaError(null);
  }

  function updateStartTime(startTime: string) {
    setDraft((current) => {
      const currentDuration = calculateDurationMinutes(current.startTime, current.endTime);
      return {
        ...current,
        startTime,
        endTime: currentDuration === null ? current.endTime : addMinutesToLocalDateTime(startTime, currentDuration),
      };
    });
  }

  function updateEndTime(endTime: string) {
    setDraft((current) => ({ ...current, endTime }));
  }

  function updateDuration(value: string) {
    const minutes = Number(value);
    if (!Number.isInteger(minutes) || minutes < 15 || minutes > 480 || minutes % 15 !== 0) return;
    setDraft((current) => ({ ...current, endTime: addMinutesToLocalDateTime(current.startTime, minutes) }));
  }

  function moveAgenda(index: number, offset: -1 | 1) {
    setAgendaDrafts((current) => {
      const nextIndex = index + offset;
      if (nextIndex < 0 || nextIndex >= current.length) return current;
      const next = [...current];
      [next[index], next[nextIndex]] = [next[nextIndex], next[index]];
      return next;
    });
  }

  function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const validationError = validateMeetingDraft(draft);
    if (validationError) { setError(t(`meetings.validation.${validationError}`)); return; }
    if (projectLookupUnavailable) { setError(t("meetings.projectOptionsUnavailable")); return; }
    if (projectLookupFailed || projectLookupPending || (projectPublicId && !projectId)) { setError(t("meetings.projectLoadError")); return; }
    setError(null);
    const template = {
      ...toMeetingInput(draft, projectId || undefined),
      agendaItems: agendaDrafts.map(({ topic, description, durationMinutes }) => ({ topic, description: description || undefined, durationMinutes: durationMinutes ? Number(durationMinutes) : undefined })),
    };
    if (recurring) {
      if (!recurrenceRule || recurrencePreview.isLoading || recurrencePreview.isFetching || recurrencePreview.isError || !recurrencePreview.data) {
        setError(recurrencePreview.isError ? t("meetings.recurrence.previewError") : t("meetings.recurrence.previewPending"));
        return;
      }
      const fingerprint = JSON.stringify({ template, recurrenceRule });
      if (recurrenceRequestRef.current?.fingerprint !== fingerprint)
        recurrenceRequestRef.current = { fingerprint, requestId: crypto.randomUUID() };
      mutations.createSeries.mutate({ requestId: recurrenceRequestRef.current.requestId, template, rule: recurrenceRule }, {
        onSuccess: (meeting) => navigate(meetingPublicPath(meeting.publicId) ?? "/meetings"),
        onError: () => setError(t("meetings.saveError")),
      });
      return;
    }
    mutations.create.mutate(template, {
      onSuccess: (meeting) => navigate(meetingPublicPath(meeting.publicId) ?? "/meetings"),
      onError: () => setError(t("meetings.saveError")),
    });
  }

  const reminderOptions = [
    { value: "", label: t("meetings.noReminder") },
    { value: "15", label: t("meetings.fifteenMinutesBefore") },
    { value: "30", label: t("meetings.thirtyMinutesBefore") },
    { value: "60", label: t("meetings.oneHourBefore") },
    { value: "1440", label: t("meetings.oneDayBefore") },
  ];

  const durationMinutes = calculateDurationMinutes(draft.startTime, draft.endTime);
  const durationHours = durationMinutes === null ? 0 : Math.floor(durationMinutes / 60);
  const remainingMinutes = durationMinutes === null ? 0 : durationMinutes % 60;
  const formatDurationOption = (minutes: number) => {
    const hours = Math.floor(minutes / 60);
    const remainder = minutes % 60;
    if (hours && remainder) return `${t("meetings.hours", { count: hours })} ${t("meetings.minutesShort", { count: remainder })}`;
    return hours ? t("meetings.hours", { count: hours }) : t("meetings.minutesShort", { count: remainder });
  };
  const durationLabel = durationMinutes === null
    ? t("meetings.notAvailable")
    : durationHours && remainingMinutes
      ? `${t("meetings.hours", { count: durationHours })} ${t("meetings.minutesShort", { count: remainingMinutes })}`
      : durationHours
        ? t("meetings.hours", { count: durationHours })
        : t("meetings.minutesShort", { count: remainingMinutes });
  const durationOptionMatches = durationMinutes !== null && durationMinutes >= 15 && durationMinutes <= 480 && durationMinutes % 15 === 0;
  const durationSelectValue = durationOptionMatches
    ? String(durationMinutes)
    : durationMinutes !== null
      ? "custom"
      : "";

  React.useEffect(() => {
    if (!draft.startTime) return;
    setRecurrenceStartDate(draft.startTime.slice(0, 10));
  }, [draft.startTime]);

  const recurrenceRule = React.useMemo<MeetingRecurrenceRule | null>(() => {
    if (!recurrenceStartDate || !recurrenceEndDate || durationMinutes === null || !recurrenceTimeZone.trim()) return null;
    if (recurrenceFrequency === "Weekly" && recurrenceWeekdays.length === 0) return null;
    return {
      frequency: recurrenceFrequency,
      startDate: recurrenceStartDate,
      endDate: recurrenceEndDate,
      localStartTime: `${draft.startTime.slice(11, 16)}:00`,
      durationMinutes,
      timeZoneId: recurrenceTimeZone.trim(),
      weekdays: recurrenceFrequency === "Weekly" ? [...recurrenceWeekdays].sort((a, b) => a - b) : [],
    };
  }, [recurrenceEndDate, recurrenceFrequency, recurrenceStartDate, recurrenceTimeZone, recurrenceWeekdays, durationMinutes, draft.startTime]);

  React.useEffect(() => {
    if (!recurring || !recurrenceRule) { setDebouncedRecurrenceRule(null); return; }
    const timer = window.setTimeout(() => setDebouncedRecurrenceRule(recurrenceRule), 300);
    return () => window.clearTimeout(timer);
  }, [recurring, recurrenceRule]);

  const recurrencePreview = useMeetingRecurrencePreview(debouncedRecurrenceRule, recurring);
  const timeZoneOptions = React.useMemo(() => {
    const intl = Intl as typeof Intl & { supportedValuesOf?: (key: "timeZone") => string[] };
    const values = intl.supportedValuesOf?.("timeZone") ?? [];
    return Array.from(new Set([recurrenceTimeZone, "UTC", ...values])).sort((a, b) => a.localeCompare(b));
  }, [recurrenceTimeZone]);
  if (user && !canCreate) return <div className="space-y-5"><Link to="/meetings" className="button-outline inline-flex items-center"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("meetings.backToMeetings")}</Link><Card><CardContent className="p-6 text-sm text-destructive">{t("meetings.permissionDenied")}</CardContent></Card></div>;

  const formErrors = Array.from(new Set([
    error,
    agendaError,
    projectLookupUnavailable ? t("meetings.projectOptionsUnavailable") : null,
    projectLookupFailed ? t("meetings.projectLoadError") : null,
    projectsQuery.isError && !projectPublicId ? t("meetings.projectsLoadError") : null,
    usersQuery.isError ? t("meetings.usersLoadError") : null,
    recurring && recurrencePreview.isError ? t("meetings.recurrence.previewError") : null,
  ].filter((message): message is string => Boolean(message))));

  return <form noValidate onSubmit={save} aria-labelledby="meeting-create-title" className="w-full min-w-0 space-y-6">
    <div className="space-y-3">
      <Link to="/meetings" className="button-outline inline-flex items-center"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("meetings.backToMeetings")}</Link>
      <div>
        <h1 id="meeting-create-title" className="text-2xl font-semibold tracking-tight">{t("meetings.createMeeting")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("meetings.createDescription")}</p>
      </div>
    </div>
    {formErrors.length > 0 ? <div role="alert" aria-live="assertive" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive"><ul className="list-inside list-disc space-y-1">{formErrors.map((message) => <li key={message}>{message}</li>)}</ul></div> : null}
    <div className="grid min-w-0 grid-cols-1 items-stretch gap-5 lg:grid-cols-2 2xl:grid-cols-12">
      <Card className="min-w-0 2xl:col-span-7">
        <CardHeader><CardTitle>{t("meetings.details")}</CardTitle></CardHeader>
        <CardContent className="grid min-w-0 gap-4 md:grid-cols-2">
          <div className="space-y-1.5 md:col-span-2"><Label htmlFor="meeting-page-title">{t("meetings.titleField")} *</Label><Input id="meeting-page-title" maxLength={500} value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} required /></div>
          <div className="space-y-1.5 md:col-span-2"><Label htmlFor="meeting-page-objective">{t("meetings.objective")}</Label><Textarea id="meeting-page-objective" rows={3} value={draft.objective} onChange={(event) => setDraft({ ...draft, objective: event.target.value })} /></div>
          <div className="space-y-1.5 md:col-span-2">
            <Label>{t("meetings.project")}</Label>
            <SearchableSelect options={projectOptions} value={projectId || undefined} onValueChange={setProjectId} placeholder={t("meetings.noProject")} searchPlaceholder={t("meetings.searchProjects")} emptyText={t("meetings.noProjects")} loading={projectsQuery.isLoading || projectLookupPending} disabled={Boolean(projectPublicId) || projectsQuery.isError} />
          </div>
          <div className="space-y-1.5"><Label htmlFor="meeting-page-location">{t("meetings.location")}</Label><Input id="meeting-page-location" value={draft.location} onChange={(event) => setDraft({ ...draft, location: event.target.value })} /></div>
          <div className="space-y-1.5"><Label htmlFor="meeting-page-link">{t("meetings.meetingLink")} <span className="text-xs font-normal text-muted-foreground">({t("meetings.optional")})</span></Label><Input id="meeting-page-link" type="url" value={draft.meetingLink} onChange={(event) => setDraft({ ...draft, meetingLink: event.target.value })} /></div>
        </CardContent>
      </Card>

      <Card className="min-w-0 2xl:col-span-5">
        <CardHeader><CardTitle>{t("meetings.scheduleTitle")}</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid min-w-0 gap-3 sm:grid-cols-2">
            <div className="min-w-0 space-y-1.5"><Label htmlFor="meeting-page-start">{t("meetings.start")} *</Label><Input id="meeting-page-start" type="datetime-local" value={draft.startTime} onChange={(event) => updateStartTime(event.target.value)} required /></div>
            <div className="min-w-0 space-y-1.5"><Label htmlFor="meeting-page-end">{t("meetings.end")} *</Label><Input id="meeting-page-end" type="datetime-local" value={draft.endTime} onChange={(event) => updateEndTime(event.target.value)} required /></div>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 sm:items-end">
            <div className="min-w-0 space-y-1.5">
              <div className="flex items-center gap-1.5">
                <Label htmlFor="meeting-page-duration">{t("meetings.duration")}</Label>
                <span className="group relative inline-flex">
                  <button type="button" aria-label={t("meetings.durationCalculated")} className="rounded-full p-0.5 text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                    <Info className="h-3.5 w-3.5" aria-hidden="true" />
                  </button>
                  <span role="tooltip" className="pointer-events-none absolute bottom-full start-1/2 z-50 mb-2 hidden w-56 max-w-[calc(100vw-2rem)] -translate-x-1/2 rounded-md border bg-popover px-3 py-2 text-xs font-normal text-popover-foreground shadow-md group-hover:block group-focus-within:block">
                    {t("meetings.durationCalculated")}
                  </span>
                </span>
              </div>
              <Select value={durationSelectValue} onValueChange={updateDuration}>
                <SelectTrigger id="meeting-page-duration" disabled={!draft.startTime}><SelectValue placeholder={t("meetings.notAvailable")} /></SelectTrigger>
                <SelectContent>
                  {durationMinutes !== null && !durationOptionMatches ? <SelectItem value="custom" disabled>{t("meetings.customDuration", { duration: durationLabel })}</SelectItem> : null}
                  {Array.from({ length: 32 }, (_, index) => (index + 1) * 15).map((minutes) => <SelectItem key={minutes} value={String(minutes)}>{formatDurationOption(minutes)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5"><Label htmlFor="meeting-page-reminder">{t("meetings.reminder")}</Label><select id="meeting-page-reminder" className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={draft.reminderOffsetMinutes} onChange={(event) => setDraft({ ...draft, reminderOffsetMinutes: event.target.value })}>{reminderOptions.map((option) => <option key={option.value || "none"} value={option.value}>{option.label}</option>)}</select></div>
          </div>
          <section className="space-y-4 rounded-lg border bg-muted/20 p-4" aria-labelledby="meeting-recurring-title">
            <label className="flex cursor-pointer items-start gap-3">
              <input id="meeting-recurring-toggle" type="checkbox" checked={recurring} onChange={(event) => setRecurring(event.target.checked)} className="mt-1 h-4 w-4 rounded border-input text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" />
              <span><span id="meeting-recurring-title" className="block text-sm font-semibold">{t("meetings.recurrence.toggle")}</span><span className="mt-1 block text-xs text-muted-foreground">{t("meetings.recurrence.description")}</span></span>
            </label>
            {recurring ? <div className="grid min-w-0 gap-4 border-t pt-4 sm:grid-cols-2">
              <div className="space-y-1.5"><Label htmlFor="meeting-recurrence-frequency">{t("meetings.recurrence.frequency")}</Label><select id="meeting-recurrence-frequency" className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={recurrenceFrequency} onChange={(event) => setRecurrenceFrequency(event.target.value as MeetingRecurrenceRule["frequency"])}><option value="Daily">{t("meetings.recurrence.daily")}</option><option value="EveryTwoDays">{t("meetings.recurrence.everyTwoDays")}</option><option value="Weekly">{t("meetings.recurrence.weekly")}</option><option value="Monthly">{t("meetings.recurrence.monthly")}</option></select></div>
              <div className="space-y-1.5"><Label htmlFor="meeting-recurrence-start">{t("meetings.recurrence.startDate")}</Label><Input id="meeting-recurrence-start" type="date" value={recurrenceStartDate} onChange={(event) => setRecurrenceStartDate(event.target.value)} required={recurring} /></div>
              <div className="space-y-1.5"><Label htmlFor="meeting-recurrence-end">{t("meetings.recurrence.endDate")}</Label><Input id="meeting-recurrence-end" type="date" min={recurrenceStartDate || undefined} value={recurrenceEndDate} onChange={(event) => setRecurrenceEndDate(event.target.value)} required={recurring} /><p className="text-xs text-muted-foreground">{t("meetings.recurrence.endDateLimit")}</p></div>
              <div className="space-y-1.5"><Label htmlFor="meeting-recurrence-timezone">{t("meetings.recurrence.timeZone")}</Label><Input id="meeting-recurrence-timezone" list="meeting-timezone-options" value={recurrenceTimeZone} onChange={(event) => setRecurrenceTimeZone(event.target.value)} required={recurring} /><datalist id="meeting-timezone-options">{timeZoneOptions.map((zone) => <option key={zone} value={zone} />)}</datalist></div>
              <div className="space-y-1.5 sm:col-span-2"><p className="text-sm font-medium">{t("meetings.recurrence.timeAndDuration")}</p><p className="text-xs text-muted-foreground">{t("meetings.recurrence.timeAndDurationValue", { time: draft.startTime.slice(11, 16) || t("meetings.notAvailable"), duration: durationLabel })}</p></div>
              {recurrenceFrequency === "Weekly" ? <div className="space-y-2 sm:col-span-2"><p className="text-sm font-medium">{t("meetings.recurrence.weekdays")}</p><div className="flex flex-wrap gap-2">{[[6, "saturday"], [0, "sunday"], [1, "monday"], [2, "tuesday"], [3, "wednesday"], [4, "thursday"], [5, "friday"]].map(([day, key]) => { const dayNumber = Number(day); const selected = recurrenceWeekdays.includes(dayNumber); return <button key={String(key)} type="button" aria-pressed={selected} onClick={() => setRecurrenceWeekdays((current) => selected ? current.filter((item) => item !== dayNumber) : [...current, dayNumber])} className={`min-h-10 rounded-md border px-3 text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${selected ? "border-primary bg-primary/10 text-primary" : "bg-background hover:bg-muted"}`}>{t(`meetings.recurrence.days.${String(key)}`)}</button>; })}</div></div> : null}
              {recurrenceFrequency === "Monthly" ? <p className="text-xs text-muted-foreground sm:col-span-2">{t("meetings.recurrence.monthlyRule")}</p> : null}
              <div className="space-y-2 sm:col-span-2" aria-live="polite"><p className="text-sm font-semibold">{t("meetings.recurrence.preview")}</p>{!recurrenceRule ? <p className="rounded-md border border-dashed px-3 py-3 text-xs text-muted-foreground">{t("meetings.recurrence.completeSettings")}</p> : recurrencePreview.isLoading || recurrencePreview.isFetching ? <p className="text-xs text-muted-foreground">{t("common.loading")}</p> : recurrencePreview.isError ? null : recurrencePreview.data ? <><ol className="max-h-44 space-y-1 overflow-y-auto rounded-md border bg-background p-2">{recurrencePreview.data.occurrences.slice(0, 6).map((occurrence) => <li key={occurrence.occurrenceKey} className="flex items-center justify-between gap-3 rounded px-2 py-1.5 text-xs"><span>{formatDate(occurrence.startTime, { dateStyle: "medium", timeStyle: "short", timeZone: recurrenceTimeZone })}</span><span className="shrink-0 text-muted-foreground">{recurrenceTimeZone}</span></li>)}</ol><p className="text-xs text-muted-foreground">{t("meetings.recurrence.previewCount", { count: recurrencePreview.data.totalOccurrenceCount })}{recurrencePreview.data.skippedDaylightSavingCount ? ` · ${t("meetings.recurrence.skippedDst", { count: recurrencePreview.data.skippedDaylightSavingCount })}` : ""}</p></> : null}</div>
            </div> : null}
          </section>
        </CardContent>
      </Card>

      <Card className="min-w-0 2xl:col-span-5">
        <CardHeader><CardTitle>{t("meetings.peopleTitle")}</CardTitle></CardHeader>
        <CardContent className="space-y-5">
          <div className="space-y-1.5"><Label>{t("meetings.organizer")} *</Label><SearchableSelect options={userOptions} value={draft.organizerId || undefined} onValueChange={(organizerId) => setDraft({ ...draft, organizerId })} placeholder={t("meetings.selectOrganizer")} searchPlaceholder={t("meetings.searchUsers")} emptyText={t("meetings.noUsers")} loading={usersQuery.isLoading} disabled={usersQuery.isError} />{usersQuery.isError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("meetings.usersLoadError")}</span><Button type="button" size="sm" variant="outline" onClick={() => void usersQuery.refetch()}>{t("common.retry")}</Button></div> : null}</div>
          <div className="space-y-3">
            <div><h3 className="text-sm font-medium">{t("meetings.attendees")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("meetings.attendeeDescription")}</p></div>
            <Input aria-label={t("meetings.searchUsers")} value={attendeeSearch} onChange={(event) => setAttendeeSearch(event.target.value)} placeholder={t("meetings.searchUsers")} disabled={usersQuery.isError} />
            {usersQuery.isError ? null : visibleUsers.length ? <div className="max-h-56 space-y-1 overflow-y-auto overscroll-contain rounded-md border p-1">{visibleUsers.map((person) => { const selected = draft.attendees.some((attendee) => attendee.userId === person.id); return <button key={person.id} type="button" aria-pressed={selected} onClick={() => toggleAttendee(person)} className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-start text-sm hover:bg-accent"><input type="checkbox" checked={selected} readOnly tabIndex={-1} /><UserRound className="h-3.5 w-3.5 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate">{person.fullName}</span></button>; })}</div> : usersQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("meetings.loadingUsers")}</p> : <p className="text-xs text-muted-foreground">{t("meetings.noUsers")}</p>}
            {canAddExternal ? <Button type="button" size="sm" variant="outline" onClick={addExternalAttendee}><Mail className="me-1.5 h-4 w-4" />{t("meetings.addExternalAttendee", { email: emailCandidate })}</Button> : null}
            {draft.attendees.length ? <div className="max-h-64 space-y-2 overflow-y-auto overscroll-contain border-t pt-3"><p className="text-xs text-muted-foreground">{t("meetings.selectedUsers", { count: draft.attendees.length })}</p>{draft.attendees.map((attendee) => <div key={attendee.userId ?? attendee.email.toLocaleLowerCase()} className="flex min-w-0 items-start gap-2 rounded-md border p-2"><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{attendee.displayName || attendee.email}</p><p className="truncate text-xs text-muted-foreground">{t(attendee.userId ? "meetings.internalUser" : "meetings.externalAttendee")} · {attendee.email}</p>{!attendee.userId ? <Input className="mt-1.5 h-8" maxLength={200} value={attendee.displayName ?? ""} onChange={(event) => updateExternalName(attendee.email, event.target.value)} placeholder={t("meetings.optionalDisplayName")} aria-label={t("meetings.optionalDisplayName")} /> : null}</div><Button type="button" size="sm" variant="ghost" aria-label={t("meetings.removeUser", { name: attendee.displayName || attendee.email })} onClick={() => removeAttendee(attendee)}><Trash2 className="h-4 w-4" /></Button></div>)}</div> : null}
          </div>
        </CardContent>
      </Card>

      <Card className="min-w-0 2xl:col-span-7">
        <CardHeader><CardTitle>{t("meetings.agendaTitle")}</CardTitle><CardDescription>{t("meetings.createAgendaDescription")}</CardDescription></CardHeader>
        <CardContent className="grid min-w-0 gap-6 2xl:grid-cols-[minmax(0,1.2fr)_minmax(18rem,0.8fr)] 2xl:items-start">
          <div className="min-w-0">
            {agendaDrafts.length ? <ol className="space-y-2">{agendaDrafts.map((item, index) => <li key={item.key} className="flex min-w-0 flex-col gap-3 rounded-md border p-3 sm:flex-row sm:items-start"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-muted-foreground">{index + 1}</span><div className="min-w-0 flex-1"><p className="break-words text-sm font-medium">{item.topic}</p>{item.description ? <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">{item.description}</p> : null}{item.durationMinutes ? <p className="mt-1 text-xs text-muted-foreground">{t("meetings.agendaDuration", { count: Number(item.durationMinutes) })}</p> : null}</div><div className="flex w-full flex-wrap gap-1 sm:w-auto sm:shrink-0"><Button type="button" size="icon" variant="outline" aria-label={t("meetings.moveAgendaUp", { topic: item.topic })} disabled={index === 0} onClick={() => moveAgenda(index, -1)}><ArrowUp className="h-4 w-4" /></Button><Button type="button" size="icon" variant="outline" aria-label={t("meetings.moveAgendaDown", { topic: item.topic })} disabled={index === agendaDrafts.length - 1} onClick={() => moveAgenda(index, 1)}><ArrowDown className="h-4 w-4" /></Button><Button type="button" size="icon" variant="outline" aria-label={t("meetings.editAgenda", { topic: item.topic })} onClick={() => editAgendaItem(item)}><Pencil className="h-4 w-4" /></Button><Button type="button" size="icon" variant="outline" className="text-destructive" aria-label={t("meetings.deleteAgenda", { topic: item.topic })} onClick={() => { setAgendaDrafts((current) => current.filter((candidate) => candidate.key !== item.key)); if (editingAgendaKey === item.key) clearAgendaEditor(); }}><Trash2 className="h-4 w-4" /></Button></div></li>)}</ol> : <p className="rounded-md border border-dashed px-3 py-5 text-center text-sm text-muted-foreground">{t("meetings.agendaEmpty")}</p>}
          </div>
          <div className="grid min-w-0 gap-3 border-t pt-4 2xl:border-t-0 2xl:pt-0">
            <div className="space-y-1.5"><Label htmlFor="meeting-page-agenda-topic">{t("meetings.agendaTopic")}</Label><Input id="meeting-page-agenda-topic" maxLength={500} value={agendaTopic} onChange={(event) => setAgendaTopic(event.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="meeting-page-agenda-description">{t("meetings.agendaItemDescription")}</Label><Textarea id="meeting-page-agenda-description" rows={3} maxLength={4000} value={agendaDescription} onChange={(event) => setAgendaDescription(event.target.value)} /></div>
            <div className="space-y-1.5 sm:max-w-48"><Label htmlFor="meeting-page-agenda-duration">{t("meetings.agendaDurationLabel")}</Label><Input id="meeting-page-agenda-duration" type="number" min={1} max={1440} value={agendaDuration} onChange={(event) => setAgendaDuration(event.target.value)} /></div>
            <div className="flex flex-wrap justify-end gap-2">{editingAgendaKey ? <Button type="button" size="sm" variant="outline" onClick={clearAgendaEditor}><X className="me-1.5 h-4 w-4" />{t("common.cancel")}</Button> : null}<Button type="button" size="sm" variant="outline" onClick={addAgendaItem}>{editingAgendaKey ? <Pencil className="me-1.5 h-4 w-4" /> : <Plus className="me-1.5 h-4 w-4" />}{editingAgendaKey ? t("meetings.saveAgenda") : t("meetings.addAgenda")}</Button></div>
          </div>
        </CardContent>
      </Card>
    </div>
    <div className="flex flex-col-reverse gap-2 border-t pt-5 sm:flex-row sm:justify-end">
      <Button asChild type="button" variant="outline" className="w-full sm:w-auto"><Link to="/meetings">{t("common.cancel")}</Link></Button>
      <Button type="submit" className="w-full sm:w-auto" disabled={mutations.create.isPending || mutations.createSeries.isPending || projectLookupPending || projectLookupFailed || (recurring && (!recurrenceRule || recurrencePreview.isLoading || recurrencePreview.isFetching || recurrencePreview.isError || !recurrencePreview.data))}>{mutations.create.isPending || mutations.createSeries.isPending ? t("common.saving") : t("meetings.createMeeting")}</Button>
    </div>
  </form>;
}
