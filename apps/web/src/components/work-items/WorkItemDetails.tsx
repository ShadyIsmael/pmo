"use client";

import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { useLocale } from "next-intl";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, CalendarDays, CircleAlert, History, MessageSquare, Pencil, Save, Ticket, Wrench } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { RichTextContent, RichTextEditor } from "@/components/ui/rich-text-editor";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { WorkItemAssigneeSelect } from "@/components/work-items/WorkItemAssigneeSelect";
import { ProjectPicker } from "@/components/work-items/ProjectPicker";
import { WorkItemAttachments } from "@/components/work-items/WorkItemAttachments";
import { TaskComments } from "@/components/work-items/TaskComments";
import { useProjects } from "@/hooks/projects/useProjects";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { getWorkItemStatusColorClasses } from "@/lib/status-presentation";
import { TablePagination } from "@/components/ui/table-pagination";
import { formatDateTime } from "@/lib/utils";
import { useQueryClient } from "@tanstack/react-query";
import { useWorkItem, useWorkItemActivity, useWorkItemAssignees, useWorkItemAttachmentMutation, useWorkItemMutations, useWorkItemWorkflow, type SaveWorkItemDto, type WorkItemPriority, type WorkItemType } from "@/lib/api/work-items";
import { useManagementPerspective } from "@/lib/perspective-context";
import { getWorkItemStatusName, useWorkItemStatuses, type WorkItemStatusReference } from "@/lib/api/reference-data";

