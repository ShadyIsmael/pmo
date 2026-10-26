import { MarkerType, type Edge, type Node } from "@xyflow/react";
import type {
  PlanActivityDto,
  PlanActivitySchedulingMetricsDto,
  PlanDependencyDto,
  PlanSchedulingAnalysisDto,
  PlanWbsNodeDto,
} from "../api/planningFull";

export interface CriticalPathActivityNodeData extends Record<string, unknown> {
  activity: PlanActivityDto;
  metrics?: PlanActivitySchedulingMetricsDto;
  wbsPath?: string;
  critical: boolean;
  highlightCritical: boolean;
  selectedDependencies: boolean;
  scheduleTooltip: string;
  textDirection: "rtl" | "ltr";
  labels: {
    task: string;
    milestone: string;
    summary: string;
    duration: string;
    start: string;
    finish: string;
    totalFloat: string;
    progress: string;
    earlyStart: string;
    earlyFinish: string;
    lateStart: string;
    lateFinish: string;
    days: string;
  };
  formatDate: (value: string) => string;
  formatNumber: (value: number) => string;
}

export interface CriticalPathDependencyEdgeData extends Record<string, unknown> {
  dependency: PlanDependencyDto;
  critical: boolean;
  selected: boolean;
  label: string;
}

export type CriticalPathActivityNode = Node<CriticalPathActivityNodeData, "activity" | "milestone">;
export type CriticalPathDependencyEdge = Edge<CriticalPathDependencyEdgeData>;

export interface CriticalPathNetworkGraph {
  nodes: CriticalPathActivityNode[];
  edges: CriticalPathDependencyEdge[];
  hasUnscheduledActivities: boolean;
  hasMissingDependencyData: boolean;
  orphanDependencyIds: string[];
  analysisUsable: boolean;
}

interface BuildGraphOptions {
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  wbs: PlanWbsNodeDto[];
  analysis?: PlanSchedulingAnalysisDto;
  selectedActivityId?: string;
  highlightCritical: boolean;
  highlightSelectedDependencies: boolean;
  textDirection: "rtl" | "ltr";
  dependencyLabel: (type: PlanDependencyDto["type"]) => string;
  labels: CriticalPathActivityNodeData["labels"];
  formatDate: (value: string) => string;
  formatNumber: (value: number) => string;
}

export function buildCriticalPathNetworkGraph(options: BuildGraphOptions): CriticalPathNetworkGraph {
  const { activities, dependencies, wbs, analysis } = options;
  const activityById = new Map(activities.map((activity) => [activity.id, activity]));
  const validDependencies = dependencies.filter((dependency) =>
    activityById.has(dependency.predecessorActivityId) && activityById.has(dependency.activityId));
  const orphanDependencyIds = dependencies
    .filter((dependency) => !activityById.has(dependency.predecessorActivityId) || !activityById.has(dependency.activityId))
    .map((dependency) => dependency.id);
  const wbsPaths = flattenWbsPaths(wbs);
  const metricsByActivityId = new Map((analysis?.activities ?? []).map((metrics) => [metrics.activityId, metrics]));
  const criticalActivityIds = new Set(analysis?.criticalPathActivityIds ?? []);
  const criticalDependencyIds = new Set(analysis?.criticalPathDependencyIds ?? []);
  const positions = layoutNetwork(activities, validDependencies);
  const hasUnscheduledActivities = activities.some((activity) => !activity.startDate || !activity.endDate);
  const hasMissingDependencyData = activities.length > 1 && dependencies.length === 0;
  const analysisUsable = Boolean(analysis && !analysis.hasCycle && activities.length > 0 &&
    !hasUnscheduledActivities && !hasMissingDependencyData && orphanDependencyIds.length === 0);

  const nodes = activities.map((activity): CriticalPathActivityNode => {
    const metrics = metricsByActivityId.get(activity.id);
    const hasDates = Boolean(activity.startDate && activity.endDate);
    const critical = Boolean(analysisUsable && hasDates &&
      (criticalActivityIds.has(activity.id) || metrics?.critical));
    const titleParts = [
      `${options.labels.earlyStart}: ${metrics?.earlyStart ? options.formatDate(metrics.earlyStart) : "—"}`,
      `${options.labels.earlyFinish}: ${metrics?.earlyFinish ? options.formatDate(metrics.earlyFinish) : "—"}`,
      `${options.labels.lateStart}: ${metrics?.lateStart ? options.formatDate(metrics.lateStart) : "—"}`,
      `${options.labels.lateFinish}: ${metrics?.lateFinish ? options.formatDate(metrics.lateFinish) : "—"}`,
    ];
    return {
      id: activity.id,
      type: activity.type === "Milestone" ? "milestone" : "activity",
      position: positions.get(activity.id) ?? { x: 0, y: 0 },
      data: {
        activity,
        metrics,
        wbsPath: activity.wbsId ? wbsPaths.get(activity.wbsId) : undefined,
        critical,
        highlightCritical: options.highlightCritical,
        selectedDependencies: options.highlightSelectedDependencies && activity.id === options.selectedActivityId,
        scheduleTooltip: titleParts.join(" · "),
        textDirection: options.textDirection,
        labels: options.labels,
        formatDate: options.formatDate,
        formatNumber: options.formatNumber,
      },
    };
  });

  const edges = validDependencies.map((dependency): CriticalPathDependencyEdge => {
    const lag = dependency.lag ?? 0;
    const lagText = lag === 0 ? "" : ` ${lag > 0 ? "+" : ""}${lag}`;
    const critical = Boolean(analysisUsable && criticalDependencyIds.has(dependency.id));
    const selected = options.highlightSelectedDependencies && Boolean(options.selectedActivityId) &&
      (dependency.predecessorActivityId === options.selectedActivityId || dependency.activityId === options.selectedActivityId);
    const stroke = critical && options.highlightCritical ? "hsl(var(--destructive))" : selected ? "hsl(var(--primary))" : "hsl(var(--border))";
    return {
      id: dependency.id,
      source: dependency.predecessorActivityId,
      target: dependency.activityId,
      type: "smoothstep",
      markerEnd: { type: MarkerType.ArrowClosed, color: stroke },
      label: `${options.dependencyLabel(dependency.type)}${lagText}`,
      data: { dependency, critical, selected, label: `${options.dependencyLabel(dependency.type)}${lagText}` },
      style: {
        stroke,
        strokeWidth: critical && options.highlightCritical ? 3 : selected ? 2.5 : 1.5,
      },
      labelStyle: { fill: "hsl(var(--foreground))", fontSize: 10 },
      labelBgStyle: { fill: "hsl(var(--background))", fillOpacity: 0.94 },
      interactionWidth: 18,
    };
  });

  return {
    nodes,
    edges,
    hasUnscheduledActivities,
    hasMissingDependencyData,
    orphanDependencyIds,
    analysisUsable,
  };
}

