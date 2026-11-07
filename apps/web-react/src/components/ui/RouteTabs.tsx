import * as React from "react";
import { NavLink } from "react-router-dom";
import { cn } from "@lib/utils";

export type RouteTabIcon = React.ComponentType<{ className?: string; "aria-hidden"?: boolean | "true" | "false" }>;

export interface RouteTabItem {
  id: string;
  href: string;
  label: string;
  icon?: RouteTabIcon;
  /** Match the current location exactly instead of also matching child routes. */
  end?: boolean;
}

export interface RouteTabsProps {
  tabs: readonly RouteTabItem[];
  /** Accessible name for the tablist. */
  label: string;
  /** Extra classes for the inner tablist element. */
  className?: string;
  /** Extra classes for the full-bleed wrapper element. */
  wrapperClassName?: string;
}

/**
 * Route-backed horizontal tab bar. This is the single source of truth for the
 * Project Details tab design (active indicator, spacing, typography, colors,
 * borders, hover states, RTL order). My Work reuses it so both surfaces stay
 * visually identical instead of growing a second tab design.
 */
export function RouteTabs({ tabs, label, className, wrapperClassName }: RouteTabsProps) {
  return (
    <div className={cn("nexus-fullbleed-tabs sticky top-0 z-20 isolate border-y border-border bg-muted/70 py-1", wrapperClassName)}>
      <div
        role="tablist"
        aria-label={label}
        className={cn("flex h-auto w-full min-w-0 items-center justify-between gap-1 overflow-x-auto overflow-y-hidden rounded-none bg-transparent px-2", className)}
      >
        {tabs.map((tab) => (
          <NavLink
            key={tab.id}
            to={tab.href}
            end={tab.end}
            role="tab"
            className={({ isActive }) =>
              cn(
                "flex h-auto shrink-0 items-center whitespace-nowrap rounded-none border-b-2 border-transparent px-3 py-1.5 text-[13px] font-semibold leading-5 text-muted-foreground transition-colors rtl:flex-row-reverse",
                isActive && "border-primary bg-transparent px-3.5 py-1.5 text-[15px] font-semibold text-primary shadow-none"
              )
            }
          >
            {tab.icon ? <tab.icon className="me-1.5 h-3.5 w-3.5" aria-hidden="true" /> : null}
            {tab.label}
          </NavLink>
        ))}
      </div>
    </div>
  );
}
