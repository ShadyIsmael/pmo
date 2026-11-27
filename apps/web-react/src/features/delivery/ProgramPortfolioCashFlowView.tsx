import * as React from "react";
import { ApiError } from "@api/client";
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { CashFlowExportButtons } from "../finance/CashFlowExportButtons";
import type { CashFlowExportInput } from "../finance/cash-flow-export";
import {
  useProgramPortfolioCashFlow,
  type CashFlowDataQualityStatus,
  type CashFlowGranularity,
  type ProgramPortfolioCashFlowBucket,
  type ProgramPortfolioCashFlowCurrencyGroup,
  type ProgramPortfolioCashFlowItem,
} from "./api/program-portfolio-cash-flow";

export interface ProgramPortfolioCashFlowViewProps {
  entityType: "Program" | "Portfolio";
  entityId: string;
  entityName: string;
  initialFrom: string;
  initialTo: string;
  enabled: boolean;
  scopeKey: string;
  canExport: boolean;
  onOpenProject?: (projectPublicId: string) => void;
  onOpenCashFlowItem?: (item: ProgramPortfolioCashFlowItem) => void;
}

type MetricKey = "plannedResidual" | "committedUnpaid" | "actualGross" | "executedAdjustments" | "actualNet";
const metrics: MetricKey[] = ["plannedResidual", "committedUnpaid", "actualGross", "executedAdjustments", "actualNet"];
const CHART_COLORS = ["hsl(var(--fin-navy))", "hsl(var(--fin-violet))", "hsl(var(--fin-blue))", "hsl(var(--fin-amber))", "hsl(var(--fin-green))"];
const ALL_CURRENCIES = "__all__";
const UNKNOWN_CURRENCY = "__unknown__";

