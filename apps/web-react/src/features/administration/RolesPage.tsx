import * as React from "react";
import { ArrowLeft, Plus, ShieldCheck, Trash2 } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import {
  useAssignablePermissions,
  useDeleteManagedRole,
  useManagedRole,
  useManagedRoles,
  useSaveManagedRole,
  type ManagedRoleDto,
  type PermissionCatalogItemDto,
  type SaveRoleInput,
} from "./api/admin";
import { getPermissionDisplayName, getPermissionResourceLabel } from "./permission-labels";

function toSaveInput(role: ManagedRoleDto): SaveRoleInput {
  return {
    name: role.name,
    description: role.description ?? "",
    category: role.category ?? "",
    permissionIds: role.permissions.map((permission) => permission.id),
  };
}

function emptyRole(): SaveRoleInput {
  return { name: "", description: "", category: "", permissionIds: [] };
}

export function RolesPage() {
  const { t } = useI18n();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Users.Write));
  const roles = useManagedRoles(canManage);
  const permissionCatalog = useAssignablePermissions(canManage);
  const saveRole = useSaveManagedRole();
  const deleteRole = useDeleteManagedRole();
  const [selectedRoleId, setSelectedRoleId] = React.useState<string | null>(null);
  const [isCreating, setIsCreating] = React.useState(false);
  const [formOpen, setFormOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveRoleInput | null>(null);
  const [roleSearch, setRoleSearch] = React.useState("");
  const [permissionSearch, setPermissionSearch] = React.useState("");
  const [actionError, setActionError] = React.useState<string | null>(null);
  const roleDetails = useManagedRole(selectedRoleId, canManage && !isCreating);
  const selectedRole = roles.data?.find((role) => role.id === selectedRoleId);

  React.useEffect(() => {
    if (!isCreating && roleDetails.data && roleDetails.data.id === selectedRoleId) {
      setDraft(toSaveInput(roleDetails.data));
    }
  }, [isCreating, roleDetails.data, selectedRoleId]);

  const filteredRoles = React.useMemo(() => {
    const term = roleSearch.trim().toLocaleLowerCase();
    return (roles.data ?? []).filter((role) => !term
      || `${role.name} ${role.category ?? ""} ${role.description ?? ""}`.toLocaleLowerCase().includes(term));
  }, [roleSearch, roles.data]);

  const permissionGroups = React.useMemo(() => {
    const grouped = new Map<string, PermissionCatalogItemDto[]>();
    const term = permissionSearch.trim().toLocaleLowerCase();
    for (const permission of permissionCatalog.data ?? []) {
      const label = `${permission.name} ${getPermissionDisplayName(permission, t)}`.toLocaleLowerCase();
      if (term && !label.includes(term)) continue;
      const group = grouped.get(permission.resource) ?? [];
      group.push(permission);
      grouped.set(permission.resource, group);
    }
    return [...grouped.entries()];
  }, [permissionCatalog.data, permissionSearch, t]);

  function beginCreate() {
    setSelectedRoleId(null);
    setIsCreating(true);
    setFormOpen(true);
    setDraft(emptyRole());
    setPermissionSearch("");
    setActionError(null);
  }

  function selectRole(roleId: string) {
    setSelectedRoleId(roleId);
    setIsCreating(false);
    setFormOpen(false);
    setDraft(null);
    setPermissionSearch("");
    setActionError(null);
  }

  function cancelEditing() {
    setActionError(null);
    setFormOpen(false);
    if (isCreating) {
      setIsCreating(false);
      setDraft(null);
      return;
    }
    if (roleDetails.data) setDraft(toSaveInput(roleDetails.data));
  }

  function beginEdit() {
    if (!roleDetails.data || isSaving) return;
    setDraft(toSaveInput(roleDetails.data));
    setIsCreating(false);
    setPermissionSearch("");
    setActionError(null);
    setFormOpen(true);
  }

  function togglePermission(permissionId: string, checked: boolean) {
    setDraft((current) => {
      if (!current) return current;
      const selected = new Set(current.permissionIds);
      if (checked) selected.add(permissionId);
      else selected.delete(permissionId);
      return { ...current, permissionIds: [...selected] };
    });
  }

  async function handleSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!draft || !draft.name.trim() || saveRole.isPending) return;
    setActionError(null);
    try {
      const saved = await saveRole.mutateAsync({
        roleId: isCreating ? null : selectedRoleId,
        input: {
          ...draft,
          name: draft.name.trim(),
          description: draft.description.trim(),
          category: draft.category.trim(),
          permissionIds: [...new Set(draft.permissionIds)],
        },
      });
      setSelectedRoleId(saved.id);
      setIsCreating(false);
      setFormOpen(false);
      setDraft(toSaveInput(saved));
    } catch (error) {
      setActionError(error instanceof Error ? error.message : t("administration.areas.roles.saveError"));
    }
  }

  async function handleDelete() {
    if (!selectedRole || selectedRole.isSystem || selectedRole.assignedUserCount > 0 || deleteRole.isPending) return;
    if (!await confirm({ description: t("administration.areas.roles.deleteConfirm", { name: selectedRole.name }), destructive: true })) return;
    setActionError(null);
    try {
      await deleteRole.mutateAsync(selectedRole.id);
      setSelectedRoleId(null);
      setDraft(null);
    } catch (error) {
      setActionError(error instanceof Error ? error.message : t("administration.areas.roles.deleteError"));
    }
  }

  const currentRole = isCreating ? null : roleDetails.data;
  const permissionIds = new Set(draft?.permissionIds ?? []);
  const isSaving = saveRole.isPending || deleteRole.isPending;
  const financeReadLabel = `${t("administration.permissionLabels.resources.finances")} · ${t("administration.permissionLabels.actions.read")}`;
  const canDelegateFinancialRead = (permissionCatalog.data ?? []).some((permission) => permission.name === Permissions.Finances.Read);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.areas.roles.title")}
        description={t("administration.areas.roles.description")}
        actions={<div className="flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" variant="outline" onClick={() => navigate("/administration/access")}><ArrowLeft className="me-2 h-4 w-4 rtl:-scale-x-100" />{t("administration.areas.access.backToAccessSecurity")}</Button>
          <Button type="button" onClick={beginCreate} disabled={!canManage || isSaving}><Plus className="me-2 h-4 w-4" />{t("administration.areas.roles.newRole")}</Button>
        </div>}
      />
      <div className="grid gap-4 xl:grid-cols-[minmax(17rem,0.75fr)_minmax(0,1.8fr)]">
        <Card className="h-fit">
          <CardHeader>
            <CardTitle>{t("administration.areas.roles.roleList")}</CardTitle>
            <CardDescription>{t("administration.areas.roles.roleListDescription")}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <Input value={roleSearch} onChange={(event) => setRoleSearch(event.target.value)} placeholder={t("administration.areas.roles.searchRoles")} aria-label={t("administration.areas.roles.searchRoles")} />
            {roles.isLoading ? <p className="py-5 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
              : roles.isError ? <div className="space-y-2"><p role="alert" className="text-sm text-destructive">{t("administration.areas.roles.loadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void roles.refetch()}>{t("common.retry")}</Button></div>
                : filteredRoles.length === 0 ? <p className="py-5 text-center text-sm text-muted-foreground">{t("administration.areas.roles.emptyRoles")}</p>
                  : <ul className="space-y-2" aria-label={t("administration.areas.roles.roleList")}>
                    {filteredRoles.map((role) => <li key={role.id}>
                      <button type="button" onClick={() => selectRole(role.id)} aria-current={selectedRoleId === role.id ? "true" : undefined}
                        className={`w-full rounded-lg border p-3 text-start transition-colors hover:border-primary/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${selectedRoleId === role.id && !isCreating ? "border-primary bg-accent/50" : "bg-card"}`}>
                        <span className="flex items-start justify-between gap-2"><span className="min-w-0"><span className="block break-words text-sm font-semibold">{role.name}</span><span className="mt-1 block text-xs text-muted-foreground">{role.category || t("administration.areas.roles.uncategorized")}</span></span>{role.isSystem ? <Badge variant="outline" className="shrink-0 bg-transparent text-info">{t("administration.areas.roles.systemRole")}</Badge> : null}</span>
                        <span className="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground"><span>{t("administration.areas.roles.permissionCount", { count: role.permissionCount })}</span><span>{t("administration.areas.roles.assignedUserCount", { count: role.assignedUserCount })}</span></span>
                      </button>
                    </li>)}
                  </ul>}
          </CardContent>
        </Card>

        {!selectedRoleId ? <Card className="flex min-h-72 items-center justify-center"><CardContent className="py-10 text-center"><ShieldCheck className="mx-auto h-10 w-10 text-muted-foreground" /><p className="mt-3 text-sm font-medium">{t("administration.areas.roles.selectRole")}</p><p className="mt-1 text-sm text-muted-foreground">{t("administration.areas.roles.selectRoleHint")}</p></CardContent></Card>
          : <Card>
            <CardHeader>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div><CardTitle>{currentRole?.name ?? selectedRole?.name ?? t("administration.areas.roles.editRole")}</CardTitle><CardDescription className="mt-1">{currentRole?.description || t("administration.areas.roles.editRoleDescription")}</CardDescription></div>
                <div className="flex items-center gap-2">{currentRole?.isSystem ? <Badge variant="outline" className="bg-transparent text-info">{t("administration.areas.roles.systemRole")}</Badge> : null}<Button type="button" size="sm" variant="outline" onClick={beginEdit} disabled={!currentRole || isSaving}>{t("common.edit")}</Button></div>
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              {roleDetails.isLoading || (roleDetails.isFetching && !roleDetails.data) ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                : roleDetails.isError ? <div className="flex flex-col items-center gap-3 py-8"><p role="alert" className="text-sm text-destructive">{t("administration.areas.roles.detailLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void roleDetails.refetch()}>{t("common.retry")}</Button></div>
                  : currentRole ? <>
                    <div className="grid gap-3 sm:grid-cols-2"><div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">{t("administration.areas.roles.category")}</p><p className="mt-1 text-sm font-medium">{currentRole.category || t("administration.areas.roles.uncategorized")}</p></div><div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">{t("administration.areas.roles.assignedUserCount", { count: currentRole.assignedUserCount })}</p><p className="mt-1 text-sm font-medium">{t("administration.areas.roles.permissionCount", { count: currentRole.permissionCount })}</p></div></div>
                    {currentRole.isSystem ? <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.systemRoleProtected")}</p> : null}
                    <section className="space-y-3" aria-labelledby="role-permissions-heading"><h3 id="role-permissions-heading" className="text-sm font-semibold">{t("administration.areas.roles.permissions")}</h3>
                      {currentRole.permissions.length === 0 ? <p className="rounded-md border p-4 text-sm text-muted-foreground">{t("administration.areas.roles.noRolePermissions")}</p>
                        : <div className="grid gap-3 md:grid-cols-2">{permissionGroupsForRole(currentRole.permissions).map(([resource, group]) => <div key={resource} className="space-y-2 rounded-lg border p-3"><h4 className="text-sm font-semibold">{getPermissionResourceLabel(group[0], t)}</h4><div className="flex flex-wrap gap-2">{group.map((permission) => <Badge key={permission.id} variant="secondary">{getPermissionDisplayName(permission, t)}</Badge>)}</div></div>)}</div>}
                    </section>
                    {!currentRole.isSystem ? <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">{currentRole.assignedUserCount > 0 ? <span className="text-xs text-muted-foreground">{t("administration.areas.roles.deleteBlocked")}</span> : <span /> }<Button type="button" size="sm" variant="outline" className="border-destructive/40 text-destructive hover:border-destructive hover:text-destructive" onClick={() => void handleDelete()} disabled={isSaving || currentRole.assignedUserCount > 0} aria-label={t("administration.areas.roles.deleteRole")}><Trash2 className="me-1.5 h-4 w-4" />{t("administration.areas.roles.deleteRole")}</Button></div> : null}
                  </>
                  : null}
            </CardContent>
          </Card>}
      </div>
      <SideSheet open={formOpen} onOpenChange={(open) => { if (!open && !isSaving) cancelEditing(); }} title={isCreating ? t("administration.areas.roles.newRole") : t("administration.areas.roles.editRole")} description={isCreating ? t("administration.areas.roles.newRoleDescription") : t("administration.areas.roles.editRoleDescription")} size="xl" closeLabel={t("common.close")}>
        {draft ? <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void handleSave(event)}>
          <SideSheetBody className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2"><Label htmlFor="managed-role-name">{t("administration.areas.roles.name")}</Label><Input id="managed-role-name" value={draft.name} onChange={(event) => setDraft((current) => current ? { ...current, name: event.target.value } : current)} maxLength={100} required disabled={Boolean(currentRole?.isSystem || isSaving)} /></div>
              <div className="space-y-2"><Label htmlFor="managed-role-category">{t("administration.areas.roles.category")}</Label><Input id="managed-role-category" value={draft.category} onChange={(event) => setDraft((current) => current ? { ...current, category: event.target.value } : current)} maxLength={100} disabled={isSaving} /></div>
              <div className="space-y-2 md:col-span-2"><Label htmlFor="managed-role-description">{t("administration.areas.roles.descriptionField")}</Label><textarea id="managed-role-description" value={draft.description} onChange={(event) => setDraft((current) => current ? { ...current, description: event.target.value } : current)} maxLength={500} disabled={isSaving} rows={2} className="flex min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50" /></div>
            </div>
            {currentRole?.isSystem ? <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.systemRoleProtected")}</p> : null}
            <section className="space-y-3 border-t pt-4" aria-labelledby="role-editor-permissions-heading">
              <div className="flex flex-wrap items-center justify-between gap-3"><div><h3 id="role-editor-permissions-heading" className="text-sm font-semibold">{t("administration.areas.roles.permissions")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("administration.areas.roles.permissionSummary", { selected: permissionIds.size, total: permissionCatalog.data?.length ?? 0 })}</p></div><div className="w-full sm:max-w-xs"><Input value={permissionSearch} onChange={(event) => setPermissionSearch(event.target.value)} placeholder={t("administration.areas.roles.searchPermissions")} aria-label={t("administration.areas.roles.searchPermissions")} /></div></div>
              {permissionCatalog.data && !canDelegateFinancialRead ? <p role="note" className="text-sm text-muted-foreground">{t("administration.areas.roles.financialPermissionMissing", { permission: financeReadLabel })}</p> : null}
              {permissionCatalog.isLoading ? <p className="py-5 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                : permissionCatalog.isError ? <div className="flex flex-col items-center gap-3 py-5"><p role="alert" className="text-sm text-destructive">{t("administration.areas.roles.permissionCatalogError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void permissionCatalog.refetch()}>{t("common.retry")}</Button></div>
                  : permissionGroups.length === 0 ? <p className="py-5 text-center text-sm text-muted-foreground">{t("administration.areas.roles.noMatchingPermissions")}</p>
                    : <div className="grid gap-3 md:grid-cols-2">{permissionGroups.map(([resource, group]) => <fieldset key={resource} className="min-w-0 space-y-2 rounded-lg border p-3">
                      <legend className="px-1 text-sm font-semibold">{getPermissionResourceLabel(group[0], t)}</legend>
                      <div className="max-h-64 space-y-1 overflow-y-auto">
                        {group.map((permission) => <label key={permission.id} htmlFor={`role-permission-${permission.id}`} className="flex min-h-10 cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/60 focus-within:ring-2 focus-within:ring-ring">
                          <input id={`role-permission-${permission.id}`} type="checkbox" checked={permissionIds.has(permission.id)} disabled={isSaving} onChange={(event) => togglePermission(permission.id, event.target.checked)} className="h-4 w-4 shrink-0 accent-primary" />
                          <span className="min-w-0">{getPermissionDisplayName(permission, t)}</span>
                        </label>)}
                      </div>
                    </fieldset>)}</div>}
            </section>
            {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={cancelEditing} disabled={isSaving}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isSaving || permissionCatalog.isLoading || permissionCatalog.isError || (!isCreating && (roleDetails.isLoading || roleDetails.isError))}>{saveRole.isPending ? t("common.saving") : t("administration.areas.roles.saveRole")}</Button>
          </SideSheetFooter>
        </form> : null}
      </SideSheet>
    </div>
  );
}

function permissionGroupsForRole(permissions: PermissionCatalogItemDto[]) {
  const grouped = new Map<string, PermissionCatalogItemDto[]>();
  for (const permission of permissions) {
    const group = grouped.get(permission.resource) ?? [];
    group.push(permission);
    grouped.set(permission.resource, group);
  }
  return [...grouped.entries()].map(([resource, items]) => [resource, items] as const);
}
