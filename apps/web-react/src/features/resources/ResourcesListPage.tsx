import * as React from "react";
import { useResource, useResources, useCreateResource, useUpdateResource, useDeleteResource, useBaseCalendars, type ResourceDto, type ResourceBaseCalendarDto } from "./api/resources";
import { useOrganizationUnits } from "../organization/api/organization";
import { useUserLookup, type UserLookupDto } from "../projects/lib/project-lookups";
import { useVendors, type VendorDto } from "../vendors/api/vendors";
import { useManagement } from "@api/management";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { getResourceTypeTextClass, getStatusTextClass, getSystemUserTextClass } from "@lib/status-presentation";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Skeleton } from "@components/ui/Skeleton";
import { Plus, Edit, Eye, Power, UserRound } from "lucide-react";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { dialogValuesToResourceForm, toCreateResourcePayload, toUpdateResourcePayload, validateResourceForm } from "./resource-form";
import { emptyResourceFormValues, ResourceFormSections, type ResourceFormValues } from "./ResourceFormSections";
import { ResourceFinderDialog } from "./ResourceFinderDialog";

interface ResourceTableRowProps {
  resource: ResourceDto;
  onEdit: (resource: ResourceDto) => void;
  onView: (resource: ResourceDto) => void;
  onDelete: (resource: ResourceDto) => void;
  canEdit: boolean;
  canDelete: boolean;
}

function ResourceTableRow({ resource, onEdit, onView, onDelete, canEdit, canDelete }: ResourceTableRowProps) {
  const { t } = useI18n();
  const typeLabel = resource.type === "Staff" ? t("resources.directory.staff") : t("resources.directory.vendorResource");
  const statusLabel = resource.isActive ? t("resources.directory.active") : t("resources.directory.inactive");

  return (
    <TableRow>
      <TableCell><button type="button" className="flex items-center gap-2 text-start font-medium hover:text-primary" onClick={() => onView(resource)}><span className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary"><UserRound className="h-3.5 w-3.5" /></span>{resource.name}</button></TableCell>
      <TableCell>{resource.email || t("resources.directory.notProvided")}</TableCell>
      <TableCell>{resource.jobTitle || t("resources.directory.notProvided")}</TableCell>
      <TableCell>{resource.primaryOrganizationalUnitName || resource.departmentName || t("resources.directory.notProvided")}</TableCell>
      <TableCell>
        <span className={`text-sm font-medium ${getResourceTypeTextClass(resource.type)}`}>{typeLabel}</span>
      </TableCell>
      <TableCell><span className={`text-sm font-medium ${getSystemUserTextClass(Boolean(resource.userId))}`}>{resource.userId ? t("resources.directory.yes") : t("resources.directory.no")}</span></TableCell>
      <TableCell>
        <span className={`text-sm font-medium ${getStatusTextClass(resource.isActive ? "Active" : "Inactive")}`}>{statusLabel}</span>
      </TableCell>
      <TableCell className="text-right">
        <div className="flex items-center justify-end gap-1">
          <Button variant="ghost" size="icon" aria-label={t("resources.directory.viewDetails")} onClick={() => onView(resource)}><Eye className="h-4 w-4" /></Button>
          {canEdit && <Button variant="ghost" size="icon" onClick={() => onEdit(resource)}><Edit className="h-4 w-4" /></Button>}
          {canDelete && <Button variant="ghost" size="icon" aria-label={resource.isActive ? t("resources.directory.deactivate") : t("resources.directory.activate")} className={resource.isActive ? "text-destructive hover:text-destructive" : ""} onClick={() => onDelete(resource)}><Power className="h-4 w-4" /></Button>}
        </div>
      </TableCell>
    </TableRow>
  );
}

