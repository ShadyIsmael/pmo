"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import {
  ArrowRight,
  ArrowRightLeft,
  ArrowUpRight,
  CalendarDays,
  CheckCircle2,
  CircleDollarSign,
  ClipboardList,
  Edit,
  FilePen,
  GanttChartSquare,
  MoreHorizontal,
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
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import type { ProjectDto } from "@/hooks/projects/useProjects";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import type { ProjectOverviewDto } from "@/lib/api/projects";
import { RiskLevelHelper, RiskStatusHelper } from "@/lib/enums";
import { getStatusTone } from "@/lib/status-presentation";
import {
  formatCurrency,
  formatDate,
  formatNullablePercentage,
  getRagVariant,
  isFiniteNumber,
} from "@/lib/utils";

type ProjectOverviewProps = {
  project: ProjectDto;
  overview?: ProjectOverviewDto;
  planMilestones?: ProjectOverviewDto["milestones"];
  overviewLoading: boolean;
  overviewError: boolean;
  getTabHref: (_tab: string) => string;
  canEditProject: boolean;
  canManageChangeRequests: boolean;
  canManageHandovers: boolean;
  onEdit: () => void;
  onChangeRequest: () => void;
  onHandover: () => void;
  onOpenPlan: () => void;
};

function displayText(value?: string | null) {
  return value?.trim() || "—";
}

function displayDate(value?: string | null, locale = "en") {
  return value ? formatDate(value, locale === "ar" ? "ar-EG-u-nu-latn" : "en-GB") : "—";
}

function nullableCurrency(value: number | null | undefined, currency: string, locale: string) {
  return isFiniteNumber(value) ? formatCurrency(value, currency || "GBP", locale === "ar" ? "ar-EG-u-nu-latn" : "en-GB") : "—";
}

function englishNumber(value: number | null | undefined, locale: string, fractionDigits = 0) {
  return isFiniteNumber(value)
    ? new Intl.NumberFormat(locale === "ar" ? "ar-EG-u-nu-latn" : locale, { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits }).format(value)
    : "—";
}

function percent(value: number | null | undefined, locale: string) {
  return isFiniteNumber(value) ? `${englishNumber(value, locale)}%` : "—";
}

type MilestoneState = "completed" | "inProgress" | "overdue" | "upcoming";

function getMilestoneState(milestone: ProjectOverviewDto["milestones"][number]): MilestoneState {
  const normalizedStatus = (milestone.status ?? "").replace(/[\s_-]+/g, "").toLowerCase();
  const isComplete = Boolean(milestone.completedDate)
    || (isFiniteNumber(milestone.progress) && milestone.progress >= 100)
    || ["complete", "completed", "closed", "done"].includes(normalizedStatus);
  if (isComplete) return "completed";

  const isOverdue = ["delayed", "late", "overdue"].includes(normalizedStatus)
    || Boolean(milestone.dueDate && new Date(milestone.dueDate).getTime() < Date.now());
  if (isOverdue) return "overdue";

  const isInProgress = (isFiniteNumber(milestone.progress) && milestone.progress > 0)
    || ["active", "inprogress", "ongoing", "started"].includes(normalizedStatus);
  return isInProgress ? "inProgress" : "upcoming";
}

