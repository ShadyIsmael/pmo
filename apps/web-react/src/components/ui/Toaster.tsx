import * as React from "react";
import { useToast } from "../../hooks/use-toast";
import { cn } from "../../lib/utils";

export function Toaster() {
  const { toasts, dismiss } = useToast();
  return (
    <div className="fixed bottom-4 right-4 z-[100] flex w-[380px] max-w-[calc(100vw-2rem)] flex-col gap-2" aria-live="polite">
      {toasts.map((t) => (
        <div
          key={t.id}
          role="status"
          className={cn(
            "rounded-lg border bg-card p-4 shadow-lg",
            t.variant === "destructive" && "border-destructive/40 bg-destructive/5",
            t.variant === "success" && "border-success/40"
          )}
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              {t.title ? <p className="text-sm font-semibold">{t.title}</p> : null}
              {t.description ? <p className="mt-1 text-sm text-muted-foreground">{t.description}</p> : null}
            </div>
            <button type="button" aria-label="Dismiss" onClick={() => dismiss(t.id)} className="text-muted-foreground hover:text-foreground">✕</button>
          </div>
        </div>
      ))}
    </div>
  );
}
