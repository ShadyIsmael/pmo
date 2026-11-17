import * as React from "react";
import { ArrowLeft, ArrowUpRight, Copy, KeyRound, Pencil, Plus, ShieldCheck, UsersRound, Eye, EyeOff } from "lucide-react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { ApiError, getApiFieldError } from "@api/client";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { useManagement } from "@api/management";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { getStatusTextClass } from "@lib/status-presentation";
import { formatDateTime } from "@lib/utils";
import { useAdminUsers, useAssignableRoles, useCreateAdminUser, useResetAdminUserPassword, useSaveUserPermissions, useUpdateAdminUser, useUserPermissions, toUserPermissionOverrides, type AdminUserDto, type AssignedRoleDto, type AssignableRoleDto, type PermissionOverrideEffect, type UserPermissionAssignmentDto } from "./api/admin";
import { getPermissionDisplayName, getPermissionResourceLabel, getPermissionScopeValueLabel } from "./permission-labels";

function isPermissionOverrideEffect(value: string): value is PermissionOverrideEffect {
  return value === "Inherit" || value === "Allow" || value === "Deny";
}

function isProtectedTenantRole(role: Pick<AssignedRoleDto, "scope" | "isOrganizationOwned" | "isProtected">): boolean {
  return !role.isOrganizationOwned && role.scope.toLocaleLowerCase() === "tenant" && role.isProtected;
}

function isUnresolvedRoleOwnership(role: Pick<AssignableRoleDto | AssignedRoleDto, "scope" | "isLegacyOrganizationOwnershipUnresolved" | "isOrganizationOwnershipUnresolved">): boolean {
  return role.isOrganizationOwnershipUnresolved === true
    || role.isLegacyOrganizationOwnershipUnresolved === true
    || role.scope.toLocaleLowerCase() === "unresolved";
}

function roleOrganizationName(role: Pick<AssignableRoleDto | AssignedRoleDto, "organizationName" | "isOrganizationOwned" | "scope" | "isLegacyOrganizationOwnershipUnresolved" | "isOrganizationOwnershipUnresolved">, tenantLabel: string, unavailableLabel: string, unresolvedLabel: string): string {
  if (isUnresolvedRoleOwnership(role)) return unresolvedLabel;
  if (!role.isOrganizationOwned) return tenantLabel;
  return role.organizationName?.trim() || unavailableLabel;
}

export function AccessPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const hasUsersRead = hasAnyPermission(permissions, [Permissions.Users.View, Permissions.Users.ManageAccounts]);
  const hasRoleAdministration = hasAnyPermission(permissions, [Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete]);
  const components = [
    ...(hasUsersRead ? [{ id: "users", href: "/administration/access/users", icon: UsersRound, title: t("administration.areas.access.users.title"), description: t("administration.areas.access.users.description") }] : []),
    ...(hasRoleAdministration ? [{ id: "roles", href: "/administration/access/roles", icon: ShieldCheck, title: t("administration.areas.access.roles.title"), description: t("administration.areas.access.roles.description") }] : []),
  ];

  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.areas.access.title")} description={t("administration.areas.access.description")} />
      {components.length === 0
        ? <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("administration.areas.access.noComponents")}</CardContent></Card>
        : <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {components.map(({ id, href, icon: Icon, title, description }) => (
            <Link key={id} to={href} className="group">
              <Card className="h-full transition-all group-hover:-translate-y-0.5 group-hover:border-primary/30">
                <CardHeader className="gap-4">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-5 w-5" /></div>
                  <div><CardTitle className="text-base">{title}</CardTitle><CardDescription className="mt-1.5 text-xs leading-5">{description}</CardDescription></div>
                </CardHeader>
                <CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("administration.home.open")}</span><ArrowUpRight className="h-4 w-4" /></div></CardContent>
              </Card>
            </Link>
          ))}
        </div>}
    </div>
  );
}

