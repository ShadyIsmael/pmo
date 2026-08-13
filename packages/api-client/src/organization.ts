import type { ApiClient } from "./client";
import type { Organization, BusinessUnit, Department, Team, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createOrganizationApi(client: ApiClient) {
  return {
    getOrganization: () => client.get<Organization>("/organization"),

    getBusinessUnits: (params?: PaginationParams) =>
      client.getPaged<BusinessUnit>("/organization/business-units", params),

    getDepartments: (businessUnitId?: string, params?: PaginationParams) =>
      client.getPaged<Department>("/organization/departments", { ...params, businessUnitId }),

    getTeams: (departmentId?: string, params?: PaginationParams) =>
      client.getPaged<Team>("/organization/teams", { ...params, departmentId }),
  };
}
