"use client"

import { Card, CardContent } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { cn, formatCurrency } from "@/lib/utils"
import type { PlanSummary } from "@/lib/plan-mock-data"
import {
  ListChecks, CheckCircle2, Clock, AlertTriangle, Route, Diamond, BarChart3,
  CalendarDays, Briefcase, Banknote,
} from "lucide-react"

interface PlanSummaryPanelProps {
  summary: PlanSummary
  open: boolean
  onToggle: () => void
}

export function PlanSummaryPanel({ summary, open, onToggle }: PlanSummaryPanelProps) {
  if (!open) {
    return (
      <>
        <button
          onClick={onToggle}
          aria-expanded={false}
          aria-label="Open plan summary"
          className="hidden items-center gap-1.5 border-l bg-card px-3 py-1.5 text-[11px] font-medium text-muted-foreground hover:text-foreground xl:flex"
        >
          <BarChart3 className="h-3.5 w-3.5" />
          Summary
        </button>
        <button
          onClick={onToggle}
          aria-expanded={false}
          aria-label="Open plan summary"
          className="flex items-center gap-1.5 border-b bg-card px-3 py-1.5 text-[11px] font-medium text-muted-foreground hover:text-foreground xl:hidden"
        >
          <BarChart3 className="h-3.5 w-3.5" />
          Summary
        </button>
      </>
    )
  }

  const items = [
    { label: "Total Tasks", value: summary.totalTasks, icon: ListChecks, color: "text-muted-foreground" },
    { label: "Completed", value: summary.completed, icon: CheckCircle2, color: "text-status-green" },
    { label: "In Progress", value: summary.inProgress, icon: Clock, color: "text-blue-500" },
    { label: "Delayed", value: summary.delayed, icon: AlertTriangle, color: "text-status-red" },
    { label: "Critical", value: summary.criticalCount, icon: Route, color: "text-status-red" },
    { label: "Milestones", value: summary.milestones, icon: Diamond, color: "text-amber-500" },
  ]

  return (
    <>
      <div
        className={cn(
          "border-l bg-card",
          "xl:relative xl:inset-auto xl:z-auto xl:block xl:w-auto xl:overflow-visible xl:shadow-none",
          "fixed inset-y-12 end-0 z-50 block w-[min(90vw,360px)] overflow-y-auto shadow-xl"
        )}
      >
      <div className="flex items-center justify-between px-3 py-1.5 border-b">
        <span className="text-[11px] font-medium">Plan Summary</span>
        <button onClick={onToggle} aria-label="Close plan summary" className="text-muted-foreground hover:text-foreground">
          <svg className="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>
      <div className="p-3 space-y-2.5 w-56">
        {items.map(item => {
          const Icon = item.icon
          return (
            <div key={item.label} className="flex items-center justify-between text-[11px]">
              <span className="flex items-center gap-1.5 text-muted-foreground">
                <Icon className={`h-3.5 w-3.5 ${item.color}`} />
                {item.label}
              </span>
              <span className="font-medium tabular-nums">{item.value}</span>
            </div>
          )
        })}
        <div className="pt-2 border-t space-y-2">
          <div className="flex items-center justify-between text-[11px]">
            <span className="flex items-center gap-1.5 text-muted-foreground">
              <CalendarDays className="h-3.5 w-3.5" />
              Plan Duration
            </span>
            <span className="font-medium tabular-nums">{summary.plannedDuration}</span>
          </div>
          <div className="flex items-center justify-between text-[11px]">
            <span className="flex items-center gap-1.5 text-muted-foreground">
              <Briefcase className="h-3.5 w-3.5" />
              Planned Work
            </span>
            <span className="font-medium tabular-nums">{summary.totalPlannedWork}h</span>
          </div>
          <div className="flex items-center justify-between text-[11px]">
            <span className="flex items-center gap-1.5 text-muted-foreground">
              <Banknote className="h-3.5 w-3.5" />
              Planned Cost
            </span>
            <span className="font-medium tabular-nums">{formatCurrency(summary.totalPlannedCost)}</span>
          </div>
          <div className="flex items-center justify-between text-[11px]">
            <span className="flex items-center gap-1.5 text-muted-foreground">
              <Banknote className="h-3.5 w-3.5" />
              Forecast Cost
            </span>
            <span className="font-medium tabular-nums text-status-red">{formatCurrency(summary.totalForecastCost)}</span>
          </div>
          <div className="flex items-center justify-between text-[11px]">
            <span className="flex items-center gap-1.5 text-muted-foreground">Schedule Variance</span>
            <span className={summary.scheduleVariance < 0 ? "font-medium tabular-nums text-status-red" : "font-medium tabular-nums text-status-green"}>
              {summary.scheduleVariance > 0 ? "+" : ""}{summary.scheduleVariance}%
            </span>
          </div>
        </div>
        <div className="pt-1.5 text-[10px] text-muted-foreground/60">
          Last baseline: {summary.lastBaselineDate}
        </div>
      </div>
      </div>
      <button
        type="button"
        aria-label="Close plan summary"
        onClick={onToggle}
        className="fixed inset-12 z-40 bg-black/30 xl:hidden"
      />
    </>
  )
}
