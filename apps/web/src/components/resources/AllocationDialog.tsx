"use client";

import { useState, useEffect } from "react";
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

  const [resourceId, setResourceId] = useState(allocation?.resourceId ?? "");
  const [projectId, setProjectId] = useState(allocation?.projectId ?? "");
  const [projectName, setProjectName] = useState(allocation?.projectName ?? "");
  const [workItemId, setWorkItemId] = useState(allocation?.workItemId ?? "");
  const [role, setRole] = useState(allocation?.role ?? "");
  const [startDate, setStartDate] = useState(allocation?.startDate?.split("T")[0] ?? "");
  const [endDate, setEndDate] = useState(allocation?.endDate?.split("T")[0] ?? "");
  const [allocationPercentage, setAllocationPercentage] = useState(String(allocation?.allocationPercentage ?? ""));
  const [hoursPerWeek, setHoursPerWeek] = useState(String(allocation?.hoursPerWeek ?? ""));
  const [status, setStatus] = useState(allocation?.status ?? "Confirmed");
  const [isBillable, setIsBillable] = useState(allocation?.isBillable ?? true);
  const [notes, setNotes] = useState(allocation?.notes ?? "");
  const [showConflicts, setShowConflicts] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (allocation) {
      setResourceId(allocation.resourceId);
      setProjectId(allocation.projectId ?? "");
      setProjectName(allocation.projectName ?? "");
      setWorkItemId(allocation.workItemId ?? "");
      setRole(allocation.role);
      setStartDate(allocation.startDate?.split("T")[0] ?? "");
      setEndDate(allocation.endDate?.split("T")[0] ?? "");
      setAllocationPercentage(String(allocation.allocationPercentage));
      setHoursPerWeek(String(allocation.hoursPerWeek ?? ""));
      setStatus(allocation.status);
      setIsBillable(allocation.isBillable);
      setNotes(allocation.notes ?? "");
    }
  }, [allocation]);

  const { data: conflicts } = useAllocationConflicts(
    resourceId,
    startDate || "2026-01-01",
    endDate || "2026-12-31",
    allocation?.id,
  );
  const hasConflicts = conflicts && conflicts.length > 0;

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      if (isEdit && allocation) {
        const dto: UpdateAllocationDto = {
          ...(projectId ? { projectId } : { projectId: null as unknown as undefined }),
          projectName: projectName || undefined,
          ...(workItemId ? { workItemId } : {}),
          role: role || undefined,
          allocationPercentage: Number(allocationPercentage) || undefined,
          hoursPerWeek: Number(hoursPerWeek) || undefined,
          startDate: startDate ? new Date(startDate).toISOString() : undefined,
          endDate: endDate ? new Date(endDate).toISOString() : undefined,
          status: status || undefined,
          isBillable,
          notes: notes || undefined,
        };
        await updateAllocation.mutateAsync(dto);
      } else {
        const dto: CreateAllocationDto = {
          resourceId,
          ...(projectId ? { projectId } : {}),
          projectName: projectName || undefined,
          ...(workItemId ? { workItemId } : {}),
          role,
          allocationPercentage: Number(allocationPercentage),
          ...(hoursPerWeek ? { hoursPerWeek: Number(hoursPerWeek) } : {}),
          startDate: new Date(startDate).toISOString(),
          endDate: new Date(endDate).toISOString(),
          status,
          isBillable,
          notes: notes || undefined,
        };
        await createAllocation.mutateAsync(dto);
      }
      onOpenChange(false);
    } catch (err) {
      console.error("Failed to save allocation", err);
    } finally {
      setSubmitting(false);
    }
  };

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
            <Select value={resourceId} onValueChange={setResourceId} disabled={isEdit}>
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
              <Input placeholder={t("placeholderProjectId")} value={projectId} onChange={(e) => setProjectId(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("projectName")}</Label>
              <Input placeholder={t("placeholderProjectName")} value={projectName} onChange={(e) => setProjectName(e.target.value)} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t("workItem")}</Label>
            <Input placeholder={t("placeholderWorkItem")} value={workItemId} onChange={(e) => setWorkItemId(e.target.value)} />
          </div>

          <div className="space-y-2">
            <Label>{t("role")}</Label>
            <Input placeholder={t("placeholderRole")} value={role} onChange={(e) => setRole(e.target.value)} />
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("startDate")}</Label>
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("endDate")}</Label>
              <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>{t("allocationPercent")}</Label>
              <Input type="number" min="1" max="100" value={allocationPercentage} onChange={(e) => setAllocationPercentage(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("hoursPerWeek")}</Label>
              <Input type="number" min="1" max="168" value={hoursPerWeek} onChange={(e) => setHoursPerWeek(e.target.value)} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t("status")}</Label>
            <Select value={status} onValueChange={setStatus}>
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
            <Switch checked={isBillable} onCheckedChange={setIsBillable} id="billable" />
            <Label htmlFor="billable">{t("billable")}</Label>
          </div>

          <div className="space-y-2">
            <Label>{t("notes")}</Label>
            <Textarea placeholder={t("placeholderNotes")} value={notes} onChange={(e) => setNotes(e.target.value)} />
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
                    <Badge variant="outline" className="text-[10px]">{c.severity}</Badge>
                  </div>
                ))}
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {t("newTotal")}: {Math.round(
                  (allocation?.allocationPercentage ?? Number(allocationPercentage)) +
                    conflicts.reduce((s, c) => s + c.existingPercentage, 0)
                )}%
              </p>
            </div>
          )}
        </div>
        </FormSheetBody>

        <FormSheetFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{tc("cancel")}</Button>
          <Button onClick={handleSubmit} disabled={submitting || !resourceId || !role || !startDate || !endDate || !allocationPercentage}>
            {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isEdit ? t("update") : t("create")}
          </Button>
        </FormSheetFooter>
    </FormSheet>
  );
}