function ResourceDetailsSheet({ resource, detail, loading, error, onRetry, onClose, onEdit, canEdit }: {
  resource: ResourceDto | null;
  detail?: ResourceDto;
  loading: boolean;
  error: boolean;
  onRetry: () => void;
  onClose: () => void;
  onEdit: (resource: ResourceDto) => void;
  canEdit: boolean;
}) {
  const { t } = useI18n();
  const record = detail ?? resource;
  const typeLabel = record?.type === "VendorResource" ? t("resources.directory.vendorResource") : t("resources.directory.staff");
  const statusLabel = record?.isActive ? t("resources.directory.active") : t("resources.directory.inactive");
  return (
    <SideSheet open={Boolean(resource)} onOpenChange={(open) => { if (!open) onClose(); }} title={record?.name ?? t("resources.directory.detailsTitle")} description={t("resources.directory.detailsDescription")}>
      <SideSheetBody className="space-y-5">
        {loading ? <div className="space-y-3">{[1, 2, 3, 4].map((index) => <Skeleton key={index} className="h-8 w-full" />)}</div> : null}
        {error ? <div role="alert" className="space-y-2 text-sm text-destructive"><p>{t("resources.directory.detailsError")}</p><Button variant="outline" size="sm" onClick={onRetry}>{t("common.retry")}</Button></div> : null}
        {record && !loading ? <>
          <section className="rounded-lg border p-4">
            <div className="flex items-start justify-between gap-3"><div><h3 className="font-semibold">{record.name}</h3><p className="text-sm text-muted-foreground">{record.email || t("resources.directory.notProvided")}</p></div><span className={`text-sm font-medium ${getStatusTextClass(record.isActive ? "Active" : "Inactive")}`}>{statusLabel}</span></div>
            <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
              <div><dt className="text-muted-foreground">{t("resources.directory.jobTitle")}</dt><dd>{record.jobTitle || t("resources.directory.notProvided")}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.resourceType")}</dt><dd className={`font-medium ${getResourceTypeTextClass(record.type)}`}>{typeLabel}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.systemUser")}</dt><dd className={`font-medium ${getSystemUserTextClass(Boolean(record.userId))}`}>{record.userId ? t("resources.directory.linked") : t("resources.directory.noSystemUser")}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.organization")}</dt><dd>{record.primaryOrganizationalUnitName || record.departmentName || t("resources.directory.notProvided")}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.vendor")}</dt><dd>{record.vendorName || t("resources.directory.notProvided")}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.baseCalendar")}</dt><dd>{record.baseCalendarName || t("resources.directory.notProvided")}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.employmentType")}</dt><dd>{record.employmentType || t("resources.directory.notProvided")}</dd></div>
              <div><dt className="text-muted-foreground">{t("resources.directory.seniority")}</dt><dd>{record.seniority || t("resources.directory.notProvided")}</dd></div>
            </dl>
            {record.notes ? <div className="mt-3 border-t pt-3 text-sm"><p className="text-muted-foreground">{t("resources.directory.notes")}</p><p className="whitespace-pre-wrap">{record.notes}</p></div> : null}
          </section>
          <section>
            <h3 className="mb-2 text-sm font-semibold">{t("resources.directory.allocations")}</h3>
            {record.allocations?.length ? <div className="space-y-2">{record.allocations.map((allocation) => <div key={allocation.id} className="rounded-lg border p-3 text-sm"><div className="flex items-center justify-between gap-2"><span className="font-medium">{allocation.projectName || t("resources.directory.unassignedProject")}</span><span className={`text-xs font-medium ${getStatusTextClass(allocation.status ?? "Tentative")}`}>{t(`resources.directory.${(allocation.status ?? "tentative").toLowerCase()}`)}</span></div><p className="mt-1 text-xs text-muted-foreground">{allocation.role} · {allocation.allocationPercentage}% · {allocation.startDate.slice(0, 10)} – {allocation.endDate.slice(0, 10)}</p></div>)}</div> : <p className="text-sm text-muted-foreground">{t("resources.directory.noAllocations")}</p>}
          </section>
        </> : null}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={onClose}>{t("resources.directory.close")}</Button>{canEdit && record ? <Button type="button" onClick={() => onEdit(record)}>{t("resources.directory.editResource")}</Button> : null}</SideSheetFooter>
    </SideSheet>
  );
}

type ResourceLookups = { calendars?: ResourceBaseCalendarDto[]; units?: { id: string; name: string; code?: string; isActive?: boolean }[]; users?: UserLookupDto[]; vendors?: VendorDto[]; calendarsLoading?: boolean; usersLoading?: boolean; vendorsLoading?: boolean };

