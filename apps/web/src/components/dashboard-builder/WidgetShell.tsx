"use client";

import * as React from "react";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  RefreshCw, Settings, Copy, X, ChevronDown, ChevronRight,
  GripVertical, AlertCircle, Loader2, Eye, ExternalLink,
} from "lucide-react";

interface WidgetShellProps {
  title: string;
  subtitle?: string;
  lastUpdated?: number | null;
  loading?: boolean;
  error?: string | null;
  collapsed?: boolean;
  editMode?: boolean;
  dataSource?: string;
  onRefresh?: () => void;
  onSettings?: () => void;
  onDuplicate?: () => void;
  onRemove?: () => void;
  onToggleCollapse?: () => void;
  onViewDetails?: () => void;
  children: React.ReactNode;
}

export function WidgetShell({
  title, subtitle, lastUpdated, loading, error, collapsed, editMode,
  dataSource, onRefresh, onSettings, onDuplicate, onRemove, onToggleCollapse, onViewDetails, children,
}: WidgetShellProps) {
  return (
    <Card className="nexus-widget h-full overflow-hidden flex flex-col border-slate-200/60 bg-white hover:border-slate-300 hover:shadow-md transition-all duration-200">
      <CardHeader className="flex flex-row items-center justify-between py-3 px-4 shrink-0 gap-3 border-b border-slate-100">
        <div className="flex items-center gap-2.5 min-w-0 flex-1">
          {editMode && (
            <div className="cursor-grab active:cursor-grabbing drag-handle text-slate-400 hover:text-slate-600 transition-colors p-1 rounded hover:bg-slate-100">
              <GripVertical className="h-4 w-4" />
            </div>
          )}
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              {collapsed && onToggleCollapse && (
                <Button variant="ghost" size="icon" className="h-6 w-6 -ml-1.5 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onToggleCollapse}>
                  <ChevronRight className="h-4 w-4" />
                </Button>
              )}
              <CardTitle className="text-xs font-semibold uppercase tracking-wide text-slate-700 truncate flex items-center gap-2">
                {title}
                {loading && <Loader2 className="h-3.5 w-3.5 animate-spin text-slate-400" />}
              </CardTitle>
            </div>
            {subtitle && !collapsed && (
              <p className="text-[11px] text-slate-500 truncate mt-0.5">{subtitle}</p>
            )}
          </div>
        </div>

        <div className="flex items-center gap-1 shrink-0">
          {dataSource && !collapsed && (
            <span className="text-[10px] text-slate-400 mr-1 hidden sm:inline-flex items-center gap-1" title={dataSource}>
              <Eye className="h-3 w-3" />
            </span>
          )}
          {lastUpdated && !collapsed && (
            <span className="text-[10px] text-slate-400 mr-1 hidden sm:inline">
              {new Date(lastUpdated).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </span>
          )}
          {onRefresh && (
            <Button variant="ghost" size="icon" className="h-7 w-7 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onRefresh} disabled={loading}>
              <RefreshCw className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
            </Button>
          )}
          {onViewDetails && !editMode && (
            <Button variant="ghost" size="icon" className="h-7 w-7 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onViewDetails}>
              <ExternalLink className="h-3.5 w-3.5" />
            </Button>
          )}
          {editMode && onSettings && (
            <Button variant="ghost" size="icon" className="h-7 w-7 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onSettings}>
              <Settings className="h-3.5 w-3.5" />
            </Button>
          )}
          {editMode && onDuplicate && (
            <Button variant="ghost" size="icon" className="h-7 w-7 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onDuplicate}>
              <Copy className="h-3.5 w-3.5" />
            </Button>
          )}
          {!collapsed && onToggleCollapse && (
            <Button variant="ghost" size="icon" className="h-7 w-7 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={onToggleCollapse}>
              <ChevronDown className="h-3.5 w-3.5" />
            </Button>
          )}
          {editMode && onRemove && (
            <Button variant="ghost" size="icon" className="h-7 w-7 text-red-500 hover:text-red-600 hover:bg-red-50" onClick={onRemove}>
              <X className="h-3.5 w-3.5" />
            </Button>
          )}
        </div>
      </CardHeader>
      {!collapsed && (
        <CardContent className="flex-1 overflow-auto p-4 min-h-0">
          {error ? (
            <div className="flex flex-col items-center justify-center h-full gap-3 text-slate-500">
              <div className="w-10 h-10 rounded-full bg-red-50 flex items-center justify-center">
                <AlertCircle className="h-5 w-5 text-red-500" />
              </div>
              <p className="text-xs font-medium">{error}</p>
              {onRefresh && <Button variant="outline" size="sm" className="h-8 text-xs border-slate-200 hover:bg-slate-50" onClick={onRefresh}>Retry</Button>}
            </div>
          ) : loading ? (
            <div className="flex items-center justify-center h-full">
              <div className="flex flex-col items-center gap-2">
                <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
                <span className="text-xs text-slate-400">Loading...</span>
              </div>
            </div>
          ) : (
            children
          )}
        </CardContent>
      )}
    </Card>
  );
}
