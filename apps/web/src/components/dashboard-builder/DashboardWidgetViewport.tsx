"use client";

import * as React from "react";

interface DashboardWidgetViewportProps {
  children: React.ReactNode;
  widgetType: string;
  priority?: boolean;
  onVisible?: (_widgetType: string) => void;
}

const EMPTY_WIDGET = <div className="h-full w-full" aria-hidden="true" />;

export function DashboardWidgetViewport({
  children,
  widgetType,
  priority = false,
  onVisible,
}: DashboardWidgetViewportProps) {
  const containerRef = React.useRef<HTMLDivElement>(null);
  const notifiedRef = React.useRef(false);
  const [visible, setVisible] = React.useState(priority);

  const reveal = React.useCallback(() => {
    if (notifiedRef.current) return;
    notifiedRef.current = true;
    setVisible(true);
    onVisible?.(widgetType);
  }, [onVisible, widgetType]);

  React.useEffect(() => {
    if (priority) {
      reveal();
      return;
    }

    const element = containerRef.current;
    if (!element) return;

    if (!("IntersectionObserver" in window)) {
      reveal();
      return;
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry?.isIntersecting) return;
        reveal();
        observer.disconnect();
      },
      { rootMargin: "0px" },
    );
    observer.observe(element);

    return () => observer.disconnect();
  }, [priority, reveal]);

  return (
    <div ref={containerRef} className="h-full min-h-0">
      {visible ? children : EMPTY_WIDGET}
    </div>
  );
}
