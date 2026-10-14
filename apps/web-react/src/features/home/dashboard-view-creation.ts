export type DashboardStartMode = "blank" | "template" | "duplicate";

export interface DashboardWidgetSeed {
  id: string;
  type: string;
  position: { x: number; y: number; w: number; h: number };
}

export function createDashboardWidgetCopies<T extends DashboardWidgetSeed>(
  mode: DashboardStartMode,
  templateWidgets: readonly T[],
  sourceWidgets: readonly T[] = [],
): T[] {
  if (mode === "blank") return [];
  const source = mode === "template" ? templateWidgets : sourceWidgets;
  const copyId = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  return source.map((widget, index) => ({
    ...widget,
    id: `${widget.type}-${copyId}-${index}`,
    position: { ...widget.position },
  }));
}