export function AccessUsersPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [search, setSearch] = React.useState("");
  const [page, setPage] = React.useState(1);
  const { user } = useAuth();
  const { effectiveReadOnly } = useManagement();
  const canManageUsers = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Users.ManageAccounts]) && !effectiveReadOnly);
  const canManageUserStatus = Boolean(user && hasPermission(user.permissions, Permissions.Users.SetStatus) && !effectiveReadOnly);
  const canAssignRoles = Boolean(user && hasPermission(user.permissions, Permissions.Users.AssignRoles) && !effectiveReadOnly);
  const canEditUser = canManageUsers || canAssignRoles || canManageUserStatus;
  const canManageOverrides = Boolean(user && hasPermission(user.permissions, Permissions.Users.ManageOverrides) && !effectiveReadOnly);
  const canResetPasswords = Boolean(user && hasPermission(user.permissions, Permissions.Users.ResetPassword) && !effectiveReadOnly);
  const users = useAdminUsers(true, { page, pageSize: 25, search: search.trim() });
  const roles = useAssignableRoles(canAssignRoles);
  const createUser = useCreateAdminUser();
  const updateUser = useUpdateAdminUser();
  const resetPassword = useResetAdminUserPassword();
  const [editingUser, setEditingUser] = React.useState<AdminUserDto | null>(null);
  const [isCreatingUser, setIsCreatingUser] = React.useState(false);
  const [draft, setDraft] = React.useState<{ firstName: string; lastName: string; email: string; authenticationSource: AdminUserDto["authenticationSource"]; isActive: boolean; roleIds: string[] }>({ firstName: "", lastName: "", email: "", authenticationSource: "Local", isActive: true, roleIds: [] });
  const [roleSearch, setRoleSearch] = React.useState("");
  const [formError, setFormError] = React.useState<string | null>(null);
  const [apiError, setApiError] = React.useState<unknown>(null);
  const [credential, setCredential] = React.useState<{ userName: string; password: string; expiresAt?: string } | null>(null);
  const [credentialError, setCredentialError] = React.useState<string | null>(null);
  const isSavingUser = createUser.isPending || updateUser.isPending;
  const isSelfEdit = Boolean(editingUser && editingUser.id === user?.id);
  const items = users.data?.items ?? [];
  const roleTerm = roleSearch.trim().toLocaleLowerCase();

  const assignableRoles = (roles.data ?? []).filter((role) => role.isAssignable);
  const assignableRoleIds = new Set(assignableRoles.map((role) => role.id));
  const assignedRoleById = new Map((editingUser?.roles ?? []).map((role) => [role.id, role]));
  const availableRoleById = new Map((roles.data ?? []).map((role) => [role.id, role]));
  const selectedRoleRecords = [...new Set(draft.roleIds)].map((id) => assignedRoleById.get(id) ?? availableRoleById.get(id)).filter((role): role is AssignedRoleDto | AssignableRoleDto => Boolean(role));
  const organizationIdsByName = new Map<string, Set<string>>();
  for (const role of [...(roles.data ?? []), ...items.flatMap((item) => item.roles), ...(editingUser?.roles ?? [])]) {
    const normalizedName = role.organizationName?.trim().toLocaleLowerCase();
    if (!role.isOrganizationOwned || !role.organizationId || !normalizedName) continue;
    const organizationIds = organizationIdsByName.get(normalizedName) ?? new Set<string>();
    organizationIds.add(role.organizationId);
    organizationIdsByName.set(normalizedName, organizationIds);
  }
  function getRoleOrganizationLabel(role: AssignableRoleDto | AssignedRoleDto): string {
    const label = roleOrganizationName(role, t("administration.users.tenantRolesTitle"), t("administration.users.roleOrganizationUnavailable"), t("administration.users.unresolvedRoleOwnershipTitle"));
    const normalizedName = role.organizationName?.trim().toLocaleLowerCase();
    const sameNameOrganizations = normalizedName ? organizationIdsByName.get(normalizedName) : undefined;
    return role.isOrganizationOwned && role.organizationId && (!normalizedName || Boolean(sameNameOrganizations && sameNameOrganizations.size > 1))
      ? `${label} (${role.organizationId.slice(0, 8)})`
      : label;
  }
  function getRoleDisplayName(role: AssignableRoleDto | AssignedRoleDto): string {
    return `${getRoleOrganizationLabel(role)} \u2014 ${role.name}`;
  }
  const derivedOrganizations = [...new Map(selectedRoleRecords
    .filter((role) => role.isOrganizationOwned && role.organizationId)
    .map((role) => [role.organizationId!, role.organizationName?.trim() || t("administration.users.roleOrganizationUnavailable")])).entries()]
    .map(([id, name]) => {
      const sameNameOrganizations = organizationIdsByName.get(name.toLocaleLowerCase());
      const lacksOrganizationName = selectedRoleRecords.some((role) => role.organizationId === id && !role.organizationName?.trim());
      return {
        id,
        name,
        displayName: lacksOrganizationName || (sameNameOrganizations && sameNameOrganizations.size > 1)
          ? `${name} (${id.slice(0, 8)})`
          : name,
      };
    })
    .sort((left, right) => left.name.localeCompare(right.name));
  const selectedOrganizationRolesMissingDetails = selectedRoleRecords.some((role) => role.isOrganizationOwned && (!role.organizationId || !role.organizationName?.trim()));
  const protectedTenantRoles = (editingUser?.roles ?? []).filter(isProtectedTenantRole);
  const unavailableAssignedRoles = (editingUser?.roles ?? []).filter((role) => !assignableRoleIds.has(role.id) && !isProtectedTenantRole(role));
  const filteredUnavailableAssignedRoles = unavailableAssignedRoles.filter((role) => {
    const searchText = `${role.name} ${role.organizationName ?? ""}`.toLocaleLowerCase();
    return !roleTerm || searchText.includes(roleTerm);
  });
  const filteredRoles = assignableRoles.filter((role) => {
    const searchText = `${role.name} ${role.organizationName ?? ""} ${role.description ?? ""} ${role.scope}`.toLocaleLowerCase();
    return !roleTerm || searchText.includes(roleTerm);
  });
  const groupedRoles = [...filteredRoles.reduce((groups, role) => {
    const groupId = role.isOrganizationOwned && role.organizationId
      ? `organization:${role.organizationId}`
      : `scope:${(role.scope ?? "Tenant").toLocaleLowerCase()}`;
    const label = getRoleOrganizationLabel(role);
    const group = groups.get(groupId) ?? { id: groupId, label, roles: [] as AssignableRoleDto[] };
    group.roles.push(role);
    groups.set(groupId, group);
    return groups;
  }, new Map<string, { id: string; label: string; roles: AssignableRoleDto[] }>()).values()]
    .sort((left, right) => left.label.localeCompare(right.label));

  function beginCreateUser() {
    setEditingUser(null);
    setIsCreatingUser(true);
    setDraft({ firstName: "", lastName: "", email: "", authenticationSource: "Local", isActive: true, roleIds: [] });
    setRoleSearch("");
    setFormError(null);
    setApiError(null);
  }

  function beginEditUser(item: AdminUserDto) {
    setEditingUser(item);
    setIsCreatingUser(false);
    setDraft({
      firstName: item.firstName,
      lastName: item.lastName,
      email: item.email,
      authenticationSource: item.authenticationSource ?? "Local",
      isActive: item.isActive,
      roleIds: [...new Set(item.roles.map((role) => role.id))],
    });
    setRoleSearch("");
    setFormError(null);
    setApiError(null);
  }

  function closeUserForm() {
    if (isSavingUser) return;
    setEditingUser(null);
    setIsCreatingUser(false);
    setFormError(null);
    setApiError(null);
  }

  async function handleSaveUser(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if ((!editingUser && !isCreatingUser) || isSavingUser || (canAssignRoles && (roles.isLoading || roles.isError))) return;
    setFormError(null);
    setApiError(null);
    const input = {
      email: draft.email.trim(),
      firstName: draft.firstName.trim(),
      lastName: draft.lastName.trim(),
      isActive: draft.isActive,
      ...(canAssignRoles ? { roleIds: [...new Set(draft.roleIds)] } : {}),
    };
    try {
      if (editingUser) {
        await updateUser.mutateAsync({ userId: editingUser.id, input });
      } else {
        const created = await createUser.mutateAsync({ ...input, authenticationSource: draft.authenticationSource });
        if (created.temporaryPassword) {
          setCredential({ userName: created.user.fullName, password: created.temporaryPassword, expiresAt: created.user.temporaryPasswordExpiresAt ?? undefined });
        }
      }
      setEditingUser(null);
      setIsCreatingUser(false);
    } catch (error) {
      setApiError(error);
      let saveError = error instanceof ApiError ? error.message : t("administration.users.error");
      if (error instanceof ApiError && error.details && typeof error.details === "object") {
        const payload = error.details as Record<string, unknown>;
        const fieldName = typeof payload.fieldName === "string" ? payload.fieldName : "";
        const payloadErrors = payload.errors && typeof payload.errors === "object" ? payload.errors as Record<string, unknown> : null;
        const hasRoleFieldError = fieldName.toLocaleLowerCase() === "roleids"
          || Boolean(payloadErrors && Object.keys(payloadErrors).some((field) => field.toLocaleLowerCase() === "roleids"));
        const nestedDetails = payload.details && typeof payload.details === "object" ? payload.details as Record<string, unknown> : null;
        const organizationNames = Array.isArray(nestedDetails?.organizationNames)
          ? nestedDetails.organizationNames.filter((name): name is string => typeof name === "string" && name.trim().length > 0)
          : [];
        if (hasRoleFieldError) {
          saveError = error.status === 409 && organizationNames.length > 0
            ? t("administration.users.roleMembershipDependencyConflict", { organizations: organizationNames.join(", ") })
            : t("administration.users.roleAssignmentValidationError");
        }
      }
      setFormError(saveError);
    }
  }

  async function handleResetPassword(item: AdminUserDto) {
    setCredentialError(null);
    try {
      const result = await resetPassword.mutateAsync(item.id);
      setCredential({ userName: item.fullName, password: result.temporaryPassword, expiresAt: result.expiresAt });
    } catch (error) {
      setCredentialError(error instanceof ApiError ? error.message : t("administration.users.resetPasswordError"));
    }
  }

  function updateDraft<K extends keyof typeof draft>(key: K, value: (typeof draft)[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  function setRoleSelected(roleId: string, checked: boolean, canSelect: boolean, canRemove = true) {
    if (checked && !canSelect) return;
    if (!checked && !canRemove) return;
    setDraft((current) => ({
      ...current,
      roleIds: checked
        ? [...new Set([...current.roleIds, roleId])]
        : current.roleIds.filter((value) => value !== roleId),
    }));
  }

  const userSheetOpen = Boolean(editingUser || isCreatingUser);
  const userSheetTitle = editingUser
    ? canAssignRoles && !canManageUsers ? t("nav.roles") : canManageUserStatus && !canManageUsers ? t("administration.users.status") : t("administration.users.editUser")
    : t("administration.users.addUser");

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.areas.access.users.title")}
        description={t("administration.areas.access.users.description")}
        actions={<div className="flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" variant="outline" onClick={() => navigate("/administration/access")}><ArrowLeft className="me-2 h-4 w-4 rtl:-scale-x-100" />{t("administration.areas.access.backToAccessSecurity")}</Button>
          {canManageUsers ? <Button type="button" size="default" onClick={beginCreateUser}><Plus className="me-2 h-4 w-4" />{t("administration.users.addUser")}</Button> : null}
        </div>}
      />
      <Card>
        <CardHeader><CardTitle>{t("administration.areas.access.userDirectory")}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <Input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder={t("administration.areas.access.searchUsers")} aria-label={t("administration.areas.access.searchUsers")} />
          {users.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : users.isError ? <p role="alert" className="p-4 text-center text-sm text-destructive">{t("administration.areas.access.usersLoadError")}</p>
              : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
                : <div className="overflow-x-auto"><Table>
                  <TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.email")}</TableHead><TableHead>{t("administration.users.authenticationSource")}</TableHead><TableHead>{t("nav.roles")}</TableHead><TableHead>{t("administration.users.status")}</TableHead>{canEditUser || canResetPasswords || canManageOverrides ? <TableHead>{t("common.actions")}</TableHead> : null}</TableRow></TableHeader>
                  <TableBody>{items.map((item) => <TableRow key={item.id}>
                    <TableCell className="font-medium">{item.fullName}</TableCell>
                    <TableCell className="text-muted-foreground">{item.email}</TableCell>
                    <TableCell>{t(`administration.users.sources.${item.authenticationSource ?? "Local"}`)}</TableCell>
                    <TableCell><div className="flex flex-wrap gap-1">{item.roles.length > 0 ? item.roles.map((role) => <Badge key={role.id} variant="outline">{getRoleDisplayName(role)}</Badge>) : <span className="text-muted-foreground">-</span>}</div></TableCell>
                    <TableCell><span className={`text-sm font-medium ${getStatusTextClass(item.isActive ? "active" : "inactive")}`}>{item.isActive ? t("administration.users.active") : t("administration.users.inactive")}</span></TableCell>
                    {canEditUser || canResetPasswords || canManageOverrides ? <TableCell><div className="flex flex-wrap gap-2">
                      {canEditUser ? <Button type="button" size="sm" variant="outline" onClick={() => beginEditUser(item)} aria-label={`${t("administration.users.edit")} ${item.fullName}`}><Pencil className="me-1.5 h-3.5 w-3.5" />{canAssignRoles && !canManageUsers ? t("nav.roles") : t("administration.users.edit")}</Button> : null}
                      {canResetPasswords && (item.authenticationSource ?? "Local") === "Local" && item.id !== user?.id ? <Button type="button" size="sm" variant="outline" disabled={resetPassword.isPending} onClick={() => void handleResetPassword(item)}><KeyRound className="me-1.5 h-3.5 w-3.5" />{t("administration.users.resetPassword")}</Button> : null}
                      {canManageOverrides ? item.id === user?.id
                        ? <span className="self-center text-xs text-muted-foreground">{t("administration.areas.access.selfChangesBlocked")}</span>
                        : <Button type="button" size="sm" variant="outline" onClick={() => navigate("/administration/access/users/" + item.id + "/permissions", { state: { userName: item.fullName } })}>{t("administration.areas.access.managePermissions")}</Button> : null}
                    </div></TableCell> : null}
                  </TableRow>)}</TableBody>
                </Table></div>}
          <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3">
            <span className="text-xs text-muted-foreground">{t("administration.areas.access.userPage", { page: users.data?.page ?? page, total: Math.max(1, users.data?.totalPages ?? 1), count: users.data?.totalCount ?? 0 })}</span>
            <div className="flex gap-2">
              <Button type="button" size="sm" variant="outline" disabled={page <= 1 || users.isLoading} onClick={() => setPage((current) => Math.max(1, current - 1))}>{t("common.previous")}</Button>
              <Button type="button" size="sm" variant="outline" disabled={page >= (users.data?.totalPages ?? 1) || users.isLoading} onClick={() => setPage((current) => current + 1)}>{t("common.next")}</Button>
            </div>
          </div>
        </CardContent>
      </Card>
      <SideSheet open={userSheetOpen} onOpenChange={(open) => { if (!open) closeUserForm(); }} title={userSheetTitle} description={t("administration.areas.access.userFormInstructions")} size="lg" closeLabel={t("common.close")}>
        {userSheetOpen ? <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void handleSaveUser(event)}>
          <SideSheetBody className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2"><Label htmlFor="user-first-name">{t("administration.users.firstName")}</Label><Input id="user-first-name" autoComplete="given-name" value={draft.firstName} disabled={isSavingUser || !canManageUsers} aria-invalid={Boolean(getApiFieldError(apiError, "firstName"))} aria-describedby={getApiFieldError(apiError, "firstName") ? "user-first-name-error" : undefined} onChange={(event) => updateDraft("firstName", event.target.value)} required maxLength={100} /><ApiFieldError id="user-first-name-error" error={apiError} fieldName="firstName" /></div>
              <div className="space-y-2"><Label htmlFor="user-last-name">{t("administration.users.lastName")}</Label><Input id="user-last-name" autoComplete="family-name" value={draft.lastName} disabled={isSavingUser || !canManageUsers} aria-invalid={Boolean(getApiFieldError(apiError, "lastName"))} aria-describedby={getApiFieldError(apiError, "lastName") ? "user-last-name-error" : undefined} onChange={(event) => updateDraft("lastName", event.target.value)} required maxLength={100} /><ApiFieldError id="user-last-name-error" error={apiError} fieldName="lastName" /></div>
              <div className="space-y-2"><Label htmlFor="user-email">{t("administration.users.email")}</Label><Input id="user-email" type="email" autoComplete="email" value={draft.email} disabled={isSavingUser || !canManageUsers} aria-invalid={Boolean(getApiFieldError(apiError, "email"))} aria-describedby={getApiFieldError(apiError, "email") ? "user-email-error" : undefined} onChange={(event) => updateDraft("email", event.target.value)} required maxLength={256} /><ApiFieldError id="user-email-error" error={apiError} fieldName="email" /></div>
              {isCreatingUser ? <div className="space-y-2"><Label htmlFor="user-authentication-source">{t("administration.users.authenticationSource")}</Label><select id="user-authentication-source" className="input-field" value={draft.authenticationSource} disabled={isSavingUser} onChange={(event) => updateDraft("authenticationSource", event.target.value as AdminUserDto["authenticationSource"])}><option value="Local">{t("administration.users.sources.Local")}</option><option value="EntraId" disabled>{t("administration.users.sources.EntraId")} — {t("administration.users.directoryUnavailable")}</option><option value="ActiveDirectoryLdap" disabled>{t("administration.users.sources.ActiveDirectoryLdap")} — {t("administration.users.directoryUnavailable")}</option></select><p className="text-xs text-muted-foreground">{t("administration.users.directoryProvisioningHint")}</p></div> : null}
            </div>
            <label htmlFor="user-is-active" className="flex cursor-pointer items-start gap-3 rounded-lg border p-3">
              <input id="user-is-active" type="checkbox" checked={draft.isActive} disabled={isSavingUser || isSelfEdit || !canManageUserStatus} onChange={(event) => updateDraft("isActive", event.target.checked)} className="mt-0.5 h-4 w-4 accent-primary" />
              <span className="text-sm font-medium">{t("administration.users.isActive")}{isSelfEdit ? <span className="ms-2 text-xs font-normal text-muted-foreground">{t("administration.areas.access.selfStatusLocked")}</span> : null}</span>
            </label>
            <section className="space-y-3 rounded-lg border bg-muted/20 p-4" aria-labelledby="user-derived-organizations-heading">
              <div className="space-y-1"><h3 id="user-derived-organizations-heading" className="text-sm font-semibold">{t("administration.users.derivedOrganizationsLabel")}</h3><p className="text-xs text-muted-foreground">{t("administration.users.derivedOrganizationsHint")}</p></div>
              {derivedOrganizations.length > 0
                ? <div className="flex flex-wrap gap-2">{derivedOrganizations.map((organization) => <Badge key={organization.id} variant="outline">{organization.displayName}</Badge>)}</div>
                : <p className="text-sm text-muted-foreground">{t("administration.users.noDerivedOrganizations")}</p>}
              {selectedOrganizationRolesMissingDetails ? <p role="alert" className="text-xs text-destructive">{t("administration.users.roleOrganizationUnavailable")}</p> : null}
            </section>
            <section className="space-y-3">
              <div className="space-y-1"><h3 className="text-sm font-semibold">{t("administration.users.rolesLabel")}</h3><p className="text-xs text-muted-foreground">{t("administration.users.roleAssignmentByOrganizationHint")}</p></div>
              {protectedTenantRoles.length > 0 ? <div className="space-y-2 rounded-lg border border-info/30 bg-info/5 p-3">
                <h4 className="text-xs font-semibold text-info">{t("administration.users.protectedTenantRolesTitle")}</h4>
                <div className="flex flex-wrap gap-2">{protectedTenantRoles.map((role) => <Badge key={role.id} variant="outline" className="border-info/40">{role.name} ({t("administration.users.protectedTenantRoleLocked")})</Badge>)}</div>
              </div> : null}
              {canAssignRoles ? <>
              <Input value={roleSearch} onChange={(event) => setRoleSearch(event.target.value)} placeholder={t("administration.users.searchRolesByOrganizationOrName")} aria-label={t("administration.users.searchRolesByOrganizationOrName")} disabled={isSavingUser || isSelfEdit} />
              {roles.isLoading ? <p className="py-5 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                : roles.isError ? <div className="flex flex-col items-center gap-3 py-5"><p role="alert" className="text-sm text-destructive">{t("administration.areas.access.roleLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void roles.refetch()}>{t("common.retry")}</Button></div>
                  : <div className="space-y-4">
                    {groupedRoles.map((group) => <section key={group.id} className="space-y-2">
                      <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{group.label}</h4>
                      <div className="grid gap-2 sm:grid-cols-2">{group.roles.map((role) => <label key={role.id} htmlFor={"user-role-" + role.id} className="flex cursor-pointer items-start gap-3 rounded-lg border p-3 hover:bg-accent/50 focus-within:ring-2 focus-within:ring-ring">
                        <input id={"user-role-" + role.id} type="checkbox" checked={draft.roleIds.includes(role.id)} disabled={isSavingUser || isSelfEdit || (draft.roleIds.includes(role.id) && assignedRoleById.get(role.id)?.canRemove === false)} onChange={(event) => setRoleSelected(role.id, event.target.checked, role.isAssignable, assignedRoleById.get(role.id)?.canRemove ?? true)} className="mt-0.5 h-4 w-4 shrink-0 accent-primary" />
                        <span className="min-w-0"><span className="block text-sm font-medium">{getRoleDisplayName(role)}</span>{role.description ? <span className="mt-1 block text-xs text-muted-foreground">{role.description}</span> : null}{role.isSystem || role.isProtected ? <span className="mt-2 flex flex-wrap gap-1">{role.isSystem ? <span className="rounded-full border px-2 py-0.5 text-[10px] text-info">{t("administration.areas.roles.systemRole")}</span> : null}{role.isProtected ? <span className="rounded-full border px-2 py-0.5 text-[10px]">{t("administration.areas.roles.protectedRole")}</span> : null}</span> : null}</span>
                      </label>)}</div>
                    </section>)}
                    {filteredUnavailableAssignedRoles.length > 0 ? <section className="space-y-2">
                      <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("administration.users.currentRolesNotAssignable")}</h4>
                      <div className="grid gap-2 sm:grid-cols-2">{filteredUnavailableAssignedRoles.map((role) => <label key={role.id} htmlFor={"user-role-current-" + role.id} className="flex items-start gap-3 rounded-lg border p-3">
                        <input id={"user-role-current-" + role.id} type="checkbox" checked={draft.roleIds.includes(role.id)} disabled={isSavingUser || isSelfEdit || !role.canRemove} onChange={(event) => setRoleSelected(role.id, event.target.checked, false, role.canRemove)} className="mt-0.5 h-4 w-4 shrink-0 accent-primary" />
                        <span className="min-w-0"><span className="block text-sm font-medium">{getRoleDisplayName(role)}</span><span className="mt-1 block text-xs text-muted-foreground">{isUnresolvedRoleOwnership(role) ? t("administration.users.unresolvedRoleOwnershipHint") : role.canRemove ? t("administration.areas.access.unavailableAssignedRole") : t("administration.users.assignedRoleRemovalProtected")}</span></span>
                      </label>)}</div>
                    </section> : null}
                    {filteredRoles.length === 0 && filteredUnavailableAssignedRoles.length === 0
                      ? <p className="py-5 text-center text-sm text-muted-foreground">{roleTerm ? t("common.noResults") : t("administration.users.noAssignableRoles")}</p>
                      : null}
                  </div>}
              <ApiFieldError id="user-roles-error" error={apiError} fieldName="roleIds" />
              {isSelfEdit ? <p className="text-xs text-muted-foreground">{t("administration.areas.access.selfRoleLocked")}</p> : null}
              </> : <div className="flex flex-wrap gap-2">{(editingUser?.roles ?? []).filter((role) => !isProtectedTenantRole(role)).map((role) => <Badge key={role.id} variant="outline">{getRoleDisplayName(role)}</Badge>)}{(editingUser?.roles ?? []).filter((role) => !isProtectedTenantRole(role)).length === 0 ? <span className="text-sm text-muted-foreground">-</span> : null}</div>}
            </section>
            {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" disabled={isSavingUser} onClick={closeUserForm}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isSavingUser || (canAssignRoles && (roles.isLoading || roles.isError))}>{isSavingUser ? t("administration.users.saving") : isCreatingUser ? t("administration.areas.access.createUserAction") : t("administration.users.save")}</Button>
          </SideSheetFooter>
        </form> : null}
      </SideSheet>
      {credentialError ? <p role="alert" className="text-sm text-destructive">{credentialError}</p> : null}
      <TemporaryPasswordDialog credential={credential} onClose={() => setCredential(null)} />
    </div>
  );
}

