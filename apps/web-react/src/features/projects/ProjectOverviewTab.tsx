import * as React from "react";
import { Link, useNavigate, useOutletContext } from "react-router-dom";
import {
  ArrowRight,
  ArrowRightLeft,
  ArrowUpRight,
  CalendarDays,
  CheckCircle2,
  CircleDollarSign,
  FilePen,
  Info,
  ShieldAlert,
  Users,
  type LucideIcon,
} from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  LabelList,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useI18n } from "@i18n/messages";
import { formatDate, cn } from "@lib/utils";
import { getStatusTone } from "@lib/status-presentation";
import { Card, CardContent, CardHeader } from "@components/ui/Card";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Progress } from "@components/ui/Progress";
import { Skeleton } from "@components/ui/Skeleton";
import { ErrorState } from "@components/ui/StateDisplay";
import { useProjectOverview, type ProjectDto, type ProjectOverviewDto } from "./api/projects";
import { useProjectEnumLabels } from "./lib/project-labels";

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function displayText(value?: string | null) {
  return value?.trim() || "—";
}

function displayDate(value?: string | null, locale = "en") {
  return value ? formatDate(value, locale) : "—";
}

function englishNumber(value: number | null | undefined, locale: string, fractionDigits = 0) {
  return isFiniteNumber(value)
    ? new Intl.NumberFormat(locale === "ar" ? "ar-EG-u-nu-latn" : locale, { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits }).format(value)
    : "—";
}

function nullableCurrency(value: number | null | undefined, currency: string, locale: string) {
  if (!isFiniteNumber(value)) return "—";
  const code = currency?.trim() || "USD";
  try {
    return new Intl.NumberFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", { style: "currency", currency: code, minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  } catch {
    return `${value.toFixed(2)} ${code}`;
  }
}

function percent(value: number | null | undefined, locale: string) {
  return isFiniteNumber(value) ? `${englishNumber(value, locale)}%` : "—";
}

type MilestoneState = "completed" | "inProgress" | "overdue" | "upcoming";

function getMilestoneState(m: { completedDate?: string; dueDate?: string; status?: string; progress?: number }): MilestoneState {
  const normalized = (m.status ?? "").replace(/[\s_-]+/g, "").toLowerCase();
  if (m.completedDate || (isFiniteNumber(m.progress) && m.progress >= 100) || ["complete", "completed", "closed", "done"].includes(normalized)) return "completed";
  if (["delayed", "late", "overdue"].includes(normalized) || (m.dueDate && new Date(m.dueDate).getTime() < Date.now())) return "overdue";
  if ((isFiniteNumber(m.progress) && m.progress > 0) || ["active", "inprogress", "ongoing", "started"].includes(normalized)) return "inProgress";
  return "upcoming";
}

function SectionTitle({ title, description, action }: { title: string; description?: string; action?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5 border-b pb-2.5 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <h2 className="text-sm font-semibold tracking-tight">{title}</h2>
        {description ? <p className="mt-1 text-xs text-muted-foreground">{description}</p> : null}
      </div>
      {action}
    </div>
  );
}

function InfoHint({ text }: { text: string }) {
  return <span title={text} aria-label={text} tabIndex={0} className="inline-flex shrink-0 cursor-help rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><Info aria-hidden="true" className="h-3 w-3" /></span>;
}

function Kpi({ label, value, detail, icon: Icon, tone = "text-foreground" }: { label: string; value: string; detail?: string; icon: LucideIcon; tone?: string }) {
  return (
    <div className="min-w-0 rounded-lg border bg-card px-3 py-2.5 shadow-none">
      <div className="flex items-center justify-between gap-2 text-[10px] font-medium text-muted-foreground">
        <span className="min-w-0 truncate">{label}</span>
        <Icon className="h-3.5 w-3.5 shrink-0" />
      </div>
      <p className={cn("mt-1 truncate text-base font-semibold tracking-tight tabular-nums", tone)}>{value}</p>
      {detail ? <p className="mt-0.5 truncate text-[10px] text-muted-foreground">{detail}</p> : null}
    </div>
  );
}

const EVM_REASON_KEYS: Record<string, string> = {
  NO_APPROVED_BASELINE: "projects.evmReasons.noApprovedBaseline",
  NO_BASELINE_ASSIGNMENTS: "projects.evmReasons.noBaselineAssignments",
  PLANNED_HOURS_MISSING: "projects.evmReasons.plannedHoursMissing",
  PLANNED_HOURS_INVALID: "projects.evmReasons.plannedHoursInvalid",
  BASELINE_DATES_INVALID: "projects.evmReasons.baselineDatesInvalid",
  ACTIVITY_PROGRESS_MISSING: "projects.evmReasons.activityProgressMissing",
  RESOURCE_NOT_FOUND: "projects.evmReasons.resourceNotFound",
  RESOURCE_ASSIGNMENT_MISSING: "projects.evmReasons.resourceAssignmentMissing",
  RESOURCE_CAPACITY_MISSING: "projects.evmReasons.resourceCapacityMissing",
  RESOURCE_COST_PROFILE_MISSING: "projects.evmReasons.resourceCostProfileMissing",
  RESOURCE_COST_PROFILE_AMBIGUOUS: "projects.evmReasons.resourceCostProfileAmbiguous",
  CURRENCY_MISMATCH: "projects.evmReasons.currencyMismatch",
  NO_ACTUAL_COST_DATA: "projects.evmReasons.noActualCostData",
  NON_LABOR_COSTS_NOT_BASELINED: "projects.evmReasons.nonLaborCostsNotBaselined",
  ZERO_PLANNED_VALUE: "projects.evmReasons.zeroPlannedValue",
  ZERO_ACTUAL_COST: "projects.evmReasons.zeroActualCost",
  EVM_DATA_INCOMPLETE: "projects.evmReasons.dataIncomplete",
};

