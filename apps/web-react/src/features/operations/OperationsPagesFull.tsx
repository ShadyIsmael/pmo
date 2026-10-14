import * as React from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { AlertCircle, ArrowUpRight, Beaker, Bot, CheckCircle2, Clock3, Download, Eye, FileText, Filter, Lock, Pencil, Plus, Search, Send, ShieldCheck, Users, XCircle } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Skeleton } from "@components/ui/Skeleton";
import { Textarea } from "@components/ui/Textarea";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Pagination } from "@components/ui/Pagination";
import { useI18n } from "@i18n/messages";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useAuditEntries } from "@api/audit";
import { auditActionLabel, auditEntityLabel, formatAuditChanges } from "@lib/audit-format";
import { useFullWorkItemInsights, useFullWorkItems, type WorkItemType } from "@api/workItemsFull";
import { useWorkItemStatuses, type WorkItemStatusDto } from "@api/workItems";
import { getStatusTextClass } from "@lib/status-presentation";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { MANAGEMENT_PERSPECTIVES } from "@lib/perspective";
import { useManagedPortfolios, usePortfolioManagementOverview, type PortfolioManagementListItem } from "@features/delivery/api/delivery";
import { useDebounce } from "use-debounce";

type Paged<T> = { items: T[]; totalCount: number; totalPages?: number };
type Report = { id: string; name: string; description?: string; category: string; outputFormat?: string; isSystem: boolean; isActive: boolean; createdAt?: string; updatedAt?: string };
type AIRequest = { id: string; title: string; context: string; provider: string; model?: string; status: string; requestedAt: string; completedAt?: string; result?: string; errorMessage?: string };

function Loading({ text }: { text: string }) { return <p className="py-12 text-center text-sm text-muted-foreground">{text}</p>; }
function ErrorState({ text }: { text: string }) { return <p className="py-12 text-center text-sm text-destructive">{text}</p>; }

