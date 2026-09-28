"use client";

import { useRef, useState, type ComponentType } from "react";
import { Download, File, FileArchive, FileAudio, FileImage, FileText, FileVideo, Paperclip, Trash2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useDeleteWorkItemAttachment, useWorkItemAttachmentMutation, useWorkItemAttachments, workItemsApi, type WorkItemAttachmentDto } from "@/lib/api/work-items";

type Copy = Record<string, string>;
type FileIcon = ComponentType<{ className?: string }>;

function formatFileSize(size: number): string {
  if (size < 1024) return `${size} B`;
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
  if (size < 1024 * 1024 * 1024) return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  return `${(size / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

function fileVisual(fileName: string, contentType?: string): { icon: FileIcon; tone: string } {
  const extension = fileName.split(".").pop()?.toLowerCase();
  if (contentType?.startsWith("image/") || ["png", "jpg", "jpeg", "gif", "svg", "webp"].includes(extension ?? "")) return { icon: FileImage, tone: "bg-info/10 text-info" };
  if (contentType?.startsWith("video/") || ["mp4", "mov", "avi", "webm"].includes(extension ?? "")) return { icon: FileVideo, tone: "bg-purple-500/10 text-purple-600 dark:text-purple-400" };
  if (contentType?.startsWith("audio/") || ["mp3", "wav", "m4a", "ogg"].includes(extension ?? "")) return { icon: FileAudio, tone: "bg-warning/10 text-warning" };
  if (["zip", "rar", "7z", "tar", "gz"].includes(extension ?? "")) return { icon: FileArchive, tone: "bg-amber-500/10 text-amber-600 dark:text-amber-400" };
  if (contentType?.startsWith("text/") || ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "csv"].includes(extension ?? "")) return { icon: FileText, tone: "bg-success/10 text-success" };
  return { icon: File, tone: "bg-muted text-muted-foreground" };
}

function FileTile({ fileName, contentType }: { fileName: string; contentType?: string }) {
  const visual = fileVisual(fileName, contentType);
  const Icon = visual.icon;
  return <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${visual.tone}`}><Icon className="h-5 w-5" /></div>;
}

function AttachmentRow({ attachment, canManage, t, onDownload, onRemove, removing }: { attachment: WorkItemAttachmentDto; canManage: boolean; t: Copy; onDownload: () => void; onRemove: () => void; removing: boolean }) {
  return <div className="group flex min-w-0 items-center gap-3 rounded-xl border border-border/70 bg-background p-3 transition-colors hover:border-primary/30 hover:bg-muted/20">
    <FileTile fileName={attachment.fileName} contentType={attachment.contentType} />
    <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={attachment.fileName}>{attachment.fileName}</p><p className="mt-1 text-xs text-muted-foreground">{formatFileSize(attachment.fileSize)} <span aria-hidden="true">·</span> {t.attached}</p></div>
    <div className="flex shrink-0 items-center gap-1 opacity-100 sm:opacity-70 sm:transition-opacity sm:group-hover:opacity-100"><Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label={`${t.download}: ${attachment.fileName}`} onClick={onDownload}><Download className="h-4 w-4" /></Button>{canManage ? <Button type="button" variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={`${t.remove}: ${attachment.fileName}`} disabled={removing} onClick={onRemove}><Trash2 className="h-4 w-4" /></Button> : null}</div>
  </div>;
}

