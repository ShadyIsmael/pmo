"use client";

import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import * as React from "react";
import { AlertTriangle, CalendarDays, FolderKanban, Gauge, Layers3, ListChecks, ShieldAlert, UsersRound } from "lucide-react";
import {
  usePortfolioManagementOverview,
  type PortfolioFinancialGroup,
  type PortfolioManagementOverview,
  type PortfolioResourcePressure,
} from "@/hooks/portfolios/usePortfolioManagement";
import { PageHeader } from "@/components/layout/PageHeader";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { KpiCard } from "@/components/ui/kpi-card";
import { Progress } from "@/components/ui/progress";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TablePagination } from "@/components/ui/table-pagination";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { WidgetShell } from "@/components/dashboard-builder/WidgetShell";
import { useManagementPerspective } from "@/lib/perspective-context";
import { MANAGEMENT_PERSPECTIVES } from "@/lib/perspective";

type WorkspaceTab = "overview" | "programs" | "projects" | "delivery" | "financials" | "risks" | "resources" | "status" | "activity";

const PORTFOLIO_WIDGETS = [
  ["portfolio.health", "portfolioHealth"],
  ["portfolio.program-health", "programHealth"],
  ["portfolio.project-health", "projectHealth"],
  ["portfolio.delivery", "delivery"],
  ["portfolio.schedule-attention", "scheduleAttention"],
  ["portfolio.milestones", "milestones"],
  ["portfolio.financials", "financials"],
  ["portfolio.risks", "risks"],
  ["portfolio.issues", "issues"],
  ["portfolio.resources", "resources"],
  ["portfolio.status-reports", "statusReports"],
  ["portfolio.activity", "meetingsActivity"],
] as const;

export function PortfolioManagementWorkspace({ portfolioId, actions }: { portfolioId: string; actions?: React.ReactNode }) {
  const t = useTranslations("portfolioManager");
  const locale = useLocale();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [tab, setTab] = React.useState<WorkspaceTab>(searchParams.get("tab") === "status" ? "status" : "overview");
  const [programsPage, setProgramsPage] = React.useState(1);
  const [projectsPage, setProjectsPage] = React.useState(1);
  const query = usePortfolioManagementOverview(portfolioId, programsPage, projectsPage);
  const { current, switchPerspective } = useManagementPerspective();

  if (query.isLoading) return <LoadingSkeleton rows={8} />;
  if (query.isError || !query.data) return <ErrorState title={t("loadFailed")} description={t("noData")} onRetry={() => void query.refetch()} />;

  const data = query.data;
  const portfolio = data.portfolio;
  const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString(locale === "ar" ? "ar-EG" : "en-GB") : "—";
  const formatMoney = (value: number, currency: string) => new Intl.NumberFormat(locale === "ar" ? "ar-EG" : "en-GB", { style: "currency", currency, maximumFractionDigits: 0 }).format(value);
  const programHref = (id: string) => `/${locale}/portfolios/programs/${encodeURIComponent(id)}`;
  const projectHref = (id: string) => `/${locale}/projects/${encodeURIComponent(id)}`;
  const viewAsProgram = (id: string) => { switchPerspective(MANAGEMENT_PERSPECTIVES.ProgramManager, id); router.push(programHref(id)); };
  const viewAsProject = (id: string) => { switchPerspective(MANAGEMENT_PERSPECTIVES.ProjectManager, id); router.push(projectHref(id)); };
  const tabs: WorkspaceTab[] = ["overview", "programs", "projects", "delivery", "financials", "risks", "resources", "status", "activity"];

  return (
    <div className="space-y-5">
      <PageHeader title={portfolio.name} description={`${t("portfolioWorkspace")} · ${portfolio.code ?? "—"}`} actions={actions}>
        <div className="flex flex-wrap items-center gap-2">
          <Badge status={portfolio.status}>{portfolio.status ?? t("unknown")}</Badge>
          <Badge status={portfolio.health}>{portfolio.health ?? t("unknown")}</Badge>
          {current?.viewMode === "ReadOnlyViewAs" && <Badge status="ReadOnly">{t("readOnly")}</Badge>}
        </div>
      </PageHeader>
      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-card px-3 py-2 text-xs text-muted-foreground">
        <Layers3 className="h-4 w-4" />
        <span className="font-medium text-foreground">{t("portfolio")}: {portfolio.name}</span>
        <span aria-hidden="true">·</span><span>{t("baseCurrency")}: {portfolio.currency}</span>
        {current?.viewMode === "ReadOnlyViewAs" && <Badge status="ReadOnly">{t("readOnly")}</Badge>}
      </div>
      <Tabs value={tab} onValueChange={(value) => setTab(value as WorkspaceTab)}>
        <TabsList className="flex h-auto flex-wrap justify-start">
          {tabs.map((value) => <TabsTrigger key={value} value={value}>{t(value === "risks" ? "risksIssues" : value === "status" ? "statusReports" : value === "activity" ? "meetingsActivity" : value)}</TabsTrigger>)}
        </TabsList>
        <TabsContent value="overview" className="space-y-5"><OverviewSummary data={data} t={t} formatDate={formatDate} /><AttentionView data={data} t={t} projectHref={projectHref} programHref={programHref} /><PortfolioDashboard data={data} t={t} formatMoney={formatMoney} /></TabsContent>
        <TabsContent value="programs"><ProgramsTable data={data} t={t} programHref={programHref} onViewAs={viewAsProgram} onPageChange={setProgramsPage} /></TabsContent>
        <TabsContent value="projects"><ProjectsTable data={data} t={t} projectHref={projectHref} programHref={programHref} onViewAs={viewAsProject} onPageChange={setProjectsPage} /></TabsContent>
        <TabsContent value="delivery"><DeliveryView data={data} t={t} formatDate={formatDate} projectHref={projectHref} /></TabsContent>
        <TabsContent value="financials"><FinancialView data={data} t={t} formatMoney={formatMoney} projectHref={projectHref} /></TabsContent>
        <TabsContent value="risks"><RisksIssuesView data={data} t={t} projectHref={projectHref} /></TabsContent>
        <TabsContent value="resources"><ResourcesView data={data} t={t} projectHref={projectHref} /></TabsContent>
        <TabsContent value="status"><StatusView data={data} t={t} formatDate={formatDate} projectHref={projectHref} /></TabsContent>
        <TabsContent value="activity"><ActivityView data={data} t={t} formatDate={formatDate} projectHref={projectHref} /></TabsContent>
      </Tabs>
    </div>
  );
}

