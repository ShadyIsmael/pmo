"use client";

import * as React from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ChevronDown, ChevronRight, Expand, Focus, GitBranch, GripVertical, Maximize2, Minimize2, Minus, Move, Plus, Search, Users } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";

const TENANT_ROOT_ID = "__tenant-root__";
const NODE_WIDTH = 236;
const NODE_HEIGHT = 184;
const HORIZONTAL_GAP = 28;
const VERTICAL_GAP = 78;

export type OrganizationUnit = { id: string; code: string; name: string; typeCode: string; typeNameEn?: string; typeNameAr?: string; description?: string; hierarchyPath?: string; parentOrganizationalUnitId?: string | null; parentName?: string; isActive: boolean; sortOrder: number; childCount: number; currentMemberCount: number };
export type OrganizationUnitType = { id: string; code: string; nameEn: string; nameAr: string; displayOrder: number; isActive: boolean };
export type OrganizationMembership = { id: string; resourceId: string; resourceName: string; organizationalUnitId: string; organizationalUnitName: string; isPrimary: boolean; effectiveFrom: string; effectiveTo?: string; isActive: boolean };
export type OrganizationReportingLine = { id: string; resourceId: string; resourceName: string; managerResourceId: string; managerResourceName: string; reportingType: string; isPrimary: boolean; effectiveFrom: string; effectiveTo?: string; isActive: boolean };

type Translate = (key: string, values?: Record<string, string | number>) => string;
type DisplayMode = "structure" | "resources";
type StatusFilter = "all" | "active" | "inactive";
type ResourceFilter = "all" | "with" | "empty";
export type OrganizationViewFilters = { search: string; type: string; status: StatusFilter; resources: ResourceFilter };
type Position = { x: number; y: number; depth: number };
type Layout = { positions: Record<string, Position>; width: number; height: number };

export interface OrganizationalHierarchyFlowProps {
  units: OrganizationUnit[];
  types: OrganizationUnitType[];
  memberships: OrganizationMembership[];
  reportingLines: OrganizationReportingLine[];
  tenantName?: string | null;
  canManage: boolean;
  busy?: boolean;
  onCreate: (parentId?: string) => void;
  onEdit: (unit: OrganizationUnit) => void;
  onStatus: (unit: OrganizationUnit) => void;
  onDelete: (unit: OrganizationUnit) => void;
  onReparent: (unitId: string, parentId: string | null) => Promise<void>;
  onManageResources: (unitId: string) => void;
  filters?: OrganizationViewFilters;
  onFiltersChange?: (filters: OrganizationViewFilters) => void;
  showFilters?: boolean;
  t: Translate;
}

