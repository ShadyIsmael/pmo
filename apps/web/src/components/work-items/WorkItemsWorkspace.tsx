"use client";

import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useLocale } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { AlertCircle, CalendarDays, CheckCircle2, Clock3, Columns3, Filter, GripVertical, List, Plus, Search, Ticket, UserRound, Wrench } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { KpiCard } from "@/components/ui/kpi-card";
import { EmptyState, ErrorState } from "@/components/ui/state-display";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { RichTextEditor } from "@/components/ui/rich-text-editor";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { ProjectPicker } from "@/components/work-items/ProjectPicker";
import { WorkItemAttachments } from "@/components/work-items/WorkItemAttachments";
import { WorkItemAssigneeSelect } from "@/components/work-items/WorkItemAssigneeSelect";
import { useProjects } from "@/hooks/projects/useProjects";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { getWorkItemStatusColorClasses } from "@/lib/status-presentation";
import { formatDate } from "@/lib/utils";
import { useWorkItemAssignees, useWorkItemMutations, useWorkItems, useWorkItemWorkflowTransition, workItemsApi, type SaveWorkItemDto, type WorkItemDto, type WorkItemPriority, type WorkItemStatus, type WorkItemType, type WorkflowTransitionOption } from "@/lib/api/work-items";
import { useManagementPerspective } from "@/lib/perspective-context";
import { getWorkItemStatusName, useWorkItemStatuses, type WorkItemStatusReference } from "@/lib/api/reference-data";

export type WorkItemsScope = "all" | "task" | "issue";
export type CreateWorkItemType = Extract<WorkItemType, "Task" | "Issue">;
export type Copy = Record<string, string>;

