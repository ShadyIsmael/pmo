import * as React from "react";
import { ArrowRightLeft, History, Loader2, Plus } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useI18n } from "@i18n/messages";
import { getStatusTextClass } from "@lib/status-presentation";
import {
  useAcceptManagerHandover,
  useCancelManagerHandover,
  useCreateManagerHandover,
  useEligibleManagerHandoverManagers,
  useManagerAssignmentHistory,
  useManagerHandoverCapabilities,
  useManagerHandoverRequests,
  useManagerHandoverSnapshots,
  useRejectManagerHandover,
  useSelfPendingManagerHandover,
  type ManagerHandoverRequestDto,
  type ManagerHandoverInitiationType,
  type ManagerHandoverScope,
} from "./api/managerHandovers";

interface ManagerHandoverPanelProps {
  scope: ManagerHandoverScope;
  entityId: string;
  entityName: string;
  currentManagerId?: string;
  currentManagerName?: string;
  currentManagerLoading?: boolean;
  currentManagerError?: boolean;
  onRetryCurrentManager?: () => void;
  cacheScope: string;
  canView: boolean;
  canManage: boolean;
  canAdminInitiate?: boolean;
  canApprove: boolean;
  canMutate: boolean;
}

function localDateValue() {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

function errorStatus(error: unknown): number | undefined {
  return (error as { status?: number } | null)?.status;
}

// Capability model mirrors the backend (ManagerHandoverService + ManagerHandoverController):
// - Initiate: Handover.Manage + caller is the current manager + no pending request.
// - Cancel: Handover.Manage + caller is the requester + request is Pending.
// - Accept/Reject: Handover.Approve + caller is the selected new manager + request is Pending.
// - Accept finalizes the manager change immediately (snapshot + assignment + audit + notifications).
// - ReadOnlyViewAs blocks every mutation (enforced by middleware; mirrored here via canMutate).
export function ManagerHandoverPanel({ scope, entityId, entityName, currentManagerName, currentManagerLoading, currentManagerError, onRetryCurrentManager, cacheScope, canView, canManage, canAdminInitiate, canApprove, canMutate }: ManagerHandoverPanelProps) {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { refreshSnapshot } = useManagement();
  const isProgram = scope === "programs";
  const entityLabel = t(isProgram ? "managerHandover.program" : "managerHandover.portfolio");
  const capabilities = useManagerHandoverCapabilities(scope, entityId, canView || canManage || canAdminInitiate || canApprove, cacheScope);
  const canReadRequests = Boolean(capabilities.data?.canViewRequests);
  const requests = useManagerHandoverRequests(scope, entityId, canView || canManage || canAdminInitiate, cacheScope);
  const requestsForbidden = errorStatus(requests.error) === 403;
  // Approve-only users have no Handover.View, so the full request list is forbidden for them.
  // The self-scoped endpoint returns ONLY the pending request addressed to the current user.
  const selfPendingEnabled = canApprove && (!canView || requestsForbidden);
  const selfPending = useSelfPendingManagerHandover(scope, entityId, selfPendingEnabled, cacheScope);
  const history = useManagerAssignmentHistory(scope, entityId, canView || canManage || canAdminInitiate, cacheScope);
  const snapshots = useManagerHandoverSnapshots(scope, entityId, canView || canManage || canAdminInitiate, cacheScope);
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [initiationType, setInitiationType] = React.useState<ManagerHandoverInitiationType>("ManagerInitiated");
  const [managerId, setManagerId] = React.useState("");
  const [error, setError] = React.useState("");
  const [actionError, setActionError] = React.useState<{ requestId: string; message: string } | null>(null);
  const [rejectingId, setRejectingId] = React.useState<string | null>(null);
  const [rejectionReason, setRejectionReason] = React.useState("");
  const [rejectError, setRejectError] = React.useState<string | null>(null);
  const [cancellingId, setCancellingId] = React.useState<string | null>(null);
  const [effectiveDate, setEffectiveDate] = React.useState(localDateValue);
  const selectedModeAllowed = initiationType === "ManagerInitiated"
    ? capabilities.data?.canInitiate
    : initiationType === "AdminInitiated" ? capabilities.data?.canAdminInitiate : capabilities.data?.canAssignInitial;
  const eligibleManagers = useEligibleManagerHandoverManagers(scope, entityId, sheetOpen && canMutate && Boolean(selectedModeAllowed), cacheScope);
  const create = useCreateManagerHandover(scope, entityId, cacheScope);
  const cancel = useCancelManagerHandover(scope, entityId, cacheScope);
  const accept = useAcceptManagerHandover(scope, entityId, cacheScope);
  const reject = useRejectManagerHandover(scope, entityId, cacheScope);
  const items = requests.data ?? [];
  const pendingRequest = items.find((item) => item.status === "Pending");
  const selfItems = selfPending.data ?? [];
  const selfPendingRequest = selfItems.find((item) => item.status === "Pending");
  const canInitiate = Boolean(canMutate && capabilities.data?.canInitiate && !pendingRequest);
  const canAdminRequest = Boolean(canMutate && capabilities.data?.canAdminInitiate && !pendingRequest);
  const canInitialAssign = Boolean(canMutate && capabilities.data?.canAssignInitial && !pendingRequest);
  const managers = (eligibleManagers.data ?? []).map((manager) => ({
    value: manager.id,
    label: `${manager.fullName} · ${manager.managerTypes.map((type) => t(type === "PortfolioManager" ? "projects.handoverManagerTypePortfolio" : type === "ProgramManager" ? "projects.handoverManagerTypeProgram" : "projects.handoverManagerTypeProject")).join(" / ")} · ${manager.email}`,
    keywords: [manager.jobTitle ?? ""],
  }));

  React.useEffect(() => {
    if (!sheetOpen) return;
    setManagerId("");
    setEffectiveDate(localDateValue());
    setError("");
  }, [sheetOpen]);

  function openRequest(mode: ManagerHandoverInitiationType) {
    setInitiationType(mode);
    setSheetOpen(true);
  }

  function canCancelRequest(request: ManagerHandoverRequestDto): boolean {
    return Boolean(capabilities.data?.canCancelOwnPending && canMutate && request.status === "Pending" && user?.id && request.requestedBy === user.id);
  }

  function canDecideRequest(request: ManagerHandoverRequestDto): boolean {
    return Boolean(capabilities.data?.canApproveIncoming && canApprove && canMutate && request.status === "Pending" && user?.id && request.newManagerId === user.id);
  }

  function initiateHint(): string | null {
    if (!canMutate) return t("managerHandover.readOnlyHandover");
    if (!canManage && !canAdminInitiate) return t("managerHandover.managePermissionHint");
    if (pendingRequest || capabilities.data?.hasPendingRequest) return t("managerHandover.pendingExistsHint");
    if (currentManagerLoading || currentManagerError) return null;
    if (!capabilities.data?.currentManagerId) return t("managerHandover.noCurrentManager");
    if (!capabilities.data?.canInitiate && !capabilities.data?.canAdminInitiate) return t("managerHandover.notCurrentManagerHint", { manager: capabilities.data?.currentManagerName || currentManagerName || "—" });
    return null;
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const reason = String(data.get("reason") ?? "").trim();
    if (!managerId || !effectiveDate || !reason) {
      setError(t("managerHandover.requiredFields"));
      return;
    }
    setError("");
    try {
      await create.mutateAsync({
        initiationType,
        newManagerId: managerId,
        effectiveHandoverDate: effectiveDate,
        reason,
        notes: String(data.get("notes") ?? "").trim() || undefined,
      });
      setSheetOpen(false);
      await refreshSnapshot();
    } catch (requestError: unknown) {
      setError(errorStatus(requestError) === 409 ? t("managerHandover.decisionConflict") : t("managerHandover.saveFailed"));
    }
  }

  async function confirmCancel() {
    if (!cancellingId) return;
    setError("");
    setActionError(null);
    try {
      await cancel.mutateAsync(cancellingId);
      setCancellingId(null);
      await refreshSnapshot();
    } catch (requestError: unknown) {
      setError(errorStatus(requestError) === 409 ? t("managerHandover.decisionConflict") : t("managerHandover.cancelFailed"));
      setCancellingId(null);
    }
  }

  async function acceptRequest(requestId: string) {
    setActionError(null);
    try {
      await accept.mutateAsync(requestId);
      await refreshSnapshot();
    } catch (requestError: unknown) {
      setActionError({ requestId, message: errorStatus(requestError) === 409 ? t("managerHandover.decisionConflict") : t("managerHandover.acceptFailed") });
    }
  }

  async function confirmReject() {
    if (!rejectingId || !rejectionReason.trim()) {
      setRejectError(t("managerHandover.rejectionRequired"));
      return;
    }
    setRejectError(null);
    setActionError(null);
    try {
      await reject.mutateAsync({ requestId: rejectingId, rejectionReason: rejectionReason.trim() });
      setRejectingId(null);
      setRejectionReason("");
      await refreshSnapshot();
    } catch (requestError: unknown) {
      const message = errorStatus(requestError) === 409 ? t("managerHandover.decisionConflict") : t("managerHandover.rejectFailed");
      if (errorStatus(requestError) === 409) {
        setActionError({ requestId: rejectingId, message });
        setRejectingId(null);
        setRejectionReason("");
      } else {
        setRejectError(message);
      }
    }
  }

  const isLoading = requests.isLoading || history.isLoading || snapshots.isLoading || capabilities.isLoading;
  const hasError = requests.isError || history.isError || snapshots.isError || selfPending.isError || capabilities.isError || currentManagerError;
  const hint = initiateHint();
  const showApproveOnlySection = (!canReadRequests || requestsForbidden) && canApprove;

  // Self-scoped pending card for Handover.Approve holders without Handover.View.
  // Data comes from the self-scoped backend endpoint, which returns ONLY the pending
  // request addressed to the current user. `withHistoryNotice` also renders the scoped
  // permission notice because full history/details stay behind Handover.View.
  function renderSelfPendingCard(withHistoryNotice: boolean) {
    const decidable = selfPendingRequest != null && canDecideRequest(selfPendingRequest);
    return <Card>
      <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div><CardTitle className="flex items-center gap-2 text-base"><ArrowRightLeft className="h-4 w-4" />{t("managerHandover.handover")}</CardTitle><p className="text-sm text-muted-foreground">{t("managerHandover.pendingForYou")}</p></div>
      </CardHeader>
      <CardContent className="space-y-3 p-6 pt-0">
        {selfPending.isLoading ? <div className="flex justify-center py-10"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
          : selfPending.isError ? null
            : selfPendingRequest == null ? <p role="status" className="text-sm text-muted-foreground">{t("managerHandover.noSelfPending")}</p>
              : <div className="flex flex-col gap-3 rounded-md border border-primary/30 bg-primary/5 px-4 py-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0 space-y-1">
                  <p className="text-sm font-medium">{selfPendingRequest.currentManagerName || t("managerHandover.notAssigned")} → {selfPendingRequest.newManagerName}</p>
                  <p className="text-xs text-muted-foreground">{t("managerHandover.currentManager")}: {selfPendingRequest.currentManagerName || t("managerHandover.notAssigned")} · {t("managerHandover.newManager")}: {selfPendingRequest.newManagerName}</p>
                  {selfPendingRequest.initiationType !== "ManagerInitiated" ? <p className="text-xs text-muted-foreground">{t(selfPendingRequest.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}
                  <p className="text-xs text-muted-foreground">{t("managerHandover.effectiveDate")}: {formatDate(selfPendingRequest.effectiveHandoverDate)} · {t("managerHandover.requestedBy")}: {selfPendingRequest.requestedByName} · {formatDate(selfPendingRequest.requestedAt)}</p>
                  <p className="text-sm">{selfPendingRequest.reason}</p>
                  {selfPendingRequest.notes ? <p className="text-xs text-muted-foreground">{t("managerHandover.notes")}: {selfPendingRequest.notes}</p> : null}
                  {actionError?.requestId === selfPendingRequest.id ? <p role="alert" className="text-xs text-destructive">{actionError.message}</p> : null}
                  {!decidable ? <p role="status" className="text-xs text-muted-foreground">{!canMutate ? t("managerHandover.readOnlyHandover") : t("managerHandover.noActionsOnPending")}</p> : null}
                </div>
                <div className="flex shrink-0 flex-wrap items-center gap-2">
                  <span className={`text-xs font-semibold ${getStatusTextClass(selfPendingRequest.status)}`}>{t(`projects.handoverStatus.${selfPendingRequest.status}`)}</span>
                  {decidable ? <>
                    <Button size="sm" onClick={() => void acceptRequest(selfPendingRequest.id)} disabled={accept.isPending || reject.isPending}>{accept.isPending ? t("managerHandover.saving") : t("managerHandover.accept")}</Button>
                    <Button size="sm" variant="outline" onClick={() => { setRejectError(null); setRejectionReason(""); setRejectingId(selfPendingRequest.id); }} disabled={accept.isPending || reject.isPending}>{t("managerHandover.reject")}</Button>
                  </> : null}
                </div>
              </div>}
        {withHistoryNotice ? <p role="note" className="text-xs text-muted-foreground">{t("managerHandover.historyRequiresView")}</p> : null}
      </CardContent>
    </Card>;
  }

  return <div className="space-y-4">
    {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p> : null}
    {hasError ? <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive"><span>{requestsForbidden ? t("managerHandover.viewPermissionRequired") : t("managerHandover.loadFailed")}</span><Button size="sm" variant="outline" onClick={() => { void requests.refetch(); void history.refetch(); void snapshots.refetch(); void selfPending.refetch(); if (currentManagerError) onRetryCurrentManager?.(); }}>{t("common.retry")}</Button></div> : null}
    {canReadRequests ? <>
      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div><CardTitle className="flex items-center gap-2 text-base"><ArrowRightLeft className="h-4 w-4" />{t("managerHandover.handover")}</CardTitle><p className="text-sm text-muted-foreground">{t("managerHandover.handoverDescription", { entity: entityLabel })}</p></div>
          <div className="flex flex-wrap gap-2">
            {canInitiate ? <Button size="sm" onClick={() => openRequest("ManagerInitiated")} disabled={requests.isLoading || requests.isError}><Plus className="me-1.5 h-4 w-4" />{t("managerHandover.request")}</Button> : null}
            {canAdminRequest ? <Button size="sm" variant="outline" onClick={() => openRequest("AdminInitiated")} disabled={requests.isLoading || requests.isError}><Plus className="me-1.5 h-4 w-4" />{t("managerHandover.adminRequest")}</Button> : null}
            {canInitialAssign ? <Button size="sm" variant="outline" onClick={() => openRequest("InitialAssignment")} disabled={requests.isLoading || requests.isError}><Plus className="me-1.5 h-4 w-4" />{t("managerHandover.initialAssignment")}</Button> : null}
          </div>
        </CardHeader>
        <CardContent className="space-y-3 p-6 pt-0">
          {requests.isLoading ? <div className="flex justify-center py-10"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
            : requests.isError ? null
              : items.length === 0 ? <div className="space-y-2">
                <p className="text-sm text-muted-foreground">{t("managerHandover.noRequests")}</p>
                {currentManagerLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : null}
                {hint ? <p role="status" className="text-sm text-muted-foreground">{hint}</p> : null}
                {canInitiate || !canMutate || currentManagerLoading || currentManagerError ? null : <p className="text-xs text-muted-foreground">{t("managerHandover.viewApprovalsHint")}</p>}
              </div>
              : <div className="divide-y rounded-md border">{items.map((request) => {
                const cancellable = canCancelRequest(request);
                const decidable = canDecideRequest(request);
                const isPending = request.status === "Pending";
                return <div key={request.id} className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0 space-y-1">
                    <p className="text-sm font-medium">{request.currentManagerName || t("managerHandover.notAssigned")} → {request.newManagerName}</p>
                    {request.initiationType !== "ManagerInitiated" ? <p className="text-xs text-muted-foreground">{t(request.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}
                    <p className="text-xs text-muted-foreground">{t("managerHandover.effectiveDate")}: {formatDate(request.effectiveHandoverDate)} · {t("managerHandover.requestedBy")}: {request.requestedByName} · {formatDate(request.requestedAt)}</p>
                    <p className="text-sm">{request.reason}</p>
                    {request.notes ? <p className="text-xs text-muted-foreground">{t("managerHandover.notes")}: {request.notes}</p> : null}
                    {request.respondedByName || request.respondedAt ? <p className="text-xs text-muted-foreground">{t("managerHandover.respondedBy")}: {request.respondedByName ?? "—"} · {request.respondedAt ? formatDate(request.respondedAt) : "—"}</p> : null}
                    {request.rejectionReason ? <p className="text-xs text-destructive">{t("projects.rejectionReason")}: {request.rejectionReason}</p> : null}
                    {actionError?.requestId === request.id ? <p role="alert" className="text-xs text-destructive">{actionError.message}</p> : null}
                    {isPending && !cancellable && !decidable ? <p role="status" className="text-xs text-muted-foreground">{!canMutate ? t("managerHandover.readOnlyHandover") : t("managerHandover.noActionsOnPending")}</p> : null}
                  </div>
                  <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <span className={`text-xs font-semibold ${getStatusTextClass(request.status)}`}>{t(`projects.handoverStatus.${request.status}`)}</span>
                    {decidable ? <>
                      <Button size="sm" onClick={() => void acceptRequest(request.id)} disabled={accept.isPending || reject.isPending}>{accept.isPending ? t("managerHandover.saving") : t("managerHandover.accept")}</Button>
                      <Button size="sm" variant="outline" onClick={() => { setRejectError(null); setRejectionReason(""); setRejectingId(request.id); }} disabled={accept.isPending || reject.isPending}>{t("managerHandover.reject")}</Button>
                    </> : null}
                    {cancellable ? <Button size="sm" variant="outline" onClick={() => setCancellingId(request.id)} disabled={cancel.isPending}>{cancel.isPending ? t("managerHandover.saving") : t("managerHandover.cancel")}</Button> : null}
                  </div>
                </div>;
              })}</div>}
          {!requests.isLoading && !requests.isError && pendingRequest == null && !canInitiate && items.length > 0 && hint ? <p role="status" className="text-xs text-muted-foreground">{hint}</p> : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="flex items-center gap-2 text-base"><History className="h-4 w-4" />{t("managerHandover.managerHistory")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {history.isLoading ? <div className="flex justify-center py-8"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
            : history.isError ? <p role="alert" className="p-6 text-sm text-destructive">{t("managerHandover.loadFailed")}</p>
              : (history.data?.length ?? 0) === 0 ? <p className="p-6 text-sm text-muted-foreground">{t("managerHandover.noHistory")}</p>
                : <div className="divide-y">{history.data?.map((assignment) => <div key={assignment.id} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                  <div><p className="text-sm font-medium">{assignment.managerName}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(assignment.startDate)} – {assignment.endDate ? formatDate(assignment.endDate) : t("managerHandover.current")}</p>{assignment.handoverReason ? <p className="mt-1 text-xs text-muted-foreground">{assignment.handoverReason}</p> : null}</div>
                  {assignment.isCurrent ? <span className={`w-fit text-xs font-semibold ${getStatusTextClass("Active")}`}>{t("managerHandover.current")}</span> : null}
                </div>)}</div>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">{t("managerHandover.snapshots")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {snapshots.isLoading ? <div className="flex justify-center py-8"><Loader2 className="h-5 w-5 animate-spin text-muted-foreground" /></div>
            : snapshots.isError ? <p role="alert" className="p-6 text-sm text-destructive">{t("managerHandover.loadFailed")}</p>
              : (snapshots.data?.length ?? 0) === 0 ? <p className="p-6 text-sm text-muted-foreground">{t("managerHandover.noSnapshots")}</p>
                : <div className="divide-y">{snapshots.data?.map((snapshot) => <div key={snapshot.id} className="space-y-1 px-4 py-3">
                  <p className="text-sm font-medium">{snapshot.name}</p>
                  <p className="text-xs text-muted-foreground">{formatDate(snapshot.snapshotDate)} · {snapshot.previousManagerName || t("managerHandover.notAssigned")} → {snapshot.newManagerName}</p>
                  <p className="text-xs text-muted-foreground">{snapshot.handoverReason}</p>
                </div>)}</div>}
        </CardContent>
      </Card>
    </> : showApproveOnlySection ? renderSelfPendingCard(true) : <Card><CardContent className="flex flex-col items-start gap-3 p-6 text-sm text-muted-foreground">
      <span>{isLoading ? t("common.loading") : t("managerHandover.viewPermissionRequired")}</span>
      {canApprove && !isLoading ? <Link to="/my-work/approvals" className="text-primary underline-offset-4 hover:underline">{t("managerHandover.viewApprovalsHint")}</Link> : null}
    </CardContent></Card>}

    <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={t(initiationType === "ManagerInitiated" ? "managerHandover.requestTitle" : initiationType === "AdminInitiated" ? "managerHandover.adminRequestTitle" : "managerHandover.initialAssignmentTitle")} description={t(initiationType === "ManagerInitiated" ? "managerHandover.requestDescription" : initiationType === "AdminInitiated" ? "managerHandover.adminRequestDescription" : "managerHandover.initialAssignmentDescription", { entity: entityLabel })}>
      <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p> : null}
          <div><Label>{t("managerHandover.entity")}</Label><Input className="mt-1" value={entityName} readOnly /></div>
          <div><Label>{t("managerHandover.currentManager")}</Label><Input className="mt-1" value={capabilities.data?.currentManagerName || currentManagerName || t("managerHandover.notAssigned")} readOnly /></div>
          <div><Label>{t("managerHandover.newManager")} *</Label><div className="mt-1"><SearchableSelect options={managers} value={managerId} onValueChange={setManagerId} placeholder={t("managerHandover.selectManager")} searchPlaceholder={t("managerHandover.searchManager")} loading={eligibleManagers.isLoading} loadingText={t("projects.loadingManagers")} emptyText={t("managerHandover.noManagers")} disabled={eligibleManagers.isError} /></div>
            {eligibleManagers.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("managerHandover.loadFailed")}</p> : null}
            {!eligibleManagers.isLoading && !eligibleManagers.isError && managers.length === 0 ? <div className="mt-2 space-y-1 text-xs text-muted-foreground"><p>{t("projects.noEligibleManagersHint")}</p><Link to="/administration/users" className="text-primary underline-offset-4 hover:underline">{t("projects.manageUsers")}</Link></div> : null}
          </div>
          <div><Label htmlFor="manager-handover-effective-date">{t("managerHandover.effectiveDate")} *</Label><Input id="manager-handover-effective-date" className="mt-1" type="date" value={effectiveDate} onChange={(event) => setEffectiveDate(event.target.value)} required /><p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.effectiveDateHint")}</p></div>
          <div><Label htmlFor="manager-handover-reason">{t(initiationType === "ManagerInitiated" ? "managerHandover.reason" : "managerHandover.administrativeReason")} *</Label><Textarea id="manager-handover-reason" className="mt-1" name="reason" maxLength={500} required placeholder={t("managerHandover.reasonPlaceholder")} /></div>
          <div><Label htmlFor="manager-handover-notes">{t("managerHandover.notes")}</Label><Textarea id="manager-handover-notes" className="mt-1" name="notes" rows={3} maxLength={2000} placeholder={t("managerHandover.notesPlaceholder")} /></div>
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => setSheetOpen(false)} disabled={create.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={create.isPending || eligibleManagers.isLoading || eligibleManagers.isError || !managerId || Boolean(pendingRequest)}>{create.isPending ? t("projects.submitting") : t("managerHandover.submit")}</Button>
        </SideSheetFooter>
      </form>
    </SideSheet>

    <Dialog open={rejectingId != null} onOpenChange={(open) => { if (!open) { setRejectingId(null); setRejectionReason(""); setRejectError(null); } }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("managerHandover.rejectTitle")}</DialogTitle><DialogDescription>{t("managerHandover.rejectDescription")}</DialogDescription></DialogHeader>
        <div className="space-y-2">
          <Label htmlFor="manager-handover-rejection-reason">{t("managerHandover.rejectionReason")} *</Label>
          <Textarea id="manager-handover-rejection-reason" value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value)} placeholder={t("managerHandover.rejectionReasonPlaceholder")} rows={3} />
          {rejectError ? <p role="alert" className="text-sm text-destructive">{rejectError}</p> : null}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => { setRejectingId(null); setRejectionReason(""); setRejectError(null); }} disabled={reject.isPending}>{t("common.cancel")}</Button>
          <Button variant="destructive" disabled={!rejectionReason.trim() || reject.isPending} onClick={() => void confirmReject()}>{reject.isPending ? <Loader2 className="me-1.5 h-4 w-4 animate-spin" /> : null}{t("managerHandover.confirmReject")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <Dialog open={cancellingId != null} onOpenChange={(open) => { if (!open) setCancellingId(null); }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("managerHandover.cancelTitle")}</DialogTitle><DialogDescription>{t("managerHandover.cancelDescription")}</DialogDescription></DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => setCancellingId(null)} disabled={cancel.isPending}>{t("common.cancel")}</Button>
          <Button variant="destructive" onClick={() => void confirmCancel()} disabled={cancel.isPending}>{cancel.isPending ? <Loader2 className="me-1.5 h-4 w-4 animate-spin" /> : null}{t("managerHandover.confirmCancel")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>;
}
