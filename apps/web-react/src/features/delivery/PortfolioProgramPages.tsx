import * as React from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Activity, ArrowRightLeft, CalendarDays, ClipboardList, Files, GitBranch, LayoutDashboard, TriangleAlert, Users, WalletCards, type LucideIcon } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { managementCacheScopeKey, useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { MANAGEMENT_PERSPECTIVES } from "@lib/perspective";
import { getStatusTextClass } from "@lib/status-presentation";
import {
  useArchivePortfolio,
  usePortfolio,
  useManagedPortfolios,
  useManagedPrograms,
  usePortfolioManagementOverview,
  useProgram,
  useProgramManagementOverview,
  useArchiveProgram,
  useReactivatePortfolio,
  useReactivateProgram,
  type PortfolioManagementOverview,
  type ProgramManagementOverview,
} from "./api/delivery";
import { PortfolioSheet } from "./PortfolioSheet";
import { ProgramSheet } from "./ProgramSheet";
import { ManagerHandoverPanel } from "./ManagerHandoverPanel";
import { FinancialRollupView } from "./FinancialRollupView";

type Tab = "overview" | "programs" | "projects" | "delivery" | "financials" | "risks" | "resources" | "statusReports" | "activity" | "handover";

const TAB_ICONS: Record<Tab, LucideIcon> = {
  overview: LayoutDashboard,
  programs: GitBranch,
  projects: Files,
  delivery: CalendarDays,
  financials: WalletCards,
  risks: TriangleAlert,
  resources: Users,
  statusReports: ClipboardList,
  activity: Activity,
  handover: ArrowRightLeft,
};

const projectTabListClass = "flex h-auto w-full min-w-0 items-center justify-between gap-1 overflow-x-auto overflow-y-hidden rounded-none bg-transparent px-2";
const projectTabTriggerClass = "flex h-auto shrink-0 items-center whitespace-nowrap rounded-none border-b-2 border-transparent px-3 py-1.5 text-[13px] font-semibold leading-5 text-muted-foreground transition-colors rtl:flex-row-reverse data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:px-3.5 data-[state=active]:py-1.5 data-[state=active]:text-[15px] data-[state=active]:font-semibold data-[state=active]:text-primary data-[state=active]:shadow-none";
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

function projectPublicId(reference: unknown): string | undefined {
  if (typeof reference !== "object" || reference === null) return undefined;
  const value = reference as { publicId?: unknown; projectPublicId?: unknown };
  const id = value.publicId ?? value.projectPublicId;
  return typeof id === "string" && id.length > 0 ? id : undefined;
}

function projectHref(reference: unknown): string | undefined {
  const publicId = projectPublicId(reference);
  return publicId ? `/projects/${encodeURIComponent(publicId)}` : undefined;
}

function normalizePortfolioManagerId(value?: string): string | undefined {
  const managerId = value?.trim();
  return managerId && managerId.toLowerCase() !== EMPTY_GUID ? managerId : undefined;
}

const portfolioOverviewWidgets = [
  ["portfolio.health-overview", "portfolioManager.healthOverview"],
  ["portfolio.delivery-milestones", "portfolioManager.deliveryAndMilestones"],
  ["portfolio.risk-issues", "portfolioManager.riskAndIssues"],
  ["portfolio.resources", "portfolioManager.resources"],
  ["portfolio.status-reports", "portfolioManager.statusReports"],
  ["portfolio.activity", "portfolioManager.meetingsActivity"],
] as const;

export function ManagedPortfoliosPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { context, loading: managementLoading, canMutate, effectiveReadOnly } = useManagement();
  const cacheScope = managementCacheScopeKey(user?.id, context);
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.Create) && canMutate && !effectiveReadOnly);
  const canAssignInitialManager = Boolean(user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const [search, setSearch] = React.useState("");
  const [createOpen, setCreateOpen] = React.useState(false);
  const query = useManagedPortfolios(search, !managementLoading, cacheScope);
  const items = query.data?.items ?? [];
  return <div className="space-y-6">
    <PageHeader title={t("portfolioManager.myPortfolios")} description={t("portfolioManager.myPortfoliosDescription")} actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}>{t("portfolioManager.createPortfolio")}</Button> : null} />
    <Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.portfolioScope")}</CardTitle><p className="text-sm text-muted-foreground">{t("portfolioManager.scopeDescription")}</p><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("portfolioManager.searchPortfolios")} aria-label={t("portfolioManager.searchPortfolios")} className="mt-2 max-w-sm" /></CardHeader><CardContent className="p-0">
      {managementLoading || query.isLoading ? <Loading /> : query.isError ? <ErrorState text={t("portfolioManager.loadFailed")} onRetry={() => void query.refetch()} /> : items.length === 0 ? <Empty text={t("portfolioManager.noPortfolios")} /> : <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.portfolio")}</TableHead><TableHead>{t("portfolioManager.owner")}</TableHead><TableHead>{t("portfolioManager.status")}</TableHead><TableHead>{t("portfolioManager.health")}</TableHead><TableHead>{t("portfolioManager.programs")}</TableHead><TableHead>{t("portfolioManager.directProjects")}</TableHead><TableHead>{t("portfolioManager.totalProjects")}</TableHead><TableHead>{t("portfolioManager.attention")}</TableHead><TableHead>{t("portfolioManager.risk")}</TableHead><TableHead>{t("portfolioManager.reporting")}</TableHead></TableRow></TableHeader><TableBody>{items.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" to={`/portfolios/${encodeURIComponent(item.id)}`}>{item.name}</Link><div className="text-xs text-muted-foreground">{item.code ?? "—"} · {item.currency || "—"}</div></TableCell><TableCell>{item.ownerName ?? "—"}</TableCell><TableCell><Status value={item.status} label={localizedStatus(t, item.status)} /></TableCell><TableCell><Status value={item.health} label={localizedStatus(t, item.health)} /></TableCell><TableCell>{item.programCount}</TableCell><TableCell>{item.directProjectCount}</TableCell><TableCell>{item.totalProjectCount}</TableCell><TableCell><Status value={item.projectsRequiringAttention + item.programsRequiringAttention > 0 ? "Amber" : "Green"} label={String(item.projectsRequiringAttention + item.programsRequiringAttention)} /></TableCell><TableCell><Status value={item.riskIndicator === "NeedsAttention" ? "Amber" : item.riskIndicator} label={portfolioRiskLabel(t, item.riskIndicator)} /></TableCell><TableCell><Status value={item.reportingIndicator === "Current" ? "Green" : item.reportingIndicator === "Stale" ? "Amber" : "Red"} label={portfolioReportingLabel(t, item.reportingIndicator)} /></TableCell></TableRow>)}</TableBody></Table></div>}
    </CardContent></Card>
    <PortfolioSheet open={createOpen} onOpenChange={setCreateOpen} canSave={canCreate} canAssignInitialManager={canAssignInitialManager} />
  </div>;
}

