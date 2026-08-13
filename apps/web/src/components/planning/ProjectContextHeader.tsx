"use client"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Progress } from "@/components/ui/progress"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"
import { formatDate } from "@/lib/utils"
import type { PlanTask } from "@/lib/plan-mock-data"
import {
  Save, Upload, FileSpreadsheet, Bot, Download, Undo2, Redo2, RefreshCw, MoreHorizontal,
  CalendarDays, Clock, User, FolderKanban, AlertTriangle
} from "lucide-react"

interface ProjectContextHeaderProps {
  project: {
    projectName: string
    projectCode: string
    portfolio: string
    program: string
    projectManager: string
    health: "green" | "amber" | "red"
    plannedStart: string
    plannedFinish: string
    forecastFinish: string
    progress: number
    baselineName: string
    lastUpdate: string
    scheduleSource: string
  }
  onAction?: (action: string) => void
}

export function ProjectContextHeader({ project, onAction }: ProjectContextHeaderProps) {
  const healthColor = {
    green: "bg-status-green",
    amber: "bg-status-amber",
    red: "bg-status-red",
  }
  const healthLabel = {
    green: "On Track",
    amber: "At Risk",
    red: "Critical",
  }

  return (
    <TooltipProvider>
      <div className="border-b bg-card px-4 py-2.5">
        <div className="flex flex-col gap-2 xl:flex-row xl:items-start xl:justify-between xl:gap-4">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <h2 className="truncate text-base font-semibold">{project.projectName}</h2>
              <Badge variant="outline" className="shrink-0 text-[10px] font-mono">{project.projectCode}</Badge>
              <span className={`inline-block h-2.5 w-2.5 shrink-0 rounded-full ${healthColor[project.health]}`} title={healthLabel[project.health]} />
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-muted-foreground">
              <span className="flex items-center gap-1"><FolderKanban className="h-3 w-3" />{project.portfolio}</span>
              <span className="flex items-center gap-1"><User className="h-3 w-3" />{project.projectManager}</span>
              <span className="flex items-center gap-1"><CalendarDays className="h-3 w-3" />Plan: {formatDate(project.plannedFinish)}</span>
              <span className="flex items-center gap-1"><Clock className="h-3 w-3" />Forecast: {formatDate(project.forecastFinish)}</span>
              <span className="flex items-center gap-1"><AlertTriangle className="h-3 w-3" />{project.baselineName}</span>
              <span className="text-muted-foreground/60">Updated: {formatDate(project.lastUpdate)}</span>
              <Badge variant="outline" className="text-[10px]">{project.scheduleSource}</Badge>
            </div>
            <div className="mt-1.5 flex items-center gap-2">
              <div className="flex-1 max-w-[200px]">
                <Progress value={project.progress} className="h-1.5" />
              </div>
              <span className="text-[11px] font-medium tabular-nums">{project.progress}%</span>
            </div>
          </div>
          <div className="flex max-w-full shrink-0 flex-wrap items-center gap-1">
            <Tooltip><TooltipTrigger asChild><Button aria-label="Save plan" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("save")}><Save className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Save</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="Publish plan" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("publish")}><Upload className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Publish Plan</TooltipContent></Tooltip>
            <div className="mx-0.5 h-5 w-px bg-border" />
            <Tooltip><TooltipTrigger asChild><Button aria-label="Set baseline" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("baseline")}><FileSpreadsheet className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Set Baseline</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="Import Microsoft Project" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("import")}><Download className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Import Microsoft Project</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="Generate plan with AI" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("aiDraft")}><Bot className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Generate Draft Plan with AI</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="Export plan" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("export")}><Download className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Export</TooltipContent></Tooltip>
            <div className="mx-0.5 h-5 w-px bg-border" />
            <Tooltip><TooltipTrigger asChild><Button aria-label="Undo" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("undo")}><Undo2 className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Undo</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="Redo" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("redo")}><Redo2 className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Redo</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="Refresh plan" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("refresh")}><RefreshCw className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>Refresh</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button aria-label="More plan actions" variant="ghost" size="icon" className="h-7 w-7" onClick={() => onAction?.("more")}><MoreHorizontal className="h-3.5 w-3.5" /></Button></TooltipTrigger><TooltipContent>More Actions</TooltipContent></Tooltip>
          </div>
        </div>
      </div>
    </TooltipProvider>
  )
}
