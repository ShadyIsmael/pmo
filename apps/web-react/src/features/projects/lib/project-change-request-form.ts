import type { CreateProjectChangeRequestDto } from "../api/projects";

export type ProjectChangeRequestFormError =
  | "changeReasonRequired"
  | "budgetChangeAmountInvalid"
  | "budgetChangeAmountZero"
  | "budgetChangeAmountPrecision"
  | "budgetChangeAmountCeilingRequired"
  | "budgetChangeAmountResultNegative"
  | "budgetChangeAmountJustificationRequired";

export type ProjectChangeRequestFormResult =
  | { ok: true; value: CreateProjectChangeRequestDto }
  | { ok: false; error: ProjectChangeRequestFormError };

export function buildProjectChangeRequestInput(
  projectId: string,
  formData: FormData,
  projectBudget?: number | null,
  approvals: { strategy: string; approverUserIds: string[] } = { strategy: "AllRequired", approverUserIds: [] },
): ProjectChangeRequestFormResult {
  const reason = String(formData.get("reason") ?? "").trim();
  if (!reason) return { ok: false, error: "changeReasonRequired" };

  const typeRaw = String(formData.get("type") ?? "Administrative");
  const directionRaw = String(formData.get("budgetDirection") ?? "").trim();
  const magnitudeText = String(formData.get("budgetChangeAmount") ?? "").trim();

  // A provided financial amount always denotes a Budget Change request, even if the
  // caller did not explicitly pick the BudgetChange type (legacy/backward compatible).
  const hasAmountInput = magnitudeText !== "" || directionRaw === "increase" || directionRaw === "decrease";
  let type: "Administrative" | "Baseline" | "BudgetChange";
  if (typeRaw === "BudgetChange") type = "BudgetChange";
  else if (typeRaw === "Baseline" && !hasAmountInput) type = "Baseline";
  else if (typeRaw === "Baseline") type = "BudgetChange";
  else type = hasAmountInput ? "BudgetChange" : "Administrative";

  let budgetChangeAmount: number | null = null;
  if (type === "BudgetChange") {
    let signed: number;
    if (directionRaw === "increase" || directionRaw === "decrease") {
      if (!magnitudeText) return { ok: false, error: "budgetChangeAmountInvalid" };
      const mag = Number(magnitudeText);
      if (!Number.isFinite(mag)) return { ok: false, error: "budgetChangeAmountInvalid" };
      signed = directionRaw === "decrease" ? -mag : mag;
    } else {
      // backward compat: signed value provided
      if (!magnitudeText) return { ok: false, error: "budgetChangeAmountInvalid" };
      signed = Number(magnitudeText);
      if (!Number.isFinite(signed)) return { ok: false, error: "budgetChangeAmountInvalid" };
    }
    const textForPrecision = magnitudeText;
    if (signed === 0) return { ok: false, error: "budgetChangeAmountZero" };
    if (!Number.isFinite(projectBudget)) return { ok: false, error: "budgetChangeAmountCeilingRequired" };
    if (hasMoreThanTwoDecimalPlaces(textForPrecision)) return { ok: false, error: "budgetChangeAmountPrecision" };
    if ((projectBudget as number) + signed < 0) return { ok: false, error: "budgetChangeAmountResultNegative" };
    if (!String(formData.get("justification") ?? "").trim()) {
      return { ok: false, error: "budgetChangeAmountJustificationRequired" };
    }
    budgetChangeAmount = signed;
  }

  const optionalText = (name: string) => String(formData.get(name) ?? "").trim() || null;

  return {
    ok: true,
    value: {
      projectId,
      type,
      reason,
      budgetChangeAmount,
      justification: optionalText("justification"),
      impact: optionalText("impact"),
      attachments: optionalText("attachments"),
      approvalStrategy: approvals.strategy,
      approverUserIds: approvals.approverUserIds,
    },
  };
}

function hasMoreThanTwoDecimalPlaces(value: string): boolean {
  const [coefficient, exponentText] = value.toLowerCase().split("e");
  const [whole, fraction = ""] = coefficient.split(".");
  const exponent = exponentText === undefined ? 0 : Number(exponentText);
  const significantDigits = `${whole}${fraction}`.replace(/^[+-]?0+/, "");
  const trailingZeros = significantDigits.match(/0+$/)?.[0].length ?? 0;
  return fraction.length - exponent - trailingZeros > 2;
}
