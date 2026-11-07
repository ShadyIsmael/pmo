import * as React from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { AlertCircle, CheckCircle2, Clock3, Filter, Paperclip, Plus, Ticket, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { EmptyState } from "@components/ui/StateDisplay";
import { Input } from "@components/ui/Input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { KpiCard } from "@components/ui/KpiCard";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useFullWorkItemAssignees, useFullWorkItemMutations, useFullWorkItems, workItemsApiFull, type SaveWorkItemDto, type WorkItemPriority } from "@api/workItemsFull";
import { useWorkItemStatuses } from "@api/workItems";
import { hasPermission, Permissions } from "@lib/permissions";
import { getStatusTextClass } from "@lib/status-presentation";
import { WorkItemForm } from "@features/delivery/WorkItemForm";
import { useProjects } from "@features/projects/api/projects";

const issuePriorities: WorkItemPriority[] = ["Critical", "High", "Medium", "Low", "Backlog"];
type WorkItemFilter = "all" | "task" | "issue";

function getPriorityLabel(priority: WorkItemPriority, t: (key: string) => string): string { return t(`workItems.priority${priority}`); }

function emptyIssueDraft(): SaveWorkItemDto { return { title: "", description: "", type: "Issue", priority: "Medium", severity: "Medium" }; }

function getPriorityTextClass(priority: WorkItemPriority): string {
  if (priority === "Critical") return "text-destructive";
  if (priority === "High") return "text-amber-700 dark:text-amber-300";
  if (priority === "Medium") return "text-blue-700 dark:text-blue-300";
  return "text-muted-foreground";
}

export function IssuesPage() {
  const { t, formatDate, formatNumber, locale } = useI18n();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const [search, setSearch] = React.useState("");
  const [priority, setPriority] = React.useState<WorkItemPriority | "all">("all");
  const [status, setStatus] = React.useState("all");
  const [typeFilter, setTypeFilter] = React.useState<WorkItemFilter>("issue");
  const [filtersOpen, setFiltersOpen] = React.useState(false);
  const [createOpen, setCreateOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto>(emptyIssueDraft);
  const [createError, setCreateError] = React.useState("");
  const [pendingFiles, setPendingFiles] = React.useState<File[]>([]);
  const [createdIssueId, setCreatedIssueId] = React.useState<string>();
  const [savingIssue, setSavingIssue] = React.useState(false);
  const attachmentInputRef = React.useRef<HTMLInputElement>(null);
  const statusesQuery = useWorkItemStatuses(true);
  const statuses = statusesQuery.data ?? [];
  const projectId = searchParams.get("projectId") || undefined;
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, createOpen);
  const draftProjectPublicId = projectsQuery.data?.items.find((project) => project.id === draft.projectId)?.publicId;
  const assigneesQuery = useFullWorkItemAssignees(draftProjectPublicId, createOpen);
  const mutations = useFullWorkItemMutations();
  const query = useFullWorkItems({
    page: 1,
    pageSize: 100,
    projectId,
    type: typeFilter === "all" ? undefined : typeFilter === "task" ? "Task" : "Issue",
    search: search.trim() || undefined,
    priority: priority === "all" ? undefined : priority,
    status: status === "all" ? undefined : status,
  });
  const items = query.data?.items ?? [];
  const doneCode = statuses.find((status) => status.code.toLowerCase() === "done")?.code;
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create) && canMutate && !effectiveReadOnly);
  const totalCount = query.data?.totalCount ?? 0;
  const openCount = items.filter((item) => item.status !== doneCode).length;
  const doneCount = items.filter((item) => item.status === doneCode).length;
  const criticalCount = items.filter((item) => item.priority === "Critical").length;
  const hasFilters = Boolean(search.trim()) || priority !== "all" || status !== "all";
  const formatCount = (value: number) => formatNumber(value);

  function openCreate() {
    setDraft(emptyIssueDraft());
    setCreateError("");
    setPendingFiles([]);
    setCreatedIssueId(undefined);
    setCreateOpen(true);
  }

  function closeCreate(open: boolean) {
    setCreateOpen(open);
    if (!open) {
      const createdId = createdIssueId;
      setDraft(emptyIssueDraft());
      setCreateError("");
      setPendingFiles([]);
      setCreatedIssueId(undefined);
      if (createdId) navigate(`/work-items/${encodeURIComponent(createdId)}`);
    }
  }

  function updateDraft(patch: Partial<SaveWorkItemDto>) { setDraft((current) => ({ ...current, ...patch })); }

  function queueAttachments(files: FileList | null) {
    if (files?.length) setPendingFiles((current) => [...current, ...Array.from(files)]);
    if (attachmentInputRef.current) attachmentInputRef.current.value = "";
  }

  function finishCreate(itemId: string) {
    setCreateOpen(false);
    setDraft(emptyIssueDraft());
    setCreateError("");
    setPendingFiles([]);
    setCreatedIssueId(undefined);
    navigate(`/work-items/${encodeURIComponent(itemId)}`);
  }

  async function saveIssue() {
    if (!createdIssueId && !draft.title.trim()) {
      setCreateError(t("workItems.validation"));
      return;
    }
    setCreateError("");
    setSavingIssue(true);
    let itemId = createdIssueId;
    try {
      if (!itemId) {
        const item = await mutations.create.mutateAsync({ ...draft, title: draft.title.trim(), dueDate: undefined });
        itemId = item.id;
        setCreatedIssueId(itemId);
      }
      if (pendingFiles.length) await workItemsApiFull.uploadAttachments(itemId, pendingFiles);
      finishCreate(itemId);
    } catch {
      setCreateError(itemId ? t("workItems.attachmentUploadError") : t("workItems.createError"));
    } finally {
      setSavingIssue(false);
    }
  }

  function clearFilters() {
    setSearch("");
    setPriority("all");
    setStatus("all");
  }

  function statusName(statusCode: string) {
    const status = statuses.find((candidate) => candidate.code === statusCode);
    return locale === "ar" ? status?.nameAr ?? statusCode : status?.nameEn ?? statusCode;
  }

  return <div className="space-y-6">
    <PageHeader
      title={t("nav.issues")}
      description={`${t("risks.issuesDescription")} · ${formatCount(totalCount)} ${t("risks.totalIssues")}`}
      actions={<Button type="button" variant="outline" size="sm" disabled={!canCreate} onClick={openCreate}><Plus className="me-2 h-4 w-4" />{t("risks.newIssue")}</Button>}
    />

    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <KpiCard label={t("common.all")} value={formatCount(totalCount)} icon={Ticket} semantic="primary" />
      <KpiCard label={t("risks.issueOpen")} value={formatCount(openCount)} icon={Clock3} semantic="amber" />
      <KpiCard label={t("common.done")} value={formatCount(doneCount)} icon={CheckCircle2} semantic="green" />
      <KpiCard label={t("risks.critical")} value={formatCount(criticalCount)} icon={AlertCircle} semantic="red" />
    </div>

    <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
      <Input className="min-w-64 flex-1" value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("risks.searchIssues")} aria-label={t("risks.searchIssues")} />
      <div className="flex flex-wrap items-center gap-2">
        <label className="sr-only" htmlFor="issues-priority-filter">{t("common.priority")}</label>
        <select id="issues-priority-filter" className="input-field" value={priority} onChange={(event) => setPriority(event.target.value as WorkItemPriority | "all")} aria-label={t("common.priority")}>
          <option value="all">{t("common.all")}</option>
          {issuePriorities.map((item) => <option key={item} value={item}>{getPriorityLabel(item, t)}</option>)}
        </select>
        <Button variant="outline" size="sm" type="button" aria-expanded={filtersOpen} onClick={() => setFiltersOpen((open) => !open)}>
          <Filter className="me-2 h-4 w-4" />{t("common.filter")}
        </Button>
        {hasFilters ? <Button variant="outline" size="sm" onClick={clearFilters}>{t("common.clearFilters")}</Button> : null}
      </div>
    </div>
    {filtersOpen ? <div className="max-w-xs space-y-1.5">
      <label htmlFor="issues-status-filter" className="text-sm font-medium">{t("common.status")}</label>
      <select id="issues-status-filter" className="input-field w-full" value={status} onChange={(event) => setStatus(event.target.value)}>
        <option value="all">{t("risks.allStatuses")}</option>
        {[...statuses].sort((left, right) => left.displayOrder - right.displayOrder).map((item) => <option key={item.id} value={item.code}>{locale === "ar" ? item.nameAr : item.nameEn}</option>)}
      </select>
    </div> : null}

    <div className="flex flex-wrap gap-1 rounded-lg border bg-muted/20 p-1" role="tablist" aria-label={t("common.type")}>
      {(["all", "task", "issue"] as const).map((value) => (
        <button key={value} type="button" role="tab" aria-selected={typeFilter === value} onClick={() => setTypeFilter(value)} className={`rounded-md px-3 py-1.5 text-sm ${typeFilter === value ? "bg-background font-medium shadow-sm" : "text-muted-foreground hover:text-foreground"}`}>
          {value === "all" ? t("common.all") : value === "task" ? t("workItems.task") : t("workItems.issue")}
        </button>
      ))}
    </div>

    <div className="rounded-lg border bg-card">
      {query.isLoading ? <div className="space-y-3 p-5" aria-label={t("common.loading")}><Skeleton className="h-8" /><Skeleton className="h-12" /><Skeleton className="h-12" /></div>
        : query.isError ? <div className="flex flex-col items-center justify-center gap-4 py-12 text-center" role="alert"><p className="text-sm text-destructive">{t("common.errorLoading")}</p><Button variant="outline" size="sm" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div>
          : items.length === 0 ? <EmptyState title={t("risks.emptyIssues")} description={t("risks.emptyIssuesHint")} icon={<Ticket className="h-8 w-8" />} action={<Button type="button" variant="outline" size="sm" disabled={!canCreate} onClick={openCreate}><Plus className="me-2 h-4 w-4" />{t("risks.newIssue")}</Button>} />
            : <Table>
              <TableHeader><TableRow>
                <TableHead>{t("common.title")}</TableHead>
                <TableHead>{t("workItems.project")}</TableHead>
                <TableHead>{t("common.priority")}</TableHead>
                <TableHead>{t("workItems.dueDate")}</TableHead>
                <TableHead>{t("common.status")}</TableHead>
              </TableRow></TableHeader>
              <TableBody>{items.map((item) => {
                const deadline = item.type === "Issue" ? item.targetResolutionDate : item.dueDate;
                return <TableRow key={item.id} className="cursor-pointer" onClick={() => navigate(`/work-items/${encodeURIComponent(item.id)}`)} onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); navigate(`/work-items/${encodeURIComponent(item.id)}`); } }} tabIndex={0}>
                  <TableCell><Link to={`/work-items/${encodeURIComponent(item.id)}`} onClick={(event) => event.stopPropagation()} className="font-semibold hover:underline">{item.title}</Link></TableCell>
                  <TableCell>{item.projectName || t("workItems.noProject")}</TableCell>
                  <TableCell><span className={`font-medium ${getPriorityTextClass(item.priority)}`}>{getPriorityLabel(item.priority, t)}</span></TableCell>
                  <TableCell>{deadline ? formatDate(deadline) : "—"}</TableCell>
                  <TableCell><span className={`font-medium ${getStatusTextClass(item.status)}`}>{statusName(item.status)}</span></TableCell>
                </TableRow>;
              })}</TableBody>
            </Table>}
    </div>

    <SideSheet open={createOpen} onOpenChange={closeCreate} title={t("risks.newIssue")} description={t("risks.createIssueDescription")} closeLabel={t("common.cancel")}>
      <SideSheetBody className="space-y-4">
        <input ref={attachmentInputRef} id="issue-attachments" type="file" multiple className="sr-only" aria-label={t("workItems.chooseFiles")} onChange={(event) => queueAttachments(event.target.files)} />
        {createError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{createError}</p> : null}
        {createdIssueId ? <div className="space-y-3">
          <div className="flex items-center gap-2"><Paperclip className="h-4 w-4 text-muted-foreground" /><span className="text-sm font-medium">{t("workItems.pendingAttachments")}</span><span className="text-xs text-muted-foreground">{formatCount(pendingFiles.length)}</span></div>
          {pendingFiles.map((file, index) => <div key={`${file.name}-${file.size}-${index}`} className="flex items-center gap-2 rounded-md border p-2"><Paperclip className="h-4 w-4 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate text-sm" title={file.name}>{file.name}</span><Button type="button" size="icon-sm" variant="ghost" aria-label={`${t("workItems.remove")}: ${file.name}`} disabled={savingIssue} onClick={() => setPendingFiles((current) => current.filter((_, fileIndex) => fileIndex !== index))}><Trash2 className="h-4 w-4" /></Button></div>)}
          <Button type="button" variant="outline" size="sm" onClick={() => attachmentInputRef.current?.click()} disabled={savingIssue}><Paperclip className="me-2 h-4 w-4" />{t("workItems.chooseFiles")}</Button>
        </div> : <WorkItemForm draft={draft} onChange={updateDraft} t={t} assignees={assigneesQuery.data ?? []} assigneesLoading={assigneesQuery.isLoading} assigneesError={assigneesQuery.isError} onRetryAssignees={() => { void assigneesQuery.refetch(); }} projects={projectsQuery.data?.items ?? []} allowTypeChange={false} richTextDescription extraContent={<div className="space-y-2"><div className="flex items-center gap-2"><Paperclip className="h-4 w-4 text-muted-foreground" /><span className="text-sm font-medium">{t("workItems.attachments")}</span></div><Button type="button" variant="outline" size="sm" onClick={() => attachmentInputRef.current?.click()}><Paperclip className="me-2 h-4 w-4" />{t("workItems.chooseFiles")}</Button>{pendingFiles.map((file, index) => <div key={`${file.name}-${file.size}-${index}`} className="flex items-center gap-2 rounded-md border p-2"><Paperclip className="h-4 w-4 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate text-sm" title={file.name}>{file.name}</span><Button type="button" size="icon-sm" variant="ghost" aria-label={`${t("workItems.remove")}: ${file.name}`} disabled={savingIssue} onClick={() => setPendingFiles((current) => current.filter((_, fileIndex) => fileIndex !== index))}><Trash2 className="h-4 w-4" /></Button></div>)}</div>} />}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => closeCreate(false)} disabled={savingIssue}>{t("common.cancel")}</Button>
        <Button type="button" onClick={() => void saveIssue()} disabled={savingIssue || mutations.create.isPending}>{savingIssue || mutations.create.isPending ? t("common.loading") : createdIssueId ? pendingFiles.length ? t("workItems.upload") : t("workItems.openFullDetails") : t("common.create")}</Button>
      </SideSheetFooter>
    </SideSheet>
  </div>;
}
