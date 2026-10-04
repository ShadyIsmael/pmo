"use client";

import { useMemo, useState, type ReactNode } from "react";
import { CalendarDays, CircleAlert, History, MessageSquare, UserRound, Wrench, Ticket } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/state-display";
import { SideSheet, SideSheetBody } from "@/components/ui/side-sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { TaskComments } from "@/components/work-items/TaskComments";
import { WorkItemAssigneeSelect } from "@/components/work-items/WorkItemAssigneeSelect";
import { ApiError } from "@/lib/api/client";
import { useWorkItem, useWorkItemActivity, useWorkItemAssignees, useWorkItemMutations, useWorkItemWorkflow, type WorkItemAssigneeDto, type WorkItemDto } from "@/lib/api/work-items";
import type { WorkItemStatusReference } from "@/lib/api/reference-data";
import { getAssignmentTargetTextClass, getWorkItemStatusColorClasses, type AssignmentTargetKind } from "@/lib/status-presentation";
import { formatDate, formatDateTime } from "@/lib/utils";

type Copy = Record<string, string>;

export function WorkItemDetailsSideSheet({
  id,
  open,
  onOpenChange,
  locale,
  t,
  statuses,
  canEdit,
}: {
  id: string | null;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  locale: string;
  t: Copy;
  statuses: readonly WorkItemStatusReference[];
  canEdit: boolean;
}) {
  const itemQuery = useWorkItem(id ?? undefined);
  const item = itemQuery.data;
  const assigneesQuery = useWorkItemAssignees(item?.projectId, open && Boolean(item?.projectId));
  const assigneeCandidates = assigneesQuery.data ?? [];
  const statusOptions = useMemo(
    () => statuses.filter((status) => status.isActive || status.code === itemQuery.data?.status),
    [statuses, itemQuery.data?.status],
  );
  const assigneeName = item?.assignedResourceId || item?.assignedTeamId
    ? item.assignedResourceName ?? item.assignedTeamName ?? t.notAvailable
    : t.unassigned;
  const assignmentKind: AssignmentTargetKind = item?.assignedTeamId ? "team" : item?.assignedResourceId ? "resource" : "none";
  const title = item?.title ?? t.detailTitle;

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={title} description={item ? `${item.type} · ${item.id}` : undefined} size="xl" closeLabel={t.close}>
      <SideSheetBody className="space-y-4">
        {itemQuery.isLoading ? <DetailsSkeleton /> : itemQuery.isError || !item ? <EmptyState title={t.notFound} /> : (
          <WorkItemDetailsContent
            item={item}
            locale={locale}
            t={t}
            statuses={statuses}
            statusOptions={statusOptions}
            assigneeName={assigneeName}
            assignmentKind={assignmentKind}
            assigneeCandidates={assigneeCandidates}
            assigneeLoading={assigneesQuery.isLoading}
            canEdit={canEdit}
          />
        )}
      </SideSheetBody>
    </SideSheet>
  );
}