export function ManagedProgramsPage() {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { context, loading: managementLoading, canMutate, effectiveReadOnly } = useManagement();
  const cacheScope = managementCacheScopeKey(user?.id, context);
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Program.Create) && canMutate && !effectiveReadOnly);
  const canAssignInitialManager = Boolean(user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const [search, setSearch] = React.useState("");
  const [createOpen, setCreateOpen] = React.useState(false);
  const query = useManagedPrograms(search, !managementLoading, cacheScope);
  const items = query.data?.items ?? [];
  return <div className="space-y-6">
    <PageHeader title={t("programManager.myPrograms")} description={t("programManager.myProgramsDescription")} actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}>{t("programManager.createProgram")}</Button> : null} />
    <Card><CardHeader><CardTitle className="text-base">{t("programManager.program")}</CardTitle><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("programManager.searchPrograms")} aria-label={t("programManager.searchPrograms")} className="mt-2 max-w-sm" /></CardHeader><CardContent className="p-0">
      {managementLoading || query.isLoading ? <Loading /> : query.isError ? <ErrorState text={t("programManager.loadFailed")} onRetry={() => void query.refetch()} /> : items.length === 0 ? <Empty text={t("programManager.noPrograms")} /> : <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("programManager.program")}</TableHead><TableHead>{t("programManager.health")}</TableHead><TableHead>{t("programManager.status")}</TableHead><TableHead>{t("programManager.manager")}</TableHead><TableHead>{t("programManager.portfolio")}</TableHead><TableHead>{t("programManager.start")}</TableHead><TableHead>{t("programManager.end")}</TableHead><TableHead>{t("programManager.projectCount")}</TableHead><TableHead>{t("programManager.attention")}</TableHead><TableHead>{t("programManager.schedule")}</TableHead></TableRow></TableHeader><TableBody>{items.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" to={`/portfolios/programs/${encodeURIComponent(item.id)}`}>{item.name}</Link><div className="text-xs text-muted-foreground">{item.code ?? "—"} · {item.currency || "—"}</div></TableCell><TableCell><Status value={item.health} label={localizedStatus(t, item.health)} /></TableCell><TableCell><Status value={item.status} label={localizedStatus(t, item.status)} /></TableCell><TableCell>{item.programManagerName ?? "—"}</TableCell><TableCell>{item.portfolioName ?? "—"}</TableCell><TableCell>{item.startDate ? formatDate(item.startDate) : "—"}</TableCell><TableCell>{item.endDate ? formatDate(item.endDate) : "—"}</TableCell><TableCell>{item.projectCount}</TableCell><TableCell><Status value={item.projectsRequiringAttention > 0 ? "Amber" : "Green"} label={String(item.projectsRequiringAttention)} /></TableCell><TableCell><Status value={item.scheduleIndicator === "OnTrack" ? "Green" : item.scheduleIndicator === "Unknown" ? "Unknown" : "Amber"} label={programScheduleLabel(t, item.scheduleIndicator)} /></TableCell></TableRow>)}</TableBody></Table></div>}
    </CardContent></Card>
    <ProgramSheet open={createOpen} onOpenChange={setCreateOpen} canSave={canCreate} canAssignInitialManager={canAssignInitialManager} cacheScope={cacheScope} />
  </div>;
}

export function ManagedPortfolioDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, formatDate, formatPercent } = useI18n();
  const confirm = useConfirm();
  const formatOptionalDate = (value?: string) => value ? formatDate(value) : "—";
  const { user } = useAuth();
  const { context, snapshot, loading: managementLoading, canMutate, effectiveReadOnly, switchPerspective } = useManagement();
  const cacheScope = managementCacheScopeKey(user?.id, context);
  const hasEditPermission = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.Edit));
  const canEdit = hasEditPermission && canMutate && !effectiveReadOnly;
  const canArchive = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.Archive) && canMutate && !effectiveReadOnly);
  const canReactivate = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.Reactivate) && canMutate && !effectiveReadOnly);
  const canAssignInitialManager = Boolean(user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const canViewHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.View));
  const canManageHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage));
  const canAdminInitiateHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const canApproveHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Approve));
  const canSeeHandoverTab = canViewHandover || canManageHandover || canAdminInitiateHandover || canApproveHandover;
  const canViewFinancials = Boolean(user
    && hasPermission(user.permissions, Permissions.Portfolio.View)
    && hasPermission(user.permissions, Permissions.PortfolioFinancialRollup.View));
  const shouldLoadHandoverManager = canViewHandover && canManageHandover;
  const [tab, setTab] = React.useState<Tab>("overview");
  const [programsPage, setProgramsPage] = React.useState(1);
  const [projectsPage, setProjectsPage] = React.useState(1);
  const [editOpen, setEditOpen] = React.useState(false);
  const [actionError, setActionError] = React.useState("");
  const portfolioQuery = usePortfolio(id, (canEdit || shouldLoadHandoverManager) && !managementLoading, cacheScope);
  const archive = useArchivePortfolio();
  const reactivate = useReactivatePortfolio();
  const query = usePortfolioManagementOverview(id, { programsPage, projectsPage, pageSize: 25 }, !managementLoading, cacheScope);
  if (managementLoading || query.isLoading) return <Loading />;
  if (query.isError || !query.data) return <ErrorState text={t("portfolioManager.loadFailed")} onRetry={() => void query.refetch()} />;
  const data = query.data;
  const portfolio = data.portfolio;
  const currentManagerId = normalizePortfolioManagerId(portfolioQuery.data?.ownerId ?? portfolio.ownerId);
  const currentManagerName = currentManagerId ? portfolioQuery.data?.ownerName ?? portfolio.ownerName : undefined;
  const currentManagerLoading = shouldLoadHandoverManager && !currentManagerId && portfolioQuery.isLoading;
  const currentManagerError = shouldLoadHandoverManager && !currentManagerId && portfolioQuery.isError;
  const canViewAs = (perspective: string, entityId: string) => Boolean(snapshot?.availablePerspectives.find((item) => item.perspective === perspective)?.contexts.some((item) => item.id === entityId));
  const viewAsProgram = (programId: string) => { switchPerspective(MANAGEMENT_PERSPECTIVES.ProgramManager, programId); navigate(`/portfolios/programs/${encodeURIComponent(programId)}`); };
  const viewAsProject = (projectId: string, publicId: string) => { switchPerspective(MANAGEMENT_PERSPECTIVES.ProjectManager, projectId); navigate(`/projects/${encodeURIComponent(publicId)}`); };
  const archivePortfolio = async () => {
    if (!await confirm({ description: t("portfolioManager.archiveConfirm"), destructive: true })) return;
    setActionError("");
    try {
      await archive.mutateAsync(id);
      navigate("/portfolios");
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("portfolioManager.portfolioSaveFailed"));
    }
  };
  const reactivatePortfolio = async () => {
    setActionError("");
    try { await reactivate.mutateAsync(id); }
    catch (reason) { setActionError(reason instanceof Error ? reason.message : t("portfolioManager.portfolioSaveFailed")); }
  };
  const actions = canEdit && portfolioQuery.data || canArchive && portfolio.status !== "Archived" || canReactivate && portfolio.status === "Archived" ? <div className="flex flex-wrap items-center gap-2">
    {canEdit && portfolioQuery.data ? <Button size="sm" variant="outline" onClick={() => setEditOpen(true)}>{t("portfolioManager.edit")}</Button> : null}
    {canArchive && portfolio.status !== "Archived" ? <Button size="sm" variant="outline" onClick={() => void archivePortfolio()} disabled={archive.isPending}>{archive.isPending ? t("portfolioManager.saving") : t("portfolioManager.archive")}</Button> : null}
    {canReactivate && portfolio.status === "Archived" ? <Button size="sm" variant="outline" onClick={() => void reactivatePortfolio()} disabled={reactivate.isPending}>{reactivate.isPending ? t("portfolioManager.saving") : t("portfolioManager.reactivate")}</Button> : null}
  </div> : null;
  const tabs: [Tab, string][] = [["overview", "portfolioManager.overview"], ["programs", "portfolioManager.programs"], ["projects", "portfolioManager.projects"], ["delivery", "portfolioManager.delivery"], ...(canViewFinancials ? [["financials", "portfolioManager.financials"] as [Tab, string]] : []), ["risks", "portfolioManager.risksIssues"], ["resources", "portfolioManager.resources"], ["statusReports", "portfolioManager.statusReports"], ["activity", "portfolioManager.meetingsActivity"]];
  if (canSeeHandoverTab) tabs.push(["handover", "managerHandover.handover"]);
  return <div className="space-y-5">
    <PageHeader title={portfolio.name} description={`${portfolio.code ?? ""}${portfolio.ownerName ? ` · ${portfolio.ownerName}` : ""}`} actions={actions} />
    {!canEdit && !managementLoading ? <p role="status" className="text-sm text-muted-foreground">{effectiveReadOnly ? t("portfolioManager.readOnlyEditHint") : !canMutate ? t("portfolioManager.writableContextRequired") : t("portfolioManager.editPermissionRequired")}</p> : null}
    {canEdit && portfolioQuery.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("portfolioManager.loadingPortfolioForEdit")}</p> : null}
    {canEdit && portfolioQuery.isError ? <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm"><span>{t("portfolioManager.portfolioForEditUnavailable")}</span><Button size="sm" variant="outline" onClick={() => void portfolioQuery.refetch()}>{t("common.retry")}</Button></div> : null}
    {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p> : null}
    <Tabs value={tab} onValueChange={(value) => setTab(value as Tab)}>
      <div className="nexus-fullbleed-tabs sticky top-0 z-20 isolate border-y border-border bg-muted/70 py-1">
        <TabsList aria-label={t("portfolioManager.portfolioWorkspace")} className={projectTabListClass}>
          {tabs.map(([key, label]) => { const Icon = TAB_ICONS[key]; return <TabsTrigger key={key} value={key} className={projectTabTriggerClass}><Icon className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />{t(label)}</TabsTrigger>; })}
        </TabsList>
      </div>
      <TabsContent value="overview" className="space-y-5"><PortfolioOverview data={data} t={t} formatPercent={formatPercent} formatDate={formatOptionalDate} /></TabsContent>
      <TabsContent value="programs"><PortfolioPrograms data={data} t={t} page={programsPage} onPage={setProgramsPage} canViewAs={(programId) => canViewAs(MANAGEMENT_PERSPECTIVES.ProgramManager, programId)} onViewAs={viewAsProgram} /></TabsContent>
      <TabsContent value="projects"><PortfolioProjects data={data} t={t} page={projectsPage} onPage={setProjectsPage} canViewAs={(projectId) => canViewAs(MANAGEMENT_PERSPECTIVES.ProjectManager, projectId)} onViewAs={viewAsProject} /></TabsContent>
      <TabsContent value="delivery"><PortfolioDelivery data={data} t={t} formatDate={formatDate} /></TabsContent>
      {canViewFinancials ? <TabsContent value="financials"><FinancialRollupView kind="Portfolio" id={id} enabled={tab === "financials"} scopeKey={cacheScope} /></TabsContent> : null}
      <TabsContent value="risks"><RisksIssues data={data} t={t} formatDate={formatOptionalDate} /></TabsContent>
      <TabsContent value="resources"><Resources data={data} t={t} /></TabsContent>
      <TabsContent value="statusReports"><StatusReports data={data} t={t} formatDate={formatOptionalDate} showSummary /></TabsContent>
      <TabsContent value="activity"><Meetings data={data} t={t} formatDate={formatDate} /></TabsContent>
      {canSeeHandoverTab ? <TabsContent value="handover"><ManagerHandoverPanel scope="portfolios" entityId={id} entityName={portfolio.name} currentManagerId={currentManagerId} currentManagerName={currentManagerName} currentManagerLoading={currentManagerLoading} currentManagerError={currentManagerError} onRetryCurrentManager={() => void portfolioQuery.refetch()} cacheScope={cacheScope} canView={canViewHandover} canManage={canManageHandover} canAdminInitiate={canAdminInitiateHandover} canApprove={canApproveHandover} canMutate={canMutate && !effectiveReadOnly} /></TabsContent> : null}
    </Tabs>
    <PortfolioSheet open={editOpen} onOpenChange={setEditOpen} portfolio={portfolioQuery.data} canSave={canEdit} canAssignInitialManager={canAssignInitialManager} />
  </div>;
}

