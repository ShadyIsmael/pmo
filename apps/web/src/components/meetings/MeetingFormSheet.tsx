"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ProjectPicker } from "@/components/work-items/ProjectPicker";
import { MeetingUserPicker, type MeetingAttendeeSelection } from "@/components/meetings/MeetingUserPicker";
import { useProjects } from "@/hooks/projects/useProjects";
import { useCreateMeeting, useUpdateMeeting } from "@/hooks/meetings/useMeetings";
import type { UserLookupDto } from "@/hooks/projects/useProjectLookups";
import type { CreateMeetingRequest, MeetingDetailDto } from "@/lib/api/meetings";
import { MeetingReminderHelper } from "@/lib/enums";

export interface MeetingFormProjectContext {
  id: string;
  name: string;
  code: string;
}

interface MeetingFormSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  meeting?: MeetingDetailDto;
  onSaved?: (meeting: MeetingDetailDto) => void;
  initialProject?: MeetingFormProjectContext;
  fixedProjectId?: string;
  projectLocked?: boolean;
}

type MeetingFormValues = {
  title: string;
  objective: string;
  projectId: string;
  startTime: string;
  endTime: string;
  reminderOffsetMinutes: string;
  location: string;
  meetingLink: string;
  organizerId: string;
  attendees: MeetingAttendeeSelection[];
};

