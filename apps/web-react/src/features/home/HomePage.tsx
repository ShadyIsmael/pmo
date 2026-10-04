import { useHomeSummary } from "@api/home";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { KpiCard } from "@components/ui/KpiCard";
import { ProjectHealthChart } from "@components/ui/ProjectHealthChart";
import { TopRisksWidget } from "@components/ui/TopRisksWidget";
import { AttentionRequiredWidget } from "@components/ui/AttentionRequiredWidget";
import { BudgetOverviewWidget } from "@components/ui/BudgetOverviewWidget";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Skeleton } from "@components/ui/Skeleton";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";

export function HomePage() {
  const { t } = useI18n();
  const { isAuthenticated } = useAuth();
  const { loading: managementLoading } = useManagement();
  // Visible-first: Home summary loads only for authenticated sessions on this route.
  const query = useHomeSummary(isAuthenticated && !managementLoading);

  if (query.isLoading) {
    return (
      <div className="space-y-4" role="status" aria-label={t("common.loading")}>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-24" />)}
        </div>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3].map((i) => <Skeleton key={i} className="h-64" />)}
        </div>
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="py-12 text-center" role="alert">
        <p className="text-destructive">{t("dashboard.loadFailed")}</p>
        <p className="mt-1 text-sm text-muted-foreground">{t("dashboard.loadFailedDescription")}</p>
        <Button className="mt-4" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button>
      </div>
    );
  }

  const data = query.data;
  const budget = (data?.budgetByCurrency ?? [])[0];

  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <KpiCard label={t("dashboard.activeProjects")} value={data?.activeProjectsCount ?? 0} icon="folder" />
        <KpiCard label={t("dashboard.projectsAtRisk")} value={data?.projectsAtRiskCount ?? 0} icon="alert-triangle" variant="destructive" />
        <KpiCard label={t("dashboard.openRisks")} value={data?.openRisksCount ?? 0} icon="alert-circle" />
        <KpiCard label={t("dashboard.critical")} value={data?.criticalOpenRisksCount ?? 0} icon="alert-triangle" variant="destructive" />
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader><CardTitle>{t("dashboard.riskDistribution")}</CardTitle></CardHeader>
          <CardContent>
            <ProjectHealthChart data={data?.projectHealthCounts} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>{t("dashboard.noAttention")}</CardTitle></CardHeader>
          <CardContent>
            <AttentionRequiredWidget projects={data?.attentionProjects ?? []} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>{t("dashboard.noRisks")}</CardTitle></CardHeader>
          <CardContent>
            <TopRisksWidget risks={data?.topRisks ?? []} />
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>{t("dashboard.totalBudget")}</CardTitle></CardHeader>
          <CardContent>
            <BudgetOverviewWidget data={budget ? { approved: budget.budget, actual: budget.actualCost, currency: budget.currency } : undefined} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>{t("dashboard.utilization")}</CardTitle></CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">{t("dashboard.noFinancialData")}</p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