export function ProgramPortfolioCashFlowView({
  entityType,
  entityId,
  entityName,
  initialFrom,
  initialTo,
  enabled,
  scopeKey,
  canExport,
  onOpenProject,
  onOpenCashFlowItem,
}: ProgramPortfolioCashFlowViewProps) {
  const { t, locale, formatCurrency, formatNumber, formatDate } = useI18n();
  const [draftFrom, setDraftFrom] = React.useState(initialFrom);
  const [draftTo, setDraftTo] = React.useState(initialTo);
  const [period, setPeriod] = React.useState({ from: initialFrom, to: initialTo });
  const [periodError, setPeriodError] = React.useState(false);
  const [granularity, setGranularity] = React.useState<CashFlowGranularity>("Month");
  const [currencyFilter, setCurrencyFilter] = React.useState(ALL_CURRENCIES);

  React.useEffect(() => {
    setDraftFrom(initialFrom);
    setDraftTo(initialTo);
    setPeriod({ from: initialFrom, to: initialTo });
    setPeriodError(false);
  }, [initialFrom, initialTo]);

  const validPeriod = Boolean(period.from && period.to && period.from <= period.to);
  const query = useProgramPortfolioCashFlow(
    entityType,
    entityId,
    { ...period, granularity },
    enabled && validPeriod,
    scopeKey,
  );
  const data = query.data;

  const makeExportInput = React.useCallback((): CashFlowExportInput | null => {
    if (!data) return null;
    return {
      level: entityType,
      entityName,
      report: data,
      locale: locale === "ar" ? "ar" : "en",
      generatedAt: new Date().toISOString(),
      translate: t,
    };
  }, [data, entityType, entityName, locale, t]);

  function applyPeriod(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!draftFrom || !draftTo || draftFrom > draftTo) {
      setPeriodError(true);
      return;
    }
    setPeriodError(false);
    setPeriod({ from: draftFrom, to: draftTo });
  }

  const amount = React.useCallback((value: number | null | undefined, currency: string | null) => {
    if (value == null || currency == null) return t("cashFlow.unavailableValue");
    return formatCurrency(value, currency);
  }, [formatCurrency, t]);

  if (!enabled) return null;
  if (periodError) {
    return <Card><CardContent className="py-5"><p role="alert" className="text-sm text-destructive">{t("cashFlow.invalidDateRange")}</p></CardContent></Card>;
  }
  if (query.isLoading) return <p role="status" className="py-6 text-sm text-muted-foreground">{t("cashFlow.loading")}</p>;
  if (query.isError) {
    const restricted = query.error instanceof ApiError && query.error.status === 403;
    return <Card><CardContent className="flex items-center justify-between gap-3 py-5">
      <p role="alert" className="text-sm text-destructive">{t(restricted ? "cashFlow.accessRestricted" : "cashFlow.loadFailed")}</p>
      {!restricted ? <Button size="sm" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button> : null}
    </CardContent></Card>;
  }
  if (!data) return null;

  const shownGroups = data.currencyGroups.filter(group => currencyFilter === ALL_CURRENCIES
    || (currencyFilter === UNKNOWN_CURRENCY ? group.currency == null : group.currency === currencyFilter));
  const selectedCurrencyGroups = data.currencyGroups;
  const currencyOptions = selectedCurrencyGroups.map(group => ({
    value: group.currency ?? UNKNOWN_CURRENCY,
    label: group.currency ?? t("cashFlow.unknownCurrency"),
  }));

  return <div className="space-y-4">
    <div className="flex flex-wrap items-end justify-between gap-3 rounded-lg border bg-card p-4">
      <div>
        <h2 className="font-semibold">{t("cashFlow.title")}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{entityName}</p>
      </div>
      <div className="flex flex-wrap items-end gap-3">
        <form className="flex flex-wrap items-end gap-2" noValidate onSubmit={applyPeriod}>
          <label className="grid gap-1 text-sm"><span>{t("cashFlow.from")}</span><input type="date" className="h-9 rounded-md border bg-background px-3" value={draftFrom} onChange={event => { setDraftFrom(event.currentTarget.value); setPeriodError(false); }} /></label>
          <label className="grid gap-1 text-sm"><span>{t("cashFlow.to")}</span><input type="date" className="h-9 rounded-md border bg-background px-3" value={draftTo} onChange={event => { setDraftTo(event.currentTarget.value); setPeriodError(false); }} /></label>
          <Button type="submit" size="sm">{t("cashFlow.applyPeriod")}</Button>
        </form>
        <label className="grid gap-1 text-sm"><span>{t("cashFlow.granularity.label")}</span>
          <select className="h-9 rounded-md border bg-background px-3" value={granularity} onChange={event => setGranularity(event.currentTarget.value as CashFlowGranularity)}>
            <option value="Month">{t("cashFlow.granularity.month")}</option>
            <option value="Day">{t("cashFlow.granularity.day")}</option>
          </select>
        </label>
        <label className="grid gap-1 text-sm"><span>{t("cashFlow.currency")}</span>
          <select className="h-9 min-w-40 rounded-md border bg-background px-3" value={currencyFilter} onChange={event => setCurrencyFilter(event.currentTarget.value)}>
            <option value={ALL_CURRENCIES}>{t("cashFlow.allCurrencies")}</option>
            {currencyOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        <CashFlowExportButtons ready={Boolean(data)} canExport={canExport} makeInput={makeExportInput} />
      </div>
    </div>

    {data.partialCoverage || data.dataQualityStatus !== "Available" ? <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-950" role="status">
      <strong>{t(`cashFlow.status.${data.dataQualityStatus.toLowerCase()}`)}</strong>
      {data.partialCoverage ? <span className="ms-2">{t("cashFlow.partialCoverage")}</span> : null}
      {data.projectsWithUnavailableData > 0 ? <span className="ms-2">{t("cashFlow.unavailableProjectCount", { count: data.projectsWithUnavailableData })}</span> : null}
    </div> : null}
    {data.hasMixedCurrencies ? <p className="text-sm text-muted-foreground">{t("cashFlow.mixedCurrencyNotice")}</p> : null}

    {data.includedProjectCount === 0 ? <Card><CardContent className="py-6 text-sm text-muted-foreground">{t("cashFlow.noProjects")}</CardContent></Card> : null}
    {data.dataQualityStatus === "Unavailable" && data.currencyGroups.length === 0 ? <Card><CardContent className="py-6 text-sm text-muted-foreground">{t("cashFlow.unavailable")}</CardContent></Card> : null}
    {data.includedProjectCount > 0 && data.currencyGroups.length === 0 && data.dataQualityStatus !== "Unavailable" ? <Card><CardContent className="py-6 text-sm text-muted-foreground">{t("cashFlow.empty")}</CardContent></Card> : null}

    {shownGroups.map(group => <CurrencyGroupView
      key={group.currency ?? UNKNOWN_CURRENCY}
      group={group}
      amount={amount}
      formatDate={formatDate}
      formatNumber={formatNumber}
      t={t}
      onOpenCashFlowItem={onOpenCashFlowItem}
    />)}

    {data.projects.length ? <Card>
      <CardHeader><CardTitle className="text-base">{t("cashFlow.projectContributions")}</CardTitle></CardHeader>
      <CardContent className="overflow-x-auto">
        <Table>
          <TableHeader><TableRow>
            <TableHead>{t("cashFlow.project")}</TableHead>
            {entityType === "Portfolio" ? <TableHead>{t("cashFlow.relationship")}</TableHead> : null}
            <TableHead>{t("cashFlow.currency")}</TableHead>
            <TableHead>{t("cashFlow.status.label")}</TableHead>
            <TableHead>{t("cashFlow.qualityCodes")}</TableHead>
          </TableRow></TableHeader>
          <TableBody>{data.projects.map(project => <TableRow key={project.projectId}>
            <TableCell>{onOpenProject
              ? <button type="button" className="font-medium text-primary hover:underline" onClick={() => onOpenProject(project.projectPublicId)}>{project.projectName}</button>
              : project.projectName}</TableCell>
            {entityType === "Portfolio" ? <TableCell>{project.isDirectProject ? t("cashFlow.directProject") : project.programName ?? t("cashFlow.programUnavailable")}</TableCell> : null}
            <TableCell>{project.currency ?? t("cashFlow.unknownCurrency")}</TableCell>
            <TableCell><StatusLabel status={project.cashFlow.dataQualityStatus} t={t} /></TableCell>
            <TableCell>{[...new Set(project.cashFlow.dataQualityIssues.map(issue => issue.code))].join(", ") || "—"}</TableCell>
          </TableRow>)}</TableBody>
        </Table>
      </CardContent>
    </Card> : null}

    {data.dataQualityIssues.length ? <Card>
      <CardHeader><CardTitle className="text-base">{t("cashFlow.dataQuality")}</CardTitle></CardHeader>
      <CardContent className="overflow-x-auto"><Table>
        <TableHeader><TableRow><TableHead>{t("cashFlow.project")}</TableHead><TableHead>{t("cashFlow.qualityCode")}</TableHead><TableHead>{t("cashFlow.status.label")}</TableHead><TableHead>{t("cashFlow.currency")}</TableHead></TableRow></TableHeader>
        <TableBody>{data.dataQualityIssues.map((row, index) => <TableRow key={`${row.projectId}:${row.issue.code}:${row.issue.sourceId ?? index}`}>
          <TableCell>{row.projectName}</TableCell><TableCell>{row.issue.code}</TableCell><TableCell><StatusLabel status={row.issue.severity} t={t} /></TableCell><TableCell>{row.issue.currency ?? t("cashFlow.unknownCurrency")}</TableCell>
        </TableRow>)}</TableBody>
      </Table></CardContent>
    </Card> : null}
  </div>;
}

