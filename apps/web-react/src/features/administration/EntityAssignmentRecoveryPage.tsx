import * as React from "react";
import { ArrowLeft, ArrowUpRight, ClipboardList, History, RefreshCw, Search, UserRoundCog } from "lucide-react";
import { Link } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useAuth } from "@auth/AuthProvider";
import { ApiError } from "@api/client";
import { useManagement } from "@api/management";
import { useI18n } from "@i18n/messages";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import { getApprovalReviewPath } from "@lib/approval-navigation";
import {
  useAssignmentRecoveryHistory,
  useAssignmentRecoveryPage,
  useAssignRecoverySponsor,
  useCreateInitialRecoveryAssignment,
  useCreateRecoveryHandover,
  useReconcileRecoveryAssignment,
  useRecoveryManagerCandidates,
  useRecoveryExistingRequestDetails,
  useRecoveryProjectSummary,
  useRecoverySponsorCandidates,
  type AssignmentRecoveryItem,
  type RecoveryProjectSummary,
  type RecoveryEntityType,
  type RecoveryFilter,
} from "./api/assignmentRecovery";

type SheetMode = "initial" | "handover" | "sponsor" | "history" | null;

const entityTypes: RecoveryEntityType[] = ["Project", "Program", "Portfolio"];
const recoveryFilters: RecoveryFilter[] = ["All", "NoManager", "NoSponsor", "MissingGovernanceAssignment", "PendingInitialAssignment", "Pending", "Rejected", "Assigned", "MissingParentHierarchy"];

