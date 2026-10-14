import * as React from "react";
import { ArrowUpRight, FolderKanban } from "lucide-react";
import { Link } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { getRiskLevelTextClass, getRiskStatusTextClass } from "@lib/status-presentation";
import { useRiskDashboard, type RiskDto } from "./api/risks";

const levels = ["Critical", "High", "Medium", "Low"] as const;
const statuses = ["Identified", "ActionRequired", "FollowUp", "Mitigated", "Closed"] as const;
const levelPriority: Record<string, number> = { critical: 4, high: 3, medium: 2, low: 1 };
const levelBarClasses: Record<(typeof levels)[number], string> = {
  Critical: "bg-red-500",
  High: "bg-orange-500",
  Medium: "bg-amber-500",
  Low: "bg-emerald-500",
};
const statusBarClasses: Record<(typeof statuses)[number], string> = {
  Identified: "bg-blue-500",
  ActionRequired: "bg-amber-500",
  FollowUp: "bg-cyan-500",
  Mitigated: "bg-emerald-500",
  Closed: "bg-slate-500",
};

function normalizeRiskValue(value: string | null | undefined): string {
  return (value ?? "").replace(/[\s_-]/g, "").toLowerCase();
}

function enumLabel(t: (key: string) => string, namespace: "enums.riskLevel" | "enums.riskStatus", value: string | null | undefined): string {
  if (!value) return "—";
  const values = namespace === "enums.riskLevel" ? levels : statuses;
  const canonical = values.find((candidate) => normalizeRiskValue(candidate) === normalizeRiskValue(value)) ?? value;
  const suffix = `${canonical.charAt(0).toLowerCase()}${canonical.slice(1)}`;
  const key = `${namespace}.${suffix}`;
  const label = t(key);
  return label === key ? value : label;
}

