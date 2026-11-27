import * as React from "react";
import { ApiError } from "@api/client";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { ProjectAnnualBudgetWorkspace } from "./ProjectAnnualBudgetWorkspace";
import { ProjectAnnualBudgetYearEndClosing } from "./ProjectAnnualBudgetYearEndClosing";
import {
  useProjectFinancialActivity,
  useProjectFinancialActivityFacets,
  type FinancialActivityFilters,
  type ProjectFinancialActivityEvent,
} from "./api/project-financials";

const PAGE_SIZE = 10;

export type FinanceWorkspaceSection = "overview" | "annualBudgets" | "paymentRequests" | "settlements" | "closing" | "auditTrail";

export function ProjectFinancialWorkspace({ projectId, canViewPaymentRequests, canViewAnnualBudgetClosing, canCreateAnnualBudget, canEditAnnualBudget, canConfigureDraftApproval, canDeleteAnnualBudget, canWithdrawAnnualBudget, canSubmitAnnualBudget, canReviseAnnualBudget, canApprove, canPrepareClosing, canApproveClosing, canCarryForward, projectCurrency, activeSection, selectedFiscalYear, onFiscalYearChange, onOpenClosing }: {
  projectId: string; canApprove: boolean; projectCurrency: string | null;
  canViewPaymentRequests: boolean; canViewAnnualBudgetClosing: boolean;
  canCreateAnnualBudget: boolean; canEditAnnualBudget: boolean; canConfigureDraftApproval: boolean; canDeleteAnnualBudget: boolean; canWithdrawAnnualBudget: boolean; canSubmitAnnualBudget: boolean; canReviseAnnualBudget: boolean;
  canPrepareClosing: boolean; canApproveClosing: boolean; canCarryForward: boolean;
  activeSection: FinanceWorkspaceSection;
  selectedFiscalYear: number | null;
  onFiscalYearChange: (year: number) => void;
  onOpenClosing: () => void;
}) {
  return <div className="space-y-4">
    <section hidden={activeSection !== "annualBudgets"}>
      <ProjectAnnualBudgetWorkspace projectId={projectId} canCreate={canCreateAnnualBudget} canEdit={canEditAnnualBudget} canConfigureDraftApproval={canConfigureDraftApproval}
        canDelete={canDeleteAnnualBudget} canWithdraw={canWithdrawAnnualBudget}
        canSubmit={canSubmitAnnualBudget} canRevise={canReviseAnnualBudget} canApprove={canApprove}
        canViewPaymentRequests={canViewPaymentRequests} canViewAnnualBudgetClosing={canViewAnnualBudgetClosing}
        canPrepareClosing={canPrepareClosing} onOpenClosing={onOpenClosing}
        projectCurrency={projectCurrency} enabled={activeSection === "annualBudgets"}
        selectedFiscalYear={selectedFiscalYear} onFiscalYearChange={onFiscalYearChange} />
    </section>
    <section hidden={activeSection !== "closing"}>
      <ProjectAnnualBudgetYearEndClosing projectId={projectId} canPrepare={canPrepareClosing}
        canApprove={canApproveClosing} canCarryForward={canCarryForward} active={activeSection === "closing"}
        selectedFiscalYear={selectedFiscalYear} onFiscalYearChange={onFiscalYearChange} />
    </section>
    <section hidden={activeSection !== "auditTrail"}>
      <ProjectFinancialActivity projectId={projectId} currency={projectCurrency} enabled={activeSection === "auditTrail"} />
    </section>
  </div>;
}

