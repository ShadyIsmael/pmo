import { BaseEntity } from "./common";

export enum WorkItemType {
  Task = "task",
  Story = "story",
  Epic = "epic",
  Bug = "bug",
  SubTask = "sub_task",
  Feature = "feature",
  Improvement = "improvement",
}

export enum WorkItemPriority {
  Critical = "critical",
  High = "high",
  Medium = "medium",
  Low = "low",
  Lowest = "lowest",
}

export enum WorkItemStatus {
  Backlog = "backlog",
  ToDo = "to_do",
  InProgress = "in_progress",
  InReview = "in_review",
  Done = "done",
  Blocked = "blocked",
  Cancelled = "cancelled",
}

export enum WorkItemSource {
  Manual = "manual",
  Email = "email",
  Integration = "integration",
  Ai = "ai",
  Import = "import",
}

export interface WorkItem extends BaseEntity {
  projectId: string;
  parentId?: string;
  type: WorkItemType;
  priority: WorkItemPriority;
  status: WorkItemStatus;
  source: WorkItemSource;
  title: string;
  description?: string;
  assigneeId?: string;
  reporterId: string;
  storyPoints?: number;
  originalEstimate?: number;
  remainingEstimate?: number;
  timeSpent?: number;
  dueDate?: string;
  completedAt?: string;
  tags: readonly string[];
  sortOrder: number;
  isBlocked: boolean;
  blockedReason?: string;
}

export interface WorkItemAssignment {
  workItemId: string;
  userId: string;
  assignedAt: string;
  assignedBy: string;
  role: string;
}
