import * as React from "react";
import {
  Background,
  Controls,
  Handle,
  Position,
  ReactFlow,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  useReactFlow,
  type NodeProps,
  type NodeTypes,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { Activity, Download, GitBranch, LayoutGrid, Maximize2, Search, ZoomIn, ZoomOut } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { useI18n } from "@i18n/messages";
import { formatNumberValue } from "@lib/formatting/number";
import { CriticalPathExportDialog } from "./CriticalPathExportDialog";
import type {
  PlanActivityDto,
  PlanDependencyDto,
  PlanSchedulingAnalysisDto,
  PlanWbsNodeDto,
} from "../api/planningFull";
import {
  buildCriticalPathNetworkGraph,
  type CriticalPathActivityNode,
  type CriticalPathDependencyEdge,
} from "../lib/critical-path-network";
import { roundPlanProgress } from "../lib/plan-progress";
import { CRITICAL_PATH_EXPORT_FALLBACK_NODE_DIMENSIONS, type CriticalPathExportSnapshot } from "../lib/critical-path-export";

interface Props {
  projectId: string;
  projectName: string;
  projectCode: string;
  planId: string;
  planName: string;
  workingVersionId: string;
  workingVersionNumber: number;
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  wbs: PlanWbsNodeDto[];
  analysis?: PlanSchedulingAnalysisDto;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onOpenDetails: (activity: PlanActivityDto) => void;
  onOpenInGrid: (activityId: string) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
}

const nodeTypes = { activity: ActivityNodeRenderer, milestone: MilestoneNodeRenderer } satisfies NodeTypes;

export function CriticalPathNetworkDiagram(props: Props) {
  return <ReactFlowProvider><NetworkCanvas {...props} /></ReactFlowProvider>;
}

function NetworkCanvas({ projectId, projectName, projectCode, planId, planName, workingVersionId, workingVersionNumber, activities, dependencies, wbs, analysis, isLoading, isError, onRetry, onOpenDetails, onOpenInGrid, t }: Props) {
  const { locale, formatDate } = useI18n();
  const tr = React.useCallback((key: string, params?: Record<string, string | number>) => t(`projects.planning.${key}`, params), [t]);
  const flow = useReactFlow<CriticalPathActivityNode, CriticalPathDependencyEdge>();
  const [highlightCritical, setHighlightCritical] = React.useState(true);
  const [criticalOnly, setCriticalOnly] = React.useState(false);
  const [highlightSelectedDependencies, setHighlightSelectedDependencies] = React.useState(true);
  const [selectedActivityId, setSelectedActivityId] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [draggedPositions, setDraggedPositions] = React.useState<Record<string, { x: number; y: number }>>({});
  const [exportOpen, setExportOpen] = React.useState(false);
  const [exportSnapshot, setExportSnapshot] = React.useState<CriticalPathExportSnapshot | null>(null);
  const formatWesternNumber = React.useCallback((value: number) =>
    formatNumberValue(value, locale, { maximumFractionDigits: 2 }), [locale]);
  const labels = React.useMemo(() => ({
    task: tr("network.task"),
    milestone: tr("network.milestone"),
    summary: tr("network.summary"),
    duration: tr("network.duration"),
    start: tr("network.start"),
    finish: tr("network.finish"),
    totalFloat: tr("network.totalFloat"),
    progress: tr("network.progress"),
    earlyStart: tr("network.earlyStart"),
    earlyFinish: tr("network.earlyFinish"),
    lateStart: tr("network.lateStart"),
    lateFinish: tr("network.lateFinish"),
    days: tr("durationDays"),
  }), [tr]);
  const formatNetworkDate = React.useCallback((value: string) => formatDate(value, { numberingSystem: "latn" }), [formatDate]);
  const graph = React.useMemo(() => buildCriticalPathNetworkGraph({
    activities,
    dependencies,
    wbs,
    analysis,
    selectedActivityId,
    highlightCritical,
    highlightSelectedDependencies,
    textDirection: locale === "ar" ? "rtl" : "ltr",
    dependencyLabel: (type) => {
      const translated = t(`enums.planning.dependencyType.${type}`);
      return translated === `enums.planning.dependencyType.${type}` ? type : translated;
    },
    labels,
    formatDate: formatNetworkDate,
    formatNumber: formatWesternNumber,
  }), [activities, dependencies, wbs, analysis, selectedActivityId, highlightCritical, highlightSelectedDependencies, t, labels, formatNetworkDate, formatWesternNumber, locale]);
  const [nodes, setNodes, onNodesChange] = useNodesState<CriticalPathActivityNode>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState(graph.edges);
  React.useEffect(() => {
    setNodes(graph.nodes.map((node) => ({ ...node, position: draggedPositions[node.id] ?? node.position })));
    setEdges(graph.edges);
  }, [graph.nodes, graph.edges, draggedPositions, setNodes, setEdges]);

  const selectedActivity = activities.find((activity) => activity.id === selectedActivityId);
  const matchingActivities = React.useMemo(() => {
    const query = search.trim().toLocaleLowerCase(locale);
    if (!query) return [];
    return activities.filter((activity) => `${activity.code} ${activity.name}`.toLocaleLowerCase(locale).includes(query)).slice(0, 6);
  }, [activities, search, locale]);
  const networkUsable = graph.analysisUsable && Boolean(analysis) && !analysis?.hasCycle;
  const visibleNodes = React.useMemo(() => nodes.filter((node) => !criticalOnly || (networkUsable && node.data.critical)), [nodes, criticalOnly, networkUsable]);
  const visibleNodeIds = React.useMemo(() => new Set(visibleNodes.map((node) => node.id)), [visibleNodes]);
  const visibleEdges = React.useMemo(() => edges.filter((edge) => visibleNodeIds.has(edge.source) && visibleNodeIds.has(edge.target)), [edges, visibleNodeIds]);

  const focusActivity = React.useCallback((activity: PlanActivityDto) => {
    setSelectedActivityId(activity.id);
    setSearch(`${activity.code} · ${activity.name}`);
    const node = nodes.find((item) => item.id === activity.id);
    if (node) flow.setCenter(node.position.x + 112, node.position.y + 72, { zoom: 1.08, duration: 350 });
  }, [flow, nodes]);
  const openGrid = () => { if (selectedActivity) onOpenInGrid(selectedActivity.id); };
  const autoLayout = () => {
    setDraggedPositions({});
    window.requestAnimationFrame(() => void flow.fitView({ padding: 0.18, duration: 300 }));
  };
  const fitToScreen = () => void flow.fitView({ padding: 0.18, duration: 300 });
  const isEmpty = !isLoading && !isError && activities.length === 0;
  const openExport = () => {
    const clone = <T,>(value: T): T => JSON.parse(JSON.stringify(value)) as T;
    const layoutNodes = nodes.length === graph.nodes.length ? nodes : graph.nodes;
    setExportSnapshot({
      projectId,
      projectName,
      projectCode,
      planId,
      planName,
      planVersionId: workingVersionId,
      planVersionNumber: workingVersionNumber,
      activities: clone(activities),
      dependencies: clone(dependencies),
      wbs: clone(wbs),
      layout: layoutNodes.map((node) => {
        const fallback = node.type === "milestone"
          ? CRITICAL_PATH_EXPORT_FALLBACK_NODE_DIMENSIONS.milestone
          : CRITICAL_PATH_EXPORT_FALLBACK_NODE_DIMENSIONS.activity;
        return {
          activityId: node.id,
          x: node.position.x,
          y: node.position.y,
          width: node.measured?.width ?? node.width ?? fallback.width,
          height: node.measured?.height ?? node.height ?? fallback.height,
        };
      }),
      analysis: analysis ? clone(analysis) : undefined,
      exportedAt: new Date().toISOString(),
    });
    setExportOpen(true);
  };

  return <section className="m-3 overflow-hidden rounded-lg border bg-background" aria-label={tr("network.title")}>
    <div className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/20 px-3 py-2">
      <div className="flex min-w-0 items-center gap-2">
        <GitBranch className="h-4 w-4 shrink-0 text-primary" />
        <div className="min-w-0"><h2 className="truncate text-sm font-semibold">{tr("network.title")}</h2><p className="truncate text-xs text-muted-foreground">{tr("network.subtitle")}</p></div>
      </div>
      <div className="flex flex-wrap items-center gap-1.5">
        <div className="relative w-64 max-w-full">
          <Search className="pointer-events-none absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input type="search" value={search} onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => {
            if (event.key === "Enter" && matchingActivities[0]) { event.preventDefault(); focusActivity(matchingActivities[0]); }
          }} placeholder={tr("network.searchTask")} aria-label={tr("network.searchTask")} className="h-9 ps-8 text-xs" />
          {matchingActivities.length > 0 && search !== `${matchingActivities[0].code} · ${matchingActivities[0].name}` ? <div className="absolute inset-x-0 top-full z-20 mt-1 max-h-52 overflow-auto rounded-md border bg-popover p-1 shadow-md">
            {matchingActivities.map((activity) => <button key={activity.id} type="button" className="flex w-full flex-col items-start rounded px-2 py-1.5 text-start text-xs hover:bg-accent" onClick={() => focusActivity(activity)}><span className="font-medium">{activity.code}</span><span className="text-muted-foreground">{activity.name}</span></button>)}
          </div> : null}
        </div>
        <Button type="button" size="sm" variant={highlightCritical ? "secondary" : "outline"} className="h-9 text-xs" aria-pressed={highlightCritical} onClick={() => setHighlightCritical((value) => !value)}>{tr("network.highlightCritical")}</Button>
        <Button type="button" size="sm" variant={criticalOnly ? "secondary" : "outline"} className="h-9 text-xs" disabled={!networkUsable} aria-pressed={criticalOnly} onClick={() => setCriticalOnly((value) => !value)}>{criticalOnly ? tr("network.showAll") : tr("network.criticalOnly")}</Button>
        <Button type="button" size="sm" variant={highlightSelectedDependencies ? "secondary" : "outline"} className="h-9 text-xs" aria-pressed={highlightSelectedDependencies} onClick={() => setHighlightSelectedDependencies((value) => !value)}>{tr("network.highlightDependencies")}</Button>
        <Button type="button" size="sm" variant="outline" className="h-9 gap-1.5 text-xs" disabled={isLoading} onClick={openExport}><Download className="h-3.5 w-3.5" />{tr("exportDiagram.open")}</Button>
        <Button type="button" size="icon" variant="outline" className="h-9 w-9" title={tr("network.zoomOut")} aria-label={tr("network.zoomOut")} onClick={() => void flow.zoomOut({ duration: 180 })}><ZoomOut className="h-4 w-4" /></Button>
        <Button type="button" size="icon" variant="outline" className="h-9 w-9" title={tr("network.zoomIn")} aria-label={tr("network.zoomIn")} onClick={() => void flow.zoomIn({ duration: 180 })}><ZoomIn className="h-4 w-4" /></Button>
        <Button type="button" size="icon" variant="outline" className="h-9 w-9" title={tr("network.fitToScreen")} aria-label={tr("network.fitToScreen")} onClick={fitToScreen}><Maximize2 className="h-4 w-4" /></Button>
        <Button type="button" size="sm" variant="outline" className="h-9 gap-1 text-xs" onClick={autoLayout}><LayoutGrid className="h-3.5 w-3.5" />{tr("network.autoLayout")}</Button>
      </div>
    </div>

    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b px-3 py-1.5 text-[11px] text-muted-foreground">
      <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm border-2 border-destructive" />{tr("network.criticalActivity")}</span>
      <span className="inline-flex items-center gap-1.5"><span className="h-0.5 w-5 bg-destructive" />{tr("network.criticalDependency")}</span>
      <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm border border-border" />{tr("network.activity")}</span>
      <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rotate-45 border border-border" />{tr("network.milestone")}</span>
      <span className="ms-auto inline-flex items-center gap-1"><Activity className="h-3.5 w-3.5" />{tr("network.activityCount", { count: activities.length })}</span>
      <span>{tr("network.dependencyCount", { count: dependencies.length })}</span>
      {analysis?.projectFinishDate ? <span>{tr("network.projectFinish")}: {formatNetworkDate(analysis.projectFinishDate)}</span> : null}
      {networkUsable ? <span className="text-destructive">{tr("network.criticalCount", { count: analysis?.criticalPathActivityIds.length ?? 0 })}</span> : null}
    </div>

    {!isLoading && analysis?.hasCycle ? <Diagnostic>{tr("network.cycleDetected")}</Diagnostic> : null}
    {!isLoading && graph.hasUnscheduledActivities ? <Diagnostic>{tr("network.unscheduledActivities")}</Diagnostic> : null}
    {!isLoading && graph.hasMissingDependencyData ? <Diagnostic>{tr("network.missingDependencies")}</Diagnostic> : null}
    {!isLoading && graph.orphanDependencyIds.length > 0 ? <Diagnostic>{tr("network.invalidDependencies", { count: graph.orphanDependencyIds.length })}</Diagnostic> : null}
    {isError ? <Diagnostic destructive><span>{tr("network.analysisFailed")}</span><Button type="button" size="sm" variant="outline" className="h-7" onClick={onRetry}>{t("common.retry")}</Button></Diagnostic> : null}
    {!isLoading && !isError && analysis && !analysis.hasCycle && !networkUsable && !graph.hasUnscheduledActivities && !graph.hasMissingDependencyData && graph.orphanDependencyIds.length === 0 ? <Diagnostic>{tr("network.noUsableSchedule")}</Diagnostic> : null}
    {!isError && !analysis && !isLoading && activities.length > 0 ? <Diagnostic>{tr("network.analysisUnavailable")}</Diagnostic> : null}
    {selectedActivity ? <div className="flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2 text-xs">
      <span className="min-w-0 truncate font-medium">{selectedActivity.code} · {selectedActivity.name}</span>
      <div className="flex gap-1"><Button type="button" size="sm" variant="outline" className="h-7" onClick={() => onOpenDetails(selectedActivity)}>{tr("network.openDetails")}</Button><Button type="button" size="sm" variant="outline" className="h-7" onClick={openGrid}>{tr("network.openInGrid")}</Button></div>
    </div> : null}

    {isLoading ? <div className="flex h-[min(68vh,760px)] min-h-[34rem] items-center justify-center text-sm text-muted-foreground" role="status">{t("common.loading")}</div> : isEmpty ? <div className="flex h-[min(68vh,760px)] min-h-[34rem] items-center justify-center text-sm text-muted-foreground">{tr("emptyWorkspace")}</div> : <div className="h-[min(68vh,760px)] min-h-[34rem]" dir="ltr" aria-label={tr("network.canvasLabel")}>
      <ReactFlow<CriticalPathActivityNode, CriticalPathDependencyEdge>
        nodes={visibleNodes}
        edges={visibleEdges}
        nodeTypes={nodeTypes}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={(_, node) => setSelectedActivityId(node.id)}
        onNodeDoubleClick={(_, node) => { const activity = activities.find((item) => item.id === node.id); if (activity) onOpenDetails(activity); }}
        onNodeDragStop={(_, node) => setDraggedPositions((current) => ({ ...current, [node.id]: node.position }))}
        nodesConnectable={false}
        elementsSelectable
        nodesDraggable
        fitView
        fitViewOptions={{ padding: 0.18, minZoom: 0.25, maxZoom: 1.2 }}
        minZoom={0.12}
        maxZoom={2}
        panOnDrag
        zoomOnScroll
        proOptions={{ hideAttribution: false }}
      >
        <Background gap={22} size={1} color="hsl(var(--border))" />
        <Controls position="bottom-right" showInteractive={false} />
      </ReactFlow>
    </div>}
    <CriticalPathExportDialog open={exportOpen} onOpenChange={setExportOpen} snapshot={exportSnapshot} t={t} />
  </section>;
}

