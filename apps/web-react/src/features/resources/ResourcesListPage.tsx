import * as React from "react";
import { useResources, useCreateResource, useUpdateResource, useDeleteResource, useBaseCalendars } from "./api/resources";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Badge } from "@components/ui/Badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@components/ui/Dialog";
import { Textarea } from "@components/ui/Textarea";
import { Label } from "@components/ui/Label";
import { Skeleton } from "@components/ui/Skeleton";
import { Plus, Edit, Trash2 } from "lucide-react";

interface ResourceTableRowProps {
  resource: any;
  onEdit: (resource: any) => void;
  onDelete: (resource: any) => void;
  canEdit: boolean;
  canDelete: boolean;
}

function ResourceTableRow({ resource, onEdit, onDelete, canEdit, canDelete }: ResourceTableRowProps) {
  const typeLabel = resource.type === "Staff" ? "Staff" : "Vendor Resource";
  const statusLabel = resource.isActive ? "Active" : "Inactive";

  return (
    <TableRow>
      <TableCell className="font-medium">{resource.name}</TableCell>
      <TableCell>{resource.employeeId || "—"}</TableCell>
      <TableCell>
        <Badge status={resource.type}>{typeLabel}</Badge>
      </TableCell>
      <TableCell>
        <Badge status={resource.status}>{statusLabel}</Badge>
      </TableCell>
      <TableCell>{resource.departmentName || "—"}</TableCell>
      <TableCell>{resource.jobTitle || "—"}</TableCell>
      <TableCell className="text-right">
        <div className="flex items-center justify-end gap-1">
          {canEdit && <Button variant="ghost" size="icon" onClick={() => onEdit(resource)}><Edit className="h-4 w-4" /></Button>}
          {canDelete && <Button variant="ghost" size="icon" className="text-destructive hover:text-destructive" onClick={() => onDelete(resource)}><Trash2 className="h-4 w-4" /></Button>}
        </div>
      </TableCell>
    </TableRow>
  );
}

