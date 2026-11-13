import * as React from "react";
import { Link, Navigate } from "react-router-dom";
import { ArrowLeft, Check, CircleHelp, ListFilter, Loader2, Pencil, Plus, Power, Search, ShieldCheck, Star } from "lucide-react";
import { ApiError } from "@api/client";
import { useManagement } from "@api/management";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import {
  useCreateProjectLifecycleItem,
  useProjectLifecycleCatalog,
  useUpdateProjectLifecycleItem,
  useProjectLifecycleItemAction,
  type ProjectLifecycleCatalogItem,
  type ProjectLifecycleCatalogKind,
  type ProjectStatusAttentionLevel,
  type ProjectStatusReportingCategory,
  type SaveProjectLifecycleCatalogItem,
} from "./api/projectLifecycle";

type ViewFilter = "all" | "active" | "inactive";
type LifecycleForm = SaveProjectLifecycleCatalogItem;

const EMPTY_FORM: LifecycleForm = {
  code: "",
  nameEn: "",
  nameAr: "",
  descriptionEn: "",
  descriptionAr: "",
  displayOrder: 10,
  isContinuousActivity: false,
  reportingCategory: "InProgress",
  attentionLevel: "None",
};

const permissionKeys: Record<ProjectLifecycleCatalogKind, { view: string; create: string; edit: string; status: string }> = {
  phases: {
    view: Permissions.ProjectLifecyclePhase.View,
    create: Permissions.ProjectLifecyclePhase.Create,
    edit: Permissions.ProjectLifecyclePhase.Edit,
    status: Permissions.ProjectLifecyclePhase.ActivateDeactivate,
  },
  statuses: {
    view: Permissions.ProjectLifecycleStatus.View,
    create: Permissions.ProjectLifecycleStatus.Create,
    edit: Permissions.ProjectLifecycleStatus.Edit,
    status: Permissions.ProjectLifecycleStatus.ActivateDeactivate,
  },
};

function itemName(item: Pick<ProjectLifecycleCatalogItem, "nameEn" | "nameAr">, locale: string) {
  return locale === "ar" ? item.nameAr || item.nameEn : item.nameEn || item.nameAr;
}

function textError(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}

function Summary({ label, value, icon, accent }: { label: string; value: number; icon: React.ReactNode; accent: string }) {
  return <Card className="shadow-none"><CardContent className="flex items-center gap-3 p-4"><span className={`flex h-10 w-10 items-center justify-center rounded-xl bg-muted ${accent}`}>{icon}</span><span className="min-w-0"><span className="block truncate text-xs text-muted-foreground">{label}</span><span className="mt-1 block text-xl font-semibold tabular-nums">{value}</span></span></CardContent></Card>;
}

