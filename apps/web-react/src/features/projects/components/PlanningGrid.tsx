import * as React from "react";
import { createPortal } from "react-dom";
import { Bold, ChevronDown, ChevronRight, ChevronUp, ChevronsDownUp, ChevronsUpDown, Columns3, Diamond, Eraser, Filter, GripVertical, IndentDecrease, IndentIncrease, Italic, Link2, Pencil, Plus, RotateCcw, Rows3, Trash2, Underline, Users, X } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Progress } from "@components/ui/Progress";
import { cn } from "@lib/utils";
import { calculateAnchoredOverlayPosition } from "@components/ui/AnchoredOverlay";
import { PLANNING_ACTIVITY_TYPE } from "@lib/enums";
import { toast } from "@hooks/use-toast";
import { usePlanningAssignmentsFull, usePlanningCalendarsFull, usePlanningResourcesFull, usePlanningTablePreferencesFull, useResetPlanningTablePreferencesFull, useSavePlanningTablePreferencesFull, type PlanActivityDto, type PlanAssignmentDto, type PlanCalendarDto, type PlanDependencyDto, type PlanDependencyType, type PlanningColumnFilterDto, type PlanWbsNodeDto, type ResourceLookupDto } from "../api/planningFull";
import { buildPlanWorkspaceRows, findWbsNode, visiblePlanWorkspaceRows, type PlanWorkspaceRow } from "../lib/plan-workspace-rows";
import { clampPlanningGridWidth, DEFAULT_PLANNING_GRID_COLUMNS, getPlanningGridFilterKind, matchesPlanningGridFilter, normalizePlanningGridColumns, reorderPlanningGridColumn, type PlanningGridColumnId } from "../lib/planning-grid-preferences";
import { formatPredecessor, parsePredecessor } from "../lib/predecessor-syntax";
import { PLAN_FONT_SIZE_TOKENS, PLAN_HIGHLIGHT_COLOR_TOKENS, PLAN_TEXT_COLOR_TOKENS, planTextFormatClassName } from "../lib/plan-text-format";
import { PlanningRibbonGroup, type PlanningRibbonTab } from "./PlanningRibbon";

const BASELINE_COLUMNS: PlanningGridColumnId[] = ["baselineStart", "baselineFinish"];
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

interface Mutations {
  updateActivity: { mutateAsync: (args: { id: string; body: unknown }) => Promise<unknown> };
  deleteActivity: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  createActivity: { mutateAsync: (body: unknown) => Promise<unknown> };
  updateWbs: { mutateAsync: (args: { id: string; body: unknown }) => Promise<unknown> };
  createWbs: { mutateAsync: (body: unknown) => Promise<unknown> };
  deleteWbs: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  createDependency: { mutateAsync: (body: unknown) => Promise<unknown> };
  deleteDependency: { mutateAsync: (args: { id: string; rowVersion: string }) => Promise<unknown> };
  moveActivity: { mutateAsync: (args: { id: string; body: { targetActivityId?: string; targetWbsId?: string; position: "Before" | "After" | "Child"; sortOrder?: number; rowVersion: string; wbsRowVersion: string; targetWbsRowVersion?: string } }) => Promise<unknown>; isPending: boolean };
}

function toDateInput(value?: string | null) { return value?.slice(0, 10) ?? ""; }

