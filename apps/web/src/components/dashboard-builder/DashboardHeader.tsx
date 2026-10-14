"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  RefreshCw, Plus, Pencil, Save, Layout, RotateCcw, EyeOff,
  Layers,
} from "lucide-react";
import type { PointOfView, GlobalFilters, DashboardView, DashboardAccess, DashboardType } from "./dashboard-types";
import { POV_OPTIONS, POV_LABELS, POV_DESCRIPTIONS } from "./default-layouts";
import { DashboardFilters } from "./DashboardFilters";

interface DashboardHeaderProps {
  pov: PointOfView;
  povOptions?: PointOfView[];
  editMode: boolean;
  lastUpdated: number | null;
  refreshing: boolean;
  viewName: string;
  viewCount: number;
  views: DashboardView[];
  currentViewId: string;
  canEdit: boolean;
  dashboardType: DashboardType;
  access: DashboardAccess;
  filters: GlobalFilters;
  onPOVChange: (pov: PointOfView) => void;
  onToggleEditMode: () => void;
  onAddWidget: () => void;
  onRefreshAll: () => void;
  onSaveView: () => void;
  onManageViews: () => void;
  onReset: () => void;
  onFiltersChange: (filters: GlobalFilters) => void;
  onSwitchView: (id: string) => void;
}

export function DashboardHeader({
  pov, povOptions, editMode, lastUpdated, refreshing, viewName, viewCount,
  views, currentViewId, canEdit, dashboardType, access, filters, onPOVChange, onToggleEditMode, onAddWidget,
  onRefreshAll, onSaveView, onManageViews, onReset, onFiltersChange, onSwitchView,
}: DashboardHeaderProps) {
  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-1">
          <div className="flex items-center gap-2.5">
            <h1 className="text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">{viewName}</h1>
            <span className="text-xs font-semibold uppercase tracking-wide text-slate-500 bg-slate-100 px-2 py-0.5 rounded-full">{dashboardType}</span>
            {!canEdit && <Badge variant="secondary" className="h-6 text-[10px] font-medium bg-slate-100 text-slate-600 border-slate-200">{access === "duplicate" ? "Can duplicate" : "View only"}</Badge>}
            {editMode && (
              <Badge variant="default" className="bg-amber-500 text-white hover:bg-amber-600 text-[10px] font-medium h-6 px-2.5">
                <Pencil className="h-3 w-3 mr-1" />
                Editing
              </Badge>
            )}
          </div>
          <p className="text-sm text-slate-500">{POV_LABELS[pov]} · {POV_DESCRIPTIONS[pov]}</p>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <Select value={pov} onValueChange={(v) => onPOVChange(v as PointOfView)}>
            <SelectTrigger className="h-8 w-[140px] text-xs font-medium border-slate-200">
              <Layers className="h-3.5 w-3.5 mr-1.5 text-slate-500" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(povOptions ?? POV_OPTIONS).map((opt) => (
                <SelectItem key={opt} value={opt} className="text-xs font-medium">{opt}</SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={currentViewId} onValueChange={onSwitchView}>
            <SelectTrigger className="h-8 w-[150px] text-xs font-medium border-slate-200">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {views.map((v) => (
                <SelectItem key={v.id} value={v.id} className="text-xs font-medium">
                  {v.name} {v.isDefault ? "★" : ""}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {lastUpdated && (
            <span className="hidden sm:block text-[10px] text-slate-400 mr-1">
              Updated {new Date(lastUpdated).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </span>
          )}

          <Button variant="ghost" size="icon" className="h-8 w-8 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onRefreshAll} disabled={refreshing}>
            <RefreshCw className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`} />
          </Button>

          <Button variant={editMode ? "default" : "outline"} size="sm" className="h-8 text-xs font-medium gap-1.5" onClick={onToggleEditMode} disabled={!canEdit}>
            {editMode ? <EyeOff className="h-3.5 w-3.5" /> : <Pencil className="h-3.5 w-3.5" />}
            {editMode ? "View" : "Edit"}
          </Button>

          {!editMode && canEdit && (
            <Button variant="outline" size="sm" className="h-8 text-xs font-medium gap-1.5 border-slate-200 hover:bg-slate-50" onClick={onAddWidget}>
              <Plus className="h-3.5 w-3.5" />Add Widget
            </Button>
          )}

          {!editMode && canEdit && (
            <Button variant="outline" size="sm" className="h-8 text-xs font-medium gap-1.5 border-slate-200 hover:bg-slate-50" onClick={onManageViews}>
              <Layout className="h-3.5 w-3.5" />Views ({viewCount})
            </Button>
          )}

          {editMode && (
            <>
              <Button variant="outline" size="sm" className="h-8 text-xs font-medium gap-1.5 border-slate-200 hover:bg-slate-50" onClick={onSaveView}>
                <Save className="h-3.5 w-3.5" />Save
              </Button>
              <Button variant="outline" size="sm" className="h-8 text-xs font-medium gap-1.5 border-slate-200 hover:bg-slate-50" onClick={onManageViews}>
                <Layout className="h-3.5 w-3.5" />{viewCount}
              </Button>
              <Button variant="outline" size="sm" className="h-8 text-xs font-medium gap-1.5 text-red-600 border-red-200 hover:bg-red-50 hover:text-red-700" onClick={onReset}>
                <RotateCcw className="h-3.5 w-3.5" />Reset
              </Button>
            </>
          )}
        </div>
      </div>

      <DashboardFilters
        filters={filters}
        onFiltersChange={onFiltersChange}
        compact={false}
      />
    </div>
  );
}
