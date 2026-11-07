import * as React from "react";
import { cn } from "@lib/utils";
import { Button } from "@components/ui/Button";

export type PlanningRibbonTab = "file" | "task" | "resource" | "project" | "view" | "report";

export function PlanningRibbonGroup({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <div role="group" aria-label={label} className={cn("flex shrink-0 flex-col gap-0.5 border-e border-border pe-1.5 last:border-e-0", className)}>
      <div className="flex min-h-7 flex-wrap items-center gap-0.5">{children}</div>
      <span className="px-0.5 text-center text-[10px] font-medium leading-3 text-muted-foreground">{label}</span>
    </div>
  );
}

export function PlanningRibbonTabs({ ariaLabel, activeTab, tabs, onChange }: { ariaLabel: string; activeTab: PlanningRibbonTab; tabs: Array<{ id: PlanningRibbonTab; label: string }>; onChange: (tab: PlanningRibbonTab) => void }) {
  const tabRefs = React.useRef<Array<HTMLButtonElement | null>>([]);
  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    const directionRoot = event.currentTarget.closest("[dir]");
    const rtl = directionRoot?.getAttribute("dir") === "rtl" || getComputedStyle(event.currentTarget).direction === "rtl";
    const previous = rtl ? "ArrowRight" : "ArrowLeft";
    const next = rtl ? "ArrowLeft" : "ArrowRight";
    let targetIndex: number | undefined;
    if (event.key === previous) targetIndex = (index - 1 + tabs.length) % tabs.length;
    else if (event.key === next) targetIndex = (index + 1) % tabs.length;
    else if (event.key === "Home") targetIndex = 0;
    else if (event.key === "End") targetIndex = tabs.length - 1;
    if (targetIndex === undefined) return;
    event.preventDefault();
    const target = tabs[targetIndex];
    if (!target) return;
    onChange(target.id);
    tabRefs.current[targetIndex]?.focus();
  };
  return (
    <div role="tablist" aria-label={ariaLabel} className="flex min-w-max items-end gap-0.5 overflow-x-auto">
      {tabs.map((tab, index) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={activeTab === tab.id}
          tabIndex={activeTab === tab.id ? 0 : -1}
          ref={(element) => { tabRefs.current[index] = element; }}
          onClick={() => onChange(tab.id)}
          onKeyDown={(event) => handleKeyDown(event, index)}
          className={cn(
            "rounded-t-md border-b-2 border-transparent px-3 py-1.5 text-xs font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground",
            activeTab === tab.id && "border-primary bg-background text-foreground shadow-sm"
          )}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}

export function WorkspaceButton({ onClick, disabled, icon, children }: { onClick: () => void; disabled?: boolean; icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className="inline-flex h-7 shrink-0 items-center justify-center gap-1 whitespace-nowrap rounded-md px-2 text-[11px] font-medium ring-offset-background transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50"
    >
      {icon}
      {children}
    </button>
  );
}

export function PlanningIconButton({ label, icon, onClick, disabled, variant = "ghost", pressed, className }: { label: string; icon: React.ReactNode; onClick: () => void; disabled?: boolean; variant?: "ghost" | "secondary"; pressed?: boolean; className?: string }) {
  return (
    <span title={label} className="inline-flex shrink-0">
      <Button
        type="button"
        size="icon-sm"
        variant={variant}
        onClick={onClick}
        disabled={disabled}
        aria-label={label}
        aria-pressed={pressed}
        className={cn("shrink-0", className)}
      >
        {icon}
      </Button>
    </span>
  );
}
