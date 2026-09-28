import { useMemo, useState } from "react";
import {
  ArrowUpRight,
  FolderKanban,
  ListFilter,
  Plus,
  Search,
  ShieldAlert,
  X,
} from "lucide-react";
import { useRisks } from "@/hooks/useRisks";
import {
  RISK_LEVELS,
  RISK_STATUSES,
  getRiskImpactTypeLabel,
  getRiskLevelLabel,
  getRiskLevelTextClass,
  getRiskStatusLabel,
  getRiskStatusTextClass,
} from "@/lib/enums";
import { riskImpactTypeAccents } from "@/lib/risk-presentation";
import { formatDate } from "@/lib/utils";
import type { RiskImpactType } from "@/types/risk";

function RiskLevelBadge({ value }: { value: string }) {
  return (
    <span className={`font-semibold ${getRiskLevelTextClass(value)}`}>
      {getRiskLevelLabel(value)}
    </span>
  );
}

function RiskStatusBadge({ value }: { value: string }) {
  return (
    <span className={`font-semibold ${getRiskStatusTextClass(value)}`}>
      {getRiskStatusLabel(value)}
    </span>
  );
}

export default function RiskRegisterPage() {
  const query = useRisks();
  const risks = useMemo(() => query.data?.items ?? [], [query.data?.items]);
  const [search, setSearch] = useState("");
  const [project, setProject] = useState("all");
  const [level, setLevel] = useState("all");
  const [status, setStatus] = useState("all");

  const projects = useMemo(
    () =>
      [...new Set(risks.map((r) => r.projectName).filter(Boolean))].sort(),
    [risks]
  );

  const filteredRisks = useMemo(() => {
    const q = search.trim().toLowerCase();
    return risks.filter((risk) => {
      const matchesSearch =
        !q ||
        [risk.title, risk.projectName, risk.ownerResourceName].some((v) =>
          v?.toLowerCase().includes(q)
        );
      return (
        matchesSearch &&
        (project === "all" || risk.projectName === project) &&
        (level === "all" || risk.riskLevel === level) &&
        (status === "all" || risk.status === status)
      );
    });
  }, [level, project, risks, search, status]);

  const clearFilters = () => {
    setSearch("");
    setProject("all");
    setLevel("all");
    setStatus("all");
  };

  const hasFilters =
    Boolean(search || project !== "all" || level !== "all" || status !== "all");

  return (
    <div className="space-y-6">
      {/* PageHeader */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-0.5">
          <div className="flex items-center gap-3">
            <h1 className="text-lg font-bold tracking-tight sm:text-xl">
              Risk Register
            </h1>
          </div>
          <p className="text-xs text-muted-foreground/80">
            Track risks across accessible projects.
          </p>
        </div>
        <div className="flex items-center gap-1.5 shrink-0">
          <button className="inline-flex items-center justify-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground shadow hover:bg-primary/90">
            <Plus className="h-3.5 w-3.5" />
            Create Risk
          </button>
        </div>
      </div>

      {/* Filter Card */}
      <div className="rounded-lg border bg-card shadow-sm">
        <div className="space-y-4 p-4">
          <div className="flex items-center gap-2 text-sm font-semibold">
            <ListFilter className="h-4 w-4 text-primary" />
            Risk Register
          </div>
          <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-[minmax(240px,1.6fr)_repeat(3,minmax(150px,1fr))_auto]">
            {/* Search */}
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search risks, projects, or owners..."
                className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 pl-9 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>
            {/* Project filter */}
            <select
              value={project}
              onChange={(e) => setProject(e.target.value)}
              className="flex h-9 w-full items-center justify-between rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">All projects</option>
              {projects.map((name) => (
                <option key={name} value={name}>
                  {name}
                </option>
              ))}
            </select>
            {/* Level filter */}
            <select
              value={level}
              onChange={(e) => setLevel(e.target.value)}
              className="flex h-9 w-full items-center justify-between rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">All levels</option>
              {RISK_LEVELS.map((v) => (
                <option key={v} value={v}>
                  {getRiskLevelLabel(v)}
                </option>
              ))}
            </select>
            {/* Status filter */}
            <select
              value={status}
              onChange={(e) => setStatus(e.target.value)}
              className="flex h-9 w-full items-center justify-between rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">All statuses</option>
              {RISK_STATUSES.map((v) => (
                <option key={v} value={v}>
                  {getRiskStatusLabel(v)}
                </option>
              ))}
            </select>
            {/* Clear */}
            {hasFilters ? (
              <button
                onClick={clearFilters}
                className="inline-flex items-center justify-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              >
                <X className="h-3.5 w-3.5" />
                Clear filters
              </button>
            ) : (
              <span />
            )}
          </div>
        </div>
      </div>

      {/* Table Card */}
      <div className="rounded-lg border bg-card shadow-sm">
        {query.isLoading ? (
          <div className="p-5">
            <div className="h-40 animate-pulse rounded-md bg-muted" />
          </div>
        ) : query.isError ? (
          <p className="p-8 text-center text-sm text-destructive">
            Unable to load risks.
          </p>
        ) : filteredRisks.length === 0 ? (
          <div className="flex min-h-64 flex-col items-center justify-center px-6 text-center">
            <ShieldAlert className="h-9 w-9 text-muted-foreground/40" />
            <p className="mt-3 text-sm font-medium">
              {hasFilters
                ? "No risks match these filters."
                : "No risks have been recorded yet."}
            </p>
          </div>
        ) : (
          <div className="overflow-auto">
            <table className="w-full caption-bottom text-sm">
              <thead className="border-b">
                <tr className="transition-colors hover:bg-muted/50">
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Title
                  </th>
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Project
                  </th>
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Risk Level
                  </th>
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Impact
                  </th>
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Owner
                  </th>
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Status
                  </th>
                  <th className="h-10 px-4 text-left align-middle font-medium text-muted-foreground">
                    Last update
                  </th>
                  <th className="h-10 w-12 px-4 text-left align-middle font-medium text-muted-foreground">
                    <span className="sr-only">Open risk</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredRisks.map((risk) => (
                  <tr
                    key={risk.id}
                    className="cursor-pointer border-b transition-colors hover:bg-muted/50"
                  >
                    <td className="max-w-[260px] p-4 align-middle">
                      <p className="truncate font-semibold">{risk.title}</p>
                      <p className="mt-0.5 truncate text-[11px] text-muted-foreground">
                        {risk.id}
                      </p>
                    </td>
                    <td className="max-w-[180px] p-4 align-middle">
                      <span className="inline-flex max-w-full items-center gap-1.5 truncate font-medium text-blue-700 hover:text-blue-800 hover:underline dark:text-blue-300 dark:hover:text-blue-200">
                        <FolderKanban className="h-3.5 w-3.5 shrink-0" />
                        {risk.projectName || "\u2014"}
                      </span>
                    </td>
                    <td className="p-4 align-middle">
                      <RiskLevelBadge value={risk.riskLevel} />
                    </td>
                    <td className="max-w-[240px] p-4 align-middle">
                      <div className="flex flex-wrap gap-1">
                        {risk.impactTypes?.length ? (
                          risk.impactTypes.map((type) => (
                            <span
                              key={type}
                              className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] font-medium ${riskImpactTypeAccents[type as RiskImpactType]?.chip ?? ""}`}
                            >
                              <span
                                className={`h-1.5 w-1.5 rounded-full ${riskImpactTypeAccents[type as RiskImpactType]?.dot ?? ""}`}
                              />
                              {getRiskImpactTypeLabel(type)}
                            </span>
                          ))
                        ) : (
                          <span className="text-muted-foreground">
                            Not recorded
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="p-4 align-middle">
                      {risk.ownerResourceName || "\u2014"}
                    </td>
                    <td className="p-4 align-middle">
                      <RiskStatusBadge value={risk.status} />
                    </td>
                    <td className="whitespace-nowrap p-4 align-middle text-muted-foreground">
                      {formatDate(
                        risk.lastActionUpdateAt ?? risk.updatedAt ?? risk.createdAt,
                        "en-GB"
                      )}
                    </td>
                    <td className="p-4 align-middle">
                      <ArrowUpRight className="h-4 w-4 text-muted-foreground" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
