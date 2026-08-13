"use client"

import { SideSheet } from "@/components/ui/side-sheet"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Progress } from "@/components/ui/progress"
import { cn, formatCurrency, formatDate } from "@/lib/utils"
import { Plus, Trash2, Circle, AlertCircle } from "lucide-react"
import type { PlanTask, DependencyType } from "@/lib/plan-mock-data"

interface TaskDetailsDrawerProps {
  task: PlanTask | null
  open: boolean
  onOpenChange: (open: boolean) => void
  onTaskChange: (task: PlanTask) => void
}

const CONSTRAINT_OPTIONS = [
  "As Soon As Possible",
  "As Late As Possible",
  "Must Start On",
  "Must Finish On",
  "Start No Earlier Than",
  "Start No Later Than",
  "Finish No Earlier Than",
  "Finish No Later Than",
] as const

const DEPENDENCY_TYPES: DependencyType[] = ["FS", "SS", "FF", "SF"]

type FieldRowProps = {
  label: string
  children: React.ReactNode
  className?: string
}

function FieldRow({ label, children, className }: FieldRowProps) {
  return (
    <div className={className}>
      <label className="text-[10px] font-medium uppercase tracking-wider text-muted-foreground">{label}</label>
      <div className="mt-1">{children}</div>
    </div>
  )
}

function InfoRow({ label, value, className }: { label: string; value: React.ReactNode; className?: string }) {
  return (
    <div className={cn("flex items-center justify-between border-b border-border/30 py-1.5 last:border-0", className)}>
      <span className="text-[11px] text-muted-foreground">{label}</span>
      <span className="text-xs font-medium">{value}</span>
    </div>
  )
}

