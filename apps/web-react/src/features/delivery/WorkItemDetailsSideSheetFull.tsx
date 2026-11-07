import * as React from "react";
import { Link } from "react-router-dom";
import { CalendarDays, ChevronDown, CircleAlert, History, MessageSquare, Reply, Send, UserRound, Wrench, Ticket } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Pagination } from "@components/ui/Pagination";
import { SideSheet, SideSheetBody } from "@components/ui/SideSheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { RichTextContent, RichTextEditor } from "@components/ui/rich-text-editor";
import { useFullWorkItem, useFullWorkItemActivity, useFullWorkItemAssignees, useFullWorkItemCommentMutation, useFullWorkItemComments, useFullWorkItemMutations, useFullWorkItemWorkflow } from "@api/workItemsFull";
import type { WorkItemStatusDto } from "@api/workItems";
import { useI18n } from "@i18n/messages";
import { WorkItemStatusTrack } from "./WorkItemStatusTrack";

function LoadingState({ label }: { label: string }) { return <p className="py-8 text-center text-sm text-muted-foreground">{label}</p>; }

export function WorkItemDetailsSideSheetFull({ id, open, onOpenChange, canEdit, statuses, statusesLoading = false, statusesError = false, onRetryStatuses }: {
  id?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  canEdit: boolean;
  statuses: readonly WorkItemStatusDto[];
  statusesLoading?: boolean;
  statusesError?: boolean;
  onRetryStatuses?: () => void;
}) {
  const { t, locale, formatDate } = useI18n();
  const itemQuery = useFullWorkItem(id, open);
  const item = itemQuery.data;
  const [tab, setTab] = React.useState("details");
  const [error, setError] = React.useState("");
  const [comment, setComment] = React.useState("");
  const [replyTo, setReplyTo] = React.useState<string>();
  const [commentsPage, setCommentsPage] = React.useState(1);
  const [activityPage, setActivityPage] = React.useState(1);
  const [expandedReplies, setExpandedReplies] = React.useState<Record<string, boolean>>({});
  const assigneesQuery = useFullWorkItemAssignees(item?.projectPublicId, open && canEdit && Boolean(item?.projectPublicId));
  const workflow = useFullWorkItemWorkflow(id, open && Boolean(item));
  const mutations = useFullWorkItemMutations();
  const commentsQuery = useFullWorkItemComments(id ?? "", open && tab === "comments", commentsPage, 20);
  const activityQuery = useFullWorkItemActivity(id, open && tab === "activity", activityPage, 20);
  const commentMutation = useFullWorkItemCommentMutation(id ?? "");
  const status = statuses.find((candidate) => candidate.code === item?.status);
  const deadline = item?.type === "Issue" ? item.targetResolutionDate ?? item.dueDate : item?.dueDate;
  const currentTarget = item?.assignedResourceId ? `resource:${item.assignedResourceId}` : item?.assignedTeamId ? `team:${item.assignedTeamId}` : "";
  const eligibleAssignees = assigneesQuery.data ?? [];
  const currentTargetIsEligible = eligibleAssignees.some((candidate) => `${candidate.targetType === "Resource" ? "resource" : "team"}:${candidate.targetId}` === currentTarget);
  const assigneeOptions = [
    { value: "", label: t("workItems.unassigned") },
    ...(currentTarget && !currentTargetIsEligible ? [{ value: currentTarget, label: item?.assignedTeamName ?? item?.assignedResourceName ?? t("common.notAvailable") }] : []),
    ...eligibleAssignees.map((candidate) => ({ value: `${candidate.targetType === "Resource" ? "resource" : "team"}:${candidate.targetId}`, label: `${candidate.name} · ${candidate.targetType === "Resource" ? t("workItems.resourceTarget") : t("workItems.teamTarget")}` })),
  ];
  const assigneeScopeUnavailable = Boolean(item?.projectId && !item.projectPublicId);
  const commentItems = commentsQuery.data?.items ?? [];
  const rootComments = commentItems.filter((entry) => !entry.parentCommentId);
  const repliesByComment = commentItems.reduce<Record<string, typeof commentItems>>((grouped, entry) => {
    if (entry.parentCommentId) (grouped[entry.parentCommentId] ??= []).push(entry);
    return grouped;
  }, {});
  const richTextLabels = { formatting: t("meetings.formatting"), bold: t("meetings.bold"), italic: t("meetings.italic"), underline: t("meetings.underline"), bulletedList: t("meetings.bulletedList"), numberedList: t("meetings.numberedList"), emoji: t("meetings.emoji") };

  React.useEffect(() => {
    setTab("details");
    setError("");
    setComment("");
    setReplyTo(undefined);
    setCommentsPage(1);
    setActivityPage(1);
    setExpandedReplies({});
  }, [id, open]);

  async function changeStatus(transitionCode: string) {
    setError("");
    try { await workflow.execute.mutateAsync({ transitionCode }); }
    catch (cause) { setError(cause instanceof Error ? cause.message : t("workItems.moveError")); }
  }

  async function changeAssignee(value: string) {
    if (!item || !canEdit || mutations.update.isPending || value === currentTarget) return;
    const [kind, targetId] = value.split(":");
    setError("");
    try {
      await mutations.update.mutateAsync({ id: item.id, body: {
        title: item.title, description: item.description, type: item.type, priority: item.priority,
        projectId: item.projectId, assignedResourceId: kind === "resource" ? targetId : undefined,
        assignedTeamId: kind === "team" ? targetId : undefined, parentId: item.parentId,
        storyPoints: item.storyPoints, estimatedHours: item.estimatedHours, actualHours: item.actualHours,
        startDate: item.startDate, dueDate: item.dueDate, severity: item.severity,
        targetResolutionDate: item.targetResolutionDate, resolvedDate: item.resolvedDate,
        resolution: item.resolution, tags: item.tags, labels: item.labels, rowVersion: item.rowVersion,
      } });
    } catch (cause) { setError(cause instanceof Error ? cause.message : t("workItems.assignmentError")); }
  }

  async function addComment() {
    if (!id || !comment.trim()) return;
    setError("");
    const parentId = replyTo;
    try {
      await commentMutation.mutateAsync({ commentText: comment.trim(), parentCommentId: parentId });
      setComment("");
      setReplyTo(undefined);
      setCommentsPage(1);
      if (parentId) setExpandedReplies((current) => ({ ...current, [parentId]: true }));
    } catch (cause) { setError(cause instanceof Error ? cause.message : t("workItems.commentError")); }
  }

  return <SideSheet open={open} onOpenChange={onOpenChange} title={item?.title ?? t("workItems.detailTitle")} description={item ? `${item.type} · ${item.id}` : undefined} size="xl" closeLabel={t("common.close")}>
    <SideSheetBody className="space-y-4">
      {itemQuery.isLoading ? <div className="space-y-3" aria-label={t("common.loading")}><div className="h-24 animate-pulse rounded-xl bg-muted/60" /><div className="h-10 animate-pulse rounded-lg bg-muted/60" /><div className="h-52 animate-pulse rounded-xl bg-muted/60" /></div> : itemQuery.isError ? <div className="space-y-3 py-8 text-center"><p role="alert" className="text-sm text-destructive">{t("workItems.detailLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void itemQuery.refetch()}>{t("common.retry")}</Button></div> : !item ? <p role="alert" className="py-8 text-center text-sm text-destructive">{t("common.notFound")}</p> : <>
        <div className="flex items-start gap-3 rounded-xl border border-border/70 bg-muted/20 p-4">
          <div className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${item.type === "Issue" ? "bg-info/10 text-info" : "bg-primary/10 text-primary"}`}>{item.type === "Issue" ? <Ticket className="h-5 w-5" /> : <Wrench className="h-5 w-5" />}</div>
          <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span>{item.type === "Task" || item.type === "Issue" ? t(`workItems.${item.type.toLowerCase()}`) : item.type}</span><span aria-hidden="true">·</span><span className="font-mono">{item.id}</span></div><h2 className="mt-1 break-words text-lg font-semibold">{item.title}</h2><div className="mt-3 flex flex-wrap gap-2"><Badge status={item.status}>{locale === "ar" ? status?.nameAr ?? item.status : status?.nameEn ?? item.status}</Badge><Badge status={item.priority}>{t(`workItems.priority${item.priority}`)}</Badge></div></div>
          <Link to={`/work-items/${encodeURIComponent(item.id)}`} className="shrink-0 text-xs font-medium text-primary hover:underline">{t("workItems.openFullDetails")}</Link>
        </div>
        {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList className="flex h-auto w-full flex-wrap justify-start gap-1 rounded-xl border border-border/70 bg-muted/30 p-1">
            <TabsTrigger value="details" className="flex-1 gap-1.5 sm:flex-none"><CircleAlert className="h-3.5 w-3.5" />{t("workItems.tabs.details")}</TabsTrigger>
            <TabsTrigger value="comments" className="flex-1 gap-1.5 sm:flex-none"><MessageSquare className="h-3.5 w-3.5" />{t("workItems.tabs.comments")}</TabsTrigger>
            <TabsTrigger value="activity" className="flex-1 gap-1.5 sm:flex-none"><History className="h-3.5 w-3.5" />{t("workItems.tabs.activity")}</TabsTrigger>
          </TabsList>
          <TabsContent value="details" className="space-y-4">
            <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="text-sm">{t("workItems.detailPrimaryFields")}</CardTitle></CardHeader><CardContent className="grid gap-4 p-4 sm:grid-cols-2">
              <div className="space-y-2"><span className="text-xs text-muted-foreground">{t("workItems.currentState")}</span><WorkItemStatusTrack
                statuses={statuses}
                currentStateCode={workflow.currentState ?? item.status}
                currentStateNameEn={workflow.data?.currentStateNameEn}
                currentStateNameAr={workflow.data?.currentStateNameAr}
                transitions={workflow.availableTransitions}
                canTransition={canEdit}
                transitionPending={workflow.execute.isPending}
                loading={workflow.isLoading || statusesLoading}
                error={statusesError}
                onRetry={onRetryStatuses}
                onTransition={(code) => void changeStatus(code)}
              /></div>
              <Field label={t("common.priority")} value={t(`workItems.priority${item.priority}`)} />
              <div className="space-y-1"><span className="text-xs text-muted-foreground">{t("workItems.assignee")}</span>{canEdit && item.projectId ? <SearchableSelect aria-label={t("workItems.assignee")} options={assigneeOptions} value={currentTarget} onValueChange={(value) => void changeAssignee(value)} placeholder={t("workItems.unassigned")} searchPlaceholder={t("workItems.searchAssignees")} emptyText={t(assigneesQuery.isError ? "workItems.assigneesLoadError" : "workItems.noEligibleProjectPeople")} loadingText={t("common.loading")} loading={assigneesQuery.isLoading} disabled={mutations.update.isPending} /> : <p className="flex items-center gap-1.5 text-sm font-medium"><UserRound className="h-3.5 w-3.5" />{item.assignedTeamName ?? item.assignedResourceName ?? t("workItems.unassigned")}</p>}{canEdit && !item.projectId ? <p className="text-xs text-muted-foreground">{t("workItems.selectProjectFirst")}</p> : null}{canEdit && item.projectId && assigneesQuery.isError ? <div role="alert" className="flex flex-wrap items-center justify-between gap-2"><p className="text-xs text-destructive">{t("workItems.assigneesLoadError")}</p><Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-xs" onClick={() => void assigneesQuery.refetch()}>{t("common.retry")}</Button></div> : null}{canEdit && assigneeScopeUnavailable ? <p role="alert" className="text-xs text-destructive">{t("workItems.assigneesScopeUnavailable")}</p> : null}{canEdit && item.projectId && !assigneeScopeUnavailable && assigneesQuery.isSuccess && eligibleAssignees.length === 0 && !currentTarget ? <p className="text-xs text-muted-foreground">{t("workItems.noEligibleProjectPeople")}</p> : null}</div>
              <Field label={t(item.type === "Issue" ? "workItems.targetResolutionDate" : "workItems.deadline")} value={deadline ? formatDate(deadline) : t("common.notAvailable")} icon={<CalendarDays className="h-3.5 w-3.5" />} />
            </CardContent></Card>
            <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="text-sm">{item.type === "Issue" ? t("workItems.issueDetails") : t("workItems.taskDetails")}</CardTitle></CardHeader><CardContent className="grid gap-4 p-4 sm:grid-cols-2">
              {item.type === "Issue" ? <><Field label={t("workItems.severity")} value={item.severity || t("common.notAvailable")} /><Field label={t("workItems.resolution")} value={item.resolution || t("common.notAvailable")} /><Field label={t("workItems.identifiedDate")} value={item.identifiedDate ? formatDate(item.identifiedDate) : t("common.notAvailable")} /><Field label={t("workItems.resolvedDate")} value={item.resolvedDate ? formatDate(item.resolvedDate) : t("common.notAvailable")} /></> : <><Field label={t("workItems.storyPoints")} value={item.storyPoints == null ? t("common.notAvailable") : String(item.storyPoints)} /><Field label={t("workItems.estimatedHours")} value={item.estimatedHours == null ? t("common.notAvailable") : String(item.estimatedHours)} /><Field label={t("workItems.actualHours")} value={item.actualHours == null ? t("common.notAvailable") : String(item.actualHours)} /><Field label={t("workItems.startDate")} value={item.startDate ? formatDate(item.startDate) : t("common.notAvailable")} /></>}
            </CardContent></Card>
        {item.description ? <Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="text-sm">{t("workItems.description")}</CardTitle></CardHeader><CardContent className="p-4"><RichTextContent value={item.description} /></CardContent></Card> : null}
          </TabsContent>
          <TabsContent value="comments"><Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="flex items-center gap-2 text-sm"><MessageSquare className="h-4 w-4 text-primary" />{t("workItems.comments")}</CardTitle></CardHeader><CardContent className="space-y-4 p-4">
            {commentsQuery.isLoading ? <LoadingState label={t("common.loading")} /> : commentsQuery.isError ? <div className="space-y-2"><p role="alert" className="text-sm text-destructive">{t("workItems.commentLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void commentsQuery.refetch()}>{t("common.retry")}</Button></div> : rootComments.length ? <div className="space-y-4">{rootComments.map((entry) => { const replies = repliesByComment[entry.id] ?? []; const expanded = Boolean(expandedReplies[entry.id]); return <article key={entry.id} className="overflow-hidden rounded-xl border border-border/70 shadow-sm"><header className="flex flex-wrap items-center justify-between gap-2 border-b bg-muted/20 px-3 py-2.5"><span className="truncate text-sm font-semibold">{entry.authorName ?? entry.createdBy}</span><time className="text-xs text-muted-foreground">{formatDate(entry.createdAt)}</time></header><div className="p-3"><RichTextContent value={entry.commentText} /></div><footer className="border-t px-3 py-1.5"><Button type="button" size="sm" variant="ghost" className="gap-1.5 text-xs" onClick={() => setReplyTo(entry.id)}><Reply className="h-3.5 w-3.5" />{t("workItems.reply")}</Button></footer>{replies.length ? <><Button type="button" size="sm" variant="ghost" className="ms-3 my-1 h-8 gap-1.5 px-2 text-xs text-info" aria-expanded={expanded} onClick={() => setExpandedReplies((current) => ({ ...current, [entry.id]: !expanded }))}><ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} />{t(expanded ? "workItems.hideReplies" : "workItems.showReplies")} ({replies.length})</Button>{expanded ? <div className="space-y-2 px-3 pb-3">{replies.map((reply) => <div key={reply.id} className="ms-3 rounded-md border-s-2 border-info/35 bg-info/[0.035] px-3 py-2"><div className="flex flex-wrap items-center justify-between gap-2 text-xs"><strong>{reply.authorName ?? reply.createdBy}</strong><time className="text-muted-foreground">{formatDate(reply.createdAt)}</time></div><div className="mt-2"><RichTextContent value={reply.commentText} /></div></div>)}</div> : null}</> : null}{replyTo === entry.id ? <div className="space-y-2 border-t bg-muted/10 p-3"><p className="flex items-center gap-2 text-xs font-medium text-primary"><Reply className="h-3.5 w-3.5" />{t("workItems.replyingToComment")}</p><RichTextEditor id={`work-item-reply-${entry.id}`} value={comment} onChange={setComment} placeholder={t("workItems.replyPlaceholder")} ariaLabel={t("workItems.replyPlaceholder")} t={richTextLabels} minHeight="min-h-20" /><div className="flex justify-end gap-2"><Button type="button" size="sm" variant="outline" onClick={() => { setReplyTo(undefined); setComment(""); }}>{t("common.cancel")}</Button><Button type="button" size="sm" disabled={!comment.trim() || commentMutation.isPending} onClick={() => void addComment()}><Send className="me-1 h-3.5 w-3.5" />{t("workItems.reply")}</Button></div></div> : null}</article>; })}</div> : <p className="rounded-md border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">{t("workItems.noComments")}</p>}
            {commentsQuery.data && (commentsQuery.data.totalPages ?? 1) > 1 ? <Pagination page={commentsPage} totalPages={commentsQuery.data.totalPages ?? 1} total={commentsQuery.data.totalCount} pageSize={20} onChange={setCommentsPage} /> : null}
            {!replyTo ? <div className="space-y-2 rounded-xl border border-info/25 bg-info/[0.04] p-3"><RichTextEditor id={`work-item-comment-${item.id}`} value={comment} onChange={setComment} placeholder={t("workItems.commentPlaceholder")} ariaLabel={t("workItems.commentPlaceholder")} t={richTextLabels} /><div className="flex justify-end"><Button type="button" size="sm" disabled={!comment.trim() || commentMutation.isPending} onClick={() => void addComment()}><Send className="me-1 h-3.5 w-3.5" />{commentMutation.isPending ? t("common.loading") : t("workItems.addComment")}</Button></div></div> : null}
          </CardContent></Card></TabsContent>
          <TabsContent value="activity"><Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-3"><CardTitle className="flex items-center gap-2 text-sm"><History className="h-4 w-4 text-primary" />{t("workItems.tabs.activity")}</CardTitle></CardHeader><CardContent className="space-y-3 p-4">{activityQuery.isLoading ? <LoadingState label={t("common.loading")} /> : activityQuery.isError ? <div className="space-y-2"><p role="alert" className="text-sm text-destructive">{t("workItems.activityLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void activityQuery.refetch()}>{t("common.retry")}</Button></div> : (activityQuery.data?.items ?? []).length ? <>{activityQuery.data!.items.map((entry) => <div key={entry.id} className="border-s-2 border-primary/30 ps-3"><div className="flex flex-wrap items-center justify-between gap-2"><Badge variant="secondary">{entry.action}</Badge><time className="text-xs text-muted-foreground">{formatDate(entry.timestamp)}</time></div><p className="mt-1 text-xs text-muted-foreground">{entry.userName ?? entry.userId ?? "—"}{entry.changes ? ` · ${entry.changes}` : ""}</p></div>)}{activityQuery.data && (activityQuery.data.totalPages ?? 1) > 1 ? <Pagination page={activityPage} totalPages={activityQuery.data.totalPages ?? 1} total={activityQuery.data.totalCount} pageSize={20} onChange={setActivityPage} /> : null}</> : <p className="text-sm text-muted-foreground">{t("workItems.noActivity")}</p>}</CardContent></Card></TabsContent>
        </Tabs>
      </>}
    </SideSheetBody>
  </SideSheet>;
}

function Field({ label, value, icon }: { label: string; value: string; icon?: React.ReactNode }) {
  return <div className="space-y-1"><span className="text-xs text-muted-foreground">{label}</span><p className="flex items-center gap-1.5 text-sm font-medium">{icon}{value}</p></div>;
}
