import * as React from "react";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { AlertTriangle, ArrowDown, ArrowUp, Banknote, BarChart2, Landmark, Layers, PieChart, TrendingDown, TrendingUp, Wallet, type LucideIcon, XCircle, CheckCircle, AlertCircle, Info } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Badge } from "@components/ui/Badge";
import { Progress } from "@components/ui/Progress";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, hasAnyPermission, Permissions } from "@lib/permissions";
import { useManagement } from "@api/management";
import { Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart as RechartsPieChart, PolarGrid, RadialBarChart, ResponsiveContainer, Sector, Tooltip, XAxis, YAxis } from "recharts";
import { useProjectFinancialReporting } from "./api/project-financials";
import { useProjectAnnualBudgets } from "./api/project-annual-budgets";
import { CashFlowCalendar, type CashFlowFiscalYearOption } from "./CashFlowCalendar";
import { CashFlowForecastDashboard } from "./CashFlowForecastDashboard";
import { ProjectPaymentRequests } from "./ProjectPaymentRequests";
import type { CashFlowLineItem } from "./api/project-cash-flow";
import { FinancialReportingExportButtons } from "../finance/FinancialReportingExportButtons";
import type { FinancialReportingExportInput } from "../finance/financial-reporting-export";
import "./project-finance.css";

type ProjectContext = { projectId: string; project: { id: string; name?: string; currency?: string } };

const COLORS = {
  lifetimeCeiling: "hsl(var(--fin-navy))",
  lifetimeReserved: "hsl(var(--warning))",
  grossActualPaid: "hsl(var(--fin-violet))",
  netActualPaid: "hsl(var(--fin-blue))",
  available: "hsl(var(--fin-green))",
  consumed: "hsl(var(--fin-red))",
  adjustments: "hsl(var(--fin-amber))",
  outstanding: "hsl(var(--fin-orange))",
};

function money(value: number | null | undefined, currency: string, formatCurrency: (value: number, currency?: string) => string) {
  return typeof value === "number" && Number.isFinite(value) && currency ? formatCurrency(value, currency) : "—";
}

function percent(value: number | null | undefined, formatNumber: (value: number, options?: Intl.NumberFormatOptions) => string) {
  return typeof value === "number" && Number.isFinite(value) ? formatNumber(value, { style: "percent", minimumFractionDigits: 1, maximumFractionDigits: 1 }) : "—";
}

