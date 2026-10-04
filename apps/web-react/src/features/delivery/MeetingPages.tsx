import * as React from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { Check, Download, MessageSquare, Paperclip, Plus, Save, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { Badge } from "@components/ui/Badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasAnyPermission, Permissions } from "@lib/permissions";
import { useFullMeeting, useFullMeetings, useMeetingFullMutations, useMeetingMinutes, useMeetingMinutesAttachments, useMeetingMinutesChecklist, useMeetingMinutesComments, meetingsApiFull, type MeetingInput } from "@api/meetingsFull";

function Loading({ text }: { text: string }) { return <p className="py-8 text-center text-sm text-muted-foreground">{text}</p>; }
function ErrorMessage({ text }: { text: string }) { return <p role="alert" className="py-8 text-center text-sm text-destructive">{text}</p>; }

export function MeetingsPageFull() {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useFullMeetings({ page: 1, pageSize: 100 });
  const mutations = useMeetingFullMutations();
  const canManage = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Meeting.Create, Permissions.Meeting.Manage]) && canMutate && !effectiveReadOnly);
  const navigate = useNavigate();
  const [open, setOpen] = React.useState(false);
  const [draft, setDraft] = React.useState({ title: "", objective: "", startTime: "", endTime: "", location: "" });
  const items = query.data?.items ?? [];
  function save() { if (!draft.title.trim() || !draft.startTime || !draft.endTime || !user) return; const body: MeetingInput = { ...draft, title: draft.title.trim(), organizerId: user.id, attendees: [] }; mutations.create.mutate(body, { onSuccess: (meeting) => { setOpen(false); setDraft({ title: "", objective: "", startTime: "", endTime: "", location: "" }); navigate(`/meetings/${encodeURIComponent(meeting.id)}`); } }); }
  return <div className="space-y-6">
    <PageHeader title={t("meetings.title")} description={t("meetings.description", { count: query.data?.totalCount ?? items.length })} actions={canManage ? <Button size="sm" onClick={() => setOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("meetings.createMeeting")}</Button> : undefined} />
    <Card><CardContent className="p-0">{query.isLoading ? <Loading text={t("common.loading")} /> : query.isError ? <ErrorMessage text={t("meetings.loadError")} /> : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("meetings.emptyTitle")}</p> : <Table><TableHeader><TableRow><TableHead>{t("meetings.titleField")}</TableHead><TableHead>{t("meetings.dateTime")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("meetings.attendees")}</TableHead><TableHead>{t("meetings.minutesTitle")}</TableHead></TableRow></TableHeader><TableBody>{items.map((meeting) => <TableRow key={meeting.id}><TableCell><Link to={`/meetings/${encodeURIComponent(meeting.id)}`} className="font-semibold hover:underline">{meeting.title}</Link></TableCell><TableCell>{formatDate(meeting.startTime)}</TableCell><TableCell><Badge status={meeting.status ?? ""}>{meeting.status ?? "—"}</Badge></TableCell><TableCell>{meeting.attendeeCount}</TableCell><TableCell>{meeting.minutesState ? t(`meetings.minutes.${meeting.minutesState.toLowerCase()}`) : t("meetings.notAvailable")}</TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card>
    <SideSheet open={open} onOpenChange={setOpen} title={t("meetings.createMeeting")}><SideSheetBody className="space-y-4"><div className="space-y-1.5"><Label>{t("meetings.titleField")} *</Label><Input value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} /></div><div className="space-y-1.5"><Label>{t("meetings.objective")}</Label><Textarea value={draft.objective} onChange={(e) => setDraft({ ...draft, objective: e.target.value })} /></div><div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label>{t("meetings.start")} *</Label><Input type="datetime-local" value={draft.startTime} onChange={(e) => setDraft({ ...draft, startTime: e.target.value })} /></div><div className="space-y-1.5"><Label>{t("meetings.end")} *</Label><Input type="datetime-local" value={draft.endTime} onChange={(e) => setDraft({ ...draft, endTime: e.target.value })} /></div></div><div className="space-y-1.5"><Label>{t("meetings.location")}</Label><Input value={draft.location} onChange={(e) => setDraft({ ...draft, location: e.target.value })} /></div></SideSheetBody><SideSheetFooter><Button variant="outline" onClick={() => setOpen(false)}>{t("common.cancel")}</Button><Button onClick={save} disabled={mutations.create.isPending || !draft.title.trim()}><Save className="me-1.5 h-4 w-4" />{t("common.save")}</Button></SideSheetFooter></SideSheet>
  </div>;
}

