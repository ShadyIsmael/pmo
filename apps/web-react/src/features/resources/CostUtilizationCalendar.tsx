import * as React from "react";
import { AlertTriangle, ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { SideSheet, SideSheetBody } from "@components/ui/SideSheet";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { formatCalendarDayValue, formatCalendarMonthValue, formatCalendarWeekdayValue } from "@lib/formatting/date";
import { getFirstDayOfWeekIndex } from "@lib/formatting/regional";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatNumberValue } from "@lib/formatting/number";
import { cn } from "@lib/utils";
import type { CostUtilizationDaily, CostUtilizationMetric } from "./api/cost-utilization";
import { dateKey, groupCostUtilizationByCurrency, groupCostUtilizationByDate, monthGridDays, parseDateKey } from "./cost-utilization-calendar";

interface CostUtilizationCalendarProps {
  month: Date;
  rows: readonly CostUtilizationDaily[];
  canViewCosts: boolean;
  isLoading: boolean;
  isError: boolean;
  from?: string;
  to?: string;
  selectedDay: string | null;
  sheetOpen: boolean;
  onMonthChange: (month: Date) => void;
  onDaySelect: (day: string) => void;
  onSheetOpenChange: (open: boolean) => void;
  onRetry: () => void;
}

function hours(value: number, locale: "en" | "ar"): string {
  return `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}h`;
}

function percent(value: number | null | undefined, na: string, locale: "en" | "ar"): string {
  return value == null ? na : `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}%`;
}

function money(value: number | null | undefined, currency: string | null | undefined, locale: "en" | "ar", na: string): string {
  const formatted = formatCurrencyValue(value, currency, locale, { maximumFractionDigits: 2, numberingSystem: "latn" });
  return formatted === "-" ? na : formatted;
}

function UtilizationMeter({ label, value, color }: { label: string; value: number | undefined; color: "primary" | "success" }) {
  const ratio = value == null ? 0 : Math.min(100, Math.max(0, value));
  return <div className="space-y-1" aria-label={label}>
    <div className="flex items-center justify-between gap-2 text-[10px] text-muted-foreground"><span className="truncate">{label}</span><span className="shrink-0 tabular-nums">{value == null ? "—" : `${Math.round(value)}%`}</span></div>
    <div className="h-1.5 overflow-hidden rounded-full bg-muted"><div className={cn("h-full rounded-full", color === "primary" ? "bg-primary" : "bg-success")} style={{ width: `${ratio}%` }} /></div>
  </div>;
}

function DayCell({
  date,
  month,
  rows,
  canViewCosts,
  inRange,
  onSelect,
}: {
  date: Date;
  month: Date;
  rows: readonly CostUtilizationDaily[];
  canViewCosts: boolean;
  inRange: boolean;
  onSelect: () => void;
}) {
  const { t, locale } = useI18n();
  const key = dateKey(date);
  const outsideMonth = date.getMonth() !== month.getMonth() || date.getFullYear() !== month.getFullYear();
  const isToday = key === dateKey(new Date());
  const capacity = rows.reduce((total, row) => total + row.capacityHours, 0);
  const planned = rows.reduce((total, row) => total + row.plannedHours, 0);
  const actual = rows.reduce((total, row) => total + row.actualHours, 0);
  const plannedUtilization = capacity > 0 ? planned / capacity * 100 : undefined;
  const actualUtilization = capacity > 0 ? actual / capacity * 100 : undefined;
  const currencyMetrics = groupCostUtilizationByCurrency(rows);
  const label = `${formatCalendarDayValue(key, locale)}. ${t("costUtilization.plannedWork")}: ${hours(planned, locale)}. ${t("costUtilization.actualWork")}: ${hours(actual, locale)}.`;

  return <button
    type="button"
    role="gridcell"
    aria-label={label}
    onClick={onSelect}
    className={cn(
      "flex min-h-36 min-w-0 flex-col gap-2 border-b border-e p-2 text-start transition-colors hover:bg-muted/50 focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring sm:min-h-40 sm:p-3",
      outsideMonth && "bg-muted/20 text-muted-foreground",
      isToday && "bg-primary/5",
    )}
  >
    <span className={cn("flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold tabular-nums", isToday && "bg-primary text-primary-foreground", outsideMonth && "text-muted-foreground/70")}>
      {formatNumberValue(date.getDate(), locale, { useGrouping: false })}
    </span>
    {!inRange ? <span className="text-[10px] leading-snug text-muted-foreground">{t("costUtilization.outsideRange")}</span> : rows.length === 0 ? <span className="text-[10px] text-muted-foreground">{t("costUtilization.noDailyData")}</span> : <>
      {canViewCosts ? <div className="space-y-0.5">
        {currencyMetrics.map((metric, index) => <div key={`${metric.currency ?? "unavailable"}-${index}`} className="flex min-w-0 items-baseline justify-between gap-1">
          <span className="truncate text-[10px] text-muted-foreground">{metric.currency ?? t("costUtilization.na")}</span>
          <span className="truncate text-xs font-semibold tabular-nums">{money(metric.actualLaborCost, metric.currency, locale, t("costUtilization.na"))}</span>
        </div>)}
      </div> : <span className="text-[10px] text-muted-foreground">{t("costUtilization.na")}</span>}
      <div className="mt-auto space-y-1.5">
        <UtilizationMeter label={t("costUtilization.plannedUtilization")} value={plannedUtilization} color="primary" />
        <UtilizationMeter label={t("costUtilization.actualUtilization")} value={actualUtilization} color="success" />
      </div>
    </>}
  </button>;
}

