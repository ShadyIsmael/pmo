import type { Locale } from "@i18n/messages";
import type { PermissionCatalogItemDto, UserPermissionAssignmentDto } from "./api/admin";

type PermissionLabelSource = PermissionCatalogItemDto | UserPermissionAssignmentDto;

export function getPermissionResourceLabel(permission: PermissionLabelSource, locale: Locale): string {
  const label = locale === "ar" ? permission.screenNameAr : permission.screenNameEn;
  return label ?? permission.resource;
}

export function getPermissionModuleLabel(permission: PermissionCatalogItemDto, locale: Locale): string {
  return locale === "ar" ? permission.moduleNameAr : permission.moduleNameEn;
}

export function getPermissionActionLabel(permission: PermissionLabelSource, locale: Locale): string {
  if ("moduleId" in permission) return locale === "ar" ? permission.labelAr : permission.labelEn;
  return locale === "ar" ? permission.labelAr ?? permission.action : permission.labelEn ?? permission.action;
}

export function getPermissionDescription(permission: PermissionLabelSource, locale: Locale): string {
  if ("moduleId" in permission) return locale === "ar" ? permission.descriptionAr : permission.descriptionEn;
  return permission.description ?? "";
}

export function getPermissionDisplayName(permission: PermissionLabelSource, locale: Locale): string {
  return getPermissionActionLabel(permission, locale);
}
