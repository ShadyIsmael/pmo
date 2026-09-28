"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Activity, ArrowUpRight, CheckCircle2, Clock3, FolderKanban, ShieldAlert, Target, TrendingUp } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useRiskDashboard } from "@/hooks/risks/useRisks";
import { RISK_LEVELS, RISK_STATUSES, RiskStatusHelper } from "@/lib/enums";
import { formatDateTime } from "@/lib/utils";
import { RiskLevelText } from "@/components/risks/RiskValueText";
import { riskDashboardKpiAccents, riskLevelAccents, riskStatusAccents } from "@/lib/risk-presentation";

const levelPriority: Record<string, number> = { Critical: 4, High: 3, Medium: 2, Low: 1 };

function normalizeRiskValue(value: string | null | undefined): string {
  return (value ?? "").replace(/[\s_-]/g, "").toLowerCase();
}

export default function RiskDashboardPage() {
  const t = useTranslations("risks");
  const enums = useTranslations("enums");
  const locale = useLocale();
  const query = useRiskDashboard();
  const risks = query.data?.items ?? [];
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  const levelCounts = RISK_LEVELS.reduce<Record<string, number>>((counts, level) => {
    counts[normalizeRiskValue(level)] = risks.filter((risk) => normalizeRiskValue(risk.riskLevel) === normalizeRiskValue(level)).length;
    return counts;
  }, {});
  const statusCounts = RISK_STATUSES.reduce<Record<string, number>>((counts, status) => {
    counts[normalizeRiskValue(status)] = risks.filter((risk) => normalizeRiskValue(risk.status) === normalizeRiskValue(status)).length;
    return counts;
  }, {});
  const countLevel = (value: string) => levelCounts[normalizeRiskValue(value)] ?? 0;
  const countStatus = (value: string) => statusCounts[normalizeRiskValue(value)] ?? 0;
  const totalRisks = risks.length;
  const topRisks = [...risks].sort((left, right) => (levelPriority[right.riskLevel] ?? 0) - (levelPriority[left.riskLevel] ?? 0)).slice(0, 5);
  const recentRisks = [...risks].filter((risk) => risk.lastActionUpdateAt).sort((left, right) => new Date(right.lastActionUpdateAt ?? 0).getTime() - new Date(left.lastActionUpdateAt ?? 0).getTime()).slice(0, 5);
  const maxLevelCount = Math.max(1, ...RISK_LEVELS.map(countLevel));
  const maxStatusCount = Math.max(1, ...RISK_STATUSES.map(countStatus));

  if (query.isLoading) return <DashboardSkeleton />;
  if (query.isError) return <Card><CardContent className="py-12 text-center text-sm text-destructive">{t("loadError")}</CardContent></Card>;

  return (
    <div className="space-y-6">
      <PageHeader title={t("dashboardTitle")} description={t("dashboardDescription")} actions={<Link href={`/${locale}/risks-issues/risks`}><Button size="sm">{t("riskRegister")}</Button></Link>} />

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <KpiCard label={t("totalRisks")} value={totalRisks} icon={ShieldAlert} accent={riskDashboardKpiAccents.total} />
        <KpiCard label={t("critical")} value={countLevel("Critical")} icon={Target} accent={riskDashboardKpiAccents.critical} />
        <KpiCard label={t("high")} value={countLevel("High")} icon={TrendingUp} accent={riskDashboardKpiAccents.high} />
        <KpiCard label={t("actionRequired")} value={countStatus("ActionRequired")} icon={Activity} accent={riskDashboardKpiAccents.actionRequired} />
        <KpiCard label={t("followUp")} value={countStatus("FollowUp")} icon={Clock3} accent={riskDashboardKpiAccents.followUp} />
        <KpiCard label={t("mitigated")} value={countStatus("Mitigated")} icon={CheckCircle2} accent={riskDashboardKpiAccents.mitigated} />
      </section>

      <section className="grid gap-5 xl:grid-cols-2">
        <DistributionCard title={t("risksByLevel")} items={RISK_LEVELS.map((level) => ({ value: level, label: <RiskLevelText value={level} />, count: countLevel(level), width: (countLevel(level) / maxLevelCount) * 100, accent: riskLevelAccents[level].bar }))} />
        <DistributionCard title={t("risksByStatus")} items={RISK_STATUSES.map((status) => ({ value: status, label: enums(RiskStatusHelper.getLabelKey(status) ?? "unknown"), count: countStatus(status), width: (countStatus(status) / maxStatusCount) * 100, accent: riskStatusAccents[status].bar }))} />
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base">{t("topRisks")}</CardTitle>
            <Link href={`/${locale}/risks-issues/risks`} className="text-xs font-medium text-primary hover:underline">{t("riskRegister")}</Link>
          </CardHeader>
          <CardContent className="pt-0">
            {topRisks.length ? <div className="divide-y divide-border/60">{topRisks.map((risk) => <Link key={risk.id} href={`/${locale}/risks-issues/risks/${risk.id}?from=risks`} className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0 hover:bg-muted/20"><div className="min-w-0"><p className="truncate text-sm font-semibold">{risk.title}</p><span className="mt-1 inline-flex max-w-full items-center gap-1.5 truncate text-xs font-medium text-blue-700 hover:text-blue-800 dark:text-blue-300 dark:hover:text-blue-200"><FolderKanban className="h-3 w-3 shrink-0" />{risk.projectName || "—"}</span></div><div className="flex shrink-0 items-center gap-3"><RiskLevelText value={risk.riskLevel} /><ArrowUpRight className="h-3.5 w-3.5 text-muted-foreground" /></div></Link>)}</div> : <EmptyLine text={t("noTopRisks")} />}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0"><CardTitle className="text-base">{t("recentUpdates")}</CardTitle><Clock3 className="h-4 w-4 text-muted-foreground" /></CardHeader>
          <CardContent className="pt-0">
            {recentRisks.length ? <div className="divide-y divide-border/60">{recentRisks.map((risk) => <Link key={risk.id} href={`/${locale}/risks-issues/risks/${risk.id}?from=risks`} className="block py-3 first:pt-0 last:pb-0 hover:bg-muted/20"><div className="flex items-center justify-between gap-3"><p className="truncate text-sm font-medium">{risk.title}</p><span className="shrink-0 text-[11px] text-muted-foreground">{formatDateTime(risk.lastActionUpdateAt, dateLocale)}</span></div><p className="mt-1 line-clamp-2 text-xs leading-5 text-muted-foreground">{risk.lastActionUpdateText}</p><span className="mt-1 inline-flex max-w-full items-center gap-1.5 truncate text-xs font-medium text-blue-700 dark:text-blue-300"><FolderKanban className="h-3 w-3 shrink-0" />{risk.projectName || "—"}</span></Link>)}</div> : <EmptyLine text={t("noRecentUpdates")} />}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

function KpiCard({ label, value, icon: Icon, accent }: { label: string; value: number; icon: typeof ShieldAlert; accent: { border: string; icon: string } }) {
  return <Card className={`overflow-hidden border-s-4 ${accent.border}`}><CardContent className="flex items-center justify-between gap-3 p-4"><div><p className="text-xs font-medium text-muted-foreground">{label}</p><p className="mt-1 text-2xl font-bold tracking-tight">{value}</p></div><div className={`rounded-xl p-2.5 ${accent.icon}`}><Icon className="h-5 w-5" /></div></CardContent></Card>;
}

function DistributionCard({ title, items }: { title: string; items: { value: string; label: ReactNode; count: number; width: number; accent: string }[] }) {
  return <Card><CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader><CardContent className="space-y-4 pt-0">{items.map((item) => <div key={item.value} className="space-y-1.5"><div className="flex items-center justify-between gap-3 text-xs"><span className="font-medium">{item.label}</span><span className="tabular-nums text-muted-foreground">{item.count}</span></div><div className="h-2 overflow-hidden rounded-full bg-muted"><div className={`h-full rounded-full transition-all ${item.accent}`} style={{ width: `${item.width}%` }} /></div></div>)}</CardContent></Card>;
}

function EmptyLine({ text }: { text: string }) { return <div className="flex min-h-28 items-center justify-center rounded-lg border border-dashed bg-muted/20 px-4 text-center text-sm text-muted-foreground">{text}</div>; }

function DashboardSkeleton() { return <div className="space-y-6"><Skeleton className="h-12 w-72" /><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">{Array.from({ length: 6 }, (_, index) => <Skeleton key={index} className="h-24" />)}</div><div className="grid gap-5 xl:grid-cols-2"><Skeleton className="h-64" /><Skeleton className="h-64" /></div><div className="grid gap-5 xl:grid-cols-2"><Skeleton className="h-72" /><Skeleton className="h-72" /></div></div>; }
