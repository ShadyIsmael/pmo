import * as React from "react";
import { Button } from "@components/ui/Button";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import type { SaveWorkflowRule, WorkflowManagementMetadata, WorkflowTransition } from "./api/workflowsFull";
import type { useFullWorkflowMutations } from "./api/workflowsFull";

interface WorkflowRuleSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transition?: WorkflowTransition;
  entityType?: string;
  metadata?: WorkflowManagementMetadata;
  editable: boolean;
  locale: string;
  t: (key: string) => string;
  mutations: ReturnType<typeof useFullWorkflowMutations>;
}

function parseConfiguration(text: string): Record<string, unknown> {
  const value: unknown = JSON.parse(text.trim() || "{}");
  if (value === null || Array.isArray(value) || typeof value !== "object") {
    throw new Error("configurationObjectRequired");
  }
  return value as Record<string, unknown>;
}

export function WorkflowRuleSheet({ open, onOpenChange, transition, entityType, metadata, editable, locale, t, mutations }: WorkflowRuleSheetProps) {
  const [type, setType] = React.useState("");
  const [fact, setFact] = React.useState("");
  const [configuration, setConfiguration] = React.useState("{}");
  const [error, setError] = React.useState<string | null>(null);
  const facts = metadata?.entityTypes.find((item) => item.entityType === entityType)?.facts ?? [];
  const selectedFact = facts.find((item) => item.code === fact);
  const isPending = mutations.createGuard.isPending;

  React.useEffect(() => {
    if (!open) return;
    setType("");
    setFact("");
    setConfiguration("{}");
    setError(null);
  }, [open, transition]);

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editable || !transition || !type) return;
    const form = new FormData(event.currentTarget);
    let ruleConfiguration: Record<string, unknown>;

    try {
      if (type === "RequireFact") {
        const rawValue = String(form.get("expectedValue") ?? "").trim();
        if (!selectedFact || !rawValue) {
          setError(t("workflows.ruleValueRequired"));
          return;
        }
        let expectedValue: string | number | boolean = rawValue;
        if (selectedFact.valueType === "boolean") expectedValue = rawValue === "true";
        else if (selectedFact.valueType === "number") {
          const numericValue = Number(rawValue);
          if (!Number.isFinite(numericValue)) {
            setError(t("workflows.ruleNumberInvalid"));
            return;
          }
          expectedValue = numericValue;
        }
        ruleConfiguration = { fact: selectedFact.code, operator: "eq", expectedValue };
      } else if (type === "RequirePermission") {
        const permission = String(form.get("permission") ?? "").trim();
        if (!permission) { setError(t("workflows.ruleValueRequired")); return; }
        ruleConfiguration = { permission };
      } else if (type === "RequireRole") {
        const role = String(form.get("role") ?? "").trim();
        if (!role) { setError(t("workflows.ruleValueRequired")); return; }
        ruleConfiguration = { role };
      } else {
        try {
          ruleConfiguration = parseConfiguration(configuration);
        } catch {
          setError(t("workflows.ruleConfigurationInvalid"));
          return;
        }
      }
    } catch {
      setError(t("workflows.ruleConfigurationInvalid"));
      return;
    }

    setError(null);
    const body: SaveWorkflowRule = {
      guardType: type,
      configuration: ruleConfiguration,
      executionOrder: 10,
      isActive: true,
    };
    try {
      await mutations.createGuard.mutateAsync({ id: transition.id, body });
      onOpenChange(false);
    } catch (reason) {
      setError(reason instanceof Error && reason.message ? reason.message : t("workflows.ruleSaveFailed"));
    }
  }

  const knownGuard = ["RequireFact", "RequirePermission", "RequireRole"].includes(type);

  return (
    <SideSheet open={open} onOpenChange={onOpenChange} title={t("workflows.addGuard")}>
      <form onSubmit={(event) => void save(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="workflow-rule-type">{t("workflows.ruleType")}</Label>
            <select
              id="workflow-rule-type"
              className="input-field w-full"
              required
              value={type}
              disabled={!editable || isPending}
              onChange={(event) => { setType(event.target.value); setFact(""); setError(null); }}
            >
              <option value="">{t("workflows.selectType")}</option>
              {(metadata?.guardTypes ?? []).map((item) => (
                <option key={item} value={item}>{item}</option>
              ))}
            </select>
          </div>

          {type === "RequireFact" ? (
            <>
              <div className="space-y-2">
                <Label htmlFor="workflow-rule-fact">{t("workflows.fact")}</Label>
                <select
                  id="workflow-rule-fact"
                  className="input-field w-full"
                  required
                  value={fact}
                  disabled={!editable || isPending || facts.length === 0}
                  onChange={(event) => { setFact(event.target.value); setError(null); }}
                >
                  <option value="">{t("workflows.selectType")}</option>
                  {facts.map((item) => <option key={item.code} value={item.code}>{locale === "ar" ? item.nameAr : item.nameEn}</option>)}
                </select>
                {facts.length === 0 ? <p className="text-xs text-muted-foreground">{t("workflows.noFactsForEntity")}</p> : null}
              </div>
              {selectedFact?.valueType === "boolean" ? (
                <div className="space-y-2">
                  <Label htmlFor="workflow-rule-expected-value">{t("workflows.expectedValue")}</Label>
                  <select id="workflow-rule-expected-value" className="input-field w-full" name="expectedValue" required disabled={!editable || isPending} defaultValue="">
                    <option value="">{t("workflows.selectType")}</option>
                    <option value="true">true</option>
                    <option value="false">false</option>
                  </select>
                </div>
              ) : (
                <div className="space-y-2">
                  <Label htmlFor="workflow-rule-expected-value">{t("workflows.expectedValue")}</Label>
                  <input
                    id="workflow-rule-expected-value"
                    className="input-field w-full"
                    name="expectedValue"
                    type={selectedFact?.valueType === "number" ? "number" : "text"}
                    step={selectedFact?.valueType === "number" ? "any" : undefined}
                    required
                    disabled={!editable || isPending || !selectedFact}
                  />
                </div>
              )}
            </>
          ) : null}

          {type === "RequirePermission" ? (
            <div className="space-y-2">
              <Label htmlFor="workflow-rule-permission">{t("workflows.permission")}</Label>
              <input id="workflow-rule-permission" className="input-field w-full" name="permission" required disabled={!editable || isPending} />
            </div>
          ) : null}
          {type === "RequireRole" ? (
            <div className="space-y-2">
              <Label htmlFor="workflow-rule-role">{t("workflows.role")}</Label>
              <input id="workflow-rule-role" className="input-field w-full" name="role" required disabled={!editable || isPending} />
            </div>
          ) : null}
          {!knownGuard ? (
            <div className="space-y-2">
              <Label htmlFor="workflow-rule-configuration">{t("workflows.configuration")}</Label>
              <Textarea id="workflow-rule-configuration" value={configuration} disabled={!editable || isPending} onChange={(event) => setConfiguration(event.target.value)} />
            </div>
          ) : null}
          {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        </SideSheetBody>
        <SideSheetFooter>
          <Button type="button" variant="outline" disabled={isPending} onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
          {editable ? <Button type="submit" disabled={isPending || !type}>{isPending ? t("common.saving") : t("common.save")}</Button> : null}
        </SideSheetFooter>
      </form>
    </SideSheet>
  );
}