export function ReportsPageFull() {
  const { t, formatDate } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const section = location.pathname.split("/")[2] ?? "";
  const isScheduled = section === "scheduled";
  const isPortfolio = section === "portfolio";
  const query = useQuery({ queryKey: ["reports", "definitions"], queryFn: () => api.get<Paged<Report>>(endpoints.reports.root, { page: 1, pageSize: 100 }), staleTime: 30_000, enabled: !isScheduled && !isPortfolio });
  const [search, setSearch] = React.useState("");
  const [selectedCategory, setSelectedCategory] = React.useState<string | null>(null);
  const [tab, setTab] = React.useState("recent");
  const sectionTitle = section === "executive" ? t("operations.reportsExecutive") : section === "portfolio" ? t("operations.reportsPortfolio") : section === "custom" ? t("operations.reportsCustom") : section === "scheduled" ? t("operations.reportsScheduled") : t("operations.reportsTitle");
  const sourceItems = query.data?.items ?? [];
  const term = search.trim().toLowerCase();
  const categoryForRoute = ["executive", "portfolio", "project", "financial", "risk", "custom"].includes(section) ? section : "";
  const items = sourceItems
    .filter((item) => !term || `${item.name} ${item.category} ${item.description ?? ""}`.toLowerCase().includes(term))
    .filter((item) => !categoryForRoute || reportInCategory(item, categoryForRoute))
    .filter((item) => !selectedCategory || reportInCategory(item, selectedCategory));
  const categories = [
    { id: "executive", label: t("operations.reportsExecutive"), icon: "📊" },
    { id: "portfolio", label: t("operations.reportsPortfolio"), icon: "📁" },
    { id: "project", label: t("operations.reportsProject"), icon: "📋" },
    { id: "financial", label: t("operations.reportsFinancial"), icon: "💰" },
    { id: "risk", label: t("operations.reportsRisk"), icon: "⚠️" },
    { id: "custom", label: t("operations.reportsCustom"), icon: "📝" },
  ];
  const isLanding = !section;
  const reportTable = (rows: Report[]) => rows.length ? <Table><TableHeader><TableRow><TableHead>{t("operations.name")}</TableHead><TableHead>{t("operations.category")}</TableHead><TableHead>{t("operations.format")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("operations.updated")}</TableHead></TableRow></TableHeader><TableBody>{rows.map((item) => <TableRow key={item.id}><TableCell className="font-semibold"><FileText className="me-2 inline h-4 w-4" />{item.name}</TableCell><TableCell>{item.category}</TableCell><TableCell>{item.outputFormat ?? "—"}</TableCell><TableCell><Badge status={item.isActive ? "Active" : "Inactive"}>{item.isActive ? t("common.active") : t("common.inactive")}</Badge></TableCell><TableCell>{item.updatedAt ? formatDate(item.updatedAt) : "—"}</TableCell></TableRow>)}</TableBody></Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>;
  const reportState = query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorState text={t("common.errorLoading")} /> : null;
  const scheduleUnavailable = <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{t("operations.reportScheduleUnavailable")}</CardContent></Card>;

  if (isPortfolio) return <PortfolioReportsFull />;

  return <div className="space-y-6">
    <PageHeader title={sectionTitle} description={t("operations.reportsDescription")} actions={isLanding ? <div className="flex gap-2"><Button type="button" variant="outline" size="sm" onClick={() => navigate("/reports/scheduled")}><Clock3 className="me-2 h-4 w-4" />{t("operations.reportsSchedule")}</Button><Button type="button" size="sm" disabled title={t("operations.reportActionsUnavailable")}><Plus className="me-2 h-4 w-4" />{t("operations.reportsNew")}</Button></div> : undefined} />
    {isScheduled ? scheduleUnavailable : <>
      {!isScheduled ? <div className="relative max-w-md"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("operations.searchReports")} className="ps-8" /></div> : null}
      {isLanding ? <div className="grid gap-4 md:grid-cols-3 lg:grid-cols-6">{categories.map((category) => { const count = query.data ? sourceItems.filter((item) => reportInCategory(item, category.id)).length : null; const active = selectedCategory === category.id; return <button key={category.id} type="button" aria-pressed={active} onClick={() => setSelectedCategory((current) => current === category.id ? null : category.id)} className={`flex min-h-32 flex-col items-center justify-center gap-2 rounded-xl border bg-card px-3 py-6 text-center transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${active ? "border-primary ring-1 ring-primary/30" : ""}`}><span className="text-2xl" aria-hidden="true">{category.icon}</span><span className="text-sm font-medium">{category.label}</span><span className="text-xs text-muted-foreground">{count ?? "—"} {t("operations.reportCount")}</span></button>; })}</div> : null}
      {isLanding ? <Tabs value={tab} onValueChange={setTab}><TabsList><TabsTrigger value="recent">{t("operations.reportsRecent")}</TabsTrigger><TabsTrigger value="scheduled">{t("operations.reportsScheduled")}</TabsTrigger><TabsTrigger value="templates">{t("operations.reportsTemplates")}</TabsTrigger></TabsList>
        <TabsContent value="recent"><Card><CardContent className="p-0">{reportState ?? reportTable(items)}</CardContent></Card></TabsContent>
        <TabsContent value="scheduled">{scheduleUnavailable}</TabsContent>
        <TabsContent value="templates"><div className="grid gap-4 md:grid-cols-3">{reportState ?? (items.filter((item) => item.isSystem).length ? items.filter((item) => item.isSystem).map((item) => <Card key={item.id}><CardHeader><CardTitle className="text-base">{item.name}</CardTitle><p className="text-sm text-muted-foreground">{item.description || t("operations.reportTemplateDescription")}</p></CardHeader><CardContent><Badge status={item.isActive ? "Active" : "Inactive"}>{item.outputFormat ?? t("operations.category")}</Badge></CardContent></Card>) : <Card className="md:col-span-3"><CardContent className="py-10 text-center text-sm text-muted-foreground">{t("common.noResults")}</CardContent></Card>)}</div></TabsContent>
      </Tabs> : <Card><CardContent className="p-0">{reportState ?? reportTable(items)}</CardContent></Card>}
    </>}
  </div>;
}

function reportInCategory(report: Report, category: string): boolean {
  const value = report.category.toLowerCase();
  if (category === "financial") return value.includes("financial") || value.includes("finance");
  if (category === "project") return value.includes("project");
  if (category === "risk") return value.includes("risk");
  return value.includes(category);
}

function PortfolioReportsFull() {
  const { t, formatCurrency, formatPercent, formatNumber } = useI18n();
  const { user } = useAuth();
  const { context, loading: managementLoading } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.View));
  const hasPortfolioScope = context?.perspective === MANAGEMENT_PERSPECTIVES.PortfolioManager;
  const portfoliosQuery = useManagedPortfolios("", canView && hasPortfolioScope);
  const portfolios = portfoliosQuery.data?.items ?? [];

  return <div className="space-y-6">
    <PageHeader title={t("operations.reportsPortfolio")} description={t("operations.portfolioReportDescription")} actions={<Button type="button" variant="outline" size="sm" disabled title={t("operations.reportActionsUnavailable")}><Download className="me-2 h-4 w-4" />{t("operations.portfolioReportExport")}</Button>} />
    {!canView ? <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{t("operations.portfolioReportPermissionRequired")}</CardContent></Card>
      : managementLoading ? <Loading text={t("common.loading")} />
        : !hasPortfolioScope ? <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{t("operations.portfolioReportScopeRequired")}</CardContent></Card>
          : portfoliosQuery.isLoading ? <Loading text={t("common.loading")} />
            : portfoliosQuery.isError ? <Card><CardContent className="flex flex-col items-center gap-3 py-10 text-center"><ErrorState text={t("portfolioManager.loadFailed")} /><Button size="sm" variant="outline" onClick={() => void portfoliosQuery.refetch()}>{t("common.retry")}</Button></CardContent></Card>
              : portfolios.length === 0 ? <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{t("portfolioManager.noPortfolios")}</CardContent></Card>
                : <div className="grid gap-4 md:grid-cols-2">{portfolios.map((portfolio) => <PortfolioReportCard key={portfolio.id} portfolio={portfolio} t={t} formatCurrency={formatCurrency} formatPercent={formatPercent} formatNumber={formatNumber} />)}</div>}
  </div>;
}

