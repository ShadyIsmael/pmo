import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { endOfDay, startOfDay } from "date-fns";
import { CalendarDays, Filter, List, Search } from "lucide-react";
import { useDebounce } from "use-debounce";
import { Button } from "@components/ui/Button";
import { Card, CardContent } from "@components/ui/Card";
import { Checkbox } from "@components/ui/Checkbox";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Pagination } from "@components/ui/Pagination";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Skeleton } from "@components/ui/Skeleton";
import { Tabs, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { ApiError } from "@api/client";
import { useFullMeetings, useMeetingCalendar, useMeetingProjectOptions } from "@api/meetingsFull";
import { useI18n } from "@i18n/messages";
import { cn } from "@lib/utils";
import { MeetingCalendarView } from "./MeetingCalendarView";
import type { ProjectFilterOption } from "./ProjectMultiSelect";
import { MeetingsTable, MEETINGS_PAGE_SIZE, MEETING_STATUSES, isMeetingStatus, type MeetingStatus } from "./MeetingsTable";
import { getMeetingStatusLabel } from "./meetings-labels";
import {
  calendarRange,
  parseCalendarDate,
  toCalendarDateValue,
  type CalendarMode,
} from "./meetings-calendar";

export interface LockedProject {
  publicId: string;
  name: string;
  code?: string | null;
}

interface MeetingsBrowserProps {
  lockedProject?: LockedProject;
  pageSize?: number;
  canView: boolean;
  canManageMinutes: boolean;
  authLoading?: boolean;
  permissionDeniedText: string;
  emptyTitle: string;
  emptyDescription: string;
  className?: string;
}

function toUtcDayStart(value: string | null): string | undefined {
  if (!value) return undefined;
  const date = new Date(`${value}T00:00:00`);
  return Number.isFinite(date.getTime()) ? startOfDay(date).toISOString() : undefined;
}

function toUtcDayEnd(value: string | null): string | undefined {
  if (!value) return undefined;
  const date = new Date(`${value}T00:00:00`);
  return Number.isFinite(date.getTime()) ? endOfDay(date).toISOString() : undefined;
}

function projectQueryParams(ids: readonly string[]): Record<string, string | undefined> {
  if (ids.length === 0) return {};
  return { projectIds: ids.join(",") };
}

function onlySelectedProjectMeetings<T extends { projectPublicId?: string | null }>(items: readonly T[], selectedProjectIds: readonly string[]): T[] {
  const selected = new Set(selectedProjectIds);
  return items.filter((item) => Boolean(item.projectPublicId && selected.has(item.projectPublicId)));
}

interface CalendarFiltersPanelProps {
  options: readonly ProjectFilterOption[];
  selectedIds: readonly string[];
  lockedProject?: LockedProject;
  optionsLoading: boolean;
  optionsError: boolean;
  onRetryProjects: () => void;
  onProjectChange: (ids: string[]) => void;
  status: MeetingStatus | "all";
  search: string;
  dateFrom: string | null;
  dateTo: string | null;
  onParamsChange: (patch: Record<string, string | undefined>) => void;
}

function MeetingsFiltersPanel({
  options,
  selectedIds,
  lockedProject,
  optionsLoading,
  optionsError,
  onRetryProjects,
  onProjectChange,
  status,
  search,
  dateFrom,
  dateTo,
  onParamsChange,
}: CalendarFiltersPanelProps) {
  const { t } = useI18n();
  const [projectSearch, setProjectSearch] = React.useState("");
  const selected = React.useMemo(() => new Set(selectedIds), [selectedIds]);
  const term = projectSearch.trim().toLocaleLowerCase();
  const filteredOptions = React.useMemo(() => term
    ? options.filter((option) => option.name.toLocaleLowerCase().includes(term) || (option.code ?? "").toLocaleLowerCase().includes(term))
    : options, [options, term]);
  const visibleOptions = filteredOptions.slice(0, 200);

  return (
    <Card className="min-w-0 lg:sticky lg:top-4 lg:self-start">
      <CardContent className="space-y-4 p-4">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-sm font-semibold">{t("meetings.filters")}</h2>
          <Button type="button" variant="ghost" size="sm" onClick={() => onParamsChange({ q: undefined, status: undefined, projects: undefined, from: undefined, to: undefined })}>
            <Filter className="me-1.5 h-4 w-4" aria-hidden="true" />{t("meetings.clearFilters")}
          </Button>
        </div>

        <section className="space-y-2 border-t pt-3" aria-labelledby="calendar-project-filter-label">
          <div className="flex items-center justify-between gap-2">
            <h3 id="calendar-project-filter-label" className="text-sm font-medium">{t("meetings.projectFilter.label")}</h3>
            {!lockedProject ? (
              <div className="flex gap-2 text-xs">
                <button type="button" className="font-medium text-primary hover:underline" disabled={options.length === 0} onClick={() => onProjectChange(options.map((option) => option.publicId))}>{t("meetings.projectFilter.selectAll")}</button>
                <button type="button" className="text-muted-foreground hover:text-foreground" disabled={selectedIds.length === 0} onClick={() => onProjectChange([])}>{t("meetings.projectFilter.clearAll")}</button>
              </div>
            ) : null}
          </div>
          {lockedProject ? (
            <label className="flex items-center gap-2 rounded-md border bg-muted/30 px-2.5 py-2 text-sm">
              <Checkbox checked disabled aria-label={lockedProject.name} />
              <span className="min-w-0 flex-1 truncate font-medium">{lockedProject.name}</span>
              {lockedProject.code ? <span className="shrink-0 text-xs text-muted-foreground">{lockedProject.code}</span> : null}
            </label>
          ) : (
            <>
              <Input value={projectSearch} onChange={(event) => setProjectSearch(event.target.value)} placeholder={t("meetings.searchProjects")} aria-label={t("meetings.searchProjects")} />
              {optionsError ? (
                <div className="flex flex-wrap items-center gap-2 rounded-md border border-destructive/25 bg-destructive/5 p-2 text-xs text-destructive">
                  <span>{t("meetings.projectsLoadError")}</span>
                  <Button type="button" variant="outline" size="sm" onClick={onRetryProjects}>{t("common.retry")}</Button>
                </div>
              ) : optionsLoading ? (
                <p className="py-4 text-center text-xs text-muted-foreground">{t("common.loading")}</p>
              ) : filteredOptions.length === 0 ? (
                <p className="py-4 text-center text-xs text-muted-foreground">{t("meetings.noProjects")}</p>
              ) : (
                <div className="max-h-64 space-y-1 overflow-y-auto pe-1" aria-label={t("meetings.projectFilter.label")}>
                  {visibleOptions.map((option) => (
                    <label key={option.publicId} className="flex min-w-0 cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-muted/60">
                      <Checkbox
                        checked={selected.has(option.publicId)}
                        onCheckedChange={() => onProjectChange(selected.has(option.publicId) ? selectedIds.filter((id) => id !== option.publicId) : [...selectedIds, option.publicId])}
                        aria-label={option.name}
                      />
                      <span className="min-w-0 flex-1 truncate">{option.name}</span>
                      {option.code ? <span className="shrink-0 text-[11px] text-muted-foreground">{option.code}</span> : null}
                    </label>
                  ))}
                  {filteredOptions.length > visibleOptions.length ? <p className="px-2 py-2 text-center text-xs text-muted-foreground">{t("meetings.projectFilter.refineSearch", { count: filteredOptions.length })}</p> : null}
                </div>
              )}
            </>
          )}
        </section>

        <section className="space-y-1.5 border-t pt-3">
          <Label htmlFor="calendar-meeting-search">{t("common.search")}</Label>
          <div className="relative">
            <Search className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" aria-hidden="true" />
            <Input id="calendar-meeting-search" value={search} onChange={(event) => onParamsChange({ q: event.target.value || undefined })} placeholder={t("meetings.searchPlaceholder")} className="ps-9" />
          </div>
        </section>

        <section className="space-y-1.5 border-t pt-3">
          <Label htmlFor="calendar-meeting-status">{t("common.status")}</Label>
          <Select value={status} onValueChange={(value) => { if (isMeetingStatus(value)) onParamsChange({ status: value === "all" ? undefined : value }); }}>
            <SelectTrigger id="calendar-meeting-status"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("common.all")}</SelectItem>
              {MEETING_STATUSES.map((value) => <SelectItem key={value} value={value}>{getMeetingStatusLabel(value, t)}</SelectItem>)}
            </SelectContent>
          </Select>
        </section>

        <section className="grid grid-cols-2 gap-2 border-t pt-3">
          <div className="min-w-0 space-y-1.5">
            <Label htmlFor="calendar-date-from">{t("meetings.from")}</Label>
            <Input id="calendar-date-from" type="date" value={dateFrom ?? ""} onChange={(event) => onParamsChange({ from: event.target.value || undefined })} />
          </div>
          <div className="min-w-0 space-y-1.5">
            <Label htmlFor="calendar-date-to">{t("meetings.to")}</Label>
            <Input id="calendar-date-to" type="date" value={dateTo ?? ""} onChange={(event) => onParamsChange({ to: event.target.value || undefined })} />
          </div>
        </section>
      </CardContent>
    </Card>
  );
}

