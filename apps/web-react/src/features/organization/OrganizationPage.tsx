import * as React from "react";
import { ReactFlow, Background, Controls, MiniMap, Handle, Position, type Node, type Edge, useReactFlow } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Card, CardContent } from "@components/ui/Card";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { Plus } from "lucide-react";
import {
  useOrganizationUnits, useOrganizationTypes, useOrganizationMemberships, useOrganizationReporting,
  useCreateUnit, useUpdateUnit, useDeleteUnit, useChangeUnitStatus,
  type OrganizationUnit,
} from "./api/organization";

const TENANT_ROOT_ID = "__tenant-root__";

function buildTree(units: OrganizationUnit[], search: string, status: string, expanded: Set<string>) {
  const byParent = new Map<string, OrganizationUnit[]>();
  for (const u of units) {
    const key = u.parentOrganizationalUnitId || TENANT_ROOT_ID;
    const list = byParent.get(key) ?? [];
    list.push(u);
    byParent.set(key, list);
  }
  for (const list of byParent.values()) list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
  const term = search.trim().toLowerCase();
  const matches = (u: OrganizationUnit) => {
    if (status !== "all" && (status === "active" ? !u.isActive : u.isActive)) return false;
    if (!term) return true;
    return [u.name, u.code, u.typeCode].filter(Boolean).join(" ").toLowerCase().includes(term);
  };
  const visible: OrganizationUnit[] = [];
  const walk = (parentId: string, depth: number) => {
    for (const child of byParent.get(parentId) ?? []) {
      if (!matches(child) && term) {
        // still walk children in case they match
        walk(child.id, depth + 1);
        continue;
      }
      visible.push({ ...child });
      if (expanded.has(child.id)) walk(child.id, depth + 1);
    }
  };
  walk(TENANT_ROOT_ID, 0);
  return { byParent, visible };
}

function layoutNodes(visible: OrganizationUnit[], byParent: Map<string, OrganizationUnit[]>): { nodes: Node[]; edges: Edge[] } {
  // Simple layered layout: BFS rows by depth, x spread by index.
  const depthOf = new Map<string, number>();
  const queue: { id: string; depth: number }[] = [{ id: TENANT_ROOT_ID, depth: -1 }];
  const childrenOf = (pid: string) => byParent.get(pid) ?? [];
  while (queue.length) {
    const { id, depth } = queue.shift()!;
    for (const child of childrenOf(id)) {
      if (!depthOf.has(child.id)) {
        depthOf.set(child.id, depth + 1);
        queue.push({ id: child.id, depth: depth + 1 });
      }
    }
  }
  const rows = new Map<number, OrganizationUnit[]>();
  for (const u of visible) {
    const d = depthOf.get(u.id) ?? 0;
    const list = rows.get(d) ?? [];
    list.push(u);
    rows.set(d, list);
  }
  const nodes: Node[] = [];
  const edges: Edge[] = [];
  for (const [depth, list] of rows) {
    list.forEach((u, i) => {
      nodes.push({
        id: u.id,
        type: "orgNode",
        position: { x: i * 260 - (list.length * 260) / 2, y: depth * 180 },
        data: { unit: u },
        draggable: true,
      });
      const parent = u.parentOrganizationalUnitId || TENANT_ROOT_ID;
      if (parent !== TENANT_ROOT_ID) edges.push({ id: `${parent}->${u.id}`, source: parent, target: u.id, type: "smoothstep" });
      else edges.push({ id: `${TENANT_ROOT_ID}->${u.id}`, source: TENANT_ROOT_ID, target: u.id, type: "smoothstep", style: { strokeDasharray: "4 4" } });
    });
  }
  nodes.unshift({ id: TENANT_ROOT_ID, type: "tenantNode", position: { x: -110, y: -180 }, data: {}, draggable: false });
  return { nodes, edges };
}

function OrgNode({ data }: { data: { unit: OrganizationUnit } }) {
  const u = data.unit;
  const { t } = useI18n();
  return (
    <div className="w-56 rounded-lg border bg-card p-3 shadow-sm">
      <Handle type="target" position={Position.Top} />
      <p className="truncate text-sm font-semibold">{u.name}</p>
      <p className="font-mono text-[11px] text-muted-foreground">{u.code}</p>
      <p className="mt-1 text-xs text-muted-foreground">{u.currentMemberCount} {t("organization.members")}</p>
      <p className="text-xs">{u.isActive ? t("common.active") : t("common.inactive")}</p>
      <Handle type="source" position={Position.Bottom} />
    </div>
  );
}

function TenantNode() {
  const { t } = useI18n();
  return (
    <div className="rounded-lg border bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground shadow">
      {t("organization.tenantRoot")}
      <Handle type="source" position={Position.Bottom} />
    </div>
  );
}

const nodeTypes = { orgNode: OrgNode, tenantNode: TenantNode };

