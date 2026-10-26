export type PlanningGridColumnId =
  | "id" | "wbs" | "outlineLevel" | "taskName" | "type" | "schedulingMode"
  | "schedulingStatus" | "constraintType" | "constraintDate" | "deadline" | "calendar" | "resources"
  | "duration" | "start" | "finish" | "actualStart" | "actualFinish"
  | "baselineStart" | "baselineFinish" | "baselineDuration" | "startVariance"
  | "finishVariance" | "durationVariance" | "critical" | "progress"
  | "predecessors" | "estimateHours" | "description";

export const DEFAULT_PLANNING_GRID_COLUMNS: PlanningGridColumnId[] = [
  "id", "schedulingMode", "wbs", "taskName", "duration", "start", "finish", "progress", "predecessors",
];

export const REQUIRED_PLANNING_GRID_COLUMNS: PlanningGridColumnId[] = ["id", "schedulingMode", "wbs", "taskName"];

const KNOWN_PLANNING_GRID_COLUMNS = new Set<PlanningGridColumnId>([
  "id", "wbs", "outlineLevel", "taskName", "type", "schedulingMode", "duration", "start", "finish",
  "schedulingStatus", "constraintType", "constraintDate", "deadline", "calendar", "resources",
  "actualStart", "actualFinish", "baselineStart", "baselineFinish", "baselineDuration", "startVariance",
  "finishVariance", "durationVariance", "critical", "progress", "predecessors", "estimateHours", "description",
]);

export function normalizePlanningGridColumns(preferred: readonly string[] | undefined): PlanningGridColumnId[] {
  const valid = [...new Set((preferred ?? []).filter((column): column is PlanningGridColumnId => KNOWN_PLANNING_GRID_COLUMNS.has(column as PlanningGridColumnId)))];
  if (!valid.length) return [...DEFAULT_PLANNING_GRID_COLUMNS];
  const optional = valid.filter((column) => !REQUIRED_PLANNING_GRID_COLUMNS.includes(column));
  return [...REQUIRED_PLANNING_GRID_COLUMNS, ...optional];
}

export function reorderPlanningGridColumn(order: readonly PlanningGridColumnId[], source: PlanningGridColumnId, target: PlanningGridColumnId): PlanningGridColumnId[] {
  if (source === target || REQUIRED_PLANNING_GRID_COLUMNS.includes(source)) return [...order];
  const optional = order.filter((column) => !REQUIRED_PLANNING_GRID_COLUMNS.includes(column));
  const sourceIndex = optional.indexOf(source);
  const targetIndex = optional.indexOf(target);
  if (sourceIndex < 0) return [...order];
  const next = optional.filter((column) => column !== source);
  next.splice(targetIndex < 0 ? 0 : Math.max(0, targetIndex - (sourceIndex < targetIndex ? 1 : 0)), 0, source);
  return [...REQUIRED_PLANNING_GRID_COLUMNS, ...next];
}

export function clampPlanningGridWidth(width: number, min = 56, max = 420): number {
  return Math.min(max, Math.max(min, Math.round(width)));
}

export interface PlanningGridColumnFilter {
  text?: string;
  min?: string;
  max?: string;
  value?: string;
}

export type PlanningGridFilterKind = "text" | "number" | "date" | "enum";

export function getPlanningGridFilterKind(column: PlanningGridColumnId): PlanningGridFilterKind {
  if (["id", "outlineLevel", "duration", "baselineDuration", "startVariance", "finishVariance", "durationVariance", "progress", "estimateHours"].includes(column)) return "number";
  if (column === "critical" || ["type", "schedulingMode", "constraintType"].includes(column)) return "enum";
  if (["constraintDate", "deadline", "start", "finish", "actualStart", "actualFinish", "baselineStart", "baselineFinish"].includes(column)) return "date";
  return "text";
}

export function matchesPlanningGridFilter(value: string | number | undefined, filter: PlanningGridColumnFilter | undefined, kind: PlanningGridFilterKind): boolean {
  if (!filter || (!filter.text?.trim() && !filter.min && !filter.max && !filter.value)) return true;
  if (value === undefined || value === "") return false;
  if (kind === "number") {
    const numeric = typeof value === "number" ? value : Number(value);
    if (!Number.isFinite(numeric)) return false;
    return (!filter.min || numeric >= Number(filter.min)) && (!filter.max || numeric <= Number(filter.max));
  }
  if (kind === "date") {
    const date = String(value).slice(0, 10);
    return (!filter.min || date >= filter.min) && (!filter.max || date <= filter.max);
  }
  if (kind === "enum") return !filter.value || String(value) === filter.value;
  return !filter.text?.trim() || String(value).toLocaleLowerCase().includes(filter.text.trim().toLocaleLowerCase());
}
