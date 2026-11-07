import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { AlertTriangle, Check, Save } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader } from "@components/ui/Card";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Input } from "@components/ui/Input";
import { Skeleton } from "@components/ui/Skeleton";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { toast } from "@hooks/use-toast";
import {
  useProjectReportingConfiguration,
  useProjectReportingOptions,
  useSaveProjectReportingConfiguration,
  type ProjectDto,
  type ProjectReportingPlanOptionDto,
  type SaveProjectReportingConfigurationInput,
} from "./api/projects";

type ProjectContext = { projectId: string; project: ProjectDto };
type PlanSelection = { include: boolean; workingVersionId: string; baselineVersionId: string };

const SECTION_KEYS = [
  ["ExecutiveSummary", "projects.reporting.sections.executiveSummary"],
  ["PerformanceKPIs", "projects.reporting.sections.performanceKpis"],
  ["PlanPerformance", "projects.reporting.sections.planPerformance"],
  ["Milestones", "projects.reporting.sections.milestones"],
  ["RisksIssues", "projects.reporting.sections.risksIssues"],
  ["DecisionsRequired", "projects.reporting.sections.decisionsRequired"],
] as const;

function latestVersion(plan: ProjectReportingPlanOptionDto, type: "Working" | "Baseline") {
  const requiredStatus = type === "Working" ? "Active" : "Approved";
  return plan.versions.filter(item => item.type === type && item.status === requiredStatus)
    .sort((left, right) => right.versionNumber - left.versionNumber)[0];
}

function nextUtcDate(value?: string) {
  const date = value ? new Date(value) : new Date();
  date.setUTCDate(date.getUTCDate() + (value ? 1 : 0));
  return date.toISOString().slice(0, 10);
}

