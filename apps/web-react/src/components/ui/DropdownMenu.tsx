import * as React from "react";
import { cn } from "@lib/utils";

interface MenuContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const MenuContext = React.createContext<MenuContextValue | null>(null);

export function DropdownMenu({ children, className }: { children: React.ReactNode; className?: string }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open ]);

  return (
    <MenuContext.Provider value={{ open, setOpen }}>
      <div ref={ref} className={cn("relative", className)}>{children}</div>
    </MenuContext.Provider>
  );
}

export function DropdownMenuTrigger({ children, asChild }: { children: React.ReactElement; asChild?: boolean }) {
  const ctx = React.useContext(MenuContext);
  if (!ctx) throw new Error("DropdownMenuTrigger must be used within DropdownMenu");
  void asChild;
  const child = React.Children.only(children) as React.ReactElement<{ onClick?: (e: React.MouseEvent) => void }>;
  return React.cloneElement(child, {
    onClick: (e: React.MouseEvent) => {
      child.props.onClick?.(e);
      ctx.setOpen(!ctx.open);
    },
    "aria-expanded": ctx.open,
    "aria-haspopup": "menu",
  } as Record<string, unknown>);
}

export function DropdownMenuContent({ children, align = "end", className }: { children: React.ReactNode; align?: "start" | "center" | "end"; className?: string }) {
  const ctx = React.useContext(MenuContext);
  if (!ctx) throw new Error("DropdownMenuContent must be used within DropdownMenu");
  if (!ctx.open) return null;
  return (
    <div
      role="menu"
      className={cn(
        "absolute z-50 mt-1 min-w-[12rem] overflow-hidden rounded-md border bg-popover p-1 text-popover-foreground shadow-md",
        align === "end" && "end-0",
        align === "start" && "start-0",
        className
      )}
    >
      {children}
    </div>
  );
}

function useCloseMenu() {
  const ctx = React.useContext(MenuContext);
  return () => ctx?.setOpen(false);
}

export function DropdownMenuItem({ children, className, onSelect, onClick }: { children: React.ReactNode; className?: string; onSelect?: () => void; onClick?: () => void }) {
  const close = useCloseMenu();
  return (
    <button
      type="button"
      role="menuitem"
      onClick={() => {
        onSelect?.();
        onClick?.();
        close();
      }}
      className={cn(
        "flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-sm text-start hover:bg-accent hover:text-accent-foreground focus-visible:outline-none",
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
