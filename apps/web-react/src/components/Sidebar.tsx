import * as React from "react";
import { NavLink, useLocation } from "react-router-dom";
import { ChevronRight, Search, X, PanelLeft, PanelLeftClose, Menu } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { navigationConfig, filterSections, normalizePathname, findActiveItem, itemIsActive, type NavItem, type NavSection } from "@lib/navigation";
import { useAuth } from "@auth/AuthProvider";

const GROUP_STORAGE_KEY = "nexus_sidebar_groups";

function readGroupState(): Record<string, boolean> {
  if (typeof window === "undefined") return {};
  try {
    return JSON.parse(window.localStorage.getItem(GROUP_STORAGE_KEY) || "{}") as Record<string, boolean>;
  } catch {
    return {};
  }
}

interface NavSectionViewProps {
  section: NavSection;
  items: NavItem[];
  pathname: string;
  active: boolean;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onNavigate?: () => void;
}

function NavSectionView({ section, items, pathname, open, onOpenChange, onNavigate }: NavSectionViewProps) {
  const { t } = useI18n();
  const Icon = section.icon;

  return (
    <details
      open={open}
      onToggle={(event) => onOpenChange(event.currentTarget.open)}
      className="group"
    >
      <summary className="flex h-8 w-full items-center gap-2 rounded-lg px-2.5 text-[11px] font-bold uppercase tracking-[0.1em] transition-colors cursor-pointer list-none">
        {Icon && <Icon className="h-3.5 w-3.5 shrink-0" />}
        <span className="flex-1 text-start">{t(section.labelKey)}</span>
        <ChevronRight className={`h-3 w-3 transition-transform duration-200 ${open ? "rotate-90" : ""}`} />
      </summary>
      <div className="overflow-hidden">
        <div className="space-y-1 ps-1.5">
          {items.map((item) => (
            <SidebarNavLink key={item.id} item={item} pathname={pathname} onNavigate={onNavigate} />
          ))}
        </div>
      </div>
    </details>
  );
}

interface SidebarNavLinkProps {
  item: NavItem;
  pathname: string;
  onNavigate?: () => void;
}

function SidebarNavLink({ item, pathname, onNavigate }: SidebarNavLinkProps) {
  const active = itemIsActive(item, pathname);
  const Icon = item.icon;
  const { t } = useI18n();

  return (
    <NavLink
      to={item.href}
      onClick={onNavigate}
      className={({ isActive }) =>
        `group relative flex h-8 items-center gap-3 rounded-lg px-2.5 text-[11px] font-normal transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring ${
          isActive
            ? "border border-sidebar-active/20 bg-sidebar-active/10 text-sidebar-foreground shadow-[inset_0_0_18px_hsl(var(--sidebar-active)/0.05)]"
            : "border border-transparent text-sidebar-foreground/58 hover:bg-sidebar-hover hover:text-sidebar-foreground/90"
        }`
      }
    >
      {active && <span className="absolute inset-y-1 start-0 w-[3px] rounded-full bg-sidebar-active" />}
      {Icon && <Icon className={`h-4 w-4 shrink-0 ${active ? "text-sidebar-active" : "text-sidebar-foreground/42 group-hover:text-sidebar-foreground/75"}`} />}
      <span className="truncate">{t(item.labelKey)}</span>
    </NavLink>
  );
}