export function RiskDashboardPage() {
  const { t, locale } = useI18n();
  const query = useRiskDashboard();
  const risks = query.data?.items ?? [];
  const count = (field: "riskLevel" | "status", value: string) => risks.filter((risk) => normalizeRiskValue(risk[field]) === normalizeRiskValue(value)).length;
  const top = [...risks].sort((left, right) => (levelPriority[normalizeRiskValue(right.riskLevel)] ?? 0) - (levelPriority[normalizeRiskValue(left.riskLevel)] ?? 0)).slice(0, 5);
  const recent = [...risks]
    .filter((risk) => risk.lastActionUpdateAt)
    .sort((left, right) => new Date(right.lastActionUpdateAt ?? 0).getTime() - new Date(left.lastActionUpdateAt ?? 0).getTime())
    .slice(0, 5);
  const dateTimeFormat = React.useMemo(() => new Intl.DateTimeFormat(locale === "ar" ? "ar-EG" : "en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }), [locale]);

  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError) return <p className="py-12 text-center text-sm text-destructive">{t("risks.loadError")}</p>;

  const levelValues = levels.map((value) => ({
    value,
    label: enumLabel(t, "enums.riskLevel", value),
    count: count("riskLevel", value),
    textClass: getRiskLevelTextClass(value),
    barClass: levelBarClasses[value],
  }));
  const statusValues = statuses.map((value) => ({
    value,
    label: enumLabel(t, "enums.riskStatus", value),
    count: count("status", value),
    textClass: getRiskStatusTextClass(value),
    barClass: statusBarClasses[value],
  }));

  return <div className="space-y-6">
    <PageHeader title={t("risks.dashboardTitle")} description={t("risks.dashboardDescription")} actions={<Link to="/risks-issues/risks"><Button size="sm">{t("risks.riskRegister")}</Button></Link>} />
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
      <Kpi label={t("risks.totalRisks")} value={risks.length} />
      <Kpi label={t("risks.critical")} value={count("riskLevel", "Critical")} />
      <Kpi label={t("risks.high")} value={count("riskLevel", "High")} />
      <Kpi label={t("risks.actionRequired")} value={count("status", "ActionRequired")} />
      <Kpi label={t("risks.followUp")} value={count("status", "FollowUp")} />
      <Kpi label={t("risks.mitigated")} value={count("status", "Mitigated")} />
    </div>
    <div className="grid gap-5 xl:grid-cols-2">
      <Distribution title={t("risks.risksByLevel")} values={levelValues} />
      <Distribution title={t("risks.risksByStatus")} values={statusValues} />
    </div>
    <div className="grid gap-5 xl:grid-cols-2">
      <RiskList title={t("risks.topRisks")} items={top} t={t} />
      <Card>
        <CardHeader><CardTitle className="text-base">{t("risks.recentUpdates")}</CardTitle></CardHeader>
        <CardContent>
          {recent.length ? <div className="divide-y">{recent.map((risk) => <Link key={risk.id} to={`/risks-issues/risks/${encodeURIComponent(risk.id)}?from=risks`} className="block py-3 first:pt-0 last:pb-0">
            <div className="flex items-center justify-between gap-2">
              <span className="truncate text-sm font-medium">{risk.title}</span>
              <time dateTime={risk.lastActionUpdateAt} className="shrink-0 text-xs text-muted-foreground">{dateTimeFormat.format(new Date(risk.lastActionUpdateAt ?? risk.createdAt))}</time>
            </div>
            <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{risk.lastActionUpdateText ?? risk.description}</p>
            <span className="mt-1 inline-flex max-w-full items-center gap-1.5 truncate text-xs font-medium text-blue-700 dark:text-blue-300"><FolderKanban className="h-3 w-3 shrink-0" />{risk.projectName || "—"}</span>
          </Link>)}</div> : <p className="text-sm text-muted-foreground">{t("risks.noRecentUpdates")}</p>}
        </CardContent>
      </Card>
    </div>
  </div>;
}

function Kpi({ label, value }: { label: string; value: number }) {
  return <Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 text-2xl font-semibold tabular-nums">{value}</p></CardContent></Card>;
}

function Distribution({ title, values }: { title: string; values: { value: string; label: string; count: number; textClass: string; barClass: string }[] }) {
  const max = Math.max(1, ...values.map(({ count: value }) => value));
  return <Card><CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader><CardContent className="space-y-4">{values.map(({ value, label, count: valueCount, textClass, barClass }) => <div key={value} className="space-y-1">
    <div className="flex justify-between text-xs"><span className={`font-medium ${textClass}`}>{label}</span><span>{valueCount}</span></div>
    <div className="h-2 rounded-full bg-muted"><div className={`h-full rounded-full ${barClass}`} style={{ width: `${(valueCount / max) * 100}%` }} /></div>
  </div>)}</CardContent></Card>;
}

function RiskList({ title, items, t }: { title: string; items: RiskDto[]; t: (key: string) => string }) {
  return <Card><CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader><CardContent>{items.length ? <div className="divide-y">{items.map((risk) => <Link key={risk.id} to={`/risks-issues/risks/${encodeURIComponent(risk.id)}?from=risks`} className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0">
    <div className="min-w-0"><p className="truncate text-sm font-semibold">{risk.title}</p><span className="mt-1 inline-flex max-w-full items-center gap-1.5 truncate text-xs font-medium text-blue-700 dark:text-blue-300"><FolderKanban className="h-3 w-3 shrink-0" />{risk.projectName || "—"}</span></div>
    <div className="flex shrink-0 items-center gap-2"><span className={`text-xs font-semibold ${getRiskLevelTextClass(risk.riskLevel)}`}>{enumLabel(t, "enums.riskLevel", risk.riskLevel)}</span><ArrowUpRight className="h-3.5 w-3.5 text-muted-foreground" /></div>
  </Link>)}</div> : <p className="text-sm text-muted-foreground">{t("risks.noTopRisks")}</p>}</CardContent></Card>;
}
