import { describe, expect, it } from "vitest";
import { getAnnualBudgetProjectReviewPath, getAnnualBudgetProjectSectionPath, getApprovalReviewApiPath } from "../lib/approval-navigation";

describe("approval review API paths", () => {
  it("targets the project handover review route exposed by the API", () => {
    expect(getApprovalReviewApiPath("project-handover", "11111111-1111-1111-1111-111111111111"))
      .toBe("/api/handover-requests/11111111-1111-1111-1111-111111111111/review");
  });

  it("routes an assigned annual budget to its public project and fiscal year", () => {
    expect(getAnnualBudgetProjectSectionPath("11111111-1111-4111-8111-111111111111"))
      .toBe("/projects/11111111-1111-4111-8111-111111111111/financials?section=annualBudgets");
    expect(getAnnualBudgetProjectReviewPath("11111111-1111-4111-8111-111111111111", 2026))
      .toBe("/projects/11111111-1111-4111-8111-111111111111/financials?section=annualBudgets&fiscalYear=2026");
    expect(getAnnualBudgetProjectReviewPath("not-a-public-id", 2026)).toBeNull();
    expect(getAnnualBudgetProjectReviewPath("11111111-1111-4111-8111-111111111111", 0)).toBeNull();
  });
});
