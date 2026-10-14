import * as React from "react";
import { cn } from "@lib/utils";

const CollapsibleContext = React.createContext<{ open: boolean; setOpen: (open: boolean) => void } | null>(null);

export function Collapsible({ open, onOpenChange, children, className }: { open: boolean; onOpenChange: (open: boolean) => void; children: React.ReactNode; className?: string }) {
  const value = React.useMemo(() => ({ open, setOpen: onOpenChange }), [open, onOpenChange]);
  return (
    <CollapsibleContext.Provider value={value}>
      <div data-state={open ? "open" : "closed"} className={cn(className)}>
        {children}
      </div>
    </CollapsibleContext.Provider>
  );
}

export function CollapsibleTrigger({ asChild, children }: { asChild?: boolean; children: React.ReactElement }) {
  const ctx = React.useContext(CollapsibleContext);
  if (!ctx) return children;
  const child = React.Children.only(children) as React.ReactElement<{
    onClick?: (e: React.MouseEvent) => void;
    onKeyDown?: (e: React.KeyboardEvent) => void;
    role?: string;
    tabIndex?: number;
  }>;
  const isNativeButton = child.type === "button";
  return React.cloneElement(child, {
    onClick: (e: React.MouseEvent) => {
      child.props.onClick?.(e);
      if (!e.defaultPrevented) ctx.setOpen(!ctx.open);
    },
    onKeyDown: (e: React.KeyboardEvent) => {
      child.props.onKeyDown?.(e);
      if (!e.defaultPrevented && !isNativeButton && (e.key === "Enter" || e.key === " ")) {
        e.preventDefault();
        ctx.setOpen(!ctx.open);
      }
    },
    role: asChild && !isNativeButton ? child.props.role ?? "button" : child.props.role,
    tabIndex: asChild && !isNativeButton ? child.props.tabIndex ?? 0 : child.props.tabIndex,
    "aria-expanded": ctx.open,
    "data-state": ctx.open ? "open" : "closed",
  } as Record<string, unknown>);
}

export function CollapsibleContent({ children, className }: { children: React.ReactNode; className?: string }) {
  const ctx = React.useContext(CollapsibleContext);
  if (!ctx?.open) return null;
  return <div className={cn(className)}>{children}</div>;
}
