import * as React from "react";
import { AlertTriangle, Loader2 } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Badge } from "@components/ui/Badge";
import { Checkbox } from "@components/ui/Checkbox";
import { useI18n } from "@i18n/messages";
import { useResources } from "./api/resources";
import { useAllocationConflicts, useCreateAllocation, useUpdateAllocation, type AllocationDto, type CreateAllocationDto, type UpdateAllocationDto } from "./api/allocations";

interface AllocationForm {
  resourceId: string;
  projectId: string;
  projectName: string;
  workItemId: string;
  role: string;
  startDate: string;
  endDate: string;
  allocationPercentage: string;
  hoursPerWeek: string;
  status: string;
  isBillable: boolean;
  notes: string;
}

function formFromAllocation(allocation?: AllocationDto | null): AllocationForm {
  return {
    resourceId: allocation?.resourceId ?? "",
    projectId: allocation?.projectId ?? "",
    projectName: allocation?.projectName ?? "",
    workItemId: allocation?.workItemId ?? "",
    role: allocation?.role ?? "",
    startDate: allocation?.startDate?.slice(0, 10) ?? "",
    endDate: allocation?.endDate?.slice(0, 10) ?? "",
    allocationPercentage: allocation ? String(allocation.allocationPercentage) : "",
    hoursPerWeek: allocation?.hoursPerWeek == null ? "" : String(allocation.hoursPerWeek),
    status: allocation?.status ?? "Confirmed",
    isBillable: allocation?.isBillable ?? true,
    notes: allocation?.notes ?? "",
  };
}

interface AllocationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  allocation?: AllocationDto | null;
}