function SectionTitle({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
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

function Kpi({ label, value, detail, icon: Icon, tone = "text-foreground" }: { label: string; value: string; detail?: string; icon: LucideIcon; tone?: string }) {
  return (
    <div className="min-w-0 rounded-lg border bg-card px-3 py-2.5 shadow-none">
      <div className="flex items-center justify-between gap-2 text-[10px] font-medium text-muted-foreground">
        <span className="truncate">{label}</span>
        <Icon className="h-3.5 w-3.5 shrink-0" />
      </div>
      <p className={`mt-1 truncate text-base font-semibold tracking-tight ${tone}`}>{value}</p>
      {detail ? <p className="mt-0.5 truncate text-[10px] text-muted-foreground">{detail}</p> : null}
    </div>
  );
}

function DetailItem({ label, children, className = "" }: { label: string; children: ReactNode; className?: string }) {
  return (
    <div className={`min-w-0 rounded-lg border bg-muted/10 px-3 py-2.5 ${className}`}>
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
    <div className={`min-w-0 rounded-lg border bg-card shadow-none ${large ? "px-4 py-3" : "px-3 py-2.5"}`}>
      <div className={`flex items-center justify-between gap-2 font-medium text-muted-foreground ${large ? "text-xs" : "text-[11px]"}`}>
        <span className="truncate">{label}</span>
        <CheckCircle2 className={`shrink-0 ${large ? "h-4 w-4" : "h-3.5 w-3.5"}`} />
      </div>
      <div className={`relative mt-1 ${large ? "h-[148px]" : "h-[92px]"}`} role="img" aria-label={`${label}: ${displayValue}`}>
        <svg viewBox="0 0 100 62" className="h-full w-full overflow-visible" aria-hidden="true">
          <path d="M 8 52 A 42 42 0 0 1 92 52" fill="none" stroke="currentColor" strokeWidth="9" strokeLinecap="round" className="text-muted-foreground/20" />
          <path d="M 8 52 A 42 42 0 0 1 92 52" fill="none" stroke="currentColor" strokeWidth="9" strokeLinecap="round" pathLength={100} strokeDasharray={`${normalizedValue ?? 0} 100`} className={progressColorClass} />
          {normalizedValue !== null ? <circle cx={markerX} cy={markerY} r="4.8" className={`${progressColorClass} fill-current stroke-card`} strokeWidth="2.5" /> : null}
        </svg>
        <div className="absolute inset-x-0 bottom-0 flex flex-col items-center text-center">
          <div className="flex items-baseline justify-center gap-1.5">
            <p className={`font-semibold leading-none tracking-tight ${progressColorClass} ${large ? "text-4xl" : "text-2xl"}`}>{displayValue}</p>
            {comparisonValue ? <span className={`rounded-full bg-muted px-1.5 py-0.5 font-semibold leading-none ${progressColorClass} ${large ? "text-xs" : "text-[10px]"}`}>{comparisonValue}</span> : null}
          </div>
          {comparisonValue ? <p className={`mt-1 font-medium text-muted-foreground ${large ? "text-[11px]" : "text-[9px]"}`}>{comparisonLabel}</p> : null}
        </div>
      </div>
    </div>
  );
}

function EmptyPanel({ title, description, action, className = "" }: { title: string; description: string; action?: ReactNode; className?: string }) {
  return (
    <div className={`flex min-h-28 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-5 py-5 text-center ${className}`}>
      <p className="text-sm font-medium">{title}</p>
      <p className="mt-1 max-w-sm text-xs text-muted-foreground">{description}</p>
      {action ? <div className="mt-2.5">{action}</div> : null}
    </div>
  );
}

function LinkAction({ href, children }: { href: string; children: ReactNode }) {
  return <Link href={href} scroll={false}><Button size="sm" variant="ghost" className="h-7 px-2 text-xs">{children}<ArrowUpRight className="ms-1 h-3.5 w-3.5" /></Button></Link>;
}

export function ProjectOverview({
  project,
  overview,
  planMilestones,
  overviewLoading,
  overviewError,
  getTabHref,
  canEditProject,
  canManageChangeRequests,
  canManageHandovers,
  onEdit,
  onChangeRequest,
  onHandover,
  onOpenPlan,
}: ProjectOverviewProps) {
  const locale = useLocale();
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const enums = useTranslations("enums");
  const { statusLabel, healthStatusLabel, priorityLabel } = useProjectEnumLabels();
  const latestReport = overview?.statusReports.latestPublishedReport;
  const projectStatus = latestReport?.projectLifecycleStatusAtReport || project.status;
  const projectHealth = latestReport?.overallHealth || project.health || project.ragStatus;
  const progress = overview?.planProgress ?? project.progress;
  const progressDelta = isFiniteNumber(progress) && isFiniteNumber(latestReport?.overallProgressPercent) ? progress - latestReport.overallProgressPercent : null;
  const projectHealthTone = getStatusTone(projectHealth);
  const progressTone = projectHealthTone === "red" ? "red" : projectHealthTone === "amber" ? "amber" : "green";
  const highCriticalRisks = overview?.risks.highCriticalOpenCount ?? null;
  const overdueIssues = overview?.issues.overdueOpenIssueCount ?? null;
  const criticalAttention = highCriticalRisks !== null && overdueIssues !== null ? highCriticalRisks + overdueIssues : null;
  const milestones = planMilestones ?? overview?.milestones ?? [];
  const nextMilestone = milestones.find((m) => getMilestoneState(m) !== "completed");
  const evmCurrency = overview?.currency ?? project.currency;
  const pv = overview?.plannedValue ?? project.plannedValue;
  const ev = overview?.earnedValue ?? project.earnedValue;
  const ac = overview?.hasCostEntryData ? overview.actualCost : (overview?.actualCost ?? project.actualCost);
  const approvedBudget = overview?.hasBudgetData ? overview.approvedBudget : (overview?.approvedBudget ?? project.budget);
  const committedCost = overview?.committedCost;
  const forecastAtCompletion = overview?.forecastAtCompletion;
  const remainingBudget = overview?.remainingBudget;
  const budgetUtilization = overview?.budgetUtilizationPercentage;
  const sv = overview?.scheduleVariance ?? null;
  const cv = overview?.costVariance ?? null;
  const spi = overview?.schedulePerformanceIndex ?? null;
  const cpi = overview?.costPerformanceIndex ?? null;
  const chartData = [{
    label: t("projectPerformance"),
    plannedValue: pv,
    earnedValue: ev,
    actualCost: ac,
  }];
  const hasEvmData = [pv, ev, ac].some(isFiniteNumber);
  const riskLevelLabel = (value: string) => {
    const key = RiskLevelHelper.getLabelKey(value);
    return key ? enums(key) : "—";
  };
  const riskStatusLabel = (value: string) => {
    const key = RiskStatusHelper.getLabelKey(value);
    return key ? enums(key) : "—";
  };

  return (
    <div className="space-y-3 pt-1">
      <section aria-labelledby="project-overview-heading" className="overflow-hidden rounded-2xl border bg-card shadow-sm">
        <div className="border-b bg-gradient-to-br from-muted/40 via-card to-card px-4 py-4 sm:px-5">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
            <div className="min-w-0">
              <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{t("details")}</p>
              <div className="mt-1.5 flex flex-wrap items-center gap-2">
                <h1 id="project-overview-heading" className="max-w-full truncate text-xl font-semibold tracking-tight sm:text-2xl">{project.name}</h1>
                <Badge status={projectStatus} className="text-[10px]">{statusLabel(projectStatus)}</Badge>
                <Badge variant={getRagVariant(projectHealth)} className="text-[10px]"><span className="me-1.5 h-1.5 w-1.5 rounded-full bg-current" />{healthStatusLabel(projectHealth)}</Badge>
                <Badge status={project.priority} className="text-[10px]">{priorityLabel(project.priority)}</Badge>
              </div>
              <p className="mt-1.5 text-xs text-muted-foreground"><span className="font-semibold text-foreground">{project.code}</span>{project.phase ? ` · ${project.phase}` : ""}</p>
            </div>
            <div className="flex shrink-0 flex-wrap items-center gap-2 xl:justify-end">
              {canEditProject ? <Button size="sm" variant="outline" onClick={onEdit}><Edit className="me-1.5 h-3.5 w-3.5" />{t("edit")}</Button> : null}
              {canManageChangeRequests ? <Button size="sm" variant="outline" onClick={onChangeRequest}><FilePen className="me-1.5 h-3.5 w-3.5" />{t("changeRequest")}</Button> : null}
              {canManageHandovers ? <Button size="sm" variant="outline" onClick={onHandover}><ArrowRightLeft className="me-1.5 h-3.5 w-3.5" />{t("handover")}</Button> : null}
              <DropdownMenu>
                <DropdownMenuTrigger asChild><Button size="sm" variant="outline" aria-label={common("actions")}><MoreHorizontal className="h-4 w-4" /></Button></DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onSelect={onOpenPlan}><GanttChartSquare className="me-2 h-4 w-4" />{t("plan")}</DropdownMenuItem>
                  <DropdownMenuItem asChild><Link href={getTabHref("status-reports")}><ClipboardList className="me-2 h-4 w-4" />{t("statusReports")}</Link></DropdownMenuItem>
                  <DropdownMenuSeparator />
                  {canManageHandovers ? <DropdownMenuItem onSelect={onHandover}><ArrowRightLeft className="me-2 h-4 w-4" />{t("handover")}</DropdownMenuItem> : null}
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </div>

        <div className="grid lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
          <div className="space-y-3 p-4 sm:p-5">
            <div>
              <h2 className="text-sm font-semibold tracking-tight">{t("projectInformation")}</h2>
              <p className="mt-0.5 text-xs text-muted-foreground">{t("projectInformationDescription")}</p>
            </div>
            <div className="grid gap-2 sm:grid-cols-2">
              <div className="rounded-xl border bg-muted/15 p-3">
                <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("description")}</p>
                <p className="mt-1 line-clamp-4 text-sm leading-6">{displayText(project.description)}</p>
              </div>
              <div className="rounded-xl border bg-muted/15 p-3">
                <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("objectiveBusinessCase")}</p>
                <p className="mt-1 line-clamp-4 text-sm leading-6">{displayText(project.businessCase)}</p>
              </div>
            </div>
            <div className="rounded-xl border bg-muted/15 p-3">
              <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("notes")}</p>
              <p className="mt-1 line-clamp-3 text-sm leading-6">{displayText(project.notes)}</p>
            </div>
          </div>

          <div className="border-t p-4 sm:p-5 lg:border-s lg:border-t-0">
            <div className="mb-2">
              <h2 className="text-sm font-semibold tracking-tight">{t("projectDetails")}</h2>
              <p className="mt-0.5 text-xs text-muted-foreground">{t("projectInformationDescription")}</p>
            </div>
            <dl className="grid gap-2 sm:grid-cols-2">
              <DetailItem label={t("projectManager")}>{displayText(project.projectManagerName)}</DetailItem>
              <DetailItem label={t("sponsor")}>{displayText(project.sponsorName)}</DetailItem>
              <DetailItem label={t("portfolio")}>{displayText(project.portfolioName)}</DetailItem>
              <DetailItem label={t("program")}>{overview?.program ? `${overview.program.name}${overview.program.code ? ` · ${overview.program.code}` : ""}` : "—"}</DetailItem>
              <DetailItem label={`${t("startDate")} / ${t("endDate")}`}>{displayDate(project.startDate, locale)} <span className="text-muted-foreground">→</span> {displayDate(project.endDate, locale)}</DetailItem>
              <DetailItem label={t("updatedAt")}>{displayDate(project.updatedAt || project.createdAt, locale)}</DetailItem>
              <DetailItem label={t("publishedAt")}>{latestReport ? displayDate(latestReport.publishedAt, locale) : "—"}</DetailItem>
              <DetailItem label={t("department")}>{displayText(project.departmentName)}</DetailItem>
              <DetailItem label={t("currency")}>{displayText(project.currency)}</DetailItem>
              <DetailItem label={t("riskRating")}>{displayText(project.riskRating)}</DetailItem>
              <DetailItem label={t("confidenceLevel")}><Badge variant={getRagVariant(project.confidenceLevel)} className="text-[10px]">{project.confidenceLevel || "—"}</Badge></DetailItem>
              <DetailItem label={t("baseline")} className="sm:col-span-2">{overview?.currentBaseline ? `${overview.currentBaseline.name} · v${englishNumber(overview.currentBaseline.version, locale)}` : "—"}</DetailItem>
            </dl>
          </div>
        </div>
      </section>

      <section aria-labelledby="project-kpi-heading">
        <div className="mb-2 flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between"><h2 id="project-kpi-heading" className="text-sm font-semibold">{t("kpiSummary")}</h2><span className="text-[11px] text-muted-foreground">{latestReport ? `${t("publishedAt")}: ${displayDate(latestReport.publishedAt, locale)}` : t("noStatusReports")}</span></div>
        <div className="grid items-stretch gap-3 lg:grid-cols-[minmax(240px,0.9fr)_minmax(0,2fr)]">
          <div className="min-w-0">
            <ProgressGauge label={t("overallProgressLabel")} value={progress} comparison={progressDelta} comparisonLabel={t("progressSinceLatestPublished")} tone={progressTone} large locale={locale} />
          </div>
          <div className="grid min-w-0 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Kpi label={t("scheduleVariance")} value={formatNullablePercentage(sv, 1, true)} detail={isFiniteNumber(sv) ? (sv < 0 ? t("behindSchedule") : sv > 0 ? t("aheadOfSchedule") : t("onSchedule")) : t("scheduleSignalUnavailable")} icon={CalendarDays} tone={isFiniteNumber(sv) && sv < 0 ? "text-status-red" : "text-status-green"} />
            <Kpi label={t("costVariance")} value={formatNullablePercentage(cv, 1, true)} detail={evmCurrency} icon={CircleDollarSign} tone={isFiniteNumber(cv) && cv < 0 ? "text-status-red" : "text-status-green"} />
            <Kpi label={t("schedulePerformanceIndex")} value={isFiniteNumber(spi) ? englishNumber(spi, locale, 2) : "—"} detail={isFiniteNumber(spi) ? (spi! > 1 ? t("aheadOfSchedule") : spi! < 1 ? t("behindSchedule") : t("onSchedule")) : undefined} icon={CalendarDays} tone={isFiniteNumber(spi) && spi! < 1 ? "text-status-red" : "text-status-green"} />
            <Kpi label={t("costPerformanceIndex")} value={isFiniteNumber(cpi) ? englishNumber(cpi, locale, 2) : "—"} detail={isFiniteNumber(cpi) ? (cpi! > 1 ? t("underBudget") : cpi! < 1 ? t("overBudget") : t("onBudget")) : undefined} icon={CircleDollarSign} tone={isFiniteNumber(cpi) && cpi! < 1 ? "text-status-red" : "text-status-green"} />
            <Kpi label={t("criticalAttention")} value={englishNumber(criticalAttention, locale)} detail={criticalAttention === null ? t("overviewDataUnavailable") : `${englishNumber(highCriticalRisks, locale)} ${t("highCriticalRisks")} · ${englishNumber(overdueIssues, locale)} ${t("overdueOpenIssues")}`} icon={ShieldAlert} tone={criticalAttention ? "text-status-red" : "text-status-green"} />
            <Kpi label={t("teamSignal")} value={englishNumber(overview?.allocations.activeResourceCount ?? project.members.length, locale)} detail={overview ? `${englishNumber(overview.allocations.allocatedHoursPerWeek, locale, 1)} ${t("hoursPerWeek")}` : t("overviewDataUnavailable")} icon={Users} />
            <div className="min-w-0 sm:col-span-2 xl:col-span-2"><Kpi label={t("nextMilestone")} value={nextMilestone?.name || "—"} detail={nextMilestone ? displayDate(nextMilestone.dueDate, locale) : t("noMilestonesYet")} icon={CalendarDays} /></div>
          </div>
        </div>
      </section>

      {overviewLoading ? <div className="grid gap-3 lg:grid-cols-2"><Skeleton className="h-64" /><Skeleton className="h-64" /></div> : overviewError ? <Card><CardContent className="!py-8 text-center text-sm text-destructive">{common("errorLoading")}</CardContent></Card> : (
        <div className="grid items-start gap-3 lg:grid-cols-3">
          <Card className="shadow-none lg:col-span-2">
            <CardHeader className="!py-3 !pb-2"><SectionTitle title={t("projectPerformance")} description={t("currentPerformanceDescription")} action={<LinkAction href={getTabHref("financials")}>{common("viewAll")}</LinkAction>} /></CardHeader>
            <CardContent className="!py-3 !pt-1">
              {!hasEvmData ? <EmptyPanel className="h-44" title={t("evmUnavailable")} description={t("evmUnavailableDescription")} /> : <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(270px,0.75fr)] xl:items-center">
                <div className="h-56 min-w-0" aria-label={t("projectPerformance")}>
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={chartData} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} className="stroke-border" />
                      <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fontSize: 11 }} />
                      <YAxis tickLine={false} axisLine={false} tick={{ fontSize: 10 }} tickFormatter={(value) => englishNumber(typeof value === "number" ? value : Number(value), locale)} width={48} />
                      <Tooltip formatter={(value, name) => [nullableCurrency(typeof value === "number" ? value : undefined, evmCurrency, locale), name]} />
                      <Legend wrapperStyle={{ fontSize: 11, paddingTop: 4 }} />
                      <Bar dataKey="plannedValue" name={t("plannedValue")} fill="hsl(var(--chart-1))" radius={[4, 4, 0, 0]} maxBarSize={42}>
                        <LabelList dataKey="plannedValue" position="top" formatter={(value: number | string | undefined) => isFiniteNumber(value) ? nullableCurrency(value, evmCurrency, locale) : "—"} style={{ fontSize: 10, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                      <Bar dataKey="earnedValue" name={t("earnedValue")} fill="hsl(var(--chart-2))" radius={[4, 4, 0, 0]} maxBarSize={42}>
                        <LabelList dataKey="earnedValue" position="top" formatter={(value: number | string | undefined) => isFiniteNumber(value) ? nullableCurrency(value, evmCurrency, locale) : "—"} style={{ fontSize: 10, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                      <Bar dataKey="actualCost" name={t("actualCost")} fill="hsl(var(--chart-3))" radius={[4, 4, 0, 0]} maxBarSize={42}>
                        <LabelList dataKey="actualCost" position="top" formatter={(value: number | string | undefined) => isFiniteNumber(value) ? nullableCurrency(value, evmCurrency, locale) : "—"} style={{ fontSize: 10, fill: "hsl(var(--foreground))" }} />
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>
                <dl className="grid gap-x-4 gap-y-2 rounded-lg border bg-muted/20 p-3 sm:grid-cols-2">
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("approvedBudget")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(approvedBudget, evmCurrency, locale)}</dd></div>
                  {overview?.originalApprovedBudget && overview.originalApprovedBudget !== approvedBudget && (
                    <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("budgetRevisions.originalApprovedBudget")}</dt><dd className="mt-1 text-xs text-muted-foreground">{nullableCurrency(overview.originalApprovedBudget, evmCurrency, locale)}</dd></div>
                  )}
                  {overview?.hasPendingRevision && (
                    <div className="rounded-md bg-amber-50 p-2 text-xs text-amber-700 dark:bg-amber-950 dark:text-amber-300">{t("budgetRevisions.pendingRevision")}</div>
                  )}
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("plannedValue")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(pv, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("earnedValue")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(ev, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("actualCost")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(ac, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("committedCost")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(committedCost, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("forecast")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(forecastAtCompletion, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("remainingBudget")}</dt><dd className="mt-1 text-sm font-semibold">{nullableCurrency(remainingBudget, evmCurrency, locale)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("budgetUtilization")}</dt><dd className="mt-1 text-sm font-semibold">{isFiniteNumber(budgetUtilization) ? `${englishNumber(budgetUtilization, locale, 1)}%` : "—"}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("scheduleVariance")}</dt><dd className="mt-1 text-sm font-semibold">{formatNullablePercentage(sv, 1, true)}</dd></div>
                  <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("costVariance")}</dt><dd className="mt-1 text-sm font-semibold">{formatNullablePercentage(cv, 1, true)}</dd></div>
<div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("schedulePerformanceIndex")}</dt><dd className="mt-1 text-sm font-semibold">{isFiniteNumber(spi) ? new Intl.NumberFormat(locale, {minimumFractionDigits: 2, maximumFractionDigits: 2}).format(spi!) : "—"}</dd></div>
                   <div><dt className="text-[10px] uppercase tracking-wide text-muted-foreground">{t("costPerformanceIndex")}</dt><dd className="mt-1 text-sm font-semibold">{isFiniteNumber(cpi) ? new Intl.NumberFormat(locale, {minimumFractionDigits: 2, maximumFractionDigits: 2}).format(cpi!) : "—"}</dd></div>
                </dl>
              </div>}
              <p className="mt-3 text-[10px] text-muted-foreground">{t("currentSnapshotNoHistory")}</p>
            </CardContent>
          </Card>

          <Card className="shadow-none lg:col-span-1">
            <CardHeader className="!py-3 !pb-2"><SectionTitle title={t("latestPublishedStatus")} description={t("publishedReportOnlyDescription")} action={<LinkAction href={getTabHref("status-reports")}>{common("viewAll")}</LinkAction>} /></CardHeader>
            <CardContent className="!py-3 !pt-1">
              {latestReport ? <div className="space-y-3">
                <div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">{displayDate(latestReport.periodFrom, locale)} – {displayDate(latestReport.periodTo, locale)}</p><p className="mt-1 text-xs text-muted-foreground">{t("publishedAt")}: {displayDate(latestReport.publishedAt, locale)}</p></div><Badge variant={getRagVariant(latestReport.overallHealth)} className="text-[10px]">{healthStatusLabel(latestReport.overallHealth)}</Badge></div>
                <div><div className="flex items-center justify-between text-xs"><span className="text-muted-foreground">{t("overallProgress")}</span><span className="font-semibold">{percent(latestReport.overallProgressPercent, locale)}</span></div><Progress value={latestReport.overallProgressPercent ?? 0} className="mt-1.5 h-1.5" /></div>
                <div className="rounded-lg border bg-muted/20 p-3"><p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("executiveSummary")}</p><p className="mt-1 line-clamp-5 text-sm leading-6">{displayText(latestReport.executiveSummary)}</p></div>
                <Link href={getTabHref("status-reports")} scroll={false}><Button size="sm" variant="outline" className="w-full">{t("statusReportDetails")}<ArrowRight className="ms-1.5 h-3.5 w-3.5" /></Button></Link>
              </div> : <EmptyPanel title={t("noStatusReports")} description={t("noStatusReportsDescription")} action={<Link href={getTabHref("status-reports")} scroll={false}><Button size="sm" variant="outline">{t("statusReports")}</Button></Link>} />}
            </CardContent>
          </Card>
        </div>
      )}

      <div className="grid items-start gap-3 lg:grid-cols-3">
      <Card className="shadow-none lg:col-span-2">
        <CardHeader className="!py-3 !pb-2"><SectionTitle title={t("milestones")} description={t("milestoneTimelineDescription")} action={<LinkAction href={getTabHref("plan")}>{common("viewAll")}</LinkAction>} /></CardHeader>
        <CardContent className="!py-3 !pt-1">
          {milestones.length ? <>
            <div className="mb-3 flex flex-wrap gap-x-4 gap-y-2 border-b pb-3" aria-label={t("milestoneStatusLegend")}>
              <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-success" />{t("milestoneCompleted")}</span>
              <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-warning" />{t("milestoneInProgress")}</span>
              <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-destructive" />{t("milestoneOverdue")}</span>
              <span className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground"><span className="h-2 w-2 rounded-full bg-info" />{t("milestoneUpcoming")}</span>
            </div>
            <div className="relative">
              <div className="absolute start-[15px] top-3 bottom-3 w-px bg-border" aria-hidden="true" />
              <div className="space-y-1">
                {milestones.map((milestone) => {
                  const state = getMilestoneState(milestone);
                  const statusLabel = state === "completed" ? t("milestoneCompleted") : state === "inProgress" ? t("milestoneInProgress") : state === "overdue" ? t("milestoneOverdue") : t("milestoneUpcoming");
                  const statusVariant = state === "completed" ? "status-green" : state === "inProgress" ? "status-amber" : state === "overdue" ? "status-red" : "status-blue";
                  const dotClass = state === "completed" ? "bg-success ring-success/20" : state === "inProgress" ? "bg-warning ring-warning/20" : state === "overdue" ? "bg-destructive ring-destructive/20" : "bg-info ring-info/20";
                  return <Link key={milestone.id} href={getTabHref("plan")} scroll={false} className="group relative z-10 flex items-start gap-3 rounded-lg px-1.5 py-2.5 transition-colors hover:bg-muted/30">
                    <span className={`mt-1 flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-4 border-card ring-2 ${dotClass}`} aria-hidden="true" />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <p className="min-w-0 flex-1 text-sm font-semibold leading-5">{milestone.name}</p>
                        <Badge variant={statusVariant} className="shrink-0 text-[9px]">{statusLabel}</Badge>
                      </div>
                      <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[10px] text-muted-foreground">
                        <span>{displayDate(milestone.dueDate, locale)}</span>
                        {isFiniteNumber(milestone.progress) ? <><span aria-hidden="true">·</span><span>{percent(milestone.progress, locale)}</span></> : null}
                      </div>
                    </div>
                  </Link>;
                })}
              </div>
            </div>
          </> : <EmptyPanel title={t("noMilestonesYet")} description={t("milestoneTimelineDescription")} action={<Button size="sm" variant="outline" onClick={onOpenPlan}>{t("plan")}</Button>} />}
        </CardContent>
      </Card>

        <Card className="shadow-none lg:col-span-1">
          <CardHeader className="!py-3 !pb-2"><SectionTitle title={t("attentionRequired")} description={t("attentionRequiredDescription")} action={<LinkAction href={getTabHref("risks-issues")}>{common("viewAll")}</LinkAction>} /></CardHeader>
          <CardContent className="!py-3 !pt-1"><div className="grid gap-3">
            <div><div className="flex items-center justify-between"><h3 className="text-xs font-semibold">{t("topRisks")}</h3><span className="text-xs font-semibold">{englishNumber(overview?.risks.openCount, locale)}</span></div><div className="mt-2 divide-y rounded-lg border">{overview?.risks.topRisks.length ? overview.risks.topRisks.map((risk) => <Link key={risk.id} href={getTabHref("risks-issues")} scroll={false} className="flex items-center justify-between gap-3 px-3 py-2.5 hover:bg-muted/30"><span className="min-w-0 truncate text-xs font-medium">{risk.title}</span><span className="shrink-0 text-[10px] font-semibold" title={riskStatusLabel(risk.status)}>{riskLevelLabel(risk.riskLevel)}</span></Link>) : <p className="px-3 py-4 text-xs text-muted-foreground">{t("noRisksYet")}</p>}</div></div>
            <div><div className="flex items-center justify-between"><h3 className="text-xs font-semibold">{t("issues")}</h3><span className="text-xs font-semibold">{englishNumber(overview?.issues.openIssueCount, locale)}</span></div><div className="mt-2 divide-y rounded-lg border">{overview?.issues.topIssues.length ? overview.issues.topIssues.map((issue) => <Link key={issue.id} href={`/${locale}/work-items/${encodeURIComponent(issue.id)}`} className="flex items-center justify-between gap-3 px-3 py-2.5 hover:bg-muted/30"><span className="min-w-0 truncate text-xs font-medium">{issue.title}</span><span className="shrink-0 text-[10px] text-muted-foreground">{issue.dueDate ? displayDate(issue.dueDate, locale) : "—"}</span></Link>) : <p className="px-3 py-4 text-xs text-muted-foreground">{t("noIssuesYet")}</p>}</div></div>
          </div><div className="mt-3 grid gap-2 sm:grid-cols-2"><Link href={getTabHref("change-tracker")} scroll={false} className="rounded-lg border p-3 hover:bg-muted/30"><div className="flex items-center justify-between text-xs font-medium"><span>{t("changeRequests")}</span><FilePen className="h-3.5 w-3.5 text-muted-foreground" /></div><p className="mt-1.5 text-lg font-semibold">{englishNumber(overview?.pendingChangeRequestCount, locale)}</p><p className="text-[10px] text-muted-foreground">{t("openChangeRequests")}</p></Link><Link href={getTabHref("handover")} scroll={false} className="rounded-lg border p-3 hover:bg-muted/30"><div className="flex items-center justify-between text-xs font-medium"><span>{t("handover")}</span><ArrowRightLeft className="h-3.5 w-3.5 text-muted-foreground" /></div><p className="mt-1.5 text-lg font-semibold">{englishNumber(overview?.pendingHandoverCount, locale)}</p><p className="text-[10px] text-muted-foreground">{t("pendingHandovers")}</p></Link></div></CardContent>
        </Card>

      </div>

      <div className="grid gap-3 lg:grid-cols-2">
        <Card className="shadow-none"><CardHeader className="!py-3 !pb-2"><SectionTitle title={t("resourceAllocationSummary")} action={<LinkAction href={getTabHref("team")}>{common("viewAll")}</LinkAction>} /></CardHeader><CardContent className="!py-3 !pt-1"><div className="grid grid-cols-3 gap-2 text-center"><div className="rounded-lg bg-muted/30 p-3"><p className="text-lg font-semibold">{englishNumber(overview?.allocations.activeResourceCount ?? project.members.length, locale)}</p><p className="mt-1 text-[10px] text-muted-foreground">{t("teamResources")}</p></div><div className="rounded-lg bg-muted/30 p-3"><p className="text-lg font-semibold">{englishNumber(overview?.allocations.activeAllocationCount, locale)}</p><p className="mt-1 text-[10px] text-muted-foreground">{common("allocation")}</p></div><div className="rounded-lg bg-muted/30 p-3"><p className="text-lg font-semibold">{englishNumber(overview?.allocations.allocatedHoursPerWeek, locale, 1)}</p><p className="mt-1 text-[10px] text-muted-foreground">{t("hoursPerWeek")}</p></div></div><p className="mt-2 text-[10px] text-muted-foreground">{t("utilizationUnavailable")}</p></CardContent></Card>
        <Card className="shadow-none"><CardHeader className="!py-3 !pb-2"><SectionTitle title={t("governanceReporting")} action={<LinkAction href={getTabHref("status-reports")}>{common("viewAll")}</LinkAction>} /></CardHeader><CardContent className="!py-3 !pt-1"><dl className="grid gap-x-6 gap-y-2 text-xs sm:grid-cols-2"><div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("statusReports")}</dt><dd className="font-semibold">{englishNumber(overview?.statusReports.totalCount, locale)}</dd></div><div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("publishedReports")}</dt><dd className="font-semibold">{englishNumber(overview?.statusReports.publishedCount, locale)}</dd></div><div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{common("meetings")}</dt><dd className="font-semibold">{englishNumber(overview?.meetings.meetingCount, locale)}</dd></div><div className="flex items-center justify-between gap-3"><dt className="text-muted-foreground">{t("finalMinutes")}</dt><dd className="font-semibold">{englishNumber(overview?.meetings.finalMinutesCount, locale)}</dd></div></dl></CardContent></Card>
      </div>

    </div>
  );
}
