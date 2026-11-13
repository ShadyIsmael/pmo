import * as React from "react";
import { Link, useOutletContext, useParams } from "react-router-dom";
import { AlertTriangle, ArrowLeft, ArrowRight, Camera, Eye, History, Lock, Printer, type LucideIcon } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { PageHeader } from "@components/layout/PageHeader";
import { useProjectSnapshot, useProjectSnapshots, type ProjectSnapshotCollectionItem, type ProjectSnapshotDataDto, type ProjectSnapshotDto, type ProjectSnapshotSummaryDto } from "./api/projects";
import { useProjectEnumLabels } from "./lib/project-labels";
import { projectLifecycleLabel, useProjectLifecycleOptions } from "./api/project-lifecycle";
import { formatDateTime } from "@lib/utils";
import { formatNumberValue } from "@lib/formatting/number";
import { getRiskLevelTextClass, getRiskStatusTextClass, getStatusTextClass, getStatusTone, statusToneDotClasses } from "@lib/status-presentation";

type SnapshotCollectionKey = keyof ProjectSnapshotDataDto;
type SnapshotGroupKey = "people" | "planDelivery" | "governance" | "financial";
type Translator = (key: string, values?: Record<string, string | number>) => string;

interface SnapshotSectionDefinition {
  key: SnapshotCollectionKey;
  titleKey: string;
  group: SnapshotGroupKey;
}

const snapshotSections: readonly SnapshotSectionDefinition[] = [
  { key: "members", titleKey: "projects.snapshotTeam", group: "people" },
  { key: "allocations", titleKey: "projects.snapshotAllocations", group: "people" },
  { key: "stakeholders", titleKey: "projects.snapshotStakeholders", group: "people" },
  { key: "phases", titleKey: "projects.snapshotPhases", group: "planDelivery" },
  { key: "milestones", titleKey: "projects.snapshotMilestones", group: "planDelivery" },
  { key: "deliverables", titleKey: "projects.snapshotDeliverables", group: "planDelivery" },
  { key: "schedules", titleKey: "projects.snapshotSchedules", group: "planDelivery" },
  { key: "wbsItems", titleKey: "projects.snapshotWbsItems", group: "planDelivery" },
  { key: "baselines", titleKey: "projects.snapshotBaselines", group: "planDelivery" },
  { key: "workItems", titleKey: "projects.snapshotWorkItems", group: "governance" },
  { key: "risks", titleKey: "projects.snapshotRisks", group: "governance" },
  { key: "issues", titleKey: "projects.snapshotIssues", group: "governance" },
  { key: "meetings", titleKey: "projects.snapshotMeetings", group: "governance" },
  { key: "statusReports", titleKey: "projects.snapshotStatusReports", group: "governance" },
  { key: "documents", titleKey: "projects.snapshotDocuments", group: "governance" },
  { key: "budgets", titleKey: "projects.snapshotBudgets", group: "financial" },
  { key: "costEntries", titleKey: "projects.snapshotCostEntries", group: "financial" },
];

const snapshotGroups: readonly { key: SnapshotGroupKey; titleKey: string }[] = [
  { key: "people", titleKey: "projects.snapshotGroupPeople" },
  { key: "planDelivery", titleKey: "projects.snapshotGroupPlanDelivery" },
  { key: "governance", titleKey: "projects.snapshotGroupGovernance" },
  { key: "financial", titleKey: "projects.snapshotGroupFinancial" },
];

export function ProjectSnapshotDetailPage() {
  return <ProjectSnapshotsTab />;
}

