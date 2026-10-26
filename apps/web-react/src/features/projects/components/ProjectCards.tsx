import { useNavigate } from "react-router-dom";
import { useI18n } from "@i18n/messages";
import { formatDate } from "@lib/utils";
import { Badge } from "@components/ui/Badge";
import { Card, CardContent } from "@components/ui/Card";
import { Progress } from "@components/ui/Progress";
import { Skeleton } from "@components/ui/Skeleton";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { FolderKanban } from "lucide-react";
import type { ProjectSummaryDto } from "../api/projects";
import { HealthDot, getInitials } from "./ProjectTable";

interface ProjectCardsProps {
  data: { items: readonly ProjectSummaryDto[]; totalCount: number } | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function ProjectCards({ data, isLoading, isError, onRetry }: ProjectCardsProps) {
  const { t, locale, formatCurrency } = useI18n();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="grid gap-4 p-4 sm:grid-cols-2 xl:grid-cols-3" role="status" aria-label={t("common.loading")}>
        {[1, 2, 3, 4, 5, 6].map((i) => (
          <div key={i} className="space-y-3 rounded-lg border p-4">
            <Skeleton className="h-5 w-2/3" />
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-2 w-full" />
            <div className="flex justify-between">
              <Skeleton className="h-6 w-20" />
              <Skeleton className="h-6 w-16" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (isError) {
    return <ErrorState title={t("common.errorLoading")} onRetry={onRetry} />;
  }

  if (!data?.items.length) {
    return <EmptyState title={t("projects.noRecords")} description={t("projects.totalRecords", { count: 0 })} icon={<FolderKanban className="h-10 w-10" />} />;
  }

  return (
    <div className="grid gap-4 p-4 sm:grid-cols-2 xl:grid-cols-3">
      {data.items.map((project) => (
        <Card
          key={project.id}
          role="link"
          tabIndex={0}
          aria-label={project.name}
          onClick={() => navigate(`/projects/${project.publicId}/overview`)}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              navigate(`/projects/${project.publicId}/overview`);
            }
          }}
          className="cursor-pointer transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-md"
        >
          <CardContent className="space-y-3 p-4">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="truncate font-semibold">{project.name}</p>
                <p className="mt-0.5 font-mono text-[11px] text-muted-foreground">{project.code}</p>
              </div>
              <Badge status={project.status}>{project.status}</Badge>
            </div>
            <div className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
              <span className="inline-flex min-w-0 items-center gap-1.5">
                <HealthDot health={project.health} />
                <span className="truncate">{project.health || "—"}</span>
              </span>
              {project.endDate ? <span className="shrink-0 tabular-nums">{formatDate(project.endDate, locale)}</span> : null}
            </div>
            <div className="w-full space-y-1">
              <div className="flex items-center justify-between gap-2 text-xs">
                <span className="text-muted-foreground">{t("projects.progressSourceWorkingPlan")}</span>
                <span className="tabular-nums">{project.planProgress == null ? "—" : `${Math.round(project.planProgress)}%`}</span>
              </div>
              {project.planProgress != null ? <Progress value={project.planProgress} className="w-full" /> : null}
            </div>
            <div className="flex items-center justify-between gap-2 border-t pt-3 text-xs">
              <span className="inline-flex min-w-0 items-center gap-1.5">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-[10px] font-semibold text-primary">
                  {getInitials(project.projectManagerName)}
                </span>
                <span className="truncate text-muted-foreground">{project.projectManagerName || "—"}</span>
              </span>
              <span className="shrink-0 font-semibold tabular-nums">
                {project.budget ? formatCurrency(project.budget, project.currency) : "—"}
              </span>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
