import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { DashboardDataState, formatMoney, isClosedProject, useDashboardLabels, useDashboardProjects, useDashboardSummary, useDashboardWorkItems } from "../dashboard-data";

function ActiveProjectsKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => { const counts = summary.projectHealthCounts; return <div className="flex flex-col items-center justify-center h-full gap-3"><div className="text-4xl font-bold text-slate-900">{summary.activeProjectsCount}</div><div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t("activeProjects")}</div><div className="flex gap-4 mt-1 text-xs"><div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-emerald-500"></span><span className="text-slate-600">{counts.green ?? 0}</span></div><div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-amber-500"></span><span className="text-slate-600">{counts.amber ?? 0}</span></div><div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-red-500"></span><span className="text-slate-600">{counts.red ?? 0}</span></div>{(counts.unknown ?? 0) > 0 && <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-slate-400"></span><span className="text-slate-600">{counts.unknown}</span></div>}</div></div>; }}</DashboardDataState>;
}

function ProjectsAtRiskKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => { const atRisk = summary.projectsAtRiskCount; return <div className="flex flex-col items-center justify-center h-full gap-3"><div className="text-4xl font-bold text-amber-600">{atRisk}</div><div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t("projectsAtRisk")}</div><div className="mt-1 px-3 py-1 rounded-full bg-amber-50 border border-amber-200"><span className="text-xs font-medium text-amber-700">{summary.activeProjectsCount > 0 ? `${Math.round(atRisk / summary.activeProjectsCount * 100)}% ${t("ofActive")}` : t("notAvailable")}</span></div></div>; }}</DashboardDataState>;
}

function TotalBudgetKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t, locale } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => { const currencies = summary.budgetByCurrency; return <div className="flex flex-col items-center justify-center h-full gap-3"><div className="text-3xl font-bold text-slate-900">{currencies.length === 1 ? formatMoney(currencies[0]!.budget, currencies[0]!.currency, locale) : currencies.length > 1 ? t("multipleCurrencies") : "—"}</div><div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t("totalBudget")}</div>{currencies.length > 1 && <div className="text-[11px] text-slate-400 mt-1">{t("groupedByCurrency")}</div>}</div>; }}</DashboardDataState>;
}

function ForecastVarianceKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardProjects(); const { t, locale } = useDashboardLabels();
  return <DashboardDataState query={query}>{(page) => { const projects = page.items.filter((p) => !isClosedProject(p.status)); const currencies = new Set(projects.map((p) => p.currency)); const budget = currencies.size === 1 ? projects.reduce((s, p) => s + (p.budget ?? 0), 0) : 0; const actual = currencies.size === 1 ? projects.reduce((s, p) => s + (p.actualCost ?? 0), 0) : 0; const variance = budget > 0 ? ((actual - budget) / budget) * 100 : null; const isPositive = variance !== null && variance > 0; return <div className="flex flex-col items-center justify-center h-full gap-3"><div className={`text-4xl font-bold ${isPositive ? "text-red-600" : variance !== null ? "text-emerald-600" : "text-slate-900"}`}>{variance === null ? "—" : `${variance > 0 ? "+" : ""}${variance.toFixed(1)}%`}</div><div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t("costVariance")}</div><div className="text-xs text-slate-400 mt-1">{variance === null ? (currencies.size > 1 ? t("multipleCurrencies") : t("notAvailable")) : formatMoney(actual, [...currencies][0]!, locale)}</div></div>; }}</DashboardDataState>;
}

function OpenRisksKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => <div className="flex flex-col items-center justify-center h-full gap-3"><div className="text-4xl font-bold text-red-600">{summary.openRisksCount}</div><div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t("openRisks")}</div><div className="mt-1 px-3 py-1 rounded-full bg-red-50 border border-red-200"><span className="text-xs font-medium text-red-700">{summary.criticalOpenRisksCount} {t("critical")}</span></div></div>}</DashboardDataState>;
}

function OverdueTasksKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardWorkItems(); const { t } = useDashboardLabels(); const today = new Date().toISOString().slice(0, 10);
  return <DashboardDataState query={query}>{(page) => { const tasks = page.items.filter((item) => item.type === "Task" && item.dueDate && item.dueDate.slice(0, 10) < today && item.status.toLowerCase() !== "completed"); return <div className="flex flex-col items-center justify-center h-full gap-3"><div className="text-4xl font-bold text-red-600">{tasks.length}</div><div className="text-xs font-medium text-slate-500 uppercase tracking-wide">{t("overdueTasks")}</div><div className="mt-1 px-3 py-1 rounded-full bg-red-50 border border-red-200"><span className="text-xs font-medium text-red-700">{tasks.filter((task) => task.priority === "Critical").length} {t("critical")}</span></div></div>; }}</DashboardDataState>;
}

const common = { minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 }, defaultConfig: {}, configFields: [f.customTitle], dataSourceLabel: "Authoritative NEXUS read model" };
registerWidget("active-projects", { ...common, name: "Active Projects", description: "Authoritative active project count by health", category: "Overview", icon: "FolderKanban", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"] }, ActiveProjectsKPI);
registerWidget("projects-at-risk", { ...common, name: "Projects at Risk", description: "Active projects with an amber or red health signal", category: "Overview", icon: "AlertTriangle", supportedPOVs: ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "PMO Administrator"] }, ProjectsAtRiskKPI);
registerWidget("total-budget", { ...common, name: "Total Budget", description: "Active project budgets grouped by currency", category: "Overview", icon: "Banknote", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "Finance Controller"] }, TotalBudgetKPI);
registerWidget("forecast-variance", { ...common, name: "Cost Variance", description: "Actual cost compared with budget without currency conversion", category: "Overview", icon: "TrendingUp", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "Finance Controller"] }, ForecastVarianceKPI);
registerWidget("open-risks", { ...common, name: "Open Risks", description: "Open project-origin risks", category: "Risks", icon: "AlertTriangle", supportedPOVs: ["Executive", "Project Manager", "Program Manager", "Portfolio Manager"], configFields: [f.customTitle] }, OpenRisksKPI);
registerWidget("overdue-tasks", { ...common, name: "Overdue Tasks", description: "Open project work items past their due date", category: "My Work", icon: "Clock", supportedPOVs: ["Project Manager", "Program Manager", "Portfolio Manager", "Team Member"] }, OverdueTasksKPI);
