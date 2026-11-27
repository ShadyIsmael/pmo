import * as React from "react";
import { ApiError } from "@api/client";
import { api } from "@api/client";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { CalendarClock, LockKeyhole, Plus, Send, Trash2, RotateCcw, AlertTriangle, ChevronRight, XCircle, ArrowUpRight, Loader2 } from "lucide-react";
import {
  usePlannedPaymentBudget,
  usePlannedPaymentMilestones,
  usePlannedPaymentReplacementPreview,
  useProjectPlannedPayments,
  useRegisterPlannedPayment,
  useReplacePlannedPayment,
  type PlannedPaymentRequestContext,
  type PlannedPaymentDto,
  type PlannedPaymentReplacementPreviewBlocker,
  type ReplacePlannedPaymentInstallmentInput,
  type ReplaceRemainingCommitmentInput,
} from "../api/planned-payments";
import {
  useCancelProjectPaymentRequest,
  useCreateProjectPaymentReversal,
  usePaymentRequestApprovers,
  type ProjectPaymentRequest,
} from "../api/project-financials";

type InstallmentDraft = {
  key: string;
  amount: string;
  dueDate: string;
  planActivityId: string;
  description: string;
};

type ReplacementInstallmentDraft = {
  key: string;
  amount: string;
  dueDate: string;
  planActivityId: string;
  description: string;
};

type PlannedPaymentSectionProps = {
  projectId: string;
  projectCurrency: string;
  canRegister: boolean;
  canCreateRequest: boolean;
  canReplace: boolean;
  onCreateRequest: (context: PlannedPaymentRequestContext) => void;
  enabled?: boolean;
};

const PAGE_SIZE = 5;

function newKey() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function emptyDraft(): { description: string; idempotencyKey: string; installments: InstallmentDraft[] } {
  return {
    description: "",
    idempotencyKey: newKey(),
    installments: [{ key: newKey(), amount: "", dueDate: "", planActivityId: "", description: "" }],
  };
}

function emptyReplacementDraft(): { newRemainingCommitment: string; installments: ReplacementInstallmentDraft[]; reason: string; idempotencyKey: string } {
  return {
    newRemainingCommitment: "",
    installments: [{ key: newKey(), amount: "", dueDate: "", planActivityId: "", description: "" }],
    reason: "",
    idempotencyKey: newKey(),
  };
}

function money(value: number | null | undefined, currency: string, formatCurrency: (value: number, currency?: string) => string) {
  return typeof value === "number" && Number.isFinite(value) && currency ? formatCurrency(value, currency) : "—";
}

function isFourDecimalAmount(value: string) {
  return /^\d+(\.\d{1,4})?$/.test(value.trim()) && Number(value) > 0;
}

function stateLabel(t: (key: string) => string, state: string) {
  const key = `projects.plannedPaymentState.${state}`;
  const label = t(key);
  return label === key ? state : label;
}

function actionError(error: unknown, t: (key: string) => string) {
  if (error instanceof ApiError && error.code === "INSUFFICIENT_LIFETIME_BUDGET")
    return t("projects.plannedPaymentInsufficientBudget");
  if (error instanceof ApiError && error.code === "INSTALLMENT_OVERCOMMITTED")
    return t("projects.plannedPaymentInstallmentOvercommitted");
  return error instanceof Error && error.message ? error.message : t("projects.plannedPaymentSaveFailed");
}

