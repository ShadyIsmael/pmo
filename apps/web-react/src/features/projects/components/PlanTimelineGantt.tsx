import * as React from "react";
import { AlertTriangle, Bot, CalendarDays, ChevronDown, ChevronRight, Diamond, Hand, Link2 } from "lucide-react";
import { PLANNING_ACTIVITY_TYPE, PLANNING_DEPENDENCY_TYPE } from "@lib/enums";
import { cn } from "@lib/utils";
import { useI18n } from "@i18n/messages";
import type { PlanDependencyDto } from "../api/planningFull";
import type { PlanWorkspaceRow } from "../lib/plan-workspace-rows";
import type { GanttZoom } from "../lib/gantt-constants";
import { PLAN_ROW_HEIGHT } from "../lib/gantt-constants";
import { GANTT_STATUS_STYLES, getGanttStatus } from "../lib/gantt-status";
import { roundPlanProgress } from "../lib/plan-progress";
import { planTextFormatClassName } from "../lib/plan-text-format";

const DAY_WIDTH: Record<GanttZoom, number> = { day: 24, week: 14, month: 6 };

function day(value?: string) {
  if (!value) return undefined;
  const parsed = new Date(`${value.slice(0, 10)}T00:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed;
}

function daysBetween(from: Date, to: Date) { return Math.round((to.getTime() - from.getTime()) / 86_400_000); }

export function PlanTimelineGantt({ rows, dependencies, selectedId, zoom = "day", collapsed, showBaseline = false, showIdentityPanel = true, onSelect, onToggleCollapse, onOpenDetails, onContextMenu }: {
  rows: PlanWorkspaceRow[];
  dependencies: PlanDependencyDto[];
  selectedId: string;
  zoom?: GanttZoom;
  collapsed?: ReadonlySet<string>;
  showBaseline?: boolean;
  /** False in split Grid+Gantt view (the planning grid already renders the identity panel). */
  showIdentityPanel?: boolean;
  onSelect: (id: string) => void;
  onToggleCollapse?: (row: PlanWorkspaceRow) => void;
  onOpenDetails?: (row: PlanWorkspaceRow) => void;
  onContextMenu?: (row: PlanWorkspaceRow, event: React.MouseEvent<HTMLButtonElement>) => void;
}) {
  const { t, locale } = useI18n();
  const tr = (key: string) => t(`projects.planning.${key}`);
  const rowRefs = React.useRef(new Map<string, HTMLButtonElement>());
  const dated = React.useMemo(() => rows.flatMap((row) => [day(row.activity?.startDate), day(row.activity?.endDate), ...(showBaseline ? [day(row.activity?.baselineStartDate), day(row.activity?.baselineFinishDate)] : []), day(row.activity?.deadline)].filter((value): value is Date => Boolean(value))), [rows, showBaseline]);
  const now = new Date();
  const today = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  const minimum = dated.length ? new Date(Math.min(...dated.map((value) => value.getTime()))) : today;
  const maximum = dated.length ? new Date(Math.max(...dated.map((value) => value.getTime()))) : new Date(today.getTime() + 13 * 86_400_000);
  const start = new Date(minimum.getTime() - 2 * 86_400_000);
  const timelineStartMs = start.getTime();
  const totalDays = Math.max(14, daysBetween(start, maximum) + 3);
  const dayWidth = DAY_WIDTH[zoom];
  const width = totalDays * dayWidth;
  const labels = React.useMemo(() => Array.from({ length: totalDays }, (_, index) => new Date(timelineStartMs + index * 86_400_000)), [timelineStartMs, totalDays]);
  const todayOffset = daysBetween(new Date(timelineStartMs), today) * dayWidth;
  const shortDateFormatter = React.useMemo(() => new Intl.DateTimeFormat(locale, { month: "short", day: "numeric" }), [locale]);
  const shortMonthFormatter = React.useMemo(() => new Intl.DateTimeFormat(locale, { month: "short" }), [locale]);
  const labelDateFormatter = React.useMemo(() => new Intl.DateTimeFormat(locale, { dateStyle: "medium" }), [locale]);
  const labelText = (label: Date, index: number) => {
    if (zoom === "day") return String(label.getUTCDate());
    if (zoom === "week") return label.getUTCDay() === 1 || index === 0 ? shortDateFormatter.format(label) : "";
    return label.getUTCDate() === 1 || index === 0 ? shortMonthFormatter.format(label) : "";
  };
  const summaryRanges = React.useMemo(() => {
    const ranges = new Map<string, { start?: Date; finish?: Date }>();
    for (let i = 0; i < rows.length; i++) {
      const row = rows[i];
      if (!row.isSummary) continue;
      let start: Date | undefined;
      let finish: Date | undefined;
      for (let j = i + 1; j < rows.length && rows[j].depth > row.depth; j++) {
        const childStart = day(rows[j].activity?.startDate);
        const childFinish = day(rows[j].activity?.endDate);
        if (childStart && (!start || childStart < start)) start = childStart;
        if (childFinish && (!finish || childFinish > finish)) finish = childFinish;
      }
      if (start || finish) ranges.set(row.id, { start, finish });
    }
    return ranges;
  }, [rows]);
  const metrics = React.useCallback((row: PlanWorkspaceRow) => {
    const fallbackRange = row.isSummary ? summaryRanges.get(row.id) : undefined;
    const startDate = day(row.activity?.startDate) ?? fallbackRange?.start;
    const endDate = day(row.activity?.endDate) ?? fallbackRange?.finish;
    const baselineStartDate = day(row.activity?.baselineStartDate);
    const baselineEndDate = day(row.activity?.baselineFinishDate);
    const deadlineDate = day(row.activity?.deadline);
    const left = startDate ? Math.max(0, daysBetween(new Date(timelineStartMs), startDate)) * dayWidth : 0;
    const barWidth = startDate ? Math.max(dayWidth, (daysBetween(startDate, endDate ?? startDate) + 1) * dayWidth) : 0;
    const baselineLeft = baselineStartDate ? Math.max(0, daysBetween(new Date(timelineStartMs), baselineStartDate)) * dayWidth : 0;
    const baselineBarWidth = baselineStartDate ? Math.max(dayWidth, (daysBetween(baselineStartDate, baselineEndDate ?? baselineStartDate) + 1) * dayWidth) : 0;
    const deadlineLeft = deadlineDate ? Math.max(0, daysBetween(new Date(timelineStartMs), deadlineDate)) * dayWidth + dayWidth / 2 : undefined;
    return { startDate, left, barWidth, baselineStartDate, baselineLeft, baselineBarWidth, deadlineLeft };
  }, [timelineStartMs, dayWidth, summaryRanges]);
  const metricsByRowId = React.useMemo(() => new Map(rows.map((row) => [row.id, metrics(row)])), [rows, metrics]);
  const rowIndexByActivityId = React.useMemo(() => new Map(rows.flatMap((row, index) => row.activity ? [[row.activity.id, index] as const] : [])), [rows]);
  const linkedActivityIds = React.useMemo(() => new Set(dependencies.flatMap((dependency) => [dependency.activityId, dependency.predecessorActivityId])), [dependencies]);

  React.useEffect(() => {
    if (!selectedId) return;
    rowRefs.current.get(selectedId)?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }, [selectedId]);

  const identity = (row: PlanWorkspaceRow) => row.activity?.name ?? row.wbs?.name ?? "—";
  return <div dir="ltr" className="min-w-max border-s bg-background text-[12px] leading-5" aria-label={tr("timeline")}>
    {showBaseline && <div className="flex items-center gap-4 border-b bg-muted/20 px-3 py-1.5 text-[11px] text-muted-foreground"><span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-7 rounded-full bg-primary/70" />{tr("currentSchedule")}</span><span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-7 rounded-sm border border-dashed border-foreground/60 bg-muted-foreground/35" />{tr("baselineSchedule")}</span></div>}
    <div className={cn("grid", showIdentityPanel ? "grid-cols-[minmax(15rem,20rem)_max-content]" : "grid-cols-1")}>
      {showIdentityPanel && <div className="sticky start-0 z-20 bg-background" dir="auto">
        <div className="sticky top-0 z-20 flex h-11 items-center border-b border-e bg-muted/80 px-3 text-[10px] font-medium uppercase tracking-wide text-muted-foreground backdrop-blur">{tr("taskIdentity")}</div>
        {rows.map((row) => <button key={row.id} type="button" data-testid="planning-gantt-row" data-row-kind={row.activity?.type === PLANNING_ACTIVITY_TYPE.milestone ? "milestone" : row.isSummary ? "summary" : "task"} onClick={() => onSelect(row.id)} onDoubleClick={() => onOpenDetails?.(row)} onContextMenu={(event) => { event.preventDefault(); onSelect(row.id); onContextMenu?.(row, event); }} className={cn("flex w-full items-center gap-1 border-b border-e px-2 text-start transition-colors hover:bg-primary/5", selectedId === row.id && "bg-primary/10 ring-2 ring-inset ring-primary", row.isSummary && selectedId !== row.id && "bg-muted/25")} style={{ height: PLAN_ROW_HEIGHT, paddingInlineStart: 8 + row.depth * 16 }} aria-label={identity(row)} aria-pressed={selectedId === row.id}>
          {row.hasChildren ? (() => {
            const wbsId = row.wbs?.id ?? row.activity?.wbsId;
            const isCollapsedRow = Boolean(wbsId && collapsed?.has(wbsId));
            return <span role="button" tabIndex={0} aria-label={isCollapsedRow ? tr("expand") : tr("collapse")} aria-expanded={!isCollapsedRow} onClick={(event) => { event.stopPropagation(); onToggleCollapse?.(row); }} onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); event.stopPropagation(); onToggleCollapse?.(row); } }} className="rounded p-0.5 hover:bg-muted">{isCollapsedRow ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}</span>;
          })() : <span className="w-4 shrink-0" />}
          <span className={cn("w-10 shrink-0 tabular-nums text-muted-foreground", row.isSummary && "font-semibold text-foreground")}>{row.outlineNumber ?? row.rowNumber}</span>
          {row.activity?.type === PLANNING_ACTIVITY_TYPE.milestone ? <Diamond className="h-3 w-3 shrink-0 fill-warning text-warning" role="img" aria-label={tr("milestone")} /> : null}
          <span className={cn("min-w-0 truncate text-[11px]", planTextFormatClassName(row.textFormat), row.isSummary && "font-semibold")}>{identity(row)}</span>
          {row.activity && <span className="ms-auto flex shrink-0 items-center gap-0.5" title={row.activity.schedulingMode === "Manual" ? tr("manual") : tr("automatic")}>
            {row.activity.schedulingMode === "Manual" ? <Hand className="h-3 w-3 text-warning" aria-hidden="true" /> : <Bot className="h-3 w-3 text-primary" aria-hidden="true" />}
            {row.activity.deadlineExceeded && <AlertTriangle className="h-3 w-3 text-destructive" aria-label={tr("deadlineExceeded")} />}
            {row.activity.constraintApplied && <span className="rounded border border-info/30 px-0.5 text-[8px] text-info" aria-label={tr("constraintType")}>C</span>}
            {row.activity.schedulingConflict && <AlertTriangle className="h-3 w-3 text-destructive" aria-label={tr("schedulingConflict")} />}
            {row.activity.calendarId && <CalendarDays className="h-3 w-3 text-muted-foreground" aria-label={tr("effectiveCalendar")} />}
            {row.activity && linkedActivityIds.has(row.activity.id) && <Link2 className="h-3 w-3 text-muted-foreground" aria-label={tr("predecessors")} />}
          </span>}
        </button>)}
      </div>}
      <div className="min-w-max">
        <div className="sticky top-0 z-10 flex h-11 border-b bg-muted/80 backdrop-blur">
          {labels.map((label, index) => <div key={label.toISOString()} className="flex shrink-0 items-center justify-center border-e text-[10px] font-normal text-muted-foreground" style={{ width: dayWidth }} title={labelDateFormatter.format(label)}>{labelText(label, index)}</div>)}
        </div>
        <div className="relative" style={{ width }}>
          <div className="pointer-events-none absolute inset-0 flex">{labels.map((label) => <div key={label.toISOString()} className={cn("shrink-0 border-e", label.getUTCDay() === 0 || label.getUTCDay() === 6 ? "bg-muted/40" : "")} style={{ width: dayWidth }} />)}</div>
          {todayOffset >= 0 && todayOffset <= width && <span className="pointer-events-none absolute inset-y-0 z-[3] border-s-2 border-dashed border-destructive/70" style={{ insetInlineStart: todayOffset }} title={tr("today")} aria-label={tr("today")} />}
          {rows.map((row) => {
            const { startDate, left, barWidth, baselineStartDate, baselineLeft, baselineBarWidth, deadlineLeft } = metricsByRowId.get(row.id)!;
            const status = getGanttStatus(row);
            const statusStyle = GANTT_STATUS_STYLES[status];
            const milestone = status === "milestone";
            const summary = status === "summary";
            const critical = Boolean(row.activity?.critical);
            const progress = roundPlanProgress(row.activity?.progress) ?? 0;
            return <button key={row.id} ref={(element) => { if (element) rowRefs.current.set(row.id, element); else rowRefs.current.delete(row.id); }} type="button" data-testid="planning-gantt-row" data-row-kind={milestone ? "milestone" : summary ? "summary" : "task"} onClick={() => onSelect(row.id)} onDoubleClick={() => onOpenDetails?.(row)} onContextMenu={(event) => { event.preventDefault(); onSelect(row.id); onContextMenu?.(row, event); }} className={cn("relative z-[1] block w-full border-b text-start transition-colors hover:bg-primary/5", selectedId === row.id && "bg-primary/10 ring-2 ring-inset ring-primary", summary && selectedId !== row.id && "bg-muted/25")} style={{ height: PLAN_ROW_HEIGHT }} aria-label={`${identity(row)}${critical ? ` · ${tr("columns.critical")}` : ""}`} aria-pressed={selectedId === row.id}>
              {startDate && summary && <span data-testid="gantt-summary-bar" title={`${tr("ganttStatus.summary")}${critical ? ` · ${tr("columns.critical")}` : ""}`} className={cn("absolute top-1/2 flex -translate-y-1/2 items-center", selectedId === row.id && "ring-2 ring-primary ring-offset-1")} style={{ insetInlineStart: left, width: barWidth }} aria-hidden="false">
                <span className="h-4 w-[3px] shrink-0 rounded-sm bg-foreground/75" />
                <span className="h-[6px] flex-1 bg-foreground/75" />
                <span className="h-4 w-[3px] shrink-0 rounded-sm bg-foreground/75" />
              </span>}
              {startDate && milestone && <span data-testid="gantt-milestone" title={`${tr("ganttStatus.milestone")}${critical ? ` · ${tr("columns.critical")}` : ""}`} className={cn("absolute top-1/2 block h-3.5 w-3.5 -translate-x-1/2 -translate-y-1/2 rotate-45 rounded-[2px] shadow-sm", statusStyle.bar, selectedId === row.id && statusStyle.selected, critical && "ring-2 ring-destructive ring-offset-1")} style={{ insetInlineStart: left + dayWidth / 2 }} />}
              {startDate && !milestone && !summary && <span data-testid="gantt-task-bar" title={`${tr(`ganttStatus.${status}`)}${critical ? ` · ${tr("columns.critical")}` : ""}`} className={cn("absolute top-1/2 block -translate-y-1/2 overflow-hidden shadow-sm", statusStyle.bar, selectedId === row.id && statusStyle.selected, critical && "ring-2 ring-destructive ring-offset-1")} style={{ insetInlineStart: left, width: barWidth }}>
                {progress > 0 && progress < 100 ? <span className="block h-full rounded-full bg-foreground/40" style={{ width: `${Math.min(100, progress)}%` }} /> : null}
              </span>}
              {showBaseline && baselineStartDate && <span title={`${tr("columns.baselineStart")} · ${baselineStartDate.toISOString().slice(0, 10)}`} className={cn("absolute top-[26px] block h-1.5 border border-dashed border-foreground/60 bg-muted-foreground/35", milestone ? "h-2.5 w-2.5 -translate-x-1/2 rotate-45 rounded-[1px]" : "rounded-sm")} style={{ insetInlineStart: milestone ? baselineLeft + dayWidth / 2 : baselineLeft, width: milestone ? undefined : baselineBarWidth }} />}
              {deadlineLeft !== undefined && <span title={`${tr("columns.deadline")} · ${row.activity?.deadline?.slice(0, 10)}`} aria-label={tr("columns.deadline")} className={cn("pointer-events-none absolute inset-y-1 z-[3] border-s-2 border-dotted", row.activity?.deadlineExceeded ? "border-destructive" : "border-warning")} style={{ insetInlineStart: deadlineLeft }} />}
            </button>;
          })}
          <svg className="pointer-events-none absolute inset-0 z-[2] overflow-visible" width={width} height={rows.length * PLAN_ROW_HEIGHT} aria-hidden="true">
            <defs><marker id="planning-arrow" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto"><path d="M0,0 L6,3 L0,6 Z" className="fill-muted-foreground" /></marker></defs>
            {dependencies.map((dependency) => {
              const predecessorIndex = rowIndexByActivityId.get(dependency.predecessorActivityId);
              const successorIndex = rowIndexByActivityId.get(dependency.activityId);
              if (predecessorIndex === undefined || successorIndex === undefined) return null;
              const predecessor = metricsByRowId.get(rows[predecessorIndex].id)!;
              const successor = metricsByRowId.get(rows[successorIndex].id)!;
              if (!predecessor.startDate || !successor.startDate) return null;
              const predecessorUsesEnd = dependency.type === PLANNING_DEPENDENCY_TYPE.finishToStart || dependency.type === PLANNING_DEPENDENCY_TYPE.finishToFinish;
              const successorUsesEnd = dependency.type === PLANNING_DEPENDENCY_TYPE.finishToFinish || dependency.type === PLANNING_DEPENDENCY_TYPE.startToFinish;
              const x1 = predecessor.left + (predecessorUsesEnd ? predecessor.barWidth : 0);
              const x2 = successor.left + (successorUsesEnd ? successor.barWidth : 0);
              const y1 = predecessorIndex * PLAN_ROW_HEIGHT + PLAN_ROW_HEIGHT / 2;
              const y2 = successorIndex * PLAN_ROW_HEIGHT + PLAN_ROW_HEIGHT / 2;
              const elbow = x1 + Math.max(8, Math.abs(x2 - x1) / 2);
              return <path key={dependency.id} d={`M ${x1} ${y1} H ${elbow} V ${y2} H ${x2}`} fill="none" className="stroke-foreground/70" strokeWidth="1.5" markerEnd="url(#planning-arrow)" />;
            })}
          </svg>
        </div>
      </div>
    </div>
  </div>;
}
