import { describe, expect, it } from "vitest";
import { getApprovalReviewApiPath } from "../lib/approval-navigation";

describe("approval review API paths", () => {
  it("targets the project handover review route exposed by the API", () => {
    expect(getApprovalReviewApiPath("project-handover", "11111111-1111-1111-1111-111111111111"))
      .toBe("/api/handover-requests/11111111-1111-1111-1111-111111111111/review");
  });
});
