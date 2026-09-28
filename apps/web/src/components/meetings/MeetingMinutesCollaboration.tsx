"use client";

import { useMemo, useRef, useState, type ReactNode } from "react";
import { Check, ChevronDown, ChevronUp, Download, File, FileImage, FileText, MessageSquare, Paperclip, Pencil, Plus, Reply, Send, Trash2, Upload } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { formatDateTime } from "@/lib/utils";
import { meetingApi, type MeetingMinutesAttachmentDto, type MeetingMinutesChecklistItemDto, type MeetingMinutesCommentDto } from "@/lib/api/meetings";
import {
  useCreateMeetingMinutesChecklistItem,
  useCreateMeetingMinutesComment,
  useDeleteMeetingMinutesAttachment,
  useDeleteMeetingMinutesChecklistItem,
  useMeetingMinutesAttachments,
  useMeetingMinutesChecklist,
  useMeetingMinutesComments,
  useReorderMeetingMinutesChecklist,
  useUpdateMeetingMinutesChecklistItem,
  useUploadMeetingMinutesAttachments,
} from "@/hooks/meetings/useMeetings";

type MeetingsT = ReturnType<typeof useTranslations>;

export function MeetingMinutesCollaboration({ meetingId, canManage, locale, mainContent }: { meetingId: string; canManage: boolean; locale: string; mainContent: ReactNode }) {
  const t = useTranslations("meetings");
  const checklistQuery = useMeetingMinutesChecklist(meetingId);
  const attachmentsQuery = useMeetingMinutesAttachments(meetingId);
  const commentsQuery = useMeetingMinutesComments(meetingId);
  const createChecklist = useCreateMeetingMinutesChecklistItem(meetingId);
  const updateChecklist = useUpdateMeetingMinutesChecklistItem(meetingId);
  const deleteChecklist = useDeleteMeetingMinutesChecklistItem(meetingId);
  const reorderChecklist = useReorderMeetingMinutesChecklist(meetingId);
  const uploadAttachments = useUploadMeetingMinutesAttachments(meetingId);
  const deleteAttachment = useDeleteMeetingMinutesAttachment(meetingId);
  const createComment = useCreateMeetingMinutesComment(meetingId);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [newItemTitle, setNewItemTitle] = useState("");
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState("");
  const [commentText, setCommentText] = useState("");
  const [replyTo, setReplyTo] = useState<string | null>(null);
  const [replyText, setReplyText] = useState("");
  const [expandedReplies, setExpandedReplies] = useState<Record<string, boolean>>({});
  const [error, setError] = useState<string | null>(null);

  const checklist = checklistQuery.data ?? [];
  const attachments = attachmentsQuery.data ?? [];
  const comments = useMemo(() => commentsQuery.data ?? [], [commentsQuery.data]);
  const roots = useMemo(() => comments.filter((comment) => !comment.parentCommentId), [comments]);
  const replies = useMemo(() => comments.reduce<Record<string, MeetingMinutesCommentDto[]>>((grouped, comment) => {
    if (comment.parentCommentId) (grouped[comment.parentCommentId] ??= []).push(comment);
    return grouped;
  }, {}), [comments]);
  const busy = createChecklist.isPending || updateChecklist.isPending || deleteChecklist.isPending || reorderChecklist.isPending || uploadAttachments.isPending || deleteAttachment.isPending || createComment.isPending;

  const showError = (message: string) => setError(message);

  const addChecklistItem = async () => {
    const title = newItemTitle.trim();
    if (!title) { showError(t("checklistTitleRequired")); return; }
    setError(null);
    try { await createChecklist.mutateAsync({ title }); setNewItemTitle(""); } catch { showError(t("checklistSaveError")); }
  };

  const saveChecklistItem = async (item: MeetingMinutesChecklistItemDto) => {
    const title = editingTitle.trim();
    if (!title) { showError(t("checklistTitleRequired")); return; }
    setError(null);
    try { await updateChecklist.mutateAsync({ itemId: item.id, body: { title, isCompleted: item.isCompleted } }); setEditingItemId(null); } catch { showError(t("checklistSaveError")); }
  };

  const toggleChecklistItem = async (item: MeetingMinutesChecklistItemDto, isCompleted: boolean) => {
    setError(null);
    try { await updateChecklist.mutateAsync({ itemId: item.id, body: { title: item.title, isCompleted } }); } catch { showError(t("checklistSaveError")); }
  };

  const removeChecklistItem = async (item: MeetingMinutesChecklistItemDto) => {
    if (!window.confirm(t("deleteChecklistConfirm"))) return;
    setError(null);
    try { await deleteChecklist.mutateAsync(item.id); } catch { showError(t("checklistDeleteError")); }
  };

  const moveChecklistItem = async (index: number, direction: -1 | 1) => {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= checklist.length) return;
    const itemIds = checklist.map((item) => item.id);
    [itemIds[index], itemIds[targetIndex]] = [itemIds[targetIndex], itemIds[index]];
    setError(null);
    try { await reorderChecklist.mutateAsync({ itemIds }); } catch { showError(t("checklistReorderError")); }
  };

  const upload = async (files: File[]) => {
    if (files.length === 0) return;
    setError(null);
    try { await uploadAttachments.mutateAsync(files); } catch { showError(t("attachmentUploadError")); }
  };

  const download = async (attachment: MeetingMinutesAttachmentDto) => {
    setError(null);
    try {
      const blob = await meetingApi.downloadAttachment(meetingId, attachment.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = attachment.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch { showError(t("attachmentDownloadError")); }
  };

  const removeAttachment = async (attachment: MeetingMinutesAttachmentDto) => {
    if (!window.confirm(t("deleteAttachmentConfirm"))) return;
    setError(null);
    try { await deleteAttachment.mutateAsync(attachment.id); } catch { showError(t("attachmentRemoveError")); }
  };

  const submitComment = async (text: string, parentCommentId?: string) => {
    const value = text.trim();
    if (!value) { showError(t("commentRequired")); return; }
    setError(null);
    try {
      await createComment.mutateAsync({ commentText: value, parentCommentId });
      if (parentCommentId) { setReplyTo(null); setReplyText(""); } else setCommentText("");
    } catch { showError(t("commentSaveError")); }
  };

  return <div className="space-y-6">
    {error ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(280px,360px)]" dir="ltr">
      <div className="min-w-0 space-y-6" dir={locale === "ar" ? "rtl" : "ltr"}>
        {mainContent}
      </div>
      <aside className="min-w-0 space-y-6" dir={locale === "ar" ? "rtl" : "ltr"}>
        <ChecklistSection t={t} items={checklist} canManage={canManage} busy={busy} loading={checklistQuery.isLoading} hasError={checklistQuery.isError} newTitle={newItemTitle} editingItemId={editingItemId} editingTitle={editingTitle} onNewTitleChange={setNewItemTitle} onEditingTitleChange={setEditingTitle} onAdd={() => void addChecklistItem()} onStartEdit={(item) => { setEditingItemId(item.id); setEditingTitle(item.title); setError(null); }} onCancelEdit={() => setEditingItemId(null)} onSaveEdit={(item) => void saveChecklistItem(item)} onToggle={(item, checked) => void toggleChecklistItem(item, checked)} onDelete={(item) => void removeChecklistItem(item)} onMove={(index, direction) => void moveChecklistItem(index, direction)} />
        <AttachmentsSection t={t} attachments={attachments} canManage={canManage} busy={busy} loading={attachmentsQuery.isLoading} hasError={attachmentsQuery.isError} inputRef={fileInputRef} onChoose={() => fileInputRef.current?.click()} onFiles={(files) => void upload(files)} onDownload={(attachment) => void download(attachment)} onDelete={(attachment) => void removeAttachment(attachment)} />
      </aside>
      <div className="min-w-0 lg:col-span-2" dir={locale === "ar" ? "rtl" : "ltr"}>
        <CommentsSection t={t} comments={comments} roots={roots} replies={replies} canManage={canManage} busy={busy} loading={commentsQuery.isLoading} hasError={commentsQuery.isError} locale={locale} commentText={commentText} replyTo={replyTo} replyText={replyText} expandedReplies={expandedReplies} onCommentTextChange={setCommentText} onReplyTextChange={setReplyText} onSubmitComment={() => void submitComment(commentText)} onSubmitReply={(parentId) => void submitComment(replyText, parentId)} onReply={(commentId) => { setReplyTo((current) => current === commentId ? null : commentId); setError(null); }} onCancelReply={() => { setReplyTo(null); setReplyText(""); }} onToggleReplies={(commentId) => setExpandedReplies((current) => ({ ...current, [commentId]: !current[commentId] }))} />
      </div>
    </div>
  </div>;
}

function ChecklistSection({ t, items, canManage, busy, loading, hasError, newTitle, editingItemId, editingTitle, onNewTitleChange, onEditingTitleChange, onAdd, onStartEdit, onCancelEdit, onSaveEdit, onToggle, onDelete, onMove }: { t: MeetingsT; items: MeetingMinutesChecklistItemDto[]; canManage: boolean; busy: boolean; loading: boolean; hasError: boolean; newTitle: string; editingItemId: string | null; editingTitle: string; onNewTitleChange: (_value: string) => void; onEditingTitleChange: (_value: string) => void; onAdd: () => void; onStartEdit: (_item: MeetingMinutesChecklistItemDto) => void; onCancelEdit: () => void; onSaveEdit: (_item: MeetingMinutesChecklistItemDto) => void; onToggle: (_item: MeetingMinutesChecklistItemDto, _checked: boolean) => void; onDelete: (_item: MeetingMinutesChecklistItemDto) => void; onMove: (_index: number, _direction: -1 | 1) => void }) {
  return <Card><CardHeader className="border-b bg-muted/10"><CardTitle className="flex items-center gap-2 text-base"><Check className="h-4 w-4 text-primary" />{t("checklistTitle")}</CardTitle><CardDescription>{t("checklistDescription")}</CardDescription></CardHeader><CardContent className="space-y-3 p-4">
    {canManage ? <div className="flex gap-2"><Textarea value={newTitle} onChange={(event) => onNewTitleChange(event.target.value)} placeholder={t("checklistPlaceholder")} rows={1} className="min-h-10 resize-none" onKeyDown={(event) => { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); onAdd(); } }} /><Button type="button" size="sm" className="shrink-0 self-end" disabled={busy} onClick={onAdd}><Plus className="me-1.5 h-4 w-4" />{t("addChecklistItem")}</Button></div> : null}
    {loading ? <div className="space-y-2"><Skeleton className="h-12" /><Skeleton className="h-12" /></div> : hasError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("checklistLoadError")}</p> : items.length === 0 ? <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("checklistEmpty")}</p> : <div className="space-y-2">{items.map((item, index) => <div key={item.id} className="flex items-center gap-2 rounded-lg border bg-background p-2.5"><Checkbox checked={item.isCompleted} disabled={!canManage || busy} onCheckedChange={(checked) => onToggle(item, checked === true)} aria-label={`${t("completed")}: ${item.title}`} /><div className="min-w-0 flex-1">{editingItemId === item.id ? <div className="flex gap-2"><Textarea value={editingTitle} onChange={(event) => onEditingTitleChange(event.target.value)} rows={1} className="min-h-9 resize-none" onKeyDown={(event) => { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); onSaveEdit(item); } }} /><Button type="button" size="sm" onClick={() => onSaveEdit(item)} disabled={busy}>{t("saveChecklistItem")}</Button><Button type="button" size="sm" variant="ghost" onClick={onCancelEdit}>{t("cancel")}</Button></div> : <p className={`text-sm ${item.isCompleted ? "text-muted-foreground line-through" : "font-medium"}`}>{item.title}</p>}</div>{canManage && editingItemId !== item.id ? <div className="flex shrink-0 items-center gap-0.5"><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={t("moveUp")} disabled={busy || index === 0} onClick={() => onMove(index, -1)}><ChevronUp className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={t("moveDown")} disabled={busy || index === items.length - 1} onClick={() => onMove(index, 1)}><ChevronDown className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={t("editChecklistItem")} disabled={busy} onClick={() => onStartEdit(item)}><Pencil className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={t("deleteChecklistItem")} disabled={busy} onClick={() => onDelete(item)}><Trash2 className="h-4 w-4" /></Button></div> : null}</div>)}</div>}
  </CardContent></Card>;
}

