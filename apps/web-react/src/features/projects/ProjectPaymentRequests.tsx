import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { Check, Eye, Pencil, Receipt, Send, X, RotateCcw } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useI18n } from "@i18n/messages";
import { ApiError } from "@api/client";
import {
  useCancelProjectPaymentRequest,
  useCancelProjectPaymentReversal,
  useCreateQuickOneOffPaymentRequest,
  useCreateProjectPaymentRequest,
  useCreateProjectPaymentReversal,
  useDecideProjectPaymentRequest,
  useDecideProjectPaymentReversal,
  useDecideSettlementAdjustment,
  useConfirmSettlementAdjustmentExecution,
  usePaymentRequestApprovers,
  usePaymentRequestMilestones,
  useProjectPaymentReversalApprovers,
  useProjectFinancialFoundation,
  useProjectPaymentRequestFundingOptions,
  useProjectPaymentRequestSettlements,
  useProjectPaymentReversals,
  useAssignedProjectPaymentReversalReview,
  useProjectPaymentRequest,
  useProjectPaymentRequests,
  useRecordProjectPaymentRequestSettlement,
  useRecordSettlementAdjustment,
  useSettlementAdjustments,
  useSettlementAdjustmentApprovers,
  useSubmitProjectPaymentReversal,
  useSubmitProjectPaymentRequest,
  useUpdateProjectPaymentReversal,
  useUpdateProjectPaymentRequest,
  type PaymentRequestApproverCandidate,
  type ProjectPaymentReversal,
  type ProjectPaymentRequestListItem,
  type ProjectPaymentRequestSettlement,
  type SettlementAdjustmentDto,
} from "./api/project-financials";
import { usePlannedPaymentBudget, type PlannedPaymentRequestContext } from "./api/planned-payments";

type ApprovalMode = "Sequential" | "Parallel";
type RequestForm = { title: string; amount: string; reason: string; dueDate: string; milestoneId: string; annualBudgetId: string; annualBudgetLineId: string };
const emptyForm = (): RequestForm => ({ title: "", amount: "", reason: "", dueDate: todayLocal(), milestoneId: "", annualBudgetId: "", annualBudgetLineId: "" });
const todayLocal = () => {
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
};

