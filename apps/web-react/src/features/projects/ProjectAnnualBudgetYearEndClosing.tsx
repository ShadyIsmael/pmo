import * as React from "react";
import { AlertTriangle, ArrowDown, ArrowUp, Check, X } from "lucide-react";
import { ApiError } from "@api/client";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useProjectAnnualBudgets } from "./api/project-annual-budgets";
import {
  useCancelProjectAnnualBudgetClosing,
  useDecideProjectAnnualBudgetClosing,
  usePrepareProjectAnnualBudgetClosing,
  useProjectAnnualBudgetClosingApprovers,
  useProjectAnnualBudgetClosingRecoveryApprovers,
  useProjectAnnualBudgetClosings,
  useProjectFinancialFoundation,
  useSubmitProjectAnnualBudgetClosing,
  useReassignProjectAnnualBudgetClosingApprover,
  useRecoverCancelProjectAnnualBudgetClosing,
  type PaymentRequestApproverSelection,
  type ProjectAnnualBudgetClosing,
} from "./api/project-financials";

type Props = {
  projectId: string;
  canPrepare: boolean;
  canApprove: boolean;
  canManageAssignmentRecovery: boolean;
  canCarryForward: boolean;
  active?: boolean;
  selectedFiscalYear: number | null;
  onFiscalYearChange: (year: number) => void;
};

const newIdempotencyKey = () => globalThis.crypto?.randomUUID?.()
  ?? "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, character => {
    const random = Math.floor(Math.random() * 16);
    return (character === "x" ? random : (random & 0x3) | 0x8).toString(16);
  });

