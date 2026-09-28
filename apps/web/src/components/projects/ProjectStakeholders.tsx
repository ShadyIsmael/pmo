"use client";

import { FormEvent, useState } from "react";
import { Edit3, Plus, UserRound, Users, UserRoundPlus, UserX } from "lucide-react";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ErrorState, LoadingSkeleton, PermissionDenied, EmptyState } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ApiError } from "@/lib/api";
import { useAddContactProjectStakeholder, useAddInternalProjectStakeholder, useProjectStakeholders, useRevokeProjectStakeholder, useStakeholderUserLookup, useUpdateProjectStakeholder } from "@/hooks/projects/useProjectStakeholders";
import type { ProjectStakeholderDto } from "@/lib/api/stakeholders";

type Mode = "internal" | "contact" | "edit" | null;
type FormState = { userId: string; name: string; email: string; role: string; interest: string; influence: string };
const emptyForm: FormState = { userId: "", name: "", email: "", role: "", interest: "", influence: "" };

export function ProjectStakeholders({ projectId, enabled = true, canManage = false }: { projectId: string; enabled?: boolean; canManage?: boolean }) {
  const t = useTranslations("projects");
  const query = useProjectStakeholders(projectId, enabled);
  const [mode, setMode] = useState<Mode>(null);
  const [selected, setSelected] = useState<ProjectStakeholderDto | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const usersQuery = useStakeholderUserLookup(mode === "internal");
  const addInternal = useAddInternalProjectStakeholder(projectId);
  const addContact = useAddContactProjectStakeholder(projectId);
  const update = useUpdateProjectStakeholder(projectId, selected?.id ?? "");
  const revoke = useRevokeProjectStakeholder(projectId, selected?.id ?? "");
  const mutationError = addInternal.error ?? addContact.error ?? update.error ?? revoke.error;
  const items = query.data ?? [];

  const reset = () => { setMode(null); setSelected(null); setForm(emptyForm); };
  const openAdd = (nextMode: "internal" | "contact") => { setSelected(null); setForm(emptyForm); setMode(nextMode); };
  const openEdit = (item: ProjectStakeholderDto) => {
    setSelected(item);
    setForm({ userId: item.userId ?? "", name: item.name, email: item.email ?? "", role: item.role, interest: item.interest ?? "", influence: item.influence ?? "" });
    setMode("edit");
  };
  const setField = (field: keyof FormState, value: string) => setForm((current) => ({ ...current, [field]: value }));
  const errorMessage = mutationError instanceof ApiError ? mutationError.message : mutationError instanceof Error ? mutationError.message : null;
  const activeItems = items.filter((item) => item.status !== "Revoked");

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (mode === "internal") await addInternal.mutateAsync({ userId: form.userId, role: form.role, interest: form.interest || undefined, influence: form.influence || undefined });
    if (mode === "contact") await addContact.mutateAsync({ name: form.name, email: form.email || undefined, role: form.role, interest: form.interest || undefined, influence: form.influence || undefined });
    if (mode === "edit" && selected) await update.mutateAsync({ name: form.name, email: form.email || undefined, role: form.role, interest: form.interest || undefined, influence: form.influence || undefined, rowVersion: selected.rowVersion });
    reset();
  };

  const handleRevoke = async (item: ProjectStakeholderDto) => {
    if (!window.confirm(t("confirmRevokeStakeholder"))) return;
    setSelected(item);
    await revoke.mutateAsync(item.rowVersion);
    setSelected(null);
  };

  if (!enabled) return <Card className="shadow-none"><CardContent className="py-8"><PermissionDenied title={t("stakeholdersPermissionRequired")} /></CardContent></Card>;
  if (query.isLoading) return <Card className="shadow-none"><CardContent className="p-4"><LoadingSkeleton rows={4} /></CardContent></Card>;
  if (query.isError) return <Card className="shadow-none"><CardContent className="p-4"><ErrorState title={t("stakeholdersLoadError")} onRetry={() => void query.refetch()} /></CardContent></Card>;

  return (
    <Card className="shadow-none">
      <CardHeader className="flex flex-row items-start justify-between gap-3">
        <div><h2 className="text-sm font-semibold">{t("stakeholders")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("stakeholdersDescription")}</p></div>
        {canManage && <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" onClick={() => openAdd("internal")}><UserRoundPlus className="me-1.5 h-3.5 w-3.5" />{t("addInternalStakeholder")}</Button><Button size="sm" onClick={() => openAdd("contact")}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addContactStakeholder")}</Button></div>}
      </CardHeader>
      <CardContent>
        {items.length === 0 ? <EmptyState icon={Users} title={t("noStakeholders")} description={t("noStakeholdersDescription")} /> : (
          <Table containerClassName="rounded-lg">
            <TableHeader><TableRow><TableHead>{t("stakeholderName")}</TableHead><TableHead>{t("stakeholderEmail")}</TableHead><TableHead>{t("stakeholderRelationship")}</TableHead><TableHead>{t("stakeholderType")}</TableHead><TableHead>{t("stakeholderStatus")}</TableHead>{canManage && <TableHead className="text-end">{t("actions")}</TableHead>}</TableRow></TableHeader>
            <TableBody>{items.map((item) => <TableRow key={item.id} className={item.status === "Revoked" ? "opacity-60" : undefined}>
              <TableCell><div className="flex items-center gap-2"><div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-3.5 w-3.5" /></div><span className="font-medium">{item.name}</span></div></TableCell>
              <TableCell className="text-muted-foreground">{item.email || "—"}</TableCell><TableCell>{item.role}</TableCell><TableCell>{item.userId ? t("stakeholderInternal") : t("stakeholderContactOnly")}</TableCell>
              <TableCell><Badge status={item.status}>{item.status === "ContactOnly" ? t("stakeholderContactOnly") : item.status === "Revoked" ? t("stakeholderRevoked") : t("stakeholderActive")}</Badge></TableCell>
              {canManage && <TableCell><div className="flex justify-end gap-1">{item.status !== "Revoked" && <><Button variant="ghost" size="icon" onClick={() => openEdit(item)} aria-label={t("editStakeholder")}><Edit3 className="h-4 w-4" /></Button><Button variant="ghost" size="icon" onClick={() => void handleRevoke(item)} aria-label={t("revokeStakeholder")}><UserX className="h-4 w-4 text-destructive" /></Button></>}</div></TableCell>}
            </TableRow>)}</TableBody>
          </Table>
        )}
        {activeItems.length === 0 && items.length > 0 ? <p className="mt-3 text-xs text-muted-foreground">{t("allStakeholdersRevoked")}</p> : null}
        {errorMessage ? <p className="mt-3 text-sm text-destructive" role="alert">{errorMessage}</p> : null}
      </CardContent>

      <Dialog open={mode !== null} onOpenChange={(open) => { if (!open) reset(); }}>
        <DialogContent><DialogHeader><DialogTitle>{mode === "internal" ? t("addInternalStakeholder") : mode === "contact" ? t("addContactStakeholder") : t("editStakeholder")}</DialogTitle><DialogDescription>{t("stakeholderDialogDescription")}</DialogDescription></DialogHeader>
          <form onSubmit={(event) => void submit(event)}><DialogBody className="space-y-4">
            {mode === "internal" ? <div className="space-y-2"><Label>{t("stakeholderUser")}</Label><Select value={form.userId} onValueChange={(value) => setField("userId", value)}><SelectTrigger><SelectValue placeholder={t("selectStakeholderUser")} /></SelectTrigger><SelectContent>{(usersQuery.data ?? []).map((user) => <SelectItem key={user.id} value={user.id}>{user.fullName} · {user.email}</SelectItem>)}</SelectContent></Select></div> : null}
            {mode !== "internal" ? <><div className="space-y-2"><Label htmlFor="stakeholder-name">{t("stakeholderName")}</Label><Input id="stakeholder-name" value={form.name} onChange={(event) => setField("name", event.target.value)} required /></div><div className="space-y-2"><Label htmlFor="stakeholder-email">{t("stakeholderEmail")}</Label><Input id="stakeholder-email" type="email" value={form.email} onChange={(event) => setField("email", event.target.value)} /></div></> : null}
            <div className="space-y-2"><Label htmlFor="stakeholder-role">{t("stakeholderRelationship")}</Label><Input id="stakeholder-role" value={form.role} onChange={(event) => setField("role", event.target.value)} required /></div>
            <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-2"><Label htmlFor="stakeholder-interest">{t("stakeholderInterest")}</Label><Input id="stakeholder-interest" value={form.interest} onChange={(event) => setField("interest", event.target.value)} /></div><div className="space-y-2"><Label htmlFor="stakeholder-influence">{t("stakeholderInfluence")}</Label><Input id="stakeholder-influence" value={form.influence} onChange={(event) => setField("influence", event.target.value)} /></div></div>
          </DialogBody><DialogFooter><Button type="button" variant="outline" onClick={reset}>{t("cancel")}</Button><Button type="submit" disabled={addInternal.isPending || addContact.isPending || update.isPending}>{t("saveStakeholder")}</Button></DialogFooter></form>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