function evmReasonText(reason: string | null | undefined, t: (key: string) => string) {
  return reason ? t(EVM_REASON_KEYS[reason] ?? EVM_REASON_KEYS.EVM_DATA_INCOMPLETE) : t("projects.evmUnavailable");
}

function DetailItem({ label, children, className = "" }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <div className={cn("min-w-0 rounded-lg border bg-muted/10 px-3 py-2.5", className)}>
      <dt className="truncate text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-1 truncate text-sm font-medium">{children}</dd>
    </div>
  );
}

function ProgressGauge({ label, value, comparison, comparisonLabel, tone, large, locale }: { label: string; value: number | null | undefined; comparison: number | null; comparisonLabel: string; tone: "green" | "amber" | "red"; large?: boolean; locale: string }) {
  const normalizedValue = isFiniteNumber(value) ? Math.min(100, Math.max(0, value)) : null;
  const progressFraction = normalizedValue === null ? 0 : normalizedValue / 100;
  const markerAngle = Math.PI - progressFraction * Math.PI;
  const markerX = 50 + 42 * Math.cos(markerAngle);
  const markerY = 52 - 42 * Math.sin(markerAngle);
  const displayValue = normalizedValue === null ? "—" : `${englishNumber(normalizedValue, locale)}%`;
  const comparisonValue = isFiniteNumber(comparison) ? `${comparison >= 0 ? "+" : ""}${englishNumber(comparison, locale)}%` : null;
  const progressColorClass = tone === "red" ? "text-destructive" : tone === "amber" ? "text-warning" : "text-success";

  return (
    <div className={cn("min-w-0 rounded-lg border bg-card shadow-none", large ? "px-4 py-3" : "px-3 py-2.5")}>
      <div className={cn("flex items-center justify-between gap-2 font-medium text-muted-foreground", large ? "text-xs" : "text-[11px]")}>
        <span className="truncate">{label}</span>
        <CheckCircle2 className={cn("shrink-0", large ? "h-4 w-4" : "h-3.5 w-3.5")} />
      </div>
      <div className={cn("relative mt-1", large ? "h-[148px]" : "h-[92px]")} role="img" aria-label={`${label}: ${displayValue}`}>
        <svg viewBox="0 0 100 62" className="h-full w-full overflow-visible" aria-hidden="true">
          <path d="M 8 52 A 42 42 0 0 1 92 52" fill="none" stroke="currentColor" strokeWidth="9" strokeLinecap="round" className="text-muted-foreground/20" />
          <path d="M 8 52 A 42 42 0 0 1 92 52" fill="none" stroke="currentColor" strokeWidth="9" strokeLinecap="round" pathLength={100} strokeDasharray={`${normalizedValue ?? 0} 100`} className={progressColorClass} />
          {normalizedValue !== null ? <circle cx={markerX} cy={markerY} r="4.8" className={cn(progressColorClass, "fill-current stroke-card")} strokeWidth="2.5" /> : null}
        </svg>
        <div className="absolute inset-x-0 bottom-0 flex flex-col items-center text-center">
          <div className="flex items-baseline justify-center gap-1.5">
            <p className={cn("font-semibold leading-none tracking-tight tabular-nums", progressColorClass, large ? "text-4xl" : "text-2xl")}>{displayValue}</p>
            {comparisonValue ? <span className={cn("rounded-full bg-muted px-1.5 py-0.5 font-semibold leading-none tabular-nums", progressColorClass, large ? "text-xs" : "text-[10px]")}>{comparisonValue}</span> : null}
          </div>
          {comparisonValue ? <p className={cn("mt-1 font-medium text-muted-foreground", large ? "text-[11px]" : "text-[9px]")}>{comparisonLabel}</p> : null}
        </div>
      </div>
    </div>
  );
}

function EmptyPanel({ title, description, action, className = "" }: { title: string; description: string; action?: React.ReactNode; className?: string }) {
  return (
    <div className={cn("flex min-h-28 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-5 py-5 text-center", className)}>
      <p className="text-sm font-medium">{title}</p>
      <p className="mt-1 max-w-sm text-xs text-muted-foreground">{description}</p>
      {action ? <div className="mt-2.5">{action}</div> : null}
    </div>
  );
}

