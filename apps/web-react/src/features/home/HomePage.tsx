import { AlertTriangle, ArrowUpRight, CheckCircle2, CircleAlert, RefreshCw } from "lucide-react";
import { useHomeSummary } from "../../api/home";
import { useManagement } from "../../api/management";
import { useI18n, formatNumber, formatPercent } from "../../i18n/messages";

function healthLabel(value: string, fallback: string): string { return value === "green" ? "Healthy" : value === "amber" ? "At risk" : value === "red" ? "Critical" : fallback; }
function tone(value: string): string { return value === "green" ? "good" : value === "amber" ? "warn" : value === "red" ? "bad" : "neutral"; }

export function HomePage() {
  const { t } = useI18n();
  const { loading: managementLoading } = useManagement();
  const query = useHomeSummary(!managementLoading);
  const data = query.data;
  if (query.isPending) return <div className="page-state" role="status"><div className="spinner" />{t("loading")}</div>;
  if (query.isError || !data) return <div className="page-state error-state" role="alert"><CircleAlert size={22} /><span>{t("loadError")}</span><button className="primary-button" onClick={() => query.refetch()} type="button"><RefreshCw size={15} />{t("retry")}</button></div>;
  const health = ["green", "amber", "red", "unknown"].map((key) => ({ key, count: data.projectHealthCounts[key] ?? 0 }));
  return <section className="home-page">
    <div className="page-heading"><div><p className="eyebrow">{t("dashboard")}</p><h1>{t("home")}</h1><p className="page-subtitle">{t("pilotScope")}</p></div><button className="secondary-button" type="button" onClick={() => query.refetch()}><RefreshCw size={15} />{t("refresh")}</button></div>
    <div className="kpi-grid">
      <Kpi title={t("activeProjects")} value={data.activeProjectsCount} icon={<CheckCircle2 size={18} />} tone="primary" />
      <Kpi title={t("projectsAtRisk")} value={data.projectsAtRiskCount} icon={<AlertTriangle size={18} />} tone="warning" />
      <Kpi title={t("openRisks")} value={data.openRisksCount} icon={<CircleAlert size={18} />} tone="indigo" />
      <Kpi title={t("criticalRisks")} value={data.criticalOpenRisksCount} icon={<AlertTriangle size={18} />} tone="danger" />
    </div>
    <div className="content-grid">
      <article className="panel"><div className="panel-heading"><h2>{t("health")}</h2><ArrowUpRight size={17} /></div><div className="health-bars">{health.map(({ key, count }) => <div className="health-row" key={key}><div className="health-label"><span className={`status-dot ${tone(key)}`} />{healthLabel(key, key)}<strong>{formatNumber(count)}</strong></div><div className="bar-track"><span className={`bar-fill ${tone(key)}`} style={{ width: `${data.activeProjectsCount ? Math.max(4, count / data.activeProjectsCount * 100) : 0}%` }} /></div></div>)}</div></article>
      <article className="panel"><div className="panel-heading"><h2>{t("budget")}</h2><ArrowUpRight size={17} /></div>{data.budgetByCurrency.length ? <div className="budget-list">{data.budgetByCurrency.map((item) => <div className="budget-row" key={item.currency}><span>{item.currency}</span><strong>{formatNumber(item.actualCost)} / {formatNumber(item.budget)}</strong></div>)}</div> : <p className="empty-text">{t("noBudget")}</p>}</article>
    </div>
    <div className="content-grid lower-grid">
      <article className="panel"><div className="panel-heading"><h2>{t("attention")}</h2><span className="panel-count">{formatNumber(data.attentionProjects.length)}</span></div>{data.attentionProjects.length ? <div className="data-list">{data.attentionProjects.map((project) => <div className="data-row" key={project.id}><div className="row-main"><span className={`status-dot ${tone(project.ragStatus.toLowerCase())}`} /><strong>{project.name}</strong></div><span className="progress-value">{formatPercent(project.progress)}</span></div>)}</div> : <p className="empty-text">{t("noAttention")}</p>}</article>
      <article className="panel"><div className="panel-heading"><h2>{t("topRisks")}</h2><span className="panel-count">{formatNumber(data.topRisks.length)}</span></div>{data.topRisks.length ? <div className="data-list">{data.topRisks.map((risk) => <div className="risk-row" key={risk.id}><CircleAlert size={16} /><div><strong>{risk.title}</strong><span>{risk.projectName} · {risk.status}</span></div><em className={tone(risk.riskLevel.toLowerCase())}>{risk.riskLevel}</em></div>)}</div> : <p className="empty-text">{t("noRisks")}</p>}</article>
    </div>
  </section>;
}

function Kpi({ title, value, icon, tone }: { title: string; value: number; icon: React.ReactNode; tone: string }) { return <article className={`kpi-card ${tone}`}><div className="kpi-icon">{icon}</div><p>{title}</p><strong>{formatNumber(value)}</strong></article>; }