export function ManagedProgramDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, formatDate } = useI18n();
  const confirm = useConfirm();
  const formatOptionalDate = (value?: string) => value ? formatDate(value) : "—";
  const { user } = useAuth();
  const { context, loading: managementLoading, canMutate, effectiveReadOnly } = useManagement();
  const cacheScope = managementCacheScopeKey(user?.id, context);
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Program.Edit) && canMutate && !effectiveReadOnly);
  const canArchive = Boolean(user && hasPermission(user.permissions, Permissions.Program.Archive) && canMutate && !effectiveReadOnly);
  const canReactivate = Boolean(user && hasPermission(user.permissions, Permissions.Program.Reactivate) && canMutate && !effectiveReadOnly);
  const canAssignInitialManager = Boolean(user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const canViewHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.View));
  const canManageHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage));
  const canAdminInitiateHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const canApproveHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Approve));
  const canSeeProgramHandoverTab = canViewHandover || canManageHandover || canAdminInitiateHandover || canApproveHandover;
  const canViewFinancials = Boolean(user
    && hasPermission(user.permissions, Permissions.Program.View)
    && hasPermission(user.permissions, Permissions.ProgramFinancialRollup.View));
  const [tab, setTab] = React.useState<Exclude<Tab, "programs">>("overview");
  const [projectsPage, setProjectsPage] = React.useState(1);
  const [editOpen, setEditOpen] = React.useState(false);
  const [actionError, setActionError] = React.useState("");
  const query = useProgramManagementOverview(id, { projectsPage, projectsPageSize: 25 }, !managementLoading, cacheScope);
  const programQuery = useProgram(id, canEdit && !managementLoading, cacheScope);
  const archive = useArchiveProgram();
  const reactivate = useReactivateProgram();
  if (managementLoading || query.isLoading) return <Loading />;
  if (query.isError || !query.data) return <ErrorState text={t("programManager.loadFailed")} onRetry={() => void query.refetch()} />;
  const data = query.data;
  const program = data.program;
  const archiveProgram = async () => {
    if (!await confirm({ description: t("programManager.archiveConfirm"), destructive: true })) return;
    setActionError("");
    try {
      await archive.mutateAsync(id);
      navigate("/portfolios/programs");
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("programManager.programSaveFailed"));
    }
  };
  const reactivateProgram = async () => {
    setActionError("");
    try { await reactivate.mutateAsync(id); }
    catch (reason) { setActionError(reason instanceof Error ? reason.message : t("programManager.programSaveFailed")); }
  };
  const actions = <div className="flex flex-wrap items-center gap-2">
    {canEdit ? <Button size="sm" variant="outline" onClick={() => {
      setActionError("");
      if (programQuery.data) setEditOpen(true);
    }} disabled={!programQuery.data}>{t("programManager.editProgram")}</Button> : null}
    {canArchive && program.status !== "Archived" ? <Button size="sm" variant="outline" onClick={() => void archiveProgram()} disabled={archive.isPending}>{archive.isPending ? t("programManager.saving") : t("programManager.archive")}</Button> : null}
    {canReactivate && program.status === "Archived" ? <Button size="sm" variant="outline" onClick={() => void reactivateProgram()} disabled={reactivate.isPending}>{reactivate.isPending ? t("programManager.saving") : t("programManager.reactivate")}</Button> : null}
    <Status value={program.status} label={localizedStatus(t, program.status)} />{program.health ? <Status value={program.health} label={localizedStatus(t, program.health)} /> : null}{effectiveReadOnly ? <Status value="ReadOnly" label={t("programManager.readOnly")} /> : null}
  </div>;
  const tabs: [Exclude<Tab, "programs">, string][] = [["overview", "programManager.overview"], ["projects", "programManager.projects"], ["delivery", "programManager.delivery"], ...(canViewFinancials ? [["financials", "programManager.financials"] as [Exclude<Tab, "programs">, string]] : []), ["risks", "programManager.risksIssues"], ["resources", "programManager.resources"], ["statusReports", "programManager.statusReports"], ["activity", "programManager.meetingsActivity"]];
  if (canSeeProgramHandoverTab) tabs.push(["handover", "managerHandover.handover"]);
  const visibleActionError = actionError || (canEdit && programQuery.isError ? t("programManager.loadProgramForEditFailed") : "");
  return <div className="space-y-5">
    <PageHeader title={program.name} description={`${t("programManager.programWorkspace")} · ${program.code ?? "—"}`} actions={actions} />
    {visibleActionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{visibleActionError}</p> : null}
    <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-card px-3 py-2 text-xs text-muted-foreground">
      <span className="font-medium text-foreground">{t("programManager.program")}: {program.name}</span>
      {program.portfolioName ? <><span aria-hidden="true">·</span><span>{t("programManager.portfolio")}: {program.portfolioName}</span></> : null}
      {context?.viewMode === "ReadOnlyViewAs" ? <Status value="ReadOnly" label={t("programManager.readOnly")} /> : null}
    </div>
    <Tabs value={tab} onValueChange={(value) => setTab(value as Exclude<Tab, "programs">)}>
      <div className="nexus-fullbleed-tabs sticky top-0 z-20 isolate border-y border-border bg-muted/70 py-1">
        <TabsList aria-label={t("programManager.programWorkspace")} className={projectTabListClass}>
          {tabs.map(([key, label]) => { const Icon = TAB_ICONS[key]; return <TabsTrigger key={key} value={key} className={projectTabTriggerClass}><Icon className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />{t(label)}</TabsTrigger>; })}
        </TabsList>
      </div>
      <TabsContent value="overview" className="space-y-5">
        <ProgramOverview data={data} t={t} />
        <ProgramAttention data={data} t={t} />
        <ProgramDashboard data={data} t={t} formatDate={formatDate} />
      </TabsContent>
      <TabsContent value="projects"><ProgramProjects data={data} t={t} formatDate={formatDate} page={projectsPage} onPage={setProjectsPage} /></TabsContent>
      <TabsContent value="delivery"><ProgramDelivery data={data} t={t} formatDate={formatDate} /></TabsContent>
      {canViewFinancials ? <TabsContent value="financials"><FinancialRollupView kind="Program" id={id} enabled={tab === "financials"} scopeKey={cacheScope} /></TabsContent> : null}
      <TabsContent value="risks"><RisksIssues data={data} t={t} formatDate={formatOptionalDate} /></TabsContent>
      <TabsContent value="resources"><Resources data={data} t={t} /></TabsContent>
      <TabsContent value="statusReports"><StatusReports data={data} t={t} formatDate={formatOptionalDate} /></TabsContent>
      <TabsContent value="activity"><Meetings data={data} t={t} formatDate={formatDate} /></TabsContent>
      {canSeeProgramHandoverTab ? <TabsContent value="handover"><ManagerHandoverPanel scope="programs" entityId={id} entityName={program.name} currentManagerId={program.programManagerId} currentManagerName={program.programManagerName} cacheScope={cacheScope} canView={canViewHandover} canManage={canManageHandover} canAdminInitiate={canAdminInitiateHandover} canApprove={canApproveHandover} canMutate={canMutate && !effectiveReadOnly} /></TabsContent> : null}
    </Tabs>
    {canEdit && programQuery.data ? <ProgramSheet open={editOpen} onOpenChange={setEditOpen} program={programQuery.data} canSave={canEdit} canAssignInitialManager={canAssignInitialManager} cacheScope={cacheScope} /> : null}
  </div>;
}

