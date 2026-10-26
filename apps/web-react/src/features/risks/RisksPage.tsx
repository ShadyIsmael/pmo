import * as React from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { ArrowUpRight, FolderKanban, ListFilter, Plus, Search, ShieldAlert, X } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { EmptyState, ErrorState } from "@components/ui/StateDisplay";
import { Input } from "@components/ui/Input";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { getRiskLevelTextClass, getRiskStatusTextClass } from "@lib/status-presentation";
import { formatDate } from "@lib/utils";
import { useResources } from "@features/resources/api/resources";
import { useRisks, type RiskLevel, type RiskStatus } from "./api/risks";
import { RiskCreatePage } from "./RiskCreatePage";

const RISK_LEVELS: RiskLevel[] = ["Low", "Medium", "High", "Critical"];
const RISK_STATUSES: RiskStatus[] = ["Identified", "ActionRequired", "FollowUp", "Mitigated", "Closed"];

function enumLabel(t: (key: string) => string, namespace: string, value: string | null | undefined): string {
  if (!value) return "—";
  const key = `${namespace}.${value.charAt(0).toLowerCase()}${value.slice(1)}`;
  const label = t(key);
  return label === key ? value : label;
}

const impactTextClasses: Record<string, string> = {
  Schedule: "text-blue-700 dark:text-blue-300",
  Budget: "text-violet-700 dark:text-violet-300",
  Scope: "text-indigo-700 dark:text-indigo-300",
  Quality: "text-teal-700 dark:text-teal-300",
  Resources: "text-cyan-700 dark:text-cyan-300",
  Procurement: "text-amber-700 dark:text-amber-300",
  Compliance: "text-orange-700 dark:text-orange-300",
  Reputation: "text-pink-700 dark:text-pink-300",
  Operations: "text-slate-700 dark:text-slate-300",
  Other: "text-muted-foreground",
};

