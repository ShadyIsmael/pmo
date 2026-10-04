import * as React from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Download, MessageSquare, Paperclip, Plus, Save, Workflow } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { Badge } from "@components/ui/Badge";
import { Tabs, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import {
  useFullWorkItem, useFullWorkItems, useFullWorkItemActivity, useFullWorkItemAssignees, useFullWorkItemAttachments,
  useFullWorkItemComments, useFullWorkItemCommentMutation, useFullWorkItemMutations, useFullWorkItemUploadMutation, useFullWorkItemWorkflow, workItemsApiFull,
  type SaveWorkItemDto, type WorkItemDto, type WorkItemPriority, type WorkItemType,
} from "@api/workItemsFull";

const itemTypes: WorkItemType[] = ["Task", "Issue", "Subtask", "Bug", "Feature", "Epic", "Story", "Ticket", "TestCase"];
const priorities: WorkItemPriority[] = ["Critical", "High", "Medium", "Low", "Backlog"];

function ErrorState({ message }: { message: string }) { return <p role="alert" className="py-8 text-center text-sm text-destructive">{message}</p>; }
function LoadingState({ label }: { label: string }) { return <p className="py-8 text-center text-sm text-muted-foreground">{label}</p>; }

function emptyDraft(): SaveWorkItemDto { return { title: "", description: "", type: "Task", priority: "Medium", dueDate: "" }; }

export function WorkItemsPageFull() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useFullWorkItems({ page: 1, pageSize: 100 });
  const mutations = useFullWorkItemMutations();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create) && canMutate && !effectiveReadOnly);
  const [createOpen, setCreateOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto>(emptyDraft());
  const items = query.data?.items ?? [];

  function updateDraft(patch: Partial<SaveWorkItemDto>) { setDraft((current) => ({ ...current, ...patch })); }
  function save() { if (!draft.title.trim()) return; mutations.create.mutate({ ...draft, title: draft.title.trim(), dueDate: draft.dueDate || undefined }, { onSuccess: (item) => { setCreateOpen(false); setDraft(emptyDraft()); navigate(`/work-items/${encodeURIComponent(item.id)}`); } }); }

  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.workItems")} description={t("workItems.listDescription", { total: query.data?.totalCount ?? items.length })} actions={canManage ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("workItems.create")}</Button> : undefined} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <LoadingState label={t("common.loading")} /> : query.isError ? <ErrorState message={t("common.errorLoading")} /> : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p> : (
          <Table><TableHeader><TableRow><TableHead>{t("common.title")}</TableHead><TableHead>{t("common.type")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("workItems.assignee")}</TableHead><TableHead>{t("workItems.dueDate")}</TableHead></TableRow></TableHeader><TableBody>
            {items.map((item) => <TableRow key={item.id}><TableCell><Link to={`/work-items/${encodeURIComponent(item.id)}`} className="font-semibold hover:underline">{item.title}</Link></TableCell><TableCell>{item.type}</TableCell><TableCell><Badge status={item.status}>{item.status}</Badge></TableCell><TableCell>{item.assignedTeamName ?? item.assignedResourceName ?? t("workItems.unassigned")}</TableCell><TableCell>{item.dueDate ? new Date(item.dueDate).toLocaleDateString() : "—"}</TableCell></TableRow>)}
          </TableBody></Table>
        )}
      </CardContent></Card>
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("workItems.create")}>
        <SideSheetBody className="space-y-4"><WorkItemForm draft={draft} onChange={updateDraft} t={t} /></SideSheetBody>
        <SideSheetFooter><Button variant="outline" onClick={() => setCreateOpen(false)}>{t("common.cancel")}</Button><Button onClick={save} disabled={mutations.create.isPending || !draft.title.trim()}><Save className="me-1.5 h-4 w-4" />{t("common.save")}</Button></SideSheetFooter>
      </SideSheet>
    </div>
  );
}

