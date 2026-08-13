import { registerWidget } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import {
  getResourceCapacity, getOverallocatedResources,
  getMyTimesheetStatus, getHoursThisWeek, getMissingTimesheets,
  getTimesheetApprovalInbox, getPlannedVsActual, getCostVariance, getRiskHeatMap,
} from "@/lib/dashboard-mock-data";

function ResourceCapacityWidget({ config }: { config: WidgetConfig }) {
  const cap = getResourceCapacity();
  const pct = cap.total > 0 ? Math.round((cap.allocated / cap.total) * 100) : 0;
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1 px-2">
      <div className="relative h-20 w-20">
        <svg className="h-20 w-20 -rotate-90" viewBox="0 0 100 100">
          <circle cx="50" cy="50" r="38" fill="none" stroke="hsl(var(--muted))" strokeWidth="6" />
          <circle cx="50" cy="50" r="38" fill="none" stroke={pct > 90 ? "hsl(var(--destructive))" : pct > 75 ? "hsl(var(--warning))" : "hsl(var(--success))"} strokeWidth="6" strokeDasharray={`${2 * Math.PI * 38}`} strokeDashoffset={`${2 * Math.PI * 38 * (1 - pct / 100)}`} strokeLinecap="round" />
        </svg>
        <span className="absolute inset-0 flex items-center justify-center text-lg font-bold">{pct}%</span>
      </div>
      <div className="flex gap-3 text-xs text-muted-foreground">
        <span>{cap.allocated} allocated</span>
        <span>{cap.available} free</span>
      </div>
    </div>
  );
}

function OverallocatedResourcesWidget({ config }: { config: WidgetConfig }) {
  const resources = getOverallocatedResources().slice(0, config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {resources.map((r) => (
        <div key={r.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg text-xs">
          <span className="h-2 w-2 shrink-0 rounded-full bg-destructive" />
          <span className="flex-1 truncate font-medium">{r.name}</span>
          <span className="text-destructive tabular-nums">{r.allocated}/{r.availability}h</span>
        </div>
      ))}
      {resources.length === 0 && <p className="text-xs text-muted-foreground text-center py-4">No overallocated resources</p>}
    </div>
  );
}

function MyTimesheetStatusWidget({ config }: { config: WidgetConfig }) {
  const ts = getMyTimesheetStatus();
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className={`text-lg font-semibold ${ts.status === "Submitted" ? "text-success" : ts.status === "Approved" ? "text-primary" : "text-warning"}`}>{ts.status}</span>
      <span className="text-xs text-muted-foreground">Week ending {ts.weekEnding}</span>
      <div className="flex gap-3 mt-1 text-xs text-muted-foreground">
        <span>{ts.totalHours}h total</span>
        <span className="text-success">{ts.approvedHours}h approved</span>
        <span className="text-warning">{ts.pendingHours}h pending</span>
      </div>
    </div>
  );
}

function HoursThisWeekWidget({ config }: { config: WidgetConfig }) {
  const h = getHoursThisWeek();
  const pct = Math.round((h.total / h.target) * 100);
  return (
    <div className="flex flex-col items-center justify-center h-full gap-1">
      <span className="text-3xl font-bold">{h.total}</span>
      <span className="text-xs text-muted-foreground">Hours This Week</span>
      <div className="w-full max-w-[120px] h-1.5 rounded-full bg-muted overflow-hidden mt-1">
        <div className={`h-full rounded-full ${pct >= 100 ? "bg-success" : "bg-warning"}`} style={{ width: `${Math.min(pct, 100)}%` }} />
      </div>
      <div className="flex gap-2 text-xs text-muted-foreground">
        <span className="text-success">{h.billable}b</span>
        <span>{h.nonBillable}nb</span>
        <span>target {h.target}h</span>
      </div>
    </div>
  );
}

