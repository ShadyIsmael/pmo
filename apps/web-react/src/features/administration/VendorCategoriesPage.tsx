import * as React from "react";
import { ArrowDown, ArrowUp, Pencil, Plus, Power } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { getStatusTextClass } from "@lib/status-presentation";
import { hasPermission, Permissions } from "@lib/permissions";
import { useCreateVendorCategory, useUpdateVendorCategory, useVendorCategories, type SaveVendorCategoryInput, type VendorCategoryDto } from "./api/admin";

const EMPTY_FORM: SaveVendorCategoryInput = { code: "", nameEn: "", nameAr: "", description: "", displayOrder: 10, isActive: true };

function requestFor(category: VendorCategoryDto, changes: Partial<SaveVendorCategoryInput> = {}): SaveVendorCategoryInput {
  return { code: category.code, nameEn: category.nameEn, nameAr: category.nameAr, description: category.description, color: category.color, displayOrder: category.displayOrder, isActive: category.isActive, ...changes };
}

export function VendorCategoriesPage() {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write));
  const categoriesQuery = useVendorCategories(true);
  const create = useCreateVendorCategory();
  const update = useUpdateVendorCategory();
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<VendorCategoryDto | null>(null);
  const [form, setForm] = React.useState<SaveVendorCategoryInput>(EMPTY_FORM);
  const [formError, setFormError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [pendingAction, setPendingAction] = React.useState<string | null>(null);
  const categories = React.useMemo(() => [...(categoriesQuery.data ?? [])].sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)), [categoriesQuery.data]);
  const isSaving = create.isPending || update.isPending;

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (categories.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setSheetOpen(true);
  }
  function openEdit(category: VendorCategoryDto) {
    setEditing(category);
    setForm(requestFor(category));
    setFormError(null);
    setSheetOpen(true);
  }
  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) { setEditing(null); setFormError(null); }
  }
  function updateForm<K extends keyof SaveVendorCategoryInput>(key: K, value: SaveVendorCategoryInput[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }
  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing && !form.code.trim()) return setFormError(t("administration.vendorCategories.codeRequired"));
    if (!form.nameEn.trim()) return setFormError(t("administration.vendorCategories.nameEnRequired"));
    setFormError(null);
    const body: SaveVendorCategoryInput = { ...form, code: form.code.trim(), nameEn: form.nameEn.trim(), nameAr: form.nameAr.trim(), description: form.description?.trim() || undefined };
    try {
      if (editing) await update.mutateAsync({ id: editing.id, input: body });
      else await create.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      const message = error instanceof Error ? error.message.toLowerCase() : "";
      setFormError(message.includes("already exists") || message.includes("duplicate") ? t("administration.vendorCategories.conflictError") : t("administration.vendorCategories.unknownError"));
    }
  }
  async function updateCategory(category: VendorCategoryDto, changes: Partial<SaveVendorCategoryInput>, actionId: string) {
    setActionError(null);
    setPendingAction(actionId);
    try { await update.mutateAsync({ id: category.id, input: requestFor(category, changes) }); }
    catch (error) { setActionError(error instanceof Error ? error.message : t("administration.vendorCategories.unknownError")); }
    finally { setPendingAction(null); }
  }
  async function toggleActive(category: VendorCategoryDto) {
    const name = locale === "ar" ? category.nameAr : category.nameEn;
    if (category.isActive && !window.confirm(t("administration.vendorCategories.deactivateConfirm", { name }))) return;
    await updateCategory(category, { isActive: !category.isActive }, `active-${category.id}`);
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
      <PageHeader title={t("administration.vendorCategories.title")} description={t("administration.vendorCategories.description")} actions={canManage ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("administration.vendorCategories.add")}</Button> : undefined} />
      {categoriesQuery.isError ? <div role="alert" className="space-y-2 text-sm text-destructive"><p>{t("administration.vendorCategories.unknownError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void categoriesQuery.refetch()}>{t("administration.workItemStatuses.retry")}</Button></div> : (
        <Card className="shadow-none"><CardContent className="p-0">
          <Table>
            <TableHeader><TableRow><TableHead className="w-16">{t("administration.vendorCategories.order")}</TableHead><TableHead>{t("administration.vendorCategories.code")}</TableHead><TableHead>{t("administration.vendorCategories.nameEn")}</TableHead><TableHead>{t("administration.vendorCategories.nameAr")}</TableHead><TableHead>{t("administration.vendorCategories.status")}</TableHead>{canManage ? <TableHead className="text-end">{t("administration.vendorCategories.actions")}</TableHead> : null}</TableRow></TableHeader>
            <TableBody>
              {categories.map((category, index) => <TableRow key={category.id} className={!category.isActive ? "opacity-60" : undefined}>
                <TableCell className="text-muted-foreground">{category.displayOrder}</TableCell><TableCell className="font-mono text-xs">{category.code}</TableCell><TableCell>{category.nameEn}</TableCell><TableCell dir="rtl">{category.nameAr}</TableCell>
                <TableCell><span className={`text-xs font-medium ${getStatusTextClass(category.isActive ? "Active" : "Inactive")}`}>{category.isActive ? t("administration.vendorCategories.active") : t("administration.vendorCategories.inactive")}</span></TableCell>
                {canManage ? <TableCell><div className="flex justify-end gap-1">
                  <Button type="button" size="icon" variant="ghost" onClick={() => openEdit(category)} aria-label={t("administration.vendorCategories.edit")} disabled={Boolean(pendingAction)}><Pencil className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void toggleActive(category)} aria-label={category.isActive ? t("administration.vendorCategories.deactivate") : t("administration.vendorCategories.activate")} disabled={Boolean(pendingAction)}><Power className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void moveOrder(category, "up")} disabled={index === 0 || Boolean(pendingAction)} aria-label={t("administration.vendorCategories.moveUp")}><ArrowUp className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void moveOrder(category, "down")} disabled={index === categories.length - 1 || Boolean(pendingAction)} aria-label={t("administration.vendorCategories.moveDown")}><ArrowDown className="h-3.5 w-3.5" /></Button>
                </div></TableCell> : null}
              </TableRow>)}
              {categories.length === 0 ? <TableRow><TableCell colSpan={canManage ? 6 : 5} className="py-8 text-center text-sm text-muted-foreground">{t("administration.vendorCategories.empty")}</TableCell></TableRow> : null}
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
            <div className="space-y-1.5"><Label htmlFor="vendor-category-code">{t("administration.vendorCategories.code")}</Label><Input id="vendor-category-code" value={form.code} onChange={(event) => updateForm("code", event.target.value)} disabled={Boolean(editing)} required={!editing} /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-name-en">{t("administration.vendorCategories.nameEn")}</Label><Input id="vendor-category-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-name-ar">{t("administration.vendorCategories.nameAr")}</Label><Input id="vendor-category-name-ar" dir="rtl" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-description">{t("administration.vendorCategories.descriptionField")}</Label><Input id="vendor-category-description" value={form.description ?? ""} onChange={(event) => updateForm("description", event.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="vendor-category-order">{t("administration.vendorCategories.displayOrder")}</Label><Input id="vendor-category-order" type="number" min="0" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} /></div>
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("administration.vendorCategories.cancel")}</Button><Button type="submit" disabled={isSaving}>{isSaving ? t("administration.workItemStatuses.saving") : t("administration.vendorCategories.save")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
