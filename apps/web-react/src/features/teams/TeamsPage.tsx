import * as React from "react";
import { Search, Plus, MoreHorizontal, History, UserMinus, Users } from "lucide-react";
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
import { getResourceTypeTextClass, getStatusTextClass } from "@lib/status-presentation";
import { useTeams, useCreateTeam, useUpdateTeam, useDeactivateTeam, useReactivateTeam, useDeleteTeam, useTeamMembers, useSetTeamMembers, useRemoveTeamMember, useDepartments, type TeamDto } from "./api/teams";
import { useResources } from "@features/resources/api/resources";

const PAGE_SIZE = 20;

export function TeamMembersPanel({ teamId, open, canEdit }: { teamId: string; open: boolean; canEdit: boolean }) {
  const { t, formatDate } = useI18n();
  const membersQuery = useTeamMembers(open ? teamId : null, false);
  const allResources = useResources({ page: 1, pageSize: 200, isActive: true }, open && canEdit);
  const setMembers = useSetTeamMembers();
  const removeMember = useRemoveTeamMember();
  const [selected, setSelected] = React.useState<string[]>([]);
  const [showHistory, setShowHistory] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (membersQuery.data) setSelected(membersQuery.data.filter((member) => member.isActive && !member.effectiveTo).map((member) => member.resourceId));
  }, [membersQuery.data]);

  const members = React.useMemo(() => membersQuery.data ?? [], [membersQuery.data]);
  const currentMembers = React.useMemo(() => members.filter((member) => member.isActive && !member.effectiveTo), [members]);
  const historicalMembers = React.useMemo(() => members.filter((member) => !member.isActive || Boolean(member.effectiveTo)), [members]);
  const currentIds = React.useMemo(() => new Set(currentMembers.map((member) => member.resourceId)), [currentMembers]);
  const resources = React.useMemo(() => allResources.data?.items ?? [], [allResources.data?.items]);
  const addableResources = React.useMemo(() => resources.filter((resource) => resource.isActive && !currentIds.has(resource.id)), [resources, currentIds]);
  const additions = selected.filter((resourceId) => !currentIds.has(resourceId));
  const pending = setMembers.isPending || removeMember.isPending;
  const formattedDate = (value?: string) => {
    if (!value || Number.isNaN(new Date(value).getTime())) return "—";
    return formatDate(value, { numberingSystem: "latn" });
  };

  async function saveMembers() {
    setError(null);
    try {
      await setMembers.mutateAsync({ teamId, resourceIds: selected });
    } catch (reason) {
      setError(reason instanceof Error && reason.message ? reason.message : t("resources.teams.membersSaveError"));
    }
  }

  async function remove(resourceId: string) {
    setError(null);
    try {
      await removeMember.mutateAsync({ teamId, resourceId });
    } catch (reason) {
      setError(reason instanceof Error && reason.message ? reason.message : t("resources.teams.membersSaveError"));
    }
  }

  if (membersQuery.isLoading) return <p className="text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (membersQuery.isError) return <div role="alert" className="space-y-2 text-sm text-destructive"><p>{membersQuery.error instanceof Error ? membersQuery.error.message : t("resources.teams.loadError")}</p><Button type="button" variant="outline" size="sm" onClick={() => void membersQuery.refetch()}>{t("common.retry")}</Button></div>;

  return (
    <div className="space-y-6">
      <section aria-labelledby="team-members-heading" className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2"><h3 id="team-members-heading" className="text-sm font-semibold">{t("resources.teams.members")}</h3><span className="text-xs text-muted-foreground">{t("resources.teams.memberCountLabel", { count: currentMembers.length })}</span></div>
        {currentMembers.length === 0 ? <p className="rounded-md border border-dashed border-border bg-muted/20 px-3 py-4 text-sm text-muted-foreground">{t("resources.teams.noMembers")}</p> : (
          <ul className="space-y-2">{currentMembers.map((member) => <li key={member.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border bg-background px-3 py-2"><div className="min-w-0"><p className="truncate text-sm font-medium">{member.resourceName ?? member.resourceId}</p><p className="text-xs text-muted-foreground">{t("resources.teams.memberSince", { date: formattedDate(member.effectiveFrom) })}</p></div>{canEdit ? <Button type="button" variant="ghost" size="sm" className="text-destructive focus:text-destructive" disabled={pending} onClick={() => void remove(member.resourceId)}><UserMinus className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />{t("resources.teams.removeMember")}</Button> : null}</li>)}</ul>
        )}
      </section>

      {canEdit ? <section aria-labelledby="team-add-members-heading" className="space-y-3">
        <h3 id="team-add-members-heading" className="text-sm font-semibold">{t("resources.teams.addMembers")}</h3>
        {allResources.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : allResources.isError ? <div role="alert" className="space-y-2 text-sm text-destructive"><p>{t("resources.teams.resourcesLoadError")}</p><Button type="button" variant="outline" size="sm" onClick={() => void allResources.refetch()}>{t("common.retry")}</Button></div> : addableResources.length === 0 ? <p className="text-sm text-muted-foreground">{t("resources.teams.noResourcesToAdd")}</p> : (
          <div className="max-h-64 space-y-1 overflow-y-auto rounded-md border border-border p-2">{addableResources.map((resource) => <label key={resource.id} className={`flex cursor-pointer items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-muted/50 ${selected.includes(resource.id) ? "" : "opacity-70"}`}><input type="checkbox" className="h-4 w-4 rounded border-input" checked={selected.includes(resource.id)} onChange={(event) => setSelected((current) => event.target.checked ? [...current, resource.id] : current.filter((id) => id !== resource.id))} /><span className="truncate">{resource.name}</span>{resource.type ? <span className={`ms-auto shrink-0 text-xs font-medium ${getResourceTypeTextClass(resource.type)}`}>{resource.type}</span> : null}</label>)}</div>
        )}
        {additions.length > 0 ? <p className="text-xs text-muted-foreground">{t("resources.teams.pendingChanges", { added: additions.length, removed: 0 })}</p> : null}
        <Button type="button" size="sm" disabled={additions.length === 0 || pending || allResources.isError} onClick={() => void saveMembers()}><Users className="me-1.5 h-4 w-4" aria-hidden="true" />{setMembers.isPending ? t("resources.teams.savingMembers") : t("resources.teams.saveMembers")}</Button>
      </section> : null}

      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}

      <section aria-labelledby="team-member-history-heading" className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2"><h3 id="team-member-history-heading" className="text-sm font-semibold">{t("resources.teams.memberHistory")}</h3><Button type="button" variant="ghost" size="sm" onClick={() => setShowHistory((value) => !value)}><History className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />{showHistory ? t("resources.teams.hideHistory") : t("resources.teams.showHistory")}</Button></div>
        {showHistory ? historicalMembers.length === 0 ? <p className="text-sm text-muted-foreground">{t("resources.teams.noHistory")}</p> : <ul className="space-y-1.5">{historicalMembers.map((member) => <li key={member.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border/60 bg-muted/20 px-3 py-2 text-sm"><span className="truncate text-muted-foreground">{member.resourceName ?? member.resourceId}</span><span className="text-xs text-muted-foreground">{t("resources.teams.memberPeriod", { from: formattedDate(member.effectiveFrom), to: member.effectiveTo ? formattedDate(member.effectiveTo) : t("resources.teams.openEnded") })}</span></li>)}</ul> : null}
      </section>
    </div>
  );
}

export function TeamRowActions({ team }: { team: TeamDto }) {
  const { t } = useI18n();
  const { user } = useAuth();
  const canEdit = Boolean(user && (user.permissions.includes(Permissions.Team.Edit) || user.permissions.includes(Permissions.Team.Edit)));
  const canDelete = Boolean(user && (user.permissions.includes(Permissions.Team.Delete) || user.permissions.includes(Permissions.Team.Edit)));
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
      <SideSheet open={membersOpen} onOpenChange={setMembersOpen} title={team.name} description={team.code ?? undefined} size="lg" closeLabel={t("resources.teams.close")}>
        <SideSheetBody className="space-y-6">
          <section aria-labelledby="team-detail-heading" className="space-y-3">
            <h3 id="team-detail-heading" className="text-sm font-semibold text-foreground">{t("resources.teams.details")}</h3>
            <dl className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-0.5"><dt className="text-xs text-muted-foreground">{t("resources.teams.code")}</dt><dd className="font-mono text-sm font-medium">{team.code ?? "—"}</dd></div>
              <div className="space-y-0.5"><dt className="text-xs text-muted-foreground">{t("resources.teams.status")}</dt><dd className={`text-sm font-medium ${getStatusTextClass(team.isActive ? "Active" : "Inactive")}`}>{team.isActive ? t("resources.teams.active") : t("resources.teams.inactive")}</dd></div>
              <div className="space-y-0.5"><dt className="text-xs text-muted-foreground">{t("resources.teams.department")}</dt><dd className="text-sm">{team.departmentName ?? t("resources.teams.noDepartment")}</dd></div>
              <div className="space-y-0.5"><dt className="text-xs text-muted-foreground">{t("resources.teams.teamLead")}</dt><dd className="text-sm">{team.teamLeadName ?? t("resources.teams.noTeamLead")}</dd></div>
              <div className="space-y-0.5"><dt className="text-xs text-muted-foreground">{t("resources.teams.emailAlias")}</dt><dd className="text-sm">{team.emailAlias ?? "—"}</dd></div>
              <div className="space-y-0.5 sm:col-span-2"><dt className="text-xs text-muted-foreground">{t("resources.teams.description")}</dt><dd className="text-sm">{team.description ?? "—"}</dd></div>
            </dl>
          </section>
          <TeamMembersPanel teamId={team.id} open={membersOpen} canEdit={canEdit} />
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setMembersOpen(false)}>{t("resources.teams.close")}</Button></SideSheetFooter>
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
  const canCreate = Boolean(user && (user.permissions.includes(Permissions.Team.Create) || user.permissions.includes(Permissions.Team.Edit)));
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
  const hasFilters = Boolean(search || departmentId || filterStatus !== "all");
  const clearFilters = () => { setSearch(""); setDepartmentId(""); setFilterStatus("all"); };
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
        {hasFilters ? <Button type="button" variant="ghost" size="sm" onClick={clearFilters}>{t("resources.teams.clearFilters")}</Button> : null}
      </div>
      {teamsQuery.isLoading ? (
        <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : teamsQuery.isError ? (
        <div role="alert" className="space-y-2 py-12 text-center text-sm text-destructive">
          <p>{t("resources.teams.loadError")}</p>
          {teamsQuery.error instanceof Error ? <p>{teamsQuery.error.message}</p> : null}
          <Button type="button" variant="outline" size="sm" onClick={() => void teamsQuery.refetch()}>{t("common.retry")}</Button>
        </div>
      ) : teams.length === 0 ? (
        <div className="space-y-3 py-12 text-center"><p className="text-sm font-medium">{t("resources.teams.empty")}</p><p className="text-sm text-muted-foreground">{t("resources.teams.emptyDescription")}</p>{canCreate ? <Button type="button" size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("resources.teams.addTeam")}</Button> : null}</div>
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
                  <TableCell><span className={`font-medium ${getStatusTextClass(team.isActive ? "Active" : "Inactive")}`}>{team.isActive ? t("resources.teams.active") : t("resources.teams.inactive")}</span></TableCell>
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
