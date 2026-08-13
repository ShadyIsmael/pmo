import { BaseEntity } from "./common";

export interface IntegrationConnection extends BaseEntity {
  organizationId: string;
  name: string;
  provider: string;
  config: Record<string, unknown>;
  credentials: Record<string, unknown>;
  isEnabled: boolean;
  lastSyncAt?: string;
  lastSyncStatus?: "success" | "failed" | "partial";
  errorMessage?: string;
  webhookUrl?: string;
}

export interface IntegrationSync extends BaseEntity {
  connectionId: string;
  startedAt: string;
  completedAt?: string;
  status: "running" | "completed" | "failed" | "cancelled";
  recordsProcessed: number;
  recordsCreated: number;
  recordsUpdated: number;
  recordsFailed: number;
  errorLog?: string;
}

export interface IntegrationWebhook extends BaseEntity {
  connectionId: string;
  url: string;
  secret?: string;
  events: readonly string[];
  isActive: boolean;
  lastTriggeredAt?: string;
  lastResponseStatus?: number;
}

export interface SyncConflict extends BaseEntity {
  syncId: string;
  entityType: string;
  entityId: string;
  localValue: unknown;
  remoteValue: unknown;
  resolution?: "keep_local" | "keep_remote" | "merge" | "manual";
  resolvedAt?: string;
  resolvedBy?: string;
}
