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
import { useRisks, useCreateRisk } from "./api/risks";
import { useProjects } from "@features/projects/api/projects";

export function RisksPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Create));
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [level, setLevel] = React.useState("");
  const [createOpen, setCreateOpen] = React.useState(false);
  const query = useRisks({ status: status || undefined }, true);
  const create = useCreateRisk();
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, createOpen);
  const [form, setForm] = React.useState({ projectId: "", title: "", description: "", riskLevel: "Medium" });

  const risks = React.useMemo(() => {
    const q = search.trim().toLowerCase();
    return (query.data?.items ?? []).filter((r) => {
      if (level && r.riskLevel !== level) return false;
      if (!q) return true;
      return [r.title, r.projectName, r.ownerResourceName].some((v) => v?.toLowerCase().includes(q));
    });
  }, [query.data, search, level]);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("risks.title")}
        description={t("risks.description")}
        actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("risks.create")}</Button> : undefined}
      />
      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-muted/20 px-3 py-2.5">
        <div className="relative min-w-56 flex-1">
          <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("risks.searchPlaceholder")} className="ps-8" />
        </div>
        <select className="h-10 w-40 rounded-md border bg-background px-3 text-sm" value={level} onChange={(e) => setLevel(e.target.value)} aria-label={t("risks.riskLevel")}>
          <option value="">{t("risks.allLevels")}</option>
          <option value="Low">Low</option><option value="Medium">Medium</option><option value="High">High</option><option value="Critical">Critical</option>
        </select>
        <select className="h-10 w-44 rounded-md border bg-background px-3 text-sm" value={status} onChange={(e) => setStatus(e.target.value)} aria-label={t("risks.status")}>
          <option value="">{t("risks.allStatuses")}</option>
          <option value="Identified">Identified</option><option value="ActionRequired">ActionRequired</option><option value="FollowUp">FollowUp</option><option value="Mitigated">Mitigated</option><option value="Closed">Closed</option>
        </select>
      </div>
      {query.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : query.isError ? <p className="py-12 text-center text-sm text-destructive">{t("risks.loadError")}</p>
        : risks.length === 0 ? <p className="py-12 text-center text-sm text-muted-foreground">{t("risks.empty")}</p>
        : (
          <Table>
            <TableHeader><TableRow><TableHead>{t("risks.titleField")}</TableHead><TableHead>{t("risks.project")}</TableHead><TableHead>{t("risks.riskLevel")}</TableHead><TableHead>{t("risks.owner")}</TableHead><TableHead>{t("risks.status")}</TableHead></TableRow></TableHeader>
            <TableBody>
              {risks.map((r) => (
                <TableRow key={r.id}>
                  <TableCell><Link to={`/risks-issues/risks/${encodeURIComponent(r.id)}`} className="font-semibold hover:underline">{r.title}</Link></TableCell>
                  <TableCell>{r.projectName || "—"}</TableCell>
                  <TableCell>{r.riskLevel}</TableCell>
                  <TableCell>{r.ownerResourceName || "—"}</TableCell>
                  <TableCell>{r.status}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      {/* Lookup data loads on demand only when the dialog opens (visible-first) */}
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("risks.createTitle")} description={t("risks.createDescription")}>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!form.projectId || !form.title.trim()) return;
            create.mutate({ projectId: form.projectId, title: form.title.trim(), description: form.description, riskLevel: form.riskLevel as "Low" }, { onSuccess: () => { setCreateOpen(false); setForm({ projectId: "", title: "", description: "", riskLevel: "Medium" }); } });
          }}
          className="flex min-h-0 flex-1 flex-col"
        >
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5">
              <Label>{t("risks.project")} *</Label>
              <select className="input-field" value={form.projectId} onChange={(e) => setForm({ ...form, projectId: e.target.value })} required>
                <option value="">{t("risks.allProjects")}</option>
                {(projectsQuery.data?.items ?? []).map((p) => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}
              </select>
            </div>
            <div className="space-y-1.5"><Label>{t("risks.titleField")} *</Label><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label>{t("risks.description")}</Label><Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
            <div className="space-y-1.5">
              <Label>{t("risks.riskLevel")}</Label>
              <select className="input-field" value={form.riskLevel} onChange={(e) => setForm({ ...form, riskLevel: e.target.value })}>
                <option value="Low">Low</option><option value="Medium">Medium</option><option value="High">High</option><option value="Critical">Critical</option>
              </select>
            </div>
          </SideSheetBody>
          <SideSheetFooter><Button type="submit" disabled={create.isPending}>{create.isPending ? t("common.saving") : t("common.create")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
