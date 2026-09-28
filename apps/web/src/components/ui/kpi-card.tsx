"use client";

import * as React from "react";
import { cn } from "@/lib/utils";
import type { LucideIcon } from "lucide-react";

interface KpiCardProps extends React.HTMLAttributes<HTMLDivElement> {
  label: string;
  value: string | number;
  hint?: string;
  icon?: LucideIcon;
  semantic?: "navy" | "indigo" | "blue" | "amber" | "violet" | "green" | "red" | "primary";
  accent?: "success" | "warning" | "danger";
  trend?: { value: string; positive?: boolean };
}

const semanticStyles: Record<NonNullable<KpiCardProps["semantic"]>, { card: string; iconBg: string; iconText: string; bar: string }> = {
  navy: {
    card: "border-fin-navy/25 bg-fin-navy/[0.04] dark:border-fin-blue/30 dark:bg-fin-blue/[0.08]",
    iconBg: "bg-fin-navy/10 dark:bg-fin-blue/20",
    iconText: "text-fin-navy dark:text-fin-blue",
    bar: "bg-fin-navy dark:bg-fin-blue",
  },
  indigo: {
    card: "border-fin-indigo/30 bg-fin-indigo/[0.06] dark:border-fin-indigo/[0.35] dark:bg-fin-indigo/[0.12]",
    iconBg: "bg-fin-indigo/[0.12] dark:bg-fin-indigo/[0.22]",
    iconText: "text-fin-indigo",
    bar: "bg-fin-indigo",
  },
  blue: {
    card: "border-fin-blue/30 bg-fin-blue/[0.06] dark:border-fin-blue/[0.35] dark:bg-fin-blue/[0.12]",
    iconBg: "bg-fin-blue/[0.12] dark:bg-fin-blue/[0.22]",
    iconText: "text-fin-blue",
    bar: "bg-fin-blue",
  },
  amber: {
    card: "border-fin-amber/[0.35] bg-fin-amber/[0.07] dark:border-fin-amber/40 dark:bg-fin-amber/[0.12]",
    iconBg: "bg-fin-amber/[0.14] dark:bg-fin-amber/[0.24]",
    iconText: "text-fin-amber",
    bar: "bg-fin-amber",
  },
  violet: {
    card: "border-fin-violet/30 bg-fin-violet/[0.06] dark:border-fin-violet/[0.35] dark:bg-fin-violet/[0.12]",
    iconBg: "bg-fin-violet/[0.12] dark:bg-fin-violet/[0.22]",
    iconText: "text-fin-violet",
    bar: "bg-fin-violet",
  },
  green: {
    card: "border-fin-green/30 bg-fin-green/[0.06] dark:border-fin-green/[0.35] dark:bg-fin-green/[0.12]",
    iconBg: "bg-fin-green/[0.12] dark:bg-fin-green/[0.22]",
    iconText: "text-fin-green",
    bar: "bg-fin-green",
  },
  red: {
    card: "border-fin-red/30 bg-fin-red/[0.06] dark:border-fin-red/[0.35] dark:bg-fin-red/[0.12]",
    iconBg: "bg-fin-red/[0.12] dark:bg-fin-red/[0.22]",
    iconText: "text-fin-red",
    bar: "bg-fin-red",
  },
  primary: {
    card: "border-primary/30 bg-primary/[0.06] dark:border-primary/40 dark:bg-primary/[0.14]",
    iconBg: "bg-primary/10 dark:bg-primary/[0.22]",
    iconText: "text-primary",
    bar: "bg-primary",
  },
};

const KpiCard = React.forwardRef<HTMLDivElement, KpiCardProps>(
  ({ className, label, value, hint, icon: Icon, semantic, accent, trend, ...props }, ref) => {
    const semanticStyle = semantic ? semanticStyles[semantic] : null;

    const accentBorder = accent === "success"
      ? "border-l-[3px] border-l-fin-green"
      : accent === "warning"
      ? "border-l-[3px] border-l-fin-amber"
      : accent === "danger"
      ? "border-l-[3px] border-l-fin-red"
      : "";

    return (
      <div
        ref={ref}
        className={cn(
          "relative overflow-hidden rounded-lg border border-border bg-card p-4 shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md",
          semanticStyle?.card,
          accentBorder,
          className
        )}
        {...props}
      >
        {semanticStyle && <span aria-hidden="true" className={cn("pointer-events-none absolute inset-x-0 top-0 h-1", semanticStyle.bar)} />}
        <div className="flex items-center justify-between pt-0.5">
          <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
          {Icon && (
            <span className={cn("inline-flex h-8 w-8 items-center justify-center rounded-lg", semanticStyle?.iconBg ?? "bg-muted/50")}>
              <Icon className={cn("h-4 w-4", semanticStyle?.iconText ?? "text-muted-foreground")} />
            </span>
          )}
        </div>
        <p className="mt-2 text-xl font-bold tracking-tight tabular-nums">{value}</p>
        {(hint || trend) && (
          <div className="mt-1.5 flex items-center gap-2">
            {trend && (
              <span className={cn("text-xs font-medium", trend.positive ? "text-fin-green" : "text-fin-red")}>
                {trend.value}
              </span>
            )}
            {hint && <p className="text-[11px] text-muted-foreground">{hint}</p>}
          </div>
        )}
      </div>
    );
  }
);
KpiCard.displayName = "KpiCard";

export { KpiCard };
export type { KpiCardProps };
