import { getSmoothStepPath, Position } from "@xyflow/react";
import type {
  PlanActivityDto,
  PlanActivitySchedulingMetricsDto,
  PlanDependencyDto,
  PlanSchedulingAnalysisDto,
  PlanWbsNodeDto,
} from "../api/planningFull";
import { roundPlanProgress } from "./plan-progress";

export type CriticalPathExportFormat = "pdf" | "svg" | "png";
export type CriticalPathExportScope = "full" | "critical";
export type CriticalPathPdfSize = "AUTO" | "A4" | "A3" | "A2" | "A1" | "A0" | "CUSTOM";
export type CriticalPathPdfLayout = "fit" | "detailed";
export type CriticalPathOrientation = "auto" | "landscape" | "portrait";

export interface CriticalPathExportOptions {
  format: CriticalPathExportFormat;
  scope: CriticalPathExportScope;
  pageSize: CriticalPathPdfSize;
  orientation: CriticalPathOrientation;
  pdfLayout: CriticalPathPdfLayout;
  pngScale: 1 | 2 | 3;
  showTaskDates: boolean;
  showDuration: boolean;
  showTotalFloat: boolean;
  showProgress: boolean;
  showDependencyLabels: boolean;
  includeLegend: boolean;
  includeSummary: boolean;
}

export const DEFAULT_CRITICAL_PATH_EXPORT_OPTIONS: CriticalPathExportOptions = {
  format: "pdf",
  scope: "full",
  pageSize: "AUTO",
  orientation: "auto",
  pdfLayout: "fit",
  pngScale: 2,
  showTaskDates: true,
  showDuration: true,
  showTotalFloat: true,
  showProgress: true,
  showDependencyLabels: true,
  includeLegend: true,
  includeSummary: true,
};

export interface CriticalPathExportSnapshot {
  projectId: string;
  projectName: string;
  projectCode: string;
  planId: string;
  planName: string;
  planVersionId: string;
  planVersionNumber: number;
  activities: readonly PlanActivityDto[];
  dependencies: readonly PlanDependencyDto[];
  wbs: readonly PlanWbsNodeDto[];
  layout: readonly CriticalPathExportNodeLayout[];
  analysis?: PlanSchedulingAnalysisDto;
  exportedAt: string;
}