export const labels: { en: Copy; ar: Copy } = {
  en: {
    title: "Work Items", description: "Manage tasks and issues across your work.", myWork: "My Work", myWorkDescription: "Your task queue, priorities, and deadlines in one place.", tasksTitle: "Tasks", tasksDescription: "Manage and track your assigned and project-related tasks.", issuesTitle: "Issues", issuesDescription: "Track and manage project issues requiring action.", totalWorkItems: "total work items", totalTasks: "total tasks", totalIssues: "total issues", newTask: "New Task", newIssue: "New Issue", createPermissionHint: "You don't have permission to create work items.", createTaskDescription: "Create a task and start tracking the work.", createIssueDescription: "Create an issue requiring attention.", createError: "Work item could not be created.", attachmentUploadError: "The work item was created, but the attachments could not be uploaded.", type: "Type", task: "Task", issue: "Issue", all: "All", allTasks: "All tasks", open: "Open", done: "Done", critical: "Critical", search: "Search work items...", searchTasks: "Search tasks...", filter: "Filter", titleField: "Title", project: "Project", priority: "Priority", due: "Due date", deadline: "Deadline", targetResolutionDate: "Target resolution date", status: "Status", allStatuses: "All statuses", inProgressTasks: "In progress", dueSoon: "Due soon", completedTasks: "Completed", clearSearch: "Clear search", viewDetails: "View details", viewMode: "Work item view", listView: "List", kanbanView: "Kanban", dragToMove: "Drag to move", moving: "Updating...", moveError: "Work item status could not be updated.", formatting: "Formatting", bold: "Bold", italic: "Italic", underline: "Underline", bulletedList: "Bulleted list", numberedList: "Numbered list", emoji: "Emoji", emptyWorkItems: "No work items found", emptyTasks: "No tasks found", emptyIssues: "No issues found", emptyWorkItemsHint: "Create a task or issue to start the workflow.", emptyTasksHint: "Create a task to start tracking work.", emptyIssuesHint: "Create an issue to start tracking items requiring attention.", loading: "Loading work items...", error: "Work items could not be loaded.", retry: "Retry", cancel: "Cancel", create: "Create", descriptionField: "Description", noProject: "No project", searchProjects: "Search by project name or code...", noProjects: "No matching projects found.", validation: "Title is required.", assignee: "Assignee", assign: "Assign", reassign: "Reassign", unassign: "Unassign", unassigned: "Unassigned", selectAssignee: "Select assignee", selectProjectFirst: "Select a project first", noEligibleProjectPeople: "No eligible project people found", assigneeInvalidForProject: "Selected assignee is not an active member of this project.", searchAssignees: "Search assignees...", attachments: "Attachments", chooseFiles: "Choose files", selectedAttachments: "Selected attachments", pendingUpload: "Ready to upload", uploading: "Uploading...", upload: "Upload", remove: "Remove", severity: "Severity", low: "Low", medium: "Medium", high: "High", criticalPriority: "Critical", todo: "To do", inProgress: "In progress", inReview: "In review", backlog: "Backlog", onHold: "On hold", cancelled: "Cancelled", doneStatus: "Done"
  },
  ar: {
    title: "عناصر العمل", description: "إدارة المهام والمشكلات عبر أعمالك.", myWork: "عملي", myWorkDescription: "كل مهامك وأولوياتك ومواعيدك النهائية في مكان واحد.", tasksTitle: "المهام", tasksDescription: "إدارة ومتابعة مهامك المسندة والمرتبطة بالمشروعات.", issuesTitle: "المشكلات", issuesDescription: "متابعة وإدارة مشكلات المشروعات التي تتطلب إجراءً.", totalWorkItems: "إجمالي عناصر العمل", totalTasks: "إجمالي المهام", totalIssues: "إجمالي المشكلات", newTask: "مهمة جديدة", newIssue: "مشكلة جديدة", createPermissionHint: "ليس لديك صلاحية إنشاء عناصر العمل.", createTaskDescription: "أنشئ مهمة وابدأ في متابعة العمل.", createIssueDescription: "أنشئ مشكلة تتطلب اهتمامًا.", createError: "تعذر إنشاء عنصر العمل.", attachmentUploadError: "تم إنشاء عنصر العمل، لكن تعذر رفع المرفقات.", type: "النوع", task: "مهمة", issue: "مشكلة", all: "الكل", allTasks: "كل المهام", open: "مفتوح", done: "مكتمل", critical: "حرج", search: "ابحث في عناصر العمل...", searchTasks: "ابحث في المهام...", filter: "تصفية", titleField: "العنوان", project: "المشروع", priority: "الأولوية", due: "تاريخ الاستحقاق", deadline: "الموعد النهائي", targetResolutionDate: "تاريخ الحل المستهدف", status: "الحالة", allStatuses: "كل الحالات", inProgressTasks: "قيد التنفيذ", dueSoon: "تستحق قريبًا", completedTasks: "مكتملة", clearSearch: "مسح البحث", viewDetails: "عرض التفاصيل", viewMode: "طريقة عرض عناصر العمل", listView: "قائمة", kanbanView: "كانبان", dragToMove: "اسحب للنقل", moving: "جاري التحديث...", moveError: "تعذر تحديث حالة عنصر العمل.", formatting: "التنسيق", bold: "عريض", italic: "مائل", underline: "تحته خط", bulletedList: "قائمة نقطية", numberedList: "قائمة رقمية", emoji: "رمز تعبيري", emptyWorkItems: "لا توجد عناصر عمل", emptyTasks: "لا توجد مهام", emptyIssues: "لا توجد مشكلات", emptyWorkItemsHint: "أنشئ مهمة أو مشكلة لبدء المسار.", emptyTasksHint: "أنشئ مهمة لبدء متابعة العمل.", emptyIssuesHint: "أنشئ مشكلة لبدء متابعة العناصر التي تتطلب اهتمامًا.", loading: "جاري تحميل عناصر العمل...", error: "تعذر تحميل عناصر العمل.", retry: "إعادة المحاولة", cancel: "إلغاء", create: "إنشاء", descriptionField: "الوصف", noProject: "بدون مشروع", searchProjects: "ابحث باسم المشروع أو رمزه...", noProjects: "لا توجد مشروعات مطابقة.", validation: "العنوان مطلوب.", assignee: "المسند إليه", assign: "إسناد", reassign: "إعادة إسناد", unassign: "إلغاء الإسناد", unassigned: "غير مسند", selectAssignee: "اختر المسند إليه", selectProjectFirst: "اختر مشروعًا أولًا", noEligibleProjectPeople: "لا يوجد أعضاء مؤهلون في المشروع", assigneeInvalidForProject: "المسند إليه المحدد ليس عضوًا نشطًا في هذا المشروع.", searchAssignees: "ابحث عن أعضاء المشروع...", attachments: "المرفقات", chooseFiles: "اختيار ملفات", selectedAttachments: "المرفقات المحددة", pendingUpload: "جاهز للرفع", uploading: "جاري الرفع...", upload: "رفع", remove: "إزالة", severity: "الخطورة", low: "منخفضة", medium: "متوسطة", high: "عالية", criticalPriority: "حرجة", todo: "للتنفيذ", inProgress: "قيد التنفيذ", inReview: "قيد المراجعة", backlog: "المتراكم", onHold: "متوقف", cancelled: "ملغى", doneStatus: "مكتمل"
  }
};

