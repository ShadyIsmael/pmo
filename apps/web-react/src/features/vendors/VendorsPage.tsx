import * as React from "react";
import { Search, Plus, MoreHorizontal } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { useI18n } from "@i18n/messages";
import { hasAnyPermission, Permissions } from "@lib/permissions";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { Label } from "@components/ui/Label";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@components/ui/DropdownMenu";
import { useVendors, useCreateVendor, useUpdateVendor, useDeactivateVendor, useReactivateVendor, useDeleteVendor, useVendorCategories, type VendorDto } from "./api/vendors";
import { useForm, Controller } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";

const vendorSchema = z.object({
  name: z.string().min(1),
  contactName: z.string().optional(),
  contactEmail: z.string().email().optional().or(z.literal("")),
  contactPhone: z.string().optional(),
  vendorCategoryId: z.string().min(1),
  contractValue: z.coerce.number().optional(),
  contractStart: z.string().optional(),
  contractEnd: z.string().optional(),
});

type VendorForm = z.infer<typeof vendorSchema>;

function VendorFormFields({ defaultValues, onSubmit, pending }: { defaultValues?: Partial<VendorForm>; onSubmit: (v: VendorForm) => void; pending: boolean }) {
  const { t, locale } = useI18n();
  const categories = useVendorCategories(true);
  const { control, register, handleSubmit, formState: { errors } } = useForm<VendorForm>({
    resolver: zodResolver(vendorSchema),
    defaultValues: { name: "", contactName: "", contactEmail: "", contactPhone: "", vendorCategoryId: "", ...defaultValues },
  });
  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="vendor-name">{t("resources.vendors.name")} *</Label>
          <Input id="vendor-name" {...register("name")} />
          {errors.name ? <p className="text-xs text-destructive">{t("resources.vendors.nameRequired")}</p> : null}
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1.5"><Label>{t("resources.vendors.contactName")}</Label><Input {...register("contactName")} /></div>
          <div className="space-y-1.5"><Label>{t("resources.vendors.contactEmail")}</Label><Input {...register("contactEmail")} /></div>
          <div className="space-y-1.5"><Label>{t("resources.vendors.contactPhone")}</Label><Input {...register("contactPhone")} /></div>
          <div className="space-y-1.5">
            <Label>{t("resources.vendors.category")} *</Label>
            <Controller
              control={control}
              name="vendorCategoryId"
              render={({ field }) => (
                <select className="input-field" value={field.value} onChange={(e) => field.onChange(e.target.value)}>
                  <option value="">{t("resources.vendors.selectCategory")}</option>
                  {(categories.data ?? []).map((c) => <option key={c.id} value={c.id}>{locale === "ar" ? c.nameAr : c.nameEn}</option>)}
                </select>
              )}
            />
            {errors.vendorCategoryId ? <p className="text-xs text-destructive">{t("resources.vendors.categoryRequired")}</p> : null}
          </div>
        </div>
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="submit" disabled={pending}>{pending ? t("common.saving") : t("common.save")}</Button>
      </SideSheetFooter>
    </form>
  );
}

function VendorRowActions({ vendor }: { vendor: VendorDto }) {
  const { t } = useI18n();
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const canEdit = hasAnyPermission(perms, [Permissions.Resources.Edit, Permissions.Resources.Manage]);
  const canDelete = hasAnyPermission(perms, [Permissions.Resources.Delete, Permissions.Resources.Manage]);
  const [editOpen, setEditOpen] = React.useState(false);
  const [dialog, setDialog] = React.useState<"deactivate" | "reactivate" | "delete" | null>(null);
  const deactivate = useDeactivateVendor();
  const reactivate = useReactivateVendor();
  const remove = useDeleteVendor();
  const update = useUpdateVendor();
  if (!canEdit && !canDelete) return null;
  const pending = deactivate.isPending || reactivate.isPending || remove.isPending || update.isPending;
  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" className="h-8 w-8" aria-label={t("resources.vendors.actions")} disabled={pending}>
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {canEdit ? <DropdownMenuItem onSelect={() => setEditOpen(true)}>{t("resources.vendors.edit")}</DropdownMenuItem> : null}
          {canDelete && vendor.isActive ? <DropdownMenuItem onSelect={() => setDialog("deactivate")}>{t("resources.vendors.deactivate")}</DropdownMenuItem> : null}
          {canDelete && !vendor.isActive ? <DropdownMenuItem onSelect={() => setDialog("reactivate")}>{t("resources.vendors.reactivate")}</DropdownMenuItem> : null}
          {canDelete ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem className="text-destructive" onSelect={() => setDialog("delete")}>{t("resources.vendors.delete")}</DropdownMenuItem>
            </>
          ) : null}
        </DropdownMenuContent>
      </DropdownMenu>
      <SideSheet open={editOpen} onOpenChange={setEditOpen} title={t("resources.vendors.editTitle")} description={t("resources.vendors.editDescription")}>
        <VendorFormFields
          pending={update.isPending}
          defaultValues={{ name: vendor.name, contactName: vendor.contactName, contactEmail: vendor.contactEmail, contactPhone: vendor.contactPhone, vendorCategoryId: vendor.vendorCategoryId }}
          onSubmit={(v) => update.mutate({ id: vendor.id, input: v }, { onSuccess: () => setEditOpen(false) })}
        />
      </SideSheet>
      <ConfirmDialog
        open={dialog === "deactivate"}
        onOpenChange={(o) => { if (!o) setDialog(null); }}
        title={t("resources.vendors.deactivateVendorTitle", { name: vendor.name })}
        description={t("resources.vendors.deactivateVendorDescription")}
        confirmLabel={t("resources.vendors.deactivate")}
        onConfirm={() => deactivate.mutateAsync(vendor.id)}
      />
      <ConfirmDialog
        open={dialog === "reactivate"}
        onOpenChange={(o) => { if (!o) setDialog(null); }}
        title={t("resources.vendors.reactivateVendorTitle", { name: vendor.name })}
        description={t("resources.vendors.reactivateVendorDescription")}
        confirmLabel={t("resources.vendors.reactivate")}
        onConfirm={() => reactivate.mutateAsync(vendor.id)}
      />
      <ConfirmDialog
        open={dialog === "delete"}
        onOpenChange={(o) => { if (!o) setDialog(null); }}
        title={t("resources.vendors.deleteVendorTitle", { name: vendor.name })}
        description={`${t("resources.vendors.deleteVendorDescription")} ${t("resources.vendors.deleteVendorHint")}`}
        confirmLabel={t("resources.vendors.delete")}
        destructive
        onConfirm={() => remove.mutateAsync(vendor.id)}
      />
    </>
  );
}