function PortfolioReportCard({ portfolio, t, formatCurrency, formatPercent, formatNumber }: {
  portfolio: PortfolioManagementListItem;
  t: (key: string, values?: Record<string, string | number>) => string;
  formatCurrency: (value: number, currency?: string) => string;
  formatPercent: (value: number) => string;
  formatNumber: (value: number) => string;
}) {
  const cardRef = React.useRef<HTMLDivElement>(null);
  const [visible, setVisible] = React.useState(false);
  React.useEffect(() => {
    const element = cardRef.current;
    if (!element || !("IntersectionObserver" in window)) { setVisible(true); return; }
    const observer = new IntersectionObserver(([entry]) => {
      if (entry?.isIntersecting) { setVisible(true); observer.disconnect(); }
    }, { rootMargin: "160px" });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  const overviewQuery = usePortfolioManagementOverview(portfolio.id, { programsPage: 1, projectsPage: 1, pageSize: 1 }, visible);
  const overview = overviewQuery.data;

  return <Card ref={cardRef}>
    <CardHeader className="flex flex-row items-start justify-between gap-3">
      <div className="min-w-0"><CardTitle className="text-base">{portfolio.name}</CardTitle><CardDescription className="mt-1">{t("operations.portfolioReportProjectCount", { count: portfolio.totalProjectCount })}</CardDescription></div>
      <span className={`shrink-0 text-sm font-medium ${getStatusTextClass(portfolio.health ?? "Unknown")}`}>{portfolio.health || t("portfolioManager.unknown")}</span>
    </CardHeader>
    <CardContent className="space-y-4">
      {!visible || overviewQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-5 w-2/3" /><Skeleton className="h-12 w-full" /></div>
        : overviewQuery.isError || !overview ? <div className="flex flex-col items-center gap-2 py-3 text-center"><p role="alert" className="text-sm text-destructive">{t("operations.portfolioReportLoadFailed")}</p><Button size="sm" variant="outline" onClick={() => void overviewQuery.refetch()}>{t("common.retry")}</Button></div>
          : <>
            <div className="grid grid-cols-2 gap-3 border-b pb-4">
              <div><p className="text-xs text-muted-foreground">{t("portfolioManager.totalProjects")}</p><p className="mt-1 text-lg font-semibold tabular-nums">{formatNumber(overview.summary.totalProjects)}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("portfolioManager.averageProgress")}</p><p className="mt-1 text-lg font-semibold tabular-nums">{overview.summary.averageProjectProgress == null ? "—" : formatPercent(overview.summary.averageProjectProgress)}</p></div>
            </div>
            <div className="space-y-3">
              {overview.financials.groups.length ? <div className="grid gap-3 sm:grid-cols-2">{overview.financials.groups.map((group) => <div key={group.currency} className="rounded-md border p-3"><p className="text-xs font-medium text-muted-foreground">{group.currency}</p><div className="mt-2 grid grid-cols-2 gap-3 text-sm"><div><p className="text-xs text-muted-foreground">{t("portfolioManager.budget")}</p><p className="mt-1 font-medium tabular-nums">{formatCurrency(group.budget, group.currency)}</p></div><div><p className="text-xs text-muted-foreground">{t("portfolioManager.actual")}</p><p className="mt-1 font-medium tabular-nums">{formatCurrency(group.actual, group.currency)}</p></div></div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("portfolioManager.noFinancials")}</p>}
              {overview.financials.hasMixedCurrencies ? <p className="text-xs text-muted-foreground">{t("portfolioManager.mixedCurrencyNote")}</p> : null}
            </div>
          </>}
      <div className="flex justify-end border-t pt-3"><Link to={`/portfolios/${encodeURIComponent(portfolio.id)}`} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">{t("portfolioManager.open")}<ArrowUpRight className="h-4 w-4" /></Link></div>
    </CardContent>
  </Card>;
}

export function AuditLogPageFull() {
  const { t, formatDate } = useI18n();
  const [search, setSearch] = React.useState("");
  const [debouncedSearch] = useDebounce(search.trim(), 300);
  const [page, setPage] = React.useState(1);
  const pageSize = 25;
  const [filtersOpen, setFiltersOpen] = React.useState(false);
  const [draftFilters, setDraftFilters] = React.useState({ entityType: "", action: "", entityId: "" });
  const [filters, setFilters] = React.useState({ entityType: "", action: "", entityId: "" });
  const query = useAuditEntries({
    page,
    pageSize,
    entityType: filters.entityType || undefined,
    action: filters.action || undefined,
    entityId: filters.entityId || undefined,
    search: debouncedSearch || undefined,
  });
  const entries = query.data?.items ?? [];
  function applyFilters(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setFilters({ entityType: draftFilters.entityType.trim(), action: draftFilters.action.trim(), entityId: draftFilters.entityId.trim() });
  }
  function clearFilters() {
    const emptyFilters = { entityType: "", action: "", entityId: "" };
    setPage(1);
    setDraftFilters(emptyFilters);
    setFilters(emptyFilters);
  }
  return <div className="space-y-6">
    <PageHeader title={t("operations.auditTitle")} description={t("operations.auditDescription")} />
    <div className="flex flex-wrap items-center gap-2">
      <div className="relative max-w-lg flex-1"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(event) => { setPage(1); setSearch(event.target.value); }} placeholder={t("operations.searchAudit")} className="ps-8" /></div>
      <Button type="button" variant="outline" aria-expanded={filtersOpen} onClick={() => setFiltersOpen((open) => !open)}><Filter className="me-2 h-4 w-4" />{t("operations.auditAdvancedFilters")}</Button>
    </div>
    {filtersOpen && <form onSubmit={applyFilters} className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-2 lg:grid-cols-4">
      <div className="space-y-1.5"><Label htmlFor="audit-entity-type">{t("operations.auditEntityType")}</Label><Input id="audit-entity-type" value={draftFilters.entityType} onChange={(event) => setDraftFilters((current) => ({ ...current, entityType: event.target.value }))} /></div>
      <div className="space-y-1.5"><Label htmlFor="audit-action">{t("operations.auditActionFilter")}</Label><Input id="audit-action" value={draftFilters.action} onChange={(event) => setDraftFilters((current) => ({ ...current, action: event.target.value }))} /></div>
      <div className="space-y-1.5"><Label htmlFor="audit-entity-id">{t("operations.auditEntityId")}</Label><Input id="audit-entity-id" value={draftFilters.entityId} onChange={(event) => setDraftFilters((current) => ({ ...current, entityId: event.target.value }))} /></div>
      <div className="flex items-end gap-2"><Button type="submit">{t("operations.applyAuditFilters")}</Button><Button type="button" variant="outline" onClick={clearFilters}>{t("operations.clearAuditFilters")}</Button></div>
    </form>}
    <Card><CardContent className="p-0">
      {query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorState text={t("common.errorLoading")} /> : entries.length ? <Table>
        <TableHeader><TableRow><TableHead>{t("operations.timestamp")}</TableHead><TableHead>{t("operations.user")}</TableHead><TableHead>{t("operations.action")}</TableHead><TableHead>{t("operations.entity")}</TableHead><TableHead>{t("operations.changes")}</TableHead></TableRow></TableHeader>
        <TableBody>{entries.map((entry) => <TableRow key={entry.id}><TableCell className="whitespace-nowrap text-xs">{formatDate(entry.timestamp)}</TableCell><TableCell>{entry.userName ?? "—"}</TableCell><TableCell><span title={entry.action}>{auditActionLabel(entry.action, t)}</span></TableCell><TableCell><span title={entry.entityType}>{auditEntityLabel(entry.entityType, t)}</span><span className="ms-1 text-xs text-muted-foreground">{entry.entityId}</span></TableCell><TableCell className="max-w-md truncate text-xs text-muted-foreground" title={entry.changes ?? undefined}>{formatAuditChanges(entry.changes, t) || "—"}</TableCell></TableRow>)}</TableBody>
      </Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}
    </CardContent></Card>
    {!query.isLoading && !query.isError && (query.data?.totalCount ?? 0) > 0 ? <Pagination page={page} totalPages={query.data?.totalPages ?? 1} total={query.data?.totalCount} pageSize={pageSize} onChange={setPage} /> : null}
  </div>;
}

export function DataClassificationPageFull() {
  const { t } = useI18n();
  const levels = [
    { title: t("operations.securityClassification.public"), description: t("operations.securityClassification.publicDescription"), icon: Eye, tone: "bg-status-green", examples: [t("operations.securityClassification.projectNames"), t("operations.securityClassification.teamMembers"), t("operations.securityClassification.meetingSchedules")] },
    { title: t("operations.securityClassification.internal"), description: t("operations.securityClassification.internalDescription"), icon: Users, tone: "bg-blue-500", examples: [t("operations.securityClassification.statusReports"), t("operations.securityClassification.resourceAllocation"), t("operations.securityClassification.budgetSummaries")] },
    { title: t("operations.securityClassification.confidential"), description: t("operations.securityClassification.confidentialDescription"), icon: Lock, tone: "bg-status-amber", examples: [t("operations.securityClassification.financialDetails"), t("operations.securityClassification.vendorContracts"), t("operations.securityClassification.riskAssessments")] },
    { title: t("operations.securityClassification.restricted"), description: t("operations.securityClassification.restrictedDescription"), icon: ShieldCheck, tone: "bg-status-red", examples: [t("operations.securityClassification.securityCredentials"), t("operations.securityClassification.auditLogs"), t("operations.securityClassification.personalData")] },
  ];
  return <div className="space-y-6"><PageHeader title={t("operations.securityClassification.title")} description={t("operations.securityClassification.description")} /><div className="grid gap-4 md:grid-cols-2">{levels.map((level) => { const Icon = level.icon; return <Card key={level.title}><CardHeader><div className="flex items-center gap-3"><div className={`flex h-10 w-10 items-center justify-center rounded-lg ${level.tone}/10`}><Icon className={`h-5 w-5 ${level.tone} text-white`} /></div><div><CardTitle className="text-base">{level.title}</CardTitle><p className="text-xs text-muted-foreground">{level.description}</p></div></div></CardHeader><CardContent><p className="mb-2 text-xs font-medium text-muted-foreground">{t("operations.securityClassification.examples")}</p><ul className="space-y-1">{level.examples.map((example) => <li key={example} className="flex items-center gap-2 text-xs text-muted-foreground"><span className={`h-1.5 w-1.5 rounded-full ${level.tone}`} />{example}</li>)}</ul></CardContent></Card>; })}</div></div>;
}

export function SecurityPoliciesPageFull() {
  const { t } = useI18n();
  const policies = [
    { name: t("operations.securityPolicies.passwordComplexity"), description: t("operations.securityPolicies.passwordComplexityDescription"), active: true, scope: t("operations.securityPolicies.global") },
    { name: t("operations.securityPolicies.mfaEnforcement"), description: t("operations.securityPolicies.mfaEnforcementDescription"), active: true, scope: t("operations.securityPolicies.global") },
    { name: t("operations.securityPolicies.sessionTimeout"), description: t("operations.securityPolicies.sessionTimeoutDescription"), active: true, scope: t("operations.securityPolicies.global") },
    { name: t("operations.securityPolicies.ipWhitelisting"), description: t("operations.securityPolicies.ipWhitelistingDescription"), active: false, scope: t("operations.securityPolicies.global") },
    { name: t("operations.securityPolicies.dataExportControls"), description: t("operations.securityPolicies.dataExportControlsDescription"), active: true, scope: t("operations.securityPolicies.project") },
    { name: t("operations.securityPolicies.auditLogRetention"), description: t("operations.securityPolicies.auditLogRetentionDescription"), active: true, scope: t("operations.securityPolicies.system") },
  ];
  return <div className="space-y-6"><PageHeader title={t("operations.securityPolicies.title")} description={t("operations.securityPolicies.description")} actions={<Button size="sm"><Plus className="me-2 h-4 w-4" />{t("operations.securityPolicies.newPolicy")}</Button>} /><div className="space-y-3">{policies.map((policy) => <Card key={policy.name}><CardContent className="flex items-center gap-4 py-4"><div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted"><ShieldCheck className="h-5 w-5" /></div><div className="flex-1"><div className="flex flex-wrap items-center gap-2"><p className="font-medium">{policy.name}</p><Badge status={policy.active ? "Active" : "Inactive"} className="text-[10px]">{policy.active ? t("common.active") : t("common.inactive")}</Badge><span className="text-xs font-medium text-muted-foreground">{policy.scope}</span></div><p className="mt-1 text-xs text-muted-foreground">{policy.description}</p></div><Button size="sm" variant="outline"><Pencil className="me-1.5 h-3.5 w-3.5" />{t("common.edit")}</Button></CardContent></Card>)}</div></div>;
}

export function WorkItemCollectionPageFull({ type, titleKey, descriptionKey }: { type: WorkItemType; titleKey: string; descriptionKey: string }) {
  const { t, formatDate } = useI18n();
  const [search, setSearch] = React.useState("");
  const query = useFullWorkItems({ type, search: search || undefined, page: 1, pageSize: 100 });
  const items = query.data?.items ?? [];
  return <div className="space-y-6"><PageHeader title={t(titleKey)} description={`${t(descriptionKey)} · ${query.data?.totalCount ?? 0}`} actions={<Link to="/work-items" className="button-outline"><Plus className="me-1 h-4 w-4" />{t("operations.openWorkItems")}</Link>} /><div className="relative max-w-lg"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("operations.searchWorkItems")} className="ps-8" /></div><Card><CardContent className="p-0">{query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorState text={t("common.errorLoading")} /> : items.length ? <Table><TableHeader><TableRow><TableHead>{t("common.title")}</TableHead><TableHead>{t("common.priority")}</TableHead><TableHead>{t("workItems.assignee")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("workItems.dueDate")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{items.map(item => <TableRow key={item.id}><TableCell className="font-semibold">{item.title}</TableCell><TableCell><Badge status={item.priority}>{item.priority}</Badge></TableCell><TableCell>{item.assignedResourceName ?? item.assignedTeamName ?? t("workItems.unassigned")}</TableCell><TableCell><Badge status={item.status}>{item.status}</Badge></TableCell><TableCell>{item.dueDate ? formatDate(item.dueDate) : "—"}</TableCell><TableCell><Link to={`/work-items/${encodeURIComponent(item.id)}`} className="text-primary hover:underline"><ArrowUpRight className="h-4 w-4" /></Link></TableCell></TableRow>)}</TableBody></Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}</CardContent></Card></div>;
}

export function TestManagementPageFull() {
  const { t, formatDate, locale } = useI18n();
  const [search, setSearch] = React.useState("");
  const [debouncedSearch] = useDebounce(search.trim(), 300);
  const [statusTab, setStatusTab] = React.useState("all");
  const [page, setPage] = React.useState(1);
  const pageSize = 20;
  const status = statusTab === "all" ? undefined : statusTab;
  const query = useFullWorkItems({ type: "TestCase", status, search: debouncedSearch || undefined, page, pageSize });
  const insights = useFullWorkItemInsights({ type: "TestCase" });
  const statuses = useWorkItemStatuses();
  const rows = query.data?.items ?? [];
  const countForStatus = (value: string) => insights.data?.byStatus.find((bucket) => bucket.key.trim().toLowerCase() === value.toLowerCase())?.count ?? 0;
  const metric = (value: number) => insights.isLoading || insights.isError ? "—" : String(value);
  const testManagementLabels = [
    { id: "passed", label: t("operations.testManagement.passed"), value: metric(countForStatus("Passed")), icon: CheckCircle2, tone: "text-success" },
    { id: "failed", label: t("operations.testManagement.failed"), value: metric(countForStatus("Failed")), icon: XCircle, tone: "text-destructive" },
    { id: "blocked", label: t("operations.testManagement.blocked"), value: metric(countForStatus("Blocked")), icon: AlertCircle, tone: "text-warning" },
    { id: "draft-ready", label: t("operations.testManagement.draftReady"), value: metric(countForStatus("Draft") + countForStatus("Ready")), icon: Beaker, tone: "text-muted-foreground" },
  ];
  const statusName = (itemStatus: string, references: WorkItemStatusDto[]) => {
    const reference = references.find((candidate) => candidate.code === itemStatus);
    return reference ? (locale === "ar" ? reference.nameAr : reference.nameEn) : itemStatus;
  };
  const total = query.data?.totalCount;
  const description = total === undefined ? t("operations.testsDescription") : `${total} ${t("operations.testManagement.testCasesCount")}`;

  return <div className="space-y-6">
    <PageHeader title={t("operations.testsTitle")} description={description} actions={<Link to="/work-items" className="button-outline"><Plus className="me-1 h-4 w-4" />{t("operations.openWorkItems")}</Link>} />
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      {testManagementLabels.map((item) => {
        const Icon = item.icon;
        return <Card key={item.id}><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{item.label}</CardTitle><Icon className={`h-4 w-4 ${item.tone}`} /></CardHeader><CardContent><div className={`text-2xl font-bold ${item.tone}`}>{item.value}</div></CardContent></Card>;
      })}
    </div>
    <div className="relative max-w-lg"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(event) => { setPage(1); setSearch(event.target.value); }} placeholder={t("operations.testManagement.searchPlaceholder")} className="ps-8" /></div>
    {insights.isError ? <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive"><span>{t("common.errorLoading")}</span><Button type="button" variant="outline" size="sm" onClick={() => void insights.refetch()}>{t("common.retry")}</Button></div> : null}
    <Tabs value={statusTab} onValueChange={(value) => { setPage(1); setStatusTab(value); }}>
      <TabsList><TabsTrigger value="all">{t("operations.testManagement.all")}</TabsTrigger><TabsTrigger value="Passed">{t("operations.testManagement.passed")}</TabsTrigger><TabsTrigger value="Failed">{t("operations.testManagement.failed")}</TabsTrigger><TabsTrigger value="Blocked">{t("operations.testManagement.blocked")}</TabsTrigger></TabsList>
      <TabsContent value={statusTab}>
        <Card><CardContent className="p-0">
          {query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <div role="alert" className="flex items-center justify-center gap-3 p-8 text-sm text-destructive"><span>{t("common.errorLoading")}</span><Button type="button" variant="outline" size="sm" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div> : rows.length ? <Table>
            <TableHeader><TableRow><TableHead>{t("operations.testManagement.testCase")}</TableHead><TableHead>{t("common.priority")}</TableHead><TableHead>{t("workItems.assignee")}</TableHead><TableHead>{t("operations.updated")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
            <TableBody>{rows.map((item) => <TableRow key={item.id}><TableCell className="max-w-lg font-medium">{item.title}</TableCell><TableCell><span className={`text-xs font-semibold ${getStatusTextClass(item.priority)}`}>{t(`workItems.priority${item.priority}`)}</span></TableCell><TableCell>{item.assignedResourceName ?? item.assignedTeamName ?? t("workItems.unassigned")}</TableCell><TableCell className="whitespace-nowrap text-xs text-muted-foreground">{formatDate(item.updatedAt ?? item.createdAt)}</TableCell><TableCell><span className={`text-xs font-semibold ${getStatusTextClass(item.status)}`}>{statusName(item.status, statuses.data ?? [])}</span></TableCell></TableRow>)}</TableBody>
          </Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}
        </CardContent></Card>
        {!query.isLoading && !query.isError && (query.data?.totalCount ?? 0) > 0 ? <Pagination page={page} totalPages={query.data?.totalPages ?? 1} total={query.data?.totalCount} pageSize={pageSize} onChange={setPage} className="mt-4" /> : null}
      </TabsContent>
    </Tabs>
  </div>;
}

export function AIWorkspacePageFull() {
  const { t, formatDate } = useI18n();
  const [title, setTitle] = React.useState("");
  const [context, setContext] = React.useState("");
  const [selected, setSelected] = React.useState<AIRequest | null>(null);
  const history = useQuery({ queryKey: ["ai-planning", "requests"], queryFn: () => api.get<Paged<AIRequest>>(endpoints.aiPlanning.root, { page: 1, pageSize: 50 }), staleTime: 15_000 });
  const generate = useMutation({ mutationFn: (body: { title: string; context: string }) => api.post<AIRequest>(endpoints.aiPlanning.generate, body), onSuccess: (result) => setSelected(result) });
  function submit(event: React.FormEvent) { event.preventDefault(); if (title.trim() && context.trim()) generate.mutate({ title: title.trim(), context: context.trim() }); }
  return <div className="space-y-6"><PageHeader title={t("operations.aiTitle")} description={t("operations.aiDescription")} actions={<Badge status="AI"><Bot className="me-1 h-4 w-4" />{t("operations.aiBackend")}</Badge>} /><div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(18rem,1fr)]"><Card><CardHeader><CardTitle className="flex items-center gap-2"><Bot className="h-5 w-5 text-primary" />{t("operations.generatePlan")}</CardTitle></CardHeader><CardContent><form onSubmit={submit} className="space-y-4"><div className="space-y-1.5"><Label>{t("operations.titleField")}</Label><Input value={title} onChange={(e) => setTitle(e.target.value)} required /></div><div className="space-y-1.5"><Label>{t("operations.context")}</Label><Textarea value={context} onChange={(e) => setContext(e.target.value)} rows={8} required placeholder={t("operations.contextPlaceholder")} /></div><Button type="submit" disabled={generate.isPending}><Send className="me-1.5 h-4 w-4" />{generate.isPending ? t("operations.generating") : t("operations.generate")}</Button>{generate.isError ? <p className="text-sm text-destructive">{t("operations.generateError")}</p> : null}</form>{selected ? <div className="mt-6 rounded border p-4"><p className="mb-2 font-semibold">{selected.title}</p><p className="whitespace-pre-wrap text-sm">{selected.result ?? selected.errorMessage ?? selected.status}</p></div> : null}</CardContent></Card><Card><CardHeader><CardTitle>{t("operations.aiHistory")}</CardTitle></CardHeader><CardContent className="space-y-2">{history.isLoading ? <Loading text={t("common.loading")} /> : (history.data?.items ?? []).map(item => <button key={item.id} type="button" className="w-full rounded border p-3 text-start hover:bg-muted/50" onClick={() => setSelected(item)}><p className="font-medium">{item.title}</p><p className="text-xs text-muted-foreground">{item.status} · {formatDate(item.requestedAt)}</p></button>)}{!history.isLoading && !(history.data?.items ?? []).length ? <p className="text-sm text-muted-foreground">{t("common.noResults")}</p> : null}</CardContent></Card></div></div>;
}

export function AIInsightsPageFull() {
  const { t, formatDate } = useI18n();
  const history = useQuery({ queryKey: ["ai-planning", "insights"], queryFn: () => api.get<Paged<AIRequest>>(endpoints.aiPlanning.root, { page: 1, pageSize: 100 }), staleTime: 15_000 });
  const items = history.data?.items ?? [];
  const completed = items.filter((item) => item.status.toLowerCase() === "completed").length;
  const failed = items.filter((item) => ["failed", "error"].includes(item.status.toLowerCase())).length;
  return <div className="space-y-6"><PageHeader title={t("operations.aiInsightsTitle")} description={t("operations.aiInsightsDescription")} /><div className="grid gap-4 md:grid-cols-3"><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("operations.aiRequests")}</p><p className="mt-1 text-2xl font-semibold">{items.length}</p></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("operations.aiCompleted")}</p><p className="mt-1 text-2xl font-semibold text-status-green">{completed}</p></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("operations.aiFailed")}</p><p className="mt-1 text-2xl font-semibold text-status-red">{failed}</p></CardContent></Card></div><Card><CardHeader><CardTitle>{t("operations.aiHistory")}</CardTitle></CardHeader><CardContent className="p-0">{history.isLoading ? <Loading text={t("common.loading")} /> : history.isError ? <ErrorState text={t("common.errorLoading")} /> : items.length ? <Table><TableHeader><TableRow><TableHead>{t("operations.titleField")}</TableHead><TableHead>{t("operations.aiProvider")}</TableHead><TableHead>{t("operations.aiRequestStatus")}</TableHead><TableHead>{t("operations.updated")}</TableHead><TableHead>{t("operations.context")}</TableHead></TableRow></TableHeader><TableBody>{items.map((item) => <TableRow key={item.id}><TableCell className="font-semibold">{item.title}</TableCell><TableCell>{item.provider}{item.model ? ` · ${item.model}` : ""}</TableCell><TableCell><Badge status={item.status}>{item.status}</Badge></TableCell><TableCell>{formatDate(item.completedAt ?? item.requestedAt)}</TableCell><TableCell className="max-w-md truncate text-xs text-muted-foreground">{item.result ?? item.errorMessage ?? item.context}</TableCell></TableRow>)}</TableBody></Table> : <p className="p-8 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>}</CardContent></Card></div>;
}

export function AIPlanGeneratorPageFull() {
  const { t } = useI18n();
  const [title, setTitle] = React.useState("");
  const [context, setContext] = React.useState("");
  const [result, setResult] = React.useState<AIRequest | null>(null);
  const generate = useMutation({ mutationFn: (body: { title: string; context: string }) => api.post<AIRequest>(endpoints.aiPlanning.generate, body), onSuccess: setResult });
  function submit(event: React.FormEvent) { event.preventDefault(); if (title.trim() && context.trim()) generate.mutate({ title: title.trim(), context: context.trim() }); }
  return <div className="space-y-6"><PageHeader title={t("operations.aiPlanTitle")} description={t("operations.aiPlanDescription")} actions={<Badge status="AI">{t("operations.aiBackend")}</Badge>} /><Card><CardHeader><CardTitle>{t("operations.generatePlan")}</CardTitle></CardHeader><CardContent><form onSubmit={submit} className="max-w-3xl space-y-4"><div className="space-y-1.5"><Label>{t("operations.titleField")}</Label><Input value={title} onChange={(event) => setTitle(event.target.value)} required /></div><div className="space-y-1.5"><Label>{t("operations.context")}</Label><Textarea value={context} onChange={(event) => setContext(event.target.value)} rows={10} required placeholder={t("operations.contextPlaceholder")} /></div><Button type="submit" disabled={generate.isPending || !title.trim() || !context.trim()}><Send className="me-1.5 h-4 w-4" />{generate.isPending ? t("operations.generating") : t("operations.generate")}</Button>{generate.isError ? <p role="alert" className="text-sm text-destructive">{t("operations.generateError")}</p> : null}</form>{result ? <div className="mt-6 rounded border p-4"><div className="flex items-center justify-between gap-3"><p className="font-semibold">{result.title}</p><Badge status={result.status}>{result.status}</Badge></div><p className="mt-3 whitespace-pre-wrap text-sm">{result.result ?? result.errorMessage ?? result.context}</p></div> : null}</CardContent></Card></div>;
}

export function SecurityAuditPageFull() { return <AuditLogPageFull />; }
