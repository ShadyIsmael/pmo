import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";
import { getStatusBadgeVariant, statusToneClasses } from "@/lib/status-presentation";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider transition-all duration-150 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
  {
    variants: {
      variant: {
        default: "border-transparent bg-primary/10 text-primary hover:bg-primary/20",
        secondary: "border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80",
        destructive: "border-transparent bg-destructive/10 text-destructive hover:bg-destructive/20",
        outline: "text-foreground border-border",
        success: "border-transparent bg-success/10 text-success hover:bg-success/20",
        warning: "border-transparent bg-warning/10 text-warning hover:bg-warning/20",
        danger: "border-transparent bg-destructive/10 text-destructive hover:bg-destructive/20",
        info: "border-transparent bg-info/10 text-info hover:bg-info/20",
        "status-green": statusToneClasses.green,
        "status-amber": statusToneClasses.amber,
        "status-red": statusToneClasses.red,
        "status-blue": statusToneClasses.blue,
        "status-neutral": statusToneClasses.neutral,
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {
  /** Optional domain status; when present it resolves to the shared semantic tone. */
  status?: string | null;
}

function Badge({ className, variant, status, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant: status ? getStatusBadgeVariant(status) : variant }), className)} {...props} />;
}

export { Badge, badgeVariants };
