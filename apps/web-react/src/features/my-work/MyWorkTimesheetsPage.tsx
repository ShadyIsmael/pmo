import * as React from "react";
import { useMemo, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { CalendarDays, Clock3, Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { useProjects } from "@features/projects/api/projects";
import { useTimeEntries, useTimeEntrySummary, useCreateTimeEntry, useUpdateTimeEntry, useDeleteTimeEntry } from "./api/myWork";

const toDateInput = (date: Date) => date.toISOString().slice(0, 10);
const formatHours = (value: number, locale: string) => new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(value);

export function MyWorkTimesheetsPage() {
  const { t, locale } = useI18n();
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
      <PageHeader title={t("timeTracking.title")} description={t("timeTracking.description")} actions={<Button size="sm" type="submit" form="time-entry-form"><Plus className="me-2 h-4 w-4" />{t("timeTracking.addEntry")}</Button>} />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard title={t("timeTracking.actual")} value={`${formatHours(summary?.actualHours ?? 0, locale!)} ${t("timeTracking.hours")}`} icon={<Clock3 className="h-4 w-4" />} />
        <MetricCard title={t("timeTracking.planned")} value={`${formatHours(summary?.plannedHours ?? 0, locale!)} ${t("timeTracking.hours")}`} icon={<CalendarDays className="h-4 w-4" />} />
        <MetricCard title={t("timeTracking.variance")} value={`${formatHours(summary?.workVarianceHours ?? 0, locale!)} ${t("timeTracking.hours")}`} icon={<Clock3 className="h-4 w-4" />} />
        <MetricCard title={t("timeTracking.utilization")} value={`${formatHours(summary?.actualUtilization ?? 0, locale!)}%`} icon={<CalendarDays className="h-4 w-4" />} />
      </div>
      {summary?.byTeam?.length ? (
        <Card>
          <CardHeader><CardTitle className="text-base">{t("timeTracking.teamActualWork")}</CardTitle></CardHeader>
          <CardContent className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {summary.byTeam.map((team) => (
              <div key={team.id ?? team.label} className="flex items-center justify-between rounded-md border border-border/60 px-3 py-2 text-sm">
                <span className="truncate font-medium" title={team.label}>{team.label}</span>
                <span className="ms-3 shrink-0 tabular-nums text-muted-foreground">{formatHours(team.actualHours, locale!)} {t("timeTracking.hours")}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}
      <Card>
        <CardHeader><CardTitle className="text-base">{editing ? t("timeTracking.editEntry") : t("timeTracking.newEntry")}</CardTitle></CardHeader>
        <CardContent>
          <form id="time-entry-form" onSubmit={submit} className="grid gap-3 md:grid-cols-6">
            <label className="space-y-1 text-sm md:col-span-2"><span>{t("timeTracking.project")}</span><select className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={form.projectId} onChange={event => setForm({ ...form, projectId: event.target.value })} required><option value="">{t("timeTracking.selectProject")}</option>{projects.map(project => <option key={project.id} value={project.id}>{project.code} — {project.name}</option>)}</select></label>
            <label className="space-y-1 text-sm"><span>{t("timeTracking.workDate")}</span><Input type="date" value={form.workDate} onChange={event => setForm({ ...form, workDate: event.target.value })} required /></label>
            <label className="space-y-1 text-sm"><span>{t("timeTracking.start")}</span><Input type="time" value={form.startTime} onChange={event => setForm({ ...form, startTime: event.target.value })} required /></label>
            <label className="space-y-1 text-sm"><span>{t("timeTracking.end")}</span><Input type="time" value={form.endTime} onChange={event => setForm({ ...form, endTime: event.target.value })} required /></label>
            <label className="space-y-1 text-sm md:col-span-6"><span>{t("timeTracking.descriptionField")}</span><Textarea className="min-h-16" value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} placeholder={t("timeTracking.descriptionPlaceholder")} /></label>
          </form>
          {(createMutation.isError || updateMutation.isError) && <p className="mt-3 text-sm text-destructive">{t("timeTracking.saveError")}</p>}
          {editing && <Button type="button" variant="ghost" size="sm" className="mt-2" onClick={cancelEdit}>{t("common.cancel")}</Button>}
        </CardContent>
      </Card>
      <Card>
        <CardHeader><CardTitle className="text-base">{t("timeTracking.entries")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {entriesQuery.isLoading ? <p className="p-5 text-sm text-muted-foreground">{t("common.loading")}</p> : entriesQuery.isError ? <p className="p-5 text-sm text-destructive">{t("common.errorLoading")}</p> : entries.length === 0 ? <p className="p-5 text-sm text-muted-foreground">{t("timeTracking.noEntries")}</p> : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/40 text-center text-xs text-muted-foreground">
                    <th className="px-4 py-3">{t("timeTracking.project")}</th>
                    <th className="px-4 py-3">{t("timeTracking.workDate")}</th>
                    <th className="px-4 py-3">{t("timeTracking.time")}</th>
                    <th className="px-4 py-3">{t("timeTracking.actual")}</th>
                    <th className="px-4 py-3">{t("timeTracking.descriptionField")}</th>
                    <th className="px-4 py-3">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {entries.map((entry) => (
                    <tr key={entry.id} className="border-b last:border-0">
                      <td className="px-4 py-3 font-medium">{entry.projectName}</td>
                      <td className="px-4 py-3">{entry.workDate.slice(0, 10)}</td>
                      <td className="px-4 py-3">{entry.startTime.slice(0, 5)} — {entry.endTime.slice(0, 5)}</td>
                      <td className="px-4 py-3">{formatHours(entry.actualHours, locale!)} {t("timeTracking.hours")}</td>
                      <td className="max-w-xs truncate px-4 py-3 text-muted-foreground">{entry.description || "—"}</td>
                      <td className="px-4 py-3 text-center">
                        <Button type="button" variant="ghost" size="icon" aria-label={t("timeTracking.editEntry")} onClick={() => beginEdit(entry)}><Pencil className="h-4 w-4" /></Button>
                        <Button type="button" variant="ghost" size="icon" aria-label={t("common.delete")} disabled={deleteMutation.isPending} onClick={() => deleteMutation.mutate(entry.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function MetricCard({ title, value, icon }: { title: string; value: string; icon: ReactNode }) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-xs font-medium text-muted-foreground">{title}</CardTitle>
        <span className="text-primary">{icon}</span>
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-semibold tabular-nums">{value}</div>
      </CardContent>
    </Card>
  );
}