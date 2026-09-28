"use client";

import { FormEvent, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { Eye, Filter, Pencil, Plus, Power, Search, UserRound } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingSkeleton, PermissionDenied } from "@/components/ui/state-display";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { TablePagination } from "@/components/ui/table-pagination";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useUserLookup } from "@/hooks/projects/useProjectLookups";
import { useBaseCalendars, useCreateResource, useDeleteResource, useResource, useResources, useUpdateResource, type ResourceDto } from "@/hooks/resources/useResources";
import { useVendors } from "@/hooks/resources/useVendors";
import { ResourceFinderDialog } from "@/components/resources/ResourceFinderDialog";
import { ResourceForm } from "@/components/resources/ResourceFormFields";
import {
  emptyResourceForm,
  hasErrors,
  RESOURCE_TYPE_VENDOR,
  resourceToForm,
  toCreatePayload,
  toResourceType,
  toUpdatePayload,
  validateResourceForm,
  type ResourceFormErrors,
  type ResourceFormModel,
} from "@/lib/resources/resource-form";

type SheetMode = "create" | "edit" | "detail" | null;
const PAGE_SIZE = 20;

export default function ResourcesPage() {
  const t = useTranslations("resources.directory");
  const { user } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Resources.View));
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Manage));
  const [sheet, setSheet] = useState<SheetMode>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<ResourceFormModel>(emptyResourceForm);
  const [errors, setErrors] = useState<ResourceFormErrors>({});
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [type, setType] = useState("all");
  const [status, setStatus] = useState("all");
  const [systemUser, setSystemUser] = useState("all");

  const list = useResources({ search: search.trim() || undefined, type: type === "all" ? undefined : type, status: status === "all" ? undefined : status, userId: systemUser === "all" ? undefined : systemUser, isActive: status === "all" ? undefined : status === "Active", page, pageSize: PAGE_SIZE }, canView);
  const detail = useResource(selectedId ?? "");
  const users = useUserLookup(undefined, canView);
  const baseCalendars = useBaseCalendars(canView);
  // One lookup for both modes. Create offers active vendors only, while Edit also
  // keeps the currently linked vendor selectable even if that vendor is inactive, so
  // editing a resource never silently drops the link.
  const vendorsQuery = useVendors();
  const vendorOptions = useMemo(
    () =>
      (vendorsQuery.data ?? [])
        .filter((vendor) => vendor.isActive || vendor.id === form.vendorId)
        .map((vendor) => ({ id: vendor.id, name: vendor.name, code: vendor.code })),
    [vendorsQuery.data, form.vendorId]
  );
  const create = useCreateResource();
  const update = useUpdateResource();
  const deactivate = useDeleteResource();
  const items = list.data?.items ?? [];
  const totalPages = Math.max(1, Math.ceil((list.data?.totalCount ?? 0) / PAGE_SIZE));
  const mutationError = create.error ?? update.error ?? deactivate.error;

  const fieldLabels = useMemo(
    () => ({
      name: t("name"),
      email: t("email"),
      phone: t("phone"),
      jobTitle: t("jobTitle"),
      employeeId: t("employeeId"),
      resourceType: t("resourceType"),
      status: t("status"),
      employmentType: t("employmentType"),
      seniority: t("seniority"),
      notes: t("notes"),
      vendor: t("vendor"),
      selectVendor: t("selectVendor"),
      baseCalendar: t("baseCalendar"),
      baseCalendarHint: t("baseCalendarHint"),
      systemUser: t("systemUser"),
      noSystemUser: t("noSystemUser"),
      systemUserHint: t("systemUserHint"),
      loadingUsers: t("loadingUsers"),
      loadingCalendars: t("loadingCalendars"),
      noCalendars: t("noCalendars"),
      staff: t("staff"),
      vendorResource: t("vendorResource"),
      active: t("active"),
      inactive: t("inactive"),
      optional: t("optional"),
      typeReadOnlyHint: t("typeReadOnlyHint"),
      basicInformation: t("basicInformation"),
      professionalInformation: t("professionalInformation"),
      associationInformation: t("associationInformation"),
    }),
    [t]
  );

  const closeSheet = () => { setSheet(null); setSelectedId(null); setForm(emptyResourceForm); setErrors({}); create.reset(); update.reset(); };
  const openCreate = () => { setSelectedId(null); setForm(emptyResourceForm); setErrors({}); create.reset(); setSheet("create"); };
  const openEdit = (resource: ResourceDto) => { setSelectedId(resource.id); setForm(resourceToForm(resource)); setErrors({}); update.reset(); setSheet("edit"); };
  const openDetail = (resource: ResourceDto) => { setSelectedId(resource.id); setSheet("detail"); };
  const setField = <K extends keyof ResourceFormModel>(field: K, value: ResourceFormModel[K]) => setForm(current => ({ ...current, [field]: value }));

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const found = validateResourceForm(form);
    setErrors(found);
    if (hasErrors(found)) return;

    try {
      if (sheet === "create") {
        await create.mutateAsync(toCreatePayload(form));
      } else if (sheet === "edit" && selectedId) {
        await update.mutateAsync({ id: selectedId, data: toUpdatePayload(form) });
      }
      closeSheet();
    } catch {
      /* surfaced via mutationError below */
    }
  };

  const handleDeactivate = async (resource: ResourceDto) => {
    if (!window.confirm(resource.isActive ? t("deactivateConfirm") : t("activateConfirm"))) return;
    if (resource.isActive) await deactivate.mutateAsync(resource.id);
    else await update.mutateAsync({ id: resource.id, data: { status: "Active", isActive: true } });
  };

  if (!canView) return <Card><CardContent className="p-6"><PermissionDenied title={t("permissionRequired")} description={t("permissionDescription")} /></CardContent></Card>;

  return <div className="space-y-6">
    <PageHeader title={t("title")} description={t("description", { count: list.data?.totalCount ?? 0 })} actions={<div className="flex items-center gap-2"><ResourceFinderDialog />{canManage ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("addResource")}</Button> : null}</div>} />
    <Card className="shadow-none"><CardContent className="space-y-3 p-4"><div className="flex flex-col gap-2 md:flex-row"><div className="relative flex-1"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="ps-8" value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder={t("searchPlaceholder")} /></div><Select value={type} onValueChange={value => { setType(value); setPage(1); }}><SelectTrigger className="w-full md:w-44"><Filter className="me-2 h-4 w-4" /><SelectValue placeholder={t("typeFilter")} /></SelectTrigger><SelectContent><SelectItem value="all">{t("allTypes")}</SelectItem><SelectItem value="Staff">{t("staff")}</SelectItem><SelectItem value="VendorResource">{t("vendorResource")}</SelectItem></SelectContent></Select><Select value={status} onValueChange={value => { setStatus(value); setPage(1); }}><SelectTrigger className="w-full md:w-40"><SelectValue placeholder={t("statusFilter")} /></SelectTrigger><SelectContent><SelectItem value="all">{t("allStatuses")}</SelectItem><SelectItem value="Active">{t("active")}</SelectItem><SelectItem value="Inactive">{t("inactive")}</SelectItem></SelectContent></Select><Select value={systemUser} onValueChange={value => { setSystemUser(value); setPage(1); }}><SelectTrigger className="w-full md:w-52"><SelectValue placeholder={t("systemUserFilter")} /></SelectTrigger><SelectContent><SelectItem value="all">{t("allSystemUsers")}</SelectItem>{(users.data ?? []).map(userOption => <SelectItem key={userOption.id} value={userOption.id}>{userOption.fullName} · {userOption.email}</SelectItem>)}</SelectContent></Select></div></CardContent></Card>
    {list.isLoading ? <LoadingSkeleton rows={6} /> : list.isError ? <ErrorState title={t("loadError")} description={(list.error as Error).message} onRetry={() => void list.refetch()} /> : items.length === 0 ? <EmptyState icon={UserRound} title={t("emptyTitle")} description={t("emptyDescription")} action={canManage ? <Button size="sm" onClick={openCreate}>{t("addResource")}</Button> : undefined} /> : <><Table containerClassName="rounded-lg"><TableHeader><TableRow><TableHead>{t("name")}</TableHead><TableHead>{t("email")}</TableHead><TableHead>{t("jobTitle")}</TableHead><TableHead>{t("organization")}</TableHead><TableHead>{t("resourceType")}</TableHead><TableHead>{t("systemUser")}</TableHead><TableHead>{t("status")}</TableHead><TableHead className="text-end">{t("actions")}</TableHead></TableRow></TableHeader><TableBody>{items.map(resource => <TableRow key={resource.id}><TableCell><button type="button" className="flex items-center gap-2 text-start font-medium hover:text-primary" onClick={() => openDetail(resource)}><span className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-3.5 w-3.5" /></span>{resource.name}</button></TableCell><TableCell className="text-muted-foreground">{resource.email || t("notProvided")}</TableCell><TableCell>{resource.jobTitle || t("notProvided")}</TableCell><TableCell>{resource.departmentName || resource.businessUnitName || t("notProvided")}</TableCell><TableCell><Badge variant="outline">{resource.type === "VendorResource" ? t("vendorResource") : t("staff")}</Badge></TableCell><TableCell>{resource.userId ? t("yes") : t("no")}</TableCell><TableCell><Badge status={resource.isActive ? "Active" : "Inactive"}>{resource.isActive ? t("active") : t("inactive")}</Badge></TableCell><TableCell><div className="flex justify-end gap-1">{canManage ? <><Button type="button" variant="ghost" size="icon" aria-label={t("viewDetails")} onClick={() => openDetail(resource)}><Eye className="h-4 w-4" /></Button><Button type="button" variant="ghost" size="icon" aria-label={t("editResource")} onClick={() => openEdit(resource)}><Pencil className="h-4 w-4" /></Button><Button type="button" variant="ghost" size="icon" aria-label={resource.isActive ? t("deactivate") : t("activate")} onClick={() => void handleDeactivate(resource)}><Power className="h-4 w-4" /></Button></> : <Button type="button" variant="ghost" size="icon" aria-label={t("viewDetails")} onClick={() => openDetail(resource)}><Eye className="h-4 w-4" /></Button>}</div></TableCell></TableRow>)}</TableBody></Table><TablePagination page={page} totalPages={totalPages} summary={t("paginationSummary", { from: (page - 1) * PAGE_SIZE + 1, to: Math.min(page * PAGE_SIZE, list.data?.totalCount ?? 0), total: list.data?.totalCount ?? 0 })} previousLabel={t("previous")} nextLabel={t("next")} onPageChange={setPage} disabled={list.isFetching} /></>}
    {mutationError ? <p className="text-sm text-destructive" role="alert">{mutationError instanceof Error ? mutationError.message : t("mutationError")}</p> : null}
    <SideSheet open={sheet === "create" || sheet === "edit"} onOpenChange={open => { if (!open) closeSheet(); }} title={sheet === "create" ? t("createTitle") : t("editTitle")} description={t("formDescription")} size="lg">
      <form className="flex min-h-0 flex-1 flex-col" onSubmit={event => void submit(event)}>
        <SideSheetBody className="space-y-4">
          <ResourceForm
            form={form}
            onChange={setField}
            labels={fieldLabels}
            sectionLabels={{
              basicInformation: fieldLabels.basicInformation,
              professionalInformation: fieldLabels.professionalInformation,
              associationInformation: fieldLabels.associationInformation,
            }}
            vendorOptions={vendorOptions}
            baseCalendarOptions={baseCalendars.data ?? []}
            userOptions={users.data ?? []}
            usersLoading={users.isLoading}
            calendarsLoading={baseCalendars.isLoading}
            readOnlyType={sheet === "edit"}
            fieldErrors={{
              name: errors.name ? t("nameRequired") : undefined,
              email: errors.email ? t("emailInvalid") : undefined,
              vendorId: errors.vendorId ? t("vendorRequired") : undefined,
              baseCalendarId: errors.baseCalendarId ? t("baseCalendarInvalid") : undefined,
            }}
          />
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={closeSheet}>{t("cancel")}</Button>
          <Button type="submit" disabled={create.isPending || update.isPending || !form.name.trim()}>{create.isPending || update.isPending ? t("saving") : sheet === "create" ? t("create") : t("save")}</Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
    <SideSheet open={sheet === "detail"} onOpenChange={open => { if (!open) closeSheet(); }} title={detail.data?.name ?? t("detailsTitle")} description={t("detailsDescription")} size="lg"><SideSheetBody className="space-y-5">{detail.isLoading ? <LoadingSkeleton rows={5} /> : detail.isError ? <ErrorState title={t("detailsError")} onRetry={() => void detail.refetch()} /> : detail.data ? <><section className="rounded-lg border p-4"><div className="flex items-start justify-between gap-3"><div><h3 className="text-base font-semibold">{detail.data.name}</h3><p className="text-sm text-muted-foreground">{detail.data.email || t("notProvided")}</p></div><Badge status={detail.data.isActive ? "Active" : "Inactive"}>{detail.data.isActive ? t("active") : t("inactive")}</Badge></div><dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2"><div><dt className="text-muted-foreground">{t("jobTitle")}</dt><dd>{detail.data.jobTitle || t("notProvided")}</dd></div><div><dt className="text-muted-foreground">{t("resourceType")}</dt><dd>{toResourceType(detail.data.type) === RESOURCE_TYPE_VENDOR ? fieldLabels.vendorResource : fieldLabels.staff}</dd></div><div><dt className="text-muted-foreground">{t("systemUser")}</dt><dd>{detail.data.userId ? t("linked") : t("noSystemUser")}</dd></div><div><dt className="text-muted-foreground">{t("organization")}</dt><dd>{detail.data.departmentName || detail.data.businessUnitName || t("notProvided")}</dd></div></dl></section><section><h3 className="mb-2 text-sm font-semibold">{t("allocations")}</h3>{detail.data.allocations.length === 0 ? <p className="rounded-lg bg-muted/30 p-3 text-sm text-muted-foreground">{t("noAllocations")}</p> : <div className="space-y-2">{detail.data.allocations.map(allocation => { const allocationLabel = allocation.status === "Confirmed" ? t("confirmed") : allocation.status === "Proposed" ? t("proposed") : allocation.status === "Cancelled" ? t("cancelled") : t("tentative"); return <div key={allocation.id} className="rounded-lg border p-3 text-sm"><div className="flex items-center justify-between gap-2"><span className="font-medium">{allocation.projectName || t("unassignedProject")}</span><Badge status={allocation.status || "Tentative"}>{allocationLabel}</Badge></div><p className="mt-1 text-xs text-muted-foreground">{allocation.role} · {allocation.allocationPercentage}%</p></div>; })}</div>}</section></> : null}</SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={closeSheet}>{t("close")}</Button>{canManage && detail.data ? <Button type="button" onClick={() => openEdit(detail.data!)}>{t("editResource")}</Button> : null}</SideSheetFooter></SideSheet>
  </div>;
}