function AttachmentsSection({ t, attachments, canManage, busy, loading, hasError, inputRef, onChoose, onFiles, onDownload, onDelete }: { t: MeetingsT; attachments: MeetingMinutesAttachmentDto[]; canManage: boolean; busy: boolean; loading: boolean; hasError: boolean; inputRef: React.RefObject<HTMLInputElement>; onChoose: () => void; onFiles: (_files: File[]) => void; onDownload: (_attachment: MeetingMinutesAttachmentDto) => void; onDelete: (_attachment: MeetingMinutesAttachmentDto) => void }) {
  return <Card><CardHeader className="flex flex-row items-center justify-between gap-3 border-b bg-muted/10"><div><CardTitle className="flex items-center gap-2 text-base"><Paperclip className="h-4 w-4 text-primary" />{t("attachmentsTitle")}</CardTitle><CardDescription>{t("attachmentsDescription")}</CardDescription></div>{canManage ? <><input ref={inputRef} type="file" multiple className="sr-only" onChange={(event) => { onFiles(Array.from(event.target.files ?? [])); event.target.value = ""; }} /><Button type="button" size="sm" variant="outline" disabled={busy} onClick={onChoose}><Upload className="me-1.5 h-4 w-4" />{busy ? t("uploading") : t("uploadAttachment")}</Button></> : null}</CardHeader><CardContent className="space-y-2 p-4">{loading ? <><Skeleton className="h-14" /><Skeleton className="h-14" /></> : hasError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("attachmentsLoadError")}</p> : attachments.length === 0 ? <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("attachmentsEmpty")}</p> : attachments.map((attachment) => <AttachmentRow key={attachment.id} attachment={attachment} t={t} canManage={canManage} busy={busy} onDownload={onDownload} onDelete={onDelete} />)}</CardContent></Card>;
}

