"use client";

import * as React from "react";
import { useTranslations } from "next-intl";
import { Search } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { EmptyState, ErrorState, LoadingSkeleton, PermissionDenied } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TablePagination } from "@/components/ui/table-pagination";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { useDepartments, useTeams, type TeamStatusFilter } from "@/hooks/resources/useTeams";
import { AddTeamButton } from "@/components/resources/TeamFormSheets";
import { TeamRowActions } from "@/components/resources/TeamRowActions";
import { getLookupTextClass, getStatusTextClass } from "@/lib/status-presentation";

const PAGE_SIZE = 20;

export default function TeamsPage() {
  const t = useTranslations("resources.teams");
  const { user, status: authStatus } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Team.View));

  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [departmentId, setDepartmentId] = React.useState("");
  const [status, setStatus] = React.useState<TeamStatusFilter>("all");
  const deferredSearch = React.useDeferredValue(search);

  const filters = React.useMemo(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      search: deferredSearch.trim() || undefined,
      departmentId: departmentId || undefined,
      isActive: status === "all" ? undefined : status === "active",
    }),
    [page, deferredSearch, departmentId, status],
  );

  const canQuery = authStatus === "authenticated" && canView;
  const teamsQuery = useTeams(filters, canQuery);
  const departmentsQuery = useDepartments(false, canQuery);

  const teams = teamsQuery.data?.items ?? [];
  const departments = departmentsQuery.data ?? [];

  React.useEffect(() => {
    setPage(1);
  }, [deferredSearch, departmentId, status]);

  if (authStatus === "loading") {
    return <LoadingSkeleton rows={6} />;
  }

  if (!canView) {
    return <PermissionDenied title={t("permissionTitle")} description={t("permissionRequired")} />;
  }

  const totalPages = teamsQuery.data?.totalPages ?? 1;
  const hasFilters = Boolean(search || departmentId || status !== "all");
  const clearFilters = () => {
    setSearch("");
    setDepartmentId("");
    setStatus("all");
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("listTitle")}
        description={t("listDescription", { count: teamsQuery.data?.totalCount ?? 0 })}
        actions={<AddTeamButton />}
      />

      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-muted/20 px-3 py-2.5">
        <div className="relative min-w-56 flex-1">
          <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" aria-hidden="true" />
          <Input
            placeholder={t("searchPlaceholder")}
            className="bg-background ps-8"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            aria-label={t("searchPlaceholder")}
          />
        </div>
        <label className="sr-only" htmlFor="team-department-filter">
          {t("filterDepartment")}
        </label>
        <select
          id="team-department-filter"
          className="h-10 w-52 rounded-md border border-input bg-background px-3 text-sm"
          value={departmentId}
          onChange={(event) => setDepartmentId(event.target.value)}
        >
          <option value="">{t("allDepartments")}</option>
          {departments.map((department) => (
            <option key={department.id} value={department.id}>
              {department.name}
            </option>
          ))}
        </select>
        <label className="sr-only" htmlFor="team-status-filter">
          {t("filterStatus")}
        </label>
        <select
          id="team-status-filter"
          className="h-10 w-40 rounded-md border border-input bg-background px-3 text-sm"
          value={status}
          onChange={(event) => setStatus(event.target.value as TeamStatusFilter)}
        >
          <option value="all">{t("allStatuses")}</option>
          <option value="active">{t("active")}</option>
          <option value="inactive">{t("inactive")}</option>
        </select>
        {hasFilters ? (
          <Button variant="ghost" size="sm" onClick={clearFilters}>
            {t("clearFilters")}
          </Button>
        ) : null}
      </div>

      {teamsQuery.isLoading ? (
        <LoadingSkeleton rows={5} />
      ) : teamsQuery.isError ? (
        <ErrorState
          title={t("loadError")}
          description={teamsQuery.error instanceof Error ? teamsQuery.error.message : undefined}
          onRetry={() => void teamsQuery.refetch()}
        />
      ) : teams.length === 0 ? (
        <EmptyState title={t("empty")} description={t("emptyDescription")} action={<AddTeamButton />} />
      ) : (
        <TooltipProvider>
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="w-56 text-start">{t("name")}</TableHead>
                <TableHead className="w-32 text-start">{t("code")}</TableHead>
                <TableHead className="w-44 text-start">{t("department")}</TableHead>
                <TableHead className="w-40 text-start">{t("teamLead")}</TableHead>
                <TableHead className="w-24 text-start">{t("members")}</TableHead>
                <TableHead className="w-28 text-start">{t("status")}</TableHead>
                <TableHead className="w-14 text-end">{t("actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {teams.map((team) => (
                <TableRow key={team.id}>
                  <TableCell className="w-56 text-start align-middle">
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <p className={cn("truncate font-medium", !team.isActive && "text-muted-foreground")}>
                          {team.name}
                        </p>
                      </TooltipTrigger>
                      <TooltipContent side="top" className="max-w-xs">
                        <p className="font-medium">{team.name}</p>
                        {team.description ? (
                          <p className="text-xs text-muted-foreground">{team.description}</p>
                        ) : null}
                      </TooltipContent>
                    </Tooltip>
                  </TableCell>
                  <TableCell className="w-32 text-start align-middle" title={team.code ?? undefined}>
                    <span className="font-mono text-xs font-medium tracking-wide">{team.code ?? "—"}</span>
                  </TableCell>
                  <TableCell className="w-44 text-start align-middle">
                    {team.departmentName ? (
                      <span className={cn("block max-w-full truncate text-sm font-medium", getLookupTextClass(team.departmentName))}>
                        {team.departmentName}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="max-w-40 truncate text-start align-middle" title={team.teamLeadName ?? undefined}>
                    {team.teamLeadName ?? <span className="text-muted-foreground">—</span>}
                  </TableCell>
                  <TableCell className="w-24 text-start align-middle">
                    <span className="text-sm tabular-nums">{team.memberCount}</span>
                  </TableCell>
                  <TableCell className="w-28 text-start align-middle">
                    <span className={`text-sm font-medium ${getStatusTextClass(team.isActive ? "Active" : "Inactive")}`}>
                      {team.isActive ? t("active") : t("inactive")}
                    </span>
                  </TableCell>
                  <TableCell className="w-14 text-end align-middle">
                    <TeamRowActions team={team} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TooltipProvider>
      )}

      {!teamsQuery.isLoading && !teamsQuery.isError && teams.length > 0 ? (
        <TablePagination
          page={page}
          totalPages={totalPages}
          summary={t("pageSummary", {
            page,
            totalPages,
            total: teamsQuery.data?.totalCount ?? 0,
          })}
          previousLabel={t("previous")}
          nextLabel={t("next")}
          onPageChange={setPage}
        />
      ) : null}
    </div>
  );
}