function OverviewSummary({ data, t, formatDate }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string }) {
  const p = data.portfolio;
  return <>
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
      <KpiCard label={t("totalPrograms")} value={data.summary.totalPrograms} icon={Layers3} semantic="indigo" />
      <KpiCard label={t("totalProjects")} value={data.summary.totalProjects} icon={FolderKanban} semantic="blue" />
      <KpiCard label={t("directProjects")} value={data.summary.directPortfolioProjects} icon={FolderKanban} semantic="violet" />
      <KpiCard label={t("projectsAtRisk")} value={data.summary.atRiskProjects} icon={AlertTriangle} semantic="amber" accent={data.summary.atRiskProjects ? "warning" : undefined} />
      <KpiCard label={t("criticalProjects")} value={data.summary.criticalProjects} icon={ShieldAlert} semantic="red" accent={data.summary.criticalProjects ? "danger" : undefined} />
    </div>
    <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
      <Card><CardHeader><CardTitle className="text-sm">{t("portfolioIdentity")}</CardTitle></CardHeader><CardContent className="grid gap-3 text-sm sm:grid-cols-2"><Info label={t("portfolio")} value={p.name} /><Info label={t("code")} value={p.code} /><Info label={t("owner")} value={p.ownerName} /><Info label={t("baseCurrency")} value={p.currency} /><Info label={t("start")} value={formatDate(p.startDate)} /><Info label={t("end")} value={formatDate(p.endDate)} />{p.strategicObjective && <Info label={t("strategicObjective")} value={p.strategicObjective} />}{p.priority && <Info label={t("priority")} value={p.priority} />}</CardContent></Card>
      <Card><CardHeader><CardTitle className="text-sm">{t("structureSummary")}</CardTitle></CardHeader><CardContent className="space-y-2 text-sm"><Info label={t("programProjects")} value={data.summary.programContainedProjects} /><Info label={t("directProjects")} value={data.summary.directPortfolioProjects} /><Info label={t("activeProjects")} value={data.summary.activeProjects} /><Info label={t("completedProjects")} value={data.summary.completedProjects} /><Info label={t("averageProgress")} value={data.summary.averageProjectProgress == null ? "—" : `${data.summary.averageProjectProgress.toFixed(1)}%`} /><Info label={t("description")} value={p.description} /></CardContent></Card>
    </div>
  </>;
}

