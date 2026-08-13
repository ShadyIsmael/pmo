import { BaseEntity } from "./common";

export interface User extends BaseEntity {
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  avatarUrl?: string;
  phone?: string;
  jobTitle?: string;
  isActive: boolean;
  lastLoginAt?: string;
  preferredLocale: string;
  timezone: string;
}

export interface Role extends BaseEntity {
  name: string;
  description: string;
  isSystem: boolean;
  permissions: Permission[];
}

export interface Permission extends BaseEntity {
  name: string;
  code: string;
  description: string;
  group: string;
}

export interface UserRole {
  userId: string;
  roleId: string;
  role: Role;
  assignedAt: string;
  assignedBy: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  user: CurrentUser;
  token: AuthToken;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string;
  roles: string[];
  permissions: string[];
  tenantId: string;
}

export interface AuthToken {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  tokenType: string;
}
