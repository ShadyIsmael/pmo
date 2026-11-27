import { describe, expect, it } from "vitest";
import { buildProjectChangeRequestInput } from "../features/projects/lib/project-change-request-form";

describe("buildProjectChangeRequestInput", () => {
  it("requires a non-whitespace reason", () => {
    const formData = new FormData();
    formData.set("reason", "   \n  ");

    expect(buildProjectChangeRequestInput("p1", formData)).toEqual({ ok: false, error: "changeReasonRequired" });
  });

  it("trims optional fields and includes attachments and budget", () => {
    const formData = new FormData();
    formData.set("type", "Baseline");
    formData.set("reason", "  Update delivery baseline  ");
    formData.set("budgetChangeAmount", "125.50");
    formData.set("justification", "  Approved scope adjustment  ");
    formData.set("impact", "  Schedule remains unchanged  ");
    formData.set("attachments", "  baseline-review.pdf  ");

    expect(buildProjectChangeRequestInput("p1", formData, 1000)).toEqual({
      ok: true,
      value: {
        projectId: "p1",
        type: "BudgetChange",
        reason: "Update delivery baseline",
        budgetChangeAmount: 125.5,
        justification: "Approved scope adjustment",
        impact: "Schedule remains unchanged",
        attachments: "baseline-review.pdf",
        approvalStrategy: "AllRequired",
        approverUserIds: [],
      },
    });
  });

  it("treats blank budget as null and rejects non-finite values", () => {
    const blankBudget = new FormData();
    blankBudget.set("reason", "Administrative update");
    blankBudget.set("budgetChangeAmount", "  ");
    expect(buildProjectChangeRequestInput("p1", blankBudget)).toMatchObject({ ok: true, value: { budgetChangeAmount: null } });

    const invalidBudget = new FormData();
    invalidBudget.set("reason", "Administrative update");
    invalidBudget.set("budgetChangeAmount", "not-a-number");
    expect(buildProjectChangeRequestInput("p1", invalidBudget)).toEqual({ ok: false, error: "budgetChangeAmountInvalid" });
  });

  it("prevents financial change requests that the API would reject", () => {
    const makeForm = (amount: string, justification = "Valid reason") => {
      const formData = new FormData();
      formData.set("reason", "Budget update");
      formData.set("budgetChangeAmount", amount);
      formData.set("justification", justification);
      return formData;
    };

    expect(buildProjectChangeRequestInput("p1", makeForm("10"))).toEqual({ ok: false, error: "budgetChangeAmountCeilingRequired" });
    expect(buildProjectChangeRequestInput("p1", makeForm("0"), 100)).toEqual({ ok: false, error: "budgetChangeAmountZero" });
    expect(buildProjectChangeRequestInput("p1", makeForm("1.001"), 100)).toEqual({ ok: false, error: "budgetChangeAmountPrecision" });
    expect(buildProjectChangeRequestInput("p1", makeForm("100e-3"), 100)).toEqual({ ok: true, value: expect.objectContaining({ budgetChangeAmount: 0.1 }) });
    expect(buildProjectChangeRequestInput("p1", makeForm("-100.01"), 100)).toEqual({ ok: false, error: "budgetChangeAmountResultNegative" });
    expect(buildProjectChangeRequestInput("p1", makeForm("10", "  "), 100)).toEqual({ ok: false, error: "budgetChangeAmountJustificationRequired" });

    expect(buildProjectChangeRequestInput("p1", makeForm("25.50"), 100)).toMatchObject({
      ok: true,
      value: { budgetChangeAmount: 25.5, justification: "Valid reason" },
    });
  });
});