function FlowCanvas({ units, search, status, expanded, onSelect, onReorder }: { units: OrganizationUnit[]; search: string; status: string; expanded: Set<string>; onSelect: (u: OrganizationUnit) => void; onReorder: (unit: OrganizationUnit, position: { x: number; y: number }, nodes: Node[]) => void }) {
  const { byParent, visible } = React.useMemo(() => buildTree(units, search, status, expanded), [units, search, status, expanded]);
  const { nodes: initialNodes, edges: initialEdges } = React.useMemo(() => layoutNodes(visible, byParent), [visible, byParent]);
  const [nodes, setNodes] = React.useState<Node[]>(initialNodes);
  const [edges, setEdges] = React.useState<Edge[]>(initialEdges);
  React.useEffect(() => { setNodes(initialNodes); setEdges(initialEdges); }, [initialNodes, initialEdges]);
  const { fitView } = useReactFlow();
  React.useEffect(() => { const t = setTimeout(() => fitView({ padding: 0.2 }), 50); return () => clearTimeout(t); }, [fitView, visible.length]);
  return (
    <div className="h-[560px] w-full overflow-hidden rounded-lg border">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        onNodesChange={(changes) => {
          // support drag
          setNodes((nds) => {
            const next = [...nds];
            for (const c of changes) {
              if (c.type === "position" && c.position) {
                const idx = next.findIndex((n) => n.id === c.id);
                if (idx >= 0) next[idx] = { ...next[idx], position: c.position };
              }
            }
            return next;
          });
        }}
        onNodeDragStop={(_, node) => {
          if (node.id === TENANT_ROOT_ID) return;
          const unit = units.find((candidate) => candidate.id === node.id);
          if (unit) onReorder(unit, node.position, nodes);
        }}
        onNodeClick={(_, node) => {
          if (node.id === TENANT_ROOT_ID) return;
          const unit = units.find((u) => u.id === node.id);
          if (unit) onSelect(unit);
        }}
        fitView
        panOnDrag
        zoomOnScroll
      >
        <Background />
        <Controls />
        <MiniMap />
      </ReactFlow>
    </div>
  );
}

