import * as React from "react";
import { createPortal } from "react-dom";
import { useQueryClient } from "@tanstack/react-query";
import { Bold, Bot, Check, ChevronDown, ChevronRight, ChevronUp, ChevronsDownUp, ChevronsUpDown, Columns3, Diamond, Eraser, Filter, GripVertical, Hand, IndentDecrease, IndentIncrease, Italic, Link2, Percent, Pencil, Plus, RotateCcw, Rows3, Trash2, Underline, Users, X } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Input } from "@components/ui/Input";
import { Progress } from "@components/ui/Progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { cn } from "@lib/utils";
import { calculateAnchoredOverlayPosition } from "@components/ui/AnchoredOverlay";
import { PLANNING_ACTIVITY_TYPE } from "@lib/enums";
import { toast } from "@hooks/use-toast";
import { ApiError } from "@api/client";
import { planningApiFull, planningKeys, usePlanningAssignmentsFull, usePlanningCalendarsFull, usePlanningResourcesFull, usePlanningTablePreferencesFull, useResetPlanningTablePreferencesFull, useSavePlanningTablePreferencesFull, type PlanActivityDto, type PlanAssignmentDto, type PlanCalendarDto, type PlanDependencyDto, type PlanPredecessorInputDto, type PlanningColumnFilterDto, type PlanWbsNodeDto, type ReplacePlanPredecessorsInput, type ResourceLookupDto } from "../api/planningFull";
import { buildPlanWorkspaceRows, comparePlanOrder, findWbsNode, visiblePlanWorkspaceRows, type PlanWorkspaceRow } from "../lib/plan-workspace-rows";
import { clampPlanningGridWidth, DEFAULT_PLANNING_GRID_COLUMNS, getPlanningGridFilterKind, matchesPlanningGridFilter, normalizePlanningGridColumns, reorderPlanningGridColumn, REQUIRED_PLANNING_GRID_COLUMNS, type PlanningGridColumnId } from "../lib/planning-grid-preferences";
import { formatPredecessor, parsePredecessor } from "../lib/predecessor-syntax";
import { roundPlanProgress } from "../lib/plan-progress";
import { PLAN_FONT_SIZE_TOKENS, PLAN_HIGHLIGHT_COLOR_TOKENS, PLAN_TEXT_COLOR_TOKENS, planTextFormatClassName } from "../lib/plan-text-format";
import { PLAN_ROW_HEIGHT } from "../lib/gantt-constants";
import { PlanningIconButton, PlanningRibbonGroup, type PlanningRibbonTab } from "./PlanningRibbon";

const BASELINE_COLUMNS: PlanningGridColumnId[] = ["baselineStart", "baselineFinish"];
const CLEARABLE_PLANNING_GRID_COLUMNS: readonly PlanningGridColumnId[] = ["start", "finish", "actualStart", "actualFinish", "deadline", "progress", "predecessors"];
const ALL_COLUMNS: { id: PlanningGridColumnId; labelKey: string }[] = [
  { id: "id", labelKey: "planning.columns.id" },
  { id: "wbs", labelKey: "planning.columns.wbs" },
  { id: "outlineLevel", labelKey: "planning.columns.outlineLevel" },
  { id: "taskName", labelKey: "planning.columns.taskName" },
  { id: "type", labelKey: "planning.columns.type" },
  { id: "schedulingMode", labelKey: "planning.columns.schedulingMode" },
  { id: "schedulingStatus", labelKey: "planning.columns.schedulingStatus" },
  { id: "constraintType", labelKey: "planning.columns.constraintType" },
  { id: "constraintDate", labelKey: "planning.columns.constraintDate" },
  { id: "deadline", labelKey: "planning.columns.deadline" },
  { id: "duration", labelKey: "planning.columns.duration" },
  { id: "start", labelKey: "planning.columns.start" },
  { id: "finish", labelKey: "planning.columns.finish" },
  { id: "calendar", labelKey: "planning.columns.calendar" },
  { id: "actualStart", labelKey: "planning.columns.actualStart" },
  { id: "actualFinish", labelKey: "planning.columns.actualFinish" },
  { id: "baselineStart", labelKey: "planning.columns.baselineStart" },
  { id: "baselineFinish", labelKey: "planning.columns.baselineFinish" },
  { id: "baselineDuration", labelKey: "planning.columns.baselineDuration" },
  { id: "startVariance", labelKey: "planning.columns.startVariance" },
  { id: "finishVariance", labelKey: "planning.columns.finishVariance" },
  { id: "durationVariance", labelKey: "planning.columns.durationVariance" },
  { id: "progress", labelKey: "planning.columns.progress" },
  { id: "predecessors", labelKey: "planning.columns.predecessors" },
  { id: "critical", labelKey: "planning.columns.critical" },
  { id: "estimateHours", labelKey: "planning.columns.estimateHours" },
  { id: "resources", labelKey: "planning.columns.resources" },
  { id: "description", labelKey: "planning.columns.description" },
];

const COLUMN_WIDTHS: Record<PlanningGridColumnId, { initial: number; min: number; max: number }> = {
  id: { initial: 64, min: 44, max: 100 }, wbs: { initial: 100, min: 72, max: 180 }, outlineLevel: { initial: 94, min: 72, max: 140 },
  taskName: { initial: 220, min: 150, max: 480 }, type: { initial: 100, min: 76, max: 150 }, schedulingMode: { initial: 132, min: 96, max: 210 },
  duration: { initial: 92, min: 64, max: 140 }, start: { initial: 128, min: 96, max: 170 }, finish: { initial: 128, min: 96, max: 170 },
  actualStart: { initial: 128, min: 96, max: 170 }, actualFinish: { initial: 128, min: 96, max: 170 }, baselineStart: { initial: 128, min: 96, max: 170 },
  baselineFinish: { initial: 128, min: 96, max: 170 }, baselineDuration: { initial: 128, min: 96, max: 170 }, startVariance: { initial: 112, min: 84, max: 160 },
  finishVariance: { initial: 112, min: 84, max: 160 }, durationVariance: { initial: 128, min: 96, max: 170 }, progress: { initial: 112, min: 88, max: 150 },
  predecessors: { initial: 160, min: 120, max: 280 }, critical: { initial: 92, min: 72, max: 130 }, estimateHours: { initial: 112, min: 84, max: 160 },
  description: { initial: 240, min: 160, max: 420 },
  schedulingStatus: { initial: 180, min: 130, max: 300 }, constraintType: { initial: 170, min: 130, max: 260 },
  constraintDate: { initial: 128, min: 96, max: 170 }, deadline: { initial: 112, min: 92, max: 160 },
  calendar: { initial: 150, min: 110, max: 260 }, resources: { initial: 180, min: 120, max: 320 },
};

const ENUM_FILTER_VALUES: Partial<Record<PlanningGridColumnId, readonly string[]>> = {
  type: ["Task", "Milestone", "WBS"],
  schedulingMode: ["Automatic", "Manual"],
  constraintType: ["AsSoonAsPossible", "AsLateAsPossible", "MustStartOn", "MustFinishOn", "StartNoEarlierThan", "StartNoLaterThan", "FinishNoEarlierThan", "FinishNoLaterThan"],
  critical: ["Yes", "No"],
};
const PLAN_CONSTRAINT_TYPES = ["AsSoonAsPossible", "AsLateAsPossible", "MustStartOn", "MustFinishOn", "StartNoEarlierThan", "StartNoLaterThan", "FinishNoEarlierThan", "FinishNoLaterThan"] as const;
const DATE_COLUMNS: readonly PlanningGridColumnId[] = ["start", "finish", "actualStart", "actualFinish", "constraintDate", "deadline", "baselineStart", "baselineFinish"];

interface Mutations {
  updateActivity: { mutateAsync: (args: { id: string; body: unknown }) => Promise<unknown> };
  bulkUpdateActivities: { mutateAsync: (body: unknown) => Promise<unknown> };
  deleteActivity: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  deleteAssignment: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  createActivity: { mutateAsync: (body: unknown) => Promise<unknown> };
  updateWbs: { mutateAsync: (args: { id: string; body: unknown }) => Promise<unknown> };
  bulkUpdateWbsPlacements: { mutateAsync: (items: Array<{ nodeId: string; parentWbsId: string | null; sortOrder: number; rowVersion: string }>) => Promise<unknown> };
  createWbs: { mutateAsync: (body: unknown) => Promise<unknown> };
  deleteWbs: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  createDependency: { mutateAsync: (body: unknown) => Promise<unknown> };
  deleteDependency: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  replacePredecessorsBulk: { mutateAsync: (items: ReplacePlanPredecessorsInput[]) => Promise<unknown> };
  moveActivity: { mutateAsync: (args: { id: string; body: { targetActivityId?: string; targetWbsId?: string; position: "Before" | "After" | "Child"; sortOrder?: number; rowVersion: string; wbsRowVersion: string; targetWbsRowVersion?: string } }) => Promise<unknown>; isPending: boolean };
}

function toDateInput(value?: string | null) { return value?.slice(0, 10) ?? ""; }

