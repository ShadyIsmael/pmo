import * as React from "react";
import { Search, Plus, MoreHorizontal } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { Label } from "@components/ui/Label";
import { Pagination } from "@components/ui/Pagination";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@components/ui/DropdownMenu";
import { useTeams, useCreateTeam, useUpdateTeam, useDeactivateTeam, useReactivateTeam, useDeleteTeam, useTeamMembers, useSetTeamMembers, useDepartments, type TeamDto } from "./api/teams";
import { useResources } from "@features/resources/api/resources";

const PAGE_SIZE = 20;

function TeamMembersPanel({ teamId }: { teamId: string }) {
  const { t } = useI18n();
  const members = useTeamMembers(teamId, true);
  const allResources = useResources({ page: 1, pageSize: 200, isActive: true }, Boolean(teamId));
  const setMembers = useSetTeamMembers();
  const [selected, setSelected] = React.useState<string[]>([]);

  React.useEffect(() => {
    if (members.data) setSelected(members.data.filter((m) => m.isActive).map((m) => m.resourceId));
  }, [members.data]);

  if (members.isLoading) return <p className="text-sm text-muted-foreground">{t("common.loading")}</p>;
  const options = (allResources.data?.items ?? []).filter((r) => r.isActive);
  const current = members.data ?? [];
  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">{t("resources.teams.memberCountLabel", { count: current.filter((m) => m.isActive).length })}</p>
      <div className="max-h-64 space-y-1 overflow-auto rounded-md border p-2">
        {options.map((r) => (
          <label key={r.id} className="flex items-center gap-2 rounded px-2 py-1 text-sm hover:bg-muted">
            <input type="checkbox" checked={selected.includes(r.id)} onChange={(e) => setSelected((s) => (e.target.checked ? [...s, r.id] : s.filter((id) => id !== r.id)))} />
            <span className="truncate">{r.name}</span>
          </label>
        ))}
      </div>
      <Button
        size="sm"
        disabled={setMembers.isPending}
        onClick={() => setMembers.mutate({ teamId, resourceIds: selected })}
      >
        {setMembers.isPending ? t("resources.teams.savingMembers") : t("resources.teams.saveMembers")}
      </Button>
    </div>
  );
}

function TeamRowActions({ team }: { team: TeamDto }) {
  const { t } = useI18n();
  const { user } = useAuth();
  const canEdit = Boolean(user && (user.permissions.includes(Permissions.Team.Edit) || user.permissions.includes(Permissions.Team.Manage)));
  const canDelete = Boolean(user && (user.permissions.includes(Permissions.Team.Delete) || user.permissions.includes(Permissions.Team.Manage)));
  const [editOpen, setEditOpen] = React.useState(false);
  const [membersOpen, setMembersOpen] = React.useState(false);
  const [dialog, setDialog] = React.useState<"deactivate" | "reactivate" | "delete" | null>(null);
  const update = useUpdateTeam();
  const deactivate = useDeactivateTeam();
  const reactivate = useReactivateTeam();
  const remove = useDeleteTeam();
  const [name, setName] = React.useState(team.name);
  const [description, setDescription] = React.useState(team.description ?? "");

  if (!canEdit && !canDelete) return null;
  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" className="h-8 w-8" aria-label={t("resources.teams.actions")}>
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onSelect={() => setMembersOpen(true)}>{t("resources.teams.manageMembers")}</DropdownMenuItem>
          {canEdit ? <DropdownMenuItem onSelect={() => { setName(team.name); setDescription(team.description ?? ""); setEditOpen(true); }}>{t("resources.teams.edit")}</DropdownMenuItem> : null}
          {canDelete ? (
            <>
              <DropdownMenuSeparator />
              {team.isActive ? <DropdownMenuItem onSelect={() => setDialog("deactivate")}>{t("resources.teams.deactivate")}</DropdownMenuItem> : <DropdownMenuItem onSelect={() => setDialog("reactivate")}>{t("resources.teams.reactivate")}</DropdownMenuItem>}
              <DropdownMenuItem className="text-destructive" onSelect={() => setDialog("delete")}>{t("resources.teams.delete")}</DropdownMenuItem>
            </>
          ) : null}
        </DropdownMenuContent>
      </DropdownMenu>
      <SideSheet open={editOpen} onOpenChange={setEditOpen} title={t("resources.teams.editTitle")} description={t("resources.teams.editDescription")}>
        <SideSheetBody className="space-y-3">
          <div className="space-y-1.5"><Label>{t("resources.teams.name")}</Label><Input value={name} onChange={(e) => setName(e.target.value)} /></div>
          <div className="space-y-1.5"><Label>{t("resources.teams.description")}</Label><Input value={description} onChange={(e) => setDescription(e.target.value)} /></div>
        </SideSheetBody>
        <SideSheetFooter>
          <Button disabled={update.isPending || !name.trim()} onClick={() => update.mutate({ id: team.id, input: { name: name.trim(), description } }, { onSuccess: () => setEditOpen(false) })}>
            {t("resources.teams.save")}
          </Button>
        </SideSheetFooter>
      </SideSheet>
      <SideSheet open={membersOpen} onOpenChange={setMembersOpen} title={t("resources.teams.manageMembers")} description={team.name}>
        <SideSheetBody><TeamMembersPanel teamId={team.id} /></SideSheetBody>
      </SideSheet>
      <ConfirmDialog open={dialog === "deactivate"} onOpenChange={(o) => { if (!o) setDialog(null); }} title={t("resources.teams.deactivateTeamTitle", { name: team.name })} description={t("resources.teams.deactivateTeamDescription")} confirmLabel={t("resources.teams.deactivate")} onConfirm={() => deactivate.mutateAsync(team.id)} />
      <ConfirmDialog open={dialog === "reactivate"} onOpenChange={(o) => { if (!o) setDialog(null); }} title={t("resources.teams.reactivateTeamTitle", { name: team.name })} description={t("resources.teams.reactivateTeamDescription")} confirmLabel={t("resources.teams.reactivate")} onConfirm={() => reactivate.mutateAsync(team.id)} />
      <ConfirmDialog open={dialog === "delete"} onOpenChange={(o) => { if (!o) setDialog(null); }} title={t("resources.teams.deleteTeamTitle", { name: team.name })} description={`${t("resources.teams.deleteTeamDescription")} ${t("resources.teams.deleteTeamHint")}`} confirmLabel={t("resources.teams.delete")} destructive onConfirm={() => remove.mutateAsync(team.id)} />
    </>
  );
}

