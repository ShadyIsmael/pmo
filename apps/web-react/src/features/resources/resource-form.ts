import type { ResourceDto } from "./api/resources";

export type ResourceFormModel = {
  name: string;
  email: string;
  phone: string;
  jobTitle: string;
  employeeId: string;
  type: "Staff" | "VendorResource";
  status: "Active" | "Inactive";
  userId: string;
  vendorId: string;
  baseCalendarId: string;
  organizationalUnitId: string;
  employmentType: string;
  seniority: string;
  notes: string;
};

export const emptyResourceForm: ResourceFormModel = {
  name: "", email: "", phone: "", jobTitle: "", employeeId: "", type: "Staff", status: "Active",
  userId: "", vendorId: "", baseCalendarId: "", organizationalUnitId: "", employmentType: "", seniority: "", notes: "",
};

export function dialogValuesToResourceForm(values: Record<string, string>): ResourceFormModel {
  return {
    name: values.name ?? "", email: values.email ?? "", phone: values.phone ?? "", jobTitle: values.jobTitle ?? "",
    employeeId: values.employeeId ?? "", type: values.resourceType === "VendorResource" ? "VendorResource" : "Staff",
    status: values.status === "Inactive" ? "Inactive" : "Active", userId: values.systemUserId ?? "", vendorId: values.vendorId ?? "",
    baseCalendarId: values.baseCalendarId ?? "", organizationalUnitId: values.organizationalUnitId ?? "",
    employmentType: values.employmentType ?? "", seniority: values.seniority ?? "", notes: values.notes ?? "",
  };
}

const optional = (value: string) => value.trim() || undefined;
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function resourceToForm(resource: ResourceDto): ResourceFormModel {
  return {
    name: resource.name ?? "", email: resource.email ?? "", phone: resource.phone ?? "", jobTitle: resource.jobTitle ?? "",
    employeeId: resource.employeeId ?? "", type: resource.type === "VendorResource" ? "VendorResource" : "Staff",
    status: resource.isActive ? "Active" : "Inactive", userId: resource.userId ?? "", vendorId: resource.vendorId ?? "",
    baseCalendarId: resource.baseCalendarId ?? "", organizationalUnitId: resource.primaryOrganizationalUnitId ?? "",
    employmentType: resource.employmentType ?? "", seniority: resource.seniority ?? "", notes: resource.notes ?? "",
  };
}

export function validateResourceForm(form: ResourceFormModel) {
  return {
    name: !form.name.trim(),
    email: Boolean(form.email.trim()) && !/^\S+@\S+\.\S+$/.test(form.email.trim()),
    vendor: form.type === "VendorResource" && !form.vendorId,
    baseCalendar: Boolean(form.baseCalendarId.trim()) && !guidPattern.test(form.baseCalendarId.trim()),
  };
}

export function toCreateResourcePayload(form: ResourceFormModel) {
  return {
    userId: optional(form.userId), name: form.name.trim(), email: optional(form.email), phone: optional(form.phone),
    jobTitle: optional(form.jobTitle), employeeId: optional(form.employeeId), type: form.type, status: form.status,
    employmentType: optional(form.employmentType), seniority: optional(form.seniority), notes: optional(form.notes),
    vendorId: form.type === "VendorResource" ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId), primaryOrganizationalUnitId: optional(form.organizationalUnitId),
  };
}

export function toUpdateResourcePayload(form: ResourceFormModel) {
  return {
    userId: optional(form.userId), removeUserAssociation: !form.userId, name: form.name.trim(), email: optional(form.email),
    phone: optional(form.phone), jobTitle: optional(form.jobTitle), employeeId: optional(form.employeeId), status: form.status,
    employmentType: optional(form.employmentType), seniority: optional(form.seniority), notes: optional(form.notes),
    vendorId: form.type === "VendorResource" ? optional(form.vendorId) : undefined,
    baseCalendarId: optional(form.baseCalendarId), primaryOrganizationalUnitId: optional(form.organizationalUnitId),
    isActive: form.status === "Active",
  };
}

