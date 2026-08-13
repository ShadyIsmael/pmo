"use client"

import type { ReactNode } from "react"
import type { LucideIcon } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Separator } from "@/components/ui/separator"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"
import {
  Plus, ListTree, FileSymlink, Diamond, Trash2, Copy, Indent, Outdent,
  ArrowUp, ArrowDown, Link2, Unlink, Route, BarChart3, Users, ZoomIn, ZoomOut,
  Maximize2, ChevronsUpDown, Columns, Filter, GripVertical,
} from "lucide-react"

interface PlanningToolbarProps {
  onAction: (action: string) => void
  zoom: number
  onZoomChange: (zoom: number) => void
  view: string
  showCritical?: boolean
  showBaseline?: boolean
  selectedCount?: number
}

interface ToolButtonProps {
  label: string
  icon: LucideIcon
  onClick: () => void
  active?: boolean
}

function ToolButton({ label, icon: Icon, onClick, active = false }: ToolButtonProps) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          type="button"
          variant={active ? "secondary" : "ghost"}
          size="icon"
          aria-label={label}
          title={label}
          className="h-7 w-7 shrink-0"
          onClick={onClick}
        >
          <Icon className="h-3.5 w-3.5" />
        </Button>
      </TooltipTrigger>
      <TooltipContent side="bottom">{label}</TooltipContent>
    </Tooltip>
  )
}

function ToolGroup({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div role="group" aria-label={label} className="flex items-center gap-0.5">
      {children}
    </div>
  )
}

export function PlanningToolbar({ onAction, zoom, onZoomChange, view, showCritical = false, showBaseline = false, selectedCount = 0 }: PlanningToolbarProps) {
  return (
    <TooltipProvider>
      <div
        role="toolbar"
        aria-label="Planning toolbar"
        className="flex max-h-20 flex-wrap items-center gap-0.5 overflow-y-auto border-b bg-muted/30 px-2 py-1 text-[11px]"
      >
        <ToolGroup label="Task actions">
          <ToolButton label="Add task" icon={Plus} onClick={() => onAction("addTask")} />
          <ToolButton label="Add subtask" icon={ListTree} onClick={() => onAction("addSubtask")} />
          <ToolButton label="Add summary task" icon={FileSymlink} onClick={() => onAction("addSummary")} />
          <ToolButton label="Add milestone" icon={Diamond} onClick={() => onAction("addMilestone")} />
          <Separator orientation="vertical" className="mx-0.5 h-5" />
          <ToolButton label="Delete selected task" icon={Trash2} onClick={() => onAction("delete")} />
          <ToolButton label="Duplicate selected task" icon={Copy} onClick={() => onAction("duplicate")} />
        </ToolGroup>

        <Separator orientation="vertical" className="mx-1 h-5" />

        <ToolGroup label="Task hierarchy">
          <ToolButton label="Indent task" icon={Indent} onClick={() => onAction("indent")} />
          <ToolButton label="Outdent task" icon={Outdent} onClick={() => onAction("outdent")} />
          <ToolButton label="Move task up" icon={ArrowUp} onClick={() => onAction("moveUp")} />
          <ToolButton label="Move task down" icon={ArrowDown} onClick={() => onAction("moveDown")} />
        </ToolGroup>

        <Separator orientation="vertical" className="mx-1 h-5" />

        <ToolGroup label="Schedule actions">
          <ToolButton label="Link tasks" icon={Link2} onClick={() => onAction("link")} />
          <ToolButton label="Unlink tasks" icon={Unlink} onClick={() => onAction("unlink")} />
          <ToolButton label="Assign resources" icon={Users} onClick={() => onAction("assignResources")} />
        </ToolGroup>

        <Separator orientation="vertical" className="mx-1 h-5" />

        <ToolGroup label="Analysis views">
          <ToolButton label="Gantt view" icon={GripVertical} active={view === "gantt"} onClick={() => onAction("viewGantt")} />
          <ToolButton label="Milestones view" icon={Diamond} active={view === "milestones"} onClick={() => onAction("viewMilestones")} />
          <ToolButton label="Critical path view" icon={Route} active={view === "criticalPath"} onClick={() => onAction("viewCriticalPath")} />
          <ToolButton label="Baseline comparison view" icon={BarChart3} active={view === "baseline"} onClick={() => onAction("viewBaseline")} />
          <ToolButton label="Resource usage view" icon={Users} active={view === "resources"} onClick={() => onAction("viewResources")} />
        </ToolGroup>

        <Separator orientation="vertical" className="mx-1 h-5" />

        <ToolGroup label="Display actions">
          <ToolButton label="Zoom in" icon={ZoomIn} onClick={() => onZoomChange(Math.min(zoom + 0.25, 2))} />
          <ToolButton label="Zoom out" icon={ZoomOut} onClick={() => onZoomChange(Math.max(zoom - 0.25, 0.25))} />
          <span className="w-9 text-center text-[10px] tabular-nums text-muted-foreground" aria-live="polite">{Math.round(zoom * 100)}%</span>
          <ToolButton label="Fit project timeline" icon={Maximize2} onClick={() => onAction("fitProject")} />
          <ToolButton label="Highlight critical tasks" icon={Route} active={showCritical} onClick={() => onAction("toggleCritical")} />
          <ToolButton label="Show baseline bars" icon={BarChart3} active={showBaseline} onClick={() => onAction("toggleBaseline")} />
          <ToolButton label="Expand all tasks" icon={ChevronsUpDown} onClick={() => onAction("expandAll")} />
          <ToolButton label="Collapse all tasks" icon={ChevronsUpDown} onClick={() => onAction("collapseAll")} />
          <ToolButton label="Choose columns" icon={Columns} onClick={() => onAction("columnChooser")} />
          <ToolButton label="Toggle filters" icon={Filter} onClick={() => onAction("filter")} />
        </ToolGroup>
        {selectedCount > 0 && (
          <span className="ms-auto whitespace-nowrap rounded bg-primary/10 px-2 py-1 text-[10px] font-medium text-primary" aria-live="polite">
            {selectedCount} selected
          </span>
        )}
      </div>
    </TooltipProvider>
  )
}