function AttachmentRow({ attachment, t, canManage, busy, onDownload, onDelete }: { attachment: MeetingMinutesAttachmentDto; t: MeetingsT; canManage: boolean; busy: boolean; onDownload: (_attachment: MeetingMinutesAttachmentDto) => void; onDelete: (_attachment: MeetingMinutesAttachmentDto) => void }) {
  const Icon = attachment.contentType.startsWith("image/") ? FileImage : attachment.contentType === "application/pdf" || attachment.contentType.includes("document") || attachment.contentType.includes("sheet") ? FileText : File;
  return <div className="flex min-w-0 items-center gap-3 rounded-lg border p-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="h-4 w-4" /></div><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={attachment.fileName}>{attachment.fileName}</p><p className="text-xs text-muted-foreground">{formatFileSize(attachment.fileSize)} · {attachment.contentType || t("unknownFileType")}</p></div><Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label={`${t("downloadAttachment")}: ${attachment.fileName}`} disabled={busy} onClick={() => onDownload(attachment)}><Download className="h-4 w-4" /></Button>{canManage ? <Button type="button" variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={`${t("deleteAttachment")}: ${attachment.fileName}`} disabled={busy} onClick={() => onDelete(attachment)}><Trash2 className="h-4 w-4" /></Button> : null}</div>;
}

