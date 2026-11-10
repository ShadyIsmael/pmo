import * as React from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Check, Circle, Clock3, X } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { notificationKeys } from "@api/notifications";
import { useI18n } from "@i18n/messages";
import { useDecideProjectAnnualBudgetReview } from "@features/projects/api/project-annual-budgets";
import {
  annualBudgetApprovalDetailKey,
  useAnnualBudgetApprovalReviewDetail,
  type AnnualBudgetApprovalReviewDetailDto,
  type AnnualBudgetApprovalReviewerDto,
} from "./annualBudgetReviewApi";

type ReviewDecision = "Approved" | "Rejected";

export interface AnnualBudgetReviewDetailsSheetProps {
  requestId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  readOnly: boolean;
  onDecision: () => void;
}

export function AnnualBudgetReviewDetailsSheet({
  requestId,
  open,
  onOpenChange,
  readOnly,
  onDecision,
}: AnnualBudgetReviewDetailsSheetProps) {
  const { t, formatDate, formatDateTime, formatCurrency } = useI18n();
  const queryClient = useQueryClient();
  const [decisionComment, setDecisionComment] = React.useState("");
  const [commentError, setCommentError] = React.useState(false);
  const [confirming, setConfirming] = React.useState<ReviewDecision | null>(null);
  const [decisionRecorded, setDecisionRecorded] = React.useState(false);
  const detailQuery = useAnnualBudgetApprovalReviewDetail(requestId, open && Boolean(requestId));
  const detail = detailQuery.data;
  const decision = useDecideProjectAnnualBudgetReview(detail?.projectPublicId ?? "");
  const canDecide = Boolean(detail?.canDecide && !readOnly && !decisionRecorded);

  React.useEffect(() => {
    if (!open) {
      setDecisionComment("");
      setCommentError(false);
      setConfirming(null);
      setDecisionRecorded(false);
    }
  }, [open, requestId]);

  const invalidateAfterDecision = React.useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["annualBudgetApprovals", "pendingForMe"] }),
      queryClient.invalidateQueries({ queryKey: ["annualBudgetApprovals", "history"] }),
      queryClient.invalidateQueries({ queryKey: annualBudgetApprovalDetailKey(requestId) }),
      queryClient.invalidateQueries({ queryKey: ["approvalReview"] }),
      queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
    ]);
  }, [queryClient, requestId]);

  const submitDecision = async () => {
    if (!detail || !canDecide || !confirming || decision.isPending) return;
    if (confirming === "Rejected" && !decisionComment.trim()) {
      setCommentError(true);
      setConfirming(null);
      return;
    }

    try {
      await decision.mutateAsync({
        id: detail.budgetId,
        requestId: detail.requestId,
        decision: confirming,
        reason: decisionComment.trim() || undefined,
        budgetRowVersion: detail.budgetRowVersion,
        approvalRowVersion: detail.approvalRowVersion,
      });
      setDecisionRecorded(true);
      setConfirming(null);
      await invalidateAfterDecision();
      onDecision();
    } catch {
      setConfirming(null);
      await invalidateAfterDecision();
      await detailQuery.refetch();
    }
  };

  const openConfirmation = (next: ReviewDecision) => {
    if (!canDecide) return;
    if (next === "Rejected" && !decisionComment.trim()) {
      setCommentError(true);
      return;
    }
    setCommentError(false);
    setConfirming(next);
  };

  const labelStatus = (status: string) => {
    const known = new Set(["Draft", "Pending", "Approved", "Rejected", "Returned", "Cancelled", "Completed", "RevisionPending", "RevisionReturned"]);
    return known.has(status) ? t(`approvals.reviewSheet.status.${status}`) : t("approvals.reviewSheet.status.unavailable");
  };

  const disabledReason = (reason: string | null) => {
    const known = new Set([
      "request_completed",
      "already_decided",
      "approval_permission_required",
      "scope_read_only",
      "reviewer_not_currently_eligible",
      "waiting_for_previous_approvers",
    ]);
    return reason && known.has(reason)
      ? t(`approvals.reviewSheet.disabled.${reason}`)
      : t("approvals.reviewSheet.actionUnavailable");
  };

  const displayCurrency = (amount: number | null | undefined, currency: string | null | undefined) => {
    if (typeof amount !== "number" || !Number.isFinite(amount) || !currency || !/^[A-Za-z]{3}$/.test(currency)) {
      return t("approvals.reviewSheet.unavailable");
    }
    return formatCurrency(amount, currency);
  };

  return (
    <>
      <SideSheet
        open={open}
        onOpenChange={onOpenChange}
        title={t("approvals.reviewSheet.title")}
        description={t("approvals.reviewSheet.description")}
        size="xl"
        closeLabel={t("common.close")}
        ariaLabel={t("approvals.reviewSheet.title")}
      >
        {detailQuery.isLoading ? (
          <SideSheetBody>
            <div role="status" className="py-10 text-center text-sm text-muted-foreground">{t("common.loading")}</div>
          </SideSheetBody>
        ) : detailQuery.isError ? (
          <SideSheetBody>
            <div role="alert" className="space-y-3 rounded-md border border-destructive/30 p-4 text-sm">
              <p>{t("approvals.reviewSheet.loadFailed")}</p>
              <Button type="button" variant="outline" onClick={() => void detailQuery.refetch()}>{t("common.retry")}</Button>
            </div>
          </SideSheetBody>
        ) : detail ? (
          <>
            <SideSheetBody className="space-y-5">
              <RequestInformation detail={detail} labelStatus={labelStatus} formatDate={formatDate} t={t} />
              <FinancialOverview detail={detail} displayCurrency={displayCurrency} t={t} />
              <ApprovalProgress detail={detail} labelStatus={labelStatus} formatDateTime={formatDateTime} t={t} />
              {detail.requestDescription || detail.submissionComment || detail.reason ? (
                <section className="space-y-3 rounded-lg border p-4">
                  <h3 className="text-sm font-semibold">{t("approvals.reviewSheet.requestNotes")}</h3>
                  {detail.requestDescription ? <LabeledText label={t("approvals.reviewSheet.descriptionLabel")} value={detail.requestDescription} /> : null}
                  {detail.submissionComment ? <LabeledText label={t("approvals.reviewSheet.submissionComment")} value={detail.submissionComment} /> : null}
                  {detail.reason ? <LabeledText label={t("approvals.reviewSheet.justification")} value={detail.reason} /> : null}
                </section>
              ) : null}
              {canDecide ? (
                <div className="space-y-2">
                  <label htmlFor="annual-budget-decision-comment" className="text-sm font-medium">{t("approvals.reviewSheet.decisionComment")}</label>
                  <Textarea
                    id="annual-budget-decision-comment"
                    value={decisionComment}
                    onChange={(event) => {
                      setDecisionComment(event.target.value);
                      if (event.target.value.trim()) setCommentError(false);
                    }}
                    aria-invalid={commentError}
                    aria-describedby={commentError ? "annual-budget-decision-comment-error" : undefined}
                    placeholder={t("approvals.reviewSheet.decisionCommentPlaceholder")}
                    maxLength={2000}
                  />
                  {commentError ? <p id="annual-budget-decision-comment-error" role="alert" className="text-sm text-destructive">{t("approvals.reviewSheet.rejectReasonRequired")}</p> : null}
                </div>
              ) : (
                <p role="status" className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
                  {detail.disabledReason
                    ? disabledReason(detail.disabledReason)
                    : readOnly || decisionRecorded
                      ? t("approvals.reviewSheet.readOnly")
                      : t("approvals.reviewSheet.actionUnavailable")}
                </p>
              )}
              {decision.isError ? <p role="alert" className="text-sm text-destructive">{t("approvals.reviewSheet.decisionFailed")}</p> : null}
            </SideSheetBody>
            <SideSheetFooter className="border-t bg-background/95 sm:items-end sm:justify-between">
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.close")}</Button>
              {canDecide ? (
                <div className="flex flex-col-reverse gap-2 sm:flex-row">
                  <Button type="button" variant="destructive" disabled={decision.isPending} onClick={() => openConfirmation("Rejected")}>
                    {t("approvals.reviewSheet.reject")}
                  </Button>
                  <Button type="button" disabled={decision.isPending} onClick={() => openConfirmation("Approved")}>
                    {decision.isPending ? t("common.saving") : t("approvals.reviewSheet.approve")}
                  </Button>
                </div>
              ) : null}
            </SideSheetFooter>
          </>
        ) : null}
      </SideSheet>

      <Dialog open={Boolean(confirming)} onOpenChange={(value) => { if (!value && !decision.isPending) setConfirming(null); }} role="alertdialog" ariaLabel={t("approvals.reviewSheet.confirmTitle")}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t(confirming === "Rejected" ? "approvals.reviewSheet.rejectConfirmTitle" : "approvals.reviewSheet.approveConfirmTitle")}</DialogTitle>
            <DialogDescription>{t(confirming === "Rejected" ? "approvals.reviewSheet.rejectConfirmDescription" : "approvals.reviewSheet.approveConfirmDescription")}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" variant="outline" disabled={decision.isPending} onClick={() => setConfirming(null)}>{t("common.cancel")}</Button>
            <Button type="button" variant={confirming === "Rejected" ? "destructive" : "default"} disabled={decision.isPending} onClick={() => void submitDecision()}>
              {decision.isPending ? t("common.saving") : t("common.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

function RequestInformation({
  detail,
  labelStatus,
  formatDate,
  t,
}: {
  detail: AnnualBudgetApprovalReviewDetailDto;
  labelStatus: (status: string) => string;
  formatDate: (date: Date | string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  return (
    <section className="space-y-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <h3 className="font-semibold">{detail.projectName}{detail.projectCode ? ` · ${detail.projectCode}` : ""}</h3>
          <p className="mt-1 text-sm text-muted-foreground">{detail.requestTitle}</p>
        </div>
        <Badge status={detail.status}>{labelStatus(detail.status)}</Badge>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <LabeledText label={t("approvals.reviewSheet.requestType")} value={labelRequestType(detail.requestType, t)} />
        <LabeledText label={t("approvals.reviewSheet.fiscalYear")} value={t("approvals.reviewSheet.fiscalYearValue", { year: detail.fiscalYear })} />
        <LabeledText label={t("approvals.reviewSheet.fiscalPeriod")} value={`${formatDate(detail.fiscalYearStartDate)} – ${formatDate(detail.fiscalYearEndDate)}`} />
        <LabeledText label={t("approvals.requestedBy")} value={detail.requestedByName || t("approvals.reviewSheet.unavailable")} />
        <LabeledText label={t("approvals.requestedAt")} value={formatDate(detail.requestedAtUtc)} />
        <LabeledText label={t("approvals.reviewSheet.budgetStatus")} value={labelStatus(detail.budgetStatus)} />
      </div>
    </section>
  );
}

function FinancialOverview({
  detail,
  displayCurrency,
  t,
}: {
  detail: AnnualBudgetApprovalReviewDetailDto;
  displayCurrency: (amount: number | null | undefined, currency: string | null | undefined) => string;
  t: (key: string) => string;
}) {
  return (
    <section className="space-y-3">
      <h3 className="text-sm font-semibold">{t("approvals.reviewSheet.financialOverview")}</h3>
      <div className="grid gap-3 md:grid-cols-2">
        <div className="space-y-3 rounded-lg border border-primary/30 bg-primary/5 p-4">
          <div>
            <h4 className="text-sm font-semibold">{t("approvals.reviewSheet.lifetimeProjectBudget")}</h4>
            <p className="mt-1 text-xs text-muted-foreground">{t("approvals.reviewSheet.lifetimeDescription")}</p>
          </div>
          <p className="text-xl font-semibold tabular-nums">{displayCurrency(detail.projectBudget, detail.projectCurrency)}</p>
        </div>
        <div className="space-y-3 rounded-lg border p-4">
          <div>
            <h4 className="text-sm font-semibold">{t("approvals.reviewSheet.annualBudgetUnderReview")}</h4>
            <p className="mt-1 text-xs text-muted-foreground">{detail.currency}</p>
          </div>
          <div className="space-y-2 text-sm">
            <MoneyLine label={t("approvals.reviewSheet.newFunding")} value={displayCurrency(detail.newFunding, detail.currency)} />
            <MoneyLine label={t("approvals.reviewSheet.approvedCarryForward")} value={displayCurrency(detail.approvedCarryForward, detail.currency)} />
            <MoneyLine label={t("approvals.reviewSheet.approvedAdjustments")} value={displayCurrency(detail.approvedAdjustments, detail.currency)} />
            <MoneyLine label={t("approvals.reviewSheet.totalAnnualBudget")} value={displayCurrency(detail.totalAnnualBudget, detail.currency)} strong />
          </div>
        </div>
      </div>
    </section>
  );
}

function ApprovalProgress({
  detail,
  labelStatus,
  formatDateTime,
  t,
}: {
  detail: AnnualBudgetApprovalReviewDetailDto;
  labelStatus: (status: string) => string;
  formatDateTime: (date: Date | string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const parallelActive = detail.approvalMode === "Parallel"
    ? detail.steps.filter((step) => step.workflowState === "Active" || step.isActionable)
    : [];
  const waiting = detail.steps.filter((step) => step.workflowState === "Waiting");
  const sequentialSteps = detail.approvalMode === "Sequential"
    ? [...detail.steps].sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
    : detail.steps;

  return (
    <section className="space-y-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">{t("approvals.reviewSheet.approvalProgress")}</h3>
          <p className="mt-1 text-xs text-muted-foreground">{t(`approvals.reviewSheet.${detail.approvalMode.toLowerCase()}Method`)}</p>
        </div>
        <Badge variant="outline">{t(`approvals.reviewSheet.${detail.approvalMode.toLowerCase()}Method`)}</Badge>
      </div>

      {detail.approvalMode === "Parallel" && parallelActive.length > 0 ? (
        <div className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("approvals.reviewSheet.activeReviewers")}</h4>
          <div className="grid gap-2 sm:grid-cols-2">{parallelActive.map((step) => <ReviewerStep key={step.assignmentId ?? step.userId} step={step} sequential={false} labelStatus={labelStatus} formatDateTime={formatDateTime} t={t} />)}</div>
        </div>
      ) : null}

      {detail.approvalMode === "Sequential" ? (
        <ol className="space-y-2" aria-label={t("approvals.reviewSheet.sequentialTimeline")}>
          {sequentialSteps.map((step) => <li key={step.assignmentId ?? step.userId}><ReviewerStep step={step} sequential labelStatus={labelStatus} formatDateTime={formatDateTime} t={t} /></li>)}
        </ol>
      ) : null}

      {detail.approvalMode === "Parallel" ? (
        <div className="space-y-2">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("approvals.reviewSheet.allReviewers")}</h4>
          <div className="grid gap-2 sm:grid-cols-2">
            {detail.steps.filter((step) => !parallelActive.includes(step)).map((step) => <ReviewerStep key={step.assignmentId ?? step.userId} step={step} sequential={false} labelStatus={labelStatus} formatDateTime={formatDateTime} t={t} />)}
          </div>
        </div>
      ) : null}

      {waiting.length > 0 ? <p className="text-xs text-muted-foreground">{t("approvals.reviewSheet.waitingCount", { count: waiting.length })}</p> : null}
      {detail.canDecide ? <p className="rounded-md bg-primary/10 px-3 py-2 text-sm font-medium text-primary">{t("approvals.reviewSheet.youAwaitingDecision")}</p> : null}
    </section>
  );
}

function ReviewerStep({
  step,
  sequential,
  labelStatus,
  formatDateTime,
  t,
}: {
  step: AnnualBudgetApprovalReviewerDto;
  sequential: boolean;
  labelStatus: (status: string) => string;
  formatDateTime: (date: Date | string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const completed = Boolean(step.decision || step.state === "Approved" || step.state === "Rejected");
  const icon = completed ? <Check className="h-4 w-4" aria-hidden="true" />
    : step.isActionable ? <Clock3 className="h-4 w-4" aria-hidden="true" />
      : <Circle className="h-4 w-4" aria-hidden="true" />;
  const stateText = step.isCurrentUser && step.isActionable
    ? t("approvals.reviewSheet.youAwaitingDecision")
    : step.decision ? labelStatus(step.decision)
      : step.workflowState === "Waiting" ? t("approvals.reviewSheet.waiting")
        : step.workflowState === "Active" ? t("approvals.reviewSheet.active")
          : step.workflowState === "NotRequired" ? t("approvals.reviewSheet.notRequired")
          : labelStatus(step.state);

  return (
    <div className="flex min-w-0 gap-3 rounded-md border p-3">
      <span className={`mt-0.5 shrink-0 ${completed ? "text-success" : step.isActionable ? "text-primary" : "text-muted-foreground"}`}>{icon}</span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{step.displayName || t("approvals.reviewSheet.unavailable")}{step.isCurrentUser ? ` — ${t("approvals.reviewSheet.you")}` : ""}</p>
            {step.requirement ? <p className="text-xs text-muted-foreground">{t(`approvals.reviewSheet.requirement.${step.requirement}`)}</p> : null}
          </div>
          <div className="flex items-center gap-2">
            {sequential && step.position !== null ? <Badge variant="outline">{t("approvals.reviewSheet.stepNumber", { number: step.position })}</Badge> : null}
            <Badge status={step.decision ?? step.state}>{stateText}</Badge>
          </div>
        </div>
        {step.decidedAtUtc ? <p className="mt-1 text-xs text-muted-foreground">{t("approvals.reviewSheet.decidedAt", { date: formatDateTime(step.decidedAtUtc) })}</p> : null}
        {step.comment ? <p className="mt-2 whitespace-pre-wrap text-sm text-muted-foreground">{step.comment}</p> : null}
      </div>
    </div>
  );
}

function LabeledText({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-0.5 break-words text-sm">{value}</p></div>;
}

function MoneyLine({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return <div className={`flex items-start justify-between gap-3 ${strong ? "border-t pt-2 font-semibold" : ""}`}><span>{label}</span><span className="text-end tabular-nums">{value}</span></div>;
}

function labelRequestType(type: string, t: (key: string) => string) {
  if (type === "ProjectAnnualBudgetRevision" || type === "Revision") return t("approvals.reviewSheet.revisionRequest");
  if (type === "ProjectAnnualBudgetReview" || type === "Initial") return t("approvals.reviewSheet.initialRequest");
  return t("approvals.reviewSheet.unavailable");
}