export function PlanningGrid({ projectId, planId, wbs, activities, dependencies, mutations, editable, showBaseline, ribbonTab, t, onEdit, onDelete, onOpenAssignments }: {
  projectId: string;
  planId: string;
  wbs: PlanWbsNodeDto[];
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  mutations: Mutations;
  editable: boolean;
  showBaseline: boolean;
  ribbonTab: PlanningRibbonTab;
  t: (key: string) => string;
  onEdit: (activity: PlanActivityDto) => void;
  onDelete: (activity: PlanActivityDto) => void;
  onOpenAssignments?: () => void;
}) {
  const [collapsed, setCollapsed] = React.useState<ReadonlySet<string>>(() => new Set());
  const [columns, setColumns] = React.useState<PlanningGridColumnId[]>(DEFAULT_PLANNING_GRID_COLUMNS);
  const [columnFilters, setColumnFilters] = React.useState<Record<string, PlanningColumnFilterDto>>({});
  const [columnWidths, setColumnWidths] = React.useState<Record<PlanningGridColumnId, number>>(() => Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, value]) => [key, value.initial])) as Record<PlanningGridColumnId, number>);
  const [columnManagerOpen, setColumnManagerOpen] = React.useState(false);
  const [columnFiltersOpen, setColumnFiltersOpen] = React.useState(false);
  const [draggedColumn, setDraggedColumn] = React.useState<PlanningGridColumnId | null>(null);
  const [editing, setEditing] = React.useState<{ rowId: string; column: PlanningGridColumnId; draft: string } | null>(null);
  const [quickName, setQuickName] = React.useState("");
  const [pastingTasks, setPastingTasks] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [moving, setMoving] = React.useState(false);
  const [draggedActivityId, setDraggedActivityId] = React.useState<string | null>(null);
  const [dropTarget, setDropTarget] = React.useState<{ targetId: string; position: "Before" | "After" | "Child" } | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [selectedId, setSelectedId] = React.useState("");
  const [contextMenu, setContextMenu] = React.useState<{ rowId: string; x: number; y: number } | null>(null);
  const contextMenuRef = React.useRef<HTMLDivElement>(null);
  const [contextMenuStyle, setContextMenuStyle] = React.useState<React.CSSProperties>({ visibility: "hidden" });
  const [error, setError] = React.useState<string | null>(null);
  const [preferenceSaveRevision, setPreferenceSaveRevision] = React.useState(0);
  const tablePreferences = usePlanningTablePreferencesFull(projectId, planId);
  const needAssignments = columns.includes("resources");
  const needCalendars = columns.includes("calendar");
  const assignmentsQuery = usePlanningAssignmentsFull(projectId, planId, needAssignments);
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
  const committingCellKeys = React.useRef(new Set<string>());

  const notifySaved = () => toast({ title: t("planning.saved"), description: t("planning.updated"), variant: "success" });
  const notifyError = React.useCallback((e: unknown) => toast({ title: t("common.error"), description: e instanceof Error ? e.message : t("planning.saveFailed"), variant: "destructive" }), [t]);

  const markPreferencesDirty = () => {
    if (!editable || preferencesHydratedPlan.current !== planId) return;
    preferencesDirty.current = true;
    preferencesRevision.current += 1;
  };

  React.useEffect(() => {
    preferencesHydratedPlan.current = null;
    preferencesDirty.current = false;
    preferencesRevision.current = 0;
    if (preferenceSaveTimer.current) clearTimeout(preferenceSaveTimer.current);
    setColumns([...DEFAULT_PLANNING_GRID_COLUMNS]);
    setColumnFilters({});
    setColumnWidths(Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, value]) => [key, value.initial])) as Record<PlanningGridColumnId, number>);
    setCollapsed(new Set());
  }, [planId]);

  React.useEffect(() => {
    const preference = tablePreferences.data;
    if (!preference || preference.planId !== planId || preferencesHydratedPlan.current === planId) return;
    setColumns(normalizePlanningGridColumns(preference.columnOrder.length ? preference.columnOrder : preference.visibleColumns));
    setColumnFilters(preference.columnFilters ?? {});
    setColumnWidths(Object.fromEntries(Object.entries(COLUMN_WIDTHS).map(([key, bounds]) => {
      const id = key as PlanningGridColumnId;
      return [id, clampPlanningGridWidth(preference.columnWidths[id] ?? bounds.initial, bounds.min, bounds.max)];
    })) as Record<PlanningGridColumnId, number>);
    setCollapsed(new Set(preference.collapsedRowIds));
    preferencesHydratedPlan.current = planId;
    preferencesDirty.current = false;
  }, [planId, tablePreferences.data]);

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
  const activityForWbs = React.useCallback((wbsId?: string) => {
    if (!wbsId) return undefined;
    const direct = fullRows.find((r) => r.wbs?.id === wbsId)?.activity;
    if (direct) return direct;
    const node = findWbsNode(wbs, wbsId);
    return node?.activityId ? activities.find((a) => a.id === node.activityId) : undefined;
  }, [fullRows, wbs, activities]);
  const siblingWbsNodes = React.useCallback((parentWbsId?: string) => {
    if (!parentWbsId) return wbs.slice().sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
    const parent = findWbsNode(wbs, parentWbsId);
    return (parent?.children ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
  }, [wbs]);

  const siblingWbsNode = (target: PlanWorkspaceRow | undefined, direction: "up" | "down") => {
    if (!target?.wbs) return undefined;
    const siblings = siblingWbsNodes(target.wbs.parentWbsId);
    const index = siblings.findIndex((node) => node.id === target.wbs!.id);
    return siblings[index + (direction === "up" ? -1 : 1)];
  };

  const siblingActivity = (target: PlanWorkspaceRow | undefined, direction: "up" | "down") => {
    if (!target?.activity) return undefined;
    if (target.wbs) {
      const siblings = siblingWbsNodes(target.wbs.parentWbsId);
      const index = siblings.findIndex((node) => node.id === target.wbs!.id);
      const step = direction === "up" ? -1 : 1;
      for (let cursor = index + step; cursor >= 0 && cursor < siblings.length; cursor += step) {
        const candidate = activityForWbs(siblings[cursor].id);
        if (candidate) return candidate;
      }
      return undefined;
    }
    if (target.activity.wbsId) {
      const siblings = activities.filter((item) => item.wbsId === target.activity!.wbsId).sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0) || left.code.localeCompare(right.code));
      const index = siblings.findIndex((item) => item.id === target.activity!.id);
      return siblings[index + (direction === "up" ? -1 : 1)];
    }
    const roots = activities.filter((item) => !item.wbsId).sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0) || left.code.localeCompare(right.code));
    const index = roots.findIndex((item) => item.id === target.activity!.id);
    return roots[index + (direction === "up" ? -1 : 1)];
  };

  const doMove = async (activityId: string, input: { targetActivityId?: string; targetWbsId?: string; position: "Before" | "After" | "Child"; sortOrder?: number }) => {
    const activity = activities.find((a) => a.id === activityId);
    if (!activity) return;
    const sourceNode = activity.wbsId ? findWbsNode(wbs, activity.wbsId) : undefined;
    const targetActivity = input.targetActivityId ? activities.find((a) => a.id === input.targetActivityId) : undefined;
    const targetNode = targetActivity?.wbsId ? findWbsNode(wbs, targetActivity.wbsId) : undefined;
    setMoving(true);
    try {
      await mutations.moveActivity.mutateAsync({
        id: activityId,
        body: { ...input, rowVersion: activity.rowVersion, wbsRowVersion: (sourceNode as { rowVersion?: string } | undefined)?.rowVersion ?? "", targetWbsRowVersion: (targetNode as { rowVersion?: string } | undefined)?.rowVersion },
      });
      setSelectedId(`activity:${activityId}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const handleActivityDragStart = (event: React.DragEvent<HTMLButtonElement>, activityId: string) => {
    if (!editable || moving) { event.preventDefault(); return; }
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", activityId);
    setSelectedId(`activity:${activityId}`);
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
      setSelectedId(node.activityId ? `activity:${node.activityId}` : `wbs:${node.id}`);
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
        estimateHours: activity.estimateHours, progress: activity.progress, wbsId: wbsId ?? null,
        sortOrder, textFormat: activity.textFormat, rowVersion: activity.rowVersion,
      } });
      setSelectedId(`activity:${activity.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setMoving(false);
    }
  };

  const nextWbsSortOrder = (parentWbsId?: string, excludingId?: string) => Math.max(-1, ...siblingWbsNodes(parentWbsId).filter((node) => node.id !== excludingId).map((node) => node.sortOrder)) + 1;
  const nextActivitySortOrder = (wbsId?: string) => Math.max(-1, ...activities.filter((item) => item.wbsId === wbsId).map((item) => item.sortOrder ?? 0)) + 1;
  const rowWbsId = (row: PlanWorkspaceRow | undefined) => row?.wbs?.id ?? row?.activity?.wbsId;
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

  const indent = async (target = selected) => {
    if (!target) return;
    const parentWbsId = rowWbsId(previousIndentTarget(target));
    if (!parentWbsId || parentWbsId === rowWbsId(target)) return;
    if (target.wbs) {
      if (wouldCreateWbsCycle(target.wbs.id, parentWbsId)) return;
      await updateWbsPlacement(target.wbs, parentWbsId, nextWbsSortOrder(parentWbsId, target.wbs.id));
    } else if (target.activity) {
      await updateActivityPlacement(target.activity, parentWbsId, nextActivitySortOrder(parentWbsId));
    }
  };

  const outdent = async (target = selected) => {
    if (target?.wbs?.parentWbsId) {
      const parent = findWbsNode(wbs, target.wbs.parentWbsId);
      await updateWbsPlacement(target.wbs, parent?.parentWbsId, nextWbsSortOrder(parent?.parentWbsId, target.wbs.id));
      return;
    }
    if (target?.activity?.wbsId) {
      const currentNode = findWbsNode(wbs, target.activity.wbsId);
      if (!currentNode) return;
      const destinationWbsId = currentNode.parentWbsId;
      await updateActivityPlacement(target.activity, destinationWbsId, nextActivitySortOrder(destinationWbsId));
    }
  };

  const moveSibling = async (direction: "up" | "down", target = selected) => {
    if (target?.wbs) {
      const adjacent = siblingWbsNode(target, direction);
      if (!adjacent) return;
      const originalOrder = target.wbs.sortOrder;
      await updateWbsPlacement(target.wbs, target.wbs.parentWbsId, adjacent.sortOrder);
      await updateWbsPlacement(adjacent, adjacent.parentWbsId, originalOrder);
      setSelectedId(target.id);
      return;
    }
    if (!target?.activity) return;
    const sibling = siblingActivity(target, direction);
    if (sibling) await doMove(target.activity.id, { targetActivityId: sibling.id, position: direction === "up" ? "Before" : "After" });
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

  const toggleCollapse = (wbsId: string) => { markPreferencesDirty(); setCollapsed((prev) => { const next = new Set(prev); if (next.has(wbsId)) next.delete(wbsId); else next.add(wbsId); return next; }); };
  const collapseAll = () => { markPreferencesDirty(); setCollapsed(new Set(fullRows.filter((r) => r.wbs && r.hasChildren).map((r) => r.wbs!.id))); };
  const expandAll = () => { markPreferencesDirty(); setCollapsed(new Set()); };
  const collapsibleWbsIds = React.useMemo(() => fullRows.filter((r) => r.wbs && r.hasChildren).map((r) => r.wbs!.id), [fullRows]);
  const indentParentWbsId = selected ? rowWbsId(previousIndentTarget(selected)) : undefined;
  const canIndentSelected = Boolean(indentParentWbsId && indentParentWbsId !== rowWbsId(selected) && (!selected?.wbs || !wouldCreateWbsCycle(selected.wbs.id, indentParentWbsId)));
  const canOutdentSelected = Boolean(selected?.wbs?.parentWbsId || (selected?.activity?.wbsId && findWbsNode(wbs, selected.activity.wbsId)));
  const canMoveSelectedUp = Boolean(selected && (selected.wbs ? siblingWbsNode(selected, "up") : siblingActivity(selected, "up")));
  const canMoveSelectedDown = Boolean(selected && (selected.wbs ? siblingWbsNode(selected, "down") : siblingActivity(selected, "down")));
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
      case "wbs": return row.wbs?.code ?? a?.code ?? "";
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
      case "progress": return a?.progress == null ? "" : String(a.progress);
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
  const filterOptionLabel = (column: PlanningGridColumnId, value: string) => {
    if (column === "schedulingMode") return t(value === "Manual" ? "planning.manual" : "planning.automatic");
    if (column === "constraintType") return t(`planning.constraintTypes.${value}`);
    if (column === "type") return value === "WBS" ? t("planning.columns.wbs") : t(`planning.${value.toLowerCase()}`);
    if (column === "critical") return t(value === "Yes" ? "common.yes" : "common.no");
    return value;
  };

  const toggleColumn = (columnId: PlanningGridColumnId) => {
    if (["schedulingMode", "id", "taskName"].includes(columnId)) return;
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
    setCollapsed(new Set());
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
      else if (column === "progress") { const n = Number(value); if (!Number.isFinite(n) || n < 0 || n > 100 || n === (activity.progress ?? 0)) return; patch.progress = n; }
      else if (column === "schedulingMode") { if (!(value === "Automatic" || value === "Manual") || value === (activity.schedulingMode ?? "Automatic")) return; patch.schedulingMode = value; }
      else if (column === "constraintType") { if (!(PLAN_CONSTRAINT_TYPES as readonly string[]).includes(value) || value === (activity.constraintType ?? "AsSoonAsPossible")) return; patch.constraintType = value; }
      else if (column === "start" || column === "finish" || column === "actualStart" || column === "actualFinish" || column === "constraintDate" || column === "deadline") {
        if (value && !/^\d{4}-\d{2}-\d{2}$/.test(value)) return;
        const field = column === "start" ? "startDate" : column === "finish" ? "endDate" : column;
        const current = toDateInput((activity as unknown as Record<string, string | undefined>)[field]);
        if (value === current) return;
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
          progress: ("progress" in patch ? patch.progress : activity.progress) ?? null,
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
    const current = depsByActivity.get(activity.id) ?? [];
    const currentKeys = new Set(current.map((d) => {
      const num = rowNumberByActivity.get(d.predecessorActivityId);
      return num === undefined ? null : formatPredecessor(num, d.type, d.lag);
    }));
    const wanted = text.split(",").map((s) => s.trim()).filter(Boolean);
    const parsed = wanted.map(parsePredecessor).filter((p): p is NonNullable<typeof p> => Boolean(p));
    if (parsed.length !== wanted.length) { setError(t("planning.predecessorSyntaxError")); return; }
    const wantedKeys = new Set<string>();
    const toCreate: { predecessorActivityId: string; type: PlanDependencyType; lag?: number }[] = [];
    for (const p of parsed) {
      const pred = activityByRowNumber.get(p.rowNumber);
      if (!pred || pred.id === activity.id) { setError(t("planning.predecessorSyntaxError")); return; }
      const key = formatPredecessor(p.rowNumber, p.type, p.lag);
      wantedKeys.add(key);
      if (!currentKeys.has(key)) toCreate.push({ predecessorActivityId: pred.id, type: p.type, lag: p.lag });
    }
    setSaving(true);
    try {
      for (const dep of current) {
        const num = rowNumberByActivity.get(dep.predecessorActivityId);
        const key = num === undefined ? null : formatPredecessor(num, dep.type, dep.lag);
        if (!key || !wantedKeys.has(key)) await mutations.deleteDependency.mutateAsync({ id: dep.id, rowVersion: dep.rowVersion });
      }
      for (const c of toCreate) await mutations.createDependency.mutateAsync({ activityId: activity.id, predecessorActivityId: c.predecessorActivityId, type: c.type, lag: c.lag ?? 0 });
      notifySaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : t("common.error"));
    } finally {
      setSaving(false);
    }
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

  const createRow = async (type: "Task" | "Milestone", placement: "append" | "above" | "below", nameOverride?: string, anchorOverride?: PlanWorkspaceRow): Promise<boolean> => {
    if (!editable || creating) return false;
    const anchor = placement === "append" ? undefined : anchorOverride ?? selected;
    const parentWbsId = anchor?.wbs?.parentWbsId;
    const sortOrder = anchor ? anchor.wbs?.sortOrder ?? anchor.activity?.sortOrder ?? 0 : Math.max(-1, ...fullRows.filter((row) => row.depth === 0).map((row) => row.wbs?.sortOrder ?? row.activity?.sortOrder ?? 0)) + 1;
    const placedOrder = placement === "above" ? sortOrder - 1 : placement === "below" ? sortOrder + 1 : sortOrder;
    const code = `${type === "Milestone" ? "M" : "T"}-${Math.random().toString(36).slice(2, 10).toUpperCase()}`;
    setCreating(true);
    setError(null);
    let createdNodeId: string | undefined;
    let createdNodeVersion: string | undefined;
    try {
      const node = await mutations.createWbs.mutateAsync({ code, name: nameOverride?.trim() || (type === "Milestone" ? t("planning.newMilestone") : t("planning.newTask")), parentWbsId: parentWbsId ?? null, sortOrder: placedOrder }) as PlanWbsNodeDto;
      createdNodeId = node.id;
      createdNodeVersion = node.rowVersion;
      const createdActivity = await mutations.createActivity.mutateAsync({ code, name: node.name, type, duration: type === "Milestone" ? 0 : 1, progress: 0, wbsId: node.id, sortOrder: placedOrder }) as PlanActivityDto;
      setSelectedId(`activity:${createdActivity.id}`);
      notifySaved();
      return true;
    } catch (e) {
      if (createdNodeId) {
        try { await mutations.deleteWbs.mutateAsync({ id: createdNodeId, rowVersion: createdNodeVersion ?? "" }); } catch { /* original error stands */ }
      }
      notifyError(e);
      return false;
    } finally {
      setCreating(false);
    }
  };

  const updateTextFormat = async (format: Record<string, unknown> | null) => {
    if (!editable || !selected) return;
    setSaving(true);
    try {
      if (selected.activity) {
        const a = selected.activity;
        await mutations.updateActivity.mutateAsync({ id: a.id, body: { code: a.code, name: a.name, description: a.description ?? null, type: a.type, schedulingMode: a.schedulingMode, constraintType: a.constraintType, constraintDate: a.constraintDate ?? null, deadline: a.deadline ?? null, startDate: a.startDate ?? null, endDate: a.endDate ?? null, actualStart: a.actualStart ?? null, actualFinish: a.actualFinish ?? null, calendarId: a.calendarId ?? null, duration: a.duration, estimateHours: a.estimateHours ?? null, progress: a.progress ?? null, wbsId: a.wbsId ?? null, sortOrder: a.sortOrder, textFormat: format, rowVersion: a.rowVersion } });
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
    if (!editable || !target || target.hasChildren) {
      if (target?.hasChildren) setError(t("planning.deleteSummaryFirst"));
      return;
    }
    if (!window.confirm(t("planning.confirmDelete"))) return;
    setSaving(true);
    try {
      if (target.activity) {
        const related = dependencies.filter((d) => d.activityId === target.activity!.id || d.predecessorActivityId === target.activity!.id);
        for (const dep of related) await mutations.deleteDependency.mutateAsync({ id: dep.id, rowVersion: dep.rowVersion });
        await mutations.deleteActivity.mutateAsync({ id: target.activity.id, rowVersion: target.activity.rowVersion });
      }
      if (target.wbs) {
        const latest = findWbsNode(wbs, target.wbs.id) ?? target.wbs;
        await mutations.deleteWbs.mutateAsync({ id: latest.id, rowVersion: latest.rowVersion });
      }
      setSelectedId("");
      notifySaved();
    } catch (e) {
      notifyError(e);
    } finally {
      setSaving(false);
    }
  };

  const openContextMenu = (row: PlanWorkspaceRow, event: React.MouseEvent<HTMLElement>) => {
    if (!row.activity && !row.wbs) return;
    event.preventDefault();
    setSelectedId(row.id);
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
    if (e.key === "Enter") direction = "next";
    else if (e.key === "Tab") direction = e.shiftKey ? "previous" : "next";
    else if (e.key === "ArrowUp") direction = "up";
    else if (e.key === "ArrowDown") direction = "down";
    else if (e.currentTarget.type === "text" && e.key === "ArrowLeft" && e.currentTarget.selectionStart === 0 && e.currentTarget.selectionEnd === 0) direction = "previous";
    else if (e.currentTarget.type === "text" && e.key === "ArrowRight" && e.currentTarget.selectionStart === e.currentTarget.value.length && e.currentTarget.selectionEnd === e.currentTarget.value.length) direction = "next";
    if (!direction) return;
    e.preventDefault();
    const next = findEditableCell(row, column, direction);
    void commitCell(row, column, e.currentTarget.value).then(() => {
      if (next) setEditing({ rowId: next.row.id, column: next.column, draft: cellText(next.row, next.column) });
    });
  };

  return <div>
    {ribbonTab !== "project" && ribbonTab !== "view" ? <div className="flex min-w-max items-stretch gap-2 overflow-x-auto border-b bg-muted/20 px-2 py-1.5 text-[12px]">
      {ribbonTab === "task" ? <>
        <PlanningRibbonGroup label={t("planning.ribbon.task")}>
          <Button size="sm" variant="secondary" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || creating} onClick={() => void createRow("Task", "append")}><Plus className="h-3.5 w-3.5" />{t("planning.addTask")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || creating} onClick={() => void createRow("Milestone", "append")}><Diamond className="h-3.5 w-3.5" />{t("planning.addMilestone")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || creating} onClick={() => void createRow("Task", "above")}><Rows3 className="h-3.5 w-3.5" />{t("planning.insertAbove")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || creating} onClick={() => void createRow("Task", "below")}><Rows3 className="h-3.5 w-3.5 rotate-180" />{t("planning.insertBelow")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !selected || moving} onClick={() => void removeRow()}><Trash2 className="h-3.5 w-3.5 text-destructive" />{t("common.delete")}</Button>
        </PlanningRibbonGroup>
        <PlanningRibbonGroup label={t("planning.ribbon.hierarchy")}>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !canIndentSelected || moving} onClick={() => void indent()}><IndentIncrease className="h-3.5 w-3.5" />{t("planning.indent")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !canOutdentSelected || moving} onClick={() => void outdent()}><IndentDecrease className="h-3.5 w-3.5" />{t("planning.outdent")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !canMoveSelectedUp || moving} onClick={() => void moveSibling("up")}><ChevronUp className="h-3.5 w-3.5" />{t("planning.moveUp")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!editable || !canMoveSelectedDown || moving} onClick={() => void moveSibling("down")}><ChevronDown className="h-3.5 w-3.5" />{t("planning.moveDown")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!canToggleSelectedCollapse || moving} aria-pressed={selectedIsCollapsed} onClick={toggleSelectedCollapse}>{selectedIsCollapsed ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}{selectedIsCollapsed ? t("planning.expand") : t("planning.collapse")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!canCollapseAll || moving} onClick={collapseAll}><ChevronsDownUp className="h-3.5 w-3.5" />{t("planning.collapseAll")}</Button>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!canExpandAll || moving} onClick={expandAll}><ChevronsUpDown className="h-3.5 w-3.5" />{t("planning.expandAll")}</Button>
        </PlanningRibbonGroup>
        <PlanningRibbonGroup label={t("planning.ribbon.links")}>
          <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" disabled={!selected?.activity} onClick={() => { if (selected?.activity) onEdit(selected.activity); }}><Link2 className="h-3.5 w-3.5" />{t("planning.manageLinks")}</Button>
        </PlanningRibbonGroup>
      </> : null}
      {ribbonTab === "resource" && onOpenAssignments ? <PlanningRibbonGroup label={t("planning.ribbon.resource")}>
        <Button size="sm" variant="ghost" className="h-7 gap-1 px-2 text-[11px]" onClick={onOpenAssignments}><Users className="h-3.5 w-3.5" />{t("planning.resourceAssignments")}</Button>
      </PlanningRibbonGroup> : null}
      {ribbonTab === "format" ? <PlanningRibbonGroup label={t("planning.ribbon.format")} className="min-w-[26rem]">
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
    {columnManagerOpen ? <div className="flex flex-wrap gap-1.5 rounded-lg border p-3">{ALL_COLUMNS.map((c) => { const active = visibleColumns.includes(c.id); const required = ["schedulingMode", "id", "taskName"].includes(c.id); return <button key={c.id} type="button" aria-pressed={active} disabled={!editable || required || (showBaseline && BASELINE_COLUMNS.includes(c.id) && active)} onClick={() => toggleColumn(c.id)} className={cn("rounded-full border px-2.5 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-60", active ? "border-primary bg-primary/10 text-primary" : "border-input text-muted-foreground")}>{t(c.labelKey)}</button>; })}</div> : null}
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[960px] table-fixed text-sm">
        <colgroup><col style={{ width: 40 }} />{visibleColumns.map((column) => <col key={column} style={{ width: columnWidths[column] ?? COLUMN_WIDTHS[column].initial }} />)}<col style={{ width: 64 }} /></colgroup>
        <thead><tr className="border-b bg-muted/60 text-start text-xs text-muted-foreground">
          <th className="w-10 px-2 py-2.5" />
          {visibleColumns.map((c) => <th key={c} draggable={editable && !["schedulingMode", "id", "taskName"].includes(c)} onDragStart={(event) => { if (["schedulingMode", "id", "taskName"].includes(c)) { event.preventDefault(); return; } setDraggedColumn(c); event.dataTransfer.effectAllowed = "move"; }} onDragOver={(event) => { if (draggedColumn) event.preventDefault(); }} onDrop={(event) => { event.preventDefault(); if (editable && draggedColumn) { markPreferencesDirty(); setColumns((current) => reorderPlanningGridColumn(current, draggedColumn, c)); } setDraggedColumn(null); }} onDragEnd={() => setDraggedColumn(null)} className={cn("relative px-2 py-2.5 text-start font-semibold", editable && !["schedulingMode", "id", "taskName"].includes(c) && "cursor-grab", draggedColumn === c && "opacity-50")} title={t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}>
            <span className="block truncate pe-2">{t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}</span>
            {editable && !["schedulingMode", "id", "taskName"].includes(c) ? <GripVertical className="absolute start-0 top-1/2 h-3 w-3 -translate-y-1/2 text-muted-foreground/60" aria-hidden="true" /> : null}
            {editable ? <span role="separator" aria-label={`${t("planning.resizeColumn")}: ${t(ALL_COLUMNS.find((a) => a.id === c)?.labelKey ?? c)}`} aria-orientation="vertical" onPointerDown={(event) => beginColumnResize(c, event)} onPointerMove={moveColumnResize} onPointerUp={endColumnResize} onPointerCancel={endColumnResize} className="absolute inset-y-1 end-0 z-10 w-2 cursor-col-resize touch-none rounded-sm hover:bg-primary/30" /> : null}
          </th>)}
          <th className="w-16 px-2 py-2.5" />
        </tr></thead>
        <tbody>
          {filteredRows.map((row) => (
      <tr key={row.id} aria-selected={selectedId === row.id} onClick={() => setSelectedId(row.id)} onDoubleClick={() => { if (row.activity) onEdit(row.activity); }} onContextMenu={(e) => openContextMenu(row, e)} onDragOver={(event) => handleRowDragOver(event, row)} onDrop={(event) => handleRowDrop(event, row)} className={cn("border-b transition-colors last:border-0 hover:bg-muted/40", selectedId === row.id && "bg-primary/10", row.activity?.critical && "bg-destructive/[0.03]", planTextFormatClassName(row.textFormat), draggedActivityId === row.activity?.id && "opacity-50", dropTarget?.targetId === row.activity?.id && "outline outline-2 outline-primary")}>
              <td className="px-2 py-1.5" style={{ paddingInlineStart: 8 + row.depth * 16 }}>
                {row.wbs && row.hasChildren ? <button type="button" onClick={() => toggleCollapse(row.wbs!.id)} className="rounded p-0.5 hover:bg-muted" aria-label={collapsed.has(row.wbs.id) ? t("planning.expand") : t("planning.collapse")}>{collapsed.has(row.wbs.id) ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}</button> : null}
              </td>
              {visibleColumns.map((column) => {
                const isEditing = editing?.rowId === row.id && editing.column === column;
                if (column === "progress" && row.activity && !isEditing) {
                  return <td key={column} className="min-w-28 px-2 py-1.5" onDoubleClick={(event) => { event.stopPropagation(); if (isEditable(row, column)) setEditing({ rowId: row.id, column, draft: cellText(row, column) }); }}><span className="flex items-center gap-2"><Progress value={row.activity.progress ?? 0} className="h-1.5 w-12" /><span className="text-xs tabular-nums">{row.activity.progress ?? 0}%</span></span></td>;
                }
                if (column === "schedulingMode" && row.activity && !isEditing) {
                  const mode = row.activity.schedulingMode ?? "Automatic";
                  return <td key={column} className="px-2 py-1.5 text-center"><button type="button" disabled={!editable || saving} aria-label={`${t("planning.schedulingMode")}: ${t(mode === "Automatic" ? "planning.automatic" : "planning.manual")}`} aria-pressed={mode === "Automatic"} onClick={(event) => { event.stopPropagation(); setSelectedId(row.id); void commitCell(row, "schedulingMode", mode === "Automatic" ? "Manual" : "Automatic"); }} onDoubleClick={(event) => event.stopPropagation()} className="rounded border border-transparent px-2 py-1 text-xs text-muted-foreground hover:border-input hover:bg-background disabled:opacity-100">{displayCellText(row, column) || "—"}</button></td>;
                }
                if (column === "constraintType" && row.activity && !isEditing) {
                  return <td key={column} className="px-1 py-1"><select className="h-7 w-full rounded border bg-background px-1 text-xs" disabled={!editable || saving} aria-label={t("planning.columns.constraintType")} value={row.activity.constraintType ?? "AsSoonAsPossible"} onClick={(event) => event.stopPropagation()} onFocus={() => setSelectedId(row.id)} onDoubleClick={(event) => event.stopPropagation()} onChange={(event) => void commitCell(row, "constraintType", event.target.value)}>{PLAN_CONSTRAINT_TYPES.map((value) => <option key={value} value={value}>{t(`planning.constraintTypes.${value}`)}</option>)}</select></td>;
                }
                if (["schedulingStatus", "constraintType", "critical", "type"].includes(column) && !isEditing) {
                  return <td key={column} className="px-2 py-1.5 text-xs text-muted-foreground" title={displayCellText(row, column)}>{displayCellText(row, column) || "—"}</td>;
                }
                if (isEditing) {
                  return <td key={column} className="px-1 py-1" onDoubleClick={(event) => event.stopPropagation()}><Input autoFocus disabled={pastingTasks} value={editing.draft} onChange={(e) => setEditing({ ...editing, draft: e.target.value })} onBlur={(e) => void commitCell(row, column, e.target.value)} onPaste={(event) => { if (column !== "taskName") return; const pasted = event.clipboardData.getData("text"); const names = pasted.split(/\r?\n/).map((name) => name.trim()).filter(Boolean); if (names.length > 1) { event.preventDefault(); void createPastedTasks(names); } }} onKeyDown={(e) => onCellKey(row, column, e)} className="h-7 text-xs" type={column === "start" || column === "finish" || column === "actualStart" || column === "actualFinish" || column === "constraintDate" || column === "deadline" ? "date" : column === "duration" || column === "progress" ? "number" : "text"} /></td>;
                }
                return <td key={column} className={cn("px-2 py-1.5", column === "taskName" && planTextFormatClassName(row.textFormat), column === "taskName" ? "min-w-40 font-medium" : "whitespace-nowrap text-xs tabular-nums text-muted-foreground", column === "predecessors" && "font-mono")} onDoubleClick={(event) => { event.stopPropagation(); if (isEditable(row, column)) setEditing({ rowId: row.id, column, draft: cellText(row, column) }); }} title={editable && isEditable(row, column) ? t("planning.doubleClickToEdit") : undefined}>{column === "taskName" && row.activity && editable ? <span className="flex items-center gap-1"><button type="button" draggable={!moving} onDragStart={(event) => handleActivityDragStart(event, row.activity!.id)} onDragEnd={handleActivityDragEnd} onClick={(event) => event.stopPropagation()} aria-label={t("planning.moveActivity")} title={t("planning.moveActivity")} className="cursor-grab rounded p-0.5 text-muted-foreground/60 hover:bg-muted hover:text-foreground active:cursor-grabbing"><GripVertical className="h-3.5 w-3.5" /></button><span>{displayCellText(row, column) || "—"}</span>{dropTarget?.targetId === row.activity.id ? <span className="ms-auto rounded bg-primary px-1.5 py-0.5 text-[10px] font-semibold text-primary-foreground">{t(`planning.drop${dropTarget.position}`)}</span> : null}</span> : displayCellText(row, column) || "—"}</td>;
              })}
              <td className="px-2 py-1.5"><div className="flex gap-0.5">{row.activity ? <><Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={() => onEdit(row.activity!)} aria-label={t("common.edit")}><Pencil className="h-3.5 w-3.5" /></Button>{editable ? <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={() => onDelete(row.activity!)} aria-label={t("common.delete")}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button> : null}</> : null}</div></td>
            </tr>
          ))}
          {filteredRows.length === 0 ? <tr><td colSpan={visibleColumns.length + 2} className="p-8 text-center text-sm text-muted-foreground">{rows.length ? t("planning.noMatchingRows") : t("planning.emptyWorkspace")}</td></tr> : null}
        </tbody>
      </table>
    </div>
    </div>
    {contextMenu && contextRow ? createPortal(<div ref={contextMenuRef} role="menu" style={contextMenuStyle} className="min-w-52 max-h-[calc(100vh-1rem)] overflow-y-auto rounded-lg border bg-popover p-1.5 text-sm shadow-xl">
      <ContextMenuButton label={t("planning.taskDetails")} onClick={() => { if (contextRow.activity) onEdit(contextRow.activity); setContextMenu(null); }} />
      {editable && contextRow.activity ? <>
        <ContextMenuButton label={t("planning.insertAbove")} onClick={() => { void createRow("Task", "above", undefined, contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.insertBelow")} onClick={() => { void createRow("Task", "below", undefined, contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.addMilestone")} onClick={() => { void createRow("Milestone", "below", undefined, contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.indent")} onClick={() => { void indent(contextRow); setContextMenu(null); }} />
        {contextRow.wbs?.parentWbsId ? <ContextMenuButton label={t("planning.outdent")} onClick={() => { void outdent(contextRow); setContextMenu(null); }} /> : null}
        <ContextMenuButton label={t("planning.moveUp")} onClick={() => { void moveSibling("up", contextRow); setContextMenu(null); }} />
        <ContextMenuButton label={t("planning.moveDown")} onClick={() => { void moveSibling("down", contextRow); setContextMenu(null); }} />
        {onOpenAssignments ? <ContextMenuButton label={t("planning.assignments")} onClick={() => { onOpenAssignments(); setContextMenu(null); }} /> : null}
        <ContextMenuButton label={t("common.delete")} danger onClick={() => { void removeRow(contextRow); setContextMenu(null); }} />
      </> : null}
    </div>, document.body) : null}
  </div>;
}

function ContextMenuButton({ label, danger, onClick }: { label: string; danger?: boolean; onClick: () => void }) {
  return <button type="button" role="menuitem" onClick={onClick} className={`flex w-full items-center rounded-md px-2.5 py-1.5 text-start text-[13px] hover:bg-accent hover:text-accent-foreground ${danger ? "text-destructive" : ""}`}>{label}</button>;
}
