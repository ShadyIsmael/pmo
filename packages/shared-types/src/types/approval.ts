import { BaseEntity } from "./common";

export enum ApprovalStatus {
  Pending = "pending",
  Approved = "approved",
  Rejected = "rejected",
  Cancelled = "cancelled",
  Revoked = "revoked",
}

export interface ApprovalRequest extends BaseEntity {
  entityType: string;
  entityId: string;
  requestorId: string;
  approverId: string;
  status: ApprovalStatus;
  comments?: string;
  decidedAt?: string;
  decidedBy?: string;
  reason?: string;
  metadata?: Record<string, unknown>;
}

export interface ApprovalAction extends BaseEntity {
  approvalRequestId: string;
  action: "approved" | "rejected" | "revoked";
  comments?: string;
  performedBy: string;
  performedAt: string;
}

export interface Delegation extends BaseEntity {
  delegatorId: string;
  delegateId: string;
  startDate: string;
  endDate?: string;
  permissionCodes: readonly string[];
  isActive: boolean;
  reason?: string;
}
