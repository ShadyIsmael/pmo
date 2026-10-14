import { describe, expect, it } from "vitest";
import { resolveNotificationPath } from "../lib/notification-navigation";

describe("notification navigation", () => {
  it("maps only supported entity references to internal routes", () => {
    const id = "53f762a8-cd25-4a3b-b821-f9897c1a14e0";
    expect(resolveNotificationPath("Project", id)).toBe(`/projects/${id}`);
    expect(resolveNotificationPath("Program", id)).toBe(`/portfolios/programs/${id}`);
    expect(resolveNotificationPath("Portfolio", id)).toBe(`/portfolios/${id}`);
  });

  it("rejects external, unknown, and malformed destinations", () => {
    expect(resolveNotificationPath("https://example.com", "53f762a8-cd25-4a3b-b821-f9897c1a14e0")).toBeNull();
    expect(resolveNotificationPath("project", "not-a-guid")).toBeNull();
    expect(resolveNotificationPath("task", "53f762a8-cd25-4a3b-b821-f9897c1a14e0")).toBeNull();
  });
});
