import * as React from "react";
import { ApiError } from "@api/client";
import { ArrowDown, ArrowUp } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useProjectAnnualBudgetApprovalConfiguration, useSaveProjectAnnualBudgetApprovalConfiguration } from "./api/project-annual-budgets";

export function ProjectAnnualBudgetApprovalSettings({ projectId, canManage, enabled }: {
  projectId: string;
  canManage: boolean;
  enabled: boolean;
}) {
  const { t } = useI18n();
  const configuration = useProjectAnnualBudgetApprovalConfiguration(projectId, enabled);
  const save = useSaveProjectAnnualBudgetApprovalConfiguration(projectId);
  const [editing, setEditing] = React.useState(false);
  const [mode, setMode] = React.useState<"Sequential" | "Parallel" | null>(null);
  const [approvers, setApprovers] = React.useState<string[]>([]);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!configuration.data || editing) return;
    setMode(configuration.data.approvalMode);
    setApprovers(configuration.data.configuredApprovers
      .slice()
      .sort((left, right) => (left.position ?? Number.MAX_SAFE_INTEGER) - (right.position ?? Number.MAX_SAFE_INTEGER))
      .map(item => item.userId));
  }, [configuration.data, editing]);

  React.useEffect(() => {
    if (!canManage) setEditing(false);
  }, [canManage]);

  const move = (userId: string, delta: -1 | 1) => setApprovers(current => {
    const index = current.indexOf(userId);
    const next = index + delta;
    if (index < 0 || next < 0 || next >= current.length) return current;
    const updated = [...current];
    [updated[index], updated[next]] = [updated[next], updated[index]];
    return updated;
  });

  const submit = async () => {
    const current = configuration.data;
    if (!canManage || !current || !mode || approvers.length === 0) return;
    setError(null);
    try {
      await save.mutateAsync({
        approvalMode: mode,
        approverUserIds: approvers,
        rowVersion: current.rowVersion,
        projectSettingsRowVersion: current.projectSettingsRowVersion,
      });
      setEditing(false);
    } catch (cause) {
      setError(cause instanceof ApiError && cause.status === 409
        ? t("projects.annualBudgetApprovalConfigurationStale")
        : t("projects.annualBudgetApprovalConfigurationSaveFailed"));
    }
  };

  const data = configuration.data;
  const eligibleById = new Map((data?.eligibleApprovers ?? []).map(item => [item.userId, item]));
  const configuredNames = data?.configuredApprovers.map(item => item.displayName ?? item.userId) ?? [];

  return <Card className="overflow-hidden">
    <CardHeader className="border-b bg-muted/30 p-5 sm:p-6">
      <CardTitle className="text-lg">{t("projects.annualBudgetProjectDefaults")}</CardTitle>
      <p className="text-sm text-muted-foreground">{t("projects.annualBudgetProjectDefaultsDescription")}</p>
    </CardHeader>
    <CardContent className="space-y-4 p-5 sm:p-6">
      {configuration.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
        : configuration.isError || !data ? <div className="flex items-center justify-between gap-3"><p role="alert" className="text-sm text-destructive">{t("projects.annualBudgetApprovalConfigurationLoadFailed")}</p><Button type="button" size="sm" variant="outline" onClick={() => void configuration.refetch()}>{t("common.retry")}</Button></div>
          : <>
            <div className="rounded-xl border bg-muted/20 p-4 text-sm">
              <p><span className="font-medium">{t("projects.annualBudgetConfigurationSource")}:</span> {t("projects.annualBudgetProjectSettingsSource")}</p>
              {!data.isValid ? <div role="status" className="mt-2 space-y-1 text-warning">
                <p className="font-medium">{t("projects.annualBudgetDefaultsInvalid")}</p>
                {data.validationIssues.map(issue => <p key={issue} className="text-xs">{t(`projects.annualBudgetApprovalIssue.${issue}`)}</p>)}
              </div> : null}
              {configuredNames.length > 0 ? <ol className={`mt-2 list-inside space-y-1 text-muted-foreground ${data.approvalMode === "Parallel" ? "list-disc" : "list-decimal"}`}>
                {data.configuredApprovers.map(approver => <li key={approver.userId}>
                  {approver.displayName ?? approver.userId}
                  {!approver.isEligible ? <span className="ms-2 text-destructive">{t("projects.annualBudgetApproverUnavailable")}</span> : null}
                </li>)}
              </ol> : <p className="mt-2 text-muted-foreground">{t("projects.annualBudgetDefaultsMissing")}</p>}
              <p className="mt-2 text-muted-foreground">{t("projects.annualBudgetApprovalMethod")}: {data.approvalMode === "Sequential" ? t("projects.financeApprovalSequential") : data.approvalMode === "Parallel" ? t("projects.financeApprovalParallel") : t("projects.annualBudgetApprovalMethodInvalid")}</p>
            </div>
            {!data.financeApprovalMode ? <p role="status" className="text-sm text-muted-foreground">{t("projects.annualBudgetProjectModeRequired")}</p> : null}
            {editing && canManage ? <div className="space-y-4 rounded-xl border p-4 sm:p-5">
              <div>
                <Label htmlFor="annual-budget-default-mode">{t("projects.annualBudgetApprovalMethod")}</Label>
                <select id="annual-budget-default-mode" className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm" value={mode ?? ""} onChange={event => setMode((event.target.value || null) as "Sequential" | "Parallel" | null)}>
                  <option value="">{t("common.select")}</option>
                  {data.allowedModes.map(allowed => <option key={allowed} value={allowed}>{t(allowed === "Sequential" ? "projects.financeApprovalSequential" : "projects.financeApprovalParallel")}</option>)}
                </select>
              </div>
              <p className="text-xs text-muted-foreground">{t("projects.annualBudgetDefaultsAllMandatory")}</p>
              {data.eligibleApprovers.length === 0 ? <p role="status" className="text-sm text-muted-foreground">{t("projects.annualBudgetNoEligibleApprovers")}</p> : null}
              <ul className="space-y-2">{data.eligibleApprovers.map(user => {
                const index = approvers.indexOf(user.userId);
                const roleLabels = (user.eligibleRoleCategories ?? []).flatMap(category => category === "System"
                  ? [t("administration.areas.roles.systemCategory")]
                  : category === "Management" ? [t("administration.areas.roles.categoryOptions.management")] : []);
                return <li key={user.userId} className="flex items-center justify-between gap-2 rounded-lg border bg-card p-3">
                  <label className="flex min-w-0 items-center gap-2 text-sm"><input type="checkbox" checked={index >= 0} onChange={event => setApprovers(current => event.target.checked ? [...current, user.userId] : current.filter(id => id !== user.userId))} /><span className="truncate">{user.displayName}</span>{roleLabels.length ? <span className="shrink-0 text-xs text-muted-foreground">({roleLabels.join(", ")})</span> : null}</label>
                  {mode === "Sequential" && index >= 0 ? <div className="flex items-center gap-1"><span className="min-w-5 text-center text-xs text-muted-foreground">{index + 1}</span><Button type="button" size="sm" variant="ghost" aria-label={t("projects.yearEndClosingMoveApproverUp", { name: user.displayName })} disabled={index === 0} onClick={() => move(user.userId, -1)}><ArrowUp className="size-4" /></Button><Button type="button" size="sm" variant="ghost" aria-label={t("projects.yearEndClosingMoveApproverDown", { name: user.displayName })} disabled={index === approvers.length - 1} onClick={() => move(user.userId, 1)}><ArrowDown className="size-4" /></Button></div> : null}
                </li>;
              })}</ul>
              {approvers.some(id => !eligibleById.has(id)) ? <p role="alert" className="text-sm text-destructive">{t("projects.annualBudgetApproverUnavailable")}</p> : null}
              {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
              <div className="flex flex-wrap gap-2"><Button type="button" size="sm" onClick={() => void submit()} disabled={!canManage || !mode || !data.allowedModes.includes(mode) || !approvers.length || approvers.some(id => !eligibleById.has(id)) || save.isPending}>{save.isPending ? t("common.saving") : t("common.save")}</Button><Button type="button" size="sm" variant="outline" onClick={() => setEditing(false)} disabled={save.isPending}>{t("common.cancel")}</Button></div>
            </div> : canManage ? <Button type="button" size="sm" variant="outline" onClick={() => {
              setError(null);
              const eligibleIds = new Set(data.eligibleApprovers.map(item => item.userId));
              setApprovers(current => current.filter(id => eligibleIds.has(id)));
              setEditing(true);
            }} disabled={!data.allowedModes.length}>{t("projects.annualBudgetEditProjectDefaults")}</Button> : null}
          </>}
    </CardContent>
  </Card>;
}
