"use client";

import * as React from "react";
import dynamic from "next/dynamic";
import { CalendarCheck, ExternalLink, Plus, Search, Users } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/navigation";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { useMeetings } from "@/hooks/meetings/useMeetings";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { MEETING_STATUSES, MeetingStatusHelper } from "@/lib/enums";
import { ApiError } from "@/lib/api/client";
import type { MeetingFilters, MeetingListDto, MeetingStatus } from "@/lib/api/meetings";
import { formatDateTime } from "@/lib/utils";
import { TablePagination } from "@/components/ui/table-pagination";

const LazyMeetingFormSheet = dynamic(
  () => import("@/components/meetings/MeetingFormSheet").then((module) => module.MeetingFormSheet),
  { ssr: false },
);

interface ProjectMeetingsTabProps {
  projectId: string;
  project: { id: string; name: string; code: string };
  enabled: boolean;
}

export function ProjectMeetingsTab({ projectId, project, enabled }: ProjectMeetingsTabProps) {
  const t = useTranslations("projects");
  const meetingT = useTranslations("meetings");
  const common = useTranslations("common");
  const locale = useLocale();
  const { user, status: authStatus } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create));
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState<MeetingStatus | "all">("all");
  const [page, setPage] = React.useState(1);
  const [formOpen, setFormOpen] = React.useState(false);
  const deferredSearch = React.useDeferredValue(search);
  const filters = React.useMemo<MeetingFilters>(() => ({
    page,
    pageSize: 10,
    search: deferredSearch.trim() || undefined,
    status: status === "all" ? undefined : status,
    projectId,
  }), [deferredSearch, page, projectId, status]);
  const meetingsQuery = useMeetings(filters, Boolean(projectId) && enabled && authStatus === "authenticated" && canView);
  const meetings = meetingsQuery.data?.items ?? [];
  const totalPages = meetingsQuery.data?.totalPages ?? 0;

  React.useEffect(() => {
    setPage(1);
  }, [deferredSearch, status]);

  if (authStatus === "loading") return <ProjectMeetingsSkeleton />;
  if (!canView) return <ProjectMeetingsState message={t("projectMeetings.permissionDenied")} />;

  function isMeetingStatus(value: string): value is MeetingStatus | "all" {
    return value === "all" || MEETING_STATUSES.some((meetingStatus) => meetingStatus === value);
  }

  return (
    <Card className="shadow-none">
      <CardHeader className="gap-3">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="text-sm font-semibold">{t("projectMeetings.title")}</h2>
            <p className="mt-1 text-xs text-muted-foreground">{t("projectMeetings.description")}</p>
          </div>
          {canCreate ? <Button size="sm" onClick={() => setFormOpen(true)}><Plus className="me-1.5 h-3.5 w-3.5" />{meetingT("createMeeting")}</Button> : null}
        </div>
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
          <div className="min-w-0 flex-1 space-y-1.5">
            <label htmlFor="project-meetings-search" className="text-xs font-medium text-muted-foreground">{common("search")}</label>
            <div className="relative"><Search className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input id="project-meetings-search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder={meetingT("searchPlaceholder")} className="ps-9" /></div>
          </div>
          <div className="w-full space-y-1.5 sm:w-48">
            <label htmlFor="project-meetings-status" className="text-xs font-medium text-muted-foreground">{common("status")}</label>
            <Select value={status} onValueChange={(value) => { if (isMeetingStatus(value)) setStatus(value); }}>
              <SelectTrigger id="project-meetings-status"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="all">{common("all")}</SelectItem>{MEETING_STATUSES.map((value) => <SelectItem key={value} value={value}>{meetingT(MeetingStatusHelper.getLabelKey(value))}</SelectItem>)}</SelectContent>
            </Select>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {meetingsQuery.isLoading ? <ProjectMeetingsSkeleton /> : meetingsQuery.isError ? <ProjectMeetingsState message={meetingsQuery.error instanceof ApiError && meetingsQuery.error.status === 403 ? t("projectMeetings.permissionDenied") : t("projectMeetings.loadError")} action={<Button variant="outline" onClick={() => void meetingsQuery.refetch()}>{common("retry")}</Button>} /> : meetings.length === 0 ? <ProjectMeetingsState message={t("projectMeetings.emptyDescription")} title={t("projectMeetings.emptyTitle")} icon /> : <ProjectMeetingRows meetings={meetings} locale={locale} meetingT={meetingT} />}
        {!meetingsQuery.isLoading && !meetingsQuery.isError && meetings.length > 0 && totalPages > 1 ? <TablePagination className="mt-4" page={page} totalPages={totalPages} summary={meetingT("pageSummary", { page, totalPages, total: meetingsQuery.data?.totalCount ?? 0 })} previousLabel={meetingT("previous")} nextLabel={meetingT("next")} onPageChange={setPage} /> : null}
      </CardContent>
      {formOpen ? <LazyMeetingFormSheet open={formOpen} onOpenChange={setFormOpen} fixedProjectId={projectId} initialProject={project} projectLocked /> : null}
    </Card>
  );
}

function ProjectMeetingRows({ meetings, locale, meetingT }: { meetings: readonly MeetingListDto[]; locale: string; meetingT: ReturnType<typeof useTranslations> }) {
  return <div className="divide-y rounded-md border">{meetings.map((meeting) => <Link key={meeting.id} href={`/meetings/${encodeURIComponent(meeting.id)}`} className="group flex items-center gap-3 px-3 py-3 hover:bg-muted/30"><div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><CalendarCheck className="h-4 w-4" /></div><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium group-hover:text-primary">{meeting.title}</p><div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground"><span>{formatDateTime(meeting.startTime, locale)}</span><span aria-hidden="true">·</span><span>{meeting.organizerName ?? "—"}</span><span aria-hidden="true">·</span><span className="inline-flex items-center gap-1"><Users className="h-3 w-3" />{meeting.attendeeCount}</span></div></div><Badge status={meeting.status}>{meeting.status ? meetingT(MeetingStatusHelper.getLabelKey(meeting.status)) : meetingT("status.unknown")}</Badge><ExternalLink className="h-4 w-4 shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100" /></Link>)}</div>;
}

function ProjectMeetingsSkeleton() {
  return <div className="space-y-2">{[1, 2, 3].map((value) => <Skeleton key={value} className="h-16 w-full" />)}</div>;
}

function ProjectMeetingsState({ message, title, action, icon = false }: { message: string; title?: string; action?: React.ReactNode; icon?: boolean }) {
  return <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 py-8 text-center">{icon ? <CalendarCheck className="h-8 w-8 text-muted-foreground/50" /> : null}{title ? <p className="mt-2 text-sm font-medium">{title}</p> : null}<p className="mt-1 max-w-md text-xs text-muted-foreground">{message}</p>{action ? <div className="mt-3">{action}</div> : null}</div>;
}
