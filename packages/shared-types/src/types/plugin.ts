import { BaseEntity } from "./common";

export enum PluginStatus {
  Installed = "installed",
  Active = "active",
  Inactive = "inactive",
  Error = "error",
  PendingApproval = "pending_approval",
  Removed = "removed",
}

export interface Plugin extends BaseEntity {
  name: string;
  packageName: string;
  version: string;
  description?: string;
  author: string;
  icon?: string;
  status: PluginStatus;
  entryPoint: string;
  permissions: readonly string[];
  settings: Record<string, unknown>;
  isSystem: boolean;
  dependencies: readonly PluginDependency[];
}

export interface TenantPlugin extends BaseEntity {
  tenantId: string;
  pluginId: string;
  status: PluginStatus;
  settings: Record<string, unknown>;
  installedAt: string;
  installedBy: string;
  activatedAt?: string;
  deactivatedAt?: string;
}

export interface PluginDependency {
  pluginName: string;
  version: string;
  isRequired: boolean;
}
