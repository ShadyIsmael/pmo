"use client";

import Link from "next/link";
import { useTranslations } from "next-intl";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ArrowLeft, Lock, Users, GanttChartSquare, Milestone, Package, ClipboardList, AlertTriangle, AlertOctagon, Wallet, Banknote, CalendarDays, FileText, BarChart3 } from "lucide-react";
import { formatCurrency, formatDate } from "@/lib/utils";
import { useProjectSnapshot } from "@/hooks/projects/useProjects";

function Section({ title, icon, empty, count, children }: { title: string; icon: React.ReactNode; empty: string; count?: number; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-sm flex items-center gap-2">
          {icon}
          {title}
          {count !== undefined && <span className="text-muted-foreground font-normal">({count})</span>}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {count !== undefined && count === 0 ? (
          <p className="text-sm text-muted-foreground">{empty}</p>
        ) : (
          children
        )}
      </CardContent>
    </Card>
  );
}

function Row({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex justify-between text-sm py-1">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium text-right max-w-[60%]">{value ?? "-"}</span>
    </div>
  );
}

export function ProjectSnapshotView({ projectId, snapshotId }: { projectId: string; snapshotId: string }) {
  const t = useTranslations("projects");
  const { data: snapshot, isLoading, isError } = useProjectSnapshot(projectId, snapshotId);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Card><CardContent className="h-24"><Skeleton className="h-full" /></CardContent></Card>
        <div className="grid gap-4 lg:grid-cols-2">
          <Card><CardContent className="h-40"><Skeleton className="h-full" /></CardContent></Card>
          <Card><CardContent className="h-40"><Skeleton className="h-full" /></CardContent></Card>
        </div>
      </div>
    );
  }

  if (isError || !snapshot) {
    return (
      <Card>
        <CardContent className="py-12 text-center">
          <AlertTriangle className="h-12 w-12 text-status-red mx-auto mb-4" />
          <p className="text-muted-foreground">{t("noSnapshots")}</p>
        </CardContent>
      </Card>
    );
  }

  const data = snapshot.data;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-2">
          <Badge variant="secondary" className="gap-1"><Lock className="h-3 w-3" />{t("readOnlySnapshot")}</Badge>
          <Badge variant="outline">{t("snapshotNumber")} #{snapshot.snapshotNumber}</Badge>
        </div>
        <Link href={`/projects/${projectId}`}>
          <Button variant="outline" size="sm"><ArrowLeft className="mr-2 h-4 w-4" />{t("backToProject")}</Button>
        </Link>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{snapshot.name}</CardTitle>
          <CardDescription>{t("readOnlySnapshotDescription")}</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-1">
            <Row label={t("snapshotDate")} value={formatDate(snapshot.snapshotDate)} />
            <Row label={t("effectiveEndDate")} value={formatDate(snapshot.effectiveEndDate)} />
            <Row label={t("previousManager")} value={snapshot.previousProjectManagerName} />
            <Row label={t("newManager")} value={snapshot.newProjectManagerName} />
            <Row label={t("handoverReason")} value={snapshot.handoverReason} />
            {snapshot.handoverNotes && <Row label={t("handoverNotes")} value={snapshot.handoverNotes} />}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm flex items-center gap-2">{t("snapshotProjectDetails")}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-1">
            <Row label={t("name")} value={snapshot.name} />
            <Row label={t("code")} value={snapshot.code} />
            <Row label={t("status")} value={snapshot.status} />
            <Row label={t("priority")} value={snapshot.priority} />
            <Row label={t("phase")} value={snapshot.phase} />
            <Row label={t("health")} value={snapshot.health} />
            <Row label={t("confidenceLevel")} value={snapshot.confidenceLevel} />
            <Row label={t("riskRating")} value={snapshot.riskRating} />
            <Row label={t("portfolio")} value={snapshot.portfolioName} />
            <Row label={t("program")} value={snapshot.programName} />
            <Row label={t("department")} value={snapshot.departmentName} />
            <Row label={t("sponsor")} value={snapshot.sponsorName} />
            <Row label={t("startDate")} value={snapshot.startDate ? formatDate(snapshot.startDate) : undefined} />
            <Row label={t("endDate")} value={snapshot.endDate ? formatDate(snapshot.endDate) : undefined} />
            <Row label={t("budget")} value={snapshot.budget !== undefined ? formatCurrency(snapshot.budget) : undefined} />
            <Row label={t("actualCost")} value={snapshot.actualCost !== undefined ? formatCurrency(snapshot.actualCost) : undefined} />
            <Row label={t("progress")} value={snapshot.progress !== undefined ? `${snapshot.progress}%` : undefined} />
            {snapshot.description && <Row label={t("description")} value={snapshot.description} />}
            {snapshot.businessCase && <Row label={t("businessCase")} value={snapshot.businessCase} />}
            {snapshot.notes && <Row label={t("notes")} value={snapshot.notes} />}
          </div>
        </CardContent>
      </Card>

      {data && (
        <div className="grid gap-4 lg:grid-cols-2">
          <Section title={t("snapshotTeam")} icon={<Users className="h-4 w-4" />} count={data.members?.length ?? 0} empty={t("noTeamMembers")}>
            <div className="space-y-2">
              {(data.members ?? []).map((m) => (
                <div key={m.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{m.resourceName || "-"}</span>
                  <span className="text-muted-foreground text-xs">{m.role} · {m.allocationPercentage}% · {formatDate(m.startDate)} - {formatDate(m.endDate)}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotWorkItems")} icon={<ClipboardList className="h-4 w-4" />} count={data.workItems?.length ?? 0} empty={t("noTasksYet")}>
            <div className="space-y-2">
              {(data.workItems ?? []).map((w) => (
                <div key={w.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{w.title}</span>
                  <span className="text-muted-foreground text-xs">{w.status}{w.assigneeName ? ` · ${w.assigneeName}` : ""}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotMilestones")} icon={<Milestone className="h-4 w-4" />} count={data.milestones?.length ?? 0} empty={t("noMilestonesYet")}>
            <div className="space-y-2">
              {(data.milestones ?? []).map((m) => (
                <div key={m.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{m.name}</span>
                  <span className="text-muted-foreground text-xs">{m.status}{m.dueDate ? ` · ${formatDate(m.dueDate)}` : ""}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotDeliverables")} icon={<Package className="h-4 w-4" />} count={data.deliverables?.length ?? 0} empty={t("noDeliverablesYet")}>
            <div className="space-y-2">
              {(data.deliverables ?? []).map((d) => (
                <div key={d.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{d.name}</span>
                  <span className="text-muted-foreground text-xs">{d.status}{d.dueDate ? ` · ${formatDate(d.dueDate)}` : ""}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotPhases")} icon={<GanttChartSquare className="h-4 w-4" />} count={data.phases?.length ?? 0} empty={t("noPhasesYet")}>
            <div className="space-y-2">
              {(data.phases ?? []).map((p) => (
                <div key={p.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{p.name}</span>
                  <span className="text-muted-foreground text-xs">{p.status}{p.progress !== undefined ? ` · ${p.progress}%` : ""}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotRisks")} icon={<AlertTriangle className="h-4 w-4" />} count={data.risks?.length ?? 0} empty={t("noRisksYet")}>
            <div className="space-y-2">
              {(data.risks ?? []).map((r) => (
                <div key={r.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{r.title}</span>
                  <span className="text-muted-foreground text-xs">{r.riskLevel}{r.status ? ` · ${r.status}` : ""}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotIssues")} icon={<AlertOctagon className="h-4 w-4" />} count={data.issues?.length ?? 0} empty={t("noIssuesYet")}>
            <div className="space-y-2">
              {(data.issues ?? []).map((i) => (
                <div key={i.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{i.title}</span>
                  <span className="text-muted-foreground text-xs">{i.severity}{i.status ? ` · ${i.status}` : ""}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotBudgets")} icon={<Wallet className="h-4 w-4" />} count={data.budgets?.length ?? 0} empty={t("noBudgetsYet")}>
            <div className="space-y-2">
              {(data.budgets ?? []).map((b) => (
                <div key={b.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{b.name}</span>
                  <span className="text-muted-foreground text-xs">{formatCurrency(b.approvedAmount)} {b.currency}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotCostEntries")} icon={<Banknote className="h-4 w-4" />} count={data.costEntries?.length ?? 0} empty={t("noCostEntriesYet")}>
            <div className="space-y-2">
              {(data.costEntries ?? []).map((c) => (
                <div key={c.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{c.description || c.category}</span>
                  <span className="text-muted-foreground text-xs">{formatCurrency(c.amount)} {c.currency} · {formatDate(c.incurredDate)}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotMeetings")} icon={<CalendarDays className="h-4 w-4" />} count={data.meetings?.length ?? 0} empty={t("noMeetingsYet")}>
            <div className="space-y-2">
              {(data.meetings ?? []).map((m) => (
                <div key={m.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{m.title}</span>
                  <span className="text-muted-foreground text-xs">{formatDate(m.startTime)}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotDocuments")} icon={<FileText className="h-4 w-4" />} count={data.documents?.length ?? 0} empty={t("noDocumentsYet")}>
            <div className="space-y-2">
              {(data.documents ?? []).map((d) => (
                <div key={d.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{d.name}</span>
                  <span className="text-muted-foreground text-xs">v{d.version} · {d.status}</span>
                </div>
              ))}
            </div>
          </Section>

          <Section title={t("snapshotStatusReports")} icon={<BarChart3 className="h-4 w-4" />} count={data.statusReports?.length ?? 0} empty={t("noStatusReportsYet")}>
            <div className="space-y-2">
              {(data.statusReports ?? []).map((r) => (
                <div key={r.originalId} className="flex items-center justify-between text-sm">
                  <span className="font-medium truncate">{r.summary || r.status || "Status report"}</span>
                  <span className="text-muted-foreground text-xs">{formatDate(r.reportedAt)}</span>
                </div>
              ))}
            </div>
          </Section>
        </div>
      )}
    </div>
  );
}
