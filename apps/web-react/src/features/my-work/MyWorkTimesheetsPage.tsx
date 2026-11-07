import * as React from "react";
import { useMemo, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { CalendarDays, Clock3, Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { getApiFieldError } from "@api/client";
import { formatNumberValue } from "@lib/formatting/number";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import { useProjects } from "@features/projects/api/projects";
import { useTimeEntries, useTimeEntrySummary, useCreateTimeEntry, useUpdateTimeEntry, useDeleteTimeEntry } from "./api/myWork";
import { MyWorkTabs } from "./MyWorkTabs";

const toDateInput = (date: Date) => date.toISOString().slice(0, 10);
const formatHours = (value: number, locale: string) => formatNumberValue(value, locale, { maximumFractionDigits: 2 });

export function MyWorkTimesheetsPage() {
  const { t, locale } = useI18n();
  const { user, status } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const permissions = user?.permissions ?? [];
  const canView = hasAnyPermission(permissions, [
    Permissions.TimeTracking.ViewOwn,
    Permissions.TimeTracking.ViewTeam,
  ]);
  const canViewOwn = hasPermission(permissions, Permissions.TimeTracking.ViewOwn);
  const canManageOwn = canViewOwn && hasPermission(permissions, Permissions.TimeTracking.ManageOwn) && canMutate && !effectiveReadOnly;
  const canViewTeam = hasPermission(permissions, Permissions.TimeTracking.ViewTeam);
  // The API returns both personal and in-scope team entries for ViewTeam. Without ManageTeam
  // there is no reliable client-side ownership field, so keep row mutations hidden in that mix.
  const canManageRows = canManageOwn && (!canViewTeam || hasPermission(permissions, Permissions.TimeTracking.ManageTeam));
  const today = useMemo(() => new Date(), []);
  const from = useMemo(() => { const date = new Date(today); date.setDate(date.getDate() - 6); return toDateInput(date); }, [today]);
  const to = toDateInput(today);
  const defaultForm = useMemo(() => ({ projectId: "", workDate: to, startTime: "09:00", endTime: "17:00", description: "" }), [to]);
  const [form, setForm] = useState(defaultForm);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [discardConfirmOpen, setDiscardConfirmOpen] = useState(false);
  const initialFormRef = React.useRef(defaultForm);
  const [editing, setEditing] = useState<{ id: string; rowVersion: number[] } | null>(null);
  const projectsQuery = useProjects({ page: 1, pageSize: 200 }, canManageOwn);
  const entriesQuery = useTimeEntries({ page: 1, pageSize: 100, from, to }, canView);
  const summaryQuery = useTimeEntrySummary({ from, to }, canView);
  const createMutation = useCreateTimeEntry();
  const deleteMutation = useDeleteTimeEntry();
  const updateMutation = useUpdateTimeEntry(editing?.id ?? "");
  const projects = canManageOwn ? projectsQuery.data?.items ?? [] : [];
  const entries = canView ? entriesQuery.data?.items ?? [] : [];
  const summary = canView ? summaryQuery.data : undefined;
  const isSaving = createMutation.isPending || updateMutation.isPending;
  const hasUnsavedChanges = JSON.stringify(form) !== JSON.stringify(initialFormRef.current);
  const saveMutationError = editing ? updateMutation.error : createMutation.error;
  const fieldErrorNames = ["projectId", "workDate", "startTime", "endTime", "description"] as const;
  const hasMappedFieldErrors = fieldErrorNames.some((field) => Boolean(getApiFieldError(saveMutationError, field)));

  const finishClose = () => {
    setSheetOpen(false);
    setDiscardConfirmOpen(false);
    setEditing(null);
    setForm(defaultForm);
    initialFormRef.current = defaultForm;
  };

  const requestSheetOpenChange = (open: boolean) => {
    if (open) {
      setSheetOpen(true);
      return;
    }
    if (isSaving) return;
    if (hasUnsavedChanges) {
      setDiscardConfirmOpen(true);
      return;
    }
    finishClose();
  };

  const updateForm = (patch: Partial<typeof defaultForm>) => {
    createMutation.reset();
    updateMutation.reset();
    setForm((current) => ({ ...current, ...patch }));
  };

  const openCreateSheet = () => {
    createMutation.reset();
    updateMutation.reset();
    setEditing(null);
    setForm(defaultForm);
    initialFormRef.current = defaultForm;
    setSheetOpen(true);
  };

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!form.projectId || !form.workDate || !form.startTime || !form.endTime) return;
    const onSuccess = () => finishClose();
    if (editing) updateMutation.mutate({ projectId: form.projectId, workDate: form.workDate, startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00`, description: form.description || undefined, rowVersion: editing.rowVersion }, { onSuccess });
    else createMutation.mutate({ projectId: form.projectId, workDate: form.workDate, startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00`, description: form.description || undefined }, { onSuccess });
  };

  const beginEdit = (entry: (typeof entries)[number]) => {
    const editForm = { projectId: entry.projectId, workDate: entry.workDate.slice(0, 10), startTime: entry.startTime.slice(0, 5), endTime: entry.endTime.slice(0, 5), description: entry.description ?? "" };
    createMutation.reset();
    updateMutation.reset();
    setEditing({ id: entry.id, rowVersion: entry.rowVersion });
    setForm(editForm);
    initialFormRef.current = editForm;
    setSheetOpen(true);
  };

  return (
    <div className="space-y-5">
      <PageHeader title={t("timeTracking.title")} description={t("timeTracking.description")} actions={canManageOwn ? <Button type="button" size="sm" onClick={openCreateSheet}><Plus className="me-2 h-4 w-4" />{t("timeTracking.addEntry")}</Button> : undefined} />
      <MyWorkTabs />
      {!canView ? status === "loading" ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : <p role="alert" className="rounded-md border border-border/70 p-4 text-sm text-muted-foreground">{t("timeTracking.permissionRequired")}</p> : <>
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
                    {canManageRows ? <th className="px-4 py-3">{t("common.actions")}</th> : null}
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
                      {canManageRows ? <td className="px-4 py-3 text-center">
                        <Button type="button" variant="ghost" size="icon" aria-label={t("timeTracking.editEntry")} onClick={() => beginEdit(entry)}><Pencil className="h-4 w-4" /></Button>
                        <Button type="button" variant="ghost" size="icon" aria-label={t("common.delete")} disabled={deleteMutation.isPending} onClick={() => deleteMutation.mutate(entry.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button>
                      </td> : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
      {canManageOwn ? <>
        <SideSheet
          open={sheetOpen}
          onOpenChange={requestSheetOpenChange}
          title={editing ? t("timeTracking.editEntry") : t("timeTracking.newEntry")}
          size="md"
          closeLabel={t("common.cancel")}
        >
          <form onSubmit={submit} className="flex min-h-0 flex-1 flex-col">
            <SideSheetBody className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="time-entry-project">{t("timeTracking.project")}</Label>
                <select
                  id="time-entry-project"
                  className="input-field w-full"
                  aria-invalid={projectsQuery.isError || Boolean(getApiFieldError(saveMutationError, "projectId"))}
                  aria-describedby={[
                    getApiFieldError(saveMutationError, "projectId") ? "time-entry-project-api-error" : null,
                    projectsQuery.isError ? "time-entry-project-error" : null,
                  ].filter(Boolean).join(" ") || undefined}
                  value={form.projectId}
                  onChange={(event) => updateForm({ projectId: event.target.value })}
                  disabled={projectsQuery.isLoading || isSaving}
                  required
                >
                  <option value="">{projectsQuery.isLoading ? t("common.loading") : t("timeTracking.selectProject")}</option>
                  {projects.map((project) => <option key={project.id} value={project.id}>{project.code} — {project.name}</option>)}
                </select>
                <ApiFieldError id="time-entry-project-api-error" error={saveMutationError} fieldName="projectId" />
                {projectsQuery.isError ? <p id="time-entry-project-error" role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p> : null}
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="time-entry-date">{t("timeTracking.workDate")}</Label>
                <Input id="time-entry-date" type="date" value={form.workDate} onChange={(event) => updateForm({ workDate: event.target.value })} disabled={isSaving} required aria-invalid={Boolean(getApiFieldError(saveMutationError, "workDate"))} aria-describedby={getApiFieldError(saveMutationError, "workDate") ? "time-entry-date-api-error" : undefined} />
                <ApiFieldError id="time-entry-date-api-error" error={saveMutationError} fieldName="workDate" />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="time-entry-start">{t("timeTracking.start")}</Label>
                  <Input id="time-entry-start" type="time" value={form.startTime} onChange={(event) => updateForm({ startTime: event.target.value })} disabled={isSaving} required aria-invalid={Boolean(getApiFieldError(saveMutationError, "startTime"))} aria-describedby={getApiFieldError(saveMutationError, "startTime") ? "time-entry-start-api-error" : undefined} />
                  <ApiFieldError id="time-entry-start-api-error" error={saveMutationError} fieldName="startTime" />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="time-entry-end">{t("timeTracking.end")}</Label>
                  <Input id="time-entry-end" type="time" value={form.endTime} onChange={(event) => updateForm({ endTime: event.target.value })} disabled={isSaving} required aria-invalid={Boolean(getApiFieldError(saveMutationError, "endTime"))} aria-describedby={getApiFieldError(saveMutationError, "endTime") ? "time-entry-end-api-error" : undefined} />
                  <ApiFieldError id="time-entry-end-api-error" error={saveMutationError} fieldName="endTime" />
                </div>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="time-entry-description">{t("timeTracking.descriptionField")}</Label>
                <Textarea id="time-entry-description" className="min-h-16" value={form.description} onChange={(event) => updateForm({ description: event.target.value })} placeholder={t("timeTracking.descriptionPlaceholder")} disabled={isSaving} aria-invalid={Boolean(getApiFieldError(saveMutationError, "description"))} aria-describedby={getApiFieldError(saveMutationError, "description") ? "time-entry-description-api-error" : undefined} />
                <ApiFieldError id="time-entry-description-api-error" error={saveMutationError} fieldName="description" />
              </div>
              {(createMutation.isError || updateMutation.isError) && !hasMappedFieldErrors ? <p role="alert" className="text-sm text-destructive">{t("timeTracking.saveError")}</p> : null}
            </SideSheetBody>
            <SideSheetFooter>
              <Button type="button" variant="outline" disabled={isSaving} onClick={() => requestSheetOpenChange(false)}>{t("common.cancel")}</Button>
              <Button type="submit" disabled={isSaving || projectsQuery.isLoading || projectsQuery.isError}>
                {isSaving ? t("common.saving") : t("common.save")}
              </Button>
            </SideSheetFooter>
          </form>
        </SideSheet>
        <ConfirmDialog
          open={discardConfirmOpen}
          onOpenChange={setDiscardConfirmOpen}
          title={t("timeTracking.unsavedTitle")}
          description={t("timeTracking.unsavedDescription")}
          confirmLabel={t("timeTracking.discardChanges")}
          destructive
          onConfirm={finishClose}
        />
      </> : null}
      </>}
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
