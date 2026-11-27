import * as React from "react";
import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { CheckCircle2, XCircle, ArrowRightLeft, GitBranch, Landmark, Loader2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Badge } from "@components/ui/Badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@components/ui/Dialog";
import { Textarea } from "@components/ui/Textarea";
import { ApiError } from "@api/client";
import { getStatusTextClass } from "@lib/status-presentation";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { getApprovalReviewPath, getManagerHandoverApprovalReviewType } from "@lib/approval-navigation";
import {
  usePendingHandoverRequestsForMe,
  useAcceptHandoverRequest,
  useRejectHandoverRequest,
  usePendingManagerHandovers,
  useAcceptManagerHandover,
  useRejectManagerHandover,
  usePendingProjectChangeRequests,
  usePendingAnnualBudgetApprovals,
  usePendingPaymentRequestApprovals,
  useDecideAssignedPaymentRequest,
  useApproveProjectChangeRequest,
  useRejectProjectChangeRequest,
} from "./api/myWork";
import type { PendingProjectChangeApprovalDto, ManagerHandoverRequestDto, PendingAnnualBudgetApprovalDto, PendingPaymentRequestApprovalDto } from "./api/myWork";
import { MyWorkTabs } from "./MyWorkTabs";
import { AnnualBudgetReviewDetailsSheet } from "./AnnualBudgetReviewDetailsSheet";
import { useAssignedAnnualBudgetApprovalHistory } from "./annualBudgetReviewApi";
import type { AssignedAnnualBudgetApprovalHistoryItem } from "./annualBudgetReviewApi";
import { getLocalizedApiErrorMessage, type ApiErrorLocale } from "@api/api-error-localization";
import {
  useApproveTemplateDeletion,
  useRejectTemplateDeletion,
  useTemplateDeletionInbox,
  type TemplateDeletionRequestDto,
} from "../projects/api/templateDeletion";