function finite(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

type MetricTone = "neutral" | "approved" | "planned" | "actual" | "available" | "danger" | "warning";

function SummaryCard({ label, value, hint, tone = "neutral", metricKey, icon: Icon, featured }: {
  label: string; value: string; hint?: string; tone?: MetricTone; metricKey?: string; icon?: LucideIcon; featured?: boolean;
}) {
  return <Card className={featured ? "finance-metric finance-feature" : "finance-metric"} data-tone={tone}>
    <div className="flex items-center gap-2">
      {Icon && <span className="finance-icon"><Icon aria-hidden="true" size={18} /></span>}
      <p className="text-sm font-medium">{label}</p>
    </div>
    <p data-testid={metricKey && `finance-metric-${metricKey}`} className="finance-value"><bdi>{value}</bdi></p>
    {hint && <p className="text-xs leading-relaxed text-muted-foreground">{hint}</p>}
  </Card>;
}

function DataQualityBadge({ status, t }: { status: string; t: (key: string) => string }) {
  const config = {
    Available: { label: t("dataQualityAvailable"), variant: "default" as const, icon: CheckCircle, color: "text-success" },
    Partial: { label: t("dataQualityPartial"), variant: "secondary" as const, icon: AlertCircle, color: "text-warning" },
    Unavailable: { label: t("dataQualityUnavailable"), variant: "destructive" as const, icon: XCircle, color: "text-destructive" },
  }[status] || { label: status, variant: "outline" as const, icon: Info, color: "text-muted-foreground" };

  const Icon = config.icon;
  return (
    <Badge variant={config.variant} className="gap-1.5">
      <Icon className={`h-3.5 w-3.5 ${config.color}`} aria-hidden="true" />
      {config.label}
    </Badge>
  );
}

function DataQualityDetails({ details, t }: { details: string[]; t: (key: string) => string }) {
  if (!details.length) return null;
  return (
    <Card className="data-quality-details">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <AlertCircle className="h-4 w-4 text-warning" aria-hidden="true" />
          {t("dataQuality")}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="space-y-1.5 text-sm" role="list">
          {details.map((detail, i) => (
            <li key={i} className="flex items-start gap-2 text-muted-foreground">
              <span className="mt-1 h-1.5 w-1.5 rounded-full bg-current shrink-0" aria-hidden="true" />
              <span>{detail}</span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function LegacyOverlapWarning({ t }: { t: (key: string) => string }) {
  return (
    <div className="legacy-overlap-warning flex items-start gap-3 rounded-md border border-warning/30 bg-warning/5 p-3" role="alert">
      <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-warning" aria-hidden="true" />
      <div>
        <p className="text-sm font-medium text-warning">{t("legacyCommitmentOverlapWarning")}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("dataQualityLegacyCommitmentOverlap")}</p>
      </div>
    </div>
  );
}

function LifetimeBudgetChart({ lifetimeCeiling, lifetimeReserved, grossActualPaid, currency, formatCurrency, t }: {
  lifetimeCeiling: number | null;
  lifetimeReserved: number;
  grossActualPaid: number | null;
  currency: string;
  formatCurrency: (value: number, currency?: string) => string;
  t: (key: string) => string;
}) {
  const consumed = finite(grossActualPaid) ? grossActualPaid! : 0;
  const reserved = finite(lifetimeReserved) ? lifetimeReserved : 0;
  const ceiling = finite(lifetimeCeiling) ? lifetimeCeiling! : 0;
  const available = Math.max(0, ceiling - consumed - reserved);

  if (ceiling <= 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("lifetimeBudgetBreakdown")}</CardTitle>
          <p className="text-xs text-muted-foreground">{t("lifetimeCeilingHint")}</p>
        </CardHeader>
        <CardContent className="flex items-center justify-center h-48 text-muted-foreground">{t("chartNoData")}</CardContent>
      </Card>
    );
  }

  const data = [
    { name: t("consumed"), value: consumed, color: COLORS.consumed },
    { name: t("reserved"), value: reserved, color: COLORS.lifetimeReserved },
    { name: t("available"), value: available, color: COLORS.available },
  ].filter(d => d.value > 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("lifetimeBudgetBreakdown")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("lifetimeCeilingHint")}</p>
      </CardHeader>
      <CardContent>
        <div className="h-48 min-w-0" role="img" aria-label={t("lifetimeBudgetBreakdown")}>
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={data}
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={100}
                fill="#8884d8"
                paddingAngle={2}
                dataKey="value"
                nameKey="name"
                label={({ name, percent }) => `${name} ${(percent * 100).toFixed(1)}%`}
                labelLine={false}
              >
                {data.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip formatter={(value: number) => money(value, currency, formatCurrency)} />
            </PieChart>
          </ResponsiveContainer>
        </div>
        <div className="mt-4 grid gap-2 sm:grid-cols-3">
          {[
            { key: "consumed", label: t("consumed"), value: consumed, color: COLORS.consumed },
            { key: "reserved", label: t("reserved"), value: reserved, color: COLORS.lifetimeReserved },
            { key: "available", label: t("available"), value: available, color: COLORS.available },
          ].map(({ key, label, value, color }) => (
            <div key={key} className="flex items-center justify-between gap-3">
              <span className="flex items-center gap-2 text-muted-foreground"><span aria-hidden="true" className={`h-2 w-2 rounded-full ${color.replace("hsl(var(", "bg-").replace("))", "")}`} />{label}</span>
              <span className="font-semibold tabular-nums">{money(value, currency, formatCurrency)}</span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

function GrossVsNetChart({ grossActualPaid, netActualPaid, confirmedRefundsAndCorrections, currency, formatCurrency, t }: {
  grossActualPaid: number | null;
  netActualPaid: number | null;
  confirmedRefundsAndCorrections: number | null;
  currency: string;
  formatCurrency: (value: number, currency?: string) => string;
  t: (key: string) => string;
}) {
  const hasData = finite(grossActualPaid) || finite(netActualPaid);

  if (!hasData) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("grossVsNetPaid")}</CardTitle>
          <p className="text-xs text-muted-foreground">{t("grossActualPaidHint")}</p>
        </CardHeader>
        <CardContent className="flex items-center justify-center h-48 text-muted-foreground">{t("chartNoData")}</CardContent>
      </Card>
    );
  }

  const data = [
    { name: t("gross"), value: finite(grossActualPaid) ? grossActualPaid! : 0, color: COLORS.grossActualPaid },
    { name: t("adjustments"), value: finite(confirmedRefundsAndCorrections) ? confirmedRefundsAndCorrections! : 0, color: COLORS.adjustments },
    { name: t("net"), value: finite(netActualPaid) ? netActualPaid! : 0, color: COLORS.netActualPaid },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("grossVsNetPaid")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("grossActualPaidHint")}</p>
      </CardHeader>
      <CardContent>
        <div className="h-48 min-w-0" role="img" aria-label={t("grossVsNetPaid")}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} layout="vertical" margin={{ top: 0, right: 8, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" horizontal={false} />
              <XAxis type="number" tickFormatter={(value: number) => money(value, currency, formatCurrency)} tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }} />
              <YAxis type="category" dataKey="name" hide />
              <Tooltip formatter={(value: number) => money(value, currency, formatCurrency)} />
              <Bar dataKey="value" name={t("grossVsNetPaid")} radius={[0, 4, 4, 0]}>
                {data.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
        <div className="mt-4 grid gap-2 sm:grid-cols-3">
          {data.map(({ name, value, color }) => (
            <div key={name} className="flex items-center justify-between gap-3">
              <span className="flex items-center gap-2 text-muted-foreground"><span aria-hidden="true" className={`h-2 w-2 rounded-full ${color.replace("hsl(var(", "bg-").replace("))", "")}`} />{name}</span>
              <span className="font-semibold tabular-nums">{money(value, currency, formatCurrency)}</span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

function BudgetLineUtilizationChart({ budgetLineUtilizations, currency, formatCurrency, t }: {
  budgetLineUtilizations: { reference: string; description: string; authorizedLimit: number; reservedAmount: number; utilizationPercentage: number; hasCurrencyMismatch: boolean }[];
  currency: string;
  formatCurrency: (value: number, currency?: string) => string;
  t: (key: string) => string;
}) {
  if (!budgetLineUtilizations.length) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("budgetUtilizationChart")}</CardTitle>
          <p className="text-xs text-muted-foreground">{t("budgetLineUtilizationHint")}</p>
        </CardHeader>
        <CardContent className="flex items-center justify-center h-48 text-muted-foreground">{t("noBudgetLines")}</CardContent>
      </Card>
    );
  }

  const data = budgetLineUtilizations.map((line, index) => ({
    name: line.reference || line.description || `Line ${index + 1}`,
    authorized: line.authorizedLimit,
    reserved: line.reservedAmount,
    utilization: line.utilizationPercentage,
    mismatch: line.hasCurrencyMismatch,
  }));

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t("budgetUtilizationChart")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("budgetLineUtilizationHint")}</p>
      </CardHeader>
      <CardContent>
        <div className="h-56 min-w-0" role="img" aria-label={t("budgetUtilizationChart")}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data} layout="vertical" margin={{ top: 0, right: 8, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" horizontal={false} />
              <XAxis type="number" tickFormatter={(value: number) => money(value, currency, formatCurrency)} tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }} />
              <YAxis type="category" dataKey="name" width={80} tick={{ fontSize: 11 }} />
              <Tooltip formatter={(value: number, name: string) => [money(value, currency, formatCurrency), name]} />
              <Legend />
              <Bar dataKey="authorized" name={t("budgetLineAuthorized")} stackId="a" fill="hsl(var(--fin-slate)/0.4)" radius={[0, 4, 4, 0]} />
              <Bar dataKey="reserved" name={t("budgetLineReserved")} stackId="a" fill="hsl(var(--warning))" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}

function FinancialTableState({ loading, error, colSpan, onRetry, t }: { loading: boolean; error: boolean; colSpan: number; onRetry: () => void; t: (key: string) => string }) {
  if (loading) return <TableRow><TableCell colSpan={colSpan} className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</TableCell></TableRow>;
  if (error) return <TableRow><TableCell colSpan={colSpan} className="p-6 text-center"><div className="flex flex-col items-center gap-2"><p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p><button className="text-sm underline" onClick={onRetry}>{t("common.retry")}</button></div></TableCell></TableRow>;
  return null;
}

export function ProjectFinanceTab() {
  const { projectId, project } = useOutletContext<ProjectContext>();
  const [searchParams, setSearchParams] = useSearchParams();
  const { t: translate, locale, formatCurrency, formatNumber } = useI18n();
  const t = React.useCallback(
    (key: string, params?: Record<string, string | number>) => translate(key.includes(".") ? key : `projects.${key}`, params),
    [translate],
  );
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();

  const canView = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectFinance.View)
    && hasPermission(user.permissions, Permissions.ProjectFinancialFoundation.View)
    && hasPermission(user.permissions, Permissions.ProjectSettings.View));
  const canViewPaymentRequests = Boolean(user && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.View));
  const selectedPaymentRequestId = searchParams.get("paymentRequestId");
  const queriesEnabled = canView && Boolean(projectId);

  const openPaymentRequestDetails = React.useCallback((item: CashFlowLineItem) => {
    if (!canViewPaymentRequests || !item.paymentRequestId) return;
    setSearchParams(current => {
      const next = new URLSearchParams(current);
      next.set("paymentRequestId", item.paymentRequestId!);
      next.delete("paymentReversalId");
      return next;
    });
  }, [canViewPaymentRequests, setSearchParams]);

  const reporting = useProjectFinancialReporting(projectId, undefined, undefined, queriesEnabled);
  const canViewFiscalYears = Boolean(user && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.View));
  const annualBudgets = useProjectAnnualBudgets(projectId, queriesEnabled && canViewFiscalYears);
  const fiscalYearOptions = React.useMemo<CashFlowFiscalYearOption[]>(() => {
    const page = annualBudgets.data;
    if (!page) return [];
    const budgetByYear = new Map(page.items.map(item => [item.fiscalYear, item]));
    const fiscalYears = [...new Set([page.currentFiscalYear, ...page.items.map(item => item.fiscalYear)])]
      .sort((left, right) => right - left);

    return fiscalYears.flatMap(year => {
      const budget = budgetByYear.get(year);
      const fiscalStartMonth = page.fiscalYearStartMonth;
      const fallbackRange = Number.isInteger(fiscalStartMonth) && fiscalStartMonth >= 1 && fiscalStartMonth <= 12
        ? {
            from: new Date(Date.UTC(year, fiscalStartMonth - 1, 1)).toISOString().slice(0, 10),
            to: new Date(Date.UTC(year + 1, fiscalStartMonth - 1, 0)).toISOString().slice(0, 10),
          }
        : null;
      const from = budget?.fiscalYearStartDate?.slice(0, 10) || fallbackRange?.from;
      const to = budget?.fiscalYearEndDate?.slice(0, 10) || fallbackRange?.to;
      if (!from || !to || from > to) return [];
      return [{
        value: String(year),
        label: budget?.fiscalYearLabel || translate("projects.cashFlow.fiscalYearOption", { year }),
        from,
        to,
      }];
    });
  }, [annualBudgets.data, translate]);

  const makeExportInput = React.useCallback((): FinancialReportingExportInput | null => {
    const report = reporting.data;
    if (!report) return null;
    return { level: "Project", report, locale: locale === "ar" ? "ar" : "en", generatedAt: new Date().toISOString(), translate };
  }, [reporting.data, locale, translate]);

  if (!canView) return <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("projects.financialsPermissionRequired")}</CardContent></Card>;
  if (reporting.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (reporting.isError) return <Card><CardContent className="p-6 text-center text-sm text-destructive">{t("financialReportingLoadFailed")}</CardContent></Card>;

  const data = reporting.data;
  if (!data) return <Card><CardContent className="p-6 text-center text-sm text-muted-foreground">{t("financialReportingEmpty")}</CardContent></Card>;

  const currency = data.currency || project.currency || "";
  const dataQuality = data.dataQuality;
  const overallStatus = dataQuality.overallStatus || "Unavailable";

  const lifetimeCeiling = finite(data.lifetimeCeiling) ? data.lifetimeCeiling : null;
  const annualApprovedBudget = finite(data.annualApprovedBudget) ? data.annualApprovedBudget : null;
  const lifetimeReserved = finite(data.lifetimeReserved) ? data.lifetimeReserved : 0;
  const outstandingCommitments = finite(data.outstandingCommitments) ? data.outstandingCommitments : 0;
  const grossActualPaid = finite(data.grossActualPaid) ? data.grossActualPaid : null;
  const confirmedRefundsAndCorrections = finite(data.confirmedRefundsAndCorrections) ? data.confirmedRefundsAndCorrections : null;
  const netActualPaid = finite(data.netActualPaid) ? data.netActualPaid : null;
  const availableLifetimeBudget = finite(data.availableLifetimeBudget) ? data.availableLifetimeBudget : null;
  const annualBudgetUtilization = finite(data.annualBudgetUtilizationPercentage) ? data.annualBudgetUtilizationPercentage : null;
  const budgetLineUtilizations = data.budgetLineUtilizations || [];

  const hasLegacyOverlap = dataQuality.hasCurrencyMismatch || dataQuality.hasMissingFinancialReferences;

  return (
    <div className="finance-dashboard space-y-5" dir={document.documentElement.dir || "ltr"}>
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-semibold tracking-tight">{t("financialReportingSection")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t("financialReportingSectionDescription")}</p>
        </div>
        <div className="flex items-center gap-2">
          <DataQualityBadge status={overallStatus} t={t} />
          <FinancialReportingExportButtons ready={Boolean(data)} makeInput={makeExportInput} />
        </div>
      </header>

      {dataQuality.projectCurrencyUnavailable && (
        <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive" role="alert">
          {t("financialCurrencyUnavailable")}
        </div>
      )}

      {dataQuality.hasCurrencyMismatch && (
        <div className="rounded-md border border-warning/30 bg-warning/5 p-3 text-sm text-warning" role="alert">
          <div className="flex items-center gap-2"><AlertTriangle className="h-4 w-4 shrink-0" />{t("financialCurrencyMismatch")}</div>
          {dataQuality.details.map((d, i) => <p key={i} className="mt-1 text-xs">{d}</p>)}
        </div>
      )}

      {hasLegacyOverlap && <LegacyOverlapWarning t={t} />}

      <DataQualityDetails details={dataQuality.details} t={t} />

      <CashFlowCalendar
        projectId={projectId}
        projectName={project.name}
        enabled={queriesEnabled}
        canExport={canView}
        fiscalYearOptions={fiscalYearOptions}
        onOpenItem={canViewPaymentRequests ? openPaymentRequestDetails : undefined}
      />
      <CashFlowForecastDashboard
        projectId={projectId}
        enabled={queriesEnabled}
        fiscalYearOptions={fiscalYearOptions}
      />

      <section className="grid gap-3 lg:grid-cols-2 xl:grid-cols-4" aria-label={t("financialSummary")}>
        <SummaryCard
          featured
          icon={Landmark}
          metricKey="lifetime-ceiling"
          label={t("lifetimeCeiling")}
          value={money(lifetimeCeiling, currency, formatCurrency)}
          hint={t("lifetimeCeilingHint")}
          tone="approved"
        />
        <SummaryCard
          icon={Layers}
          tone="planned"
          metricKey="annual-approved-budget"
          label={t("annualApprovedBudget")}
          value={money(annualApprovedBudget, currency, formatCurrency)}
          hint={t("annualApprovedBudgetHint")}
        />
        <SummaryCard
          icon={Wallet}
          tone={finite(lifetimeReserved) && lifetimeReserved > 0 ? "warning" : "neutral"}
          metricKey="lifetime-reserved"
          label={t("lifetimeReserved")}
          value={money(lifetimeReserved, currency, formatCurrency)}
          hint={t("lifetimeReservedHint")}
        />
        <SummaryCard
          icon={Banknote}
          tone={finite(availableLifetimeBudget) !== null && availableLifetimeBudget !== null && availableLifetimeBudget < 0 ? "danger" : "available"}
          metricKey="available-lifetime-budget"
          label={t("availableLifetimeBudget")}
          value={money(availableLifetimeBudget, currency, formatCurrency)}
          hint={t("availableLifetimeBudgetHint")}
        />
      </section>

      <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-4">
        <SummaryCard
          icon={ArrowDown}
          tone="actual"
          metricKey="gross-actual-paid"
          label={t("grossActualPaid")}
          value={money(grossActualPaid, currency, formatCurrency)}
          hint={t("grossActualPaidHint")}
        />
        <SummaryCard
          icon={TrendingDown}
          tone={finite(confirmedRefundsAndCorrections) !== null && confirmedRefundsAndCorrections !== null && confirmedRefundsAndCorrections < 0 ? "warning" : "neutral"}
          metricKey="confirmed-refunds-corrections"
          label={t("confirmedRefundsAndCorrections")}
          value={money(confirmedRefundsAndCorrections, currency, formatCurrency)}
          hint={t("confirmedRefundsAndCorrectionsHint")}
        />
        <SummaryCard
          icon={TrendingUp}
          tone="actual"
          metricKey="net-actual-paid"
          label={t("netActualPaid")}
          value={money(netActualPaid, currency, formatCurrency)}
          hint={t("netActualPaidHint")}
        />
        <SummaryCard
          icon={AlertTriangle}
          tone="warning"
          metricKey="outstanding-commitments"
          label={t("outstandingCommitments")}
          value={money(outstandingCommitments, currency, formatCurrency)}
          hint={t("outstandingCommitmentsHint")}
        />
      </div>

      <div className="grid gap-3 lg:grid-cols-2">
        <LifetimeBudgetChart
          lifetimeCeiling={lifetimeCeiling}
          lifetimeReserved={lifetimeReserved}
          grossActualPaid={grossActualPaid}
          currency={currency}
          formatCurrency={formatCurrency}
          t={t}
        />
        <GrossVsNetChart
          grossActualPaid={grossActualPaid}
          netActualPaid={netActualPaid}
          confirmedRefundsAndCorrections={confirmedRefundsAndCorrections}
          currency={currency}
          formatCurrency={formatCurrency}
          t={t}
        />
      </div>

      <Card>
        <CardHeader className="finance-section-head">
          <div>
            <CardTitle className="flex items-center gap-2 text-lg"><BarChart2 size={18} aria-hidden="true" />{t("commitmentsReservations")}</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{t("outstandingCommitmentsHint")}</p>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-3">
            <SummaryCard
              icon={Banknote}
              tone="warning"
              label={t("outstandingCommitments")}
              value={money(outstandingCommitments, currency, formatCurrency)}
              hint={t("outstandingCommitmentsHint")}
            />
            <SummaryCard
              icon={Layers}
              tone="planned"
              label={t("lifetimeReserved")}
              value={money(lifetimeReserved, currency, formatCurrency)}
              hint={t("lifetimeReservedHint")}
            />
            <SummaryCard
              icon={Landmark}
              tone="approved"
              label={t("annualApprovedBudget")}
              value={money(annualApprovedBudget, currency, formatCurrency)}
              hint={t("annualApprovedBudgetHint")}
            />
          </div>
          {annualBudgetUtilization !== null && (
            <div className="finance-progress space-y-2">
              <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                <span>{t("annualBudgetUtilization")}</span>
                <span className="tabular-nums"><bdi>{percent(annualBudgetUtilization / 100, formatNumber)}</bdi></span>
              </div>
              <Progress value={Math.min(annualBudgetUtilization, 100)} className="h-2" />
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="finance-section-head">
          <div>
            <CardTitle className="flex items-center gap-2 text-lg"><PieChart size={18} aria-hidden="true" />{t("paymentPerformance")}</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{t("grossActualPaidHint")}</p>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-3">
            <SummaryCard
              icon={ArrowDown}
              tone="actual"
              label={t("grossActualPaid")}
              value={money(grossActualPaid, currency, formatCurrency)}
              hint={t("grossActualPaidHint")}
            />
            <SummaryCard
              icon={TrendingDown}
              tone={finite(confirmedRefundsAndCorrections) !== null && confirmedRefundsAndCorrections !== null && confirmedRefundsAndCorrections < 0 ? "warning" : "neutral"}
              label={t("confirmedRefundsAndCorrections")}
              value={money(confirmedRefundsAndCorrections, currency, formatCurrency)}
              hint={t("confirmedRefundsAndCorrectionsHint")}
            />
            <SummaryCard
              icon={TrendingUp}
              tone="actual"
              label={t("netActualPaid")}
              value={money(netActualPaid, currency, formatCurrency)}
              hint={t("netActualPaidHint")}
            />
          </div>
          <GrossVsNetChart
            grossActualPaid={grossActualPaid}
            netActualPaid={netActualPaid}
            confirmedRefundsAndCorrections={confirmedRefundsAndCorrections}
            currency={currency}
            formatCurrency={formatCurrency}
            t={t}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="finance-section-head">
          <div>
            <CardTitle className="flex items-center gap-2 text-lg"><BarChart2 size={18} aria-hidden="true" />{t("budgetLineBreakdown")}</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{t("budgetLineUtilizationHint")}</p>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {budgetLineUtilizations.length === 0 ? (
            <div className="p-6 text-center text-sm text-muted-foreground">{t("noBudgetLines")}</div>
          ) : (
            <>
              <Table className="finance-ledger">
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("projects.budgetLine")}</TableHead>
                    <TableHead>{t("projects.description")}</TableHead>
                    <TableHead className="text-right">{t("budgetLineAuthorized")}</TableHead>
                    <TableHead className="text-right">{t("budgetLineReserved")}</TableHead>
                    <TableHead className="text-right">{t("budgetLineAvailable")}</TableHead>
                    <TableHead className="text-right">{t("budgetLineUtilizationPct")}</TableHead>
                    <TableHead>{t("dataQualityCurrencyMismatch")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {budgetLineUtilizations.map((line) => (
                    <TableRow key={line.budgetLineId}>
                      <TableCell className="font-medium">{line.reference}</TableCell>
                      <TableCell>{line.description}</TableCell>
                      <TableCell className="tabular-nums text-right">{money(line.authorizedLimit, currency, formatCurrency)}</TableCell>
                      <TableCell className="tabular-nums text-right">{money(line.reservedAmount, currency, formatCurrency)}</TableCell>
                      <TableCell className="tabular-nums text-right">{money(line.authorizedLimit - line.reservedAmount, currency, formatCurrency)}</TableCell>
                      <TableCell className="tabular-nums text-right">
                        <Badge variant={line.utilizationPercentage > 100 ? "destructive" : line.utilizationPercentage > 80 ? "warning" : "secondary"}>
                          {percent(line.utilizationPercentage / 100, formatNumber)}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        {line.hasCurrencyMismatch ? (
                          <Badge variant="warning" className="text-xs"><AlertTriangle className="h-3 w-3 mr-1" />{t("financialCurrencyMismatch")}</Badge>
                        ) : (
                          <Badge variant="success" className="text-xs"><CheckCircle className="h-3 w-3 mr-1" />OK</Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <BudgetLineUtilizationChart
                budgetLineUtilizations={budgetLineUtilizations}
                currency={currency}
                formatCurrency={formatCurrency}
                t={t}
              />
            </>
          )}
        </CardContent>
      </Card>

      {selectedPaymentRequestId && canViewPaymentRequests ? <ProjectPaymentRequests
        projectId={projectId}
        canViewRequests={canViewPaymentRequests}
        canReviewRequests={false}
        canReviewReversals={false}
        canCreateRequests={false}
        canCreatePlannedPayments={false}
        canEditRequests={false}
        canSubmitRequests={false}
        canCancelRequests={false}
        canApproveRequests={false}
        canRecordSettlements={false}
        canCreateReversals={false}
        canApproveReversals={false}
        active
      /> : null}
    </div>
  );
}
