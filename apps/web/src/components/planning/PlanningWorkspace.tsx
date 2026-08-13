"use client"

import { useState, useMemo, useCallback } from "react"
import { useToast } from "@/hooks/use-toast"
import type { PlanTask, PlanSummary } from "@/lib/plan-mock-data"
import { projectPlanData, getPlanSummary, getAllTasks } from "@/lib/plan-mock-data"
import { ProjectContextHeader } from "./ProjectContextHeader"
import { PlanningToolbar } from "./PlanningToolbar"
import { SplitPane } from "./SplitPane"
import { TaskGrid, type ColumnDef } from "./TaskGrid"
import { GanttChart } from "./GanttChart"
import { TaskDetailsDrawer } from "./TaskDetailsDrawer"
import { PlanSummaryPanel } from "./PlanSummaryPanel"
import { ColumnChooser } from "./ColumnChooser"
import { FilterBar } from "./FilterBar"

type TaskSelectionOptions = {
  additive?: boolean
  range?: boolean
  visibleIds?: string[]
}

export function PlanningWorkspace() {
  const { toast } = useToast()
  // Core data
  const [rootTasks, setRootTasks] = useState<PlanTask[]>(() =>
    projectPlanData.rootTasks.map(t => JSON.parse(JSON.stringify(t)))
  )
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => {
    const s = new Set<string>()
    const walk = (tasks: PlanTask[]) => {
      for (const t of tasks) {
        if (t.children.length > 0) s.add(t.id)
        walk(t.children)
      }
    }
    walk(projectPlanData.rootTasks)
    return s
  })
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [selectionAnchorId, setSelectionAnchorId] = useState<string | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [summaryOpen, setSummaryOpen] = useState(true)

  // View state
  const [view, setView] = useState("gantt")
  const [zoom, setZoom] = useState(1)
  const [showCritical, setShowCritical] = useState(false)
  const [showBaseline, setShowBaseline] = useState(false)

  // Column state
  const [columnChooserOpen, setColumnChooserOpen] = useState(false)
  const [columns, setColumns] = useState<ColumnDef[]>([
    { id: "indicators", label: "", width: 36, visible: true, render: () => null },
    { id: "wbs", label: "WBS", width: 50, visible: true, render: () => null },
    { id: "name", label: "Task Name", width: 280, visible: true, frozen: true, render: () => null },
    { id: "duration", label: "Duration", width: 70, visible: true, align: "center", render: () => null },
    { id: "start", label: "Start", width: 85, visible: true, render: () => null },
    { id: "finish", label: "Finish", width: 85, visible: true, render: () => null },
    { id: "predecessors", label: "Predecessors", width: 90, visible: true, render: () => null },
    { id: "resources", label: "Resources", width: 120, visible: true, render: () => null },
    { id: "progress", label: "% Complete", width: 90, visible: true, render: () => null },
    { id: "status", label: "Status", width: 88, visible: true, render: () => null },
    { id: "critical", label: "Critical", width: 50, visible: true, align: "center", render: () => null },
    { id: "owner", label: "Owner", width: 100, visible: false, render: () => null },
    { id: "work", label: "Work", width: 60, visible: false, align: "right", render: () => null },
    { id: "actualWork", label: "Actual Work", width: 60, visible: false, align: "right", render: () => null },
    { id: "cost", label: "Cost", width: 70, visible: false, align: "right", render: () => null },
    { id: "forecastCost", label: "Forecast Cost", width: 70, visible: false, align: "right", render: () => null },
    { id: "baselineStart", label: "Baseline Start", width: 85, visible: false, render: () => null },
    { id: "baselineFinish", label: "Baseline Finish", width: 85, visible: false, render: () => null },
    { id: "variance", label: "Variance", width: 65, visible: false, align: "right", render: () => null },
    { id: "slack", label: "Slack", width: 55, visible: false, align: "right", render: () => null },
    { id: "source", label: "Source", width: 70, visible: false, render: () => null },
    { id: "rowNumber", label: "#", width: 32, visible: false, align: "center", render: () => null },
  ])

  // Filter state
  const [filters, setFilters] = useState({
    status: "", owner: "", type: "", priority: "",
    search: "", showMilestones: false, showCriticalOnly: false, showDelayedOnly: false,
  })
  const [filterVisible, setFilterVisible] = useState(false)

  // Derived data
  const allTasks = useMemo(() => {
    const result: PlanTask[] = []
    const walk = (tasks: PlanTask[]) => {
      for (const t of tasks) {
        result.push(t)
        if (t.children.length) walk(t.children)
      }
    }
    walk(rootTasks)
    return result
  }, [rootTasks])

  const owners = useMemo(
    () => Array.from(new Set(allTasks.map(task => task.owner).filter(Boolean))).sort(),
    [allTasks]
  )

  const selectedTask = useMemo(() => allTasks.find(t => t.id === selectedId) ?? null, [allTasks, selectedId])

  // Deep clone for state updates
  const cloneTasks = (tasks: PlanTask[]): PlanTask[] => JSON.parse(JSON.stringify(tasks))

  const findAndUpdate = useCallback((tasks: PlanTask[], id: string, updater: (t: PlanTask) => PlanTask): PlanTask[] => {
    return tasks.map(t => {
      if (t.id === id) return updater(t)
      if (t.children.length) return { ...t, children: findAndUpdate(t.children, id, updater) }
      return t
    })
  }, [])

  const findParentId = useCallback((tasks: PlanTask[], id: string): string | null => {
    for (const t of tasks) {
      if (t.children.find(c => c.id === id)) return t.id
      const found = findParentId(t.children, id)
      if (found) return found
    }
    return null
  }, [])

  const rebuildWBS = useCallback((tasks: PlanTask[], prefix = ""): PlanTask[] => {
    return tasks.map((t, i) => {
      const wbs = prefix ? `${prefix}.${i + 1}` : `${i + 1}`
      return {
        ...t,
        wbs,
        children: t.children.length ? rebuildWBS(t.children, wbs) : [],
      }
    })
  }, [])

  // Action handlers
  const handleTaskChange = useCallback((updated: PlanTask) => {
    setRootTasks(prev => rebuildWBS(findAndUpdate(prev, updated.id, () => updated)))
  }, [findAndUpdate, rebuildWBS])

  const handleToggleExpand = useCallback((id: string) => {
    setExpandedIds(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }, [])

  const handleSelectTask = useCallback((id: string, options: TaskSelectionOptions = {}) => {
    setSelectedIds(previous => {
      const visibleIds = options.visibleIds ?? allTasks.map(task => task.id)
      let next = new Set<string>()

      if (options.range && selectionAnchorId && visibleIds.includes(selectionAnchorId)) {
        const start = visibleIds.indexOf(selectionAnchorId)
        const end = visibleIds.indexOf(id)
        const [from, to] = start <= end ? [start, end] : [end, start]
        next = options.additive ? new Set(previous) : new Set<string>()
        visibleIds.slice(from, to + 1).forEach(taskId => next.add(taskId))
      } else if (options.additive) {
        next = new Set(previous)
        if (next.has(id)) next.delete(id)
        else next.add(id)
      } else {
        next.add(id)
      }

      setSelectedId(next.has(id) ? id : Array.from(next).at(-1) ?? null)
      setSelectionAnchorId(id)
      return next
    })
  }, [allTasks, selectionAnchorId])

  const handleOpenTaskDetails = useCallback((id: string) => {
    setSelectedId(id)
    setSelectedIds(new Set([id]))
    setSelectionAnchorId(id)
    setDrawerOpen(true)
  }, [])

  const handleExpandAll = useCallback(() => {
    const s = new Set<string>()
    const walk = (tasks: PlanTask[]) => {
      for (const t of tasks) {
        if (t.children.length > 0) {
          s.add(t.id)
          walk(t.children)
        }
      }
    }
    walk(rootTasks)
    setExpandedIds(s)
  }, [rootTasks])

  const handleCollapseAll = useCallback(() => {
    setExpandedIds(new Set())
  }, [])

  const handleAddTask = useCallback((parentId: string | null) => {
    const newId = `pt-new-${Date.now()}`
    const newTask: PlanTask = {
      id: newId, parentId, wbs: "", name: "New Task", type: "Task", mode: "Auto",
      duration: "1d", start: "2024-09-01", finish: "2024-09-01",
      actualStart: null, actualFinish: null, progress: 0, predecessors: [], successorIds: [],
      resourceNames: [], resources: [], work: 8, actualWork: 0, remainingWork: 8,
      cost: 0, actualCost: 0, remainingCost: 0, plannedCost: 0, forecastCost: 0, committedCost: 0,
      baselineStart: null, baselineFinish: null, baselineDuration: null, baselineWork: null, baselineCost: null,
      variance: 0, totalSlack: 0, freeSlack: 0, constraintType: "As Soon As Possible",
      constraintDate: null, deadline: null, priority: "Medium", isCritical: false,
      status: "Not Started", owner: "", source: "Internal", externalId: null, externalKey: null,
      syncStatus: null, lastSync: null, externalLink: null, calendar: "Standard",
      description: "", notes: "", isExpanded: true, children: [], tags: [],
    }

    setRootTasks(prev => {
      const updated = cloneTasks(prev)
      if (parentId) {
        const addToParent = (tasks: PlanTask[]): boolean => {
          for (let i = 0; i < tasks.length; i++) {
            const t = tasks[i]!
            if (t.id === parentId) {
              t.children.push({ ...newTask, parentId: t.id })
              return true
            }
            if (t.children.length && addToParent(t.children)) return true
          }
          return false
        }
        addToParent(updated)
      } else {
        updated.push(newTask)
      }
      return rebuildWBS(updated)
    })
    setExpandedIds(prev => new Set(prev).add(parentId || ""))
  }, [rebuildWBS])

  const handleAddSubtask = useCallback((parentId: string) => {
    handleAddTask(parentId)
  }, [handleAddTask])

  const handleAddMilestone = useCallback((parentId: string | null) => {
    const newId = `pt-new-milestone-${Date.now()}`
    const newMs: PlanTask = {
      id: newId, parentId, wbs: "", name: "New Milestone", type: "Milestone", mode: "Auto",
      duration: "0d", start: "2024-09-01", finish: "2024-09-01",
      actualStart: null, actualFinish: null, progress: 0, predecessors: [], successorIds: [],
      resourceNames: [], resources: [], work: 0, actualWork: 0, remainingWork: 0,
      cost: 0, actualCost: 0, remainingCost: 0, plannedCost: 0, forecastCost: 0, committedCost: 0,
      baselineStart: null, baselineFinish: null, baselineDuration: null, baselineWork: null, baselineCost: null,
      variance: 0, totalSlack: 0, freeSlack: 0, constraintType: "As Soon As Possible",
      constraintDate: null, deadline: null, priority: "Medium", isCritical: false,
      status: "Not Started", owner: "", source: "Internal", externalId: null, externalKey: null,
      syncStatus: null, lastSync: null, externalLink: null, calendar: "Standard",
      description: "", notes: "", isExpanded: true, children: [], tags: [],
    }
    setRootTasks(prev => {
      const updated = cloneTasks(prev)
      if (parentId) {
        const addToParent = (tasks: PlanTask[]): boolean => {
          for (let i = 0; i < tasks.length; i++) {
            const t = tasks[i]!
            if (t.id === parentId) {
              t.children.push({ ...newMs, parentId: t.id })
              return true
            }
            if (t.children.length && addToParent(t.children)) return true
          }
          return false
        }
        addToParent(updated)
      } else {
        updated.push(newMs)
      }
      return rebuildWBS(updated)
    })
  }, [rebuildWBS])

  const handleDeleteTask = useCallback((id: string) => {
    setRootTasks(prev => {
      const remove = (tasks: PlanTask[]): PlanTask[] => tasks.filter(t => {
        if (t.id === id) return false
        if (t.children.length) t.children = remove(t.children)
        return true
      })
      return rebuildWBS(remove(cloneTasks(prev)))
    })
    setSelectedIds(previous => {
      if (!previous.has(id)) return previous
      const next = new Set(previous)
      next.delete(id)
      setSelectedId(Array.from(next).at(-1) ?? null)
      if (selectionAnchorId === id) setSelectionAnchorId(Array.from(next).at(-1) ?? null)
      return next
    })
    if (selectedId === id) {
      setSelectedId(null)
      setDrawerOpen(false)
    }
  }, [rebuildWBS, selectedId, selectionAnchorId])

  const handleDeleteSelectedTasks = useCallback(() => {
    if (selectedIds.size === 0) return
    const idsToDelete = new Set(selectedIds)
    setRootTasks(prev => {
      const remove = (tasks: PlanTask[]): PlanTask[] => tasks
        .filter(task => !idsToDelete.has(task.id))
        .map(task => task.children.length ? { ...task, children: remove(task.children) } : task)
      return rebuildWBS(remove(cloneTasks(prev)))
    })
    setSelectedIds(new Set())
    setSelectionAnchorId(null)
    setSelectedId(null)
    setDrawerOpen(false)
  }, [rebuildWBS, selectedIds])

  const handleIndent = useCallback((id: string) => {
    setRootTasks(prev => {
      const updated = cloneTasks(prev)
      const walk = (tasks: PlanTask[], parent: PlanTask | null): boolean => {
        for (let i = 0; i < tasks.length; i++) {
          const t = tasks[i]!
          if (t.id === id && parent && i > 0) {
            const newParent = tasks[i - 1]!
            const removed = tasks.splice(i, 1)
            const moved = removed[0]!
            moved.parentId = newParent.id
            newParent.children.push(moved)
            return true
          }
          if (t.children.length && walk(t.children, t)) return true
        }
        return false
      }
      walk(updated, null)
      return rebuildWBS(updated)
    })
  }, [rebuildWBS])

  const handleOutdent = useCallback((id: string) => {
    setRootTasks(prev => {
      const updated = cloneTasks(prev)
      const walk = (tasks: PlanTask[], grandparent: PlanTask[] | null): boolean => {
        for (let i = 0; i < tasks.length; i++) {
          const t = tasks[i]!
          const children = t.children
          const childIdx = children.findIndex(c => c.id === id)
          if (childIdx >= 0 && grandparent !== null) {
            const removed = children.splice(childIdx, 1)
            const moved = removed[0]!
            moved.parentId = t.parentId
            const insertIdx = tasks.findIndex(x => x.id === t.id) + 1
            tasks.splice(insertIdx, 0, moved)
            return true
          }
          if (children.length && walk(children, tasks)) return true
        }
        return false
      }
      walk(updated, null)
      return rebuildWBS(updated)
    })
  }, [rebuildWBS])

  const handleReorder = useCallback((taskId: string, direction: "up" | "down") => {
    setRootTasks(prev => {
      const updated = cloneTasks(prev)
      const swapInList = (tasks: PlanTask[]): boolean => {
        const idx = tasks.findIndex(t => t.id === taskId)
        if (idx >= 0) {
          const swapIdx = direction === "up" ? idx - 1 : idx + 1
          if (swapIdx >= 0 && swapIdx < tasks.length) {
            const a = tasks[idx]!
            const b = tasks[swapIdx]!
            tasks[idx] = b
            tasks[swapIdx] = a
          }
          return true
        }
        for (const t of tasks) {
          if (t.children.length && swapInList(t.children)) return true
        }
        return false
      }
      swapInList(updated)
      return rebuildWBS(updated)
    })
  }, [rebuildWBS])

  const handleAction = useCallback((action: string) => {
    switch (action) {
      case "addTask": handleAddTask(null); break
      case "addMilestone": handleAddMilestone(null); break
      case "addSummary": {
        const newId = `pt-new-summary-${Date.now()}`
        const newSummary: PlanTask = {
          id: newId, parentId: null, wbs: "", name: "New Summary", type: "Summary", mode: "Auto",
          duration: "1d", start: "2024-09-01", finish: "2024-09-01",
          actualStart: null, actualFinish: null, progress: 0, predecessors: [], successorIds: [],
          resourceNames: [], resources: [], work: 0, actualWork: 0, remainingWork: 0,
          cost: 0, actualCost: 0, remainingCost: 0, plannedCost: 0, forecastCost: 0, committedCost: 0,
          baselineStart: null, baselineFinish: null, baselineDuration: null, baselineWork: null, baselineCost: null,
          variance: 0, totalSlack: 0, freeSlack: 0, constraintType: "As Soon As Possible",
          constraintDate: null, deadline: null, priority: "Medium", isCritical: false,
          status: "Not Started", owner: "", source: "Internal", externalId: null, externalKey: null,
          syncStatus: null, lastSync: null, externalLink: null, calendar: "Standard",
          description: "", notes: "", isExpanded: true, children: [], tags: [],
        }
        setRootTasks(prev => rebuildWBS([...cloneTasks(prev), newSummary]))
        break
      }
      case "expandAll": handleExpandAll(); break
      case "collapseAll": handleCollapseAll(); break
      case "delete": handleDeleteSelectedTasks(); break
      case "duplicate": {
        const tasksToDuplicate = allTasks.filter(task => selectedIds.has(task.id))
        if (tasksToDuplicate.length) {
          const timestamp = Date.now()
          const duplicates = tasksToDuplicate.map((task, index) => ({
            ...JSON.parse(JSON.stringify(task)),
            id: `pt-dup-${timestamp}-${index}`,
            parentId: null,
            name: `${task.name} (Copy)`,
          }))
          setRootTasks(prev => rebuildWBS([...cloneTasks(prev), ...duplicates]))
          const duplicateIds = duplicates.map(task => task.id)
          setSelectedIds(new Set(duplicateIds))
          setSelectedId(duplicateIds.at(-1) ?? null)
          setSelectionAnchorId(duplicateIds[0] ?? null)
        }
        break
      }
      case "indent": if (selectedId) handleIndent(selectedId); break
      case "outdent": if (selectedId) handleOutdent(selectedId); break
      case "moveUp": if (selectedId) handleReorder(selectedId, "up"); break
      case "moveDown": if (selectedId) handleReorder(selectedId, "down"); break
      case "link":
        toast({
          title: "Link tasks",
          description: selectedIds.size >= 2
            ? `${selectedIds.size} tasks selected. Dependency linking is ready for the plan API.`
            : "Select a second task to create a dependency.",
        })
        break
      case "unlink":
        toast({ title: "Unlink tasks", description: "Choose a task with an existing dependency first." })
        break
      case "assignResources":
        toast({ title: "Assign resources", description: "Resource assignment will be available when the plan API is connected." })
        break
      case "viewGantt": setView("gantt"); break
      case "viewMilestones": setView("milestones"); break
      case "viewCriticalPath": setView("criticalPath"); break
      case "viewBaseline": setView("baseline"); break
      case "viewResources": setView("resources"); break
      case "toggleCritical": setShowCritical(value => !value); break
      case "toggleBaseline": setShowBaseline(value => !value); break
      case "fitProject": setZoom(0.75); break
      case "columnChooser": setColumnChooserOpen(true); break
      case "filter": setFilterVisible(v => !v); break
    }
  }, [selectedId, selectedIds, allTasks, handleAddTask, handleAddMilestone, handleDeleteTask, handleDeleteSelectedTasks, handleIndent, handleOutdent, handleReorder, handleExpandAll, handleCollapseAll, rebuildWBS, toast])

  const handleZoomChange = useCallback((newZoom: number) => {
    setZoom(newZoom)
  }, [])

  const handleProjectAction = useCallback((action: string) => {
    if (action === "refresh") {
      setRootTasks(projectPlanData.rootTasks.map(task => JSON.parse(JSON.stringify(task))))
      setSelectedId(null)
      setSelectedIds(new Set())
      setSelectionAnchorId(null)
      setDrawerOpen(false)
      toast({ title: "Plan refreshed", description: "The local plan data has been reloaded.", variant: "success" })
      return
    }

    const labels: Record<string, string> = {
      save: "Save",
      publish: "Publish plan",
      baseline: "Set baseline",
      import: "Import Microsoft Project",
      aiDraft: "Generate AI draft",
      export: "Export plan",
      undo: "Undo",
      redo: "Redo",
      more: "More actions",
    }
    toast({
      title: labels[action] ?? "Plan action",
      description: "This action is ready for the plan API integration.",
    })
  }, [toast])

  const handleToggleColumn = useCallback((columnId: string) => {
    setColumns(prev => prev.map(c => c.id === columnId ? { ...c, visible: !c.visible } : c))
  }, [])

  const handleClearFilters = useCallback(() => {
    setFilters({ status: "", owner: "", type: "", priority: "", search: "", showMilestones: false, showCriticalOnly: false, showDelayedOnly: false })
  }, [])

  // Filtered/flattened tasks for display
  const displayedTasks = useMemo(() => {
    let result: PlanTask[] = []

    const walk = (tasks: PlanTask[]) => {
      for (const t of tasks) {
        result.push(t)
        if (t.children.length > 0 && expandedIds.has(t.id)) {
          walk(t.children)
        }
      }
    }
    walk(rootTasks)

    // Apply filters
    if (filters.search) {
      const q = filters.search.toLowerCase()
      result = result.filter(t => t.name.toLowerCase().includes(q) || t.wbs.includes(q))
    }
    if (filters.status) result = result.filter(t => t.status === filters.status)
    if (filters.type) result = result.filter(t => t.type === filters.type)
    if (filters.owner) result = result.filter(t => t.owner === filters.owner)
    if (filters.priority) result = result.filter(t => t.priority === filters.priority)
    if (filters.showMilestones) result = result.filter(t => t.type === "Milestone")
    if (filters.showCriticalOnly) result = result.filter(t => t.isCritical)
    if (filters.showDelayedOnly) result = result.filter(t => t.status === "Delayed")

    return result
  }, [rootTasks, expandedIds, filters])

  // Tasks for Gantt - always use all expanded tasks, but filter for special views
  const ganttTasks = useMemo(() => {
    if (view === "milestones") return allTasks.filter(t => t.type === "Milestone")
    if (view === "criticalPath") return allTasks.filter(t => t.isCritical || t.type === "Summary")
    if (view === "baseline") return allTasks.filter(t => t.baselineStart && t.baselineFinish)
    return displayedTasks
  }, [allTasks, displayedTasks, view, expandedIds])

  const summary = useMemo(() => getPlanSummary(allTasks), [allTasks])

  return (
    <div className="flex h-full flex-col">
      {/* Project Context Header */}
      <ProjectContextHeader project={projectPlanData} onAction={handleProjectAction} />

      {/* Planning Toolbar */}
      <PlanningToolbar
        onAction={handleAction}
        zoom={zoom}
        onZoomChange={handleZoomChange}
        view={view}
        showCritical={showCritical}
        showBaseline={showBaseline}
        selectedCount={selectedIds.size}
      />

      {/* Filter Bar */}
      {filterVisible && (
        <FilterBar filters={filters} owners={owners} onFilterChange={setFilters} onClear={handleClearFilters} />
      )}

      {/* Workspace */}
      <div className="flex flex-1 overflow-hidden">
        {/* Main View */}
        <div className="flex flex-1 flex-col overflow-hidden">
          {/* View Tab Bar */}
          {view !== "gantt" && view !== "resources" && (
            <div className="flex items-center gap-0.5 border-b bg-muted/20 px-2 py-1 text-[11px]">
              {view === "milestones" && <span className="text-muted-foreground">Showing {ganttTasks.length} milestones</span>}
              {view === "criticalPath" && <span className="text-muted-foreground">Showing critical and near-critical tasks ({ganttTasks.filter(t => t.isCritical).length} critical)</span>}
              {view === "baseline" && <span className="text-muted-foreground">Showing tasks with baseline values</span>}
            </div>
          )}

          {view === "resources" ? (
            <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
              <div className="text-center">
                <UsersIcon className="mx-auto h-12 w-12 text-muted-foreground/30" />
                <p className="mt-2 font-medium">Resource Usage View</p>
                <p className="text-xs text-muted-foreground/60">Resource planning and allocation visualization will be displayed here.</p>
                <p className="mt-4 text-[11px] text-muted-foreground/40">Available in a future iteration.</p>
              </div>
            </div>
          ) : view === "milestones" || view === "criticalPath" || view === "baseline" ? (
            <div className="flex flex-1">
              <div className="w-1/2 min-w-[400px] overflow-auto">
                <TaskGrid
                  tasks={rootTasks}
                  expandedIds={expandedIds}
                  selectedId={selectedId}
                  selectedIds={selectedIds}
                  onSelectTask={handleSelectTask}
                  onOpenTaskDetails={handleOpenTaskDetails}
                  onToggleExpand={handleToggleExpand}
                  onTaskChange={handleTaskChange}
                  onIndent={handleIndent}
                  onOutdent={handleOutdent}
                  onAddTask={handleAddTask}
                  onDeleteTask={handleDeleteTask}
                  columns={columns}
                  onAddSubtask={handleAddSubtask}
                  onAddMilestone={handleAddMilestone}
                  onReorderTasks={handleReorder}
                />
              </div>
              <div className="flex-1 border-l overflow-auto">
                {view === "milestones" ? <MilestoneView tasks={allTasks} /> :
                 view === "criticalPath" ? <CriticalPathView tasks={allTasks} /> :
                 view === "baseline" ? <BaselineComparisonView tasks={allTasks} /> : null}
              </div>
            </div>
          ) : (
            /* Split Pane: Grid + Gantt */
            <SplitPane
              left={
                <TaskGrid
                  tasks={rootTasks}
                  expandedIds={expandedIds}
                  selectedId={selectedId}
                  selectedIds={selectedIds}
                  onSelectTask={handleSelectTask}
                  onOpenTaskDetails={handleOpenTaskDetails}
                  onToggleExpand={handleToggleExpand}
                  onTaskChange={handleTaskChange}
                  onIndent={handleIndent}
                  onOutdent={handleOutdent}
                  onAddTask={handleAddTask}
                  onDeleteTask={handleDeleteTask}
                  columns={columns}
                  onAddSubtask={handleAddSubtask}
                  onAddMilestone={handleAddMilestone}
                  onReorderTasks={handleReorder}
                />
              }
              right={
                <GanttChart
                  tasks={ganttTasks}
                  expandedIds={expandedIds}
                  selectedId={selectedId}
                  selectedIds={selectedIds}
                  onSelectTask={handleSelectTask}
                  onOpenTaskDetails={handleOpenTaskDetails}
                  zoom={zoom}
                  showCritical={showCritical}
                  showBaseline={showBaseline}
                  onTaskChange={handleTaskChange}
                />
              }
              defaultLeftWidth={42}
              minLeftWidth={30}
              minRightWidth={25}
              leftLabel="Tasks"
              rightLabel="Gantt"
              syncVerticalScroll
            />
          )}
        </div>

        {/* Summary Panel */}
        <PlanSummaryPanel summary={summary} open={summaryOpen} onToggle={() => setSummaryOpen(!summaryOpen)} />
      </div>

      {/* Task Details Drawer */}
      <TaskDetailsDrawer
        task={selectedTask}
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        onTaskChange={handleTaskChange}
      />

      {/* Column Chooser Dialog */}
      <ColumnChooser
        open={columnChooserOpen}
        onOpenChange={setColumnChooserOpen}
        columns={columns}
        onToggleColumn={handleToggleColumn}
      />
    </div>
  )
}

