import * as React from "react";
import { Link } from "react-router-dom";
import { AlertCircle, Clock3 } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";
import { Permissions } from "@lib/permissions";
import { useTimeEntrySummary } from "@features/my-work/api/myWork";

const viewPermissions = Object.values(Permissions.TimeTracking);

function localDateParameter(date: Date): string {
  const year = String(date.getFullYear()).padStart(4, "0");
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function getCurrentWeekDateRange(today = new Date()): { from: string; to: string } {
  const end = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const start = new Date(end);
  start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
  return { from: localDateParameter(start), to: localDateParameter(end) };
}

export function HoursThisWeekWidget({ permissions }: { permissions: readonly string[] }) {
  const { t } = useI18n();
  const canView = viewPermissions.some((permission) => permissions.includes(permission));
  const dateRange = React.useMemo(() => getCurrentWeekDateRange(), []);
  const summary = useTimeEntrySummary(dateRange, canView, 0);

  if (!canView) {
    return <div className="flex min-h-32 items-center justify-center text-center text-sm text-muted-foreground">{t("dashboard.timeTrackingPermissionRequired")}</div>;
  }
  if (summary.isLoading) {
    return <div className="flex min-h-32 items-center justify-center gap-2 text-sm text-muted-foreground" role="status"><span className="h-4 w-4 animate-spin rounded-full border-2 border-current border-e-transparent" />{t("common.loading")}</div>;
  }
  if (summary.isError) {
    return <div className="flex min-h-32 flex-col items-center justify-center gap-2 text-center" role="alert"><p className="flex items-center gap-2 text-sm text-destructive"><AlertCircle className="h-4 w-4" />{t("timeTracking.loadError")}</p><Button size="sm" variant="outline" onClick={() => void summary.refetch()}>{t("common.retry")}</Button></div>;
  }

  const actualHours = summary.data?.actualHours ?? 0;
  const westernHours = new Intl.NumberFormat("en-US", { maximumFractionDigits: 2 }).format(actualHours);
  return <Link to="/my-work/timesheets" className="flex min-h-32 items-center gap-3 rounded-md px-3 py-4 transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary"><Clock3 className="h-5 w-5" /></span>
    <span className="min-w-0"><span className="block text-2xl font-semibold tabular-nums">{westernHours} <span className="text-sm font-normal text-muted-foreground">{t("timeTracking.hours")}</span></span><span className="mt-1 block text-xs text-muted-foreground">{t("dashboard.hoursThisWeekDescription")}</span></span>
  </Link>;
}
