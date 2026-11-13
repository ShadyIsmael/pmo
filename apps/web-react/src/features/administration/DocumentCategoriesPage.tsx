import * as React from "react";
import { Link } from "react-router-dom";
import { ArrowLeft, Building2, Pencil, Plus, Power, Search } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { ApiError } from "@api/client";
import { useManagement } from "@api/management";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Skeleton } from "@components/ui/Skeleton";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import {
  useCreateDocumentCategory,
  useManagedDocumentCategories,
  useSetDocumentCategoryStatus,
  useUpdateDocumentCategory,
  type DocumentCategory,
  type SaveDocumentCategoryInput,
} from "@features/projects/api/documents";

function categoryDisplayName(category: DocumentCategory, locale: string) {
  return locale === "ar"
    ? category.nameAr || category.name || category.code || ""
    : category.nameEn || category.name || category.code || "";
}

export function DocumentCategoriesPage() {
  const { t, locale, formatNumber } = useI18n();
  const { user } = useAuth();
  const { snapshot, canMutate, loading: managementLoading } = useManagement();
  const confirm = useConfirm();
  const [organizationId, setOrganizationId] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [formOpen, setFormOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<DocumentCategory | null>(null);
  const [nameEn, setNameEn] = React.useState("");
  const [nameAr, setNameAr] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [formError, setFormError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [pendingStatusId, setPendingStatusId] = React.useState<string | null>(null);

  const canCreate = Boolean(canMutate && user && hasPermission(user.permissions, Permissions.DocumentCategory.Create));
  const canEdit = Boolean(canMutate && user && hasPermission(user.permissions, Permissions.DocumentCategory.Edit));
  const canChangeStatus = Boolean(canMutate && user && hasPermission(user.permissions, Permissions.DocumentCategory.ActivateDeactivate));
  const scope = snapshot?.selectedContext ? snapshot.selectedContext.scope : snapshot?.scope;
  const organizations = React.useMemo(() => {
    if (!scope) return [];
    const allowedIds = new Set(scope.organizationIds ?? []);
    return (scope.organizationReferences ?? []).filter((organization) => allowedIds.has(organization.id));
  }, [scope]);
  const organizationIsAllowed = organizations.some((organization) => organization.id === organizationId);

  React.useEffect(() => {
    setOrganizationId((current) => {
      if (organizations.some((organization) => organization.id === current)) return current;
      return organizations.length === 1 ? organizations[0].id : "";
    });
  }, [organizations]);

  const query = useManagedDocumentCategories({
    organizationId: organizationIsAllowed ? organizationId : undefined,
    search: search.trim() || undefined,
    includeInactive: true,
  }, organizationIsAllowed);
  const create = useCreateDocumentCategory();
  const update = useUpdateDocumentCategory();
  const setStatus = useSetDocumentCategoryStatus();
  const saving = create.isPending || update.isPending;

  const openCreate = () => {
    setEditing(null);
    setNameEn("");
    setNameAr("");
    setDescription("");
    setFormError(null);
    setActionError(null);
    setFormOpen(true);
  };
  const openEdit = (category: DocumentCategory) => {
    if (!canEdit || category.canManage === false) return;
    setEditing(category);
    setNameEn(category.nameEn || category.name || "");
    setNameAr(category.nameAr || "");
    setDescription(category.description || "");
    setFormError(null);
    setActionError(null);
    setFormOpen(true);
  };
  const closeForm = (open: boolean) => {
    setFormOpen(open);
    if (!open) {
      setEditing(null);
      setFormError(null);
    }
  };
  const saveCategory = async (event?: React.FormEvent<HTMLFormElement>) => {
    event?.preventDefault();
    if (!organizationIsAllowed || (editing ? !canEdit : !canCreate)) {
      setFormError(t("documents.categorySaveFailed"));
      setActionError(t("documents.categorySaveFailed"));
      return;
    }
    if (!nameEn.trim() || !nameAr.trim()) {
      setFormError(t("documents.categoryNamesRequired"));
      return;
    }
    setFormError(null);
    setActionError(null);
    const input: SaveDocumentCategoryInput = {
      organizationId,
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      description: description.trim() || undefined,
    };
    try {
      if (editing) {
        await update.mutateAsync({
          id: editing.id,
          input: { nameEn: input.nameEn, nameAr: input.nameAr, description: input.description },
        });
      } else {
        await create.mutateAsync(input);
      }
      closeForm(false);
    } catch (error) {
      const duplicate = (error instanceof ApiError && error.status === 409)
        || (error instanceof Error && /duplicate|already exists/i.test(error.message));
      setFormError(duplicate
        ? t("documents.categoryDuplicate")
        : error instanceof ApiError ? error.message : t("documents.categorySaveFailed"));
    }
  };
  const toggleStatus = async (category: DocumentCategory) => {
    if (!canChangeStatus || category.canManage === false || !organizationIsAllowed) return;
    const active = category.isActive !== false;
    if (active) {
      const accepted = await confirm({
        title: t("documents.categoryDeactivate"),
        description: t("documents.categoryDeactivateConfirm", { name: categoryDisplayName(category, locale) }),
        confirmLabel: t("documents.categoryDeactivate"),
        destructive: true,
      });
      if (!accepted) return;
    }
    setActionError(null);
    setPendingStatusId(category.id);
    try {
      await setStatus.mutateAsync({ id: category.id, isActive: !active });
    } catch (error) {
      setActionError(error instanceof ApiError ? error.message : t("documents.categoryStatusFailed"));
    } finally {
      setPendingStatusId(null);
    }
  };

  const loadingOrganizationScope = managementLoading && !scope;
  const categoryLoading = query.isLoading;
  const visibleCategories = query.data ?? [];

  return <div className="space-y-5">
    <PageHeader
      title={t("documents.categoriesTitle")}
      description={t("documents.categoryManagementDescription")}
      actions={<div className="flex flex-wrap items-center gap-2">
        <Button asChild size="sm" variant="outline">
          <Link to="/administration/reference-data"><ArrowLeft className="me-2 h-4 w-4 rtl:rotate-180" />{t("administration.areas.referenceData.back")}</Link>
        </Button>
        {canCreate && organizationIsAllowed ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("documents.categoryCreate")}</Button> : null}
      </div>}
    />

    <Card className="shadow-none">
      <CardHeader className="gap-4 sm:flex-row sm:items-end sm:justify-between">
        <CardTitle className="text-base">{t("documents.categoriesTitle")}</CardTitle>
        <div className="grid w-full gap-3 sm:max-w-2xl sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="document-category-organization">{t("documents.fields.organization")}</Label>
            <select
              id="document-category-organization"
              aria-label={t("documents.fields.organization")}
              value={organizationId}
              onChange={(event) => setOrganizationId(event.target.value)}
              disabled={loadingOrganizationScope || !organizations.length}
              className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm"
            >
              <option value="">{organizations.length ? t("documents.organizationSelect") : t("documents.organizationsUnavailable")}</option>
              {organizations.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}
            </select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="document-category-search">{t("documents.categorySearch")}</Label>
            <div className="relative">
              <Search aria-hidden="true" className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input id="document-category-search" className="ps-8" value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("documents.categorySearch")} disabled={!organizationIsAllowed} />
            </div>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {loadingOrganizationScope ? <div className="space-y-2"><Skeleton className="h-12" /><Skeleton className="h-12" /></div>
          : !organizations.length ? <EmptyState icon={<Building2 className="h-9 w-9" />} title={t("documents.organizationsUnavailable")} description={t("documents.organizationsUnavailableDescription")} />
            : !organizationIsAllowed ? <EmptyState icon={<Building2 className="h-9 w-9" />} title={t("documents.categoryOrganizationRequired")} description={t("documents.categoryOrganizationRequiredDescription")} />
              : categoryLoading ? <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3"><Skeleton className="h-36" /><Skeleton className="h-36" /><Skeleton className="h-36" /></div>
                : query.isError ? <ErrorState title={t("documents.categoryLoadError")} onRetry={() => void query.refetch()} />
                  : !visibleCategories.length ? <EmptyState title={t(search.trim() ? "documents.categorySearchEmptyTitle" : "documents.categoriesEmptyTitle")} description={t(search.trim() ? "documents.categorySearchEmptyDescription" : "documents.categoryManagementEmptyDescription")} action={canCreate ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("documents.categoryCreate")}</Button> : null} />
                    : <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                      {visibleCategories.map((category) => {
                        const manageable = category.canManage !== false;
                        const active = category.isActive !== false;
                        const displayName = categoryDisplayName(category, locale);
                        const secondaryName = locale === "ar" ? category.nameEn : category.nameAr;
                        return <article key={category.id} className={`rounded-lg border border-border bg-background p-4 ${!active ? "opacity-75" : ""}`}>
                          <div className="flex items-start justify-between gap-3">
                            <div className="min-w-0"><h3 className="truncate text-sm font-semibold">{displayName}</h3>{secondaryName ? <p dir={locale === "ar" ? "ltr" : "rtl"} className="mt-0.5 truncate text-xs text-muted-foreground">{secondaryName}</p> : null}</div>
                            <span className={`shrink-0 rounded-full border px-2 py-0.5 text-xs ${active ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300" : "border-muted-foreground/30 bg-muted text-muted-foreground"}`}>{active ? t("documents.status.active") : t("documents.categoryInactive")}</span>
                          </div>
                          {category.description ? <p className="mt-3 line-clamp-2 text-sm text-muted-foreground">{category.description}</p> : null}
                          <div className="mt-4 flex items-center justify-between gap-3 border-t border-border pt-3">
                            <span className="text-xs text-muted-foreground">{t("documents.categoryUsage", { count: formatNumber(category.usageCount ?? 0) })}</span>
                            {manageable && (canEdit || canChangeStatus) ? <div className="flex gap-1">
                              {canEdit ? <Button type="button" size="icon" variant="ghost" aria-label={`${t("documents.categoryEdit")} ${displayName}`} onClick={() => openEdit(category)} disabled={saving || pendingStatusId !== null}><Pencil className="h-3.5 w-3.5" /></Button> : null}
                              {canChangeStatus ? <Button type="button" size="icon" variant="ghost" aria-label={`${active ? t("documents.categoryDeactivate") : t("documents.categoryActivate")} ${displayName}`} onClick={() => void toggleStatus(category)} disabled={pendingStatusId !== null || saving}><Power className="h-3.5 w-3.5" /></Button> : null}
                            </div> : null}
                          </div>
                        </article>;
                      })}
                    </div>}
        {actionError ? <p role="alert" className="mt-4 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p> : null}
      </CardContent>
    </Card>

    <SideSheet open={formOpen} onOpenChange={closeForm} title={editing ? t("documents.categoryEdit") : t("documents.categoryCreate")} description={t("documents.categoryFormDescription")} size="md">
      <form id="document-category-form" className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void saveCategory(event)}>
        <SideSheetBody className="space-y-4">
          {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
          <div className="space-y-1.5"><Label htmlFor="document-category-name-en">{t("documents.categoryNameEn")}</Label><Input id="document-category-name-en" value={nameEn} onChange={(event) => setNameEn(event.target.value)} required maxLength={150} /></div>
          <div className="space-y-1.5"><Label htmlFor="document-category-name-ar">{t("documents.categoryNameAr")}</Label><Input id="document-category-name-ar" dir="rtl" value={nameAr} onChange={(event) => setNameAr(event.target.value)} required maxLength={150} /></div>
          <div className="space-y-1.5"><Label htmlFor="document-category-description">{t("documents.fields.description")}</Label><Input id="document-category-description" value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} /></div>
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeForm(false)} disabled={saving}>{t("documents.cancel")}</Button><Button type="submit" form="document-category-form" onClick={(event) => { event.preventDefault(); void saveCategory(); }} disabled={saving || (editing ? !canEdit : !canCreate)}>{saving ? t("documents.saving") : t("documents.categorySave")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  </div>;
}
