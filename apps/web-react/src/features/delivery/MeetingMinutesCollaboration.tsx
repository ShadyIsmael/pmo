import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { Check, ChevronDown, ChevronUp, Download, File, FileImage, FileText, MessageSquare, Paperclip, Pencil, Plus, Reply, Send, Trash2, Upload } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Skeleton } from "@components/ui/Skeleton";
import { Textarea } from "@components/ui/Textarea";
import { formatDateTime } from "@lib/utils";
import { formatFileSize as formatFileSizeValue } from "@lib/formatting/file-size";
import { useI18n } from "@i18n/messages";
import { meetingsApiFull, useMeetingFullMutations, useMeetingMinutesAttachments, useMeetingMinutesChecklist, useMeetingMinutesComments, type ChecklistItemDto, type MeetingAttachmentDto, type MeetingCommentDto } from "@api/meetingsFull";

type Translator = ReturnType<typeof useI18n>["t"];

export function MeetingMinutesCollaboration({ meetingId, canManage, locale, mainContent, sideContent, hasMinutes = true }: { meetingId: string; canManage: boolean; locale: string; mainContent: React.ReactNode; sideContent?: React.ReactNode; hasMinutes?: boolean }) {
  const { t, formatNumber } = useI18n();
  const [searchParams] = useSearchParams();
  const confirm = useConfirm();
  const checklistQuery = useMeetingMinutesChecklist(meetingId, hasMinutes);
  const attachmentsQuery = useMeetingMinutesAttachments(meetingId, hasMinutes);
  const commentsQuery = useMeetingMinutesComments(meetingId, hasMinutes);
  const mutations = useMeetingFullMutations(meetingId);
  const fileInputRef = React.useRef<HTMLInputElement>(null);
  const [newItemTitle, setNewItemTitle] = React.useState("");
  const [editingItemId, setEditingItemId] = React.useState<string | null>(null);
  const [editingTitle, setEditingTitle] = React.useState("");
  const [commentText, setCommentText] = React.useState("");
  const [replyTo, setReplyTo] = React.useState<string | null>(null);
  const [replyText, setReplyText] = React.useState("");
  const [expandedReplies, setExpandedReplies] = React.useState<Record<string, boolean>>({});
  const [error, setError] = React.useState<string | null>(null);
  const targetPublicId = searchParams.get("reply") || searchParams.get("comment");

  const checklist = checklistQuery.data ?? [];
  const attachments = attachmentsQuery.data ?? [];
  const comments = React.useMemo(() => commentsQuery.data ?? [], [commentsQuery.data]);
  const roots = React.useMemo(() => comments.filter((comment) => !comment.parentCommentId), [comments]);
  const replies = React.useMemo(() => comments.reduce<Record<string, MeetingCommentDto[]>>((grouped, comment) => {
    if (comment.parentCommentId) (grouped[comment.parentCommentId] ??= []).push(comment);
    return grouped;
  }, {}), [comments]);
  const targetComment = React.useMemo(() => targetPublicId ? comments.find((comment) => comment.publicId === targetPublicId) : undefined, [comments, targetPublicId]);

  React.useEffect(() => {
    if (!targetComment?.publicId) return;
    if (targetComment.parentCommentId && !expandedReplies[targetComment.parentCommentId]) {
      setExpandedReplies((current) => ({ ...current, [targetComment.parentCommentId!]: true }));
      return;
    }
    const target = document.getElementById(`meeting-comment-${targetComment.publicId}`);
    if (!target) return;
    target.scrollIntoView?.({ behavior: "smooth", block: "center" });
    target.focus({ preventScroll: true });
  }, [targetComment, expandedReplies]);
  const busy = mutations.createChecklist.isPending || mutations.updateChecklist.isPending || mutations.deleteChecklist.isPending
    || mutations.reorderChecklist.isPending || mutations.uploadAttachments.isPending || mutations.deleteAttachment.isPending || mutations.addComment.isPending;

  async function addChecklistItem() {
    const title = newItemTitle.trim();
    if (!title) { setError(t("meetings.checklistTitleRequired")); return; }
    setError(null);
    try { await mutations.createChecklist.mutateAsync(title); setNewItemTitle(""); }
    catch { setError(t("meetings.checklistSaveError")); }
  }

  async function saveChecklistItem(item: ChecklistItemDto) {
    const title = editingTitle.trim();
    if (!title) { setError(t("meetings.checklistTitleRequired")); return; }
    setError(null);
    try { await mutations.updateChecklist.mutateAsync({ itemId: item.id, body: { title, isCompleted: item.isCompleted } }); setEditingItemId(null); }
    catch { setError(t("meetings.checklistSaveError")); }
  }

  async function toggleChecklistItem(item: ChecklistItemDto, isCompleted: boolean) {
    setError(null);
    try { await mutations.updateChecklist.mutateAsync({ itemId: item.id, body: { title: item.title, isCompleted } }); }
    catch { setError(t("meetings.checklistSaveError")); }
  }

  async function removeChecklistItem(item: ChecklistItemDto) {
    if (!await confirm({ description: t("meetings.deleteChecklistConfirm"), destructive: true })) return;
    setError(null);
    try { await mutations.deleteChecklist.mutateAsync(item.id); }
    catch { setError(t("meetings.checklistDeleteError")); }
  }

  async function moveChecklistItem(index: number, direction: -1 | 1) {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= checklist.length) return;
    const itemIds = checklist.map((item) => item.id);
    [itemIds[index], itemIds[targetIndex]] = [itemIds[targetIndex], itemIds[index]];
    setError(null);
    try { await mutations.reorderChecklist.mutateAsync(itemIds); }
    catch { setError(t("meetings.checklistReorderError")); }
  }

  async function upload(files: File[]) {
    if (files.length === 0) return;
    setError(null);
    try { await mutations.uploadAttachments.mutateAsync(files); }
    catch { setError(t("meetings.attachmentUploadError")); }
  }

  async function download(attachment: MeetingAttachmentDto) {
    setError(null);
    try {
      const blob = await meetingsApiFull.downloadAttachment(meetingId, attachment.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = attachment.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch { setError(t("meetings.attachmentDownloadError")); }
  }

  async function removeAttachment(attachment: MeetingAttachmentDto) {
    if (!await confirm({ description: t("meetings.deleteAttachmentConfirm"), destructive: true })) return;
    setError(null);
    try { await mutations.deleteAttachment.mutateAsync(attachment.id); }
    catch { setError(t("meetings.attachmentRemoveError")); }
  }

  async function submitComment(text: string, parentCommentId?: string) {
    const value = text.trim();
    if (!value) { setError(t("meetings.commentRequired")); return; }
    setError(null);
    try {
      await mutations.addComment.mutateAsync({ commentText: value, parentCommentId });
      if (parentCommentId) { setReplyTo(null); setReplyText(""); }
      else setCommentText("");
    } catch { setError(t("meetings.commentSaveError")); }
  }

  return <div className="space-y-6">
    {error ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
    <div className={sideContent ? "grid items-start gap-6 xl:grid-cols-12" : "space-y-6"} dir="ltr">
      <div className={`min-w-0 ${sideContent ? "xl:col-span-7" : ""}`} dir={locale === "ar" ? "rtl" : "ltr"}>{mainContent}</div>
      {sideContent ? <aside className="min-w-0 xl:col-span-5" dir={locale === "ar" ? "rtl" : "ltr"}>{sideContent}</aside> : null}
    </div>
    {!hasMinutes ? <p role="status" className="rounded-md border border-dashed px-3 py-3 text-sm text-muted-foreground">{t("meetings.saveMinutesDraftBeforeCollaboration")}</p> : <><div className="grid items-start gap-6 md:grid-cols-2">
      <ChecklistSection t={t} items={checklist} canManage={canManage} busy={busy} loading={checklistQuery.isLoading} hasError={checklistQuery.isError} newTitle={newItemTitle} editingItemId={editingItemId} editingTitle={editingTitle} onNewTitleChange={setNewItemTitle} onEditingTitleChange={setEditingTitle} onAdd={() => void addChecklistItem()} onStartEdit={(item) => { setEditingItemId(item.id); setEditingTitle(item.title); setError(null); }} onCancelEdit={() => setEditingItemId(null)} onSaveEdit={(item) => void saveChecklistItem(item)} onToggle={(item, checked) => void toggleChecklistItem(item, checked)} onDelete={(item) => void removeChecklistItem(item)} onMove={(index, direction) => void moveChecklistItem(index, direction)} />
      <AttachmentsSection t={t} locale={locale} attachments={attachments} canManage={canManage} busy={busy} loading={attachmentsQuery.isLoading} hasError={attachmentsQuery.isError} inputRef={fileInputRef} onChoose={() => fileInputRef.current?.click()} onFiles={(files) => void upload(files)} onDownload={(attachment) => void download(attachment)} onDelete={(attachment) => void removeAttachment(attachment)} />
    </div>
    <div className="min-w-0" dir={locale === "ar" ? "rtl" : "ltr"}>
        <CommentsSection t={t} formatNumber={formatNumber} comments={comments} roots={roots} replies={replies} canManage={canManage} busy={busy} loading={commentsQuery.isLoading} hasError={commentsQuery.isError} locale={locale} commentText={commentText} replyTo={replyTo} replyText={replyText} expandedReplies={expandedReplies} targetPublicId={targetPublicId} onCommentTextChange={setCommentText} onReplyTextChange={setReplyText} onSubmitComment={() => void submitComment(commentText)} onSubmitReply={(parentId) => void submitComment(replyText, parentId)} onReply={(commentId) => { setReplyTo((current) => current === commentId ? null : commentId); setError(null); }} onCancelReply={() => { setReplyTo(null); setReplyText(""); }} onToggleReplies={(commentId) => setExpandedReplies((current) => ({ ...current, [commentId]: !current[commentId] }))} />
    </div></>}
  </div>;
}

function ChecklistSection({ t, items, canManage, busy, loading, hasError, newTitle, editingItemId, editingTitle, onNewTitleChange, onEditingTitleChange, onAdd, onStartEdit, onCancelEdit, onSaveEdit, onToggle, onDelete, onMove }: { t: Translator; items: ChecklistItemDto[]; canManage: boolean; busy: boolean; loading: boolean; hasError: boolean; newTitle: string; editingItemId: string | null; editingTitle: string; onNewTitleChange: (_value: string) => void; onEditingTitleChange: (_value: string) => void; onAdd: () => void; onStartEdit: (_item: ChecklistItemDto) => void; onCancelEdit: () => void; onSaveEdit: (_item: ChecklistItemDto) => void; onToggle: (_item: ChecklistItemDto, _checked: boolean) => void; onDelete: (_item: ChecklistItemDto) => void; onMove: (_index: number, _direction: -1 | 1) => void }) {
  return <Card><CardHeader className="border-b bg-muted/10"><CardTitle className="flex items-center gap-2 text-base"><Check className="h-4 w-4 text-primary" />{t("meetings.checklistTitle")}</CardTitle><CardDescription>{t("meetings.checklistDescription")}</CardDescription></CardHeader><CardContent className="space-y-3 p-5">
    {canManage ? <div className="flex gap-2"><Textarea value={newTitle} onChange={(event) => onNewTitleChange(event.target.value)} placeholder={t("meetings.checklistPlaceholder")} rows={1} className="min-h-10 resize-none" onKeyDown={(event) => { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); onAdd(); } }} /><Button type="button" size="sm" className="shrink-0 self-end" disabled={busy} onClick={onAdd}><Plus className="me-1.5 h-4 w-4" />{t("meetings.addChecklistItem")}</Button></div> : null}
    {loading ? <div className="space-y-2"><Skeleton className="h-12" /><Skeleton className="h-12" /></div> : hasError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("meetings.checklistLoadError")}</p> : items.length === 0 ? <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("meetings.checklistEmpty")}</p> : <div className="space-y-2">{items.map((item, index) => <div key={item.id} className="flex items-center gap-2 rounded-lg border bg-background p-2.5"><input type="checkbox" checked={item.isCompleted} disabled={!canManage || busy} aria-label={`${t("meetings.completed")}: ${item.title}`} onChange={(event) => onToggle(item, event.target.checked)} className="h-4 w-4 shrink-0 accent-primary" /><div className="min-w-0 flex-1">{editingItemId === item.id ? <div className="flex gap-2"><Textarea value={editingTitle} onChange={(event) => onEditingTitleChange(event.target.value)} rows={1} className="min-h-9 resize-none" onKeyDown={(event) => { if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); onSaveEdit(item); } }} /><Button type="button" size="sm" onClick={() => onSaveEdit(item)} disabled={busy}>{t("meetings.saveChecklistItem")}</Button><Button type="button" size="sm" variant="ghost" onClick={onCancelEdit}>{t("meetings.cancel")}</Button></div> : <p className={`text-sm ${item.isCompleted ? "text-muted-foreground line-through" : "font-medium"}`}>{item.title}</p>}</div>{canManage && editingItemId !== item.id ? <div className="flex shrink-0 items-center gap-0.5"><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={t("meetings.moveUp")} disabled={busy || index === 0} onClick={() => onMove(index, -1)}><ChevronUp className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={t("meetings.moveDown")} disabled={busy || index === items.length - 1} onClick={() => onMove(index, 1)}><ChevronDown className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={t("meetings.editChecklistItem")} disabled={busy} onClick={() => onStartEdit(item)}><Pencil className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={t("meetings.deleteChecklistItem")} disabled={busy} onClick={() => onDelete(item)}><Trash2 className="h-4 w-4" /></Button></div> : null}</div>)}</div>}
  </CardContent></Card>;
}