function WorkItemDetailsContent({ item, locale, t, statuses, statusOptions: _statusOptions, assigneeName, assignmentKind, assigneeCandidates, assigneeLoading, canEdit }: {
  item: WorkItemDto;
  locale: string;
  t: Copy;
  statuses: readonly WorkItemStatusReference[];
  statusOptions: readonly WorkItemStatusReference[];
  assigneeName: string;
  assignmentKind: AssignmentTargetKind;
  assigneeCandidates: readonly WorkItemAssigneeDto[];
  assigneeLoading: boolean;
  canEdit: boolean;
}) {
  const [tab, setTab] = useState("details");
  const workflow = useWorkItemWorkflow(item.id);
  const { update } = useWorkItemMutations();
  const [statusError, setStatusError] = useState("");
  const [assigneeError, setAssigneeError] = useState("");
  const status = statuses.find((candidate) => candidate.code === item.status);
  const tone = getWorkItemStatusColorClasses(status?.color);
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const deadline = item.type === "Issue" ? item.targetResolutionDate : item.dueDate;
  const deadlineLabel = item.type === "Issue" ? t.targetResolutionDate : t.deadline;
  const Icon = item.type === "Issue" ? Ticket : Wrench;

  const changeStatus = async (nextStatus: string, transitionCode?: string) => {
    if (!canEdit || nextStatus === item.status || workflow.execute.isPending) return;
    setStatusError("");
    try {
      const candidates = workflow.availableTransitions.filter((candidate) => candidate.targetStateCode === nextStatus && candidate.canExecute);
      const selected = transitionCode ? candidates.find((candidate) => candidate.code === transitionCode) : candidates.length === 1 ? candidates[0] : undefined;
      if (!selected) throw new Error(workflow.availableTransitions.find((candidate) => candidate.targetStateCode === nextStatus)?.denialReason || t.transitionUnavailable);
      await workflow.execute.mutateAsync({ transitionCode: selected.code });
    } catch (error) {
      setStatusError(error instanceof ApiError ? error.message : error instanceof Error ? error.message : t.moveError);
    }
  };

  const changeAssignee = async (nextTarget: string) => {
    if (!canEdit || update.isPending) return;
    const [targetType, targetId] = nextTarget.split(":");
    const currentTarget = item.assignedResourceId ? `resource:${item.assignedResourceId}` : item.assignedTeamId ? `team:${item.assignedTeamId}` : "";
    if (nextTarget === currentTarget) return;
    setAssigneeError("");
    try {
      await update.mutateAsync({ id: item.id, body: { title: item.title, description: item.description, type: item.type, priority: item.priority, projectId: item.projectId, assignedResourceId: targetType === "resource" ? targetId : undefined, assignedTeamId: targetType === "team" ? targetId : undefined, parentId: item.parentId, storyPoints: item.storyPoints, estimatedHours: item.estimatedHours, actualHours: item.actualHours, startDate: item.startDate, dueDate: item.dueDate, severity: item.severity, targetResolutionDate: item.targetResolutionDate, resolvedDate: item.resolvedDate, resolution: item.resolution, tags: item.tags, labels: item.labels, rowVersion: item.rowVersion } });
    } catch (error) {
      setAssigneeError(error instanceof ApiError ? error.message : t.assignmentError);
    }
  };

  return <div className="space-y-4">
    <div className="flex items-start gap-3 rounded-xl border border-border/70 bg-muted/20 p-4">
      <div className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${item.type === "Issue" ? "bg-info/10 text-info" : "bg-primary/10 text-primary"}`}><Icon className="h-5 w-5" /></div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span>{item.type}</span><span aria-hidden="true">·</span><span className="font-mono">{item.id}</span></div>
        <h2 className="mt-1 break-words text-lg font-semibold">{item.title}</h2>
        <div className="mt-3 flex flex-wrap gap-2"><Badge variant="outline" className={`${tone.badge} gap-1.5`}><span className={`h-2 w-2 rounded-full ${tone.dot}`} />{status ? (locale === "ar" ? status.nameAr : status.nameEn) : item.status}</Badge><Badge status={item.priority}>{t[item.priority.toLowerCase()] || item.priority}</Badge></div>
      </div>
    </div>

    <Tabs value={tab} onValueChange={setTab} className="w-full">
      <TabsList className="flex h-auto w-full flex-wrap justify-start gap-1 rounded-xl border border-border/70 bg-muted/30 p-1">
        <TabsTrigger value="details" className="flex-1 gap-1.5 sm:flex-none"><CircleAlert className="h-3.5 w-3.5" />{t.detailTabDetails}</TabsTrigger>
        <TabsTrigger value="comments" className="flex-1 gap-1.5 sm:flex-none"><MessageSquare className="h-3.5 w-3.5" />{t.detailTabComments}</TabsTrigger>
        <TabsTrigger value="activity" className="flex-1 gap-1.5 sm:flex-none"><History className="h-3.5 w-3.5" />{t.detailTabActivity}</TabsTrigger>
      </TabsList>

      <TabsContent value="details" className="space-y-4">
        <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="text-sm">{t.detailPrimaryFields}</CardTitle></CardHeader><CardContent className="grid gap-4 p-4 sm:grid-cols-2">
          <div className="space-y-2"><span className="text-xs text-muted-foreground">{t.currentState}</span><p className="text-sm font-semibold">{status ? (locale === "ar" ? status.nameAr : status.nameEn) : item.status}</p>{workflow.isLoading ? <p className="text-xs text-muted-foreground">{t.loading}</p> : workflow.availableTransitions.length > 0 ? <div className="flex flex-wrap gap-2">{workflow.availableTransitions.map((candidate) => <Button key={candidate.code} type="button" size="sm" variant="outline" disabled={!canEdit || !candidate.canExecute || workflow.execute.isPending} onClick={() => void changeStatus(candidate.targetStateCode, candidate.code)}>{locale === "ar" ? candidate.nameAr : candidate.nameEn}</Button>)}</div> : <p className="text-xs text-muted-foreground">{t.noTransitions}</p>}{statusError ? <p role="alert" className="text-xs text-destructive">{statusError}</p> : null}</div>
          <Field label={t.priority} value={t[item.priority.toLowerCase()] || item.priority} />
          <div className="space-y-1"><span className="text-xs text-muted-foreground">{t.assignee}</span>{canEdit && item.projectId ? <WorkItemAssigneeSelect value={item.assignedResourceId ? `resource:${item.assignedResourceId}` : item.assignedTeamId ? `team:${item.assignedTeamId}` : ""} candidates={assigneeCandidates} loading={assigneeLoading || update.isPending} onValueChange={changeAssignee} placeholder={t.unassigned} searchPlaceholder={t.searchAssignees} emptyText={t.noEligibleProjectPeople} clearLabel={t.unassign} /> : <p className={`flex items-center gap-1.5 text-sm font-medium ${getAssignmentTargetTextClass(assignmentKind)}`}><UserRound className="h-3.5 w-3.5" />{assigneeName}</p>}{canEdit && !item.projectId ? <p className="text-xs text-muted-foreground">{t.selectProjectFirst}</p> : null}{assigneeError ? <p role="alert" className="text-xs text-destructive">{assigneeError}</p> : null}</div>
          <Field label={deadlineLabel} value={deadline ? formatDate(deadline, dateLocale) : t.notAvailable} icon={<CalendarDays className="h-3.5 w-3.5" />} />
        </CardContent></Card>
        <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="text-sm">{item.type === "Issue" ? t.issueDetails : t.taskDetails}</CardTitle></CardHeader><CardContent className="grid gap-4 p-4 sm:grid-cols-2">
          {item.type === "Issue" ? <><Field label={t.severity} value={item.severity || t.notAvailable} /><Field label={t.resolution} value={item.resolution || t.notAvailable} /><Field label={t.identifiedDate} value={item.identifiedDate ? formatDate(item.identifiedDate, dateLocale) : t.notAvailable} /><Field label={t.resolvedDate} value={item.resolvedDate ? formatDate(item.resolvedDate, dateLocale) : t.notAvailable} /></> : <><Field label={t.storyPoints} value={item.storyPoints == null ? t.notAvailable : String(item.storyPoints)} /><Field label={t.estimatedHours} value={item.estimatedHours == null ? t.notAvailable : String(item.estimatedHours)} /><Field label={t.actualHours} value={item.actualHours == null ? t.notAvailable : String(item.actualHours)} /><Field label={t.startDate} value={item.startDate ? formatDate(item.startDate, dateLocale) : t.notAvailable} /></>}
        </CardContent></Card>
        {item.description ? <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="text-sm">{t.description}</CardTitle></CardHeader><CardContent className="whitespace-pre-wrap p-4 text-sm leading-6">{item.description}</CardContent></Card> : null}
      </TabsContent>
      <TabsContent value="comments"><TaskComments workItemId={item.id} locale={locale} t={t} /></TabsContent>
      <TabsContent value="activity"><ActivityPanel id={item.id} locale={locale} t={t} /></TabsContent>
    </Tabs>
  </div>;
}

function ActivityPanel({ id, locale, t }: { id: string; locale: string; t: Copy }) {
  const activityQuery = useWorkItemActivity(id, 1, 50, true);
  const entries = activityQuery.data?.items ?? [];
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";
  return <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="flex items-center gap-2 text-sm"><History className="h-4 w-4 text-primary" />{t.detailTabActivity}</CardTitle></CardHeader><CardContent className="space-y-3 p-4">{activityQuery.isLoading ? <><Skeleton className="h-14" /><Skeleton className="h-14" /></> : activityQuery.isError ? <p role="alert" className="text-sm text-destructive">{t.activityLoadError}</p> : entries.length === 0 ? <p className="text-sm text-muted-foreground">{t.noActivity}</p> : entries.map((entry) => <div key={entry.id} className="border-s-2 border-primary/30 ps-3"><div className="flex flex-wrap items-center justify-between gap-2"><Badge variant="secondary">{entry.action}</Badge><time className="text-[11px] text-muted-foreground">{formatDateTime(entry.timestamp, dateLocale)}</time></div><p className="mt-1 text-xs text-muted-foreground">{entry.userName || entry.userId || t.system}{entry.changes ? ` · ${entry.changes}` : ""}</p></div>)}</CardContent></Card>;
}

function Field({ label, value, icon }: { label: string; value: string; icon?: ReactNode }) {
  return <div className="space-y-1"><span className="text-xs text-muted-foreground">{label}</span><p className="flex items-center gap-1.5 text-sm font-medium">{icon}{value}</p></div>;
}

function DetailsSkeleton() {
  return <div className="space-y-4"><Skeleton className="h-24 w-full" /><Skeleton className="h-9 w-full" /><Skeleton className="h-48 w-full" /><Skeleton className="h-32 w-full" /></div>;
}
