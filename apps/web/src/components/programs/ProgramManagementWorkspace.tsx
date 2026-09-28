"use client";

import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import * as React from "react";
import {
  AlertTriangle, ArrowUpRight, CalendarDays, FolderKanban, Gauge, Layers3, ListChecks, UsersRound,
} from "lucide-react";
import { useProgramManagementOverview, type ProgramFinancialGroup, type ProgramManagementOverview, type ProgramProject, type ProgramResourcePressure } from "@/hooks/portfolios/useProgramManagement";
import { PageHeader } from "@/components/layout/PageHeader";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { KpiCard } from "@/components/ui/kpi-card";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TablePagination } from "@/components/ui/table-pagination";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { WidgetShell } from "@/components/dashboard-builder/WidgetShell";
import { useManagementPerspective } from "@/lib/perspective-context";

type WorkspaceTab = "overview" | "projects" | "delivery" | "financials" | "risks" | "resources" | "status" | "activity";

const PROGRAM_WIDGETS = [
  { id: "program.health", titleKey: "programHealth" },
  { id: "program.project-health", titleKey: "healthDistribution" },
  { id: "program.delivery", titleKey: "deliverySummary" },
  { id: "program.milestones", titleKey: "milestones" },
  { id: "program.financials", titleKey: "financials" },
  { id: "program.risks", titleKey: "riskSummary" },
  { id: "program.issues", titleKey: "issueSummary" },
  { id: "program.resources", titleKey: "resourceSummary" },
  { id: "program.status-reports", titleKey: "statusReports" },
  { id: "program.activity", titleKey: "meetingsActivity" },
] as const;

