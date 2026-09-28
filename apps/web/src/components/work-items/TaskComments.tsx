"use client";

import { useMemo, useState } from "react";
import { ChevronDown, Clock3, MessageSquare, MessageSquarePlus, Reply, Send, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { RichTextContent, RichTextEditor } from "@/components/ui/rich-text-editor";
import { formatDateTime } from "@/lib/utils";
import { useWorkItemCommentMutation, useWorkItemComments, type WorkItemCommentDto } from "@/lib/api/work-items";
import { TablePagination } from "@/components/ui/table-pagination";

type Copy = Record<string, string>;
const COMMENTS_PAGE_SIZE = 20;

export function TaskComments({ workItemId, locale, t }: { workItemId: string; locale: string; t: Copy }) {
  const [page, setPage] = useState(1);
  const commentsQuery = useWorkItemComments(workItemId, true, page, COMMENTS_PAGE_SIZE);
  const commentMutation = useWorkItemCommentMutation(workItemId);
  const [composerOpen, setComposerOpen] = useState(false);
  const [commentText, setCommentText] = useState("");
  const [replyTo, setReplyTo] = useState<string | null>(null);
  const [replyText, setReplyText] = useState("");
  const [expandedReplies, setExpandedReplies] = useState<Record<string, boolean>>({});
  const [error, setError] = useState("");
  const comments = useMemo(() => commentsQuery.data?.items ?? [], [commentsQuery.data]);
  const roots = useMemo(() => comments.filter((comment) => !comment.parentCommentId), [comments]);
  const replies = useMemo(() => comments.reduce<Record<string, WorkItemCommentDto[]>>((grouped, comment) => {
    if (comment.parentCommentId) (grouped[comment.parentCommentId] ??= []).push(comment);
    return grouped;
  }, {}), [comments]);
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  const submit = async (text: string, parentCommentId?: string) => {
    const value = text.trim();
    if (!value) { setError(t.commentRequired); return; }
    setError("");
    try {
      await commentMutation.mutateAsync({ commentText: value, parentCommentId });
      setPage(1);
      if (parentCommentId) { setReplyText(""); setReplyTo(null); } else { setCommentText(""); setComposerOpen(false); }
    } catch { setError(t.commentSaveError); }
  };

  return <Card className="overflow-hidden border-border/70 shadow-sm">
    <CardHeader className="flex flex-row items-center justify-between gap-3 border-b bg-muted/20 px-4 py-4 sm:px-5">
      <CardTitle className="flex items-center gap-2 text-base"><MessageSquare className="h-4 w-4 text-info" />{t.comments}</CardTitle>
      <Button type="button" size="sm" variant="outline" className="gap-1.5 border-info/30 text-info hover:bg-info/10 hover:text-info" onClick={() => { setComposerOpen((current) => !current); setError(""); }}><MessageSquarePlus className="h-4 w-4" />{t.addComment}</Button>
    </CardHeader>
    <CardContent className="space-y-4 p-4 sm:p-5">
      {composerOpen ? <div className="space-y-3 rounded-xl border border-info/25 bg-info/[0.04] p-3 sm:p-4"><div className="flex items-center gap-2 text-sm font-semibold text-info"><MessageSquarePlus className="h-4 w-4" />{t.addComment}</div><RichTextEditor id={`comment-editor-${workItemId}`} value={commentText} onChange={setCommentText} placeholder={t.commentPlaceholder} ariaLabel={t.commentPlaceholder} t={t} /><div className="flex flex-wrap justify-end gap-2"><Button type="button" variant="outline" size="sm" onClick={() => { setComposerOpen(false); setCommentText(""); }}>{t.cancel}</Button><Button type="button" size="sm" className="gap-1.5" disabled={commentMutation.isPending} onClick={() => submit(commentText)}><Send className="h-4 w-4" />{commentMutation.isPending ? t.savingComment : t.submitComment}</Button></div></div> : null}
      {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
      {commentsQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-24 rounded-xl" /><Skeleton className="h-24 rounded-xl" /></div> : commentsQuery.isError ? <p role="alert" className="rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t.commentsLoadError}</p> : roots.length === 0 ? <p className="rounded-lg border border-dashed border-border/70 bg-muted/15 px-4 py-6 text-center text-sm text-muted-foreground">{t.noComments}</p> : <div className="space-y-5">{roots.map((comment) => { const commentReplies = replies[comment.id] ?? []; const repliesExpanded = Boolean(expandedReplies[comment.id]); return <div key={comment.id} className="space-y-3"><Comment comment={comment} dateLocale={dateLocale} t={t} onReply={() => { setReplyTo((current) => current === comment.id ? null : comment.id); setError(""); }} />{commentReplies.length > 0 ? <Button type="button" variant="ghost" size="sm" className="ms-4 h-8 gap-1.5 px-2 text-xs font-medium text-info hover:bg-info/10 hover:text-info sm:ms-8" aria-expanded={repliesExpanded} aria-label={`${repliesExpanded ? t.hideReplies : t.showReplies}: ${commentReplies.length}`} onClick={() => setExpandedReplies((current) => ({ ...current, [comment.id]: !repliesExpanded }))}><ChevronDown className={`h-4 w-4 transition-transform ${repliesExpanded ? "rotate-180" : ""}`} />{repliesExpanded ? t.hideReplies : t.showReplies} ({commentReplies.length} {commentReplies.length === 1 ? t.replyCount : t.replies})</Button> : null}{repliesExpanded ? commentReplies.map((reply) => <div key={reply.id} className="ms-4 border-s-2 border-info/35 ps-3 sm:ms-8 sm:ps-4"><Comment comment={reply} dateLocale={dateLocale} t={t} variant="reply" /></div>) : null}{replyTo === comment.id ? <div className="ms-4 space-y-3 border-s-2 border-primary/35 ps-3 sm:ms-8 sm:ps-4"><div className="flex items-center gap-2 text-xs font-semibold text-primary"><Reply className="h-3.5 w-3.5" />{t.reply}</div><RichTextEditor id={`reply-editor-${comment.id}`} value={replyText} onChange={setReplyText} placeholder={t.replyPlaceholder} ariaLabel={t.replyPlaceholder} t={t} minHeight="min-h-20" /><div className="flex flex-wrap justify-end gap-2"><Button type="button" variant="outline" size="sm" onClick={() => { setReplyTo(null); setReplyText(""); }}>{t.cancel}</Button><Button type="button" size="sm" className="gap-1.5" disabled={commentMutation.isPending} onClick={() => submit(replyText, comment.id)}><Send className="h-4 w-4" />{commentMutation.isPending ? t.savingComment : t.submitReply}</Button></div></div> : null}</div>; })}</div>}
      {commentsQuery.data && roots.length > 0 ? <Pagination page={page} totalPages={commentsQuery.data.totalPages} pageSize={COMMENTS_PAGE_SIZE} itemCount={comments.length} previous={t.previous} next={t.next} pageLabel={t.page} ofLabel={t.of} disabled={commentsQuery.isFetching} onPageChange={setPage} /> : null}
    </CardContent>
  </Card>;
}

function Pagination({ page, totalPages, pageSize, itemCount, previous, next, pageLabel, ofLabel, disabled, onPageChange }: { page: number; totalPages: number; pageSize: number; itemCount: number; previous: string; next: string; pageLabel: string; ofLabel: string; disabled: boolean; onPageChange: (_page: number) => void }) {
  const firstItem = totalPages > 0 ? (page - 1) * pageSize + 1 : 0;
  const lastItem = itemCount > 0 ? firstItem + itemCount - 1 : 0;
  return <TablePagination className="border-t border-border/60 pt-4" page={page} totalPages={totalPages} summary={`${firstItem}–${lastItem} · ${pageLabel} ${page} ${ofLabel} ${totalPages}`} previousLabel={previous} nextLabel={next} onPageChange={onPageChange} disabled={disabled} />;
}

function Comment({ comment, dateLocale, t, onReply, variant = "root" }: { comment: WorkItemCommentDto; dateLocale: string; t: Copy; onReply?: () => void; variant?: "root" | "reply" }) {
  const author = comment.authorName || comment.createdBy || t.system;
  const initials = author.trim().slice(0, 1).toUpperCase();

  return <article className={`overflow-hidden rounded-xl border shadow-sm transition-colors ${variant === "reply" ? "border-info/25 bg-info/[0.035]" : "border-border/70 bg-background hover:border-info/35"}`}>
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border/60 bg-muted/20 px-3 py-3 sm:px-4">
      <div className="flex min-w-0 items-center gap-2.5">
        <span aria-hidden="true" className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold ${variant === "reply" ? "bg-info/15 text-info" : "bg-primary/15 text-primary"}`}>{initials || <UserRound className="h-4 w-4" />}</span>
        <span className="min-w-0 truncate text-sm font-semibold text-foreground" title={author}>{author}</span>
      </div>
      <time dateTime={comment.createdAt} className="flex shrink-0 items-center gap-1.5 text-[11px] text-muted-foreground"><Clock3 className="h-3.5 w-3.5" />{formatDateTime(comment.createdAt, dateLocale)}</time>
    </header>
    <div className="px-3 py-3 sm:px-4 sm:py-4"><div className={`rounded-lg border-s-2 px-3 py-2.5 text-sm leading-6 ${variant === "reply" ? "border-info bg-info/[0.04]" : "border-primary bg-muted/20"}`}><RichTextContent value={comment.commentText} /></div></div>
    {onReply ? <footer className="flex items-center border-t border-border/60 px-3 py-2 sm:px-4"><Button type="button" variant="ghost" size="sm" className="h-8 gap-1.5 px-2 text-xs font-medium text-info hover:bg-info/10 hover:text-info" onClick={onReply}><Reply className="h-3.5 w-3.5" />{t.reply}</Button></footer> : null}
  </article>;
}
