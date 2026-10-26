import * as React from "react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { CheckCircle2, XCircle, ArrowRightLeft, GitBranch, Loader2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Badge } from "@components/ui/Badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@components/ui/Dialog";
import { Textarea } from "@components/ui/Textarea";
import { formatDate } from "@lib/utils";
import { getStatusTextClass } from "@lib/status-presentation";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { getApprovalReviewPath } from "@lib/approval-navigation";
import {
  usePendingHandoverRequestsForMe,
  useAcceptHandoverRequest,
  useRejectHandoverRequest,
  usePendingManagerHandovers,
  useAcceptManagerHandover,
  useRejectManagerHandover,
  usePendingProjectChangeRequests,
  useApproveProjectChangeRequest,
  useRejectProjectChangeRequest,
} from "./api/myWork";
import type { PendingProjectChangeApprovalDto, ManagerHandoverRequestDto } from "./api/myWork";

export function MyWorkApprovalsPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { refreshSnapshot, effectiveReadOnly } = useManagement();
  const canApproveHandovers = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Approve));
  const canApproveChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Approve));
  const { data: requests, isLoading, isError: handoverRequestsError, refetch: refetchHandovers } = usePendingHandoverRequestsForMe(canApproveHandovers);
  const { data: managerRequests, isLoading: managerLoading, isError: managerRequestsError, refetch: refetchManagerHandovers } = usePendingManagerHandovers(canApproveHandovers);
  const { data: changeRequests, isLoading: changeLoading, isError: changeRequestsError, refetch: refetchChangeRequests } = usePendingProjectChangeRequests(canApproveChangeRequests);
  const acceptMutation = useAcceptHandoverRequest();
  const rejectMutation = useRejectHandoverRequest();
  const approveChange = useApproveProjectChangeRequest();
  const rejectChange = useRejectProjectChangeRequest();
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectionReason, setRejectionReason] = useState("");
  const [managerRejectingId, setManagerRejectingId] = useState<string | null>(null);
  const [managerRejectionReason, setManagerRejectionReason] = useState("");
  const [managerHandoverActionError, setManagerHandoverActionError] = useState<{ requestId: string; message: string } | null>(null);
  const [managerRejectError, setManagerRejectError] = useState<string | null>(null);
  const [changeRejectingId, setChangeRejectingId] = useState<string | null>(null);
  const [changeRejectionReason, setChangeRejectionReason] = useState("");
  const [changeApprovalError, setChangeApprovalError] = useState<{ requestId: string; message: string } | null>(null);
  const [changeRejectError, setChangeRejectError] = useState<string | null>(null);
  const [handoverActionError, setHandoverActionError] = useState<string | null>(null);
  const managerAccept = useAcceptManagerHandover("programs");
  const managerAcceptPortfolio = useAcceptManagerHandover("portfolios");
  const managerReject = useRejectManagerHandover("programs");
  const managerRejectPortfolio = useRejectManagerHandover("portfolios");

  const pending = canApproveHandovers ? (requests ?? []).filter((r) => r.status === "Pending") : [];
  const history = canApproveHandovers ? (requests ?? []).filter((r) => r.status !== "Pending") : [];
  const managerPending = canApproveHandovers ? managerRequests ?? [] : [];
  const changePending = canApproveChangeRequests ? changeRequests ?? [] : [];
  const canRetryHandoverRequests = canApproveHandovers && (handoverRequestsError || managerRequestsError);
  const canRetryChangeRequests = canApproveChangeRequests && changeRequestsError;

  const handleAccept = async (id: string) => {
    setHandoverActionError(null);
    try {
      await acceptMutation.mutateAsync(id);
      await refreshSnapshot();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      setHandoverActionError(status === 409 ? t("approvals.handoverDecisionConflict") : t("approvals.handoverDecisionFailed"));
    }
  };

  const handleManagerHandoverAccept = async (request: ManagerHandoverRequestDto) => {
    setManagerHandoverActionError(null);
    const mutation = request.entityType === "Program" ? managerAccept : managerAcceptPortfolio;
    try {
      await mutation.mutateAsync(request.id);
      await refreshSnapshot();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      setManagerHandoverActionError({ requestId: request.id, message: status === 409 ? t("approvals.handoverDecisionConflict") : t("approvals.handoverDecisionFailed") });
    }
  };

  const handleManagerHandoverReject = async () => {
    if (!managerRejectingId || !managerRejectionReason.trim()) return;
    setManagerRejectError(null);
    const request = managerRequests?.find((item) => item.id === managerRejectingId);
    if (!request) {
      setManagerRejectError(t("approvals.handoverDecisionConflict"));
      return;
    }
    const mutation = request.entityType === "Program" ? managerReject : managerRejectPortfolio;
    try {
      await mutation.mutateAsync({ requestId: managerRejectingId, rejectionReason: managerRejectionReason.trim() });
      setManagerRejectingId(null);
      setManagerRejectionReason("");
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      setManagerRejectError(status === 409 ? t("approvals.handoverDecisionConflict") : t("approvals.handoverDecisionFailed"));
    }
  };

  const handleReject = async (id: string) => {
    if (!rejectionReason.trim()) return;
    await rejectMutation.mutateAsync({ requestId: id, rejectionReason: rejectionReason.trim() });
    setRejectingId(null);
    setRejectionReason("");
  };

  const handleApproveChange = async (id: string) => {
    setChangeApprovalError(null);
    try {
      await approveChange.mutateAsync({ id, dto: {} });
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status;
      setChangeApprovalError({ requestId: id, message: status === 409 ? t("approvals.changeDecisionConflict") : t("approvals.changeDecisionFailed") });
    }
  };

  const handleRejectChange = async (id: string) => {
    if (!changeRejectionReason.trim()) return;
    setChangeRejectError(null);
    try {
      await rejectChange.mutateAsync({ id, decisionNotes: changeRejectionReason.trim() });
      setChangeRejectingId(null);
      setChangeRejectionReason("");
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status;
      setChangeRejectError(status === 409 ? t("approvals.changeDecisionConflict") : t("approvals.changeDecisionFailed"));
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader title={t("approvals.title")} description={t("approvals.description")} />

      <Tabs defaultValue="pending">
        <TabsList>
          <TabsTrigger value="pending">{t("approvals.pending")} ({pending.length})</TabsTrigger>
          <TabsTrigger value="history">{t("approvals.history")} ({history.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="pending" className="space-y-4">
          {handoverActionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{handoverActionError}</p> : null}
          {canRetryHandoverRequests ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.handoverRequestsLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => {
                if (handoverRequestsError) void refetchHandovers();
                if (managerRequestsError) void refetchManagerHandovers();
              }}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {canRetryChangeRequests ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.changeRequestsLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchChangeRequests()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {isLoading || managerLoading || changeLoading ? (
            <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
          ) : pending.length === 0 && managerPending.length === 0 && changePending.length === 0 && !canRetryHandoverRequests && !canRetryChangeRequests ? (
            <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <CheckCircle2 className="h-12 w-12" />
              <p className="text-sm">{t("approvals.noPending")}</p>
            </div>
          ) : (
            <div className="space-y-1">
              {pending.map((request) => (
                <div key={request.id} className="flex items-start gap-4 border-t px-4 py-4 last:border-0">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
                    <ArrowRightLeft className="h-5 w-5" />
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{t("approvals.handoverRequest")}</span>
                      <Badge status="Pending">{t("approvals.statusPending")}</Badge>
                    </div>
                    <p className="mt-1 text-sm">
                        {request.currentProjectManagerName || t("managerHandover.notAssigned")} → <span className="font-medium">{request.newProjectManagerName}</span>
                    </p>
                      {request.initiationType !== "ManagerInitiated" ? <p className="mt-1 text-xs text-muted-foreground">{t(request.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}
                    <p className="text-xs text-muted-foreground">
                      <span className="font-medium">{request.projectName}</span>{" "}
                      · {request.projectCode}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">{request.reason}</p>
                    <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                      <span>{t("approvals.effectiveDate")}: {formatDate(request.effectiveHandoverDate)}</span>
                      <span>·</span>
                      <span>{t("approvals.requestedBy")}: {request.requestedByName}</span>
                      <span>·</span>
                      <span>{formatDate(request.requestedAt)}</span>
                    </div>
                    <Link to={getApprovalReviewPath("project-handover", request.id)} className="mt-2 inline-flex text-sm font-medium text-primary underline-offset-4 hover:underline">{t("approvals.reviewRequest")}</Link>
                  </div>
                  {!effectiveReadOnly ? <div className="flex gap-2">
                    <Button size="sm" disabled={acceptMutation.isPending || rejectMutation.isPending} onClick={() => handleAccept(request.id)}>
                      <CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => setRejectingId(request.id)}>
                      <XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}
                    </Button>
                  </div> : null}
                </div>
              ))}
              {managerPending.map((request) => (
                <ManagerApproval
                  key={`${request.entityType}-${request.id}`}
                  request={request}
                  accepting={managerAccept.isPending || managerAcceptPortfolio.isPending}
                  rejecting={managerReject.isPending || managerRejectPortfolio.isPending}
                  canDecide={!effectiveReadOnly}
                  error={managerHandoverActionError?.requestId === request.id ? managerHandoverActionError.message : null}
                  onAccept={() => void handleManagerHandoverAccept(request)}
                  onReject={() => { setManagerRejectError(null); setManagerRejectingId(request.id); }}
                />
              ))}
              {changePending.map((request) => (
                <ChangeRequestApproval
                  key={request.id}
                  request={request}
                  approving={approveChange.isPending}
                  rejecting={rejectChange.isPending}
                  canDecide={!effectiveReadOnly}
                  approvalError={changeApprovalError?.requestId === request.id ? changeApprovalError.message : null}
                  onApprove={() => handleApproveChange(request.id)}
                  onReject={() => { setChangeApprovalError(null); setChangeRejectError(null); setChangeRejectingId(request.id); }}
                />
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="history">
          {history.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <XCircle className="h-12 w-12" />
              <p className="text-sm">{t("approvals.noHistory")}</p>
            </div>
          ) : (
            <div className="space-y-1">
              {history.map((request) => (
                <div key={request.id} className="flex items-start gap-4 border-t px-4 py-3 last:border-0">
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{request.projectName}</span>
                      <Badge status={request.status}>{request.status}</Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {request.currentProjectManagerName} → {request.newProjectManagerName} · {formatDate(request.requestedAt)}
                    </p>
                    {request.rejectionReason && (
                      <p className="mt-1 text-xs text-destructive">{t("approvals.rejectionReason")}: {request.rejectionReason}</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <Dialog open={!!managerRejectingId} onOpenChange={(open) => { if (!open) { setManagerRejectingId(null); setManagerRejectionReason(""); setManagerRejectError(null); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{t("approvals.rejectDialogTitle")}</DialogTitle><DialogDescription>{t("approvals.rejectDialogDescription")}</DialogDescription></DialogHeader>
          <div className="space-y-2"><Textarea value={managerRejectionReason} onChange={(event) => setManagerRejectionReason(event.target.value)} placeholder={t("approvals.rejectionReasonPlaceholder")} rows={3} />{managerRejectError ? <p role="alert" className="text-sm text-destructive">{managerRejectError}</p> : null}</div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setManagerRejectingId(null); setManagerRejectionReason(""); setManagerRejectError(null); }} disabled={managerReject.isPending || managerRejectPortfolio.isPending}>{t("common.cancel")}</Button>
            <Button
              variant="destructive"
              disabled={!managerRejectionReason.trim() || effectiveReadOnly || managerReject.isPending || managerRejectPortfolio.isPending}
              onClick={() => void handleManagerHandoverReject()}
            >
              {(managerReject.isPending || managerRejectPortfolio.isPending) && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("approvals.confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!rejectingId} onOpenChange={(open) => { if (!open) { setRejectingId(null); setRejectionReason(""); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("approvals.rejectDialogTitle")}</DialogTitle>
            <DialogDescription>{t("approvals.rejectDialogDescription")}</DialogDescription>
          </DialogHeader>
          <Textarea value={rejectionReason} onChange={(e) => setRejectionReason(e.target.value)} placeholder={t("approvals.rejectionReasonPlaceholder")} rows={3} />
          <DialogFooter>
            <Button variant="outline" onClick={() => { setRejectingId(null); setRejectionReason(""); }}>{t("common.cancel")}</Button>
            <Button variant="destructive" disabled={!rejectionReason.trim() || effectiveReadOnly || rejectMutation.isPending} onClick={() => rejectingId && handleReject(rejectingId)}>
              {rejectMutation.isPending && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("approvals.confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!changeRejectingId} onOpenChange={(open) => { if (!open) { setChangeRejectingId(null); setChangeRejectionReason(""); setChangeRejectError(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("approvals.rejectChangeDialogTitle")}</DialogTitle>
            <DialogDescription>{t("approvals.rejectChangeDialogDescription")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Textarea value={changeRejectionReason} onChange={(e) => setChangeRejectionReason(e.target.value)} placeholder={t("approvals.decisionNotesPlaceholder")} rows={3} />
            {!changeRejectionReason.trim() && <p className="text-xs text-muted-foreground">{t("approvals.decisionNotesRequired")}</p>}
            {changeRejectError ? <p role="alert" className="text-sm text-destructive">{changeRejectError}</p> : null}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setChangeRejectingId(null); setChangeRejectionReason(""); setChangeRejectError(null); }}>{t("common.cancel")}</Button>
            <Button variant="destructive" disabled={!changeRejectionReason.trim() || effectiveReadOnly || rejectChange.isPending} onClick={() => changeRejectingId && handleRejectChange(changeRejectingId)}>
              {rejectChange.isPending && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("approvals.confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ManagerApproval({ request, accepting, rejecting, canDecide, error, onAccept, onReject }: { request: ManagerHandoverRequestDto; accepting: boolean; rejecting: boolean; canDecide: boolean; error?: string | null; onAccept: () => void; onReject: () => void }) {
  const { t } = useI18n();
  const entityType = request.entityType === "Program" ? t("managerHandover.program") : t("managerHandover.portfolio");
  const reviewType = request.entityType === "Program" ? "program-handover" : "portfolio-handover";
  return (
    <div className="flex items-start gap-4 border-t px-4 py-4">
      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
        <ArrowRightLeft className="h-5 w-5" />
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("managerHandover.handover")} · {entityType}</span>
          <span className={`text-xs font-semibold ${getStatusTextClass("Pending")}`}>{t("approvals.statusPending")}</span>
        </div>
          <p className="mt-1 text-sm">{request.currentManagerName || t("managerHandover.notAssigned")} → <span className="font-medium">{request.newManagerName}</span></p>
          {request.initiationType !== "ManagerInitiated" ? <p className="mt-1 text-xs text-muted-foreground">{t(request.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}
        <p className="text-xs text-muted-foreground">{request.entityName} · {request.reason}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("approvals.effectiveDate")}: {formatDate(request.effectiveHandoverDate)} · {t("approvals.requestedBy")}: {request.requestedByName}</p>
        <Link to={getApprovalReviewPath(reviewType, request.id)} className="mt-2 inline-flex text-sm font-medium text-primary underline-offset-4 hover:underline">{t("approvals.reviewRequest")}</Link>
        {error ? <p role="alert" className="mt-2 text-xs text-destructive">{error}</p> : null}
      </div>
      {canDecide ? <div className="flex gap-2">
        <Button size="sm" disabled={accepting || rejecting} onClick={onAccept}><CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}</Button>
        <Button size="sm" variant="outline" disabled={accepting || rejecting} onClick={onReject}><XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}</Button>
      </div> : null}
    </div>
  );
}

function ChangeRequestApproval({ request, approving, rejecting, canDecide, approvalError, onApprove, onReject }: { request: PendingProjectChangeApprovalDto; approving: boolean; rejecting: boolean; canDecide: boolean; approvalError?: string | null; onApprove: () => void; onReject: () => void }) {
  const { t } = useI18n();
  return (
    <div className="flex items-start gap-4 border-t px-4 py-4">
      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
        <GitBranch className="h-5 w-5" />
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("approvals.changeRequest")}</span>
          <span className={`text-xs font-semibold ${getStatusTextClass(request.status)}`}>{t(`projects.changeStatus.${request.status}`)}</span>
        </div>
        <p className="mt-1 text-xs text-muted-foreground">
          <span className="font-medium">{request.projectName}</span>
          <span> · </span>
          {t("approvals.changeType")}: {t(request.type === "Baseline" ? "projects.changeTypeBaseline" : "projects.changeTypeAdministrative")}
        </p>
        <p className="mt-1 text-sm">{t("approvals.changeNumber")} #{request.number} · {request.reason}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("approvals.requestedBy")}: {request.requestedByName} · {formatDate(request.requestedAt)}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("projects.approvalProgress")}: <span className="font-medium tabular-nums">{request.approvalCount}/{request.requiredApprovalCount}</span></p>
        <Link to={getApprovalReviewPath("change-request", request.id)} className="mt-2 inline-flex text-sm font-medium text-primary underline-offset-4 hover:underline">{t("approvals.reviewRequest")}</Link>
        {approvalError ? <p role="alert" className="mt-2 text-xs text-destructive">{approvalError}</p> : null}
      </div>
      {canDecide ? <div className="flex gap-2">
        <Button size="sm" disabled={approving || rejecting} onClick={onApprove}><CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}</Button>
        <Button size="sm" variant="outline" onClick={onReject}><XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}</Button>
      </div> : null}
    </div>
  );
}