function AttachmentsSection({ t, locale, attachments, canManage, busy, loading, hasError, inputRef, onChoose, onFiles, onDownload, onDelete }: { t: Translator; locale: string; attachments: MeetingAttachmentDto[]; canManage: boolean; busy: boolean; loading: boolean; hasError: boolean; inputRef: React.RefObject<HTMLInputElement>; onChoose: () => void; onFiles: (_files: File[]) => void; onDownload: (_attachment: MeetingAttachmentDto) => void; onDelete: (_attachment: MeetingAttachmentDto) => void }) {
  return <Card><CardHeader className="flex flex-col gap-3 border-b bg-muted/10 sm:flex-row sm:items-center sm:justify-between"><div><CardTitle className="flex items-center gap-2 text-base"><Paperclip className="h-4 w-4 text-primary" />{t("meetings.attachmentsTitle")}</CardTitle><CardDescription>{t("meetings.attachmentsDescription")}</CardDescription></div>{canManage ? <><input ref={inputRef} type="file" multiple className="sr-only" onChange={(event) => { onFiles(Array.from(event.target.files ?? [])); event.target.value = ""; }} /><Button type="button" size="sm" variant="outline" disabled={busy} onClick={onChoose}><Upload className="me-1.5 h-4 w-4" />{busy ? t("meetings.uploading") : t("meetings.uploadAttachment")}</Button></> : null}</CardHeader><CardContent className="space-y-2 p-5">{loading ? <><Skeleton className="h-14" /><Skeleton className="h-14" /></> : hasError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("meetings.attachmentsLoadError")}</p> : attachments.length === 0 ? <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("meetings.attachmentsEmpty")}</p> : attachments.map((attachment) => <AttachmentRow key={attachment.id} attachment={attachment} t={t} canManage={canManage} busy={busy} onDownload={onDownload} onDelete={onDelete} locale={locale} />)}</CardContent></Card>;
}