export function ProgramManagementWorkspace({ programId }: { programId: string }) {
  const t = useTranslations("programManager");
  const locale = useLocale();
  const searchParams = useSearchParams();
  const [tab, setTab] = React.useState<WorkspaceTab>(searchParams.get("tab") === "status" ? "status" : "overview");
  const [projectsPage, setProjectsPage] = React.useState(1);
  const query = useProgramManagementOverview(programId, projectsPage);
  const { current, effectiveReadOnly } = useManagementPerspective();

  if (query.isLoading) return <WorkspaceLoading />;
  if (query.isError || !query.data) return <Alert variant="danger" title={t("loadFailed")}>{t("noData")}</Alert>;

  const data = query.data;
  const program = data.program;
  const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString(locale === "ar" ? "ar-EG" : "en-GB") : "—";
  const formatMoney = (value: number, currency: string) => new Intl.NumberFormat(locale === "ar" ? "ar-EG" : "en-GB", { style: "currency", currency, maximumFractionDigits: 0 }).format(value);
  const projectHref = (id: string) => `/${locale}/projects/${encodeURIComponent(id)}`;

  return (
    <div className="space-y-5">
      <PageHeader title={program.name} description={`${t("programWorkspace")} · ${program.code ?? "—"}`}>
        <div className="flex flex-wrap items-center gap-2">
          <Badge status={program.status}>{program.status ?? t("unknown")}</Badge>
          {program.health && <Badge status={program.health}>{program.health}</Badge>}
          {effectiveReadOnly && <Badge status="ReadOnly">{t("readOnly")}</Badge>}
        </div>
      </PageHeader>

      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-card px-3 py-2 text-xs text-muted-foreground">
        <Layers3 className="h-4 w-4" />
        <span className="font-medium text-foreground">{t("program")}: {program.name}</span>
        {program.portfolioName && <><span aria-hidden="true">·</span><span>{t("portfolio")}: {program.portfolioName}</span></>}
        {current?.viewMode === "ReadOnlyViewAs" && <Badge status="ReadOnly">{t("readOnly")}</Badge>}
      </div>

      <Tabs value={tab} onValueChange={(value) => setTab(value as WorkspaceTab)}>
        <TabsList className="flex h-auto flex-wrap justify-start">
          <TabsTrigger value="overview">{t("overview")}</TabsTrigger>
          <TabsTrigger value="projects">{t("projects")}</TabsTrigger>
          <TabsTrigger value="delivery">{t("delivery")}</TabsTrigger>
          <TabsTrigger value="financials">{t("financials")}</TabsTrigger>
          <TabsTrigger value="risks">{t("risksIssues")}</TabsTrigger>
          <TabsTrigger value="resources">{t("resources")}</TabsTrigger>
          <TabsTrigger value="status">{t("statusReports")}</TabsTrigger>
          <TabsTrigger value="activity">{t("meetingsActivity")}</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-5">
          <OverviewSummary data={data} t={t} formatDate={formatDate} formatMoney={formatMoney} />
          <AttentionView data={data} t={t} projectHref={projectHref} />
          <ProgramDashboard data={data} t={t} formatMoney={formatMoney} projectHref={projectHref} />
        </TabsContent>
        <TabsContent value="projects">
          <ProjectsTable data={data} t={t} formatDate={formatDate} formatMoney={formatMoney} projectHref={projectHref} onPageChange={setProjectsPage} />
        </TabsContent>
        <TabsContent value="delivery">
          <DeliveryView data={data} t={t} formatDate={formatDate} projectHref={projectHref} />
        </TabsContent>
        <TabsContent value="financials">
          <FinancialView data={data} t={t} formatMoney={formatMoney} projectHref={projectHref} />
        </TabsContent>
        <TabsContent value="risks">
          <RisksIssuesView data={data} t={t} formatDate={formatDate} projectHref={projectHref} />
        </TabsContent>
        <TabsContent value="resources">
          <ResourcesView data={data} t={t} projectHref={projectHref} />
        </TabsContent>
        <TabsContent value="status">
          <StatusReportsView data={data} t={t} formatDate={formatDate} projectHref={projectHref} />
        </TabsContent>
        <TabsContent value="activity">
          <ActivityView data={data} t={t} formatDate={formatDate} projectHref={projectHref} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function OverviewSummary({ data, t, formatDate, formatMoney }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; formatMoney: (_value: number, _currency: string) => string }) {
  const program = data.program;
  return <>
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
      <KpiCard label={t("totalProjects")} value={data.summary.totalProjects} icon={FolderKanban} semantic="indigo" />
      <KpiCard label={t("activeProjects")} value={data.summary.activeProjects} icon={Gauge} semantic="blue" />
      <KpiCard label={t("completedProjects")} value={data.summary.completedProjects} icon={ListChecks} semantic="green" />
      <KpiCard label={t("projectsAtRisk")} value={data.summary.projectsAtRisk} icon={AlertTriangle} semantic="amber" accent={data.summary.projectsAtRisk > 0 ? "warning" : undefined} />
      <KpiCard label={t("criticalProjects")} value={data.summary.criticalProjects} icon={AlertTriangle} semantic="red" accent={data.summary.criticalProjects > 0 ? "danger" : undefined} />
    </div>
    <Card>
      <CardHeader><CardTitle className="text-sm">{t("programIdentity")}</CardTitle></CardHeader>
      <CardContent className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <Identity label={t("status")} value={program.status ?? t("unknown")} badge />
        <Identity label={t("health")} value={program.health ?? t("unknown")} badge />
        <Identity label={t("manager")} value={program.programManagerName ?? "—"} />
        <Identity label={t("portfolio")} value={program.portfolioName ?? "—"} />
        <Identity label={t("currency")} value={program.currency || "—"} />
        <Identity label={t("start")} value={formatDate(program.startDate)} />
        <Identity label={t("end")} value={formatDate(program.endDate)} />
        <Identity label={t("budget")} value={program.budget == null ? "—" : formatMoney(program.budget, program.currency)} />
      </CardContent>
    </Card>
  </>;
}

function ProgramDashboard({ data, t, formatMoney, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string }) {
  return <section aria-label={t("dashboard")} className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
    {PROGRAM_WIDGETS.map((widget) => <div key={widget.id} data-widget-id={widget.id} className="min-h-40">
      <WidgetShell title={t(widget.titleKey)} dataSource={t("dataSource")}>
        <WidgetContent id={widget.id} data={data} t={t} formatMoney={formatMoney} projectHref={projectHref} />
      </WidgetShell>
    </div>)}
  </section>;
}

function AttentionView({ data, t, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string }) {
  return <Card><CardHeader><CardTitle className="text-sm">{t("attentionAreas")}</CardTitle></CardHeader><CardContent>{data.attentionItems.length ? <div className="divide-y">{data.attentionItems.map(item => <div key={`${item.kind}-${item.itemId ?? item.projectId ?? item.message}`} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0"><Badge status={item.level}>{item.level === "NeedsAttention" ? t("needsAttention") : item.level === "Informational" ? t("informational") : t("critical")}</Badge><div className="min-w-0 flex-1 text-sm"><span className="font-medium">{attentionKindLabel(item.kind, t)}</span><span className="mx-1 text-muted-foreground">·</span>{item.projectId ? <Link href={projectHref(item.projectId)} className="hover:text-primary">{item.message}</Link> : item.message}</div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("noAttention")}</p>}</CardContent></Card>;
}

function WidgetContent({ id, data, t, formatMoney, projectHref }: { id: string; data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string }) {
  if (id === "program.health") return <div className="flex h-full items-center justify-center"><Badge status={data.program.health}>{data.program.health ?? t("unknown")}</Badge></div>;
  if (id === "program.project-health") return <div className="space-y-2">{Object.entries(data.summary.projectHealthDistribution).map(([health, count]) => <div key={health} className="flex items-center justify-between text-xs"><span>{health}</span><span className="font-semibold tabular-nums">{count}</span></div>)}</div>;
  if (id === "program.delivery") return <div className="space-y-2 text-xs"><Metric label={t("averageProgress")} value={data.summary.averageProjectProgress == null ? "—" : `${data.summary.averageProjectProgress}%`} /><Metric label={t("projectsBehindSchedule")} value={data.delivery.projectsBehindSchedule} /><Metric label={t("milestones")} value={data.delivery.totalMilestones} /></div>;
  if (id === "program.milestones") return <MiniLinks items={data.milestones.slice(0, 4).map(item => ({ id: item.id, label: `${item.name} · ${item.projectName}`, badge: item.state, href: projectHref(item.projectId) }))} empty={t("noMilestones")} />;
  if (id === "program.financials") return <div className="space-y-2">{data.financials.groups.slice(0, 3).map(group => <Metric key={group.currency} label={group.currency} value={formatMoney(group.budget, group.currency)} hint={`${t("actual")}: ${formatMoney(group.actual, group.currency)}`} />)}{data.financials.hasMixedCurrencies && <p className="text-[10px] text-muted-foreground">{t("mixedCurrencyNote")}</p>}</div>;
  if (id === "program.risks") return <Metric label={t("open")} value={data.risks.openCount} hint={Object.entries(data.risks.bySeverity).map(([key, value]) => `${key}: ${value}`).join(" · ")} />;
  if (id === "program.issues") return <Metric label={t("open")} value={data.issues.openCount} hint={data.issues.overdueCount ? `${t("overdue")}: ${data.issues.overdueCount}` : undefined} />;
  if (id === "program.resources") return <div className="space-y-2 text-xs"><Metric label={t("activeResources")} value={data.resources.activeResourceCount} /><Metric label={t("overAllocated")} value={data.resources.overAllocatedResourceCount} /><Metric label={t("hoursPerWeek")} value={data.resources.allocatedHoursPerWeek} /></div>;
  if (id === "program.status-reports") return <div className="space-y-2 text-xs"><Metric label={t("currentReports")} value={data.statusReports.currentCount} /><Metric label={t("staleReports")} value={data.statusReports.staleCount} /><Metric label={t("missingReports")} value={data.statusReports.missingCount} /></div>;
  return <MiniLinks items={data.upcomingMeetings.slice(0, 4).map(item => ({ id: item.id, label: `${item.title} · ${item.projectName ?? ""}`, badge: new Date(item.startTime).toLocaleDateString(), href: item.projectId ? projectHref(item.projectId) : undefined }))} empty={t("noMeetings")} />;
}

function ProjectsTable({ data, t, formatDate, formatMoney, projectHref, onPageChange }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string; onPageChange: (_page: number) => void }) {
  return <Card><CardHeader><CardTitle className="text-sm">{t("projects")}</CardTitle><CardDescription>{t("summary")}</CardDescription></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead>{t("projectManager")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("progress")}</TableHead><TableHead>{t("scheduleCondition")}</TableHead><TableHead>{t("budget")}</TableHead><TableHead>{t("statusReport")}</TableHead></TableRow></TableHeader><TableBody>{data.projects.length === 0 ? <TableRow><TableCell colSpan={8} className="h-24 text-center text-muted-foreground">{t("noData")}</TableCell></TableRow> : data.projects.map(project => <ProjectRow key={project.id} project={project} t={t} formatDate={formatDate} formatMoney={formatMoney} href={projectHref(project.id)} />)}</TableBody></Table></div><TablePagination page={data.projectsPage} totalPages={data.projectsTotalPages} summary={t("pageSummary", { page: data.projectsPage, pages: Math.max(data.projectsTotalPages, 1) })} previousLabel={t("previous")} nextLabel={t("next")} onPageChange={onPageChange} /></CardContent></Card>;
}

function ProjectRow({ project, t, formatDate, formatMoney, href }: { project: ProgramProject; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; formatMoney: (_value: number, _currency: string) => string; href: string }) {
  return <TableRow><TableCell><Link className="font-medium hover:text-primary" href={href}>{project.name}<span className="ms-1 text-xs text-muted-foreground">{project.code}</span></Link></TableCell><TableCell>{project.projectManagerName ?? "—"}</TableCell><TableCell><Badge status={project.status}>{project.status}</Badge></TableCell><TableCell><Badge status={project.health}>{project.health || t("unknown")}</Badge></TableCell><TableCell className="min-w-32"><div className="flex items-center gap-2"><Progress value={project.progress} className="h-1.5" /><span className="text-xs tabular-nums">{project.progress}%</span></div></TableCell><TableCell><Badge status={project.scheduleCondition}>{labelForValue(project.scheduleCondition, t)}</Badge>{project.forecastEndDate && <div className="mt-1 text-[10px] text-muted-foreground">{formatDate(project.forecastEndDate)}</div>}</TableCell><TableCell>{project.budget == null ? "—" : formatMoney(project.budget, project.currency)}</TableCell><TableCell><Badge status={project.statusReportState}>{labelForValue(project.statusReportState, t)}</Badge>{project.latestStatusReportDate && <div className="mt-1 text-[10px] text-muted-foreground">{formatDate(project.latestStatusReportDate)}</div>}</TableCell></TableRow>;
}

function DeliveryView({ data, t, formatDate, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) {
  return <div className="space-y-4"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5"><KpiCard label={t("projectsBehindSchedule")} value={data.delivery.projectsBehindSchedule} icon={AlertTriangle} semantic="amber" /><KpiCard label={t("scheduleData")} value={data.delivery.projectsWithScheduleData} icon={CalendarDays} semantic="blue" /><KpiCard label={t("overdue")} value={data.delivery.overdueMilestones} icon={AlertTriangle} semantic="red" /><KpiCard label={t("dueSoon")} value={data.delivery.dueSoonMilestones} icon={CalendarDays} semantic="amber" /><KpiCard label={t("completed")} value={data.delivery.completedMilestones} icon={ListChecks} semantic="green" /></div><Card><CardHeader><CardTitle className="text-sm">{t("milestones")}</CardTitle></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("milestone")}</TableHead><TableHead>{t("project")}</TableHead><TableHead>{t("end")}</TableHead><TableHead>{t("status")}</TableHead></TableRow></TableHeader><TableBody>{data.milestones.length ? data.milestones.map(item => <TableRow key={item.id}><TableCell className="font-medium">{item.name}</TableCell><TableCell><Link className="hover:text-primary" href={projectHref(item.projectId)}>{item.projectName}</Link></TableCell><TableCell>{formatDate(item.dueDate)}</TableCell><TableCell><Badge status={item.state}>{item.state}</Badge></TableCell></TableRow>) : <TableRow><TableCell colSpan={4} className="h-24 text-center text-muted-foreground">{t("noMilestones")}</TableCell></TableRow>}</TableBody></Table></CardContent></Card></div>;
}

function FinancialView({ data, t, formatMoney, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string }) {
  return <div className="space-y-4">{data.financials.hasMixedCurrencies && <Alert variant="info" title={t("foreignCurrency")}>{t("mixedCurrencyNote")}</Alert>}{data.financials.groups.length ? data.financials.groups.map(group => <FinancialGroup key={group.currency} group={group} t={t} formatMoney={formatMoney} projectHref={projectHref} />) : <EmptyCard text={t("financialUnavailable")} />}</div>;
}

function FinancialGroup({ group, t, formatMoney, projectHref }: { group: ProgramFinancialGroup; t: ReturnType<typeof useTranslations>; formatMoney: (_value: number, _currency: string) => string; projectHref: (_id: string) => string }) {
  return <Card><CardHeader><CardTitle className="text-sm">{group.isBaseCurrency ? t("baseCurrency") : t("foreignCurrency")} · {group.currency}</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5"><Metric label={t("budget")} value={formatMoney(group.budget, group.currency)} /><Metric label={t("actual")} value={formatMoney(group.actual, group.currency)} /><Metric label={t("committed")} value={formatMoney(group.committed, group.currency)} /><Metric label={t("forecast")} value={formatMoney(group.forecast, group.currency)} /><Metric label={t("remaining")} value={group.remaining == null ? "—" : formatMoney(group.remaining, group.currency)} /></div><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead className="text-right">{t("budget")}</TableHead><TableHead className="text-right">{t("actual")}</TableHead><TableHead className="text-right">{t("committed")}</TableHead><TableHead className="text-right">{t("forecast")}</TableHead></TableRow></TableHeader><TableBody>{group.projects.map(project => <TableRow key={project.projectId}><TableCell><Link href={projectHref(project.projectId)} className="font-medium hover:text-primary">{project.projectName}</Link></TableCell><TableCell className="text-right">{formatMoney(project.budget, group.currency)}</TableCell><TableCell className="text-right">{formatMoney(project.actual, group.currency)}</TableCell><TableCell className="text-right">{formatMoney(project.committed, group.currency)}</TableCell><TableCell className="text-right">{formatMoney(project.forecast, group.currency)}</TableCell></TableRow>)}</TableBody></Table></div></CardContent></Card>;
}

function RisksIssuesView({ data, t, formatDate, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) {
  return <div className="grid gap-4 xl:grid-cols-2"><SummaryCard title={t("riskSummary")} count={data.risks.openCount} overdue={data.risks.overdueCount} openLabel={t("open")} overdueLabel={t("overdue")}><Table><TableHeader><TableRow><TableHead>{t("risk")}</TableHead><TableHead>{t("project")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("status")}</TableHead></TableRow></TableHeader><TableBody>{data.topRisks.length ? data.topRisks.map(item => <TableRow key={item.id}><TableCell className="font-medium">{item.title}</TableCell><TableCell><Link href={projectHref(item.projectId)} className="hover:text-primary">{item.projectName}</Link></TableCell><TableCell><Badge status={item.severity}>{item.severity}</Badge></TableCell><TableCell>{item.status}</TableCell></TableRow>) : <TableRow><TableCell colSpan={4} className="h-24 text-center text-muted-foreground">{t("noRisks")}</TableCell></TableRow>}</TableBody></Table></SummaryCard><SummaryCard title={t("issueSummary")} count={data.issues.openCount} overdue={data.issues.overdueCount} openLabel={t("open")} overdueLabel={t("overdue")}><Table><TableHeader><TableRow><TableHead>{t("issue")}</TableHead><TableHead>{t("project")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("end")}</TableHead></TableRow></TableHeader><TableBody>{data.topIssues.length ? data.topIssues.map(item => <TableRow key={item.id}><TableCell className="font-medium">{item.title}</TableCell><TableCell><Link href={projectHref(item.projectId)} className="hover:text-primary">{item.projectName}</Link></TableCell><TableCell><Badge status={item.severity}>{item.severity || item.priority}</Badge></TableCell><TableCell>{formatDate(item.targetResolutionDate)}</TableCell></TableRow>) : <TableRow><TableCell colSpan={4} className="h-24 text-center text-muted-foreground">{t("noIssues")}</TableCell></TableRow>}</TableBody></Table></SummaryCard></div>;
}

function ResourcesView({ data, t, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; projectHref: (_id: string) => string }) {
  return <div className="space-y-4"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><KpiCard label={t("activeAllocations")} value={data.resources.activeAllocationCount} icon={UsersRound} semantic="blue" /><KpiCard label={t("activeResources")} value={data.resources.activeResourceCount} icon={UsersRound} semantic="indigo" /><KpiCard label={t("sharedResources")} value={data.resources.sharedResourceCount} icon={UsersRound} semantic="violet" /><KpiCard label={t("overAllocated")} value={data.resources.overAllocatedResourceCount} icon={AlertTriangle} semantic="red" /></div><Card><CardHeader><CardTitle className="text-sm">{t("resourcePressure")}</CardTitle></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("resource")}</TableHead><TableHead>{t("projectCount")}</TableHead><TableHead>{t("progress")}</TableHead><TableHead>{t("projects")}</TableHead></TableRow></TableHeader><TableBody>{data.resources.pressure.length ? data.resources.pressure.map(item => <ResourcePressureRow key={item.resourceId} item={item} projectHref={projectHref} />) : <TableRow><TableCell colSpan={4} className="h-24 text-center text-muted-foreground">{t("noPressure")}</TableCell></TableRow>}</TableBody></Table></CardContent></Card></div>;
}

function ResourcePressureRow({ item, projectHref }: { item: ProgramResourcePressure; projectHref: (_id: string) => string }) { return <TableRow><TableCell className="font-medium">{item.resourceName || "—"}</TableCell><TableCell>{item.projectCount}</TableCell><TableCell className="min-w-36"><div className="flex items-center gap-2"><Progress value={Math.min(item.allocationPercentage, 100)} className="h-1.5" /><span className="text-xs tabular-nums">{item.allocationPercentage}%</span></div></TableCell><TableCell><div className="flex flex-wrap gap-1">{item.projects.map(project => <Link key={project.projectId} href={projectHref(project.projectId)} className="text-xs text-primary hover:underline">{project.projectName}</Link>)}</div></TableCell></TableRow>; }

function StatusReportsView({ data, t, formatDate, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) { return <Card><CardHeader><CardTitle className="text-sm">{t("statusReports")}</CardTitle><CardDescription>{t("freshnessRule", { days: data.statusReports.freshnessDays })}</CardDescription></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead>{t("reportDate")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("progress")}</TableHead><TableHead>{t("freshness")}</TableHead></TableRow></TableHeader><TableBody>{data.statusReports.latestPublished.length ? data.statusReports.latestPublished.map(item => <TableRow key={item.projectId}><TableCell><Link href={projectHref(item.projectId)} className="font-medium hover:text-primary">{item.projectName}</Link></TableCell><TableCell>{formatDate(item.reportDate)}</TableCell><TableCell><Badge status={item.health}>{item.health ?? t("unknown")}</Badge></TableCell><TableCell>{item.progress == null ? "—" : `${item.progress}%`}</TableCell><TableCell><Badge status={item.freshness}>{item.freshness}</Badge></TableCell></TableRow>) : <TableRow><TableCell colSpan={5} className="h-24 text-center text-muted-foreground">{t("noData")}</TableCell></TableRow>}</TableBody></Table></CardContent></Card>; }

function ActivityView({ data, t, formatDate, projectHref }: { data: ProgramManagementOverview; t: ReturnType<typeof useTranslations>; formatDate: (_value?: string) => string; projectHref: (_id: string) => string }) { return <Card><CardHeader><CardTitle className="text-sm">{t("upcomingMeetings")}</CardTitle></CardHeader><CardContent>{data.upcomingMeetings.length ? <div className="divide-y">{data.upcomingMeetings.map(item => <div key={item.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0"><CalendarDays className="h-4 w-4 shrink-0 text-muted-foreground" /><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{item.title}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(item.startTime)} · {item.projectId ? <Link href={projectHref(item.projectId)} className="hover:text-primary">{item.projectName}</Link> : "—"}</p></div><ArrowUpRight className="h-4 w-4 text-muted-foreground" /></div>)}</div> : <div className="py-8 text-center text-sm text-muted-foreground">{t("noMeetings")}</div>}</CardContent></Card>; }

function SummaryCard({ title, count, overdue, openLabel, overdueLabel, children }: { title: string; count: number; overdue: number; openLabel: string; overdueLabel: string; children: React.ReactNode }) { return <Card><CardHeader className="flex flex-row items-start justify-between gap-3"><div><CardTitle className="text-sm">{title}</CardTitle><CardDescription>{count} {openLabel}{overdue > 0 ? ` · ${overdue} ${overdueLabel}` : ""}</CardDescription></div><Badge status={overdue > 0 ? "Red" : "Green"}>{count}</Badge></CardHeader><CardContent className="p-0">{children}</CardContent></Card>; }
function Identity({ label, value, badge }: { label: string; value: string; badge?: boolean }) { return <div><dt className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</dt><dd className="mt-1">{badge ? <Badge status={value}>{value}</Badge> : <span className="font-medium">{value}</span>}</dd></div>; }
function Metric({ label, value, hint }: { label: string; value: string | number; hint?: string }) { return <div><p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-1 text-sm font-semibold tabular-nums">{value}</p>{hint && <p className="mt-0.5 text-[10px] text-muted-foreground">{hint}</p>}</div>; }
function MiniLinks({ items, empty }: { items: { id: string; label: string; badge: string; href?: string }[]; empty: string }) { return items.length ? <div className="space-y-2">{items.map(item => <div key={item.id} className="flex items-center gap-2 text-xs"><div className="min-w-0 flex-1 truncate">{item.href ? <Link href={item.href} className="hover:text-primary">{item.label}</Link> : item.label}</div><Badge status={item.badge}>{item.badge}</Badge></div>)}</div> : <p className="text-xs text-muted-foreground">{empty}</p>; }
function EmptyCard({ text }: { text: string }) { return <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{text}</CardContent></Card>; }
function WorkspaceLoading() { return <div className="space-y-5"><Skeleton className="h-10 w-1/2" /><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">{[1, 2, 3, 4, 5].map(item => <Skeleton key={item} className="h-24" />)}</div><Skeleton className="h-64 w-full" /></div>; }
function labelForValue(value: string, t: ReturnType<typeof useTranslations>): string {
  const keys: Record<string, string> = { Unknown: "unknown", OnTrack: "onTrack", Delayed: "overdue", Current: "currentReports", Stale: "staleReports", Missing: "missingReports", Available: "financial", Unavailable: "notAvailable", DueSoon: "dueSoon" };
  return keys[value] ? t(keys[value]) : value;
}

function attentionKindLabel(value: string, t: ReturnType<typeof useTranslations>): string {
  const keys: Record<string, string> = { Project: "project", Milestone: "milestone", Risk: "risk", Issue: "issue", ChangeRequest: "changeRequest", StatusReport: "statusReport" };
  return keys[value] ? t(keys[value]) : value;
}
