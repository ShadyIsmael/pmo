import * as React from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { AlertCircle, ArrowLeft, CalendarDays, ChevronDown, ChevronRight, Clock3, Download, Eye, EyeOff, FileImage, FileText, Filter, History, MessageSquare, MoreHorizontal, Paperclip, Plus, Reply, Save, Search, Send, SortAsc, Tags, Ticket, Trash2, Upload, UserRound, Wrench } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@components/ui/Collapsible";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@components/ui/DropdownMenu";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Badge } from "@components/ui/Badge";
import { Pagination } from "@components/ui/Pagination";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { RichTextContent, RichTextEditor } from "@components/ui/rich-text-editor";
import { useI18n } from "@i18n/messages";
import { formatFileSize } from "@lib/formatting/file-size";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import {
  useFullWorkItem, useFullWorkItems, useFullWorkItemActivity, useFullWorkItemAssignees, useFullWorkItemAttachments,
  useFullWorkItemComments, useFullWorkItemCommentMutation, useFullWorkItemDeleteAttachment, useFullWorkItemInsights, useFullWorkItemMutations, useFullWorkItemUploadMutation, useFullWorkItemWorkflow, workItemsApiFull,
  type SaveWorkItemDto, type WorkItemAttachmentDto, type WorkItemDto, type WorkItemPriority, type WorkflowTransitionOption,
} from "@api/workItemsFull";
import { WorkflowMoveError, useWorkItemWorkflowTransition, workflowMoveErrorMessage } from "@api/workItemWorkflow";
import { useWorkItemStatuses, type WorkItemStatusDto } from "@api/workItems";
import { useProjects } from "@features/projects/api/projects";
import { useWorkItemWorkflowUserLookup } from "@features/projects/lib/project-lookups";
import { buildWorkItemListQuery, emptyWorkItemListFilters, readWorkItemListLocation, type WorkItemListFilters } from "./workItemFilters";
import { isAllVisibleWorkItemsSelected, toggleAllVisibleWorkItemSelection, toggleWorkItemSelection } from "./workItemSelection";
import { WorkItemsKanban } from "./WorkItemsKanban";
import { WorkItemForm } from "./WorkItemForm";
import { WorkItemDetailsSideSheetFull } from "./WorkItemDetailsSideSheetFull";
import { WorkItemStatusTrack } from "./WorkItemStatusTrack";

const priorities: WorkItemPriority[] = ["Critical", "High", "Medium", "Low", "Backlog"];
const emptyWorkItemStatuses: WorkItemStatusDto[] = [];

function ErrorState({ message }: { message: string }) { return <p role="alert" className="py-8 text-center text-sm text-destructive">{message}</p>; }
function LoadingState({ label }: { label: string }) { return <p className="py-8 text-center text-sm text-muted-foreground">{label}</p>; }

function CompactWorkItemKpi({ label, value, icon: Icon, color }: { label: string; value: number | string; icon: React.ElementType; color: string }) {
  return <div className="flex items-center gap-3 rounded-lg border border-border/60 bg-card px-3 py-2.5">
    <span className={`inline-flex h-8 w-8 items-center justify-center rounded-md bg-muted/40 ${color}`}><Icon className="h-4 w-4" /></span>
    <div className="min-w-0"><p className="text-lg font-bold leading-none tabular-nums">{value}</p><p className="mt-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p></div>
  </div>;
}

const statusColorHex: Record<string, string> = { slate: "#64748b", blue: "#3b82f6", amber: "#f59e0b", violet: "#8b5cf6", emerald: "#10b981", rose: "#f43f5e", orange: "#f97316" };
function getStatusColorHex(color?: string) { return statusColorHex[color?.toLowerCase() ?? "slate"] ?? "#64748b"; }

const priorityBarClasses: Record<string, string> = { Critical: "bg-destructive", High: "bg-warning", Medium: "bg-info", Low: "bg-muted-foreground/40", Backlog: "bg-muted-foreground/20" };
const priorityTextClasses: Record<string, string> = { Critical: "text-destructive font-semibold", High: "text-warning", Medium: "text-info", Low: "text-muted-foreground", Backlog: "text-muted-foreground" };
const priorityDotClasses: Record<string, string> = { Critical: "bg-destructive", High: "bg-warning", Medium: "bg-info", Low: "bg-muted-foreground", Backlog: "bg-muted-foreground/50" };
const workItemTypeClasses: Record<string, string> = { Task: "border-primary/25 bg-primary/10 text-primary", Issue: "border-info/30 bg-info/10 text-info", Subtask: "border-muted-foreground/25 bg-muted text-muted-foreground", Bug: "border-destructive/25 bg-destructive/10 text-destructive", Feature: "border-success/25 bg-success/10 text-success", Epic: "border-fin-violet/25 bg-fin-violet/10 text-fin-violet", Story: "border-warning/25 bg-warning/10 text-warning" };

function formatAttachmentSize(size: number, locale: string): string { return formatFileSize(size, locale); }

function AttachmentIcon({ fileName, contentType }: { fileName: string; contentType?: string }) {
  const isImage = contentType?.startsWith("image/") || /\.(png|jpe?g|gif|svg|webp)$/i.test(fileName);
  const Icon = isImage ? FileImage : FileText;
  return <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary"><Icon className="h-4 w-4" /></span>;
}

