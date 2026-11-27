import * as React from "react";
import { Link } from "react-router-dom";
import { ApiError } from "@api/client";
import { Bar, BarChart, CartesianGrid, Cell, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { FinancialReportingExportButtons } from "../finance/FinancialReportingExportButtons";
import type { FinancialReportingExportInput } from "../finance/financial-reporting-export";
import { usePortfolioFinancialRollup, useProgramFinancialRollup, type ProgramPortfolioFinancialRollup } from "./api/delivery";

type Props = { kind: "Program" | "Portfolio"; id: string; enabled: boolean; scopeKey: string; entityName?: string };
type CurrencyGroup = ProgramPortfolioFinancialRollup["currencyGroups"][number];
type ProjectRow = ProgramPortfolioFinancialRollup["projects"][number];

const CHART_COLORS = ["hsl(var(--fin-navy))", "hsl(var(--fin-violet))", "hsl(var(--fin-blue))", "hsl(var(--fin-green))", "hsl(var(--fin-amber))"];

export function FinancialRollupView({ kind, id, enabled, scopeKey, entityName }: Props) {
  const { t, locale, formatCurrency, formatNumber, formatDate } = useI18n();
  const { user } = useAuth();
  const canExport = Boolean(user && hasPermission(user.permissions, kind === "Program" ? Permissions.ProgramFinancialRollup.View : Permissions.PortfolioFinancialRollup.View));
  const [fiscalYear, setFiscalYear] = React.useState<number>();
  const [fiscalYearDraft, setFiscalYearDraft] = React.useState("");
  const [fiscalYearError, setFiscalYearError] = React.useState(false);
  const initializedFiscalYearInput = React.useRef(false);
  const [page, setPage] = React.useState(1);
  const params = { fiscalYear, page, pageSize: 25 };
  const programQuery = useProgramFinancialRollup(id, params, enabled && kind === "Program", scopeKey);
  const portfolioQuery = usePortfolioFinancialRollup(id, params, enabled && kind === "Portfolio", scopeKey);
  const query = kind === "Program" ? programQuery : portfolioQuery;
  const data = query.data;

  const makeExportInput = React.useCallback((): FinancialReportingExportInput | null => {
    if (!data) return null;
    return {
      level: kind,
      entityName: entityName ?? data.entityType ?? kind,
      report: data,
      locale: locale === "ar" ? "ar" : "en",
      generatedAt: new Date().toISOString(),
      translate: t,
    };
  }, [data, kind, entityName, locale, t]);
  React.useEffect(() => {
    if (!initializedFiscalYearInput.current && data) {
      setFiscalYearDraft(String(data.fiscalYear));
      initializedFiscalYearInput.current = true;
    }
  }, [data]);
  const base = kind === "Program" ? "programManager" : "portfolioManager";
  const key = (value: string) => `${base}.financialRollup.${value}`;
  const amount = (value: number | null | undefined, currency: string | null | undefined) => value == null || currency == null ? t(key("unavailable")) : formatCurrency(value, currency);
  const projectRows = data?.projects ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.projectsTotalCount / data.projectsPageSize)) : 1;

  function applyFiscalYear(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const selectedYear = Number(fiscalYearDraft);
    if (!Number.isInteger(selectedYear) || selectedYear < 1900 || selectedYear > 2200) {
      setFiscalYearError(true);
      return;
    }
    setFiscalYearError(false);
    setFiscalYear(selectedYear);
    setPage(1);
  }

  if (query.isLoading) return <p role="status" className="py-6 text-sm text-muted-foreground">{t(key("loading"))}</p>;
  if (query.isError) {
    const restricted = query.error instanceof ApiError && query.error.status === 403;
    return <Card><CardContent className="flex items-center justify-between gap-3 py-5"><p role="alert" className="text-sm text-destructive">{t(key(restricted ? "accessRestricted" : "loadFailed"))}</p>{!restricted ? <Button size="sm" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button> : null}</CardContent></Card>;
  }
  if (!data) return null;

  const columns: [string, (group: CurrencyGroup) => string][] = [
    ["approvedBudget", group => amount(group.approvedAnnualBudget, group.currency)],
    ["activeAllocations", group => amount(group.effectiveActiveAllocations, group.currency)],
    ["availableBudget", group => amount(group.availableAnnualBudget, group.currency)],
    ["totalUnspent", group => amount(group.totalUnspentBudget, group.currency)],
    ["paidInYear", group => amount(group.actualSettlementsInFiscalYear, group.currency)],
    ["remainingPayable", group => amount(group.remainingPayableCommitments, group.currency)],
    ["reversalReleases", group => amount(group.approvedReversalReleases, group.currency)],
    ["newFunding", group => amount(group.newApprovedFunding, group.currency)],
    ["incomingFreeCarryForward", group => amount(group.approvedFreeCarryForward, group.currency)],
    ["incomingReservedCarryForward", group => amount(group.approvedReservedCarryForward, group.currency)],
    ["adjustments", group => amount(group.approvedAdjustments, group.currency)],
    ["freeCarryForward", group => amount(group.freeCarryForward, group.currency)],
    ["reservedCarryForward", group => amount(group.reservedCarryForward, group.currency)],
    ["nonCarriedBalance", group => amount(group.nonCarriedBalance, group.currency)],
  ];

  return <div className="space-y-4">
    <div className="flex flex-wrap items-end justify-between gap-3 rounded-lg border bg-card p-4">
      <div><h2 className="font-semibold">{t(key("title"))}</h2><p className="mt-1 text-sm text-muted-foreground">{t(key("fiscalYearValue"), { year: data.fiscalYear })} · {formatDate(data.fiscalYearStartDate)} - {formatDate(data.fiscalYearEndDate)}</p></div>
      <div className="flex flex-wrap items-end gap-3">
        <form className="flex flex-wrap items-end gap-2" noValidate onSubmit={applyFiscalYear}>
          <label className="grid gap-1 text-sm"><span>{t(key("fiscalYear"))}</span><input type="number" min={1900} max={2200} step={1} className="h-9 w-36 rounded-md border bg-background px-3" value={initializedFiscalYearInput.current ? fiscalYearDraft : String(fiscalYear ?? data.fiscalYear)} aria-invalid={fiscalYearError} aria-describedby={fiscalYearError ? `${kind.toLowerCase()}-fiscal-year-error` : undefined} onChange={event => { setFiscalYearDraft(event.currentTarget.value); setFiscalYearError(false); }} /></label>
          <Button type="submit" size="sm">{t(key("applyFiscalYear"))}</Button>
          {fiscalYearError ? <p id={`${kind.toLowerCase()}-fiscal-year-error`} role="alert" className="w-full text-sm text-destructive">{t(key("invalidFiscalYear"))}</p> : null}
        </form>
        {canExport ? <FinancialReportingExportButtons ready={Boolean(data)} makeInput={makeExportInput} /> : null}
      </div>
    </div>

    {data.coverage !== "Complete" || data.reconciliationStatus !== "Reconciled" ? <div role="note" className="rounded-md border border-amber-500/40 bg-amber-500/5 p-3 text-sm">
      {t(key(data.coverage === "Partial" ? "partialCoverage" : data.coverage === "NoProjects" ? "noProjects" : "provisional"))}
      {data.unresolvedLegacyPaymentRequests > 0 ? <span className="ms-1">{t(key("legacyRequests"), { count: data.unresolvedLegacyPaymentRequests })}</span> : null}
      {data.projectsWithFinancialDataGaps + data.projectsMissingApprovedAnnualBudget > 0 ? <span className="ms-1">{t(key("dataGaps"), { count: data.projectsWithFinancialDataGaps + data.projectsMissingApprovedAnnualBudget })}</span> : null}
    </div> : null}

    {data.currencyGroups.length ? data.currencyGroups.map(group => <Card key={group.currency}>
      <CardHeader><CardTitle className="text-base">{group.currency} · {t(key("projectCount"), { count: group.projectCount })}</CardTitle></CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {columns.map(([name, value]) => <div key={name} className="rounded-md border p-3"><div className="text-xs text-muted-foreground">{t(key(name))}</div><div className="mt-1 font-semibold tabular-nums">{value(group)}</div></div>)}
        </div>
        <div className="rounded-md border bg-muted/30 p-3">
          <div className="mb-2 text-xs font-medium text-muted-foreground">{t(key("lifetimeSection"))}</div>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
            <SummaryCard label={t(key("lifetimeCeiling"))} value={amount(group.lifetimeCeiling, group.currency)} />
            <SummaryCard label={t(key("availableLifetimeBudget"))} value={amount(group.availableLifetimeBudget, group.currency)} />
            <SummaryCard label={t(key("lifetimeReserved"))} value={amount(group.lifetimeReserved, group.currency)} />
            <SummaryCard label={t(key("outstandingCommitments"))} value={amount(group.outstandingCommitments, group.currency)} />
            <SummaryCard label={t(key("executedRefundsCorrections"))} value={amount(group.executedRefundsCorrections, group.currency)} />
          </div>
        </div>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <SummaryCard label={t(key("requests"))} value={group.totalPaymentRequests} detail={`${t(key("draft"))}: ${group.draftRequests} · ${t(key("pending"))}: ${group.pendingApprovalRequests}`} />
          <SummaryCard label={t(key("approvedRequests"))} value={group.approvedRequests} detail={amount(group.approvedRequestAmount, group.currency)} />
          <SummaryCard label={t(key("settlementStatus"))} value={`${group.notSettledRequests} / ${group.partiallySettledRequests} / ${group.fullySettledRequests}`} detail={t(key("notPartialFull"))} />
          <SummaryCard label={t(key("rejected"))} value={group.rejectedRequests} detail={t(key("requests"))} />
        </div>
      </CardContent>
    </Card>) : <Card><CardContent className="py-6 text-sm text-muted-foreground">{t(key(data.includedProjectCount === 0 ? "noProjects" : "noFinancials"))}</CardContent></Card>}

    {data.currencyGroups.length ? <Card>
      <CardHeader><CardTitle className="text-base">{t(key("currencyBudgetChart"))}</CardTitle><p className="mt-1 text-xs text-muted-foreground">{t(key(data.hasMixedCurrencies ? "currencyBudgetMixedHint" : "currencyBudgetHint"))}</p></CardHeader>
      <CardContent>
        <div className="h-56 min-w-0" role="img" aria-label={t(key("currencyBudgetChart"))}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data.currencyGroups.map(group => ({ currency: group.currency, approvedAnnualBudget: group.approvedAnnualBudget, lifetimeCeiling: group.lifetimeCeiling }))} margin={{ top: 8, right: 8, left: 8, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="currency" tick={{ fontSize: 12 }} />
              <YAxis tickFormatter={(value: number) => formatNumber(value, { notation: "compact", maximumFractionDigits: 1 })} tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }} width={72} />
              <Tooltip formatter={(value: number) => formatNumber(value, { maximumFractionDigits: 2 })} />
              <Legend />
              <Bar dataKey="approvedAnnualBudget" name={t(key("approvedBudget"))} fill={CHART_COLORS[0]} radius={[4, 4, 0, 0]}>
                {data.currencyGroups.map((group, index) => <Cell key={group.currency} fill={CHART_COLORS[index % CHART_COLORS.length]} />)}
              </Bar>
              <Bar dataKey="lifetimeCeiling" name={t(key("lifetimeCeiling"))} fill="hsl(var(--fin-green))" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card> : null}

    {kind === "Portfolio" && data.programBreakdowns.length ? <Card>
      <CardHeader><CardTitle className="text-base">{t(key("programBreakdown"))}</CardTitle></CardHeader>
      <CardContent className="grid gap-3 xl:grid-cols-2">{data.programBreakdowns.map((group, index) => <div key={group.programId ?? `direct-${index}`} className="rounded-md border p-3">
        <div className="flex items-center justify-between gap-2">{group.isDirectProjectGroup ? <span className="font-medium">{t(key("directProjects"))}</span> : group.programId ? <Link className="font-medium hover:text-primary" to={`/portfolios/programs/${encodeURIComponent(group.programId)}`}>{group.name}</Link> : <span className="font-medium">{group.name}</span>}<span className="text-xs text-muted-foreground">{t(key("projectCount"), { count: group.projectCount })}</span></div>
        {group.currencyGroups.map(currency => <div key={currency.currency} className="mt-3 border-t pt-3 text-sm">
          <div className="font-medium">{currency.currency}</div>
          <div className="mt-1 grid grid-cols-2 gap-x-4 gap-y-1 text-muted-foreground sm:grid-cols-4">
            <span>{t(key("approvedBudget"))}: {amount(currency.approvedAnnualBudget, currency.currency)}</span>
            <span>{t(key("activeAllocations"))}: {amount(currency.effectiveActiveAllocations, currency.currency)}</span>
            <span>{t(key("paidInYear"))}: {amount(currency.actualSettlementsInFiscalYear, currency.currency)}</span>
            <span>{t(key("remainingPayable"))}: {amount(currency.remainingPayableCommitments, currency.currency)}</span>
          </div>
        </div>)}
      </div>)}</CardContent>
    </Card> : null}

    <Card>
      <CardHeader><CardTitle className="text-base">{t(key("projectBreakdown"))}</CardTitle></CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          <SummaryCard label={t(key("closingOpen"))} value={data.closingCoverage.open} />
          <SummaryCard label={t(key("closingReview"))} value={data.closingCoverage.underReview} />
          <SummaryCard label={t(key("closingPending"))} value={data.closingCoverage.pendingApproval} />
          <SummaryCard label={t(key("closingClosed"))} value={data.closingCoverage.closed} />
          <SummaryCard label={t(key("closingStatus"))} value={t(key(data.reconciliationStatus === "Reconciled" ? "reconciled" : "provisionalLabel"))} />
        </div>
        <div className="overflow-x-auto"><Table>
          <TableHeader><TableRow>
            <TableHead>{t(key("project"))}</TableHead>{kind === "Portfolio" ? <TableHead>{t(key("relationship"))}</TableHead> : null}
            <TableHead>{t(key("currency"))}</TableHead><TableHead>{t(key("approvedBudget"))}</TableHead><TableHead>{t(key("activeAllocations"))}</TableHead>
            <TableHead>{t(key("paidInYear"))}</TableHead><TableHead>{t(key("remainingPayable"))}</TableHead><TableHead>{t(key("closingStatus"))}</TableHead>
          </TableRow></TableHeader>
          <TableBody>{projectRows.length ? projectRows.map(project => <ProjectFinancialRow key={project.projectId} project={project} kind={kind} base={base} t={t} amount={amount} />)
            : <TableRow><TableCell colSpan={kind === "Portfolio" ? 8 : 7} className="py-6 text-center text-muted-foreground">{t(key("noProjects"))}</TableCell></TableRow>}</TableBody>
        </Table></div>
        <div className="flex items-center justify-between gap-3 text-sm">
          <span className="text-muted-foreground">{t(key("page"), { page, total: totalPages, count: data.projectsTotalCount })}</span>
          <div className="flex gap-2"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>{t("common.previous")}</Button><Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage(value => Math.min(totalPages, value + 1))}>{t("common.next")}</Button></div>
        </div>
      </CardContent>
    </Card>
  </div>;
}

