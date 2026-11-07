import * as React from "react";
import { Search, X } from "lucide-react";
import { Input } from "@components/ui/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";
import { usePortfolios } from "@features/delivery/api/delivery";

export interface ProjectFilterState {
  search: string;
  status: string;
  priority: string;
  portfolioId: string;
  page: number;
  pageSize: number;
  [key: string]: string | number | boolean | undefined;
}

interface ProjectFiltersProps {
  filters: ProjectFilterState;
  onChange: (filters: ProjectFilterState) => void;
}

export function ProjectFilters({ filters, onChange }: ProjectFiltersProps) {
  const { t } = useI18n();
  const statusLabels: Record<string, string> = {
    Active: t("projects.filterStatuses.active"), OnHold: t("projects.filterStatuses.onHold"),
    Completed: t("projects.filterStatuses.completed"), Cancelled: t("projects.filterStatuses.cancelled"),
    Archived: t("projects.filterStatuses.archived"),
  };
  const priorityLabels: Record<string, string> = {
    High: t("enums.projectPriority.high"), Medium: t("enums.projectPriority.medium"),
    Low: t("enums.projectPriority.low"),
  };
  const [draft, setDraft] = React.useState(filters.search);
  const portfoliosQuery = usePortfolios(true);

  // Debounce search input so typing doesn't refetch on every keystroke.
  React.useEffect(() => {
    setDraft(filters.search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.status, filters.priority, filters.portfolioId]);

  React.useEffect(() => {
    if (draft === filters.search) return;
    const timer = window.setTimeout(() => onChange({ ...filters, search: draft }), 300);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft]);

  const activeFilters = [
    filters.search && { key: "search", label: `${t("common.search")}: ${filters.search}` },
    filters.status && { key: "status", label: `${t("common.status")}: ${statusLabels[filters.status] ?? filters.status}` },
    filters.priority && { key: "priority", label: `${t("projects.priority")}: ${priorityLabels[filters.priority] ?? filters.priority}` },
    filters.portfolioId && {
      key: "portfolioId",
      label: `${t("projects.portfolio")}: ${(portfoliosQuery.data?.items ?? []).find((p) => p.id === filters.portfolioId)?.name ?? filters.portfolioId}`,
    },
  ].filter(Boolean) as { key: "search" | "status" | "priority" | "portfolioId"; label: string }[];

  function clearKey(key: "search" | "status" | "priority" | "portfolioId") {
    if (key === "search") setDraft("");
    onChange({ ...filters, [key]: "" });
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-col gap-2 md:flex-row">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder={t("projects.searchPlaceholder")}
            aria-label={t("projects.searchPlaceholder")}
            className="bg-background ps-9"
          />
        </div>
        <div className="grid grid-cols-2 gap-2 md:flex">
          <Select value={filters.status} onValueChange={(value) => onChange({ ...filters, status: value })}>
            <SelectTrigger className="md:w-40" aria-label={t("common.status")}>
              <SelectValue placeholder={t("common.status")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">{t("common.all")}</SelectItem>
              <SelectItem value="Active">{statusLabels.Active}</SelectItem>
              <SelectItem value="OnHold">{statusLabels.OnHold}</SelectItem>
              <SelectItem value="Completed">{statusLabels.Completed}</SelectItem>
              <SelectItem value="Cancelled">{statusLabels.Cancelled}</SelectItem>
              <SelectItem value="Archived">{statusLabels.Archived}</SelectItem>
            </SelectContent>
          </Select>
          <Select value={filters.priority} onValueChange={(value) => onChange({ ...filters, priority: value })}>
            <SelectTrigger className="md:w-36" aria-label={t("projects.priority")}>
              <SelectValue placeholder={t("projects.priority")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">{t("common.all")}</SelectItem>
              <SelectItem value="High">{priorityLabels.High}</SelectItem>
              <SelectItem value="Medium">{priorityLabels.Medium}</SelectItem>
              <SelectItem value="Low">{priorityLabels.Low}</SelectItem>
            </SelectContent>
          </Select>
          <Select value={filters.portfolioId} onValueChange={(value) => onChange({ ...filters, portfolioId: value })}>
            <SelectTrigger className="col-span-2 md:w-48" aria-label={t("projects.portfolio")}>
              <SelectValue placeholder={t("projects.portfolio")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="">{t("common.all")}</SelectItem>
              {(portfoliosQuery.data?.items ?? []).map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {p.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {activeFilters.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          {activeFilters.map((filter) => (
            <span key={filter.key} className="inline-flex items-center gap-1 rounded-full bg-muted px-2.5 py-1 text-xs">
              <span className="max-w-56 truncate">{filter.label}</span>
              <button
                type="button"
                onClick={() => clearKey(filter.key)}
                className="rounded-full p-0.5 hover:bg-background"
                aria-label={`${t("common.clearFilters")}: ${filter.label}`}
              >
                <X className="h-3 w-3" />
              </button>
            </span>
          ))}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setDraft("");
              onChange({ ...filters, search: "", status: "", priority: "", portfolioId: "" });
            }}
          >
            {t("common.clearFilters")}
          </Button>
        </div>
      )}
    </div>
  );
}