export function ProjectSnapshotsTab() {
  const context = useOutletContext<{ projectId?: string } | undefined>();
  const { id: routeProjectId, snapshotId } = useParams<{ id?: string; snapshotId?: string }>();
  const projectId = context?.projectId ?? routeProjectId ?? "";
  const { t, locale } = useI18n();
  const snapshotsQuery = useProjectSnapshots(projectId, !snapshotId && Boolean(projectId));
  const snapshotQuery = useProjectSnapshot(projectId, snapshotId ?? "", Boolean(snapshotId && projectId));
  const lifecycleOptions = useProjectLifecycleOptions({ enabled: Boolean(snapshotId && projectId), projectId });

  if (!projectId) {
    return <SnapshotMessage icon="error" title={t("projects.snapshotScopeUnavailable")} />;
  }

  if (snapshotId) {
    if (snapshotQuery.isLoading) return <SnapshotPreviewLoading t={t} />;
    if (snapshotQuery.isError || !snapshotQuery.data) {
      return <SnapshotMessage icon="error" title={t("projects.snapshotLoadError")} projectId={projectId} />;
    }
    return <>
      {lifecycleOptions.isLoading ? <p role="status" className="mb-3 text-xs text-muted-foreground">{t("projects.lifecycle.phaseLookupLoading")} · {t("projects.lifecycle.statusLookupLoading")}</p> : null}
      {lifecycleOptions.isError ? <div className="mb-3 flex flex-wrap items-center gap-2"><p role="alert" className="text-xs text-destructive">{t("projects.lifecycle.lookupError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void lifecycleOptions.refetch()}>{t("projects.lifecycle.retry")}</Button></div> : null}
      <SnapshotPreview snapshot={snapshotQuery.data} projectId={projectId} lifecycleOptions={lifecycleOptions} />
    </>;
  }

  const rows = snapshotsQuery.data ?? [];
  return (
    <div className="space-y-5">
      <PageHeader
        title={t("projects.snapshots")}
        description={t("projects.snapshotsDescription")}
        actions={snapshotsQuery.isLoading || snapshotsQuery.isError ? undefined : <span aria-label={`${t("projects.snapshots")}: ${formatNumberValue(rows.length, locale)}`} className="rounded-full border border-border px-3 py-1 text-sm font-medium tabular-nums">{formatNumberValue(rows.length, locale)}</span>}
      />

      {snapshotsQuery.isLoading ? <SnapshotListLoading t={t} /> : null}
      {lifecycleOptions.isLoading ? <p role="status" className="text-xs text-muted-foreground">{t("projects.lifecycle.phaseLookupLoading")} · {t("projects.lifecycle.statusLookupLoading")}</p> : null}
      {lifecycleOptions.isError ? <div className="flex flex-wrap items-center gap-2"><p role="alert" className="text-xs text-destructive">{t("projects.lifecycle.lookupError")}</p><Button type="button" size="sm" variant="outline" onClick={() => void lifecycleOptions.refetch()}>{t("projects.lifecycle.retry")}</Button></div> : null}
      {snapshotsQuery.isError ? <SnapshotMessage icon="error" title={t("projects.snapshotListLoadError")} onRetry={() => void snapshotsQuery.refetch()} /> : null}
      {!snapshotsQuery.isLoading && !snapshotsQuery.isError && rows.length === 0 ? <SnapshotMessage icon="empty" title={t("projects.noSnapshots")} description={t("projects.noSnapshotsDescription")} /> : null}
      {!snapshotsQuery.isLoading && !snapshotsQuery.isError && rows.length > 0 ? <SnapshotList rows={rows} projectId={projectId} /> : null}
    </div>
  );
}

function SnapshotList({ rows, projectId: _projectId }: { rows: ProjectSnapshotSummaryDto[]; projectId: string }) {
  const { t, locale, formatDate } = useI18n();
  return (
    <ol className="overflow-hidden rounded-lg border border-border bg-card">
      {rows.map((snapshot) => {
        const typeKey = `projects.snapshotTypes.${snapshot.snapshotType}`;
        const typeLabel = t(typeKey) === typeKey ? t("projects.unknownSnapshotType") : t(typeKey);
        const hasManagerTransition = Boolean(snapshot.previousProjectManagerName?.trim() || snapshot.newProjectManagerName?.trim());
        return (
          <li key={snapshot.id} className="border-b border-border last:border-b-0">
            <div className="flex flex-col gap-4 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-5">
              <div className="flex min-w-0 items-start gap-3">
                <span aria-hidden="true" className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground"><History className="h-4 w-4" /></span>
                <div className="min-w-0 flex-1 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="break-words text-sm font-semibold text-foreground">{snapshot.snapshotName || t("projects.snapshotRecordNameUnavailable")}</h2>
                    <Badge variant="outline">{typeLabel}</Badge>
                    <span className="text-xs tabular-nums text-muted-foreground">{t("projects.snapshotNumber")} #{formatNumberValue(snapshot.snapshotNumber, locale)}</span>
                  </div>
                  <dl className="grid min-w-0 gap-x-6 gap-y-1 text-xs sm:grid-cols-2 xl:grid-cols-4">
                    <SnapshotListMeta label={t("projects.snapshotDate")} value={formatDate(snapshot.snapshotDate, { dateStyle: "medium" })} />
                    <SnapshotListMeta label={t("projects.effectiveEndDate")} value={formatDate(snapshot.effectiveEndDate, { dateStyle: "medium" })} />
                    <SnapshotListMeta label={t("projects.snapshotCreatedAt")} value={formatDateTime(snapshot.createdAt, locale)} />
                    <SnapshotListMeta label={t("projects.createdBy")} value={snapshot.createdByName || t("projects.snapshotCreatorUnavailable")} />
                  </dl>
                  {hasManagerTransition ? <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground"><span>{t("projects.snapshotManagerTransition")}:</span><span>{snapshot.previousProjectManagerName || t("projects.snapshotValueUnavailable")}</span><ArrowRight aria-hidden="true" className="h-3 w-3 rtl:rotate-180" /><span>{snapshot.newProjectManagerName || t("projects.snapshotValueUnavailable")}</span></div> : null}
                  {snapshot.handoverReason ? <p className="line-clamp-2 break-words text-sm text-muted-foreground">{snapshot.handoverReason}</p> : null}
                </div>
              </div>
              <SnapshotNavigationLink to={snapshot.id} Icon={Eye} className="self-start sm:self-center" aria-label={`${t("projects.viewSnapshot")}: ${snapshot.snapshotName || t("projects.snapshotRecordNameUnavailable")}`}>{t("projects.viewSnapshot")}</SnapshotNavigationLink>
            </div>
          </li>
        );
      })}
    </ol>
  );
}

function SnapshotListMeta({ label, value }: { label: string; value: string }) {
  return <div className="flex min-w-0 items-baseline gap-1.5"><dt className="shrink-0 text-muted-foreground">{label}:</dt><dd className="min-w-0 break-words font-medium text-foreground">{value}</dd></div>;
}

function SnapshotPreview({ snapshot, projectId, lifecycleOptions }: { snapshot: ProjectSnapshotDto; projectId: string; lifecycleOptions: ReturnType<typeof useProjectLifecycleOptions> }) {
  const { t, locale, formatDate, formatCurrency } = useI18n();
  const { priorityLabel, healthLabel, healthStatusLabel } = useProjectEnumLabels();
  const notRecorded = t("projects.snapshotValueUnavailable");
  const status = lifecycleOptions.data?.statuses.find((item) => item.code.toLocaleUpperCase() === snapshot.status?.toLocaleUpperCase());
  const phase = lifecycleOptions.data?.phases.find((item) => item.code.toLocaleUpperCase() === snapshot.phase?.toLocaleUpperCase());
  const statusLabel = projectLifecycleLabel(status, locale, snapshot.status || notRecorded);
  const phaseLabel = projectLifecycleLabel(phase, locale, snapshot.phase || notRecorded);
  const snapshotProjectIdentity = [snapshot.name, snapshot.code ? `(${snapshot.code})` : undefined].filter(Boolean).join(" ");
  const snapshotTypeKey = `projects.snapshotTypes.${snapshot.snapshotType}`;
  const snapshotType = t(snapshotTypeKey) === snapshotTypeKey ? t("projects.unknownSnapshotType") : t(snapshotTypeKey);
  const formatSnapshotMoney = (value?: number | null) => typeof value === "number" && Number.isFinite(value) && snapshot.currency?.trim() ? formatCurrency(value, snapshot.currency) : notRecorded;
  const formatSnapshotPercent = (value?: number | null) => typeof value === "number" && Number.isFinite(value) ? `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}%` : notRecorded;

  return <>
    <article className="snapshot-screen-preview space-y-4 rounded-xl border border-border bg-card p-4 text-card-foreground shadow-sm sm:space-y-5 sm:p-5">
      <PageHeader
        title={snapshot.snapshotName || t("projects.snapshot")}
        description={t("projects.snapshotForProject", { project: snapshotProjectIdentity || notRecorded })}
        actions={<div className="flex flex-wrap items-center gap-2"><Button type="button" size="sm" variant="outline" className="whitespace-nowrap" onClick={() => window.print()}><Printer className="me-1.5 h-4 w-4" />{t("projects.printSnapshotDocument")}</Button><SnapshotNavigationLink to={`/projects/${encodeURIComponent(projectId)}/snapshots`} Icon={ArrowLeft} iconClassName="rtl:rotate-180">{t("projects.backToSnapshots")}</SnapshotNavigationLink></div>}
      />

      <div className="flex flex-wrap items-center gap-2" aria-label={t("projects.snapshotClassification")}>
        <Badge variant="secondary"><Lock className="me-1 h-3 w-3" />{t("projects.readOnlySnapshot")}</Badge>
        <Badge variant="outline">{snapshotType}</Badge>
        <Badge variant="outline">{t("projects.snapshotNumber")} #{formatNumberValue(snapshot.snapshotNumber, locale)}</Badge>
        <SnapshotStatusIndicator status={snapshot.status} label={statusLabel} />
        <SnapshotStatusIndicator status={snapshot.health} label={healthLabel(snapshot.health)} />
      </div>

      <dl className="grid gap-x-6 rounded-lg border border-border bg-muted px-3 py-2 sm:grid-cols-2 lg:grid-cols-4">
        <SnapshotHeaderField label={t("projects.snapshotDate")} value={formatDate(snapshot.snapshotDate, { dateStyle: "medium" })} />
        <SnapshotHeaderField label={t("projects.effectiveEndDate")} value={formatDate(snapshot.effectiveEndDate, { dateStyle: "medium" })} />
        <SnapshotHeaderField label={t("projects.snapshotCreatedAt")} value={formatDateTime(snapshot.createdAt, locale)} />
        <SnapshotHeaderField label={t("projects.createdBy")} value={snapshot.createdByName || t("projects.snapshotCreatorUnavailable")} />
      </dl>

      <section aria-label={t("projects.snapshotKeyMeasures")} className="grid grid-cols-2 rounded-lg border border-border bg-muted sm:grid-cols-4">
        <SnapshotMetric label={t("projects.progress")} value={formatSnapshotPercent(snapshot.progress)} />
        <SnapshotMetric label={t("projects.budget")} value={formatSnapshotMoney(snapshot.budget)} financial />
        <SnapshotMetric label={t("projects.actualCost")} value={formatSnapshotMoney(snapshot.actualCost)} financial />
        <SnapshotMetric label={t("projects.snapshotFields.earnedValue")} value={formatSnapshotMoney(snapshot.earnedValue)} financial />
      </section>

      <SnapshotDetailGroup title={t("projects.snapshotProjectDetails")}>
        <SnapshotDetailField label={t("projects.project")} value={snapshot.name} t={t} />
        <SnapshotDetailField label={t("projects.code")} value={snapshot.code} t={t} />
        <SnapshotDetailField label={t("common.status")} value={statusLabel} valueClassName={getStatusTextClass(snapshot.status)} t={t} />
        <SnapshotDetailField label={t("projects.priority")} value={priorityLabel(snapshot.priority)} t={t} />
        <SnapshotDetailField label={t("projects.phase")} value={phaseLabel} t={t} />
        <SnapshotDetailField label={t("projects.health")} value={healthLabel(snapshot.health)} valueClassName={getStatusTextClass(snapshot.health)} t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.ragStatus")} value={snapshot.ragStatus ? healthStatusLabel(snapshot.ragStatus) : undefined} valueClassName={snapshot.ragStatus ? getStatusTextClass(snapshot.ragStatus) : undefined} t={t} />
        <SnapshotDetailField label={t("projects.confidenceLevel")} value={snapshot.confidenceLevel} t={t} />
        <SnapshotDetailField label={t("projects.riskRating")} value={snapshot.riskRating} t={t} />
        <SnapshotDetailField label={t("projects.category")} value={snapshot.category} t={t} />
      </SnapshotDetailGroup>

      <SnapshotDetailGroup title={t("projects.snapshotOrganizationalContext")}>
        <SnapshotDetailField label={t("projects.portfolio")} value={snapshot.portfolioName} t={t} />
        <SnapshotDetailField label={t("projects.program")} value={snapshot.programName} t={t} />
        <SnapshotDetailField label={t("projects.department")} value={snapshot.departmentName} t={t} />
        <SnapshotDetailField label={t("projects.sponsor")} value={snapshot.sponsorName} t={t} />
      </SnapshotDetailGroup>

      <SnapshotDetailGroup title={t("projects.snapshotScheduleAndPerformance")}>
        <SnapshotDetailField label={t("projects.startDate")} value={snapshot.startDate ? formatDate(snapshot.startDate, { dateStyle: "medium" }) : undefined} t={t} />
        <SnapshotDetailField label={t("projects.endDate")} value={snapshot.endDate ? formatDate(snapshot.endDate, { dateStyle: "medium" }) : undefined} t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.actualStart")} value={snapshot.actualStartDate ? formatDate(snapshot.actualStartDate, { dateStyle: "medium" }) : undefined} t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.actualEnd")} value={snapshot.actualEndDate ? formatDate(snapshot.actualEndDate, { dateStyle: "medium" }) : undefined} t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.plannedValue")} value={formatSnapshotMoney(snapshot.plannedValue)} valueClassName="text-financial-total" t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.earnedValue")} value={formatSnapshotMoney(snapshot.earnedValue)} valueClassName="text-financial-total" t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.costVariance")} value={formatSnapshotMoney(snapshot.costVariance)} valueClassName={varianceTextClass(snapshot.costVariance)} t={t} />
        <SnapshotDetailField label={t("projects.snapshotFields.scheduleVariance")} value={formatSnapshotMoney(snapshot.scheduleVariance)} valueClassName={varianceTextClass(snapshot.scheduleVariance)} t={t} />
        <SnapshotDetailField label={t("projects.currencyCode")} value={snapshot.currency} t={t} />
      </SnapshotDetailGroup>

      {snapshot.handoverReason || snapshot.handoverNotes || snapshot.previousProjectManagerName || snapshot.newProjectManagerName ? (
        <SnapshotDetailGroup title={t("projects.handoverDetails")}>
          <SnapshotDetailField label={t("projects.previousManager")} value={snapshot.previousProjectManagerName} t={t} />
          <SnapshotDetailField label={t("projects.newManager")} value={snapshot.newProjectManagerName} t={t} />
          <SnapshotDetailField label={t("projects.handoverReason")} value={snapshot.handoverReason} t={t} />
          <SnapshotDetailField label={t("projects.handoverNotes")} value={snapshot.handoverNotes} t={t} />
        </SnapshotDetailGroup>
      ) : null}

      {snapshot.description || snapshot.businessCase || snapshot.notes ? (
        <SnapshotDetailGroup title={t("projects.snapshotNarrativeDetails")}>
          <SnapshotDetailField label={t("projects.projectDescription")} value={snapshot.description} t={t} wide />
          <SnapshotDetailField label={t("projects.businessCase")} value={snapshot.businessCase} t={t} wide />
          <SnapshotDetailField label={t("projects.notes")} value={snapshot.notes} t={t} wide />
        </SnapshotDetailGroup>
      ) : null}

      <SnapshotCollections snapshot={snapshot} />
    </article>
    <SnapshotPrintDocument snapshot={snapshot} projectIdentity={snapshotProjectIdentity || notRecorded} statusLabel={statusLabel} phaseLabel={phaseLabel} />
  </>;
}