function WorkItemForm({ draft, onChange, t, assignees = [], currentItem }: { draft: SaveWorkItemDto; onChange: (patch: Partial<SaveWorkItemDto>) => void; t: (key: string, params?: Record<string, string | number>) => string; assignees?: { targetType: "Resource" | "Team"; targetId: string; name: string }[]; currentItem?: WorkItemDto }) {
  const target = draft.assignedResourceId ? `resource:${draft.assignedResourceId}` : draft.assignedTeamId ? `team:${draft.assignedTeamId}` : "";
  return <>
    <div className="space-y-1.5"><Label>{t("common.title")} *</Label><Input value={draft.title} onChange={(e) => onChange({ title: e.target.value })} required /></div>
    <div className="space-y-1.5"><Label>{t("common.type")}</Label><select className="input-field" value={draft.type} onChange={(e) => onChange({ type: e.target.value as WorkItemType })}>{itemTypes.map((type) => <option key={type}>{type}</option>)}</select></div>
    <div className="space-y-1.5"><Label>{t("common.priority")}</Label><select className="input-field" value={draft.priority} onChange={(e) => onChange({ priority: e.target.value as WorkItemPriority })}>{priorities.map((priority) => <option key={priority}>{priority}</option>)}</select></div>
    <div className="space-y-1.5"><Label>{t("workItems.dueDate")}</Label><Input type="date" value={draft.dueDate?.slice(0, 10) ?? ""} onChange={(e) => onChange({ dueDate: e.target.value || undefined })} /></div>
    {assignees.length ? <div className="space-y-1.5"><Label>{t("workItems.assignee")}</Label><select className="input-field" value={target} onChange={(e) => { const [kind, id] = e.target.value.split(":"); onChange({ assignedResourceId: kind === "resource" ? id : undefined, assignedTeamId: kind === "team" ? id : undefined }); }}><option value="">{t("workItems.unassigned")}</option>{assignees.map((candidate) => <option key={`${candidate.targetType}:${candidate.targetId}`} value={`${candidate.targetType === "Resource" ? "resource" : "team"}:${candidate.targetId}`}>{candidate.name} · {candidate.targetType}</option>)}</select></div> : null}
    {currentItem?.projectName ? <p className="text-xs text-muted-foreground">{t("workItems.project")}: {currentItem.projectName}</p> : null}
    <div className="space-y-1.5"><Label>{t("workItems.description")}</Label><Textarea value={draft.description ?? ""} onChange={(e) => onChange({ description: e.target.value })} rows={5} /></div>
  </>;
}

