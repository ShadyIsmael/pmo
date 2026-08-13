import type { DashboardState, DashboardView, PointOfView } from "./dashboard-types";

const STORAGE_KEY = "nexus-pmo-dashboard-v2";
const LEGACY_STORAGE_KEY = "nexus-pmo-dashboard";

const defaultFilters = { dateRange: "30d", portfolio: null, program: null, project: null };

function migrateView(view: Partial<DashboardView>, pov: PointOfView): DashboardView {
  return {
    id: view.id ?? `v-${Date.now()}`,
    name: view.name ?? "My Dashboard",
    description: view.description,
    pov: view.pov ?? pov,
    ownerId: view.ownerId ?? "current-user",
    ownerName: view.ownerName ?? "You",
    type: view.type ?? "personal",
    access: view.access ?? "manage-sharing",
    sharedWith: view.sharedWith ?? [],
    widgets: view.widgets ?? [],
    filters: view.filters ?? defaultFilters,
    isDefault: view.isDefault ?? false,
    createdAt: view.createdAt ?? Date.now(),
    updatedAt: view.updatedAt ?? Date.now(),
  };
}

function migrateState(raw: Partial<DashboardState> & { filters?: DashboardState["views"][number]["filters"] }): DashboardState | null {
  if (!raw.pov || !raw.views?.length) return null;
  const views = raw.views.map((view) => {
    const migrated = migrateView(view, raw.pov!);
    if (!view.filters && raw.filters) migrated.filters = raw.filters;
    return migrated;
  });
  const current = views.find((view) => view.id === raw.currentViewId) ?? views[0]!;
  return {
    currentViewId: current.id,
    views,
    pov: raw.pov,
    lastViewByPOV: raw.lastViewByPOV ?? { [raw.pov]: current.id },
    editMode: false,
    lastUpdated: raw.lastUpdated ?? null,
  };
}

export function saveState(state: DashboardState): void {
  if (typeof window === "undefined") return;
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    console.warn("Failed to save dashboard state to localStorage");
  }
}

export function loadState(): DashboardState | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = localStorage.getItem(STORAGE_KEY) ?? localStorage.getItem(LEGACY_STORAGE_KEY);
    if (raw) return migrateState(JSON.parse(raw));
  } catch {
    console.warn("Failed to load dashboard state from localStorage");
  }
  return null;
}

export function clearState(): void {
  if (typeof window === "undefined") return;
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* ignore */
  }
}