const text = {
  en: { back: "Back to work items", loading: "Loading work item...", error: "Work item could not be loaded.", notFound: "This work item is no longer available.", edit: "Edit work item", save: "Save changes", saving: "Saving...", cancel: "Cancel", description: "Description", project: "Project", noProject: "No project", searchProjects: "Search by project name or code...", noProjects: "No matching projects found.", assignedTo: "Assigned to", assignee: "Assignee", assign: "Assign", reassign: "Reassign", unassign: "Unassign", unassigned: "Unassigned", selectAssignee: "Select assignee", selectProjectFirst: "Select a project first", noEligibleProjectPeople: "No eligible project people found", assigneeInvalidForProject: "Selected assignee is not an active member of this project.", searchAssignees: "Search assignees...", notAvailable: "Not available", searchUsers: "Search users...", noUsers: "No users found.", formatting: "Formatting", bold: "Bold", italic: "Italic", underline: "Underline", bulletedList: "Bulleted list", numberedList: "Numbered list", emoji: "Emoji", priority: "Priority", status: "Status", type: "Type", severity: "Severity", resolution: "Resolution", deadline: "Deadline", targetResolutionDate: "Target resolution date", history: "Activity history", historyHint: "System-generated record of changes. Activity cannot be entered manually.", noHistory: "No activity recorded yet.", comments: "Comments", addComment: "Add comment", reply: "Reply", showReplies: "Show replies", hideReplies: "Hide replies", replyCount: "reply", replies: "replies", commentPlaceholder: "Write a comment...", replyPlaceholder: "Write a reply...", submitComment: "Post comment", submitReply: "Post reply", savingComment: "Posting...", noComments: "No comments yet.", commentsLoadError: "Comments could not be loaded.", commentSaveError: "Your comment could not be posted.", commentRequired: "Comment text is required.", attachments: "Attachments", chooseFiles: "Choose files", selectedAttachments: "Selected attachments", pendingAttachments: "Pending attachments", pendingUpload: "Ready to upload", uploading: "Uploading...", upload: "Upload", attached: "Attached", download: "Download", remove: "Remove", noAttachments: "No attachments yet.", attachmentsLoadError: "Attachments could not be loaded.", attachmentUploadError: "Attachments could not be uploaded.", attachmentDownloadError: "The attachment could not be downloaded.", attachmentRemoveError: "The attachment could not be removed.", system: "System", created: "Created", updated: "Updated", transitioned: "Status or type changed", deleted: "Deleted", task: "Task", issue: "Issue", done: "Done", backlog: "Backlog", todo: "To do", inProgress: "In progress", inReview: "In review", cancelled: "Cancelled", onHold: "On hold", critical: "Critical", high: "High", medium: "Medium", low: "Low", errorSave: "Changes could not be saved.", titleRequired: "Title is required.", due: "Deadline", backTo: "Back", to: "to", from: "from", title: "Title", previous: "Previous page", next: "Next page", page: "Page", of: "of", details: "Details", workItem: "Work item" },
  ar: { back: "العودة إلى عناصر العمل", loading: "جاري تحميل عنصر العمل...", error: "تعذر تحميل عنصر العمل.", notFound: "عنصر العمل لم يعد متاحًا.", edit: "تعديل عنصر العمل", save: "حفظ التغييرات", saving: "جاري الحفظ...", cancel: "إلغاء", description: "الوصف", project: "المشروع", noProject: "بدون مشروع", searchProjects: "ابحث باسم المشروع أو رمزه...", noProjects: "لا توجد مشروعات مطابقة.", assignedTo: "مسند إلى", assignee: "المسند إليه", assign: "إسناد", reassign: "إعادة إسناد", unassign: "إلغاء الإسناد", unassigned: "غير مسند", selectAssignee: "اختر المسند إليه", selectProjectFirst: "اختر مشروعًا أولًا", noEligibleProjectPeople: "لا يوجد أعضاء مؤهلون في المشروع", assigneeInvalidForProject: "المسند إليه المحدد ليس عضوًا نشطًا في هذا المشروع.", searchAssignees: "ابحث عن أعضاء المشروع...", notAvailable: "غير متاح", searchUsers: "ابحث عن مستخدم...", noUsers: "لا يوجد مستخدمون.", formatting: "التنسيق", bold: "عريض", italic: "مائل", underline: "تحته خط", bulletedList: "قائمة نقطية", numberedList: "قائمة رقمية", emoji: "رمز تعبيري", priority: "الأولوية", status: "الحالة", type: "النوع", severity: "الخطورة", resolution: "الحل", deadline: "الموعد النهائي", targetResolutionDate: "تاريخ الحل المستهدف", history: "سجل النشاط", historyHint: "سجل تلقائي لتغييرات النظام. لا يمكن إدخال النشاط يدويًا.", noHistory: "لا يوجد نشاط مسجل بعد.", comments: "التعليقات", addComment: "إضافة تعليق", reply: "رد", showReplies: "عرض الردود", hideReplies: "إخفاء الردود", replyCount: "رد", replies: "ردود", commentPlaceholder: "اكتب تعليقًا...", replyPlaceholder: "اكتب ردًا...", submitComment: "نشر التعليق", submitReply: "نشر الرد", savingComment: "جاري النشر...", noComments: "لا توجد تعليقات بعد.", commentsLoadError: "تعذر تحميل التعليقات.", commentSaveError: "تعذر نشر التعليق.", commentRequired: "نص التعليق مطلوب.", attachments: "المرفقات", chooseFiles: "اختيار ملفات", selectedAttachments: "المرفقات المحددة", pendingAttachments: "المرفقات قيد الرفع", pendingUpload: "جاهز للرفع", uploading: "جاري الرفع...", upload: "رفع", attached: "مرفق", download: "تنزيل", remove: "إزالة", noAttachments: "لا توجد مرفقات بعد.", attachmentsLoadError: "تعذر تحميل المرفقات.", attachmentUploadError: "تعذر رفع المرفقات.", attachmentDownloadError: "تعذر تنزيل المرفق.", attachmentRemoveError: "تعذر إزالة المرفق.", system: "النظام", created: "تم الإنشاء", updated: "تم التعديل", transitioned: "تم تغيير الحالة أو النوع", deleted: "تم الحذف", task: "مهمة", issue: "مشكلة", done: "مكتمل", backlog: "المتراكم", todo: "للتنفيذ", inProgress: "قيد التنفيذ", inReview: "قيد المراجعة", cancelled: "ملغى", onHold: "متوقف", critical: "حرج", high: "عالية", medium: "متوسطة", low: "منخفضة", errorSave: "تعذر حفظ التغييرات.", titleRequired: "العنوان مطلوب.", due: "الموعد النهائي", backTo: "عودة", to: "إلى", from: "من", title: "العنوان", previous: "الصفحة السابقة", next: "الصفحة التالية", page: "صفحة", of: "من", details: "التفاصيل", workItem: "عنصر العمل" },
} as const;

