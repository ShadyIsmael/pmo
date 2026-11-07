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

  it("routes Meetings notifications by the meeting public identifier", () => {
    const internalId = "53f762a8-cd25-4a3b-b821-f9897c1a14e0";
    const meetingPublicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    expect(resolveNotificationPath("Meeting", internalId, meetingPublicId)).toBe(`/meetings/${meetingPublicId}`);
    expect(resolveNotificationPath("Meeting", meetingPublicId)).toBe(`/meetings/${meetingPublicId}`);
    expect(resolveNotificationPath("Meeting", null)).toBeNull();
  });

  it("routes Minutes comment and reply notifications to their public deep-link targets", () => {
    const meetingPublicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    const commentPublicId = "a69b24b6-f0c2-4d10-8b60-5bc9f07a6c0e";
    const replyPublicId = "5eeaa771-8681-4f8a-b1f7-60c857e23360";
    expect(resolveNotificationPath("MeetingMinutesComment", meetingPublicId, commentPublicId))
      .toBe(`/meetings/${meetingPublicId}?tab=minutes&comment=${commentPublicId}`);
    expect(resolveNotificationPath("MeetingMinutesCommentReply", meetingPublicId, replyPublicId))
      .toBe(`/meetings/${meetingPublicId}?tab=minutes&reply=${replyPublicId}`);
    expect(resolveNotificationPath("MeetingMinutesComment", "bad-meeting-id", commentPublicId)).toBeNull();
    expect(resolveNotificationPath("MeetingMinutesCommentReply", meetingPublicId, "bad-reply-id")).toBeNull();
  });

  it("uses notification event type for Meeting and Minutes targets when the reference type is Meeting", () => {
    const meetingPublicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    const commentPublicId = "a69b24b6-f0c2-4d10-8b60-5bc9f07a6c0e";
    const replyPublicId = "5eeaa771-8681-4f8a-b1f7-60c857e23360";

    expect(resolveNotificationPath("Meeting", meetingPublicId, null, "MeetingInvitation"))
      .toBe(`/meetings/${meetingPublicId}`);
    expect(resolveNotificationPath("Meeting", meetingPublicId, null, "MeetingReminder"))
      .toBe(`/meetings/${meetingPublicId}`);
    expect(resolveNotificationPath("Meeting", meetingPublicId, null, "MeetingStarted"))
      .toBe(`/meetings/${meetingPublicId}`);
    expect(resolveNotificationPath("Meeting", meetingPublicId, null, "MeetingMinutesPublished"))
      .toBe(`/meetings/${meetingPublicId}?tab=minutes`);
    expect(resolveNotificationPath("Meeting", meetingPublicId, commentPublicId, "MeetingMinutesComment"))
      .toBe(`/meetings/${meetingPublicId}?tab=minutes&comment=${commentPublicId}`);
    expect(resolveNotificationPath("Meeting", meetingPublicId, replyPublicId, "MeetingMinutesCommentReply"))
      .toBe(`/meetings/${meetingPublicId}?tab=minutes&reply=${replyPublicId}`);
    expect(resolveNotificationPath("Meeting", "not-a-public-id", commentPublicId, "MeetingMinutesComment"))
      .toBeNull();
  });

  it("routes Risk Action follow-up notifications only by the action public identifier", () => {
    const internalId = "53f762a8-cd25-4a3b-b821-f9897c1a14e0";
    const publicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    expect(resolveNotificationPath("RiskActionFollowUpDue", internalId, publicId))
      .toBe(`/risks-issues/risk-action-follow-up/${publicId}`);
    expect(resolveNotificationPath("RiskActionFollowUpDue", publicId)).toBeNull();
    expect(resolveNotificationPath("RiskActionFollowUpDue", internalId, "malformed-public-id")).toBeNull();
    expect(resolveNotificationPath("RiskActionFollowUpDue", internalId, "00000000-0000-0000-0000-000000000000")).toBeNull();
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
