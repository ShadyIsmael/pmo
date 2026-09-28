"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import { CalendarDays, Search, SlidersHorizontal } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import type { UserLookupDto } from "@/hooks/projects/useProjectLookups";
import type { WorkItemStatusReference } from "@/lib/api/reference-data";

type Copy = Record<string, string>;
export type WorkItemFilterValues = {
  type: string;
  status: string;
  priority: string;
  assignee: string;
  projectId: string;
  dueFrom: string;
  dueTo: string;
};

export const emptyWorkItemFilters: WorkItemFilterValues = { type: "all", status: "all", priority: "all", assignee: "all", projectId: "", dueFrom: "", dueTo: "" };

export function countActiveWorkItemFilters(filters: WorkItemFilterValues) {
  return [filters.type !== "all", filters.status !== "all", filters.priority !== "all", filters.assignee !== "all", Boolean(filters.projectId), Boolean(filters.dueFrom), Boolean(filters.dueTo)].filter(Boolean).length;
}

export function WorkItemFiltersSideSheet({ open, onOpenChange, value, onApply, locale, t, statuses, users, projects }: {
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  value: WorkItemFilterValues;
  onApply: (_value: WorkItemFilterValues) => void;
  locale: string;
  t: Copy;
  statuses: readonly WorkItemStatusReference[];
  users: readonly UserLookupDto[];
  projects: readonly { id: string; name: string; code?: string }[];
}) {
  const [draft, setDraft] = useState(value);
  const [userSearch, setUserSearch] = useState("");

  useEffect(() => { if (open) { setDraft(value); setUserSearch(""); } }, [open, value]);

  const filteredUsers = useMemo(() => {
    const query = userSearch.trim().toLowerCase();
    return query ? users.filter((user) => `${user.fullName} ${user.email}`.toLowerCase().includes(query)) : users;
  }, [users, userSearch]);

  const set = (key: keyof WorkItemFilterValues, next: string | boolean) => setDraft((current) => ({ ...current, [key]: next }));
  const clear = () => setDraft(emptyWorkItemFilters);
  const apply = () => { onApply(draft); onOpenChange(false); };

  return <SideSheet open={open} onOpenChange={onOpenChange} title={<span className="flex items-center gap-2"><SlidersHorizontal className="h-4 w-4" />{t.advancedFilters}</span>} description={t.advancedFiltersHint} size="md" closeLabel={t.close}>
    <SideSheetBody className="space-y-5">
      <FilterSection title={t.filterType}><div className="grid grid-cols-3 gap-2">{[["all", t.typeAll], ["Task", t.task], ["Issue", t.issue]].map(([key, label]) => <Choice key={key} selected={draft.type === key} label={label} onClick={() => set("type", key)} />)}</div></FilterSection>
      <FilterSection title={t.filterStatus}><select aria-label={t.filterStatus} value={draft.status} onChange={(event) => set("status", event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="all">{t.statusAll}</option>{statuses.filter((status) => status.isActive).map((status) => <option key={status.id} value={status.code}>{locale === "ar" ? status.nameAr : status.nameEn}</option>)}</select></FilterSection>
      <FilterSection title={t.filterPriority}><div className="grid grid-cols-2 gap-2">{[["all", t.priorityAll], ["Critical", t.critical], ["High", t.high], ["Medium", t.medium], ["Low", t.low], ["Backlog", t.backlog]].map(([key, label]) => <Choice key={key} selected={draft.priority === key} label={label} onClick={() => set("priority", key)} />)}</div></FilterSection>
      <FilterSection title={t.filterAssignee}><div className="relative"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="ps-8" value={userSearch} onChange={(event) => setUserSearch(event.target.value)} placeholder={t.searchUsers} /></div><select aria-label={t.filterAssignee} value={draft.assignee} onChange={(event) => set("assignee", event.target.value)} className="mt-2 h-10 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="all">{t.assigneeAll}</option><option value="me">{t.assigneeMe}</option><option value="unassigned">{t.assigneeUnassigned}</option>{filteredUsers.map((user) => <option key={user.id} value={user.id}>{user.fullName}</option>)}</select></FilterSection>
      <FilterSection title={t.filterProject}><select aria-label={t.filterProject} value={draft.projectId} onChange={(event) => set("projectId", event.target.value)} className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"><option value="">{t.allProjects}</option>{projects.map((project) => <option key={project.id} value={project.id}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}</select></FilterSection>
      <FilterSection title={t.filterDueDate}><div className="grid gap-3 sm:grid-cols-2"><label className="space-y-1.5 text-sm"><span className="text-xs text-muted-foreground">{t.from}</span><span className="relative block"><CalendarDays className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input type="date" className="ps-8" value={draft.dueFrom} onChange={(event) => set("dueFrom", event.target.value)} /></span></label><label className="space-y-1.5 text-sm"><span className="text-xs text-muted-foreground">{t.to}</span><span className="relative block"><CalendarDays className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input type="date" className="ps-8" value={draft.dueTo} onChange={(event) => set("dueTo", event.target.value)} /></span></label></div></FilterSection>
    </SideSheetBody>
    <SideSheetFooter><Button type="button" variant="ghost" onClick={clear}>{t.clearAll}</Button><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t.cancel}</Button><Button type="button" onClick={apply}>{t.applyFilters}</Button></SideSheetFooter>
  </SideSheet>;
}

function FilterSection({ title, children }: { title: string; children: ReactNode }) { return <section className="space-y-2"><h3 className="text-sm font-semibold">{title}</h3>{children}</section>; }
function Choice({ selected, label, onClick }: { selected: boolean; label: string; onClick: () => void }) { return <button type="button" onClick={onClick} className={`rounded-md border px-3 py-2 text-start text-sm transition-colors ${selected ? "border-primary/50 bg-primary/10 text-primary" : "border-border bg-background hover:bg-muted/40"}`}>{label}</button>; }
