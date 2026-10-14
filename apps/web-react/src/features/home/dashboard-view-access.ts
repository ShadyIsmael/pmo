export type DashboardViewAccessLevel = "view" | "duplicate" | "edit" | "manage-sharing";

export interface DashboardViewAccessDefinition {
  access: DashboardViewAccessLevel;
}

export function dashboardViewsForPov<T extends { pov: string }>(views: readonly T[], pov: string): T[] {
  return views.filter((view) => view.pov === pov);
}

export function canEditDashboardView(view: DashboardViewAccessDefinition): boolean {
  return view.access === "edit" || view.access === "manage-sharing";
}

export function canMutateDashboard(canMutate: boolean, managementLoading: boolean, effectiveReadOnly: boolean): boolean {
  return !managementLoading && canMutate && !effectiveReadOnly;
}

export function canDuplicateDashboardView(view: DashboardViewAccessDefinition): boolean {
  return view.access !== "view";
}

export function defaultDashboardViewIdForPov<T extends { id: string; pov: string; isDefault: boolean }>(
  views: readonly T[],
  pov: string,
): string | undefined {
  const scopedViews = views.filter((view) => view.pov === pov);
  return scopedViews.find((view) => view.isDefault)?.id ?? scopedViews[0]?.id;
}
