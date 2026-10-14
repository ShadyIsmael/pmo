import * as React from "react";
import { ArrowLeft, ArrowUpRight, Copy, KeyRound, Pencil, Plus, ShieldCheck, UsersRound, Eye, EyeOff } from "lucide-react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { getStatusTextClass } from "@lib/status-presentation";
import { useAdminUsers, useAssignableRoles, useCreateAdminUser, useResetAdminUserPassword, useSaveUserPermissions, useUpdateAdminUser, useUserPermissions, toUserPermissionOverrides, type AdminUserDto, type PermissionOverrideEffect, type UserPermissionAssignmentDto } from "./api/admin";
import { getPermissionDisplayName, getPermissionResourceLabel } from "./permission-labels";

function isPermissionOverrideEffect(value: string): value is PermissionOverrideEffect {
  return value === "Inherit" || value === "Allow" || value === "Deny";
}

export function AccessPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const hasUsersRead = hasPermission(permissions, Permissions.Users.Read);
  const hasUsersWrite = hasPermission(permissions, Permissions.Users.Write);
  const components = [
    ...(hasUsersRead ? [{ id: "users", href: "/administration/access/users", icon: UsersRound, title: t("administration.areas.access.users.title"), description: t("administration.areas.access.users.description") }] : []),
    ...(hasUsersWrite ? [{ id: "roles", href: "/administration/access/roles", icon: ShieldCheck, title: t("administration.areas.access.roles.title"), description: t("administration.areas.access.roles.description") }] : []),
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
  const canManageUsers = Boolean(user && hasPermission(user.permissions, Permissions.Users.Write) && !effectiveReadOnly);
  const canResetPasswords = Boolean(user && hasPermission(user.permissions, Permissions.Users.ResetPassword) && !effectiveReadOnly);
  const users = useAdminUsers(true, { page, pageSize: 25, search: search.trim() });
  const roles = useAssignableRoles(canManageUsers);
  const createUser = useCreateAdminUser();
  const updateUser = useUpdateAdminUser();
  const resetPassword = useResetAdminUserPassword();
  const [editingUser, setEditingUser] = React.useState<AdminUserDto | null>(null);
  const [isCreatingUser, setIsCreatingUser] = React.useState(false);
  const [draft, setDraft] = React.useState<{ firstName: string; lastName: string; email: string; authenticationSource: AdminUserDto["authenticationSource"]; jobTitle: string; phone: string; isActive: boolean; roles: string[] }>({ firstName: "", lastName: "", email: "", authenticationSource: "Local", jobTitle: "", phone: "", isActive: true, roles: [] });
  const [roleSearch, setRoleSearch] = React.useState("");
  const [formError, setFormError] = React.useState<string | null>(null);
  const [credential, setCredential] = React.useState<{ userName: string; password: string; expiresAt: string } | null>(null);
  const [credentialError, setCredentialError] = React.useState<string | null>(null);
  const isSavingUser = createUser.isPending || updateUser.isPending;
  const isSelfEdit = Boolean(editingUser && editingUser.id === user?.id);
  const items = users.data?.items ?? [];
  const roleTerm = roleSearch.trim().toLocaleLowerCase();
  const filteredRoles = (roles.data ?? []).filter((role) => !roleTerm || role.name.toLocaleLowerCase().includes(roleTerm));
  const assignableRoleNames = new Set((roles.data ?? []).map((role) => role.name));
  const unavailableAssignedRoles = (editingUser?.roles ?? []).filter((name) => !assignableRoleNames.has(name));
  const filteredUnavailableAssignedRoles = unavailableAssignedRoles.filter((name) => !roleTerm || name.toLocaleLowerCase().includes(roleTerm));
  const hasUnavailableRoleSelected = unavailableAssignedRoles.some((name) => draft.roles.includes(name));

  function beginCreateUser() {
    setEditingUser(null);
    setIsCreatingUser(true);
    setDraft({ firstName: "", lastName: "", email: "", authenticationSource: "Local", jobTitle: "", phone: "", isActive: true, roles: [] });
    setRoleSearch("");
    setFormError(null);
  }

  function beginEditUser(item: AdminUserDto) {
    setEditingUser(item);
    setIsCreatingUser(false);
    setDraft({ firstName: item.firstName, lastName: item.lastName, email: item.email, authenticationSource: item.authenticationSource ?? "Local", jobTitle: item.jobTitle ?? "", phone: item.phone ?? "", isActive: item.isActive, roles: [...item.roles] });
    setRoleSearch("");
    setFormError(null);
  }

  function closeUserForm() {
    if (isSavingUser) return;
    setEditingUser(null);
    setIsCreatingUser(false);
    setFormError(null);
  }

  async function handleSaveUser(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if ((!editingUser && !isCreatingUser) || isSavingUser || roles.isLoading || roles.isError) return;
    setFormError(null);
    const input = {
      email: draft.email.trim(),
      firstName: draft.firstName.trim(),
      lastName: draft.lastName.trim(),
      jobTitle: draft.jobTitle.trim() || null,
      phone: draft.phone.trim() || null,
      isActive: draft.isActive,
      roles: draft.roles,
    };
    try {
      if (editingUser) {
        await updateUser.mutateAsync({ userId: editingUser.id, input });
      } else {
        const created = await createUser.mutateAsync({ ...input, authenticationSource: draft.authenticationSource });
        if (created.temporaryPassword) {
          setCredential({ userName: created.user.fullName, password: created.temporaryPassword, expiresAt: created.user.temporaryPasswordExpiresAt ?? new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString() });
        }
      }
      setEditingUser(null);
      setIsCreatingUser(false);
    } catch (error) {
      setFormError(error instanceof Error ? error.message : t("administration.users.error"));
    }
  }

  async function handleResetPassword(item: AdminUserDto) {
    setCredentialError(null);
    try {
      const result = await resetPassword.mutateAsync(item.id);
      setCredential({ userName: item.fullName, password: result.temporaryPassword, expiresAt: result.expiresAt });
    } catch {
      setCredentialError(t("administration.users.resetPasswordError"));
    }
  }

  function updateDraft<K extends keyof typeof draft>(key: K, value: (typeof draft)[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  const userSheetOpen = Boolean(editingUser || isCreatingUser);
  const userSheetTitle = editingUser
    ? t("administration.users.editUser")
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
                  <TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.email")}</TableHead><TableHead>{t("administration.users.authenticationSource")}</TableHead><TableHead>{t("nav.roles")}</TableHead><TableHead>{t("administration.users.status")}</TableHead>{canManageUsers || canResetPasswords ? <TableHead>{t("common.actions")}</TableHead> : null}</TableRow></TableHeader>
                  <TableBody>{items.map((item) => <TableRow key={item.id}>
                    <TableCell className="font-medium">{item.fullName}</TableCell>
                    <TableCell className="text-muted-foreground">{item.email}</TableCell>
                    <TableCell>{t(`administration.users.sources.${item.authenticationSource ?? "Local"}`)}</TableCell>
                    <TableCell><div className="flex flex-wrap gap-1">{item.roles.length > 0 ? item.roles.map((role) => <Badge key={role} variant="outline">{role}</Badge>) : <span className="text-muted-foreground">—</span>}</div></TableCell>
                    <TableCell><span className={`text-sm font-medium ${getStatusTextClass(item.isActive ? "active" : "inactive")}`}>{item.isActive ? t("administration.users.active") : t("administration.users.inactive")}</span></TableCell>
                    {canManageUsers || canResetPasswords ? <TableCell><div className="flex flex-wrap gap-2">
                      {canManageUsers ? <Button type="button" size="sm" variant="outline" onClick={() => beginEditUser(item)} aria-label={`${t("administration.users.edit")} ${item.fullName}`}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("administration.users.edit")}</Button> : null}
                      {canResetPasswords && (item.authenticationSource ?? "Local") === "Local" && item.id !== user?.id ? <Button type="button" size="sm" variant="outline" disabled={resetPassword.isPending} onClick={() => void handleResetPassword(item)}><KeyRound className="me-1.5 h-3.5 w-3.5" />{t("administration.users.resetPassword")}</Button> : null}
                      {canManageUsers ? item.id === user?.id
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
              <div className="space-y-2"><Label htmlFor="user-first-name">{t("administration.users.firstName")}</Label><Input id="user-first-name" autoComplete="given-name" value={draft.firstName} disabled={isSavingUser} onChange={(event) => updateDraft("firstName", event.target.value)} required maxLength={100} /></div>
              <div className="space-y-2"><Label htmlFor="user-last-name">{t("administration.users.lastName")}</Label><Input id="user-last-name" autoComplete="family-name" value={draft.lastName} disabled={isSavingUser} onChange={(event) => updateDraft("lastName", event.target.value)} required maxLength={100} /></div>
              <div className="space-y-2"><Label htmlFor="user-email">{t("administration.users.email")}</Label><Input id="user-email" type="email" autoComplete="email" value={draft.email} disabled={isSavingUser} onChange={(event) => updateDraft("email", event.target.value)} required maxLength={256} /></div>
              {isCreatingUser ? <div className="space-y-2"><Label htmlFor="user-authentication-source">{t("administration.users.authenticationSource")}</Label><select id="user-authentication-source" className="input-field" value={draft.authenticationSource} disabled={isSavingUser} onChange={(event) => updateDraft("authenticationSource", event.target.value as AdminUserDto["authenticationSource"])}><option value="Local">{t("administration.users.sources.Local")}</option><option value="EntraId" disabled>{t("administration.users.sources.EntraId")} — {t("administration.users.directoryUnavailable")}</option><option value="ActiveDirectoryLdap" disabled>{t("administration.users.sources.ActiveDirectoryLdap")} — {t("administration.users.directoryUnavailable")}</option></select><p className="text-xs text-muted-foreground">{t("administration.users.directoryProvisioningHint")}</p></div> : null}
              <div className="space-y-2"><Label htmlFor="user-job-title">{t("administration.users.jobTitle")}</Label><Input id="user-job-title" autoComplete="organization-title" value={draft.jobTitle} disabled={isSavingUser} onChange={(event) => updateDraft("jobTitle", event.target.value)} maxLength={150} /></div>
              <div className="space-y-2"><Label htmlFor="user-phone">{t("administration.users.phone")}</Label><Input id="user-phone" type="tel" autoComplete="tel" value={draft.phone} disabled={isSavingUser} onChange={(event) => updateDraft("phone", event.target.value)} maxLength={40} /></div>
            </div>
            <label htmlFor="user-is-active" className="flex cursor-pointer items-start gap-3 rounded-lg border p-3">
              <input id="user-is-active" type="checkbox" checked={draft.isActive} disabled={isSavingUser || isSelfEdit} onChange={(event) => updateDraft("isActive", event.target.checked)} className="mt-0.5 h-4 w-4 accent-primary" />
              <span className="text-sm font-medium">{t("administration.users.isActive")}{isSelfEdit ? <span className="ms-2 text-xs font-normal text-muted-foreground">{t("administration.areas.access.selfStatusLocked")}</span> : null}</span>
            </label>
            <section className="space-y-3">
              <div className="space-y-1"><h3 className="text-sm font-semibold">{t("administration.users.rolesLabel")}</h3><p className="text-xs text-muted-foreground">{t("administration.areas.access.roleAssignmentInstructions")}</p></div>
              <Input value={roleSearch} onChange={(event) => setRoleSearch(event.target.value)} placeholder={t("administration.areas.roles.searchRoles")} aria-label={t("administration.areas.roles.searchRoles")} disabled={isSavingUser || isSelfEdit} />
              {roles.isLoading ? <p className="py-5 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                : roles.isError ? <div className="flex flex-col items-center gap-3 py-5"><p role="alert" className="text-sm text-destructive">{t("administration.areas.access.roleLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void roles.refetch()}>{t("common.retry")}</Button></div>
                  : filteredRoles.length === 0 && filteredUnavailableAssignedRoles.length === 0 ? <p className="py-5 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
                    : <div className="grid gap-2 sm:grid-cols-2">
                      {filteredRoles.map((role) => <label key={role.id} htmlFor={"user-role-" + role.id} className="flex cursor-pointer items-start gap-3 rounded-lg border p-3 hover:bg-accent/50 focus-within:ring-2 focus-within:ring-ring">
                        <input id={"user-role-" + role.id} type="checkbox" checked={draft.roles.includes(role.name)} disabled={isSavingUser || isSelfEdit || (hasUnavailableRoleSelected && !draft.roles.includes(role.name))} onChange={(event) => updateDraft("roles", event.target.checked ? [...new Set([...draft.roles, role.name])] : draft.roles.filter((name) => name !== role.name))} className="mt-0.5 h-4 w-4 shrink-0 accent-primary" />
                        <span className="min-w-0"><span className="block text-sm font-medium">{role.name}</span>{role.description ? <span className="mt-1 block text-xs text-muted-foreground">{role.description}</span> : null}</span>
                      </label>)}
                      {filteredUnavailableAssignedRoles.map((name) => <label key={name} htmlFor={"user-role-unavailable-" + encodeURIComponent(name)} className="flex cursor-pointer items-start gap-3 rounded-lg border p-3 hover:bg-accent/50 focus-within:ring-2 focus-within:ring-ring">
                        <input id={"user-role-unavailable-" + encodeURIComponent(name)} type="checkbox" checked={draft.roles.includes(name)} disabled={isSavingUser || isSelfEdit || !draft.roles.includes(name)} onChange={(event) => { if (!event.target.checked) updateDraft("roles", draft.roles.filter((roleName) => roleName !== name)); }} className="mt-0.5 h-4 w-4 shrink-0 accent-primary" />
                        <span className="min-w-0"><span className="block text-sm font-medium">{name}</span><span className="mt-1 block text-xs text-muted-foreground">{t("administration.areas.access.unavailableAssignedRole")}</span></span>
                      </label>)}
                    </div>}
              {hasUnavailableRoleSelected && !isSelfEdit ? <p className="text-xs text-muted-foreground">{t("administration.areas.access.removeUnavailableRoleFirst")}</p> : null}
              {isSelfEdit ? <p className="text-xs text-muted-foreground">{t("administration.areas.access.selfRoleLocked")}</p> : null}
            </section>
            {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" disabled={isSavingUser} onClick={closeUserForm}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isSavingUser || roles.isLoading || roles.isError}>{isSavingUser ? t("administration.users.saving") : isCreatingUser ? t("administration.areas.access.createUserAction") : t("administration.users.save")}</Button>
          </SideSheetFooter>
        </form> : null}
      </SideSheet>
      {credentialError ? <p role="alert" className="text-sm text-destructive">{credentialError}</p> : null}
      <TemporaryPasswordDialog credential={credential} onClose={() => setCredential(null)} />
    </div>
  );
}

function TemporaryPasswordDialog({ credential, onClose }: { credential: { userName: string; password: string; expiresAt: string } | null; onClose: () => void }) {
  const { t } = useI18n();
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
        <p className="text-sm text-muted-foreground">{t("administration.users.temporaryPasswordExpires", { expiresAt: credential ? new Date(credential.expiresAt).toLocaleString() : "" })}</p>
        <p className="text-sm text-warning">{t("administration.users.temporaryPasswordOneTimeWarning")}</p>
      </div>
      <DialogFooter><Button type="button" onClick={onClose}>{t("common.close")}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}

export function UserPermissionsPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const { userId } = useParams<{ userId: string }>();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Users.Write));
  const routeState = location.state as { userName?: unknown } | null;
  const userName = typeof routeState?.userName === "string" ? routeState.userName : null;
  const permissions = useUserPermissions(userId ?? null, canManage);
  const savePermissions = useSaveUserPermissions();
  const [draft, setDraft] = React.useState<Record<string, PermissionOverrideEffect>>({});
  const [search, setSearch] = React.useState("");
  const [saveError, setSaveError] = React.useState<string | null>(null);
  const actorPermissions = user?.permissions ?? [];
  const hasUnmanageableOverrides = (permissions.data ?? []).some((permission) =>
    permission.overrideEffect !== null && !hasPermission(actorPermissions, permission.name));

  React.useEffect(() => {
    if (permissions.data) {
      setDraft(Object.fromEntries(permissions.data.map((item) => [item.id, item.overrideEffect ?? "Inherit"])));
    }
  }, [permissions.data, userId]);

  const groups = React.useMemo(() => {
    const grouped = new Map<string, UserPermissionAssignmentDto[]>();
    const term = search.trim().toLocaleLowerCase();
    for (const permission of permissions.data ?? []) {
      const label = (permission.name + " " + getPermissionDisplayName(permission, t)).toLocaleLowerCase();
      if (term && !label.includes(term)) continue;
      const group = grouped.get(permission.resource) ?? [];
      group.push(permission);
      grouped.set(permission.resource, group);
    }
    return [...grouped.entries()];
  }, [permissions.data, search, t]);

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
              <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("administration.areas.access.searchPermissions")} aria-label={t("administration.areas.access.searchPermissions")} />
              {groups.length === 0 ? <p className="py-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
                : <div className="grid gap-4 md:grid-cols-2">{groups.map(([resource, group]) => <section key={resource} className="space-y-2">
                  <h3 className="text-sm font-semibold">{getPermissionResourceLabel(group[0], t)}</h3>
                  <div className="overflow-hidden rounded-lg border">
                    {group.map((permission) => {
                      const effect = draft[permission.id] ?? "Inherit";
                      const isEffective = effect === "Allow" || (effect === "Inherit" && permission.isRoleGranted);
                      const canOverride = hasPermission(actorPermissions, permission.name);
                      const effectTextClass = effect === "Allow" ? "text-success" : effect === "Deny" ? "text-destructive" : "text-muted-foreground";
                      return <div key={permission.id} className="grid gap-3 border-b p-3 last:border-b-0 lg:grid-cols-[minmax(0,1fr)_13rem] lg:items-center">
                        <div className="min-w-0"><p className="text-sm font-medium">{getPermissionDisplayName(permission, t)}</p><p className="mt-1 text-xs"><span className={permission.isRoleGranted ? "text-info" : "text-muted-foreground"}>{permission.isRoleGranted ? t("administration.areas.access.roleGranted") : t("administration.areas.access.notRoleGranted")}</span><span className="text-muted-foreground"> · </span><span className={isEffective ? "text-success" : "text-muted-foreground"}>{isEffective ? t("administration.areas.access.effective") : t("administration.areas.access.notEffective")}</span>{!canOverride ? <span className="mt-1 block text-muted-foreground">{t("administration.areas.access.permissionNotAssignable")}</span> : null}</p></div>
                        <div className="space-y-1"><Label className="sr-only" htmlFor={"user-permission-" + permission.id}>{t("administration.areas.access.overrideFor", { permission: getPermissionDisplayName(permission, t) })}</Label><select id={"user-permission-" + permission.id} aria-describedby={!canOverride ? "permission-assignment-scope-hint" : undefined} className={`h-9 w-full rounded-md border border-input bg-background px-3 text-sm ${effectTextClass}`} value={effect} disabled={savePermissions.isPending || !canOverride} onChange={(event) => { const nextEffect = event.target.value; if (isPermissionOverrideEffect(nextEffect)) setDraft((current) => ({ ...current, [permission.id]: nextEffect })); }}>
                          <option value="Inherit">{t("administration.areas.access.inherit")}</option><option value="Allow">{t("administration.areas.access.allow")}</option><option value="Deny">{t("administration.areas.access.deny")}</option>
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
