import * as React from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";
import { useProjectWorkflowSettings, useWorkflowDefinitions, useWorkflowMutations, type WorkflowDomain } from "@features/workflow/api/workflows";

const DOMAINS: WorkflowDomain[] = ["Task", "Risk", "Finance"];

/** Project → Settings → Workflows. PM controls Standard vs Custom published workflow per domain. Existing instances stay version-bound. */
export function ProjectWorkflowSettings({ projectId }: { projectId: string }) {
  const { t } = useI18n();
  const settings = useProjectWorkflowSettings(projectId);
  const mutations = useWorkflowMutations();
  const [domain, setDomain] = React.useState<WorkflowDomain>("Task");
  // Load published matching workflows on demand when the picker opens.
  const [pickerOpen, setPickerOpen] = React.useState(false);
  const published = useWorkflowDefinitions(pickerOpen ? { domain, isActive: "true" } : { domain: "__none__" });
  const current = settings.data?.settings.find((s) => s.domain === domain);

  return (
    <Card>
      <CardHeader><CardTitle>{t("projects.workflowSettings")}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <div className="flex gap-2">
          {DOMAINS.map((d) => (
            <Button key={d} size="sm" variant={domain === d ? "default" : "outline"} onClick={() => setDomain(d)}>{d}</Button>
          ))}
        </div>
        {settings.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
          : (
            <div className="space-y-2 text-sm">
              <p>{t("workflows.currentMode")}: <strong>{current?.mode ?? "Standard"}</strong></p>
              {current?.workflowDefinitionId ? <p className="text-muted-foreground">{current.workflowDefinitionId}</p> : null}
              <div className="flex gap-2">
                <Button size="sm" variant="outline" onClick={() => mutations.saveProjectConfiguration.mutate({ projectId, domain, body: { mode: "Standard" } })}>{t("workflows.useStandard")}</Button>
                <Button size="sm" variant="outline" onClick={() => setPickerOpen((v) => !v)}>{t("workflows.chooseCustom")}</Button>
              </div>
              {pickerOpen ? (
                <select
                  className="input-field"
                  value=""
                  onChange={(e) => {
                    if (!e.target.value) return;
                    mutations.saveProjectConfiguration.mutate({ projectId, domain, body: { mode: "Custom", workflowDefinitionId: e.target.value } }, { onSuccess: () => setPickerOpen(false) });
                  }}
                  aria-label={t("workflows.chooseCustom")}
                >
                  <option value="">{t("workflows.selectWorkflow")}</option>
                  {(published.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name} ({d.code})</option>)}
                </select>
              ) : null}
            </div>
          )}
      </CardContent>
    </Card>
  );
}