function statusText(t: (key: string) => string, status: string) {
  const key = `projects.paymentRequestStatus${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

function isFourDecimalAmount(value: string) {
  return /^\d+(\.\d{1,4})?$/.test(value.trim()) && Number(value) > 0;
}

function settlementAdjustmentStatusKey(status: string) {
  const keys: Record<string, string> = {
    PendingApproval: "projects.settlementAdjustmentPendingApproval",
    Approved: "projects.settlementAdjustmentApproved",
    Rejected: "projects.settlementAdjustmentRejected",
    Executed: "projects.settlementAdjustmentExecuted",
  };
  return keys[status] ?? "projects.settlementAdjustmentUnknownStatus";
}

function SettlementAdjustmentRow({ projectId, settlementId, item, t, formatCurrency, formatDate, formatDateTime }: {
  projectId: string;
  settlementId: string;
  item: SettlementAdjustmentDto;
  t: (key: string, values?: Record<string, string | number>) => string;
  formatCurrency: (value: number, currency?: string) => string;
  formatDate: (value: string) => string;
  formatDateTime: (value: string) => string;
}) {
  const decide = useDecideSettlementAdjustment(projectId, settlementId, item.id);
  const confirmExecution = useConfirmSettlementAdjustmentExecution(projectId, settlementId, item.id);
  const [comment, setComment] = React.useState("");
  const [executionDate, setExecutionDate] = React.useState(todayLocal());
  const [executionKey] = React.useState(() => crypto.randomUUID());
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);

  const decideAdjustment = async (decision: "Approved" | "Rejected") => {
    if (!item.rowVersion || (decision === "Rejected" && !comment.trim())) return;
    setErrorMessage(null);
    try {
      await decide.mutateAsync({ decision, comment: comment.trim() || null, rowVersion: item.rowVersion });
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("projects.settlementAdjustmentDecisionFailed"));
    }
  };

  const confirmAdjustmentExecution = async () => {
    if (!executionDate) return;
    setErrorMessage(null);
    try {
      await confirmExecution.mutateAsync({ idempotencyKey: executionKey, executionDate });
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("projects.settlementAdjustmentExecutionFailed"));
    }
  };

  return <li className="rounded-md bg-muted/30 p-2">
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div>
        <p className="text-xs font-medium">{t(item.adjustmentType === "Refund" ? "projects.settlementAdjustmentRefund" : "projects.settlementAdjustmentCorrection")}</p>
        <p className="text-xs text-muted-foreground">{t("projects.settlementAdjustmentEffectiveDate")}: {formatDate(item.effectiveDate)} · {t("projects.settlementAdjustmentRecordedAt")}: {formatDateTime(item.recordedAt)}</p>
        <p className="mt-1 text-xs font-medium">{t(settlementAdjustmentStatusKey(item.status))}</p>
      </div>
      <span className="text-xs font-semibold tabular-nums"><bdi>{item.adjustmentType === "Refund" ? "-" : "+"}{formatCurrency(item.amount, item.currency)}</bdi></span>
    </div>
    <p className="mt-1 whitespace-pre-wrap text-xs text-muted-foreground">{item.reason}</p>
    {item.status === "Approved" ? <p className="mt-1 text-xs text-muted-foreground">{t("projects.settlementAdjustmentAwaitingExecution")}</p> : null}
    {item.status === "Executed" && item.executionDate ? <p className="mt-1 text-xs text-muted-foreground">{t("projects.settlementAdjustmentExecutionDate")}: {formatDate(item.executionDate)}</p> : null}
    {item.canDecide ? <div className="mt-3 space-y-2 rounded-md border p-2">
      <Label htmlFor={`settlement-adjustment-comment-${item.id}`}>{t("projects.settlementAdjustmentDecisionComment")}</Label>
      <Textarea id={`settlement-adjustment-comment-${item.id}`} value={comment} onChange={event => setComment(event.target.value)} maxLength={2000} rows={2} />
      <div className="flex flex-wrap gap-2">
        <Button size="sm" disabled={decide.isPending || !item.rowVersion} onClick={() => void decideAdjustment("Approved")}>{decide.isPending ? t("common.saving") : t("projects.settlementAdjustmentApprove")}</Button>
        <Button size="sm" variant="outline" disabled={decide.isPending || !item.rowVersion || !comment.trim()} onClick={() => void decideAdjustment("Rejected")}>{t("projects.settlementAdjustmentReject")}</Button>
      </div>
    </div> : null}
    {item.canConfirmExecution ? <div className="mt-3 flex flex-wrap items-end gap-2 rounded-md border p-2">
      <div className="space-y-1.5"><Label htmlFor={`settlement-adjustment-execution-date-${item.id}`}>{t("projects.settlementAdjustmentExecutionDate")}</Label>
        <Input id={`settlement-adjustment-execution-date-${item.id}`} type="date" max={todayLocal()} value={executionDate} onChange={event => setExecutionDate(event.target.value)} required /></div>
      <Button size="sm" disabled={confirmExecution.isPending || !executionDate} onClick={() => void confirmAdjustmentExecution()}>{confirmExecution.isPending ? t("common.saving") : t("projects.settlementAdjustmentConfirmExecution")}</Button>
    </div> : null}
    {errorMessage ? <p role="alert" className="mt-2 text-xs text-destructive">{errorMessage}</p> : null}
  </li>;
}

function SettlementAdjustmentHistory({ projectId, settlement, canRecord, t, formatCurrency, formatDate, formatDateTime }: {
  projectId: string;
  settlement: ProjectPaymentRequestSettlement;
  canRecord: boolean;
  t: (key: string, values?: Record<string, string | number>) => string;
  formatCurrency: (value: number, currency?: string) => string;
  formatDate: (value: string) => string;
  formatDateTime: (value: string) => string;
}) {
  const [expanded, setExpanded] = React.useState(false);
  const adjustments = useSettlementAdjustments(projectId, settlement.id, expanded);
  const recordRefund = useRecordSettlementAdjustment(projectId, "Refund");
  const recordCorrection = useRecordSettlementAdjustment(projectId, "Correction");
  const [activeType, setActiveType] = React.useState<"Refund" | "Correction" | null>(null);
  const approvers = useSettlementAdjustmentApprovers(projectId, expanded && Boolean(activeType));
  const [amount, setAmount] = React.useState("");
  const [effectiveDate, setEffectiveDate] = React.useState(todayLocal());
  const [reason, setReason] = React.useState("");
  const [assignedApproverId, setAssignedApproverId] = React.useState("");
  const [idempotencyKey, setIdempotencyKey] = React.useState("");
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);
  const refundTotal = (adjustments.data ?? []).filter(item => item.adjustmentType === "Refund" && item.status !== "Rejected")
    .reduce((sum, item) => sum + item.amount, 0);
  const remainingRefundable = Math.max(0, settlement.amount - refundTotal);
  const mutation = activeType === "Refund" ? recordRefund : recordCorrection;

  const openForm = (type: "Refund" | "Correction") => {
    setActiveType(type);
    setAmount("");
    setEffectiveDate(todayLocal());
    setReason("");
    setAssignedApproverId("");
    setIdempotencyKey(crypto.randomUUID());
    setErrorMessage(null);
  };

  const saveAdjustment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const numericAmount = Number(amount);
    if (!activeType || !isFourDecimalAmount(amount) || (activeType === "Refund" && numericAmount > remainingRefundable)) {
      setErrorMessage(t(activeType === "Refund" ? "projects.settlementRefundAmountInvalid" : "projects.settlementCorrectionAmountInvalid"));
      return;
    }
    if (!effectiveDate || !reason.trim() || !assignedApproverId) {
      setErrorMessage(t("projects.settlementAdjustmentFieldsRequired"));
      return;
    }
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        idempotencyKey,
        originalSettlementId: settlement.id,
        amount: numericAmount,
        effectiveDate,
        reason: reason.trim(),
        assignedApproverId,
      });
      setActiveType(null);
      setIdempotencyKey("");
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : t("projects.settlementAdjustmentSaveFailed"));
    }
  };

  return <div className="mt-3 space-y-3 border-t pt-3">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <div>
        <h4 className="text-xs font-semibold">{t("projects.settlementAdjustmentHistory")}</h4>
        <p className="text-xs text-muted-foreground">{t("projects.settlementAdjustmentOriginalReference", { reference: settlement.settlementReference })}</p>
      </div>
      <Button size="sm" variant="outline" aria-expanded={expanded} onClick={() => setExpanded(value => !value)}>{t(expanded ? "projects.settlementAdjustmentHide" : "projects.settlementAdjustmentShow")}</Button>
    </div>
    {expanded ? <>
    <div className="flex flex-wrap items-center justify-end gap-2">
      {canRecord && !activeType ? <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={() => openForm("Refund")} disabled={adjustments.isLoading || adjustments.isError || remainingRefundable <= 0}>
          <RotateCcw className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.settlementRecordRefund")}
        </Button>
        <Button size="sm" variant="outline" onClick={() => openForm("Correction")} disabled={adjustments.isLoading || adjustments.isError}>
          {t("projects.settlementRecordCorrection")}
        </Button>
      </div> : null}
    </div>
    {adjustments.isLoading ? <p role="status" className="text-xs text-muted-foreground">{t("common.loading")}</p>
      : adjustments.isError ? <p role="alert" className="text-xs text-destructive">{t("projects.settlementAdjustmentHistoryLoadFailed")}</p>
        : !adjustments.data?.length ? <p className="text-xs text-muted-foreground">{t("projects.settlementAdjustmentHistoryEmpty")}</p>
          : <ol className="space-y-2">{adjustments.data.map(item => <SettlementAdjustmentRow key={item.id} projectId={projectId} settlementId={settlement.id} item={item} t={t} formatCurrency={formatCurrency} formatDate={formatDate} formatDateTime={formatDateTime} />)}</ol>}
    {activeType ? <form onSubmit={event => void saveAdjustment(event)} className="space-y-3 rounded-md border p-3">
      <h4 className="text-sm font-medium">{t(activeType === "Refund" ? "projects.settlementRecordRefund" : "projects.settlementRecordCorrection")}</h4>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1.5"><Label htmlFor={`settlement-adjustment-amount-${settlement.id}`}>{t("projects.settlementAdjustmentAmount")}</Label>
          <Input id={`settlement-adjustment-amount-${settlement.id}`} type="number" min="0.0001" max={activeType === "Refund" ? remainingRefundable : undefined} step="0.0001" value={amount} onChange={event => { setAmount(event.target.value); setIdempotencyKey(crypto.randomUUID()); }} required />
          {activeType === "Refund" ? <p className="text-xs text-muted-foreground">{t("projects.settlementRefundRemaining")}: {formatCurrency(remainingRefundable, settlement.currency)}</p> : null}</div>
        <div className="space-y-1.5"><Label htmlFor={`settlement-adjustment-date-${settlement.id}`}>{t("projects.settlementAdjustmentEffectiveDate")}</Label>
          <Input id={`settlement-adjustment-date-${settlement.id}`} type="date" value={effectiveDate} onChange={event => { setEffectiveDate(event.target.value); setIdempotencyKey(crypto.randomUUID()); }} required /></div>
      </div>
      <div className="space-y-1.5"><Label htmlFor={`settlement-adjustment-reason-${settlement.id}`}>{t("projects.settlementAdjustmentReason")}</Label>
        <Textarea id={`settlement-adjustment-reason-${settlement.id}`} value={reason} onChange={event => { setReason(event.target.value); setIdempotencyKey(crypto.randomUUID()); }} maxLength={2000} rows={3} required /></div>
      <div className="space-y-1.5">
        <Label htmlFor={`settlement-adjustment-approver-${settlement.id}`}>{t("projects.settlementAdjustmentAssignedApprover")}</Label>
        {approvers.isLoading ? <p role="status" className="text-xs text-muted-foreground">{t("common.loading")}</p>
          : approvers.isError ? <p role="alert" className="text-xs text-destructive">{t("projects.settlementAdjustmentApproversLoadFailed")}</p>
            : !approvers.data?.length ? <p role="alert" className="text-xs text-destructive">{t("projects.settlementAdjustmentNoEligibleApprovers")}</p>
              : <Select value={assignedApproverId} onValueChange={setAssignedApproverId}>
                <SelectTrigger id={`settlement-adjustment-approver-${settlement.id}`} aria-label={t("projects.settlementAdjustmentAssignedApprover")}>
                  <SelectValue placeholder={t("projects.settlementAdjustmentSelectApprover")} />
                </SelectTrigger>
                <SelectContent>{approvers.data.map(approver => <SelectItem key={approver.userId} value={approver.userId}>{approver.displayName}</SelectItem>)}</SelectContent>
              </Select>}
      </div>
      <p className="text-xs text-muted-foreground">{t("projects.settlementAdjustmentApprovalAndExecutionHint")}</p>
      {errorMessage ? <p role="alert" className="text-sm text-destructive">{errorMessage}</p> : null}
      <div className="flex flex-wrap gap-2"><Button type="submit" size="sm" disabled={mutation.isPending || approvers.isLoading || approvers.isError || !approvers.data?.length || !assignedApproverId}>{mutation.isPending ? t("common.saving") : t("projects.settlementAdjustmentRequestApproval")}</Button>
        <Button type="button" size="sm" variant="outline" onClick={() => { setActiveType(null); setErrorMessage(null); }}>{t("common.cancel")}</Button></div>
    </form> : null}
    </> : null}
  </div>;
}

export function ProjectPaymentRequests({ projectId, canViewRequests, canReviewRequests, canReviewReversals, canCreateRequests, canCreatePlannedPayments, canEditRequests, canSubmitRequests, canCancelRequests, canApproveRequests, canRecordSettlements, canCreateReversals, canApproveReversals, section = "requests", active = true, initialInstallmentContext, onInstallmentContextConsumed }: {
  projectId: string;
  canViewRequests: boolean;
  canReviewRequests: boolean;
  canReviewReversals: boolean;
  canCreateRequests: boolean;
  canCreatePlannedPayments: boolean;
  canEditRequests: boolean;
  canSubmitRequests: boolean;
  canCancelRequests: boolean;
  canApproveRequests: boolean;
  canRecordSettlements: boolean;
  canCreateReversals: boolean;
  canApproveReversals: boolean;
  section?: "requests" | "settlements";
  active?: boolean;
  initialInstallmentContext?: PlannedPaymentRequestContext | null;
  onInstallmentContextConsumed?: () => void;
}) {
  const { t, formatCurrency, formatDate, formatDateTime } = useI18n();
  const confirm = useConfirm();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeSection, setActiveSection] = React.useState<"requests" | "settlements">(section);
  React.useEffect(() => setActiveSection(section), [section]);
  const linkedReversalId = searchParams.get("paymentReversalId");
  const requests = useProjectPaymentRequests(projectId, active && canViewRequests, activeSection === "settlements" ? "Approved" : undefined);
  const foundation = useProjectFinancialFoundation(projectId, active && canViewRequests);
  const createRequest = useCreateProjectPaymentRequest(projectId);
  const createQuickOneOff = useCreateQuickOneOffPaymentRequest(projectId);
  const updateRequest = useUpdateProjectPaymentRequest(projectId);
  const submitRequest = useSubmitProjectPaymentRequest(projectId);
  const decideRequest = useDecideProjectPaymentRequest(projectId);
  const cancelRequest = useCancelProjectPaymentRequest(projectId);
  const recordSettlement = useRecordProjectPaymentRequestSettlement(projectId);
  const createReversal = useCreateProjectPaymentReversal(projectId);
  const updateReversal = useUpdateProjectPaymentReversal(projectId);
  const submitReversal = useSubmitProjectPaymentReversal(projectId);
  const decideReversal = useDecideProjectPaymentReversal(projectId);
  const cancelReversal = useCancelProjectPaymentReversal(projectId);
  const [formOpen, setFormOpen] = React.useState(false);
  const [quickOneOff, setQuickOneOff] = React.useState(false);
  const [editing, setEditing] = React.useState<ProjectPaymentRequestListItem | null>(null);
  const [form, setForm] = React.useState<RequestForm>(emptyForm);
  const [formError, setFormError] = React.useState(false);
  const [quickOneOffError, setQuickOneOffError] = React.useState<string | null>(null);
  const [idempotencyKey, setIdempotencyKey] = React.useState("");
  const [installmentContext, setInstallmentContext] = React.useState<PlannedPaymentRequestContext | null>(null);
  const [selectedRequestId, setSelectedRequestId] = React.useState<string | null>(() => searchParams.get("paymentRequestId"));
  const [submitTarget, setSubmitTarget] = React.useState<ProjectPaymentRequestListItem | null>(null);
  const [reversalSubmitTarget, setReversalSubmitTarget] = React.useState<ProjectPaymentReversal | null>(null);
  const [submissionIdempotencyKey, setSubmissionIdempotencyKey] = React.useState("");
  const [approvalMode, setApprovalMode] = React.useState<ApprovalMode | "">("");
  const [selectedApproverIds, setSelectedApproverIds] = React.useState<string[]>([]);
  const [requirements, setRequirements] = React.useState<Record<string, "Mandatory" | "Optional">>({});
  const [submitError, setSubmitError] = React.useState<string | null>(null);
  const [comment, setComment] = React.useState("");
  const [actionError, setActionError] = React.useState(false);
  const [decisionValidation, setDecisionValidation] = React.useState<string | null>(null);
  const [settlementFormOpen, setSettlementFormOpen] = React.useState(false);
  const [settlementForm, setSettlementForm] = React.useState({ amount: "", paymentDate: todayLocal(), reference: "", description: "" });
  const [settlementIdempotencyKey, setSettlementIdempotencyKey] = React.useState("");
  const [settlementError, setSettlementError] = React.useState<string | null>(null);
  const [reversalFormOpen, setReversalFormOpen] = React.useState(false);
  const [editingReversal, setEditingReversal] = React.useState<ProjectPaymentReversal | null>(null);
  const [reversalForm, setReversalForm] = React.useState({ amount: "", reason: "" });
  const [reversalFormError, setReversalFormError] = React.useState<string | null>(null);
  const [reversalIdempotencyKey, setReversalIdempotencyKey] = React.useState("");
  const [reversalDecisionComment, setReversalDecisionComment] = React.useState("");
  const [reversalActionError, setReversalActionError] = React.useState<string | null>(null);
  const paymentApprovers = usePaymentRequestApprovers(projectId, active && Boolean((submitTarget || (formOpen && quickOneOff && canSubmitRequests)) && canSubmitRequests));
  const reversalApprovers = useProjectPaymentReversalApprovers(
    projectId, active && Boolean(reversalSubmitTarget && canCreateReversals));
  const approvers = reversalSubmitTarget ? reversalApprovers : paymentApprovers;
  const milestones = usePaymentRequestMilestones(projectId, active && Boolean(formOpen && (canCreateRequests || canEditRequests)));
  const fundingOptions = useProjectPaymentRequestFundingOptions(projectId, active && canViewRequests);
  const quickOneOffBudget = usePlannedPaymentBudget(projectId, active && Boolean(formOpen && quickOneOff && canCreateRequests && canCreatePlannedPayments));
  const detail = useProjectPaymentRequest(projectId, selectedRequestId, active && Boolean(selectedRequestId) && !linkedReversalId);
  const assignedReversalReview = useAssignedProjectPaymentReversalReview(
    projectId, selectedRequestId, linkedReversalId,
    active && canReviewReversals && Boolean(linkedReversalId));
  const settlementHistory = useProjectPaymentRequestSettlements(projectId, selectedRequestId,
    active && activeSection === "settlements" && Boolean(selectedRequestId));
  const reversalHistory = useProjectPaymentReversals(
    projectId, selectedRequestId, active && activeSection === "settlements" && Boolean(selectedRequestId && detail.data?.status === "Approved"));
  const editDetail = useProjectPaymentRequest(projectId, editing?.id ?? null, active && Boolean(formOpen && editing));

  React.useEffect(() => {
    if (!initialInstallmentContext) return;
    setInstallmentContext(initialInstallmentContext);
    setEditing(null);
    setQuickOneOff(false);
    setForm({
      title: t("projects.paymentRequestFromInstallmentTitle", { reference: initialInstallmentContext.planReference, sequence: initialInstallmentContext.installmentSequence }),
      amount: String(initialInstallmentContext.remainingRequestableAmount),
      reason: "",
      dueDate: initialInstallmentContext.dueDateUtc?.slice(0, 10) ?? todayLocal(),
      milestoneId: initialInstallmentContext.planActivityId ?? "",
      annualBudgetId: "",
      annualBudgetLineId: "",
    });
    setFormError(false);
    setIdempotencyKey(crypto.randomUUID());
    setFormOpen(true);
    onInstallmentContextConsumed?.();
  }, [initialInstallmentContext?.installmentId]);

  const closeRequestDetails = () => {
    setSelectedRequestId(null);
    setEditing(null);
    setSettlementFormOpen(false);
    setSettlementError(null);
    setReversalFormOpen(false);
    setReversalActionError(null);
    if (searchParams.has("paymentRequestId")) {
      setSearchParams(current => {
        const next = new URLSearchParams(current);
        next.delete("paymentRequestId");
        next.delete("paymentReversalId");
        return next;
      }, { replace: true });
    }
  };

  React.useEffect(() => {
    const linkedRequestId = searchParams.get("paymentRequestId");
    if (active && linkedRequestId && (canViewRequests || canReviewRequests || canReviewReversals)) setSelectedRequestId(linkedRequestId);
  }, [active, canReviewReversals, canReviewRequests, canViewRequests, searchParams]);

  React.useEffect(() => {
    if (!active) {
      setFormOpen(false);
      setQuickOneOff(false);
      setSelectedRequestId(null);
      setSubmitTarget(null);
      setReversalSubmitTarget(null);
      setSettlementFormOpen(false);
      setReversalFormOpen(false);
    }
  }, [active]);

  const openSettlementForm = () => {
    setSettlementForm({ amount: "", paymentDate: todayLocal(), reference: "", description: "" });
    setSettlementIdempotencyKey(crypto.randomUUID());
    setSettlementError(null);
    setSettlementFormOpen(true);
  };

  const saveSettlement = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const summary = settlementHistory.data?.summary;
    const amount = Number(settlementForm.amount);
    if (!selectedRequestId || !summary || !Number.isFinite(amount) || amount <= 0 || amount > summary.outstandingAmount) {
      setSettlementError(t("projects.paymentRequestSettlementAmountInvalid"));
      return;
    }
    if (!settlementForm.paymentDate || !settlementForm.reference.trim() || !settlementForm.description.trim()) {
      setSettlementError(t("projects.paymentRequestSettlementFieldsRequired"));
      return;
    }
    setSettlementError(null);
    try {
      await recordSettlement.mutateAsync({
        requestId: selectedRequestId,
        idempotencyKey: settlementIdempotencyKey,
        amount,
        paymentDate: settlementForm.paymentDate,
        paymentReference: settlementForm.reference.trim(),
        description: settlementForm.description.trim(),
      });
      setSettlementFormOpen(false);
      setSettlementIdempotencyKey("");
      setSettlementForm({ amount: "", paymentDate: todayLocal(), reference: "", description: "" });
    } catch (error) {
      setSettlementError(error instanceof ApiError ? error.message : t("projects.paymentRequestSettlementSaveFailed"));
    }
  };

  React.useEffect(() => {
    if (formOpen && editing && editDetail.data?.id === editing.id) {
      setForm({
        title: editDetail.data.title,
        amount: String(editDetail.data.amount),
        reason: editDetail.data.reason,
        dueDate: todayLocal(),
        milestoneId: editDetail.data.projectPlanMilestoneId ?? "",
        annualBudgetId: editDetail.data.annualBudgetId ?? "",
        annualBudgetLineId: editDetail.data.annualBudgetLineId ?? "",
      });
    }
  }, [formOpen, editing, editDetail.data]);

  const openEdit = (item: ProjectPaymentRequestListItem) => {
    setEditing(item);
    setQuickOneOff(false);
    setInstallmentContext(null);
    setForm({ title: item.title, amount: String(item.amount), reason: "", dueDate: todayLocal(), milestoneId: "", annualBudgetId: item.annualBudgetId ?? "", annualBudgetLineId: item.annualBudgetLineId ?? "" });
    setFormError(false);
    setFormOpen(true);
  };

  const openQuickOneOff = () => {
    setEditing(null);
    setInstallmentContext(null);
    setQuickOneOff(true);
    setForm(emptyForm());
    setFormError(false);
    setQuickOneOffError(null);
    setIdempotencyKey(crypto.randomUUID());
    const configured = foundation.data?.financeApprovalMode;
    setApprovalMode(configured === "Sequential" || configured === "Parallel" ? configured : "");
    setSelectedApproverIds([]);
    setRequirements({});
    setFormOpen(true);
  };

  const saveRequest = async (event: React.FormEvent<HTMLFormElement>) => {
    if (quickOneOff) {
      const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
      await saveQuickOneOff(event, submitter?.value === "submit");
      return;
    }
    event.preventDefault();
    setFormError(false);
    const amount = Number(form.amount);
    if (!form.title.trim() || !form.reason.trim() || !Number.isFinite(amount) || amount <= 0
      || !fundingOptions.data?.projectCurrency || !form.annualBudgetId || !form.annualBudgetLineId
      || (!editing && !installmentContext?.installmentId)
      || (installmentContext && amount > installmentContext.remainingRequestableAmount)
      || !hasFundingForAmount(form.annualBudgetId, form.annualBudgetLineId, amount)) {
      setFormError(true);
      return;
    }
    try {
      if (editing) {
        const current = editDetail.data?.id === editing.id ? editDetail.data : null;
        if (!current) { setFormError(true); return; }
        await updateRequest.mutateAsync({
          id: editing.id,
          title: form.title.trim(),
          amount,
          reason: form.reason.trim(),
          projectAnnualBudgetId: form.annualBudgetId,
          projectAnnualBudgetLineId: form.annualBudgetLineId,
          projectPlanMilestoneId: form.milestoneId || undefined,
          rowVersion: current.rowVersion,
        });
      } else {
        const installmentId = installmentContext?.installmentId;
        if (!installmentId) { setFormError(true); return; }
        await createRequest.mutateAsync({
          title: form.title.trim(),
          amount,
          reason: form.reason.trim(),
          plannedPaymentInstallmentId: installmentId,
          projectAnnualBudgetId: form.annualBudgetId,
          projectAnnualBudgetLineId: form.annualBudgetLineId,
          projectPlanMilestoneId: form.milestoneId || undefined,
          idempotencyKey,
        });
      }
      setFormOpen(false);
      setEditing(null);
      setInstallmentContext(null);
    } catch {
      setFormError(true);
    }
  };

  const openSubmission = (item: ProjectPaymentRequestListItem) => {
    setReversalSubmitTarget(null);
    setSubmitTarget(item);
    setSubmissionIdempotencyKey(crypto.randomUUID());
    const configured = foundation.data?.financeApprovalMode;
    setApprovalMode(configured === "Sequential" || configured === "Parallel" ? configured : "");
    setSelectedApproverIds([]);
    setRequirements({});
    setSubmitError(null);
  };

  const toggleApprover = (candidate: PaymentRequestApproverCandidate, checked: boolean) => {
    setSelectedApproverIds(current => checked
      ? current.includes(candidate.userId) ? current : [...current, candidate.userId]
      : current.filter(id => id !== candidate.userId));
    if (checked) setRequirements(current => ({ ...current, [candidate.userId]: current[candidate.userId] ?? "Mandatory" }));
  };

  const saveQuickOneOff = async (event: React.FormEvent<HTMLFormElement>, submitForApproval: boolean) => {
    event.preventDefault();
    setFormError(false);
    setQuickOneOffError(null);
    const amount = Number(form.amount);
    const validLifetimeBudget = Boolean(quickOneOffBudget.data
      && quickOneOffBudget.data.currency === foundation.data?.projectCurrency
      && quickOneOffBudget.data.canRegister
      && amount <= quickOneOffBudget.data.availableLifetimeBudget);
    const budget = fundingBudgets.find(item => item.id === form.annualBudgetId);
    const line = budget?.lines.find(item => item.id === form.annualBudgetLineId);
    const validAnnualAssignment = Boolean(budget && line && budget.currency === foundation.data?.projectCurrency);
    const validAnnualFunding = Boolean(form.annualBudgetId && form.annualBudgetLineId
      && hasFundingForAmount(form.annualBudgetId, form.annualBudgetLineId, amount));
    if (!form.title.trim() || !form.reason.trim() || !isFourDecimalAmount(form.amount) || !form.dueDate
      || !foundation.data?.projectCurrency || quickOneOffBudget.isLoading || quickOneOffBudget.isError
      || !validLifetimeBudget || !validAnnualAssignment
      || (submitForApproval && (!canSubmitRequests || !validAnnualFunding || !approvalMode || selectedApproverIds.length === 0))) {
      setFormError(true);
      if (!quickOneOffBudget.isLoading && !quickOneOffBudget.isError && quickOneOffBudget.data && amount > quickOneOffBudget.data.availableLifetimeBudget) {
        setQuickOneOffError(t("projects.quickOneOffInsufficientLifetimeBudget"));
      } else if (submitForApproval && !validAnnualFunding) {
        setQuickOneOffError(t("projects.paymentRequestInsufficientAnnualBudget"));
      } else if (submitForApproval && (!approvalMode || selectedApproverIds.length === 0)) {
        setQuickOneOffError(t("projects.paymentRequestSubmitFailed"));
      }
      return;
    }
    const assigned = selectedApproverIds.map((userId, index) => ({
      userId,
      position: approvalMode === "Sequential" ? index + 1 : null,
      requirement: approvalMode === "Sequential" ? "Mandatory" as const : requirements[userId] ?? "Mandatory" as const,
    }));
    if (submitForApproval && approvalMode === "Parallel" && !assigned.some(item => item.requirement === "Mandatory")) {
      setFormError(true);
      setQuickOneOffError(t("projects.paymentRequestSubmitFailed"));
      return;
    }
    try {
      const result = await createQuickOneOff.mutateAsync({
        description: form.title.trim(),
        amount,
        purpose: form.reason.trim(),
        dueDateUtc: `${form.dueDate}T00:00:00Z`,
        projectPlanMilestoneId: form.milestoneId || undefined,
        projectAnnualBudgetId: form.annualBudgetId || undefined,
        projectAnnualBudgetLineId: form.annualBudgetLineId || undefined,
        idempotencyKey,
        submitForApproval,
        ...(submitForApproval ? { approvalMode: approvalMode as ApprovalMode, approvers: assigned } : {}),
      });
      setFormOpen(false);
      setQuickOneOff(false);
      setForm(emptyForm());
      setIdempotencyKey("");
      setSelectedRequestId(result.paymentRequest.id);
    } catch (error) {
      setFormError(true);
      setQuickOneOffError(error instanceof ApiError ? error.message : t("projects.quickOneOffSaveFailed"));
    }
  };

  const submitForApproval = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if ((!submitTarget && !reversalSubmitTarget) || !approvalMode || selectedApproverIds.length === 0) { setSubmitError(t("projects.paymentRequestSubmitFailed")); return; }
    const assigned = selectedApproverIds.map((userId, index) => ({
      userId,
      position: approvalMode === "Sequential" ? index + 1 : null,
      requirement: approvalMode === "Sequential" ? "Mandatory" as const : requirements[userId] ?? "Mandatory" as const,
    }));
    if (approvalMode === "Parallel" && !assigned.some(item => item.requirement === "Mandatory")) { setSubmitError(t("projects.paymentRequestSubmitFailed")); return; }
    if (reversalSubmitTarget) {
      if (!selectedRequestId || !reversalSubmitTarget.canSubmit || reversalSubmitTarget.amount > reversalSubmitTarget.maximumReversibleAmount) {
        setSubmitError(t("projects.paymentReversalMaximumExceeded"));
        return;
      }
      try {
        await submitReversal.mutateAsync({
          requestId: selectedRequestId,
          reversalId: reversalSubmitTarget.id,
          approvalMode,
          approvers: assigned,
          rowVersion: reversalSubmitTarget.rowVersion,
        });
        setReversalSubmitTarget(null);
        setSubmitError(null);
      } catch (error) {
        setSubmitError(error instanceof ApiError ? error.message : t("projects.paymentReversalSubmitFailed"));
      }
      return;
    }
    if (!submitTarget) return;
    if (!hasFundingForAmount(submitTarget.annualBudgetId ?? "", submitTarget.annualBudgetLineId ?? "", submitTarget.amount)) {
      setSubmitError(t("projects.paymentRequestInsufficientAnnualBudget"));
      return;
    }
    try {
      await submitRequest.mutateAsync({ id: submitTarget.id, approvalMode, approvers: assigned,
        rowVersion: submitTarget.rowVersion, idempotencyKey: submissionIdempotencyKey });
      setSubmitTarget(null);
      setSelectedRequestId(submitTarget.id);
    } catch (error) {
      setSubmitError(error instanceof ApiError ? error.message : t("projects.paymentRequestSubmitFailed"));
    }
  };

  const decide = async (decision: "Approved" | "Rejected" | "ReturnedForRevision") => {
    if (!detail.data) return;
    setActionError(false);
    if (decision === "ReturnedForRevision" && !comment.trim()) {
      setDecisionValidation(t("projects.paymentRequestReturnReasonRequired"));
      return;
    }
    setDecisionValidation(null);
    try {
      await decideRequest.mutateAsync({ id: detail.data.id, decision, comment: comment.trim() || undefined, rowVersion: detail.data.rowVersion });
      setComment("");
      setDecisionValidation(null);
    } catch {
      setActionError(true);
    }
  };

  const cancel = async (item: ProjectPaymentRequestListItem) => {
    if (!await confirm({ description: t("projects.paymentRequestCancelConfirm"), destructive: true })) return;
    try {
      await cancelRequest.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
    } catch {
      setActionError(true);
      setSelectedRequestId(item.id);
    }
  };

  const openReversalForm = (item?: ProjectPaymentReversal) => {
    setEditingReversal(item ?? null);
    setReversalForm(item ? { amount: String(item.amount), reason: item.reason } : { amount: "", reason: "" });
    setReversalIdempotencyKey(crypto.randomUUID());
    setReversalFormError(null);
    setReversalFormOpen(true);
  };

  const saveReversalDraft = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const page = reversalHistory.data;
    const amount = Number(reversalForm.amount);
    if (!selectedRequestId || !page || !Number.isFinite(amount) || amount <= 0
      || amount > page.maximumReversibleAmount || !reversalForm.reason.trim()) {
      setReversalFormError(t("projects.paymentReversalMaximumExceeded"));
      return;
    }
    try {
      if (editingReversal) {
        await updateReversal.mutateAsync({
          requestId: selectedRequestId, reversalId: editingReversal.id, amount,
          reason: reversalForm.reason.trim(), rowVersion: editingReversal.rowVersion,
        });
      } else {
        await createReversal.mutateAsync({
          requestId: selectedRequestId, amount, reason: reversalForm.reason.trim(), idempotencyKey: reversalIdempotencyKey,
        });
      }
      setReversalFormOpen(false);
      setEditingReversal(null);
      setReversalForm({ amount: "", reason: "" });
      setReversalFormError(null);
    } catch (error) {
      setReversalFormError(error instanceof ApiError ? error.message : t("projects.paymentReversalSaveFailed"));
    }
  };

  const openReversalSubmission = (item: ProjectPaymentReversal) => {
    setSubmitTarget(null);
    setReversalSubmitTarget(item);
    const configured = foundation.data?.financeApprovalMode;
    setApprovalMode(configured === "Sequential" || configured === "Parallel" ? configured : "");
    setSelectedApproverIds([]);
    setRequirements({});
    setSubmitError(null);
  };

  const decideReversalAction = async (item: ProjectPaymentReversal, decision: "Approved" | "Rejected") => {
    if (!selectedRequestId) return;
    setReversalActionError(null);
    try {
      await decideReversal.mutateAsync({
        requestId: selectedRequestId, reversalId: item.id, decision,
        comment: reversalDecisionComment.trim() || undefined, rowVersion: item.rowVersion,
      });
      setReversalDecisionComment("");
      if (linkedReversalId) closeRequestDetails();
    } catch (error) {
      setReversalActionError(error instanceof ApiError ? error.message : t("projects.paymentReversalDecisionFailed"));
    }
  };

  const cancelReversalDraft = async (item: ProjectPaymentReversal) => {
    if (!selectedRequestId || !await confirm({ description: t("projects.paymentReversalCancelConfirm"), destructive: true })) return;
    try {
      await cancelReversal.mutateAsync({ requestId: selectedRequestId, reversalId: item.id, rowVersion: item.rowVersion });
    } catch (error) {
      setReversalActionError(error instanceof ApiError ? error.message : t("projects.paymentReversalActionFailed"));
    }
  };

  const isSaving = createRequest.isPending || createQuickOneOff.isPending || updateRequest.isPending || (Boolean(editing) && editDetail.isLoading);
  const isSavingReversal = createReversal.isPending || updateReversal.isPending;
  const configMode = foundation.data?.financeApprovalMode;
  const budgetAvailability = requests.data?.budgetAvailability;
  const fundingBudgets = fundingOptions.data?.budgets ?? [];
  const selectedFundingBudget = fundingBudgets.find(item => item.id === form.annualBudgetId) ?? null;
  const selectedFundingLine = selectedFundingBudget?.lines.find(item => item.id === form.annualBudgetLineId) ?? null;
  const quickOneOffAmount = isFourDecimalAmount(form.amount) ? Number(form.amount) : null;
  const quickOneOffAvailableLifetime = quickOneOffBudget.data?.availableLifetimeBudget ?? null;
  const quickOneOffAvailableAfter = quickOneOffAmount !== null && quickOneOffAvailableLifetime !== null
    ? quickOneOffAvailableLifetime - quickOneOffAmount
    : null;
  const hasFundingForAmount = (annualBudgetId: string, annualBudgetLineId: string, amount: number) => {
    const budget = fundingBudgets.find(item => item.id === annualBudgetId);
    const line = budget?.lines.find(item => item.id === annualBudgetLineId);
    return Boolean(budget && budget.status === "Approved" && budget.currency === fundingOptions.data?.projectCurrency && budget.availableAmount >= amount
      && line && line.availableAmount >= amount);
  };
  const canSubmitFromFunding = (item: ProjectPaymentRequestListItem) =>
    Boolean(item.canSubmit && hasFundingForAmount(item.annualBudgetId ?? "", item.annualBudgetLineId ?? "", item.amount));
  const availabilityMessageKey: Record<string, string> = {
    AnnualBudgetMissing: "projects.paymentRequestAnnualBudgetMissing",
    AnnualBudgetDraft: "projects.paymentRequestAnnualBudgetDraft",
    LegacyReconciliationRequired: "projects.paymentRequestLegacyReconciliation",
    CurrencyOrPeriodMismatch: "projects.paymentRequestBudgetMismatch",
    NoAvailableBalance: "projects.paymentRequestNoAvailableBalance",
  };
  const budgetMessageKey = budgetAvailability ? availabilityMessageKey[budgetAvailability.availabilityStatus] : undefined;
  const canRecordSettlementHere = canRecordSettlements && foundation.data?.isCurrentProjectManager === true;

  return <>
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 pb-3">
        <div><CardTitle className="text-base">{t(activeSection === "requests" ? "projects.paymentRequestsTitle" : "projects.financeWorkspaceSettlements")}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{t(activeSection === "requests" ? "projects.paymentRequestsDescription" : "projects.financeSettlementWorkspaceDescription")}</p>
        </div>
        <div className="flex flex-wrap items-center justify-end gap-2">
          {activeSection === "requests" && canCreateRequests && canCreatePlannedPayments && foundation.data?.isCurrentProjectManager === true ? <Button size="sm" onClick={openQuickOneOff}><Receipt className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.quickOneOffCreate")}</Button> : null}
          <div role="group" aria-label={t("projects.paymentRequestWorkspaceNavigation")} className="flex shrink-0 gap-2">
            <Button aria-pressed={activeSection === "requests"} size="sm" variant={activeSection === "requests" ? "default" : "outline"} onClick={() => { setActiveSection("requests"); closeRequestDetails(); }}>{t("projects.paymentRequestWorkspaceRequests")}</Button>
            <Button aria-pressed={activeSection === "settlements"} size="sm" variant={activeSection === "settlements" ? "default" : "outline"} onClick={() => { setActiveSection("settlements"); closeRequestDetails(); }}>{t("projects.paymentRequestWorkspaceSettlements")}</Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("projects.paymentRequestActionFailed")}</p> : null}
        {activeSection === "requests" && canCreateRequests && foundation.data?.isCurrentProjectManager === false ? <p role="status" className="rounded-md border p-3 text-xs text-muted-foreground">{t("projects.paymentRequestCurrentManagerOnly")}</p> : null}
        {activeSection === "requests" && canViewRequests && budgetAvailability ? <section className="space-y-3 rounded-lg border bg-muted/20 p-4" aria-label={t("projects.paymentRequestAnnualBudget")}>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h3 className="text-sm font-semibold">{t("projects.paymentRequestAnnualBudget")}</h3>
              <p className="mt-1 text-xs text-muted-foreground">{t("projects.fiscalYearShort")} {budgetAvailability.fiscalYear} · {formatDate(budgetAvailability.fiscalYearStartDate)} – {formatDate(budgetAvailability.fiscalYearEndDate)}</p>
              {budgetAvailability.annualBudgetReference ? <p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetReference")}: {budgetAvailability.annualBudgetReference}</p> : null}
            </div>
            {budgetAvailability.annualBudgetStatus ? <Badge status={budgetAvailability.annualBudgetStatus}>{t(budgetAvailability.annualBudgetStatus === "Approved" ? "projects.annualBudgetApproved" : "projects.annualBudgetDraft")}</Badge> : null}
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <BudgetMetric label={t("projects.paymentRequestAnnualApprovedBudget")} value={budgetAvailability.approvedAnnualBudget === null ? "—" : formatCurrency(budgetAvailability.approvedAnnualBudget, budgetAvailability.currency)} />
            <BudgetMetric label={t("projects.paymentRequestAnnualReservations")} value={formatCurrency(budgetAvailability.approvedReservations, budgetAvailability.currency)} />
            <BudgetMetric label={t("projects.paymentRequestAnnualAvailable")} value={budgetAvailability.availableAmount === null ? "—" : formatCurrency(budgetAvailability.availableAmount, budgetAvailability.currency)} strong />
          </div>
          {budgetMessageKey ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t(budgetMessageKey)}</p> : null}
        </section> : null}
        {activeSection === "settlements" ? <section className="space-y-3 rounded-lg border bg-muted/20 p-4" aria-label={t("projects.actualPaid")}>
          <h3 className="text-sm font-semibold">{t("projects.settlementActualPaidSummary")}</h3>
          <div className="grid gap-3 sm:grid-cols-2">
            <BudgetMetric label={t("projects.grossActualPaid")} value={foundation.isLoading ? t("common.loading") : foundation.data?.grossActualPaid == null ? "-" : formatCurrency(foundation.data.grossActualPaid, foundation.data.projectCurrency ?? undefined)} />
            <BudgetMetric label={t("projects.netActualPaid")} value={foundation.isLoading ? t("common.loading") : foundation.data?.netActualPaid == null ? "-" : formatCurrency(foundation.data.netActualPaid, foundation.data.projectCurrency ?? undefined)} strong />
          </div>
        </section> : null}
        {activeSection === "settlements" && canRecordSettlements && (foundation.isError || foundation.data?.isCurrentProjectManager === false)
          ? <p role="status" className="rounded-md border p-3 text-xs text-muted-foreground">{t(foundation.isError ? "projects.paymentSettlementCurrentManagerUnavailable" : "projects.paymentSettlementCurrentManagerOnly")}</p>
          : null}
        {!canViewRequests ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("projects.paymentRequestAssignedReviewOnly")}</p>
          : requests.isLoading ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : requests.isError ? <div className="flex items-center justify-between gap-3 rounded-md border p-4"><p role="alert" className="text-sm text-destructive">{requests.error instanceof ApiError && requests.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.paymentRequestLoadFailed")}</p><Button size="sm" variant="outline" onClick={() => void requests.refetch()}>{t("common.retry")}</Button></div>
            : !requests.data?.items.length ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("projects.paymentRequestEmpty")}</p>
              : <ul className="divide-y">
                {requests.data.items.map(item => <li key={item.id} className="flex flex-col gap-3 py-3 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2"><p className="font-medium">{item.title}</p><Badge status={item.status}>{statusText(t, item.status)}</Badge></div>
                    <p className="text-xs text-muted-foreground">{item.reference} · {item.requesterName || t("projects.paymentRequestUnknownRequester")}</p>
                    <p className="text-xs text-muted-foreground">{t("projects.createdAt")}: {formatDateTime(item.createdAtUtc)}{item.submittedAtUtc ? ` · ${t("projects.paymentRequestSubmittedAt")}: ${formatDateTime(item.submittedAtUtc)}` : ""}{item.completedAtUtc ? ` · ${t("projects.paymentRequestCompletedAt")}: ${formatDateTime(item.completedAtUtc)}` : ""}</p>
                    {item.annualBudgetFiscalYear ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestFiscalYear")}: {t("projects.fiscalYearShort")} {item.annualBudgetFiscalYear}{item.annualBudgetReference ? ` · ${item.annualBudgetReference}` : ""}</p> : null}
                    <p className="font-semibold tabular-nums">{formatCurrency(item.amount, item.currency)}</p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button size="sm" variant="outline" onClick={() => { setSelectedRequestId(item.id); setActionError(false); }}><Eye className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestDetails")}</Button>
                    {activeSection === "requests" && canEditRequests && item.canEdit ? <Button size="sm" variant="outline" onClick={() => openEdit(item)}><Pencil className="me-1.5 h-4 w-4" aria-hidden="true" />{t("common.edit")}</Button> : null}
                     {activeSection === "requests" && canSubmitRequests && item.canSubmit ? <Button size="sm" onClick={() => openSubmission(item)} disabled={!canSubmitFromFunding(item)}><Send className="me-1.5 h-4 w-4" aria-hidden="true" />{item.status === "ReturnedForRevision" ? t("projects.paymentRequestResubmit") : t("projects.paymentRequestSubmit")}</Button> : null}
                    {activeSection === "requests" && canCancelRequests && item.canCancel ? <Button size="sm" variant="outline" onClick={() => void cancel(item)} disabled={cancelRequest.isPending}>{t("projects.paymentRequestCancel")}</Button> : null}
                  </div>
                </li>)}
              </ul>}
        {canViewRequests && requests.data ? <p className="border-t pt-3 text-xs text-muted-foreground">{t("projects.totalRecords", { count: requests.data.total })}</p> : null}
      </CardContent>
    </Card>

    <SideSheet open={formOpen} onOpenChange={open => { setFormOpen(open); if (!open) { setEditing(null); setInstallmentContext(null); setQuickOneOff(false); } }} title={quickOneOff ? t("projects.quickOneOffTitle") : editing ? t("projects.paymentRequestEdit") : t("projects.paymentRequestCreate")} description={quickOneOff ? t("projects.quickOneOffDescription") : t("projects.paymentRequestFormDescription")}>
      <form onSubmit={event => void saveRequest(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {installmentContext ? <div className="space-y-1 rounded-md border bg-muted/20 p-3 text-sm">
            <p className="font-medium">{t("projects.paymentRequestInstallment")}: {installmentContext.planReference} · {t("projects.plannedPaymentInstallmentNumber")} {installmentContext.installmentSequence}</p>
            <p className="text-xs text-muted-foreground">{t("projects.paymentRequestInstallmentScheduled")}: {formatCurrency(installmentContext.installmentAmount, installmentContext.currency)}</p>
            <p className="text-xs text-muted-foreground">{t("projects.paymentRequestInstallmentRemaining")}: {formatCurrency(installmentContext.remainingRequestableAmount, installmentContext.currency)}</p>
          </div> : editing ? <p role="status" className="rounded-md border p-3 text-xs text-muted-foreground">{editDetail.data?.plannedPaymentInstallmentId ? t("projects.paymentRequestInstallmentLinked") : t("projects.paymentRequestLegacyWithoutInstallment")}</p> : null}
          <div><Label htmlFor="payment-request-title">{t(quickOneOff ? "projects.quickOneOffDescriptionLabel" : "projects.paymentRequestTitleLabel")}</Label><Input id="payment-request-title" value={form.title} onChange={event => setForm(current => ({ ...current, title: event.target.value }))} required maxLength={200} /></div>
          <div><Label htmlFor="payment-request-amount">{t("projects.amount")}</Label><Input id="payment-request-amount" type="number" min="0.0001" max={installmentContext?.remainingRequestableAmount} step="0.0001" value={form.amount} onChange={event => setForm(current => ({ ...current, amount: event.target.value }))} required />
            <p className="mt-1 text-xs text-muted-foreground">{quickOneOff ? foundation.data?.projectCurrency ?? t("projects.financialCurrencyUnavailable") : fundingOptions.data?.projectCurrency ?? t("projects.financialCurrencyUnavailable")}</p>
            {installmentContext ? <p className="mt-1 text-xs text-muted-foreground">{t("projects.paymentRequestInstallmentMaximum")}: {formatCurrency(installmentContext.remainingRequestableAmount, installmentContext.currency)}</p> : null}
          </div>
          {quickOneOff ? <div><Label htmlFor="quick-one-off-due-date">{t("projects.quickOneOffDueDate")}</Label><Input id="quick-one-off-due-date" type="date" value={form.dueDate} onChange={event => setForm(current => ({ ...current, dueDate: event.target.value }))} required /></div> : null}
          <div><Label htmlFor="payment-request-reason">{t(quickOneOff ? "projects.quickOneOffPurpose" : "projects.paymentRequestReason")}</Label><Textarea id="payment-request-reason" value={form.reason} onChange={event => setForm(current => ({ ...current, reason: event.target.value }))} rows={5} required maxLength={2000} /></div>
          <div><Label htmlFor="payment-request-milestone">{t("projects.paymentRequestMilestone")}</Label>
            <select id="payment-request-milestone" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm" value={form.milestoneId} onChange={event => setForm(current => ({ ...current, milestoneId: event.target.value }))}>
              <option value="">{t("projects.paymentRequestNoMilestone")}</option>
              {(milestones.data ?? []).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            {milestones.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("projects.paymentRequestMilestonesLoadFailed")}</p> : null}
          </div>
          {quickOneOff ? <>
            <section className="space-y-3 rounded-md border p-3" aria-label={t("projects.quickOneOffLifetimeBudget")}>
              <h3 className="text-sm font-semibold">{t("projects.quickOneOffLifetimeBudget")}</h3>
              {quickOneOffBudget.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
                : quickOneOffBudget.isError ? <div className="flex items-center justify-between gap-3"><p role="alert" className="text-xs text-destructive">{t("projects.quickOneOffLifetimeBudgetUnavailable")}</p><Button type="button" size="sm" variant="outline" onClick={() => void quickOneOffBudget.refetch()}>{t("common.retry")}</Button></div>
                  : !quickOneOffBudget.data || quickOneOffBudget.data.currency !== foundation.data?.projectCurrency ? <p role="status" className="text-xs text-muted-foreground">{t("projects.quickOneOffLifetimeBudgetUnavailable")}</p>
                    : <>
                      <div className="grid gap-2 sm:grid-cols-3">
                        <BudgetMetric label={t("projects.quickOneOffLifetimeAvailable")} value={formatCurrency(quickOneOffBudget.data.availableLifetimeBudget, quickOneOffBudget.data.currency)} strong />
                        <BudgetMetric label={t("projects.quickOneOffReservationImpact")} value={quickOneOffAmount === null ? "-" : formatCurrency(quickOneOffAmount, quickOneOffBudget.data.currency)} />
                        <BudgetMetric label={t("projects.quickOneOffEstimatedAvailableAfter")} value={quickOneOffAvailableAfter === null ? "-" : formatCurrency(quickOneOffAvailableAfter, quickOneOffBudget.data.currency)} />
                      </div>
                      <p className="text-xs text-muted-foreground">{t("projects.quickOneOffLifetimeReservationNote")}</p>
                      {quickOneOffAmount !== null && quickOneOffAmount > quickOneOffBudget.data.availableLifetimeBudget ? <p role="alert" className="text-xs text-destructive">{t("projects.quickOneOffInsufficientLifetimeBudget")}</p> : null}
                    </>}
            </section>
            <p className="rounded-md border bg-muted/20 p-3 text-xs text-muted-foreground">{t("projects.quickOneOffDraftFundingNote")}</p>
          </> : null}
          <section className="space-y-3 rounded-md border p-3" aria-label={t("projects.paymentRequestFunding") }>
            <h3 className="text-sm font-semibold">{t("projects.paymentRequestFunding")}</h3>
            {fundingOptions.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
              : fundingOptions.isError ? <div className="flex items-center justify-between gap-3"><p role="alert" className="text-xs text-destructive">{t("projects.paymentRequestFundingOptionsLoadFailed")}</p><Button type="button" size="sm" variant="outline" onClick={() => void fundingOptions.refetch()}>{t("common.retry")}</Button></div>
                : fundingBudgets.length === 0 ? <p role="status" className="text-xs text-muted-foreground">{t("projects.paymentRequestNoEligibleAnnualBudget")}</p>
                  : <>
                    <div><Label htmlFor="payment-request-annual-budget">{t("projects.paymentRequestAnnualBudget")}</Label>
                      <select id="payment-request-annual-budget" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm" value={form.annualBudgetId} onChange={event => setForm(current => ({ ...current, annualBudgetId: event.target.value, annualBudgetLineId: "" }))} required>
                        <option value="">{t("projects.paymentRequestChooseAnnualBudget")}</option>
                        {fundingBudgets.map(budget => <option key={budget.id} value={budget.id}>{budget.reference} · {t("projects.fiscalYearShort")} {budget.fiscalYear} · {formatCurrency(budget.availableAmount, budget.currency)} {t("projects.paymentRequestAvailable")}</option>)}
                      </select>
                    </div>
                    <div><Label htmlFor="payment-request-annual-budget-line">{t("projects.paymentRequestBudgetLine")}</Label>
                      <select id="payment-request-annual-budget-line" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm" value={form.annualBudgetLineId} onChange={event => setForm(current => ({ ...current, annualBudgetLineId: event.target.value }))} required disabled={!selectedFundingBudget}>
                        <option value="">{t("projects.paymentRequestChooseBudgetLine")}</option>
                        {selectedFundingBudget ? selectedFundingBudget.lines.map(line => <option key={line.id} value={line.id}>{line.reference}{line.description ? ` · ${line.description}` : ""} · {formatCurrency(line.availableAmount, selectedFundingBudget.currency)} {t("projects.paymentRequestAvailable")}</option>) : null}
                      </select>
                    </div>
                    {selectedFundingBudget && selectedFundingBudget.lines.length === 0 ? <p role="status" className="text-xs text-muted-foreground">{t("projects.paymentRequestNoEligibleBudgetLine")}</p> : null}
                    {selectedFundingBudget ? <div className="grid gap-2 sm:grid-cols-3">
                      <BudgetMetric label={t("projects.paymentRequestAnnualApprovedBudget")} value={formatCurrency(selectedFundingBudget.annualBudgetAmount, selectedFundingBudget.currency)} />
                      <BudgetMetric label={t("projects.paymentRequestAnnualReservations")} value={formatCurrency(selectedFundingBudget.reservedAmount, selectedFundingBudget.currency)} />
                      <BudgetMetric label={t("projects.paymentRequestAnnualAvailable")} value={formatCurrency(selectedFundingBudget.availableAmount, selectedFundingBudget.currency)} strong />
                    </div> : null}
                    {selectedFundingLine && selectedFundingBudget ? <div className="grid gap-2 sm:grid-cols-3">
                      <BudgetMetric label={t("projects.paymentRequestBudgetLineLimit")} value={formatCurrency(selectedFundingLine.authorizedLimit, selectedFundingBudget.currency)} />
                      <BudgetMetric label={t("projects.paymentRequestBudgetLineReservations")} value={formatCurrency(selectedFundingLine.reservedAmount, selectedFundingBudget.currency)} />
                      <BudgetMetric label={t("projects.paymentRequestBudgetLineAvailable")} value={formatCurrency(selectedFundingLine.availableAmount, selectedFundingBudget.currency)} strong />
                    </div> : null}
                  </>}
          </section>
          {quickOneOff && canSubmitRequests ? <section className="space-y-3 rounded-md border p-3" aria-label={t("projects.paymentRequestApprovalMethod")}>
            <div>
              <Label htmlFor="quick-one-off-approval-mode">{t("projects.paymentRequestApprovalMethod")}</Label>
              <select id="quick-one-off-approval-mode" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm" value={approvalMode} onChange={event => setApprovalMode(event.target.value as ApprovalMode | "")} required>
                <option value="">{t("projects.paymentRequestChooseApprovalMethod")}</option>
                {(configMode === "Both" || configMode === "Sequential") ? <option value="Sequential">{t("projects.financeApprovalSequential")}</option> : null}
                {(configMode === "Both" || configMode === "Parallel") ? <option value="Parallel">{t("projects.financeApprovalParallel")}</option> : null}
              </select>
              {!configMode ? <p role="alert" className="mt-1 text-xs text-destructive">{t("projects.paymentRequestApprovalModeUnconfigured")}</p> : null}
              <p className="mt-1 text-xs text-muted-foreground">{t("projects.quickOneOffApproversSubmitOnly")}</p>
            </div>
            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">{t("projects.paymentRequestSelectApprovers")}</legend>
              {paymentApprovers.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
                : paymentApprovers.isError ? <p role="alert" className="text-sm text-destructive">{t("projects.paymentRequestApproversLoadFailed")}</p>
                  : (paymentApprovers.data ?? []).length === 0 ? <p role="status" className="text-sm text-muted-foreground">{t("projects.paymentRequestNoEligibleApprovers")}</p>
                    : <ul className="space-y-2">{(paymentApprovers.data ?? []).map(candidate => {
                      const checked = selectedApproverIds.includes(candidate.userId);
                      return <li key={candidate.userId} className="rounded-md border p-3">
                        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={checked} onChange={event => toggleApprover(candidate, event.target.checked)} /><span>{candidate.displayName}</span>{checked && approvalMode === "Sequential" ? <span className="ms-auto text-xs text-muted-foreground">{t("projects.paymentRequestSequenceStep", { step: selectedApproverIds.indexOf(candidate.userId) + 1 })}</span> : null}</label>
                        {checked && approvalMode === "Parallel" ? <div className="mt-2"><Label htmlFor={`quick-one-off-requirement-${candidate.userId}`}>{t("projects.paymentRequestRequirement")}</Label><select id={`quick-one-off-requirement-${candidate.userId}`} className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm" value={requirements[candidate.userId] ?? "Mandatory"} onChange={event => setRequirements(current => ({ ...current, [candidate.userId]: event.target.value as "Mandatory" | "Optional" }))}><option value="Mandatory">{t("projects.paymentRequestMandatory")}</option><option value="Optional">{t("projects.paymentRequestOptional")}</option></select></div> : null}
                      </li>;
                    })}</ul>}
              {approvalMode === "Parallel" ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestMandatoryRule")}</p> : null}
            </fieldset>
          </section> : null}
          {formError ? <p role="alert" className="text-sm text-destructive">{quickOneOffError ?? t("projects.paymentRequestSaveFailed")}</p> : null}
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => { setFormOpen(false); setEditing(null); setInstallmentContext(null); setQuickOneOff(false); }}>{t("common.cancel")}</Button>
          {quickOneOff ? <>
            <Button type="submit" value="draft" disabled={isSaving || quickOneOffBudget.isLoading || quickOneOffBudget.isError || !foundation.data?.projectCurrency}>{isSaving ? t("common.saving") : t("projects.quickOneOffSaveDraft")}</Button>
            {canSubmitRequests ? <Button type="submit" value="submit" disabled={isSaving || quickOneOffBudget.isLoading || quickOneOffBudget.isError || !foundation.data?.projectCurrency}>{isSaving ? t("common.saving") : t("projects.quickOneOffSubmit")}</Button> : null}
          </> : <Button type="submit" disabled={isSaving || fundingOptions.isLoading || fundingOptions.isError || !fundingOptions.data?.projectCurrency || !form.annualBudgetId || !form.annualBudgetLineId}>{isSaving ? t("common.saving") : t("common.save")}</Button>}
        </SideSheetFooter>
      </form>
    </SideSheet>

    <SideSheet open={Boolean(submitTarget || reversalSubmitTarget)} onOpenChange={open => { if (!open) { setSubmitTarget(null); setReversalSubmitTarget(null); } }} title={t("projects.paymentRequestSubmitTitle")} description={t("projects.paymentRequestSubmitDescription")}>
      <form onSubmit={event => void submitForApproval(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {reversalSubmitTarget && reversalHistory.data
            ? <div className="rounded-md border px-3 py-2 text-sm"><p className="font-medium">{t("projects.paymentReversalTitle")} · {reversalSubmitTarget.reference}</p><p className="mt-1 text-xs text-muted-foreground">{t("projects.paymentReversalMaximum")}: {formatCurrency(reversalHistory.data.maximumReversibleAmount, reversalHistory.data.currency)}</p>{reversalHistory.data.annualBudgetFiscalYear ? <p className="mt-1 text-xs text-muted-foreground">{t("projects.paymentRequestFiscalYear")}: {t("projects.fiscalYearShort")} {reversalHistory.data.annualBudgetFiscalYear}</p> : null}</div>
            : budgetAvailability ? <div className="rounded-md border px-3 py-2 text-sm"><p className="font-medium">{t("projects.fiscalYearShort")} {budgetAvailability.fiscalYear} · {budgetAvailability.annualBudgetReference ?? t("projects.paymentRequestAnnualBudget")}</p><p className="mt-1 text-xs text-muted-foreground">{t("projects.paymentRequestAnnualAvailable")}: {budgetAvailability.availableAmount === null ? "-" : formatCurrency(budgetAvailability.availableAmount, budgetAvailability.currency)}</p>{budgetMessageKey ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t(budgetMessageKey)}</p> : null}</div> : null}
          <div><Label htmlFor="payment-request-approval-mode">{t("projects.paymentRequestApprovalMethod")}</Label>
            <select id="payment-request-approval-mode" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm" value={approvalMode} onChange={event => setApprovalMode(event.target.value as ApprovalMode | "")} required disabled={configMode !== "Both"}>
              <option value="">{t("projects.paymentRequestChooseApprovalMethod")}</option>
              {(configMode === "Both" || configMode === "Sequential") ? <option value="Sequential">{t("projects.financeApprovalSequential")}</option> : null}
              {(configMode === "Both" || configMode === "Parallel") ? <option value="Parallel">{t("projects.financeApprovalParallel")}</option> : null}
            </select>
            {!configMode ? <p role="alert" className="mt-1 text-xs text-destructive">{t("projects.paymentRequestApprovalModeUnconfigured")}</p> : null}
          </div>
          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">{t("projects.paymentRequestSelectApprovers")}</legend>
            {approvers.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
              : approvers.isError ? <p role="alert" className="text-sm text-destructive">{t("projects.paymentRequestApproversLoadFailed")}</p>
                : (approvers.data ?? []).length === 0 ? <p role="status" className="text-sm text-muted-foreground">{t("projects.paymentRequestNoEligibleApprovers")}</p>
                  : <ul className="space-y-2">{(approvers.data ?? []).map(candidate => {
                    const checked = selectedApproverIds.includes(candidate.userId);
                    return <li key={candidate.userId} className="rounded-md border p-3">
                      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={checked} onChange={event => toggleApprover(candidate, event.target.checked)} /><span>{candidate.displayName}</span>{checked && approvalMode === "Sequential" ? <span className="ms-auto text-xs text-muted-foreground">{t("projects.paymentRequestSequenceStep", { step: selectedApproverIds.indexOf(candidate.userId) + 1 })}</span> : null}</label>
                      {checked && approvalMode === "Parallel" ? <div className="mt-2"><Label htmlFor={`payment-request-requirement-${candidate.userId}`}>{t("projects.paymentRequestRequirement")}</Label><select id={`payment-request-requirement-${candidate.userId}`} className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm" value={requirements[candidate.userId] ?? "Mandatory"} onChange={event => setRequirements(current => ({ ...current, [candidate.userId]: event.target.value as "Mandatory" | "Optional" }))}><option value="Mandatory">{t("projects.paymentRequestMandatory")}</option><option value="Optional">{t("projects.paymentRequestOptional")}</option></select></div> : null}
                    </li>;
                  })}</ul>}
            {approvalMode === "Parallel" ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestMandatoryRule")}</p> : null}
          </fieldset>
          {submitError ? <p role="alert" className="text-sm text-destructive">{submitError}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => { setSubmitTarget(null); setReversalSubmitTarget(null); }}>{t("common.cancel")}</Button><Button type="submit" disabled={submitRequest.isPending || submitReversal.isPending || approvers.isLoading || !configMode || (reversalSubmitTarget ? !reversalSubmitTarget.canSubmit : !submitTarget || !canSubmitFromFunding(submitTarget))}>{submitRequest.isPending || submitReversal.isPending ? t("common.saving") : t(submitTarget?.status === "ReturnedForRevision" ? "projects.paymentRequestResubmit" : "projects.paymentRequestSubmit")}</Button></SideSheetFooter>
      </form>
    </SideSheet>

    <SideSheet open={Boolean(selectedRequestId) && !linkedReversalId} onOpenChange={open => { if (!open) closeRequestDetails(); }} title={detail.data?.title ?? t("projects.paymentRequestDetails")} description={detail.data?.reference}>
      <SideSheetBody className="space-y-4">
        {detail.isLoading ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : detail.isError || !detail.data ? <p role="alert" className="text-sm text-destructive">{detail.error instanceof ApiError && detail.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.paymentRequestDetailsLoadFailed")}</p>
            : <>
              <div className="flex flex-wrap items-center justify-between gap-3"><Badge status={detail.data.status}>{statusText(t, detail.data.status)}</Badge><span className="font-semibold tabular-nums">{formatCurrency(detail.data.amount, detail.data.currency)}</span></div>
              <p className="text-sm text-muted-foreground">{t("projects.paymentRequestRequestedBy")}: {detail.data.requesterName || t("projects.paymentRequestUnknownRequester")}</p>
               {detail.data.annualBudgetFiscalYear ? <p className="text-sm"><span className="font-medium">{t("projects.paymentRequestFiscalYear")}:</span> {t("projects.fiscalYearShort")} {detail.data.annualBudgetFiscalYear}{detail.data.annualBudgetReference ? ` · ${detail.data.annualBudgetReference}` : ""}</p> : <p role="status" className="text-xs text-muted-foreground">{t("projects.paymentRequestLegacyWithoutFiscalYear")}</p>}
               {detail.data.annualBudgetLineName ? <p className="text-sm"><span className="font-medium">{t("projects.paymentRequestBudgetLine")}:</span> {detail.data.annualBudgetLineName}</p> : null}
               {detail.data.plannedPaymentInstallmentId ? <p className="text-sm text-muted-foreground">{t("projects.paymentRequestInstallmentLinked")}</p> : null}
              <div><h3 className="text-sm font-medium">{t("projects.paymentRequestReason")}</h3><p className="mt-1 whitespace-pre-wrap text-sm">{detail.data.reason}</p></div>
              {detail.data.projectPlanMilestoneName ? <p className="text-sm"><span className="font-medium">{t("projects.paymentRequestMilestone")}:</span> {detail.data.projectPlanMilestoneName}</p> : null}
              {detail.data.submittedAtUtc ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestSubmittedAt")}: {formatDateTime(detail.data.submittedAtUtc)}</p> : null}
              {detail.data.completedAtUtc ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestCompletedAt")}: {formatDateTime(detail.data.completedAtUtc)}</p> : null}
              {activeSection === "requests" ? <section className="space-y-3 border-t pt-4"><h3 className="text-sm font-semibold">{t("projects.paymentRequestApprovalHistory")}</h3>
                 {detail.data.approvalCycles?.length ? <ol className="space-y-3">{detail.data.approvalCycles.map(cycle => <li key={cycle.id} className="space-y-2 rounded-md border p-3">
                   <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium">{t("projects.paymentRequestApprovalCycle", { number: cycle.cycleNumber })}</p><p className="text-xs text-muted-foreground">{cycle.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : t("projects.financeApprovalParallel")} · {formatDateTime(cycle.submittedAtUtc)}</p></div><Badge status={cycle.status}>{statusText(t, cycle.status)}</Badge></div>
                   <p className="text-sm font-semibold tabular-nums">{formatCurrency(cycle.amount, cycle.currency)}</p>
                   <p className="text-xs text-muted-foreground">{cycle.annualBudgetReference ?? t("projects.paymentRequestAnnualBudget")} · {cycle.annualBudgetLineName ?? t("projects.paymentRequestBudgetLine")}</p>
                   <p className="text-xs text-muted-foreground">{t("projects.paymentRequestRequestedBy")}: {cycle.requesterName || t("projects.paymentRequestUnknownRequester")}</p>
                   <ol className="space-y-2">{cycle.approvers.map((approver) => <li key={`${cycle.id}-${approver.userId}`} className="rounded-md bg-muted/30 p-2">
                     <div className="flex flex-wrap items-center justify-between gap-2"><span className="font-medium">{approver.position ? `${approver.position}. ` : ""}{approver.displayName || t("projects.paymentRequestUnknownApprover")}</span><Badge status={approver.decisionStatus}>{statusText(t, approver.decisionStatus)}</Badge></div>
                     <p className="mt-1 text-xs text-muted-foreground">{approver.requirement === "Mandatory" ? t("projects.paymentRequestMandatory") : t("projects.paymentRequestOptional")}{approver.isActive ? ` · ${t("projects.paymentRequestActiveApprover")}` : ""}</p>
                     {approver.decidedAt ? <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(approver.decidedAt)}</p> : null}
                     {approver.comment ? <p className="mt-2 whitespace-pre-wrap text-sm">{approver.comment}</p> : null}
                     {approver.canDecide ? <p className="mt-1 text-xs font-medium text-primary">{t("projects.paymentRequestYourTurn")}</p> : null}
                   </li>)}</ol>
                 </li>)}</ol>
                   : detail.data.approvalMode ? <><p className="text-xs text-muted-foreground">{t("projects.paymentRequestApprovalMethod")}: {detail.data.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : t("projects.financeApprovalParallel")}</p><ol className="space-y-2">{detail.data.approvers.map((approver) => <li key={approver.userId} className="rounded-md border p-3">
                     <div className="flex flex-wrap items-center justify-between gap-2"><span className="font-medium">{approver.position ? `${approver.position}. ` : ""}{approver.displayName || t("projects.paymentRequestUnknownApprover")}</span><Badge status={approver.decisionStatus}>{statusText(t, approver.decisionStatus)}</Badge></div>
                     <p className="mt-1 text-xs text-muted-foreground">{approver.requirement === "Mandatory" ? t("projects.paymentRequestMandatory") : t("projects.paymentRequestOptional")}{approver.isActive ? ` · ${t("projects.paymentRequestActiveApprover")}` : ""}</p>
                     {approver.decidedAt ? <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(approver.decidedAt)}</p> : null}
                     {approver.comment ? <p className="mt-2 whitespace-pre-wrap text-sm">{approver.comment}</p> : null}
                     {approver.canDecide ? <p className="mt-1 text-xs font-medium text-primary">{t("projects.paymentRequestYourTurn")}</p> : null}
                   </li>)}</ol></>
                     : <p role="status" className="text-sm text-muted-foreground">{t("projects.paymentRequestNoApprovalHistory")}</p>}
               </section> : null}
              {activeSection === "settlements" ? <section className="space-y-3 border-t pt-4" aria-labelledby="payment-request-settlements-heading">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h3 id="payment-request-settlements-heading" className="text-sm font-semibold">{t("projects.paymentRequestSettlements")}</h3>
                  {canRecordSettlementHere && detail.data.status === "Approved" && settlementHistory.data && settlementHistory.data.summary.outstandingAmount > 0 && !settlementFormOpen ? <Button size="sm" onClick={openSettlementForm}><Receipt className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestRecordSettlement")}</Button> : null}
                </div>
                {settlementHistory.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
                  : settlementHistory.isError || !settlementHistory.data ? <p role="alert" className="text-sm text-destructive">{settlementHistory.error instanceof ApiError && settlementHistory.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.paymentRequestSettlementHistoryLoadFailed")}</p>
                    : <>
                      <div className="grid gap-2 sm:grid-cols-3">
                        <BudgetMetric label={t("projects.paymentRequestApprovedAmount")} value={formatCurrency(settlementHistory.data.summary.approvedAmount, settlementHistory.data.summary.currency)} />
                        <BudgetMetric label={t("projects.paymentRequestTotalSettled")} value={formatCurrency(settlementHistory.data.summary.totalSettled, settlementHistory.data.summary.currency)} />
                        <BudgetMetric label={t("projects.paymentRequestOutstandingAmount")} value={formatCurrency(settlementHistory.data.summary.outstandingAmount, settlementHistory.data.summary.currency)} strong />
                      </div>
                      <p className="text-xs text-muted-foreground">{t("projects.paymentRequestSettlementStatus")}: {settlementHistory.data.summary.settlementStatus === "Not Settled" ? t("projects.paymentRequestSettlementNotSettled") : settlementHistory.data.summary.settlementStatus === "Fully Settled" ? t("projects.paymentRequestSettlementFullySettled") : t("projects.paymentRequestSettlementPartiallySettled")}</p>
                      {settlementHistory.data.summary.annualBudgetFiscalYear ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestFiscalYear")}: {t("projects.fiscalYearShort")} {settlementHistory.data.summary.annualBudgetFiscalYear}{settlementHistory.data.summary.annualBudgetReference ? ` · ${settlementHistory.data.summary.annualBudgetReference}` : ""}</p> : null}
                      {settlementHistory.data.items.length === 0 ? <p className="text-sm text-muted-foreground">{t("projects.paymentRequestSettlementHistoryEmpty")}</p>
                        : <ol className="space-y-2">{settlementHistory.data.items.map(item => <li key={item.id} className="rounded-md border p-3">
                          <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium">{item.settlementReference}</p><p className="text-xs text-muted-foreground">{t("projects.paymentRequestSettlementDate")}: {formatDate(item.paymentDate)} · {t("projects.paymentRequestFiscalYear")}: {t("projects.fiscalYearShort")} {item.reportingFiscalYear}</p></div><p className="shrink-0 font-semibold tabular-nums">{formatCurrency(item.amount, item.currency)}</p></div>
                          {item.description ? <p className="mt-2 whitespace-pre-wrap text-sm text-muted-foreground">{item.description}</p> : null}
                          <SettlementAdjustmentHistory projectId={projectId} settlement={item} canRecord={canRecordSettlementHere} t={t} formatCurrency={formatCurrency} formatDate={formatDate} formatDateTime={formatDateTime} />
                        </li>)}</ol>}
                    </>}
                {settlementFormOpen && canRecordSettlementHere && detail.data.status === "Approved" && settlementHistory.data ? <form onSubmit={event => void saveSettlement(event)} className="space-y-3 rounded-md border p-3">
                  <p className="text-xs text-muted-foreground">{t("projects.paymentRequestSettlementBudgetHint")}</p>
                  {settlementError ? <p role="alert" className="text-sm text-destructive">{settlementError}</p> : null}
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-amount">{t("projects.paymentRequestSettlementAmount")}</Label><Input id="payment-request-settlement-amount" type="number" min="0.0001" max={settlementHistory.data.summary.outstandingAmount} step="0.0001" value={settlementForm.amount} onChange={event => setSettlementForm(current => ({ ...current, amount: event.target.value }))} required /></div>
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-date">{t("projects.paymentRequestSettlementDate")}</Label><Input id="payment-request-settlement-date" type="date" value={settlementForm.paymentDate} onChange={event => setSettlementForm(current => ({ ...current, paymentDate: event.target.value }))} required /></div>
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-reference">{t("projects.paymentRequestSettlementReference")}</Label><Input id="payment-request-settlement-reference" value={settlementForm.reference} onChange={event => setSettlementForm(current => ({ ...current, reference: event.target.value }))} maxLength={100} required /></div>
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-description">{t("projects.paymentRequestSettlementDescription")}</Label><Textarea id="payment-request-settlement-description" value={settlementForm.description} onChange={event => setSettlementForm(current => ({ ...current, description: event.target.value }))} maxLength={2000} required /></div>
                  <div className="flex flex-wrap gap-2"><Button type="submit" disabled={recordSettlement.isPending}>{recordSettlement.isPending ? t("common.saving") : t("projects.paymentRequestRecordSettlement")}</Button><Button type="button" variant="outline" onClick={() => { setSettlementFormOpen(false); setSettlementError(null); }}>{t("common.cancel")}</Button></div>
                </form> : null}
              </section> : null}
              {activeSection === "settlements" && detail.data.status === "Approved" ? <section className="space-y-3 border-t pt-4" aria-labelledby="payment-request-reversals-heading">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div><h3 id="payment-request-reversals-heading" className="text-sm font-semibold">{t("projects.paymentReversalsTitle")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("projects.paymentReversalUnpaidOnlyHint")}</p></div>
                  {canCreateReversals && reversalHistory.data?.canCreate ? <Button size="sm" onClick={() => openReversalForm()} disabled={isSavingReversal}><X className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentReversalCreate")}</Button> : null}
                </div>
                {reversalHistory.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
                  : reversalHistory.isError || !reversalHistory.data ? <p role="alert" className="text-sm text-destructive">{reversalHistory.error instanceof ApiError && reversalHistory.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.paymentReversalHistoryLoadFailed")}</p>
                    : <>
                      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                        <BudgetMetric label={t("projects.paymentRequestApprovedAmount")} value={formatCurrency(reversalHistory.data.originalApprovedAmount, reversalHistory.data.currency)} />
                        <BudgetMetric label={t("projects.paymentRequestTotalSettled")} value={formatCurrency(reversalHistory.data.totalSettled, reversalHistory.data.currency)} />
                        <BudgetMetric label={t("projects.paymentReversalPreviouslyReleased")} value={formatCurrency(reversalHistory.data.totalReleased, reversalHistory.data.currency)} />
                        <BudgetMetric label={t("projects.paymentReversalMaximum")}
                          value={formatCurrency(reversalHistory.data.maximumReversibleAmount, reversalHistory.data.currency)} strong />
                        <BudgetMetric label={t("projects.paymentReversalActiveAllocation")} value={formatCurrency(reversalHistory.data.remainingActiveAllocation, reversalHistory.data.currency)} />
                        <BudgetMetric label={t("projects.paymentRequestOutstandingAmount")} value={formatCurrency(reversalHistory.data.remainingPayable, reversalHistory.data.currency)} />
                      </div>
                      {reversalHistory.data.annualBudgetFiscalYear ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestFiscalYear")}: {t("projects.fiscalYearShort")} {reversalHistory.data.annualBudgetFiscalYear}{reversalHistory.data.annualBudgetReference ? ` · ${reversalHistory.data.annualBudgetReference}` : ""}</p> : null}
                      {reversalHistory.data.hasPendingReversal ? <p role="status" className="rounded-md border p-2 text-xs text-muted-foreground">{t("projects.paymentReversalPendingExists")}</p> : null}
                      {reversalHistory.data.items.length === 0 ? <p className="text-sm text-muted-foreground">{t("projects.paymentReversalHistoryEmpty")}</p>
                        : <ol className="space-y-2">{reversalHistory.data.items.map(item => <li key={item.id} className="space-y-3 rounded-md border p-3">
                          <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium">{item.reference}</p><p className="text-xs text-muted-foreground">{t("projects.paymentRequestRequestedBy")}: {item.requesterName || t("projects.paymentRequestUnknownRequester")} · {formatDateTime(item.createdAtUtc)}</p></div><div className="flex items-center gap-2"><Badge status={item.status}>{statusText(t, item.status)}</Badge><span className="font-semibold tabular-nums">{formatCurrency(item.amount, item.currency)}</span></div></div>
                          <p className="whitespace-pre-wrap text-sm text-muted-foreground">{item.reason}</p>
                          {item.approvalMode ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestApprovalMethod")}: {item.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : t("projects.financeApprovalParallel")}</p> : null}
                          {item.approvers.length ? <ol className="space-y-1">{item.approvers.map(approver => <li key={approver.userId} className="flex flex-wrap items-center justify-between gap-2 text-xs"><span>{approver.position ? `${approver.position}. ` : ""}{approver.displayName || t("projects.paymentRequestUnknownApprover")} · {approver.requirement === "Mandatory" ? t("projects.paymentRequestMandatory") : t("projects.paymentRequestOptional")}</span><span>{statusText(t, approver.decisionStatus)}{approver.decidedAt ? ` · ${formatDateTime(approver.decidedAt)}` : ""}</span></li>)}</ol> : null}
                          <div className="flex flex-wrap gap-2">
                            {canCreateReversals && item.canEdit ? <Button size="sm" variant="outline" onClick={() => openReversalForm(item)}><Pencil className="me-1.5 h-4 w-4" aria-hidden="true" />{t("common.edit")}</Button> : null}
                            {canCreateReversals && item.canSubmit ? <Button size="sm" onClick={() => openReversalSubmission(item)}><Send className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestSubmit")}</Button> : null}
                            {canCreateReversals && item.canCancel ? <Button size="sm" variant="outline" onClick={() => void cancelReversalDraft(item)} disabled={cancelReversal.isPending}>{t("projects.paymentReversalCancelDraft")}</Button> : null}
                          </div>
                          {canApproveReversals && item.canDecide ? <div className="space-y-2 border-t pt-3"><Label htmlFor={`payment-reversal-comment-${item.id}`}>{t("projects.paymentRequestCommentOptional")}</Label><Textarea id={`payment-reversal-comment-${item.id}`} value={reversalDecisionComment} onChange={event => setReversalDecisionComment(event.target.value)} rows={2} /><div className="flex flex-wrap gap-2"><Button size="sm" onClick={() => void decideReversalAction(item, "Approved")} disabled={decideReversal.isPending}><Check className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestApprove")}</Button><Button size="sm" variant="destructive" onClick={() => void decideReversalAction(item, "Rejected")} disabled={decideReversal.isPending}><X className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestReject")}</Button></div></div> : null}
                        </li>)}</ol>}
                    </>}
                {reversalActionError ? <p role="alert" className="text-sm text-destructive">{reversalActionError}</p> : null}
              </section> : null}
               {activeSection === "requests" && canApproveRequests && detail.data.canDecide ? <section className="space-y-3 border-t pt-4"><Label htmlFor="payment-request-comment">{t("projects.paymentRequestDecisionCommentOrReturnReason")}</Label><Textarea id="payment-request-comment" value={comment} onChange={event => { setComment(event.target.value); setDecisionValidation(null); }} rows={3} /><p className="text-xs text-muted-foreground">{t("projects.paymentRequestReturnReasonRequiredHint")}</p>
                 {decisionValidation ? <p role="alert" className="text-sm text-destructive">{decisionValidation}</p> : null}
                 <div className="flex flex-wrap gap-2"><Button onClick={() => void decide("Approved")} disabled={decideRequest.isPending}><Check className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestApprove")}</Button><Button variant="destructive" onClick={() => void decide("Rejected")} disabled={decideRequest.isPending}><X className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestReject")}</Button><Button variant="outline" onClick={() => void decide("ReturnedForRevision")} disabled={decideRequest.isPending}>{t("projects.paymentRequestReturnForRevision")}</Button></div>
              </section> : null}
              {actionError ? <p role="alert" className="text-sm text-destructive">{t("projects.paymentRequestActionFailed")}</p> : null}
            </>}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={closeRequestDetails}>{t("common.close")}</Button></SideSheetFooter>
    </SideSheet>

    <SideSheet open={Boolean(selectedRequestId && linkedReversalId)} onOpenChange={open => { if (!open) closeRequestDetails(); }} title={assignedReversalReview.data?.reference ?? t("projects.paymentReversalsTitle")} description={assignedReversalReview.data?.originalPaymentRequestReference}>
      <SideSheetBody className="space-y-4">
        {assignedReversalReview.isLoading ? <p role="status" className="py-6 text-sm text-muted-foreground">{t("common.loading")}</p>
          : assignedReversalReview.isError || !assignedReversalReview.data ? <p role="alert" className="text-sm text-destructive">{assignedReversalReview.error instanceof ApiError && assignedReversalReview.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.paymentRequestDetailsLoadFailed")}</p>
            : <>
              <div className="flex flex-wrap items-center justify-between gap-3"><Badge status={assignedReversalReview.data.status}>{statusText(t, assignedReversalReview.data.status)}</Badge><span className="font-semibold tabular-nums">{formatCurrency(assignedReversalReview.data.amount, assignedReversalReview.data.currency)}</span></div>
              <p className="text-sm text-muted-foreground">{t("projects.paymentReversalOriginalRequest")}: {assignedReversalReview.data.originalPaymentRequestReference}</p>
              <p className="text-sm text-muted-foreground">{t("projects.paymentRequestRequestedBy")}: {assignedReversalReview.data.requesterName || t("projects.paymentRequestUnknownRequester")}</p>
              <div><h3 className="text-sm font-medium">{t("projects.paymentRequestReason")}</h3><p className="mt-1 whitespace-pre-wrap text-sm">{assignedReversalReview.data.reason}</p></div>
              <div className="grid gap-3 sm:grid-cols-2"><BudgetMetric label={t("projects.paymentRequestApprovedAmount")} value={formatCurrency(assignedReversalReview.data.originalApprovedAmount, assignedReversalReview.data.currency)} /><BudgetMetric label={t("projects.paymentRequestTotalSettled")} value={formatCurrency(assignedReversalReview.data.totalSettled, assignedReversalReview.data.currency)} /><BudgetMetric label={t("projects.paymentReversalPreviouslyReleased")} value={formatCurrency(assignedReversalReview.data.previouslyReleasedAmount, assignedReversalReview.data.currency)} /><BudgetMetric label={t("projects.paymentReversalMaximum")} value={formatCurrency(assignedReversalReview.data.maximumReversibleAmount, assignedReversalReview.data.currency)} /></div>
              {assignedReversalReview.data.approvers.length ? <section className="space-y-2 border-t pt-4"><h3 className="text-sm font-semibold">{t("projects.paymentRequestApprovalHistory")}</h3><ol className="space-y-2">{assignedReversalReview.data.approvers.map(approver => <li key={approver.userId} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3"><span className="text-sm">{approver.position ? `${approver.position}. ` : ""}{approver.displayName || t("projects.paymentRequestUnknownApprover")}</span><Badge status={approver.decisionStatus}>{statusText(t, approver.decisionStatus)}</Badge></li>)}</ol></section> : null}
              {canApproveReversals && assignedReversalReview.data.canDecide ? <section className="space-y-2 border-t pt-4"><Label htmlFor="assigned-reversal-review-comment">{t("projects.paymentRequestCommentOptional")}</Label><Textarea id="assigned-reversal-review-comment" value={reversalDecisionComment} onChange={event => setReversalDecisionComment(event.target.value)} rows={3} />{reversalActionError ? <p role="alert" className="text-sm text-destructive">{reversalActionError}</p> : null}<div className="flex flex-wrap gap-2"><Button onClick={() => void decideReversalAction(assignedReversalReview.data!, "Approved")} disabled={decideReversal.isPending}><Check className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestApprove")}</Button><Button variant="destructive" onClick={() => void decideReversalAction(assignedReversalReview.data!, "Rejected")} disabled={decideReversal.isPending}><X className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestReject")}</Button></div></section> : null}
            </>}
      </SideSheetBody>
      <SideSheetFooter><Button type="button" variant="outline" onClick={closeRequestDetails}>{t("common.close")}</Button></SideSheetFooter>
    </SideSheet>

    <SideSheet open={reversalFormOpen} onOpenChange={setReversalFormOpen} title={editingReversal ? t("projects.paymentReversalEdit") : t("projects.paymentReversalCreate")} description={t("projects.paymentReversalFormDescription")}>
      <form onSubmit={event => void saveReversalDraft(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <p className="rounded-md border p-3 text-sm text-muted-foreground">{t("projects.paymentReversalUnpaidOnlyHint")}</p>
          {reversalHistory.data ? <p className="text-sm">{t("projects.paymentReversalMaximum")}: <span className="font-semibold tabular-nums">{formatCurrency(reversalHistory.data.maximumReversibleAmount, reversalHistory.data.currency)}</span></p> : null}
          <div><Label htmlFor="payment-reversal-amount">{t("projects.paymentReversalAmount")}</Label><Input id="payment-reversal-amount" type="number" min="0.0001" max={reversalHistory.data?.maximumReversibleAmount} step="0.0001" value={reversalForm.amount} onChange={event => setReversalForm(current => ({ ...current, amount: event.target.value }))} required /><p className="mt-1 text-xs text-muted-foreground">{detail.data?.currency}</p></div>
          <div><Label htmlFor="payment-reversal-reason">{t("projects.paymentRequestReason")}</Label><Textarea id="payment-reversal-reason" value={reversalForm.reason} onChange={event => setReversalForm(current => ({ ...current, reason: event.target.value }))} rows={5} maxLength={2000} required /></div>
          {reversalFormError ? <p role="alert" className="text-sm text-destructive">{reversalFormError}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setReversalFormOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={isSavingReversal || !reversalHistory.data?.maximumReversibleAmount}>{isSavingReversal ? t("common.saving") : t("common.save")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  </>;
}

function BudgetMetric({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return <div className="rounded-md border bg-background p-3">
    <p className="text-xs text-muted-foreground">{label}</p>
    <p className={`mt-1 text-sm tabular-nums ${strong ? "font-semibold" : "font-medium"}`}>{value}</p>
  </div>;
}
