import * as React from "react";
import { useNavigate } from "react-router-dom";
import { ListChecks, Clock3, CalendarClock, CheckCircle2, Search, Plus, Paperclip, Upload, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Skeleton } from "@components/ui/Skeleton";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { formatDate } from "@lib/utils";
import { hasPermission, Permissions } from "@lib/permissions";
import { useProjects } from "@features/projects/api/projects";
import type { ProjectSummaryDto } from "@features/projects/api/projects";
import { useFullWorkItemAssignees, useFullWorkItemMutations, useFullWorkItems, workItemsApiFull, type SaveWorkItemDto, type WorkItemDto, type WorkItemPriority } from "@api/workItemsFull";
import { useWorkItemStatuses, type WorkItemStatusDto } from "@api/workItems";
import { WorkItemForm } from "@features/delivery/WorkItemForm";
import { MyWorkTabs } from "./MyWorkTabs";

type TaskFilter = "all" | "inProgress" | "done";

function emptyTaskDraft(): SaveWorkItemDto {
  return { title: "", description: "", type: "Task", priority: "Medium", dueDate: "" };
}

function dueSoon(item: Pick<WorkItemDto, "dueDate" | "status">, terminalStatuses: readonly string[]): boolean {
  if (!item.dueDate || terminalStatuses.includes(item.status)) return false;
  const due = new Date(item.dueDate).getTime();
  const now = Date.now();
  return due >= now - 86_400_000 && due <= now + 7 * 86_400_000;
}

function priorityLabel(priority: WorkItemPriority, t: (key: string) => string): string {
  const keys: Record<WorkItemPriority, string> = {
    Critical: "priorityCritical", High: "priorityHigh", Medium: "priorityMedium", Low: "priorityLow", Backlog: "priorityBacklog",
  };
  return t(`workItems.${keys[priority]}`);
}

function statusLabel(status: string, statuses: readonly WorkItemStatusDto[], locale: string): string {
  const found = statuses.find((candidate) => candidate.code === status);
  if (!found) return status;
  return locale === "ar" ? found.nameAr : found.nameEn;
}

function TaskRow({ item, t, locale, statuses, terminalStatuses }: {
  item: WorkItemDto;
  t: (key: string) => string;
  locale: string;
  statuses: readonly WorkItemStatusDto[];
  terminalStatuses: readonly string[];
}) {
  const navigate = useNavigate();
  const dueClass = item.dueDate && dueSoon(item, terminalStatuses) ? "text-warning" : "text-muted-foreground";
  return <button type="button" onClick={() => navigate(`/work-items/${encodeURIComponent(item.id)}`)} className="group flex w-full flex-col gap-3 border-b p-4 text-start transition-colors hover:bg-muted/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring sm:flex-row sm:items-center">
    <div className="flex min-w-0 flex-1 items-start gap-3">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-info/10 text-info"><ListChecks className="h-4 w-4" /></div>
      <div className="min-w-0"><p className="truncate text-sm font-semibold group-hover:text-primary">{item.title}</p><p className="mt-1 truncate text-xs text-muted-foreground">{item.projectName || t("workItems.noProject")}</p></div>
    </div>
    <div className="flex flex-wrap items-center gap-2 ps-12 sm:ps-0">
      <Badge status={item.priority}>{priorityLabel(item.priority, t)}</Badge>
      <Badge status={item.status}>{statusLabel(item.status, statuses, locale)}</Badge>
      <span className={`inline-flex items-center gap-1 text-xs ${dueClass}`}><CalendarClock className="h-3.5 w-3.5" />{item.dueDate ? formatDate(item.dueDate, locale === "ar" ? "ar-EG" : "en-GB") : "—"}</span>
    </div>
  </button>;
}

function StatCard({ label, value, icon: Icon, tone }: { label: string; value: number; icon: typeof ListChecks; tone: string }) {
  return <Card className="border-border/70"><CardContent className="flex items-center gap-3 p-4">
    <div className={`flex h-9 w-9 items-center justify-center rounded-lg ${tone}`}><Icon className="h-4 w-4" /></div>
    <div><p className="text-xl font-bold tabular-nums">{value}</p><p className="text-[11px] text-muted-foreground">{label}</p></div>
  </CardContent></Card>;
}

export function MyWorkTasksPage() {
  const { locale, t } = useI18n();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create) && canMutate && !effectiveReadOnly);
  const statusesQuery = useWorkItemStatuses(true);
  const allStatuses = statusesQuery.data ?? [];
  const inProgressCode = allStatuses.find((status) => status.code.toLowerCase() === "inprogress")?.code;
  const doneCode = allStatuses.find((status) => status.code.toLowerCase() === "done")?.code;
  const cancelledCode = allStatuses.find((status) => status.code.toLowerCase() === "cancelled")?.code;
  const [filter, setFilter] = React.useState<TaskFilter>("all");
  const [search, setSearch] = React.useState("");
  const [createOpen, setCreateOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto>(emptyTaskDraft);
  const [createError, setCreateError] = React.useState("");
  const [files, setFiles] = React.useState<File[]>([]);
  const [createdTaskId, setCreatedTaskId] = React.useState<string>();
  const fileInput = React.useRef<HTMLInputElement>(null);
  const allQueryParams = React.useMemo(() => ({ page: 1, pageSize: 100, type: "Task", search: search.trim() || undefined, myWork: true }), [search]);
  const listParams = React.useMemo(() => ({ ...allQueryParams, status: filter === "all" ? undefined : filter === "inProgress" ? inProgressCode : doneCode }), [allQueryParams, filter, inProgressCode, doneCode]);
  const allTasksQuery = useFullWorkItems(allQueryParams);
  const query = useFullWorkItems(listParams);
  const createMutation = useFullWorkItemMutations().create;
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, createOpen);
  const assigneesQuery = useFullWorkItemAssignees(draft.projectId || undefined, createOpen && Boolean(draft.projectId));
  const tasks = query.data?.items ?? [];
  const allTasks = allTasksQuery.data?.items ?? [];
  const terminalStatuses = [doneCode, cancelledCode].filter((code): code is string => Boolean(code));
  const counts = {
    all: allTasksQuery.data?.totalCount ?? 0,
    inProgress: allTasks.filter((item) => item.status === inProgressCode).length,
    dueSoon: allTasks.filter((item) => dueSoon(item, terminalStatuses)).length,
    done: allTasks.filter((item) => item.status === doneCode).length,
  };
  const closeCreate = (open: boolean) => {
    setCreateOpen(open);
    if (!open) { setDraft(emptyTaskDraft()); setCreateError(""); setFiles([]); setCreatedTaskId(undefined); }
  };
  const saveTask = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!draft.title.trim()) { setCreateError(t("workItems.validation")); return; }
    setCreateError("");
    try {
      const created = await createMutation.mutateAsync({ ...draft, title: draft.title.trim(), type: "Task", dueDate: draft.dueDate || undefined });
      if (files.length > 0) {
        setCreatedTaskId(created.id);
        try {
          await workItemsApiFull.uploadAttachments(created.id, files);
        } catch {
          setCreateError(t("workItems.attachmentUploadError"));
          return;
        }
      }
      closeCreate(false);
      navigate(`/work-items/${encodeURIComponent(created.id)}`);
    } catch {
      setCreateError(t("workItems.createError"));
    }
  };
  const updateDraft = (patch: Partial<SaveWorkItemDto>) => setDraft((current) => ({ ...current, ...patch }));

  return <div className="space-y-6">
    <PageHeader title={t("nav.myWork")} description={`${t("workItems.myWorkDescription")} · ${counts.all} ${t("workItems.totalTasks")}`} actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-2 h-4 w-4" />{t("workItems.newTask")}</Button> : undefined} />
    <MyWorkTabs />
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label={t("workItems.allTasks")} value={counts.all} icon={ListChecks} tone="bg-primary/10 text-primary" />
      <StatCard label={t("workItems.inProgressTasks")} value={counts.inProgress} icon={Clock3} tone="bg-info/10 text-info" />
      <StatCard label={t("workItems.dueSoon")} value={counts.dueSoon} icon={CalendarClock} tone="bg-warning/10 text-warning" />
      <StatCard label={t("workItems.completedTasks")} value={counts.done} icon={CheckCircle2} tone="bg-success/10 text-success" />
    </div>
    <Card className="overflow-hidden border-border/70 shadow-sm">
      <CardHeader className="gap-4 border-b bg-muted/10 pb-4">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <CardTitle className="text-base">{t("workItems.tasksTitle")}</CardTitle>
          <div className="flex w-full items-center gap-2 lg:max-w-md"><div className="relative flex-1"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input aria-label={t("workItems.searchTasks")} placeholder={t("workItems.searchTasks")} className="ps-8" value={search} onChange={(event) => setSearch(event.target.value)} /></div>
            {search ? <Button type="button" variant="ghost" size="sm" onClick={() => setSearch("")}>{t("workItems.clearSearch")}</Button> : null}
          </div>
        </div>
        <div className="flex flex-wrap gap-1" role="tablist" aria-label={t("workItems.status")}>
          {(["all", "inProgress", "done"] as TaskFilter[]).map((value) => <button key={value} type="button" role="tab" aria-selected={filter === value} onClick={() => setFilter(value)} className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${filter === value ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:bg-muted hover:text-foreground"}`}>
            {value === "all" ? t("workItems.allTasks") : value === "inProgress" ? t("workItems.inProgressTasks") : t("workItems.completedTasks")}
          </button>)}
        </div>
      </CardHeader>
      <CardContent className="p-0">
        {query.isLoading ? <div className="space-y-3 p-4"><Skeleton className="h-14" /><Skeleton className="h-14" /><Skeleton className="h-14" /></div>
          : query.isError ? <ErrorState title={t("workItems.loadError")} onRetry={() => void query.refetch()} />
            : tasks.length === 0 ? <EmptyState title={t("workItems.emptyTasks")} description={t("workItems.emptyTasksHint")} icon={<ListChecks className="h-8 w-8" />} action={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-2 h-4 w-4" />{t("workItems.newTask")}</Button> : undefined} />
              : <div>{tasks.map((item) => <TaskRow key={item.id} item={item} t={t} locale={locale} statuses={allStatuses} terminalStatuses={terminalStatuses} />)}</div>}
      </CardContent>
    </Card>
    <SideSheet open={createOpen} onOpenChange={closeCreate} title={t("workItems.newTask")} description={t("workItems.createTaskDescription")} size="md" closeLabel={t("common.cancel")}>
      <form onSubmit={(event) => void saveTask(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {createError ? <div role="alert" className="space-y-2 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive"><p>{createError}</p>{createdTaskId ? <Button type="button" size="sm" variant="outline" onClick={() => navigate(`/work-items/${encodeURIComponent(createdTaskId)}`)}>{t("workItems.viewDetails")}</Button> : null}</div> : null}
          <WorkItemForm draft={draft} onChange={updateDraft} t={t} assignees={assigneesQuery.data ?? []} assigneesLoading={assigneesQuery.isLoading} assigneesError={assigneesQuery.isError} onRetryAssignees={() => { void assigneesQuery.refetch(); }} projects={(projectsQuery.data?.items ?? []) as readonly ProjectSummaryDto[]} allowTypeChange={false} richTextDescription extraContent={<div className="space-y-2">
            <Label>{t("workItems.attachments")}</Label>
            <input ref={fileInput} className="sr-only" type="file" multiple onChange={(event) => { if (event.target.files?.length) setFiles((current) => [...current, ...Array.from(event.target.files ?? [])]); event.target.value = ""; }} />
            <Button type="button" variant="outline" size="sm" onClick={() => fileInput.current?.click()}><Upload className="me-2 h-4 w-4" />{t("workItems.chooseFiles")}</Button>
            {files.length ? <div className="space-y-2 rounded-md border border-dashed p-3"><p className="flex items-center gap-2 text-xs font-medium"><Paperclip className="h-3.5 w-3.5" />{t("workItems.selectedAttachments")} · {files.length}</p>{files.map((file, index) => <div key={`${file.name}-${file.size}-${index}`} className="flex items-center gap-2 text-xs"><span className="min-w-0 flex-1 truncate" title={file.name}>{file.name}</span><Button type="button" size="sm" variant="ghost" aria-label={`${t("workItems.remove")}: ${file.name}`} onClick={() => setFiles((current) => current.filter((_, fileIndex) => fileIndex !== index))}><Trash2 className="h-3.5 w-3.5 text-destructive" /></Button></div>)}</div> : null}
          </div>} />
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeCreate(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={createMutation.isPending || Boolean(createdTaskId) || !draft.title.trim()}>{createMutation.isPending ? t("common.loading") : t("common.save")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  </div>;
}
