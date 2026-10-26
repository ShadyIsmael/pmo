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
  userId: "", vendorId: "", baseCalendarId: "", organizationalUnitId: "", seniority: "", notes: "",
};

export function dialogValuesToResourceForm(values: Record<string, string>, types: ResourceTypeOptionDto[] = []): ResourceFormModel {
  const type = values.resourceType ?? "";
  return {
    name: values.name ?? "", email: values.email ?? "", phone: values.phone ?? "", jobTitle: values.jobTitle ?? "",
    type,
    requiresVendor: types.find((option) => option.code === type)?.requiresVendor ?? false,
    status: values.status === "Inactive" ? "Inactive" : "Active", userId: values.systemUserId ?? "", vendorId: values.vendorId ?? "",
    baseCalendarId: values.baseCalendarId ?? "", organizationalUnitId: values.organizationalUnitId ?? "",
    seniority: values.seniority ?? "", notes: values.notes ?? "",
  };
}

const optional = (value: string) => value.trim() || undefined;
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function resourceToForm(resource: ResourceDto, types: ResourceTypeOptionDto[] = []): ResourceFormModel {
  const type = resource.type ?? "";
  return {
    name: resource.name ?? "", email: resource.email ?? "", phone: resource.phone ?? "", jobTitle: resource.jobTitle ?? "",
    type,
    requiresVendor: types.find((option) => option.code === type)?.requiresVendor ?? Boolean(resource.vendorId),
    status: resource.isActive ? "Active" : "Inactive", userId: resource.userId ?? "", vendorId: resource.vendorId ?? "",
    baseCalendarId: resource.baseCalendarId ?? "", organizationalUnitId: resource.primaryOrganizationalUnitId ?? "",
    seniority: resource.seniority ?? "", notes: resource.notes ?? "",
  };
}

export function validateResourceForm(form: ResourceFormModel) {
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
    baseCalendar: Boolean(form.baseCalendarId.trim()) && !guidPattern.test(form.baseCalendarId.trim()),
  };
}

export function toCreateResourcePayload(form: ResourceFormModel) {
  return {
    userId: optional(form.userId), name: form.name.trim(), email: optional(form.email), phone: optional(form.phone),
    jobTitle: optional(form.jobTitle), type: form.type, status: form.status,
    seniority: optional(form.seniority), notes: optional(form.notes),
    vendorId: form.requiresVendor ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId), primaryOrganizationalUnitId: optional(form.organizationalUnitId),
  };
}

export function toUpdateResourcePayload(form: ResourceFormModel) {
  return {
    userId: optional(form.userId), removeUserAssociation: !form.userId, name: form.name.trim(), email: optional(form.email),
    phone: optional(form.phone), jobTitle: optional(form.jobTitle), status: form.status,
    seniority: optional(form.seniority), notes: optional(form.notes),
    vendorId: form.requiresVendor ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId), primaryOrganizationalUnitId: optional(form.organizationalUnitId),
    isActive: form.status === "Active",
  };
}

