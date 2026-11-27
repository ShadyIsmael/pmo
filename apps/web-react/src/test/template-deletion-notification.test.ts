import { describe, expect, it } from "vitest";
import { getLocalizedNotificationContent } from "../lib/notification-content";
import { resolveNotificationPath } from "../lib/notification-navigation";

const requestId = "10000000-0000-4000-8000-000000000001";

describe("template deletion notifications", () => {
  it("localizes request notifications and routes them to My Approvals", () => {
    const notification = { type: "TemplateDeletionRequested", title: "Safety Handbook", body: "Replaced by the current standard" };

    expect(getLocalizedNotificationContent(notification, "en")).toEqual({
      title: "Template deletion requested: Safety Handbook",
      body: "Reason: Replaced by the current standard",
    });
    expect(getLocalizedNotificationContent(notification, "ar")).toEqual({
      title: "طلب حذف القالب: Safety Handbook",
      body: "السبب: Replaced by the current standard",
    });
    expect(resolveNotificationPath("TemplateDeletionRequest", requestId, null, notification.type))
      .toBe(`/my-work/approvals?templateDeletionRequestId=${requestId}`);
  });

  it("localizes approval and rejection updates using the decision comment", () => {
    expect(getLocalizedNotificationContent({ type: "TemplateDeletionApproved", title: "Safety Handbook", body: "All existing records were retained." }, "en"))
      .toEqual({ title: "Template deletion approved: Safety Handbook", body: "Decision: All existing records were retained." });
    expect(getLocalizedNotificationContent({ type: "TemplateDeletionRejected", title: "Safety Handbook", body: "The template is still required." }, "ar"))
      .toEqual({ title: "تم رفض حذف القالب: Safety Handbook", body: "القرار: The template is still required." });
  });

  it("rejects malformed request links", () => {
    expect(resolveNotificationPath("TemplateDeletionRequest", "not-a-guid", null, "TemplateDeletionRejected")).toBeNull();
  });
});
