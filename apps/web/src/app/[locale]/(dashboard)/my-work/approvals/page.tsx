"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CheckCircle2, XCircle, ArrowRightLeft, GitBranch, Loader2 } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  usePendingHandoverRequestsForMe,
  useAcceptHandoverRequest,
  useRejectHandoverRequest,
} from "@/hooks/projects/useProjects";
import { useAcceptManagerHandover, usePendingManagerHandovers, useRejectManagerHandover, type ManagerHandoverRequestDto } from "@/hooks/portfolios/useManagerHandovers";
import { useApproveProjectChangeRequest, usePendingProjectChangeRequests, useRejectProjectChangeRequest } from "@/hooks/projects/useProjectChangeRequests";
import type { ProjectChangeRequestDto } from "@/lib/api/project-change-requests";
import { useEnumLabel } from "@/hooks/useEnumLabels";

export default function ApprovalsPage() {
  const t = useTranslations("approvals");
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
      setChangeApprovalError(error?.data?.error ?? error?.data?.message ?? error?.message ?? t("approvalFailed"));
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
      <PageHeader title={t("title")} description={t("description")} />

      <Tabs defaultValue="pending">
        <TabsList>
          <TabsTrigger value="pending">{t("pending")} ({pending.length})</TabsTrigger>
          <TabsTrigger value="history">{t("history")} ({history.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="pending" className="space-y-4">
          {isLoading || managerLoading || changeLoading ? (
            <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
          ) : pending.length === 0 && managerPending.length === 0 && changePending.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <CheckCircle2 className="h-12 w-12" />
              <p className="text-sm">{t("noPending")}</p>
            </div>
          ) : (
            <div className="nexus-table-surface">
              {pending.map((request) => (
                <div key={request.id} className="nexus-table-row flex items-start gap-4 px-4 py-4 last:border-0">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
                    <ArrowRightLeft className="h-5 w-5" />
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{t("handoverRequest")}</span>
                      <Badge status="Pending">{t("statusPending")}</Badge>
                    </div>
                    <p className="mt-1 text-sm">
                      {request.currentProjectManagerName} → <span className="font-medium">{request.newProjectManagerName}</span>
                    </p>
                    <p className="text-xs text-muted-foreground">
                      <Link href={`/projects/${request.projectId}`} className="font-medium hover:underline">
                        {request.projectName}
                      </Link>{" "}
                      · {request.projectCode}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">{request.reason}</p>
                    <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                      <span>{t("effectiveDate")}: {formatDate(request.effectiveHandoverDate)}</span>
                      <span>·</span>
                      <span>{t("requestedBy")}: {request.requestedByName}</span>
                      <span>·</span>
                      <span>{formatDate(request.requestedAt)}</span>
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      disabled={acceptMutation.isPending || rejectMutation.isPending}
                      onClick={() => handleAccept(request.id)}
                    >
                      <CheckCircle2 className="mr-1 h-4 w-4" />{t("approve")}
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => setRejectingId(request.id)}>
                      <XCircle className="mr-1 h-4 w-4" />{t("reject")}
                    </Button>
                  </div>
                </div>
              ))}
              {managerPending.map((request) => <ManagerApproval key={`${request.entityType}-${request.id}`} request={request} accepting={managerAccept.isPending || managerAcceptPortfolio.isPending} onAccept={() => request.entityType === "Program" ? managerAccept.mutate(request.id) : managerAcceptPortfolio.mutate(request.id)} onReject={() => setManagerRejectingId(request.id)} />)}
              {changePending.map((request) => <ChangeRequestApproval key={request.id} request={request} approving={approveChange.isPending} rejecting={rejectChange.isPending} onApprove={() => handleApproveChange(request.id)} onReject={() => setChangeRejectingId(request.id)} approvalError={changeApprovalError} />)}
            </div>
          )}
        </TabsContent>

        <TabsContent value="history">
          {history.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <XCircle className="h-12 w-12" />
              <p className="text-sm">{t("noHistory")}</p>
            </div>
          ) : (
            <div className="nexus-table-surface">
              {history.map((request) => {
                // Determine badge variant based on status string from API
                return (
                  <div key={request.id} className="nexus-table-row flex items-start gap-4 px-4 py-3 last:border-0">
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium">{request.projectName}</span>
                        {/* Original rendering for handover status */}
                        <Badge status={request.status}>{request.status}</Badge>
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {request.currentProjectManagerName} → {request.newProjectManagerName} · {formatDate(request.requestedAt)}
                      </p>
                      {request.rejectionReason && (
                        <p className="mt-1 text-xs text-destructive">{t("rejectionReason")}: {request.rejectionReason}</p>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <Dialog open={!!managerRejectingId} onOpenChange={(open) => { if (!open) { setManagerRejectingId(null); setManagerRejectionReason(""); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{t("rejectDialogTitle")}</DialogTitle><DialogDescription>{t("rejectDialogDescription")}</DialogDescription></DialogHeader>
          <Textarea value={managerRejectionReason} onChange={(event) => setManagerRejectionReason(event.target.value)} placeholder={t("rejectionReasonPlaceholder")} rows={3} />
          <DialogFooter><Button variant="outline" onClick={() => { setManagerRejectingId(null); setManagerRejectionReason(""); }}>{t("cancel")}</Button><Button variant="destructive" disabled={!managerRejectionReason.trim() || managerReject.isPending || managerRejectPortfolio.isPending} onClick={() => { if (managerRejectingId) { const mutation = managerRequests?.find((request) => request.id === managerRejectingId)?.entityType === "Program" ? managerReject : managerRejectPortfolio; mutation.mutate({ requestId: managerRejectingId, rejectionReason: managerRejectionReason.trim() }, { onSuccess: () => { setManagerRejectingId(null); setManagerRejectionReason(""); } }); } }}>{(managerReject.isPending || managerRejectPortfolio.isPending) && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("confirmReject")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!rejectingId} onOpenChange={(open) => { if (!open) { setRejectingId(null); setRejectionReason(""); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("rejectDialogTitle")}</DialogTitle>
            <DialogDescription>{t("rejectDialogDescription")}</DialogDescription>
          </DialogHeader>
          <Textarea
            value={rejectionReason}
            onChange={(e) => setRejectionReason(e.target.value)}
            placeholder={t("rejectionReasonPlaceholder")}
            rows={3}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => { setRejectingId(null); setRejectionReason(""); }}>
              {t("cancel")}
            </Button>
            <Button
              variant="destructive"
              disabled={!rejectionReason.trim() || rejectMutation.isPending}
              onClick={() => rejectingId && handleReject(rejectingId)}
            >
              {rejectMutation.isPending && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!changeRejectingId} onOpenChange={(open) => { if (!open) { setChangeRejectingId(null); setChangeRejectionReason(""); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("rejectChangeDialogTitle")}</DialogTitle>
            <DialogDescription>{t("rejectChangeDialogDescription")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Textarea
              value={changeRejectionReason}
              onChange={(e) => setChangeRejectionReason(e.target.value)}
              placeholder={t("decisionNotesPlaceholder")}
              rows={3}
            />
            {!changeRejectionReason.trim() && <p className="text-xs text-muted-foreground">{t("decisionNotesRequired")}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setChangeRejectingId(null); setChangeRejectionReason(""); }}>
              {t("cancel")}
            </Button>
            <Button
              variant="destructive"
              disabled={!changeRejectionReason.trim() || rejectChange.isPending}
              onClick={() => changeRejectingId && handleRejectChange(changeRejectingId)}
            >
              {rejectChange.isPending && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}
              {t("confirmReject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function ChangeRequestApproval({ request, approving, rejecting, onApprove, onReject, approvalError }: { request: ProjectChangeRequestDto; approving: boolean; rejecting: boolean; onApprove: () => void; onReject: () => void; approvalError?: string | null }) {
  const t = useTranslations("approvals");
  const getStatusLabel = useEnumLabel('projectChangeRequestStatus');
  const getTypeLabel = useEnumLabel('projectChangeRequestType');

  return (
    <div className="flex items-start gap-4 border-t px-4 py-4">
      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
        <GitBranch className="h-5 w-5" />
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("changeRequest")}</span>
          {/* Using the new helper to get the translated status label */}
          <Badge status={request.status}>{getStatusLabel(request.status)}</Badge>
        </div>
        <p className="mt-1 text-xs text-muted-foreground">
          <Link href={`/projects/${request.projectId}`} className="font-medium hover:underline">
            {request.projectName}
          </Link>
          <span> · </span>
          {/* Using the new helper to get the translated type label */}
          {t("changeType")}: {getTypeLabel(request.type)}
        </p>
        <p className="mt-1 text-sm">
          {t("changeNumber")} #{request.number} · {request.reason}
        </p>
        {request.justification && <p className="mt-1 text-xs text-muted-foreground">{t("justification")}: {request.justification}</p>}
        <p className="mt-1 text-xs text-muted-foreground">
          {t("requestedBy")}: {request.requestedByName} · {formatDate(request.requestedAt)}
        </p>
        {approvalError && (
          <p className="mt-2 text-xs text-destructive">{approvalError}</p>
        )}
      </div>
      <div className="flex gap-2">
        <Button size="sm" disabled={approving || rejecting} onClick={onApprove}>
          <CheckCircle2 className="mr-1 h-4 w-4" />{t("approve")}
        </Button>
        <Button size="sm" variant="outline" onClick={onReject}>
          <XCircle className="mr-1 h-4 w-4" />{t("reject")}
        </Button>
      </div>
    </div>
  );
}

function ManagerApproval({ request, accepting, onAccept, onReject }: { request: ManagerHandoverRequestDto; accepting: boolean; onAccept: () => void; onReject: () => void }) {
  const t = useTranslations("approvals");
  return <div className="flex items-start gap-4 border-t px-4 py-4"><div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted"><ArrowRightLeft className="h-5 w-5" /></div><div className="flex-1"><div className="flex items-center gap-2"><span className="text-sm font-medium">{request.entityType} {t("handoverRequest")}</span><Badge status="Pending">{t("statusPending")}</Badge></div><p className="mt-1 text-sm">{request.currentManagerName} → <span className="font-medium">{request.newManagerName}</span></p><p className="text-xs text-muted-foreground">{request.entityName} · {request.reason}</p><p className="mt-1 text-xs text-muted-foreground">{t("effectiveDate")}: {formatDate(request.effectiveHandoverDate)} · {t("requestedBy")}: {request.requestedByName}</p></div><div className="flex gap-2"><Button size="sm" disabled={accepting} onClick={onAccept}><CheckCircle2 className="mr-1 h-4 w-4" />{t("approve")}</Button><Button size="sm" variant="outline" onClick={onReject}><XCircle className="mr-1 h-4 w-4" />{t("reject")}</Button></div></div>;
}