function SnapshotNavigationLink({ to, Icon, children, className = "", iconClassName = "", "aria-label": ariaLabel }: {
  to: string;
  Icon: LucideIcon;
  children: React.ReactNode;
  className?: string;
  iconClassName?: string;
  "aria-label"?: string;
}) {
  return <Link to={to} aria-label={ariaLabel} className={`inline-flex h-9 min-w-max shrink-0 flex-nowrap items-center justify-center gap-1.5 whitespace-nowrap rounded-md bg-primary px-3 text-xs font-medium leading-none text-primary-foreground shadow-sm ring-1 ring-primary/15 transition-colors hover:bg-primary/90 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 active:translate-y-px ${className}`}>
    <Icon aria-hidden="true" className={`h-4 w-4 shrink-0 ${iconClassName}`} />
    <span className="whitespace-nowrap">{children}</span>
  </Link>;
}

function SnapshotPrintDocument({ snapshot, projectIdentity, statusLabel, phaseLabel }: { snapshot: ProjectSnapshotDto; projectIdentity: string; statusLabel: string; phaseLabel: string }) {
  const { t, locale, formatDate, formatCurrency } = useI18n();
  const { priorityLabel, healthLabel, healthStatusLabel } = useProjectEnumLabels();
  const notRecorded = t("projects.snapshotValueUnavailable");
  const typeKey = `projects.snapshotTypes.${snapshot.snapshotType}`;
  const snapshotType = t(typeKey) === typeKey ? t("projects.unknownSnapshotType") : t(typeKey);
  const formatSnapshotMoney = (value?: number | null) => typeof value === "number" && Number.isFinite(value) && snapshot.currency?.trim() ? formatCurrency(value, snapshot.currency) : notRecorded;
  const formatSnapshotPercent = (value?: number | null) => typeof value === "number" && Number.isFinite(value) ? `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}%` : notRecorded;

  return <article className="snapshot-print-document" dir={locale.toLowerCase().startsWith("ar") ? "rtl" : "ltr"} lang={locale} aria-hidden="true">
    <header className="snapshot-print-header">
      <div>
        <p className="snapshot-print-eyebrow">{t("projects.snapshotDocument")}</p>
        <h1>{snapshot.snapshotName || t("projects.snapshot")}</h1>
        <p>{t("projects.snapshotForProject", { project: projectIdentity })}</p>
      </div>
      <div className="snapshot-print-classification">
        <p>{snapshotType}</p>
        <p>{t("projects.snapshotNumber")} #{formatNumberValue(snapshot.snapshotNumber, locale)}</p>
        <p><SnapshotStatusIndicator status={snapshot.status} label={statusLabel} /> · <SnapshotStatusIndicator status={snapshot.health} label={healthLabel(snapshot.health)} /></p>
      </div>
    </header>

    <section className="snapshot-print-section">
      <h2>{t("projects.snapshotDocumentRecord")}</h2>
      <dl className="snapshot-print-metadata">
        <SnapshotPrintMeta label={t("projects.snapshotDate")} value={formatDate(snapshot.snapshotDate, { dateStyle: "medium" })} />
        <SnapshotPrintMeta label={t("projects.effectiveEndDate")} value={formatDate(snapshot.effectiveEndDate, { dateStyle: "medium" })} />
        <SnapshotPrintMeta label={t("projects.snapshotCreatedAt")} value={formatDateTime(snapshot.createdAt, locale)} />
        <SnapshotPrintMeta label={t("projects.createdBy")} value={snapshot.createdByName || t("projects.snapshotCreatorUnavailable")} />
        <SnapshotPrintMeta label={t("projects.priority")} value={priorityLabel(snapshot.priority)} valueClassName={getStatusTextClass(snapshot.priority)} />
        <SnapshotPrintMeta label={t("projects.health")} value={healthLabel(snapshot.health)} valueClassName={getStatusTextClass(snapshot.health)} />
        {snapshot.ragStatus ? <SnapshotPrintMeta label={t("projects.snapshotFields.ragStatus")} value={healthStatusLabel(snapshot.ragStatus)} valueClassName={getStatusTextClass(snapshot.ragStatus)} /> : null}
      </dl>
    </section>

    <section className="snapshot-print-section">
      <h2>{t("projects.snapshotKeyMeasures")}</h2>
      <dl className="snapshot-print-measures">
        <SnapshotPrintMeta label={t("projects.progress")} value={formatSnapshotPercent(snapshot.progress)} />
        <SnapshotPrintMeta label={t("projects.budget")} value={formatSnapshotMoney(snapshot.budget)} valueClassName="text-financial-total" />
        <SnapshotPrintMeta label={t("projects.actualCost")} value={formatSnapshotMoney(snapshot.actualCost)} valueClassName="text-financial-total" />
        <SnapshotPrintMeta label={t("projects.snapshotFields.plannedValue")} value={formatSnapshotMoney(snapshot.plannedValue)} valueClassName="text-financial-total" />
        <SnapshotPrintMeta label={t("projects.snapshotFields.earnedValue")} value={formatSnapshotMoney(snapshot.earnedValue)} valueClassName="text-financial-total" />
        <SnapshotPrintMeta label={t("projects.snapshotFields.costVariance")} value={formatSnapshotMoney(snapshot.costVariance)} valueClassName={varianceTextClass(snapshot.costVariance)} />
        <SnapshotPrintMeta label={t("projects.snapshotFields.scheduleVariance")} value={formatSnapshotMoney(snapshot.scheduleVariance)} valueClassName={varianceTextClass(snapshot.scheduleVariance)} />
      </dl>
    </section>

    <SnapshotDetailGroup title={t("projects.snapshotProjectDetails")} includeUnavailable>
      <SnapshotDetailField label={t("projects.project")} value={snapshot.name} t={t} />
      <SnapshotDetailField label={t("projects.code")} value={snapshot.code} t={t} />
      <SnapshotDetailField label={t("common.status")} value={statusLabel} valueClassName={getStatusTextClass(snapshot.status)} t={t} />
      <SnapshotDetailField label={t("projects.priority")} value={priorityLabel(snapshot.priority)} t={t} />
      <SnapshotDetailField label={t("projects.phase")} value={phaseLabel} t={t} />
      <SnapshotDetailField label={t("projects.health")} value={healthLabel(snapshot.health)} valueClassName={getStatusTextClass(snapshot.health)} t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.ragStatus")} value={snapshot.ragStatus ? healthStatusLabel(snapshot.ragStatus) : undefined} valueClassName={snapshot.ragStatus ? getStatusTextClass(snapshot.ragStatus) : undefined} t={t} />
      <SnapshotDetailField label={t("projects.confidenceLevel")} value={snapshot.confidenceLevel} t={t} />
      <SnapshotDetailField label={t("projects.riskRating")} value={snapshot.riskRating} t={t} />
      <SnapshotDetailField label={t("projects.category")} value={snapshot.category} t={t} />
    </SnapshotDetailGroup>

    <SnapshotDetailGroup title={t("projects.snapshotOrganizationalContext")} includeUnavailable>
      <SnapshotDetailField label={t("projects.portfolio")} value={snapshot.portfolioName} t={t} />
      <SnapshotDetailField label={t("projects.program")} value={snapshot.programName} t={t} />
      <SnapshotDetailField label={t("projects.department")} value={snapshot.departmentName} t={t} />
      <SnapshotDetailField label={t("projects.sponsor")} value={snapshot.sponsorName} t={t} />
    </SnapshotDetailGroup>

    <SnapshotDetailGroup title={t("projects.snapshotScheduleAndPerformance")} includeUnavailable>
      <SnapshotDetailField label={t("projects.startDate")} value={snapshot.startDate ? formatDate(snapshot.startDate, { dateStyle: "medium" }) : undefined} t={t} />
      <SnapshotDetailField label={t("projects.endDate")} value={snapshot.endDate ? formatDate(snapshot.endDate, { dateStyle: "medium" }) : undefined} t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.actualStart")} value={snapshot.actualStartDate ? formatDate(snapshot.actualStartDate, { dateStyle: "medium" }) : undefined} t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.actualEnd")} value={snapshot.actualEndDate ? formatDate(snapshot.actualEndDate, { dateStyle: "medium" }) : undefined} t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.plannedValue")} value={formatSnapshotMoney(snapshot.plannedValue)} valueClassName="text-financial-total" t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.earnedValue")} value={formatSnapshotMoney(snapshot.earnedValue)} valueClassName="text-financial-total" t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.costVariance")} value={formatSnapshotMoney(snapshot.costVariance)} valueClassName={varianceTextClass(snapshot.costVariance)} t={t} />
      <SnapshotDetailField label={t("projects.snapshotFields.scheduleVariance")} value={formatSnapshotMoney(snapshot.scheduleVariance)} valueClassName={varianceTextClass(snapshot.scheduleVariance)} t={t} />
      <SnapshotDetailField label={t("projects.currencyCode")} value={snapshot.currency} t={t} />
    </SnapshotDetailGroup>

    <SnapshotDetailGroup title={t("projects.handoverDetails")} includeUnavailable>
      <SnapshotDetailField label={t("projects.previousManager")} value={snapshot.previousProjectManagerName} t={t} />
      <SnapshotDetailField label={t("projects.newManager")} value={snapshot.newProjectManagerName} t={t} />
      <SnapshotDetailField label={t("projects.handoverReason")} value={snapshot.handoverReason} t={t} wide />
      <SnapshotDetailField label={t("projects.handoverNotes")} value={snapshot.handoverNotes} t={t} wide />
    </SnapshotDetailGroup>

    <SnapshotDetailGroup title={t("projects.snapshotNarrativeDetails")} includeUnavailable>
      <SnapshotDetailField label={t("projects.projectDescription")} value={snapshot.description} t={t} wide />
      <SnapshotDetailField label={t("projects.businessCase")} value={snapshot.businessCase} t={t} wide />
      <SnapshotDetailField label={t("projects.notes")} value={snapshot.notes} t={t} wide />
    </SnapshotDetailGroup>

    <SnapshotPrintCollections snapshot={snapshot} />
  </article>;
}

