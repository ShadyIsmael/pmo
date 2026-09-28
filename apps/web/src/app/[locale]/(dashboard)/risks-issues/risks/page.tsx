"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowUpRight, FolderKanban, ListFilter, Plus, Search, ShieldAlert, X } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { CreateRiskForm } from "@/components/risks/CreateRiskForm";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState, ErrorState } from "@/components/ui/state-display";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SearchableSelect, type SelectOption } from "@/components/ui/searchable-select";
import { SideSheet, SideSheetBody } from "@/components/ui/side-sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { RiskImpactTypeText, RiskLevelText, RiskStatusText } from "@/components/risks/RiskValueText";
import { useRisks } from "@/hooks/risks/useRisks";
import { useResources } from "@/hooks/resources/useResources";
import { RISK_LEVELS, RISK_STATUSES, RiskLevelHelper, RiskStatusHelper } from "@/lib/enums";
import { formatDate } from "@/lib/utils";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";

export default function RiskRegisterPage() {
  const t = useTranslations("risks");
  const enums = useTranslations("enums");
  const locale = useLocale();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const canCreateRisk = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Create));
  const projectId = searchParams.get("projectId") ?? undefined;
  const [ownerResourceId, setOwnerResourceId] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const query = useRisks(projectId, true, ownerResourceId || undefined);
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });
  const risks = useMemo(() => query.data?.items ?? [], [query.data?.items]);
  const [search, setSearch] = useState("");
  const [project, setProject] = useState("all");
  const [level, setLevel] = useState("all");
  const [status, setStatus] = useState("all");

  const projects = useMemo(() => [...new Set(risks.map((risk) => risk.projectName).filter(Boolean))].sort(), [risks]);
  const filteredRisks = useMemo(() => {
    const queryText = search.trim().toLowerCase();
    return risks.filter((risk) => {
      const matchesSearch = !queryText || [risk.title, risk.projectName, risk.ownerResourceName].some((value) => value?.toLowerCase().includes(queryText));
      return matchesSearch && (project === "all" || risk.projectName === project) && (level === "all" || risk.riskLevel === level) && (status === "all" || risk.status === status);
    });
  }, [level, project, risks, search, status]);

  const ownerOptions = useMemo<SelectOption[]>(() => (resourcesQuery.data?.items ?? []).map((resource) => ({ value: resource.id, label: resource.name, keywords: [resource.email ?? "", resource.jobTitle ?? ""] })), [resourcesQuery.data?.items]);
  const clearFilters = () => { setSearch(""); setProject("all"); setLevel("all"); setStatus("all"); setOwnerResourceId(""); };
  const hasFilters = Boolean(search || project !== "all" || level !== "all" || status !== "all" || ownerResourceId);
  const openRisk = (riskId: string) => router.push(`/${locale}/risks-issues/risks/${riskId}?from=risks`);

  return <div className="space-y-6">
    <PageHeader title={t("title")} description={t("description")} actions={canCreateRisk ? <Button size="sm" onClick={() => setCreateOpen(true)}><Plus className="me-1.5 h-3.5 w-3.5" />{t("create")}</Button> : undefined} />
    <Card className="shadow-sm"><CardContent className="space-y-4 p-4"><div className="flex items-center gap-2 text-sm font-semibold"><ListFilter className="h-4 w-4 text-primary" />{t("riskRegister")}</div><div className="grid gap-2 md:grid-cols-2 xl:grid-cols-[minmax(240px,1.6fr)_repeat(4,minmax(150px,1fr))_auto]"><div className="relative"><Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("searchPlaceholder")} className="ps-9" /></div><Select value={project} onValueChange={setProject}><SelectTrigger><SelectValue placeholder={t("allProjects")} /></SelectTrigger><SelectContent><SelectItem value="all">{t("allProjects")}</SelectItem>{projects.map((name) => <SelectItem key={name} value={name}>{name}</SelectItem>)}</SelectContent></Select><SearchableSelect options={ownerOptions} value={ownerResourceId} onValueChange={setOwnerResourceId} placeholder={t("allOwners")} searchPlaceholder={t("searchOwners")} emptyText={t("noOwners")} loading={resourcesQuery.isLoading} clearable /><Select value={level} onValueChange={setLevel}><SelectTrigger><SelectValue placeholder={t("allLevels")} /></SelectTrigger><SelectContent><SelectItem value="all">{t("allLevels")}</SelectItem>{RISK_LEVELS.map((value) => <SelectItem key={value} value={value}>{enums(RiskLevelHelper.getLabelKey(value) ?? "unknown")}</SelectItem>)}</SelectContent></Select><Select value={status} onValueChange={setStatus}><SelectTrigger><SelectValue placeholder={t("allStatuses")} /></SelectTrigger><SelectContent><SelectItem value="all">{t("allStatuses")}</SelectItem>{RISK_STATUSES.map((value) => <SelectItem key={value} value={value}>{enums(RiskStatusHelper.getLabelKey(value) ?? "unknown")}</SelectItem>)}</SelectContent></Select>{hasFilters ? <Button variant="ghost" size="sm" onClick={clearFilters} className="justify-self-start text-muted-foreground"><X className="me-1.5 h-3.5 w-3.5" />{t("clearFilters")}</Button> : <span />}</div></CardContent></Card>
    <Card className="shadow-sm"><CardContent className="p-0">{query.isLoading ? <div className="p-5"><Skeleton className="h-40" /></div> : query.isError ? <ErrorState title={t("loadError")} onRetry={() => void query.refetch()} /> : filteredRisks.length === 0 ? <EmptyState title={hasFilters ? t("noFilteredRisks") : t("empty")} icon={ShieldAlert} /> : <Table><TableHeader><TableRow><TableHead className="text-center">{t("titleField")}</TableHead><TableHead className="text-center">{t("project")}</TableHead><TableHead className="text-center">{t("riskLevel")}</TableHead><TableHead className="text-center">{t("impact")}</TableHead><TableHead className="text-center">{t("owner")}</TableHead><TableHead className="text-center">{t("status")}</TableHead><TableHead className="text-center">{t("lastUpdate")}</TableHead><TableHead className="w-12 text-center"><span className="sr-only">{t("openRisk")}</span></TableHead></TableRow></TableHeader><TableBody>{filteredRisks.map((risk) => <TableRow key={risk.id} onClick={() => openRisk(risk.id)} className="cursor-pointer"><TableCell className="max-w-[260px]"><Link href={`/${locale}/risks-issues/risks/${encodeURIComponent(risk.id)}?from=risks`} className="block truncate font-semibold underline-offset-4 hover:underline" onClick={(event) => event.stopPropagation()}>{risk.title}</Link><p className="mt-0.5 truncate text-[11px] text-muted-foreground">{risk.id}</p></TableCell><TableCell className="max-w-[180px]"><Link href={`/${locale}/projects/${encodeURIComponent(risk.projectId)}?tab=risks-issues`} onClick={(event) => event.stopPropagation()} className="inline-flex max-w-full items-center gap-1.5 truncate font-medium text-blue-700 hover:text-blue-800 hover:underline dark:text-blue-300 dark:hover:text-blue-200"><FolderKanban className="h-3.5 w-3.5 shrink-0" />{risk.projectName || "—"}</Link></TableCell><TableCell><RiskLevelText value={risk.riskLevel} /></TableCell><TableCell className="max-w-[240px]"><div className="flex flex-wrap gap-1">{risk.impactTypes?.length ? risk.impactTypes.map((type) => <RiskImpactTypeText key={type} value={type} />) : <span className="text-muted-foreground">{t("noImpactRecorded")}</span>}</div></TableCell><TableCell>{risk.ownerResourceName || "—"}</TableCell><TableCell><RiskStatusText value={risk.status} /></TableCell><TableCell className="whitespace-nowrap text-muted-foreground">{formatDate(risk.lastActionUpdateAt ?? risk.updatedAt ?? risk.createdAt, locale === "ar" ? "ar-EG" : "en-GB")}</TableCell><TableCell><ArrowUpRight className="h-4 w-4 text-muted-foreground" /></TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card>
    <SideSheet open={createOpen} onOpenChange={setCreateOpen} title={t("createTitle")} description={t("createDescription")} size="lg" closeLabel={t("cancel")}><SideSheetBody className="p-0"><CreateRiskForm returnTo={`/${locale}/risks-issues/risks`} onCancel={() => setCreateOpen(false)} onCreated={() => setCreateOpen(false)} /></SideSheetBody></SideSheet>
  </div>;
}
