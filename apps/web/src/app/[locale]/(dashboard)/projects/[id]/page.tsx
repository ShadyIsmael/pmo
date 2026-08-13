"use client";

import { useCallback, useEffect, useState } from "react";
import { usePathname, useParams, useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import Link from "next/link";
import dynamic from "next/dynamic";
import {
  AlertTriangle,
  ArrowRight,
  ArrowRightLeft,
  Banknote,
  CalendarDays,
  Camera,
  CheckCircle2,
  ChevronDown,
  CircleDollarSign,
  ClipboardList,
  Edit,
  FilePen,
  FileText,
  Files,
  GanttChartSquare,
  GitBranch,
  History as HistoryIcon,
  BarChart3,
  LayoutDashboard,
  MoreHorizontal,
  ShieldAlert,
  Trash2,
  Users,
  WalletCards,
  type LucideIcon,
} from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ProjectConfidenceDonut } from "@/components/projects/ProjectConfidenceDonut";
import {
  useProject,
  useProjectHandoverRequests,
  useProjectManagerHistory,
  useProjectSnapshots,
  useRemoveTeamMember,
} from "@/hooks/projects/useProjects";
import { formatCurrency, formatDate, getRagVariant, getRiskVariant, getStatusVariant } from "@/lib/utils";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";

