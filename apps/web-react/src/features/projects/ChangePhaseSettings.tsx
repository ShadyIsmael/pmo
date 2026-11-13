import * as React from "react";
import { AlertTriangle, ArrowRight, CheckCircle2, History, Search } from "lucide-react";
import { ApiError } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Input } from "@components/ui/Input";
import { Progress } from "@components/ui/Progress";
import { Skeleton } from "@components/ui/Skeleton";
import { ErrorState } from "@components/ui/StateDisplay";
import { toast } from "@hooks/use-toast";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import {
  projectLifecycleDescription,
  projectLifecycleLabel,
  useProjectLifecycleOptions,
  type ProjectLifecycleOption,
} from "./api/project-lifecycle";
import { documentApi, useProjectDocuments, useUploadDocument } from "./api/documents";
import {
  useChangeProjectPhase,
  useLinkPhaseGateDocument,
  useProjectPhaseGateReadiness,
  useProjectPhaseHistory,
  useUnlinkPhaseGateDocument,
  type ProjectPhaseGateRequirementStatusDto,
  type ProjectPhaseGateReadinessStatus,
  type ProjectPhaseHistoryItem,
  type ProjectPhaseHistoryRequirement,
} from "./api/project-phase-gates";
import type { ProjectDto } from "./api/projects";
import {
  complianceStatusLabel,
  fileTypeLabel,
  isEligibleForRequirement,
  localizedPhaseLabel,
  PhaseGateRequirementCard,
  readinessLabel,
  requirementStatusLabel,
  statusClass,
} from "./components/ProjectPhaseGateSideSheet";

interface UploadLinkOutcome {
  documentId: string;
  linked: boolean;
  errorMessage?: string;
}

function normalizedPhaseCode(code: string | null | undefined) {
  return (code ?? "").trim().toLocaleLowerCase();
}

function evaluationOutcomeLabel(outcome: string | null | undefined, t: (key: string) => string) {
  if (!outcome) return t("common.notAvailable");
  if (outcome === "Ready" || outcome === "Incomplete" || outcome === "NotConfigured" || outcome === "NotApplicable") {
    return readinessLabel(outcome as ProjectPhaseGateReadinessStatus, t);
  }
  // Unknown backend outcome values are shown verbatim rather than fabricated.
  return outcome;
}

function findingsOf(requirement: ProjectPhaseHistoryRequirement) {
  return requirement.findings ?? requirement.complianceFindings ?? [];
}

/**
 * Inline Project Settings section for the approved Change Phase experience.
 * It reuses the Sprint 2 readiness, requirement, document, and transition APIs:
 * document compliance is advisory and never disables an authorized phase change.
 */
