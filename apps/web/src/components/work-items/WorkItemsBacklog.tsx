"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { useLocale } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import {
  AlertCircle, CalendarDays, CheckCircle2, ChevronDown, ChevronRight,
  Clock3, Eye, EyeOff, Filter, GripVertical, MoreHorizontal, Plus, Search, SortAsc,
  Tag, Ticket, UserRound, Wrench,
} from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { RichTextEditor } from "@/components/ui/rich-text-editor";
import { EmptyState, ErrorState } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TablePagination } from "@/components/ui/table-pagination";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { WorkItemAttachments } from "@/components/work-items/WorkItemAttachments";
import { WorkItemDetailsSideSheet } from "@/components/work-items/WorkItemDetailsSideSheet";
import { WorkItemAssigneeSelect } from "@/components/work-items/WorkItemAssigneeSelect";
import { WorkItemFiltersSideSheet, countActiveWorkItemFilters, type WorkItemFilterValues } from "@/components/work-items/WorkItemFiltersSideSheet";
import { useProjects } from "@/hooks/projects/useProjects";
import { useUserLookup } from "@/hooks/projects/useProjectLookups";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api/client";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useManagementPerspective } from "@/lib/perspective-context";
import { getWorkItemStatusColorClasses } from "@/lib/status-presentation";
import { formatDate } from "@/lib/utils";
import {
  useWorkItemAssignees, useWorkItemMutations, useWorkItems, useWorkItemInsights,
  useWorkItemWorkflowTransition, workItemsApi, type WorkflowTransitionOption,
  type SaveWorkItemDto, type WorkItemDto, type WorkItemPriority,
  type WorkItemStatus, type WorkItemType,
} from "@/lib/api/work-items";
import { getWorkItemStatusName, useWorkItemStatuses, type WorkItemStatusReference } from "@/lib/api/reference-data";

type Copy = Record<string, string>;
export type BacklogTab = "backlog" | "board" | "my-work";
export type QuickView = "all" | "my-items" | "unassigned" | "overdue" | "high-priority" | "recently-updated";
export type GroupBy = "none" | "status" | "assignee" | "priority" | "project" | "type";
export type SortBy = "createdAt" | "updatedAt" | "title" | "priority" | "status" | "dueDate";

const typeColors: Record<string, string> = {
  Task: "border-primary/25 bg-primary/10 text-primary",
  Issue: "border-info/30 bg-info/10 text-info",
  Subtask: "border-muted-foreground/25 bg-muted text-muted-foreground",
  Bug: "border-destructive/25 bg-destructive/10 text-destructive",
  Feature: "border-success/25 bg-success/10 text-success",
  Epic: "border-fin-violet/25 bg-fin-violet/10 text-fin-violet",
  Story: "border-warning/25 bg-warning/10 text-warning",
};

const typeIcons: Record<string, typeof Wrench> = {
  Task: Wrench, Issue: Ticket, Bug: AlertCircle, Feature: CheckCircle2,
};

const priorityClass = (value: WorkItemPriority) =>
  value === "Critical" ? "text-destructive font-semibold"
  : value === "High" ? "text-amber-600 dark:text-amber-400"
  : value === "Medium" ? "text-blue-700 dark:text-blue-300"
  : "text-muted-foreground";

const priorityDot: Record<string, string> = {
  Critical: "bg-destructive", High: "bg-amber-500", Medium: "bg-blue-500",
  Low: "bg-muted-foreground", Backlog: "bg-muted-foreground/50",
};

const statusLabel = (value: string, _t: Copy, statuses: readonly WorkItemStatusReference[], locale: string) => getWorkItemStatusName(value, statuses, locale, value);