export function VendorsPage() {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const canView = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Resources.View, Permissions.Resources.Manage]));
  const canCreate = hasAnyPermission(user?.permissions ?? [], [Permissions.Resources.Create, Permissions.Resources.Manage]);
  const [search, setSearch] = React.useState("");
  const [categoryId, setCategoryId] = React.useState("");
  const [status, setStatus] = React.useState<"all" | "active" | "inactive">("all");
  const [createOpen, setCreateOpen] = React.useState(false);
  const deferredSearch = React.useDeferredValue(search);
  const vendorsQuery = useVendors(deferredSearch || undefined, categoryId || undefined, status);
  const categoriesQuery = useVendorCategories(false);
  const create = useCreateVendor();
  const vendors = vendorsQuery.data?.items ?? [];
  const categories = categoriesQuery.data ?? [];

  if (!canView) return <p className="text-sm text-muted-foreground">{t("resources.vendors.permissionRequired")}</p>;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("resources.vendors.listTitle")}
        description={t("resources.vendors.listDescription", { count: vendors.length })}
        actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("resources.vendors.addVendor")}</Button> : undefined}
      />
      <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-muted/20 px-3 py-2.5">
        <div className="relative min-w-56 flex-1">
          <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("resources.vendors.searchPlaceholder")} className="ps-8" />
        </div>
        <select className="h-10 w-52 rounded-md border bg-background px-3 text-sm" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} aria-label={t("resources.vendors.filterCategory")}>
          <option value="">{t("resources.vendors.allCategories")}</option>
          {categories.map((c) => <option key={c.id} value={c.id}>{locale === "ar" ? c.nameAr : c.nameEn}</option>)}
        </select>
        <select className="h-10 w-40 rounded-md border bg-background px-3 text-sm" value={status} onChange={(e) => setStatus(e.target.value as typeof status)} aria-label={t("resources.vendors.filterStatus")}>
          <option value="all">{t("resources.vendors.allStatuses")}</option>
          <option value="active">{t("resources.vendors.active")}</option>
          <option value="inactive">{t("resources.vendors.inactive")}</option>
        </select>
      </div>
      {vendorsQuery.isLoading ? (
        <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : vendorsQuery.isError ? (
        <p className="py-12 text-center text-sm text-destructive">{t("resources.vendors.loadError")}</p>
      ) : vendors.length === 0 ? (
        <p className="py-12 text-center text-sm text-muted-foreground">{t("resources.vendors.empty")}</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("resources.vendors.name")}</TableHead>
              <TableHead>{t("resources.vendors.code")}</TableHead>
              <TableHead>{t("resources.vendors.category")}</TableHead>
              <TableHead>{t("resources.vendors.contact")}</TableHead>
              <TableHead>{t("resources.vendors.status")}</TableHead>
              <TableHead className="text-end">{t("resources.vendors.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {vendors.map((vendor) => {
              const category = categories.find((c) => c.id === vendor.vendorCategoryId);
              const categoryName = vendor.categoryName ?? (category ? (locale === "ar" ? category.nameAr : category.nameEn) : null);
              const contact = [vendor.contactName, vendor.contactEmail, vendor.contactPhone].filter(Boolean).join(" · ");
              return (
                <TableRow key={vendor.id}>
                  <TableCell className="font-medium">{vendor.name}</TableCell>
                  <TableCell><span className="font-mono text-xs">{vendor.code ?? "—"}</span></TableCell>
                  <TableCell>{categoryName ?? "—"}</TableCell>
                  <TableCell className="max-w-64 truncate text-muted-foreground">{contact || "—"}</TableCell>
                  <TableCell>{vendor.isActive ? t("resources.vendors.active") : t("resources.vendors.inactive")}</TableCell>
                  <TableCell className="text-end"><VendorRowActions vendor={vendor} /></TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("resources.vendors.createTitle")} description={t("resources.vendors.formDescription")}>
        <VendorFormFields pending={create.isPending} onSubmit={(v) => create.mutate(v, { onSuccess: () => setCreateOpen(false) })} />
      </SideSheet>
    </div>
  );
}
