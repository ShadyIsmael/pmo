import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { Check, Eye, Pencil, Plus, Receipt, Send, X } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useI18n } from "@i18n/messages";
import { ApiError } from "@api/client";
import {
  useCancelProjectPaymentRequest,
  useCancelProjectPaymentReversal,
  useCreateProjectPaymentRequest,
  useCreateProjectPaymentReversal,
  useDecideProjectPaymentRequest,
  useDecideProjectPaymentReversal,
  usePaymentRequestApprovers,
  usePaymentRequestMilestones,
  useProjectPaymentReversalApprovers,
  useProjectFinancialFoundation,
  useProjectPaymentRequestSettlements,
  useProjectPaymentReversals,
  useAssignedProjectPaymentReversalReview,
  useProjectPaymentRequest,
  useProjectPaymentRequests,
  useRecordProjectPaymentRequestSettlement,
  useSubmitProjectPaymentReversal,
  useSubmitProjectPaymentRequest,
  useUpdateProjectPaymentReversal,
  useUpdateProjectPaymentRequest,
  type PaymentRequestApproverCandidate,
  type ProjectPaymentReversal,
  type ProjectPaymentRequestListItem,
} from "./api/project-financials";

type ApprovalMode = "Sequential" | "Parallel";
type RequestForm = { title: string; amount: string; reason: string; milestoneId: string };
const emptyForm = (): RequestForm => ({ title: "", amount: "", reason: "", milestoneId: "" });
const todayLocal = () => {
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
};