export const priorities: WorkItemPriority[] = ["Critical", "High", "Medium", "Low", "Backlog"];
const scopeTypes: Record<Exclude<WorkItemsScope, "all">, CreateWorkItemType> = { task: "Task", issue: "Issue" };
export const statusLabel = (value: WorkItemStatus, _t: Copy, statuses: readonly WorkItemStatusReference[] = [], locale = "en") => getWorkItemStatusName(value, statuses, locale, value);
export const typeLabel = (value: WorkItemType, t: Copy) => value === "Issue" ? t.issue : value === "Task" ? t.task : value;
export const priorityLabel = (value: WorkItemPriority, t: Copy) => ({ Critical: t.criticalPriority, High: t.high, Medium: t.medium, Low: t.low, Backlog: t.backlog }[value]);
const priorityClass = (value: WorkItemPriority) => value === "Critical" ? "text-destructive" : value === "High" ? "text-amber-600 dark:text-amber-400" : value === "Medium" ? "text-blue-700 dark:text-blue-300" : "text-muted-foreground";
const statusClass = (value: WorkItemStatus, statuses: readonly WorkItemStatusReference[]) => getWorkItemStatusColorClasses(statuses.find((status) => status.code === value)?.color).text;

function SelectField({ id, label, value, options, onChange, disabled = false }: { id: string; label: string; value: string; options: string[][]; onChange: (_value: string) => void; disabled?: boolean }) {
  return <div className="space-y-1.5"><label htmlFor={id} className="text-sm font-medium">{label}</label><select id={id} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)} disabled={disabled}>{options.map(([optionValue, optionLabel]) => <option key={optionValue} value={optionValue}>{optionLabel}</option>)}</select></div>;
}

