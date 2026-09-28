"use client";

import * as React from "react";
import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { EmptyState, ErrorState } from "@/components/ui/state-display";
import type { NameType, Payload, ValueType } from "recharts/types/component/DefaultTooltipContent";

export const CHART_SERIES = [
  "hsl(var(--chart-1))",
  "hsl(var(--chart-2))",
  "hsl(var(--chart-3))",
  "hsl(var(--chart-4))",
  "hsl(var(--chart-5))",
  "hsl(var(--chart-6))",
] as const;

export function ChartContainer({ className, children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("relative min-h-0 w-full", className)} {...props}>{children}</div>;
}

interface ChartLegendItem { label: string; color: string; hidden?: boolean; }

export function ChartLegend({ items, className }: { items: ChartLegendItem[]; className?: string }) {
  return (
    <div className={cn("flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-muted-foreground", className)}>
      {items.map((item) => (
        <span key={item.label} className={cn("inline-flex items-center gap-1.5", item.hidden && "opacity-45")}>
          <span className="h-2 w-2 rounded-[3px]" style={{ backgroundColor: item.color }} aria-hidden="true" />
          <span>{item.label}</span>
        </span>
      ))}
    </div>
  );
}

export function ChartTooltipContent({ active, payload, label, formatter }: {
  active?: boolean;
  payload?: Payload<ValueType, NameType>[];
  label?: React.ReactNode;
  formatter?: (_value: ValueType, _name: NameType) => React.ReactNode;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-lg">
      {label !== undefined && <p className="mb-1.5 font-semibold">{label}</p>}
      <div className="space-y-1">
        {payload.map((entry, index) => {
          const name = entry.name ?? "";
          return (
            <div key={`${String(name)}-${index}`} className="flex items-center justify-between gap-5">
              <span className="flex items-center gap-1.5 text-muted-foreground">
                <span className="h-2 w-2 rounded-[3px]" style={{ backgroundColor: entry.color }} />
                {name}
              </span>
              <span className="font-semibold tabular-nums">{formatter ? formatter(entry.value ?? "—", name) : String(entry.value ?? "—")}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

export function ChartEmptyState({ title = "No chart data", description, className }: { title?: string; description?: string; className?: string }) {
  return <EmptyState title={title} description={description} className={cn("min-h-32", className)} />;
}

export function ChartLoadingState({ className }: { className?: string }) {
  return <div className={cn("flex min-h-32 items-center justify-center rounded-lg border border-dashed border-border bg-muted/15", className)}><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" aria-label="Loading chart" /></div>;
}

export function ChartErrorState({ title = "Unable to load chart", onRetry, className }: { title?: string; onRetry?: () => void; className?: string }) {
  return <ErrorState title={title} onRetry={onRetry} className={cn("min-h-32", className)} />;
}
