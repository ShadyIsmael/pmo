import * as React from "react";
import { AlertTriangle, Check, Download, FileText, Link2, Search, Unlink, Upload } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Progress } from "@components/ui/Progress";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Badge } from "@components/ui/Badge";
import { ErrorState } from "@components/ui/StateDisplay";
import { Skeleton } from "@components/ui/Skeleton";
import { toast } from "@hooks/use-toast";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { projectLifecycleLabel, useProjectLifecycleOptions } from "../api/project-lifecycle";
import { documentApi, useProjectDocument, useProjectDocuments, useUploadDocument, type DocumentListItem } from "../api/documents";
import {
  useChangeProjectPhase,
  useLinkPhaseGateDocument,
  useProjectPhaseGateReadiness,
  useUnlinkPhaseGateDocument,
  type ProjectPhaseGateReadinessDto,
  type ProjectPhaseGateRequirementStatusDto,
} from "../api/project-phase-gates";
import type { ProjectDto } from "../api/projects";

interface UploadLinkOutcome { documentId: string; linked: boolean; errorMessage?: string; }

interface ProjectPhaseGateSideSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  project: ProjectDto;
}

export function localizedPhaseLabel(phase: string | null | undefined, english?: string | null, arabic?: string | null, locale?: string) {
  if (locale === "ar" && arabic) return arabic;
  if (locale !== "ar" && english) return english;
  return phase?.replaceAll("-", " ") ?? "";
}

export function statusClass(status: string) {
  if (status === "Satisfied" || status === "Ready") return "border-success/30 bg-success/10 text-success";
  if (status === "Missing" || status === "Incomplete" || status === "Mismatch") return "border-warning/30 bg-warning/10 text-warning";
  if (status === "InvalidOrUnavailable") return "border-destructive/30 bg-destructive/10 text-destructive";
  return "border-border bg-muted/40 text-muted-foreground";
}

export function readinessLabel(status: ProjectPhaseGateReadinessDto["readinessStatus"], t: (key: string) => string) {
  if (status === "Ready") return t("projects.phaseGate.readinessReady");
  if (status === "Incomplete") return t("projects.phaseGate.readinessIncomplete");
  if (status === "NotConfigured") return t("projects.phaseGate.readinessNotConfigured");
  return t("projects.phaseGate.readinessNotApplicable");
}

export function requirementStatusLabel(status: ProjectPhaseGateRequirementStatusDto["status"], t: (key: string) => string) {
  if (status === "Satisfied") return t("projects.phaseGate.requirementStatusSatisfied");
  if (status === "OptionalNotProvided") return t("projects.phaseGate.requirementStatusOptionalNotProvided");
  if (status === "InvalidOrUnavailable") return t("projects.phaseGate.requirementStatusInvalidOrUnavailable");
  return t("projects.phaseGate.requirementStatusMissing");
}

export function complianceStatusLabel(status: ProjectPhaseGateRequirementStatusDto["complianceStatus"], t: (key: string) => string) {
  return t(`projects.phaseGate.complianceStatus${status}`);
}

export function fileTypeLabel(fileType: string | null | undefined, t: (key: string) => string) {
  if (!fileType) return t("projects.phaseGate.fileTypeUnknown");
  const key = fileType.toLocaleLowerCase();
  const knownTypes = ["pdf", "doc", "docx", "xls", "xlsx"] as const;
  return knownTypes.includes(key as typeof knownTypes[number])
    ? t(`projects.phaseGate.fileType${key.toLocaleUpperCase()}`)
    : t("projects.phaseGate.fileTypeUnknown");
}

