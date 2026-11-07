import type { ResourceDto, ResourceTypeOptionDto } from "./api/resources";

export type ResourceFormModel = {
  name: string;
  email: string;
  phone: string;
  jobTitle: string;
  type: string;
  requiresVendor: boolean;
  status: "Active" | "Inactive";
  userId: string;
  vendorId: string;
  baseCalendarId: string;
  hoursPerDay: string;
  hoursPerWeek: string;
  organizationalUnitId: string;
  seniority: string;
  notes: string;
};

/** Keep browser validation aligned with the existing SQL Server column lengths. */
export const RESOURCE_FIELD_LIMITS = {
  name: 200,
  email: 256,
  phone: 50,
  jobTitle: 200,
  seniority: 50,
  notes: 2000,
} as const;

export const emptyResourceForm: ResourceFormModel = {
  name: "", email: "", phone: "", jobTitle: "", type: "", requiresVendor: false, status: "Active",
  userId: "", vendorId: "", baseCalendarId: "", hoursPerDay: "", hoursPerWeek: "", organizationalUnitId: "", seniority: "", notes: "",
};

export function dialogValuesToResourceForm(values: Record<string, string>, types: ResourceTypeOptionDto[] = []): ResourceFormModel {
  const type = values.resourceType ?? "";
  return {
    name: values.name ?? "", email: values.email ?? "", phone: values.phone ?? "", jobTitle: values.jobTitle ?? "",
    type,
    requiresVendor: types.find((option) => option.code === type)?.requiresVendor ?? false,
    status: values.status === "Inactive" ? "Inactive" : "Active", userId: values.systemUserId ?? "", vendorId: values.vendorId ?? "",
    baseCalendarId: values.baseCalendarId ?? "", hoursPerDay: values.hoursPerDay ?? "", hoursPerWeek: values.hoursPerWeek ?? "",
    organizationalUnitId: values.organizationalUnitId ?? "",
    seniority: values.seniority ?? "", notes: values.notes ?? "",
  };
}

const optional = (value: string) => value.trim() || undefined;
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parseLocalizedHours(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) return null;

  const normalized = trimmed
    .replace(/[\u0660-\u0669]/g, (digit) => String(digit.charCodeAt(0) - 0x0660))
    .replace(/[\u06f0-\u06f9]/gi, (digit) => String(digit.charCodeAt(0) - 0x06f0))
    .replace(/\u066b/g, ".");
  if (!/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(normalized)) return Number.NaN;

  const hours = Number(normalized);
  return Number.isFinite(hours) ? hours : Number.NaN;
}

export function resourceToForm(resource: ResourceDto, types: ResourceTypeOptionDto[] = []): ResourceFormModel {
  const type = resource.type ?? "";
  return {
    name: resource.name ?? "", email: resource.email ?? "", phone: resource.phone ?? "", jobTitle: resource.jobTitle ?? "",
    type,
    requiresVendor: types.find((option) => option.code === type)?.requiresVendor ?? Boolean(resource.vendorId),
    status: resource.isActive ? "Active" : "Inactive", userId: resource.userId ?? "", vendorId: resource.vendorId ?? "",
    baseCalendarId: resource.baseCalendarId ?? "", hoursPerDay: resource.hoursPerDay?.toString() ?? "",
    hoursPerWeek: resource.hoursPerWeek?.toString() ?? "", organizationalUnitId: resource.primaryOrganizationalUnitId ?? "",
    seniority: resource.seniority ?? "", notes: resource.notes ?? "",
  };
}

export function validateResourceForm(form: ResourceFormModel, workingDaysPerWeek?: number) {
  const hoursPerDay = parseLocalizedHours(form.hoursPerDay);
  const hoursPerWeek = parseLocalizedHours(form.hoursPerWeek);
  const validHoursPerDay = hoursPerDay !== null && Number.isFinite(hoursPerDay) && hoursPerDay >= 1 && hoursPerDay <= 24;
  const validHoursPerWeek = hoursPerWeek === null || (Number.isFinite(hoursPerWeek) && hoursPerWeek > 0 && hoursPerWeek <= 168);
  const capacityMismatch = hoursPerDay !== null && hoursPerWeek !== null && validHoursPerDay && validHoursPerWeek
    && Number.isFinite(workingDaysPerWeek) && (workingDaysPerWeek ?? 0) > 0
    && Math.abs(hoursPerDay * (workingDaysPerWeek ?? 0) - hoursPerWeek) > 0.01;
  return {
    name: !form.name.trim(),
    type: !form.type,
    nameTooLong: form.name.length > RESOURCE_FIELD_LIMITS.name,
    email: Boolean(form.email.trim()) && !/^\S+@\S+\.\S+$/.test(form.email.trim()),
    emailTooLong: form.email.length > RESOURCE_FIELD_LIMITS.email,
    phoneTooLong: form.phone.length > RESOURCE_FIELD_LIMITS.phone,
    jobTitleTooLong: form.jobTitle.length > RESOURCE_FIELD_LIMITS.jobTitle,
    seniorityTooLong: form.seniority.length > RESOURCE_FIELD_LIMITS.seniority,
    notesTooLong: form.notes.length > RESOURCE_FIELD_LIMITS.notes,
    vendor: form.requiresVendor && !form.vendorId,
    user: form.type === "Staff" && !form.userId.trim(),
    baseCalendar: Boolean(form.baseCalendarId.trim()) && !guidPattern.test(form.baseCalendarId.trim()),
    hoursPerDay: !validHoursPerDay,
    hoursPerWeek: !validHoursPerWeek,
    capacityMismatch,
  };
}

const optionalHours = (value: string) => parseLocalizedHours(value) ?? undefined;

export function toCreateResourcePayload(form: ResourceFormModel) {
  return {
    userId: optional(form.userId), name: form.name.trim(), email: optional(form.email), phone: optional(form.phone),
    jobTitle: optional(form.jobTitle), type: form.type, status: form.status,
    seniority: optional(form.seniority), notes: optional(form.notes),
    vendorId: form.requiresVendor ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId), primaryOrganizationalUnitId: optional(form.organizationalUnitId),
    hoursPerDay: optionalHours(form.hoursPerDay), hoursPerWeek: optionalHours(form.hoursPerWeek),
  };
}

export function toUpdateResourcePayload(form: ResourceFormModel) {
  return {
    userId: optional(form.userId), removeUserAssociation: !form.userId, name: form.name.trim(), email: optional(form.email),
    phone: optional(form.phone), jobTitle: optional(form.jobTitle), status: form.status,
    seniority: optional(form.seniority), notes: optional(form.notes),
    vendorId: form.requiresVendor ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId), primaryOrganizationalUnitId: optional(form.organizationalUnitId),
    hoursPerDay: optionalHours(form.hoursPerDay), hoursPerWeek: optionalHours(form.hoursPerWeek),
    clearHoursPerWeek: !form.hoursPerWeek.trim(),
    isActive: form.status === "Active",
  };
}

