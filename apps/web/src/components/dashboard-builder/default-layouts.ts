import type { PointOfView, WidgetInstance, DashboardView } from "./dashboard-types";

let counter = 0;
function wid(): string { return `w-${Date.now()}-${++counter}`; }
interface DefaultEntry { type: string; w: number; h: number; }

const managementLayout: DefaultEntry[] = [
  { type: "active-projects", w: 2, h: 2 },
  { type: "projects-at-risk", w: 2, h: 2 },
  { type: "open-risks", w: 2, h: 2 },
  { type: "total-budget", w: 2, h: 2 },
  { type: "projects-by-health", w: 3, h: 3 },
  { type: "budget-vs-actual", w: 3, h: 3 },
  { type: "projects-attention", w: 3, h: 3 },
  { type: "top-risks", w: 3, h: 3 },
  { type: "status-report-freshness", w: 3, h: 2 },
  { type: "resource-utilization", w: 3, h: 3 },
  { type: "upcoming-meetings", w: 3, h: 3 },
];

const layouts: Record<PointOfView, DefaultEntry[]> = {
  Executive: managementLayout,
  "Project Manager": [
    { type: "projects-attention", w: 3, h: 3 }, { type: "my-todo", w: 3, h: 3 },
    { type: "upcoming-milestones", w: 3, h: 3 }, { type: "overdue-tasks", w: 2, h: 2 },
    { type: "open-risks", w: 2, h: 2 }, { type: "resource-utilization", w: 2, h: 2 },
    { type: "budget-vs-actual", w: 3, h: 3 }, { type: "upcoming-meetings", w: 3, h: 3 },
    { type: "status-report-freshness", w: 3, h: 2 },
  ],
  "Program Manager": managementLayout,
  "Portfolio Manager": managementLayout,
  "Team Member": [
    { type: "my-todo", w: 3, h: 3 }, { type: "upcoming-milestones", w: 3, h: 3 },
    { type: "upcoming-meetings", w: 3, h: 3 }, { type: "overdue-tasks", w: 2, h: 2 },
  ],
  "Resource Manager": [
    { type: "resource-utilization", w: 2, h: 2 }, { type: "resource-capacity", w: 3, h: 3 },
    { type: "overallocated-resources", w: 3, h: 3 }, { type: "planned-vs-actual", w: 3, h: 3 },
  ],
  "Finance Controller": [
    { type: "total-budget", w: 2, h: 2 }, { type: "budget-vs-actual", w: 3, h: 3 },
    { type: "cost-variance", w: 3, h: 3 }, { type: "top-risks", w: 3, h: 3 },
  ],
  "PMO Administrator": managementLayout,
};

function buildWidgets(entries: DefaultEntry[]): WidgetInstance[] {
  counter = 0;
  return entries.map((entry, index) => ({ id: wid(), type: entry.type, title: "", config: {}, position: { x: (index * 3) % 12, y: Math.floor(index / 4) * 3, w: entry.w, h: entry.h }, collapsed: false, lastUpdated: null, loading: false, error: null }));
}

export function createDefaultView(pov: PointOfView, name?: string): DashboardView {
  return { id: `default-${pov.toLowerCase().replace(/\s+/g, "-")}`, name: name || "My Dashboard", description: `${pov} starter dashboard`, pov, ownerId: "current-user", ownerName: "You", type: "personal", access: "manage-sharing", sharedWith: [], widgets: buildWidgets(layouts[pov] ?? managementLayout), filters: { dateRange: "30d", portfolio: null, program: null, project: null }, isDefault: true, createdAt: Date.now(), updatedAt: Date.now() };
}

export function getDefaultLayoutTypes(pov: PointOfView): string[] { return (layouts[pov] ?? managementLayout).map((entry) => entry.type); }

export const POV_LABELS: Record<PointOfView, string> = {
  Executive: "Executive Dashboard", "Project Manager": "Project Manager Dashboard", "Program Manager": "Program Manager Dashboard", "Portfolio Manager": "Portfolio Manager Dashboard", "Team Member": "Team Member Dashboard", "Resource Manager": "Resource Manager Dashboard", "Finance Controller": "Finance Controller Dashboard", "PMO Administrator": "PMO Administrator Dashboard",
};
export const POV_DESCRIPTIONS: Record<PointOfView, string> = {
  Executive: "High-level organisational view", "Project Manager": "Project delivery and personal work", "Program Manager": "Program delivery, risk, and financial oversight", "Portfolio Manager": "Portfolio health, budgets, and strategic delivery", "Team Member": "Assigned work and delivery dates", "Resource Manager": "Capacity and allocation oversight", "Finance Controller": "Financial oversight", "PMO Administrator": "Governance and operational oversight",
};
export const POV_OPTIONS: PointOfView[] = ["Project Manager", "Program Manager", "Portfolio Manager"];