export function MeetingDetailPageFull() {
  const { id } = useParams<{ id: string }>();
  const { t, formatDate } = useI18n();
  const location = useLocation();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useFullMeeting(id);
  const [tab, setTab] = React.useState<"details" | "minutes">(location.pathname.endsWith("/minutes") ? "minutes" : "details");
  const canManage = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Meeting.Edit, Permissions.Meeting.Manage]) && canMutate && !effectiveReadOnly);
  if (query.isLoading) return <Loading text={t("common.loading")} />;
  if (query.isError || !query.data) return <ErrorMessage text={t("meetings.notFoundTitle")} />;
  const meeting = query.data;
  return <div className="space-y-6"><PageHeader title={meeting.title} description={formatDate(meeting.startTime)} actions={<Link to={`/meetings/${encodeURIComponent(meeting.id)}/minutes`} className="button-outline">{t("meetings.enterMinutes")}</Link>} /><div className="flex gap-2"><Button size="sm" variant={tab === "details" ? "default" : "outline"} onClick={() => setTab("details")}>{t("meetings.details")}</Button><Button size="sm" variant={tab === "minutes" ? "default" : "outline"} onClick={() => setTab("minutes")}>{t("meetings.minutesTitle")}</Button></div>{tab === "details" ? <Card><CardHeader><CardTitle>{t("meetings.details")}</CardTitle></CardHeader><CardContent className="grid gap-4 text-sm sm:grid-cols-2"><p>{t("meetings.project")}: {meeting.projectName ?? t("meetings.noProject")}</p><p>{t("common.status")}: {meeting.status ?? "—"}</p><p>{t("meetings.organizer")}: {meeting.organizerName ?? meeting.organizerId ?? "—"}</p><p>{t("meetings.location")}: {meeting.location ?? t("meetings.noLocation")}</p><p>{t("meetings.from")}: {formatDate(meeting.startTime)}</p><p>{t("meetings.to")}: {formatDate(meeting.endTime)}</p><p className="sm:col-span-2">{t("meetings.objective")}: {meeting.objective ?? t("meetings.noObjective")}</p><div className="sm:col-span-2"><h3 className="mb-2 font-semibold">{t("meetings.attendees")}</h3>{meeting.attendees.length ? <ul className="space-y-1">{meeting.attendees.map((attendee) => <li key={attendee.id}>{attendee.displayName ?? attendee.email}</li>)}</ul> : <p className="text-muted-foreground">{t("meetings.noUsers")}</p>}</div></CardContent></Card> : <MeetingMinutesPanel meetingId={meeting.id} canManage={canManage} />}</div>;
}

