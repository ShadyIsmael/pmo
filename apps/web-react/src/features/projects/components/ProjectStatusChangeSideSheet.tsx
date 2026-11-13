import * as React from "react";
import { AlertCircle, Check } from "lucide-react";
import { useManagement } from "@api/management";
import { useAuth } from "@auth/AuthProvider";
import { Button } from "@components/ui/Button";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { ErrorState } from "@components/ui/StateDisplay";
import { Skeleton } from "@components/ui/Skeleton";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { useChangeProjectStatus, useProjectLifecycleOptions, projectLifecycleLabel } from "../api/project-lifecycle";
import type { ProjectDto } from "../api/projects";

export function ProjectStatusChangeSideSheet({
  open,
  onOpenChange,
  project,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  project: ProjectDto;
}) {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canChange = Boolean(user && hasPermission(user.permissions, Permissions.ProjectStatus.Change)
    && canMutate && !effectiveReadOnly);
  const options = useProjectLifecycleOptions({ enabled: open && canChange, projectId: project.publicId });
  const changeStatus = useChangeProjectStatus(project.publicId);
  const [targetStatusId, setTargetStatusId] = React.useState("");
  const [reason, setReason] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const [saved, setSaved] = React.useState(false);

  React.useEffect(() => {
    if (!open) return;
    setTargetStatusId("");
    setReason("");
    setError(null);
    setSaved(false);
  }, [open, project.publicId]);

  const statuses = options.data?.statuses ?? [];
  const availableTargets = statuses.filter((status) => status.isActive !== false
    && status.id !== project.statusId
    && status.code.toLocaleUpperCase() !== project.status.toLocaleUpperCase());
  const selectedTarget = availableTargets.find((status) => status.id === targetStatusId);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedTarget || changeStatus.isPending) return;
    setError(null);
    setSaved(false);
    try {
      await changeStatus.mutateAsync({
        targetStatusId: selectedTarget.id,
        ...(reason.trim() ? { reason: reason.trim() } : {}),
      });
      setSaved(true);
      setTargetStatusId("");
      setReason("");
    } catch {
      setError(t("projects.lifecycle.statusChangeFailed"));
    }
  };

  return <SideSheet
    open={open}
    onOpenChange={onOpenChange}
    title={<span dir={locale === "ar" ? "rtl" : "ltr"}>{t("projects.lifecycle.changeStatus")}</span>}
    description={<span dir={locale === "ar" ? "rtl" : "ltr"}>{project.name} · {t("projects.lifecycle.currentStatus")}: {locale === "ar" ? project.statusNameAr || project.status : project.statusNameEn || project.status}</span>}
    ariaLabel={t("projects.lifecycle.changeStatus")}
    closeLabel={t("common.close")}
    size="md"
  >
    <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
      <SideSheetBody dir={locale === "ar" ? "rtl" : "ltr"} className="space-y-4">
        {options.isLoading ? <div role="status" aria-label={t("common.loading")} className="space-y-3"><Skeleton className="h-10" /><Skeleton className="h-20" /></div> : null}
        {options.isError ? <ErrorState title={t("projects.lifecycle.lookupError")} onRetry={() => void options.refetch()} /> : null}
        {error ? <div role="alert" className="flex items-start gap-2 rounded-md border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive"><AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div> : null}
        {saved ? <p role="status" className="rounded-md border border-success/20 bg-success/10 p-3 text-sm text-success">{t("projects.lifecycle.statusUpdated")}</p> : null}
        {!options.isLoading && !options.isError ? <>
          <div className="space-y-1.5">
            <label htmlFor="project-status-target" className="text-sm font-medium">{t("projects.lifecycle.changeStatus")}</label>
            <select
              id="project-status-target"
              value={targetStatusId}
              onChange={(event) => { setTargetStatusId(event.target.value); setError(null); setSaved(false); }}
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
              required
            >
              <option value="">{t("projects.lifecycle.chooseStatus")}</option>
              {availableTargets.map((status) => <option key={status.id} value={status.id}>{projectLifecycleLabel(status, locale)}</option>)}
            </select>
            {!availableTargets.length ? <p className="text-xs text-muted-foreground">{t("projects.lifecycle.noOtherStatusesAvailable")}</p> : null}
          </div>
          <div className="space-y-1.5">
            <label htmlFor="project-status-reason" className="text-sm font-medium">{t("projects.lifecycle.reason")}</label>
            <textarea id="project-status-reason" value={reason} onChange={(event) => setReason(event.target.value)} rows={3} maxLength={2000} className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" />
            <p className="text-xs text-muted-foreground">{t("projects.lifecycle.statusChangeReasonHint")}</p>
          </div>
        </> : null}
      </SideSheetBody>
      <SideSheetFooter dir={locale === "ar" ? "rtl" : "ltr"}>
        <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.close")}</Button>
        <Button type="submit" disabled={!canChange || !selectedTarget || changeStatus.isPending || options.isLoading || options.isError}>
          <Check className="me-2 h-4 w-4" />{changeStatus.isPending ? t("projects.lifecycle.savingStatus") : t("projects.lifecycle.saveStatusChange")}
        </Button>
      </SideSheetFooter>
    </form>
  </SideSheet>;
}
