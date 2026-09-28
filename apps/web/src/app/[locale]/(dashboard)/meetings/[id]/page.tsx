"use client";

import * as React from "react";
import dynamic from "next/dynamic";
import { useParams } from "next/navigation";
import {
  AlertTriangle,
  ArrowLeft,
  CalendarCheck,
  CheckCircle2,
  ExternalLink,
  FileText,
  Loader2,
  Pencil,
  Trash2,
  UserRound,
  Users,
  XCircle,
} from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useMeeting, useDeleteMeeting, useUpdateMeeting } from "@/hooks/meetings/useMeetings";
import { ApiError } from "@/lib/api/client";
import type { MeetingDetailDto, UpdateMeetingRequest } from "@/lib/api/meetings";
import { useAuth } from "@/lib/auth";
import { MeetingReminderHelper, MeetingStatusHelper } from "@/lib/enums";
import { hasPermission, Permissions } from "@/lib/permissions";
import { formatDateTime } from "@/lib/utils";

const LazyMeetingFormSheet = dynamic(
  () => import("@/components/meetings/MeetingFormSheet").then((module) => module.MeetingFormSheet),
  { ssr: false },
);

export default function MeetingDetailPage() {
  const params = useParams<{ id: string }>();
  const meetingId = typeof params.id === "string" ? params.id : "";
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("meetings");
  const common = useTranslations("common");
  const { user, status: authStatus } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Edit));
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage));
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Delete));
  const meetingQuery = useMeeting(meetingId);
  const updateMeeting = useUpdateMeeting();
  const deleteMeeting = useDeleteMeeting();
  const [editOpen, setEditOpen] = React.useState(false);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const meeting = meetingQuery.data;
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  async function changeStatus(nextStatus: "Completed" | "Cancelled") {
    if (!meeting) return;
    const body = buildUpdateBody(meeting, nextStatus);
    if (!body) {
      setActionError(t("actionError"));
      return;
    }
    setActionError(null);
    try {
      await updateMeeting.mutateAsync({ id: meeting.id, body });
    } catch {
      setActionError(t("actionError"));
    }
  }

  async function removeMeeting() {
    if (!meeting || !window.confirm(t("deleteConfirm"))) return;
    setActionError(null);
    try {
      await deleteMeeting.mutateAsync(meeting.id);
      router.push("/meetings");
    } catch {
      setActionError(t("actionError"));
    }
  }

  if (authStatus === "authenticated" && !canView) {
    return <PermissionDenied title={t("title")} message={t("permissionDenied")} />;
  }
  if (authStatus !== "authenticated") return null;

  if (meetingQuery.isLoading) return <MeetingDetailSkeleton />;

  if (meetingQuery.isError || !meeting) {
    const status = meetingQuery.error instanceof ApiError ? meetingQuery.error.status : undefined;
    if (status === 403) return <PermissionDenied title={t("title")} message={t("permissionDenied")} />;
    if (status === 404 || !meetingQuery.isError) return <NotFoundState t={t} />;
    return <ErrorState onRetry={() => void meetingQuery.refetch()} t={t} common={common} />;
  }

  const statusLabel = t(MeetingStatusHelper.getLabelKey(meeting.status));
  const isScheduled = meeting.status === "Scheduled";
  const isBusy = updateMeeting.isPending || deleteMeeting.isPending;

  return (
    <div className="space-y-6">
      <PageHeader
        title={meeting.title}
        description={t("details")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild size="sm" variant="outline">
              <Link href="/meetings"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("backToMeetings")}</Link>
            </Button>
            {canEdit ? <Button size="sm" variant="outline" onClick={() => setEditOpen(true)} disabled={isBusy}><Pencil className="me-1.5 h-4 w-4" />{t("editMeeting")}</Button> : null}
            {canEdit && isScheduled ? <>
              <Button size="sm" onClick={() => void changeStatus("Completed")} disabled={isBusy}><CheckCircle2 className="me-1.5 h-4 w-4" />{updateMeeting.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : t("markCompleted")}</Button>
              <Button size="sm" variant="outline" onClick={() => void changeStatus("Cancelled")} disabled={isBusy}><XCircle className="me-1.5 h-4 w-4" />{t("cancelMeeting")}</Button>
            </> : null}
            {canDelete ? <Button size="sm" variant="destructive" onClick={() => void removeMeeting()} disabled={isBusy}><Trash2 className="me-1.5 h-4 w-4" />{t("delete")}</Button> : null}
          </div>
        }
      >
        <Badge status={meeting.status}>{statusLabel}</Badge>
      </PageHeader>

      {actionError ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{actionError}</p> : null}

      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(280px,360px)]">
        <Card>
          <CardHeader><CardTitle>{t("details")}</CardTitle></CardHeader>
          <CardContent className="grid gap-5 sm:grid-cols-2">
            <InfoField label={t("objective")} value={meeting.objective?.trim() || t("noObjective")} wide />
            <InfoField label={t("project")} value={meeting.projectId && meeting.projectName ? <Link className="font-medium text-primary hover:underline" href={`/projects/${encodeURIComponent(meeting.projectId)}`}>{meeting.projectName}</Link> : t("noProject")} />
            <InfoField label={t("start")} value={formatDateTime(meeting.startTime, dateLocale)} />
            <InfoField label={t("end")} value={formatDateTime(meeting.endTime, dateLocale)} />
            <InfoField label={t("duration")} value={formatDuration(meeting.startTime, meeting.endTime, t)} />
            <InfoField label={t("location")} value={meeting.location?.trim() || t("noLocation")} />
            <InfoField label={t("reminder")} value={t(MeetingReminderHelper.getLabelKey(meeting.reminderOffsetMinutes))} />
            <InfoField label={t("meetingLink")} value={meeting.meetingLink ? <a className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline" href={meeting.meetingLink} target="_blank" rel="noreferrer"><ExternalLink className="h-3.5 w-3.5" />{t("openMeetingLink")}</a> : "—"} />
          </CardContent>
        </Card>

        <div className="space-y-5">
          <Card>
            <CardHeader><CardTitle>{t("organizer")}</CardTitle></CardHeader>
            <CardContent>{meeting.organizerName ? <div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-4 w-4" /></div><span className="text-sm font-medium">{meeting.organizerName}</span></div> : <p className="text-sm text-muted-foreground">—</p>}</CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle className="flex items-center gap-2"><Users className="h-4 w-4 text-muted-foreground" />{t("attendees")}<span className="text-xs font-normal text-muted-foreground">({meeting.attendees.length})</span></CardTitle></CardHeader>
            <CardContent>{meeting.attendees.length ? <div className="space-y-3">{meeting.attendees.map((attendee) => <div key={attendee.id} className="flex min-w-0 items-center gap-3"><div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted"><UserRound className="h-3.5 w-3.5 text-muted-foreground" /></div><div className="min-w-0"><div className="flex flex-wrap items-center gap-1.5"><p className="truncate text-sm font-medium">{attendee.displayName?.trim() || attendee.email}</p><span className="rounded-full border px-1.5 py-0.5 text-[9px] font-medium text-muted-foreground">{attendee.isInternalUser ? t("internalUser") : t("externalAttendee")}</span></div><p className="truncate text-xs text-muted-foreground">{attendee.email}</p></div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("noUsers")}</p>}</CardContent>
          </Card>
        </div>
      </div>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2"><CalendarCheck className="h-4 w-4 text-muted-foreground" />{t("minutesTitle")}</CardTitle></CardHeader>
        <CardContent className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-sm font-medium">{meeting.minutesState ? t(`minutes.${meeting.minutesState.toLowerCase()}`) : t("minutes.notAvailable")}</p><p className="mt-1 text-xs text-muted-foreground">{isScheduled ? t("minutesEditorHint") : t("minutesDescription")}</p></div><Button asChild size="sm" variant={canManage && !meeting.minutesState ? "default" : "outline"}><Link href={`/meetings/${meeting.id}/minutes`}>{canManage && !meeting.minutesState ? <Pencil className="me-1.5 h-4 w-4" /> : <FileText className="me-1.5 h-4 w-4" />}{canManage && !meeting.minutesState ? t("enterMinutes") : t("viewMinutes")}</Link></Button></CardContent>
      </Card>

      {editOpen ? <LazyMeetingFormSheet open={editOpen} onOpenChange={setEditOpen} meeting={meeting} /> : null}
    </div>
  );
}

function buildUpdateBody(meeting: MeetingDetailDto, status: "Completed" | "Cancelled"): UpdateMeetingRequest | null {
  if (!meeting.organizerId) return null;
  return {
    title: meeting.title,
    objective: meeting.objective ?? undefined,
    projectId: meeting.projectId ?? undefined,
    startTime: meeting.startTime,
    endTime: meeting.endTime,
    location: meeting.location ?? undefined,
    meetingLink: meeting.meetingLink ?? undefined,
    reminderOffsetMinutes: meeting.reminderOffsetMinutes ?? null,
    organizerId: meeting.organizerId,
    attendees: meeting.attendees.map((attendee) => attendee.userId ? { userId: attendee.userId } : { email: attendee.email, displayName: attendee.displayName ?? undefined }),
    rowVersion: meeting.rowVersion,
    status,
  };
}

function formatDuration(start: string, end: string, t: ReturnType<typeof useTranslations>) {
  const startValue = new Date(start).getTime();
  const endValue = new Date(end).getTime();
  if (!Number.isFinite(startValue) || !Number.isFinite(endValue) || endValue <= startValue) return "—";
  const totalMinutes = Math.max(1, Math.round((endValue - startValue) / 60_000));
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours && minutes) return `${t("hours", { count: hours })} ${t("minutesShort", { count: minutes })}`;
  if (hours) return t("hours", { count: hours });
  return t("minutesShort", { count: minutes });
}

function InfoField({ label, value, wide = false }: { label: string; value: React.ReactNode; wide?: boolean }) {
  return <div className={wide ? "min-w-0 sm:col-span-2" : "min-w-0"}><dt className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium">{value}</dd></div>;
}

function MeetingDetailSkeleton() {
  return <div className="space-y-5"><PageHeader title="" description="" actions={<Skeleton className="h-8 w-32" />} /><Card><CardContent className="h-48 p-5"><Skeleton className="h-full" /></CardContent></Card><div className="grid gap-5 lg:grid-cols-2"><Card><CardContent className="h-56 p-5"><Skeleton className="h-full" /></CardContent></Card><Card><CardContent className="h-56 p-5"><Skeleton className="h-full" /></CardContent></Card></div></div>;
}

function NotFoundState({ t }: { t: ReturnType<typeof useTranslations> }) {
  return <StateCard icon={<AlertTriangle className="h-10 w-10 text-muted-foreground" />} title={t("notFoundTitle")} description={t("notFoundDescription")} action={<Button asChild variant="outline"><Link href="/meetings"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("backToMeetings")}</Link></Button>} />;
}

function ErrorState({ onRetry, t, common }: { onRetry: () => void; t: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations> }) {
  return <StateCard icon={<AlertTriangle className="h-10 w-10 text-destructive" />} title={t("loadError")} description={t("actionError")} action={<Button variant="outline" onClick={onRetry}>{common("retry")}</Button>} />;
}

function PermissionDenied({ title, message }: { title: string; message: string }) {
  return <div className="space-y-6"><PageHeader title={title} /><Card><CardContent className="py-16 text-center text-sm text-muted-foreground">{message}</CardContent></Card></div>;
}

function StateCard({ icon, title, description, action }: { icon: React.ReactNode; title: string; description: string; action: React.ReactNode }) {
  return <div className="space-y-4"><Card><CardContent className="flex min-h-64 flex-col items-center justify-center gap-3 py-12 text-center">{icon}<h2 className="font-medium">{title}</h2><p className="max-w-md text-sm text-muted-foreground">{description}</p>{action}</CardContent></Card></div>;
}
