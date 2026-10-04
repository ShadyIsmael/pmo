import * as React from "react";
import { Input } from "@components/ui/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Button } from "@components/ui/Button";
import { X } from "lucide-react";
import { useI18n } from "@i18n/messages";

interface FilterState {
  search: string;
  status: string;
  priority: string;
  portfolioId: string;
  page: number;
  pageSize: number;
}

interface ProjectFiltersProps {
  filters: FilterState;
  onChange: (filters: FilterState) => void;
}

export function ProjectFilters({ filters, onChange }: ProjectFiltersProps) {
  const { t } = useI18n();

  const activeFilters = [
    filters.search && { key: "search", label: `${t("common.search")}: ${filters.search}` },
    filters.status && { key: "status", label: `${t("common.status")}: ${filters.status}` },
    filters.priority && { key: "priority", label: `${t("common.priority")}: ${filters.priority}` },
    filters.portfolioId && { key: "portfolioId", label: `${t("projects.portfolio")}: ${filters.portfolioId}` },
  ].filter(Boolean) as { key: string; label: string }[];

  return (
    <div className="space-y-4 pb-4 border-b">
      <div className="flex flex-wrap gap-3">
        <div className="flex-1 min-w-[200px]">
          <Input
            placeholder={t("projects.searchPlaceholder")}
            value={filters.search}
            onChange={(e) => onChange({ ...filters, search: e.target.value })}
            className="max-w-sm"
          />
        </div>
        <div className="flex-1 min-w-[150px]">
          <Select value={filters.status} onValueChange={(value) => onChange({ ...filters, status: value })}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder={t("common.all")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">{t("common.all")}</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Archived">Archived</SelectItem>
              <SelectItem value="On Hold">On Hold</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="flex-1 min-w-[150px]">
          <Select value={filters.priority} onValueChange={(value) => onChange({ ...filters, priority: value })}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder={t("common.all")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">{t("common.all")}</SelectItem>
              <SelectItem value="High">High</SelectItem>
              <SelectItem value="Medium">Medium</SelectItem>
              <SelectItem value="Low">Low</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="flex-1 min-w-[150px]">
          <Select value={filters.portfolioId} onValueChange={(value) => onChange({ ...filters, portfolioId: value })}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder={t("common.all")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">{t("common.all")}</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {activeFilters.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {activeFilters.map((filter) => (
            <span key={filter.key} className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-muted rounded">
              {filter.label}
              <button
                type="button"
                onClick={() => onChange({ ...filters, [filter.key]: "" })}
                className="hover:text-muted-foreground"
              >
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
          <Button variant="ghost" size="sm" onClick={() => onChange({ ...filters, search: "", status: "", priority: "", portfolioId: "" })}>
            {t("common.clearFilters")}
          </Button>
        </div>
      )}
    </div>
  );
}