"use client";

import * as React from "react";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";
import type { NavItem, NavSection } from "@/lib/navigation";
import { useTranslations } from "next-intl";
import { SidebarNavItem } from "./SidebarNavItem";

export function SidebarCollapsedSection({
  section,
  items,
  pathname,
  active,
  onExpand,
}: {
  section: NavSection;
  items: NavItem[];
  pathname: string;
  active: boolean;
  onExpand: () => void;
}) {
  const t = useTranslations("nav");
  const [open, setOpen] = React.useState(false);
  const closeTimerRef = React.useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearCloseTimer = React.useCallback(() => {
    if (closeTimerRef.current === null) return;
    clearTimeout(closeTimerRef.current);
    closeTimerRef.current = null;
  }, []);

  const openOnHover = React.useCallback(() => {
    clearCloseTimer();
    setOpen(true);
  }, [clearCloseTimer]);

  const closeAfterHover = React.useCallback(() => {
    clearCloseTimer();
    closeTimerRef.current = setTimeout(() => {
      setOpen(false);
      closeTimerRef.current = null;
    }, 120);
  }, [clearCloseTimer]);

  React.useEffect(() => () => clearCloseTimer(), [clearCloseTimer]);

  const handleNavigate = () => {
    clearCloseTimer();
    setOpen(false);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <div onMouseEnter={openOnHover} onMouseLeave={closeAfterHover}>
        <PopoverTrigger asChild>
          <button
            type="button"
            onClick={onExpand}
            title={t(section.labelKey)}
            aria-label={t(section.labelKey)}
            aria-expanded={open}
            className={cn(
              "group relative flex w-full items-center justify-center rounded-md px-2 py-2 transition-all duration-150",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
              active
                ? "bg-sidebar-active/10 text-sidebar-active"
                : "text-sidebar-foreground/55 hover:bg-sidebar-hover hover:text-sidebar-foreground",
            )}
          >
            {active && <span className="absolute inset-y-1 start-0 w-[3px] rounded-full bg-sidebar-active" />}
            <section.icon className={cn("h-4 w-4 shrink-0", active ? "text-sidebar-active" : "text-sidebar-foreground/45")} />
          </button>
        </PopoverTrigger>
        <PopoverContent side="right" align="start" sideOffset={8} onMouseEnter={openOnHover} onMouseLeave={closeAfterHover} className="w-52 border-border bg-popover text-popover-foreground shadow-lg">
          <p className="px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t(section.labelKey)}</p>
          <div className="space-y-px">
            {items.map((item) => <SidebarNavItem key={item.id} item={item} pathname={pathname} onNavigate={handleNavigate} />)}
          </div>
        </PopoverContent>
      </div>
    </Popover>
  );
}