function flattenWbsPaths(nodes: PlanWbsNodeDto[]): Map<string, string> {
  const paths = new Map<string, string>();
  const visit = (items: PlanWbsNodeDto[], parentPath: string) => {
    for (const item of items) {
      const path = parentPath ? `${parentPath} › ${item.code}` : item.code;
      paths.set(item.id, path);
      visit(item.children ?? [], path);
    }
  };
  visit(nodes, "");
  return paths;
}

function layoutNetwork(activities: PlanActivityDto[], dependencies: PlanDependencyDto[]): Map<string, { x: number; y: number }> {
  const activityIds = new Set(activities.map((activity) => activity.id));
  const activityById = new Map(activities.map((activity) => [activity.id, activity]));
  const incoming = new Map(activities.map((activity) => [activity.id, 0]));
  const outgoing = new Map<string, string[]>();
  for (const dependency of dependencies) {
    if (!activityIds.has(dependency.predecessorActivityId) || !activityIds.has(dependency.activityId)) continue;
    incoming.set(dependency.activityId, (incoming.get(dependency.activityId) ?? 0) + 1);
    const successors = outgoing.get(dependency.predecessorActivityId) ?? [];
    successors.push(dependency.activityId);
    outgoing.set(dependency.predecessorActivityId, successors);
  }

  const stableOrder = (leftId: string, rightId: string) => {
    const left = activityById.get(leftId)!;
    const right = activityById.get(rightId)!;
    return (left.sortOrder ?? Number.MAX_SAFE_INTEGER) - (right.sortOrder ?? Number.MAX_SAFE_INTEGER) ||
      (left.startDate ?? "").localeCompare(right.startDate ?? "") || left.code.localeCompare(right.code);
  };
  const ranks = new Map(activities.map((activity) => [activity.id, 0]));
  const queue = [...incoming.entries()].filter(([, count]) => count === 0).map(([id]) => id).sort(stableOrder);
  for (const successors of outgoing.values()) successors.sort(stableOrder);
  for (let index = 0; index < queue.length; index += 1) {
    const current = queue[index];
    for (const successor of outgoing.get(current) ?? []) {
      ranks.set(successor, Math.max(ranks.get(successor) ?? 0, (ranks.get(current) ?? 0) + 1));
      const remaining = (incoming.get(successor) ?? 0) - 1;
      incoming.set(successor, remaining);
      if (remaining === 0) queue.push(successor);
    }
  }

  const levels = new Map<number, PlanActivityDto[]>();
  for (const activity of activities) {
    const rank = ranks.get(activity.id) ?? 0;
    const level = levels.get(rank) ?? [];
    level.push(activity);
    levels.set(rank, level);
  }
  const positions = new Map<string, { x: number; y: number }>();
  for (const [rank, level] of levels) {
    level.sort((left, right) => stableOrder(left.id, right.id));
    level.forEach((activity, row) => positions.set(activity.id, { x: rank * 360, y: row * 190 }));
  }
  return positions;
}
