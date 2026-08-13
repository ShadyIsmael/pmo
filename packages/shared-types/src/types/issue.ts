import { BaseEntity } from "./common";

export enum IssueSeverity {
  Critical = "critical",
  High = "high",
  Medium = "medium",
  Low = "low",
}

export enum IssueStatus {
  Open = "open",
  Investigating = "investigating",
  Resolved = "resolved",
  Closed = "closed",
  WontFix = "wont_fix",
}

export interface Issue extends BaseEntity {
  projectId: string;
  identifier: string;
  title: string;
  description?: string;
  severity: IssueSeverity;
  status: IssueStatus;
  categoryId?: string;
  assignedTo?: string;
  reportedBy: string;
  impact?: string;
  workaround?: string;
  resolution?: string;
  detectedAt: string;
  resolvedAt?: string;
  closedAt?: string;
  priority: number;
  isActive: boolean;
}

export interface IssueCategory extends BaseEntity {
  organizationId: string;
  name: string;
  description?: string;
  color?: string;
  isActive: boolean;
}

export interface IssueAction extends BaseEntity {
  issueId: string;
  description: string;
  assignedTo?: string;
  targetDate?: string;
  completedAt?: string;
  status: "open" | "in_progress" | "completed" | "cancelled";
}
