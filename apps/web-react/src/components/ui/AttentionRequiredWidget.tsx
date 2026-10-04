import { useI18n } from "@i18n/messages";
import type { HomeProjectSummary } from "@api/home";

export function AttentionRequiredWidget({ projects }: { projects: HomeProjectSummary[] }) {
  const { t } = useI18n();

  if (!projects.length) {
    return <div className="py-8 text-center text-sm text-muted-foreground">{t("dashboard.noAttention")}</div>;
  }

  return (
    <div className="space-y-3">
      {projects.slice(0, 5).map((project) => (
        <div key={project.id} className="rounded-lg border bg-muted/30 p-3">
          <p className="truncate text-sm font-medium">{project.name}</p>
          <p className="mt-1 text-xs text-muted-foreground">{project.progress}% · {project.health}</p>
        </div>
      ))}
    </div>
  );
}
