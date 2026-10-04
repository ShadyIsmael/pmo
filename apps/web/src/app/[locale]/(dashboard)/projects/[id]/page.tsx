"use client";

import { useCallback, useEffect, useState } from "react";
import { usePathname, useParams, useRouter, useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import Link from "next/link";
import dynamic from "next/dynamic";
import {
  AlertTriangle,
  ArrowRightLeft,
  ArrowUpRight,
  CalendarDays,
  Camera,
  ClipboardList,
  Files,
  GanttChartSquare,
  GitBranch,
  History as HistoryIcon,
  LayoutDashboard,
  ShieldAlert,
  Settings2,
  Users,
  WalletCards,
  type LucideIcon,
} from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  useProjectShell,
  useProjectHandoverRequests,
  useProjectManagerHistory,
  useProjectSnapshots,
  useProjectOverview,
} from "@/hooks/projects/useProjects";
import { formatDate } from "@/lib/utils";
import { ProjectOverview } from "@/components/projects/ProjectOverview";
import { ProjectRisksList } from "@/components/risks/ProjectRisksList";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useManagementPerspective } from "@/lib/perspective-context";
import { useWorkItems } from "@/lib/api/work-items";
import { getWorkItemStatusName, useWorkItemStatuses } from "@/lib/api/reference-data";
import { PROJECT_DETAIL_PREVIEW_SIZE } from "@/lib/constants";
import { ProjectPeople } from "@/components/projects/ProjectPeople";
import { DocumentCenter } from "@/components/documents/DocumentCenter";
import { isProjectTabVisible, shouldLoadProjectOverview } from "@/hooks/projects/project-loading";

const TAB_VALUES = [
  "overview",
  "plan",
  "team",
  "financials",
  "risks-issues",
  "status-reports",
  "meetings",
  "documents",
  "handover",
  "history",
  "snapshots",
  "change-tracker",
] as const;

type TabValue = (typeof TAB_VALUES)[number];

// Lazy-load the edit sheet so its lookup queries only fire while the sheet is open.
const LazyEditProjectSheet = dynamic(
  () =>
    import("@/components/projects/CreateProjectSheet").then(
      (mod) => mod.EditProjectSheet
    ),
  { ssr: false }
);

// These tabs/sheets are interaction-only, so keep their graphs out of the initial route chunk.
const LazyProjectHandoverSheet = dynamic(
  () => import("@/components/projects/ProjectHandoverSheet").then((mod) => mod.ProjectHandoverSheet),
  { ssr: false }
);
const LazyProjectChangeRequestSheet = dynamic(
  () => import("@/components/projects/ProjectChangeRequestSheet").then((mod) => mod.ProjectChangeRequestSheet),
  { ssr: false }
);
const LazyChangeTracker = dynamic(
  () => import("@/components/projects/ChangeTracker").then((mod) => mod.ChangeTracker),
  { ssr: false }
);
const LazyProjectPlanningWorkspace = dynamic(
  () => import("@/components/projects/ProjectPlanningWorkspace").then((mod) => mod.ProjectPlanningWorkspace),
  { ssr: false, loading: () => <Card className="shadow-none"><CardContent className="h-48 p-4"><Skeleton className="h-full" /></CardContent></Card> }
);
const LazyProjectStatusReports = dynamic(
  () => import("@/components/projects/ProjectStatusReports").then((mod) => mod.ProjectStatusReports),
  { ssr: false }
);
const LazyProjectMeetingsTab = dynamic(
  () => import("@/components/projects/ProjectMeetingsTab").then((mod) => mod.ProjectMeetingsTab),
  { ssr: false, loading: () => <Card className="shadow-none"><CardContent className="h-48 p-4"><Skeleton className="h-full" /></CardContent></Card> }
);
const LazyProjectFinancials = dynamic(
  () => import("@/components/projects/ProjectFinancials").then((mod) => mod.ProjectFinancials),
  { ssr: false, loading: () => <Card className="shadow-none"><CardContent className="h-56 p-6"><Skeleton className="h-full" /></CardContent></Card> }
);

