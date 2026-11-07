import * as React from "react";
import { useToast } from "../../hooks/use-toast";
import { cn } from "../../lib/utils";
import { useI18n } from "@i18n/messages";

export function Toaster() {
  const { t } = useI18n();
  const { toasts, dismiss } = useToast();
  return (
    <div className="fixed bottom-4 right-4 z-[100] flex w-[380px] max-w-[calc(100vw-2rem)] flex-col gap-2" aria-live="polite">
      {toasts.map((toastItem) => (
        <div
          key={toastItem.id}
          role="status"
          className={cn(
            "rounded-lg border bg-card p-4 shadow-lg",
            toastItem.variant === "destructive" && "border-destructive/40 bg-destructive/5",
            toastItem.variant === "success" && "border-success/40"
          )}
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              {toastItem.title ? <p className="text-sm font-semibold">{toastItem.title}</p> : null}
              {toastItem.description ? <p className="mt-1 text-sm text-muted-foreground">{toastItem.description}</p> : null}
            </div>
            <button type="button" aria-label={t("common.dismiss")} onClick={() => dismiss(toastItem.id)} className="text-muted-foreground hover:text-foreground">✕</button>
          </div>
        </div>
      ))}
    </div>
  );
}