export function TaskDetailsDrawer({ task, open, onOpenChange, onTaskChange }: TaskDetailsDrawerProps) {
  if (!task) return null

  const update = (field: keyof PlanTask, value: unknown) => {
    onTaskChange({ ...task, [field]: value as never })
  }

  const costVariance = task.plannedCost - task.actualCost
  const scheduleVariance = task.variance

  return (
    <SideSheet
      open={open}
      onOpenChange={onOpenChange}
      title={task.name}
      description={<>{task.type} &middot; {task.id}</>}
      headerLeading={(
        <Badge variant="outline" className="font-mono text-[10px] uppercase tracking-wider">
          {task.wbs}
        </Badge>
      )}
      size="md"
      className="sm:max-w-[600px]"
      headerClassName="px-5 py-3"
      titleClassName="truncate text-sm"
      descriptionClassName="text-[11px]"
    >

        <Tabs defaultValue="general" className="flex-1 flex overflow-hidden">
          <TabsList className="flex-col h-auto w-[120px] shrink-0 border-r rounded-none bg-muted/20 p-1 gap-0.5">
            <TabsTrigger value="general" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">General</TabsTrigger>
            <TabsTrigger value="schedule" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Schedule</TabsTrigger>
            <TabsTrigger value="dependencies" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Dependencies</TabsTrigger>
            <TabsTrigger value="resources" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Resources</TabsTrigger>
            <TabsTrigger value="costs" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Costs</TabsTrigger>
            <TabsTrigger value="baseline" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Baseline</TabsTrigger>
            <TabsTrigger value="notes" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Notes &amp; Attachments</TabsTrigger>
            <TabsTrigger value="workflow" className="w-full justify-start rounded px-2.5 py-1.5 text-[11px] data-[state=active]:bg-background data-[state=active]:shadow-sm">Workflow</TabsTrigger>
          </TabsList>

          <div className="flex-1 overflow-y-auto">
            {/* General Tab */}
            <TabsContent value="general" className="p-4 mt-0 space-y-4">
              <FieldRow label="Task Name">
                <Input value={task.name} onChange={e => update("name", e.target.value)} placeholder="Task name" />
              </FieldRow>
              <FieldRow label="Description">
                <Textarea value={task.description} onChange={e => update("description", e.target.value)} placeholder="Description" className="min-h-[60px]" />
              </FieldRow>
              <div className="grid grid-cols-2 gap-3">
                <FieldRow label="WBS Code">
                  <Badge variant="outline" className="h-8 rounded-md text-xs font-mono">{task.wbs}</Badge>
                </FieldRow>
                <FieldRow label="Type">
                  <Select value={task.type} onValueChange={v => update("type", v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {["Task", "Subtask", "Summary", "Milestone", "Recurring", "External", "ApprovalGate", "Deliverable", "Meeting", "JiraLinked", "TicketLinked"].map(t => (
                        <SelectItem key={t} value={t}>{t}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldRow>
                <FieldRow label="Mode">
                  <Select value={task.mode} onValueChange={v => update("mode", v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Auto">Auto</SelectItem>
                      <SelectItem value="Manual">Manual</SelectItem>
                    </SelectContent>
                  </Select>
                </FieldRow>
                <FieldRow label="Priority">
                  <Select value={task.priority} onValueChange={v => update("priority", v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {["Critical", "High", "Medium", "Low"].map(p => (
                        <SelectItem key={p} value={p}>{p}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldRow>
                <FieldRow label="Status">
                  <Select value={task.status} onValueChange={v => update("status", v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {["Not Started", "In Progress", "Completed", "Delayed", "On Hold"].map(s => (
                        <SelectItem key={s} value={s}>{s}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldRow>
                <FieldRow label="Owner">
                  <Input value={task.owner} onChange={e => update("owner", e.target.value)} placeholder="Owner" />
                </FieldRow>
                <FieldRow label="Contributors">
                  <Input placeholder="Contributors" disabled />
                </FieldRow>
                <FieldRow label="Reviewer">
                  <Input placeholder="Reviewer" disabled />
                </FieldRow>
                <FieldRow label="Approver">
                  <Input placeholder="Approver" disabled />
                </FieldRow>
              </div>
            </TabsContent>

            {/* Schedule Tab */}
            <TabsContent value="schedule" className="p-4 mt-0 space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <FieldRow label="Duration">
                  <Input value={task.duration} onChange={e => update("duration", e.target.value)} placeholder="e.g. 10d" />
                </FieldRow>
                <FieldRow label="% Complete">
                  <div className="space-y-1">
                    <Input type="number" min={0} max={100} value={task.progress} onChange={e => update("progress", Math.min(100, Math.max(0, Number(e.target.value))))} />
                    <Progress value={task.progress} className="h-1" />
                  </div>
                </FieldRow>
                <FieldRow label="Start">
                  <Input type="date" value={task.start} onChange={e => update("start", e.target.value)} />
                </FieldRow>
                <FieldRow label="Finish">
                  <Input type="date" value={task.finish} onChange={e => update("finish", e.target.value)} />
                </FieldRow>
                <FieldRow label="Actual Start">
                  <Input type="date" value={task.actualStart ?? ""} onChange={e => update("actualStart", e.target.value || null)} />
                </FieldRow>
                <FieldRow label="Actual Finish">
                  <Input type="date" value={task.actualFinish ?? ""} onChange={e => update("actualFinish", e.target.value || null)} />
                </FieldRow>
                <FieldRow label="Remaining Duration">
                  <Input value={task.duration} readOnly className="bg-muted/30" />
                </FieldRow>
                <FieldRow label="Deadline">
                  <Input type="date" value={task.deadline ?? ""} onChange={e => update("deadline", e.target.value || null)} />
                </FieldRow>
                <FieldRow label="Constraint Type">
                  <Select value={task.constraintType} onValueChange={v => update("constraintType", v)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {CONSTRAINT_OPTIONS.map(c => (
                        <SelectItem key={c} value={c}>{c}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldRow>
                <FieldRow label="Constraint Date">
                  <Input type="date" value={task.constraintDate ?? ""} onChange={e => update("constraintDate", e.target.value || null)} />
                </FieldRow>
                <FieldRow label="Calendar">
                  <Input value={task.calendar} readOnly className="bg-muted/30" />
                </FieldRow>
                <FieldRow label="Critical">
                  <Badge variant={task.isCritical ? "destructive" : "secondary"}>{task.isCritical ? "Yes" : "No"}</Badge>
                </FieldRow>
                <FieldRow label="Total Slack">
                  <div className="flex items-center gap-1">
                    <Input value={task.totalSlack} readOnly className="bg-muted/30 w-full" />
                    {task.totalSlack < 0 && <AlertCircle className="h-3.5 w-3.5 shrink-0 text-destructive" />}
                  </div>
                </FieldRow>
                <FieldRow label="Free Slack">
                  <Input value={task.freeSlack} readOnly className="bg-muted/30" />
                </FieldRow>
              </div>
            </TabsContent>

            {/* Dependencies Tab */}
            <TabsContent value="dependencies" className="p-4 mt-0 space-y-4">
              <div>
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-2">Predecessors</h4>
                {task.predecessors.length > 0 ? (
                  <div className="space-y-1.5 mb-3">
                    {task.predecessors.map((pred, i) => (
                      <div key={i} className="flex items-center gap-2 rounded-md border px-2.5 py-1.5">
                        <Badge variant="outline" className="font-mono text-[10px]">{pred}</Badge>
                        <Select defaultValue="FS">
                          <SelectTrigger className="h-6 w-14 px-1 text-[10px]"><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {DEPENDENCY_TYPES.map(d => <SelectItem key={d} value={d}>{d}</SelectItem>)}
                          </SelectContent>
                        </Select>
                        <Input placeholder="Lag" className="h-6 w-16 text-[10px]" />
                        <Button variant="ghost" size="icon" className="h-6 w-6 ml-auto shrink-0 text-muted-foreground hover:text-destructive">
                          <Trash2 className="h-3 w-3" />
                        </Button>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-[11px] text-muted-foreground italic mb-3">No predecessors</p>
                )}
                <div className="flex items-center gap-2">
                  <Input placeholder="Task ID" className="h-7 text-[11px] w-24" />
                  <Select defaultValue="FS">
                    <SelectTrigger className="h-7 w-14 text-[10px]"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {DEPENDENCY_TYPES.map(d => <SelectItem key={d} value={d}>{d}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  <Input placeholder="Lag" className="h-7 w-16 text-[11px]" />
                  <Button variant="outline" size="sm" className="h-7 text-[11px] gap-1">
                    <Plus className="h-3 w-3" /> Add
                  </Button>
                </div>
              </div>

              <div>
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-2">Successors</h4>
                {task.successorIds.length > 0 ? (
                  <div className="flex flex-wrap gap-1.5">
                    {task.successorIds.map((suc, i) => (
                      <Badge key={i} variant="secondary" className="font-mono text-[10px]">{suc}</Badge>
                    ))}
                  </div>
                ) : (
                  <p className="text-[11px] text-muted-foreground italic">No successors</p>
                )}
              </div>

              <div>
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-2">External Dependencies</h4>
                <p className="text-[11px] text-muted-foreground italic">No external dependencies configured</p>
              </div>
            </TabsContent>

            {/* Resources Tab */}
            <TabsContent value="resources" className="p-4 mt-0 space-y-4">
              {task.resources.length > 0 ? (
                <div className="space-y-2">
                  {task.resources.map((r, i) => (
                    <div key={r.id} className="rounded-md border p-2.5 space-y-1.5">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2 min-w-0">
                          <span className="text-xs font-medium truncate">{r.name}</span>
                          <Badge variant={r.type === "Internal" ? "default" : "outline"} className="text-[9px]">{r.type}</Badge>
                        </div>
                        <Badge variant="outline" className="text-[9px]">{r.role}</Badge>
                      </div>
                      <div className="grid grid-cols-4 gap-2 text-[10px] text-muted-foreground">
                        <span>Allocation: {r.allocation}%</span>
                        <span>Planned: {task.work}h</span>
                        <span>Actual: {task.actualWork}h</span>
                        <span>Remaining: {task.remainingWork}h</span>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-[11px] text-muted-foreground italic">No resources assigned</p>
              )}
              <Button variant="outline" size="sm" className="text-[11px] gap-1">
                <Plus className="h-3 w-3" /> Add Resource
              </Button>
              <p className="text-[10px] text-muted-foreground italic border-t pt-2">
                Cost rates visible based on Resource.ViewCostRate permission
              </p>
            </TabsContent>

            {/* Costs Tab */}
            <TabsContent value="costs" className="p-4 mt-0 space-y-3">
              <div className="rounded-md border divide-y divide-border/30">
                <InfoRow label="Planned Cost" value={formatCurrency(task.plannedCost)} />
                <InfoRow label="Actual Cost" value={formatCurrency(task.actualCost)} />
                <InfoRow label="Remaining Cost" value={formatCurrency(task.remainingCost)} />
                <InfoRow label="Committed Cost" value={formatCurrency(task.committedCost)} />
                <InfoRow label="Forecast Cost" value={formatCurrency(task.forecastCost)} />
                <InfoRow
                  label="Cost Variance"
                  value={
                    <span className={costVariance >= 0 ? "text-status-green" : "text-destructive"}>
                      {costVariance >= 0 ? "+" : ""}{formatCurrency(costVariance)}
                    </span>
                  }
                />
              </div>
              <p className="text-[10px] text-muted-foreground italic">
                GBP &mdash; Standard resource rates shown, not employee salaries
              </p>
            </TabsContent>

            {/* Baseline Tab */}
            <TabsContent value="baseline" className="p-4 mt-0 space-y-3">
              {task.baselineStart ? (
                <div className="rounded-md border divide-y divide-border/30">
                  <InfoRow label="Baseline Start" value={formatDate(task.baselineStart)} />
                  <InfoRow label="Baseline Finish" value={formatDate(task.baselineFinish ?? task.baselineStart)} />
                  <InfoRow label="Baseline Duration" value={task.baselineDuration ?? "—"} />
                  <InfoRow label="Baseline Work" value={task.baselineWork != null ? `${task.baselineWork}h` : "—"} />
                  <InfoRow label="Baseline Cost" value={task.baselineCost != null ? formatCurrency(task.baselineCost) : "—"} />
                  <InfoRow
                    label="Schedule Variance"
                    value={
                      <span className={scheduleVariance >= 0 ? "text-status-green" : "text-destructive"}>
                        {scheduleVariance >= 0 ? "+" : ""}{scheduleVariance}d
                      </span>
                    }
                  />
                  <InfoRow
                    label="Cost Variance"
                    value={
                      <span className={costVariance >= 0 ? "text-status-green" : "text-destructive"}>
                        {costVariance >= 0 ? "+" : ""}{formatCurrency(costVariance)}
                      </span>
                    }
                  />
                </div>
              ) : (
                <div className="flex flex-col items-center justify-center py-8 text-muted-foreground">
                  <Circle className="h-8 w-8 mb-2 opacity-30" />
                  <p className="text-sm font-medium">No baseline set for this task</p>
                  <p className="text-[11px]">Baseline data will appear here once set</p>
                </div>
              )}
            </TabsContent>

            {/* Notes & Attachments Tab */}
            <TabsContent value="notes" className="p-4 mt-0 space-y-4">
              <FieldRow label="Notes">
                <Textarea value={task.notes} onChange={e => update("notes", e.target.value)} placeholder="Add notes..." className="min-h-[100px]" />
              </FieldRow>
              <div>
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-2">Comments</h4>
                <p className="text-[11px] text-muted-foreground italic">No comments yet</p>
              </div>
              <div>
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-2">Attachments</h4>
                <p className="text-[11px] text-muted-foreground italic">No attachments</p>
              </div>
              <div>
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-2">Activity History</h4>
                <p className="text-[11px] text-muted-foreground italic">No recent activity</p>
              </div>
            </TabsContent>

            {/* Workflow Tab */}
            <TabsContent value="workflow" className="p-4 mt-0 space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <FieldRow label="Current Workflow State">
                  <Badge variant="secondary" className="text-[10px]">In Progress</Badge>
                </FieldRow>
                <FieldRow label="Review Status">
                  <Badge variant="outline" className="text-[10px]">Pending</Badge>
                </FieldRow>
                <FieldRow label="Approval Status">
                  <Badge variant="secondary" className="text-[10px]">N/A</Badge>
                </FieldRow>
                <FieldRow label="Required Next Action">
                  <span className="text-xs text-muted-foreground">Complete remaining work</span>
                </FieldRow>
                <FieldRow label="SLA">
                  <span className="text-xs text-muted-foreground">Not applicable</span>
                </FieldRow>
                <FieldRow label="Escalations">
                  <span className="text-xs text-muted-foreground">None</span>
                </FieldRow>
              </div>
              <div className="rounded-md border p-3 mt-2">
                <h4 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground mb-1">Workflow Details</h4>
                <p className="text-[11px] text-muted-foreground">
                  This task follows the standard project workflow. Escalation and SLA details are configured at the project level.
                </p>
              </div>
            </TabsContent>
          </div>
        </Tabs>
    </SideSheet>
  )
}