function CreateResourceDialog({ open, onOpenChange, onSubmit, calendars, units, users, vendors, calendarsLoading, usersLoading, vendorsLoading, isPending, error }: { open: boolean; onOpenChange: (open: boolean) => void; onSubmit: (data: Record<string, string>) => void; isPending: boolean; error?: string } & ResourceLookups) {
  const { t } = useI18n();
  const [formData, setFormData] = React.useState<ResourceFormValues>(emptyResourceFormValues);
  const [submitted, setSubmitted] = React.useState(false);

  React.useEffect(() => {
    if (!open) return;
    setFormData(emptyResourceFormValues);
    setSubmitted(false);
  }, [open]);

  if (!open) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
    if (Object.values(validateResourceForm(dialogValuesToResourceForm(formData))).some(Boolean)) return;
    onSubmit(formData);
  };

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("resources.directory.createTitle")} description={t("resources.directory.formDescription")} size="lg">
        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody>
            {error ? <p role="alert" className="mb-4 text-sm text-destructive">{error}</p> : null}
            <ResourceFormSections values={formData} setValues={setFormData} calendars={calendars} units={units} users={users} vendors={vendors} calendarsLoading={calendarsLoading} usersLoading={usersLoading} vendorsLoading={vendorsLoading} submitted={submitted} />
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isPending || !formData.name.trim()}>{isPending ? t("common.saving") : t("common.create")}</Button>
          </SideSheetFooter>
        </form>
    </SideSheet>
  );
}

function EditResourceDialog({ open, onOpenChange, resource, onSubmit, calendars, units, users, vendors, calendarsLoading, usersLoading, vendorsLoading, isPending, error }: { open: boolean; onOpenChange: (open: boolean) => void; resource: ResourceDto | null; onSubmit: (data: Record<string, string> & { id: string }) => void; isPending: boolean; error?: string } & ResourceLookups) {
  const { t } = useI18n();
  const [formData, setFormData] = React.useState<ResourceFormValues>(emptyResourceFormValues);
  const [submitted, setSubmitted] = React.useState(false);

  React.useEffect(() => {
    if (!resource) return;
    setFormData({
      name: resource.name ?? "", email: resource.email ?? "", phone: resource.phone ?? "", jobTitle: resource.jobTitle ?? "",
      employeeId: resource.employeeId ?? "", organizationalUnitId: resource.primaryOrganizationalUnitId ?? "",
      resourceType: resource.type === "VendorResource" ? "VendorResource" : "Staff", status: resource.isActive ? "Active" : "Inactive", vendorId: resource.vendorId ?? "",
      baseCalendarId: resource.baseCalendarId ?? "", systemUserId: resource.userId ?? "", employmentType: resource.employmentType ?? "",
      seniority: resource.seniority ?? "", notes: resource.notes ?? "",
    });
    setSubmitted(false);
  }, [resource]);

  if (!open || !resource) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
    if (Object.values(validateResourceForm(dialogValuesToResourceForm(formData))).some(Boolean)) return;
    onSubmit({ id: resource.id, ...formData });
  };

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("resources.directory.editTitle")} description={t("resources.directory.formDescription")} size="lg">
        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody>
            {error ? <p role="alert" className="mb-4 text-sm text-destructive">{error}</p> : null}
            <ResourceFormSections values={formData} setValues={setFormData} calendars={calendars} units={units} users={users} vendors={vendors} calendarsLoading={calendarsLoading} usersLoading={usersLoading} vendorsLoading={vendorsLoading} readOnlyType currentVendorId={resource.vendorId} submitted={submitted} />
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isPending || !formData.name.trim()}>{isPending ? t("common.saving") : t("common.save")}</Button>
          </SideSheetFooter>
        </form>
    </SideSheet>
  );
}

