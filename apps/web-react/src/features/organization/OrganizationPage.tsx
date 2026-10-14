import * as React from "react";
import {
  Background,
  Controls,
  Handle,
  MiniMap,
  Position,
  ReactFlow,
  useEdgesState,
  useNodesState,
  type Edge,
  type Node,
  type NodeProps,
  type ReactFlowInstance,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Card, CardContent } from "@components/ui/Card";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronDown, ChevronRight, GitBranch, GripVertical, Maximize2, Minimize2, Plus, Users } from "lucide-react";
import { useResources } from "@features/resources/api/resources";
import { loadOrganizationFlowPositions, organizationFlowLayoutStorageKey, saveOrganizationFlowPositions, type OrganizationFlowPositions } from "./organization-layout-storage";
import {
  organizationKeys, useOrganizationUnits, useOrganizationTypes, useOrganizationMemberships, useOrganizationReporting,
  useCreateUnit, useUpdateUnit, useDeleteUnit, useChangeUnitStatus, useInvalidateOrganization,
  type OrganizationMembership, type OrganizationReportingLine, type OrganizationUnit,
} from "./api/organization";

const TENANT_ROOT_ID = "__tenant-root__";
type StructureView = "chart" | "table";
type OrganizationTab = "structure" | "memberships" | "reporting";
type ResourceFilter = "all" | "with" | "empty";
type OrganizationSortKey = "code" | "name" | "type" | "parent" | "members" | "status";
type OrganizationSortState = { key: OrganizationSortKey; direction: "asc" | "desc" };

export function isCurrentOrganizationMembership(membership: Pick<OrganizationMembership, "isActive" | "effectiveTo">, now = Date.now()): boolean {
  return membership.isActive && (!membership.effectiveTo || new Date(membership.effectiveTo).getTime() > now);
}

function organizationHierarchyPath(unit: OrganizationUnit, unitById: Map<string, OrganizationUnit>, tenantLabel: string): string {
  const path = [unit.name];
  const visited = new Set([unit.id]);
  let parentId = unit.parentOrganizationalUnitId ?? null;
  while (parentId && !visited.has(parentId)) {
    visited.add(parentId);
    const parent = unitById.get(parentId);
    if (!parent) break;
    path.unshift(parent.name);
    parentId = parent.parentOrganizationalUnitId ?? null;
  }
  return [tenantLabel, ...path].join(" → ");
}

function OrganizationDetailValue({ label, value }: { label: string; value: string | number }) {
  return <div className="rounded-md border border-border px-3 py-2"><p className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</p><p className="mt-1 text-sm font-medium">{value}</p></div>;
}

function buildTree(units: OrganizationUnit[], search: string, status: string, typeFilter: string, resourceFilter: ResourceFilter, memberships: OrganizationMembership[], membershipsLoaded: boolean, expanded: Set<string>) {
  const byParent = new Map<string, OrganizationUnit[]>();
  for (const u of units) {
    const key = u.parentOrganizationalUnitId || TENANT_ROOT_ID;
    const list = byParent.get(key) ?? [];
    list.push(u);
    byParent.set(key, list);
  }
  for (const list of byParent.values()) list.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
  const term = search.trim().toLowerCase();
  const resourceNamesByUnit = new Map<string, string[]>();
  for (const membership of memberships) {
    if (!isCurrentOrganizationMembership(membership)) continue;
    const names = resourceNamesByUnit.get(membership.organizationalUnitId) ?? [];
    names.push(membership.resourceName);
    resourceNamesByUnit.set(membership.organizationalUnitId, names);
  }
  const matches = (u: OrganizationUnit) => {
    if (status !== "all" && (status === "active" ? !u.isActive : u.isActive)) return false;
    if (typeFilter !== "all" && u.typeCode !== typeFilter) return false;
    const resourceCount = membershipsLoaded ? resourceNamesByUnit.get(u.id)?.length ?? 0 : u.currentMemberCount;
    if (resourceFilter === "with" && resourceCount === 0) return false;
    if (resourceFilter === "empty" && resourceCount > 0) return false;
    if (!term) return true;
    return [u.name, u.code, u.typeCode, u.typeNameEn, u.typeNameAr, ...(resourceNamesByUnit.get(u.id) ?? [])].filter(Boolean).join(" ").toLowerCase().includes(term);
  };
  const unitById = new Map(units.map((unit) => [unit.id, unit]));
  // Treat missing parents and isolated/cyclic components as tenant roots. The
  // API rejects new cycles, but this keeps legacy data renderable instead of
  // silently producing an empty chart or recursing forever.
  const rootChildren = [...(byParent.get(TENANT_ROOT_ID) ?? [])];
  const reachable = new Set<string>();
  const reachStack = rootChildren.map((unit) => unit.id);
  while (reachStack.length) {
    const id = reachStack.pop()!;
    if (!reachable.add(id)) continue;
    for (const child of byParent.get(id) ?? []) reachStack.push(child.id);
  }
  for (const unit of units) {
    if (reachable.has(unit.id)) continue;
    rootChildren.push(unit);
    reachStack.push(unit.id);
    while (reachStack.length) {
      const id = reachStack.pop()!;
      if (!reachable.add(id)) continue;
      for (const child of byParent.get(id) ?? []) reachStack.push(child.id);
    }
  }
  byParent.set(TENANT_ROOT_ID, rootChildren);
  const hasFilter = Boolean(term) || status !== "all" || typeFilter !== "all" || resourceFilter !== "all";
  const contextIds = new Set<string>();
  for (const unit of units) {
    if (!matches(unit)) continue;
    contextIds.add(unit.id);
    const visitedParents = new Set<string>();
    let parentId = unit.parentOrganizationalUnitId;
    while (parentId && visitedParents.add(parentId)) {
      contextIds.add(parentId);
      parentId = unitById.get(parentId)?.parentOrganizationalUnitId ?? null;
    }
  }
  if (!hasFilter) for (const unit of units) contextIds.add(unit.id);
  const visible: OrganizationUnit[] = [];
  const visited = new Set<string>();
  // Keep traversal iterative so a malformed/deep hierarchy cannot block the
  // main thread with recursive calls while the Organization screen opens.
  const stack = (expanded.has(TENANT_ROOT_ID) || hasFilter ? byParent.get(TENANT_ROOT_ID) ?? [] : [])
    .slice()
    .reverse()
    .map((child) => ({ child, depth: 0 }));
  while (stack.length) {
    const frame = stack.pop()!;
    const child = frame.child;
    if (visited.has(child.id)) continue;
    visited.add(child.id);
    if (!contextIds.has(child.id)) continue;
    visible.push({ ...child });
    if (expanded.has(child.id) || hasFilter) {
      for (const nested of (byParent.get(child.id) ?? []).slice().reverse()) stack.push({ child: nested, depth: frame.depth + 1 });
    }
  }
  return { byParent, visible };
}

export function OrganizationStructureTable({ units, canManage, onEdit, onStatus, onDelete, emptyLabel, t }: { units: OrganizationUnit[]; canManage: boolean; onEdit: (unit: OrganizationUnit) => void; onStatus: (unit: OrganizationUnit) => void; onDelete: (unit: OrganizationUnit) => void; emptyLabel: string; t: (key: string, values?: Record<string, string | number>) => string }) {
  const { locale } = useI18n();
  const [sort, setSort] = React.useState<OrganizationSortState>({ key: "name", direction: "asc" });
  const collator = React.useMemo(() => new Intl.Collator(locale, { numeric: true, sensitivity: "base" }), [locale]);
  const rootLabel = t("administration.areas.organization.root");
  const activeLabel = t("administration.areas.organization.active");
  const inactiveLabel = t("administration.areas.organization.inactive");
  const sortedUnits = React.useMemo(() => [...units].sort((left, right) => {
    let comparison = 0;
    switch (sort.key) {
      case "code": comparison = collator.compare(left.code, right.code); break;
      case "name": comparison = collator.compare(left.name, right.name); break;
      case "type": comparison = collator.compare(left.typeNameEn ?? left.typeCode, right.typeNameEn ?? right.typeCode); break;
      case "parent": comparison = collator.compare(left.parentName ?? rootLabel, right.parentName ?? rootLabel); break;
      case "members": comparison = left.currentMemberCount - right.currentMemberCount; break;
      case "status": comparison = collator.compare(left.isActive ? activeLabel : inactiveLabel, right.isActive ? activeLabel : inactiveLabel); break;
    }
    if (comparison === 0) comparison = collator.compare(left.code, right.code);
    return sort.direction === "asc" ? comparison : -comparison;
  }), [units, sort, collator, rootLabel, activeLabel, inactiveLabel]);

  const sortHeader = (key: OrganizationSortKey, label: string) => {
    const active = sort.key === key;
    const Icon = active ? sort.direction === "asc" ? ArrowUp : ArrowDown : ArrowUpDown;
    return <th key={key} scope="col" aria-sort={active ? sort.direction === "asc" ? "ascending" : "descending" : "none"} className="px-4 py-3 text-start">
      <button type="button" onClick={() => setSort((current) => ({ key, direction: current.key === key && current.direction === "asc" ? "desc" : "asc" }))} className="inline-flex items-center gap-1.5 text-start transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
        <span>{label}</span><Icon aria-hidden="true" className={`h-3.5 w-3.5 shrink-0 ${active ? "text-primary" : "opacity-40"}`} />
      </button>
    </th>;
  };

  if (units.length === 0) return <p className="py-12 text-center text-sm text-muted-foreground">{emptyLabel}</p>;
  return <div className="overflow-x-auto rounded-lg border"><table className="w-full text-sm"><thead><tr className="border-b bg-muted/30 text-xs text-muted-foreground">{sortHeader("code", t("administration.areas.organization.code"))}{sortHeader("name", t("administration.areas.organization.name"))}{sortHeader("type", t("administration.areas.organization.type"))}{sortHeader("parent", t("administration.areas.organization.parent"))}{sortHeader("members", t("administration.areas.organization.members"))}{sortHeader("status", t("administration.areas.organization.status"))}<th scope="col" className="px-4 py-3 text-end">{t("administration.areas.organization.actions")}</th></tr></thead><tbody>{sortedUnits.map((unit) => <tr key={unit.id} className="border-b last:border-0"><td className="px-4 py-3 font-mono text-xs">{unit.code}</td><td className="px-4 py-3 font-medium">{unit.name}</td><td className="px-4 py-3"><span className={`font-medium ${organizationTypeTextTone(unit.typeCode)}`}>{unit.typeNameEn ?? unit.typeCode}</span></td><td className="px-4 py-3 text-muted-foreground">{unit.parentName ?? rootLabel}</td><td className="px-4 py-3">{unit.currentMemberCount}</td><td className="px-4 py-3"><span className={unit.isActive ? "font-medium text-success" : "font-medium text-muted-foreground"}>{unit.isActive ? activeLabel : inactiveLabel}</span></td><td className="px-4 py-3"><div className="flex justify-end gap-1">{canManage ? <><Button size="sm" variant="ghost" onClick={() => onEdit(unit)}>{t("common.edit")}</Button><Button size="sm" variant="ghost" onClick={() => onStatus(unit)}>{unit.isActive ? t("administration.areas.organization.deactivate") : t("administration.areas.organization.reactivate")}</Button><Button size="sm" variant="ghost" onClick={() => onDelete(unit)}>{t("common.delete")}</Button></> : null}</div></td></tr>)}</tbody></table></div>;
}

