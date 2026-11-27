import * as React from "react";
import { useLocation, useParams, Outlet, useSearchParams } from "react-router-dom";
import { AlertTriangle } from "lucide-react";
import { useProjectHandoverCapabilities, useProjectHandoverRequests, useProjectOverview, useProjectShell } from "./api/projects";
import { ProjectHeader } from "./components/ProjectHeader";
import { ProjectChangeRequestActionSheet, ProjectHandoverActionSheet } from "./components/ProjectOverviewActionSheets";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent } from "@components/ui/Card";
import { Skeleton } from "@components/ui/Skeleton";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useI18n();
  const { user, refreshUser } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();

  React.useEffect(() => {
    if (id) {
      void refreshUser();
    }
  }, [id, refreshUser]);
  const [changeRequestOpen, setChangeRequestOpen] = React.useState(false);
  const [handoverOpen, setHandoverOpen] = React.useState(false);
  const [projectSettingsOpen, setProjectSettingsOpen] = React.useState(false);
  const openSettingsRequested = searchParams.get("openSettings") === "1";

  React.useEffect(() => {
    setProjectSettingsOpen(false);
  }, [id]);

  React.useEffect(() => {
    if (!openSettingsRequested) return;
    setProjectSettingsOpen(true);
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.delete("openSettings");
    setSearchParams(nextSearchParams, { replace: true });
  }, [openSettingsRequested, searchParams, setSearchParams]);

  // VISIBLE-FIRST: shell only on detail mount. Each tab fetches its own data when active.
  const shellQuery = useProjectShell(id ?? "", Boolean(id));
  const overviewIsActive = Boolean(id && /\/overview\/?$/.test(location.pathname));
  const overviewQuery = useProjectOverview(id ?? "", overviewIsActive);
  const canAccessHandover = Boolean(user && hasPermission(user.permissions, Permissions.Handover.View)
    || user && hasPermission(user.permissions, Permissions.Handover.Manage)
    || user && hasPermission(user.permissions, Permissions.Handover.AdminInitiate));
  const handoverCapabilities = useProjectHandoverCapabilities(id ?? "", canAccessHandover);
  const handoverQuery = useProjectHandoverRequests(id ?? "", handoverOpen && overviewIsActive && canAccessHandover);
  const canManageChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
  const canManageHandovers = Boolean(canMutate && !effectiveReadOnly && (handoverCapabilities.data?.canInitiate || handoverCapabilities.data?.canAdminInitiate || handoverCapabilities.data?.canAssignInitial));

  React.useEffect(() => {
    if (overviewIsActive) return;
    setChangeRequestOpen(false);
    setHandoverOpen(false);
  }, [overviewIsActive]);

  if (shellQuery.isLoading) {
    return (
      <div className="space-y-4">
        <PageHeader title={t("projects.title")} description={t("projects.loadingProjectDetails")} />
        <Card><CardContent className="h-32 p-4"><Skeleton className="h-full" /></CardContent></Card>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><Skeleton className="h-24" /><Skeleton className="h-24" /><Skeleton className="h-24" /><Skeleton className="h-24" /></div>
        <Card><CardContent className="h-72 p-4"><Skeleton className="h-full" /></CardContent></Card>
      </div>
    );
  }

  if (shellQuery.isError || !shellQuery.data) {
    return (
      <div className="space-y-4">
        <PageHeader title={t("projects.title")} />
        <Card><CardContent className="py-12 text-center"><AlertTriangle className="mx-auto h-10 w-10 text-destructive" /><p className="mt-3 text-sm text-muted-foreground">{t("common.notFound")}</p></CardContent></Card>
      </div>
    );
  }

  const project = shellQuery.data;

  return (
    <div className="space-y-4">
      <ProjectHeader
        project={project}
        statusOverride={overviewIsActive ? overviewQuery.data?.statusReports.latestPublishedReport?.projectLifecycleStatusAtReport : undefined}
        healthOverride={overviewIsActive ? overviewQuery.data?.statusReports.latestPublishedReport?.overallHealth : undefined}
        onChangeRequest={overviewIsActive && canManageChangeRequests ? () => setChangeRequestOpen(true) : undefined}
        onHandover={overviewIsActive && canManageHandovers ? () => setHandoverOpen(true) : undefined}
        canInitiateHandover={canManageHandovers}
        projectSettingsOpen={projectSettingsOpen}
        onProjectSettingsOpenChange={setProjectSettingsOpen}
      />
      <Outlet context={{ projectId: id!, project }} />
      {overviewIsActive && canManageChangeRequests ? <ProjectChangeRequestActionSheet open={changeRequestOpen} onOpenChange={setChangeRequestOpen} project={project} /> : null}
      {overviewIsActive && canManageHandovers ? <ProjectHandoverActionSheet open={handoverOpen} onOpenChange={setHandoverOpen} project={project} requests={handoverQuery.data ?? []} requestsLoading={handoverQuery.isLoading} requestsError={handoverQuery.isError} /> : null}
    </div>
  );
}
