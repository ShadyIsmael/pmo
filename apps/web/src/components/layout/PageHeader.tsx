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
    <div className={cn("nexus-page-header flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between", className)}>
      <div className="space-y-1">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-bold tracking-tight sm:text-2xl">{title}</h1>
          {children}
        </div>
        {description && (
          <p className="text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
    </div>
  );
}
