import * as React from "react";
import { useSearchParams } from "react-router-dom";
import { ArrowUpRight, History, Plus, Trash2, Wallet } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { ApiFieldError } from "@components/ui/ApiFieldError";
import { Pagination } from "@components/ui/Pagination";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Textarea } from "@components/ui/Textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { PageHeader } from "@components/layout/PageHeader";
import { useI18n } from "@i18n/messages";
import { ApiError, getApiFieldError } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { useManagement } from "@api/management";
import { usePaymentProjectLookup } from "@features/projects/api/projects";
import { usePlanningBootstrapFull } from "@features/projects/api/planningFull";
import {
  usePaymentObligationMutations,
  usePaymentObligations,
  usePaymentSettlements,
  type PaymentObligationDto,
} from "./api/finance";

type PaymentForm = {
  projectId: string;
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
const emptyPayment = (projectId = "", currency?: string): PaymentForm => ({
  projectId,
  milestoneId: "",
  paymentReference: "",
  purpose: "",
  description: "",
  amount: "",
  currency: currency ?? "",
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

function sameId(left: string, right: string) {
  return left.toLowerCase() === right.toLowerCase();
}

export function PlannedPaymentsPage({ embeddedProjectId, embeddedProject, active = true }: {
  embeddedProjectId?: string;
  embeddedProject?: { id: string; publicId: string; name: string; code: string; currency?: string };
  active?: boolean;
} = {}) {
  const { t, formatCurrency, formatNumber } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const [searchParams, setSearchParams] = useSearchParams();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.PaymentObligation.View));
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.PaymentObligation.Create) && canMutate && !effectiveReadOnly);
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.PaymentObligation.Edit) && canMutate && !effectiveReadOnly);
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.PaymentObligation.Delete) && canMutate && !effectiveReadOnly);
  const canRecordSettlements = Boolean(user
    && hasPermission(user.permissions, Permissions.PaymentObligationSettlement.Record)
    && canMutate && !effectiveReadOnly);
  const canLoadPaymentProjectOptions = Boolean(user && (
    canView
    || hasPermission(user.permissions, Permissions.PaymentObligation.Create)
    || hasPermission(user.permissions, Permissions.PaymentObligation.Edit)
  ));
  const projects = usePaymentProjectLookup(canLoadPaymentProjectOptions && active && !embeddedProject);
  const projectItems = projects.data ?? [];
  const initialProjectId = embeddedProjectId ?? searchParams.get("projectId") ?? "";
  const [projectId, setProjectId] = React.useState(initialProjectId);
  const [dueOnly, setDueOnly] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [paymentOpen, setPaymentOpen] = React.useState(false);
  const [settlementOpen, setSettlementOpen] = React.useState(false);
  const [detailsOpen, setDetailsOpen] = React.useState(false);
  const [selectedPayment, setSelectedPayment] = React.useState<PaymentObligationDto | null>(null);
  const [editingPayment, setEditingPayment] = React.useState<PaymentObligationDto | null>(null);
  const [paymentForm, setPaymentForm] = React.useState<PaymentForm>(emptyPayment(initialProjectId));
  const [settlementForm, setSettlementForm] = React.useState<SettlementForm>(emptySettlement);
  const [idempotencyKey, setIdempotencyKey] = React.useState(() => crypto.randomUUID());
  const [actionError, setActionError] = React.useState<string | null>(null);

  const scopedProjectId = embeddedProjectId ?? projectId;
  const project = embeddedProject ?? projectItems.find((item) => sameId(item.id, scopedProjectId) || sameId(item.publicId, scopedProjectId));
  const paymentFormProject = projectItems.find((item) => sameId(item.id, paymentForm.projectId) || sameId(item.publicId, paymentForm.projectId))
    ?? (embeddedProject && (sameId(embeddedProject.id, paymentForm.projectId) || sameId(embeddedProject.publicId, paymentForm.projectId)) ? embeddedProject : undefined);
  const selectedPaymentProject = selectedPayment
    ? projectItems.find((item) => sameId(item.id, selectedPayment.projectId) || sameId(item.publicId, selectedPayment.projectId)) ?? embeddedProject
    : undefined;
  const payments = usePaymentObligations({
    page,
    pageSize: 20,
    ...(project ? { projectId: project.publicId } : {}),
    ...(dueOnly ? { dueOnly: true } : {}),
  }, canView && active && (!scopedProjectId || Boolean(project)));
  const planningProjectId = paymentOpen
    ? paymentFormProject?.publicId ?? ""
    : detailsOpen
      ? selectedPaymentProject?.publicId ?? ""
      : project?.publicId ?? "";
  const planning = usePlanningBootstrapFull(active ? planningProjectId : "");
  const mutations = usePaymentObligationMutations();
  const settlements = usePaymentSettlements(selectedPayment?.id ?? "", detailsOpen && active);
  const milestones = (planning.data?.activities ?? []).filter((activity) => activity.type === "Milestone");
  const paymentsList = payments.data?.items ?? [];
  const activePayment = paymentsList.find((item) => item.id === selectedPayment?.id) ?? selectedPayment;
  const activeMilestone = activePayment?.milestoneId
    ? planning.data?.activities.find((activity) => activity.id === activePayment.milestoneId)
    : undefined;

  React.useEffect(() => {
    if (!active) {
      setPaymentOpen(false);
      setSettlementOpen(false);
      setDetailsOpen(false);
    }
  }, [active]);

  React.useEffect(() => {
    if (embeddedProjectId) {
      setProjectId(embeddedProjectId);
      return;
    }
    const next = new URLSearchParams(searchParams);
    if (project) next.set("projectId", project.publicId);
    else if (projectId) next.set("projectId", projectId);
    else next.delete("projectId");
    if (next.toString() !== searchParams.toString()) setSearchParams(next, { replace: true });
  }, [embeddedProjectId, project, projectId, searchParams, setSearchParams]);

  function openCreate() {
    setEditingPayment(null);
    setPaymentForm(emptyPayment(project?.id ?? "", project?.currency ?? undefined));
    setActionError(null);
    setPaymentOpen(true);
  }

  function openEdit(payment: PaymentObligationDto) {
    setSelectedPayment(payment);
    setDetailsOpen(false);
    setEditingPayment(payment);
    setPaymentForm({
      projectId: payment.projectId,
      milestoneId: payment.milestoneId ?? "",
      paymentReference: payment.paymentReference,
      purpose: payment.purpose,
      description: payment.description,
      amount: String(payment.amount),
      currency: payment.currency,
      dueDate: payment.dueDate.slice(0, 10),
      notes: payment.notes ?? "",
    });
    setActionError(null);
    setPaymentOpen(true);
  }

  async function savePayment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setActionError(null);
    const paymentCurrency = (editingPayment?.currency ?? paymentForm.currency).trim();
    if (!/^[A-Z]{3}$/i.test(paymentCurrency)) {
      setActionError(t("payments.currencyUnavailable"));
      return;
    }
    const body = {
      projectId: editingPayment?.projectId ?? paymentFormProject?.id ?? paymentForm.projectId,
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

  if (!canView && !canCreate) return <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("projects.financialsPermissionRequired")}</CardContent></Card>;

  return (
    <div className="space-y-6">
      {projects.isError ? <div role="alert" className="flex items-center justify-between gap-2 text-sm text-destructive"><span>{t("common.errorLoading")}</span><Button type="button" size="sm" variant="outline" onClick={() => void projects.refetch()}>{t("common.retry")}</Button></div> : null}
      {embeddedProjectId ? <div className="flex flex-wrap items-start justify-between gap-3">
        <div><h3 className="text-base font-semibold">{t("payments.plannedPayments")}</h3><p className="mt-1 text-sm text-muted-foreground">{t("payments.description")}</p></div>
        {canCreate ? <Button size="sm" onClick={openCreate} disabled={!project}><Plus className="me-1.5 h-4 w-4" />{t("payments.addPayment")}</Button> : null}
      </div> : <PageHeader
        title={t("payments.title")}
        description={t("payments.description")}
        actions={canCreate ? <Button size="sm" onClick={openCreate} disabled={!projectId}><Plus className="me-1.5 h-4 w-4" />{t("payments.addPayment")}</Button> : null}
      />}

      {!embeddedProjectId ? <Card>
        <CardContent className="flex flex-col gap-3 p-4 sm:flex-row sm:items-end">
          <div className="min-w-0 flex-1 space-y-1.5">
            <Label>{t("payments.project")}</Label>
            <Select value={project?.id ?? projectId} onValueChange={(value) => { setProjectId(value); setPage(1); }}>
              <SelectTrigger aria-label={t("payments.project")}><SelectValue placeholder={t("payments.allProjects")} /></SelectTrigger>
              <SelectContent>
                <SelectItem value="">{t("payments.allProjects")}</SelectItem>
                {projectItems.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}{item.code ? ` (${item.code})` : ""}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <Button variant={dueOnly ? "default" : "outline"} onClick={() => { setDueOnly((value) => !value); setPage(1); }}>
            <Wallet className="me-1.5 h-4 w-4" />{dueOnly ? t("payments.showAll") : t("payments.duePayments")}
          </Button>
        </CardContent>
      </Card> : null}

      {canView ? <Card>
        <CardHeader><CardTitle>{dueOnly ? t("payments.duePayments") : t("payments.plannedPayments")}</CardTitle></CardHeader>
        <CardContent className="p-0">
          {payments.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : payments.isError ? <div className="p-6 text-center"><p role="alert" className="text-sm text-destructive">{payments.error instanceof ApiError && payments.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("common.errorLoading")}</p><Button className="mt-2" size="sm" variant="outline" onClick={() => void payments.refetch()}>{t("common.retry")}</Button></div>
            : paymentsList.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
            : (
              <>
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader><TableRow>
                      <TableHead>{t("payments.project")}</TableHead><TableHead>{t("payments.reference")}</TableHead>
                      <TableHead>{t("payments.purpose")}</TableHead><TableHead>{t("payments.plannedAmount")}</TableHead>
                      <TableHead>{t("payments.actualPaid")}</TableHead><TableHead>{t("payments.outstandingBalance")}</TableHead><TableHead>{t("payments.dueDate")}</TableHead>
                      <TableHead>{t("payments.settlementProgress")}</TableHead>
                      <TableHead>{t("payments.status")}</TableHead><TableHead>{t("common.actions")}</TableHead>
                    </TableRow></TableHeader>
                    <TableBody>
                      {paymentsList.map((payment) => (
                        <TableRow key={payment.id}>
                          <TableCell className="font-medium">{payment.projectName}</TableCell>
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
                  pageSize={payments.data?.pageSize ?? 20}
                  total={payments.data?.totalCount ?? 0}
                  totalPages={payments.data?.totalPages ?? Math.max(1, Math.ceil((payments.data?.totalCount ?? 0) / 20))}
                  onChange={setPage}
                />
              </>
            )}
        </CardContent>
      </Card> : <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("payments.viewUnavailable")}</CardContent></Card>}

      <SideSheet open={paymentOpen} onOpenChange={setPaymentOpen} title={editingPayment ? t("payments.editPayment") : t("payments.addPayment")}>
        <form onSubmit={(event) => void savePayment(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-3">
            {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
            <div className="space-y-1.5">
              <Label>{t("payments.project")} *</Label>
              <Select value={paymentForm.projectId} onValueChange={(value) => {
                setPaymentForm({ ...paymentForm, projectId: value, currency: projectItems.find((item) => item.id === value)?.currency ?? "", milestoneId: "" });
              }}>
                <SelectTrigger aria-label={t("payments.project")} disabled={Boolean(editingPayment)}><SelectValue placeholder={t("payments.selectProject")} /></SelectTrigger>
                <SelectContent>{projectItems.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}{item.code ? ` (${item.code})` : ""}</SelectItem>)}</SelectContent>
              </Select>
              <ApiFieldError error={mutations.update.error ?? mutations.create.error} fieldName="projectId" />
            </div>
            <div className="space-y-1.5"><Label htmlFor="payment-reference">{t("payments.reference")} *</Label><Input id="payment-reference" value={paymentForm.paymentReference} onChange={(event) => setPaymentForm({ ...paymentForm, paymentReference: event.target.value })} maxLength={100} required aria-invalid={Boolean(getApiFieldError(mutations.update.error ?? mutations.create.error, "paymentReference"))} aria-describedby={getApiFieldError(mutations.update.error ?? mutations.create.error, "paymentReference") ? "payment-reference-error" : undefined} /><ApiFieldError id="payment-reference-error" error={mutations.update.error ?? mutations.create.error} fieldName="paymentReference" /></div>
            <div className="space-y-1.5"><Label htmlFor="payment-purpose">{t("payments.purpose")} *</Label><Input id="payment-purpose" value={paymentForm.purpose} onChange={(event) => setPaymentForm({ ...paymentForm, purpose: event.target.value })} maxLength={250} required aria-invalid={Boolean(getApiFieldError(mutations.update.error ?? mutations.create.error, "purpose"))} aria-describedby={getApiFieldError(mutations.update.error ?? mutations.create.error, "purpose") ? "payment-purpose-error" : undefined} /><ApiFieldError id="payment-purpose-error" error={mutations.update.error ?? mutations.create.error} fieldName="purpose" /></div>
            <div className="space-y-1.5"><Label>{t("payments.descriptionField")}</Label><Textarea value={paymentForm.description} onChange={(event) => setPaymentForm({ ...paymentForm, description: event.target.value })} maxLength={2000} /></div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5"><Label htmlFor="payment-amount">{t("payments.amount")} *</Label><Input id="payment-amount" type="number" min="0.0001" step="0.0001" value={paymentForm.amount} onChange={(event) => setPaymentForm({ ...paymentForm, amount: event.target.value })} required aria-invalid={Boolean(getApiFieldError(mutations.update.error ?? mutations.create.error, "amount"))} aria-describedby={getApiFieldError(mutations.update.error ?? mutations.create.error, "amount") ? "payment-amount-error" : undefined} /><ApiFieldError id="payment-amount-error" error={mutations.update.error ?? mutations.create.error} fieldName="amount" /></div>
              <div className="space-y-1.5"><Label>{t("payments.currency")}</Label><Input value={paymentForm.currency} readOnly aria-invalid={!paymentForm.currency} />{!paymentForm.currency ? <p role="status" className="text-xs text-muted-foreground">{t("payments.currencyUnavailable")}</p> : null}</div>
            </div>
            <div className="space-y-1.5"><Label htmlFor="payment-due-date">{t("payments.dueDate")} *</Label><Input id="payment-due-date" type="date" value={paymentForm.dueDate} onChange={(event) => setPaymentForm({ ...paymentForm, dueDate: event.target.value })} required aria-invalid={Boolean(getApiFieldError(mutations.update.error ?? mutations.create.error, "dueDate"))} aria-describedby={getApiFieldError(mutations.update.error ?? mutations.create.error, "dueDate") ? "payment-due-date-error" : undefined} /><ApiFieldError id="payment-due-date-error" error={mutations.update.error ?? mutations.create.error} fieldName="dueDate" /></div>
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
          </SideSheetBody>
          <SideSheetFooter><Button type="submit" disabled={mutations.create.isPending || mutations.update.isPending || !paymentForm.projectId || !/^[A-Z]{3}$/i.test(paymentForm.currency)}>{t("common.save")}</Button></SideSheetFooter>
        </form>
      </SideSheet>

      <SideSheet open={detailsOpen} onOpenChange={setDetailsOpen} title={t("payments.paymentDetails")}>
        {activePayment ? (
          <SideSheetBody className="space-y-5">
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-xs text-muted-foreground">{t("payments.project")}</p><p className="font-medium">{activePayment.projectName}</p></div>
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
                    <p className="shrink-0 tabular-nums">{settlement.currency
                      ? formatCurrency(settlement.amount, settlement.currency)
                      : `${formatNumber(settlement.amount)} ${t("projects.snapshotFields.currencyUnavailable")}`}</p>
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
            <div className="space-y-1.5"><Label htmlFor="settlement-amount">{t("payments.settlementAmount")} *</Label><Input id="settlement-amount" type="number" min="0.0001" max={activePayment?.outstandingAmount} step="0.0001" value={settlementForm.amount} onChange={(event) => setSettlementForm({ ...settlementForm, amount: event.target.value })} required aria-invalid={Boolean(getApiFieldError(mutations.settle.error, "amount"))} aria-describedby={getApiFieldError(mutations.settle.error, "amount") ? "settlement-amount-error" : undefined} /><ApiFieldError id="settlement-amount-error" error={mutations.settle.error} fieldName="amount" /></div>
            <div className="space-y-1.5"><Label htmlFor="settlement-date">{t("payments.settlementDate")} *</Label><Input id="settlement-date" type="date" value={settlementForm.settlementDate} onChange={(event) => setSettlementForm({ ...settlementForm, settlementDate: event.target.value })} required aria-invalid={Boolean(getApiFieldError(mutations.settle.error, "settlementDate"))} aria-describedby={getApiFieldError(mutations.settle.error, "settlementDate") ? "settlement-date-error" : undefined} /><ApiFieldError id="settlement-date-error" error={mutations.settle.error} fieldName="settlementDate" /></div>
            <div className="space-y-1.5"><Label htmlFor="settlement-reference">{t("payments.transactionReference")} *</Label><Input id="settlement-reference" value={settlementForm.paymentReference} onChange={(event) => setSettlementForm({ ...settlementForm, paymentReference: event.target.value })} maxLength={100} required aria-invalid={Boolean(getApiFieldError(mutations.settle.error, "paymentReference"))} aria-describedby={getApiFieldError(mutations.settle.error, "paymentReference") ? "settlement-reference-error" : undefined} /><ApiFieldError id="settlement-reference-error" error={mutations.settle.error} fieldName="paymentReference" /></div>
            <div className="space-y-1.5"><Label>{t("payments.notes")}</Label><Textarea value={settlementForm.notes} onChange={(event) => setSettlementForm({ ...settlementForm, notes: event.target.value })} maxLength={2000} /></div>
          </SideSheetBody>
          <SideSheetFooter><Button type="submit" disabled={mutations.settle.isPending || !activePayment}>{t("payments.recordPayment")}</Button></SideSheetFooter>
        </form>
      </SideSheet>
    </div>
  );
}
