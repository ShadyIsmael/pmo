import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export type PlanningRibbonTab = "task" | "project" | "view" | "resource" | "format";

export function PlanningRibbonGroup({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <div role="group" aria-label={label} className={cn("flex min-h-[3.35rem] shrink-0 flex-col justify-between border-e border-border pe-2 last:border-e-0", className)}>
      <div className="flex min-h-7 flex-wrap items-center gap-1">{children}</div>
      <span className="mt-1 px-1 text-center text-[10px] font-medium leading-3 text-muted-foreground">{label}</span>
    </div>
  );
}

export function PlanningRibbonTabs({ ariaLabel, activeTab, tabs, onChange }: { ariaLabel: string; activeTab: PlanningRibbonTab; tabs: Array<{ id: PlanningRibbonTab; label: string }>; onChange: (_tab: PlanningRibbonTab) => void }) {
  return (
    <div role="tablist" aria-label={ariaLabel} className="flex min-w-max items-end gap-0.5 overflow-x-auto">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={activeTab === tab.id}
          onClick={() => onChange(tab.id)}
          className={cn(
            "rounded-t-md border-b-2 border-transparent px-3 py-1.5 text-xs font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground",
            activeTab === tab.id && "border-primary bg-background text-foreground shadow-sm",
          )}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
