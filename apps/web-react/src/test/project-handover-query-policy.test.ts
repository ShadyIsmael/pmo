import { describe, expect, it } from "vitest";
import { ApiError } from "../api/client";
import { shouldRetryHandoverQuery } from "../features/projects/api/projects";

describe("Handover permission query retry policy", () => {
  it.each([401, 403, 404])("does not retry authorization-masked HTTP %i", (status) => {
    expect(shouldRetryHandoverQuery(0, new ApiError(status, "Unavailable"))).toBe(false);
  });

  it("retries one transient non-permission failure", () => {
    expect(shouldRetryHandoverQuery(0, new Error("Network unavailable"))).toBe(true);
    expect(shouldRetryHandoverQuery(1, new Error("Network unavailable"))).toBe(false);
  });
});
