import type { Locale } from "@i18n/messages";
import type { PermissionCatalogItemDto, UserPermissionAssignmentDto } from "./api/admin";

type PermissionLabelSource = PermissionCatalogItemDto | UserPermissionAssignmentDto;

const PERMISSIONS_WITHOUT_VERIFIED_UI_CONTROL = new Set([
  "ApprovalRequest.View",
  "ApprovalRequest.Submit",
  "ApprovalRequest.Approve",
  "ApprovalRequest.Reject",
  "ApprovalRequest.Delegate",
  "ApprovalRequest.Cancel",
]);

export function hasUnverifiedPermissionUiSurface(permission: PermissionCatalogItemDto): boolean {
  return PERMISSIONS_WITHOUT_VERIFIED_UI_CONTROL.has(permission.name);
}

export function getPermissionResourceLabel(permission: PermissionLabelSource, locale: Locale): string {
  const label = locale === "ar" ? permission.screenNameAr : permission.screenNameEn;
  return label ?? permission.resource;
}

export function getPermissionModuleLabel(permission: PermissionCatalogItemDto, locale: Locale): string {
  return locale === "ar" ? permission.moduleNameAr : permission.moduleNameEn;
}

export function getPermissionBusinessModuleId(permission: PermissionCatalogItemDto): string {
  switch (permission.moduleId) {
    case "project-management":
    case "project-delivery":
      return "project-management";
    case "financial-management":
      return permission.scope === "Portfolio" || permission.scope === "Program"
        ? "portfolio-program-management"
        : "project-management";
    case "portfolio-program-management":
      return "portfolio-program-management";
    case "capacity":
      return "resources-capacity";
    case "governance":
      return "governance-approvals";
    case "reporting":
      return "reporting";
    case "administration":
      return "administration-security";
    default:
      return permission.moduleId;
  }
}

export function getPermissionBusinessModuleLabel(
  permission: PermissionCatalogItemDto,
  locale: Locale,
  t: (key: string) => string,
): string {
  switch (getPermissionBusinessModuleId(permission)) {
    case "project-management":
      return getBusinessModuleTranslation(t, "projectManagement", permission, locale);
    case "portfolio-program-management":
      return getBusinessModuleTranslation(t, "portfolioProgramManagement", permission, locale);
    case "resources-capacity":
      return getBusinessModuleTranslation(t, "resourcesCapacity", permission, locale);
    case "governance-approvals":
      return getBusinessModuleTranslation(t, "governanceApprovals", permission, locale);
    case "reporting":
      return getBusinessModuleTranslation(t, "reportingInsights", permission, locale);
    case "administration-security":
      return getBusinessModuleTranslation(t, "administrationSecurity", permission, locale);
    default:
      return getPermissionModuleLabel(permission, locale);
  }
}

function getBusinessModuleTranslation(
  t: (key: string) => string,
  group: string,
  permission: PermissionCatalogItemDto,
  locale: Locale,
): string {
  const key = `administration.areas.roles.businessModules.${group}`;
  const label = t(key);
  return label.startsWith("administration.areas.roles.businessModules.")
    ? getPermissionModuleLabel(permission, locale)
    : label;
}

export function getPermissionActionLabel(permission: PermissionLabelSource, locale: Locale): string {
  if ("moduleId" in permission) return locale === "ar" ? permission.labelAr : permission.labelEn;
  return locale === "ar" ? permission.labelAr ?? permission.action : permission.labelEn ?? permission.action;
}

export function getPermissionDescription(permission: PermissionLabelSource, locale: Locale): string {
  if ("moduleId" in permission) return locale === "ar" ? permission.descriptionAr : permission.descriptionEn;
  return permission.description ?? "";
}

export function getPermissionScopeValueLabel(scope: string, t: (key: string) => string): string | undefined {
  if (!scope) return undefined;
  const prefix = "administration.areas.roles.scopes.";
  const key = `${prefix}${scope.trim().toLowerCase()}`;
  const label = t(key);
  return label.startsWith(prefix) ? scope : label;
}

export function getPermissionDisplayName(permission: PermissionLabelSource, locale: Locale): string {
  return getPermissionActionLabel(permission, locale);
}
