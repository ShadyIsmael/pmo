import type { PointOfView, WidgetInstance, DashboardView } from "./dashboard-types";

let _counter = 0;
function wid(): string {
  return `w-${Date.now()}-${++_counter}`;
}

interface DefaultEntry {
  type: string;
  w: number;
  h: number;
}

const layouts: Record<PointOfView, DefaultEntry[]> = {
  Executive: [
    { type: "active-projects", w: 2, h: 2 },
    { type: "projects-at-risk", w: 2, h: 2 },
    { type: "total-budget", w: 2, h: 2 },
    { type: "forecast-variance", w: 2, h: 2 },
    { type: "open-risks", w: 2, h: 2 },
    { type: "projects-by-health", w: 3, h: 3 },
    { type: "budget-vs-actual", w: 3, h: 3 },
    { type: "projects-attention", w: 3, h: 3 },
    { type: "top-risks", w: 3, h: 3 },
    { type: "risk-heat-map", w: 3, h: 3 },
    { type: "pending-approvals", w: 3, h: 3 },
    { type: "cost-variance", w: 3, h: 2 },
    { type: "ai-recommendations", w: 3, h: 3 },
  ],
  "Project Manager": [
    { type: "projects-attention", w: 3, h: 3 },
    { type: "upcoming-milestones", w: 3, h: 3 },
    { type: "overdue-tasks", w: 2, h: 2 },
    { type: "open-risks", w: 2, h: 2 },
    { type: "resource-utilization", w: 2, h: 2 },
    { type: "budget-vs-actual", w: 3, h: 3 },
    { type: "top-risks", w: 3, h: 3 },
    { type: "pending-approvals", w: 3, h: 3 },
    { type: "cost-variance", w: 3, h: 2 },
    { type: "planned-vs-actual", w: 3, h: 3 },
    { type: "my-timesheet-status", w: 2, h: 2 },
    { type: "missing-timesheets", w: 2, h: 2 },
    { type: "hours-this-week", w: 2, h: 2 },
  ],
  "Portfolio Manager": [
    { type: "active-projects", w: 2, h: 2 },
    { type: "projects-at-risk", w: 2, h: 2 },
    { type: "budget-vs-actual", w: 3, h: 3 },
    { type: "projects-by-health", w: 3, h: 3 },
    { type: "projects-by-status", w: 3, h: 3 },
    { type: "resource-utilization", w: 2, h: 2 },
    { type: "projects-attention", w: 3, h: 3 },
    { type: "top-risks", w: 3, h: 3 },
    { type: "risk-heat-map", w: 3, h: 3 },
    { type: "cost-variance", w: 3, h: 2 },
    { type: "ai-recommendations", w: 3, h: 3 },
  ],
  "Team Member": [
    { type: "overdue-tasks", w: 2, h: 2 },
    { type: "my-todo", w: 3, h: 3 },
    { type: "upcoming-milestones", w: 3, h: 3 },
    { type: "upcoming-meetings", w: 3, h: 3 },
    { type: "pending-approvals", w: 3, h: 3 },
    { type: "recent-notifications", w: 3, h: 3 },
    { type: "my-timesheet-status", w: 2, h: 2 },
    { type: "hours-this-week", w: 2, h: 2 },
  ],
  "Resource Manager": [
    { type: "resource-utilization", w: 2, h: 2 },
    { type: "resource-capacity", w: 3, h: 3 },
    { type: "overallocated-resources", w: 3, h: 3 },
    { type: "resource-availability", w: 3, h: 3 },
    { type: "my-timesheet-status", w: 2, h: 2 },
    { type: "missing-timesheets", w: 2, h: 2 },
    { type: "hours-this-week", w: 2, h: 2 },
  ],
  "Finance Controller": [
    { type: "total-budget", w: 2, h: 2 },
    { type: "forecast-variance", w: 2, h: 2 },
    { type: "budget-vs-actual", w: 3, h: 3 },
    { type: "cost-variance", w: 3, h: 3 },
    { type: "top-risks", w: 3, h: 3 },
    { type: "pending-approvals", w: 3, h: 3 },
    { type: "timesheet-approval-inbox", w: 3, h: 3 },
    { type: "planned-vs-actual", w: 3, h: 3 },
  ],
  "PMO Administrator": [
    { type: "active-projects", w: 2, h: 2 },
    { type: "projects-by-health", w: 3, h: 3 },
    { type: "projects-attention", w: 3, h: 3 },
    { type: "risk-heat-map", w: 3, h: 3 },
    { type: "integration-health", w: 3, h: 3 },
    { type: "jira-sync", w: 2, h: 2 },
    { type: "ms-project-import", w: 2, h: 2 },
    { type: "ai-recommendations", w: 3, h: 3 },
    { type: "pending-approvals", w: 3, h: 3 },
    { type: "missing-timesheets", w: 2, h: 2 },
  ],
};

function buildWidgets(entries: DefaultEntry[]): WidgetInstance[] {
  const cols = 12;
  return entries.map((entry, idx) => {
    let x = (idx * 3) % cols;
    if (x + entry.w > cols) x = cols - entry.w;
    const y = Math.floor((idx * 3) / cols) * 2;
    return {
      id: wid(),
      type: entry.type,
      title: "",
      config: {},
      position: { x, y, w: entry.w, h: entry.h },
      collapsed: false,
      lastUpdated: null,
      loading: false,
      error: null,
    };
  });
}

export function createDefaultView(pov: PointOfView, name?: string): DashboardView {
  const entries = layouts[pov] ?? layouts.Executive;
  _counter = 0;
  return {
    id: `default-${pov.toLowerCase().replace(/\s+/g, "-")}`,
    name: name || "My Dashboard",
    description: `${pov} starter dashboard`,
    pov,
    ownerId: "current-user",
    ownerName: "You",
    type: "personal",
    access: "manage-sharing",
    sharedWith: [],
    widgets: buildWidgets(entries),
    filters: { dateRange: "30d", portfolio: null, program: null, project: null },
    isDefault: true,
    createdAt: Date.now(),
    updatedAt: Date.now(),
  };
}

export function getDefaultLayoutTypes(pov: PointOfView): string[] {
  return (layouts[pov] ?? layouts.Executive).map((e) => e.type);
}

export const POV_LABELS: Record<PointOfView, string> = {
  Executive: "Executive Dashboard",
  "Project Manager": "Project Manager Dashboard",
  "Portfolio Manager": "Portfolio Manager Dashboard",
  "Team Member": "Team Member Dashboard",
  "Resource Manager": "Resource Manager Dashboard",
  "Finance Controller": "Finance Controller Dashboard",
  "PMO Administrator": "PMO Administrator Dashboard",
};

export const POV_DESCRIPTIONS: Record<PointOfView, string> = {
  Executive: "High-level view of organisational performance and strategic objectives",
  "Project Manager": "Track project health, milestones, and team performance",
  "Portfolio Manager": "Oversee portfolio health, budgets, and strategic alignment",
  "Team Member": "Your personal workspace with tasks, approvals, and notifications",
  "Resource Manager": "Monitor resource capacity, utilisation, and allocation",
  "Finance Controller": "Financial oversight across all projects and portfolios",
  "PMO Administrator": "Governance, standards, and operational oversight",
};

export const POV_OPTIONS: PointOfView[] = [
  "Executive",
  "Project Manager",
  "Portfolio Manager",
  "Team Member",
  "Resource Manager",
  "Finance Controller",
  "PMO Administrator",
];