const RISK_LEVEL_LABEL_KEYS: Record<string, string> = {
  Low: "riskLevel.low",
  Medium: "riskLevel.medium",
  High: "riskLevel.high",
  Critical: "riskLevel.critical",
};

const RISK_STATUS_LABEL_KEYS: Record<string, string> = {
  Identified: "riskStatus.identified",
  ActionRequired: "riskStatus.actionRequired",
  FollowUp: "riskStatus.followUp",
  Mitigated: "riskStatus.mitigated",
  Closed: "riskStatus.closed",
  Expected: "riskStatus.expected",
  Active: "riskStatus.active",
};

function translatedRiskValue(value: string, keys: Record<string, string>, t: (key: string) => string) {
  const key = keys[value];
  return key ? t(`enums.${key}`) : "—";
}

function ProjectInformationCard({
  project,
  overview,
  locale,
}: {
  project: ProjectDto;
  overview?: ProjectOverviewDto;
  locale: string;
}) {
  const { t } = useI18n();
  const latestReport = overview?.statusReports.latestPublishedReport;
  return <section className="overflow-hidden rounded-2xl border bg-card shadow-sm">
    <div className="grid lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
      <div className="space-y-3 p-4 sm:p-5">
        <div>
          <h2 className="text-sm font-semibold tracking-tight">{t("projects.projectInformation")}</h2>
          <p className="mt-0.5 text-xs text-muted-foreground">{t("projects.projectInformationDescription")}</p>
        </div>
        <div className="grid gap-2 sm:grid-cols-2">
          <div className="rounded-xl border bg-muted/15 p-3">
            <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("projects.projectDescription")}</p>
            <p className="mt-1 line-clamp-4 text-sm leading-6">{displayText(project.description)}</p>
          </div>
          <div className="rounded-xl border bg-muted/15 p-3">
            <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("projects.objectiveBusinessCase")}</p>
            <p className="mt-1 line-clamp-4 text-sm leading-6">{displayText(project.businessCase)}</p>
          </div>
        </div>
        <div className="rounded-xl border bg-muted/15 p-3">
          <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("projects.notes")}</p>
          <p className="mt-1 line-clamp-3 text-sm leading-6">{displayText(project.notes)}</p>
        </div>
      </div>
      <div className="border-t p-4 sm:p-5 lg:border-s lg:border-t-0">
        <div className="mb-2">
          <h2 className="text-sm font-semibold tracking-tight">{t("projects.projectDetails")}</h2>
          <p className="mt-0.5 text-xs text-muted-foreground">{t("projects.projectInformationDescription")}</p>
        </div>
        <dl className="grid gap-2 sm:grid-cols-2">
          <DetailItem label={t("projects.projectManager")}>{displayText(project.projectManagerName)}</DetailItem>
          <DetailItem label={t("projects.sponsor")}>{displayText(project.sponsorName)}</DetailItem>
          <DetailItem label={t("projects.portfolio")}>{displayText(project.portfolioName)}</DetailItem>
          <DetailItem label={t("projects.program")}>{overview?.program ? `${overview.program.name}${overview.program.code ? ` · ${overview.program.code}` : ""}` : "—"}</DetailItem>
          <DetailItem label={`${t("projects.startDate")} / ${t("projects.endDate")}`}>{displayDate(project.startDate, locale)} <span className="text-muted-foreground">→</span> {displayDate(project.endDate, locale)}</DetailItem>
          <DetailItem label={t("projects.updatedAt")}>{displayDate(project.updatedAt || project.createdAt, locale)}</DetailItem>
          <DetailItem label={t("projects.publishedAt")}>{latestReport ? displayDate(latestReport.publishedAt, locale) : "—"}</DetailItem>
          <DetailItem label={t("projects.department")}>{displayText(project.departmentName)}</DetailItem>
          <DetailItem label={t("projects.currency")}>{displayText(project.currency)}</DetailItem>
          <DetailItem label={t("projects.riskRating")}>{displayText(project.riskRating)}</DetailItem>
          <DetailItem label={t("projects.confidenceLevel")}><Badge status={project.confidenceLevel} className="text-[10px]">{project.confidenceLevel || "—"}</Badge></DetailItem>
          <DetailItem label={t("projects.baseline")} className="sm:col-span-2">{overview?.currentBaseline ? `${overview.currentBaseline.name} · v${englishNumber(overview.currentBaseline.version, locale)}` : "—"}</DetailItem>
        </dl>
      </div>
    </div>
  </section>;
}

