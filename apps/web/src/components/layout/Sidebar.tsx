"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useTranslations } from "next-intl";
import {
  ChevronRight,
  PanelLeftClose,
  PanelLeft,
  Search,
  X,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { navigationConfig, type NavItem, type NavSection } from "@/lib/navigation";
import { useAuth } from "@/lib/auth";
import { APP_NAME } from "@/lib/constants";

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

const GROUP_STORAGE_KEY = "nexus_sidebar_groups";

function normalizePathname(pathname: string): string {
  const segments = pathname.split("/").filter(Boolean);
  if (segments.length === 0) return "/";
  return "/" + segments.slice(1).join("/");
}

function itemIsActive(item: NavItem, pathname: string): boolean {
  if (pathname === item.href) return true;
  if (item.href !== "/" && pathname.startsWith(item.href + "/")) return true;
  return false;
}

function findActiveItem(items: NavItem[], pathname: string): NavItem | null {
  let best: NavItem | null = null;
  for (const item of items) {
    if (item.items?.length) {
      const nested = findActiveItem(item.items, pathname);
      if (nested && (!best || nested.href.length > best.href.length)) best = nested;
    }
    if (itemIsActive(item, pathname) && (!best || item.href.length > best.href.length)) {
      best = item;
    }
  }
  return best;
}

function filterItems(items: NavItem[], permissions: string[]): NavItem[] {
  return items
    .filter((item) => {
      if (item.requiredPermission && !permissions.includes(item.requiredPermission)) return false;
      return true;
    })
    .map((item) =>
      item.items?.length
        ? { ...item, items: filterItems(item.items, permissions) }
        : item
    );
}

function filterSections(
  sections: NavSection[],
  permissions: string[]
): { section: NavSection; items: NavItem[] }[] {
  const visible: { section: NavSection; items: NavItem[] }[] = [];
  for (const section of sections) {
    if (section.requiredPermission && !permissions.includes(section.requiredPermission)) continue;
    const items = filterItems(section.items, permissions);
    if (items.length === 0) continue;
    visible.push({ section, items });
  }
  return visible;
}

function readGroupState(): Record<string, boolean> {
  if (typeof window === "undefined") return {};
  try {
    const raw = window.localStorage.getItem(GROUP_STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Record<string, boolean>) : {};
  } catch {
    return {};
  }
}

function NavItemLink({
  item,
  pathname,
  onNavigate,
  inverseTheme = false,
}: {
  item: NavItem;
  pathname: string;
  onNavigate?: () => void;
  inverseTheme?: boolean;
}) {
  const t = useTranslations("nav");
  const active = itemIsActive(item, pathname);
  const Icon = item.icon;

  if (item.items && item.items.length > 0) {
    return (
      <div className="space-y-0.5">
        <NavItemLink item={{ ...item, items: undefined, href: item.href }} pathname={pathname} onNavigate={onNavigate} inverseTheme={inverseTheme} />
        {item.items.map((subItem) => (
          <NavItemLink key={subItem.id} item={subItem} pathname={pathname} onNavigate={onNavigate} inverseTheme={inverseTheme} />
        ))}
      </div>
    );
  }

  return (
    <Link
      href={item.href}
      onClick={onNavigate}
      className={cn(
        "group relative flex items-center gap-2.5 rounded-md px-2.5 py-1.5 text-xs font-medium transition-all duration-200",
        active
          ? "bg-sidebar-active/10 text-sidebar-active"
          : inverseTheme
            ? "text-background/80 hover:bg-background/10 hover:text-background"
            : "text-sidebar-foreground/70 hover:bg-sidebar-accent/60 hover:text-sidebar-foreground"
      )}
    >
      {active && (
        <span className="absolute inset-y-1 start-0 w-0.5 rounded-full bg-sidebar-active" />
      )}
      <Icon className={cn("h-3.5 w-3.5 shrink-0", active ? "text-sidebar-active" : inverseTheme ? "text-background/70" : "text-sidebar-foreground/60")} />
      <span>{t(item.labelKey)}</span>
    </Link>
  );
}

function CollapsedNavSection({
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

  React.useEffect(() => () => {
    clearCloseTimer();
  }, [clearCloseTimer]);

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
              "group relative flex w-full items-center justify-center rounded-lg px-2 py-2 transition-all duration-200",
              active
                ? "bg-sidebar-active/10 text-sidebar-active"
                : "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground"
            )}
          >
            {active && (
              <span className="absolute inset-y-1 start-0 w-0.5 rounded-full bg-sidebar-active" />
            )}
            <section.icon className={cn("h-5 w-5 shrink-0", active ? "text-sidebar-active" : "text-sidebar-foreground/70")} />
          </button>
        </PopoverTrigger>
        <PopoverContent
          side="right"
          align="start"
          sideOffset={8}
          onMouseEnter={openOnHover}
          onMouseLeave={closeAfterHover}
          className="w-56 border-background/20 bg-foreground p-2 text-background shadow-xl"
        >
          <p className="px-2 py-1 text-xs font-semibold text-background">{t(section.labelKey)}</p>
          <div className="space-y-0.5">
            {items.map((item) => (
              <NavItemLink key={item.id} item={item} pathname={pathname} onNavigate={handleNavigate} inverseTheme />
            ))}
          </div>
        </PopoverContent>
      </div>
    </Popover>
  );
}