function PortfolioOverview({ data, t, formatPercent, formatDate }: { data: PortfolioManagementOverview; t: (key: string, params?: Record<string, string | number>) => string; formatPercent: (value: number) => string; formatDate: (value?: string) => string }) {
  return <div className="space-y-5">
    <Kpis values={[[t("portfolioManager.totalPrograms"), data.summary.totalPrograms], [t("portfolioManager.totalProjects"), data.summary.totalProjects], [t("portfolioManager.directProjects"), data.summary.directPortfolioProjects], [t("portfolioManager.projectsAtRisk"), data.summary.atRiskProjects], [t("portfolioManager.criticalProjects"), data.summary.criticalProjects]]} />
    <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
      <Identity title={t("portfolioManager.portfolioIdentity")} values={[[t("portfolioManager.portfolio"), data.portfolio.name], [t("portfolioManager.code"), data.portfolio.code], [t("portfolioManager.owner"), data.portfolio.ownerName], [t("portfolioManager.baseCurrency"), data.portfolio.currency], [t("portfolioManager.strategicObjective"), data.portfolio.strategicObjective], [t("portfolioManager.priority"), data.portfolio.priority]]} />
      <Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.structureSummary")}</CardTitle></CardHeader><CardContent className="space-y-2 text-sm"><Metric label={t("portfolioManager.programProjects")} value={data.summary.programContainedProjects} /><Metric label={t("portfolioManager.directProjects")} value={data.summary.directPortfolioProjects} /><Metric label={t("portfolioManager.activeProjects")} value={data.summary.activeProjects} /><Metric label={t("portfolioManager.completedProjects")} value={data.summary.completedProjects} /><Metric label={t("portfolioManager.averageProgress")} value={data.summary.averageProjectProgress == null ? "—" : formatPercent(data.summary.averageProjectProgress)} /><Metric label={t("portfolioManager.description")} value={data.portfolio.description || "—"} /></CardContent></Card>
    </div>
    <PortfolioAttention data={data} t={t} />
    <PortfolioDashboard data={data} t={t} formatDate={formatDate} />
  </div>;
}

function PortfolioDashboard({ data, t, formatDate }: { data: PortfolioManagementOverview; t: (key: string) => string; formatDate: (value?: string) => string }) {
  return <section aria-label={t("portfolioManager.overview")} className="space-y-2"><p className="text-xs text-muted-foreground">{t("portfolioManager.dataSource")}</p><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{portfolioOverviewWidgets.map(([id, titleKey]) => <Card key={id} data-widget-id={id}><CardHeader className="border-b px-4 py-2.5"><CardTitle className="text-sm">{t(titleKey)}</CardTitle></CardHeader><CardContent className="p-3"><PortfolioOverviewWidget id={id} data={data} t={t} formatDate={formatDate} /></CardContent></Card>)}</div></section>;
}

function PortfolioOverviewWidget({ id, data, t, formatDate }: { id: (typeof portfolioOverviewWidgets)[number][0]; data: PortfolioManagementOverview; t: (key: string) => string; formatDate: (value?: string) => string }) {
  if (id === "portfolio.health-overview") return <div className="space-y-3"><div className="flex items-center justify-between gap-2"><span className="text-sm text-muted-foreground">{t("portfolioManager.portfolioHealth")}</span><Status value={data.portfolio.health} label={localizedStatus(t, data.portfolio.health)} /></div><div className="grid gap-3 sm:grid-cols-2"><Distribution label={t("portfolioManager.programHealth")} values={data.summary.programHealthDistribution} t={t} /><Distribution label={t("portfolioManager.projectHealth")} values={data.summary.projectHealthDistribution} t={t} /></div></div>;
  if (id === "portfolio.delivery-milestones") return <div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><Metric label={t("portfolioManager.delayedProjects")} value={data.delivery.delayedProjects} valueClassName="text-destructive" /><Metric label={t("portfolioManager.delayedPrograms")} value={data.delivery.delayedPrograms} valueClassName="text-destructive" /><Metric label={t("portfolioManager.scheduleData")} value={data.delivery.projectsWithScheduleData} /><Metric label={t("portfolioManager.atRiskProjects")} value={data.summary.atRiskProjects} valueClassName="text-warning" /><Metric label={t("portfolioManager.overdueMilestones")} value={data.delivery.overdueMilestones} valueClassName="text-destructive" /><Metric label={t("portfolioManager.dueSoonMilestones")} value={data.delivery.dueSoonMilestones} valueClassName="text-warning" /><Metric label={t("portfolioManager.onTrack")} value={data.delivery.onTrackMilestones} valueClassName="text-success" /><Metric label={t("portfolioManager.completed")} value={data.delivery.completedMilestones} valueClassName="text-success" /></div>;
  if (id === "portfolio.risk-issues") return <div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><Metric label={t("portfolioManager.openRisks")} value={data.risks.openCount} valueClassName="text-warning" /><Metric label={t("portfolioManager.critical")} value={data.risks.criticalCount} valueClassName="text-destructive" /><Metric label={t("portfolioManager.high")} value={data.risks.highCount} valueClassName="text-warning" /><Metric label={t("portfolioManager.sourceScope")} value={localizedStatus(t, data.risks.sourceScope)} /><Metric label={t("portfolioManager.openIssues")} value={data.issues.openCount} valueClassName="text-warning" /><Metric label={t("portfolioManager.critical")} value={data.issues.criticalCount} valueClassName="text-destructive" /><Metric label={t("portfolioManager.overdue")} value={data.issues.overdueCount} valueClassName="text-destructive" /></div>;
  if (id === "portfolio.resources") return <div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><Metric label={t("portfolioManager.activeResources")} value={data.resources.activeResourceCount} /><Metric label={t("portfolioManager.sharedResources")} value={data.resources.sharedResourceCount} /><Metric label={t("portfolioManager.crossProgram")} value={data.resources.crossProgramResourceCount} /><Metric label={t("portfolioManager.overAllocated")} value={data.resources.overAllocatedResourceCount} valueClassName="text-destructive" /></div>;
  if (id === "portfolio.status-reports") return <div className="grid grid-cols-3 gap-2 text-sm"><Metric label={t("portfolioManager.currentReports")} value={data.statusReports.currentCount} valueClassName="text-success" /><Metric label={t("portfolioManager.staleReports")} value={data.statusReports.staleCount} valueClassName="text-warning" /><Metric label={t("portfolioManager.missingReports")} value={data.statusReports.missingCount} valueClassName="text-destructive" /></div>;
  return <div className="space-y-2"><Metric label={t("portfolioManager.upcomingMeetings")} value={data.upcomingMeetings.length} />{data.upcomingMeetings.slice(0, 3).map((item) => <p key={item.id} className="truncate text-xs text-muted-foreground">{formatDate(item.startTime)} · {item.title}</p>)}</div>;
}

