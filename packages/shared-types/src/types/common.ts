export interface BaseEntity {
  id: string;
  createdAt: string;
  updatedAt: string;
}

export interface ITenantEntity extends BaseEntity {
  tenantId: string;
}

export interface ISoftDeletable {
  isDeleted: boolean;
  deletedAt?: string;
  deletedBy?: string;
}

export interface PagedResult<T> {
  items: readonly T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PaginationParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: SortDirection;
  search?: string;
}

export enum SortDirection {
  Asc = "asc",
  Desc = "desc",
}

export enum FilterOperator {
  Equals = "eq",
  NotEquals = "ne",
  Contains = "contains",
  GreaterThan = "gt",
  GreaterThanOrEqual = "gte",
  LessThan = "lt",
  LessThanOrEqual = "lte",
  In = "in",
  Between = "between",
}