type OrganizationCardData = {
  unit: OrganizationUnit | null;
  tenantLabel: string;
  displayMode: "structure" | "resources";
  resourceNames: string[];
  directCount: number;
  totalCount: number;
  childCount: number;
  hiddenCount: number;
  expanded: boolean;
  canManage: boolean;
  onToggle: (unitId: string) => void;
  onAddChild: (parentId?: string) => void;
  onExpandBranch: (unitId: string) => void;
  onCollapseBranch: (unitId: string) => void;
};
type OrganizationUnitFlowNode = Node<OrganizationCardData, "organizationUnit">;
type TenantRootFlowNode = Node<OrganizationCardData, "tenantRoot">;
type OrganizationFlowNode = OrganizationUnitFlowNode | TenantRootFlowNode;

const ORGANIZATION_NODE_WIDTH = 236;
const ORGANIZATION_NODE_HEIGHT = 202;
const ORGANIZATION_RESOURCE_NODE_HEIGHT = 288;
const ORGANIZATION_COLUMN_GAP = 84;
const ORGANIZATION_ROW_GAP = 112;

function OrganizationCard({ data, selected }: NodeProps<OrganizationFlowNode>) {
  const { t, locale } = useI18n();
  const unit = data.unit;
  const isRoot = !unit;
  const nodeId = unit?.id ?? TENANT_ROOT_ID;
  const tone = isRoot ? "border-primary/50 bg-primary/5" : organizationTypeCardTone(unit?.typeCode ?? "");
  const typeLabel = !unit ? t("administration.areas.organization.virtualRoot") : locale === "ar" ? unit.typeNameAr || unit.typeNameEn || unit.typeCode : unit.typeNameEn || unit.typeCode;
  return <>
    {!isRoot ? <Handle type="target" position={Position.Top} id="target" className="!h-2.5 !w-2.5 !border-2 !border-background !bg-muted-foreground" /> : null}
    <div
      className={`flex h-full flex-col rounded-lg border shadow-sm transition-shadow hover:shadow-md ${tone} ${selected ? "ring-2 ring-primary ring-offset-2 ring-offset-background" : ""}`}
    >
      <div className="flex min-w-0 items-start gap-2 border-b border-border/70 px-3 py-2">
        <button type="button" className="nodrag nopan mt-0.5 shrink-0 text-muted-foreground" disabled={!data.childCount} onClick={(event) => { event.stopPropagation(); data.onToggle(nodeId); }} aria-label={data.expanded ? t("administration.areas.organization.collapse") : t("administration.areas.organization.expand")}>
          {data.childCount ? (data.expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />) : <span className="inline-block h-4 w-4" />}
        </button>
        {data.canManage && unit ? <GripVertical className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" /> : null}
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{unit?.name ?? data.tenantLabel}</p>
          <p className={`truncate text-[10px] uppercase tracking-wider ${unit ? organizationTypeTextTone(unit.typeCode) : "text-muted-foreground"}`}>{typeLabel}</p>
        </div>
        {data.canManage ? <button type="button" className="nodrag nopan rounded p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground" onClick={(event) => { event.stopPropagation(); data.onAddChild(unit?.id); }} aria-label={isRoot ? t("administration.areas.organization.addTopLevel") : t("administration.areas.organization.addChild")}><Plus className="h-4 w-4" /></button> : null}
      </div>
      <div className="flex-1 space-y-2 px-3 py-2">
        <div className="flex items-center justify-between gap-2"><span className="truncate font-mono text-[10px] text-muted-foreground">{unit?.code ?? "TENANT"}</span><span className={`shrink-0 rounded-full border px-2 py-0.5 text-[10px] ${unit?.isActive ? "border-success/30 text-success" : "border-border text-muted-foreground"}`}>{unit ? unit.isActive ? t("administration.areas.organization.active") : t("administration.areas.organization.inactive") : t("administration.areas.organization.virtualRoot")}</span></div>
        <div className="grid grid-cols-2 gap-2 text-[11px]"><div className="rounded border border-border/70 px-2 py-1"><span className="block text-muted-foreground">{t("administration.areas.organization.directResources")}</span><span className="font-semibold tabular-nums">{data.directCount}</span></div><div className="rounded border border-border/70 px-2 py-1"><span className="block text-muted-foreground">{t("administration.areas.organization.totalResources")}</span><span className="font-semibold tabular-nums">{data.totalCount}</span></div></div>
        <p className="text-[11px] text-muted-foreground">{t("administration.areas.organization.childUnits")}: {data.childCount}</p>
        {data.hiddenCount > 0 ? <p className="text-[11px] font-medium text-primary">+ {data.hiddenCount} {t("administration.areas.organization.hiddenUnits")}</p> : null}
        {data.displayMode === "resources" && unit ? <div className="space-y-1 border-t border-border/70 pt-2">{data.resourceNames.slice(0, 3).map((name, index) => <div key={`${unit.id}-${index}`} className="flex items-center gap-1.5 truncate rounded border border-border/70 px-2 py-1 text-[10px] text-success"><Users className="h-3 w-3 shrink-0" />{name}</div>)}{data.resourceNames.length > 3 ? <p className="text-[10px] text-muted-foreground">+ {data.resourceNames.length - 3} {t("administration.areas.organization.moreResources")}</p> : null}{data.resourceNames.length === 0 ? <p className="text-[10px] text-muted-foreground">{t("administration.areas.organization.noCurrentMembers")}</p> : null}</div> : null}
      </div>
      {unit ? <div className="flex items-center justify-end gap-1 border-t border-border/70 px-3 py-1.5"><button type="button" className="nodrag nopan text-[10px] text-muted-foreground hover:text-foreground" onClick={(event) => { event.stopPropagation(); data.onExpandBranch(unit.id); }}>{t("administration.areas.organization.expandBranch")}</button><span className="text-muted-foreground">·</span><button type="button" className="nodrag nopan text-[10px] text-muted-foreground hover:text-foreground" onClick={(event) => { event.stopPropagation(); data.onCollapseBranch(unit.id); }}>{t("administration.areas.organization.collapseBranch")}</button></div> : null}
    </div>
    <Handle type="source" position={Position.Bottom} id="source" className="!h-2.5 !w-2.5 !border-2 !border-background !bg-primary" />
  </>;
}

const organizationNodeTypes = { organizationUnit: OrganizationCard, tenantRoot: OrganizationCard };

export function organizationTypeCardTone(typeCode: string) {
  const normalized = typeCode.toLowerCase();
  if (normalized.includes("division")) return "border-info/50 bg-info/10";
  if (normalized.includes("department")) return "border-primary/45 bg-primary/10";
  if (normalized.includes("section")) return "border-warning/50 bg-warning/10";
  if (normalized.includes("branch")) return "border-success/45 bg-success/10";
  if (normalized.includes("function")) return "border-chart-3/55 bg-chart-3/10";
  return "border-border bg-card";
}

export function organizationTypeTextTone(typeCode: string) {
  const normalized = typeCode.toLowerCase();
  if (normalized.includes("division")) return "text-info";
  if (normalized.includes("department")) return "text-primary";
  if (normalized.includes("section")) return "text-warning";
  if (normalized.includes("branch")) return "text-success";
  if (normalized.includes("function")) return "text-chart-3";
  return "text-foreground";
}

export function buildOrganizationFlowGraph(visible: OrganizationUnit[], byParent: Map<string, OrganizationUnit[]>, memberships: OrganizationMembership[], membershipsLoaded: boolean, displayMode: "structure" | "resources", tenantRootLabel: string, expanded: Set<string>, canManage: boolean, handlers: Pick<OrganizationCardData, "onToggle" | "onAddChild" | "onExpandBranch" | "onCollapseBranch">, savedPositions: OrganizationFlowPositions = {}): { nodes: OrganizationFlowNode[]; edges: Edge[] } {
  const depthOf = new Map<string, number>();
  const safeParent = new Map<string, string>();
  const allUnitsById = new Map<string, OrganizationUnit>(visible.map((unit) => [unit.id, unit]));
  for (const children of byParent.values()) for (const unit of children) allUnitsById.set(unit.id, unit);
  const allUnits = [...allUnitsById.values()];
  const visibleIds = new Set(visible.map((unit) => unit.id));
  const queue: { id: string; depth: number }[] = [{ id: TENANT_ROOT_ID, depth: -1 }];
  for (let cursor = 0; cursor < queue.length; cursor += 1) {
    const { id, depth } = queue[cursor];
    for (const child of byParent.get(id) ?? []) {
      if (!depthOf.has(child.id)) {
        depthOf.set(child.id, depth + 1);
        safeParent.set(child.id, id);
        queue.push({ id: child.id, depth: depth + 1 });
      }
    }
  }
  const rows = new Map<number, OrganizationUnit[]>();
  for (const unit of visible) {
    const depth = depthOf.get(unit.id) ?? 0;
    const row = rows.get(depth) ?? [];
    row.push(unit);
    rows.set(depth, row);
  }
  const namesByUnit = new Map<string, string[]>();
  const directResourceIds = new Map<string, Set<string>>();
  for (const membership of memberships) {
    if (!isCurrentOrganizationMembership(membership)) continue;
    const names = namesByUnit.get(membership.organizationalUnitId) ?? [];
    names.push(membership.resourceName);
    namesByUnit.set(membership.organizationalUnitId, names);
    const ids = directResourceIds.get(membership.organizationalUnitId) ?? new Set<string>();
    ids.add(membership.resourceId);
    directResourceIds.set(membership.organizationalUnitId, ids);
  }
  const descendantCounts = new Map(allUnits.map((unit) => [unit.id, 0]));
  const subtreeResourceIds = new Map(allUnits.map((unit) => [unit.id, new Set(directResourceIds.get(unit.id) ?? [])]));
  const subtreeResourceCounts = new Map(allUnits.map((unit) => [unit.id, membershipsLoaded ? directResourceIds.get(unit.id)?.size ?? 0 : unit.currentMemberCount]));
  const bottomUp = [...allUnits].sort((left, right) => (depthOf.get(right.id) ?? 0) - (depthOf.get(left.id) ?? 0));
  for (const unit of bottomUp) {
    const parentId = safeParent.get(unit.id);
    if (!parentId || parentId === TENANT_ROOT_ID) continue;
    descendantCounts.set(parentId, (descendantCounts.get(parentId) ?? 0) + 1 + (descendantCounts.get(unit.id) ?? 0));
    if (membershipsLoaded) {
      const parentResources = subtreeResourceIds.get(parentId);
      for (const resourceId of subtreeResourceIds.get(unit.id) ?? []) parentResources?.add(resourceId);
    } else {
      subtreeResourceCounts.set(parentId, (subtreeResourceCounts.get(parentId) ?? 0) + (subtreeResourceCounts.get(unit.id) ?? 0));
    }
  }
  const tenantResourceCount = membershipsLoaded
    ? new Set(memberships.filter((membership) => isCurrentOrganizationMembership(membership)).map((membership) => membership.resourceId)).size
    : allUnits.reduce((total, unit) => total + unit.currentMemberCount, 0);
  const rootChildren = byParent.get(TENANT_ROOT_ID) ?? [];
  const rootData: OrganizationCardData = {
    unit: null, tenantLabel: tenantRootLabel, displayMode, resourceNames: [], directCount: 0,
    totalCount: tenantResourceCount, childCount: rootChildren.length, hiddenCount: 0,
    expanded: expanded.has(TENANT_ROOT_ID), canManage, ...handlers,
  };
  const orderedRows = [...rows.entries()].sort(([left], [right]) => left - right);
  const rowWidth = (count: number) => count ? count * ORGANIZATION_NODE_WIDTH + (count - 1) * ORGANIZATION_COLUMN_GAP : 0;
  const widestRow = Math.max(ORGANIZATION_NODE_WIDTH, ...orderedRows.map(([, row]) => rowWidth(row.length)));
  const nodes: OrganizationFlowNode[] = [];
  const edges: Edge[] = [];
  for (const [depth, row] of orderedRows) {
    const startX = 40 + (widestRow - rowWidth(row.length)) / 2;
    row.forEach((unit, index) => {
      const parentId = unit.parentOrganizationalUnitId;
      const hasVisibleParentAtPreviousDepth = Boolean(parentId && visibleIds.has(parentId) && depthOf.get(parentId) === depth - 1);
      const sourceId = hasVisibleParentAtPreviousDepth ? parentId! : TENANT_ROOT_ID;
      const savedPosition = savedPositions[unit.id];
      nodes.push({
        id: unit.id,
        type: "organizationUnit",
        position: savedPosition ?? { x: startX + index * (ORGANIZATION_NODE_WIDTH + ORGANIZATION_COLUMN_GAP), y: 246 + depth * ((displayMode === "resources" ? ORGANIZATION_RESOURCE_NODE_HEIGHT : ORGANIZATION_NODE_HEIGHT) + ORGANIZATION_ROW_GAP) },
        style: { width: ORGANIZATION_NODE_WIDTH, height: displayMode === "resources" ? ORGANIZATION_RESOURCE_NODE_HEIGHT : ORGANIZATION_NODE_HEIGHT },
        data: {
          unit, tenantLabel: tenantRootLabel, displayMode, resourceNames: namesByUnit.get(unit.id) ?? [],
          directCount: membershipsLoaded ? directResourceIds.get(unit.id)?.size ?? 0 : unit.currentMemberCount,
          totalCount: membershipsLoaded ? subtreeResourceIds.get(unit.id)?.size ?? 0 : subtreeResourceCounts.get(unit.id) ?? unit.currentMemberCount,
          childCount: byParent.get(unit.id)?.length ?? 0,
          hiddenCount: expanded.has(unit.id) ? 0 : descendantCounts.get(unit.id) ?? 0,
          expanded: expanded.has(unit.id), canManage, ...handlers,
        },
        draggable: true,
      });
      edges.push({ id: `${sourceId}->${unit.id}`, source: sourceId, sourceHandle: "source", target: unit.id, targetHandle: "target", type: "smoothstep", style: { stroke: "hsl(var(--border))", strokeWidth: 1.5, ...(sourceId === TENANT_ROOT_ID ? { strokeDasharray: "5 4" } : {}) } });
    });
  }
  const rootCount = rows.get(0)?.length ?? 0;
  const rootRowWidth = rowWidth(rootCount);
  nodes.unshift({ id: TENANT_ROOT_ID, type: "tenantRoot", position: { x: 40 + (rootRowWidth - ORGANIZATION_NODE_WIDTH) / 2, y: 28 }, style: { width: ORGANIZATION_NODE_WIDTH, height: ORGANIZATION_NODE_HEIGHT }, data: rootData, draggable: false });
  return { nodes, edges };
}

function OrganizationSummaryCard({ title, value, icon }: { title: string; value: number; icon: React.ReactNode }) {
  return <Card><CardContent className="flex items-center gap-3 p-4"><div className="rounded-lg bg-primary/10 p-2 text-primary">{icon}</div><div><p className="text-xs text-muted-foreground">{title}</p><p className="text-2xl font-bold tabular-nums">{value}</p></div></CardContent></Card>;
}

function OrganizationMembershipTable({ rows, locale, canManage, busy, onEnd, t }: { rows: OrganizationMembership[]; locale: string; canManage: boolean; busy: boolean; onEnd: (id: string) => void; t: (key: string, values?: Record<string, string | number>) => string }) {
  if (!rows.length) return <p className="py-10 text-center text-sm text-muted-foreground">{t("administration.areas.organization.noMemberships")}</p>;
  const dateFormatter = new Intl.DateTimeFormat(locale, { dateStyle: "medium", numberingSystem: "latn" });
  return <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.resource")}</th><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.unit")}</th><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.effectiveFrom")}</th><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.status")}</th><th scope="col" className="px-3 py-2 text-end">{t("administration.areas.organization.actions")}</th></tr></thead><tbody>{rows.map((row) => <tr key={row.id} className="border-b last:border-0"><td className="px-3 py-2">{row.resourceName}</td><td className="px-3 py-2">{row.organizationalUnitName}{row.isPrimary ? <span className="ms-2 text-xs font-medium text-primary">{t("administration.areas.organization.primary")}</span> : null}</td><td className="px-3 py-2">{dateFormatter.format(new Date(row.effectiveFrom))}</td><td className={`px-3 py-2 font-medium ${row.isActive ? "text-success" : "text-muted-foreground"}`}>{row.isActive ? t("administration.areas.organization.active") : t("administration.areas.organization.ended")}</td><td className="px-3 py-2 text-end">{canManage && row.isActive ? <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => onEnd(row.id)}>{t("administration.areas.organization.end")}</Button> : null}</td></tr>)}</tbody></table></div>;
}