export function OrganizationPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write));
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("all");
  const [expanded, setExpanded] = React.useState<Set<string>>(() => new Set());
  const [selected, setSelected] = React.useState<OrganizationUnit | null>(null);
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<OrganizationUnit | null>(null);
  const [form, setForm] = React.useState({ name: "", typeCode: "", parentOrganizationalUnitId: "", description: "" });
  const [deleteTarget, setDeleteTarget] = React.useState<OrganizationUnit | null>(null);
  const unitsQuery = useOrganizationUnits(true);
  const typesQuery = useOrganizationTypes(true);
  const membershipsQuery = useOrganizationMemberships(Boolean(selected));
  const reportingQuery = useOrganizationReporting(Boolean(selected));
  const create = useCreateUnit();
  const update = useUpdateUnit();
  const remove = useDeleteUnit();
  const changeStatus = useChangeUnitStatus();

  const units = unitsQuery.data ?? [];
  const types = typesQuery.data ?? [];

  async function persistReorder(unit: OrganizationUnit, position: { x: number; y: number }, nodes: Node[]) {
    if (!canManage || update.isPending) return;
    const siblings = units
      .filter((candidate) => (candidate.parentOrganizationalUnitId ?? null) === (unit.parentOrganizationalUnitId ?? null))
      .map((candidate) => ({ candidate, node: nodes.find((entry) => entry.id === candidate.id) }))
      .sort((left, right) => {
        const leftX = left.candidate.id === unit.id ? position.x : left.node?.position.x ?? left.candidate.sortOrder * 260;
        const rightX = right.candidate.id === unit.id ? position.x : right.node?.position.x ?? right.candidate.sortOrder * 260;
        return leftX - rightX;
      });
    await Promise.all(siblings.map(({ candidate }, index) => {
      if (candidate.sortOrder === index) return Promise.resolve();
      return update.mutateAsync({
        id: candidate.id,
        body: {
          name: candidate.name,
          typeCode: candidate.typeCode,
          parentOrganizationalUnitId: candidate.parentOrganizationalUnitId ?? null,
          description: candidate.description ?? null,
          sortOrder: index,
        },
      });
    }));
  }

  const unitIds = units.map((u) => u.id).join(",");
  React.useEffect(() => {
    // expand top-level by default once loaded
    if (units.length && expanded.size === 0) {
      const roots = units.filter((u) => !u.parentOrganizationalUnitId).map((u) => u.id);
      setExpanded(new Set(roots));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [unitIds]);

  function openCreate(parentId = "") {
    setEditing(null);
    setForm({ name: "", typeCode: types.find((x) => x.isActive)?.code ?? "", parentOrganizationalUnitId: parentId, description: "" });
    setSheetOpen(true);
  }
  function openEdit(unit: OrganizationUnit) {
    if (unit.parentOrganizationalUnitId === unit.id) return;
    setEditing(unit);
    setForm({ name: unit.name, typeCode: unit.typeCode, parentOrganizationalUnitId: unit.parentOrganizationalUnitId ?? "", description: unit.description ?? "" });
    setSheetOpen(true);
  }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (editing && form.parentOrganizationalUnitId === editing.id) return; // cycle prevention
    const payload = { ...form, parentOrganizationalUnitId: form.parentOrganizationalUnitId || null };
    if (editing) await update.mutateAsync({ id: editing.id, body: payload });
    else await create.mutateAsync(payload);
    setSheetOpen(false);
  }

  const members = (membershipsQuery.data ?? []).filter((m) => m.organizationalUnitId === selected?.id && m.isActive);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("organization.title")}
        description={t("organization.description")}
        actions={canManage ? <Button size="sm" onClick={() => openCreate()}><Plus className="me-2 h-4 w-4" />{t("organization.addUnit")}</Button> : undefined}
      />
      <Card>
        <CardContent className="flex flex-col gap-3 p-4 xl:flex-row xl:items-end">
          <div className="min-w-0 flex-1 space-y-1.5">
            <Label htmlFor="org-search">{t("organization.searchHierarchy")}</Label>
            <Input id="org-search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("organization.searchHierarchyPlaceholder")} />
          </div>
          <div className="min-w-36 space-y-1.5">
            <Label>{t("organization.statusFilter")}</Label>
            <select className="input-field" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="all">{t("organization.allStatuses")}</option>
              <option value="active">{t("common.active")}</option>
              <option value="inactive">{t("common.inactive")}</option>
            </select>
          </div>
          <Button variant="outline" onClick={() => { setSearch(""); setStatus("all"); }}>{t("common.clearFilters")}</Button>
        </CardContent>
      </Card>
      {unitsQuery.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : unitsQuery.isError ? <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
        : (
          <FlowCanvas units={units} search={search} status={status} expanded={expanded} onSelect={(u) => { setSelected(u); }} onReorder={(unit, position, nodes) => { void persistReorder(unit, position, nodes); }} />
        )}
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" size="sm" onClick={() => setExpanded(new Set(units.map((u) => u.id)))}>{t("organization.expandAll")}</Button>
        <Button variant="outline" size="sm" onClick={() => setExpanded(new Set())}>{t("organization.collapseAll")}</Button>
      </div>

      <SideSheet open={Boolean(selected)} onOpenChange={(o) => { if (!o) setSelected(null); }} title={selected?.name ?? ""} description={selected?.code}>
        <SideSheetBody className="space-y-3">
          <p className="text-sm text-muted-foreground">{selected?.description || "—"}</p>
          <p className="text-sm">{t("organization.members")}: {selected?.currentMemberCount ?? 0}</p>
          <div className="space-y-1">
            {members.map((m) => <p key={m.id} className="text-sm">{m.resourceName}</p>)}
          </div>
          {canManage && selected ? (
            <div className="flex flex-wrap gap-2 pt-2">
              <Button size="sm" variant="outline" onClick={() => openEdit(selected)}>{t("common.edit")}</Button>
              <Button size="sm" variant="outline" onClick={() => changeStatus.mutate({ unit: selected, active: !selected.isActive })}>{selected.isActive ? t("common.deactivate") : t("common.activate")}</Button>
              <Button size="sm" variant="destructive" onClick={() => setDeleteTarget(selected)}>{t("common.delete")}</Button>
            </div>
          ) : null}
        </SideSheetBody>
      </SideSheet>

      <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={editing ? t("organization.editUnit") : t("organization.addUnit")}>
        <form onSubmit={(e) => void save(e)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5"><Label>{t("organization.name")} *</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></div>
            <div className="space-y-1.5">
              <Label>{t("organization.type")}</Label>
              <select className="input-field" value={form.typeCode} onChange={(e) => setForm({ ...form, typeCode: e.target.value })}>
                {types.map((ty) => <option key={ty.id} value={ty.code}>{ty.nameEn}</option>)}
              </select>
            </div>
            <div className="space-y-1.5">
              <Label>{t("organization.parent")}</Label>
              <select className="input-field" value={form.parentOrganizationalUnitId} onChange={(e) => setForm({ ...form, parentOrganizationalUnitId: e.target.value })}>
                <option value="">{t("organization.root")}</option>
                {units.filter((u) => u.id !== editing?.id).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
              </select>
            </div>
            <div className="space-y-1.5"><Label>{t("common.notes")}</Label><Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="submit" disabled={create.isPending || update.isPending}>{t("common.save")}</Button>
          </SideSheetFooter>
        </form>
      </SideSheet>

      <ConfirmDialog
        open={Boolean(deleteTarget)}
        onOpenChange={(o) => { if (!o) setDeleteTarget(null); }}
        title={t("organization.deleteConfirm", { name: deleteTarget?.name ?? "" })}
        destructive
        confirmLabel={t("common.delete")}
        onConfirm={() => deleteTarget ? remove.mutateAsync(deleteTarget.id).then(() => setDeleteTarget(null)) : Promise.resolve()}
      />
      <span className="hidden">{void reportingQuery.data}</span>
    </div>
  );
}
