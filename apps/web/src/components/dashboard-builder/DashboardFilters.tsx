"use client";

import * as React from "react";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { X } from "lucide-react";
import type { GlobalFilters } from "./dashboard-types";
import { useDashboardPortfolios, useDashboardPrograms, useDashboardProjects } from "./dashboard-data";
import { isDashboardFilterOpen, type DashboardFilterKey } from "./dashboard-loading";

interface DashboardFiltersProps {
  filters: GlobalFilters;
  onFiltersChange: (filters: GlobalFilters) => void;
  compact?: boolean;
}

export function DashboardFilters({ filters: _filters, onFiltersChange, compact }: DashboardFiltersProps) {
  const filters = _filters;
  const [openFilter, setOpenFilter] = React.useState<DashboardFilterKey | null>(null);
  const portfoliosQuery = useDashboardPortfolios(isDashboardFilterOpen(openFilter, "portfolio"));
  const programsQuery = useDashboardPrograms(isDashboardFilterOpen(openFilter, "program"));
  const projectsQuery = useDashboardProjects(isDashboardFilterOpen(openFilter, "project"));
  const portfolios = portfoliosQuery.data?.items ?? [];
  const programs = programsQuery.data?.items ?? [];
  const projects = React.useMemo(() => projectsQuery.data?.items ?? [], [projectsQuery.data?.items]);

  const hasFilters = filters.portfolio || filters.program || filters.project;

  if (compact) {
    return (
      <div className="flex items-center gap-2 flex-wrap">
        <Select value={filters.portfolio || "_all"} onOpenChange={(open) => setOpenFilter(open ? "portfolio" : null)} onValueChange={(v) => onFiltersChange({ ...filters, portfolio: v === "_all" ? null : v, program: null, project: null })}>
          <SelectTrigger className="h-8 text-xs font-medium w-[130px] border-slate-200">
            <SelectValue placeholder="Portfolio" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all" className="text-xs font-medium">All Portfolios</SelectItem>
            {portfolios.map((pf) => (
              <SelectItem key={pf.id} value={pf.id} className="text-xs font-medium">{pf.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={filters.program || "_all"} onOpenChange={(open) => setOpenFilter(open ? "program" : null)} onValueChange={(v) => onFiltersChange({ ...filters, program: v === "_all" ? null : v })}>
          <SelectTrigger className="h-8 text-xs font-medium w-[130px] border-slate-200">
            <SelectValue placeholder="Program" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all" className="text-xs font-medium">All Programs</SelectItem>
            {programs.map((prog) => (
              <SelectItem key={prog.id} value={prog.id} className="text-xs font-medium">{prog.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {hasFilters && (
          <Button variant="ghost" size="icon" className="h-8 w-8 text-slate-500 hover:text-slate-700 hover:bg-slate-100" onClick={() => onFiltersChange({ portfolio: null, program: null, project: null, dateRange: filters.dateRange })}>
            <X className="h-3.5 w-3.5" />
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">Filters</span>
      <Select value={filters.dateRange} onValueChange={(v) => onFiltersChange({ ...filters, dateRange: v })}>
        <SelectTrigger className="h-8 text-xs font-medium w-[120px] border-slate-200">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {[{ label: "Last 7 Days", value: "7d" }, { label: "Last 30 Days", value: "30d" }, { label: "Last 90 Days", value: "90d" }, { label: "Year to Date", value: "ytd" }, { label: "Last Year", value: "1y" }].map((o) => (
            <SelectItem key={o.value} value={o.value} className="text-xs font-medium">{o.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={filters.portfolio || "_all"} onOpenChange={(open) => setOpenFilter(open ? "portfolio" : null)} onValueChange={(v) => onFiltersChange({ ...filters, portfolio: v === "_all" ? null : v, program: null, project: null })}>
        <SelectTrigger className="h-8 text-xs font-medium w-[140px] border-slate-200">
          <SelectValue placeholder="All Portfolios" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all" className="text-xs font-medium">All Portfolios</SelectItem>
          {portfolios.map((pf) => (
            <SelectItem key={pf.id} value={pf.id} className="text-xs font-medium">{pf.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={filters.program || "_all"} onOpenChange={(open) => setOpenFilter(open ? "program" : null)} onValueChange={(v) => onFiltersChange({ ...filters, program: v === "_all" ? null : v })}>
        <SelectTrigger className="h-8 text-xs font-medium w-[140px] border-slate-200">
          <SelectValue placeholder="All Programs" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all" className="text-xs font-medium">All Programs</SelectItem>
          {programs.map((prog) => (
            <SelectItem key={prog.id} value={prog.id} className="text-xs font-medium">{prog.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={filters.project || "_all"} onOpenChange={(open) => setOpenFilter(open ? "project" : null)} onValueChange={(v) => onFiltersChange({ ...filters, project: v === "_all" ? null : v })}>
        <SelectTrigger className="h-8 text-xs font-medium w-[140px] border-slate-200">
          <SelectValue placeholder="All Projects" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all" className="text-xs font-medium">All Projects</SelectItem>
          {projects.map((p) => (
            <SelectItem key={p.id} value={p.id} className="text-xs font-medium">{p.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      {hasFilters && (
        <Button variant="ghost" size="sm" className="h-8 text-xs font-medium gap-1.5 text-slate-600 hover:text-slate-800 hover:bg-slate-100" onClick={() => onFiltersChange({ portfolio: null, program: null, project: null, dateRange: filters.dateRange })}>
          <X className="h-3.5 w-3.5" />
          Clear
        </Button>
      )}
    </div>
  );
}