function KanbanBoard({
  items,
  statuses,
  locale,
  t,
  emptyLabel,
  canEdit,
  movingItemId,
  moveError,
  assigneeNames,
  onMove,
  onOpen,
}: {
  items: readonly WorkItemDto[];
  statuses: readonly WorkItemStatusReference[];
  locale: string;
  t: Copy;
  emptyLabel: string;
  canEdit: boolean;
  movingItemId: string | null;
  moveError: string;
  assigneeNames: ReadonlyMap<string, string>;
  onMove: (_itemId: string, _status: WorkItemStatus) => void;
  onOpen: (_id: string) => void;
}) {
  const [draggedItemId, setDraggedItemId] = useState<string | null>(null);
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  return (
    <div className="space-y-3 p-3 sm:p-4">
      {moveError ? <p role="alert" className="rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{moveError}</p> : null}
      {canEdit ? <p className="flex items-center gap-1.5 text-xs text-muted-foreground"><GripVertical className="h-3.5 w-3.5" />{t.dragToMove}</p> : null}
      <div className="overflow-x-auto pb-2" dir={locale === "ar" ? "rtl" : "ltr"}>
        <div className="flex min-w-max items-stretch gap-3">
          {statuses.map((status) => {
            const tone = getWorkItemStatusColorClasses(status.color);
            const columnItems = items.filter((item) => item.status === status.code);
            return (
              <section
                key={status.id}
                aria-label={statusLabel(status.code, t, statuses, locale)}
                className={`flex min-h-[28rem] w-[18rem] flex-col rounded-xl border sm:w-[20rem] ${tone.panel}`}
                onDragOver={(event) => {
                  if (canEdit && status.isActive) {
                    event.preventDefault();
                    event.dataTransfer.dropEffect = "move";
                  }
                }}
                onDrop={(event) => {
                  if (!canEdit || !status.isActive) return;
                  event.preventDefault();
                  const itemId = event.dataTransfer.getData("text/plain") || draggedItemId;
                  if (itemId) onMove(itemId, status.code);
                  setDraggedItemId(null);
                }}
              >
                <header className="flex items-center justify-between gap-2 border-b border-inherit px-3 py-3">
                  <h2 className="flex min-w-0 items-center gap-2 text-sm font-semibold"><span className={`h-2.5 w-2.5 shrink-0 rounded-full ${tone.dot}`} />{statusLabel(status.code, t, statuses, locale)}</h2>
                  <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold tabular-nums ${tone.badge}`}>{columnItems.length}</span>
                </header>
                <div className="flex flex-1 flex-col gap-3 p-3" role="list">
                  {columnItems.length > 0 ? columnItems.map((item) => {
                    const deadline = item.type === "Issue" ? item.targetResolutionDate : item.dueDate;
                    const isMoving = movingItemId === item.id;
                    const assignee = item.assigneeName ?? (item.assigneeId ? assigneeNames.get(item.assigneeId) ?? t.notAvailable : t.unassigned);
                    return (
                      <button
                        key={item.id}
                        type="button"
                        role="listitem"
                        draggable={canEdit && !isMoving}
                        aria-busy={isMoving}
                        aria-label={`${item.title}. ${t.viewDetails}`}
                        onDragStart={(event) => { setDraggedItemId(item.id); event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", item.id); }}
                        onDragEnd={() => setDraggedItemId(null)}
                        onClick={() => onOpen(item.id)}
                        className={`group w-full rounded-lg border border-border/70 bg-background p-3 text-start shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${isMoving ? "opacity-60" : ""} ${canEdit ? "cursor-grab active:cursor-grabbing" : ""}`}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <span className={`rounded-md border px-1.5 py-0.5 text-[10px] font-semibold ${typeColors[item.type] ?? typeColors.Task}`}>{t[item.type.toLowerCase() as keyof Copy] || item.type}</span>
                          {canEdit ? <GripVertical className="h-4 w-4 shrink-0 text-muted-foreground/50 transition-colors group-hover:text-muted-foreground" aria-hidden="true" /> : null}
                        </div>
                        <div className="mt-2 font-mono text-[10px] text-muted-foreground">{item.id.slice(0, 8)}</div>
                        <h3 className="mt-1 line-clamp-2 text-sm font-semibold leading-5 text-foreground">{item.title}</h3>
                        <div className="mt-3 flex flex-wrap gap-1.5 text-[11px]"><span className={`rounded-md bg-muted/70 px-2 py-1 font-medium ${priorityClass(item.priority)}`}>{t[item.priority.toLowerCase() as keyof Copy] || item.priority}</span></div>
                        <div className="mt-3 flex flex-col gap-1.5 border-t border-border/60 pt-2.5 text-[11px] text-muted-foreground">
                          <span className="flex min-w-0 items-center gap-1.5"><UserRound className="h-3.5 w-3.5 shrink-0" /><span className="truncate">{assignee}</span></span>
                          {deadline ? <span className="flex items-center gap-1.5"><CalendarDays className="h-3.5 w-3.5 shrink-0" /><span>{formatDate(deadline, dateLocale)}</span></span> : null}
                        </div>
                        {isMoving ? <span className="mt-2 block text-[11px] font-medium text-primary">{t.moving}</span> : null}
                      </button>
                    );
                  }) : <div className="flex flex-1 items-center justify-center rounded-lg border border-dashed border-border/70 bg-background/35 p-4 text-center text-xs text-muted-foreground">{emptyLabel}</div>}
                </div>
              </section>
            );
          })}
        </div>
      </div>
    </div>
  );
}

export function WorkItemsBacklog() {
  const locale = useLocale();
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = locale === "ar" ? text.ar : text.en;
  const { user } = useAuth();
  const { canMutate } = useManagementPerspective();
  const transition = useWorkItemWorkflowTransition();
  const canCreate = canMutate && Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create));
  const canEdit = canMutate && Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit));
  const projectsQuery = useProjects({ page: 1, pageSize: 100 }, true);
  const usersQuery = useUserLookup(undefined, true);

  const statusesQuery = useWorkItemStatuses(true);
  const allStatuses = statusesQuery.data ?? [];
  const activeStatuses = allStatuses.filter((s) => s.isActive).sort((a, b) => a.displayOrder - b.displayOrder);
  const replaceQuery = useCallback((updates: Record<string, string | undefined>) => {
    const next = new URLSearchParams(searchParams.toString());
    if (Object.keys(updates).some((key) => key !== "page" && key !== "view")) next.delete("page");
    Object.entries(updates).forEach(([key, value]) => {
      if (value === undefined || value === "" || value === "all" || (key === "view" && value === "backlog") || (key === "group" && value === "none") || (key === "sort" && value === "createdAt") || (key === "quick" && value === "all")) next.delete(key);
      else next.set(key, value);
    });
    router.replace(`/${locale}/work-items${next.toString() ? `?${next.toString()}` : ""}`);
  }, [locale, router, searchParams]);

  const activeTab = (searchParams.get("view") as BacklogTab | null) ?? "backlog";
  const quickView = (searchParams.get("quick") as QuickView | null) ?? "all";
  const typeFilter = (searchParams.get("type") as "all" | "Task" | "Issue" | null) ?? "all";
  const statusFilter = searchParams.get("status") ?? "all";
  const priorityFilter = searchParams.get("priority") ?? "all";
  const assigneeFilter = searchParams.get("assignee") ?? "all";
  const searchQuery = searchParams.get("search") ?? "";
  const groupBy = (searchParams.get("group") as GroupBy | null) ?? "none";
  const sortBy = (searchParams.get("sort") as SortBy | null) ?? "createdAt";
  const page = Math.max(1, Number(searchParams.get("page") ?? "1") || 1);
  const projectId = searchParams.get("projectId") || undefined;
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [showInsights, setShowInsights] = useState(true);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [openCreateSheet, setOpenCreateSheet] = useState(false);
  const [createType, setCreateType] = useState<WorkItemType>("Task");
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());
  const [detailItemId, setDetailItemId] = useState<string | null>(null);
  const [movingItemId, setMovingItemId] = useState<string | null>(null);
  const [moveError, setMoveError] = useState("");
  const [pendingMove, setPendingMove] = useState<{ itemId: string; status: WorkItemStatus; options: WorkflowTransitionOption[] } | null>(null);

  const filterValues: WorkItemFilterValues = useMemo(() => ({
    type: typeFilter,
    status: statusFilter,
    priority: priorityFilter,
    assignee: assigneeFilter,
    projectId: projectId ?? "",
    dueFrom: searchParams.get("dueFrom") ?? "",
    dueTo: searchParams.get("dueTo") ?? "",
  }), [assigneeFilter, priorityFilter, projectId, searchParams, statusFilter, typeFilter]);
  const activeFilterCount = countActiveWorkItemFilters(filterValues) + (searchQuery.trim() ? 1 : 0);
  const setActiveTab = (value: BacklogTab) => replaceQuery({ view: value, page: undefined });
  const setQuickView = (value: QuickView) => replaceQuery({ quick: value });
  const setTypeFilter = (value: "all" | "Task" | "Issue") => replaceQuery({ type: value });
  const setStatusFilter = (value: string) => replaceQuery({ status: value });
  const setPriorityFilter = (value: string) => replaceQuery({ priority: value });
  const setAssigneeFilter = (value: string) => replaceQuery({ assignee: value });
  const setSearchQuery = (value: string) => replaceQuery({ search: value });
  const setGroupBy = (value: GroupBy) => replaceQuery({ group: value });
  const setSortBy = (value: SortBy) => replaceQuery({ sort: value });
  const applyFilters = (value: WorkItemFilterValues) => replaceQuery({ type: value.type, status: value.status, priority: value.priority, assignee: value.assignee, projectId: value.projectId, dueFrom: value.dueFrom, dueTo: value.dueTo });

  const buildFilterParams = useCallback(() => {
    const p: Record<string, string | number | undefined> = {
      projectId,
      type: typeFilter === "all" ? undefined : typeFilter,
      status: statusFilter === "all" ? undefined : statusFilter,
      priority: priorityFilter === "all" ? undefined : priorityFilter,
      assigneeId: assigneeFilter === "all" ? undefined
        : assigneeFilter === "me" ? user?.id
        : assigneeFilter === "unassigned" ? undefined
        : assigneeFilter,
      unassigned: assigneeFilter === "unassigned" || quickView === "unassigned" ? "true" : undefined,
      search: searchQuery.trim() || undefined,
      dueDateFrom: searchParams.get("dueFrom") || undefined,
      dueDateTo: searchParams.get("dueTo") || undefined,
      overdue: quickView === "overdue" ? "true" : undefined,
      highPriority: quickView === "high-priority" ? "true" : undefined,
      sortBy: quickView === "recently-updated" ? "updatedAt" : sortBy,
    };
    if (quickView === "my-items" || activeTab === "my-work") p.assigneeId = user?.id;
    return p;
  }, [activeTab, assigneeFilter, priorityFilter, projectId, quickView, searchParams, searchQuery, sortBy, statusFilter, typeFilter, user?.id]);

  const query = useWorkItems({ ...buildFilterParams(), page, pageSize: 100 });
  const insightsQuery = useWorkItemInsights(buildFilterParams());
  const items = query.data?.items ?? [];
  const assigneeNames = useMemo(() => new Map((usersQuery.data ?? []).map((candidate) => [candidate.id, candidate.fullName])), [usersQuery.data]);
  const filteredItems = items;
  const insights = insightsQuery.data;
  const openItems = insights?.open ?? 0;
  const overdueItems = insights?.overdue ?? 0;
  const unassignedItems = insights?.unassigned ?? 0;
  const dueThisWeekItems = insights?.dueThisWeek ?? 0;

  const statusDistribution = useMemo(() => {
    return (insights?.byStatus ?? []).map((bucket) => [bucket.key, bucket.count] as [string, number]);
  }, [insights]);

  const priorityDistribution = useMemo(() => {
    return (insights?.byPriority ?? []).map((bucket) => [bucket.key, bucket.count] as [string, number]);
  }, [insights]);

  const groupedItems = useMemo(() => {
    if (groupBy === "none") return new Map([["", filteredItems]]);
    const map = new Map<string, WorkItemDto[]>();
    filteredItems.forEach((item) => {
      let key = "";
      switch (groupBy) {
        case "status": key = item.status; break;
        case "assignee": key = item.assigneeId || "unassigned"; break;
        case "priority": key = item.priority; break;
        case "project": key = item.projectName || item.projectId || "no-project"; break;
        case "type": key = item.type; break;
      }
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(item);
    });
    return map;
  }, [filteredItems, groupBy]);

  const toggleGroup = (key: string) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  };

  useEffect(() => {
    const allGroups = Array.from(groupedItems.keys());
    setExpandedGroups(new Set(allGroups));
  }, [groupBy, groupedItems]);

  const toggleSelectAll = () => {
    if (selectedIds.size === filteredItems.length) setSelectedIds(new Set());
    else setSelectedIds(new Set(filteredItems.map((i) => i.id)));
  };

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const quickViews: { key: QuickView; label: string }[] = useMemo(() => [
    { key: "all", label: t.qvAll },
    { key: "my-items", label: t.qvMyItems },
    { key: "unassigned", label: t.qvUnassigned },
    { key: "overdue", label: t.qvOverdue },
    { key: "high-priority", label: t.qvHighPriority },
    { key: "recently-updated", label: t.qvRecentlyUpdated },
  ], [t]);

  const openCreate = (type: WorkItemType) => { setCreateType(type); setOpenCreateSheet(true); };

  const changeProject = (value: string) => {
    const next = new URLSearchParams(searchParams.toString());
    if (value) next.set("projectId", value); else next.delete("projectId");
    router.replace(`/${locale}/work-items${next.toString() ? `?${next.toString()}` : ""}`);
  };

  const executeMove = async (itemId: string, status: WorkItemStatus, transitionCode?: string) => {
    const item = filteredItems.find((candidate) => candidate.id === itemId);
    if (!item) return;
    setMoveError("");
    setMovingItemId(itemId);
    try {
      await transition.mutateAsync({ itemId: item.id, targetStateCode: status, transitionCode });
      setPendingMove(null);
    } catch (error) {
      setMoveError(error instanceof ApiError ? error.message : t.moveError);
    } finally {
      setMovingItemId(null);
    }
  };

  const moveItem = async (itemId: string, status: WorkItemStatus) => {
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item || !canEdit || item.status === status || movingItemId || !activeStatuses.some((candidate) => candidate.code === status)) return;
    setMoveError("");
    try {
      const available = await workItemsApi.workflowTransitions(item.id);
      const candidates = available.transitions.filter((candidate) => candidate.targetStateCode === status && candidate.canExecute);
      if (candidates.length === 0) throw new Error(available.transitions.find((candidate) => candidate.targetStateCode === status)?.denialReason || t.transitionUnavailable);
      if (candidates.length > 1) {
        setPendingMove({ itemId, status, options: candidates });
        return;
      }
      await executeMove(itemId, status, candidates[0].code);
    } catch (error) {
      setMoveError(error instanceof ApiError ? error.message : t.moveError);
    }
  };

  const boardStatuses = [...activeStatuses, ...allStatuses.filter((status) => !status.isActive && items.some((item) => item.status === status.code))];

  const renderFilterBar = () => (
    <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
      <div className="relative flex-1">
        <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input placeholder={t.searchPlaceholder} className="ps-8" value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} />
      </div>
      <div className="flex flex-wrap items-center gap-1.5">
        <FilterSelect value={typeFilter} onChange={(v) => setTypeFilter(v as typeof typeFilter)} options={[["all", t.typeAll], ["Task", t.task], ["Issue", t.issue]]} />
        <FilterSelect value={statusFilter} onChange={setStatusFilter} options={[["all", t.statusAll], ...activeStatuses.map((s) => [s.code, locale === "ar" ? s.nameAr : s.nameEn] as [string, string])]} />
        <FilterSelect value={priorityFilter} onChange={setPriorityFilter} options={[["all", t.priorityAll], ["Critical", t.critical], ["High", t.high], ["Medium", t.medium], ["Low", t.low], ["Backlog", t.backlog]]} />
        <FilterSelect value={assigneeFilter} onChange={setAssigneeFilter} options={[["all", t.assigneeAll], ["me", t.assigneeMe], ["unassigned", t.assigneeUnassigned], ...((usersQuery.data ?? []).map((candidate) => [candidate.id, candidate.fullName] as [string, string]))]} />
        <Button variant="outline" size="sm" className="h-8" onClick={() => setFiltersOpen(true)}><Filter className="me-1 h-3 w-3" />{t.moreFilters}{activeFilterCount > 0 ? <span className="ms-1 rounded-full bg-primary/10 px-1.5 text-[10px] tabular-nums text-primary">{activeFilterCount}</span> : null}</Button>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button variant="outline" size="sm" className="h-8"><SortAsc className="me-1 h-3 w-3" />{t.sortBy}</Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">{([ ["createdAt", t.sortCreated], ["updatedAt", t.sortUpdated], ["title", t.sortTitle], ["priority", t.sortPriority], ["status", t.sortStatus], ["dueDate", t.sortDueDate] ] as [SortBy, string][]).map(([val, label]) => <DropdownMenuItem key={val} onClick={() => setSortBy(val)} className={sortBy === val ? "bg-accent" : ""}>{label}</DropdownMenuItem>)}</DropdownMenuContent>
        </DropdownMenu>
        <DropdownMenu>
          <DropdownMenuTrigger asChild><Button variant="outline" size="sm" className="h-8">{t.groupBy}</Button></DropdownMenuTrigger>
          <DropdownMenuContent align="end">{([ ["none", t.groupNone], ["status", t.groupStatus], ["assignee", t.groupAssignee], ["priority", t.groupPriority], ["project", t.groupProject], ["type", t.groupType] ] as [GroupBy, string][]).map(([val, label]) => <DropdownMenuItem key={val} onClick={() => setGroupBy(val)} className={groupBy === val ? "bg-accent" : ""}>{label}</DropdownMenuItem>)}</DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  );

  const renderGrid = (displayItems: readonly WorkItemDto[]) => (
    <div className="overflow-x-auto">
      <Table density="compact">
        <TableHeader>
          <TableRow>
            <TableHead className="w-10">
              <input
                type="checkbox"
                checked={selectedIds.size === displayItems.length && displayItems.length > 0}
                onChange={toggleSelectAll}
                className="h-4 w-4 rounded border-input"
                aria-label={t.selectAll}
              />
            </TableHead>
            <TableHead className="w-12">{t.colType}</TableHead>
            <TableHead className="w-24">{t.colId}</TableHead>
            <TableHead>{t.colTitle}</TableHead>
            <TableHead className="w-28">{t.colStatus}</TableHead>
            <TableHead className="w-24">{t.colPriority}</TableHead>
            <TableHead className="w-32">{t.colAssignee}</TableHead>
            <TableHead className="w-28">{t.colDueDate}</TableHead>
            {!projectId && <TableHead className="w-32">{t.colProject}</TableHead>}
            <TableHead className="w-20">{t.colTags}</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {displayItems.length === 0 ? (
            <TableRow>
              <TableCell colSpan={projectId ? 10 : 11}>
                <EmptyState title={t.emptyTitle} description={t.emptyHint} />
              </TableCell>
            </TableRow>
          ) : displayItems.map((item) => (
            <BacklogRow
              key={item.id}
              item={item}
              t={t}
              locale={locale}
              statuses={allStatuses}
              projectId={projectId}
              selected={selectedIds.has(item.id)}
              onSelect={() => toggleSelect(item.id)}
              onOpen={() => setDetailItemId(item.id)}
            />
          ))}
        </TableBody>
      </Table>
    </div>
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title={t.title}
        description={t.subtitle}
        actions={
          <div className="flex items-center gap-2">
            <select aria-label={t.projectScope} value={projectId ?? ""} onChange={(event) => changeProject(event.target.value)} className="h-9 max-w-[16rem] rounded-md border border-input bg-background px-2 text-xs">
              <option value="">{t.allProjects}</option>
              {(projectsQuery.data?.items ?? []).map((project) => <option key={project.id} value={project.id}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}
            </select>
            <Button size="sm" disabled={!canCreate} onClick={() => openCreate("Task")}>
              <Plus className="me-2 h-4 w-4" />
              {t.newWorkItem}
            </Button>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-1 rounded-lg border bg-muted/20 p-1" role="tablist">
        {(["backlog", "board", "my-work"] as BacklogTab[]).map((tab) => (
          <button
            key={tab}
            type="button"
            role="tab"
            aria-selected={activeTab === tab}
            onClick={() => setActiveTab(tab)}
            className={`rounded-md px-3 py-1.5 text-sm transition-colors ${
              activeTab === tab ? "bg-background font-medium shadow-sm" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            {tab === "backlog" ? t.tabBacklog : tab === "board" ? t.tabBoard : t.tabMyWork}
          </button>
        ))}
      </div>

      {activeTab === "board" ? renderFilterBar() : null}
      {activeTab === "my-work" ? renderFilterBar() : null}

      {activeTab === "backlog" && (
        <>
          <div className="grid gap-3 sm:grid-cols-4">
            <CompactKpi label={t.kpiOpen} value={openItems} icon={Clock3} color="text-amber-600" />
            <CompactKpi label={t.kpiOverdue} value={overdueItems} icon={AlertCircle} color="text-destructive" />
            <CompactKpi label={t.kpiUnassigned} value={unassignedItems} icon={UserRound} color="text-muted-foreground" />
            <CompactKpi label={t.kpiDueThisWeek} value={dueThisWeekItems} icon={CalendarDays} color="text-blue-600" />
          </div>

          <Collapsible open={showInsights} onOpenChange={setShowInsights}>
            <div className="flex items-center justify-between">
              <CollapsibleTrigger asChild>
                <button type="button" className="flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground">
                  {showInsights ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  {showInsights ? t.hideInsights : t.showInsights}
                </button>
              </CollapsibleTrigger>
            </div>
            <CollapsibleContent className="mt-3 space-y-4">
              <div className="grid gap-4 md:grid-cols-3">
                <InsightCard title={t.insightByStatus}>
                  <div className="space-y-2">
                    {statusDistribution.map(([code, count]) => {
                      const status = allStatuses.find((s) => s.code === code);
                      const max = Math.max(...statusDistribution.map(([, c]) => c), 1);
                      return (
                        <div key={code} className="flex items-center gap-2 text-xs">
                          <span className="w-24 truncate text-muted-foreground">
                            {status ? (locale === "ar" ? status.nameAr : status.nameEn) : code}
                          </span>
                          <div className="flex-1 h-1.5 rounded-full bg-muted/40 overflow-hidden">
                            <div className="h-full rounded-full" style={{ width: `${(count / max) * 100}%`, backgroundColor: getStatusColor(status?.color) }} />
                          </div>
                          <span className="w-6 text-right font-medium tabular-nums">{count}</span>
                        </div>
                      );
                    })}
                    {statusDistribution.length === 0 && <p className="text-xs text-muted-foreground">{t.noData}</p>}
                  </div>
                </InsightCard>

                <InsightCard title={t.insightByPriority}>
                  <div className="space-y-2">
                    {priorityDistribution.map(([priority, count]) => {
                      const max = Math.max(...priorityDistribution.map(([, c]) => c), 1);
                      return (
                        <div key={priority} className="flex items-center gap-2 text-xs">
                          <span className="w-20 truncate text-muted-foreground">{t[priority.toLowerCase() as keyof Copy] || priority}</span>
                          <div className="flex-1 h-1.5 rounded-full bg-muted/40 overflow-hidden">
                            <div className={`h-full rounded-full ${getPriorityBarColor(priority)}`} style={{ width: `${(count / max) * 100}%` }} />
                          </div>
                          <span className="w-6 text-right font-medium tabular-nums">{count}</span>
                        </div>
                      );
                    })}
                  </div>
                </InsightCard>

                <InsightCard title={t.insightAttention}>
                  <div className="space-y-2.5">
                    <AttentionRow label={t.qvOverdue} count={overdueItems} color="text-destructive" />
                    <AttentionRow label={t.qvUnassigned} count={unassignedItems} color="text-muted-foreground" />
                    <AttentionRow label={t.qvDueThisWeek} count={dueThisWeekItems} color="text-blue-600" />
                  </div>
                </InsightCard>
              </div>
            </CollapsibleContent>
          </Collapsible>

          <div className="flex flex-wrap gap-1.5">
            {quickViews.map((qv) => (
              <button
                key={qv.key}
                type="button"
                onClick={() => setQuickView(qv.key)}
                className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
                  quickView === qv.key
                    ? "border-primary/40 bg-primary/10 text-primary"
                    : "border-border bg-background text-muted-foreground hover:text-foreground hover:border-border/80"
                }`}
              >
                {qv.label}
              </button>
            ))}
          </div>

          {renderFilterBar()}

          {query.isLoading ? (
            <div className="space-y-3 p-4">
              {Array.from({ length: 5 }).map((_, i) => (
                <Skeleton key={i} className="h-12 w-full rounded-lg" />
              ))}
            </div>
          ) : query.isError ? (
            <ErrorState title={t.loadError} onRetry={() => query.refetch()} />
          ) : groupedItems.size === 0 ? (
            <EmptyState title={t.emptyTitle} description={t.emptyHint} />
          ) : groupBy === "none" ? (
            <Card><CardContent className="p-0">{renderGrid(filteredItems)}</CardContent></Card>
          ) : (
            <div className="space-y-2">
              {Array.from(groupedItems.entries()).map(([groupKey, groupItems]) => {
                const isExpanded = expandedGroups.has(groupKey);
                const groupLabel = getGroupLabel(groupKey, groupBy, allStatuses, locale);
                return (
                  <Collapsible key={groupKey} open={isExpanded} onOpenChange={() => toggleGroup(groupKey)}>
                    <Card>
                      <CollapsibleTrigger asChild>
                        <button type="button" className="flex w-full items-center gap-2 px-4 py-2.5 text-sm font-medium hover:bg-muted/20 rounded-t-lg">
                          {isExpanded ? <ChevronDown className="h-4 w-4 text-muted-foreground" /> : <ChevronRight className="h-4 w-4 text-muted-foreground" />}
                          <span>{groupLabel}</span>
                          <Badge variant="secondary" className="ms-auto">{groupItems.length}</Badge>
                        </button>
                      </CollapsibleTrigger>
                      <CollapsibleContent>
                        <CardContent className="p-0 border-t">
                          {renderGrid(groupItems)}
                        </CardContent>
                      </CollapsibleContent>
                    </Card>
                  </Collapsible>
                );
              })}
            </div>
          )}
        </>
      )}

      {activeTab === "board" && (
        query.isLoading ? <Card><CardContent className="space-y-3 p-5"><Skeleton className="h-10 w-full" /><Skeleton className="h-64 w-full" /></CardContent></Card>
          : query.isError ? <ErrorState title={t.loadError} onRetry={() => void query.refetch()} />
            : <div className="space-y-2">{query.data && query.data.totalPages > 1 ? <p className="text-xs text-muted-foreground">{t.boardPageHint}</p> : null}<Card className="overflow-hidden"><CardContent className="p-0"><KanbanBoard items={filteredItems} statuses={boardStatuses} locale={locale} t={t} emptyLabel={t.emptyHint} canEdit={canEdit} movingItemId={movingItemId} moveError={moveError} assigneeNames={assigneeNames} onMove={moveItem} onOpen={setDetailItemId} /></CardContent></Card></div>
      )}

      {activeTab === "my-work" && (
        <Card><CardContent className="p-0">
          {filteredItems.length === 0 ? (
            <EmptyState title={t.emptyMyWork} description={t.emptyMyWorkHint} />
          ) : renderGrid(filteredItems)}
        </CardContent></Card>
      )}

      {query.data && query.data.totalPages > 1 ? <TablePagination className="border-t border-border/60 pt-3" page={page} totalPages={query.data.totalPages} summary={`${t.page} ${page} ${t.of} ${query.data.totalPages}`} previousLabel={t.previous} nextLabel={t.next} onPageChange={(nextPage) => replaceQuery({ page: String(nextPage) })} disabled={query.isFetching} /> : null}

      <CreateWorkItemSheet
        open={openCreateSheet}
        onOpenChange={setOpenCreateSheet}
        t={t}
        locale={locale}
        defaultType={createType}
        statuses={allStatuses}
      />

      <WorkItemFiltersSideSheet open={filtersOpen} onOpenChange={setFiltersOpen} value={filterValues} onApply={applyFilters} locale={locale} t={t} statuses={allStatuses} users={usersQuery.data ?? []} projects={projectsQuery.data?.items ?? []} />
      <WorkItemDetailsSideSheet id={detailItemId} open={!!detailItemId} onOpenChange={(v) => { if (!v) setDetailItemId(null); }} locale={locale} t={t} statuses={allStatuses} canEdit={canEdit} />
      <SideSheet open={Boolean(pendingMove)} onOpenChange={(open) => { if (!open) setPendingMove(null); }} title={t.chooseTransition ?? "Choose transition"} description={t.multipleTransitions ?? "More than one transition is available for this target state."} size="md">
        <SideSheetBody className="space-y-2">
          {(pendingMove?.options ?? []).map((option) => <Button key={option.code} type="button" variant="outline" className="w-full justify-between" disabled={Boolean(movingItemId)} onClick={() => { if (pendingMove) void executeMove(pendingMove.itemId, pendingMove.status, option.code); }}><span>{locale === "ar" ? option.nameAr : option.nameEn}</span><span className="font-mono text-xs text-muted-foreground">{option.code}</span></Button>)}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setPendingMove(null)}>{t.cancel}</Button></SideSheetFooter>
      </SideSheet>
    </div>
  );
}

function BacklogRow({ item, t, locale, statuses, projectId, selected, onSelect, onOpen }: {
  item: WorkItemDto; t: Copy; locale: string; statuses: readonly WorkItemStatusReference[];
  projectId?: string; selected: boolean; onSelect: () => void; onOpen: () => void;
}) {
  const status = statuses.find((s) => s.code === item.status);
  const tone = getWorkItemStatusColorClasses(status?.color);
  const Icon = typeIcons[item.type] ?? Wrench;
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const deadline = item.type === "Issue" ? item.targetResolutionDate : item.dueDate;
  const isOverdue = deadline && new Date(deadline) < new Date() && !["Done", "Cancelled"].includes(item.status);

  return (
    <TableRow className="cursor-pointer" onClick={onOpen}>
      <TableCell onClick={(e) => e.stopPropagation()}>
        <input type="checkbox" checked={selected} onChange={onSelect} className="h-4 w-4 rounded border-input" />
      </TableCell>
      <TableCell>
        <span className={`inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-[10px] font-semibold ${typeColors[item.type] ?? typeColors.Task}`}>
          <Icon className="h-3 w-3" />
          {t[item.type.toLowerCase() as keyof Copy] || item.type}
        </span>
      </TableCell>
      <TableCell className="font-mono text-xs text-muted-foreground">{item.id.slice(0, 8)}</TableCell>
      <TableCell>
        <span className="line-clamp-1 font-medium">{item.title}</span>
      </TableCell>
      <TableCell>
        <span className={`inline-flex items-center gap-1.5 text-xs ${tone.text}`}>
          <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${tone.dot}`} />
          {status ? (locale === "ar" ? status.nameAr : status.nameEn) : item.status}
        </span>
      </TableCell>
      <TableCell>
        <span className={`flex items-center gap-1 text-xs ${priorityClass(item.priority)}`}>
          <span className={`h-1.5 w-1.5 rounded-full ${priorityDot[item.priority]}`} />
          {t[item.priority.toLowerCase() as keyof Copy] || item.priority}
        </span>
      </TableCell>
      <TableCell className="text-xs text-muted-foreground">
        {item.assigneeId ? <span className="flex items-center gap-1"><UserRound className="h-3 w-3" />{item.assigneeId.slice(0, 8)}</span> : "—"}
      </TableCell>
      <TableCell>
        {deadline ? (
          <span className={`flex items-center gap-1 text-xs ${isOverdue ? "font-semibold text-destructive" : "text-muted-foreground"}`}>
            <CalendarDays className="h-3 w-3" />
            {formatDate(deadline, dateLocale)}
          </span>
        ) : "—"}
      </TableCell>
      {!projectId && (
        <TableCell className="max-w-[120px] truncate text-xs text-muted-foreground" title={item.projectName}>
          {item.projectName || "—"}
        </TableCell>
      )}
      <TableCell>
        {item.tags ? (
          <span className="flex items-center gap-1 text-xs text-muted-foreground">
            <Tag className="h-3 w-3" />
            <span className="truncate">{item.tags.split(",")[0]}</span>
          </span>
        ) : "—"}
      </TableCell>
      <TableCell onClick={(e) => e.stopPropagation()}>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={onOpen}>{t.viewDetails}</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </TableCell>
    </TableRow>
  );
}

function CompactKpi({ label, value, icon: Icon, color }: { label: string; value: number; icon: typeof Clock3; color: string }) {
  return (
    <div className="flex items-center gap-3 rounded-lg border border-border/60 bg-card px-3 py-2.5 shadow-none">
      <span className={`inline-flex h-8 w-8 items-center justify-center rounded-md bg-muted/40 ${color}`}>
        <Icon className="h-4 w-4" />
      </span>
      <div className="min-w-0">
        <p className="text-lg font-bold leading-none tabular-nums">{value}</p>
        <p className="mt-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      </div>
    </div>
  );
}

function InsightCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card className="shadow-none">
      <CardHeader className="pb-2">
        <CardTitle className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent className="pt-0">{children}</CardContent>
    </Card>
  );
}

function AttentionRow({ label, count, color }: { label: string; count: number; color: string }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className={`text-sm font-bold tabular-nums ${color}`}>{count}</span>
    </div>
  );
}

function FilterSelect({ value, onChange, options }: { value: string; onChange: (_value: string) => void; options: [string, string][] }) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="h-8 rounded-md border border-input bg-background px-2 text-xs"
    >
      {options.map(([val, label]) => (
        <option key={val} value={val}>{label}</option>
      ))}
    </select>
  );
}

function getStatusColor(color?: string): string {
  const map: Record<string, string> = {
    slate: "#64748b", blue: "#3b82f6", amber: "#f59e0b",
    violet: "#8b5cf6", emerald: "#10b981", rose: "#f43f5e", orange: "#f97316",
  };
  return map[color ?? "slate"] ?? "#64748b";
}

function getPriorityBarColor(priority: string): string {
  const map: Record<string, string> = {
    Critical: "bg-destructive", High: "bg-amber-500", Medium: "bg-blue-500", Low: "bg-muted-foreground/40", Backlog: "bg-muted-foreground/20",
  };
  return map[priority] ?? "bg-muted-foreground/40";
}

function getGroupLabel(key: string, groupBy: GroupBy, statuses: readonly WorkItemStatusReference[], locale: string): string {
  if (groupBy === "status") {
    const s = statuses.find((st) => st.code === key);
    return s ? (locale === "ar" ? s.nameAr : s.nameEn) : key;
  }
  if (groupBy === "priority") {
    return key === "Critical" ? "Critical" : key === "High" ? "High" : key === "Medium" ? "Medium" : key === "Low" ? "Low" : key;
  }
  if (groupBy === "assignee") return key === "unassigned" ? "Unassigned" : key.slice(0, 8);
  if (groupBy === "project") return key === "no-project" ? "No Project" : key;
  if (groupBy === "type") return key;
  return key;
}

function CreateWorkItemSheet({ open, onOpenChange, t, locale, defaultType, statuses: _statuses }: {
  open: boolean; onOpenChange: (_open: boolean) => void; t: Copy; locale: string;
  defaultType: WorkItemType; statuses: readonly WorkItemStatusReference[];
}) {
  const router = useRouter();
  const { create } = useWorkItemMutations();
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, open);
  const [form, setForm] = useState({ title: "", description: "", type: defaultType as WorkItemType, priority: "Medium" as WorkItemPriority, projectId: "", assigneeId: "", dueDate: "", targetResolutionDate: "", severity: "Medium" });
  const [error, setError] = useState("");
  const [assigneeNotice, setAssigneeNotice] = useState("");
  const update = (key: string, value: string) => setForm((c) => ({ ...c, [key]: value }));
  const assigneesQuery = useWorkItemAssignees(form.projectId, open && Boolean(form.projectId));
  const assigneeCandidates = assigneesQuery.data ?? [];
  const close = (v: boolean) => { if (!v) { setForm({ title: "", description: "", type: defaultType, priority: "Medium", projectId: "", assigneeId: "", dueDate: "", targetResolutionDate: "", severity: "Medium" }); setError(""); setAssigneeNotice(""); } onOpenChange(v); };
  const isIssue = defaultType === "Issue";
  const projects = projectsQuery.data?.items ?? [];

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) { setError(t.validation); return; }
    try {
      const item = await create.mutateAsync({
        title: form.title.trim(), description: form.description.trim() || undefined,
        type: defaultType, priority: form.priority,
        projectId: form.projectId || undefined,
        assigneeId: form.assigneeId || undefined,
        dueDate: isIssue ? undefined : form.dueDate || undefined,
        targetResolutionDate: isIssue ? form.targetResolutionDate || undefined : undefined,
        severity: isIssue ? form.severity : undefined,
      } as SaveWorkItemDto);
      close(false);
      router.push(`/${locale}/work-items/${encodeURIComponent(item.id)}`);
    } catch { setError(t.createError); }
  };

  return (
    <SideSheet open={open} onOpenChange={close} title={isIssue ? t.newIssue : t.newTask} description={isIssue ? t.createIssueDesc : t.createTaskDesc} size="md" closeLabel={t.cancel}>
      <form onSubmit={submit} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {error && <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p>}
          {assigneeNotice && <p className="rounded-md border border-amber-500/30 bg-amber-500/5 px-3 py-2 text-sm text-amber-700 dark:text-amber-300">{assigneeNotice}</p>}
          <div className="space-y-1.5">
            <label className="text-sm font-medium">{t.titleField}</label>
            <Input value={form.title} onChange={(e) => update("title", e.target.value)} autoFocus maxLength={500} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <label className="text-sm font-medium">{t.priority}</label>
              <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.priority} onChange={(e) => update("priority", e.target.value)}>
                {["Critical", "High", "Medium", "Low", "Backlog"].map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium">{t.colProject}</label>
              <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.projectId} onChange={(e) => { const nextProjectId = e.target.value; setForm((current) => ({ ...current, projectId: nextProjectId, assigneeId: current.projectId === nextProjectId ? current.assigneeId : "" })); setAssigneeNotice(form.projectId && nextProjectId !== form.projectId && form.assigneeId ? t.assigneeInvalidForProject : ""); }}>
                <option value="">{t.noProject}</option>
                {projects.map((p) => <option key={p.id} value={p.id}>{p.code ? `${p.code} · ${p.name}` : p.name}</option>)}
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium">{t.assignee}</label>
              <WorkItemAssigneeSelect
                value={form.assigneeId}
                candidates={assigneeCandidates}
                loading={assigneesQuery.isLoading}
                disabled={!form.projectId}
                onValueChange={(value) => { setAssigneeNotice(""); update("assigneeId", value); }}
                placeholder={form.projectId ? t.selectAssignee : t.selectProjectFirst}
                searchPlaceholder={t.searchAssignees}
                emptyText={t.noEligibleProjectPeople}
                clearLabel={t.unassign}
              />
              {form.projectId && !assigneesQuery.isLoading && assigneeCandidates.length === 0 ? <p className="text-xs text-muted-foreground">{t.noEligibleProjectPeople}</p> : null}
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium">{isIssue ? t.targetResolutionDate : t.deadline}</label>
              <Input type="date" value={isIssue ? form.targetResolutionDate : form.dueDate} onChange={(e) => update(isIssue ? "targetResolutionDate" : "dueDate", e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium">{t.descriptionField}</label>
            <RichTextEditor id="create-description" value={form.description} onChange={(v) => update("description", v)} placeholder={t.descriptionField} ariaLabel={t.descriptionField} t={t} />
          </div>
          <WorkItemAttachments canManage t={t} files={[]} onFilesChange={() => {}} />
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? t.saving : t.create}
          </Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}

