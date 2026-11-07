import { brand, brandCopy } from "@lib/brand";
import { productVersion } from "@lib/version";
import * as React from "react";
import { NavLink, useLocation } from "react-router-dom";
import { ChevronRight, Search, X, PanelLeft, PanelLeftClose, Menu, Info } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { navigationConfig, filterSections, normalizePathname, findActiveItem, itemIsActive, type NavItem, type NavSection } from "@lib/navigation";
import { useAuth } from "@auth/AuthProvider";
import { useSettingsAccess } from "@features/administration/api/settings";
import { useOrganizationProfiles } from "@features/administration/api/settings";
import { SidebarRail } from "./SidebarRail";
import { cn } from "@lib/utils";

const GROUP_STORAGE_KEY = "nexus_sidebar_groups";
const COLLAPSED_STORAGE_KEY = "nexus_sidebar_collapsed";
const DESKTOP_MEDIA_QUERY = "(min-width: 768px)";

function readGroupState(): Record<string, boolean> {
  if (typeof window === "undefined") return {};
  try {
    return JSON.parse(window.localStorage.getItem(GROUP_STORAGE_KEY) || "{}") as Record<string, boolean>;
  } catch {
    return {};
  }
}

function readCollapsedPreference(): boolean {
  if (typeof window === "undefined") return false;
  try {
    return window.localStorage.getItem(COLLAPSED_STORAGE_KEY) === "true";
  } catch {
    return false;
  }
}

function persistCollapsedPreference(value: boolean) {
  try {
    window.localStorage.setItem(COLLAPSED_STORAGE_KEY, String(value));
  } catch {
    // best effort
  }
}