export function ResourcesListPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Resources.View));
  const mutable = canMutate && !effectiveReadOnly;
  const permissions = user?.permissions ?? [];
  const canCreate = hasAnyPermission(permissions, [Permissions.Resources.Create, Permissions.Resources.Manage]) && mutable;
  const canEdit = hasAnyPermission(permissions, [Permissions.Resources.Edit, Permissions.Resources.Manage]) && mutable;
  const canDelete = hasAnyPermission(permissions, [Permissions.Resources.Delete, Permissions.Resources.Manage]) && mutable;

  const [filters, setFilters] = React.useState({
    search: "",
    status: "",
    type: "",
    systemUserId: "",
    page: 1,
    pageSize: 20,
  });
  const [createOpen, setCreateOpen] = React.useState(false);
  const [editResource, setEditResource] = React.useState<ResourceDto | null>(null);
  const [detailResource, setDetailResource] = React.useState<ResourceDto | null>(null);
  const [confirmResource, setConfirmResource] = React.useState<ResourceDto | null>(null);

  const query = useResources({ ...filters, search: filters.search.trim() || undefined, type: filters.type || undefined, status: filters.status || undefined, isActive: filters.status ? filters.status === "Active" : undefined, userId: filters.systemUserId || undefined }, canView);
  const createMutation = useCreateResource();
  const updateMutation = useUpdateResource();
  const deleteMutation = useDeleteResource();
  const detailQuery = useResource(detailResource?.id ?? "");
  const calendarsQuery = useBaseCalendars(canView);
  const usersQuery = useUserLookup(undefined, canView);
  const unitsQuery = useOrganizationUnits(canView);
  const vendorsQuery = useVendors(undefined, undefined, "all", canView && (createOpen || Boolean(editResource)));
  const mutationError = createMutation.error ?? updateMutation.error ?? deleteMutation.error;

  const handleFilterChange = (newFilters: typeof filters) => {
    setFilters({ ...newFilters, page: 1 });
  };

  const handleCreate = (data: Record<string, string>) => {
    createMutation.mutate(toCreateResourcePayload(dialogValuesToResourceForm(data)), {
      onSuccess: () => setCreateOpen(false),
    });
  };

  const handleUpdate = (data: Record<string, string> & { id: string }) => {
    updateMutation.mutate({ id: data.id, data: toUpdateResourcePayload(dialogValuesToResourceForm(data)) }, {
      onSuccess: () => setEditResource(null),
    });
  };

  const confirmStatusChange = async () => {
    if (!confirmResource) return;
    try {
      if (confirmResource.isActive) await deleteMutation.mutateAsync(confirmResource.id);
      else await updateMutation.mutateAsync({ id: confirmResource.id, data: { status: "Active", isActive: true } });
    } catch {
      // Mutation errors are shown in the page alert; keep the interaction free of an unhandled rejection.
    } finally {
      setConfirmResource(null);
    }
  };

  if (!canView) return <Card><CardContent className="p-6"><h1 className="text-lg font-semibold">{t("resources.directory.permissionRequired")}</h1><p className="mt-1 text-sm text-muted-foreground">{t("resources.directory.permissionDescription")}</p></CardContent></Card>;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t("resources.directory.title")}</h1>
          <p className="text-muted-foreground">{t("resources.directory.description", { count: query.data?.totalCount ?? 0 })}</p>
        </div>
        <div className="flex items-center gap-2"><ResourceFinderDialog />{canCreate ? <Button onClick={() => { createMutation.reset(); setCreateOpen(true); }} className="gap-2">
          <Plus className="h-4 w-4" />
          {t("resources.directory.addResource")}
        </Button> : null}</div>
      </div>

      <Card>
        <CardHeader className="pb-0">
          <CardTitle>{t("resources.directory.title")}</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <div className="space-y-4 pb-4 border-b">
            <div className="flex flex-wrap gap-3">
              <div className="flex-1 min-w-[200px]">
                <Input placeholder={t("resources.directory.searchPlaceholder")} value={filters.search} onChange={(e) => handleFilterChange({ ...filters, search: e.target.value })} className="max-w-sm" />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Select value={filters.type} onValueChange={(value) => handleFilterChange({ ...filters, type: value })}>
                  <SelectTrigger className="w-full"><SelectValue placeholder={t("resources.directory.allTypes")} /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">{t("resources.directory.allTypes")}</SelectItem>
                    <SelectItem value="Staff">{t("resources.directory.staff")}</SelectItem>
                    <SelectItem value="VendorResource">{t("resources.directory.vendorResource")}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex-1 min-w-[150px]">
                <Select value={filters.status} onValueChange={(value) => handleFilterChange({ ...filters, status: value })}>
                  <SelectTrigger className="w-full"><SelectValue placeholder={t("resources.directory.allStatuses")} /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">{t("resources.directory.allStatuses")}</SelectItem>
                    <SelectItem value="Active">{t("resources.directory.active")}</SelectItem>
                    <SelectItem value="Inactive">{t("resources.directory.inactive")}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex-1 min-w-[180px]">
                <Select value={filters.systemUserId} onValueChange={(value) => handleFilterChange({ ...filters, systemUserId: value })}>
                  <SelectTrigger className="w-full"><SelectValue placeholder={t("resources.directory.systemUserFilter")} /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">{t("resources.directory.allSystemUsers")}</SelectItem>
                    {usersQuery.data?.map((userOption) => <SelectItem key={userOption.id} value={userOption.id}>{userOption.fullName} · {userOption.email}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          {query.isLoading ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("resources.directory.name")}</TableHead><TableHead>{t("resources.directory.email")}</TableHead><TableHead>{t("resources.directory.jobTitle")}</TableHead><TableHead>{t("resources.directory.organization")}</TableHead><TableHead>{t("resources.directory.resourceType")}</TableHead><TableHead>{t("resources.directory.systemUser")}</TableHead><TableHead>{t("resources.directory.status")}</TableHead><TableHead>{t("common.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[1, 2, 3, 4, 5].map((i) => <TableRow key={i}>{Array.from({ length: 8 }, (_, index) => <TableCell key={index}><Skeleton className="h-4 w-3/4" /></TableCell>)}</TableRow>)}
              </TableBody>
            </Table>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("resources.directory.name")}</TableHead>
                  <TableHead>{t("resources.directory.email")}</TableHead>
                  <TableHead>{t("resources.directory.jobTitle")}</TableHead>
                  <TableHead>{t("resources.directory.organization")}</TableHead>
                  <TableHead>{t("resources.directory.resourceType")}</TableHead>
                  <TableHead>{t("resources.directory.systemUser")}</TableHead>
                  <TableHead>{t("resources.directory.status")}</TableHead>
                  <TableHead className="text-right">{t("common.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {query.data?.items.map((resource) => (
                  <ResourceTableRow
                    key={resource.id}
                    resource={resource}
                    onEdit={setEditResource}
                    onView={setDetailResource}
                    onDelete={setConfirmResource}
                    canEdit={canEdit}
                    canDelete={canDelete}
                  />
                ))}
                {query.data?.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8} className="text-center py-8 text-muted-foreground"><p>{t("resources.directory.emptyTitle")}</p><p className="mt-1 text-sm">{t("resources.directory.emptyDescription")}</p></TableCell>
                  </TableRow>
                ) : null}
              </TableBody>
            </Table>
          )}

          {query.isError ? <div role="alert" className="mt-4 flex items-center justify-between gap-3 text-sm text-destructive"><span>{t("resources.directory.loadError")}: {query.error instanceof Error ? query.error.message : ""}</span><Button size="sm" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div> : null}

          {query.data && (
            <div className="flex items-center justify-between mt-4 text-sm text-muted-foreground">
              <span>{t("resources.directory.paginationSummary", { from: (query.data.page - 1) * query.data.pageSize + 1, to: Math.min(query.data.page * query.data.pageSize, query.data.totalCount), total: query.data.totalCount })}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => handleFilterChange({ ...filters, page: filters.page - 1 })} disabled={filters.page <= 1}>{t("common.previous")}</Button>
                <Button variant="outline" size="sm" onClick={() => handleFilterChange({ ...filters, page: filters.page + 1 })} disabled={filters.page >= query.data.totalPages}>{t("common.next")}</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

          {mutationError && !createOpen && !editResource ? <p className="text-sm text-destructive" role="alert">{mutationError instanceof Error ? mutationError.message : t("resources.directory.mutationError")}</p> : null}
      <CreateResourceDialog open={createOpen} onOpenChange={setCreateOpen} onSubmit={handleCreate} calendars={calendarsQuery.data} units={unitsQuery.data} users={usersQuery.data} vendors={vendorsQuery.data?.items} calendarsLoading={calendarsQuery.isLoading} usersLoading={usersQuery.isLoading} vendorsLoading={vendorsQuery.isLoading} isPending={createMutation.isPending} error={createMutation.error ? createMutation.error instanceof Error ? createMutation.error.message : t("resources.directory.mutationError") : undefined} />
      <EditResourceDialog open={!!editResource} onOpenChange={(open) => { if (!open) setEditResource(null); }} resource={editResource} onSubmit={handleUpdate} calendars={calendarsQuery.data} units={unitsQuery.data} users={usersQuery.data} vendors={vendorsQuery.data?.items} calendarsLoading={calendarsQuery.isLoading} usersLoading={usersQuery.isLoading} vendorsLoading={vendorsQuery.isLoading} isPending={updateMutation.isPending} error={updateMutation.error ? updateMutation.error instanceof Error ? updateMutation.error.message : t("resources.directory.mutationError") : undefined} />
      <ConfirmDialog open={!!confirmResource} onOpenChange={(open) => { if (!open) setConfirmResource(null); }} title={confirmResource?.isActive ? t("resources.directory.deactivateConfirm") : t("resources.directory.activateConfirm")} confirmLabel={confirmResource?.isActive ? t("resources.directory.deactivate") : t("resources.directory.activate")} destructive={Boolean(confirmResource?.isActive)} loading={deleteMutation.isPending || updateMutation.isPending} onConfirm={confirmStatusChange} />
      <ResourceDetailsSheet resource={detailResource} detail={detailQuery.data} loading={detailQuery.isLoading} error={detailQuery.isError} onRetry={() => void detailQuery.refetch()} onClose={() => setDetailResource(null)} onEdit={(resource) => { setDetailResource(null); setEditResource(resource); }} canEdit={canEdit} />
    </div>
  );
}
