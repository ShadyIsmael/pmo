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
    <Card className="nexus-widget h-full overflow-hidden flex flex-col">
      <CardHeader className="flex flex-row items-center justify-between py-1.5 px-3 shrink-0 gap-2">
        <div className="flex items-center gap-2 min-w-0 flex-1">
          {editMode && (
            <div className="cursor-grab active:cursor-grabbing drag-handle text-muted-foreground/40 hover:text-muted-foreground/70 transition-colors">
              <GripVertical className="h-3.5 w-3.5" />
            </div>
          )}
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-1.5">
              {collapsed && onToggleCollapse && (
                <Button variant="ghost" size="icon" className="h-4 w-4 -ml-1" onClick={onToggleCollapse}>
                  <ChevronRight className="h-3 w-3" />
                </Button>
              )}
              <CardTitle className="text-[11px] font-semibold uppercase tracking-wider truncate flex items-center gap-1.5">
                {title}
                {loading && <Loader2 className="h-3 w-3 animate-spin" />}
              </CardTitle>
            </div>
            {subtitle && !collapsed && (
              <p className="text-[9px] text-muted-foreground/60 truncate mt-0.5">{subtitle}</p>
            )}
          </div>
        </div>

        <div className="flex items-center gap-0.5 shrink-0">
          {dataSource && !collapsed && (
            <span className="text-[8px] text-muted-foreground/30 mr-0.5 hidden sm:inline" title={dataSource}>
              <Eye className="h-2.5 w-2.5 inline mr-0.5" />
            </span>
          )}
          {lastUpdated && !collapsed && (
            <span className="text-[8px] text-muted-foreground/40 mr-0.5 hidden sm:inline">
              {new Date(lastUpdated).toLocaleTimeString()}
            </span>
          )}
          {onRefresh && (
            <Button variant="ghost" size="icon" className="h-5 w-5" onClick={onRefresh} disabled={loading}>
              <RefreshCw className={`h-3 w-3 ${loading ? "animate-spin" : ""}`} />
            </Button>
          )}
          {onViewDetails && !editMode && (
            <Button variant="ghost" size="icon" className="h-5 w-5" onClick={onViewDetails}>
              <ExternalLink className="h-3 w-3" />
            </Button>
          )}
          {editMode && onSettings && (
            <Button variant="ghost" size="icon" className="h-5 w-5" onClick={onSettings}>
              <Settings className="h-3 w-3" />
            </Button>
          )}
          {editMode && onDuplicate && (
            <Button variant="ghost" size="icon" className="h-5 w-5" onClick={onDuplicate}>
              <Copy className="h-3 w-3" />
            </Button>
          )}
          {!collapsed && onToggleCollapse && (
            <Button variant="ghost" size="icon" className="h-5 w-5" onClick={onToggleCollapse}>
              <ChevronDown className="h-3 w-3" />
            </Button>
          )}
          {editMode && onRemove && (
            <Button variant="ghost" size="icon" className="h-5 w-5 text-destructive hover:text-destructive" onClick={onRemove}>
              <X className="h-3 w-3" />
            </Button>
          )}
        </div>
      </CardHeader>
      {!collapsed && (
        <CardContent className="flex-1 overflow-auto p-2 pt-1 min-h-0">
          {error ? (
            <div className="flex flex-col items-center justify-center h-full gap-2 text-muted-foreground">
              <AlertCircle className="h-5 w-5 text-destructive" />
              <p className="text-xs">{error}</p>
              {onRefresh && <Button variant="outline" size="sm" className="h-7 text-xs" onClick={onRefresh}>Retry</Button>}
            </div>
          ) : loading ? (
            <div className="flex items-center justify-center h-full">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : (
            children
          )}
        </CardContent>
      )}
    </Card>
  );
}
