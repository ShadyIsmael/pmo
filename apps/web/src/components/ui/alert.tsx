import * as React from "react";
import { AlertCircle, CheckCircle2, Info, TriangleAlert } from "lucide-react";
import { cn } from "@/lib/utils";

type AlertVariant = "info" | "success" | "warning" | "danger" | "neutral";

const variantStyles: Record<AlertVariant, { icon: React.ComponentType<{ className?: string }>; className: string }> = {
  info: { icon: Info, className: "border-info/25 bg-info/5 text-info" },
  success: { icon: CheckCircle2, className: "border-success/25 bg-success/5 text-success" },
  warning: { icon: TriangleAlert, className: "border-warning/30 bg-warning/5 text-warning" },
  danger: { icon: AlertCircle, className: "border-destructive/25 bg-destructive/5 text-destructive" },
  neutral: { icon: Info, className: "border-border bg-muted/35 text-muted-foreground" },
};

export interface AlertProps extends Omit<React.HTMLAttributes<HTMLDivElement>, "title"> {
  variant?: AlertVariant;
  title?: React.ReactNode;
  action?: React.ReactNode;
}

const Alert = React.forwardRef<HTMLDivElement, AlertProps>(
  ({ className, variant = "neutral", title, action, children, ...props }, ref) => {
    const { icon: Icon, className: variantClassName } = variantStyles[variant];
    return (
      <div ref={ref} role={variant === "danger" ? "alert" : "status"} className={cn("flex items-start gap-3 rounded-lg border px-3.5 py-3 text-sm", variantClassName, className)} {...props}>
        <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          {title && <p className="font-semibold leading-5">{title}</p>}
          {children && <div className={cn("leading-relaxed", title && "mt-0.5 text-current/80")}>{children}</div>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
    );
  }
);
Alert.displayName = "Alert";

export { Alert };
export type { AlertVariant };