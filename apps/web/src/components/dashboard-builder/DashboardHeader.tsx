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
    <div className="space-y-2">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-0.5">
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-bold tracking-tight sm:text-xl">{viewName}</h1>
            <span className="text-xs font-medium capitalize text-info">{dashboardType}</span>
            {!canEdit && <Badge variant="secondary" className="h-5 text-[9px]">{access === "duplicate" ? "Can duplicate" : "View only"}</Badge>}
            {editMode && (
              <Badge variant="default" className="bg-warning text-warning-foreground hover:bg-warning text-[9px] h-5">
                <Pencil className="h-2.5 w-2.5 mr-0.5" />
                Editing
              </Badge>
            )}
          </div>
          <p className="text-xs text-muted-foreground/80">{POV_LABELS[pov]} · {POV_DESCRIPTIONS[pov]}</p>
        </div>

        <div className="flex items-center gap-1.5 flex-wrap">
          <Select value={pov} onValueChange={(v) => onPOVChange(v as PointOfView)}>
            <SelectTrigger className="h-7 w-[130px] text-[10px]">
              <Layers className="h-3 w-3 mr-1" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(povOptions ?? POV_OPTIONS).map((opt) => (
                <SelectItem key={opt} value={opt} className="text-xs">{opt}</SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={currentViewId} onValueChange={onSwitchView}>
            <SelectTrigger className="h-7 w-[140px] text-[10px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {views.map((v) => (
                <SelectItem key={v.id} value={v.id} className="text-xs">
                  {v.name} {v.isDefault ? "★" : ""}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {lastUpdated && (
            <span className="hidden sm:block text-[9px] text-muted-foreground/50 mr-1">
              {new Date(lastUpdated).toLocaleTimeString()}
            </span>
          )}

          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={onRefreshAll} disabled={refreshing}>
            <RefreshCw className={`h-3.5 w-3.5 ${refreshing ? "animate-spin" : ""}`} />
          </Button>

          <Button variant={editMode ? "default" : "outline"} size="sm" className="h-7 text-[10px] gap-1" onClick={onToggleEditMode} disabled={!canEdit}>
            {editMode ? <EyeOff className="h-3 w-3" /> : <Pencil className="h-3 w-3" />}
            {editMode ? "View" : "Edit"}
          </Button>

          {!editMode && canEdit && (
            <Button variant="outline" size="sm" className="h-7 text-[10px] gap-1" onClick={onAddWidget}>
              <Plus className="h-3 w-3" />Add
            </Button>
          )}

          {!editMode && canEdit && (
            <Button variant="outline" size="sm" className="h-7 text-[10px] gap-1" onClick={onManageViews}>
              <Layout className="h-3 w-3" />Manage ({viewCount})
            </Button>
          )}

          {editMode && (
            <>
              <Button variant="outline" size="sm" className="h-7 text-[10px] gap-1" onClick={onSaveView}>
                <Save className="h-3 w-3" />Save
              </Button>
              <Button variant="outline" size="sm" className="h-7 text-[10px] gap-1" onClick={onManageViews}>
                <Layout className="h-3 w-3" />{viewCount}
              </Button>
              <Button variant="outline" size="sm" className="h-7 text-[10px] gap-1 text-destructive hover:text-destructive" onClick={onReset}>
                <RotateCcw className="h-3 w-3" />Reset
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
