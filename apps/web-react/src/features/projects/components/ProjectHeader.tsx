import { NavLink } from "react-router-dom";
import { cn } from "@lib/utils";
import { LayoutDashboard, ListTodo, TriangleAlert, DollarSign, Users, FileText, Calendar, Settings, MoreHorizontal } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@components/ui/DropdownMenu";
import { useArchiveProject, useReactivateProject } from "../api/projects";
import { useManagement } from "@api/management";

interface ProjectHeaderProps {
  project: { id: string; code: string; name: string; description?: string; portfolioName?: string; progress: number; health: string; budget?: number; actualCost?: number; currency: string };
}

export function ProjectHeader({ project }: ProjectHeaderProps) {
  const { t, formatCurrency } = useI18n();
  const { canMutate } = useManagement();
  const archive = useArchiveProject();
  const reactivate = useReactivateProject();

  const tabs = [
    { id: "overview", label: t("projects.tabs.overview"), href: `/projects/${project.id}/overview`, icon: LayoutDashboard, end: false },
    { id: "tasks", label: t("projects.tabs.tasks"), href: `/projects/${project.id}/tasks`, icon: ListTodo, end: true },
    { id: "risks", label: t("projects.tabs.risks"), href: `/projects/${project.id}/risks`, icon: TriangleAlert, end: true },
    { id: "finance", label: t("projects.tabs.finance"), href: `/projects/${project.id}/finance`, icon: DollarSign, end: true },
    { id: "members", label: t("projects.tabs.members"), href: `/projects/${project.id}/members`, icon: Users, end: true },
    { id: "documents", label: t("projects.tabs.documents"), href: `/projects/${project.id}/documents`, icon: FileText, end: true },
    { id: "planning", label: t("projects.tabs.planning"), href: `/projects/${project.id}/planning`, icon: Calendar, end: true },
    { id: "workflows", label: t("projects.tabs.workflows"), href: `/projects/${project.id}/workflows`, icon: Settings, end: true },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <span className="rounded bg-muted px-2 py-0.5 font-mono text-xs">{project.code}</span>
            {project.portfolioName ? <span className="truncate">{project.portfolioName}</span> : null}
          </div>
          <h1 className="truncate text-2xl font-bold">{project.name}</h1>
          <p className="mt-1 text-muted-foreground">{project.description || "—"}</p>
        </div>
        {canMutate ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" aria-label={t("common.actions")}>
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onSelect={() => archive.mutate(project.id)}>{t("projects.archive")}</DropdownMenuItem>
              <DropdownMenuItem onSelect={() => reactivate.mutate(project.id)}>{t("projects.reactivate")}</DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem className="text-destructive">{t("projects.delete")}</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <div className="rounded-lg bg-muted/30 p-4">
          <p className="text-sm text-muted-foreground">{t("projects.overallProgressLabel")}</p>
          <p className="text-2xl font-bold tabular-nums">{project.progress}%</p>
        </div>
        <div className="rounded-lg bg-muted/30 p-4">
          <p className="text-sm text-muted-foreground">{t("projects.health")}</p>
          <p className="text-2xl font-bold capitalize">{project.health}</p>
        </div>
        <div className="rounded-lg bg-muted/30 p-4">
          <p className="text-sm text-muted-foreground">{t("projects.budgetCeiling")}</p>
          <p className="text-2xl font-bold tabular-nums">{project.budget ? formatCurrency(project.budget, project.currency) : "—"}</p>
        </div>
        <div className="rounded-lg bg-muted/30 p-4">
          <p className="text-sm text-muted-foreground">{t("projects.actualSpend")}</p>
          <p className="text-2xl font-bold tabular-nums">{project.actualCost ? formatCurrency(project.actualCost, project.currency) : "—"}</p>
        </div>
      </div>

      <nav className="flex gap-1 overflow-x-auto pb-2" aria-label="Project tabs">
        {tabs.map((tab) => (
          <NavLink
            key={tab.id}
            to={tab.href}
            end={tab.end}
            className={({ isActive }) =>
              cn(
                "flex items-center gap-1.5 whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium transition-colors",
                isActive ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              )
            }
          >
            <tab.icon className="h-4 w-4" />
            {tab.label}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
