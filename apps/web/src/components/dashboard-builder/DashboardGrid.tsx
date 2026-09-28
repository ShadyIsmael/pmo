"use client";

import * as React from "react";
import GridLayout, { type Layout, type LayoutItem } from "react-grid-layout/legacy";
import "react-grid-layout/css/styles.css";
import "react-resizable/css/styles.css";
import type { WidgetInstance } from "./dashboard-types";
import { DashboardWidgetViewport } from "./DashboardWidgetViewport";

interface DashboardGridProps {
  widgets: WidgetInstance[];
  cols: number;
  rowHeight: number;
  width: number;
  editMode: boolean;
  widgetNodes: Map<string, React.ReactNode>;
  onLayoutChange: (_widgets: WidgetInstance[]) => void;
  onWidgetVisible: (_widgetType: string) => void;
}

export function DashboardGrid({ widgets, cols, rowHeight, width, editMode, widgetNodes, onLayoutChange, onWidgetVisible }: DashboardGridProps) {
  const visibleWidgets = React.useMemo(() => widgets.filter((w) => !w.collapsed), [widgets]);
  const layout = React.useMemo(
    () =>
      visibleWidgets.map(
          (w) =>
            ({
              i: w.id,
              x: w.position.x,
              y: w.position.y,
              w: w.position.w,
              h: w.position.h,
              minW: 1,
              minH: 2,
            }) as LayoutItem,
        ) as Layout,
    [visibleWidgets],
  );

  const handleLayoutChange = React.useCallback(
    (newLayout: Layout) => {
      if (!editMode) return;
      let changed = false;
      const positions = new Map((newLayout as LayoutItem[]).map((item) => [item.i, item]));
      const updated = widgets.map((w) => {
        if (w.collapsed) return w;
        const match = positions.get(w.id);
        if (!match) return w;
        const { x, y, w: width, h: height } = match;
        if (x === w.position.x && y === w.position.y && width === w.position.w && height === w.position.h) {
          return w;
        }
        changed = true;
        return { ...w, position: { x, y, w: width, h: height } };
      });
      if (changed) onLayoutChange(updated);
    },
    [widgets, editMode, onLayoutChange],
  );

  return (
    <GridLayout
      className="layout"
      layout={layout}
      cols={cols}
      rowHeight={rowHeight}
      width={width}
      isDraggable={editMode}
      isResizable={editMode}
      compactType="vertical"
      preventCollision={false}
      onLayoutChange={handleLayoutChange}
      draggableHandle=".drag-handle"
      margin={[12, 12]}
      containerPadding={[0, 0]}
    >
      {visibleWidgets.map((w) => (
          <div key={w.id} className="overflow-hidden">
            <DashboardWidgetViewport
              widgetType={w.type}
              priority={w.position.y === 0}
              onVisible={onWidgetVisible}
            >
              {widgetNodes.get(w.id)}
            </DashboardWidgetViewport>
          </div>
        ))}
    </GridLayout>
  );
}
