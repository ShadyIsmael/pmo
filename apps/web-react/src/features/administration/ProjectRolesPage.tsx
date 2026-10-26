import * as React from "react";
import { ArrowDown, ArrowUp, Loader2, Pencil, Plus, Power } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useAuth } from "@auth/AuthProvider";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useI18n } from "@i18n/messages";
import { getStatusTextClass } from "@lib/status-presentation";
import { hasPermission, Permissions } from "@lib/permissions";
import {
  useCreateProjectRole,
  useDeactivateProjectRole,
  useProjectRoleNextCode,
  useProjectRoles,
  useUpdateProjectRole,
  type ProjectRoleDto,
} from "@features/resources/api/resources";

type RoleForm = {
  code: string;
  nameEn: string;
  nameAr: string;
  description: string;
  displayOrder: number;
  isActive: boolean;
};

const EMPTY_FORM: RoleForm = { code: "", nameEn: "", nameAr: "", description: "", displayOrder: 10, isActive: true };

export function ProjectRolesPage() {
  const { t } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Manage));
  const rolesQuery = useProjectRoles(false, true);
  const createMutation = useCreateProjectRole();
  const updateMutation = useUpdateProjectRole();
  const deactivateMutation = useDeactivateProjectRole();

  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<ProjectRoleDto | null>(null);
  const [form, setForm] = React.useState<RoleForm>(EMPTY_FORM);
  const [formError, setFormError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [codeTouched, setCodeTouched] = React.useState(false);
  const [createOpenCount, setCreateOpenCount] = React.useState(0);
  const [pendingAction, setPendingAction] = React.useState<string | null>(null);

  const generatedCodeQuery = useProjectRoleNextCode(sheetOpen && editing === null, createOpenCount);
  const roles = React.useMemo(
    () => [...(rolesQuery.data ?? [])].sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)),
    [rolesQuery.data],
  );
  const isSaving = createMutation.isPending || updateMutation.isPending;

  React.useEffect(() => {
    const nextCode = generatedCodeQuery.data?.code;
    if (!nextCode || codeTouched) return;
    setForm((current) => current.code === nextCode ? current : { ...current, code: nextCode });
  }, [generatedCodeQuery.data?.code, codeTouched]);

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, displayOrder: (roles.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setActionError(null);
    setCodeTouched(false);
    setCreateOpenCount((count) => count + 1);
    setSheetOpen(true);
  }

  function openEdit(role: ProjectRoleDto) {
    setEditing(role);
    setForm({ code: role.code, nameEn: role.nameEn, nameAr: role.nameAr, description: role.description ?? "", displayOrder: role.displayOrder, isActive: role.isActive });
    setFormError(null);
    setActionError(null);
    setCodeTouched(true);
    setSheetOpen(true);
  }

  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) {
      setEditing(null);
      setFormError(null);
    }
  }

  function updateForm<K extends keyof RoleForm>(key: K, value: RoleForm[K]) {
    if (key === "code") setCodeTouched(true);
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing && codeTouched && !form.code.trim()) return setFormError(t("administration.projectRoles.codeRequired"));
    if (!form.nameEn.trim()) return setFormError(t("administration.projectRoles.nameEnRequired"));

    setFormError(null);
    const { code, ...rest } = form;
    const body = {
      ...rest,
      ...(codeTouched ? { code: code.trim() } : {}),
      nameEn: form.nameEn.trim(),
      nameAr: form.nameAr.trim(),
      description: form.description.trim() || undefined,
    };
    try {
      if (editing) await updateMutation.mutateAsync({ id: editing.id, body });
      else await createMutation.mutateAsync(body);
      closeSheet(false);
    } catch (error) {
      const message = error instanceof Error ? error.message.toLowerCase() : "";
      setFormError(message.includes("already exists") || message.includes("duplicate")
        ? t("administration.projectRoles.conflictError")
        : t("administration.projectRoles.unknownError"));
    }
  }

  async function toggleActive(role: ProjectRoleDto) {
    if (role.isActive && !await confirm({ description: t("administration.projectRoles.deactivateConfirm"), destructive: true })) return;
    const actionId = `active-${role.id}`;
    setActionError(null);
    setPendingAction(actionId);
    try {
      if (role.isActive) await deactivateMutation.mutateAsync(role.id);
      else await updateMutation.mutateAsync({ id: role.id, body: { ...role, isActive: true } });
    } catch {
      setActionError(t("administration.projectRoles.unknownError"));
    } finally {
      setPendingAction(null);
    }
  }

  async function moveOrder(role: ProjectRoleDto, direction: "up" | "down") {
    const index = roles.findIndex((candidate) => candidate.id === role.id);
    const neighbor = direction === "up" ? roles[index - 1] : roles[index + 1];
    if (!neighbor) return;
    setActionError(null);
    setPendingAction(`order-${role.id}`);
    try {
      await updateMutation.mutateAsync({ id: role.id, body: { ...role, displayOrder: neighbor.displayOrder } });
      await updateMutation.mutateAsync({ id: neighbor.id, body: { ...neighbor, displayOrder: role.displayOrder } });
    } catch {
      setActionError(t("administration.projectRoles.unknownError"));
    } finally {
      setPendingAction(null);
    }
  }

  if (rolesQuery.isLoading) return <Card><CardContent className="p-6"><p className="text-sm text-muted-foreground">{t("common.loading")}</p></CardContent></Card>;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.projectRoles.title")}
        description={t("administration.projectRoles.description")}
        actions={canManage ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("administration.projectRoles.addRole")}</Button> : undefined}
      />
      {rolesQuery.isError ? (
        <div role="alert" className="space-y-2 text-sm text-destructive">
          <p>{t("administration.projectRoles.unknownError")}</p>
          <Button type="button" size="sm" variant="outline" onClick={() => void rolesQuery.refetch()}>{t("administration.workItemStatuses.retry")}</Button>
        </div>
      ) : (
        <Card className="shadow-none"><CardContent className="p-0">
          <Table>
            <TableHeader><TableRow>
              <TableHead className="w-12">{t("administration.projectRoles.order")}</TableHead>
              <TableHead>{t("administration.projectRoles.code")}</TableHead>
              <TableHead>{t("administration.projectRoles.nameEn")}</TableHead>
              <TableHead>{t("administration.projectRoles.nameAr")}</TableHead>
              <TableHead>{t("administration.projectRoles.status")}</TableHead>
              {canManage ? <TableHead className="text-end">{t("administration.projectRoles.actions")}</TableHead> : null}
            </TableRow></TableHeader>
            <TableBody>
              {roles.map((role, index) => <TableRow key={role.id} className={!role.isActive ? "opacity-60" : undefined}>
                <TableCell className="text-muted-foreground">{role.displayOrder}</TableCell>
                <TableCell className="font-mono text-xs">{role.code}</TableCell>
                <TableCell>{role.nameEn}</TableCell>
                <TableCell dir="rtl">{role.nameAr}</TableCell>
                <TableCell><span className={`text-xs font-medium ${getStatusTextClass(role.isActive ? "Active" : "Inactive")}`}>{role.isActive ? t("administration.projectRoles.active") : t("administration.projectRoles.inactive")}</span></TableCell>
                {canManage ? <TableCell><div className="flex justify-end gap-1">
                  <Button type="button" size="icon" variant="ghost" onClick={() => openEdit(role)} aria-label={t("administration.projectRoles.edit")} disabled={Boolean(pendingAction)}><Pencil className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void toggleActive(role)} aria-label={role.isActive ? t("administration.projectRoles.deactivate") : t("administration.projectRoles.activate")} disabled={Boolean(pendingAction)}><Power className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void moveOrder(role, "up")} disabled={index === 0 || Boolean(pendingAction)} aria-label={t("administration.projectRoles.moveUp")}><ArrowUp className="h-3.5 w-3.5" /></Button>
                  <Button type="button" size="icon" variant="ghost" onClick={() => void moveOrder(role, "down")} disabled={index === roles.length - 1 || Boolean(pendingAction)} aria-label={t("administration.projectRoles.moveDown")}><ArrowDown className="h-3.5 w-3.5" /></Button>
                </div></TableCell> : null}
              </TableRow>)}
              {roles.length === 0 ? <TableRow><TableCell colSpan={canManage ? 6 : 5} className="py-8 text-center text-sm text-muted-foreground">{t("administration.projectRoles.empty")}</TableCell></TableRow> : null}
            </TableBody>
          </Table>
          {actionError ? <p role="alert" className="border-t border-destructive/20 bg-destructive/5 px-5 py-3 text-sm text-destructive">{actionError}</p> : null}
        </CardContent></Card>
      )}

      <SideSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t("administration.projectRoles.editRole") : t("administration.projectRoles.createRole")} description={t("administration.projectRoles.formDescription")} size="md">
        <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void save(event)}>
          <SideSheetBody className="space-y-4">
            {!editing ? <div className="space-y-1.5">
              <Label htmlFor="project-role-code">{t("administration.projectRoles.code")}</Label>
              <Input id="project-role-code" className="font-mono" value={form.code} onChange={(event) => updateForm("code", event.target.value)} placeholder={t("administration.projectRoles.codePlaceholder")} aria-describedby="project-role-code-hint" />
              <p id="project-role-code-hint" className="text-xs text-muted-foreground">{generatedCodeQuery.isLoading ? t("administration.projectRoles.codeGenerating") : t("administration.projectRoles.codeHint")}</p>
            </div> : null}
            <div className="space-y-1.5"><Label htmlFor="project-role-name-en">{t("administration.projectRoles.nameEn")}</Label><Input id="project-role-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} required /></div>
            <div className="space-y-1.5"><Label htmlFor="project-role-name-ar">{t("administration.projectRoles.nameAr")}</Label><Input id="project-role-name-ar" dir="rtl" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="project-role-description">{t("administration.projectRoles.descriptionLabel")}</Label><Input id="project-role-description" value={form.description} onChange={(event) => updateForm("description", event.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="project-role-order">{t("administration.projectRoles.displayOrder")}</Label><Input id="project-role-order" type="number" min="0" value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} /></div>
            {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("administration.projectRoles.cancel")}</Button>
            <Button type="submit" disabled={isSaving}>{isSaving ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : null}{t("administration.projectRoles.save")}</Button>
          </SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
