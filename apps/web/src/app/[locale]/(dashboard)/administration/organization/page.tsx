"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { GitBranch, Loader2, Pencil, Plus, Power, Trash2, Users } from "lucide-react";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageHeader } from "@/components/layout/PageHeader";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api, type PagedResult } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useManagementPerspective } from "@/lib/perspective-context";
import { useResources } from "@/hooks/resources/useResources";
import { OrganizationalHierarchyFlow, type OrganizationMembership, type OrganizationReportingLine, type OrganizationUnit, type OrganizationUnitType, type OrganizationViewFilters } from "@/components/organization/OrganizationalHierarchyFlow";

type StructureView = "chart" | "table";
type UnitForm = { name: string; typeCode: string; parentOrganizationalUnitId: string; description: string; sortOrder: number; isActive: boolean };
const emptyForm: UnitForm = { name: "", typeCode: "", parentOrganizationalUnitId: "", description: "", sortOrder: 0, isActive: true };

export default function AdministrationOrganizationPage() {
  const t = useTranslations("administration.areas.organization");
  const { user } = useAuth();
  const { effectiveReadOnly, canMutate } = useManagementPerspective();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Settings.Write) && canMutate && !effectiveReadOnly);
  const [units, setUnits] = useState<OrganizationUnit[]>([]);
  const [types, setTypes] = useState<OrganizationUnitType[]>([]);
  const [memberships, setMemberships] = useState<OrganizationMembership[]>([]);
  const [reportingLines, setReportingLines] = useState<OrganizationReportingLine[]>([]);
  const [tenantName, setTenantName] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const initialLoadRef = useRef(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState("structure");
  const [structureView, setStructureView] = useState<StructureView>("chart");
  const [hierarchyFilters, setHierarchyFilters] = useState<OrganizationViewFilters>({ search: "", type: "all", status: "all", resources: "all" });
  const [sheetOpen, setSheetOpen] = useState(false);
  const [editing, setEditing] = useState<OrganizationUnit | null>(null);
  const [form, setForm] = useState<UnitForm>(emptyForm);
  const [membershipForm, setMembershipForm] = useState({ resourceId: "", organizationalUnitId: "", isPrimary: true });
  const [reportingForm, setReportingForm] = useState({ resourceId: "", managerResourceId: "", reportingType: "LineManager" });
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });
  const resources = useMemo(() => (resourcesQuery.data?.items ?? []).map((resource) => ({ id: resource.id, name: resource.name })), [resourcesQuery.data]);

  const load = useCallback(async () => {
    const showLoadingState = initialLoadRef.current;
    if (showLoadingState) setLoading(true);
    setError(null);
    try {
      const [unitData, typeData, membershipData, reportingData] = await Promise.all([
        api.get<OrganizationUnit[]>(endpoints.organization.units, { includeInactive: true }),
        api.get<OrganizationUnitType[]>(endpoints.organization.types),
        api.get<OrganizationMembership[]>(endpoints.organization.memberships),
        api.get<OrganizationReportingLine[]>(endpoints.organization.reportingLines),
      ]);
      setUnits(unitData);
      setTypes(typeData);
      setMemberships(membershipData);
      setReportingLines(reportingData);
      setMembershipForm((current) => ({ ...current, organizationalUnitId: current.organizationalUnitId || unitData.find((unit) => unit.isActive)?.id || "" }));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("loadError"));
    } finally {
      if (showLoadingState) setLoading(false);
      initialLoadRef.current = false;
    }
  }, [t]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    let cancelled = false;
    void api.get<PagedResult<{ name?: string }>>("/api/organizations", { page: 1, pageSize: 1 }).then((data) => { if (!cancelled) setTenantName(data.items[0]?.name ?? null); }).catch(() => { if (!cancelled) setTenantName(null); });
    return () => { cancelled = true; };
  }, []);

  function openCreate(parentId = "") {
    if (!canManage) return;
    setEditing(null);
    setForm({ ...emptyForm, typeCode: types.find((type) => type.isActive)?.code ?? "", parentOrganizationalUnitId: parentId });
    setSheetOpen(true);
  }
  function openEdit(unit: OrganizationUnit) {
    if (!canManage) return;
    setEditing(unit);
    setForm({ name: unit.name, typeCode: unit.typeCode, parentOrganizationalUnitId: unit.parentOrganizationalUnitId ?? "", description: unit.description ?? "", sortOrder: unit.sortOrder, isActive: unit.isActive });
    setSheetOpen(true);
  }
  async function saveUnit(event: FormEvent) {
    event.preventDefault();
    if (!canManage) return;
    if (editing && form.parentOrganizationalUnitId === editing.id) { setError(t("selfParentError")); return; }
    const typeCode = form.typeCode || types.find((type) => type.isActive)?.code || "Department";
    setSaving(true);
    setError(null);
    try {
      const payload = { ...form, typeCode, parentOrganizationalUnitId: form.parentOrganizationalUnitId || null };
      if (editing) await api.put(endpoints.organization.unit(editing.id), payload);
      else await api.post(endpoints.organization.units, payload);
      setSheetOpen(false);
      await load();
    } catch (cause) {
      setError(cause instanceof Error && cause.message.includes("cannot be its own parent") ? t("selfParentError") : cause instanceof Error ? cause.message : t("saveError"));
    } finally {
      setSaving(false);
    }
  }
  async function reparentUnit(unitId: string, parentId: string | null) {
    if (!canManage) return;
    const unit = units.find((item) => item.id === unitId);
    if (!unit) return;
    setSaving(true);
    setError(null);
    try {
      await api.put(endpoints.organization.unit(unitId), { name: unit.name, typeCode: unit.typeCode, description: unit.description, parentOrganizationalUnitId: parentId, sortOrder: unit.sortOrder, isActive: unit.isActive });
      await load();
    } catch (cause) {
      throw cause instanceof Error ? cause : new Error(t("moveError"));
    } finally {
      setSaving(false);
    }
  }
  async function changeStatus(unit: OrganizationUnit) {
    if (!canManage) return;
    try { await api.post(unit.isActive ? endpoints.organization.deactivateUnit(unit.id) : endpoints.organization.reactivateUnit(unit.id)); await load(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("saveError")); }
  }
  async function deleteUnit(unit: OrganizationUnit) {
    if (!canManage || !window.confirm(t("deleteConfirm", { name: unit.name }))) return;
    try { await api.delete(endpoints.organization.unit(unit.id)); await load(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("deleteError")); }
  }
  async function assignMembership(event: FormEvent) {
    event.preventDefault();
    if (!canManage || !membershipForm.resourceId || !membershipForm.organizationalUnitId) return;
    setSaving(true);
    try { await api.post(endpoints.organization.memberships, { ...membershipForm, effectiveFrom: new Date().toISOString() }); setMembershipForm((current) => ({ ...current, resourceId: "" })); await load(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("saveError")); } finally { setSaving(false); }
  }
  async function assignReporting(event: FormEvent) {
    event.preventDefault();
    if (!canManage || !reportingForm.resourceId || !reportingForm.managerResourceId) return;
    setSaving(true);
    try { await api.post(endpoints.organization.reportingLines, { ...reportingForm, isPrimary: true, effectiveFrom: new Date().toISOString() }); setReportingForm({ resourceId: "", managerResourceId: "", reportingType: "LineManager" }); await load(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("saveError")); } finally { setSaving(false); }
  }
  async function endMembership(id: string) { try { await api.post(endpoints.organization.endMembership(id), { effectiveTo: new Date().toISOString() }); await load(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("saveError")); } }
  async function endReporting(id: string) { try { await api.post(endpoints.organization.endReportingLine(id), { effectiveTo: new Date().toISOString() }); await load(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("saveError")); } }

  const activeUnits = units.filter((unit) => unit.isActive);
  const filteredUnits = useMemo(() => {
    const now = Date.now();
    const resourceNamesByUnit = new Map<string, string[]>();
    for (const membership of memberships) {
      if (!membership.isActive || (membership.effectiveTo && new Date(membership.effectiveTo).getTime() <= now)) continue;
      const names = resourceNamesByUnit.get(membership.organizationalUnitId) ?? [];
      names.push(membership.resourceName);
      resourceNamesByUnit.set(membership.organizationalUnitId, names);
    }
    const searchTerm = hierarchyFilters.search.trim().toLowerCase();
    return units.filter((unit) => {
      const resourceNames = resourceNamesByUnit.get(unit.id) ?? [];
      const searchable = [unit.name, unit.code, unit.typeCode, unit.typeNameEn, unit.typeNameAr, ...resourceNames].filter(Boolean).join(" ").toLowerCase();
      const textMatches = !searchTerm || searchable.includes(searchTerm);
      const typeMatches = hierarchyFilters.type === "all" || unit.typeCode === hierarchyFilters.type;
      const statusMatches = hierarchyFilters.status === "all" || (hierarchyFilters.status === "active" ? unit.isActive : !unit.isActive);
      const resourceCount = resourceNames.length;
      const resourceMatches = hierarchyFilters.resources === "all" || (hierarchyFilters.resources === "with" ? resourceCount > 0 : resourceCount === 0);
      return textMatches && typeMatches && statusMatches && resourceMatches;
    });
  }, [hierarchyFilters, memberships, units]);
  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} actions={canManage ? <Button onClick={() => openCreate()}><Plus className="me-2 h-4 w-4" />{t("addUnit")}</Button> : <Badge variant="outline">{t("readOnly")}</Badge>} />{error ? <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive">{error}</div> : null}<div className="grid gap-4 sm:grid-cols-3"><Summary title={t("units")} value={activeUnits.length} icon={<GitBranch className="h-4 w-4" />} /><Summary title={t("memberships")} value={memberships.length} icon={<Users className="h-4 w-4" />} /><Summary title={t("reportingLines")} value={reportingLines.length} icon={<GitBranch className="h-4 w-4" />} /></div><Tabs value={activeTab} onValueChange={setActiveTab}><TabsList><TabsTrigger value="structure">{t("structureTab")}</TabsTrigger><TabsTrigger value="memberships">{t("membershipTab")}</TabsTrigger><TabsTrigger value="reporting">{t("reportingTab")}</TabsTrigger></TabsList><TabsContent value="structure" className="space-y-4"><OrganizationHierarchyFilters filters={hierarchyFilters} types={types} onChange={setHierarchyFilters} t={t} /><Card><CardHeader><div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><CardTitle>{t("structureTitle")}</CardTitle><CardDescription>{t("structureDescription")}</CardDescription></div><div className="flex shrink-0 rounded-md border border-border p-1" role="tablist" aria-label={t("viewMode")}><Button type="button" size="sm" variant={structureView === "chart" ? "secondary" : "ghost"} aria-pressed={structureView === "chart"} onClick={() => setStructureView("chart")}>{t("chartView")}</Button><Button type="button" size="sm" variant={structureView === "table" ? "secondary" : "ghost"} aria-pressed={structureView === "table"} onClick={() => setStructureView("table")}>{t("tableView")}</Button></div></div></CardHeader><CardContent>{loading ? <Loading /> : structureView === "chart" ? <OrganizationalHierarchyFlow units={units} types={types} memberships={memberships} reportingLines={reportingLines} tenantName={tenantName} canManage={canManage} busy={saving} onCreate={openCreate} onEdit={openEdit} onStatus={(unit) => void changeStatus(unit)} onDelete={(unit) => void deleteUnit(unit)} onReparent={reparentUnit} onManageResources={(unitId) => { setMembershipForm((current) => ({ ...current, organizationalUnitId: unitId })); setActiveTab("memberships"); }} filters={hierarchyFilters} onFiltersChange={setHierarchyFilters} showFilters={false} t={t} /> : <OrganizationStructureTable units={filteredUnits} emptyLabel={hierarchyFilters.search.trim() || hierarchyFilters.type !== "all" || hierarchyFilters.status !== "all" || hierarchyFilters.resources !== "all" ? t("noMatches") : t("emptyStructure")} canManage={canManage} onEdit={openEdit} onStatus={(unit) => void changeStatus(unit)} onDelete={(unit) => void deleteUnit(unit)} t={t} />}</CardContent></Card></TabsContent><TabsContent value="memberships"><Card><CardHeader><CardTitle>{t("membershipTitle")}</CardTitle><CardDescription>{t("membershipDescription")}</CardDescription></CardHeader><CardContent className="space-y-5"><form onSubmit={assignMembership} className="grid gap-3 md:grid-cols-[1fr_1fr_auto_auto]"><SelectField label={t("resource")} value={membershipForm.resourceId} onChange={(value) => setMembershipForm((current) => ({ ...current, resourceId: value }))} options={resources.map((resource) => ({ value: resource.id, label: resource.name }))} placeholder={t("selectResource")} /><SelectField label={t("unit")} value={membershipForm.organizationalUnitId} onChange={(value) => setMembershipForm((current) => ({ ...current, organizationalUnitId: value }))} options={activeUnits.map((unit) => ({ value: unit.id, label: `${unit.name} · ${unit.code}` }))} placeholder={t("selectUnit")} /><label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={membershipForm.isPrimary} disabled={!canManage} onChange={(event) => setMembershipForm((current) => ({ ...current, isPrimary: event.target.checked }))} />{t("primary")}</label><Button type="submit" disabled={!canManage || saving} className="self-end">{saving ? <Loader2 className="h-4 w-4" /> : <Plus className="me-2 h-4 w-4" />}{t("assign")}</Button></form><MembershipTable rows={memberships} onEnd={endMembership} canManage={canManage} t={t} /></CardContent></Card></TabsContent><TabsContent value="reporting"><Card><CardHeader><CardTitle>{t("reportingTitle")}</CardTitle><CardDescription>{t("reportingDescription")}</CardDescription></CardHeader><CardContent className="space-y-5"><form onSubmit={assignReporting} className="grid gap-3 md:grid-cols-[1fr_1fr_1fr_auto]"><SelectField label={t("resource")} value={reportingForm.resourceId} onChange={(value) => setReportingForm((current) => ({ ...current, resourceId: value }))} options={resources.map((resource) => ({ value: resource.id, label: resource.name }))} placeholder={t("selectResource")} /><SelectField label={t("manager")} value={reportingForm.managerResourceId} onChange={(value) => setReportingForm((current) => ({ ...current, managerResourceId: value }))} options={resources.filter((resource) => resource.id !== reportingForm.resourceId).map((resource) => ({ value: resource.id, label: resource.name }))} placeholder={t("selectManager")} /><div className="space-y-1.5"><Label>{t("reportingType")}</Label><Input value={reportingForm.reportingType} disabled={!canManage} onChange={(event) => setReportingForm((current) => ({ ...current, reportingType: event.target.value }))} /></div><Button type="submit" disabled={!canManage || saving} className="self-end">{saving ? <Loader2 className="h-4 w-4" /> : <Plus className="me-2 h-4 w-4" />}{t("assign")}</Button></form><ReportingTable rows={reportingLines} onEnd={endReporting} canManage={canManage} t={t} /></CardContent></Card></TabsContent></Tabs><SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={editing ? t("editTitle") : t("createTitle")} description={t("formDescription")} size="md"><form onSubmit={saveUnit} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4"><div className="space-y-1.5"><Label htmlFor="org-unit-name">{t("name")}</Label><Input id="org-unit-name" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} required /></div><SelectField label={t("type")} value={form.typeCode} onChange={(value) => setForm((current) => ({ ...current, typeCode: value }))} options={types.filter((type) => type.isActive).map((type) => ({ value: type.code, label: type.nameEn }))} placeholder={t("selectType")} /><SelectField label={t("parent")} value={form.parentOrganizationalUnitId} onChange={(value) => setForm((current) => ({ ...current, parentOrganizationalUnitId: value }))} options={activeUnits.filter((unit) => unit.id !== editing?.id).map((unit) => ({ value: unit.id, label: `${unit.name} · ${unit.code}` }))} placeholder={t("root")} /><div className="space-y-1.5"><Label htmlFor="org-unit-description">{t("descriptionField")}</Label><textarea id="org-unit-description" className="min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={form.description} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></div></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>{t("cancel")}</Button><Button type="submit" disabled={!canManage || saving}>{saving ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : null}{t("save")}</Button></SideSheetFooter></form></SideSheet></div>;
}

