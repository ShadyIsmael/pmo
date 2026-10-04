"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Search } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { EmptyState, ErrorState, LoadingSkeleton, PermissionDenied } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useVendors } from "@/hooks/resources/useVendors";
import { useVendorCategories } from "@/lib/api/reference-data";
import type { VendorStatusFilter } from "@/lib/api/resources";
import { AddVendorButton } from "@/components/resources/AddVendorButton";
import { VendorRowActions } from "@/components/resources/VendorRowActions";
import { cn } from "@/lib/utils";
import { getLookupTextClass, getStatusTextClass } from "@/lib/status-presentation";

export default function VendorsPage() {
  const t = useTranslations("resources.vendors");
  const locale = useLocale();
  const { user, status: authStatus } = useAuth();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Resources.View));
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [status, setStatus] = useState<VendorStatusFilter>("all");
  const vendorsQuery = useVendors(search || undefined, categoryId || undefined, status);
  const categoriesQuery = useVendorCategories(false);

  const vendors = vendorsQuery.data ?? [];
  const categories = categoriesQuery.data ?? [];

  if (authStatus === "loading") {
    return <LoadingSkeleton rows={6} />;
  }

  if (!canView) {
    return <PermissionDenied title={t("permissionTitle")} description={t("permissionRequired")} />;
  }

  const hasFilters = Boolean(search || categoryId || status !== "all");
  const clearFilters = () => {
    setSearch("");
    setCategoryId("");
    setStatus("all");
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("listTitle")}
        description={t("listDescription", { count: vendors.length })}
        actions={<AddVendorButton />}
      />

      <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-muted/20 px-3 py-2.5">
        <div className="relative min-w-56 flex-1">
          <Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" aria-hidden="true" />
          <Input
            placeholder={t("searchPlaceholder")}
            className="bg-background ps-8"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            aria-label={t("searchPlaceholder")}
          />
        </div>
        <label className="sr-only" htmlFor="vendor-category-filter">{t("filterCategory")}</label>
        <select
          id="vendor-category-filter"
          className="h-10 w-52 rounded-md border border-input bg-background px-3 text-sm"
          value={categoryId}
          onChange={(event) => setCategoryId(event.target.value)}
        >
          <option value="">{t("allCategories")}</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {locale === "ar" ? category.nameAr : category.nameEn}
            </option>
          ))}
        </select>
        <label className="sr-only" htmlFor="vendor-status-filter">{t("filterStatus")}</label>
        <select
          id="vendor-status-filter"
          className="h-10 w-40 rounded-md border border-input bg-background px-3 text-sm"
          value={status}
          onChange={(event) => setStatus(event.target.value as VendorStatusFilter)}
        >
          <option value="all">{t("allStatuses")}</option>
          <option value="active">{t("active")}</option>
          <option value="inactive">{t("inactive")}</option>
        </select>
        {hasFilters ? (
          <Button variant="ghost" size="sm" onClick={clearFilters}>
            {t("clearFilters")}
          </Button>
        ) : null}
      </div>

      {vendorsQuery.isLoading ? (
        <LoadingSkeleton rows={5} />
      ) : vendorsQuery.isError ? (
        <ErrorState
          title={t("loadError")}
          description={vendorsQuery.error instanceof Error ? vendorsQuery.error.message : undefined}
          onRetry={() => void vendorsQuery.refetch()}
        />
      ) : vendors.length === 0 ? (
        <EmptyState title={t("empty")} description={t("emptyDescription")} action={<AddVendorButton />} />
      ) : (
        <TooltipProvider>
          <Table>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="w-56 text-start">{t("name")}</TableHead>
                <TableHead className="w-32 text-start">{t("code")}</TableHead>
                <TableHead className="w-44 text-start">{t("category")}</TableHead>
                <TableHead className="text-start">{t("contact")}</TableHead>
                <TableHead className="w-28 text-start">{t("status")}</TableHead>
                <TableHead className="w-14 text-end">{t("actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {vendors.map((vendor) => {
                const category = categories.find((c) => c.id === vendor.vendorCategoryId);
                const categoryName = vendor.categoryName ?? (category
                  ? (locale === "ar" ? category.nameAr : category.nameEn)
                  : null);
                const contact = [vendor.contactName, vendor.contactEmail, vendor.contactPhone].filter(Boolean).join(" · ");
                return (
                    <TableRow key={vendor.id}>
                    <TableCell className="w-56 text-start align-middle">
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <p className={cn("truncate font-medium", !vendor.isActive && "text-muted-foreground")}>{vendor.name}</p>
                        </TooltipTrigger>
                        <TooltipContent side="top" className="max-w-xs">
                          <p className="font-medium">{vendor.name}</p>
                        </TooltipContent>
                      </Tooltip>
                    </TableCell>
                    <TableCell className="w-32 text-start align-middle" title={vendor.code ?? undefined}>
                      <span className="font-mono text-xs font-medium tracking-wide">{vendor.code ?? "—"}</span>
                    </TableCell>
                    <TableCell className="w-44 text-start align-middle">
                      {categoryName ? (
                        <span className={cn("block max-w-full truncate text-sm font-medium", getLookupTextClass(categoryName))}>
                          {categoryName}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell className="max-w-64 truncate text-start align-middle text-muted-foreground" title={contact || undefined}>
                      {contact || "—"}
                    </TableCell>
                    <TableCell className="w-28 text-start align-middle">
                      <span className={cn("text-sm font-medium", getStatusTextClass(vendor.isActive ? "Active" : "Inactive"))}>
                        {vendor.isActive ? t("active") : t("inactive")}
                      </span>
                    </TableCell>
                    <TableCell className="w-14 text-end align-middle">
                      <VendorRowActions vendor={vendor} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TooltipProvider>
      )}
    </div>
  );
}