function ProjectFinancialActivity({ projectId, currency, enabled = true }: { projectId: string; currency: string | null; enabled?: boolean }) {
  const { t, formatCurrency, formatDateTime } = useI18n();
  const facets = useProjectFinancialActivityFacets(projectId, enabled);
  const [draft, setDraft] = React.useState<Omit<FinancialActivityFilters, "page" | "pageSize">>({});
  const [filters, setFilters] = React.useState<FinancialActivityFilters>({ page: 1, pageSize: PAGE_SIZE });
  const activity = useProjectFinancialActivity(projectId, filters, enabled);

  const applyFilters = (event: React.FormEvent) => {
    event.preventDefault();
    setFilters({ page: 1, pageSize: PAGE_SIZE, ...draft });
  };
  const clearFilters = () => {
    setDraft({});
    setFilters({ page: 1, pageSize: PAGE_SIZE });
  };

  return <Card>
    <CardHeader className="pb-3"><CardTitle>{t("projects.financialActivity")}</CardTitle>
      <p className="text-sm text-muted-foreground">{t("projects.financialActivityDescription")}</p>
    </CardHeader>
    <CardContent className="space-y-4">
      <details className="rounded-md border px-3 py-2">
        <summary className="cursor-pointer text-sm font-medium">{t("projects.financialActivityFilters")}</summary>
        <form className="mt-3 grid gap-3 sm:grid-cols-2 xl:grid-cols-5" onSubmit={applyFilters}>
          <div><label htmlFor="finance-activity-type" className="text-xs text-muted-foreground">{t("projects.financialActivityType")}</label>
            <select id="finance-activity-type" className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm" value={draft.eventType ?? ""} onChange={(event) => setDraft((current) => ({ ...current, eventType: event.target.value || undefined }))}>
              <option value="">{t("projects.allFinancialActivityTypes")}</option>
              {(facets.data?.eventTypes ?? []).map((eventType) => <option key={eventType} value={eventType}>{financialEntityLabel(t, eventType)}</option>)}
            </select>
          </div>
          <div><label htmlFor="finance-activity-actor" className="text-xs text-muted-foreground">{t("projects.financialActivityActor")}</label>
            <select id="finance-activity-actor" className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm" value={draft.actorId ?? ""} onChange={(event) => setDraft((current) => ({ ...current, actorId: event.target.value || undefined }))}>
              <option value="">{t("projects.allFinancialActivityActors")}</option>
              {(facets.data?.actors ?? []).map((actor) => <option key={actor.id} value={actor.id}>{actor.name}</option>)}
            </select>
          </div>
          <div><label htmlFor="finance-activity-from" className="text-xs text-muted-foreground">{t("projects.dateFrom")}</label><Input id="finance-activity-from" type="date" value={draft.from ?? ""} onChange={(event) => setDraft((current) => ({ ...current, from: event.target.value || undefined }))} /></div>
          <div><label htmlFor="finance-activity-to" className="text-xs text-muted-foreground">{t("projects.dateTo")}</label><Input id="finance-activity-to" type="date" value={draft.to ?? ""} onChange={(event) => setDraft((current) => ({ ...current, to: event.target.value || undefined }))} /></div>
          <div><label htmlFor="finance-activity-reference" className="text-xs text-muted-foreground">{t("projects.financialReference")}</label><Input id="finance-activity-reference" value={draft.reference ?? ""} onChange={(event) => setDraft((current) => ({ ...current, reference: event.target.value || undefined }))} /></div>
          <div className="flex gap-2 sm:col-span-2 xl:col-span-5"><Button size="sm" type="submit">{t("projects.applyFinancialActivityFilters")}</Button><Button size="sm" variant="outline" type="button" onClick={clearFilters}>{t("projects.clearFinancialActivityFilters")}</Button></div>
        </form>
      </details>

      {activity.isLoading ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : activity.isError ? <div className="flex items-center justify-between gap-3 rounded-md border p-4"><p role="alert" className="text-sm text-destructive">{activity.error instanceof ApiError && activity.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.financialActivityLoadFailed")}</p><Button size="sm" variant="outline" onClick={() => void activity.refetch()}>{t("common.retry")}</Button></div>
          : !activity.data?.items.length ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("projects.financialActivityEmpty")}</p>
            : <ol className="divide-y" aria-label={t("projects.financialActivity")}>
              {activity.data.items.map((event) => <FinancialActivityItem key={event.id} event={event} currency={currency} t={t} formatCurrency={formatCurrency} formatDateTime={formatDateTime} />)}
            </ol>}

      {activity.data && activity.data.total > PAGE_SIZE ? <div className="flex items-center justify-between border-t pt-3">
        <span className="text-xs text-muted-foreground">{t("projects.totalRecords", { count: activity.data.total })}</span>
        <div className="flex gap-2">
          <Button size="sm" variant="outline" disabled={filters.page <= 1 || activity.isFetching} onClick={() => setFilters((current) => ({ ...current, page: current.page - 1 }))}>{t("common.previous")}</Button>
          <Button size="sm" variant="outline" disabled={!activity.data.hasMore || activity.isFetching} onClick={() => setFilters((current) => ({ ...current, page: current.page + 1 }))}>{t("common.next")}</Button>
        </div>
      </div> : null}
    </CardContent>
  </Card>;
}