const text: { en: Copy; ar: Copy } = {
  en: {
    title: "Work Items", subtitle: "Plan, prioritize and track tasks and issues across your scope.",
    newWorkItem: "New Work Item", newTask: "New Task", newIssue: "New Issue", projectScope: "Project scope", allProjects: "All projects", moreFilters: "More filters", unassigned: "Unassigned", assignee: "Assignee", assign: "Assign", reassign: "Reassign", unassign: "Unassign", selectAssignee: "Select assignee", selectProjectFirst: "Select a project first", noEligibleProjectPeople: "No eligible project people found", assigneeInvalidForProject: "Selected assignee is not an active member of this project.", searchAssignees: "Search assignees...", advancedFilters: "Advanced filters", advancedFiltersHint: "Refine the current Work Items view.", applyFilters: "Apply filters", clearAll: "Clear all", filterType: "Type", filterStatus: "Status", filterPriority: "Priority", filterAssignee: "Assignee", filterProject: "Project", filterDueDate: "Due date", searchUsers: "Search users...", from: "From", to: "To",
    tabBacklog: "Backlog", tabBoard: "Board", tabMyWork: "My Work",
    kpiOpen: "Open", kpiOverdue: "Overdue", kpiUnassigned: "Unassigned", kpiDueThisWeek: "Due This Week",
    hideInsights: "Hide Insights", showInsights: "Show Insights",
    insightByStatus: "Work by Status", insightByPriority: "Priority Distribution", insightAttention: "Attention Needed",
    qvAll: "All Work", qvMyItems: "My Items", qvUnassigned: "Unassigned", qvOverdue: "Overdue", qvHighPriority: "High Priority", qvRecentlyUpdated: "Recently Updated",
    searchPlaceholder: "Search work items...", searchProjects: "Search projects...", noProject: "No project", loading: "Loading...", noProjects: "No projects",
    typeAll: "All Types", task: "Task", issue: "Issue", statusAll: "All Statuses", priorityAll: "All Priorities",
    critical: "Critical", high: "High", medium: "Medium", low: "Low", backlog: "Backlog",
    assigneeAll: "All Assignees", assigneeMe: "My Items", assigneeUnassigned: "Unassigned",
    sortBy: "Sort by", sortCreated: "Created", sortUpdated: "Updated", sortTitle: "Title", sortPriority: "Priority", sortStatus: "Status", sortDueDate: "Due Date",
    groupBy: "Group by", groupNone: "None", groupStatus: "Status", groupAssignee: "Assignee", groupPriority: "Priority", groupProject: "Project", groupType: "Type",
    colType: "Type", colId: "ID", colTitle: "Title", colStatus: "Status", colPriority: "Priority", colAssignee: "Assignee", colDueDate: "Due Date", colProject: "Project", colTags: "Tags",
    selectAll: "Select all", viewDetails: "View details",
    emptyTitle: "No work items found", emptyHint: "Create a task or issue to start tracking work.", emptyMyWork: "No items assigned to you", emptyMyWorkHint: "Work items assigned to you will appear here.",
    loadError: "Work items could not be loaded.", notFound: "Work item not found.",
    boardPlaceholder: "Board view — switch to Backlog for the list view.", boardPageHint: "Showing a bounded page of matching work items. Use pagination to view more.", detailTitle: "Work item details", detailPrimaryFields: "Primary fields", detailTabDetails: "Details", detailTabComments: "Comments", detailTabActivity: "Activity", taskDetails: "Task details", issueDetails: "Issue details", noActivity: "No activity recorded.", activityLoadError: "Activity could not be loaded.", notAvailable: "Not available", storyPoints: "Story points", estimatedHours: "Estimated hours", actualHours: "Actual hours", startDate: "Start date", identifiedDate: "Identified date", resolvedDate: "Resolved date", resolution: "Resolution", comments: "Comments", addComment: "Add comment", reply: "Reply", showReplies: "Show replies", hideReplies: "Hide replies", replyCount: "reply", replies: "replies", commentPlaceholder: "Write a comment...", replyPlaceholder: "Write a reply...", submitComment: "Post comment", submitReply: "Post reply", savingComment: "Posting...", noComments: "No comments yet.", commentsLoadError: "Comments could not be loaded.", commentSaveError: "Your comment could not be posted.", commentRequired: "Comment text is required.", system: "System", previous: "Previous page", next: "Next page", page: "Page", of: "of",
    titleField: "Title", descriptionField: "Description", description: "Description",
    priority: "Priority", status: "Status", currentState: "Current state", noTransitions: "No transitions available.", transitionUnavailable: "Transition unavailable.", chooseTransition: "Choose transition", multipleTransitions: "More than one transition is available for this target state.", due: "Due date", deadline: "Deadline", targetResolutionDate: "Target resolution date",
    createTaskDesc: "Create a task and start tracking the work.", createIssueDesc: "Create an issue requiring attention.",
    create: "Create", saving: "Saving...", cancel: "Cancel", close: "Close", validation: "Title is required.", createError: "Failed to create work item.",
    noData: "No data available", assignmentError: "The assignee could not be updated.",
  },
  ar: {
    title: "عناصر العمل", subtitle: "خطط ورتب وتتبع المهام والمشكلات في نطاقك.",
    newWorkItem: "عنصر عمل جديد", newTask: "مهمة جديدة", newIssue: "مشكلة جديدة", projectScope: "نطاق المشروع", allProjects: "كل المشروعات", moreFilters: "مرشحات إضافية", unassigned: "غير مسند", assignee: "المسند إليه", assign: "إسناد", reassign: "إعادة إسناد", unassign: "إلغاء الإسناد", selectAssignee: "اختر المسند إليه", selectProjectFirst: "اختر مشروعًا أولًا", noEligibleProjectPeople: "لا يوجد أعضاء مؤهلون في المشروع", assigneeInvalidForProject: "المسند إليه المحدد ليس عضوًا نشطًا في هذا المشروع.", searchAssignees: "ابحث عن أعضاء المشروع...", advancedFilters: "مرشحات متقدمة", advancedFiltersHint: "ضيّق نطاق عرض عناصر العمل الحالية.", applyFilters: "تطبيق المرشحات", clearAll: "مسح الكل", filterType: "النوع", filterStatus: "الحالة", filterPriority: "الأولوية", filterAssignee: "المسند إليه", filterProject: "المشروع", filterDueDate: "تاريخ الاستحقاق", searchUsers: "ابحث عن مستخدمين...", from: "من", to: "إلى",
    tabBacklog: "قائمة الأعمال", tabBoard: "لوحة", tabMyWork: "عملي",
    kpiOpen: "مفتوح", kpiOverdue: "متأخر", kpiUnassigned: "غير مسند", kpiDueThisWeek: "تستحق هذا الأسبوع",
    hideInsights: "إخفاء الرؤى", showInsights: "عرض الرؤى",
    insightByStatus: "التوزيع حسب الحالة", insightByPriority: "التوزيع حسب الأولوية", insightAttention: "يحتاج اهتمام",
    qvAll: "كل العناصر", qvMyItems: "عناصري", qvUnassigned: "غير مسند", qvOverdue: "متأخر", qvHighPriority: "أولوية عالية", qvRecentlyUpdated: "حديث التحديث",
    searchPlaceholder: "ابحث في عناصر العمل...", searchProjects: "ابحث في المشاريع...", noProject: "بدون مشروع", loading: "جاري التحميل...", noProjects: "لا توجد مشاريع",
    typeAll: "كل الأنواع", task: "مهمة", issue: "مشكلة", statusAll: "كل الحالات", priorityAll: "كل الأولويات",
    critical: "حرج", high: "عالي", medium: "متوسط", low: "منخفض", backlog: "قائمة انتظار",
    assigneeAll: "كل المُعيَّنين", assigneeMe: "عناصري", assigneeUnassigned: "غير مسند",
    sortBy: "ترتيب حسب", sortCreated: "تاريخ الإنشاء", sortUpdated: "آخر تحديث", sortTitle: "العنوان", sortPriority: "الأولوية", sortStatus: "الحالة", sortDueDate: "تاريخ الاستحقاق",
    groupBy: "تجميع حسب", groupNone: "بدون", groupStatus: "الحالة", groupAssignee: "المُعيَّن", groupPriority: "الأولوية", groupProject: "المشروع", groupType: "النوع",
    colType: "النوع", colId: "المعرف", colTitle: "العنوان", colStatus: "الحالة", colPriority: "الأولوية", colAssignee: "المُعيَّن", colDueDate: "الاستحقاق", colProject: "المشروع", colTags: "الوسوم",
    selectAll: "تحديد الكل", viewDetails: "عرض التفاصيل",
    emptyTitle: "لا توجد عناصر عمل", emptyHint: "أنشئ مهمة أو مشكلة لبدء المتابعة.", emptyMyWork: "لا توجد عناصر مسندة إليك", emptyMyWorkHint: "ستظهر هنا عناصر العمل المسندة إليك.",
    loadError: "تعذر تحميل عناصر العمل.", notFound: "عنصر العمل غير موجود.",
    boardPlaceholder: "عرض اللوحة — انتقل إلى قائمة الأعمال لعرض القائمة.", boardPageHint: "يتم عرض صفحة محددة من عناصر العمل المطابقة. استخدم التنقل بين الصفحات لعرض المزيد.", detailTitle: "تفاصيل عنصر العمل", detailPrimaryFields: "البيانات الأساسية", detailTabDetails: "التفاصيل", detailTabComments: "التعليقات", detailTabActivity: "النشاط", taskDetails: "تفاصيل المهمة", issueDetails: "تفاصيل المشكلة", noActivity: "لا يوجد نشاط مسجل.", activityLoadError: "تعذر تحميل النشاط.", notAvailable: "غير متاح", storyPoints: "نقاط القصة", estimatedHours: "الساعات المقدرة", actualHours: "الساعات الفعلية", startDate: "تاريخ البدء", identifiedDate: "تاريخ التحديد", resolvedDate: "تاريخ الحل", resolution: "الحل", comments: "التعليقات", addComment: "إضافة تعليق", reply: "رد", showReplies: "عرض الردود", hideReplies: "إخفاء الردود", replyCount: "رد", replies: "ردود", commentPlaceholder: "اكتب تعليقًا...", replyPlaceholder: "اكتب ردًا...", submitComment: "نشر التعليق", submitReply: "نشر الرد", savingComment: "جاري النشر...", noComments: "لا توجد تعليقات بعد.", commentsLoadError: "تعذر تحميل التعليقات.", commentSaveError: "تعذر نشر التعليق.", commentRequired: "نص التعليق مطلوب.", system: "النظام", previous: "الصفحة السابقة", next: "الصفحة التالية", page: "صفحة", of: "من",
    titleField: "العنوان", descriptionField: "الوصف", description: "الوصف",
    priority: "الأولوية", status: "الحالة", currentState: "الحالة الحالية", noTransitions: "لا توجد انتقالات متاحة.", transitionUnavailable: "الانتقال غير متاح.", chooseTransition: "اختر الانتقال", multipleTransitions: "يوجد أكثر من انتقال متاح إلى هذه الحالة.", due: "الاستحقاق", deadline: "الموعد النهائي", targetResolutionDate: "تاريخ الحل المستهدف",
    createTaskDesc: "أنشئ مهمة وابدأ في متابعة العمل.", createIssueDesc: "أنشئ مشكلة تتطلب اهتمامًا.",
    create: "إنشاء", saving: "جاري الحفظ...", cancel: "إلغاء", close: "إغلاق", validation: "العنوان مطلوب.", createError: "تعذر إنشاء عنصر العمل.",
    noData: "لا تتوفر بيانات", assignmentError: "تعذر تحديث المسند إليه.",
  },
};
