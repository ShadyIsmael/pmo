import * as React from "react";
import { ArrowLeft, ChevronDown, LockKeyhole, Plus, ShieldAlert, ShieldCheck, Trash2 } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { ApiError, getApiFieldError } from "@api/client";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import {
  usePermissionCatalog,
  useDeleteManagedRole,
  useManagedRole,
  useManagedRoles,
  useSaveManagedRole,
  type ManagedRoleDto,
  type PermissionCatalogItemDto,
  type SaveRoleInput,
} from "./api/admin";
import { getPermissionActionLabel, getPermissionDescription, getPermissionDisplayName, getPermissionModuleLabel, getPermissionResourceLabel } from "./permission-labels";

const ROLE_CATEGORY_OPTIONS = ["Tenant", "Organization", "Management", "Operation", "Business"] as const;
const BOOTSTRAP_PERMISSION_KEYS = new Set([
  "ProjectPlan.Create",
  "Planning.Edit",
  "Planning.View",
  "ProjectAnnualBudget.View",
  "ProjectAnnualBudget.Create",
  "ProjectAnnualBudget.EditDraft",
  "ProjectAnnualBudget.ConfigureDraftApproval",
  "ProjectAnnualBudget.SubmitForReview",
  "ProjectAnnualBudget.Approve",
  "ProjectAnnualBudget.ReviseApproved",
  "ProjectAnnualBudget.ManageApprovalConfiguration",
  "ProjectAnnualBudget.Delete",
  "ProjectAnnualBudget.Withdraw",
  "ProjectAnnualBudgetClosing.View",
  "ProjectAnnualBudget.Close",
  "ProjectAnnualBudget.CloseApprove",
  "ProjectAnnualBudget.CarryForward",
  "ProjectFinancialFoundation.View",
  "CostUtilization.Export",
  "ProgramFinancialRollup.View",
  "PortfolioFinancialRollup.View",
]);
const PROTECTED_ADMIN_BOOTSTRAP_PERMISSION_KEYS = new Set([
  "ProjectAnnualBudget.View",
  "ProjectAnnualBudget.Create",
  "ProjectAnnualBudget.EditDraft",
  "ProjectAnnualBudget.SubmitForReview",
  "ProjectAnnualBudget.Approve",
  "ProjectAnnualBudget.ReviseApproved",
  "ProjectAnnualBudget.ManageApprovalConfiguration",
  "ProjectAnnualBudget.Delete",
  "ProjectAnnualBudget.Withdraw",
  "ProjectAnnualBudgetClosing.View",
  "ProjectAnnualBudget.Close",
  "ProjectAnnualBudget.CloseApprove",
  "ProjectAnnualBudget.CarryForward",
]);
type Translate = ReturnType<typeof useI18n>["t"];

function getRoleCategoryLabel(category: string | undefined, t: Translate): string {
  if (!category) return t("administration.areas.roles.uncategorized");
  if (category.toLocaleLowerCase() === "system") return t("administration.areas.roles.systemCategory");
  const canonical = ROLE_CATEGORY_OPTIONS.find((option) => option.toLocaleLowerCase() === category.toLocaleLowerCase());
  return canonical ? t(`administration.areas.roles.categoryOptions.${canonical.toLocaleLowerCase()}`) : category;
}

function isCanonicalRoleCategory(category: string | undefined): boolean {
  return Boolean(category && ROLE_CATEGORY_OPTIONS.includes(category as typeof ROLE_CATEGORY_OPTIONS[number]));
}

function toSaveInput(role: ManagedRoleDto, catalog: PermissionCatalogItemDto[] = []): SaveRoleInput {
  const permissionIds = new Set(role.permissions.map((permission) => permission.id));
  if (role.isProtectedSystemAdmin) {
    catalog.filter((permission) => permission.isMandatoryForSystemAdmin).forEach((permission) => permissionIds.add(permission.id));
  }
  return {
    name: role.name,
    description: role.description ?? "",
    category: role.category ?? "",
    permissionIds: [...permissionIds],
  };
}

function emptyRole(): SaveRoleInput {
  return { name: "", description: "", category: "", permissionIds: [] };
}

