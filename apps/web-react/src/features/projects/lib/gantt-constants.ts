export const GANTT_ZOOM_LEVELS = ["day", "week", "month"] as const;
export type GanttZoom = (typeof GANTT_ZOOM_LEVELS)[number];

/** Shared grid/Gantt row height so both panels stay aligned in split view. */
export const PLAN_ROW_HEIGHT = 40;