function paymentRequestApprovalStatusText(t: (key: string) => string, status: string) {
  const key = `projects.paymentRequestStatus${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

export function MyWorkApprovalsPage() {
  const { t, locale, formatDate, formatCurrency } = useI18n();
  const [searchParams] = useSearchParams();
  const templateDeletionRequestTarget = searchParams.get("templateDeletionRequestId");
  const templateDeletionTargetRef = useRef<HTMLDivElement>(null);
  const [activeApprovalsTab, setActiveApprovalsTab] = useState("pending");
  const { user } = useAuth();
  const { refreshSnapshot, effectiveReadOnly } = useManagement();
  const canApproveHandovers = Boolean(user
    && hasPermission(user.permissions, Permissions.Handover.Approve)
    && user.roles.some((role) => role.category?.toLowerCase() === "management"));
  const canApproveChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Approve));
  const canViewAnnualBudgetApprovalHistory = Boolean(user
    && hasPermission(user.permissions, Permissions.Project.View)
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.View));
  const canViewAnnualBudgetApprovals = Boolean(user
    && canViewAnnualBudgetApprovalHistory
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.Approve));
  const canViewPaymentRequestApprovals = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.Approve));
  const canViewTemplateDeletionRequests = Boolean(user
    && hasPermission(user.permissions, Permissions.Document.TemplateDeletion.View));
  const canApproveTemplateDeletion = Boolean(user
    && hasPermission(user.permissions, Permissions.Document.TemplateDeletion.Approve));
  const canRejectTemplateDeletion = Boolean(user
    && hasPermission(user.permissions, Permissions.Document.TemplateDeletion.Reject));
  const { data: requests, isLoading, isError: handoverRequestsError, refetch: refetchHandovers } = usePendingHandoverRequestsForMe(canApproveHandovers);
  const { data: managerRequests, isLoading: managerLoading, isError: managerRequestsError, refetch: refetchManagerHandovers } = usePendingManagerHandovers(canApproveHandovers);
  const { data: changeRequests, isLoading: changeLoading, isError: changeRequestsError, refetch: refetchChangeRequests } = usePendingProjectChangeRequests(canApproveChangeRequests);
  const { data: annualBudgetApprovals, isLoading: annualBudgetApprovalsLoading, isError: annualBudgetApprovalsError, refetch: refetchAnnualBudgetApprovals } = usePendingAnnualBudgetApprovals(canViewAnnualBudgetApprovals);
  const { data: paymentRequestApprovals, isLoading: paymentRequestApprovalsLoading, isError: paymentRequestApprovalsError, refetch: refetchPaymentRequestApprovals } = usePendingPaymentRequestApprovals(canViewPaymentRequestApprovals && activeApprovalsTab === "pending");
  const { data: annualBudgetHistory, isLoading: annualBudgetHistoryLoading, isError: annualBudgetHistoryError, refetch: refetchAnnualBudgetHistory } = useAssignedAnnualBudgetApprovalHistory(canViewAnnualBudgetApprovalHistory && activeApprovalsTab === "history");
  const { data: templateDeletionPendingPage, isLoading: templateDeletionPendingLoading, isError: templateDeletionPendingError, refetch: refetchTemplateDeletionPending } = useTemplateDeletionInbox("PendingApproval", canViewTemplateDeletionRequests);
  const { data: templateDeletionHistoryPage, isLoading: templateDeletionHistoryLoading, isError: templateDeletionHistoryError, refetch: refetchTemplateDeletionHistory } = useTemplateDeletionInbox("History", canViewTemplateDeletionRequests && (activeApprovalsTab === "history" || Boolean(templateDeletionRequestTarget)));
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
  const [annualBudgetReviewRequestId, setAnnualBudgetReviewRequestId] = useState<string | null>(null);
  const [templateDeletionDecision, setTemplateDeletionDecision] = useState<{ request: TemplateDeletionRequestDto; decision: "approve" | "reject" } | null>(null);
  const [templateDeletionDecisionComments, setTemplateDeletionDecisionComments] = useState("");
  const [templateDeletionActionError, setTemplateDeletionActionError] = useState<string | null>(null);
  const [templateDeletionDecisionValidation, setTemplateDeletionDecisionValidation] = useState<string | null>(null);
  const [paymentRequestDecision, setPaymentRequestDecision] = useState<{ request: PendingPaymentRequestApprovalDto; decision: "Rejected" | "ReturnedForRevision" } | null>(null);
  const [paymentRequestDecisionReason, setPaymentRequestDecisionReason] = useState("");
  const [paymentRequestDecisionError, setPaymentRequestDecisionError] = useState<string | null>(null);
  const [paymentRequestDecisionValidation, setPaymentRequestDecisionValidation] = useState<string | null>(null);
  const [paymentRequestActionErrors, setPaymentRequestActionErrors] = useState<Record<string, string>>({});
  const managerAccept = useAcceptManagerHandover("programs");
  const managerAcceptPortfolio = useAcceptManagerHandover("portfolios");
  const managerReject = useRejectManagerHandover("programs");
  const managerRejectPortfolio = useRejectManagerHandover("portfolios");
  const approveTemplateDeletion = useApproveTemplateDeletion();
  const rejectTemplateDeletion = useRejectTemplateDeletion();
  const decidePaymentRequest = useDecideAssignedPaymentRequest();

  const pending = canApproveHandovers ? (requests ?? []).filter((r) => r.status === "Pending") : [];
  const history = canApproveHandovers ? (requests ?? []).filter((r) => r.status !== "Pending") : [];
  const managerPending = canApproveHandovers ? managerRequests ?? [] : [];
  const changePending = canApproveChangeRequests ? changeRequests ?? [] : [];
  const annualBudgetPending = canViewAnnualBudgetApprovals ? annualBudgetApprovals ?? [] : [];
  const paymentRequestPending = canViewPaymentRequestApprovals ? paymentRequestApprovals?.items ?? [] : [];
  const templateDeletionPending = canViewTemplateDeletionRequests ? templateDeletionPendingPage?.items ?? [] : [];
  const templateDeletionHistory = canViewTemplateDeletionRequests ? templateDeletionHistoryPage?.items ?? [] : [];
  useEffect(() => {
    if (!templateDeletionRequestTarget) return;
    if (templateDeletionPending.some((request) => request.id === templateDeletionRequestTarget)) {
      if (activeApprovalsTab !== "pending") setActiveApprovalsTab("pending");
      return;
    }
    if (templateDeletionHistory.some((request) => request.id === templateDeletionRequestTarget)
      && activeApprovalsTab !== "history") setActiveApprovalsTab("history");
  }, [activeApprovalsTab, templateDeletionHistory, templateDeletionPending, templateDeletionRequestTarget]);
  useEffect(() => {
    if (!templateDeletionRequestTarget || activeApprovalsTab !== "pending" && activeApprovalsTab !== "history") return;
    const visible = activeApprovalsTab === "pending"
      ? templateDeletionPending.some((request) => request.id === templateDeletionRequestTarget)
      : templateDeletionHistory.some((request) => request.id === templateDeletionRequestTarget);
    if (!visible) return;
    templateDeletionTargetRef.current?.scrollIntoView?.({ behavior: "smooth", block: "center" });
    templateDeletionTargetRef.current?.focus({ preventScroll: true });
  }, [activeApprovalsTab, templateDeletionHistory, templateDeletionPending, templateDeletionRequestTarget]);
  const canRetryProjectHandovers = canApproveHandovers && handoverRequestsError;
  const canRetryManagerHandovers = canApproveHandovers && managerRequestsError;
  const canRetryChangeRequests = canApproveChangeRequests && changeRequestsError;
  const canRetryAnnualBudgetApprovals = canViewAnnualBudgetApprovals && annualBudgetApprovalsError;
  const canRetryTemplateDeletionRequests = canViewTemplateDeletionRequests && templateDeletionPendingError;
  const canRetryTemplateDeletionHistory = canViewTemplateDeletionRequests && templateDeletionHistoryError;
  const pendingCount = pending.length + managerPending.length + changePending.length + annualBudgetPending.length + templateDeletionPending.length + paymentRequestPending.length;
  const approvalsLoading = (canApproveHandovers && (isLoading || managerLoading))
    || (canApproveChangeRequests && changeLoading)
    || (canViewAnnualBudgetApprovals && annualBudgetApprovalsLoading)
    || (canViewPaymentRequestApprovals && activeApprovalsTab === "pending" && paymentRequestApprovalsLoading)
    || (canViewTemplateDeletionRequests && templateDeletionPendingLoading);
  const completedAnnualBudgetApprovals = canViewAnnualBudgetApprovalHistory ? annualBudgetHistory ?? [] : [];
  const historyCount = history.length + completedAnnualBudgetApprovals.length + templateDeletionHistory.length;
  const approvalsLoadFailed = canRetryProjectHandovers || canRetryManagerHandovers
    || canRetryChangeRequests || canRetryAnnualBudgetApprovals || canRetryTemplateDeletionRequests
    || (canViewPaymentRequestApprovals && paymentRequestApprovalsError);

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

  const handleApprovePaymentRequest = async (request: PendingPaymentRequestApprovalDto) => {
    setPaymentRequestActionErrors(current => ({ ...current, [request.requestId]: "" }));
    try {
      await decidePaymentRequest.mutateAsync({
        projectId: request.projectId,
        requestId: request.requestId,
        decision: "Approved",
        rowVersion: request.rowVersion,
      });
    } catch (error: unknown) {
      const status = error instanceof ApiError ? error.status : (error as { status?: number })?.status;
      if (status === 409) await refetchPaymentRequestApprovals();
      setPaymentRequestActionErrors(current => ({
        ...current,
        [request.requestId]: status === 409 ? t("approvals.paymentRequestDecisionConflict") : t("approvals.paymentRequestDecisionFailed"),
      }));
    }
  };

  const openPaymentRequestDecision = (request: PendingPaymentRequestApprovalDto, decision: "Rejected" | "ReturnedForRevision") => {
    setPaymentRequestDecision({ request, decision });
    setPaymentRequestDecisionReason("");
    setPaymentRequestDecisionError(null);
    setPaymentRequestDecisionValidation(null);
  };

  const confirmPaymentRequestDecision = async () => {
    if (!paymentRequestDecision) return;
    if (paymentRequestDecision.decision === "ReturnedForRevision" && !paymentRequestDecisionReason.trim()) {
      setPaymentRequestDecisionValidation(t("projects.paymentRequestReturnReasonRequired"));
      return;
    }
    setPaymentRequestDecisionValidation(null);
    setPaymentRequestDecisionError(null);
    try {
      await decidePaymentRequest.mutateAsync({
        projectId: paymentRequestDecision.request.projectId,
        requestId: paymentRequestDecision.request.requestId,
        decision: paymentRequestDecision.decision,
        comment: paymentRequestDecisionReason.trim() || undefined,
        rowVersion: paymentRequestDecision.request.rowVersion,
      });
      setPaymentRequestDecision(null);
      setPaymentRequestDecisionReason("");
    } catch (error: unknown) {
      const status = error instanceof ApiError ? error.status : (error as { status?: number })?.status;
      if (status === 409) await refetchPaymentRequestApprovals();
      setPaymentRequestDecisionError(status === 409
        ? t("approvals.paymentRequestDecisionConflict")
        : t("approvals.paymentRequestDecisionFailed"));
    }
  };

  const handleManagerHandoverAccept = async (request: ManagerHandoverRequestDto) => {
    setManagerHandoverActionError(null);
    const reviewType = getManagerHandoverApprovalReviewType(request.entityType);
    if (!reviewType) {
      setManagerHandoverActionError({ requestId: request.id, message: t("approvals.reviewUnavailable") });
      return;
    }
    const mutation = reviewType === "program-handover" ? managerAccept : managerAcceptPortfolio;
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
    const reviewType = getManagerHandoverApprovalReviewType(request.entityType);
    if (!reviewType) {
      setManagerRejectError(t("approvals.reviewUnavailable"));
      return;
    }
    const mutation = reviewType === "program-handover" ? managerReject : managerRejectPortfolio;
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

  const openTemplateDeletionDecision = (request: TemplateDeletionRequestDto, decision: "approve" | "reject") => {
    setTemplateDeletionActionError(null);
    setTemplateDeletionDecisionValidation(null);
    setTemplateDeletionDecisionComments("");
    setTemplateDeletionDecision({ request, decision });
  };

  const submitTemplateDeletionDecision = async () => {
    if (!templateDeletionDecision) return;
    const comments = templateDeletionDecisionComments.trim();
    if (!comments) {
      setTemplateDeletionDecisionValidation(t("approvals.templateDeletionDecisionCommentsRequired"));
      return;
    }
    setTemplateDeletionActionError(null);
    setTemplateDeletionDecisionValidation(null);
    try {
      const decision = templateDeletionDecision.decision;
      const requestId = templateDeletionDecision.request.id;
      if (decision === "approve") await approveTemplateDeletion.mutateAsync({ requestId, comments });
      else await rejectTemplateDeletion.mutateAsync({ requestId, comments });
      setTemplateDeletionDecision(null);
      setTemplateDeletionDecisionComments("");
    } catch (error: unknown) {
      const status = (error as { status?: number })?.status;
      setTemplateDeletionActionError(status === 409
        ? t("approvals.templateDeletionDecisionConflict")
        : error instanceof ApiError
          ? getLocalizedApiErrorMessage(error.code, error.status, locale as ApiErrorLocale, error.details, error.summary)
          : t("approvals.templateDeletionDecisionFailed"));
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader title={t("approvals.title")} description={t("approvals.description")} />
      <MyWorkTabs />

      <Tabs value={activeApprovalsTab} onValueChange={setActiveApprovalsTab}>
        <TabsList>
          <TabsTrigger value="pending">{t("approvals.pending")} ({pendingCount})</TabsTrigger>
          <TabsTrigger value="history">{t("approvals.history")} ({historyCount})</TabsTrigger>
        </TabsList>

        <TabsContent value="pending" className="space-y-4">
          {handoverActionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{handoverActionError}</p> : null}
          {canRetryProjectHandovers ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("common.errorLoading")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchHandovers()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {canRetryManagerHandovers ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("common.errorLoading")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchManagerHandovers()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {canRetryChangeRequests ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.changeRequestsLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchChangeRequests()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {canRetryAnnualBudgetApprovals ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.annualBudgetApprovalsLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchAnnualBudgetApprovals()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {canViewPaymentRequestApprovals && paymentRequestApprovalsError ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.paymentRequestApprovalsLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchPaymentRequestApprovals()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {canRetryTemplateDeletionRequests ? (
            <div role="alert" className="flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.templateDeletionInboxLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchTemplateDeletionPending()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {approvalsLoading ? (
            <div role="status" className="flex items-center justify-center gap-2 py-4 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />{t("common.loading")}
            </div>
          ) : null}
          {!approvalsLoading && pendingCount === 0 && !approvalsLoadFailed ? (
            <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <CheckCircle2 className="h-12 w-12" />
              <p className="text-sm">{t("approvals.noPending")}</p>
            </div>
          ) : null}
          <div className="space-y-3">
              {pending.map((request) => (
                <div key={request.id} className="flex items-start flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted">
                    <ArrowRightLeft className="h-5 w-5" />
                  </div>
                  <div className="min-w-0 flex-1">
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
                    <div className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
                      <span>{t("approvals.effectiveDate")}: {formatDate(request.effectiveHandoverDate)}</span>
                      <span>·</span>
                      <span>{t("approvals.requestedBy")}: {request.requestedByName}</span>
                      <span>·</span>
                      <span>{formatDate(request.requestedAt)}</span>
                    </div>
                    <Link to={getApprovalReviewPath("project-handover", request.id)} className="mt-2 inline-flex text-sm font-semibold text-primary underline-offset-4 hover:underline">{t("approvals.reviewDetails")}</Link>
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
              {annualBudgetPending.map((request) => (
                <AnnualBudgetApproval
                  key={request.requestId}
                  request={request}
                  onReview={() => setAnnualBudgetReviewRequestId(request.requestId)}
                />
              ))}
              {paymentRequestPending.map((request) => (
                <div key={request.requestId} className="flex flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row sm:items-start">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted"><Landmark className="h-5 w-5" aria-hidden="true" /></div>
                  <div className="min-w-0 flex-1 space-y-1">
                    <div className="flex flex-wrap items-center gap-2"><span className="text-sm font-medium">{request.title}</span><Badge status={request.status}>{paymentRequestApprovalStatusText(t, request.status)}</Badge></div>
                    <p className="text-xs text-muted-foreground">{request.reference} · {request.projectName}</p>
                    <p className="font-semibold tabular-nums">{formatCurrency(request.amount, request.currency)}</p>
                    <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
                      <span>{t("projects.paymentRequestRequestedBy")}: {request.requesterName || t("projects.paymentRequestUnknownRequester")}</span>
                      <span>{request.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : t("projects.financeApprovalParallel")}</span>
                      <span>{t("projects.paymentRequestSubmittedAt")}: {formatDate(request.submittedAtUtc)}</span>
                    </div>
                    <Link to={`/projects/${encodeURIComponent(request.projectId)}/financials?paymentRequestId=${encodeURIComponent(request.requestId)}`} className="mt-2 inline-flex text-sm font-semibold text-primary underline-offset-4 hover:underline">{t("approvals.reviewDetails")}</Link>
                    {paymentRequestActionErrors[request.requestId] ? <p role="alert" className="text-sm text-destructive">{paymentRequestActionErrors[request.requestId]}</p> : null}
                  </div>
                  {!effectiveReadOnly && request.canDecide ? <div className="flex flex-wrap gap-2">
                    <Button size="sm" disabled={decidePaymentRequest.isPending} onClick={() => void handleApprovePaymentRequest(request)}><CheckCircle2 className="me-1 h-4 w-4" aria-hidden="true" />{t("approvals.approve")}</Button>
                    <Button size="sm" variant="outline" disabled={decidePaymentRequest.isPending} onClick={() => openPaymentRequestDecision(request, "Rejected")}><XCircle className="me-1 h-4 w-4" aria-hidden="true" />{t("approvals.reject")}</Button>
                    <Button size="sm" variant="outline" disabled={decidePaymentRequest.isPending} onClick={() => openPaymentRequestDecision(request, "ReturnedForRevision")}>{t("projects.paymentRequestReturnForRevision")}</Button>
                  </div> : null}
                </div>
              ))}
              {templateDeletionPending.map((request) => (
                <div key={request.id} ref={request.id === templateDeletionRequestTarget ? templateDeletionTargetRef : undefined} tabIndex={request.id === templateDeletionRequestTarget ? -1 : undefined} data-template-deletion-request-id={request.id} className={`flex flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row sm:items-start ${request.id === templateDeletionRequestTarget ? "ring-2 ring-primary ring-offset-2" : ""}`}>
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted">
                    <XCircle className="h-5 w-5 text-muted-foreground" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-medium">{t("approvals.templateDeletionRequest")}</span>
                      <Badge status="Pending">{t("documents.templateDeletion.status.pendingApproval")}</Badge>
                    </div>
                    <p className="mt-1 text-sm font-semibold">{request.templateName}</p>
                    <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
                      <span>{t("approvals.templateDeletionRequestedBy")}: {request.requesterName || t("documents.unavailable")}</span>
                      <span>{t("approvals.templateDeletionOwnerScope")}: {templateDeletionOwnerScopeLabel(request.ownerScope, t)}</span>
                      <span>{formatDate(request.requestedAt)}</span>
                    </div>
                    <p className="mt-2 whitespace-pre-wrap break-words text-sm text-foreground">{request.reason}</p>
                  </div>
                  {!effectiveReadOnly && request.requesterId !== user?.id && (canApproveTemplateDeletion || canRejectTemplateDeletion) ? (
                    <div className="flex flex-wrap gap-2">
                      {canApproveTemplateDeletion ? <Button size="sm" disabled={approveTemplateDeletion.isPending || rejectTemplateDeletion.isPending} onClick={() => openTemplateDeletionDecision(request, "approve")}><CheckCircle2 className="me-1 h-4 w-4" />{t("approvals.approve")}</Button> : null}
                      {canRejectTemplateDeletion ? <Button size="sm" variant="outline" disabled={approveTemplateDeletion.isPending || rejectTemplateDeletion.isPending} onClick={() => openTemplateDeletionDecision(request, "reject")}><XCircle className="me-1 h-4 w-4" />{t("approvals.reject")}</Button> : null}
                    </div>
                  ) : null}
                </div>
              ))}
          </div>
        </TabsContent>

        <TabsContent value="history">
          {canViewAnnualBudgetApprovalHistory && annualBudgetHistoryError ? (
            <div role="alert" className="mb-3 flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.annualBudgetApprovalsLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchAnnualBudgetHistory()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {annualBudgetHistoryLoading ? (
            <div role="status" className="flex items-center justify-center gap-2 py-4 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />{t("common.loading")}
            </div>
          ) : null}
          {canRetryTemplateDeletionHistory ? (
            <div role="alert" className="mb-3 flex items-center justify-between gap-3 rounded-md border border-destructive/30 p-3 text-sm text-destructive">
              <span>{t("approvals.templateDeletionInboxLoadFailed")}</span>
              <Button size="sm" variant="outline" onClick={() => void refetchTemplateDeletionHistory()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {templateDeletionHistoryLoading ? <div role="status" className="mb-3 flex items-center justify-center gap-2 py-4 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />{t("common.loading")}</div> : null}
          {!annualBudgetHistoryLoading && !templateDeletionHistoryLoading && history.length === 0 && completedAnnualBudgetApprovals.length === 0 && templateDeletionHistory.length === 0 && !(canViewAnnualBudgetApprovalHistory && annualBudgetHistoryError) && !canRetryTemplateDeletionHistory ? (
            <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <XCircle className="h-12 w-12" />
              <p className="text-sm">{t("approvals.noHistory")}</p>
            </div>
          ) : (
            <div className="space-y-3">
              {history.map((request) => (
                <div key={request.id} className="flex items-start gap-4 rounded-lg border bg-card p-4 shadow-sm">
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
              {completedAnnualBudgetApprovals.map((request) => (
                <AnnualBudgetHistoryApproval
                  key={request.requestId}
                  request={request}
                  onReview={() => setAnnualBudgetReviewRequestId(request.requestId)}
                />
              ))}
              {templateDeletionHistory.map((request) => (
                <div key={request.id} ref={request.id === templateDeletionRequestTarget ? templateDeletionTargetRef : undefined} tabIndex={request.id === templateDeletionRequestTarget ? -1 : undefined} data-template-deletion-request-id={request.id} className={`rounded-lg border bg-card p-4 shadow-sm ${request.id === templateDeletionRequestTarget ? "ring-2 ring-primary ring-offset-2" : ""}`}>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold">{request.templateName}</span>
                      <Badge status={request.status}>{templateDeletionStatusLabel(request.status, t)}</Badge>
                    </div>
                    <span className="text-xs text-muted-foreground">{formatDate(request.requestedAt)}</span>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">{t("approvals.templateDeletionRequestedBy")}: {request.requesterName || t("documents.unavailable")}</p>
                  <p className="mt-2 whitespace-pre-wrap break-words text-sm">{request.reason}</p>
                  {request.approverName || request.decidedAt ? <p className="mt-2 text-xs text-muted-foreground">{t("approvals.templateDeletionDecisionBy")}: {request.approverName || t("documents.unavailable")}{request.decidedAt ? ` · ${formatDate(request.decidedAt)}` : ""}</p> : null}
                  {request.decisionComments ? <p className="mt-1 whitespace-pre-wrap break-words text-xs text-muted-foreground">{t("approvals.templateDeletionDecisionCommentsLabel")}: {request.decisionComments}</p> : null}
                  {request.deletionType ? <p className="mt-1 text-xs text-muted-foreground">{t("approvals.templateDeletionType")}: {request.deletionType.toLowerCase().includes("soft") ? t("approvals.templateDeletionSoftDelete") : t("approvals.templateDeletionHardDelete")}</p> : null}
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

      <Dialog open={templateDeletionDecision !== null} onOpenChange={(open) => { if (!open) { setTemplateDeletionDecision(null); setTemplateDeletionDecisionComments(""); setTemplateDeletionDecisionValidation(null); setTemplateDeletionActionError(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("approvals.templateDeletionDecisionTitle")}</DialogTitle>
            <DialogDescription>{t("approvals.templateDeletionDecisionDescription")}</DialogDescription>
          </DialogHeader>
          {templateDeletionDecision ? <div className="space-y-2">
            <p className="text-sm font-semibold">{templateDeletionDecision.request.templateName}</p>
            <p className="text-xs text-muted-foreground">{t("approvals.templateDeletionRequestedBy")}: {templateDeletionDecision.request.requesterName || t("documents.unavailable")}</p>
          </div> : null}
          <label className="block space-y-1 text-sm font-medium">
            <span>{t("approvals.templateDeletionDecisionComments")}</span>
            <Textarea
              value={templateDeletionDecisionComments}
              onChange={(event) => { setTemplateDeletionDecisionComments(event.target.value); setTemplateDeletionDecisionValidation(null); }}
              placeholder={t("approvals.templateDeletionDecisionCommentsPlaceholder")}
              rows={4}
              required
              aria-required="true"
              aria-invalid={Boolean(templateDeletionDecisionValidation)}
            />
          </label>
          {!templateDeletionDecisionComments.trim() ? <p className="text-xs text-muted-foreground">{t("approvals.templateDeletionDecisionCommentsRequired")}</p> : null}
          {templateDeletionDecisionValidation || templateDeletionActionError ? <p role="alert" className="text-sm text-destructive">{templateDeletionDecisionValidation || templateDeletionActionError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setTemplateDeletionDecision(null)} disabled={approveTemplateDeletion.isPending || rejectTemplateDeletion.isPending}>{t("common.cancel")}</Button>
            <Button
              variant={templateDeletionDecision?.decision === "reject" ? "destructive" : "default"}
              disabled={!templateDeletionDecisionComments.trim() || effectiveReadOnly || approveTemplateDeletion.isPending || rejectTemplateDeletion.isPending}
              onClick={() => void submitTemplateDeletionDecision()}
            >
              {(approveTemplateDeletion.isPending || rejectTemplateDeletion.isPending) ? <Loader2 className="me-1 h-4 w-4 animate-spin" /> : null}
              {templateDeletionDecision?.decision === "reject" ? t("approvals.reject") : t("approvals.approve")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={paymentRequestDecision !== null} onOpenChange={(open) => { if (!open) { setPaymentRequestDecision(null); setPaymentRequestDecisionReason(""); setPaymentRequestDecisionError(null); setPaymentRequestDecisionValidation(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{paymentRequestDecision?.decision === "ReturnedForRevision" ? t("projects.paymentRequestReturnForRevision") : t("approvals.reject")}</DialogTitle>
            <DialogDescription>{t("approvals.paymentRequestDecisionDescription", { reference: paymentRequestDecision?.request.reference ?? "" })}</DialogDescription>
          </DialogHeader>
          <label className="space-y-2 text-sm" htmlFor="my-work-payment-request-decision-reason">
            <span>{t("projects.paymentRequestDecisionCommentOrReturnReason")}{paymentRequestDecision?.decision === "ReturnedForRevision" ? " *" : ""}</span>
            <Textarea
              id="my-work-payment-request-decision-reason"
              value={paymentRequestDecisionReason}
              onChange={event => { setPaymentRequestDecisionReason(event.target.value); setPaymentRequestDecisionValidation(null); }}
              rows={4}
              required={paymentRequestDecision?.decision === "ReturnedForRevision"}
              aria-required={paymentRequestDecision?.decision === "ReturnedForRevision"}
              aria-invalid={Boolean(paymentRequestDecisionValidation)}
            />
          </label>
          {paymentRequestDecision?.decision === "ReturnedForRevision" ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestReturnReasonRequiredHint")}</p> : null}
          {paymentRequestDecisionValidation || paymentRequestDecisionError ? <p role="alert" className="text-sm text-destructive">{paymentRequestDecisionValidation || paymentRequestDecisionError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setPaymentRequestDecision(null)} disabled={decidePaymentRequest.isPending}>{t("common.cancel")}</Button>
            <Button
              variant={paymentRequestDecision?.decision === "Rejected" ? "destructive" : "default"}
              disabled={effectiveReadOnly || decidePaymentRequest.isPending || (paymentRequestDecision?.decision === "ReturnedForRevision" && !paymentRequestDecisionReason.trim())}
              onClick={() => void confirmPaymentRequestDecision()}
            >
              {decidePaymentRequest.isPending ? <Loader2 className="me-1 h-4 w-4 animate-spin" /> : null}
              {paymentRequestDecision?.decision === "ReturnedForRevision" ? t("projects.paymentRequestReturnForRevision") : t("approvals.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AnnualBudgetReviewDetailsSheet
        requestId={annualBudgetReviewRequestId ?? ""}
        open={annualBudgetReviewRequestId !== null}
        onOpenChange={(open) => { if (!open) setAnnualBudgetReviewRequestId(null); }}
        readOnly={effectiveReadOnly || (!annualBudgetPending.some((item) => item.requestId === annualBudgetReviewRequestId)
          && !canReviewBudgetApproval(completedAnnualBudgetApprovals.find((item) => item.requestId === annualBudgetReviewRequestId)))}
        onDecision={() => { void refetchAnnualBudgetApprovals(); void refetchAnnualBudgetHistory(); }}
      />
    </div>
  );
}

function templateDeletionStatusLabel(status: string, t: (key: string, values?: Record<string, string | number>) => string) {
  switch (status.toLowerCase()) {
    case "pendingapproval": return t("documents.templateDeletion.status.pendingApproval");
    case "approved": return t("documents.templateDeletion.status.approved");
    case "rejected": return t("documents.templateDeletion.status.rejected");
    case "stale": return t("documents.templateDeletion.status.stale");
    default: return t("documents.unavailable");
  }
}

function templateDeletionOwnerScopeLabel(scope: string, t: (key: string, values?: Record<string, string | number>) => string) {
  return scope.toLowerCase() === "tenant"
    ? t("approvals.templateDeletionTenantScope")
    : t("approvals.templateDeletionOrganizationScope");
}

function AnnualBudgetApproval({ request, onReview }: { request: PendingAnnualBudgetApprovalDto; onReview: () => void }) {
  const { t, formatDate, formatCurrency } = useI18n();
  return (
    <div className="flex items-start flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row">
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted">
        <Landmark className="h-5 w-5" aria-hidden="true" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold">{t("approvals.annualBudgetApproval")}</span>
          <Badge status="Pending">{t("approvals.statusPending")}</Badge>
        </div>
        <p className="mt-1 truncate text-sm font-medium">{request.projectName}{request.projectCode ? ` · ${request.projectCode}` : ""}</p>
        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
          <span>{t("approvals.annualBudgetFiscalYear", { year: request.fiscalYear })}</span>
          <span>{t("approvals.requestedBy")}: {request.requestedByName}</span>
          <time dateTime={request.requestedAtUtc}>{formatDate(request.requestedAtUtc)}</time>
        </div>
        <p className="mt-2 text-sm font-semibold tabular-nums">{formatCurrency(request.amount, request.currency)}</p>
      </div>
      <Button className="w-full shrink-0 sm:w-auto" onClick={onReview}>{t("approvals.reviewDetails")}</Button>
    </div>
  );
}

function AnnualBudgetHistoryApproval({ request, onReview }: { request: AssignedAnnualBudgetApprovalHistoryItem; onReview: () => void }) {
  const { t, formatDate, formatCurrency } = useI18n();
  const normalizedStatus = request.status.toLowerCase();
  const statusLabel = normalizedStatus === "pending" || normalizedStatus === "pendingapproval"
    ? t("projects.annualBudgetPendingReview")
    : normalizedStatus === "approved" || normalizedStatus === "completed"
      ? t("projects.annualBudgetApproved")
      : normalizedStatus === "rejected"
        ? t("projects.annualBudgetRejected")
        : normalizedStatus === "returned" || normalizedStatus === "returnedforrevision"
          ? t("projects.annualBudgetReturned")
          : normalizedStatus === "withdrawn"
            ? t("projects.annualBudgetWithdrawn")
            : t("common.notAvailable");

  return (
    <div className="flex items-start flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row">
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted">
        <Landmark className="h-5 w-5" aria-hidden="true" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold">{t("approvals.annualBudgetApproval")}</span>
          <Badge status={request.status}>{statusLabel}</Badge>
        </div>
        <p className="mt-1 truncate text-sm font-medium">{request.projectName}{request.projectCode ? ` · ${request.projectCode}` : ""}</p>
        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
          <span>{t("approvals.annualBudgetFiscalYear", { year: request.fiscalYear })}</span>
          <span>{t("approvals.requestedBy")}: {request.requestedByName ?? t("common.notAvailable")}</span>
          <time dateTime={request.requestedAtUtc}>{formatDate(request.requestedAtUtc)}</time>
        </div>
        <p className="mt-2 text-sm font-semibold tabular-nums">{formatCurrency(request.amount, request.currency)}</p>
      </div>
      <Button className="w-full shrink-0 sm:w-auto" variant="outline" onClick={onReview}>{t("approvals.reviewDetails")}</Button>
    </div>
  );
}

function canReviewBudgetApproval(request: AssignedAnnualBudgetApprovalHistoryItem | undefined) {
  if (!request) return false;
  const isPending = ["pending", "pendingapproval"].includes(request.status.toLowerCase());
  return isPending && request.canDecide;
}

function ManagerApproval({ request, accepting, rejecting, canDecide, error, onAccept, onReject }: { request: ManagerHandoverRequestDto; accepting: boolean; rejecting: boolean; canDecide: boolean; error?: string | null; onAccept: () => void; onReject: () => void }) {
  const { t, formatDate } = useI18n();
  const entityType = request.entityType === "Program" ? t("managerHandover.program") : request.entityType === "Portfolio" ? t("managerHandover.portfolio") : t("approvals.reviewUnavailable");
  const reviewType = getManagerHandoverApprovalReviewType(request.entityType);
  return (
    <div className="flex items-start flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row">
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted">
        <ArrowRightLeft className="h-5 w-5" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">{t("managerHandover.handover")} · {entityType}</span>
          <span className={`text-xs font-semibold ${getStatusTextClass("Pending")}`}>{t("approvals.statusPending")}</span>
        </div>
          <p className="mt-1 text-sm">{request.currentManagerName || t("managerHandover.notAssigned")} → <span className="font-medium">{request.newManagerName}</span></p>
          {request.initiationType !== "ManagerInitiated" ? <p className="mt-1 text-xs text-muted-foreground">{t(request.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}
        <p className="text-xs text-muted-foreground">{request.entityName} · {request.reason}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("approvals.effectiveDate")}: {formatDate(request.effectiveHandoverDate)} · {t("approvals.requestedBy")}: {request.requestedByName}</p>
        {reviewType ? <Link to={getApprovalReviewPath(reviewType, request.id)} className="mt-2 inline-flex text-sm font-semibold text-primary underline-offset-4 hover:underline">{t("approvals.reviewDetails")}</Link> : null}
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
  const { t, formatDate } = useI18n();
  return (
    <div className="flex items-start flex-col gap-4 rounded-lg border bg-card p-4 shadow-sm sm:flex-row">
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted">
        <GitBranch className="h-5 w-5" />
      </div>
      <div className="min-w-0 flex-1">
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
        <Link to={getApprovalReviewPath("change-request", request.id)} className="mt-2 inline-flex text-sm font-semibold text-primary underline-offset-4 hover:underline">{t("approvals.reviewDetails")}</Link>
        {approvalError ? <p role="alert" className="mt-2 text-xs text-destructive">{approvalError}</p> : null}
      </div>
      {canDecide ? <div className="flex gap-2">
        <Button size="sm" disabled={approving || rejecting} onClick={onApprove}><CheckCircle2 className="mr-1 h-4 w-4" />{t("approvals.approve")}</Button>
        <Button size="sm" variant="outline" onClick={onReject}><XCircle className="mr-1 h-4 w-4" />{t("approvals.reject")}</Button>
      </div> : null}
    </div>
  );
}
