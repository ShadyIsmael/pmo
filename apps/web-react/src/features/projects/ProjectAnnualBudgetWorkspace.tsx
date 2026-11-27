import * as React from "react";
import { ApiError } from "@api/client";
import { ArrowDown, ArrowUp, Check, Pencil, Plus, Send, Trash2, Undo2 } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { Textarea } from "@components/ui/Textarea";
import { useProjectAnnualBudgetClosings, useProjectPaymentRequests, type PaymentRequestApproverSelection } from "./api/project-financials";
import { ProjectAnnualBudgetLines } from "./ProjectAnnualBudgetLines";
import {
  useCreateProjectAnnualBudget,
  useDeleteProjectAnnualBudgetDraft,
  useDecideProjectAnnualBudgetReview,
  useProjectAnnualBudgetApprovalConfiguration,
  useProjectAnnualBudgets,
  useProjectAnnualBudgetDraftApprovalConfiguration,
  useReviseApprovedProjectAnnualBudget,
  useSaveProjectAnnualBudgetDraftApprovalConfiguration,
  useSubmitProjectAnnualBudgetReview,
  useUpdateProjectAnnualBudget,
  useWithdrawProjectAnnualBudgetSubmission,
  type ProjectAnnualBudget,
} from "./api/project-annual-budgets";

type BudgetForm = { fiscalYear: string; funding: string; adjustments: string };
type BudgetLifecycleAction = { kind: "delete" | "withdraw"; item: ProjectAnnualBudget };
const emptyForm = (fiscalYear = "", funding = ""): BudgetForm => ({ fiscalYear, funding, adjustments: "0" });
const annualBudgetDeleteBlockReasons = [
  "permission-required", "view-required", "scope-read-only", "pending-review", "approval-step-active", "not-draft", "closing-metadata",
  "closing-reference", "payment-request-reference", "funding-transfer-reference", "financial-dependency",
] as const;
const annualBudgetDeleteBlockMessageKey = (reason: string) =>
  `projects.annualBudgetDeleteBlocked.${annualBudgetDeleteBlockReasons.includes(reason as (typeof annualBudgetDeleteBlockReasons)[number]) ? reason : "unknown"}`;
const annualBudgetCandidateBlockMessageKey = (reason: string) => reason === "submitter_separation_of_duties"
  ? "projects.annualBudgetSelfApprovalBlocked"
  : reason === "approval_permission_not_effective"
    ? "projects.annualBudgetCandidateApprovalPermissionRequired"
    : reason === "system_or_management_role_required"
      ? "projects.annualBudgetCandidateRoleCategoryRequired"
      : "projects.annualBudgetCandidateUnavailable";