export function PlannedPaymentSection({ projectId, projectCurrency, canRegister, canCreateRequest, canReplace, onCreateRequest, enabled = true }: PlannedPaymentSectionProps) {
  const { t, formatCurrency, formatDate } = useI18n();
  const [page, setPage] = React.useState(1);
  const [detail, setDetail] = React.useState<PlannedPaymentDto | null>(null);
  const [registerOpen, setRegisterOpen] = React.useState(false);
  const [replaceOpen, setReplaceOpen] = React.useState(false);
  const [draft, setDraft] = React.useState(emptyDraft);
  const [replaceDraft, setReplaceDraft] = React.useState(emptyReplacementDraft);
  const [formError, setFormError] = React.useState<string | null>(null);
  const [replaceError, setReplaceError] = React.useState<string | null>(null);
  const [confirmedReplacementBlockers, setConfirmedReplacementBlockers] = React.useState<PlannedPaymentReplacementPreviewBlocker[]>([]);
  const [resolvingBlockerId, setResolvingBlockerId] = React.useState<string | null>(null);

  const budget = usePlannedPaymentBudget(projectId, enabled);
  const plans = useProjectPlannedPayments(projectId, page, enabled);
  const milestones = usePlannedPaymentMilestones(projectId, enabled && (registerOpen || replaceOpen));
  const register = useRegisterPlannedPayment(projectId);
  const replace = useReplacePlannedPayment(projectId);
  const cancelPaymentRequest = useCancelProjectPaymentRequest(projectId);
  const createReversal = useCreateProjectPaymentReversal(projectId);
  const reversalApprovers = usePaymentRequestApprovers(projectId, true);
  const replacementAmountInput = isFourDecimalAmount(replaceDraft.newRemainingCommitment) ? Number(replaceDraft.newRemainingCommitment) : null;
  const replacementPreview = usePlannedPaymentReplacementPreview(projectId, detail?.id ?? null, replacementAmountInput, replaceOpen && canReplace);
  const currentReplacementPreview = replacementPreview.data?.plannedPaymentId === detail?.id ? replacementPreview.data : undefined;
  const replacementPreviewMatchesInput = currentReplacementPreview?.newRemainingCommitment === replacementAmountInput;
  const replacementPreviewIsCurrent = replacementPreview.isSuccess && !replacementPreview.isFetching && replacementPreviewMatchesInput;
  React.useEffect(() => {
    if (replacementPreviewIsCurrent && currentReplacementPreview) {
      setConfirmedReplacementBlockers(currentReplacementPreview.blockers);
    }
  }, [currentReplacementPreview, replacementPreviewIsCurrent]);

  if (!enabled) return null;

  const currency = budget.data?.currency || projectCurrency;
  const items = plans.data?.items ?? [];
  const totalPages = Math.max(1, Math.ceil((plans.data?.totalCount ?? 0) / PAGE_SIZE));
  const draftTotal = draft.installments.reduce((sum, row) => sum + (isFourDecimalAmount(row.amount) ? Number(row.amount) : 0), 0);
  const canSubmit = canRegister && budget.data?.canRegister !== false && Boolean(currency);

  const replacementInstallmentTotal = replaceDraft.installments.reduce((sum, row) => sum + (isFourDecimalAmount(row.amount) ? Number(row.amount) : 0), 0);
  const newRemainingCommitment = replaceDraft.newRemainingCommitment ? Number(replaceDraft.newRemainingCommitment) : 0;
  const replaceBlockers = replacementPreviewIsCurrent && currentReplacementPreview
    ? currentReplacementPreview.blockers
    : confirmedReplacementBlockers;
  const amountsMatch = Math.abs(replacementInstallmentTotal - newRemainingCommitment) < 0.0001;
  const canSubmitReplace = canReplace && Boolean(currency) && replaceDraft.newRemainingCommitment !== "" && newRemainingCommitment > 0
    && replaceDraft.reason.trim() !== "" && amountsMatch && replaceDraft.installments.length > 0
    && isFourDecimalAmount(replaceDraft.newRemainingCommitment) && replacementPreviewIsCurrent
    && currentReplacementPreview?.hasSufficientLifetimeBudget === true && replaceBlockers.length === 0;

  const updateInstallment = (key: string, patch: Partial<InstallmentDraft>) => {
    setDraft((current) => ({
      ...current,
      installments: current.installments.map((row) => row.key === key ? { ...row, ...patch } : row),
    }));
  };

  const updateReplacementInstallment = (key: string, patch: Partial<ReplacementInstallmentDraft>) => {
    setReplaceDraft((current) => ({
      ...current,
      installments: current.installments.map((row) => row.key === key ? { ...row, ...patch } : row),
    }));
  };

  const closeRegister = () => {
    setRegisterOpen(false);
    setFormError(null);
  };

  const closeReplace = () => {
    setReplaceOpen(false);
    setReplaceError(null);
    setReplaceDraft(emptyReplacementDraft());
  };

  const openReplace = (plan: PlannedPaymentDto) => {
    if (!plan.isLocked) return;
    setConfirmedReplacementBlockers([]);
    const eligibleOldUnpaid = plan.totalAmount - plan.settledAmount;
    const outstandingInstallments = plan.installments
      .filter((inst) => inst.amount - inst.actualPaid > 0)
      .map((inst) => ({
        key: newKey(),
        amount: String(inst.amount - inst.actualPaid),
        dueDate: inst.dueDateUtc ? inst.dueDateUtc.slice(0, 10) : "",
        planActivityId: inst.planActivityId ?? "",
        description: inst.planActivityName ?? "",
      }));
    setDetail(plan);
    setReplaceDraft({
      newRemainingCommitment: String(eligibleOldUnpaid),
      installments: outstandingInstallments.length > 0 ? outstandingInstallments : [{ key: newKey(), amount: String(eligibleOldUnpaid), dueDate: "", planActivityId: "", description: "" }],
      reason: "",
      idempotencyKey: newKey(),
    });
    setReplaceError(null);
    setReplaceOpen(true);
  };

  const submitRegister = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    if (!currency) {
      setFormError(t("projects.financialCurrencyUnavailable"));
      return;
    }
    if (!draft.description.trim()) {
      setFormError(t("projects.plannedPaymentDescriptionRequired"));
      return;
    }
    if (draft.installments.length === 0) {
      setFormError(t("projects.plannedPaymentInstallmentRequired"));
      return;
    }
    if (draft.installments.some((row) => !isFourDecimalAmount(row.amount) || !row.dueDate)) {
      setFormError(t("projects.plannedPaymentInstallmentInvalid"));
      return;
    }

    try {
      await register.mutateAsync({
        description: draft.description.trim(),
        idempotencyKey: draft.idempotencyKey,
        installments: draft.installments.map((row) => ({
          amount: Number(row.amount),
          dueDateUtc: `${row.dueDate}T00:00:00Z`,
          planActivityId: row.planActivityId || undefined,
          description: row.description.trim() || undefined,
        })),
      });
      setDraft(emptyDraft());
      setPage(1);
      closeRegister();
    } catch (error) {
      setFormError(actionError(error, t));
    }
  };

  const submitReplace = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setReplaceError(null);
    if (!currency) {
      setReplaceError(t("projects.financialCurrencyUnavailable"));
      return;
    }
    if (!detail) {
      setReplaceError(t("projects.plannedPaymentNotFound"));
      return;
    }
    const newRemainingCommitment = Number(replaceDraft.newRemainingCommitment);
    if (!Number.isFinite(newRemainingCommitment) || newRemainingCommitment <= 0) {
      setReplaceError(t("projects.plannedPaymentReplacementAmountInvalid"));
      return;
    }
    if (replaceDraft.installments.length === 0) {
      setReplaceError(t("projects.plannedPaymentInstallmentRequired"));
      return;
    }
    if (replaceDraft.installments.some((row) => !isFourDecimalAmount(row.amount) || !row.dueDate)) {
      setReplaceError(t("projects.plannedPaymentInstallmentInvalid"));
      return;
    }
    if (!replaceDraft.reason.trim()) {
      setReplaceError(t("projects.plannedPaymentReplacementReasonRequired"));
      return;
    }
    const installmentTotal = replaceDraft.installments.reduce((sum, row) => sum + Number(row.amount), 0);
    if (Math.abs(installmentTotal - newRemainingCommitment) >= 0.0001) {
      setReplaceError(t("projects.plannedPaymentReplacementInstallmentsMismatch"));
      return;
    }

    try {
      await replace.mutateAsync({
        plannedPaymentId: detail.id,
        body: {
          newRemainingCommitment,
          installments: replaceDraft.installments.map((row) => ({
            amount: Number(row.amount),
            dueDateUtc: `${row.dueDate}T00:00:00Z`,
            planActivityId: row.planActivityId || undefined,
            description: row.description.trim() || undefined,
          })),
          reason: replaceDraft.reason.trim(),
          idempotencyKey: replaceDraft.idempotencyKey,
        } as ReplaceRemainingCommitmentInput,
      });
      setDetail(null);
      setPage(1);
      closeReplace();
    } catch (error) {
      if (error instanceof ApiError && error.code === "ACTIVE_PAYMENT_REQUESTS_BLOCK_REPLACEMENT") {
        await replacementPreview.refetch();
        setReplaceError(t("projects.plannedPaymentActiveRequestsBlock"));
      } else {
        setReplaceError(actionError(error, t));
      }
    }
  };

  const handleCancelPaymentRequest = async (blocker: PlannedPaymentReplacementPreviewBlocker) => {
    setResolvingBlockerId(blocker.paymentRequestId);
    try {
      // Fetch payment request to get rowVersion
      const paymentRequest = await api.get<ProjectPaymentRequest>(`/api/projects/${encodeURIComponent(projectId)}/payment-requests/${encodeURIComponent(blocker.paymentRequestId)}`);
      await cancelPaymentRequest.mutateAsync({ id: blocker.paymentRequestId, rowVersion: paymentRequest.rowVersion });
      const refreshed = await replacementPreview.refetch();
      setReplaceError(refreshed.data?.blockers.length ? t("projects.plannedPaymentActiveRequestsBlock") : null);
    } catch (error) {
      setReplaceError(actionError(error, t));
    } finally {
      setResolvingBlockerId(null);
    }
  };

  const handleCreateReversal = async (blocker: PlannedPaymentReplacementPreviewBlocker) => {
    setResolvingBlockerId(blocker.paymentRequestId);
    try {
      const amount = Math.max(0, blocker.outstandingAmount - blocker.approvedReversalAmount - blocker.pendingReversalAmount);
      if (amount <= 0) {
        setReplaceError(t("projects.plannedPaymentReversalNoAmount"));
        return;
      }
      await createReversal.mutateAsync({
        requestId: blocker.paymentRequestId,
        amount,
        reason: t("projects.plannedPaymentReversalForReplacement"),
        idempotencyKey: newKey(),
      });
      await replacementPreview.refetch();
      setReplaceError(t("projects.plannedPaymentReversalPendingApproval"));
    } catch (error) {
      setReplaceError(actionError(error, t));
    } finally {
      setResolvingBlockerId(null);
    }
  };

  const preview = replacementPreviewIsCurrent ? currentReplacementPreview : undefined;
  const previewCurrency = preview?.currency || currency;
  const availableBeforeText = preview && previewCurrency ? formatCurrency(preview.availableLifetimeBudgetBefore, previewCurrency) : "-";
  const availableAfterText = preview && previewCurrency ? formatCurrency(preview.availableLifetimeBudgetAfter, previewCurrency) : "-";
  const netChange = preview?.netLifetimeBudgetChange ?? null;
  const netChangeText = netChange !== null && previewCurrency
    ? `${netChange >= 0 ? "+" : ""}${formatCurrency(netChange, previewCurrency)}`
    : "-";

  return (
    <Card>
      <CardHeader className="finance-section-head">
        <div>
          <CardTitle className="flex items-center gap-2 text-lg"><CalendarClock size={18} aria-hidden="true" />{t("projects.plannedPaymentsHeading")}</CardTitle>
          <p className="mt-1 text-xs text-muted-foreground">{t("projects.plannedPaymentsDescription")}</p>
        </div>
        {canSubmit ? <Button size="sm" onClick={() => { setDraft(emptyDraft()); setFormError(null); setRegisterOpen(true); }}>
          <Plus size={16} />{t("projects.addPlannedPayment")}
        </Button> : null}
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">{t("projects.plannedPaymentLifetimeCeiling")}</p>
            <p className="mt-1 font-semibold tabular-nums"><bdi>{budget.isLoading ? t("common.loading") : money(budget.data?.lifetimeCeiling, currency, formatCurrency)}</bdi></p>
          </div>
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">{t("projects.plannedPaymentLifetimeConsumed")}</p>
            <p className="mt-1 font-semibold tabular-nums"><bdi>{budget.isLoading ? t("common.loading") : money(budget.data?.lifetimeConsumed, currency, formatCurrency)}</bdi></p>
          </div>
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">{t("projects.plannedPaymentReserved")}</p>
            <p className="mt-1 font-semibold tabular-nums"><bdi>{budget.isLoading ? t("common.loading") : money(budget.data?.lifetimeReserved, currency, formatCurrency)}</bdi></p>
          </div>
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">{t("projects.plannedPaymentAvailable")}</p>
            <p className="mt-1 font-semibold tabular-nums"><bdi>{budget.isLoading ? t("common.loading") : money(budget.data?.availableLifetimeBudget, currency, formatCurrency)}</bdi></p>
          </div>
        </div>
        {budget.isError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("projects.plannedPaymentBudgetLoadFailed")}</p> : null}

        <div className="overflow-x-auto">
          <Table className="finance-ledger">
            <TableHeader>
              <TableRow>
                <TableHead>{t("projects.reference")}</TableHead>
                <TableHead>{t("projects.description")}</TableHead>
                <TableHead className="text-right">{t("projects.amount")}</TableHead>
                <TableHead>{t("common.status")}</TableHead>
                <TableHead className="text-right">{t("projects.plannedPaymentInstallments")}</TableHead>
                <TableHead className="text-right">{t("projects.plannedPaymentSettled")}</TableHead>
                <TableHead className="text-right">{t("projects.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {plans.isLoading ? <TableRow><TableCell colSpan={7} className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</TableCell></TableRow>
                : plans.isError ? <TableRow><TableCell colSpan={7} className="p-6 text-center"><div className="flex flex-col items-center gap-2"><p role="alert" className="text-sm text-destructive">{t("projects.plannedPaymentLoadFailed")}</p><Button size="sm" variant="outline" onClick={() => void plans.refetch()}>{t("common.retry")}</Button></div></TableCell></TableRow>
                  : items.length === 0 ? <TableRow><TableCell colSpan={7} className="p-6 text-center text-sm text-muted-foreground">{t("projects.plannedPaymentEmpty")}</TableCell></TableRow>
                    : items.map((plan) => <TableRow key={plan.id}>
                      <TableCell className="font-medium"><bdi>{plan.reference}</bdi></TableCell>
                      <TableCell>{plan.description || "—"}</TableCell>
                      <TableCell className="text-right tabular-nums"><bdi>{money(plan.totalAmount, plan.currency, formatCurrency)}</bdi></TableCell>
                      <TableCell><Badge className="finance-status" status={plan.state}>{stateLabel(t, plan.state)}</Badge></TableCell>
                      <TableCell className="text-right tabular-nums">{plan.installments.length}</TableCell>
                      <TableCell className="text-right tabular-nums"><bdi>{money(plan.settledAmount, plan.currency, formatCurrency)}</bdi></TableCell>
                      <TableCell className="text-right">
                        <Button size="sm" variant="outline" onClick={() => setDetail(plan)}>{t("projects.viewDetails")}</Button>
                        {canReplace && plan.isLocked && plan.replacedByPlannedPaymentId === null && plan.totalAmount - plan.settledAmount > 0 ? (
                          <Button size="sm" variant="secondary" className="ms-2" onClick={() => openReplace(plan)}>
                            <RotateCcw className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.plannedPaymentReplace")}
                          </Button>
                        ) : null}
                      </TableCell>
                    </TableRow>)}
            </TableBody>
          </Table>
        </div>
        {totalPages > 1 ? <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-muted-foreground">{t("projects.pageOf", { page, totalPages })}</span>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((value) => Math.max(1, value - 1))}>{t("common.previous")}</Button>
            <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>{t("common.next")}</Button>
          </div>
        </div> : null}
      </CardContent>

      <SideSheet open={detail !== null} onOpenChange={(open) => { if (!open) setDetail(null); }} title={detail?.reference ?? ""} description={detail?.description ?? undefined} size="xl">
        <SideSheetBody className="space-y-4">
          {detail?.isLocked ? <p role="status" className="flex items-start gap-2 rounded-md border border-amber-500/30 bg-amber-500/10 p-3 text-sm"><LockKeyhole className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" /><span>{t("projects.plannedPaymentLocked")}</span></p> : null}
          {detail ? <>
            <div className="grid gap-3 sm:grid-cols-3">
              <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{t("projects.plannedPaymentReserved")}</p><p className="font-semibold tabular-nums"><bdi>{money(detail.outstandingReservedAmount, detail.currency, formatCurrency)}</bdi></p></div>
              <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{t("projects.plannedPaymentSettled")}</p><p className="font-semibold tabular-nums"><bdi>{money(detail.settledAmount, detail.currency, formatCurrency)}</bdi></p></div>
              <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{t("projects.plannedPaymentRegisteredAt")}</p><p className="font-semibold">{detail.registeredAtUtc ? formatDate(detail.registeredAtUtc) : "—"}</p></div>
            </div>
            <div className="overflow-x-auto">
              <Table className="finance-ledger">
                <TableHeader><TableRow>
                  <TableHead>{t("projects.plannedPaymentInstallmentNumber")}</TableHead>
                  <TableHead className="text-right">{t("projects.amount")}</TableHead>
                  <TableHead>{t("projects.dueDate")}</TableHead>
                  <TableHead>{t("projects.milestone")}</TableHead>
                  <TableHead className="text-right">{t("projects.plannedPaymentRequested")}</TableHead>
                  <TableHead className="text-right">{t("projects.plannedPaymentSettled")}</TableHead>
                  <TableHead className="text-right">{t("projects.plannedPaymentRemainingRequestable")}</TableHead>
                  <TableHead className="text-right">{t("projects.actions")}</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {detail.installments.map((installment) => <TableRow key={installment.id}>
                    <TableCell>{installment.sequence}</TableCell>
                    <TableCell className="text-right tabular-nums"><bdi>{money(installment.amount, detail.currency, formatCurrency)}</bdi></TableCell>
                    <TableCell>{installment.dueDateUtc ? formatDate(installment.dueDateUtc) : "—"}</TableCell>
                    <TableCell>{installment.planActivityName || "—"}</TableCell>
                    <TableCell className="text-right tabular-nums"><bdi>{money(installment.requestedAmount, detail.currency, formatCurrency)}</bdi></TableCell>
                    <TableCell className="text-right tabular-nums"><bdi>{money(installment.settledAmount, detail.currency, formatCurrency)}</bdi></TableCell>
                    <TableCell className="text-right tabular-nums"><bdi>{money(installment.remainingRequestableAmount, detail.currency, formatCurrency)}</bdi></TableCell>
                    <TableCell className="text-right">
                      {canCreateRequest && installment.remainingRequestableAmount > 0 ? <Button
                        size="sm"
                        onClick={() => onCreateRequest({
                          installmentId: installment.id,
                          installmentSequence: installment.sequence,
                          installmentAmount: installment.amount,
                          remainingRequestableAmount: installment.remainingRequestableAmount,
                          currency: detail.currency,
                          planReference: detail.reference,
                          planDescription: detail.description,
                          dueDateUtc: installment.dueDateUtc,
                          planActivityId: installment.planActivityId,
                          planActivityName: installment.planActivityName,
                        })}
                      ><Send className="me-1.5 h-4 w-4" aria-hidden="true" />{t("projects.paymentRequestFromExistingPlannedPayment")}</Button> : <span className="text-xs text-muted-foreground">-</span>}
                    </TableCell>
                  </TableRow>)}
                </TableBody>
              </Table>
            </div>
          </> : null}
        </SideSheetBody>
      </SideSheet>

      <SideSheet open={registerOpen} onOpenChange={(open) => { if (!open) closeRegister(); }} title={t("projects.addPlannedPayment")} description={t("projects.plannedPaymentFormDescription")} size="xl">
        <form onSubmit={(event) => void submitRegister(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-5">
            <div>
              <Label htmlFor="planned-payment-description">{t("projects.description")} *</Label>
              <Textarea id="planned-payment-description" value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} rows={2} required />
            </div>
            <div className="space-y-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.plannedPaymentInstallments")}</h3>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-muted-foreground">{t("projects.plannedPaymentTotal")}: <bdi>{currency ? formatCurrency(draftTotal, currency) : "—"}</bdi></span>
                  <Button type="button" size="sm" variant="outline" onClick={() => setDraft((current) => ({ ...current, installments: [...current.installments, { key: newKey(), amount: "", dueDate: "", planActivityId: "", description: "" }] }))}>
                    <Plus size={14} />{t("projects.addInstallment")}
                  </Button>
                </div>
              </div>
              {draft.installments.map((installment, index) => <div key={installment.key} className="grid gap-3 rounded-md border p-3 sm:grid-cols-2 xl:grid-cols-5">
                <div><Label htmlFor={`installment-amount-${installment.key}`}>{t("projects.amount")} *</Label><Input id={`installment-amount-${installment.key}`} type="number" min="0.0001" step="0.0001" value={installment.amount} onChange={(event) => updateInstallment(installment.key, { amount: event.target.value })} required /></div>
                <div><Label htmlFor={`installment-due-${installment.key}`}>{t("projects.dueDate")} *</Label><Input id={`installment-due-${installment.key}`} type="date" value={installment.dueDate} onChange={(event) => updateInstallment(installment.key, { dueDate: event.target.value })} required /></div>
                <div><Label htmlFor={`installment-milestone-${installment.key}`}>{t("projects.milestone")}</Label>
                  <select id={`installment-milestone-${installment.key}`} className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm" value={installment.planActivityId} onChange={(event) => updateInstallment(installment.key, { planActivityId: event.target.value })}>
                    <option value="">{t("projects.noMilestone")}</option>
                    {(milestones.data ?? []).map((option) => <option key={option.id} value={option.id}>{option.name}</option>)}
                  </select>
                </div>
                <div><Label htmlFor={`installment-description-${installment.key}`}>{t("projects.description")}</Label><Input id={`installment-description-${installment.key}`} value={installment.description} onChange={(event) => updateInstallment(installment.key, { description: event.target.value })} maxLength={2000} /></div>
                <div className="flex items-end justify-end">
                  <Button type="button" size="icon" variant="ghost" aria-label={t("projects.removeInstallment", { number: index + 1 })} disabled={draft.installments.length === 1} onClick={() => setDraft((current) => ({ ...current, installments: current.installments.filter((row) => row.key !== installment.key) }))}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </div>)}
              {milestones.isError ? <p role="status" className="text-xs text-muted-foreground">{t("projects.plannedPaymentMilestonesUnavailable")}</p> : null}
            </div>
            <div><Label>{t("projects.currencyLabel")}</Label><p className="rounded-md border px-3 py-2 text-sm font-medium">{currency || t("projects.financialCurrencyUnavailable")}</p></div>
            {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
          </SideSheetBody>
          <SideSheetFooter className="finance-form-actions">
            <Button type="button" variant="outline" onClick={closeRegister}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={!canSubmit || register.isPending}>{register.isPending ? t("common.saving") : t("projects.registerPlannedPayment")}</Button>
          </SideSheetFooter>
        </form>
      </SideSheet>

      <SideSheet open={replaceOpen} onOpenChange={(open) => { if (!open) closeReplace(); }} title={t("projects.plannedPaymentReplaceTitle")} description={t("projects.plannedPaymentReplaceDescription")} size="xl">
        <form onSubmit={(event) => void submitReplace(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-5">
            {detail ? <>
              <div className="rounded-md border bg-amber-50 p-3">
                <p className="text-xs font-medium text-amber-800">{t("projects.plannedPaymentReplaceOriginal")}</p>
                <div className="grid gap-2 sm:grid-cols-3 mt-1 text-sm">
                  <div><p className="text-muted-foreground">{t("projects.plannedPaymentOriginalAmount")}</p><p className="font-medium tabular-nums">{preview ? money(preview.currentCommitment, preview.currency, formatCurrency) : "-"}</p></div>
                  <div><p className="text-muted-foreground">{t("projects.plannedPaymentHistoricalPaid")}</p><p className="font-medium tabular-nums">{preview ? money(preview.historicalPaid, preview.currency, formatCurrency) : "-"}</p></div>
                  <div><p className="text-muted-foreground">{t("projects.plannedPaymentRemainingUnpaid")}</p><p className="font-medium tabular-nums">{preview ? money(preview.currentOutstandingCommitment, preview.currency, formatCurrency) : "-"}</p></div>
                </div>
                {replacementPreview.isLoading ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("projects.plannedPaymentPreviewLoading")}</p> : null}
                {replacementPreview.isError ? <p role="alert" className="mt-2 text-xs text-destructive">{t("projects.plannedPaymentPreviewLoadFailed")}</p> : null}
                {!replacementPreview.isLoading && !replacementPreview.isError && !preview ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("projects.plannedPaymentPreviewEnterValidAmount")}</p> : null}
              </div>
              {preview && !preview.hasSufficientLifetimeBudget ? <p role="status" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("projects.plannedPaymentInsufficientBudget")}</p> : null}
              {replaceBlockers.length > 0 && (
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <AlertTriangle className="h-4 w-4 text-amber-600" aria-hidden="true" />
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-amber-800">{t("projects.plannedPaymentActiveRequestsBlock")}</h3>
                  </div>
                  <div className="rounded-md border border-amber-200 bg-amber-50 p-3 space-y-2">
                    {replaceBlockers.map((blocker) => {
                      const statusKey = `projects.paymentRequestStatus${blocker.status}`;
                      const translatedStatus = t(statusKey);
                      const reversalAmount = Math.max(0, blocker.outstandingAmount - blocker.approvedReversalAmount - blocker.pendingReversalAmount);
                      return (
                      <div key={blocker.paymentRequestId} className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 p-2 rounded border bg-white">
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="text-xs font-medium">{blocker.paymentRequestReference}</span>
                            <Badge variant="outline" className="text-xs">
                              {translatedStatus === statusKey ? blocker.status : translatedStatus}
                            </Badge>
                          </div>
                          <p className="text-xs text-muted-foreground">{t("projects.plannedPaymentBlockerOutstanding")}: {formatCurrency(blocker.outstandingAmount, preview?.currency ?? detail.currency)}</p>
                          {blocker.approvedReversalAmount > 0 ? <p className="text-xs text-muted-foreground">{t("projects.plannedPaymentBlockerApprovedReversal")}: {formatCurrency(blocker.approvedReversalAmount, preview?.currency ?? detail.currency)}</p> : null}
                          {blocker.pendingReversalAmount > 0 ? <p role="status" className="text-xs text-muted-foreground">{t("projects.plannedPaymentBlockerPendingReversal")}: {formatCurrency(blocker.pendingReversalAmount, preview?.currency ?? detail.currency)}</p> : null}
                        </div>
                        <div className="flex items-center gap-2">
                          {blocker.status === "Draft" || blocker.status === "Pending" ? (
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={resolvingBlockerId === blocker.paymentRequestId || cancelPaymentRequest.isPending}
                              onClick={() => handleCancelPaymentRequest(blocker)}
                            >
                              {resolvingBlockerId === blocker.paymentRequestId ? <Loader2 className="h-4 w-4 animate-spin" /> : <XCircle className="me-1.5 h-4 w-4" />}
                              {t("projects.plannedPaymentCancelRequest")}
                            </Button>
                          ) : blocker.status === "Approved" && reversalAmount > 0 && blocker.pendingReversalAmount <= 0 ? (
                            <Button
                              size="sm"
                              variant="destructive"
                              disabled={resolvingBlockerId === blocker.paymentRequestId || createReversal.isPending}
                              onClick={() => handleCreateReversal(blocker)}
                            >
                              {resolvingBlockerId === blocker.paymentRequestId ? <Loader2 className="h-4 w-4 animate-spin" /> : <ArrowUpRight className="me-1.5 h-4 w-4" />}
                              {t("projects.plannedPaymentCreateReversal")}
                            </Button>
                          ) : blocker.pendingReversalAmount > 0 ? (
                            <Badge variant="secondary" className="text-xs">{t("projects.plannedPaymentReversalPendingApproval")}</Badge>
                          ) : (
                            <Badge variant="secondary" className="text-xs">{t("projects.plannedPaymentManualResolutionRequired")}</Badge>
                          )}
                        </div>
                      </div>
                      );
                    })}
                  </div>
                </div>
              )}
              <div className="space-y-3">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.plannedPaymentReplaceNewCommitment")}</h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1.5"><Label htmlFor="replacement-new-amount">{t("projects.plannedPaymentNewRemainingCommitment")} *</Label><Input id="replacement-new-amount" type="number" min="0.0001" step="0.0001" value={replaceDraft.newRemainingCommitment} onChange={(event) => setReplaceDraft({ ...replaceDraft, newRemainingCommitment: event.target.value })} required /></div>
                  <div className="space-y-1.5"><Label>{t("projects.plannedPaymentEligibleOldUnpaid")}</Label><p className="rounded-md border px-3 py-2 text-sm font-medium tabular-nums">{preview ? formatCurrency(preview.currentOutstandingCommitment, preview.currency) : "-"}</p></div>
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  <div className="space-y-1.5"><Label>{t("projects.plannedPaymentAvailableBeforeReplacement")}</Label><p className="rounded-md border px-3 py-2 text-sm font-medium tabular-nums">{availableBeforeText}</p></div>
                  <div className="space-y-1.5"><Label>{t("projects.plannedPaymentNetLifetimeBudgetChange")}</Label>
                    <p className="rounded-md border px-3 py-2 text-sm font-medium tabular-nums" style={{ color: netChange !== null && netChange > 0 ? "var(--destructive)" : netChange !== null && netChange < 0 ? "var(--success)" : "var(--muted-foreground)" }}>{netChangeText}</p>
                  </div>
                  <div className="space-y-1.5"><Label>{t("projects.plannedPaymentAvailableAfterReplacement")}</Label>
                    <p className="rounded-md border px-3 py-2 text-sm font-medium tabular-nums" style={{ color: preview && preview.availableLifetimeBudgetAfter < 0 ? "var(--destructive)" : "var(--success)" }}>{availableAfterText}</p>
                  </div>
                </div>
              </div>
              <div className="space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.plannedPaymentReplacementInstallments")}</h3>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-muted-foreground">{t("projects.plannedPaymentTotal")}: <bdi>{currency ? formatCurrency(replacementInstallmentTotal, currency) : "—"}</bdi></span>
                    <Button type="button" size="sm" variant="outline" onClick={() => setReplaceDraft((current) => ({ ...current, installments: [...current.installments, { key: newKey(), amount: "", dueDate: "", planActivityId: "", description: "" }] }))}>
                      <Plus size={14} />{t("projects.addInstallment")}
                    </Button>
                  </div>
                </div>
                {replaceDraft.installments.map((installment, index) => <div key={installment.key} className="grid gap-3 rounded-md border p-3 sm:grid-cols-2 xl:grid-cols-5">
                  <div><Label htmlFor={`replacement-amount-${installment.key}`}>{t("projects.amount")} *</Label><Input id={`replacement-amount-${installment.key}`} type="number" min="0.0001" step="0.0001" value={installment.amount} onChange={(event) => updateReplacementInstallment(installment.key, { amount: event.target.value })} required /></div>
                  <div><Label htmlFor={`replacement-due-${installment.key}`}>{t("projects.dueDate")} *</Label><Input id={`replacement-due-${installment.key}`} type="date" value={installment.dueDate} onChange={(event) => updateReplacementInstallment(installment.key, { dueDate: event.target.value })} required /></div>
                  <div><Label htmlFor={`replacement-milestone-${installment.key}`}>{t("projects.milestone")}</Label>
                    <select id={`replacement-milestone-${installment.key}`} className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm" value={installment.planActivityId} onChange={(event) => updateReplacementInstallment(installment.key, { planActivityId: event.target.value })}>
                      <option value="">{t("projects.noMilestone")}</option>
                      {(milestones.data ?? []).map((option) => <option key={option.id} value={option.id}>{option.name}</option>)}
                    </select>
                  </div>
                  <div><Label htmlFor={`replacement-description-${installment.key}`}>{t("projects.description")}</Label><Input id={`replacement-description-${installment.key}`} value={installment.description} onChange={(event) => updateReplacementInstallment(installment.key, { description: event.target.value })} maxLength={2000} /></div>
                  <div className="flex items-end justify-end">
                    <Button type="button" size="icon" variant="ghost" aria-label={t("projects.removeInstallment", { number: index + 1 })} disabled={replaceDraft.installments.length === 1} onClick={() => setReplaceDraft((current) => ({ ...current, installments: current.installments.filter((row) => row.key !== installment.key) }))}>
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </div>)}
                {milestones.isError ? <p role="status" className="text-xs text-muted-foreground">{t("projects.plannedPaymentMilestonesUnavailable")}</p> : null}
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="replacement-reason">{t("projects.plannedPaymentReplacementReason")} *</Label>
                <Textarea id="replacement-reason" value={replaceDraft.reason} onChange={(event) => setReplaceDraft({ ...replaceDraft, reason: event.target.value })} rows={3} required />
              </div>
              <div><Label>{t("projects.currencyLabel")}</Label><p className="rounded-md border px-3 py-2 text-sm font-medium">{currency || t("projects.financialCurrencyUnavailable")}</p></div>
              {replaceError ? <p role="alert" className="text-sm text-destructive">{replaceError}</p> : null}
            </> : null}
          </SideSheetBody>
          <SideSheetFooter className="finance-form-actions">
            <Button type="button" variant="outline" onClick={closeReplace}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={!canSubmitReplace || replace.isPending}>{replace.isPending ? t("common.saving") : t("projects.plannedPaymentReplaceConfirm")}</Button>
          </SideSheetFooter>
        </form>
      </SideSheet>
    </Card>
  );
}