function OrganizationReportingTable({ rows, canManage, busy, onEnd, t }: { rows: OrganizationReportingLine[]; canManage: boolean; busy: boolean; onEnd: (id: string) => void; t: (key: string, values?: Record<string, string | number>) => string }) {
  if (!rows.length) return <p className="py-10 text-center text-sm text-muted-foreground">{t("administration.areas.organization.noReportingLines")}</p>;
  return <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.resource")}</th><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.manager")}</th><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.reportingType")}</th><th scope="col" className="px-3 py-2 text-start">{t("administration.areas.organization.status")}</th><th scope="col" className="px-3 py-2 text-end">{t("administration.areas.organization.actions")}</th></tr></thead><tbody>{rows.map((row) => <tr key={row.id} className="border-b last:border-0"><td className="px-3 py-2">{row.resourceName}</td><td className="px-3 py-2">{row.managerResourceName}</td><td className="px-3 py-2">{row.reportingType}</td><td className={`px-3 py-2 font-medium ${row.isActive ? "text-success" : "text-muted-foreground"}`}>{row.isActive ? t("administration.areas.organization.active") : t("administration.areas.organization.ended")}</td><td className="px-3 py-2 text-end">{canManage && row.isActive ? <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => onEnd(row.id)}>{t("administration.areas.organization.end")}</Button> : null}</td></tr>)}</tbody></table></div>;
}