function FinancialActivityItem({
  event,
  currency,
  t,
  formatCurrency,
  formatDateTime,
}: {
  event: ProjectFinancialActivityEvent;
  currency: string | null;
  t: (key: string) => string;
  formatCurrency: (value: number, currency?: string) => string;
  formatDateTime: (date: Date | string) => string;
}) {
  const eventCurrency = event.changes.currency?.to ?? currency;
  const amountChange = event.changes.amount ?? event.changes.totalAnnualBudget ?? event.changes.approvedBudget ?? event.changes.changeAmount;
  const amount = amountChange?.to;
  const formattedAmount = amount !== null && amount !== undefined && eventCurrency && Number.isFinite(Number(amount))
    ? formatCurrency(Number(amount), eventCurrency)
    : null;
  const formatAmountSide = (value: string | null | undefined) => value !== null && value !== undefined && eventCurrency && Number.isFinite(Number(value))
    ? formatCurrency(Number(value), eventCurrency)
    : "—";

  return <li className="py-3 first:pt-0 last:pb-0">
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div className="min-w-0">
        <p className="font-medium">{financialActionLabel(t, event.entityType, event.action)}</p>
        <p className="mt-0.5 text-xs text-muted-foreground">{event.actorName || t("projects.financialActivityUnknownActor")} · {formatDateTime(event.timestampUtc)}</p>
      </div>
      {formattedAmount ? <span className="shrink-0 font-semibold tabular-nums">{formattedAmount}</span> : null}
    </div>
    {!event.detailsRestricted && (event.description || event.changes.financialReference?.to || event.changes.financialReference?.from) ? <p className="mt-1 text-xs text-muted-foreground">{event.description ?? `${t("projects.financialReference")}: ${event.changes.financialReference?.to ?? event.changes.financialReference?.from}`}</p> : null}
    {event.detailsRestricted ? <p className="mt-2 text-xs text-muted-foreground">{t("projects.financialActivityDetailsRestricted")}</p> : null}
    {!event.detailsRestricted ? <details className="mt-2 text-xs">
      <summary className="cursor-pointer text-muted-foreground">{t("projects.financialActivityDetails")}</summary>
      <dl className="mt-2 grid gap-x-4 gap-y-1 sm:grid-cols-2">
        {event.changes.financialReference ? <><dt className="text-muted-foreground">{t("projects.financialReference")}</dt><dd>{event.changes.financialReference.to ?? event.changes.financialReference.from ?? "—"}</dd></> : null}
        {amountChange ? <><dt className="text-muted-foreground">{t("projects.amount")}</dt><dd>{formatAmountSide(amountChange.from)} → {formatAmountSide(amountChange.to)}</dd></> : null}
        {event.changes.financeApprovalMode ? <><dt className="text-muted-foreground">{t("projects.financeApprovalMode")}</dt><dd>{event.changes.financeApprovalMode.from ?? "—"} → {event.changes.financeApprovalMode.to ?? "—"}</dd></> : null}
        {!event.changes.financialReference && !amountChange && !event.changes.financeApprovalMode ? <dd className="text-muted-foreground">{t("projects.financialActivityNoDetails")}</dd> : null}
        {event.changes.approvalMode ? <><dt className="text-muted-foreground">{t("projects.paymentRequestApprovalMethod")}</dt><dd>{event.changes.approvalMode.to ?? event.changes.approvalMode.from ?? "-"}</dd></> : null}
        {event.changes.status ? <><dt className="text-muted-foreground">{t("projects.status")}</dt><dd>{event.changes.status.from ?? "-"} → {event.changes.status.to ?? "-"}</dd></> : null}
        {event.changes.approvers ? <><dt className="text-muted-foreground">{t("projects.paymentRequestApprovers")}</dt><dd>{event.changes.approvers.to ?? event.changes.approvers.from ?? "-"}</dd></> : null}
        {event.changes.decision ? <><dt className="text-muted-foreground">{t("projects.paymentRequestDecision")}</dt><dd>{event.changes.decision.to ?? event.changes.decision.from ?? "-"}</dd></> : null}
        {event.changes.comment ? <><dt className="text-muted-foreground">{t("projects.paymentRequestComment")}</dt><dd>{event.changes.comment.to ?? event.changes.comment.from ?? "-"}</dd></> : null}
        {event.changes.fiscalYear ? <><dt className="text-muted-foreground">{t("projects.annualBudgetFiscalYear")}</dt><dd>{event.changes.fiscalYear.to ?? event.changes.fiscalYear.from ?? "-"}</dd></> : null}
        {event.changes.annualBudgetReference ? <><dt className="text-muted-foreground">{t("projects.annualBudgetReference")}</dt><dd>{event.changes.annualBudgetReference.to ?? event.changes.annualBudgetReference.from ?? "-"}</dd></> : null}
        {event.changes.originalPaymentRequestReference ? <><dt className="text-muted-foreground">{t("projects.paymentReversalOriginalRequest")}</dt><dd>{event.changes.originalPaymentRequestReference.to ?? event.changes.originalPaymentRequestReference.from ?? "-"}</dd></> : null}
        {event.changes.approvedReleasedAmount ? <><dt className="text-muted-foreground">{t("projects.paymentReversalReleased")}</dt><dd>{formatAmountSide(event.changes.approvedReleasedAmount.from)} → {formatAmountSide(event.changes.approvedReleasedAmount.to)}</dd></> : null}
        {event.changes.totalSettled ? <><dt className="text-muted-foreground">{t("projects.paymentRequestTotalSettled")}</dt><dd>{formatAmountSide(event.changes.totalSettled.from)} → {formatAmountSide(event.changes.totalSettled.to)}</dd></> : null}
        {event.changes.effectiveActiveAllocation ? <><dt className="text-muted-foreground">{t("projects.paymentReversalActiveAllocation")}</dt><dd>{formatAmountSide(event.changes.effectiveActiveAllocation.from)} → {formatAmountSide(event.changes.effectiveActiveAllocation.to)}</dd></> : null}
        {event.changes.remainingPayable ? <><dt className="text-muted-foreground">{t("projects.paymentRequestOutstandingAmount")}</dt><dd>{formatAmountSide(event.changes.remainingPayable.from)} → {formatAmountSide(event.changes.remainingPayable.to)}</dd></> : null}
        {event.changes.newFunding ? <><dt className="text-muted-foreground">{t("projects.annualBudgetFunding")}</dt><dd>{formatAmountSide(event.changes.newFunding.from)} → {formatAmountSide(event.changes.newFunding.to)}</dd></> : null}
        {event.changes.approvedCarryForward ? <><dt className="text-muted-foreground">{t("projects.annualBudgetCarryForward")}</dt><dd>{formatAmountSide(event.changes.approvedCarryForward.from)} → {formatAmountSide(event.changes.approvedCarryForward.to)}</dd></> : null}
        {event.changes.approvedAdjustments ? <><dt className="text-muted-foreground">{t("projects.annualBudgetAdjustments")}</dt><dd>{formatAmountSide(event.changes.approvedAdjustments.from)} → {formatAmountSide(event.changes.approvedAdjustments.to)}</dd></> : null}
      </dl>
    </details> : null}
  </li>;
}