export function WorkItemAttachments({ workItemId, canManage, t, files, onFilesChange }: { workItemId?: string; canManage: boolean; t: Copy; files?: File[]; onFilesChange?: (_files: File[]) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploadingFiles, setUploadingFiles] = useState<string[]>([]);
  const [error, setError] = useState("");
  const attachmentsQuery = useWorkItemAttachments(workItemId, Boolean(workItemId));
  const uploadMutation = useWorkItemAttachmentMutation(workItemId ?? "");
  const deleteMutation = useDeleteWorkItemAttachment(workItemId ?? "");
  const queuedFiles = files ?? [];
  const attachments = attachmentsQuery.data ?? [];

  const selectFiles = (selected: FileList | null) => {
    if (!selected) return;
    onFilesChange?.([...queuedFiles, ...Array.from(selected)]);
    if (inputRef.current) inputRef.current.value = "";
  };

  const upload = async () => {
    if (!workItemId || queuedFiles.length === 0) return;
    setError("");
    setUploadingFiles(queuedFiles.map((file) => file.name));
    try {
      await uploadMutation.mutateAsync(queuedFiles);
      onFilesChange?.([]);
    } catch { setError(t.attachmentUploadError); }
    finally { setUploadingFiles([]); }
  };

  const download = async (attachment: WorkItemAttachmentDto) => {
    if (!workItemId) return;
    setError("");
    try {
      const blob = await workItemsApi.downloadAttachment(workItemId, attachment.id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = attachment.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch { setError(t.attachmentDownloadError); }
  };

  const remove = async (attachment: WorkItemAttachmentDto) => {
    if (!workItemId) return;
    setError("");
    try { await deleteMutation.mutateAsync(attachment.id); } catch { setError(t.attachmentRemoveError); }
  };

  return <Card className="overflow-hidden border-border/70 shadow-sm">
    <CardHeader className="border-b bg-muted/10 pb-4"><div className="flex flex-wrap items-center justify-between gap-3"><div className="flex items-center gap-3"><div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Paperclip className="h-5 w-5" /></div><div><CardTitle className="text-base">{t.attachments}</CardTitle><p className="mt-1 text-xs text-muted-foreground">{attachments.length > 0 ? `${attachments.length} ${t.attached}` : t.noAttachments}</p></div></div>{canManage ? <><input ref={inputRef} type="file" multiple className="sr-only" onChange={(event) => selectFiles(event.target.files)} /><Button type="button" size="sm" variant="outline" onClick={() => inputRef.current?.click()}><Upload className="me-2 h-4 w-4" />{t.chooseFiles}</Button></> : null}</div></CardHeader>
    <CardContent className="space-y-3 p-4">
      {error ? <p role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
      {queuedFiles.length > 0 ? <div className="space-y-2 rounded-xl border border-dashed border-primary/30 bg-primary/[0.03] p-3"><div className="flex items-center justify-between gap-2"><p className="text-xs font-semibold text-primary">{workItemId ? t.pendingAttachments : t.selectedAttachments}</p><span className="text-[11px] text-muted-foreground">{queuedFiles.length}</span></div>{queuedFiles.map((file, index) => { const uploading = uploadingFiles.includes(file.name); return <div key={`${file.name}-${file.size}-${index}`} className="flex min-w-0 items-center gap-3 rounded-lg bg-background/80 p-2"><FileTile fileName={file.name} contentType={file.type} /><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={file.name}>{file.name}</p><p className="mt-1 text-xs text-muted-foreground">{formatFileSize(file.size)} <span aria-hidden="true">·</span> {uploading ? t.uploading : t.pendingUpload}</p></div><Button type="button" variant="ghost" size="icon" className="h-8 w-8" aria-label={`${t.remove}: ${file.name}`} disabled={uploadingFiles.length > 0} onClick={() => onFilesChange?.(queuedFiles.filter((_, fileIndex) => fileIndex !== index))}><Trash2 className="h-4 w-4" /></Button></div>; })}{workItemId ? <div className="flex justify-end pt-1"><Button type="button" size="sm" disabled={uploadingFiles.length > 0} onClick={upload}><Upload className="me-2 h-4 w-4" />{uploadingFiles.length > 0 ? t.uploading : t.upload}</Button></div> : null}</div> : null}
      {workItemId ? attachmentsQuery.isLoading ? <div className="space-y-2"><Skeleton className="h-16 rounded-xl" /><Skeleton className="h-16 rounded-xl" /></div> : attachmentsQuery.isError ? <p role="alert" className="rounded-lg border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t.attachmentsLoadError}</p> : attachments.length > 0 ? <div className="space-y-2">{attachments.map((attachment) => <AttachmentRow key={attachment.id} attachment={attachment} canManage={canManage} t={t} onDownload={() => download(attachment)} onRemove={() => remove(attachment)} removing={deleteMutation.isPending} />)}</div> : null : null}
    </CardContent>
  </Card>;
}