function CommentsSection({ t, comments, roots, replies, canManage, busy, loading, hasError, locale, commentText, replyTo, replyText, expandedReplies, onCommentTextChange, onReplyTextChange, onSubmitComment, onSubmitReply, onReply, onCancelReply, onToggleReplies }: { t: MeetingsT; comments: MeetingMinutesCommentDto[]; roots: MeetingMinutesCommentDto[]; replies: Record<string, MeetingMinutesCommentDto[]>; canManage: boolean; busy: boolean; loading: boolean; hasError: boolean; locale: string; commentText: string; replyTo: string | null; replyText: string; expandedReplies: Record<string, boolean>; onCommentTextChange: (_value: string) => void; onReplyTextChange: (_value: string) => void; onSubmitComment: () => void; onSubmitReply: (_parentId: string) => void; onReply: (_commentId: string) => void; onCancelReply: () => void; onToggleReplies: (_commentId: string) => void }) {
  return <Card><CardHeader className="border-b bg-muted/10"><CardTitle className="flex items-center gap-2 text-base"><MessageSquare className="h-4 w-4 text-primary" />{t("commentsTitle")}</CardTitle><CardDescription>{t("commentsDescription")}</CardDescription></CardHeader><CardContent className="space-y-4 p-4">{canManage ? <div className="space-y-2"><Textarea value={commentText} onChange={(event) => onCommentTextChange(event.target.value)} placeholder={t("commentPlaceholder")} rows={3} /><div className="flex justify-end"><Button type="button" size="sm" disabled={busy} onClick={onSubmitComment}><Send className="me-1.5 h-4 w-4" />{t("addComment")}</Button></div></div> : null}{loading ? <><Skeleton className="h-20" /><Skeleton className="h-20" /></> : hasError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("commentsLoadError")}</p> : roots.length === 0 ? <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("commentsEmpty")}</p> : <div className="space-y-4">{roots.map((comment) => { const commentReplies = replies[comment.id] ?? []; const expanded = Boolean(expandedReplies[comment.id]); return <article key={comment.id} className="space-y-2 rounded-lg border p-3"><CommentBody comment={comment} locale={locale} t={t} />{canManage ? <Button type="button" variant="ghost" size="sm" className="gap-1.5 px-2 text-xs" onClick={() => onReply(comment.id)}><Reply className="h-3.5 w-3.5" />{t("reply")}</Button> : null}{commentReplies.length > 0 ? <Button type="button" variant="ghost" size="sm" className="gap-1.5 px-2 text-xs" onClick={() => onToggleReplies(comment.id)}><ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} />{expanded ? t("hideReplies") : t("showReplies")} ({commentReplies.length})</Button> : null}{replyTo === comment.id && canManage ? <div className="ms-4 space-y-2 border-s-2 border-primary/30 ps-3"><Textarea value={replyText} onChange={(event) => onReplyTextChange(event.target.value)} placeholder={t("replyPlaceholder")} rows={2} /><div className="flex justify-end gap-2"><Button type="button" size="sm" variant="ghost" onClick={onCancelReply}>{t("cancel")}</Button><Button type="button" size="sm" disabled={busy} onClick={() => onSubmitReply(comment.id)}><Send className="me-1.5 h-4 w-4" />{t("reply")}</Button></div></div> : null}{expanded ? commentReplies.map((reply) => <div key={reply.id} className="ms-4 border-s-2 border-primary/20 ps-3"><CommentBody comment={reply} locale={locale} t={t} /></div>) : null}</article>; })}</div>}{comments.length > 0 ? <p className="text-xs text-muted-foreground">{t("commentCount", { count: comments.length })}</p> : null}</CardContent></Card>;
}

function CommentBody({ comment, locale, t }: { comment: MeetingMinutesCommentDto; locale: string; t: MeetingsT }) {
  const author = comment.authorName || comment.createdBy || t("system");
  return <div><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm font-semibold">{author}</p><time dateTime={comment.createdAt} className="text-xs text-muted-foreground">{formatDateTime(comment.createdAt, locale === "ar" ? "ar-EG" : "en-GB")}</time></div><p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">{comment.commentText}</p></div>;
}

function formatFileSize(size: number): string {
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
  if (size < 1024 * 1024 * 1024) return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  return `${(size / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}