function WorkItemAttachmentsPanel({ workItemId, canManage }: { workItemId: string; canManage: boolean }) {
  const { t, locale } = useI18n();
  const fileInput = React.useRef<HTMLInputElement>(null);
  const [queuedFiles, setQueuedFiles] = React.useState<File[]>([]);
  const [error, setError] = React.useState("");
  const attachments = useFullWorkItemAttachments(workItemId);
  const uploadMutation = useFullWorkItemUploadMutation(workItemId);
  const deleteMutation = useFullWorkItemDeleteAttachment(workItemId);

  function queueFiles(files: FileList | null) {
    if (!files?.length) return;
    setQueuedFiles((current) => [...current, ...Array.from(files)]);
    setError("");
    if (fileInput.current) fileInput.current.value = "";
  }

  async function uploadQueuedFiles() {
    if (!queuedFiles.length) return;
    setError("");
    try {
      await uploadMutation.mutateAsync(queuedFiles);
      setQueuedFiles([]);
    } catch {
      setError(t("workItems.attachmentUploadError"));
    }
  }

  async function downloadAttachment(attachment: WorkItemAttachmentDto) {
    setError("");
    try {
      const blob = await workItemsApiFull.downloadAttachment(workItemId, attachment.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = attachment.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch {
      setError(t("workItems.attachmentDownloadError"));
    }
  }

  async function removeAttachment(attachmentId: string) {
    setError("");
    try {
      await deleteMutation.mutateAsync(attachmentId);
    } catch {
      setError(t("workItems.attachmentRemoveError"));
    }
  }

  const attachmentRows = attachments.data ?? [];
  return <Card><CardHeader><div className="flex flex-wrap items-center justify-between gap-3"><CardTitle className="flex items-center gap-2"><Paperclip className="h-4 w-4" />{t("workItems.attachments")}</CardTitle>{canManage ? <><input ref={fileInput} className="sr-only" type="file" multiple onChange={(event) => queueFiles(event.target.files)} /><Button type="button" size="sm" variant="outline" onClick={() => fileInput.current?.click()}><Upload className="me-2 h-4 w-4" />{t("workItems.chooseFiles")}</Button></> : null}</div></CardHeader><CardContent className="space-y-3">
    {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
    {queuedFiles.length ? <div className="space-y-2 rounded-md border border-dashed border-primary/40 bg-primary/[0.03] p-3"><div className="flex items-center justify-between"><p className="text-xs font-semibold text-primary">{t("workItems.pendingAttachments")}</p><span className="text-xs text-muted-foreground">{queuedFiles.length}</span></div>{queuedFiles.map((file, index) => <div key={`${file.name}-${file.size}-${index}`} className="flex min-w-0 items-center gap-3 rounded-md bg-background p-2"><AttachmentIcon fileName={file.name} contentType={file.type} /><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={file.name}>{file.name}</p><p className="mt-1 text-xs text-muted-foreground">{formatAttachmentSize(file.size, locale)} · {uploadMutation.isPending ? t("workItems.uploading") : t("workItems.pendingUpload")}</p></div><Button type="button" size="sm" variant="ghost" disabled={uploadMutation.isPending} aria-label={`${t("workItems.remove")}: ${file.name}`} onClick={() => setQueuedFiles((current) => current.filter((_, fileIndex) => fileIndex !== index))}><Trash2 className="h-4 w-4" /></Button></div>)}<div className="flex justify-end"><Button type="button" size="sm" onClick={() => void uploadQueuedFiles()} disabled={uploadMutation.isPending}><Upload className="me-2 h-4 w-4" />{uploadMutation.isPending ? t("workItems.uploading") : t("workItems.upload")}</Button></div></div> : null}
    {attachments.isLoading ? <LoadingState label={t("common.loading")} /> : attachments.isError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{t("workItems.attachmentsLoadError")}</p> : attachmentRows.length ? <div className="space-y-2">{attachmentRows.map((file) => <div key={file.id} className="flex min-w-0 items-center gap-3 rounded-md border p-3"><AttachmentIcon fileName={file.fileName} contentType={file.contentType} /><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium" title={file.fileName}>{file.fileName}</p><p className="mt-1 text-xs text-muted-foreground">{formatAttachmentSize(file.fileSize, locale)} · {t("workItems.attached")}</p></div><Button type="button" size="sm" variant="ghost" onClick={() => void downloadAttachment(file)} aria-label={`${t("workItems.download")}: ${file.fileName}`}><Download className="me-1 h-3.5 w-3.5" />{t("workItems.download")}</Button>{canManage ? <Button type="button" size="sm" variant="ghost" onClick={() => void removeAttachment(file.id)} disabled={deleteMutation.isPending} aria-label={`${t("workItems.remove")}: ${file.fileName}`}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button> : null}</div>)}</div> : <p className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("workItems.noAttachments")}</p>}
  </CardContent></Card>;
}

function emptyDraft(): SaveWorkItemDto { return { title: "", description: "", type: "Task", priority: "Medium", dueDate: "" }; }

export function WorkItemsPageFull() {
  const { t, locale, formatDate } = useI18n();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const { search, filters, quickView, groupBy, sortBy, view, page } = readWorkItemListLocation(searchParams);
  const activeTab = view === "kanban" ? "board" : searchParams.get("tab") === "my-work" ? "my-work" : "backlog";
  const [filterDraft, setFilterDraft] = React.useState<WorkItemListFilters>(emptyWorkItemListFilters);
  const [filtersOpen, setFiltersOpen] = React.useState(false);
  const [insightsOpen, setInsightsOpen] = React.useState(true);
  const [collapsedGroups, setCollapsedGroups] = React.useState<Set<string>>(new Set());
  const [selectedIds, setSelectedIds] = React.useState<Set<string>>(new Set());
  const [detailItemId, setDetailItemId] = React.useState<string>();
  const [createError, setCreateError] = React.useState("");
  const pageSize = 100;
  const statusesQuery = useWorkItemStatuses(true);
  const canLoadAssigneeLookup = Boolean(user && (
    hasPermission(user.permissions, Permissions.WorkItem.View)
    || hasPermission(user.permissions, Permissions.WorkItem.Create)
    || hasPermission(user.permissions, Permissions.WorkItem.Edit)
  ));
  const usersQuery = useWorkItemWorkflowUserLookup(canLoadAssigneeLookup);
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, true);
  const listQuery = buildWorkItemListQuery(filters, search, page, pageSize, quickView, sortBy);
  const query = useFullWorkItems({ ...listQuery, myWork: activeTab === "my-work" || listQuery.myWork ? true : undefined });
  const insightsParams = {
    projectId: filters.projectId || undefined,
    search: search.trim() || undefined,
    type: filters.type === "all" ? undefined : filters.type,
    status: filters.status === "all" ? undefined : filters.status,
    priority: filters.priority === "all" ? undefined : filters.priority,
    assigneeId: ["all", "me", "unassigned"].includes(filters.assignee) ? undefined : filters.assignee,
    unassigned: filters.assignee === "unassigned" || quickView === "unassigned" ? true : undefined,
    dueDateFrom: filters.dueFrom || undefined,
    dueDateTo: filters.dueTo || undefined,
    overdue: quickView === "overdue" ? true : undefined,
    highPriority: quickView === "high-priority" ? true : undefined,
  };
  const insightsQuery = useFullWorkItemInsights(insightsParams, activeTab === "backlog");
  const mutations = useFullWorkItemMutations();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create) && canMutate && !effectiveReadOnly);
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit) && canMutate && !effectiveReadOnly);
  const [createOpen, setCreateOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto>(emptyDraft());
  const formProjects = useProjects({ page: 1, pageSize: 200 }, createOpen);
  const [movingItemId, setMovingItemId] = React.useState<string>();
  const [moveError, setMoveError] = React.useState("");
  const [pendingTransition, setPendingTransition] = React.useState<{ itemId: string; targetStatus: string; options: WorkflowTransitionOption[] }>();
  const transitionMove = useWorkItemWorkflowTransition();
  const createAssignees = useFullWorkItemAssignees(draft.projectId || undefined, createOpen);
  const items = React.useMemo(() => query.data?.items ?? [], [query.data?.items]);
  const statuses = statusesQuery.data ?? emptyWorkItemStatuses;
  const activeStatuses = React.useMemo(() => statuses.filter((status) => status.isActive).sort((a, b) => a.displayOrder - b.displayOrder), [statuses]);
  const boardStatuses = React.useMemo(() => [...activeStatuses, ...statuses.filter((status) => !status.isActive && items.some((item) => item.status === status.code))], [activeStatuses, items, statuses]);
  const activeFilterCount = [filters.type !== "all", filters.status !== "all", filters.priority !== "all", filters.assignee !== "all", Boolean(filters.projectId), Boolean(filters.dueFrom), Boolean(filters.dueTo), Boolean(search.trim())].filter(Boolean).length;
  const quickViews = [
    ["all", t("workItems.quickAll")], ["my-items", t("workItems.quickMyItems")], ["unassigned", t("workItems.quickUnassigned")],
    ["overdue", t("workItems.quickOverdue")], ["high-priority", t("workItems.quickHighPriority")], ["recently-updated", t("workItems.quickRecentlyUpdated")],
  ] as const;
  const groupedItems = React.useMemo(() => {
    const groups = new Map<string, WorkItemDto[]>();
    items.forEach((item) => {
      const key = groupBy === "status" ? item.status
        : groupBy === "assignee" ? item.assignedResourceName || item.assignedTeamName || "unassigned"
          : groupBy === "priority" ? item.priority
            : groupBy === "project" ? item.projectName || item.projectId || "no-project"
              : groupBy === "type" ? item.type : "";
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(item);
    });
    return groups;
  }, [groupBy, items]);

  const replaceQuery = React.useCallback((updates: Record<string, string | undefined>) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (Object.keys(updates).some((key) => key !== "page" && key !== "view")) next.delete("page");
      Object.entries(updates).forEach(([key, value]) => {
        if (value === undefined || value === "" || value === "all" || (key === "group" && value === "none") || (key === "sort" && value === "createdAt") || (key === "quick" && value === "all")) next.delete(key);
        else next.set(key, value);
      });
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  function changeTab(tab: "backlog" | "board" | "my-work") {
    if (tab === "board") replaceQuery({ view: "kanban", tab: undefined });
    else if (tab === "my-work") replaceQuery({ view: undefined, tab: "my-work" });
    else replaceQuery({ view: undefined, tab: undefined });
  }
  function changeProject(projectId: string) { replaceQuery({ projectId: projectId || undefined }); }

  function updateDraft(patch: Partial<SaveWorkItemDto>) { setDraft((current) => ({ ...current, ...patch })); }
  function closeCreate(open: boolean) {
    setCreateOpen(open);
    if (!open) { setDraft(emptyDraft()); setCreateError(""); }
  }
  async function save() {
    if (!draft.title.trim()) { setCreateError(t("workItems.validation")); return; }
    setCreateError("");
    try {
      const item = await mutations.create.mutateAsync({ ...draft, title: draft.title.trim(), dueDate: draft.dueDate || undefined });
      closeCreate(false);
      navigate(`/work-items/${encodeURIComponent(item.id)}`);
    } catch {
      setCreateError(t("workItems.createError"));
    }
  }
  function openFilters() { setFilterDraft(filters); setFiltersOpen(true); }
  function applyFilters() { replaceQuery({ type: filterDraft.type, status: filterDraft.status, priority: filterDraft.priority, assignee: filterDraft.assignee, projectId: filterDraft.projectId, dueFrom: filterDraft.dueFrom, dueTo: filterDraft.dueTo }); setFiltersOpen(false); }
  function clearAllFilters() { replaceQuery({ search: undefined, type: "all", status: "all", priority: "all", assignee: "all", projectId: undefined, dueFrom: undefined, dueTo: undefined, quick: "all", group: "none", sort: "createdAt" }); }
  async function executeTransition(itemId: string, targetStatus: string, option: WorkflowTransitionOption) {
    setMovingItemId(itemId);
    setMoveError("");
    try {
      await transitionMove.mutateAsync({ itemId, targetStateCode: targetStatus, transitionCode: option.code });
      setPendingTransition(undefined);
    } catch (cause) {
      setMoveError(workflowMoveErrorMessage(cause, t("workItems.moveError")));
    } finally {
      setMovingItemId(undefined);
    }
  }
  async function moveItem(itemId: string, targetStatus: string) {
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item || !canEdit || movingItemId || item.status === targetStatus || !activeStatuses.some((status) => status.code === targetStatus)) return;
    setMoveError("");
    setMovingItemId(itemId);
    try {
      await transitionMove.mutateAsync({ itemId, targetStateCode: targetStatus });
      setPendingTransition(undefined);
    } catch (cause) {
      if (cause instanceof WorkflowMoveError && cause.failure.kind === "multiple") {
        setPendingTransition({ itemId, targetStatus, options: cause.failure.options });
        return;
      }
      setMoveError(workflowMoveErrorMessage(cause, t("workItems.moveError")));
    } finally {
      setMovingItemId(undefined);
    }
  }
  function groupLabel(key: string) {
    if (groupBy === "status") return statusesQuery.data?.find((status) => status.code === key)?.[locale === "ar" ? "nameAr" : "nameEn"] ?? key;
    if (groupBy === "priority") return t(`workItems.priority${key}`);
    if (groupBy === "type") return key === "Task" || key === "Issue" ? t(`workItems.${key.toLowerCase()}`) : key;
    if (groupBy === "project" && key === "no-project") return t("workItems.noProject");
    if (groupBy === "assignee" && key === "unassigned") return t("workItems.unassigned");
    return key;
  }
  function toggleGroup(key: string) { setCollapsedGroups((current) => { const next = new Set(current); if (next.has(key)) next.delete(key); else next.add(key); return next; }); }
  function toggleSelectAll() { setSelectedIds((current) => toggleAllVisibleWorkItemSelection(current, items.map((item) => item.id))); }
  function toggleSelect(itemId: string) { setSelectedIds((current) => toggleWorkItemSelection(current, itemId)); }
  function renderItemRow(item: WorkItemDto) {
    const status = statuses.find((candidate) => candidate.code === item.status);
    const deadline = item.type === "Issue" ? item.targetResolutionDate ?? item.dueDate : item.dueDate;
    const assignee = item.assignedTeamName
      ? `${t("workItems.teamTarget")} · ${item.assignedTeamName}`
      : item.assignedResourceName ? `${t("workItems.resourceTarget")} · ${item.assignedResourceName}` : t("workItems.unassigned");
    const type = item.type === "Task" || item.type === "Issue" ? t(`workItems.${item.type.toLowerCase()}`) : item.type;
    const isOverdue = Boolean(deadline && new Date(deadline) < new Date() && !status?.isTerminal);
    const TypeIcon = item.type === "Issue" || item.type === "Bug" ? Ticket : Wrench;
    return <TableRow key={item.id} className="cursor-pointer" onClick={() => setDetailItemId(item.id)}>
      <TableCell onClick={(event) => event.stopPropagation()}><input type="checkbox" checked={selectedIds.has(item.id)} onChange={() => toggleSelect(item.id)} aria-label={t("workItems.selectItem", { title: item.title })} className="h-4 w-4 rounded border-input" /></TableCell>
      <TableCell><span className={`inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-[10px] font-semibold ${workItemTypeClasses[item.type] ?? workItemTypeClasses.Task}`}><TypeIcon className="h-3 w-3" />{type}</span></TableCell>
      <TableCell className="font-mono text-xs text-muted-foreground">{item.id.slice(0, 8)}</TableCell>
      <TableCell className="max-w-[24rem]"><button type="button" onClick={() => setDetailItemId(item.id)} className="block max-w-full truncate text-start font-semibold hover:text-primary hover:underline">{item.title}</button></TableCell>
      <TableCell><span className="inline-flex items-center gap-1.5 text-xs" style={{ color: getStatusColorHex(status?.color) }}><span aria-hidden="true" className="h-1.5 w-1.5 shrink-0 rounded-full" style={{ backgroundColor: getStatusColorHex(status?.color) }} />{locale === "ar" ? status?.nameAr ?? item.status : status?.nameEn ?? item.status}</span></TableCell>
      <TableCell><span className={`inline-flex items-center gap-1 text-xs ${priorityTextClasses[item.priority] ?? "text-muted-foreground"}`}><span aria-hidden="true" className={`h-1.5 w-1.5 rounded-full ${priorityDotClasses[item.priority] ?? "bg-muted-foreground/50"}`} />{t(`workItems.priority${item.priority}`)}</span></TableCell>
      <TableCell className="text-xs text-muted-foreground">{item.assignedTeamName || item.assignedResourceName ? <span className="inline-flex items-center gap-1"><UserRound className="h-3 w-3" />{assignee}</span> : assignee}</TableCell>
      <TableCell>{deadline ? <span className={`inline-flex items-center gap-1 text-xs ${isOverdue ? "font-semibold text-destructive" : "text-muted-foreground"}`}><CalendarDays className="h-3 w-3" />{formatDate(deadline)}</span> : "—"}</TableCell>
      {!filters.projectId ? <TableCell className="max-w-[12rem] truncate" title={item.projectName ?? t("workItems.noProject")}>{item.projectName || t("workItems.noProject")}</TableCell> : null}
      <TableCell className="max-w-[10rem] truncate" title={item.tags}>{item.tags?.split(",")[0]?.trim() ? <span className="inline-flex items-center gap-1 text-xs text-muted-foreground"><Tags className="h-3 w-3" />{item.tags.split(",")[0]?.trim()}</span> : "—"}</TableCell>
      <TableCell onClick={(event) => event.stopPropagation()}><DropdownMenu><DropdownMenuTrigger asChild><Button type="button" size="sm" variant="ghost" className="h-7 w-7 p-0" aria-label={t("workItems.table.actions")}><MoreHorizontal className="h-4 w-4" /></Button></DropdownMenuTrigger><DropdownMenuContent align="end"><DropdownMenuItem onClick={() => setDetailItemId(item.id)}>{t("workItems.viewDetails")}</DropdownMenuItem></DropdownMenuContent></DropdownMenu></TableCell>
    </TableRow>;
  }
  const tableColumnCount = filters.projectId ? 10 : 11;

  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.workItems")} description={t("workItems.pageDescription")} actions={<div className="flex flex-wrap items-center gap-2"><label className="sr-only" htmlFor="work-items-project-scope">{t("workItems.projectScope")}</label><select id="work-items-project-scope" className="input-field h-9 max-w-[16rem]" value={filters.projectId} onChange={(event) => changeProject(event.target.value)}><option value="">{t("workItems.allProjects")}</option>{(projectsQuery.data?.items ?? []).map((project) => <option key={project.publicId} value={project.publicId}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}</select><Button size="sm" disabled={!canCreate} onClick={() => closeCreate(true)}><Plus className="me-1.5 h-4 w-4" />{t("workItems.create")}</Button></div>} />
      <Tabs value={activeTab} onValueChange={(value) => changeTab(value as "backlog" | "board" | "my-work")}>
        <TabsList aria-label={t("workItems.viewMode")} className="h-auto flex-wrap">
          <TabsTrigger value="backlog">{t("workItems.tabBacklog")}</TabsTrigger>
          <TabsTrigger value="board">{t("workItems.tabBoard")}</TabsTrigger>
          <TabsTrigger value="my-work">{t("workItems.tabMyWork")}</TabsTrigger>
        </TabsList>
      </Tabs>
      {activeTab === "backlog" ? <>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <CompactWorkItemKpi label={t("workItems.kpiOpen")} value={insightsQuery.isLoading || insightsQuery.isError ? "—" : insightsQuery.data?.open ?? 0} icon={Clock3} color="text-warning" />
          <CompactWorkItemKpi label={t("workItems.kpiOverdue")} value={insightsQuery.isLoading || insightsQuery.isError ? "—" : insightsQuery.data?.overdue ?? 0} icon={AlertCircle} color="text-destructive" />
          <CompactWorkItemKpi label={t("workItems.kpiUnassigned")} value={insightsQuery.isLoading || insightsQuery.isError ? "—" : insightsQuery.data?.unassigned ?? 0} icon={UserRound} color="text-muted-foreground" />
          <CompactWorkItemKpi label={t("workItems.kpiDueThisWeek")} value={insightsQuery.isLoading || insightsQuery.isError ? "—" : insightsQuery.data?.dueThisWeek ?? 0} icon={CalendarDays} color="text-info" />
        </div>
        <Collapsible open={insightsOpen} onOpenChange={setInsightsOpen}>
          <CollapsibleTrigger asChild><button type="button" className="flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground">{insightsOpen ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}{t(insightsOpen ? "workItems.hideInsights" : "workItems.showInsights")}</button></CollapsibleTrigger>
          <CollapsibleContent className="mt-3">
            {insightsQuery.isError ? <div className="rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive" role="alert">{t("workItems.insightsLoadError")} <Button size="sm" variant="ghost" onClick={() => void insightsQuery.refetch()}>{t("common.retry")}</Button></div> : <div className="grid gap-4 md:grid-cols-3">
              <Card><CardHeader className="pb-3"><CardTitle className="text-sm">{t("workItems.insightByStatus")}</CardTitle></CardHeader><CardContent className="space-y-3">
                {(insightsQuery.data?.byStatus ?? []).length ? insightsQuery.data!.byStatus.map((bucket) => { const max = Math.max(...insightsQuery.data!.byStatus.map((row) => row.count), 1); const status = statuses.find((candidate) => candidate.code === bucket.key); const percent = Math.min(100, Math.round(bucket.count / max * 100)); const color = getStatusColorHex(status?.color); return <div key={bucket.key} className="flex items-center gap-3 text-xs"><span className="w-24 truncate text-muted-foreground">{locale === "ar" ? status?.nameAr ?? bucket.key : status?.nameEn ?? bucket.key}</span><div role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent} className="h-1.5 flex-1 overflow-hidden rounded-full bg-muted/40"><div className="h-full rounded-full" style={{ width: `${percent}%`, backgroundColor: color }} /></div><span className="w-7 text-end font-medium tabular-nums">{bucket.count}</span></div>; }) : <p className="text-xs text-muted-foreground">{t("workItems.insightsNoData")}</p>}
              </CardContent></Card>
              <Card><CardHeader className="pb-3"><CardTitle className="text-sm">{t("workItems.insightByPriority")}</CardTitle></CardHeader><CardContent className="space-y-3">
                {(insightsQuery.data?.byPriority ?? []).length ? insightsQuery.data!.byPriority.map((bucket) => { const max = Math.max(...insightsQuery.data!.byPriority.map((row) => row.count), 1); const percent = Math.min(100, Math.round(bucket.count / max * 100)); return <div key={bucket.key} className="flex items-center gap-3 text-xs"><span className="w-24 truncate text-muted-foreground">{t(`workItems.priority${bucket.key}`)}</span><div role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent} className="h-1.5 flex-1 overflow-hidden rounded-full bg-muted/40"><div className={`h-full rounded-full ${priorityBarClasses[bucket.key] ?? "bg-muted-foreground/40"}`} style={{ width: `${percent}%` }} /></div><span className="w-7 text-end font-medium tabular-nums">{bucket.count}</span></div>; }) : <p className="text-xs text-muted-foreground">{t("workItems.insightsNoData")}</p>}
              </CardContent></Card>
              <Card><CardHeader className="pb-3"><CardTitle className="text-sm">{t("workItems.insightAttention")}</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
                {[[t("workItems.quickOverdue"), insightsQuery.data?.overdue ?? 0, "text-destructive"], [t("workItems.quickUnassigned"), insightsQuery.data?.unassigned ?? 0, "text-muted-foreground"], [t("workItems.quickDueThisWeek"), insightsQuery.data?.dueThisWeek ?? 0, "text-primary"]].map(([label, value, tone]) => <div key={String(label)} className="flex items-center justify-between gap-3"><span className="text-muted-foreground">{label}</span><span className={`font-semibold tabular-nums ${tone}`}>{value}</span></div>)}
              </CardContent></Card>
            </div>}
          </CollapsibleContent>
        </Collapsible>
        <div className="flex flex-wrap gap-1.5" role="group" aria-label={t("workItems.quickView")}>
          {quickViews.map(([key, label]) => <button key={key} type="button" aria-pressed={quickView === key} onClick={() => replaceQuery({ quick: key })} className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-colors ${quickView === key ? "border-primary/40 bg-primary/10 text-primary" : "border-border bg-background text-muted-foreground hover:text-foreground"}`}>{label}</button>)}
        </div>
      </> : null}
      <div className="flex items-center gap-2">
        <div className="relative min-w-0 flex-1"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="ps-8" value={search} onChange={(event) => replaceQuery({ search: event.target.value })} placeholder={t("common.search")} aria-label={t("common.search")} /></div>
        <div className="flex shrink-0 items-center gap-1.5">
          <Button variant="outline" size="sm" className="h-8" onClick={openFilters}><Filter className="me-1 h-3 w-3" />{t("workItems.moreFilters")}{activeFilterCount > 0 ? <span className="ms-1 rounded-full bg-primary/10 px-1.5 text-[10px] tabular-nums text-primary">{activeFilterCount}</span> : null}</Button>
          <DropdownMenu><DropdownMenuTrigger asChild><Button type="button" variant="outline" size="sm" className="h-8"><SortAsc className="me-1 h-3 w-3" />{t("workItems.sortBy")}</Button></DropdownMenuTrigger><DropdownMenuContent align="end">{([["createdAt", "workItems.sortCreated"], ["updatedAt", "workItems.sortUpdated"], ["title", "workItems.sortTitle"], ["priority", "workItems.sortPriority"], ["status", "workItems.sortStatus"], ["dueDate", "workItems.sortDueDate"]] as const).map(([value, key]) => <DropdownMenuItem key={value} onClick={() => replaceQuery({ sort: value })} className={sortBy === value ? "bg-accent" : ""}>{t(key)}</DropdownMenuItem>)}</DropdownMenuContent></DropdownMenu>
          {view === "list" ? <DropdownMenu><DropdownMenuTrigger asChild><Button type="button" variant="outline" size="sm" className="h-8">{t("workItems.groupBy")}</Button></DropdownMenuTrigger><DropdownMenuContent align="end">{([["none", "workItems.groupNone"], ["status", "workItems.groupStatus"], ["assignee", "workItems.groupAssignee"], ["priority", "workItems.groupPriority"], ["project", "workItems.groupProject"], ["type", "workItems.groupType"]] as const).map(([value, key]) => <DropdownMenuItem key={value} onClick={() => { setCollapsedGroups(new Set()); replaceQuery({ group: value }); }} className={groupBy === value ? "bg-accent" : ""}>{t(key)}</DropdownMenuItem>)}</DropdownMenuContent></DropdownMenu> : null}
        </div>
        {search || activeFilterCount > 0 || quickView !== "all" || groupBy !== "none" || sortBy !== "createdAt" ? <Button variant="ghost" onClick={clearAllFilters}>{t("common.clearFilters")}</Button> : null}
      </div>
      <Card><CardContent className="p-0">
        {query.isLoading ? <div className="space-y-3 p-4" aria-label={t("common.loading")}>{view === "kanban" ? <><div className="h-10 w-full animate-pulse rounded-lg bg-muted/60" /><div className="h-64 w-full animate-pulse rounded-lg bg-muted/60" /></> : Array.from({ length: 5 }).map((_, index) => <div key={index} className="h-12 w-full animate-pulse rounded-lg bg-muted/60" />)}</div> : query.isError ? <div className="space-y-2 p-8 text-center"><ErrorState message={t("common.errorLoading")} /><Button type="button" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div> : view === "kanban" ? <WorkItemsKanban items={items} statuses={boardStatuses} locale={locale} canEdit={canEdit} movingItemId={movingItemId} moveError={moveError} onMove={(itemId, targetStatus) => void moveItem(itemId, targetStatus)} onOpen={setDetailItemId} t={(key) => t(key)} /> : items.length === 0 ? <div className="space-y-2 px-5 py-12 text-center"><h2 className="text-base font-semibold">{activeTab === "my-work" ? t("workItems.emptyMyWork") : t("workItems.emptyTitle")}</h2><p className="mx-auto max-w-lg text-sm text-muted-foreground">{activeTab === "my-work" ? t("workItems.emptyMyWorkHint") : t("workItems.emptyHint")}</p>{canCreate && activeTab !== "my-work" ? <Button size="sm" className="mt-2" onClick={() => closeCreate(true)}><Plus className="me-1.5 h-4 w-4" />{t("workItems.create")}</Button> : null}</div> : (
          <Table className="[&_th]:h-9 [&_th]:px-2.5 [&_td]:p-2.5"><TableHeader><TableRow>
            <TableHead className="w-10"><input type="checkbox" checked={isAllVisibleWorkItemsSelected(selectedIds, items.map((item) => item.id))} onChange={toggleSelectAll} aria-label={t("workItems.selectAll")} className="h-4 w-4 rounded border-input" /></TableHead>
            <TableHead className="w-12">{t("common.type")}</TableHead><TableHead className="w-24">{t("workItems.table.id")}</TableHead><TableHead>{t("common.title")}</TableHead><TableHead className="w-28">{t("common.status")}</TableHead><TableHead className="w-24">{t("common.priority")}</TableHead><TableHead className="w-32">{t("workItems.assignee")}</TableHead><TableHead className="w-28">{t("workItems.dueDate")}</TableHead>
            {!filters.projectId ? <TableHead>{t("workItems.project")}</TableHead> : null}<TableHead>{t("workItems.table.tags")}</TableHead><TableHead>{t("workItems.table.actions")}</TableHead>
          </TableRow></TableHeader><TableBody>
            {groupBy === "none" ? items.map(renderItemRow) : Array.from(groupedItems.entries()).map(([key, groupItems]) => <React.Fragment key={key}><TableRow><TableCell colSpan={tableColumnCount}><Button type="button" variant="ghost" size="sm" aria-expanded={!collapsedGroups.has(key)} onClick={() => toggleGroup(key)} className="gap-2 font-semibold">{collapsedGroups.has(key) ? <ChevronRight className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}{groupLabel(key)}<span className="rounded-full bg-muted px-2 py-0.5 text-xs tabular-nums text-muted-foreground">{groupItems.length}</span></Button></TableCell></TableRow>{collapsedGroups.has(key) ? null : groupItems.map(renderItemRow)}</React.Fragment>)}
          </TableBody></Table>
        )}
      </CardContent></Card>
      {query.data && (query.data.totalPages ?? 1) > 1 ? <Pagination page={page} totalPages={query.data.totalPages ?? 1} total={query.data.totalCount} pageSize={pageSize} onChange={(nextPage) => replaceQuery({ page: String(nextPage) })} /> : null}
      <SideSheet open={filtersOpen} onOpenChange={setFiltersOpen} title={t("workItems.advancedFilters")} description={t("workItems.advancedFiltersHint")} size="md" closeLabel={t("common.close")}>
        <SideSheetBody className="space-y-4">
          <div className="space-y-1.5"><Label>{t("common.type")}</Label><select className="input-field w-full" value={filterDraft.type} onChange={(event) => setFilterDraft((current) => ({ ...current, type: event.target.value }))}><option value="all">{t("common.all")}</option><option value="Task">{t("workItems.task")}</option><option value="Issue">{t("workItems.issue")}</option></select></div>
          <div className="space-y-1.5"><Label>{t("common.status")}</Label><select className="input-field w-full" value={filterDraft.status} onChange={(event) => setFilterDraft((current) => ({ ...current, status: event.target.value }))}><option value="all">{t("common.all")}</option>{(statusesQuery.data ?? []).filter((status) => status.isActive).sort((a, b) => a.displayOrder - b.displayOrder).map((status) => <option key={status.id} value={status.code}>{locale === "ar" ? status.nameAr : status.nameEn}</option>)}</select></div>
          <div className="space-y-1.5"><Label>{t("common.priority")}</Label><select className="input-field w-full" value={filterDraft.priority} onChange={(event) => setFilterDraft((current) => ({ ...current, priority: event.target.value }))}><option value="all">{t("common.all")}</option>{priorities.map((item) => <option key={item} value={item}>{t(`workItems.priority${item}`)}</option>)}</select></div>
          <div className="space-y-1.5"><Label>{t("workItems.assignee")}</Label><select className="input-field w-full" value={filterDraft.assignee} onChange={(event) => setFilterDraft((current) => ({ ...current, assignee: event.target.value }))}><option value="all">{t("workItems.assigneeAll")}</option><option value="me">{t("workItems.assigneeMe")}</option><option value="unassigned">{t("workItems.unassigned")}</option>{(usersQuery.data ?? []).map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.fullName}</option>)}</select>{usersQuery.isError ? <div role="alert" className="flex items-center justify-between gap-2 text-xs text-destructive"><span>{t("workItems.assigneeFilterLoadError")}</span><Button type="button" size="sm" variant="outline" onClick={() => void usersQuery.refetch()}>{t("common.retry")}</Button></div> : null}</div>
          <div className="space-y-1.5"><Label>{t("workItems.project")}</Label><select className="input-field w-full" value={filterDraft.projectId} onChange={(event) => setFilterDraft((current) => ({ ...current, projectId: event.target.value }))}><option value="">{t("workItems.allProjects")}</option>{(projectsQuery.data?.items ?? []).map((project) => <option key={project.publicId} value={project.publicId}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}</select></div>
          <div className="space-y-1.5"><Label>{t("workItems.dueDateRange")}</Label><div className="grid gap-3 sm:grid-cols-2"><label className="space-y-1 text-xs text-muted-foreground">{t("workItems.fromDate")}<Input type="date" value={filterDraft.dueFrom} onChange={(event) => setFilterDraft((current) => ({ ...current, dueFrom: event.target.value }))} /></label><label className="space-y-1 text-xs text-muted-foreground">{t("workItems.toDate")}<Input type="date" value={filterDraft.dueTo} onChange={(event) => setFilterDraft((current) => ({ ...current, dueTo: event.target.value }))} /></label></div></div>
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="ghost" onClick={() => setFilterDraft(emptyWorkItemListFilters)}>{t("common.clearFilters")}</Button><Button type="button" variant="outline" onClick={() => setFiltersOpen(false)}>{t("common.cancel")}</Button><Button type="button" onClick={applyFilters}>{t("workItems.applyFilters")}</Button></SideSheetFooter>
      </SideSheet>
      <SideSheet open={createOpen} onOpenChange={closeCreate} title={t("workItems.create")} description={t("workItems.createDescription")} size="xl">
        <SideSheetBody className="flex flex-col gap-4">{createError ? <p role="alert" className="shrink-0 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{createError}</p> : null}<WorkItemForm draft={draft} onChange={updateDraft} t={t} assignees={createAssignees.data ?? []} assigneesLoading={createAssignees.isLoading} assigneesError={createAssignees.isError} onRetryAssignees={() => { void createAssignees.refetch(); }} projects={formProjects.data?.items ?? []} richTextDescription layout="structured" /></SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeCreate(false)}>{t("common.cancel")}</Button><Button type="button" onClick={() => void save()} disabled={mutations.create.isPending}><Save className="me-1.5 h-4 w-4" />{mutations.create.isPending ? t("common.loading") : t("common.save")}</Button></SideSheetFooter>
      </SideSheet>
      <WorkItemDetailsSideSheetFull id={detailItemId} open={Boolean(detailItemId)} onOpenChange={(open) => { if (!open) setDetailItemId(undefined); }} canEdit={canEdit} statuses={statuses} statusesLoading={statusesQuery.isLoading} statusesError={statusesQuery.isError} onRetryStatuses={() => { void statusesQuery.refetch(); }} />
      <SideSheet open={Boolean(pendingTransition)} onOpenChange={(open) => { if (!open && !movingItemId) setPendingTransition(undefined); }} title={t("workItems.chooseTransition")} description={t("workItems.multipleTransitions")} size="md" closeLabel={t("common.close")}>
        <SideSheetBody className="space-y-2">{(pendingTransition?.options ?? []).map((option) => <Button key={option.code} type="button" variant="outline" className="w-full justify-between" disabled={Boolean(movingItemId)} onClick={() => { if (pendingTransition) void executeTransition(pendingTransition.itemId, pendingTransition.targetStatus, option); }}><span>{locale === "ar" ? option.nameAr : option.nameEn}</span><span className="font-mono text-xs text-muted-foreground">{option.code}</span></Button>)}{moveError ? <p role="alert" className="text-sm text-destructive">{moveError}</p> : null}</SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setPendingTransition(undefined)} disabled={Boolean(movingItemId)}>{t("common.cancel")}</Button></SideSheetFooter>
      </SideSheet>
    </div>
  );
}

export function WorkItemDetailPageFull() {
  const { id } = useParams<{ id: string }>();
  const { t, locale, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useFullWorkItem(id, true);
  const item = query.data;
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit) && canMutate && !effectiveReadOnly);
  const [tab, setTab] = React.useState("comments");
  const [editOpen, setEditOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto | null>(null);
  const [activityPage, setActivityPage] = React.useState(1);
  const [commentsPage, setCommentsPage] = React.useState(1);
  const [expandedReplies, setExpandedReplies] = React.useState<Record<string, boolean>>({});
  const formProjects = useProjects({ page: 1, pageSize: 200 }, editOpen);
  const mutations = useFullWorkItemMutations();
  const commentMutation = useFullWorkItemCommentMutation(id ?? "");
  const draftProjectId = draft?.projectId ?? item?.projectId;
  const assignees = useFullWorkItemAssignees(draftProjectId, editOpen);
  const workflow = useFullWorkItemWorkflow(id, Boolean(id));
  const statusReferences = useWorkItemStatuses(Boolean(id));
  const comments = useFullWorkItemComments(id, tab === "comments", commentsPage, 20);
  const activity = useFullWorkItemActivity(id, tab === "activity", activityPage, 20);
  const [comment, setComment] = React.useState("");
  const [replyTo, setReplyTo] = React.useState<string | undefined>();
  const [error, setError] = React.useState("");
  const richTextLabels = { formatting: t("meetings.formatting"), bold: t("meetings.bold"), italic: t("meetings.italic"), underline: t("meetings.underline"), bulletedList: t("meetings.bulletedList"), numberedList: t("meetings.numberedList"), emoji: t("meetings.emoji") };

  React.useEffect(() => {
    if (!item?.type) return;
    setTab(item.type === "Task" ? "comments" : "activity");
    setActivityPage(1);
    setCommentsPage(1);
    setExpandedReplies({});
    setComment("");
    setReplyTo(undefined);
  }, [id, item?.type]);

  if (query.isLoading) return <LoadingState label={t("common.loading")} />;
  if (query.isError) return <div className="space-y-3 py-8 text-center"><ErrorState message={t("workItems.detailLoadError")} /><Button type="button" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div>;
  if (!item) return <ErrorState message={t("common.notFound")} />;
  const commentItems = comments.data?.items ?? [];
  const rootComments = commentItems.filter((entry) => !entry.parentCommentId);
  const repliesByComment = commentItems.reduce<Record<string, typeof commentItems>>((grouped, entry) => { if (entry.parentCommentId) (grouped[entry.parentCommentId] ??= []).push(entry); return grouped; }, {});
  const itemDraft = draft ?? { title: item.title, description: item.description, type: item.type, priority: item.priority, projectId: item.projectId, assignedResourceId: item.assignedResourceId, assignedTeamId: item.assignedTeamId, dueDate: item.dueDate, targetResolutionDate: item.targetResolutionDate, severity: item.severity, resolution: item.resolution, startDate: item.startDate, identifiedDate: item.identifiedDate, resolvedDate: item.resolvedDate, storyPoints: item.storyPoints, estimatedHours: item.estimatedHours, actualHours: item.actualHours, rowVersion: item.rowVersion };
  const deadline = item.type === "Issue" ? item.targetResolutionDate ?? item.dueDate : item.dueDate;
  const statusLabel = statusReferences.data?.find((status) => status.code === item.status);
  const typeLabel = item.type === "Task" || item.type === "Issue" ? t(`workItems.${item.type.toLowerCase()}`) : item.type;
  function updateDraft(patch: Partial<SaveWorkItemDto>) { setDraft((current) => ({ ...itemDraft, ...current, ...patch })); }
  async function saveEdit() { if (!draft || !id) return; if (!draft.title.trim()) { setError(t("workItems.validation")); return; } setError(""); try { await mutations.update.mutateAsync({ id, body: { ...draft, title: draft.title.trim(), dueDate: draft.dueDate || undefined } }); setEditOpen(false); setDraft(null); await query.refetch(); } catch (cause) { setError(cause instanceof Error ? cause.message : t("common.errorLoading")); } }
  async function changeStatus(transitionCode: string) { setError(""); try { await workflow.execute.mutateAsync({ transitionCode }); } catch (cause) { setError(cause instanceof Error ? cause.message : t("workItems.moveError")); } }
  async function addComment() { if (!id || !comment.replace(/<[^>]*>/g, "").trim()) { setError(t("workItems.validation")); return; } setError(""); const parentId = replyTo; try { await commentMutation.mutateAsync({ commentText: comment.trim(), parentCommentId: parentId }); setComment(""); setReplyTo(undefined); setCommentsPage(1); if (parentId) setExpandedReplies((current) => ({ ...current, [parentId]: true })); } catch (cause) { setError(cause instanceof Error ? cause.message : t("workItems.commentError")); } }
  const showDetailsTab = item.type === "Task";
  const statusColor = getStatusColorHex(statusLabel?.color);
  const activityActionLabels: Record<string, string> = { Created: t("workItems.activityCreated"), Updated: t("workItems.activityUpdated"), Transitioned: t("workItems.activityTransitioned"), Assigned: t("workItems.activityAssigned"), Reassigned: t("workItems.activityReassigned"), Unassigned: t("workItems.activityUnassigned"), Deleted: t("workItems.activityDeleted") };
  const formatChanges = (raw?: string) => {
    if (!raw) return "";
    try {
      const parsed = JSON.parse(raw) as Record<string, unknown>;
      return Object.entries(parsed).map(([field, value]) => {
        const fieldLabel = t(({ title: "common.title", description: "workItems.description", type: "common.type", priority: "common.priority", status: "workItems.status", projectId: "workItems.project", assignee: "workItems.assignee", dueDate: "workItems.dueDate", severity: "workItems.severity", resolution: "workItems.resolution" } as Record<string, string>)[field] ?? "workItems.details");
        const pair = value && typeof value === "object" ? value as { from?: unknown; to?: unknown } : undefined;
        const localize = (part: unknown) => typeof part === "string" && priorities.includes(part as WorkItemPriority) ? t(`workItems.priority${part}`) : String(part ?? "—");
        return pair ? `${fieldLabel}: ${localize(pair.from)} → ${localize(pair.to)}` : `${fieldLabel}: ${localize(value)}`;
      }).join(" · ");
    } catch { return raw; }
  };
  const infoField = (label: string, value: string) => <div className="space-y-1 border-b border-border/60 pb-3 last:border-0 last:pb-0"><p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</p><p className="break-words text-sm">{value}</p></div>;

  return <div className="space-y-4">
    <Link to="/work-items" className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground"><ArrowLeft className="h-4 w-4 rtl:rotate-180" />{t("workItems.backToWorkItems")}</Link>
    <Card className="overflow-hidden border-border/70 shadow-sm">
      <header className="flex flex-wrap items-start gap-3 border-b bg-background px-4 py-5 sm:px-6">
        <div className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${item.type === "Issue" ? "bg-info/10 text-info" : "bg-primary/10 text-primary"}`}>{item.type === "Issue" ? <Ticket className="h-5 w-5" /> : <Wrench className="h-5 w-5" />}</div>
        <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span className="font-semibold uppercase tracking-wide">{typeLabel}</span><span aria-hidden="true">·</span><span className="max-w-[16rem] truncate font-mono" title={item.id}>#{item.id}</span></div><h1 className="mt-1 break-words text-xl font-semibold tracking-tight sm:text-2xl">{item.title}</h1><div className="mt-3 flex flex-wrap items-center gap-2"><Badge variant="outline" className="gap-1.5" style={{ color: statusColor, borderColor: statusColor }}><span className="h-2 w-2 rounded-full" style={{ backgroundColor: statusColor }} />{locale === "ar" ? statusLabel?.nameAr ?? item.status : statusLabel?.nameEn ?? item.status}</Badge><Badge status={item.priority}>{t(`workItems.priority${item.priority}`)}</Badge>{item.severity ? <Badge status={item.severity}>{item.severity}</Badge> : null}{deadline ? <span className="inline-flex items-center gap-1.5 text-xs font-medium text-warning"><CalendarDays className="h-3.5 w-3.5" />{t(item.type === "Issue" ? "workItems.targetResolutionDate" : "workItems.deadline")}: {formatDate(deadline)}</span> : null}</div></div>
        {canManage && !editOpen ? <Button type="button" size="sm" variant="outline" onClick={() => { setDraft(itemDraft); setEditOpen(true); setError(""); }}>{t("common.edit")}</Button> : null}
      </header>
      {!editOpen ? <div className="border-b bg-muted/20 px-4 py-3 sm:px-6"><div className="flex flex-wrap items-center gap-2"><span className="text-xs font-medium text-muted-foreground">{t("workItems.status")}:</span><WorkItemStatusTrack
        statuses={statusReferences.data ?? []}
        currentStateCode={workflow.currentState ?? item.status}
        currentStateNameEn={workflow.data?.currentStateNameEn}
        currentStateNameAr={workflow.data?.currentStateNameAr}
        transitions={workflow.availableTransitions}
        canTransition={canManage}
        transitionPending={workflow.execute.isPending}
        loading={workflow.isLoading || statusReferences.isLoading}
        error={statusReferences.isError}
        onRetry={() => void statusReferences.refetch()}
        onTransition={(code) => void changeStatus(code)}
      /></div></div> : null}
      {error ? <p role="alert" className="mx-4 mt-4 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive sm:mx-6">{error}</p> : null}
      {editOpen ? <div className="space-y-4 p-4 sm:p-6"><WorkItemForm draft={itemDraft} onChange={updateDraft} t={t} assignees={assignees.data ?? []} assigneesLoading={assignees.isLoading} assigneesError={assignees.isError} onRetryAssignees={() => { void assignees.refetch(); }} currentAssignee={item.assignedResourceId ? { targetType: "Resource" as const, targetId: item.assignedResourceId, resourceId: item.assignedResourceId, name: item.assignedResourceName ?? t("common.notAvailable") } : item.assignedTeamId ? { targetType: "Team" as const, targetId: item.assignedTeamId, teamId: item.assignedTeamId, name: item.assignedTeamName ?? t("common.notAvailable") } : undefined} currentProject={item.projectId ? { id: item.projectId, name: item.projectName ?? t("common.notAvailable") } : undefined} projects={formProjects.data?.items ?? []} richTextDescription /><WorkItemAttachmentsPanel workItemId={item.id} canManage={canManage || item.createdBy === user?.id} /><div className="flex flex-wrap justify-end gap-2"><Button type="button" variant="outline" onClick={() => { setEditOpen(false); setDraft(null); setError(""); }}>{t("common.cancel")}</Button><Button type="button" disabled={mutations.update.isPending} onClick={() => void saveEdit()}><Save className="me-1.5 h-4 w-4" />{mutations.update.isPending ? t("common.loading") : t("common.save")}</Button></div></div> : <div className="grid gap-4 p-4 sm:p-6 lg:grid-cols-[minmax(0,1fr)_18rem]">
        <Card className="flex min-h-[28rem] flex-col border-border/60 bg-muted/[0.14] p-4"><div className="mb-3 flex items-center gap-2"><Wrench className="h-4 w-4 text-primary" /><h2 className="text-sm font-semibold">{t("workItems.description")}</h2></div><div className="min-h-0 flex-1 overflow-y-auto pe-2"><RichTextContent value={item.description} /></div></Card>
        <aside className="min-w-0 space-y-4"><Card className="border-border/60 p-4"><div className="mb-3 flex items-center gap-2 border-b border-border/60 pb-3"><div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-primary"><Wrench className="h-4 w-4" /></div><h2 className="text-sm font-semibold">{t("workItems.details")}</h2></div><div className="space-y-3">{infoField(t("workItems.project"), item.projectName || t("workItems.noProject"))}{infoField(t("workItems.assignee"), item.assignedTeamName ?? item.assignedResourceName ?? t("workItems.unassigned"))}{item.type === "Issue" ? <>{infoField(t("workItems.severity"), item.severity || t("common.notAvailable"))}{infoField(t("workItems.resolution"), item.resolution || t("common.notAvailable"))}{infoField(t("workItems.identifiedDate"), item.identifiedDate ? formatDate(item.identifiedDate) : t("common.notAvailable"))}{infoField(t("workItems.resolvedDate"), item.resolvedDate ? formatDate(item.resolvedDate) : t("common.notAvailable"))}</> : <>{infoField(t("workItems.storyPoints"), item.storyPoints == null ? t("common.notAvailable") : String(item.storyPoints))}{infoField(t("workItems.estimatedHours"), item.estimatedHours == null ? t("common.notAvailable") : String(item.estimatedHours))}{infoField(t("workItems.actualHours"), item.actualHours == null ? t("common.notAvailable") : String(item.actualHours))}</>}</div></Card><WorkItemAttachmentsPanel workItemId={item.id} canManage={canManage || item.createdBy === user?.id} /></aside>
      </div>}
    </Card>
    {!editOpen ? <Tabs value={tab} onValueChange={(value) => setTab(value)} className="w-full"><TabsList className="flex h-auto w-full flex-wrap justify-start gap-1 rounded-xl border border-border/70 bg-muted/30 p-1 sm:w-fit">{showDetailsTab ? <TabsTrigger value="comments" className="flex-1 gap-1.5 rounded-lg data-[state=active]:bg-info/10 data-[state=active]:text-info sm:flex-none"><MessageSquare className="h-3.5 w-3.5" />{t("workItems.comments")}</TabsTrigger> : null}<TabsTrigger value="activity" className="flex-1 gap-1.5 rounded-lg data-[state=active]:bg-primary/10 data-[state=active]:text-primary sm:flex-none"><History className="h-3.5 w-3.5" />{t("workItems.activity")}</TabsTrigger></TabsList>
      {showDetailsTab ? <TabsContent value="comments"><Card className="border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 px-4 py-4"><CardTitle className="flex items-center gap-2 text-base"><MessageSquare className="h-4 w-4 text-info" />{t("workItems.comments")}</CardTitle></CardHeader><CardContent className="space-y-4 p-4 sm:p-5">{comments.isLoading ? <div className="space-y-3"><div className="h-24 animate-pulse rounded-xl bg-muted/60" /><div className="h-24 animate-pulse rounded-xl bg-muted/60" /></div> : comments.isError ? <div className="space-y-2"><p role="alert" className="text-sm text-destructive">{t("workItems.commentLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void comments.refetch()}>{t("common.retry")}</Button></div> : rootComments.length === 0 ? <p className="rounded-lg border border-dashed px-4 py-6 text-center text-sm text-muted-foreground">{t("workItems.noComments")}</p> : <div className="space-y-4">{rootComments.map((entry) => { const replies = repliesByComment[entry.id] ?? []; const expanded = Boolean(expandedReplies[entry.id]); return <article key={entry.id} className="overflow-hidden rounded-xl border border-border/70 shadow-sm"><header className="flex flex-wrap items-center justify-between gap-3 border-b bg-muted/20 px-3 py-3 sm:px-4"><span className="truncate text-sm font-semibold">{entry.authorName ?? entry.createdBy}</span><time className="text-xs text-muted-foreground">{formatDate(entry.createdAt)}</time></header><div className="p-3 sm:p-4"><RichTextContent value={entry.commentText} /></div>{canManage ? <footer className="border-t px-3 py-2 sm:px-4"><Button type="button" size="sm" variant="ghost" className="gap-1.5 text-xs text-info" onClick={() => { setReplyTo(entry.id); setComment(""); setError(""); }}><Reply className="h-3.5 w-3.5" />{t("workItems.reply")}</Button></footer> : null}{replies.length ? <><Button type="button" size="sm" variant="ghost" className="ms-3 my-1 h-8 gap-1.5 px-2 text-xs text-info" aria-expanded={expanded} onClick={() => setExpandedReplies((current) => ({ ...current, [entry.id]: !expanded }))}><ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} />{t(expanded ? "workItems.hideReplies" : "workItems.showReplies")} ({replies.length})</Button>{expanded ? <div className="space-y-2 px-3 pb-3">{replies.map((reply) => <div key={reply.id} className="ms-3 rounded-md border-s-2 border-info/35 bg-info/[0.035] px-3 py-2"><div className="flex flex-wrap items-center justify-between gap-2 text-xs"><strong>{reply.authorName ?? reply.createdBy}</strong><time className="text-muted-foreground">{formatDate(reply.createdAt)}</time></div><div className="mt-2"><RichTextContent value={reply.commentText} /></div></div>)}</div> : null}</> : null}{replyTo === entry.id && canManage ? <div className="space-y-3 border-t border-primary/25 bg-primary/[0.03] p-3 sm:p-4"><p className="flex items-center gap-2 text-xs font-semibold text-primary"><Reply className="h-3.5 w-3.5" />{t("workItems.replyingToComment")}</p><RichTextEditor id={`work-item-detail-reply-${entry.id}`} value={comment} onChange={setComment} placeholder={t("workItems.replyPlaceholder")} ariaLabel={t("workItems.replyPlaceholder")} t={richTextLabels} minHeight="min-h-20" /><div className="flex justify-end gap-2"><Button type="button" size="sm" variant="outline" onClick={() => { setReplyTo(undefined); setComment(""); }}>{t("common.cancel")}</Button><Button type="button" size="sm" disabled={!comment.replace(/<[^>]*>/g, "").trim() || commentMutation.isPending} onClick={() => void addComment()}><Send className="me-1 h-3.5 w-3.5" />{commentMutation.isPending ? t("common.loading") : t("workItems.reply")}</Button></div></div> : null}</article>; })}</div>}
        {comments.data && (comments.data.totalPages ?? 1) > 1 ? <Pagination page={commentsPage} totalPages={comments.data.totalPages ?? 1} total={comments.data.totalCount} pageSize={20} onChange={setCommentsPage} /> : null}{canManage && !replyTo ? <div className="space-y-3 rounded-xl border border-info/25 bg-info/[0.04] p-3 sm:p-4"><RichTextEditor id={`work-item-detail-comment-${item.id}`} value={comment} onChange={setComment} placeholder={t("workItems.commentPlaceholder")} ariaLabel={t("workItems.commentPlaceholder")} t={richTextLabels} /><div className="flex justify-end"><Button type="button" size="sm" disabled={!comment.replace(/<[^>]*>/g, "").trim() || commentMutation.isPending} onClick={() => void addComment()}><Send className="me-1 h-3.5 w-3.5" />{commentMutation.isPending ? t("common.loading") : t("workItems.addComment")}</Button></div></div> : null}</CardContent></Card></TabsContent> : null}
      <TabsContent value="activity"><Card className="border-primary/15 shadow-sm"><CardHeader><CardTitle className="flex items-center gap-2 text-base"><History className="h-4 w-4 text-primary" />{t("workItems.activity")}</CardTitle><p className="text-xs text-muted-foreground">{t("workItems.activityHint")}</p></CardHeader><CardContent className="space-y-4">{activity.isLoading ? <div className="space-y-3"><div className="h-14 animate-pulse rounded-lg bg-muted/60" /><div className="h-14 animate-pulse rounded-lg bg-muted/60" /></div> : activity.isError ? <div className="space-y-2"><p role="alert" className="text-sm text-destructive">{t("workItems.activityLoadError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void activity.refetch()}>{t("common.retry")}</Button></div> : (activity.data?.items ?? []).length ? <div className={`space-y-4 ${activity.isFetching ? "opacity-60" : ""}`}>{activity.data!.items.map((entry) => <div key={entry.id} className={`flex gap-3 border-s-2 ps-4 ${entry.action === "Created" ? "border-success" : entry.action === "Transitioned" ? "border-warning" : entry.action === "Deleted" ? "border-destructive" : "border-info"}`}><div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted/70"><span className={`h-2 w-2 rounded-full ${entry.action === "Created" ? "bg-success" : entry.action === "Transitioned" ? "bg-warning" : entry.action === "Deleted" ? "bg-destructive" : "bg-info"}`} /></div><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><Badge variant="secondary">{activityActionLabels[entry.action] ?? entry.action}</Badge><span className="text-[11px] text-muted-foreground">{entry.userName ?? entry.userId ?? t("workItems.activitySystem")} · {formatDate(entry.timestamp)}</span></div><p className="mt-2 break-words text-xs leading-5 text-muted-foreground">{formatChanges(entry.changes) || "—"}</p></div></div>)}</div> : <p className="text-sm text-muted-foreground">{t("workItems.noActivity")}</p>}{activity.data && (activity.data.totalPages ?? 1) > 1 ? <Pagination page={activityPage} totalPages={activity.data.totalPages ?? 1} total={activity.data.totalCount} pageSize={20} onChange={setActivityPage} /> : null}</CardContent></Card></TabsContent>
    </Tabs> : null}
  </div>;
}