/** Collapsed rail styles are desktop-only; the mobile drawer always renders expanded. */
function useIsDesktopViewport(): boolean {
  const [isDesktop, setIsDesktop] = React.useState<boolean>(() => {
    if (typeof window === "undefined" || typeof window.matchMedia !== "function") return true;
    return window.matchMedia(DESKTOP_MEDIA_QUERY).matches;
  });

  React.useEffect(() => {
    if (typeof window === "undefined" || typeof window.matchMedia !== "function") return;
    const media = window.matchMedia(DESKTOP_MEDIA_QUERY);
    const onChange = (event: MediaQueryListEvent) => setIsDesktop(event.matches);
    setIsDesktop(media.matches);
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, []);

  return isDesktop;
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
  const settingsAccess = useSettingsAccess();
  const canReadPlatform = settingsAccess.data?.canReadPlatform === true;
  const organizationSettingsAccess = useOrganizationProfiles(permissions.includes("Settings.View"));
  const canReadOrganizations = (organizationSettingsAccess.data?.length ?? 0) > 0;
  const visibleSections = React.useMemo(() => filterSections(navigationConfig, permissions, canReadPlatform, canReadOrganizations), [permissions, canReadPlatform, canReadOrganizations]);
  const activeItem = React.useMemo(() => visibleSections.reduce<NavItem | null>((found, entry) => found || findActiveItem(entry.items, normalizedPathname), null), [visibleSections, normalizedPathname]);
  const activeSectionId = visibleSections.find(({ items }) => findActiveItem(items, normalizedPathname) === activeItem)?.id;
  const isDesktop = useIsDesktopViewport();
  const [collapsed, setCollapsed] = React.useState<boolean>(readCollapsedPreference);
  const [mobileOpen, setMobileOpen] = React.useState(false);
  const [groupState, setGroupState] = React.useState<Record<string, boolean>>({});
  const rail = collapsed && isDesktop;

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

  function updateCollapsed(next: boolean) {
    setCollapsed(next);
    persistCollapsedPreference(next);
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
        id="app-sidebar"
        dir={locale === "ar" ? "rtl" : "ltr"}
        className={`
          nexus-sidebar fixed inset-y-0 start-0 z-50 flex h-screen min-h-0 flex-col border-e border-sidebar-border bg-sidebar text-sidebar-foreground shadow-sidebar transition-[width,transform] duration-200 motion-reduce:transition-none md:sticky md:top-0 md:z-auto md:h-screen md:max-h-screen md:shrink-0 md:shadow-none
          ${rail ? "md:w-[72px]" : "md:w-[260px]"}
          ${mobileOpen ? "w-[min(88vw,288px)] translate-x-0" : "w-[min(88vw,288px)] ltr:-translate-x-full rtl:translate-x-full md:!translate-x-0"}
        `}
      >
        <div className={cn("relative flex h-14 shrink-0 items-center border-b border-sidebar-border/60", rail ? "justify-center px-2" : "px-3")}>
          {rail ? (
            <div className="flex h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-primary-gradient shadow-primary-sm">
              <span className="text-lg font-bold text-white">{brand.product.monogram}</span>
            </div>
          ) : (
            <div className="flex items-center gap-2.5">
              <div className="relative flex h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-primary-gradient shadow-primary-sm">
                <span className="text-lg font-bold text-white">{brand.product.monogram}</span>
              </div>
              <div>
                <div className="text-sm font-bold tracking-tight text-sidebar-foreground">{brand.product.name}</div>
                <div className="text-[9px] font-medium uppercase tracking-[0.16em] text-sidebar-foreground/40">{brandCopy(locale).tagline}</div>
              </div>
            </div>
          )}
          <button
            type="button"
            aria-label={collapsed ? t("nav.expandNavigation") : t("nav.collapseNavigation")}
            aria-expanded={!collapsed}
            aria-controls="app-sidebar"
            title={collapsed ? t("nav.expandNavigation") : t("nav.collapseNavigation")}
            onClick={() => updateCollapsed(!collapsed)}
            className={cn(
              "hidden items-center justify-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring md:flex",
              rail
                ? "absolute -end-3 top-1/2 z-10 h-6 w-6 -translate-y-1/2 rounded-full border border-sidebar-border bg-sidebar text-sidebar-foreground/60 shadow-sm hover:bg-sidebar-hover hover:text-sidebar-foreground"
                : "ms-auto h-7 w-7 rounded-md text-sidebar-foreground/40 hover:bg-sidebar-hover hover:text-sidebar-foreground"
            )}
          >
            {collapsed ? <PanelLeft className="h-3.5 w-3.5 rtl:-scale-x-100" /> : <PanelLeftClose className="h-3.5 w-3.5 rtl:-scale-x-100" />}
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

        <div className={cn("shrink-0", rail ? "p-1.5" : "p-2.5")}>
          <button
            type="button"
            onClick={openSearch}
            aria-label={t("nav.searchNexus")}
            title={t("nav.searchNexus")}
            className={cn(
              "group flex items-center rounded-md border border-sidebar-border/50 bg-sidebar-accent/30 text-start text-[10px] text-sidebar-foreground/50 transition-colors",
              "hover:border-sidebar-active/40 hover:bg-sidebar-accent hover:text-sidebar-foreground",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
              rail ? "mx-auto h-10 w-10 justify-center px-0" : "h-8 w-full gap-2 px-2"
            )}
          >
            <Search className="h-3.5 w-3.5 shrink-0 text-sidebar-foreground/45 group-hover:text-sidebar-active" />
            {!rail && (
              <>
                <span className="flex-1 truncate">{t("nav.searchPlaceholder")}</span>
                <kbd className="hidden rounded border border-sidebar-border/50 bg-sidebar/40 px-1.5 py-0.5 text-[9px] text-sidebar-foreground/40 md:inline">Ctrl K</kbd>
              </>
            )}
          </button>
        </div>

        <ScrollArea className={cn("min-h-0 flex-1 pb-2 scrollbar-thin", rail ? "overflow-x-hidden px-0" : "px-2")}>
          {rail ? (
            <SidebarRail
              sections={visibleSections}
              pathname={normalizedPathname}
              activeSectionId={activeSectionId}
              rtl={locale === "ar"}
              onNavigate={navigate}
            />
          ) : (
            <nav aria-label={t("nav.globalNavigation")} className="space-y-0.5">
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
          )}
        </ScrollArea>

        <div className={cn("shrink-0 border-t border-sidebar-border/60", rail ? "p-1.5" : "p-2.5")}>
          <div
            className={cn(
              "flex items-center gap-2 rounded-md text-[10px] text-sidebar-foreground/55 transition-colors hover:bg-sidebar-hover hover:text-sidebar-foreground/80",
              rail ? "justify-center px-0 py-1.5" : "px-1.5 py-1.5"
            )}
            title={rail ? `${brand.product.name} ${productVersion.version}` : undefined}
          >
            <Info className="h-3.5 w-3.5 shrink-0" />
            {!rail && (
              <div className="min-w-0">
                <div className="truncate text-[10px] font-medium uppercase tracking-[0.12em] text-sidebar-foreground/45">Version</div>
                <div className="truncate font-mono text-[11px] text-sidebar-foreground/70">{productVersion.version}</div>
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
