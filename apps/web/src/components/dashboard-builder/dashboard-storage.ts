import type { DashboardState, DashboardView, PointOfView } from "./dashboard-types";

const STORAGE_PREFIX = "nexus-pmo-dashboard-v3";
const defaultFilters = { dateRange: "30d", portfolio: null, program: null, project: null };

export interface DashboardStorageScope { tenantId?: string; userId?: string; pov: PointOfView; }

function safePart(value: string | undefined): string { return encodeURIComponent(value || "unknown"); }
function storageKey(scope: DashboardStorageScope): string { return `${STORAGE_PREFIX}:${safePart(scope.tenantId)}:${safePart(scope.userId)}:${safePart(scope.pov)}`; }

function migrateView(view: Partial<DashboardView>, pov: PointOfView): DashboardView {
  return { id: view.id ?? `v-${Date.now()}`, name: view.name ?? "My Dashboard", description: view.description, pov: view.pov ?? pov, ownerId: view.ownerId ?? "current-user", ownerName: view.ownerName ?? "You", type: view.type ?? "personal", access: view.access ?? "manage-sharing", sharedWith: view.sharedWith ?? [], widgets: view.widgets ?? [], filters: view.filters ?? defaultFilters, isDefault: view.isDefault ?? false, createdAt: view.createdAt ?? Date.now(), updatedAt: view.updatedAt ?? Date.now() };
}

function migrateState(raw: Partial<DashboardState>, pov: PointOfView): DashboardState | null {
  if (!raw.views?.length) return null;
  const views = raw.views.map((view) => migrateView(view, pov)).filter((view) => view.pov === pov);
  if (!views.length) return null;
  const current = views.find((view) => view.id === raw.currentViewId) ?? views.find((view) => view.isDefault) ?? views[0]!;
  return { currentViewId: current.id, views, pov, lastViewByPOV: { [pov]: current.id }, editMode: false, lastUpdated: raw.lastUpdated ?? null };
}

export function saveState(state: DashboardState, scope: DashboardStorageScope): void {
  if (typeof window === "undefined") return;
  try { window.localStorage.setItem(storageKey({ ...scope, pov: state.pov }), JSON.stringify(state)); } catch { console.warn("Failed to save dashboard state to localStorage"); }
}

export function loadState(scope: DashboardStorageScope): DashboardState | null {
  if (typeof window === "undefined") return null;
  try { const raw = window.localStorage.getItem(storageKey(scope)); return raw ? migrateState(JSON.parse(raw) as Partial<DashboardState>, scope.pov) : null; } catch { console.warn("Failed to load dashboard state from localStorage"); return null; }
}

export function clearState(scope: DashboardStorageScope): void {
  if (typeof window === "undefined") return;
  try { window.localStorage.removeItem(storageKey(scope)); } catch { /* best effort */ }
}
