import * as React from "react";
import { AlertTriangle } from "lucide-react";
import { Link } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { useRisks, type RiskDto, type RiskLevel } from "./api/risks";

type MatrixRisk = RiskDto & { impactScore: number; likelihoodScore: number; score: number };
type MatrixCell = { risks: MatrixRisk[] };

const levelFallback: Record<RiskLevel, number> = { Low: 1, Medium: 3, High: 4, Critical: 5 };

function toScore(value: string | undefined, fallback: number) {
  const normalized = value?.trim().toLowerCase() ?? "";
  const numeric = Number.parseInt(normalized, 10);
  if (Number.isFinite(numeric) && numeric >= 1 && numeric <= 5) return numeric;
  if (["very low", "low", "rare", "minor"].includes(normalized)) return 1;
  if (["medium", "moderate", "possible"].includes(normalized)) return 3;
  if (["high", "likely", "major"].includes(normalized)) return 4;
  if (["critical", "very high", "almost certain", "severe"].includes(normalized)) return 5;
  return fallback;
}

function cellClass(count: number) {
  if (count === 0) return "bg-muted";
  if (count <= 1) return "bg-status-green/40";
  if (count <= 3) return "bg-status-amber/40";
  return "bg-status-red/50";
}

export function RiskMatrixPage() {
  const { t } = useI18n();
  const query = useRisks({}, true);
  const risks = React.useMemo<MatrixRisk[]>(() => (query.data?.items ?? []).map((risk) => {
    const fallback = levelFallback[risk.riskLevel];
    const impactScore = toScore(risk.impact, fallback);
    const likelihoodScore = toScore(risk.likelihood, fallback);
    return { ...risk, impactScore, likelihoodScore, score: impactScore * likelihoodScore };
  }), [query.data]);
  const matrix = React.useMemo(() => {
    const result: MatrixCell[][] = Array.from({ length: 5 }, () => Array.from({ length: 5 }, () => ({ risks: [] })));
    for (const risk of risks) result[risk.impactScore - 1][risk.likelihoodScore - 1].risks.push(risk);
    return result;
  }, [risks]);
  const highRisks = React.useMemo(() => risks.filter((risk) => risk.score >= 15).sort((left, right) => right.score - left.score), [risks]);

  return <div className="space-y-6">
    <PageHeader title={t("risks.matrixTitle")} description={t("risks.matrixDescription")} />
    {query.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : null}
    {query.isError ? <p role="alert" className="py-12 text-center text-sm text-destructive">{t("risks.loadError")}</p> : null}
    {!query.isLoading && !query.isError ? <div className="grid gap-6 lg:grid-cols-5">
      <Card className="lg:col-span-3">
        <CardHeader><CardTitle className="text-lg">{t("risks.heatMapTitle")}</CardTitle><CardDescription>{t("risks.heatMapDescription")}</CardDescription></CardHeader>
        <CardContent>
          <div className="space-y-1">
            <div className="flex items-center gap-2 text-xs text-muted-foreground"><div className="w-20 text-end">{t("risks.impactAxis")}</div><div className="flex-1" /><div className="flex flex-1 justify-center text-[10px]">{t("risks.likelihoodAxis")}</div></div>
            {matrix.map((row, impactIndex) => <div key={impactIndex} className="flex items-center gap-2 py-0.5">
              <div className="w-20 text-end text-xs text-muted-foreground">{impactIndex + 1}</div>
              <div className="flex flex-1 gap-1">{row.map((cell, likelihoodIndex) => <div key={likelihoodIndex} className={`flex h-12 flex-1 items-center justify-center rounded text-xs font-medium transition-opacity hover:opacity-80 ${cellClass(cell.risks.length)}`} title={`${t("risks.impactAxis")}: ${impactIndex + 1} · ${t("risks.likelihoodAxis")}: ${likelihoodIndex + 1} · ${t("risks.score")}: ${(impactIndex + 1) * (likelihoodIndex + 1)}`}>{cell.risks.length || ""}</div>)}</div>
            </div>)}
            <div className="flex items-center gap-2 pt-1"><div className="w-20" /><div className="flex flex-1 gap-1">{[1, 2, 3, 4, 5].map((value) => <div key={value} className="flex-1 text-center text-[10px] text-muted-foreground">{value}</div>)}</div></div>
            <div className="mt-4 flex flex-wrap items-center gap-3 text-xs text-muted-foreground"><span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-status-green/40" />{t("risks.lowRisk")}</span><span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-status-amber/40" />{t("risks.mediumRisk")}</span><span className="flex items-center gap-1"><span className="h-3 w-3 rounded bg-status-red/50" />{t("risks.highRisk")}</span></div>
            <p className="pt-2 text-xs text-muted-foreground">{t("risks.matrixFallbackNote")}</p>
          </div>
        </CardContent>
      </Card>
      <Card className="lg:col-span-2">
        <CardHeader><CardTitle className="text-lg">{t("risks.highRiskItems")}</CardTitle><CardDescription>{t("risks.highRiskDescription")}</CardDescription></CardHeader>
        <CardContent className="space-y-3">{highRisks.length ? highRisks.map((risk) => <div key={risk.id} className="flex items-start gap-2 border-b pb-2 last:border-0"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-status-red" /><div><Link to={`/risks-issues/risks/${encodeURIComponent(risk.id)}`} className="text-sm font-medium hover:underline">{risk.title}</Link><p className="text-xs text-muted-foreground">{risk.projectName || "—"} · {t("risks.score")}: {risk.score} · {risk.riskLevel}</p></div></div>) : <p className="py-6 text-center text-sm text-muted-foreground">{t("risks.noHighRisks")}</p>}</CardContent>
      </Card>
    </div> : null}
  </div>;
}
