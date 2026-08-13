import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { getProjectsByHealth, getProjectsByStatus, getPortfolioBudgetSummary, getResourceUtilization, getRiskTrend, getTasksByStatus } from "@/lib/dashboard-mock-data";

function ProjectsByHealthChart({ config }: { config: WidgetConfig }) {
  const health = getProjectsByHealth();
  const total = health.green + health.amber + health.red;
  const items = [
    { label: "Healthy", count: health.green, color: "bg-success", pct: total > 0 ? Math.round(health.green / total * 100) : 0 },
    { label: "At Risk", count: health.amber, color: "bg-warning", pct: total > 0 ? Math.round(health.amber / total * 100) : 0 },
    { label: "Critical", count: health.red, color: "bg-destructive", pct: total > 0 ? Math.round(health.red / total * 100) : 0 },
  ];
  return (
    <div className="space-y-3 h-full flex flex-col justify-center px-2">
      {items.map((item) => (
        <div key={item.label} className="space-y-1">
          <div className="flex items-center justify-between text-xs">
            <span className="flex items-center gap-1.5"><span className={`h-2 w-2 rounded-full ${item.color}`} />{item.label}</span>
            <span className="font-medium">{item.count} ({item.pct}%)</span>
          </div>
          <div className="h-2 w-full rounded-full bg-muted overflow-hidden">
            <div className={`h-full rounded-full ${item.color} transition-all duration-500`} style={{ width: `${item.pct}%` }} />
          </div>
        </div>
      ))}
    </div>
  );
}

function ProjectsByStatusChart({ config }: { config: WidgetConfig }) {
  const statuses = getProjectsByStatus();
  const colors: Record<string, string> = { Active: "bg-success", "At Risk": "bg-warning", Delayed: "bg-destructive", "On Hold": "bg-muted-foreground", Completed: "bg-primary", Cancelled: "bg-muted" };
  return (
    <div className="space-y-1.5 h-full flex flex-col justify-center px-2">
      {Object.entries(statuses).map(([status, count]) => (
        <div key={status} className="flex items-center gap-2 px-2 py-0.5">
          <span className={`h-2 w-2 rounded-full ${colors[status] || "bg-muted"}`} />
          <span className="text-xs flex-1">{status}</span>
          <span className="text-xs font-medium tabular-nums">{count}</span>
        </div>
      ))}
    </div>
  );
}