function TemporaryPasswordDialog({ credential, onClose }: { credential: { userName: string; password: string; expiresAt?: string } | null; onClose: () => void }) {
  const { t, locale } = useI18n();
  const expiry = credential?.expiresAt && Number.isFinite(new Date(credential.expiresAt).getTime()) ? formatDateTime(credential.expiresAt, locale) : null;
  const [revealed, setRevealed] = React.useState(false);
  const [copied, setCopied] = React.useState(false);
  const [copyFailed, setCopyFailed] = React.useState(false);

  React.useEffect(() => {
    setRevealed(false);
    setCopied(false);
    setCopyFailed(false);
  }, [credential]);

  async function copyPassword() {
    if (!credential) return;
    try {
      await navigator.clipboard.writeText(credential.password);
      setCopied(true);
      setCopyFailed(false);
    } catch {
      setCopyFailed(true);
      setCopied(false);
    }
  }

  return <Dialog open={Boolean(credential)} onOpenChange={(open) => { if (!open) onClose(); }}>
    <DialogContent>
      <DialogHeader>
        <DialogTitle>{t("administration.users.temporaryPasswordTitle")}</DialogTitle>
        <DialogDescription>{t("administration.users.temporaryPasswordDescription", { name: credential?.userName ?? "" })}</DialogDescription>
      </DialogHeader>
      <div className="space-y-3">
        <Label htmlFor="one-time-temporary-password">{t("administration.users.temporaryPasswordLabel")}</Label>
        <div className="flex gap-2">
          <Input id="one-time-temporary-password" type={revealed ? "text" : "password"} value={credential?.password ?? ""} readOnly autoComplete="off" className="font-mono" />
          <Button type="button" size="icon" variant="outline" aria-label={revealed ? t("administration.users.hidePassword") : t("administration.users.showPassword")} onClick={() => setRevealed((current) => !current)}>{revealed ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}</Button>
          <Button type="button" variant="outline" onClick={() => void copyPassword()}><Copy className="me-2 h-4 w-4" />{t("administration.users.copyPassword")}</Button>
        </div>
        {copied ? <p role="status" className="text-sm text-success">{t("administration.users.passwordCopied")}</p> : null}
        {copyFailed ? <p role="alert" className="text-sm text-destructive">{t("administration.users.passwordCopyFailed")}</p> : null}
        {expiry ? <p className="text-sm text-muted-foreground">{t("administration.users.temporaryPasswordExpires", { expiresAt: expiry })}</p> : null}
        <p className="text-sm text-warning">{t("administration.users.temporaryPasswordOneTimeWarning")}</p>
      </div>
      <DialogFooter><Button type="button" onClick={onClose}>{t("common.close")}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}