export function ProjectAnnualBudgetWorkspace({
  projectId,
  canCreate,
  canEdit,
  canConfigureDraftApproval = false,
  canDelete = false,
  canWithdraw = false,
  canSubmit,
  canRevise,
  canApprove,
  canViewPaymentRequests,
  canViewAnnualBudgetClosing,
  canPrepareClosing = false,
  onOpenClosing,
  projectCurrency,
  enabled = true,
  selectedFiscalYear,
  onFiscalYearChange,
}: {
  projectId: string;
  canCreate: boolean;
  canEdit: boolean;
  canConfigureDraftApproval?: boolean;
  canDelete?: boolean;
  canWithdraw?: boolean;
  canSubmit: boolean;
  canRevise: boolean;
  canApprove: boolean;
  canViewPaymentRequests: boolean;
  canViewAnnualBudgetClosing: boolean;
  canPrepareClosing?: boolean;
  onOpenClosing?: () => void;
  projectCurrency: string | null;
  enabled?: boolean;
  selectedFiscalYear?: number | null;
  onFiscalYearChange?: (year: number) => void;
}) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { user } = useAuth();
  const query = useProjectAnnualBudgets(projectId, enabled);
  const [selectedYear, setSelectedYear] = React.useState<number | null>(null);
  const selectedForDraftConfiguration = query.data?.items.find(item => item.fiscalYear === selectedYear) ?? null;
  const [draftApprovalOpen, setDraftApprovalOpen] = React.useState(false);
  const [reviewSetupOpen, setReviewSetupOpen] = React.useState(false);
  const draftApprovalConfiguration = useProjectAnnualBudgetDraftApprovalConfiguration(
    projectId,
    selectedForDraftConfiguration?.id ?? "",
    enabled && (draftApprovalOpen || reviewSetupOpen) && selectedForDraftConfiguration?.status === "Draft",
  );
  const saveDraftApprovalConfiguration = useSaveProjectAnnualBudgetDraftApprovalConfiguration(
    projectId,
    selectedForDraftConfiguration?.id ?? "",
  );
  const paymentRequests = useProjectPaymentRequests(projectId, enabled && canViewPaymentRequests);
  const closings = useProjectAnnualBudgetClosings(projectId, enabled && canViewAnnualBudgetClosing);
  const create = useCreateProjectAnnualBudget(projectId);
  const update = useUpdateProjectAnnualBudget(projectId);
  const deleteDraft = useDeleteProjectAnnualBudgetDraft(projectId);
  const withdrawSubmission = useWithdrawProjectAnnualBudgetSubmission(projectId);
  const submitReview = useSubmitProjectAnnualBudgetReview(projectId);
  const revise = useReviseApprovedProjectAnnualBudget(projectId);
  const decideReview = useDecideProjectAnnualBudgetReview(projectId);
  const approvalConfiguration = useProjectAnnualBudgetApprovalConfiguration(projectId, enabled);
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
  const [sheetOpen, setSheetOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<ProjectAnnualBudget | null>(null);
  const [form, setForm] = React.useState<BudgetForm>(emptyForm());
  const [formError, setFormError] = React.useState<string | null>(null);
  const [budgetAction, setBudgetAction] = React.useState<BudgetLifecycleAction | null>(null);
  const [draftApprovalMode, setDraftApprovalMode] = React.useState<"Sequential" | "Parallel" | null>(null);
  const [draftApprovalApprovers, setDraftApprovalApprovers] = React.useState<string[]>([]);
  const [draftApprovalOverrideReason, setDraftApprovalOverrideReason] = React.useState("");
  const [draftApprovalError, setDraftApprovalError] = React.useState<string | null>(null);
  const [draftApprovalInitializedFor, setDraftApprovalInitializedFor] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!enabled) setSheetOpen(false);
  }, [enabled]);
  React.useEffect(() => {
    if (!draftApprovalOpen || !selectedForDraftConfiguration || !draftApprovalConfiguration.data
      || draftApprovalInitializedFor === selectedForDraftConfiguration.id) return;
    const configuration = draftApprovalConfiguration.data;
    setDraftApprovalMode(configuration.approvalMode);
    setDraftApprovalApprovers(configuration.configuredApprovers
      .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId));
    setDraftApprovalOverrideReason(configuration.overrideReason ?? "");
    setDraftApprovalError(null);
    setDraftApprovalInitializedFor(selectedForDraftConfiguration.id);
  }, [draftApprovalOpen, selectedForDraftConfiguration, draftApprovalConfiguration.data, draftApprovalInitializedFor]);
  const [actionError, setActionError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (selectedFiscalYear !== undefined && selectedFiscalYear !== null && selectedFiscalYear !== selectedYear) {
      setSelectedYear(selectedFiscalYear);
      return;
    }
    if (selectedYear === null && query.data && selectedFiscalYear == null) setSelectedYear(query.data.currentFiscalYear);
  }, [query.data?.currentFiscalYear, selectedFiscalYear, selectedYear]);

  const page = query.data;
  const items = page?.items ?? [];
  const authoritativeProjectCurrency = page?.projectCurrency?.trim().toUpperCase()
    || projectCurrency?.trim().toUpperCase()
    || null;
  const selected = items.find(item => item.fiscalYear === selectedYear) ?? null;
  const savedDraftApprovalOverride = selected && selectedForDraftConfiguration?.id === selected.id
    && draftApprovalConfiguration.data?.configurationSource === "DraftOverride"
      ? draftApprovalConfiguration.data
      : null;
  const latestFiscalYear = items.length > 0
    ? Math.max(...items.map(item => item.fiscalYear))
    : page?.currentFiscalYear ?? null;
  const suggestedFiscalYear = page?.suggestedFiscalYear ?? page?.currentFiscalYear ?? null;
  const missingClosingFiscalYear = page?.missingClosingFiscalYear ?? null;
  const nextBudgetFiscalYear = items.length === 0
    ? suggestedFiscalYear
    : latestFiscalYear === null ? suggestedFiscalYear : latestFiscalYear + 1;
  const nextFiscalYearNeedsPriorClosing = missingClosingFiscalYear !== null
    && nextBudgetFiscalYear === missingClosingFiscalYear + 1;
  const suggestedNewFunding = page?.suggestedNewFundingSource === "ProjectLifetimeCeiling"
    ? page.suggestedNewFunding
    : null;
  const isSuggestedInitialAllocation = !editing
    && items.length === 0
    && Number(form.fiscalYear) === page?.suggestedFiscalYear
    && page?.suggestedNewFundingSource === "ProjectLifetimeCeiling"
    && suggestedNewFunding !== null;
  const selectedYearNeedsPriorClosing = missingClosingFiscalYear !== null
    && selectedYear === missingClosingFiscalYear + 1
    && !selected;
  const currencyMatches = Boolean(authoritativeProjectCurrency)
    && selected?.currency === authoritativeProjectCurrency;
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
  const busy = create.isPending || update.isPending || submitReview.isPending || revise.isPending || decideReview.isPending
    || deleteDraft.isPending || withdrawSubmission.isPending || saveDraftApprovalConfiguration.isPending;
  const lifecycleActionBusy = deleteDraft.isPending || withdrawSubmission.isPending;

  const openCreate = (fiscalYear: number | null = selectedYear) => {
    setEditing(null);
    const chosenFiscalYear = fiscalYear ?? suggestedFiscalYear;
    const suggestedFunding = chosenFiscalYear === page?.suggestedFiscalYear
      && page?.suggestedNewFundingSource === "ProjectLifetimeCeiling"
      ? page.suggestedNewFunding?.toString() ?? ""
      : "";
    setForm(emptyForm(chosenFiscalYear === null ? "" : chosenFiscalYear.toString(), suggestedFunding));
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
    if (!editing && page?.totalProjectFinancialCeiling === null) {
      setFormError(t("projects.annualBudgetCeilingRequired"));
      return;
    }
    if (!editing && (!Number.isInteger(fiscalYear) || fiscalYear < 1900 || fiscalYear > 2200)) {
      setFormError(t("projects.annualBudgetYearInvalid"));
      return;
    }
    if (!editing && missingClosingFiscalYear !== null && fiscalYear === missingClosingFiscalYear + 1) {
      setFormError(t("projects.annualBudgetPriorClosingRequired", { fiscalYear: missingClosingFiscalYear }));
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
    const defaultMode = savedDraftApprovalOverride?.approvalMode ?? configuration?.approvalMode ?? null;
    const defaultApprovers = savedDraftApprovalOverride?.configuredApprovers ?? configuration?.configuredApprovers ?? [];
    setApprovalMode(defaultMode);
    setSelectedReviewers(defaultApprovers
      .slice()
      .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId));
    setReviewSetupOpen(true);
    void approvalConfiguration.refetch();
    if (selectedForDraftConfiguration?.status === "Draft") void draftApprovalConfiguration.refetch();
  };

  React.useEffect(() => {
    if (!reviewSetupOpen || customizeForSubmission || !approvalConfiguration.data
      || (selectedForDraftConfiguration?.status === "Draft" && (draftApprovalConfiguration.isLoading || draftApprovalConfiguration.isFetching))) return;
    const sourcePlan = savedDraftApprovalOverride ?? approvalConfiguration.data;
    setApprovalMode(sourcePlan.approvalMode);
    setSelectedReviewers(sourcePlan.configuredApprovers
      .slice()
      .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId));
  }, [reviewSetupOpen, customizeForSubmission, approvalConfiguration.data, savedDraftApprovalOverride, selectedForDraftConfiguration?.status, draftApprovalConfiguration.isLoading, draftApprovalConfiguration.isFetching]);

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
    const hasDraftApprovalPlan = item.status === "Draft" && selectedForDraftConfiguration?.id === item.id;
    if (hasDraftApprovalPlan && (draftApprovalConfiguration.isLoading || draftApprovalConfiguration.isFetching || draftApprovalConfiguration.isError)) {
      setReviewError(t("projects.annualBudgetApprovalConfigurationLoadFailed"));
      return;
    }
    const draftOverride = hasDraftApprovalPlan && draftApprovalConfiguration.data?.configurationSource === "DraftOverride"
      ? draftApprovalConfiguration.data
      : null;
    const selectedMode = customizeForSubmission ? approvalMode : draftOverride?.approvalMode ?? configuration.approvalMode;
    const reviewers = customizeForSubmission
      ? selectedReviewers
      : (draftOverride?.configuredApprovers ?? configuration.configuredApprovers)
        .slice()
        .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
        .map(approver => approver.userId);
    if (customizeForSubmission && !configuration.canOverride) {
      setReviewError(t("projects.annualBudgetOverrideNotAuthorized"));
      return;
    }
    if (!customizeForSubmission && (draftOverride ? !draftOverride.isValid : !configuration.isValid || !configuration.canSubmitWithDefaults)) {
      setReviewError(t(draftOverride ? "projects.annualBudgetDraftApprovalCannotSubmit" : "projects.annualBudgetDefaultsCannotSubmit"));
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
        if (draftOverride && !customizeForSubmission) {
          await submitReview.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
        } else {
          await submitReview.mutateAsync({
            id: item.id,
            approvalMode: selectedMode,
            approvers: reviewerSelections(selectedMode!, reviewers),
            rowVersion: item.rowVersion,
            customizeForThisSubmission: customizeForSubmission,
            overrideReason: customizeForSubmission ? overrideReason.trim() : null,
            approvalConfigurationRowVersion: configuration.rowVersion,
            projectSettingsRowVersion: configuration.projectSettingsRowVersion,
          });
        }
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

  const performBudgetLifecycleAction = async () => {
    if (!budgetAction) return;
    setActionError(null);
    try {
      if (budgetAction.kind === "delete") {
        await deleteDraft.mutateAsync({ id: budgetAction.item.id, rowVersion: budgetAction.item.rowVersion });
        const refreshed = await query.refetch();
        const nextYear = refreshed.data?.items.length
          ? Math.max(...refreshed.data.items.map(item => item.fiscalYear))
          : refreshed.data?.currentFiscalYear ?? null;
        if (nextYear !== null) {
          setSelectedYear(nextYear);
          onFiscalYearChange?.(nextYear);
        }
      } else {
        if (!budgetAction.item.reviewRowVersion) {
          setActionError(t("projects.annualBudgetWithdrawStale"));
          return;
        }
        await withdrawSubmission.mutateAsync({
          id: budgetAction.item.id,
          rowVersion: budgetAction.item.rowVersion,
          approvalRowVersion: budgetAction.item.reviewRowVersion,
        });
      }
      setBudgetAction(null);
      setActionError(null);
    } catch (error) {
      const isDelete = budgetAction.kind === "delete";
      setActionError(error instanceof ApiError && error.status === 403
        ? t(isDelete ? "projects.annualBudgetDeleteNotAuthorized" : "projects.annualBudgetWithdrawNotAuthorized")
        : error instanceof ApiError && error.status === 409
          ? error.message
          : error instanceof ApiError && error.status === 404
          ? t(isDelete ? "projects.annualBudgetDeleteConflict" : "projects.annualBudgetWithdrawStale")
          : t(isDelete ? "projects.annualBudgetDeleteFailed" : "projects.annualBudgetWithdrawFailed"));
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

  const openDraftApprovalEditor = () => {
    if (!selectedForDraftConfiguration || !canConfigureDraftApproval || !canEdit
      || selectedForDraftConfiguration.status !== "Draft"
      || !["Draft", "Returned", "Rejected"].includes(selectedForDraftConfiguration.reviewStatus)) return;
    setDraftApprovalInitializedFor(null);
    setDraftApprovalError(null);
    setDraftApprovalOpen(true);
    void draftApprovalConfiguration.refetch();
  };

  const saveDraftApprovalPlan = async () => {
    const configuration = draftApprovalConfiguration.data;
    const item = selectedForDraftConfiguration;
    if (!item || !configuration || !configuration.canSave || !canConfigureDraftApproval || !canEdit) return;
    if (!draftApprovalMode || !configuration.allowedModes.includes(draftApprovalMode)
      || draftApprovalApprovers.length === 0
      || new Set(draftApprovalApprovers).size !== draftApprovalApprovers.length) {
      setDraftApprovalError(t("projects.annualBudgetReviewerRequired"));
      return;
    }
    const eligibleCandidates = new Map(configuration.eligibleApprovers.map(candidate => [candidate.userId, candidate]));
    const unavailableSelection = draftApprovalApprovers.some(id => {
      const candidate = eligibleCandidates.get(id);
      return !candidate || Boolean(candidate.selectionBlockReason);
    });
    if (unavailableSelection) {
      setDraftApprovalError(t("projects.annualBudgetApproverUnavailable"));
      return;
    }
    if (!draftApprovalOverrideReason.trim()) {
      setDraftApprovalError(t("projects.annualBudgetDraftApprovalReasonRequired"));
      return;
    }
    setDraftApprovalError(null);
    try {
      const saved = await saveDraftApprovalConfiguration.mutateAsync({
        approvalMode: draftApprovalMode,
        approverUserIds: draftApprovalApprovers,
        budgetRowVersion: configuration.budgetRowVersion,
        projectSettingsRowVersion: configuration.projectSettingsRowVersion,
        projectSettingsConfigurationRowVersion: configuration.projectSettingsConfigurationRowVersion,
        overrideReason: draftApprovalOverrideReason.trim(),
      });
      setDraftApprovalMode(saved.approvalMode);
      setDraftApprovalApprovers(saved.configuredApprovers
        .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
        .map(item => item.userId));
      setDraftApprovalOverrideReason(saved.overrideReason ?? draftApprovalOverrideReason.trim());
      setDraftApprovalInitializedFor(item.id);
      setDraftApprovalOpen(false);
      setDraftApprovalError(null);
    } catch (error) {
      setDraftApprovalError(error instanceof ApiError && error.status === 403
        ? t("projects.annualBudgetDraftApprovalNotAuthorized")
        : error instanceof ApiError && (error.status === 409 || error.status === 404)
          ? t("projects.annualBudgetApprovalConfigurationStale")
          : t("projects.annualBudgetDraftApprovalSaveFailed"));
    }
  };

  const updateDraftApprover = (userId: string, checked: boolean) => setDraftApprovalApprovers(current => checked
    ? current.includes(userId) ? current : [...current, userId]
    : current.filter(id => id !== userId));

  const moveDraftApprover = (userId: string, delta: -1 | 1) => setDraftApprovalApprovers(current => {
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
  const activeApprovalMode = customizeForSubmission ? approvalMode : savedDraftApprovalOverride?.approvalMode ?? activeConfiguration?.approvalMode ?? null;
  const activeApproverIds = customizeForSubmission
    ? selectedReviewers
    : (savedDraftApprovalOverride?.configuredApprovers ?? activeConfiguration?.configuredApprovers ?? [])
      .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId) ?? [];
  const activeEligibleApproverIds = new Set((savedDraftApprovalOverride?.eligibleApprovers ?? activeConfiguration?.eligibleApprovers ?? []).map(item => item.userId));
  const activePlanIsValid = savedDraftApprovalOverride ? savedDraftApprovalOverride.isValid : activeConfiguration?.isValid === true && activeConfiguration.canSubmitWithDefaults;
  const isApprovedRevisionWithdrawal = budgetAction?.kind === "withdraw" && budgetAction.item.status === "Approved";
  const withdrawDialogTitleKey = isApprovedRevisionWithdrawal
    ? "projects.annualBudgetWithdrawRevisionDialogTitle"
    : "projects.annualBudgetWithdrawDialogTitle";
  const withdrawDialogDescriptionKey = isApprovedRevisionWithdrawal
    ? "projects.annualBudgetWithdrawRevisionDialogDescription"
    : "projects.annualBudgetWithdrawDialogDescription";
  const canSubmitReview = Boolean((activeConfiguration || savedDraftApprovalOverride) && activeApprovalMode && activeApproverIds.length > 0
    && new Set(activeApproverIds).size === activeApproverIds.length
    && activeApproverIds.every(id => activeEligibleApproverIds.has(id) && id !== user?.id)
    && (customizeForSubmission
      ? activeConfiguration?.canOverride === true && Boolean(overrideReason.trim())
      : activePlanIsValid));
  const canDeleteSelectedDraft = Boolean(selected && canDelete && selected.canDeleteDraft === true
    && selected.deleteDraftBlockReason == null && selected.status === "Draft"
    && ["Draft", "Returned", "Rejected"].includes(selected.reviewStatus) && !selected.isClosed);
  const deleteDraftBlockReason = selected?.deleteDraftBlockReason
    ?? (canDeleteSelectedDraft ? null : "unknown");
  const currentDraftApprovalConfiguration = draftApprovalConfiguration.data;
  const canSaveDraftApproval = Boolean(currentDraftApprovalConfiguration?.canSave && canConfigureDraftApproval && canEdit
    && draftApprovalMode && currentDraftApprovalConfiguration.allowedModes.includes(draftApprovalMode)
    && draftApprovalApprovers.length > 0 && new Set(draftApprovalApprovers).size === draftApprovalApprovers.length
    && draftApprovalApprovers.every(id => {
      const candidate = currentDraftApprovalConfiguration.eligibleApprovers.find(item => item.userId === id);
      return Boolean(candidate && !candidate.selectionBlockReason);
    }) && draftApprovalOverrideReason.trim());

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
          {canCreate ? <Button size="sm" variant="outline" disabled={!authoritativeProjectCurrency || query.isLoading || !page || page.totalProjectFinancialCeiling === null || nextFiscalYearNeedsPriorClosing} onClick={() => openCreate(nextBudgetFiscalYear)}>
            <Plus className="me-1.5 h-4 w-4" />{selected ? t("projects.annualBudgetCreateAnother") : t("projects.annualBudgetCreate")}
          </Button> : null}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {nextFiscalYearNeedsPriorClosing ? <div className="rounded-md border px-3 py-3 text-start" role="status">
          <p className="text-sm font-medium">{t("projects.annualBudgetPriorClosingRequired", { fiscalYear: missingClosingFiscalYear })}</p>
          <p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetOpeningUsesApprovedClosing")}</p>
          {canPrepareClosing && canViewAnnualBudgetClosing && onOpenClosing ? <Button type="button" size="sm" variant="outline" className="mt-3" onClick={() => { onFiscalYearChange?.(missingClosingFiscalYear); onOpenClosing(); }}>{t("projects.annualBudgetOpenYearEndClosing")}</Button> : null}
        </div> : null}
        {(canCreate || canEdit) && !authoritativeProjectCurrency ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.financialCurrencyUnavailable")}</p> : null}
        {page?.totalProjectFinancialCeiling === null && (canCreate || canEdit || canSubmit) ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.annualBudgetCeilingRequired")}</p> : null}
        {query.isLoading ? <p role="status" className="py-4 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : query.isError ? <div className="flex items-center justify-between gap-3 rounded-md border p-4"><p role="alert" className="text-sm text-destructive">{query.error instanceof ApiError && query.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.annualBudgetHistoryLoadFailed")}</p><Button size="sm" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div>
            : selected ? <div className="space-y-4">
              {actionError && !budgetAction ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{actionError}</p> : null}
              {!currencyMatches ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{authoritativeProjectCurrency ? t("projects.annualBudgetCurrencyMismatch") : t("projects.financialCurrencyUnavailable")}</p> : null}
              <div className="flex flex-wrap items-start justify-between gap-3 rounded-md border p-4">
                <div>
                  <div className="flex flex-wrap items-center gap-2"><h3 className="font-semibold">{selected.fiscalYearLabel}</h3><Badge status={selected.status}>{t(`projects.annualBudget${selected.reviewStatus}`)}</Badge></div>
                  <p className="mt-1 text-xs text-muted-foreground">{selected.financialReference} · {formatDate(selected.fiscalYearStartDate)} – {formatDate(selected.fiscalYearEndDate)}</p>
                </div>
                <div className="flex gap-2">
                  {canEdit && selected.canEdit && currencyMatches ? <Button size="sm" variant="outline" onClick={() => openEdit(selected)}><Pencil className="me-1.5 h-4 w-4" />{t("projects.annualBudgetEdit")}</Button> : null}
                  {canConfigureDraftApproval && canEdit && selected.canEdit && selected.status === "Draft" && ["Draft", "Returned", "Rejected"].includes(selected.reviewStatus)
                    ? <Button type="button" size="sm" variant="outline" onClick={openDraftApprovalEditor} disabled={busy}>{t("projects.annualBudgetConfigureApproval")}</Button>
                    : null}
                  {canSubmit && selected.canSubmit && currencyMatches && page?.totalProjectFinancialCeiling !== null ? <Button size="sm" onClick={() => openReviewSetup(false)}><Send className="me-1.5 h-4 w-4" />{t("projects.annualBudgetSubmitForReview")}</Button> : null}
                  {canRevise && selected.canRevise && currencyMatches ? <Button size="sm" variant="outline" onClick={() => openReviewSetup(true)}><Pencil className="me-1.5 h-4 w-4" />{t("projects.annualBudgetRevision")}</Button> : null}
                  <div className="space-y-1">
                    <Button size="sm" variant="outline" className="border-destructive text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={() => { setActionError(null); setBudgetAction({ kind: "delete", item: selected }); }} disabled={busy || !canDeleteSelectedDraft}>
                      <Trash2 className="me-1.5 h-4 w-4" />{t("projects.annualBudgetDeleteDraft")}
                    </Button>
                    {deleteDraftBlockReason !== null ? <p role="status" className="max-w-56 text-xs text-muted-foreground">{t(annualBudgetDeleteBlockMessageKey(deleteDraftBlockReason))}</p> : null}
                  </div>
                  {canWithdraw && selected.canWithdrawSubmission === true && ["Draft", "Approved"].includes(selected.status) && ["Pending", "RevisionPending"].includes(selected.reviewStatus) && selected.reviewRequestId && selected.reviewRowVersion ? <Button size="sm" variant="outline" onClick={() => { setActionError(null); setBudgetAction({ kind: "withdraw", item: selected }); }} disabled={busy}><Undo2 className="me-1.5 h-4 w-4" />{t("projects.annualBudgetWithdrawSubmission")}</Button> : null}
                </div>
              </div>
              {selected.submittedAtUtc ? <p className="text-xs text-muted-foreground">{t("projects.annualBudgetSubmitted")} · {formatDate(selected.submittedAtUtc)}</p> : null}
              <ProjectAnnualBudgetLines projectId={projectId} budget={selected}
                canManage={Boolean(user && (selected.status === "Draft"
                  ? canEdit && selected.canEdit || hasPermission(user.permissions, Permissions.ProjectAnnualBudget.ManageLines)
                  : hasPermission(user.permissions, Permissions.ProjectAnnualBudget.ManageLines)))}
                enabled={enabled} />
              {canApprove && selected.canReview ? <div className="space-y-2 rounded-md border bg-muted/20 p-3">
                <Label htmlFor="annual-budget-return-reason">{t("projects.annualBudgetReason")}</Label>
                <Textarea id="annual-budget-return-reason" value={returnReason} onChange={event => setReturnReason(event.target.value)} rows={2} placeholder={t("projects.annualBudgetRevisionReason")} />
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" onClick={() => void onApprove(selected)} disabled={busy}><Check className="me-1.5 h-4 w-4" />{busy ? t("common.saving") : t("projects.annualBudgetApprove")}</Button>
                  <Button size="sm" variant="outline" onClick={() => void returnBudgetForChanges(selected)} disabled={busy}><Undo2 className="me-1.5 h-4 w-4" />{t("projects.annualBudgetReturnForChanges")}</Button>
                </div>
              </div> : null}
              {!canApprove && selected.reviewStatus === "Pending" ? <p role="status" className="rounded-md border px-3 py-2 text-xs text-muted-foreground">{t("projects.annualBudgetApprovalPermissionHint")}</p> : null}
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <Metric label={t("projects.annualBudgetNewFunding")} value={money(selected.newApprovedFunding, selected.currency)} />
                <Metric label={t("projects.annualBudgetCarryForward")} value={money(selected.approvedCarryForward, selected.currency)} hint={t("projects.annualBudgetOpeningCarryForwardHint")} />
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
                  <div><p className="font-medium">{event.event === "RevisionSubmitted" ? t("projects.annualBudgetRevisionSubmitted") : event.event === "Approved" ? t("projects.annualBudgetApproved") : event.event === "Returned" ? t("projects.annualBudgetReturned") : event.event === "Withdrawn" ? t("projects.annualBudgetWithdrawn") : t("projects.annualBudgetSubmitted")}</p><p className="text-muted-foreground">{event.actorName ?? "—"}{event.reason ? ` · ${event.reason}` : ""}</p></div><time className="text-muted-foreground">{formatDate(event.timestampUtc)}</time>
                </li>)}</ol>}
              </div>
            </div>
              : <div className="rounded-md border border-dashed p-6 text-center">
                <p className="text-sm text-muted-foreground">{items.length === 0 ? t("projects.annualBudgetFirstUse") : t("projects.annualBudgetNoRecord")}</p>
                {selectedYearNeedsPriorClosing && !nextFiscalYearNeedsPriorClosing ? <div className="mx-auto mt-3 max-w-2xl rounded-md border px-3 py-3 text-start" role="status">
                  <p className="text-sm font-medium">{t("projects.annualBudgetPriorClosingRequired", { fiscalYear: missingClosingFiscalYear })}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetOpeningUsesApprovedClosing")}</p>
                  {canPrepareClosing && canViewAnnualBudgetClosing && onOpenClosing ? <Button type="button" size="sm" variant="outline" className="mt-3" onClick={() => { onFiscalYearChange?.(missingClosingFiscalYear); onOpenClosing(); }}>{t("projects.annualBudgetOpenYearEndClosing")}</Button> : null}
                </div> : null}
                {canCreate && !selectedYearNeedsPriorClosing && !nextFiscalYearNeedsPriorClosing ? <Button size="sm" className="mt-3" disabled={!authoritativeProjectCurrency || !page || page.totalProjectFinancialCeiling === null} onClick={() => openCreate(nextBudgetFiscalYear)}><Plus className="me-1.5 h-4 w-4" />{t("projects.annualBudgetCreate")}</Button> : null}
              </div>}


      </CardContent>
    </Card>

    <Dialog
      open={budgetAction !== null}
      role="alertdialog"
      ariaLabel={budgetAction?.kind === "delete" ? t("projects.annualBudgetDeleteDialogTitle", { fiscalYear: budgetAction.item.fiscalYearLabel }) : t(withdrawDialogTitleKey, { fiscalYear: budgetAction?.item.fiscalYearLabel ?? "" })}
      onOpenChange={open => {
        if (!open && !lifecycleActionBusy) {
          setBudgetAction(null);
          setActionError(null);
        }
      }}
    >
      {budgetAction ? <DialogContent>
        <DialogHeader>
          <DialogTitle>{budgetAction.kind === "delete"
            ? t("projects.annualBudgetDeleteDialogTitle", { fiscalYear: budgetAction.item.fiscalYearLabel })
            : t(withdrawDialogTitleKey, { fiscalYear: budgetAction.item.fiscalYearLabel })}</DialogTitle>
          <DialogDescription>{budgetAction.kind === "delete"
            ? t("projects.annualBudgetDeleteDialogDescription", { fiscalYear: budgetAction.item.fiscalYearLabel })
            : t(withdrawDialogDescriptionKey, { fiscalYear: budgetAction.item.fiscalYearLabel })}</DialogDescription>
        </DialogHeader>
        {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{actionError}</p> : null}
        <DialogFooter className="gap-2 sm:space-x-0">
          <Button type="button" variant="outline" disabled={lifecycleActionBusy} onClick={() => { setBudgetAction(null); setActionError(null); }}>{t("common.cancel")}</Button>
          <Button type="button" variant="outline" className={budgetAction.kind === "delete" ? "border-destructive text-destructive hover:bg-destructive/10 hover:text-destructive" : undefined} disabled={lifecycleActionBusy} onClick={() => void performBudgetLifecycleAction()}>
            {lifecycleActionBusy ? t("common.saving") : budgetAction.kind === "delete" ? t("projects.annualBudgetDeleteDraft") : t("projects.annualBudgetWithdrawSubmission")}
          </Button>
        </DialogFooter>
      </DialogContent> : null}
    </Dialog>

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

        {approvalConfiguration.isLoading || approvalConfiguration.isFetching
          || (selected?.status === "Draft" && (draftApprovalConfiguration.isLoading || draftApprovalConfiguration.isFetching))
          ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
          : approvalConfiguration.isError || !approvalConfiguration.data
            || (selected?.status === "Draft" && draftApprovalConfiguration.isError)
            ? <div className="flex items-center justify-between gap-3"><p role="alert" className="text-sm text-destructive">{t("projects.annualBudgetApprovalConfigurationLoadFailed")}</p><Button type="button" size="sm" variant="outline" onClick={() => { void approvalConfiguration.refetch(); if (selected?.status === "Draft") void draftApprovalConfiguration.refetch(); }}>{t("common.retry")}</Button></div>
            : (() => {
              const configuration = approvalConfiguration.data;
              const plan = savedDraftApprovalOverride ?? configuration;
              const currentMode = customizeForSubmission ? approvalMode : plan.approvalMode;
              const currentApproverIds = customizeForSubmission ? selectedReviewers : plan.configuredApprovers
                .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER)).map(item => item.userId);
              const eligible = new Map(plan.eligibleApprovers.map(item => [item.userId, item]));
              return <>
                <section className="space-y-3 rounded-md border p-3" aria-labelledby="annual-budget-approval-configuration">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div><h3 id="annual-budget-approval-configuration" className="text-sm font-semibold">{t("projects.annualBudgetApprovalConfiguration")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetConfigurationSource")}: {t(savedDraftApprovalOverride ? "projects.annualBudgetDraftApprovalOverrideSource" : "projects.annualBudgetProjectSettingsSource")}</p></div>
                    {!customizeForSubmission && configuration.canOverride ? <Button type="button" size="sm" variant="outline" onClick={() => {
                      setCustomizeForSubmission(true);
                      setApprovalMode(plan.allowedModes.includes(plan.approvalMode as "Sequential" | "Parallel") ? plan.approvalMode : null);
                      const eligibleIds = new Set(plan.eligibleApprovers.map(item => item.userId));
                      setSelectedReviewers(plan.configuredApprovers
                        .slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
                        .map(item => item.userId).filter(id => eligibleIds.has(id)));
                    }}>{t("projects.annualBudgetCustomizeThisSubmission")}</Button> : null}
                  </div>
                  {!plan.isValid ? <div role="status" className="rounded border border-destructive/30 bg-destructive/5 p-2 text-sm text-destructive"><p className="font-medium">{t(savedDraftApprovalOverride ? "projects.annualBudgetDraftApprovalCannotSubmit" : "projects.annualBudgetDefaultsCannotSubmit")}</p>{plan.validationIssues.map(issue => <p key={issue} className="mt-1 text-xs">{t(`projects.annualBudgetApprovalIssue.${issue}`)}</p>)}</div> : null}
                  {plan.configuredApprovers.length ? <ol className={`list-inside space-y-1 text-sm ${plan.approvalMode === "Parallel" ? "list-disc" : "list-decimal"}`}>{plan.configuredApprovers.map(approver => <li key={approver.userId} className={!approver.isEligible ? "text-destructive" : undefined}>{approver.displayName ?? approver.userId}{approver.issueCode === "submitter_separation_of_duties" ? ` · ${t("projects.annualBudgetSelfApprovalBlocked")}` : !approver.isEligible ? ` · ${t("projects.annualBudgetApproverUnavailable")}` : ""}</li>)}</ol> : <p className="text-sm text-muted-foreground">{t("projects.annualBudgetDefaultsMissing")}</p>}
                  {!customizeForSubmission ? <p className="text-sm"><span className="font-medium">{t("projects.annualBudgetApprovalMethod")}:</span> {currentMode === "Sequential" ? t("projects.financeApprovalSequential") : currentMode === "Parallel" ? t("projects.financeApprovalParallel") : t("projects.annualBudgetApprovalMethodInvalid")}</p> : null}
                  {customizeForSubmission ? <>
                    <div><Label htmlFor="annual-budget-review-approval-mode">{t("projects.annualBudgetApprovalMethod")}</Label><select id="annual-budget-review-approval-mode" className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm" value={approvalMode ?? ""} onChange={event => setApprovalMode((event.target.value || null) as "Sequential" | "Parallel" | null)}><option value="">{t("common.select")}</option>{plan.allowedModes.map(mode => <option key={mode} value={mode}>{t(mode === "Sequential" ? "projects.financeApprovalSequential" : "projects.financeApprovalParallel")}</option>)}</select></div>
                    <div>
                      <p className="mb-2 text-xs text-muted-foreground">{t("projects.annualBudgetDefaultsAllMandatory")}</p>
                      {plan.eligibleApprovers.length === 0 ? <p role="status" className="mb-2 text-sm text-muted-foreground">{t("projects.noEligibleApprovers")}</p> : null}
                      <ul className="grid gap-2 sm:grid-cols-2">{plan.eligibleApprovers.map(candidate => {
                        const index = selectedReviewers.indexOf(candidate.userId);
                        const isSubmitter = candidate.userId === user?.id;
                        const selectionBlockReason = "selectionBlockReason" in candidate ? candidate.selectionBlockReason : null;
                        const reasonId = isSubmitter || selectionBlockReason ? `annual-budget-approver-block-${candidate.userId}` : undefined;
                        const roleLabels = (candidate.eligibleRoleCategories ?? []).flatMap(category => category === "System"
                          ? [t("administration.areas.roles.systemCategory")]
                          : category === "Management" ? [t("administration.areas.roles.categoryOptions.management")] : []);
                        return <li key={candidate.userId} className="flex items-center justify-between gap-2 rounded border p-2">
                          <div className="min-w-0">
                            <label className="flex min-w-0 items-center gap-2 text-sm">
                              <input type="checkbox" checked={index >= 0} disabled={(isSubmitter || Boolean(selectionBlockReason)) && index < 0} aria-describedby={reasonId} onChange={event => updateReviewer(candidate.userId, event.target.checked)} />
                              <span className="truncate">{candidate.displayName}</span>
                              {roleLabels.length ? <span className="shrink-0 text-xs text-muted-foreground">({roleLabels.join(", ")})</span> : null}
                            </label>
                            {isSubmitter || selectionBlockReason ? <p id={reasonId} className="ms-6 mt-1 text-xs text-muted-foreground">{t(isSubmitter ? "projects.annualBudgetSelfApprovalBlocked" : annualBudgetCandidateBlockMessageKey(selectionBlockReason!))}</p> : null}
                          </div>
                          {approvalMode === "Sequential" && index >= 0 ? <div className="flex items-center gap-1"><span className="min-w-5 text-center text-xs text-muted-foreground">{index + 1}</span><Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverUp", { name: candidate.displayName })} disabled={index === 0} onClick={() => moveReviewer(candidate.userId, -1)}><ArrowUp className="size-4" /></Button><Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverDown", { name: candidate.displayName })} disabled={index === selectedReviewers.length - 1} onClick={() => moveReviewer(candidate.userId, 1)}><ArrowDown className="size-4" /></Button></div> : null}
                        </li>;
                      })}</ul>
                    </div>
                    <div><Label htmlFor="annual-budget-override-reason">{t("projects.annualBudgetOverrideReason")}</Label><Textarea id="annual-budget-override-reason" value={overrideReason} onChange={event => setOverrideReason(event.target.value)} rows={3} maxLength={1000} required /></div>
                    <Button type="button" size="sm" variant="outline" onClick={() => { setCustomizeForSubmission(false); setOverrideReason(""); setApprovalMode(plan.approvalMode); setSelectedReviewers(plan.configuredApprovers.slice().sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER)).map(item => item.userId)); }}>{t(savedDraftApprovalOverride ? "projects.annualBudgetRestoreDraftApprovalPlan" : "projects.annualBudgetRestoreProjectDefaults")}</Button>
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
        <Button type="button" onClick={() => selected && void submitBudgetForReview(selected)} disabled={busy || approvalConfiguration.isLoading || approvalConfiguration.isFetching || (selected?.status === "Draft" && (draftApprovalConfiguration.isLoading || draftApprovalConfiguration.isFetching || draftApprovalConfiguration.isError)) || !canSubmitReview}>{busy ? t("common.saving") : t("projects.annualBudgetSubmitForReview")}</Button>
      </SideSheetFooter>
    </SideSheet>

    <SideSheet open={sheetOpen} onOpenChange={setSheetOpen} title={editing ? t("projects.annualBudgetEdit") : t("projects.annualBudgetCreate")} description={t("projects.annualBudgetFormDescription")}>
      <form onSubmit={event => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <div><Label htmlFor="annual-budget-fiscal-year">{t("projects.annualBudgetFiscalYear")}</Label><Input id="annual-budget-fiscal-year" type="number" min="1900" max="2200" step="1" value={form.fiscalYear} readOnly aria-readonly="true" required /></div>
          {page ? <p className="text-xs text-muted-foreground">{t("projects.annualBudgetCalendarHint", { month: page.fiscalYearStartMonth })}</p> : null}
          {isSuggestedInitialAllocation && suggestedNewFunding !== null ? <div className="rounded-md border p-3" role="note">
            <p className="text-sm font-medium">{t("projects.annualBudgetInitialAllocationProposal")}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetInitialAllocationProposalHint")}</p>
            <p className="mt-2 font-semibold tabular-nums">{money(suggestedNewFunding, page?.projectCurrency)}</p>
          </div> : null}
          {missingClosingFiscalYear !== null && Number(form.fiscalYear) === missingClosingFiscalYear + 1 ? <div className="rounded-md border px-3 py-3" role="status">
            <p className="text-sm font-medium">{t("projects.annualBudgetPriorClosingRequired", { fiscalYear: missingClosingFiscalYear })}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetOpeningUsesApprovedClosing")}</p>
          </div> : null}
          <div><Label htmlFor="annual-budget-funding">{t("projects.annualBudgetNewFunding")} *</Label><Input id="annual-budget-funding" type="number" min="0" step="0.0001" value={form.funding} onChange={event => setForm(current => ({ ...current, funding: event.target.value }))} required /></div>
          <div><Label htmlFor="annual-budget-adjustments">{t("projects.annualBudgetAdjustments")} *</Label><Input id="annual-budget-adjustments" type="number" step="0.0001" value={form.adjustments} onChange={event => setForm(current => ({ ...current, adjustments: event.target.value }))} required /></div>
          <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{t("projects.annualBudgetProposedTotal")}</p><p className="mt-1 font-semibold tabular-nums">{money(form.funding.trim() && form.adjustments.trim() ? Number(form.funding) + Number(form.adjustments) : null, authoritativeProjectCurrency ?? undefined)}</p></div>
          <p className="text-xs text-muted-foreground">{t("projects.annualBudgetDraftApprovalHint")}</p>
          {formError ? <p role="alert" className="text-sm text-destructive">{formError}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setSheetOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={busy || !authoritativeProjectCurrency || (missingClosingFiscalYear !== null && Number(form.fiscalYear) === missingClosingFiscalYear + 1)}>{busy ? t("common.saving") : t("projects.annualBudgetSaveDraft")}</Button></SideSheetFooter>
      </form>
    </SideSheet>

    <SideSheet open={draftApprovalOpen} onOpenChange={open => {
      if (saveDraftApprovalConfiguration.isPending) return;
      setDraftApprovalOpen(open);
      if (!open) setDraftApprovalInitializedFor(null);
    }} title={t("projects.annualBudgetConfigureApproval")} description={t("projects.annualBudgetDraftApprovalDescription")} size="xl">
      <SideSheetBody className="space-y-4">
        {!selectedForDraftConfiguration ? <p role="status" className="text-sm text-muted-foreground">{t("projects.annualBudgetApprovalConfigurationStale")}</p>
          : draftApprovalConfiguration.isLoading || draftApprovalConfiguration.isFetching ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
            : draftApprovalConfiguration.isError || !currentDraftApprovalConfiguration ? <div className="flex items-center justify-between gap-3"><p role="alert" className="text-sm text-destructive">{t("projects.annualBudgetApprovalConfigurationLoadFailed")}</p><Button type="button" size="sm" variant="outline" onClick={() => void draftApprovalConfiguration.refetch()}>{t("common.retry")}</Button></div>
              : <>
                <section className="space-y-2 rounded-md border p-3" aria-labelledby="annual-budget-draft-approval-current">
                  <h3 id="annual-budget-draft-approval-current" className="text-sm font-semibold">{t("projects.annualBudgetDraftApprovalCurrentPlan")}</h3>
                  <p className="text-xs text-muted-foreground">{t("projects.annualBudgetConfigurationSource")}: {t(currentDraftApprovalConfiguration.configurationSource === "DraftOverride" ? "projects.annualBudgetDraftApprovalOverrideSource" : "projects.annualBudgetProjectSettingsSource")}</p>
                  <p className="text-sm"><span className="font-medium">{t("projects.annualBudgetApprovalMethod")}:</span> {currentDraftApprovalConfiguration.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : currentDraftApprovalConfiguration.approvalMode === "Parallel" ? t("projects.financeApprovalParallel") : t("projects.annualBudgetApprovalMethodInvalid")}</p>
                  {currentDraftApprovalConfiguration.configuredApprovers.length > 0
                    ? <ol className={`list-inside space-y-1 text-sm ${currentDraftApprovalConfiguration.approvalMode === "Parallel" ? "list-disc" : "list-decimal"}`}>{currentDraftApprovalConfiguration.configuredApprovers.map(approver => <li key={approver.userId} className={!approver.isEligible ? "text-destructive" : undefined}>{approver.displayName ?? approver.userId}{approver.issueCode === "submitter_separation_of_duties" ? ` — ${t("projects.annualBudgetSelfApprovalBlocked")}` : !approver.isEligible ? ` — ${t("projects.annualBudgetApproverUnavailable")}` : ""}</li>)}</ol>
                    : <p className="text-sm text-muted-foreground">{t("projects.annualBudgetDefaultsMissing")}</p>}
                  <p className="text-xs text-muted-foreground">{t("projects.annualBudgetDraftApprovalSettingsRemainUnchanged")}</p>
                </section>
                {!currentDraftApprovalConfiguration.isValid ? <div role="status" className="rounded border border-destructive/30 bg-destructive/5 p-2 text-sm text-destructive"><p className="font-medium">{t("projects.annualBudgetDraftApprovalCannotSubmit")}</p>{currentDraftApprovalConfiguration.validationIssues.map(issue => <p key={issue} className="mt-1 text-xs">{t(`projects.annualBudgetApprovalIssue.${issue}`)}</p>)}</div> : null}
                <div className="space-y-3 rounded-md border p-3">
                  <div><Label htmlFor="annual-budget-draft-approval-mode">{t("projects.annualBudgetApprovalMethod")}</Label><select id="annual-budget-draft-approval-mode" className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm" value={draftApprovalMode ?? ""} onChange={event => setDraftApprovalMode((event.target.value || null) as "Sequential" | "Parallel" | null)}>
                    <option value="">{t("common.select")}</option>
                    {currentDraftApprovalConfiguration.allowedModes.map(mode => <option key={mode} value={mode}>{t(mode === "Sequential" ? "projects.financeApprovalSequential" : "projects.financeApprovalParallel")}</option>)}
                  </select></div>
                  <p className="text-xs text-muted-foreground">{t("projects.annualBudgetDefaultsAllMandatory")}</p>
                  {currentDraftApprovalConfiguration.eligibleApprovers.length === 0 ? <p role="status" className="text-sm text-muted-foreground">{t("projects.annualBudgetNoEligibleApprovers")}</p> : null}
                  <ul className="space-y-2">{currentDraftApprovalConfiguration.eligibleApprovers.map(candidate => {
                    const index = draftApprovalApprovers.indexOf(candidate.userId);
                    const blockReason = candidate.selectionBlockReason;
                    const disabled = Boolean(blockReason) && index < 0;
                    const roleLabels = (candidate.eligibleRoleCategories ?? []).flatMap(category => category === "System"
                      ? [t("administration.areas.roles.systemCategory")]
                      : category === "Management" ? [t("administration.areas.roles.categoryOptions.management")] : []);
                    return <li key={candidate.userId} className="flex items-center justify-between gap-2 rounded border p-2">
                      <div className="min-w-0">
                        <label className="flex min-w-0 items-center gap-2 text-sm"><input type="checkbox" checked={index >= 0} disabled={disabled} onChange={event => updateDraftApprover(candidate.userId, event.target.checked)} /><span className="truncate">{candidate.displayName}</span>{roleLabels.length ? <span className="shrink-0 text-xs text-muted-foreground">({roleLabels.join(", ")})</span> : null}</label>
                        {blockReason ? <p className="ms-6 mt-1 text-xs text-muted-foreground">{t(annualBudgetCandidateBlockMessageKey(blockReason))}</p> : null}
                      </div>
                      {draftApprovalMode === "Sequential" && index >= 0 ? <div className="flex items-center gap-1"><span className="min-w-5 text-center text-xs text-muted-foreground">{index + 1}</span><Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverUp", { name: candidate.displayName })} disabled={index === 0} onClick={() => moveDraftApprover(candidate.userId, -1)}><ArrowUp className="size-4" /></Button><Button type="button" variant="ghost" size="sm" aria-label={t("projects.yearEndClosingMoveApproverDown", { name: candidate.displayName })} disabled={index === draftApprovalApprovers.length - 1} onClick={() => moveDraftApprover(candidate.userId, 1)}><ArrowDown className="size-4" /></Button></div> : null}
                    </li>;
                  })}</ul>
                  {currentDraftApprovalConfiguration.configuredApprovers.filter(approver => !currentDraftApprovalConfiguration.eligibleApprovers.some(candidate => candidate.userId === approver.userId)).map(approver => <div key={approver.userId} className="flex items-center justify-between gap-2 rounded border border-destructive/30 p-2 text-sm text-destructive"><span>{approver.displayName ?? approver.userId} — {t("projects.annualBudgetApproverUnavailable")}</span>{draftApprovalApprovers.includes(approver.userId) ? <Button type="button" size="sm" variant="outline" onClick={() => updateDraftApprover(approver.userId, false)}>{t("projects.annualBudgetRemoveApprover")}</Button> : null}</div>)}
                </div>
                <div><Label htmlFor="annual-budget-draft-approval-reason">{t("projects.annualBudgetDraftApprovalReason")}</Label><Textarea id="annual-budget-draft-approval-reason" value={draftApprovalOverrideReason} onChange={event => setDraftApprovalOverrideReason(event.target.value)} rows={3} maxLength={1000} required /></div>
                {draftApprovalError ? <p role="alert" className="text-sm text-destructive">{draftApprovalError}</p> : null}
              </>}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" disabled={saveDraftApprovalConfiguration.isPending} onClick={() => { setDraftApprovalOpen(false); setDraftApprovalInitializedFor(null); }}>{t("common.cancel")}</Button>
        <Button type="button" onClick={() => void saveDraftApprovalPlan()} disabled={busy || draftApprovalConfiguration.isLoading || draftApprovalConfiguration.isFetching || !canSaveDraftApproval}>{saveDraftApprovalConfiguration.isPending ? t("common.saving") : t("common.save")}</Button>
      </SideSheetFooter>
    </SideSheet>
  </section>;
}

function Metric({ label, value, hint, strong = false }: { label: string; value: string; hint?: string; strong?: boolean }) {
  return <div className="rounded-md border p-3"><p className="text-xs text-muted-foreground">{label}</p><p className={`mt-1 tabular-nums ${strong ? "text-lg font-semibold" : "font-medium"}`}>{value}</p>{hint ? <p className="mt-1 text-xs text-muted-foreground">{hint}</p> : null}</div>;
}