function ProjectFinancialRow({ project, kind, base, t, amount }: { project: ProjectRow; kind: Props["kind"]; base: string; t: (key: string, params?: Record<string, string | number>) => string; amount: (value: number | null | undefined, currency: string | null | undefined) => string }) {
  const key = (value: string) => `${base}.financialRollup.${value}`;
  const currency = project.currency;
  return <TableRow>
    <TableCell><Link className="font-medium hover:text-primary" to={`/projects/${encodeURIComponent(project.projectPublicId)}`}>{project.projectName}</Link><div className="text-xs text-muted-foreground">{t(key(project.isAvailable ? "available" : project.dataStatus))}</div></TableCell>
    {kind === "Portfolio" ? <TableCell>{project.isDirectProject ? t(key("directProject")) : project.programName ?? t(key("programProject"))}</TableCell> : null}
    <TableCell>{currency ?? t(key("unavailable"))}</TableCell><TableCell>{amount(project.approvedAnnualBudget, currency)}</TableCell>
    <TableCell>{amount(project.effectiveActiveAllocations, currency)}</TableCell><TableCell>{amount(project.actualSettlementsInFiscalYear, currency)}</TableCell>
    <TableCell>{amount(project.remainingPayableCommitments, currency)}</TableCell><TableCell>{t(key(project.closingStatus))}</TableCell>
  </TableRow>;
}

function SummaryCard({ label, value, detail }: { label: string; value: string | number; detail?: string }) {
  return <div className="rounded-md border p-3"><div className="text-xs text-muted-foreground">{label}</div><div className="mt-1 font-semibold tabular-nums">{value}</div>{detail ? <div className="mt-1 text-xs text-muted-foreground">{detail}</div> : null}</div>;
}
