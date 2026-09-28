import type { RiskImpactType } from "@/types/risk";

export const riskImpactTypeAccents: Record<RiskImpactType, { chip: string; dot: string }> = {
  Schedule: { chip: "border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-950/35 dark:text-blue-300", dot: "bg-blue-500" },
  Budget: { chip: "border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-800 dark:bg-violet-950/35 dark:text-violet-300", dot: "bg-violet-500" },
  Scope: { chip: "border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-800 dark:bg-indigo-950/35 dark:text-indigo-300", dot: "bg-indigo-500" },
  Quality: { chip: "border-teal-200 bg-teal-50 text-teal-700 dark:border-teal-800 dark:bg-teal-950/35 dark:text-teal-300", dot: "bg-teal-500" },
  Resources: { chip: "border-cyan-200 bg-cyan-50 text-cyan-700 dark:border-cyan-800 dark:bg-cyan-950/35 dark:text-cyan-300", dot: "bg-cyan-500" },
  Procurement: { chip: "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-950/35 dark:text-amber-300", dot: "bg-amber-500" },
  Compliance: { chip: "border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-800 dark:bg-orange-950/35 dark:text-orange-300", dot: "bg-orange-500" },
  Reputation: { chip: "border-pink-200 bg-pink-50 text-pink-700 dark:border-pink-800 dark:bg-pink-950/35 dark:text-pink-300", dot: "bg-pink-500" },
  Operations: { chip: "border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-700 dark:bg-slate-900/50 dark:text-slate-300", dot: "bg-slate-500" },
  Other: { chip: "border-gray-200 bg-gray-50 text-gray-700 dark:border-gray-700 dark:bg-gray-900/50 dark:text-gray-300", dot: "bg-gray-500" },
};
