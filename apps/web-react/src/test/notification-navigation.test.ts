import { describe, expect, it } from "vitest";
import { resolveNotificationPath } from "../lib/notification-navigation";

describe("notification navigation", () => {
  it("maps projects only from their public identifier", () => {
    const internalId = "53f762a8-cd25-4a3b-b821-f9897c1a14e0";
    const publicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    expect(resolveNotificationPath("Project", internalId)).toBeNull();
    expect(resolveNotificationPath("Project", internalId, publicId)).toBe(`/projects/${publicId}`);
    expect(resolveNotificationPath("Project", internalId, "00000000-0000-0000-0000-000000000000")).toBeNull();
  });

  it("keeps supported non-project entity routes", () => {
    const id = "53f762a8-cd25-4a3b-b821-f9897c1a14e0";
    expect(resolveNotificationPath("Program", id)).toBe(`/portfolios/programs/${id}`);
    expect(resolveNotificationPath("Portfolio", id)).toBe(`/portfolios/${id}`);
  });

  it("routes every supported approval notification to its assignment-scoped review page", () => {
    const id = "53f762a8-cd25-4a3b-b821-f9897c1a14e0";
    expect(resolveNotificationPath("ProjectHandoverRequest", id)).toBe(`/my-work/approvals/review/project-handover/${id}`);
    expect(resolveNotificationPath("ProgramHandoverRequest", id)).toBe(`/my-work/approvals/review/program-handover/${id}`);
    expect(resolveNotificationPath("PortfolioHandoverRequest", id)).toBe(`/my-work/approvals/review/portfolio-handover/${id}`);
    expect(resolveNotificationPath("ProjectChangeRequest", id)).toBe(`/my-work/approvals/review/change-request/${id}`);
  });

  it("rejects external, unknown, and malformed destinations", () => {
    expect(resolveNotificationPath("https://example.com", "53f762a8-cd25-4a3b-b821-f9897c1a14e0")).toBeNull();
    expect(resolveNotificationPath("project", "not-a-guid")).toBeNull();
    expect(resolveNotificationPath("task", "53f762a8-cd25-4a3b-b821-f9897c1a14e0")).toBeNull();
  });
});
