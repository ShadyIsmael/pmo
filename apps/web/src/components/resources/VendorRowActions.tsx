"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { MoreHorizontal } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import {
  useDeactivateVendor,
  useDeleteVendor,
  useReactivateVendor,
  type VendorDto,
} from "@/hooks/resources/useVendors";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EditVendorButton } from "@/components/resources/EditVendorButton";

type LifecycleDialog =
  | { kind: "deactivate" }
  | { kind: "reactivate" }
  | { kind: "delete" }
  | null;

function apiErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export function VendorRowActions({ vendor }: { vendor: VendorDto }) {
  const t = useTranslations("resources.vendors");
  const { user } = useAuth();
  const canManage = hasPermission(user?.permissions ?? [], Permissions.Resources.Manage);
  const [editOpen, setEditOpen] = useState(false);
  const [dialog, setDialog] = useState<LifecycleDialog>(null);
  const [error, setError] = useState<string | null>(null);
  const deactivate = useDeactivateVendor();
  const reactivate = useReactivateVendor();
  const remove = useDeleteVendor();

  if (!canManage) {
    return null;
  }

  const pending = deactivate.isPending || reactivate.isPending || remove.isPending;

  const closeDialog = () => {
    if (pending) return;
    setDialog(null);
    setError(null);
  };

  const runDeactivate = async () => {
    setError(null);
    try {
      await deactivate.mutateAsync(vendor.id);
      setDialog(null);
    } catch (err) {
      setError(apiErrorMessage(err, t("mutationError")));
    }
  };

  const runReactivate = async () => {
    setError(null);
    try {
      await reactivate.mutateAsync(vendor.id);
      setDialog(null);
    } catch (err) {
      setError(apiErrorMessage(err, t("mutationError")));
    }
  };

  const runDelete = async () => {
    setError(null);
    try {
      await remove.mutateAsync(vendor.id);
      setDialog(null);
    } catch (err) {
      setError(apiErrorMessage(err, t("mutationError")));
    }
  };

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" className="h-8 w-8" aria-label={t("actions")}>
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" sideOffset={4} className="min-w-44">
          <DropdownMenuItem onClick={() => setEditOpen(true)} disabled={pending}>
            {t("edit")}
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          {vendor.isActive ? (
            <DropdownMenuItem
              onClick={() => { setError(null); setDialog({ kind: "deactivate" }); }}
              disabled={pending}
              className="text-warning focus:text-warning"
            >
              {t("deactivate")}
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem
              onClick={() => { setError(null); setDialog({ kind: "reactivate" }); }}
              disabled={pending}
              className="text-success focus:text-success"
            >
              {t("reactivate")}
            </DropdownMenuItem>
          )}
          <DropdownMenuItem
            onClick={() => { setError(null); setDialog({ kind: "delete" }); }}
            disabled={pending}
            className="text-destructive focus:text-destructive"
          >
            {t("delete")}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <EditVendorButton vendor={vendor} open={editOpen} onOpenChange={setEditOpen} hideTrigger />

      {dialog?.kind === "deactivate" ? (
        <ConfirmDialog
          title={t("deactivateVendorTitle", { name: vendor.name })}
          description={t("deactivateVendorDescription")}
          confirmLabel={pending ? t("deactivating") : t("deactivate")}
          cancelLabel={t("cancel")}
          pendingLabel={t("deactivating")}
          variant="warning"
          pending={pending}
          error={error}
          onConfirm={runDeactivate}
          onCancel={closeDialog}
        />
      ) : null}
      {dialog?.kind === "reactivate" ? (
        <ConfirmDialog
          title={t("reactivateVendorTitle", { name: vendor.name })}
          description={t("reactivateVendorDescription")}
          confirmLabel={pending ? t("reactivating") : t("reactivate")}
          cancelLabel={t("cancel")}
          pendingLabel={t("reactivating")}
          pending={pending}
          error={error}
          onConfirm={runReactivate}
          onCancel={closeDialog}
        />
      ) : null}
      {dialog?.kind === "delete" ? (
        <ConfirmDialog
          title={t("deleteVendorTitle", { name: vendor.name })}
          description={t("deleteVendorDescription")}
          details={
            <p className="rounded-md border border-border bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
              {t("deleteVendorHint")}
            </p>
          }
          confirmLabel={pending ? t("deleting") : t("delete")}
          cancelLabel={t("cancel")}
          pendingLabel={t("deleting")}
          variant="destructive"
          pending={pending}
          error={error}
          onConfirm={runDelete}
          onCancel={closeDialog}
        />
      ) : null}
    </>
  );
}
