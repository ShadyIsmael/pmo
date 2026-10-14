import type { MeetingDetailDto, MeetingInput } from "@api/meetingsFull";

export interface MeetingAttendeeDraft {
  userId?: string;
  email: string;
  displayName?: string;
}

export interface MeetingDraft {
  title: string;
  objective: string;
  startTime: string;
  endTime: string;
  location: string;
  meetingLink: string;
  reminderOffsetMinutes: string;
  organizerId: string;
  attendees: MeetingAttendeeDraft[];
}

export type MeetingDraftError =
  | "titleRequired"
  | "titleTooLong"
  | "startRequired"
  | "endRequired"
  | "organizerRequired"
  | "emailInvalid"
  | "displayNameTooLong"
  | "duplicateAttendees"
  | "endAfterStart";

export function createMeetingDraft(now = new Date()): MeetingDraft {
  return {
    title: "",
    objective: "",
    startTime: toLocalDateTime(new Date(now.getTime() + 60 * 60 * 1000)),
    endTime: toLocalDateTime(new Date(now.getTime() + 2 * 60 * 60 * 1000)),
    location: "",
    meetingLink: "",
    reminderOffsetMinutes: "",
    organizerId: "",
    attendees: [],
  };
}

export function isValidMeetingEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

export function meetingToDraft(meeting: MeetingDetailDto): MeetingDraft {
  return {
    title: meeting.title,
    objective: meeting.objective ?? "",
    startTime: toLocalDateTimeInput(meeting.startTime),
    endTime: toLocalDateTimeInput(meeting.endTime),
    location: meeting.location ?? "",
    meetingLink: meeting.meetingLink ?? "",
    reminderOffsetMinutes: meeting.reminderOffsetMinutes == null ? "" : String(meeting.reminderOffsetMinutes),
    organizerId: meeting.organizerId ?? "",
    attendees: meeting.attendees.map((attendee) => ({
      userId: attendee.isInternalUser ? attendee.userId ?? undefined : undefined,
      email: attendee.email,
      displayName: attendee.displayName ?? undefined,
    })),
  };
}

export function buildMeetingUpdateBody(meeting: MeetingDetailDto, status?: string): (MeetingInput & { rowVersion?: string; status?: string }) | null {
  if (!meeting.organizerId) return null;
  return {
    title: meeting.title,
    objective: meeting.objective ?? undefined,
    projectId: meeting.projectId ?? undefined,
    startTime: meeting.startTime,
    endTime: meeting.endTime,
    location: meeting.location ?? undefined,
    meetingLink: meeting.meetingLink ?? undefined,
    reminderOffsetMinutes: meeting.reminderOffsetMinutes ?? null,
    organizerId: meeting.organizerId,
    attendees: meeting.attendees.map((attendee) => attendee.userId
      ? { userId: attendee.userId }
      : { email: attendee.email, displayName: attendee.displayName ?? undefined }),
    rowVersion: meeting.rowVersion,
    status,
  };
}

export function validateMeetingDraft(draft: MeetingDraft): MeetingDraftError | null {
  if (!draft.title.trim()) return "titleRequired";
  if (draft.title.trim().length > 500) return "titleTooLong";
  if (!draft.startTime) return "startRequired";
  if (!draft.endTime) return "endRequired";
  const start = new Date(draft.startTime).getTime();
  const end = new Date(draft.endTime).getTime();
  if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) return "endAfterStart";
  if (!draft.organizerId) return "organizerRequired";

  const attendeeKeys = new Set<string>();
  for (const attendee of draft.attendees) {
    const email = attendee.email.trim();
    if (!isValidMeetingEmail(email)) return "emailInvalid";
    if ((attendee.displayName?.length ?? 0) > 200) return "displayNameTooLong";
    const key = attendee.userId ? `user:${attendee.userId}` : `email:${email.toLocaleLowerCase()}`;
    if (attendeeKeys.has(key)) return "duplicateAttendees";
    attendeeKeys.add(key);
  }

  return null;
}

export function toMeetingInput(draft: MeetingDraft, projectId?: string): MeetingInput {
  return {
    title: draft.title.trim(),
    objective: draft.objective.trim() || undefined,
    projectId: projectId || undefined,
    startTime: new Date(draft.startTime).toISOString(),
    endTime: new Date(draft.endTime).toISOString(),
    location: draft.location.trim() || undefined,
    meetingLink: draft.meetingLink.trim() || undefined,
    reminderOffsetMinutes: draft.reminderOffsetMinutes ? Number(draft.reminderOffsetMinutes) : null,
    organizerId: draft.organizerId,
    attendees: draft.attendees.map((attendee) => attendee.userId
      ? { userId: attendee.userId }
      : { email: attendee.email.trim(), displayName: attendee.displayName?.trim() || undefined }),
  };
}

function toLocalDateTime(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function toLocalDateTimeInput(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : toLocalDateTime(date);
}
