"use client";

import * as React from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { ChevronRight, FolderKanban, Menu, PanelLeftClose, PanelLeft, Search, X } from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { cn, getInitials } from "@/lib/utils";
import { navigationConfig, type NavItem, type NavSection } from "@/lib/navigation";
import { filterSections, findActiveItem, normalizePathname, itemIsActive } from "./sidebar-utils";
import { useAuth } from "@/lib/auth";
import { APP_NAME } from "@/lib/constants";
import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";
import { entityRoute, loadRecent, rememberRecent, type RecentEntity } from "@/lib/recent-opened";
const LazySidebarCollapsedSection = dynamic(() => import("./SidebarCollapsedSection").then((module) => module.SidebarCollapsedSection), { ssr: false, loading: () => null });
const LazyGlobalSearchPalette = dynamic(() => import("./GlobalSearchPalette").then((module) => module.GlobalSearchPalette), { ssr: false, loading: () => null });
const GROUP_STORAGE_KEY = "nexus_sidebar_groups";

function readGroupState(): Record<string, boolean> {
  if (typeof window === "undefined") return {};
  try { return JSON.parse(window.localStorage.getItem(GROUP_STORAGE_KEY) || "{}") as Record<string, boolean>; } catch { return {}; }
}

/* ── Section header (grouping label — NOT a nav item) ── */
function NavSectionView({ section, items, pathname, active, open, onOpenChange, onNavigate }: { section: NavSection; items: NavItem[]; pathname: string; active: boolean; open: boolean; onOpenChange: (open: boolean) => void; onNavigate?: () => void }) {
  const Icon = section.icon;
  const t = useTranslations("nav");
  const hasActiveChild = items.some((item) => itemIsActive(item, pathname));
  return <Collapsible open={open} onOpenChange={onOpenChange}>
    <CollapsibleTrigger asChild>
      <button type="button" aria-expanded={open} className={cn(
        "group relative flex h-8 w-full items-center gap-2 rounded-lg px-2.5 text-[11px] font-bold uppercase tracking-[0.1em] transition-colors rtl:flex-row-reverse",
        (active || hasActiveChild) ? "text-sidebar-active" : "text-sidebar-muted"
      )}>
        <Icon className={cn("h-3.5 w-3.5 shrink-0", (active || hasActiveChild) ? "text-sidebar-active" : "text-sidebar-muted")} />
        <span className="flex-1 text-start rtl:text-right">{t(section.labelKey)}</span>
        <ChevronRight className={cn("h-3 w-3 transition-transform duration-200 text-sidebar-muted/60 ltr:rotate-0 rtl:rotate-180", open && "ltr:rotate-90 rtl:-rotate-90")} />
      </button>
    </CollapsibleTrigger>
    <CollapsibleContent className="overflow-hidden">
      <div className="space-y-1 ps-1.5">
        {items.map((item) => <SidebarNavLink key={item.id} item={item} pathname={pathname} onNavigate={onNavigate} />)}
      </div>
    </CollapsibleContent>
  </Collapsible>;
}

/* ── Submenu item (visually smaller, lower contrast) ── */
function SidebarNavLink({ item, pathname, onNavigate }: { item: NavItem; pathname: string; onNavigate?: () => void }) {
  const active = itemIsActive(item, pathname);
  const Icon = item.icon;
  const t = useTranslations("nav");
  return <Link href={item.href} onClick={onNavigate} className={cn(
    "group relative flex h-8 items-center gap-3 rounded-lg px-2.5 text-[11px] font-normal transition-all duration-150 rtl:flex-row-reverse",
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
    active
      ? "border border-sidebar-active/20 bg-sidebar-active/10 text-sidebar-foreground shadow-[inset_0_0_18px_hsl(var(--sidebar-active)/0.05)]"
      : "border border-transparent text-sidebar-foreground/58 hover:bg-sidebar-hover hover:text-sidebar-foreground/90"
  )}>
    {active && <span className="absolute inset-y-1 start-0 w-[3px] rounded-full bg-sidebar-active rtl:end-0 rtl:start-auto" />}
    <Icon className={cn("h-4 w-4 shrink-0", active ? "text-sidebar-active" : "text-sidebar-foreground/42 group-hover:text-sidebar-foreground/75")} />
    <span className="truncate">{t(item.labelKey)}</span>
  </Link>;
}