export function RisksPage() {
  const { locale, t } = useI18n();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Create));
  const projectId = searchParams.get("projectId") ?? undefined;
  const [ownerResourceId, setOwnerResourceId] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [project, setProject] = React.useState("all");
  const [level, setLevel] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [createOpen, setCreateOpen] = React.useState(false);
  const query = useRisks({ ...(projectId ? { projectId } : {}), ...(ownerResourceId ? { ownerResourceId } : {}) });
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });
  const risks = React.useMemo(() => query.data?.items ?? [], [query.data?.items]);

  const projects = React.useMemo(() => [...new Set(risks.map((risk) => risk.projectName).filter(Boolean))].sort(), [risks]);
  const filteredRisks = React.useMemo(() => {
    const queryText = search.trim().toLowerCase();
    return risks.filter((risk) => {
      const matchesSearch = !queryText || [risk.title, risk.projectName, risk.ownerResourceName].some((value) => value?.toLowerCase().includes(queryText));
      return matchesSearch && (project === "all" || risk.projectName === project) && (level === "" || risk.riskLevel === level) && (status === "" || risk.status === status);
    });
  }, [level, project, risks, search, status]);

  const ownerOptions = React.useMemo(() => [
    { value: "", label: t("risks.allOwners") },
    ...(resourcesQuery.data?.items ?? []).map((resource) => ({
      value: resource.id,
      label: resource.name,
      keywords: [resource.email ?? "", resource.jobTitle ?? ""],
    })),
  ], [resourcesQuery.data?.items, t]);
  const clearFilters = () => { setSearch(""); setProject("all"); setLevel(""); setStatus(""); setOwnerResourceId(""); };
  const hasFilters = Boolean(search || project !== "all" || level || status || ownerResourceId);
  const openRisk = (riskId: string) => navigate(`/risks-issues/risks/${encodeURIComponent(riskId)}?from=risks`);

  return (
    <div className="space-y-6">
      <PageHeader title={t("risks.title")} description={t("risks.description")} actions={canCreate ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-3.5 w-3.5" />{t("risks.create")}</Button> : undefined} />
      <div className="space-y-4 rounded-lg border bg-muted/20 px-3 py-3">
        <div className="flex items-center gap-2 text-sm font-semibold"><ListFilter className="h-4 w-4 text-primary" />{t("risks.riskRegister")}</div>
        <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-[minmax(240px,1.6fr)_repeat(4,minmax(150px,1fr))_auto]">
          <div className="relative">
            <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input aria-label={t("risks.searchPlaceholder")} value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("risks.searchPlaceholder")} className="ps-9" />
          </div>
          <select className="input-field" value={project} onChange={(event) => setProject(event.target.value)} aria-label={t("risks.allProjects")}>
            <option value="all">{t("risks.allProjects")}</option>
            {projects.map((name) => <option key={name} value={name}>{name}</option>)}
          </select>
          <SearchableSelect aria-label={t("risks.allOwners")} options={ownerOptions} value={ownerResourceId} onValueChange={setOwnerResourceId} placeholder={t("risks.allOwners")} searchPlaceholder={t("risks.searchOwners")} emptyText={t("risks.noOwners")} loading={resourcesQuery.isLoading} loadingText={t("common.loading")} />
          <select className="input-field" value={level} onChange={(event) => setLevel(event.target.value)} aria-label={t("risks.allLevels")}>
            <option value="">{t("risks.allLevels")}</option>
            {RISK_LEVELS.map((value) => <option key={value} value={value}>{enumLabel(t, "enums.riskLevel", value)}</option>)}
          </select>
          <select className="input-field" value={status} onChange={(event) => setStatus(event.target.value)} aria-label={t("risks.allStatuses")}>
            <option value="">{t("risks.allStatuses")}</option>
            {RISK_STATUSES.map((value) => <option key={value} value={value}>{enumLabel(t, "enums.riskStatus", value)}</option>)}
          </select>
          {hasFilters ? <Button variant="ghost" size="sm" onClick={clearFilters} className="justify-self-start text-muted-foreground"><X className="me-1.5 h-3.5 w-3.5" />{t("risks.clearFilters")}</Button> : <span />}
        </div>
        {resourcesQuery.isError ? <p role="alert" className="text-xs text-destructive">{t("common.errorLoading")}</p> : null}
      </div>
      <div className="rounded-lg border shadow-sm">
        {query.isLoading ? <div className="p-5 text-center text-sm text-muted-foreground">{t("common.loading")}</div>
          : query.isError ? <ErrorState title={t("risks.loadError")} onRetry={() => void query.refetch()} />
            : filteredRisks.length === 0 ? <EmptyState title={hasFilters ? t("risks.noFilteredRisks") : t("risks.empty")} icon={<ShieldAlert className="h-8 w-8" />} />
              : <Table>
                <TableHeader><TableRow><TableHead>{t("risks.titleField")}</TableHead><TableHead>{t("risks.project")}</TableHead><TableHead>{t("risks.riskLevel")}</TableHead><TableHead>{t("risks.impact")}</TableHead><TableHead>{t("risks.owner")}</TableHead><TableHead>{t("risks.status")}</TableHead><TableHead>{t("risks.lastUpdate")}</TableHead><TableHead className="w-12 text-center"><span className="sr-only">{t("risks.openRisk")}</span></TableHead></TableRow></TableHeader>
                <TableBody>
                  {filteredRisks.map((risk) => (
                    <TableRow key={risk.id} onClick={() => openRisk(risk.id)} className="cursor-pointer">
                      <TableCell className="max-w-[260px]">
                        <Link to={`/risks-issues/risks/${encodeURIComponent(risk.id)}?from=risks`} onClick={(event) => event.stopPropagation()} className="block truncate font-semibold underline-offset-4 hover:underline">{risk.title}</Link>
                        <p className="mt-0.5 truncate text-[11px] text-muted-foreground">{risk.id}</p>
                      </TableCell>
                      <TableCell className="max-w-[180px]"><Link to={`/projects/${encodeURIComponent(risk.projectPublicId)}/risks-issues`} onClick={(event) => event.stopPropagation()} className="inline-flex max-w-full items-center gap-1.5 truncate font-medium text-blue-700 hover:text-blue-800 hover:underline dark:text-blue-300 dark:hover:text-blue-200"><FolderKanban className="h-3.5 w-3.5 shrink-0" />{risk.projectName || "—"}</Link></TableCell>
                      <TableCell><span className={`font-semibold ${getRiskLevelTextClass(risk.riskLevel)}`}>{enumLabel(t, "enums.riskLevel", risk.riskLevel)}</span></TableCell>
                      <TableCell className="max-w-[240px]"><div className="flex flex-wrap gap-x-2 gap-y-1">{risk.impactTypes?.length ? risk.impactTypes.map((type) => <span key={type} className={`text-xs font-medium ${impactTextClasses[type] ?? impactTextClasses.Other}`}>{enumLabel(t, "enums.riskImpactType", type)}</span>) : <span className="text-muted-foreground">{t("risks.noImpactRecorded")}</span>}</div></TableCell>
                      <TableCell>{risk.ownerResourceName || "—"}</TableCell>
                      <TableCell><span className={`font-semibold ${getRiskStatusTextClass(risk.status)}`}>{enumLabel(t, "enums.riskStatus", risk.status)}</span></TableCell>
                      <TableCell className="whitespace-nowrap text-muted-foreground">{formatDate(risk.lastActionUpdateAt ?? risk.updatedAt ?? risk.createdAt, locale === "ar" ? "ar-EG" : "en-GB")}</TableCell>
                      <TableCell><ArrowUpRight className="h-4 w-4 text-muted-foreground" /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>}
      </div>
      <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("risks.createTitle")} description={t("risks.createDescription")} size="lg" closeLabel={t("risks.cancel")}>
        <SideSheetBody className="p-0">
          <RiskCreatePage embedded onCreated={() => setCreateOpen(false)} onCancel={() => setCreateOpen(false)} />
        </SideSheetBody>
      </SideSheet>
    </div>
  );
}