// Sub-view components
function UsersIcon(props: { className?: string }) {
  return (
    <svg className={props?.className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  )
}

function MilestoneView({ tasks }: { tasks: PlanTask[] }) {
  const ms = tasks.filter(t => t.type === "Milestone")
  return (
    <div className="p-4">
      <h3 className="text-sm font-medium mb-3">Milestone Plan ({ms.length})</h3>
      <div className="space-y-2">
        {ms.map(m => (
          <div key={m.id} className="flex items-center gap-3 rounded border px-3 py-2 text-sm">
            <span className="flex h-4 w-4 shrink-0 rotate-45 border-2 border-amber-500" />
            <div className="flex-1 min-w-0">
              <p className="truncate font-medium text-sm">{m.name}</p>
              <p className="text-[11px] text-muted-foreground">{m.wbs} · {new Date(m.finish).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" })}</p>
            </div>
            <StatusBadge status={m.status} />
          </div>
        ))}
        {ms.length === 0 && <p className="text-xs text-muted-foreground">No milestones found.</p>}
      </div>
    </div>
  )
}

function CriticalPathView({ tasks }: { tasks: PlanTask[] }) {
  const critical = tasks.filter(t => t.isCritical)
  const criticalMs = critical.filter(t => t.type === "Milestone")
  const nearCritical = tasks.filter(t => t.totalSlack > 0 && t.totalSlack <= 5 && !t.isCritical)
  const mostDelayed = [...critical].sort((a, b) => a.totalSlack - b.totalSlack)[0]
  return (
    <div className="p-4 space-y-4">
      <h3 className="text-sm font-medium">Critical Path Analysis</h3>
      <div className="grid grid-cols-2 gap-2">
        <SummaryCard label="Critical Tasks" value={critical.length} color="text-status-red" />
        <SummaryCard label="Critical Milestones" value={criticalMs.length} color="text-amber-500" />
        <SummaryCard label="Near-Critical (≤5d slack)" value={nearCritical.length} color="text-amber-500" />
        <SummaryCard label="Most Influential" value={mostDelayed?.name?.slice(0, 20) || "N/A"} color="text-status-red" small />
      </div>
      <div className="space-y-1">
        {critical.map(t => (
          <div key={t.id} className="flex items-center gap-2 rounded border border-status-red/20 bg-status-red/[0.02] px-2.5 py-1.5 text-xs">
            <span className="h-1.5 w-1.5 rounded-full bg-status-red shrink-0" />
            <span className="font-medium">{t.wbs}</span>
            <span className="flex-1 truncate">{t.name}</span>
            <span className="tabular-nums text-muted-foreground">Slack: {t.totalSlack}d</span>
          </div>
        ))}
        {critical.length === 0 && <p className="text-xs text-muted-foreground">No critical tasks identified.</p>}
      </div>
    </div>
  )
}

function BaselineComparisonView({ tasks }: { tasks: PlanTask[] }) {
  const withBaseline = tasks.filter(t => t.baselineStart && t.baselineFinish)
  return (
    <div className="p-4 space-y-4">
      <h3 className="text-sm font-medium">Baseline Comparison</h3>
      <p className="text-[11px] text-muted-foreground">Comparing current plan with <strong>{projectPlanData.baselineName}</strong></p>
      <div className="space-y-2">
        {withBaseline.slice(0, 15).map(t => {
          const planStart = new Date(t.start).getTime()
          const baseStart = t.baselineStart ? new Date(t.baselineStart).getTime() : planStart
          const startVar = Math.round((planStart - baseStart) / 86400000)
          const planEnd = new Date(t.finish).getTime()
          const baseEnd = t.baselineFinish ? new Date(t.baselineFinish).getTime() : planEnd
          const endVar = Math.round((planEnd - baseEnd) / 86400000)
          return (
            <div key={t.id} className="rounded border px-3 py-2 text-xs">
              <div className="flex items-center gap-2">
                <span className="font-medium">{t.wbs}</span>
                <span className="flex-1 truncate">{t.name}</span>
              </div>
              <div className="mt-1 grid grid-cols-2 gap-x-4 gap-y-0.5 text-[10px] text-muted-foreground">
                <span>Baseline: {t.baselineStart ? new Date(t.baselineStart).toLocaleDateString("en-GB", { day: "numeric", month: "short" }) : "—"} – {t.baselineFinish ? new Date(t.baselineFinish).toLocaleDateString("en-GB", { day: "numeric", month: "short" }) : "—"}</span>
                <span>Current: {new Date(t.start).toLocaleDateString("en-GB", { day: "numeric", month: "short" })} – {new Date(t.finish).toLocaleDateString("en-GB", { day: "numeric", month: "short" })}</span>
                <span className={startVar !== 0 ? "text-status-red" : ""}>Start var: {startVar > 0 ? "+" : ""}{startVar}d</span>
                <span className={endVar !== 0 ? "text-status-red" : ""}>Finish var: {endVar > 0 ? "+" : ""}{endVar}d</span>
              </div>
            </div>
          )
        })}
        {withBaseline.length === 0 && <p className="text-xs text-muted-foreground">No baseline data available.</p>}
      </div>
    </div>
  )
}

function SummaryCard({ label, value, color, small }: { label: string; value: string | number; color: string; small?: boolean }) {
  return (
    <div className="rounded border px-2.5 py-2">
      <p className="text-[10px] text-muted-foreground">{label}</p>
      <p className={`text-sm font-bold tabular-nums ${color} ${small ? "truncate" : ""}`}>{value}</p>
    </div>
  )
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    Completed: "bg-status-green/10 text-status-green",
    "In Progress": "bg-blue-50 text-blue-700",
    Delayed: "bg-status-red/10 text-status-red",
    "On Hold": "bg-amber-50 text-amber-700",
  }
  return (
    <span className={`rounded px-1.5 py-0.5 text-[10px] font-medium ${colors[status] || "bg-muted text-muted-foreground"}`}>
      {status}
    </span>
  )
}
