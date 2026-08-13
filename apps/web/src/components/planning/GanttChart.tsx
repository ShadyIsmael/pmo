"use client"

import React, { useEffect, useMemo, useRef } from "react"
import type { PlanTask } from "@/lib/plan-mock-data"
import { cn, formatDate } from "@/lib/utils"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip"

interface GanttChartProps {
  tasks: PlanTask[]
  expandedIds: Set<string>
  selectedId: string | null
  selectedIds: ReadonlySet<string>
  onSelectTask: (id: string, options?: { additive?: boolean; range?: boolean; visibleIds?: string[] }) => void
  onOpenTaskDetails: (id: string) => void
  zoom: number
  showCritical: boolean
  showBaseline: boolean
  onTaskChange: (task: PlanTask) => void
}

const TASK_COLORS: Record<string, string> = {
  Task: "#3b82f6",
  Summary: "#6366f1",
  Milestone: "#f59e0b",
  JiraLinked: "#8b5cf6",
  TicketLinked: "#ec4899",
  ApprovalGate: "#10b981",
  Meeting: "#14b8a6",
}

function daysBetween(a: string, b: string): number {
  const da = new Date(a)
  const db = new Date(b)
  return Math.max(0, Math.round((db.getTime() - da.getTime()) / (1000 * 60 * 60 * 24)))
}

function isWeekend(dateStr: string): boolean {
  const d = new Date(dateStr)
  return d.getDay() === 0 || d.getDay() === 6
}

function getMonthsInRange(start: Date, end: Date): Date[] {
  const months: Date[] = []
  const current = new Date(start.getFullYear(), start.getMonth(), 1)
  while (current <= end) {
    months.push(new Date(current))
    current.setMonth(current.getMonth() + 1)
  }
  return months
}

function getDaysInRange(start: Date, end: Date): Date[] {
  const days: Date[] = []
  const current = new Date(start)
  while (current <= end) {
    days.push(new Date(current))
    current.setDate(current.getDate() + 1)
  }
  return days
}

function getBarColor(task: PlanTask): string {
  return TASK_COLORS[task.type] || "#3b82f6"
}

function DependencyLines({
  tasks,
  dayWidth,
  timelineStart,
}: {
  tasks: PlanTask[]
  dayWidth: number
  timelineStart: string
}) {
  const lines = useMemo(() => {
    const taskList = tasks
    const result: { x1: number; y1: number; x2: number; y2: number }[] = []
    for (let i = 0; i < taskList.length; i++) {
      const task = taskList[i]!
      if (!task.predecessors || task.predecessors.length === 0) continue
      for (const predId of task.predecessors) {
        const pred = taskList.find((t) => t.id === predId)
        if (!pred) continue
        const predIdx = taskList.indexOf(pred)
        if (predIdx === -1) continue
        const predEndOffset = daysBetween(timelineStart, pred.finish) * dayWidth
        const taskStartOffset = daysBetween(timelineStart, task.start) * dayWidth
        result.push({
          x1: predEndOffset,
          y1: predIdx * 32 + 16,
          x2: taskStartOffset,
          y2: i * 32 + 16,
        })
      }
    }
    return result
  }, [tasks, dayWidth, timelineStart])

  return (
    <svg className="absolute inset-0 pointer-events-none z-20" style={{ width: "100%", height: "100%" }}>
      {lines.map((line, i) => {
        const midY = (line.y1 + line.y2) / 2
        const d = `M ${line.x1} ${line.y1} C ${line.x1} ${midY}, ${line.x2} ${midY}, ${line.x2} ${line.y2}`
        const arrowDir = line.x2 >= line.x1 ? 1 : -1
        const arrowPoints =
          arrowDir > 0
            ? `${line.x2 - 4},${line.y2 - 3} ${line.x2},${line.y2} ${line.x2 - 4},${line.y2 + 3}`
            : `${line.x2 + 4},${line.y2 - 3} ${line.x2},${line.y2} ${line.x2 + 4},${line.y2 + 3}`
        return (
          <g key={i}>
            <path d={d} fill="none" stroke="#94a3b8" strokeWidth="1.5" />
            <polygon points={arrowPoints} fill="#94a3b8" />
          </g>
        )
      })}
    </svg>
  )
}

