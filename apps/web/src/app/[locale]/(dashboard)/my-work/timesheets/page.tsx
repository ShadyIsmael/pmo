"use client";

import { useMemo, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { useTranslations, useLocale } from "next-intl";
import { CalendarDays, Clock3, Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useProjects } from "@/hooks/projects/useProjects";
import { useCreateTimeEntry, useDeleteTimeEntry, useTimeEntries, useTimeEntrySummary, useUpdateTimeEntry } from "@/hooks/time-tracking/useTimeEntries";

const toDateInput = (date: Date) => date.toISOString().slice(0, 10);
const formatHours = (value: number, locale: string) => new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(value);

export default function TimesheetsPage() {
  const t = useTranslations("timeTracking");
  const locale = useLocale();
  const today = useMemo(() => new Date(), []);
  const from = useMemo(() => { const date = new Date(today); date.setDate(date.getDate() - 6); return toDateInput(date); }, [today]);
  const to = toDateInput(today);
  const [form, setForm] = useState({ projectId: "", workDate: to, startTime: "09:00", endTime: "17:00", description: "" });
  const [editing, setEditing] = useState<{ id: string; rowVersion: number[] } | null>(null);
  const projectsQuery = useProjects({ page: 1, pageSize: 200 });
  const entriesQuery = useTimeEntries({ page: 1, pageSize: 100, from, to });
  const summaryQuery = useTimeEntrySummary({ from, to });
  const createMutation = useCreateTimeEntry();
  const deleteMutation = useDeleteTimeEntry();
  const updateMutation = useUpdateTimeEntry(editing?.id ?? "");
  const projects = projectsQuery.data?.items ?? [];
  const entries = entriesQuery.data?.items ?? [];
  const summary = summaryQuery.data;

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!form.projectId || !form.workDate || !form.startTime || !form.endTime) return;
    const onSuccess = () => { setForm(current => ({ ...current, description: "" })); setEditing(null); };
    if (editing) updateMutation.mutate({ projectId: form.projectId, workDate: form.workDate, startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00`, description: form.description || undefined, rowVersion: editing.rowVersion }, { onSuccess });
    else createMutation.mutate({ projectId: form.projectId, workDate: form.workDate, startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00`, description: form.description || undefined }, { onSuccess });
  };

  const beginEdit = (entry: (typeof entries)[number]) => {
    setEditing({ id: entry.id, rowVersion: entry.rowVersion });
    setForm({ projectId: entry.projectId, workDate: entry.workDate.slice(0, 10), startTime: entry.startTime.slice(0, 5), endTime: entry.endTime.slice(0, 5), description: entry.description ?? "" });
  };

  const cancelEdit = () => { setEditing(null); setForm(current => ({ ...current, description: "" })); };

  return (
    <div className="space-y-5">
      <PageHeader title={t("title")} description={t("description")} actions={<Button size="sm" type="submit" form="time-entry-form"><Plus className="me-2 h-4 w-4" />{t("addEntry")}</Button>} />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard title={t("actual")} value={`${formatHours(summary?.actualHours ?? 0, locale)} ${t("hours")}`} icon={<Clock3 className="h-4 w-4" />} />
        <MetricCard title={t("planned")} value={`${formatHours(summary?.plannedHours ?? 0, locale)} ${t("hours")}`} icon={<CalendarDays className="h-4 w-4" />} />
        <MetricCard title={t("variance")} value={`${formatHours(summary?.workVarianceHours ?? 0, locale)} ${t("hours")}`} icon={<Clock3 className="h-4 w-4" />} />
        <MetricCard title={t("utilization")} value={`${formatHours(summary?.actualUtilization ?? 0, locale)}%`} icon={<CalendarDays className="h-4 w-4" />} />
      </div>
      <Card>
        <CardHeader><CardTitle className="text-base">{editing ? t("editEntry") : t("newEntry")}</CardTitle></CardHeader>
        <CardContent>
          <form id="time-entry-form" onSubmit={submit} className="grid gap-3 md:grid-cols-6">
            <label className="space-y-1 text-sm md:col-span-2"><span>{t("project")}</span><select className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.projectId} onChange={event => setForm({ ...form, projectId: event.target.value })} required><option value="">{t("selectProject")}</option>{projects.map(project => <option key={project.id} value={project.id}>{project.code} — {project.name}</option>)}</select></label>
            <label className="space-y-1 text-sm"><span>{t("workDate")}</span><Input type="date" value={form.workDate} onChange={event => setForm({ ...form, workDate: event.target.value })} required /></label>
            <label className="space-y-1 text-sm"><span>{t("start")}</span><Input type="time" value={form.startTime} onChange={event => setForm({ ...form, startTime: event.target.value })} required /></label>
            <label className="space-y-1 text-sm"><span>{t("end")}</span><Input type="time" value={form.endTime} onChange={event => setForm({ ...form, endTime: event.target.value })} required /></label>
            <label className="space-y-1 text-sm md:col-span-6"><span>{t("descriptionField")}</span><Textarea className="min-h-16" value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} placeholder={t("descriptionPlaceholder")} /></label>
          </form>
          {(createMutation.isError || updateMutation.isError) && <p className="mt-3 text-sm text-destructive">{t("saveError")}</p>}
          {editing && <Button type="button" variant="ghost" size="sm" className="mt-2" onClick={cancelEdit}>{t("cancel")}</Button>}
        </CardContent>
      </Card>
      <Card>
        <CardHeader><CardTitle className="text-base">{t("entries")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {entriesQuery.isLoading ? <p className="p-5 text-sm text-muted-foreground">{t("loading")}</p> : entriesQuery.isError ? <p className="p-5 text-sm text-destructive">{t("loadError")}</p> : entries.length === 0 ? <p className="p-5 text-sm text-muted-foreground">{t("noEntries")}</p> : <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b bg-muted/40 text-center text-xs text-muted-foreground"><th className="px-4 py-3">{t("project")}</th><th className="px-4 py-3">{t("workDate")}</th><th className="px-4 py-3">{t("time")}</th><th className="px-4 py-3">{t("actual")}</th><th className="px-4 py-3">{t("descriptionField")}</th><th className="px-4 py-3">{t("actions")}</th></tr></thead><tbody>{entries.map(entry => <tr key={entry.id} className="border-b last:border-0"><td className="px-4 py-3 font-medium">{entry.projectName}</td><td className="px-4 py-3">{entry.workDate.slice(0, 10)}</td><td className="px-4 py-3">{entry.startTime.slice(0, 5)} — {entry.endTime.slice(0, 5)}</td><td className="px-4 py-3">{formatHours(entry.actualHours, locale)} {t("hours")}</td><td className="max-w-xs truncate px-4 py-3 text-muted-foreground">{entry.description || "—"}</td><td className="px-4 py-3 text-center"><Button type="button" variant="ghost" size="icon" aria-label={t("editEntry")} onClick={() => beginEdit(entry)}><Pencil className="h-4 w-4" /></Button><Button type="button" variant="ghost" size="icon" aria-label={t("delete")} disabled={deleteMutation.isPending} onClick={() => deleteMutation.mutate(entry.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button></td></tr>)}</tbody></table></div>}
        </CardContent>
      </Card>
    </div>
  );
}

function MetricCard({ title, value, icon }: { title: string; value: string; icon: ReactNode }) {
  return <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-xs font-medium text-muted-foreground">{title}</CardTitle><span className="text-primary">{icon}</span></CardHeader><CardContent><div className="text-2xl font-semibold tabular-nums">{value}</div></CardContent></Card>;
}
