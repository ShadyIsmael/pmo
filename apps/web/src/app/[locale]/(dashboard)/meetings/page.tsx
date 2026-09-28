"use client";

import * as React from "react";
import dynamic from "next/dynamic";
import { CalendarCheck, Eye, FileText, Filter, Pencil, Plus, Search, Users, X } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState, ErrorState, PermissionDenied } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useProjects, type ProjectSummaryDto } from "@/hooks/projects/useProjects";
import { ProjectPicker } from "@/components/work-items/ProjectPicker";
import { useMeetings } from "@/hooks/meetings/useMeetings";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { MeetingStatusHelper, MEETING_STATUSES } from "@/lib/enums";
import { formatDateTime } from "@/lib/utils";
import { ApiError } from "@/lib/api/client";
import type { MeetingStatus } from "@/lib/api/meetings";
import { TablePagination } from "@/components/ui/table-pagination";

const LazyMeetingFormSheet = dynamic(() => import("@/components/meetings/MeetingFormSheet").then((module) => module.MeetingFormSheet), { ssr: false });

export default function MeetingsPage() {
  const t = useTranslations("meetings");
  const common = useTranslations("common");
  const locale = useLocale();
  const { user, status: authStatus } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create));
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState<MeetingStatus | "all">("all");
  const [projectId, setProjectId] = React.useState("");
  const [dateFrom, setDateFrom] = React.useState("");
  const [dateTo, setDateTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [formOpen, setFormOpen] = React.useState(false);
  const [createProject, setCreateProject] = React.useState<ProjectSummaryDto | undefined>();
  const deferredSearch = React.useDeferredValue(search);
  const canQuery = authStatus === "authenticated" && canView;
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, canQuery);
  const filters = React.useMemo(() => ({
    page,
    pageSize: 20,
    search: deferredSearch.trim() || undefined,
    status: status === "all" ? undefined : status,
    projectId: projectId || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo ? `${dateTo}T23:59:59` : undefined,
  }), [dateFrom, dateTo, deferredSearch, page, projectId, status]);
  const meetingsQuery = useMeetings(filters, canQuery);
  const projects = projectsQuery.data?.items ?? [];
  const selectedProject = projects.find((project) => project.id === projectId);
  const meetings = meetingsQuery.data?.items ?? [];
  const totalPages = meetingsQuery.data?.totalPages ?? 0;

  if (authStatus === "authenticated" && !canView) return <PermissionDenied title={t("title")} description={t("permissionDenied")} />;
  if (authStatus !== "authenticated") return null;

  function isMeetingStatus(value: string): value is MeetingStatus | "all" {
    return value === "all" || (MEETING_STATUSES as readonly string[]).includes(value);
  }

  function openCreateMeeting() {
    setCreateProject(selectedProject);
    setFormOpen(true);
  }

  function handleFormOpenChange(open: boolean) {
    setFormOpen(open);
    if (!open) setCreateProject(undefined);
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t("title")} description={t("description", { count: meetingsQuery.data?.totalCount ?? 0 })} actions={canCreate ? <Button size="sm" onClick={openCreateMeeting}><Plus className="me-2 h-4 w-4" />{t("createMeeting")}</Button> : null} />

      <Card>
        <CardContent className="space-y-3 p-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
            <div className="w-full lg:w-60"><ProjectPicker id="meeting-project" label={common("project")} value={projectId || undefined} projects={projects} isLoading={projectsQuery.isLoading} onChange={(value) => { setPage(1); setProjectId(value ?? ""); }} emptyLabel={t("allProjects")} projectNameFirst t={{ noProject: t("allProjects"), searchProjects: t("searchProjects"), noProjects: t("noProjects"), loading: common("loading") }} /></div>
            <div className="w-full space-y-1.5 lg:w-48"><label htmlFor="meeting-status" className="text-xs font-medium text-muted-foreground">{common("status")}</label><Select value={status} onValueChange={(value) => { if (isMeetingStatus(value)) { setPage(1); setStatus(value); } }}><SelectTrigger id="meeting-status"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{common("all")}</SelectItem>{MEETING_STATUSES.map((value) => <SelectItem key={value} value={value}>{t(MeetingStatusHelper.getLabelKey(value))}</SelectItem>)}</SelectContent></Select></div>
            <div className="min-w-0 flex-1 space-y-1.5"><label htmlFor="meeting-search" className="text-xs font-medium text-muted-foreground">{common("search")}</label><div className="relative"><Search className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input id="meeting-search" value={search} onChange={(event) => { setPage(1); setSearch(event.target.value); }} placeholder={t("searchPlaceholder")} className="ps-9" /></div></div>
            <div className="grid w-full grid-cols-2 gap-2 lg:w-64"><div className="space-y-1.5"><label htmlFor="meeting-date-from" className="text-xs font-medium text-muted-foreground">{t("from")}</label><Input id="meeting-date-from" type="date" value={dateFrom} onChange={(event) => { setPage(1); setDateFrom(event.target.value); }} /></div><div className="space-y-1.5"><label htmlFor="meeting-date-to" className="text-xs font-medium text-muted-foreground">{t("to")}</label><Input id="meeting-date-to" type="date" value={dateTo} onChange={(event) => { setPage(1); setDateTo(event.target.value); }} /></div></div>
            <Button type="button" variant="ghost" className="self-end" onClick={() => { setPage(1); setSearch(""); setStatus("all"); setProjectId(""); setDateFrom(""); setDateTo(""); }}><Filter className="me-2 h-4 w-4" />{t("clearFilters")}</Button>
          </div>
          {selectedProject ? <div className="flex flex-wrap items-center gap-2 border-t pt-3 text-sm"><span className="text-muted-foreground">{t("project")}:</span><span className="font-medium">{selectedProject.name}</span><span className="text-xs text-muted-foreground">{selectedProject.code}</span><Button type="button" variant="ghost" size="sm" className="ms-auto h-7 gap-1 px-2 text-muted-foreground" onClick={() => { setPage(1); setProjectId(""); }}><X className="h-3.5 w-3.5" />{t("clearProject")}</Button></div> : null}
        </CardContent>
      </Card>

      {meetingsQuery.isLoading ? <MeetingListSkeleton /> : meetingsQuery.isError ? <ErrorState title={meetingsQuery.error instanceof ApiError && meetingsQuery.error.status === 403 ? t("permissionDenied") : t("loadError")} onRetry={() => void meetingsQuery.refetch()} /> : meetings.length === 0 ? <EmptyState title={t("emptyTitle")} description={t("emptyDescription")} icon={CalendarCheck} /> : <MeetingTable meetings={meetings} locale={locale} t={t} common={common} canManage={Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage))} showProject={!projectId} />}

      {!meetingsQuery.isLoading && !meetingsQuery.isError && meetings.length > 0 ? <TablePagination page={page} totalPages={totalPages} summary={t("pageSummary", { page, totalPages, total: meetingsQuery.data?.totalCount ?? 0 })} previousLabel={t("previous")} nextLabel={t("next")} onPageChange={setPage} /> : null}

      {formOpen ? <LazyMeetingFormSheet open={formOpen} onOpenChange={handleFormOpenChange} fixedProjectId={createProject?.id} initialProject={createProject ? { id: createProject.id, name: createProject.name, code: createProject.code } : undefined} projectLocked={Boolean(createProject)} /> : null}
    </div>
  );
}