export function RolesPage() {
  const { t, locale } = useI18n();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const { user, refreshUser } = useAuth();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Roles.Create));
  const canEditDetails = Boolean(user && hasPermission(user.permissions, Permissions.Roles.EditDetails));
  const canConfigurePermissions = Boolean(user && hasPermission(user.permissions, Permissions.Roles.ConfigurePermissions));
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Roles.Delete));
  const canManage = canCreate || canEditDetails || canConfigurePermissions || canDelete;
  const canView = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete]));
  const roles = useManagedRoles(canView);
  const permissionCatalog = usePermissionCatalog(canConfigurePermissions, user);
  const saveRole = useSaveManagedRole();
  const deleteRole = useDeleteManagedRole();
  const [selectedRoleId, setSelectedRoleId] = React.useState<string | null>(null);
  const [isCreating, setIsCreating] = React.useState(false);
  const [formOpen, setFormOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveRoleInput | null>(null);
  const [roleSearch, setRoleSearch] = React.useState("");
  const [permissionSearch, setPermissionSearch] = React.useState("");
  const [permissionModuleFilter, setPermissionModuleFilter] = React.useState("all");
  const [permissionLevelFilter, setPermissionLevelFilter] = React.useState("all");
  const [permissionGrantFilter, setPermissionGrantFilter] = React.useState("all");
  const [permissionSensitivityFilter, setPermissionSensitivityFilter] = React.useState("all");
  const [expandedScreens, setExpandedScreens] = React.useState<Set<string>>(new Set());
  const [searchCollapsedScreens, setSearchCollapsedScreens] = React.useState<Set<string>>(new Set());
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [roleApiError, setRoleApiError] = React.useState<unknown>(null);
  const roleDetails = useManagedRole(selectedRoleId, canView && !isCreating);
  const selectedRole = roles.data?.find((role) => role.id === selectedRoleId);

  React.useEffect(() => {
    if (!isCreating && roleDetails.data && roleDetails.data.id === selectedRoleId) {
      setDraft(toSaveInput(roleDetails.data, permissionCatalog.data ?? []));
    }
  }, [isCreating, permissionCatalog.data, roleDetails.data, selectedRoleId]);

  const filteredRoles = React.useMemo(() => {
    const term = roleSearch.trim().toLocaleLowerCase();
    return (roles.data ?? []).filter((role) => !term
      || `${role.name} ${role.category ?? ""} ${role.description ?? ""}`.toLocaleLowerCase().includes(term));
  }, [roleSearch, roles.data]);

  const permissionGroups = React.useMemo(() => {
    const screens = new Map<string, { screen: PermissionCatalogItemDto; views: { permission: PermissionCatalogItemDto; contextOnly: boolean }[]; actions: { permission: PermissionCatalogItemDto; contextOnly: boolean }[] }>();
    const term = permissionSearch.trim().toLocaleLowerCase();
    const catalog = permissionCatalog.data ?? [];
    const selectedIds = new Set(draft?.permissionIds ?? []);
    const matches = catalog.filter((permission) => {
      const label = `${permission.name} ${permission.resource} ${permission.action} ${permission.labelEn} ${permission.labelAr} ${permission.moduleNameEn} ${permission.moduleNameAr} ${permission.screenNameEn} ${permission.screenNameAr} ${permission.descriptionEn} ${permission.descriptionAr} ${(permission.requiredPermissionKeys ?? []).join(" ")}`.toLocaleLowerCase();
      const matchesSearch = !term || label.includes(term);
      const matchesModule = permissionModuleFilter === "all" || permission.moduleId === permissionModuleFilter;
      const matchesLevel = permissionLevelFilter === "all" || permission.level === permissionLevelFilter;
      const matchesGrant = permissionGrantFilter === "all"
        || (permissionGrantFilter === "assigned" ? selectedIds.has(permission.id) : !selectedIds.has(permission.id));
      const matchesSensitivity = permissionSensitivityFilter === "all"
        || permission.sensitivity === permissionSensitivityFilter;
      return matchesSearch && matchesModule && matchesLevel && matchesGrant && matchesSensitivity;
    });
    const included = new Map<string, { permission: PermissionCatalogItemDto; contextOnly: boolean }>(matches.map((permission) => [permission.name, { permission, contextOnly: false }]));
    for (const permission of matches) {
      for (const requiredKey of permission.requiredPermissionKeys ?? []) {
        const dependency = catalog.find((item) => item.name === requiredKey);
        if (dependency && !included.has(requiredKey)) included.set(requiredKey, { permission: dependency, contextOnly: true });
      }
    }
    for (const { permission, contextOnly } of included.values()) {
      const screenKey = `${permission.moduleId}:${permission.screenId}`;
      const screen = screens.get(screenKey) ?? { screen: permission, views: [], actions: [] };
      if (permission.level === "View") screen.views.push({ permission, contextOnly });
      else screen.actions.push({ permission, contextOnly });
      screens.set(screenKey, screen);
    }
    return [...screens.entries()]
      .sort(([leftId, left], [rightId, right]) => getPermissionResourceLabel(left.screen, locale).localeCompare(getPermissionResourceLabel(right.screen, locale), locale, { sensitivity: "base" }) || leftId.localeCompare(rightId))
      .map(([screenId, screen]) => [screenId, {
        ...screen,
        views: [...screen.views].sort((left, right) => getPermissionActionLabel(left.permission, locale).localeCompare(getPermissionActionLabel(right.permission, locale), locale, { sensitivity: "base" }) || left.permission.name.localeCompare(right.permission.name)),
        actions: [...screen.actions].sort((left, right) => getPermissionActionLabel(left.permission, locale).localeCompare(getPermissionActionLabel(right.permission, locale), locale, { sensitivity: "base" }) || left.permission.name.localeCompare(right.permission.name)),
      }] as const);
  }, [draft?.permissionIds, locale, permissionCatalog.data, permissionGrantFilter, permissionLevelFilter, permissionModuleFilter, permissionSearch, permissionSensitivityFilter]);

  const permissionModules = React.useMemo(() => {
    const modules = new Map<string, PermissionCatalogItemDto>();
    for (const permission of permissionCatalog.data ?? []) modules.set(permission.moduleId, permission);
    return [...modules.entries()].sort((left, right) => getPermissionModuleLabel(left[1], locale).localeCompare(getPermissionModuleLabel(right[1], locale), locale, { sensitivity: "base" }) || left[0].localeCompare(right[0]));
  }, [locale, permissionCatalog.data]);

  function beginCreate() {
    setSelectedRoleId(null);
    setIsCreating(true);
    setFormOpen(true);
    setDraft(emptyRole());
    setPermissionSearch("");
    resetPermissionFilters();
    setActionError(null);
    setRoleApiError(null);
  }

  function selectRole(roleId: string) {
    setSelectedRoleId(roleId);
    setIsCreating(false);
    setFormOpen(false);
    setDraft(null);
    setPermissionSearch("");
    resetPermissionFilters();
    setActionError(null);
    setRoleApiError(null);
  }

  function cancelEditing() {
    setActionError(null);
    setRoleApiError(null);
    setFormOpen(false);
    if (isCreating) {
      setIsCreating(false);
      setDraft(null);
      return;
    }
    if (roleDetails.data) setDraft(toSaveInput(roleDetails.data, permissionCatalog.data ?? []));
  }

  function beginEdit() {
    if (!roleDetails.data || isSaving) return;
    setDraft(toSaveInput(roleDetails.data, permissionCatalog.data ?? []));
    setIsCreating(false);
    setPermissionSearch("");
    resetPermissionFilters();
    setActionError(null);
    setRoleApiError(null);
    setFormOpen(true);
  }

  function resetPermissionFilters() {
    setPermissionModuleFilter("all");
    setPermissionLevelFilter("all");
    setPermissionGrantFilter("all");
    setPermissionSensitivityFilter("all");
    setSearchCollapsedScreens(new Set());
  }

  async function togglePermission(permission: PermissionCatalogItemDto, checked: boolean) {
    if (!draft) return;
    if (!checked && currentRole?.isProtectedSystemAdmin && permission.isMandatoryForSystemAdmin) return;
    const catalog = permissionCatalog.data ?? [];
    const selected = new Set(draft.permissionIds);
    const byKey = new Map(catalog.map((item) => [item.name, item]));
    if (checked) {
      if (!canGrantPermissionToRole(permission, currentRole)) return;
      const dependencies = (permission.requiredPermissionKeys ?? []).map((dependency) => byKey.get(dependency));
      if (dependencies.some((dependency) => !dependency || (!canGrantPermissionToRole(dependency, currentRole) && !selected.has(dependency.id)))) return;
      selected.add(permission.id);
      dependencies.forEach((dependency) => { if (dependency) selected.add(dependency.id); });
    } else if (permission.level === "View") {
      const dependentActions = catalog.filter((item) => item.level === "Action" && item.requiredPermissionKeys?.includes(permission.name) && selected.has(item.id)
        && (!currentRole?.isProtectedSystemAdmin || !item.isMandatoryForSystemAdmin));
      if (dependentActions.length > 0 && !await confirm({
        title: t("administration.areas.roles.removeViewTitle"),
        description: t("administration.areas.roles.removeViewDescription", { count: dependentActions.length }),
        confirmLabel: t("administration.areas.roles.removeViewConfirm"),
        destructive: true,
      })) return;
      selected.delete(permission.id);
      dependentActions.forEach((item) => selected.delete(item.id));
    } else {
      selected.delete(permission.id);
    }
    setDraft((current) => {
      if (!current) return current;
      return { ...current, permissionIds: [...selected] };
    });
  }

  async function togglePermissionGroup(items: PermissionCatalogItemDto[], checked: boolean) {
    if (!draft) return;
    const catalog = permissionCatalog.data ?? [];
    const selected = new Set(draft.permissionIds);
    if (checked) {
      const byKey = new Map(catalog.map((item) => [item.name, item]));
      for (const item of items) {
        if (!canGrantPermissionToRole(item, currentRole)) continue;
        const dependencies = (item.requiredPermissionKeys ?? []).map((dependency) => byKey.get(dependency));
        if (dependencies.some((dependency) => !dependency || (!canGrantPermissionToRole(dependency, currentRole) && !selected.has(dependency.id)))) continue;
        selected.add(item.id);
        dependencies.forEach((dependency) => { if (dependency) selected.add(dependency.id); });
      }
    } else {
      const viewKeys = new Set(items.filter((item) => item.level === "View").map((item) => item.name));
      const dependentActions = catalog.filter((item) => item.level === "Action"
        && item.requiredPermissionKeys?.some((key) => viewKeys.has(key)) && selected.has(item.id)
        && (!currentRole?.isProtectedSystemAdmin || !item.isMandatoryForSystemAdmin));
      if (dependentActions.length > 0 && !await confirm({
        title: t("administration.areas.roles.removeViewTitle"),
        description: t("administration.areas.roles.removeViewDescription", { count: dependentActions.length }),
        confirmLabel: t("administration.areas.roles.removeViewConfirm"),
        destructive: true,
      })) return;
      items.filter((item) => !currentRole?.isProtectedSystemAdmin || !item.isMandatoryForSystemAdmin)
        .forEach((item) => selected.delete(item.id));
      dependentActions.forEach((item) => selected.delete(item.id));
    }
    setDraft((current) => current ? { ...current, permissionIds: [...selected] } : current);
  }

  function toggleScreen(screenId: string) {
    if (permissionSearch.trim()) {
      setSearchCollapsedScreens((current) => {
        const next = new Set(current);
        if (next.has(screenId)) next.delete(screenId); else next.add(screenId);
        return next;
      });
      return;
    }
    setExpandedScreens((current) => {
      const next = new Set(current);
      if (next.has(screenId)) next.delete(screenId); else next.add(screenId);
      return next;
    });
  }

  async function handleSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!draft || !draft.name.trim() || saveRole.isPending) return;
    const catalog = permissionCatalog.data ?? [];
    const originalPermissionIds = roleDetails.data ? toSaveInput(roleDetails.data, catalog).permissionIds.sort() : [];
    const nextPermissionIds = [...new Set(draft.permissionIds)].sort();
    if (!isCreating && originalPermissionIds.join("|") !== nextPermissionIds.join("|")) {
      const originalIds = new Set(originalPermissionIds);
      const nextIds = new Set(nextPermissionIds);
      const additions = catalog.filter((permission) => nextIds.has(permission.id) && !originalIds.has(permission.id));
      const removals = (roleDetails.data?.permissions ?? []).filter((permission) => originalIds.has(permission.id) && !nextIds.has(permission.id));
      const nameList = (items: PermissionCatalogItemDto[]) => {
        const names = items.slice(0, 4).map((permission) => getPermissionDisplayName(permission, locale));
        if (items.length > 4) names.push(t("administration.areas.roles.additionalPermissionCount", { count: items.length - 4 }));
        return names.length > 0 ? names.join(locale === "ar" ? "، " : ", ") : t("administration.areas.roles.none");
      };
      if (!await confirm({
        title: t("administration.areas.roles.savePermissionChangesTitle"),
        description: t("administration.areas.roles.savePermissionChangesDetailedDescription", {
          role: roleDetails.data?.name ?? draft.name,
          added: additions.length,
          removed: removals.length,
          additions: nameList(additions),
          removals: nameList(removals),
        }),
        confirmLabel: t("administration.areas.roles.saveRole"),
      })) return;
    }
    setActionError(null);
    setRoleApiError(null);
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
      setDraft(toSaveInput(saved, permissionCatalog.data ?? []));
      try {
        await refreshUser();
        setFormOpen(false);
      } catch {
        setActionError(t("administration.areas.roles.permissionsRefreshError"));
      }
    } catch (error) {
      setRoleApiError(error);
      setActionError(error instanceof ApiError ? error.message : t("administration.areas.roles.saveError"));
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
      setActionError(error instanceof ApiError ? error.message : t("administration.areas.roles.deleteError"));
    }
  }

  const currentRole = isCreating ? null : roleDetails.data;
  const permissionIds = new Set(draft?.permissionIds ?? []);
  const isSaving = saveRole.isPending || deleteRole.isPending;
  const hasUnsavedChanges = Boolean(draft && (isCreating || !roleDetails.data
    || draft.name !== roleDetails.data.name
    || (draft.description ?? "") !== (roleDetails.data.description ?? "")
    || (draft.category ?? "") !== (roleDetails.data.category ?? "")
    || [...draft.permissionIds].sort().join("|") !== toSaveInput(roleDetails.data, permissionCatalog.data ?? []).permissionIds.sort().join("|")));
  const catalog = permissionCatalog.data ?? [];
  const financialReadPermission = catalog.find((permission) => permission.name === Permissions.ProjectFinancialFoundation.View);
  const financeReadLabel = financialReadPermission
    ? getPermissionDisplayName(financialReadPermission, locale)
    : t("administration.areas.roles.financialReadPermission");
  const persistedLegacyCategory = !isCreating && roleDetails.data?.category && !isCanonicalRoleCategory(roleDetails.data.category)
    ? roleDetails.data.category
    : undefined;
  const selectedCatalogPermissionCount = catalog.filter((permission) => permissionIds.has(permission.id)).length;
  const baselinePermissionIds = new Set(currentRole?.permissions.map((permission) => permission.id) ?? []);
  if (currentRole?.isProtectedSystemAdmin) catalog.filter((permission) => permission.isMandatoryForSystemAdmin).forEach((permission) => baselinePermissionIds.add(permission.id));
  const pendingPermissionAdds = catalog.filter((permission) => permissionIds.has(permission.id) && !baselinePermissionIds.has(permission.id));
  const pendingPermissionRemovals = (currentRole?.permissions ?? []).filter((permission) => baselinePermissionIds.has(permission.id) && !permissionIds.has(permission.id));
  const catalogByName = new Map(catalog.map((permission) => [permission.name, permission]));
  const canGrantWithDependencies = (permission: PermissionCatalogItemDto) => canGrantPermissionToRole(permission, currentRole)
    && (permission.requiredPermissionKeys ?? []).every((key) => {
      const dependency = catalogByName.get(key);
      return Boolean(dependency && (canGrantPermissionToRole(dependency, currentRole) || permissionIds.has(dependency.id)));
    });
  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.areas.roles.title")}
        description={t("administration.areas.roles.description")}
        actions={<div className="flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" variant="outline" onClick={() => navigate("/administration/access")}><ArrowLeft className="me-2 h-4 w-4 rtl:-scale-x-100" />{t("administration.areas.access.backToAccessSecurity")}</Button>
          <Button type="button" onClick={beginCreate} disabled={!canCreate || isSaving}><Plus className="me-2 h-4 w-4" />{t("administration.areas.roles.newRole")}</Button>
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
                        <span className="flex items-start justify-between gap-2"><span className="min-w-0"><span className="block break-words text-sm font-semibold">{role.name}</span><span className="mt-1 block text-xs text-muted-foreground">{getRoleCategoryLabel(role.category, t)}</span></span>{role.isSystem ? <Badge variant="outline" className="shrink-0 bg-transparent text-info">{t("administration.areas.roles.systemRole")}</Badge> : null}</span>
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
                <div className="flex items-center gap-2">{currentRole?.isSystem ? <Badge variant="outline" className="bg-transparent text-info">{t("administration.areas.roles.systemRole")}</Badge> : null}<Button type="button" size="sm" variant="outline" onClick={beginEdit} disabled={!currentRole || (!canEditDetails && !canConfigurePermissions) || isSaving}>{t("common.edit")}</Button></div>
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              {roleDetails.isLoading || (roleDetails.isFetching && !roleDetails.data) ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                : roleDetails.isError ? <div className="flex flex-col items-center gap-3 py-8"><p role="alert" className="text-sm text-destructive">{t("administration.areas.roles.detailLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void roleDetails.refetch()}>{t("common.retry")}</Button></div>
                  : currentRole ? <>
                    <div className="grid gap-3 sm:grid-cols-2"><div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">{t("administration.areas.roles.category")}</p><p className="mt-1 text-sm font-medium">{getRoleCategoryLabel(currentRole.category, t)}</p></div><div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">{t("administration.areas.roles.assignedUserCount", { count: currentRole.assignedUserCount })}</p><p className="mt-1 text-sm font-medium">{t("administration.areas.roles.permissionCount", { count: currentRole.permissionCount })}</p></div></div>
                    {currentRole.isProtectedSystemAdmin ? <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.protectedAdminBaseline")}</p> : null}
                    {currentRole.isSystem && !currentRole.isProtectedSystemAdmin ? <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.systemRoleProtected")}</p> : null}
                    <section className="space-y-3" aria-labelledby="role-permissions-heading"><h3 id="role-permissions-heading" className="text-sm font-semibold">{t("administration.areas.roles.permissions")}</h3>
                      {currentRole.permissions.length === 0 ? <p className="rounded-md border p-4 text-sm text-muted-foreground">{t("administration.areas.roles.noRolePermissions")}</p>
                        : <div className="space-y-3">{permissionGroupsForRole(currentRole.permissions, locale).map(([screenId, group]) => <section key={screenId} className="space-y-2 rounded-lg border p-3"><h4 className="text-sm font-semibold">{getPermissionResourceLabel(group.screen, locale)}</h4><div className="flex flex-wrap gap-2">{[...group.views, ...group.actions].map((permission) => <Badge key={permission.id} variant={permission.isSensitive ? "destructive" : "secondary"}>{getPermissionDisplayName(permission, locale)}{getPermissionScopeLabel(permission, t) ? <span className="ms-1 border-s ps-1 text-xs opacity-75">{getPermissionScopeLabel(permission, t)}</span> : null}</Badge>)}</div></section>)}</div>}
                    </section>
                    {!currentRole.isSystem ? <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">{currentRole.assignedUserCount > 0 ? <span className="text-xs text-muted-foreground">{t("administration.areas.roles.deleteBlocked")}</span> : <span /> }<Button type="button" size="sm" variant="outline" className="border-destructive/40 text-destructive hover:border-destructive hover:text-destructive" onClick={() => void handleDelete()} disabled={!canDelete || isSaving || currentRole.assignedUserCount > 0} aria-label={t("administration.areas.roles.deleteRole")}><Trash2 className="me-1.5 h-4 w-4" />{t("administration.areas.roles.deleteRole")}</Button></div> : null}
                  </>
                  : null}
            </CardContent>
          </Card>}
      </div>
      <SideSheet open={formOpen} onOpenChange={(open) => { if (!open && !isSaving) cancelEditing(); }} title={isCreating ? t("administration.areas.roles.newRole") : t("administration.areas.roles.editRole")} description={isCreating ? t("administration.areas.roles.newRoleDescription") : t("administration.areas.roles.editRoleDescription")} size="xl" closeLabel={t("common.close")}>
        {draft ? <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void handleSave(event)}>
          <SideSheetBody className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2"><Label htmlFor="managed-role-name">{t("administration.areas.roles.name")}</Label><Input id="managed-role-name" value={draft.name} aria-invalid={Boolean(getApiFieldError(roleApiError, "name"))} aria-describedby={getApiFieldError(roleApiError, "name") ? "managed-role-name-error" : undefined} onChange={(event) => setDraft((current) => current ? { ...current, name: event.target.value } : current)} maxLength={100} required disabled={Boolean(currentRole?.isSystem || isSaving || (!isCreating && !canEditDetails))} /><ApiFieldError id="managed-role-name-error" error={roleApiError} fieldName="name" /></div>
              <div className="space-y-2">
                <Label htmlFor="managed-role-category">{t("administration.areas.roles.category")}</Label>
                {currentRole?.isSystem
                  ? <>
                    <div id="managed-role-category" aria-describedby="managed-role-category-help" className="flex h-10 items-center rounded-md border border-input bg-muted/30 px-3 text-sm text-muted-foreground" aria-readonly="true">{getRoleCategoryLabel(draft.category, t)}</div>
                    <p id="managed-role-category-help" className="text-xs text-muted-foreground">{t("administration.areas.roles.systemRoleCategoryReadOnly")}</p>
                  </>
                  : <>
                    <select id="managed-role-category" value={draft.category} required={isCreating} aria-invalid={Boolean(getApiFieldError(roleApiError, "category"))} aria-describedby={[getApiFieldError(roleApiError, "category") ? "managed-role-category-error" : null, persistedLegacyCategory ? "managed-role-category-legacy" : null].filter(Boolean).join(" ") || undefined} onChange={(event) => setDraft((current) => current ? { ...current, category: event.target.value } : current)} disabled={isSaving || (!isCreating && !canEditDetails)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50">
                      <option value="" disabled>{t("administration.areas.roles.selectCategory")}</option>
                      {persistedLegacyCategory ? <option value={persistedLegacyCategory}>{t("administration.areas.roles.legacyCategoryOption", { category: persistedLegacyCategory })}</option> : null}
                      {ROLE_CATEGORY_OPTIONS.map((category) => <option key={category} value={category}>{getRoleCategoryLabel(category, t)}</option>)}
                    </select>
                    {persistedLegacyCategory ? <p id="managed-role-category-legacy" className="text-xs text-muted-foreground">{t("administration.areas.roles.legacyCategoryMigrationHelp", { category: persistedLegacyCategory })}</p> : null}
                  </>}
                <ApiFieldError id="managed-role-category-error" error={roleApiError} fieldName="category" />
              </div>
              <div className="space-y-2 md:col-span-2"><Label htmlFor="managed-role-description">{t("administration.areas.roles.descriptionField")}</Label><textarea id="managed-role-description" value={draft.description} aria-invalid={Boolean(getApiFieldError(roleApiError, "description"))} aria-describedby={getApiFieldError(roleApiError, "description") ? "managed-role-description-error" : undefined} onChange={(event) => setDraft((current) => current ? { ...current, description: event.target.value } : current)} maxLength={500} disabled={isSaving || (!isCreating && !canEditDetails)} rows={2} className="flex min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50" /><ApiFieldError id="managed-role-description-error" error={roleApiError} fieldName="description" /></div>
            </div>
            {currentRole?.isProtectedSystemAdmin ? <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.protectedAdminBaseline")}</p> : null}
            {currentRole?.isSystem && !currentRole.isProtectedSystemAdmin ? <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.systemRoleProtected")}</p> : null}
            <section className="space-y-3 border-t pt-4" aria-labelledby="role-editor-permissions-heading">
              <div className="flex flex-wrap items-center justify-between gap-3"><div><div className="flex flex-wrap items-center gap-2"><h3 id="role-editor-permissions-heading" className="text-sm font-semibold">{t("administration.areas.roles.permissions")}</h3>{hasUnsavedChanges ? <Badge variant="outline" className="border-amber-500/40 bg-amber-500/10 text-amber-800 dark:text-amber-300">{t("administration.areas.roles.unsavedChanges")}</Badge> : null}</div><p className="mt-1 text-xs text-muted-foreground">{t("administration.areas.roles.permissionSummary", { selected: selectedCatalogPermissionCount, total: catalog.length })}</p></div><div className="w-full sm:max-w-xs"><Input value={permissionSearch} onChange={(event) => { setPermissionSearch(event.target.value); setSearchCollapsedScreens(new Set()); }} placeholder={t("administration.areas.roles.searchPermissions")} aria-label={t("administration.areas.roles.searchPermissions")} /></div></div>
              <fieldset className="grid gap-3 rounded-lg border bg-muted/10 p-3 sm:grid-cols-2 xl:grid-cols-4">
                <legend className="px-1 text-xs font-semibold text-muted-foreground">{t("administration.areas.roles.permissionFilters")}</legend>
                <div className="space-y-1.5"><Label htmlFor="role-permission-module-filter">{t("administration.areas.roles.moduleFilter")}</Label><select id="role-permission-module-filter" value={permissionModuleFilter} onChange={(event) => setPermissionModuleFilter(event.target.value)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><option value="all">{t("administration.areas.roles.allModules")}</option>{permissionModules.map(([moduleId, permission]) => <option key={moduleId} value={moduleId}>{getPermissionModuleLabel(permission, locale)}</option>)}</select></div>
                <div className="space-y-1.5"><Label htmlFor="role-permission-level-filter">{t("administration.areas.roles.permissionLevelFilter")}</Label><select id="role-permission-level-filter" value={permissionLevelFilter} onChange={(event) => setPermissionLevelFilter(event.target.value)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><option value="all">{t("administration.areas.roles.allLevels")}</option><option value="View">{t("administration.areas.roles.viewAccess")}</option><option value="Action">{t("administration.areas.roles.action")}</option></select></div>
                <div className="space-y-1.5"><Label htmlFor="role-permission-grant-filter">{t("administration.areas.roles.grantStatusFilter")}</Label><select id="role-permission-grant-filter" value={permissionGrantFilter} onChange={(event) => setPermissionGrantFilter(event.target.value)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><option value="all">{t("administration.areas.roles.allPermissions")}</option><option value="assigned">{t("administration.areas.roles.assigned")}</option><option value="unassigned">{t("administration.areas.roles.unassigned")}</option></select></div>
                <div className="space-y-1.5"><Label htmlFor="role-permission-sensitivity-filter">{t("administration.areas.roles.sensitivityFilter")}</Label><select id="role-permission-sensitivity-filter" value={permissionSensitivityFilter} onChange={(event) => setPermissionSensitivityFilter(event.target.value)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><option value="all">{t("administration.areas.roles.allSensitivities")}</option><option value="Low">{t("administration.areas.roles.sensitivityLow")}</option><option value="Medium">{t("administration.areas.roles.sensitivityMedium")}</option><option value="High">{t("administration.areas.roles.sensitivityHigh")}</option><option value="Critical">{t("administration.areas.roles.sensitivityCritical")}</option></select></div>
              </fieldset>
              <div role="status" aria-live="polite" className="rounded-md border bg-muted/20 p-3 text-sm">
                <p className="font-medium">{t("administration.areas.roles.pendingPermissionChanges")}</p>
                {pendingPermissionAdds.length === 0 && pendingPermissionRemovals.length === 0
                  ? <p className="mt-1 text-xs text-muted-foreground">{t("administration.areas.roles.noPendingPermissionChanges")}</p>
                  : <>
                    <p className="mt-1 text-xs text-muted-foreground">{t("administration.areas.roles.pendingPermissionSummary", { added: pendingPermissionAdds.length, removed: pendingPermissionRemovals.length })}</p>
                    <div className="mt-2 grid gap-2 sm:grid-cols-2">
                      {pendingPermissionAdds.length > 0 ? <div><p className="text-xs font-semibold">{t("administration.areas.roles.permissionsToAdd")}</p><ul className="mt-1 list-inside list-disc text-xs text-muted-foreground">{pendingPermissionAdds.slice(0, 4).map((permission) => <li key={permission.id} className="break-words">{getPermissionDisplayName(permission, locale)}</li>)}</ul>{pendingPermissionAdds.length > 4 ? <p className="mt-1 text-xs text-muted-foreground">{t("administration.areas.roles.additionalPermissionCount", { count: pendingPermissionAdds.length - 4 })}</p> : null}</div> : null}
                      {pendingPermissionRemovals.length > 0 ? <div><p className="text-xs font-semibold">{t("administration.areas.roles.permissionsToRemove")}</p><ul className="mt-1 list-inside list-disc text-xs text-muted-foreground">{pendingPermissionRemovals.slice(0, 4).map((permission) => <li key={permission.id} className="break-words">{getPermissionDisplayName(permission, locale)}</li>)}</ul>{pendingPermissionRemovals.length > 4 ? <p className="mt-1 text-xs text-muted-foreground">{t("administration.areas.roles.additionalPermissionCount", { count: pendingPermissionRemovals.length - 4 })}</p> : null}</div> : null}
                    </div>
                  </>}
              </div>
              <p className="text-xs text-muted-foreground">{t("administration.areas.roles.permissionDependencyHelp")}</p>
              {!canManage ? <p role="note" className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.readOnlyRoleAccess")}</p> : null}
              {canManage && !canConfigurePermissions ? <p role="note" className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">{t("administration.areas.roles.configurePermissionAccessRequired")}</p> : null}
              {permissionCatalog.data && !financialReadPermission ? <p role="note" className="text-sm text-muted-foreground">{t("administration.areas.roles.financialPermissionMissing", { permission: financeReadLabel })}</p> : null}
              {!canConfigurePermissions ? null : permissionCatalog.isLoading ? <p className="py-5 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                : permissionCatalog.isError ? <div className="flex flex-col items-center gap-3 py-5"><p role="alert" className="text-sm text-destructive">{t("administration.areas.roles.permissionCatalogError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void permissionCatalog.refetch()}>{t("common.retry")}</Button></div>
                  : permissionGroups.length === 0 ? <p className="py-5 text-center text-sm text-muted-foreground">{t("administration.areas.roles.noMatchingPermissions")}</p>
                    : <div className="space-y-3">{permissionGroups.map(([screenId, screen]) => {
                      const screenRows = [...screen.views, ...screen.actions];
                      const screenItems = screenRows.filter((entry) => !entry.contextOnly).map((entry) => entry.permission);
                      const screenSelected = screenItems.filter((permission) => permissionIds.has(permission.id)).length;
                      const screenAllSelected = screenItems.length > 0 && screenSelected === screenItems.length;
                      const screenCanToggle = screenAllSelected
                        ? screenItems.some((permission) => permissionIds.has(permission.id) && (!currentRole?.isProtectedSystemAdmin || !permission.isMandatoryForSystemAdmin))
                        : screenItems.some((permission) => !permissionIds.has(permission.id) && canGrantWithDependencies(permission));
                      const screenExpanded = permissionSearch.trim() ? !searchCollapsedScreens.has(screenId) : expandedScreens.has(screenId);
                      return <section key={screenId} className="rounded-lg border p-3">
                        <div className="flex items-center gap-3">
                          {screenItems.length > 0 ? <GroupCheckbox checked={screenAllSelected} indeterminate={screenSelected > 0 && screenSelected < screenItems.length} disabled={isSaving || !canConfigurePermissions || !screenCanToggle} label={t("administration.areas.roles.selectGroupPermissions", { group: getPermissionResourceLabel(screen.screen, locale) })} onChange={(checked) => void togglePermissionGroup(screenItems, checked)} /> : null}
                          <button type="button" className="flex min-w-0 flex-1 items-center justify-between gap-3 text-start" onClick={() => toggleScreen(screenId)} aria-expanded={screenExpanded} aria-controls={screenExpanded ? `role-permission-group-${screenId}` : undefined}>
                            <span className="truncate text-sm font-semibold">{getPermissionResourceLabel(screen.screen, locale)}</span><span className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground">{screenItems.length > 0 ? t("administration.areas.roles.selectionCount", { selected: screenSelected, total: screenItems.length }) : t("administration.areas.roles.requiredContext")}<ChevronDown className={`h-4 w-4 transition-transform ${screenExpanded ? "rotate-180" : ""}`} /></span>
                          </button>
                        </div>
                        {screenExpanded ? <div id={`role-permission-group-${screenId}`} className="mt-3 space-y-1 border-s ps-4">
                          {screenRows.map(({ permission, contextOnly }) => {
                            const selected = permissionIds.has(permission.id);
                            const unavailableDependency = (permission.requiredPermissionKeys ?? []).some((key) => {
                              const dependency = catalogByName.get(key);
                              return !dependency || (!canGrantPermissionToRole(dependency, currentRole) && !permissionIds.has(dependency.id));
                            });
                            const dependencyUnavailableForSystemRole = (permission.requiredPermissionKeys ?? []).some((key) => {
                              const dependency = catalogByName.get(key);
                              return Boolean(dependency && !permissionIds.has(dependency.id) && isBootstrapPermissionBlockedForRole(dependency, currentRole));
                            });
                            const unavailableForSystemRole = !selected && isBootstrapPermissionBlockedForRole(permission, currentRole);
                            const cannotGrant = !canGrantPermissionToRole(permission, currentRole) && !selected;
                            return <PermissionRow key={permission.id} permission={permission} locale={locale} selected={selected} disabled={isSaving || !canConfigurePermissions || (!selected && (cannotGrant || unavailableDependency || unavailableForSystemRole || dependencyUnavailableForSystemRole))} locked={Boolean(currentRole?.isProtectedSystemAdmin && permission.isMandatoryForSystemAdmin)} lockedLabel={t("administration.areas.roles.mandatoryAdminPermission")} viewLabel={permission.level === "View" ? t("administration.areas.roles.viewAccess") : t("administration.areas.roles.action")} sensitiveLabel={t("administration.areas.roles.sensitiveAction")} legacyLabel={t("administration.areas.roles.legacyCompatibilityPermission")} unavailableLabel={t("administration.areas.roles.permissionCannotBeDelegated")} dependencyUnavailableLabel={t("administration.areas.roles.permissionDependencyCannotBeDelegated")} systemRoleUnavailableLabel={t("administration.areas.roles.permissionUnavailableForSystemRole")} dependencyLabel={t("administration.areas.roles.requiresPermissions")} dependencyLabels={(permission.requiredPermissionKeys ?? []).map((key) => { const dependency = catalogByName.get(key); return dependency ? `${getPermissionResourceLabel(dependency, locale)}: ${getPermissionActionLabel(dependency, locale)}` : key; })} contextOnly={contextOnly} contextOnlyLabel={t("administration.areas.roles.requiredContext")} scopeLabel={getPermissionScopeLabel(permission, t)} cannotGrant={cannotGrant} dependencyCannotBeGranted={unavailableDependency} unavailableForSystemRole={unavailableForSystemRole} dependencyUnavailableForSystemRole={dependencyUnavailableForSystemRole} onChange={(checked) => void togglePermission(permission, checked)} />;
                          })}
                        </div> : null}
                      </section>;
                    })}</div>}
            </section>
            {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={cancelEditing} disabled={isSaving}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isSaving || (canConfigurePermissions && (permissionCatalog.isLoading || permissionCatalog.isError)) || (!isCreating && (roleDetails.isLoading || roleDetails.isError)) || (isCreating ? !canCreate : !canEditDetails && !canConfigurePermissions)}>{saveRole.isPending ? t("common.saving") : t("administration.areas.roles.saveRole")}</Button>
          </SideSheetFooter>
        </form> : null}
      </SideSheet>
    </div>
  );
}

function canGrantPermission(permission: PermissionCatalogItemDto, role: ManagedRoleDto | null | undefined): boolean {
  const canDelegate = role?.isSystem ? permission.canDelegate : permission.canDelegateToCustomRole;
  return canDelegate === true && permission.isAssignable && permission.isActive;
}

function isBootstrapPermissionBlockedForRole(permission: PermissionCatalogItemDto, role: ManagedRoleDto | null | undefined): boolean {
  if (!role?.isSystem || !BOOTSTRAP_PERMISSION_KEYS.has(permission.name)) return false;
  return !role.isProtectedSystemAdmin || !PROTECTED_ADMIN_BOOTSTRAP_PERMISSION_KEYS.has(permission.name);
}

function canGrantPermissionToRole(permission: PermissionCatalogItemDto, role: ManagedRoleDto | null | undefined): boolean {
  return canGrantPermission(permission, role) && !isBootstrapPermissionBlockedForRole(permission, role);
}

function getPermissionScopeLabel(permission: PermissionCatalogItemDto, t: (key: string) => string): string | undefined {
  const scope = permission.scope;
  if (!scope) return undefined;
  const normalized = scope.trim().toLowerCase();
  const label = t(`administration.areas.roles.scopes.${normalized}`);
  return label.startsWith("administration.areas.roles.scopes.") ? scope : label;
}

function permissionGroupsForRole(permissions: PermissionCatalogItemDto[], locale: "en" | "ar") {
  const grouped = new Map<string, { screen: PermissionCatalogItemDto; views: PermissionCatalogItemDto[]; actions: PermissionCatalogItemDto[] }>();
  for (const permission of permissions) {
    const screenId = `${permission.moduleId}:${permission.screenId}`;
    const screen = grouped.get(screenId) ?? { screen: permission, views: [], actions: [] };
    if (permission.level === "View") screen.views.push(permission);
    else screen.actions.push(permission);
    grouped.set(screenId, screen);
  }
  return [...grouped.entries()]
    .sort(([leftId, left], [rightId, right]) => getPermissionResourceLabel(left.screen, locale).localeCompare(getPermissionResourceLabel(right.screen, locale), locale, { sensitivity: "base" }) || leftId.localeCompare(rightId))
    .map(([screenId, screen]) => [screenId, {
      ...screen,
      views: [...screen.views].sort((left, right) => getPermissionActionLabel(left, locale).localeCompare(getPermissionActionLabel(right, locale), locale, { sensitivity: "base" }) || left.name.localeCompare(right.name)),
      actions: [...screen.actions].sort((left, right) => getPermissionActionLabel(left, locale).localeCompare(getPermissionActionLabel(right, locale), locale, { sensitivity: "base" }) || left.name.localeCompare(right.name)),
    }] as const);
}

function GroupCheckbox({
  checked,
  indeterminate,
  disabled,
  label,
  onChange,
}: {
  checked: boolean;
  indeterminate: boolean;
  disabled: boolean;
  label: string;
  onChange: (checked: boolean) => void;
}) {
  const inputRef = React.useRef<HTMLInputElement>(null);
  React.useEffect(() => {
    if (inputRef.current) inputRef.current.indeterminate = indeterminate;
  }, [indeterminate]);
  return <label className="inline-flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded hover:bg-accent/60" title={label}>
    <input ref={inputRef} type="checkbox" checked={checked} disabled={disabled} aria-label={label} onChange={(event) => onChange(event.target.checked)} className="h-4 w-4 accent-primary" />
  </label>;
}

function PermissionRow({
  permission,
  locale,
  selected,
  disabled,
  locked,
  lockedLabel,
  viewLabel,
  sensitiveLabel,
  legacyLabel,
  dependencyLabel,
  dependencyLabels,
  unavailableLabel,
  dependencyUnavailableLabel,
  systemRoleUnavailableLabel,
  cannotGrant,
  dependencyCannotBeGranted,
  unavailableForSystemRole,
  dependencyUnavailableForSystemRole,
  contextOnly,
  contextOnlyLabel,
  scopeLabel,
  onChange,
}: {
  permission: PermissionCatalogItemDto;
  locale: "en" | "ar";
  selected: boolean;
  disabled: boolean;
  locked: boolean;
  lockedLabel: string;
  viewLabel: string;
  sensitiveLabel: string;
  legacyLabel: string;
  dependencyLabel: string;
  dependencyLabels: string[];
  unavailableLabel: string;
  dependencyUnavailableLabel: string;
  systemRoleUnavailableLabel: string;
  cannotGrant: boolean;
  dependencyCannotBeGranted: boolean;
  unavailableForSystemRole: boolean;
  dependencyUnavailableForSystemRole: boolean;
  contextOnly: boolean;
  contextOnlyLabel: string;
  scopeLabel?: string;
  onChange: (checked: boolean) => void;
}) {
  const description = getPermissionDescription(permission, locale);
  const dependencyList = dependencyLabels.join(locale === "ar" ? "، " : ", ");
  const infoId = `role-permission-info-${permission.id}`;
  return <div className="space-y-1">
    <label htmlFor={`role-permission-${permission.id}`} className="flex min-h-11 cursor-pointer items-start gap-3 rounded-md px-2 py-2 text-sm hover:bg-accent/60 focus-within:ring-2 focus-within:ring-ring">
      <input id={`role-permission-${permission.id}`} type="checkbox" checked={selected} disabled={disabled || locked} aria-label={`${getPermissionResourceLabel(permission, locale)}: ${getPermissionActionLabel(permission, locale)} (${permission.name})`} aria-describedby={description || dependencyLabels.length > 0 || cannotGrant || dependencyCannotBeGranted || unavailableForSystemRole || dependencyUnavailableForSystemRole || locked ? infoId : undefined} onChange={(event) => onChange(event.target.checked)} className="mt-0.5 h-4 w-4 shrink-0 accent-primary" />
      <span id={infoId} className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{viewLabel}</span>
          <span>{getPermissionActionLabel(permission, locale)}</span>
          {permission.isSensitive ? <Badge variant="destructive" className="gap-1"><ShieldAlert className="h-3 w-3" />{sensitiveLabel}</Badge> : null}
          {locked ? <Badge variant="outline" className="gap-1"><LockKeyhole className="h-3 w-3" />{lockedLabel}</Badge> : null}
          {scopeLabel ? <Badge variant="outline">{scopeLabel}</Badge> : null}
          {contextOnly ? <Badge variant="outline">{contextOnlyLabel}</Badge> : null}
          {!permission.isAssignable ? <Badge variant="outline">{legacyLabel}</Badge> : null}
        </span>
        {description ? <span className="mt-1 block text-xs leading-5 text-muted-foreground">{description}</span> : null}
        {dependencyLabels.length > 0 ? <span className="mt-1 block text-xs text-primary">{dependencyLabel}: {dependencyList}</span> : null}
        {cannotGrant && !selected ? <span className="mt-1 block text-xs text-muted-foreground">{unavailableLabel}</span> : null}
        {dependencyCannotBeGranted && !selected ? <span className="mt-1 block text-xs text-muted-foreground">{dependencyUnavailableLabel}</span> : null}
        {(unavailableForSystemRole || dependencyUnavailableForSystemRole) && !selected ? <span className="mt-1 block text-xs text-muted-foreground">{systemRoleUnavailableLabel}</span> : null}
        <span dir="ltr" className="mt-1 block break-all text-start font-mono text-[10px] text-muted-foreground/75">{permission.name}</span>
      </span>
    </label>
  </div>;
}
