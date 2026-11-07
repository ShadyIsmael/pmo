import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { meetingKeys, meetingsApiFull, type MeetingDetailDto } from "@api/meetingsFull";
import { resolveNotificationPath } from "@lib/notification-navigation";

const publicId = "2025488d-ae03-480d-936d-cb5f7c9c4a74";
const internalId = "aa12f90d-44fa-4a16-bcb1-d1f6d8f5806f";

afterEach(() => vi.restoreAllMocks());

describe("Meeting PublicId contract", () => {
  it("requests Meeting Details by PublicId", async () => {
    const detail: MeetingDetailDto = {
      id: internalId,
      publicId,
      title: "Delivery checkpoint",
      startTime: "2026-11-06T09:00:00Z",
      endTime: "2026-11-06T10:00:00Z",
      attendeeCount: 1,
      isRecurring: false,
      attendees: [],
      rowVersion: "v1",
    };
    const get = vi.spyOn(api, "get").mockResolvedValue(detail);

    await meetingsApiFull.detail(publicId);

    expect(get).toHaveBeenCalledWith(endpoints.meetings.detail(publicId));
    expect(get).not.toHaveBeenCalledWith(endpoints.meetings.detail(internalId));
  });

  it("keeps detail cache entries namespaced to the PublicId", () => {
    expect(meetingKeys.detail(publicId)).toEqual(["meetings", "detail", "public-id", publicId]);
    expect(meetingKeys.detail(publicId)).not.toEqual(meetingKeys.detail(internalId));
  });

  it("opens notification links through the Meeting PublicId route", () => {
    expect(resolveNotificationPath("meeting", publicId, null, "MeetingInvitation")).toBe(`/meetings/${publicId}`);
    expect(resolveNotificationPath("meeting", internalId, publicId, "MeetingMinutesPublished")).toBe(`/meetings/${publicId}?tab=minutes`);
    const commentPublicId = "a69b24b6-f0c2-4d10-8b60-5bc9f07a6c0e";
    expect(resolveNotificationPath("meetingminutescomment", publicId, commentPublicId, "MeetingMinutesComment"))
      .toBe(`/meetings/${publicId}?tab=minutes&comment=${commentPublicId}`);
  });
});