export function UserPermissionsPage() {
  const { t, locale } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const { userId } = useParams<{ userId: string }>();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Users.ManageOverrides));
  const routeState = location.state as { userName?: unknown } | null;
  const userName = typeof routeState?.userName === "string" ? routeState.userName : null;
  const permissions = useUserPermissions(userId ?? null, canManage);
  const savePermissions = useSaveUserPermissions();
  const [draft, setDraft] = React.useState<Record<string, PermissionOverrideEffect>>({});
  const [search, setSearch] = React.useState("");
  const [saveError, setSaveError] = React.useState<string | null>(null);
  const hasUnmanageableOverrides = (permissions.data ?? []).some((permission) =>
    permission.overrideEffect !== null && permission.canDelegate !== true);

  React.useEffect(() => {
    if (permissions.data) {
      setDraft(Object.fromEntries(permissions.data.map((item) => [
        item.id,
        item.overrideEffect ?? "Inherit",
      ])));
    }
  }, [permissions.data, userId]);

  const groups = React.useMemo(() => {
    const grouped = new Map<string, UserPermissionAssignmentDto[]>();
    const term = search.trim().toLocaleLowerCase();
    for (const permission of permissions.data ?? []) {
      const label = (permission.name + " " + getPermissionDisplayName(permission, locale)).toLocaleLowerCase();
      if (term && !label.includes(term)) continue;
      const group = grouped.get(permission.resource) ?? [];
      group.push(permission);
      grouped.set(permission.resource, group);
    }
    return [...grouped.entries()];
  }, [permissions.data, search, locale]);

  async function handleSave() {
    if (!userId || savePermissions.isPending) return;
    setSaveError(null);
    try {
      const updated = await savePermissions.mutateAsync({ userId, overrides: toUserPermissionOverrides(draft) });
      setDraft(Object.fromEntries(updated.map((item) => [item.id, item.overrideEffect ?? "Inherit"])));
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : t("administration.areas.access.saveError"));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={userName ? t("administration.areas.access.userPermissions", { name: userName }) : t("administration.areas.access.userPermissionsTitle")}
        description={t("administration.areas.access.permissionInstructions")}
        actions={<Button type="button" size="sm" variant="outline" onClick={() => navigate("/administration/access/users")}><ArrowLeft className="me-2 h-4 w-4" />{t("administration.areas.access.backToUsers")}</Button>}
      />
      {permissions.isLoading ? <Card><CardContent className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</CardContent></Card>
        : permissions.isError ? <Card><CardContent className="flex flex-col items-start gap-3 p-6"><p role="alert" className="text-sm text-destructive">{t("administration.areas.access.loadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void permissions.refetch()}>{t("common.retry")}</Button></CardContent></Card>
          : <Card>
            <CardHeader><CardTitle>{t("administration.areas.access.permissionDirectory")}</CardTitle><CardDescription>{t("administration.areas.access.permissionInstructions")}</CardDescription></CardHeader>
            <CardContent className="space-y-4">
              <p id="permission-assignment-scope-hint" className="text-sm text-muted-foreground">{t("administration.areas.access.permissionScopeHint")}</p>
              <p className="text-xs text-muted-foreground">{t("administration.areas.access.effectiveScopeNote")}</p>
              <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("administration.areas.access.searchPermissions")} aria-label={t("administration.areas.access.searchPermissions")} />
              {groups.length === 0 ? <p className="py-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
                : <div className="grid gap-4 md:grid-cols-2">{groups.map(([resource, group]) => <section key={resource} className="space-y-2">
                  <h3 className="text-sm font-semibold">{getPermissionResourceLabel(group[0], locale)}</h3>
                  <div className="overflow-hidden rounded-lg border">
                    {group.map((permission) => {
                      const effect = draft[permission.id] ?? "Inherit";
                      const isEffective = permission.isEffective;
                      const savedEffect = permission.overrideEffect ?? "Inherit";
                      const hasPendingOverride = effect !== savedEffect;
                      const roleGrantSources = permission.roleGrantSources ?? [];
                      const scopeLabel = permission.scope ? getPermissionScopeValueLabel(permission.scope, t) : undefined;
                      const canOverride = permission.canDelegate === true;
                      const canDirectlyAllow = permission.isAssignable && permission.scope === "Tenant";
                      const effectTextClass = effect === "Allow" ? "text-success" : effect === "Deny" ? "text-destructive" : "text-muted-foreground";
                      return <div key={permission.id} className="grid gap-3 border-b p-3 last:border-b-0 lg:grid-cols-[minmax(0,1fr)_13rem] lg:items-center">
                        <div className="min-w-0">
  <p className="text-sm font-medium">{getPermissionDisplayName(permission, locale)}</p>
  <p className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
    <span className={permission.isRoleGranted ? "text-info" : "text-muted-foreground"}>{permission.isRoleGranted ? t("administration.areas.access.roleGranted") : t("administration.areas.access.notRoleGranted")}</span>
    <span className={isEffective ? "text-success" : "text-muted-foreground"}>{isEffective ? t("administration.areas.access.effective") : t("administration.areas.access.notEffective")}</span>
    {roleGrantSources.length ? <span className="text-muted-foreground">{t("administration.areas.access.roleGrantSources", { roles: roleGrantSources.join(", ") })}</span> : null}
    {permission.overrideEffect === "Allow" ? <span className="text-success">{t("administration.areas.access.directAllowSource")}</span> : permission.overrideEffect === "Deny" ? <span className="text-destructive">{t("administration.areas.access.directDenySource")}</span> : null}
    {scopeLabel ? <span className="text-muted-foreground">{t("administration.areas.access.permissionScopeLabel")}: {scopeLabel}</span> : null}
    {hasPendingOverride ? <span className="text-info">{t("administration.areas.access.unsavedOverride", { effect: t("administration.areas.access." + effect.toLowerCase()) })}</span> : null}
    {!canOverride ? <span className="text-muted-foreground">{t("administration.areas.access.permissionNotAssignable")}</span> : null}
  </p>
</div><div className="space-y-1"><Label className="sr-only" htmlFor={"user-permission-" + permission.id}>{t("administration.areas.access.overrideFor", { permission: getPermissionDisplayName(permission, locale) })}</Label><select id={"user-permission-" + permission.id} aria-describedby={!canOverride ? "permission-assignment-scope-hint" : undefined} className={`h-9 w-full rounded-md border border-input bg-background px-3 text-sm ${effectTextClass}`} value={effect} disabled={savePermissions.isPending || !canOverride} onChange={(event) => { const nextEffect = event.target.value; if (isPermissionOverrideEffect(nextEffect) && (nextEffect !== "Allow" || canDirectlyAllow)) setDraft((current) => ({ ...current, [permission.id]: nextEffect })); }}>
                          <option value="Inherit">{t("administration.areas.access.inherit")}</option><option value="Allow" disabled={!canDirectlyAllow}>{t("administration.areas.access.allow")}</option><option value="Deny">{t("administration.areas.access.deny")}</option>
                        </select></div>
                      </div>;
                    })}
                  </div>
                </section>)}</div>}
              {saveError ? <p role="alert" className="text-sm text-destructive">{saveError}</p> : null}
              <div className="flex flex-wrap items-center gap-2 border-t pt-4">
                <Button type="button" onClick={() => void handleSave()} disabled={permissions.isLoading || permissions.isError || savePermissions.isPending || hasUnmanageableOverrides}>{savePermissions.isPending ? t("common.saving") : t("administration.areas.access.savePermissions")}</Button>
                {hasUnmanageableOverrides ? <p role="note" className="text-sm text-muted-foreground">{t("administration.areas.access.unmanageableOverrides")}</p> : null}
                <Button type="button" variant="outline" disabled={savePermissions.isPending} onClick={() => navigate("/administration/access/users")}>{t("common.cancel")}</Button>
              </div>
            </CardContent>
          </Card>}
    </div>
  );
}
