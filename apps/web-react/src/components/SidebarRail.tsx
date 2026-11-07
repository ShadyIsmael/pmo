import * as React from "react";
import { createPortal } from "react-dom";
import { NavLink } from "react-router-dom";
import { useI18n } from "@i18n/messages";
import { itemIsActive, type NavItem, type NavSection } from "@lib/navigation";
import { useAnchoredOverlayPosition } from "@components/ui/AnchoredOverlay";
import { cn } from "@lib/utils";

const OPEN_DELAY_MS = 110;
const CLOSE_DELAY_MS = 180;

function isKeyboardFocus(element: HTMLElement): boolean {
  try {
    return element.matches(":focus-visible");
  } catch {
    return true;
  }
}

interface RailOverlayProps {
  id?: string;
  anchorRef: React.RefObject<HTMLElement>;
  overlayRef: React.RefObject<HTMLDivElement>;
  rtl: boolean;
  label: string;
  role?: "group" | "navigation";
  className?: string;
  onPointerEnter?: () => void;
  onPointerLeave?: () => void;
  onKeyDown?: (event: React.KeyboardEvent<HTMLDivElement>) => void;
  onBlur?: (event: React.FocusEvent<HTMLDivElement>) => void;
  children: React.ReactNode;
}

/**
 * Floating surface rendered in a portal so sidebar overflow containers and the
 * sticky aside transform can never clip it. Positioned beside its anchor and
 * clamped to the viewport by the shared anchored-overlay helper.
 */
function RailOverlay({ id, anchorRef, overlayRef, rtl, label, role = "group", className, onPointerEnter, onPointerLeave, onKeyDown, onBlur, children }: RailOverlayProps) {
  const style = useAnchoredOverlayPosition({ open: true, anchorRef, contentRef: overlayRef, placement: "side" });
  const [entered, setEntered] = React.useState(false);

  React.useEffect(() => {
    const frame = window.requestAnimationFrame(() => setEntered(true));
    return () => window.cancelAnimationFrame(frame);
  }, []);

  return createPortal(
    <div
      id={id}
      ref={overlayRef}
      dir={rtl ? "rtl" : "ltr"}
      role={role}
      aria-label={label}
      style={style}
      onPointerEnter={(event) => {
        if (event.pointerType === "mouse") onPointerEnter?.();
      }}
      onPointerLeave={(event) => {
        if (event.pointerType === "mouse") onPointerLeave?.();
      }}
      onKeyDown={onKeyDown}
      onBlur={onBlur}
      className={cn(
        "rounded-xl border border-sidebar-border bg-sidebar text-sidebar-foreground shadow-xl transition-[opacity,transform] duration-150 ease-out motion-reduce:transition-none",
        entered ? "scale-100 opacity-100" : "scale-95 opacity-0",
        className
      )}
    >
      {children}
    </div>,
    document.body
  );
}

interface RailHoverOptions {
  onOpen: () => void;
  onClose: () => void;
}

function useRailHover({ onOpen, onClose }: RailHoverOptions) {
  const openTimer = React.useRef<number | null>(null);
  const closeTimer = React.useRef<number | null>(null);

  const clear = React.useCallback(() => {
    if (openTimer.current !== null) {
      window.clearTimeout(openTimer.current);
      openTimer.current = null;
    }
    if (closeTimer.current !== null) {
      window.clearTimeout(closeTimer.current);
      closeTimer.current = null;
    }
  }, []);

  React.useEffect(() => clear, [clear]);

  const open = React.useCallback(() => {
    clear();
    openTimer.current = window.setTimeout(() => {
      openTimer.current = null;
      onOpen();
    }, OPEN_DELAY_MS);
  }, [clear, onOpen]);

  const close = React.useCallback(() => {
    clear();
    closeTimer.current = window.setTimeout(() => {
      closeTimer.current = null;
      onClose();
    }, CLOSE_DELAY_MS);
  }, [clear, onClose]);

  return { open, close, clear };
}

interface RailSectionProps {
  section: NavSection;
  pathname: string;
  active: boolean;
  open: boolean;
  rtl: boolean;
  onOpenChange: (open: boolean) => void;
  onNavigate: () => void;
}