function PortfolioAttention({ data, t }: { data: PortfolioManagementOverview; t: (key: string) => string }) {
  return <Card><CardHeader><CardTitle className="text-sm">{t("portfolioManager.attentionAreas")}</CardTitle></CardHeader><CardContent>{data.attentionItems.length ? <div className="grid gap-2 md:grid-cols-2">{data.attentionItems.map((item, index) => <div key={`${item.kind}-${item.entityId}-${index}`} className="flex items-start justify-between gap-3 rounded-md border p-3 text-sm"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><Status value={item.level} label={localizedStatus(t, item.level)} /><span className="font-medium">{item.entityName}</span></div><p className="mt-1 text-xs text-muted-foreground">{item.reason}</p></div>{item.entityType === "Program" ? <Link className="shrink-0 text-xs hover:text-primary" to={`/portfolios/programs/${encodeURIComponent(item.entityId)}`}>{t("portfolioManager.open")}</Link> : item.projectId ? <Link className="shrink-0 text-xs hover:text-primary" to={projectHref(item) ?? "/projects"}>{t("portfolioManager.open")}</Link> : null}</div>)}</div> : <p className="text-sm text-muted-foreground">{t("portfolioManager.noAttention")}</p>}</CardContent></Card>;
}

function Distribution({ values, t, label }: { values: Record<string, number>; t: (key: string) => string; label: string }) {
  const entries = Object.entries(values);
  return <div className="min-w-0"><p className="mb-1 text-xs font-medium text-muted-foreground">{label}</p>{entries.length ? <div className="space-y-1">{entries.map(([name, count]) => <div key={name} className="flex items-center justify-between gap-3 text-xs"><span className={getStatusTextClass(name)}>{localizedStatus(t, name)}</span><span className={`font-semibold tabular-nums ${getStatusTextClass(name)}`}>{count}</span></div>)}</div> : <p className="text-xs text-muted-foreground">{t("portfolioManager.noData")}</p>}</div>;
}

function ProgramOverview({ data, t }: { data: ProgramManagementOverview; t: (key: string, params?: Record<string, string | number>) => string }) {
  return <>
    <Kpis values={[
      [t("programManager.totalProjects"), data.summary.totalProjects],
      [t("programManager.activeProjects"), data.summary.activeProjects],
      [t("programManager.completedProjects"), data.summary.completedProjects, "text-success"],
      [t("programManager.projectsAtRisk"), data.summary.projectsAtRisk, "text-warning"],
      [t("programManager.criticalProjects"), data.summary.criticalProjects, "text-destructive"],
    ]} />
    <Card><CardHeader><CardTitle className="text-base">{t("programManager.programIdentity")}</CardTitle></CardHeader><CardContent className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
      <IdentityValue label={t("programManager.status")} value={data.program.status} />
      <IdentityValue label={t("programManager.health")} value={data.program.health} />
      <IdentityValue label={t("programManager.manager")} value={data.program.programManagerName} />
      <IdentityValue label={t("programManager.portfolio")} value={data.program.portfolioName} />
      <IdentityValue label={t("programManager.currency")} value={data.program.currency} />
    </CardContent></Card>
  </>;
}

const programOverviewWidgets = [
  ["program.health-overview", "programManager.healthOverview"],
  ["program.delivery-milestones", "programManager.deliveryAndMilestones"],
  ["program.risk-issues", "programManager.riskAndIssues"],
  ["program.resources", "programManager.resourceSummary"],
  ["program.status-reports", "programManager.statusReports"],
  ["program.activity", "programManager.meetingsActivity"],
] as const;

function ProgramAttention({ data, t }: { data: ProgramManagementOverview; t: (key: string) => string }) {
  return <Card><CardHeader><CardTitle className="text-sm">{t("programManager.attentionAreas")}</CardTitle></CardHeader><CardContent>{data.attentionItems.length ? <div className="divide-y">{data.attentionItems.map((item) => <div key={`${item.kind}-${item.itemId ?? item.projectId ?? item.message}`} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0"><Status value={item.level} label={t(`programManager.${attentionKindTranslationKey(item.level)}`)} /><div className="min-w-0 flex-1 text-sm"><span className="font-medium">{t(`programManager.${attentionKindTranslationKey(item.kind)}`)}</span><span className="mx-1 text-muted-foreground">·</span>{item.projectId ? <Link to={projectHref(item) ?? "/projects"} className="hover:text-primary">{item.message}</Link> : item.message}</div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("programManager.noAttention")}</p>}</CardContent></Card>;
}

function ProgramDashboard({ data, t, formatDate }: { data: ProgramManagementOverview; t: (key: string) => string; formatDate: (value: string) => string }) {
  return <section aria-label={t("programManager.dashboard")} className="space-y-2"><p className="text-xs text-muted-foreground">{t("programManager.dataSource")}</p><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{programOverviewWidgets.map(([id, titleKey]) => <Card key={id} data-widget-id={id}><CardHeader className="border-b px-4 py-2.5"><CardTitle className="text-sm">{t(titleKey)}</CardTitle></CardHeader><CardContent className="p-3"><ProgramOverviewWidget id={id} data={data} t={t} formatDate={formatDate} /></CardContent></Card>)}</div></section>;
}

function ProgramOverviewWidget({ id, data, t, formatDate }: { id: (typeof programOverviewWidgets)[number][0]; data: ProgramManagementOverview; t: (key: string) => string; formatDate: (value: string) => string }) {
  if (id === "program.health-overview") return <div className="space-y-3"><div className="flex items-center justify-between gap-2"><span className="text-sm text-muted-foreground">{t("programManager.programHealth")}</span><Status value={data.program.health} label={data.program.health ? localizedStatus(t, data.program.health) : t("programManager.unknown")} /></div><div className="space-y-1">{Object.entries(data.summary.projectHealthDistribution).map(([health, count]) => <div key={health} className="flex items-center justify-between text-xs"><span className={getStatusTextClass(health)}>{localizedStatus(t, health)}</span><span className={`font-semibold tabular-nums ${getStatusTextClass(health)}`}>{count}</span></div>)}</div></div>;
  if (id === "program.delivery-milestones") return <div className="space-y-3"><div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><Metric label={t("programManager.averageProgress")} value={data.summary.averageProjectProgress == null ? "—" : `${data.summary.averageProjectProgress}%`} /><Metric label={t("programManager.projectsBehindSchedule")} value={data.delivery.projectsBehindSchedule} valueClassName="text-destructive" /><Metric label={t("programManager.scheduleData")} value={data.delivery.projectsWithScheduleData} /><Metric label={t("programManager.milestones")} value={data.delivery.totalMilestones} /><Metric label={t("programManager.overdue")} value={data.delivery.overdueMilestones} valueClassName="text-destructive" /><Metric label={t("programManager.dueSoon")} value={data.delivery.dueSoonMilestones} valueClassName="text-warning" /><Metric label={t("programManager.onTrack")} value={data.delivery.onTrackMilestones} valueClassName="text-success" /><Metric label={t("programManager.completed")} value={data.delivery.completedMilestones} valueClassName="text-success" /></div><MiniLinks items={data.milestones.slice(0, 2).map((item) => ({ id: item.id, label: `${item.name} · ${item.projectName}`, badge: item.state, href: projectHref(item) }))} empty={t("programManager.noMilestones")} /></div>;
  if (id === "program.risk-issues") return <div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><Metric label={t("programManager.riskSummary")} value={data.risks.openCount} valueClassName="text-warning" hint={Object.entries(data.risks.bySeverity).map(([key, value]) => `${localizedStatus(t, key)}: ${value}`).join(" · ")} /><Metric label={t("programManager.overdue")} value={data.risks.overdueCount} valueClassName="text-destructive" /><Metric label={t("programManager.issueSummary")} value={data.issues.openCount} valueClassName="text-warning" hint={Object.entries(data.issues.bySeverity).map(([key, value]) => `${localizedStatus(t, key)}: ${value}`).join(" · ")} /><Metric label={t("programManager.overdue")} value={data.issues.overdueCount} valueClassName="text-destructive" /></div>;
  if (id === "program.resources") return <div className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm"><Metric label={t("programManager.activeResources")} value={data.resources.activeResourceCount} /><Metric label={t("programManager.activeAllocations")} value={data.resources.activeAllocationCount} /><Metric label={t("programManager.overAllocated")} value={data.resources.overAllocatedResourceCount} valueClassName="text-destructive" /><Metric label={t("programManager.hoursPerWeek")} value={data.resources.allocatedHoursPerWeek} /></div>;
  if (id === "program.status-reports") return <div className="grid grid-cols-3 gap-2 text-sm"><Metric label={t("programManager.currentReports")} value={data.statusReports.currentCount} valueClassName="text-success" /><Metric label={t("programManager.staleReports")} value={data.statusReports.staleCount} valueClassName="text-warning" /><Metric label={t("programManager.missingReports")} value={data.statusReports.missingCount} valueClassName="text-destructive" /></div>;
  return <MiniLinks items={data.upcomingMeetings.slice(0, 4).map((item) => ({ id: item.id, label: `${item.title} · ${item.projectName ?? ""}`, badge: item.startTime ? formatDate(item.startTime) : "—", href: projectHref(item) }))} empty={t("programManager.noMeetings")} />;
}

function attentionKindTranslationKey(value: string): string {
  return ({ NeedsAttention: "needsAttention", Informational: "informational", Critical: "critical", Project: "project", Milestone: "milestone", Risk: "risk", Issue: "issue", ChangeRequest: "changeRequest", StatusReport: "statusReport" } as Record<string, string>)[value] ?? "unknown";
}

function IdentityValue({ label, value, valueClassName }: { label: string; value?: string; valueClassName?: string }) {
  return <div><dt className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</dt><dd className={`mt-1 font-medium ${valueClassName ?? ""}`}>{value || "—"}</dd></div>;
}

function PortfolioPrograms({ data, t, page, onPage, canViewAs, onViewAs }: { data: PortfolioManagementOverview; t: (key: string) => string; page: number; onPage: (page: number) => void; canViewAs: (id: string) => boolean; onViewAs: (id: string) => void }) {
  return <Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.programs")}</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.program")}</TableHead><TableHead>{t("portfolioManager.manager")}</TableHead><TableHead>{t("portfolioManager.status")}</TableHead><TableHead>{t("portfolioManager.health")}</TableHead><TableHead>{t("portfolioManager.projects")}</TableHead><TableHead>{t("portfolioManager.delivery")}</TableHead><TableHead>{t("portfolioManager.reporting")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{data.programs.length ? data.programs.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" to={`/portfolios/programs/${encodeURIComponent(item.id)}`}>{item.name}</Link><div className="text-xs text-muted-foreground">{item.code ?? "—"} · {item.currency}</div></TableCell><TableCell>{item.programManagerName ?? "—"}</TableCell><TableCell><Status value={item.status} label={localizedStatus(t, item.status)} /></TableCell><TableCell><Status value={item.health} label={localizedStatus(t, item.health)} /></TableCell><TableCell>{item.projectCount}<span className="text-xs text-muted-foreground"> · {item.projectsAtRisk} {t("portfolioManager.atRiskShort")}</span></TableCell><TableCell><Status value={item.deliveryIndicator} label={localizedStatus(t, item.deliveryIndicator)} /></TableCell><TableCell><Status value={item.reportingIndicator} label={portfolioReportingLabel(t, item.reportingIndicator)} /></TableCell><TableCell>{canViewAs(item.id) ? <Button size="sm" variant="outline" onClick={() => onViewAs(item.id)} >{t("portfolioManager.viewAsProgram")}</Button> : null}</TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noPrograms")} colSpan={8} />}</TableBody></Table></div><Pager page={page} pages={data.programsTotalPages} onPage={onPage} t={t} /></CardContent></Card>;
}

