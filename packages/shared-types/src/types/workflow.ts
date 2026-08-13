import { BaseEntity } from "./common";

export interface WorkflowTemplate extends BaseEntity {
  name: string;
  description?: string;
  category: string;
  version: number;
  isPublished: boolean;
  states: readonly WorkflowState[];
  transitions: readonly WorkflowTransition[];
  sla?: SLA;
}

export interface WorkflowVersion extends BaseEntity {
  templateId: string;
  version: number;
  definition: string;
  changelog?: string;
  publishedBy: string;
  publishedAt: string;
  isActive: boolean;
}

export interface WorkflowInstance extends BaseEntity {
  templateId: string;
  workflowVersion: number;
  entityType: string;
  entityId: string;
  currentState: string;
  states: readonly WorkflowState[];
  transitions: readonly WorkflowTransition[];
  context: Record<string, unknown>;
  status: "active" | "completed" | "cancelled" | "on_hold";
  slaDeadline?: string;
  slaBreached: boolean;
}

export interface WorkflowState extends BaseEntity {
  name: string;
  displayName: string;
  description?: string;
  color?: string;
  isInitial: boolean;
  isFinal: boolean;
  permissions: readonly string[];
}

export interface WorkflowTransition extends BaseEntity {
  name: string;
  displayName: string;
  fromState: string;
  toState: string;
  conditions: readonly WorkflowCondition[];
  permissions: readonly string[];
  requiresComment: boolean;
}

export interface WorkflowCondition {
  field: string;
  operator: string;
  value: unknown;
}

export interface SLA extends BaseEntity {
  duration: number;
  durationUnit: "minutes" | "hours" | "days" | "business_days";
  escalationIds: readonly string[];
  warnAt: number;
  breachAt: number;
}

export interface Escalation extends BaseEntity {
  name: string;
  description?: string;
  level: number;
  escalateTo: string;
  notifyBeforeMinutes: number;
  notificationTemplate?: string;
}
