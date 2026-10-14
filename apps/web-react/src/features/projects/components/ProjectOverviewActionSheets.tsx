import * as React from "react";
import { Link } from "react-router-dom";
import { useI18n } from "@i18n/messages";
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
import { buildProjectChangeRequestInput } from "../lib/project-change-request-form";
import {
  useCreateProjectChangeRequest,
  useCreateProjectHandoverRequest,
  useProjectHandoverManagers,
  type ProjectDto,
  type ProjectHandoverRequestDto,
} from "../api/projects";

interface ActionSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  project: ProjectDto;
}

export function ProjectChangeRequestActionSheet({ open, onOpenChange, project }: ActionSheetProps) {
  const { t } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
  const create = useCreateProjectChangeRequest(project.id);
  const [error, setError] = React.useState<string | null>(null);
  const formRef = React.useRef<HTMLFormElement>(null);

  React.useEffect(() => {
    if (open) {
      formRef.current?.reset();
      setError(null);
    }
  }, [open]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage) return;
    const parsed = buildProjectChangeRequestInput(project.id, new FormData(event.currentTarget), project.budget);
    if (!parsed.ok) {
      setError(t(`projects.${parsed.error}`));
      return;
    }

    setError(null);
    try {
      await create.mutateAsync(parsed.value);
      formRef.current?.reset();
      onOpenChange(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("projects.changeRequestFailed"));
    }
  }

  if (!canManage) return null;

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("projects.changeRequestTitle")} description={t("projects.changeRequestDescription")} closeLabel={t("common.close")}>
      <form ref={formRef} onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
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
          <div>
            <Label htmlFor="project-change-budget">{t("projects.budgetChangeAmount")}</Label>
            <Input id="project-change-budget" className="mt-1" name="budgetChangeAmount" type="number" step="0.01" placeholder={t("projects.budgetChangeAmountPlaceholder")} />
            <p className="mt-1 text-[10px] text-muted-foreground">{t("projects.budgetChangeAmountHint")}</p>
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
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={create.isPending}>{create.isPending ? t("projects.submitting") : t("projects.submitChangeRequest")}</Button>
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}

interface HandoverActionSheetProps extends ActionSheetProps {
  requests: ProjectHandoverRequestDto[];
}

export function ProjectHandoverActionSheet({ open, onOpenChange, project, requests }: HandoverActionSheetProps) {
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage) && canMutate && !effectiveReadOnly);
  const handoverManagers = useProjectHandoverManagers(project.id, open && canManage);
  const managersLoading = handoverManagers.isLoading;
  const managersError = handoverManagers.isError;
  const create = useCreateProjectHandoverRequest(project.id);
  const [newManagerId, setNewManagerId] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const pendingRequest = requests.find((request) => request.status === "Pending");

  React.useEffect(() => {
    if (!open) return;
    setNewManagerId("");
    setError(null);
  }, [open]);

  const managerOptions = toHandoverManagerOptions(handoverManagers.data, {
    projectManager: t("projects.handoverManagerTypeProject"),
    portfolioManager: t("projects.handoverManagerTypePortfolio"),
    programManager: t("projects.handoverManagerTypeProgram"),
  });

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canManage || !newManagerId) return;
    const data = new FormData(event.currentTarget);
    setError(null);
    const reasonText = String(data.get("reason") || "").trim();
    if (!reasonText) {
      setError(t("projects.handoverReasonRequired"));
      return;
    }
    try {
      await create.mutateAsync({
        newProjectManagerId: newManagerId,
        effectiveHandoverDate: String(data.get("effectiveHandoverDate") || ""),
        reason: reasonText,
        notes: String(data.get("notes") || "").trim() || undefined,
      });
      onOpenChange(false);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t("projects.handoverFailed"));
    }
  }

  if (!canManage) return null;

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("projects.handoverTitle")} description={t("projects.handoverDescription")} closeLabel={t("common.close")}>
      {pendingRequest ? (
        <>
          <SideSheetBody className="space-y-3">
            <p className="text-sm font-medium">{t("projects.handoverPendingTitle")}</p>
            <p className="text-sm text-muted-foreground">{t("projects.handoverPendingDescription")}</p>
            <div className="rounded border px-3 py-2 text-sm">
              <p>{t("projects.newManager")}: <span className="font-medium">{pendingRequest.newProjectManagerName}</span></p>
              <p className="mt-1">{t("projects.effectiveHandoverDate")}: {formatDate(pendingRequest.effectiveHandoverDate)}</p>
              {pendingRequest.reason ? <p className="mt-1">{t("projects.handoverReason")}: {pendingRequest.reason}</p> : null}
            </div>
          </SideSheetBody>
          <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.close")}</Button></SideSheetFooter>
        </>
      ) : (
        <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
          <SideSheetBody className="space-y-4">
            {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
            <div><Label htmlFor="handover-project">{t("projects.project")}</Label><Input id="handover-project" className="mt-1" value={project.name} readOnly /></div>
            <div><Label htmlFor="handover-current-manager">{t("projects.currentProjectManager")}</Label><Input id="handover-current-manager" className="mt-1" value={project.projectManagerName || "—"} readOnly /></div>
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
              {managersError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("common.errorLoading")}</p> : null}
              {!managersLoading && !managersError && managerOptions.length === 0 ? <div className="mt-2 space-y-1 text-xs text-muted-foreground"><p>{t("projects.noEligibleManagersHint")}</p><Link to="/administration/users" className="text-primary underline-offset-4 hover:underline">{t("projects.manageUsers")}</Link></div> : null}
            </div>
            <div>
              <Label htmlFor="handover-date">{t("projects.effectiveHandoverDate")}</Label>
              <Input id="handover-date" className="mt-1" name="effectiveHandoverDate" type="date" required min={project.startDate?.slice(0, 10)} max={project.endDate?.slice(0, 10)} />
            </div>
            <div><Label htmlFor="handover-reason">{t("projects.handoverReason")}</Label><Input id="handover-reason" className="mt-1" name="reason" placeholder={t("projects.handoverReasonPlaceholder")} maxLength={500} required /></div>
            <div><Label htmlFor="handover-notes">{t("projects.handoverNotes")}</Label><Textarea id="handover-notes" className="mt-1" name="notes" rows={3} maxLength={2000} placeholder={t("projects.handoverNotesPlaceholder")} /></div>
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={create.isPending || managersLoading || managersError || !newManagerId}>{create.isPending ? t("common.saving") : t("projects.submitHandover")}</Button>
          </SideSheetFooter>
        </form>
      )}
    </SideSheet>
  );
}
