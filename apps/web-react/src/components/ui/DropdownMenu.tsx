import * as React from "react";
import { createPortal } from "react-dom";
import { cn } from "@lib/utils";
import { useAnchoredOverlayPosition, type AnchoredOverlayAlignment } from "./AnchoredOverlay";

interface MenuContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
  triggerRef: React.MutableRefObject<HTMLButtonElement | null>;
  contentRef: React.MutableRefObject<HTMLDivElement | null>;
}

const MenuContext = React.createContext<MenuContextValue | null>(null);

export function DropdownMenu({ children, className }: { children: React.ReactNode; className?: string }) {
  const [open, setOpen] = React.useState(false);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const contentRef = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (triggerRef.current?.contains(target) || contentRef.current?.contains(target)) return;
      setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <MenuContext.Provider value={{ open, setOpen, triggerRef, contentRef }}>
      <div className={cn("relative", className)}>{children}</div>
    </MenuContext.Provider>
  );
}

function assignRef<T>(ref: React.Ref<T> | undefined, value: T | null) {
  if (typeof ref === "function") ref(value);
  else if (ref) (ref as React.MutableRefObject<T | null>).current = value;
}

export function DropdownMenuTrigger({ children, asChild }: { children: React.ReactElement; asChild?: boolean }) {
  const ctx = React.useContext(MenuContext);
  if (!ctx) throw new Error("DropdownMenuTrigger must be used within DropdownMenu");
  void asChild;
  const child = React.Children.only(children) as React.ReactElement<{ onClick?: (e: React.MouseEvent) => void }>;
  const childRef = (child as React.ReactElement & { ref?: React.Ref<HTMLButtonElement> }).ref;
  return React.cloneElement(child, {
    ref: (node: HTMLButtonElement | null) => {
      ctx.triggerRef.current = node;
      assignRef(childRef, node);
    },
    onClick: (e: React.MouseEvent) => {
      child.props.onClick?.(e);
      ctx.setOpen(!ctx.open);
    },
    "aria-expanded": ctx.open,
    "aria-haspopup": "menu",
  } as Record<string, unknown>);
}

export function DropdownMenuContent({ children, align = "end", className }: { children: React.ReactNode; align?: AnchoredOverlayAlignment; className?: string }) {
  const ctx = React.useContext(MenuContext);
  if (!ctx) throw new Error("DropdownMenuContent must be used within DropdownMenu");
  const style = useAnchoredOverlayPosition({ open: ctx.open, anchorRef: ctx.triggerRef, contentRef: ctx.contentRef, align });
  if (!ctx.open) return null;
  return createPortal(
    <div ref={ctx.contentRef} role="menu" style={style} className={cn("min-w-[12rem] overflow-y-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md", className)}>
      {children}
    </div>,
    document.body
  );
}

function useCloseMenu() {
  const ctx = React.useContext(MenuContext);
  return () => ctx?.setOpen(false);
}

export function DropdownMenuItem({ children, className, onSelect, onClick, disabled }: { children: React.ReactNode; className?: string; onSelect?: () => void; onClick?: () => void; disabled?: boolean }) {
  const close = useCloseMenu();
  return (
    <button
      type="button"
      role="menuitem"
      disabled={disabled}
      onClick={() => {
        onSelect?.();
        onClick?.();
        close();
      }}
      className={cn(
        "flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-sm text-start hover:bg-accent hover:text-accent-foreground focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50",
        className
      )}
    >
      {children}
    </button>
  );
}

export function DropdownMenuLabel({ children, className }: { children: React.ReactNode; className?: string }) {
  return <p className={cn("px-2 py-1.5 text-xs font-semibold text-muted-foreground", className)}>{children}</p>;
}

export function DropdownMenuSeparator({ className }: { className?: string }) {
  return <hr className={cn("my-1 h-px bg-border", className)} />;
}

export function DropdownMenuGroup({ children }: { children: React.ReactNode }) {
  return <div>{children}</div>;
}