export function ProjectPhaseGateSideSheet({ open, onOpenChange, project }: ProjectPhaseGateSideSheetProps) {
  const { t, locale, formatNumber } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user
    && hasPermission(user.permissions, Permissions.Project.View)
    && hasPermission(user.permissions, Permissions.Document.View));
  const canReadLifecycleOptions = Boolean(user && hasPermission(user.permissions, Permissions.Project.View));
  const canChange = Boolean(user
    && hasPermission(user.permissions, Permissions.Project.View)
    && hasPermission(user.permissions, Permissions.ProjectPhase.Change)
    && canMutate && !effectiveReadOnly);
  const canManageEvidence = Boolean(canView && canMutate && !effectiveReadOnly && user
    && hasPermission(user.permissions, Permissions.Document.Edit));
  const canUploadEvidence = Boolean(canManageEvidence && user && hasPermission(user.permissions, Permissions.Document.Create));
  const [targetPhaseId, setTargetPhaseId] = React.useState("");
  const [documentSearch, setDocumentSearch] = React.useState("");
  const deferredDocumentSearch = React.useDeferredValue(documentSearch);
  const [transitionError, setTransitionError] = React.useState(false);
  const lifecycleOptions = useProjectLifecycleOptions({
    enabled: open && canReadLifecycleOptions && Boolean(project.publicId),
    projectId: project.publicId,
  });
  // The document checklist belongs to the persisted Current Phase. Selecting a
  // Target Phase must not change which requirements are displayed.
  const readiness = useProjectPhaseGateReadiness(project.publicId, undefined, open && canView);
  const documents = useProjectDocuments({ projectId: project.publicId, page: 1, pageSize: 100, search: deferredDocumentSearch.trim() || undefined }, open && canManageEvidence);
  const upload = useUploadDocument();
  const link = useLinkPhaseGateDocument(project.publicId);
  const unlink = useUnlinkPhaseGateDocument(project.publicId);
  const changePhase = useChangeProjectPhase(project.publicId);
  // Do not render a cached readiness response after Document.View is unavailable.
  const gate = canView ? readiness.data : undefined;

  React.useEffect(() => {
    setTargetPhaseId("");
    setDocumentSearch("");
    setTransitionError(false);
  }, [project.publicId, project.phaseId]);

  const phases = lifecycleOptions.data?.phases.filter((phase) => phase.isActive !== false
    && phase.id !== project.phaseId
    && phase.code.toLocaleUpperCase() !== project.phase?.toLocaleUpperCase()) ?? [];
  React.useEffect(() => {
    if (!open || targetPhaseId || !phases.length) return;
    setTargetPhaseId(phases[0].id);
  }, [open, targetPhaseId, phases]);

  const getEligibleDocuments = React.useCallback((requirement: ProjectPhaseGateRequirementStatusDto) =>
    (documents.data?.items ?? []).filter((document) => isEligibleForRequirement(document, requirement)), [documents.data?.items]);

  const linkExistingDocument = React.useCallback(async (requirement: ProjectPhaseGateRequirementStatusDto, documentId: string, documentVersionId: string) => {
    setTransitionError(false);
    await link.mutateAsync({ requirementSnapshotId: requirement.requirementSnapshotId, documentId, documentVersionId });
    toast({ title: t("projects.phaseGate.linkSuccess") });
  }, [link, t]);

  const revalidateCompliance = React.useCallback(async (requirement: ProjectPhaseGateRequirementStatusDto) => {
    if (!requirement.documentId || !requirement.documentVersionId) {
      throw new Error(t("projects.phaseGate.revalidationUnavailable"));
    }
    // Re-posting the currently linked evidence version invokes the existing
    // server-side comparison and refreshes the phase-gate readiness response.
    await link.mutateAsync({
      requirementSnapshotId: requirement.requirementSnapshotId,
      documentId: requirement.documentId,
      documentVersionId: requirement.documentVersionId,
    });
    toast({ title: t("projects.phaseGate.complianceRevalidated") });
  }, [link, t]);

  const downloadTemplate = React.useCallback(async (requirement: ProjectPhaseGateRequirementStatusDto) => {
    if (!requirement.templateVersionNumber) throw new Error(t("projects.phaseGate.templateDownloadUnavailable"));
    const blob = await documentApi.download(requirement.templateId, requirement.templateVersionNumber);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = requirement.templateFileName || `${requirement.templateName}-v${requirement.templateVersionNumber}`;
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  }, [t]);

  const uploadForRequirement = React.useCallback(async (requirement: ProjectPhaseGateRequirementStatusDto, form: FormData) => {
    if (!project.id) throw new Error("Project identifier is unavailable.");
    form.set("ProjectId", project.id);
    form.set("IsTemplate", "false");
    form.set("SourceTemplateId", requirement.templateId);
    form.set("SourceTemplateVersionId", requirement.templateVersionId);
    const uploaded = await upload.mutateAsync(form);
    const fileVersion = uploaded.versions.find((version) => version.isCurrent)
      ?? [...uploaded.versions].sort((left, right) => right.versionNumber - left.versionNumber)[0];
    if (!fileVersion) throw new Error("Uploaded project document version is unavailable.");
    try {
      await link.mutateAsync({
        requirementSnapshotId: requirement.requirementSnapshotId,
        documentId: uploaded.document.id,
        documentVersionId: fileVersion.id,
      });
      toast({ title: t("projects.phaseGate.documentUploadSuccess") });
      return { documentId: uploaded.document.id, linked: true };
    } catch (error) {
      // The uploaded Project Document remains selectable so requirement linking
      // can be retried without uploading duplicate file contents.
      return { documentId: uploaded.document.id, linked: false, errorMessage: error instanceof Error ? error.message : undefined };
    }
  }, [link, project.id, t, upload]);

  const submitPhaseChange = async () => {
    if (!targetPhaseId || !project.rowVersion || !canChange || changePhase.isPending) return;
    setTransitionError(false);
    try {
      await changePhase.mutateAsync({ targetPhaseId, expectedProjectVersionToken: project.rowVersion });
      toast({ title: t("projects.phaseGate.transitionSuccess") });
      onOpenChange(false);
    } catch {
      setTransitionError(true);
      if (canView) await readiness.refetch();
    }
  };

  const currentLabel = localizedPhaseLabel(gate?.currentPhase || project.phase, gate?.currentPhaseLabelEn || project.phaseNameEn, gate?.currentPhaseLabelAr || project.phaseNameAr, locale);
  const gateApplies = Boolean(gate && gate.readinessStatus !== "NotApplicable");
  const canSubmitTransition = Boolean(canChange && targetPhaseId && project.rowVersion
    && canMutate && !effectiveReadOnly);

  // Group requirements by category
  const requirementsByCategory = React.useMemo(() => {
    if (!gate) return [];
    const groups = new Map<string, { categoryName: string; requirements: ProjectPhaseGateRequirementStatusDto[] }>();
    for (const req of gate.requirements) {
      const categoryKey = req.category || "uncategorized";
      const existing = groups.get(categoryKey);
      if (existing) {
        existing.requirements.push(req);
      } else {
        groups.set(categoryKey, {
          categoryName: req.category || t("documents.categoryFallback"),
          requirements: [req]
        });
      }
    }
    // Sort categories by name
    return Array.from(groups.entries())
      .map(([key, value]) => ({ categoryKey: key, ...value }))
      .sort((a, b) => a.categoryName.localeCompare(b.categoryName));
  }, [gate?.requirements, locale, t]);

  return <SideSheet
    open={open}
    onOpenChange={onOpenChange}
    title={<span dir={locale === "ar" ? "rtl" : "ltr"}>{t("projects.phaseGate.changePhaseTitle")}</span>}
    description={<span dir={locale === "ar" ? "rtl" : "ltr"}>{project.name} · {t("projects.phaseGate.changePhaseDescription")}</span>}
    ariaLabel={t("projects.phaseGate.changePhaseTitle")}
    closeLabel={t("common.close")}
    size="xl"
  >
    <SideSheetBody dir={locale === "ar" ? "rtl" : "ltr"} className="space-y-4">
      {lifecycleOptions.isLoading ? <div role="status" aria-label={t("common.loading")} className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : null}
      {lifecycleOptions.isError ? <ErrorState title={t("projects.lifecycle.lookupError")} onRetry={() => void lifecycleOptions.refetch()} /> : null}
      {canView && readiness.isLoading ? <div role="status" aria-label={t("common.loading")} className="space-y-3"><Skeleton className="h-20" /><Skeleton className="h-20" /><Skeleton className="h-36" /></div> : null}
      {canView && readiness.isError ? <ErrorState title={t("projects.phaseGate.loadFailed")} onRetry={() => void readiness.refetch()} /> : null}
      {!lifecycleOptions.isLoading && !lifecycleOptions.isError ? <>
        {canChange && !canView ? <p className="rounded-md border border-border bg-muted/20 p-3 text-sm text-muted-foreground">{t("projects.phaseGate.documentDetailsRestricted")}</p> : null}
        <section className="rounded-lg border border-border bg-muted/10 p-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <div><p className="text-xs text-muted-foreground">{t("projects.phaseGate.currentPhase")}</p><p className="mt-1 font-medium">{currentLabel}</p></div>
            <div><label htmlFor="project-phase-target" className="text-xs text-muted-foreground">{t("projects.phaseGate.targetPhase")}</label>
              <select id="project-phase-target" value={targetPhaseId} onChange={(event) => { setTargetPhaseId(event.target.value); setTransitionError(false); }} aria-label={t("projects.phaseGate.targetPhase")} className="mt-1 flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" disabled={!phases.length}>
                <option value="">{t("projects.lifecycle.choosePhase")}</option>
                {phases.map((phase) => <option key={phase.id} value={phase.id}>{projectLifecycleLabel(phase, locale)}</option>)}
              </select>
            </div>
          </div>
          {!phases.length ? <p className="mt-2 text-xs text-muted-foreground">{t("projects.lifecycle.noOtherPhasesAvailable")}</p> : null}
        </section>
        {gate && gateApplies ? <>
          <section className="rounded-lg border border-border bg-muted/10 p-4">
            {gate.configurationVersionNumber != null ? <p className="text-xs text-muted-foreground">{t("projects.phaseGate.configurationVersion", { version: gate.configurationVersionNumber })}</p> : null}
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-medium ${statusClass(gate.readinessStatus)}`}>{readinessLabel(gate.readinessStatus, t)}</span>
              {gate.readinessPercentage != null ? <span className="text-xs text-muted-foreground">{t("projects.phaseGate.completionProgress", { complete: formatNumber(gate.satisfiedMandatoryCount), total: formatNumber(gate.mandatoryRequirementCount) })}</span> : null}
            </div>
            {gate.readinessPercentage != null ? <Progress value={gate.readinessPercentage} className="mt-3" aria-label={t("projects.phaseGate.completionProgress", { complete: gate.satisfiedMandatoryCount, total: gate.mandatoryRequirementCount })} /> : null}
            {gate.readinessStatus === "Incomplete" ? <p className="mt-3 flex items-center gap-2 text-sm text-warning"><AlertTriangle className="h-4 w-4" />{t("projects.phaseGate.transitionNonBlocking")}</p> : null}
          </section>
          {canUploadEvidence && gate.requirements.some((item) => item.status !== "Satisfied") ? <p className="rounded-md border border-border bg-muted/20 p-3 text-sm text-muted-foreground">{t("projects.phaseGate.externalApprovalAssumption")}</p> : null}
          {gate.requirements.length === 0
            ? <div className="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground">{t("projects.phaseGate.noRequirements")}</div>
            : <div className="space-y-4">
              {canManageEvidence ? <div className="relative"><Search className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={documentSearch} onChange={(event) => setDocumentSearch(event.target.value)} placeholder={t("documents.search")} aria-label={t("documents.search")} className="ps-8" /></div> : null}
              {requirementsByCategory.map((group) => (
                <section key={group.categoryKey} className="space-y-3">
                  <h3 className="text-sm font-semibold">
                    {group.categoryName}
                  </h3>
                  {group.requirements.filter((item) => item.requirementType === "Mandatory").length > 0 ? (
                    <>
                      <h4 className="text-xs text-muted-foreground font-medium">{t("projects.phaseGate.mandatoryDocuments")}</h4>
                      {group.requirements.filter((item) => item.requirementType === "Mandatory").map((requirement) => (
                        <PhaseGateRequirementCard
                          key={requirement.requirementSnapshotId}
                          requirement={requirement}
                          documents={getEligibleDocuments(requirement)}
                          documentsLoading={documents.isLoading}
                          documentsError={documents.isError}
                          onRetryDocuments={() => void documents.refetch()}
                          canLink={canManageEvidence}
                          canUpload={canUploadEvidence}
                          busy={link.isPending || unlink.isPending || upload.isPending}
                          onLink={linkExistingDocument}
                          onRevalidate={revalidateCompliance}
                          onUnlink={(item) => unlink.mutateAsync(item.requirementSnapshotId)}
                          onUpload={uploadForRequirement}
                          onDownloadTemplate={downloadTemplate}
                        />
                      ))}
                    </>
                  ) : null}
                  {group.requirements.filter((item) => item.requirementType === "Optional").length > 0 ? (
                    <>
                      <h4 className="text-xs text-muted-foreground font-medium">{t("projects.phaseGate.optionalDocuments")}</h4>
                      {group.requirements.filter((item) => item.requirementType === "Optional").map((requirement) => (
                        <PhaseGateRequirementCard
                          key={requirement.requirementSnapshotId}
                          requirement={requirement}
                          documents={getEligibleDocuments(requirement)}
                          documentsLoading={documents.isLoading}
                          documentsError={documents.isError}
                          onRetryDocuments={() => void documents.refetch()}
                          canLink={canManageEvidence}
                          canUpload={canUploadEvidence}
                          busy={link.isPending || unlink.isPending || upload.isPending}
                          onLink={linkExistingDocument}
                          onRevalidate={revalidateCompliance}
                          onUnlink={(item) => unlink.mutateAsync(item.requirementSnapshotId)}
                          onUpload={uploadForRequirement}
                          onDownloadTemplate={downloadTemplate}
                        />
                      ))}
                    </>
                  ) : null}
                </section>
              ))}
            </div>}
        </> : null}
        {gate && gateApplies && gate.readinessStatus === "Ready" && gate.phaseTransitionStatus === "AwaitingTransition" ? <p className="flex items-center gap-2 text-sm text-success"><Check className="h-4 w-4" />{t("projects.phaseGate.transitionReady")}</p> : null}
        {transitionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("projects.phaseGate.transitionFailed")}</p> : null}
        {(link.isError || unlink.isError || upload.isError) ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("documents.actionFailed")}</p> : null}
      </> : null}
    </SideSheetBody>
    <SideSheetFooter dir={locale === "ar" ? "rtl" : "ltr"}>
      <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.close")}</Button>
      {canChange ? <Button type="button" disabled={!canSubmitTransition || changePhase.isPending || lifecycleOptions.isLoading || lifecycleOptions.isError} onClick={() => void submitPhaseChange()}>
        {changePhase.isPending ? t("projects.phaseGate.saving") : t("projects.phaseGate.changePhaseAction")}
      </Button> : null}
    </SideSheetFooter>
  </SideSheet>;
}

export function isEligibleForRequirement(document: DocumentListItem, requirement: ProjectPhaseGateRequirementStatusDto) {
  void requirement;
  if (document.isTemplate || document.isArchived || !document.projectId) return false;
  // Source-template differences are advisory. The backend verifies actual content type
  // and records any identity/structure differences against the pinned requirement.
  return true;
}

export function PhaseGateRequirementCard({
  requirement,
  documents,
  documentsLoading,
  documentsError,
  onRetryDocuments,
  canLink,
  canUpload,
  busy,
  onLink,
  onRevalidate,
  onUnlink,
  onUpload,
  onDownloadTemplate,
}: {
  requirement: ProjectPhaseGateRequirementStatusDto;
  documents: DocumentListItem[];
  documentsLoading: boolean;
  documentsError: boolean;
  onRetryDocuments: () => void;
  canLink: boolean;
  canUpload: boolean;
  busy: boolean;
  onLink: (requirement: ProjectPhaseGateRequirementStatusDto, documentId: string, documentVersionId: string) => Promise<void>;
  onRevalidate: (requirement: ProjectPhaseGateRequirementStatusDto) => Promise<void>;
  onUnlink: (requirement: ProjectPhaseGateRequirementStatusDto) => Promise<unknown>;
  onUpload: (requirement: ProjectPhaseGateRequirementStatusDto, form: FormData) => Promise<UploadLinkOutcome>;
  onDownloadTemplate: (requirement: ProjectPhaseGateRequirementStatusDto) => Promise<void>;
}) {
  const { t, locale } = useI18n();
  const [selectedDocumentId, setSelectedDocumentId] = React.useState("");
  const [showUpload, setShowUpload] = React.useState(false);
  const [actionError, setActionError] = React.useState(false);
  const [actionErrorMessage, setActionErrorMessage] = React.useState("");
  const [downloadingTemplate, setDownloadingTemplate] = React.useState(false);
  const [revalidating, setRevalidating] = React.useState(false);
  const selectedDocument = useProjectDocument(selectedDocumentId || null);
  const selectedVersion = selectedDocument.data?.versions.find((version) => version.isCurrent)
    ?? (selectedDocument.data?.versions.length ? [...selectedDocument.data.versions].sort((a, b) => b.versionNumber - a.versionNumber)[0] : undefined);

  const handleLink = async () => {
    if (!selectedDocumentId || !selectedVersion) return;
    setActionError(false);
    try {
      await onLink(requirement, selectedDocumentId, selectedVersion.id);
      setSelectedDocumentId("");
    } catch (error) {
      setActionError(true);
      setActionErrorMessage(error instanceof Error ? error.message : t("documents.actionFailed"));
    }
  };

  const handleUpload = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const formElement = event.currentTarget;
    const form = new FormData(formElement);
    setActionError(false);
    try {
      const result = await onUpload(requirement, form);
      formElement.reset();
      setShowUpload(false);
      setSelectedDocumentId(result.documentId);
      if (!result.linked) {
        setActionError(true);
        setActionErrorMessage(result.errorMessage || t("documents.actionFailed"));
      }
    } catch (error) {
      setActionError(true);
      setActionErrorMessage(error instanceof Error ? error.message : t("documents.actionFailed"));
    }
  };

  const handleDownloadTemplate = async () => {
    setDownloadingTemplate(true);
    setActionError(false);
    try {
      await onDownloadTemplate(requirement);
    } catch (error) {
      setActionError(true);
      setActionErrorMessage(error instanceof Error ? error.message : t("projects.phaseGate.templateDownloadUnavailable"));
    } finally {
      setDownloadingTemplate(false);
    }
  };

  const handleUnlink = async () => {
    setActionError(false);
    try {
      await onUnlink(requirement);
    } catch (error) {
      setActionError(true);
      setActionErrorMessage(error instanceof Error ? error.message : t("documents.actionFailed"));
    }
  };

  const handleRevalidate = async () => {
    setActionError(false);
    setActionErrorMessage("");
    setRevalidating(true);
    try {
      await onRevalidate(requirement);
    } catch (error) {
      setActionError(true);
      setActionErrorMessage(error instanceof Error ? error.message : t("documents.actionFailed"));
    } finally {
      setRevalidating(false);
    }
  };

  return <article className="rounded-lg border border-border bg-background p-3 sm:p-4">
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div className="min-w-0 flex-1">
        <h4 className="truncate text-sm font-semibold">{requirement.templateName}</h4>
        <p className="mt-1 text-xs text-muted-foreground">{requirement.category || t("documents.categoryFallback")} · {t("documents.table.version")} {requirement.templateVersionLabel}</p>
      </div>
      <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-medium ${statusClass(requirement.status)}`}>{requirementStatusLabel(requirement.status, t)}</span>
    </div>
    <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
      <Badge status={requirement.requirementType}>{requirement.requirementType === "Mandatory" ? t("projects.phaseGate.mandatory") : t("projects.phaseGate.optional")}</Badge>
      {requirement.documentName ? <span className="inline-flex min-w-0 items-center gap-1"><FileText className="h-3.5 w-3.5 shrink-0" /><span className="truncate">{requirement.documentName}{requirement.documentVersionNumber ? ` · v${requirement.documentVersionNumber}` : ""}</span></span> : <span>{requirement.status === "InvalidOrUnavailable" ? t("projects.phaseGate.projectDocumentUnavailable") : t("projects.phaseGate.noLinkedDocument")}</span>}
    </div>
    <div className="mt-3 flex flex-wrap items-center gap-2">
      <span className="inline-flex items-center gap-1.5">
        {requirement.complianceStatus === "Mismatch" ? <AlertTriangle aria-hidden="true" className="h-4 w-4 shrink-0 text-warning" /> : null}
        <Badge status={requirement.complianceStatus}>{complianceStatusLabel(requirement.complianceStatus, t)}</Badge>
      </span>
      <Button type="button" size="sm" variant="ghost" disabled={downloadingTemplate || !requirement.templateVersionNumber} onClick={() => void handleDownloadTemplate()}>
        <Download className="me-2 h-4 w-4" />{downloadingTemplate ? t("projects.phaseGate.templateDownloading") : t("projects.phaseGate.downloadTemplate")}
      </Button>
    </div>
    {requirement.complianceStatus === "NotVerified" ? <p role="note" className="mt-2 text-xs text-muted-foreground">{t("projects.phaseGate.notVerifiedExplanation")}</p> : null}
    {requirement.complianceStatus === "Missing" ? <p className="mt-2 text-xs text-warning">{t("projects.phaseGate.missingExpectedTemplate", { template: requirement.templateName, version: requirement.templateVersionLabel })}</p> : null}
    {requirement.expectedFileType || requirement.detectedFileType ? <p className="mt-2 text-xs text-muted-foreground">
      {t("projects.phaseGate.expectedFileType", { type: fileTypeLabel(requirement.expectedFileType, t) })} · {t("projects.phaseGate.detectedFileType", { type: fileTypeLabel(requirement.detectedFileType, t) })}
    </p> : null}
    {requirement.complianceFindings?.length ? <details open className="mt-3 rounded-md border border-border bg-muted/20 p-3">
      <summary className="cursor-pointer text-xs font-semibold">{t("projects.phaseGate.complianceDetails")}</summary>
      <ul className="mt-2 space-y-2 text-xs text-muted-foreground">
        {requirement.complianceFindings.map((finding, index) => <li key={`${finding.code}-${index}`}>
          <p>{locale === "ar" ? finding.messageAr : finding.messageEn}</p>
          {finding.expected != null || finding.detected != null ? <p className="mt-0.5 text-[11px]">
            {finding.expected != null ? <span>{t("projects.phaseGate.findingExpectedValue", { value: finding.expected })}</span> : null}
            {finding.expected != null && finding.detected != null ? <span aria-hidden="true"> · </span> : null}
            {finding.detected != null ? <span>{t("projects.phaseGate.findingDetectedValue", { value: finding.detected })}</span> : null}
          </p> : null}
        </li>)}
      </ul>
      {requirement.comparedAt || requirement.validatorVersion ? <p className="mt-2 flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
        {requirement.comparedAt ? <time dateTime={requirement.comparedAt}>{t("projects.phaseGate.comparedAt", { date: new Date(requirement.comparedAt).toLocaleString(locale) })}</time> : null}
        {requirement.validatorVersion ? <span>{t("projects.phaseGate.validatorVersion", { version: requirement.validatorVersion })}</span> : null}
      </p> : null}
    </details> : null}
    {canLink && requirement.complianceStatus === "PendingValidation" && requirement.documentId && requirement.documentVersionId ? <div className="mt-3 flex justify-end">
      <Button type="button" size="sm" variant="outline" aria-busy={revalidating} disabled={busy || revalidating} onClick={() => void handleRevalidate()}>{revalidating ? t("projects.phaseGate.revalidatingCompliance") : t("projects.phaseGate.revalidateCompliance")}</Button>
    </div> : null}
    {actionError ? <p role="alert" className="mt-3 text-sm text-destructive">{actionErrorMessage || t("documents.actionFailed")}</p> : null}
    {canLink && requirement.status !== "Satisfied" ? <div className="mt-3 space-y-3 border-t border-border pt-3">
      {documentsError ? <div className="flex items-center justify-between gap-2 rounded-md border border-destructive/30 bg-destructive/5 p-2 text-xs text-destructive"><span>{t("documents.loadError")}</span><Button type="button" size="sm" variant="outline" onClick={onRetryDocuments}>{t("projects.phaseGate.retry")}</Button></div> : null}
      <div className="flex flex-col gap-2 sm:flex-row">
        <select value={selectedDocumentId} onChange={(event) => { setSelectedDocumentId(event.target.value); setActionError(false); }} aria-label={t("projects.phaseGate.selectDocument")} className="flex h-9 min-w-0 flex-1 rounded-md border border-input bg-background px-3 text-sm" disabled={documentsLoading || documentsError}>
          <option value="">{t("projects.phaseGate.selectDocument")}</option>
          {documents.map((document) => <option key={document.id} value={document.id}>{document.name} · v{document.version}</option>)}
        </select>
        <Button type="button" size="sm" variant="outline" disabled={busy || documentsLoading || documentsError || !selectedDocumentId || selectedDocument.isLoading || !selectedVersion} onClick={() => void handleLink()}><Link2 className="me-2 h-4 w-4" />{t("projects.phaseGate.linkExistingDocument")}</Button>
      </div>
      {selectedDocumentId && selectedDocument.isError ? <p className="text-xs text-destructive">{t("documents.loadError")}</p> : null}
      {selectedDocumentId && selectedVersion ? <p className="text-xs text-muted-foreground">{t("projects.phaseGate.linkedDocument")}: {selectedDocument.data?.document.name} · v{selectedVersion.versionNumber}</p> : null}
      {documentsLoading ? <Skeleton className="h-9" /> : null}
      {!documentsLoading && !documentsError && documents.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.phaseGate.projectDocumentUnavailable")}</p> : null}
      {canUpload ? <div>
        {!showUpload ? <Button type="button" size="sm" variant="ghost" disabled={busy} onClick={() => setShowUpload(true)}><Upload className="me-2 h-4 w-4" />{t("projects.phaseGate.uploadDocument")}</Button> : <form onSubmit={(event) => void handleUpload(event)} className="grid gap-2 rounded-md bg-muted/20 p-3 sm:grid-cols-2">
          <Input name="Name" aria-label={t("documents.fields.name")} placeholder={t("documents.fields.name")} required />
          <Input name="File" type="file" aria-label={t("documents.fields.file")} accept=".pdf,.doc,.docx,.xls,.xlsx,.txt" required />
          <div className="flex gap-2 sm:col-span-2">
            <Button type="submit" size="sm" disabled={busy}><Upload className="me-2 h-4 w-4" />{t("projects.phaseGate.documentUploadTitle")}</Button>
            <Button type="button" size="sm" variant="outline" disabled={busy} onClick={() => setShowUpload(false)}>{t("documents.cancel")}</Button>
          </div>
        </form>}
      </div> : null}
    </div> : null}
    {canLink && requirement.status === "Satisfied" && requirement.documentId ? <div className="mt-3 flex justify-end border-t border-border pt-3"><Button type="button" size="sm" variant="ghost" disabled={busy} onClick={() => void handleUnlink()}><Unlink className="me-2 h-4 w-4" />{t("projects.phaseGate.removeLinkedDocument")}</Button></div> : null}
  </article>;
}
