import * as React from "react";
import { cn } from "@/lib/utils";

interface PageHeaderProps {
  title: string;
  description?: string;
  actions?: React.ReactNode;
  className?: string;
  children?: React.ReactNode;
}

export function PageHeader({ title, description, actions, className, children }: PageHeaderProps) {
  return (
    <div className={cn("flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between", className)}>
      <div className="space-y-0.5">
        <div className="flex items-center gap-3">
          <h1 className="text-lg font-bold tracking-tight sm:text-xl">{title}</h1>
          {children}
        </div>
        {description && (
          <p className="text-xs text-muted-foreground/80">{description}</p>
        )}
      </div>
      {actions && <div className="flex items-center gap-1.5 shrink-0">{actions}</div>}
    </div>
  );
}