function PortfolioProjects({ data, t, page, onPage, canViewAs, onViewAs }: { data: PortfolioManagementOverview; t: (key: string) => string; page: number; onPage: (page: number) => void; canViewAs: (id: string) => boolean; onViewAs: (id: string, publicId: string) => void }) {
  return <Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.projects")}</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.project")}</TableHead><TableHead>{t("portfolioManager.relationship")}</TableHead><TableHead>{t("portfolioManager.manager")}</TableHead><TableHead>{t("portfolioManager.status")}</TableHead><TableHead>{t("portfolioManager.health")}</TableHead><TableHead>{t("portfolioManager.progress")}</TableHead><TableHead>{t("portfolioManager.schedule")}</TableHead><TableHead>{t("portfolioManager.reporting")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{data.projects.length ? data.projects.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.name}</Link><div className="text-xs text-muted-foreground">{item.code} · {item.currency}</div></TableCell><TableCell>{item.isDirectProject ? t("portfolioManager.directProject") : item.programId ? <Link className="text-xs hover:text-primary" to={`/portfolios/programs/${encodeURIComponent(item.programId)}`}>{item.programName ?? t("portfolioManager.programProject")}</Link> : t("portfolioManager.programProject")}</TableCell><TableCell>{item.projectManagerName ?? "—"}</TableCell><TableCell><Status value={item.status} label={localizedStatus(t, item.status)} /></TableCell><TableCell><Status value={item.health} label={localizedStatus(t, item.health)} /></TableCell><TableCell><div className="min-w-28"><div className="h-1.5 overflow-hidden rounded-full bg-muted"><div className="h-full rounded-full bg-primary" style={{ width: `${Math.max(0, Math.min(item.progress, 100))}%` }} /></div><span className="text-xs text-muted-foreground">{item.progress}%</span></div></TableCell><TableCell><Status value={item.scheduleCondition} label={localizedStatus(t, item.scheduleCondition)} /></TableCell><TableCell><Status value={item.statusReportFreshness} label={localizedStatus(t, item.statusReportFreshness)} /></TableCell><TableCell>{canViewAs(item.id) ? <Button size="sm" variant="outline" onClick={() => { const publicId = projectPublicId(item); if (publicId) onViewAs(item.id, publicId); }}>{t("portfolioManager.viewAsProject")}</Button> : null}</TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noProjects")} colSpan={9} />}</TableBody></Table></div><Pager page={page} pages={data.projectsTotalPages} onPage={onPage} t={t} /></CardContent></Card>;
}