export function OrganizationalHierarchyFlow({ units, types, memberships, reportingLines, tenantName, canManage, busy = false, onCreate, onEdit, onStatus, onDelete, onReparent, onManageResources, filters, onFiltersChange, showFilters = true, t }: OrganizationalHierarchyFlowProps) {
  const [displayMode, setDisplayMode] = useState<DisplayMode>("structure");
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [resourceFilter, setResourceFilter] = useState<ResourceFilter>("all");
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set([TENANT_ROOT_ID]));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [focusId, setFocusId] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [fullScreen, setFullScreen] = useState(false);
  const [chartExpanded, setChartExpanded] = useState(false);
  const [moveParent, setMoveParent] = useState("");
  const [moveError, setMoveError] = useState<string | null>(null);
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);
  const [dragError, setDragError] = useState<string | null>(null);
  const viewportRef = useRef<HTMLDivElement>(null);
  const frameRef = useRef<HTMLDivElement>(null);
  const didInitialFit = useRef(false);
  const expandedInitialized = useRef(false);
  const panStart = useRef<{ x: number; y: number; panX: number; panY: number } | null>(null);
  const tenantLabel = tenantName || t("tenantRoot");
  useEffect(() => {
    if (!filters) return;
    setSearch(filters.search);
    setTypeFilter(filters.type);
    setStatusFilter(filters.status);
    setResourceFilter(filters.resources);
  }, [filters]);
  function updateFilter(partial: Partial<OrganizationViewFilters>) {
    const next: OrganizationViewFilters = { search, type: typeFilter, status: statusFilter, resources: resourceFilter, ...partial };
    setSearch(next.search);
    setTypeFilter(next.type);
    setStatusFilter(next.status);
    setResourceFilter(next.resources);
    onFiltersChange?.(next);
  }
  const unitById = useMemo(() => new Map(units.map((unit) => [unit.id, unit])), [units]);
  const activeMemberships = useMemo(() => memberships.filter((membership) => membership.isActive && (!membership.effectiveTo || new Date(membership.effectiveTo).getTime() > Date.now())), [memberships]);
  const membershipsByUnit = useMemo(() => {
    const result = new Map<string, OrganizationMembership[]>();
    for (const membership of activeMemberships) { const list = result.get(membership.organizationalUnitId) ?? []; list.push(membership); result.set(membership.organizationalUnitId, list); }
    return result;
  }, [activeMemberships]);
  const directResourceIds = useMemo(() => {
    const result = new Map<string, Set<string>>();
    for (const membership of activeMemberships) { const ids = result.get(membership.organizationalUnitId) ?? new Set<string>(); ids.add(membership.resourceId); result.set(membership.organizationalUnitId, ids); }
    return result;
  }, [activeMemberships]);
  const childrenByParent = useMemo(() => {
    const result = new Map<string, OrganizationUnit[]>();
    for (const unit of units) { const key = unit.parentOrganizationalUnitId || TENANT_ROOT_ID; const children = result.get(key) ?? []; children.push(unit); result.set(key, children); }
    for (const children of result.values()) children.sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
    return result;
  }, [units]);
  const subtreeResourceIds = useMemo(() => {
    const cache = new Map<string, Set<string>>();
    const collect = (unitId: string, trail = new Set<string>()): Set<string> => {
      const cached = cache.get(unitId); if (cached) return cached;
      const own = new Set(directResourceIds.get(unitId) ?? []); if (trail.has(unitId)) return own;
      const nextTrail = new Set(trail); nextTrail.add(unitId);
      for (const child of childrenByParent.get(unitId) ?? []) for (const resourceId of collect(child.id, nextTrail)) own.add(resourceId);
      cache.set(unitId, own); return own;
    };
    for (const unit of units) collect(unit.id);
    return cache;
  }, [childrenByParent, directResourceIds, units]);
  const descendantCounts = useMemo(() => {
    const cache = new Map<string, number>();
    const count = (unitId: string, trail = new Set<string>()): number => {
      if (cache.has(unitId)) return cache.get(unitId)!; if (trail.has(unitId)) return 0;
      const nextTrail = new Set(trail); nextTrail.add(unitId);
      const value = (childrenByParent.get(unitId) ?? []).reduce((total, child) => total + 1 + count(child.id, nextTrail), 0); cache.set(unitId, value); return value;
    };
    for (const unit of units) count(unit.id); return cache;
  }, [childrenByParent, units]);
  const searchTerm = search.trim().toLowerCase();
  const matchingUnits = useMemo(() => units.filter((unit) => {
    const unitMemberships = membershipsByUnit.get(unit.id) ?? [];
    const searchable = [unit.name, unit.code, unit.typeCode, unit.typeNameEn, unit.typeNameAr, ...unitMemberships.map((membership) => membership.resourceName)].filter(Boolean).join(" ").toLowerCase();
    const textMatches = !searchTerm || searchable.includes(searchTerm);
    const typeMatches = typeFilter === "all" || unit.typeCode === typeFilter;
    const statusMatches = statusFilter === "all" || (statusFilter === "active" ? unit.isActive : !unit.isActive);
    const directCount = directResourceIds.get(unit.id)?.size ?? 0;
    const resourcesMatch = resourceFilter === "all" || (resourceFilter === "with" ? directCount > 0 : directCount === 0);
    return textMatches && typeMatches && statusMatches && resourcesMatch;
  }), [directResourceIds, membershipsByUnit, resourceFilter, searchTerm, statusFilter, typeFilter, units]);
  const contextIds = useMemo(() => {
    const result = new Set<string>(matchingUnits.map((unit) => unit.id));
    for (const unit of matchingUnits) { const visited = new Set<string>(); let parentId = unit.parentOrganizationalUnitId; while (parentId && !visited.has(parentId)) { visited.add(parentId); result.add(parentId); parentId = unitById.get(parentId)?.parentOrganizationalUnitId ?? null; } }
    if (!searchTerm && typeFilter === "all" && statusFilter === "all" && resourceFilter === "all") for (const unit of units) result.add(unit.id);
    return result;
  }, [matchingUnits, resourceFilter, searchTerm, statusFilter, typeFilter, unitById, units]);
  const visibleNodes = useMemo(() => {
    const result: { id: string; depth: number }[] = [{ id: TENANT_ROOT_ID, depth: 0 }];
    const walk = (parentId: string, depth: number) => { if (!expanded.has(parentId)) return; for (const unit of childrenByParent.get(parentId) ?? []) { if (!contextIds.has(unit.id)) continue; result.push({ id: unit.id, depth }); walk(unit.id, depth + 1); } };
    walk(TENANT_ROOT_ID, 1); return result;
  }, [childrenByParent, contextIds, expanded]);
  const layout = useMemo<Layout>(() => {
    const levels = new Map<number, string[]>();
    for (const node of visibleNodes) { const level = levels.get(node.depth) ?? []; level.push(node.id); levels.set(node.depth, level); }
    const maxNodes = Math.max(1, ...Array.from(levels.values()).map((level) => level.length));
    const maxDepth = Math.max(0, ...visibleNodes.map((node) => node.depth));
    const width = Math.max(980, maxNodes * (NODE_WIDTH + HORIZONTAL_GAP) + 80);
    const height = Math.max(540, (maxDepth + 1) * (NODE_HEIGHT + VERTICAL_GAP) + 80);
    const positions: Record<string, Position> = {};
    for (const [depth, ids] of levels.entries()) { const rowWidth = ids.length * NODE_WIDTH + Math.max(0, ids.length - 1) * HORIZONTAL_GAP; const startX = Math.max(40, (width - rowWidth) / 2); ids.forEach((id, index) => { positions[id] = { x: startX + index * (NODE_WIDTH + HORIZONTAL_GAP), y: 32 + depth * (NODE_HEIGHT + VERTICAL_GAP), depth }; }); }
    return { positions, width, height };
  }, [visibleNodes]);
  const selectedUnit = selectedId && selectedId !== TENANT_ROOT_ID ? unitById.get(selectedId) ?? null : null;
  const selectedMembers = useMemo(() => selectedUnit ? membershipsByUnit.get(selectedUnit.id) ?? [] : [], [membershipsByUnit, selectedUnit]);
  const selectedReportingLines = useMemo(() => { if (!selectedUnit) return []; const ids = new Set(selectedMembers.map((membership) => membership.resourceId)); return reportingLines.filter((line) => ids.has(line.resourceId) || ids.has(line.managerResourceId)); }, [reportingLines, selectedMembers, selectedUnit]);
  const selectedChildren = selectedUnit ? childrenByParent.get(selectedUnit.id) ?? [] : [];
  const blockedParentIdsFor = useCallback((unitId: string) => {
    const blocked = new Set<string>([unitId]);
    const collect = (currentId: string) => {
      for (const child of childrenByParent.get(currentId) ?? []) {
        if (blocked.has(child.id)) continue;
        blocked.add(child.id);
        collect(child.id);
      }
    };
    collect(unitId);
    return blocked;
  }, [childrenByParent]);
  const blockedParentIds = useMemo(() => selectedUnit ? blockedParentIdsFor(selectedUnit.id) : new Set<string>(), [blockedParentIdsFor, selectedUnit]);
  const selectableParents = units.filter((unit) => unit.isActive && !blockedParentIds.has(unit.id));
  const searchResults = searchTerm ? matchingUnits.filter((unit) => { const text = [unit.name, unit.code, unit.typeCode, unit.typeNameEn, unit.typeNameAr, ...(membershipsByUnit.get(unit.id) ?? []).map((membership) => membership.resourceName)].filter(Boolean).join(" ").toLowerCase(); return text.includes(searchTerm); }).slice(0, 8) : [];

  useEffect(() => {
    setExpanded((previous) => {
      const next = expandedInitialized.current ? new Set(previous) : new Set([TENANT_ROOT_ID, ...units.map((unit) => unit.id)]);
      const currentIds = new Set(units.map((unit) => unit.id));
      next.add(TENANT_ROOT_ID);
      for (const id of next) if (id !== TENANT_ROOT_ID && !currentIds.has(id)) next.delete(id);
      expandedInitialized.current = true;
      return next;
    });
  }, [units]);
  useEffect(() => { setMoveParent(selectedUnit?.parentOrganizationalUnitId ?? ""); setMoveError(null); }, [selectedUnit]);
  const fitToScreen = useCallback(() => { const viewport = viewportRef.current; if (!viewport) return; const nextZoom = Math.max(0.45, Math.min(1.15, Math.min((viewport.clientWidth - 48) / layout.width, (viewport.clientHeight - 48) / layout.height))); setZoom(nextZoom); setPan({ x: (viewport.clientWidth - layout.width * nextZoom) / 2, y: (viewport.clientHeight - layout.height * nextZoom) / 2 }); }, [layout.height, layout.width]);
  useEffect(() => { if (didInitialFit.current || !units.length || !viewportRef.current) return; const frame = window.requestAnimationFrame(() => { fitToScreen(); didInitialFit.current = true; }); return () => window.cancelAnimationFrame(frame); }, [fitToScreen, units.length]);
  useEffect(() => { if (!focusId || !layout.positions[focusId] || !viewportRef.current) return; const position = layout.positions[focusId]; setPan({ x: viewportRef.current.clientWidth / 2 - (position.x + NODE_WIDTH / 2) * zoom, y: viewportRef.current.clientHeight / 2 - (position.y + NODE_HEIGHT / 2) * zoom }); setFocusId(null); }, [focusId, layout.positions, zoom]);
  useEffect(() => { const handler = () => setFullScreen(document.fullscreenElement === frameRef.current); document.addEventListener("fullscreenchange", handler); return () => document.removeEventListener("fullscreenchange", handler); }, []);

  function focusUnit(unitId: string) { const next = new Set(expanded); next.add(TENANT_ROOT_ID); let current = unitById.get(unitId); while (current) { next.add(current.id); current = current.parentOrganizationalUnitId ? unitById.get(current.parentOrganizationalUnitId) : undefined; } setExpanded(next); setSelectedId(unitId); setFocusId(unitId); }
  function toggleExpanded(unitId: string) { setExpanded((previous) => { const next = new Set(previous); if (next.has(unitId)) next.delete(unitId); else next.add(unitId); return next; }); }
  function expandBranch(unitId: string, shouldExpand: boolean) { setExpanded((previous) => { const next = new Set(previous); const visit = (id: string) => { if (shouldExpand) next.add(id); else next.delete(id); for (const child of childrenByParent.get(id) ?? []) visit(child.id); }; visit(unitId); return next; }); }
  async function saveMove() { if (!selectedUnit || moveParent === (selectedUnit.parentOrganizationalUnitId ?? "")) return; setMoveError(null); try { await onReparent(selectedUnit.id, moveParent || null); } catch (cause) { setMoveError(cause instanceof Error ? cause.message : t("moveError")); } }
  async function toggleFullscreen() {
    if (!frameRef.current) return;
    if (chartExpanded) { setChartExpanded(false); return; }
    if (document.fullscreenElement) { await document.exitFullscreen(); return; }
    if (typeof frameRef.current.requestFullscreen === "function") {
      try { await frameRef.current.requestFullscreen(); return; } catch { /* fall back to the in-page expanded mode */ }
    }
    setChartExpanded(true);
  }
  function handlePointerDown(event: React.PointerEvent<HTMLDivElement>) { if ((event.target as HTMLElement).closest("[data-graph-node]")) return; panStart.current = { x: event.clientX, y: event.clientY, panX: pan.x, panY: pan.y }; event.currentTarget.setPointerCapture(event.pointerId); }
  function handlePointerMove(event: React.PointerEvent<HTMLDivElement>) { if (!panStart.current) return; setPan({ x: panStart.current.panX + event.clientX - panStart.current.x, y: panStart.current.panY + event.clientY - panStart.current.y }); }
  function handlePointerUp(event: React.PointerEvent<HTMLDivElement>) { panStart.current = null; if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId); }
  function isValidDrop(sourceId: string, targetId: string) {
    const source = unitById.get(sourceId);
    const currentParentId = source?.parentOrganizationalUnitId ?? TENANT_ROOT_ID;
    return Boolean(source) && sourceId !== targetId && currentParentId !== targetId && !blockedParentIdsFor(sourceId).has(targetId);
  }
  function handleDragStart(event: React.DragEvent<HTMLDivElement>, unitId: string) {
    if (!canManage || busy) { event.preventDefault(); return; }
    setDraggedId(unitId);
    setDropTargetId(null);
    setDragError(null);
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", unitId);
  }
  function handleDragEnd() {
    setDraggedId(null);
    setDropTargetId(null);
  }
  function handleDragOver(event: React.DragEvent<HTMLDivElement>, targetId: string) {
    if (!canManage || busy || !draggedId) return;
    event.preventDefault();
    if (!isValidDrop(draggedId, targetId)) {
      event.dataTransfer.dropEffect = "none";
      setDropTargetId(null);
      return;
    }
    event.dataTransfer.dropEffect = "move";
    setDropTargetId(targetId);
  }
  function handleDragLeave(event: React.DragEvent<HTMLDivElement>, targetId: string) {
    const relatedTarget = event.relatedTarget;
    if (!(relatedTarget instanceof Node) && dropTargetId === targetId) setDropTargetId(null);
    else if (relatedTarget instanceof Node && !event.currentTarget.contains(relatedTarget) && dropTargetId === targetId) setDropTargetId(null);
  }
  async function handleDrop(event: React.DragEvent<HTMLDivElement>, targetId: string) {
    event.preventDefault();
    const sourceId = event.dataTransfer.getData("text/plain") || draggedId;
    setDropTargetId(null);
    if (!canManage || busy || !sourceId) return;
    if (!isValidDrop(sourceId, targetId)) {
      setDragError(t("invalidDrop"));
      setDraggedId(null);
      return;
    }
    setDragError(null);
    setDraggedId(null);
    try {
      await onReparent(sourceId, targetId === TENANT_ROOT_ID ? null : targetId);
    } catch (cause) {
      setDragError(cause instanceof Error ? cause.message : t("moveError"));
    }
  }
  function hierarchyPath(unit: OrganizationUnit) { if (unit.hierarchyPath) return `${tenantLabel} → ${unit.hierarchyPath.replace(/ \/ /g, " → ")}`; const path: string[] = [unit.name]; const visited = new Set<string>([unit.id]); let parentId = unit.parentOrganizationalUnitId; while (parentId && !visited.has(parentId)) { visited.add(parentId); const parent = unitById.get(parentId); if (!parent) break; path.unshift(parent.name); parentId = parent.parentOrganizationalUnitId; } return [tenantLabel, ...path].join(" → "); }
  const edgePaths = visibleNodes.filter((node) => node.id !== TENANT_ROOT_ID).flatMap((node) => { const child = unitById.get(node.id); const parent = layout.positions[child?.parentOrganizationalUnitId || TENANT_ROOT_ID]; const current = layout.positions[node.id]; if (!parent || !current) return []; const startX = parent.x + NODE_WIDTH / 2; const startY = parent.y + NODE_HEIGHT; const endX = current.x + NODE_WIDTH / 2; const endY = current.y; const midY = startY + (endY - startY) / 2; return [`M ${startX} ${startY} C ${startX} ${midY}, ${endX} ${midY}, ${endX} ${endY}`]; });
  const chartIsExpanded = chartExpanded || fullScreen;

  if (!units.length) return <EmptyHierarchy canManage={canManage} onCreate={() => onCreate()} t={t} />;
  return <div className="space-y-3" dir="auto">
    <div className={showFilters ? "flex flex-col gap-2 rounded-lg border border-border bg-card p-3 shadow-xs xl:flex-row xl:items-end" : "hidden"}><div className="min-w-0 flex-1 space-y-1.5"><Label htmlFor="organization-hierarchy-search">{t("searchHierarchy")}</Label><div className="relative"><Search className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input id="organization-hierarchy-search" value={search} onChange={(event) => updateFilter({ search: event.target.value })} placeholder={t("searchHierarchyPlaceholder")} className="ps-9" /></div></div><FilterSelect label={t("typeFilter")} value={typeFilter} onChange={(value) => updateFilter({ type: value })} options={[{ value: "all", label: t("allTypes") }, ...types.map((type) => ({ value: type.code, label: type.nameEn }))]} /><FilterSelect label={t("statusFilter")} value={statusFilter} onChange={(value) => updateFilter({ status: value as StatusFilter })} options={[{ value: "all", label: t("allStatuses") }, { value: "active", label: t("active") }, { value: "inactive", label: t("inactive") }]} /><FilterSelect label={t("resourceFilter")} value={resourceFilter} onChange={(value) => updateFilter({ resources: value as ResourceFilter })} options={[{ value: "all", label: t("allUnits") }, { value: "with", label: t("withResources") }, { value: "empty", label: t("emptyUnits") }]} /><div className="flex gap-1 rounded-md border border-border p-1"><Button size="sm" variant={displayMode === "structure" ? "secondary" : "ghost"} onClick={() => setDisplayMode("structure")}>{t("structureMode")}</Button><Button size="sm" variant={displayMode === "resources" ? "secondary" : "ghost"} onClick={() => setDisplayMode("resources")}>{t("resourcesMode")}</Button></div></div>
    {searchResults.length ? <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span>{t("searchResults")}</span>{searchResults.map((unit) => <Button key={unit.id} size="sm" variant="outline" onClick={() => focusUnit(unit.id)}>{unit.name} · {unit.code}</Button>)}</div> : null}
    {!showFilters ? <div className="flex justify-end gap-1 rounded-md border border-border bg-card p-2"><Button size="sm" variant={displayMode === "structure" ? "secondary" : "ghost"} onClick={() => setDisplayMode("structure")}>{t("structureMode")}</Button><Button size="sm" variant={displayMode === "resources" ? "secondary" : "ghost"} onClick={() => setDisplayMode("resources")}>{t("resourcesMode")}</Button></div> : null}
    <div className="flex flex-wrap items-center justify-between gap-2"><div className="flex flex-wrap gap-1"><Button size="sm" variant="outline" onClick={() => setExpanded(new Set([TENANT_ROOT_ID, ...units.map((unit) => unit.id)]))}><Expand className="me-1.5 h-4 w-4" />{t("expandAll")}</Button><Button size="sm" variant="outline" onClick={() => setExpanded(new Set([TENANT_ROOT_ID]))}><ChevronRight className="me-1.5 h-4 w-4" />{t("collapseAll")}</Button><Button size="sm" variant="outline" onClick={fitToScreen}><Focus className="me-1.5 h-4 w-4" />{t("fitToScreen")}</Button><Button size="sm" variant="outline" onClick={() => { setZoom(1); setPan({ x: 0, y: 0 }); }}>{t("resetView")}</Button></div><div className="flex items-center gap-1"><Button size="icon-sm" variant="outline" onClick={() => setZoom((value) => Math.min(1.6, value + 0.1))} aria-label={t("zoomIn")}><Plus className="h-4 w-4" /></Button><span className="min-w-12 text-center text-xs text-muted-foreground">{Math.round(zoom * 100)}%</span><Button size="icon-sm" variant="outline" onClick={() => setZoom((value) => Math.max(0.45, value - 0.1))} aria-label={t("zoomOut")}><Minus className="h-4 w-4" /></Button><Button size="icon-sm" variant="outline" onClick={() => void toggleFullscreen()} aria-label={chartIsExpanded ? t("exitFullscreen") : t("fullscreen")}>{chartIsExpanded ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}</Button></div></div>
    <Card ref={frameRef} className={chartIsExpanded ? "fixed inset-0 z-50 flex min-h-screen flex-col overflow-hidden rounded-none border-border bg-muted/20" : "overflow-hidden border-border bg-muted/20"}>
      <CardHeader className="border-b border-border py-3">
        <div className="flex items-center justify-between gap-3">
          <div>
            <CardTitle className="text-base">{t("flowTitle")}</CardTitle>
            <CardDescription>{t("flowDescription")}</CardDescription>
          </div>
          <div className="hidden items-center gap-3 text-xs text-muted-foreground sm:flex">
            <span className="flex items-center gap-1"><Move className="h-3.5 w-3.5" />{t("panHint")}</span>
            {canManage ? <span className="flex items-center gap-1"><GripVertical className="h-3.5 w-3.5" />{t("dragUnitHint")}</span> : null}
          </div>
        </div>
      </CardHeader>
      <CardContent className={chartIsExpanded ? "flex min-h-0 flex-1 flex-col p-0" : "p-0"}>
        {dragError ? <p role="alert" className="border-b border-destructive/30 bg-destructive/5 px-4 py-2 text-sm text-destructive">{dragError}</p> : null}
        {matchingUnits.length === 0 ? (
          <div className="flex min-h-[540px] flex-col items-center justify-center gap-3 p-8 text-center">
            <GitBranch className="h-10 w-10 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">{t("noMatches")}</p>
            <Button variant="outline" onClick={() => updateFilter({ search: "", type: "all", status: "all", resources: "all" })}>{t("clearFilters")}</Button>
          </div>
        ) : (
          <div
            ref={viewportRef}
            className={chartIsExpanded ? "relative min-h-0 flex-1 overflow-hidden bg-background/50" : "relative min-h-[540px] overflow-hidden bg-background/50"}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerCancel={handlePointerUp}
            onWheel={(event) => { event.preventDefault(); setZoom((value) => Math.max(0.45, Math.min(1.6, value - event.deltaY * 0.001))); }}
            data-graph-surface="true"
          >
            <div className="absolute inset-0 opacity-50 [background-image:radial-gradient(hsl(var(--muted-foreground)/0.25)_1px,transparent_1px)] [background-size:20px_20px]" data-graph-surface="true" />
            <div className="absolute origin-top-left transition-transform duration-150" style={{ width: layout.width, height: layout.height, transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})` }}>
              <svg className="pointer-events-none absolute inset-0 overflow-visible" width={layout.width} height={layout.height} aria-hidden="true">
                <defs><marker id="organization-flow-arrow" markerWidth="8" markerHeight="8" refX="4" refY="4" orient="auto"><path d="M 0 0 L 8 4 L 0 8 z" className="fill-muted-foreground/50" /></marker></defs>
                {edgePaths.map((path) => <path key={path} d={path} fill="none" className="stroke-muted-foreground/45" strokeWidth="1.5" markerEnd="url(#organization-flow-arrow)" />)}
              </svg>
              {visibleNodes.map((node) => {
                const unit = node.id === TENANT_ROOT_ID ? null : unitById.get(node.id) ?? null;
                const position = layout.positions[node.id];
                if (!position) return null;
                const directCount = unit ? directResourceIds.get(unit.id)?.size ?? 0 : 0;
                const totalCount = unit ? subtreeResourceIds.get(unit.id)?.size ?? 0 : Array.from(subtreeResourceIds.values()).reduce((total, value) => total + value.size, 0);
                const childCount = unit ? childrenByParent.get(unit.id)?.length ?? 0 : childrenByParent.get(TENANT_ROOT_ID)?.length ?? 0;
                const hiddenCount = unit && !expanded.has(unit.id) ? descendantCounts.get(unit.id) ?? 0 : 0;
                const dropAllowed = draggedId ? isValidDrop(draggedId, node.id) : false;
                return <FlowNode
                  key={node.id}
                  unit={unit}
                  position={position}
                  tenantLabel={tenantLabel}
                  selected={selectedId === node.id}
                  expanded={expanded.has(node.id)}
                  isRoot={node.id === TENANT_ROOT_ID}
                  directCount={directCount}
                  totalCount={totalCount}
                  childCount={childCount}
                  hiddenCount={hiddenCount}
                  memberships={unit ? membershipsByUnit.get(unit.id) ?? [] : []}
                  displayMode={displayMode}
                  canManage={canManage}
                  dragging={draggedId === node.id}
                  dropTarget={dropTargetId === node.id && dropAllowed}
                  onSelect={() => setSelectedId(node.id)}
                  onToggle={() => toggleExpanded(node.id)}
                  onExpandBranch={() => expandBranch(node.id, true)}
                  onCollapseBranch={() => expandBranch(node.id, false)}
                  onAddChild={() => onCreate(unit?.id)}
                  onDragStart={(event) => handleDragStart(event, node.id)}
                  onDragEnd={handleDragEnd}
                  onDragOver={(event) => handleDragOver(event, node.id)}
                  onDragLeave={(event) => handleDragLeave(event, node.id)}
                  onDrop={(event) => void handleDrop(event, node.id)}
                  t={t}
                />;
              })}
            </div>
            {visibleNodes.length > 10 ? <MiniMap layout={layout} visibleNodes={visibleNodes} unitById={unitById} selectedId={selectedId} /> : null}
          </div>
        )}
      </CardContent>
    </Card>
    <SideSheet open={Boolean(selectedId)} onOpenChange={(open) => { if (!open) setSelectedId(null); }} title={selectedUnit?.name ?? tenantLabel} description={selectedUnit ? selectedUnit.typeNameEn ?? selectedUnit.typeCode : t("virtualRoot")} size="lg"><SideSheetBody className="space-y-5">{selectedUnit ? <><div className="flex flex-wrap items-center gap-2"><Badge variant={selectedUnit.isActive ? "success" : "outline"}>{selectedUnit.isActive ? t("active") : t("inactive")}</Badge><span className="font-mono text-xs text-muted-foreground">{selectedUnit.code}</span></div><DetailSection title={t("hierarchyPath")}><p className="text-sm leading-6 text-foreground">{hierarchyPath(selectedUnit)}</p></DetailSection><div className="grid gap-3 sm:grid-cols-2"><DetailValue label={t("type")} value={selectedUnit.typeNameEn ?? selectedUnit.typeCode} /><DetailValue label={t("parent")} value={selectedUnit.parentName ?? t("root")} /><DetailValue label={t("directResources")} value={String(directResourceIds.get(selectedUnit.id)?.size ?? 0)} /><DetailValue label={t("totalResources")} value={String(subtreeResourceIds.get(selectedUnit.id)?.size ?? 0)} /><DetailValue label={t("childUnits")} value={String(selectedChildren.length)} /></div>{selectedUnit.description ? <DetailSection title={t("descriptionField")}><p className="text-sm text-muted-foreground">{selectedUnit.description}</p></DetailSection> : null}<DetailSection title={t("reparentUnit")}><div className="space-y-2"><Label htmlFor="organization-reparent">{t("newParent")}</Label><select id="organization-reparent" disabled={!canManage || busy} value={moveParent} onChange={(event) => setMoveParent(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="">{t("root")}</option>{selectableParents.map((parent) => <option key={parent.id} value={parent.id}>{parent.name} · {parent.code}</option>)}</select>{moveError ? <p className="text-xs text-destructive">{moveError}</p> : null}<Button className="w-full" variant="outline" disabled={!canManage || busy || moveParent === (selectedUnit.parentOrganizationalUnitId ?? "")} onClick={() => void saveMove()}>{t("saveMove")}</Button></div></DetailSection><DetailSection title={t("resourceMembers")}><div className="space-y-2">{selectedMembers.length ? selectedMembers.map((membership) => <div key={membership.id} className="flex items-center justify-between rounded-md border border-success/30 bg-success/5 px-3 py-2 text-sm"><span>{membership.resourceName}</span>{membership.isPrimary ? <Badge variant="outline">{t("primary")}</Badge> : null}</div>) : <p className="text-sm text-muted-foreground">{t("noCurrentMembers")}</p>}</div></DetailSection><DetailSection title={t("reportingStructure")}><div className="space-y-2">{selectedReportingLines.length ? selectedReportingLines.map((line) => <div key={line.id} className="rounded-md border border-border px-3 py-2 text-sm"><p className="font-medium">{line.resourceName} → {line.managerResourceName}</p><p className="text-xs text-muted-foreground">{line.reportingType}</p></div>) : <p className="text-sm text-muted-foreground">{t("noReportingLines")}</p>}</div></DetailSection></> : <><DetailSection title={t("hierarchyPath")}><p className="text-sm text-foreground">{tenantLabel}</p></DetailSection><p className="text-sm text-muted-foreground">{t("tenantRootDescription")}</p><DetailSection title={t("childUnits")}><p className="text-sm">{childrenByParent.get(TENANT_ROOT_ID)?.length ?? 0}</p></DetailSection></>}</SideSheetBody><SideSheetFooter>{selectedUnit ? <>{canManage ? <><Button variant="outline" onClick={() => onEdit(selectedUnit)}>{t("edit")}</Button><Button variant="outline" onClick={() => onCreate(selectedUnit.id)}><Plus className="me-1.5 h-4 w-4" />{t("addChild")}</Button><Button variant="outline" onClick={() => onManageResources(selectedUnit.id)}><Users className="me-1.5 h-4 w-4" />{t("manageResources")}</Button><Button variant="outline" onClick={() => void onStatus(selectedUnit)}>{selectedUnit.isActive ? t("deactivate") : t("reactivate")}</Button></> : <Badge variant="outline">{t("readOnly")}</Badge>}</> : canManage ? <Button onClick={() => onCreate()}><Plus className="me-1.5 h-4 w-4" />{t("addTopLevel")}</Button> : <Badge variant="outline">{t("readOnly")}</Badge>}</SideSheetFooter></SideSheet>
  </div>;
}

function FlowNode({
  unit,
  position,
  tenantLabel,
  selected,
  expanded,
  isRoot,
  directCount,
  totalCount,
  childCount,
  hiddenCount,
  memberships,
  displayMode,
  canManage,
  dragging,
  dropTarget,
  onSelect,
  onToggle,
  onExpandBranch,
  onCollapseBranch,
  onAddChild,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDragLeave,
  onDrop,
  t,
}: {
  unit: OrganizationUnit | null;
  position: Position;
  tenantLabel: string;
  selected: boolean;
  expanded: boolean;
  isRoot: boolean;
  directCount: number;
  totalCount: number;
  childCount: number;
  hiddenCount: number;
  memberships: OrganizationMembership[];
  displayMode: DisplayMode;
  canManage: boolean;
  dragging: boolean;
  dropTarget: boolean;
  onSelect: () => void;
  onToggle: () => void;
  onExpandBranch: () => void;
  onCollapseBranch: () => void;
  onAddChild: () => void;
  onDragStart: (event: React.DragEvent<HTMLDivElement>) => void;
  onDragEnd: () => void;
  onDragOver: (event: React.DragEvent<HTMLDivElement>) => void;
  onDragLeave: (event: React.DragEvent<HTMLDivElement>) => void;
  onDrop: (event: React.DragEvent<HTMLDivElement>) => void;
  t: Translate;
}) {
  const tone = isRoot ? "border-primary/50 bg-primary/5" : typeTone(unit?.typeCode ?? "");
  const nodeId = unit?.id ?? TENANT_ROOT_ID;
  return (
    <div
      data-graph-node="true"
      data-unit-id={nodeId}
      role="button"
      tabIndex={0}
      draggable={canManage && !isRoot}
      aria-pressed={selected}
      aria-grabbed={dragging}
      onClick={onSelect}
      onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); onSelect(); } }}
      onDragStart={(event) => { event.stopPropagation(); if (!isRoot) onDragStart(event); }}
      onDragEnd={onDragEnd}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      className={`absolute rounded-lg border shadow-sm transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${tone} ${selected ? "ring-2 ring-primary ring-offset-2 ring-offset-background" : ""} ${dragging ? "opacity-60" : ""} ${dropTarget ? "ring-2 ring-primary ring-offset-2 ring-offset-background bg-primary/10" : ""} ${canManage && !isRoot ? "cursor-grab active:cursor-grabbing" : ""}`}
      style={{ width: NODE_WIDTH, minHeight: NODE_HEIGHT, left: position.x, top: position.y }}
    >
      <div className="flex items-start gap-2 border-b border-border/70 px-3 py-2">
        <button type="button" className="mt-0.5 shrink-0 text-muted-foreground" onClick={(event) => { event.stopPropagation(); onToggle(); }} aria-label={expanded ? t("collapse") : t("expand")}>
          {childCount ? (expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />) : <span className="inline-block h-4 w-4" />}
        </button>
        {canManage && !isRoot ? <GripVertical className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" /> : null}
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{isRoot ? tenantLabel : unit?.name}</p>
          <p className="truncate text-[10px] uppercase tracking-wider text-muted-foreground">{isRoot ? t("virtualRoot") : unit?.typeNameEn ?? unit?.typeCode}</p>
        </div>
        {canManage && <button type="button" className="rounded p-0.5 text-muted-foreground hover:bg-muted hover:text-foreground" onClick={(event) => { event.stopPropagation(); onAddChild(); }} aria-label={isRoot ? t("addTopLevel") : t("addChild")}><Plus className="h-4 w-4" /></button>}
      </div>
      <div className="space-y-2 px-3 py-2">
        <div className="flex items-center justify-between gap-2"><span className="font-mono text-[10px] text-muted-foreground">{isRoot ? "TENANT" : unit?.code}</span>{isRoot ? <Badge variant="outline">{t("virtualRoot")}</Badge> : <Badge variant={unit?.isActive ? "success" : "outline"}>{unit?.isActive ? t("active") : t("inactive")}</Badge>}</div>
        <div className="grid grid-cols-2 gap-2 text-[11px]"><div className="rounded border border-border/70 px-2 py-1"><span className="block text-muted-foreground">{t("directResources")}</span><span className="font-semibold">{directCount}</span></div><div className="rounded border border-border/70 px-2 py-1"><span className="block text-muted-foreground">{t("totalResources")}</span><span className="font-semibold">{totalCount}</span></div></div>
        <p className="text-[11px] text-muted-foreground">{t("childUnits")}: {childCount}</p>
        {hiddenCount > 0 ? <p className="text-[11px] font-medium text-primary">+ {hiddenCount} {t("hiddenUnits")}</p> : null}
        {displayMode === "resources" && !isRoot ? <div className="space-y-1 border-t border-border/70 pt-2">{memberships.slice(0, 3).map((membership) => <div key={membership.id} className="flex items-center gap-1.5 truncate rounded border border-success/30 bg-success/5 px-2 py-1 text-[10px] text-success"><Users className="h-3 w-3 shrink-0" />{membership.resourceName}</div>)}{memberships.length > 3 ? <p className="text-[10px] text-muted-foreground">+ {memberships.length - 3} {t("moreResources")}</p> : null}</div> : null}
      </div>
      {unit && <div className="flex items-center justify-end gap-1 border-t border-border/70 px-3 py-1.5"><button type="button" className="text-[10px] text-muted-foreground hover:text-foreground" onClick={(event) => { event.stopPropagation(); onExpandBranch(); }}>{t("expandBranch")}</button><span className="text-muted-foreground">·</span><button type="button" className="text-[10px] text-muted-foreground hover:text-foreground" onClick={(event) => { event.stopPropagation(); onCollapseBranch(); }}>{t("collapseBranch")}</button></div>}
    </div>
  );
}

function MiniMap({ layout, visibleNodes, unitById, selectedId }: { layout: Layout; visibleNodes: { id: string; depth: number }[]; unitById: Map<string, OrganizationUnit>; selectedId: string | null }) {
  const paths = visibleNodes.filter((node) => node.id !== TENANT_ROOT_ID).flatMap((node) => { const current = layout.positions[node.id]; const unit = unitById.get(node.id); const parent = layout.positions[unit?.parentOrganizationalUnitId || TENANT_ROOT_ID]; if (!current || !parent) return []; return [`M ${parent.x + NODE_WIDTH / 2} ${parent.y + NODE_HEIGHT} L ${current.x + NODE_WIDTH / 2} ${current.y}`]; }).join(" ");
  return <div className="pointer-events-none absolute bottom-3 end-3 hidden w-44 rounded-md border border-border bg-background/90 p-2 shadow-md lg:block"><svg viewBox={`0 0 ${layout.width} ${layout.height}`} className="h-auto w-full"><path d={paths} className="stroke-muted-foreground/35" strokeWidth="8" fill="none" />{visibleNodes.map((node) => { const position = layout.positions[node.id]; if (!position) return null; return <rect key={node.id} x={position.x} y={position.y} width={NODE_WIDTH} height={NODE_HEIGHT} rx="8" className={node.id === selectedId ? "fill-primary" : node.id === TENANT_ROOT_ID ? "fill-primary/40" : "fill-muted-foreground/25"} />; })}</svg></div>;
}

function FilterSelect({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: { value: string; label: string }[] }) { return <div className="min-w-[145px] space-y-1.5"><Label className="text-xs">{label}</Label><select value={value} onChange={(event) => onChange(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div>; }
function DetailSection({ title, children }: { title: string; children: React.ReactNode }) { return <section className="space-y-2"><h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{title}</h3>{children}</section>; }
function DetailValue({ label, value }: { label: string; value: string }) { return <div className="rounded-md border border-border px-3 py-2"><p className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</p><p className="mt-1 text-sm font-medium">{value}</p></div>; }
function EmptyHierarchy({ canManage, onCreate, t }: { canManage: boolean; onCreate: () => void; t: Translate }) { return <div className="flex min-h-[340px] flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border p-8 text-center"><GitBranch className="h-10 w-10 text-muted-foreground" /><p className="text-sm text-muted-foreground">{t("emptyStructure")}</p>{canManage ? <Button onClick={onCreate}><Plus className="me-2 h-4 w-4" />{t("createFirstUnit")}</Button> : null}</div>; }
function typeTone(typeCode: string) { const normalized = typeCode.toLowerCase(); if (normalized.includes("division")) return "border-info/50 bg-info/5"; if (normalized.includes("department")) return "border-primary/45 bg-primary/5"; if (normalized.includes("section")) return "border-warning/50 bg-warning/5"; if (normalized.includes("branch")) return "border-success/45 bg-success/5"; if (normalized.includes("function")) return "border-secondary bg-secondary/30"; return "border-border bg-card"; }