function BudgetVsActualChart({ config }: { config: WidgetConfig }) {
  const portfolios = getPortfolioBudgetSummary();
  const fmt = (n: number) => new Intl.NumberFormat("en-GB", { style: "currency", currency: "GBP", notation: "compact", maximumFractionDigits: 1 }).format(n);
  return (
    <div className="space-y-2 h-full overflow-y-auto px-2">
      {portfolios.map((pf) => {
        const pct = pf.budget > 0 ? Math.min(pf.actual / pf.budget * 100, 100) : 0;
        return (
          <div key={pf.id} className="space-y-0.5">
            <div className="flex items-center justify-between text-xs">
              <span className="font-medium truncate flex-1">{pf.name}</span>
              <span className="tabular-nums ml-2">{fmt(pf.actual)} / {fmt(pf.budget)}</span>
            </div>
            <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
              <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${pct}%` }} />
            </div>
          </div>
        );
      })}
    </div>
  );
}

function ResourceUtilizationChart({ config }: { config: WidgetConfig }) {
  const util = getResourceUtilization();
  return (
    <div className="flex flex-col items-center justify-center h-full">
      <div className="relative h-24 w-24">
        <svg className="h-24 w-24 -rotate-90" viewBox="0 0 100 100">
          <circle cx="50" cy="50" r="42" fill="none" stroke="hsl(var(--muted))" strokeWidth="8" />
          <circle cx="50" cy="50" r="42" fill="none" stroke={util > 90 ? "hsl(var(--destructive))" : util > 75 ? "hsl(var(--warning))" : "hsl(var(--success))"} strokeWidth="8" strokeDasharray={`${2 * Math.PI * 42}`} strokeDashoffset={`${2 * Math.PI * 42 * (1 - util / 100)}`} strokeLinecap="round" />
        </svg>
        <span className="absolute inset-0 flex items-center justify-center text-2xl font-bold">{util}%</span>
      </div>
      <span className="text-xs text-muted-foreground mt-1">Utilization Rate</span>
      {config.showTrend && (
        <span className="text-[10px] text-success mt-0.5">{"\u2191"} 2.1% vs last month</span>
      )}
    </div>
  );
}

function RiskTrendChart({ config }: { config: WidgetConfig }) {
  const data = getRiskTrend();
  const max = Math.max(...data.flatMap((d) => [d.new, d.closed, d.critical]));
  return (
    <div className="h-full flex flex-col px-1">
      <div className="flex-1 flex items-end gap-0.5">
        {data.slice(-8).map((d, i) => {
          const h = max > 0 ? (d.critical / max) * 100 : 0;
          return (
            <div key={i} className="flex-1 flex flex-col items-center gap-0.5">
              <div className="w-full flex flex-col items-center" style={{ height: "100%" }}>
                <div className="w-3/4 rounded-t-sm bg-destructive transition-all" style={{ height: `${h}%` }} title={`Critical: ${d.critical}`} />
              </div>
            </div>
          );
        })}
      </div>
      <div className="flex items-center justify-between text-[8px] text-muted-foreground mt-1">
        <span className="flex items-center gap-1"><span className="h-1.5 w-1.5 rounded-sm bg-destructive" />Critical</span>
        <span>Risk Trend (Last 8 months)</span>
      </div>
    </div>
  );
}

function TasksByStatusChart({ config }: { config: WidgetConfig }) {
  const statuses = getTasksByStatus();
  const colors: Record<string, string> = { "Not Started": "bg-muted-foreground", "In Progress": "bg-primary", Completed: "bg-success", Blocked: "bg-destructive" };
  return (
    <div className="space-y-1.5 h-full flex flex-col justify-center px-2">
      {Object.entries(statuses).map(([status, count]) => (
        <div key={status} className="flex items-center gap-2 px-2 py-0.5">
          <span className={`h-2 w-2 rounded-full ${colors[status] || "bg-muted"}`} />
          <span className="text-xs flex-1">{status}</span>
          <span className="text-xs font-medium tabular-nums">{count}</span>
        </div>
      ))}
    </div>
  );
}

registerWidget("projects-by-health", {
  name: "Projects by Health", description: "Distribution of projects by health status", category: "Projects", icon: "HeartPulse",
  supportedPOVs: ["Executive", "Portfolio Manager", "PMO Administrator", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.showLegend, f.portfolio, f.groupBy],
}, ProjectsByHealthChart);

registerWidget("projects-by-status", {
  name: "Projects by Status", description: "Projects grouped by current status", category: "Projects", icon: "ListFilter",
  supportedPOVs: ["Portfolio Manager", "PMO Administrator"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.portfolio, f.program, f.sortBy],
}, ProjectsByStatusChart);

registerWidget("budget-vs-actual", {
  name: "Budget vs Actual", description: "Portfolio-level cost comparison", category: "Costs", icon: "BarChart3",
  supportedPOVs: ["Executive", "Portfolio Manager", "Finance Controller", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.dateRange, f.portfolio, f.chartType, f.showLegend],
}, BudgetVsActualChart);

registerWidget("resource-utilization", {
  name: "Resource Utilization", description: "Overall resource utilisation rate with gauge", category: "Resources", icon: "PieChart",
  supportedPOVs: ["Resource Manager", "Project Manager", "Portfolio Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [f.customTitle, f.showTrend, f.department],
}, ResourceUtilizationChart);

registerWidget("risk-trend", {
  name: "Risk Trend", description: "Monthly trend of critical risks", category: "Risks", icon: "TrendingUp",
  supportedPOVs: ["Executive", "Project Manager", "Portfolio Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.dateRange, f.showLegend],
  dataSourceLabel: "Mock data",
}, RiskTrendChart);

registerWidget("tasks-by-status", {
  name: "Tasks by Status", description: "Task progress distribution", category: "My Work", icon: "CheckSquare",
  supportedPOVs: ["Project Manager", "Team Member"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [f.customTitle, f.sortBy, f.includeCompleted],
}, TasksByStatusChart);
