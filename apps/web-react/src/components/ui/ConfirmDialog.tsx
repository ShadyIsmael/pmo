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
  const dialogRootRef = React.useRef<HTMLDivElement>(null);
  const cancelButtonRef = React.useRef<HTMLButtonElement>(null);
  const busyRef = React.useRef(false);
  const loadingRef = React.useRef(loading);
  const onOpenChangeRef = React.useRef(onOpenChange);
  const titleId = React.useId();
  const descriptionId = React.useId();
  busyRef.current = busy;
  loadingRef.current = loading;
  onOpenChangeRef.current = onOpenChange;

  React.useEffect(() => {
    if (!open) return undefined;
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    cancelButtonRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        if (!busyRef.current && !loadingRef.current) onOpenChangeRef.current(false);
        return;
      }
      if (event.key !== "Tab") return;
      const buttons = Array.from(dialogRootRef.current?.querySelectorAll<HTMLButtonElement>("button:not(:disabled)") ?? []);
      if (!buttons.length) return;
      const first = buttons[0];
      const last = buttons[buttons.length - 1];
      if (event.shiftKey && (document.activeElement === first || !buttons.includes(document.activeElement as HTMLButtonElement))) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && (document.activeElement === last || !buttons.includes(document.activeElement as HTMLButtonElement))) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      previouslyFocused?.focus();
    };
  }, [open]);

  async function handleConfirm() {
    try {
      busyRef.current = true;
      setBusy(true);
      await onConfirm();
      onOpenChange(false);
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }
  return (
    <Dialog
      ref={dialogRootRef}
      open={open}
      role="alertdialog"
      ariaLabelledBy={titleId}
      ariaDescribedBy={description ? descriptionId : undefined}
      onOpenChange={(nextOpen) => {
        if (nextOpen || (!loading && !busy)) onOpenChange(nextOpen);
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle id={titleId}>{title}</DialogTitle>
          {description ? <DialogDescription id={descriptionId}>{description}</DialogDescription> : null}
        </DialogHeader>
        <DialogFooter className="gap-2 sm:space-x-0">
          <Button ref={cancelButtonRef} type="button" variant="outline" disabled={loading || busy} onClick={() => onOpenChange(false)}>{cancelLabel ?? t("common.cancel")}</Button>
          <Button type="button" variant={destructive ? "outline" : "default"} className={destructive ? "border-destructive text-destructive hover:bg-destructive/10 hover:text-destructive" : undefined} disabled={loading || busy} onClick={() => void handleConfirm()}>
            {confirmLabel ?? t("common.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