function MissingTimesheetsWidget({ config }: { config: WidgetConfig }) {
  const entries = getMissingTimesheets().slice(0, config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {entries.map((e, i) => (
        <div key={i} className="flex items-center gap-2 px-2 py-1.5 rounded-lg text-xs">
          <span className="flex-1 truncate font-medium">{e.name}</span>
          <span className="text-destructive">{e.days} days</span>
          <span className="text-muted-foreground text-[10px]">{e.department}</span>
        </div>
      ))}
    </div>
  );
}

function TimesheetApprovalInboxWidget({ config }: { config: WidgetConfig }) {
  const entries = getTimesheetApprovalInbox();
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {entries.map((e) => (
        <div key={e.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg text-xs">
          <span className="flex-1 truncate font-medium">{e.name}</span>
          <span className="text-muted-foreground">{e.hours}h</span>
          <span className="text-warning">{e.status}</span>
        </div>
      ))}
    </div>
  );
}

function PlannedVsActualWidget({ config }: { config: WidgetConfig }) {
  const data = getPlannedVsActual().slice(0, config.maxRecords || 10);
  const max = Math.max(...data.map((d) => Math.max(d.planned, d.actual)));
  return (
    <div className="space-y-1 h-full overflow-y-auto px-1">
      {data.map((d, i) => (
        <div key={i} className="space-y-0.5">
          <div className="flex items-center justify-between text-xs">
            <span className="truncate flex-1">{d.project}</span>
            <span className="tabular-nums text-muted-foreground ml-2">{d.actual}/{d.planned}h</span>
          </div>
          <div className="h-3 w-full rounded-sm bg-muted overflow-hidden flex">
            <div className="h-full bg-primary transition-all" style={{ width: `${(d.planned / max) * 100}%` }} />
            <div className={`h-full ${d.actual <= d.planned ? "bg-success" : "bg-destructive"} transition-all`} style={{ width: `${(d.actual / max) * 100}%`, marginLeft: -2 }} />
          </div>
        </div>
      ))}
    </div>
  );
}

function CostVarianceWidget({ config }: { config: WidgetConfig }) {
  const data = getCostVariance().slice(0, config.maxRecords || 10);
  return (
    <div className="space-y-0.5 h-full overflow-y-auto">
      {data.map((d, i) => (
        <div key={i} className="flex items-center gap-2 px-2 py-1.5 rounded-lg text-xs">
          <span className="flex-1 truncate font-medium">{d.project}</span>
          <span className={`tabular-nums font-medium ${d.status === "favorable" ? "text-success" : "text-destructive"}`}>
            {d.variance > 0 ? "+" : ""}{d.variance.toFixed(1)}%
          </span>
          <span className={`text-[10px] ${d.status === "favorable" ? "text-success" : "text-destructive"}`}>
            {d.status === "favorable" ? "Under" : "Over"}
          </span>
        </div>
      ))}
    </div>
  );
}

function RiskHeatMapWidget({ config }: { config: WidgetConfig }) {
  const data = getRiskHeatMap();
  const getColor = (p: number, i: number) => {
    const score = p * i;
    if (score >= 20) return "bg-destructive";
    if (score >= 12) return "bg-warning";
    return "bg-success";
  };
  return (
    <div className="h-full flex flex-col px-2">
      <div className="flex items-center gap-1 text-[9px] text-muted-foreground mb-1">
        <span className="w-16" />
        <span className="flex-1 text-center">Impact →</span>
      </div>
      {[5, 4, 3, 2, 1].map((impact) => (
        <div key={impact} className="flex items-center gap-1 text-[9px] text-muted-foreground">
          <span className="w-16 shrink-0 text-right">{impact === 5 ? "Very High" : impact === 4 ? "High" : impact === 3 ? "Medium" : impact === 2 ? "Low" : "Very Low"}</span>
          {[1, 2, 3, 4, 5].map((prob) => {
            const entry = data.find((d) => d.impact === impact && d.probability === prob);
            return (
              <div key={prob} className={`flex-1 h-5 rounded-sm ${getColor(prob, impact)} flex items-center justify-center text-[8px] font-medium text-white ${entry ? "opacity-100" : "opacity-20"}`}>
                {entry?.count || ""}
              </div>
            );
          })}
        </div>
      ))}
      <div className="text-[9px] text-muted-foreground text-center mt-1">Probability →</div>
    </div>
  );
}

