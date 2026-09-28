"use client";

import * as React from "react";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

export interface ConfirmDialogProps {
  open?: boolean;
  // eslint-disable-next-line no-unused-vars
  onOpenChange?: (next: boolean) => void;
  title: string;
  description?: string;
  /** Optional structured detail block (e.g. dependency counts). */
  details?: React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  pendingLabel?: string;
  variant?: "default" | "warning" | "destructive";
  pending?: boolean;
  error?: string | null;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
}

const toneIcon: Record<NonNullable<ConfirmDialogProps["variant"]>, string> = {
  default: "bg-primary/10 text-primary",
  warning: "bg-warning/10 text-warning",
  destructive: "bg-destructive/10 text-destructive",
};

export function ConfirmDialog({
  open = true,
  onOpenChange,
  title,
  description,
  details,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  pendingLabel,
  variant = "default",
  pending = false,
  error = null,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  if (!open) return null;

  const handleCancel = () => {
    if (pending) return;
    onCancel();
    onOpenChange?.(false);
  };

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) handleCancel(); onOpenChange?.(next); }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader className="sm:text-start">
          <div className="flex items-start gap-3">
            {variant !== "default" ? (
              <span className={cn("mt-0.5 inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full", toneIcon[variant])} aria-hidden="true">
                <AlertTriangle className="h-4 w-4" />
              </span>
            ) : null}
            <div className="min-w-0 space-y-1.5">
              <DialogTitle>{title}</DialogTitle>
              {description && <DialogDescription>{description}</DialogDescription>}
            </div>
          </div>
        </DialogHeader>
        {(details || error) && (
          <div className="space-y-2 px-6 pb-1">
            {details}
            {error ? (
              <p className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive" role="alert">
                {error}
              </p>
            ) : null}
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={handleCancel} disabled={pending}>
            {cancelLabel}
          </Button>
          <Button
            variant={variant === "destructive" ? "destructive" : variant === "warning" ? "default" : "default"}
            className={variant === "warning" ? "bg-warning text-warning-foreground hover:bg-warning/90" : undefined}
            onClick={() => void onConfirm()}
            disabled={pending}
          >
            {pending ? (pendingLabel ?? confirmLabel) : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