function RailSection({ section, pathname, active, open, rtl, onOpenChange, onNavigate }: RailSectionProps) {
  const { t } = useI18n();
  const triggerRef = React.useRef<HTMLButtonElement>(null);
  const flyoutRef = React.useRef<HTMLDivElement>(null);
  const focusFirstRef = React.useRef(false);
  const lastPointerType = React.useRef("mouse");
  const hover = useRailHover({ onOpen: () => onOpenChange(true), onClose: () => onOpenChange(false) });
  const Icon = section.icon;
  const flyoutId = `nav-flyout-${section.id}`;
  const label = t(section.labelKey);

  React.useEffect(() => {
    if (!open || !focusFirstRef.current) return;
    focusFirstRef.current = false;
    flyoutRef.current?.querySelector<HTMLElement>("[data-nav-flyout-link]")?.focus();
  }, [open]);

  React.useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node | null;
      if (!target) return;
      if (triggerRef.current?.contains(target) || flyoutRef.current?.contains(target)) return;
      onOpenChange(false);
    };
    document.addEventListener("pointerdown", onPointerDown, true);
    return () => document.removeEventListener("pointerdown", onPointerDown, true);
  }, [open, onOpenChange]);

  function handleTriggerKeyDown(event: React.KeyboardEvent<HTMLButtonElement>) {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      hover.clear();
      onOpenChange(!open);
      return;
    }
    if (event.key === "ArrowDown" || event.key === "ArrowRight") {
      event.preventDefault();
      hover.clear();
      if (!open) focusFirstRef.current = true;
      onOpenChange(true);
      return;
    }
    if (event.key === "Escape" && open) {
      event.preventDefault();
      hover.clear();
      onOpenChange(false);
    }
  }

  function handleFlyoutKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape") {
      event.preventDefault();
      hover.clear();
      onOpenChange(false);
      triggerRef.current?.focus();
      return;
    }
    if (event.key === "Tab") {
      // The flyout is portalled to the end of the document, so return focus to
      // the trigger before letting the browser continue the tab sequence.
      hover.clear();
      triggerRef.current?.focus();
      onOpenChange(false);
      return;
    }
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp" && event.key !== "Home" && event.key !== "End") return;
    const links = Array.from(flyoutRef.current?.querySelectorAll<HTMLElement>("[data-nav-flyout-link]") ?? []);
    if (links.length === 0) return;
    event.preventDefault();
    const currentIndex = links.indexOf(document.activeElement as HTMLElement);
    const nextIndex = event.key === "Home"
      ? 0
      : event.key === "End"
        ? links.length - 1
        : event.key === "ArrowDown"
          ? (currentIndex + 1) % links.length
          : (currentIndex - 1 + links.length) % links.length;
    links[nextIndex]?.focus();
  }

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        aria-label={label}
        aria-expanded={open}
        aria-controls={flyoutId}
        onPointerDown={(event) => {
          lastPointerType.current = event.pointerType || "mouse";
        }}
        onPointerEnter={(event) => {
          if (event.pointerType === "mouse") hover.open();
        }}
        onPointerLeave={(event) => {
          if (event.pointerType === "mouse") hover.close();
        }}
        onFocus={(event) => {
          if (!isKeyboardFocus(event.currentTarget)) return;
          hover.clear();
          onOpenChange(true);
        }}
        onBlur={(event) => {
          const next = event.relatedTarget as Node | null;
          if (next && flyoutRef.current?.contains(next)) return;
          hover.close();
        }}
        onKeyDown={handleTriggerKeyDown}
        onClick={() => {
          hover.clear();
          // Mouse users already see the flyout on hover: a click keeps it open.
          // Touch and pen users toggle it with a tap.
          if (lastPointerType.current === "mouse") {
            onOpenChange(true);
            return;
          }
          onOpenChange(!open);
        }}
        className={cn(
          "group relative flex h-10 w-10 items-center justify-center rounded-lg border transition-colors duration-150",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
          active
            ? "border-sidebar-active/25 bg-sidebar-active/10 text-sidebar-active shadow-[inset_0_0_18px_hsl(var(--sidebar-active)/0.06)]"
            : "border-transparent text-sidebar-foreground/60 hover:bg-sidebar-hover hover:text-sidebar-foreground",
          open && !active && "bg-sidebar-hover text-sidebar-foreground"
        )}
      >
        {active && <span aria-hidden="true" className="absolute inset-y-1.5 start-0 w-[3px] rounded-full bg-sidebar-active" />}
        <Icon aria-hidden="true" className="h-[18px] w-[18px] shrink-0" />
      </button>
      {open && (
        <RailOverlay
          id={flyoutId}
          anchorRef={triggerRef}
          overlayRef={flyoutRef}
          rtl={rtl}
          label={label}
          role="navigation"
          className="w-60 p-1.5"
          onPointerEnter={hover.clear}
          onPointerLeave={hover.close}
          onKeyDown={handleFlyoutKeyDown}
          onBlur={(event) => {
            const next = event.relatedTarget as Node | null;
            if (next && event.currentTarget.contains(next)) return;
            hover.clear();
            onOpenChange(false);
          }}
        >
          <p className="px-2 pb-1.5 pt-1 text-[10px] font-bold uppercase tracking-[0.1em] text-sidebar-foreground/50">{label}</p>
          <div className="space-y-0.5">
            {section.items.map((item) => (
              <FlyoutLink key={item.id} item={item} pathname={pathname} onNavigate={onNavigate} depth={0} />
            ))}
          </div>
        </RailOverlay>
      )}
    </>
  );
}

interface FlyoutLinkProps {
  item: NavItem;
  pathname: string;
  onNavigate: () => void;
  depth: number;
}

