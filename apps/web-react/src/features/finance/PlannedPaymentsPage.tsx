import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { ArrowUpRight, History, Plus, Trash2, Wallet } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Pagination } from "@components/ui/Pagination";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Textarea } from "@components/ui/Textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { PageHeader } from "@components/layout/PageHeader";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import { useManagement } from "@api/management";
import { usePaymentProjectLookup } from "@features/projects/api/projects";
import { usePlanningBootstrapFull } from "@features/projects/api/planningFull";
import {
  usePaymentObligationMutations,
  usePaymentObligations,
  usePaymentSettlements,
  type PaymentObligationDto,
} from "./api/finance";
import { useProjectFinancialFoundation, useProjectPaymentRequests } from "@features/projects/api/project-financials";

type PaymentForm = {
  milestoneId: string;
  paymentReference: string;
  purpose: string;
  description: string;
  amount: string;
  currency: string;
  dueDate: string;
  notes: string;
};

type SettlementForm = {
  amount: string;
  settlementDate: string;
  paymentReference: string;
  notes: string;
};

const today = () => new Date().toISOString().slice(0, 10);
const emptyPayment = (currency = ""): PaymentForm => ({
  milestoneId: "",
  paymentReference: "",
  purpose: "",
  description: "",
  amount: "",
  currency,
  dueDate: "",
  notes: "",
});
const emptySettlement = (): SettlementForm => ({
  amount: "",
  settlementDate: today(),
  paymentReference: "",
  notes: "",
});

function errorMessage(error: unknown, fallback: string) {
  return error instanceof Error && error.message ? error.message : fallback;
}

// Links from Project Details carry the public id, while payment writes use the internal id.
function sameId(left: string, right: string) {
  return Boolean(left) && Boolean(right) && left.toLowerCase() === right.toLowerCase();
}