function ProgramProjects({ data, t, formatDate, page, onPage }: { data: ProgramManagementOverview; t: (key: string) => string; formatDate: (date: string) => string; page: number; onPage: (page: number) => void }) {
  return <Card><CardHeader><CardTitle className="text-base">{t("programManager.projects")}</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("programManager.project")}</TableHead><TableHead>{t("programManager.manager")}</TableHead><TableHead>{t("programManager.status")}</TableHead><TableHead>{t("programManager.health")}</TableHead><TableHead>{t("programManager.progress")}</TableHead><TableHead>{t("programManager.scheduleCondition")}</TableHead><TableHead>{t("programManager.statusReport")}</TableHead></TableRow></TableHeader><TableBody>{data.projects.length ? data.projects.map((item) => <TableRow key={item.id}><TableCell><Link className="font-medium hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.name}<span className="ms-1 text-xs text-muted-foreground">{item.code}</span></Link></TableCell><TableCell>{item.projectManagerName ?? "—"}</TableCell><TableCell><Status value={item.status} label={localizedStatus(t, item.status)} /></TableCell><TableCell><Status value={item.health} label={localizedStatus(t, item.health)} /></TableCell><TableCell><div className="min-w-28"><div className="h-1.5 overflow-hidden rounded-full bg-muted"><div className="h-full rounded-full bg-primary" style={{ width: `${Math.max(0, Math.min(item.progress, 100))}%` }} /></div><span className="text-xs text-muted-foreground">{item.progress}%</span></div></TableCell><TableCell><Status value={item.scheduleCondition} label={localizedStatus(t, item.scheduleCondition)} />{item.forecastEndDate ? <div className="mt-1 text-xs text-muted-foreground">{formatDate(item.forecastEndDate)}</div> : null}</TableCell><TableCell><Status value={item.statusReportState} label={localizedStatus(t, item.statusReportState)} />{item.latestStatusReportDate ? <div className="mt-1 text-xs text-muted-foreground">{formatDate(item.latestStatusReportDate)}</div> : null}</TableCell></TableRow>) : <EmptyRow text={t("programManager.noData")} colSpan={7} />}</TableBody></Table></div><Pager page={page} pages={data.projectsTotalPages} onPage={onPage} t={t} /></CardContent></Card>;
}

function PortfolioDelivery({ data, t, formatDate }: { data: PortfolioManagementOverview; t: (key: string) => string; formatDate: (date: string) => string }) { return <div className="space-y-4"><Kpis values={[[t("portfolioManager.delayedProjects"), data.delivery.delayedProjects, "text-destructive"], [t("portfolioManager.delayedPrograms"), data.delivery.delayedPrograms, "text-destructive"], [t("portfolioManager.overdueMilestones"), data.delivery.overdueMilestones, "text-destructive"], [t("portfolioManager.dueSoonMilestones"), data.delivery.dueSoonMilestones, "text-warning"], [t("portfolioManager.completed"), data.delivery.completedMilestones, "text-success"]]} /><Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.milestones")}</CardTitle></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.milestone")}</TableHead><TableHead>{t("portfolioManager.project")}</TableHead><TableHead>{t("portfolioManager.program")}</TableHead><TableHead>{t("portfolioManager.dueDate")}</TableHead><TableHead>{t("portfolioManager.status")}</TableHead></TableRow></TableHeader><TableBody>{data.milestones.length ? data.milestones.map((item) => <TableRow key={item.id}><TableCell>{item.name}</TableCell><TableCell><Link className="hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.projectName}</Link></TableCell><TableCell>{item.programName ?? t("portfolioManager.directProject")}</TableCell><TableCell>{item.dueDate ? formatDate(item.dueDate) : "—"}</TableCell><TableCell><Status value={item.state} label={localizedStatus(t, item.state)} /></TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noData")} colSpan={5} />}</TableBody></Table></CardContent></Card></div>; }
function ProgramDelivery({ data, t, formatDate }: { data: ProgramManagementOverview; t: (key: string) => string; formatDate: (date: string) => string }) { return <div className="space-y-4"><Kpis values={[[t("programManager.projectsBehindSchedule"), data.delivery.projectsBehindSchedule, "text-destructive"], [t("programManager.scheduleData"), data.delivery.projectsWithScheduleData], [t("programManager.overdue"), data.delivery.overdueMilestones, "text-destructive"], [t("programManager.dueSoon"), data.delivery.dueSoonMilestones, "text-warning"], [t("programManager.completed"), data.delivery.completedMilestones, "text-success"]]} /><Milestones items={data.milestones} t={t} formatDate={formatDate} /> </div>; }
function Milestones({ items, t, formatDate }: { items: { id: string; projectId: string; projectName: string; name: string; dueDate?: string; state: string }[]; t: (key: string) => string; formatDate: (date: string) => string }) { return <Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.milestones")}</CardTitle></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.milestone")}</TableHead><TableHead>{t("portfolioManager.project")}</TableHead><TableHead>{t("portfolioManager.dueDate")}</TableHead><TableHead>{t("portfolioManager.status")}</TableHead></TableRow></TableHeader><TableBody>{items.length ? items.map((item) => <TableRow key={item.id}><TableCell>{item.name}</TableCell><TableCell><Link className="hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.projectName}</Link></TableCell><TableCell>{item.dueDate ? formatDate(item.dueDate) : "—"}</TableCell><TableCell><Status value={item.state} /></TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noData")} colSpan={4} />}</TableBody></Table></CardContent></Card>; }

