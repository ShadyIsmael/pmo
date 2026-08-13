import { BaseEntity } from "./common";

export enum RiskStatus {
  Identified = "identified",
  Assessed = "assessed",
  MitigationInProgress = "mitigation_in_progress",
  Mitigated = "mitigated",
  Closed = "closed",
  Accepted = "accepted",
}

export enum RiskLevel {
  VeryLow = "very_low",
  Low = "low",
  Medium = "medium",
  High = "high",
  VeryHigh = "very_high",
}

export interface Risk extends BaseEntity {
  projectId: string;
  identifier: string;
  title: string;
  description?: string;
  categoryId?: string;
  status: RiskStatus;
  level: RiskLevel;
  probability: number;
  impact: number;
  score: number;
  ownerId?: string;
  rootCause?: string;
  trigger?: string;
  detectedAt: string;
  reviewDate?: string;
  closedAt?: string;
  isActive: boolean;
}

export interface RiskCategory extends BaseEntity {
  organizationId: string;
  name: string;
  description?: string;
  color?: string;
  isActive: boolean;
}

export interface RiskMitigation extends BaseEntity {
  riskId: string;
  description: string;
  type: "preventive" | "contingent";
  ownerId?: string;
  targetDate?: string;
  completedAt?: string;
  cost: number;
  effectiveness: number;
  status: "proposed" | "approved" | "in_progress" | "completed" | "cancelled";
}

export interface RiskContingency extends BaseEntity {
  riskId: string;
  description: string;
  trigger: string;
  ownerId?: string;
  budget: number;
  currencyCode: string;
  status: "active" | "triggered" | "expired";
}