const TAB_VALUES = [
  "overview",
  "plan",
  "team",
  "financials",
  "risks-issues",
  "status-reports",
  "stakeholders",
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

const TAB_ICONS: Record<TabValue, LucideIcon> = {
  overview: LayoutDashboard,
  plan: GanttChartSquare,
  team: Users,
  financials: WalletCards,
  "risks-issues": ShieldAlert,
  "status-reports": ClipboardList,
  stakeholders: Users,
  meetings: CalendarDays,
  documents: Files,
  handover: ArrowRightLeft,
  history: HistoryIcon,
  snapshots: Camera,
  "change-tracker": GitBranch,
};

const PROJECT_HEADER_DETAILS_ID = "project-header-details";

function isTabValue(value: string | null): value is TabValue {
  return !!value && TAB_VALUES.includes(value as TabValue);
}

function displayDate(value?: string) {
  return value ? formatDate(value) : "—";
}

function displayText(value?: string | null) {
  return value?.trim() || "—";
}

type NullableNumber = number | null | undefined;

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function formatNullableCurrency(value: NullableNumber, currency?: string) {
  return isFiniteNumber(value) ? formatCurrency(value, currency || "GBP") : "—";
}

function formatNullablePercentage(value: NullableNumber, fractionDigits = 0, signed = false) {
  if (!isFiniteNumber(value)) return "—";
  return `${signed && value > 0 ? "+" : ""}${value.toFixed(fractionDigits)}%`;
}

function clampProgress(value: NullableNumber) {
  return isFiniteNumber(value) ? Math.min(100, Math.max(0, value)) : 0;
}

function getConfidencePercentage(value?: string | number | null) {
  if (isFiniteNumber(value)) return Math.min(100, Math.max(0, value));
  if (typeof value !== "string" || value.trim() === "") return null;
  const parsedValue = Number(value);
  return isFiniteNumber(parsedValue) ? Math.min(100, Math.max(0, parsedValue)) : null;
}

function getNullableRatio(numerator: NullableNumber, denominator: NullableNumber) {
  if (!isFiniteNumber(numerator) || !isFiniteNumber(denominator) || denominator <= 0) return null;
  const ratio = (numerator / denominator) * 100;
  return Number.isFinite(ratio) ? ratio : null;
}

function getRemainingDays(endDate?: string) {
  if (!endDate) return null;
  const end = new Date(endDate).getTime();
  if (!Number.isFinite(end)) return null;
  const remainingDays = Math.ceil((end - Date.now()) / 86_400_000);
  return Number.isFinite(remainingDays) ? remainingDays : null;
}

function MetricCard({
  label,
  value,
  icon: Icon,
  detail,
  valueClassName,
}: {
  label: string;
  value: string | number;
  icon: LucideIcon;
  detail?: string;
  valueClassName?: string;
}) {
  return (
    <Card className="h-full min-w-0 shadow-none">
      <CardContent className="p-3.5">
        <div className="flex items-center justify-between gap-2 text-[11px] font-medium text-muted-foreground">
          <span className="truncate">{label}</span>
          <Icon className="h-3.5 w-3.5 shrink-0" />
        </div>
        <div className={`mt-2 truncate text-lg font-semibold tracking-tight ${valueClassName ?? ""}`}>{value}</div>
        {detail && <p className="mt-1 truncate text-[10px] text-muted-foreground">{detail}</p>}
      </CardContent>
    </Card>
  );
}

function DefinitionGrid({ items }: { items: Array<{ label: string; value: React.ReactNode; wide?: boolean }> }) {
  return (
    <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
      {items.map((item) => (
        <div key={item.label} className={item.wide ? "sm:col-span-2 lg:col-span-3" : "min-w-0"}>
          <dt className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{item.label}</dt>
          <dd className="mt-1 break-words text-sm font-medium">{item.value}</dd>
        </div>
      ))}
    </dl>
  );
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
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const params = useParams();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const projectId = params.id as string;
  const searchParamsString = searchParams.toString();
  const requestedTab = searchParams.get("tab");
  const activeTab: TabValue = isTabValue(requestedTab) ? requestedTab : "overview";
  const [showEditSheet, setShowEditSheet] = useState(false);
  const [showHandoverSheet, setShowHandoverSheet] = useState(false);
  const [showChangeRequestSheet, setShowChangeRequestSheet] = useState(false);
  const [isProjectHeaderExpanded, setIsProjectHeaderExpanded] = useState(true);

  const { data: project, isLoading: projectLoading, isError: projectError } = useProject(projectId);
  const { data: managerHistory, isLoading: historyLoading } = useProjectManagerHistory(projectId, activeTab === "history");
  const { data: snapshots, isLoading: snapshotsLoading } = useProjectSnapshots(projectId, activeTab === "snapshots");
  const { data: handoverRequests, isLoading: handoverRequestsLoading } = useProjectHandoverRequests(projectId, activeTab === "handover" || showHandoverSheet);
  const removeMemberMutation = useRemoveTeamMember(projectId);
  const remainingDays = getRemainingDays(project?.endDate);
  const safeProgress = clampProgress(project?.progress);
  const confidencePercentage = getConfidencePercentage(project?.confidenceLevel);
  const actualCostRatio = getNullableRatio(project?.actualCost, project?.budget);

  const updateTab = useCallback((value: string) => {
    if (!isTabValue(value)) return;
    const nextParams = new URLSearchParams(searchParamsString);
    nextParams.set("tab", value);
    if (nextParams.toString() === searchParamsString) return;
    router.push(`${pathname}?${nextParams.toString()}`, { scroll: false });
  }, [pathname, router, searchParamsString]);

  useEffect(() => {
    if (!requestedTab || isTabValue(requestedTab)) return;
    const nextParams = new URLSearchParams(searchParamsString);
    nextParams.set("tab", "overview");
    if (nextParams.toString() === searchParamsString) return;
    router.replace(`${pathname}?${nextParams.toString()}`, { scroll: false });
  }, [pathname, requestedTab, router, searchParamsString]);

  const { statusLabel, healthStatusLabel, priorityLabel } = useProjectEnumLabels();

  const statusBadgeLabel = project ? statusLabel(project.status) : "—";
  const healthBadgeLabel = healthStatusLabel(project?.health || project?.ragStatus);

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

  const handleRemoveMember = async (memberId: string) => {
    if (!confirm(t("confirmRemoveMember") || "Are you sure you want to remove this team member?")) return;
    await removeMemberMutation.mutateAsync(memberId);
  };

  const statusBadge = <Badge variant={getStatusVariant(project.status)} className="text-[11px]">{statusBadgeLabel}</Badge>;
  const healthBadge = <Badge variant={getRagVariant(project.health || project.ragStatus)} className="text-[11px]"><span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-current" />{healthBadgeLabel}</Badge>;

  return (
    <div className="space-y-4">
      <section className="rounded-lg border bg-card px-4 py-4 shadow-sm sm:px-5">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="truncate text-xl font-semibold tracking-tight sm:text-2xl">{project.name}</h1>
              {statusBadge}
              {healthBadge}
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
              <span className="font-medium text-foreground">{project.code}</span>
              <span aria-hidden="true">·</span>
              <span>{t("portfolio")}: {displayText(project.portfolioName)}</span>
              {project.phase && <><span aria-hidden="true">·</span><span>{project.phase}</span></>}
            </div>
            <div className="mt-3 flex max-w-sm items-center gap-2">
              <span className="shrink-0 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("progress")}</span>
              <Progress value={safeProgress} className="h-1.5 flex-1" />
              <span className="shrink-0 text-sm font-semibold">{formatNullablePercentage(project.progress)}</span>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap items-center gap-1.5">
            <Button size="sm" variant="outline" onClick={() => setShowEditSheet(true)}><Edit className="mr-1.5 h-3.5 w-3.5" />{t("edit")}</Button>
            <Button size="sm" variant="outline" onClick={() => setShowChangeRequestSheet(true)}><FilePen className="mr-1.5 h-3.5 w-3.5" />{t("changeRequest")}</Button>
            <Button size="sm" variant="outline" onClick={() => setShowHandoverSheet(true)}><ArrowRightLeft className="mr-1.5 h-3.5 w-3.5" />{t("handover")}</Button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button size="sm" variant="outline" aria-label={common("actions")}><MoreHorizontal className="h-4 w-4" /></Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem asChild><Link href={`/projects/${projectId}/plan`}><GanttChartSquare className="mr-2 h-4 w-4" />{t("plan")}</Link></DropdownMenuItem>
                <DropdownMenuItem onSelect={() => updateTab("snapshots")}><Camera className="mr-2 h-4 w-4" />{t("snapshots")}</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={() => setShowHandoverSheet(true)}><ArrowRightLeft className="mr-2 h-4 w-4" />{t("handover")}</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
            <Button
              size="sm"
              variant="ghost"
              className="gap-1.5"
              aria-expanded={isProjectHeaderExpanded}
              aria-controls={PROJECT_HEADER_DETAILS_ID}
              onClick={() => setIsProjectHeaderExpanded((expanded) => !expanded)}
            >
              {isProjectHeaderExpanded ? t("collapseDetails") : t("expandDetails")}
              <ChevronDown className={`h-4 w-4 transition-transform duration-200 ${isProjectHeaderExpanded ? "rotate-180" : ""}`} aria-hidden="true" />
            </Button>
          </div>
        </div>

        <div
          id={PROJECT_HEADER_DETAILS_ID}
          aria-hidden={!isProjectHeaderExpanded}
          className={`grid overflow-hidden transition-[grid-template-rows,opacity,margin-top] duration-300 ease-out ${isProjectHeaderExpanded ? "mt-4 grid-rows-[1fr] opacity-100" : "mt-0 grid-rows-[0fr] opacity-0"}`}
        >
          <div className="min-h-0 overflow-hidden">
            <div className="grid gap-x-5 gap-y-3 border-t pt-3 sm:grid-cols-2 lg:grid-cols-3">
              <div className="min-w-0"><p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("projectManager")}</p><p className="mt-1 truncate text-sm font-medium">{displayText(project.projectManagerName)}</p></div>
              <div><p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("startDate")} / {t("endDate")}</p><p className="mt-1 text-sm font-medium">{displayDate(project.startDate)} <span className="text-muted-foreground">→</span> {displayDate(project.endDate)}</p></div>
              <div><p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{t("updatedAt")}</p><p className="mt-1 text-sm font-medium">{displayDate(project.updatedAt || project.createdAt)}</p></div>
            </div>
          </div>
        </div>
      </section>

      <Tabs value={activeTab} onValueChange={updateTab} className="w-full">
        <div className="sticky top-0 z-20 isolate border-y border-border bg-background py-1.5">
          <TabsList className="flex h-auto w-full min-w-0 items-center justify-start gap-0.5 overflow-x-auto overflow-y-hidden rounded-none bg-transparent p-0">
            {TAB_VALUES.map((tab) => {
              const Icon = TAB_ICONS[tab];
              const label = tab === "overview" ? t("overview") : tab === "status-reports" ? t("statusReports") : tab === "stakeholders" ? t("stakeholders") : tab === "history" ? t("historyTab") : tab === "risks-issues" ? `${t("risks")} & ${t("issues")}` : tab === "financials" ? t("financials") : tab === "documents" ? common("documents") : tab === "meetings" ? common("meetings") : tab === "team" ? t("team") : tab === "plan" ? t("plan") : tab === "handover" ? t("handover") : tab === "snapshots" ? t("snapshots") : t("changeTracker");
              return <TabsTrigger key={tab} value={tab} className="h-auto shrink-0 whitespace-nowrap rounded-none border-b-2 border-transparent px-3 py-2 text-xs leading-5 data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary data-[state=active]:shadow-none"><Icon className="mr-1.5 h-3.5 w-3.5" />{label}</TabsTrigger>;
            })}
          </TabsList>
        </div>

        <TabsContent value="overview" className="space-y-4 pt-1">
          <div className="grid auto-rows-fr gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <MetricCard label={common("budget")} value={formatNullableCurrency(project.budget, project.currency)} icon={Banknote} detail={project.currency} />
            <MetricCard label={common("forecast")} value="—" icon={CircleDollarSign} detail={project.currency} />
            <MetricCard label={t("actualCost")} value={formatNullableCurrency(project.actualCost, project.currency)} icon={WalletCards} detail={project.currency} />
            <MetricCard label={t("risks")} value="—" icon={ShieldAlert} detail={t("noRisksYet")} />
            <MetricCard label={t("issues")} value="—" icon={AlertTriangle} detail={t("noIssuesYet")} />
            <MetricCard label={t("remainingDays")} value={remainingDays === null ? "—" : remainingDays < 0 ? `${Math.abs(remainingDays)} ${t("overdue")}` : remainingDays} icon={CalendarDays} valueClassName={remainingDays !== null && remainingDays < 0 ? "text-status-red" : undefined} />
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <Card className="h-full shadow-none lg:col-span-1">
              <CardHeader className="pb-2"><CardTitle className="text-sm">{t("confidenceLevel")}</CardTitle><CardDescription>{t("confidencePercentageUnavailable")}</CardDescription></CardHeader>
              <CardContent>
                <ProjectConfidenceDonut value={confidencePercentage} />
              </CardContent>
            </Card>
            <Card className="h-full shadow-none lg:col-span-2">
              <CardHeader className="pb-2"><CardTitle className="text-sm">{t("actualVsPlannedProgress")}</CardTitle><CardDescription>{t("progressHistoryDataGap")}</CardDescription></CardHeader>
              <CardContent><EmptyState icon={BarChart3} title={t("progressHistoryUnavailable")} description={t("progressHistoryDataGap")} /></CardContent>
            </Card>
          </div>

          <Card className="shadow-none">
            <CardHeader className="pb-4"><CardTitle className="text-sm">{t("details")}</CardTitle><CardDescription>{t("description")}</CardDescription></CardHeader>
            <CardContent>
              <DefinitionGrid items={[
                { label: t("description"), value: displayText(project.description), wide: true },
                { label: t("projectManager"), value: displayText(project.projectManagerName) },
                { label: t("sponsor"), value: displayText(project.sponsorName) },
                { label: t("portfolio"), value: displayText(project.portfolioName) },
                { label: t("program"), value: displayText(project.programId) },
                { label: t("department"), value: displayText(project.departmentName) },
                { label: t("priority"), value: project.priority ? <Badge variant={getRiskVariant(project.priority)} className="text-[10px]">{priorityLabel(project.priority)}</Badge> : "—" },
                { label: t("status"), value: statusBadge },
                { label: `${t("health")} / ${t("ragStatus")}`, value: healthBadge },
                { label: t("confidenceLevel"), value: project.confidenceLevel ? <Badge variant={getRagVariant(project.confidenceLevel)} className="text-[10px]">{project.confidenceLevel}</Badge> : "—" },
                { label: t("startDate"), value: displayDate(project.startDate) },
                { label: t("endDate"), value: displayDate(project.endDate) },
                { label: t("currency"), value: displayText(project.currency) },
              ]} />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="plan" className="pt-1">
          <Card className="shadow-none"><CardHeader><SectionHeader title={t("plan")} description="Phases, milestones, deliverables, schedule, WBS, and work items" action={<Link href={`/projects/${projectId}/plan`}><Button size="sm"><GanttChartSquare className="mr-1.5 h-3.5 w-3.5" />Open plan workspace<ArrowRight className="ml-1.5 h-3.5 w-3.5" /></Button></Link>} /></CardHeader><CardContent><div className="grid gap-4 md:grid-cols-3"><div><p className="text-[11px] uppercase tracking-wide text-muted-foreground">{t("phase")}</p><p className="mt-1 text-sm font-medium">{displayText(project.phase)}</p></div><div><p className="text-[11px] uppercase tracking-wide text-muted-foreground">{t("progress")}</p><p className="mt-1 text-sm font-medium">{formatNullablePercentage(project.progress)}</p></div><div><p className="text-[11px] uppercase tracking-wide text-muted-foreground">{t("scheduleVariance")}</p><p className={`mt-1 text-sm font-medium ${isFiniteNumber(project.scheduleVariance) && project.scheduleVariance < 0 ? "text-status-red" : "text-status-green"}`}>{formatNullablePercentage(project.scheduleVariance, 1, true)}</p></div></div></CardContent></Card>
        </TabsContent>

        <TabsContent value="team" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("team")} description="Project members and current allocations" /></CardHeader><CardContent>{project.members.length > 0 ? <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">{project.members.map((member) => <Card key={member.id} className="shadow-none"><CardContent className="flex items-center gap-3 p-4"><Avatar className="h-9 w-9"><AvatarFallback>{member.resourceName?.charAt(0) || "?"}</AvatarFallback></Avatar><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{member.resourceName}</p><p className="truncate text-xs text-muted-foreground">{member.resourceJobTitle || member.role}</p><p className="mt-1 text-[11px] text-muted-foreground">{member.role} · {formatNullablePercentage(member.allocationPercentage, 1)} · {displayDate(member.startDate)} – {displayDate(member.endDate)}</p></div><Button variant="ghost" size="icon" onClick={() => void handleRemoveMember(member.id)} aria-label={`${t("removeTeamMember")}: ${member.resourceName}`}><Trash2 className="h-4 w-4 text-status-red" /></Button></CardContent></Card>)}</div> : <EmptyState icon={Users} title={t("noTeamMembers")} description="Assign resources to the project to make ownership and capacity visible here." />}</CardContent></Card></TabsContent>

        <TabsContent value="financials" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("financials")} description="Budget, forecast, actual cost, and variance signals" /></CardHeader><CardContent><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><MetricCard label={common("budget")} value={formatNullableCurrency(project.budget, project.currency)} icon={Banknote} detail={project.currency} /><MetricCard label={common("forecast")} value="—" icon={CircleDollarSign} detail="No forecast value in current project record" /><MetricCard label={t("actualCost")} value={formatNullableCurrency(project.actualCost, project.currency)} icon={WalletCards} detail={project.currency} /><MetricCard label={t("costVariance")} value={formatNullablePercentage(project.costVariance, 1, true)} icon={CheckCircle2} valueClassName={isFiniteNumber(project.costVariance) && project.costVariance < 0 ? "text-status-red" : "text-status-green"} /></div><div className="mt-4 rounded-lg border bg-muted/20 p-4"><div className="flex items-center justify-between text-xs"><span className="text-muted-foreground">{t("actualCost")} vs {common("budget")}</span><span className="font-medium">{formatNullablePercentage(actualCostRatio)}</span></div><Progress value={clampProgress(actualCostRatio)} className="mt-2 h-2" /></div></CardContent></Card></TabsContent>

        <TabsContent value="risks-issues" className="space-y-4 pt-1"><div className="grid gap-4 lg:grid-cols-2"><Card className="shadow-none"><CardHeader><SectionHeader title={t("risks")} description="Risk register entries linked to this project" /></CardHeader><CardContent><EmptyState icon={ShieldAlert} title={t("noRisksYet")} description="No project risk records are available in the current detail payload." /></CardContent></Card><Card className="shadow-none"><CardHeader><SectionHeader title={t("issues")} description="Issue register entries linked to this project" /></CardHeader><CardContent><EmptyState icon={AlertTriangle} title={t("noIssuesYet")} description="No project issue records are available in the current detail payload." /></CardContent></Card></div></TabsContent>

        <TabsContent value="status-reports" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("statusReports")} description="Periodic delivery updates and executive reporting" /></CardHeader><CardContent><EmptyState icon={ClipboardList} title="No status reports" description="No status report records are currently available for this project." /></CardContent></Card></TabsContent>
        <TabsContent value="stakeholders" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("stakeholders")} description="People and groups with an interest in the project" /></CardHeader><CardContent><EmptyState icon={Users} title="No stakeholders" description="No stakeholder records are currently available for this project." /></CardContent></Card></TabsContent>
        <TabsContent value="meetings" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={common("meetings")} description="Project meetings and governance sessions" /></CardHeader><CardContent><EmptyState icon={CalendarDays} title="No project meetings" description="No meeting records are currently available for this project." /></CardContent></Card></TabsContent>
        <TabsContent value="documents" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={common("documents")} description="Documents associated with the project" action={<Link href="/documents"><Button size="sm" variant="outline"><Files className="mr-1.5 h-3.5 w-3.5" />Open documents</Button></Link>} /></CardHeader><CardContent><EmptyState icon={FileText} title="No project documents" description="No document records are currently available in the project detail payload." /></CardContent></Card></TabsContent>

        <TabsContent value="handover" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("handover")} description={t("handoverDescription")} /></CardHeader><CardContent>{handoverRequestsLoading ? <Skeleton className="h-24" /> : handoverRequests && handoverRequests.length > 0 ? <div className="divide-y">{handoverRequests.map((request) => <div key={request.id} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-start gap-3"><ArrowRightLeft className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><div className="min-w-0"><p className="text-sm font-medium">{request.currentProjectManagerName} → {request.newProjectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{t("effectiveHandoverDate")}: {displayDate(request.effectiveHandoverDate)} · {t("requestedBy")}: {request.requestedByName}</p>{request.reason && <p className="mt-1 text-xs text-muted-foreground">{request.reason}</p>}{request.rejectionReason && <p className="mt-1 text-xs text-destructive">{t("rejectionReason")}: {request.rejectionReason}</p>}</div></div><Badge variant={request.status === "Rejected" ? "destructive" : request.status === "Pending" || request.status === "Accepted" ? "default" : "secondary"} className="w-fit text-[10px]">{t(`handoverStatus.${request.status}`) || request.status}</Badge></div>)}</div> : <EmptyState icon={ArrowRightLeft} title={t("noHandoverRequests")} description="Create a handover request when project ownership needs to change." action={<Button size="sm" onClick={() => setShowHandoverSheet(true)}><ArrowRightLeft className="mr-1.5 h-3.5 w-3.5" />{t("handover")}</Button>} />}</CardContent></Card></TabsContent>

        <TabsContent value="history" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("managerHistory")} description="Project Manager ownership history" /></CardHeader><CardContent>{historyLoading ? <Skeleton className="h-24" /> : managerHistory && managerHistory.length > 0 ? <div className="divide-y">{managerHistory.map((entry) => <div key={entry.id} className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-start gap-3"><HistoryIcon className="mt-0.5 h-4 w-4 text-muted-foreground" /><div><p className="text-sm font-medium">{entry.projectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{displayDate(entry.startDate)} – {entry.endDate ? displayDate(entry.endDate) : t("current")}{entry.handoverFromProjectManagerName ? ` · ${common("from")}: ${entry.handoverFromProjectManagerName}` : ""}</p>{entry.handoverReason && <p className="mt-1 text-xs text-muted-foreground">{entry.handoverReason}</p>}</div></div>{entry.isCurrent ? <Badge variant="outline" className="w-fit text-[10px]">{t("current")}</Badge> : <Badge variant="secondary" className="w-fit text-[10px]">{displayDate(entry.endDate)}</Badge>}</div>)}</div> : <EmptyState icon={HistoryIcon} title={t("noManagerHistory")} description="Manager changes will be recorded here as handovers are completed." />}</CardContent></Card></TabsContent>

        <TabsContent value="snapshots" className="pt-1"><Card className="shadow-none"><CardHeader><SectionHeader title={t("snapshots")} description={t("snapshotsDescription")} /></CardHeader><CardContent>{snapshotsLoading ? <Skeleton className="h-24" /> : snapshots && snapshots.length > 0 ? <div className="divide-y">{snapshots.map((snapshot) => <div key={snapshot.id} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-start gap-3"><Camera className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><div className="min-w-0"><p className="text-sm font-medium">{snapshot.snapshotName}</p><p className="mt-1 text-xs text-muted-foreground">{t("effectiveEndDate")}: {displayDate(snapshot.effectiveEndDate)} · {t("snapshotDate")}: {displayDate(snapshot.snapshotDate)}</p><p className="mt-1 text-xs text-muted-foreground">{snapshot.previousProjectManagerName || "—"} → {snapshot.newProjectManagerName || "—"}</p></div></div><Link href={`/projects/${projectId}/snapshots/${snapshot.id}`}><Button variant="outline" size="sm"><Camera className="mr-1.5 h-3.5 w-3.5" />{t("viewSnapshot")}</Button></Link></div>)}</div> : <EmptyState icon={Camera} title={t("noSnapshots")} description={t("snapshotsDescription")} />}</CardContent></Card></TabsContent>
        <TabsContent value="change-tracker" className="pt-1"><LazyChangeTracker projectId={projectId} /></TabsContent>
      </Tabs>

      {showEditSheet ? (
        <LazyEditProjectSheet open={showEditSheet} onOpenChange={setShowEditSheet} project={project} />
      ) : null}
      <LazyProjectHandoverSheet open={showHandoverSheet} onOpenChange={setShowHandoverSheet} project={project} requests={handoverRequests ?? []} />
      <LazyProjectChangeRequestSheet open={showChangeRequestSheet} onOpenChange={setShowChangeRequestSheet} project={project} />
    </div>
  );
}