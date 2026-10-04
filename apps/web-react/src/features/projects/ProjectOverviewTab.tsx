import { useOutletContext } from "react-router-dom";
import { useI18n } from "@i18n/messages";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useProjectOverview } from "./api/projects";

export function ProjectOverviewTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t, formatCurrency } = useI18n();
  // Visible-first: overview loads only when this tab is active.
  const query = useProjectOverview(projectId, Boolean(projectId));

  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !query.data) return <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  const overview = query.data as unknown as { progress: number; health: string; openRisks: number; openIssues: number; budget?: { approved: number; actual: number; currency: string } };

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <Card><CardHeader><CardTitle>{t("projects.overallProgressLabel")}</CardTitle></CardHeader><CardContent><p className="text-3xl font-bold tabular-nums">{overview.progress}%</p></CardContent></Card>
      <Card><CardHeader><CardTitle>{t("projects.health")}</CardTitle></CardHeader><CardContent><p className="text-3xl font-bold capitalize">{overview.health}</p></CardContent></Card>
      <Card><CardHeader><CardTitle>{t("projects.openRisks")}</CardTitle></CardHeader><CardContent><p className="text-3xl font-bold tabular-nums">{overview.openRisks}</p></CardContent></Card>
      <Card><CardHeader><CardTitle>{t("projects.budget")}</CardTitle></CardHeader><CardContent><p className="text-3xl font-bold tabular-nums">{overview.budget ? formatCurrency(overview.budget.approved, overview.budget.currency) : "—"}</p></CardContent></Card>
    </div>
  );
}
