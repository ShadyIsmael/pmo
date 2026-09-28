"use client";

import dynamic from "next/dynamic";
import { useEffect, useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { AlertTriangle, Bold, Bot, CalendarDays, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, Columns3, Diamond, Eraser, Filter, GripVertical, Hand, IndentDecrease, IndentIncrease, Italic, Link2, Plus, Rows3, Search, Settings2, Trash2, Underline, Users, X } from "lucide-react";
import { useToast } from "@/hooks/use-toast";
import { PLANNING_ACTIVITY_TYPE } from "@/lib/enums";
import { planningApi, planningKeys, type MovePlanActivityInput, type PlanActivityDto, type PlanActivityMovePosition, type PlanActivityType, type PlanAssignmentDto, type PlanDependencyDto, type PlanFontSizeToken, type PlanHighlightColorToken, type PlanTextColorToken, type PlanTextFormatDto, type PlanWbsNodeDto, usePlanningAssignments, usePlanningCalendars, usePlanningResources, usePlanningTablePreferences, useResetPlanningTablePreferences, useSavePlanningTablePreferences } from "@/lib/api/planning";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { buildPlanWorkspaceRows, findWbsNode, removeWbsNode, upsertWbsNode, visiblePlanWorkspaceRows, type PlanWorkspaceRow } from "./plan-workspace-rows";
import { formatPredecessor, parsePredecessor } from "./predecessor-syntax";
import { type GanttZoom } from "./gantt-constants";
import { isPlanFontSizeToken, isPlanHighlightColorToken, isPlanTextColorToken, PLAN_FONT_SIZE_TOKENS, PLAN_HIGHLIGHT_COLOR_TOKENS, PLAN_TEXT_COLOR_TOKENS, planTextFormatClassName } from "./plan-text-format";
import { PlanningRibbonGroup, type PlanningRibbonTab } from "./PlanningRibbonGroup";
import { DEFAULT_PLANNING_COLUMN_IDS, getPlanningColumnDefinition, getPlanningColumnValue, matchesPlanningColumnFilter, PLANNING_COLUMN_DEFINITIONS, type PlanningColumnFilter, type PlanningColumnId } from "./planning-columns";

const PlanTimelineGantt = dynamic(
  () => import("./PlanTimelineGantt").then((module) => module.PlanTimelineGantt),
  { ssr: false, loading: () => <Skeleton className="h-80 w-[560px]" /> },
);

const LazyPlanningTaskDetailsSheet = dynamic(
  () => import("./PlanningTaskDetailsSheet").then((module) => module.PlanningTaskDetailsSheet),
  { ssr: false },
);

const PLANNING_ROW_HEIGHT = 44;
const PLANNING_VIRTUAL_OVERSCAN = 2;
const PLANNING_VIRTUALIZATION_THRESHOLD = 20;

type ActivityPatch = Partial<Pick<PlanActivityDto, "name" | "duration" | "startDate" | "endDate" | "actualStart" | "actualFinish" | "wbsId" | "sortOrder" | "textFormat" | "schedulingMode">> & {
  description?: string | null;
  estimateHours?: number | null;
  progress?: number | null;
  calendarId?: string | null;
  constraintType?: PlanActivityDto["constraintType"];
  constraintDate?: string | null;
  deadline?: string | null;
};
type WbsPatch = Partial<Pick<PlanWbsNodeDto, "name" | "parentWbsId" | "sortOrder" | "textFormat">>;
type NavigationDirection = "next" | "previous" | "up" | "down";
type CellPosition = { rowId: string; column: number };
type DropTarget = { targetId: string; position: PlanActivityMovePosition };
type MoveIntent = Omit<MovePlanActivityInput, "rowVersion" | "wbsRowVersion" | "targetWbsRowVersion">;
type MoveUndo = MoveIntent & { activityId: string };
export type PlanningView = "table" | "gantt";

type PlanningTablePreferencePayload = Omit<import("@/lib/api/planning").PlanningTablePreferenceDto, "planId">;

function normalizePlanningPreferencePayload(payload: PlanningTablePreferencePayload): PlanningTablePreferencePayload {
  const normalizeFilters = Object.fromEntries(
    Object.entries(payload.columnFilters ?? {})
      .filter(([, filter]) => Boolean(filter && (filter.text || filter.min || filter.max || filter.value)))
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, filter]) => [key, { text: filter.text || undefined, min: filter.min || undefined, max: filter.max || undefined, value: filter.value || undefined }]),
  );
  const normalizeWidths = Object.fromEntries(
    Object.entries(payload.columnWidths ?? {})
      .filter(([, width]) => Number.isFinite(width))
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, width]) => [key, width]),
  );
  return {
    visibleColumns: [...new Set(payload.visibleColumns ?? [])],
    columnOrder: [...new Set(payload.columnOrder ?? [])],
    columnWidths: normalizeWidths,
    columnFilters: normalizeFilters,
    collapsedRowIds: [...new Set(payload.collapsedRowIds ?? [])].sort(),
  };
}

function serializePlanningPreferencePayload(payload: PlanningTablePreferencePayload): string {
  return JSON.stringify(normalizePlanningPreferencePayload(payload));
}

function message(error: unknown, fallback: string) { return error instanceof Error ? error.message : fallback; }
function dateValue(value?: string | null) { return value?.slice(0, 10) ?? ""; }
function formatVariance(value: number) { return `${value > 0 ? "+" : ""}${value} ${value === 1 || value === -1 ? "day" : "days"}`; }
function flattenWbsNodes(nodes: PlanWbsNodeDto[]): PlanWbsNodeDto[] {
  const flattened: PlanWbsNodeDto[] = [];
  const visit = (items: PlanWbsNodeDto[]) => {
    for (const item of items) {
      flattened.push(item);
      visit(item.children ?? []);
    }
  };
  visit(nodes);
  return flattened;
}
function compatiblePaste(value: string, type: "text" | "number" | "date", min?: number, max?: number) {
  const trimmed = value.trim();
  if (!trimmed || type === "text") return true;
  if (type === "date") return /^\d{4}-\d{2}-\d{2}$/.test(trimmed);
  if (!/^\d+(?:\.\d+)?$/.test(trimmed)) return false;
  const numeric = Number(trimmed);
  return Number.isFinite(numeric) && (min === undefined || numeric >= min) && (max === undefined || numeric <= max);
}

function activityBody(activity: PlanActivityDto, patch: ActivityPatch) {
  return {
    code: activity.code,
    name: patch.name ?? activity.name,
    description: patch.description !== undefined ? (patch.description?.trim() || null) : activity.description ?? null,
    type: activity.type,
    schedulingMode: patch.schedulingMode ?? activity.schedulingMode,
    constraintType: patch.constraintType ?? activity.constraintType,
    constraintDate: patch.constraintDate === "" ? null : patch.constraintDate ?? activity.constraintDate ?? null,
    deadline: patch.deadline === "" ? null : patch.deadline ?? activity.deadline ?? null,
    startDate: patch.startDate === "" ? null : patch.startDate ?? activity.startDate ?? null,
    endDate: patch.endDate === "" ? null : patch.endDate ?? activity.endDate ?? null,
    actualStart: patch.actualStart === "" ? null : patch.actualStart ?? activity.actualStart ?? null,
    actualFinish: patch.actualFinish === "" ? null : patch.actualFinish ?? activity.actualFinish ?? null,
    calendarId: patch.calendarId !== undefined ? patch.calendarId : activity.calendarId ?? null,
    duration: activity.type === PLANNING_ACTIVITY_TYPE.milestone ? 0 : patch.duration ?? activity.duration,
    estimateHours: patch.estimateHours !== undefined ? patch.estimateHours : activity.estimateHours ?? null,
    progress: patch.progress !== undefined ? patch.progress : activity.progress ?? null,
    wbsId: patch.wbsId ?? activity.wbsId ?? null,
    textFormat: patch.textFormat !== undefined ? patch.textFormat : activity.textFormat ?? null,
    sortOrder: patch.sortOrder ?? activity.sortOrder,
    rowVersion: activity.rowVersion,
  };
}

function changedActivityPatch(activity: PlanActivityDto, patch: ActivityPatch): ActivityPatch {
  const next = { ...patch };
  if (next.startDate !== undefined && dateValue(next.startDate) === dateValue(activity.startDate)) delete next.startDate;
  if (next.endDate !== undefined && dateValue(next.endDate) === dateValue(activity.endDate)) delete next.endDate;
  if (next.actualStart !== undefined && dateValue(next.actualStart) === dateValue(activity.actualStart)) delete next.actualStart;
  if (next.actualFinish !== undefined && dateValue(next.actualFinish) === dateValue(activity.actualFinish)) delete next.actualFinish;
  if (next.duration !== undefined && next.duration === activity.duration) delete next.duration;
  if (next.description !== undefined && (next.description ?? "") === (activity.description ?? "")) delete next.description;
  if (next.estimateHours !== undefined && (next.estimateHours ?? null) === (activity.estimateHours ?? null)) delete next.estimateHours;
  if (next.progress !== undefined && (next.progress ?? null) === (activity.progress ?? null)) delete next.progress;
  if (next.calendarId !== undefined && (next.calendarId ?? null) === (activity.calendarId ?? null)) delete next.calendarId;
  if (next.schedulingMode !== undefined && next.schedulingMode === activity.schedulingMode) delete next.schedulingMode;
  if (next.constraintType !== undefined && next.constraintType === activity.constraintType) delete next.constraintType;
  if (next.constraintDate !== undefined && dateValue(next.constraintDate) === dateValue(activity.constraintDate)) delete next.constraintDate;
  if (next.deadline !== undefined && dateValue(next.deadline) === dateValue(activity.deadline)) delete next.deadline;
  return next;
}