export function CreateSheet({ open, onOpenChange, t, locale, defaultType, statuses: _statuses }: { open: boolean; onOpenChange: (_value: boolean) => void; t: Copy; locale: string; defaultType: CreateWorkItemType; statuses: readonly WorkItemStatusReference[] }) {
  const router = useRouter();
  const { create } = useWorkItemMutations();
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, open);
  const [form, setForm] = useState({ title: "", description: "", type: defaultType as WorkItemType, priority: "Medium" as WorkItemPriority, projectId: "", assigneeId: "", dueDate: "", targetResolutionDate: "", severity: "Medium" });
  const [files, setFiles] = useState<File[]>([]);
  const [error, setError] = useState("");
  const [assigneeNotice, setAssigneeNotice] = useState("");
  const update = (key: keyof typeof form, value: string) => setForm((current) => ({ ...current, [key]: value }));
  const assigneesQuery = useWorkItemAssignees(form.projectId, open && Boolean(form.projectId));
  const assigneeCandidates = assigneesQuery.data ?? [];
  const close = (value: boolean) => {
    if (!value) {
       setForm({ title: "", description: "", type: defaultType, priority: "Medium", projectId: "", assigneeId: "", dueDate: "", targetResolutionDate: "", severity: "Medium" });
      setFiles([]);
      setError("");
      setAssigneeNotice("");
    }
    onOpenChange(value);
  };
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.title.trim()) {
      setError(t.validation);
      return;
    }
    try {
      const item = await create.mutateAsync({ title: form.title.trim(), description: form.description.trim() || undefined, type: defaultType, priority: form.priority, projectId: form.projectId || undefined, assigneeId: form.assigneeId || undefined, dueDate: defaultType === "Task" ? form.dueDate || undefined : undefined, targetResolutionDate: defaultType === "Issue" ? form.targetResolutionDate || undefined : undefined, severity: defaultType === "Issue" ? form.severity : undefined } as SaveWorkItemDto);
      if (files.length > 0) await workItemsApi.uploadAttachments(item.id, files);
      close(false);
      router.push(`/${locale}/work-items/${encodeURIComponent(item.id)}`);
    } catch {
      setError(t.createError);
    }
  };
  const projects = projectsQuery.data?.items ?? [];
  const isIssue = defaultType === "Issue";
  const changeProject = (value: string) => {
    const hadAssignee = Boolean(form.assigneeId && form.projectId !== value);
    setForm((current) => ({ ...current, projectId: value, assigneeId: current.projectId === value ? current.assigneeId : "" }));
    setAssigneeNotice(hadAssignee ? t.assigneeInvalidForProject : "");
  };
  const projectField = defaultType === "Task"
    ? <ProjectPicker id="work-item-project" label={t.project} value={form.projectId || undefined} onChange={(value) => changeProject(value ?? "")} projects={projects} isLoading={projectsQuery.isLoading} t={t} />
    : <SelectField id="work-item-project" label={t.project} value={form.projectId} onChange={changeProject} options={[["", t.noProject], ...projects.map((project) => [project.id, project.code ? `${project.code} · ${project.name}` : project.name])]} />;

  return <SideSheet open={open} onOpenChange={close} title={isIssue ? t.newIssue : t.newTask} description={isIssue ? t.createIssueDescription : t.createTaskDescription} size="md" closeLabel={t.cancel}><form onSubmit={submit} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4">{error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}{assigneeNotice ? <p className="rounded-md border border-amber-500/30 bg-amber-500/5 px-3 py-2 text-sm text-amber-700 dark:text-amber-300">{assigneeNotice}</p> : null}<div className="space-y-1.5"><label htmlFor="work-item-title" className="text-sm font-medium">{t.titleField}</label><Input id="work-item-title" value={form.title} onChange={(event) => update("title", event.target.value)} autoFocus maxLength={500} /></div><div className="grid gap-4 sm:grid-cols-2"><SelectField id="work-item-priority" label={t.priority} value={form.priority} onChange={(value) => update("priority", value)} options={priorities.map((value) => [value, value === "Critical" ? t.criticalPriority : value === "High" ? t.high : value === "Medium" ? t.medium : value === "Low" ? t.low : value])} />{projectField}<div className="space-y-1.5"><label className="text-sm font-medium">{t.assignee}</label><WorkItemAssigneeSelect value={form.assigneeId} candidates={assigneeCandidates} loading={assigneesQuery.isLoading} disabled={!form.projectId} onValueChange={(value) => { setAssigneeNotice(""); update("assigneeId", value); }} placeholder={form.projectId ? t.selectAssignee : t.selectProjectFirst} searchPlaceholder={t.searchAssignees} emptyText={t.noEligibleProjectPeople} clearLabel={t.unassign} /></div><div className="space-y-1.5"><label htmlFor="work-item-target-date" className="text-sm font-medium">{isIssue ? t.targetResolutionDate : t.deadline}</label><Input id="work-item-target-date" type="date" value={isIssue ? form.targetResolutionDate : form.dueDate} onChange={(event) => update(isIssue ? "targetResolutionDate" : "dueDate", event.target.value)} /></div></div>{isIssue ? <SelectField id="work-item-severity" label={t.severity} value={form.severity} onChange={(value) => update("severity", value)} options={[["Low", t.low], ["Medium", t.medium], ["High", t.high], ["Critical", t.criticalPriority]]} /> : null}<WorkItemAttachments canManage t={t} files={files} onFilesChange={setFiles} /><div className="space-y-1.5"><label htmlFor="work-item-description" className="text-sm font-medium">{t.descriptionField}</label><RichTextEditor id="work-item-description" value={form.description} onChange={(value) => update("description", value)} placeholder={t.descriptionField} ariaLabel={t.descriptionField} t={t} /></div></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => close(false)}>{t.cancel}</Button><Button type="submit" disabled={create.isPending}>{create.isPending ? t.loading : t.create}</Button></SideSheetFooter></form></SideSheet>;
}

export function CreateButton({ type, canCreate, onCreate, t, variant = "default", className }: { type: CreateWorkItemType; canCreate: boolean; onCreate: (_type: CreateWorkItemType) => void; t: Copy; variant?: "default" | "outline"; className?: string }) {
  const label = type === "Task" ? t.newTask : t.newIssue;
  const button = <Button size="sm" variant={variant} className={className} disabled={!canCreate} onClick={canCreate ? () => onCreate(type) : undefined}><Plus className="me-2 h-4 w-4" />{label}</Button>;

  if (canCreate) return button;

  return <Tooltip><TooltipTrigger asChild><span tabIndex={0} aria-label={`${label}. ${t.createPermissionHint}`} className="inline-flex cursor-not-allowed">{button}</span></TooltipTrigger><TooltipContent>{t.createPermissionHint}</TooltipContent></Tooltip>;
}

