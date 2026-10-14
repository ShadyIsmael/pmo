import * as React from "react";
import { cn } from "@lib/utils";
import { TrendingUp, TrendingDown, Minus } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useI18n } from "@i18n/messages";

interface NexusKpiCardProps extends React.HTMLAttributes<HTMLDivElement> {
  label: string;
  value: string | number;
  hint?: string;
  icon?: LucideIcon | string;
  semantic?: "navy" | "indigo" | "blue" | "amber" | "violet" | "green" | "red" | "primary";
  variant?: "default" | "destructive";
  trend?: { value: number; label: string } | { value: string; positive?: boolean };
}

const semanticStyles: Record<string, { card: string; iconBg: string; iconText: string; bar: string }> = {
  navy: {
    card: "border-fin-navy/25 bg-fin-navy/[0.04] dark:border-fin-blue/30 dark:bg-fin-blue/[0.08]",
    iconBg: "bg-fin-navy/10 dark:bg-fin-blue/20",
    iconText: "text-fin-navy dark:text-fin-blue",
    bar: "bg-fin-navy dark:bg-fin-blue",
  },
  indigo: {
    card: "border-fin-indigo/30 bg-fin-indigo/[0.06]",
    iconBg: "bg-fin-indigo/[0.12]",
    iconText: "text-fin-indigo",
    bar: "bg-fin-indigo",
  },
  blue: {
    card: "border-fin-blue/30 bg-fin-blue/[0.06]",
    iconBg: "bg-fin-blue/[0.12]",
    iconText: "text-fin-blue",
    bar: "bg-fin-blue",
  },
  amber: {
    card: "border-fin-amber/[0.35] bg-fin-amber/[0.07]",
    iconBg: "bg-fin-amber/[0.14]",
    iconText: "text-fin-amber",
    bar: "bg-fin-amber",
  },
  violet: {
    card: "border-fin-violet/30 bg-fin-violet/[0.06]",
    iconBg: "bg-fin-violet/[0.12]",
    iconText: "text-fin-violet",
    bar: "bg-fin-violet",
  },
  green: {
    card: "border-fin-green/30 bg-fin-green/[0.06]",
    iconBg: "bg-fin-green/[0.12]",
    iconText: "text-fin-green",
    bar: "bg-fin-green",
  },
  red: {
    card: "border-fin-red/30 bg-fin-red/[0.06]",
    iconBg: "bg-fin-red/[0.12]",
    iconText: "text-fin-red",
    bar: "bg-fin-red",
  },
  primary: {
    card: "border-primary/30 bg-primary/[0.06]",
    iconBg: "bg-primary/10",
    iconText: "text-primary",
    bar: "bg-primary",
  },
};

function isLucideIcon(icon: unknown): icon is LucideIcon {
  return typeof icon === "function" || (typeof icon === "object" && icon !== null);
}

export function KpiCard({ className, label, value, hint, icon: IconProp, semantic, variant = "default", trend, ...props }: NexusKpiCardProps) {
  const { formatNumber } = useI18n();
  const semanticStyle = semantic ? semanticStyles[semantic] : null;
  const isDestructive = variant === "destructive";

  // Legacy string-icon support (dashboard widgets)
  if (typeof IconProp === "string") {
    let trendIcon = null;
    let trendColor = "text-muted-foreground";
    const numericTrend = trend && typeof trend === "object" && "label" in trend ? (trend as { value: number; label: string }) : null;
    if (numericTrend) {
      if (numericTrend.value > 0) {
        trendIcon = <TrendingUp className="h-3 w-3" />;
        trendColor = "text-green-600";
      } else if (numericTrend.value < 0) {
        trendIcon = <TrendingDown className="h-3 w-3" />;
        trendColor = "text-red-600";
      } else {
        trendIcon = <Minus className="h-3 w-3" />;
      }
    }
    return (
      <div className={cn("rounded-lg border bg-card p-4 shadow-sm", isDestructive && "border-destructive/20 bg-destructive/5", className)} {...props}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-muted-foreground">{label}</p>
            <p className={cn("text-2xl font-bold", isDestructive ? "text-destructive" : "text-foreground")}>
              {typeof value === "number" ? formatNumber(value) : value}
            </p>
          </div>
          <div className={cn("rounded-lg p-2", isDestructive ? "bg-destructive/10" : "bg-primary/10")}>
            {IconProp === "folder" && <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" /></svg>}
            {IconProp === "alert-triangle" && <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>}
            {IconProp === "alert-circle" && <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>}
          </div>
        </div>
        {numericTrend && (
          <div className={cn("mt-2 flex items-center gap-1 text-xs", trendColor)}>
            {trendIcon}
            <span>{Math.abs(numericTrend.value)}% {numericTrend.label}</span>
          </div>
        )}
      </div>
    );
  }

  const Icon = isLucideIcon(IconProp) ? (IconProp as LucideIcon) : undefined;
  const stringTrend = trend && typeof trend === "object" && "positive" in trend ? (trend as { value: string; positive?: boolean }) : null;

  return (
    <div
      className={cn(
        "relative overflow-hidden rounded-lg border border-border bg-card p-4 shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md",
        semanticStyle?.card,
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
      <p className="mt-2 text-xl font-bold tabular-nums tracking-tight">{value}</p>
      {(hint || stringTrend) && (
        <div className="mt-1.5 flex items-center gap-2">
          {stringTrend && (
            <span className={cn("text-xs font-medium", stringTrend.positive ? "text-fin-green" : "text-fin-red")}>{stringTrend.value}</span>
          )}
          {hint && <p className="text-[11px] text-muted-foreground">{hint}</p>}
        </div>
      )}
    </div>
  );
}