export function PlanningGridEditor({ projectId, planId, editable, wbs, activities, dependencies, loading, view = "table", ganttZoom, ribbonTab = "task", onOpenAssignments, showBaseline = false, revealBaselineColumns = false }: {
  projectId: string;
  planId: string;
  editable: boolean;
  wbs: PlanWbsNodeDto[];
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  loading: boolean;
  view?: PlanningView;
  ganttZoom?: GanttZoom;
  ribbonTab?: PlanningRibbonTab;
  onOpenAssignments?: () => void;
  showBaseline?: boolean;
  revealBaselineColumns?: boolean;
}) {
  const t = useTranslations("projects.planning");
  const common = useTranslations("common");
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState("");
  const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set());
  const [creating, setCreating] = useState(false);
  const [quickTaskCreating, setQuickTaskCreating] = useState(false);
  const [quickTaskName, setQuickTaskName] = useState("");
  const [activeCell, setActiveCell] = useState<CellPosition | null>(null);
  const [columnOrder, setColumnOrder] = useState<PlanningColumnId[]>(() => [...DEFAULT_PLANNING_COLUMN_IDS]);
  const [columnWidths, setColumnWidths] = useState<Map<PlanningColumnId, number>>(() => new Map(PLANNING_COLUMN_DEFINITIONS.map((column) => [column.id, column.initialWidth])));
  const [columnFilters, setColumnFilters] = useState<Record<string, PlanningColumnFilter>>({});
  const [preferenceSaveRevision, setPreferenceSaveRevision] = useState(0);
  const [columnManagerOpen, setColumnManagerOpen] = useState(false);
  const [draggedColumn, setDraggedColumn] = useState<PlanningColumnId | null>(null);
  const [resizingColumn, setResizingColumn] = useState<number | null>(null);
  const [pastingTasks, setPastingTasks] = useState(false);
  const [moving, setMoving] = useState(false);
  const [draggedActivityId, setDraggedActivityId] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<DropTarget | null>(null);
  const [moveUndo, setMoveUndo] = useState<MoveUndo | null>(null);
  const [detailsRowId, setDetailsRowId] = useState<string | null>(null);
  const [contextMenu, setContextMenu] = useState<{ rowId: string; x: number; y: number } | null>(null);
  const rowOperationLock = useRef(false);
  const quickTaskLock = useRef(false);
  const quickTaskInputRef = useRef<HTMLInputElement>(null);
  const cellRefs = useRef(new Map<string, HTMLInputElement>());
  const tableScrollRef = useRef<HTMLDivElement>(null);
  const [tableScrollTop, setTableScrollTop] = useState(0);
  const resizeRef = useRef<{ columnId: PlanningColumnId; startX: number; startWidth: number; pointerId: number } | null>(null);
  const resizeFrame = useRef<number | null>(null);
  const pendingWidth = useRef<number | null>(null);
  const pasteTaskLock = useRef(false);
  const preferenceSaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const preferenceHydratedPlan = useRef<string | null>(null);
  const preferenceHydrated = useRef(false);
  const preferenceDirty = useRef(false);
  const preferenceSaveInFlight = useRef(false);
  const preferenceSaveQueued = useRef(false);
  const lastLoadedPreferencePayload = useRef<string | null>(null);
  const lastSavedPreferencePayload = useRef<string | null>(null);
  const activityQueues = useRef(new Map<string, Promise<void>>());
  const dependencyLocks = useRef(new Set<string>());
  const fullRows = useMemo(() => buildPlanWorkspaceRows(wbs, activities), [activities, wbs]);
  const flatWbs = useMemo(() => flattenWbsNodes(wbs).sort((left, right) => left.sortOrder - right.sortOrder || left.code.localeCompare(right.code)), [wbs]);
  const rows = useMemo(() => visiblePlanWorkspaceRows(fullRows, collapsed), [collapsed, fullRows]);
  const visibleColumns = useMemo(() => {
    const columns = columnOrder.map((id) => getPlanningColumnDefinition(id));
    return columns.sort((left, right) => left.id === "schedulingMode" ? -1 : right.id === "schedulingMode" ? 1 : 0);
  }, [columnOrder]);
  const resourceColumnVisible = columnOrder.includes("resources");
  const calendarColumnVisible = columnOrder.includes("calendar");
  const assignmentsQuery = usePlanningAssignments(projectId, planId, resourceColumnVisible);
  const resourcesQuery = usePlanningResources(resourceColumnVisible);
  const calendarsQuery = usePlanningCalendars(projectId, planId, calendarColumnVisible);
  const tablePreferencesQuery = usePlanningTablePreferences(projectId, planId, Boolean(planId));
  const saveTablePreferences = useSavePlanningTablePreferences(projectId, planId);
  const resetTablePreferences = useResetPlanningTablePreferences(projectId, planId);
  const saveTablePreferencesRef = useRef(saveTablePreferences.mutateAsync);
  saveTablePreferencesRef.current = saveTablePreferences.mutateAsync;
  const assignmentsByActivity = useMemo(() => {
    const grouped = new Map<string, PlanAssignmentDto[]>();
    for (const assignment of assignmentsQuery.data ?? []) {
      const existing = grouped.get(assignment.activityId);
      if (existing) existing.push(assignment);
      else grouped.set(assignment.activityId, [assignment]);
    }
    return grouped;
  }, [assignmentsQuery.data]);
  const resourceNames = useMemo(() => new Map((resourcesQuery.data ?? []).map((resource) => [resource.id, resource.name])), [resourcesQuery.data]);
  const calendarNames = useMemo(() => new Map((calendarsQuery.data ?? []).map((calendar) => [calendar.id, calendar.name])), [calendarsQuery.data]);
  const defaultCalendarName = useMemo(() => calendarsQuery.data?.find((calendar) => calendar.isDefault)?.name ?? calendarsQuery.data?.[0]?.name, [calendarsQuery.data]);
  const predecessorTextByActivity = useMemo(() => {
    const rowNumberByActivity = new Map(fullRows.flatMap((row) => row.activity ? [[row.activity.id, row.rowNumber] as const] : []));
    const dependenciesByActivity = new Map<string, string[]>();
    for (const dependency of dependencies) {
      const predecessorRowNumber = rowNumberByActivity.get(dependency.predecessorActivityId);
      if (predecessorRowNumber === undefined) continue;
      const formatted = formatPredecessor(predecessorRowNumber, dependency.type, dependency.lag);
      const existing = dependenciesByActivity.get(dependency.activityId);
      if (existing) existing.push(formatted);
      else dependenciesByActivity.set(dependency.activityId, [formatted]);
    }
    return new Map([...dependenciesByActivity].map(([activityId, values]) => [activityId, values.join(", ")]));
  }, [dependencies, fullRows]);
  const successorCountByActivity = useMemo(() => {
    const counts = new Map<string, number>();
    for (const dependency of dependencies) counts.set(dependency.predecessorActivityId, (counts.get(dependency.predecessorActivityId) ?? 0) + 1);
    return counts;
  }, [dependencies]);
  const columnValueContext = useMemo(() => ({ predecessorText: (activityId: string) => predecessorTextByActivity.get(activityId) ?? "", successorCountByActivity, assignmentsByActivity, resourceNames, calendarNames, defaultCalendarName }), [assignmentsByActivity, calendarNames, defaultCalendarName, predecessorTextByActivity, resourceNames, successorCountByActivity]);
  const filteredRows = useMemo(() => rows.filter((row) => visibleColumns.every((column) => matchesPlanningColumnFilter(getPlanningColumnValue(column.id, row, columnValueContext), columnFilters[column.id], column.filterType))), [columnFilters, columnValueContext, rows, visibleColumns]);
  const virtualizeRows = filteredRows.length > PLANNING_VIRTUALIZATION_THRESHOLD;
  const renderedRowWindow = useMemo(() => {
    if (!virtualizeRows) return { start: 0, end: filteredRows.length };
    const start = Math.max(0, Math.floor(tableScrollTop / PLANNING_ROW_HEIGHT) - PLANNING_VIRTUAL_OVERSCAN);
    const end = Math.min(filteredRows.length, Math.ceil((tableScrollTop + 640) / PLANNING_ROW_HEIGHT) + PLANNING_VIRTUAL_OVERSCAN);
    return { start, end };
  }, [filteredRows.length, tableScrollTop, virtualizeRows]);
  const renderedRows = virtualizeRows ? filteredRows.slice(renderedRowWindow.start, renderedRowWindow.end) : filteredRows;
  const topSpacerHeight = renderedRowWindow.start * PLANNING_ROW_HEIGHT;
  const bottomSpacerHeight = (filteredRows.length - renderedRowWindow.end) * PLANNING_ROW_HEIGHT;

  useEffect(() => {
    const container = tableScrollRef.current;
    if (!container) return;
    const maxScrollTop = Math.max(0, filteredRows.length * PLANNING_ROW_HEIGHT - container.clientHeight);
    const nextScrollTop = Math.min(container.scrollTop, maxScrollTop);
    if (container.scrollTop !== nextScrollTop) container.scrollTop = nextScrollTop;
    setTableScrollTop(nextScrollTop);
  }, [filteredRows.length, planId]);
  const selected = fullRows.find((row) => row.id === selectedId);
  const detailsRow = detailsRowId ? fullRows.find((row) => row.id === detailsRowId) : undefined;
  const activityKey = planningKeys.activities(projectId, planId);
  const wbsKey = planningKeys.wbs(projectId, planId);
  const dependencyKey = planningKeys.dependencies(projectId, planId);
  const selectedGanttZoom = ganttZoom ?? "day";
  const gridTemplateColumns = visibleColumns.map((column) => `${columnWidths.get(column.id) ?? column.initialWidth}px`).join(" ");
  const stickyOffsets = visibleColumns.reduce<Map<PlanningColumnId, number>>((offsets, column, index) => {
    if (index === 0) offsets.set(column.id, 0);
    else {
      const previous = visibleColumns[index - 1];
      offsets.set(column.id, (offsets.get(previous.id) ?? 0) + (columnWidths.get(previous.id) ?? previous.initialWidth));
    }
    return offsets;
  }, new Map());

  const isCellEditable = (row: PlanWorkspaceRow, column: number) => {
    if (!editable) return false;
    const columnId = visibleColumns[column]?.id;
    if (columnId === "taskName") return Boolean(row.activity || row.wbs);
    if (!row.activity) return false;
    if (row.activity.isSummaryTask && ["start", "finish", "duration", "progress"].includes(columnId ?? "")) return false;
    if (columnId === "duration") return row.activity.type !== PLANNING_ACTIVITY_TYPE.milestone;
    return columnId === "start" || columnId === "finish" || columnId === "actualStart" || columnId === "actualFinish" || columnId === "progress" || columnId === "predecessors" || columnId === "schedulingMode";
  };

  const registerCell = (key: string, element: HTMLInputElement | null) => {
    if (element) cellRefs.current.set(key, element);
    else cellRefs.current.delete(key);
  };

  const focusCell = (position: CellPosition) => {
    setSelectedId(position.rowId);
    setActiveCell(position);
    if (virtualizeRows) {
      const targetIndex = filteredRows.findIndex((item) => item.id === position.rowId);
      const container = tableScrollRef.current;
      if (targetIndex >= 0 && container) {
        const targetTop = targetIndex * PLANNING_ROW_HEIGHT;
        const targetBottom = targetTop + PLANNING_ROW_HEIGHT;
        if (targetTop < container.scrollTop) container.scrollTop = targetTop;
        else if (targetBottom > container.scrollTop + container.clientHeight) container.scrollTop = targetBottom - container.clientHeight;
      }
    }
    requestAnimationFrame(() => {
      requestAnimationFrame(() => cellRefs.current.get(`${position.rowId}:${position.column}`)?.focus());
    });
  };

  const navigateCell = (row: PlanWorkspaceRow, column: number, direction: NavigationDirection) => {
    const rowIndex = filteredRows.findIndex((item) => item.id === row.id);
    if (rowIndex < 0) return;
    if (direction === "up" || direction === "down") {
      const step = direction === "up" ? -1 : 1;
      for (let index = rowIndex + step; index >= 0 && index < filteredRows.length; index += step) {
        if (isCellEditable(filteredRows[index], column)) {
          focusCell({ rowId: filteredRows[index].id, column });
          return;
        }
      }
      return;
    }
    const offset = direction === "next" ? 1 : -1;
    let nextRowIndex = rowIndex;
    let nextColumn = column + offset;
    while (nextRowIndex >= 0 && nextRowIndex < filteredRows.length) {
      while (nextColumn >= 0 && nextColumn < visibleColumns.length) {
        if (isCellEditable(filteredRows[nextRowIndex], nextColumn)) {
          focusCell({ rowId: filteredRows[nextRowIndex].id, column: nextColumn });
          return;
        }
        nextColumn += offset;
      }
      nextRowIndex += offset;
      nextColumn = direction === "next" ? 0 : visibleColumns.length - 1;
    }
  };

  const beginColumnResize = (index: number, event: React.PointerEvent<HTMLSpanElement>) => {
    event.preventDefault();
    event.stopPropagation();
    const column = visibleColumns[index];
    if (!column) return;
    resizeRef.current = { columnId: column.id, startX: event.clientX, startWidth: columnWidths.get(column.id) ?? column.initialWidth, pointerId: event.pointerId };
    setResizingColumn(index);
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const moveColumnResize = (event: React.PointerEvent<HTMLSpanElement>) => {
    const resize = resizeRef.current;
    if (!resize || resize.pointerId !== event.pointerId) return;
    const column = getPlanningColumnDefinition(resize.columnId);
    const nextWidth = Math.min(column.maxWidth, Math.max(column.minWidth, resize.startWidth + event.clientX - resize.startX));
    pendingWidth.current = nextWidth;
    if (resizeFrame.current === null) {
      resizeFrame.current = requestAnimationFrame(() => {
        const width = pendingWidth.current;
        const activeResize = resizeRef.current;
        if (width !== null && activeResize) {
          markPreferencesDirty();
          setColumnWidths((current) => new Map(current).set(activeResize.columnId, width));
        }
        pendingWidth.current = null;
        resizeFrame.current = null;
      });
    }
  };

  const endColumnResize = (event: React.PointerEvent<HTMLSpanElement>) => {
    if (resizeRef.current?.pointerId === event.pointerId && event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    resizeRef.current = null;
    pendingWidth.current = null;
    if (resizeFrame.current !== null) { cancelAnimationFrame(resizeFrame.current); resizeFrame.current = null; }
    setResizingColumn(null);
  };

  const addColumn = (columnId: PlanningColumnId) => {
    markPreferencesDirty();
    setColumnOrder((current) => current.includes(columnId) ? current : [...current, columnId]);
    setColumnManagerOpen(false);
  };

  const removeColumn = (columnId: PlanningColumnId) => {
    const definition = getPlanningColumnDefinition(columnId);
    if (definition.required) return;
    markPreferencesDirty();
    setColumnOrder((current) => current.filter((id) => id !== columnId));
    setColumnFilters((current) => {
      const next = { ...current };
      delete next[columnId];
      return next;
    });
  };

  const updateColumnFilter = (columnId: PlanningColumnId, filter: PlanningColumnFilter | undefined) => {
    markPreferencesDirty();
    setColumnFilters((current) => {
      const next = { ...current };
      if (!filter || (!filter.text && !filter.min && !filter.max && !filter.value)) delete next[columnId];
      else next[columnId] = filter;
      return next;
    });
  };

  const reorderColumn = (sourceId: PlanningColumnId, targetId: PlanningColumnId) => {
    if (sourceId === targetId) return;
    markPreferencesDirty();
    setColumnOrder((current) => {
      if (getPlanningColumnDefinition(sourceId).required) return current;
      const optional = current.filter((id) => !getPlanningColumnDefinition(id).required);
      const sourceIndex = optional.indexOf(sourceId);
      const targetIndex = optional.indexOf(targetId);
      if (sourceIndex < 0) return current;
      const nextOptional = optional.filter((id) => id !== sourceId);
      const insertIndex = targetIndex < 0 ? 0 : Math.max(0, targetIndex - (sourceIndex < targetIndex ? 1 : 0));
      nextOptional.splice(insertIndex, 0, sourceId);
      return [...current.filter((id) => getPlanningColumnDefinition(id).required), ...nextOptional];
    });
  };

  const clearAllFilters = () => { markPreferencesDirty(); setColumnFilters({}); };
  const activeFilterCount = Object.keys(columnFilters).filter((id) => columnOrder.some((columnId) => columnId === id)).length;

  const markPreferencesDirty = () => {
    if (preferenceHydrated.current && preferenceHydratedPlan.current === planId) preferenceDirty.current = true;
  };

  const currentPreferencePayload = useMemo<PlanningTablePreferencePayload>(() => ({
    visibleColumns: columnOrder,
    columnOrder,
    columnWidths: Object.fromEntries(columnWidths),
    columnFilters,
    collapsedRowIds: [...collapsed],
  }), [collapsed, columnFilters, columnOrder, columnWidths]);

  useEffect(() => {
    if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current);
    preferenceHydratedPlan.current = null;
    preferenceHydrated.current = false;
    preferenceDirty.current = false;
    preferenceSaveInFlight.current = false;
    preferenceSaveQueued.current = false;
    lastLoadedPreferencePayload.current = null;
    lastSavedPreferencePayload.current = null;
    setSelectedId("");
    setCollapsed(new Set());
    setDetailsRowId(null);
    setContextMenu(null);
  }, [planId]);

  useEffect(() => {
    if (!planId || !tablePreferencesQuery.isSuccess || preferenceHydratedPlan.current === planId || tablePreferencesQuery.data?.planId !== planId) return;
    const preference = tablePreferencesQuery.data;
    const known = new Set(PLANNING_COLUMN_DEFINITIONS.map((column) => column.id));
    const required = PLANNING_COLUMN_DEFINITIONS.filter((column) => column.required).map((column) => column.id);
    const savedOrder = (preference.columnOrder.length ? preference.columnOrder : preference.visibleColumns).filter((id): id is PlanningColumnId => known.has(id as PlanningColumnId));
    const optional = savedOrder.filter((id) => !required.includes(id));
    const missingOptional = DEFAULT_PLANNING_COLUMN_IDS.filter((id) => !required.includes(id) && optional.includes(id) === false && preference.columnOrder.length === 0);
    setColumnOrder([...required, ...optional, ...missingOptional]);
    setColumnWidths(new Map(PLANNING_COLUMN_DEFINITIONS.map((column) => [column.id, preference.columnWidths[column.id] ?? column.initialWidth])));
    setColumnFilters(preference.columnFilters ?? {});
    setCollapsed(new Set(preference.collapsedRowIds ?? []));
    preferenceHydratedPlan.current = planId;
    preferenceHydrated.current = true;
    preferenceDirty.current = false;
    const serialized = serializePlanningPreferencePayload({
      visibleColumns: preference.visibleColumns,
      columnOrder: preference.columnOrder,
      columnWidths: preference.columnWidths,
      columnFilters: preference.columnFilters,
      collapsedRowIds: preference.collapsedRowIds,
    });
    lastLoadedPreferencePayload.current = serialized;
    lastSavedPreferencePayload.current = serialized;
  }, [planId, tablePreferencesQuery.data, tablePreferencesQuery.isSuccess]);

  useEffect(() => {
    if (!revealBaselineColumns || !preferenceHydrated.current || preferenceHydratedPlan.current !== planId) return;
    const baselineColumns: PlanningColumnId[] = ["baselineStart", "baselineFinish", "baselineDuration", "startVariance", "finishVariance", "durationVariance"];
    setColumnOrder((current) => {
      const missing = baselineColumns.filter((columnId) => !current.includes(columnId));
      return missing.length ? [...current, ...missing] : current;
    });
  }, [planId, revealBaselineColumns, tablePreferencesQuery.isSuccess]);

  useEffect(() => () => { if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current); }, []);

  useEffect(() => {
    if (!preferenceHydrated.current || !preferenceDirty.current || !planId || preferenceHydratedPlan.current !== planId) return;
    const payload = currentPreferencePayload;
    const serialized = serializePlanningPreferencePayload(payload);
    if (serialized === lastLoadedPreferencePayload.current || serialized === lastSavedPreferencePayload.current) {
      preferenceDirty.current = false;
      return;
    }
    if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current);
    preferenceSaveTimer.current = setTimeout(() => {
      preferenceSaveTimer.current = null;
      if (!preferenceDirty.current || preferenceHydratedPlan.current !== planId) return;
      if (preferenceSaveInFlight.current) {
        preferenceSaveQueued.current = true;
        return;
      }
      const latestPayload = currentPreferencePayload;
      const latestSerialized = serializePlanningPreferencePayload(latestPayload);
      if (latestSerialized === lastLoadedPreferencePayload.current || latestSerialized === lastSavedPreferencePayload.current) {
        preferenceDirty.current = false;
        return;
      }
      preferenceSaveInFlight.current = true;
      const requestPlanId = planId;
      void saveTablePreferencesRef.current(latestPayload)
        .then((saved) => {
          if (preferenceHydratedPlan.current !== requestPlanId) return;
          const savedPayload = {
            visibleColumns: saved.visibleColumns,
            columnOrder: saved.columnOrder,
            columnWidths: saved.columnWidths,
            columnFilters: saved.columnFilters,
            collapsedRowIds: saved.collapsedRowIds,
          } satisfies PlanningTablePreferencePayload;
          lastSavedPreferencePayload.current = serializePlanningPreferencePayload(savedPayload);
          lastLoadedPreferencePayload.current = lastSavedPreferencePayload.current;
          preferenceDirty.current = serializePlanningPreferencePayload(currentPreferencePayload) !== lastSavedPreferencePayload.current;
        })
        .catch(() => undefined)
        .finally(() => {
          preferenceSaveInFlight.current = false;
          if (preferenceHydratedPlan.current === requestPlanId && preferenceSaveQueued.current && preferenceDirty.current) {
            preferenceSaveQueued.current = false;
            setPreferenceSaveRevision((current) => current + 1);
          }
        });
    }, 650);
    return () => { if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current); };
  }, [currentPreferencePayload, planId, preferenceSaveRevision]);

  const resetTableLayout = () => {
    if (resetTablePreferences.isPending) return;
    setColumnOrder([...DEFAULT_PLANNING_COLUMN_IDS]);
    setColumnWidths(new Map(PLANNING_COLUMN_DEFINITIONS.map((column) => [column.id, column.initialWidth])));
    setColumnFilters({});
    setCollapsed(new Set());
    void resetTablePreferences.mutateAsync().catch(() => undefined);
  };

  useEffect(() => {
    if (!contextMenu) return;
    const close = (event: PointerEvent) => {
      const target = event.target;
      if (target instanceof Element && target.closest("[data-planning-context-menu]")) return;
      setContextMenu(null);
    };
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === "Escape") setContextMenu(null); };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", onKeyDown);
    return () => { document.removeEventListener("pointerdown", close); document.removeEventListener("keydown", onKeyDown); };
  }, [contextMenu]);

  const notifyError = (error: unknown) => toast({ title: common("error"), description: message(error, t("saveFailed")), variant: "destructive" });
  const notifySaved = () => toast({ title: t("saved"), description: t("updated"), variant: "success" });
  const setActivity = (updated: PlanActivityDto) => queryClient.setQueryData<PlanActivityDto[]>(activityKey, (current) => current?.map((item) => item.id === updated.id ? updated : item));

  const activityForWbs = (wbsId?: string | null) => {
    const node = flatWbs.find((item) => item.id === wbsId);
    return (node?.activityId ? activities.find((item) => item.id === node.activityId) : undefined)
      ?? activities.find((item) => item.wbsId === wbsId);
  };
  const siblingWbsNodes = (parentWbsId?: string | null) => flatWbs.filter((node) => (node.parentWbsId ?? null) === (parentWbsId ?? null)).sort((left, right) => left.sortOrder - right.sortOrder || left.code.localeCompare(right.code));

  const buildUndoDescriptor = (activity: PlanActivityDto, sourceNode?: PlanWbsNodeDto): MoveUndo | null => {
    if (sourceNode) {
      const siblings = siblingWbsNodes(sourceNode.parentWbsId);
      const sourceIndex = siblings.findIndex((node) => node.id === sourceNode.id);
      const next = siblings.slice(sourceIndex + 1).map((node) => activityForWbs(node.id)).find(Boolean);
      const previous = siblings.slice(0, sourceIndex).reverse().map((node) => activityForWbs(node.id)).find(Boolean);
      if (next) return { activityId: activity.id, targetActivityId: next.id, position: "Before" };
      if (previous) return { activityId: activity.id, targetActivityId: previous.id, position: "After" };
      return { activityId: activity.id, targetWbsId: sourceNode.parentWbsId ?? undefined, position: "Child", sortOrder: sourceNode.sortOrder };
    }
    const roots = activities.filter((item) => !item.wbsId).sort((left, right) => left.sortOrder - right.sortOrder || left.code.localeCompare(right.code));
    const sourceIndex = roots.findIndex((item) => item.id === activity.id);
    const next = roots[sourceIndex + 1];
    const previous = roots[sourceIndex - 1];
    if (next) return { activityId: activity.id, targetActivityId: next.id, position: "Before" };
    if (previous) return { activityId: activity.id, targetActivityId: previous.id, position: "After" };
    return null;
  };

  const executeMove = async (activityId: string, input: MoveIntent, undoDescriptor?: MoveUndo | null) => {
    if (!editable || moving || rowOperationLock.current) return false;
    const cachedActivities = queryClient.getQueryData<PlanActivityDto[]>(activityKey) ?? activities;
    const activity = cachedActivities.find((item) => item.id === activityId);
    if (!activity) return false;
    const cachedWbs = queryClient.getQueryData<PlanWbsNodeDto[]>(wbsKey) ?? wbs;
    const cachedFlatWbs = flattenWbsNodes(cachedWbs);
    const sourceNode = activity.wbsId ? cachedFlatWbs.find((node) => node.id === activity.wbsId) : undefined;
    const targetActivity = input.targetActivityId ? cachedActivities.find((item) => item.id === input.targetActivityId) : undefined;
    const targetNode = targetActivity?.wbsId ? cachedFlatWbs.find((node) => node.id === targetActivity.wbsId) : undefined;
    const payload: MovePlanActivityInput = {
      ...input,
      rowVersion: activity.rowVersion,
      wbsRowVersion: sourceNode?.rowVersion ?? "",
      targetWbsRowVersion: targetNode?.rowVersion,
    };
    rowOperationLock.current = true;
    setMoving(true);
    try {
      await planningApi.moveActivity(projectId, planId, activityId, payload);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: activityKey }),
        queryClient.invalidateQueries({ queryKey: wbsKey }),
      ]);
      setSelectedId(`activity:${activityId}`);
      if (undoDescriptor) setMoveUndo(undoDescriptor);
      return true;
    } catch (error) {
      notifyError(error);
      return false;
    } finally {
      setMoving(false);
      rowOperationLock.current = false;
    }
  };

  const moveActivity = async (target: DropTarget, sourceId = draggedActivityId) => {
    if (!sourceId || target.targetId === sourceId) return;
    const source = (queryClient.getQueryData<PlanActivityDto[]>(activityKey) ?? activities).find((item) => item.id === sourceId);
    const targetActivity = (queryClient.getQueryData<PlanActivityDto[]>(activityKey) ?? activities).find((item) => item.id === target.targetId);
    if (!source || !targetActivity) return;
    const sourceNode = source.wbsId ? flatWbs.find((node) => node.id === source.wbsId) : undefined;
    const undo = buildUndoDescriptor(source, sourceNode);
    const succeeded = await executeMove(sourceId, { targetActivityId: targetActivity.id, position: target.position }, undo);
    setDraggedActivityId(null);
    setDropTarget(null);
    if (succeeded) toast({ title: t("activityMoved"), action: <button type="button" className="rounded border px-2 py-1 text-xs font-medium hover:bg-accent" onClick={() => void undoLastMove(undo)}>{t("undo")}</button>, duration: 8000 });
  };

  const undoLastMove = async (requestedUndo?: MoveUndo | null) => {
    const undo = requestedUndo ?? moveUndo;
    if (!undo) return;
    const { activityId, ...input } = undo;
    const succeeded = await executeMove(activityId, input, null);
    if (succeeded) setMoveUndo(null);
  };

  const commitActivity = (activityId: string, patch: ActivityPatch, quiet = false) => {
    const prior = activityQueues.current.get(activityId) ?? Promise.resolve();
    const queued = prior.catch(() => undefined).then(async () => {
      const cachedActivities = queryClient.getQueryData<PlanActivityDto[]>(activityKey);
      const latest = cachedActivities?.find((item) => item.id === activityId);
      if (!latest) return;
      const actualStart = patch.actualStart === "" ? null : patch.actualStart ?? latest.actualStart ?? null;
      const actualFinish = patch.actualFinish === "" ? null : patch.actualFinish ?? latest.actualFinish ?? null;
      if (actualFinish && !actualStart) throw new Error(t("actualFinishRequiresStart"));
      const constraintType = patch.constraintType ?? latest.constraintType;
      const constraintDate = patch.constraintDate === "" ? null : patch.constraintDate ?? latest.constraintDate ?? null;
      if (constraintType && constraintType !== "AsSoonAsPossible" && constraintType !== "AsLateAsPossible" && !constraintDate) {
        throw new Error(t("constraintDateRequired"));
      }
      const updated = await planningApi.updateActivity(projectId, planId, activityId, activityBody(latest, patch));
      setActivity(updated);
      // The API persists the changed leaf and all affected summary ancestors,
      // but its mutation response is intentionally the changed activity only.
      // Refetch the authoritative plan collection so the grid, Gantt and an
      // already-open summary drawer all consume the same backend values.
      await queryClient.refetchQueries({ queryKey: activityKey, type: "active" });
      if (!quiet) notifySaved();
    });
    activityQueues.current.set(activityId, queued);
    void queued.catch(notifyError).finally(() => { if (activityQueues.current.get(activityId) === queued) activityQueues.current.delete(activityId); });
    return queued;
  };

  const updateWbs = async (node: PlanWbsNodeDto, patch: WbsPatch, optimistic = false) => {
    const previous = queryClient.getQueryData<PlanWbsNodeDto[]>(wbsKey) ?? wbs;
    const candidate = { ...node, ...patch };
    const parentWbsId = "parentWbsId" in patch ? patch.parentWbsId ?? null : node.parentWbsId ?? null;
    if (optimistic) queryClient.setQueryData(wbsKey, upsertWbsNode(previous, candidate));
    try {
      const updated = await planningApi.updateWbs(projectId, planId, node.id, { code: node.code, name: patch.name ?? node.name, parentWbsId, sortOrder: patch.sortOrder ?? node.sortOrder, textFormat: patch.textFormat !== undefined ? patch.textFormat : node.textFormat ?? null, rowVersion: node.rowVersion });
      queryClient.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => upsertWbsNode(current ?? previous, updated));
      return updated;
    } catch (error) {
      if (optimistic) queryClient.setQueryData(wbsKey, previous);
      throw error;
    }
  };

  const commitName = async (row: PlanWorkspaceRow, name: string) => {
    if (row.activity) await commitActivity(row.activity.id, { name }, true);
    if (row.wbs) await updateWbs(row.wbs, { name });
    notifySaved();
  };

  const updateTextFormat = async (format: PlanTextFormatDto | null) => {
    if (!editable || !selected || rowOperationLock.current) return;
    rowOperationLock.current = true;
    setMoving(true);
    try {
      if (selected.activity) await commitActivity(selected.activity.id, { textFormat: format }, true);
      if (selected.wbs) {
        const latest = findWbsNode(queryClient.getQueryData<PlanWbsNodeDto[]>(wbsKey) ?? wbs, selected.wbs.id) ?? selected.wbs;
        await updateWbs(latest, { textFormat: format });
      }
      notifySaved();
    } catch (error) {
      notifyError(error);
    } finally {
      setMoving(false);
      rowOperationLock.current = false;
    }
  };

  const selectedFormat = selected?.textFormat;
  const toggleFormat = (property: "isBold" | "isItalic" | "isUnderline") => {
    if (!selectedFormat) return updateTextFormat({ isBold: property === "isBold", isItalic: property === "isItalic", isUnderline: property === "isUnderline" });
    return updateTextFormat({ ...selectedFormat, [property]: !selectedFormat[property] });
  };

  const changeTextColor = (value: string) => {
    const textColorToken: PlanTextColorToken | undefined = isPlanTextColorToken(value) ? value : undefined;
    return updateTextFormat(selectedFormat ? { ...selectedFormat, textColorToken } : { isBold: false, isItalic: false, isUnderline: false, textColorToken });
  };

  const changeHighlight = (value: string) => {
    const highlightColorToken: PlanHighlightColorToken | undefined = isPlanHighlightColorToken(value) ? value : undefined;
    return updateTextFormat(selectedFormat ? { ...selectedFormat, highlightColorToken } : { isBold: false, isItalic: false, isUnderline: false, highlightColorToken });
  };

  const changeFontSize = (value: string) => {
    const fontSizeToken: PlanFontSizeToken | undefined = isPlanFontSizeToken(value) ? value : undefined;
    return updateTextFormat(selectedFormat ? { ...selectedFormat, fontSizeToken } : { isBold: false, isItalic: false, isUnderline: false, fontSizeToken });
  };

  const createRow = async (type: PlanActivityType, placement: "append" | "above" | "below", nameOverride?: string, anchorOverride?: PlanWorkspaceRow): Promise<boolean> => {
    if (!editable || creating || rowOperationLock.current) return false;
    rowOperationLock.current = true;
    setCreating(true);
    const anchor = placement === "append" ? undefined : anchorOverride ?? selected;
    const parentWbsId = anchor?.wbs?.parentWbsId;
    const sortOrder = anchor ? anchor.wbs?.sortOrder ?? anchor.activity?.sortOrder ?? 0 : Math.max(-1, ...fullRows.filter((row) => row.depth === 0).map((row) => row.wbs?.sortOrder ?? row.activity?.sortOrder ?? 0)) + 1;
    const placedOrder = placement === "above" ? sortOrder - 1 : placement === "below" ? sortOrder + 1 : sortOrder;
    const code = `${type === PLANNING_ACTIVITY_TYPE.milestone ? "M" : "T"}-${crypto.randomUUID().slice(0, 8).toUpperCase()}`;
    let createdNode: PlanWbsNodeDto | undefined;
    try {
      const node = await planningApi.createWbs(projectId, planId, { code, name: nameOverride?.trim() || (type === PLANNING_ACTIVITY_TYPE.milestone ? t("newMilestone") : t("newTask")), parentWbsId: parentWbsId ?? null, sortOrder: placedOrder });
      createdNode = node;
      queryClient.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => upsertWbsNode(current ?? [], node));
      const createdActivity = await planningApi.createActivity(projectId, planId, { code, name: node.name, type, duration: 0, progress: 0, wbsId: node.id, sortOrder: placedOrder });
      queryClient.setQueryData<PlanActivityDto[]>(activityKey, (current) => [...(current ?? []), createdActivity]);
      setSelectedId(`activity:${createdActivity.id}`);
      void queryClient.invalidateQueries({ queryKey: activityKey });
      return true;
    } catch (error) {
      if (createdNode) {
        const nodeId = createdNode.id;
        try {
          await planningApi.deleteWbs(projectId, planId, nodeId, createdNode.rowVersion);
          queryClient.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => removeWbsNode(current ?? [], nodeId));
        } catch { /* the original create error remains the actionable failure */ }
      }
      notifyError(error);
      return false;
    } finally { setCreating(false); rowOperationLock.current = false; }
  };

  const createPastedTasks = async (names: string[]) => {
    if (!editable || creating || pastingTasks || pasteTaskLock.current) return;
    pasteTaskLock.current = true;
    setPastingTasks(true);
    let createdCount = 0;
    try {
      for (const name of names) {
        if (await createRow(PLANNING_ACTIVITY_TYPE.task, "append", name)) createdCount += 1;
        else break;
      }
      if (createdCount > 0) toast({ title: t("created"), description: t("updated"), variant: "success" });
    } finally {
      setPastingTasks(false);
      pasteTaskLock.current = false;
    }
  };

  const createQuickTask = async () => {
    const name = quickTaskName.trim();
    if (!editable || !name || quickTaskCreating || quickTaskLock.current) return;
    quickTaskLock.current = true;
    setQuickTaskCreating(true);
    try {
      const created = await createRow(PLANNING_ACTIVITY_TYPE.task, "append", name);
      if (created) {
        setQuickTaskName("");
        requestAnimationFrame(() => quickTaskInputRef.current?.focus());
      }
    } finally {
      setQuickTaskCreating(false);
      quickTaskLock.current = false;
    }
  };

  const ensureWbs = async (row: PlanWorkspaceRow) => {
    if (row.wbs) return row.wbs;
    if (!row.activity) return undefined;
    const created = await planningApi.createWbs(projectId, planId, { code: row.activity.code, name: row.activity.name, parentWbsId: null, sortOrder: row.activity.sortOrder });
    queryClient.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => upsertWbsNode(current ?? [], created));
    await commitActivity(row.activity.id, { wbsId: created.id }, true);
    return created;
  };

  const indent = async (target = selected) => {
    if (!target?.activity) return;
    const index = rows.findIndex((row) => row.id === target.id);
    const parentRow = index > 0 ? [...rows.slice(0, index)].reverse().find((row) => row.activity && row.id !== target.id) : undefined;
    if (!parentRow?.activity) return;
    await moveActivity({ targetId: parentRow.activity.id, position: "Child" }, target.activity.id);
  };

  const outdent = async (target = selected) => {
    if (!target?.activity || !target.wbs?.parentWbsId) return;
    const parent = findWbsNode(wbs, target.wbs.parentWbsId);
    const parentActivity = activityForWbs(parent?.id);
    const nextSibling = siblingWbsNodes(parent?.parentWbsId).filter((node) => node.id !== parent?.id).map((node) => activityForWbs(node.id)).find(Boolean);
    if (nextSibling) await moveActivity({ targetId: nextSibling.id, position: "Before" }, target.activity.id);
    else if (parentActivity) await moveActivity({ targetId: parentActivity.id, position: "After" }, target.activity.id);
    else if (parent) await executeMove(target.activity.id, { targetWbsId: parent.parentWbsId ?? undefined, position: "Child", sortOrder: parent.sortOrder + 1 }, buildUndoDescriptor(target.activity, target.wbs));
    else toast({ title: common("error"), description: t("invalidMove"), variant: "destructive" });
  };

  const moveSibling = async (direction: "up" | "down", target = selected) => {
    if (!target?.activity) return;
    if (target.wbs) {
      const siblings = siblingWbsNodes(target.wbs.parentWbsId);
      const index = siblings.findIndex((node) => node.id === target.wbs!.id);
      const step = direction === "up" ? -1 : 1;
      for (let cursor = index + step; cursor >= 0 && cursor < siblings.length; cursor += step) {
        const siblingActivity = activityForWbs(siblings[cursor].id);
        if (siblingActivity) {
          await moveActivity({ targetId: siblingActivity.id, position: direction === "up" ? "Before" : "After" }, target.activity.id);
          return;
        }
      }
      return;
    }
    const roots = activities.filter((item) => !item.wbsId).sort((left, right) => left.sortOrder - right.sortOrder || left.code.localeCompare(right.code));
    const index = roots.findIndex((item) => item.id === target.activity!.id);
    const sibling = roots[index + (direction === "up" ? -1 : 1)];
    if (sibling) await moveActivity({ targetId: sibling.id, position: direction === "up" ? "Before" : "After" }, target.activity.id);
  };

  const removeSelected = async (target = selected) => {
    if (!editable || !target || creating || moving || rowOperationLock.current) return;
    if (target.hasChildren) { toast({ title: common("error"), description: t("deleteSummaryFirst"), variant: "destructive" }); return; }
    if (!window.confirm(t("confirmDelete"))) return;
    rowOperationLock.current = true;
    setMoving(true);
    let succeeded = false;
    try {
      if (target.activity) {
        const activityId = target.activity.id;
        const related = dependencies.filter((item) => item.activityId === activityId || item.predecessorActivityId === activityId);
        for (const dependency of related) await planningApi.deleteDependency(projectId, planId, dependency.id, dependency.rowVersion);
        await planningApi.deleteActivity(projectId, planId, activityId, target.activity.rowVersion);
        queryClient.setQueryData<PlanActivityDto[]>(activityKey, (current) => current?.filter((item) => item.id !== activityId));
        queryClient.setQueryData<PlanDependencyDto[]>(dependencyKey, (current) => current?.filter((item) => !related.some((removed) => removed.id === item.id)));
      }
      if (target.wbs) {
        const latest = findWbsNode(queryClient.getQueryData<PlanWbsNodeDto[]>(wbsKey) ?? wbs, target.wbs.id) ?? target.wbs;
        await planningApi.deleteWbs(projectId, planId, latest.id, latest.rowVersion);
        queryClient.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => removeWbsNode(current ?? [], latest.id));
      }
      setSelectedId("");
      succeeded = true;
    } catch (error) { notifyError(error); }
    finally {
      if (!succeeded) {
        void queryClient.invalidateQueries({ queryKey: activityKey });
        void queryClient.invalidateQueries({ queryKey: wbsKey });
        void queryClient.invalidateQueries({ queryKey: dependencyKey });
      }
      setMoving(false);
      rowOperationLock.current = false;
    }
  };

  const predecessorText = (activityId: string) => columnValueContext.predecessorText(activityId);

  const savePredecessors = async (activity: PlanActivityDto, value: string) => {
    if (dependencyLocks.current.has(activity.id)) return;
    const tokens = value.split(",").map((token) => token.trim()).filter(Boolean);
    const targets: Array<{ parsed: NonNullable<ReturnType<typeof parsePredecessor>>; predecessor: PlanActivityDto }> = [];
    const predecessorIds = new Set<string>();
    for (const token of tokens) {
      const parsed = parsePredecessor(token);
      if (!parsed) throw new Error(t("predecessorSyntaxError"));
      const predecessor = fullRows.find((row) => row.rowNumber === parsed.rowNumber)?.activity;
      if (!predecessor) throw new Error(t("predecessorRowError"));
      if (predecessor.id === activity.id) throw new Error(t("predecessorSelfError"));
      if (predecessorIds.has(predecessor.id)) throw new Error(t("predecessorDuplicateError"));
      predecessorIds.add(predecessor.id);
      targets.push({ parsed, predecessor });
    }
    dependencyLocks.current.add(activity.id);
    let succeeded = false;
    try {
      const result = await planningApi.replacePredecessors(projectId, planId, {
        activityId: activity.id,
        predecessors: targets.map((target) => ({
          predecessorActivityId: target.predecessor.id,
          type: target.parsed.type,
          lag: target.parsed.lag ?? null,
        })),
      });
      queryClient.setQueryData<PlanDependencyDto[]>(dependencyKey, result.dependencies);
      if (result.scheduledActivities.length > 0) {
        const scheduledById = new Map(result.scheduledActivities.map((scheduled) => [scheduled.activityId, scheduled]));
        queryClient.setQueryData<PlanActivityDto[]>(activityKey, (current) => current?.map((item) => {
          const scheduled = scheduledById.get(item.id);
          return scheduled ? { ...item, startDate: scheduled.startDate, endDate: scheduled.endDate, duration: scheduled.duration } : item;
        }));
      }
      if (result.schedulingRecalculated) {
        // The dependency endpoint schedules the successor chain and rolls up
        // affected summaries before returning. Refresh both authoritative
        // collections before releasing the editor lock so Grid, Gantt and an
        // open task sheet cannot render the pre-schedule child/summary dates.
        await Promise.all([
          queryClient.refetchQueries({ queryKey: activityKey, type: "active" }),
          queryClient.refetchQueries({ queryKey: dependencyKey, type: "active" }),
        ]);
      }
      succeeded = true;
      notifySaved();
    } finally {
      dependencyLocks.current.delete(activity.id);
      if (!succeeded) {
        void queryClient.invalidateQueries({ queryKey: dependencyKey });
        void queryClient.invalidateQueries({ queryKey: activityKey });
      }
    }
  };

  const openDetails = (row: PlanWorkspaceRow) => {
    if (!row.activity) return;
    setSelectedId(row.id);
    setActiveCell(null);
    setContextMenu(null);
    setDetailsRowId(row.id);
  };

  const toggleSelectedCollapse = () => {
    if (!selected?.wbs || !selected.hasChildren) return;
    markPreferencesDirty();
    setCollapsed((current) => {
      const next = new Set(current);
      if (next.has(selected.wbs!.id)) next.delete(selected.wbs!.id);
      else next.add(selected.wbs!.id);
      return next;
    });
  };

  const collapsibleWbsIds = useMemo(() => fullRows.filter((row) => row.wbs && row.hasChildren).map((row) => row.wbs!.id), [fullRows]);
  const collapseAll = () => { markPreferencesDirty(); setCollapsed(new Set(collapsibleWbsIds)); };
  const expandAll = () => { markPreferencesDirty(); setCollapsed(new Set()); };

  const openContextMenu = (row: PlanWorkspaceRow, event: React.MouseEvent<HTMLElement>) => {
    if (!row.activity && !row.wbs) return;
    event.preventDefault();
    setSelectedId(row.id);
    setActiveCell(null);
    setContextMenu({ rowId: row.id, x: event.clientX, y: event.clientY });
  };

  const handleDragStart = (event: React.DragEvent<HTMLButtonElement>, activityId: string) => {
    if (!editable || moving) { event.preventDefault(); return; }
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", activityId);
    setSelectedId(`activity:${activityId}`);
    setDraggedActivityId(activityId);
    setDropTarget(null);
  };

  const handleDropZoneDragOver = (event: React.DragEvent<HTMLDivElement>, targetId: string, position: PlanActivityMovePosition) => {
    if (!draggedActivityId || draggedActivityId === targetId || moving) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
    setDropTarget({ targetId, position });
    const container = tableScrollRef.current;
    if (container) {
      const edge = 72;
      if (event.clientY < container.getBoundingClientRect().top + edge) container.scrollBy({ top: -18 });
      else if (event.clientY > container.getBoundingClientRect().bottom - edge) container.scrollBy({ top: 18 });
    }
  };

  const handleDropZoneDrop = (event: React.DragEvent<HTMLDivElement>, targetId: string, position: PlanActivityMovePosition) => {
    event.preventDefault();
    void moveActivity({ targetId, position });
  };

  const handleDragEnd = () => { setDraggedActivityId(null); setDropTarget(null); };

  const contextRow = contextMenu ? fullRows.find((row) => row.id === contextMenu.rowId) : undefined;

  const renderColumnCell = (row: PlanWorkspaceRow, columnId: PlanningColumnId, columnIndex: number) => {
    const activity = row.activity;
    const cellKey = (column: number) => `${row.id}:${column}`;
    const commonCellProps = { rowId: row.id, column: columnIndex, active: activeCell?.rowId === row.id && activeCell.column === columnIndex, inputRef: (element: HTMLInputElement | null) => registerCell(cellKey(columnIndex), element), onFocus: () => { setSelectedId(row.id); setActiveCell({ rowId: row.id, column: columnIndex }); }, onNavigate: (direction: NavigationDirection) => navigateCell(row, columnIndex, direction), disabled: !editable || !activity, ariaLabel: t(getPlanningColumnDefinition(columnId).labelKey) };
    if (columnId === "taskName") return <InlineCell {...commonCellProps} onMultiLinePaste={createPastedTasks} onPasteRejected={() => toast({ title: common("error"), description: t("pasteInvalid"), variant: "destructive" })} value={activity?.name ?? row.wbs?.name ?? ""} disabled={!editable || pastingTasks || (!activity && !row.wbs)} onCommit={(value) => commitName(row, value)} />;
    if (columnId === "schedulingMode") {
      if (!activity) return <span className="w-full text-center text-muted-foreground">—</span>;
      const mode = activity.schedulingMode ?? "Automatic";
      const label = mode === "Automatic" ? t("automatic") : t("manual");
      const Icon = mode === "Automatic" ? Bot : Hand;
      return <button type="button" disabled={!editable} aria-label={`${t("schedulingMode")}: ${label}`} title={`${t("schedulingMode")}: ${label}`} aria-pressed={mode === "Automatic"} onClick={(event) => { event.stopPropagation(); void commitActivity(activity.id, { schedulingMode: mode === "Automatic" ? "Manual" : "Automatic" }); }} className={`mx-auto flex h-8 w-8 items-center justify-center rounded border border-transparent hover:border-input hover:bg-background focus:border-primary focus:bg-background disabled:cursor-default disabled:opacity-100 ${mode === "Automatic" ? "text-primary" : "text-amber-600 dark:text-amber-400"}`}><Icon className="h-4 w-4" aria-hidden="true" /></button>;
    }
    if (columnId === "schedulingStatus") {
      if (!activity) return <span className="w-full text-center text-muted-foreground">—</span>;
      const statuses = String(getPlanningColumnValue(columnId, row, columnValueContext) ?? "").split(" ").filter(Boolean);
      const hasLinks = statuses.includes("Predecessors");
      return <div className="flex items-center gap-1" title={statuses.join(" · ")} aria-label={statuses.join(" ")}>
        {statuses.includes("Manual") && <Hand className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" aria-hidden="true" />}
        {statuses.includes("Automatic") && <Bot className="h-3.5 w-3.5 text-primary" aria-hidden="true" />}
        {statuses.includes("Constraint") && <span className="rounded border border-blue-500/30 px-1 text-[9px] text-blue-700 dark:text-blue-300">C</span>}
        {statuses.includes("DeadlineExceeded") && <AlertTriangle className="h-3.5 w-3.5 text-destructive" aria-hidden="true" />}
        {statuses.includes("SchedulingConflict") && <AlertTriangle className="h-3.5 w-3.5 text-destructive" aria-label={t("schedulingConflict")} />}
        {statuses.includes("Critical") && <span className="h-2 w-2 rounded-full bg-destructive" aria-label={t("critical")} />}
        {hasLinks && <Link2 className="h-3.5 w-3.5 text-muted-foreground" aria-hidden="true" />}
        {statuses.includes("Successors") && <span className="text-[10px] text-muted-foreground" aria-label={t("successors")}>›</span>}
        {statuses.includes("CalendarOverride") && <CalendarDays className="h-3.5 w-3.5 text-muted-foreground" aria-hidden="true" />}
      </div>;
    }
    if (columnId === "constraintType") {
      if (!activity) return <span className="w-full text-center text-muted-foreground">—</span>;
      const values = getPlanningColumnDefinition("constraintType").enumValues ?? [];
      return <select value={activity.constraintType ?? "AsSoonAsPossible"} disabled={!editable} aria-label={t("columns.constraintType")} onChange={(event) => void commitActivity(activity.id, { constraintType: event.target.value as PlanActivityDto["constraintType"] })} className="h-8 w-full rounded border bg-background px-1 text-xs"><option value="">{t("automatic")}</option>{values.map((value) => <option key={value} value={value}>{t(`constraintTypes.${value}`)}</option>)}</select>;
    }
     if (columnId === "duration") return <InlineCell {...commonCellProps} type="number" value={activity ? String(activity.duration) : ""} disabled={!editable || !activity || activity.isSummaryTask || activity.type === PLANNING_ACTIVITY_TYPE.milestone} ariaLabel={t("durationShort")} onPasteRejected={() => toast({ title: common("error"), description: t("pasteInvalid"), variant: "destructive" })} onCommit={(value) => activity ? commitActivity(activity.id, { duration: Number(value) }) : Promise.resolve()} />;
     if (columnId === "start" || columnId === "finish" || columnId === "actualStart" || columnId === "actualFinish" || columnId === "constraintDate" || columnId === "deadline") return <InlineCell {...commonCellProps} type="date" value={dateValue(columnId === "start" ? activity?.startDate : columnId === "finish" ? activity?.endDate : columnId === "actualStart" ? activity?.actualStart : columnId === "actualFinish" ? activity?.actualFinish : columnId === "constraintDate" ? activity?.constraintDate : activity?.deadline)} disabled={!editable || !activity || (activity.isSummaryTask && (columnId === "start" || columnId === "finish"))} ariaLabel={t(getPlanningColumnDefinition(columnId).labelKey)} onPasteRejected={() => toast({ title: common("error"), description: t("pasteInvalid"), variant: "destructive" })} onCommit={(value) => { if (!activity) return Promise.resolve(); if (columnId === "start") return commitActivity(activity.id, { startDate: value }); if (columnId === "finish") return commitActivity(activity.id, { endDate: value }); if (columnId === "actualStart") return commitActivity(activity.id, { actualStart: value }); if (columnId === "actualFinish") return commitActivity(activity.id, { actualFinish: value }); if (columnId === "constraintDate") return commitActivity(activity.id, { constraintDate: value }); return commitActivity(activity.id, { deadline: value }); }} />;
     if (columnId === "progress") {
       const progress = activity?.progress == null ? undefined : Math.max(0, Math.min(100, activity.progress));
       return <div className="flex min-w-0 w-full items-center gap-1" title={progress === undefined ? undefined : `${progress}%`}>
         {activity && <Progress value={progress} aria-label={t("complete")} className="h-1.5 min-w-8 flex-1" />}
         <div className={activity ? "min-w-0 w-12" : "w-full"}><InlineCell {...commonCellProps} type="number" value={progress == null ? "" : String(progress)} disabled={!editable || !activity || activity.isSummaryTask} ariaLabel={t("complete")} min={0} max={100} onPasteRejected={() => toast({ title: common("error"), description: t("pasteInvalid"), variant: "destructive" })} onCommit={(value) => activity ? commitActivity(activity.id, { progress: value === "" ? null : Number(value) }) : Promise.resolve()} /></div>
       </div>;
     }
    if (columnId === "predecessors") return <InlineCell {...commonCellProps} value={activity ? columnValueContext.predecessorText(activity.id) : ""} disabled={!editable || !activity} ariaLabel={t("predecessors")} placeholder={t("predecessorPlaceholder")} onPasteRejected={() => toast({ title: common("error"), description: t("pasteInvalid"), variant: "destructive" })} onCommit={async (value) => { if (activity) await savePredecessors(activity, value); }} />;
    const value = getPlanningColumnValue(columnId, row, columnValueContext);
    if (columnId === "startVariance" || columnId === "finishVariance" || columnId === "durationVariance") {
      return value === undefined || value === null ? "—" : formatVariance(Number(value));
    }
    if (columnId === "type" && value === "Task") return t("task");
    if (columnId === "type" && value === "Milestone") return t("milestone");
    if (columnId === "critical" && value === "Yes") return common("yes");
    if (columnId === "critical" && value === "No") return common("no");
    return value === undefined || value === null || value === "" ? "—" : String(value);
  };

  if (loading) return <div className="p-4"><Skeleton className="h-96 w-full" /></div>;
  const visualSelectedId = contextMenu?.rowId ?? selectedId;
  const canToggleSelectedCollapse = Boolean(selected?.wbs && selected.hasChildren);
  const selectedIsCollapsed = selected?.wbs ? collapsed.has(selected.wbs.id) : false;
  return <div className="planning-grid font-planning text-[13px] leading-5">
    {ribbonTab !== "project" && ribbonTab !== "view" && <div className="flex min-w-max items-stretch gap-2 overflow-x-auto border-b bg-muted/20 px-2 py-1.5 text-[12px]">
      {ribbonTab === "task" && <>
      <PlanningRibbonGroup label={t("ribbon.task")}>
        <Button size="sm" variant="secondary" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || creating} onClick={() => void createRow(PLANNING_ACTIVITY_TYPE.task, "append")}><Plus className="h-3.5 w-3.5" />{t("addTask")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || creating} onClick={() => void createRow(PLANNING_ACTIVITY_TYPE.milestone, "append")}><Diamond className="h-3.5 w-3.5" />{t("addMilestone")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || creating} onClick={() => void createRow(PLANNING_ACTIVITY_TYPE.task, "above")}><Rows3 className="h-3.5 w-3.5" />{t("insertAbove")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || creating} onClick={() => void createRow(PLANNING_ACTIVITY_TYPE.task, "below")}><Rows3 className="h-3.5 w-3.5 rotate-180" />{t("insertBelow")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || moving} onClick={() => void removeSelected()}><Trash2 className="h-3.5 w-3.5 text-destructive" />{common("delete")}</Button>
      </PlanningRibbonGroup>
      <PlanningRibbonGroup label={t("ribbon.hierarchy")}>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || moving} onClick={() => void indent()}><IndentIncrease className="h-3.5 w-3.5" />{t("indent")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected?.wbs?.parentWbsId || moving} onClick={() => void outdent()}><IndentDecrease className="h-3.5 w-3.5" />{t("outdent")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected?.activity || moving} onClick={() => void moveSibling("up")}><ChevronDown className="h-3.5 w-3.5 rotate-180" />{t("moveUp")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected?.activity || moving} onClick={() => void moveSibling("down")}><ChevronDown className="h-3.5 w-3.5" />{t("moveDown")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!canToggleSelectedCollapse} aria-pressed={selectedIsCollapsed} onClick={toggleSelectedCollapse}>{selectedIsCollapsed ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}{selectedIsCollapsed ? t("expand") : t("collapse")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!collapsibleWbsIds.length || moving} onClick={collapseAll}><ChevronsDownUp className="h-3.5 w-3.5" />{t("collapseAll")}</Button>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!collapsibleWbsIds.length || moving} onClick={expandAll}><ChevronsUpDown className="h-3.5 w-3.5" />{t("expandAll")}</Button>
      </PlanningRibbonGroup>
      <PlanningRibbonGroup label={t("ribbon.links")}>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!selected?.activity} onClick={() => { if (selected) openDetails(selected); }}><Link2 className="h-3.5 w-3.5" />{t("manageLinks")}</Button>
      </PlanningRibbonGroup>
      </>}
      {ribbonTab === "resource" && onOpenAssignments && <PlanningRibbonGroup label={t("ribbon.resource")}>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" onClick={onOpenAssignments}><Users className="h-3.5 w-3.5" />{t("resourceAssignments")}</Button>
      </PlanningRibbonGroup>}
      {ribbonTab === "format" && <PlanningFormatRibbon editable={editable} selected={selected} moving={moving} selectedFormat={selectedFormat ?? undefined} toggleFormat={toggleFormat} changeTextColor={changeTextColor} changeHighlight={changeHighlight} changeFontSize={changeFontSize} updateTextFormat={updateTextFormat} t={t} />}
    </div>}
    {view === "gantt" ? <div className="max-h-[640px] overflow-auto" aria-label={t("timeline")}>
      <PlanTimelineGantt rows={rows} dependencies={dependencies} selectedId={visualSelectedId} zoom={selectedGanttZoom} collapsed={collapsed} showBaseline={showBaseline} onSelect={(rowId) => { setSelectedId(rowId); setActiveCell(null); }} onToggleCollapse={(row) => { if (!row.wbs || !row.hasChildren) return; markPreferencesDirty(); setCollapsed((current) => { const next = new Set(current); if (next.has(row.wbs!.id)) next.delete(row.wbs!.id); else next.add(row.wbs!.id); return next; }); }} onOpenDetails={openDetails} onContextMenu={openContextMenu} />
    </div> : <div ref={tableScrollRef} onScroll={(event) => setTableScrollTop(event.currentTarget.scrollTop)} className="max-h-[640px] overflow-auto" aria-label={t("workspaceTable")}>
      <div className="min-w-max">
        <div className="flex items-center justify-between gap-2 border-b bg-muted/10 px-2 py-1">
          <span className="text-[11px] font-medium text-muted-foreground">{t("columnManager")}{activeFilterCount > 0 ? ` · ${activeFilterCount} ${t("activeFilters")}` : ""}</span>
          <div className="flex items-center gap-1">
            <ColumnCatalogPopover open={columnManagerOpen} onOpenChange={setColumnManagerOpen} visibleColumnIds={columnOrder} onAdd={addColumn} t={t} />
            <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-[11px]" disabled={resetTablePreferences.isPending} onClick={resetTableLayout}>{t("resetTableLayout")}</Button>
            <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-[11px]" disabled={activeFilterCount === 0} onClick={clearAllFilters}>{t("clearAllFilters")}</Button>
          </div>
        </div>
        <div className="min-w-max bg-background">
          <div className={`planning-grid-header sticky top-0 z-20 grid h-11 items-center px-2 text-[11px] font-medium tracking-[0.08em] text-muted-foreground backdrop-blur ${resizingColumn !== null ? "select-none" : ""}`} style={{ gridTemplateColumns }}>
            {visibleColumns.map((column, index) => {
              const sticky = column.id === "schedulingMode" || column.id === "id" || column.id === "taskName";
              const iconOnly = column.id === "schedulingMode";
              return <div key={column.id} draggable={!column.required} onDragStart={column.required ? undefined : () => setDraggedColumn(column.id)} onDragOver={(event) => event.preventDefault()} onDrop={() => { if (draggedColumn) reorderColumn(draggedColumn, column.id); setDraggedColumn(null); }} className={`relative flex h-full min-w-0 items-center gap-0.5 ${iconOnly ? "justify-center" : ""} ${sticky ? "planning-sticky-header sticky z-[3]" : ""} ${draggedColumn === column.id ? "opacity-50" : ""}`} style={sticky ? { insetInlineStart: stickyOffsets.get(column.id) ?? 0 } : undefined} title={iconOnly ? t(column.labelKey) : undefined}>
                {!column.required && <GripVertical className="h-3 w-3 shrink-0 cursor-grab text-muted-foreground/60" />}
                {iconOnly ? <><Settings2 className="h-3.5 w-3.5" aria-hidden="true" /><span className="sr-only">{t(column.labelKey)}</span></> : <span className="truncate px-0.5">{t(column.labelKey)}</span>}
                <ColumnFilterPopover columnId={column.id} label={t(column.labelKey)} filterType={column.filterType} enumValues={column.enumValues} filter={columnFilters[column.id]} onChange={(filter) => updateColumnFilter(column.id, filter)} t={t} />
                {!column.required && <button type="button" className="rounded p-0.5 text-muted-foreground hover:bg-accent hover:text-foreground" aria-label={`${t("removeColumn")}: ${t(column.labelKey)}`} title={t("removeColumn")} onClick={(event) => { event.stopPropagation(); removeColumn(column.id); }}><X className="h-3 w-3" /></button>}
                <span role="separator" aria-label={t("resizeColumn")} aria-orientation="vertical" onPointerDown={(event) => beginColumnResize(index, event)} onPointerMove={moveColumnResize} onPointerUp={endColumnResize} onPointerCancel={endColumnResize} className={`absolute inset-y-1 end-0 z-10 w-2 cursor-col-resize rounded-sm hover:bg-primary/30 ${resizingColumn === index ? "bg-primary/40" : ""}`} />
              </div>;
            })}
          </div>
          {filteredRows.length === 0 && <div className="flex h-44 items-center justify-center border-b text-sm text-muted-foreground">{t("emptyWorkspace")}</div>}
          {topSpacerHeight > 0 && <div aria-hidden="true" style={{ height: topSpacerHeight }} />}
          {renderedRows.map((row) => {
            const isGroupRow = Boolean(row.wbs && (row.hasChildren || row.activity?.isSummaryTask || !row.activity));
            const rowKind = isGroupRow ? "wbs" : row.activity?.type === PLANNING_ACTIVITY_TYPE.milestone ? "milestone" : "activity";
            const isZebra = rowKind === "activity" && row.rowNumber % 2 === 0;
            const isSelected = visualSelectedId === row.id;
            const isActive = activeCell?.rowId === row.id;
            return <div key={row.id} role="row" aria-selected={isSelected} data-row-kind={rowKind} data-zebra={isZebra ? "true" : undefined} data-selected={isSelected ? "true" : undefined} data-active={isActive ? "true" : undefined} data-critical={row.activity?.critical ? "true" : undefined} onClick={() => setSelectedId(row.id)} onDoubleClick={() => openDetails(row)} onContextMenu={(event) => openContextMenu(row, event)} className={`planning-grid-row planning-grid-row-${rowKind} group relative grid h-11 items-center px-2 text-[13px] ${isZebra ? "planning-grid-row-zebra" : ""} ${draggedActivityId === row.activity?.id ? "opacity-60" : ""} ${planTextFormatClassName(row.textFormat)}`} style={{ gridTemplateColumns }}>
              {draggedActivityId && row.activity && draggedActivityId !== row.activity.id && (['Before', 'Child', 'After'] as const).map((position) => {
                const activeDrop = dropTarget?.targetId === row.activity!.id && dropTarget.position === position;
                const placement = position === "Before" ? "top-0" : position === "Child" ? "top-1/3" : "top-2/3";
                const label = position === "Before" ? t("dropBefore") : position === "After" ? t("dropAfter") : t("dropChild");
                return <div key={position} role="button" tabIndex={-1} aria-label={`${t("moveActivity")}: ${label}`} onDragOver={(event) => handleDropZoneDragOver(event, row.activity!.id, position)} onDrop={(event) => handleDropZoneDrop(event, row.activity!.id, position)} className={`absolute inset-x-0 z-10 h-1/3 border-primary/70 transition-colors ${placement} ${activeDrop ? "border-y-2 bg-primary/10 opacity-100" : "opacity-0 hover:opacity-100"}`}><span className={`absolute end-2 top-0.5 rounded bg-primary px-1.5 text-[10px] font-semibold text-primary-foreground ${activeDrop ? "opacity-100" : "opacity-0"}`}>{label}</span></div>;
              })}
              {visibleColumns.map((column, columnIndex) => {
                const sticky = column.id === "schedulingMode" || column.id === "id" || column.id === "taskName";
                const taskName = column.id === "taskName";
                const value = getPlanningColumnValue(column.id, row, columnValueContext);
                return <div key={column.id} data-selected={isSelected ? "true" : undefined} data-formatted={taskName && row.textFormat?.highlightColorToken ? "true" : undefined} className={`${sticky ? "planning-sticky-cell sticky z-[2]" : ""} flex h-full min-w-0 items-center ${taskName ? `gap-1 ${planTextFormatClassName(row.textFormat)}` : "px-1"} ${column.id === "id" ? "justify-center tabular-nums text-muted-foreground" : ""}`} style={sticky ? { insetInlineStart: stickyOffsets.get(column.id) ?? 0, paddingInlineStart: taskName ? row.depth * 18 : undefined } : undefined}>
                  {taskName && row.wbs && row.hasChildren ? <button type="button" className="rounded p-0.5 hover:bg-muted" onClick={(event) => { event.stopPropagation(); markPreferencesDirty(); setCollapsed((current) => { const next = new Set(current); if (next.has(row.wbs!.id)) next.delete(row.wbs!.id); else next.add(row.wbs!.id); return next; }); }} aria-label={collapsed.has(row.wbs.id) ? t("expand") : t("collapse")}>{collapsed.has(row.wbs.id) ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}</button> : taskName ? <span className="w-4" /> : null}
                  {taskName && row.activity?.type === PLANNING_ACTIVITY_TYPE.milestone && <Diamond className="h-3.5 w-3.5 shrink-0 fill-primary text-primary" />}
                  {taskName && row.activity && editable && <button type="button" draggable={!moving} onDragStart={(event) => handleDragStart(event, row.activity!.id)} onDragEnd={handleDragEnd} onClick={(event) => event.stopPropagation()} aria-label={t("moveActivity")} title={t("moveActivity")} className="cursor-grab rounded p-0.5 text-muted-foreground/60 hover:bg-muted hover:text-foreground active:cursor-grabbing"><GripVertical className="h-3.5 w-3.5" /></button>}
                  {column.id === "id" ? value : renderColumnCell(row, column.id, columnIndex)}
                </div>;
              })}
            </div>;
          })}
          {bottomSpacerHeight > 0 && <div aria-hidden="true" style={{ height: bottomSpacerHeight }} />}
          {editable && <div role="row" className="grid h-11 items-center border-b bg-muted/10 px-2 text-[13px]" style={{ gridTemplateColumns }}>
            {visibleColumns.map((column, index) => column.id === "taskName" ? <input key={column.id} ref={quickTaskInputRef} value={quickTaskName} onChange={(event) => setQuickTaskName(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") { event.preventDefault(); void createQuickTask(); } }} disabled={quickTaskCreating} aria-label={t("quickEntryLabel")} placeholder={t("quickEntryPlaceholder")} className="h-8 min-w-0 w-full rounded border border-dashed border-input bg-transparent px-1.5 text-xs outline-none focus:border-primary focus:bg-background disabled:opacity-60" /> : index === 0 ? <span key={column.id} className="text-center text-lg leading-none text-muted-foreground">+</span> : index === visibleColumns.length - 1 ? <span key={column.id} className="px-1 text-[10px] text-muted-foreground">{t("keyboardHint")}</span> : <span key={column.id} />)}
          </div>}
        </div>
      </div>
    </div>}
    {contextMenu && contextRow && <PlanningContextMenu row={contextRow} x={contextMenu.x} y={contextMenu.y} editable={editable} moving={moving} creating={creating} onClose={() => setContextMenu(null)} onOpenDetails={() => openDetails(contextRow)} onSetSchedulingMode={(mode) => commitActivity(contextRow.activity!.id, { schedulingMode: mode })} onInsertTaskAbove={() => createRow(PLANNING_ACTIVITY_TYPE.task, "above", undefined, contextRow)} onInsertTaskBelow={() => createRow(PLANNING_ACTIVITY_TYPE.task, "below", undefined, contextRow)} onMoveUp={() => moveSibling("up", contextRow)} onMoveDown={() => moveSibling("down", contextRow)} onAddSubtask={() => indent(contextRow)} onOutdent={() => outdent(contextRow)} onAddMilestone={() => createRow(PLANNING_ACTIVITY_TYPE.milestone, "below", undefined, contextRow)} onEditPredecessor={() => openDetails(contextRow)} onAssignResource={onOpenAssignments} onDelete={() => removeSelected(contextRow)} t={t} common={common} />}
    {detailsRow && <LazyPlanningTaskDetailsSheet projectId={projectId} planId={planId} row={detailsRow} activities={activities} dependencies={dependencies} editable={editable} open={detailsRowId === detailsRow.id} onOpenChange={(open) => { if (!open) setDetailsRowId(null); }} predecessorText={detailsRow.activity ? predecessorText(detailsRow.activity.id) : ""} onSaveActivity={async (activity, patch) => { const { name, ...rest } = patch; if (name !== activity.name) await commitName(detailsRow, name); const changed = changedActivityPatch(activity, rest); if (Object.keys(changed).length > 0) await commitActivity(activity.id, changed); }} onSavePredecessors={savePredecessors} onSaveTextFormat={updateTextFormat} />}
  </div>;
}

function ColumnCatalogPopover({ open, onOpenChange, visibleColumnIds, onAdd, t }: { open: boolean; onOpenChange: (_open: boolean) => void; visibleColumnIds: PlanningColumnId[]; onAdd: (_columnId: PlanningColumnId) => void; t: ReturnType<typeof useTranslations> }) {
  const [search, setSearch] = useState("");
  const normalizedSearch = search.trim().toLocaleLowerCase();
  const categories = Array.from(new Set(PLANNING_COLUMN_DEFINITIONS.map((column) => column.categoryKey)));
  return <Popover open={open} onOpenChange={onOpenChange}>
    <PopoverTrigger asChild><Button type="button" size="sm" variant="outline" className="h-7 gap-1 px-2 text-[11px]"><Columns3 className="h-3.5 w-3.5" />{t("addColumn")}</Button></PopoverTrigger>
    <PopoverContent align="start" className="w-80 p-3">
      <div className="relative"><Search className="absolute start-2 top-2 h-3.5 w-3.5 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("columnSearch")} className="h-8 ps-7 text-xs" /></div>
      <div className="mt-3 max-h-72 space-y-3 overflow-y-auto">
        {categories.map((categoryKey) => {
          const columns = PLANNING_COLUMN_DEFINITIONS.filter((column) => column.categoryKey === categoryKey && !visibleColumnIds.includes(column.id) && (!normalizedSearch || t(column.labelKey).toLocaleLowerCase().includes(normalizedSearch)));
          if (columns.length === 0) return null;
          return <div key={categoryKey}><p className="mb-1 px-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t(categoryKey)}</p><div className="space-y-0.5">{columns.map((column) => <button key={column.id} type="button" onClick={() => onAdd(column.id)} className="flex w-full items-center justify-between rounded px-2 py-1.5 text-start text-xs hover:bg-accent"><span>{t(column.labelKey)}</span><Plus className="h-3.5 w-3.5 text-muted-foreground" /></button>)}</div></div>;
        })}
        {!PLANNING_COLUMN_DEFINITIONS.some((column) => !visibleColumnIds.includes(column.id) && (!normalizedSearch || t(column.labelKey).toLocaleLowerCase().includes(normalizedSearch))) && <p className="py-4 text-center text-xs text-muted-foreground">{t("noMatchingFields")}</p>}
      </div>
      <p className="mt-3 border-t pt-2 text-[10px] leading-4 text-muted-foreground">{t("unsupportedFields")}</p>
    </PopoverContent>
  </Popover>;
}

function ColumnFilterPopover({ columnId, label, filterType, enumValues, filter, onChange, t }: { columnId: PlanningColumnId; label: string; filterType: "text" | "number" | "date" | "enum"; enumValues?: readonly string[]; filter?: PlanningColumnFilter; onChange: (_filter: PlanningColumnFilter | undefined) => void; t: ReturnType<typeof useTranslations> }) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<PlanningColumnFilter>(filter ?? {});
  useEffect(() => { if (open) setDraft(filter ?? {}); }, [filter, open]);
  const hasFilter = Boolean(filter && (filter.text || filter.min || filter.max || filter.value));
  const update = (patch: PlanningColumnFilter) => setDraft((current) => ({ ...current, ...patch }));
  const apply = () => { onChange(draft); setOpen(false); };
  const clear = () => { onChange(undefined); setDraft({}); setOpen(false); };
  return <Popover open={open} onOpenChange={setOpen}>
    <PopoverTrigger asChild><Button type="button" size="icon-sm" variant={hasFilter ? "secondary" : "ghost"} data-column-id={columnId} aria-label={`${t("filter")}: ${label}`} className="h-6 w-6"><Filter className="h-3 w-3" /></Button></PopoverTrigger>
    <PopoverContent align="start" className="w-64 p-3">
      <p className="mb-2 text-xs font-semibold">{t("filter")}: {label}</p>
      {filterType === "text" && <div className="space-y-1"><label className="text-[11px] text-muted-foreground">{t("filterContains")}</label><Input value={draft.text ?? ""} onChange={(event) => update({ text: event.target.value })} className="h-8 text-xs" /></div>}
      {filterType === "number" && <div className="grid grid-cols-2 gap-2"><label className="space-y-1 text-[11px] text-muted-foreground">{t("filterMin")}<Input type="number" value={draft.min ?? ""} onChange={(event) => update({ min: event.target.value })} className="h-8 text-xs" /></label><label className="space-y-1 text-[11px] text-muted-foreground">{t("filterMax")}<Input type="number" value={draft.max ?? ""} onChange={(event) => update({ max: event.target.value })} className="h-8 text-xs" /></label></div>}
      {filterType === "date" && <div className="grid grid-cols-2 gap-2"><label className="space-y-1 text-[11px] text-muted-foreground">{t("filterFrom")}<Input type="date" value={draft.min ?? ""} onChange={(event) => update({ min: event.target.value })} className="h-8 text-xs" /></label><label className="space-y-1 text-[11px] text-muted-foreground">{t("filterTo")}<Input type="date" value={draft.max ?? ""} onChange={(event) => update({ max: event.target.value })} className="h-8 text-xs" /></label></div>}
      {filterType === "enum" && <div className="space-y-1"><label className="text-[11px] text-muted-foreground">{t("filterValue")}</label><select value={draft.value ?? ""} onChange={(event) => update({ value: event.target.value })} className="h-8 w-full rounded border bg-background px-2 text-xs"><option value="">{t("formatNone")}</option>{(enumValues ?? []).map((value) => <option key={value} value={value}>{value === "WBS" ? t("columns.wbs") : value === "Task" ? t("task") : value === "Milestone" ? t("milestone") : value === "Automatic" ? t("automatic") : value === "Manual" ? t("manual") : value === "Yes" ? t("yes") : value === "No" ? t("no") : value}</option>)}</select></div>}
      <div className="mt-3 flex justify-between gap-2"><Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-[11px]" disabled={!hasFilter} onClick={clear}>{t("clearFilter")}</Button><Button type="button" size="sm" className="h-7 px-2 text-[11px]" onClick={apply}>{t("save")}</Button></div>
    </PopoverContent>
  </Popover>;
}

function ContextMenuButton({ label, onClick, disabled = false, destructive = false }: { label: string; onClick: () => void; disabled?: boolean; destructive?: boolean }) {
  return <button type="button" role="menuitem" disabled={disabled} onClick={onClick} className={`flex w-full items-center rounded px-2.5 py-1.5 text-start hover:bg-accent disabled:pointer-events-none disabled:opacity-50 ${destructive ? "text-destructive" : ""}`}>{label}</button>;
}

function PlanningContextMenu({ row, x, y, editable, moving, creating, onClose, onOpenDetails, onSetSchedulingMode, onInsertTaskAbove, onInsertTaskBelow, onMoveUp, onMoveDown, onAddSubtask, onOutdent, onAddMilestone, onEditPredecessor, onAssignResource, onDelete, t, common }: {
  row: PlanWorkspaceRow;
  x: number;
  y: number;
  editable: boolean;
  moving: boolean;
  creating: boolean;
  onClose: () => void;
  onOpenDetails: () => void;
  onSetSchedulingMode: (_mode: "Automatic" | "Manual") => void;
  onInsertTaskAbove: () => Promise<boolean>;
  onInsertTaskBelow: () => Promise<boolean>;
  onMoveUp: () => Promise<void>;
  onMoveDown: () => Promise<void>;
  onAddSubtask: () => Promise<void>;
  onOutdent: () => Promise<void>;
  onAddMilestone: () => Promise<boolean>;
  onEditPredecessor: () => void;
  onAssignResource?: () => void;
  onDelete: () => Promise<void>;
  t: ReturnType<typeof useTranslations>;
  common: ReturnType<typeof useTranslations>;
}) {
  const menuRef = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ left: x, top: y });
  useEffect(() => {
    const el = menuRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const viewW = window.innerWidth;
    const viewH = window.innerHeight;
    let left = x;
    let top = y;
    if (x + rect.width > viewW) left = Math.max(0, viewW - rect.width - 8);
    if (y + rect.height > viewH) top = Math.max(0, viewH - rect.height - 8);
    if (left !== pos.left || top !== pos.top) setPos({ left, top });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [x, y]);
  const run = (action: () => void | Promise<void> | Promise<boolean>) => { const result = action(); void Promise.resolve(result).then(() => onClose()); };
  return <div ref={menuRef} data-planning-context-menu role="menu" aria-label={t("taskContextMenu")} className="fixed z-[80] min-w-52 rounded-md border bg-popover p-1 text-xs text-popover-foreground shadow-lg" style={{ left: pos.left, top: pos.top }} onPointerDown={(event) => event.stopPropagation()}>
    {row.activity && <ContextMenuButton label={t("openDetails")} onClick={() => run(onOpenDetails)} />}
    {editable && <>
      {row.activity && <>
        <ContextMenuButton label={t("setAutomatic")} disabled={row.activity.schedulingMode === "Automatic"} onClick={() => run(() => onSetSchedulingMode("Automatic"))} />
        <ContextMenuButton label={t("setManual")} disabled={row.activity.schedulingMode === "Manual"} onClick={() => run(() => onSetSchedulingMode("Manual"))} />
      </>}
      <ContextMenuButton label={t("insertTaskAbove")} onClick={() => run(onInsertTaskAbove)} />
      <ContextMenuButton label={t("insertTaskBelow")} onClick={() => run(onInsertTaskBelow)} />
      {row.activity && <>
        <ContextMenuButton label={t("moveUp")} disabled={moving} onClick={() => run(onMoveUp)} />
        <ContextMenuButton label={t("moveDown")} disabled={moving} onClick={() => run(onMoveDown)} />
      </>}
      <ContextMenuButton label={t("addSubtask")} disabled={moving} onClick={() => run(onAddSubtask)} />
      {row.wbs?.parentWbsId && <ContextMenuButton label={t("outdent")} disabled={moving} onClick={() => run(onOutdent)} />}
      <ContextMenuButton label={t("addMilestone")} disabled={creating} onClick={() => run(onAddMilestone)} />
      {row.activity && <ContextMenuButton label={t("editPredecessor")} onClick={() => run(onEditPredecessor)} />}
      {onAssignResource && <ContextMenuButton label={t("assignResource")} onClick={() => run(onAssignResource)} />}
      <ContextMenuButton label={common("delete")} disabled={moving} destructive onClick={() => run(onDelete)} />
    </>}
  </div>;
}

function PlanningFormatRibbon({ editable, selected, moving, selectedFormat, toggleFormat, changeTextColor, changeHighlight, changeFontSize, updateTextFormat, t }: {
  editable: boolean;
  selected: PlanWorkspaceRow | undefined;
  moving: boolean;
  selectedFormat: PlanTextFormatDto | undefined;
  toggleFormat: (_property: "isBold" | "isItalic" | "isUnderline") => Promise<void>;
  changeTextColor: (_value: string) => Promise<void>;
  changeHighlight: (_value: string) => Promise<void>;
  changeFontSize: (_value: string) => Promise<void>;
  updateTextFormat: (_format: PlanTextFormatDto | null) => Promise<void>;
  t: ReturnType<typeof useTranslations>;
}) {
  return <PlanningRibbonGroup label={t("ribbon.format")} className="min-w-[26rem]">
    <Button size="icon-sm" variant={selectedFormat?.isBold ? "secondary" : "ghost"} disabled={!editable || !selected || moving} onClick={() => void toggleFormat("isBold")} aria-label={t("formatBold")} aria-pressed={selectedFormat?.isBold}><Bold className="h-3.5 w-3.5" /></Button>
    <Button size="icon-sm" variant={selectedFormat?.isItalic ? "secondary" : "ghost"} disabled={!editable || !selected || moving} onClick={() => void toggleFormat("isItalic")} aria-label={t("formatItalic")} aria-pressed={selectedFormat?.isItalic}><Italic className="h-3.5 w-3.5" /></Button>
    <Button size="icon-sm" variant={selectedFormat?.isUnderline ? "secondary" : "ghost"} disabled={!editable || !selected || moving} onClick={() => void toggleFormat("isUnderline")} aria-label={t("formatUnderline")} aria-pressed={selectedFormat?.isUnderline}><Underline className="h-3.5 w-3.5" /></Button>
    <label className="sr-only" htmlFor="planning-text-color">{t("formatTextColor")}</label>
    <select id="planning-text-color" value={selectedFormat?.textColorToken ?? ""} disabled={!editable || !selected || moving} onChange={(event) => void changeTextColor(event.target.value)} className="h-7 max-w-24 rounded border bg-background px-1.5 text-[11px]" aria-label={t("formatTextColor")}>
      <option value="">{t("formatNone")}</option>
      {PLAN_TEXT_COLOR_TOKENS.map((token) => <option key={token} value={token}>{t(`formatColors.${token}`)}</option>)}
    </select>
    <label className="sr-only" htmlFor="planning-highlight-color">{t("formatHighlight")}</label>
    <select id="planning-highlight-color" value={selectedFormat?.highlightColorToken ?? ""} disabled={!editable || !selected || moving} onChange={(event) => void changeHighlight(event.target.value)} className="h-7 max-w-24 rounded border bg-background px-1.5 text-[11px]" aria-label={t("formatHighlight")}>
      <option value="">{t("formatNone")}</option>
      {PLAN_HIGHLIGHT_COLOR_TOKENS.map((token) => <option key={token} value={token}>{t(`formatHighlights.${token}`)}</option>)}
    </select>
    <label className="sr-only" htmlFor="planning-font-size">{t("formatFontSize")}</label>
    <select id="planning-font-size" value={selectedFormat?.fontSizeToken ?? ""} disabled={!editable || !selected || moving} onChange={(event) => void changeFontSize(event.target.value)} className="h-7 max-w-24 rounded border bg-background px-1.5 text-[11px]" aria-label={t("formatFontSize")}>
      <option value="">{t("formatNone")}</option>
      {PLAN_FONT_SIZE_TOKENS.map((token) => <option key={token} value={token}>{t(`formatSizes.${token}`)}</option>)}
    </select>
    <Button size="icon-sm" variant="ghost" disabled={!editable || !selected || !selectedFormat || moving} onClick={() => void updateTextFormat(null)} aria-label={t("clearFormatting")}><Eraser className="h-3.5 w-3.5" /></Button>
  </PlanningRibbonGroup>;
}

function InlineCell({ value, onCommit, onNavigate, onFocus, inputRef, onMultiLinePaste, onPasteRejected, active, disabled, ariaLabel, type = "text", placeholder, min, max }: {
  value: string;
  onCommit: (_value: string) => Promise<void>;
  onNavigate?: (_direction: NavigationDirection) => void;
  onFocus?: () => void;
  inputRef?: (_element: HTMLInputElement | null) => void;
  onMultiLinePaste?: (_values: string[]) => void;
  onPasteRejected?: () => void;
  active?: boolean;
  rowId: string;
  column: number;
  disabled: boolean;
  ariaLabel: string;
  type?: "text" | "number" | "date";
  placeholder?: string;
  min?: number;
  max?: number;
}) {
  const [draft, setDraft] = useState(value);
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (!saving) setDraft(value); }, [saving, value]);
  const skipBlur = useRef(false);
  const save = async () => {
    if (disabled || saving || draft === value) return true;
    setSaving(true);
    try { await onCommit(draft.trim()); return true; }
    catch { setDraft(value); return false; }
    finally { setSaving(false); }
  };
  const navigate = async (direction: NavigationDirection) => {
    if (disabled || saving) return;
    skipBlur.current = true;
    const committed = await save();
    if (!committed) { skipBlur.current = false; return; }
    if (onNavigate) onNavigate(direction);
    else skipBlur.current = false;
  };
  const handlePaste = (event: React.ClipboardEvent<HTMLInputElement>) => {
    const pasted = event.clipboardData.getData("text");
    const lines = pasted.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
    if (lines.length > 1 && type === "text" && onMultiLinePaste) {
      event.preventDefault();
      onMultiLinePaste(lines);
      return;
    }
    if (!compatiblePaste(pasted, type, min, max)) {
      event.preventDefault();
      onPasteRejected?.();
    }
  };
  return <input ref={inputRef} type={type} value={draft} min={min} max={max} placeholder={placeholder} aria-label={ariaLabel} disabled={disabled || saving} onFocus={onFocus} onClick={(event) => event.stopPropagation()} onPaste={handlePaste} onInput={type === "date" ? (event) => setDraft(event.currentTarget.value) : undefined} onChange={(event) => setDraft(event.target.value)} onBlur={() => { if (skipBlur.current) { skipBlur.current = false; return; } void save(); }} onKeyDown={(event) => {
    if (event.key === "Escape") { event.preventDefault(); setDraft(value); return; }
    if (event.key === "Enter") { event.preventDefault(); void navigate("next"); return; }
    if (event.key === "Tab") { event.preventDefault(); void navigate(event.shiftKey ? "previous" : "next"); return; }
    if (event.key === "ArrowUp" || event.key === "ArrowDown") { event.preventDefault(); void navigate(event.key === "ArrowUp" ? "up" : "down"); return; }
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      const atBoundary = type !== "text" || (event.key === "ArrowLeft" ? event.currentTarget.selectionStart === 0 : event.currentTarget.selectionEnd === event.currentTarget.value.length);
      if (atBoundary) { event.preventDefault(); void navigate(event.key === "ArrowLeft" ? "previous" : "next"); }
    }
  }} className={`h-8 min-w-0 w-full rounded border border-transparent bg-transparent px-1.5 outline-none hover:border-input focus:border-primary focus:bg-background disabled:cursor-default disabled:opacity-100 ${active ? "ring-1 ring-primary" : ""}`} />;
}
