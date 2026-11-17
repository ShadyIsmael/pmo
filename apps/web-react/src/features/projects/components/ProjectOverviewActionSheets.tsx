import * as React from "react";
import { Link } from "react-router-dom";
import { CircleDollarSign, FilePen, Loader2, Paperclip, Save, Send } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { getApiErrorMessage } from "@api/client";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { toHandoverManagerOptions } from "../lib/handover-manager-options";
import { hasPermission, Permissions } from "@lib/permissions";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { buildProjectChangeRequestInput } from "../lib/project-change-request-form";
import {
  useCreateProjectChangeRequest,
  useProjectChangeRequestApproverOptions,
  useSubmitProjectChangeRequest,
  useUpdateProjectChangeRequestDraft,
  useCancelProjectHandoverRequest,
  useCreateProjectHandoverRequest,
  useProjectHandoverCapabilities,
  useProjectHandoverManagers,
  type ProjectDto,
  type ProjectChangeRequestDto,
  type ProjectHandoverInitiationType,
  type ProjectHandoverRequestDto,
} from "../api/projects";

interface ActionSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  project: ProjectDto;
}

export function ProjectChangeRequestActionSheet({ open, onOpenChange, project, request }: ActionSheetProps & { request?: ProjectChangeRequestDto | null }) {
  const { t } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
  const create = useCreateProjectChangeRequest(project.publicId);
  const update = useUpdateProjectChangeRequestDraft(project.publicId);
  const submitDraft = useSubmitProjectChangeRequest(project.publicId);
  const approverOptions = useProjectChangeRequestApproverOptions(project.publicId, open && canManage);
  const [error, setError] = React.useState<string | null>(null);
  const [savedDraft, setSavedDraft] = React.useState<ProjectChangeRequestDto | null>(null);
  const [approvalStrategy, setApprovalStrategy] = React.useState("AllRequired");
  const [approverIds, setApproverIds] = React.useState<string[]>([]);
  const formRef = React.useRef<HTMLFormElement>(null);

  React.useEffect(() => {
    if (!open) return;
    formRef.current?.reset();
    setSavedDraft(request?.status === "Draft" ? request : null);
    setApprovalStrategy(request?.approvalStrategy ?? "AllRequired");
    setApproverIds(request?.selectedApproverUserIds ?? []);
    setError(null);
    const form = formRef.current;
    if (!form || !request) return;
    const setValue = (name: string, value: string | number | null | undefined) => {
      const input = form.elements.namedItem(name);
      if (input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement || input instanceof HTMLSelectElement)
        input.value = value == null ? "" : String(value);
    };
    setValue("type", request.type);
    setValue("reason", request.reason);
    setValue("budgetChangeAmount", request.budgetChangeAmount);
    setValue("justification", request.justification);
    setValue("impact", request.impact);
    setValue("attachments", request.attachments);
  }, [open, request]);

  React.useEffect(() => {
    if (!open || request || approverIds.length > 0 || !approverOptions.data?.sponsorUserId) return;
    if (approverOptions.data.approvers.some((item) => item.userId === approverOptions.data?.sponsorUserId))
      setApproverIds([approverOptions.data.sponsorUserId]);
  }, [open, request, approverIds.length, approverOptions.data]);

  async function saveDraftOrSubmit(submitForReview: boolean) {
    if (!canManage) return;
    const form = formRef.current;
    if (!form?.reportValidity()) return;
    if (submitForReview && approverOptions.data?.policyMessage) {
      setError(approverOptions.data.policyMessage);
      return;
    }
    const parsed = buildProjectChangeRequestInput(project.id, new FormData(form), project.budget, {
      strategy: approvalStrategy,
      approverUserIds: approverIds,
    });
    if (!parsed.ok) {
      setError(t(`projects.${parsed.error}`));
      return;
    }

    setError(null);
    try {
      const current = savedDraft ?? (request?.status === "Draft" ? request : null);
      const draft = current
        ? await update.mutateAsync({ id: current.id, dto: { ...parsed.value, rowVersion: current.rowVersion } })
        : await create.mutateAsync(parsed.value);
      setSavedDraft(draft);
      if (submitForReview) await submitDraft.mutateAsync({ id: draft.id, rowVersion: draft.rowVersion });
      onOpenChange(false);
    } catch (reason) {
      setError(getApiErrorMessage(reason, t("projects.changeRequestFailed")));
    }
  }

  const busy = create.isPending || update.isPending || submitDraft.isPending;
  const selectedApproversValid = approverIds.length > 0
    && approverIds.every((id) => approverOptions.data?.approvers.some((item) => item.userId === id));
  const canSubmit = Boolean(approverOptions.data && !approverOptions.data.policyMessage
    && selectedApproversValid && approverOptions.data.allowedStrategies.includes(approvalStrategy));

  if (!canManage) return null;

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={savedDraft || request?.status === "Draft" ? t("projects.editChangeRequestDraft") : t("projects.changeRequestTitle")} description={t("projects.changeRequestDescription")} closeLabel={t("common.close")} size="lg">
      <form ref={formRef} onSubmit={(event) => { event.preventDefault(); void saveDraftOrSubmit(true); }} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-6">
          {error ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
          <section className="space-y-4">
            <div className="flex items-center gap-2 border-b border-border/60 pb-2">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary"><FilePen className="h-3.5 w-3.5" /></span>
              <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.changeRequestSectionDetails")}</h3>
            </div>
            <div>
              <Label htmlFor="project-change-type">{t("projects.changeType")} <span aria-hidden="true" className="text-destructive">*</span></Label>
              <select id="project-change-type" className="input-field mt-1 w-full" name="type" defaultValue="Administrative">
                <option value="Administrative">{t("projects.changeTypeAdministrative")}</option>
                <option value="Baseline">{t("projects.changeTypeBaseline")}</option>
              </select>
            </div>
            <div>
              <Label htmlFor="project-change-reason">{t("projects.changeReason")} <span aria-hidden="true" className="text-destructive">*</span></Label>
              <Input id="project-change-reason" className="mt-1" name="reason" placeholder={t("projects.changeReasonPlaceholder")} maxLength={500} required />
            </div>
          </section>
          <section className="space-y-4">
            <div className="flex items-center gap-2 border-b border-border/60 pb-2">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary"><CircleDollarSign className="h-3.5 w-3.5" /></span>
              <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.changeRequestSectionFinancial")}</h3>
            </div>
            <div>
              <Label htmlFor="project-change-budget">{t("projects.budgetChangeAmount")}</Label>
              <Input id="project-change-budget" className="mt-1" name="budgetChangeAmount" type="number" step="0.01" placeholder={t("projects.budgetChangeAmountPlaceholder")} />
              <p className="mt-1 text-[11px] leading-relaxed text-muted-foreground">{t("projects.budgetChangeAmountHint")}</p>
            </div>
          </section>
          <section className="space-y-4">
            <div className="flex items-center gap-2 border-b border-border/60 pb-2">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary"><Paperclip className="h-3.5 w-3.5" /></span>
              <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.changeRequestSectionContext")}</h3>
            </div>
            <div>
              <Label htmlFor="project-change-justification">{t("projects.justification")}</Label>
              <Textarea id="project-change-justification" className="mt-1" name="justification" rows={3} maxLength={2000} placeholder={t("projects.justificationPlaceholder")} />
            </div>
            <div>
              <Label htmlFor="project-change-impact">{t("projects.impactDescription")}</Label>
              <Textarea id="project-change-impact" className="mt-1" name="impact" rows={3} maxLength={2000} placeholder={t("projects.impactPlaceholder")} />
            </div>
            <div>
              <Label htmlFor="project-change-attachments">{t("projects.attachments")}</Label>
              <Textarea id="project-change-attachments" className="mt-1" name="attachments" rows={2} maxLength={4000} placeholder={t("projects.attachmentsPlaceholder")} />
            </div>
          </section>
          <section className="space-y-4">
            <div className="flex items-center gap-2 border-b border-border/60 pb-2">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary"><Send className="h-3.5 w-3.5" /></span>
              <h3 className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.changeApprovals")}</h3>
            </div>
            {approverOptions.isLoading ? <p className="text-sm text-muted-foreground">{t("projects.loadingApprovers")}</p>
              : approverOptions.isError ? <p role="alert" className="text-sm text-destructive">{t("projects.approversLoadFailed")}</p>
                : approverOptions.data?.policyMessage ? <p role="alert" className="rounded-md border border-warning/40 bg-warning/5 px-3 py-2 text-sm text-warning">{approverOptions.data.policyMessage}</p> : null}
            <div>
              <Label htmlFor="project-change-strategy">{t("projects.approvalStrategy")}</Label>
              <select id="project-change-strategy" className="input-field mt-1 w-full" value={approvalStrategy} onChange={(event) => setApprovalStrategy(event.target.value)} disabled={!approverOptions.data}>
                {(approverOptions.data?.allowedStrategies ?? ["AllRequired"]).map((strategy) => <option key={strategy} value={strategy}>{t(`projects.approvalStrategies.${strategy}`)}</option>)}
              </select>
            </div>
            <div className="space-y-2">
              <p className="text-sm font-medium">{t("projects.selectApprovers")}</p>
              {(approverOptions.data?.approvers ?? []).map((approver) => (
                <label key={approver.userId} className="flex items-start gap-2 rounded-md border border-border/70 px-3 py-2 text-sm">
                  <input type="checkbox" className="mt-1 accent-primary" checked={approverIds.includes(approver.userId)} disabled={busy || !approverOptions.data?.approvers}
                    onChange={(event) => setApproverIds((current) => event.target.checked
                      ? [...current, approver.userId]
                      : current.filter((id) => id !== approver.userId))} />
                  <span className="min-w-0 flex-1"><span className="font-medium">{approver.fullName}</span><span className="ms-2 text-xs text-muted-foreground">{approver.projectRole === "Business Approver" ? t("projects.businessApprover") : approver.projectRole}</span>
                    {approver.isMandatory ? <span className="ms-2 text-xs font-semibold text-warning">{t("projects.mandatoryApprover")}</span> : null}</span>
                </label>
              ))}
              {approverOptions.data && (approverOptions.data.approvers ?? []).length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.noEligibleApprovers")}</p> : null}
            </div>
          </section>
          <p className="text-[11px] text-muted-foreground">{t("projects.changeRequestRequiredHint")}</p>
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="button" variant="outline" disabled={busy} onClick={() => void saveDraftOrSubmit(false)} className="gap-2">
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
            {t("projects.saveChangeRequestDraft")}
          </Button>
          <Button type="submit" disabled={busy || !canSubmit} className="gap-2">
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            {busy ? t("projects.submitting") : t("projects.submitChangeRequest")}
          </Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}

interface HandoverActionSheetProps extends ActionSheetProps {
  requests: ProjectHandoverRequestDto[];
  requestsLoading?: boolean;
  requestsError?: boolean;
  requestsErrorMessage?: string;
}

export function ProjectHandoverActionSheet({ open, onOpenChange, project, requests, requestsLoading, requestsError, requestsErrorMessage }: HandoverActionSheetProps) {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canChange = canMutate && !effectiveReadOnly;
  const [newManagerId, setNewManagerId] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [initiationType, setInitiationType] = React.useState<ProjectHandoverInitiationType>("ManagerInitiated");
  const [confirmCancel, setConfirmCancel] = React.useState(false);
  const capabilities = useProjectHandoverCapabilities(project.publicId, open);
  const selectedAllowed = initiationType === "ManagerInitiated"
    ? capabilities.data?.canInitiate
    : initiationType === "AdminInitiated" ? capabilities.data?.canAdminInitiate : capabilities.data?.canAssignInitial;
  const canOpen = Boolean(canChange && capabilities.data && (capabilities.data.canInitiate || capabilities.data.canAdminInitiate || capabilities.data.canAssignInitial));
  const handoverManagers = useProjectHandoverManagers(project.publicId, open && canChange && Boolean(selectedAllowed));
  const managersLoading = handoverManagers.isLoading;
  const managersError = handoverManagers.isError;
  const create = useCreateProjectHandoverRequest(project.publicId);
  const cancel = useCancelProjectHandoverRequest(project.publicId);
  const pendingRequest = requests.find((request) => request.status === "Pending");
  const canCancelPending = Boolean(pendingRequest && capabilities.data?.canCancelOwnPending && canChange && pendingRequest.requestedBy === user?.id);

  React.useEffect(() => {
    if (!open) return;
    setNewManagerId("");
    setError(null);
    if (capabilities.data?.canInitiate) setInitiationType("ManagerInitiated");
    else if (capabilities.data?.canAdminInitiate) setInitiationType("AdminInitiated");
    else if (capabilities.data?.canAssignInitial) setInitiationType("InitialAssignment");
  }, [open, capabilities.data]);

  const managerOptions = toHandoverManagerOptions(handoverManagers.data, {
    projectManager: t("projects.handoverManagerTypeProject"),
    portfolioManager: t("projects.handoverManagerTypePortfolio"),
    programManager: t("projects.handoverManagerTypeProgram"),
  });

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canOpen || !selectedAllowed || !newManagerId) return;
    const data = new FormData(event.currentTarget);
    setError(null);
    const reasonText = String(data.get("reason") || "").trim();
    if (!reasonText) {
      setError(t("projects.handoverReasonRequired"));
      return;
    }
    try {
      await create.mutateAsync({
        initiationType,
        newProjectManagerId: newManagerId,
        effectiveHandoverDate: String(data.get("effectiveHandoverDate") || ""),
        reason: reasonText,
        notes: String(data.get("notes") || "").trim() || undefined,
      });
      onOpenChange(false);
    } catch (reason) {
      setError(getApiErrorMessage(reason, t("projects.handoverFailed")));
    }
  }

  async function cancelPending() {
    if (!pendingRequest || !canCancelPending) return;
    try {
      await cancel.mutateAsync(pendingRequest.id);
      setConfirmCancel(false);
      setError(null);
    } catch (reason) {
      setError(getApiErrorMessage(reason, t("managerHandover.cancelFailed")));
      setConfirmCancel(false);
    }
  }

  const modes: { value: ProjectHandoverInitiationType; label: string; allowed: boolean }[] = [
    { value: "ManagerInitiated", label: t("managerHandover.request"), allowed: Boolean(capabilities.data?.canInitiate) },
    { value: "AdminInitiated", label: t("managerHandover.adminRequest"), allowed: Boolean(capabilities.data?.canAdminInitiate) },
    { value: "InitialAssignment", label: t("managerHandover.initialAssignment"), allowed: Boolean(capabilities.data?.canAssignInitial) },
  ];

  if (!canOpen && !open) return null;

  return (
    <>
    <SideSheet open={open} onOpenChange={onOpenChange} title={t(initiationType === "ManagerInitiated" ? "managerHandover.requestTitle" : initiationType === "AdminInitiated" ? "managerHandover.adminRequestTitle" : "managerHandover.initialAssignmentTitle")} description={t(initiationType === "ManagerInitiated" ? "managerHandover.requestDescription" : initiationType === "AdminInitiated" ? "managerHandover.adminRequestDescription" : "managerHandover.initialAssignmentDescription", { entity: t("projects.project") })} closeLabel={t("common.close")}>
      {pendingRequest ? (
        <>
          <SideSheetBody className="space-y-3">
            <p className="text-sm font-medium">{t("projects.handoverPendingTitle")}</p>
            <p className="text-sm text-muted-foreground">{t("projects.handoverPendingDescription")}</p>
            <div className="rounded border px-3 py-2 text-sm">
              <p>{t("managerHandover.currentManager")}: <span className="font-medium">{pendingRequest.currentProjectManagerName || t("managerHandover.notAssigned")}</span></p>
              <p>{t("projects.newManager")}: <span className="font-medium">{pendingRequest.newProjectManagerName}</span></p>
              <p className="mt-1">{t("projects.effectiveHandoverDate")}: {formatDate(pendingRequest.effectiveHandoverDate)}</p>
              {pendingRequest.initiationType !== "ManagerInitiated" ? <p className="mt-1 text-xs text-muted-foreground">{t(pendingRequest.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}
              {pendingRequest.reason ? <p className="mt-1">{t("projects.handoverReason")}: {pendingRequest.reason}</p> : null}
            </div>
            {canCancelPending ? <Button type="button" variant="outline" onClick={() => setConfirmCancel(true)} disabled={cancel.isPending}>{t("managerHandover.cancel")}</Button> : null}
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.close")}</Button></SideSheetFooter>
        </>
      ) : (
        <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-4">
            {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
            {requestsLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p> : null}
            {requestsError || capabilities.isError ? <p role="alert" className="text-sm text-destructive">{requestsErrorMessage || getApiErrorMessage(capabilities.error, t("managerHandover.loadFailed"))}</p> : null}
            <div className="space-y-2"><Label>{t("managerHandover.handover")}</Label><div className="flex flex-wrap gap-2">{modes.filter((mode) => mode.allowed).map((mode) => <Button key={mode.value} type="button" size="sm" variant={initiationType === mode.value ? "default" : "outline"} onClick={() => { setInitiationType(mode.value); setNewManagerId(""); setError(null); }}>{mode.label}</Button>)}</div></div>
            <div><Label htmlFor="handover-project">{t("projects.project")}</Label><Input id="handover-project" className="mt-1" value={project.name} readOnly /></div>
            <div><Label htmlFor="handover-current-manager">{t("projects.currentProjectManager")}</Label><Input id="handover-current-manager" className="mt-1" value={capabilities.data?.currentManagerName || project.projectManagerName || t("managerHandover.notAssigned")} readOnly /></div>
            <div>
              <Label>{t("projects.newManager")}</Label>
              <SearchableSelect
                options={managerOptions}
                value={newManagerId}
                onValueChange={setNewManagerId}
                placeholder={t("projects.selectProjectManager")}
                searchPlaceholder={t("projects.searchManagerPlaceholder")}
                loading={managersLoading}
                emptyText={t("projects.noEligibleManagers")}
                disabled={managersError}
              />
              {managersError ? <p role="alert" className="mt-1 text-xs text-destructive">{getApiErrorMessage(handoverManagers.error, t("common.errorLoading"))}</p> : null}
              {!managersLoading && !managersError && managerOptions.length === 0 ? <div className="mt-2 space-y-1 text-xs text-muted-foreground"><p>{t("projects.noEligibleManagersHint")}</p><Link to="/administration/users" className="text-primary underline-offset-4 hover:underline">{t("projects.manageUsers")}</Link></div> : null}
            </div>
            <div>
              <Label htmlFor="handover-date">{t("projects.effectiveHandoverDate")}</Label>
              <Input id="handover-date" className="mt-1" name="effectiveHandoverDate" type="date" required min={project.startDate?.slice(0, 10)} max={project.endDate?.slice(0, 10)} />
              <p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.effectiveDateHint")}</p>
            </div>
            <div><Label htmlFor="handover-reason">{t(initiationType === "ManagerInitiated" ? "projects.handoverReason" : "managerHandover.administrativeReason")}</Label><Input id="handover-reason" className="mt-1" name="reason" placeholder={t("projects.handoverReasonPlaceholder")} maxLength={500} required /></div>
            <div><Label htmlFor="handover-notes">{t("projects.handoverNotes")}</Label><Textarea id="handover-notes" className="mt-1" name="notes" rows={3} maxLength={2000} placeholder={t("projects.handoverNotesPlaceholder")} /></div>
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={create.isPending || requestsLoading || requestsError || Boolean(pendingRequest) || managersLoading || managersError || !newManagerId || !selectedAllowed}>{create.isPending ? t("common.saving") : t("projects.submitHandover")}</Button>
          </SideSheetFooter>
        </form>
      )}
    </SideSheet>
    <Dialog open={confirmCancel} onOpenChange={setConfirmCancel}><DialogContent><DialogHeader><DialogTitle>{t("managerHandover.cancelTitle")}</DialogTitle><DialogDescription>{t("managerHandover.cancelDescription")}</DialogDescription></DialogHeader><DialogFooter><Button type="button" variant="outline" onClick={() => setConfirmCancel(false)} disabled={cancel.isPending}>{t("common.cancel")}</Button><Button type="button" variant="destructive" onClick={() => void cancelPending()} disabled={cancel.isPending}>{cancel.isPending ? t("common.saving") : t("managerHandover.confirmCancel")}</Button></DialogFooter></DialogContent></Dialog>
    </>
  );
}
