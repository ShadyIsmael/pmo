import * as React from "react";
import { Mail, UserRound, X } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { Textarea } from "@components/ui/Textarea";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { useI18n } from "@i18n/messages";
import { ApiError, getApiFieldError } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { useMeetingProjectOptions, useMeetingFullMutations, type MeetingDetailDto } from "@api/meetingsFull";
import { useMeetingWorkflowUserLookup } from "@features/projects/lib/project-lookups";
import type { WorkflowUserLookupDto } from "@features/projects/lib/project-lookups";
import { hasPermission, Permissions } from "@lib/permissions";
import { createMeetingDraft, isValidMeetingEmail, meetingToDraft, toMeetingInput, validateMeetingDraft, type MeetingAttendeeDraft } from "./meeting-form";

interface MeetingCreateSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  meeting?: MeetingDetailDto;
  fixedProjectId?: string;
  initialProject?: { id: string; name: string; code: string };
  projectLocked?: boolean;
  onCreated?: (meeting: MeetingDetailDto) => void;
  onSaved?: (meeting: MeetingDetailDto) => void;
}

export function MeetingCreateSheet({ open, onOpenChange, meeting, fixedProjectId, initialProject, projectLocked = false, onCreated, onSaved }: MeetingCreateSheetProps) {
  const { t } = useI18n();
  const { user } = useAuth();
  const isEdit = Boolean(meeting);
  const reminderOptions = [
    { value: "", label: t("meetings.noReminder") },
    { value: "15", label: t("meetings.fifteenMinutesBefore") },
    { value: "30", label: t("meetings.thirtyMinutesBefore") },
    { value: "60", label: t("meetings.oneHourBefore") },
    { value: "1440", label: t("meetings.oneDayBefore") },
  ] as const;
  const [draft, setDraft] = React.useState(() => createMeetingDraft());
  const [projectId, setProjectId] = React.useState("");
  const [attendeeSearch, setAttendeeSearch] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [apiError, setApiError] = React.useState<unknown>(null);
  const lockedProjectId = projectLocked ? fixedProjectId ?? initialProject?.id : undefined;
  const canManageMeeting = Boolean(user && hasPermission(user.permissions, meeting ? Permissions.Meeting.Edit : Permissions.Meeting.Create));
  const canUseMeetingProjects = canManageMeeting || Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const mutations = useMeetingFullMutations(meeting?.id, meeting?.publicId ?? undefined);
  const projectsQuery = useMeetingProjectOptions({}, open && canUseMeetingProjects && !lockedProjectId);
  const usersQuery = useMeetingWorkflowUserLookup(open && canManageMeeting);
  const projects = projectsQuery.data;
  const users = usersQuery.data;
  const projectOptions = React.useMemo(() => {
    const options = (projects ?? []).map((project) => ({ value: project.id, label: project.code ? `${project.name} · ${project.code}` : project.name, keywords: project.code ? [project.code] : [] }));
    if (initialProject && !options.some((option) => option.value === initialProject.id)) {
      options.unshift({ value: initialProject.id, label: `${initialProject.name} · ${initialProject.code}`, keywords: [initialProject.code] });
    }
    if (meeting?.projectId && meeting.projectName && !options.some((option) => option.value === meeting.projectId)) {
      options.unshift({ value: meeting.projectId, label: meeting.projectName, keywords: [] });
    }
    return options;
  }, [initialProject, meeting?.projectId, meeting?.projectName, projects]);
  const userOptions = React.useMemo(() => {
    const candidates: WorkflowUserLookupDto[] = [...(users ?? [])];
    if (meeting?.organizerId && !candidates.some((user) => user.id === meeting.organizerId)) {
      candidates.push({ id: meeting.organizerId, fullName: meeting.organizerName ?? meeting.organizerId });
    }
    for (const attendee of meeting?.attendees ?? []) {
      if (attendee.isInternalUser && attendee.userId && !candidates.some((user) => user.id === attendee.userId)) {
        candidates.push({ id: attendee.userId, fullName: attendee.displayName ?? attendee.email });
      }
    }
    return candidates.map((user) => ({ value: user.id, label: user.fullName, keywords: [] }));
  }, [meeting, users]);
  const selectableUsers = React.useMemo(() => {
    const candidates: WorkflowUserLookupDto[] = [...(users ?? [])];
    for (const attendee of meeting?.attendees ?? []) {
      if (attendee.isInternalUser && attendee.userId && !candidates.some((user) => user.id === attendee.userId)) {
        candidates.push({ id: attendee.userId, fullName: attendee.displayName ?? attendee.email });
      }
    }
    return candidates;
  }, [meeting, users]);
  const attendeeQuery = attendeeSearch.trim().toLocaleLowerCase();
  const visibleUsers = selectableUsers.filter((candidate) => !attendeeQuery || candidate.fullName.toLocaleLowerCase().includes(attendeeQuery));
  const externalEmail = attendeeSearch.trim();
  const canAddExternal = isValidMeetingEmail(externalEmail)
    && !draft.attendees.some((attendee) => !attendee.userId && attendee.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());

  React.useEffect(() => {
    if (!open) return;
    setDraft(meeting ? meetingToDraft(meeting) : createMeetingDraft());
    setProjectId(meeting?.projectId ?? lockedProjectId ?? "");
    setAttendeeSearch("");
    setError(null);
    setApiError(null);
  }, [lockedProjectId, meeting, open]);

  function toggleAttendee(person: WorkflowUserLookupDto) {
    const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
    setDraft((current) => ({ ...current, attendees: selected
      ? current.attendees.filter((attendee) => attendee.userId !== person.id)
      : [...current.attendees, { userId: person.id, email: "", displayName: person.fullName }] }));
  }

  function addExternalAttendee() {
    if (!canAddExternal) return;
    setDraft((current) => ({ ...current, attendees: [...current.attendees, { email: externalEmail }] }));
    setAttendeeSearch("");
  }

  function removeAttendee(attendee: MeetingAttendeeDraft) {
    setDraft((current) => ({ ...current, attendees: current.attendees.filter((candidate) => candidate.userId !== attendee.userId || candidate.email !== attendee.email) }));
  }

  function updateExternalName(email: string, displayName: string) {
    setDraft((current) => ({ ...current, attendees: current.attendees.map((attendee) => attendee.email === email && !attendee.userId ? { ...attendee, displayName } : attendee) }));
  }

  function save() {
    const validationError = validateMeetingDraft(draft);
    if (validationError) {
      setError(t(`meetings.validation.${validationError}`));
      return;
    }
    setError(null);
    setApiError(null);
    const body = toMeetingInput(draft, lockedProjectId ?? (projectId || undefined));
    const onSuccess = (saved: MeetingDetailDto) => {
      onOpenChange(false);
      if (meeting) onSaved?.(saved);
      else onCreated?.(saved);
    };
    if (meeting) {
      mutations.update.mutate({ body: { ...body, rowVersion: meeting.rowVersion, status: meeting.status ?? undefined } }, { onSuccess, onError: (reason) => { setApiError(reason); setError(reason instanceof ApiError ? reason.message : t("meetings.saveError")); } });
    } else {
      mutations.create.mutate(body, { onSuccess, onError: (reason) => { setApiError(reason); setError(reason instanceof ApiError ? reason.message : t("meetings.saveError")); } });
    }
  }

  function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    save();
  }

  return <SideSheet open={open} onOpenChange={onOpenChange} title={t(isEdit ? "meetings.editMeeting" : "meetings.createMeeting")} description={t(isEdit ? "meetings.editDescription" : "meetings.createDescription")} size="lg">
    <form onSubmit={submit} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-5">
        <div className="space-y-1.5"><Label htmlFor="meeting-create-title">{t("meetings.titleField")} *</Label><Input id="meeting-create-title" maxLength={500} value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} aria-invalid={Boolean(getApiFieldError(apiError, "title"))} aria-describedby={getApiFieldError(apiError, "title") ? "meeting-title-error" : undefined} /><ApiFieldError id="meeting-title-error" error={apiError} fieldName="title" /></div>
        <div className="space-y-1.5"><Label htmlFor="meeting-create-objective">{t("meetings.objective")}</Label><Textarea id="meeting-create-objective" rows={3} value={draft.objective} onChange={(event) => setDraft({ ...draft, objective: event.target.value })} /></div>
        <div className="space-y-1.5"><Label>{t("meetings.project")}</Label><SearchableSelect aria-label={t("meetings.project")} aria-invalid={Boolean(getApiFieldError(apiError, "projectId"))} aria-describedby={getApiFieldError(apiError, "projectId") ? "meeting-project-error" : undefined} options={projectOptions} value={lockedProjectId ?? projectId} onValueChange={setProjectId} placeholder={t("meetings.noProject")} searchPlaceholder={t("meetings.searchProjects")} emptyText={t("meetings.noProjects")} loading={projectsQuery.isLoading} disabled={Boolean(lockedProjectId) || !canUseMeetingProjects || projectsQuery.isError} /><ApiFieldError id="meeting-project-error" error={apiError} fieldName="projectId" />{!lockedProjectId && !canUseMeetingProjects ? <p role="status" className="text-xs text-muted-foreground">{t("meetings.projectOptionsUnavailable")}</p> : null}{!lockedProjectId && projectsQuery.isError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("meetings.projectsLoadError")}</span><Button type="button" size="sm" variant="outline" onClick={() => void projectsQuery.refetch()}>{t("common.retry")}</Button></div> : null}</div>
        <div className="space-y-1.5"><Label>{t("meetings.organizer")} *</Label><SearchableSelect aria-label={t("meetings.organizer")} aria-invalid={Boolean(getApiFieldError(apiError, "organizerId"))} aria-describedby={getApiFieldError(apiError, "organizerId") ? "meeting-organizer-error" : undefined} options={userOptions} value={draft.organizerId} onValueChange={(organizerId) => setDraft({ ...draft, organizerId })} placeholder={t("meetings.selectOrganizer")} searchPlaceholder={t("meetings.searchUsers")} emptyText={t("meetings.noUsers")} loading={usersQuery.isLoading} disabled={usersQuery.isError} /><ApiFieldError id="meeting-organizer-error" error={apiError} fieldName="organizerId" />{usersQuery.isError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("meetings.usersLoadError")}</span><Button type="button" size="sm" variant="outline" onClick={() => void usersQuery.refetch()}>{t("common.retry")}</Button></div> : null}</div>
        <div className="space-y-2">
          <Label htmlFor="meeting-create-attendee-search">{t("meetings.attendees")}</Label>
          <Input id="meeting-create-attendee-search" value={attendeeSearch} onChange={(event) => setAttendeeSearch(event.target.value)} placeholder={t("meetings.searchUsers")} />
          {draft.attendees.length > 0 ? <p className="text-xs text-muted-foreground">{t("meetings.selectedUsers", { count: draft.attendees.length })}</p> : null}
          {usersQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("meetings.loadingUsers")}</p> : usersQuery.isError ? null : visibleUsers.length > 0 ? <div className="max-h-36 space-y-1 overflow-y-auto rounded-md border p-1">{visibleUsers.map((person) => {
            const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
            return <button key={person.id} type="button" aria-pressed={selected} onClick={() => toggleAttendee(person)} className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-start text-sm hover:bg-accent"><input type="checkbox" checked={selected} readOnly tabIndex={-1} /><UserRound className="h-3.5 w-3.5 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate">{person.fullName}</span></button>;
          })}</div> : <p className="text-xs text-muted-foreground">{t("meetings.noUsers")}</p>}
          {canAddExternal ? <Button type="button" size="sm" variant="outline" onClick={addExternalAttendee}><Mail className="me-1.5 h-3.5 w-3.5" />{t("meetings.addExternalAttendee", { email: externalEmail })}</Button> : null}
          {draft.attendees.length > 0 ? <div className="space-y-2">{draft.attendees.map((attendee) => <div key={attendee.userId ?? attendee.email.toLocaleLowerCase()} className="flex items-start gap-2 rounded-md border bg-muted/20 p-2"><div className="min-w-0 flex-1"><p className="truncate text-xs font-medium">{attendee.displayName || attendee.email}</p><p className="truncate text-xs text-muted-foreground">{t(attendee.userId ? "meetings.internalUser" : "meetings.externalAttendee")} · {attendee.email}</p>{!attendee.userId ? <Input className="mt-1.5 h-8" maxLength={200} value={attendee.displayName ?? ""} onChange={(event) => updateExternalName(attendee.email, event.target.value)} placeholder={t("meetings.optionalDisplayName")} aria-label={t("meetings.optionalDisplayName")} /> : null}</div><Button type="button" size="sm" variant="ghost" aria-label={t("meetings.removeUser", { name: attendee.displayName || attendee.email })} onClick={() => removeAttendee(attendee)}><X className="h-4 w-4" /></Button></div>)}</div> : null}
        </div>
        <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-create-start">{t("meetings.start")} *</Label><Input id="meeting-create-start" type="datetime-local" value={draft.startTime} onChange={(event) => setDraft({ ...draft, startTime: event.target.value })} aria-invalid={Boolean(getApiFieldError(apiError, "startTime"))} aria-describedby={getApiFieldError(apiError, "startTime") ? "meeting-start-error" : undefined} /><ApiFieldError id="meeting-start-error" error={apiError} fieldName="startTime" /></div><div className="space-y-1.5"><Label htmlFor="meeting-create-end">{t("meetings.end")} *</Label><Input id="meeting-create-end" type="datetime-local" value={draft.endTime} onChange={(event) => setDraft({ ...draft, endTime: event.target.value })} aria-invalid={Boolean(getApiFieldError(apiError, "endTime"))} aria-describedby={getApiFieldError(apiError, "endTime") ? "meeting-end-error" : undefined} /><ApiFieldError id="meeting-end-error" error={apiError} fieldName="endTime" /></div></div>
        <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-create-location">{t("meetings.location")}</Label><Input id="meeting-create-location" value={draft.location} onChange={(event) => setDraft({ ...draft, location: event.target.value })} /></div><div className="space-y-1.5"><Label htmlFor="meeting-create-link">{t("meetings.meetingLink")}</Label><Input id="meeting-create-link" type="text" value={draft.meetingLink} onChange={(event) => setDraft({ ...draft, meetingLink: event.target.value })} /></div></div>
        <div className="space-y-1.5"><Label htmlFor="meeting-create-reminder">{t("meetings.reminder")}</Label><select id="meeting-create-reminder" className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={draft.reminderOffsetMinutes} onChange={(event) => setDraft({ ...draft, reminderOffsetMinutes: event.target.value })}>{reminderOptions.map((option) => <option key={option.value || "none"} value={option.value}>{option.label}</option>)}</select></div>
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={mutations.create.isPending || mutations.update.isPending}>{mutations.create.isPending || mutations.update.isPending ? t("common.saving") : t("common.save")}</Button></SideSheetFooter>
    </form>
  </SideSheet>;
}