function ProjectKpiSummary({ project, overview, locale }: { project: ProjectDto; overview?: ProjectOverviewDto; locale: string }) {
  const { t } = useI18n();
  const latestReport = overview?.statusReports.latestPublishedReport;
  const projectHealth = latestReport?.overallHealth || project.health || project.ragStatus;
  const progress = overview?.planProgress ?? project.progress;
  const progressDelta = isFiniteNumber(progress) && isFiniteNumber(latestReport?.overallProgressPercent) ? progress - latestReport.overallProgressPercent : null;
  const projectHealthTone = getStatusTone(projectHealth);
  const progressTone = projectHealthTone === "red" ? "red" : projectHealthTone === "amber" ? "amber" : "green";
  const highCriticalRisks = overview?.risks.highCriticalOpenCount ?? null;
  const overdueIssues = overview?.issues.overdueOpenIssueCount ?? null;
  const criticalAttention = highCriticalRisks !== null && overdueIssues !== null ? highCriticalRisks + overdueIssues : null;
  const evmCurrency = overview?.currency ?? project.currency;
  const sv = overview?.scheduleVariance ?? null;
  const cv = overview?.costVariance ?? null;

  return <section aria-labelledby="project-kpi-heading">
    <div className="mb-2 flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between"><h2 id="project-kpi-heading" className="text-sm font-semibold">{t("projects.kpiSummary")}</h2><span className="text-[11px] text-muted-foreground">{latestReport ? `${t("projects.publishedAt")}: ${displayDate(latestReport.publishedAt, locale)}` : t("projects.noStatusReports")}</span></div>
    <div className="grid items-stretch gap-3 lg:grid-cols-[minmax(240px,0.8fr)_minmax(0,2fr)]">
      <div className="min-w-0"><ProgressGauge label={t("projects.overallProgressLabel")} value={progress} comparison={progressDelta} comparisonLabel={t("projects.progressSinceLatestPublished")} tone={progressTone} large locale={locale} /></div>
      <div className="grid min-w-0 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi label={t("projects.scheduleVariance")} value={nullableCurrency(sv, evmCurrency, locale)} detail={isFiniteNumber(sv) ? (sv < 0 ? t("projects.behindSchedule") : sv > 0 ? t("projects.aheadOfSchedule") : t("projects.onSchedule")) : t("projects.scheduleSignalUnavailable")} icon={CalendarDays} tone={isFiniteNumber(sv) && sv < 0 ? "text-destructive" : "text-success"} />
        <Kpi label={t("projects.costVariance")} value={nullableCurrency(cv, evmCurrency, locale)} detail={isFiniteNumber(cv) ? evmCurrency : evmReasonText(overview?.costVarianceUnavailableReason, t)} icon={CircleDollarSign} tone={isFiniteNumber(cv) && cv < 0 ? "text-destructive" : "text-success"} />
        <Kpi label={t("projects.criticalAttention")} value={englishNumber(criticalAttention, locale)} detail={criticalAttention === null ? t("projects.overviewDataUnavailable") : `${englishNumber(highCriticalRisks, locale)} ${t("projects.highCriticalRisks")} · ${englishNumber(overdueIssues, locale)} ${t("projects.overdueOpenIssues")}`} icon={ShieldAlert} tone={criticalAttention ? "text-destructive" : "text-success"} />
        <Kpi label={t("projects.teamSignal")} value={englishNumber(overview?.allocations.activeResourceCount ?? project.members?.length ?? 0, locale)} detail={overview ? `${englishNumber(overview.allocations.allocatedHoursPerWeek, locale, 1)} ${t("projects.hoursPerWeek")}` : t("projects.overviewDataUnavailable")} icon={Users} />
      </div>
    </div>
  </section>;
}

