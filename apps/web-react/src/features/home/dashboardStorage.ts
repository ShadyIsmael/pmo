const DASHBOARD_VIEWS_STORAGE_PREFIX = "nexus-react-dashboard-views:v2";

function storagePart(value?: string): string {
  return encodeURIComponent(value?.trim() || "unknown");
}

export function dashboardViewsStorageKey(tenantId?: string, userId?: string): string {
  return `${DASHBOARD_VIEWS_STORAGE_PREFIX}:${storagePart(tenantId)}:${storagePart(userId)}`;
}

export function loadDashboardViews<T>(tenantId: string | undefined, userId: string | undefined): T | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(dashboardViewsStorageKey(tenantId, userId));
    return raw ? (JSON.parse(raw) as T) : null;
  } catch {
    return null;
  }
}

export function saveDashboardViews<T>(tenantId: string | undefined, userId: string | undefined, value: T): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(dashboardViewsStorageKey(tenantId, userId), JSON.stringify(value));
  } catch {
    // Dashboard preferences are optional; storage failures must not break Home.
  }
}
