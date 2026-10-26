import { Link, useOutletContext } from "react-router-dom";
import { ArrowUpRight, ClipboardList, FolderKanban, Plus, ShieldAlert } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader } from "@components/ui/Card";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { getRiskLevelTextClass, getRiskStatusTextClass } from "@lib/status-presentation";
import { useRisks } from "@features/risks/api/risks";
import { useWorkItems, useWorkItemStatuses } from "@api/workItems";

function statusName(status: string, statuses: { code: string; nameEn: string; nameAr: string }[], locale: string, fallback: string) {
  const found = statuses.find((s) => s.code === status);
  if (!found) return fallback;
  return locale === "ar" ? found.nameAr : found.nameEn;
}

function displayDueDate(value: string, locale: string) {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return "—";
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-EG" : "en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(date);
}

function enumLabel(t: (key: string) => string, namespace: string, value: string | null | undefined): string {
  if (!value) return "—";
  const key = `${namespace}.${value.charAt(0).toLowerCase()}${value.slice(1)}`;
  const label = t(key);
  return label === key ? value : label;
}

export function ProjectRisksTab() {
  const { projectId, project } = useOutletContext<{ projectId: string; project: { name: string } }>();
  const { t, locale } = useI18n();
  const risksQuery = useRisks({ projectId, pageSize: 5 }, Boolean(projectId));
  const workItemsQuery = useWorkItems({ page: 1, pageSize: 5, projectId });
  const statusesQuery = useWorkItemStatuses(true);
  const risks = risksQuery.data?.items ?? [];
  const items = workItemsQuery.data?.items ?? [];
  const statuses = statusesQuery.data ?? [];

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card className="shadow-none">
        <CardHeader className="flex !flex-row items-start justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-1.5 text-sm font-semibold"><FolderKanban className="h-3.5 w-3.5 shrink-0" />{project.name}: {t("projects.projectRisksTitle")}</h2>
            <p className="mt-1 text-xs text-muted-foreground">{t("projects.projectRisksDescription")}</p>
          </div>
          <div className="flex shrink-0 gap-2">
            <Link to={`/risks-issues/risks?projectId=${encodeURIComponent(projectId)}`}><Button size="sm" variant="outline"><ArrowUpRight className="me-1.5 h-3.5 w-3.5" />{t("common.viewAll")}</Button></Link>
            <Link to={`/risks-issues/risks/new?projectId=${encodeURIComponent(projectId)}`}><Button size="sm"><Plus className="me-1.5 h-3.5 w-3.5" />{t("risks.create")}</Button></Link>
          </div>
        </CardHeader>
        <CardContent>
          {risksQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div>
          : risksQuery.isError ? <p className="text-sm text-destructive">{t("risks.loadError")}</p>
          : risks.length === 0 ? <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 text-center"><ShieldAlert className="h-7 w-7 text-muted-foreground/50" /><p className="mt-2 text-sm font-medium">{t("risks.empty")}</p></div>
          : <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("risks.titleField")}</TableHead><TableHead>{t("risks.riskLevel")}</TableHead><TableHead>{t("risks.status")}</TableHead><TableHead>{t("projects.owner")}</TableHead></TableRow></TableHeader>
            <TableBody>{risks.map((r) => <TableRow key={r.id}><TableCell><Link to={`/risks-issues/risks/${encodeURIComponent(r.id)}?from=project&projectId=${encodeURIComponent(projectId)}`} className="font-medium text-primary underline-offset-4 hover:underline">{r.title}</Link></TableCell><TableCell><span className={`font-semibold ${getRiskLevelTextClass(r.riskLevel)}`}>{enumLabel(t, "enums.riskLevel", r.riskLevel)}</span></TableCell><TableCell><span className={`font-semibold ${getRiskStatusTextClass(r.status)}`}>{enumLabel(t, "enums.riskStatus", r.status)}</span></TableCell><TableCell>{r.ownerResourceName || "—"}</TableCell></TableRow>)}</TableBody></Table></div>}
        </CardContent>
      </Card>

      <Card className="shadow-none">
        <CardHeader className="flex !flex-row items-start justify-between gap-3">
          <div>
            <h2 className="text-sm font-semibold">{t("projects.tasksAndIssues")}</h2>
            <p className="mt-1 text-xs text-muted-foreground">{t("projects.tasksAndIssuesDescription")}</p>
          </div>
          <Link to={`/work-items?projectId=${encodeURIComponent(projectId)}`}><Button size="sm" variant="outline" className="shrink-0"><ArrowUpRight className="me-1.5 h-3.5 w-3.5" />{t("common.viewAll")}</Button></Link>
        </CardHeader>
        <CardContent>
          {workItemsQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div>
          : workItemsQuery.isError ? <p className="text-sm text-destructive">{t("common.errorLoading")}</p>
          : items.length === 0 ? <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 text-center"><ClipboardList className="h-7 w-7 text-muted-foreground/50" /><p className="mt-2 text-sm font-medium">{t("projects.noTasksAndIssues")}</p></div>
          : <div className="divide-y">{items.map((item) => <Link key={item.id} to={`/work-items/${encodeURIComponent(item.id)}`} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0 hover:bg-muted/20"><div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-muted/40 ${item.type === "Issue" ? "text-info" : "text-primary"}`}><ClipboardList className="h-4 w-4" /></div><div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold">{item.title}</p><div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground"><span className={item.type === "Issue" ? "font-medium text-info" : "font-medium text-primary"}>{item.type === "Issue" ? t("common.issues") : t("common.tasks")}</span><span aria-hidden="true">·</span><span>{statusName(item.status, statuses, locale, item.status)}</span>{item.dueDate ? <><span aria-hidden="true">·</span><span>{displayDueDate(item.dueDate, locale)}</span></> : null}</div></div><ArrowUpRight className="h-4 w-4 shrink-0 text-muted-foreground" /></Link>)}</div>}
        </CardContent>
      </Card>
    </div>
  );
}
