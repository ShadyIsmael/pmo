import * as React from "react";
import { Link, useNavigate, useOutletContext } from "react-router-dom";
import {
  AlertTriangle,
  ArrowUpRight,
  BarChart3,
  Briefcase,
  Building2,
  CalendarDays,
  CheckCircle2,
  CircleDollarSign,
  Clock3,
  FilePen,
  FileText,
  GitBranch,
  Info,
  Layers,
  ShieldAlert,
  UserCheck,
  Users,
  type LucideIcon,
} from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  Legend,
  Pie,
  PieChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useI18n } from "@i18n/messages";
import { formatDate, cn } from "@lib/utils";
import { getRiskLevelTextClass, getStatusTextClass, getStatusTone } from "@lib/status-presentation";
import { Card, CardContent, CardHeader } from "@components/ui/Card";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Skeleton } from "@components/ui/Skeleton";
import { ErrorState } from "@components/ui/StateDisplay";
import {
  useProjectOverview,
  useProjectTeamCapacity,
  type ProjectCapacityPeriodType,
  type ProjectDto,
  type ProjectOverviewDto,
} from "./api/projects";

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function displayText(value?: string | null) {
  return value?.trim() || "—";
}

function humanizeCode(value: string) {
  return value.trim().replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[\s_-]+/g, " ");
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

function toLocalDateString(date: Date) {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

function getCapacityReportingPeriod(periodType: ProjectCapacityPeriodType, now = new Date()) {
  let start: Date;
  let end: Date;

  if (periodType === "Week") {
    start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - now.getDay());
    end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 7);
  } else if (periodType === "Quarter") {
    const quarterStartMonth = Math.floor(now.getMonth() / 3) * 3;
    start = new Date(now.getFullYear(), quarterStartMonth, 1);
    end = new Date(now.getFullYear(), quarterStartMonth + 3, 1);
  } else {
    start = new Date(now.getFullYear(), now.getMonth(), 1);
    end = new Date(now.getFullYear(), now.getMonth() + 1, 1);
  }

  return { startDate: toLocalDateString(start), endDate: toLocalDateString(end) };
}

type MilestoneState = "completed" | "inProgress" | "overdue" | "upcoming";

function getMilestoneState(m: { completedDate?: string; dueDate?: string; status?: string; progress?: number }, statusDate?: string): MilestoneState {
  const normalized = (m.status ?? "").replace(/[\s_-]+/g, "").toLowerCase();
  if (m.completedDate || (isFiniteNumber(m.progress) && m.progress >= 100) || ["complete", "completed", "closed", "done"].includes(normalized)) return "completed";
  const dueDay = m.dueDate?.slice(0, 10);
  const currentDay = statusDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10);
  if (["delayed", "late", "overdue"].includes(normalized) || (dueDay && dueDay < currentDay)) return "overdue";
  if ((isFiniteNumber(m.progress) && m.progress > 0) || ["active", "inprogress", "ongoing", "started"].includes(normalized)) return "inProgress";
  return "upcoming";
}

const PREMIUM_CARD = "overflow-hidden rounded-2xl border-border/60 shadow-sm transition-shadow hover:shadow-md";
const PREMIUM_CARD_HEADER = "!py-3.5 !pb-2.5";
const PREMIUM_CARD_CONTENT = "!py-4 !pt-1";