const TAB_ICONS: Record<TabValue, LucideIcon> = {
  overview: LayoutDashboard,
  plan: GanttChartSquare,
  team: Users,
  financials: WalletCards,
  "risks-issues": ShieldAlert,
  "status-reports": ClipboardList,
  meetings: CalendarDays,
  documents: Files,
  handover: ArrowRightLeft,
  history: HistoryIcon,
  snapshots: Camera,
  "change-tracker": GitBranch,
};

function isTabValue(value: string | null): value is TabValue {
  return !!value && TAB_VALUES.includes(value as TabValue);
}

function displayDate(value?: string) {
  return value ? formatDate(value) : "—";
}

function ProjectTasksAndIssues({ projectId }: { projectId: string }) {
  const locale = useLocale();
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const query = useWorkItems({ page: 1, pageSize: PROJECT_DETAIL_PREVIEW_SIZE, projectId });
  const statusesQuery = useWorkItemStatuses(true);
  const items = query.data?.items ?? [];
  const statuses = statusesQuery.data ?? [];

  return <Card className="shadow-none"><CardHeader className="flex flex-row items-start justify-between gap-3"><div><SectionHeader title={t("tasksAndIssues")} description={t("tasksAndIssuesDescription")} /></div><Link href={`/${locale}/work-items?projectId=${encodeURIComponent(projectId)}`}><Button size="sm" variant="outline" className="shrink-0"><ArrowUpRight className="me-1.5 h-3.5 w-3.5" />{common("viewAll")}</Button></Link></CardHeader><CardContent>{query.isLoading ? <div className="space-y-3"><Skeleton className="h-10" /><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : query.isError ? <p className="text-sm text-destructive">{common("errorLoading")}</p> : items.length === 0 ? <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 text-center"><ClipboardList className="h-7 w-7 text-muted-foreground/50" /><p className="mt-2 text-sm font-medium">{t("noTasksAndIssues")}</p></div> : <div className="divide-y">{items.map((item) => <Link key={item.id} href={`/${locale}/work-items/${encodeURIComponent(item.id)}`} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0 hover:bg-muted/20"><div className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${item.type === "Issue" ? "bg-blue-500/10 text-blue-700 dark:text-blue-300" : "bg-primary/10 text-primary"}`}><ClipboardList className="h-4 w-4" /></div><div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold">{item.title}</p><div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground"><span className={item.type === "Issue" ? "font-medium text-blue-700 dark:text-blue-300" : "font-medium text-primary"}>{item.type === "Issue" ? common("issues") : common("tasks")}</span><span aria-hidden="true">·</span><span>{getWorkItemStatusName(item.status, statuses, locale, item.status)}</span>{item.dueDate ? <><span aria-hidden="true">·</span><span>{formatDate(item.dueDate, locale === "ar" ? "ar-EG" : "en-GB")}</span></> : null}</div></div><ArrowUpRight className="h-4 w-4 shrink-0 text-muted-foreground" /></Link>)}</div>}</CardContent></Card>;
}


function EmptyState({ icon: Icon, title, description, action }: { icon: LucideIcon; title: string; description: string; action?: React.ReactNode }) {
  return (
    <div className="flex min-h-40 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 py-8 text-center">
      <Icon className="h-8 w-8 text-muted-foreground/50" />
      <p className="mt-3 text-sm font-medium">{title}</p>
      <p className="mt-1 max-w-md text-xs text-muted-foreground">{description}</p>
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

function SectionHeader({ title, description, action }: { title: string; description?: string; action?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-3 border-b pb-3 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <h2 className="text-sm font-semibold">{title}</h2>
        {description && <p className="mt-1 text-xs text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  );
}

export default function ProjectDetailPage() {
  const locale = useLocale();
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const params = useParams();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const { canMutate } = useManagementPerspective();
  const projectId = params.id as string;
  const searchParamsString = searchParams.toString();
  const requestedTab = searchParams.get("tab");
  const activeTab: TabValue = isTabValue(requestedTab) ? requestedTab : "overview";
  const [showEditSheet, setShowEditSheet] = useState(false);
  const [showHandoverSheet, setShowHandoverSheet] = useState(false);
  const [showChangeRequestSheet, setShowChangeRequestSheet] = useState(false);

  const { data: project, isLoading: projectLoading, isError: projectError } = useProjectShell(projectId);
  const { data: managerHistory, isLoading: historyLoading } = useProjectManagerHistory(projectId, isProjectTabVisible(activeTab, "history"));
  const { data: snapshots, isLoading: snapshotsLoading } = useProjectSnapshots(projectId, isProjectTabVisible(activeTab, "snapshots"));
  const { data: overview, isLoading: overviewLoading, isError: overviewError } = useProjectOverview(projectId, shouldLoadProjectOverview(activeTab));
  const { data: handoverRequests, isLoading: handoverRequestsLoading } = useProjectHandoverRequests(projectId, isProjectTabVisible(activeTab, "handover") || showHandoverSheet);
  const canEditProject = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit));
  const canManageChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage));
  const canManageHandovers = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage));
  const canAssignResources = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Assign));
  const canViewFinances = Boolean(user && hasPermission(user.permissions, Permissions.Finances.Read));
  const canManageFinances = Boolean(user && hasPermission(user.permissions, Permissions.Finances.Write));
  const canViewStakeholders = Boolean(user && hasPermission(user.permissions, Permissions.Stakeholders.Read));
  const canManageStakeholders = Boolean(user && hasPermission(user.permissions, Permissions.Stakeholders.Manage));
  const canManagePeople = canMutate && (canAssignResources || canManageStakeholders);
  const updateTab = useCallback((value: string) => {
    if (!isTabValue(value)) return;
    const nextParams = new URLSearchParams(searchParamsString);
    nextParams.set("tab", value);
    if (nextParams.toString() === searchParamsString) return;
    router.push(`${pathname}?${nextParams.toString()}`, { scroll: false });
  }, [pathname, router, searchParamsString]);

  const getTabHref = (tab: string) => {
    if (!isTabValue(tab)) return pathname;
    const nextParams = new URLSearchParams(searchParamsString);
    nextParams.set("tab", tab);
    return `${pathname}?${nextParams.toString()}`;
  };

  useEffect(() => {
    if (!requestedTab || isTabValue(requestedTab)) return;
    const nextParams = new URLSearchParams(searchParamsString);
    nextParams.set("tab", "overview");
    if (nextParams.toString() === searchParamsString) return;
    router.replace(`${pathname}?${nextParams.toString()}`, { scroll: false });
  }, [pathname, requestedTab, router, searchParamsString]);

  if (projectLoading) {
    return (
      <div className="space-y-4">
        <PageHeader title={t("title")} description={t("description")} />
        <Card><CardContent className="h-32 p-4"><Skeleton className="h-full" /></CardContent></Card>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><Skeleton className="h-24" /><Skeleton className="h-24" /><Skeleton className="h-24" /><Skeleton className="h-24" /></div>
        <Card><CardContent className="h-72 p-4"><Skeleton className="h-full" /></CardContent></Card>
      </div>
    );
  }

  if (projectError || !project) {
    return (
      <div className="space-y-4">
        <PageHeader title={t("title")} description={t("description")} />
        <Card><CardContent className="py-12 text-center"><AlertTriangle className="mx-auto h-10 w-10 text-status-red" /><p className="mt-3 text-sm text-muted-foreground">{common("notFound") || "Project not found"}</p></CardContent></Card>
      </div>
    );
  }

  return (
    <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-project-detail space-y-4">
      {canEditProject ? <div className="flex justify-end"><Link href={`/${locale}/projects/${projectId}/settings/workflows`}><Button variant="outline" size="sm"><Settings2 className="me-1.5 h-3.5 w-3.5" />{locale === "ar" ? "إعدادات المشروع" : "Project settings"}</Button></Link></div> : null}
      <Tabs value={activeTab} onValueChange={updateTab} className="w-full">
        <div className="sticky top-0 z-20 isolate border-y border-border bg-muted/70 py-1.5 dark:bg-sidebar">
          <TabsList className="flex h-auto w-full min-w-0 items-center justify-start gap-0.5 overflow-x-auto overflow-y-hidden rounded-none bg-transparent p-0">
            {TAB_VALUES.map((tab) => {
              const Icon = TAB_ICONS[tab];
              const label = tab === "overview" ? t("overview") : tab === "status-reports" ? t("statusReports") : tab === "history" ? t("historyTab") : tab === "risks-issues" ? `${t("risks")} & ${t("issues")}` : tab === "financials" ? t("financials") : tab === "documents" ? common("documents") : tab === "meetings" ? common("meetings") : tab === "team" ? t("projectPeople") : tab === "plan" ? t("plan") : tab === "handover" ? t("handover") : tab === "snapshots" ? t("snapshots") : t("changeTracker");
              return <TabsTrigger key={tab} value={tab} className="h-auto shrink-0 whitespace-nowrap rounded-none border-b-2 border-transparent px-3 py-2 text-xs leading-5 text-muted-foreground transition-[color,font-size,padding] data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:px-3.5 data-[state=active]:py-2.5 data-[state=active]:text-sm data-[state=active]:font-semibold data-[state=active]:text-primary data-[state=active]:shadow-none rtl:flex-row-reverse"><Icon className="me-1.5 h-3.5 w-3.5" />{label}</TabsTrigger>;
            })}
          </TabsList>
        </div>

        <TabsContent value="overview" className="pt-1">
          <ProjectOverview
            project={project}
            overview={overview}
            overviewLoading={overviewLoading}
            overviewError={overviewError}
            getTabHref={getTabHref}
            canEditProject={canEditProject}
            canManageChangeRequests={canManageChangeRequests}
            canManageHandovers={canManageHandovers}
            onEdit={() => setShowEditSheet(true)}
            onChangeRequest={() => setShowChangeRequestSheet(true)}
            onHandover={() => setShowHandoverSheet(true)}
            onOpenPlan={() => router.push(`${pathname}/plan`)}
          />
        </TabsContent>

        <TabsContent value="plan" className="pt-1">
          <LazyProjectPlanningWorkspace projectId={projectId} />
        </TabsContent>

        <TabsContent value="team" className="pt-1"><ProjectPeople projectId={projectId} enabled={isProjectTabVisible(activeTab, "team") && (canViewStakeholders || canAssignResources)} canManage={canManagePeople} /></TabsContent>

        <TabsContent value="financials" className="pt-1"><LazyProjectFinancials project={project} summary={overview} enabled={activeTab === "financials" && canViewFinances} canManage={canManageFinances} loading={overviewLoading} error={overviewError} /></TabsContent>

        <TabsContent value="risks-issues" className="space-y-4 pt-1"><div className="grid gap-4 lg:grid-cols-2"><ProjectRisksList projectId={projectId} projectName={project.name} /><ProjectTasksAndIssues projectId={projectId} /></div></TabsContent>

        <TabsContent value="status-reports" className="pt-1"><LazyProjectStatusReports projectId={projectId} project={project} canManage={canEditProject} enabled={isProjectTabVisible(activeTab, "status-reports")} /></TabsContent>
        <TabsContent value="meetings" className="pt-1"><LazyProjectMeetingsTab projectId={projectId} project={{ id: project.id, name: project.name, code: project.code }} enabled={isProjectTabVisible(activeTab, "meetings")} /></TabsContent>
        <TabsContent value="documents" className="pt-1"><DocumentCenter projectId={projectId} compact /></TabsContent>

        <TabsContent value="handover" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("handover")} description={t("handoverDescription")} /></CardHeader><CardContent>{handoverRequestsLoading ? <Skeleton className="h-24" /> : handoverRequests && handoverRequests.length > 0 ? <div className="divide-y">{handoverRequests.map((request) => <div key={request.id} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-start gap-3"><ArrowRightLeft className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><div className="min-w-0"><p className="text-sm font-medium">{request.currentProjectManagerName} → {request.newProjectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{t("effectiveHandoverDate")}: {displayDate(request.effectiveHandoverDate)} · {t("requestedBy")}: {request.requestedByName}</p>{request.reason && <p className="mt-1 text-xs text-muted-foreground">{request.reason}</p>}{request.rejectionReason && <p className="mt-1 text-xs text-destructive">{t("rejectionReason")}: {request.rejectionReason}</p>}</div></div><Badge status={request.status} className="w-fit text-[10px]">{t(`handoverStatus.${request.status}`) || request.status}</Badge></div>)}</div> : <EmptyState icon={ArrowRightLeft} title={t("noHandoverRequests")} description="Create a handover request when project ownership needs to change." action={<Button size="sm" onClick={() => setShowHandoverSheet(true)}><ArrowRightLeft className="mr-1.5 h-3.5 w-3.5" />{t("handover")}</Button>} />}</CardContent></Card></TabsContent>

        <TabsContent value="history" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("managerHistory")} description="Project Manager ownership history" /></CardHeader><CardContent>{historyLoading ? <Skeleton className="h-24" /> : managerHistory && managerHistory.length > 0 ? <div className="divide-y">{managerHistory.map((entry) => <div key={entry.id} className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-start gap-3"><HistoryIcon className="mt-0.5 h-4 w-4 text-muted-foreground" /><div><p className="text-sm font-medium">{entry.projectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{displayDate(entry.startDate)} – {entry.endDate ? displayDate(entry.endDate) : t("current")}{entry.handoverFromProjectManagerName ? ` · ${common("from")}: ${entry.handoverFromProjectManagerName}` : ""}</p>{entry.handoverReason && <p className="mt-1 text-xs text-muted-foreground">{entry.handoverReason}</p>}</div></div>{entry.isCurrent ? <Badge status="Active" className="w-fit text-[10px]">{t("current")}</Badge> : <Badge status="Closed" className="w-fit text-[10px]">{displayDate(entry.endDate)}</Badge>}</div>)}</div> : <EmptyState icon={HistoryIcon} title={t("noManagerHistory")} description="Manager changes will be recorded here as handovers are completed." />}</CardContent></Card></TabsContent>

        <TabsContent value="snapshots" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("snapshots")} description={t("snapshotsDescription")} /></CardHeader><CardContent>{snapshotsLoading ? <Skeleton className="h-24" /> : snapshots && snapshots.length > 0 ? <div className="divide-y">{snapshots.map((snapshot) => <div key={snapshot.id} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-start gap-3"><Camera className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><div className="min-w-0"><p className="text-sm font-medium">{snapshot.snapshotName}</p><p className="mt-1 text-xs text-muted-foreground">{t("effectiveEndDate")}: {displayDate(snapshot.effectiveEndDate)} · {t("snapshotDate")}: {displayDate(snapshot.snapshotDate)}</p><p className="mt-1 text-xs text-muted-foreground">{snapshot.previousProjectManagerName || "—"} → {snapshot.newProjectManagerName || "—"}</p></div></div><Link href={`/projects/${projectId}/snapshots/${snapshot.id}`}><Button variant="outline" size="sm"><Camera className="mr-1.5 h-3.5 w-3.5" />{t("viewSnapshot")}</Button></Link></div>)}</div> : <EmptyState icon={Camera} title={t("noSnapshots")} description={t("snapshotsDescription")} />}</CardContent></Card></TabsContent>
        <TabsContent value="change-tracker" className="pt-1"><LazyChangeTracker projectId={projectId} currency={project.currency} /></TabsContent>
      </Tabs>

      {showEditSheet ? (
        <LazyEditProjectSheet open={showEditSheet} onOpenChange={setShowEditSheet} project={project} />
      ) : null}
      <LazyProjectHandoverSheet open={showHandoverSheet} onOpenChange={setShowHandoverSheet} project={project} requests={handoverRequests ?? []} />
      <LazyProjectChangeRequestSheet open={showChangeRequestSheet} onOpenChange={setShowChangeRequestSheet} project={project} />
    </div>
  );
}