function MeetingTable({ meetings, locale, t, common, canManage, showProject }: { meetings: readonly import("@/lib/api/meetings").MeetingListDto[]; locale: string; t: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations>; canManage: boolean; showProject: boolean }) {
  return <Table><TableHeader><TableRow><TableHead>{t("columns.meeting")}</TableHead>{showProject ? <TableHead>{common("project")}</TableHead> : null}<TableHead>{t("organizer")}</TableHead><TableHead>{t("dateTime")}</TableHead><TableHead>{t("attendees")}</TableHead><TableHead>{common("status")}</TableHead><TableHead>{t("minutesTitle")}</TableHead><TableHead className="text-end">{common("actions")}</TableHead></TableRow></TableHeader><TableBody>{meetings.map((meeting) => { const hasMinutes = Boolean(meeting.minutesState); const minutesActionLabel = hasMinutes || !canManage ? t("viewMinutes") : t("enterMinutes"); return <TableRow key={meeting.id} className="group"><TableCell className="min-w-[16rem]"><Link href={`/meetings/${meeting.id}`} className="block min-w-0"><p className="truncate font-semibold underline-offset-4 group-hover:text-primary group-hover:underline">{meeting.title}</p><p className="mt-1 truncate text-[11px] text-muted-foreground">{hasMinutes ? t(`minutes.${meeting.minutesState!.toLowerCase()}`) : t("minutes.notAvailable")}</p></Link></TableCell>{showProject ? <TableCell className="max-w-[14rem]"><span className="block truncate text-muted-foreground">{meeting.projectName ?? t("notAvailable")}</span></TableCell> : null}<TableCell className="max-w-[12rem]"><span className="block truncate text-muted-foreground">{meeting.organizerName ?? t("notAvailable")}</span></TableCell><TableCell className="whitespace-nowrap"><span className="flex items-center gap-1.5 text-muted-foreground"><CalendarCheck className="h-3.5 w-3.5 shrink-0" />{formatDateTime(meeting.startTime, locale)}</span></TableCell><TableCell><span className="inline-flex items-center gap-1.5 text-muted-foreground"><Users className="h-3.5 w-3.5" />{meeting.attendeeCount}</span></TableCell><TableCell><Badge status={meeting.status}>{meeting.status ? t(MeetingStatusHelper.getLabelKey(meeting.status)) : t("status.unknown")}</Badge></TableCell><TableCell><Badge status={meeting.minutesState}>{hasMinutes ? t(`minutes.${meeting.minutesState!.toLowerCase()}`) : t("minutes.notAvailable")}</Badge></TableCell><TableCell className="text-end"><div className="nexus-table-actions flex justify-end gap-1"><Button asChild size="icon" variant="ghost" aria-label={t("viewMeeting")} title={t("viewMeeting")}><Link href={`/meetings/${meeting.id}`}><Eye className="h-4 w-4" /></Link></Button><Button asChild size="sm" variant={canManage && !hasMinutes ? "default" : "outline"} className="gap-1.5"><Link href={`/meetings/${meeting.id}/minutes`}>{canManage && !hasMinutes ? <Pencil className="h-3.5 w-3.5" /> : <FileText className="h-3.5 w-3.5" />}<span className="hidden xl:inline">{minutesActionLabel}</span><span className="sr-only xl:hidden">{minutesActionLabel}</span></Link></Button></div></TableCell></TableRow>; })}</TableBody></Table>;
}

function MeetingListSkeleton() { return <div className="space-y-2">{[1, 2, 3, 4].map((value) => <Skeleton key={value} className="h-20 w-full" />)}</div>; }