export function Sidebar() {
  const pathname = usePathname();
  const locale = useLocale();
  const { user } = useAuth();
  const t = useTranslations("nav");
  const normalizedPathname = React.useMemo(() => normalizePathname(pathname), [pathname]);
  const permissions = React.useMemo(() => user?.permissions ?? [], [user?.permissions]);
  const visibleSections = React.useMemo(() => filterSections(navigationConfig, permissions), [permissions]);
  const activeItem = React.useMemo(() => visibleSections.reduce<NavItem | null>((found, entry) => found || findActiveItem(entry.items, normalizedPathname), null), [visibleSections, normalizedPathname]);
  const activeSectionId = visibleSections.find(({ items }) => findActiveItem(items, normalizedPathname) === activeItem)?.section.id;
  const [collapsed, setCollapsed] = React.useState(false);
  const [mobileOpen, setMobileOpen] = React.useState(false);
  const [searchOpen, setSearchOpen] = React.useState(false);
  const [searchLoaded, setSearchLoaded] = React.useState(false);
  const [groupState, setGroupState] = React.useState<Record<string, boolean>>({});
  const [recent, setRecent] = React.useState<RecentEntity[]>([]);

  React.useEffect(() => { setGroupState(readGroupState()); }, []);
  React.useEffect(() => { setRecent(loadRecent(user?.tenantId)); }, [user?.tenantId]);
  React.useEffect(() => { setMobileOpen(false); }, [pathname]);
  React.useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const key = typeof event.key === "string" ? event.key.toLowerCase() : null;
      if (key === "k" && (event.ctrlKey || event.metaKey)) { event.preventDefault(); setSearchLoaded(true); setSearchOpen((value) => !value); }
    };
    document.addEventListener("keydown", onKeyDown); return () => document.removeEventListener("keydown", onKeyDown);
  }, []);
  React.useEffect(() => {
    const openSearch = () => { setSearchLoaded(true); setSearchOpen(true); };
    window.addEventListener("nexus:open-global-search", openSearch);
    return () => window.removeEventListener("nexus:open-global-search", openSearch);
  }, []);

  React.useEffect(() => {
    const route = entityRoute(normalizedPathname);
    const tenantId = user?.tenantId;
    if (!route || !tenantId) return;
    const currentRoute = route;
    let cancelled = false;
    async function remember() {
      try {
        const data = currentRoute.type === "project" ? await api.get<{ id: string; name: string; code?: string }>(endpoints.projects.detail(currentRoute.id))
          : currentRoute.type === "program" ? await api.get<{ id: string; name: string; code?: string }>(endpoints.programs.detail(currentRoute.id))
            : currentRoute.type === "portfolio" ? await api.get<{ id: string; name: string; code?: string }>(endpoints.portfolios.detail(currentRoute.id))
              : currentRoute.type === "resource" ? await api.get<{ id: string; name: string; employeeId?: string; jobTitle?: string }>(endpoints.resources.detail(currentRoute.id))
                : await api.get<{ id: string; title: string; projectName?: string }>(endpoints.meetings.detail(currentRoute.id));
        if (cancelled) return;
        const name = "title" in data ? data.title : data.name;
        const next = rememberRecent({ id: data.id, type: currentRoute.type, name, href: currentRoute.href, code: "code" in data ? data.code : "employeeId" in data ? data.employeeId : undefined, context: "projectName" in data ? data.projectName : "jobTitle" in data ? data.jobTitle : undefined }, tenantId);
        setRecent(next);
      } catch { /* inaccessible entities are omitted from recent history */ }
    }
    void remember(); return () => { cancelled = true; };
  }, [normalizedPathname, user?.tenantId]);

  function updateGroup(id: string, open: boolean) { setGroupState((previous) => { const next = { ...previous, [id]: open }; try { window.localStorage.setItem(GROUP_STORAGE_KEY, JSON.stringify(next)); } catch { /* best effort */ } return next; }); }
  const navigate = () => setMobileOpen(false);
  const openSearch = () => { setSearchLoaded(true); setSearchOpen(true); };

  return <>
    {/* Mobile hamburger */}
    <button type="button" aria-label={t("openNavigation")} onClick={() => setMobileOpen(true)} className="fixed start-3 top-3 z-40 inline-flex h-10 w-10 items-center justify-center rounded-lg border border-border bg-background/90 text-foreground shadow-sm backdrop-blur md:hidden">
      <Menu className="h-5 w-5" />
    </button>
    {/* Mobile overlay */}
    {mobileOpen && <button type="button" aria-label={t("closeNavigation")} onClick={() => setMobileOpen(false)} className="fixed inset-0 z-40 bg-black/50 backdrop-blur-[1px] md:hidden" />}

    {/* Sidebar */}
    <aside dir={locale === "ar" ? "rtl" : "ltr"} className={cn(
      "nexus-sidebar fixed inset-y-0 start-0 z-50 flex h-screen min-h-0 flex-col border-e border-sidebar-border bg-sidebar text-sidebar-foreground shadow-sidebar transition-[width,transform] duration-200 md:static md:z-auto md:shadow-none",
      collapsed ? "md:w-[64px]" : "md:w-[260px]",
      mobileOpen ? "w-[min(88vw,288px)] translate-x-0" : "w-[min(88vw,288px)] ltr:-translate-x-full rtl:translate-x-full md:!translate-x-0"
    )}>
      {/* Brand header */}
      <div className={cn("relative flex h-[56px] shrink-0 items-center border-b border-sidebar-border/60 px-3", collapsed && "md:justify-center md:px-2")}>
        <div className={cn("flex items-center gap-2.5", collapsed && "md:hidden")}>
          <div className="relative flex h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-primary-gradient shadow-primary-sm">
            <img src="/nexus-side-logo.svg" alt="Nexus PMO logo" className="h-8 w-8 object-cover" />
          </div>
          <div>
            <div className="text-sm font-bold tracking-tight text-sidebar-foreground">{APP_NAME}</div>
            <div className="text-[9px] font-medium uppercase tracking-[0.16em] text-sidebar-foreground/40">Enterprise PMO</div>
          </div>
        </div>
        <div className={cn("hidden h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-primary-gradient shadow-primary-sm", collapsed && "md:flex")}>
          <img src="/nexus-side-logo.svg" alt="Nexus PMO logo" className="h-8 w-8 object-cover" />
        </div>
        <button type="button" aria-label={collapsed ? t("expandNavigation") : t("collapseNavigation")} onClick={() => setCollapsed((value) => !value)} className="ms-auto hidden h-7 w-7 items-center justify-center rounded-md text-sidebar-foreground/40 transition-colors hover:bg-sidebar-hover hover:text-sidebar-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring md:flex">
          {collapsed ? <PanelLeft className="h-3.5 w-3.5" /> : <PanelLeftClose className="h-3.5 w-3.5" />}
        </button>
        <button type="button" aria-label={t("closeNavigation")} onClick={() => setMobileOpen(false)} className="ms-auto inline-flex h-7 w-7 items-center justify-center rounded-md text-sidebar-foreground/40 hover:bg-sidebar-hover md:hidden">
          <X className="h-3.5 w-3.5" />
        </button>
      </div>

      {/* Search trigger */}
      <div className={cn("shrink-0 p-2.5", collapsed && "md:p-1.5")}>
        <Tooltip>
          <TooltipTrigger asChild>
            <button type="button" onClick={openSearch} aria-label={t("searchNexus")} className={cn(
              "group flex h-8 w-full items-center gap-2 rounded-md border border-sidebar-border/50 bg-sidebar-accent/30 px-2 text-start text-[10px] text-sidebar-foreground/50 transition-colors",
              "hover:border-sidebar-active/40 hover:bg-sidebar-accent hover:text-sidebar-foreground",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
              collapsed && "md:justify-center md:px-0"
            )}>
              <Search className="h-3.5 w-3.5 shrink-0 text-sidebar-foreground/45 group-hover:text-sidebar-active" />
              {!collapsed && <>
                <span className="flex-1 truncate">{t("searchPlaceholder")}</span>
                <kbd className="hidden rounded border border-sidebar-border/50 bg-sidebar/40 px-1.5 py-0.5 text-[9px] text-sidebar-foreground/40 md:inline">Ctrl K</kbd>
              </>}
            </button>
          </TooltipTrigger>
          <TooltipContent side="right">{t("searchTooltip")}</TooltipContent>
        </Tooltip>
      </div>

      {/* Recent Opened */}
      <div className={cn("shrink-0 px-2 pb-0.5", collapsed && "md:px-1.5")}>
        {recent.length > 0 && <>
          <div className={cn("px-2 pb-0.5 pt-1.5 text-[9px] font-semibold uppercase tracking-[0.16em] text-sidebar-muted/60", collapsed && "md:hidden")}>
            {t("recentOpened")}
          </div>
          <div className={cn("space-y-px", collapsed && "md:flex md:flex-col md:items-center md:gap-0.5")}>
            {recent.map((item) => {
              const Icon = navigationConfig.flatMap((section) => section.items).find((nav) => nav.id === `${item.type}s` || nav.id === item.type)?.icon || FolderKanban;
              return <Tooltip key={`${item.type}-${item.id}`}>
                <TooltipTrigger asChild>
                  <Link href={item.href} onClick={navigate} title={item.name} className={cn(
                    "group flex items-center gap-2 rounded-md px-2 py-1.5 text-start text-[11px] text-sidebar-foreground/50 transition-colors",
                    "hover:bg-sidebar-hover hover:text-sidebar-foreground/80",
                    collapsed && "h-8 w-8 justify-center px-0"
                  )}>
                    <Icon className="h-3 w-3 shrink-0 text-sidebar-foreground/35 group-hover:text-sidebar-active" />
                    <span className={cn("min-w-0 truncate", collapsed && "md:hidden")}>
                      {item.name}
                      <span className="ms-1 text-[9px] text-sidebar-foreground/30">{item.code || item.type}</span>
                    </span>
                  </Link>
                </TooltipTrigger>
                <TooltipContent side="right">{item.name} · {item.type}</TooltipContent>
              </Tooltip>;
            })}
          </div>
        </>}
      </div>

      {/* Main navigation */}
      <ScrollArea className="min-h-0 flex-1 px-2 pb-2 scrollbar-thin">
        <nav aria-label={t("globalNavigation")} className={cn("space-y-0.5", collapsed && "md:px-0")}>
          {visibleSections.map(({ section, items }) => {
            const active = activeSectionId === section.id;
            const open = groupState[section.id] ?? active;
            if (collapsed) return <LazySidebarCollapsedSection key={section.id} section={section} items={items} pathname={normalizedPathname} active={active} onExpand={() => { updateGroup(section.id, true); setCollapsed(false); }} />;
            return <NavSectionView key={section.id} section={section} items={items} pathname={normalizedPathname} active={active} open={open} onOpenChange={(value) => updateGroup(section.id, value)} onNavigate={navigate} />;
          })}
        </nav>
      </ScrollArea>

      {/* User profile */}
      <div className={cn("shrink-0 border-t border-sidebar-border/60 p-2.5", collapsed && "md:p-1.5")}>
        <div className={cn("flex items-center gap-2 rounded-md bg-sidebar-accent/20 p-2", collapsed && "md:justify-center md:bg-transparent md:p-0")}>
          <Avatar className="h-7 w-7 shrink-0">
            <AvatarFallback className="bg-primary/15 text-[10px] font-semibold text-primary">{getInitials(user?.fullName || "User")}</AvatarFallback>
          </Avatar>
          {!collapsed && <div className="min-w-0">
            <div className="truncate text-[11px] font-medium text-sidebar-foreground">{user?.fullName || "User"}</div>
            <div className="truncate text-[9px] text-sidebar-muted/70">{user?.roles?.[0]?.name || user?.jobTitle || "NEXUS user"}</div>
          </div>}
        </div>
      </div>
    </aside>

    {/* Global Search Palette */}
    {searchLoaded && <LazyGlobalSearchPalette open={searchOpen} onOpenChange={setSearchOpen} recent={recent} onRecentChange={setRecent} />}
  </>;
}
