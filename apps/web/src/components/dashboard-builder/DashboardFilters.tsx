"use client";

import * as React from "react";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { X } from "lucide-react";
import type { GlobalFilters } from "./dashboard-types";
import { mockPortfolios, mockProjects } from "@/lib/mock-data";

interface DashboardFiltersProps {
  filters: GlobalFilters;
  onFiltersChange: (filters: GlobalFilters) => void;
  compact?: boolean;
}

export function DashboardFilters({ filters: _filters, onFiltersChange, compact }: DashboardFiltersProps) {
  const filters = _filters;
  const programs = React.useMemo(() => {
    const progs = new Set(mockProjects.filter((p) => !filters.portfolio || p.portfolio === filters.portfolio).map((p) => p.program));
    return Array.from(progs);
  }, [filters.portfolio]);

  const projects = React.useMemo(() => {
    return mockProjects.filter((p) => {
      if (filters.portfolio && p.portfolio !== filters.portfolio) return false;
      if (filters.program && p.program !== filters.program) return false;
      return true;
    });
  }, [filters.portfolio, filters.program]);

  const hasFilters = filters.portfolio || filters.program || filters.project;

  if (compact) {
    return (
      <div className="flex items-center gap-1.5 flex-wrap">
        <Select value={filters.portfolio || "_all"} onValueChange={(v) => onFiltersChange({ ...filters, portfolio: v === "_all" ? null : v, program: null, project: null })}>
          <SelectTrigger className="h-7 text-[10px] w-[120px]">
            <SelectValue placeholder="Portfolio" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all" className="text-xs">All Portfolios</SelectItem>
            {mockPortfolios.map((pf) => (
              <SelectItem key={pf.id} value={pf.id} className="text-xs">{pf.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={filters.program || "_all"} onValueChange={(v) => onFiltersChange({ ...filters, program: v === "_all" ? null : v })}>
          <SelectTrigger className="h-7 text-[10px] w-[120px]">
            <SelectValue placeholder="Program" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all" className="text-xs">All Programs</SelectItem>
            {programs.map((prog) => (
              <SelectItem key={prog} value={prog} className="text-xs">{prog}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {hasFilters && (
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => onFiltersChange({ portfolio: null, program: null, project: null, dateRange: filters.dateRange })}>
            <X className="h-3 w-3" />
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <span className="text-[10px] text-muted-foreground font-medium uppercase tracking-wider">Filters</span>
      <Select value={filters.dateRange} onValueChange={(v) => onFiltersChange({ ...filters, dateRange: v })}>
        <SelectTrigger className="h-7 text-[10px] w-[110px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {[{ label: "Last 7 Days", value: "7d" }, { label: "Last 30 Days", value: "30d" }, { label: "Last 90 Days", value: "90d" }, { label: "Year to Date", value: "ytd" }, { label: "Last Year", value: "1y" }].map((o) => (
            <SelectItem key={o.value} value={o.value} className="text-xs">{o.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={filters.portfolio || "_all"} onValueChange={(v) => onFiltersChange({ ...filters, portfolio: v === "_all" ? null : v, program: null, project: null })}>
        <SelectTrigger className="h-7 text-[10px] w-[130px]">
          <SelectValue placeholder="All Portfolios" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all" className="text-xs">All Portfolios</SelectItem>
          {mockPortfolios.map((pf) => (
            <SelectItem key={pf.id} value={pf.id} className="text-xs">{pf.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={filters.program || "_all"} onValueChange={(v) => onFiltersChange({ ...filters, program: v === "_all" ? null : v })}>
        <SelectTrigger className="h-7 text-[10px] w-[130px]">
          <SelectValue placeholder="All Programs" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all" className="text-xs">All Programs</SelectItem>
          {programs.map((prog) => (
            <SelectItem key={prog} value={prog} className="text-xs">{prog}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={filters.project || "_all"} onValueChange={(v) => onFiltersChange({ ...filters, project: v === "_all" ? null : v })}>
        <SelectTrigger className="h-7 text-[10px] w-[130px]">
          <SelectValue placeholder="All Projects" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="_all" className="text-xs">All Projects</SelectItem>
          {projects.map((p) => (
            <SelectItem key={p.id} value={p.id} className="text-xs">{p.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      {hasFilters && (
        <Button variant="ghost" size="sm" className="h-7 text-xs gap-1" onClick={() => onFiltersChange({ portfolio: null, program: null, project: null, dateRange: filters.dateRange })}>
          <X className="h-3 w-3" />
          Clear
        </Button>
      )}
    </div>
  );
}