export const GanttChart = React.memo(function GanttChart({
  tasks,
  expandedIds,
  selectedId,
  selectedIds,
  onSelectTask,
  onOpenTaskDetails,
  zoom,
  showCritical,
  showBaseline,
}: GanttChartProps) {
  const dayWidth = 20 * zoom
  const timelineRef = useRef<HTMLDivElement>(null)

  const { start: timelineStart, end: timelineEnd, months, days } = useMemo(() => {
    let minDate = new Date("2024-01-01")
    let maxDate = new Date("2025-03-01")
    for (const task of tasks) {
      const taskStart = new Date(task.start)
      const taskFinish = new Date(task.finish)
      if (!isNaN(taskStart.getTime()) && taskStart < minDate) minDate = new Date(taskStart)
      if (!isNaN(taskFinish.getTime()) && taskFinish > maxDate) maxDate = new Date(taskFinish)
    }
    minDate.setDate(minDate.getDate() - 14)
    maxDate.setDate(maxDate.getDate() + 14)
    const months = getMonthsInRange(minDate, maxDate)
    const days = getDaysInRange(minDate, maxDate)
    return { start: minDate, end: maxDate, months, days }
  }, [tasks])

  const timelineStartStr = timelineStart.toISOString().split("T")[0]!
  const timelineEndStr = timelineEnd.toISOString().split("T")[0]!

  const totalDays = daysBetween(timelineStartStr, timelineEndStr!)
  const totalWidth = totalDays * dayWidth
  const rowHeight = 32
  const headerHeight = 48

  const todayStr = new Date().toISOString().split("T")[0]!
  const todayOffset = daysBetween(timelineStartStr, todayStr) * dayWidth

  const visibleTasks = useMemo(() => {
    return tasks.filter((task) => {
      if (!task.parentId) return true
      let current = task
      while (current.parentId) {
        const parent = tasks.find((t) => t.id === current.parentId)
        if (!parent) return true
        if (!expandedIds.has(parent.id)) return false
        current = parent
      }
      return true
    })
  }, [tasks, expandedIds])

  const rowsHeight = visibleTasks.length * rowHeight
  const totalHeight = rowsHeight + headerHeight

  useEffect(() => {
    const timeline = timelineRef.current
    if (!timeline || tasks.length === 0) return

    const firstTaskStart = tasks.reduce((earliest, task) => task.start < earliest ? task.start : earliest, tasks[0]!.start)
    const firstTaskOffset = daysBetween(timelineStartStr, firstTaskStart) * dayWidth
    timeline.scrollLeft = Math.max(0, firstTaskOffset - timeline.clientWidth * 0.2)
  }, [tasks, timelineStartStr, dayWidth])

  return (
    <TooltipProvider>
      <div ref={timelineRef} style={{ overflowX: "auto", overflowY: "hidden" }} dir="ltr">
        <div className="relative" style={{ width: totalWidth, minHeight: totalHeight }}>
          {/* Header */}
          <div className="sticky top-0 z-30 bg-background" style={{ height: headerHeight }}>
            {/* Month row */}
            <div className="flex" style={{ height: 24 }}>
              {months.map((month, i) => {
                const monthStart = new Date(month.getFullYear(), month.getMonth(), 1)
                const monthStartOffset = daysBetween(timelineStartStr, monthStart.toISOString().split("T")[0]!)
                let monthEnd: Date
                if (i < months.length - 1) {
                  monthEnd = new Date(months[i + 1]!.getFullYear(), months[i + 1]!.getMonth(), 1)
                } else {
                  monthEnd = new Date(timelineEnd)
                }
                const monthDays = daysBetween(monthStart.toISOString().split("T")[0]!, monthEnd.toISOString().split("T")[0]!)
                const actualWidth = monthDays * dayWidth
                return (
                  <div
                    key={i}
                    className="flex items-center border-r border-b bg-muted/50 px-1 text-[11px] font-medium text-muted-foreground truncate shrink-0"
                    style={{ width: actualWidth, minWidth: actualWidth }}
                  >
                    {month.toLocaleDateString("en-GB", { month: "short", year: "numeric" })}
                  </div>
                )
              })}
            </div>
            {/* Day row */}
            <div className="flex" style={{ height: 24 }}>
              {days.map((day, i) => {
                const dayStr = day.toISOString().split("T")[0]!
                const weekend = isWeekend(dayStr)
                const isToday = dayStr === todayStr
                return (
                  <div
                    key={i}
                    className={cn(
                      "flex items-center justify-center border-r border-b text-[10px] tabular-nums shrink-0",
                      weekend && "bg-muted/30",
                      isToday && "bg-red-50"
                    )}
                    style={{ width: dayWidth, minWidth: dayWidth, height: 24 }}
                  >
                    <span className={cn(isToday && "text-red-600 font-bold")}>{day.getDate()}</span>
                  </div>
                )
              })}
            </div>
          </div>

          {/* Rows area */}
          <div className="relative" style={{ height: rowsHeight }}>
            {/* Weekend column backgrounds */}
            {days.map((day, i) => {
                if (!isWeekend(day.toISOString().split("T")[0]!)) return null
              return (
                <div
                  key={i}
                  className="absolute top-0 bg-muted/20 pointer-events-none"
                  style={{ left: i * dayWidth, width: dayWidth, height: rowsHeight }}
                />
              )
            })}

            {/* Today line */}
            {todayOffset >= 0 && todayOffset <= totalWidth && (
              <div
                className="absolute top-0 w-px border-l border-red-400 border-dashed pointer-events-none z-10"
                style={{ left: todayOffset, height: rowsHeight }}
              />
            )}

            {/* Task bars */}
            {visibleTasks.map((task, index) => {
              const startOffset = daysBetween(timelineStartStr, task.start) * dayWidth
              const duration = Math.max(daysBetween(task.start, task.finish), 0)
              const barWidth = Math.max(duration * dayWidth, duration === 0 ? dayWidth : 4)
              const top = index * rowHeight
              const color = getBarColor(task)
              const isSelected = selectedIds.has(task.id)
              const isCritical = task.isCritical && showCritical
              const isMilestone = task.type === "Milestone"

              return (
                <Tooltip key={task.id}>
                  <TooltipTrigger asChild>
                    <div
                      className={cn("absolute flex items-center cursor-pointer group", isSelected && "z-10")}
                      style={{ top, left: 0, width: totalWidth, height: rowHeight }}
                      role="button"
                      tabIndex={0}
                      aria-label={`Open task ${task.name}`}
                      aria-current={selectedId === task.id ? "true" : undefined}
                      onClick={(event) => onSelectTask(task.id, {
                        additive: event.ctrlKey || event.metaKey,
                        range: event.shiftKey,
                        visibleIds: tasks.map(item => item.id),
                      })}
                      onDoubleClick={() => onOpenTaskDetails(task.id)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter") {
                          event.preventDefault()
                          onOpenTaskDetails(task.id)
                        } else if (event.key === " ") {
                          event.preventDefault()
                          onSelectTask(task.id, {
                            additive: event.ctrlKey || event.metaKey,
                            range: event.shiftKey,
                            visibleIds: tasks.map(item => item.id),
                          })
                        }
                      }}
                    >
                      {/* Baseline bar */}
                      {showBaseline && task.baselineStart && task.baselineFinish && (
                        <div
                          className="absolute rounded-sm opacity-60 pointer-events-none"
                          style={{
                            left: daysBetween(timelineStartStr, task.baselineStart!) * dayWidth,
                            width: Math.max(daysBetween(task.baselineStart!, task.baselineFinish!) * dayWidth, 4),
                            top: 2,
                            height: 6,
                            backgroundColor: "#94a3b8",
                            borderRadius: 2,
                          }}
                        />
                      )}

                      {/* Milestone diamond */}
                      {isMilestone ? (
                        <div
                          className={cn(
                            "absolute flex items-center justify-center",
                            isCritical && "shadow-[0_0_6px_rgba(239,68,68,0.7)]",
                            isSelected && "ring-2 ring-blue-400 rounded"
                          )}
                          style={{ left: startOffset - 8, width: 16, height: 16, top: 8 }}
                        >
                          <div className="w-3 h-3 rotate-45" style={{ backgroundColor: color }} />
                        </div>
                      ) : (
                        <div
                          className={cn(
                            "absolute rounded-sm overflow-hidden",
                            isCritical && "ring-2 ring-red-500/70 shadow-[0_0_6px_rgba(239,68,68,0.5)]",
                            isSelected && "ring-2 ring-blue-400"
                          )}
                          style={{
                            left: startOffset,
                            width: barWidth,
                            height: task.type === "Summary" ? 20 : 14,
                            top: task.type === "Summary" ? 6 : 9,
                            backgroundColor: color,
                            opacity: 0.85,
                          }}
                        >
                          {/* Progress fill with darker bottom */}
                          {task.progress > 0 && (
                            <div
                              className="absolute inset-y-0 left-0"
                              style={{
                                width: `${task.progress}%`,
                                background: "linear-gradient(to bottom, rgba(0,0,0,0.1), rgba(0,0,0,0.35))",
                              }}
                            />
                          )}

                          {/* Summary triangular ends */}
                          {task.type === "Summary" && (
                            <>
                              <div
                                className="absolute w-0 h-0 pointer-events-none"
                                style={{
                                  left: -8,
                                  top: 2,
                                  borderTop: "8px solid transparent",
                                  borderBottom: "8px solid transparent",
                                  borderRight: `8px solid ${color}`,
                                }}
                              />
                              <div
                                className="absolute w-0 h-0 pointer-events-none"
                                style={{
                                  right: -8,
                                  top: 2,
                                  borderTop: "8px solid transparent",
                                  borderBottom: "8px solid transparent",
                                  borderLeft: `8px solid ${color}`,
                                }}
                              />
                            </>
                          )}
                        </div>
                      )}
                    </div>
                  </TooltipTrigger>
                  <TooltipContent side="top" align="center" className="text-xs">
                    <div className="space-y-0.5">
                      <p className="font-medium">{task.name}</p>
                      <p>
                        {formatDate(task.start)} - {formatDate(task.finish)}
                      </p>
                      <p>Progress: {task.progress}%</p>
                      <p>Owner: {task.owner}</p>
                    </div>
                  </TooltipContent>
                </Tooltip>
              )
            })}

            {/* Dependency lines */}
            <DependencyLines tasks={visibleTasks} dayWidth={dayWidth} timelineStart={timelineStartStr} />
          </div>
        </div>
      </div>
    </TooltipProvider>
  )
})