function statusText(t: (key: string) => string, status: string) {
  const key = `projects.paymentRequestStatus${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

export function ProjectPaymentRequests({ projectId, canViewRequests, canReviewRequests, canReviewReversals, canCreateRequests, canEditRequests, canSubmitRequests, canCancelRequests, canApproveRequests, canRecordSettlements, canCreateReversals, canApproveReversals, section = "requests", active = true }: {
  projectId: string;
  canViewRequests: boolean;
  canReviewRequests: boolean;
  canReviewReversals: boolean;
  canCreateRequests: boolean;
  canEditRequests: boolean;
  canSubmitRequests: boolean;
  canCancelRequests: boolean;
  canApproveRequests: boolean;
  canRecordSettlements: boolean;
  canCreateReversals: boolean;
  canApproveReversals: boolean;
  section?: "requests" | "settlements";
  active?: boolean;
}) {
  const { t, formatCurrency, formatDate, formatDateTime } = useI18n();
  const confirm = useConfirm();
  const [searchParams, setSearchParams] = useSearchParams();
  const linkedReversalId = searchParams.get("paymentReversalId");
  const requests = useProjectPaymentRequests(projectId, active && canViewRequests, section === "settlements" ? "Approved" : undefined);
  const foundation = useProjectFinancialFoundation(projectId, active && canViewRequests);
  const createRequest = useCreateProjectPaymentRequest(projectId);
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
  const [editing, setEditing] = React.useState<ProjectPaymentRequestListItem | null>(null);
  const [form, setForm] = React.useState<RequestForm>(emptyForm);
  const [formError, setFormError] = React.useState(false);
  const [idempotencyKey, setIdempotencyKey] = React.useState("");
  const [selectedRequestId, setSelectedRequestId] = React.useState<string | null>(() => searchParams.get("paymentRequestId"));
  const [submitTarget, setSubmitTarget] = React.useState<ProjectPaymentRequestListItem | null>(null);
  const [reversalSubmitTarget, setReversalSubmitTarget] = React.useState<ProjectPaymentReversal | null>(null);
  const [approvalMode, setApprovalMode] = React.useState<ApprovalMode | "">("");
  const [selectedApproverIds, setSelectedApproverIds] = React.useState<string[]>([]);
  const [requirements, setRequirements] = React.useState<Record<string, "Mandatory" | "Optional">>({});
  const [submitError, setSubmitError] = React.useState<string | null>(null);
  const [comment, setComment] = React.useState("");
  const [actionError, setActionError] = React.useState(false);
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
  const paymentApprovers = usePaymentRequestApprovers(projectId, active && Boolean(submitTarget && canSubmitRequests));
  const reversalApprovers = useProjectPaymentReversalApprovers(
    projectId, active && Boolean(reversalSubmitTarget && canCreateReversals));
  const approvers = reversalSubmitTarget ? reversalApprovers : paymentApprovers;
  const milestones = usePaymentRequestMilestones(projectId, active && Boolean(formOpen && (canCreateRequests || canEditRequests)));
  const detail = useProjectPaymentRequest(projectId, selectedRequestId, active && Boolean(selectedRequestId) && !linkedReversalId);
  const assignedReversalReview = useAssignedProjectPaymentReversalReview(
    projectId, selectedRequestId, linkedReversalId,
    active && canReviewReversals && Boolean(linkedReversalId));
  const settlementHistory = useProjectPaymentRequestSettlements(projectId, selectedRequestId,
    active && section === "settlements" && Boolean(selectedRequestId));
  const reversalHistory = useProjectPaymentReversals(
    projectId, selectedRequestId, active && section === "settlements" && Boolean(selectedRequestId && detail.data?.status === "Approved"));
  const editDetail = useProjectPaymentRequest(projectId, editing?.id ?? null, active && Boolean(formOpen && editing));

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
        milestoneId: editDetail.data.projectPlanMilestoneId ?? "",
      });
    }
  }, [formOpen, editing, editDetail.data]);

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setFormError(false);
    setIdempotencyKey(crypto.randomUUID());
    setFormOpen(true);
  };

  const openEdit = (item: ProjectPaymentRequestListItem) => {
    setEditing(item);
    setForm({ title: item.title, amount: String(item.amount), reason: "", milestoneId: "" });
    setFormError(false);
    setFormOpen(true);
  };

  const saveRequest = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(false);
    const amount = Number(form.amount);
    if (!form.title.trim() || !form.reason.trim() || !Number.isFinite(amount) || amount <= 0 || !foundation.data?.projectCurrency) {
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
          projectPlanMilestoneId: form.milestoneId || undefined,
          rowVersion: current.rowVersion,
        });
      } else {
        await createRequest.mutateAsync({
          title: form.title.trim(),
          amount,
          reason: form.reason.trim(),
          projectPlanMilestoneId: form.milestoneId || undefined,
          idempotencyKey,
        });
      }
      setFormOpen(false);
      setEditing(null);
    } catch {
      setFormError(true);
    }
  };

  const openSubmission = (item: ProjectPaymentRequestListItem) => {
    setReversalSubmitTarget(null);
    setSubmitTarget(item);
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
    if (!budgetAvailability?.canSubmit
      || budgetAvailability.fiscalYear !== submitTarget.annualBudgetFiscalYear
      || (budgetAvailability.availableAmount ?? 0) < submitTarget.amount) {
      setSubmitError(t("projects.paymentRequestInsufficientAnnualBudget"));
      return;
    }
    try {
      await submitRequest.mutateAsync({ id: submitTarget.id, approvalMode, approvers: assigned, rowVersion: submitTarget.rowVersion });
      setSubmitTarget(null);
      setSelectedRequestId(submitTarget.id);
    } catch (error) {
      setSubmitError(error instanceof ApiError ? error.message : t("projects.paymentRequestSubmitFailed"));
    }
  };

  const decide = async (decision: "Approved" | "Rejected") => {
    if (!detail.data) return;
    setActionError(false);
    try {
      await decideRequest.mutateAsync({ id: detail.data.id, decision, comment: comment.trim() || undefined, rowVersion: detail.data.rowVersion });
      setComment("");
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

  const isSaving = createRequest.isPending || updateRequest.isPending || (Boolean(editing) && editDetail.isLoading);
  const isSavingReversal = createReversal.isPending || updateReversal.isPending;
  const configMode = foundation.data?.financeApprovalMode;
  const budgetAvailability = requests.data?.budgetAvailability;
  const availabilityMessageKey: Record<string, string> = {
    AnnualBudgetMissing: "projects.paymentRequestAnnualBudgetMissing",
    AnnualBudgetDraft: "projects.paymentRequestAnnualBudgetDraft",
    LegacyReconciliationRequired: "projects.paymentRequestLegacyReconciliation",
    CurrencyOrPeriodMismatch: "projects.paymentRequestBudgetMismatch",
    NoAvailableBalance: "projects.paymentRequestNoAvailableBalance",
  };
  const budgetMessageKey = budgetAvailability ? availabilityMessageKey[budgetAvailability.availabilityStatus] : undefined;

  return <>
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 pb-3">
        <div><CardTitle className="text-base">{t(section === "requests" ? "projects.paymentRequestsTitle" : "projects.financeWorkspaceSettlements")}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{t(section === "requests" ? "projects.paymentRequestsDescription" : "projects.financeSettlementWorkspaceDescription")}</p>
        </div>
        {section === "requests" && canCreateRequests ? <Button size="sm" onClick={openCreate} disabled={!foundation.data?.projectCurrency || !budgetAvailability?.canCreate}>
          <Plus className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestCreate")}
        </Button> : null}
      </CardHeader>
      <CardContent className="space-y-3">
        {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("projects.paymentRequestActionFailed")}</p> : null}
        {section === "requests" && canViewRequests && budgetAvailability ? <section className="space-y-3 rounded-lg border bg-muted/20 p-4" aria-label={t("projects.paymentRequestAnnualBudget")}>
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
                    {section === "requests" && canEditRequests && item.canEdit ? <Button size="sm" variant="outline" onClick={() => openEdit(item)}><Pencil className="me-1.5 h-4 w-4" aria-hidden="true" />{t("common.edit")}</Button> : null}
                    {section === "requests" && canSubmitRequests && item.canSubmit ? <Button size="sm" onClick={() => openSubmission(item)} disabled={!budgetAvailability?.canSubmit || budgetAvailability.fiscalYear !== item.annualBudgetFiscalYear || (budgetAvailability.availableAmount ?? 0) < item.amount}><Send className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestSubmit")}</Button> : null}
                    {section === "requests" && canCancelRequests && item.canCancel ? <Button size="sm" variant="outline" onClick={() => void cancel(item)} disabled={cancelRequest.isPending}>{t("projects.paymentRequestCancel")}</Button> : null}
                  </div>
                </li>)}
              </ul>}
        {canViewRequests && requests.data ? <p className="border-t pt-3 text-xs text-muted-foreground">{t("projects.totalRecords", { count: requests.data.total })}</p> : null}
      </CardContent>
    </Card>

    <SideSheet open={formOpen} onOpenChange={setFormOpen} title={editing ? t("projects.paymentRequestEdit") : t("projects.paymentRequestCreate")} description={t("projects.paymentRequestFormDescription")}>
      <form onSubmit={event => void saveRequest(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <div><Label htmlFor="payment-request-title">{t("projects.paymentRequestTitleLabel")}</Label><Input id="payment-request-title" value={form.title} onChange={event => setForm(current => ({ ...current, title: event.target.value }))} required maxLength={200} /></div>
          <div><Label htmlFor="payment-request-amount">{t("projects.amount")}</Label><Input id="payment-request-amount" type="number" min="0.0001" step="0.0001" value={form.amount} onChange={event => setForm(current => ({ ...current, amount: event.target.value }))} required />
            <p className="mt-1 text-xs text-muted-foreground">{foundation.data?.projectCurrency ?? t("projects.financialCurrencyUnavailable")}</p>
          </div>
          <div><Label htmlFor="payment-request-reason">{t("projects.paymentRequestReason")}</Label><Textarea id="payment-request-reason" value={form.reason} onChange={event => setForm(current => ({ ...current, reason: event.target.value }))} rows={5} required /></div>
          <div><Label htmlFor="payment-request-milestone">{t("projects.paymentRequestMilestone")}</Label>
            <select id="payment-request-milestone" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm" value={form.milestoneId} onChange={event => setForm(current => ({ ...current, milestoneId: event.target.value }))}>
              <option value="">{t("projects.paymentRequestNoMilestone")}</option>
              {(milestones.data ?? []).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            {milestones.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("projects.paymentRequestMilestonesLoadFailed")}</p> : null}
          </div>
          {formError ? <p role="alert" className="text-sm text-destructive">{t("projects.paymentRequestSaveFailed")}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setFormOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={isSaving || !foundation.data?.projectCurrency}>{isSaving ? t("common.saving") : t("common.save")}</Button></SideSheetFooter>
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
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => { setSubmitTarget(null); setReversalSubmitTarget(null); }}>{t("common.cancel")}</Button><Button type="submit" disabled={submitRequest.isPending || submitReversal.isPending || approvers.isLoading || !configMode || (reversalSubmitTarget ? !reversalSubmitTarget.canSubmit : !budgetAvailability?.canSubmit || budgetAvailability.fiscalYear !== submitTarget?.annualBudgetFiscalYear || (budgetAvailability.availableAmount ?? 0) < (submitTarget?.amount ?? 0))}>{submitRequest.isPending || submitReversal.isPending ? t("common.saving") : t("projects.paymentRequestSubmit")}</Button></SideSheetFooter>
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
              <div><h3 className="text-sm font-medium">{t("projects.paymentRequestReason")}</h3><p className="mt-1 whitespace-pre-wrap text-sm">{detail.data.reason}</p></div>
              {detail.data.projectPlanMilestoneName ? <p className="text-sm"><span className="font-medium">{t("projects.paymentRequestMilestone")}:</span> {detail.data.projectPlanMilestoneName}</p> : null}
              {detail.data.submittedAtUtc ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestSubmittedAt")}: {formatDateTime(detail.data.submittedAtUtc)}</p> : null}
              {detail.data.completedAtUtc ? <p className="text-xs text-muted-foreground">{t("projects.paymentRequestCompletedAt")}: {formatDateTime(detail.data.completedAtUtc)}</p> : null}
              {section === "requests" && detail.data.approvalMode ? <section className="space-y-2 border-t pt-4"><h3 className="text-sm font-semibold">{t("projects.paymentRequestApprovalHistory")}</h3><p className="text-xs text-muted-foreground">{t("projects.paymentRequestApprovalMethod")}: {detail.data.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : t("projects.financeApprovalParallel")}</p>
                <ol className="space-y-2">{detail.data.approvers.map((approver) => <li key={approver.userId} className="rounded-md border p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2"><span className="font-medium">{approver.position ? `${approver.position}. ` : ""}{approver.displayName || t("projects.paymentRequestUnknownApprover")}</span><Badge status={approver.decisionStatus}>{statusText(t, approver.decisionStatus)}</Badge></div>
                  <p className="mt-1 text-xs text-muted-foreground">{approver.requirement === "Mandatory" ? t("projects.paymentRequestMandatory") : t("projects.paymentRequestOptional")}{approver.isActive ? ` · ${t("projects.paymentRequestActiveApprover")}` : ""}</p>
                  {approver.decidedAt ? <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(approver.decidedAt)}</p> : null}
                  {approver.comment ? <p className="mt-2 whitespace-pre-wrap text-sm">{approver.comment}</p> : null}
                  {approver.canDecide ? <p className="mt-1 text-xs font-medium text-primary">{t("projects.paymentRequestYourTurn")}</p> : null}
                </li>)}</ol>
              </section> : null}
              {section === "settlements" ? <section className="space-y-3 border-t pt-4" aria-labelledby="payment-request-settlements-heading">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h3 id="payment-request-settlements-heading" className="text-sm font-semibold">{t("projects.paymentRequestSettlements")}</h3>
                  {canRecordSettlements && detail.data.status === "Approved" && settlementHistory.data && settlementHistory.data.summary.outstandingAmount > 0 && !settlementFormOpen ? <Button size="sm" onClick={openSettlementForm}><Receipt className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestRecordSettlement")}</Button> : null}
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
                        </li>)}</ol>}
                    </>}
                {settlementFormOpen && canRecordSettlements && detail.data.status === "Approved" && settlementHistory.data ? <form onSubmit={event => void saveSettlement(event)} className="space-y-3 rounded-md border p-3">
                  <p className="text-xs text-muted-foreground">{t("projects.paymentRequestSettlementBudgetHint")}</p>
                  {settlementError ? <p role="alert" className="text-sm text-destructive">{settlementError}</p> : null}
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-amount">{t("projects.paymentRequestSettlementAmount")}</Label><Input id="payment-request-settlement-amount" type="number" min="0.0001" max={settlementHistory.data.summary.outstandingAmount} step="0.0001" value={settlementForm.amount} onChange={event => setSettlementForm(current => ({ ...current, amount: event.target.value }))} required /></div>
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-date">{t("projects.paymentRequestSettlementDate")}</Label><Input id="payment-request-settlement-date" type="date" value={settlementForm.paymentDate} onChange={event => setSettlementForm(current => ({ ...current, paymentDate: event.target.value }))} required /></div>
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-reference">{t("projects.paymentRequestSettlementReference")}</Label><Input id="payment-request-settlement-reference" value={settlementForm.reference} onChange={event => setSettlementForm(current => ({ ...current, reference: event.target.value }))} maxLength={100} required /></div>
                  <div className="space-y-1.5"><Label htmlFor="payment-request-settlement-description">{t("projects.paymentRequestSettlementDescription")}</Label><Textarea id="payment-request-settlement-description" value={settlementForm.description} onChange={event => setSettlementForm(current => ({ ...current, description: event.target.value }))} maxLength={2000} required /></div>
                  <div className="flex flex-wrap gap-2"><Button type="submit" disabled={recordSettlement.isPending}>{recordSettlement.isPending ? t("common.saving") : t("projects.paymentRequestRecordSettlement")}</Button><Button type="button" variant="outline" onClick={() => { setSettlementFormOpen(false); setSettlementError(null); }}>{t("common.cancel")}</Button></div>
                </form> : null}
              </section> : null}
              {section === "settlements" && detail.data.status === "Approved" ? <section className="space-y-3 border-t pt-4" aria-labelledby="payment-request-reversals-heading">
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
              {section === "requests" && canApproveRequests && detail.data.canDecide ? <section className="space-y-3 border-t pt-4"><Label htmlFor="payment-request-comment">{t("projects.paymentRequestCommentOptional")}</Label><Textarea id="payment-request-comment" value={comment} onChange={event => setComment(event.target.value)} rows={3} />
                <div className="flex flex-wrap gap-2"><Button onClick={() => void decide("Approved")} disabled={decideRequest.isPending}><Check className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestApprove")}</Button><Button variant="destructive" onClick={() => void decide("Rejected")} disabled={decideRequest.isPending}><X className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestReject")}</Button></div>
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