export function ProjectReportingConfigurationPage() {
  const { projectId, project } = useOutletContext<ProjectContext>();
  const { t, locale, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const confirm = useConfirm();
  const optionsQuery = useProjectReportingOptions(projectId);
  const configurationQuery = useProjectReportingConfiguration(projectId);
  const save = useSaveProjectReportingConfiguration(projectId);
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.ManageConfiguration) && canMutate && !effectiveReadOnly);
  const candidates = React.useMemo(() => (optionsQuery.data?.plans ?? []).filter(plan => plan.planType === "Execution"), [optionsQuery.data?.plans]);
  const [selections, setSelections] = React.useState<Record<string, PlanSelection>>({});
  const [effectiveFrom, setEffectiveFrom] = React.useState("");
  const [sections, setSections] = React.useState<string[]>(SECTION_KEYS.map(([key]) => key));
  const [error, setError] = React.useState<string | null>(null);
  const [sheetOpen, setSheetOpen] = React.useState(false);

  React.useEffect(() => {
    if (!optionsQuery.data || configurationQuery.isLoading) return;
    const configured = new Map((configurationQuery.data?.plans ?? []).map(plan => [plan.planId, plan]));
    const next: Record<string, PlanSelection> = {};
    for (const plan of candidates) {
      const existing = configured.get(plan.planId);
      const activeWorking = latestVersion(plan, "Working");
      const approvedBaseline = latestVersion(plan, "Baseline");
      next[plan.planId] = {
        include: existing?.includeInConsolidatedReporting ?? false,
        workingVersionId: existing?.workingVersionId ?? activeWorking?.id ?? "",
        baselineVersionId: existing?.baselineVersionId ?? approvedBaseline?.id ?? "",
      };
    }
    setSelections(next);
    setSections(configurationQuery.data?.includedSections?.length ? configurationQuery.data.includedSections : SECTION_KEYS.map(([key]) => key));
    setEffectiveFrom(nextUtcDate(configurationQuery.data?.effectiveFrom));
  }, [candidates, optionsQuery.data, configurationQuery.data, configurationQuery.isLoading]);

  function updatePlan(planId: string, patch: Partial<PlanSelection>) {
    setSelections(current => ({ ...current, [planId]: { ...current[planId], ...patch } }));
  }

  function toggleSection(key: string) {
    setSections(current => current.includes(key) ? current.filter(item => item !== key) : [...current, key]);
  }

  async function saveConfiguration() {
    const selected = candidates.filter(plan => selections[plan.planId]?.include);
    if (selected.length === 0) {
      setError(t("projects.reporting.selectOneExecutionPlan"));
      return;
    }
    if (selected.some(plan => !selections[plan.planId]?.workingVersionId || !selections[plan.planId]?.baselineVersionId)) {
      setError(t("projects.reporting.missingVersionOrBaseline"));
      return;
    }
    const accepted = await confirm({ description: t("projects.reporting.confirmConfigurationChange") });
    if (!accepted) return;
    const input: SaveProjectReportingConfigurationInput = {
      effectiveFrom,
      reportingFrequency: "Weekly",
      timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
      currencyCode: project.currency,
      includedSections: sections,
      plans: selected.map(plan => ({
        planId: plan.planId,
        workingVersionId: selections[plan.planId].workingVersionId,
        baselineVersionId: selections[plan.planId].baselineVersionId,
        includeInConsolidatedReporting: true,
      })),
    };
    setError(null);
    try {
      await save.mutateAsync(input);
      await Promise.all([configurationQuery.refetch(), optionsQuery.refetch()]);
      toast({ title: t("projects.reporting.configurationSaved") });
      setSheetOpen(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("projects.reporting.saveFailed"));
    }
  }

  if (optionsQuery.isLoading || configurationQuery.isLoading) {
    return <div className="space-y-4" role="status" aria-label={t("common.loading")}><Skeleton className="h-16" /><Skeleton className="h-60" /><Skeleton className="h-40" /></div>;
  }
  if (optionsQuery.isError || configurationQuery.isError) {
    return <Card><CardContent className="p-8 text-center"><AlertTriangle className="mx-auto h-8 w-8 text-destructive" /><p className="mt-3 text-sm">{t("projects.reporting.loadFailed")}</p><Button className="mt-4" size="sm" variant="outline" onClick={() => { void optionsQuery.refetch(); void configurationQuery.refetch(); }}>{t("common.retry")}</Button></CardContent></Card>;
  }

  return <div dir={locale === "ar" ? "rtl" : "ltr"} className="space-y-4">
    <Card>
      <CardHeader className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div><h2 className="text-base font-semibold">{t("projects.reporting.configurationTitle")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("projects.reporting.configurationDescription")}</p></div>
        {canManage ? <Button size="sm" onClick={() => { setError(null); setSheetOpen(true); }}>{configurationQuery.data ? t("common.edit") : t("projects.reporting.configureAction")}</Button> : null}
      </CardHeader>
      <CardContent className="space-y-3">
        {configurationQuery.data ? <><div className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border bg-muted/20 px-3 py-2 text-xs"><span><strong>{t("projects.reporting.configurationVersion")}:</strong> {configurationQuery.data.version}</span><span><strong>{t("projects.reporting.effectiveFrom")}:</strong> {formatDate(configurationQuery.data.effectiveFrom)}</span><span><strong>{t("projects.reporting.currency")}:</strong> {configurationQuery.data.currencyCode}</span><span className="text-success"><Check className="me-1 inline h-3.5 w-3.5" />{t("projects.reporting.approvedConfiguration")}</span></div><div className="flex flex-wrap gap-2">{configurationQuery.data.plans.filter(item => item.includeInConsolidatedReporting).map(item => <span key={item.planId} className="rounded-full border px-2.5 py-1 text-xs">{item.planName} · {t("projects.reporting.officialScope")}</span>)}</div></> : <p className="rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-xs text-muted-foreground">{t("projects.reporting.noConfiguration")}</p>}
      </CardContent>
    </Card>
    <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={t("projects.reporting.configurationTitle")} description={t("projects.reporting.configurationDescription")}>
      <SideSheetBody className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-3">
          <label className="space-y-1 text-xs"><span className="font-medium">{t("projects.reporting.effectiveFrom")}</span><Input type="date" value={effectiveFrom} onChange={event => setEffectiveFrom(event.target.value)} disabled={!canManage} /></label>
          <label className="space-y-1 text-xs"><span className="font-medium">{t("projects.reporting.frequency")}</span><Input value={t("projects.reporting.weekly")} readOnly /></label>
          <label className="space-y-1 text-xs"><span className="font-medium">{t("projects.reporting.currency")}</span><Input value={project.currency} readOnly /></label>
        </div>
        <div className="space-y-2">
          <h3 className="text-sm font-semibold">{t("projects.reporting.executionPlans")}</h3>
          {candidates.length === 0 ? <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">{t("projects.reporting.noExecutionPlans")}</p> : candidates.map(plan => {
            const selection = selections[plan.planId] ?? { include: false, workingVersionId: "", baselineVersionId: "" };
            const workingOptions = plan.versions.filter(version => version.type === "Working");
            const baselineOptions = plan.versions.filter(version => version.type === "Baseline");
            const hasCurrent = Boolean(latestVersion(plan, "Working"));
            const hasBaseline = Boolean(latestVersion(plan, "Baseline"));
            return <article key={plan.planId} className="rounded-lg border p-3">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <label className="flex min-w-0 items-start gap-2 text-sm"><input type="checkbox" className="mt-1 accent-primary" checked={selection.include} disabled={!canManage || !hasCurrent || !hasBaseline} onChange={event => updatePlan(plan.planId, { include: event.target.checked })} /><span className="min-w-0"><span className="block truncate font-medium">{plan.name}</span><span className="text-xs text-muted-foreground">{t("projects.reporting.executionPlan")} · {plan.status}</span></span></label>
                {selection.include ? <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium text-primary">{t("projects.reporting.officialScope")}</span> : null}
              </div>
              <div className="mt-3 grid gap-3 sm:grid-cols-2">
                <label className="space-y-1 text-xs"><span>{t("projects.reporting.workingVersion")}</span><select className="input-field w-full" value={selection.workingVersionId} disabled={!canManage || !selection.include} onChange={event => updatePlan(plan.planId, { workingVersionId: event.target.value })}><option value="">{t("projects.reporting.selectWorkingVersion")}</option>{workingOptions.map(version => <option key={version.id} value={version.id} disabled={version.status !== "Active"}>{version.name} · v{version.versionNumber} · {version.status}</option>)}</select></label>
                <label className="space-y-1 text-xs"><span>{t("projects.reporting.reportingBaseline")}</span><select className="input-field w-full" value={selection.baselineVersionId} disabled={!canManage || !selection.include} onChange={event => updatePlan(plan.planId, { baselineVersionId: event.target.value })}><option value="">{t("projects.reporting.selectBaseline")}</option>{baselineOptions.map(version => <option key={version.id} value={version.id} disabled={version.status !== "Approved"}>{version.name} · v{version.versionNumber} · {version.status}</option>)}</select></label>
              </div>
              {!hasCurrent || !hasBaseline ? <p className="mt-2 text-xs text-warning">{t("projects.reporting.planNeedsActiveVersionAndBaseline")}</p> : null}
            </article>;
          })}
        </div>
        <div className="space-y-2">
          <h3 className="text-sm font-semibold">{t("projects.reporting.includedSections")}</h3>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">{SECTION_KEYS.map(([key, label]) => <label key={key} className="flex items-center gap-2 rounded-md border px-3 py-2 text-xs"><input type="checkbox" className="accent-primary" checked={sections.includes(key)} disabled={!canManage} onChange={() => toggleSection(key)} />{t(label)}</label>)}</div>
        </div>
        {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
        {effectiveReadOnly ? <p className="text-xs text-muted-foreground">{t("common.readOnlyView")}</p> : null}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>{t("common.cancel")}</Button>
        <Button type="button" onClick={() => void saveConfiguration()} disabled={!canManage || save.isPending || !effectiveFrom}><Save className="me-1.5 h-4 w-4" />{save.isPending ? t("common.saving") : t("common.save")}</Button>
      </SideSheetFooter>
    </SideSheet>
  </div>;
}