type Copy = Record<string, string>;
const priorities: WorkItemPriority[] = ["Critical", "High", "Medium", "Low", "Backlog"];

function label(value: string, t: Copy, statuses: readonly WorkItemStatusReference[] = [], locale = "en") {
  if (statuses.some((status) => status.code === value)) return getWorkItemStatusName(value, statuses, locale, value);
  const labels: Record<string, string> = { Task: t.task, Issue: t.issue, Done: t.done, Backlog: t.backlog, ToDo: t.todo, InProgress: t.inProgress, InReview: t.inReview, Cancelled: t.cancelled, OnHold: t.onHold, Critical: t.critical, High: t.high, Medium: t.medium, Low: t.low, Created: t.created, Updated: t.updated, Transitioned: t.transitioned, Deleted: t.deleted, Assigned: t.assigned, Reassigned: t.reassigned, Unassigned: t.unassigned };
  return labels[value] ?? value;
}

function activityVariant(action: string): "status-green" | "status-blue" | "status-amber" | "status-red" | "status-neutral" {
  if (action === "Created") return "status-green";
  if (action === "Updated" || action === "Assigned" || action === "Reassigned") return "status-blue";
  if (action === "Unassigned") return "status-amber";
  if (action === "Transitioned") return "status-amber";
  if (action === "Deleted") return "status-red";
  return "status-neutral";
}

function formatChanges(raw: string | undefined, t: Copy, statuses: readonly WorkItemStatusReference[] = [], locale = "en") {
  if (!raw) return null;
  try {
    const changes = JSON.parse(raw) as Record<string, { from?: unknown; to?: unknown } | unknown>;
    const fieldLabels: Record<string, string> = { title: t.title, description: t.description, type: t.type, priority: t.priority, status: t.status, projectId: t.project, assignee: t.assignee, dueDate: t.due, severity: t.severity, targetResolutionDate: t.due, resolvedDate: t.due, resolution: t.resolution };
    return Object.entries(changes).map(([field, value]) => {
      const entry = value as { from?: unknown; to?: unknown };
      const formatValue = (current: unknown) => typeof current === "string" ? label(current, t, statuses, locale) : String(current ?? "—");
      if (entry && typeof entry === "object" && ("from" in entry || "to" in entry)) return `${fieldLabels[field] ?? field}: ${formatValue(entry.from)} ${t.to} ${formatValue(entry.to)}`;
      return `${fieldLabels[field] ?? field}: ${formatValue(value)}`;
    }).join(" · ");
  } catch { return raw; }
}

function DetailField({ label: fieldLabel, children }: { label: string; children: ReactNode }) {
  return <div className="space-y-1.5 border-b border-border/60 pb-3 last:border-0 last:pb-0"><p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{fieldLabel}</p><div className="min-w-0 text-sm">{children}</div></div>;
}

