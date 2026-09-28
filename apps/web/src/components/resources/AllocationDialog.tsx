"use client";

import { useReducer, useEffect } from "react";
import { useTranslations } from "next-intl";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { AlertTriangle, Loader2 } from "lucide-react";
import { useCreateAllocation, useUpdateAllocation, useAllocationConflicts, AllocationDto, CreateAllocationDto, UpdateAllocationDto } from "@/hooks/resources/useAllocations";
import { useResources } from "@/hooks/resources/useResources";

interface AllocationFormState {
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
  submitting: boolean;
}

type AllocationFormAction =
  | { type: "SET_FIELD"; field: keyof AllocationFormState; value: string | boolean }
  | { type: "SYNC_FROM_ALLOCATION"; allocation: AllocationDto }
  | { type: "SUBMIT_START" }
  | { type: "SUBMIT_END" };

function allocationReducer(state: AllocationFormState, action: AllocationFormAction): AllocationFormState {
  switch (action.type) {
    case "SET_FIELD":
      return { ...state, [action.field]: action.value };
    case "SYNC_FROM_ALLOCATION":
      return {
        ...state,
        resourceId: action.allocation.resourceId,
        projectId: action.allocation.projectId ?? "",
        projectName: action.allocation.projectName ?? "",
        workItemId: action.allocation.workItemId ?? "",
        role: action.allocation.role,
        startDate: action.allocation.startDate?.split("T")[0] ?? "",
        endDate: action.allocation.endDate?.split("T")[0] ?? "",
        allocationPercentage: String(action.allocation.allocationPercentage),
        hoursPerWeek: String(action.allocation.hoursPerWeek ?? ""),
        status: action.allocation.status,
        isBillable: action.allocation.isBillable,
        notes: action.allocation.notes ?? "",
      };
    case "SUBMIT_START":
      return { ...state, submitting: true };
    case "SUBMIT_END":
      return { ...state, submitting: false };
  }
}

function initialState(allocation?: AllocationDto | null): AllocationFormState {
  return {
    resourceId: allocation?.resourceId ?? "",
    projectId: allocation?.projectId ?? "",
    projectName: allocation?.projectName ?? "",
    workItemId: allocation?.workItemId ?? "",
    role: allocation?.role ?? "",
    startDate: allocation?.startDate?.split("T")[0] ?? "",
    endDate: allocation?.endDate?.split("T")[0] ?? "",
    allocationPercentage: String(allocation?.allocationPercentage ?? ""),
    hoursPerWeek: String(allocation?.hoursPerWeek ?? ""),
    status: allocation?.status ?? "Confirmed",
    isBillable: allocation?.isBillable ?? true,
    notes: allocation?.notes ?? "",
    submitting: false,
  };
}

interface AllocationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  allocation?: AllocationDto | null;
}