registerWidget("resource-capacity", {
  name: "Resource Capacity", description: "Resource capacity vs allocation gauge", category: "Resources", icon: "PieChart",
  supportedPOVs: ["Resource Manager", "Project Manager", "Portfolio Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [{ key: "showTrend", label: "Show Trend", type: "boolean", defaultValue: true }],
}, ResourceCapacityWidget);

registerWidget("overallocated-resources", {
  name: "Overallocated Resources", description: "Resources exceeding their available capacity", category: "Resources", icon: "Users",
  supportedPOVs: ["Resource Manager", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [{ key: "maxRecords", label: "Max Records", type: "number", defaultValue: 10 }],
  dataSourceLabel: "Mock data",
}, OverallocatedResourcesWidget);

registerWidget("my-timesheet-status", {
  name: "My Timesheet Status", description: "Current timesheet submission and approval status", category: "Timesheets", icon: "Clock",
  supportedPOVs: ["Team Member", "Project Manager", "Resource Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [],
}, MyTimesheetStatusWidget);

registerWidget("hours-this-week", {
  name: "Hours This Week", description: "Total logged hours vs target for the current week", category: "Timesheets", icon: "Clock",
  supportedPOVs: ["Team Member", "Project Manager", "Resource Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [],
}, HoursThisWeekWidget);

registerWidget("missing-timesheets", {
  name: "Missing Timesheets", description: "Team members who haven't submitted timesheets", category: "Timesheets", icon: "AlertCircle",
  supportedPOVs: ["Project Manager", "Resource Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [{ key: "maxRecords", label: "Max Records", type: "number", defaultValue: 10 }],
  dataSourceLabel: "Mock data",
}, MissingTimesheetsWidget);

registerWidget("timesheet-approval-inbox", {
  name: "Timesheet Approval Inbox", description: "Timesheets pending your approval", category: "Timesheets", icon: "CheckSquare",
  supportedPOVs: ["Project Manager", "Portfolio Manager", "Finance Controller"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 },
  defaultConfig: {}, configFields: [],
  dataSourceLabel: "Mock data",
}, TimesheetApprovalInboxWidget);

registerWidget("planned-vs-actual", {
  name: "Planned vs Actual Work", description: "Comparison of planned hours to actual hours logged", category: "Timesheets", icon: "BarChart3",
  supportedPOVs: ["Project Manager", "Portfolio Manager", "Resource Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [{ key: "maxRecords", label: "Max Records", type: "number", defaultValue: 10 }],
}, PlannedVsActualWidget);

registerWidget("cost-variance", {
  name: "Cost Variance", description: "Budget vs actual cost variance by project", category: "Costs", icon: "TrendingDown",
  supportedPOVs: ["Executive", "Finance Controller", "Portfolio Manager", "Project Manager"],
  minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 3, h: 2 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [{ key: "maxRecords", label: "Max Records", type: "number", defaultValue: 10 }],
}, CostVarianceWidget);

registerWidget("risk-heat-map", {
  name: "Risk Heat Map", description: "5x5 risk heat map showing risk distribution", category: "Risks", icon: "Grid3x3",
  supportedPOVs: ["Executive", "Portfolio Manager", "Project Manager", "PMO Administrator"],
  minSize: { x: 0, y: 0, w: 3, h: 3 }, defaultSize: { x: 0, y: 0, w: 3, h: 3 }, maxSize: { x: 0, y: 0, w: 6, h: 4 },
  defaultConfig: {}, configFields: [],
  dataSourceLabel: "Mock data",
}, RiskHeatMapWidget);
