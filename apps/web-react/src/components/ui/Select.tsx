import * as React from "react";
import { createPortal } from "react-dom";
import { Check, ChevronDown } from "lucide-react";
import { cn } from "@lib/utils";
import { useAnchoredOverlayPosition } from "./AnchoredOverlay";

interface SelectContextValue {
  value: string;
  onValueChange: (value: string) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
  labels: Map<string, string>;
  triggerRef: React.MutableRefObject<HTMLButtonElement | null>;
  contentRef: React.MutableRefObject<HTMLDivElement | null>;
}

const SelectContext = React.createContext<SelectContextValue | null>(null);

interface SelectRootProps {
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
  children: React.ReactNode;
}

export function Select({ value, defaultValue = "", onValueChange, children }: SelectRootProps) {
  const [uncontrolled, setUncontrolled] = React.useState(defaultValue);
  const [open, setOpen] = React.useState(false);
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const contentRef = React.useRef<HTMLDivElement>(null);
  const current = value ?? uncontrolled;
  const handleChange = React.useCallback(
    (next: string) => {
      if (value === undefined) setUncontrolled(next);
      onValueChange?.(next);
      setOpen(false);
    },
    [value, onValueChange]
  );
  // Declarative label map: walked from children on every render (cheap for
  // small option lists). Works before the menu is ever opened and cannot loop,
  // unlike effect-based registration with ReactNode identity.
  const labels = React.useMemo(() => collectLabels(children), [children]);
  const ctx = React.useMemo(
    () => ({ value: current, onValueChange: handleChange, open, setOpen, labels, triggerRef, contentRef }),
    [current, handleChange, open, labels]
  );

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
    <SelectContext.Provider value={ctx}>
      <div className="relative w-full">{children}</div>
    </SelectContext.Provider>
  );
}

function collectLabels(node: React.ReactNode, map: Map<string, string> = new Map()): Map<string, string> {
  React.Children.forEach(node, (child) => {
    if (!React.isValidElement<{ value?: unknown; children?: React.ReactNode }>(child)) return;
    if (child.type === SelectItem && typeof child.props.value === "string") {
      const text = flattenText(child.props.children);
      if (text) map.set(child.props.value, text);
    } else if (child.props?.children) {
      collectLabels(child.props.children, map);
    }
  });
  return map;
}

function flattenText(node: React.ReactNode): string | null {
  let out = "";
  let ok = true;
  React.Children.forEach(node, (child) => {
    if (typeof child === "string" || typeof child === "number") out += String(child);
    else ok = false;
  });
  return ok && out ? out : null;
}

export function SelectTrigger({ className, children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  const ctx = React.useContext(SelectContext);
  if (!ctx) throw new Error("SelectTrigger must be used within Select");
  return (
    <>
      <button
        ref={ctx.triggerRef}
        type="button"
        aria-haspopup="listbox"
        aria-expanded={ctx.open}
        onClick={() => ctx.setOpen(!ctx.open)}
        className={cn(
          "flex h-10 w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50",
          className
        )}
        {...props}
      >
        <span className="min-w-0 flex-1 truncate text-start">{children}</span>
        <ChevronDown className={cn("h-4 w-4 shrink-0 opacity-50 transition-transform", ctx.open && "rotate-180")} />
      </button>
    </>
  );
}

export function SelectValue({ placeholder, children }: { placeholder?: string; children?: React.ReactNode }) {
  const ctx = React.useContext(SelectContext);
  if (!ctx) throw new Error("SelectValue must be used within Select");
  if (children) return <>{children}</>;
  if (!ctx.value) return <span className="text-muted-foreground">{placeholder}</span>;
  const label = ctx.labels.get(ctx.value);
  return <span className="truncate">{label ?? ctx.value}</span>;
}

export function SelectContent({ className, children }: { className?: string; children: React.ReactNode; position?: "popper" | "inline" }) {
  const ctx = React.useContext(SelectContext);
  if (!ctx) throw new Error("SelectContent must be used within Select");
  const style = useAnchoredOverlayPosition({ open: ctx.open, anchorRef: ctx.triggerRef, contentRef: ctx.contentRef, matchTriggerWidth: true });
  if (!ctx.open) return null;
  return createPortal(
    <div ref={ctx.contentRef} role="listbox" style={style} className={cn("max-h-72 rounded-md border bg-popover p-1 text-popover-foreground shadow-md", className)}>
      {children}
    </div>,
    document.body
  );
}

export function SelectItem({ value, children, disabled }: { value: string; children: React.ReactNode; disabled?: boolean }) {
  const ctx = React.useContext(SelectContext);
  if (!ctx) throw new Error("SelectItem must be used within Select");
  const selected = ctx.value === value;
  return (
    <button
      type="button"
      role="option"
      aria-selected={selected}
      disabled={disabled}
      onClick={() => ctx.onValueChange(value)}
      className={cn(
        "relative flex w-full cursor-pointer select-none items-center rounded-sm py-1.5 pe-2 ps-8 text-sm outline-none transition-colors hover:bg-accent hover:text-accent-foreground disabled:pointer-events-none disabled:opacity-50",
        selected && "bg-accent/60 font-medium"
      )}
    >
      <span className="absolute start-2 flex h-3.5 w-3.5 items-center justify-center">
        {selected ? <Check className="h-4 w-4" /> : null}
      </span>
      <span className="truncate">{children}</span>
    </button>
  );
}
