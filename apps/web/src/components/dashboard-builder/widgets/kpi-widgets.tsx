import { registerWidget, commonFields as f } from "../widget-registry";
import type { WidgetConfig } from "../dashboard-types";
import { DashboardDataState, formatMoney, isClosedProject, useDashboardLabels, useDashboardProjects, useDashboardSummary, useDashboardWorkItems } from "../dashboard-data";

function ActiveProjectsKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => { const counts = summary.projectHealthCounts; return <div className="flex flex-col items-center justify-center h-full gap-1"><span className="text-3xl font-bold">{summary.activeProjectsCount}</span><span className="text-xs text-muted-foreground">{t("activeProjects")}</span><div className="flex gap-3 mt-1 text-xs"><span className="text-success">● {counts.green ?? 0}</span><span className="text-warning">● {counts.amber ?? 0}</span><span className="text-destructive">● {counts.red ?? 0}</span>{(counts.unknown ?? 0) > 0 && <span className="text-muted-foreground">● {counts.unknown}</span>}</div></div>; }}</DashboardDataState>;
}

function ProjectsAtRiskKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => { const atRisk = summary.projectsAtRiskCount; return <div className="flex flex-col items-center justify-center h-full gap-1"><span className="text-3xl font-bold text-warning">{atRisk}</span><span className="text-xs text-muted-foreground">{t("projectsAtRisk")}</span><span className="text-xs text-muted-foreground">{summary.activeProjectsCount > 0 ? `${Math.round(atRisk / summary.activeProjectsCount * 100)}% ${t("ofActive")}` : t("notAvailable")}</span></div>; }}</DashboardDataState>;
}

function TotalBudgetKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t, locale } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => { const currencies = summary.budgetByCurrency; return <div className="flex flex-col items-center justify-center h-full gap-1"><span className="text-2xl font-bold">{currencies.length === 1 ? formatMoney(currencies[0]!.budget, currencies[0]!.currency, locale) : currencies.length > 1 ? t("multipleCurrencies") : "—"}</span><span className="text-xs text-muted-foreground">{t("totalBudget")}</span>{currencies.length > 1 && <span className="text-[10px] text-muted-foreground">{t("groupedByCurrency")}</span>}</div>; }}</DashboardDataState>;
}

function ForecastVarianceKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardProjects(); const { t, locale } = useDashboardLabels();
  return <DashboardDataState query={query}>{(page) => { const projects = page.items.filter((p) => !isClosedProject(p.status)); const currencies = new Set(projects.map((p) => p.currency)); const budget = currencies.size === 1 ? projects.reduce((s, p) => s + (p.budget ?? 0), 0) : 0; const actual = currencies.size === 1 ? projects.reduce((s, p) => s + (p.actualCost ?? 0), 0) : 0; const variance = budget > 0 ? ((actual - budget) / budget) * 100 : null; return <div className="flex flex-col items-center justify-center h-full gap-1"><span className="text-3xl font-bold">{variance === null ? "—" : `${variance > 0 ? "+" : ""}${variance.toFixed(1)}%`}</span><span className="text-xs text-muted-foreground">{t("costVariance")}</span><span className="text-xs text-muted-foreground">{variance === null ? (currencies.size > 1 ? t("multipleCurrencies") : t("notAvailable")) : formatMoney(actual, [...currencies][0]!, locale)}</span></div>; }}</DashboardDataState>;
}

function OpenRisksKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardSummary(); const { t } = useDashboardLabels();
  return <DashboardDataState query={query}>{(summary) => <div className="flex flex-col items-center justify-center h-full gap-1"><span className="text-3xl font-bold text-destructive">{summary.openRisksCount}</span><span className="text-xs text-muted-foreground">{t("openRisks")}</span><span className="text-xs text-destructive">{summary.criticalOpenRisksCount} {t("critical")}</span></div>}</DashboardDataState>;
}

function OverdueTasksKPI({ config: _config }: { config: WidgetConfig }) {
  const query = useDashboardWorkItems(); const { t } = useDashboardLabels(); const today = new Date().toISOString().slice(0, 10);
  return <DashboardDataState query={query}>{(page) => { const tasks = page.items.filter((item) => item.type === "Task" && item.dueDate && item.dueDate.slice(0, 10) < today && item.status.toLowerCase() !== "completed"); return <div className="flex flex-col items-center justify-center h-full gap-1"><span className="text-3xl font-bold text-destructive">{tasks.length}</span><span className="text-xs text-muted-foreground">{t("overdueTasks")}</span><span className="text-xs text-destructive">{tasks.filter((task) => task.priority === "Critical").length} {t("critical")}</span></div>; }}</DashboardDataState>;
}

const common = { minSize: { x: 0, y: 0, w: 2, h: 2 }, defaultSize: { x: 0, y: 0, w: 2, h: 2 }, maxSize: { x: 0, y: 0, w: 4, h: 3 }, defaultConfig: {}, configFields: [f.customTitle], dataSourceLabel: "Authoritative NEXUS read model" };
registerWidget("active-projects", { ...common, name: "Active Projects", description: "Authoritative active project count by health", category: "Overview", icon: "FolderKanban", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "PMO Administrator"] }, ActiveProjectsKPI);
registerWidget("projects-at-risk", { ...common, name: "Projects at Risk", description: "Active projects with an amber or red health signal", category: "Overview", icon: "AlertTriangle", supportedPOVs: ["Executive", "Project Manager", "Program Manager", "Portfolio Manager", "PMO Administrator"] }, ProjectsAtRiskKPI);
registerWidget("total-budget", { ...common, name: "Total Budget", description: "Active project budgets grouped by currency", category: "Overview", icon: "Banknote", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "Finance Controller"] }, TotalBudgetKPI);
registerWidget("forecast-variance", { ...common, name: "Cost Variance", description: "Actual cost compared with budget without currency conversion", category: "Overview", icon: "TrendingUp", supportedPOVs: ["Executive", "Program Manager", "Portfolio Manager", "Finance Controller"] }, ForecastVarianceKPI);
registerWidget("open-risks", { ...common, name: "Open Risks", description: "Open project-origin risks", category: "Risks", icon: "AlertTriangle", supportedPOVs: ["Executive", "Project Manager", "Program Manager", "Portfolio Manager"], configFields: [f.customTitle] }, OpenRisksKPI);
registerWidget("overdue-tasks", { ...common, name: "Overdue Tasks", description: "Open project work items past their due date", category: "My Work", icon: "Clock", supportedPOVs: ["Project Manager", "Program Manager", "Portfolio Manager", "Team Member"] }, OverdueTasksKPI);