function SnapshotPrintMeta({ label, value, valueClassName }: { label: string; value: string; valueClassName?: string }) {
  return <div><dt>{label}</dt><dd className={valueClassName}>{value}</dd></div>;
}

function SnapshotPrintCollections({ snapshot }: { snapshot: ProjectSnapshotDto }) {
  const { t, locale, formatDate, formatCurrency } = useI18n();
  return <section className="snapshot-print-section snapshot-print-collections">
    <h2>{t("projects.snapshotCapturedSections")}</h2>
    {snapshotGroups.map((group) => <section key={group.key} className="snapshot-print-group">
      <h3>{t(group.titleKey)}</h3>
      {snapshotSections.filter((section) => section.group === group.key).map((section) => {
        const records = snapshot.data?.[section.key];
        return <section key={section.key} className="snapshot-print-collection">
          <header><h4>{t(section.titleKey)}</h4><span>{Array.isArray(records) ? formatNumberValue(records.length, locale) : notRecordedLabel(t)}</span></header>
          {!Array.isArray(records) ? <p className="snapshot-print-empty">{t("projects.snapshotSectionUnavailable")}</p>
            : records.length === 0 ? <p className="snapshot-print-empty">{t("projects.snapshotSectionEmpty")}</p>
              : <ol>{records.map((item, index) => <SnapshotRecord key={item.originalId || `${section.key}-${index}`} item={item} section={section.key} index={index} t={t} locale={locale} formatDate={formatDate} formatCurrency={formatCurrency} />)}</ol>}
        </section>;
      })}
    </section>)}
  </section>;
}