export function AllocationDialog({ open, onOpenChange, allocation }: AllocationDialogProps) {
  const t = useTranslations("resources.allocation");
  const tc = useTranslations("common");
  const isEdit = !!allocation;
  const { data: resourcesData } = useResources({ pageSize: 100 });
  const createAllocation = useCreateAllocation();
  const updateAllocation = useUpdateAllocation(allocation?.id ?? "");
  const resources = resourcesData?.items ?? [];

  const [form, dispatch] = useReducer(allocationReducer, allocation, initialState);

  useEffect(() => {
    if (allocation) dispatch({ type: "SYNC_FROM_ALLOCATION", allocation });
  }, [allocation]);

  const { data: conflicts } = useAllocationConflicts(
    form.resourceId,
    form.startDate || "2026-01-01",
    form.endDate || "2026-12-31",
    allocation?.id,
  );
  const hasConflicts = conflicts && conflicts.length > 0;

  const handleSubmit = async () => {
    dispatch({ type: "SUBMIT_START" });
    try {
      if (isEdit && allocation) {
        const dto: UpdateAllocationDto = {
          ...(form.projectId ? { projectId: form.projectId } : { projectId: null as unknown as undefined }),
          projectName: form.projectName || undefined,
          ...(form.workItemId ? { workItemId: form.workItemId } : {}),
          role: form.role || undefined,
          allocationPercentage: Number(form.allocationPercentage) || undefined,
          hoursPerWeek: Number(form.hoursPerWeek) || undefined,
          startDate: form.startDate ? new Date(form.startDate).toISOString() : undefined,
          endDate: form.endDate ? new Date(form.endDate).toISOString() : undefined,
          status: form.status || undefined,
          isBillable: form.isBillable,
          notes: form.notes || undefined,
        };
        await updateAllocation.mutateAsync(dto);
      } else {
        const dto: CreateAllocationDto = {
          resourceId: form.resourceId,
          ...(form.projectId ? { projectId: form.projectId } : {}),
          projectName: form.projectName || undefined,
          ...(form.workItemId ? { workItemId: form.workItemId } : {}),
          role: form.role,
          allocationPercentage: Number(form.allocationPercentage),
          ...(form.hoursPerWeek ? { hoursPerWeek: Number(form.hoursPerWeek) } : {}),
          startDate: new Date(form.startDate).toISOString(),
          endDate: new Date(form.endDate).toISOString(),
          status: form.status,
          isBillable: form.isBillable,
          notes: form.notes || undefined,
        };
        await createAllocation.mutateAsync(dto);
      }
      onOpenChange(false);
    } catch (err) {
      console.error("Failed to save allocation", err);
    } finally {
      dispatch({ type: "SUBMIT_END" });
    }
  };

  const set = (field: keyof AllocationFormState) => (value: string | boolean) => dispatch({ type: "SET_FIELD", field, value });

  return (
    <FormSheet
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t("editTitle") : t("createTitle")}
    >

      <FormSheetBody>
        <div className="space-y-5">
          <div className="space-y-2">
            <Label>{t("resource")}</Label>
            <Select value={form.resourceId} onValueChange={set("resourceId")} disabled={isEdit}>
              <SelectTrigger><SelectValue placeholder={t("selectResource")} /></SelectTrigger>
              <SelectContent>
                {resources.map((r) => (
                  <SelectItem key={r.id} value={r.id}>{r.name} — {r.jobTitle ?? ""}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("project")}</Label>
              <Input placeholder={t("placeholderProjectId")} value={form.projectId} onChange={(e) => set("projectId")(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("projectName")}</Label>
              <Input placeholder={t("placeholderProjectName")} value={form.projectName} onChange={(e) => set("projectName")(e.target.value)} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t("workItem")}</Label>
            <Input placeholder={t("placeholderWorkItem")} value={form.workItemId} onChange={(e) => set("workItemId")(e.target.value)} />
          </div>

          <div className="space-y-2">
            <Label>{t("role")}</Label>
            <Input placeholder={t("placeholderRole")} value={form.role} onChange={(e) => set("role")(e.target.value)} />
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("startDate")}</Label>
              <Input type="date" value={form.startDate} onChange={(e) => set("startDate")(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("endDate")}</Label>
              <Input type="date" value={form.endDate} onChange={(e) => set("endDate")(e.target.value)} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("allocationPercent")}</Label>
              <Input type="number" min="1" max="100" value={form.allocationPercentage} onChange={(e) => set("allocationPercentage")(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("hoursPerWeek")}</Label>
              <Input type="number" min="1" max="168" value={form.hoursPerWeek} onChange={(e) => set("hoursPerWeek")(e.target.value)} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t("status")}</Label>
            <Select value={form.status} onValueChange={set("status")}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Confirmed">{t("confirmed")}</SelectItem>
                <SelectItem value="Proposed">{t("proposed")}</SelectItem>
                <SelectItem value="Tentative">{t("tentative")}</SelectItem>
                <SelectItem value="Cancelled">{t("cancelled")}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="flex items-center gap-2">
            <Switch checked={form.isBillable} onCheckedChange={set("isBillable")} id="billable" />
            <Label htmlFor="billable">{t("billable")}</Label>
          </div>

          <div className="space-y-2">
            <Label>{t("notes")}</Label>
            <Textarea placeholder={t("placeholderNotes")} value={form.notes} onChange={(e) => set("notes")(e.target.value)} />
          </div>

          {hasConflicts && (
            <div className="rounded-md border border-status-amber/30 bg-status-amber-light p-3">
              <div className="flex items-center gap-2 text-sm font-medium text-status-amber">
                <AlertTriangle className="h-4 w-4" />
                {t("conflictsDetected")}
              </div>
              <div className="mt-2 space-y-1">
                {conflicts.map((c, i) => (
                  <div key={i} className="flex items-center justify-between text-xs">
                    <span>{c.existingProjectName} — {c.existingRole} ({c.existingPercentage}%)</span>
                    <Badge status={c.severity} className="text-[10px]">{c.severity}</Badge>
                  </div>
                ))}
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {t("newTotal")}: {Math.round(
                  (allocation?.allocationPercentage ?? Number(form.allocationPercentage)) +
                    conflicts.reduce((s, c) => s + c.existingPercentage, 0)
                )}%
              </p>
            </div>
          )}
        </div>
        </FormSheetBody>

        <FormSheetFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{tc("cancel")}</Button>
          <Button onClick={handleSubmit} disabled={form.submitting || !form.resourceId || !form.role || !form.startDate || !form.endDate || !form.allocationPercentage}>
            {form.submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isEdit ? t("update") : t("create")}
          </Button>
        </FormSheetFooter>
    </FormSheet>
  );
}
