import * as React from "react";
import { Pencil } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { useI18n } from "@i18n/messages";
import type { AllocationDto } from "./api/allocations";

interface ResourceGanttViewProps {
  data?: AllocationDto[];
  isLoading: boolean;
  startDate: Date;
  endDate: Date;
  canEdit: boolean;
  onEdit: (allocation: AllocationDto) => void;
}

function dateValue(value: string): number {
  const parsed = new Date(value).getTime();
  return Number.isFinite(parsed) ? parsed : 0;
}

export function ResourceGanttView({ data = [], isLoading, startDate, endDate, canEdit, onEdit }: ResourceGanttViewProps) {
  const { t, formatNumber, formatDate } = useI18n();
  const start = startDate.getTime();
  const end = Math.max(endDate.getTime(), start + 86_400_000);
  const range = end - start;
  const grouped = React.useMemo(() => {
    const groups = new Map<string, AllocationDto[]>();
    data.forEach((allocation) => {
      const key = `${allocation.resourceId}:${allocation.resourceName}`;
      const list = groups.get(key) ?? [];
      list.push(allocation);
      groups.set(key, list);
    });
    return [...groups.values()].sort((a, b) => (a[0]?.resourceName ?? "").localeCompare(b[0]?.resourceName ?? ""));
  }, [data]);

  if (isLoading) return <p className="py-10 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (grouped.length === 0) return <p className="py-10 text-center text-sm text-muted-foreground">{t("resources.gantt.noData")}</p>;

  return <div className="space-y-3 overflow-x-auto">
    <div className="min-w-[760px] rounded-lg border">
      <div className="grid grid-cols-[12rem_1fr] border-b bg-muted/30 px-3 py-2 text-xs font-medium text-muted-foreground"><span>{t("resources.gantt.resource")}</span><span>{formatDate(startDate)} – {formatDate(endDate)}</span></div>
      {grouped.map((allocations) => <div key={`${allocations[0]?.resourceId}:${allocations[0]?.resourceName}`} className="grid min-h-20 grid-cols-[12rem_1fr] border-b px-3 py-3 last:border-b-0"><div className="min-w-0 pe-3"><p className="truncate text-sm font-medium">{allocations[0]?.resourceName}</p><p className="truncate text-xs text-muted-foreground">{allocations[0]?.resourceJobTitle || t("resources.gantt.unnamed")}</p></div><div className="relative min-h-12 rounded bg-muted/30">{allocations.map((allocation) => { const left = Math.max(0, Math.min(100, ((dateValue(allocation.startDate) - start) / range) * 100)); const right = Math.max(0, Math.min(100, ((dateValue(allocation.endDate) - start) / range) * 100)); const width = Math.max(4, right - left); const label = `${allocation.projectName || t("resources.gantt.unnamedProject")} · ${formatNumber(allocation.allocationPercentage)}%`; return canEdit ? <button key={allocation.id} type="button" title={label} onClick={() => onEdit(allocation)} className="absolute top-2 flex h-8 min-w-8 items-center gap-1 overflow-hidden rounded bg-primary px-2 text-start text-[11px] text-primary-foreground shadow-sm hover:bg-primary/90" style={{ left: `${left}%`, width: `${width}%` }}><span className="truncate">{label}</span><Pencil className="h-3 w-3 shrink-0" /></button> : <div key={allocation.id} title={label} className="absolute top-2 h-8 min-w-8 overflow-hidden rounded bg-primary px-2 py-2 text-[11px] text-primary-foreground shadow-sm" style={{ left: `${left}%`, width: `${width}%` }}><span className="block truncate">{label}</span></div>; })}</div></div>)}
    </div>
    <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">{["Confirmed", "Proposed", "Tentative", "Cancelled"].map((status) => <Badge key={status} status={status}>{t(`resources.gantt.${status.toLowerCase()}`)}</Badge>)}</div>
  </div>;
}