function CurrencyGroupView({
  group,
  amount,
  formatDate,
  formatNumber,
  t,
  onOpenCashFlowItem,
}: {
  group: ProgramPortfolioCashFlowCurrencyGroup;
  amount: (value: number | null | undefined, currency: string | null) => string;
  formatDate: (value: string) => string;
  formatNumber: (value: number, options?: Intl.NumberFormatOptions) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
  onOpenCashFlowItem?: (item: ProgramPortfolioCashFlowItem) => void;
}) {
  const currencyLabel = group.currency ?? t("cashFlow.unknownCurrency");
  const groupTotals = metrics.map(key => [key, totalFor(group, key)] as const);
  const chartData = group.buckets.map(bucket => ({
    period: formatDate(bucket.periodStart),
    plannedResidual: bucket.plannedResidual,
    committedUnpaid: bucket.committedUnpaid,
    actualGross: bucket.actualGross,
    actualNet: bucket.actualNet,
  }));

  return <Card>
    <CardHeader><CardTitle className="text-base">{currencyLabel}<span className="ms-2 text-sm font-normal text-muted-foreground">{t("cashFlow.contributingProjectCount", { count: group.contributingProjectCount })}</span></CardTitle></CardHeader>
    <CardContent className="space-y-4">
      {group.qualityCodes.length ? <ul className="list-inside list-disc text-xs text-amber-800">{group.qualityCodes.map(code => <li key={code}>{t("cashFlow.qualityIssue", { code })}</li>)}</ul> : null}
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {groupTotals.map(([key, value]) => <SummaryCard key={key} label={t(`cashFlow.${key}`)} value={amount(value, group.currency)} />)}
      </div>
      {group.buckets.length ? <div className="rounded-md border p-3">
        <h3 className="mb-2 text-sm font-medium">{t("cashFlow.timelineChart")}</h3>
        <p className="mb-2 text-xs text-muted-foreground">{t("cashFlow.lifecycleOverlapHint")}</p>
        <div className="h-64 min-w-0" role="img" aria-label={`${t("cashFlow.timelineChart")} · ${currencyLabel}`}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData} margin={{ top: 8, right: 8, left: 8, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="period" tick={{ fontSize: 11 }} />
              <YAxis tickFormatter={(value: number) => formatNumber(value, { notation: "compact", maximumFractionDigits: 1 })} tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }} width={72} />
              <Tooltip formatter={(value) => typeof value === "number" ? amount(value, group.currency) : String(value)} />
              <Legend />
              <Bar dataKey="plannedResidual" name={t("cashFlow.plannedResidual")} fill={CHART_COLORS[0]} radius={[3, 3, 0, 0]} />
              <Bar dataKey="committedUnpaid" name={t("cashFlow.committedUnpaid")} fill={CHART_COLORS[1]} radius={[3, 3, 0, 0]} />
              <Bar dataKey="actualGross" name={t("cashFlow.actualGross")} fill={CHART_COLORS[2]} radius={[3, 3, 0, 0]} />
              <Bar dataKey="actualNet" name={t("cashFlow.actualNet")} fill={CHART_COLORS[4]} radius={[3, 3, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div> : <p className="text-sm text-muted-foreground">{t("cashFlow.noDatedItems")}</p>}

      <div className="overflow-x-auto"><Table>
        <TableHeader><TableRow>
          <TableHead>{t("cashFlow.period")}</TableHead>
          {metrics.map(key => <TableHead key={key} className="text-end">{t(`cashFlow.${key}`)}</TableHead>)}
          <TableHead>{t("cashFlow.status.label")}</TableHead>
          <TableHead>{t("cashFlow.qualityCodes")}</TableHead>
          <TableHead>{t("cashFlow.details")}</TableHead>
        </TableRow></TableHeader>
        <TableBody>{group.buckets.length ? group.buckets.map(bucket => <BucketRow key={bucket.periodStart} bucket={bucket} currency={group.currency} amount={amount} formatDate={formatDate} t={t} onOpenCashFlowItem={onOpenCashFlowItem} />)
          : <TableRow><TableCell colSpan={9} className="py-5 text-center text-muted-foreground">{t("cashFlow.noDatedItems")}</TableCell></TableRow>}</TableBody>
      </Table></div>

      <div className="rounded-md border bg-muted/30 p-3">
        <h3 className="font-medium">{t("cashFlow.undated")}</h3>
        <div className="mt-2 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          {metrics.map(key => <SummaryCard key={key} label={t(`cashFlow.${key}`)} value={amount(group.undated[key], group.currency)} />)}
        </div>
        {group.undated.qualityCodes.length ? <p className="mt-2 text-xs text-amber-800">{group.undated.qualityCodes.map(code => t("cashFlow.qualityIssue", { code })).join(" · ")}</p> : null}
        {group.undated.items.length ? <div className="mt-3 space-y-2">{group.undated.items.map(item => <CashFlowItem key={`${item.projectId}:${item.line.sourceType}:${item.line.sourceId}:${item.line.measure}`} item={item} currency={group.currency} amount={amount} formatDate={formatDate} t={t} onOpenCashFlowItem={onOpenCashFlowItem} />)}</div>
          : <p className="mt-2 text-sm text-muted-foreground">{t("cashFlow.noUndatedItems")}</p>}
      </div>
    </CardContent>
  </Card>;
}

function BucketRow({
  bucket,
  currency,
  amount,
  formatDate,
  t,
  onOpenCashFlowItem,
}: {
  bucket: ProgramPortfolioCashFlowBucket;
  currency: string | null;
  amount: (value: number | null | undefined, currency: string | null) => string;
  formatDate: (value: string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
  onOpenCashFlowItem?: (item: ProgramPortfolioCashFlowItem) => void;
}) {
  return <TableRow>
    <TableCell>{formatDate(bucket.periodStart)}</TableCell>
    {metrics.map(key => <TableCell key={key} className="text-end tabular-nums">{amount(bucket[key], currency)}</TableCell>)}
    <TableCell><StatusLabel status={bucket.dataQualityStatus} t={t} /></TableCell>
    <TableCell className="text-xs text-amber-800">{bucket.qualityCodes.map(code => t("cashFlow.qualityIssue", { code })).join(" · ") || "—"}</TableCell>
    <TableCell>{bucket.items.length ? <details>
      <summary className="cursor-pointer text-primary">{t("cashFlow.viewItems", { count: bucket.items.length })}</summary>
      <div className="min-w-72 space-y-2 py-2">{bucket.items.map(item => <CashFlowItem key={`${item.projectId}:${item.line.sourceType}:${item.line.sourceId}:${item.line.measure}`} item={item} currency={currency} amount={amount} formatDate={formatDate} t={t} onOpenCashFlowItem={onOpenCashFlowItem} />)}</div>
    </details> : "—"}</TableCell>
  </TableRow>;
}

function CashFlowItem({
  item,
  currency,
  amount,
  formatDate,
  t,
  onOpenCashFlowItem,
}: {
  item: ProgramPortfolioCashFlowItem;
  currency: string | null;
  amount: (value: number | null | undefined, currency: string | null) => string;
  formatDate: (value: string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
  onOpenCashFlowItem?: (item: ProgramPortfolioCashFlowItem) => void;
}) {
  const line = item.line;
  return <div className="rounded border bg-card p-2 text-xs">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <span className="font-medium">{line.description ?? line.sourceType}</span>
      <span className="tabular-nums">{amount(line.amount, line.currency ?? currency)}</span>
    </div>
    <div className="mt-1 text-muted-foreground">
      {item.projectName} · {item.isDirectProject ? t("cashFlow.directProject") : item.programName ?? t("cashFlow.programUnavailable")}
      {line.expectedDate ? ` · ${t("cashFlow.expectedDate")}: ${formatDate(line.expectedDate)}` : ""}
      {line.timingVarianceDays != null ? ` · ${t("cashFlow.timingVarianceDays", { count: line.timingVarianceDays })}` : ""}
      {line.qualityCodes.length ? ` · ${line.qualityCodes.map(code => t("cashFlow.qualityIssue", { code })).join("; ")}` : ""}
    </div>
    {onOpenCashFlowItem ? <button type="button" className="mt-1 font-medium text-primary hover:underline" onClick={() => onOpenCashFlowItem(item)}>{t("cashFlow.openSource")}</button> : null}
  </div>;
}

function SummaryCard({ label, value }: { label: string; value: string | number }) {
  return <div className="rounded-md border p-3">
    <div className="text-xs text-muted-foreground">{label}</div>
    <div className="mt-1 font-semibold tabular-nums">{value}</div>
  </div>;
}

function StatusLabel({ status, t }: { status: CashFlowDataQualityStatus; t: (key: string) => string }) {
  const style = status === "Available" ? "border-emerald-300 bg-emerald-50 text-emerald-800"
    : status === "Partial" ? "border-amber-300 bg-amber-50 text-amber-900"
      : "border-rose-300 bg-rose-50 text-rose-900";
  return <span className={`inline-flex rounded-full border px-2 py-0.5 text-xs ${style}`}>{t(`cashFlow.status.${status.toLowerCase()}`)}</span>;
}

function totalFor(group: ProgramPortfolioCashFlowCurrencyGroup, metric: MetricKey): number | null {
  if (group.dataQualityStatus !== "Available") return null;
  const values = group.buckets.map(bucket => bucket[metric]);
  if (values.some(value => value == null || !Number.isFinite(value))) return null;
  return values.reduce<number>((sum, value) => sum + (value ?? 0), 0);
}