function NavSectionView({
  section,
  items,
  pathname,
  active,
  open,
  onOpenChange,
  onNavigate,
}: {
  section: NavSection;
  items: NavItem[];
  pathname: string;
  active: boolean;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  onNavigate?: () => void;
}) {
  const t = useTranslations("nav");
  const Icon = section.icon;

  return (
    <Collapsible open={open} onOpenChange={onOpenChange}>
      <CollapsibleTrigger asChild>
        <button
          className={cn(
            "group relative flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-xs font-medium transition-all duration-200",
            active
              ? "bg-sidebar-active/10 text-sidebar-active"
              : "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground"
          )}
        >
          {active && (
            <span className="absolute inset-y-1 start-0 w-0.5 rounded-full bg-sidebar-active" />
          )}
          <Icon className={cn("h-4 w-4 shrink-0", active ? "text-sidebar-active" : "text-sidebar-foreground/70")} />
          <span className="flex-1 text-start">{t(section.labelKey)}</span>
          <ChevronRight
            className={cn(
              "h-3.5 w-3.5 shrink-0 text-sidebar-foreground/50 transition-transform duration-200",
              "ltr:rotate-0 rtl:rotate-180",
              open && "ltr:rotate-90 rtl:-rotate-90"
            )}
          />
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent className="overflow-hidden data-[state=closed]:animate-collapsible-up data-[state=open]:animate-collapsible-down">
        <div className="ms-2 mt-0.5 space-y-0.5 border-s border-sidebar-border ps-3">
          {items.map((item) => (
            <NavItemLink key={item.id} item={item} pathname={pathname} onNavigate={onNavigate} />
          ))}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

export function Sidebar({
  collapsed,
  onToggle,
}: SidebarProps) {
  const pathname = usePathname();
  const t = useTranslations("nav");
  const { user } = useAuth();
  const permissions = React.useMemo(() => user?.permissions ?? [], [user]);

  const normalizedPathname = React.useMemo(() => normalizePathname(pathname), [pathname]);
  const visibleSections = React.useMemo(
    () => filterSections(navigationConfig, permissions),
    [permissions]
  );

  const activeItem = React.useMemo(
    () => {
      for (const { items } of visibleSections) {
        const found = findActiveItem(items, normalizedPathname);
        if (found) return found;
      }
      return null;
    },
    [visibleSections, normalizedPathname]
  );

  const [groupState, setGroupState] = React.useState<Record<string, boolean>>({});
  const [groupStateLoaded, setGroupStateLoaded] = React.useState(false);

  React.useEffect(() => {
    setGroupState(readGroupState());
    setGroupStateLoaded(true);
  }, []);

  const [searchQuery, setSearchQuery] = React.useState("");
  const trimmedQuery = searchQuery.trim();
  const searchActive = trimmedQuery.length > 0;

  React.useEffect(() => {
    if (collapsed) setSearchQuery("");
  }, [collapsed]);

  const searchResults = React.useMemo(() => {
    if (!searchActive) return null;
    const q = trimmedQuery.toLowerCase();
    const results: { section: NavSection; items: NavItem[] }[] = [];
    for (const { section, items } of visibleSections) {
      const matched = items.filter((item) =>
        t(item.labelKey).toLowerCase().includes(q)
      );
      if (matched.length > 0) results.push({ section, items: matched });
    }
    return results;
  }, [searchActive, trimmedQuery, visibleSections, t]);

  function clearSearch() {
    setSearchQuery("");
  }

  const activeSectionId = activeItem
    ? visibleSections.find(({ items }) =>
        findActiveItem(items, normalizedPathname) === activeItem
      )?.section.id
    : undefined;

  function updateGroup(sectionId: string, open: boolean) {
    setGroupState((prev) => {
      const next = { ...prev, [sectionId]: open };
      try {
        window.localStorage.setItem(GROUP_STORAGE_KEY, JSON.stringify(next));
      } catch {
        // Storage is best-effort.
      }
      return next;
    });
  }

  function handleCollapsedSectionClick(sectionId: string) {
    updateGroup(sectionId, true);
    onToggle();
  }

  return (
    <aside
      className={cn(
        "flex flex-col border-e border-sidebar-border bg-sidebar bg-navy-gradient dark:bg-navy-gradient text-sidebar-foreground transition-all duration-300 ease-out-expo",
        collapsed ? "w-16" : "w-64"
      )}
    >
      <div className={cn(
        "relative flex h-14 items-center border-b border-sidebar-border px-4",
        collapsed && "justify-center px-2"
      )}>
        {!collapsed ? (
          <div className="flex items-center gap-3">
            <div className="relative flex h-8 w-8 items-center justify-center rounded-xl bg-primary-gradient text-[11px] font-bold text-white shadow-primary-sm">
              N
            </div>
            <div className="flex flex-col overflow-hidden">
              <span className="text-xs font-bold leading-tight tracking-tight text-sidebar-foreground">{APP_NAME}</span>
              <span className="text-[9px] leading-tight text-sidebar-foreground/50 font-medium uppercase tracking-[0.1em]">Portfolio Platform</span>
            </div>
          </div>
        ) : (
          <div className="relative flex h-8 w-8 items-center justify-center rounded-xl bg-primary-gradient text-[11px] font-bold text-white shadow-primary-sm">
            N
          </div>
        )}
      </div>

      {/* Sidebar Search Input — hidden when collapsed */}
      {!collapsed && (
        <div className="px-2 py-1.5">
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-sidebar-foreground/40 pointer-events-none" />
            <input
              type="text"
              placeholder="Search navigation..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="flex h-8 w-full rounded-md border border-sidebar-border bg-sidebar-accent/30 px-2.5 pl-8 pr-8 text-xs text-sidebar-foreground placeholder:text-sidebar-foreground/40 focus:border-sidebar-active/50 focus:outline-none focus:ring-1 focus:ring-sidebar-active/30 transition-all duration-200"
            />
            {searchActive && (
              <button
                onClick={clearSearch}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-sidebar-foreground/40 hover:text-sidebar-foreground transition-colors"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            )}
          </div>
        </div>
      )}

      <ScrollArea className="flex-1 px-2 py-2 scrollbar-thin">
        <nav className="space-y-1">
          {searchActive && searchResults
            ? searchResults.length > 0
              ? searchResults.map(({ section, items }) => (
                  <NavSectionView
                    key={section.id}
                    section={section}
                    items={items}
                    pathname={normalizedPathname}
                    active={activeSectionId === section.id}
                    open={true}
                    onOpenChange={() => {}}
                    onNavigate={clearSearch}
                  />
                ))
              : !collapsed && (
                  <div className="px-2.5 py-6 text-center text-xs text-sidebar-foreground/50">
                    No matching items found
                  </div>
                )
            : visibleSections.map(({ section, items }) => {
                const sectionActive = activeSectionId === section.id;
                const open = groupStateLoaded
                  ? groupState[section.id] ?? sectionActive
                  : sectionActive;

                if (collapsed) {
                  return (
                    <CollapsedNavSection
                      key={section.id}
                      section={section}
                      items={items}
                      pathname={normalizedPathname}
                      active={sectionActive}
                      onExpand={() => handleCollapsedSectionClick(section.id)}
                    />
                  );
                }

                return (
                  <NavSectionView
                    key={section.id}
                    section={section}
                    items={items}
                    pathname={normalizedPathname}
                    active={sectionActive}
                    open={open}
                    onOpenChange={(next) => updateGroup(section.id, next)}
                  />
                );
              })}
        </nav>
      </ScrollArea>

      <div className="relative border-t border-sidebar-border p-2">
        <button
          onClick={onToggle}
          className={cn(
            "flex w-full items-center gap-2 rounded-lg px-3 py-2 text-xs font-medium text-sidebar-foreground/50 transition-all duration-200 hover:bg-sidebar-accent hover:text-sidebar-foreground hover:shadow-sm",
            collapsed && "justify-center px-2"
          )}
        >
          {collapsed ? (
            <PanelLeft className="h-4 w-4" />
          ) : (
            <>
              <PanelLeftClose className="h-3.5 w-3.5" />
              <span>Minimize menu</span>
            </>
          )}
        </button>
      </div>
    </aside>
  );
}
