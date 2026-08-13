"use client"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { X, Search } from "lucide-react"
import type { PlanTask } from "@/lib/plan-mock-data"

interface FilterState {
  status: string
  owner: string
  type: string
  priority: string
  search: string
  showMilestones: boolean
  showCriticalOnly: boolean
  showDelayedOnly: boolean
}

interface FilterBarProps {
  filters: FilterState
  owners: string[]
  onFilterChange: (filters: FilterState) => void
  onClear: () => void
}

export function FilterBar({ filters, owners, onFilterChange, onClear }: FilterBarProps) {
  const update = (partial: Partial<FilterState>) => {
    onFilterChange({ ...filters, ...partial })
  }

  const hasFilters = filters.status || filters.owner || filters.type || filters.priority || filters.search || filters.showMilestones || filters.showCriticalOnly || filters.showDelayedOnly

  return (
    <div className="flex flex-wrap items-center gap-2 border-b bg-muted/20 px-3 py-1.5">
      <div className="relative min-w-[160px] max-w-[240px] flex-1">
        <Search className="absolute left-2 top-1/2 h-3 w-3 -translate-y-1/2 text-muted-foreground/60" />
        <Input
          placeholder="Search tasks..."
          className="h-7 pl-7 text-[11px]"
          value={filters.search}
          onChange={e => update({ search: e.target.value })}
        />
      </div>
      <Select value={filters.status} onValueChange={v => update({ status: v })}>
        <SelectTrigger className="h-7 w-[110px] text-[11px]">
          <SelectValue placeholder="All Status" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="" className="text-xs">All Status</SelectItem>
          <SelectItem value="Not Started" className="text-xs">Not Started</SelectItem>
          <SelectItem value="In Progress" className="text-xs">In Progress</SelectItem>
          <SelectItem value="Completed" className="text-xs">Completed</SelectItem>
          <SelectItem value="Delayed" className="text-xs">Delayed</SelectItem>
          <SelectItem value="On Hold" className="text-xs">On Hold</SelectItem>
        </SelectContent>
      </Select>
      <Select value={filters.type} onValueChange={v => update({ type: v })}>
        <SelectTrigger className="h-7 w-[120px] text-[11px]">
          <SelectValue placeholder="All Types" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="" className="text-xs">All Types</SelectItem>
          <SelectItem value="Task" className="text-xs">Task</SelectItem>
          <SelectItem value="Summary" className="text-xs">Summary</SelectItem>
          <SelectItem value="Milestone" className="text-xs">Milestone</SelectItem>
          <SelectItem value="ApprovalGate" className="text-xs">Approval Gate</SelectItem>
          <SelectItem value="Meeting" className="text-xs">Meeting</SelectItem>
          <SelectItem value="JiraLinked" className="text-xs">Jira Linked</SelectItem>
          <SelectItem value="TicketLinked" className="text-xs">Ticket Linked</SelectItem>
        </SelectContent>
      </Select>
      <Select value={filters.owner} onValueChange={v => update({ owner: v })}>
        <SelectTrigger className="h-7 w-[130px] text-[11px]">
          <SelectValue placeholder="All Owners" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="" className="text-xs">All Owners</SelectItem>
          {owners.map(owner => <SelectItem key={owner} value={owner} className="text-xs">{owner}</SelectItem>)}
        </SelectContent>
      </Select>
      <Select value={filters.priority} onValueChange={v => update({ priority: v })}>
        <SelectTrigger className="h-7 w-[110px] text-[11px]">
          <SelectValue placeholder="All Priority" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="" className="text-xs">All Priority</SelectItem>
          <SelectItem value="Critical" className="text-xs">Critical</SelectItem>
          <SelectItem value="High" className="text-xs">High</SelectItem>
          <SelectItem value="Medium" className="text-xs">Medium</SelectItem>
          <SelectItem value="Low" className="text-xs">Low</SelectItem>
        </SelectContent>
      </Select>
      <Button
        variant={filters.showCriticalOnly ? "default" : "outline"}
        size="sm"
        className="h-7 text-[10px] px-2"
        onClick={() => update({ showCriticalOnly: !filters.showCriticalOnly })}
      >
        Critical
      </Button>
      <Button
        variant={filters.showDelayedOnly ? "default" : "outline"}
        size="sm"
        className="h-7 text-[10px] px-2"
        onClick={() => update({ showDelayedOnly: !filters.showDelayedOnly })}
      >
        Delayed
      </Button>
      <Button
        variant={filters.showMilestones ? "default" : "outline"}
        size="sm"
        className="h-7 text-[10px] px-2"
        onClick={() => update({ showMilestones: !filters.showMilestones })}
      >
        Milestones
      </Button>
      {hasFilters && (
        <Button variant="ghost" size="sm" className="h-7 px-2 text-[10px]" onClick={onClear}>
          <X className="h-3 w-3 mr-1" />
          Clear
        </Button>
      )}
      {hasFilters && <span className="text-[10px] text-muted-foreground">Filtered</span>}
    </div>
  )
}
