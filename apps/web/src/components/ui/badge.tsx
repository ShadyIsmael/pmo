import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-2 py-0.5 text-[9px] font-semibold uppercase tracking-wider transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
  {
    variants: {
      variant: {
        default: "border-transparent bg-primary/10 text-primary hover:bg-primary/20",
        secondary: "border-transparent bg-secondary/10 text-secondary-foreground hover:bg-secondary/20",
        destructive: "border-transparent bg-destructive/10 text-destructive hover:bg-destructive/20",
        outline: "text-foreground border-border",
        "status-green": "border-transparent bg-success/10 text-success hover:bg-success/20",
        "status-amber": "border-transparent bg-warning/10 text-warning hover:bg-warning/20",
        "status-red": "border-transparent bg-destructive/10 text-destructive hover:bg-destructive/20",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };
