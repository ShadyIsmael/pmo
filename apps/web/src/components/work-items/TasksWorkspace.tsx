"use client";

import { useMemo, useState } from "react";
import { AlertCircle, CalendarClock, CheckCircle2, Clock3, ListChecks, Search } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/state-display";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { CreateButton, CreateSheet, labels, priorityLabel, statusLabel, type Copy } from "@/components/work-items/WorkItemsWorkspace";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { formatDate } from "@/lib/utils";
import { useWorkItems, type WorkItemDto } from "@/lib/api/work-items";
import { useLocale } from "next-intl";
import { useRouter } from "next/navigation";
import { useWorkItemStatuses } from "@/lib/api/reference-data";

type TaskFilter = "all" | "inProgress" | "done";

function dueSoon(item: WorkItemDto, terminalStatuses: readonly string[]): boolean {
  if (!item.dueDate || terminalStatuses.includes(item.status)) return false;
  const due = new Date(item.dueDate).getTime();
  const now = Date.now();
  return due >= now - 86_400_000 && due <= now + 7 * 86_400_000;
}

function TaskRow({ item, t, locale, statuses, terminalStatuses }: { item: WorkItemDto; t: Copy; locale: string; statuses: Parameters<typeof statusLabel>[2]; terminalStatuses: readonly string[] }) {
  const router = useRouter();
  const dueClass = item.dueDate && dueSoon(item, terminalStatuses) ? "text-warning" : "text-muted-foreground";
  return <button type="button" onClick={() => router.push(`/${locale}/work-items/${encodeURIComponent(item.id)}`)} className="group flex w-full flex-col gap-3 border-b p-4 text-start transition-colors hover:bg-muted/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring sm:flex-row sm:items-center">
    <div className="flex min-w-0 flex-1 items-start gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-info/10 text-info"><ListChecks className="h-4 w-4" /></div><div className="min-w-0"><p className="truncate text-sm font-semibold group-hover:text-primary">{item.title}</p><p className="mt-1 truncate text-xs text-muted-foreground">{item.projectName || t.noProject}</p></div></div>
     <div className="flex flex-wrap items-center gap-2 ps-12 sm:ps-0"><Badge status={item.priority}>{priorityLabel(item.priority, t)}</Badge><Badge status={item.status}>{statusLabel(item.status, t, statuses, locale)}</Badge><span className={`inline-flex items-center gap-1 text-xs ${dueClass}`}><CalendarClock className="h-3.5 w-3.5" />{item.dueDate ? formatDate(item.dueDate, locale === "ar" ? "ar-EG" : "en-GB") : "—"}</span></div>
  </button>;
}

function StatCard({ label, value, icon: Icon, tone }: { label: string; value: number; icon: typeof ListChecks; tone: string }) {
  return <Card className="border-border/70"><CardContent className="flex items-center gap-3 p-4"><div className={`flex h-9 w-9 items-center justify-center rounded-lg ${tone}`}><Icon className="h-4 w-4" /></div><div><p className="text-xl font-bold tabular-nums">{value}</p><p className="text-[11px] text-muted-foreground">{label}</p></div></CardContent></Card>;
}

export function TasksWorkspace() {
  const locale = useLocale();
  const t = locale === "ar" ? labels.ar : labels.en;
  const { user } = useAuth();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create));
  const statusesQuery = useWorkItemStatuses(true);
  const allStatuses = statusesQuery.data ?? [];
  const activeStatuses = allStatuses.filter((status) => status.isActive).sort((left, right) => left.displayOrder - right.displayOrder);
  const inProgressCode = allStatuses.find((status) => status.code.toLowerCase() === "inprogress")?.code;
  const doneCode = allStatuses.find((status) => status.code.toLowerCase() === "done")?.code;
  const cancelledCode = allStatuses.find((status) => status.code.toLowerCase() === "cancelled")?.code;
  const [filter, setFilter] = useState<TaskFilter>("all");
  const [search, setSearch] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const params = useMemo(() => ({ page: 1, pageSize: 100, type: "Task", status: filter === "all" ? undefined : filter === "inProgress" ? inProgressCode : doneCode, search: search.trim() || undefined, assigneeId: user?.id }), [filter, search, inProgressCode, doneCode, user?.id]);
  const allTasksQuery = useWorkItems({ page: 1, pageSize: 100, type: "Task", search: search.trim() || undefined, assigneeId: user?.id });
  const query = useWorkItems(params);
  const tasks = query.data?.items ?? [];
  const allTasks = allTasksQuery.data?.items ?? [];
  const terminalStatuses = [doneCode, cancelledCode].filter((code): code is string => Boolean(code));
  const counts = { all: allTasksQuery.data?.totalCount ?? 0, inProgress: allTasks.filter((item) => item.status === inProgressCode).length, dueSoon: allTasks.filter((item) => dueSoon(item, terminalStatuses)).length, done: allTasks.filter((item) => item.status === doneCode).length };

  return <div className="space-y-6">
    <PageHeader title={t.myWork} description={`${t.myWorkDescription} · ${counts.all} ${t.totalTasks}`} actions={<CreateButton type="Task" canCreate={canCreate} onCreate={() => setCreateOpen(true)} t={t} />} />
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><StatCard label={t.allTasks} value={counts.all} icon={ListChecks} tone="bg-primary/10 text-primary" /><StatCard label={t.inProgressTasks} value={counts.inProgress} icon={Clock3} tone="bg-info/10 text-info" /><StatCard label={t.dueSoon} value={counts.dueSoon} icon={CalendarClock} tone="bg-warning/10 text-warning" /><StatCard label={t.completedTasks} value={counts.done} icon={CheckCircle2} tone="bg-success/10 text-success" /></div>
     <Card className="overflow-hidden border-border/70 shadow-sm"><CardHeader className="gap-4 border-b bg-muted/10 pb-4"><div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between"><CardTitle className="text-base">{t.tasksTitle}</CardTitle><div className="flex w-full items-center gap-2 lg:max-w-md"><div className="relative flex-1"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input aria-label={t.searchTasks} placeholder={t.searchTasks} className="ps-8" value={search} onChange={(event) => setSearch(event.target.value)} /></div>{search ? <Button type="button" variant="ghost" size="sm" onClick={() => setSearch("")}>{t.clearSearch}</Button> : null}</div></div><div className="flex flex-wrap gap-1" role="tablist" aria-label={t.status}>{(["all", "inProgress", "done"] as TaskFilter[]).map((value) => <button key={value} type="button" role="tab" aria-selected={filter === value} onClick={() => setFilter(value)} className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${filter === value ? "bg-primary text-primary-foreground shadow-sm" : "text-muted-foreground hover:bg-muted hover:text-foreground"}`}>{value === "all" ? t.allTasks : value === "inProgress" ? t.inProgressTasks : t.completedTasks}</button>)}</div></CardHeader><CardContent className="p-0">{query.isLoading ? <div className="space-y-3 p-4"><Skeleton className="h-14" /><Skeleton className="h-14" /><Skeleton className="h-14" /></div> : query.isError ? <ErrorState title={t.error} onRetry={() => void query.refetch()} /> : tasks.length === 0 ? <EmptyState title={t.emptyTasks} description={t.emptyTasksHint} icon={ListChecks} action={<CreateButton type="Task" canCreate={canCreate} onCreate={() => setCreateOpen(true)} t={t} />} /> : <div>{tasks.map((item) => <TaskRow key={item.id} item={item} t={t} locale={locale} statuses={allStatuses} terminalStatuses={terminalStatuses} />)}</div>}</CardContent></Card>
     <CreateSheet open={createOpen} onOpenChange={setCreateOpen} t={t} locale={locale} defaultType="Task" statuses={activeStatuses} />
  </div>;
}
