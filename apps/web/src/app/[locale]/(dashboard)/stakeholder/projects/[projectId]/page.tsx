"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { ArrowLeft, CalendarDays, ClipboardList } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useMyStakeholderProject } from "@/hooks/stakeholder/useStakeholderProjects";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import { formatDate } from "@/lib/utils";

export default function StakeholderProjectDetailPage() {
  const locale = useLocale();
  const t = useTranslations("stakeholder");
  const { statusLabel, healthLabel } = useProjectEnumLabels();
  const params = useParams();
  const projectId = params.projectId as string;
  const query = useMyStakeholderProject(projectId);
  const project = query.data;
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  if (query.isLoading) return <div className="space-y-4"><PageHeader title={t("detailTitle")} description={t("detailDescription")} /><LoadingSkeleton rows={6} /></div>;
  if (query.isError || !project) return <div className="space-y-4"><PageHeader title={t("detailTitle")} description={t("detailDescription")} /><ErrorState title={t("notAvailableTitle")} description={t("notAvailableDescription")} onRetry={() => void query.refetch()} /></div>;

  const report = project.latestPublishedStatusReport;
  return (
    <div className="space-y-4">
      <PageHeader title={project.name} description={`${project.code}${project.description ? ` · ${project.description}` : ""}`}><Badge status={project.status}>{statusLabel(project.status)}</Badge></PageHeader>
      <Link href={`/${locale}/stakeholder/projects`}><Button variant="ghost" size="sm"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("backToProjects")}</Button></Link>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><InfoCard label={t("status")} value={statusLabel(project.status)} /><InfoCard label={t("health")} value={healthLabel(project.health)} badge status={project.health} /><InfoCard label={t("progress")} value={`${project.progress.toLocaleString(locale === "ar" ? "ar-EG" : "en-GB")} %`} /><InfoCard label={t("dates")} value={`${formatDate(project.startDate, dateLocale)} – ${formatDate(project.endDate, dateLocale)}`} /></div>
      <Card className="shadow-none"><CardHeader><h2 className="text-sm font-semibold">{t("peopleTitle")}</h2></CardHeader><CardContent className="grid gap-3 sm:grid-cols-2"><Person label={t("projectManager")} value={project.projectManagerName} /><Person label={t("sponsor")} value={project.sponsorName} /></CardContent></Card>
      <Card className="shadow-none"><CardHeader><div className="flex items-center gap-2"><ClipboardList className="h-4 w-4 text-primary" /><div><h2 className="text-sm font-semibold">{t("latestUpdate")}</h2><p className="text-xs text-muted-foreground">{t("publishedOnlyHint")}</p></div></div></CardHeader><CardContent>{report ? <div className="space-y-4"><div className="flex flex-wrap items-center gap-2"><Badge status={report.projectLifecycleStatus}>{statusLabel(report.projectLifecycleStatus || project.status)}</Badge>{report.overallHealth ? <Badge status={report.overallHealth}>{healthLabel(report.overallHealth)}</Badge> : null}<span className="text-xs text-muted-foreground">{formatDate(report.reportDate, dateLocale)} · {formatDate(report.periodFrom, dateLocale)} – {formatDate(report.periodTo, dateLocale)}</span></div><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{report.scheduleHealth ? <Health label={t("scheduleHealth")} value={healthLabel(report.scheduleHealth)} status={report.scheduleHealth} /> : null}{report.costHealth ? <Health label={t("costHealth")} value={healthLabel(report.costHealth)} status={report.costHealth} /> : null}{report.scopeHealth ? <Health label={t("scopeHealth")} value={healthLabel(report.scopeHealth)} status={report.scopeHealth} /> : null}{report.qualityHealth ? <Health label={t("qualityHealth")} value={healthLabel(report.qualityHealth)} status={report.qualityHealth} /> : null}{report.resourceHealth ? <Health label={t("resourceHealth")} value={healthLabel(report.resourceHealth)} status={report.resourceHealth} /> : null}</div><Narrative label={t("executiveSummary")} value={report.executiveSummary} /><Narrative label={t("keyAchievements")} value={report.keyAchievements} /><Narrative label={t("challengesConcerns")} value={report.challengesConcerns} /><Narrative label={t("nextPeriodPriorities")} value={report.nextPeriodPriorities} /></div> : <EmptyState icon={ClipboardList} title={t("noPublishedUpdate")} description={t("noPublishedUpdateDescription")} />}</CardContent></Card>
      <Card className="shadow-none"><CardHeader><div className="flex items-center gap-2"><CalendarDays className="h-4 w-4 text-primary" /><div><h2 className="text-sm font-semibold">{t("milestones")}</h2><p className="text-xs text-muted-foreground">{t("milestonesHint")}</p></div></div></CardHeader><CardContent>{project.milestones.length === 0 ? <EmptyState icon={CalendarDays} title={t("noMilestones")} description={t("noMilestonesDescription")} /> : <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("milestone")}</TableHead><TableHead>{t("dueDate")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("progress")}</TableHead></TableRow></TableHeader><TableBody>{project.milestones.map((milestone) => <TableRow key={milestone.id}><TableCell className="font-medium">{milestone.name}</TableCell><TableCell>{formatDate(milestone.dueDate, dateLocale)}</TableCell><TableCell><Badge status={milestone.status}>{milestone.status || t("notStarted")}</Badge></TableCell><TableCell className="tabular-nums">{milestone.progress == null ? "—" : `${milestone.progress.toLocaleString(locale === "ar" ? "ar-EG" : "en-GB")} %`}</TableCell></TableRow>)}</TableBody></Table></div>}</CardContent></Card>
      <Card className="shadow-none"><CardHeader><h2 className="text-sm font-semibold">{t("statusHistory")}</h2><p className="text-xs text-muted-foreground">{t("publishedOnlyHint")}</p></CardHeader><CardContent>{project.publishedStatusHistory.length === 0 ? <EmptyState icon={ClipboardList} title={t("noPublishedHistory")} description={t("noPublishedHistoryDescription")} /> : <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("reportDate")}</TableHead><TableHead>{t("reportingPeriod")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("progress")}</TableHead></TableRow></TableHeader><TableBody>{project.publishedStatusHistory.map((entry) => <TableRow key={entry.id}><TableCell>{formatDate(entry.reportDate, dateLocale)}</TableCell><TableCell>{formatDate(entry.periodFrom, dateLocale)} – {formatDate(entry.periodTo, dateLocale)}</TableCell><TableCell><Badge status={entry.projectLifecycleStatus}>{statusLabel(entry.projectLifecycleStatus || project.status)}</Badge></TableCell><TableCell><Badge status={entry.overallHealth}>{entry.overallHealth ? healthLabel(entry.overallHealth) : "—"}</Badge></TableCell><TableCell>{entry.overallProgressPercent == null ? "—" : `${entry.overallProgressPercent.toLocaleString(locale === "ar" ? "ar-EG" : "en-GB")} %`}</TableCell></TableRow>)}</TableBody></Table></div>}</CardContent></Card>
    </div>
  );
}

function InfoCard({ label, value, badge = false, status }: { label: string; value: string; badge?: boolean; status?: string }) { return <Card className="shadow-none"><CardContent className="p-4"><p className="text-xs text-muted-foreground">{label}</p>{badge ? <Badge status={status ?? value} className="mt-2">{value}</Badge> : <p className="mt-2 text-sm font-semibold">{value}</p>}</CardContent></Card>; }
function Person({ label, value }: { label: string; value?: string }) { return <div className="rounded-lg border bg-muted/20 p-3"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 text-sm font-medium">{value || "—"}</p></div>; }
function Health({ label, value, status }: { label: string; value: string; status?: string }) { return <div className="flex items-center justify-between rounded-lg border bg-muted/20 p-3"><span className="text-xs text-muted-foreground">{label}</span><Badge status={status ?? value}>{value}</Badge></div>; }
function Narrative({ label, value }: { label: string; value?: string }) { return value ? <div><p className="text-xs font-semibold text-muted-foreground">{label}</p><p className="mt-1 whitespace-pre-wrap text-sm">{value}</p></div> : null; }
