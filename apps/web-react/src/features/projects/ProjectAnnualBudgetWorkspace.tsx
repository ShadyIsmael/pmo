import * as React from "react";
import { ApiError } from "@api/client";
import { ArrowDown, ArrowUp, Check, Pencil, Plus, Send, Undo2 } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { Textarea } from "@components/ui/Textarea";
import { useProjectAnnualBudgetClosings, useProjectPaymentRequests, type PaymentRequestApproverSelection } from "./api/project-financials";
import {
  useCreateProjectAnnualBudget,
  useDecideProjectAnnualBudgetReview,
  useProjectAnnualBudgetApprovalConfiguration,
  useProjectAnnualBudgets,
  useReviseApprovedProjectAnnualBudget,
  useSubmitProjectAnnualBudgetReview,
  useUpdateProjectAnnualBudget,
  type ProjectAnnualBudget,
} from "./api/project-annual-budgets";

type BudgetForm = { fiscalYear: string; funding: string; adjustments: string };
const emptyForm = (fiscalYear = ""): BudgetForm => ({ fiscalYear, funding: "", adjustments: "0" });
export function ProjectAnnualBudgetWorkspace({
  projectId,
  canCreate,
  canEdit,
  canSubmit,
  canRevise,
  canApprove,
  canViewPaymentRequests,
  canViewAnnualBudgetClosing,
  projectCurrency,
  enabled = true,
  selectedFiscalYear,
  onFiscalYearChange,
}: {
  projectId: string;
  canCreate: boolean;
  canEdit: boolean;
  canSubmit: boolean;
  canRevise: boolean;
  canApprove: boolean;
  canViewPaymentRequests: boolean;
  canViewAnnualBudgetClosing: boolean;
  projectCurrency: string | null;
  enabled?: boolean;
  selectedFiscalYear?: number | null;
  onFiscalYearChange?: (year: number) => void;
}) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { user } = useAuth();
  const query = useProjectAnnualBudgets(projectId, enabled);
  const paymentRequests = useProjectPaymentRequests(projectId, enabled && canViewPaymentRequests);
  const closings = useProjectAnnualBudgetClosings(projectId, enabled && canViewAnnualBudgetClosing);
  const create = useCreateProjectAnnualBudget(projectId);
  const update = useUpdateProjectAnnualBudget(projectId);
  const submitReview = useSubmitProjectAnnualBudgetReview(projectId);
  const revise = useReviseApprovedProjectAnnualBudget(projectId);
  const decideReview = useDecideProjectAnnualBudgetReview(projectId);
  const approvalConfiguration = useProjectAnnualBudgetApprovalConfiguration(projectId, enabled);
  const [reviewSetupOpen, setReviewSetupOpen] = React.useState(false);
  const [revisionMode, setRevisionMode] = React.useState(false);
  const [approvalMode, setApprovalMode] = React.useState<"Sequential" | "Parallel" | null>(null);
  const [customizeForSubmission, setCustomizeForSubmission] = React.useState(false);
  const [overrideReason, setOverrideReason] = React.useState("");
  const [selectedReviewers, setSelectedReviewers] = React.useState<string[]>([]);
  const [revisionFunding, setRevisionFunding] = React.useState("");
  const [revisionAdjustments, setRevisionAdjustments] = React.useState("0");
  const [revisionReason, setRevisionReason] = React.useState("");
  const [returnReason, setReturnReason] = React.useState("");
  const [reviewError, setReviewError] = React.useState<string | null>(null);
  const [selectedYear, setSelectedYear] = React.useState<number | null>(null);
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<ProjectAnnualBudget | null>(null);
  const [form, setForm] = React.useState<BudgetForm>(emptyForm());
  const [formError, setFormError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!enabled) setSheetOpen(false);
  }, [enabled]);
  const [actionError, setActionError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (selectedFiscalYear !== undefined && selectedFiscalYear !== null && selectedFiscalYear !== selectedYear) {
      setSelectedYear(selectedFiscalYear);
      return;
    }
    if (selectedYear === null && query.data) setSelectedYear(query.data.currentFiscalYear);
  }, [query.data?.currentFiscalYear, selectedFiscalYear, selectedYear]);

  const page = query.data;
  const items = page?.items ?? [];
  const selected = items.find(item => item.fiscalYear === selectedYear) ?? null;
  const currencyMatches = Boolean(projectCurrency)
    && selected?.currency === projectCurrency?.trim().toUpperCase();
  const selectedClosing = closings.data?.items.find(item => item.fiscalYear === selected?.fiscalYear && item.status === "Closed");
  const currentAvailability = paymentRequests.data?.budgetAvailability;
  const selectedAvailability = selected
    && currentAvailability?.fiscalYear === selected.fiscalYear
    && currentAvailability.annualBudgetId === selected.id
    && currentAvailability.currency === selected.currency
      ? currentAvailability
      : null;
  const years = [...new Set([...(page ? [page.currentFiscalYear] : []), ...items.map(item => item.fiscalYear)])]
    .sort((left, right) => right - left);
  const busy = create.isPending || update.isPending || submitReview.isPending || revise.isPending || decideReview.isPending;

  const openCreate = (fiscalYear: number | null = selectedYear) => {
    setEditing(null);
    setForm(emptyForm(fiscalYear === null ? "" : fiscalYear.toString()));
    setFormError(null);
    setSheetOpen(true);
  };

  const openEdit = (item: ProjectAnnualBudget) => {
    setEditing(item);
    setForm({ fiscalYear: item.fiscalYear.toString(), funding: item.newApprovedFunding.toString(), adjustments: item.approvedAdjustments.toString() });
    setFormError(null);
    setSheetOpen(true);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setFormError(null);
    const fiscalYear = Number(form.fiscalYear);
    const funding = Number(form.funding);
    const adjustments = Number(form.adjustments);
    if (!editing && (!Number.isInteger(fiscalYear) || fiscalYear < 1900 || fiscalYear > 2200)) {
      setFormError(t("projects.annualBudgetYearInvalid"));
      return;
    }
    if (!Number.isFinite(funding) || !Number.isFinite(adjustments) || funding < 0 || funding + adjustments < 0) {
      setFormError(t("projects.annualBudgetAmountsInvalid"));
      return;
    }
    try {
      if (editing) {
        await update.mutateAsync({ id: editing.id, newApprovedFunding: funding, approvedAdjustments: adjustments, rowVersion: editing.rowVersion });
      } else {
        await create.mutateAsync({ fiscalYear, newApprovedFunding: funding, approvedAdjustments: adjustments });
        setSelectedYear(fiscalYear);
        onFiscalYearChange?.(fiscalYear);
      }
      setSheetOpen(false);
    } catch {
      setFormError(t("projects.annualBudgetSaveFailed"));
    }
  };

  const onApprove = async (item: ProjectAnnualBudget) => {
    try {
      if (!item.reviewRequestId || !item.reviewRowVersion) return;
      await decideReview.mutateAsync({ id: item.id, requestId: item.reviewRequestId, decision: "Approved", budgetRowVersion: item.rowVersion, approvalRowVersion: item.reviewRowVersion });
      setActionError(null);
    } catch {
      setActionError(t("projects.annualBudgetReviewActionFailed"));
    }
  };

  const openReviewSetup = (asRevision: boolean) => {
    setRevisionMode(asRevision);
    setRevisionFunding(selected?.newApprovedFunding.toString() ?? "");
    setRevisionAdjustments(selected?.approvedAdjustments.toString() ?? "0");
    setRevisionReason("");
    setSelectedReviewers([]);
    setCustomizeForSubmission(false);
    setOverrideReason("");
    setReviewError(null);
    const configuration = approvalConfiguration.data;
    setApprovalMode(configuration?.approvalMode ?? null);
    setSelectedReviewers((configuration?.configuredApprovers ?? [])
      .slice()
      .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId));
    setReviewSetupOpen(true);
    void approvalConfiguration.refetch();
  };

  React.useEffect(() => {
    if (!reviewSetupOpen || customizeForSubmission || !approvalConfiguration.data) return;
    setApprovalMode(approvalConfiguration.data.approvalMode);
    setSelectedReviewers(approvalConfiguration.data.configuredApprovers
      .slice()
      .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId));
  }, [reviewSetupOpen, customizeForSubmission, approvalConfiguration.data]);

  const reviewerSelections = (mode: "Sequential" | "Parallel", reviewers: string[]): PaymentRequestApproverSelection[] => reviewers.map((userId, index) => ({
    userId,
    position: mode === "Sequential" ? index + 1 : null,
    requirement: "Mandatory",
  }));

  const submitBudgetForReview = async (item: ProjectAnnualBudget) => {
    const configuration = approvalConfiguration.data;
    if (!configuration) {
      setReviewError(t("projects.annualBudgetApprovalConfigurationLoadFailed"));
      return;
    }
    const selectedMode = customizeForSubmission ? approvalMode : configuration.approvalMode;
    const reviewers = customizeForSubmission
      ? selectedReviewers
      : configuration.configuredApprovers
        .slice()
        .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
        .map(approver => approver.userId);
    if (customizeForSubmission && !configuration.canOverride) {
      setReviewError(t("projects.annualBudgetOverrideNotAuthorized"));
      return;
    }
    if (!customizeForSubmission && (!configuration.isValid || !configuration.canSubmitWithDefaults)) {
      setReviewError(t("projects.annualBudgetDefaultsCannotSubmit"));
      return;
    }
    if (!selectedMode || reviewers.length === 0 || new Set(reviewers).size !== reviewers.length) {
      setReviewError(t("projects.annualBudgetReviewerRequired"));
      return;
    }
    if (customizeForSubmission && !overrideReason.trim()) {
      setReviewError(t("projects.annualBudgetOverrideReasonRequired"));
      return;
    }
    try {
      if (revisionMode) {
        const funding = Number(revisionFunding);
        const adjustments = Number(revisionAdjustments);
        if (!Number.isFinite(funding) || !Number.isFinite(adjustments) || funding < 0 || funding + adjustments < 0 || !revisionReason.trim()) {
          setReviewError(t("projects.annualBudgetAmountsInvalid"));
          return;
        }
        await revise.mutateAsync({
          id: item.id,
          newApprovedFunding: funding,
          approvedAdjustments: adjustments,
          reason: revisionReason.trim(),
          approvalMode: selectedMode,
          approvers: reviewerSelections(selectedMode, reviewers),
          rowVersion: item.rowVersion,
          customizeForThisSubmission: customizeForSubmission,
          overrideReason: customizeForSubmission ? overrideReason.trim() : null,
          approvalConfigurationRowVersion: configuration.rowVersion,
          projectSettingsRowVersion: configuration.projectSettingsRowVersion,
        });
      } else {
        await submitReview.mutateAsync({
          id: item.id,
          approvalMode: selectedMode,
          approvers: reviewerSelections(selectedMode, reviewers),
          rowVersion: item.rowVersion,
          customizeForThisSubmission: customizeForSubmission,
          overrideReason: customizeForSubmission ? overrideReason.trim() : null,
          approvalConfigurationRowVersion: configuration.rowVersion,
          projectSettingsRowVersion: configuration.projectSettingsRowVersion,
        });
      }
      setReviewSetupOpen(false);
      setReviewError(null);
    } catch (error) {
      setReviewError(error instanceof ApiError && error.status === 403
        ? t("projects.annualBudgetOverrideNotAuthorized")
        : error instanceof ApiError && error.status === 409
          ? t("projects.annualBudgetApprovalConfigurationStale")
          : t("projects.annualBudgetReviewActionFailed"));
    }
  };

  const returnBudgetForChanges = async (item: ProjectAnnualBudget) => {
    if (!item.reviewRequestId || !item.reviewRowVersion) return;
    if (!returnReason.trim()) {
      setReviewError(t("projects.annualBudgetReturnReasonRequired"));
      return;
    }
    try {
      await decideReview.mutateAsync({ id: item.id, requestId: item.reviewRequestId, decision: "Returned", reason: returnReason.trim(), budgetRowVersion: item.rowVersion, approvalRowVersion: item.reviewRowVersion });
      setReturnReason("");
      setReviewError(null);
    } catch {
      setReviewError(t("projects.annualBudgetReviewActionFailed"));
    }
  };

  const updateReviewer = (userId: string, enabled: boolean) => setSelectedReviewers(current => enabled
    ? current.includes(userId) ? current : [...current, userId]
    : current.filter(id => id !== userId));

  const moveReviewer = (userId: string, delta: -1 | 1) => setSelectedReviewers(current => {
    const index = current.indexOf(userId);
    const next = index + delta;
    if (index < 0 || next < 0 || next >= current.length) return current;
    const reordered = [...current];
    [reordered[index], reordered[next]] = [reordered[next], reordered[index]];
    return reordered;
  });

  const money = (amount: number | null | undefined, currency?: string) =>
    typeof amount === "number" && Number.isFinite(amount) && currency ? formatCurrency(amount, currency) : "-";
  const activeConfiguration = approvalConfiguration.data;
  const activeApprovalMode = customizeForSubmission ? approvalMode : activeConfiguration?.approvalMode ?? null;
  const activeApproverIds = customizeForSubmission
    ? selectedReviewers
    : activeConfiguration?.configuredApprovers
      .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId) ?? [];
  const activeEligibleApproverIds = new Set(activeConfiguration?.eligibleApprovers.map(item => item.userId) ?? []);
  const canSubmitReview = Boolean(activeConfiguration && activeApprovalMode && activeApproverIds.length > 0
    && new Set(activeApproverIds).size === activeApproverIds.length
    && activeApproverIds.every(id => activeEligibleApproverIds.has(id) && id !== user?.id)
    && (customizeForSubmission
      ? activeConfiguration.canOverride && Boolean(overrideReason.trim())
      : activeConfiguration.isValid && activeConfiguration.canSubmitWithDefaults));

  return <section className="space-y-4" aria-label={t("projects.annualBudgetTitle")}>
    {page ? <div className="grid gap-3 sm:grid-cols-3">
      <Metric label={t("projects.financeKpiProjectLifetimeCeiling")} value={money(page.totalProjectFinancialCeiling, page.projectCurrency)} />
      <Metric label={t("projects.financeKpiApprovedNewAllocations")} value={money(page.totalApprovedNewAllocations, page.projectCurrency)} />
      <Metric label={t("projects.financeKpiRemainingUnallocatedCeiling")} value={money(page.remainingUnallocatedCeiling, page.projectCurrency)} hint={page.pendingSubmittedAllocations > 0 ? t("projects.annualBudgetAllocationCeilingHint") : undefined} />
    </div> : null}
    <Card>
      <CardHeader className="flex flex-wrap items-start justify-between gap-3 pb-3">
        <div>
          <CardTitle>{t("projects.annualBudgetTitle")}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{t("projects.annualBudgetDescription")}</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          {selectedFiscalYear === undefined ? <div>
            <Label htmlFor="project-annual-budget-year">{t("projects.annualBudgetFiscalYear")}</Label>
            <select id="project-annual-budget-year" className="mt-1 h-10 rounded-md border bg-background px-3 text-sm" value={selectedYear ?? ""} onChange={event => {
              const year = Number(event.target.value);
              setSelectedYear(year);
              onFiscalYearChange?.(year);
            }} disabled={query.isLoading || years.length === 0}>
              {years.map(year => <option key={year} value={year}>{t("projects.fiscalYearShort")} {year}</option>)}
            </select>
          </div> : null}
          {canCreate ? <Button size="sm" variant="outline" disabled={!projectCurrency} onClick={() => openCreate(selected ? null : selectedYear)}>
            <Plus className="me-1.5 h-4 w-4" />{selected ? t("projects.annualBudgetCreateAnother") : t("projects.annualBudgetCreate")}
          </Button> : null}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {(canCreate || canEdit) && !projectCurrency ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.financialCurrencyUnavailable")}</p> : null}
        {query.isLoading ? <p role="status" className="py-4 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : query.isError ? <div className="flex items-center justify-between gap-3 rounded-md border p-4"><p role="alert" className="text-sm text-destructive">{query.error instanceof ApiError && query.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.annualBudgetHistoryLoadFailed")}</p><Button size="sm" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div>
            : selected ? <div className="space-y-4">
              {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{actionError}</p> : null}
              {!currencyMatches ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{projectCurrency ? t("projects.annualBudgetCurrencyMismatch") : t("projects.financialCurrencyUnavailable")}</p> : null}
              <div className="flex flex-wrap items-start justify-between gap-3 rounded-md border p-4">
                <div>
                  <div className="flex flex-wrap items-center gap-2"><h3 className="font-semibold">{selected.fiscalYearLabel}</h3><Badge status={selected.status}>{t(`projects.annualBudget${selected.reviewStatus}`)}</Badge></div>
                  <p className="mt-1 text-xs text-muted-foreground">{selected.financialReference} · {formatDate(selected.fiscalYearStartDate)} – {formatDate(selected.fiscalYearEndDate)}</p>
                </div>
                <div className="flex gap-2">
                  {canEdit && selected.canEdit && currencyMatches ? <Button size="sm" variant="outline" onClick={() => openEdit(selected)}><Pencil className="me-1.5 h-4 w-4" />{t("projects.annualBudgetEdit")}</Button> : null}
                  {canSubmit && selected.canSubmit && currencyMatches && page?.totalProjectFinancialCeiling !== null ? <Button size="sm" onClick={() => openReviewSetup(false)}><Send className="me-1.5 h-4 w-4" />{t("projects.annualBudgetSubmitForReview")}</Button> : null}
                  {canRevise && selected.canRevise && currencyMatches ? <Button size="sm" variant="outline" onClick={() => openReviewSetup(true)}><Pencil className="me-1.5 h-4 w-4" />{t("projects.annualBudgetRevision")}</Button> : null}
                </div>
              </div>
              {selected.submittedAtUtc ? <p className="text-xs text-muted-foreground">{t("projects.annualBudgetSubmitted")} · {formatDate(selected.submittedAtUtc)}</p> : null}
              {canApprove && selected.canReview ? <div className="space-y-2 rounded-md border bg-muted/20 p-3">
                <Label htmlFor="annual-budget-return-reason">{t("projects.annualBudgetReason")}</Label>
                <Textarea id="annual-budget-return-reason" value={returnReason} onChange={event => setReturnReason(event.target.value)} rows={2} placeholder={t("projects.annualBudgetRevisionReason")} />
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" onClick={() => void onApprove(selected)} disabled={busy}><Check className="me-1.5 h-4 w-4" />{busy ? t("common.saving") : t("projects.annualBudgetApprove")}</Button>
                  <Button size="sm" variant="outline" onClick={() => void returnBudgetForChanges(selected)} disabled={busy}><Undo2 className="me-1.5 h-4 w-4" />{t("projects.annualBudgetReturnForChanges")}</Button>
                </div>
              </div> : null}
              {!canApprove && selected.reviewStatus === "Pending" ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.annualBudgetApprovalPermissionHint")}</p> : null}
              {(canCreate || canEdit || canSubmit) && selected.status === "Draft" && page?.totalProjectFinancialCeiling === null ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.annualBudgetCeilingRequired")}</p> : null}
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <Metric label={t("projects.annualBudgetFunding")} value={money(selected.newApprovedFunding, selected.currency)} />
                <Metric label={t("projects.annualBudgetCarryForward")} value={money(selected.approvedCarryForward, selected.currency)} hint={t("projects.annualBudgetCarryForwardHint")} />
                {selected.approvedReservedCarryForward > 0 ? <Metric label={t("projects.yearEndClosingReservedCarryForward")} value={money(selected.approvedReservedCarryForward, selected.currency)} /> : null}
                {selected.approvedFreeCarryForward > 0 ? <Metric label={t("projects.yearEndClosingFreeCarryForward")} value={money(selected.approvedFreeCarryForward, selected.currency)} /> : null}
                <Metric label={t("projects.annualBudgetAdjustments")} value={money(selected.approvedAdjustments, selected.currency)} />
                <Metric label={t("projects.annualBudgetTotal")} value={money(selected.totalAnnualBudget, selected.currency)} strong />
                <Metric
                  label={t("projects.financeKpiActualSettlements")}
                  value={selectedClosing ? money(selectedClosing.actualSettlementsInFiscalYear, selectedClosing.currency) : t("projects.financeKpiUnavailable")}
                  hint={selectedClosing ? t("projects.financeKpiActualSettlementsFinalized") : t("projects.financeKpiActualSettlementsUnavailable")}
                />
                <Metric
                  label={t("projects.financeKpiAvailableBudget")}
                  value={selectedAvailability && ["AnnualBudgetDraft", "Ready", "NoAvailableBalance"].includes(selectedAvailability.availabilityStatus) && selectedAvailability.availableAmount !== null
                    ? money(selectedAvailability.availableAmount, selectedAvailability.currency)
                    : t("projects.financeKpiUnavailable")}
                  hint={selectedAvailability?.availabilityStatus === "AnnualBudgetDraft"
                    ? t("projects.financeKpiNoAuthorizedAnnualBudget")
                    : selectedAvailability?.availabilityStatus === "Ready" || selectedAvailability?.availabilityStatus === "NoAvailableBalance"
                      ? t("projects.financeKpiAvailableCurrentYearOnly")
                    : t("projects.annualBudgetAvailableUnavailable")}
                />
              </div>
              {selected.status === "Approved" && selected.totalAnnualBudget === 0 ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.annualBudgetApprovedZero")}</p> : null}
              <p className="text-xs text-muted-foreground">{t("projects.annualBudgetLegacyBudgetHint")}</p>
              <div className="space-y-2 border-t pt-3">
                <h4 className="text-sm font-semibold">{t("projects.annualBudgetReviewHistory")}</h4>
                {selected.reviewHistory.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.annualBudgetNoReviewHistory")}</p> : <ol className="space-y-2">{selected.reviewHistory.map((event, index) => <li key={`${event.timestampUtc}-${index}`} className="flex flex-wrap items-start justify-between gap-2 rounded border px-3 py-2 text-xs">
                  <div><p className="font-medium">{event.event === "RevisionSubmitted" ? t("projects.annualBudgetRevisionSubmitted") : event.event === "Approved" ? t("projects.annualBudgetApproved") : event.event === "Returned" ? t("projects.annualBudgetReturned") : t("projects.annualBudgetSubmitted")}</p><p className="text-muted-foreground">{event.actorName ?? "—"}{event.reason ? ` · ${event.reason}` : ""}</p></div><time className="text-muted-foreground">{formatDate(event.timestampUtc)}</time>
                </li>)}</ol>}
              </div>
            </div>
              : <div className="rounded-md border border-dashed p-6 text-center">
                <p className="text-sm text-muted-foreground">{items.length === 0 ? t("projects.annualBudgetFirstUse") : t("projects.annualBudgetNoRecord")}</p>
                {canCreate ? <Button size="sm" className="mt-3" disabled={!projectCurrency} onClick={() => openCreate(selectedYear)}><Plus className="me-1.5 h-4 w-4" />{t("projects.annualBudgetCreate")}</Button> : null}
              </div>}

        <div className="space-y-2 border-t pt-4">
          <h3 className="text-sm font-semibold">{t("projects.annualBudgetHistory")}</h3>
          <div className="overflow-x-auto">
            <Table>
              <TableHeader><TableRow><TableHead>{t("projects.annualBudgetFiscalYear")}</TableHead><TableHead>{t("projects.annualBudgetReference")}</TableHead><TableHead>{t("projects.annualBudgetPeriod")}</TableHead><TableHead>{t("projects.annualBudgetTotal")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
              <TableBody>
                {items.length === 0 ? <TableRow><TableCell colSpan={5} className="p-5 text-center text-sm text-muted-foreground">{t("projects.annualBudgetHistoryEmpty")}</TableCell></TableRow>
                  : items.map(item => <TableRow key={item.id}>
                    <TableCell className="font-medium"><Button size="sm" variant="ghost" className="h-auto p-0" onClick={() => { setSelectedYear(item.fiscalYear); onFiscalYearChange?.(item.fiscalYear); }}>{item.fiscalYearLabel}</Button></TableCell><TableCell>{item.financialReference}</TableCell><TableCell className="whitespace-nowrap">{formatDate(item.fiscalYearStartDate)} - {formatDate(item.fiscalYearEndDate)}</TableCell><TableCell className="tabular-nums">{money(item.totalAnnualBudget, item.currency)}</TableCell><TableCell><div className="flex flex-wrap gap-1"><Badge status={item.status}>{t(item.status === "Approved" ? "projects.annualBudgetApproved" : "projects.annualBudgetDraft")}</Badge>{item.isClosed ? <Badge variant="secondary">{t("projects.yearEndClosingStatusClosed")}</Badge> : null}</div></TableCell>
                  </TableRow>)}
              </TableBody>
            </Table>
          </div>
        </div>
      </CardContent>
    </Card>

    <SideSheet open={reviewSetupOpen} onOpenChange={setReviewSetupOpen}
      title={t("projects.annualBudgetReviewSheetTitle")}
      description={t("projects.annualBudgetReviewSheetDescription")} size="xl">
      <SideSheetBody className="space-y-5">
        {selected ? <section className="space-y-2 rounded-md border p-3" aria-labelledby="annual-budget-review-summary">
          <h3 id="annual-budget-review-summary" className="text-sm font-semibold">{t("projects.annualBudgetSummary")}</h3>
          <dl className="grid gap-2 text-sm sm:grid-cols-3">
            <div><dt className="text-xs text-muted-foreground">{t("projects.annualBudgetProject")}</dt><dd>{approvalConfiguration.data?.projectName ?? "—"}</dd></div>
            <div><dt className="text-xs text-muted-foreground">{t("projects.annualBudgetFiscalYear")}</dt><dd>{selected.fiscalYearLabel}</dd></div>
            <div><dt className="text-xs text-muted-foreground">{t("projects.annualBudgetAmountCurrency")}</dt><dd>{money(revisionMode ? (Number(revisionFunding) || 0) + (Number(revisionAdjustments) || 0) : selected.totalAnnualBudget, selected.currency)}</dd></div>
          </dl>
        </section> : null}

        {revisionMode ? <section className="grid gap-3 rounded-md border p-3 sm:grid-cols-2" aria-label={t("projects.annualBudgetRevision")}>
          <div><Label htmlFor="annual-budget-revision-funding">{t("projects.annualBudgetFunding")}</Label><Input id="annual-budget-revision-funding" type="number" min="0" step="0.0001" value={revisionFunding} onChange={event => setRevisionFunding(event.target.value)} /></div>
          <div><Label htmlFor="annual-budget-revision-adjustments">{t("projects.annualBudgetAdjustments")}</Label><Input id="annual-budget-revision-adjustments" type="number" step="0.0001" value={revisionAdjustments} onChange={event => setRevisionAdjustments(event.target.value)} /></div>
          <div className="sm:col-span-2"><Label htmlFor="annual-budget-revision-reason">{t("projects.annualBudgetReason")}</Label><Textarea id="annual-budget-revision-reason" value={revisionReason} onChange={event => setRevisionReason(event.target.value)} rows={2} placeholder={t("projects.annualBudgetRevisionReason")} /></div>
        </section> : null}

        {approvalConfiguration.isLoading || approvalConfiguration.isFetching ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
          : approvalConfiguration.isError || !approvalConfiguration.data ? <div className="flex items-center justify-between gap-3"><p role="alert" className="text-sm text-destructive">{t("projects.annualBudgetApprovalConfigurationLoadFailed")}</p><Button type="button" size="sm" variant="outline" onClick={() => void approvalConfiguration.refetch()}>{t("common.retry")}</Button></div>
            : (() => {
              const configuration = approvalConfiguration.data;
              const currentMode = customizeForSubmission ? approvalMode : configuration.approvalMode;
              const currentApproverIds = customizeForSubmission ? selectedReviewers : configuration.configuredApprovers
                .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER)).map(item => item.userId);
              const eligible = new Map(configuration.eligibleApprovers.map(item => [item.userId, item]));
              return <>
                <section className="space-y-3 rounded-md border p-3" aria-labelledby="annual-budget-approval-configuration">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div><h3 id="annual-budget-approval-configuration" className="text-sm font-semibold">{t("projects.annualBudgetApprovalConfiguration")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetConfigurationSource")}: {t("projects.annualBudgetProjectSettingsSource")}</p></div>
                    {!customizeForSubmission && configuration.canOverride ? <Button type="button" size="sm" variant="outline" onClick={() => {
                      setCustomizeForSubmission(true);
                      setApprovalMode(configuration.allowedModes.includes(configuration.approvalMode as "Sequential" | "Parallel") ? configuration.approvalMode : null);
                      const eligibleIds = new Set(configuration.eligibleApprovers.map(item => item.userId));
                      setSelectedReviewers(configuration.configuredApprovers
                        .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
                        .map(item => item.userId).filter(id => eligibleIds.has(id) && id !== user?.id));
                    }}>{t("projects.annualBudgetCustomizeThisSubmission")}</Button> : null}
                  </div>
                  {!configuration.isValid ? <div role="status" className="rounded border border-destructive/30 bg-destructive/5 p-2 text-sm text-destructive"><p className="font-medium">{t("projects.annualBudgetDefaultsCannotSubmit")}</p>{configuration.validationIssues.map(issue => <p key={issue} className="mt-1 text-xs">{t(`projects.annualBudgetApprovalIssue.${issue}`)}</p>)}</div> : null}
                  {configuration.configuredApprovers.length ? <ol className={`list-inside space-y-1 text-sm ${configuration.approvalMode === "Parallel" ? "list-disc" : "list-decimal"}`}>{configuration.configuredApprovers.map(approver => <li key={approver.userId} className={!approver.isEligible ? "text-destructive" : undefined}>{approver.displayName ?? approver.userId}{!approver.isEligible ? ` · ${t("projects.annualBudgetApproverUnavailable")}` : ""}</li>)}</ol> : <p className="text-sm text-muted-foreground">{t("projects.annualBudgetDefaultsMissing")}</p>}
                  {!customizeForSubmission ? <p className="text-sm"><span className="font-medium">{t("projects.annualBudgetApprovalMethod")}:</span> {currentMode === "Sequential" ? t("projects.financeApprovalSequential") : currentMode === "Parallel" ? t("projects.financeApprovalParallel") : t("projects.annualBudgetApprovalMethodInvalid")}</p> : null}
                  {customizeForSubmission ? <>
                    <div><Label htmlFor="annual-budget-review-approval-mode">{t("projects.annualBudgetApprovalMethod")}</Label><select id="annual-budget-review-approval-mode" className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm" value={approvalMode ?? ""} onChange={event => setApprovalMode((event.target.value || null) as "Sequential" | "Parallel" | null)}><option value="">{t("common.select")}</option>{configuration.allowedModes.map(mode => <option key={mode} value={mode}>{t(mode === "Sequential" ? "projects.financeApprovalSequential" : "projects.financeApprovalParallel")}</option>)}</select></div>
                    <div><p className="mb-2 text-xs text-muted-foreground">{t("projects.annualBudgetDefaultsAllMandatory")}</p><ul className="grid gap-2 sm:grid-cols-2">{configuration.eligibleApprovers.filter(candidate => candidate.userId !== user?.id).map(candidate => {
                      const index = selectedReviewers.indexOf(candidate.userId);
                      return <li key={candidate.userId} className="flex items-center justify-between gap-2 rounded border p-2"><label className="flex min-w-0 items-center gap-2 text-sm"><input type="checkbox" checked={index >= 0} onChange={event => updateReviewer(candidate.userId, event.target.checked)} /><span className="truncate">{candidate.displayName}</span></label>{approvalMode === "Sequential" && index >= 0 ? <div className="flex items-center gap-1"><span className="min-w-5 text-center text-xs text-muted-foreground">{index + 1}</span><Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverUp", { name: candidate.displayName })} disabled={index === 0} onClick={() => moveReviewer(candidate.userId, -1)}><ArrowUp className="size-4" /></Button><Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverDown", { name: candidate.displayName })} disabled={index === selectedReviewers.length - 1} onClick={() => moveReviewer(candidate.userId, 1)}><ArrowDown className="size-4" /></Button></div> : null}</li>;
                    })}</ul></div>
                    <div><Label htmlFor="annual-budget-override-reason">{t("projects.annualBudgetOverrideReason")}</Label><Textarea id="annual-budget-override-reason" value={overrideReason} onChange={event => setOverrideReason(event.target.value)} rows={3} maxLength={1000} required /></div>
                    <Button type="button" size="sm" variant="outline" onClick={() => { setCustomizeForSubmission(false); setOverrideReason(""); setApprovalMode(configuration.approvalMode); setSelectedReviewers(configuration.configuredApprovers.slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER)).map(item => item.userId)); }}>{t("projects.annualBudgetRestoreProjectDefaults")}</Button>
                  </> : null}
                  {customizeForSubmission ? <p className="text-xs text-muted-foreground">{t("projects.annualBudgetOverrideOneSubmissionOnly")}</p> : null}
                </section>
                <section className="space-y-2 rounded-md border p-3" aria-labelledby="annual-budget-approval-preview">
                  <h3 id="annual-budget-approval-preview" className="text-sm font-semibold">{t("projects.annualBudgetApprovalPreview")}</h3>
                  <p className="text-xs text-muted-foreground">{currentMode === "Parallel" ? t("projects.annualBudgetParallelCompletionRule") : currentMode === "Sequential" ? t("projects.annualBudgetSequentialCompletionRule") : t("projects.annualBudgetApprovalPreviewUnavailable")}</p>
                  {currentApproverIds.length ? <ol className={currentMode === "Parallel" ? "list-inside list-disc space-y-1 text-sm" : "list-inside list-decimal space-y-1 text-sm"}>{currentApproverIds.map(id => <li key={id}>{eligible.get(id)?.displayName ?? configuration.configuredApprovers.find(item => item.userId === id)?.displayName ?? id}</li>)}</ol> : null}
                </section>
                {reviewError ? <p role="alert" className="text-sm text-destructive">{reviewError}</p> : null}
              </>;
            })()}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" onClick={() => setReviewSetupOpen(false)}>{t("common.cancel")}</Button>
        <Button type="button" onClick={() => selected && void submitBudgetForReview(selected)} disabled={busy || approvalConfiguration.isLoading || approvalConfiguration.isFetching || !canSubmitReview}>{busy ? t("common.saving") : t("projects.annualBudgetSubmitForReview")}</Button>
      </SideSheetFooter>
    </SideSheet>

    <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={editing ? t("projects.annualBudgetEdit") : t("projects.annualBudgetCreate")} description={t("projects.annualBudgetFormDescription")}>
      <form onSubmit={event => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <div><Label htmlFor="annual-budget-fiscal-year">{t("projects.annualBudgetFiscalYear")}</Label><Input id="annual-budget-fiscal-year" type="number" min="1900" max="2200" step="1" value={form.fiscalYear} disabled={Boolean(editing)} onChange={event => setForm(current => ({ ...current, fiscalYear: event.target.value }))} required /></div>
          {page ? <p className="text-xs text-muted-foreground">{t("projects.annualBudgetCalendarHint", { month: page.fiscalYearStartMonth })}</p> : null}
          <div><Label htmlFor="annual-budget-funding">{t("projects.annualBudgetFunding")} *</Label><Input id="annual-budget-funding" type="number" min="0" step="0.0001" value={form.funding} onChange={event => setForm(current => ({ ...current, funding: event.target.value }))} required /></div>
          <div><Label htmlFor="annual-budget-adjustments">{t("projects.annualBudgetAdjustments")} *</Label><Input id="annual-budget-adjustments" type="number" step="0.0001" value={form.adjustments} onChange={event => setForm(current => ({ ...current, adjustments: event.target.value }))} required /></div>
          <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{t("projects.annualBudgetTotal")}</p><p className="mt-1 font-semibold tabular-nums">{money((Number(form.funding) || 0) + (Number(form.adjustments) || 0), selected?.currency ?? projectCurrency ?? undefined)}</p></div>
          {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={busy}>{busy ? t("common.saving") : t("projects.annualBudgetSaveDraft")}</Button></SideSheetFooter>
      </form>
    </SideSheet>
  </section>;
}

function Metric({ label, value, hint, strong = false }: { label: string; value: string; hint?: string; strong?: boolean }) {
  return <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{label}</p><p className={`mt-1 tabular-nums ${strong ? "text-lg font-semibold" : "font-medium"}`}>{value}</p>{hint ? <p className="mt-1 text-xs text-muted-foreground">{hint}</p> : null}</div>;
}
