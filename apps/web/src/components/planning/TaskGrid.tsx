"use client"

import { useState, useCallback, useMemo, useRef, useEffect, useLayoutEffect } from "react"
import { cn } from "@/lib/utils"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { Checkbox } from "@/components/ui/checkbox"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"
import type { PlanTask } from "@/lib/plan-mock-data"
import {
  ChevronRight, ChevronDown, Diamond, GripVertical, Clock, AlertTriangle,
  Link2, User, CalendarDays, FileText, ExternalLink, Bug,
} from "lucide-react"

export interface ColumnDef {
  id: string
  label: string
  width: number
  visible: boolean
  frozen?: boolean
  align?: "left" | "center" | "right"
  render: (task: PlanTask) => React.ReactNode
}

interface TaskGridProps {
  tasks: PlanTask[]
  expandedIds: Set<string>
  selectedId: string | null
  selectedIds: ReadonlySet<string>
  onSelectTask: (id: string, options?: { additive?: boolean; range?: boolean; visibleIds?: string[] }) => void
  onOpenTaskDetails: (id: string) => void
  onToggleExpand: (id: string) => void
  onTaskChange: (task: PlanTask) => void
  onIndent: (id: string) => void
  onOutdent: (id: string) => void
  onAddTask: (parentId: string | null) => void
  onDeleteTask: (id: string) => void
  columns: ColumnDef[]
  onAddSubtask: (parentId: string) => void
  onAddMilestone: (parentId: string | null) => void
  onReorderTasks: (taskId: string, direction: "up" | "down") => void
}

const typeIcon: Record<string, React.ReactNode> = {
  Milestone: <Diamond className="h-3 w-3 text-amber-500" />,
  Summary: <FileText className="h-3 w-3 text-indigo-500" />,
  ApprovalGate: <CheckIcon className="h-3 w-3 text-emerald-500" />,
  Meeting: <Clock className="h-3 w-3 text-teal-500" />,
  JiraLinked: <ExternalLink className="h-3 w-3 text-purple-500" />,
  TicketLinked: <Bug className="h-3 w-3 text-pink-500" />,
}