function notRecordedLabel(t: Translator): string { return t("projects.snapshotValueUnavailable"); }

function SnapshotHeaderField({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0 py-1.5"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-0.5 break-words text-sm font-medium text-foreground">{value}</dd></div>;
}

function SnapshotMetric({ label, value, financial = false }: { label: string; value: string; financial?: boolean }) {
  return <div className="min-w-0 border-b border-border px-3 py-3 odd:border-e even:border-e-0 last:border-b-0 sm:border-b-0 sm:px-4 sm:first:ps-0 sm:odd:border-e sm:even:border-e-0 lg:py-4">
    <p className="text-xs text-muted-foreground">{label}</p>
    <p className={`mt-1 break-words text-base font-semibold leading-6 tabular-nums sm:text-lg ${financial ? "text-financial-total" : "text-foreground"}`}>{value}</p>
  </div>;
}

function SnapshotDetailGroup({ title, children, includeUnavailable = false }: { title: string; children: React.ReactNode; includeUnavailable?: boolean }) {
  const { t, locale } = useI18n();
  const fields = React.Children.toArray(children).flatMap((child) => React.isValidElement<SnapshotDetailFieldProps>(child) && child.type === SnapshotDetailField ? [child.props] : []);
  const recordedFields = fields.filter((field) => field.value != null && field.value !== "");
  const unavailableFields = fields.filter((field) => field.value == null || field.value === "");

  return <section className={`snapshot-print-section space-y-2 ${includeUnavailable ? "border-b border-border pb-3 snapshot-print-detail-group" : "rounded-lg border border-border bg-muted p-3 sm:p-4"}`}>
    <h2 className="text-sm font-semibold text-foreground">{title}</h2>
    {recordedFields.length > 0 ? <dl className="grid gap-x-5 sm:grid-cols-2 xl:grid-cols-3">{recordedFields.map((field, index) => <SnapshotDetailField key={`${field.label}-${index}`} {...field} />)}</dl> : null}
    {includeUnavailable && unavailableFields.length > 0 ? <dl className="snapshot-print-unavailable grid gap-x-5 sm:grid-cols-2 xl:grid-cols-3">{unavailableFields.map((field, index) => <SnapshotDetailField key={`${field.label}-${index}`} label={field.label} value={t("projects.snapshotValueUnavailable")} />)}</dl> : null}
    {!includeUnavailable && unavailableFields.length > 0 ? <details className="rounded-md bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
      <summary className="cursor-pointer rounded-sm font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">{t("projects.snapshotUnavailableFields", { count: formatNumberValue(unavailableFields.length, locale) })}</summary>
      <ul className="mt-2 grid gap-x-5 gap-y-1 sm:grid-cols-2">
        {unavailableFields.map((field, index) => <li key={`${field.label}-${index}`} className="flex flex-wrap items-baseline justify-between gap-x-2"><span>{field.label}</span><span className="text-muted-foreground">{t("projects.snapshotValueUnavailable")}</span></li>)}
      </ul>
    </details> : null}
  </section>;
}

interface SnapshotDetailFieldProps {
  label: string;
  value?: React.ReactNode;
  t?: (key: string) => string;
  wide?: boolean;
  valueClassName?: string;
}

function SnapshotDetailField({ label, value, wide = false, valueClassName }: SnapshotDetailFieldProps) {
  return <div className={`min-w-0 border-t border-border/60 py-1.5 ${wide ? "sm:col-span-2 xl:col-span-3" : ""}`}>
    <dt className="break-words text-[11px] text-muted-foreground">{label}</dt>
    <dd className={`mt-0.5 break-words text-sm leading-5 ${valueClassName ?? "text-foreground"}`}>{value}</dd>
  </div>;
}

function SnapshotStatusIndicator({ status, label }: { status: string | null | undefined; label: string }) {
  const tone = getStatusTone(status);
  return <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${getStatusTextClass(status)}`}>
    <span aria-hidden="true" className={`h-2 w-2 shrink-0 rounded-full ${statusToneDotClasses[tone]}`} />{label}
  </span>;
}

function varianceTextClass(value?: number | null): string {
  if (value == null || !Number.isFinite(value) || value === 0) return "text-muted-foreground";
  return value > 0 ? "text-success" : "text-destructive";
}

function SnapshotCollections({ snapshot }: { snapshot: ProjectSnapshotDto }) {
  const { t, locale, formatDate, formatCurrency } = useI18n();
  const capturedData = snapshot.data;

  if (!capturedData) {
    return <section aria-labelledby="snapshot-collections-title" className="space-y-2 border-t border-border pt-4">
      <h2 id="snapshot-collections-title" className="text-sm font-semibold text-foreground">{t("projects.snapshotCapturedSections")}</h2>
      <p role="status" className="rounded-md border border-border px-4 py-3 text-sm text-muted-foreground">{t("projects.snapshotCollectionsUnavailable")}</p>
    </section>;
  }

  const availableSections = snapshotSections.filter(({ key }) => {
    const records = capturedData[key];
    return Array.isArray(records) && records.length > 0;
  });
  const otherSections = snapshotSections.filter(({ key }) => {
    const records = capturedData[key];
    return !Array.isArray(records) || records.length === 0;
  });

  return (
    <div className="space-y-4 border-t border-border pt-4">
      <div className="space-y-3 rounded-lg border border-border bg-muted p-3 sm:p-4">
        <h2 className="text-sm font-semibold text-foreground">{t("projects.snapshotCapturedSections")}</h2>
        {availableSections.length > 0 ? <nav aria-label={t("projects.snapshotContents")} className="flex flex-wrap gap-2">
          {availableSections.map(({ key, titleKey }) => {
            const records = capturedData[key];
            return <a key={key} href={`#snapshot-section-${key}`} className="inline-flex min-h-8 items-center gap-2 rounded-md border border-border bg-card px-2.5 py-1 text-xs text-foreground shadow-sm transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              <span>{t(titleKey)}</span><span className="tabular-nums text-muted-foreground">{Array.isArray(records) ? records.length : "—"}</span>
            </a>;
          })}
        </nav> : <p className="text-sm text-muted-foreground">{t("projects.snapshotContentsEmpty")}</p>}
      </div>

      {availableSections.length > 0 ? <div className="grid gap-x-8 lg:grid-cols-2">
        {snapshotGroups.map((group) => {
          const sections = availableSections.filter((section) => section.group === group.key);
          if (sections.length === 0) return null;
          return <section key={group.key} className="min-w-0 rounded-lg border border-border bg-muted p-3 sm:p-4">
            <h2 className="mb-3 text-sm font-semibold text-foreground">{t(group.titleKey)}</h2>
            <div className="space-y-4">
              {sections.map((section) => {
                const records = capturedData[section.key];
                return Array.isArray(records) && records.length > 0 ? <SnapshotCollectionSection key={section.key} section={section} records={records} t={t} locale={locale} formatDate={formatDate} formatCurrency={formatCurrency} /> : null;
              })}
            </div>
          </section>;
        })}
      </div> : null}

      {otherSections.length > 0 ? <details className="rounded-md border border-border bg-muted px-3 py-2 text-xs">
        <summary className="cursor-pointer font-medium text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">{t("projects.snapshotOtherSections", { count: formatNumberValue(otherSections.length, locale) })}</summary>
        <ul className="mt-2 grid gap-x-6 gap-y-1.5 sm:grid-cols-2">
          {otherSections.map((section) => {
            const records = capturedData[section.key];
            return <li key={section.key} className="flex flex-wrap items-baseline justify-between gap-x-2 border-t border-border/60 py-1.5">
              <span>{t(section.titleKey)}</span>
              <span className="text-muted-foreground">{Array.isArray(records) ? t("projects.snapshotSectionEmpty") : t("projects.snapshotSectionUnavailable")}</span>
            </li>;
          })}
        </ul>
      </details> : null}
    </div>
  );
}

