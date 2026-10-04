export type TeamStatusFilter = "all" | "active" | "inactive";

export interface TeamFilterParams {
  page?: number;
  pageSize?: number;
  search?: string;
  departmentId?: string;
  isActive?: boolean;
}

export const teamKeys = {
  all: ["teams"] as const,
  list: (filters: TeamFilterParams = {}) => ["teams", "list", filters] as const,
  detail: (id: string) => ["teams", "detail", id] as const,
  members: (id: string) => ["teams", "detail", id, "members"] as const,
  lookup: (activeOnly: boolean) => ["teams", "lookup", String(activeOnly)] as const,
};

export interface DepartmentOption {
  id: string;
  name: string;
  code?: string;
  isActive?: boolean;
}
