"use client";

import { useState } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { useTranslations, useLocale } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { KpiCard } from "@/components/ui/kpi-card";
import { ErrorState } from "@/components/ui/state-display";
import { Progress } from "@/components/ui/progress";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Plus, Download, FolderKanban, Banknote, AlertTriangle, Clock, ArrowUpRight,
  FileSpreadsheet, FileText, Loader2, Filter, ChevronDown,
} from "lucide-react";
import { formatDate, getHealthDotColor, getStatusVariant } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useProjects, type ProjectSummaryDto } from "@/hooks/projects/useProjects";
import {
  CreateProjectSheetProvider,
  useCreateProjectSheet,
} from "@/components/projects/CreateProjectSheetProvider";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { useCurrencyFormatter } from "@/hooks/useCurrencyFormatter";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";

// The advanced search form is collapsed by default, so lazy-load it to keep its
// form/select graph out of the initial route chunk. The fallback mirrors the
// collapsed header so there is no layout shift.
function AdvancedSearchFallback({ activeFilterCount }: { activeFilterCount: number }) {
  const common = useTranslations("common");
  return (
    <Card>
      <CardHeader className="cursor-pointer transition-colors hover:bg-muted/50">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Filter className="h-4 w-4 text-muted-foreground" />
            <CardTitle className="text-base">{common("advancedSearch") || "Advanced Search"}</CardTitle>
            {activeFilterCount > 0 && <Badge variant="secondary" className="text-xs">{activeFilterCount}</Badge>}
          </div>
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        </div>
      </CardHeader>
    </Card>
  );
}

const LazyProjectAdvancedSearch = dynamic(
  () => import("@/components/projects/ProjectAdvancedSearch").then((mod) => mod.ProjectAdvancedSearch),
  { ssr: false, loading: () => <AdvancedSearchFallback activeFilterCount={0} /> }
);

const STATUS_TAB_VALUES = [
  "all",
  "active",
  "atRisk",
  "delayed",
  "onHold",
  "completed",
  "cancelled",
] as const;

type StatusTab = (typeof STATUS_TAB_VALUES)[number];

const STATUS_TABS: ReadonlyArray<{ value: StatusTab; translationKey: string }> = STATUS_TAB_VALUES.map((value) => ({
  value,
  translationKey: `statusTabs.${value}`,
}));

function isStatusTab(value: string): value is StatusTab {
  return (STATUS_TAB_VALUES as readonly string[]).includes(value);
}

function statusMatches(project: ProjectSummaryDto, tab: StatusTab): boolean {
  switch (tab) {
    case "all":
    case "active":
      return project.status !== "Completed" && project.status !== "Cancelled";
    case "atRisk":
      return project.status !== "Completed" && project.status !== "Cancelled" && project.health === "Amber";
    case "delayed":
      return project.status !== "Completed" && project.status !== "Cancelled" && project.health === "Red";
    case "onHold":
      return project.status === "OnHold";
    case "completed":
      return project.status === "Completed";
    case "cancelled":
      return project.status === "Cancelled";
  }
}

