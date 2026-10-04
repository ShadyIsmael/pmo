import * as React from "react";
import { cn } from "@lib/utils";

interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "default" | "secondary" | "destructive" | "outline";
  status?: string;
}

const Badge = React.forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant = "default", status, ...props }, ref) => {
    const statusVariants: Record<string, string> = {
      Pending: "bg-yellow-100 text-yellow-800",
      Approved: "bg-green-100 text-green-800",
      Rejected: "bg-red-100 text-red-800",
      Active: "bg-green-100 text-green-800",
      Inactive: "bg-gray-100 text-gray-800",
      Completed: "bg-blue-100 text-blue-800",
      Draft: "bg-gray-100 text-gray-800",
      High: "bg-red-100 text-red-800",
      Medium: "bg-yellow-100 text-yellow-800",
      Low: "bg-green-100 text-green-800",
    };

    const statusClass = status ? statusVariants[status] : "";

    return (
      <span
        ref={ref}
        className={cn(
          "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
          {
            "border-transparent bg-primary text-primary-foreground hover:bg-primary/80": variant === "default",
            "border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80": variant === "secondary",
            "border-transparent bg-destructive text-destructive-foreground hover:bg-destructive/80": variant === "destructive",
            "text-foreground": variant === "outline",
          },
          statusClass,
          className
        )}
        {...props}
      />
    );
  }
);
Badge.displayName = "Badge";

export { Badge };