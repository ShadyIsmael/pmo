import type { PlanAssignmentDto } from "@/lib/api/planning";
import type { PlanWorkspaceRow } from "./plan-workspace-rows";

export type PlanningColumnId =
  | "id"
  | "wbs"
  | "outlineLevel"
  | "taskName"
  | "type"
  | "schedulingMode"
  | "schedulingStatus"
  | "constraintType"
  | "constraintDate"
  | "deadline"
  | "duration"
  | "start"
  | "finish"
  | "calendar"
  | "actualStart"
  | "actualFinish"
  | "baselineStart"
  | "baselineFinish"
  | "baselineDuration"
  | "startVariance"
  | "finishVariance"
  | "durationVariance"
  | "totalSlack"
  | "freeSlack"
  | "critical"
  | "progress"
  | "predecessors"
  | "resources"
  | "estimateHours"
  | "description";

export type PlanningColumnFilterType = "text" | "number" | "date" | "enum";
export type PlanningColumnFilter = { text?: string; min?: string; max?: string; value?: string };

export interface PlanningColumnDefinition {
  id: PlanningColumnId;
  labelKey: string;
  categoryKey: string;
  filterType: PlanningColumnFilterType;
  initialWidth: number;
  minWidth: number;
  maxWidth: number;
  required?: boolean;
  defaultVisible?: boolean;
  enumValues?: readonly string[];
}

export interface PlanningColumnValueContext {
  predecessorText: (_activityId: string) => string;
  successorCountByActivity: Map<string, number>;
  assignmentsByActivity: Map<string, PlanAssignmentDto[]>;
  resourceNames: Map<string, string>;
  calendarNames: Map<string, string>;
  defaultCalendarName?: string;
}

export const PLANNING_COLUMN_DEFINITIONS: readonly PlanningColumnDefinition[] = [
  { id: "schedulingMode", labelKey: "columns.schedulingMode", categoryKey: "columnCategories.schedule", filterType: "enum", initialWidth: 56, minWidth: 48, maxWidth: 72, required: true, enumValues: ["Automatic", "Manual"], defaultVisible: true },
  { id: "schedulingStatus", labelKey: "columns.schedulingStatus", categoryKey: "columnCategories.schedule", filterType: "text", initialWidth: 150, minWidth: 110, maxWidth: 260, defaultVisible: false },
  { id: "constraintType", labelKey: "columns.constraintType", categoryKey: "columnCategories.schedule", filterType: "enum", initialWidth: 170, minWidth: 130, maxWidth: 260, enumValues: ["AsSoonAsPossible", "AsLateAsPossible", "MustStartOn", "MustFinishOn", "StartNoEarlierThan", "StartNoLaterThan", "FinishNoEarlierThan", "FinishNoLaterThan"] },
  { id: "constraintDate", labelKey: "columns.constraintDate", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 120, minWidth: 92, maxWidth: 160 },
  { id: "deadline", labelKey: "columns.deadline", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160 },
  { id: "id", labelKey: "columns.id", categoryKey: "columnCategories.identity", filterType: "number", initialWidth: 52, minWidth: 40, maxWidth: 84, required: true, defaultVisible: true },
  { id: "wbs", labelKey: "columns.wbs", categoryKey: "columnCategories.identity", filterType: "text", initialWidth: 112, minWidth: 84, maxWidth: 180, defaultVisible: false },
  { id: "outlineLevel", labelKey: "columns.outlineLevel", categoryKey: "columnCategories.identity", filterType: "number", initialWidth: 92, minWidth: 72, maxWidth: 130, defaultVisible: false },
  { id: "taskName", labelKey: "columns.taskName", categoryKey: "columnCategories.identity", filterType: "text", initialWidth: 260, minWidth: 180, maxWidth: 520, required: true, defaultVisible: true },
  { id: "type", labelKey: "columns.type", categoryKey: "columnCategories.identity", filterType: "enum", initialWidth: 100, minWidth: 84, maxWidth: 150, enumValues: ["Task", "Milestone", "WBS"], defaultVisible: false },
  { id: "duration", labelKey: "columns.duration", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 86, minWidth: 64, maxWidth: 140, defaultVisible: true },
  { id: "start", labelKey: "columns.start", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160, defaultVisible: true },
  { id: "finish", labelKey: "columns.finish", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160, defaultVisible: true },
  { id: "calendar", labelKey: "columns.calendar", categoryKey: "columnCategories.schedule", filterType: "text", initialWidth: 150, minWidth: 110, maxWidth: 260, defaultVisible: false },
  { id: "actualStart", labelKey: "columns.actualStart", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160, defaultVisible: false },
  { id: "actualFinish", labelKey: "columns.actualFinish", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160, defaultVisible: false },
  { id: "baselineStart", labelKey: "columns.baselineStart", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160, defaultVisible: false },
  { id: "baselineFinish", labelKey: "columns.baselineFinish", categoryKey: "columnCategories.schedule", filterType: "date", initialWidth: 112, minWidth: 92, maxWidth: 160, defaultVisible: false },
  { id: "baselineDuration", labelKey: "columns.baselineDuration", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 112, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "startVariance", labelKey: "columns.startVariance", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 104, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "finishVariance", labelKey: "columns.finishVariance", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 104, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "durationVariance", labelKey: "columns.durationVariance", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 112, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "totalSlack", labelKey: "columns.totalSlack", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 104, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "freeSlack", labelKey: "columns.freeSlack", categoryKey: "columnCategories.schedule", filterType: "number", initialWidth: 96, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "critical", labelKey: "columns.critical", categoryKey: "columnCategories.schedule", filterType: "enum", initialWidth: 90, minWidth: 72, maxWidth: 130, enumValues: ["Yes", "No"], defaultVisible: false },
  { id: "progress", labelKey: "columns.progress", categoryKey: "columnCategories.progress", filterType: "number", initialWidth: 92, minWidth: 72, maxWidth: 140, defaultVisible: true },
  { id: "predecessors", labelKey: "columns.predecessors", categoryKey: "columnCategories.links", filterType: "text", initialWidth: 160, minWidth: 120, maxWidth: 300, defaultVisible: true },
  { id: "resources", labelKey: "columns.resources", categoryKey: "columnCategories.resources", filterType: "text", initialWidth: 180, minWidth: 120, maxWidth: 320, defaultVisible: false },
  { id: "estimateHours", labelKey: "columns.estimateHours", categoryKey: "columnCategories.progress", filterType: "number", initialWidth: 110, minWidth: 84, maxWidth: 160, defaultVisible: false },
  { id: "description", labelKey: "columns.description", categoryKey: "columnCategories.metadata", filterType: "text", initialWidth: 240, minWidth: 160, maxWidth: 420, defaultVisible: false },
];

