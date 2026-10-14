import type { PermissionCatalogItemDto } from "./api/admin";

type Translate = (key: string) => string;

function translatedOrSource(t: Translate, key: string, sourceValue: string): string {
  const translated = t(key);
  return translated === key ? sourceValue : translated;
}

export function getPermissionResourceLabel(permission: PermissionCatalogItemDto, t: Translate): string {
  return translatedOrSource(t, `administration.permissionLabels.resources.${permission.resource}`, permission.resource);
}

export function getPermissionActionLabel(permission: PermissionCatalogItemDto, t: Translate): string {
  return translatedOrSource(t, `administration.permissionLabels.actions.${permission.action}`, permission.action);
}

export function getPermissionDisplayName(permission: PermissionCatalogItemDto, t: Translate): string {
  return `${getPermissionResourceLabel(permission, t)} · ${getPermissionActionLabel(permission, t)}`;
}