function MeetingMinutesPanel({ meetingId, canManage }: { meetingId: string; canManage: boolean }) {
  const { t, formatDate } = useI18n();
  const minutes = useMeetingMinutes(meetingId, true);
  const checklist = useMeetingMinutesChecklist(meetingId, true);
  const attachments = useMeetingMinutesAttachments(meetingId, true);
  const comments = useMeetingMinutesComments(meetingId, true);
  const mutations = useMeetingFullMutations(meetingId);
  const [draft, setDraft] = React.useState({ summary: "", discussionNotes: "", decisions: "", outcomes: "" });
  const [newChecklist, setNewChecklist] = React.useState("");
  const [comment, setComment] = React.useState("");
  const [error, setError] = React.useState("");
  React.useEffect(() => { if (minutes.data) setDraft({ summary: minutes.data.summary ?? "", discussionNotes: minutes.data.discussionNotes ?? "", decisions: minutes.data.decisions ?? "", outcomes: minutes.data.outcomes ?? "" }); }, [minutes.data]);
  const isFinal = minutes.data?.state === "Final";
  async function saveMinutes() { setError(""); try { if (minutes.data) await mutations.updateMinutes.mutateAsync({ ...draft, rowVersion: minutes.data.rowVersion }); else await mutations.createMinutes.mutateAsync(draft); } catch (cause) { setError(cause instanceof Error ? cause.message : t("meetings.minutesSaveError")); } }
  async function finalize() { if (!minutes.data) return; try { await mutations.finalizeMinutes.mutateAsync(minutes.data.rowVersion); } catch (cause) { setError(cause instanceof Error ? cause.message : t("meetings.minutesFinalizeError")); } }
  async function addChecklist() { if (!newChecklist.trim()) return; await mutations.createChecklist.mutateAsync(newChecklist.trim()); setNewChecklist(""); }
  async function upload(files: FileList | null) { if (files?.length) await mutations.uploadAttachments.mutateAsync(Array.from(files)); }
  async function download(fileId: string, name: string) { const blob = await meetingsApiFull.downloadAttachment(meetingId, fileId); const url = URL.createObjectURL(blob); const anchor = document.createElement("a"); anchor.href = url; anchor.download = name; anchor.click(); URL.revokeObjectURL(url); }
  async function addComment() { if (!comment.trim()) return; await mutations.addComment.mutateAsync({ commentText: comment.trim() }); setComment(""); }
  return <div className="space-y-6">{error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}<Card><CardHeader><CardTitle>{t("meetings.minutesTitle")}</CardTitle></CardHeader><CardContent className="space-y-3"><p className="text-sm text-muted-foreground">{minutes.data ? `${t("meetings.minutesRecordedAt")}: ${formatDate(minutes.data.recordedAt)}` : t("meetings.minutesEditorHint")}</p>{(["summary", "discussionNotes", "decisions", "outcomes"] as const).map((field) => <div key={field} className="space-y-1.5"><Label>{t(`meetings.${field}`)}</Label><Textarea value={draft[field]} disabled={!canManage || isFinal} onChange={(e) => setDraft({ ...draft, [field]: e.target.value })} rows={field === "summary" ? 3 : 5} /></div>)}<div className="flex gap-2">{canManage && !isFinal ? <Button onClick={() => void saveMinutes()} disabled={mutations.createMinutes.isPending || mutations.updateMinutes.isPending}><Save className="me-1.5 h-4 w-4" />{t("common.save")}</Button> : null}{canManage && minutes.data && !isFinal ? <Button variant="outline" onClick={() => void finalize()} disabled={mutations.finalizeMinutes.isPending}>{t("meetings.finalizeMinutes")}</Button> : null}<Badge status={minutes.data?.state ?? "Draft"}>{minutes.data?.state ?? t("meetings.minutes.notAvailable")}</Badge></div></CardContent></Card>
    <div className="grid gap-6 lg:grid-cols-2"><Card><CardHeader><CardTitle className="flex items-center gap-2"><Check className="h-4 w-4" />{t("meetings.checklistTitle")}</CardTitle></CardHeader><CardContent className="space-y-3">{canManage && !isFinal ? <div className="flex gap-2"><Input value={newChecklist} onChange={(e) => setNewChecklist(e.target.value)} placeholder={t("meetings.checklistPlaceholder")} /><Button size="sm" onClick={() => void addChecklist()}><Plus className="me-1 h-4 w-4" />{t("meetings.addChecklistItem")}</Button></div> : null}{checklist.isLoading ? <Loading text={t("common.loading")} /> : (checklist.data ?? []).length ? (checklist.data ?? []).map((item) => <div key={item.id} className="flex items-center gap-2 rounded border p-2"><input type="checkbox" checked={item.isCompleted} disabled={!canManage || isFinal} onChange={(e) => void mutations.updateChecklist.mutateAsync({ itemId: item.id, body: { title: item.title, isCompleted: e.target.checked } })} /><span className={item.isCompleted ? "text-sm line-through text-muted-foreground" : "text-sm"}>{item.title}</span>{canManage && !isFinal ? <Button size="sm" variant="ghost" className="ms-auto" onClick={() => void mutations.deleteChecklist.mutateAsync(item.id)}><Trash2 className="h-3.5 w-3.5" /></Button> : null}</div>) : <p className="text-sm text-muted-foreground">{t("meetings.checklistEmpty")}</p>}</CardContent></Card>
      <Card><CardHeader><CardTitle className="flex items-center gap-2"><Paperclip className="h-4 w-4" />{t("meetings.attachmentsTitle")}</CardTitle></CardHeader><CardContent className="space-y-3">{canManage && !isFinal ? <Input type="file" multiple onChange={(e) => void upload(e.target.files)} /> : null}{attachments.isLoading ? <Loading text={t("common.loading")} /> : (attachments.data ?? []).map((file) => <div key={file.id} className="flex items-center justify-between gap-2 rounded border p-2 text-sm"><span className="truncate">{file.fileName}</span><div className="flex gap-1"><Button size="sm" variant="ghost" onClick={() => void download(file.id, file.fileName)}><Download className="me-1 h-3.5 w-3.5" />{t("meetings.downloadAttachment")}</Button>{canManage && !isFinal ? <Button size="sm" variant="ghost" onClick={() => void mutations.deleteAttachment.mutateAsync(file.id)}><Trash2 className="h-3.5 w-3.5" /></Button> : null}</div></div>)}{!attachments.isLoading && !(attachments.data ?? []).length ? <p className="text-sm text-muted-foreground">{t("meetings.attachmentsEmpty")}</p> : null}</CardContent></Card>
    </div><Card><CardHeader><CardTitle className="flex items-center gap-2"><MessageSquare className="h-4 w-4" />{t("meetings.commentsTitle")}</CardTitle></CardHeader><CardContent className="space-y-3">{canManage && !isFinal ? <div className="flex gap-2"><Textarea value={comment} onChange={(e) => setComment(e.target.value)} placeholder={t("meetings.commentPlaceholder")} rows={2} /><Button size="sm" onClick={() => void addComment()}><MessageSquare className="me-1 h-4 w-4" />{t("meetings.addComment")}</Button></div> : null}{comments.isLoading ? <Loading text={t("common.loading")} /> : (comments.data ?? []).length ? (comments.data ?? []).map((entry) => <div key={entry.id} className="rounded border p-3"><p className="text-sm">{entry.commentText}</p><p className="mt-1 text-xs text-muted-foreground">{entry.authorName ?? entry.createdBy} · {formatDate(entry.createdAt)}</p></div>) : <p className="text-sm text-muted-foreground">{t("meetings.commentsEmpty")}</p>}</CardContent></Card></div>;
}