export function Sidebar() {
  const location = useLocation();
  const { user } = useAuth();
  const { t, locale } = useI18n();
  const normalizedPathname = React.useMemo(() => normalizePathname(location.pathname), [location.pathname]);
  const permissions = React.useMemo(() => user?.permissions ?? [], [user?.permissions]);
  const visibleSections = React.useMemo(() => filterSections(navigationConfig, permissions), [permissions]);
  const activeItem = React.useMemo(() => visibleSections.reduce<NavItem | null>((found, entry) => found || findActiveItem(entry.items, normalizedPathname), null), [visibleSections, normalizedPathname]);
  const activeSectionId = visibleSections.find(({ items }) => findActiveItem(items, normalizedPathname) === activeItem)?.id;
  const [collapsed, setCollapsed] = React.useState(false);
  const [mobileOpen, setMobileOpen] = React.useState(false);
  const [groupState, setGroupState] = React.useState<Record<string, boolean>>({});

  React.useEffect(() => {
    setGroupState(readGroupState());
  }, []);

  React.useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  React.useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        window.dispatchEvent(new CustomEvent("nexus:open-global-search"));
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  function updateGroup(id: string, open: boolean) {
    setGroupState((previous) => {
      const next = { ...previous, [id]: open };
      try {
        window.localStorage.setItem(GROUP_STORAGE_KEY, JSON.stringify(next));
      } catch {
        // best effort
      }
      return next;
    });
  }

  const navigate = () => setMobileOpen(false);
  const openSearch = () => window.dispatchEvent(new CustomEvent("nexus:open-global-search"));

  return (
    <>
      <button
        type="button"
        aria-label={t("nav.openNavigation")}
        onClick={() => setMobileOpen(true)}
        className="fixed start-3 top-3 z-40 inline-flex h-10 w-10 items-center justify-center rounded-lg border border-border bg-background/90 text-foreground shadow-sm backdrop-blur md:hidden"
      >
        <Menu className="h-5 w-5" />
      </button>
      {mobileOpen && (
        <button
          type="button"
          aria-label={t("nav.closeNavigation")}
          onClick={() => setMobileOpen(false)}
          className="fixed inset-0 z-40 bg-black/50 backdrop-blur-[1px] md:hidden"
        />
      )}

      <aside
        dir={locale === "ar" ? "rtl" : "ltr"}
        className={`
          nexus-sidebar fixed inset-y-0 start-0 z-50 flex h-screen min-h-0 flex-col border-e border-sidebar-border bg-sidebar text-sidebar-foreground shadow-sidebar transition-[width,transform] duration-200 md:sticky md:top-0 md:z-auto md:h-screen md:max-h-screen md:shrink-0 md:shadow-none
          ${collapsed ? "md:w-[64px]" : "md:w-[260px]"}
          ${mobileOpen ? "w-[min(88vw,288px)] translate-x-0" : "w-[min(88vw,288px)] ltr:-translate-x-full rtl:translate-x-full md:!translate-x-0"}
        `}
      >
        <div className={`relative flex h-[56px] shrink-0 items-center border-b border-sidebar-border/60 px-3 ${collapsed && "md:justify-center md:px-2"}`}>
          <div className={`flex items-center gap-2.5 ${collapsed && "md:hidden"}`}>
            <div className="relative flex h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-primary-gradient shadow-primary-sm">
              <span className="text-lg font-bold text-white">N</span>
            </div>
            <div>
              <div className="text-sm font-bold tracking-tight text-sidebar-foreground">{t("app.name")}</div>
              <div className="text-[9px] font-medium uppercase tracking-[0.16em] text-sidebar-foreground/40">Enterprise PMO</div>
            </div>
          </div>
          <div className={`hidden h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-primary-gradient shadow-primary-sm ${collapsed && "md:flex"}`}>
            <span className="text-lg font-bold text-white">N</span>
          </div>
          <button
            type="button"
            aria-label={collapsed ? t("nav.expandNavigation") : t("nav.collapseNavigation")}
            onClick={() => setCollapsed((value) => !value)}
            className="ms-auto hidden h-7 w-7 items-center justify-center rounded-md text-sidebar-foreground/40 transition-colors hover:bg-sidebar-hover hover:text-sidebar-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring md:flex"
          >
            {collapsed ? <PanelLeft className="h-3.5 w-3.5" /> : <PanelLeftClose className="h-3.5 w-3.5" />}
          </button>
          <button
            type="button"
            aria-label={t("nav.closeNavigation")}
            onClick={() => setMobileOpen(false)}
            className="ms-auto inline-flex h-7 w-7 items-center justify-center rounded-md text-sidebar-foreground/40 hover:bg-sidebar-hover md:hidden"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>

        <div className={`shrink-0 p-2.5 ${collapsed && "md:p-1.5"}`}>
          <button
            type="button"
            onClick={openSearch}
            aria-label={t("nav.searchNexus")}
            className={`
              group flex h-8 w-full items-center gap-2 rounded-md border border-sidebar-border/50 bg-sidebar-accent/30 px-2 text-start text-[10px] text-sidebar-foreground/50 transition-colors
              hover:border-sidebar-active/40 hover:bg-sidebar-accent hover:text-sidebar-foreground
              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring
              ${collapsed && "md:justify-center md:px-0"}
            `}
          >
            <Search className="h-3.5 w-3.5 shrink-0 text-sidebar-foreground/45 group-hover:text-sidebar-active" />
            {!collapsed && (
              <>
                <span className="flex-1 truncate">{t("nav.searchPlaceholder")}</span>
                <kbd className="hidden rounded border border-sidebar-border/50 bg-sidebar/40 px-1.5 py-0.5 text-[9px] text-sidebar-foreground/40 md:inline">Ctrl K</kbd>
              </>
            )}
          </button>
        </div>

        <ScrollArea className="min-h-0 flex-1 px-2 pb-2 scrollbar-thin">
          <nav aria-label={t("nav.globalNavigation")} className={`space-y-0.5 ${collapsed && "md:px-0"}`}>
            {visibleSections.map(({ id, items, labelKey, icon: Icon }) => {
              const active = activeSectionId === id;
              const open = groupState[id] ?? active;
              return (
                <NavSectionView
                  key={id}
                  section={{ id, labelKey, icon: Icon, items: [] }}
                  items={items}
                  pathname={normalizedPathname}
                  active={active}
                  open={open}
                  onOpenChange={(value: boolean) => updateGroup(id, value)}
                  onNavigate={navigate}
                />
              );
            })}
          </nav>
        </ScrollArea>

        <div className={`shrink-0 border-t border-sidebar-border/60 p-2.5 ${collapsed && "md:p-1.5"}`}>
          <div className={`flex items-center gap-2 rounded-md bg-sidebar-accent/20 p-2 ${collapsed && "md:justify-center md:bg-transparent md:p-0"}`}>
            <div className="h-7 w-7 shrink-0 rounded-full bg-primary/15 flex items-center justify-center">
              <span className="text-[10px] font-semibold text-primary">
                {user?.fullName?.split(" ").map((p) => p[0]).join("").slice(0, 2).toUpperCase() || "US"}
              </span>
            </div>
            {!collapsed && (
              <div className="min-w-0">
                <div className="truncate text-[11px] font-medium text-sidebar-foreground">{user?.fullName || "User"}</div>
                <div className="truncate text-[9px] text-sidebar-muted/70">{user?.roles?.[0]?.name || user?.jobTitle || "NEXUS user"}</div>
              </div>
            )}
          </div>
        </div>
      </aside>
    </>
  );
}

function ScrollArea({ children, className }: { children: React.ReactNode; className?: string }) {
  return <div className={`flex-1 overflow-y-auto ${className || ""}`}>{children}</div>;
}