function AttachmentRow({ attachment, t, locale, canManage, busy, onDownload, onDelete }: { attachment: MeetingAttachmentDto; t: Translator; locale: string; canManage: boolean; busy: boolean; onDownload: (_attachment: MeetingAttachmentDto) => void; onDelete: (_attachment: MeetingAttachmentDto) => void }) {
  const Icon = attachment.contentType.startsWith("image/") ? FileImage : attachment.contentType === "application/pdf" || attachment.contentType.includes("document") || attachment.contentType.includes("sheet") ? FileText : File;
  return <div className="flex min-w-0 items-center gap-3 rounded-lg border p-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="h-4 w-4" /></div><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={attachment.fileName}>{attachment.fileName}</p><p className="text-xs text-muted-foreground">{formatFileSize(attachment.fileSize, locale)} · {attachment.contentType || t("meetings.unknownFileType")}</p></div><Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label={`${t("meetings.downloadAttachment")}: ${attachment.fileName}`} disabled={busy} onClick={() => onDownload(attachment)}><Download className="h-4 w-4" /></Button>{canManage ? <Button type="button" variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={`${t("meetings.deleteAttachment")}: ${attachment.fileName}`} disabled={busy} onClick={() => onDelete(attachment)}><Trash2 className="h-4 w-4" /></Button> : null}</div>;
}

