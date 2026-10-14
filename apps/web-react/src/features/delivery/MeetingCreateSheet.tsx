import * as React from "react";
import { Mail, UserRound, X } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { useI18n } from "@i18n/messages";
import { useProjects } from "@features/projects/api/projects";
import type { MeetingDetailDto } from "@api/meetingsFull";
import { useMeetingFullMutations } from "@api/meetingsFull";
import { useUserLookup } from "@features/projects/lib/project-lookups";
import type { UserLookupDto } from "@features/projects/lib/project-lookups";
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
  const mutations = useMeetingFullMutations(meeting?.id);
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, open);
  const usersQuery = useUserLookup(undefined, open);
  const projects = projectsQuery.data?.items;
  const users = usersQuery.data;
  const lockedProjectId = projectLocked ? fixedProjectId ?? initialProject?.id : undefined;
  const projectOptions = React.useMemo(() => {
    const options = (projects ?? []).map((project) => ({ value: project.id, label: `${project.name} · ${project.code}`, keywords: [project.code] }));
    if (initialProject && !options.some((option) => option.value === initialProject.id)) {
      options.unshift({ value: initialProject.id, label: `${initialProject.name} · ${initialProject.code}`, keywords: [initialProject.code] });
    }
    if (meeting?.projectId && meeting.projectName && !options.some((option) => option.value === meeting.projectId)) {
      options.unshift({ value: meeting.projectId, label: meeting.projectName, keywords: [] });
    }
    return options;
  }, [initialProject, meeting?.projectId, meeting?.projectName, projects]);
  const userOptions = React.useMemo(() => {
    const candidates = [...(users ?? [])];
    if (meeting?.organizerId && !candidates.some((user) => user.id === meeting.organizerId)) {
      candidates.push({ id: meeting.organizerId, fullName: meeting.organizerName ?? meeting.organizerId, email: "" });
    }
    for (const attendee of meeting?.attendees ?? []) {
      if (attendee.isInternalUser && attendee.userId && !candidates.some((user) => user.id === attendee.userId)) {
        candidates.push({ id: attendee.userId, fullName: attendee.displayName ?? attendee.email, email: attendee.email });
      }
    }
    return candidates.map((user) => ({ value: user.id, label: user.email ? `${user.fullName} · ${user.email}` : user.fullName, keywords: [user.jobTitle ?? ""] }));
  }, [meeting, users]);
  const selectableUsers = React.useMemo(() => {
    const candidates = [...(users ?? [])];
    for (const attendee of meeting?.attendees ?? []) {
      if (attendee.isInternalUser && attendee.userId && !candidates.some((user) => user.id === attendee.userId)) {
        candidates.push({ id: attendee.userId, fullName: attendee.displayName ?? attendee.email, email: attendee.email });
      }
    }
    return candidates;
  }, [meeting, users]);
  const attendeeQuery = attendeeSearch.trim().toLocaleLowerCase();
  const visibleUsers = selectableUsers.filter((candidate) => !attendeeQuery
    || `${candidate.fullName} ${candidate.email} ${candidate.jobTitle ?? ""}`.toLocaleLowerCase().includes(attendeeQuery));
  const externalEmail = attendeeSearch.trim();
  const matchingInternalUser = selectableUsers.some((candidate) => candidate.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());
  const canAddExternal = isValidMeetingEmail(externalEmail)
    && !matchingInternalUser
    && !draft.attendees.some((attendee) => attendee.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());

  React.useEffect(() => {
    if (!open) return;
    setDraft(meeting ? meetingToDraft(meeting) : createMeetingDraft());
    setProjectId(meeting?.projectId ?? lockedProjectId ?? "");
    setAttendeeSearch("");
    setError(null);
  }, [lockedProjectId, meeting, open]);

  function toggleAttendee(person: UserLookupDto) {
    const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
    setDraft((current) => ({ ...current, attendees: selected
      ? current.attendees.filter((attendee) => attendee.userId !== person.id)
      : [...current.attendees, { userId: person.id, email: person.email, displayName: person.fullName }] }));
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
    const body = toMeetingInput(draft, lockedProjectId ?? (projectId || undefined));
    const onSuccess = (saved: MeetingDetailDto) => {
      onOpenChange(false);
      if (meeting) onSaved?.(saved);
      else onCreated?.(saved);
    };
    if (meeting) {
      mutations.update.mutate({ body: { ...body, rowVersion: meeting.rowVersion, status: meeting.status ?? undefined } }, { onSuccess, onError: () => setError(t("meetings.saveError")) });
    } else {
      mutations.create.mutate(body, { onSuccess, onError: () => setError(t("meetings.saveError")) });
    }
  }

  function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    save();
  }

  return <SideSheet open={open} onOpenChange={onOpenChange} title={t(isEdit ? "meetings.editMeeting" : "meetings.createMeeting")} description={t(isEdit ? "meetings.editDescription" : "meetings.createDescription")} size="lg">
    <form onSubmit={submit} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-5">
        <div className="space-y-1.5"><Label htmlFor="meeting-create-title">{t("meetings.titleField")} *</Label><Input id="meeting-create-title" maxLength={500} value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} /></div>
        <div className="space-y-1.5"><Label htmlFor="meeting-create-objective">{t("meetings.objective")}</Label><Textarea id="meeting-create-objective" rows={3} value={draft.objective} onChange={(event) => setDraft({ ...draft, objective: event.target.value })} /></div>
        <div className="space-y-1.5"><Label>{t("meetings.project")}</Label><SearchableSelect options={projectOptions} value={lockedProjectId ?? projectId} onValueChange={setProjectId} placeholder={t("meetings.noProject")} searchPlaceholder={t("meetings.searchProjects")} emptyText={t("meetings.noProjects")} loading={projectsQuery.isLoading} disabled={Boolean(lockedProjectId)} /></div>
        <div className="space-y-1.5"><Label>{t("meetings.organizer")} *</Label><SearchableSelect options={userOptions} value={draft.organizerId} onValueChange={(organizerId) => setDraft({ ...draft, organizerId })} placeholder={t("meetings.selectOrganizer")} searchPlaceholder={t("meetings.searchUsers")} emptyText={t("meetings.noUsers")} loading={usersQuery.isLoading} /></div>
        <div className="space-y-2">
          <Label htmlFor="meeting-create-attendee-search">{t("meetings.attendees")}</Label>
          <Input id="meeting-create-attendee-search" value={attendeeSearch} onChange={(event) => setAttendeeSearch(event.target.value)} placeholder={t("meetings.searchUsers")} />
          {draft.attendees.length > 0 ? <p className="text-xs text-muted-foreground">{t("meetings.selectedUsers", { count: draft.attendees.length })}</p> : null}
          {usersQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("meetings.loadingUsers")}</p> : visibleUsers.length > 0 ? <div className="max-h-36 space-y-1 overflow-y-auto rounded-md border p-1">{visibleUsers.map((person) => {
            const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
            return <button key={person.id} type="button" aria-pressed={selected} onClick={() => toggleAttendee(person)} className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-start text-sm hover:bg-accent"><input type="checkbox" checked={selected} readOnly tabIndex={-1} /><UserRound className="h-3.5 w-3.5 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate">{person.fullName}<span className="ms-1 text-xs text-muted-foreground">{person.email}</span></span></button>;
          })}</div> : <p className="text-xs text-muted-foreground">{t("meetings.noUsers")}</p>}
          {canAddExternal ? <Button type="button" size="sm" variant="outline" onClick={addExternalAttendee}><Mail className="me-1.5 h-3.5 w-3.5" />{t("meetings.addExternalAttendee", { email: externalEmail })}</Button> : null}
          {draft.attendees.length > 0 ? <div className="space-y-2">{draft.attendees.map((attendee) => <div key={attendee.userId ?? attendee.email.toLocaleLowerCase()} className="flex items-start gap-2 rounded-md border bg-muted/20 p-2"><div className="min-w-0 flex-1"><p className="truncate text-xs font-medium">{attendee.displayName || attendee.email}</p><p className="truncate text-xs text-muted-foreground">{t(attendee.userId ? "meetings.internalUser" : "meetings.externalAttendee")} · {attendee.email}</p>{!attendee.userId ? <Input className="mt-1.5 h-8" maxLength={200} value={attendee.displayName ?? ""} onChange={(event) => updateExternalName(attendee.email, event.target.value)} placeholder={t("meetings.optionalDisplayName")} aria-label={t("meetings.optionalDisplayName")} /> : null}</div><Button type="button" size="sm" variant="ghost" aria-label={t("meetings.removeUser", { name: attendee.displayName || attendee.email })} onClick={() => removeAttendee(attendee)}><X className="h-4 w-4" /></Button></div>)}</div> : null}
        </div>
        <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-create-start">{t("meetings.start")} *</Label><Input id="meeting-create-start" type="datetime-local" value={draft.startTime} onChange={(event) => setDraft({ ...draft, startTime: event.target.value })} /></div><div className="space-y-1.5"><Label htmlFor="meeting-create-end">{t("meetings.end")} *</Label><Input id="meeting-create-end" type="datetime-local" value={draft.endTime} onChange={(event) => setDraft({ ...draft, endTime: event.target.value })} /></div></div>
        <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-create-location">{t("meetings.location")}</Label><Input id="meeting-create-location" value={draft.location} onChange={(event) => setDraft({ ...draft, location: event.target.value })} /></div><div className="space-y-1.5"><Label htmlFor="meeting-create-link">{t("meetings.meetingLink")}</Label><Input id="meeting-create-link" type="text" value={draft.meetingLink} onChange={(event) => setDraft({ ...draft, meetingLink: event.target.value })} /></div></div>
        <div className="space-y-1.5"><Label htmlFor="meeting-create-reminder">{t("meetings.reminder")}</Label><select id="meeting-create-reminder" className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={draft.reminderOffsetMinutes} onChange={(event) => setDraft({ ...draft, reminderOffsetMinutes: event.target.value })}>{reminderOptions.map((option) => <option key={option.value || "none"} value={option.value}>{option.label}</option>)}</select></div>
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={mutations.create.isPending || mutations.update.isPending}>{mutations.create.isPending || mutations.update.isPending ? t("common.saving") : t("common.save")}</Button></SideSheetFooter>
    </form>
  </SideSheet>;
}
