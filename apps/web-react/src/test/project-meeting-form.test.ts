import { describe, expect, it } from "vitest";
import {
  buildMeetingUpdateBody,
  createMeetingDraft,
  isValidMeetingEmail,
  meetingToDraft,
  toMeetingInput,
  validateMeetingDraft,
  type MeetingDraft,
} from "@features/delivery/meeting-form";
import type { MeetingDetailDto } from "@api/meetingsFull";

const validDraft = (): MeetingDraft => ({
  title: "Quarterly review",
  objective: "Review the delivery plan",
  startTime: "2026-10-10T10:00",
  endTime: "2026-10-10T11:00",
  location: "Room 4",
  meetingLink: "https://meet.example.test/room-4",
  reminderOffsetMinutes: "30",
  organizerId: "organizer-1",
  attendees: [{ userId: "user-1", email: "user@example.test", displayName: "User One" }],
});

const meetingDetail = (): MeetingDetailDto => ({
  id: "meeting-1",
  title: "Quarterly review",
  objective: "Review delivery progress",
  projectId: "project-1",
  projectName: "Core modernization",
  organizerId: "organizer-1",
  organizerName: "Organizer One",
  startTime: "2026-10-10T10:00:00.000Z",
  endTime: "2026-10-10T11:00:00.000Z",
  location: "Room 4",
  meetingLink: "https://meet.example.test/room-4",
  status: "Scheduled",
  attendeeCount: 2,
  reminderOffsetMinutes: 30,
  minutesState: "Draft",
  rowVersion: "version-1",
  attendees: [
    { id: "attendee-1", meetingId: "meeting-1", userId: "user-1", displayName: "Internal User", email: "user@example.test", isInternalUser: true },
    { id: "attendee-2", meetingId: "meeting-1", displayName: "External User", email: "guest@example.test", isInternalUser: false },
  ],
});

describe("project meeting form", () => {
  it("defaults the meeting to a future one-hour time window", () => {
    const now = new Date(2026, 9, 7, 9, 30);
    const draft = createMeetingDraft(now);
    expect(new Date(draft.startTime).getTime()).toBe(now.getTime() + 60 * 60 * 1000);
    expect(new Date(draft.endTime).getTime()).toBe(now.getTime() + 2 * 60 * 60 * 1000);
  });

  it("requires an organizer and an end time after the start", () => {
    const draft = validDraft();
    expect(validateMeetingDraft({ ...draft, organizerId: "" })).toBe("organizerRequired");
    expect(validateMeetingDraft({ ...draft, endTime: draft.startTime })).toBe("endAfterStart");
  });

  it("rejects invalid and duplicate external attendees", () => {
    expect(isValidMeetingEmail("not-an-email")).toBe(false);
    expect(validateMeetingDraft({ ...validDraft(), attendees: [{ email: "nope" }] })).toBe("emailInvalid");
    expect(validateMeetingDraft({ ...validDraft(), attendees: [{ email: "guest@example.test" }, { email: " GUEST@example.test " }] })).toBe("duplicateAttendees");
  });

  it("builds the API payload with optional fields, attendees, and reminder", () => {
    const draft = validDraft();
    const body = toMeetingInput(draft, "project-1");
    expect(body).toMatchObject({
      title: "Quarterly review",
      objective: "Review the delivery plan",
      projectId: "project-1",
      location: "Room 4",
      meetingLink: "https://meet.example.test/room-4",
      reminderOffsetMinutes: 30,
      organizerId: "organizer-1",
      attendees: [{ userId: "user-1" }],
    });
    expect(body.startTime).toBe(new Date(draft.startTime).toISOString());
  });

  it("leaves project association empty when a standalone meeting has no project", () => {
    expect(toMeetingInput(validDraft()).projectId).toBeUndefined();
  });

  it("hydrates every editable field and attendee type from an existing meeting", () => {
    const meeting = meetingDetail();
    const draft = meetingToDraft(meeting);
    expect(draft).toMatchObject({
      title: meeting.title,
      objective: meeting.objective,
      location: meeting.location,
      meetingLink: meeting.meetingLink,
      reminderOffsetMinutes: "30",
      organizerId: meeting.organizerId,
      attendees: [
        { userId: "user-1", email: "user@example.test", displayName: "Internal User" },
        { email: "guest@example.test", displayName: "External User" },
      ],
    });
    expect(new Date(draft.startTime).getTime()).toBe(new Date(meeting.startTime).getTime());
    expect(new Date(draft.endTime).getTime()).toBe(new Date(meeting.endTime).getTime());
  });

  it("preserves project, reminder, attendee, concurrency, and status fields in detail actions", () => {
    const meeting = meetingDetail();
    const body = buildMeetingUpdateBody(meeting, "Completed");
    expect(body).toMatchObject({
      title: meeting.title,
      projectId: "project-1",
      reminderOffsetMinutes: 30,
      organizerId: "organizer-1",
      attendees: [{ userId: "user-1" }, { email: "guest@example.test", displayName: "External User" }],
      rowVersion: "version-1",
      status: "Completed",
    });
  });

  it("refuses a status update if the meeting has no organizer identifier", () => {
    expect(buildMeetingUpdateBody({ ...meetingDetail(), organizerId: null })).toBeNull();
  });
});
