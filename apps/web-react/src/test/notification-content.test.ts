import { describe, expect, it } from "vitest";
import { getLocalizedNotificationContent } from "../lib/notification-content";

const meetingNotificationTitles = [
  ["MeetingInvitation", "Meeting Invitation", "\u062f\u0639\u0648\u0629 \u0644\u062d\u0636\u0648\u0631 \u0627\u062c\u062a\u0645\u0627\u0639"],
  ["MeetingReminder", "Upcoming Meeting", "\u062a\u0630\u0643\u064a\u0631 \u0628\u0627\u062c\u062a\u0645\u0627\u0639 \u0642\u0627\u062f\u0645"],
  ["MeetingStarted", "Meeting Started", "\u0628\u062f\u0623 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639"],
  ["MeetingMinutesPublished", "Meeting Minutes Available", "\u0645\u062d\u0636\u0631 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639 \u0645\u062a\u0627\u062d"],
  ["MeetingMinutesComment", "New Comment on Meeting Minutes", "\u062a\u0639\u0644\u064a\u0642 \u062c\u062f\u064a\u062f \u0639\u0644\u0649 \u0645\u062d\u0636\u0631 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639"],
  ["MeetingMinutesCommentReply", "New Reply on Meeting Minutes", "\u0631\u062f \u062c\u062f\u064a\u062f \u0639\u0644\u0649 \u062a\u0639\u0644\u064a\u0642 \u0628\u0645\u062d\u0636\u0631 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639"],
] as const;

describe("localized notification content", () => {
  it.each(meetingNotificationTitles)("renders bilingual title and body for %s", (type, englishTitle, arabicTitle) => {
    const notification = {
      type,
      title: `${englishTitle}\n${arabicTitle}`,
      body: `English body for ${type}.\n\nArabic body for ${type}.`,
    };

    expect(getLocalizedNotificationContent(notification, "en")).toEqual({
      title: englishTitle,
      body: `English body for ${type}.`,
    });
    expect(getLocalizedNotificationContent(notification, "ar")).toEqual({
      title: arabicTitle,
      body: `Arabic body for ${type}.`,
    });
  });

  it("keeps generic and existing completion notifications unchanged", () => {
    expect(getLocalizedNotificationContent({ type: "ProjectHandover", title: "Plan handed over", body: "Review the plan." }, "ar"))
      .toEqual({ title: "Plan handed over", body: "Review the plan." });
    expect(getLocalizedNotificationContent({ type: "MeetingCompleted", title: "Meeting completed\n\u062a\u0645 \u0625\u0646\u0647\u0627\u0621 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639", body: "Completion details." }, "ar"))
      .toEqual({ title: "Meeting completed\n\u062a\u0645 \u0625\u0646\u0647\u0627\u0621 \u0627\u0644\u0627\u062c\u062a\u0645\u0627\u0639", body: "Completion details." });
  });
});
