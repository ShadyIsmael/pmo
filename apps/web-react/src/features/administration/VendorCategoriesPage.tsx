import * as React from "react";
import { ArrowDown, ArrowLeft, ArrowUp, Pencil, Plus, Power } from "lucide-react";
import { Link } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { ApiError, getApiFieldError } from "@api/client";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useAuth } from "@auth/AuthProvider";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useI18n } from "@i18n/messages";
import { getStatusTextClass } from "@lib/status-presentation";
import { hasPermission, Permissions } from "@lib/permissions";
import { useCreateVendorCategory, useDeactivateVendorCategory, useUpdateVendorCategory, useVendorCategories, type SaveVendorCategoryInput, type VendorCategoryDto } from "./api/admin";

const EMPTY_FORM: SaveVendorCategoryInput = { code: "", nameEn: "", nameAr: "", description: "", displayOrder: 10, isActive: true };

function requestFor(category: VendorCategoryDto, changes: Partial<SaveVendorCategoryInput> = {}): SaveVendorCategoryInput {
  return { code: category.code, nameEn: category.nameEn, nameAr: category.nameAr, description: category.description, color: category.color, displayOrder: category.displayOrder, isActive: category.isActive, ...changes };
}

export function VendorCategoriesPage() {
  const { t, locale } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.ReferenceData.Create));
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.ReferenceData.Edit));
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.ReferenceData.Delete));
  const categoriesQuery = useVendorCategories(true);
  const create = useCreateVendorCategory();
  const deactivate = useDeactivateVendorCategory();
  const update = useUpdateVendorCategory();
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<VendorCategoryDto | null>(null);
  const [form, setForm] = React.useState<SaveVendorCategoryInput>(EMPTY_FORM);
  const [formError, setFormError] = React.useState<string | null>(null);
  const [apiError, setApiError] = React.useState<unknown>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [pendingAction, setPendingAction] = React.useState<string | null>(null);
  const categories = React.useMemo(() => [...(categoriesQuery.data ?? [])].sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)), [categoriesQuery.data]);
  const isSaving = create.isPending || update.isPending;

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (categories.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setApiError(null);
    setSheetOpen(true);
  }
  function openEdit(category: VendorCategoryDto) {
    setEditing(category);
    setForm(requestFor(category));
    setFormError(null);
    setApiError(null);
    setSheetOpen(true);
  }
  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) { setEditing(null); setFormError(null); setApiError(null); }
  }
  function updateForm<K extends keyof SaveVendorCategoryInput>(key: K, value: SaveVendorCategoryInput[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }
  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (editing ? !canEdit : !canCreate) return;
    if (!editing && !form.code.trim()) return setFormError(t("administration.vendorCategories.codeRequired"));
    if (!form.nameEn.trim()) return setFormError(t("administration.vendorCategories.nameEnRequired"));
    setFormError(null);
    setApiError(null);
    const body: SaveVendorCategoryInput = { ...form, code: form.code.trim(), nameEn: form.nameEn.trim(), nameAr: form.nameAr.trim(), description: form.description?.trim() || undefined };
    try {
      if (editing) await update.mutateAsync({ id: editing.id, input: body });
      else await create.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      setApiError(error);
      const message = error instanceof Error ? error.message.toLowerCase() : "";
      setFormError(message.includes("already exists") || message.includes("duplicate") || (error instanceof ApiError && error.status === 409)
        ? t("administration.vendorCategories.conflictError")
        : error instanceof ApiError ? error.message : t("administration.vendorCategories.unknownError"));
    }
  }
  async function updateCategory(category: VendorCategoryDto, changes: Partial<SaveVendorCategoryInput>, actionId: string) {
    if (!canEdit) return;
    setActionError(null);
    setPendingAction(actionId);
    try { await update.mutateAsync({ id: category.id, input: requestFor(category, changes) }); }
    catch (error) { setActionError(error instanceof ApiError ? error.message : t("administration.vendorCategories.unknownError")); }
    finally { setPendingAction(null); }
  }
  async function toggleActive(category: VendorCategoryDto) {
    if (category.isActive) {
      const name = locale === "ar" ? category.nameAr : category.nameEn;
      if (!canDelete || !await confirm({ description: t("administration.vendorCategories.deactivateConfirm", { name }), destructive: true })) return;
      setActionError(null);
      setPendingAction(`active-${category.id}`);
      try { await deactivate.mutateAsync(category.id); }
      catch (error) { setActionError(error instanceof ApiError ? error.message : t("administration.vendorCategories.unknownError")); }
      finally { setPendingAction(null); }
      return;
    }
    if (canEdit) await updateCategory(category, { isActive: true }, `active-${category.id}`);
  }
  async function moveOrder(category: VendorCategoryDto, direction: "up" | "down") {
    const index = categories.findIndex((candidate) => candidate.id === category.id);
    const neighbor = direction === "up" ? categories[index - 1] : categories[index + 1];
    if (!neighbor) return;
    await updateCategory(category, { displayOrder: neighbor.displayOrder }, `order-${category.id}`);
    await updateCategory(neighbor, { displayOrder: category.displayOrder }, `order-${neighbor.id}`);
  }

  if (categoriesQuery.isLoading) return <Card><CardContent className="p-6"><p className="text-sm text-muted-foreground">{t("common.loading")}</p></CardContent></Card>;

  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.vendorCategories.title")} description={t("administration.vendorCategories.description")} actions={
        <div className="flex items-center gap-2">
          <Button asChild size="sm" variant="outline" className="whitespace-nowrap text-xs inline-flex items-center gap-1.5">
            <Link to="/administration/reference-data"><ArrowLeft className="me-2 h-4 w-4 rtl:rotate-180" />{t("administration.areas.referenceData.back")}</Link>
          </Button>
          {canCreate ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("administration.vendorCategories.add")}</Button> : null}
        </div>
      } />
      {categoriesQuery.isError ? <div role="alert" className="space-y-2 text-sm text-destructive"><p>{t("administration.vendorCategories.unknownError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void categoriesQuery.refetch()}>{t("administration.workItemStatuses.retry")}</Button></div> : (
        <Card className="shadow-none"><CardContent className="p-0">
          <Table>
            <TableHeader><TableRow><TableHead className="w-16">{t("administration.vendorCategories.order")}</TableHead><TableHead>{t("administration.vendorCategories.code")}</TableHead><TableHead>{t("administration.vendorCategories.nameEn")}</TableHead><TableHead>{t("administration.vendorCategories.nameAr")}</TableHead><TableHead>{t("administration.vendorCategories.status")}</TableHead>{canEdit || canDelete ? <TableHead className="text-end">{t("administration.vendorCategories.actions")}</TableHead> : null}</TableRow></TableHeader>
            <TableBody>
              {categories.map((category, index) => <TableRow key={category.id} className={!category.isActive ? "opacity-60" : undefined}>
                <TableCell className="text-muted-foreground">{category.displayOrder}</TableCell><TableCell className="font-mono text-xs">{category.code}</TableCell><TableCell>{category.nameEn}</TableCell><TableCell dir="rtl">{category.nameAr}</TableCell>
                <TableCell><span className={`text-xs font-medium ${getStatusTextClass(category.isActive ? "Active" : "Inactive")}`}>{category.isActive ? t("administration.vendorCategories.active") : t("administration.vendorCategories.inactive")}</span></TableCell>
                {canEdit || canDelete ? <TableCell><div className="flex justify-end gap-1">
                  {canEdit ? <><Button type="button" size="icon" variant="ghost" onClick={() => openEdit(category)} aria-label={t("administration.vendorCategories.edit")} disabled={Boolean(pendingAction)}><Pencil className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void moveOrder(category, "up")} disabled={index === 0 || Boolean(pendingAction)} aria-label={t("administration.vendorCategories.moveUp")}><ArrowUp className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void moveOrder(category, "down")} disabled={index === categories.length - 1 || Boolean(pendingAction)} aria-label={t("administration.vendorCategories.moveDown")}><ArrowDown className="h-3.5 w-3.5" /></Button></> : null}
                  {(category.isActive ? canDelete : canEdit) ? <Button type="button" size="icon" variant="ghost" onClick={() => void toggleActive(category)} aria-label={category.isActive ? t("administration.vendorCategories.deactivate") : t("administration.vendorCategories.activate")} disabled={Boolean(pendingAction)}><Power className="h-3.5 w-3.5" /></Button> : null}
                </div></TableCell> : null}
              </TableRow>)}
              {categories.length === 0 ? <TableRow><TableCell colSpan={canEdit || canDelete ? 6 : 5} className="py-8 text-center text-sm text-muted-foreground">{t("administration.vendorCategories.empty")}</TableCell></TableRow> : null}
            </TableBody>
          </Table>
          {actionError ? <p role="alert" className="border-t border-destructive/20 bg-destructive/5 px-5 py-3 text-sm text-destructive">{actionError}</p> : null}
          <p className="border-t bg-muted/20 px-5 py-3 text-xs text-muted-foreground">{t("administration.vendorCategories.noDelete")}</p>
        </CardContent></Card>
      )}
      <SideSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t("administration.vendorCategories.edit") : t("administration.vendorCategories.add")} description={t("administration.vendorCategories.formDescription")} size="md">
        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void save(event)}>
          <SideSheetBody className="space-y-4">
            {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
            <div className="space-y-1.5"><Label htmlFor="vendor-category-code">{t("administration.vendorCategories.code")}</Label><Input id="vendor-category-code" value={form.code} aria-invalid={Boolean(getApiFieldError(apiError, "code"))} aria-describedby={getApiFieldError(apiError, "code") ? "vendor-category-code-error" : undefined} onChange={(event) => updateForm("code", event.target.value)} disabled={Boolean(editing)} required={!editing} /><ApiFieldError id="vendor-category-code-error" error={apiError} fieldName="code" /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-name-en">{t("administration.vendorCategories.nameEn")}</Label><Input id="vendor-category-name-en" value={form.nameEn} aria-invalid={Boolean(getApiFieldError(apiError, "nameEn"))} aria-describedby={getApiFieldError(apiError, "nameEn") ? "vendor-category-name-en-error" : undefined} onChange={(event) => updateForm("nameEn", event.target.value)} required /><ApiFieldError id="vendor-category-name-en-error" error={apiError} fieldName="nameEn" /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-name-ar">{t("administration.vendorCategories.nameAr")}</Label><Input id="vendor-category-name-ar" dir="rtl" value={form.nameAr} aria-invalid={Boolean(getApiFieldError(apiError, "nameAr"))} aria-describedby={getApiFieldError(apiError, "nameAr") ? "vendor-category-name-ar-error" : undefined} onChange={(event) => updateForm("nameAr", event.target.value)} /><ApiFieldError id="vendor-category-name-ar-error" error={apiError} fieldName="nameAr" /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-description">{t("administration.vendorCategories.descriptionField")}</Label><Input id="vendor-category-description" value={form.description ?? ""} aria-invalid={Boolean(getApiFieldError(apiError, "description"))} aria-describedby={getApiFieldError(apiError, "description") ? "vendor-category-description-error" : undefined} onChange={(event) => updateForm("description", event.target.value)} /><ApiFieldError id="vendor-category-description-error" error={apiError} fieldName="description" /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-order">{t("administration.vendorCategories.displayOrder")}</Label><Input id="vendor-category-order" type="number" min="0" value={form.displayOrder} aria-invalid={Boolean(getApiFieldError(apiError, "displayOrder"))} aria-describedby={getApiFieldError(apiError, "displayOrder") ? "vendor-category-order-error" : undefined} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} /><ApiFieldError id="vendor-category-order-error" error={apiError} fieldName="displayOrder" /></div>
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("administration.vendorCategories.cancel")}</Button><Button type="submit" disabled={isSaving || (editing ? !canEdit : !canCreate)}>{isSaving ? t("administration.workItemStatuses.saving") : t("administration.vendorCategories.save")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
