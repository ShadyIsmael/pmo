import { BaseEntity } from "./common";

export enum NotificationType {
  Info = "info",
  Success = "success",
  Warning = "warning",
  Error = "error",
  Assignment = "assignment",
  Mention = "mention",
  Approval = "approval",
  Reminder = "reminder",
  System = "system",
}

export interface Notification extends BaseEntity {
  userId: string;
  type: NotificationType;
  title: string;
  message: string;
  isRead: boolean;
  readAt?: string;
  actionUrl?: string;
  icon?: string;
  senderId?: string;
  metadata?: Record<string, unknown>;
}

export interface NotificationTemplate extends BaseEntity {
  name: string;
  code: string;
  type: NotificationType;
  subject: string;
  bodyTemplate: string;
  channels: readonly ("in_app" | "email" | "push" | "sms")[];
  isActive: boolean;
}

export interface NotificationPreference extends BaseEntity {
  userId: string;
  type: NotificationType;
  channels: readonly ("in_app" | "email" | "push" | "sms")[];
  isEnabled: boolean;
}

export interface NotificationDigest extends BaseEntity {
  userId: string;
  frequency: "immediate" | "daily" | "weekly";
  lastSentAt?: string;
  nextScheduledAt?: string;
  notifications: readonly Notification[];
}
