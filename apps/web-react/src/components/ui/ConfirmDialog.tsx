import * as React from "react";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "./Dialog";
import { Button } from "./Button";
import { useI18n } from "@i18n/messages";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  loading?: boolean;
  onConfirm: () => void | Promise<unknown>;
}

export function ConfirmDialog({ open, onOpenChange, title, description, confirmLabel, cancelLabel, destructive, loading, onConfirm }: ConfirmDialogProps) {
  const { t } = useI18n();
  const [busy, setBusy] = React.useState(false);
  async function handleConfirm() {
    try {
      setBusy(true);
      await onConfirm();
      onOpenChange(false);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description ? <DialogDescription>{description}</DialogDescription> : null}
        </DialogHeader>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{cancelLabel ?? t("common.cancel")}</Button>
          <Button type="button" variant={destructive ? "destructive" : "default"} disabled={loading || busy} onClick={() => void handleConfirm()}>
            {confirmLabel ?? t("common.delete")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