function financialEntityLabel(t: (key: string) => string, entityType: string) {
  const keys: Record<string, string> = {
    ProjectBudget: "projects.financialActivityProjectBudget",
    ProjectFinanceConfiguration: "projects.financialActivityConfiguration",
    Budget: "projects.financialActivityBudgetLine",
    BudgetRevision: "projects.financialActivityBudgetRevision",
    CostEntry: "projects.financialActivityCost",
    PaymentObligation: "projects.financialActivityPaymentObligation",
    PaymentSettlement: "projects.financialActivitySettlement",
    PaymentSettlementAdjustment: "projects.financialActivitySettlementAdjustment",
    ProjectPaymentRequest: "projects.paymentRequestsTitle",
    ProjectPaymentReversal: "projects.paymentReversalsTitle",
    ProjectAnnualBudget: "projects.annualBudgetTitle",
    ProjectAnnualBudgetClosing: "projects.yearEndClosingTitle",
    ProjectPaymentRequestFundingTransfer: "projects.yearEndClosingReservedCarryForward",
  };
  return keys[entityType] ? t(keys[entityType]) : t("projects.financialActivityGeneric");
}

function financialActionLabel(t: (key: string) => string, entityType: string, action: string) {
  const eventKeys: Record<string, string> = {
    "ProjectBudget:InitialApprovedBudgetRecorded": "projects.financialActivityInitialBudget",
    "ProjectBudget:ApprovedBudgetChanged": "projects.financialActivityBudgetChanged",
    "ProjectFinanceConfiguration:ApprovalModeChanged": "projects.financialActivityApprovalModeChanged",
    "PaymentSettlement:SettlementRecorded": "projects.financialActivitySettlementRecorded",
    "PaymentSettlement:ActualSettlementRecorded": "projects.financialActivityActualSettlementRecorded",
    "PaymentSettlement:SettlementStatusChanged": "projects.financialActivitySettlementStatusChanged",
    "PaymentSettlementAdjustment:SettlementAdjustmentRequested": "projects.financialActivitySettlementAdjustmentRequested",
    "PaymentSettlementAdjustment:SettlementAdjustmentApproved": "projects.financialActivitySettlementAdjustmentApproved",
    "PaymentSettlementAdjustment:SettlementAdjustmentRejected": "projects.financialActivitySettlementAdjustmentRejected",
    "PaymentSettlementAdjustment:SettlementAdjustmentExecutionConfirmed": "projects.financialActivitySettlementAdjustmentExecuted",
    "PaymentObligation:PaymentObligationCreated": "projects.financialActivityPaymentCreated",
    "PaymentObligation:PaymentObligationUpdated": "projects.financialActivityPaymentUpdated",
    "PaymentObligation:PaymentObligationDeleted": "projects.financialActivityPaymentDeleted",
    "ProjectPaymentRequest:Created": "projects.financialActivityPaymentRequestCreated",
    "ProjectPaymentRequest:Edited": "projects.financialActivityPaymentRequestEdited",
    "ProjectPaymentRequest:Submitted": "projects.financialActivityPaymentRequestSubmitted",
    "ProjectPaymentRequest:ApproversAssigned": "projects.financialActivityPaymentRequestApproversAssigned",
    "ProjectPaymentRequest:ApproverApproved": "projects.financialActivityPaymentRequestApproverApproved",
    "ProjectPaymentRequest:ApproverRejected": "projects.financialActivityPaymentRequestApproverRejected",
    "ProjectPaymentRequest:Approved": "projects.financialActivityPaymentRequestApproved",
    "ProjectPaymentRequest:Rejected": "projects.financialActivityPaymentRequestRejected",
    "ProjectPaymentRequest:Cancelled": "projects.financialActivityPaymentRequestCancelled",
    "ProjectPaymentRequest:BudgetReserved": "projects.financialActivityPaymentRequestBudgetReserved",
    "ProjectAnnualBudget:Created": "projects.financialActivityAnnualBudgetCreated",
    "ProjectAnnualBudget:Updated": "projects.financialActivityAnnualBudgetUpdated",
    "ProjectAnnualBudget:Approved": "projects.financialActivityAnnualBudgetApproved",
    "ProjectAnnualBudget:DeletedDraft": "projects.financialActivityAnnualBudgetDeleted",
    "ProjectAnnualBudget:SubmissionWithdrawn": "projects.financialActivityAnnualBudgetSubmissionWithdrawn",
    "ProjectAnnualBudgetClosing:YearEndClosingPrepared": "projects.financialActivityYearEndClosingPrepared",
    "ProjectAnnualBudgetClosing:YearEndClosingSubmitted": "projects.financialActivityYearEndClosingSubmitted",
    "ProjectAnnualBudgetClosing:YearEndClosingApproved": "projects.financialActivityYearEndClosingApproved",
    "ProjectAnnualBudgetClosing:YearEndClosingRejected": "projects.financialActivityYearEndClosingRejected",
    "ProjectAnnualBudgetClosing:YearEndClosingCancelled": "projects.financialActivityYearEndClosingCancelled",
    "ProjectAnnualBudgetClosing:ClosingApproverApproved": "projects.financialActivityClosingApproverApproved",
    "ProjectAnnualBudgetClosing:ClosingApproverRejected": "projects.financialActivityClosingApproverRejected",
    "ProjectAnnualBudgetClosing:FiscalYearClosed": "projects.financialActivityFiscalYearClosed",
    "ProjectAnnualBudgetClosing:FreeCarryForwardApproved": "projects.financialActivityFreeCarryForwardApproved",
    "ProjectAnnualBudgetClosing:ReservedCarryForwardApproved": "projects.financialActivityReservedCarryForwardApproved",
    "ProjectAnnualBudgetClosing:CommitmentFundingTransferred": "projects.financialActivityCommitmentFundingTransferred",
    "ProjectAnnualBudgetClosing:AnnualBudgetOpened": "projects.financialActivityAnnualBudgetOpened",
    "ProjectPaymentReversal:ReversalRequested": "projects.financialActivityReversalRequested",
    "ProjectPaymentReversal:ReversalEdited": "projects.financialActivityReversalEdited",
    "ProjectPaymentReversal:ReversalSubmitted": "projects.financialActivityReversalSubmitted",
    "ProjectPaymentReversal:ReversalApproverAssigned": "projects.financialActivityReversalApproversAssigned",
    "ProjectPaymentReversal:ReversalApproverApproved": "projects.financialActivityReversalApproverApproved",
    "ProjectPaymentReversal:ReversalApproverRejected": "projects.financialActivityReversalApproverRejected",
    "ProjectPaymentReversal:ReversalApproved": "projects.financialActivityReversalApproved",
    "ProjectPaymentReversal:ReversalRejected": "projects.financialActivityReversalRejected",
    "ProjectPaymentReversal:BudgetAllocationReleased": "projects.financialActivityBudgetAllocationReleased",
    "ProjectPaymentReversal:ReversalCancelled": "projects.financialActivityReversalCancelled",
  };
  const eventKey = eventKeys[`${entityType}:${action}`];
  if (eventKey) return t(eventKey);
  const actionKeys: Record<string, string> = {
    Create: "projects.financialActivityActionCreated",
    Update: "projects.financialActivityActionUpdated",
    Delete: "projects.financialActivityActionDeleted",
    Close: "projects.financialActivityActionClosed",
    Cancel: "projects.financialActivityActionCancelled",
    Submit: "projects.financialActivityActionSubmitted",
    Approve: "projects.financialActivityActionApproved",
    Reject: "projects.financialActivityActionRejected",
  };
  const entityKeys: Record<string, string> = {
    ProjectBudget: "projects.financialActivityProjectBudget",
    ProjectFinanceConfiguration: "projects.financialActivityConfiguration",
    Budget: "projects.financialActivityBudgetLine",
    BudgetRevision: "projects.financialActivityBudgetRevision",
    CostEntry: "projects.financialActivityCost",
    PaymentObligation: "projects.financialActivityPaymentObligation",
    PaymentSettlement: "projects.financialActivitySettlement",
    PaymentSettlementAdjustment: "projects.financialActivitySettlementAdjustment",
    ProjectPaymentRequest: "projects.paymentRequestsTitle",
    ProjectAnnualBudget: "projects.annualBudgetTitle",
    ProjectAnnualBudgetClosing: "projects.yearEndClosingTitle",
    ProjectPaymentRequestFundingTransfer: "projects.yearEndClosingReservedCarryForward",
  };
  const entityKey = entityKeys[entityType];
  return entityKey && actionKeys[action]
    ? `${t(entityKey)}: ${t(actionKeys[action])}`
    : t("projects.financialActivityGeneric");
}
