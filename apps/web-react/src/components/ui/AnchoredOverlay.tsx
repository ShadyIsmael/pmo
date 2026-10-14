import * as React from "react";

export type AnchoredOverlayAlignment = "start" | "center" | "end";

interface OverlayRect {
  top: number;
  bottom: number;
  left: number;
  right: number;
  width: number;
  height: number;
}

interface OverlayViewport {
  width: number;
  height: number;
}

interface AnchoredOverlayOptions {
  align?: AnchoredOverlayAlignment;
  matchTriggerWidth?: boolean;
  rtl?: boolean;
}

const VIEWPORT_GUTTER = 8;
const OVERLAY_GAP = 4;
const DEFAULT_MENU_WIDTH = 192;

export function calculateAnchoredOverlayPosition(
  anchor: OverlayRect,
  content: Pick<OverlayRect, "width" | "height">,
  viewport: OverlayViewport,
  { align = "start", matchTriggerWidth = false, rtl = false }: AnchoredOverlayOptions = {}
): React.CSSProperties {
  const availableWidth = Math.max(0, viewport.width - VIEWPORT_GUTTER * 2);
  const contentWidth = matchTriggerWidth ? anchor.width : content.width || DEFAULT_MENU_WIDTH;
  const width = Math.min(contentWidth, availableWidth);
  const below = Math.max(0, viewport.height - anchor.bottom - OVERLAY_GAP - VIEWPORT_GUTTER);
  const above = Math.max(0, anchor.top - OVERLAY_GAP - VIEWPORT_GUTTER);
  const contentHeight = content.height;
  const openAbove = contentHeight > below && (contentHeight <= above || above > below);
  const availableHeight = openAbove ? above : below;
  const visibleHeight = Math.min(contentHeight, availableHeight);

  const alignedLeft = align === "center"
    ? anchor.left + (anchor.width - width) / 2
    : align === "end"
      ? (rtl ? anchor.left : anchor.right - width)
      : (rtl ? anchor.right - width : anchor.left);
  const left = Math.max(VIEWPORT_GUTTER, Math.min(alignedLeft, viewport.width - VIEWPORT_GUTTER - width));
  const top = openAbove
    ? Math.max(VIEWPORT_GUTTER, anchor.top - visibleHeight - OVERLAY_GAP)
    : Math.min(anchor.bottom + OVERLAY_GAP, viewport.height - VIEWPORT_GUTTER);

  return {
    position: "fixed",
    top,
    left,
    width: matchTriggerWidth ? width : undefined,
    minWidth: matchTriggerWidth ? width : Math.min(DEFAULT_MENU_WIDTH, availableWidth),
    maxWidth: availableWidth,
    maxHeight: availableHeight,
    overflowY: "auto",
    zIndex: 110,
  };
}

export function useAnchoredOverlayPosition({
  open,
  anchorRef,
  contentRef,
  align = "start",
  matchTriggerWidth = false,
}: {
  open: boolean;
  anchorRef: React.RefObject<HTMLElement>;
  contentRef: React.RefObject<HTMLElement>;
  align?: AnchoredOverlayAlignment;
  matchTriggerWidth?: boolean;
}): React.CSSProperties {
  const [style, setStyle] = React.useState<React.CSSProperties>({ visibility: "hidden" });

  React.useLayoutEffect(() => {
    if (!open) return;

    const updatePosition = () => {
      const anchor = anchorRef.current;
      const content = contentRef.current;
      if (!anchor || !content) return;

      const anchorRect = anchor.getBoundingClientRect();
      const contentRect = content.getBoundingClientRect();
      const nextStyle = calculateAnchoredOverlayPosition(
        anchorRect,
        {
          width: contentRect.width || content.offsetWidth,
          height: contentRect.height || content.offsetHeight || content.scrollHeight,
        },
        { width: window.innerWidth, height: window.innerHeight },
        { align, matchTriggerWidth, rtl: document.documentElement.dir === "rtl" }
      );
      setStyle((current) => {
        const keys = Object.keys(nextStyle) as Array<keyof React.CSSProperties>;
        return keys.every((key) => current[key] === nextStyle[key]) ? current : nextStyle;
      });
    };

    updatePosition();
    window.addEventListener("resize", updatePosition);
    document.addEventListener("scroll", updatePosition, true);

    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(updatePosition);
    if (anchorRef.current) observer?.observe(anchorRef.current);
    if (contentRef.current) observer?.observe(contentRef.current);

    return () => {
      window.removeEventListener("resize", updatePosition);
      document.removeEventListener("scroll", updatePosition, true);
      observer?.disconnect();
    };
  }, [align, anchorRef, contentRef, matchTriggerWidth, open]);

  return style;
}