function SnapshotCollectionSection({ section, records, t, locale, formatDate, formatCurrency }: {
  section: SnapshotSectionDefinition;
  records: ProjectSnapshotCollectionItem[];
  t: Translator;
  locale: string;
  formatDate: (value: string | Date, options?: Intl.DateTimeFormatOptions) => string;
  formatCurrency: (value: number, currency?: string) => string;
}) {
  const sectionId = `snapshot-section-${section.key}`;
  return <section id={sectionId} aria-labelledby={`${sectionId}-title`} className="min-w-0 scroll-mt-5 border-b border-border/70 pb-3 last:border-b-0">
    <div className="mb-1.5 flex items-baseline justify-between gap-3">
      <h3 id={`${sectionId}-title`} className="text-sm font-medium text-foreground">{t(section.titleKey)}</h3>
      <span className="shrink-0 text-xs tabular-nums text-muted-foreground">{records ? formatNumberValue(records.length, locale) : "—"}</span>
    </div>
    <ol className="divide-y divide-border/60">{records.map((item, index) => <SnapshotRecord key={item.originalId || `${section.key}-${index}`} item={item} section={section.key} index={index} t={t} locale={locale} formatDate={formatDate} formatCurrency={formatCurrency} />)}</ol>
  </section>;
}

interface SnapshotRecordField {
  label: string;
  value: string;
  valueClassName?: string;
}

