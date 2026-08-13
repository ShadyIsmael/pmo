import { BaseEntity } from "./common";

export enum MeetingStatus {
  Scheduled = "scheduled",
  InProgress = "in_progress",
  Completed = "completed",
  Cancelled = "cancelled",
}

export interface Meeting extends BaseEntity {
  projectId: string;
  title: string;
  description?: string;
  meetingType: string;
  status: MeetingStatus;
  startTime: string;
  endTime: string;
  timezone: string;
  location?: string;
  virtualMeetingUrl?: string;
  organizerId: string;
  isRecurring: boolean;
  recurrenceRule?: string;
  minutes?: string;
}

export interface MeetingAgenda extends BaseEntity {
  meetingId: string;
  title: string;
  description?: string;
  sortOrder: number;
  durationMinutes: number;
  ownerId?: string;
  isCompleted: boolean;
  notes?: string;
}

export interface MeetingParticipant extends BaseEntity {
  meetingId: string;
  userId: string;
  role: "organizer" | "presenter" | "attendee" | "minute_taker";
  status: "pending" | "accepted" | "declined" | "tentative";
  attended: boolean;
}

export interface MeetingMinutes extends BaseEntity {
  meetingId: string;
  content: string;
  recordedBy: string;
  isApproved: boolean;
  approvedBy?: string;
  approvedAt?: string;
}

export interface MeetingAction extends BaseEntity {
  meetingId: string;
  description: string;
  assignedTo?: string;
  dueDate?: string;
  completedAt?: string;
  status: "open" | "in_progress" | "completed" | "cancelled";
  priority: string;
}