export function AllocationDialog({ open, onOpenChange, allocation }: AllocationDialogProps) {
  const { t, formatNumber } = useI18n();
  const isEdit = Boolean(allocation);
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true }, open);
  const createAllocation = useCreateAllocation();
  const updateAllocation = useUpdateAllocation(allocation?.id ?? "");
  const [form, setForm] = React.useState<AllocationForm>(() => formFromAllocation(allocation));
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (open) {
      setForm(formFromAllocation(allocation));
      setError(null);
    }
  }, [allocation, open]);

  const conflictCheckEnabled = open && Boolean(form.resourceId && form.startDate && form.endDate);
  const conflictsQuery = useAllocationConflicts(form.resourceId, form.startDate, form.endDate, allocation?.id, conflictCheckEnabled);
  const conflicts = conflictsQuery.data ?? [];
  const resources = (resourcesQuery.data?.items ?? []).map((resource) => ({
    value: resource.id,
    label: `${resource.name}${resource.jobTitle ? ` · ${resource.jobTitle}` : ""}`,
    keywords: [resource.email ?? "", resource.employeeId ?? ""],
  }));

  const set = <K extends keyof AllocationForm>(field: K, value: AllocationForm[K]) => setForm((current) => ({ ...current, [field]: value }));

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    const percentage = Number(form.allocationPercentage);
    const hours = form.hoursPerWeek ? Number(form.hoursPerWeek) : undefined;
    if (!form.resourceId || !form.role || !form.startDate || !form.endDate) { setError(t("resources.allocation.requiredFields")); return; }
    if (!Number.isFinite(percentage) || percentage <= 0 || percentage > 100) { setError(t("resources.allocation.allocationRangeInvalid")); return; }
    if (hours !== undefined && (!Number.isFinite(hours) || hours <= 0 || hours > 168)) { setError(t("resources.allocation.hoursRangeInvalid")); return; }
    if (form.endDate < form.startDate) { setError(t("resources.allocation.endDateAfterStartDate")); return; }
    try {
      if (allocation) {
        const data: UpdateAllocationDto = {
          projectId: form.projectId || undefined,
          projectName: form.projectName || undefined,
          workItemId: form.workItemId || undefined,
          role: form.role,
          allocationPercentage: percentage,
          hoursPerWeek: hours,
          startDate: new Date(`${form.startDate}T00:00:00`).toISOString(),
          endDate: new Date(`${form.endDate}T00:00:00`).toISOString(),
          status: form.status,
          isBillable: form.isBillable,
          notes: form.notes || undefined,
        };
        await updateAllocation.mutateAsync(data);
      } else {
        const data: CreateAllocationDto = {
          resourceId: form.resourceId,
          projectId: form.projectId || undefined,
          projectName: form.projectName || undefined,
          workItemId: form.workItemId || undefined,
          role: form.role,
          allocationPercentage: percentage,
          hoursPerWeek: hours,
          startDate: new Date(`${form.startDate}T00:00:00`).toISOString(),
          endDate: new Date(`${form.endDate}T00:00:00`).toISOString(),
          status: form.status,
          isBillable: form.isBillable,
          notes: form.notes || undefined,
        };
        await createAllocation.mutateAsync(data);
      }
      onOpenChange(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("resources.allocation.saveError"));
    }
  };

  const saving = createAllocation.isPending || updateAllocation.isPending;
  return <SideSheet open={open} onOpenChange={onOpenChange} title={t(isEdit ? "resources.allocation.editTitle" : "resources.allocation.createTitle")} size="md">
    <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody className="space-y-4">
        <div><Label>{t("resources.allocation.resource")}</Label><SearchableSelect options={resources} value={form.resourceId} onValueChange={(value) => set("resourceId", value)} placeholder={t("resources.allocation.selectResource")} searchPlaceholder={t("resources.searchPlaceholder")} emptyText={t("common.noResults")} loading={resourcesQuery.isLoading} disabled={isEdit} /></div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2"><div><Label>{t("resources.allocation.project")}</Label><Input value={form.projectId} onChange={(event) => set("projectId", event.target.value)} placeholder={t("resources.allocation.placeholderProjectId")} /></div><div><Label>{t("resources.allocation.projectName")}</Label><Input value={form.projectName} onChange={(event) => set("projectName", event.target.value)} placeholder={t("resources.allocation.placeholderProjectName")} /></div></div>
        <div><Label>{t("resources.allocation.workItem")}</Label><Input value={form.workItemId} onChange={(event) => set("workItemId", event.target.value)} placeholder={t("resources.allocation.placeholderWorkItem")} /></div>
        <div><Label>{t("resources.allocation.role")}</Label><Input value={form.role} onChange={(event) => set("role", event.target.value)} placeholder={t("resources.allocation.placeholderRole")} /></div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2"><div><Label>{t("resources.allocation.startDate")}</Label><Input type="date" value={form.startDate} onChange={(event) => set("startDate", event.target.value)} /></div><div><Label>{t("resources.allocation.endDate")}</Label><Input type="date" value={form.endDate} onChange={(event) => set("endDate", event.target.value)} /></div></div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2"><div><Label>{t("resources.allocation.allocationPercent")}</Label><Input type="number" min="1" max="100" step="0.1" value={form.allocationPercentage} onChange={(event) => set("allocationPercentage", event.target.value)} /></div><div><Label>{t("resources.allocation.hoursPerWeek")}</Label><Input type="number" min="1" max="168" step="0.5" value={form.hoursPerWeek} onChange={(event) => set("hoursPerWeek", event.target.value)} /></div></div>
        <div><Label>{t("resources.allocation.status")}</Label><select className="input-field" value={form.status} onChange={(event) => set("status", event.target.value)}><option value="Confirmed">{t("resources.allocation.confirmed")}</option><option value="Proposed">{t("resources.allocation.proposed")}</option><option value="Tentative">{t("resources.allocation.tentative")}</option><option value="Cancelled">{t("resources.allocation.cancelled")}</option></select></div>
        <div className="flex items-center gap-2"><Checkbox id="allocation-billable" checked={form.isBillable} onCheckedChange={() => set("isBillable", !form.isBillable)} /><Label htmlFor="allocation-billable">{t("resources.allocation.billable")}</Label></div>
        <div><Label>{t("resources.allocation.notes")}</Label><Textarea value={form.notes} onChange={(event) => set("notes", event.target.value)} placeholder={t("resources.allocation.placeholderNotes")} /></div>
        {conflictCheckEnabled && conflictsQuery.isFetching ? <p className="text-xs text-muted-foreground">{t("projects.checkingAllocationConflicts")}</p> : null}
        {conflicts.length > 0 ? <div className="space-y-2 rounded-md border border-warning/40 bg-warning/10 p-3" role="alert"><p className="flex items-center gap-2 text-sm font-medium text-warning"><AlertTriangle className="h-4 w-4" />{t("resources.allocation.conflictsDetected")}</p>{conflicts.map((conflict) => <div key={conflict.id} className="flex items-center justify-between gap-2 text-xs"><span>{conflict.projectName || t("resources.gantt.unnamedProject")} · {conflict.role} ({formatNumber(conflict.allocationPercentage)}%)</span><Badge status={conflict.status}>{conflict.status}</Badge></div>)}<p className="text-xs text-muted-foreground">{t("resources.allocation.newTotal")}: {formatNumber(Number(form.allocationPercentage || 0) + conflicts.reduce((total, conflict) => total + conflict.allocationPercentage, 0))}%</p></div> : null}
        {error ? <p className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p> : null}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("resources.allocation.cancel")}</Button><Button type="submit" disabled={saving || !form.resourceId || !form.role || !form.startDate || !form.endDate || !form.allocationPercentage}>{saving ? <><Loader2 className="me-2 h-4 w-4 animate-spin" />{t("common.saving")}</> : t(isEdit ? "resources.allocation.update" : "resources.allocation.create")}</Button></SideSheetFooter>
    </form>
  </SideSheet>;
}
