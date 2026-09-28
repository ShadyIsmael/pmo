import { BaseEntity } from "./common";

export enum ProjectStatus {
  Initiation = "initiation",
  Planning = "planning",
  InProgress = "in_progress",
  OnHold = "on_hold",
  Completed = "completed",
  Cancelled = "cancelled",
  Closed = "closed",
}

export enum ProjectHealth {
  Green = "green",
  Amber = "amber",
  Red = "red",
}

export interface Project extends BaseEntity {
  organizationId: string;
  portfolioId?: string;
  programId?: string;
  name: string;
  code: string;
  description?: string;
  status: ProjectStatus;
  health: ProjectHealth;
  managerId: string;
  sponsorId?: string;
  startDate: string;
  endDate?: string;
  actualEndDate?: string;
  budget: number;
  actualCost: number;
  currencyCode: string;
  priority: number;
  progress: number;
  phase: string;
  isActive: boolean;
}

export interface ProjectMember extends BaseEntity {
  projectId: string;
  userId: string;
  role: string;
  allocationPercentage: number;
  startDate: string;
  endDate?: string;
  isBillable: boolean;
}

export interface ProjectPhase extends BaseEntity {
  projectId: string;
  name: string;
  description?: string;
  sortOrder: number;
  startDate: string;
  endDate?: string;
  isCompleted: boolean;
  progress: number;
}

export interface ProjectStakeholder extends BaseEntity {
  projectId: string;
  userId?: string;
  name: string;
  email?: string;
  role: string;
  interest?: string;
  influence?: string;
  type: "Internal" | "ContactOnly";
  status: "Active" | "ContactOnly" | "Revoked" | string;
}

export interface Milestone extends BaseEntity {
  projectId: string;
  name: string;
  description?: string;
  targetDate: string;
  actualDate?: string;
  isCompleted: boolean;
  status: string;
}

export interface Baseline extends BaseEntity {
  projectId: string;
  name: string;
  version: number;
  scope: string;
  schedule: string;
  cost: number;
  isApproved: boolean;
  approvedBy?: string;
  approvedAt?: string;
}

export interface Deliverable extends BaseEntity {
  projectId: string;
  phaseId?: string;
  name: string;
  description?: string;
  acceptanceCriteria: string;
  status: string;
  assignedTo?: string;
  targetDate: string;
  completedAt?: string;
  qualityScore?: number;
}

export interface ProjectStatusReport extends BaseEntity {
  projectId: string;
  reportDate: string;
  health: ProjectHealth;
  progress: number;
  accomplishments: string;
  nextSteps: string;
  issues?: string;
  risks?: string;
  budgetVariance: number;
  scheduleVariance: number;
  submittedBy: string;
  reviewedBy?: string;
  isApproved: boolean;
}