function ProjectsPageContent() {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const locale = useLocale();
  const { toast } = useToast();
  const { user } = useAuth();
  const canCreateProject = Boolean(user && hasPermission(user.permissions, Permissions.Project.Create));
  const [filterStatus, setFilterStatus] = useState<StatusTab>("all");
  const [exporting, setExporting] = useState<"excel" | "pdf" | null>(null);

  const [filterName, setFilterName] = useState("");
  const [filterCode, setFilterCode] = useState("");
  const [filterStatuses, setFilterStatuses] = useState<string[]>([]);
  const [filterHealths, setFilterHealths] = useState<string[]>([]);
  const [filterProgressMin, setFilterProgressMin] = useState("");
  const [filterProgressMax, setFilterProgressMax] = useState("");
  const [filterBudgetMin, setFilterBudgetMin] = useState("");
  const [filterBudgetMax, setFilterBudgetMax] = useState("");
  const [filterForecastMin, setFilterForecastMin] = useState("");
  const [filterForecastMax, setFilterForecastMax] = useState("");
  const [filterProjectManager, setFilterProjectManager] = useState("");
  const [filterProjectManagerName, setFilterProjectManagerName] = useState("");
  const [filterDueDateFrom, setFilterDueDateFrom] = useState("");
  const [filterDueDateTo, setFilterDueDateTo] = useState("");
  const { openCreateProject } = useCreateProjectSheet();
  const selectedProjectManager = filterProjectManagerName ? { fullName: filterProjectManagerName } : undefined;

  const { data, isLoading, isError } = useProjects({
    page: 1,
    pageSize: 50,
  });

  // Use the new currency formatter
  const currencyFormatter = useCurrencyFormatter();
  const { statusLabel, healthLabel } = useProjectEnumLabels();

  const items = data?.items ?? [];

  const tabCounts = STATUS_TABS.reduce<Record<string, number>>((acc, tab) => {
    acc[tab.value] = items.filter((p) => statusMatches(p, tab.value)).length;
    return acc;
  }, {});

  const tabbed = items.filter((p) => statusMatches(p, filterStatus));

  const activeFilterCount =
    (filterName.trim() ? 1 : 0) +
    (filterCode.trim() ? 1 : 0) +
    (filterStatuses.length > 0 ? 1 : 0) +
    (filterHealths.length > 0 ? 1 : 0) +
    (filterProgressMin.trim() || filterProgressMax.trim() ? 1 : 0) +
    (filterBudgetMin.trim() || filterBudgetMax.trim() ? 1 : 0) +
    (filterForecastMin.trim() || filterForecastMax.trim() ? 1 : 0) +
    (filterProjectManager.trim() ? 1 : 0) +
    (filterDueDateFrom.trim() || filterDueDateTo.trim() ? 1 : 0);

  function handleClearFilters() {
    setFilterName("");
    setFilterCode("");
    setFilterStatuses([]);
    setFilterHealths([]);
    setFilterProgressMin("");
    setFilterProgressMax("");
    setFilterBudgetMin("");
    setFilterBudgetMax("");
    setFilterForecastMin("");
    setFilterForecastMax("");
    setFilterProjectManager("");
    setFilterProjectManagerName("");
    setFilterDueDateFrom("");
    setFilterDueDateTo("");
  }

  function toggleStatus(status: string) {
    setFilterStatuses(prev =>
      prev.includes(status)
        ? prev.filter(s => s !== status)
        : [...prev, status]
    );
  }

  function toggleHealth(health: string) {
    setFilterHealths(prev =>
      prev.includes(health)
        ? prev.filter(h => h !== health)
        : [...prev, health]
    );
  }

  const filtered = tabbed.filter((p: ProjectSummaryDto) => {
    if (filterName.trim() && !p.name.toLowerCase().includes(filterName.toLowerCase())) return false;
    if (filterCode.trim() && !p.code.toLowerCase().includes(filterCode.toLowerCase())) return false;
    if (filterStatuses.length > 0 && !filterStatuses.includes(p.status)) return false;
    if (filterHealths.length > 0 && p.health && !filterHealths.includes(p.health)) return false;
    if (filterProgressMin.trim() && p.progress < parseFloat(filterProgressMin)) return false;
    if (filterProgressMax.trim() && p.progress > parseFloat(filterProgressMax)) return false;
    if (filterBudgetMin.trim() && (p.budget || 0) < parseFloat(filterBudgetMin)) return false;
    if (filterBudgetMax.trim() && (p.budget || 0) > parseFloat(filterBudgetMax)) return false;
    if (filterForecastMin.trim() && (p.actualCost || 0) < parseFloat(filterForecastMin)) return false;
    if (filterForecastMax.trim() && (p.actualCost || 0) > parseFloat(filterForecastMax)) return false;
    if (filterProjectManager.trim() && p.projectManagerId !== filterProjectManager) return false;
    if (filterDueDateFrom.trim() && p.endDate && new Date(p.endDate) < new Date(filterDueDateFrom)) return false;
    if (filterDueDateTo.trim() && p.endDate && new Date(p.endDate) > new Date(filterDueDateTo)) return false;
    return true;
  });

  // Calculate totals using the currency formatter for the appropriate currency.
  // Summing across different currencies is misleading, so a total is only shown
  // when every project with an amount shares a single currency; otherwise "—".
  const currencyCode = (value?: string): string | undefined => (value?.trim() ? value : undefined);
  const budgetProjects = filtered.filter((p: ProjectSummaryDto) => typeof p.budget === "number" && Number.isFinite(p.budget));
  const forecastProjects = filtered.filter((p: ProjectSummaryDto) => typeof p.actualCost === "number" && Number.isFinite(p.actualCost));
  const budgetCurrencies = [...new Set(budgetProjects.map((p: ProjectSummaryDto) => currencyCode(p.currency)).filter(Boolean))];
  const forecastCurrencies = [...new Set(forecastProjects.map((p: ProjectSummaryDto) => currencyCode(p.currency)).filter(Boolean))];
  const budgetCurrency = budgetCurrencies.length === 1 ? budgetCurrencies[0] : undefined;
  const forecastCurrency = forecastCurrencies.length === 1 ? forecastCurrencies[0] : undefined;
  const totalBudget = budgetProjects.length === 0 || budgetCurrencies.length !== 1 ? null : budgetProjects.reduce((s, p) => s + (p.budget ?? 0), 0);
  const totalForecast = forecastProjects.length === 0 || forecastCurrencies.length !== 1 ? null : forecastProjects.reduce((s, p) => s + (p.actualCost ?? 0), 0);

  const healthCounts = { green: 0, amber: 0, red: 0 };
  filtered.forEach((p: ProjectSummaryDto) => {
    const health = p.health?.toLowerCase() || "green";
    if (healthCounts.hasOwnProperty(health)) healthCounts[health as keyof typeof healthCounts]++;
  });

  async function handleExport(format: "excel" | "pdf") {
    if (exporting) return;
    setExporting(format);
    try {
      const { fetchFilteredProjects, generateProjectsExcel, generateProjectsPdf } = await import("@/lib/export/project-export");

      const tabFilter =
        filterStatus === "atRisk" ? { statuses: undefined as string[] | undefined, healths: ["Amber"] as string[] | undefined } :
        filterStatus === "delayed" ? { statuses: undefined, healths: ["Red"] } :
        filterStatus === "onHold" ? { statuses: ["OnHold"], healths: undefined } :
        filterStatus === "completed" ? { statuses: ["Completed"], healths: undefined } :
        filterStatus === "cancelled" ? { statuses: ["Cancelled"], healths: undefined } :
        { statuses: undefined, healths: undefined };

      const exportItems = await fetchFilteredProjects({
        name: filterName.trim() || undefined,
        code: filterCode.trim() || undefined,
        statuses: filterStatuses.length > 0 ? filterStatuses : tabFilter.statuses,
        healths: filterHealths.length > 0 ? filterHealths : tabFilter.healths,
        progressMin: filterProgressMin.trim() ? parseFloat(filterProgressMin) : undefined,
        progressMax: filterProgressMax.trim() ? parseFloat(filterProgressMax) : undefined,
        budgetMin: filterBudgetMin.trim() ? parseFloat(filterBudgetMin) : undefined,
        budgetMax: filterBudgetMax.trim() ? parseFloat(filterBudgetMax) : undefined,
        forecastMin: filterForecastMin.trim() ? parseFloat(filterForecastMin) : undefined,
        forecastMax: filterForecastMax.trim() ? parseFloat(filterForecastMax) : undefined,
        projectManager: selectedProjectManager?.fullName || undefined,
        dueDateFrom: filterDueDateFrom.trim() || undefined,
        dueDateTo: filterDueDateTo.trim() || undefined,
      });

      const headers = [
        common("project"),
        common("code"),
        common("status"),
        common("health"),
        common("progress"),
        common("budget"),
        common("forecast"),
        t("pm"),
        common("dueDate"),
      ];

      const rows = exportItems.map((p) => [
        p.name,
        p.code,
        statusLabel(p.status),
        healthLabel(p.health),
        `${p.progress}%`,
        // Use the currency formatter for the export
        currencyFormatter.format(p.budget, p.currency),
        currencyFormatter.format(p.actualCost, p.currency),
        p.projectManagerName || "—",
        p.endDate ? formatDate(p.endDate, locale) : "—",
      ]);

      if (format === "excel") {
        await generateProjectsExcel({ headers, rows });
      } else {
        const filterParts: string[] = [];
        if (filterStatus !== "all") {
          const activeTab = STATUS_TABS.find((f) => f.value === filterStatus);
          if (activeTab) filterParts.push(`${common("status")}: ${t(activeTab.translationKey)}`);
        }
        if (filterName.trim()) filterParts.push(`${common("project")}: "${filterName.trim()}"`);
        if (filterCode.trim()) filterParts.push(`${common("code")}: "${filterCode.trim()}"`);
        if (filterStatuses.length > 0) filterParts.push(`${common("status")}: ${filterStatuses.map(statusLabel).join(", ")}`);
        if (filterHealths.length > 0) filterParts.push(`${common("health")}: ${filterHealths.map(healthLabel).join(", ")}`);
        if (filterProgressMin.trim() || filterProgressMax.trim()) {
          const range = [filterProgressMin.trim() || "0", filterProgressMax.trim() || "100"].join(" - ");
          filterParts.push(`${common("progress")}: ${range}%`);
        }
        if (filterBudgetMin.trim() || filterBudgetMax.trim()) {
          const range = [filterBudgetMin.trim() || "0", filterBudgetMax.trim() || "∞"].join(" - ");
          filterParts.push(`${common("budget")}: ${range}`);
        }
        if (filterForecastMin.trim() || filterForecastMax.trim()) {
          const range = [filterForecastMin.trim() || "0", filterForecastMax.trim() || "∞"].join(" - ");
          filterParts.push(`${common("forecast")}: ${range}`);
        }
        if (selectedProjectManager) filterParts.push(`${t("pm")}: "${selectedProjectManager.fullName}"`);
        if (filterDueDateFrom.trim() || filterDueDateTo.trim()) {
          const range = [filterDueDateFrom.trim() || "∞", filterDueDateTo.trim() || "∞"].join(" - ");
          filterParts.push(`${common("dueDate")}: ${range}`);
        }

        await generateProjectsPdf({
          headers,
          rows,
          title: t("title"),
          exportDateLabel: common("exportDate"),
          exportDate: formatDate(new Date(), locale, { year: "numeric", month: "long", day: "numeric" }),
          filtersLabel: common("activeFilters"),
          filtersSummary: filterParts.join("  |  ") || common("noActiveFilters"),
          countLabel: common("total"),
          locale,
        });
      }
    } catch (error) {
      console.error("[projects:export]", error);
      toast({
        variant: "destructive",
        title: common("exportFailed"),
      });
    } finally {
      setExporting(null);
    }
  }

  if (isLoading) {
    return (
      <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-page nexus-projects space-y-6">
        <PageHeader title={t("title")} description={t("description")} />
        <div className="grid gap-4 md:grid-cols-4">
          {[1,2,3,4].map(i => (
            <div key={i} className="h-28 animate-pulse rounded-xl border bg-card" />
          ))}
        </div>
        <div className="nexus-table-surface"><Skeleton className="h-64" /></div>
      </div>
    );
  }

  if (isError) {
    return (
      <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-page nexus-projects space-y-6">
        <PageHeader title={t("title")} description={t("description")} />
        <ErrorState
          title={common("errorLoading") || "Failed to load projects"}
          onRetry={() => window.location.reload()}
        />
      </div>
    );
  }

  return (
    <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-page nexus-projects space-y-6">
      <PageHeader
        title={t("title")}
        description={t("description", { count: filtered.length })}
        actions={
          <div className="flex gap-2">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="sm" disabled={!!exporting}>
                  {exporting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
                  {exporting ? common("exportGenerating") : common("export")}
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>{common("exportAs")}</DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem disabled={!!exporting} onSelect={() => handleExport("excel")}>
                  {exporting === "excel" ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileSpreadsheet className="mr-2 h-4 w-4" />}
                  Excel
                </DropdownMenuItem>
                <DropdownMenuItem disabled={!!exporting} onSelect={() => handleExport("pdf")}>
                  {exporting === "pdf" ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileText className="mr-2 h-4 w-4" />}
                  PDF
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
            <Link href="/ai-workspace/plan-generator"><Button variant="outline" size="sm"><Plus className="mr-2 h-4 w-4" />AI Plan</Button></Link>
            {canCreateProject ? <Button size="sm" onClick={openCreateProject}><Plus className="mr-2 h-4 w-4" />{common("create")}</Button> : null}
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard
          label={t("activeProjects")}
          value={filtered.length}
          icon={FolderKanban}
          semantic="primary"
        />
        <KpiCard
          label={common("budget")}
          value={currencyFormatter.format(totalBudget, budgetCurrency)}
          hint={`${common("forecast")}: ${currencyFormatter.format(totalForecast, forecastCurrency)}`}
          icon={Banknote}
          semantic="navy"
        />
        <KpiCard
          label={t("criticalRisks")}
          value={0}
          hint={t("acrossAllProjects")}
          icon={AlertTriangle}
          semantic="red"
        />
        <KpiCard
          label={t("avgScheduleVariance")}
          value="—"
          hint={t("weightedAverage")}
          icon={Clock}
          semantic="amber"
        />
      </div>

      <Tabs value={filterStatus} onValueChange={(value) => { if (isStatusTab(value)) setFilterStatus(value); }}>
        <TabsList className="flex flex-wrap">
          {STATUS_TABS.map((tab) => (
            <TabsTrigger key={tab.value} value={tab.value}>
              {t(tab.translationKey)}
              <span className="ml-1.5 rounded-full bg-muted px-1.5 text-[10px] font-medium text-muted-foreground">{tabCounts[tab.value]}</span>
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <LazyProjectAdvancedSearch
        filterName={filterName}
        setFilterName={setFilterName}
        filterCode={filterCode}
        setFilterCode={setFilterCode}
        filterStatuses={filterStatuses}
        toggleStatus={toggleStatus}
        filterHealths={filterHealths}
        toggleHealth={toggleHealth}
        filterProgressMin={filterProgressMin}
        setFilterProgressMin={setFilterProgressMin}
        filterProgressMax={filterProgressMax}
        setFilterProgressMax={setFilterProgressMax}
        filterBudgetMin={filterBudgetMin}
        setFilterBudgetMin={setFilterBudgetMin}
        filterBudgetMax={filterBudgetMax}
        setFilterBudgetMax={setFilterBudgetMax}
        filterForecastMin={filterForecastMin}
        setFilterForecastMin={setFilterForecastMin}
        filterForecastMax={filterForecastMax}
        setFilterForecastMax={setFilterForecastMax}
        filterProjectManager={filterProjectManager}
        setFilterProjectManager={setFilterProjectManager}
        setFilterProjectManagerName={setFilterProjectManagerName}
        filterDueDateFrom={filterDueDateFrom}
        setFilterDueDateFrom={setFilterDueDateFrom}
        filterDueDateTo={filterDueDateTo}
        setFilterDueDateTo={setFilterDueDateTo}
        activeFilterCount={activeFilterCount}
        onClearFilters={handleClearFilters}
      />

      <Tabs defaultValue="table">
        <TabsList>
          <TabsTrigger value="table">{t("tableView")}</TabsTrigger>
          <TabsTrigger value="cards">{t("cardView")}</TabsTrigger>
        </TabsList>

        <TabsContent value="table">
          <div className="nexus-table-surface">
            <div className="nexus-table-header grid grid-cols-12 gap-2">
              <div className="col-span-3">{common("project")}</div>
              <div className="col-span-1">{common("code")}</div>
              <div className="col-span-1">{common("status")}</div>
              <div className="col-span-1">{common("health")}</div>
              <div className="col-span-1">{common("progress")}</div>
              <div className="col-span-1">{common("budget")}</div>
              <div className="col-span-1">{common("forecast")}</div>
              <div className="col-span-1">{t("pm")}</div>
              <div className="col-span-1">{common("dueDate")}</div>
              <div className="col-span-1"></div>
            </div>
            {filtered.length === 0 ? (
              <div className="px-4 py-12 text-center text-muted-foreground">{common("noResults")}</div>
            ) : (
              filtered.map((project: ProjectSummaryDto) => (
                <Link key={project.id} href={`/projects/${project.id}`} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
                  <div className="col-span-3 font-medium">{project.name}</div>
                  <div className="col-span-1 text-muted-foreground">{project.code}</div>
                  <div className="col-span-1"><Badge variant={getStatusVariant(project.status)} className="text-[10px]">{statusLabel(project.status)}</Badge></div>
                  <div className="col-span-1 flex items-center"><span className={`h-2.5 w-2.5 rounded-full ${getHealthDotColor(project.health)}`} /></div>
                  <div className="col-span-1 flex items-center gap-1">
                    <Progress value={project.progress} className="h-1.5 w-12" />
                    <span className="text-xs">{project.progress}%</span>
                  </div>
                  <div className="col-span-1 text-muted-foreground">{currencyFormatter.format(project.budget, project.currency)}</div>
                  <div className="col-span-1 text-muted-foreground">{currencyFormatter.format(project.actualCost, project.currency)}</div>
                  <div className="col-span-1">
                    <div className="flex items-center gap-1">
                      <Avatar className="h-5 w-5"><AvatarFallback className="text-[8px]">{project.projectManagerName?.charAt(0) || "?"}</AvatarFallback></Avatar>
                      <span className="text-xs text-muted-foreground">{project.projectManagerName?.split(" ")[1] || ""}</span>
                    </div>
                  </div>
                  <div className="col-span-1 text-muted-foreground text-xs">{project.endDate ? formatDate(project.endDate) : "-"}</div>
                  <div className="col-span-1 flex items-center"><ArrowUpRight className="h-3.5 w-3.5 text-muted-foreground" /></div>
                </Link>
              ))
            )}
          </div>
        </TabsContent>

        <TabsContent value="cards">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {filtered.length === 0 ? (
              <div className="col-span-full text-center text-muted-foreground py-12">{common("noResults")}</div>
            ) : (
              filtered.slice(0, 12).map((project: ProjectSummaryDto) => (
                <Link key={project.id} href={`/projects/${project.id}`}>
                  <Card className="h-full transition-colors hover:bg-muted/50">
                    <CardHeader>
                      <div className="flex items-start justify-between">
                        <div>
                          <CardTitle className="text-base">{project.name}</CardTitle>
                          <CardDescription>{project.code}</CardDescription>
                        </div>
                        <span className={`h-3 w-3 rounded-full ${getHealthDotColor(project.health)}`} />
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <div className="flex items-center gap-2">
                        <Badge variant={getStatusVariant(project.status)} className="text-[10px]">{statusLabel(project.status)}</Badge>
                      </div>
                      <Progress value={project.progress} className="h-2" />
                      <div className="grid grid-cols-2 gap-2 text-xs">
                        <div><span className="text-muted-foreground">{common("budget")}</span><p className="font-medium">{currencyFormatter.format(project.budget, project.currency)}</p></div>
                        <div><span className="text-muted-foreground">{common("forecast")}</span><p className="font-medium">{currencyFormatter.format(project.actualCost, project.currency)}</p></div>
                        <div><span className="text-muted-foreground">{t("pm")}</span><p className="font-medium">{project.projectManagerName || "N/A"}</p></div>
                        <div><span className="text-muted-foreground">{common("dueDate")}</span><p className="font-medium">{project.endDate ? formatDate(project.endDate) : "N/A"}</p></div>
                      </div>
                    </CardContent>
                  </Card>
                </Link>
              ))
            )}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}

export default function ProjectsPage() {
  return (
    <CreateProjectSheetProvider>
      <ProjectsPageContent />
    </CreateProjectSheetProvider>
  );
}
