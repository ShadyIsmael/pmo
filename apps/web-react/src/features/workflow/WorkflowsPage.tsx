import * as React from "react";
import { Link } from "react-router-dom";
import { Plus, Search } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { useWorkflowDefinitions, useWorkflowMutations } from "./api/workflows";

export function WorkflowsPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Workflow.Manage));
  const [search, setSearch] = React.useState("");
  const [createOpen, setCreateOpen] = React.useState(false);
  const query = useWorkflowDefinitions(search ? { search } : {});
  const mutations = useWorkflowMutations();
  const [form, setForm] = React.useState({ name: "", code: "", domain: "Task", description: "" });

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("workflows.title")}
        description={t("workflows.description")}
        actions={canManage ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("workflows.create")}</Button> : undefined}
      />
      <div className="relative max-w-md">
        <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("workflows.searchPlaceholder")} className="ps-8" />
      </div>
      {query.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : query.isError ? <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
        : (query.data ?? []).length === 0 ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
        : (
          <Table>
            <TableHeader><TableRow><TableHead>{t("workflows.name")}</TableHead><TableHead>{t("workflows.code")}</TableHead><TableHead>{t("workflows.domain")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
            <TableBody>
              {(query.data ?? []).map((d) => (
                <TableRow key={d.id}>
                  <TableCell><Link to={`/workflows/${encodeURIComponent(d.id)}`} className="font-semibold hover:underline">{d.name}</Link></TableCell>
                  <TableCell><span className="font-mono text-xs">{d.code}</span></TableCell>
                  <TableCell>{d.domain}</TableCell>
                  <TableCell>{d.isActive ? t("common.active") : t("common.inactive")}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("workflows.createTitle")}>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            mutations.create.mutate({ name: form.name, code: form.code, domain: form.domain, description: form.description || undefined }, { onSuccess: () => setCreateOpen(false) });
          }}
          className="flex min-h-0 flex-1 flex-col"
        >
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5"><Label>{t("workflows.name")} *</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>{t("workflows.code")} *</Label><Input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} required /></div>
            <div className="space-y-1.5">
              <Label>{t("workflows.domain")}</Label>
              <select className="input-field" value={form.domain} onChange={(e) => setForm({ ...form, domain: e.target.value })}>
                <option value="Task">Task</option><option value="Risk">Risk</option><option value="Finance">Finance</option>
              </select>
            </div>
          </SideSheetBody>
          <SideSheetFooter><Button type="submit" disabled={mutations.create.isPending}>{t("common.create")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