export function TeamsPage() {
  const { t } = useI18n();
  const { user, status } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Team.View));
  const canCreate = Boolean(user && (user.permissions.includes(Permissions.Team.Create) || user.permissions.includes(Permissions.Team.Manage)));
  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [departmentId, setDepartmentId] = React.useState("");
  const [filterStatus, setFilterStatus] = React.useState<"all" | "active" | "inactive">("all");
  const deferredSearch = React.useDeferredValue(search);
  const filters = React.useMemo(() => ({
    page, pageSize: PAGE_SIZE,
    search: deferredSearch.trim() || undefined,
    departmentId: departmentId || undefined,
    isActive: filterStatus === "all" ? undefined : filterStatus === "active",
  }), [page, deferredSearch, departmentId, filterStatus]);
  const teamsQuery = useTeams(filters, status === "authenticated" && canView);
  const departmentsQuery = useDepartments(false, status === "authenticated" && canView);
  const create = useCreateTeam();
  const [createOpen, setCreateOpen] = React.useState(false);
  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");

  React.useEffect(() => { setPage(1); }, [deferredSearch, departmentId, filterStatus]);

  if (status === "loading") return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (!canView) return <p className="py-12 text-center text-sm text-muted-foreground">{t("resources.teams.permissionRequired")}</p>;

  const teams = teamsQuery.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader
        title={t("resources.teams.listTitle")}
        description={t("resources.teams.listDescription", { count: teamsQuery.data?.totalCount ?? 0 })}
        actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("resources.teams.addTeam")}</Button> : undefined}
      />
      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-muted/20 px-3 py-2.5">
        <div className="relative min-w-56 flex-1">
          <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("resources.teams.searchPlaceholder")} className="ps-8" />
        </div>
        <select className="h-10 w-52 rounded-md border bg-background px-3 text-sm" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} aria-label={t("resources.teams.filterDepartment")}>
          <option value="">{t("resources.teams.allDepartments")}</option>
          {(departmentsQuery.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        <select className="h-10 w-40 rounded-md border bg-background px-3 text-sm" value={filterStatus} onChange={(e) => setFilterStatus(e.target.value as typeof filterStatus)} aria-label={t("resources.teams.filterStatus")}>
          <option value="all">{t("resources.teams.allStatuses")}</option>
          <option value="active">{t("resources.teams.active")}</option>
          <option value="inactive">{t("resources.teams.inactive")}</option>
        </select>
      </div>
      {teamsQuery.isLoading ? (
        <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : teamsQuery.isError ? (
        <p className="py-12 text-center text-sm text-destructive">{t("resources.teams.loadError")}</p>
      ) : teams.length === 0 ? (
        <p className="py-12 text-center text-sm text-muted-foreground">{t("resources.teams.empty")}</p>
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("resources.teams.name")}</TableHead>
                <TableHead>{t("resources.teams.code")}</TableHead>
                <TableHead>{t("resources.teams.department")}</TableHead>
                <TableHead>{t("resources.teams.teamLead")}</TableHead>
                <TableHead>{t("resources.teams.members")}</TableHead>
                <TableHead>{t("resources.teams.status")}</TableHead>
                <TableHead className="text-end">{t("resources.teams.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {teams.map((team) => (
                <TableRow key={team.id}>
                  <TableCell className="font-medium">{team.name}</TableCell>
                  <TableCell><span className="font-mono text-xs">{team.code ?? "—"}</span></TableCell>
                  <TableCell>{team.departmentName ?? "—"}</TableCell>
                  <TableCell>{team.teamLeadName ?? "—"}</TableCell>
                  <TableCell className="tabular-nums">{team.memberCount}</TableCell>
                  <TableCell>{team.isActive ? t("resources.teams.active") : t("resources.teams.inactive")}</TableCell>
                  <TableCell className="text-end"><TeamRowActions team={team} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Pagination page={page} totalPages={teamsQuery.data?.totalPages ?? 1} total={teamsQuery.data?.totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
        </>
      )}
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("resources.teams.createTitle")} description={t("resources.teams.formDescription")}>
        <SideSheetBody className="space-y-3">
          <div className="space-y-1.5"><Label>{t("resources.teams.name")} *</Label><Input value={name} onChange={(e) => setName(e.target.value)} /></div>
          <div className="space-y-1.5"><Label>{t("resources.teams.description")}</Label><Input value={description} onChange={(e) => setDescription(e.target.value)} /></div>
        </SideSheetBody>
        <SideSheetFooter>
          <Button
            disabled={create.isPending || !name.trim()}
            onClick={() => create.mutate({ name: name.trim(), description: description || undefined }, { onSuccess: () => { setCreateOpen(false); setName(""); setDescription(""); } })}
          >
            {create.isPending ? t("resources.teams.saving") : t("resources.teams.create")}
          </Button>
        </SideSheetFooter>
      </SideSheet>
    </div>
  );
}