function RisksIssues({ data, t, formatDate }: { data: PortfolioManagementOverview | ProgramManagementOverview; t: (key: string) => string; formatDate: (date?: string) => string }) {
  return <div className="grid gap-4 lg:grid-cols-2"><Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.riskSummary")}</CardTitle><p className="text-sm text-muted-foreground">{data.risks.openCount} {t("portfolioManager.openRisks")} · {data.risks.overdueCount} {t("portfolioManager.overdue")}</p></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.risk")}</TableHead><TableHead>{t("portfolioManager.project")}</TableHead><TableHead>{t("portfolioManager.severity")}</TableHead><TableHead>{t("portfolioManager.status")}</TableHead><TableHead>{t("portfolioManager.identifiedDate")}</TableHead></TableRow></TableHeader><TableBody>{data.topRisks.length ? data.topRisks.map((item) => <TableRow key={item.id}><TableCell>{item.title}</TableCell><TableCell><Link className="hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.projectName}</Link></TableCell><TableCell><Status value={item.severity} label={localizedStatus(t, item.severity)} /></TableCell><TableCell><Status value={item.status} label={localizedStatus(t, item.status)} /></TableCell><TableCell>{formatDate(item.identifiedDate)}</TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noRisks")} colSpan={5} />}</TableBody></Table></CardContent></Card><Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.issueSummary")}</CardTitle><p className="text-sm text-muted-foreground">{data.issues.openCount} {t("portfolioManager.openIssues")} · {data.issues.overdueCount} {t("portfolioManager.overdue")}</p></CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.issue")}</TableHead><TableHead>{t("portfolioManager.project")}</TableHead><TableHead>{t("portfolioManager.severity")}</TableHead><TableHead>{t("portfolioManager.dueDate")}</TableHead></TableRow></TableHeader><TableBody>{data.topIssues.length ? data.topIssues.map((item) => <TableRow key={item.id}><TableCell>{item.title}</TableCell><TableCell><Link className="hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.projectName}</Link></TableCell><TableCell><Status value={item.severity} label={localizedStatus(t, item.severity || item.priority)} /></TableCell><TableCell>{formatDate(item.targetResolutionDate)}</TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noIssues")} colSpan={4} />}</TableBody></Table></CardContent></Card></div>;
}
function Resources({ data, t }: { data: PortfolioManagementOverview | ProgramManagementOverview; t: (key: string) => string }) {
  const portfolioScope = "crossProgramResourceCount" in data.resources;
  const metrics: [string, string | number][] = "crossProgramResourceCount" in data.resources
    ? [[t("portfolioManager.activeResources"), data.resources.activeResourceCount], [t("portfolioManager.sharedResources"), data.resources.sharedResourceCount], [t("portfolioManager.crossProgram"), data.resources.crossProgramResourceCount], [t("portfolioManager.overAllocated"), data.resources.overAllocatedResourceCount], [t("portfolioManager.hoursPerWeek"), data.resources.allocatedHoursPerWeek]]
    : [[t("programManager.activeAllocations"), data.resources.activeAllocationCount], [t("portfolioManager.activeResources"), data.resources.activeResourceCount], [t("portfolioManager.sharedResources"), data.resources.sharedResourceCount], [t("portfolioManager.overAllocated"), data.resources.overAllocatedResourceCount]];
  return <div className="space-y-4"><Kpis values={metrics} /><Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.resourcePressure")}</CardTitle></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.resource")}</TableHead><TableHead>{t("portfolioManager.allocation")}</TableHead>{portfolioScope ? <TableHead>{t("portfolioManager.programs")}</TableHead> : null}<TableHead>{t("portfolioManager.projects")}</TableHead><TableHead>{t("portfolioManager.competingProjects")}</TableHead></TableRow></TableHeader><TableBody>{data.resources.pressure.length ? data.resources.pressure.map((item) => <TableRow key={item.resourceId}><TableCell>{item.resourceName}</TableCell><TableCell><Status value={item.allocationPercentage > 100 ? "Critical" : item.allocationPercentage > 80 ? "Warning" : "OnTrack"} label={`${item.allocationPercentage}%`} /></TableCell>{portfolioScope ? <TableCell>{"programCount" in item ? item.programCount : 0}</TableCell> : null}<TableCell>{item.projectCount}</TableCell><TableCell><div className="flex flex-wrap gap-x-2 gap-y-1">{item.projects.map((project) => <Link key={project.projectId} className="text-xs hover:text-primary" to={projectHref(project) ?? "/projects"}>{project.projectName}</Link>)}</div></TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noPressure")} colSpan={portfolioScope ? 5 : 4} />}</TableBody></Table></div></CardContent></Card></div>;
}
function StatusReports({ data, t, formatDate, showSummary = false }: { data: PortfolioManagementOverview | ProgramManagementOverview; t: (key: string, params?: Record<string, string | number>) => string; formatDate: (date?: string) => string; showSummary?: boolean }) {
  const portfolioGaps = "programsWithReportingGaps" in data.statusReports ? data.statusReports : undefined;
  return <div className="space-y-4">{showSummary ? <Kpis values={[[t("portfolioManager.currentReports"), data.statusReports.currentCount, "text-success"], [t("portfolioManager.staleReports"), data.statusReports.staleCount, "text-warning"], [t("portfolioManager.missingReports"), data.statusReports.missingCount, "text-destructive"]]} /> : null}<Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.reporting")}</CardTitle><p className="text-sm text-muted-foreground">{t("portfolioManager.freshnessRule", { days: data.statusReports.freshnessDays })}</p></CardHeader><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("portfolioManager.project")}</TableHead>{portfolioGaps ? <TableHead>{t("portfolioManager.program")}</TableHead> : null}<TableHead>{t("portfolioManager.reportDate")}</TableHead><TableHead>{t("portfolioManager.health")}</TableHead><TableHead>{t("portfolioManager.progress")}</TableHead><TableHead>{t("portfolioManager.freshness")}</TableHead></TableRow></TableHeader><TableBody>{data.statusReports.latestPublished.length ? data.statusReports.latestPublished.map((item) => <TableRow key={item.projectId}><TableCell><Link className="hover:text-primary" to={projectHref(item) ?? "/projects"}>{item.projectName}</Link></TableCell>{portfolioGaps ? <TableCell>{"programName" in item ? item.programName ?? t("portfolioManager.directProject") : t("portfolioManager.directProject")}</TableCell> : null}<TableCell>{formatDate(item.reportDate)}</TableCell><TableCell><Status value={item.health} label={localizedStatus(t, item.health)} /></TableCell><TableCell>{item.progress == null ? "—" : `${item.progress}%`}</TableCell><TableCell><Status value={item.freshness} label={localizedStatus(t, item.freshness)} /></TableCell></TableRow>) : <EmptyRow text={t("portfolioManager.noData")} colSpan={portfolioGaps ? 6 : 5} />}</TableBody></Table></div></CardContent></Card>{portfolioGaps ? <div className="grid gap-4 lg:grid-cols-2"><GapList title={t("portfolioManager.programReportingGaps")} values={portfolioGaps.programsWithReportingGaps} empty={t("portfolioManager.noReportingGaps")} /><GapList title={t("portfolioManager.projectReportingGaps")} values={portfolioGaps.projectsWithReportingGaps} empty={t("portfolioManager.noReportingGaps")} /></div> : null}</div>;
}
function Meetings({ data, t, formatDate }: { data: PortfolioManagementOverview | ProgramManagementOverview; t: (key: string) => string; formatDate: (date: string) => string }) { return <Card><CardHeader><CardTitle className="text-base">{t("portfolioManager.upcomingMeetings")}</CardTitle></CardHeader><CardContent>{data.upcomingMeetings.length ? <div className="divide-y">{data.upcomingMeetings.map((item) => <div key={item.id} className="flex items-center justify-between gap-3 py-3"><div><p className="font-medium">{item.title}</p><p className="text-xs text-muted-foreground">{formatDate(item.startTime)} · {item.projectName ?? "—"}</p></div>{item.projectId ? <Link className="text-sm text-primary hover:underline" to={projectHref(item) ?? "/projects"}>{t("portfolioManager.project")}</Link> : null}</div>)}</div> : <p className="text-sm text-muted-foreground">{t("portfolioManager.noMeetings")}</p>}</CardContent></Card>; }

function Kpis({ values }: { values: [string, string | number, string?][] }) { return <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">{values.map(([label, value, valueClassName]) => <Card key={label}><CardContent className="p-4"><p className="text-xs text-muted-foreground">{label}</p><p className={`mt-1 text-2xl font-semibold tabular-nums ${valueClassName ?? ""}`}>{value}</p></CardContent></Card>)}</div>; }
function Identity({ title, values }: { title: string; values: [string, string | undefined][] }) { return <Card><CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader><CardContent className="grid gap-3 sm:grid-cols-2">{values.map(([label, value]) => <div key={label}><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 text-sm font-medium">{value || "—"}</p></div>)}</CardContent></Card>; }
function Metric({ label, value, hint, valueClassName }: { label: string; value: string | number; hint?: string; valueClassName?: string }) { return <div><p className="text-xs text-muted-foreground">{label}</p><p className={`mt-1 font-semibold tabular-nums ${valueClassName ?? ""}`}>{value}</p>{hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}</div>; }

function MiniLinks({ items, empty }: { items: { id: string; label: string; badge: string; href?: string }[]; empty: string }) { return items.length ? <div className="space-y-2">{items.map((item) => <div key={item.id} className="flex items-center gap-2 text-xs"><div className="min-w-0 flex-1 truncate">{item.href ? <Link to={item.href} className="hover:text-primary">{item.label}</Link> : item.label}</div><Status value={item.badge} /></div>)}</div> : <p className="text-xs text-muted-foreground">{empty}</p>; }
function Status({ value, label }: { value?: string; label?: string }) { return <Badge status={value || ""}>{label ?? value ?? "—"}</Badge>; }
function localizedStatus(t: (key: string) => string, value?: string) {
  const key = value?.trim().toLowerCase().replace(/[\s_-]/g, "");
  const translations: Record<string, string> = {
    active: "portfolioManager.statuses.active",
    archived: "portfolioManager.statuses.archived",
    planning: "programManager.statuses.planning",
    onhold: "programManager.statuses.onHold",
    completed: "programManager.statuses.completed",
    green: "programManager.healthOptions.green",
    amber: "programManager.healthOptions.amber",
    red: "programManager.healthOptions.red",
    blue: "programManager.healthOptions.blue",
    unknown: "programManager.unknown",
    healthy: "portfolioManager.healthy",
    warning: "portfolioManager.warning",
    informational: "portfolioManager.informational",
    high: "portfolioManager.high",
    open: "portfolioManager.open",
    project: "portfolioManager.project",
    risk: "portfolioManager.risk",
    issue: "portfolioManager.issue",
    ontrack: "programManager.onTrack",
    overdue: "programManager.overdue",
    delayed: "programManager.overdue",
    current: "portfolioManager.current",
    stale: "portfolioManager.stale",
    missing: "portfolioManager.missing",
    critical: "portfolioManager.critical",
    needsattention: "portfolioManager.needsAttention",
    clear: "portfolioManager.clear",
  };
  return (key && translations[key] ? t(translations[key]) : undefined) ?? value ?? "—";
}
function portfolioRiskLabel(t: (key: string) => string, value: string) {
  if (value === "Critical") return t("portfolioManager.critical");
  if (value === "NeedsAttention") return t("portfolioManager.needsAttention");
  return t("portfolioManager.clear");
}
function portfolioReportingLabel(t: (key: string) => string, value: string) {
  if (value === "Current") return t("portfolioManager.current");
  if (value === "Stale") return t("portfolioManager.stale");
  return t("portfolioManager.missing");
}
function programScheduleLabel(t: (key: string) => string, value: string) {
  if (value === "OnTrack") return t("programManager.onTrack");
  if (value === "Overdue" || value === "Delayed") return t("programManager.overdue");
  return t("programManager.unknown");
}

function Pager({ page, pages, onPage, t }: { page: number; pages: number; onPage: (page: number) => void; t: (key: string) => string }) { return pages > 1 ? <div className="flex items-center justify-end gap-2 border-t p-3"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>{t("portfolioManager.previous")}</Button><span className="text-xs text-muted-foreground">{page} / {pages}</span><Button size="sm" variant="outline" disabled={page >= pages} onClick={() => onPage(page + 1)}>{t("portfolioManager.next")}</Button></div> : null; }
function Loading() { return <div className="space-y-3" role="status"><div className="h-8 w-1/3 animate-pulse rounded bg-muted" /><div className="h-24 animate-pulse rounded bg-muted" /><div className="h-64 animate-pulse rounded bg-muted" /></div>; }
function ErrorState({ text, onRetry }: { text: string; onRetry?: () => void }) { const { t } = useI18n(); return <div className="flex flex-col items-center gap-3 py-12 text-center"><p className="text-sm text-destructive">{text}</p>{onRetry ? <Button size="sm" variant="outline" onClick={onRetry}>{t("common.retry")}</Button> : null}</div>; }
function Empty({ text }: { text: string }) { return <p className="p-8 text-center text-sm text-muted-foreground">{text}</p>; }
function EmptyRow({ text, colSpan }: { text: string; colSpan: number }) { return <TableRow><TableCell colSpan={colSpan} className="h-24 text-center text-muted-foreground">{text}</TableCell></TableRow>; }
function GapList({ title, values, empty }: { title: string; values: string[]; empty: string }) { return <Card><CardHeader><CardTitle className="text-sm">{title}</CardTitle></CardHeader><CardContent>{values.length ? <ul className="list-inside list-disc space-y-1 text-sm">{values.map((value) => <li key={value}>{value}</li>)}</ul> : <p className="text-sm text-muted-foreground">{empty}</p>}</CardContent></Card>; }
