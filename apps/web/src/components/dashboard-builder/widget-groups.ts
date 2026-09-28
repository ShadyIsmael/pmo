type WidgetGroup = "charts" | "integrations" | "ai" | "extra";

const widgetGroupLoaders: Record<WidgetGroup, () => Promise<unknown>> = {
  charts: () => import("./widgets/chart-widgets"),
  integrations: () => import("./widgets/integration-widgets"),
  ai: () => import("./widgets/ai-widgets"),
  extra: () => import("./widgets/extra-widgets"),
};

const widgetGroupByType: Record<string, WidgetGroup> = {
  "projects-by-health": "charts",
  "projects-by-status": "charts",
  "budget-vs-actual": "charts",
  "resource-utilization": "charts",
  "risk-trend": "charts",
  "tasks-by-status": "charts",
  "jira-sync": "integrations",
  "ms-project-import": "integrations",
  "integration-health": "integrations",
  "ai-recommendations": "ai",
  "delayed-predictions": "ai",
  "resource-capacity": "extra",
  "overallocated-resources": "extra",
  "my-timesheet-status": "extra",
  "hours-this-week": "extra",
  "missing-timesheets": "extra",
  "timesheet-approval-inbox": "extra",
  "planned-vs-actual": "extra",
  "cost-variance": "extra",
  "risk-heat-map": "extra",
};

let allDeferredGroupsPromise: Promise<void> | null = null;
const loadedGroups = new Set<WidgetGroup>();
const groupPromises = new Map<WidgetGroup, Promise<void>>();

function loadGroup(group: WidgetGroup): Promise<void> {
  const existing = groupPromises.get(group);
  if (existing) return existing;

  const promise = widgetGroupLoaders[group]().then(() => {
    loadedGroups.add(group);
  });
  groupPromises.set(group, promise);
  return promise;
}

export function loadWidgetGroupsFor(types: Iterable<string>): Promise<void> {
  const groups = new Set<WidgetGroup>();
  for (const type of types) {
    const group = widgetGroupByType[type];
    if (group && !loadedGroups.has(group)) groups.add(group);
  }
  return Promise.all(Array.from(groups, loadGroup)).then(() => undefined);
}

export function loadAllDeferredWidgetGroups(): Promise<void> {
  if (!allDeferredGroupsPromise) {
    allDeferredGroupsPromise = Promise.all(Object.keys(widgetGroupLoaders).map((group) => loadGroup(group as WidgetGroup))).then(() => undefined);
  }
  return allDeferredGroupsPromise;
}

export function isDeferredWidgetType(type: string): boolean {
  return type in widgetGroupByType;
}