function CreateResourceDialog({ open, onOpenChange, onSubmit, calendars: _calendars, isPending }: { open: boolean; onOpenChange: (open: boolean) => void; onSubmit: (data: any) => void; calendars?: any[]; isPending: boolean }) {
  void _calendars;
  const { t } = useI18n();
  const [formData, setFormData] = React.useState({
    name: "",
    email: "",
    phone: "",
    jobTitle: "",
    employeeId: "",
    organizationalUnitId: "",
    resourceType: "Staff",
    status: "Active",
    vendorId: "",
    baseCalendarId: "",
    systemUserId: "",
    employmentType: "",
    seniority: "",
    notes: "",
  });

  if (!open) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(formData);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("resources.directory.createTitle")}</DialogTitle>
          <DialogDescription>{t("resources.directory.formDescription")}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5"><Label htmlFor="name">{t("resources.directory.name")} *</Label><Input id="name" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label htmlFor="email">{t("resources.directory.email")}</Label><Input id="email" type="email" value={formData.email} onChange={(e) => setFormData({ ...formData, email: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="phone">{t("resources.directory.phone")}</Label><Input id="phone" value={formData.phone} onChange={(e) => setFormData({ ...formData, phone: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="jobTitle">{t("resources.directory.jobTitle")}</Label><Input id="jobTitle" value={formData.jobTitle} onChange={(e) => setFormData({ ...formData, jobTitle: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="employeeId">{t("resources.directory.employeeId")}</Label><Input id="employeeId" value={formData.employeeId} onChange={(e) => setFormData({ ...formData, employeeId: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="resourceType">{t("resources.directory.resourceType")} *</Label>
              <Select value={formData.resourceType} onValueChange={(value) => setFormData({ ...formData, resourceType: value })}>
                <SelectTrigger><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Staff">{t("resources.directory.staff")}</SelectItem>
                  <SelectItem value="VendorResource">{t("resources.directory.vendorResource")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5"><Label htmlFor="status">{t("resources.directory.status")}</Label>
              <Select value={formData.status} onValueChange={(value) => setFormData({ ...formData, status: value })}>
                <SelectTrigger><SelectValue placeholder={t("common.select")} /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">{t("resources.directory.active")}</SelectItem>
                  <SelectItem value="Inactive">{t("resources.directory.inactive")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="notes">{t("resources.directory.notes")}</Label>
            <Textarea id="notes" value={formData.notes} onChange={(e) => setFormData({ ...formData, notes: e.target.value })} placeholder={t("resources.directory.notes")} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isPending}>{isPending ? t("common.saving") : t("common.create")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function EditResourceDialog({ open, onOpenChange, resource, onSubmit, isPending }: { open: boolean; onOpenChange: (open: boolean) => void; resource: any; onSubmit: (data: any) => void; isPending: boolean }) {
  const { t } = useI18n();
  const [formData, setFormData] = React.useState({
    name: resource?.name || "",
    email: resource?.email || "",
    phone: resource?.phone || "",
    jobTitle: resource?.jobTitle || "",
    employeeId: resource?.employeeId || "",
    organizationalUnitId: resource?.primaryOrganizationalUnitId || "",
    resourceType: resource?.type || "Staff",
    status: resource?.isActive ? "Active" : "Inactive",
    vendorId: resource?.vendorId || "",
    baseCalendarId: resource?.baseCalendarId || "",
    systemUserId: resource?.userId || "",
    employmentType: resource?.employmentType || "",
    seniority: resource?.seniority || "",
    notes: resource?.notes || "",
  });

  if (!open || !resource) return null;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({ id: resource.id, ...formData });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("resources.directory.editTitle")}</DialogTitle>
          <DialogDescription>{t("resources.directory.formDescription")}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5"><Label htmlFor="name">{t("resources.directory.name")} *</Label><Input id="name" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} required /></div>
            <div className="space-y-1.5"><Label htmlFor="email">{t("resources.directory.email")}</Label><Input id="email" type="email" value={formData.email} onChange={(e) => setFormData({ ...formData, email: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="phone">{t("resources.directory.phone")}</Label><Input id="phone" value={formData.phone} onChange={(e) => setFormData({ ...formData, phone: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="jobTitle">{t("resources.directory.jobTitle")}</Label><Input id="jobTitle" value={formData.jobTitle} onChange={(e) => setFormData({ ...formData, jobTitle: e.target.value })} /></div>
            <div className="space-y-1.5"><Label htmlFor="employeeId">{t("resources.directory.employeeId")}</Label><Input id="employeeId" value={formData.employeeId} onChange={(e) => setFormData({ ...formData, employeeId: e.target.value })} /></div>
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="notes">{t("resources.directory.notes")}</Label>
            <Textarea id="notes" value={formData.notes} onChange={(e) => setFormData({ ...formData, notes: e.target.value })} placeholder={t("resources.directory.notes")} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={isPending}>{isPending ? t("common.saving") : t("common.save")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function DeleteConfirmDialog({ open, onOpenChange, onConfirm }: { open: boolean; onOpenChange: (open: boolean) => void; onConfirm: () => void }) {
  const { t } = useI18n();
  if (!open) return null;
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("resources.directory.deactivateConfirm")}</DialogTitle></DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button variant="destructive" onClick={() => { onConfirm(); onOpenChange(false); }}>{t("common.delete")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function ResourcesListPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Create));
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Edit));
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Resources.Delete));

  const [filters, setFilters] = React.useState({
    search: "",
    status: "",
    type: "",
    page: 1,
    pageSize: 20,
  });
  const [createOpen, setCreateOpen] = React.useState(false);
  const [editResource, setEditResource] = React.useState<any>(null);
  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [deleteResourceId, setDeleteResourceId] = React.useState<string | null>(null);

  const query = useResources(filters, true);
  const createMutation = useCreateResource();
  const updateMutation = useUpdateResource();
  const deleteMutation = useDeleteResource();
  const calendarsQuery = useBaseCalendars(true);

  const handleFilterChange = (newFilters: typeof filters) => {
    setFilters({ ...newFilters, page: 1 });
  };

  const handleCreate = (data: any) => {
    createMutation.mutate(data, {
      onSuccess: () => setCreateOpen(false),
    });
  };

  const handleUpdate = (data: any) => {
    updateMutation.mutate({ id: editResource?.id, data }, {
      onSuccess: () => setEditResource(null),
    });
  };

  const handleDelete = (id: string) => {
    setDeleteResourceId(id);
    setDeleteOpen(true);
  };

  const confirmDelete = () => {
    if (deleteResourceId) {
      deleteMutation.mutate(deleteResourceId, {
        onSuccess: () => {
          setDeleteResourceId(null);
          setDeleteOpen(false);
        },
      });
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t("resources.directory.title")}</h1>
          <p className="text-muted-foreground">{t("resources.directory.description", { count: query.data?.totalCount ?? 0 })}</p>
        </div>
        <Button onClick={() => setCreateOpen(true)} disabled={!canCreate} className="gap-2">
          <Plus className="h-4 w-4" />
          {t("resources.directory.addResource")}
        </Button>
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
                    <SelectItem value="Staff">Staff</SelectItem>
                    <SelectItem value="VendorResource">Vendor Resource</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex-1 min-w-[150px]">
                <Select value={filters.status} onValueChange={(value) => handleFilterChange({ ...filters, status: value })}>
                  <SelectTrigger className="w-full"><SelectValue placeholder={t("resources.directory.allStatuses")} /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">{t("resources.directory.allStatuses")}</SelectItem>
                    <SelectItem value="Active">Active</SelectItem>
                    <SelectItem value="Inactive">Inactive</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          {query.isLoading ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Employee ID</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Department</TableHead>
                  <TableHead>Job Title</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[1, 2, 3, 4, 5].map((i) => <TableRow key={i}><TableCell><Skeleton className="h-4 w-3/4" /></TableCell><TableCell><Skeleton className="h-4 w-1/2" /></TableCell><TableCell><Skeleton className="h-4 w-1/2" /></TableCell><TableCell><Skeleton className="h-4 w-1/2" /></TableCell><TableCell><Skeleton className="h-4 w-1/2" /></TableCell><TableCell><Skeleton className="h-4 w-1/2" /></TableCell><TableCell></TableCell></TableRow>)}
              </TableBody>
            </Table>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("resources.directory.name")}</TableHead>
                  <TableHead>{t("resources.directory.employeeId")}</TableHead>
                  <TableHead>{t("resources.directory.resourceType")}</TableHead>
                  <TableHead>{t("resources.directory.status")}</TableHead>
                  <TableHead>{t("resources.directory.organization")}</TableHead>
                  <TableHead>{t("resources.directory.jobTitle")}</TableHead>
                  <TableHead className="text-right">{t("common.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {query.data?.items.map((resource) => (
                  <ResourceTableRow
                    key={resource.id}
                    resource={resource}
                    onEdit={setEditResource}
                    onDelete={(r: { id: string }) => handleDelete(r.id)}
                    canEdit={canEdit}
                    canDelete={canDelete}
                  />
                )) || (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">{t("resources.directory.emptyTitle")}</TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}

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

      <CreateResourceDialog open={createOpen} onOpenChange={setCreateOpen} onSubmit={handleCreate} calendars={calendarsQuery.data} isPending={createMutation.isPending} />
      <EditResourceDialog open={!!editResource} onOpenChange={setEditResource} resource={editResource} onSubmit={handleUpdate} isPending={updateMutation.isPending} />
      <DeleteConfirmDialog open={deleteOpen} onOpenChange={setDeleteOpen} onConfirm={confirmDelete} />
    </div>
  );
}