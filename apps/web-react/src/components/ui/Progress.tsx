import * as React from "react";
import { cn } from "@lib/utils";

interface ProgressProps extends React.HTMLAttributes<HTMLDivElement> {
  value: number;
  tone?: "default" | "success" | "warning" | "destructive";
}

const Progress = React.forwardRef<HTMLDivElement, ProgressProps>(
  ({ className, value, tone = "default", ...props }, ref) => {
    const clamped = Math.min(100, Math.max(0, Math.round(value)));
    return (
      <div
        ref={ref}
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={clamped}
        className={cn("h-2 w-full overflow-hidden rounded-full bg-muted", className)}
        {...props}
      >
        <div
          className={cn("h-full rounded-full transition-all", {
            "bg-primary": tone === "default",
            "bg-success": tone === "success",
            "bg-warning": tone === "warning",
            "bg-destructive": tone === "destructive",
          })}
          style={{ width: `${clamped}%` }}
        />
      </div>
    );
  }
);
Progress.displayName = "Progress";

export { Progress };