function CreateActions({ scope, canCreate, onCreate, t }: { scope: WorkItemsScope; canCreate: boolean; onCreate: (_type: CreateWorkItemType) => void; t: Copy }) {
  const taskButton = <CreateButton type="Task" canCreate={canCreate} onCreate={onCreate} t={t} />;
  const issueButton = <CreateButton type="Issue" canCreate={canCreate} onCreate={onCreate} t={t} variant="outline" className="border-info/40 bg-info/5 text-info hover:bg-info/10 hover:text-info" />;
  if (scope === "task") return taskButton;
  if (scope === "issue") return issueButton;
  return <div className="flex flex-wrap items-center gap-2">{taskButton}{issueButton}</div>;
}

function Row({ item, t, locale, statuses }: { item: WorkItemDto; t: Copy; locale: string; statuses: readonly WorkItemStatusReference[] }) {
  const router = useRouter();
  const status = statuses.find((candidate) => candidate.code === item.status);
  const statusTone = getWorkItemStatusColorClasses(status?.color);
  const Icon = status?.color === "emerald" ? CheckCircle2 : item.type === "Issue" ? Ticket : Wrench;
  return <button type="button" onClick={() => router.push(`/${locale}/work-items/${encodeURIComponent(item.id)}`)} className="nexus-table-row grid w-full grid-cols-1 gap-2 px-4 py-3 text-start text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring sm:grid-cols-12 sm:items-center"><span className="flex min-w-0 items-center gap-2 sm:col-span-4"><Icon className={`h-4 w-4 shrink-0 ${item.type === "Issue" ? "text-blue-600" : "text-muted-foreground"}`} /><span className="min-w-0 truncate font-medium">{item.title}</span><span className="shrink-0 rounded-full border px-1.5 py-0.5 text-[10px] text-muted-foreground">{typeLabel(item.type, t)}</span></span><span className="truncate text-muted-foreground sm:col-span-2">{item.projectName || t.noProject}</span><span className={`sm:col-span-2 ${priorityClass(item.priority)}`}>{priorityLabel(item.priority, t)}</span><span className="text-muted-foreground sm:col-span-2">{item.dueDate ? formatDate(item.dueDate, locale === "ar" ? "ar-EG" : "en-GB") : "—"}</span><span className={`flex items-center gap-1.5 sm:col-span-2 ${statusClass(item.status, statuses)}`}><span className={`h-2 w-2 shrink-0 rounded-full ${statusTone.dot}`} />{statusLabel(item.status, t, statuses, locale)}</span></button>;
}

type WorkItemsView = "list" | "kanban";
const VIEW_STORAGE_KEY = "nexus_work_items_view";

function ViewSwitcher({ view, onChange, t }: { view: WorkItemsView; onChange: (_view: WorkItemsView) => void; t: Copy }) {
  return <div className="flex shrink-0 items-center rounded-lg border border-border/70 bg-muted/30 p-1" role="tablist" aria-label={t.viewMode}>
    <button type="button" role="tab" aria-selected={view === "list"} onClick={() => onChange("list")} className={`flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium transition-colors ${view === "list" ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"}`}><List className="h-3.5 w-3.5" />{t.listView}</button>
    <button type="button" role="tab" aria-selected={view === "kanban"} onClick={() => onChange("kanban")} className={`flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium transition-colors ${view === "kanban" ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"}`}><Columns3 className="h-3.5 w-3.5" />{t.kanbanView}</button>
  </div>;
}