function FlyoutLink({ item, pathname, onNavigate, depth }: FlyoutLinkProps) {
  const { t } = useI18n();
  const active = itemIsActive(item, pathname);
  const Icon = item.icon;

  return (
    <>
      <NavLink
        to={item.href}
        onClick={onNavigate}
        data-nav-flyout-link=""
        className={({ isActive }) =>
          cn(
            "group relative flex h-8 items-center gap-2 rounded-lg px-2 text-[11px] transition-colors duration-150",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
            isActive
              ? "bg-sidebar-active/10 text-sidebar-foreground"
              : "text-sidebar-foreground/70 hover:bg-sidebar-hover hover:text-sidebar-foreground",
            depth > 0 && "ms-3"
          )
        }
      >
        {active && <span aria-hidden="true" className="absolute inset-y-1 start-0 w-[3px] rounded-full bg-sidebar-active" />}
        <Icon aria-hidden="true" className={cn("h-3.5 w-3.5 shrink-0", active ? "text-sidebar-active" : "text-sidebar-foreground/50 group-hover:text-sidebar-foreground/75")} />
        <span className="truncate">{t(item.labelKey)}</span>
      </NavLink>
      {item.items?.map((child) => (
        <FlyoutLink key={child.id} item={child} pathname={pathname} onNavigate={onNavigate} depth={depth + 1} />
      ))}
    </>
  );
}

interface RailLinkProps {
  item: NavItem;
  pathname: string;
  rtl: boolean;
  onNavigate: () => void;
}

/** Direct rail link for single-item sections: a compact label tooltip replaces an empty flyout. */
function RailLink({ item, pathname, rtl, onNavigate }: RailLinkProps) {
  const { t } = useI18n();
  const linkRef = React.useRef<HTMLAnchorElement>(null);
  const tooltipRef = React.useRef<HTMLDivElement>(null);
  const [tooltipOpen, setTooltipOpen] = React.useState(false);
  const hover = useRailHover({ onOpen: () => setTooltipOpen(true), onClose: () => setTooltipOpen(false) });
  const active = itemIsActive(item, pathname);
  const Icon = item.icon;
  const label = t(item.labelKey);

  return (
    <>
      <NavLink
        ref={linkRef}
        to={item.href}
        onClick={onNavigate}
        aria-label={label}
        onPointerEnter={(event) => {
          if (event.pointerType === "mouse") hover.open();
        }}
        onPointerLeave={(event) => {
          if (event.pointerType === "mouse") hover.close();
        }}
        onFocus={(event) => {
          if (!isKeyboardFocus(event.currentTarget)) return;
          hover.clear();
          setTooltipOpen(true);
        }}
        onBlur={() => {
          hover.clear();
          setTooltipOpen(false);
        }}
        className={({ isActive }) =>
          cn(
            "group relative flex h-10 w-10 items-center justify-center rounded-lg border transition-colors duration-150",
            "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
            isActive
              ? "border-sidebar-active/25 bg-sidebar-active/10 text-sidebar-active"
              : "border-transparent text-sidebar-foreground/60 hover:bg-sidebar-hover hover:text-sidebar-foreground"
          )
        }
      >
        {active && <span aria-hidden="true" className="absolute inset-y-1.5 start-0 w-[3px] rounded-full bg-sidebar-active" />}
        <Icon aria-hidden="true" className="h-[18px] w-[18px] shrink-0" />
      </NavLink>
      {tooltipOpen && (
        <RailOverlay
          anchorRef={linkRef}
          overlayRef={tooltipRef}
          rtl={rtl}
          label={label}
          className="w-auto whitespace-nowrap px-2.5 py-1.5 text-[11px] font-medium"
          onPointerEnter={hover.clear}
          onPointerLeave={hover.close}
        >
          {label}
        </RailOverlay>
      )}
    </>
  );
}

export interface SidebarRailProps {
  sections: NavSection[];
  pathname: string;
  activeSectionId?: string;
  rtl: boolean;
  onNavigate: () => void;
}

/**
 * Icon-only navigation rail for the collapsed sidebar. Renders the existing
 * navigation configuration — it never defines its own menu structure.
 */
export function SidebarRail({ sections, pathname, activeSectionId, rtl, onNavigate }: SidebarRailProps) {
  const { t } = useI18n();
  const [openSectionId, setOpenSectionId] = React.useState<string | null>(null);

  React.useEffect(() => {
    setOpenSectionId(null);
  }, [pathname]);

  const setSectionOpen = React.useCallback((sectionId: string, open: boolean) => {
    setOpenSectionId((current) => (open ? sectionId : current === sectionId ? null : current));
  }, []);

  return (
    <nav aria-label={t("nav.globalNavigation")} aria-orientation="vertical" className="flex flex-col items-center gap-1 py-1">
      {sections.map((section) => {
        const single = section.items.length === 1 && !section.items[0].items?.length;
        if (single) {
          return <RailLink key={section.id} item={section.items[0]} pathname={pathname} rtl={rtl} onNavigate={onNavigate} />;
        }
        return (
          <RailSection
            key={section.id}
            section={section}
            pathname={pathname}
            active={activeSectionId === section.id}
            open={openSectionId === section.id}
            rtl={rtl}
            onOpenChange={(open) => setSectionOpen(section.id, open)}
            onNavigate={onNavigate}
          />
        );
      })}
    </nav>
  );
}
