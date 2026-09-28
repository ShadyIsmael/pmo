import type { RiskLevel, RiskStatus, RiskImpactType } from "@/types/risk";

export const RISK_LEVELS: readonly RiskLevel[] = ["Low", "Medium", "High", "Critical"] as const;
export const RISK_STATUSES: readonly RiskStatus[] = ["Identified", "ActionRequired", "FollowUp", "Mitigated", "Closed"] as const;
export const RISK_IMPACT_TYPES: readonly RiskImpactType[] = [
  "Schedule", "Budget", "Scope", "Quality", "Resources",
  "Procurement", "Compliance", "Reputation", "Operations", "Other",
] as const;

const RISK_LEVEL_LABELS: Record<RiskLevel, string> = {
  Low: "Low",
  Medium: "Medium",
  High: "High",
  Critical: "Critical",
};

const RISK_STATUS_LABELS: Record<RiskStatus, string> = {
  Identified: "Identified",
  ActionRequired: "Action required",
  FollowUp: "Follow-up",
  Mitigated: "Mitigated",
  Closed: "Closed",
};

const RISK_IMPACT_TYPE_LABELS: Record<RiskImpactType, string> = {
  Schedule: "Schedule / delay",
  Budget: "Budget / cost",
  Scope: "Scope",
  Quality: "Quality",
  Resources: "Resources",
  Procurement: "Procurement / supplier",
  Compliance: "Compliance",
  Reputation: "Reputation",
  Operations: "Operations",
  Other: "Other",
};

export function getRiskLevelLabel(value: string): string {
  return RISK_LEVEL_LABELS[value as RiskLevel] ?? value;
}

export function getRiskStatusLabel(value: string): string {
  return RISK_STATUS_LABELS[value as RiskStatus] ?? value;
}

export function getRiskImpactTypeLabel(value: string): string {
  return RISK_IMPACT_TYPE_LABELS[value as RiskImpactType] ?? value;
}

const RISK_LEVEL_CLASSES: Record<RiskLevel, string> = {
  Low: "text-emerald-700 dark:text-emerald-400",
  Medium: "text-amber-700 dark:text-amber-300",
  High: "text-red-700 dark:text-red-400",
  Critical: "text-red-800 dark:text-red-300",
};

const RISK_STATUS_CLASSES: Record<RiskStatus, string> = {
  Identified: "text-blue-700 dark:text-blue-300",
  ActionRequired: "text-amber-700 dark:text-amber-300",
  FollowUp: "text-blue-700 dark:text-blue-300",
  Mitigated: "text-emerald-700 dark:text-emerald-400",
  Closed: "text-emerald-700 dark:text-emerald-400",
};

export function getRiskLevelTextClass(value: string): string {
  return RISK_LEVEL_CLASSES[value as RiskLevel] ?? "text-foreground";
}

export function getRiskStatusTextClass(value: string): string {
  return RISK_STATUS_CLASSES[value as RiskStatus] ?? "text-foreground";
}