export function ProjectAnnualBudgetYearEndClosing({ projectId, canPrepare, canApprove, canManageAssignmentRecovery, canCarryForward, active = true, selectedFiscalYear, onFiscalYearChange }: Props) {
  const { t, formatCurrency, formatDateTime } = useI18n();
  const { user } = useAuth();
  const confirm = useConfirm();
  const closings = useProjectAnnualBudgetClosings(projectId, active);
  const budgets = useProjectAnnualBudgets(projectId, active);
  const foundation = useProjectFinancialFoundation(projectId, active);
  const prepare = usePrepareProjectAnnualBudgetClosing(projectId);
  const submit = useSubmitProjectAnnualBudgetClosing(projectId);
  const decide = useDecideProjectAnnualBudgetClosing(projectId);
  const cancel = useCancelProjectAnnualBudgetClosing(projectId);
  const recoverCancel = useRecoverCancelProjectAnnualBudgetClosing(projectId);
  const approvers = useProjectAnnualBudgetClosingApprovers(projectId, active && canPrepare);
  const [freeCarryForward, setFreeCarryForward] = React.useState("0");
  const [newFunding, setNewFunding] = React.useState("0");
  const [adjustments, setAdjustments] = React.useState("0");
  const [carryCommitments, setCarryCommitments] = React.useState(false);
  const [activeId, setActiveId] = React.useState<string | null>(null);
  const [approvalMode, setApprovalMode] = React.useState<"Sequential" | "Parallel">("Sequential");
  const [selected, setSelected] = React.useState<Record<string, "Mandatory" | "Optional">>({});
  const [comment, setComment] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [recoveryCancelReason, setRecoveryCancelReason] = React.useState("");
  const [recoveryCancelError, setRecoveryCancelError] = React.useState<string | null>(null);

  React.useEffect(() => {
    setFreeCarryForward("0");
    setNewFunding("0");
    setAdjustments("0");
    setCarryCommitments(false);
    setSelected({});
    setError(null);
  }, [selectedFiscalYear]);

  const sourceBudget = selectedFiscalYear === null
    ? undefined
    : (budgets.data?.items ?? []).find(item => item.fiscalYear === selectedFiscalYear);
  const eligibleSourceBudgets = (budgets.data?.items ?? []).filter(item => item.status === "Approved"
    && !item.isClosed && item.fiscalYear < (closings.data?.currentFiscalYear ?? Infinity));
  const selectedBudgetEligible = eligibleSourceBudgets.some(item => item.fiscalYear === selectedFiscalYear);
  const history = closings.data?.items ?? [];
  const selectedHistory = history.filter(item => item.fiscalYear === selectedFiscalYear);
  const activeClosing = selectedHistory.find(item => item.id === activeId)
    ?? selectedHistory.find(item => item.status === "Review" || item.status === "PendingApproval")
    ?? selectedHistory[0];
  const selectedApprovers: PaymentRequestApproverSelection[] = Object.entries(selected)
    .filter(([id]) => id !== user?.id)
    .map(([userId, requirement], index) => ({
      userId,
      requirement: approvalMode === "Sequential" ? "Mandatory" : requirement,
      position: approvalMode === "Sequential" ? index + 1 : null,
    }));
  const projectMode = foundation.data?.financeApprovalMode;
  const allowedModes: Array<"Sequential" | "Parallel"> = projectMode === "Both"
    ? ["Sequential", "Parallel"]
    : projectMode === "Parallel" ? ["Parallel"]
      : projectMode === "Sequential" ? ["Sequential"] : [];
  React.useEffect(() => {
    if (projectMode === "Parallel" && approvalMode !== "Parallel") setApprovalMode("Parallel");
    else if (projectMode === "Sequential" && approvalMode !== "Sequential") setApprovalMode("Sequential");
  }, [approvalMode, projectMode]);
  const destinationFreeCarry = activeClosing?.status === "Closed"
    ? activeClosing.approvedFreeCarryForward : activeClosing?.proposedFreeCarryForward ?? Number(freeCarryForward);
  const destinationReservedCarry = activeClosing?.status === "Closed"
    ? activeClosing.approvedReservedCarryForward
    : (activeClosing?.carryOutstandingCommitments ? activeClosing.outstandingCommitments : 0);
  const destinationNewFunding = activeClosing?.nextFiscalYearNewFunding ?? Number(newFunding);
  const destinationAdjustments = activeClosing?.nextFiscalYearAdjustments ?? Number(adjustments);
  const destinationBudgetPreview = destinationNewFunding + destinationFreeCarry + destinationReservedCarry + destinationAdjustments;
  const statementValues: Array<{ label: string; amount: number }> = activeClosing ? [
    { label: t("projects.annualBudgetTotal"), amount: activeClosing.approvedAnnualBudget },
    { label: t("projects.annualBudgetFunding"), amount: activeClosing.newFunding },
    { label: t("projects.yearEndClosingIncomingCarry"), amount: activeClosing.incomingCarryForward },
    { label: t("projects.annualBudgetAdjustments"), amount: activeClosing.approvedAdjustments },
    { label: t("projects.yearEndClosingApprovedAllocations"), amount: activeClosing.approvedPaymentRequestAllocations },
    { label: t("projects.yearEndClosingReversalReleases"), amount: activeClosing.approvedReversalReleases },
    { label: t("projects.yearEndClosingEffectiveAllocations"), amount: activeClosing.effectiveActiveAllocations },
    { label: t("projects.yearEndClosingActualPaid"), amount: activeClosing.actualSettlementsInFiscalYear },
    { label: t("projects.yearEndClosingOutstanding"), amount: activeClosing.outstandingCommitments },
    { label: t("projects.yearEndClosingFreeAvailable"), amount: activeClosing.freeAvailableBudget },
    { label: t("projects.yearEndClosingTotalUnspent"), amount: activeClosing.totalUnspentBudget },
    { label: t("projects.yearEndClosingProposedFreeCarryForward"), amount: activeClosing.proposedFreeCarryForward },
    { label: t("projects.yearEndClosingReservedCarryForward"), amount: activeClosing.approvedReservedCarryForward || destinationReservedCarry },
    { label: t("projects.yearEndClosingNonCarried"), amount: activeClosing.nonCarriedFreeBalance },
    { label: t("projects.yearEndClosingNextFunding"), amount: activeClosing.nextFiscalYearNewFunding },
    { label: t("projects.yearEndClosingNextAdjustments"), amount: activeClosing.nextFiscalYearAdjustments },
    { label: t("projects.yearEndClosingDestinationBudgetPreview"), amount: destinationBudgetPreview },
  ] : [];

  const errorMessage = (value: unknown) => value instanceof Error ? value.message : t("projects.yearEndClosingActionFailed");
  const formatMoney = (amount: number, currency: string) => formatCurrency(amount, currency);
  const updateApprover = (id: string, requirement: "Mandatory" | "Optional" | null) => {
    setSelected(current => {
      const next = { ...current };
      if (requirement) next[id] = requirement;
      else delete next[id];
      return next;
    });
  };
  const moveApprover = (id: string, direction: -1 | 1) => {
    setSelected(current => {
      const entries = Object.entries(current);
      const index = entries.findIndex(([userId]) => userId === id);
      const target = index + direction;
      if (index < 0 || target < 0 || target >= entries.length) return current;
      [entries[index], entries[target]] = [entries[target], entries[index]];
      return Object.fromEntries(entries) as Record<string, "Mandatory" | "Optional">;
    });
  };

  const prepareClosing = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    if (selectedFiscalYear === null || !sourceBudget || !selectedBudgetEligible) {
      setError(t("projects.yearEndClosingSelectedBudgetNotEligible"));
      return;
    }
    try {
      const closing = await prepare.mutateAsync({
        fiscalYear: selectedFiscalYear,
        proposedFreeCarryForward: Number(freeCarryForward),
        carryOutstandingCommitments: carryCommitments,
        nextFiscalYearNewFunding: Number(newFunding),
        nextFiscalYearAdjustments: Number(adjustments),
        idempotencyKey: newIdempotencyKey(),
      });
      setActiveId(closing.id);
    } catch (cause) {
      setError(errorMessage(cause));
    }
  };

  const submitClosing = async (closing: ProjectAnnualBudgetClosing) => {
    setError(null);
    if (!allowedModes.includes(approvalMode) || selectedApprovers.length === 0) {
      setError(t("projects.yearEndClosingApproverRequired"));
      return;
    }
    if (approvalMode === "Parallel" && !selectedApprovers.some(item => item.requirement === "Mandatory")) {
      setError(t("projects.yearEndClosingMandatoryApproverRequired"));
      return;
    }
    try {
      await submit.mutateAsync({ closingId: closing.id, approvalMode, approvers: selectedApprovers, rowVersion: closing.rowVersion });
      setSelected({});
    } catch (cause) {
      setError(errorMessage(cause));
    }
  };

  const recordDecision = async (closing: ProjectAnnualBudgetClosing, decision: "Approved" | "Rejected") => {
    setError(null);
    try {
      await decide.mutateAsync({ closingId: closing.id, decision, comment: comment.trim() || undefined });
      setComment("");
    } catch (cause) {
      setError(errorMessage(cause));
    }
  };

  const cancelClosing = async (closing: ProjectAnnualBudgetClosing) => {
    const accepted = await confirm({
      title: t("projects.yearEndClosingCancelConfirmTitle"),
      description: t("projects.yearEndClosingCancelConfirmDescription"),
      confirmLabel: t("projects.yearEndClosingCancelConfirm"),
      destructive: true,
    });
    if (!accepted) return;
    setError(null);
    try {
      await cancel.mutateAsync({ closingId: closing.id, rowVersion: closing.rowVersion, approvalRowVersion: closing.approvalRowVersion });
    } catch (cause) {
      setError(errorMessage(cause));
    }
  };

  const cancelForRecovery = async (closing: ProjectAnnualBudgetClosing, assignmentId: string) => {
    const reason = recoveryCancelReason.trim();
    if (!reason || reason.length > 1000 || !closing.approvalRowVersion) {
      setRecoveryCancelError(t("projects.yearEndClosingRecoveryCancelFieldsRequired"));
      return;
    }
    const accepted = await confirm({
      title: t("projects.yearEndClosingRecoveryCancelTitle"),
      description: t("projects.yearEndClosingRecoveryCancelDescription"),
      confirmLabel: t("projects.yearEndClosingRecoveryCancel"),
      destructive: true,
    });
    if (!accepted) return;
    setRecoveryCancelError(null);
    try {
      await recoverCancel.mutateAsync({
        closingId: closing.id,
        assignmentId,
        rowVersion: closing.rowVersion,
        approvalRowVersion: closing.approvalRowVersion,
        reason,
      });
      setRecoveryCancelReason("");
    } catch (cause) {
      setRecoveryCancelError(cause instanceof ApiError && cause.status === 409
        ? t("projects.paymentRequestRecoveryRefresh")
        : t("projects.yearEndClosingRecoveryCancelFailed"));
    }
  };

  return <Card>
    <CardHeader className="pb-3">
      <CardTitle>{t("projects.yearEndClosingTitle")}</CardTitle>
      <p className="text-sm text-muted-foreground">{t("projects.yearEndClosingDescription")}</p>
    </CardHeader>
    <CardContent className="space-y-4">
      {closings.isLoading || budgets.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : null}
      {closings.isError || budgets.isError ? <p role="alert" className="text-sm text-destructive">{[closings.error, budgets.error].some(error => error instanceof ApiError && error.status === 403) ? t("projects.financeWorkspaceAccessDenied") : t("projects.yearEndClosingLoadFailed")}</p> : null}
      {!budgets.isLoading && !budgets.isError && canPrepare && !selectedBudgetEligible
        ? <p role="status" className="rounded-md border p-3 text-sm text-muted-foreground">{eligibleSourceBudgets.length === 0 ? t("projects.yearEndClosingNoEligibleBudget") : t("projects.yearEndClosingSelectedBudgetNotEligible")}</p> : null}
      {canPrepare ? <form onSubmit={prepareClosing} className="grid gap-3 rounded-md border p-3 md:grid-cols-2 xl:grid-cols-4">
        <div>
          <p className="text-sm font-medium">{t("projects.annualBudgetFiscalYear")}</p>
          <p className="mt-1 flex h-10 items-center rounded-md border bg-muted/30 px-3 text-sm">{sourceBudget?.fiscalYearLabel ?? activeClosing?.fiscalYearLabel ?? (selectedFiscalYear === null ? t("projects.financeKpiNoYearAvailable") : `${t("projects.fiscalYearShort")} ${selectedFiscalYear}`)}</p>
        </div>
        <div><label htmlFor="closing-free-carry" className="text-sm font-medium">{t("projects.yearEndClosingFreeCarryForward")}</label>
          <Input id="closing-free-carry" type="number" min="0" step="0.0001" value={freeCarryForward} onChange={event => setFreeCarryForward(event.target.value)} /></div>
        <div><label htmlFor="closing-new-funding" className="text-sm font-medium">{t("projects.yearEndClosingNextFunding")}</label>
          <Input id="closing-new-funding" type="number" min="0" step="0.0001" value={newFunding} onChange={event => setNewFunding(event.target.value)} /></div>
        <div><label htmlFor="closing-adjustments" className="text-sm font-medium">{t("projects.annualBudgetAdjustments")}</label>
          <Input id="closing-adjustments" type="number" step="0.0001" value={adjustments} onChange={event => setAdjustments(event.target.value)} /></div>
        <label className="flex items-center gap-2 self-end pb-2 text-sm">
          <input type="checkbox" checked={carryCommitments} onChange={event => setCarryCommitments(event.target.checked)} />
          {t("projects.yearEndClosingCarryCommitments")}
        </label>
        <div className="md:col-span-2 xl:col-span-5">
          <Button type="submit" disabled={prepare.isPending || !selectedBudgetEligible}>
            {prepare.isPending ? t("common.loading") : t("projects.yearEndClosingPrepare")}
          </Button>
          {!canCarryForward ? <p className="mt-2 text-xs text-muted-foreground">{t("projects.yearEndClosingCarryPermissionHint")}</p> : null}
        </div>
      </form> : null}

      {error ? <p role="alert" className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{error}</p> : null}
      {activeClosing ? <section className="space-y-3 rounded-md border p-3" aria-label={t("projects.yearEndClosingStatement")}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div><h3 className="font-semibold">{activeClosing.fiscalYearLabel} · {t("projects.yearEndClosingStatement")}</h3>
            <p className="text-xs text-muted-foreground">{activeClosing.fiscalYearStartDate} — {activeClosing.fiscalYearEndDate} · {activeClosing.currency}</p></div>
          <Badge variant={activeClosing.status === "Closed" ? "default" : "secondary"}>{t(`projects.yearEndClosingStatus${activeClosing.status}`)}</Badge>
        </div>
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          {statementValues.map(({ label, amount }) => <div key={label} className="rounded border p-2">
            <p className="text-xs text-muted-foreground">{label}</p>
            <p className="mt-1 font-semibold tabular-nums">{formatMoney(Number(amount), activeClosing.currency)}</p>
          </div>)}
        </div>
        {activeClosing.integrityIssues.length > 0 ? <div role="alert" className="rounded-md bg-warning/10 p-3 text-sm">
          <p className="flex items-center gap-2 font-medium"><AlertTriangle className="size-4" />{t("projects.yearEndClosingBlocked")}</p>
          <ul className="mt-2 list-disc ps-5">{activeClosing.integrityIssues.map(issue => <li key={issue}>{t(`projects.yearEndClosingIssue.${issue.split(":")[0]}`)}</li>)}</ul>
        </div> : null}
        {activeClosing.pendingPaymentRequestCount || activeClosing.pendingReversalRequestCount || activeClosing.unresolvedLegacyRequestCount
          ? <p className="text-sm text-warning">{t("projects.yearEndClosingPendingFinancialItems", {
            requests: activeClosing.pendingPaymentRequestCount, reversals: activeClosing.pendingReversalRequestCount,
            legacy: activeClosing.unresolvedLegacyRequestCount,
          })}</p> : null}
        {activeClosing.approverDecisions.length > 0 ? <div>
          <h4 className="text-sm font-medium">{t("projects.yearEndClosingApprovers")}</h4>
          <ul className="mt-2 space-y-2">{activeClosing.approverDecisions.map(item => <li key={item.assignmentId || item.userId} className="flex flex-wrap items-center justify-between gap-2 rounded border p-2 text-sm">
            <span>{item.userName || item.userId} · {t(item.requirement === "Mandatory" ? "projects.paymentRequestMandatory" : "projects.paymentRequestOptional")}</span>
            <span>{item.decision ? t(`projects.yearEndClosingDecision${item.decision}`) : t("projects.yearEndClosingDecisionPending")}{item.decidedAtUtc ? ` · ${formatDateTime(item.decidedAtUtc)}` : ""}</span>
            {activeClosing.status === "PendingApproval" && item.state === "Pending" && item.canReassignAssignment
              && canApprove && canManageAssignmentRecovery && activeClosing.approvalRowVersion
              ? <ClosingApproverRecoveryControl projectId={projectId} closing={activeClosing} assignmentId={item.assignmentId} /> : null}
          </li>)}</ul>
        </div> : null}

        {activeClosing.canCancel && !activeClosing.canRecoverCancel && canPrepare
          && activeClosing.preparedBy === user?.id
          ? <div className="border-t pt-3">
            <Button variant="outline" onClick={() => void cancelClosing(activeClosing)} disabled={cancel.isPending}>
              {cancel.isPending ? t("common.saving") : t("common.cancel")}
            </Button>
          </div> : null}

        {activeClosing.status === "PendingApproval" && activeClosing.canRecoverCancel && canApprove && canManageAssignmentRecovery
          ? <section className="space-y-3 rounded-md border border-destructive/40 bg-destructive/5 p-3">
            <div>
              <h4 className="font-medium">{t("projects.yearEndClosingRecoveryCancelTitle")}</h4>
              <p className="mt-1 text-sm text-muted-foreground">{t("projects.yearEndClosingRecoveryCancelDescription")}</p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="closing-recovery-cancel-reason">{t("projects.yearEndClosingRecoveryCancelReason")}</Label>
              <Textarea id="closing-recovery-cancel-reason" value={recoveryCancelReason}
                onChange={event => { setRecoveryCancelReason(event.target.value); setRecoveryCancelError(null); }}
                rows={3} maxLength={1000} required />
            </div>
            {recoveryCancelError ? <p role="alert" className="text-sm text-destructive">{recoveryCancelError}</p> : null}
            <Button variant="destructive" onClick={() => {
              const blocked = activeClosing.approverDecisions.find(item => item.canRecoverCancelAssignment && item.state === "Pending");
              if (blocked) void cancelForRecovery(activeClosing, blocked.assignmentId);
            }} disabled={recoverCancel.isPending || !recoveryCancelReason.trim() || !activeClosing.approvalRowVersion
              || !activeClosing.approverDecisions.some(item => item.canRecoverCancelAssignment && item.state === "Pending")}>
              {recoverCancel.isPending ? t("common.saving") : t("projects.yearEndClosingRecoveryCancel")}
            </Button>
          </section> : null}
        {activeClosing.canSubmit ? <div className="space-y-3 border-t pt-3">
          <div className="flex flex-wrap items-end gap-3">
            <div><label htmlFor="closing-approval-mode" className="text-sm font-medium">{t("projects.financeApprovalMode")}</label>
              <select id="closing-approval-mode" className="mt-1 h-9 rounded-md border bg-background px-3 text-sm" value={approvalMode}
                onChange={event => setApprovalMode(event.target.value as "Sequential" | "Parallel")}>
                {allowedModes.map(mode => <option key={mode} value={mode}>{t(mode === "Sequential" ? "projects.financeApprovalSequential" : "projects.financeApprovalParallel")}</option>)}
              </select>
            </div>
            <Button onClick={() => void submitClosing(activeClosing)} disabled={submit.isPending || selectedApprovers.length === 0}>
              {submit.isPending ? t("common.loading") : t("projects.yearEndClosingSubmit")}
            </Button>

          </div>
          {approvers.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
            : approvers.isError ? <p role="alert" className="text-sm text-destructive">{t("projects.yearEndClosingApproversLoadFailed")}</p>
              : <ul className="grid gap-2 sm:grid-cols-2">{(approvers.data ?? []).filter(item => item.userId !== user?.id).map(candidate => {
                const requirement = selected[candidate.userId];
                const order = Object.keys(selected).indexOf(candidate.userId);
                return <li key={candidate.userId} className="flex items-center justify-between gap-2 rounded border p-2">
                  <label className="flex min-w-0 items-center gap-2 text-sm">
                    <input type="checkbox" checked={Boolean(requirement)} onChange={event => updateApprover(candidate.userId, event.target.checked ? "Mandatory" : null)} />
                    <span className="truncate">{candidate.displayName}</span>
                  </label>
                  {approvalMode === "Sequential" && requirement ? <div className="flex items-center gap-1">
                    <span className="min-w-5 text-center text-xs text-muted-foreground">{order + 1}</span>
                    <Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverUp", { name: candidate.displayName })}
                      disabled={order <= 0} onClick={() => moveApprover(candidate.userId, -1)}><ArrowUp className="size-4" /></Button>
                    <Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverDown", { name: candidate.displayName })}
                      disabled={order < 0 || order >= Object.keys(selected).length - 1} onClick={() => moveApprover(candidate.userId, 1)}><ArrowDown className="size-4" /></Button>
                  </div> : null}
                  {approvalMode === "Parallel" && requirement ? <select className="h-8 rounded border bg-background px-2 text-xs"
                    aria-label={t("projects.yearEndClosingApproverRequirement", { name: candidate.displayName })}
                    value={requirement} onChange={event => updateApprover(candidate.userId, event.target.value as "Mandatory" | "Optional")}>
                    <option value="Mandatory">{t("projects.paymentRequestMandatory")}</option>
                    <option value="Optional">{t("projects.paymentRequestOptional")}</option>
                  </select> : null}
                </li>;
              })}</ul>}
          {approvalMode === "Parallel" ? <p className="text-xs text-muted-foreground">{t("projects.yearEndClosingParallelHint")}</p> : null}
          <p className="text-xs text-muted-foreground">{t("projects.yearEndClosingSeparationOfDuties")}</p>
        </div> : null}
        {activeClosing.canApprove && canApprove ? <div className="flex flex-wrap items-end gap-2 border-t pt-3">
          <div className="min-w-56 flex-1"><label htmlFor="closing-decision-comment" className="text-sm font-medium">{t("projects.paymentRequestComment")}</label>
            <Input id="closing-decision-comment" value={comment} onChange={event => setComment(event.target.value)} /></div>
          <Button onClick={() => void recordDecision(activeClosing, "Approved")} disabled={decide.isPending}><Check className="me-1 size-4" />{t("projects.yearEndClosingApprove")}</Button>
          <Button variant="outline" onClick={() => void recordDecision(activeClosing, "Rejected")} disabled={decide.isPending}><X className="me-1 size-4" />{t("projects.yearEndClosingReject")}</Button>
        </div> : null}
      </section> : null}

      {history.length > 0 ? <div className="space-y-2 border-t pt-3">
        <h3 className="font-medium">{t("projects.yearEndClosingHistory")}</h3>
        <ul className="divide-y">{history.map(item => <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
          <button type="button" className="text-start font-medium underline-offset-2 hover:underline" onClick={() => { onFiscalYearChange(item.fiscalYear); setActiveId(item.id); }}>{item.fiscalYearLabel}</button>
          <span className="text-muted-foreground">{item.status === "Closed" && item.completedAtUtc ? formatDateTime(item.completedAtUtc) : ""}</span>
          <Badge variant={item.status === "Closed" ? "default" : "secondary"}>{t(`projects.yearEndClosingStatus${item.status}`)}</Badge>
        </li>)}</ul>
      </div> : null}
      {!closings.isLoading && history.length === 0 ? <p className="text-sm text-muted-foreground">{t("projects.yearEndClosingHistoryEmpty")}</p> : null}
      <p className="text-xs text-muted-foreground">{t("projects.yearEndClosingReadOnlyHistoryHint")}</p>
    </CardContent>
  </Card>;
}