export function PlanningGrid({ projectId, planId, projectStartDate, wbs, activities, dependencies, mutations, editable, showBaseline, ribbonTab, ribbonExpanded = true, t, onEdit, onOpenAssignments, selectedRowId, collapsedWbsIds, onSelectionChange, onCollapsedChange }: {
  projectId: string;
  planId: string;
  projectStartDate?: string;
  wbs: PlanWbsNodeDto[];
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  mutations: Mutations;
  editable: boolean;
  showBaseline: boolean;
  ribbonTab: PlanningRibbonTab;
  ribbonExpanded?: boolean;
  t: (key: string) => string;
  onEdit: (activity: PlanActivityDto) => void;
  /** Kept optional for older callers; row deletion is handled here to share confirmation and cleanup. */
  onDelete?: (activity: PlanActivityDto) => void;
  onOpenAssignments?: () => void;
  /** Controlled selection (shared with the Gantt in split view); falls back to internal state. */
  selectedRowId?: string;
  /** Controlled collapsed WBS ids (shared with the Gantt in split view); falls back to internal state. */
  collapsedWbsIds?: ReadonlySet<string>;
  onSelectionChange?: (rowId: string) => void;
  onCollapsedChange?: (collapsed: ReadonlySet<string>) => void;
}) {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [internalCollapsed, setInternalCollapsed] = React.useState<ReadonlySet<string>>(() => new Set());
  const [internalSelectedId, setInternalSelectedId] = React.useState("");
  const [columns, setColumns] = React.useState<PlanningGridColumnId[]>(DEFAULT_PLANNING_GRID_COLUMNS);
  const [columnFilters, setColumnFilters] = React.useState<Record<string, PlanningColumnFilterDto>>({});
  const [columnWidths, setColumnWidths] = React.useState<Record<PlanningGridColumnId, number>>(() => Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, value]) => [key, value.initial])) as Record<PlanningGridColumnId, number>);
  const [columnManagerOpen, setColumnManagerOpen] = React.useState(false);
  const [columnFiltersOpen, setColumnFiltersOpen] = React.useState(false);
  const [draggedColumn, setDraggedColumn] = React.useState<PlanningGridColumnId | null>(null);
  const [editing, setEditing] = React.useState<{ rowId: string; column: PlanningGridColumnId; draft: string } | null>(null);
  const [activeCell, setActiveCell] = React.useState<{ rowId: string; column: PlanningGridColumnId } | null>(null);
  const [selectedRowIds, setSelectedRowIds] = React.useState<ReadonlySet<string>>(() => new Set());
  const [rowSelectionAnchorId, setRowSelectionAnchorId] = React.useState("");
  const [selectedCellKeys, setSelectedCellKeys] = React.useState<ReadonlySet<string>>(() => new Set());
  const [cellSelectionAnchor, setCellSelectionAnchor] = React.useState<{ rowId: string; column: PlanningGridColumnId } | null>(null);
  const [quickName, setQuickName] = React.useState("");
  const [bulkProgress, setBulkProgress] = React.useState("0");
  const [pastingTasks, setPastingTasks] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [moving, setMoving] = React.useState(false);
  const [draggedActivityId, setDraggedActivityId] = React.useState<string | null>(null);
  const [dropTarget, setDropTarget] = React.useState<{ targetId: string; position: "Before" | "After" | "Child" } | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [contextMenu, setContextMenu] = React.useState<{ rowId: string; x: number; y: number } | null>(null);
  const contextMenuRef = React.useRef<HTMLDivElement>(null);
  const [contextMenuStyle, setContextMenuStyle] = React.useState<React.CSSProperties>({ visibility: "hidden" });
  const [error, setError] = React.useState<string | null>(null);
  const [preferenceSaveRevision, setPreferenceSaveRevision] = React.useState(0);
  const tablePreferences = usePlanningTablePreferencesFull(projectId, planId);
  const needAssignments = columns.includes("resources");
  const needCalendars = columns.includes("calendar");
  const assignmentsQuery = usePlanningAssignmentsFull(projectId, planId, needAssignments || editable);
  const resourcesQuery = usePlanningResourcesFull(needAssignments);
  const calendarsQuery = usePlanningCalendarsFull(projectId, planId, needCalendars);
  const saveTablePreferences = useSavePlanningTablePreferencesFull(projectId, planId);
  const savePreferencesRef = React.useRef(saveTablePreferences.mutateAsync);
  savePreferencesRef.current = saveTablePreferences.mutateAsync;
  const resetTablePreferences = useResetPlanningTablePreferencesFull(projectId, planId);
  const preferencesHydratedPlan = React.useRef<string | null>(null);
  const preferencesDirty = React.useRef(false);
  const preferencesRevision = React.useRef(0);
  const preferenceSaveTimer = React.useRef<ReturnType<typeof setTimeout> | null>(null);
  const preferenceSaveInFlight = React.useRef(false);
  const preferenceSaveQueued = React.useRef(false);
  const resizingColumn = React.useRef<{ id: PlanningGridColumnId; startX: number; startWidth: number; pointerId: number } | null>(null);
  const pasteTaskLock = React.useRef(false);
  const draggingCells = React.useRef(false);
  const committingCellKeys = React.useRef(new Set<string>());

  const notifySaved = () => toast({ title: t("planning.saved"), description: t("planning.updated"), variant: "success" });
  const notifyError = React.useCallback((e: unknown) => toast({ title: t("common.error"), description: e instanceof Error ? e.message : t("planning.saveFailed"), variant: "destructive" }), [t]);

  const markPreferencesDirty = () => {
    if (!editable || preferencesHydratedPlan.current !== planId) return;
    preferencesDirty.current = true;
    preferencesRevision.current += 1;
  };

  const selectedId = selectedRowId ?? internalSelectedId;
  const collapsed = collapsedWbsIds ?? internalCollapsed;
  const onSelectionChangeRef = React.useRef(onSelectionChange);
  onSelectionChangeRef.current = onSelectionChange;
  const onCollapsedChangeRef = React.useRef(onCollapsedChange);
  onCollapsedChangeRef.current = onCollapsedChange;
  const applySelectedId = React.useCallback((rowId: string) => {
    setInternalSelectedId(rowId);
    onSelectionChangeRef.current?.(rowId);
  }, []);
  const applyCollapsed = React.useCallback((next: ReadonlySet<string>) => {
    setInternalCollapsed(next);
    onCollapsedChangeRef.current?.(next);
  }, []);

  React.useEffect(() => {
    preferencesHydratedPlan.current = null;
    preferencesDirty.current = false;
    preferencesRevision.current = 0;
    if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current);
    setColumns([...DEFAULT_PLANNING_GRID_COLUMNS]);
    setColumnFilters({});
    setColumnWidths(Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, value]) => [key, value.initial])) as Record<PlanningGridColumnId, number>);
    setSelectedRowIds(new Set());
    setSelectedCellKeys(new Set());
    setRowSelectionAnchorId("");
    setCellSelectionAnchor(null);
    setActiveCell(null);
    applyCollapsed(new Set());
  }, [planId, applyCollapsed]);

  React.useEffect(() => {
    const preference = tablePreferences.data;
    if (!preference || preference.planId !== planId || preferencesHydratedPlan.current === planId) return;
    setColumns(normalizePlanningGridColumns(preference.columnOrder.length ? preference.columnOrder : preference.visibleColumns));
    setColumnFilters(preference.columnFilters ?? {});
    setColumnWidths(Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, bounds]) => {
      const id = key as PlanningGridColumnId;
      return [id, clampPlanningGridWidth(preference.columnWidths[id] ?? bounds.initial, bounds.min, bounds.max)];
    })) as Record<PlanningGridColumnId, number>);
    applyCollapsed(new Set(preference.collapsedRowIds));
    preferencesHydratedPlan.current = planId;
    preferencesDirty.current = false;
  }, [planId, tablePreferences.data, applyCollapsed]);

  React.useEffect(() => {
    if (!preferencesDirty.current || preferencesHydratedPlan.current !== planId) return;
    if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current);
    preferenceSaveTimer.current = setTimeout(() => {
      preferenceSaveTimer.current = null;
      if (!preferencesDirty.current || preferencesHydratedPlan.current !== planId) return;
      if (preferenceSaveInFlight.current) { preferenceSaveQueued.current = true; return; }
      const requestRevision = preferencesRevision.current;
      const payload = {
        visibleColumns: columns,
        columnOrder: columns,
        columnWidths,
        columnFilters,
        collapsedRowIds: [...collapsed],
      };
      preferenceSaveInFlight.current = true;
      void savePreferencesRef.current(payload)
        .then(() => { if (preferencesRevision.current === requestRevision) preferencesDirty.current = false; })
        .catch((e: unknown) => { if (preferencesRevision.current === requestRevision) preferencesDirty.current = false; notifyError(e); })
        .finally(() => {
          preferenceSaveInFlight.current = false;
          if (preferenceSaveQueued.current || preferencesDirty.current) {
            preferenceSaveQueued.current = false;
            preferencesRevision.current += 1;
            setPreferenceSaveRevision((revision) => revision + 1);
          }
        });
    }, 500);
    return () => { if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current); };
  }, [collapsed, columnFilters, columnWidths, columns, notifyError, planId, preferenceSaveRevision]);

  React.useEffect(() => () => { if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current); }, []);

  const fullRows = React.useMemo(() => buildPlanWorkspaceRows(wbs, activities), [wbs, activities]);
  const rows = React.useMemo(() => visiblePlanWorkspaceRows(fullRows, collapsed), [fullRows, collapsed]);
  const selected = React.useMemo(() => fullRows.find((r) => r.id === selectedId), [fullRows, selectedId]);
  const wbsNodeForActivity = (activity: PlanActivityDto) => {
    const byWbsId = activity.wbsId ? findWbsNode(wbs, activity.wbsId) : undefined;
    if (byWbsId) return byWbsId;
    const findActivityLink = (nodes: PlanWbsNodeDto[]): PlanWbsNodeDto | undefined => {
      for (const node of nodes) {
        if (node.activityId === activity.id) return node;
        const nested = findActivityLink(node.children ?? []);
        if (nested) return nested;
      }
      return undefined;
    };
    return findActivityLink(wbs);
  };
  const siblingWbsNodes = React.useCallback((parentWbsId?: string) => {
    const nodes = fullRows.flatMap((row) => row.wbs ? [row.wbs] : []);
    const ids = new Set(nodes.map((node) => node.id));
    const effectiveParent = (node: PlanWbsNodeDto) => node.parentWbsId && ids.has(node.parentWbsId) ? node.parentWbsId : undefined;
    const requestedParent = parentWbsId && ids.has(parentWbsId) ? parentWbsId : undefined;
    return nodes.filter((node) => effectiveParent(node) === requestedParent).sort(comparePlanOrder);
  }, [fullRows]);
  const siblingParentForRow = (row: PlanWorkspaceRow | undefined): string | null => {
    if (row?.wbs) return row.wbs.parentWbsId && findWbsNode(wbs, row.wbs.parentWbsId) ? row.wbs.parentWbsId : null;
    return row?.activity?.wbsId && findWbsNode(wbs, row.activity.wbsId) ? row.activity.wbsId : null;
  };
  const rowWbsId = (row: PlanWorkspaceRow | undefined) => row?.wbs?.id ?? (row?.activity?.wbsId && findWbsNode(wbs, row.activity.wbsId) ? row.activity.wbsId : undefined);

  const siblingWbsNode = (target: PlanWorkspaceRow | undefined, direction: "up" | "down") => {
    if (!target?.wbs) return undefined;
    const siblings = siblingWbsNodes(target.wbs.parentWbsId);
    const index = siblings.findIndex((node) => node.id === target.wbs!.id);
    return siblings[index + (direction === "up" ? -1 : 1)];
  };

  const workspaceSiblingsForRow = (target: PlanWorkspaceRow | undefined) => {
    if (!target || (!target.wbs && !target.activity)) return [];
    const parentWbsId = siblingParentForRow(target);
    return fullRows.filter((row) => parentWbsId
      ? row.wbs?.parentWbsId === parentWbsId || (!row.wbs && row.activity?.wbsId === parentWbsId)
      : row.depth === 0).sort((left, right) => {
      const leftItem = left.wbs ?? left.activity;
      const rightItem = right.wbs ?? right.activity;
      if (!leftItem) return rightItem ? -1 : 0;
      if (!rightItem) return 1;
      return comparePlanOrder(leftItem, rightItem);
    });
  };
  const siblingWorkspaceRow = (target: PlanWorkspaceRow | undefined, direction: "up" | "down") => {
    if (!target) return undefined;
    const siblings = workspaceSiblingsForRow(target);
    const index = siblings.findIndex((row) => row.id === target.id);
    return siblings[index + (direction === "up" ? -1 : 1)];
  };

  const doMove = async (activityId: string, input: { targetActivityId?: string; targetWbsId?: string; position: "Before" | "After" | "Child"; sortOrder?: number }) => {
    const activity = activities.find((a) => a.id === activityId);
    if (!activity) return;
    const sourceNode = wbsNodeForActivity(activity);
    const targetActivity = input.targetActivityId ? activities.find((a) => a.id === input.targetActivityId) : undefined;
    const targetNode = targetActivity ? wbsNodeForActivity(targetActivity) : input.targetWbsId ? findWbsNode(wbs, input.targetWbsId) : undefined;
    if ((activity.wbsId && !sourceNode) || (targetActivity?.wbsId && !targetNode)) {
      setError(t("planning.moveMissingWbsLink"));
      return;
    }
    if (input.position === "Child" && !sourceNode) {
      setError(t("planning.moveChildRequiresWbs"));
      return;
    }
    if (input.position === "Child" && targetActivity && !targetNode) {
      setError(t("planning.moveChildTargetRequiresWbs"));
      return;
    }
    setMoving(true);
    setError(null);
    try {
      await mutations.moveActivity.mutateAsync({
        id: activityId,
        body: { ...input, rowVersion: activity.rowVersion, wbsRowVersion: (sourceNode as { rowVersion?: string } | undefined)?.rowVersion ?? "", targetWbsRowVersion: (targetNode as { rowVersion?: string } | undefined)?.rowVersion },
      });
      applySelectedId(`activity:${activityId}`);
    } catch (e) {
      void queryClient.invalidateQueries({ queryKey: planningKeys.wbs(projectId, planId) });
      void queryClient.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) });
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const handleActivityDragStart = (event: React.DragEvent<HTMLButtonElement>, activityId: string) => {
    if (!editable || moving) { event.preventDefault(); return; }
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", activityId);
    applySelectedId(`activity:${activityId}`);
    setDraggedActivityId(activityId);
    setDropTarget(null);
  };

  const handleRowDragOver = (event: React.DragEvent<HTMLTableRowElement>, row: PlanWorkspaceRow) => {
    if (!draggedActivityId || !row.activity || draggedActivityId === row.activity.id || moving) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
    const bounds = event.currentTarget.getBoundingClientRect();
    const offset = bounds.height ? (event.clientY - bounds.top) / bounds.height : 0.5;
    const position = offset < 1 / 3 ? "Before" : offset > 2 / 3 ? "After" : "Child";
    setDropTarget({ targetId: row.activity.id, position });
  };

  const handleRowDrop = (event: React.DragEvent<HTMLTableRowElement>, row: PlanWorkspaceRow) => {
    event.preventDefault();
    if (draggedActivityId && row.activity && draggedActivityId !== row.activity.id && dropTarget?.targetId === row.activity.id) {
      void doMove(draggedActivityId, { targetActivityId: row.activity.id, position: dropTarget.position });
    }
    setDraggedActivityId(null);
    setDropTarget(null);
  };

  const handleActivityDragEnd = () => { setDraggedActivityId(null); setDropTarget(null); };

  const updateWbsPlacement = async (node: PlanWbsNodeDto, parentWbsId: string | undefined, sortOrder: number) => {
    setMoving(true);
    setError(null);
    try {
      await mutations.updateWbs.mutateAsync({ id: node.id, body: { code: node.code, name: node.name, parentWbsId: parentWbsId ?? null, sortOrder, textFormat: node.textFormat, rowVersion: node.rowVersion } });
      applySelectedId(node.activityId ? `activity:${node.activityId}` : `wbs:${node.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const updateActivityPlacement = async (activity: PlanActivityDto, wbsId: string | undefined, sortOrder: number) => {
    setMoving(true);
    setError(null);
    try {
      await mutations.updateActivity.mutateAsync({ id: activity.id, body: {
        code: activity.code, name: activity.name, description: activity.description, type: activity.type,
        schedulingMode: activity.schedulingMode, constraintType: activity.constraintType, constraintDate: activity.constraintDate,
        deadline: activity.deadline, startDate: activity.startDate, endDate: activity.endDate, actualStart: activity.actualStart,
        actualFinish: activity.actualFinish, calendarId: activity.calendarId, duration: activity.duration,
        estimateHours: activity.estimateHours, progress: roundPlanProgress(activity.progress), wbsId: wbsId ?? null,
        sortOrder, textFormat: activity.textFormat, rowVersion: activity.rowVersion,
      } });
      applySelectedId(`activity:${activity.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const nextWorkspaceSortOrder = (parentWbsId?: string | null) => {
    const siblings = fullRows.filter((row) => parentWbsId
      ? row.wbs?.parentWbsId === parentWbsId || (!row.wbs && row.activity?.wbsId === parentWbsId)
      : row.depth === 0);
    return Math.max(-1, ...siblings.map((row) => row.wbs?.sortOrder ?? row.activity?.sortOrder ?? 0)) + 1;
  };
  const wouldCreateWbsCycle = (nodeId: string, parentWbsId: string) => {
    let current: string | undefined = parentWbsId;
    while (current) {
      if (current === nodeId) return true;
      current = findWbsNode(wbs, current)?.parentWbsId;
    }
    return false;
  };

  const previousIndentTarget = (target: PlanWorkspaceRow | undefined) => {
    if (!target) return undefined;
    const index = rows.findIndex((row) => row.id === target.id);
    if (index <= 0) return undefined;
    const sourceWbsId = rowWbsId(target);
    return [...rows.slice(0, index)].reverse().find((row) => {
      const candidateWbsId = rowWbsId(row);
      return row.depth === target.depth && candidateWbsId && candidateWbsId !== sourceWbsId;
    });
  };

  const structuralSelection = (target = selected) => {
    if (target && selectedRowIds.size > 1 && selectedRowIds.has(target.id))
      return fullRows.filter((row) => selectedRowIds.has(row.id));
    return target ? [target] : [];
  };

  const selectedSiblingActivities = (target = selected) => {
    const selectedRows = structuralSelection(target);
    if (selectedRows.length < 2 || selectedRows.some((row) => !row.activity || row.activity.isSummaryTask)) return null;
    const first = selectedRows[0]!;
    // Bulk Activity updates assign Activity.SortOrder independently. Do not
    // use them when the visible sibling sequence also contains WBS nodes.
    const workspaceSiblings = workspaceSiblingsForRow(first);
    if (workspaceSiblings.some((row) => row.wbs || row.activity?.wbsId !== first.activity!.wbsId)) return null;
    if (selectedRows.some((row) => row.depth !== first.depth || row.activity!.wbsId !== first.activity!.wbsId)) return null;
    const siblings = activities
      .filter((activity) => activity.wbsId === first.activity!.wbsId)
      .slice()
      .sort(comparePlanOrder);
    const selectedIds = new Set(selectedRows.map((row) => row.activity!.id));
    const indexes = siblings.map((activity, index) => selectedIds.has(activity.id) ? index : -1).filter((index) => index >= 0);
    if (indexes.length !== selectedRows.length || indexes.some((index, offset) => index !== indexes[0]! + offset)) return null;
    return { rows: selectedRows, siblings, startIndex: indexes[0]!, endIndex: indexes[indexes.length - 1]! };
  };

  const selectedWbsSiblingGroup = (target = selected) => {
    const selectedRows = structuralSelection(target);
    if (selectedRows.length < 2 || selectedRows.some((row) => !row.wbs)) return null;
    const first = selectedRows[0]!;
    // Bulk WBS updates are safe only when Activities do not share this
    // sibling sequence; otherwise they would be reordered independently.
    const workspaceSiblings = workspaceSiblingsForRow(first);
    if (workspaceSiblings.some((row) => !row.wbs)) return null;
    if (selectedRows.some((row) => row.depth !== first.depth || row.wbs!.parentWbsId !== first.wbs!.parentWbsId)) return null;
    const siblings = siblingWbsNodes(first.wbs!.parentWbsId);
    if (workspaceSiblings.length !== siblings.length || workspaceSiblings.some((row) => !siblings.some((node) => node.id === row.wbs!.id))) return null;
    const selectedIds = new Set(selectedRows.map((row) => row.wbs!.id));
    const indexes = siblings.map((node, index) => selectedIds.has(node.id) ? index : -1).filter((index) => index >= 0);
    if (indexes.length !== selectedRows.length || indexes.some((index, offset) => index !== indexes[0]! + offset)) return null;
    return { rows: selectedRows, siblings, startIndex: indexes[0]!, endIndex: indexes[indexes.length - 1]! };
  };

  const updateActivityStructureBulk = async (items: Array<{ activity: PlanActivityDto; wbsId: string | null; sortOrder: number }>) => {
    if (!items.length || items.length > 500) return;
    setMoving(true);
    setError(null);
    try {
      await mutations.bulkUpdateActivities.mutateAsync({ items: items.map(({ activity, wbsId, sortOrder }) => ({
        activityId: activity.id,
        update: {
          code: activity.code,
          name: activity.name,
          description: activity.description ?? null,
          type: activity.type,
          schedulingMode: activity.schedulingMode ?? "Automatic",
          constraintType: activity.constraintType ?? "AsSoonAsPossible",
          constraintDate: activity.constraintDate ?? null,
          deadline: activity.deadline ?? null,
          startDate: activity.startDate ?? null,
          endDate: activity.endDate ?? null,
          actualStart: activity.actualStart ?? null,
          actualFinish: activity.actualFinish ?? null,
          calendarId: activity.calendarId ?? null,
          duration: activity.duration,
          estimateHours: activity.estimateHours ?? null,
          progress: roundPlanProgress(activity.progress),
          wbsId,
          sortOrder,
          textFormat: activity.textFormat ?? null,
          rowVersion: activity.rowVersion,
        },
      })) });
      if (selected) applySelectedId(selected.id);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const updateWbsPlacementsBulk = async (items: Array<{ node: PlanWbsNodeDto; parentWbsId: string | null; sortOrder: number }>) => {
    if (!items.length || items.length > 500) return;
    setMoving(true);
    setError(null);
    try {
      await mutations.bulkUpdateWbsPlacements.mutateAsync(items.map(({ node, parentWbsId, sortOrder }) => ({
        nodeId: node.id,
        parentWbsId,
        sortOrder,
        rowVersion: node.rowVersion,
      })));
      if (selected) applySelectedId(selected.id);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const indent = async (target = selected) => {
    if (!target) return;
    const selectedRows = structuralSelection(target);
    if (selectedRows.length > 1) {
      const wbsGroup = selectedWbsSiblingGroup(target);
      if (wbsGroup) {
        const parentWbsId = rowWbsId(previousIndentTarget(wbsGroup.rows[0]));
        if (!parentWbsId || wbsGroup.rows.some((row) => row.wbs!.id === parentWbsId || wouldCreateWbsCycle(row.wbs!.id, parentWbsId))) return;
        const nextOrder = nextWorkspaceSortOrder(parentWbsId);
        await updateWbsPlacementsBulk(wbsGroup.rows.map((row, index) => ({ node: row.wbs!, parentWbsId, sortOrder: nextOrder + index })));
        return;
      }
      const group = selectedSiblingActivities(target);
      if (!group) return;
      const parentWbsId = rowWbsId(previousIndentTarget(group.rows[0]));
      if (!parentWbsId || group.rows.some((row) => row.activity!.wbsId === parentWbsId)) return;
      const nextOrder = nextWorkspaceSortOrder(parentWbsId);
      await updateActivityStructureBulk(group.rows.map((row, index) => ({ activity: row.activity!, wbsId: parentWbsId, sortOrder: nextOrder + index })));
      return;
    }
    const parentWbsId = rowWbsId(previousIndentTarget(target));
    if (!parentWbsId || parentWbsId === rowWbsId(target)) return;
    if (target.wbs) {
      if (wouldCreateWbsCycle(target.wbs.id, parentWbsId)) return;
      await updateWbsPlacement(target.wbs, parentWbsId, nextWorkspaceSortOrder(parentWbsId));
    } else if (target.activity) {
      await updateActivityPlacement(target.activity, parentWbsId, nextWorkspaceSortOrder(parentWbsId));
    }
  };

  const outdent = async (target = selected) => {
    const selectedRows = structuralSelection(target);
    if (selectedRows.length > 1) {
      const wbsGroup = selectedWbsSiblingGroup(target);
      if (wbsGroup) {
        const currentParent = wbsGroup.rows[0]!.wbs!.parentWbsId;
        if (!currentParent) return;
        const parent = findWbsNode(wbs, currentParent);
        if (!parent) return;
        const destinationWbsId = parent.parentWbsId ?? null;
        const nextOrder = nextWorkspaceSortOrder(destinationWbsId);
        await updateWbsPlacementsBulk(wbsGroup.rows.map((row, index) => ({ node: row.wbs!, parentWbsId: destinationWbsId, sortOrder: nextOrder + index })));
        return;
      }
      const group = selectedSiblingActivities(target);
      if (!group) return;
      const currentWbsId = group.rows[0]!.activity!.wbsId;
      if (!currentWbsId) return;
      const currentNode = findWbsNode(wbs, currentWbsId);
      if (!currentNode) return;
      const destinationWbsId = currentNode.parentWbsId ?? null;
      const nextOrder = nextWorkspaceSortOrder(destinationWbsId);
      await updateActivityStructureBulk(group.rows.map((row, index) => ({ activity: row.activity!, wbsId: destinationWbsId, sortOrder: nextOrder + index })));
      return;
    }
    if (target?.wbs?.parentWbsId) {
      const parent = findWbsNode(wbs, target.wbs.parentWbsId);
      await updateWbsPlacement(target.wbs, parent?.parentWbsId, nextWorkspaceSortOrder(parent?.parentWbsId));
      return;
    }
    if (target?.activity?.wbsId) {
      const currentNode = findWbsNode(wbs, target.activity.wbsId);
      if (!currentNode) return;
      const destinationWbsId = currentNode.parentWbsId;
      await updateActivityPlacement(target.activity, destinationWbsId, nextWorkspaceSortOrder(destinationWbsId));
    }
  };

  const moveSibling = async (direction: "up" | "down", target = selected) => {
    const selectedRows = structuralSelection(target);
    if (selectedRows.length > 1) {
      const wbsGroup = selectedWbsSiblingGroup(target);
      if (wbsGroup) {
        const adjacentIndex = direction === "up" ? wbsGroup.startIndex - 1 : wbsGroup.endIndex + 1;
        if (adjacentIndex < 0 || adjacentIndex >= wbsGroup.siblings.length || wbsGroup.siblings.length > 500) return;
        const reordered = wbsGroup.siblings.slice();
        const moved = reordered.splice(wbsGroup.startIndex, wbsGroup.rows.length);
        const insertionIndex = direction === "up" ? wbsGroup.startIndex - 1 : wbsGroup.startIndex + 1;
        reordered.splice(insertionIndex, 0, ...moved);
        const parentWbsId = wbsGroup.siblings[0]?.parentWbsId ?? null;
        const items = reordered.flatMap((node, index) => node.sortOrder === index ? [] : [{ node, parentWbsId, sortOrder: index }]);
        await updateWbsPlacementsBulk(items);
        return;
      }
      const group = selectedSiblingActivities(target);
      if (!group || group.siblings.length > 500) return;
      const adjacentIndex = direction === "up" ? group.startIndex - 1 : group.endIndex + 1;
      if (adjacentIndex < 0 || adjacentIndex >= group.siblings.length) return;
      const reordered = group.siblings.slice();
      const moved = reordered.splice(group.startIndex, group.rows.length);
      const insertionIndex = direction === "up" ? group.startIndex - 1 : group.startIndex + 1;
      reordered.splice(insertionIndex, 0, ...moved);
      const items = reordered.flatMap((activity, index) => activity.sortOrder === index ? [] : [{ activity, wbsId: activity.wbsId ?? null, sortOrder: index }]);
      await updateActivityStructureBulk(items);
      return;
    }
    if (target?.activity) {
      const adjacent = siblingWorkspaceRow(target, direction);
      if (!adjacent) return;
      await doMove(target.activity.id, {
        targetActivityId: adjacent.activity?.id,
        targetWbsId: adjacent.activity ? undefined : adjacent.wbs?.id,
        position: direction === "up" ? "Before" : "After",
      });
      return;
    }
    if (target?.wbs) {
      const adjacent = siblingWbsNode(target, direction);
      if (!adjacent) return;
      await updateWbsPlacementsBulk([
        { node: target.wbs, parentWbsId: target.wbs.parentWbsId ?? null, sortOrder: adjacent.sortOrder },
        { node: adjacent, parentWbsId: adjacent.parentWbsId ?? null, sortOrder: target.wbs.sortOrder },
      ]);
      applySelectedId(target.id);
      return;
    }
  };



  const visibleColumns = React.useMemo(() => {
    const withBaseline = showBaseline ? [...columns, ...BASELINE_COLUMNS.filter((c) => !columns.includes(c))] : columns;
    return withBaseline;
  }, [columns, showBaseline]);

  const rowNumberByActivity = React.useMemo(() => new Map(fullRows.flatMap((row) => row.activity ? [[row.activity.id, row.rowNumber] as const] : [])), [fullRows]);
  const activityByRowNumber = React.useMemo(() => new Map([...rowNumberByActivity].map(([id, num]) => [num, activities.find((a) => a.id === id)!])), [rowNumberByActivity, activities]);
  const predecessorTextByActivity = React.useMemo(() => {
    const map = new Map<string, string[]>();
    for (const dep of dependencies) {
      const num = rowNumberByActivity.get(dep.predecessorActivityId);
      if (num === undefined) continue;
      const list = map.get(dep.activityId) ?? [];
      list.push(formatPredecessor(num, dep.type, dep.lag));
      map.set(dep.activityId, list);
    }
    return new Map([...map].map(([k, v]) => [k, v.join(", ")] as const));
  }, [dependencies, rowNumberByActivity]);
  const depsByActivity = React.useMemo(() => {
    const map = new Map<string, PlanDependencyDto[]>();
    for (const dep of dependencies) {
      const list = map.get(dep.activityId) ?? [];
      list.push(dep);
      map.set(dep.activityId, list);
    }
    return map;
  }, [dependencies]);
  const successorCountByActivity = React.useMemo(() => {
    const counts = new Map<string, number>();
    dependencies.forEach((dependency) => counts.set(dependency.predecessorActivityId, (counts.get(dependency.predecessorActivityId) ?? 0) + 1));
    return counts;
  }, [dependencies]);
  const assignmentsByActivity = React.useMemo(() => {
    const map = new Map<string, PlanAssignmentDto[]>();
    (assignmentsQuery.data ?? []).forEach((assignment) => {
      const rows = map.get(assignment.activityId) ?? [];
      rows.push(assignment);
      map.set(assignment.activityId, rows);
    });
    return map;
  }, [assignmentsQuery.data]);
  const resourceNames = React.useMemo(() => new Map((resourcesQuery.data ?? []).map((resource: ResourceLookupDto) => [resource.id, resource.name])), [resourcesQuery.data]);
  const calendarNames = React.useMemo(() => new Map((calendarsQuery.data ?? []).map((calendar: PlanCalendarDto) => [calendar.id, calendar.name])), [calendarsQuery.data]);
  const defaultCalendarName = calendarsQuery.data?.find((calendar: PlanCalendarDto) => calendar.isDefault)?.name;

  const toggleCollapse = (wbsId: string) => {
    markPreferencesDirty();
    const next = new Set(collapsed);
    if (next.has(wbsId)) next.delete(wbsId);
    else next.add(wbsId);
    applyCollapsed(next);
  };
  const collapseAll = () => { markPreferencesDirty(); applyCollapsed(new Set(fullRows.filter((r) => r.wbs && r.hasChildren).map((r) => r.wbs!.id))); };
  const expandAll = () => { markPreferencesDirty(); applyCollapsed(new Set()); };
  const collapsibleWbsIds = React.useMemo(() => fullRows.filter((r) => r.wbs && r.hasChildren).map((r) => r.wbs!.id), [fullRows]);
  const selectedStructureRows = structuralSelection();
  const selectedWbsGroup = selectedStructureRows.length > 1 ? selectedWbsSiblingGroup() : null;
  const selectedActivityGroup = selectedStructureRows.length > 1 ? selectedSiblingActivities() : null;
  const indentParentWbsId = selected ? rowWbsId(previousIndentTarget(selectedStructureRows[0] ?? selected)) : undefined;
  const canIndentSelected = selectedStructureRows.length > 1
    ? Boolean((selectedWbsGroup && indentParentWbsId && selectedWbsGroup.rows.every((row) => row.wbs!.id !== indentParentWbsId && !wouldCreateWbsCycle(row.wbs!.id, indentParentWbsId))) ||
      (selectedActivityGroup && indentParentWbsId && selectedActivityGroup.rows.every((row) => row.activity!.wbsId !== indentParentWbsId)))
    : Boolean(indentParentWbsId && indentParentWbsId !== rowWbsId(selected) && (!selected?.wbs || !wouldCreateWbsCycle(selected.wbs.id, indentParentWbsId)));
  const selectedActivityWbsId = selectedActivityGroup?.rows[0]?.activity?.wbsId;
  const selectedActivityWbs = selectedActivityWbsId ? findWbsNode(wbs, selectedActivityWbsId) : undefined;
  const canOutdentSelected = selectedStructureRows.length > 1
    ? Boolean((selectedWbsGroup && selectedWbsGroup.rows[0]?.wbs?.parentWbsId) || (selectedActivityGroup && selectedActivityWbs))
    : Boolean(selected?.wbs?.parentWbsId || (selected?.activity?.wbsId && findWbsNode(wbs, selected.activity.wbsId)));
  const canMoveSelectedUp = selectedStructureRows.length > 1
    ? Boolean((selectedWbsGroup && selectedWbsGroup.startIndex > 0 && selectedWbsGroup.siblings.length <= 500) || (selectedActivityGroup && selectedActivityGroup.startIndex > 0 && selectedActivityGroup.siblings.length <= 500))
    : Boolean(selected && (selected.activity ? siblingWorkspaceRow(selected, "up") : siblingWbsNode(selected, "up")));
  const canMoveSelectedDown = selectedStructureRows.length > 1
    ? Boolean((selectedWbsGroup && selectedWbsGroup.endIndex < selectedWbsGroup.siblings.length - 1 && selectedWbsGroup.siblings.length <= 500) || (selectedActivityGroup && selectedActivityGroup.endIndex < selectedActivityGroup.siblings.length - 1 && selectedActivityGroup.siblings.length <= 500))
    : Boolean(selected && (selected.activity ? siblingWorkspaceRow(selected, "down") : siblingWbsNode(selected, "down")));
  const canToggleSelectedCollapse = Boolean(selected?.wbs && selected.hasChildren);
  const canCollapseAll = collapsibleWbsIds.some((id) => !collapsed.has(id));
  const canExpandAll = collapsibleWbsIds.some((id) => collapsed.has(id));
  const selectedIsCollapsed = Boolean(selected?.wbs && collapsed.has(selected.wbs.id));
  const toggleSelectedCollapse = () => { if (selected?.wbs && selected.hasChildren) toggleCollapse(selected.wbs.id); };

  const isEditable = (row: PlanWorkspaceRow, column: PlanningGridColumnId) => {
    if (!editable) return false;
    if (column === "taskName") return Boolean(row.activity || row.wbs);
    if (!row.activity) return false;
    if (row.activity.isSummaryTask && ["start", "finish", "duration", "progress"].includes(column)) return false;
    if (column === "duration") return row.activity.type !== PLANNING_ACTIVITY_TYPE.milestone;
    return column === "start" || column === "finish" || column === "actualStart" || column === "actualFinish" || column === "constraintDate" || column === "deadline" || column === "progress" || column === "predecessors";
  };

  const cellText = (row: PlanWorkspaceRow, column: PlanningGridColumnId): string => {
    const a = row.activity;
    switch (column) {
      case "id": return String(row.rowNumber);
      case "wbs": return row.outlineNumber ?? row.wbs?.code ?? a?.code ?? "";
      case "outlineLevel": return String(row.depth + 1);
      case "taskName": return a?.name ?? row.wbs?.name ?? "";
      case "type": return a?.type ?? (row.wbs ? "WBS" : "");
      case "schedulingMode": return a?.schedulingMode ?? "";
      case "schedulingStatus": {
        if (!a) return "";
        const statuses: string[] = [a.schedulingMode ?? "Automatic"];
        if (a.constraintApplied) statuses.push("Constraint");
        if (a.deadlineExceeded) statuses.push("DeadlineExceeded");
        if (a.schedulingConflict) statuses.push("SchedulingConflict");
        if (a.critical) statuses.push("Critical");
        if (predecessorTextByActivity.get(a.id)) statuses.push("Predecessors");
        if ((successorCountByActivity.get(a.id) ?? 0) > 0) statuses.push("Successors");
        if (a.calendarOverride) statuses.push("CalendarOverride");
        return statuses.join(" ");
      }
      case "constraintType": return a?.constraintType ?? "";
      case "constraintDate": return toDateInput(a?.constraintDate);
      case "deadline": return toDateInput(a?.deadline);
      case "duration": return a ? String(a.duration) : "";
      case "start": return toDateInput(a?.startDate);
      case "finish": return toDateInput(a?.endDate);
      case "calendar": return a ? (a.calendarId ? calendarNames.get(a.calendarId) ?? a.calendarId : defaultCalendarName ?? "") : "";
      case "actualStart": return toDateInput(a?.actualStart);
      case "actualFinish": return toDateInput(a?.actualFinish);
      case "progress": return a?.progress == null ? "" : String(roundPlanProgress(a.progress) ?? "");
      case "predecessors": return a ? predecessorTextByActivity.get(a.id) ?? "" : "";
      case "baselineStart": return toDateInput(a?.baselineStartDate);
      case "baselineFinish": return toDateInput(a?.baselineFinishDate);
      case "baselineDuration": return a?.baselineDuration == null ? "" : String(a.baselineDuration);
      case "startVariance": return a?.startVarianceDays == null ? "" : String(a.startVarianceDays);
      case "finishVariance": return a?.finishVarianceDays == null ? "" : String(a.finishVarianceDays);
      case "durationVariance": return a?.durationVarianceDays == null ? "" : String(a.durationVarianceDays);
      case "critical": return a ? (a.critical ? "Yes" : "No") : "";
      case "estimateHours": return a?.estimateHours == null ? "" : String(a.estimateHours);
      case "resources": return a ? (assignmentsByActivity.get(a.id) ?? []).map((assignment) => assignment.resourceName ?? resourceNames.get(assignment.resourceId) ?? assignment.resourceId.slice(0, 8)).join(", ") : "";
      case "description": return a?.description ?? "";
    }
  };

  const displayCellText = (row: PlanWorkspaceRow, column: PlanningGridColumnId) => {
    const value = cellText(row, column);
    if (column === "schedulingMode") return value ? t(value === "Manual" ? "planning.manual" : "planning.automatic") : "";
    if (column === "schedulingStatus") return value.split(" ").map((status) => t(`planning.schedulingStatusValues.${status}`)).join(" · ");
    if (column === "constraintType") return value ? t(`planning.constraintTypes.${value}`) : "";
    if (column === "type") return value === "WBS" ? t("planning.columns.wbs") : value === "Task" ? t("planning.task") : value === "Milestone" ? t("planning.milestone") : value;
    if (column === "critical") return value === "Yes" ? t("common.yes") : value === "No" ? t("common.no") : "";
    return value;
  };

  const setColumnFilter = (column: PlanningGridColumnId, filter: PlanningColumnFilterDto) => {
    markPreferencesDirty();
    setColumnFilters((current) => {
      const next = { ...current };
      if (Object.values(filter).some((value) => Boolean(value?.trim()))) next[column] = filter;
      else delete next[column];
      return next;
    });
  };
  const activeColumnFilterCount = visibleColumns.filter((column) => columnFilters[column] && Object.values(columnFilters[column]).some((value) => Boolean(value?.trim()))).length;
  const filteredRows = rows.filter((row) => visibleColumns.every((column) => matchesPlanningGridFilter(cellText(row, column), columnFilters[column], getPlanningGridFilterKind(column))));
  const rowIds = React.useMemo(() => new Set(fullRows.map((row) => row.id)), [fullRows]);
  React.useEffect(() => {
    setSelectedRowIds((current) => new Set([...current].filter((id) => rowIds.has(id))));
    setSelectedCellKeys((current) => new Set([...current].filter((key) => {
      const separator = key.lastIndexOf("::");
      return separator >= 0 && rowIds.has(key.slice(0, separator)) && visibleColumns.includes(key.slice(separator + 2) as PlanningGridColumnId);
    })));
    if (activeCell && (!rowIds.has(activeCell.rowId) || !visibleColumns.includes(activeCell.column))) setActiveCell(null);
    if (cellSelectionAnchor && (!rowIds.has(cellSelectionAnchor.rowId) || !visibleColumns.includes(cellSelectionAnchor.column))) setCellSelectionAnchor(null);
    if (rowSelectionAnchorId && !rowIds.has(rowSelectionAnchorId)) setRowSelectionAnchorId("");
  }, [activeCell, cellSelectionAnchor, rowIds, rowSelectionAnchorId, visibleColumns]);
  React.useEffect(() => {
    const stopDragging = () => { draggingCells.current = false; };
    window.addEventListener("mouseup", stopDragging);
    window.addEventListener("blur", stopDragging);
    return () => { stopDragging(); window.removeEventListener("mouseup", stopDragging); window.removeEventListener("blur", stopDragging); };
  }, []);
  const filterOptionLabel = (column: PlanningGridColumnId, value: string) => {
    if (column === "schedulingMode") return t(value === "Manual" ? "planning.manual" : "planning.automatic");
    if (column === "constraintType") return t(`planning.constraintTypes.${value}`);
    if (column === "type") return value === "WBS" ? t("planning.columns.wbs") : t(`planning.${value.toLowerCase()}`);
    if (column === "critical") return t(value === "Yes" ? "common.yes" : "common.no");
    return value;
  };

  const toggleColumn = (columnId: PlanningGridColumnId) => {
    if (REQUIRED_PLANNING_GRID_COLUMNS.includes(columnId)) return;
    markPreferencesDirty();
    setColumns((current) => current.includes(columnId) ? current.filter((id) => id !== columnId) : [...current, columnId]);
  };

  const resetTableLayout = () => {
    if (resetTablePreferences.isPending) return;
    if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current);
    preferencesDirty.current = false;
    preferencesRevision.current += 1;
    setColumns([...DEFAULT_PLANNING_GRID_COLUMNS]);
    setColumnFilters({});
    setColumnWidths(Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, value]) => [key, value.initial])) as Record<PlanningGridColumnId, number>);
    applyCollapsed(new Set());
    void resetTablePreferences.mutateAsync().catch(notifyError);
  };

  const beginColumnResize = (columnId: PlanningGridColumnId, event: React.PointerEvent<HTMLSpanElement>) => {
    if (!editable) return;
    event.preventDefault();
    event.stopPropagation();
    resizingColumn.current = { id: columnId, startX: event.clientX, startWidth: columnWidths[columnId] ?? COLUMN_WIDTHS[columnId].initial, pointerId: event.pointerId };
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const moveColumnResize = (event: React.PointerEvent<HTMLSpanElement>) => {
    const active = resizingColumn.current;
    if (!active || active.pointerId !== event.pointerId) return;
    const bounds = COLUMN_WIDTHS[active.id];
    const width = clampPlanningGridWidth(active.startWidth + event.clientX - active.startX, bounds.min, bounds.max);
    markPreferencesDirty();
    setColumnWidths((current) => current[active.id] === width ? current : { ...current, [active.id]: width });
  };

  const endColumnResize = (event: React.PointerEvent<HTMLSpanElement>) => {
    if (resizingColumn.current?.pointerId === event.pointerId && event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    resizingColumn.current = null;
  };

  const commitCell = async (row: PlanWorkspaceRow, column: PlanningGridColumnId, raw: string) => {
    const cellKey = `${row.id}:${column}`;
    if (committingCellKeys.current.has(cellKey)) return;
    committingCellKeys.current.add(cellKey);
    setEditing(null);
    setError(null);
    const value = raw.trim();
    try {
      if (column === "taskName" && row.wbs && !row.activity) {
        if (value && value !== row.wbs.name) {
          setSaving(true);
          const node = findWbsNode(wbs, row.wbs.id) ?? row.wbs;
          await mutations.updateWbs.mutateAsync({ id: node.id, body: { code: node.code, name: value, parentWbsId: node.parentWbsId ?? null, sortOrder: node.sortOrder, textFormat: (node as { textFormat?: unknown }).textFormat ?? null, rowVersion: node.rowVersion } });
          notifySaved();
        }
        return;
      }
      const activity = row.activity;
      if (!activity) return;
      if (column === "predecessors") {
        await savePredecessors(activity, value);
        return;
      }
      const patch: Record<string, unknown> = {};
      if (column === "taskName") { if (!value || value === activity.name) return; patch.name = value; }
      else if (column === "duration") { const n = Number(value); if (!Number.isFinite(n) || n < 0 || n === activity.duration) return; patch.duration = n; }
      else if (column === "progress") { const rawProgress = Number(value); if (!Number.isFinite(rawProgress) || rawProgress < 0 || rawProgress > 100) return; const progress = roundPlanProgress(rawProgress)!; if (progress === activity.progress) return; patch.progress = progress; }
      else if (column === "schedulingMode") { if (!(value === "Automatic" || value === "Manual") || value === (activity.schedulingMode ?? "Automatic")) return; patch.schedulingMode = value; }
      else if (column === "constraintType") { if (!(PLAN_CONSTRAINT_TYPES as readonly string[]).includes(value) || value === (activity.constraintType ?? "AsSoonAsPossible")) return; patch.constraintType = value; }
      else if (column === "start" || column === "finish" || column === "actualStart" || column === "actualFinish" || column === "constraintDate" || column === "deadline") {
        if (value && !/^\d{4}-\d{2}-\d{2}$/.test(value)) return;
        const field = column === "start" ? "startDate" : column === "finish" ? "endDate" : column;
        const current = toDateInput((activity as unknown as Record<string, string | undefined>)[field]);
        if (value === current) return;
        if (column === "start" || column === "finish") {
          const other = column === "start" ? toDateInput(activity.endDate) : toDateInput(activity.startDate);
          const nextStart = (column === "start" ? value : other).slice(0, 10);
          const nextFinish = (column === "finish" ? value : other).slice(0, 10);
          if (nextStart && nextFinish && nextFinish < nextStart) throw new Error(t("planning.invalidDateRange"));
          // A date edited directly in the plan grid is an explicit user
          // schedule. Preserve the entered date and the other schedule fields
          // by saving the activity in Manual mode.
          patch.schedulingMode = "Manual";
        }
        patch[field] = value || null;
      }
      else return;
      const actualStart = ("actualStart" in patch ? patch.actualStart : activity.actualStart) as string | null | undefined;
      const actualFinish = ("actualFinish" in patch ? patch.actualFinish : activity.actualFinish) as string | null | undefined;
      if (actualFinish && !actualStart) throw new Error(t("planning.actualFinishRequiresStart"));
      const constraintType = (patch.constraintType as string | undefined) ?? activity.constraintType;
      const constraintDate = "constraintDate" in patch ? patch.constraintDate : activity.constraintDate;
      if (constraintType && !["AsSoonAsPossible", "AsLateAsPossible"].includes(constraintType) && !constraintDate) throw new Error(t("planning.constraintDateRequired"));
      setSaving(true);
      await mutations.updateActivity.mutateAsync({
        id: activity.id,
        body: {
          code: activity.code,
          name: patch.name ?? activity.name,
          description: activity.description ?? null,
          type: activity.type,
          schedulingMode: patch.schedulingMode ?? activity.schedulingMode,
          constraintType: patch.constraintType ?? activity.constraintType,
          constraintDate: ("constraintDate" in patch ? patch.constraintDate : activity.constraintDate) ?? null,
          deadline: ("deadline" in patch ? patch.deadline : activity.deadline) ?? null,
          startDate: ("startDate" in patch ? patch.startDate : activity.startDate) ?? null,
          endDate: ("endDate" in patch ? patch.endDate : activity.endDate) ?? null,
          actualStart: ("actualStart" in patch ? patch.actualStart : activity.actualStart) ?? null,
          actualFinish: ("actualFinish" in patch ? patch.actualFinish : activity.actualFinish) ?? null,
          calendarId: activity.calendarId ?? null,
          duration: activity.type === "Milestone" ? 0 : (patch.duration as number | undefined) ?? activity.duration,
          estimateHours: activity.estimateHours ?? null,
          progress: roundPlanProgress(("progress" in patch ? patch.progress : activity.progress) as number | null | undefined),
          wbsId: activity.wbsId ?? null,
          sortOrder: activity.sortOrder,
          rowVersion: activity.rowVersion,
        },
      });
      notifySaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setSaving(false);
      committingCellKeys.current.delete(cellKey);
    }
  };

  const savePredecessors = async (activity: PlanActivityDto, text: string) => {
    const wanted = text.split(",").map((s) => s.trim()).filter(Boolean);
    const parsed = wanted.map(parsePredecessor).filter((p): p is NonNullable<typeof p> => Boolean(p));
    if (parsed.length !== wanted.length) { setError(t("planning.predecessorSyntaxError")); return; }
    const predecessors: PlanPredecessorInputDto[] = [];
    const predecessorIds = new Set<string>();
    for (const p of parsed) {
      const pred = activityByRowNumber.get(p.rowNumber);
      if (!pred || pred.id === activity.id) { setError(t("planning.predecessorSyntaxError")); return; }
      if (predecessorIds.has(pred.id)) { setError(t("planning.predecessorSyntaxError")); return; }
      predecessorIds.add(pred.id);
      predecessors.push({ predecessorActivityId: pred.id, type: p.type, lag: p.lag });
    }
    setSaving(true);
    try {
      await mutations.replacePredecessorsBulk.mutateAsync([{ activityId: activity.id, predecessors }]);
      notifySaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setSaving(false);
    }
  };

  const selectedLeafActivities = () => {
    const selectedRows = fullRows.filter((row) => selectedRowIds.has(row.id));
    if (selectedRows.length < 2 || selectedRows.some((row) => !row.activity || row.isSummary || row.activity.isSummaryTask)) return [];
    return selectedRows.map((row) => row.activity!);
  };

  const selectedLeafRows = () => {
    const selectedRows = selectedRowIds.size > 0
      ? fullRows.filter((row) => selectedRowIds.has(row.id))
      : selected?.activity ? [selected] : [];
    if (!selectedRows.length || selectedRows.length > 500 || selectedRows.some((row) => !row.activity || row.isSummary || row.activity.isSummaryTask)) return [];
    return selectedRows;
  };

  const updateSelectedActivities = async (patch: { schedulingMode?: "Automatic" | "Manual"; progress?: number }) => {
    const rows = selectedLeafRows();
    if (!editable || !rows.length || saving) return;
    setSaving(true);
    try {
      await mutations.bulkUpdateActivities.mutateAsync({ items: rows.map((row) => {
        const activity = row.activity!;
        return {
          activityId: activity.id,
          update: {
            code: activity.code,
            name: activity.name,
            description: activity.description ?? null,
            type: activity.type,
            schedulingMode: patch.schedulingMode ?? activity.schedulingMode ?? "Automatic",
            constraintType: activity.constraintType ?? "AsSoonAsPossible",
            constraintDate: activity.constraintDate ?? null,
            deadline: activity.deadline ?? null,
            startDate: activity.startDate ?? null,
            endDate: activity.endDate ?? null,
            actualStart: activity.actualStart ?? null,
            actualFinish: activity.actualFinish ?? null,
            calendarId: activity.calendarId ?? null,
            duration: activity.duration,
            estimateHours: activity.estimateHours ?? null,
            progress: roundPlanProgress(patch.progress ?? activity.progress),
            wbsId: activity.wbsId ?? null,
            sortOrder: activity.sortOrder,
            textFormat: activity.textFormat ?? null,
            rowVersion: activity.rowVersion,
          },
        };
      }) });
      notifySaved();
    } catch (e) {
      notifyError(e);
    } finally {
      setSaving(false);
    }
  };

  const linkSelectedActivities = async () => {
    const selected = selectedLeafActivities();
    if (!editable || selected.length < 2) return;
    const selectedIds = new Set(selected.map((activity) => activity.id));
    const items: ReplacePlanPredecessorsInput[] = selected.slice(1).map((activity, index) => {
      const existing = depsByActivity.get(activity.id) ?? [];
      const predecessors: PlanPredecessorInputDto[] = existing.map((dependency) => ({ predecessorActivityId: dependency.predecessorActivityId, type: dependency.type, lag: dependency.lag }));
      const predecessorActivityId = selected[index]!.id;
      if (!predecessors.some((predecessor) => predecessor.predecessorActivityId === predecessorActivityId))
        predecessors.push({ predecessorActivityId, type: "FinishToStart", lag: 0 });
      return { activityId: activity.id, predecessors };
    }).filter((item) => item.predecessors.some((predecessor) => selectedIds.has(predecessor.predecessorActivityId)));
    if (!items.length) return;
    setSaving(true);
    try { await mutations.replacePredecessorsBulk.mutateAsync(items); notifySaved(); }
    catch (e) { setError(e instanceof Error ? e.message : t("common.error")); }
    finally { setSaving(false); }
  };

  const unlinkSelectedActivities = async () => {
    const selected = selectedLeafActivities();
    if (!editable || selected.length < 2) return;
    const selectedIds = new Set(selected.map((activity) => activity.id));
    const items = selected.flatMap((activity) => {
      const existing = depsByActivity.get(activity.id) ?? [];
      if (!existing.some((dependency) => selectedIds.has(dependency.predecessorActivityId))) return [];
      const predecessors: PlanPredecessorInputDto[] = existing
        .filter((dependency) => !selectedIds.has(dependency.predecessorActivityId))
        .map((dependency) => ({ predecessorActivityId: dependency.predecessorActivityId, type: dependency.type, lag: dependency.lag }));
      return [{ activityId: activity.id, predecessors }];
    });
    if (!items.length) return;
    setSaving(true);
    try { await mutations.replacePredecessorsBulk.mutateAsync(items); notifySaved(); }
    catch (e) { setError(e instanceof Error ? e.message : t("common.error")); }
    finally { setSaving(false); }
  };

  const quickAdd = async (type: "Task" | "Milestone") => {
    if (!quickName.trim()) return;
    await createRow(type, "append", quickName.trim());
    setQuickName("");
  };

  const createPastedTasks = async (names: string[]) => {
    if (!editable || creating || pastingTasks || pasteTaskLock.current) return;
    pasteTaskLock.current = true;
    setPastingTasks(true);
    let createdCount = 0;
    try {
      for (const name of names) {
        if (await createRow("Task", "append", name)) createdCount += 1;
        else break;
      }
      if (createdCount > 0) toast({ title: t("planning.created"), description: t("planning.updated"), variant: "success" });
    } finally {
      setPastingTasks(false);
      pasteTaskLock.current = false;
    }
  };

  const createRow = async (type: "Task" | "Milestone", placement: "append" | "above" | "below" | "child", nameOverride?: string, anchorOverride?: PlanWorkspaceRow): Promise<boolean> => {
    if (!editable || creating) return false;
    const anchor = placement === "append" ? undefined : anchorOverride ?? selected;
    let parentWbsId = anchor ? siblingParentForRow(anchor) : null;
    let sortOrder = nextWorkspaceSortOrder(parentWbsId);
    if (placement === "child") {
      parentWbsId = rowWbsId(anchor) ?? null;
      if (!parentWbsId) return false;
      sortOrder = nextWorkspaceSortOrder(parentWbsId);
    }
    // Create at the end of the intended sibling set, then use the same move
    // operation as drag/drop to place it before or after the selected row.
    // This lets the API normalize mixed WBS/Task/Milestone sibling ordering
    // without relying on colliding anchor.SortOrder +/- 1 values.
    const placedOrder = sortOrder;
    const code = `${type === "Milestone" ? "M" : "T"}-${Math.random().toString(36).slice(2, 10).toUpperCase()}`;
    setCreating(true);
    setError(null);
    let createdNodeId: string | undefined;
    let createdNodeVersion: string | undefined;
    let createdActivityId: string | undefined;
    let activityCreateAttempted = false;
    try {
      const node = await mutations.createWbs.mutateAsync({ code, name: nameOverride?.trim() || (type === "Milestone" ? t("planning.newMilestone") : t("planning.newTask")), parentWbsId, sortOrder: placedOrder }) as PlanWbsNodeDto;
      createdNodeId = node.id;
      createdNodeVersion = node.rowVersion;
      const isFirstActivity = activities.length === 0;
      const initialStartDate = isFirstActivity && projectStartDate ? toDateInput(projectStartDate) : undefined;
      const initialMilestoneFinishDate = isFirstActivity && type === "Milestone" ? initialStartDate : undefined;
      activityCreateAttempted = true;
      const createdActivity = await mutations.createActivity.mutateAsync({
        code,
        name: node.name,
        type,
        schedulingMode: isFirstActivity ? "Manual" : "Automatic",
        duration: type === "Milestone" ? 0 : 1,
        progress: 0,
        wbsId: node.id,
        sortOrder: placedOrder,
        ...(initialStartDate ? { startDate: initialStartDate } : {}),
        ...(initialMilestoneFinishDate ? { endDate: initialMilestoneFinishDate } : {}),
      }) as PlanActivityDto;
      createdActivityId = createdActivity.id;
      if (anchor && (placement === "above" || placement === "below")) {
        const targetNode = anchor.activity ? wbsNodeForActivity(anchor.activity) : anchor.wbs;
        if (anchor.activity?.wbsId && !targetNode) throw new Error(t("planning.moveMissingWbsLink"));
        await mutations.moveActivity.mutateAsync({
          id: createdActivity.id,
          body: {
            targetActivityId: anchor.activity?.id,
            targetWbsId: anchor.activity ? undefined : anchor.wbs?.id,
            position: placement === "above" ? "Before" : "After",
            rowVersion: createdActivity.rowVersion,
            wbsRowVersion: node.rowVersion,
            targetWbsRowVersion: targetNode?.rowVersion,
          },
        });
      }
      const createdRowId = `activity:${createdActivity.id}`;
      setSelectedRowIds(new Set([createdRowId]));
      setSelectedCellKeys(new Set());
      setRowSelectionAnchorId(createdRowId);
      setCellSelectionAnchor(null);
      setActiveCell(null);
      applySelectedId(createdRowId);
      notifySaved();
      return true;
    } catch (e) {
      if (createdActivityId) {
        // A failed response can arrive after the server committed the move.
        // Keep the row and reload authoritative placement before the user acts
        // again instead of compensating with a destructive delete.
        void queryClient.invalidateQueries({ queryKey: planningKeys.wbs(projectId, planId) });
        void queryClient.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) });
        setError(t("planning.insertMoveFailed"));
      } else if (activityCreateAttempted && e instanceof ApiError && e.status >= 400 && e.status < 500 && createdNodeId) {
        // A client-side rejection confirms no Activity was created, so the
        // WBS placeholder can be safely removed.
        try { await mutations.deleteWbs.mutateAsync({ id: createdNodeId, rowVersion: createdNodeVersion ?? "" }); } catch { /* Keep the visible WBS node if cleanup fails. */ }
        notifyError(e);
      } else if (activityCreateAttempted) {
        // The Activity request may have committed even if its response was
        // lost, so preserve the WBS node and refresh before retrying.
        void queryClient.invalidateQueries({ queryKey: planningKeys.wbs(projectId, planId) });
        void queryClient.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) });
        setError(t("planning.createActivityOutcomeUnknown"));
      } else if (createdNodeId) {
        // Only compensate when Activity creation was never attempted and the
        // newly created WBS node is known to be unused.
        try { await mutations.deleteWbs.mutateAsync({ id: createdNodeId, rowVersion: createdNodeVersion ?? "" }); } catch { /* Original error stands; the WBS row remains visible. */ }
        notifyError(e);
      } else {
        notifyError(e);
      }
      return false;
    } finally {
      setCreating(false);
    }
  };

  const updateTextFormat = async (format: Record<string, unknown> | null) => {
    if (!editable || !selected) return;
    setSaving(true);
    try {
      const selectedRows = selectedRowIds.size > 1 ? fullRows.filter((row) => selectedRowIds.has(row.id)) : [];
      if (selectedRows.length > 1) {
        if (selectedRows.some((row) => !row.activity)) throw new Error(t("planning.pasteInvalid"));
        const items = selectedRows.map((row) => {
          const a = row.activity!;
          return { activityId: a.id, update: { code: a.code, name: a.name, description: a.description ?? null, type: a.type, schedulingMode: a.schedulingMode ?? "Automatic", constraintType: a.constraintType ?? "AsSoonAsPossible", constraintDate: a.constraintDate ?? null, deadline: a.deadline ?? null, startDate: a.startDate ?? null, endDate: a.endDate ?? null, actualStart: a.actualStart ?? null, actualFinish: a.actualFinish ?? null, calendarId: a.calendarId ?? null, duration: a.duration, estimateHours: a.estimateHours ?? null, progress: roundPlanProgress(a.progress), wbsId: a.wbsId ?? null, sortOrder: a.sortOrder, textFormat: format, rowVersion: a.rowVersion } };
        });
        await mutations.bulkUpdateActivities.mutateAsync({ items });
      } else if (selected.activity) {
        const a = selected.activity;
        await mutations.updateActivity.mutateAsync({ id: a.id, body: { code: a.code, name: a.name, description: a.description ?? null, type: a.type, schedulingMode: a.schedulingMode, constraintType: a.constraintType, constraintDate: a.constraintDate ?? null, deadline: a.deadline ?? null, startDate: a.startDate ?? null, endDate: a.endDate ?? null, actualStart: a.actualStart ?? null, actualFinish: a.actualFinish ?? null, calendarId: a.calendarId ?? null, duration: a.duration, estimateHours: a.estimateHours ?? null, progress: roundPlanProgress(a.progress), wbsId: a.wbsId ?? null, sortOrder: a.sortOrder, textFormat: format, rowVersion: a.rowVersion } });
      }
      if (selected.wbs) {
        const node = findWbsNode(wbs, selected.wbs.id) ?? selected.wbs;
        await mutations.updateWbs.mutateAsync({ id: node.id, body: { code: node.code, name: node.name, parentWbsId: node.parentWbsId ?? null, sortOrder: node.sortOrder, textFormat: format, rowVersion: node.rowVersion } });
      }
      notifySaved();
    } catch (e) {
      notifyError(e);
    } finally {
      setSaving(false);
    }
  };

  const toggleFormat = (property: "isBold" | "isItalic" | "isUnderline") => {
    const current = (selected?.activity as { textFormat?: Record<string, unknown> } | undefined)?.textFormat ?? (selected?.wbs as unknown as { textFormat?: Record<string, unknown> } | undefined)?.textFormat;
    if (!current) void updateTextFormat({ isBold: property === "isBold", isItalic: property === "isItalic", isUnderline: property === "isUnderline" });
    else void updateTextFormat({ ...current, [property]: !current[property] });
  };

  const removeRow = async (target = selected) => {
    if (!editable || !target || (!target.activity && !target.wbs) || saving || moving) return;
    if (!assignmentsQuery.isSuccess || assignmentsQuery.isError) {
      setError(t("planning.deleteAssignmentsUnavailable"));
      return;
    }

    const targetIndex = fullRows.findIndex((row) => row.id === target.id);
    const subtreeEndIndex = fullRows.findIndex((row, index) => index > targetIndex && row.depth <= target.depth);
    const subtreeRows = targetIndex < 0 ? [target] : fullRows.slice(targetIndex, subtreeEndIndex < 0 ? fullRows.length : subtreeEndIndex);
    const subtreeActivities = [...new Map(subtreeRows.flatMap((row) => row.activity ? [[row.activity.id, row.activity] as const] : [])).values()];
    const subtreeWbs = [...new Map(subtreeRows.slice().reverse().flatMap((row) => row.wbs ? [[row.wbs.id, row.wbs] as const] : [])).values()];
    const activityIds = new Set(subtreeActivities.map((activity) => activity.id));
    const relatedDependencies = dependencies.filter((dependency) => activityIds.has(dependency.activityId) || activityIds.has(dependency.predecessorActivityId));
    const relatedAssignments = (assignmentsQuery.data ?? []).filter((assignment) => activityIds.has(assignment.activityId));
    const isCascade = subtreeRows.length > 1;
    if (!await confirm({ description: t(isCascade ? "planning.confirmDeleteSummary" : "planning.confirmDelete"), destructive: true })) return;

    setSaving(true);
    let completedDeletions = 0;
    try {
      for (const dependency of relatedDependencies) {
        await mutations.deleteDependency.mutateAsync({ id: dependency.id, rowVersion: dependency.rowVersion });
        completedDeletions += 1;
      }
      for (const assignment of relatedAssignments) {
        await mutations.deleteAssignment.mutateAsync({ id: assignment.id, rowVersion: assignment.rowVersion });
        completedDeletions += 1;
      }
      const latestActivities = relatedDependencies.length > 0 ? await planningApiFull.activities(projectId, planId) : activities;
      const latestActivityById = new Map(latestActivities.map((activity) => [activity.id, activity]));
      for (const activity of subtreeActivities) {
        const latest = latestActivityById.get(activity.id) ?? activity;
        await mutations.deleteActivity.mutateAsync({ id: latest.id, rowVersion: latest.rowVersion });
        completedDeletions += 1;
      }
      for (const node of subtreeWbs) {
        const latest = findWbsNode(wbs, node.id) ?? node;
        await mutations.deleteWbs.mutateAsync({ id: latest.id, rowVersion: latest.rowVersion });
        completedDeletions += 1;
      }
      applySelectedId("");
      notifySaved();
    } catch (e) {
      if (completedDeletions > 0) toast({ title: t("common.error"), description: t("planning.deletePartialFailure"), variant: "destructive" });
      else notifyError(e);
    } finally {
      setSaving(false);
    }
  };

  const openContextMenu = (row: PlanWorkspaceRow, event: React.MouseEvent<HTMLElement>) => {
    if (!row.activity && !row.wbs) return;
    event.preventDefault();
    if (!selectedRowIds.has(row.id)) {
      setSelectedRowIds(new Set([row.id]));
      setRowSelectionAnchorId(row.id);
      setSelectedCellKeys(new Set());
    }
    applySelectedId(row.id);
    setEditing(null);
    setContextMenu({ rowId: row.id, x: event.clientX, y: event.clientY });
  };

  React.useEffect(() => {
    if (!contextMenu) return;
    const closeOutside = (event: MouseEvent) => {
      if (contextMenuRef.current?.contains(event.target as Node)) return;
      setContextMenu(null);
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setContextMenu(null); };
    document.addEventListener("mousedown", closeOutside);
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("mousedown", closeOutside); document.removeEventListener("keydown", onKey); };
  }, [contextMenu]);

  React.useLayoutEffect(() => {
    if (!contextMenu) return;
    const updatePosition = () => {
      const menu = contextMenuRef.current;
      if (!menu) return;
      const rect = menu.getBoundingClientRect();
      setContextMenuStyle(calculateAnchoredOverlayPosition(
        { top: contextMenu.y, bottom: contextMenu.y, left: contextMenu.x, right: contextMenu.x, width: 0, height: 0 },
        { width: rect.width || menu.offsetWidth || 208, height: rect.height || menu.offsetHeight || menu.scrollHeight },
        { width: window.innerWidth, height: window.innerHeight },
        { align: "start", rtl: false }
      ));
    };
    updatePosition();
    window.addEventListener("resize", updatePosition);
    return () => window.removeEventListener("resize", updatePosition);
  }, [contextMenu]);

  const contextRow = contextMenu ? fullRows.find((r) => r.id === contextMenu.rowId) : undefined;
  const selectedFormat = selected?.textFormat;

  const cellKey = (rowId: string, column: PlanningGridColumnId) => `${rowId}::${column}`;
  const applyBulkCells = async (matrix: string[][], start: { rowId: string; column: PlanningGridColumnId }) => {
    if (!editable || saving || matrix.length === 0) return;
    const startRowIndex = filteredRows.findIndex((row) => row.id === start.rowId);
    const startColumnIndex = visibleColumns.indexOf(start.column);
    if (startRowIndex < 0 || startColumnIndex < 0) return;
    const updates = new Map<string, { activity: PlanActivityDto; patch: Record<string, unknown> }>();
    for (let rowOffset = 0; rowOffset < matrix.length; rowOffset += 1) {
      const row = filteredRows[startRowIndex + rowOffset];
      if (!row) break;
      for (let columnOffset = 0; columnOffset < (matrix[rowOffset]?.length ?? 0); columnOffset += 1) {
        const column = visibleColumns[startColumnIndex + columnOffset];
        const raw = matrix[rowOffset]?.[columnOffset] ?? "";
        if (!column || raw === "") continue;
        const activity = row.activity;
        if (!activity || !isEditable(row, column) || !["taskName", "duration", "progress", "start", "finish", "actualStart", "actualFinish", "constraintDate", "deadline"].includes(column)) {
          setError(t("planning.pasteInvalid")); return;
        }
        const entry = updates.get(activity.id) ?? { activity, patch: {} };
        if (column === "taskName") {
          if (!raw.trim()) { setError(t("planning.pasteInvalid")); return; }
          entry.patch.name = raw.trim();
        } else if (column === "duration") {
          const value = Number(raw);
          if (!Number.isInteger(value) || value < 0 || activity.type === "Milestone") { setError(t("planning.pasteInvalid")); return; }
          entry.patch.duration = value;
        } else if (column === "progress") {
          const value = Number(raw);
          if (!Number.isFinite(value) || value < 0 || value > 100) { setError(t("planning.pasteInvalid")); return; }
          entry.patch.progress = roundPlanProgress(value)!;
        } else {
          if (raw && !/^\d{4}-\d{2}-\d{2}$/.test(raw)) { setError(t("planning.pasteInvalid")); return; }
          const field = column === "start" ? "startDate" : column === "finish" ? "endDate" : column;
          entry.patch[field] = raw || null;
        }
        updates.set(activity.id, entry);
      }
    }
    if (!updates.size) return;
    if (updates.size > 500) { setError(t("planning.pasteInvalid")); return; }
    const items = [...updates.values()].map(({ activity, patch }) => ({ activityId: activity.id, update: {
      code: activity.code, name: patch.name ?? activity.name, description: activity.description ?? null,
      type: activity.type, schedulingMode: activity.schedulingMode ?? "Automatic", constraintType: activity.constraintType ?? "AsSoonAsPossible",
      constraintDate: ("constraintDate" in patch ? patch.constraintDate : activity.constraintDate) ?? null,
      deadline: ("deadline" in patch ? patch.deadline : activity.deadline) ?? null,
      startDate: ("startDate" in patch ? patch.startDate : activity.startDate) ?? null,
      endDate: ("endDate" in patch ? patch.endDate : activity.endDate) ?? null,
      actualStart: ("actualStart" in patch ? patch.actualStart : activity.actualStart) ?? null,
      actualFinish: ("actualFinish" in patch ? patch.actualFinish : activity.actualFinish) ?? null,
      calendarId: activity.calendarId ?? null,
      duration: activity.type === "Milestone" ? 0 : (patch.duration as number | undefined) ?? activity.duration,
      estimateHours: activity.estimateHours ?? null, progress: roundPlanProgress(("progress" in patch ? patch.progress : activity.progress) as number | null | undefined),
      wbsId: activity.wbsId ?? null, sortOrder: activity.sortOrder, rowVersion: activity.rowVersion,
    } }));
    setSaving(true); setError(null);
    try {
      await mutations.bulkUpdateActivities.mutateAsync({ items });
      notifySaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally { setSaving(false); }
  };

  const selectedCellBounds = () => {
    const selectedCells = [...selectedCellKeys].map((key) => {
      const separator = key.lastIndexOf("::");
      return separator < 0 ? null : { rowId: key.slice(0, separator), column: key.slice(separator + 2) as PlanningGridColumnId };
    }).filter((cell): cell is { rowId: string; column: PlanningGridColumnId } => cell !== null);
    if (!selectedCells.length && activeCell) return { minRow: activeCell.rowId, minColumn: activeCell.column, selectedCells: [activeCell] };
    const rowIndexes = selectedCells.map((cell) => filteredRows.findIndex((row) => row.id === cell.rowId)).filter((index) => index >= 0);
    const columnIndexes = selectedCells.map((cell) => visibleColumns.indexOf(cell.column)).filter((index) => index >= 0);
    if (!rowIndexes.length || !columnIndexes.length) return null;
    return { minRow: filteredRows[Math.min(...rowIndexes)]!.id, minColumn: visibleColumns[Math.min(...columnIndexes)]!, selectedCells };
  };

  const handleGridCopy = (event: React.ClipboardEvent<HTMLDivElement>) => {
    const bounds = selectedCellBounds();
    if (!bounds) return;
    const selected = new Set(bounds.selectedCells.map((cell) => cellKey(cell.rowId, cell.column)));
    const minRow = filteredRows.findIndex((row) => row.id === bounds.minRow);
    const minColumn = visibleColumns.indexOf(bounds.minColumn);
    const maxRow = Math.max(...bounds.selectedCells.map((cell) => filteredRows.findIndex((row) => row.id === cell.rowId)));
    const maxColumn = Math.max(...bounds.selectedCells.map((cell) => visibleColumns.indexOf(cell.column)));
    const text = filteredRows.slice(minRow, maxRow + 1).map((row) => visibleColumns.slice(minColumn, maxColumn + 1).map((column) => selected.has(cellKey(row.id, column)) ? cellText(row, column) : "").join("\t")).join("\n");
    event.preventDefault();
    event.clipboardData.setData("text/plain", text);
  };

  const handleGridPaste = (event: React.ClipboardEvent<HTMLDivElement>) => {
    if (!activeCell || !editable) return;
    const raw = event.clipboardData.getData("text/plain");
    if (!raw) return;
    event.preventDefault();
    void applyBulkCells(raw.replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n").map((line) => line.split("\t")), activeCell);
  };

  const fillSelectionDown = () => {
    const bounds = selectedCellBounds();
    if (!bounds) return;
    const rowIndexes = bounds.selectedCells.map((cell) => filteredRows.findIndex((row) => row.id === cell.rowId)).filter((index) => index >= 0);
    const columnIndexes = [...new Set(bounds.selectedCells.map((cell) => visibleColumns.indexOf(cell.column)).filter((index) => index >= 0))].sort((a, b) => a - b);
    if (rowIndexes.length < 2 || !columnIndexes.length) return;
    const firstRow = filteredRows[Math.min(...rowIndexes)];
    if (!firstRow) return;
    const minRowIndex = Math.min(...rowIndexes);
    const minColumnIndex = Math.min(...columnIndexes);
    const maxColumnIndex = Math.max(...columnIndexes);
    const selected = new Set(bounds.selectedCells.map((cell) => cellKey(cell.rowId, cell.column)));
    const firstValues = visibleColumns.slice(minColumnIndex, maxColumnIndex + 1).map((column) => selected.has(cellKey(firstRow.id, column)) ? cellText(firstRow, column) : "");
    const matrix = Array.from({ length: Math.max(...rowIndexes) - minRowIndex + 1 }, (_, rowIndex) => rowIndex === 0 ? firstValues : [...firstValues]);
    void applyBulkCells(matrix, { rowId: firstRow.id, column: visibleColumns[minColumnIndex]! });
  };

  const selectWholeColumn = (column: PlanningGridColumnId, additive = false) => {
    if (!filteredRows.length) return;
    const cells = filteredRows.map((row) => cellKey(row.id, column));
    const rowIds = filteredRows.map((row) => row.id);
    setSelectedCellKeys((current) => new Set(additive ? [...current, ...cells] : cells));
    setSelectedRowIds((current) => new Set(additive ? [...current, ...rowIds] : rowIds));
    const firstRowId = filteredRows[0]!.id;
    setCellSelectionAnchor({ rowId: firstRowId, column });
    setRowSelectionAnchorId(firstRowId);
    setActiveCell({ rowId: firstRowId, column });
    applySelectedId(firstRowId);
  };

  const selectWholeRow = (rowId: string) => {
    if (!visibleColumns.length) return;
    setSelectedCellKeys(new Set(visibleColumns.map((column) => cellKey(rowId, column))));
    setSelectedRowIds(new Set([rowId]));
    setCellSelectionAnchor({ rowId, column: visibleColumns[0]! });
    setRowSelectionAnchorId(rowId);
    setActiveCell({ rowId, column: visibleColumns[0]! });
    applySelectedId(rowId);
  };

  const selectCellRange = (anchor: { rowId: string; column: PlanningGridColumnId }, focus: { rowId: string; column: PlanningGridColumnId }, additive: boolean) => {
    const anchorRowIndex = filteredRows.findIndex((candidate) => candidate.id === anchor.rowId);
    const focusRowIndex = filteredRows.findIndex((candidate) => candidate.id === focus.rowId);
    const anchorColumnIndex = visibleColumns.indexOf(anchor.column);
    const focusColumnIndex = visibleColumns.indexOf(focus.column);
    if (anchorRowIndex < 0 || focusRowIndex < 0 || anchorColumnIndex < 0 || focusColumnIndex < 0) return;
    const next = new Set(additive ? selectedCellKeys : []);
    for (let r = Math.min(anchorRowIndex, focusRowIndex); r <= Math.max(anchorRowIndex, focusRowIndex); r += 1) {
      for (let c = Math.min(anchorColumnIndex, focusColumnIndex); c <= Math.max(anchorColumnIndex, focusColumnIndex); c += 1) {
        const candidateRow = filteredRows[r];
        const candidateColumn = visibleColumns[c];
        if (candidateRow && candidateColumn) next.add(cellKey(candidateRow.id, candidateColumn));
      }
    }
    setSelectedCellKeys(next);
    const rangeRows = filteredRows.slice(Math.min(anchorRowIndex, focusRowIndex), Math.max(anchorRowIndex, focusRowIndex) + 1).map((candidate) => candidate.id);
    setSelectedRowIds((current) => new Set(additive ? [...current, ...rangeRows] : rangeRows));
    setCellSelectionAnchor(anchor);
    setRowSelectionAnchorId(anchor.rowId);
  };
  const selectRow = (row: PlanWorkspaceRow, event: React.MouseEvent) => {
    const additive = event.ctrlKey || event.metaKey;
    if (event.shiftKey) {
      const anchorIndex = filteredRows.findIndex((candidate) => candidate.id === rowSelectionAnchorId);
      const focusIndex = filteredRows.findIndex((candidate) => candidate.id === row.id);
      if (anchorIndex >= 0 && focusIndex >= 0) {
        const start = Math.min(anchorIndex, focusIndex);
        const end = Math.max(anchorIndex, focusIndex);
        const rangeIds = filteredRows.slice(start, end + 1).map((candidate) => candidate.id);
        setSelectedRowIds((current) => new Set(additive ? [...current, ...rangeIds] : rangeIds));
      } else setSelectedRowIds(new Set([row.id]));
    } else if (additive) {
      setSelectedRowIds((current) => { const next = new Set(current); if (next.has(row.id)) next.delete(row.id); else next.add(row.id); return next; });
      setRowSelectionAnchorId(row.id);
    } else {
      setSelectedRowIds(new Set([row.id]));
      setRowSelectionAnchorId(row.id);
      setSelectedCellKeys(new Set());
      setCellSelectionAnchor(null);
    }
    applySelectedId(row.id);
  };
  const selectCell = (row: PlanWorkspaceRow, column: PlanningGridColumnId, event: React.MouseEvent) => {
    const rowIndex = filteredRows.findIndex((candidate) => candidate.id === row.id);
    const columnIndex = visibleColumns.indexOf(column);
    const additive = event.ctrlKey || event.metaKey;
    if (event.shiftKey && rowIndex >= 0 && columnIndex >= 0) {
      const anchor = cellSelectionAnchor ?? { rowId: row.id, column };
      selectCellRange(anchor, { rowId: row.id, column }, additive);
    } else if (additive) {
      setSelectedCellKeys((current) => { const next = new Set(current); const key = cellKey(row.id, column); if (next.has(key)) next.delete(key); else next.add(key); return next; });
      setSelectedRowIds((current) => { const next = new Set(current); if (next.has(row.id)) next.delete(row.id); else next.add(row.id); return next; });
      setCellSelectionAnchor({ rowId: row.id, column });
      setRowSelectionAnchorId(row.id);
    } else {
      setSelectedCellKeys(new Set([cellKey(row.id, column)]));
      setSelectedRowIds(new Set([row.id]));
      setCellSelectionAnchor({ rowId: row.id, column });
      setRowSelectionAnchorId(row.id);
    }
    setActiveCell({ rowId: row.id, column });
    applySelectedId(row.id);
  };
  const activateCell = (row: PlanWorkspaceRow, column: PlanningGridColumnId, event: React.MouseEvent) => {
    draggingCells.current = true;
    selectCell(row, column, event);
  };
  const extendCellSelection = (row: PlanWorkspaceRow, column: PlanningGridColumnId) => {
    if (!draggingCells.current) return;
    const focus = { rowId: row.id, column };
    selectCellRange(cellSelectionAnchor ?? activeCell ?? focus, focus, false);
    setActiveCell(focus);
    applySelectedId(row.id);
  };
  const isCellSelected = (row: PlanWorkspaceRow, column: PlanningGridColumnId) => selectedCellKeys.has(cellKey(row.id, column));
  const activeCellClass = (row: PlanWorkspaceRow, column: PlanningGridColumnId) => (activeCell?.rowId === row.id && activeCell.column === column ? "ring-1 ring-inset ring-primary/50" : undefined);

  const beginEditActiveCell = (initialDraft?: string): boolean => {
    if (!editable || editing || moving || saving) return false;
    const row = selected && filteredRows.find((r) => r.id === selected.id);
    if (!row) return false;
    const preferred = activeCell?.rowId === row.id ? activeCell.column : "taskName";
    const column = isEditable(row, preferred) && visibleColumns.includes(preferred)
      ? preferred
      : isEditable(row, "taskName")
        ? "taskName"
        : visibleColumns.find((c) => isEditable(row, c));
    if (!column) return false;
    setEditing({ rowId: row.id, column, draft: initialDraft && !DATE_COLUMNS.includes(column) ? initialDraft : cellText(row, column) });
    applySelectedId(row.id);
    return true;
  };

  const findEditableCell = (row: PlanWorkspaceRow, column: PlanningGridColumnId, direction: "next" | "previous" | "up" | "down") => {
    const rowIndex = filteredRows.findIndex((item) => item.id === row.id);
    const columnIndex = visibleColumns.indexOf(column);
    if (rowIndex < 0 || columnIndex < 0) return undefined;
    if (direction === "up" || direction === "down") {
      const step = direction === "up" ? -1 : 1;
      for (let index = rowIndex + step; index >= 0 && index < filteredRows.length; index += step) {
        if (isEditable(filteredRows[index], column)) return { row: filteredRows[index], column };
      }
      return undefined;
    }
    const step = direction === "next" ? 1 : -1;
    const cellCount = filteredRows.length * visibleColumns.length;
    let index = rowIndex * visibleColumns.length + columnIndex + step;
    while (index >= 0 && index < cellCount) {
      const candidateRow = filteredRows[Math.floor(index / visibleColumns.length)];
      const candidateColumn = visibleColumns[index % visibleColumns.length];
      if (isEditable(candidateRow, candidateColumn)) return { row: candidateRow, column: candidateColumn };
      index += step;
    }
    return undefined;
  };

  const onCellKey = (row: PlanWorkspaceRow, column: PlanningGridColumnId, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Escape") { e.preventDefault(); setEditing(null); return; }
    let direction: "next" | "previous" | "up" | "down" | undefined;
    if (e.key === "Enter") direction = "down";
    else if (e.key === "Tab") direction = e.shiftKey ? "previous" : "next";
    else if (e.key === "ArrowUp") direction = "up";
    else if (e.key === "ArrowDown") direction = "down";
    else if (e.currentTarget.type === "text" && e.key === "ArrowLeft" && e.currentTarget.selectionStart === 0 && e.currentTarget.selectionEnd === 0) direction = "previous";
    else if (e.currentTarget.type === "text" && e.key === "ArrowRight" && e.currentTarget.selectionStart === e.currentTarget.value.length && e.currentTarget.selectionEnd === e.currentTarget.value.length) direction = "next";
    if (!direction) return;
    e.preventDefault();
    const next = findEditableCell(row, column, direction);
    void commitCell(row, column, editing?.draft ?? e.currentTarget.value).then(() => {
      if (next) {
        setActiveCell({ rowId: next.row.id, column: next.column });
        setEditing({ rowId: next.row.id, column: next.column, draft: cellText(next.row, next.column) });
      }
    });
  };

  const onGridKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    const target = event.target as HTMLElement;
    if (editing || target instanceof HTMLInputElement || target instanceof HTMLSelectElement || target instanceof HTMLTextAreaElement || target.isContentEditable) return;
    const mod = event.ctrlKey || event.metaKey;
    const inButton = Boolean(target.closest("button") && !target.closest("[data-planning-column-select]"));
    if (!inButton && mod && !event.shiftKey && event.code === "Space" && activeCell) {
      event.preventDefault();
      selectWholeColumn(activeCell.column);
      return;
    }
    if (!inButton && !mod && event.shiftKey && event.code === "Space" && activeCell) {
      event.preventDefault();
      selectWholeRow(activeCell.rowId);
      return;
    }
    if (editable && !inButton && mod && !event.shiftKey && event.key.toLowerCase() === "d") {
      event.preventDefault();
      fillSelectionDown();
      return;
    }
    if (editable && selected && !inButton && mod && !event.shiftKey && (event.key === "-" || event.code === "NumpadSubtract")) {
      event.preventDefault();
      void removeRow(selected);
      return;
    }
    if (editable && !inButton && event.key === "Delete" && activeCell && visibleColumns.includes(activeCell.column) && CLEARABLE_PLANNING_GRID_COLUMNS.includes(activeCell.column)) {
      const row = fullRows.find((item) => item.id === activeCell.rowId);
      if (row && isEditable(row, activeCell.column)) {
        event.preventDefault();
        void commitCell(row, activeCell.column, "");
        return;
      }
    }
    if (mod && event.shiftKey && (event.key === "ArrowRight" || event.key === "ArrowLeft")) {
      if (!editable || !selected || moving) return;
      event.preventDefault();
      if (event.key === "ArrowRight") { if (canIndentSelected) void indent(); }
      else if (canOutdentSelected) void outdent();
      return;
    }
    if (!mod && !event.altKey && !(target instanceof HTMLButtonElement)) {
      if (event.key === "Enter" || event.key === "F2") {
        if (beginEditActiveCell()) event.preventDefault();
        return;
      }
      if (event.key.length === 1 && event.key !== " ") {
        if (beginEditActiveCell(event.key)) event.preventDefault();
        return;
      }
    }
    if (!mod && !event.altKey && ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key) && filteredRows.length > 0 && visibleColumns.length > 0) {
      const current = activeCell ?? { rowId: selectedId || filteredRows[0].id, column: "taskName" as PlanningGridColumnId };
      let rowIndex = filteredRows.findIndex((row) => row.id === current.rowId);
      let columnIndex = visibleColumns.indexOf(current.column);
      if (rowIndex < 0) rowIndex = 0;
      if (columnIndex < 0) columnIndex = Math.max(0, visibleColumns.indexOf("taskName"));
      const rtl = (event.currentTarget.closest("[dir]") as HTMLElement | null)?.getAttribute("dir") === "rtl" || getComputedStyle(event.currentTarget).direction === "rtl";
      if (event.key === "ArrowUp") rowIndex -= 1;
      else if (event.key === "ArrowDown") rowIndex += 1;
      else if ((event.key === "ArrowLeft") !== rtl) columnIndex -= 1;
      else columnIndex += 1;
      const nextRow = filteredRows[rowIndex];
      const nextColumn = visibleColumns[columnIndex];
      if (nextRow && nextColumn) {
        event.preventDefault();
        const focus = { rowId: nextRow.id, column: nextColumn };
        if (event.shiftKey) selectCellRange(cellSelectionAnchor ?? current, focus, false);
        else {
          setSelectedCellKeys(new Set([cellKey(focus.rowId, focus.column)]));
          setSelectedRowIds(new Set([focus.rowId]));
          setCellSelectionAnchor(focus);
          setRowSelectionAnchorId(focus.rowId);
        }
        setActiveCell(focus);
        applySelectedId(nextRow.id);
      }
    }
  };

  const eligibleBulkRows = selectedLeafRows();
  const formatRibbonGroup = <PlanningRibbonGroup label={t("planning.ribbon.format")}>
    <Button size="icon-sm" variant={selectedFormat?.isBold ? "secondary" : "ghost"} disabled={!editable || !selected || moving} onClick={() => toggleFormat("isBold")} aria-label={t("planning.formatBold")} aria-pressed={Boolean(selectedFormat?.isBold)}><Bold className="h-3.5 w-3.5" /></Button>
    <Button size="icon-sm" variant={selectedFormat?.isItalic ? "secondary" : "ghost"} disabled={!editable || !selected || moving} onClick={() => toggleFormat("isItalic")} aria-label={t("planning.formatItalic")} aria-pressed={Boolean(selectedFormat?.isItalic)}><Italic className="h-3.5 w-3.5" /></Button>
    <Button size="icon-sm" variant={selectedFormat?.isUnderline ? "secondary" : "ghost"} disabled={!editable || !selected || moving} onClick={() => toggleFormat("isUnderline")} aria-label={t("planning.formatUnderline")} aria-pressed={Boolean(selectedFormat?.isUnderline)}><Underline className="h-3.5 w-3.5" /></Button>
    <label className="sr-only" htmlFor="planning-text-color">{t("planning.formatTextColor")}</label>
    <select id="planning-text-color" value={String(selectedFormat?.textColorToken ?? "")} disabled={!editable || !selected || moving} onChange={(e) => { const v = e.target.value; void updateTextFormat(selectedFormat ? { ...selectedFormat, textColorToken: v || undefined } : { isBold: false, isItalic: false, isUnderline: false, textColorToken: v || undefined }); }} className="h-7 max-w-24 rounded border bg-background px-1.5 text-[11px]" aria-label={t("planning.formatTextColor")}>
      <option value="">{t("planning.formatNone")}</option>
      {PLAN_TEXT_COLOR_TOKENS.map((token) => <option key={token} value={token}>{t(`planning.formatColors.${token}`)}</option>)}
    </select>
    <label className="sr-only" htmlFor="planning-highlight-color">{t("planning.formatHighlight")}</label>
    <select id="planning-highlight-color" value={String(selectedFormat?.highlightColorToken ?? "")} disabled={!editable || !selected || moving} onChange={(e) => { const v = e.target.value; void updateTextFormat(selectedFormat ? { ...selectedFormat, highlightColorToken: v || undefined } : { isBold: false, isItalic: false, isUnderline: false, highlightColorToken: v || undefined }); }} className="h-7 max-w-24 rounded border bg-background px-1.5 text-[11px]" aria-label={t("planning.formatHighlight")}>
      <option value="">{t("planning.formatNone")}</option>
      {PLAN_HIGHLIGHT_COLOR_TOKENS.map((token) => <option key={token} value={token}>{t(`planning.formatHighlights.${token}`)}</option>)}
    </select>
    <label className="sr-only" htmlFor="planning-font-size">{t("planning.formatFontSize")}</label>
    <select id="planning-font-size" value={String(selectedFormat?.fontSizeToken ?? "")} disabled={!editable || !selected || moving} onChange={(e) => { const v = e.target.value; void updateTextFormat(selectedFormat ? { ...selectedFormat, fontSizeToken: v || undefined } : { isBold: false, isItalic: false, isUnderline: false, fontSizeToken: v || undefined }); }} className="h-7 max-w-24 rounded border bg-background px-1.5 text-[11px]" aria-label={t("planning.formatFontSize")}>
      <option value="">{t("planning.formatNone")}</option>
      {PLAN_FONT_SIZE_TOKENS.map((token) => <option key={token} value={token}>{t(`planning.formatSizes.${token}`)}</option>)}
    </select>
    <Button size="icon-sm" variant="ghost" disabled={!editable || !selected || !selectedFormat || moving} onClick={() => void updateTextFormat(null)} aria-label={t("planning.clearFormatting")}><Eraser className="h-3.5 w-3.5" /></Button>
  </PlanningRibbonGroup>;
  return <div tabIndex={0} onCopy={handleGridCopy} onPaste={handleGridPaste} onMouseDown={(event) => { const target = event.target; if (target instanceof HTMLElement && !target.closest("button, input, select, textarea, [contenteditable='true']")) event.currentTarget.focus({ preventScroll: true }); }} onKeyDown={onGridKeyDown}>
      {ribbonExpanded && (ribbonTab === "task" || (ribbonTab === "resource" && Boolean(onOpenAssignments))) ? <div className="flex min-w-max items-stretch gap-1 overflow-x-auto border-b bg-muted/20 px-1.5 py-1 text-[12px]">
      {ribbonTab === "task" ? <>
        <PlanningRibbonGroup label={t("planning.ribbon.task")}>
          <PlanningIconButton label={t("planning.addTask")} icon={<Plus className="h-3.5 w-3.5" />} variant="secondary" disabled={!editable || creating} onClick={() => void createRow("Task", "append")} />
          <PlanningIconButton label={t("planning.addSubtask")} icon={<Plus className="h-3.5 w-3.5" />} disabled={!editable || !selected || !rowWbsId(selected) || creating} onClick={() => void createRow("Task", "child")} />
          <PlanningIconButton label={t("planning.addMilestone")} icon={<Diamond className="h-3.5 w-3.5" />} disabled={!editable || creating} onClick={() => void createRow("Milestone", "append")} />
          <PlanningIconButton label={t("planning.insertAbove")} icon={<Rows3 className="h-3.5 w-3.5" />} disabled={!editable || !selected || creating} onClick={() => void createRow("Task", "above")} />
          <PlanningIconButton label={t("planning.insertBelow")} icon={<Rows3 className="h-3.5 w-3.5 rotate-180" />} disabled={!editable || !selected || creating} onClick={() => void createRow("Task", "below")} />
          <PlanningIconButton label={t("common.delete")} icon={<Trash2 className="h-3.5 w-3.5 text-destructive" />} disabled={!editable || !selected || moving} onClick={() => void removeRow()} />
        </PlanningRibbonGroup>
        <PlanningRibbonGroup label={t("planning.ribbon.scheduling")}>
          <PlanningIconButton label={t("planning.manualSelected")} icon={<Hand className="h-3.5 w-3.5" />} disabled={!editable || !eligibleBulkRows.length || eligibleBulkRows.every((row) => (row.activity?.schedulingMode ?? "Automatic") === "Manual") || saving} onClick={() => void updateSelectedActivities({ schedulingMode: "Manual" })} />
          <PlanningIconButton label={t("planning.automaticSelected")} icon={<Bot className="h-3.5 w-3.5" />} disabled={!editable || !eligibleBulkRows.length || eligibleBulkRows.every((row) => (row.activity?.schedulingMode ?? "Automatic") === "Automatic") || saving} onClick={() => void updateSelectedActivities({ schedulingMode: "Automatic" })} />
          <div title={t("planning.bulkProgress")} className="w-10 shrink-0">
            <Select value={bulkProgress} onValueChange={setBulkProgress}>
              <SelectTrigger aria-label={t("planning.bulkProgress")} disabled={!editable || !eligibleBulkRows.length || saving} className="h-7 w-10 gap-0 px-1.5 py-0">
                <Percent aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
                <span className="sr-only"><SelectValue /></span>
              </SelectTrigger>
              <SelectContent>
                {[0, 25, 50, 75, 100].map((value) => <SelectItem key={value} value={String(value)}>{t("planning.progressValue").replace("{{value}}", String(value))}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <PlanningIconButton label={t("planning.applyProgress")} icon={<Check className="h-3.5 w-3.5" />} disabled={!editable || !eligibleBulkRows.length || saving} onClick={() => void updateSelectedActivities({ progress: Number(bulkProgress) })} />
        </PlanningRibbonGroup>
        <PlanningRibbonGroup label={t("planning.ribbon.hierarchy")}>
          <PlanningIconButton label={t("planning.indent")} icon={<IndentIncrease className="h-3.5 w-3.5" />} disabled={!editable || !canIndentSelected || moving} onClick={() => void indent()} />
          <PlanningIconButton label={t("planning.outdent")} icon={<IndentDecrease className="h-3.5 w-3.5" />} disabled={!editable || !canOutdentSelected || moving} onClick={() => void outdent()} />
          <PlanningIconButton label={t("planning.moveUp")} icon={<ChevronUp className="h-3.5 w-3.5" />} disabled={!editable || !canMoveSelectedUp || moving} onClick={() => void moveSibling("up")} />
          <PlanningIconButton label={t("planning.moveDown")} icon={<ChevronDown className="h-3.5 w-3.5" />} disabled={!editable || !canMoveSelectedDown || moving} onClick={() => void moveSibling("down")} />
          <PlanningIconButton label={t(selectedIsCollapsed ? "planning.expand" : "planning.collapse")} icon={selectedIsCollapsed ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />} disabled={!canToggleSelectedCollapse || moving} pressed={selectedIsCollapsed} onClick={toggleSelectedCollapse} />
          <PlanningIconButton label={t("planning.collapseAll")} icon={<ChevronsDownUp className="h-3.5 w-3.5" />} disabled={!canCollapseAll || moving} onClick={collapseAll} />
          <PlanningIconButton label={t("planning.expandAll")} icon={<ChevronsUpDown className="h-3.5 w-3.5" />} disabled={!canExpandAll || moving} onClick={expandAll} />
        </PlanningRibbonGroup>
        <PlanningRibbonGroup label={t("planning.ribbon.links")}>
          <PlanningIconButton label={t("planning.linkSelected")} icon={<Link2 className="h-3.5 w-3.5" />} disabled={!editable || selectedLeafActivities().length < 2 || saving} onClick={() => void linkSelectedActivities()} />
          <PlanningIconButton label={t("planning.unlinkSelected")} icon={<Link2 className="h-3.5 w-3.5 rotate-45" />} disabled={!editable || selectedLeafActivities().length < 2 || saving || !selectedLeafActivities().some((activity) => (depsByActivity.get(activity.id) ?? []).some((dependency) => selectedRowIds.has(`activity:${dependency.predecessorActivityId}`)))} onClick={() => void unlinkSelectedActivities()} />
          <PlanningIconButton label={t("planning.manageLinks")} icon={<Link2 className="h-3.5 w-3.5" />} disabled={!selected?.activity} onClick={() => { if (selected?.activity) onEdit(selected.activity); }} />
        </PlanningRibbonGroup>
        {formatRibbonGroup}
      </> : null}
      {ribbonTab === "resource" && onOpenAssignments ? <PlanningRibbonGroup label={t("planning.ribbon.resource")}>
        <PlanningIconButton label={t("planning.resourceAssignments")} icon={<Users className="h-3.5 w-3.5" />} onClick={onOpenAssignments} />
      </PlanningRibbonGroup> : null}
    </div> : null}
    <div className="space-y-3 px-4 pb-4 pt-3">
    <div className="flex flex-wrap items-center gap-2">
      {editable ? <div className="flex min-w-0 flex-1 items-center gap-2"><Input value={quickName} onChange={(e) => setQuickName(e.target.value)} placeholder={t("planning.quickAddPlaceholder")} className="h-8 max-w-xs" onKeyDown={(e) => { if (e.key === "Enter") void quickAdd("Task"); }} /><Button size="sm" onClick={() => void quickAdd("Task")} disabled={saving || creating || !quickName.trim()}><Plus className="me-1 h-3.5 w-3.5" />{t("planning.addTask")}</Button></div> : null}
      <div className="ms-auto flex items-center gap-1">
        <Button size="sm" variant="ghost" onClick={() => setColumnManagerOpen((v) => !v)} aria-label={t("planning.columnManager")}><Columns3 className="h-4 w-4" /></Button>
        <Button size="sm" variant={activeColumnFilterCount ? "secondary" : "ghost"} onClick={() => setColumnFiltersOpen((open) => !open)} aria-label={t("planning.filter")} aria-expanded={columnFiltersOpen}><Filter className="me-1 h-4 w-4" />{activeColumnFilterCount || ""}</Button>
        {activeColumnFilterCount ? <Button size="sm" variant="ghost" onClick={() => { markPreferencesDirty(); setColumnFilters({}); }} aria-label={t("planning.clearAllFilters")} title={t("planning.clearAllFilters")}><X className="h-4 w-4" /></Button> : null}
        {editable ? <Button size="sm" variant="ghost" onClick={resetTableLayout} disabled={resetTablePreferences.isPending} aria-label={t("planning.resetTableLayout")} title={t("planning.resetTableLayout")}><RotateCcw className="h-4 w-4" /></Button> : null}
      </div>
    </div>
    {error ? <p role="alert" className="rounded border border-destructive/30 px-3 py-2 text-xs text-destructive">{error}</p> : null}
    {columnFiltersOpen ? <div className="grid gap-3 rounded-lg border bg-muted/10 p-3 sm:grid-cols-2 xl:grid-cols-3">{visibleColumns.map((column) => {
      const kind = getPlanningGridFilterKind(column);
      const filter = columnFilters[column] ?? {};
      const label = t(ALL_COLUMNS.find((entry) => entry.id === column)?.labelKey ?? column);
      return <div key={column} className="min-w-0 space-y-1"><label className="text-xs font-medium">{label}</label>{kind === "enum" ? <select className="input-field h-8 w-full text-xs" aria-label={label} value={filter.value ?? ""} onChange={(event) => setColumnFilter(column, { ...filter, value: event.target.value || undefined })}><option value="">{t("planning.filterValue")}</option>{(ENUM_FILTER_VALUES[column] ?? []).map((value) => <option key={value} value={value}>{filterOptionLabel(column, value)}</option>)}</select> : kind === "text" ? <Input className="h-8 text-xs" aria-label={label} placeholder={t("planning.filterContains")} value={filter.text ?? ""} onChange={(event) => setColumnFilter(column, { ...filter, text: event.target.value || undefined })} /> : <div className="grid grid-cols-2 gap-2"><Input className="h-8 text-xs" aria-label={`${label} ${t(kind === "date" ? "planning.filterFrom" : "planning.filterMin")}`} placeholder={t(kind === "date" ? "planning.filterFrom" : "planning.filterMin")} type={kind === "date" ? "date" : "number"} value={filter.min ?? ""} onChange={(event) => setColumnFilter(column, { ...filter, min: event.target.value || undefined })} /><Input className="h-8 text-xs" aria-label={`${label} ${t(kind === "date" ? "planning.filterTo" : "planning.filterMax")}`} placeholder={t(kind === "date" ? "planning.filterTo" : "planning.filterMax")} type={kind === "date" ? "date" : "number"} value={filter.max ?? ""} onChange={(event) => setColumnFilter(column, { ...filter, max: event.target.value || undefined })} /></div>}</div>;
    })}</div> : null}
    {columnManagerOpen ? <div className="flex flex-wrap gap-1.5 rounded-lg border p-3">{ALL_COLUMNS.map((c) => { const active = visibleColumns.includes(c.id); const required = REQUIRED_PLANNING_GRID_COLUMNS.includes(c.id); return <button key={c.id} type="button" aria-pressed={active} disabled={!editable || required || (showBaseline && BASELINE_COLUMNS.includes(c.id) && active)} onClick={() => toggleColumn(c.id)} className={cn("rounded-full border px-2.5 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-60", active ? "border-primary bg-primary/10 text-primary" : "border-input text-muted-foreground")}>{t(c.labelKey)}</button>; })}</div> : null}
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[960px] table-fixed text-sm">
        <colgroup>{visibleColumns.map((column) => <col key={column} style={{ width: columnWidths[column] ?? COLUMN_WIDTHS[column].initial }} />)}<col style={{ width: 64 }} /></colgroup>
        <thead><tr className="border-b bg-muted/60 text-start text-xs text-muted-foreground">
          {visibleColumns.map((c) => <th key={c} draggable={editable && !REQUIRED_PLANNING_GRID_COLUMNS.includes(c)} onDragStart={(event) => { if (REQUIRED_PLANNING_GRID_COLUMNS.includes(c)) { event.preventDefault(); return; } setDraggedColumn(c); event.dataTransfer.effectAllowed = "move"; }} onDragOver={(event) => { if (draggedColumn) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); if (editable && draggedColumn) { markPreferencesDirty(); setColumns((current) => reorderPlanningGridColumn(current, draggedColumn, c)); } setDraggedColumn(null); }} onDragEnd={() => setDraggedColumn(null)} className={cn("relative px-2 py-2.5 text-start font-semibold", editable && !REQUIRED_PLANNING_GRID_COLUMNS.includes(c) && "cursor-grab", draggedColumn === c && "opacity-50")} title={t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}>
            <button type="button" data-planning-column-select="true" className="block w-full truncate pe-2 text-start font-semibold" aria-label={`${t("planning.selectColumn")}: ${t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}`} aria-pressed={filteredRows.length > 0 && filteredRows.every((row) => selectedCellKeys.has(cellKey(row.id, c)))} onClick={(event) => { event.stopPropagation(); selectWholeColumn(c, event.ctrlKey || event.metaKey); }}>{t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}</button>
            {editable && !REQUIRED_PLANNING_GRID_COLUMNS.includes(c) ? <GripVertical className="absolute start-0 top-1/2 h-3 w-3 -translate-y-1/2 text-muted-foreground/60" aria-hidden="true" /> : null}
            {editable ? <span role="separator" aria-label={`${t("planning.resizeColumn")}: ${t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}`} aria-orientation="vertical" onPointerDown={(event) => beginColumnResize(c, event)} onPointerMove={moveColumnResize} onPointerUp={endColumnResize} onPointerCancel={endColumnResize} className="absolute inset-y-1 end-0 z-10 w-2 cursor-col-resize touch-none rounded-sm hover:bg-primary/30" /> : null}
          </th>)}
          <th className="w-16 px-2 py-2.5" />
        </tr></thead>
        <tbody>
          {filteredRows.map((row) => {
            const isMilestone = row.activity?.type === PLANNING_ACTIVITY_TYPE.milestone;
            const rowKind = isMilestone ? "milestone" : row.isSummary ? "summary" : "task";
            return (
      <tr key={row.id} aria-selected={selectedRowIds.has(row.id) || (selectedRowIds.size === 0 && selectedId === row.id)} data-testid="planning-grid-row" data-row-kind={rowKind} onClick={(event) => { const target = event.target; const clickedCellAlreadySelected = target instanceof HTMLElement && target.closest("td") && activeCell?.rowId === row.id && selectedCellKeys.has(cellKey(row.id, activeCell.column)); if (target === event.currentTarget || !clickedCellAlreadySelected) selectRow(row, event); }} onDoubleClick={() => { if (row.activity) onEdit(row.activity); }} onContextMenu={(e) => openContextMenu(row, e)} onDragOver={(event) => handleRowDragOver(event, row)} onDrop={(event) => handleRowDrop(event, row)} style={{ height: PLAN_ROW_HEIGHT }} className={cn("border-b transition-colors last:border-0 hover:bg-muted/40", (selectedRowIds.has(row.id) || (selectedRowIds.size === 0 && selectedId === row.id)) && "bg-primary/10", row.activity?.critical && "bg-destructive/[0.03]", row.isSummary && !selectedRowIds.has(row.id) && (selectedRowIds.size > 0 || selectedId !== row.id) && "bg-muted/25", planTextFormatClassName(row.textFormat), draggedActivityId === row.activity?.id && "opacity-50", dropTarget?.targetId === row.activity?.id && "outline outline-2 outline-primary")}>
              {visibleColumns.map((column) => {
                const isEditing = editing?.rowId === row.id && editing.column === column;
                if (column === "progress" && row.activity && !isEditing) {
                  const progress = roundPlanProgress(row.activity.progress) ?? 0;
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("min-w-28 px-2 py-1.5", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)} onDoubleClick={(event) => { event.stopPropagation(); if (isEditable(row, column)) setEditing({ rowId: row.id, column, draft: cellText(row, column) }); }}><span className="flex items-center gap-2"><Progress value={progress} className="h-1.5 w-12" /><span className="text-xs tabular-nums">{progress}%</span></span></td>;
                }
                if (column === "schedulingMode" && row.activity && !isEditing) {
                  const mode = row.activity.schedulingMode ?? "Automatic";
                  const modeLabel = t(mode === "Manual" ? "planning.manual" : "planning.automatic");
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("px-2 py-1.5 text-center", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)}><button type="button" disabled={!editable || saving} aria-label={`${t("planning.schedulingMode")}: ${modeLabel}`} title={modeLabel} aria-pressed={mode === "Automatic"} onClick={(event) => { event.stopPropagation(); applySelectedId(row.id); void commitCell(row, "schedulingMode", mode === "Manual" ? "Automatic" : "Manual"); }} onDoubleClick={(event) => event.stopPropagation()} className="flex items-center justify-center rounded border border-transparent p-1 hover:border-input hover:bg-background disabled:opacity-100">{mode === "Manual" ? <Hand className="h-3.5 w-3.5 text-warning" aria-hidden="true" /> : <Bot className="h-3.5 w-3.5 text-primary" aria-hidden="true" />}</button></td>;
                }
                if (column === "constraintType" && row.activity && !isEditing) {
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("px-1 py-1", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)}><select className="h-7 w-full rounded border bg-background px-1 text-xs" disabled={!editable || saving} aria-label={t("planning.columns.constraintType")} value={row.activity.constraintType ?? "AsSoonAsPossible"} onClick={(event) => event.stopPropagation()} onFocus={() => applySelectedId(row.id)} onDoubleClick={(event) => event.stopPropagation()} onChange={(event) => void commitCell(row, "constraintType", event.target.value)}>{PLAN_CONSTRAINT_TYPES.map((value) => <option key={value} value={value}>{t(`planning.constraintTypes.${value}`)}</option>)}</select></td>;
                }
                if (["schedulingStatus", "constraintType", "critical", "type"].includes(column) && !isEditing) {
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("px-2 py-1.5 text-xs text-muted-foreground", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)} title={displayCellText(row, column)}>{displayCellText(row, column) || "—"}</td>;
                }
                if (isEditing) {
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("px-1 py-1", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onDoubleClick={(event) => event.stopPropagation()}><Input autoFocus disabled={pastingTasks} value={editing.draft} onChange={(e) => setEditing({ ...editing, draft: e.target.value })} onFocus={(e) => { if (editing.draft === cellText(row, column)) e.currentTarget.select(); }} onBlur={() => void commitCell(row, column, editing.draft)} onPaste={(event) => { if (column !== "taskName") return; const pasted = event.clipboardData.getData("text"); const names = pasted.split(/\r?\n/).map((name) => name.trim()).filter(Boolean); if (names.length > 1) { event.preventDefault(); void createPastedTasks(names); } }} onKeyDown={(e) => onCellKey(row, column, e)} className="h-7 text-xs" min={column === "progress" ? 0 : undefined} max={column === "progress" ? 100 : undefined} step={column === "progress" ? 1 : undefined} type={column === "start" || column === "finish" || column === "actualStart" || column === "actualFinish" || column === "constraintDate" || column === "deadline" ? "date" : column === "duration" || column === "progress" ? "number" : "text"} /></td>;
                }
                if (column === "wbs") {
                  const rawCode = row.wbs?.code ?? row.activity?.code;
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("whitespace-nowrap px-2 py-1.5 text-xs tabular-nums text-muted-foreground", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)} title={row.outlineNumber && rawCode ? rawCode : undefined}>{displayCellText(row, column) || "—"}</td>;
                }
                if (column === "taskName") {
                  return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("min-w-40 px-2 py-1.5", row.isSummary ? "font-semibold" : "font-medium", isCellSelected(row, column) && "bg-primary/10", planTextFormatClassName(row.textFormat), activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)} style={{ paddingInlineStart: 6 + row.depth * 14 }} onDoubleClick={(event) => { event.stopPropagation(); if (isEditable(row, column)) setEditing({ rowId: row.id, column, draft: cellText(row, column) }); }} title={editable && isEditable(row, column) ? t("planning.doubleClickToEdit") : undefined}>
                    <span className="flex items-center gap-1">
                      {row.hasChildren ? (() => {
                        const childWbsId = row.wbs?.id ?? row.activity?.wbsId;
                        const isCollapsedRow = Boolean(childWbsId && collapsed.has(childWbsId));
                        return <button type="button" onClick={(event) => { event.stopPropagation(); if (childWbsId) toggleCollapse(childWbsId); }} onDoubleClick={(event) => event.stopPropagation()} className="shrink-0 rounded p-0.5 hover:bg-muted" aria-label={isCollapsedRow ? t("planning.expand") : t("planning.collapse")} aria-expanded={!isCollapsedRow}>{isCollapsedRow ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}</button>;
                      })() : <span className="w-4 shrink-0" />}
                      {row.activity && editable ? <button type="button" draggable={!moving} onDragStart={(event) => handleActivityDragStart(event, row.activity!.id)} onDragEnd={handleActivityDragEnd} onClick={(event) => event.stopPropagation()} aria-label={t("planning.moveActivity")} title={t("planning.moveActivity")} className="cursor-grab rounded p-0.5 text-muted-foreground/60 hover:bg-muted hover:text-foreground active:cursor-grabbing"><GripVertical className="h-3.5 w-3.5" /></button> : null}
                      {isMilestone ? <Diamond role="img" className="h-3 w-3 shrink-0 fill-warning text-warning" aria-label={t("planning.milestone")} /> : null}
                      <span className="min-w-0 truncate">{displayCellText(row, column) || "—"}</span>
                      {dropTarget && row.activity && dropTarget.targetId === row.activity.id ? <span className="ms-auto rounded bg-primary px-1.5 py-0.5 text-[10px] font-semibold text-primary-foreground">{t(`planning.drop${dropTarget.position}`)}</span> : null}
                    </span>
                  </td>;
                }
                return <td key={column} aria-selected={isCellSelected(row, column)} data-cell-selected={isCellSelected(row, column)} className={cn("whitespace-nowrap px-2 py-1.5 text-xs tabular-nums text-muted-foreground", column === "predecessors" && "font-mono", isCellSelected(row, column) && "bg-primary/10", activeCellClass(row, column))} onMouseDown={(event) => activateCell(row, column, event)} onMouseEnter={() => extendCellSelection(row, column)} onDoubleClick={(event) => { event.stopPropagation(); if (isEditable(row, column)) setEditing({ rowId: row.id, column, draft: cellText(row, column) }); }} title={editable && isEditable(row, column) ? t("planning.doubleClickToEdit") : undefined}>{displayCellText(row, column) || "—"}</td>;
              })}
              <td className="px-2 py-1.5"><div className="flex gap-0.5">{row.activity ? <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={() => onEdit(row.activity!)} aria-label={t("common.edit")}><Pencil className="h-3.5 w-3.5" /></Button> : null}{editable && (row.activity || row.wbs) ? <Button data-testid="planning-grid-row-delete" size="sm" variant="ghost" className="h-7 w-7 p-0" disabled={saving || moving} onClick={() => void removeRow(row)} aria-label={t("common.delete")}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button> : null}</div></td>
            </tr>
            );
          })}
          {filteredRows.length === 0 ? <tr><td colSpan={visibleColumns.length + 1} className="p-8 text-center text-sm text-muted-foreground">{rows.length ? t("planning.noMatchingRows") : t("planning.emptyWorkspace")}</td></tr> : null}
        </tbody>
      </table>
    </div>
    </div>
    {contextMenu && contextRow ? createPortal(<div ref={contextMenuRef} role="menu" style={contextMenuStyle} className="min-w-52 max-h-[calc(100vh-1rem)] overflow-y-auto rounded-lg border bg-popover p-1.5 text-sm shadow-xl">
      {contextRow.activity ? <ContextMenuButton label={t("planning.taskDetails")} onClick={() => { onEdit(contextRow.activity!); setContextMenu(null); }} /> : null}
      {editable && contextRow.activity ? <>
        <ContextMenuButton label={t("planning.insertAbove")} onClick={() => { void createRow("Task", "above", undefined, contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.insertBelow")} onClick={() => { void createRow("Task", "below", undefined, contextRow); setContextMenu(null); }} />
        {rowWbsId(contextRow) ? <ContextMenuButton label={t("planning.addSubtask")} onClick={() => { void createRow("Task", "child", undefined, contextRow); setContextMenu(null); }} /> : null}
        <ContextMenuButton label={t("planning.addMilestone")} onClick={() => { void createRow("Milestone", "below", undefined, contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.indent")} onClick={() => { void indent(contextRow); setContextMenu(null); }} />
        {contextRow.wbs?.parentWbsId ? <ContextMenuButton label={t("planning.outdent")} onClick={() => { void outdent(contextRow); setContextMenu(null); }} /> : null}
        <ContextMenuButton label={t("planning.moveUp")} onClick={() => { void moveSibling("up", contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.moveDown")} onClick={() => { void moveSibling("down", contextRow); setContextMenu(null); }} />
        {onOpenAssignments ? <ContextMenuButton label={t("planning.assignments")} onClick={() => { onOpenAssignments(); setContextMenu(null); }} /> : null}
      </> : null}
      {editable && (contextRow.activity || contextRow.wbs) ? <ContextMenuButton label={t("common.delete")} danger onClick={() => { void removeRow(contextRow); setContextMenu(null); }} /> : null}
    </div>, document.body) : null}
  </div>;
}

function ContextMenuButton({ label, danger, onClick }: { label: string; danger?: boolean; onClick: () => void }) {
  return <button type="button" role="menuitem" onClick={onClick} className={`flex w-full items-center rounded-md px-2.5 py-1.5 text-start text-[13px] hover:bg-accent hover:text-accent-foreground ${danger ? "text-destructive" : ""}`}>{label}</button>;
}