function SnapshotRecord({ item, section, index, t, locale, formatDate, formatCurrency }: {
  item: ProjectSnapshotCollectionItem;
  section: SnapshotCollectionKey;
  index: number;
  t: Translator;
  locale: string;
  formatDate: (value: string | Date, options?: Intl.DateTimeFormatOptions) => string;
  formatCurrency: (value: number, currency?: string) => string;
}) {
  const notRecorded = t("projects.snapshotValueUnavailable");
  const recordedTitle = [item.name, item.title, item.resourceName, item.summary, item.category, item.fileName, item.code].find((value) => typeof value === "string" && value.trim());
  const title = recordedTitle || t("projects.snapshotRecordNumber", { number: formatNumberValue(index + 1, locale) });
  const fields: SnapshotRecordField[] = [];
  const add = (key: string, value?: string | number | boolean | null, formattedValue?: string, valueClassName?: string) => {
    if (value === undefined || value === null || value === "") return;
    const display = formattedValue ?? (typeof value === "boolean" ? t(value ? "projects.snapshotFields.yes" : "projects.snapshotFields.no") : String(value));
    fields.push({ label: t(`projects.snapshotFields.${key}`), value: display, valueClassName });
  };
  const addDate = (key: string, value?: string | null) => { if (value) add(key, value, formatDate(value, { dateStyle: "medium" })); };
  const addDateTime = (key: string, value?: string | null) => { if (value) add(key, value, formatDateTime(value, locale)); };
  const addPercent = (key: string, value?: number | null) => { if (typeof value === "number" && Number.isFinite(value)) add(key, value, `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}%`); };
  const addHours = (key: string, value?: number | null) => { if (typeof value === "number" && Number.isFinite(value)) add(key, value, formatNumberValue(value, locale, { maximumFractionDigits: 1 })); };
  const addMoney = (key: string, value?: number | null, currencyCode?: string | null) => {
    if (typeof value !== "number" || !Number.isFinite(value)) return;
    const code = currencyCode?.trim();
    const formattedValue = code ? formatCurrency(value, code) : `${formatNumberValue(value, locale)} · ${t("projects.snapshotFields.currencyUnavailable")}`;
    add(key, value, formattedValue, varianceTextClass(value));
  };
  const addAmountWithoutCurrency = (key: string, value?: number | null) => {
    if (typeof value !== "number" || !Number.isFinite(value)) return;
    add(key, value, `${formatNumberValue(value, locale)} · ${t("projects.snapshotFields.currencyUnavailable")}`, varianceTextClass(value));
  };
  const addStatus = (value?: string | null, group?: string, labelKey = "status") => {
    if (!value) return;
    const enumValue = localizedSnapshotEnum(value, [group, "riskStatus", "riskLevel", "projectStatus", "projectPriority", "projectHealth"].filter((candidate): candidate is string => Boolean(candidate)), t);
    add(labelKey, value, enumValue, labelKey === "riskStatus" ? getRiskStatusTextClass(value) : getStatusTextClass(value));
  };
  const addPriority = (value?: string | null) => { if (value) add("priority", value, localizedSnapshotEnum(value, ["projectPriority"], t), getStatusTextClass(value)); };
  const addText = (key: string, value?: string | null) => { if (value?.trim() && value !== title) add(key, value); };

  switch (section) {
    case "members":
      addText("role", item.role); addText("resourceTitle", item.resourceJobTitle); addStatus(item.status); addPercent("allocation", item.allocationPercentage);
      addHours("hoursPerWeek", item.hoursPerWeek); addDate("startDate", item.startDate); addDate("endDate", item.endDate);
      if (typeof item.isBillable === "boolean") add("billable", item.isBillable);
      addText("notes", item.notes);
      break;
    case "allocations":
      addStatus(item.status); addPercent("allocation", item.allocationPercentage); addHours("hoursPerWeek", item.hoursPerWeek);
      addDate("startDate", item.startDate); addDate("endDate", item.endDate); if (typeof item.isBillable === "boolean") add("billable", item.isBillable);
      addText("notes", item.notes);
      break;
    case "phases":
      addStatus(item.status); addPercent("progress", item.progress); addDate("startDate", item.startDate); addDate("endDate", item.endDate);
      break;
    case "milestones":
      addStatus(item.status); addPercent("progress", item.progress); addText("category", item.category); addText("assignee", item.assigneeName);
      addDate("dueDate", item.dueDate); addDate("completionDate", item.completedDate);
      break;
    case "deliverables":
      addStatus(item.status); addText("owner", item.ownerName); addDate("dueDate", item.dueDate); addDate("deliveryDate", item.deliveredDate);
      addText("qualityCriteria", item.qualityCriteria);
      break;
    case "stakeholders":
      addText("role", item.role); addText("interest", item.interest); addText("influence", item.influence); addText("email", item.email);
      break;
    case "schedules":
      addDate("baselineStart", item.baselineStartDate); addDate("baselineEnd", item.baselineEndDate); addDate("forecastStart", item.forecastStartDate);
      addDate("forecastEnd", item.forecastEndDate); addDate("actualStart", item.actualStartDate); addDate("actualEnd", item.actualEndDate);
      add("totalDays", item.totalDays, typeof item.totalDays === "number" ? formatNumberValue(item.totalDays, locale) : undefined);
      add("remainingDays", item.remainingDays, typeof item.remainingDays === "number" ? formatNumberValue(item.remainingDays, locale) : undefined);
      break;
    case "wbsItems":
      addText("code", item.code); addStatus(item.status); addPercent("progress", item.progress); addHours("plannedHours", item.plannedHours);
      addHours("actualHours", item.actualHours); addText("deliverable", item.deliverable);
      break;
    case "baselines":
      add("version", item.version, typeof item.version === "number" ? formatNumberValue(item.version, locale) : undefined);
      addDate("baselineDate", item.baselineDate); if (typeof item.isCurrent === "boolean") add("currentBaseline", item.isCurrent);
      break;
    case "statusReports":
      addStatus(item.status); addDate("periodStart", item.periodStart); addDate("periodEnd", item.periodEnd); addDateTime("reportedAt", item.reportedAt);
      addText("reportedBy", item.reportedByName); addAmountWithoutCurrency("scheduleVariance", item.scheduleVariance); addAmountWithoutCurrency("costVariance", item.costVariance);
      addText("summary", item.summary); addText("accomplishments", item.accomplishments); addText("nextSteps", item.nextSteps); addText("blockers", item.blockers);
      break;
    case "workItems":
      addText("type", item.type); addPriority(item.priority); addStatus(item.status); addText("assignee", item.assigneeName || item.assignedResourceName);
      addText("team", item.assignedTeamName); add("storyPoints", item.storyPoints, typeof item.storyPoints === "number" ? formatNumberValue(item.storyPoints, locale) : undefined);
      addHours("estimatedHours", item.estimatedHours); addHours("actualHours", item.actualHours); addDate("startDate", item.startDate);
      addDate("dueDate", item.dueDate); addDateTime("completedAt", item.completedAt);
      break;
    case "risks":
      add("riskLevel", item.riskLevel, item.riskLevel ? localizedSnapshotEnum(item.riskLevel, ["riskLevel"], t) : undefined, getRiskLevelTextClass(item.riskLevel));
      addStatus(item.status, "riskStatus", "riskStatus"); addText("owner", item.ownerName); addDate("identifiedDate", item.identifiedDate); addDate("resolutionDate", item.targetResolutionDate);
      addAmountWithoutCurrency("potentialCost", item.potentialCost); add("potentialDelay", item.potentialDelay, typeof item.potentialDelay === "number" ? `${formatNumberValue(item.potentialDelay, locale)} ${t("projects.days")}` : undefined);
      addText("riskResponse", item.riskResponse);
      break;
    case "issues":
      if (item.severity) add("severity", item.severity, localizedSnapshotEnum(item.severity, ["riskLevel", "projectPriority"], t), getRiskLevelTextClass(item.severity));
      addPriority(item.priority); addStatus(item.status); addText("assignee", item.assigneeName);
      addDate("identifiedDate", item.identifiedDate); addDate("resolutionDate", item.targetResolutionDate); addDate("resolvedDate", item.resolvedDate);
      addText("resolution", item.resolution);
      break;
    case "budgets":
      addText("category", item.category); addStatus(item.status); add("fiscalPeriod", item.fiscalYear, item.fiscalYear == null ? undefined : formatFiscalPeriod(item.fiscalYear, item.fiscalQuarter, locale, t));
      addMoney("plannedAmount", item.plannedAmount, item.currency); addMoney("approvedAmount", item.approvedAmount, item.currency); addMoney("actualAmount", item.actualAmount, item.currency);
      addMoney("committedAmount", item.committedAmount, item.currency); addMoney("forecastAmount", item.forecastAmount, item.currency); addDate("startDate", item.startDate); addDate("endDate", item.endDate);
      break;
    case "costEntries":
      addText("category", item.category); addText("subCategory", item.subCategory); addText("type", item.type); addStatus(item.status);
      addMoney("amount", item.amount, item.currency); addDate("incurredDate", item.incurredDate); addText("vendor", item.vendorName); addText("reference", item.reference);
      break;
    case "meetings":
      addText("meetingType", item.meetingType); addStatus(item.status); addDateTime("startTime", item.startTime); addDateTime("endTime", item.endTime);
      addText("organizer", item.organizerName); addText("recurrence", item.recurrence); addText("notes", item.notes);
      break;
    case "documents":
      addText("documentType", item.fileType); add("documentVersion", item.version, typeof item.version === "number" ? formatNumberValue(item.version, locale) : undefined);
      addStatus(item.status); addText("owner", item.ownerName); addText("fileName", item.fileName); addText("tags", item.tags);
      break;
  }

  const description = item.description?.trim() && item.description !== title ? item.description : undefined;
  return <li className="snapshot-print-record space-y-2 py-3 first:pt-1 last:pb-0">
    <h4 className="break-words text-sm font-medium text-foreground">{title}</h4>
    {description ? <p className="whitespace-pre-wrap break-words text-sm leading-6 text-muted-foreground">{description}</p> : null}
    {fields.length > 0 ? <dl className="grid gap-x-4 gap-y-1 sm:grid-cols-2 xl:grid-cols-3">{fields.map((field, fieldIndex) => <div key={`${field.label}-${fieldIndex}`} className="min-w-0"><dt className="inline text-xs text-muted-foreground">{field.label}: </dt><dd className={`inline break-words text-xs font-medium ${field.valueClassName ?? "text-foreground"}`}>{field.value}</dd></div>)}</dl> : <p className="text-xs text-muted-foreground">{notRecorded}</p>}
  </li>;
}

