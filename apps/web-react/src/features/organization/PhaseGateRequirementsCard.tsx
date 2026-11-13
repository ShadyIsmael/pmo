import * as React from "react";
import { FileText, Plus, Search, X, ChevronDown, ChevronUp } from "lucide-react";
import { ApiError } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { useDocumentCategories, documentApi, useProjectDocuments, type DocumentListItem } from "@features/projects/api/documents";
import {
  usePhaseGateRequirements,
  usePhaseGateTransitions,
  useSavePhaseGateRequirements,
  usePhaseGateTemplatesByCategory,
  type PhaseGateRequirement,
  type PhaseGateRequirementType,
  type PhaseGateTransition,
  type TemplateCategoryGroup,
} from "./api/phase-gate-requirements";

type RequirementDraft = Pick<PhaseGateRequirement, "templateId" | "templateVersionId" | "templateName" | "category" | "version" | "requirementType">;

function labelForTransition(transition: PhaseGateTransition, locale: string) {
  const source = locale === "ar" ? transition.sourceLabelAr : transition.sourceLabelEn;
  const target = locale === "ar" ? transition.targetLabelAr : transition.targetLabelEn;
  return `${source} → ${target}`;
}

function requirementsEqual(left: RequirementDraft[], right: PhaseGateRequirement[]) {
  if (left.length !== right.length) return false;
  const sort = <T extends { templateId: string }>(items: T[]) => [...items].sort((a, b) => a.templateId.localeCompare(b.templateId));
  return sort(left).every((item, index) => {
    const other = sort(right)[index];
    return item.templateId === other.templateId
      && item.templateVersionId === other.templateVersionId
      && item.requirementType === other.requirementType;
  });
}

function isConflict(error: unknown) {
  return error instanceof ApiError && (error.status === 409 || error.status === 412);
}