function ActivityNodeRenderer(props: NodeProps<CriticalPathActivityNode>) {
  return <ActivityNodeContent {...props} milestone={false} />;
}

function MilestoneNodeRenderer(props: NodeProps<CriticalPathActivityNode>) {
  return <ActivityNodeContent {...props} milestone />;
}

export function ActivityNodeContent({ data, selected, milestone }: NodeProps<CriticalPathActivityNode> & { milestone: boolean }) {
  const { activity, metrics, labels } = data;
  const criticalClass = data.critical && data.highlightCritical ? "border-destructive text-destructive" : "border-border text-foreground";
  const selectionClass = selected || data.selectedDependencies ? "ring-2 ring-primary ring-offset-2 ring-offset-background" : "";
  const float = metrics?.totalSlack === undefined || metrics.totalSlack === null ? "—" : `${data.formatNumber(metrics.totalSlack)} ${labels.days}`;
  const progressValue = roundPlanProgress(activity.progress);
  const progress = progressValue === null ? "—" : `${data.formatNumber(progressValue)}%`;
  const kindLabel = milestone ? labels.milestone : activity.isSummaryTask ? labels.summary : labels.task;
  const summaryClass = activity.isSummaryTask ? "border-dashed" : "";
  const body = <div dir={data.textDirection} className={`relative z-10 ${milestone ? "w-[9.5rem]" : "w-[13.5rem]"} rounded-md border bg-card px-2.5 py-2 text-start shadow-sm ${criticalClass} ${selectionClass} ${summaryClass}`} title={data.scheduleTooltip} data-testid={milestone ? "critical-path-milestone-node" : "critical-path-activity-node"}>
    <Handle type="target" position={Position.Left} className="!h-2 !w-2 !border-0 !bg-muted-foreground" />
    <div className="mb-1 flex items-center justify-between gap-2"><span className={`truncate text-[9px] font-semibold uppercase tracking-wide ${data.critical && data.highlightCritical ? "text-destructive" : "text-muted-foreground"}`}>{kindLabel}</span><span dir="ltr" className="shrink-0 text-[9px] font-semibold tabular-nums">{activity.code}</span></div>
    <p className="line-clamp-2 min-h-8 text-[11px] font-semibold leading-4" title={activity.name}>{activity.name}</p>
    {data.wbsPath ? <p className="mt-0.5 truncate text-[9px] text-muted-foreground" title={data.wbsPath}>{data.wbsPath}</p> : null}
    <div className="mt-1.5 grid grid-cols-2 gap-x-2 gap-y-1 border-t pt-1.5 text-[9px] leading-3">
      <NodeMetric label={labels.duration} value={`${data.formatNumber(activity.duration)} ${labels.days}`} />
      <NodeMetric label={labels.totalFloat} value={float} critical={data.critical && data.highlightCritical && metrics?.totalSlack === 0} />
      <NodeMetric label={labels.start} value={activity.startDate ? data.formatDate(activity.startDate) : "—"} />
      <NodeMetric label={labels.finish} value={activity.endDate ? data.formatDate(activity.endDate) : "—"} />
      <NodeMetric label={labels.progress} value={progress} />
    </div>
    <Handle type="source" position={Position.Right} className="!h-2 !w-2 !border-0 !bg-muted-foreground" />
  </div>;

  if (!milestone) return body;
  return <div className="relative flex h-[11rem] w-[13rem] items-center justify-center" data-testid="critical-path-milestone-shape">
    <div className={`absolute h-[9.5rem] w-[9.5rem] rotate-45 rounded-sm border-2 bg-card ${data.critical && data.highlightCritical ? "border-destructive" : "border-border"}`} />
    {body}
  </div>;
}

function NodeMetric({ label, value, critical = false }: { label: string; value: string; critical?: boolean }) {
  return <div className="min-w-0"><p className="truncate text-[8px] text-muted-foreground">{label}</p><p className={`truncate font-medium tabular-nums ${critical ? "text-destructive" : "text-foreground"}`} title={value}>{value}</p></div>;
}

function Diagnostic({ children, destructive = false }: { children: React.ReactNode; destructive?: boolean }) {
  return <div role={destructive ? "alert" : "status"} className={`flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2 text-xs ${destructive ? "text-destructive" : "text-amber-700 dark:text-amber-400"}`}>{children}</div>;
}