function localizedSnapshotEnum(value: string, groups: string[], t: Translator): string {
  const normalized = value.charAt(0).toLowerCase() + value.slice(1);
  for (const group of groups) {
    const key = `enums.${group}.${normalized}`;
    const label = t(key);
    if (label !== key) return label;
  }
  return value;
}

function formatFiscalPeriod(year: number, quarter: number | null | undefined, locale: string, t: Translator): string {
  const formattedYear = formatNumberValue(year, locale);
  return quarter == null ? formattedYear : `${t("projects.snapshotFields.quarter")} ${formatNumberValue(quarter, locale)} · ${formattedYear}`;
}

function SnapshotListLoading({ t }: { t: (key: string) => string }) {
  return <div role="status" aria-label={t("common.loading")} className="space-y-2 rounded-lg border border-border p-4 sm:p-5">
    {[0, 1, 2].map((row) => <div key={row} className="flex items-center gap-3 border-b border-border/60 py-3 last:border-b-0"><span className="h-9 w-9 animate-pulse rounded-md bg-muted" /><span className="h-4 w-full max-w-md animate-pulse rounded bg-muted" /></div>)}
  </div>;
}

function SnapshotPreviewLoading({ t }: { t: (key: string) => string }) {
  return <div role="status" aria-label={t("common.loading")} className="space-y-4">
    <div className="h-12 animate-pulse rounded bg-muted" />
    <div className="h-16 animate-pulse rounded border-y border-border bg-muted/50" />
    <div className="h-24 animate-pulse rounded border-y border-border bg-muted/50" />
    <div className="h-64 animate-pulse rounded bg-muted/50" />
  </div>;
}

function SnapshotMessage({ icon, title, description, projectId, onRetry }: {
  icon: "error" | "empty";
  title: string;
  description?: string;
  projectId?: string;
  onRetry?: () => void;
}) {
  const { t } = useI18n();
  const Icon = icon === "error" ? AlertTriangle : Camera;
  return <div className="flex min-h-44 flex-col items-center justify-center gap-3 rounded-lg border border-border px-4 py-8 text-center">
    <Icon aria-hidden="true" className={`h-5 w-5 ${icon === "error" ? "text-destructive" : "text-muted-foreground"}`} />
    <div className="space-y-1"><h2 className="text-sm font-medium text-foreground">{title}</h2>{description ? <p className="max-w-xl text-sm text-muted-foreground">{description}</p> : null}</div>
    <div className="flex flex-wrap justify-center gap-2">
      {onRetry ? <Button type="button" size="sm" variant="outline" onClick={onRetry}>{t("common.retry")}</Button> : null}
      {projectId ? <Button asChild size="sm" variant="outline"><Link to={`/projects/${encodeURIComponent(projectId)}/snapshots`}><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("projects.backToSnapshots")}</Link></Button> : null}
    </div>
  </div>;
}
