export type StatusTone = "green" | "amber" | "red" | "blue" | "neutral";

export const WORK_ITEM_STATUS_COLORS = ["slate", "blue", "amber", "violet", "emerald", "rose", "orange"] as const;
export type WorkItemStatusColor = (typeof WORK_ITEM_STATUS_COLORS)[number];

export type WorkItemStatusColorClasses = {
  panel: string;
  badge: string;
  dot: string;
  text: string;
  swatch: string;
};

export const workItemStatusColorClasses: Record<WorkItemStatusColor, WorkItemStatusColorClasses> = {
  slate: { panel: "border-border/70 bg-muted/40 dark:border-border/70 dark:bg-muted/20", badge: "bg-secondary text-secondary-foreground dark:bg-secondary/80 dark:text-secondary-foreground", dot: "bg-muted-foreground", text: "text-secondary-foreground dark:text-secondary-foreground", swatch: "bg-muted-foreground" },
  blue: { panel: "border-info/30 bg-info/5 dark:border-info/30 dark:bg-info/10", badge: "bg-info/10 text-info dark:bg-info/20 dark:text-info", dot: "bg-info", text: "text-info dark:text-info", swatch: "bg-info" },
  amber: { panel: "border-warning/30 bg-warning/5 dark:border-warning/30 dark:bg-warning/10", badge: "bg-warning/10 text-warning dark:bg-warning/20 dark:text-warning", dot: "bg-warning", text: "text-warning dark:text-warning", swatch: "bg-warning" },
  violet: { panel: "border-fin-violet/30 bg-fin-violet/5 dark:border-fin-violet/30 dark:bg-fin-violet/10", badge: "bg-fin-violet/10 text-fin-violet dark:bg-fin-violet/20 dark:text-fin-violet", dot: "bg-fin-violet", text: "text-fin-violet dark:text-fin-violet", swatch: "bg-fin-violet" },
  emerald: { panel: "border-success/30 bg-success/5 dark:border-success/30 dark:bg-success/10", badge: "bg-success/10 text-success dark:bg-success/20 dark:text-success", dot: "bg-success", text: "text-success dark:text-success", swatch: "bg-success" },
  rose: { panel: "border-destructive/30 bg-destructive/5 dark:border-destructive/30 dark:bg-destructive/10", badge: "bg-destructive/10 text-destructive dark:bg-destructive/20 dark:text-destructive", dot: "bg-destructive", text: "text-destructive dark:text-destructive", swatch: "bg-destructive" },
  orange: { panel: "border-warning/30 bg-warning/5 dark:border-warning/30 dark:bg-warning/10", badge: "bg-warning/10 text-warning dark:bg-warning/20 dark:text-warning", dot: "bg-warning", text: "text-warning dark:text-warning", swatch: "bg-warning" },
};

export function getWorkItemStatusColor(value: string | null | undefined): WorkItemStatusColor {
  return WORK_ITEM_STATUS_COLORS.find((color) => color === value) ?? "slate";
}

export function getWorkItemStatusColorClasses(value: string | null | undefined): WorkItemStatusColorClasses {
  return workItemStatusColorClasses[getWorkItemStatusColor(value)];
}

export type StatusBadgeVariant = `status-${StatusTone}`;

export const statusToneClasses: Record<StatusTone, string> = {
  green: "border-success/20 bg-success/10 text-success hover:bg-success/20",
  amber: "border-warning/20 bg-warning/10 text-warning hover:bg-warning/20",
  red: "border-destructive/20 bg-destructive/10 text-destructive hover:bg-destructive/20",
  blue: "border-info/20 bg-info/10 text-info hover:bg-info/20",
  neutral: "border-border bg-muted text-muted-foreground hover:bg-muted/80",
};

export const statusToneDotClasses: Record<StatusTone, string> = {
  green: "bg-success",
  amber: "bg-warning",
  red: "bg-destructive",
  blue: "bg-info",
  neutral: "bg-muted-foreground",
};

export const statusToneTextClasses: Record<StatusTone, string> = {
  green: "text-success",
  amber: "text-warning",
  red: "text-destructive",
  blue: "text-info",
  neutral: "text-muted-foreground",
};

export const statusTonePdfColors: Record<StatusTone, string> = {
  green: "#15803D",
  amber: "#B45309",
  red: "#B91C1C",
  blue: "#0369A1",
  neutral: "#64748B",
};

const STATUS_TONES: Record<string, StatusTone> = {
  active: "green",
  approved: "green",
  accepted: "green",
  completed: "green",
  connected: "green",
  inprogress: "green",
  installed: "green",
  low: "green",
  matched: "green",
  mitigated: "green",
  ok: "green",
  ontrack: "green",
  passed: "green",
  published: "green",
  ready: "green",
  resolved: "green",
  verified: "green",
  green: "green",

  actionrequired: "amber",
  atrisk: "amber",
  delayed: "amber",
  draft: "amber",
  followup: "amber",
  inreview: "amber",
  onhold: "amber",
  pending: "amber",
  pendingapproval: "amber",
  planned: "amber",
  medium: "amber",
  warning: "amber",
  amber: "amber",

  blocked: "red",
  cancelled: "red",
  critical: "red",
  failed: "red",
  high: "red",
  overbudget: "red",
  overdue: "red",
  rejected: "red",
  red: "red",
  unmatched: "red",
  open: "red",

  identified: "blue",
  info: "blue",
  initiation: "blue",
  notstarted: "blue",
  new: "blue",
  planning: "blue",
  expected: "blue",

  archived: "neutral",
  closed: "neutral",
  paused: "neutral",
  superseded: "neutral",
};

function normalizeStatus(value: string | null | undefined): string {
  return (value ?? "").trim().toLowerCase().replace(/[\s_-]/g, "");
}

export function getStatusTone(value: string | null | undefined): StatusTone {
  return STATUS_TONES[normalizeStatus(value)] ?? "neutral";
}

export function getStatusBadgeVariant(value: string | null | undefined): StatusBadgeVariant {
  return `status-${getStatusTone(value)}`;
}

export function getStatusTextClass(value: string | null | undefined): string {
  return statusToneTextClasses[getStatusTone(value)];
}
