import * as React from "react";
import { cn } from "@lib/utils";
import { getStatusTone, statusToneClasses } from "@lib/status-presentation";

interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "default" | "secondary" | "destructive" | "outline" | "success" | "warning";
  status?: string;
}

const Badge = React.forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = "default", status, ...props }, ref) => {
    const tone = status ? statusToneClasses[getStatusTone(status)] : undefined;
    return (
      <span
        ref={ref}
        className={cn(
          "inline-flex items-center gap-1 whitespace-nowrap rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors",
          {
            "border-transparent bg-primary text-primary-foreground": variant === "default" && !tone && !status,
            "border-transparent bg-transparent text-muted-foreground": variant === "default" && !tone && Boolean(status),
            "border-transparent bg-secondary text-secondary-foreground": variant === "secondary",
            "border-transparent bg-transparent text-destructive": variant === "destructive",
            "border-input bg-background text-foreground": variant === "outline" && !tone,
            "border-transparent bg-transparent text-success": variant === "success",
            "border-transparent bg-transparent text-warning": variant === "warning",
          },
          tone,
          className
        )}
        {...props}
      />
    );
  }
);
Badge.displayName = "Badge";

export { Badge };