export function ChangePhaseSettings({ projectId, project }: { projectId: string; project: ProjectDto }) {
  const { t, locale, formatNumber } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const confirm = useConfirm();
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
  const [transitionError, setTransitionError] = React.useState<"failed" | "conflict" | null>(null);
  const [transitionSucceeded, setTransitionSucceeded] = React.useState(false);

  const lifecycleOptions = useProjectLifecycleOptions({
    enabled: canReadLifecycleOptions && Boolean(project.publicId),
    projectId: project.publicId,
  });
  // Phase-gate endpoints are scoped by the project's external PublicId; the
  // `projectId` prop can be a public identifier when rendered from the header sheet.
  const phaseGateProjectId = project.publicId || projectId;
  // Fetch readiness based on the project's current phase for the document checklist.
  // The targetPhaseId is used separately for transition eligibility below.
  const readiness = useProjectPhaseGateReadiness(phaseGateProjectId, undefined, canView);
  const documents = useProjectDocuments({ projectId: phaseGateProjectId, page: 1, pageSize: 100, search: deferredDocumentSearch.trim() || undefined }, canManageEvidence);
  const upload = useUploadDocument();
  const link = useLinkPhaseGateDocument(phaseGateProjectId);
  const unlink = useUnlinkPhaseGateDocument(phaseGateProjectId);
  const changePhase = useChangeProjectPhase(phaseGateProjectId);
  const history = useProjectPhaseHistory(phaseGateProjectId, canView);
  // Do not render cached readiness output after Document.View becomes unavailable.
  const gate = canView ? readiness.data : undefined;

  React.useEffect(() => {
    setTargetPhaseId("");
    setDocumentSearch("");
    setTransitionError(null);
    setTransitionSucceeded(false);
    void readiness.refetch();
  }, [project.publicId, project.phaseId, canView]);
  // Target phases come only from the Project-scoped API response. The backend
  // owns protected Pre-Initiation provisioning and the authoritative order.
  const phases = React.useMemo(() => {
    const allPhases = lifecycleOptions.data?.phases ?? [];
    const currentPhaseCode = normalizedPhaseCode(project.phase);
    return allPhases
      .filter((phase) => phase.isActive !== false
        && phase.id !== project.phaseId
        && (!currentPhaseCode || normalizedPhaseCode(phase.code) !== currentPhaseCode))
      .sort((left, right) => left.displayOrder - right.displayOrder);
  }, [lifecycleOptions.data, project.phaseId, project.phase]);

  React.useEffect(() => {
    if (targetPhaseId && phases.some((phase) => phase.id === targetPhaseId)) return;
    setTargetPhaseId(phases[0]?.id ?? "");
  }, [phases, targetPhaseId]);

  const selectedPhase: ProjectLifecycleOption | undefined = phases.find((phase) => phase.id === targetPhaseId);
  const selectedPhaseDescription = projectLifecycleDescription(selectedPhase, locale);

  const getEligibleDocuments = React.useCallback((requirement: ProjectPhaseGateRequirementStatusDto) =>
    (documents.data?.items ?? []).filter((document) => isEligibleForRequirement(document, requirement)), [documents.data?.items]);

  const linkExistingDocument = React.useCallback(async (requirement: ProjectPhaseGateRequirementStatusDto, documentId: string, documentVersionId: string) => {
    setTransitionError(null);
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

  const uploadForRequirement = React.useCallback(async (requirement: ProjectPhaseGateRequirementStatusDto, form: FormData): Promise<UploadLinkOutcome> => {
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

  const currentPhaseLabel = localizedPhaseLabel(
    gate?.currentPhase ?? project.phase,
    gate?.currentPhaseLabelEn ?? project.phaseNameEn,
    gate?.currentPhaseLabelAr ?? project.phaseNameAr,
    locale,
  ) || t("common.notAvailable");

  const submitPhaseChange = async () => {
    if (!canChange || !targetPhaseId || !project.rowVersion || changePhase.isPending) return;
    const accepted = await confirm({
      title: t("projects.phaseGate.changePhaseConfirmTitle"),
      description: t("projects.phaseGate.changePhaseConfirmDescription", {
        current: currentPhaseLabel,
        target: selectedPhase ? projectLifecycleLabel(selectedPhase, locale) : targetPhaseId,
      }),
      confirmLabel: t("projects.phaseGate.changePhaseAction"),
    });
    if (!accepted) return;
    setTransitionError(null);
    setTransitionSucceeded(false);
    try {
      await changePhase.mutateAsync({ targetPhaseId, expectedProjectVersionToken: project.rowVersion });
      toast({ title: t("projects.phaseGate.transitionSuccess") });
      setTransitionSucceeded(true);
      setTargetPhaseId("");
    } catch (error) {
      setTransitionError(error instanceof ApiError && error.status === 409 ? "conflict" : "failed");
      // The row version or readiness may have changed elsewhere; never submit a stale decision.
      if (canView) await readiness.refetch();
    }
  };

  const gateApplies = Boolean(gate && gate.readinessStatus !== "NotApplicable");
  const mandatoryRequirements = gate?.requirements.filter((requirement) => requirement.requirementType === "Mandatory") ?? [];
  const optionalRequirements = gate?.requirements.filter((requirement) => requirement.requirementType === "Optional") ?? [];
  const showAdvisory = Boolean(gate && (gate.readinessStatus === "Incomplete"
    || gate.requirements.some((requirement) => requirement.complianceStatus === "Mismatch" || requirement.complianceStatus === "NotVerified")));
  const canSubmitTransition = Boolean(canChange && targetPhaseId && project.rowVersion && canMutate && !effectiveReadOnly);

  if (!canView) {
    return <p role="note" className="rounded-lg border border-border bg-muted/20 p-4 text-sm text-muted-foreground">{t("projects.phaseGate.documentDetailsRestricted")}</p>;
  }

  const renderRequirementCard = (requirement: ProjectPhaseGateRequirementStatusDto) => <PhaseGateRequirementCard
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
  />;

  return <section aria-labelledby="project-change-phase-title" className="space-y-4" dir={locale === "ar" ? "rtl" : "ltr"}>
    <div>
      <h2 id="project-change-phase-title" className="text-lg font-semibold">{t("projects.phaseGate.changePhaseTitle")}</h2>
      <p className="mt-1 text-sm text-muted-foreground">{t("projects.phaseGate.changePhaseDescription")}</p>
    </div>
    {!canChange ? <p role="note" className="text-sm text-muted-foreground">{t("projects.phaseGate.changePhasePermissionRequired")}</p> : null}

    <Card className="overflow-hidden">
      <CardContent className="space-y-4 p-5 sm:p-6">
        {lifecycleOptions.isLoading ? <div role="status" aria-label={t("common.loading")} className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-10" /></div> : null}
        {lifecycleOptions.isError ? <ErrorState title={t("projects.lifecycle.lookupError")} onRetry={() => void lifecycleOptions.refetch()} /> : null}
        {!lifecycleOptions.isLoading && !lifecycleOptions.isError ? <>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="min-w-0 rounded-lg border border-border bg-muted/20 p-4">
              <p className="text-xs text-muted-foreground">{t("projects.phaseGate.currentPhase")}</p>
              <p className="mt-1 break-words font-medium">{currentPhaseLabel}</p>
            </div>
            <div className="min-w-0 rounded-lg border border-border bg-muted/20 p-4">
              <label htmlFor="project-settings-target-phase" className="text-xs text-muted-foreground">{t("projects.phaseGate.targetPhase")}</label>
              <select
                id="project-settings-target-phase"
                aria-label={t("projects.phaseGate.targetPhase")}
                value={targetPhaseId}
                disabled={!phases.length || changePhase.isPending}
                onChange={(event) => { setTargetPhaseId(event.target.value); setTransitionError(null); setTransitionSucceeded(false); }}
                className="mt-1 flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-60"
              >
                <option value="">{t("projects.lifecycle.choosePhase")}</option>
                {phases.map((phase) => <option key={phase.id} value={phase.id}>{projectLifecycleLabel(phase, locale)}</option>)}
              </select>
              {selectedPhase ? <div className="mt-2">
                <p className="text-sm font-medium">{projectLifecycleLabel(selectedPhase, locale)}</p>
                {selectedPhaseDescription ? <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{selectedPhaseDescription}</p> : null}
              </div> : null}
              {!phases.length ? <p className="mt-2 text-xs text-muted-foreground">{t("projects.lifecycle.noOtherPhasesAvailable")}</p> : null}
            </div>
          </div>
          <div className="flex flex-col gap-3 border-t border-border pt-4 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-xs text-muted-foreground">{t("projects.phaseGate.advisoryHint")}</p>
            {canChange ? <Button type="button" disabled={!canSubmitTransition || changePhase.isPending} onClick={() => void submitPhaseChange()}>
              <ArrowRight aria-hidden="true" className="me-2 h-4 w-4 rtl:rotate-180" />
              {changePhase.isPending ? t("projects.phaseGate.saving") : t("projects.phaseGate.changePhaseAction")}
            </Button> : null}
          </div>
          {transitionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
            {transitionError === "conflict" ? t("projects.phaseGate.transitionConflict") : t("projects.phaseGate.transitionFailed")}
          </p> : null}
          {transitionSucceeded ? <p role="status" className="flex items-center gap-2 rounded-md border border-success/30 bg-success/5 p-3 text-sm text-success">
            <CheckCircle2 aria-hidden="true" className="h-4 w-4 shrink-0" />{t("projects.phaseGate.transitionSuccess")}
          </p> : null}
        </> : null}
      </CardContent>
    </Card>

    <Card className="overflow-hidden">
      <CardHeader className="border-b bg-muted/30 pb-4">
        <CardTitle className="text-base">{t("projects.phaseGate.readinessTitle")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 p-5 sm:p-6">
        {!targetPhaseId ? <p className="text-sm text-muted-foreground">{t("projects.lifecycle.choosePhase")}</p> : null}
        {targetPhaseId && readiness.isLoading ? <div role="status" aria-label={t("common.loading")} className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-24" /></div> : null}
        {targetPhaseId && readiness.isError ? <ErrorState title={t("projects.phaseGate.loadFailed")} onRetry={() => void readiness.refetch()} /> : null}
        {gate ? <>
          <div className="flex flex-wrap items-center gap-2">
            <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-medium ${statusClass(gate.readinessStatus)}`}>{readinessLabel(gate.readinessStatus, t)}</span>
            {gate.configurationVersionNumber != null ? <span className="text-xs text-muted-foreground">{t("projects.phaseGate.configurationVersion", { version: gate.configurationVersionNumber })}</span> : null}
          </div>
          {gate.readinessPercentage != null ? <Progress value={gate.readinessPercentage} aria-label={t("projects.phaseGate.completionProgress", { complete: formatNumber(gate.satisfiedMandatoryCount), total: formatNumber(gate.mandatoryRequirementCount) })} /> : null}
          <p className="text-xs text-muted-foreground">{t("projects.phaseGate.requirementCountSummary", {
            mandatorySatisfied: formatNumber(gate.satisfiedMandatoryCount),
            mandatory: formatNumber(gate.mandatoryRequirementCount),
            optionalSatisfied: formatNumber(gate.satisfiedOptionalCount),
            optional: formatNumber(gate.optionalRequirementCount),
            missing: formatNumber(gate.missingMandatoryCount),
          })}</p>
          {showAdvisory ? <p className="flex items-start gap-2 text-sm text-warning">
            <AlertTriangle aria-hidden="true" className="mt-0.5 h-4 w-4 shrink-0" />{t("projects.phaseGate.transitionNonBlocking")}
          </p> : null}
          {gate.readinessStatus === "NotConfigured" ? <p role="note" className="text-sm text-muted-foreground">{t("projects.phaseGate.notConfiguredMessage")}</p> : null}
          {gate.readinessStatus === "NotApplicable" ? <p role="note" className="text-sm text-muted-foreground">{t("projects.phaseGate.readinessNotApplicableDescription")}</p> : null}
          {gateApplies ? <>
            {gate.requirements.length === 0
              ? <p className="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground">{t("projects.phaseGate.noRequirements")}</p>
              : <div className="space-y-5">
                {canManageEvidence ? <div className="relative"><Search aria-hidden="true" className="absolute start-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={documentSearch} onChange={(event) => setDocumentSearch(event.target.value)} placeholder={t("documents.search")} aria-label={t("documents.search")} className="ps-8" /></div> : null}
                <section aria-labelledby="change-phase-mandatory-documents-title" className="space-y-3">
                  <h3 id="change-phase-mandatory-documents-title" className="text-sm font-semibold">{t("projects.phaseGate.mandatoryDocuments")}</h3>
                  {mandatoryRequirements.length === 0
                    ? <p className="rounded-lg border border-dashed border-border p-4 text-center text-xs text-muted-foreground">{t("projects.phaseGate.noMandatoryRequirements")}</p>
                    : mandatoryRequirements.map(renderRequirementCard)}
                </section>
                <section aria-labelledby="change-phase-optional-documents-title" className="space-y-3">
                  <h3 id="change-phase-optional-documents-title" className="text-sm font-semibold">{t("projects.phaseGate.optionalDocuments")}</h3>
                  {optionalRequirements.length === 0
                    ? <p className="rounded-lg border border-dashed border-border p-4 text-center text-xs text-muted-foreground">{t("projects.phaseGate.noOptionalRequirements")}</p>
                    : optionalRequirements.map(renderRequirementCard)}
                </section>
              </div>}
          </> : null}
        </> : null}
        {link.isError || unlink.isError || upload.isError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("documents.actionFailed")}</p> : null}
      </CardContent>
    </Card>

    <Card className="overflow-hidden">
      <CardHeader className="border-b bg-muted/30 pb-4">
        <div className="flex items-start gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><History aria-hidden="true" className="h-4 w-4" /></span>
          <div className="min-w-0">
            <CardTitle className="text-base">{t("projects.phaseGate.historyTitle")}</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">{t("projects.phaseGate.historyDescription")}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3 p-5 sm:p-6">
        {history.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : null}
        {history.isError ? <div className="flex flex-wrap items-center justify-between gap-3">
          <p role="alert" className="text-sm text-destructive">{t("projects.phaseGate.historyLoadFailed")}</p>
          <Button type="button" size="sm" variant="outline" onClick={() => void history.refetch()}>{t("common.retry")}</Button>
        </div> : null}
        {!history.isLoading && !history.isError && (history.data?.length ?? 0) === 0 ? <p className="text-sm text-muted-foreground">{t("projects.phaseGate.historyEmpty")}</p> : null}
        {!history.isLoading && !history.isError && (history.data?.length ?? 0) > 0 ? <ol className="space-y-3">
          {history.data!.map((entry) => <li key={entry.id}><PhaseHistoryEntry entry={entry} /></li>)}
        </ol> : null}
      </CardContent>
    </Card>
  </section>;
}

function PhaseHistoryEntry({ entry }: { entry: ProjectPhaseHistoryItem }) {
  const { t, locale, formatNumber, formatDateTime } = useI18n();
  const previousLabel = localizedPhaseLabel(entry.previousPhase, entry.previousPhaseLabelEn, entry.previousPhaseLabelAr, locale) || t("common.notAvailable");
  const newLabel = localizedPhaseLabel(entry.newPhase, entry.newPhaseLabelEn, entry.newPhaseLabelAr, locale) || t("common.notAvailable");
  const requirements = entry.requirements ?? [];
  const outcome = entry.evaluationOutcome;

  return <article className="rounded-lg border border-border bg-background p-4">
    <div className="flex flex-wrap items-start justify-between gap-2">
      <p className="flex min-w-0 flex-wrap items-center gap-2 text-sm font-semibold">
        <span className="break-words">{previousLabel}</span>
        <ArrowRight aria-hidden="true" className="h-4 w-4 shrink-0 text-muted-foreground rtl:rotate-180" />
        <span className="break-words">{newLabel}</span>
      </p>
      <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-medium ${outcome ? statusClass(outcome) : "border-border bg-muted/40 text-muted-foreground"}`}>
        {evaluationOutcomeLabel(outcome, t)}
      </span>
    </div>
    <p className="mt-2 flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
      <span>{t("projects.phaseGate.historyActor")}: {entry.actorName || t("common.notAvailable")}</span>
      {entry.transitionedAt ? <><span aria-hidden="true">·</span><time dateTime={entry.transitionedAt}>{formatDateTime(entry.transitionedAt)}</time></> : null}
      {entry.configurationVersionNumber != null ? <><span aria-hidden="true">·</span><span>{t("projects.phaseGate.configurationVersion", { version: entry.configurationVersionNumber })}</span></> : null}
    </p>
    <p className="mt-1 text-xs text-muted-foreground">{t("projects.phaseGate.requirementCountSummary", {
      mandatorySatisfied: formatNumber(entry.satisfiedMandatoryCount ?? 0),
      mandatory: formatNumber(entry.mandatoryRequirementCount ?? 0),
      optionalSatisfied: formatNumber(entry.satisfiedOptionalCount ?? 0),
      optional: formatNumber(entry.optionalRequirementCount ?? 0),
      missing: formatNumber(entry.missingMandatoryCount ?? 0),
    })}</p>
    <details className="mt-3 rounded-md border border-border bg-muted/20 p-3">
      <summary className="cursor-pointer text-xs font-semibold">{t("projects.phaseGate.historyRequirements")}</summary>
      {requirements.length === 0
        ? <p className="mt-2 text-xs text-muted-foreground">{t("projects.phaseGate.historyNoRequirements")}</p>
        : <ul className="mt-2 space-y-3">
          {requirements.map((requirement) => <li key={requirement.requirementSnapshotId} className="space-y-1 text-xs">
            <div className="flex flex-wrap items-center gap-2">
              <span className="break-words font-medium text-foreground">{requirement.templateName}</span>
              <span className="text-muted-foreground">{t("documents.table.version")} {requirement.templateVersionLabel}</span>
              <Badge status={requirement.requirementType}>{requirement.requirementType === "Mandatory" ? t("projects.phaseGate.mandatory") : t("projects.phaseGate.optional")}</Badge>
              <span className={`inline-flex rounded-full border px-2 py-0.5 text-[11px] font-medium ${statusClass(requirement.status)}`}>{requirementStatusLabel(requirement.status, t)}</span>
              <Badge status={requirement.complianceStatus}>{complianceStatusLabel(requirement.complianceStatus, t)}</Badge>
            </div>
            {requirement.expectedFileType || requirement.detectedFileType ? <p className="text-muted-foreground">
              {t("projects.phaseGate.expectedFileType", { type: fileTypeLabel(requirement.expectedFileType, t) })} · {t("projects.phaseGate.detectedFileType", { type: fileTypeLabel(requirement.detectedFileType, t) })}
            </p> : null}
            {findingsOf(requirement).length ? <ul className="space-y-1 text-muted-foreground">
              {findingsOf(requirement).map((finding, index) => <li key={`${finding.code}-${index}`}>
                <p>{locale === "ar" ? finding.messageAr : finding.messageEn}</p>
                {finding.expected != null || finding.detected != null ? <p className="text-[11px]">
                  {finding.expected != null ? <span>{t("projects.phaseGate.findingExpectedValue", { value: finding.expected })}</span> : null}
                  {finding.expected != null && finding.detected != null ? <span aria-hidden="true"> · </span> : null}
                  {finding.detected != null ? <span>{t("projects.phaseGate.findingDetectedValue", { value: finding.detected })}</span> : null}
                </p> : null}
              </li>)}
            </ul> : null}
            {requirement.comparedAt || requirement.validatorVersion ? <p className="flex flex-wrap items-center gap-x-2 text-muted-foreground">
              {requirement.comparedAt ? <time dateTime={requirement.comparedAt}>{t("projects.phaseGate.comparedAt", { date: formatDateTime(requirement.comparedAt) })}</time> : null}
              {requirement.validatorVersion ? <span>{t("projects.phaseGate.validatorVersion", { version: requirement.validatorVersion })}</span> : null}
            </p> : null}
          </li>)}
        </ul>}
    </details>
  </article>;
}
