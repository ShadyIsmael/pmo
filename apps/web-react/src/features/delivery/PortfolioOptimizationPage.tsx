import * as React from "react";
import { BarChart3, Lightbulb, TrendingUp } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { managementCacheScopeKey, useManagement } from "@api/management";
import { useManagedPortfolios } from "./api/delivery";

export function PortfolioOptimizationPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { context, loading: managementLoading } = useManagement();
  const cacheScope = managementCacheScopeKey(user?.id, context);
  const portfoliosQuery = useManagedPortfolios("", !managementLoading, cacheScope);
  const portfolios = React.useMemo(() => portfoliosQuery.data?.items ?? [], [portfoliosQuery.data]);
  const totalProjects = portfolios.reduce((total, portfolio) => total + portfolio.totalProjectCount, 0);
  const projectsRequiringAttention = portfolios.reduce((total, portfolio) => total + portfolio.projectsRequiringAttention + portfolio.programsRequiringAttention, 0);

  return <div className="space-y-6">
    <PageHeader title={t("portfolioOptimization.title")} description={t("portfolioOptimization.description")} actions={<Button size="sm" disabled title={t("portfolioOptimization.unavailableSource")}><Lightbulb className="me-2 h-4 w-4" />{t("portfolioOptimization.runOptimization")}</Button>} />
    {managementLoading || portfoliosQuery.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : null}
    {portfoliosQuery.isError ? <p role="alert" className="py-12 text-center text-sm text-destructive">{t("portfolioOptimization.loadError")}</p> : null}
    {!managementLoading && !portfoliosQuery.isLoading && !portfoliosQuery.isError ? <>
      <div className="grid gap-4 md:grid-cols-2">
        <MetricCard title={t("portfolioOptimization.roi")} value="—" hint={t("portfolioOptimization.roiUnavailable")} icon={<TrendingUp className="h-4 w-4 text-muted-foreground" />} />
        <MetricCard title={t("portfolioOptimization.optimizationScore")} value="—" hint={t("portfolioOptimization.optimizerUnavailable")} icon={<BarChart3 className="h-4 w-4 text-muted-foreground" />} />
      </div>
      <Card><CardHeader><CardTitle className="text-base">{t("portfolioOptimization.livePortfolioSignals")}</CardTitle><CardDescription>{t("portfolioOptimization.livePortfolioSignalsDescription")}</CardDescription></CardHeader><CardContent className="grid gap-3 sm:grid-cols-3"><Signal label={t("portfolioOptimization.portfolios")} value={portfolios.length} /><Signal label={t("portfolioOptimization.projects")} value={totalProjects} /><Signal label={t("portfolioOptimization.attentionItems")} value={projectsRequiringAttention} /></CardContent></Card>
      <Tabs defaultValue="scenarios">
        <TabsList>
          <TabsTrigger value="scenarios">{t("portfolioOptimization.scenarios")}</TabsTrigger>
          <TabsTrigger value="constraints">{t("portfolioOptimization.constraints")}</TabsTrigger>
        </TabsList>
        <TabsContent value="scenarios">
          <Card><CardHeader><CardTitle className="text-base">{t("portfolioOptimization.scenarios")}</CardTitle><CardDescription>{t("portfolioOptimization.scenariosDescription")}</CardDescription></CardHeader><CardContent className="space-y-3"><Badge variant="outline">{t("portfolioOptimization.unavailable")}</Badge><p className="text-sm text-muted-foreground">{t("portfolioOptimization.unavailableSource")}</p></CardContent></Card>
        </TabsContent>
        <TabsContent value="constraints">
          <Card><CardHeader><CardTitle className="text-base">{t("portfolioOptimization.constraints")}</CardTitle></CardHeader><CardContent className="space-y-3"><Badge variant="outline">{t("portfolioOptimization.unavailable")}</Badge><p className="text-sm text-muted-foreground">{t("portfolioOptimization.constraintsUnavailable")}</p></CardContent></Card>
        </TabsContent>
      </Tabs>
    </> : null}
  </div>;
}

function MetricCard({ title, value, hint, icon }: { title: string; value: string; hint: string; icon: React.ReactNode }) {
  return <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>{icon}</CardHeader><CardContent><div className="text-2xl font-bold">{value}</div><p className="text-xs text-muted-foreground">{hint}</p></CardContent></Card>;
}

function Signal({ label, value }: { label: string; value: number }) {
  return <div><p className="text-xs text-muted-foreground">{label}</p><p className="text-2xl font-semibold">{value}</p></div>;
}