export function WorkItemDetailPageFull() {
  const { id } = useParams<{ id: string }>();
  const { t, locale, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useFullWorkItem(id, true);
  const item = query.data;
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit) && canMutate && !effectiveReadOnly);
  const [tab, setTab] = React.useState("details");
  const [editOpen, setEditOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto | null>(null);
  const mutations = useFullWorkItemMutations();
  const commentMutation = useFullWorkItemCommentMutation(id ?? "");
  const uploadMutation = useFullWorkItemUploadMutation(id ?? "");
  const assignees = useFullWorkItemAssignees(item?.projectId, editOpen);
  const workflow = useFullWorkItemWorkflow(id, tab === "details");
  const comments = useFullWorkItemComments(id, tab === "comments");
  const activity = useFullWorkItemActivity(id, tab === "activity");
  const attachments = useFullWorkItemAttachments(id, tab === "attachments");
  const [comment, setComment] = React.useState("");
  const [replyTo, setReplyTo] = React.useState<string | undefined>();
  const [error, setError] = React.useState("");

  if (query.isLoading) return <LoadingState label={t("common.loading")} />;
  if (query.isError || !item) return <ErrorState message={t("common.notFound")} />;
  const items = comments.data?.items ?? [];
  const itemDraft = draft ?? { title: item.title, description: item.description, type: item.type, priority: item.priority, projectId: item.projectId, assignedResourceId: item.assignedResourceId, assignedTeamId: item.assignedTeamId, dueDate: item.dueDate, startDate: item.startDate, rowVersion: item.rowVersion };
  function updateDraft(patch: Partial<SaveWorkItemDto>) { setDraft((current) => ({ ...itemDraft, ...current, ...patch })); }
  async function saveEdit() { if (!draft || !id) return; setError(""); try { await mutations.update.mutateAsync({ id, body: { ...draft, title: draft.title.trim(), dueDate: draft.dueDate || undefined } }); setEditOpen(false); setDraft(null); await query.refetch(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("common.errorLoading")); } }
  async function addComment() { if (!id || !comment.trim()) return; try { await commentMutation.mutateAsync({ commentText: comment.trim(), parentCommentId: replyTo }); setComment(""); setReplyTo(undefined); } catch (cause) { setError(cause instanceof Error ? cause.message : t("common.errorLoading")); } }
  async function upload(files: FileList | null) { if (!id || !files?.length) return; try { await uploadMutation.mutateAsync(Array.from(files)); } catch (cause) { setError(cause instanceof Error ? cause.message : t("common.errorLoading")); } }
  async function download(attachmentId: string, fileName: string) { if (!id) return; const blob = await workItemsApiFull.downloadAttachment(id, attachmentId); const href = URL.createObjectURL(blob); const link = document.createElement("a"); link.href = href; link.download = fileName; link.click(); URL.revokeObjectURL(href); }

  return <div className="space-y-6">
    <PageHeader title={item.title} description={`${item.type} · ${item.projectName ?? ""}`} actions={canManage ? <Button size="sm" variant="outline" onClick={() => { setDraft(itemDraft); setEditOpen(true); }}>{t("common.edit")}</Button> : undefined} />
    {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    <div className="flex flex-wrap gap-2"><Badge status={item.status}>{item.status}</Badge><Badge status={item.priority}>{item.priority}</Badge><span className="text-sm text-muted-foreground">{item.assignedTeamName ?? item.assignedResourceName ?? t("workItems.unassigned")}</span></div>
    <Tabs defaultValue={tab}><TabsList>{["details", "comments", "activity", "attachments"].map((value) => <TabsTrigger key={value} value={value} onClick={() => setTab(value)}>{t(`workItems.tabs.${value}`)}</TabsTrigger>)}</TabsList></Tabs>
    {tab === "details" ? <div className="grid gap-4 lg:grid-cols-2"><Card><CardHeader><CardTitle>{t("workItems.details")}</CardTitle></CardHeader><CardContent className="space-y-3 text-sm"><p>{t("workItems.status")}: <strong>{item.status}</strong></p><p>{t("common.priority")}: {item.priority}</p><p>{t("workItems.assignee")}: {item.assignedTeamName ?? item.assignedResourceName ?? t("workItems.unassigned")}</p><p>{t("workItems.dueDate")}: {item.dueDate ? formatDate(item.dueDate) : "—"}</p><p className="whitespace-pre-wrap">{item.description || "—"}</p></CardContent></Card><Card><CardHeader><CardTitle className="flex items-center gap-2"><Workflow className="h-4 w-4" />{t("workItems.workflow")}</CardTitle></CardHeader><CardContent className="flex flex-wrap gap-2">{workflow.isLoading ? <LoadingState label={t("common.loading")} /> : workflow.availableTransitions.length ? workflow.availableTransitions.map((transition) => <Button key={transition.code} size="sm" variant="outline" disabled={!canManage || !transition.canExecute || workflow.execute.isPending} onClick={() => workflow.execute.mutate({ transitionCode: transition.code })}>{locale === "ar" ? transition.nameAr : transition.nameEn}</Button>) : <p className="text-sm text-muted-foreground">{t("workItems.noTransitions")}</p>}</CardContent></Card></div> : null}
    {tab === "comments" ? <Card><CardHeader><CardTitle className="flex items-center gap-2"><MessageSquare className="h-4 w-4" />{t("workItems.comments")}</CardTitle></CardHeader><CardContent className="space-y-4"><div className="space-y-2">{comments.isLoading ? <LoadingState label={t("common.loading")} /> : items.length ? items.map((entry) => <div key={entry.id} className="rounded-md border p-3"><p className="text-sm">{entry.commentText}</p><p className="mt-1 text-xs text-muted-foreground">{entry.authorName ?? entry.createdBy} · {formatDate(entry.createdAt)}</p><Button size="sm" variant="ghost" onClick={() => setReplyTo(entry.id)}>{t("workItems.reply")}</Button></div>) : <p className="text-sm text-muted-foreground">{t("workItems.noComments")}</p>}</div>{canManage ? <div className="space-y-2"><Textarea value={comment} onChange={(e) => setComment(e.target.value)} placeholder={replyTo ? t("workItems.replyPlaceholder") : t("workItems.commentPlaceholder")} rows={3} /><div className="flex gap-2"><Button size="sm" onClick={() => void addComment()} disabled={!comment.trim() || commentMutation.isPending}>{t("workItems.addComment")}</Button>{replyTo ? <Button size="sm" variant="ghost" onClick={() => setReplyTo(undefined)}>{t("common.cancel")}</Button> : null}</div></div> : null}</CardContent></Card> : null}
    {tab === "activity" ? <Card><CardHeader><CardTitle>{t("workItems.activity")}</CardTitle></CardHeader><CardContent className="space-y-3">{activity.isLoading ? <LoadingState label={t("common.loading")} /> : (activity.data?.items ?? []).length ? (activity.data?.items ?? []).map((entry) => <div key={entry.id} className="border-s-2 border-primary/30 ps-3 text-sm"><div className="flex justify-between gap-2"><strong>{entry.action}</strong><span className="text-xs text-muted-foreground">{formatDate(entry.timestamp)}</span></div><p className="text-xs text-muted-foreground">{entry.userName ?? entry.userId ?? "—"}{entry.changes ? ` · ${entry.changes}` : ""}</p></div>) : <p className="text-sm text-muted-foreground">{t("workItems.noActivity")}</p>}</CardContent></Card> : null}
    {tab === "attachments" ? <Card><CardHeader><CardTitle className="flex items-center gap-2"><Paperclip className="h-4 w-4" />{t("workItems.attachments")}</CardTitle></CardHeader><CardContent className="space-y-3">{canManage ? <Input type="file" multiple onChange={(e) => void upload(e.target.files)} /> : null}{attachments.isLoading ? <LoadingState label={t("common.loading")} /> : (attachments.data ?? []).map((file) => <div key={file.id} className="flex items-center justify-between gap-2 rounded-md border p-2 text-sm"><span className="truncate">{file.fileName}</span><Button size="sm" variant="ghost" onClick={() => void download(file.id, file.fileName)}><Download className="me-1 h-3.5 w-3.5" />{t("workItems.download")}</Button></div>)}{!attachments.isLoading && !(attachments.data ?? []).length ? <p className="text-sm text-muted-foreground">{t("workItems.noAttachments")}</p> : null}</CardContent></Card> : null}
    <SideSheet open={editOpen} onOpenChange={setEditOpen} title={t("common.edit")}><SideSheetBody className="space-y-4"><WorkItemForm draft={itemDraft} onChange={updateDraft} t={t} assignees={assignees.data ?? []} currentItem={item} /></SideSheetBody><SideSheetFooter><Button variant="outline" onClick={() => setEditOpen(false)}>{t("common.cancel")}</Button><Button onClick={() => void saveEdit()} disabled={mutations.update.isPending}><Save className="me-1.5 h-4 w-4" />{t("common.save")}</Button></SideSheetFooter></SideSheet>
  </div>;
}