function KanbanBoard({ items, statuses, locale, t, emptyLabel, canEdit, movingItemId, moveError, onMove }: { items: readonly WorkItemDto[]; statuses: readonly WorkItemStatusReference[]; locale: string; t: Copy; emptyLabel: string; canEdit: boolean; movingItemId: string | null; moveError: string; onMove: (_itemId: string, _status: WorkItemStatus) => void }) {
  const router = useRouter();
  const [draggedItemId, setDraggedItemId] = useState<string | null>(null);
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  return <div className="space-y-3 p-3 sm:p-4">
    {moveError ? <p role="alert" className="rounded-lg border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{moveError}</p> : null}
    {canEdit ? <p className="flex items-center gap-1.5 text-xs text-muted-foreground"><GripVertical className="h-3.5 w-3.5" />{t.dragToMove}</p> : null}
    <div className="overflow-x-auto pb-2">
      <div className="flex min-w-max items-stretch gap-3">
        {statuses.map((status) => {
          const tone = getWorkItemStatusColorClasses(status.color);
          const columnItems = items.filter((item) => item.status === status.code);
          return <section key={status.id} className={`flex min-h-[28rem] w-[18rem] flex-col rounded-xl border sm:w-[20rem] ${tone.panel}`} onDragOver={(event) => { if (canEdit && status.isActive) { event.preventDefault(); event.dataTransfer.dropEffect = "move"; } }} onDrop={(event) => { if (!canEdit || !status.isActive) return; event.preventDefault(); const itemId = event.dataTransfer.getData("text/plain") || draggedItemId; if (itemId) onMove(itemId, status.code); setDraggedItemId(null); }}>
            <header className="flex items-center justify-between gap-2 border-b border-inherit px-3 py-3"><h2 className="flex min-w-0 items-center gap-2 text-sm font-semibold"><span className={`h-2.5 w-2.5 shrink-0 rounded-full ${tone.dot}`} />{statusLabel(status.code, t, statuses, locale)}</h2><span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${tone.badge}`}>{columnItems.length}</span></header>
            <div className="flex flex-1 flex-col gap-3 p-3">
              {columnItems.length > 0 ? columnItems.map((item) => { const deadline = item.type === "Issue" ? item.targetResolutionDate : item.dueDate; const isMoving = movingItemId === item.id; return <button key={item.id} type="button" draggable={canEdit && !isMoving} aria-busy={isMoving} aria-label={`${item.title}. ${t.viewDetails}`} onDragStart={(event) => { setDraggedItemId(item.id); event.dataTransfer.effectAllowed = "move"; event.dataTransfer.setData("text/plain", item.id); }} onDragEnd={() => setDraggedItemId(null)} onClick={() => router.push(`/${locale}/work-items/${encodeURIComponent(item.id)}`)} className={`group w-full rounded-lg border border-border/70 bg-background p-3 text-start shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${isMoving ? "opacity-60" : ""} ${canEdit ? "cursor-grab active:cursor-grabbing" : ""}`}>
                <div className="flex items-start justify-between gap-2"><span className={`rounded-md border px-1.5 py-0.5 text-[10px] font-semibold ${item.type === "Issue" ? "border-info/30 bg-info/10 text-info" : "border-primary/25 bg-primary/10 text-primary"}`}>{typeLabel(item.type, t)}</span>{canEdit ? <GripVertical className="h-4 w-4 shrink-0 text-muted-foreground/50 transition-colors group-hover:text-muted-foreground" /> : null}</div>
                <h3 className="mt-2 line-clamp-2 text-sm font-semibold leading-5 text-foreground">{item.title}</h3>
                <div className="mt-3 flex flex-wrap gap-1.5 text-[11px]"><span className={`rounded-md bg-muted/70 px-2 py-1 font-medium ${priorityClass(item.priority)}`}>{priorityLabel(item.priority, t)}</span>{item.projectName ? <span className="max-w-[11rem] truncate rounded-md bg-muted/70 px-2 py-1 text-muted-foreground" title={item.projectName}>{item.projectName}</span> : null}</div>
                <div className="mt-3 flex flex-col gap-1.5 border-t border-border/60 pt-2.5 text-[11px] text-muted-foreground">{item.assigneeId ? <span className="flex min-w-0 items-center gap-1.5"><UserRound className="h-3.5 w-3.5 shrink-0" /><span className="truncate">{item.assigneeName ?? t.unassigned}</span></span> : null}{deadline ? <span className="flex items-center gap-1.5"><CalendarDays className="h-3.5 w-3.5 shrink-0" /><span>{formatDate(deadline, dateLocale)}</span></span> : null}</div>
                {isMoving ? <span className="mt-2 block text-[11px] font-medium text-primary">{t.moving}</span> : null}
              </button>; }) : <div className="flex flex-1 items-center justify-center rounded-lg border border-dashed border-border/70 bg-background/35 p-4 text-center text-xs text-muted-foreground">{emptyLabel}</div>}
            </div>
          </section>;
        })}
      </div>
    </div>
  </div>;
}

export function WorkItemsWorkspace({ scope = "all" }: { scope?: WorkItemsScope } = {}) {
  const locale = useLocale();
  const searchParams = useSearchParams();
  const t = locale === "ar" ? labels.ar : labels.en;
  const { user } = useAuth();
  const workflowTransition = useWorkItemWorkflowTransition();
  const { effectiveReadOnly } = useManagementPerspective();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create));
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit) && !effectiveReadOnly);
  const statusesQuery = useWorkItemStatuses(true);
  const allStatuses = statusesQuery.data ?? [];
  const activeStatuses = allStatuses.filter((status) => status.isActive).sort((left, right) => left.displayOrder - right.displayOrder);
  const defaultType: CreateWorkItemType = scope === "issue" ? "Issue" : "Task";
  const [filter, setFilter] = useState<WorkItemsScope>(scope);
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState(false);
  const [createType, setCreateType] = useState<CreateWorkItemType>(defaultType);
  const [view, setView] = useState<WorkItemsView>("list");
  const [movingItemId, setMovingItemId] = useState<string | null>(null);
  const [moveError, setMoveError] = useState("");
  const [pendingMove, setPendingMove] = useState<{ itemId: string; status: WorkItemStatus; options: WorkflowTransitionOption[] } | null>(null);
  useEffect(() => {
    const savedView = window.localStorage.getItem(VIEW_STORAGE_KEY);
    if (savedView === "list" || savedView === "kanban") setView(savedView);
  }, []);
  const changeView = (nextView: WorkItemsView) => {
    setView(nextView);
    window.localStorage.setItem(VIEW_STORAGE_KEY, nextView);
  };
  const projectId = searchParams.get("projectId") || undefined;
  const params = useMemo(() => ({ page: 1, pageSize: 100, projectId, type: filter === "all" ? undefined : scopeTypes[filter], search: search.trim() || undefined }), [filter, projectId, search]);
  const query = useWorkItems(params);
  const items = query.data?.items ?? [];
  const copy = scope === "task"
    ? { title: t.tasksTitle, description: t.tasksDescription, total: t.totalTasks, empty: t.emptyTasks, emptyHint: t.emptyTasksHint }
    : scope === "issue"
      ? { title: t.issuesTitle, description: t.issuesDescription, total: t.totalIssues, empty: t.emptyIssues, emptyHint: t.emptyIssuesHint }
      : { title: t.title, description: t.description, total: t.totalWorkItems, empty: t.emptyWorkItems, emptyHint: t.emptyWorkItemsHint };
  const doneCode = allStatuses.find((status) => status.code.toLowerCase() === "done")?.code;
  const openCreate = (type: CreateWorkItemType) => { setCreateType(type); setOpen(true); };
  const executeMove = async (itemId: string, status: WorkItemStatus, transitionCode?: string) => {
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item) return;
    setMoveError("");
    setMovingItemId(itemId);
    try {
      await workflowTransition.mutateAsync({ itemId: item.id, targetStateCode: status, transitionCode });
      setPendingMove(null);
    } catch {
      setMoveError(t.moveError);
    } finally {
      setMovingItemId(null);
    }
  };

  const moveItem = async (itemId: string, status: WorkItemStatus) => {
    const item = items.find((candidate) => candidate.id === itemId);
     if (!item || !canEdit || item.status === status || movingItemId || !activeStatuses.some((candidate) => candidate.code === status)) return;
    setMoveError("");
    try {
      const available = await workItemsApi.workflowTransitions(item.id);
      const candidates = available.transitions.filter((candidate) => candidate.targetStateCode === status && candidate.canExecute);
      if (candidates.length === 0) throw new Error(available.transitions.find((candidate) => candidate.targetStateCode === status)?.denialReason || t.moveError);
      if (candidates.length > 1) {
        setPendingMove({ itemId, status, options: candidates });
        return;
      }
      await executeMove(itemId, status, candidates[0].code);
    } catch {
      setMoveError(t.moveError);
    }
  };

  const boardStatuses = [...activeStatuses, ...allStatuses.filter((status) => !status.isActive && items.some((item) => item.status === status.code))];
  return <div className="space-y-6"><PageHeader title={copy.title} description={`${copy.description} · ${query.data?.totalCount ?? 0} ${copy.total}`} actions={<CreateActions scope={scope} canCreate={canCreate} onCreate={openCreate} t={t} />} /><div className="grid gap-4 md:grid-cols-4"><KpiCard label={t.all} value={query.data?.totalCount ?? 0} icon={Ticket} semantic="primary" /><KpiCard label={t.open} value={items.filter((item) => item.status !== doneCode).length} icon={Clock3} semantic="amber" accent="warning" /><KpiCard label={t.done} value={items.filter((item) => item.status === doneCode).length} icon={CheckCircle2} semantic="green" accent="success" /><KpiCard label={t.critical} value={items.filter((item) => item.priority === "Critical").length} icon={AlertCircle} semantic="red" accent="danger" /></div><div className="flex flex-col gap-2 sm:flex-row sm:items-center"><div className="relative flex-1"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input aria-label={t.search} placeholder={t.search} className="ps-8" value={search} onChange={(event) => setSearch(event.target.value)} /></div><div className="flex flex-wrap items-center gap-2"><Button variant="outline" size="sm" type="button"><Filter className="me-2 h-4 w-4" />{t.filter}</Button>{scope === "all" ? <ViewSwitcher view={view} onChange={changeView} t={t} /> : null}</div></div><div className="flex flex-wrap gap-1 rounded-lg border bg-muted/20 p-1" role="tablist" aria-label={t.type}>{(["all", "task", "issue"] as WorkItemsScope[]).map((value) => <button key={value} type="button" role="tab" aria-selected={filter === value} onClick={() => setFilter(value)} className={`rounded-md px-3 py-1.5 text-sm ${filter === value ? "bg-background font-medium shadow-sm" : "text-muted-foreground hover:text-foreground"}`}>{value === "all" ? t.all : value === "task" ? t.task : t.issue}</button>)}</div><Card><CardContent className="p-0">{query.isLoading ? <div className="space-y-3 p-5" aria-label={t.loading}><Skeleton className="h-8" /><Skeleton className="h-12" /><Skeleton className="h-12" /></div> : query.isError ? <ErrorState title={t.error} onRetry={() => void query.refetch()} /> : items.length === 0 ? <EmptyState title={copy.empty} description={copy.emptyHint} icon={Ticket} action={<CreateActions scope={scope} canCreate={canCreate} onCreate={openCreate} t={t} />} /> : view === "kanban" && scope === "all" ? <KanbanBoard items={items} statuses={boardStatuses} locale={locale} t={t} emptyLabel={copy.empty} canEdit={canEdit} movingItemId={movingItemId} moveError={moveError} onMove={moveItem} /> : <div className="overflow-x-auto"><div className="nexus-table-header hidden min-w-[720px] grid-cols-12 gap-2 sm:grid"><div className="col-span-4">{t.titleField}</div><div className="col-span-2">{t.project}</div><div className="col-span-2">{t.priority}</div><div className="col-span-2">{t.due}</div><div className="col-span-2">{t.status}</div></div><div className="min-w-[720px]">{items.map((item) => <Row key={item.id} item={item} t={t} locale={locale} statuses={allStatuses} />)}</div></div>}</CardContent></Card><CreateSheet key={createType} open={open} onOpenChange={setOpen} t={t} locale={locale} defaultType={createType} statuses={activeStatuses} /><SideSheet open={Boolean(pendingMove)} onOpenChange={(open) => { if (!open) setPendingMove(null); }} title={t.chooseTransition ?? "Choose transition"} description={t.multipleTransitions ?? "More than one transition is available for this target state."} size="md"><SideSheetBody className="space-y-2">{(pendingMove?.options ?? []).map((option) => <Button key={option.code} type="button" variant="outline" className="w-full justify-between" disabled={Boolean(movingItemId)} onClick={() => { if (pendingMove) void executeMove(pendingMove.itemId, pendingMove.status, option.code); }}><span>{locale === "ar" ? option.nameAr : option.nameEn}</span><span className="font-mono text-xs text-muted-foreground">{option.code}</span></Button>)}</SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setPendingMove(null)}>{t.cancel}</Button></SideSheetFooter></SideSheet></div>;
}
