/**
 * Canonical Resource field set.
 *
 * This module is the single source of truth shared by every Add/Edit Resource form
 * (the Resource Directory sheet and the outsourced resource sheets) so the forms
 * cannot drift apart again.
 *
 * The canonical field set is the intersection of the backend `CreateResourceDto` and
 * `UpdateResourceDto`, constrained by `ResourceTypeConstants` on the server, which
 * only accepts `Staff` and `VendorResource`.
 */

export const RESOURCE_TYPES = ["Staff", "VendorResource"] as const;
export type ResourceType = (typeof RESOURCE_TYPES)[number];

export const RESOURCE_TYPE_STAFF: ResourceType = "Staff";
export const RESOURCE_TYPE_VENDOR: ResourceType = "VendorResource";

export const RESOURCE_STATUSES = ["Active", "Inactive"] as const;
export type ResourceStatus = (typeof RESOURCE_STATUSES)[number];

export const RESOURCE_STATUS_ACTIVE: ResourceStatus = "Active";
export const RESOURCE_STATUS_INACTIVE: ResourceStatus = "Inactive";

/** One canonical form model. Every Add/Edit form reads and writes this shape. */
export type ResourceFormModel = {
  /** Optional link to a NEXUS system user. */
  userId: string;
  name: string;
  email: string;
  phone: string;
  jobTitle: string;
  employeeId: string;
  type: ResourceType;
  status: ResourceStatus;
  employmentType: string;
  seniority: string;
  notes: string;
  /** Required when `type` is `VendorResource`. */
  vendorId: string;
  /** GUID or empty. Binds to `Guid?` server-side, so a non-GUID is a 400. */
  baseCalendarId: string;
};

export const emptyResourceForm: ResourceFormModel = {
  userId: "",
  name: "",
  email: "",
  phone: "",
  jobTitle: "",
  employeeId: "",
  type: RESOURCE_TYPE_STAFF,
  status: RESOURCE_STATUS_ACTIVE,
  employmentType: "",
  seniority: "",
  notes: "",
  vendorId: "",
  baseCalendarId: "",
};

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value);
}

export function isValidEmail(value: string): boolean {
  if (!value) return true;
  try {
    return Boolean(new URL(`mailto:${value}`));
  } catch {
    return false;
  }
}

export type ResourceFormErrors = Partial<Record<keyof ResourceFormModel, boolean>>;

/** Normalises any value to a canonical type, defaulting to Staff. */
export function toResourceType(value: string | null | undefined): ResourceType {
  return value === RESOURCE_TYPE_VENDOR ? RESOURCE_TYPE_VENDOR : RESOURCE_TYPE_STAFF;
}

/** Normalises any value to a canonical status, defaulting to Active. */
export function toResourceStatus(value: string | null | undefined): ResourceStatus {
  return value === RESOURCE_STATUS_INACTIVE ? RESOURCE_STATUS_INACTIVE : RESOURCE_STATUS_ACTIVE;
}

/**
 * The one validation routine used by every Add/Edit form.
 * Returns the fields that failed so the caller can render the same errors.
 */
export function validateResourceForm(form: ResourceFormModel): ResourceFormErrors {
  const errors: ResourceFormErrors = {};

  if (!form.name.trim()) errors.name = true;
  if (!isValidEmail(form.email.trim())) errors.email = true;
  if (form.baseCalendarId.trim() && !isGuid(form.baseCalendarId.trim())) errors.baseCalendarId = true;
  if (form.type === RESOURCE_TYPE_VENDOR && !form.vendorId) errors.vendorId = true;

  return errors;
}

export function hasErrors(errors: ResourceFormErrors): boolean {
  return Object.values(errors).some(Boolean);
}

export type CreateResourceInput = {
  userId?: string;
  name: string;
  email?: string;
  phone?: string;
  jobTitle?: string;
  employeeId?: string;
  type: ResourceType;
  status: ResourceStatus;
  employmentType?: string;
  seniority?: string;
  notes?: string;
  vendorId?: string;
  baseCalendarId?: string;
};

export type UpdateResourceInput = {
  userId?: string;
  removeUserAssociation?: boolean;
  name?: string;
  email?: string;
  phone?: string;
  jobTitle?: string;
  employeeId?: string;
  status?: ResourceStatus;
  employmentType?: string;
  seniority?: string;
  notes?: string;
  vendorId?: string;
  baseCalendarId?: string;
  isActive?: boolean;
};

/** Optional text: empty string is normalised to `undefined` so the API omits it. */
function optional(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
}

/**
 * Builds the create payload. `type` is always sent because the server requires it
 * and validates it against ResourceTypeConstants. `vendorId` is only sent for
 * VendorResource so a Staff record can never carry a vendor link.
 */
export function toCreatePayload(form: ResourceFormModel): CreateResourceInput {
  return {
    userId: optional(form.userId),
    name: form.name.trim(),
    email: optional(form.email),
    phone: optional(form.phone),
    jobTitle: optional(form.jobTitle),
    employeeId: optional(form.employeeId),
    type: form.type,
    status: form.status,
    employmentType: optional(form.employmentType),
    seniority: optional(form.seniority),
    notes: optional(form.notes),
    vendorId: form.type === RESOURCE_TYPE_VENDOR ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId),
  };
}

/**
 * Builds the update payload.
 *
 * `type` is intentionally NOT sent: resource type is an immutable identity field
 * (changing it would orphan allocations and vendor links), so it stays read-only
 * in Edit. `isActive` is derived from `status`, and `removeUserAssociation` clears
 * the system-user link when no user is selected.
 */
export function toUpdatePayload(form: ResourceFormModel): UpdateResourceInput {
  return {
    userId: optional(form.userId),
    removeUserAssociation: !form.userId,
    name: form.name.trim(),
    email: optional(form.email),
    phone: optional(form.phone),
    jobTitle: optional(form.jobTitle),
    employeeId: optional(form.employeeId),
    status: form.status,
    employmentType: optional(form.employmentType),
    seniority: optional(form.seniority),
    notes: optional(form.notes),
    vendorId: form.type === RESOURCE_TYPE_VENDOR ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId),
    isActive: form.status === RESOURCE_STATUS_ACTIVE,
  };
}

/** Minimal shape needed to hydrate the form from an existing resource. */
export type ResourceFormSource = {
  userId?: string | null;
  name: string;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  employeeId?: string | null;
  type?: string | null;
  status?: string | null;
  employmentType?: string | null;
  seniority?: string | null;
  notes?: string | null;
  vendorId?: string | null;
  baseCalendarId?: string | null;
  isActive?: boolean;
};

/** Hydrates the canonical form model from an API resource. */
export function resourceToForm(resource: ResourceFormSource): ResourceFormModel {
  // `isActive` is authoritative when present; legacy rows only carry `status`.
  const status = resource.isActive === undefined
    ? toResourceStatus(resource.status)
    : resource.isActive
      ? RESOURCE_STATUS_ACTIVE
      : RESOURCE_STATUS_INACTIVE;

  return {
    userId: resource.userId ?? "",
    name: resource.name ?? "",
    email: resource.email ?? "",
    phone: resource.phone ?? "",
    jobTitle: resource.jobTitle ?? "",
    employeeId: resource.employeeId ?? "",
    type: toResourceType(resource.type),
    status,
    employmentType: resource.employmentType ?? "",
    seniority: resource.seniority ?? "",
    notes: resource.notes ?? "",
    vendorId: resource.vendorId ?? "",
    baseCalendarId: resource.baseCalendarId ?? "",
  };
}
