import * as React from "react";
import { Link, useOutletContext } from "react-router-dom";
import {
  ChevronDown,
  ChevronUp,
  ExternalLink,
  Filter,
  History,
  RefreshCw,
  Search,
  ShieldAlert,
  SlidersHorizontal,
  X,
} from "lucide-react";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { cn } from "@lib/utils";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Badge } from "@components/ui/Badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Pagination } from "@components/ui/Pagination";
import {
  type ProjectAuditEventDto,
  type ProjectAuditFilters,
  useProjectAuditFacets,
  useProjectAuditTrail,
} from "./api/projects";

interface ProjectContext {
  projectId: string;
}

function useProjectContext(): ProjectContext {
  return useOutletContext<ProjectContext>();
}

const PAGE_SIZE = 25;

const DEFAULT_FILTERS: ProjectAuditFilters = { page: 1, pageSize: PAGE_SIZE, sort: "newest" };

interface ActiveFilterChip {
  key: string;
  group: string;
  value: string;
  remove: () => void;
}

/**
 * Translates a backend code (module, entity type or action) and falls back to a
 * humanised version of the code when no translation exists, so newly added audit
 * producers stay readable without shipping a frontend change first.
 */
function useAuditLabels() {
  const { t } = useI18n();
  return React.useMemo(() => {
    const resolve = (group: string, code: string) => {
      const key = `audit.${group}.${code}`;
      const translated = t(key);
      return translated === key ? humanize(code) : translated;
    };
    return {
      module: (code: string) => resolve("modules", code),
      entityType: (code: string) => resolve("entityTypes", code),
      action: (code: string) => resolve("actions", code),
      source: (code: string) => resolve("sources", code),
    };
  }, [t]);
}

