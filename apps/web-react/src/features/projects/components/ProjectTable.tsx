import * as React from "react";
import { cn } from "@lib/utils";
import { NavLink } from "react-router-dom";
import { useI18n } from "@i18n/messages";
import { Skeleton } from "@components/ui/Skeleton";

interface ProjectTableProps {
  data: { items: readonly any[]; totalCount: number; page: number; totalPages: number } | undefined;
  isLoading: boolean;
  error: boolean;
}

export function ProjectTable({ data, isLoading, error }: ProjectTableProps) {
  const { t, formatNumber } = useI18n();

  if (isLoading) {
    return (
      <div className="space-y-3">
        {[1, 2, 3, 4, 5].map((i) => (
          <Skeleton key={i} className="h-12" />
        ))}
      </div>
    );
  }

  if (error) {
    return <div className="text-center py-8 text-destructive">Failed to load projects</div>;
  }

  if (!data?.items.length) {
    return <div className="text-center py-8 text-muted-foreground">{t("projects.noRecords")}</div>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full" role="grid">
        <thead>
          <tr className="border-b">
            <th className="text-left p-3 font-medium text-muted-foreground">{t("projects.project")}</th>
            <th className="text-left p-3 font-medium text-muted-foreground">{t("projects.pm")}</th>
            <th className="text-left p-3 font-medium text-muted-foreground">{t("common.status")}</th>
            <th className="text-left p-3 font-medium text-muted-foreground">{t("common.health")}</th>
            <th className="text-left p-3 font-medium text-muted-foreground">{t("common.progress")}</th>
            <th className="text-left p-3 font-medium text-muted-foreground">{t("projects.budget")}</th>
            <th className="text-right p-3 font-medium text-muted-foreground">{t("common.actions")}</th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((project) => (
            <tr key={project.id} className="border-b hover:bg-muted/30 transition-colors">
              <td className="p-3">
                <NavLink to={`/projects/${project.id}`} className="font-medium hover:text-primary transition-colors">
                  {project.name}
                </NavLink>
                <p className="text-sm text-muted-foreground">{project.code}</p>
              </td>
              <td className="p-3">{project.projectManagerName || "—"}</td>
              <td className="p-3">
                <span className={cn("px-2 py-0.5 rounded text-xs", getStatusColor(project.status))}>
                  {project.status}
                </span>
              </td>
              <td className="p-3">
                <span className={cn("px-2 py-0.5 rounded text-xs", getHealthColor(project.health))}>
                  {project.health}
                </span>
              </td>
              <td className="p-3">
                <div className="flex items-center gap-2">
                  <div className="flex-1 max-w-[100px] h-2 bg-muted rounded-full overflow-hidden">
                    <div className="h-full bg-primary rounded-full transition-all" style={{ width: `${project.progress}%` }} />
                  </div>
                  <span className="text-sm text-muted-foreground">{project.progress}%</span>
                </div>
              </td>
              <td className="p-3">
                {project.budget && (
                  <span className="font-medium">{formatNumber(project.budget)} {project.currency}</span>
                )}
              </td>
              <td className="p-3 text-right">
                <NavLink to={`/projects/${project.id}`} className="text-primary hover:underline text-sm">
                  {t("common.details")}
                </NavLink>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function getStatusColor(status: string) {
  switch (status?.toLowerCase()) {
    case "active":
      return "bg-green-100 text-green-700";
    case "completed":
      return "bg-blue-100 text-blue-700";
    case "archived":
      return "bg-gray-100 text-gray-700";
    case "on hold":
      return "bg-yellow-100 text-yellow-700";
    default:
      return "bg-gray-100 text-gray-700";
  }
}

function getHealthColor(health: string) {
  switch (health?.toLowerCase()) {
    case "green":
      return "bg-green-100 text-green-700";
    case "yellow":
      return "bg-yellow-100 text-yellow-700";
    case "red":
      return "bg-red-100 text-red-700";
    default:
      return "bg-gray-100 text-gray-700";
  }
}