function CalendarGrid({
  month,
  byDate,
  canViewCosts,
  from,
  to,
  isLoading,
  onDaySelect,
}: {
  month: Date;
  byDate: Map<string, CostUtilizationDaily[]>;
  canViewCosts: boolean;
  from?: string;
  to?: string;
  isLoading: boolean;
  onDaySelect: (day: string) => void;
}) {
  const { locale } = useI18n();
  const firstDayOfWeek = getFirstDayOfWeekIndex();
  const days = React.useMemo(() => monthGridDays(month, firstDayOfWeek), [month, firstDayOfWeek]);
  const weekdays = days.slice(0, 7);
  const weeks = Array.from({ length: 6 }, (_, index) => days.slice(index * 7, index * 7 + 7));

  return <div className="overflow-x-auto rounded-md border">
    <div className="min-w-[720px]" role="grid" aria-label={formatCalendarMonthValue(month, locale)}>
      <div className="grid grid-cols-7 bg-muted/40" role="row">
        {weekdays.map((day) => <div key={day.getDay()} role="columnheader" className="border-b border-e px-2 py-2 text-start text-xs font-medium text-muted-foreground sm:px-3">{formatCalendarWeekdayValue(day, locale, "short")}</div>)}
      </div>
      {weeks.map((week, index) => <div key={index} className="grid grid-cols-7" role="row">
        {week.map((day) => {
          const key = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, "0")}-${String(day.getDate()).padStart(2, "0")}`;
          const inRange = (!from || key >= from) && (!to || key <= to);
          return isLoading
            ? <div key={key} role="gridcell" className="min-h-36 border-b border-e p-2 sm:min-h-40 sm:p-3"><Skeleton className="mb-3 h-7 w-7 rounded-full" /><Skeleton className="mb-3 h-4 w-20" /><Skeleton className="mb-2 h-1.5 w-full" /><Skeleton className="h-1.5 w-4/5" /></div>
            : <DayCell key={key} date={day} month={month} rows={inRange ? byDate.get(key) ?? [] : []} canViewCosts={canViewCosts} inRange={inRange} onSelect={() => onDaySelect(key)} />;
        })}
      </div>)}
    </div>
  </div>;
}

function CostBreakdown({ metric, locale }: { metric: CostUtilizationMetric; locale: "en" | "ar" }) {
  const { t } = useI18n();
  const values: Array<[string, number | undefined]> = [
    ["plannedCost", metric.plannedLaborCost],
    ["actualCost", metric.actualLaborCost],
    ["variance", metric.laborCostVariance],
    ["overtimeCost", metric.overtimeCost],
    ["unusedCost", metric.unusedCapacityCost],
    ["totalCost", metric.totalResourceCost],
  ];
  return <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">{values.map(([key, value]) => <div key={key} className="min-w-0 rounded-md border p-3">
    <div className="text-xs text-muted-foreground">{t(`costUtilization.${key}`)}</div>
    <div className="mt-1 truncate text-sm font-semibold tabular-nums">{money(value, metric.currency, locale, t("costUtilization.na"))}</div>
  </div>)}</div>;
}

function DayDetailsSideSheet({
  open,
  day,
  rows,
  canViewCosts,
  isLoading,
  isError,
  onOpenChange,
  onRetry,
}: {
  open: boolean;
  day: string | null;
  rows: readonly CostUtilizationDaily[];
  canViewCosts: boolean;
  isLoading: boolean;
  isError: boolean;
  onOpenChange: (open: boolean) => void;
  onRetry: () => void;
}) {
  const { t, locale } = useI18n();
  const selected = day ? parseDateKey(day) : null;
  const currencyMetrics = React.useMemo(() => groupCostUtilizationByCurrency(rows), [rows]);
  const activities = React.useMemo(() => rows.flatMap((row) => (row.activities ?? []).map((activity, index) => ({
    ...activity,
    resourceName: row.resourceName,
    key: `${row.resourceId}-${activity.planActivityId ?? "unassigned"}-${activity.projectId ?? "none"}-${index}`,
  }))), [rows]);
  const capacity = rows.reduce((total, row) => total + row.capacityHours, 0);
  const planned = rows.reduce((total, row) => total + row.plannedHours, 0);
  const actual = rows.reduce((total, row) => total + row.actualHours, 0);
  const actualUtilization = capacity > 0 ? actual / capacity * 100 : undefined;

  return <SideSheet
    open={open}
    onOpenChange={onOpenChange}
    title={t("costUtilization.dayDetails")}
    description={selected ? formatCalendarDayValue(day!, locale) : undefined}
    size="xl"
    closeLabel={t("common.close")}
  >
    <SideSheetBody className="space-y-5">
      {isLoading ? <div className="space-y-4" role="status" aria-label={t("common.loading")}><Skeleton className="h-24" /><Skeleton className="h-48" /><Skeleton className="h-52" /></div>
        : isError ? <ErrorState title={t("costUtilization.noData")} description={t("common.errorLoading")} onRetry={onRetry} />
          : rows.length === 0 ? <EmptyState title={t("costUtilization.noDayData")} description={t("costUtilization.noDayDataDescription")} />
            : <>
              {!canViewCosts ? <div className="flex items-start gap-2 rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{t("costUtilization.costPermission")}</div> : null}
              <Card>
                <CardHeader><CardTitle>{t("costUtilization.dailySummary")}</CardTitle></CardHeader>
                <CardContent className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                  <div><p className="text-xs text-muted-foreground">{t("costUtilization.capacity")}</p><p className="mt-1 font-semibold tabular-nums">{hours(capacity, locale)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("costUtilization.plannedWork")}</p><p className="mt-1 font-semibold tabular-nums">{hours(planned, locale)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("costUtilization.actualWork")}</p><p className="mt-1 font-semibold tabular-nums">{hours(actual, locale)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("costUtilization.actualUtilization")}</p><p className="mt-1 font-semibold tabular-nums">{percent(actualUtilization, t("costUtilization.na"), locale)}</p></div>
                </CardContent>
              </Card>

              {canViewCosts ? <Card>
                <CardHeader><CardTitle>{t("costUtilization.currencyCosts")}</CardTitle></CardHeader>
                <CardContent className="space-y-4">{currencyMetrics.map((metric, index) => <div key={`${metric.currency ?? "unavailable"}-${index}`} className="space-y-3">
                  <div className="flex items-center gap-2 text-sm font-medium">{metric.currency ? <span className="rounded bg-primary/10 px-2 py-1 text-xs">{metric.currency}</span> : <span className="text-muted-foreground">{t("costUtilization.na")}</span>}<span>{t("costUtilization.actualCost")}</span></div>
                  <CostBreakdown metric={metric} locale={locale} />
                </div>)}</CardContent>
              </Card> : null}

              <Card>
                <CardHeader><CardTitle>{t("costUtilization.resourceBreakdown")}</CardTitle></CardHeader>
                <CardContent className="p-0">
                  <div className="overflow-x-auto">
                    <Table className="min-w-[680px]">
                      <TableHeader><TableRow><TableHead>{t("costUtilization.resource")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead>{canViewCosts ? <><TableHead>{t("costUtilization.plannedCost")}</TableHead><TableHead>{t("costUtilization.actualCost")}</TableHead></> : null}</TableRow></TableHeader>
                      <TableBody>{rows.map((row) => <TableRow key={`${row.resourceId}-${row.currency ?? ""}`}>
                        <TableCell className="font-medium">{row.resourceName}<span className="ms-2 text-xs text-muted-foreground">{row.currency ?? t("costUtilization.na")}</span></TableCell>
                        <TableCell className="tabular-nums">{hours(row.plannedHours, locale)}</TableCell>
                        <TableCell className="tabular-nums">{hours(row.actualHours, locale)}</TableCell>
                        {canViewCosts ? <><TableCell className="tabular-nums">{money(row.plannedLaborCost, row.currency, locale, t("costUtilization.na"))}</TableCell><TableCell className="tabular-nums">{money(row.actualLaborCost, row.currency, locale, t("costUtilization.na"))}</TableCell></> : null}
                      </TableRow>)}</TableBody>
                    </Table>
                  </div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader><CardTitle>{t("costUtilization.activities")}</CardTitle></CardHeader>
                <CardContent className="p-0">
                  {activities.length === 0 ? <EmptyState title={t("costUtilization.noActivities")} /> : <div className="overflow-x-auto">
                    <Table className="min-w-[720px]">
                      <TableHeader><TableRow><TableHead>{t("costUtilization.activity")}</TableHead><TableHead>{t("costUtilization.resource")}</TableHead><TableHead>{t("costUtilization.project")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead></TableRow></TableHeader>
                      <TableBody>{activities.map((activity) => {
                        const label = activity.activityName ?? (activity.planActivityId ? t("costUtilization.activityNameUnavailable") : t("costUtilization.unassignedActivity"));
                        return <TableRow key={activity.key}>
                          <TableCell className="font-medium"><div>{label}</div>{activity.workItemTitles.length > 0 ? <div className="mt-1 text-xs font-normal text-muted-foreground">{activity.workItemTitles.join(", ")}</div> : null}</TableCell>
                          <TableCell>{activity.resourceName}</TableCell><TableCell>{activity.projectName ?? t("costUtilization.na")}</TableCell>
                          <TableCell className="tabular-nums">{hours(activity.plannedHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(activity.actualHours, locale)}</TableCell>
                        </TableRow>;
                      })}</TableBody>
                    </Table>
                  </div>}
                </CardContent>
              </Card>
            </>}
    </SideSheetBody>
  </SideSheet>;
}

export function CostUtilizationCalendar({
  month,
  rows,
  canViewCosts,
  isLoading,
  isError,
  from,
  to,
  selectedDay,
  sheetOpen,
  onMonthChange,
  onDaySelect,
  onSheetOpenChange,
  onRetry,
}: CostUtilizationCalendarProps) {
  const { t, locale } = useI18n();
  const byDate = React.useMemo(() => groupCostUtilizationByDate(rows), [rows]);
  const onNavigate = (offset: number) => onMonthChange(new Date(month.getFullYear(), month.getMonth() + offset, 1));

  return <>
    <Card>
      <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div><CardTitle>{t("costUtilization.calendarView")}</CardTitle><p className="mt-1 text-sm text-muted-foreground">{t("costUtilization.calendarDescription")}</p></div>
        <div className="flex items-center justify-between gap-3 sm:justify-end">
          <Button variant="outline" size="icon-sm" aria-label={t("costUtilization.previousMonth")} onClick={() => onNavigate(-1)}><ChevronLeft className="h-4 w-4 rtl:rotate-180" /></Button>
          <h2 className="min-w-36 text-center text-sm font-semibold">{formatCalendarMonthValue(month, locale)}</h2>
          <Button variant="outline" size="icon-sm" aria-label={t("costUtilization.nextMonth")} onClick={() => onNavigate(1)}><ChevronRight className="h-4 w-4 rtl:rotate-180" /></Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {isError ? <ErrorState title={t("costUtilization.noData")} description={t("common.errorLoading")} onRetry={onRetry} /> : null}
        {!isLoading && !isError && rows.length === 0 ? <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">{t("costUtilization.noData")}</div> : null}
        <CalendarGrid month={month} byDate={byDate} canViewCosts={canViewCosts} from={from} to={to} isLoading={isLoading} onDaySelect={onDaySelect} />
        <div className="flex flex-wrap gap-x-5 gap-y-2 text-xs text-muted-foreground">
          <span className="flex items-center gap-2"><span className="h-2 w-2 rounded-full bg-primary" />{t("costUtilization.plannedUtilization")}</span>
          <span className="flex items-center gap-2"><span className="h-2 w-2 rounded-full bg-success" />{t("costUtilization.actualUtilization")}</span>
          {canViewCosts ? <span>{t("costUtilization.actualCost")}</span> : <span>{t("costUtilization.costPermission")}</span>}
        </div>
      </CardContent>
    </Card>
    <DayDetailsSideSheet
      open={sheetOpen}
      day={selectedDay}
      rows={selectedDay ? byDate.get(selectedDay) ?? [] : []}
      canViewCosts={canViewCosts}
      isLoading={isLoading && Boolean(selectedDay)}
      isError={isError && Boolean(selectedDay)}
      onOpenChange={onSheetOpenChange}
      onRetry={onRetry}
    />
  </>;
}
