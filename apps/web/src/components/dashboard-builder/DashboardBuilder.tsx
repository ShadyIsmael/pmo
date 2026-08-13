"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import { ArrowUp, ArrowDown, Plus, Layout } from "lucide-react";
import { DashboardHeader } from "./DashboardHeader";
import { DashboardGrid } from "./DashboardGrid";
import { WidgetShell } from "./WidgetShell";
import { WidgetLibrary } from "./WidgetLibrary";
import { WidgetSettings } from "./WidgetSettings";
import { DashboardViewManager } from "./DashboardViewManager";
import { saveState, loadState } from "./dashboard-storage";
import { createDefaultView } from "./default-layouts";
import { getWidgetDefinition, getWidgetComponent } from "./widget-registry";
import type {
  DashboardState, DashboardView, WidgetInstance,
  PointOfView, WidgetConfig, GlobalFilters, DashboardAccess, DashboardStartMode, DashboardType,
} from "./dashboard-types";
import "./widgets/kpi-widgets";
import "./widgets/chart-widgets";
import "./widgets/list-widgets";
import "./widgets/integration-widgets";
import "./widgets/ai-widgets";
import "./widgets/extra-widgets";

function wid(): string {
  return `w-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}

function createSharedTemplate(pov: PointOfView): DashboardView {
  const view = createDefaultView(pov, `${pov} Standard`);
  return {
    ...view,
    id: `shared-${pov.toLowerCase().replace(/\s+/g, "-")}`,
    ownerId: "pmo-admin",
    ownerName: "PMO Office",
    type: "shared",
    access: "duplicate",
    sharedWith: [pov],
    isDefault: false,
  };
}

function createInitialState(pov: PointOfView): DashboardState {
  const view = createDefaultView(pov);
  return {
    currentViewId: view.id,
    views: [view, createSharedTemplate(pov)],
    pov,
    lastViewByPOV: { [pov]: view.id },
    editMode: false,
    lastUpdated: null,
  };
}

export function DashboardBuilder() {
  const [state, setState] = React.useState<DashboardState>(() => {
    const saved = loadState();
    if (saved) return saved;
    return createInitialState("Executive");
  });

  const [libraryOpen, setLibraryOpen] = React.useState(false);
  const [settingsWidgetId, setSettingsWidgetId] = React.useState<string | null>(null);
  const [viewsOpen, setViewsOpen] = React.useState(false);

  const latestStateRef = React.useRef(state);
  React.useEffect(() => {
    latestStateRef.current = state;
    const timer = setTimeout(() => saveState(state), 300);
    return () => clearTimeout(timer);
  }, [state]);
  React.useEffect(() => {
    return () => saveState(latestStateRef.current);
  }, []);

  const povViews = React.useMemo(() => state.views.filter((view) => view.pov === state.pov), [state.views, state.pov]);
  const currentView = (povViews.find((v) => v.id === state.currentViewId) ?? povViews.find((v) => v.isDefault) ?? povViews[0])!;
  const canEditCurrent = currentView.access === "edit" || currentView.access === "manage-sharing";

  const [windowWidth, setWindowWidth] = React.useState(1200);
  React.useEffect(() => {
    const onResize = () => setWindowWidth(window.innerWidth);
    onResize();
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  const isMobile = windowWidth < 640;
  const isTablet = windowWidth >= 640 && windowWidth < 1024;
  const cols = isMobile ? 1 : isTablet ? 6 : 12;

  const updateView = React.useCallback(
    (updater: (view: DashboardView) => DashboardView) => {
      setState((prev) => {
        const view = prev.views.find((v) => v.id === prev.currentViewId);
        if (!view) return prev;
        return {
          ...prev,
          views: prev.views.map((v) =>
            v.id === prev.currentViewId ? { ...updater({ ...v }), updatedAt: Date.now() } : v
          ),
        };
      });
    },
    [],
  );

  const setPOV = React.useCallback((pov: PointOfView) => {
    setState((prev) => {
      const existing = prev.views.filter((view) => view.pov === pov);
      if (!existing.length) {
        const template = createDefaultView(pov);
        return { ...prev, pov, views: [...prev.views, template, createSharedTemplate(pov)], currentViewId: template.id, lastViewByPOV: { ...prev.lastViewByPOV, [pov]: template.id }, editMode: false };
      }
      const remembered = prev.lastViewByPOV[pov];
      const selected = existing.find((view) => view.id === remembered) ?? existing.find((view) => view.isDefault) ?? existing[0]!;
      return { ...prev, pov, currentViewId: selected.id, lastViewByPOV: { ...prev.lastViewByPOV, [pov]: selected.id }, editMode: false };
    });
  }, []);

  const addWidget = React.useCallback(
    (type: string) => {
      updateView((view) => {
        const def = getWidgetDefinition(type);
        if (!def) return view;
        const maxY = view.widgets.reduce((m, w) => Math.max(m, w.position.y + w.position.h), 0);
        return {
          ...view,
          widgets: [
            ...view.widgets,
            {
              id: wid(),
              type,
              title: def.name,
              config: {},
              position: { x: 0, y: maxY, w: def.defaultSize.w, h: def.defaultSize.h },
              collapsed: false,
              lastUpdated: null,
              loading: false,
              error: null,
            },
          ],
        };
      });
      setLibraryOpen(false);
    },
    [updateView],
  );

  const removeWidget = React.useCallback(
    (id: string) => updateView((v) => ({ ...v, widgets: v.widgets.filter((w) => w.id !== id) })),
    [updateView],
  );

  const duplicateWidget = React.useCallback(
    (id: string) => {
      updateView((view) => {
        const widget = view.widgets.find((w) => w.id === id);
        if (!widget) return view;
        const maxY = view.widgets.reduce((m, w) => Math.max(m, w.position.y + w.position.h), 0);
        return {
          ...view,
          widgets: [
            ...view.widgets,
            { ...widget, id: wid(), title: `${widget.title} (Copy)`, position: { ...widget.position, y: maxY } },
          ],
        };
      });
    },
    [updateView],
  );

  const updateConfig = React.useCallback(
    (id: string, config: WidgetConfig) => updateView((v) => ({ ...v, widgets: v.widgets.map((w) => (w.id === id ? { ...w, config } : w)) })),
    [updateView],
  );

  const toggleCollapse = React.useCallback(
    (id: string) => updateView((v) => ({ ...v, widgets: v.widgets.map((w) => (w.id === id ? { ...w, collapsed: !w.collapsed } : w)) })),
    [updateView],
  );

  const refreshWidget = React.useCallback(
    (id: string) => {
      setState((prev) => ({
        ...prev,
        views: prev.views.map((v) => ({
          ...v,
          widgets: v.widgets.map((w) => (w.id === id ? { ...w, loading: true } : w)),
        })),
      }));
      setTimeout(() => {
        setState((prev) => ({
          ...prev,
          lastUpdated: Date.now(),
          views: prev.views.map((v) => ({
            ...v,
            widgets: v.widgets.map((w) =>
              w.id === id ? { ...w, loading: false, lastUpdated: Date.now() } : w
            ),
          })),
        }));
      }, 800);
    },
    [],
  );

  const refreshAll = React.useCallback(() => {
    setState((prev) => ({
      ...prev,
      views: prev.views.map((v) => ({ ...v, widgets: v.widgets.map((w) => ({ ...w, loading: true })) })),
    }));
    setTimeout(() => {
      setState((prev) => ({
        ...prev,
        lastUpdated: Date.now(),
        views: prev.views.map((v) => ({
          ...v,
          widgets: v.widgets.map((w) => ({ ...w, loading: false, lastUpdated: Date.now() })),
        })),
      }));
    }, 1200);
  }, []);

  const resetDashboard = React.useCallback(() => {
    const newView = createDefaultView(state.pov);
    setState((prev) => ({
      ...prev,
      views: [...prev.views.filter((v) => v.id !== prev.currentViewId), { ...newView, id: prev.currentViewId, name: currentView.name, isDefault: currentView.isDefault }],
      currentViewId: prev.currentViewId,
      lastUpdated: Date.now(),
    }));
  }, [state.pov, currentView]);

  const onLayoutChange = React.useCallback(
    (widgets: WidgetInstance[]) => updateView((v) => ({ ...v, widgets })),
    [updateView],
  );

  const settingsWidget = settingsWidgetId
    ? currentView.widgets.find((w) => w.id === settingsWidgetId)
    : null;

  const handleMoveUp = (index: number) => {
    const expanded = currentView.widgets.filter((w) => !w.collapsed);
    if (index <= 0 || index >= expanded.length) return;
    const a = expanded[index];
    const b = expanded[index - 1];
    if (!a || !b) return;
    const posA = a.position;
    updateView((v) => ({
      ...v,
      widgets: v.widgets.map((w) => {
        if (w.id === a.id) return { ...w, position: { ...b.position } };
        if (w.id === b.id) return { ...w, position: { ...posA } };
        return w;
      }),
    }));
  };

  const handleMoveDown = (index: number) => {
    const expanded = currentView.widgets.filter((w) => !w.collapsed);
    if (index < 0 || index >= expanded.length - 1) return;
    const a = expanded[index];
    const b = expanded[index + 1];
    if (!a || !b) return;
    const posA = a.position;
    updateView((v) => ({
      ...v,
      widgets: v.widgets.map((w) => {
        if (w.id === a.id) return { ...w, position: { ...b.position } };
        if (w.id === b.id) return { ...w, position: { ...posA } };
        return w;
      }),
    }));
  };

  const createView = React.useCallback(
    (input: { name: string; type: DashboardType; startMode: DashboardStartMode; sourceViewId?: string }) => {
      setState((prev) => {
        const source = input.startMode === "duplicate" ? prev.views.find((view) => view.id === input.sourceViewId) : undefined;
        const template = input.startMode === "template" ? createDefaultView(prev.pov, input.name) : undefined;
        const newView: DashboardView = {
          id: `v-${Date.now()}`,
          name: input.name,
          description: input.startMode === "blank" ? "Blank dashboard" : input.startMode === "template" ? `${prev.pov} default template` : `Copy of ${source?.name ?? "dashboard"}`,
          pov: prev.pov,
          ownerId: "current-user",
          ownerName: "You",
          type: input.type,
          access: "manage-sharing",
          sharedWith: [],
          widgets: (template?.widgets ?? source?.widgets ?? []).map((widget) => ({ ...widget, id: wid(), position: { ...widget.position }, config: { ...widget.config } })),
          filters: { ...(template?.filters ?? source?.filters ?? { dateRange: "30d", portfolio: null, program: null, project: null }) },
          isDefault: false,
          createdAt: Date.now(),
          updatedAt: Date.now(),
        };
        return { ...prev, views: [...prev.views, newView], currentViewId: newView.id, lastViewByPOV: { ...prev.lastViewByPOV, [prev.pov]: newView.id } };
      });
    },
    [],
  );

  const switchView = React.useCallback((id: string) => setState((prev) => ({ ...prev, currentViewId: id, lastViewByPOV: { ...prev.lastViewByPOV, [prev.pov]: id }, editMode: false })), []);

  const renameView = React.useCallback(
    (id: string, name: string) => setState((prev) => ({ ...prev, views: prev.views.map((v) => (v.id === id ? { ...v, name } : v)) })),
    [],
  );

  const duplicateView = React.useCallback(
    (id: string) => {
      setState((prev) => {
        const view = prev.views.find((v) => v.id === id);
        if (!view) return prev;
        const dup: DashboardView = { ...view, id: `v-${Date.now()}`, name: `${view.name} (Copy)`, ownerId: "current-user", ownerName: "You", type: "personal", access: "manage-sharing", sharedWith: [], widgets: view.widgets.map((widget) => ({ ...widget, id: wid(), config: { ...widget.config }, position: { ...widget.position } })), isDefault: false, createdAt: Date.now(), updatedAt: Date.now() };
        return { ...prev, views: [...prev.views, dup], currentViewId: dup.id, lastViewByPOV: { ...prev.lastViewByPOV, [prev.pov]: dup.id } };
      });
    },
    [],
  );

  const deleteView = React.useCallback(
    (id: string) => {
      setState((prev) => {
        const scoped = prev.views.filter((view) => view.pov === prev.pov);
        if (scoped.length <= 1) return prev;
        const filtered = prev.views.filter((v) => v.id !== id);
        const fallback = filtered.find((view) => view.pov === prev.pov && view.isDefault) ?? filtered.find((view) => view.pov === prev.pov);
        return { ...prev, views: filtered, currentViewId: prev.currentViewId === id ? (fallback?.id ?? prev.currentViewId) : prev.currentViewId, lastViewByPOV: fallback ? { ...prev.lastViewByPOV, [prev.pov]: fallback.id } : prev.lastViewByPOV };
      });
    },
    [],
  );

  const setDefaultView = React.useCallback(
    (id: string) => setState((prev) => ({ ...prev, views: prev.views.map((v) => v.pov === prev.pov ? { ...v, isDefault: v.id === id } : v) })),
    [],
  );

  const updateSharing = React.useCallback(
    (id: string, type: DashboardType, access: DashboardAccess) => setState((prev) => ({ ...prev, views: prev.views.map((view) => view.id === id ? { ...view, type, access, updatedAt: Date.now() } : view) })),
    [],
  );

  const handleViewDetails = React.useCallback((type: string) => {
    const routes: Record<string, string> = {
      "my-todo": "/my-work/tasks",
      "my-timesheet-status": "/my-work/timesheets",
      "hours-this-week": "/my-work/timesheets",
      "missing-timesheets": "/my-work/timesheets",
      "timesheet-approval-inbox": "/my-work/approvals",
      "pending-approvals": "/my-work/approvals",
      "upcoming-milestones": "/projects/gantt",
      "overdue-tasks": "/my-work/tasks",
      "upcoming-meetings": "/meetings",
      "projects-attention": "/projects",
      "projects-by-health": "/projects",
      "projects-by-status": "/projects",
      "active-projects": "/projects",
      "projects-at-risk": "/projects",
      "budget-vs-actual": "/costs",
      "cost-variance": "/costs",
      "total-budget": "/costs",
      "forecast-variance": "/costs",
      "resource-utilization": "/resources",
      "resource-capacity": "/resources/capacity",
      "overallocated-resources": "/resources",
      "top-risks": "/risks-issues/risks",
      "risk-heat-map": "/risks-issues/risks",
      "open-risks": "/risks-issues/risks",
      "ai-recommendations": "/ai-workspace",
    };
    const href = routes[type];
    if (href) {
      window.location.href = href;
    }
  }, []);

  const widgetNodeCache = React.useRef(new Map<string, { signature: string; node: React.ReactNode }>());

  const widgetNodes = React.useMemo(() => {
    const map = new Map<string, React.ReactNode>();
    const liveIds = new Set<string>();
    currentView.widgets.forEach((w) => {
      liveIds.add(w.id);
      const def = getWidgetDefinition(w.type);
      const Component = getWidgetComponent(w.type);
      if (!def || !Component) return;
      const signature = JSON.stringify([
        w.id,
        w.type,
        w.title,
        w.collapsed,
        w.loading,
        w.error,
        w.lastUpdated,
        w.config,
        state.editMode,
      ]);
      const cached = widgetNodeCache.current.get(w.id);
      if (cached && cached.signature === signature) {
        map.set(w.id, cached.node);
        return;
      }
      const node = (
        <WidgetShell
          title={w.title || def.name}
          subtitle={def.description}
          lastUpdated={w.lastUpdated}
          loading={w.loading}
          error={w.error}
          collapsed={w.collapsed}
          editMode={state.editMode}
          dataSource={def.dataSourceLabel}
          onRefresh={() => refreshWidget(w.id)}
          onSettings={() => setSettingsWidgetId(w.id)}
          onDuplicate={() => duplicateWidget(w.id)}
          onRemove={() => removeWidget(w.id)}
          onToggleCollapse={() => toggleCollapse(w.id)}
          onViewDetails={() => handleViewDetails(w.type)}
        >
          <Component config={w.config} />
        </WidgetShell>
      );
      widgetNodeCache.current.set(w.id, { signature, node });
      map.set(w.id, node);
    });
    widgetNodeCache.current.forEach((_entry, id) => {
      if (!liveIds.has(id)) widgetNodeCache.current.delete(id);
    });
    return map;
  }, [currentView.widgets, state.editMode, refreshWidget, duplicateWidget, removeWidget, toggleCollapse, handleViewDetails]);

  const collapsedWidgets = currentView.widgets.filter((w) => w.collapsed);

  return (
    <div className="space-y-4">
      <DashboardHeader
        pov={state.pov}
        editMode={state.editMode}
        lastUpdated={state.lastUpdated}
        refreshing={false}
        viewName={currentView.name}
        viewCount={povViews.length}
        views={povViews}
        currentViewId={state.currentViewId}
        filters={currentView.filters}
        onPOVChange={setPOV}
        canEdit={canEditCurrent}
        dashboardType={currentView.type}
        access={currentView.access}
        onToggleEditMode={() => canEditCurrent && setState((prev) => ({ ...prev, editMode: !prev.editMode }))}
        onAddWidget={() => setLibraryOpen(true)}
        onRefreshAll={refreshAll}
        onSaveView={() => updateView((v) => v)}
        onManageViews={() => setViewsOpen(true)}
        onReset={resetDashboard}
        onFiltersChange={(filters: GlobalFilters) => updateView((view) => ({ ...view, filters }))}
        onSwitchView={switchView}
      />

      {isMobile ? (
        <div className="space-y-3">
          {currentView.widgets
            .filter((w) => !w.collapsed)
            .map((w, i) => (
              <div key={w.id}>
                {state.editMode && (
                  <div className="flex items-center justify-center gap-1 py-1 border-b border-border/20 bg-muted/10">
                    <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => handleMoveUp(i)} disabled={i === 0}>
                      <ArrowUp className="h-3 w-3" />
                    </Button>
                    <span className="text-[9px] text-muted-foreground">Reorder</span>
                    <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => handleMoveDown(i)} disabled={i === currentView.widgets.filter((we) => !we.collapsed).length - 1}>
                      <ArrowDown className="h-3 w-3" />
                    </Button>
                  </div>
                )}
                {widgetNodes.get(w.id)}
              </div>
            ))}
          {collapsedWidgets.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                Collapsed ({collapsedWidgets.length})
              </h3>
              {collapsedWidgets.map((w) => (
                <div key={w.id}>{widgetNodes.get(w.id)}</div>
              ))}
            </div>
          )}
        </div>
      ) : (
        <>
          <DashboardGrid
            widgets={currentView.widgets}
            cols={cols}
            rowHeight={120}
            width={windowWidth - 60}
            editMode={state.editMode && !isMobile}
            onLayoutChange={onLayoutChange}
            widgetNodes={widgetNodes}
          />
          {collapsedWidgets.length > 0 && (
            <div className="space-y-2 mt-4">
              <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                Collapsed ({collapsedWidgets.length})
              </h3>
              <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
                {collapsedWidgets.map((w) => (
                  <div key={w.id}>{widgetNodes.get(w.id)}</div>
                ))}
              </div>
            </div>
          )}
        </>
      )}

      {currentView.widgets.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20 text-muted-foreground">
          <Layout className="h-12 w-12 mb-3 opacity-20" />
          <p className="text-sm font-medium">No widgets on this dashboard</p>
          <p className="text-xs mt-1 mb-4">Add widgets to get started</p>
          <Button size="sm" onClick={() => setLibraryOpen(true)}>
            <Plus className="h-3.5 w-3.5 mr-1.5" />Add Widget
          </Button>
        </div>
      )}

      <WidgetLibrary
        open={libraryOpen}
        onClose={() => setLibraryOpen(false)}
        onAddWidget={addWidget}
        pov={state.pov}
      />

      {settingsWidget && (
        <WidgetSettings
          open={!!settingsWidgetId}
          onClose={() => setSettingsWidgetId(null)}
          widgetType={settingsWidget.type}
          widgetTitle={settingsWidget.title}
          config={settingsWidget.config}
          onSave={(config) => updateConfig(settingsWidget.id, config)}
        />
      )}

      <DashboardViewManager
        open={viewsOpen}
        onClose={() => setViewsOpen(false)}
        pov={state.pov}
        views={povViews}
        currentViewId={state.currentViewId}
        onSwitch={switchView}
        onCreate={createView}
        onRename={renameView}
        onDuplicate={duplicateView}
        onDelete={deleteView}
        onSetDefault={setDefaultView}
        onUpdateSharing={updateSharing}
      />
    </div>
  );
}