function FlowCanvas({ units, search, status, typeFilter, resourceFilter, searchResults, memberships, membershipsLoaded, displayMode, tenantLabel, expanded, chartExpanded, layoutPositions, onLayoutPositionsChange, canManage, onSelect, onSelectRoot, onCreateUnit, onToggleUnit, onExpandBranch, onCollapseBranch, onReorder, onReparent, onToggleChart }: {
  units: OrganizationUnit[]; search: string; status: string; typeFilter: string; resourceFilter: ResourceFilter;
  searchResults: OrganizationUnit[];
  memberships: OrganizationMembership[]; membershipsLoaded: boolean; displayMode: "structure" | "resources"; tenantLabel: string; expanded: Set<string>;
  layoutPositions: OrganizationFlowPositions; onLayoutPositionsChange: (positions: OrganizationFlowPositions) => void;
  chartExpanded: boolean; canManage: boolean; onSelect: (u: OrganizationUnit) => void; onSelectRoot: () => void;
  onCreateUnit: (parentId?: string) => void; onToggleUnit: (id: string) => void;
  onExpandBranch: (id: string) => void; onCollapseBranch: (id: string) => void;
  onReorder: (unit: OrganizationUnit, position: { x: number; y: number }, nodes: Node[]) => void;
  onReparent: (unit: OrganizationUnit, parent: OrganizationUnit | null) => void; onToggleChart: () => void;
}) {
  const { t } = useI18n();
  const { byParent, visible } = React.useMemo(() => buildTree(units, search, status, typeFilter, resourceFilter, memberships, membershipsLoaded, expanded), [units, search, status, typeFilter, resourceFilter, memberships, membershipsLoaded, expanded]);
  const unitById = React.useMemo(() => new Map(units.map((unit) => [unit.id, unit])), [units]);
  const actionsRef = React.useRef({ onSelect, onCreateUnit, onToggleUnit, onExpandBranch, onCollapseBranch });
  actionsRef.current = { onSelect, onCreateUnit, onToggleUnit, onExpandBranch, onCollapseBranch };
  const nodeHandlers = React.useMemo(() => ({
    onToggle: (id: string) => actionsRef.current.onToggleUnit(id),
    onAddChild: (parentId?: string) => actionsRef.current.onCreateUnit(parentId),
    onExpandBranch: (id: string) => actionsRef.current.onExpandBranch(id),
    onCollapseBranch: (id: string) => actionsRef.current.onCollapseBranch(id),
  }), []);
  const graph = React.useMemo(() => buildOrganizationFlowGraph(visible, byParent, memberships, membershipsLoaded, displayMode, tenantLabel, expanded, canManage, nodeHandlers, layoutPositions), [visible, byParent, memberships, membershipsLoaded, displayMode, tenantLabel, expanded, canManage, nodeHandlers, layoutPositions]);
  const [nodes, setNodes, onNodesChange] = useNodesState<OrganizationFlowNode>(graph.nodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>(graph.edges);
  const flowRef = React.useRef<ReactFlowInstance<OrganizationFlowNode, Edge> | null>(null);
  const canvasRef = React.useRef<HTMLDivElement | null>(null);
  const [nativeFullscreen, setNativeFullscreen] = React.useState(false);
  const isFullscreen = chartExpanded || nativeFullscreen;
  const toggleChartRef = React.useRef(onToggleChart);
  toggleChartRef.current = onToggleChart;

  React.useEffect(() => {
    setNodes(graph.nodes);
    setEdges(graph.edges);
  }, [graph, setEdges, setNodes]);

  React.useEffect(() => {
    if (!isFullscreen) return;
    const frame = window.requestAnimationFrame(() => flowRef.current?.fitView({ padding: 0.16, duration: 180 }));
    return () => window.cancelAnimationFrame(frame);
  }, [isFullscreen]);

  React.useEffect(() => {
    const syncFullscreen = () => setNativeFullscreen(document.fullscreenElement === canvasRef.current);
    document.addEventListener("fullscreenchange", syncFullscreen);
    return () => document.removeEventListener("fullscreenchange", syncFullscreen);
  }, []);

  React.useEffect(() => {
    if (!chartExpanded) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.body.style.overflow = previousOverflow; };
  }, [chartExpanded]);

  React.useEffect(() => {
    if (!chartExpanded) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") toggleChartRef.current();
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [chartExpanded]);

  const handleNodeDragStop = React.useCallback((_event: MouseEvent | TouchEvent, draggedNode: OrganizationFlowNode) => {
    if (!canManage || draggedNode.id === TENANT_ROOT_ID) return;
    const unit = unitById.get(draggedNode.id);
    if (!unit) return;
    onLayoutPositionsChange({ ...layoutPositions, [unit.id]: draggedNode.position });
    const width = Number(draggedNode.style?.width) || ORGANIZATION_NODE_WIDTH;
    const height = Number(draggedNode.style?.height) || ORGANIZATION_NODE_HEIGHT;
    const center = { x: draggedNode.position.x + width / 2, y: draggedNode.position.y + height / 2 };
    const target = nodes.find((candidate) => {
      if (candidate.id === draggedNode.id) return false;
      const candidateWidth = Number(candidate.style?.width) || ORGANIZATION_NODE_WIDTH;
      const candidateHeight = Number(candidate.style?.height) || ORGANIZATION_NODE_HEIGHT;
      return center.x >= candidate.position.x && center.x <= candidate.position.x + candidateWidth && center.y >= candidate.position.y && center.y <= candidate.position.y + candidateHeight;
    });
    if (target) {
      onReparent(unit, target.id === TENANT_ROOT_ID ? null : unitById.get(target.id) ?? null);
      return;
    }
    onReorder(unit, draggedNode.position, [...nodes.filter((node) => node.id !== draggedNode.id), draggedNode]);
  }, [canManage, layoutPositions, nodes, onLayoutPositionsChange, onReorder, onReparent, unitById]);

  const toggleFullscreen = React.useCallback(async () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    if (document.fullscreenElement) {
      try { await document.exitFullscreen(); } catch { /* Keep the in-page fallback usable if the browser refuses to exit. */ }
      return;
    }
    if (chartExpanded) {
      onToggleChart();
      return;
    }
    if (typeof canvas.requestFullscreen === "function") {
      try {
        await canvas.requestFullscreen();
        return;
      } catch { /* Fall back to an in-page fullscreen canvas. */ }
    }
    onToggleChart();
  }, [chartExpanded, onToggleChart]);

  return (
    <>
      {searchResults.length ? <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span>{t("administration.areas.organization.searchResults")}</span>{searchResults.map((unit) => <Button key={unit.id} size="sm" variant="outline" onClick={() => { onSelect(unit); setNodes((current) => current.map((node) => ({ ...node, selected: node.id === unit.id }))); window.requestAnimationFrame(() => flowRef.current?.fitView({ nodes: [{ id: unit.id }], padding: 0.55, duration: 240 })); }}>{unit.name} · {unit.code}</Button>)}</div> : null}
      <div ref={canvasRef} data-testid="organization-flow-canvas" className={`${isFullscreen ? chartExpanded ? "fixed inset-0 z-[100] h-screen w-screen rounded-none shadow-2xl" : "h-screen w-screen rounded-none" : "relative h-[560px] w-full rounded-lg"} overflow-hidden border bg-muted/20`}>
      <Button type="button" variant="outline" size="sm" aria-label={t(isFullscreen ? "administration.areas.organization.exitFullscreen" : "administration.areas.organization.fullscreen")} onClick={() => void toggleFullscreen()} className="absolute end-3 top-3 z-10 gap-1.5 bg-background shadow-sm">{isFullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}{t(isFullscreen ? "administration.areas.organization.exitFullscreen" : "administration.areas.organization.fullscreen")}</Button>
      <ReactFlow<OrganizationFlowNode, Edge>
        className="h-full w-full"
        nodes={nodes}
        edges={edges}
        nodeTypes={organizationNodeTypes}
        onInit={(instance) => { flowRef.current = instance; }}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={(_event, node) => { if (node.data.unit) onSelect(node.data.unit); else onSelectRoot(); }}
        onNodeDragStop={handleNodeDragStop}
        nodesDraggable={canManage}
        nodesConnectable={false}
        elementsSelectable
        fitView
        fitViewOptions={{ padding: 0.16, duration: 180 }}
        minZoom={0.25}
        maxZoom={1.5}
        proOptions={{ hideAttribution: true }}
      >
        <Background gap={22} size={1} />
        <Controls showInteractive={false} />
        <MiniMap pannable zoomable nodeColor={(node) => node.id === TENANT_ROOT_ID ? "hsl(var(--primary))" : "hsl(var(--muted-foreground))"} />
      </ReactFlow>
      </div>
    </>
  );
}

