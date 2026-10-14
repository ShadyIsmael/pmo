export const GANTT_ZOOM_LEVELS = ["day", "week", "month"] as const;
export type GanttZoom = (typeof GANTT_ZOOM_LEVELS)[number];