function PortfolioDashboard({ data, t, formatMoney }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string }) {
  return <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{PORTFOLIO_WIDGETS.map(([id, titleKey]) => <div key={id} data-widget-id={id} className="min-h-40"><WidgetShell title={t(titleKey)} dataSource={t("dataSource")}><PortfolioWidget id={id} data={data} t={t} formatMoney={formatMoney} /></WidgetShell></div>)}</div>;
}

function PortfolioWidget({ id, data, t, formatMoney }: { id: string; data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string }) {
  if (id === "portfolio.health") return <div className="flex items-center gap-3"><Gauge className="h-8 w-8 text-primary" /><div><p className="text-2xl font-bold">{data.portfolio.health ?? t("unknown")}</p><p className="text-xs text-muted-foreground">{data.portfolio.status ?? t("unknown")}</p></div></div>;
  if (id === "portfolio.program-health") return <Distribution values={data.summary.programHealthDistribution} t={t} />;
  if (id === "portfolio.project-health") return <Distribution values={data.summary.projectHealthDistribution} t={t} />;
  if (id === "portfolio.delivery") return <Distribution values={data.delivery.programDeliveryConditions} t={t} />;
  if (id === "portfolio.schedule-attention") return <div className="grid grid-cols-2 gap-2 text-sm"><Info label={t("delayedProjects")} value={data.delivery.delayedProjects} /><Info label={t("delayedPrograms")} value={data.delivery.delayedPrograms} /><Info label={t("scheduleData")} value={data.delivery.projectsWithScheduleData} /><Info label={t("atRiskProjects")} value={data.summary.atRiskProjects} /></div>;
  if (id === "portfolio.milestones") return <div className="grid grid-cols-2 gap-2 text-sm"><Info label={t("overdueMilestones")} value={data.delivery.overdueMilestones} /><Info label={t("dueSoonMilestones")} value={data.delivery.dueSoonMilestones} /><Info label={t("onTrack")} value={data.delivery.onTrackMilestones} /><Info label={t("completed")} value={data.delivery.completedMilestones} /></div>;
  if (id === "portfolio.financials") return <div className="space-y-2">{data.financials.groups.slice(0, 3).map((group) => <Info key={group.currency} label={`${group.currency} · ${group.isBaseCurrency ? t("baseCurrencyFinancials") : t("foreignCurrencyExposure")}`} value={formatMoney(group.forecast, group.currency)} />)}{data.financials.hasMixedCurrencies && <p className="text-[11px] text-muted-foreground">{t("mixedCurrencyNote")}</p>}</div>;
  if (id === "portfolio.risks") return <div className="grid grid-cols-2 gap-2 text-sm"><Info label={t("openRisks")} value={data.risks.openCount} /><Info label={t("critical")} value={data.risks.criticalCount} /><Info label={t("high")} value={data.risks.highCount} /><Info label={t("sourceScope")} value={data.risks.sourceScope} /></div>;
  if (id === "portfolio.issues") return <div className="grid grid-cols-2 gap-2 text-sm"><Info label={t("openIssues")} value={data.issues.openCount} /><Info label={t("critical")} value={data.issues.criticalCount} /><Info label={t("overdue")} value={data.issues.overdueCount} /></div>;
  if (id === "portfolio.resources") return <div className="grid grid-cols-2 gap-2 text-sm"><Info label={t("activeResources")} value={data.resources.activeResourceCount} /><Info label={t("sharedResources")} value={data.resources.sharedResourceCount} /><Info label={t("crossProgram")} value={data.resources.crossProgramResourceCount} /><Info label={t("overAllocated")} value={data.resources.overAllocatedResourceCount} /></div>;
  if (id === "portfolio.status-reports") return <div className="grid grid-cols-3 gap-2 text-sm"><Info label={t("currentReports")} value={data.statusReports.currentCount} /><Info label={t("staleReports")} value={data.statusReports.staleCount} /><Info label={t("missingReports")} value={data.statusReports.missingCount} /></div>;
  return <div className="flex items-center gap-3"><CalendarDays className="h-7 w-7 text-primary" /><Info label={t("upcomingMeetings")} value={data.upcomingMeetings.length} /></div>;
}

