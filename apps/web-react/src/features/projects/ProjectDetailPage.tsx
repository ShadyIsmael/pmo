import { useParams, Outlet } from "react-router-dom";
import { useProjectShell } from "./api/projects";
import { ProjectHeader } from "./components/ProjectHeader";
import { useI18n } from "@i18n/messages";

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useI18n();

  // VISIBLE-FIRST: shell only on detail mount. Each tab fetches its own data when active.
  const shellQuery = useProjectShell(id ?? "", Boolean(id));

  if (shellQuery.isLoading) {
    return <div className="flex h-64 items-center justify-center text-sm text-muted-foreground">{t("common.loading")}</div>;
  }

  if (shellQuery.isError || !shellQuery.data) {
    return <div className="py-12 text-center text-destructive">{t("projects.notFound")}</div>;
  }

  const project = shellQuery.data;

  return (
    <div className="space-y-4">
      <ProjectHeader project={project} />
      <Outlet context={{ projectId: project.id, project }} />
    </div>
  );
}