function humanize(value: string): string {
  if (!value) return "";
  const spaced = value
    .replace(/[_-]+/g, " ")
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function formatSnapshot(value?: string | null): string | null {
  if (!value) return null;
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function toIsoDayStart(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(`${value}T00:00:00`);
  return Number.isFinite(date.getTime()) ? date.toISOString() : undefined;
}

function toIsoDayEnd(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(`${value}T23:59:59.999`);
  return Number.isFinite(date.getTime()) ? date.toISOString() : undefined;
}

export function ProjectHistoryTab() {
  const { projectId } = useProjectContext();
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const labels = useAuditLabels();

  const canViewAudit = Boolean(user && hasPermission(user.permissions, Permissions.Audit.View));

  const [filters, setFilters] = React.useState<ProjectAuditFilters>(DEFAULT_FILTERS);
  const [searchDraft, setSearchDraft] = React.useState("");
  const [fromDraft, setFromDraft] = React.useState("");
  const [toDraft, setToDraft] = React.useState("");
  const [filtersOpen, setFiltersOpen] = React.useState(false);
  const [expanded, setExpanded] = React.useState<string[]>([]);

  const panelRef = React.useRef<HTMLDivElement | null>(null);

  const facets = useProjectAuditFacets(projectId, canViewAudit);
  const trail = useProjectAuditTrail(projectId, filters, canViewAudit);

  // The panel stays mounted so collapsing never discards applied values or drafts.
  // `inert` removes the collapsed controls from the tab order and the accessibility
  // tree; it is toggled imperatively because React 18 does not type the attribute.
  React.useEffect(() => {
    panelRef.current?.toggleAttribute("inert", !filtersOpen);
  }, [filtersOpen]);

  const applyFilters = React.useCallback((patch: Partial<ProjectAuditFilters>) => {
    setFilters((current) => ({ ...current, ...patch, page: 1 }));
  }, []);

  const resetFilters = React.useCallback(() => {
    setSearchDraft("");
    setFromDraft("");
    setToDraft("");
    setFilters(DEFAULT_FILTERS);
  }, []);

  const toggleExpanded = React.useCallback((id: string) => {
    setExpanded((current) => (current.includes(id) ? current.filter((value) => value !== id) : [...current, id]));
  }, []);

  const data = trail.data;
  const events = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / (data?.pageSize ?? PAGE_SIZE)));
  const coverageStart = facets.data?.coverageStartUtc;

  const activeChips: ActiveFilterChip[] = [];
  if (filters.search) {
    activeChips.push({
      key: "search",
      group: t("audit.filters.search"),
      value: `“${filters.search}”`,
      remove: () => {
        setSearchDraft("");
        applyFilters({ search: undefined });
      },
    });
  }
  if (filters.module) {
    activeChips.push({
      key: "module",
      group: t("audit.filters.module"),
      value: labels.module(filters.module),
      remove: () => applyFilters({ module: undefined }),
    });
  }
  if (filters.action) {
    activeChips.push({
      key: "action",
      group: t("audit.filters.action"),
      value: labels.action(filters.action),
      remove: () => applyFilters({ action: undefined }),
    });
  }
  if (filters.actorId) {
    const actorName = facets.data?.actors.find((actor) => actor.id === filters.actorId)?.name;
    activeChips.push({
      key: "actor",
      group: t("audit.filters.actor"),
      value: actorName || t("audit.unknownActor"),
      remove: () => applyFilters({ actorId: undefined }),
    });
  }
  if (filters.entityType) {
    activeChips.push({
      key: "entityType",
      group: t("audit.filters.entityType"),
      value: labels.entityType(filters.entityType),
      remove: () => applyFilters({ entityType: undefined }),
    });
  }
  if (filters.from) {
    activeChips.push({
      key: "from",
      group: t("audit.filters.from"),
      value: formatDate(filters.from, { dateStyle: "medium" }),
      remove: () => {
        setFromDraft("");
        applyFilters({ from: undefined });
      },
    });
  }
  if (filters.to) {
    activeChips.push({
      key: "to",
      group: t("audit.filters.to"),
      value: formatDate(filters.to, { dateStyle: "medium" }),
      remove: () => {
        setToDraft("");
        applyFilters({ to: undefined });
      },
    });
  }
  const activeCount = activeChips.length;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="gap-3">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <History className="h-4 w-4" />
                {t("audit.title")}
              </CardTitle>
              <CardDescription className="mt-1">{t("audit.description")}</CardDescription>
            </div>
            {canViewAudit ? (
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => void trail.refetch()}
                disabled={trail.isFetching}
              >
                <RefreshCw className={`me-1.5 h-3.5 w-3.5 ${trail.isFetching ? "animate-spin" : ""}`} />
                {t("audit.refresh")}
              </Button>
            ) : null}
          </div>
          {canViewAudit ? (
            <p className="text-xs text-muted-foreground">
              {coverageStart
                ? t("audit.coverageSince", { date: formatDate(coverageStart, { dateStyle: "medium", timeStyle: "short" }) })
                : t("audit.coverageUnknown")}
              {data && !data.includeFinancialDetail ? ` ${t("audit.financialMasked")}` : ""}
            </p>
          ) : null}
        </CardHeader>

        <CardContent className="space-y-4">
          {!canViewAudit ? (
            <div className="flex flex-col items-center gap-2 rounded border border-dashed p-10 text-center">
              <ShieldAlert className="h-6 w-6 text-muted-foreground" />
              <p className="text-sm font-medium">{t("audit.noAccessTitle")}</p>
              <p className="max-w-md text-sm text-muted-foreground">{t("audit.noAccessDescription")}</p>
            </div>
          ) : (
            <>
              <form
                className="space-y-3"
                onSubmit={(event) => {
                  event.preventDefault();
                  applyFilters({ search: searchDraft.trim() || undefined });
                }}
              >
                {/* Compact toolbar: search stays available, advanced filters stay one click away. */}
                <div className="flex flex-wrap items-center gap-2">
                  <div className="flex min-w-0 flex-1 items-center gap-2 sm:max-w-sm">
                    <Label htmlFor="audit-search" className="sr-only">
                      {t("audit.filters.search")}
                    </Label>
                    <Input
                      id="audit-search"
                      value={searchDraft}
                      onChange={(event) => setSearchDraft(event.target.value)}
                      placeholder={t("audit.filters.searchPlaceholder")}
                      className="min-w-0"
                    />
                    <Button type="submit" size="icon" variant="outline" aria-label={t("audit.filters.applySearch")}>
                      <Search className="h-4 w-4" />
                    </Button>
                  </div>

                  <Button
                    type="button"
                    size="sm"
                    variant={filtersOpen ? "secondary" : "outline"}
                    aria-expanded={filtersOpen}
                    aria-controls="audit-filters-panel"
                    onClick={() => setFiltersOpen((open) => !open)}
                  >
                    <SlidersHorizontal className="me-1.5 h-3.5 w-3.5" />
                    {t("audit.filters.toggle")}
                    {activeCount > 0 ? (
                      <span className="ms-1.5 rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold text-primary tabular-nums">
                        {t("audit.filters.activeCount", { count: activeCount })}
                      </span>
                    ) : null}
                    <ChevronDown
                      className={cn(
                        "ms-1 h-3.5 w-3.5 transition-transform duration-200 motion-reduce:transition-none",
                        filtersOpen && "rotate-180",
                      )}
                    />
                  </Button>

                  {activeCount > 0 ? (
                    <Button type="button" size="sm" variant="ghost" onClick={resetFilters}>
                      <X className="me-1.5 h-3.5 w-3.5" />
                      {t("audit.filters.clearAll")}
                    </Button>
                  ) : null}

                  <span className="inline-flex items-center gap-1 text-xs text-muted-foreground ms-auto">
                    <Filter className="h-3 w-3" />
                    {t("audit.resultCount", { count: total })}
                  </span>
                </div>

                {/* Applied filters stay visible while the panel is collapsed. */}
                {activeCount > 0 ? (
                  <ul className="flex flex-wrap items-center gap-1.5" aria-label={t("audit.filters.appliedFilters")}>
                    {activeChips.map((chip) => (
                      <li key={chip.key}>
                        <span className="inline-flex items-center gap-1 rounded-full border bg-muted/40 py-0.5 pe-1 ps-2.5 text-xs">
                          <span className="text-muted-foreground">{chip.group}:</span>
                          <span className="font-medium">{chip.value}</span>
                          <button
                            type="button"
                            onClick={chip.remove}
                            aria-label={t("audit.filters.removeFilter", { filter: `${chip.group}: ${chip.value}` })}
                            className="rounded-full p-0.5 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                          >
                            <X className="h-3 w-3" />
                          </button>
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : null}

                <div
                  id="audit-filters-panel"
                  ref={panelRef}
                  aria-hidden={!filtersOpen}
                  className={cn(
                    "grid transition-[grid-template-rows] duration-200 ease-out motion-reduce:transition-none",
                    filtersOpen ? "grid-rows-[1fr]" : "grid-rows-[0fr]",
                  )}
                >
                  <div className="overflow-hidden">
                    <div className="grid gap-3 pt-1 md:grid-cols-2 xl:grid-cols-4">
                      <div className="space-y-1">
                        <Label htmlFor="audit-module">{t("audit.filters.module")}</Label>
                        <Select
                          value={filters.module ?? "all"}
                          onValueChange={(value) => applyFilters({ module: value === "all" ? undefined : value })}
                        >
                          <SelectTrigger id="audit-module">
                            <SelectValue placeholder={t("audit.filters.allModules")} />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="all">{t("audit.filters.allModules")}</SelectItem>
                            {(facets.data?.modules ?? []).map((module) => (
                              <SelectItem key={module} value={module}>
                                {labels.module(module)}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-1">
                        <Label htmlFor="audit-action">{t("audit.filters.action")}</Label>
                        <Select
                          value={filters.action ?? "all"}
                          onValueChange={(value) => applyFilters({ action: value === "all" ? undefined : value })}
                        >
                          <SelectTrigger id="audit-action">
                            <SelectValue placeholder={t("audit.filters.allActions")} />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="all">{t("audit.filters.allActions")}</SelectItem>
                            {(facets.data?.actions ?? []).map((action) => (
                              <SelectItem key={action} value={action}>
                                {labels.action(action)}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-1">
                        <Label htmlFor="audit-actor">{t("audit.filters.actor")}</Label>
                        <Select
                          value={filters.actorId ?? "all"}
                          onValueChange={(value) => applyFilters({ actorId: value === "all" ? undefined : value })}
                        >
                          <SelectTrigger id="audit-actor">
                            <SelectValue placeholder={t("audit.filters.allActors")} />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="all">{t("audit.filters.allActors")}</SelectItem>
                            {(facets.data?.actors ?? []).map((actor) => (
                              <SelectItem key={actor.id} value={actor.id}>
                                {actor.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-1">
                        <Label htmlFor="audit-entity-type">{t("audit.filters.entityType")}</Label>
                        <Select
                          value={filters.entityType ?? "all"}
                          onValueChange={(value) => applyFilters({ entityType: value === "all" ? undefined : value })}
                        >
                          <SelectTrigger id="audit-entity-type">
                            <SelectValue placeholder={t("audit.filters.allEntityTypes")} />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="all">{t("audit.filters.allEntityTypes")}</SelectItem>
                            {(facets.data?.entityTypes ?? []).map((entityType) => (
                              <SelectItem key={entityType} value={entityType}>
                                {labels.entityType(entityType)}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-1">
                        <Label htmlFor="audit-from">{t("audit.filters.from")}</Label>
                        <Input
                          id="audit-from"
                          type="date"
                          value={fromDraft}
                          max={toDraft || undefined}
                          onChange={(event) => {
                            setFromDraft(event.target.value);
                            applyFilters({ from: toIsoDayStart(event.target.value) });
                          }}
                        />
                      </div>

                      <div className="space-y-1">
                        <Label htmlFor="audit-to">{t("audit.filters.to")}</Label>
                        <Input
                          id="audit-to"
                          type="date"
                          value={toDraft}
                          min={fromDraft || undefined}
                          onChange={(event) => {
                            setToDraft(event.target.value);
                            applyFilters({ to: toIsoDayEnd(event.target.value) });
                          }}
                        />
                      </div>

                      <div className="space-y-1">
                        <Label htmlFor="audit-sort">{t("audit.filters.order")}</Label>
                        <Select
                          value={filters.sort ?? "newest"}
                          onValueChange={(value) => applyFilters({ sort: value === "oldest" ? "oldest" : "newest" })}
                        >
                          <SelectTrigger id="audit-sort">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="newest">{t("audit.filters.newestFirst")}</SelectItem>
                            <SelectItem value="oldest">{t("audit.filters.oldestFirst")}</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="flex items-end">
                        <Button type="submit" size="sm" variant="outline">
                          {t("audit.filters.apply")}
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
              </form>

              {trail.isLoading ? (
                <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
              ) : trail.isError ? (
                <div className="py-12 text-center text-sm text-destructive">
                  <p>{t("audit.loadError")}</p>
                  <Button className="mt-3" size="sm" variant="outline" onClick={() => void trail.refetch()}>
                    <RefreshCw className="me-1.5 h-3.5 w-3.5" />
                    {t("common.retry")}
                  </Button>
                </div>
              ) : events.length === 0 ? (
                <p className="rounded border border-dashed p-8 text-center text-sm text-muted-foreground">
                  {activeCount > 0 ? t("audit.noMatchingEvents") : t("audit.noEvents")}
                </p>
              ) : (
                <ol className="divide-y rounded border">
                  {events.map((event) => (
                    <AuditEventRow
                      key={event.id}
                      event={event}
                      projectId={data?.projectPublicId ?? projectId}
                      isExpanded={expanded.includes(event.id)}
                      onToggle={() => toggleExpanded(event.id)}
                      labels={labels}
                    />
                  ))}
                </ol>
              )}

              {events.length > 0 ? (
                <Pagination
                  page={filters.page}
                  totalPages={totalPages}
                  total={total}
                  pageSize={filters.pageSize}
                  onChange={(page) => setFilters((current) => ({ ...current, page }))}
                />
              ) : null}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function AuditEventRow({
  event,
  projectId,
  isExpanded,
  onToggle,
  labels,
}: {
  event: ProjectAuditEventDto;
  projectId: string;
  isExpanded: boolean;
  onToggle: () => void;
  labels: ReturnType<typeof useAuditLabels>;
}) {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const entityLink = resolveEntityLink(event, projectId, permissions);
  const changeEntries = Object.entries(event.changes ?? {});
  const before = formatSnapshot(event.before);
  const after = formatSnapshot(event.after);
  const timestamp = formatDate(event.timestampUtc, { dateStyle: "medium", timeStyle: "short" });
  const actor = event.actorName || t("audit.unknownActor");
  const detailId = `audit-event-detail-${event.id}`;

  return (
    <li className="px-4 py-3">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-medium">{labels.action(event.action)}</span>
            <Badge variant="outline" className="text-[10px]">
              {labels.module(event.module)}
            </Badge>
            {event.source !== "User" ? (
              <Badge variant="secondary" className="text-[10px]">
                {labels.source(event.source)}
              </Badge>
            ) : null}
          </div>
          <p className="text-xs text-muted-foreground">
            {actor} · {timestamp} · {labels.entityType(event.entityType)}
          </p>
          {event.description ? <p className="text-sm text-muted-foreground">{event.description}</p> : null}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {entityLink ? (
            <Link
              to={entityLink}
              className="inline-flex h-9 items-center rounded-md px-3 text-sm font-medium text-primary underline-offset-4 hover:bg-accent hover:underline"
            >
              <ExternalLink className="me-1.5 h-3.5 w-3.5" />
              {t("audit.openEntity")}
            </Link>
          ) : null}
          <Button
            type="button"
            size="sm"
            variant="ghost"
            aria-expanded={isExpanded}
            aria-controls={isExpanded ? detailId : undefined}
            // The visible label repeats for every row, so the accessible name adds the
            // action to keep the button list meaningful in screen readers.
            aria-label={`${isExpanded ? t("audit.hideDetails") : t("audit.showDetails")}: ${labels.action(event.action)}`}
            onClick={onToggle}
          >
            {isExpanded ? <ChevronUp className="me-1.5 h-3.5 w-3.5" /> : <ChevronDown className="me-1.5 h-3.5 w-3.5" />}
            {isExpanded ? t("audit.hideDetails") : t("audit.showDetails")}
          </Button>
        </div>
      </div>

      {isExpanded ? (
        <div id={detailId} className="mt-3 overflow-hidden rounded-lg border bg-muted/20">
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2 border-b px-3 py-2 text-[11px] sm:grid-cols-4">
            <EventMeta label={t("audit.meta.actor")} value={actor} />
            <EventMeta label={t("audit.meta.source")} value={labels.source(event.source)} />
            <EventMeta label={t("audit.meta.recorded")} value={timestamp} />
            <EventMeta label={t("audit.meta.record")} value={labels.entityType(event.entityType)} />
          </dl>

          <div className="space-y-3 p-3">
            {event.changedFields.length > 0 ? (
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="text-xs font-medium text-muted-foreground">{t("audit.changedFields")}</span>
                {event.changedFields.map((field) => (
                  <Badge key={field} variant="outline" className="text-[10px]">
                    {humanize(field)}
                  </Badge>
                ))}
              </div>
            ) : null}

            {event.detailsRestricted ? (
              <p className="rounded border border-dashed px-3 py-2 text-xs text-muted-foreground">
                {t("audit.detailsRestricted")}
              </p>
            ) : changeEntries.length > 0 ? (
              <div className="overflow-x-auto rounded border bg-background">
                <table className="w-full text-xs">
                  <thead>
                    <tr className="bg-muted/40 text-start text-muted-foreground">
                      <th scope="col" className="px-3 py-1.5 text-start font-medium">{t("audit.field")}</th>
                      <th scope="col" className="px-3 py-1.5 text-start font-medium">{t("audit.before")}</th>
                      <th scope="col" className="px-3 py-1.5 text-start font-medium">{t("audit.after")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {changeEntries.map(([field, change]) => (
                      <tr key={field} className="border-t align-top">
                        <th scope="row" className="px-3 py-1.5 text-start font-medium">{humanize(field)}</th>
                        <td className="px-3 py-1.5 text-muted-foreground">{formatChangeValue(change.from, t("audit.emptyValue"))}</td>
                        <td className="px-3 py-1.5">{formatChangeValue(change.to, t("audit.emptyValue"))}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-xs text-muted-foreground">{t("audit.noFieldDetail")}</p>
            )}

            {!event.detailsRestricted && (before || after) ? (
              <details className="rounded border bg-background">
                <summary className="cursor-pointer px-3 py-2 text-xs font-medium text-muted-foreground">
                  {t("audit.rawPayload")}
                </summary>
                <div className="grid gap-2 border-t p-2 md:grid-cols-2">
                  {before ? (
                    <div className="min-w-0">
                      <p className="mb-1 text-xs font-medium text-muted-foreground">{t("audit.recordedBefore")}</p>
                      <pre dir="ltr" className="max-h-56 overflow-auto rounded bg-muted/40 p-2 text-[11px]">{before}</pre>
                    </div>
                  ) : null}
                  {after ? (
                    <div className="min-w-0">
                      <p className="mb-1 text-xs font-medium text-muted-foreground">{t("audit.recordedAfter")}</p>
                      <pre dir="ltr" className="max-h-56 overflow-auto rounded bg-muted/40 p-2 text-[11px]">{after}</pre>
                    </div>
                  ) : null}
                </div>
              </details>
            ) : null}
          </div>

          <dl className="flex flex-wrap gap-x-4 gap-y-1 border-t px-3 py-2 text-[11px] text-muted-foreground">
            <div className="flex min-w-0 gap-1">
              <dt className="font-medium">{t("audit.auditId")}:</dt>
              <dd dir="ltr" className="truncate">{event.id}</dd>
            </div>
            {event.correlationId ? (
              <div className="flex min-w-0 gap-1">
                <dt className="font-medium">{t("audit.correlationId")}:</dt>
                <dd dir="ltr" className="truncate">{event.correlationId}</dd>
              </div>
            ) : null}
          </dl>
        </div>
      ) : null}
    </li>
  );
}

function EventMeta({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="font-medium text-muted-foreground">{label}</dt>
      <dd className="truncate text-foreground">{value}</dd>
    </div>
  );
}

function formatChangeValue(value: string | null | undefined, emptyLabel: string): string {
  if (value === null || value === undefined || value === "") return emptyLabel;
  return value;
}

/**
 * Links to the owning project module using the project's public identifier. Internal
 * entity identifiers never appear in the URL.
 */
function resolveEntityLink(event: ProjectAuditEventDto, projectId: string, permissions: string[]): string | null {
  if (!projectId) return null;
  const base = `/projects/${encodeURIComponent(projectId)}`;
  const allowed = (permission: string) => hasPermission(permissions, permission);
  switch (event.module) {
    case "Planning":
      return allowed(Permissions.Planning.View) ? `${base}/plan` : null;
    case "WorkItems":
    case "TimeTracking":
      return allowed(Permissions.WorkItem.View) ? `${base}/tasks` : null;
    case "Meetings":
      return allowed(Permissions.Meeting.View) ? `${base}/meetings` : null;
    case "Risk":
    case "Issues":
      return allowed(Permissions.Risk.View) ? `${base}/risks-issues` : null;
    case "Finance":
      return allowed(Permissions.ProjectFinance.View) ? `${base}/financials` : null;
    case "Documents":
      return allowed(Permissions.Document.View) ? `${base}/documents` : null;
    case "Resources":
      return allowed(Permissions.Resources.View) ? `${base}/team` : null;
    case "Governance":
      if (event.entityType === "ProjectStatusReport") {
        return allowed(Permissions.StatusReport.View) ? `${base}/status-reports` : null;
      }
      return allowed(Permissions.ChangeRequest.View) ? `${base}/change-tracker` : null;
    case "Workflow":
      return allowed(Permissions.Workflow.View) ? `${base}/workflows` : null;
    case "ProjectManagement":
      return `${base}/overview`;
    default:
      return null;
  }
}
