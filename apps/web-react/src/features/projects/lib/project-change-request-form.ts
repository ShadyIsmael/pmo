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

  const budgetChangeText = String(formData.get("budgetChangeAmount") ?? "").trim();
  const budgetChangeAmount = budgetChangeText ? Number(budgetChangeText) : null;
  if (budgetChangeAmount !== null && !Number.isFinite(budgetChangeAmount)) {
    return { ok: false, error: "budgetChangeAmountInvalid" };
  }
  if (budgetChangeAmount !== null) {
    if (budgetChangeAmount === 0) return { ok: false, error: "budgetChangeAmountZero" };
    if (!Number.isFinite(projectBudget)) return { ok: false, error: "budgetChangeAmountCeilingRequired" };
    if (hasMoreThanTwoDecimalPlaces(budgetChangeText)) return { ok: false, error: "budgetChangeAmountPrecision" };
    if ((projectBudget as number) + budgetChangeAmount < 0) return { ok: false, error: "budgetChangeAmountResultNegative" };
    if (!String(formData.get("justification") ?? "").trim()) {
      return { ok: false, error: "budgetChangeAmountJustificationRequired" };
    }
  }

  const optionalText = (name: string) => String(formData.get(name) ?? "").trim() || null;
  const type = String(formData.get("type") ?? "Administrative");

  return {
    ok: true,
    value: {
      projectId,
      type: type === "Baseline" ? "Baseline" : "Administrative",
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