function SectionTitle({ title, description, action, icon: Icon, accent = "bg-primary/10 text-primary", titleAdornment }: { title: string; description?: string; action?: React.ReactNode; icon?: LucideIcon; accent?: string; titleAdornment?: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3 border-b border-border/60 pb-3">
      <div className="flex min-w-0 items-start gap-2.5">
        {Icon ? <span className={cn("flex h-8 w-8 shrink-0 items-center justify-center rounded-lg", accent)}><Icon className="h-4 w-4" /></span> : null}
        <div className="min-w-0">
          <h2 className="flex min-w-0 items-center gap-1.5 text-sm font-semibold tracking-tight"><span className="truncate">{title}</span>{titleAdornment}</h2>
        {description ? <p className="mt-0.5 text-xs leading-5 text-muted-foreground">{description}</p> : null}
        </div>
      </div>
      {action}
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
  NO_REPORTING_CONFIGURATION: "projects.evmReasons.NO_REPORTING_CONFIGURATION",
  NO_ELIGIBLE_EXECUTION_PLANS: "projects.evmReasons.NO_ELIGIBLE_EXECUTION_PLANS",
  INCOMPATIBLE_CURRENCY: "projects.evmReasons.INCOMPATIBLE_CURRENCY",
  REPORTING_CONFIGURATION_INVALID: "projects.evmReasons.REPORTING_CONFIGURATION_INVALID",
  OVERLAPPING_SCOPE: "projects.evmReasons.OVERLAPPING_SCOPE",
  PROGRESS_HISTORY_UNAVAILABLE: "projects.evmReasons.PROGRESS_HISTORY_UNAVAILABLE",
  NO_PLANNED_VALUE: "projects.evmReasons.NO_PLANNED_VALUE",
  NO_EARNED_VALUE: "projects.evmReasons.NO_EARNED_VALUE",
  NO_ACTUAL_COST: "projects.evmReasons.NO_ACTUAL_COST",
  INCOMPLETE_COST_ATTRIBUTION: "projects.evmReasons.INCOMPLETE_COST_ATTRIBUTION",
  UNIFIED_CROSS_PLAN_NETWORK_UNAVAILABLE: "projects.evmReasons.UNIFIED_CROSS_PLAN_NETWORK_UNAVAILABLE",
};

function evmReasonText(reason: string | null | undefined, t: (key: string) => string) {
  return reason ? t(EVM_REASON_KEYS[reason] ?? EVM_REASON_KEYS.EVM_DATA_INCOMPLETE) : t("projects.evmUnavailable");
}

function DetailItem({ label, children, icon: Icon, accent = "text-muted-foreground/60", className = "" }: { label: string; children: React.ReactNode; icon?: LucideIcon; accent?: string; className?: string }) {
  return (
    <div className={cn("group flex min-w-0 items-center justify-between gap-3 border-b border-border/40 py-2 last:border-b-0", className)}>
      <dt className="flex min-w-0 items-center gap-2 text-[11px] text-muted-foreground">
        {Icon ? <Icon aria-hidden="true" className={cn("h-3.5 w-3.5 shrink-0", accent)} /> : null}
        <span className="truncate">{label}</span>
      </dt>
      <dd className="min-w-0 max-w-[62%] truncate text-end text-[13px] font-semibold text-foreground">{children}</dd>
    </div>
  );
}

function DetailGroup({ title, accent = "bg-primary/60", children }: { title: string; accent?: string; children: React.ReactNode }) {
  return (
    <section className="min-w-0">
      <h3 className="mb-1 flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
        <span className={cn("h-3 w-0.5 rounded-full", accent)} aria-hidden="true" />
        {title}
      </h3>
      <dl>{children}</dl>
    </section>
  );
}

function ProgressGauge({ label, value, comparison, comparisonLabel, comparisonUnit, tone, large, locale }: { label: string; value: number | null | undefined; comparison: number | null; comparisonLabel: string; comparisonUnit: string; tone: "green" | "amber" | "red" | "neutral"; large?: boolean; locale: string }) {
  const normalizedValue = isFiniteNumber(value) ? Math.min(100, Math.max(0, value)) : null;
  const progressFraction = normalizedValue === null ? 0 : normalizedValue / 100;
  const markerAngle = Math.PI - progressFraction * Math.PI;
  const markerX = 50 + 42 * Math.cos(markerAngle);
  const markerY = 52 - 42 * Math.sin(markerAngle);
  const displayValue = normalizedValue === null ? "—" : `${englishNumber(normalizedValue, locale)}%`;
  const comparisonValue = isFiniteNumber(comparison) ? `${comparison >= 0 ? "+" : ""}${englishNumber(comparison, locale)} ${comparisonUnit}` : null;
  const progressColorClass = tone === "red" ? "text-destructive" : tone === "amber" ? "text-warning" : tone === "neutral" ? "text-muted-foreground" : "text-success";
  const comparisonColorClass = !isFiniteNumber(comparison) || comparison === 0 ? "text-muted-foreground" : comparison < 0 ? "text-destructive" : "text-success";

  return (
    <div className={cn("min-w-0", large ? "px-1 py-1" : "rounded-lg border bg-card px-3 py-2.5")}>
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
            {comparisonValue ? <span className={cn("rounded-full bg-muted px-1.5 py-0.5 font-semibold leading-none tabular-nums", comparisonColorClass, large ? "text-xs" : "text-[10px]")}>{comparisonValue}</span> : null}
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
  const confidenceAccent = getStatusTextClass(project.confidenceLevel);
  const riskAccent = getRiskLevelTextClass(project.riskRating);
  return <section className="overflow-hidden rounded-2xl border border-border/60 bg-card shadow-sm">
    <div className="grid lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
      <div className="space-y-3 p-4 sm:p-5">
        <div className="flex items-start gap-2.5">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><FileText className="h-4 w-4" /></span>
          <div>
            <h2 className="text-sm font-semibold tracking-tight">{t("projects.projectInformation")}</h2>
            <p className="mt-0.5 text-xs text-muted-foreground">{t("projects.projectInformationDescription")}</p>
          </div>
        </div>
        <div className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <div className="border-b border-border/40 pb-3">
            <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground"><span className="h-3 w-0.5 rounded-full bg-info/60" aria-hidden="true" />{t("projects.projectDescription")}</p>
            <p className="mt-1.5 line-clamp-4 text-sm leading-6">{displayText(project.description)}</p>
          </div>
          <div className="border-b border-border/40 pb-3">
            <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground"><span className="h-3 w-0.5 rounded-full bg-primary/60" aria-hidden="true" />{t("projects.objectiveBusinessCase")}</p>
            <p className="mt-1.5 line-clamp-4 text-sm leading-6">{displayText(project.businessCase)}</p>
          </div>
        </div>
        <div className="border-b border-border/40 pb-1">
          <p className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground"><span className="h-3 w-0.5 rounded-full bg-warning/60" aria-hidden="true" />{t("projects.notes")}</p>
          <p className="mt-1.5 line-clamp-3 text-sm leading-6">{displayText(project.notes)}</p>
        </div>
      </div>
      <div className="border-t border-border/60 bg-gradient-to-b from-muted/20 to-transparent p-4 sm:p-5 lg:border-s lg:border-t-0">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div className="flex min-w-0 items-start gap-2.5">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-info/20 to-info/5 text-info ring-1 ring-inset ring-info/20"><BarChart3 className="h-4 w-4" /></span>
            <div className="min-w-0">
              <h2 className="text-sm font-semibold tracking-tight">{t("projects.projectDetails")}</h2>
              <p className="mt-0.5 text-xs text-muted-foreground">{t("projects.projectInformationDescription")}</p>
            </div>
          </div>
          <div className="shrink-0 text-end">
            <p className="text-[9px] font-semibold uppercase tracking-wider text-muted-foreground">{t("projects.confidenceLevel")}</p>
            <p className={cn("mt-1 flex items-center justify-end gap-2 text-2xl font-bold leading-none tracking-tight", confidenceAccent)}>
              <span className="h-2.5 w-2.5 rounded-full bg-current" aria-hidden="true" />
              {project.confidenceLevel || "—"}
            </p>
          </div>
        </div>
        <div className="grid gap-x-8 gap-y-4 sm:grid-cols-2">
          <DetailGroup accent="bg-info/70" title={t("projects.ownershipHierarchy")}>
            <DetailItem icon={Users} accent="text-info" label={t("projects.projectManager")}>{displayText(project.projectManagerName)}</DetailItem>
            <DetailItem icon={UserCheck} accent="text-info" label={t("projects.sponsor")}>{displayText(project.sponsorName)}</DetailItem>
            <DetailItem icon={Briefcase} accent="text-primary" label={t("projects.portfolio")}>{displayText(project.portfolioName)}</DetailItem>
            <DetailItem icon={Layers} accent="text-primary" label={t("projects.program")}>{overview?.program ? `${overview.program.name}${overview.program.code ? ` · ${overview.program.code}` : ""}` : "—"}</DetailItem>
            <DetailItem icon={Building2} accent="text-info" label={t("projects.department")} className="last:border-b-0">{displayText(project.departmentName)}</DetailItem>
          </DetailGroup>
          <div className="space-y-4">
            <DetailGroup accent="bg-primary/70" title={t("projects.scheduleSection")}>
              <DetailItem icon={CalendarDays} accent="text-primary" label={`${t("projects.startDate")} / ${t("projects.endDate")}`}>{displayDate(project.startDate, locale)} <span className="text-muted-foreground">→</span> {displayDate(project.endDate, locale)}</DetailItem>
              <DetailItem icon={Clock3} accent="text-info" label={t("projects.updatedAt")}>{displayDate(project.updatedAt || project.createdAt, locale)}</DetailItem>
              <DetailItem icon={FileText} accent="text-success" label={t("projects.publishedAt")}>{latestReport ? displayDate(latestReport.publishedAt, locale) : "—"}</DetailItem>
              <DetailItem icon={GitBranch} accent="text-primary" label={t("projects.baseline")} className="border-b-0">{overview?.currentBaseline ? `${overview.currentBaseline.name} · v${englishNumber(overview.currentBaseline.version, locale)}` : "—"}</DetailItem>
            </DetailGroup>
            <DetailGroup accent="bg-warning/70" title={t("projects.classification")}>
              <DetailItem icon={CircleDollarSign} accent="text-success" label={t("projects.currency")}>{displayText(project.currency)}</DetailItem>
              <DetailItem icon={ShieldAlert} accent={riskAccent} label={t("projects.riskRating")} className="border-b-0">{displayText(project.riskRating)}</DetailItem>
            </DetailGroup>
          </div>
        </div>
      </div>
    </div>
  </section>;
}

function ProjectKpiSummary({ project, overview, locale }: { project: ProjectDto; overview?: ProjectOverviewDto; locale: string }) {
  const { t } = useI18n();
  const latestReport = overview?.statusReports.latestPublishedReport;
  const projectHealth = latestReport?.overallHealth || project.health || project.ragStatus;
  const progress = overview?.planProgress ?? project.progress;
  const progressLabel = isFiniteNumber(overview?.planProgress)
    ? t("projects.progressSourceWorkingPlan")
    : t("projects.progressSourceProjectRecord");
  const progressDelta = isFiniteNumber(progress) && isFiniteNumber(latestReport?.overallProgressPercent) ? progress - latestReport.overallProgressPercent : null;
  const progressComparisonLabel = isFiniteNumber(latestReport?.overallProgressPercent)
    ? t("projects.progressComparedToPublished", { value: percent(latestReport.overallProgressPercent, locale) })
    : "";
  const projectHealthTone = getStatusTone(projectHealth);
  const progressTone = projectHealthTone === "red" ? "red" : projectHealthTone === "amber" ? "amber" : projectHealthTone === "green" ? "green" : "neutral";
  const overdueIssues = overview?.issues.overdueOpenIssueCount ?? null;
  const evmCurrency = overview?.currency ?? project.currency;
  const sv = overview?.scheduleVariance ?? null;
  const cv = overview?.costVariance ?? null;
  const scheduleVarianceUnavailableReason = overview?.schedulePerformanceIndexUnavailableReason
    ?? overview?.earnedValueUnavailableReason
    ?? overview?.plannedValueUnavailableReason;
  const varianceData = [
    { name: t("projects.scheduleVariance"), value: sv },
    { name: t("projects.costVariance"), value: cv },
  ].filter((item): item is { name: string; value: number } => isFiniteNumber(item.value));
  const varianceLimit = Math.max(1, ...varianceData.map((item) => Math.abs(item.value)));
  const attentionData = overdueIssues !== null
    ? [{ name: t("projects.overdueOpenIssues"), value: overdueIssues }]
    : [];

  return <section aria-labelledby="project-kpi-heading" className="min-w-0">
    <Card className={cn(PREMIUM_CARD, "h-full")}>
      <CardHeader className={PREMIUM_CARD_HEADER}>
        <div className="flex flex-col gap-1.5 border-b border-border/60 pb-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex min-w-0 items-start gap-2.5">
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><BarChart3 className="h-4 w-4" /></span>
            <div className="min-w-0">
              <h2 id="project-kpi-heading" className="text-sm font-semibold tracking-tight">{t("projects.kpiSummary")}</h2>
              <p className="mt-0.5 text-[11px] text-muted-foreground">{latestReport ? `${t("projects.publishedAt")}: ${displayDate(latestReport.publishedAt, locale)}` : t("projects.noStatusReports")}</p>
            </div>
          </div>
        </div>
      </CardHeader>
      <CardContent className={PREMIUM_CARD_CONTENT}>
        <div className="grid min-w-0 items-center gap-4 sm:grid-cols-[minmax(150px,0.8fr)_minmax(0,1.2fr)]">
          <ProgressGauge label={progressLabel} value={progress} comparison={progressDelta} comparisonLabel={progressComparisonLabel} comparisonUnit={t("projects.percentagePointsAbbreviation")} tone={progressTone} large locale={locale} />
          <div className="grid min-w-0 grid-cols-2 gap-2.5">
            <section className="col-span-2 min-w-0 rounded-lg border border-border/50 p-2">
              <h3 className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.varianceComparison")}</h3>
              {varianceData.length ? <div className="h-28">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={varianceData} layout="vertical" margin={{ top: 3, right: locale === "ar" ? 8 : 48, left: locale === "ar" ? 48 : 8, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" horizontal={false} className="stroke-border" />
                    <XAxis type="number" domain={[-varianceLimit, varianceLimit]} tickLine={false} axisLine={false} tick={{ fontSize: 9 }} tickFormatter={(value) => englishNumber(Number(value), locale)} />
                    <YAxis type="category" dataKey="name" width={locale === "ar" ? 118 : 108} orientation={locale === "ar" ? "right" : "left"} tickLine={false} axisLine={false} tick={{ fontSize: 9 }} />
                    <ReferenceLine x={0} stroke="hsl(var(--border))" />
                    <Tooltip formatter={(value: unknown) => nullableCurrency(typeof value === "number" ? value : undefined, evmCurrency, locale)} />
                    <Bar dataKey="value" name={t("projects.varianceComparison")} radius={[3, 3, 3, 3]} maxBarSize={18}>
                      {varianceData.map((item) => <Cell key={item.name} fill={item.value < 0 ? "hsl(var(--destructive))" : "hsl(var(--chart-2))"} />)}
                      <LabelList dataKey="value" position="right" formatter={(value: unknown) => nullableCurrency(typeof value === "number" ? value : undefined, evmCurrency, locale)} style={{ fontSize: 9, fill: "hsl(var(--foreground))" }} />
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div> : <div className="space-y-1 py-2 text-[10px] text-muted-foreground">
                <p>{t("projects.scheduleVariance")}: {evmReasonText(scheduleVarianceUnavailableReason, t)}</p>
                <p>{t("projects.costVariance")}: {evmReasonText(overview?.costVarianceUnavailableReason, t)}</p>
              </div>}
            </section>
            <section className="min-w-0 border-b border-border/40 pb-2.5">
              <div className="flex items-center justify-between gap-1">
                <dt className="text-[10px] font-medium text-muted-foreground">{t("projects.overdueOpenIssues")}</dt>
                <p className={cn("text-lg font-semibold tabular-nums", overdueIssues === null ? "text-muted-foreground" : overdueIssues > 0 ? "text-destructive" : "text-success")}>{englishNumber(overdueIssues, locale)}</p>
              </div>
              {attentionData.length ? <div className="mt-1 h-16">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={attentionData} layout="vertical" margin={{ top: 0, right: locale === "ar" ? 22 : 22, left: locale === "ar" ? 4 : 0, bottom: 0 }}>
                    <XAxis type="number" allowDecimals={false} hide />
                    <YAxis type="category" dataKey="name" width={100} orientation={locale === "ar" ? "right" : "left"} tickLine={false} axisLine={false} tick={{ fontSize: 8 }} />
                    <Tooltip formatter={(value) => englishNumber(typeof value === "number" ? value : undefined, locale)} />
                    <Bar dataKey="value" fill="hsl(var(--destructive))" radius={[0, 3, 3, 0]} maxBarSize={12}>
                      <LabelList dataKey="value" position="right" style={{ fontSize: 9, fill: "hsl(var(--foreground))" }} />
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div> : <p className="mt-2 text-[10px] text-muted-foreground">{t("projects.overviewDataUnavailable")}</p>}
            </section>
          </div>
        </div>
      </CardContent>
    </Card>
  </section>;
}

const OVERVIEW_CHART_COLORS = ["hsl(var(--chart-1))", "hsl(var(--chart-2))", "hsl(var(--chart-3))", "hsl(var(--chart-4))", "hsl(var(--chart-5))"];

function getRiskStatusChartColor(status: string, index: number) {
  const normalized = status.toLowerCase().replace(/[\s_-]/g, "");
  const semanticColors: Record<string, string> = {
    identified: "hsl(var(--info))",
    actionrequired: "hsl(var(--warning))",
    followup: "hsl(var(--chart-6))",
    mitigated: "hsl(var(--success))",
  };
  return semanticColors[normalized] ?? OVERVIEW_CHART_COLORS[index % OVERVIEW_CHART_COLORS.length];
}

const WORK_ITEM_STATUS_CHART_COLORS: Record<string, string> = {
  slate: "hsl(var(--muted-foreground))",
  blue: "hsl(var(--info))",
  amber: "hsl(var(--warning))",
  violet: "hsl(var(--chart-6))",
  emerald: "hsl(var(--success))",
  rose: "hsl(var(--destructive))",
  orange: "hsl(var(--chart-5))",
};

function getIssueStatusChartColor(color: string | null | undefined, status: string, index: number) {
  const configuredColor = color?.trim().toLowerCase();
  if (configuredColor && WORK_ITEM_STATUS_CHART_COLORS[configuredColor]) return WORK_ITEM_STATUS_CHART_COLORS[configuredColor];

  const normalizedStatus = status.toLowerCase().replace(/[\s_-]/g, "");
  const semanticColors: Record<string, string> = {
    open: "hsl(var(--info))",
    todo: "hsl(var(--info))",
    new: "hsl(var(--info))",
    inprogress: "hsl(var(--warning))",
    inreview: "hsl(var(--chart-6))",
    blocked: "hsl(var(--destructive))",
    overdue: "hsl(var(--destructive))",
    done: "hsl(var(--success))",
    resolved: "hsl(var(--success))",
    closed: "hsl(var(--success))",
    cancelled: "hsl(var(--muted-foreground))",
    canceled: "hsl(var(--muted-foreground))",
  };
  return semanticColors[normalizedStatus] ?? OVERVIEW_CHART_COLORS[index % OVERVIEW_CHART_COLORS.length];
}

function OverviewQuestionDashboard({
  project,
  overview,
  locale,
  teamCapacityQuery,
  capacityPeriodType,
  onCapacityPeriodTypeChange,
}: {
  project: ProjectDto;
  overview: ProjectOverviewDto;
  locale: string;
  teamCapacityQuery: ReturnType<typeof useProjectTeamCapacity>;
  capacityPeriodType: ProjectCapacityPeriodType;
  onCapacityPeriodTypeChange: (periodType: ProjectCapacityPeriodType) => void;
}) {
  const { t } = useI18n();
  const currency = overview.currency ?? project.currency;

  const spent = overview.actualCost;
  const committed = overview.committedCost;
  const approved = overview.approvedBudget;
  const money = (value: number | null | undefined) => nullableCurrency(value, currency, locale);
  const financialSummaryData = [
    { key: "spent", name: t("projects.actualSpend"), value: spent, color: "hsl(var(--chart-1))" },
    { key: "committed", name: t("projects.dueCommitted"), value: committed, color: "hsl(var(--chart-2))" },
  ];
  const financialChartData = financialSummaryData.filter((item): item is { key: string; name: string; value: number; color: string } => isFiniteNumber(item.value) && item.value > 0);
  const hasFinancialSummary = [approved, spent, committed].some(isFiniteNumber);

  const teamCapacity = teamCapacityQuery.data;
  const capacityDataReady = teamCapacity?.isCapacityDataAvailable === true
    && teamCapacity.isAllocationDataAvailable === true;
  const capacityDataIncomplete = Boolean(teamCapacity && !capacityDataReady && teamCapacity.activeResourceCount !== 0);
  const totalCapacityHours = capacityDataReady && isFiniteNumber(teamCapacity?.totalCapacityHours)
    ? teamCapacity.totalCapacityHours
    : null;
  const utilizedHours = capacityDataReady && isFiniteNumber(teamCapacity?.utilizedHours)
    ? teamCapacity.utilizedHours
    : null;
  const availableHours = capacityDataReady && isFiniteNumber(teamCapacity?.availableHours)
    ? teamCapacity.availableHours
    : null;
  const utilizedPercent = capacityDataReady && isFiniteNumber(teamCapacity?.utilizedPercent)
    ? teamCapacity.utilizedPercent
    : null;
  const availablePercent = capacityDataReady && isFiniteNumber(teamCapacity?.availablePercent)
    ? teamCapacity.availablePercent
    : null;
  const teamCapacityTotal = isFiniteNumber(totalCapacityHours) ? totalCapacityHours : 0;
  const chartUtilizedHours = isFiniteNumber(utilizedHours) ? Math.min(utilizedHours, teamCapacityTotal) : 0;
  const chartAvailableHours = Math.max(0, teamCapacityTotal - chartUtilizedHours);
  const capacityChartData = [
    { key: "utilized", name: t("projects.utilizedCapacity"), value: chartUtilizedHours, color: "hsl(var(--chart-1))" },
    { key: "available", name: t("projects.availableCapacity"), value: chartAvailableHours, color: "hsl(var(--success))" },
  ].filter((item) => item.value > 0);
  const capacityHasChart = capacityDataReady && teamCapacityTotal > 0 && capacityChartData.length > 0;
  const capacityChartDisplayData = capacityHasChart
    ? capacityChartData
    : [{ key: "unavailable", name: t("projects.capacityDistributionUnavailable"), value: 1, color: "hsl(var(--muted))" }];
  const hoursLabel = (value: number | null | undefined) => isFiniteNumber(value)
    ? `${englishNumber(value, locale, 1)} ${t("projects.hoursUnit")}`
    : "—";
  const percentageLabel = (value: number | null | undefined) => isFiniteNumber(value)
    ? `${englishNumber(value, locale, 1)}%`
    : "—";

  const openIssues = overview.issues.openIssueCount;
  const overdueIssues = overview.issues.overdueOpenIssueCount;
  const issueStatusData = (overview.issues.statusCounts ?? [])
    .map((item, index) => {
      const localizedName = locale === "ar" ? item.nameAr : item.nameEn;
      return {
        code: item.status,
        name: localizedName?.trim() || humanizeCode(item.status),
        value: item.count,
        displayOrder: item.displayOrder ?? Number.MAX_SAFE_INTEGER,
        color: getIssueStatusChartColor(item.color, item.status, index),
      };
    })
    .filter((item) => isFiniteNumber(item.value) && item.value > 0)
    .sort((a, b) => a.displayOrder - b.displayOrder || b.value - a.value || a.name.localeCompare(b.name, locale));
  const issueStatusTotal = issueStatusData.reduce((total, item) => total + item.value, 0);
  const topIssues = overview.issues.topIssues ?? [];
  const statusName = (code: string) => issueStatusData.find((item) => item.code.toLowerCase() === code.toLowerCase())?.name
    ?? humanizeCode(code);

  const nonClosedRisks = overview.risks.openCount;
  const riskStatusRows = (overview.risks.statusCounts ?? []).map((item) => {
    const key = `enums.riskStatus.${item.status.charAt(0).toLowerCase()}${item.status.slice(1)}`;
    const localizedName = t(key);
    return { status: item.status, name: localizedName === key ? humanizeCode(item.status) : localizedName, value: item.count };
  }).filter((item) => item.value > 0);
  const riskStatusTotal = riskStatusRows.reduce((total, item) => total + item.value, 0);
  const riskStatusBreakdownAvailable = Array.isArray(overview.risks.statusCounts) && riskStatusTotal === nonClosedRisks;
  const riskStatusData = riskStatusRows.map((item, index) => ({
    ...item,
    color: getRiskStatusChartColor(item.status, index),
    percentage: riskStatusTotal > 0 ? item.value / riskStatusTotal * 100 : 0,
  }));
  const issueStatusBreakdownAvailable = Array.isArray(overview.issues.statusCounts)
    && (issueStatusData.length > 0 || openIssues === 0);

  return (
    <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-2">
      <Card className={cn(PREMIUM_CARD, "order-3")}>
        <CardHeader className={PREMIUM_CARD_HEADER}><SectionTitle icon={CircleDollarSign} accent="bg-success/10 text-success" title={t("projects.financialSummary")} description={t("projects.financialSummaryDescription")} /></CardHeader>
        <CardContent className={PREMIUM_CARD_CONTENT}>
          {hasFinancialSummary ? <>
            <div className="grid items-center gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(180px,0.85fr)]">
              <div className="relative mx-auto h-[270px] w-full max-w-[300px]" role="img" aria-label={t("projects.financialSummary")}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={financialChartData.length ? financialChartData : [{ key: "empty", name: t("projects.financialSummary"), value: 1, color: "hsl(var(--muted))" }]}
                      dataKey="value"
                      nameKey="name"
                      cx="50%"
                      cy="43%"
                      innerRadius={68}
                      outerRadius={104}
                      paddingAngle={financialChartData.length > 1 ? 3 : 0}
                    >
                      {financialChartData.length
                        ? financialChartData.map((item) => <Cell key={item.key} fill={item.color} />)
                        : <Cell fill="hsl(var(--muted))" />}
                    </Pie>
                    {financialChartData.length ? <Tooltip formatter={(value) => money(typeof value === "number" ? value : undefined)} contentStyle={{ fontSize: 12 }} /> : null}
                  </PieChart>
                </ResponsiveContainer>
                <div aria-hidden="true" className="pointer-events-none absolute left-1/2 top-[43%] flex max-w-[140px] -translate-x-1/2 -translate-y-1/2 flex-col items-center text-center">
                  <span className="text-sm font-semibold leading-5 tabular-nums">{money(approved)}</span>
                  <span className="mt-1 text-xs leading-4 text-muted-foreground">{t("projects.totalBudget")}</span>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-1">
                {financialSummaryData.map((item) => <div key={item.key} className="min-w-0 rounded-md border border-t-2 px-3 py-2.5" style={{ borderTopColor: item.color }}>
                  <p className="text-xs font-medium leading-4 text-muted-foreground">{item.name}</p>
                  <p className="mt-1 text-base font-semibold leading-5 tabular-nums" title={money(item.value)}>{money(item.value)}</p>
                </div>)}
              </div>
            </div>
            <p className="mt-2 text-xs leading-5 text-muted-foreground">{t("projects.financialDonutNote")}</p>
          </> : <EmptyPanel title={t("projects.overviewDataUnavailable")} description={t("projects.financialSummaryDescription")} />}
        </CardContent>
      </Card>

      <Card className={cn(PREMIUM_CARD, "order-4")}>
        <CardHeader className={PREMIUM_CARD_HEADER}>
          <SectionTitle
            icon={Users}
            accent="bg-info/10 text-info"
            title={t("projects.teamAndAllocation")}
            description={t("projects.teamAndAllocationDescription")}
            titleAdornment={capacityDataIncomplete ? (
              <span className="group relative inline-flex shrink-0">
                <button
                  type="button"
                  className="inline-flex h-5 w-5 items-center justify-center rounded-full text-warning outline-none hover:bg-warning/10 focus-visible:ring-2 focus-visible:ring-warning"
                  aria-label={t("projects.teamCapacityUnavailable")}
                  aria-describedby="project-team-capacity-unavailable-tooltip"
                >
                  <Info aria-hidden="true" className="h-4 w-4" />
                </button>
                <span
                  id="project-team-capacity-unavailable-tooltip"
                  role="tooltip"
                  className="pointer-events-none absolute start-0 top-full z-50 mt-2 w-64 max-w-[calc(100vw-2rem)] rounded-md border border-border bg-popover p-3 text-start text-xs font-normal leading-5 text-popover-foreground opacity-0 shadow-lg transition-opacity group-hover:opacity-100 group-focus-within:opacity-100"
                >
                  <span className="block font-semibold">{t("projects.teamCapacityUnavailable")}</span>
                  <span className="mt-1 block">{t("projects.teamCapacityIncompleteDescription")}</span>
                </span>
              </span>
            ) : null}
            action={(
              <div className="w-32 shrink-0">
                <Select value={capacityPeriodType} onValueChange={(value) => onCapacityPeriodTypeChange(value as ProjectCapacityPeriodType)}>
                  <SelectTrigger className="h-8 text-xs" aria-label={t("projects.reportingPeriod")}><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Week">{t("projects.capacityPeriods.week")}</SelectItem>
                    <SelectItem value="Month">{t("projects.capacityPeriods.month")}</SelectItem>
                    <SelectItem value="Quarter">{t("projects.capacityPeriods.quarter")}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            )}
          />
        </CardHeader>
        <CardContent className={cn(PREMIUM_CARD_CONTENT, "space-y-4")}>
          {teamCapacityQuery.isLoading ? (
            <Skeleton className="mx-auto h-52 w-full max-w-[440px]" role="status" aria-label={t("common.loading")} />
          ) : teamCapacityQuery.isError ? (
            <ErrorState title={t("projects.teamCapacityUnavailable")} onRetry={() => void teamCapacityQuery.refetch()} />
          ) : (
            <>
              {teamCapacity?.activeResourceCount === 0 ? (
                <EmptyPanel title={t("projects.noTeamMembers")} description={t("projects.teamCapacityEmptyDescription")} />
              ) : (
                <div className="grid items-center gap-3 sm:grid-cols-[minmax(210px,0.9fr)_minmax(190px,1.1fr)]">
                  <div
                    className="relative mx-auto h-[230px] w-full max-w-[280px]"
                    role="img"
                    aria-label={capacityHasChart
                      ? `${t("projects.capacityDistribution")}: ${t("projects.totalCapacity")} ${hoursLabel(totalCapacityHours)}`
                      : t("projects.capacityDistributionUnavailable")}
                  >
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie data={capacityChartDisplayData} dataKey="value" nameKey="name" cx="50%" cy="50%" innerRadius={68} outerRadius={102} paddingAngle={capacityHasChart && capacityChartData.length > 1 ? 3 : 0}>
                          {capacityChartDisplayData.map((item) => <Cell key={item.key} fill={item.color} />)}
                        </Pie>
                        {capacityHasChart ? <Tooltip
                          formatter={(value: unknown, name: unknown) => {
                            const hours = typeof value === "number" && Number.isFinite(value) ? value : undefined;
                            const share = hours !== undefined && teamCapacityTotal > 0 ? hours / teamCapacityTotal * 100 : 0;
                            return [`${hoursLabel(hours)} (${percentageLabel(share)})`, String(name ?? "")];
                          }}
                          contentStyle={{ fontSize: 13 }}
                        /> : null}
                      </PieChart>
                    </ResponsiveContainer>
                    <div aria-hidden="true" className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
                      <span className="text-2xl font-semibold leading-none tabular-nums">{totalCapacityHours === null ? "—" : hoursLabel(totalCapacityHours)}</span>
                      <span className="mt-1 max-w-[120px] text-xs leading-4 text-muted-foreground">{t("projects.totalCapacity")}</span>
                    </div>
                  </div>
                  <ul className="grid content-center gap-2" aria-label={t("projects.capacityDistribution")}>
                    <li className="flex min-w-0 items-center gap-2 rounded-md border border-border/50 px-3 py-2.5 text-sm">
                      <span aria-hidden="true" className="h-3 w-3 shrink-0 rounded-full" style={{ backgroundColor: "hsl(var(--chart-1))" }} />
                      <span className="min-w-0 flex-1 font-medium">{t("projects.utilizedCapacity")}</span>
                      <span className="text-end font-semibold tabular-nums">{hoursLabel(utilizedHours)} <span className="font-normal text-muted-foreground">({percentageLabel(utilizedPercent)})</span></span>
                    </li>
                    <li className="flex min-w-0 items-center gap-2 rounded-md border border-success/30 bg-success/5 px-3 py-2.5 text-sm">
                      <span aria-hidden="true" className="h-3 w-3 shrink-0 rounded-full" style={{ backgroundColor: "hsl(var(--success))" }} />
                      <span className="min-w-0 flex-1 font-medium text-success">{t("projects.availableCapacity")}</span>
                      <span className="text-end font-semibold tabular-nums text-success">{hoursLabel(availableHours)} <span className="font-normal">({percentageLabel(availablePercent)})</span></span>
                    </li>
                  </ul>
                </div>
              )}

              {capacityDataReady && !capacityHasChart && teamCapacity?.activeResourceCount !== 0 ? (
                <EmptyPanel title={t("projects.noCapacityInPeriod")} description={t("projects.noCapacityInPeriodDescription")} />
              ) : null}

              {capacityDataReady && isFiniteNumber(teamCapacity?.overallocatedHours) && teamCapacity.overallocatedHours > 0 ? (
                <p role="status" className="rounded-md border border-warning/30 bg-warning/5 px-3 py-2 text-sm text-warning">
                  {t("projects.overallocatedCapacity").replace("{{hours}}", hoursLabel(teamCapacity.overallocatedHours))}
                </p>
              ) : null}
            </>
          )}
          <div className="grid gap-2 sm:grid-cols-2" data-testid="project-team-capacity-kpis">
            <div className="min-w-0 rounded-md border border-border/60 border-t-2 border-t-info px-3 py-2.5">
              <div className="flex items-start gap-2">
                <Users aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-info" />
                <div className="min-w-0">
                  <p className="text-xs font-medium leading-4 text-muted-foreground">{t("projects.teamMemberKpi")}</p>
                  <p className="mt-1 text-xl font-semibold leading-6 tabular-nums">{teamCapacity ? englishNumber(teamCapacity.activeResourceCount, locale) : "—"}</p>
                  <p className="mt-1 text-xs leading-4 text-muted-foreground">{t("projects.assignedResources")}</p>
                </div>
              </div>
            </div>
            <div className="min-w-0 rounded-md border border-border/60 border-t-2 border-t-primary px-3 py-2.5">
              <div className="flex items-start gap-2">
                <Clock3 aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-primary" />
                <div className="min-w-0">
                  <p className="text-xs font-medium leading-4 text-muted-foreground">{t("projects.utilizedCapacity")}</p>
                  <p className="mt-1 text-xl font-semibold leading-6 tabular-nums">{percentageLabel(utilizedPercent)}</p>
                  <p className="mt-1 text-xs leading-4 text-muted-foreground">{t("projects.allocatedHours")}: {hoursLabel(utilizedHours)}</p>
                </div>
              </div>
            </div>
            <div className="min-w-0 rounded-md border border-border/60 border-t-2 border-t-warning px-3 py-2.5">
              <div className="flex items-start gap-2">
                <UserCheck aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
                <div className="min-w-0">
                  <p className="text-xs font-medium leading-4 text-muted-foreground">{t("projects.idleResources")}</p>
                  <p className="mt-1 text-xl font-semibold leading-6 tabular-nums">{teamCapacity?.isAllocationDataAvailable && teamCapacity.idleResourceCount !== null ? englishNumber(teamCapacity.idleResourceCount, locale) : "—"}</p>
                  <p className="mt-1 text-xs leading-4 text-muted-foreground">{t("projects.noAllocatedWork")}</p>
                </div>
              </div>
            </div>
            <div className="min-w-0 rounded-md border border-success/30 border-t-2 border-t-success bg-success/5 px-3 py-2.5">
              <div className="flex items-start gap-2">
                <CheckCircle2 aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0 text-success" />
                <div className="min-w-0">
                  <p className="text-xs font-medium leading-4 text-success">{t("projects.availableCapacity")}</p>
                  <p className="mt-1 text-xl font-semibold leading-6 tabular-nums text-success">{percentageLabel(availablePercent)}</p>
                  <p className="mt-1 text-xs leading-4 text-success">{t("projects.availableHours")}: {hoursLabel(availableHours)}</p>
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className={cn(PREMIUM_CARD, "order-1")}>
        <CardHeader className={PREMIUM_CARD_HEADER}><SectionTitle icon={AlertTriangle} accent="bg-warning/10 text-warning" title={t("projects.issues")} description={t("projects.issueBreakdown")} action={<Link to={`/risks-issues/issues?projectId=${encodeURIComponent(project.publicId)}`} className="inline-flex h-7 shrink-0 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} /></CardHeader>
        <CardContent className={PREMIUM_CARD_CONTENT}>
          {issueStatusBreakdownAvailable && issueStatusData.length ? (
            <div className="border-b border-border/50 pb-3">
              <h3 className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.issuesByStatus")}</h3>
              <div className="grid items-center gap-2 sm:grid-cols-[minmax(135px,0.8fr)_minmax(150px,1.2fr)]">
                <div className="relative mx-auto h-[205px] w-full max-w-[220px]" role="img" aria-label={`${t("projects.issuesByStatus")}: ${t("projects.issueTotal")} ${englishNumber(issueStatusTotal, locale)}`}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie data={issueStatusData} dataKey="value" nameKey="name" cx="50%" cy="50%" innerRadius={48} outerRadius={76} paddingAngle={issueStatusData.length > 1 ? 2 : 0}>
                        {issueStatusData.map((item) => <Cell key={item.code} fill={item.color} />)}
                      </Pie>
                      <Tooltip
                        formatter={(value: unknown, name: unknown) => {
                          const count = typeof value === "number" && Number.isFinite(value) ? value : undefined;
                          const status = issueStatusData.find((item) => item.name === String(name));
                          const percentage = status && issueStatusTotal > 0 ? Math.round(status.value / issueStatusTotal * 100) : 0;
                          return [`${englishNumber(count, locale)} (${englishNumber(percentage, locale)}%)`, status?.name ?? String(name ?? "")];
                        }}
                        contentStyle={{ fontSize: 12 }}
                      />
                    </PieChart>
                  </ResponsiveContainer>
                  <div aria-hidden="true" className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
                    <span className="text-2xl font-semibold leading-none tabular-nums">{englishNumber(issueStatusTotal, locale)}</span>
                    <span className="mt-1 max-w-[96px] text-[11px] leading-4 text-muted-foreground">{t("projects.issueTotal")}</span>
                  </div>
                </div>
                <ul className="grid content-center gap-1.5" aria-label={t("projects.issuesByStatus")}>
                  {issueStatusData.map((item) => (
                    <li key={item.code} className="flex min-w-0 items-center gap-2 rounded-md border border-border/40 px-2 py-1.5 text-xs">
                      <span aria-hidden="true" className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: item.color }} />
                      <span className="min-w-0 flex-1 truncate font-medium" title={item.name}>{item.name}</span>
                      <span className="shrink-0 text-end font-semibold tabular-nums">{englishNumber(item.value, locale)} <span className="font-normal text-muted-foreground">({englishNumber(Math.round(item.value / issueStatusTotal * 100), locale)}%)</span></span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          ) : <EmptyPanel title={issueStatusBreakdownAvailable ? t("projects.noIssuesYet") : t("projects.issueStatusBreakdownUnavailable")} description={t("projects.attentionRequiredDescription")} className="mb-3" />}
          <div className="mt-3 flex items-center justify-between gap-2">
            <h3 className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.openIssues")}</h3>
            <span className="text-xs font-semibold tabular-nums text-foreground">{englishNumber(openIssues, locale)} <span className="font-normal text-muted-foreground">· {t("projects.overdueOpenIssues")}: </span><span className={overdueIssues > 0 ? "text-destructive" : "text-muted-foreground"}>{englishNumber(overdueIssues, locale)}</span></span>
          </div>
          {topIssues.length ? (
            <div className="mt-1 divide-y divide-border/50">
              {topIssues.map((issue) => (
                <Link key={issue.id} to={`/work-items/${encodeURIComponent(issue.id)}`} className="block rounded-sm px-1 py-2 transition-colors hover:bg-muted/30">
                  <p className="truncate text-xs font-medium text-foreground">{displayText(issue.title)}</p>
                  <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[10px] text-muted-foreground">
                    <Badge status={issue.status} className="px-1.5 py-0 text-[9px]">{statusName(issue.status)}</Badge>
                    <span>{t(`workItems.priority${issue.priority}`)}</span>
                    {issue.dueDate ? <span>{t("common.dueDate")}: {displayDate(issue.dueDate, locale)}</span> : null}
                  </div>
                </Link>
              ))}
            </div>
          ) : <p className="mt-2 text-xs text-muted-foreground">{t("projects.noOpenIssues")}</p>}
        </CardContent>
      </Card>

      <Card className={cn(PREMIUM_CARD, "order-2")}>
        <CardHeader className={PREMIUM_CARD_HEADER}><SectionTitle icon={ShieldAlert} accent="bg-destructive/10 text-destructive" title={t("projects.risksOverview")} description={t("projects.nonClosedRiskDescription")} action={<Link to={`/risks-issues/risks?projectId=${encodeURIComponent(project.publicId)}`} className="inline-flex h-7 shrink-0 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} /></CardHeader>
        <CardContent className={PREMIUM_CARD_CONTENT}>
          {!riskStatusBreakdownAvailable ? <EmptyPanel title={t("projects.overviewDataUnavailable")} description={t("projects.riskStatusBreakdownUnavailable")} />
            : nonClosedRisks === 0 ? <EmptyPanel title={t("projects.noNonClosedRisks")} description={t("projects.nonClosedRiskDescription")} />
              : <div className="grid items-center gap-3 lg:grid-cols-[minmax(220px,0.9fr)_minmax(190px,1.1fr)]">
                <div className="relative mx-auto h-[290px] w-full max-w-[320px]" role="img" aria-label={`${t("projects.risksByStatus")}: ${t("projects.nonClosedRisks")} ${englishNumber(nonClosedRisks, locale)}`}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie data={riskStatusData} dataKey="value" nameKey="name" cx="50%" cy="50%" innerRadius={74} outerRadius={108} paddingAngle={2}>
                        {riskStatusData.map((item) => <Cell key={item.status} fill={item.color} />)}
                      </Pie>
                      <Tooltip
                        formatter={(value, name) => {
                          const count = typeof value === "number" && Number.isFinite(value) ? value : undefined;
                          const status = riskStatusData.find((item) => item.name === String(name));
                          const percentage = status && riskStatusTotal > 0 ? Math.round(status.value / riskStatusTotal * 100) : 0;
                          return [`${englishNumber(count, locale)} (${englishNumber(percentage, locale)}%)`, status?.name ?? String(name ?? "")];
                        }}
                        contentStyle={{ fontSize: 13 }}
                      />
                    </PieChart>
                  </ResponsiveContainer>
                  <div aria-hidden="true" className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
                    <span className="text-3xl font-semibold leading-none tabular-nums">{englishNumber(nonClosedRisks, locale)}</span>
                    <span className="mt-2 max-w-[118px] text-xs leading-4 text-muted-foreground">{t("projects.nonClosedRisks")}</span>
                  </div>
                </div>
                <ul className="grid gap-x-4 gap-y-3 sm:grid-cols-2 lg:grid-cols-1" aria-label={t("projects.risksByStatus")}>
                  {riskStatusData.map((item) => <li key={item.status} className="flex min-w-0 items-center gap-2 text-sm">
                    <span className="h-3 w-3 shrink-0 rounded-full" style={{ backgroundColor: item.color }} aria-hidden="true" />
                    <span className="min-w-0 flex-1 truncate font-medium" title={item.name}>{item.name}</span>
                    <span className="shrink-0 font-semibold tabular-nums">{englishNumber(item.value, locale)}</span>
                    <span className="w-12 shrink-0 text-end text-muted-foreground tabular-nums">{englishNumber(Math.round(item.percentage), locale)}%</span>
                  </li>)}
                </ul>
              </div>}
        </CardContent>
      </Card>
    </section>
  );
}

export function ProjectOverviewTab() {
  const { projectId, project } = useOutletContext<{
    projectId: string;
    project: ProjectDto;
  }>();
  const { t, locale } = useI18n();
  const navigate = useNavigate();
  const query = useProjectOverview(projectId, Boolean(projectId));
  const [capacityPeriodType, setCapacityPeriodType] = React.useState<ProjectCapacityPeriodType>("Month");
  const capacityPeriod = React.useMemo(() => getCapacityReportingPeriod(capacityPeriodType), [capacityPeriodType]);
  const teamCapacityQuery = useProjectTeamCapacity(
    projectId,
    capacityPeriodType,
    capacityPeriod.startDate,
    capacityPeriod.endDate,
    Boolean(projectId),
  );
  const overview = query.data;
  const projectInformation = <ProjectInformationCard project={project} overview={overview} locale={locale} />;

  if (query.isLoading) {
    return (
      <div className="space-y-3 pt-1">
        {projectInformation}
        <div className="grid gap-3 lg:grid-cols-2" role="status" aria-label={t("common.loading")}>
          <ProjectKpiSummary project={project} locale={locale} />
          <Card className="shadow-none">
            <CardContent className="space-y-3 p-4">
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="h-56 w-full" />
              <Skeleton className="h-16 w-full" />
            </CardContent>
          </Card>
        </div>
        <div className="grid gap-3 sm:grid-cols-2" aria-hidden="true">
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
        <div className="grid gap-3 lg:grid-cols-2">
          <ProjectKpiSummary project={project} locale={locale} />
          <Card className="shadow-none">
            <CardContent className="p-0">
              <ErrorState title={t("projects.overviewDataUnavailable")} onRetry={() => void query.refetch()} />
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  const milestones = overview.milestones ?? [];
  const milestoneProgressData = milestones
    .filter((milestone) => isFiniteNumber(milestone.progress) && milestone.progress >= 0 && milestone.progress <= 100)
    .map((milestone) => ({ id: milestone.id, name: milestone.name, progress: milestone.progress }));
  const publishedRatio = overview.statusReports.totalCount > 0
    && overview.statusReports.publishedCount <= overview.statusReports.totalCount
    ? Math.round((overview.statusReports.publishedCount / overview.statusReports.totalCount) * 100)
    : null;
  const unpublishedReportCount = overview.statusReports.publishedCount <= overview.statusReports.totalCount
    ? overview.statusReports.totalCount - overview.statusReports.publishedCount
    : null;
  const meetingsWithoutFinalMinutes = overview.meetings.finalMinutesCount <= overview.meetings.meetingCount
    ? overview.meetings.meetingCount - overview.meetings.finalMinutesCount
    : null;

  return (
    <div className="space-y-4 pt-1">
      {projectInformation}
      <OverviewQuestionDashboard
        project={project}
        overview={overview}
        locale={locale}
        teamCapacityQuery={teamCapacityQuery}
        capacityPeriodType={capacityPeriodType}
        onCapacityPeriodTypeChange={setCapacityPeriodType}
      />
      <div data-testid="project-overview-kpi-performance-row" className="grid items-stretch gap-3">
        <ProjectKpiSummary project={project} overview={overview} locale={locale} />
      </div>

      <div data-testid="project-overview-milestones-layout" className="grid gap-4">
        <Card className={PREMIUM_CARD}>
          <CardHeader className={PREMIUM_CARD_HEADER}>
            <SectionTitle icon={CalendarDays} title={t("projects.milestones")} description={t("projects.milestoneTimelineDescription")} action={<Link to={`/projects/${projectId}/plan`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className={PREMIUM_CARD_CONTENT}>
            {milestones.length ? (
              <>
                <div className="mb-3 flex flex-wrap gap-x-4 gap-y-2 border-b border-border/60 pb-3" aria-label={t("projects.milestoneStatusLegend")}>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-success" />{t("projects.milestoneCompleted")}</span>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-warning" />{t("projects.milestoneInProgress")}</span>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-destructive" />{t("projects.milestoneOverdue")}</span>
                  <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-info" />{t("projects.milestoneUpcoming")}</span>
                </div>
                <section className="mb-3 border-b border-border/50 pb-3" aria-label={t("projects.milestoneProgressChart")}>
                  <h3 className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.milestoneProgressChart")}</h3>
                  {milestoneProgressData.length ? <div className="w-full" style={{ height: Math.max(96, milestoneProgressData.length * 28) }} role="img" aria-label={t("projects.milestoneProgressChart")}>
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={milestoneProgressData} layout="vertical" margin={{ top: 3, right: locale === "ar" ? 4 : 18, left: locale === "ar" ? 18 : 4, bottom: 0 }}>
                        <CartesianGrid strokeDasharray="3 3" horizontal={false} className="stroke-border" />
                        <XAxis type="number" domain={[0, 100]} tickLine={false} axisLine={false} tick={{ fontSize: 9 }} tickFormatter={(value) => `${englishNumber(Number(value), locale)}%`} />
                        <YAxis type="category" dataKey="name" width={locale === "ar" ? 132 : 120} orientation={locale === "ar" ? "right" : "left"} tickLine={false} axisLine={false} tick={{ fontSize: 9 }} />
                        <Tooltip formatter={(value: unknown) => [percent(typeof value === "number" ? value : undefined, locale), t("projects.milestoneProgressChart")]} />
                        <Bar dataKey="progress" name={t("projects.milestoneProgressChart")} fill="hsl(var(--chart-1))" radius={[0, 4, 4, 0]} maxBarSize={18}>
                          <LabelList dataKey="progress" position="right" formatter={(value: unknown) => percent(typeof value === "number" ? value : undefined, locale)} style={{ fontSize: 9, fill: "hsl(var(--foreground))" }} />
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div> : <p className="py-2 text-xs text-muted-foreground">{t("projects.milestoneProgressUnavailable")}</p>}
                </section>
                <div className="grid sm:grid-cols-2 sm:gap-x-6">
                  {milestones.map((milestone) => {
                      const state = getMilestoneState(milestone, overview.statusDate);
                      const stateLabel = state === "completed" ? t("projects.milestoneCompleted") : state === "inProgress" ? t("projects.milestoneInProgress") : state === "overdue" ? t("projects.milestoneOverdue") : t("projects.milestoneUpcoming");
                      const dotClass = state === "completed" ? "bg-success ring-success/20" : state === "inProgress" ? "bg-warning ring-warning/20" : state === "overdue" ? "bg-destructive ring-destructive/20" : "bg-info ring-info/20";
                      return (
                        <Link key={milestone.id} to={`/projects/${projectId}/plan`} className="group flex items-start gap-3 border-b border-border/40 px-1.5 py-2.5 transition-colors hover:bg-muted/20">
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
              </>
            ) : <EmptyPanel title={t("projects.noMilestonesYet")} description={t("projects.milestoneTimelineDescription")} action={<Button size="sm" variant="outline" onClick={() => navigate(`/projects/${projectId}/plan`)}>{t("projects.plan")}</Button>} />}
          </CardContent>
        </Card>
      </div>

      <div data-testid="project-overview-secondary-panels" className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Card className={cn(PREMIUM_CARD, "flex h-full flex-col")}>
          <CardHeader className={PREMIUM_CARD_HEADER}>
            <SectionTitle icon={FilePen} accent="bg-info/10 text-info" title={t("projects.changeRequests")} description={t("projects.openChangeRequests")} action={<Link to={`/projects/${projectId}/change-tracker`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className={cn(PREMIUM_CARD_CONTENT, "flex flex-1 items-center")}>
            {overview.pendingChangeRequestCount === null
              ? <EmptyPanel title={t("projects.overviewDataUnavailable")} description={t("projects.openChangeRequests")} className="w-full" />
              : <div className="w-full">
                <div className="mb-1 flex items-center justify-between gap-2">
                  <span className="flex min-w-0 items-center gap-2 text-[11px] text-muted-foreground"><FilePen className="h-4 w-4 shrink-0 text-info" aria-hidden="true" />{overview.pendingChangeRequestCount > 0 ? t("projects.openChangeRequests") : t("projects.noChangeRequestsYet")}</span>
                  <span className="text-2xl font-semibold leading-none tabular-nums text-info">{englishNumber(overview.pendingChangeRequestCount, locale)}</span>
                </div>
                <div className="h-20">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={[{ name: t("projects.openChangeRequests"), value: overview.pendingChangeRequestCount }]} layout="vertical" margin={{ top: 1, right: locale === "ar" ? 4 : 16, left: locale === "ar" ? 16 : 4, bottom: 0 }}>
                      <XAxis type="number" allowDecimals={false} hide />
                      <YAxis type="category" dataKey="name" width={142} orientation={locale === "ar" ? "right" : "left"} tickLine={false} axisLine={false} tick={{ fontSize: 9 }} />
                      <Tooltip formatter={(value) => englishNumber(typeof value === "number" ? value : undefined, locale)} />
                      <Bar dataKey="value" fill="hsl(var(--chart-3))" radius={[0, 3, 3, 0]} maxBarSize={16}>
                        <LabelList dataKey="value" position="right" style={{ fontSize: 9, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>}
          </CardContent>
        </Card>

        <Card className={cn(PREMIUM_CARD, "flex h-full flex-col")}>
          <CardHeader className={PREMIUM_CARD_HEADER}>
            <SectionTitle icon={FileText} title={t("projects.governanceReporting")} action={<Link to={`/projects/${projectId}/status-reports`} className="inline-flex h-7 items-center px-2 text-xs font-medium text-primary hover:underline">{t("common.viewAll")}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Link>} />
          </CardHeader>
          <CardContent className={cn(PREMIUM_CARD_CONTENT, "flex flex-1 flex-col justify-between")}>
            <div className="space-y-3">
              <section className="border-b border-border/40 pb-2.5">
                <div className="mb-1 flex items-center justify-between gap-2 text-[10px] text-muted-foreground">
                  <span className="inline-flex items-center gap-1.5"><FileText className="h-3.5 w-3.5 text-primary" aria-hidden="true" />{t("projects.statusReports")}</span>
                  <span className="font-semibold tabular-nums">{publishedRatio === null ? "—" : `${englishNumber(publishedRatio, locale)}%`}</span>
                </div>
                {overview.statusReports.totalCount > 0 && unpublishedReportCount !== null ? <div className="h-36" role="img" aria-label={t("projects.statusReports")}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie data={[{ name: t("projects.publishedReports"), value: overview.statusReports.publishedCount }, { name: t("projects.notPublishedReports"), value: unpublishedReportCount }]} dataKey="value" nameKey="name" cx="50%" cy="43%" innerRadius={30} outerRadius={52} paddingAngle={2}>
                        <Cell fill="hsl(var(--chart-2))" />
                        <Cell fill="hsl(var(--muted-foreground))" />
                      </Pie>
                      <Tooltip formatter={(value) => englishNumber(typeof value === "number" ? value : undefined, locale)} />
                      <Legend wrapperStyle={{ fontSize: 9, direction: locale === "ar" ? "rtl" : "ltr" }} />
                    </PieChart>
                  </ResponsiveContainer>
                </div> : <p className="py-2 text-[10px] text-muted-foreground">{unpublishedReportCount === null ? t("projects.overviewDataUnavailable") : t("projects.noStatusReports")}</p>}
              </section>
              <section>
                <div className="mb-1 flex items-center gap-1.5 text-[10px] text-muted-foreground"><CalendarDays className="h-3.5 w-3.5 text-info" aria-hidden="true" />{t("common.meetings")}</div>
                {overview.meetings.meetingCount > 0 && meetingsWithoutFinalMinutes !== null ? <div className="h-36" role="img" aria-label={t("common.meetings")}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie data={[{ name: t("projects.finalMinutes"), value: overview.meetings.finalMinutesCount }, { name: t("projects.meetingsWithoutFinalMinutes"), value: meetingsWithoutFinalMinutes }]} dataKey="value" nameKey="name" cx="50%" cy="43%" innerRadius={30} outerRadius={52} paddingAngle={2}>
                        <Cell fill="hsl(var(--chart-1))" />
                        <Cell fill="hsl(var(--muted-foreground))" />
                      </Pie>
                      <Tooltip formatter={(value) => englishNumber(typeof value === "number" ? value : undefined, locale)} />
                      <Legend wrapperStyle={{ fontSize: 9, direction: locale === "ar" ? "rtl" : "ltr" }} />
                    </PieChart>
                  </ResponsiveContainer>
                </div> : <p className="py-2 text-[10px] text-muted-foreground">{meetingsWithoutFinalMinutes === null ? t("projects.overviewDataUnavailable") : t("projects.noMeetingsRecorded")}</p>}
              </section>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