export function ProjectOverviewTab() {
  const { projectId, project } = useOutletContext<{
    projectId: string;
    project: ProjectDto;
  }>();
  const { t, locale } = useI18n();
  const { healthStatusLabel } = useProjectEnumLabels();
  const navigate = useNavigate();
  const query = useProjectOverview(projectId, Boolean(projectId));
  const overview = query.data;
  const projectInformation = <ProjectInformationCard project={project} overview={overview} locale={locale} />;

  if (query.isLoading) {
    return (
      <div className="space-y-3 pt-1">
        {projectInformation}
        <ProjectKpiSummary project={project} locale={locale} />
        <div className="grid gap-3 lg:grid-cols-2" role="status" aria-label={t("common.loading")}>
          {[1, 2, 3, 4].map((i) => (
            <Card key={i}>
              <CardContent className="space-y-3 p-4">
                <Skeleton className="h-4 w-1/3" />
                <Skeleton className="h-8 w-1/2" />
                <Skeleton className="h-2 w-full" />
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    );
  }

  if (query.isError || !overview) {
    return (
      <div className="space-y-3 pt-1">
        {projectInformation}
        <ProjectKpiSummary project={project} locale={locale} />
        <Card>
          <CardContent className="p-0">
            <ErrorState title={t("projects.overviewDataUnavailable")} onRetry={() => void query.refetch()} />
          </CardContent>
        </Card>
      </div>
    );
  }

  const latestReport = overview.statusReports.latestPublishedReport;
  const milestones = overview.milestones ?? [];
  const evmCurrency = overview.currency ?? project.currency;
  const pv = overview.plannedValue;
  const ev = overview.earnedValue;
  const ac = overview.actualCost;
  const approvedBudget = overview.hasBudgetData ? overview.approvedBudget : (overview.approvedBudget ?? project.budget);
  const committedCost = overview.committedCost;
  const forecastAtCompletion = overview.forecastAtCompletion;
  const remainingBudget = overview.remainingBudget;
  const budgetUtilization = overview.budgetUtilizationPercentage;
  const sv = overview.scheduleVariance ?? null;
  const cv = overview.costVariance ?? null;
  const spi = overview.schedulePerformanceIndex ?? null;
  const cpi = overview.costPerformanceIndex ?? null;
  const chartData = [{
    label: t("projects.projectPerformance"),
    plannedValue: pv,
    earnedValue: ev,
    actualCost: ac,
  }];
  const hasEvmData = [pv, ev, ac].some(isFiniteNumber);

  return (
    <div className="space-y-3 pt-1">
      {projectInformation}
      <ProjectKpiSummary project={project} overview={overview} locale={locale} />

      <div className="grid items-start gap-3 lg:grid-cols-3">
        <Card className="shadow-none lg:col-span-2">
          <CardHeader className="!py-3 !pb-2">
            <SectionTitle title={t("projects.projectPerformance")} description={t("projects.currentPerformanceDescription")} action={<Link to={`/projects/${projectId}/financials`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className="!py-3 !pt-1">
            <p className="mb-2 text-[10px] text-muted-foreground">{t("projects.evmStatusDateNote", { date: displayDate(overview.statusDate, locale) })}</p>
            {!hasEvmData ? <EmptyPanel className="h-44" title={t("projects.evmUnavailable")} description={`${t("projects.evmUnavailableDescription")} ${evmReasonText(overview.plannedValueUnavailableReason ?? overview.earnedValueUnavailableReason ?? overview.actualCostUnavailableReason, t)}`} /> : (
              <div>
                <div className="h-56 min-w-0" role="img" aria-label={t("projects.projectPerformance")}>
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={chartData} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} className="stroke-border" />
                      <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fontSize: 11 }} />
                      <YAxis tickLine={false} axisLine={false} tick={{ fontSize: 10 }} tickFormatter={(value) => englishNumber(typeof value === "number" ? value : Number(value), locale)} width={48} />
                      <Tooltip formatter={(value, name) => [nullableCurrency(typeof value === "number" ? value : undefined, evmCurrency, locale), name]} />
                      <Legend wrapperStyle={{ fontSize: 11, paddingTop: 4 }} />
                      <Bar dataKey="plannedValue" name={t("projects.plannedValue")} fill="hsl(var(--chart-1))" radius={[4, 4, 0, 0]} maxBarSize={42}>
                        <LabelList dataKey="plannedValue" position="top" formatter={(value: number | string | undefined) => isFiniteNumber(value) ? nullableCurrency(value, evmCurrency, locale) : "—"} style={{ fontSize: 10, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                      <Bar dataKey="earnedValue" name={t("projects.earnedValue")} fill="hsl(var(--chart-2))" radius={[4, 4, 0, 0]} maxBarSize={42}>
                        <LabelList dataKey="earnedValue" position="top" formatter={(value: number | string | undefined) => isFiniteNumber(value) ? nullableCurrency(value, evmCurrency, locale) : "—"} style={{ fontSize: 10, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                      <Bar dataKey="actualCost" name={t("projects.actualCost")} fill="hsl(var(--chart-3))" radius={[4, 4, 0, 0]} maxBarSize={42}>
                        <LabelList dataKey="actualCost" position="top" formatter={(value: number | string | undefined) => isFiniteNumber(value) ? nullableCurrency(value, evmCurrency, locale) : "—"} style={{ fontSize: 10, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>
                <dl className="mt-3 grid gap-x-4 gap-y-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-2">
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.approvedBudget")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(approvedBudget, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.budgetAtCompletion")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(overview.budgetAtCompletion, evmCurrency, locale)}</dd></div>
                  {overview.originalApprovedBudget && overview.originalApprovedBudget !== approvedBudget ? (
                    <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.originalApprovedBudget")}</dt><dd className="mt-1 text-xs text-muted-foreground">{nullableCurrency(overview.originalApprovedBudget, evmCurrency, locale)}</dd></div>
                  ) : null}
                  {overview.hasPendingRevision ? (
                    <div className="rounded-md bg-amber-50 p-2 text-xs text-amber-700 dark:bg-amber-950 dark:text-amber-300">{t("projects.budgetRevisions.pendingRevision")}</div>
                  ) : null}
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.plannedValue")}</dt><dd title={pv == null ? evmReasonText(overview.plannedValueUnavailableReason, t) : undefined} className="mt-1 text-sm font-semibold">{nullableCurrency(pv, evmCurrency, locale)}</dd>{pv == null && overview.plannedValueUnavailableReason ? <p className="mt-0.5 text-[10px] text-muted-foreground">{evmReasonText(overview.plannedValueUnavailableReason, t)}</p> : null}</div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.earnedValue")}</dt><dd title={ev == null ? evmReasonText(overview.earnedValueUnavailableReason, t) : undefined} className="mt-1 text-sm font-semibold">{nullableCurrency(ev, evmCurrency, locale)}</dd>{ev == null && overview.earnedValueUnavailableReason ? <p className="mt-0.5 text-[10px] text-muted-foreground">{evmReasonText(overview.earnedValueUnavailableReason, t)}</p> : null}</div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.actualCost")}</dt><dd title={ac == null ? evmReasonText(overview.actualCostUnavailableReason, t) : undefined} className="mt-1 text-sm font-semibold">{nullableCurrency(ac, evmCurrency, locale)}</dd>{ac == null && overview.actualCostUnavailableReason ? <p className="mt-0.5 text-[10px] text-muted-foreground">{evmReasonText(overview.actualCostUnavailableReason, t)}</p> : null}</div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.committedCost")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(committedCost, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("common.forecast")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(forecastAtCompletion, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.remainingBudget")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(remainingBudget, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.budgetUtilization")}</dt><dd className="mt-1 text-sm font-semibold">{isFiniteNumber(budgetUtilization) ? `${englishNumber(budgetUtilization, locale, 1)}%` : "—"}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.scheduleVariance")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(sv, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.costVariance")}</dt><dd title={cv == null ? evmReasonText(overview.costVarianceUnavailableReason, t) : undefined} className="mt-1 text-sm font-semibold">{nullableCurrency(cv, evmCurrency, locale)}</dd>{cv == null && overview.costVarianceUnavailableReason ? <p className="mt-0.5 text-[10px] text-muted-foreground">{evmReasonText(overview.costVarianceUnavailableReason, t)}</p> : null}</div>
                </dl>
              </div>
            )}
            <dl className="mt-3 grid gap-x-4 gap-y-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-2">
              <div><dt className="flex items-center gap-1 text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.schedulePerformanceIndex")}<InfoHint text={t("projects.spiTooltip")} /></dt><dd className="mt-1 text-sm font-semibold">{isFiniteNumber(spi) ? englishNumber(spi, locale, 2) : "—"}</dd>{!isFiniteNumber(spi) && overview.schedulePerformanceIndexUnavailableReason ? <p className="mt-0.5 text-[10px] text-muted-foreground">{evmReasonText(overview.schedulePerformanceIndexUnavailableReason, t)}</p> : null}</div>
              <div><dt className="flex items-center gap-1 text-[10px] uppercase tracking-wide text-muted-foreground">{t("projects.costPerformanceIndex")}<InfoHint text={t("projects.cpiTooltip")} /></dt><dd className="mt-1 text-sm font-semibold">{isFiniteNumber(cpi) ? englishNumber(cpi, locale, 2) : "—"}</dd>{!isFiniteNumber(cpi) && overview.costPerformanceIndexUnavailableReason ? <p className="mt-0.5 text-[10px] text-muted-foreground">{evmReasonText(overview.costPerformanceIndexUnavailableReason, t)}</p> : null}</div>
            </dl>
            <p className="mt-3 text-[10px] text-muted-foreground">{t("projects.currentSnapshotNoHistory")}</p>
          </CardContent>
        </Card>

        <Card className="shadow-none lg:col-span-1">
          <CardHeader className="!py-3 !pb-2">
            <SectionTitle title={t("projects.attentionRequired")} description={t("projects.attentionRequiredDescription")} action={<Link to={`/projects/${projectId}/risks-issues`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className="!py-3 !pt-1">
            <div className="grid gap-3">
              <div className="rounded-xl border bg-destructive/5 p-3">
                <div className="flex items-center justify-between"><h3 className="flex items-center gap-1.5 text-xs font-semibold"><ShieldAlert className="h-3.5 w-3.5 text-destructive" />{t("projects.topRisks")}</h3><span className="text-xs font-semibold text-destructive">{englishNumber(overview.risks.openCount, locale)}</span></div>
                <div className="mt-2 divide-y rounded-lg border">{overview.risks.topRisks.length ? overview.risks.topRisks.map((risk) => <Link key={risk.id} to={`/projects/${projectId}/risks-issues`} className="flex items-center justify-between gap-3 px-3 py-2.5 hover:bg-muted/30"><span className="min-w-0 truncate text-xs font-medium">{risk.title}</span><Badge status={risk.riskLevel} title={translatedRiskValue(risk.status, RISK_STATUS_LABEL_KEYS, t)} className="shrink-0 text-[10px]">{translatedRiskValue(risk.riskLevel, RISK_LEVEL_LABEL_KEYS, t)}</Badge></Link>) : <p className="px-3 py-4 text-xs text-muted-foreground">{t("projects.noRisksYet")}</p>}</div>
              </div>
              <div className="rounded-xl border bg-amber-500/5 p-3">
                <div className="flex items-center justify-between"><h3 className="flex items-center gap-1.5 text-xs font-semibold"><CircleDollarSign className="h-3.5 w-3.5 text-warning" />{t("projects.issues")}</h3><span className="text-xs font-semibold text-warning">{englishNumber(overview.issues.openIssueCount, locale)}</span></div>
                <div className="mt-2 divide-y rounded-lg border">{overview.issues.topIssues.length ? overview.issues.topIssues.map((issue) => <Link key={issue.id} to={`/work-items/${encodeURIComponent(issue.id)}`} className="flex items-center justify-between gap-3 px-3 py-2.5 hover:bg-muted/30"><span className="min-w-0 truncate text-xs font-medium">{issue.title}</span><span className="shrink-0 text-[10px] text-muted-foreground">{issue.dueDate ? displayDate(issue.dueDate, locale) : "—"}</span></Link>) : <p className="px-3 py-4 text-xs text-muted-foreground">{t("projects.noIssuesYet")}</p>}</div>
              </div>
              <div className="rounded-xl border bg-blue-500/5 p-3">
                <div className="flex items-center justify-between"><h3 className="flex items-center gap-1.5 text-xs font-semibold"><FilePen className="h-3.5 w-3.5 text-info" />{t("projects.changeRequests")}</h3><span className="text-xs font-semibold text-info">{englishNumber(overview.pendingChangeRequestCount, locale)}</span></div>
                <div className="mt-2 divide-y rounded-lg border"><p className="px-3 py-4 text-xs text-muted-foreground">{overview.pendingChangeRequestCount > 0 ? t("projects.openChangeRequests") : t("projects.noChangeRequestsYet")}</p></div>
              </div>
              <div className="rounded-xl border bg-purple-500/5 p-3">
                <div className="flex items-center justify-between"><h3 className="flex items-center gap-1.5 text-xs font-semibold"><ArrowRightLeft className="h-3.5 w-3.5 text-purple-500" />{t("projects.handover")}</h3><span className="text-xs font-semibold text-purple-500">{englishNumber(overview.pendingHandoverCount, locale)}</span></div>
                <div className="mt-2 divide-y rounded-lg border"><p className="px-3 py-4 text-xs text-muted-foreground">{overview.pendingHandoverCount > 0 ? t("projects.pendingHandovers") : t("projects.noHandoversYet")}</p></div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid items-start gap-3 lg:grid-cols-3">
        <Card className="shadow-none lg:col-span-2">
          <CardHeader className="!py-3 !pb-2">
            <SectionTitle title={t("projects.milestones")} description={t("projects.milestoneTimelineDescription")} action={<Link to={`/projects/${projectId}/plan`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className="!py-3 !pt-1">
            {milestones.length ? (
              <>
                <div className="mb-3 flex flex-wrap gap-x-4 gap-y-2 border-b pb-3" aria-label={t("projects.milestoneStatusLegend")}>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-success" />{t("projects.milestoneCompleted")}</span>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-warning" />{t("projects.milestoneInProgress")}</span>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-destructive" />{t("projects.milestoneOverdue")}</span>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-info" />{t("projects.milestoneUpcoming")}</span>
                </div>
                <div className="relative">
                  <div className="absolute bottom-3 start-[15px] top-3 w-px bg-border" aria-hidden="true" />
                  <div className="space-y-1">
                    {milestones.map((milestone) => {
                      const state = getMilestoneState(milestone);
                      const stateLabel = state === "completed" ? t("projects.milestoneCompleted") : state === "inProgress" ? t("projects.milestoneInProgress") : state === "overdue" ? t("projects.milestoneOverdue") : t("projects.milestoneUpcoming");
                      const dotClass = state === "completed" ? "bg-success ring-success/20" : state === "inProgress" ? "bg-warning ring-warning/20" : state === "overdue" ? "bg-destructive ring-destructive/20" : "bg-info ring-info/20";
                      return (
                        <Link key={milestone.id} to={`/projects/${projectId}/plan`} className="group relative z-10 flex items-start gap-3 rounded-lg px-1.5 py-2.5 transition-colors hover:bg-muted/30">
                          <span className={cn("mt-1 flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-4 border-card ring-2", dotClass)} aria-hidden="true" />
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-start justify-between gap-2">
                              <p className="min-w-0 flex-1 text-sm font-semibold leading-5">{milestone.name}</p>
                              <Badge status={state === "completed" ? "Completed" : state === "inProgress" ? "Active" : state === "overdue" ? "Delayed" : "Planned"} className="shrink-0 text-[9px]">{stateLabel}</Badge>
                            </div>
                            <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[10px] text-muted-foreground">
                              <span>{displayDate(milestone.dueDate, locale)}</span>
                              {isFiniteNumber(milestone.progress) ? <><span aria-hidden="true">·</span><span>{percent(milestone.progress, locale)}</span></> : null}
                            </div>
                          </div>
                        </Link>
                      );
                    })}
                  </div>
                </div>
              </>
            ) : <EmptyPanel title={t("projects.noMilestonesYet")} description={t("projects.milestoneTimelineDescription")} action={<Button size="sm" variant="outline" onClick={() => navigate(`/projects/${projectId}/plan`)}>{t("projects.plan")}</Button>} />}
          </CardContent>
        </Card>

        <Card className="shadow-none lg:col-span-1">
          <CardHeader className="!py-3 !pb-2">
            <SectionTitle title={t("projects.latestPublishedStatus")} description={t("projects.publishedReportOnlyDescription")} action={<Link to={`/projects/${projectId}/status-reports`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className="!py-3 !pt-1">
            {latestReport ? (
              <div className="space-y-3">
                <div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">{displayDate(latestReport.periodFrom, locale)} – {displayDate(latestReport.periodTo, locale)}</p><p className="mt-1 text-xs text-muted-foreground">{t("projects.publishedAt")}: {displayDate(latestReport.publishedAt, locale)}</p></div><Badge status={latestReport.overallHealth} className="text-[10px]">{healthStatusLabel(latestReport.overallHealth)}</Badge></div>
                <div className="rounded-xl border bg-muted/20 p-3"><div className="flex items-center justify-between text-xs"><span className="text-muted-foreground">{t("projects.overallProgress")}</span><span className="font-semibold">{percent(latestReport.overallProgressPercent, locale)}</span></div><Progress value={latestReport.overallProgressPercent ?? 0} className="mt-1.5 h-1.5" /></div>
                <div className="rounded-lg border bg-muted/20 p-3"><p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("projects.executiveSummary")}</p><p className="mt-1 line-clamp-5 text-sm leading-6">{displayText(latestReport.executiveSummary)}</p></div>
                <Link to={`/projects/${projectId}/status-reports`}><Button size="sm" variant="outline" className="w-full">{t("projects.statusReportDetails")}<ArrowRight className="ms-1.5 h-3.5 w-3.5" /></Button></Link>
              </div>
            ) : overview.statusReports.totalCount > 0 ? (
              <EmptyPanel title={t("projects.noPublishedStatusReports")} description={t("projects.noPublishedStatusReportsDescription")} action={<Link to={`/projects/${projectId}/status-reports`}><Button size="sm" variant="outline">{t("projects.statusReports")}</Button></Link>} />
            ) : (
              <EmptyPanel title={t("projects.noStatusReports")} description={t("projects.noStatusReportsDescription")} action={<Link to={`/projects/${projectId}/status-reports`}><Button size="sm" variant="outline">{t("projects.statusReports")}</Button></Link>} />
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-3 lg:grid-cols-2">
        <Card className="shadow-none">
          <CardHeader className="!py-3 !pb-2">
            <SectionTitle title={t("projects.resourceAllocationSummary")} action={<Link to={`/projects/${projectId}/team`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className="!py-3 !pt-1">
            <div className="grid grid-cols-3 gap-2 text-center">
              <div className="rounded-lg bg-muted/30 p-3"><p className="text-lg font-semibold tabular-nums">{englishNumber(overview.allocations.activeResourceCount ?? project.members?.length ?? 0, locale)}</p><p className="mt-1 text-[10px] text-muted-foreground">{t("projects.teamResources")}</p></div>
              <div className="rounded-lg bg-muted/30 p-3"><p className="text-lg font-semibold tabular-nums">{englishNumber(overview.allocations.activeAllocationCount, locale)}</p><p className="mt-1 text-[10px] text-muted-foreground">{t("common.allocation")}</p></div>
              <div className="rounded-lg bg-muted/30 p-3"><p className="text-lg font-semibold tabular-nums">{englishNumber(overview.allocations.allocatedHoursPerWeek, locale, 1)}</p><p className="mt-1 text-[10px] text-muted-foreground">{t("projects.hoursPerWeek")}</p></div>
            </div>
            <p className="mt-2 text-[10px] text-muted-foreground">{t("projects.utilizationUnavailable")}</p>
          </CardContent>
        </Card>
        <Card className="shadow-none">
          <CardHeader className="!py-3 !pb-2">
            <SectionTitle title={t("projects.governanceReporting")} action={<Link to={`/projects/${projectId}/status-reports`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className="!py-3 !pt-1">
            <dl className="grid gap-x-6 gap-y-2 text-xs sm:grid-cols-2">
              <div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("projects.statusReports")}</dt><dd className="font-semibold tabular-nums">{englishNumber(overview.statusReports.totalCount, locale)}</dd></div>
              <div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("projects.publishedReports")}</dt><dd className="font-semibold tabular-nums">{englishNumber(overview.statusReports.publishedCount, locale)}</dd></div>
              <div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("common.meetings")}</dt><dd className="font-semibold tabular-nums">{englishNumber(overview.meetings.meetingCount, locale)}</dd></div>
              <div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("projects.finalMinutes")}</dt><dd className="font-semibold tabular-nums">{englishNumber(overview.meetings.finalMinutesCount, locale)}</dd></div>
            </dl>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
