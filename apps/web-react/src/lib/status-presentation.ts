export type StatusTone = "green" | "amber" | "red" | "blue" | "neutral";
export type StatusBadgeVariant = `status-${StatusTone}`;

export const statusToneClasses: Record<StatusTone, string> = {
  green: "border-transparent bg-transparent text-success",
  amber: "border-transparent bg-transparent text-warning",
  red: "border-transparent bg-transparent text-destructive",
  blue: "border-transparent bg-transparent text-info",
  neutral: "border-transparent bg-transparent text-muted-foreground",
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
  final: "green",
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
  scheduled: "blue",
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

export function getResourceTypeTextClass(value: string | null | undefined): string {
  const normalized = normalizeStatus(value);
  if (normalized === "staff") return "text-primary";
  if (normalized === "vendorresource" || normalized === "vendor") return "text-warning";
  return "text-muted-foreground";
}

export function getSystemUserTextClass(linked: boolean): string {
  return linked ? "text-success" : "text-muted-foreground";
}

const RISK_LEVEL_TEXT_CLASSES: Record<string, string> = {
  low: "text-success",
  medium: "text-warning",
  high: "text-warning",
  critical: "text-destructive",
};

const RISK_STATUS_TEXT_CLASSES: Record<string, string> = {
  identified: "text-info",
  actionrequired: "text-warning",
  followup: "text-info",
  mitigated: "text-success",
  closed: "text-muted-foreground",
  expected: "text-info",
  active: "text-success",
};

export function getRiskLevelTextClass(value: string | null | undefined): string {
  return RISK_LEVEL_TEXT_CLASSES[normalizeStatus(value)] ?? "text-muted-foreground";
}

export function getRiskStatusTextClass(value: string | null | undefined): string {
  return RISK_STATUS_TEXT_CLASSES[normalizeStatus(value)] ?? "text-muted-foreground";
}
