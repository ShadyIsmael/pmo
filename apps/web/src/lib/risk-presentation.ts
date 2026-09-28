import type { RiskImpactType, RiskLevel, RiskStatus } from "@/lib/api/risks";

export type RiskAccent = {
  chip: string;
  dot: string;
  bar: string;
};

export const riskImpactTypeAccents: Record<RiskImpactType, RiskAccent> = {
  Schedule: { chip: "border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-950/35 dark:text-blue-300", dot: "bg-blue-500", bar: "bg-blue-500" },
  Budget: { chip: "border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-800 dark:bg-violet-950/35 dark:text-violet-300", dot: "bg-violet-500", bar: "bg-violet-500" },
  Scope: { chip: "border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-800 dark:bg-indigo-950/35 dark:text-indigo-300", dot: "bg-indigo-500", bar: "bg-indigo-500" },
  Quality: { chip: "border-teal-200 bg-teal-50 text-teal-700 dark:border-teal-800 dark:bg-teal-950/35 dark:text-teal-300", dot: "bg-teal-500", bar: "bg-teal-500" },
  Resources: { chip: "border-cyan-200 bg-cyan-50 text-cyan-700 dark:border-cyan-800 dark:bg-cyan-950/35 dark:text-cyan-300", dot: "bg-cyan-500", bar: "bg-cyan-500" },
  Procurement: { chip: "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-950/35 dark:text-amber-300", dot: "bg-amber-500", bar: "bg-amber-500" },
  Compliance: { chip: "border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-800 dark:bg-orange-950/35 dark:text-orange-300", dot: "bg-orange-500", bar: "bg-orange-500" },
  Reputation: { chip: "border-pink-200 bg-pink-50 text-pink-700 dark:border-pink-800 dark:bg-pink-950/35 dark:text-pink-300", dot: "bg-pink-500", bar: "bg-pink-500" },
  Operations: { chip: "border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-700 dark:bg-slate-900/50 dark:text-slate-300", dot: "bg-slate-500", bar: "bg-slate-500" },
  Other: { chip: "border-gray-200 bg-gray-50 text-gray-700 dark:border-gray-700 dark:bg-gray-900/50 dark:text-gray-300", dot: "bg-gray-500", bar: "bg-gray-500" },
};

export function getRiskImpactTypeAccent(value?: string | null): RiskAccent {
  return value && value in riskImpactTypeAccents
    ? riskImpactTypeAccents[value as RiskImpactType]
    : riskImpactTypeAccents.Other;
}

export const riskLevelAccents: Record<RiskLevel, RiskAccent> = {
  Low: { chip: "text-emerald-700 dark:text-emerald-400", dot: "bg-emerald-500", bar: "bg-emerald-500" },
  Medium: { chip: "text-amber-700 dark:text-amber-300", dot: "bg-amber-500", bar: "bg-amber-500" },
  High: { chip: "text-orange-700 dark:text-orange-400", dot: "bg-orange-500", bar: "bg-orange-500" },
  Critical: { chip: "text-red-800 dark:text-red-300", dot: "bg-red-600", bar: "bg-red-600" },
};

export const riskStatusAccents: Record<RiskStatus, RiskAccent> = {
  Identified: { chip: "text-blue-700 dark:text-blue-300", dot: "bg-blue-500", bar: "bg-blue-500" },
  ActionRequired: { chip: "text-amber-700 dark:text-amber-300", dot: "bg-amber-500", bar: "bg-amber-500" },
  FollowUp: { chip: "text-cyan-700 dark:text-cyan-300", dot: "bg-cyan-500", bar: "bg-cyan-500" },
  Mitigated: { chip: "text-emerald-700 dark:text-emerald-400", dot: "bg-emerald-500", bar: "bg-emerald-500" },
  Closed: { chip: "text-slate-600 dark:text-slate-300", dot: "bg-slate-400", bar: "bg-slate-400" },
};

export const riskActionPrimaryButtonClass = "bg-blue-600 text-white shadow-none hover:bg-blue-700 dark:bg-blue-500 dark:hover:bg-blue-400";
export const riskEditButtonClass = "border-blue-200 bg-background text-blue-700 shadow-none hover:border-blue-300 hover:bg-blue-50 hover:text-blue-800 dark:border-blue-800 dark:text-blue-300 dark:hover:border-blue-700 dark:hover:bg-blue-950/40 dark:hover:text-blue-200";
export const riskMitigateButtonClass = "border-emerald-200 bg-emerald-50 text-emerald-700 shadow-none hover:border-emerald-300 hover:bg-emerald-100 dark:border-emerald-800 dark:bg-emerald-950/35 dark:text-emerald-300 dark:hover:bg-emerald-950/55";
export const riskCloseButtonClass = "border-red-200 bg-red-50 text-red-700 shadow-none hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-950/35 dark:text-red-300 dark:hover:bg-red-950/55";
export const riskSelectedActionClass = "border-s-4 border-s-blue-500 border-blue-200 bg-blue-50/70 dark:border-s-blue-400 dark:border-blue-800 dark:bg-blue-950/25";

export const riskDashboardKpiAccents = {
  total: { border: "border-s-blue-500", icon: "bg-blue-50 text-blue-600 dark:bg-blue-950/40 dark:text-blue-300" },
  critical: { border: "border-s-red-500", icon: "bg-red-50 text-red-600 dark:bg-red-950/40 dark:text-red-300" },
  high: { border: "border-s-orange-500", icon: "bg-orange-50 text-orange-600 dark:bg-orange-950/40 dark:text-orange-300" },
  actionRequired: { border: "border-s-amber-500", icon: "bg-amber-50 text-amber-600 dark:bg-amber-950/40 dark:text-amber-300" },
  followUp: { border: "border-s-cyan-500", icon: "bg-cyan-50 text-cyan-600 dark:bg-cyan-950/40 dark:text-cyan-300" },
  mitigated: { border: "border-s-emerald-500", icon: "bg-emerald-50 text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-300" },
} as const;
