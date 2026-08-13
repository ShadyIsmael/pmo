export interface RoleDto { id: string; name: string; description?: string; category?: string; }
export interface CurrentUser { id: string; email: string; firstName: string; lastName: string; fullName: string; jobTitle?: string; phone?: string; avatarUrl?: string; tenantId?: string; roles: RoleDto[]; permissions: string[]; }
export interface AuthResponse { token: string; refreshToken: string; expiresAt: string; user: CurrentUser; }
export interface PaginationParams { page?: number; pageSize?: number; sortBy?: string; sortDirection?: "asc" | "desc"; search?: string; [key: string]: string | number | boolean | undefined; }
export interface PagedResult<T> { items: readonly T[]; page: number; pageSize: number; totalCount: number; totalPages: number; }
export interface ValidationFieldError { field: string; messages: string[]; }
export type ApiErrorKind = "network" | "validation" | "conflict" | "unauthorized" | "forbidden" | "not-found" | "http";
export interface ApiErrorPayload { message?: string; title?: string; errors?: Record<string, string[] | string>; detail?: string; }
export class ApiError extends Error {
  readonly status: number; readonly kind: ApiErrorKind; readonly fieldErrors: ValidationFieldError[]; readonly data?: unknown;
  constructor(status: number, message: string, data?: unknown, fieldErrors: ValidationFieldError[] = []) {
    super(message); this.name = "ApiError"; this.status = status; this.data = data; this.fieldErrors = fieldErrors;
    this.kind = status === 0 ? "network" : status === 400 ? "validation" : status === 401 ? "unauthorized" : status === 403 ? "forbidden" : status === 404 ? "not-found" : status === 409 ? "conflict" : "http";
  }
}
