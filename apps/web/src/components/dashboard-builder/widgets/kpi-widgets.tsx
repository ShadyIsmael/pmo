import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { getActiveProjects, getProjectsByHealth, getTotalBudget, getTotalActual, getForecastVariancePercent, getOpenRisks, getOverdueTasks } from "@/lib/dashboard-mock-data";

function ActiveProjectsKPI({ config }: { config: WidgetConfig }) {
  const projects = getActiveProjects();
  const health = getProjectsByHealth();
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className="text-3xl font-bold">{projects.length}</span>
      <span className="text-xs text-muted-foreground">Active Projects</span>
      <div className="flex gap-3 mt-1 text-xs">
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-success" />{health.green}</span>
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-warning" />{health.amber}</span>
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-destructive" />{health.red}</span>
      </div>
    </div>
  );
}

function ProjectsAtRiskKPI({ config }: { config: WidgetConfig }) {
  const health = getProjectsByHealth();
  const atRisk = health.amber + health.red;
  const total = health.green + atRisk;
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className="text-3xl font-bold text-warning">{atRisk}</span>
      <span className="text-xs text-muted-foreground">Projects at Risk</span>
      <span className="text-xs text-muted-foreground">{total > 0 ? Math.round(atRisk / total * 100) : 0}% of active</span>
    </div>
  );
}

function TotalBudgetKPI({ config }: { config: WidgetConfig }) {
  const budget = getTotalBudget();
  const actual = getTotalActual();
  const pct = budget > 0 ? Math.round(actual / budget * 100) : 0;
  const fmt = (n: number) => new Intl.NumberFormat("en-GB", { style: "currency", currency: "GBP", notation: "compact", maximumFractionDigits: 1 }).format(n);
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className="text-3xl font-bold">{fmt(budget)}</span>
      <span className="text-xs text-muted-foreground">Total Budget</span>
      <span className="text-xs text-muted-foreground">{fmt(actual)} spent ({pct}%)</span>
    </div>
  );
}

function ForecastVarianceKPI({ config }: { config: WidgetConfig }) {
  const variance = getForecastVariancePercent();
  const under = variance <= 0;
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className={`text-3xl font-bold ${under ? "text-success" : "text-destructive"}`}>
        {under ? "" : "+"}{variance.toFixed(1)}%
      </span>
      <span className="text-xs text-muted-foreground">Forecast Variance</span>
      <span className={`text-xs ${under ? "text-success" : "text-destructive"}`}>
        {under ? "Under budget" : "Over budget"}
      </span>
    </div>
  );
}

function OpenRisksKPI({ config }: { config: WidgetConfig }) {
  const risks = getOpenRisks();
  const critical = risks.filter((r) => r.score >= 15).length;
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className="text-3xl font-bold text-destructive">{risks.length}</span>
      <span className="text-xs text-muted-foreground">Open Risks</span>
      <span className="text-xs text-destructive">{critical} critical</span>
    </div>
  );
}

function OverdueTasksKPI({ config }: { config: WidgetConfig }) {
  const tasks = getOverdueTasks();
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className="text-3xl font-bold text-destructive">{tasks.length}</span>
      <span className="text-xs text-muted-foreground">Overdue Tasks</span>
      <span className="text-xs text-destructive">{tasks.filter((t) => t.priority === "Critical").length} critical</span>
    </div>
  );
}

registerWidget("active-projects", {
  name: "Active Projects", description: "Count of active projects by health", category: "Overview", icon: "FolderKanban",
  supportedPOVs: ["Executive", "Portfolio Manager", "PMO Administrator"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.dateRange, f.portfolio, f.includeCompleted],
}, ActiveProjectsKPI);

registerWidget("projects-at-risk", {
  name: "Projects at Risk", description: "Number of amber and red projects", category: "Overview", icon: "AlertTriangle",
  supportedPOVs: ["Executive", "Portfolio Manager", "Project Manager", "PMO Administrator"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.dateRange, f.portfolio, f.program],
}, ProjectsAtRiskKPI);

registerWidget("total-budget", {
  name: "Total Budget", description: "Total approved budget across active projects", category: "Overview", icon: "Banknote",
  supportedPOVs: ["Executive", "Finance Controller", "Portfolio Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.dateRange, f.portfolio, f.includeCompleted],
}, TotalBudgetKPI);

registerWidget("forecast-variance", {
  name: "Forecast Variance", description: "Percentage variance between budget and forecast", category: "Overview", icon: "TrendingUp",
  supportedPOVs: ["Executive", "Finance Controller", "Portfolio Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.dateRange, f.portfolio, f.includeCompleted],
}, ForecastVarianceKPI);

registerWidget("open-risks", {
  name: "Open Risks", description: "Count of open and critical risks", category: "Risks", icon: "AlertTriangle",
  supportedPOVs: ["Executive", "Project Manager", "Portfolio Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords],
}, OpenRisksKPI);

registerWidget("overdue-tasks", {
  name: "Overdue Tasks", description: "Number of past-due tasks", category: "My Work", icon: "Clock",
  supportedPOVs: ["Team Member", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.maxRecords, f.includeCompleted],
}, OverdueTasksKPI);
