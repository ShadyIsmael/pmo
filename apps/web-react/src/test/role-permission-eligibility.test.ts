import { describe, expect, it } from "vitest";
import type { ManagedRoleDto, PermissionCatalogItemDto } from "@features/administration/api/admin";
import { canGrantPermissionToRole, getEffectivePermissionScopeForRole } from "@features/administration/RolesPage";

function permission(
  name: string,
  scope: string,
  canGrantToOrganizationRole: boolean,
  moduleId = "project-management",
): PermissionCatalogItemDto {
  const [resource, action] = name.split(".");
  return {
    id: name,
    name,
    resource,
    action,
    moduleId,
    moduleNameEn: moduleId,
    moduleNameAr: moduleId,
    screenId: resource.toLowerCase(),
    screenNameEn: resource,
    screenNameAr: resource,
    level: action === "View" ? "View" : "Action",
    labelEn: action,
    labelAr: action,
    descriptionEn: name,
    descriptionAr: name,
    scope,
    sensitivity: "Low",
    isSensitive: false,
    requiredPermissionKeys: [],
    isActive: true,
    isAssignable: true,
    canGrantToOrganizationRole,
    isMandatoryForSystemAdmin: false,
    allowedCategories: [],
    canDelegateToCustomRole: true,
  };
}

const organizationRole = {
  id: "role-nexus",
  name: "Portfolio Manager",
  organizationId: "nexus-organization",
  isOrganizationOwned: true,
  isSystem: false,
} as ManagedRoleDto;

describe("Organization-owned Role permission eligibility", () => {
  it("allows operational grants whose catalog scope is Tenant when the API marks them Organization-role eligible", () => {
    for (const key of ["Project.View", "Project.Edit", "Program.Edit", "Portfolio.Edit"]) {
      expect(canGrantPermissionToRole(permission(key, "Tenant", true), organizationRole)).toBe(true);
    }
  });

  it("shows the Organization effective scope for Handover permissions assigned to an Organization-owned Role", () => {
    for (const key of ["Handover.View", "Handover.AdminInitiate"]) {
      const handoverPermission = permission(key, "Tenant", true);
      expect(canGrantPermissionToRole(handoverPermission, organizationRole)).toBe(true);
      expect(getEffectivePermissionScopeForRole(handoverPermission, organizationRole)).toBe("Organization");
    }
    expect(getEffectivePermissionScopeForRole(permission("Handover.AdminInitiate", "Tenant", true), {
      ...organizationRole,
      organizationId: null,
      isOrganizationOwned: false,
    } as ManagedRoleDto)).toBe("Tenant");
  });

  it("keeps tenant administration grants unavailable even when the catalog scope is Tenant", () => {
    expect(canGrantPermissionToRole(permission("Governance.AssignmentRecovery.View", "Tenant", false), organizationRole)).toBe(false);
    expect(canGrantPermissionToRole(permission("Roles.ConfigurePermissions", "Tenant", false, "administration"), organizationRole)).toBe(false);
  });
});
