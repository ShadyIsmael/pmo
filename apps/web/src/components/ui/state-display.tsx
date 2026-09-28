"use client";

import * as React from "react";
import { cn } from "@/lib/utils";
import type { LucideIcon } from "lucide-react";
import { AlertTriangle, Inbox, RefreshCw, ShieldAlert } from "lucide-react";
import { Button } from "@/components/ui/button";

interface StateDisplayProps {
  icon?: LucideIcon;
  title: string;
  description?: string;
  action?: React.ReactNode;
  variant?: "empty" | "error" | "loading" | "permission";
  className?: string;
}

function StateDisplay({ icon: Icon, title, description, action, variant = "empty", className }: StateDisplayProps) {
  const defaultIcon = variant === "error" ? AlertTriangle : variant === "permission" ? ShieldAlert : Inbox;
  const FinalIcon = Icon ?? defaultIcon;

  const iconColor = variant === "error"
    ? "text-destructive/60"
    : variant === "permission"
    ? "text-warning/60"
    : "text-muted-foreground/40";

  const containerBg = variant === "error"
    ? "bg-destructive/5"
    : variant === "permission"
    ? "bg-warning/5"
    : "bg-muted/20";

  return (
    <div className={cn("flex min-h-40 flex-col items-center justify-center rounded-lg border border-dashed border-border px-6 py-8 text-center", containerBg, className)}>
      <FinalIcon className={cn("h-8 w-8", iconColor)} />
      <p className="mt-3 max-w-sm text-sm font-medium">{title}</p>
      {description && <p className="mt-1 max-w-md text-xs text-muted-foreground">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

function EmptyState({ title, description, action, icon, className }: Omit<StateDisplayProps, "variant">) {
  return <StateDisplay variant="empty" title={title} description={description} action={action} icon={icon} className={className} />;
}

function ErrorState({ title, description, onRetry, className }: { title: string; description?: string; onRetry?: () => void; className?: string }) {
  return (
    <StateDisplay
      variant="error"
      title={title}
      description={description}
      action={onRetry ? (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RefreshCw className="me-1.5 h-3.5 w-3.5" />
          Retry
        </Button>
      ) : undefined}
      className={className}
    />
  );
}

function PermissionDenied({ title = "Permission Required", description = "You don't have access to view this content.", className }: { title?: string; description?: string; className?: string }) {
  return <StateDisplay variant="permission" title={title} description={description} className={className} />;
}

function LoadingSkeleton({ rows = 5, className }: { rows?: number; className?: string }) {
  return (
    <div className={cn("space-y-3", className)}>
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="h-12 w-full animate-pulse rounded-md bg-muted" />
      ))}
    </div>
  );
}

export { StateDisplay, EmptyState, ErrorState, PermissionDenied, LoadingSkeleton };
