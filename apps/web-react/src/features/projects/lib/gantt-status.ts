import { PLANNING_ACTIVITY_TYPE } from "@lib/enums";
import type { PlanWorkspaceRow } from "./plan-workspace-rows";

export type GanttStatus = "notStarted" | "inProgress" | "completed" | "milestone" | "summary";

export const GANTT_STATUS_STYLES: Record<GanttStatus, { bar: string; selected: string }> = {
  notStarted: { bar: "bg-muted-foreground/55", selected: "ring-2 ring-primary/60" },
  inProgress: { bar: "bg-primary", selected: "ring-2 ring-primary/70" },
  completed: { bar: "bg-success", selected: "ring-2 ring-primary/70" },
  milestone: { bar: "bg-warning", selected: "ring-2 ring-primary/70" },
  summary: { bar: "bg-foreground/80", selected: "ring-2 ring-primary/70" },
};

export function getGanttStatus(row: PlanWorkspaceRow): GanttStatus {
  if (row.activity?.type === PLANNING_ACTIVITY_TYPE.milestone) return "milestone";
  if (row.isSummary || row.hasChildren || (row.wbs && !row.activity)) return "summary";
  const progress = row.activity?.progress;
  if (progress != null && progress >= 100) return "completed";
  if (progress != null && progress > 0) return "inProgress";
  return "notStarted";
}
