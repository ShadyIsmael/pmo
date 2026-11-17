import { describe, expect, it } from "vitest";
import type { Locale } from "@i18n/messages";
import type { PermissionCatalogItemDto } from "@features/administration/api/admin";
import {
  getPermissionBusinessModuleId,
  getPermissionBusinessModuleLabel,
  hasUnverifiedPermissionUiSurface,
} from "@features/administration/permission-labels";

function permission(moduleId: string, scope: string): PermissionCatalogItemDto {
  return {
    id: `${moduleId}-${scope}`,
    name: "Example.View",
    resource: "Example",
    action: "View",
    moduleId,
    moduleNameEn: moduleId,
    moduleNameAr: moduleId,
    screenId: "example",
    screenNameEn: "Example",
    screenNameAr: "مثال",
    level: "View",
    labelEn: "View",
    labelAr: "عرض",
    descriptionEn: "View example records",
    descriptionAr: "عرض سجلات المثال",
    scope,
    sensitivity: "Low",
    isSensitive: false,
    requiredPermissionKeys: [],
    isActive: true,
    isAssignable: true,
    canGrantToOrganizationRole: true,
    isMandatoryForSystemAdmin: false,
    allowedCategories: [],
  };
}

describe("business permission modules", () => {
  it("maps each current catalog module to one business group and keeps rollups at their parent scope", () => {
    const groups = [
      [permission("project-management", "Project"), "project-management"],
      [permission("project-delivery", "Project"), "project-management"],
      [permission("financial-management", "Project"), "project-management"],
      [permission("financial-management", "Program"), "portfolio-program-management"],
      [permission("financial-management", "Portfolio"), "portfolio-program-management"],
      [permission("portfolio-program-management", "Portfolio"), "portfolio-program-management"],
      [permission("capacity", "Tenant"), "resources-capacity"],
      [permission("governance", "Tenant"), "governance-approvals"],
      [permission("reporting", "Tenant"), "reporting"],
      [permission("administration", "Tenant"), "administration-security"],
    ] as const;

    for (const [item, expectedGroup] of groups)
      expect(getPermissionBusinessModuleId(item)).toBe(expectedGroup);
  });

  it("marks all generic ApprovalRequest keys as lacking a verified UI control", () => {
    const genericApprovalKeys = [
      "ApprovalRequest.View",
      "ApprovalRequest.Submit",
      "ApprovalRequest.Approve",
      "ApprovalRequest.Reject",
      "ApprovalRequest.Delegate",
      "ApprovalRequest.Cancel",
    ];
    const base = permission("project-delivery", "Tenant");

    for (const name of genericApprovalKeys)
      expect(hasUnverifiedPermissionUiSurface({ ...base, name })).toBe(true);

    expect(hasUnverifiedPermissionUiSurface({ ...base, name: "ChangeRequest.Approve" })).toBe(false);
  });

  it("provides English and Arabic business group labels", () => {
    const item = permission("financial-management", "Project");
    const key = "administration.areas.roles.businessModules.projectManagement";
    const english = (translationKey: string) => translationKey === key ? "Project Management" : translationKey;
    const arabic = (translationKey: string) => translationKey === key ? "إدارة المشاريع" : translationKey;

    expect(getPermissionBusinessModuleLabel(item, "en" as Locale, english)).toBe("Project Management");
    expect(getPermissionBusinessModuleLabel(item, "ar" as Locale, arabic)).toBe("إدارة المشاريع");
  });
});
