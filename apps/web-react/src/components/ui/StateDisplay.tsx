import { cn } from "@lib/utils";
import { Button } from "./Button";
import { AlertCircle } from "lucide-react";

interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: React.ReactNode;
  action?: React.ReactNode;
  className?: string;
}

export function EmptyState({ title, description, icon, action, className }: EmptyStateProps) {
  return (
    <div className={cn("flex flex-col items-center justify-center gap-4 py-12 text-center", className)}>
      {icon && <div className="text-muted-foreground">{icon}</div>}
      <div>
        <h3 className="text-sm font-medium">{title}</h3>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

interface ErrorStateProps {
  title: string;
  description?: string;
  onRetry?: () => void;
  className?: string;
}

export function ErrorState({ title, description, onRetry, className }: ErrorStateProps) {
  return (
    <div className={cn("flex flex-col items-center justify-center gap-4 py-12 text-center", className)}>
      <AlertCircle className="h-8 w-8 text-destructive" />
      <div>
        <h3 className="text-sm font-medium text-destructive">{title}</h3>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {onRetry && <Button variant="outline" size="sm" onClick={onRetry}>Retry</Button>}
    </div>
  );
}