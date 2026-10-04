import * as React from "react";
import { Check, ChevronDown } from "lucide-react";
import { cn } from "@lib/utils";

interface SelectContextValue {
  value: string;
  onValueChange: (value: string) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
  labels: Map<string, React.ReactNode>;
  registerLabel: (value: string, label: React.ReactNode) => void;
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
  const [labels, setLabels] = React.useState(() => new Map<string, React.ReactNode>());
  const current = value ?? uncontrolled;
  const handleChange = React.useCallback(
    (next: string) => {
      if (value === undefined) setUncontrolled(next);
      onValueChange?.(next);
      setOpen(false);
    },
    [value, onValueChange]
  );
  const registerLabel = React.useCallback((itemValue: string, label: React.ReactNode) => {
    setLabels((prev) => {
      if (prev.get(itemValue) === label) return prev;
      const next = new Map(prev);
      next.set(itemValue, label);
      return next;
    });
  }, []);
  const ctx = React.useMemo(
    () => ({ value: current, onValueChange: handleChange, open, setOpen, labels, registerLabel }),
    [current, handleChange, open, labels, registerLabel]
  );

  React.useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open ]);

  return (
    <SelectContext.Provider value={ctx}>
      <div className="relative w-full">{children}</div>
    </SelectContext.Provider>
  );
}

export function SelectTrigger({ className, children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  const ctx = React.useContext(SelectContext);
  if (!ctx) throw new Error("SelectTrigger must be used within Select");
  return (
    <>
      <button
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
      {ctx.open ? (
        <button type="button" aria-label="Close" tabIndex={-1} onClick={() => ctx.setOpen(false)} className="fixed inset-0 z-40 cursor-default bg-transparent" />
      ) : null}
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
  if (!ctx.open) return null;
  return (
    <div role="listbox" className={cn("absolute z-50 mt-1 max-h-72 w-full overflow-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md", className)}>
      {children}
    </div>
  );
}

export function SelectItem({ value, children, disabled }: { value: string; children: React.ReactNode; disabled?: boolean }) {
  const ctx = React.useContext(SelectContext);
  if (!ctx) throw new Error("SelectItem must be used within Select");
  React.useEffect(() => {
    ctx.registerLabel(value, children);
  }, [value, children, ctx]);
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
