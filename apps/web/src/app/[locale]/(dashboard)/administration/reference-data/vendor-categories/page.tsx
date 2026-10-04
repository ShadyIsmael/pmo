"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import Link from "next/link";
import { ArrowDown, ArrowUp, ListFilter, Loader2, Pencil, Plus, Power } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getStatusTextClass } from "@/lib/status-presentation";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import {
  useVendorCategories,
  useVendorCategoryMutations,
  type SaveVendorCategoryRequest,
  type VendorCategoryReference,
} from "@/lib/api/reference-data";

type CategoryForm = SaveVendorCategoryRequest;

const EMPTY_FORM: CategoryForm = { code: "", nameEn: "", nameAr: "", displayOrder: 10, isActive: true };

function requestFor(category: VendorCategoryReference, changes: Partial<CategoryForm> = {}): CategoryForm {
  return {
    code: category.code,
    nameEn: category.nameEn,
    nameAr: category.nameAr,
    displayOrder: category.displayOrder,
    isActive: category.isActive,
    description: category.description,
    color: category.color,
    ...changes,
  };
}

export default function VendorCategoriesPage() {
  const locale = useLocale();
  const t = useTranslations("administration.vendorCategories");
  const tw = useTranslations("administration.workItemStatuses");
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write));
  const categoriesQuery = useVendorCategories(false);
  const mutations = useVendorCategoryMutations();

  const [sheetOpen, setSheetOpen] = useState(false);
  const [editing, setEditing] = useState<VendorCategoryReference | null>(null);
  const [form, setForm] = useState<CategoryForm>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<string | null>(null);

  const categories = [...(categoriesQuery.data ?? [])].sort(
    (left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)
  );
  const isSaving = mutations.create.isPending || mutations.update.isPending;

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (categories.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setSheetOpen(true);
  }

  function openEdit(category: VendorCategoryReference) {
    setEditing(category);
    setForm(requestFor(category));
    setFormError(null);
    setSheetOpen(true);
  }

  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) {
      setEditing(null);
      setFormError(null);
    }
  }

  function updateForm<K extends keyof CategoryForm>(key: K, value: CategoryForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing && !form.code.trim()) return setFormError(t("codeRequired"));
    if (!form.nameEn.trim()) return setFormError(t("nameEnRequired"));

    setFormError(null);
    try {
      const body = {
        ...form,
        code: form.code.trim(),
        nameEn: form.nameEn.trim(),
        nameAr: form.nameAr.trim(),
        description: form.description?.trim() || undefined,
      };
      if (editing) await mutations.update.mutateAsync({ id: editing.id, body });
      else await mutations.create.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      const message = error instanceof Error ? error.message.toLowerCase() : "";
      if (message.includes("already exists") || message.includes("duplicate")) setFormError(t("conflictError"));
      else setFormError(t("unknownError"));
    }
  }

  async function updateCategory(category: VendorCategoryReference, changes: Partial<CategoryForm>, actionId: string) {
    setActionError(null);
    setPendingAction(actionId);
    try {
      await mutations.update.mutateAsync({ id: category.id, body: requestFor(category, changes) });
    } catch (error) {
      setActionError(error instanceof Error ? error.message : t("unknownError"));
    } finally {
      setPendingAction(null);
    }
  }

  async function toggleActive(category: VendorCategoryReference) {
    if (category.isActive && !window.confirm(t("deactivateConfirm", { name: locale === "ar" ? category.nameAr : category.nameEn }))) return;
    await updateCategory(category, { isActive: !category.isActive }, `active-${category.id}`);
  }

  async function moveOrder(category: VendorCategoryReference, direction: "up" | "down") {
    const index = categories.findIndex((candidate) => candidate.id === category.id);
    const swap = direction === "up" ? categories[index - 1] : categories[index + 1];
    if (!swap) return;
    await updateCategory(category, { displayOrder: swap.displayOrder }, `order-${category.id}`);
    await updateCategory(swap, { displayOrder: category.displayOrder }, `order-${swap.id}`);
  }

  if (categoriesQuery.isLoading) {
    return <Card><CardContent className="p-6"><Skeleton className="h-40 w-full" /></CardContent></Card>;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={canManage ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("add")}</Button> : undefined}
      />
      <div role="tablist" aria-label={tw("referenceDataTitle")} className="flex w-fit max-w-full flex-wrap gap-1 rounded-lg border bg-muted/20 p-1">
        <Link href={`/${locale}/administration/reference-data/work-item-statuses`} role="tab" aria-selected="false" className="rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground">
          <ListFilter className="me-1.5 inline h-3.5 w-3.5" />{tw("tabs.workItemStatuses")}
        </Link>
        <Link href={`/${locale}/administration/currencies`} role="tab" aria-selected="false" className="rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground">
          {tw("tabs.currencies")}
        </Link>
        <Link href={`/${locale}/administration/reference-data/vendor-categories`} role="tab" aria-selected="true" className="rounded-md bg-background px-3 py-1.5 text-sm font-medium text-foreground shadow-sm">
          {tw("tabs.vendorCategories")}
        </Link>
      </div>
      <Card className="shadow-none">
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-16">{t("order")}</TableHead>
                <TableHead>{t("code")}</TableHead>
                <TableHead>{t("nameEn")}</TableHead>
                <TableHead>{t("nameAr")}</TableHead>
                <TableHead>{t("status")}</TableHead>
                {canManage && <TableHead className="text-end">{t("actions")}</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {categories.map((category) => (
                <TableRow key={category.id} className={!category.isActive ? "opacity-60" : undefined}>
                  <TableCell className="text-muted-foreground">{category.displayOrder}</TableCell>
                  <TableCell className="font-mono text-xs">{category.code}</TableCell>
                  <TableCell>{category.nameEn}</TableCell>
                  <TableCell dir="rtl">{category.nameAr}</TableCell>
                  <TableCell>
                    <span className={`text-xs font-medium ${getStatusTextClass(category.isActive ? "Active" : "Inactive")}`}>
                      {category.isActive ? t("active") : t("inactive")}
                    </span>
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button size="icon" variant="ghost" onClick={() => openEdit(category)} aria-label={t("edit")} disabled={Boolean(pendingAction)}>
                          <Pencil className="h-3.5 w-3.5" />
                        </Button>
                        <Button size="icon" variant="ghost" onClick={() => void toggleActive(category)} aria-label={category.isActive ? t("deactivate") : t("activate")} disabled={Boolean(pendingAction)}>
                          <Power className="h-3.5 w-3.5" />
                        </Button>
                        <Button size="icon" variant="ghost" onClick={() => void moveOrder(category, "up")} disabled={categories.indexOf(category) === 0 || Boolean(pendingAction)} aria-label={t("moveUp")}>
                          <ArrowUp className="h-3.5 w-3.5" />
                        </Button>
                        <Button size="icon" variant="ghost" onClick={() => void moveOrder(category, "down")} disabled={categories.indexOf(category) === categories.length - 1 || Boolean(pendingAction)} aria-label={t("moveDown")}>
                          <ArrowDown className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))}
              {categories.length === 0 && (
                <TableRow>
                  <TableCell colSpan={canManage ? 6 : 5} className="py-8 text-center text-sm text-muted-foreground">
                    {t("empty")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
          {actionError ? (
            <p role="alert" className="border-t border-destructive/20 bg-destructive/5 px-5 py-3 text-sm text-destructive">{actionError}</p>
          ) : null}
          <p className="border-t bg-muted/20 px-5 py-3 text-xs text-muted-foreground">{t("noDelete")}</p>
        </CardContent>
      </Card>

      <SideSheet
        open={sheetOpen}
        onOpenChange={closeSheet}
        title={editing ? t("edit") : t("add")}
        description={t("formDescription")}
        size="md"
      >
        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void save(event)}>
          <SideSheetBody className="space-y-4">
            {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
            <div className="space-y-1.5">
              <Label htmlFor="vendor-category-code">{t("code")}</Label>
              <Input
                id="vendor-category-code"
                value={form.code}
                onChange={(event) => updateForm("code", event.target.value)}
                disabled={Boolean(editing)}
                required={!editing}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="vendor-category-name-en">{t("nameEn")}</Label>
              <Input id="vendor-category-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="vendor-category-name-ar">{t("nameAr")}</Label>
              <Input id="vendor-category-name-ar" dir="rtl" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="vendor-category-description">{t("descriptionField")}</Label>
              <Input id="vendor-category-description" value={form.description ?? ""} onChange={(event) => updateForm("description", event.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="vendor-category-order">{t("displayOrder")}</Label>
              <Input id="vendor-category-order" type="number" min="0" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} />
            </div>
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("cancel")}</Button>
            <Button type="submit" disabled={isSaving}>
              {isSaving ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}
              {t("save")}
            </Button>
          </SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
