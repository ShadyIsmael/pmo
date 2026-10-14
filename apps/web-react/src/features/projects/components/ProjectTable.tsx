import { NavLink, useNavigate } from "react-router-dom";
import { useI18n } from "@i18n/messages";
import { formatDate } from "@lib/utils";
import { cn } from "@lib/utils";
import { Badge } from "@components/ui/Badge";
import { Progress } from "@components/ui/Progress";
import { Skeleton } from "@components/ui/Skeleton";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { FolderKanban } from "lucide-react";
import type { ProjectSummaryDto } from "../api/projects";

interface ProjectTableProps {
  data: { items: readonly ProjectSummaryDto[]; totalCount: number; page: number; totalPages: number } | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}

export function getInitials(name?: string | null): string {
  if (!name) return "—";
  const parts = name.trim().split(/\s+/).map((p) => p[0]).join("").slice(0, 2).toUpperCase();
  return parts || "—";
}

export function HealthDot({ health, className }: { health?: string | null; className?: string }) {
  const tone =
    health === "Green" ? "bg-success" : health === "Amber" || health === "Yellow" ? "bg-warning" : health === "Red" ? "bg-destructive" : "bg-muted-foreground/40";
  return <span aria-hidden="true" className={cn("inline-block h-2 w-2 shrink-0 rounded-full", tone, className)} />;
}

export function ProjectTable({ data, isLoading, isError, onRetry }: ProjectTableProps) {
  const { t, locale, formatCurrency } = useI18n();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="space-y-2 p-4" role="status" aria-label={t("common.loading")}>
        {[1, 2, 3, 4, 5, 6].map((i) => (
          <div key={i} className="flex items-center gap-4">
            <Skeleton className="h-9 w-9 rounded-full" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="h-3 w-1/4" />
            </div>
            <Skeleton className="hidden h-6 w-20 sm:block" />
            <Skeleton className="hidden h-6 w-24 md:block" />
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
    <div className="overflow-x-auto">
      <table className="w-full caption-bottom text-sm" role="grid" aria-label={t("projects.title")}>
        <thead>
          <tr className="border-b bg-muted/40 text-start text-xs text-muted-foreground">
            <th className="px-4 py-3 text-start font-semibold">{t("projects.project")}</th>
            <th className="hidden px-4 py-3 text-start font-semibold lg:table-cell">{t("projects.pm")}</th>
            <th className="px-4 py-3 text-start font-semibold">{t("common.status")}</th>
            <th className="px-4 py-3 text-start font-semibold">{t("common.health")}</th>
            <th className="hidden px-4 py-3 text-start font-semibold xl:table-cell">{t("common.progress")}</th>
            <th className="hidden px-4 py-3 text-end font-semibold md:table-cell">{t("projects.budget")}</th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((project) => (
            <tr
              key={project.id}
              onClick={() => navigate(`/projects/${project.id}/overview`)}
              className="cursor-pointer border-b transition-colors last:border-0 hover:bg-muted/40"
            >
              <td className="max-w-[260px] px-4 py-3">
                <span className="block truncate font-semibold hover:text-primary hover:underline">{project.name}</span>
                <span className="mt-0.5 flex items-center gap-1.5 text-[11px] text-muted-foreground">
                  <span className="font-mono">{project.code}</span>
                  {project.endDate ? <span>· {formatDate(project.endDate, locale)}</span> : null}
                </span>
              </td>
              <td className="hidden max-w-[160px] px-4 py-3 lg:table-cell">
                {project.projectManagerName ? (
                  <span className="flex items-center gap-2">
                    <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-[11px] font-semibold text-primary">
                      {getInitials(project.projectManagerName)}
                    </span>
                    <span className="truncate">{project.projectManagerName}</span>
                  </span>
                ) : (
                  <span className="text-muted-foreground">—</span>
                )}
              </td>
              <td className="whitespace-nowrap px-4 py-3">
                <Badge status={project.status}>{project.status}</Badge>
              </td>
              <td className="whitespace-nowrap px-4 py-3">
                <span className="inline-flex items-center gap-1.5 text-[13px]">
                  <HealthDot health={project.health} />
                  {project.health || "—"}
                </span>
              </td>
              <td className="hidden min-w-[140px] px-4 py-3 xl:table-cell">
                <span className="flex items-center gap-2">
                  <Progress value={project.progress} className="flex-1" />
                  <span className="w-10 text-end text-xs tabular-nums text-muted-foreground">{project.progress}%</span>
                </span>
              </td>
              <td className="hidden whitespace-nowrap px-4 py-3 text-end tabular-nums md:table-cell">
                {project.budget ? formatCurrency(project.budget, project.currency) : <span className="text-muted-foreground">—</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="px-4 py-2 text-xs text-muted-foreground">
        <NavLink to="/projects" className="sr-only">{t("projects.viewAllCount", { count: data.totalCount })}</NavLink>
        {t("projects.totalRecords", { count: data.totalCount })}
      </p>
    </div>
  );
}
