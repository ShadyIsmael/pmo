import { BaseEntity } from "./common";

export enum AuditAction {
  Create = "create",
  Update = "update",
  Delete = "delete",
  Read = "read",
  Login = "login",
  Logout = "logout",
  Export = "export",
  Import = "import",
  Approve = "approve",
  Reject = "reject",
  Submit = "submit",
  Cancel = "cancel",
  Archive = "archive",
  Restore = "restore",
}

export interface AuditEntry extends BaseEntity {
  tenantId: string;
  userId: string;
  action: AuditAction;
  entityType: string;
  entityId: string;
  changes?: Record<string, { oldValue?: unknown; newValue?: unknown }>;
  metadata?: Record<string, unknown>;
  ipAddress?: string;
  userAgent?: string;
  correlationId?: string;
}

export interface AuditActivity extends BaseEntity {
  userId: string;
  action: AuditAction;
  description: string;
  timestamp: string;
  entityType?: string;
  entityId?: string;
}
