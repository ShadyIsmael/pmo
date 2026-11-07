import * as React from "react";
import { Label } from "@components/ui/Label";
import { cn } from "@lib/utils";

export function PlanningDetailSection({
  title,
  description,
  children,
  className,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={cn("space-y-3 border-b border-border/70 pb-5 last:border-b-0 last:pb-0", className)}>
      <div className="space-y-0.5">
        <h3 className="text-sm font-semibold tracking-tight">{title}</h3>
        {description ? <p className="text-xs leading-5 text-muted-foreground">{description}</p> : null}
      </div>
      {children}
    </section>
  );
}

export function PlanningDetailMetrics({ children, className }: { children: React.ReactNode; className?: string }) {
  return <div className={cn("grid grid-cols-2 gap-x-5 gap-y-4 sm:grid-cols-3", className)}>{children}</div>;
}

export function PlanningDetailMetric({ label, value, className }: { label: string; value: string; className?: string }) {
  return (
    <div className={cn("min-w-0 border-s border-border/70 ps-3", className)}>
      <p className="text-[11px] font-medium leading-4 text-muted-foreground">{label}</p>
      <p className="mt-1 break-words text-sm font-semibold leading-5 tabular-nums">{value || "—"}</p>
    </div>
  );
}

export function PlanningDetailSummaryItem({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <div className={cn("min-w-0 border-s border-border/70 ps-3", className)}>
      <dt className="text-[11px] font-medium leading-4 text-muted-foreground">{label}</dt>
      <dd className="mt-1 min-w-0 break-words text-sm font-medium leading-5">{children}</dd>
    </div>
  );
}

export function PlanningDetailField({
  id,
  label,
  hint,
  children,
  className,
}: {
  id: string;
  label: string;
  hint?: string;
  children: React.ReactNode;
  className?: string;
}) {
  const hintId = hint ? `${id}-hint` : undefined;
  return (
    <div className={cn("min-w-0 space-y-1.5", className)}>
      <Label htmlFor={id}>{label}</Label>
      {children}
      {hint ? <p id={hintId} className="text-xs leading-5 text-muted-foreground">{hint}</p> : null}
    </div>
  );
}