export function MeetingsBrowser({
  lockedProject,
  pageSize = MEETINGS_PAGE_SIZE,
  canView,
  canManageMinutes,
  authLoading = false,
  permissionDeniedText,
  emptyTitle,
  emptyDescription,
  className,
}: MeetingsBrowserProps) {
  const { t, formatDate } = useI18n();
  const [params, setParams] = useSearchParams();

  const view = params.get("view") === "calendar" ? "calendar" : "list";
  const modeParam = params.get("mode");
  const mode: CalendarMode = modeParam === "week" || modeParam === "day" ? modeParam : "month";
  const dateParam = params.get("date");
  const anchor = React.useMemo(() => parseCalendarDate(dateParam) ?? startOfDay(new Date()), [dateParam]);
  const search = params.get("q") ?? "";
  const [debouncedSearch] = useDebounce(search.trim(), 350);
  const statusParam = params.get("status") ?? "all";
  const status: MeetingStatus | "all" = isMeetingStatus(statusParam) ? statusParam : "all";
  const dateFrom = params.get("from");
  const dateTo = params.get("to");
  const pageValue = Number(params.get("page") ?? "1");
  const page = Number.isFinite(pageValue) && pageValue >= 1 ? Math.floor(pageValue) : 1;

  const updateParams = React.useCallback((patch: Record<string, string | undefined>, resetPage = true) => {
    setParams((current) => {
      const next = new URLSearchParams(current);
      for (const [key, value] of Object.entries(patch)) {
        if (value === undefined || value === "") next.delete(key);
        else next.set(key, value);
      }
      if (resetPage) next.delete("page");
      return next;
    }, { replace: true });
  }, [setParams]);

  const optionsQuery = useMeetingProjectOptions({}, canView && !lockedProject);
  const options = React.useMemo(
    () => lockedProject ? [{ publicId: lockedProject.publicId, name: lockedProject.name, code: lockedProject.code }] : optionsQuery.data ?? [],
    [lockedProject, optionsQuery.data],
  );
  const projectParam = params.get("projects");
  const parsedSelected = React.useMemo(
    () => projectParam && projectParam !== "none" ? projectParam.split(",").map((value) => value.trim()).filter(Boolean) : [],
    [projectParam],
  );
  const allProjectIds = React.useMemo(() => options.map((option) => option.publicId), [options]);
  const selectedIds = React.useMemo(() => {
    if (lockedProject) return allProjectIds;
    if (projectParam === null) return allProjectIds;
    if (projectParam === "none") return [];
    if (allProjectIds.length === 0) return parsedSelected;
    return parsedSelected.filter((id) => allProjectIds.includes(id));
  }, [allProjectIds, lockedProject, parsedSelected, projectParam]);
  const filterIds = React.useMemo(() => {
    if (lockedProject) return allProjectIds;
    if (projectParam === "none") return [];
    if (projectParam === null) return allProjectIds;
    if (allProjectIds.length === 0) return parsedSelected;
    return parsedSelected.filter((id) => allProjectIds.includes(id));
  }, [allProjectIds, lockedProject, parsedSelected, projectParam]);
  const noSelection = !lockedProject && projectParam === "none";
  const noMatchingProjects = !lockedProject && projectParam !== null && projectParam !== "none" && parsedSelected.length > 0 && allProjectIds.length > 0 && filterIds.length === 0;
  const projectFilterReady = Boolean(lockedProject) || (optionsQuery.isSuccess && allProjectIds.length > 0);
  const queryEnabled = canView && projectFilterReady && !noSelection && !noMatchingProjects;

  const listParams = React.useMemo(() => ({
    page,
    pageSize,
    search: debouncedSearch || undefined,
    status: status === "all" ? undefined : status,
    ...projectQueryParams(filterIds),
    dateFrom: toUtcDayStart(dateFrom),
    dateTo: toUtcDayEnd(dateTo),
  }), [dateFrom, dateTo, debouncedSearch, filterIds, page, pageSize, status]);
  const range = React.useMemo(() => calendarRange(mode, anchor), [anchor, mode]);
  const calendarBounds = React.useMemo(() => {
    const fromFilter = toUtcDayStart(dateFrom);
    const toFilter = toUtcDayEnd(dateTo);
    const from = fromFilter ? new Date(Math.max(range.from.getTime(), new Date(fromFilter).getTime())) : range.from;
    const to = toFilter ? new Date(Math.min(range.to.getTime(), new Date(toFilter).getTime())) : range.to;
    return { from, to, isValid: from < to };
  }, [dateFrom, dateTo, range.from, range.to]);
  const calendarParams = React.useMemo(() => ({
    from: calendarBounds.from.toISOString(),
    to: calendarBounds.to.toISOString(),
    search: debouncedSearch || undefined,
    status: status === "all" ? undefined : status,
    ...projectQueryParams(filterIds),
  }), [calendarBounds.from, calendarBounds.to, debouncedSearch, filterIds, status]);

  const listQuery = useFullMeetings(listParams, queryEnabled && view === "list");
  const calendarQuery = useMeetingCalendar(calendarParams, queryEnabled && view === "calendar" && calendarBounds.isValid);

  const meetings = React.useMemo(
    () => onlySelectedProjectMeetings(listQuery.data?.items ?? [], selectedIds),
    [listQuery.data?.items, selectedIds],
  );
  const calendarMeetings = React.useMemo(
    () => onlySelectedProjectMeetings(calendarQuery.data?.items ?? [], selectedIds),
    [calendarQuery.data?.items, selectedIds],
  );
  if (authLoading) {
    return <div className={cn("space-y-2", className)}>{[0, 1, 2].map((item) => <Skeleton key={item} className="h-16 w-full" />)}</div>;
  }

  if (!canView) return <p role="alert" className="py-8 text-center text-sm text-destructive">{permissionDeniedText}</p>;

  const totalPages = listQuery.data?.totalPages ?? Math.ceil((listQuery.data?.totalCount ?? 0) / pageSize);
  const handleProjectChange = (next: string[]) => {
    if (options.length > 0 && next.length === options.length) updateParams({ projects: undefined });
    else if (next.length === 0) updateParams({ projects: "none" });
    else updateParams({ projects: next.join(",") });
  };
  const filtersPanel = (
    <MeetingsFiltersPanel
      options={options}
      selectedIds={selectedIds}
      lockedProject={lockedProject}
      optionsLoading={optionsQuery.isLoading}
      optionsError={optionsQuery.isError}
      onRetryProjects={() => void optionsQuery.refetch()}
      onProjectChange={handleProjectChange}
      status={status}
      search={search}
      dateFrom={dateFrom}
      dateTo={dateTo}
      onParamsChange={(patch) => updateParams(patch)}
    />
  );

  return (
    <div className={cn("space-y-3", className)}>
      <div className="flex flex-wrap items-center gap-2">
        <Tabs value={view} onValueChange={(value) => updateParams({ view: value === "calendar" ? "calendar" : undefined }, false)}>
          <TabsList aria-label={t("meetings.view.label")}>
            <TabsTrigger value="calendar"><CalendarDays className="me-1.5 h-4 w-4" aria-hidden="true" />{t("meetings.view.calendar")}</TabsTrigger>
            <TabsTrigger value="list"><List className="me-1.5 h-4 w-4" aria-hidden="true" />{t("meetings.view.list")}</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>

      {view === "calendar" ? (
        <MeetingCalendarView
          mode={mode}
          anchor={anchor}
          meetings={calendarMeetings}
          isLoading={calendarQuery.isLoading || (!lockedProject && optionsQuery.isLoading)}
          isError={calendarQuery.isError || (!lockedProject && optionsQuery.isError)}
          errorText={!lockedProject && optionsQuery.isError ? t("meetings.projectsLoadError") : undefined}
          isTruncated={calendarQuery.data?.isTruncated}
          totalCount={calendarQuery.data?.totalCount}
          filtersPanel={filtersPanel}
          onModeChange={(nextMode) => updateParams({ view: "calendar", mode: nextMode === "month" ? undefined : nextMode }, false)}
          onAnchorChange={(date) => updateParams({ date: toCalendarDateValue(date) }, false)}
          onDayClick={(date) => updateParams({ view: "calendar", mode: "day", date: toCalendarDateValue(date) }, false)}
          onRetry={() => void (!lockedProject && optionsQuery.isError ? optionsQuery.refetch() : calendarQuery.refetch())}
        />
      ) : (
        <div className="grid min-w-0 gap-3 lg:grid-cols-[17rem_minmax(0,1fr)]">
          {filtersPanel}
          <div className="min-w-0 space-y-3">
            {noSelection || noMatchingProjects ? (
              <Card><CardContent className="flex flex-col items-center gap-2 p-8 text-center">
                <CalendarDays className="h-8 w-8 text-muted-foreground/60" aria-hidden="true" />
                <p className="font-medium">{noMatchingProjects ? t("meetings.projectFilter.noMatchesTitle") : t("meetings.projectFilter.noneSelectedTitle")}</p>
                <p className="text-sm text-muted-foreground">{noMatchingProjects ? t("meetings.projectFilter.noMatchesDescription") : t("meetings.projectFilter.noneSelectedDescription")}</p>
                <Button type="button" variant="outline" size="sm" onClick={() => updateParams({ projects: undefined })}>{t("meetings.projectFilter.selectAll")}</Button>
              </CardContent></Card>
            ) : !lockedProject && optionsQuery.isError ? (
              <div role="alert" className="flex flex-wrap items-center justify-center gap-3 rounded-lg border border-destructive/25 bg-destructive/5 p-6 text-sm text-destructive"><span>{t("meetings.projectsLoadError")}</span><Button type="button" variant="outline" size="sm" onClick={() => void optionsQuery.refetch()}>{t("common.retry")}</Button></div>
            ) : (!lockedProject && optionsQuery.isLoading) || listQuery.isLoading ? (
              <div className="space-y-2">{[1, 2, 3, 4].map((row) => <Skeleton key={row} className="h-16 w-full" />)}</div>
            ) : listQuery.isError ? (
              listQuery.error instanceof ApiError && listQuery.error.status === 403
                ? <div role="alert" className="flex flex-wrap items-center justify-center gap-3 py-8 text-sm text-destructive"><span>{permissionDeniedText}</span><Button type="button" variant="outline" size="sm" onClick={() => void listQuery.refetch()}>{t("common.retry")}</Button></div>
                : <div className="flex flex-wrap items-center justify-center gap-3 rounded-lg border border-destructive/25 bg-destructive/5 p-6 text-sm text-destructive"><span>{t("meetings.loadError")}</span><Button type="button" variant="outline" size="sm" onClick={() => void listQuery.refetch()}>{t("common.retry")}</Button></div>
            ) : meetings.length === 0 ? (
              <Card><CardContent className="p-8 text-center"><CalendarDays className="mx-auto mb-3 h-8 w-8 text-muted-foreground/60" aria-hidden="true" /><h2 className="font-medium">{emptyTitle}</h2><p className="mt-1 text-sm text-muted-foreground">{emptyDescription}</p></CardContent></Card>
            ) : (
              <MeetingsTable meetings={meetings} showProject={!lockedProject && filterIds.length !== 1} canManageMinutes={canManageMinutes} formatDate={formatDate} />
            )}

            {!noSelection && !noMatchingProjects && !listQuery.isLoading && !listQuery.isError && meetings.length > 0 ? (
              <Pagination page={page} totalPages={totalPages} total={listQuery.data?.totalCount ?? 0} pageSize={pageSize} onChange={(nextPage) => updateParams({ page: nextPage > 1 ? String(nextPage) : undefined }, false)} />
            ) : null}
          </div>
        </div>
      )}
    </div>
  );
}