export function PlannedPaymentsPage() {
  const { t, formatCurrency } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const [searchParams] = useSearchParams();
  const permissions = user?.permissions ?? [];
  const canView = Boolean(user && hasPermission(permissions, Permissions.PaymentObligation.View));
  const canCreate = Boolean(user && hasPermission(permissions, Permissions.PaymentObligation.Create) && canMutate && !effectiveReadOnly);
  const canEdit = Boolean(user && hasPermission(permissions, Permissions.PaymentObligation.Edit) && canMutate && !effectiveReadOnly);
  const canDelete = Boolean(user && hasPermission(permissions, Permissions.PaymentObligation.Delete) && canMutate && !effectiveReadOnly);
  const canRecordSettlements = Boolean(user && hasPermission(permissions, Permissions.PaymentObligationSettlement.Record) && canMutate && !effectiveReadOnly);
  const canLoadProjectOptions = Boolean(user && hasAnyPermission(permissions, [
    Permissions.PaymentObligation.View,
    Permissions.PaymentObligation.Create,
    Permissions.PaymentObligation.Edit,
  ]));
  const projects = usePaymentProjectLookup(canLoadProjectOptions);
  const projectId = searchParams.get("projectId") ?? "";
  const project = (projects.data ?? []).find((item) => sameId(item.id, projectId) || sameId(item.publicId, projectId));
  const isCurrentProjectPayment = React.useCallback((payment: PaymentObligationDto) =>
    Boolean(project) && (sameId(payment.projectId, project!.id) || sameId(payment.projectId, project!.publicId)), [project]);
  const [dueOnly, setDueOnly] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [paymentOpen, setPaymentOpen] = React.useState(false);
  const [settlementOpen, setSettlementOpen] = React.useState(false);
  const [detailsOpen, setDetailsOpen] = React.useState(false);
  const [selectedPayment, setSelectedPayment] = React.useState<PaymentObligationDto | null>(null);
  const [editingPayment, setEditingPayment] = React.useState<PaymentObligationDto | null>(null);
  const [paymentForm, setPaymentForm] = React.useState<PaymentForm>(emptyPayment);
  const [settlementForm, setSettlementForm] = React.useState<SettlementForm>(emptySettlement);
  const [idempotencyKey, setIdempotencyKey] = React.useState(() => crypto.randomUUID());
  const [actionError, setActionError] = React.useState<string | null>(null);
  // Track whether the user has manually edited the due date (vs. it being auto-filled from a milestone).
  // We use a timestamp-based dirty flag: when the user changes the due date input, we record the time.
  // Auto-fill from milestone only happens when the field is still at its "clean" state (no user interaction yet).
  const [dueDateDirty, setDueDateDirty] = React.useState(false);

  // Budget availability from the project financial foundation
  const foundation = useProjectFinancialFoundation(project?.publicId ?? "");
  const paymentRequests = useProjectPaymentRequests(project?.publicId ?? "");
  // Planning data for milestone date prefill
  const planning = usePlanningBootstrapFull(project?.publicId ?? "");
  // Payment obligations for the current project (project-scoped)
  const { data: paymentsData, isLoading: paymentsLoading, isError: paymentsError, refetch: paymentsRefetch } = usePaymentObligations({
    page,
    pageSize: 20,
    ...(project ? { projectId: project.publicId } : {}),
    ...(dueOnly ? { dueOnly: true } : {}),
  }, canView && Boolean(project));

  // Derived values for budget calculation
  const approvedBudget = React.useMemo(() => {
    if (foundation.data) return foundation.data.approvedBudget ?? null;
    return null;
  }, [foundation.data]);
  // Payment requests list with budget availability
  const requestsList = React.useMemo(() => {
    if (paymentRequests.data?.items) return paymentRequests.data.items;
    return [];
  }, [paymentRequests.data]);

  // Active (non-cancelled, non-rejected) payment obligations for the current project
  const activePayments = React.useMemo(() => {
    if (!project) return [];
    return (paymentsData?.items ?? []).filter((payment) => {
      // Include payments that are active (not cancelled/rejected)
      const activeLifecycle = payment.lifecycleStatus !== "Cancelled" && payment.lifecycleStatus !== "Rejected";
      const isCurrent = isCurrentProjectPayment(payment);
      return activeLifecycle && isCurrent;
    });
  }, [project, paymentsData, isCurrentProjectPayment]);

  // Calculate total of existing planned payments (active only)
  const existingPlannedPaymentsTotal = React.useMemo(() => {
    if (!project) return 0;
    return activePayments.reduce((sum, p) => sum + p.amount, 0);
  }, [project, activePayments]);

  // Calculate total of approved payment requests that reserve budget
  const approvedPaymentRequestsTotal = React.useMemo(() => {
    if (!project) return 0;
    return requestsList
      .filter((req) => req.status === "Approved" && req.reservesBudget)
      .reduce((sum, req) => sum + req.amount, 0);
  }, [project, requestsList]);
  const mutations = usePaymentObligationMutations();
  const settlements = usePaymentSettlements(selectedPayment?.id ?? "", detailsOpen);
  const milestones = (planning.data?.activities ?? []).filter((activity) => activity.type === "Milestone");
  // Defense in depth: never render or act on a record outside the current project,
  // even if a stale cache or an unexpected API response contains one.
  const paymentsList = (paymentsData?.items ?? []).filter(isCurrentProjectPayment);
  const activePayment = paymentsList.find((item) => item.id === selectedPayment?.id)
    ?? (selectedPayment && isCurrentProjectPayment(selectedPayment) ? selectedPayment : null);
  const activeMilestone = activePayment?.milestoneId
    ? planning.data?.activities.find((activity) => activity.id === activePayment.milestoneId)
    : undefined;

  React.useEffect(() => {
    setPage(1);
    setPaymentOpen(false);
    setSettlementOpen(false);
    setDetailsOpen(false);
    setSelectedPayment(null);
    setEditingPayment(null);
    setPaymentForm(emptyPayment());
    setActionError(null);
  }, [projectId]);

  // Auto-fill due date from selected milestone.
  // - When a milestone is selected, use its endDate (scheduled finish/target date).
  // - Do not overwrite if the user has manually edited the due date.
  // - In edit mode (editingPayment is set), preserve the existing saved due date on initial load.
  React.useEffect(() => {
    if (editingPayment) {
      // In edit mode, do not auto-fill from milestone - preserve the existing saved date.
      // The dueDateDirty flag is also irrelevant here since we're editing an existing payment.
      return;
    }
    // Find the selected milestone's endDate.
    const selectedMilestoneId = paymentForm.milestoneId;
    if (!selectedMilestoneId) {
      // No milestone selected; clear the dirty flag if no milestone is selected.
      setDueDateDirty(false);
      return;
    }
    const milestone = (planning.data?.activities ?? []).find(
      (activity) => activity.type === "Milestone" && activity.id === selectedMilestoneId
    );
    if (!milestone) {
      setDueDateDirty(false);
      return;
    }
    const milestoneDate = milestone.endDate ?? milestone.baselineFinishDate ?? milestone.startDate ?? milestone.baselineStartDate;
    if (!milestoneDate) {
      // Milestone has no valid date - allow manual entry without inventing a default.
      setDueDateDirty(false);
      return;
    }
    // Only auto-fill if the due date is still "clean" (user hasn't manually edited it).
    // We compare the current dueDate with what it would be after auto-filling.
    // Since the input is uncontrolled in terms of our state (we use paymentForm.dueDate),
    // we check the dirty flag: if user has interacted, don't overwrite.
    if (!dueDateDirty) {
      setPaymentForm((current) => current.milestoneId === selectedMilestoneId
        ? { ...current, dueDate: milestoneDate.slice(0, 10) }
        : current);
    }
  }, [paymentForm.milestoneId, planning.data, dueDateDirty, editingPayment]);

  function openCreate() {
    if (!project) return;
    setEditingPayment(null);
    setPaymentForm(emptyPayment(project.currency ?? undefined));
    setActionError(null);
    setPaymentOpen(true);
  }

  function openEdit(payment: PaymentObligationDto) {
    if (!project || !isCurrentProjectPayment(payment)) return;
    setSelectedPayment(payment);
    setDetailsOpen(false);
    setEditingPayment(payment);
    setPaymentForm({
      milestoneId: payment.milestoneId ?? "",
      paymentReference: payment.paymentReference,
      purpose: payment.purpose,
      description: payment.description,
      amount: String(payment.amount),
      currency: payment.currency || project.currency || "",
      dueDate: payment.dueDate.slice(0, 10),
      notes: payment.notes ?? "",
    });
    setActionError(null);
    setPaymentOpen(true);
  }

  async function savePayment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!project || (editingPayment && !isCurrentProjectPayment(editingPayment))) return;
    setActionError(null);
    const paymentCurrency = paymentForm.currency.trim();
    if (!/^[A-Z]{3}$/i.test(paymentCurrency)) {
      setActionError(t("payments.currencyUnavailable"));
      return;
    }
    const body = {
      projectId: project.id,
      milestoneId: paymentForm.milestoneId || undefined,
      paymentReference: paymentForm.paymentReference.trim(),
      purpose: paymentForm.purpose.trim(),
      description: paymentForm.description.trim() || undefined,
      amount: Number(paymentForm.amount),
      currency: paymentCurrency.toUpperCase(),
      dueDate: `${paymentForm.dueDate}T00:00:00Z`,
      notes: paymentForm.notes.trim() || undefined,
    };

    try {
      if (editingPayment) {
        await mutations.update.mutateAsync({ id: editingPayment.id, body: { ...body, rowVersion: editingPayment.rowVersion } });
      } else {
        await mutations.create.mutateAsync(body);
      }
      setPaymentOpen(false);
    } catch (error) {
      setActionError(errorMessage(error, t("payments.saveFailed")));
    }
  }

  function openSettlement(payment: PaymentObligationDto) {
    if (!isCurrentProjectPayment(payment)) return;
    setSelectedPayment(payment);
    setSettlementForm(emptySettlement());
    setIdempotencyKey(crypto.randomUUID());
    setActionError(null);
    setSettlementOpen(true);
  }

  async function saveSettlement(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!activePayment) return;
    setActionError(null);
    try {
      await mutations.settle.mutateAsync({
        id: activePayment.id,
        body: {
          idempotencyKey,
          amount: Number(settlementForm.amount),
          settlementDate: `${settlementForm.settlementDate}T00:00:00Z`,
          paymentReference: settlementForm.paymentReference.trim(),
          notes: settlementForm.notes.trim() || undefined,
        },
      });
      setSettlementOpen(false);
      setDetailsOpen(true);
      setIdempotencyKey(crypto.randomUUID());
    } catch (error) {
      setActionError(errorMessage(error, t("payments.settlementFailed")));
    }
  }

  async function deletePayment(payment: PaymentObligationDto) {
    if (!isCurrentProjectPayment(payment)) return;
    if (!await confirm({ description: t("payments.deleteConfirm", { reference: payment.paymentReference }), destructive: true })) return;
    setActionError(null);
    try {
      await mutations.delete.mutateAsync({ id: payment.id, rowVersion: payment.rowVersion });
      setDetailsOpen(false);
      setSelectedPayment(null);
    } catch (error) {
      setActionError(errorMessage(error, t("payments.deleteFailed")));
    }
  }

  const projectContext = project
    ? `${t("payments.project")}: ${project.name}${project.code ? ` (${project.code})` : ""}`
    : t("payments.description");
  const projectCurrency = project?.currency?.trim() ?? "";
  const remainingLoading = Boolean(project) && (foundation.isLoading || paymentRequests.isLoading);
  // Never invent a currency: without the project's authoritative currency, show an unavailable state.
  const formatProjectMoney = (value: number | null) =>
    value == null || !projectCurrency ? t("common.notAvailable") : formatCurrency(value, projectCurrency);
  const proposedAmount = Number(paymentForm.amount);
  const proposedAmountText = !Number.isFinite(proposedAmount)
    ? t("common.notAvailable")
    : paymentForm.currency
      ? formatCurrency(proposedAmount, paymentForm.currency)
      : t("common.notAvailable");

  // Budget availability calculation
  const remainingAvailable = React.useMemo(() => {
    if (!project || approvedBudget == null) return null;
    // Start with approved budget, subtract active planned payments and approved payment requests
    const activePaymentsSubtotal = existingPlannedPaymentsTotal; // already excludes cancelled/rejected
    const paymentRequestsSubtotal = approvedPaymentRequestsTotal; // already filters Approved + reservesBudget
    return approvedBudget - activePaymentsSubtotal - paymentRequestsSubtotal;
  }, [project, approvedBudget, existingPlannedPaymentsTotal, approvedPaymentRequestsTotal]);

  const projectedRemaining = React.useMemo(() => {
    if (remainingAvailable == null || paymentForm.amount === "") return null;
    const amount = Number(paymentForm.amount);
    return remainingAvailable - amount;
  }, [remainingAvailable, paymentForm.amount]);

  const hasExceededAvailable = React.useMemo(() => {
    if (remainingAvailable == null || paymentForm.amount === "") return false;
    return Number(paymentForm.amount) > remainingAvailable;
  }, [remainingAvailable, paymentForm.amount]);

  if (!canView && !canCreate) {
    return <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("projects.financialsPermissionRequired")}</CardContent></Card>;
  }

  return (
    <div className="space-y-6">
      {projects.isError ? <div role="alert" className="flex items-center justify-between gap-2 text-sm text-destructive"><span>{t("common.errorLoading")}</span><Button type="button" size="sm" variant="outline" onClick={() => void projects.refetch()}>{t("common.retry")}</Button></div> : null}
      <PageHeader
        title={t("payments.title")}
        description={projectContext}
        actions={canCreate && project ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("payments.addPayment")}</Button> : null}
      />

      {/* Budget Availability Summary */}
      {project && canView ? (
        <Card className="border-border p-4 mb-4">
          <CardHeader className="flex flex-col gap-2">
            <CardTitle>{t("payments.budgetAvailability")}</CardTitle>
            <p className="text-sm text-muted-foreground">{t("payments.availableForPlanning")}</p>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <p className="text-xs text-muted-foreground">{t("payments.currentApprovedBudget")}</p>
                <p className="font-medium tabular-nums">{formatProjectMoney(approvedBudget)}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">{t("payments.existingPlannedPayments")}</p>
                <p className="font-medium tabular-nums negative">{formatProjectMoney(existingPlannedPaymentsTotal)}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">{t("payments.approvedPaymentRequests")}</p>
                <p className="font-medium tabular-nums negative">{formatProjectMoney(approvedPaymentRequestsTotal)}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">{t("payments.remainingAvailable")}</p>
                <p className={remainingAvailable !== null ? "font-semibold tabular-nums positive" : "text-muted-foreground"}>
                  {formatProjectMoney(remainingAvailable)}
                </p>
              </div>
            </div>
            {remainingAvailable !== null && projectedRemaining !== null && hasExceededAvailable && (
              <p className="text-sm text-destructive">{t("payments.exceedsAvailable", { amount: proposedAmountText })}</p>
            )}
            {remainingAvailable !== null && projectedRemaining !== null && !hasExceededAvailable && (
              <p className="text-sm text-positive">{t("payments.projectedRemaining", { amount: formatProjectMoney(projectedRemaining) })}</p>
            )}
          </CardContent>
        </Card>
      ) : remainingLoading ? (
        <Card className="border-border p-4 mb-4">
          <CardHeader><CardTitle>{t("payments.budgetAvailability")}</CardTitle></CardHeader>
          <CardContent className="p-4">{t("common.loading")}</CardContent>
        </Card>
      ) : null}

      {!project ? (
        <Card>
          <CardContent className="p-6 text-center text-sm text-muted-foreground">
            {projects.isLoading ? t("common.loading") : t("payments.projectUnavailable")}
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardContent className="flex justify-end p-4">
              <Button variant={dueOnly ? "default" : "outline"} onClick={() => { setDueOnly((value) => !value); setPage(1); }}>
                <Wallet className="me-1.5 h-4 w-4" />{dueOnly ? t("payments.showAll") : t("payments.duePayments")}
              </Button>
            </CardContent>
          </Card>

          {canView ? (
            <Card>
              <CardHeader><CardTitle>{dueOnly ? t("payments.duePayments") : t("payments.plannedPayments")}</CardTitle></CardHeader>
              <CardContent className="p-0">
                {paymentsLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
                  : paymentsError ? <div className="p-6 text-center"><p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p><Button className="mt-2" size="sm" variant="outline" onClick={() => void paymentsRefetch()}>{t("common.retry")}</Button></div>
                  : paymentsList.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
                  : (
                    <>
                      <div className="overflow-x-auto">
                        <Table>
                          <TableHeader><TableRow>
                            <TableHead>{t("payments.reference")}</TableHead>
                            <TableHead>{t("payments.purpose")}</TableHead><TableHead>{t("payments.plannedAmount")}</TableHead>
                            <TableHead>{t("payments.actualPaid")}</TableHead><TableHead>{t("payments.outstandingBalance")}</TableHead><TableHead>{t("payments.dueDate")}</TableHead>
                            <TableHead>{t("payments.settlementProgress")}</TableHead>
                            <TableHead>{t("payments.status")}</TableHead><TableHead>{t("common.actions")}</TableHead>
                          </TableRow></TableHeader>
                          <TableBody>
                            {paymentsList.map((payment) => (
                              <TableRow key={payment.id}>
                                <TableCell>{payment.paymentReference}</TableCell>
                                <TableCell>{payment.purpose}</TableCell>
                                <TableCell className="tabular-nums">{formatCurrency(payment.amount, payment.currency)}</TableCell>
                                <TableCell className="tabular-nums">{formatCurrency(payment.settledAmount, payment.currency)}</TableCell>
                                <TableCell className="tabular-nums">{formatCurrency(payment.outstandingAmount, payment.currency)}</TableCell>
                                <TableCell>{payment.dueDate.slice(0, 10)}</TableCell>
                                <TableCell>{t(`payments.progress.${payment.settlementProgressStatus}`)}</TableCell>
                                <TableCell><span className={payment.isOverdue ? "text-destructive" : "text-muted-foreground"}>{t(`payments.dueStatuses.${payment.dueDateStatus}`)}</span></TableCell>
                                <TableCell>
                                  <Button size="sm" variant="ghost" onClick={() => { setSelectedPayment(payment); setDetailsOpen(true); }}>
                                    <ArrowUpRight className="me-1 h-4 w-4" />{t("payments.viewDetails")}
                                  </Button>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                      <Pagination
                        page={page}
                        pageSize={paymentsData?.pageSize ?? 20}
                        total={paymentsData?.totalCount ?? 0}
                        totalPages={paymentsData?.totalPages ?? Math.max(1, Math.ceil((paymentsData?.totalCount ?? 0) / 20))}
                        onChange={setPage}
                      />
                    </>
                  )}
              </CardContent>
            </Card>
          ) : (
            <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("payments.viewUnavailable")}</CardContent></Card>
          )}

          <SideSheet open={paymentOpen} onOpenChange={setPaymentOpen} title={editingPayment ? t("payments.editPayment") : t("payments.addPayment")}>
            <form onSubmit={(event) => void savePayment(event)} className="flex min-h-0 flex-1 flex-col">
              <SideSheetBody className="space-y-3">
                {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
                <div className="rounded-md border p-3 text-sm">
                  <p className="text-xs text-muted-foreground">{t("payments.project")}</p>
                  <p className="font-medium">{project.name}{project.code ? ` (${project.code})` : ""}</p>
                </div>
                <div className="space-y-1.5"><Label>{t("payments.reference")} *</Label><Input value={paymentForm.paymentReference} onChange={(event) => setPaymentForm({ ...paymentForm, paymentReference: event.target.value })} maxLength={100} required /></div>
                <div className="space-y-1.5"><Label>{t("payments.purpose")} *</Label><Input value={paymentForm.purpose} onChange={(event) => setPaymentForm({ ...paymentForm, purpose: event.target.value })} maxLength={250} required /></div>
                <div className="space-y-1.5"><Label>{t("payments.descriptionField")}</Label><Textarea value={paymentForm.description} onChange={(event) => setPaymentForm({ ...paymentForm, description: event.target.value })} maxLength={2000} /></div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5"><Label>{t("payments.amount")} *</Label><Input type="number" min="0.0001" step="0.0001" value={paymentForm.amount} onChange={(event) => setPaymentForm({ ...paymentForm, amount: event.target.value })} required /></div>
                  <div className="space-y-1.5"><Label>{t("payments.currency")}</Label><Input value={paymentForm.currency} readOnly aria-invalid={!paymentForm.currency} /></div>
                </div>
                {!paymentForm.currency ? <p role="status" className="text-xs text-muted-foreground">{t("payments.currencyUnavailable")}</p> : null}
                <div className="space-y-1.5"><Label>{t("payments.dueDate")} *</Label><Input type="date" value={paymentForm.dueDate} onChange={(event) => { setPaymentForm({ ...paymentForm, dueDate: event.target.value }); setDueDateDirty(true); }} required /></div>
                <div className="space-y-1.5">
                  <Label>{t("payments.milestone")}</Label>
                  <Select value={paymentForm.milestoneId} onValueChange={(value) => setPaymentForm({ ...paymentForm, milestoneId: value })}>
                    <SelectTrigger aria-label={t("payments.milestone")}><SelectValue placeholder={t("payments.noMilestone")} /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">{t("payments.noMilestone")}</SelectItem>
                      {milestones.map((milestone) => <SelectItem key={milestone.id} value={milestone.id}>{milestone.code ? `${milestone.code} — ` : ""}{milestone.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">{t("payments.milestoneHint")}</p>
                </div>
                <div className="space-y-1.5"><Label>{t("payments.notes")}</Label><Textarea value={paymentForm.notes} onChange={(event) => setPaymentForm({ ...paymentForm, notes: event.target.value })} maxLength={2000} /></div>
                {/* Budget Availability Section */}
                {project && (
                  <div className="space-y-1.5 p-3 border-t border-border">
                    <p className="text-xs font-medium text-muted-foreground">{t("payments.budgetAvailability")}</p>
                    <div className="grid grid-cols-2 gap-2 text-sm">
                      <div>
                        <p className="text-xs text-muted-foreground">{t("payments.approvedBudget")}</p>
                        <p className="font-medium tabular-nums">{formatProjectMoney(approvedBudget)}</p>
                      </div>
                      <div>
                        <p className="text-xs text-muted-foreground">{t("payments.allocated")}</p>
                        <p className="font-medium tabular-nums negative">{formatProjectMoney(existingPlannedPaymentsTotal)}</p>
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-2 text-sm my-2">
                      <div>
                        <p className="text-xs text-muted-foreground">{t("payments.approvedRequests")}</p>
                        <p className="font-medium tabular-nums negative">{formatProjectMoney(approvedPaymentRequestsTotal)}</p>
                      </div>
                      <div>
                        <p className="text-xs text-muted-foreground">{t("payments.remainingBeforeNew")}</p>
                        <p className={remainingAvailable !== null ? "font-semibold tabular-nums positive" : "text-muted-foreground"}>
                          {formatProjectMoney(remainingAvailable)}
                        </p>
                      </div>
                    </div>
                    {remainingAvailable !== null && projectedRemaining !== null && hasExceededAvailable && (
                      <p className="text-sm text-destructive mt-1">{t("payments.exceedsAvailable", { amount: proposedAmountText })}</p>
                    )}
                    {remainingAvailable !== null && projectedRemaining !== null && !hasExceededAvailable && (
                      <p className="text-sm text-positive mt-1">{t("payments.projectedRemainingAfter", { amount: formatProjectMoney(projectedRemaining) })}</p>
                    )}
                  </div>
                )}
              </SideSheetBody>
              <SideSheetFooter><Button type="submit" disabled={mutations.create.isPending || mutations.update.isPending || !paymentForm.currency}>{t("common.save")}</Button></SideSheetFooter>
            </form>
          </SideSheet>

          <SideSheet open={detailsOpen} onOpenChange={setDetailsOpen} title={t("payments.paymentDetails")}>
            {activePayment ? (
              <SideSheetBody className="space-y-5">
                <div className="grid grid-cols-2 gap-3">
                  <div><p className="text-xs text-muted-foreground">{t("payments.reference")}</p><p className="font-medium">{activePayment.paymentReference}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("payments.plannedAmount")}</p><p className="font-medium tabular-nums">{formatCurrency(activePayment.amount, activePayment.currency)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("payments.actualPaid")}</p><p className="font-medium tabular-nums">{formatCurrency(activePayment.settledAmount, activePayment.currency)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("payments.outstandingBalance")}</p><p className="font-medium tabular-nums">{formatCurrency(activePayment.outstandingAmount, activePayment.currency)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("payments.settlementProgress")}</p><p>{t(`payments.progress.${activePayment.settlementProgressStatus}`)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("payments.dueDate")}</p><p>{activePayment.dueDate.slice(0, 10)}</p></div>
                  <div><p className="text-xs text-muted-foreground">{t("payments.status")}</p><p>{t(`payments.dueStatuses.${activePayment.dueDateStatus}`)}</p></div>
                </div>
                {activePayment.milestoneId ? <div><p className="text-xs text-muted-foreground">{t("payments.milestone")}</p><p>{activeMilestone?.name ?? activePayment.milestoneId}</p></div> : null}
                {activePayment.description ? <p className="text-sm text-muted-foreground">{activePayment.description}</p> : null}
                {activePayment.notes ? <p className="text-sm"><span className="font-medium">{t("payments.notes")}:</span> {activePayment.notes}</p> : null}
                <div>
                  <div className="mb-2 flex items-center gap-2"><History className="h-4 w-4" /><h3 className="font-semibold">{t("payments.settlementHistory")}</h3></div>
                  {settlements.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
                    : settlements.isError ? <p role="alert" className="text-sm text-destructive">{t("payments.historyFailed")}</p>
                    : (settlements.data?.items ?? []).length === 0 ? <p className="text-sm text-muted-foreground">{t("payments.noSettlements")}</p>
                    : <div className="space-y-2">{(settlements.data?.items ?? []).map((settlement) => (
                      <div key={settlement.id} className="flex items-start justify-between gap-3 rounded-md border p-3 text-sm">
                        <div><p className="font-medium">{settlement.paymentReference}</p><p className="text-xs text-muted-foreground">{settlement.settlementDate.slice(0, 10)}{settlement.notes ? ` · ${settlement.notes}` : ""}</p></div>
                        <p className="shrink-0 tabular-nums">{formatCurrency(settlement.amount, activePayment.currency)}</p>
                      </div>
                    ))}</div>}
                </div>
                <div className="flex gap-2">
                  {canRecordSettlements && activePayment.outstandingAmount > 0 ? <Button onClick={() => openSettlement(activePayment)}>{t("payments.recordPayment")}</Button> : null}
                  {canEdit ? <Button variant="outline" onClick={() => openEdit(activePayment)}>{t("payments.editPayment")}</Button> : null}
                  {canDelete && activePayment.settlements.length === 0 ? <Button variant="destructive" onClick={() => void deletePayment(activePayment)}><Trash2 className="me-1.5 h-4 w-4" />{t("payments.deletePayment")}</Button> : null}
                </div>
              </SideSheetBody>
            ) : null}
          </SideSheet>

          <SideSheet open={settlementOpen} onOpenChange={setSettlementOpen} title={t("payments.recordPayment")}>
            <form onSubmit={(event) => void saveSettlement(event)} className="flex min-h-0 flex-1 flex-col">
              <SideSheetBody className="space-y-3">
                {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
                {activePayment ? <div className="rounded-md border p-3 text-sm">
                  <p className="font-medium">{activePayment.paymentReference} · {activePayment.projectName}</p>
                  <p>{t("payments.plannedAmount")}: {formatCurrency(activePayment.amount, activePayment.currency)}</p>
                  <p>{t("payments.actualPaid")}: {formatCurrency(activePayment.settledAmount, activePayment.currency)}</p>
                  <p className="text-muted-foreground">{t("payments.outstandingBalance")}: {formatCurrency(activePayment.outstandingAmount, activePayment.currency)}</p>
                </div> : null}
                <div className="space-y-1.5"><Label>{t("payments.settlementAmount")} *</Label><Input type="number" min="0.0001" max={activePayment?.outstandingAmount} step="0.0001" value={settlementForm.amount} onChange={(event) => setSettlementForm({ ...settlementForm, amount: event.target.value })} required /></div>
                <div className="space-y-1.5"><Label>{t("payments.settlementDate")} *</Label><Input type="date" value={settlementForm.settlementDate} onChange={(event) => setSettlementForm({ ...settlementForm, settlementDate: event.target.value })} required /></div>
                <div className="space-y-1.5"><Label>{t("payments.transactionReference")} *</Label><Input value={settlementForm.paymentReference} onChange={(event) => setSettlementForm({ ...settlementForm, paymentReference: event.target.value })} maxLength={100} required /></div>
                <div className="space-y-1.5"><Label>{t("payments.notes")}</Label><Textarea value={settlementForm.notes} onChange={(event) => setSettlementForm({ ...settlementForm, notes: event.target.value })} maxLength={2000} /></div>
              </SideSheetBody>
              <SideSheetFooter><Button type="submit" disabled={mutations.settle.isPending || !activePayment}>{t("payments.recordPayment")}</Button></SideSheetFooter>
            </form>
          </SideSheet>
        </>
      )}
    </div>
  );
}