function CommentsSection({ t, formatNumber, comments, roots, replies, canManage, busy, loading, hasError, locale, commentText, replyTo, replyText, expandedReplies, targetPublicId, onCommentTextChange, onReplyTextChange, onSubmitComment, onSubmitReply, onReply, onCancelReply, onToggleReplies }: { t: Translator; formatNumber: (_value: number) => string; comments: MeetingCommentDto[]; roots: MeetingCommentDto[]; replies: Record<string, MeetingCommentDto[]>; canManage: boolean; busy: boolean; loading: boolean; hasError: boolean; locale: string; commentText: string; replyTo: string | null; replyText: string; expandedReplies: Record<string, boolean>; targetPublicId: string | null; onCommentTextChange: (_value: string) => void; onReplyTextChange: (_value: string) => void; onSubmitComment: () => void; onSubmitReply: (_parentId: string) => void; onReply: (_commentId: string) => void; onCancelReply: () => void; onToggleReplies: (_commentId: string) => void }) {
  return <Card><CardHeader className="border-b bg-muted/10"><CardTitle className="flex items-center gap-2 text-base"><MessageSquare className="h-4 w-4 text-primary" />{t("meetings.commentsTitle")}</CardTitle><CardDescription>{t("meetings.commentsDescription")}</CardDescription></CardHeader><CardContent className="space-y-4 p-5">
    {canManage ? <div className="space-y-2"><Textarea value={commentText} onChange={(event) => onCommentTextChange(event.target.value)} placeholder={t("meetings.commentPlaceholder")} rows={3} /><div className="flex justify-end"><Button type="button" size="sm" disabled={busy} onClick={onSubmitComment}><Send className="me-1.5 h-4 w-4" />{t("meetings.addComment")}</Button></div></div> : null}
    {loading ? <><Skeleton className="h-20" /><Skeleton className="h-20" /></> : hasError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("meetings.commentsLoadError")}</p> : roots.length === 0 ? <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("meetings.commentsEmpty")}</p> : <div className="space-y-4">{roots.map((comment) => {
      const commentReplies = replies[comment.id] ?? [];
      const expanded = Boolean(expandedReplies[comment.id]);
      const targeted = comment.publicId === targetPublicId;
      return <article key={comment.id} id={comment.publicId ? `meeting-comment-${comment.publicId}` : undefined} tabIndex={targeted ? -1 : undefined} className={`space-y-2 rounded-lg border p-3 ${targeted ? "border-primary ring-2 ring-primary/30" : ""}`}>
        <CommentBody comment={comment} locale={locale} />
        {canManage ? <Button type="button" variant="ghost" size="sm" className="gap-1.5 px-2 text-xs" onClick={() => onReply(comment.id)}><Reply className="h-3.5 w-3.5" />{t("meetings.reply")}</Button> : null}
        {commentReplies.length > 0 ? <Button type="button" variant="ghost" size="sm" className="gap-1.5 px-2 text-xs" onClick={() => onToggleReplies(comment.id)}><ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} />{expanded ? t("meetings.hideReplies") : t("meetings.showReplies")} ({commentReplies.length})</Button> : null}
        {replyTo === comment.id && canManage ? <div className="ms-4 space-y-2 border-s-2 border-primary/30 ps-3"><Textarea value={replyText} onChange={(event) => onReplyTextChange(event.target.value)} placeholder={t("meetings.replyPlaceholder")} rows={2} /><div className="flex justify-end gap-2"><Button type="button" size="sm" variant="ghost" onClick={onCancelReply}>{t("meetings.cancel")}</Button><Button type="button" size="sm" disabled={busy} onClick={() => onSubmitReply(comment.id)}><Send className="me-1.5 h-4 w-4" />{t("meetings.reply")}</Button></div></div> : null}
        {expanded ? commentReplies.map((reply) => {
          const replyTargeted = reply.publicId === targetPublicId;
          return <div key={reply.id} id={reply.publicId ? `meeting-comment-${reply.publicId}` : undefined} tabIndex={replyTargeted ? -1 : undefined} className={`ms-4 border-s-2 ps-3 ${replyTargeted ? "border-primary/60 bg-primary/5" : "border-primary/20"}`}><CommentBody comment={reply} locale={locale} /></div>;
        }) : null}
      </article>;
    })}</div>}
    {comments.length > 0 ? <p className="text-xs text-muted-foreground">{formatNumber(comments.length)} {t("meetings.commentsTitle")}</p> : null}
  </CardContent></Card>;
}
function CommentBody({ comment, locale }: { comment: MeetingCommentDto; locale: string }) {
  const { t } = useI18n();
  const author = comment.authorName || comment.createdBy || t("meetings.system");
  return <div><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm font-semibold">{author}</p><time dateTime={comment.createdAt} className="text-xs text-muted-foreground">{formatDateTime(comment.createdAt, locale)}</time></div><p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">{comment.commentText}</p></div>;
}

function formatFileSize(size: number, locale: string): string { return formatFileSizeValue(size, locale); }
