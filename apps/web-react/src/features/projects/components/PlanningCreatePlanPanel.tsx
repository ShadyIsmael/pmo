import * as React from "react";
import { Plus } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import type { CreatePlanInput, PlanSummaryDto, PlanType } from "../api/planningFull";

const PLAN_TYPES: PlanType[] = ["Execution", "Recovery", "Tender", "Construction", "Shutdown", "Scenario", "Custom"];

type CreatePlanMutation = {
  mutateAsync: (input: CreatePlanInput) => Promise<PlanSummaryDto>;
  isPending: boolean;
  error: unknown;
};

export function PlanningCreatePlanPanel({
  mutation,
  t,
  onCreated,
  onCancel,
}: {
  mutation: CreatePlanMutation;
  t: (key: string) => string;
  onCreated: (planId: string) => void;
  onCancel: () => void;
}) {
  const [planType, setPlanType] = React.useState<PlanType>("Execution");
  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmedName = name.trim();
    if (mutation.isPending || !trimmedName) return;
    try {
      const plan = await mutation.mutateAsync({ planType, name: trimmedName, description: description.trim() || undefined });
      onCreated(plan.id);
    } catch {
      // The mutation state supplies the error shown in this panel.
    }
  }

  const errorMessage = mutation.error instanceof Error && mutation.error.message.trim()
    ? mutation.error.message
    : t("projects.planning.createPlanFailed");

  return (
    <form aria-label={t("projects.planning.createPlan")} className="grid gap-3 rounded border bg-muted/20 p-4" onSubmit={(event) => void submit(event)}>
      <div>
        <h3 className="text-sm font-semibold">{t("projects.planning.createPlan")}</h3>
        <p className="mt-1 text-xs text-muted-foreground">{t("projects.planning.createPlanDescription")}</p>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="planning-plan-type">{t("projects.planning.planType")}</Label>
          <Select value={planType} onValueChange={(value) => setPlanType(value as PlanType)}>
            <SelectTrigger id="planning-plan-type"><SelectValue /></SelectTrigger>
            <SelectContent>{PLAN_TYPES.map((value) => <SelectItem key={value} value={value}>{t(`enums.planning.planType.${value}`)}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="planning-plan-name">{t("projects.planning.planName")}</Label>
          <Input id="planning-plan-name" value={name} onChange={(event) => setName(event.target.value)} placeholder={t("projects.planning.planNamePlaceholder")} required />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="planning-plan-description">{t("projects.planning.planDescription")}</Label>
        <Input id="planning-plan-description" value={description} onChange={(event) => setDescription(event.target.value)} placeholder={t("projects.planning.planDescriptionPlaceholder")} />
      </div>
      {mutation.error ? <p role="alert" className="text-xs text-destructive">{errorMessage}</p> : null}
      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending || !name.trim()}>
          <Plus className="me-1.5 h-4 w-4" />{mutation.isPending ? t("projects.planning.creatingPlan") : t("projects.planning.createPlan")}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={mutation.isPending}>{t("projects.planning.cancelCreatePlan")}</Button>
      </div>
    </form>
  );
}
