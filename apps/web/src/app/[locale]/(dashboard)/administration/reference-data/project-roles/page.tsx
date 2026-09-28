"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { ArrowDown, ArrowUp, Loader2, Pencil, Plus, Power } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useProjectRoles, useCreateProjectRole, useUpdateProjectRole, useDeactivateProjectRole, type ProjectRoleDto } from "@/hooks/resources/useResources";

type RoleForm = {
  code: string;
  nameEn: string;
  nameAr: string;
  description: string;
  displayOrder: number;
  isActive: boolean;
};

const EMPTY_FORM: RoleForm = { code: "", nameEn: "", nameAr: "", description: "", displayOrder: 10, isActive: true };

export default function ProjectRolesPage() {
  const locale = useLocale();
  const t = useTranslations("administration.projectRoles");
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Manage));
  const rolesQuery = useProjectRoles(false);
  const createMutation = useCreateProjectRole();
  const updateMutation = useUpdateProjectRole();
  const deactivateMutation = useDeactivateProjectRole();

  const [sheetOpen, setSheetOpen] = useState(false);
  const [editing, setEditing] = useState<ProjectRoleDto | null>(null);
  const [form, setForm] = useState<RoleForm>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);

  const roles = [...(rolesQuery.data ?? [])].sort((a, b) => a.displayOrder - b.displayOrder || a.code.localeCompare(b.code));
  const isSaving = createMutation.isPending || updateMutation.isPending;

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (roles.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setSheetOpen(true);
  }

  function openEdit(role: ProjectRoleDto) {
    setEditing(role);
    setForm({ code: role.code, nameEn: role.nameEn, nameAr: role.nameAr, description: role.description ?? "", displayOrder: role.displayOrder, isActive: role.isActive });
    setFormError(null);
    setSheetOpen(true);
  }

  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) { setEditing(null); setFormError(null); }
  }

  function updateForm<K extends keyof RoleForm>(key: K, value: RoleForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing && !form.code.trim()) return setFormError(t("codeRequired", { defaultValue: "Code is required" }));
    if (!form.nameEn.trim()) return setFormError(t("nameEnRequired", { defaultValue: "English name is required" }));

    setFormError(null);
    try {
      const body = { ...form, code: form.code.trim(), nameEn: form.nameEn.trim(), nameAr: form.nameAr.trim(), description: form.description.trim() || undefined };
      if (editing) await updateMutation.mutateAsync({ id: editing.id, body });
      else await createMutation.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      const message = error instanceof Error ? error.message.toLowerCase() : "";
      if (message.includes("already exists") || message.includes("duplicate")) setFormError(t("conflictError", { defaultValue: "A role with this code already exists" }));
      else setFormError(t("unknownError", { defaultValue: "An error occurred" }));
    }
  }

  async function toggleActive(role: ProjectRoleDto) {
    if (role.isActive && !window.confirm(t("deactivateConfirm", { defaultValue: "Deactivate this role?" }))) return;
    await deactivateMutation.mutateAsync(role.id);
  }

  async function moveOrder(role: ProjectRoleDto, direction: "up" | "down") {
    const idx = roles.findIndex(r => r.id === role.id);
    if (idx < 0) return;
    const swap = direction === "up" ? roles[idx - 1] : roles[idx + 1];
    if (!swap) return;
    await updateMutation.mutateAsync({ id: role.id, body: { ...role, displayOrder: swap.displayOrder } });
    await updateMutation.mutateAsync({ id: swap.id, body: { ...role, displayOrder: role.displayOrder } });
  }

  if (rolesQuery.isLoading) return <Card><CardContent className="p-6"><Skeleton className="h-40 w-full" /></CardContent></Card>;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("title", { defaultValue: "Project Roles" })}
        description={t("description", { defaultValue: "Manage functional project roles used for team assignments" })}
        actions={canManage ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("addRole", { defaultValue: "Add Role" })}</Button> : undefined}
      />
      <Card className="shadow-none">
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">{t("order", { defaultValue: "Order" })}</TableHead>
                <TableHead>{t("code", { defaultValue: "Code" })}</TableHead>
                <TableHead>{t("nameEn", { defaultValue: "Name (EN)" })}</TableHead>
                <TableHead>{t("nameAr", { defaultValue: "Name (AR)" })}</TableHead>
                <TableHead>{t("status", { defaultValue: "Status" })}</TableHead>
                {canManage && <TableHead className="text-end">{t("actions", { defaultValue: "Actions" })}</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {roles.map((role) => (
                <TableRow key={role.id} className={!role.isActive ? "opacity-60" : undefined}>
                  <TableCell className="text-muted-foreground">{role.displayOrder}</TableCell>
                  <TableCell className="font-mono text-xs">{role.code}</TableCell>
                  <TableCell>{role.nameEn}</TableCell>
                  <TableCell dir="rtl">{role.nameAr}</TableCell>
                  <TableCell>
                    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${role.isActive ? "bg-green-50 text-green-700 dark:bg-green-900/30 dark:text-green-400" : "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400"}`}>
                      {role.isActive ? t("active", { defaultValue: "Active" }) : t("inactive", { defaultValue: "Inactive" })}
                    </span>
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button size="icon" variant="ghost" onClick={() => openEdit(role)} aria-label={t("edit", { defaultValue: "Edit" })}><Pencil className="h-3.5 w-3.5" /></Button>
                        <Button size="icon" variant="ghost" onClick={() => void toggleActive(role)} aria-label={role.isActive ? t("deactivate", { defaultValue: "Deactivate" }) : t("activate", { defaultValue: "Activate" })}><Power className="h-3.5 w-3.5" /></Button>
                        <Button size="icon" variant="ghost" onClick={() => void moveOrder(role, "up")} disabled={roles.indexOf(role) === 0} aria-label={t("moveUp", { defaultValue: "Move up" })}><ArrowUp className="h-3.5 w-3.5" /></Button>
                        <Button size="icon" variant="ghost" onClick={() => void moveOrder(role, "down")} disabled={roles.indexOf(role) === roles.length - 1} aria-label={t("moveDown", { defaultValue: "Move down" })}><ArrowDown className="h-3.5 w-3.5" /></Button>
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))}
              {roles.length === 0 && (
                <TableRow><TableCell colSpan={canManage ? 6 : 5} className="text-center text-sm text-muted-foreground py-8">{t("empty", { defaultValue: "No project roles defined yet" })}</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <SideSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t("editRole", { defaultValue: "Edit Role" }) : t("createRole", { defaultValue: "Create Role" })} description={t("formDescription", { defaultValue: "Define a functional project role" })} size="md">
        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void save(event)}>
          <SideSheetBody className="space-y-4">
            {!editing && (
              <div className="space-y-1.5">
                <Label htmlFor="role-code">{t("code", { defaultValue: "Code" })}</Label>
                <Input id="role-code" value={form.code} onChange={(event) => updateForm("code", event.target.value)} placeholder="BackendDeveloper" required />
                <p className="text-xs text-muted-foreground">{t("codeHint", { defaultValue: "Unique code identifier" })}</p>
              </div>
            )}
            <div className="space-y-1.5">
              <Label htmlFor="role-name-en">{t("nameEn", { defaultValue: "Name (English)" })}</Label>
              <Input id="role-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="role-name-ar">{t("nameAr", { defaultValue: "Name (Arabic)" })}</Label>
              <Input id="role-name-ar" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} dir="rtl" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="role-desc">{t("descriptionLabel", { defaultValue: "Description" })}</Label>
              <Input id="role-desc" value={form.description} onChange={(event) => updateForm("description", event.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="role-order">{t("displayOrder", { defaultValue: "Display Order" })}</Label>
              <Input id="role-order" type="number" min="0" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} />
            </div>
            {formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("cancel", { defaultValue: "Cancel" })}</Button>
            <Button type="submit" disabled={isSaving}>{isSaving ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{t("save", { defaultValue: "Save" })}</Button>
          </SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
