import * as React from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { cn } from "@lib/utils";

export type SideSheetSize = "sm" | "md" | "lg" | "xl" | "full";

const sideSheetSizes: Record<SideSheetSize, string> = {
  sm: "sm:max-w-sm",
  md: "sm:max-w-lg",
  lg: "sm:max-w-2xl lg:max-w-3xl",
  xl: "sm:max-w-3xl lg:max-w-5xl",
  full: "sm:max-w-[calc(100vw-2rem)]",
};

export interface SideSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  size?: SideSheetSize;
  closeLabel?: string;
  className?: string;
}

export function SideSheet({ open, onOpenChange, title, description, children, size = "lg", closeLabel = "Close", className }: SideSheetProps) {
  React.useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onOpenChange(false);
    };
    document.addEventListener("keydown", onKey);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = "";
    };
  }, [open, onOpenChange]);

  if (!open) return null;
  return createPortal(
    <div className="fixed inset-0 z-50" role="dialog" aria-modal="true" aria-label={typeof title === "string" ? title : undefined}>
      <button type="button" aria-label={closeLabel} onClick={() => onOpenChange(false)} className="absolute inset-0 bg-black/50" />
      <div className={cn("absolute inset-y-0 end-0 flex w-full max-w-md flex-col border-s bg-background shadow-xl", sideSheetSizes[size], className)}>
        <div className="flex shrink-0 items-start gap-3 border-b px-5 py-4">
          <div className="min-w-0 flex-1">
            <h2 className="text-base font-semibold">{title}</h2>
            {description ? <p className="mt-1 text-sm text-muted-foreground">{description}</p> : null}
          </div>
          <button
            type="button"
            aria-label={closeLabel}
            onClick={() => onOpenChange(false)}
            className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        {children}
      </div>
    </div>,
    document.body
  );
}

export function SideSheetBody({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("min-h-0 flex-1 overflow-y-auto px-5 py-4", className)} {...props} />;
}

export function SideSheetFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("flex shrink-0 flex-col-reverse gap-2 border-t bg-background px-5 py-4 sm:flex-row sm:justify-end", className)} {...props} />;
}