export function PhaseGateRequirementsCard({ organizationId, canManage }: { organizationId: string; canManage: boolean }) {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const canViewDocuments = Boolean(user && hasPermission(user.permissions, Permissions.Document.View));
  const canManageRequirements = canManage && canViewDocuments;
  const transitions = usePhaseGateTransitions(organizationId, canViewDocuments);
  const [transitionKey, setTransitionKey] = React.useState("");
  const availableTransitions = transitions.data ?? [];

  React.useEffect(() => {
    if (!availableTransitions.length) {
      setTransitionKey("");
      return;
    }
    const stillAvailable = availableTransitions.some(item => transitionKey === `${item.sourcePhaseId}:${item.targetPhaseId}`);
    if (!stillAvailable) setTransitionKey(`${availableTransitions[0].sourcePhaseId}:${availableTransitions[0].targetPhaseId}`);
  }, [availableTransitions, transitionKey]);

  const selectedTransition = availableTransitions.find(item => transitionKey === `${item.sourcePhaseId}:${item.targetPhaseId}`) ?? null;
  const configuration = usePhaseGateRequirements(organizationId, selectedTransition, canViewDocuments);
  const save = useSavePhaseGateRequirements(organizationId, selectedTransition);
  const [draft, setDraft] = React.useState<RequirementDraft[]>([]);
  const [search, setSearch] = React.useState("");
  const [expandedCategories, setExpandedCategories] = React.useState<Set<string>>(new Set());
  const [feedback, setFeedback] = React.useState<{ kind: "success" | "error"; message: string; conflict?: boolean } | null>(null);
  const [loadingTemplateId, setLoadingTemplateId] = React.useState<string | null>(null);
  const [templateVersionError, setTemplateVersionError] = React.useState(false);

  // Use category-based templates when a transition is selected
  const templatesByCategory = usePhaseGateTemplatesByCategory(
    organizationId,
    selectedTransition?.sourcePhaseId ?? "",
    undefined,
    Boolean(selectedTransition && organizationId && canViewDocuments)
  );

  const config = configuration.data;
  React.useEffect(() => {
    if (!config || config.organizationId !== organizationId || config.sourcePhaseId !== selectedTransition?.sourcePhaseId || config.targetPhaseId !== selectedTransition?.targetPhaseId) return;
    setDraft(config.requirements.map(item => ({
      templateId: item.templateId,
      templateVersionId: item.templateVersionId,
      templateName: item.templateName,
      category: item.category ?? null,
      version: item.version,
      requirementType: item.requirementType,
    })));
    setFeedback(null);
    setTemplateVersionError(false);
  }, [config, organizationId, selectedTransition?.sourcePhaseId, selectedTransition?.targetPhaseId]);

  const templateGroups = templatesByCategory.data ?? [];
  const allTemplates = React.useMemo(() => templateGroups.flatMap(group => group.templates), [templateGroups]);
  const selectedTemplateIds = React.useMemo(() => new Set(draft.map(item => item.templateId)), [draft]);
  const dirty = config?.organizationId === organizationId
    && config.sourcePhaseId === selectedTransition?.sourcePhaseId
    && config.targetPhaseId === selectedTransition?.targetPhaseId
    ? !requirementsEqual(draft, config.requirements)
    : false;

  const toggleCategory = (categoryId: string) => {
    setExpandedCategories(prev => {
      const next = new Set(prev);
      if (next.has(categoryId)) next.delete(categoryId);
      else next.add(categoryId);
      return next;
    });
  };

  const addTemplate = async (template: TemplateCategoryGroup["templates"][0]) => {
    if (!canManageRequirements || selectedTemplateIds.has(template.templateId) || loadingTemplateId) return;
    setLoadingTemplateId(template.templateId);
    setTemplateVersionError(false);
    try {
      // The template from category endpoint already has version info
      setDraft(current => current.some(item => item.templateId === template.templateId) ? current : [...current, {
        templateId: template.templateId,
        templateVersionId: template.templateVersionId,
        templateName: template.templateName,
        category: template.category,
        version: template.version,
        requirementType: "Mandatory",
      }]);
      setFeedback(null);
    } catch {
      setTemplateVersionError(true);
    } finally {
      setLoadingTemplateId(null);
    }
  };

  const saveDraft = async () => {
    if (!config || !selectedTransition || !canManageRequirements || !dirty || save.isPending) return;
    setFeedback(null);
    try {
      const saved = await save.mutateAsync({
        expectedVersionToken: config.versionToken,
        requirements: draft.map(({ templateId, templateVersionId, requirementType }) => ({ templateId, templateVersionId, requirementType })),
      });
      setDraft(saved.requirements.map(item => ({
        templateId: item.templateId,
        templateVersionId: item.templateVersionId,
        templateName: item.templateName,
        category: item.category ?? null,
        version: item.version,
        requirementType: item.requirementType,
      })));
      setFeedback({ kind: "success", message: t("administration.settingsWorkspace.phaseGate.saved") });
    } catch (error) {
      const conflict = isConflict(error);
      setFeedback({
        kind: "error",
        conflict,
        message: conflict
          ? t("administration.settingsWorkspace.phaseGate.conflict")
          : t("administration.settingsWorkspace.phaseGate.saveFailed"),
      });
    }
  };

  return <Card>
    <CardHeader>
      <CardTitle>{t("administration.settingsWorkspace.phaseGate.title")}</CardTitle>
      <CardDescription>{t("administration.settingsWorkspace.phaseGate.description")}</CardDescription>
    </CardHeader>
    <CardContent className="space-y-6">
      {!canViewDocuments ? <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.documentViewRequired")}</p>
        : transitions.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
        : transitions.isError ? <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive"><span>{t("administration.settingsWorkspace.phaseGate.loadFailed")}</span><Button type="button" variant="outline" size="sm" onClick={() => void transitions.refetch()}>{t("administration.settingsWorkspace.phaseGate.retry")}</Button></div>
          : availableTransitions.length === 0 ? <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.noTransitions")}</p>
            : <>
              <div className="max-w-xl space-y-2">
                <Label htmlFor={`phase-gate-transition-${organizationId}`}>{t("administration.settingsWorkspace.phaseGate.transition")}</Label>
                <select id={`phase-gate-transition-${organizationId}`} value={transitionKey} onChange={event => { setTransitionKey(event.target.value); setFeedback(null); }} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
                  {availableTransitions.map(transition => <option key={`${transition.sourcePhaseId}:${transition.targetPhaseId}`} value={`${transition.sourcePhaseId}:${transition.targetPhaseId}`}>{labelForTransition(transition, locale)}</option>)}
                </select>
              </div>

              {configuration.isLoading ? <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.loading")}</p>
                : configuration.isError || !config ? <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive"><span>{t("administration.settingsWorkspace.phaseGate.loadFailed")}</span><Button type="button" variant="outline" size="sm" onClick={() => void configuration.refetch()}>{t("administration.settingsWorkspace.phaseGate.retry")}</Button></div>
                  : <>
                    <section className="space-y-3 rounded-lg border p-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div><h3 className="text-sm font-semibold">{t("administration.settingsWorkspace.phaseGate.requirementsTitle")}</h3>
                          {config.configurationVersion > 0 && <p className="mt-1 text-xs text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.configurationVersion", { version: config.configurationVersion })}</p>}
                        </div>
                      </div>
                      {draft.length === 0 ? <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.noRequirements")}</p>
                        : <div className="space-y-2">{draft.map(requirement => <div key={`${requirement.templateId}:${requirement.templateVersionId}`} className="grid gap-3 rounded-md border p-3 sm:grid-cols-[minmax(0,1fr)_minmax(9rem,auto)_auto] sm:items-center">
                          <div className="flex min-w-0 items-start gap-3"><FileText className="mt-0.5 h-4 w-4 shrink-0 text-primary" /><div className="min-w-0"><p className="truncate text-sm font-medium">{requirement.templateName}</p><p className="mt-1 text-xs text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.category")}: {requirement.category || t("documents.categoryUnavailable")} · {t("administration.settingsWorkspace.phaseGate.version")} {requirement.version}</p></div></div>
                          <div className="space-y-1"><Label className="sr-only" htmlFor={`phase-gate-type-${requirement.templateId}`}>{t("administration.settingsWorkspace.phaseGate.requirementType")}</Label><select id={`phase-gate-type-${requirement.templateId}`} value={requirement.requirementType} disabled={!canManage} onChange={event => setDraft(current => current.map(item => item.templateId === requirement.templateId ? { ...item, requirementType: event.target.value as PhaseGateRequirementType } : item))} className="flex h-9 w-full rounded-md border border-input bg-background px-2 text-sm disabled:opacity-60"><option value="Mandatory">{t("administration.settingsWorkspace.phaseGate.mandatory")}</option><option value="Optional">{t("administration.settingsWorkspace.phaseGate.optional")}</option></select></div>
                          {canManageRequirements && <Button type="button" variant="ghost" size="icon" aria-label={`${t("administration.settingsWorkspace.phaseGate.remove")} ${requirement.templateName}`} title={t("administration.settingsWorkspace.phaseGate.remove")} onClick={() => setDraft(current => current.filter(item => item.templateId !== requirement.templateId))}><X className="h-4 w-4" /></Button>}
                        </div>)}</div>}
                    </section>

                    <section className="space-y-3 rounded-lg border p-4">
                      <div><h3 className="text-sm font-semibold">{t("administration.settingsWorkspace.phaseGate.templatesTitle")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.templatesDescription")}</p></div>
                      <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(10rem,auto)]">
                        <div className="relative"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={event => setSearch(event.target.value)} placeholder={t("administration.settingsWorkspace.phaseGate.searchTemplates")} className="ps-8" /></div>
                      </div>
                      {templateVersionError && <p role="alert" className="text-sm text-destructive">{t("administration.settingsWorkspace.phaseGate.versionUnavailable")}</p>}
                      {templatesByCategory.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
                        : templatesByCategory.isError ? <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive"><span>{t("administration.settingsWorkspace.phaseGate.templatesLoadFailed")}</span><Button type="button" variant="outline" size="sm" onClick={() => void templatesByCategory.refetch()}>{t("administration.settingsWorkspace.phaseGate.retry")}</Button></div>
                          : templateGroups.length === 0 ? <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.noTemplates")}</p>
                            : <div className="space-y-2">{templateGroups.map(group => {
                              const isExpanded = expandedCategories.has(group.categoryId);
                              const filteredTemplates = group.templates.filter(t => 
                                t.templateName.toLowerCase().includes(search.toLowerCase())
                              );
                              if (filteredTemplates.length === 0 && search) return null;
                              return (
                                <div key={group.categoryId} className="rounded-md border">
                                  <div className="flex items-center justify-between p-3 bg-muted/50" onClick={() => toggleCategory(group.categoryId)}>
                                    <span className="font-medium">{locale === "ar" ? group.categoryNameAr || group.categoryName : group.categoryName}</span>
                                    {isExpanded ? <ChevronUp className="h-4 w-4 text-muted-foreground" /> : <ChevronDown className="h-4 w-4 text-muted-foreground" />}
                                  </div>
                                  {isExpanded && (
                                    <div className="grid gap-2 p-3 lg:grid-cols-2">
                                      {filteredTemplates.map(template => <div key={template.templateId} className="flex min-w-0 items-center justify-between gap-3 rounded-md border p-3">
                                        <div className="flex min-w-0 items-start gap-3"><FileText className="mt-0.5 h-4 w-4 shrink-0 text-primary" /><div className="min-w-0"><p className="truncate text-sm font-medium">{template.templateName}</p><p className="mt-1 truncate text-xs text-muted-foreground">{template.category || t("documents.categoryUnavailable")} · {t("administration.settingsWorkspace.phaseGate.version")} {template.version}</p></div></div>
                                        <Button type="button" variant="outline" size="sm" disabled={!canManageRequirements || selectedTemplateIds.has(template.templateId) || loadingTemplateId !== null} onClick={() => void addTemplate(template)}><Plus className="me-1 h-4 w-4" />{loadingTemplateId === template.templateId ? t("administration.settingsWorkspace.phaseGate.selectingVersion") : selectedTemplateIds.has(template.templateId) ? t("administration.settingsWorkspace.phaseGate.added") : t("administration.settingsWorkspace.phaseGate.addRequirement")}</Button>
                                      </div>)}
                                    </div>
                                  )}
                                </div>
                              );
                            })}</div>}
                    </section>

                    {feedback && <div role={feedback.kind === "error" ? "alert" : "status"} className={`flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 text-sm ${feedback.kind === "error" ? "border-destructive/30 bg-destructive/5 text-destructive" : "border-emerald-500/30 bg-emerald-500/5 text-emerald-800 dark:text-emerald-300"}`}><span>{feedback.message}</span>{feedback.conflict && <Button type="button" variant="outline" size="sm" onClick={() => { if (dirty && !window.confirm(t("administration.settingsWorkspace.discardSettings"))) return; void configuration.refetch().then(result => { if (result.data) { setDraft(result.data.requirements.map(item => ({ templateId: item.templateId, templateVersionId: item.templateVersionId, templateName: item.templateName, category: item.category ?? null, version: item.version, requirementType: item.requirementType }))); setFeedback(null); } }); }}>{t("administration.settingsWorkspace.phaseGate.reload")}</Button>}</div>}
                    {!canManageRequirements && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.phaseGate.readOnly")}</p>}
                    {canManageRequirements && <div className="flex justify-end border-t pt-4"><Button type="button" disabled={!dirty || save.isPending} onClick={() => void saveDraft()}>{save.isPending ? t("administration.settingsWorkspace.phaseGate.saving") : t("administration.settingsWorkspace.phaseGate.save")}</Button></div>}
                  </>}
            </>
      }
    </CardContent>
  </Card>;
}