function CheckIcon(props: { className?: string }) {
  return (
    <svg className={props.className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

function getDepth(task: PlanTask, allTasks: PlanTask[]): number {
  let depth = 0
  let current = task
  while (current.parentId) {
    depth++
    const parent = allTasks.find(t => t.id === current.parentId)
    if (!parent) break
    current = parent
  }
  return depth
}

function getStatusColor(status: string): string {
  switch (status) {
    case "Completed": return "bg-status-green/10 text-status-green border-status-green/20"
    case "In Progress": return "bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-950 dark:text-blue-400 dark:border-blue-800"
    case "Delayed": return "bg-status-red/10 text-status-red border-status-red/20"
    case "On Hold": return "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-950 dark:text-amber-400 dark:border-amber-800"
    default: return "bg-muted text-muted-foreground border-border"
  }
}

export function TaskGrid({
  tasks,
  expandedIds,
  selectedId,
  selectedIds,
  onSelectTask,
  onOpenTaskDetails,
  onToggleExpand,
  onTaskChange,
  onIndent,
  onOutdent,
  onAddTask,
  onDeleteTask,
  columns,
  onAddSubtask,
  onAddMilestone,
  onReorderTasks,
}: TaskGridProps) {
  const [editingField, setEditingField] = useState<{ id: string; field: string } | null>(null)
  const [editValue, setEditValue] = useState("")
  const [contextMenu, setContextMenu] = useState<{ id: string; x: number; y: number } | null>(null)
  const [contextMenuPosition, setContextMenuPosition] = useState<{ x: number; y: number } | null>(null)
  const contextMenuRef = useRef<HTMLDivElement>(null)
  const editRef = useRef<HTMLInputElement>(null)
  const [dragOverId, setDragOverId] = useState<string | null>(null)
  const [dragId, setDragId] = useState<string | null>(null)

  const visibleTasks = useMemo(() => {
    const result: PlanTask[] = []
    const walk = (tasks: PlanTask[]) => {
      for (const task of tasks) {
        result.push(task)
        if (task.children.length > 0 && expandedIds.has(task.id)) {
          walk(task.children)
        }
      }
    }
    walk(tasks)
    return result
  }, [tasks, expandedIds])

  const handleStartEdit = (id: string, field: string, value: string) => {
    setEditingField({ id, field })
    setEditValue(value)
  }

  const handleFinishEdit = useCallback((task: PlanTask) => {
    if (!editingField) return
    const updated = { ...task }
    const field = editingField.field
    switch (field) {
      case "name": updated.name = editValue; break
      case "duration": updated.duration = editValue; break
      case "start": updated.start = editValue; break
      case "finish": updated.finish = editValue; break
      case "progress": updated.progress = Math.max(0, Math.min(100, Number(editValue) || 0)); break
      case "owner": updated.owner = editValue; break
    }
    onTaskChange(updated)
    setEditingField(null)
  }, [editingField, editValue, onTaskChange])

  const handleKeyDown = useCallback((e: React.KeyboardEvent, task: PlanTask) => {
    if (e.key === "Enter") {
      handleFinishEdit(task)
    } else if (e.key === "Escape") {
      setEditingField(null)
    }
  }, [handleFinishEdit])

  useEffect(() => {
    if (editingField && editRef.current) {
      editRef.current.focus()
      editRef.current.select()
    }
  }, [editingField])

  useEffect(() => {
    const handleClick = () => setContextMenu(null)
    document.addEventListener("click", handleClick)
    return () => document.removeEventListener("click", handleClick)
  }, [])

  useLayoutEffect(() => {
    if (!contextMenu || !contextMenuRef.current) {
      setContextMenuPosition(null)
      return
    }

    const menu = contextMenuRef.current
    const edgePadding = 8
    const maxX = window.innerWidth - menu.offsetWidth - edgePadding
    const maxY = window.innerHeight - menu.offsetHeight - edgePadding

    setContextMenuPosition({
      x: Math.max(edgePadding, Math.min(contextMenu.x, maxX)),
      y: Math.max(edgePadding, Math.min(contextMenu.y, maxY)),
    })
  }, [contextMenu])

  useEffect(() => {
    if (contextMenu) {
      contextMenuRef.current?.querySelector<HTMLButtonElement>("button")?.focus()
    }
  }, [contextMenu])

  const handleContextMenu = (e: React.MouseEvent, id: string) => {
    e.preventDefault()
    if (!selectedIds.has(id)) onSelectTask(id)
    setContextMenu({ id, x: e.clientX, y: e.clientY })
  }

  const handleDragStart = (id: string) => {
    setDragId(id)
  }

  const handleDragOver = (e: React.DragEvent, id: string) => {
    e.preventDefault()
    setDragOverId(id)
  }

  const handleDragEnd = () => {
    setDragId(null)
    setDragOverId(null)
  }

  const handleDrop = (targetId: string) => {
    if (dragId && dragId !== targetId) {
      const idx = visibleTasks.findIndex(t => t.id === dragId)
      const tgtIdx = visibleTasks.findIndex(t => t.id === targetId)
      if (idx >= 0 && tgtIdx >= 0 && idx !== tgtIdx) {
        if (tgtIdx < idx) onReorderTasks(dragId, "up")
        else onReorderTasks(dragId, "down")
      }
    }
    setDragId(null)
    setDragOverId(null)
  }

  return (
    <TooltipProvider>
      <div
        className="h-full"
        role="grid"
        aria-label="Project tasks. Use Ctrl or Command-click to select multiple tasks, or Shift-click to select a range."
      >
        {/* Column Headers */}
        <div className="sticky top-0 z-20 flex min-h-12 border-b bg-muted/80 text-[11px] font-medium text-muted-foreground backdrop-blur">
          {columns.filter(c => c.visible).map(col => (
            <div
              key={col.id}
              className={cn(
                "flex items-center gap-1 border-r px-2 py-1.5 shrink-0",
                col.align === "right" ? "justify-end" : col.align === "center" ? "justify-center" : "justify-start"
              )}
              style={{ width: col.width, minWidth: col.width }}
            >
              {col.id === "name" && (
                <div className="flex items-center gap-1">
                  <span className="text-[10px] font-mono text-muted-foreground/50">#</span>
                </div>
              )}
              <span className="truncate">{col.label}</span>
            </div>
          ))}
        </div>

        {/* Task Rows */}
        <div className="divide-y divide-border/50">
          {visibleTasks.length === 0 && (
            <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
              No tasks to display. Click "Add Task" to create one.
            </div>
          )}
          {visibleTasks.map((task) => {
            const depth = getDepth(task, visibleTasks)
            const isExpanded = expandedIds.has(task.id)
            const isSelected = selectedIds.has(task.id)
            const hasChildren = task.children.length > 0
            const isSummary = task.type === "Summary"

            return (
              <div
                key={task.id}
                className={cn(
                  "flex min-h-8 text-[11px] transition-colors",
                  isSelected ? "bg-primary/5 ring-1 ring-inset ring-primary/30" : "hover:bg-muted/30",
                  task.type === "Summary" && "font-medium",
                  task.type === "Milestone" && "",
                  task.isCritical && "border-l-2 border-status-red/40",
                  task.status === "Delayed" && "bg-status-red/[0.02]",
                  dragOverId === task.id && "border-t-2 border-primary",
                )}
                role="row"
                tabIndex={0}
                aria-selected={isSelected}
                aria-current={selectedId === task.id ? "true" : undefined}
                onClick={(event) => onSelectTask(task.id, {
                  additive: event.ctrlKey || event.metaKey,
                  range: event.shiftKey,
                  visibleIds: visibleTasks.map(item => item.id),
                })}
                onDoubleClick={() => onOpenTaskDetails(task.id)}
                onKeyDown={(event) => {
                  if (event.target instanceof HTMLInputElement) return
                  if (event.key === "Enter") {
                    event.preventDefault()
                    onOpenTaskDetails(task.id)
                  } else if (event.key === " ") {
                    event.preventDefault()
                    onSelectTask(task.id)
                  } else if (event.key === "ContextMenu" || (event.key === "F10" && event.shiftKey)) {
                    event.preventDefault()
                    const rect = event.currentTarget.getBoundingClientRect()
                    onSelectTask(task.id)
                    setContextMenu({ id: task.id, x: rect.left + 24, y: rect.bottom })
                  }
                }}
                onContextMenu={(e) => handleContextMenu(e, task.id)}
                draggable
                onDragStart={() => handleDragStart(task.id)}
                onDragOver={(e) => handleDragOver(e, task.id)}
                onDragEnd={handleDragEnd}
                onDrop={() => handleDrop(task.id)}
              >
                {columns.filter(c => c.visible).map((col) => {
                  const isEditing = editingField?.id === task.id && editingField?.field === col.id
                  const indent = col.id === "name" ? depth * 20 : 0

                  return (
                    <div
                      key={col.id}
                      className={cn(
                        "flex items-center gap-1 border-r border-border/40 px-2 py-1.5 shrink-0 overflow-hidden",
                        col.align === "right" ? "justify-end" : col.align === "center" ? "justify-center" : "justify-start",
                      )}
                      style={{ width: col.width, minWidth: col.width }}
                      onDoubleClick={(e) => {
                        // The task name follows the task-level interaction: double-click opens details.
                        if (col.id === "name") {
                          e.stopPropagation()
                          onOpenTaskDetails(task.id)
                          return
                        }
                        // Keep inline editing for editable grid fields without opening the drawer.
                        if (col.id === "duration" || col.id === "start" || col.id === "finish" || col.id === "owner" || col.id === "progress") {
                          e.stopPropagation()
                          const val = String(task[col.id as keyof PlanTask] ?? "")
                          handleStartEdit(task.id, col.id, val)
                        }
                      }}
                    >
                      {col.id === "wbs" && (
                        <span className="font-mono text-muted-foreground/60">{task.wbs}</span>
                      )}
                      {col.id === "name" && (
                        <>
                          <div className="flex items-center gap-0.5 shrink-0" style={{ width: indent }}>
                            {hasChildren ? (
                              <button
                                onClick={(e) => { e.stopPropagation(); onToggleExpand(task.id) }}
                                className="h-4 w-4 flex items-center justify-center rounded hover:bg-muted"
                              >
                                {isExpanded ? <ChevronDown className="h-3 w-3" /> : <ChevronRight className="h-3 w-3" />}
                              </button>
                            ) : (
                              <span className="w-4 shrink-0" />
                            )}
                          </div>
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <span className="shrink-0">{typeIcon[task.type] || <GripVertical className="h-3 w-3 text-muted-foreground/40" />}</span>
                            </TooltipTrigger>
                            <TooltipContent side="top">{task.type}</TooltipContent>
                          </Tooltip>
                          <span className={cn(
                            "truncate",
                            task.type === "Milestone" && "text-amber-600 dark:text-amber-400",
                            task.type === "JiraLinked" && "text-purple-600 dark:text-purple-400",
                            task.type === "TicketLinked" && "text-pink-600 dark:text-pink-400",
                            task.type === "Meeting" && "text-teal-600 dark:text-teal-400",
                            task.type === "ApprovalGate" && "text-emerald-600 dark:text-emerald-400",
                          )}>
                            {isEditing ? (
                              <Input
                                ref={editRef}
                                className="h-6 px-1 text-[11px]"
                                value={editValue}
                                onChange={e => setEditValue(e.target.value)}
                                onBlur={() => handleFinishEdit(task)}
                                onKeyDown={(e) => handleKeyDown(e, task)}
                                onClick={e => e.stopPropagation()}
                              />
                            ) : (
                              task.name
                            )}
                          </span>
                        </>
                      )}
                      {col.id === "duration" && (
                        isEditing ? (
                          <Input
                            ref={editRef}
                            className="h-6 px-1 text-[11px] w-16"
                            value={editValue}
                            onChange={e => setEditValue(e.target.value)}
                            onBlur={() => handleFinishEdit(task)}
                            onKeyDown={(e) => handleKeyDown(e, task)}
                            onClick={e => e.stopPropagation()}
                          />
                        ) : (
                          <span className={cn(task.duration === "0d" && "text-muted-foreground/60")}>{task.duration}</span>
                        )
                      )}
                      {col.id === "start" && (
                        isEditing ? (
                          <Input
                            ref={editRef}
                            className="h-6 px-1 text-[11px] w-24"
                            value={editValue}
                            onChange={e => setEditValue(e.target.value)}
                            onBlur={() => handleFinishEdit(task)}
                            onKeyDown={(e) => handleKeyDown(e, task)}
                            onClick={e => e.stopPropagation()}
                          />
                        ) : (
                          <span className="tabular-nums">{formatLocalDate(task.start)}</span>
                        )
                      )}
                      {col.id === "finish" && (
                        isEditing ? (
                          <Input
                            ref={editRef}
                            className="h-6 px-1 text-[11px] w-24"
                            value={editValue}
                            onChange={e => setEditValue(e.target.value)}
                            onBlur={() => handleFinishEdit(task)}
                            onKeyDown={(e) => handleKeyDown(e, task)}
                            onClick={e => e.stopPropagation()}
                          />
                        ) : (
                          <span className={cn("tabular-nums", task.status === "Delayed" && "text-status-red")}>{formatLocalDate(task.finish)}</span>
                        )
                      )}
                      {col.id === "predecessors" && (
                        <div className="flex flex-wrap gap-0.5">
                          {task.predecessors.length === 0 ? (
                            <span className="text-muted-foreground/40">—</span>
                          ) : (
                            task.predecessors.map(p => (
                              <Badge key={p} variant="outline" className="text-[9px] px-1 py-0 h-4">{p.replace("pt-", "T")}</Badge>
                            ))
                          )}
                        </div>
                      )}
                      {col.id === "resources" && (
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <span className="truncate text-muted-foreground flex items-center gap-1">
                              <User className="h-2.5 w-2.5 shrink-0" />
                              {task.resourceNames.join(", ") || "—"}
                            </span>
                          </TooltipTrigger>
                          <TooltipContent side="top">{task.resourceNames.join(", ") || "Unassigned"}</TooltipContent>
                        </Tooltip>
                      )}
                      {col.id === "progress" && (
                        <div className="flex items-center gap-1.5">
                          <div className="h-1.5 w-12 overflow-hidden rounded-full bg-muted">
                            <div
                              className={cn(
                                "h-full rounded-full transition-all",
                                task.progress === 100 ? "bg-status-green" : "bg-primary"
                              )}
                              style={{ width: `${task.progress}%` }}
                            />
                          </div>
                          <span className={cn(
                            "tabular-nums text-[10px]",
                            task.progress === 100 ? "text-status-green" : "text-muted-foreground"
                          )}>
                            {task.progress}%
                          </span>
                        </div>
                      )}
                      {col.id === "status" && (
                        <Badge className={cn("text-[9px] px-1.5 py-0 h-4 font-normal border", getStatusColor(task.status))} variant="outline">
                          {task.status}
                        </Badge>
                      )}
                      {col.id === "critical" && (
                        task.isCritical ? (
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <AlertTriangle className="h-3.5 w-3.5 text-status-red" />
                            </TooltipTrigger>
                            <TooltipContent>Critical Task (Slack: {task.totalSlack}d)</TooltipContent>
                          </Tooltip>
                        ) : (
                          <span className="text-muted-foreground/30">—</span>
                        )
                      )}
                      {col.id === "work" && (
                        <span className="tabular-nums text-muted-foreground">{task.work}h</span>
                      )}
                      {col.id === "actualWork" && (
                        <span className="tabular-nums text-muted-foreground">{task.actualWork}h</span>
                      )}
                      {col.id === "cost" && (
                        <span className="tabular-nums">£{(task.cost / 1000).toFixed(1)}k</span>
                      )}
                      {col.id === "forecastCost" && (
                        <span className="tabular-nums">£{(task.forecastCost / 1000).toFixed(1)}k</span>
                      )}
                      {col.id === "baselineStart" && (
                        <span className="tabular-nums text-muted-foreground/60">{task.baselineStart ? formatLocalDate(task.baselineStart) : "—"}</span>
                      )}
                      {col.id === "baselineFinish" && (
                        <span className="tabular-nums text-muted-foreground/60">{task.baselineFinish ? formatLocalDate(task.baselineFinish) : "—"}</span>
                      )}
                      {col.id === "variance" && (
                        <span className={cn("tabular-nums", task.variance < 0 ? "text-status-red" : "text-status-green")}>
                          {task.variance > 0 ? "+" : ""}{task.variance}d
                        </span>
                      )}
                      {col.id === "slack" && (
                        <span className={cn("tabular-nums", task.totalSlack <= 0 ? "text-status-red" : "text-muted-foreground")}>
                          {task.totalSlack}d
                        </span>
                      )}
                      {col.id === "owner" && (
                        <span className="truncate text-muted-foreground">{task.owner || "—"}</span>
                      )}
                      {col.id === "source" && (
                        <Badge variant="outline" className="text-[9px] px-1 py-0 h-4 font-normal">
                          {task.source}
                        </Badge>
                      )}
                      {col.id === "rowNumber" && (
                        <span className="font-mono text-muted-foreground/40 text-[10px]">{visibleTasks.indexOf(task) + 1}</span>
                      )}
                      {col.id === "indicators" && (
                        <div className="flex items-center gap-0.5">
                          {task.isCritical && <AlertTriangle className="h-3 w-3 text-status-red" />}
                          {task.status === "Delayed" && <Clock className="h-3 w-3 text-status-red" />}
                          {task.type === "Milestone" && <Diamond className="h-3 w-3 text-amber-500" />}
                          {task.source === "Jira" && <ExternalLink className="h-3 w-3 text-purple-500" />}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            )
          })}
        </div>

        {/* Context Menu */}
        {contextMenu && (
          <div
            ref={contextMenuRef}
            role="menu"
            aria-label="Task actions"
            className="fixed z-50 max-h-[calc(100vh-1rem)] min-w-[180px] overflow-y-auto rounded-md border bg-popover p-1 text-xs shadow-md"
            style={{
              left: contextMenuPosition?.x ?? contextMenu.x,
              top: contextMenuPosition?.y ?? contextMenu.y,
              visibility: contextMenuPosition ? "visible" : "hidden",
            }}
          >
            <button
              role="menuitem"
              className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-start font-medium hover:bg-accent"
              onClick={() => { onOpenTaskDetails(contextMenu.id); setContextMenu(null) }}
            >
              <FileText className="h-3.5 w-3.5 text-muted-foreground" />
              View task details
            </button>
            <div className="my-1 h-px bg-border" />
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onAddTask(contextMenu.id); setContextMenu(null) }}>Add Task Below</button>
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onAddSubtask(contextMenu.id); setContextMenu(null) }}>Add Subtask</button>
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onAddMilestone(contextMenu.id); setContextMenu(null) }}>Add Milestone</button>
            <div className="my-1 h-px bg-border" />
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onIndent(contextMenu.id); setContextMenu(null) }}>Indent</button>
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onOutdent(contextMenu.id); setContextMenu(null) }}>Outdent</button>
            <div className="my-1 h-px bg-border" />
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onReorderTasks(contextMenu.id, "up"); setContextMenu(null) }}>Move Up</button>
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start hover:bg-accent" onClick={() => { onReorderTasks(contextMenu.id, "down"); setContextMenu(null) }}>Move Down</button>
            <div className="my-1 h-px bg-border" />
            <button role="menuitem" className="w-full rounded px-2 py-1.5 text-start text-status-red hover:bg-status-red/10" onClick={() => { onDeleteTask(contextMenu.id); setContextMenu(null) }}>Delete</button>
          </div>
        )}
      </div>
    </TooltipProvider>
  )
}

function formatLocalDate(date: string): string {
  if (!date) return "—"
  const d = new Date(date)
  return d.toLocaleDateString("en-GB", { day: "numeric", month: "short" })
}
