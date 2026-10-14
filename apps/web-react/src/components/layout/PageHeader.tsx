import * as React from "react";
import { cn } from "@lib/utils";

interface PageHeaderProps {
  title: string;
  description?: string;
  actions?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}

export function PageHeader({ title, description, actions, children, className }: PageHeaderProps) {
  return (
    <div className={cn("nexus-page-header flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between", className)}>
      <div className="space-y-1">
        {children ? (
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-bold tracking-tight sm:text-2xl">{title}</h1>
            {children}
          </div>
        ) : <h1 className="text-xl font-bold tracking-tight sm:text-2xl">{title}</h1>}
        {description && <p className="text-sm text-muted-foreground">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}
