"use client";

import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { Check, ChevronDown, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";
import type { ProjectSummaryDto } from "@/hooks/projects/useProjects";

type Copy = Record<string, string>;

interface ProjectPickerProps {
  id: string;
  label: string;
  value?: string;
  projects: readonly ProjectSummaryDto[];
  isLoading?: boolean;
  onChange: (_value: string | undefined) => void;
  t: Copy;
  emptyLabel?: string;
  projectNameFirst?: boolean;
}

export function ProjectPicker({ id, label, value, projects, isLoading, onChange, t, emptyLabel, projectNameFirst = false }: ProjectPickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [highlighted, setHighlighted] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const selected = projects.find((project) => project.id === value);
  const filteredProjects = useMemo(() => {
    const normalized = search.trim().toLocaleLowerCase();
    if (!normalized) return projects;
    return projects.filter((project) => `${project.name} ${project.code}`.toLocaleLowerCase().includes(normalized));
  }, [projects, search]);

  useEffect(() => {
    if (!open) return;
    setHighlighted(0);
    requestAnimationFrame(() => inputRef.current?.focus());
  }, [open]);

  const options = [null, ...filteredProjects];
  const select = (projectId?: string) => {
    onChange(projectId);
    setOpen(false);
    setSearch("");
  };
  const onSearchKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown") { event.preventDefault(); setHighlighted((current) => Math.min(current + 1, options.length - 1)); }
    else if (event.key === "ArrowUp") { event.preventDefault(); setHighlighted((current) => Math.max(current - 1, 0)); }
    else if (event.key === "Enter") { event.preventDefault(); const option = options[highlighted]; select(option === null ? undefined : option?.id); }
    else if (event.key === "Escape") { event.preventDefault(); setOpen(false); }
  };

  const emptyProjectLabel = emptyLabel ?? t.noProject;
  const projectLabel = (project: ProjectSummaryDto) => projectNameFirst ? <><span className="font-medium">{project.name}</span><span className="mt-0.5 block text-xs text-muted-foreground">{project.code}</span></> : <><span className="font-medium">{project.code}</span><span className="mx-1 text-muted-foreground">·</span>{project.name}</>;

  return <div className="space-y-1.5">
    <label htmlFor={`${id}-trigger`} className="text-sm font-medium">{label}</label>
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild><Button id={`${id}-trigger`} type="button" variant="outline" role="combobox" aria-expanded={open} aria-controls={`${id}-options`} className="w-full justify-between font-normal text-start"><span className="min-w-0 truncate">{selected ? projectLabel(selected) : emptyProjectLabel}</span><ChevronDown className="ms-2 h-4 w-4 shrink-0 opacity-50" /></Button></PopoverTrigger>
      <PopoverContent align="start" className="w-[var(--radix-popover-trigger-width)] min-w-72 p-0">
        <div className="flex items-center border-b px-3"><Search className="h-4 w-4 shrink-0 text-muted-foreground" /><Input ref={inputRef} value={search} onChange={(event) => { setSearch(event.target.value); setHighlighted(0); }} onKeyDown={onSearchKeyDown} placeholder={t.searchProjects} aria-label={t.searchProjects} className="h-10 border-0 px-2 shadow-none focus-visible:ring-0" /></div>
        <div id={`${id}-options`} role="listbox" aria-label={label} className="max-h-60 overflow-y-auto p-1">
          <button type="button" role="option" aria-selected={!value} onMouseEnter={() => setHighlighted(0)} onClick={() => select()} className={cn("flex w-full items-center justify-between rounded-sm px-3 py-2 text-start text-sm", highlighted === 0 && "bg-accent text-accent-foreground")}><span>{emptyProjectLabel}</span>{!value ? <Check className="h-4 w-4" /> : null}</button>
          {isLoading ? <p className="px-3 py-3 text-sm text-muted-foreground">{t.loading}</p> : filteredProjects.length === 0 ? <p className="px-3 py-3 text-sm text-muted-foreground">{t.noProjects}</p> : filteredProjects.map((project, index) => { const optionIndex = index + 1; return <button key={project.id} type="button" role="option" aria-selected={project.id === value} onMouseEnter={() => setHighlighted(optionIndex)} onClick={() => select(project.id)} className={cn("flex w-full items-center justify-between rounded-sm px-3 py-2 text-start text-sm", highlighted === optionIndex && "bg-accent text-accent-foreground")}><span className="min-w-0 text-start">{projectLabel(project)}</span>{project.id === value ? <Check className="ms-2 h-4 w-4 shrink-0" /> : null}</button>; })}
        </div>
      </PopoverContent>
    </Popover>
  </div>;
}
