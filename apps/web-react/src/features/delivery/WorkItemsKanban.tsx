import * as React from "react";
import { Columns3, GripVertical, List, UserRound } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "@components/ui/Badge";
import type { WorkItemStatusDto } from "@api/workItems";
import type { WorkItemDto } from "@api/workItemsFull";
import { formatDate } from "@lib/utils";
import type { WorkItemListView } from "./workItemFilters";

export function WorkItemViewSwitcher({ view, onChange, label, listLabel, kanbanLabel }: {
  view: WorkItemListView;
  onChange: (view: WorkItemListView) => void;
  label: string;
  listLabel: string;
  kanbanLabel: string;
}) {
  return <div className="flex shrink-0 items-center rounded-md border bg-muted/30 p-1" role="tablist" aria-label={label}>
    <button type="button" role="tab" aria-selected={view === "list"} onClick={() => onChange("list")} className={`inline-flex h-7 items-center gap-1.5 rounded px-2 text-xs font-medium ${view === "list" ? "bg-background shadow-sm" : "text-muted-foreground hover:text-foreground"}`}><List className="h-3.5 w-3.5" />{listLabel}</button>
    <button type="button" role="tab" aria-selected={view === "kanban"} onClick={() => onChange("kanban")} className={`inline-flex h-7 items-center gap-1.5 rounded px-2 text-xs font-medium ${view === "kanban" ? "bg-background shadow-sm" : "text-muted-foreground hover:text-foreground"}`}><Columns3 className="h-3.5 w-3.5" />{kanbanLabel}</button>
  </div>;
}

export function WorkItemsKanban({ items, statuses, locale, canEdit, movingItemId, moveError, onMove, onOpen, t }: {
  items: readonly WorkItemDto[];
  statuses: readonly WorkItemStatusDto[];
  locale: string;
  canEdit: boolean;
  movingItemId?: string;
  moveError?: string;
  onMove: (itemId: string, targetStatus: string) => void;
  onOpen?: (itemId: string) => void;
  t: (key: string) => string;
}) {
  const [draggedItemId, setDraggedItemId] = React.useState<string | null>(null);
  const language = locale === "ar" ? "ar" : "en";
  const activeStatuses = statuses.filter((status) => status.isActive).sort((a, b) => a.displayOrder - b.displayOrder);

  return <div className="space-y-3 p-3 sm:p-4">
    {moveError ? <p role="alert" className="rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{moveError}</p> : null}
    {canEdit ? <p className="flex items-center gap-1.5 text-xs text-muted-foreground"><GripVertical className="h-3.5 w-3.5" />{t("workItems.dragToMove")}</p> : null}
    <div className="overflow-x-auto pb-2">
      <div className="flex min-w-max items-stretch gap-3">
        {statuses.map((status) => {
          const columnItems = items.filter((item) => item.status === status.code);
          return <section key={status.id} className={`flex min-h-80 w-72 flex-col rounded-lg border bg-muted/20 ${status.isActive ? "border-border" : "border-dashed border-muted-foreground/40 opacity-80"}`} onDragOver={(event) => {
            if (canEdit && status.isActive) { event.preventDefault(); event.dataTransfer.dropEffect = "move"; }
          }} onDrop={(event) => {
            if (!canEdit || !status.isActive) return;
            event.preventDefault();
            const itemId = event.dataTransfer.getData("text/plain") || draggedItemId;
            if (itemId) onMove(itemId, status.code);
            setDraggedItemId(null);
          }}>
            <header className="flex items-center justify-between gap-2 border-b px-3 py-2.5">
              <h2 className="flex min-w-0 items-center gap-2 text-sm font-semibold"><span aria-hidden="true" className={`h-2.5 w-2.5 shrink-0 rounded-full ${status.isTerminal ? "bg-success" : "bg-primary"}`} /><span className="truncate">{language === "ar" ? status.nameAr : status.nameEn}</span></h2>
              <span className="rounded-full bg-background px-2 py-0.5 text-[11px] font-semibold tabular-nums text-muted-foreground">{columnItems.length}</span>
            </header>
            <div className="flex flex-1 flex-col gap-2 p-2">
              {columnItems.length === 0 ? <p className="flex min-h-20 flex-1 items-center justify-center rounded-md border border-dashed bg-background/40 p-3 text-center text-xs text-muted-foreground">{t("common.noResults")}</p> : columnItems.map((item) => {
                const date = item.type === "Issue" ? item.targetResolutionDate : item.dueDate;
                const canMove = canEdit && activeStatuses.length > 0;
                return <article key={item.id} draggable={canMove && movingItemId !== item.id} aria-busy={movingItemId === item.id} onDragStart={(event) => {
                  setDraggedItemId(item.id);
                  event.dataTransfer.effectAllowed = "move";
                  event.dataTransfer.setData("text/plain", item.id);
                }} onDragEnd={() => setDraggedItemId(null)} className={`rounded-md border bg-background p-3 shadow-sm ${canMove ? "cursor-grab active:cursor-grabbing" : ""} ${movingItemId === item.id ? "opacity-60" : ""}`}>
                  <div className="flex items-start justify-between gap-2"><Badge variant="outline" className="text-[10px]">{item.type}</Badge>{canMove ? <GripVertical className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" /> : null}</div>
                  {onOpen ? <button type="button" onClick={() => onOpen(item.id)} className="mt-2 block w-full text-start line-clamp-2 text-sm font-semibold hover:text-primary hover:underline">{item.title}</button> : <Link to={`/work-items/${encodeURIComponent(item.id)}`} className="mt-2 block line-clamp-2 text-sm font-semibold hover:text-primary hover:underline">{item.title}</Link>}
                  <div className="mt-2 flex flex-wrap items-center gap-1.5 text-[11px]">
                    <Badge status={item.priority}>{t(`workItems.priority${item.priority}`)}</Badge>
                    {item.projectName ? <span className="max-w-40 truncate rounded bg-muted px-2 py-1 text-muted-foreground" title={item.projectName}>{item.projectName}</span> : null}
                  </div>
                  <div className="mt-2 flex flex-col gap-1 border-t pt-2 text-[11px] text-muted-foreground">
                    {item.assignedResourceName || item.assignedTeamName ? <span className="flex min-w-0 items-center gap-1.5"><UserRound className="h-3.5 w-3.5 shrink-0" /><span className="truncate">{item.assignedResourceName ?? item.assignedTeamName}</span></span> : null}
                    {date ? <span>{t("workItems.dueDate")}: {formatDate(date, language)}</span> : null}
                  </div>
                  {canMove ? <label className="mt-2 block"><span className="sr-only">{t("workItems.changeStatus")}: {item.title}</span><select className="input-field h-8 w-full text-xs" value={item.status} disabled={movingItemId === item.id} onChange={(event) => onMove(item.id, event.target.value)}>{activeStatuses.map((candidate) => <option key={candidate.id} value={candidate.code}>{language === "ar" ? candidate.nameAr : candidate.nameEn}</option>)}</select></label> : null}
                  {movingItemId === item.id ? <p className="mt-2 text-[11px] font-medium text-primary">{t("workItems.moving")}</p> : null}
                </article>;
              })}
            </div>
          </section>;
        })}
      </div>
    </div>
  </div>;
}