export const DEFAULT_PLANNING_COLUMN_IDS: PlanningColumnId[] = PLANNING_COLUMN_DEFINITIONS.filter((column) => column.defaultVisible).map((column) => column.id);

export function getPlanningColumnDefinition(id: PlanningColumnId) {
  return PLANNING_COLUMN_DEFINITIONS.find((column) => column.id === id) ?? PLANNING_COLUMN_DEFINITIONS[0];
}

export function getPlanningColumnValue(id: PlanningColumnId, row: PlanWorkspaceRow, context: PlanningColumnValueContext): string | number | undefined {
  const activity = row.activity;
  switch (id) {
    case "id": return row.rowNumber;
    case "wbs": return row.wbs?.code ?? activity?.code;
    case "outlineLevel": return row.depth + 1;
    case "taskName": return activity?.name ?? row.wbs?.name;
    case "type": return activity?.type ?? (row.wbs ? "WBS" : undefined);
    case "schedulingMode": return activity?.schedulingMode;
    case "schedulingStatus": {
      if (!activity) return undefined;
      const statuses = [activity.schedulingMode === "Manual" ? "Manual" : "Automatic"];
      if (activity.constraintApplied) statuses.push("Constraint");
      if (activity.deadlineExceeded) statuses.push("DeadlineExceeded");
      if (activity.schedulingConflict) statuses.push("SchedulingConflict");
      if (activity.critical) statuses.push("Critical");
      if (context.predecessorText(activity.id)) statuses.push("Predecessors");
      if ((context.successorCountByActivity.get(activity.id) ?? 0) > 0) statuses.push("Successors");
      if (activity.calendarOverride) statuses.push("CalendarOverride");
      return statuses.join(" ");
    }
    case "constraintType": return activity?.constraintType;
    case "constraintDate": return activity?.constraintDate?.slice(0, 10);
    case "deadline": return activity?.deadline?.slice(0, 10);
    case "duration": return activity?.duration;
    case "start": return activity?.startDate?.slice(0, 10);
    case "finish": return activity?.endDate?.slice(0, 10);
    case "calendar": return activity ? (activity.calendarId ? (context.calendarNames.get(activity.calendarId) ?? activity.calendarId) : context.defaultCalendarName) : undefined;
    case "actualStart": return activity?.actualStart?.slice(0, 10);
    case "actualFinish": return activity?.actualFinish?.slice(0, 10);
    case "baselineStart": return activity?.baselineStartDate?.slice(0, 10);
    case "baselineFinish": return activity?.baselineFinishDate?.slice(0, 10);
    case "baselineDuration": return activity?.baselineDuration ?? undefined;
    case "startVariance": return activity?.startVarianceDays ?? undefined;
    case "finishVariance": return activity?.finishVarianceDays ?? undefined;
    case "durationVariance": return activity?.durationVarianceDays ?? undefined;
    case "totalSlack": return activity?.totalSlack ?? undefined;
    case "freeSlack": return activity?.freeSlack ?? undefined;
    case "critical": return activity ? (activity.critical ? "Yes" : "No") : undefined;
    case "progress": return activity?.progress ?? undefined;
    case "predecessors": return activity ? context.predecessorText(activity.id) : undefined;
    case "resources": return activity ? (context.assignmentsByActivity.get(activity.id) ?? []).map((assignment) => context.resourceNames.get(assignment.resourceId) ?? assignment.resourceId.slice(0, 8)).join(", ") : undefined;
    case "estimateHours": return activity?.estimateHours ?? undefined;
    case "description": return activity?.description;
  }
}

export function matchesPlanningColumnFilter(value: string | number | undefined, filter: PlanningColumnFilter | undefined, type: PlanningColumnFilterType): boolean {
  if (!filter || (!filter.text && !filter.min && !filter.max && !filter.value)) return true;
  if (value === undefined || value === null || value === "") return false;
  if (type === "number") {
    const numeric = typeof value === "number" ? value : Number(value);
    if (!Number.isFinite(numeric)) return false;
    if (filter.min && numeric < Number(filter.min)) return false;
    if (filter.max && numeric > Number(filter.max)) return false;
    return true;
  }
  if (type === "date") {
    const date = String(value).slice(0, 10);
    return (!filter.min || date >= filter.min) && (!filter.max || date <= filter.max);
  }
  if (type === "enum") return !filter.value || String(value) === filter.value;
  return !filter.text || String(value).toLocaleLowerCase().includes(filter.text.toLocaleLowerCase());
}