export function EntityAssignmentRecoveryPage() {
  const { t, formatDate, formatNumber } = useI18n();
  const { user } = useAuth();
  const { effectiveReadOnly } = useManagement();
  const confirm = useConfirm();
  const permissions = user?.permissions ?? [];
  const canView = hasPermission(permissions, Permissions.AssignmentRecovery.View);
  const canInitiate = hasPermission(permissions, Permissions.AssignmentRecovery.Initiate)
    && hasPermission(permissions, Permissions.Handover.AdminInitiate)
    && !effectiveReadOnly;
  const canManage = hasPermission(permissions, Permissions.AssignmentRecovery.Manage) && !effectiveReadOnly;
  const canReadPage = Boolean(user && hasAnyPermission(permissions, [Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage]));
  const [entityType, setEntityType] = React.useState<RecoveryEntityType>("Project");
  const [filter, setFilter] = React.useState<RecoveryFilter>("All");
  const [search, setSearch] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [selected, setSelected] = React.useState<AssignmentRecoveryItem | null>(null);
  const [sheetMode, setSheetMode] = React.useState<SheetMode>(null);
  const [managerId, setManagerId] = React.useState("");
  const [sponsorUserId, setSponsorUserId] = React.useState("");
  const [effectiveDate, setEffectiveDate] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [reason, setReason] = React.useState("");
  const [notes, setNotes] = React.useState("");
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [viewingExistingRequest, setViewingExistingRequest] = React.useState<AssignmentRecoveryItem | null>(null);
  const result = useAssignmentRecoveryPage(entityType, page, 25, search.trim(), filter, canReadPage);
  const managerCandidates = useRecoveryManagerCandidates(entityType, selected?.publicId ?? null, sheetMode === "initial" || sheetMode === "handover");
  const isInitialProject = sheetMode === "initial" && selected?.entityType === "Project";
  const projectSummary = useRecoveryProjectSummary(selected?.publicId ?? null, isInitialProject && canInitiate);
  const matchedProjectSummary = projectSummary.data?.publicId === selected?.publicId ? projectSummary.data : undefined;
  const projectSummaryReady = !isInitialProject || Boolean(projectSummary.isSuccess && matchedProjectSummary);
  const sponsorCandidates = useRecoverySponsorCandidates(selected?.publicId ?? null, sheetMode === "sponsor");
  const history = useAssignmentRecoveryHistory(entityType, selected?.publicId ?? null, sheetMode === "history");
  const existingRequestDetails = useRecoveryExistingRequestDetails(
    viewingExistingRequest?.requestId ?? null,
    Boolean(viewingExistingRequest?.canViewExistingRequest),
  );
  const matchedExistingRequestDetails = existingRequestDetails.data?.id.toLowerCase()
    === viewingExistingRequest?.requestId?.toLowerCase()
    ? existingRequestDetails.data
    : undefined;
  const createInitial = useCreateInitialRecoveryAssignment();
  const createHandover = useCreateRecoveryHandover();
  const assignSponsor = useAssignRecoverySponsor();
  const reconcile = useReconcileRecoveryAssignment();
  const isSaving = createInitial.isPending || createHandover.isPending || assignSponsor.isPending;
  const summary = result.data?.summary;
  const rows = result.data?.items ?? [];

  function openSheet(mode: Exclude<SheetMode, null>, item: AssignmentRecoveryItem) {
    setSelected(item);
    setSheetMode(mode);
    setActionError(null);
    setManagerId(item.proposedManagerId ?? "");
    setSponsorUserId("");
    setEffectiveDate(new Date().toISOString().slice(0, 10));
    setReason("");
    setNotes("");
  }

  function closeSheet(open: boolean) {
    if (open) return;
    setSheetMode(null);
    setSelected(null);
    setActionError(null);
  }

  async function submitManager(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected || !managerId || !effectiveDate || !reason.trim() || isSaving || (isInitialProject && !projectSummaryReady)) return;
    setActionError(null);
    const input = { managerId, effectiveDate, reason: reason.trim(), notes: notes.trim() || undefined };
    try {
      if (sheetMode === "initial") await createInitial.mutateAsync({ type: entityType, publicId: selected.publicId, input });
      else await createHandover.mutateAsync({ type: entityType, publicId: selected.publicId, input });
      closeSheet(false);
    } catch (error: unknown) {
      if (error instanceof ApiError && [
        "HANDOVER_REQUEST_ALREADY_PENDING",
        "HANDOVER_REQUEST_ALREADY_SCHEDULED",
        "PROJECT_ALREADY_ASSIGNED",
        "PROJECT_ASSIGNMENT_RECONCILIATION_REQUIRED",
      ].includes(error.code ?? "")) {
        void result.refetch();
      }
      setActionError(error instanceof ApiError
        ? error.message
        : t("administration.governance.assignmentRecovery.saveFailed"));
    }
  }

  async function submitSponsor(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected || !sponsorUserId || assignSponsor.isPending) return;
    setActionError(null);
    try {
      await assignSponsor.mutateAsync({ publicId: selected.publicId, sponsorUserId });
      closeSheet(false);
    } catch {
      setActionError(t("administration.governance.assignmentRecovery.saveFailed"));
    }
  }

  async function reconcileItem(item: AssignmentRecoveryItem) {
    const accepted = await confirm({
      title: t("administration.governance.assignmentRecovery.reconcileTitle"),
      description: t("administration.governance.assignmentRecovery.reconcileConfirm", { name: item.name }),
      confirmLabel: t("administration.governance.assignmentRecovery.reconcile"),
    });
    if (!accepted) return;
    setActionError(null);
    try {
      await reconcile.mutateAsync({ type: item.entityType, publicId: item.publicId });
    } catch {
      setActionError(t("administration.governance.assignmentRecovery.reconcileFailed"));
    }
  }

  const assignmentLabel = (state: string) => t(`administration.governance.assignmentRecovery.managerState.${state}`);
  const requestLabel = (status?: string | null) => status ? t(`administration.governance.assignmentRecovery.requestState.${status}`) : t("administration.governance.assignmentRecovery.none");

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.governance.assignmentRecovery.title")}
        description={t("administration.governance.assignmentRecovery.description")}
        actions={<Button variant="outline" asChild><Link to="/administration/governance"><ArrowLeft className="me-2 h-4 w-4" />{t("administration.governance.assignmentRecovery.back")}</Link></Button>}
      />

      {effectiveReadOnly && <div role="status" className="rounded-md border border-warning/40 bg-warning/5 px-4 py-3 text-sm text-warning">{t("administration.governance.assignmentRecovery.readOnly")}</div>}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <SummaryCard label={t("administration.governance.assignmentRecovery.unassignedManagers")} value={summary?.unassignedManagers ?? 0} format={formatNumber} />
        <SummaryCard label={t("administration.governance.assignmentRecovery.projectsWithoutSponsor")} value={summary?.projectsWithoutSponsor ?? 0} format={formatNumber} />
        <SummaryCard label={t("administration.governance.assignmentRecovery.pendingAssignments")} value={summary?.pendingAssignments ?? 0} format={formatNumber} />
        <SummaryCard label={t("administration.governance.assignmentRecovery.inconsistentAssignments")} value={summary?.inconsistentAssignments ?? 0} format={formatNumber} />
      </div>

      <div className="flex flex-wrap gap-2" role="tablist" aria-label={t("administration.governance.assignmentRecovery.entityTypes")}>
        {entityTypes.map((type) => (
          <Button key={type} type="button" role="tab" aria-selected={entityType === type} variant={entityType === type ? "default" : "outline"} size="sm" onClick={() => { setEntityType(type); setPage(1); setFilter("All"); }}>
            {t(`administration.governance.assignmentRecovery.entity.${type}`)}
          </Button>
        ))}
      </div>

      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <CardTitle className="text-base">{t(`administration.governance.assignmentRecovery.entity.${entityType}`)}</CardTitle>
          <div className="grid w-full gap-3 sm:max-w-2xl sm:grid-cols-[minmax(15rem,1fr)_15rem]">
            <div className="space-y-1.5">
              <Label htmlFor="assignment-recovery-search">{t("administration.governance.assignmentRecovery.search")}</Label>
              <div className="relative"><Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input id="assignment-recovery-search" className="ps-9" value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder={t("administration.governance.assignmentRecovery.searchPlaceholder")} /></div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="assignment-recovery-filter">{t("administration.governance.assignmentRecovery.filter")}</Label>
              <select id="assignment-recovery-filter" className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={filter} onChange={(event) => { setFilter(event.target.value as RecoveryFilter); setPage(1); }}>
                {recoveryFilters.filter((option) => option !== "NoSponsor" || entityType === "Project").filter((option) => option !== "MissingParentHierarchy" || entityType !== "Portfolio").map((option) => <option key={option} value={option}>{t(`administration.governance.assignmentRecovery.filters.${option}`)}</option>)}
              </select>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {result.isLoading ? <div role="status" className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</div>
            : result.isError ? <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{t("administration.governance.assignmentRecovery.loadFailed")}</div>
            : rows.length === 0 ? <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">{t("administration.governance.assignmentRecovery.empty")}</div>
            : <Table>
              <TableHeader><TableRow>
                <TableHead>{t("administration.governance.assignmentRecovery.columns.entity")}</TableHead>
                <TableHead>{t("administration.governance.assignmentRecovery.columns.manager")}</TableHead>
                {entityType === "Project" && <TableHead>{t("administration.governance.assignmentRecovery.columns.sponsor")}</TableHead>}
                <TableHead>{t("administration.governance.assignmentRecovery.columns.hierarchy")}</TableHead>
                <TableHead>{t("administration.governance.assignmentRecovery.columns.assignment")}</TableHead>
                <TableHead>{t("administration.governance.assignmentRecovery.columns.updated")}</TableHead>
                <TableHead className="text-end">{t("administration.governance.assignmentRecovery.columns.actions")}</TableHead>
              </TableRow></TableHeader>
              <TableBody>{rows.map((item) => <TableRow key={item.publicId}>
                <TableCell className="min-w-48"><div className="font-medium">{item.name}</div><div className="text-xs text-muted-foreground">{item.code || item.publicId}</div></TableCell>
                <TableCell className="min-w-40"><div>{item.managerName || t("administration.governance.assignmentRecovery.unassigned")}</div><Badge status={item.managerAssignmentState} className="mt-1">{assignmentLabel(item.managerAssignmentState)}</Badge></TableCell>
                {entityType === "Project" && <TableCell className="min-w-36"><div>{item.sponsorName || t("administration.governance.assignmentRecovery.unassigned")}</div>{item.sponsorState !== "NotApplicable" && <Badge status={item.sponsorState} className="mt-1">{t(`administration.governance.assignmentRecovery.sponsorState.${item.sponsorState}`)}</Badge>}</TableCell>}
                <TableCell className="min-w-40 text-sm"><div>{item.parentProgramName || t("administration.governance.assignmentRecovery.noProgram")}</div><div className="text-xs text-muted-foreground">{item.parentPortfolioName || t("administration.governance.assignmentRecovery.noPortfolio")}</div>{item.missingParentHierarchy && <span className="mt-1 inline-block text-xs text-warning">{t("administration.governance.assignmentRecovery.missingParent")}</span>}</TableCell>
                <TableCell className="min-w-40"><Badge status={item.assignmentRequestStatus || ""}>{requestLabel(item.assignmentRequestStatus)}</Badge>{item.proposedManagerName && hasActiveAssignmentRequestStatus(item.assignmentRequestStatus) && <div className="mt-1 text-xs text-muted-foreground">{t("administration.governance.assignmentRecovery.proposed", { name: item.proposedManagerName })}</div>}{getRequestContext(item).map((context, index) => <div key={item.publicId + "-request-context-" + index} className="mt-1 text-xs text-muted-foreground">{context.name ? t(context.key, { name: context.name }) : t(context.key)}</div>)}</TableCell>
                <TableCell className="whitespace-nowrap text-sm text-muted-foreground">{formatDate(item.updatedAt ?? item.createdAt)}</TableCell>
                <TableCell><div className="flex min-w-56 flex-wrap justify-end gap-1.5">
                  {canInitiate && !item.managerId && item.managerAssignmentState === "Unassigned" && !hasActiveAssignmentRequestStatus(item.assignmentRequestStatus) && <Button size="sm" onClick={() => openSheet("initial", item)}><UserRoundCog className="me-1.5 h-3.5 w-3.5" />{t("administration.governance.assignmentRecovery.assignManager")}</Button>}
                  {canManage && item.managerId && item.managerAssignmentState === "Active" && !hasActiveAssignmentRequestStatus(item.assignmentRequestStatus) && <Button size="sm" variant="outline" onClick={() => openSheet("handover", item)}>{t("administration.governance.assignmentRecovery.handover")}</Button>}
                  {canManage && ["ManagerInactive", "MissingAssignment", "Inconsistent"].includes(item.managerAssignmentState) && <Button size="sm" variant="outline" onClick={() => void reconcileItem(item)} disabled={reconcile.isPending}><RefreshCw className="me-1.5 h-3.5 w-3.5" />{t("administration.governance.assignmentRecovery.reconcile")}</Button>}
                  {canManage && entityType === "Project" && item.sponsorState === "Unassigned" && <Button size="sm" variant="secondary" onClick={() => openSheet("sponsor", item)}>{t("administration.governance.assignmentRecovery.assignSponsor")}</Button>}
                  {item.entityType === "Project" && item.canViewExistingRequest && item.requestId && <Button size="sm" variant="outline" onClick={() => setViewingExistingRequest(item)}>{t("administration.governance.assignmentRecovery.viewExistingRequest")}</Button>}
                  {canView && <Button size="sm" variant="ghost" onClick={() => openSheet("history", item)}><History className="me-1.5 h-3.5 w-3.5" />{t("administration.governance.assignmentRecovery.history")}</Button>}
                </div></TableCell>
              </TableRow>)}</TableBody>
            </Table>}

          {result.data && result.data.totalPages > 1 && <div className="mt-4 flex items-center justify-between border-t pt-4 text-sm text-muted-foreground"><span>{t("administration.governance.assignmentRecovery.pageOf", { page: result.data.page, pages: result.data.totalPages, count: formatNumber(result.data.totalCount) })}</span><div className="flex gap-2"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>{t("common.previous")}</Button><Button size="sm" variant="outline" disabled={page >= result.data.totalPages} onClick={() => setPage((current) => current + 1)}>{t("common.next")}</Button></div></div>}
        </CardContent>
      </Card>

      <div className="flex items-center justify-between gap-4 rounded-md border bg-muted/20 px-4 py-3 text-sm">
        <span className="text-muted-foreground">{t("administration.governance.assignmentRecovery.approvalHint")}</span>
        <Button size="sm" variant="outline" asChild><Link to="/my-work/approvals">{t("administration.governance.assignmentRecovery.openApprovals")}<ArrowUpRight className="ms-2 h-4 w-4" /></Link></Button>
      </div>

      <SideSheet open={sheetMode !== null} onOpenChange={closeSheet} size="lg" title={sheetTitle(sheetMode, t)} description={selected?.name} closeLabel={t("common.close")}>
        {(sheetMode === "initial" || sheetMode === "handover") && <form onSubmit={(event) => void submitManager(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-5">
            {sheetMode === "initial" && <p className="rounded-md border border-primary/20 bg-primary/5 p-3 text-sm">{t("administration.governance.assignmentRecovery.initialHint")}</p>}
            {isInitialProject && <ProjectSummaryPanel
              summary={matchedProjectSummary}
              isLoading={projectSummary.isLoading}
              isError={projectSummary.isError || Boolean(projectSummary.data && !matchedProjectSummary)}
              isEmpty={projectSummary.isSuccess && !matchedProjectSummary}
              onRetry={() => void projectSummary.refetch()}
              t={t}
              formatDate={formatDate}
            />}
            <div className="space-y-1.5"><Label htmlFor="recovery-manager">{t("administration.governance.assignmentRecovery.manager")}</Label><select id="recovery-manager" required className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={managerId} onChange={(event) => setManagerId(event.target.value)} disabled={managerCandidates.isLoading || managerCandidates.isError || !projectSummaryReady}><option value="">{managerCandidates.isLoading ? t("common.loading") : t("administration.governance.assignmentRecovery.selectManager")}</option>{(managerCandidates.data ?? []).map((candidate) => <option key={candidate.userId} value={candidate.userId}>{candidate.fullName} — {candidate.email}</option>)}</select>{managerCandidates.isError && <p role="alert" className="text-sm text-destructive">{t("administration.governance.assignmentRecovery.candidatesFailed")}</p>}</div>
            <div className="space-y-1.5"><Label htmlFor="recovery-effective-date">{t("administration.governance.assignmentRecovery.effectiveDate")}</Label><Input id="recovery-effective-date" type="date" required value={effectiveDate} onChange={(event) => setEffectiveDate(event.target.value)} /></div>
            <div className="space-y-1.5"><Label htmlFor="recovery-reason">{t("administration.governance.assignmentRecovery.reason")}</Label><textarea id="recovery-reason" required maxLength={500} value={reason} onChange={(event) => setReason(event.target.value)} rows={4} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" /></div>
            <div className="space-y-1.5"><Label htmlFor="recovery-notes">{t("administration.governance.assignmentRecovery.notes")}</Label><textarea id="recovery-notes" maxLength={2000} value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm" /></div>
            {actionError && <p role="alert" className="text-sm text-destructive">{actionError}</p>}
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={isSaving}>{t("common.cancel")}</Button><Button type="submit" disabled={isSaving || !managerId || !reason.trim() || managerCandidates.isLoading || !projectSummaryReady}>{isSaving ? t("common.saving") : t("administration.governance.assignmentRecovery.submitForAcceptance")}</Button></SideSheetFooter>
        </form>}
        {sheetMode === "sponsor" && <form onSubmit={(event) => void submitSponsor(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-5"><p className="rounded-md border border-primary/20 bg-primary/5 p-3 text-sm">{t("administration.governance.assignmentRecovery.sponsorHint")}</p><div className="space-y-1.5"><Label htmlFor="recovery-sponsor">{t("administration.governance.assignmentRecovery.sponsor")}</Label><select id="recovery-sponsor" required className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={sponsorUserId} onChange={(event) => setSponsorUserId(event.target.value)} disabled={sponsorCandidates.isLoading || sponsorCandidates.isError}><option value="">{sponsorCandidates.isLoading ? t("common.loading") : t("administration.governance.assignmentRecovery.selectSponsor")}</option>{(sponsorCandidates.data ?? []).map((candidate) => <option key={candidate.userId} value={candidate.userId}>{candidate.fullName} — {candidate.email}</option>)}</select>{sponsorCandidates.isError && <p role="alert" className="text-sm text-destructive">{t("administration.governance.assignmentRecovery.candidatesFailed")}</p>}</div>{actionError && <p role="alert" className="text-sm text-destructive">{actionError}</p>}</SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeSheet(false)} disabled={assignSponsor.isPending}>{t("common.cancel")}</Button><Button type="submit" disabled={assignSponsor.isPending || !sponsorUserId}>{assignSponsor.isPending ? t("common.saving") : t("administration.governance.assignmentRecovery.assignSponsor")}</Button></SideSheetFooter>
        </form>}
        {sheetMode === "history" && <SideSheetBody className="space-y-3">
          {history.isLoading ? <p role="status" className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : history.isError ? <p role="alert" className="text-sm text-destructive">{t("administration.governance.assignmentRecovery.historyFailed")}</p> : (history.data?.items.length ?? 0) === 0 ? <p className="py-8 text-center text-sm text-muted-foreground">{t("administration.governance.assignmentRecovery.noHistory")}</p> : history.data?.items.map((entry, index) => <div key={`${entry.recordType}-${entry.status}-${entry.effectiveDate}-${index}`} className="rounded-lg border p-4"><div className="flex flex-wrap items-center justify-between gap-2"><Badge status={entry.status}>{t(`administration.governance.assignmentRecovery.requestState.${entry.status}`)}</Badge><span className="text-xs text-muted-foreground">{entry.requestedAt ? formatDate(entry.requestedAt) : formatDate(entry.effectiveDate)}</span></div><p className="mt-2 text-sm font-medium">{entry.managerName}{entry.previousManagerName ? ` ← ${entry.previousManagerName}` : ""}</p>{entry.initiatedByName && <p className="mt-1 text-xs text-muted-foreground">{t("administration.governance.assignmentRecovery.initiatedBy", { name: entry.initiatedByName })}</p>}{entry.reason && <p className="mt-2 text-sm">{entry.reason}</p>}{entry.rejectionReason && <p className="mt-2 text-sm text-destructive">{t("administration.governance.assignmentRecovery.rejectionReason", { reason: entry.rejectionReason })}</p>}</div>)}
        </SideSheetBody>}
      </SideSheet>
      <Dialog
        open={Boolean(viewingExistingRequest)}
        onOpenChange={(open) => { if (!open) setViewingExistingRequest(null); }}
        ariaLabelledBy="assignment-recovery-existing-request-title"
        contentClassName="max-h-[85vh] max-w-2xl overflow-y-auto"
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle id="assignment-recovery-existing-request-title">{t("administration.governance.assignmentRecovery.existingRequest.title")}</DialogTitle>
            <DialogDescription>{t("administration.governance.assignmentRecovery.existingRequest.description")}</DialogDescription>
          </DialogHeader>
          {existingRequestDetails.isLoading ? (
            <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("administration.governance.assignmentRecovery.existingRequest.loading")}</p>
          ) : existingRequestDetails.isError ? (
            <div role="alert" className="space-y-3 rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
              <p>{existingRequestDetails.error instanceof ApiError
                ? existingRequestDetails.error.message
                : t("administration.governance.assignmentRecovery.existingRequest.loadFailed")}</p>
              <Button size="sm" variant="outline" onClick={() => void existingRequestDetails.refetch()}>{t("common.retry")}</Button>
            </div>
          ) : matchedExistingRequestDetails && viewingExistingRequest ? (
            <div className="space-y-4 rounded-lg border bg-card p-4">
              <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-3">
                <div className="min-w-0">
                  <p className="font-semibold">{matchedExistingRequestDetails.projectName}</p>
                  <p className="text-xs text-muted-foreground">{matchedExistingRequestDetails.projectCode}</p>
                </div>
                <Badge status={matchedExistingRequestDetails.status}>{requestLabel(matchedExistingRequestDetails.status)}</Badge>
              </div>
              <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                {getRequestContext(viewingExistingRequest, matchedExistingRequestDetails.status, matchedExistingRequestDetails.newProjectManagerName).map((context, index) => (
                  <span key={"existing-request-context-" + index} className="rounded-full border px-2 py-1">
                    {context.name ? t(context.key, { name: context.name }) : t(context.key)}
                  </span>
                ))}
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.currentManager")} value={matchedExistingRequestDetails.currentProjectManagerName || t("administration.governance.assignmentRecovery.unassigned")} />
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.proposedManager")} value={matchedExistingRequestDetails.newProjectManagerName} />
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.requestedBy")} value={matchedExistingRequestDetails.requestedByName} />
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.requestedAt")} value={formatDate(matchedExistingRequestDetails.requestedAt)} />
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.effectiveDate")} value={formatDate(matchedExistingRequestDetails.effectiveHandoverDate)} />
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.reason")} value={matchedExistingRequestDetails.reason} wide />
                <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.notes")} value={matchedExistingRequestDetails.notes} wide />
                {matchedExistingRequestDetails.respondedByName ? <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.respondedBy")} value={matchedExistingRequestDetails.respondedByName} /> : null}
                {matchedExistingRequestDetails.respondedAt ? <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.respondedAt")} value={formatDate(matchedExistingRequestDetails.respondedAt)} /> : null}
                {matchedExistingRequestDetails.rejectionReason ? <RequestDetailField label={t("administration.governance.assignmentRecovery.existingRequest.rejectionReason")} value={matchedExistingRequestDetails.rejectionReason} wide /> : null}
              </div>
            </div>
          ) : (
            <p role="status" className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">{t("administration.governance.assignmentRecovery.existingRequest.unavailable")}</p>
          )}
          <DialogFooter>
            {viewingExistingRequest?.canReviewExistingRequest && viewingExistingRequest.requestId ? (
              <Button asChild>
                <Link to={getApprovalReviewPath("project-handover", viewingExistingRequest.requestId)}>{t("administration.governance.assignmentRecovery.existingRequest.openReview")}</Link>
              </Button>
            ) : null}
            <Button variant="outline" onClick={() => setViewingExistingRequest(null)}>{t("common.close")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function SummaryCard({ label, value, format }: { label: string; value: number; format: (value: number) => string }) {
  return <Card><CardContent className="p-4"><div className="text-xs font-medium text-muted-foreground">{label}</div><div className="mt-1 text-2xl font-semibold tabular-nums">{format(value)}</div></CardContent></Card>;
}

function ProjectSummaryPanel({
  summary,
  isLoading,
  isError,
  isEmpty,
  onRetry,
  t,
  formatDate,
}: {
  summary?: RecoveryProjectSummary;
  isLoading: boolean;
  isError: boolean;
  isEmpty: boolean;
  onRetry: () => void;
  t: (key: string) => string;
  formatDate: (date: Date | string) => string;
}) {
  const copy = "administration.governance.assignmentRecovery.projectSummary";
  const description = summary?.description?.trim();
  const sponsor = summary?.sponsorName?.trim();

  return (
    <Card className="border-primary/20 bg-muted/20" aria-labelledby="recovery-project-summary-title">
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div className="flex min-w-0 items-start gap-3">
          <span className="mt-0.5 rounded-md border bg-background p-2 text-primary"><ClipboardList aria-hidden="true" className="h-4 w-4" /></span>
          <div className="min-w-0">
            <CardTitle id="recovery-project-summary-title" className="text-base">{t(`${copy}.title`)}</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{t(`${copy}.readOnly`)}</p>
          </div>
        </div>
        <span className="shrink-0 rounded-full border px-2 py-1 text-[11px] font-medium text-muted-foreground">{t(`${copy}.readOnly`)}</span>
      </CardHeader>
      <CardContent>
        {isLoading ? <p role="status" className="py-5 text-center text-sm text-muted-foreground">{t(`${copy}.loading`)}</p>
          : isError ? <div className="flex flex-col items-start justify-between gap-3 rounded-md border border-destructive/30 bg-destructive/5 p-3 sm:flex-row sm:items-center"><p role="alert" className="text-sm text-destructive">{t(`${copy}.loadFailed`)}</p><Button type="button" size="sm" variant="outline" onClick={onRetry}>{t(`${copy}.retry`)}</Button></div>
            : !summary || isEmpty ? <p role="status" className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">{t(`${copy}.empty`)}</p>
              : <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <SummaryField label={t(`${copy}.name`)} value={summary.name?.trim() || t(`${copy}.notProvided`)} />
                  <SummaryField label={t(`${copy}.code`)} value={summary.code?.trim() || t(`${copy}.notProvided`)} />
                  <SummaryField label={t(`${copy}.description`)} value={description || t(`${copy}.notProvided`)} wide />
                  <SummaryField label={t(`${copy}.startDate`)} value={summary.startDate ? formatDate(summary.startDate) : t(`${copy}.notProvided`)} />
                  <SummaryField label={t(`${copy}.endDate`)} value={summary.endDate ? formatDate(summary.endDate) : t(`${copy}.notProvided`)} />
                  <SummaryField label={t(`${copy}.sponsor`)} value={sponsor || t(`${copy}.notAssigned`)} wide />
                </div>
              </div>}
      </CardContent>
    </Card>
  );
}

function SummaryField({ label, value, wide = false }: { label: string; value: string; wide?: boolean }) {
  return <div className={wide ? "min-w-0 sm:col-span-2" : "min-w-0"}>
    <div className="text-xs font-medium text-muted-foreground">{label}</div>
    <div className="mt-1 break-words text-sm">{value}</div>
  </div>;
}

function getRequestContext(
  item: AssignmentRecoveryItem,
  status = item.assignmentRequestStatus,
  proposedManagerName = item.proposedManagerName,
): Array<{ key: string; name?: string }> {
  const contexts: Array<{ key: string; name?: string }> = [];
  if (item.isInitiatedByCurrentUser) {
    contexts.push({ key: "administration.governance.assignmentRecovery.requestRole.initiatedByMe" });
  }
  if (item.isAssignedToCurrentUser) {
    contexts.push({ key: "administration.governance.assignmentRecovery.requestRole.assignedToMe" });
  }
  if (status === "ApprovedScheduled") {
    contexts.push({ key: "administration.governance.assignmentRecovery.requestRole.approvedScheduled" });
  }
  if (status === "Pending" && proposedManagerName && !item.isAssignedToCurrentUser) {
    contexts.push({
      key: "administration.governance.assignmentRecovery.requestRole.awaitingProposedManager",
      name: proposedManagerName,
    });
  }
  if (status && ["Accepted", "Rejected", "Cancelled", "Completed"].includes(status)) {
    contexts.push({ key: "administration.governance.assignmentRecovery.requestRole.completed" });
  }
  return contexts;
}

function hasActiveAssignmentRequestStatus(status?: string | null): boolean {
  return status === "Pending" || status === "ApprovedScheduled";
}

function RequestDetailField({ label, value, wide = false }: { label: string; value?: string | null; wide?: boolean }) {
  if (!value) return null;
  return <div className={wide ? "min-w-0 sm:col-span-2" : "min-w-0"}>
    <div className="text-xs font-medium text-muted-foreground">{label}</div>
    <div className="mt-1 whitespace-pre-wrap break-words text-sm">{value}</div>
  </div>;
}

function sheetTitle(mode: SheetMode, t: (key: string) => string) {
  if (mode === "initial") return t("administration.governance.assignmentRecovery.initialTitle");
  if (mode === "handover") return t("administration.governance.assignmentRecovery.handoverTitle");
  if (mode === "sponsor") return t("administration.governance.assignmentRecovery.sponsorTitle");
  if (mode === "history") return t("administration.governance.assignmentRecovery.historyTitle");
  return "";
}