export function ProjectLifecycleCatalogPage() {
  const { t, locale, formatNumber } = useI18n();
  const { user } = useAuth();
  const { snapshot, canMutate, loading: managementLoading } = useManagement();
  const confirm = useConfirm();
  const [activeKind, setActiveKind] = React.useState<ProjectLifecycleCatalogKind>("phases");
  const [organizationId, setOrganizationId] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [viewFilter, setViewFilter] = React.useState<ViewFilter>("all");
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<ProjectLifecycleCatalogItem | null>(null);
  const [form, setForm] = React.useState<LifecycleForm>(EMPTY_FORM);
  const [formError, setFormError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [pendingActionId, setPendingActionId] = React.useState<string | null>(null);

  const scope = snapshot?.selectedContext?.scope ?? snapshot?.scope;
  const organizations = React.useMemo(() => {
    if (!scope) return [];
    const allowedIds = new Set(scope.organizationIds ?? []);
    return (scope.organizationReferences ?? []).filter((organization) => allowedIds.has(organization.id));
  }, [scope]);
  const organizationIsAllowed = !organizationId || organizations.some((organization) => organization.id === organizationId);
  const canView = (kind: ProjectLifecycleCatalogKind) => Boolean(user && hasPermission(user.permissions, permissionKeys[kind].view));
  const canCreate = (kind: ProjectLifecycleCatalogKind) => Boolean(canMutate && user && hasPermission(user.permissions, permissionKeys[kind].create));
  const canEdit = (kind: ProjectLifecycleCatalogKind) => Boolean(canMutate && user && hasPermission(user.permissions, permissionKeys[kind].edit));
  const canChangeStatus = (kind: ProjectLifecycleCatalogKind) => Boolean(canMutate && user && hasPermission(user.permissions, permissionKeys[kind].status));
  const phaseQuery = useProjectLifecycleCatalog("phases", organizationId || null, true, organizationIsAllowed && canView("phases"));
  const statusQuery = useProjectLifecycleCatalog("statuses", organizationId || null, true, organizationIsAllowed && canView("statuses"));
  const activeQuery = activeKind === "phases" ? phaseQuery : statusQuery;
  const createMutation = useCreateProjectLifecycleItem(activeKind);
  const updateMutation = useUpdateProjectLifecycleItem(activeKind);
  const activateMutation = useProjectLifecycleItemAction(activeKind, "activate");
  const deactivateMutation = useProjectLifecycleItemAction(activeKind, "deactivate");
  const makeDefaultMutation = useProjectLifecycleItemAction(activeKind, "default");

  React.useEffect(() => {
    setOrganizationId((current) => organizations.some((organization) => organization.id === current) ? current : "");
  }, [organizations]);
  const catalogItems = React.useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    return [...(activeQuery.data ?? [])]
      .sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code))
      .filter((item) => {
        const matchesActive = viewFilter === "all" || (viewFilter === "active" ? item.isActive : !item.isActive);
        const matchesSearch = !query || [item.code, item.nameEn, item.nameAr, item.descriptionEn, item.descriptionAr].some((value) => value?.toLocaleLowerCase().includes(query));
        return matchesActive && matchesSearch;
      });
  }, [activeQuery.data, search, viewFilter]);
  const allItems = activeQuery.data ?? [];
  const activeCount = allItems.filter((item) => item.isActive).length;
  const inactiveCount = allItems.length - activeCount;
  const defaultItem = allItems.find((item) => item.isDefault);
  const busy = createMutation.isPending || updateMutation.isPending || activateMutation.isPending || deactivateMutation.isPending;
  const namespace = "administration.projectLifecycleCatalog";
  const visibleKinds = (["phases", "statuses"] as const).filter(canView);

  React.useEffect(() => {
    if (visibleKinds.length && !visibleKinds.includes(activeKind)) setActiveKind(visibleKinds[0]);
  }, [activeKind, visibleKinds.join("|")]);

  function openCreate() {
    setEditing(null);
    setForm({ ...EMPTY_FORM, reportingCategory: "InProgress", attentionLevel: "None", displayOrder: (allItems.at(-1)?.displayOrder ?? 0) + 10 });
    setFormError(null);
    setActionError(null);
    setSheetOpen(true);
  }

  function openEdit(item: ProjectLifecycleCatalogItem) {
    setEditing(item);
    setForm({
      code: item.code,
      nameEn: item.nameEn,
      nameAr: item.nameAr,
      descriptionEn: item.descriptionEn ?? "",
      descriptionAr: item.descriptionAr ?? "",
      displayOrder: item.displayOrder,
      ...(activeKind === "phases"
        ? { isContinuousActivity: Boolean(item.isContinuousActivity) }
        : { reportingCategory: item.reportingCategory ?? "InProgress", attentionLevel: item.attentionLevel ?? "None" }),
    });
    setFormError(null);
    setActionError(null);
    setSheetOpen(true);
  }

  function closeSheet(open: boolean) {
    setSheetOpen(open);
    if (!open) {
      setEditing(null);
      setFormError(null);
    }
  }

  function updateForm<K extends keyof LifecycleForm>(key: K, value: LifecycleForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function saveItem(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (editing ? !canEdit(activeKind) : !canCreate(activeKind)) return;
    const code = form.code.trim();
    const nameEn = form.nameEn.trim();
    const nameAr = form.nameAr.trim();
    if (!code || !nameEn || !nameAr) {
      setFormError(t(`${namespace}.requiredFields`));
      return;
    }
    setFormError(null);
    const input: SaveProjectLifecycleCatalogItem = {
      code: code.toUpperCase(),
      nameEn,
      nameAr,
      organizationId: editing?.organizationId ?? (organizationId || null),
      displayOrder: form.displayOrder,
      descriptionEn: form.descriptionEn?.trim() ?? "",
      descriptionAr: form.descriptionAr?.trim() ?? "",
      ...(activeKind === "phases"
        ? { isContinuousActivity: Boolean(form.isContinuousActivity) }
        : {
          reportingCategory: (form.reportingCategory ?? "InProgress") as ProjectStatusReportingCategory,
          attentionLevel: (form.attentionLevel ?? "None") as ProjectStatusAttentionLevel,
        }),
    };
    try {
      if (editing) await updateMutation.mutateAsync({ id: editing.id, input: { ...input, rowVersion: editing.rowVersion } });
      else await createMutation.mutateAsync({ ...input, organizationId: organizationId || null });
      closeSheet(false);
    } catch (error) {
      setFormError(textError(error, t(`${namespace}.saveError`)));
    }
  }

  async function setItemActive(item: ProjectLifecycleCatalogItem) {
    if (!canChangeStatus(activeKind) || pendingActionId) return;
    const activate = !item.isActive;
    if (!activate && !await confirm({
      title: t(`${namespace}.deactivateTitle`),
      description: t(`${namespace}.deactivateConfirm`, { name: itemName(item, locale), count: formatNumber(item.referencedProjectCount) }),
      confirmLabel: t(`${namespace}.deactivate`),
      destructive: true,
    })) return;
    setPendingActionId(item.id);
    setActionError(null);
    try {
      await (activate ? activateMutation : deactivateMutation).mutateAsync({ id: item.id, rowVersion: item.rowVersion });
    } catch (error) {
      setActionError(textError(error, t(`${namespace}.actionError`)));
    } finally {
      setPendingActionId(null);
    }
  }

  async function setItemDefault(item: ProjectLifecycleCatalogItem) {
    if (!canEdit(activeKind) || item.isDefault || !item.isActive || pendingActionId) return;
    setPendingActionId(item.id);
    setActionError(null);
    try {
      await makeDefaultMutation.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
    } catch (error) {
      setActionError(textError(error, t(`${namespace}.actionError`)));
    } finally {
      setPendingActionId(null);
    }
  }

  const loadingScope = managementLoading && !scope;
  const currentTabCanView = canView(activeKind);
  const isDefaultName = defaultItem ? itemName(defaultItem, locale) : t(`${namespace}.none`);

  if (!user || visibleKinds.length === 0) return <Navigate to="/administration/reference-data" replace />;

  return <div className="space-y-6">
    <PageHeader
      title={t(`${namespace}.title`)}
      description={t(`${namespace}.description`)}
      actions={<div className="flex flex-wrap items-center gap-2">
        <Button asChild size="sm" variant="outline"><Link to="/administration/reference-data"><ArrowLeft className="me-2 h-4 w-4 rtl:rotate-180" />{t("administration.areas.referenceData.back")}</Link></Button>
        {canCreate(activeKind) && organizationIsAllowed ? <Button size="sm" onClick={openCreate} disabled={busy}><Plus className="me-1.5 h-4 w-4" />{t(`${namespace}.add`)}</Button> : null}
      </div>}
    />

    <Card className="overflow-hidden border-primary/10 bg-gradient-to-br from-primary/[0.06] via-background to-background shadow-none">
      <CardContent className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div className="flex items-start gap-3"><span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary"><ListFilter className="h-5 w-5" /></span><div><p className="text-sm font-semibold">{t(`${namespace}.independentCatalogsTitle`)}</p><p className="mt-1 max-w-2xl text-sm text-muted-foreground">{t(`${namespace}.independentCatalogsDescription`)}</p></div></div>
        <div className="inline-flex w-fit items-center gap-2 rounded-full border border-primary/15 bg-background/80 px-3 py-1.5 text-xs font-medium text-primary"><ShieldCheck className="h-3.5 w-3.5" />{t(`${namespace}.preservedReferences`)}</div>
      </CardContent>
    </Card>

    <div className="grid gap-3 sm:grid-cols-3">
      <Summary label={t(`${namespace}.totalEntries`)} value={allItems.length} icon={<ListFilter className="h-4 w-4" />} accent="text-primary" />
      <Summary label={t(`${namespace}.activeEntries`)} value={activeCount} icon={<Check className="h-4 w-4" />} accent="text-emerald-600 dark:text-emerald-400" />
      <Summary label={t(`${namespace}.configuredDefault`)} value={defaultItem ? 1 : 0} icon={<CircleHelp className="h-4 w-4" />} accent="text-violet-600 dark:text-violet-400" />
    </div>

    <Card className="shadow-none">
      <CardHeader className="gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="space-y-3">
          <div><CardTitle className="text-base">{t(`${namespace}.catalogTitle`)}</CardTitle><CardDescription className="mt-1">{t(`${namespace}.catalogDescription`)}</CardDescription></div>
          <div className="inline-flex w-fit rounded-lg border bg-muted/50 p-1" role="tablist" aria-label={t(`${namespace}.catalogTabs`)}>
            {visibleKinds.map((kind) => <button key={kind} id={`lifecycle-tab-${kind}`} type="button" role="tab" aria-selected={activeKind === kind} onClick={() => { setActiveKind(kind); setSearch(""); setViewFilter("all"); setActionError(null); }} className={`rounded-md px-3 py-2 text-sm font-medium transition-colors ${activeKind === kind ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"}`}>{t(`${namespace}.${kind}`)}</button>)}
          </div>
        </div>
        <div className="grid w-full gap-3 sm:grid-cols-2 lg:max-w-2xl">
          <div className="space-y-1.5"><Label htmlFor="lifecycle-scope">{t(`${namespace}.scope`)}</Label><select id="lifecycle-scope" value={organizationId} onChange={(event) => { setOrganizationId(event.target.value); setSearch(""); }} disabled={loadingScope} className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="">{t(`${namespace}.tenantScope`)}</option>{organizations.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}</select></div>
          <div className="space-y-1.5"><Label htmlFor="lifecycle-search">{t(`${namespace}.search`)}</Label><div className="relative"><Search aria-hidden="true" className="absolute start-3 top-3 h-4 w-4 text-muted-foreground" /><Input id="lifecycle-search" className="ps-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t(`${namespace}.searchPlaceholder`)} disabled={!organizationIsAllowed || !currentTabCanView} /></div></div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 p-3">
          <div className="flex flex-wrap gap-2" role="group" aria-label={t(`${namespace}.filterByStatus`)}>
            {(["all", "active", "inactive"] as const).map((filter) => <Button key={filter} type="button" size="sm" variant={viewFilter === filter ? "secondary" : "ghost"} onClick={() => setViewFilter(filter)}>{t(`${namespace}.filter.${filter}`)}</Button>)}
          </div>
          <p className="text-xs text-muted-foreground">{t(`${namespace}.defaultSummary`, { name: isDefaultName })}</p>
        </div>

        {loadingScope ? <div className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />{t("common.loading")}</div>
          : !organizationIsAllowed ? <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">{t(`${namespace}.scopeUnavailable`)}</div>
            : activeQuery.isLoading ? <div className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />{t("common.loading")}</div>
              : activeQuery.isError ? <div className="flex flex-col items-center gap-3 rounded-lg border border-destructive/30 bg-destructive/5 p-8 text-center"><p role="alert" className="text-sm text-destructive">{t(`${namespace}.loadError`)}</p><Button size="sm" variant="outline" onClick={() => void activeQuery.refetch()}>{t("administration.workItemStatuses.retry")}</Button></div>
                : catalogItems.length === 0 ? <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed p-10 text-center"><span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-muted text-muted-foreground"><Search className="h-5 w-5" /></span><p className="text-sm font-medium">{search || viewFilter !== "all" ? t(`${namespace}.noMatches`) : t(`${namespace}.empty`)}</p><p className="max-w-lg text-xs text-muted-foreground">{t(`${namespace}.emptyDescription`)}</p>{search || viewFilter !== "all" ? <Button size="sm" variant="outline" onClick={() => { setSearch(""); setViewFilter("all"); }}>{t(`${namespace}.clearFilters`)}</Button> : null}</div>
                  : <div className="grid gap-3 xl:grid-cols-2">
                    {catalogItems.map((item) => {
                      const canManageItem = canEdit(activeKind) || canChangeStatus(activeKind);
                      return <article key={item.id} className={`group rounded-xl border bg-card p-4 transition-shadow hover:shadow-sm ${!item.isActive ? "opacity-80" : ""}`}>
                        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2"><h3 className="text-sm font-semibold">{itemName(item, locale)}</h3><span className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${item.isActive ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300" : "border-muted-foreground/25 bg-muted text-muted-foreground"}`}>{item.isActive ? t(`${namespace}.active`) : t(`${namespace}.inactive`)}</span>{item.isDefault ? <span className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-[11px] font-medium text-primary"><Check className="h-3 w-3" />{t(`${namespace}.default`)}</span> : null}{activeKind === "phases" && item.isContinuousActivity ? <span className="rounded-full border border-violet-500/25 bg-violet-500/10 px-2 py-0.5 text-[11px] font-medium text-violet-700 dark:text-violet-300">{t(`${namespace}.continuousActivity`)}</span> : null}</div>
                            <p className="mt-1 text-xs text-muted-foreground">{locale === "ar" ? item.nameEn : item.nameAr}</p>
                            <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground"><span className="font-mono">{item.code}</span><span>{t(`${namespace}.orderValue`, { order: formatNumber(item.displayOrder) })}</span><span>{t(`${namespace}.projectReferences`, { count: formatNumber(item.referencedProjectCount) })}</span>{activeKind === "statuses" ? <span>{t(`${namespace}.reportingCategory.${item.reportingCategory ?? "InProgress"}`)}</span> : null}{activeKind === "statuses" && item.attentionLevel && item.attentionLevel !== "None" ? <span>{t(`${namespace}.attentionLevel.${item.attentionLevel}`)}</span> : null}</div>
                            {(item.descriptionEn || item.descriptionAr) ? <p className="mt-3 line-clamp-2 text-sm text-muted-foreground">{locale === "ar" ? item.descriptionAr || item.descriptionEn : item.descriptionEn || item.descriptionAr}</p> : null}
                          </div>
                          {canManageItem || (canEdit(activeKind) && !item.isDefault && item.isActive) ? <div className="flex shrink-0 gap-1 self-end sm:self-start">{canEdit(activeKind) ? <><Button size="icon" variant="ghost" onClick={() => openEdit(item)} aria-label={`${t(`${namespace}.edit`)} ${itemName(item, locale)}`} disabled={busy || pendingActionId !== null}><Pencil className="h-4 w-4" /></Button>{!item.isDefault && item.isActive ? <Button size="icon" variant="ghost" onClick={() => void setItemDefault(item)} aria-label={`${t(`${namespace}.makeDefault`)} ${itemName(item, locale)}`} disabled={busy || pendingActionId !== null}><Star className="h-4 w-4" /></Button> : null}</> : null}{canChangeStatus(activeKind) ? <Button size="icon" variant="ghost" onClick={() => void setItemActive(item)} aria-label={`${item.isActive ? t(`${namespace}.deactivate`) : t(`${namespace}.activate`)} ${itemName(item, locale)}`} disabled={busy || pendingActionId !== null}><Power className={`h-4 w-4 ${item.isActive ? "text-muted-foreground" : "text-emerald-600 dark:text-emerald-400"}`} /></Button> : null}</div> : null}
                        </div>
                        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-3 text-xs text-muted-foreground"><span>{item.organizationId ? organizations.find((organization) => organization.id === item.organizationId)?.name ?? t(`${namespace}.organizationScope`) : t(`${namespace}.tenantScope`)}</span></div>
                      </article>;
                    })}
                  </div>}
        {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p> : null}
      </CardContent>
    </Card>

    <SideSheet open={sheetOpen} onOpenChange={closeSheet} title={editing ? t(`${namespace}.edit`) : t(`${namespace}.add`)} description={t(`${namespace}.formDescription`)} size="lg">
      <form className="flex min-h-0 flex-1 flex-col" onSubmit={(event) => void saveItem(event)}>
        <SideSheetBody className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5"><Label htmlFor="lifecycle-code">{t(`${namespace}.code`)}</Label><Input id="lifecycle-code" value={form.code} onChange={(event) => updateForm("code", event.target.value.toUpperCase())} maxLength={64} disabled={Boolean(editing)} required /><p className="text-xs text-muted-foreground">{t(`${namespace}.stableCodeHint`)}</p></div>
            <div className="space-y-1.5"><Label htmlFor="lifecycle-order">{t(`${namespace}.displayOrder`)}</Label><Input id="lifecycle-order" type="number" min={0} step={1} value={form.displayOrder} onChange={(event) => updateForm("displayOrder", Number(event.target.value))} required /></div>
            <div className="space-y-1.5"><Label htmlFor="lifecycle-name-en">{t(`${namespace}.nameEn`)}</Label><Input id="lifecycle-name-en" value={form.nameEn} onChange={(event) => updateForm("nameEn", event.target.value)} dir="ltr" required /></div>
            <div className="space-y-1.5"><Label htmlFor="lifecycle-name-ar">{t(`${namespace}.nameAr`)}</Label><Input id="lifecycle-name-ar" value={form.nameAr} onChange={(event) => updateForm("nameAr", event.target.value)} dir="rtl" required /></div>
            <div className="space-y-1.5 sm:col-span-2"><Label htmlFor="lifecycle-description-en">{t(`${namespace}.descriptionEn`)}</Label><textarea id="lifecycle-description-en" value={form.descriptionEn ?? ""} onChange={(event) => updateForm("descriptionEn", event.target.value)} dir="ltr" rows={2} className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" /></div>
            <div className="space-y-1.5 sm:col-span-2"><Label htmlFor="lifecycle-description-ar">{t(`${namespace}.descriptionAr`)}</Label><textarea id="lifecycle-description-ar" value={form.descriptionAr ?? ""} onChange={(event) => updateForm("descriptionAr", event.target.value)} dir="rtl" rows={2} className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" /></div>
            {activeKind === "phases" ? <label className="flex items-start gap-3 rounded-lg border border-violet-500/20 bg-violet-500/[0.04] p-3 sm:col-span-2"><input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary" checked={Boolean(form.isContinuousActivity)} onChange={(event) => updateForm("isContinuousActivity", event.target.checked)} /><span><span className="block text-sm font-medium">{t(`${namespace}.continuousActivity`)}</span><span className="mt-0.5 block text-xs text-muted-foreground">{t(`${namespace}.continuousActivityHint`)}</span></span></label> : null}
            {activeKind === "statuses" ? <>
              <div className="space-y-1.5"><Label htmlFor="lifecycle-reporting-category">{t(`${namespace}.reportingCategory.label`)}</Label><select id="lifecycle-reporting-category" className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.reportingCategory ?? "InProgress"} onChange={(event) => updateForm("reportingCategory", event.target.value as ProjectStatusReportingCategory)}><option value="InProgress">{t(`${namespace}.reportingCategory.InProgress`)}</option><option value="Completed">{t(`${namespace}.reportingCategory.Completed`)}</option><option value="Excluded">{t(`${namespace}.reportingCategory.Excluded`)}</option></select><p className="text-xs text-muted-foreground">{t(`${namespace}.reportingCategoryHint`)}</p></div>
              <div className="space-y-1.5"><Label htmlFor="lifecycle-attention-level">{t(`${namespace}.attentionLevel.label`)}</Label><select id="lifecycle-attention-level" className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.attentionLevel ?? "None"} onChange={(event) => updateForm("attentionLevel", event.target.value as ProjectStatusAttentionLevel)}><option value="None">{t(`${namespace}.attentionLevel.None`)}</option><option value="NeedsAttention">{t(`${namespace}.attentionLevel.NeedsAttention`)}</option><option value="Critical">{t(`${namespace}.attentionLevel.Critical`)}</option></select><p className="text-xs text-muted-foreground">{t(`${namespace}.attentionLevelHint`)}</p></div>
            </> : null}
          </div>
          {formError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{formError}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={busy}>{t(`${namespace}.cancel`)}</Button><Button type="submit" disabled={busy || (editing ? !canEdit(activeKind) : !canCreate(activeKind))}>{busy ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{t(`${namespace}.saving`)}</> : t(`${namespace}.save`)}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  </div>;
}
