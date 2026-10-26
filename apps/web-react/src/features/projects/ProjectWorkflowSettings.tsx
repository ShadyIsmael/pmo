import * as React from "react";
import { AlertTriangle, CheckCircle2, Settings2 } from "lucide-react";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { PageHeader } from "@components/layout/PageHeader";
import { useI18n } from "@i18n/messages";
import { useManagement } from "@api/management";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { useProjectWorkflowSettings, useWorkflowDefinitions, useWorkflowMutations, type WorkflowDomain, type WorkflowRowVersion } from "@features/workflow/api/workflows";

const DOMAINS: WorkflowDomain[] = ["Task", "Risk", "Finance", "ChangeRequest"];

export function ProjectWorkflowSettings({ projectId }: { projectId: string }) {
  const { t } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const settings = useProjectWorkflowSettings(projectId);
  const mutations = useWorkflowMutations();
  const [error, setError] = React.useState<string | null>(null);
  const [policyDraft, setPolicyDraft] = React.useState({ sponsorApprovalRequired: true, allowAnyOneOptionalApprover: false, allowSequentialApprovers: true });
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit) && canMutate && !effectiveReadOnly);

  const taskDefinitions = useWorkflowDefinitions({ domain: "Task", isActive: "true" });
  const riskDefinitions = useWorkflowDefinitions({ domain: "Risk", isActive: "true" });
  const financeDefinitions = useWorkflowDefinitions({ domain: "Finance", isActive: "true" });
  const changeRequestDefinitions = useWorkflowDefinitions({ domain: "ChangeRequest", isActive: "true" });
  const definitions = {
    Task: taskDefinitions.data?.filter((item) => Boolean(item.activePublishedVersionId)) ?? [],
    Risk: riskDefinitions.data?.filter((item) => Boolean(item.activePublishedVersionId)) ?? [],
    Finance: financeDefinitions.data?.filter((item) => Boolean(item.activePublishedVersionId)) ?? [],
    ChangeRequest: changeRequestDefinitions.data?.filter((item) => Boolean(item.activePublishedVersionId)) ?? [],
  };
  const labels: Record<WorkflowDomain, string> = {
    Task: t("workflows.domainTask"),
    Risk: t("workflows.domainRisk"),
    Finance: t("workflows.domainFinance"),
    ChangeRequest: t("workflows.domainChangeRequest"),
  };

  const currentPolicy = settings.data?.domains.find((item) => item.domain === "ChangeRequest");
  React.useEffect(() => {
    if (!currentPolicy) return;
    setPolicyDraft({
      sponsorApprovalRequired: currentPolicy.sponsorApprovalRequired,
      allowAnyOneOptionalApprover: currentPolicy.allowAnyOneOptionalApprover,
      allowSequentialApprovers: currentPolicy.allowSequentialApprovers,
    });
  }, [currentPolicy]);

  const save = async (domain: WorkflowDomain, mode: "Standard" | "Custom", workflowDefinitionId?: string, rowVersion?: WorkflowRowVersion, policy?: typeof policyDraft) => {
    setError(null);
    const current = settings.data?.domains.find((item) => item.domain === domain);
    const changedWorkflow = current?.mode === "Custom" && (mode === "Standard" || current.workflowDefinitionId !== workflowDefinitionId);
    if (changedWorkflow && !await confirm({ description: t("workflows.confirmConfigurationChange") })) return;
    try {
      await mutations.saveProjectConfiguration.mutateAsync({
        projectId,
        domain,
        body: { mode, workflowDefinitionId: mode === "Custom" ? workflowDefinitionId : undefined, rowVersion, ...(domain === "ChangeRequest" ? policyDraft : {}), ...policy },
      });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("workflows.saveConfigurationError"));
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("projects.workflowSettings")}
        description={t("projects.workflowSettingsDescription")}
        actions={effectiveReadOnly ? <span className="rounded-full border px-2.5 py-0.5 text-xs font-semibold text-muted-foreground">{t("workflows.readOnlyView")}</span> : null}
      />
      {error ? <div role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div> : null}
      {settings.isError ? (
        <div role="alert" className="flex flex-col items-center gap-3 rounded-lg border border-destructive/30 p-6 text-center text-sm text-destructive">
          <AlertTriangle className="h-6 w-6" />
          <p>{t("common.errorLoading")}</p>
          <Button variant="outline" size="sm" onClick={() => void settings.refetch()}>{t("common.retry")}</Button>
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-4">
          {DOMAINS.map((domain) => {
            const current = settings.data?.domains.find((item) => item.domain === domain);
            const options = definitions[domain];
            const mode = current?.mode ?? "Standard";

            return (
              <Card key={domain} className="flex flex-col">
                <CardHeader>
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <CardTitle className="text-base">{labels[domain]}</CardTitle>
                      <CardDescription className="mt-1">{mode === "Custom" ? t("workflows.customWorkflow") : t("workflows.standardBehavior")}</CardDescription>
                    </div>
                    <Settings2 className="h-4 w-4 text-muted-foreground" />
                  </div>
                </CardHeader>
                <CardContent className="flex flex-1 flex-col gap-4">
                  <select
                    className="h-10 rounded-md border border-input bg-background px-3 text-sm"
                    value={mode === "Custom" ? current?.workflowDefinitionId ?? "" : "Standard"}
                    disabled={!canManage || settings.isLoading || mutations.saveProjectConfiguration.isPending}
                    aria-label={labels[domain]}
                    onChange={(event) => {
                      const value = event.target.value;
                      if (value === "Standard") void save(domain, "Standard", undefined, current?.rowVersion);
                      else if (value) void save(domain, "Custom", value, current?.rowVersion);
                    }}
                  >
                    <option value="Standard">{t("workflows.standardBehavior")}</option>
                    {mode === "Custom" && !current?.workflowDefinitionId ? <option value="" disabled>{t("workflows.selectPublishedVersion")}</option> : null}
                    {options.map((item) => <option key={item.id} value={item.id}>{item.name} · v{item.activePublishedVersionNumber}</option>)}
                  </select>
                  {mode === "Custom" && !current?.workflowDefinitionId ? <p className="text-xs text-destructive">{t("workflows.selectPublishedVersion")}</p>
                    : mode === "Custom" ? <p className="text-xs text-muted-foreground">{current?.workflowDefinitionName ?? current?.workflowDefinitionId} · v{current?.effectiveWorkflowVersionNumber ?? "—"}</p>
                      : <p className="text-xs text-muted-foreground">{t("workflows.noPublishedWorkflows")}</p>}
                  {domain === "ChangeRequest" ? <div className="space-y-3 rounded-md border border-border/70 p-3">
                    <p className="text-xs text-muted-foreground">{t("workflows.changeRequestPolicyHint")}</p>
                    {([
                      ["sponsorApprovalRequired", "projects.sponsorApprovalRequired"],
                      ["allowAnyOneOptionalApprover", "projects.allowAnyOneOptionalApprover"],
                      ["allowSequentialApprovers", "projects.allowSequentialApprovers"],
                    ] as const).map(([key, label]) => <label key={key} className="flex items-start gap-2 text-sm">
                      <input type="checkbox" className="mt-1 accent-primary" checked={policyDraft[key]} disabled={!canManage || settings.isLoading || mutations.saveProjectConfiguration.isPending}
                        onChange={(event) => setPolicyDraft((currentValue) => ({ ...currentValue, [key]: event.target.checked }))} />
                      <span>{t(label)}</span>
                    </label>)}
                    <Button type="button" size="sm" variant="outline" disabled={!canManage || settings.isLoading || mutations.saveProjectConfiguration.isPending}
                      onClick={() => void save("ChangeRequest", mode, current?.workflowDefinitionId, current?.rowVersion, policyDraft)}>
                      {t("projects.savePolicy")}
                    </Button>
                  </div> : null}
                  <div className="mt-auto space-y-1 border-t pt-3 text-xs text-muted-foreground">
                    <p className="flex items-center gap-1.5"><CheckCircle2 className="h-3.5 w-3.5 text-success" />{t("workflows.existingRecordsUnchanged")}</p>
                    <p>{t("workflows.newRecordsUseConfiguration")}</p>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