function ProgramsTable({ data, t, programHref, onViewAs, onPageChange }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; programHref: (_id: string) => string; onViewAs: (_id: string) => void; onPageChange: (_page: number) => void }) {
  return <Card><CardHeader><CardTitle>{t("programs")}</CardTitle><CardDescription>{t("programsDescription")}</CardDescription></CardHeader><CardContent className="p-0">{data.programs.length === 0 ? <EmptyState title={t("noPrograms")} /> : <><Table><TableHeader><TableRow><TableHead>{t("program")}</TableHead><TableHead>{t("manager")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("projects")}</TableHead><TableHead>{t("delivery")}</TableHead><TableHead>{t("reporting")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{data.programs.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" href={programHref(item.id)}>{item.name}</Link><div className="text-xs text-muted-foreground">{item.code ?? "—"} · {item.currency}</div></TableCell><TableCell>{item.programManagerName ?? "—"}</TableCell><TableCell><Badge status={item.status}>{item.status ?? t("unknown")}</Badge></TableCell><TableCell><Badge status={item.health}>{item.health ?? t("unknown")}</Badge></TableCell><TableCell>{item.projectCount}<span className="text-xs text-muted-foreground"> · {item.projectsAtRisk} {t("atRiskShort")}</span></TableCell><TableCell><Badge status={item.deliveryIndicator}>{labelForState(t, item.deliveryIndicator)}</Badge></TableCell><TableCell><Badge status={item.reportingIndicator}>{labelForState(t, item.reportingIndicator)}</Badge></TableCell><TableCell><Button type="button" size="sm" variant="ghost" onClick={() => onViewAs(item.id)}>{t("viewAsProgram")}</Button></TableCell></TableRow>)}</TableBody></Table><TablePagination page={data.programsPage} totalPages={data.programsTotalPages} summary={t("pageSummary", { page: data.programsPage, pages: Math.max(data.programsTotalPages, 1) })} previousLabel={t("previous")} nextLabel={t("next")} onPageChange={onPageChange} /></>}</CardContent></Card>;
}

function ProjectsTable({ data, t, projectHref, programHref, onViewAs, onPageChange }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string; programHref: (_id: string) => string; onViewAs: (_id: string) => void; onPageChange: (_page: number) => void }) {
  return <Card><CardHeader><CardTitle>{t("projects")}</CardTitle><CardDescription>{t("projectsDescription")}</CardDescription></CardHeader><CardContent className="p-0">{data.projects.length === 0 ? <EmptyState title={t("noProjects")} /> : <><Table><TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead>{t("relationship")}</TableHead><TableHead>{t("manager")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("progress")}</TableHead><TableHead>{t("schedule")}</TableHead><TableHead>{t("reporting")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{data.projects.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" href={projectHref(item.id)}>{item.name}</Link><div className="text-xs text-muted-foreground">{item.code} · {item.currency}</div></TableCell><TableCell>{item.isDirectProject ? <Badge variant="outline">{t("directProject")}</Badge> : item.programId ? <Link className="text-xs hover:text-primary" href={programHref(item.programId)}>{item.programName ?? t("programProject")}</Link> : <span>{t("programProject")}</span>}</TableCell><TableCell>{item.projectManagerName ?? "—"}</TableCell><TableCell><Badge status={item.status}>{item.status}</Badge></TableCell><TableCell><Badge status={item.health}>{item.health}</Badge></TableCell><TableCell className="min-w-28"><Progress value={item.progress} className="h-2" /><span className="text-xs text-muted-foreground">{item.progress}%</span></TableCell><TableCell><Badge status={item.scheduleCondition}>{labelForState(t, item.scheduleCondition)}</Badge></TableCell><TableCell><Badge status={item.statusReportFreshness}>{labelForState(t, item.statusReportFreshness)}</Badge></TableCell><TableCell><Button type="button" size="sm" variant="ghost" onClick={() => onViewAs(item.id)}>{t("viewAsProject")}</Button></TableCell></TableRow>)}</TableBody></Table><TablePagination page={data.projectsPage} totalPages={data.projectsTotalPages} summary={t("pageSummary", { page: data.projectsPage, pages: Math.max(data.projectsTotalPages, 1) })} previousLabel={t("previous")} nextLabel={t("next")} onPageChange={onPageChange} /></>}</CardContent></Card>;
}

function DeliveryView({ data, t, formatDate, projectHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) { return <div className="space-y-4"><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5"><KpiCard label={t("delayedProjects")} value={data.delivery.delayedProjects} icon={Gauge} semantic="red" /><KpiCard label={t("delayedPrograms")} value={data.delivery.delayedPrograms} icon={Layers3} semantic="amber" /><KpiCard label={t("overdueMilestones")} value={data.delivery.overdueMilestones} icon={AlertTriangle} semantic="red" /><KpiCard label={t("dueSoonMilestones")} value={data.delivery.dueSoonMilestones} icon={CalendarDays} semantic="amber" /><KpiCard label={t("completedMilestones")} value={data.delivery.completedMilestones} icon={ListChecks} semantic="green" /></div><Card><CardHeader><CardTitle>{t("milestoneExposure")}</CardTitle></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("milestone")}</TableHead><TableHead>{t("project")}</TableHead><TableHead>{t("program")}</TableHead><TableHead>{t("dueDate")}</TableHead><TableHead>{t("status")}</TableHead></TableRow></TableHeader><TableBody>{data.milestones.map((item) => <TableRow key={item.id}><TableCell>{item.name}</TableCell><TableCell><Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link></TableCell><TableCell>{item.programName ?? t("directProject")}</TableCell><TableCell>{formatDate(item.dueDate)}</TableCell><TableCell><Badge status={item.state}>{labelForState(t, item.state)}</Badge></TableCell></TableRow>)}</TableBody></Table></CardContent></Card></div>; }

function FinancialView({ data, t, formatMoney, projectHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string }) { return <div className="space-y-4">{data.financials.hasMixedCurrencies && <Alert title={t("mixedCurrencyNote")} />}{data.financials.groups.length === 0 ? <EmptyState title={t("noFinancials")} /> : data.financials.groups.map((group) => <FinancialGroup key={group.currency} group={group} t={t} formatMoney={formatMoney} projectHref={projectHref} />)}</div>; }
function FinancialGroup({ group, t, formatMoney, projectHref }: { group: PortfolioFinancialGroup; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string }) { return <Card><CardHeader><CardTitle className="text-sm">{group.currency} · {group.isBaseCurrency ? t("baseCurrencyFinancials") : t("foreignCurrencyExposure")}</CardTitle></CardHeader><CardContent className="space-y-3"><div className="grid gap-2 sm:grid-cols-3 lg:grid-cols-6"><Info label={t("budget")} value={formatMoney(group.budget, group.currency)} /><Info label={t("allocated")} value={formatMoney(group.allocated, group.currency)} /><Info label={t("actual")} value={formatMoney(group.actual, group.currency)} /><Info label={t("committed")} value={formatMoney(group.committed, group.currency)} /><Info label={t("forecast")} value={formatMoney(group.forecast, group.currency)} /><Info label={t("remaining")} value={formatMoney(group.remaining, group.currency)} /></div><Table><TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead>{t("relationship")}</TableHead><TableHead className="text-right">{t("budget")}</TableHead><TableHead className="text-right">{t("actual")}</TableHead><TableHead className="text-right">{t("committed")}</TableHead></TableRow></TableHeader><TableBody>{group.contributions.map((item) => <TableRow key={item.projectId}><TableCell><Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link></TableCell><TableCell>{item.isDirectProject ? t("directProject") : item.programName ?? t("programProject")}</TableCell><TableCell className="text-right">{formatMoney(item.budget, group.currency)}</TableCell><TableCell className="text-right">{formatMoney(item.actual, group.currency)}</TableCell><TableCell className="text-right">{formatMoney(item.committed, group.currency)}</TableCell></TableRow>)}</TableBody></Table></CardContent></Card>; }

function RisksIssuesView({ data, t, projectHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string }) { return <div className="grid gap-4 lg:grid-cols-2"><Card><CardHeader><CardTitle>{t("risks")}</CardTitle><CardDescription>{t("projectRiskSource")}</CardDescription></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("risk")}</TableHead><TableHead>{t("project")}</TableHead><TableHead>{t("severity")}</TableHead><TableHead>{t("status")}</TableHead></TableRow></TableHeader><TableBody>{data.topRisks.map((item) => <TableRow key={item.id}><TableCell>{item.title}</TableCell><TableCell><Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link></TableCell><TableCell><Badge status={item.severity}>{item.severity}</Badge></TableCell><TableCell>{item.status}</TableCell></TableRow>)}</TableBody></Table></CardContent></Card><Card><CardHeader><CardTitle>{t("issues")}</CardTitle></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("issue")}</TableHead><TableHead>{t("project")}</TableHead><TableHead>{t("severity")}</TableHead><TableHead>{t("dueDate")}</TableHead></TableRow></TableHeader><TableBody>{data.topIssues.map((item) => <TableRow key={item.id}><TableCell>{item.title}</TableCell><TableCell><Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link></TableCell><TableCell><Badge status={item.severity}>{item.severity}</Badge></TableCell><TableCell>{item.targetResolutionDate ? new Date(item.targetResolutionDate).toLocaleDateString() : "—"}</TableCell></TableRow>)}</TableBody></Table></CardContent></Card></div>; }

function ResourcesView({ data, t, projectHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string }) { return <Card><CardHeader><CardTitle>{t("resourcePressure")}</CardTitle><CardDescription>{t("resourceHierarchy")}</CardDescription></CardHeader><CardContent className="space-y-3"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5"><KpiCard label={t("activeResources")} value={data.resources.activeResourceCount} icon={UsersRound} semantic="blue" /><KpiCard label={t("sharedResources")} value={data.resources.sharedResourceCount} icon={UsersRound} semantic="indigo" /><KpiCard label={t("crossProgram")} value={data.resources.crossProgramResourceCount} icon={Layers3} semantic="violet" /><KpiCard label={t("overAllocated")} value={data.resources.overAllocatedResourceCount} icon={AlertTriangle} semantic="red" /><KpiCard label={t("hoursPerWeek")} value={data.resources.allocatedHoursPerWeek} icon={Gauge} semantic="green" /></div>{data.resources.pressure.length === 0 ? <EmptyState title={t("noPressure")} /> : <ResourceTable pressure={data.resources.pressure} t={t} projectHref={projectHref} />}</CardContent></Card>; }
function ResourceTable({ pressure, t, projectHref }: { pressure: PortfolioResourcePressure[]; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string }) { return <Table><TableHeader><TableRow><TableHead>{t("resource")}</TableHead><TableHead>{t("allocation")}</TableHead><TableHead>{t("programs")}</TableHead><TableHead>{t("projects")}</TableHead><TableHead>{t("competingProjects")}</TableHead></TableRow></TableHeader><TableBody>{pressure.map((item) => <TableRow key={item.resourceId}><TableCell>{item.resourceName || "—"}</TableCell><TableCell><Badge status={item.allocationPercentage > 100 ? "Critical" : "Warning"}>{item.allocationPercentage}%</Badge></TableCell><TableCell>{item.programCount}</TableCell><TableCell>{item.projectCount}</TableCell><TableCell>{item.projects.map((project) => <Link key={project.projectId} className="me-2 text-xs hover:text-primary" href={projectHref(project.projectId)}>{project.projectName}</Link>)}</TableCell></TableRow>)}</TableBody></Table>; }

function StatusView({ data, t, formatDate, projectHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) { return <div className="space-y-4"><div className="grid gap-3 sm:grid-cols-3"><KpiCard label={t("currentReports")} value={data.statusReports.currentCount} icon={ListChecks} semantic="green" /><KpiCard label={t("staleReports")} value={data.statusReports.staleCount} icon={AlertTriangle} semantic="amber" /><KpiCard label={t("missingReports")} value={data.statusReports.missingCount} icon={ShieldAlert} semantic="red" /></div><Card><CardHeader><CardTitle>{t("statusReports")}</CardTitle><CardDescription>{t("freshnessRule", { days: data.statusReports.freshnessDays })}</CardDescription></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead>{t("program")}</TableHead><TableHead>{t("reportDate")}</TableHead><TableHead>{t("freshness")}</TableHead></TableRow></TableHeader><TableBody>{data.statusReports.latestPublished.map((item) => <TableRow key={item.projectId}><TableCell><Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link></TableCell><TableCell>{item.programName ?? t("directProject")}</TableCell><TableCell>{formatDate(item.reportDate)}</TableCell><TableCell><Badge status={item.freshness}>{labelForState(t, item.freshness)}</Badge></TableCell></TableRow>)}</TableBody></Table></CardContent></Card><div className="grid gap-4 lg:grid-cols-2"><GapList title={t("programReportingGaps")} values={data.statusReports.programsWithReportingGaps} empty={t("noReportingGaps")} /><GapList title={t("projectReportingGaps")} values={data.statusReports.projectsWithReportingGaps} empty={t("noReportingGaps")} /></div></div>; }
function ActivityView({ data, t, formatDate, projectHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) { return <Card><CardHeader><CardTitle>{t("upcomingMeetings")}</CardTitle><CardDescription>{t("projectLinkedMeetings")}</CardDescription></CardHeader><CardContent>{data.upcomingMeetings.length === 0 ? <EmptyState title={t("noMeetings")} /> : <div className="space-y-2">{data.upcomingMeetings.map((item) => <div key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm"><div><p className="font-medium">{item.title}</p><p className="text-xs text-muted-foreground">{formatDate(item.startTime)} · {item.projectId ? <Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link> : t("unknown")}</p></div><CalendarDays className="h-4 w-4 text-muted-foreground" /></div>)}</div>}</CardContent></Card>; }
function AttentionView({ data, t, projectHref, programHref }: { data: PortfolioManagementOverview; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string; programHref: (_id: string) => string }) { return <Card><CardHeader><CardTitle>{t("attentionAreas")}</CardTitle></CardHeader><CardContent>{data.attentionItems.length === 0 ? <p className="text-sm text-muted-foreground">{t("noAttention")}</p> : <div className="grid gap-2 md:grid-cols-2">{data.attentionItems.map((item, index) => <div key={`${item.kind}-${item.entityId}-${index}`} className="flex items-start justify-between gap-3 rounded-md border p-3 text-sm"><div><div className="flex items-center gap-2"><Badge status={item.level}>{labelForState(t, item.level)}</Badge><span className="font-medium">{item.entityName}</span></div><p className="mt-1 text-xs text-muted-foreground">{item.reason}</p></div>{item.entityType === "Program" ? <Link className="text-xs hover:text-primary" href={programHref(item.entityId)}>{t("open")}</Link> : item.projectId ? <Link className="text-xs hover:text-primary" href={projectHref(item.projectId)}>{t("open")}</Link> : null}</div>)}</div>}</CardContent></Card>; }
function Distribution({ values, t }: { values: Record<string, number>; t: ReturnType<typeof useTranslations> }) { return <div className="space-y-2">{Object.entries(values).length === 0 ? <p className="text-xs text-muted-foreground">{t("noData")}</p> : Object.entries(values).map(([key, value]) => <div key={key} className="flex items-center justify-between text-sm"><span>{labelForState(t, key)}</span><span className="font-semibold tabular-nums">{value}</span></div>)}</div>; }
function GapList({ title, values, empty }: { title: string; values: string[]; empty: string }) { return <Card><CardHeader><CardTitle className="text-sm">{title}</CardTitle></CardHeader><CardContent>{values.length ? <ul className="space-y-1 text-sm">{values.map((value) => <li key={value}>{value}</li>)}</ul> : <p className="text-sm text-muted-foreground">{empty}</p>}</CardContent></Card>; }
function Info({ label, value }: { label: string; value: React.ReactNode }) { return <div className="flex items-center justify-between gap-3"><span className="text-muted-foreground">{label}</span><span className="text-right font-medium">{value || "—"}</span></div>; }
function labelForState(t: ReturnType<typeof useTranslations>, value?: string) { const labels: Record<string, string> = { Healthy: t("healthy"), Green: t("healthy"), Warning: t("warning"), Amber: t("warning"), Critical: t("critical"), Red: t("critical"), Unknown: t("unknown"), Current: t("currentReports"), Stale: t("staleReports"), Missing: t("missingReports"), Delayed: t("delayed"), OnTrack: t("onTrack"), DueSoon: t("dueSoon"), Completed: t("completed"), NeedsAttention: t("needsAttention"), Informational: t("informational"), Clear: t("clear"), Available: t("available"), Unavailable: t("unavailable") }; return labels[value ?? ""] ?? value ?? t("unknown"); }