export function WorkItemDetails() {
  const locale = useLocale();
  const t = locale === "ar" ? text.ar : text.en;
  const params = useParams<{ id: string }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const router = useRouter();
  const { user } = useAuth();
  const { effectiveReadOnly } = useManagementPerspective();
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit) && !effectiveReadOnly);
  const queryClient = useQueryClient();
  const itemQuery = useWorkItem(id);
  const workflow = useWorkItemWorkflow(id);
  const statusesQuery = useWorkItemStatuses(true);
  const [activityPage, setActivityPage] = useState(1);
  const [detailTab, setDetailTab] = useState<"comments" | "history">("history");
  const activityQuery = useWorkItemActivity(id, activityPage, 20, detailTab === "history");
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, canEdit);
  const { update } = useWorkItemMutations();
  const attachmentMutation = useWorkItemAttachmentMutation(id);
  const [editing, setEditing] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState<SaveWorkItemDto | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const assigneesQuery = useWorkItemAssignees(form?.projectId, canEdit && Boolean(form?.projectId));

  useEffect(() => {
    const item = itemQuery.data;
    if (item) { setForm({ title: item.title, description: item.description, type: item.type, priority: item.priority, projectId: item.projectId, assigneeId: item.assigneeId, parentId: item.parentId, storyPoints: item.storyPoints, estimatedHours: item.estimatedHours, actualHours: item.actualHours, startDate: item.startDate, dueDate: item.dueDate, severity: item.severity, targetResolutionDate: item.targetResolutionDate, resolvedDate: item.resolvedDate, resolution: item.resolution, tags: item.tags, labels: item.labels, rowVersion: item.rowVersion }); if (item.type === "Task") setDetailTab("comments"); }
  }, [itemQuery.data]);

  const item = itemQuery.data;
  const statusOptions = statusesQuery.data ?? [];
  const projects = projectsQuery.data?.items ?? [];
  const assigneeCandidates = assigneesQuery.data ?? [];
  const assigneeName = item?.assigneeId ? item.assigneeName ?? t.notAvailable : t.unassigned;
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const set = <K extends keyof SaveWorkItemDto>(key: K, value: SaveWorkItemDto[K]) => setForm((current) => current ? { ...current, [key]: value } : current);
  const changeProject = (value: string | undefined) => {
    if (!form) return;
    const nextProjectId = value ?? "";
    const hadAssignee = Boolean(form.assigneeId && form.projectId !== nextProjectId);
    setForm((current) => current ? { ...current, projectId: nextProjectId, assigneeId: current.projectId === nextProjectId ? current.assigneeId : undefined } : current);
    if (hadAssignee) setError(t.assigneeInvalidForProject);
  };
  const projectField = form?.type === "Task"
    ? <ProjectPicker id="item-project" label={t.project} value={form.projectId} onChange={changeProject} projects={projects} isLoading={projectsQuery.isLoading} t={t} />
    : <div className="space-y-1.5"><label htmlFor="item-project" className="text-sm font-medium">{t.project}</label><select id="item-project" className="h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={form?.projectId ?? ""} onChange={(event) => changeProject(event.target.value)}><option value="">{t.noProject}</option>{projects.map((project) => <option key={project.id} value={project.id}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}</select></div>;
  const assigneeField = <div className="space-y-1.5"><label htmlFor="item-assignee" className="text-sm font-medium">{t.assignee}</label><WorkItemAssigneeSelect value={form?.assigneeId} candidates={assigneeCandidates} loading={assigneesQuery.isLoading} disabled={!form?.projectId} onValueChange={(value) => { setError(""); set("assigneeId", value || undefined); }} placeholder={form?.projectId ? t.selectAssignee : t.selectProjectFirst} searchPlaceholder={t.searchAssignees} emptyText={t.noEligibleProjectPeople} clearLabel={t.unassign} /></div>;

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!form || !form.title.trim()) { setError(t.titleRequired); return; }
    setError("");
    try { await update.mutateAsync({ id, body: { ...form, title: form.title.trim(), projectId: form.projectId || undefined } }); if (files.length > 0) await attachmentMutation.mutateAsync(files); setFiles([]); setEditing(false); await queryClient.invalidateQueries({ queryKey: ["work-items", "activity", id] }); } catch { setError(files.length > 0 ? t.attachmentUploadError : t.errorSave); }
  };

  if (itemQuery.isLoading) return <div className="space-y-4"><Skeleton className="h-10 w-1/2" /><Skeleton className="h-48" /><Skeleton className="h-64" /></div>;
  if (itemQuery.isError || !item || !form) return <div className="space-y-4"><Button variant="ghost" onClick={() => router.push(`/${locale}/work-items`)}><ArrowLeft className="me-2 h-4 w-4 rtl:rotate-180" />{t.back}</Button><Card><CardContent className="flex min-h-48 flex-col items-center justify-center gap-2 text-center"><CircleAlert className="h-8 w-8 text-destructive/70" /><p role="alert" className="text-sm text-destructive">{itemQuery.isError ? t.error : t.notFound}</p></CardContent></Card></div>;

  const typeIcon = item.type === "Issue" ? <Ticket className="h-5 w-5" /> : <Wrench className="h-5 w-5" />;
  const typeTone = item.type === "Issue" ? "bg-info/10 text-info" : "bg-primary/10 text-primary";
  const deadline = item.type === "Task" ? item.dueDate : item.targetResolutionDate;
  const deadlineLabel = item.type === "Task" ? t.deadline : t.targetResolutionDate;
  const statusTone = getWorkItemStatusColorClasses(statusOptions.find((status) => status.code === item.status)?.color);
  const changeWorkflowState = async (transitionCode: string) => {
    setError("");
    try { await workflow.execute.mutateAsync({ transitionCode }); }
    catch { setError(locale === "ar" ? "تعذر تنفيذ الانتقال. حدّث الشاشة وحاول مرة أخرى." : "The transition could not be executed. Refresh and try again."); }
  };

  return <div className="flex h-[calc(100dvh-9rem)] min-h-[34rem] min-w-0 flex-col gap-3">
    <div className="flex shrink-0 flex-wrap items-center justify-between gap-2"><Button variant="ghost" size="sm" onClick={() => router.push(`/${locale}/work-items`)}><ArrowLeft className="me-2 h-4 w-4 rtl:rotate-180" />{t.back}</Button>{canEdit && !editing ? <Button variant="outline" size="sm" onClick={() => setEditing(true)}><Pencil className="me-2 h-4 w-4" />{t.edit}</Button> : null}</div>
    <div className="min-h-0 flex-1 overflow-y-auto rounded-xl border border-border/70 bg-muted/20 p-3 sm:p-4 lg:p-5"><div className="mx-auto max-w-[1440px] space-y-4">
      <Card className="overflow-hidden border-border/70 shadow-sm"><div className="border-b bg-background px-4 py-5 sm:px-6"><div className="flex min-w-0 items-start gap-3"><div className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${typeTone}`}>{typeIcon}</div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span className="font-semibold uppercase tracking-wide">{label(item.type, t)}</span><span aria-hidden="true">·</span><span className="max-w-[16rem] truncate" title={id}>#{id}</span></div><h1 className="mt-1 break-words text-xl font-semibold tracking-tight sm:text-2xl">{item.title}</h1><div className="mt-3 flex flex-wrap items-center gap-2"><Badge variant="outline" className={`${statusTone.badge} gap-1.5`}><span className={`h-2 w-2 rounded-full ${statusTone.dot}`} />{label(item.status, t, statusOptions, locale)}</Badge><Badge status={item.priority}>{label(item.priority, t)}</Badge>{item.severity ? <Badge status={item.severity}>{label(item.severity, t)}</Badge> : null}{deadline ? <span className="inline-flex items-center gap-1.5 text-xs font-medium text-warning"><CalendarDays className="h-3.5 w-3.5" />{deadlineLabel}: {formatDateTime(deadline, dateLocale)}</span> : null}</div></div></div></div>
        {!editing ? <div className="border-t bg-muted/20 px-4 py-3 sm:px-6"><div className="flex flex-wrap items-center gap-2"><span className="text-xs font-medium text-muted-foreground">{t.status}:</span>{workflow.availableTransitions.length === 0 ? <span className="text-sm text-muted-foreground">{t.notAvailable}</span> : workflow.availableTransitions.map((transition) => <Button key={transition.code} type="button" size="sm" variant="outline" disabled={!canEdit || !transition.canExecute || workflow.execute.isPending} onClick={() => void changeWorkflowState(transition.code)}>{locale === "ar" ? transition.nameAr : transition.nameEn}</Button>)}</div></div> : null}
        {editing ? <CardContent className="p-4 sm:p-6"><form onSubmit={save} className="space-y-5">{error ? <p role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}<div className="space-y-1.5"><label htmlFor="item-title" className="text-sm font-medium">{t.title}</label><Input id="item-title" value={form.title} onChange={(event) => set("title", event.target.value)} maxLength={500} /></div><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1.5"><label htmlFor="item-type" className="text-sm font-medium">{t.type}</label><select id="item-type" className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.type} onChange={(event) => set("type", event.target.value as WorkItemType)}><option value="Task">{t.task}</option><option value="Issue">{t.issue}</option></select></div><div className="space-y-1.5"><label htmlFor="item-priority" className="text-sm font-medium">{t.priority}</label><select id="item-priority" className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.priority} onChange={(event) => set("priority", event.target.value as WorkItemPriority)}>{priorities.map((priority) => <option key={priority} value={priority}>{label(priority, t)}</option>)}</select></div>{projectField}{assigneeField}<div className="space-y-1.5"><label htmlFor="item-target-date" className="text-sm font-medium">{form.type === "Task" ? t.deadline : t.targetResolutionDate}</label><Input id="item-target-date" type="date" value={(form.type === "Task" ? form.dueDate : form.targetResolutionDate)?.slice(0, 10) ?? ""} onChange={(event) => set(form.type === "Task" ? "dueDate" : "targetResolutionDate", event.target.value)} /></div></div>{form.type === "Issue" ? <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1.5"><label htmlFor="item-severity" className="text-sm font-medium">{t.severity}</label><Input id="item-severity" value={form.severity ?? ""} onChange={(event) => set("severity", event.target.value)} /></div><div className="space-y-1.5"><label htmlFor="item-resolution" className="text-sm font-medium">{t.resolution}</label><Input id="item-resolution" value={form.resolution ?? ""} onChange={(event) => set("resolution", event.target.value)} /></div></div> : null}<WorkItemAttachments canManage={canEdit} t={t} files={files} onFilesChange={setFiles} /><div className="space-y-1.5"><label htmlFor="item-description" className="text-sm font-medium">{t.description}</label><RichTextEditor id="item-description" value={form.description ?? ""} onChange={(value) => set("description", value)} placeholder={t.description} ariaLabel={t.description} t={t} /></div><div className="flex flex-wrap justify-end gap-2"><Button type="button" variant="outline" onClick={() => { setEditing(false); setError(""); }}>{t.cancel}</Button><Button type="submit" disabled={update.isPending}><Save className="me-2 h-4 w-4" />{update.isPending ? t.saving : t.save}</Button></div></form></CardContent> : <div className="grid gap-4 p-4 sm:p-6 lg:grid-cols-[minmax(0,1fr)_18rem]"><div className="flex h-[75dvh] max-h-[75dvh] min-h-0 min-w-0 flex-col rounded-lg border border-border/60 bg-muted/[0.14] p-4"><div className="mb-3 flex shrink-0 items-center gap-2"><Wrench className="h-4 w-4 text-primary" /><h2 className="text-sm font-semibold">{t.description}</h2></div><div className="min-h-0 flex-1 overflow-y-auto pe-2"><RichTextContent value={item.description} /></div></div><aside className="min-w-0 space-y-4"><div className="space-y-4 rounded-lg border border-border/60 bg-background p-4"><div className="flex items-center gap-2 border-b border-border/60 pb-3"><div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-primary"><Wrench className="h-4 w-4" /></div><h2 className="text-sm font-semibold">{t.details}</h2></div><DetailField label={t.project}><span className="block truncate" title={item.projectName || t.noProject}>{item.projectName || t.noProject}</span></DetailField><DetailField label={t.assignedTo}><span className="block truncate" title={assigneeName}>{assigneeName}</span></DetailField></div><WorkItemAttachments workItemId={item.id} canManage={canEdit || item.createdBy === user?.id} t={t} /></aside></div>}
      </Card>
      {!editing ? <Tabs value={detailTab} onValueChange={(value) => { setDetailTab(value as "comments" | "history"); if (value === "history") setActivityPage(1); }} className="w-full"><TabsList className="flex h-auto w-full flex-wrap justify-start gap-1 rounded-xl border border-border/70 bg-muted/30 p-1 sm:w-fit"><>{item.type === "Task" ? <TabsTrigger value="comments" className="flex-1 gap-1.5 rounded-lg data-[state=active]:bg-info/10 data-[state=active]:text-info sm:flex-none"><MessageSquare className="h-3.5 w-3.5" />{t.comments}</TabsTrigger> : null}<TabsTrigger value="history" className="flex-1 gap-1.5 rounded-lg data-[state=active]:bg-primary/10 data-[state=active]:text-primary sm:flex-none"><History className="h-3.5 w-3.5" />{t.history}</TabsTrigger></></TabsList>{item.type === "Task" ? <TabsContent value="comments"><TaskComments workItemId={item.id} locale={locale} t={t} /></TabsContent> : null}<TabsContent value="history"><Card className="border-primary/15 shadow-sm"><CardHeader><CardTitle className="flex items-center gap-2 text-base"><History className="h-4 w-4 text-primary" />{t.history}</CardTitle><p className="text-xs text-muted-foreground">{t.historyHint}</p></CardHeader><CardContent>{activityQuery.isLoading ? <div className="space-y-3"><Skeleton className="h-14" /><Skeleton className="h-14" /></div> : activityQuery.isError ? <p role="alert" className="rounded-lg border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t.error}</p> : activityQuery.data?.items.length ? <div className={`space-y-4 ${activityQuery.isFetching ? "opacity-60" : ""}`}>{activityQuery.data.items.map((entry) => <div key={entry.id} className={`flex gap-3 border-s-2 ps-4 ${entry.action === "Created" ? "border-success" : entry.action === "Transitioned" ? "border-warning" : entry.action === "Deleted" ? "border-destructive" : "border-info"}`}><div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted/70"><span className={`h-2 w-2 rounded-full ${entry.action === "Created" ? "bg-success" : entry.action === "Transitioned" ? "bg-warning" : entry.action === "Deleted" ? "bg-destructive" : "bg-info"}`} /></div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><Badge variant={activityVariant(entry.action)}>{label(entry.action, t)}</Badge><span className="text-[11px] text-muted-foreground">{entry.userName || entry.userId || t.system} · {formatDateTime(entry.timestamp, dateLocale)}</span></div><p className="mt-2 break-words text-xs leading-5 text-muted-foreground">{formatChanges(entry.changes, t) || "—"}</p></div></div>)}</div> : <p className="text-sm text-muted-foreground">{t.noHistory}</p>}{activityQuery.data && activityQuery.data.totalPages > 1 ? <Pagination page={activityPage} totalPages={activityQuery.data.totalPages} previous={t.previous} next={t.next} pageLabel={t.page} ofLabel={t.of} disabled={activityQuery.isFetching} onPageChange={setActivityPage} /> : null}</CardContent></Card></TabsContent></Tabs> : null}
    </div></div>
  </div>;
}

function Pagination({ page, totalPages, previous, next, pageLabel, ofLabel, disabled, onPageChange }: { page: number; totalPages: number; previous: string; next: string; pageLabel: string; ofLabel: string; disabled: boolean; onPageChange: (_page: number) => void }) {
  return <TablePagination className="border-t border-border/60 pt-4" page={page} totalPages={totalPages} summary={`${pageLabel} ${page} ${ofLabel} ${totalPages}`} previousLabel={previous} nextLabel={next} onPageChange={onPageChange} disabled={disabled} />;
}