function OrganizationHierarchyFilters({ filters, types, onChange, t }: { filters: OrganizationViewFilters; types: OrganizationUnitType[]; onChange: (filters: OrganizationViewFilters) => void; t: Translate }) { const update = (partial: Partial<OrganizationViewFilters>) => onChange({ ...filters, ...partial }); return <div className="flex flex-col gap-3 rounded-lg border border-border bg-card p-3 shadow-xs xl:flex-row xl:items-end"><div className="min-w-0 flex-1 space-y-1.5"><Label htmlFor="organization-shared-search">{t("searchHierarchy")}</Label><Input id="organization-shared-search" value={filters.search} onChange={(event) => update({ search: event.target.value })} placeholder={t("searchHierarchyPlaceholder")} /></div><SharedFilterSelect label={t("typeFilter")} value={filters.type} onChange={(value) => update({ type: value })} options={[{ value: "all", label: t("allTypes") }, ...types.map((type) => ({ value: type.code, label: type.nameEn }))]} /><SharedFilterSelect label={t("statusFilter")} value={filters.status} onChange={(value) => update({ status: value as OrganizationViewFilters["status"] })} options={[{ value: "all", label: t("allStatuses") }, { value: "active", label: t("active") }, { value: "inactive", label: t("inactive") }]} /><SharedFilterSelect label={t("resourceFilter")} value={filters.resources} onChange={(value) => update({ resources: value as OrganizationViewFilters["resources"] })} options={[{ value: "all", label: t("allUnits") }, { value: "with", label: t("withResources") }, { value: "empty", label: t("emptyUnits") }]} /><Button type="button" variant="outline" onClick={() => onChange({ search: "", type: "all", status: "all", resources: "all" })}>{t("clearFilters")}</Button></div>; }
function SharedFilterSelect({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: { value: string; label: string }[] }) { return <div className="min-w-[145px] space-y-1.5"><Label className="text-xs">{label}</Label><select value={value} onChange={(event) => onChange(event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm">{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div>; }
function OrganizationStructureTable({ units, canManage, onEdit, onStatus, onDelete, emptyLabel, t }: { units: OrganizationUnit[]; canManage: boolean; onEdit: (unit: OrganizationUnit) => void; onStatus: (unit: OrganizationUnit) => void; onDelete: (unit: OrganizationUnit) => void; emptyLabel: string; t: Translate }) { return units.length === 0 ? <p className="py-12 text-center text-sm text-muted-foreground">{emptyLabel}</p> : <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th className="px-4 py-3 text-start">{t("code")}</th><th className="px-4 py-3 text-start">{t("name")}</th><th className="px-4 py-3 text-start">{t("type")}</th><th className="px-4 py-3 text-start">{t("parent")}</th><th className="px-4 py-3 text-start">{t("members")}</th><th className="px-4 py-3 text-start">{t("status")}</th><th className="px-4 py-3 text-end">{t("actions")}</th></tr></thead><tbody>{units.map((unit) => <tr key={unit.id} className="border-b last:border-0"><td className="px-4 py-3 font-mono text-xs">{unit.code}</td><td className="px-4 py-3 font-medium">{unit.name}</td><td className="px-4 py-3">{unit.typeNameEn ?? unit.typeCode}</td><td className="px-4 py-3 text-muted-foreground">{unit.parentName ?? t("root")}</td><td className="px-4 py-3">{unit.currentMemberCount}</td><td className="px-4 py-3"><Badge variant={unit.isActive ? "success" : "outline"}>{unit.isActive ? t("active") : t("inactive")}</Badge></td><td className="px-4 py-3"><div className="flex justify-end gap-1">{canManage ? <><Button variant="ghost" size="icon-sm" onClick={() => onEdit(unit)} aria-label={t("edit")}><Pencil className="h-4 w-4" /></Button><Button variant="ghost" size="icon-sm" onClick={() => onStatus(unit)} aria-label={unit.isActive ? t("deactivate") : t("reactivate")}><Power className="h-4 w-4" /></Button><Button variant="ghost" size="icon-sm" onClick={() => onDelete(unit)} aria-label={t("delete")}><Trash2 className="h-4 w-4" /></Button></> : null}</div></td></tr>)}</tbody></table></div>; }
function Summary({ title, value, icon }: { title: string; value: number; icon: ReactNode }) { return <Card><CardContent className="flex items-center gap-3 p-4"><div className="rounded-lg bg-primary/10 p-2 text-primary">{icon}</div><div><p className="text-xs text-muted-foreground">{title}</p><p className="text-2xl font-bold">{value}</p></div></CardContent></Card>; }
function Loading() { return <div className="flex items-center justify-center py-12 text-muted-foreground"><Loader2 className="me-2 h-5 w-5 animate-spin" />Loading...</div>; }
function SelectField({ label, value, onChange, options, placeholder }: { label: string; value: string; onChange: (value: string) => void; options: { value: string; label: string }[]; placeholder: string }) { return <div className="space-y-1.5"><Label>{label}</Label><select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)}><option value="" disabled={options.length === 0}>{options.length === 0 ? `— ${placeholder} —` : placeholder}</option>{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div>; }
function MembershipTable({ rows, onEnd, canManage, t }: { rows: OrganizationMembership[]; onEnd: (id: string) => void; canManage: boolean; t: Translate }) { return rows.length === 0 ? <p className="py-10 text-center text-sm text-muted-foreground">{t("noMemberships")}</p> : <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th className="px-3 py-2 text-start">{t("resource")}</th><th className="px-3 py-2 text-start">{t("unit")}</th><th className="px-3 py-2 text-start">{t("effectiveFrom")}</th><th className="px-3 py-2 text-start">{t("status")}</th><th /></tr></thead><tbody>{rows.map((row) => <tr key={row.id} className="border-b last:border-0"><td className="px-3 py-2">{row.resourceName}</td><td className="px-3 py-2">{row.organizationalUnitName}{row.isPrimary ? <Badge className="ms-2">{t("primary")}</Badge> : null}</td><td className="px-3 py-2">{new Date(row.effectiveFrom).toLocaleDateString()}</td><td className="px-3 py-2">{row.isActive ? t("active") : t("ended")}</td><td className="px-3 py-2 text-end">{canManage && row.isActive ? <Button variant="outline" size="sm" onClick={() => void onEnd(row.id)}>{t("end")}</Button> : null}</td></tr>)}</tbody></table></div>; }
function ReportingTable({ rows, onEnd, canManage, t }: { rows: OrganizationReportingLine[]; onEnd: (id: string) => void; canManage: boolean; t: Translate }) { return rows.length === 0 ? <p className="py-10 text-center text-sm text-muted-foreground">{t("noReportingLines")}</p> : <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th className="px-3 py-2 text-start">{t("resource")}</th><th className="px-3 py-2 text-start">{t("manager")}</th><th className="px-3 py-2 text-start">{t("reportingType")}</th><th className="px-3 py-2 text-start">{t("status")}</th><th /></tr></thead><tbody>{rows.map((row) => <tr key={row.id} className="border-b last:border-0"><td className="px-3 py-2">{row.resourceName}</td><td className="px-3 py-2">{row.managerResourceName}</td><td className="px-3 py-2">{row.reportingType}</td><td className="px-3 py-2">{row.isActive ? t("active") : t("ended")}</td><td className="px-3 py-2 text-end">{canManage && row.isActive ? <Button variant="outline" size="sm" onClick={() => void onEnd(row.id)}>{t("end")}</Button> : null}</td></tr>)}</tbody></table></div>; }

type Translate = (key: string, values?: Record<string, string | number>) => string;
