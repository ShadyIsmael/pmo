import { cn } from "@lib/utils";
import { TrendingUp, TrendingDown, Minus } from "lucide-react";
import { useI18n } from "@i18n/messages";

interface KpiCardProps {
  label: string;
  value: number;
  icon: string;
  variant?: "default" | "destructive";
  trend?: { value: number; label: string };
}

export function KpiCard({ label, value, icon, variant = "default", trend }: KpiCardProps) {
  const { formatNumber } = useI18n();
  const isDestructive = variant === "destructive";

  let trendIcon = null;
  let trendColor = "text-muted-foreground";

  if (trend) {
    if (trend.value > 0) {
      trendIcon = <TrendingUp className="h-3 w-3" />;
      trendColor = "text-green-600";
    } else if (trend.value < 0) {
      trendIcon = <TrendingDown className="h-3 w-3" />;
      trendColor = "text-red-600";
    } else {
      trendIcon = <Minus className="h-3 w-3" />;
      trendColor = "text-muted-foreground";
    }
  }

  return (
    <div className={cn("rounded-lg border bg-card p-4 shadow-sm", isDestructive && "border-destructive/20 bg-destructive/5")}>
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-muted-foreground">{label}</p>
          <p className={cn("text-2xl font-bold", isDestructive ? "text-destructive" : "text-foreground")}>
            {formatNumber(value)}
          </p>
        </div>
        <div className={cn("p-2 rounded-lg", isDestructive ? "bg-destructive/10" : "bg-primary/10")}>
          {icon === "folder" && <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" /></svg>}
          {icon === "alert-triangle" && <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>}
          {icon === "alert-circle" && <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>}
        </div>
      </div>
      {trend && (
        <div className={cn("mt-2 flex items-center gap-1 text-xs", trendColor)}>
          {trendIcon}
          <span>{Math.abs(trend.value)}% {trend.label}</span>
        </div>
      )}
    </div>
  );
}