function ClosingApproverRecoveryControl({ projectId, closing, assignmentId }: {
  projectId: string;
  closing: ProjectAnnualBudgetClosing;
  assignmentId: string;
}) {
  const { t } = useI18n();
  const [open, setOpen] = React.useState(false);
  const [replacementApproverId, setReplacementApproverId] = React.useState("");
  const [reason, setReason] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const options = useProjectAnnualBudgetClosingRecoveryApprovers(projectId, closing.id, assignmentId, open);
  const reassign = useReassignProjectAnnualBudgetClosingApprover(projectId);

  const close = (next: boolean) => {
    setOpen(next);
    if (!next) {
      setReplacementApproverId("");
      setReason("");
      setError(null);
    }
  };

  const save = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalizedReason = reason.trim();
    if (!replacementApproverId || !normalizedReason || normalizedReason.length > 1000 || !closing.approvalRowVersion) {
      setError(t("projects.paymentRequestRecoveryFieldsRequired"));
      return;
    }
    setError(null);
    try {
      await reassign.mutateAsync({
        closingId: closing.id,
        assignmentId,
        replacementApproverId,
        reason: normalizedReason,
        rowVersion: closing.rowVersion,
        approvalRowVersion: closing.approvalRowVersion,
      });
      close(false);
    } catch (cause) {
      setError(cause instanceof ApiError && cause.status === 409
        ? t("projects.paymentRequestRecoveryRefresh")
        : t("projects.paymentRequestRecoverySaveFailed"));
    }
  };

  return <>
    <Button type="button" size="sm" variant="outline" onClick={() => { setError(null); setOpen(true); }}>
      {t("projects.paymentRequestRecoveryReassign")}
    </Button>
    <SideSheet open={open} onOpenChange={close}
      title={t("projects.paymentRequestRecoveryTitle")}
      description={t("projects.paymentRequestRecoveryDescription")}>
      <form onSubmit={event => void save(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {options.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
            : options.isError ? <p role="alert" className="text-sm text-destructive">{t("projects.paymentRequestRecoveryOptionsFailed")}</p>
              : options.data?.length === 0 ? <p role="status" className="rounded-md border p-3 text-sm text-muted-foreground">{t("projects.paymentRequestRecoveryNoReplacement")}</p>
                : <div className="space-y-1.5">
                  <Label htmlFor={`closing-recovery-approver-${assignmentId}`}>{t("projects.paymentRequestRecoverySelectReplacement")}</Label>
                  <select id={`closing-recovery-approver-${assignmentId}`} className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                    value={replacementApproverId} onChange={event => setReplacementApproverId(event.target.value)} required>
                    <option value="">{t("projects.paymentRequestRecoverySelectReplacement")}</option>
                    {(options.data ?? []).map(candidate => <option key={candidate.userId} value={candidate.userId}>{candidate.displayName}</option>)}
                  </select>
                </div>}
          <div className="space-y-1.5">
            <Label htmlFor={`closing-recovery-reason-${assignmentId}`}>{t("projects.paymentRequestRecoveryReason")}</Label>
            <Textarea id={`closing-recovery-reason-${assignmentId}`} value={reason}
              onChange={event => setReason(event.target.value)} rows={4} maxLength={1000} required />
          </div>
          {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => close(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={reassign.isPending || options.isLoading || options.isError
            || !options.data?.length || !replacementApproverId || !reason.trim() || !closing.approvalRowVersion}>
            {reassign.isPending ? t("common.saving") : t("projects.paymentRequestRecoveryReassign")}
          </Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  </>;
}