export function MeetingFormSheet({ open, onOpenChange, meeting, onSaved, initialProject, fixedProjectId, projectLocked = false }: MeetingFormSheetProps) {
  const t = useTranslations("meetings");
  const common = useTranslations("common");
  const isEdit = Boolean(meeting);
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, !projectLocked);
  const createMeeting = useCreateMeeting();
  const updateMeeting = useUpdateMeeting();
  const [formError, setFormError] = React.useState<string | null>(null);
  const schema = React.useMemo(() => z.object({
    title: z.string().trim().min(1, t("validation.titleRequired")).max(500, t("validation.titleTooLong")),
    objective: z.string().optional(),
    projectId: z.string().optional(),
    startTime: z.string().min(1, t("validation.startRequired")),
    endTime: z.string().min(1, t("validation.endRequired")),
    location: z.string().optional(),
    meetingLink: z.string().optional(),
    organizerId: z.string().min(1, t("validation.organizerRequired")),
    attendees: z.array(z.object({ userId: z.string().optional(), email: z.string().trim().email(t("validation.emailInvalid")), displayName: z.string().max(200, t("validation.displayNameTooLong")).optional() })).superRefine((attendees, context) => {
      const keys = new Set<string>();
      attendees.forEach((attendee, index) => {
        const key = attendee.userId ? `user:${attendee.userId}` : `email:${attendee.email.trim().toLocaleLowerCase()}`;
        if (keys.has(key)) context.addIssue({ code: z.ZodIssueCode.custom, path: [index], message: t("validation.duplicateAttendees") });
        keys.add(key);
      });
    }),
  }).superRefine((values, context) => {
    if (values.startTime && values.endTime && new Date(values.endTime).getTime() <= new Date(values.startTime).getTime()) {
      context.addIssue({ code: z.ZodIssueCode.custom, path: ["endTime"], message: t("validation.endAfterStart") });
    }
  }), [t]);
  const form = useForm<MeetingFormValues>({ resolver: zodResolver(schema), defaultValues: getDefaultValues(meeting, fixedProjectId) });

  React.useEffect(() => {
    if (!open) return;
    form.reset(getDefaultValues(meeting, fixedProjectId));
    setFormError(null);
  }, [fixedProjectId, form, meeting, open]);

  const selectedUsers = React.useMemo<UserLookupDto[]>(() => (meeting?.attendees ?? []).filter((attendee) => attendee.userId && attendee.isInternalUser).map((attendee) => ({ id: attendee.userId ?? "", fullName: attendee.displayName ?? attendee.email, email: attendee.email })), [meeting]);
  const selectedOrganizer = React.useMemo<UserLookupDto[]>(() => meeting?.organizerId ? [{ id: meeting.organizerId, fullName: meeting.organizerName ?? "", email: "" }] : [], [meeting]);
  const isSaving = createMeeting.isPending || updateMeeting.isPending;

  async function submit(values: MeetingFormValues) {
    setFormError(null);
    const body: CreateMeetingRequest = {
      title: values.title.trim(),
      objective: values.objective.trim() || undefined,
      projectId: (fixedProjectId ?? values.projectId) || undefined,
      startTime: new Date(values.startTime).toISOString(),
      endTime: new Date(values.endTime).toISOString(),
      reminderOffsetMinutes: MeetingReminderHelper.toValue(values.reminderOffsetMinutes),
      location: values.location.trim() || undefined,
      meetingLink: values.meetingLink.trim() || undefined,
      organizerId: values.organizerId,
      attendees: values.attendees.map((attendee) => attendee.userId ? { userId: attendee.userId } : { email: attendee.email.trim(), displayName: attendee.displayName?.trim() || undefined }),
    };
    try {
      const saved = meeting ? await updateMeeting.mutateAsync({ id: meeting.id, body: { ...body, rowVersion: meeting.rowVersion } }) : await createMeeting.mutateAsync(body);
      onOpenChange(false);
      onSaved?.(saved);
    } catch {
      setFormError(t("saveError"));
    }
  }

  const projectCopy = { noProject: t("noProject"), searchProjects: t("searchProjects"), loading: common("loading"), noProjects: t("noProjects") };

  return (
    <FormSheet open={open} onOpenChange={onOpenChange} title={isEdit ? t("editMeeting") : t("createMeeting")} description={isEdit ? t("editDescription") : t("createDescription")} size="lg" closeLabel={common("close")}>
      <form onSubmit={form.handleSubmit(submit)} className="flex min-h-0 flex-1 flex-col">
        <FormSheetBody className="space-y-5">
          {formError ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{formError}</p> : null}
          <div className="space-y-1.5"><Label htmlFor="meeting-title">{t("titleField")}<span className="ms-1 text-destructive">*</span></Label><Input id="meeting-title" {...form.register("title")} aria-invalid={Boolean(form.formState.errors.title)} />{form.formState.errors.title ? <p className="text-xs text-destructive">{form.formState.errors.title.message}</p> : null}</div>
          <div className="space-y-1.5"><Label htmlFor="meeting-objective">{t("objective")}</Label><Textarea id="meeting-objective" rows={3} {...form.register("objective")} /></div>
          {projectLocked ? <div className="space-y-1.5"><Label>{t("project")}</Label><div className="rounded-md border bg-muted/20 px-3 py-2"><p className="text-sm font-medium">{initialProject?.name ?? t("noProject")}</p>{initialProject?.code ? <p className="mt-0.5 text-xs text-muted-foreground">{initialProject.code}</p> : null}</div></div> : <ProjectPicker id="meeting-project" label={t("project")} value={form.watch("projectId") || undefined} projects={projectsQuery.data?.items ?? []} isLoading={projectsQuery.isLoading} onChange={(value) => form.setValue("projectId", value ?? "")} t={projectCopy} />}
          <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-start">{t("start")}<span className="ms-1 text-destructive">*</span></Label><Input id="meeting-start" type="datetime-local" {...form.register("startTime")} />{form.formState.errors.startTime ? <p className="text-xs text-destructive">{form.formState.errors.startTime.message}</p> : null}</div><div className="space-y-1.5"><Label htmlFor="meeting-end">{t("end")}<span className="ms-1 text-destructive">*</span></Label><Input id="meeting-end" type="datetime-local" {...form.register("endTime")} />{form.formState.errors.endTime ? <p className="text-xs text-destructive">{form.formState.errors.endTime.message}</p> : null}</div></div>
          <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="meeting-location">{t("location")}</Label><Input id="meeting-location" {...form.register("location")} /></div><div className="space-y-1.5"><Label htmlFor="meeting-link">{t("meetingLink")}</Label><Input id="meeting-link" type="text" {...form.register("meetingLink")} /></div></div>
          <div className="space-y-1.5"><Label htmlFor="meeting-reminder">{t("reminder")}</Label><select id="meeting-reminder" className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" {...form.register("reminderOffsetMinutes")}>{MeetingReminderHelper.allOptions().map((option) => <option key={option.value ?? "none"} value={option.value ?? ""}>{t(option.labelKey)}</option>)}</select></div>
          <MeetingUserPicker id="meeting-organizer" label={t("organizer")} value={form.watch("organizerId")} selectedUsers={selectedOrganizer} onChange={(value) => form.setValue("organizerId", value ?? "", { shouldValidate: true })} required error={form.formState.errors.organizerId?.message} />
          <MeetingUserPicker id="meeting-attendees" label={t("attendees")} attendees={form.watch("attendees")} multiple selectedUsers={selectedUsers} onAttendeesChange={(values) => form.setValue("attendees", values, { shouldValidate: true })} error={typeof form.formState.errors.attendees?.message === "string" ? form.formState.errors.attendees.message : undefined} />
        </FormSheetBody>
        <FormSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{common("cancel")}</Button><Button type="submit" disabled={isSaving}>{isSaving ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{common("saving")}</> : common("save")}</Button></FormSheetFooter>
      </form>
    </FormSheet>
  );
}

function getDefaultValues(meeting?: MeetingDetailDto, fixedProjectId?: string): MeetingFormValues {
  return {
    title: meeting?.title ?? "",
    objective: meeting?.objective ?? "",
    projectId: fixedProjectId ?? meeting?.projectId ?? "",
    startTime: meeting ? toDateTimeLocal(meeting.startTime) : toDateTimeLocal(new Date(Date.now() + 60 * 60 * 1000).toISOString()),
    endTime: meeting ? toDateTimeLocal(meeting.endTime) : toDateTimeLocal(new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString()),
    reminderOffsetMinutes: meeting?.reminderOffsetMinutes == null ? "" : String(meeting.reminderOffsetMinutes),
    location: meeting?.location ?? "",
    meetingLink: meeting?.meetingLink ?? "",
    organizerId: meeting?.organizerId ?? "",
    attendees: meeting?.attendees.map((attendee) => ({ userId: attendee.userId ?? undefined, email: attendee.email, displayName: attendee.displayName ?? undefined })) ?? [],
  };
}

function toDateTimeLocal(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (part: number) => String(part).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