export interface CriticalPathExportNodeLayout {
  activityId: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface CriticalPathExportLabels {
  title: string;
  subtitle: string;
  project: string;
  plan: string;
  workingVersion: string;
  exportedAt: string;
  activities: string;
  dependencies: string;
  criticalActivities: string;
  criticalPaths: string;
  projectFinish: string;
  task: string;
  summary: string;
  milestone: string;
  critical: string;
  nonCritical: string;
  taskId: string;
  wbs: string;
  name: string;
  duration: string;
  start: string;
  finish: string;
  totalFloat: string;
  progress: string;
  days: string;
  planned: string;
  dependency: string;
  criticalDependency: string;
  normalDependency: string;
  criticalMilestone: string;
  normalMilestone: string;
  fullNetwork: string;
  criticalOnly: string;
  page: string;
  of: string;
  pageGuide: string;
  tile: string;
  row: string;
  column: string;
  pageActivities: string;
  crossPageConnectors: string;
  connection: string;
  from: string;
  to: string;
  type: string;
  lagLead: string;
  sourcePage: string;
  targetPage: string;
  noLag: string;
  legend: string;
  projectFinishUnavailable: string;
  direction: "ltr" | "rtl";
  dependencyType: Record<PlanDependencyDto["type"], string>;
  dependencyShortCode: Record<PlanDependencyDto["type"], string>;
  formatDate: (value: string) => string;
  formatNumber: (value: number) => string;
}

export type CriticalPathExportErrorCode =
  | "analysisUnavailable"
  | "cycle"
  | "unscheduledActivities"
  | "missingDependencies"
  | "invalidDependencies"
  | "staleSnapshot"
  | "noCriticalPath"
  | "invalidCriticalPath"
  | "emptyNetwork"
  | "tooLarge"
  | "fitTooSmall"
  | "pngTooLarge"
  | "pageTooLarge"
  | "invalidData"
  | "cancelled";

export class CriticalPathExportError extends Error {
  constructor(public readonly code: CriticalPathExportErrorCode, message?: string) {
    super(message ?? code);
    this.name = "CriticalPathExportError";
  }
}

export interface ExportNode {
  activity: PlanActivityDto;
  metrics?: PlanActivitySchedulingMetricsDto;
  wbsPath?: string;
  critical: boolean;
  width: number;
  height: number;
  x: number;
  y: number;
  nameLines: string[];
  wbsLines: string[];
  metricRows: Array<{ label: string; value: string }>;
  cardX: number;
  cardY: number;
  cardWidth: number;
  cardHeight: number;
}

export interface ExportEdge {
  dependency: PlanDependencyDto;
  critical: boolean;
  label: string;
  source: ExportNode;
  target: ExportNode;
}

export interface PreparedCriticalPathExport {
  snapshot: CriticalPathExportSnapshot;
  options: CriticalPathExportOptions;
  labels: CriticalPathExportLabels;
  nodes: ExportNode[];
  edges: ExportEdge[];
  criticalActivityCount: number;
  criticalPathCount: string;
  width: number;
  height: number;
  minX: number;
  minY: number;
}

const NODE_WIDTH = 216;
const ACTIVITY_FALLBACK_HEIGHT = 152;
const MILESTONE_WIDTH = 208;
const MILESTONE_HEIGHT = 176;
const MILESTONE_CARD_HEIGHT = 152;
const MILESTONE_DIAMOND_SIDE = 152;
const GRAPH_EDGE_PADDING = 32;
const PDF_MIN_READABLE_SCALE = 0.8;
const PDF_AUTO_CUSTOM_TARGET_SCALE = 0.9;
const MAX_GRAPH_NODES = 5_000;
const MAX_GRAPH_EDGES = 15_000;
const MAX_SVG_BYTES = 35 * 1024 * 1024;
const MAX_PNG_SIDE = 16_384;
const MAX_PNG_PIXELS = 40_000_000;
const MAX_PDF_PAGES = 250;
const COLORS = {
  critical: "#b91c1c",
  criticalFill: "#FEF2F2",
  normal: "#697586",
  normalFill: "#F8FAFC",
  text: "#202833",
  muted: "#697586",
  border: "#DDE3E9",
  background: "#ffffff",
  blue: "#2469A8",
};

export const CRITICAL_PATH_EXPORT_FALLBACK_NODE_DIMENSIONS = {
  activity: { width: NODE_WIDTH, height: ACTIVITY_FALLBACK_HEIGHT },
  milestone: { width: MILESTONE_WIDTH, height: MILESTONE_HEIGHT },
} as const;

export function prepareCriticalPathExport(
  snapshot: CriticalPathExportSnapshot,
  options: CriticalPathExportOptions,
  labels: CriticalPathExportLabels,
): PreparedCriticalPathExport {
  const analysis = snapshot.analysis;
  if (!analysis) throw new CriticalPathExportError("analysisUnavailable");
  if (analysis.hasCycle) throw new CriticalPathExportError("cycle");
  if (!snapshot.projectId || !snapshot.planId || !snapshot.planVersionId || !snapshot.projectName || !snapshot.planName) {
    throw new CriticalPathExportError("staleSnapshot");
  }
  if (!snapshot.activities.length) throw new CriticalPathExportError("emptyNetwork");
  if (snapshot.activities.length > MAX_GRAPH_NODES || snapshot.dependencies.length > MAX_GRAPH_EDGES) {
    throw new CriticalPathExportError("tooLarge");
  }
  if (analysis.planVersionId !== snapshot.planVersionId ||
    snapshot.activities.some((item) => item.planVersionId !== snapshot.planVersionId) ||
    snapshot.dependencies.some((item) => item.planVersionId !== snapshot.planVersionId) ||
    flattenWbs(snapshot.wbs).some((item) => item.planVersionId !== snapshot.planVersionId)) {
    throw new CriticalPathExportError("staleSnapshot");
  }

  const activityById = new Map<string, PlanActivityDto>();
  for (const activity of snapshot.activities) {
    if (activityById.has(activity.id)) throw new CriticalPathExportError("invalidData");
    activityById.set(activity.id, activity);
  }
  const dependencyById = new Map<string, PlanDependencyDto>();
  for (const dependency of snapshot.dependencies) {
    if (dependencyById.has(dependency.id)) throw new CriticalPathExportError("invalidData");
    dependencyById.set(dependency.id, dependency);
  }
  if (snapshot.activities.some((item) => !item.startDate || !item.endDate)) {
    throw new CriticalPathExportError("unscheduledActivities");
  }
  if (snapshot.activities.length > 1 && snapshot.dependencies.length === 0) {
    throw new CriticalPathExportError("missingDependencies");
  }
  if (snapshot.dependencies.some((item) => !activityById.has(item.predecessorActivityId) || !activityById.has(item.activityId))) {
    throw new CriticalPathExportError("invalidDependencies");
  }
  if (snapshot.layout.length !== snapshot.activities.length) throw new CriticalPathExportError("invalidData");
  const layoutByActivityId = new Map<string, CriticalPathExportNodeLayout>();
  for (const layout of snapshot.layout) {
    if (layoutByActivityId.has(layout.activityId) || !activityById.has(layout.activityId) ||
      !Number.isFinite(layout.x) || !Number.isFinite(layout.y) ||
      !Number.isFinite(layout.width) || !Number.isFinite(layout.height) || layout.width <= 0 || layout.height <= 0) {
      throw new CriticalPathExportError("invalidData");
    }
    layoutByActivityId.set(layout.activityId, layout);
  }

  const canonicalCriticalActivities = new Set(analysis.criticalPathActivityIds);
  for (const metric of analysis.activities) if (metric.critical) canonicalCriticalActivities.add(metric.activityId);
  const canonicalCriticalDependencies = new Set(analysis.criticalPathDependencyIds);
  if (!canonicalCriticalActivities.size) throw new CriticalPathExportError("noCriticalPath");
  if ([...canonicalCriticalActivities].some((id) => !activityById.has(id)) ||
    [...canonicalCriticalDependencies].some((id) => {
      const dependency = dependencyById.get(id);
      return !dependency || !canonicalCriticalActivities.has(dependency.predecessorActivityId) || !canonicalCriticalActivities.has(dependency.activityId);
    })) {
    throw new CriticalPathExportError("invalidCriticalPath");
  }

  const criticalPathCount = countCanonicalCriticalPaths(
    canonicalCriticalActivities,
    [...canonicalCriticalDependencies].map((id) => dependencyById.get(id)!),
  );
  const selectedActivities = snapshot.activities.filter((item) => options.scope === "full" || canonicalCriticalActivities.has(item.id));
  const selectedDependencies = snapshot.dependencies.filter((item) => options.scope === "full" || canonicalCriticalDependencies.has(item.id));
  const selectedMetrics = new Map(analysis.activities.map((item) => [item.activityId, item]));
  const wbsPaths = flattenWbsPaths(snapshot.wbs);
  const positioned = selectedActivities.map((activity): ExportNode => {
    const metrics = selectedMetrics.get(activity.id);
    const flowLayout = layoutByActivityId.get(activity.id)!;
    const isMilestone = activity.type === "Milestone";
    const cardWidth = isMilestone ? NODE_WIDTH : flowLayout.width;
    const cardHeight = isMilestone ? Math.min(MILESTONE_CARD_HEIGHT, flowLayout.height) : flowLayout.height;
    const cardX = flowLayout.x + (flowLayout.width - cardWidth) / 2;
    const cardY = flowLayout.y + (flowLayout.height - cardHeight) / 2;
    const metricRows: Array<{ label: string; value: string }> = [];
    if (options.showDuration) metricRows.push({ label: labels.duration, value: `${labels.formatNumber(activity.duration)} ${labels.days}` });
    if (options.showTotalFloat) {
      const totalFloat = metrics?.totalSlack ?? activity.totalSlack;
      metricRows.push({ label: labels.totalFloat, value: totalFloat == null ? "â€”" : `${labels.formatNumber(totalFloat)} ${labels.days}` });
    }
    if (options.showTaskDates) {
      metricRows.push({ label: labels.start, value: labels.formatDate(activity.startDate!) });
      metricRows.push({ label: labels.finish, value: labels.formatDate(activity.endDate!) });
    }
    if (options.showProgress) {
      const progress = roundPlanProgress(activity.progress);
      metricRows.push({ label: labels.progress, value: progress == null ? "â€”" : `${labels.formatNumber(progress)}%` });
    }
    const wbsPath = activity.wbsId ? wbsPaths.get(activity.wbsId) : undefined;
    const wrappedName = wrapText(cleanText(activity.name), Math.max(12, Math.floor((cardWidth - 28) / 6.4)));
    const nameLines = wrappedName.length > 2 ? [...wrappedName.slice(0, 1), `${wrappedName[1].slice(0, -1)}â€¦`] : wrappedName;
    const wbsLines = wbsPath ? [cleanText(wbsPath)] : [];
    return {
      activity,
      metrics,
      wbsPath,
      critical: canonicalCriticalActivities.has(activity.id),
      x: flowLayout.x,
      y: flowLayout.y,
      width: flowLayout.width,
      height: flowLayout.height,
      cardX,
      cardY,
      cardWidth,
      cardHeight,
      nameLines,
      wbsLines,
      metricRows,
    };
  });
  const nodeById = new Map(positioned.map((node) => [node.activity.id, node]));
  const criticalEdges = new Set(canonicalCriticalDependencies);
  const edges = selectedDependencies.map((dependency): ExportEdge => {
    const source = nodeById.get(dependency.predecessorActivityId);
    const target = nodeById.get(dependency.activityId);
    if (!source || !target) throw new CriticalPathExportError("invalidDependencies");
    const lag = dependency.lag ?? 0;
    const lagLabel = lag === 0 ? "" : ` ${lag > 0 ? "+" : "âˆ’"}${labels.formatNumber(Math.abs(lag))} ${labels.days}`;
    return { dependency, critical: criticalEdges.has(dependency.id), label: `${labels.dependencyType[dependency.type]}${lagLabel}`, source, target };
  });
  const bounds = graphBounds(positioned);
  return {
    snapshot,
    options,
    labels,
    nodes: positioned,
    edges,
    criticalActivityCount: canonicalCriticalActivities.size,
    criticalPathCount,
    width: bounds.width,
    height: bounds.height,
    minX: bounds.minX,
    minY: bounds.minY,
  };
}

function graphBounds(nodes: ExportNode[]): { minX: number; minY: number; width: number; height: number } {
  const diamondRadius = MILESTONE_DIAMOND_SIDE * Math.SQRT2 / 2;
  const extents = nodes.map((node) => {
    const centerX = node.x + node.width / 2;
    const centerY = node.y + node.height / 2;
    const isMilestone = node.activity.type === "Milestone";
    return {
      minX: Math.min(node.cardX, isMilestone ? centerX - diamondRadius : node.cardX),
      minY: Math.min(node.cardY, isMilestone ? centerY - diamondRadius : node.cardY),
      maxX: Math.max(node.cardX + node.cardWidth, isMilestone ? centerX + diamondRadius : node.cardX + node.cardWidth),
      maxY: Math.max(node.cardY + node.cardHeight, isMilestone ? centerY + diamondRadius : node.cardY + node.cardHeight),
    };
  });
  const minX = Math.min(...extents.map((extent) => extent.minX)) - GRAPH_EDGE_PADDING;
  const minY = Math.min(...extents.map((extent) => extent.minY)) - GRAPH_EDGE_PADDING;
  const maxX = Math.max(...extents.map((extent) => extent.maxX)) + GRAPH_EDGE_PADDING;
  const maxY = Math.max(...extents.map((extent) => extent.maxY)) + GRAPH_EDGE_PADDING;
  return { minX, minY, width: maxX - minX, height: maxY - minY };
}

function countCanonicalCriticalPaths(activityIds: Set<string>, dependencies: PlanDependencyDto[]): string {
  const incoming = new Map([...activityIds].map((id) => [id, 0]));
  const outgoing = new Map([...activityIds].map((id) => [id, [] as string[]]));
  for (const dependency of dependencies) {
    incoming.set(dependency.activityId, (incoming.get(dependency.activityId) ?? 0) + 1);
    outgoing.get(dependency.predecessorActivityId)!.push(dependency.activityId);
  }
  const queue = [...activityIds].filter((id) => incoming.get(id) === 0).sort();
  const pathCount = new Map<string, bigint>();
  for (const id of queue) pathCount.set(id, 1n);
  for (let index = 0; index < queue.length; index += 1) {
    const id = queue[index];
    for (const target of outgoing.get(id) ?? []) {
      pathCount.set(target, (pathCount.get(target) ?? 0n) + (pathCount.get(id) ?? 0n));
      const remaining = (incoming.get(target) ?? 0) - 1;
      incoming.set(target, remaining);
      if (remaining === 0) queue.push(target);
    }
  }
  if (queue.length !== activityIds.size) throw new CriticalPathExportError("invalidCriticalPath");
  const sinks = [...activityIds].filter((id) => (outgoing.get(id)?.length ?? 0) === 0);
  const total = sinks.reduce((sum, id) => sum + (pathCount.get(id) ?? 0n), 0n);
  return total.toString();
}

function flattenWbs(items: readonly PlanWbsNodeDto[]): PlanWbsNodeDto[] {
  const result: PlanWbsNodeDto[] = [];
  const visit = (nodes: readonly PlanWbsNodeDto[]) => nodes.forEach((node) => { result.push(node); visit(node.children ?? []); });
  visit(items);
  return result;
}

function flattenWbsPaths(items: readonly PlanWbsNodeDto[]): Map<string, string> {
  const paths = new Map<string, string>();
  const visit = (nodes: readonly PlanWbsNodeDto[], parentPath: string) => nodes.forEach((node) => {
    const path = parentPath ? `${parentPath} â€º ${node.code}` : node.code;
    paths.set(node.id, path);
    visit(node.children ?? [], path);
  });
  visit(items, "");
  return paths;
}

function cleanText(value: string): string {
  return Array.from(value).filter((character) => {
    const codePoint = character.codePointAt(0)!;
    return !(codePoint <= 0x0008 || codePoint === 0x000b || codePoint === 0x000c ||
      codePoint >= 0x000e && codePoint <= 0x001f || codePoint >= 0x007f && codePoint <= 0x009f ||
      codePoint >= 0x202a && codePoint <= 0x202e || codePoint >= 0x2066 && codePoint <= 0x2069);
  }).join("").trim();
}

function escapeXml(value: string): string {
  return cleanText(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&apos;");
}

function wrapText(value: string, maxCharacters: number): string[] {
  const words = value.split(/\s+/).filter(Boolean);
  if (!words.length) return ["â€”"];
  const lines: string[] = [];
  let current = "";
  for (const word of words) {
    const points = Array.from(word);
    const chunks: string[] = [];
    while (points.length > maxCharacters) chunks.push(points.splice(0, maxCharacters).join(""));
    chunks.push(points.join(""));
    for (const chunk of chunks) {
      if (current && Array.from(`${current} ${chunk}`).length > maxCharacters) {
        lines.push(current);
        current = chunk;
      } else current = current ? `${current} ${chunk}` : chunk;
    }
  }
  if (current) lines.push(current);
  return lines.length ? lines : ["â€”"];
}

function textElement(text: string, x: number, y: number, options: { size?: number; color?: string; weight?: number; anchor?: "start" | "middle" | "end"; direction?: "ltr" | "rtl" } = {}): string {
  const { size = 11, color = COLORS.text, weight = 400, anchor = "start", direction = "ltr" } = options;
  return `<text x="${x}" y="${y}" font-family="${direction === "rtl" ? "Cairo" : "Roboto"},Arial,sans-serif" font-size="${size}" font-weight="${weight}" fill="${color}" text-anchor="${anchor}" direction="${direction}" unicode-bidi="plaintext">${escapeXml(text)}</text>`;
}

function linesMarkup(lines: string[], x: number, y: number, lineHeight: number, options: Parameters<typeof textElement>[3] = {}): string {
  return lines.map((line, index) => textElement(line, x, y + index * lineHeight, options)).join("");
}

function nodeTypeLabel(node: ExportNode, labels: CriticalPathExportLabels): string {
  return node.activity.type === "Milestone" ? labels.milestone : node.activity.isSummaryTask ? labels.summary : labels.task;
}

function renderNode(node: ExportNode, labels: CriticalPathExportLabels, options: CriticalPathExportOptions, xOffset = 0, yOffset = 0): string {
  const critical = node.critical;
  const border = critical ? COLORS.critical : COLORS.border;
  const direction = labels.direction;
  const isMilestone = node.activity.type === "Milestone";
  const x = node.cardX + xOffset;
  const y = node.cardY + yOffset;
  const width = node.cardWidth;
  const height = node.cardHeight;
  const textStartX = direction === "rtl" ? x + width - 11 : x + 11;
  const textEndX = direction === "rtl" ? x + 11 : x + width - 11;
  const textStartAnchor = direction === "rtl" ? "end" : "start";
  const textEndAnchor = direction === "rtl" ? "start" : "end";
  let markup = "";
  if (isMilestone) {
    const centerX = node.x + node.width / 2 + xOffset;
    const centerY = node.y + node.height / 2 + yOffset;
    const radius = MILESTONE_DIAMOND_SIDE * Math.SQRT2 / 2;
    markup += `<polygon points="${centerX},${centerY - radius} ${centerX + radius},${centerY} ${centerX},${centerY + radius} ${centerX - radius},${centerY}" fill="${COLORS.background}" stroke="${border}" stroke-width="${critical ? 3 : 1.5}"/>`;
  }
  const summaryDash = node.activity.isSummaryTask ? ' stroke-dasharray="5 3"' : "";
  markup += `<rect x="${x}" y="${y}" width="${width}" height="${height}" rx="8" fill="${COLORS.background}" stroke="${border}" stroke-width="${critical ? 2.5 : 1.25}"${summaryDash}/>`;
  markup += textElement(nodeTypeLabel(node, labels).toLocaleUpperCase(), textStartX, y + 15, { size: 8, color: critical ? COLORS.critical : COLORS.muted, weight: 700, anchor: textStartAnchor, direction });
  markup += textElement(cleanText(node.activity.code), textEndX, y + 15, { size: 8, color: COLORS.text, weight: 700, anchor: textEndAnchor, direction: "ltr" });
  markup += linesMarkup(node.nameLines, textStartX, y + 35, 13, { size: 10, color: COLORS.text, weight: 700, anchor: textStartAnchor, direction });
  const detailsTop = y + (node.wbsLines.length ? 76 : 62);
  if (node.wbsLines.length) markup += textElement(node.wbsLines[0], textStartX, y + 62, { size: 7.5, color: COLORS.muted, anchor: textStartAnchor, direction });
  markup += `<line x1="${x + 9}" y1="${detailsTop}" x2="${x + width - 9}" y2="${detailsTop}" stroke="${COLORS.border}" stroke-width="1"/>`;
  if (node.metricRows.length) {
    node.metricRows.forEach((metric, index) => {
      const col = index % 2;
      const row = Math.floor(index / 2);
      const cellX = col === 0 ? textStartX : textEndX;
      const cellAnchor = col === 0 ? textStartAnchor : textEndAnchor;
      const metricY = detailsTop + 14 + row * 23;
      markup += textElement(metric.label, cellX, metricY, { size: 6.5, color: COLORS.muted, anchor: cellAnchor, direction });
      markup += textElement(metric.value, cellX, metricY + 10, { size: 7.5, color: critical && metric.label === labels.totalFloat ? COLORS.critical : COLORS.text, weight: 600, anchor: cellAnchor, direction });
    });
  }
  return `<g data-activity-id="${escapeXml(node.activity.id)}" data-critical="${critical ? "true" : "false"}" data-kind="${isMilestone ? "milestone" : "task"}">${markup}</g>`;
}

function dependencyPath(edge: ExportEdge): { path: string; labelX: number; labelY: number } {
  const [path, labelX, labelY] = getSmoothStepPath({
    sourceX: edge.source.cardX + edge.source.cardWidth,
    sourceY: edge.source.cardY + edge.source.cardHeight / 2,
    sourcePosition: Position.Right,
    targetX: edge.target.cardX,
    targetY: edge.target.cardY + edge.target.cardHeight / 2,
    targetPosition: Position.Left,
  });
  return { path, labelX, labelY };
}

function renderEdge(edge: ExportEdge, labels: CriticalPathExportLabels, options: CriticalPathExportOptions): string {
  const color = edge.critical ? COLORS.critical : COLORS.normal;
  const geometry = dependencyPath(edge);
  let markup = `<path d="${geometry.path}" fill="none" stroke="${color}" stroke-width="${edge.critical ? 3 : 1.5}" marker-end="url(#${edge.critical ? "arrow-critical" : "arrow-normal"})"/>`;
  if (options.showDependencyLabels) {
    const label = edge.label;
    const labelWidth = Math.min(190, Math.max(38, Array.from(label).length * 5.8));
    markup += `<rect x="${geometry.labelX - labelWidth / 2}" y="${geometry.labelY - 10}" width="${labelWidth}" height="14" rx="3" fill="#ffffff" fill-opacity="0.96"/>`;
    markup += textElement(label, geometry.labelX, geometry.labelY, { size: 7.5, color, weight: edge.critical ? 700 : 500, anchor: "middle", direction: labels.direction });
  }
  return `<g data-dependency-id="${escapeXml(edge.dependency.id)}" data-critical="${edge.critical ? "true" : "false"}">${markup}</g>`;
}

function defs(): string {
  return `<defs><marker id="arrow-normal" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="${COLORS.normal}"/></marker><marker id="arrow-critical" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="${COLORS.critical}"/></marker></defs>`;
}

function renderLegend(labels: CriticalPathExportLabels, startY: number, width: number): string {
  const items: Array<{ kind: "box" | "diamond" | "line" | "metric"; label: string; critical?: boolean }> = [
    { kind: "box", label: `${labels.critical} ${labels.task}`, critical: true },
    { kind: "box", label: `${labels.nonCritical} ${labels.task}` },
    { kind: "diamond", label: labels.criticalMilestone, critical: true },
    { kind: "diamond", label: labels.normalMilestone },
    { kind: "line", label: labels.criticalDependency, critical: true },
    { kind: "line", label: labels.normalDependency },
    { kind: "metric", label: labels.totalFloat },
  ];
  const cols = 4;
  const cellWidth = width / cols;
  return `${textElement(labels.legend, 0, startY - 18, { size: 9, color: COLORS.blue, weight: 700, direction: labels.direction })}<line x1="0" y1="${startY - 12}" x2="${width}" y2="${startY - 12}" stroke="${COLORS.border}"/>${items.map((item, index) => {
    const row = Math.floor(index / cols);
    const col = index % cols;
    const x = col * cellWidth + 5;
    const y = startY + row * 24;
    const color = item.critical ? COLORS.critical : COLORS.normal;
    const symbol = item.kind === "box"
      ? `<rect x="${x}" y="${y - 9}" width="14" height="11" rx="2" fill="${item.critical ? COLORS.criticalFill : COLORS.normalFill}" stroke="${color}" stroke-width="${item.critical ? 2 : 1}"/>`
      : item.kind === "diamond"
        ? `<polygon points="${x + 7},${y - 11} ${x + 13},${y - 5} ${x + 7},${y + 1} ${x + 1},${y - 5}" fill="${item.critical ? COLORS.criticalFill : COLORS.normalFill}" stroke="${color}" stroke-width="${item.critical ? 2 : 1}"/>`
        : item.kind === "metric"
          ? `<rect x="${x}" y="${y - 10}" width="16" height="13" rx="3" fill="#F0F2F5" stroke="${COLORS.border}"/><text x="${x + 8}" y="${y}" text-anchor="middle" font-family="Roboto,Arial,sans-serif" font-size="6" font-weight="700" fill="${COLORS.muted}">TF</text>`
          : `<line x1="${x}" y1="${y - 4}" x2="${x + 17}" y2="${y - 4}" stroke="${color}" stroke-width="${item.critical ? 2.5 : 1.5}"/>`;
    return `${symbol}${textElement(item.label, x + 22, y, { size: 8, color: COLORS.muted, direction: labels.direction })}`;
  }).join("")}`;
}

function summaryLines(model: PreparedCriticalPathExport, labels: CriticalPathExportLabels): string[] {
  const { snapshot } = model;
  const formattedPathCount = new Intl.NumberFormat(labels.direction === "rtl" ? "ar-SA-u-nu-latn" : "en-US").format(BigInt(model.criticalPathCount));
  return [
    `${labels.project}: ${snapshot.projectName}${snapshot.projectCode ? ` (${snapshot.projectCode})` : ""}`,
    `${labels.plan}: ${snapshot.planName} Â· ${labels.workingVersion} ${snapshot.planVersionNumber}`,
    `${labels.exportedAt}: ${labels.formatDate(snapshot.exportedAt)}`,
    `${labels.activities}: ${labels.formatNumber(snapshot.activities.length)} Â· ${labels.dependencies}: ${labels.formatNumber(snapshot.dependencies.length)} Â· ${labels.criticalActivities}: ${labels.formatNumber(model.criticalActivityCount)} Â· ${labels.criticalPaths}: ${formattedPathCount}`,
    `${labels.projectFinish}: ${snapshot.analysis?.projectFinishDate ? labels.formatDate(snapshot.analysis.projectFinishDate) : labels.projectFinishUnavailable}`,
  ];
}

function svgRoot(width: number, height: number, content: string, direction: "ltr" | "rtl"): string {
  if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0 || width > 80_000 || height > 80_000) {
    throw new CriticalPathExportError("tooLarge");
  }
  return `<?xml version="1.0" encoding="UTF-8"?><svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" direction="${direction}"><rect x="0" y="0" width="100%" height="100%" fill="#ffffff"/>${defs()}${content}</svg>`;
}

function renderNetworkContent(model: PreparedCriticalPathExport, labels: CriticalPathExportLabels, options: CriticalPathExportOptions, tile?: { x: number; y: number; width: number; height: number }): string {
  const intersects = (left: number, top: number, right: number, bottom: number) => !tile ||
    (right >= tile.x && left <= tile.x + tile.width && bottom >= tile.y && top <= tile.y + tile.height);
  const visibleEdges = tile ? model.edges.filter((edge) => {
    const sourceX = edge.source.cardX + edge.source.cardWidth - model.minX;
    const sourceY = edge.source.cardY + edge.source.cardHeight / 2 - model.minY;
    const targetX = edge.target.cardX - model.minX;
    const targetY = edge.target.cardY + edge.target.cardHeight / 2 - model.minY;
    const routePadding = 32;
    return intersects(Math.min(sourceX, targetX) - routePadding, Math.min(sourceY, targetY) - routePadding,
      Math.max(sourceX, targetX) + routePadding, Math.max(sourceY, targetY) + routePadding);
  }) : model.edges;
  const visibleNodes = tile ? model.nodes.filter((node) => {
    const bounds = nodeTileBounds(model, node);
    return bounds.minX >= tile.x && bounds.minY >= tile.y && bounds.maxX <= tile.x + tile.width && bounds.maxY <= tile.y + tile.height;
  }) : model.nodes;
  const edgeMarkup = visibleEdges.map((edge) => renderEdge(edge, labels, options)).join("");
  const nodeMarkup = visibleNodes.map((node) => renderNode(node, labels, options)).join("");
  return edgeMarkup + nodeMarkup;
}

function renderNetworkSvgNatural(model: PreparedCriticalPathExport): string {
  const { labels, options } = model;
  const heading = options.includeSummary ? 22 + summaryLines(model, labels).length * 16 : 52;
  const graphOffsetX = 28;
  const graphOffsetY = heading + 24;
  const graphWidth = model.width + 2 * graphOffsetX;
  const legendHeight = options.includeLegend ? 68 : 0;
  const contentHeight = graphOffsetY + model.height + 28 + legendHeight;
  let markup = "";
  markup += textElement(labels.title, graphOffsetX, 24, { size: 18, color: COLORS.blue, weight: 700, direction: labels.direction });
  markup += textElement(options.scope === "full" ? labels.fullNetwork : labels.criticalOnly, graphOffsetX, 42, { size: 10, color: COLORS.muted, direction: labels.direction });
  if (options.includeSummary) {
    markup += summaryLines(model, labels).map((line, index) => textElement(line, graphOffsetX, 64 + index * 16, { size: 9, color: index === 0 ? COLORS.text : COLORS.muted, weight: index === 0 ? 600 : 400, direction: labels.direction })).join("");
  } else {
    markup += textElement(`${model.snapshot.projectName} Â· ${model.snapshot.planName} Â· ${labels.workingVersion} ${model.snapshot.planVersionNumber}`, graphOffsetX, 66, { size: 10, color: COLORS.muted, direction: labels.direction });
  }
  markup += `<g transform="translate(${graphOffsetX - model.minX} ${graphOffsetY - model.minY})">${renderNetworkContent(model, labels, options)}</g>`;
  if (options.includeLegend) markup += renderLegend(labels, contentHeight - legendHeight + 8, graphWidth);
  const svg = svgRoot(graphWidth, contentHeight, markup, labels.direction);
  assertSvgSize(svg);
  return svg;
}

function assertSvgSize(svg: string): void {
  if (new Blob([svg]).size > MAX_SVG_BYTES) throw new CriticalPathExportError("tooLarge");
}

export function buildCriticalPathSvg(model: PreparedCriticalPathExport): string {
  return renderNetworkSvgNatural(model);
}

export function criticalPathExportFilename(model: PreparedCriticalPathExport, extension: CriticalPathExportFormat): string {
  const safePart = (value: string, fallback: string) => value.normalize("NFKD").replace(/[^a-zA-Z0-9._-]+/g, "_").replace(/^\.+|\.+$/g, "") || fallback;
  return `NEXUS_Critical_Path_${safePart(model.snapshot.projectCode, "Project")}_${safePart(model.snapshot.planName, "Plan")}_v${model.snapshot.planVersionNumber}.${extension}`;
}

export interface CriticalPathPdfDocumentPlan {
  definition: Record<string, unknown>;
  pageCount: number;
  nodePages: Map<string, number>;
  crossPageConnections: Array<{ ref: string; edge: ExportEdge; sourcePage: number; targetPage: number }>;
}

type ResolvedPdfPageSize = Exclude<CriticalPathPdfSize, "AUTO">;
type ResolvedOrientation = Exclude<CriticalPathOrientation, "auto">;

interface PageDimensions {
  width: number;
  height: number;
  bodyWidth: number;
  bodyHeight: number;
  pageSize: ResolvedPdfPageSize;
  orientation: ResolvedOrientation;
}

const PDF_PAGE_MARGIN = 22;
const PDF_TILE_HEADER = 54;
const PDF_TILE_OVERLAP_X = NODE_WIDTH + 8;
const PDF_TILE_OVERLAP_Y = MILESTONE_DIAMOND_SIDE * Math.SQRT2 + 8;
const STANDARD_PDF_SIZES: Array<Exclude<ResolvedPdfPageSize, "CUSTOM">> = ["A4", "A3", "A2", "A1", "A0"];
const PDF_PORTRAIT_DIMENSIONS: Record<Exclude<ResolvedPdfPageSize, "CUSTOM">, { width: number; height: number }> = {
  A4: { width: 595.28, height: 841.89 },
  A3: { width: 841.89, height: 1190.55 },
  A2: { width: 1190.55, height: 1683.78 },
  A1: { width: 1683.78, height: 2383.94 },
  A0: { width: 2383.94, height: 3370.39 },
};

interface NaturalPageContentSize { width: number; height: number }

function naturalPageContentSize(model: PreparedCriticalPathExport): NaturalPageContentSize {
  const headingHeight = model.options.includeSummary ? 22 + summaryLines(model, model.labels).length * 16 : 52;
  return {
    width: model.width + 56,
    height: headingHeight + 24 + model.height + 28 + (model.options.includeLegend ? 68 : 0),
  };
}

function standardPageDimensions(size: Exclude<ResolvedPdfPageSize, "CUSTOM">, orientation: ResolvedOrientation): PageDimensions {
  const portrait = PDF_PORTRAIT_DIMENSIONS[size];
  const width = orientation === "landscape" ? portrait.height : portrait.width;
  const height = orientation === "landscape" ? portrait.width : portrait.height;
  return { width, height, bodyWidth: width - PDF_PAGE_MARGIN * 2, bodyHeight: height - PDF_PAGE_MARGIN * 2, pageSize: size, orientation };
}

function resolveOrientations(orientation: CriticalPathOrientation, natural: NaturalPageContentSize): ResolvedOrientation[] {
  if (orientation !== "auto") return [orientation];
  return natural.width >= natural.height ? ["landscape", "portrait"] : ["portrait", "landscape"];
}

function fitScale(natural: NaturalPageContentSize, page: PageDimensions): number {
  return Math.min(page.bodyWidth / natural.width, page.bodyHeight / natural.height);
}

function fitUtilization(natural: NaturalPageContentSize, page: PageDimensions): number {
  const scale = fitScale(natural, page);
  return natural.width * natural.height * scale * scale / (page.bodyWidth * page.bodyHeight);
}

function customPageDimensions(natural: NaturalPageContentSize, orientation: CriticalPathOrientation): PageDimensions {
  const resolvedOrientation: ResolvedOrientation = orientation === "auto"
    ? (natural.width >= natural.height ? "landscape" : "portrait")
    : orientation;
  const targetWidth = natural.width / PDF_AUTO_CUSTOM_TARGET_SCALE + PDF_PAGE_MARGIN * 2;
  const targetHeight = natural.height / PDF_AUTO_CUSTOM_TARGET_SCALE + PDF_PAGE_MARGIN * 2;
  const width = resolvedOrientation === "landscape" ? Math.max(targetWidth, targetHeight) : targetWidth;
  const height = resolvedOrientation === "landscape" ? targetHeight : Math.max(targetHeight, targetWidth);
  if (width > 12_000 || height > 12_000) throw new CriticalPathExportError("tooLarge");
  return { width, height, bodyWidth: width - PDF_PAGE_MARGIN * 2, bodyHeight: height - PDF_PAGE_MARGIN * 2, pageSize: "CUSTOM", orientation: resolvedOrientation };
}

function pdfDimensions(model: PreparedCriticalPathExport): PageDimensions {
  const { pageSize, orientation, pdfLayout } = model.options;
  const natural = naturalPageContentSize(model);
  if (pdfLayout === "detailed" && (pageSize === "AUTO" || pageSize === "CUSTOM")) {
    return standardPageDimensions("A3", orientation === "auto" ? "landscape" : orientation);
  }
  if (pageSize === "CUSTOM") return customPageDimensions(natural, orientation);
  const sizes = pageSize === "AUTO" ? STANDARD_PDF_SIZES : [pageSize];
  for (const size of sizes) {
    const candidates = resolveOrientations(orientation, natural).map((candidate) => standardPageDimensions(size, candidate));
    const best = candidates.sort((left, right) => fitScale(natural, right) - fitScale(natural, left))[0];
    if (pageSize !== "AUTO") return best;
    if (fitScale(natural, best) >= PDF_MIN_READABLE_SCALE) {
      return fitUtilization(natural, best) < 0.55 ? customPageDimensions(natural, orientation) : best;
    }
  }
  return customPageDimensions(natural, orientation);
}

function wrapSvgPage(width: number, height: number, content: string, direction: "ltr" | "rtl"): string {
  return svgRoot(width, height, content, direction);
}

function fitNaturalSvg(svg: string, naturalWidth: number, naturalHeight: number, boxWidth: number, boxHeight: number, minScale: number, direction: "ltr" | "rtl"): string {
  const scale = Math.min(boxWidth / naturalWidth, boxHeight / naturalHeight);
  if (scale < minScale) throw new CriticalPathExportError("fitTooSmall");
  const offsetX = (boxWidth - naturalWidth * scale) / 2;
  const offsetY = (boxHeight - naturalHeight * scale) / 2;
  const contentStart = svg.indexOf("<rect");
  const content = contentStart >= 0 ? svg.slice(contentStart) : svg;
  const innerWithoutRootBackground = content.replace(/^<rect[^>]*\/>/, "");
  const originalDefs = innerWithoutRootBackground.match(/<defs>[\s\S]*?<\/defs>/)?.[0] ?? "";
  const remaining = innerWithoutRootBackground.replace(originalDefs, "");
  return wrapSvgPage(boxWidth, boxHeight, `${originalDefs}<g transform="translate(${offsetX} ${offsetY}) scale(${scale})">${remaining.replace(/<\/svg>$/, "")}</g>`, direction);
}

interface DiagramTile { x: number; y: number; row: number; column: number }

function tileStarts(length: number, viewport: number, overlap: number): number[] {
  if (viewport <= 0 || overlap >= viewport) throw new CriticalPathExportError("pageTooLarge");
  if (length <= viewport) return [0];
  const stride = viewport - overlap;
  const starts = [0];
  while (starts[starts.length - 1] + viewport < length) {
    const next = Math.min(starts[starts.length - 1] + stride, length - viewport);
    if (next <= starts[starts.length - 1]) break;
    starts.push(next);
  }
  return starts;
}

function makeDiagramTiles(model: PreparedCriticalPathExport, dimensions: PageDimensions): { tiles: DiagramTile[]; width: number; height: number } {
  const width = dimensions.bodyWidth - 36;
  const headerHeight = PDF_TILE_HEADER + (model.options.includeSummary ? 48 : 0);
  const height = dimensions.bodyHeight - headerHeight - (model.options.includeLegend ? 64 : 12);
  if (width <= NODE_WIDTH * 2 || height <= MILESTONE_HEIGHT * 1.5) throw new CriticalPathExportError("pageTooLarge");
  const columns = tileStarts(model.width, width, Math.min(PDF_TILE_OVERLAP_X, width - 1));
  const rows = tileStarts(model.height, height, Math.min(PDF_TILE_OVERLAP_Y, height - 1));
  const candidateTiles = rows.flatMap((y, row) => columns.map((x, column) => ({ x, y, row, column })));
  const tiles = candidateTiles.filter((tile) => {
    const containsFullNode = model.nodes.some((node) => {
      const bounds = nodeTileBounds(model, node);
      return bounds.minX >= tile.x && bounds.maxX <= tile.x + width && bounds.minY >= tile.y && bounds.maxY <= tile.y + height;
    });
    const containsCompleteDependency = model.edges.some((edge) => {
      const sourceX = edge.source.cardX + edge.source.cardWidth - model.minX;
      const sourceY = edge.source.cardY + edge.source.cardHeight / 2 - model.minY;
      const targetX = edge.target.cardX - model.minX;
      const targetY = edge.target.cardY + edge.target.cardHeight / 2 - model.minY;
      return Math.min(sourceX, targetX) >= tile.x && Math.max(sourceX, targetX) <= tile.x + width &&
        Math.min(sourceY, targetY) >= tile.y && Math.max(sourceY, targetY) <= tile.y + height;
    });
    return containsFullNode || containsCompleteDependency;
  });
  if (!tiles.length) throw new CriticalPathExportError("invalidData");
  if (tiles.length > MAX_PDF_PAGES) throw new CriticalPathExportError("tooLarge");
  return { tiles, width, height };
}

function tileOwnerPage(model: PreparedCriticalPathExport, node: ExportNode, tiles: DiagramTile[], viewWidth: number, viewHeight: number): number {
  const bounds = nodeTileBounds(model, node);
  const index = tiles.findIndex((tile) => bounds.minX >= tile.x && bounds.maxX <= tile.x + viewWidth && bounds.minY >= tile.y && bounds.maxY <= tile.y + viewHeight);
  if (index < 0) throw new CriticalPathExportError("invalidData");
  return index + 1;
}

function nodeTileBounds(model: PreparedCriticalPathExport, node: ExportNode) {
  const isMilestone = node.activity.type === "Milestone";
  const radius = isMilestone ? MILESTONE_DIAMOND_SIDE * Math.SQRT2 / 2 : 0;
  const centerX = node.x + node.width / 2 - model.minX;
  const centerY = node.y + node.height / 2 - model.minY;
  return {
    minX: Math.min(node.cardX - model.minX, centerX - radius),
    minY: Math.min(node.cardY - model.minY, centerY - radius),
    maxX: Math.max(node.cardX + node.cardWidth - model.minX, centerX + radius),
    maxY: Math.max(node.cardY + node.cardHeight - model.minY, centerY + radius),
  };
}

function renderTiledPageSvg(model: PreparedCriticalPathExport, dimensions: PageDimensions, tile: DiagramTile, pageNumber: number, pageCount: number, viewWidth: number, viewHeight: number): string {
  const { labels, options } = model;
  let markup = textElement(`${labels.title} Â· ${labels.page} ${pageNumber} ${labels.of} ${pageCount}`, 18, 21, { size: 11, color: COLORS.blue, weight: 700, direction: labels.direction });
  markup += textElement(`${labels.tile} Â· ${labels.row} ${tile.row + 1} Â· ${labels.column} ${tile.column + 1}`, 18, 39, { size: 8, color: COLORS.muted, direction: labels.direction });
  if (options.includeSummary) {
    markup += summaryLines(model, labels).map((line, index) => textElement(line, 18, 54 + index * 11, { size: 7, color: index === 0 ? COLORS.text : COLORS.muted, weight: index === 0 ? 600 : 400, direction: labels.direction })).join("");
  } else {
    markup += textElement(`${model.snapshot.projectName} Â· ${model.snapshot.planName} Â· ${labels.workingVersion} ${model.snapshot.planVersionNumber}`, 18, 51, { size: 7.5, color: COLORS.muted, direction: labels.direction });
  }
  const graphTop = PDF_TILE_HEADER + (options.includeSummary ? 48 : 0);
  markup += `<svg x="18" y="${graphTop}" width="${viewWidth}" height="${viewHeight}" viewBox="${tile.x} ${tile.y} ${viewWidth} ${viewHeight}" preserveAspectRatio="none" overflow="hidden"><g transform="translate(${-model.minX} ${-model.minY})">${renderNetworkContent(model, labels, options, { ...tile, width: viewWidth, height: viewHeight })}</g></svg>`;
  if (options.includeLegend) markup += `<g transform="translate(18 0)">${renderLegend(labels, dimensions.bodyHeight - 48, dimensions.bodyWidth - 36)}</g>`;
  return wrapSvgPage(dimensions.bodyWidth, dimensions.bodyHeight, markup, labels.direction);
}

export function buildCriticalPathPdfDocumentPlan(model: PreparedCriticalPathExport): CriticalPathPdfDocumentPlan {
  const dimensions = pdfDimensions(model);
  const pageBreakContent: Array<Record<string, unknown>> = [];
  const nodePages = new Map<string, number>();
  let crossPageConnections: CriticalPathPdfDocumentPlan["crossPageConnections"] = [];
  let pageCount = 1;

  if (model.options.pdfLayout === "fit") {
    const naturalSvg = renderNetworkSvgNatural(model);
    const natural = naturalPageContentSize(model);
    const fitSvg = fitNaturalSvg(naturalSvg, natural.width, natural.height, dimensions.bodyWidth, dimensions.bodyHeight, PDF_MIN_READABLE_SCALE, model.labels.direction);
    pageBreakContent.push({ svg: fitSvg, width: dimensions.bodyWidth, height: dimensions.bodyHeight });
  } else {
    const grid = makeDiagramTiles(model, dimensions);
    pageCount = grid.tiles.length;
    model.nodes.forEach((node) => nodePages.set(node.activity.id, tileOwnerPage(model, node, grid.tiles, grid.width, grid.height)));
    crossPageConnections = model.edges.filter((edge) => nodePages.get(edge.source.activity.id) !== nodePages.get(edge.target.activity.id)).map((edge, index) => ({
      ref: `X-${String(index + 1).padStart(3, "0")}`,
      edge,
      sourcePage: nodePages.get(edge.source.activity.id)!,
      targetPage: nodePages.get(edge.target.activity.id)!,
    }));
    grid.tiles.forEach((tile, index) => pageBreakContent.push({
      svg: renderTiledPageSvg(model, dimensions, tile, index + 1, pageCount, grid.width, grid.height),
      width: dimensions.bodyWidth,
      height: dimensions.bodyHeight,
      ...(index < pageCount - 1 ? { pageBreak: "after" } : {}),
    }));
  }
  const fonts = model.labels.direction === "rtl" ? "Cairo" : "Roboto";
  return {
    definition: {
      pageSize: dimensions.pageSize === "CUSTOM" ? { width: dimensions.width, height: dimensions.height } : dimensions.pageSize,
      ...(dimensions.pageSize === "CUSTOM" ? {} : { pageOrientation: dimensions.orientation }),
      pageMargins: [22, 22, 22, 22],
      defaultStyle: { font: fonts },
      info: { title: `${model.labels.title} - ${model.snapshot.projectName}`, author: "NEXUS PMO" },
      content: pageBreakContent,
    },
    pageCount,
    nodePages,
    crossPageConnections,
  };
}

export async function buildCriticalPathPdfBlob(model: PreparedCriticalPathExport, signal?: AbortSignal): Promise<Blob> {
  throwIfAborted(signal);
  const plan = buildCriticalPathPdfDocumentPlan(model);
  throwIfAborted(signal);
  const pdfModule = await import("pdfmake-rtl/build/pdfmake.min.js");
  const pdfMake = pdfModule.default;
  const fonts = await import("pdfmake-rtl/build/vfs_fonts.js");
  pdfMake.addVirtualFileSystem(fonts.default);
  pdfMake.addFonts({
    Roboto: { normal: "Roboto-Regular.ttf", bold: "Roboto-Medium.ttf", italics: "Roboto-Italic.ttf", bolditalics: "Roboto-MediumItalic.ttf" },
    Cairo: { normal: "Cairo-Regular.ttf", bold: "Cairo-Bold.ttf", italics: "Cairo-Regular.ttf", bolditalics: "Cairo-Bold.ttf" },
  });
  const blob = await pdfMake.createPdf(plan.definition).getBlob();
  throwIfAborted(signal);
  return blob;
}

export async function downloadCriticalPathPdf(model: PreparedCriticalPathExport, signal?: AbortSignal): Promise<void> {
  const blob = await buildCriticalPathPdfBlob(model, signal);
  throwIfAborted(signal);
  downloadBlob(blob, criticalPathExportFilename(model, "pdf"));
}

export function downloadCriticalPathSvg(model: PreparedCriticalPathExport, signal?: AbortSignal): void {
  throwIfAborted(signal);
  const svg = buildCriticalPathSvg(model);
  throwIfAborted(signal);
  downloadBlob(new Blob([svg], { type: "image/svg+xml;charset=utf-8" }), criticalPathExportFilename(model, "svg"));
}

export async function downloadCriticalPathPng(model: PreparedCriticalPathExport, signal?: AbortSignal): Promise<void> {
  throwIfAborted(signal);
  const scale = model.options.pngScale;
  const width = Math.ceil(model.width + 56) * scale;
  const headingHeight = model.options.includeSummary ? 22 + summaryLines(model, model.labels).length * 16 : 52;
  const height = Math.ceil(headingHeight + 24 + model.height + 28 + (model.options.includeLegend ? 68 : 0)) * scale;
  if (width > MAX_PNG_SIDE || height > MAX_PNG_SIDE || width * height > MAX_PNG_PIXELS) {
    throw new CriticalPathExportError("pngTooLarge");
  }
  const svg = buildCriticalPathSvg(model);
  throwIfAborted(signal);
  const url = URL.createObjectURL(new Blob([svg], { type: "image/svg+xml;charset=utf-8" }));
  try {
    const image = await loadImage(url, signal);
    throwIfAborted(signal);
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) throw new CriticalPathExportError("pngTooLarge");
    context.fillStyle = COLORS.background;
    context.fillRect(0, 0, width, height);
    context.drawImage(image, 0, 0, width, height);
    const blob = await new Promise<Blob>((resolve, reject) => canvas.toBlob((result) => result ? resolve(result) : reject(new CriticalPathExportError("pngTooLarge")), "image/png"));
    throwIfAborted(signal);
    downloadBlob(blob, criticalPathExportFilename(model, "png"));
  } finally {
    URL.revokeObjectURL(url);
  }
}

function loadImage(url: string, signal?: AbortSignal): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image();
    const cleanup = () => { image.onload = null; image.onerror = null; signal?.removeEventListener("abort", abort); };
    const abort = () => { cleanup(); image.src = ""; reject(new CriticalPathExportError("cancelled")); };
    image.onload = () => { cleanup(); resolve(image); };
    image.onerror = () => { cleanup(); reject(new CriticalPathExportError("pngTooLarge")); };
    signal?.addEventListener("abort", abort, { once: true });
    image.src = url;
  });
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) throw new CriticalPathExportError("cancelled");
}

function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.rel = "noopener";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => { if (typeof URL.revokeObjectURL === "function") URL.revokeObjectURL(url); }, 1_000);
}

export function criticalPathExportErrorMessage(error: unknown, labels: Record<CriticalPathExportErrorCode, string>): string {
  if (error instanceof CriticalPathExportError) return labels[error.code];
  return labels.analysisUnavailable;
}

export function getCriticalPathPngLimits(): { maxSide: number; maxPixels: number } {
  return { maxSide: MAX_PNG_SIDE, maxPixels: MAX_PNG_PIXELS };
}