export function OrganizationPage() {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write) && canMutate && !effectiveReadOnly);
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = React.useState<OrganizationTab>("structure");
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("all");
  const [typeFilter, setTypeFilter] = React.useState("all");
  const [resourceFilter, setResourceFilter] = React.useState<ResourceFilter>("all");
  const [structureView, setStructureView] = React.useState<StructureView>("chart");
  const [displayMode, setDisplayMode] = React.useState<"structure" | "resources">("structure");
  const flowLayoutStorageKey = organizationFlowLayoutStorageKey(user?.tenantId, user?.id, displayMode);
  const [flowLayoutState, setFlowLayoutState] = React.useState<{ storageKey: string; positions: OrganizationFlowPositions }>(() => ({ storageKey: flowLayoutStorageKey, positions: loadOrganizationFlowPositions(flowLayoutStorageKey) }));
  const flowLayoutPositions = flowLayoutState.storageKey === flowLayoutStorageKey ? flowLayoutState.positions : {};
  const saveFlowLayoutPositions = React.useCallback((positions: OrganizationFlowPositions) => {
    setFlowLayoutState({ storageKey: flowLayoutStorageKey, positions });
  }, [flowLayoutStorageKey]);
  const [expanded, setExpanded] = React.useState<Set<string>>(() => new Set());
  const [selected, setSelected] = React.useState<OrganizationUnit | null>(null);
  const [tenantRootSelected, setTenantRootSelected] = React.useState(false);
  const [moveParent, setMoveParent] = React.useState("");
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<OrganizationUnit | null>(null);
  const [form, setForm] = React.useState({ name: "", typeCode: "", parentOrganizationalUnitId: "", description: "" });
  const [deleteTarget, setDeleteTarget] = React.useState<OrganizationUnit | null>(null);
  const [chartExpanded, setChartExpanded] = React.useState(false);
  const [reparentError, setReparentError] = React.useState<string | null>(null);
  const [tabOperationError, setTabOperationError] = React.useState<string | null>(null);
  const [tabOperationPending, setTabOperationPending] = React.useState(false);
  const [membershipForm, setMembershipForm] = React.useState({ resourceId: "", organizationalUnitId: "", isPrimary: true });
  const [reportingForm, setReportingForm] = React.useState({ resourceId: "", managerResourceId: "", reportingType: "LineManager" });
  const unitsQuery = useOrganizationUnits(true);
  const typesQuery = useOrganizationTypes(true);
  const tenantQuery = useQuery({
    queryKey: ["organization", "tenant-name", user?.tenantId, user?.id],
    queryFn: async () => {
      const result = await api.get<{ items: Array<{ name?: string }> }>("/api/organizations", { page: 1, pageSize: 1 });
      return result.items[0]?.name ?? null;
    },
    enabled: Boolean(user),
    staleTime: 300_000,
  });
  const membershipsQuery = useOrganizationMemberships(activeTab !== "structure" || displayMode === "resources" || Boolean(selected) || Boolean(search.trim()) || resourceFilter !== "all");
  const reportingQuery = useOrganizationReporting(activeTab === "reporting" || Boolean(selected));
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true }, activeTab !== "structure");
  const create = useCreateUnit();
  const update = useUpdateUnit();
  const reorderUpdate = useUpdateUnit(false);
  const invalidateOrganization = useInvalidateOrganization();
  const remove = useDeleteUnit();
  const changeStatus = useChangeUnitStatus();

  const units = React.useMemo(() => unitsQuery.data ?? [], [unitsQuery.data]);
  const types = React.useMemo(() => typesQuery.data ?? [], [typesQuery.data]);
  const memberships = React.useMemo(() => membershipsQuery.data ?? [], [membershipsQuery.data]);
  const reportingLines = React.useMemo(() => reportingQuery.data ?? [], [reportingQuery.data]);
  const resources = React.useMemo(() => (resourcesQuery.data?.items ?? []).map((resource) => ({ id: resource.id, name: resource.name })), [resourcesQuery.data]);
  const unitById = React.useMemo(() => new Map(units.map((unit) => [unit.id, unit])), [units]);
  const childrenByParent = React.useMemo(() => {
    const children = new Map<string, OrganizationUnit[]>();
    for (const unit of units) {
      const parentId = unit.parentOrganizationalUnitId || TENANT_ROOT_ID;
      const siblings = children.get(parentId) ?? [];
      siblings.push(unit);
      children.set(parentId, siblings);
    }
    return children;
  }, [units]);
  const currentMemberships = React.useMemo(() => {
    const now = Date.now();
    return memberships.filter((membership) => isCurrentOrganizationMembership(membership, now));
  }, [memberships]);
  const selectedUnit = selected ? unitById.get(selected.id) ?? selected : null;
  const selectedMembers = React.useMemo(() => selectedUnit ? currentMemberships.filter((membership) => membership.organizationalUnitId === selectedUnit.id) : [], [currentMemberships, selectedUnit]);
  const selectedSubtreeIds = React.useMemo(() => {
    if (!selectedUnit) return new Set<string>();
    const descendants = new Set([selectedUnit.id]);
    const stack = [selectedUnit.id];
    while (stack.length) {
      for (const child of childrenByParent.get(stack.pop()!) ?? []) {
        if (descendants.has(child.id)) continue;
        descendants.add(child.id);
        stack.push(child.id);
      }
    }
    return descendants;
  }, [childrenByParent, selectedUnit]);
  const selectedTotalResourceCount = React.useMemo(() => new Set(currentMemberships.filter((membership) => selectedSubtreeIds.has(membership.organizationalUnitId)).map((membership) => membership.resourceId)).size, [currentMemberships, selectedSubtreeIds]);
  const selectedMemberResourceIds = React.useMemo(() => new Set(selectedMembers.map((membership) => membership.resourceId)), [selectedMembers]);
  const selectedReportingLines = React.useMemo(() => reportingLines.filter((line) => selectedMemberResourceIds.has(line.resourceId) || selectedMemberResourceIds.has(line.managerResourceId)), [reportingLines, selectedMemberResourceIds]);
  const selectedChildren = selectedUnit ? childrenByParent.get(selectedUnit.id) ?? [] : [];
  const rootLabel = tenantQuery.data || t("organization.tenantRoot");
  const selectedHierarchyPath = selectedUnit ? organizationHierarchyPath(selectedUnit, unitById, rootLabel) : rootLabel;
  const blockedMoveParentIds = selectedSubtreeIds;
  const selectableMoveParents = units.filter((unit) => !blockedMoveParentIds.has(unit.id) && (unit.isActive || unit.id === selectedUnit?.parentOrganizationalUnitId));

  React.useEffect(() => {
    setMoveParent(selectedUnit?.parentOrganizationalUnitId ?? "");
    setReparentError(null);
  }, [selectedUnit?.id, selectedUnit?.parentOrganizationalUnitId]);

  React.useEffect(() => {
    setFlowLayoutState({ storageKey: flowLayoutStorageKey, positions: loadOrganizationFlowPositions(flowLayoutStorageKey) });
  }, [flowLayoutStorageKey]);

  React.useEffect(() => {
    if (flowLayoutState.storageKey === flowLayoutStorageKey) saveOrganizationFlowPositions(flowLayoutStorageKey, flowLayoutState.positions);
  }, [flowLayoutStorageKey, flowLayoutState]);

  React.useEffect(() => {
    if (!membershipForm.organizationalUnitId && units.length) {
      const firstActiveUnit = units.find((unit) => unit.isActive);
      if (firstActiveUnit) setMembershipForm((current) => ({ ...current, organizationalUnitId: firstActiveUnit.id }));
    }
  }, [membershipForm.organizationalUnitId, units]);

  async function saveMembership(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage || !membershipForm.resourceId || !membershipForm.organizationalUnitId) return;
    setTabOperationError(null);
    setTabOperationPending(true);
    try {
      await api.post(endpoints.organization.memberships, { ...membershipForm, effectiveFrom: new Date().toISOString() });
      setMembershipForm((current) => ({ ...current, resourceId: "" }));
      await queryClient.invalidateQueries({ queryKey: organizationKeys.all });
    } catch (error) {
      setTabOperationError(error instanceof Error ? error.message : t("administration.areas.organization.saveError"));
    } finally {
      setTabOperationPending(false);
    }
  }

  async function saveReportingLine(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage || !reportingForm.resourceId || !reportingForm.managerResourceId || reportingForm.resourceId === reportingForm.managerResourceId) return;
    setTabOperationError(null);
    setTabOperationPending(true);
    try {
      await api.post(endpoints.organization.reportingLines, { ...reportingForm, isPrimary: true, effectiveFrom: new Date().toISOString() });
      setReportingForm({ resourceId: "", managerResourceId: "", reportingType: "LineManager" });
      await queryClient.invalidateQueries({ queryKey: organizationKeys.all });
    } catch (error) {
      setTabOperationError(error instanceof Error ? error.message : t("administration.areas.organization.saveError"));
    } finally {
      setTabOperationPending(false);
    }
  }

  async function endMembership(id: string) {
    if (!canManage) return;
    setTabOperationError(null);
    setTabOperationPending(true);
    try {
      await api.post(endpoints.organization.endMembership(id), { effectiveTo: new Date().toISOString() });
      await queryClient.invalidateQueries({ queryKey: organizationKeys.all });
    } catch (error) {
      setTabOperationError(error instanceof Error ? error.message : t("administration.areas.organization.saveError"));
    } finally {
      setTabOperationPending(false);
    }
  }

  async function endReportingLine(id: string) {
    if (!canManage) return;
    setTabOperationError(null);
    setTabOperationPending(true);
    try {
      await api.post(endpoints.organization.endReportingLine(id), { effectiveTo: new Date().toISOString() });
      await queryClient.invalidateQueries({ queryKey: organizationKeys.all });
    } catch (error) {
      setTabOperationError(error instanceof Error ? error.message : t("administration.areas.organization.saveError"));
    } finally {
      setTabOperationPending(false);
    }
  }

  async function persistReorder(unit: OrganizationUnit, position: { x: number; y: number }, nodes: Node[]) {
    if (!canManage || update.isPending || reorderUpdate.isPending) return;
    const nodeById = new Map(nodes.map((node) => [node.id, node]));
    const siblings = units
      .filter((candidate) => (candidate.parentOrganizationalUnitId ?? null) === (unit.parentOrganizationalUnitId ?? null))
      .map((candidate) => ({ candidate, node: nodeById.get(candidate.id) }))
      .sort((left, right) => {
        const leftX = left.candidate.id === unit.id ? position.x : left.node?.position.x ?? left.candidate.sortOrder * 260;
        const rightX = right.candidate.id === unit.id ? position.x : right.node?.position.x ?? right.candidate.sortOrder * 260;
        return leftX - rightX;
      });
    await Promise.all(siblings.map(({ candidate }, index) => {
      if (candidate.sortOrder === index) return Promise.resolve();
      return reorderUpdate.mutateAsync({
        id: candidate.id,
        body: {
          name: candidate.name,
          typeCode: candidate.typeCode,
          parentOrganizationalUnitId: candidate.parentOrganizationalUnitId ?? null,
          description: candidate.description ?? null,
          sortOrder: index,
        },
      });
    })).then(() => invalidateOrganization());
  }

  async function persistReparent(unit: OrganizationUnit, parent: OrganizationUnit | null) {
    if (!canManage || update.isPending) return;
    setReparentError(null);
    const unitById = new Map(units.map((candidate) => [candidate.id, candidate]));
    let current = parent;
    const visited = new Set<string>();
    while (current && visited.add(current.id)) {
      if (current.id === unit.id) {
        setReparentError(t("administration.areas.organization.invalidDrop"));
        return;
      }
      current = current.parentOrganizationalUnitId ? unitById.get(current.parentOrganizationalUnitId) ?? null : null;
    }
    if ((unit.parentOrganizationalUnitId ?? null) === (parent?.id ?? null)) return;
    try {
      await update.mutateAsync({ id: unit.id, body: { name: unit.name, typeCode: unit.typeCode, parentOrganizationalUnitId: parent?.id ?? null, description: unit.description ?? null, sortOrder: parent ? (units.filter((candidate) => candidate.parentOrganizationalUnitId === parent.id).length) : units.filter((candidate) => !candidate.parentOrganizationalUnitId).length } });
    } catch (error) {
      setReparentError(error instanceof Error ? error.message : t("administration.areas.organization.moveError"));
    }
  }

  const unitIds = units.map((u) => u.id).join(",");
  React.useEffect(() => {
    // expand top-level by default once loaded
    if (units.length && expanded.size === 0) {
      const roots = units.filter((u) => !u.parentOrganizationalUnitId).map((u) => u.id);
      setExpanded(new Set([TENANT_ROOT_ID, ...roots]));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [unitIds]);

  function openCreate(parentId = "") {
    setSelected(null);
    setTenantRootSelected(false);
    setReparentError(null);
    setEditing(null);
    setForm({ name: "", typeCode: types.find((x) => x.isActive)?.code ?? "", parentOrganizationalUnitId: parentId, description: "" });
    setSheetOpen(true);
  }
  function toggleBranch(id: string) {
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }
  function setBranchExpanded(id: string, shouldExpand: boolean) {
    const childrenByParent = new Map<string, OrganizationUnit[]>();
    for (const unit of units) {
      const parentId = unit.parentOrganizationalUnitId || TENANT_ROOT_ID;
      const children = childrenByParent.get(parentId) ?? [];
      children.push(unit);
      childrenByParent.set(parentId, children);
    }
    const branch = new Set<string>();
    const stack = [id];
    while (stack.length) {
      const currentId = stack.pop()!;
      if (!branch.add(currentId)) continue;
      for (const child of childrenByParent.get(currentId) ?? []) stack.push(child.id);
    }
    setExpanded((current) => {
      const next = new Set(current);
      for (const branchId of branch) {
        if (shouldExpand) next.add(branchId);
        else next.delete(branchId);
      }
      return next;
    });
  }
  function openEdit(unit: OrganizationUnit) {
    if (unit.parentOrganizationalUnitId === unit.id) return;
    setSelected(null);
    setReparentError(null);
    setEditing(unit);
    setForm({ name: unit.name, typeCode: unit.typeCode, parentOrganizationalUnitId: unit.parentOrganizationalUnitId ?? "", description: unit.description ?? "" });
    setSheetOpen(true);
  }
  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (!canManage) return;
    if (editing) {
      const unitById = new Map(units.map((candidate) => [candidate.id, candidate]));
      const visited = new Set<string>();
      let parentId: string | null = form.parentOrganizationalUnitId || null;
      while (parentId && visited.add(parentId)) {
        if (parentId === editing.id) {
          setReparentError(t("administration.areas.organization.invalidDrop"));
          return;
        }
        parentId = unitById.get(parentId)?.parentOrganizationalUnitId ?? null;
      }
    }
    const payload = { ...form, parentOrganizationalUnitId: form.parentOrganizationalUnitId || null };
    try {
      if (editing) await update.mutateAsync({ id: editing.id, body: payload });
      else await create.mutateAsync(payload);
      setReparentError(null);
      setSheetOpen(false);
    } catch (error) {
      setReparentError(error instanceof Error ? error.message : t("administration.areas.organization.moveError"));
    }
  }

  function manageSelectedResources() {
    if (!selectedUnit || !canManage) return;
    setMembershipForm((current) => ({ ...current, resourceId: "", organizationalUnitId: selectedUnit.id }));
    setSelected(null);
    setTenantRootSelected(false);
    setActiveTab("memberships");
  }

  function selectOrganizationUnit(unit: OrganizationUnit) {
    setSelected(unit);
    setTenantRootSelected(false);
  }

  function selectTenantRoot() {
    setSelected(null);
    setTenantRootSelected(true);
  }

  const filteredUnits = React.useMemo(() => {
    const namesByUnit = new Map<string, string[]>();
    const resourceIdsByUnit = new Map<string, Set<string>>();
    for (const membership of memberships) {
      if (!isCurrentOrganizationMembership(membership)) continue;
      const names = namesByUnit.get(membership.organizationalUnitId) ?? [];
      names.push(membership.resourceName);
      namesByUnit.set(membership.organizationalUnitId, names);
      const resourceIds = resourceIdsByUnit.get(membership.organizationalUnitId) ?? new Set<string>();
      resourceIds.add(membership.resourceId);
      resourceIdsByUnit.set(membership.organizationalUnitId, resourceIds);
    }
    const term = search.trim().toLowerCase();
    return units.filter((unit) => {
      if (status !== "all" && (status === "active" ? !unit.isActive : unit.isActive)) return false;
      if (typeFilter !== "all" && unit.typeCode !== typeFilter) return false;
      const resourceNames = namesByUnit.get(unit.id) ?? [];
      const resourceCount = membershipsQuery.isSuccess ? resourceIdsByUnit.get(unit.id)?.size ?? 0 : unit.currentMemberCount;
      if (resourceFilter === "with" && resourceCount === 0) return false;
      if (resourceFilter === "empty" && resourceCount > 0) return false;
      if (!term) return true;
      return [unit.name, unit.code, unit.typeCode, unit.typeNameEn, unit.typeNameAr, ...resourceNames].filter(Boolean).join(" ").toLowerCase().includes(term);
    });
  }, [memberships, membershipsQuery.isSuccess, resourceFilter, search, status, typeFilter, units]);
  const searchResults = React.useMemo(() => search.trim() ? filteredUnits.slice(0, 8) : [], [filteredUnits, search]);
  const activeUnits = React.useMemo(() => units.filter((unit) => unit.isActive), [units]);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("organization.title")}
        description={t("organization.description")}
        actions={canManage ? <Button size="sm" onClick={() => openCreate()}><Plus className="me-2 h-4 w-4" />{t("organization.addUnit")}</Button> : undefined}
      />
      {tabOperationError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive">{tabOperationError}</p> : null}
      <div className="grid gap-4 sm:grid-cols-3">
        <OrganizationSummaryCard title={t("administration.areas.organization.units")} value={activeUnits.length} icon={<GitBranch className="h-4 w-4" />} />
        <OrganizationSummaryCard title={t("administration.areas.organization.memberships")} value={memberships.length} icon={<Users className="h-4 w-4" />} />
        <OrganizationSummaryCard title={t("administration.areas.organization.reportingLines")} value={reportingLines.length} icon={<GitBranch className="h-4 w-4" />} />
      </div>
      <Tabs value={activeTab} onValueChange={(value) => { setActiveTab(value as OrganizationTab); setTabOperationError(null); }}>
        <TabsList>
          <TabsTrigger value="structure">{t("administration.areas.organization.structureTab")}</TabsTrigger>
          <TabsTrigger value="memberships">{t("administration.areas.organization.membershipTab")}</TabsTrigger>
          <TabsTrigger value="reporting">{t("administration.areas.organization.reportingTab")}</TabsTrigger>
        </TabsList>
        <TabsContent value="structure" className="space-y-4">
          <Card>
            <CardContent className="flex flex-col gap-3 p-4 xl:flex-row xl:items-end">
              <div className="min-w-0 flex-1 space-y-1.5"><Label htmlFor="org-search">{t("organization.searchHierarchy")}</Label><Input id="org-search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("administration.areas.organization.searchHierarchyPlaceholder")} /></div>
              <div className="min-w-40 space-y-1.5"><Label>{t("administration.areas.organization.typeFilter")}</Label><select className="input-field" value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}><option value="all">{t("administration.areas.organization.allTypes")}</option>{types.map((type) => <option key={type.id} value={type.code}>{locale === "ar" ? type.nameAr || type.nameEn : type.nameEn}</option>)}</select></div>
              <div className="min-w-36 space-y-1.5"><Label>{t("administration.areas.organization.statusFilter")}</Label><select className="input-field" value={status} onChange={(e) => setStatus(e.target.value)}><option value="all">{t("administration.areas.organization.allStatuses")}</option><option value="active">{t("administration.areas.organization.active")}</option><option value="inactive">{t("administration.areas.organization.inactive")}</option></select></div>
              <div className="min-w-40 space-y-1.5"><Label>{t("administration.areas.organization.resourceFilter")}</Label><select className="input-field" value={resourceFilter} onChange={(e) => setResourceFilter(e.target.value as ResourceFilter)}><option value="all">{t("administration.areas.organization.allUnits")}</option><option value="with">{t("administration.areas.organization.withResources")}</option><option value="empty">{t("administration.areas.organization.emptyUnits")}</option></select></div>
              <Button type="button" variant="outline" onClick={() => { setSearch(""); setStatus("all"); setTypeFilter("all"); setResourceFilter("all"); }}>{t("administration.areas.organization.clearFilters")}</Button>
            </CardContent>
          </Card>
          {unitsQuery.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : unitsQuery.isError ? <div className="flex flex-col items-center gap-3 py-12 text-sm text-destructive"><p>{t("common.errorLoading")}</p><Button type="button" variant="outline" onClick={() => void unitsQuery.refetch()}>{t("common.retry")}</Button></div> : <>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex gap-1 rounded-md border p-1" role="group" aria-label={t("administration.areas.organization.viewMode")}><Button type="button" size="sm" variant={structureView === "chart" ? "secondary" : "ghost"} aria-pressed={structureView === "chart"} onClick={() => setStructureView("chart")}>{t("administration.areas.organization.chartView")}</Button><Button type="button" size="sm" variant={structureView === "table" ? "secondary" : "ghost"} aria-pressed={structureView === "table"} onClick={() => setStructureView("table")}>{t("administration.areas.organization.tableView")}</Button></div>
              {structureView === "chart" ? <div className="flex gap-1 rounded-md border p-1" role="group" aria-label={t("administration.areas.organization.structureMode")}><Button type="button" size="sm" variant={displayMode === "structure" ? "secondary" : "ghost"} aria-pressed={displayMode === "structure"} onClick={() => setDisplayMode("structure")}>{t("administration.areas.organization.structureMode")}</Button><Button type="button" size="sm" variant={displayMode === "resources" ? "secondary" : "ghost"} aria-pressed={displayMode === "resources"} onClick={() => setDisplayMode("resources")}>{t("administration.areas.organization.resourcesMode")}</Button></div> : null}
            </div>
            {structureView === "chart" ? <FlowCanvas units={units} search={search} status={status} typeFilter={typeFilter} resourceFilter={resourceFilter} searchResults={searchResults} memberships={memberships} membershipsLoaded={membershipsQuery.isSuccess} displayMode={displayMode} tenantLabel={rootLabel} expanded={expanded} chartExpanded={chartExpanded} layoutPositions={flowLayoutPositions} onLayoutPositionsChange={saveFlowLayoutPositions} canManage={canManage} onSelect={selectOrganizationUnit} onSelectRoot={selectTenantRoot} onCreateUnit={openCreate} onToggleUnit={toggleBranch} onExpandBranch={(id) => setBranchExpanded(id, true)} onCollapseBranch={(id) => setBranchExpanded(id, false)} onReorder={(unit, position, nodes) => { void persistReorder(unit, position, nodes); }} onReparent={(unit, parent) => { void persistReparent(unit, parent); }} onToggleChart={() => setChartExpanded((value) => !value)} /> : <OrganizationStructureTable units={filteredUnits} canManage={canManage} onEdit={openEdit} onStatus={(unit) => changeStatus.mutate({ unit, active: !unit.isActive })} onDelete={(unit) => setDeleteTarget(unit)} emptyLabel={filteredUnits.length ? "" : t("administration.areas.organization.noMatches")} t={t} />}
            <div className="flex flex-wrap gap-2">{structureView === "chart" ? <><Button type="button" variant="outline" size="sm" onClick={() => setExpanded(new Set([TENANT_ROOT_ID, ...units.map((unit) => unit.id)]))}>{t("administration.areas.organization.expandAll")}</Button><Button type="button" variant="outline" size="sm" onClick={() => setExpanded(new Set())}>{t("administration.areas.organization.collapseAll")}</Button></> : null}{reparentError ? <p role="alert" className="basis-full text-sm text-destructive">{reparentError}</p> : null}</div>
          </>}
        </TabsContent>
        <TabsContent value="memberships">
          <Card><CardContent className="space-y-5 p-6">
            <div><h2 className="text-lg font-semibold">{t("administration.areas.organization.membershipTitle")}</h2><p className="text-sm text-muted-foreground">{t("administration.areas.organization.membershipDescription")}</p></div>
            {resourcesQuery.isLoading || membershipsQuery.isLoading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : resourcesQuery.isError || membershipsQuery.isError ? <div className="flex flex-col items-center gap-3 py-8 text-sm text-destructive"><p>{t("common.errorLoading")}</p><Button type="button" variant="outline" onClick={() => { void resourcesQuery.refetch(); void membershipsQuery.refetch(); }}>{t("common.retry")}</Button></div> : <>
              <form onSubmit={(event) => void saveMembership(event)} className="grid gap-3 md:grid-cols-[1fr_1fr_auto_auto]">
                <div className="space-y-1.5"><Label htmlFor="organization-membership-resource">{t("administration.areas.organization.resource")}</Label><select id="organization-membership-resource" className="input-field w-full" value={membershipForm.resourceId} disabled={!canManage || tabOperationPending} onChange={(event) => setMembershipForm((current) => ({ ...current, resourceId: event.target.value }))}><option value="">{t("administration.areas.organization.selectResource")}</option>{resources.map((resource) => <option key={resource.id} value={resource.id}>{resource.name}</option>)}</select></div>
                <div className="space-y-1.5"><Label htmlFor="organization-membership-unit">{t("administration.areas.organization.unit")}</Label><select id="organization-membership-unit" className="input-field w-full" value={membershipForm.organizationalUnitId || activeUnits[0]?.id || ""} disabled={!canManage || tabOperationPending} onChange={(event) => setMembershipForm((current) => ({ ...current, organizationalUnitId: event.target.value }))}><option value="">{t("administration.areas.organization.selectUnit")}</option>{activeUnits.map((unit) => <option key={unit.id} value={unit.id}>{unit.name} · {unit.code}</option>)}</select></div>
                <label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={membershipForm.isPrimary} disabled={!canManage || tabOperationPending} onChange={(event) => setMembershipForm((current) => ({ ...current, isPrimary: event.target.checked }))} />{t("administration.areas.organization.primary")}</label>
                <Button type="submit" disabled={!canManage || tabOperationPending || !membershipForm.resourceId || !activeUnits.length} className="self-end">{tabOperationPending ? t("common.saving") : t("administration.areas.organization.assign")}</Button>
              </form>
              <OrganizationMembershipTable rows={memberships} locale={locale} canManage={canManage} busy={tabOperationPending} onEnd={(id) => void endMembership(id)} t={t} />
            </>}
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="reporting">
          <Card><CardContent className="space-y-5 p-6">
            <div><h2 className="text-lg font-semibold">{t("administration.areas.organization.reportingTitle")}</h2><p className="text-sm text-muted-foreground">{t("administration.areas.organization.reportingDescription")}</p></div>
            {resourcesQuery.isLoading || reportingQuery.isLoading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : resourcesQuery.isError || reportingQuery.isError ? <div className="flex flex-col items-center gap-3 py-8 text-sm text-destructive"><p>{t("common.errorLoading")}</p><Button type="button" variant="outline" onClick={() => { void resourcesQuery.refetch(); void reportingQuery.refetch(); }}>{t("common.retry")}</Button></div> : <>
              <form onSubmit={(event) => void saveReportingLine(event)} className="grid gap-3 md:grid-cols-[1fr_1fr_1fr_auto]">
                <div className="space-y-1.5"><Label htmlFor="organization-reporting-resource">{t("administration.areas.organization.resource")}</Label><select id="organization-reporting-resource" className="input-field w-full" value={reportingForm.resourceId} disabled={!canManage || tabOperationPending} onChange={(event) => setReportingForm((current) => ({ ...current, resourceId: event.target.value, managerResourceId: current.managerResourceId === event.target.value ? "" : current.managerResourceId }))}><option value="">{t("administration.areas.organization.selectResource")}</option>{resources.map((resource) => <option key={resource.id} value={resource.id}>{resource.name}</option>)}</select></div>
                <div className="space-y-1.5"><Label htmlFor="organization-reporting-manager">{t("administration.areas.organization.manager")}</Label><select id="organization-reporting-manager" className="input-field w-full" value={reportingForm.managerResourceId} disabled={!canManage || tabOperationPending} onChange={(event) => setReportingForm((current) => ({ ...current, managerResourceId: event.target.value }))}><option value="">{t("administration.areas.organization.selectManager")}</option>{resources.filter((resource) => resource.id !== reportingForm.resourceId).map((resource) => <option key={resource.id} value={resource.id}>{resource.name}</option>)}</select></div>
                <div className="space-y-1.5"><Label htmlFor="organization-reporting-type">{t("administration.areas.organization.reportingType")}</Label><Input id="organization-reporting-type" value={reportingForm.reportingType} disabled={!canManage || tabOperationPending} onChange={(event) => setReportingForm((current) => ({ ...current, reportingType: event.target.value }))} /></div>
                <Button type="submit" disabled={!canManage || tabOperationPending || !reportingForm.resourceId || !reportingForm.managerResourceId || reportingForm.resourceId === reportingForm.managerResourceId} className="self-end">{tabOperationPending ? t("common.saving") : t("administration.areas.organization.assign")}</Button>
              </form>
              <OrganizationReportingTable rows={reportingLines} canManage={canManage} busy={tabOperationPending} onEnd={(id) => void endReportingLine(id)} t={t} />
            </>}
          </CardContent></Card>
        </TabsContent>
      </Tabs>

      <SideSheet open={Boolean(selectedUnit || tenantRootSelected)} onOpenChange={(open) => { if (!open) { setSelected(null); setTenantRootSelected(false); } }} title={selectedUnit?.name ?? (tenantRootSelected ? rootLabel : "")} description={selectedUnit ? locale === "ar" ? selectedUnit.typeNameAr || selectedUnit.typeNameEn || selectedUnit.typeCode : selectedUnit.typeNameEn || selectedUnit.typeCode : tenantRootSelected ? t("administration.areas.organization.virtualRoot") : undefined}>
        <SideSheetBody className="space-y-5">
          {selectedUnit ? <>
            <div className="flex flex-wrap items-center gap-2"><span className={`text-xs font-medium ${selectedUnit.isActive ? "text-success" : "text-muted-foreground"}`}>{selectedUnit.isActive ? t("administration.areas.organization.active") : t("administration.areas.organization.inactive")}</span><span className="font-mono text-xs text-muted-foreground">{selectedUnit.code}</span></div>
            <section className="space-y-2"><h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("administration.areas.organization.hierarchyPath")}</h3><p className="text-sm leading-6">{selectedHierarchyPath}</p></section>
            <div className="grid gap-3 sm:grid-cols-2">
              <OrganizationDetailValue label={t("administration.areas.organization.type")} value={locale === "ar" ? selectedUnit.typeNameAr || selectedUnit.typeNameEn || selectedUnit.typeCode : selectedUnit.typeNameEn || selectedUnit.typeCode} />
              <OrganizationDetailValue label={t("administration.areas.organization.parent")} value={selectedUnit.parentName ?? (selectedUnit.parentOrganizationalUnitId ? unitById.get(selectedUnit.parentOrganizationalUnitId)?.name ?? rootLabel : rootLabel)} />
              <OrganizationDetailValue label={t("administration.areas.organization.directResources")} value={new Set(selectedMembers.map((membership) => membership.resourceId)).size} />
              <OrganizationDetailValue label={t("administration.areas.organization.totalResources")} value={selectedTotalResourceCount} />
              <OrganizationDetailValue label={t("administration.areas.organization.childUnits")} value={selectedChildren.length} />
            </div>
            {selectedUnit.description ? <section className="space-y-2"><h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("administration.areas.organization.descriptionField")}</h3><p className="whitespace-pre-wrap text-sm text-muted-foreground">{selectedUnit.description}</p></section> : null}
            <section className="space-y-2 border-t pt-4">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("administration.areas.organization.reparentUnit")}</h3>
              <Label htmlFor="organization-reparent">{t("administration.areas.organization.newParent")}</Label>
              <select id="organization-reparent" className="input-field w-full" value={moveParent} disabled={!canManage || update.isPending} onChange={(event) => setMoveParent(event.target.value)}><option value="">{rootLabel}</option>{selectableMoveParents.map((parent) => <option key={parent.id} value={parent.id}>{parent.name} · {parent.code}</option>)}</select>
              {reparentError ? <p role="alert" className="text-xs text-destructive">{reparentError}</p> : null}
              <Button type="button" variant="outline" className="w-full" disabled={!canManage || update.isPending || moveParent === (selectedUnit.parentOrganizationalUnitId ?? "")} onClick={() => { const parent = moveParent ? unitById.get(moveParent) ?? null : null; void persistReparent(selectedUnit, parent); }}>{t("administration.areas.organization.saveMove")}</Button>
            </section>
            <section className="space-y-2 border-t pt-4">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("administration.areas.organization.resourceMembers")}</h3>
              {membershipsQuery.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : selectedMembers.length ? <div className="space-y-2">{selectedMembers.map((membership) => <div key={membership.id} className="flex items-center justify-between gap-3 rounded-md border px-3 py-2 text-sm"><span>{membership.resourceName}</span>{membership.isPrimary ? <span className="text-xs font-medium text-primary">{t("administration.areas.organization.primary")}</span> : null}</div>)}</div> : <p className="text-sm text-muted-foreground">{t("administration.areas.organization.noCurrentMembers")}</p>}
            </section>
            <section className="space-y-2 border-t pt-4">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("administration.areas.organization.reportingStructure")}</h3>
              {reportingQuery.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : selectedReportingLines.length ? <div className="space-y-2">{selectedReportingLines.map((line) => <div key={line.id} className="rounded-md border px-3 py-2 text-sm"><p className="font-medium">{line.resourceName} → {line.managerResourceName}</p><p className="text-xs text-muted-foreground">{line.reportingType}</p></div>)}</div> : <p className="text-sm text-muted-foreground">{t("administration.areas.organization.noReportingLines")}</p>}
            </section>
          </> : tenantRootSelected ? <>
            <section className="space-y-2"><h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("administration.areas.organization.hierarchyPath")}</h3><p className="text-sm">{rootLabel}</p></section>
            <p className="text-sm text-muted-foreground">{t("administration.areas.organization.tenantRootDescription")}</p>
            <OrganizationDetailValue label={t("administration.areas.organization.childUnits")} value={childrenByParent.get(TENANT_ROOT_ID)?.length ?? units.filter((unit) => !unit.parentOrganizationalUnitId).length} />
          </> : null}
        </SideSheetBody>
        <SideSheetFooter>
          {selectedUnit ? canManage ? <><Button type="button" variant="outline" onClick={() => openEdit(selectedUnit)}>{t("common.edit")}</Button><Button type="button" variant="outline" onClick={() => openCreate(selectedUnit.id)}>{t("administration.areas.organization.addChild")}</Button><Button type="button" variant="outline" onClick={manageSelectedResources}>{t("administration.areas.organization.manageResources")}</Button><Button type="button" variant="outline" onClick={() => changeStatus.mutate({ unit: selectedUnit, active: !selectedUnit.isActive })}>{selectedUnit.isActive ? t("common.deactivate") : t("common.activate")}</Button><Button type="button" variant="destructive" onClick={() => setDeleteTarget(selectedUnit)}>{t("common.delete")}</Button></> : <span className="text-sm text-muted-foreground">{t("administration.areas.organization.readOnly")}</span>
            : tenantRootSelected && canManage ? <Button type="button" onClick={() => openCreate()}><Plus className="me-2 h-4 w-4" />{t("administration.areas.organization.addTopLevel")}</Button>
              : tenantRootSelected ? <span className="text-sm text-muted-foreground">{t("administration.areas.organization.readOnly")}</span> : null}
        </SideSheetFooter>
      </SideSheet>

      <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={editing ? t("administration.areas.organization.editTitle") : t("administration.areas.organization.createTitle")} description={t("administration.areas.organization.formDescription")}>
        <form onSubmit={(e) => void save(e)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-3">
            {reparentError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 p-2 text-sm text-destructive">{reparentError}</p> : null}
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
