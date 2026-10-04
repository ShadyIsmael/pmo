import * as React from "react";
import { useState } from "react";
import { CheckCircle2, XCircle, ArrowRightLeft, GitBranch, Loader2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Badge } from "@components/ui/Badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@components/ui/Dialog";
import { Textarea } from "@components/ui/Textarea";
import { formatDate } from "@lib/utils";
import { useI18n } from "@i18n/messages";
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
import type { ProjectChangeRequestDto, ManagerHandoverRequestDto } from "./api/myWork";

export function MyWorkApprovalsPage() {
  const { t } = useI18n();
  const { data: requests, isLoading } = usePendingHandoverRequestsForMe();
  const { data: managerRequests, isLoading: managerLoading } = usePendingManagerHandovers();
  const { data: changeRequests, isLoading: changeLoading } = usePendingProjectChangeRequests();
  const acceptMutation = useAcceptHandoverRequest();
  const rejectMutation = useRejectHandoverRequest();
  const approveChange = useApproveProjectChangeRequest();
  const rejectChange = useRejectProjectChangeRequest();
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectionReason, setRejectionReason] = useState("");
  const [managerRejectingId, setManagerRejectingId] = useState<string | null>(null);
  const [managerRejectionReason, setManagerRejectionReason] = useState("");
  const [changeRejectingId, setChangeRejectingId] = useState<string | null>(null);
  const [changeRejectionReason, setChangeRejectionReason] = useState("");
  const [changeApprovalError, setChangeApprovalError] = useState<string | null>(null);
  const managerAccept = useAcceptManagerHandover("programs");
  const managerAcceptPortfolio = useAcceptManagerHandover("portfolios");
  const managerReject = useRejectManagerHandover("programs");
  const managerRejectPortfolio = useRejectManagerHandover("portfolios");

  const pending = (requests ?? []).filter((r) => r.status === "Pending");
  const history = (requests ?? []).filter((r) => r.status !== "Pending");
  const managerPending = managerRequests ?? [];
  const changePending = changeRequests ?? [];

  const handleAccept = async (id: string) => {
    await acceptMutation.mutateAsync(id);
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
      const error = err as { data?: { error?: string; message?: string }; message?: string };
      setChangeApprovalError(error?.data?.error ?? error?.data?.message ?? error?.message ?? "Approval failed");
    }
  };

  const handleRejectChange = async (id: string) => {
    if (!changeRejectionReason.trim()) return;
    await rejectChange.mutateAsync({ id, decisionNotes: changeRejectionReason.trim() });
    setChangeRejectingId(null);
    setChangeRejectionReason("");
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
          {isLoading || managerLoading || changeLoading ? (
            <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
          ) : pending.length === 0 && managerPending.length === 0 && changePending.length === 0 ? (
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
                      {request.currentProjectManagerName} → <span className="font-medium">{request.newProjectManagerName}</span>
                    </p>
                    <p className="text-xs text-muted-foreground">
                      <a href={`/projects/${request.projectId}`} className="font-medium hover:underline">
                        {request.projectName}
                      </a>{" "}
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
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm" disabled={acceptMutation.isPending || rejectMutation.isPending} onClick={() => handleAccept(request.id)}>
                      <CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => setRejectingId(request.id)}>
                      <XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}
                    </Button>
                  </div>
                </div>
              ))}
              {managerPending.map((request) => (
                <ManagerApproval
                  key={`${request.entityType}-${request.id}`}
                  request={request}
                  accepting={managerAccept.isPending || managerAcceptPortfolio.isPending}
                  onAccept={() => (request.entityType === "Program" ? managerAccept.mutate(request.id) : managerAcceptPortfolio.mutate(request.id))}
                  onReject={() => setManagerRejectingId(request.id)}
                />
              ))}
              {changePending.map((request) => (
                <ChangeRequestApproval
                  key={request.id}
                  request={request}
                  approving={approveChange.isPending}
                  rejecting={rejectChange.isPending}
                  onApprove={() => handleApproveChange(request.id)}
                  onReject={() => setChangeRejectingId(request.id)}
                  approvalError={changeApprovalError}
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

      <Dialog open={!!managerRejectingId} onOpenChange={(open) => { if (!open) { setManagerRejectingId(null); setManagerRejectionReason(""); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{t("approvals.rejectDialogTitle")}</DialogTitle><DialogDescription>{t("approvals.rejectDialogDescription")}</DialogDescription></DialogHeader>
          <Textarea value={managerRejectionReason} onChange={(event) => setManagerRejectionReason(event.target.value)} placeholder={t("approvals.rejectionReasonPlaceholder")} rows={3} />
          <DialogFooter>
            <Button variant="outline" onClick={() => { setManagerRejectingId(null); setManagerRejectionReason(""); }}>{t("common.cancel")}</Button>
            <Button
              variant="destructive"
              disabled={!managerRejectionReason.trim() || managerReject.isPending || managerRejectPortfolio.isPending}
              onClick={() => {
                if (managerRejectingId) {
                  const mutation = managerRequests?.find((r) => r.id === managerRejectingId)?.entityType === "Program" ? managerReject : managerRejectPortfolio;
                  mutation.mutate({ requestId: managerRejectingId, rejectionReason: managerRejectionReason.trim() }, { onSuccess: () => { setManagerRejectingId(null); setManagerRejectionReason(""); } });
                }
              }}
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
            <Button variant="destructive" disabled={!rejectionReason.trim() || rejectMutation.isPending} onClick={() => rejectingId && handleReject(rejectingId)}>
              {rejectMutation.isPending && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("approvals.confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!changeRejectingId} onOpenChange={(open) => { if (!open) { setChangeRejectingId(null); setChangeRejectionReason(""); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("approvals.rejectChangeDialogTitle")}</DialogTitle>
            <DialogDescription>{t("approvals.rejectChangeDialogDescription")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Textarea value={changeRejectionReason} onChange={(e) => setChangeRejectionReason(e.target.value)} placeholder={t("approvals.decisionNotesPlaceholder")} rows={3} />
            {!changeRejectionReason.trim() && <p className="text-xs text-muted-foreground">{t("approvals.decisionNotesRequired")}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setChangeRejectingId(null); setChangeRejectionReason(""); }}>{t("common.cancel")}</Button>
            <Button variant="destructive" disabled={!changeRejectionReason.trim() || rejectChange.isPending} onClick={() => changeRejectingId && handleRejectChange(changeRejectingId)}>
              {rejectChange.isPending && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("approvals.confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ManagerApproval({ request, accepting, onAccept, onReject }: { request: ManagerHandoverRequestDto; accepting: boolean; onAccept: () => void; onReject: () => void }) {
  const { t } = useI18n();
  return (
    <div className="flex items-start gap-4 border-t px-4 py-4">
      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
        <ArrowRightLeft className="h-5 w-5" />
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{request.entityType} {t("approvals.handoverRequest")}</span>
          <Badge status="Pending">{t("approvals.statusPending")}</Badge>
        </div>
        <p className="mt-1 text-sm">{request.currentManagerName} → <span className="font-medium">{request.newManagerName}</span></p>
        <p className="text-xs text-muted-foreground">{request.entityName} · {request.reason}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("approvals.effectiveDate")}: {formatDate(request.effectiveHandoverDate)} · {t("approvals.requestedBy")}: {request.requestedByName}</p>
      </div>
      <div className="flex gap-2">
        <Button size="sm" disabled={accepting} onClick={onAccept}><CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}</Button>
        <Button size="sm" variant="outline" onClick={onReject}><XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}</Button>
      </div>
    </div>
  );
}

function ChangeRequestApproval({ request, approving, rejecting, onApprove, onReject, approvalError }: { request: ProjectChangeRequestDto; approving: boolean; rejecting: boolean; onApprove: () => void; onReject: () => void; approvalError?: string | null }) {
  const { t } = useI18n();
  return (
    <div className="flex items-start gap-4 border-t px-4 py-4">
      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
        <GitBranch className="h-5 w-5" />
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("approvals.changeRequest")}</span>
          <Badge status={request.status}>{request.status}</Badge>
        </div>
        <p className="mt-1 text-xs text-muted-foreground">
          <a href={`/projects/${request.projectId}`} className="font-medium hover:underline">
            {request.projectName}
          </a>
          <span> · </span>
          {t("approvals.changeType")}: {request.type}
        </p>
        <p className="mt-1 text-sm">{t("approvals.changeNumber")} #{request.number} · {request.reason}</p>
        {request.justification && <p className="mt-1 text-xs text-muted-foreground">{t("approvals.justification")}: {request.justification}</p>}
        <p className="mt-1 text-xs text-muted-foreground">{t("approvals.requestedBy")}: {request.requestedByName} · {formatDate(request.requestedAt)}</p>
        {approvalError && <p className="mt-2 text-xs text-destructive">{approvalError}</p>}
      </div>
      <div className="flex gap-2">
        <Button size="sm" disabled={approving || rejecting} onClick={onApprove}><CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}</Button>
        <Button size="sm" variant="outline" onClick={onReject}><XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}</Button>
      </div>
    </div>
  );
}