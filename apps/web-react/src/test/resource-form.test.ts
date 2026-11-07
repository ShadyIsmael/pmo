import { describe, expect, it } from "vitest";
import { dialogValuesToResourceForm, emptyResourceForm, parseLocalizedHours, resourceToForm, toCreateResourcePayload, toUpdateResourcePayload, validateResourceForm } from "../features/resources/resource-form";

const resourceTypes = [
  { code: "Staff", nameEn: "Staff", nameAr: "موظف", displayOrder: 1, isDefault: true, requiresVendor: false },
  { code: "Vendor", nameEn: "Vendor", nameAr: "مورد", displayOrder: 2, isDefault: false, requiresVendor: true },
];

describe("resource directory form mapping", () => {
  it("maps the directory control names to the API create contract", () => {
    const form = dialogValuesToResourceForm({
      name: "  Dana Lee  ", email: "dana@example.com", resourceType: "Vendor", status: "Active", systemUserId: "user-1",
      vendorId: "vendor-1", baseCalendarId: "calendar-1", organizationalUnitId: "unit-1", employeeId: "HR-100", employmentType: "Contract", seniority: "Senior", notes: "External delivery lead",
    }, resourceTypes);

    expect(toCreateResourcePayload(form)).toEqual({
      userId: "user-1", name: "Dana Lee", email: "dana@example.com", phone: undefined, jobTitle: undefined,
      type: "Vendor", status: "Active", seniority: "Senior", notes: "External delivery lead",
      vendorId: "vendor-1", baseCalendarId: "calendar-1", primaryOrganizationalUnitId: "unit-1",
    });
  });

  it("keeps the resource type immutable on update and supports clearing the user association", () => {
    const form = dialogValuesToResourceForm({ name: "Sam", resourceType: "Vendor", status: "Inactive", systemUserId: "" }, resourceTypes);
    expect(toUpdateResourcePayload(form)).toMatchObject({ name: "Sam", status: "Inactive", isActive: false, removeUserAssociation: true });
    expect(toUpdateResourcePayload(form)).not.toHaveProperty("type");
  });

  it("hydrates canonical values and validates required vendor and calendar references", () => {
    expect(resourceToForm({ id: "r1", name: "Sam", type: "Staff", status: "Inactive", isActive: false } as never, resourceTypes)).toMatchObject({ name: "Sam", status: "Inactive", type: "Staff" });
    expect(validateResourceForm({ ...emptyResourceForm, name: "  ", type: "Vendor", requiresVendor: true, baseCalendarId: "not-a-guid" })).toMatchObject({ name: true, type: false, vendor: true, baseCalendar: true });
  });

  it("enforces SQL-aligned text limits and omits legacy HR fields from edit payloads", () => {
    const form = { ...emptyResourceForm, name: "N".repeat(201), phone: "1".repeat(51), seniority: "S".repeat(51), notes: "x".repeat(2001) };
    expect(validateResourceForm(form)).toMatchObject({ nameTooLong: true, phoneTooLong: true, seniorityTooLong: true, notesTooLong: true });
    expect(toUpdateResourcePayload(dialogValuesToResourceForm({ name: "Sam", employeeId: "HR-100", employmentType: "Permanent" }))).not.toHaveProperty("employeeId");
    expect(toUpdateResourcePayload(dialogValuesToResourceForm({ name: "Sam", employeeId: "HR-100", employmentType: "Permanent" }))).not.toHaveProperty("employmentType");
  });

  it("accepts inclusive 1–24 daily-hour bounds and rejects blank or invalid input", () => {
    const base = { ...emptyResourceForm, name: "Staff member", type: "Staff", userId: "user-1" };

    for (const hoursPerDay of ["1", "8", "24", "1.25", "١٫٥"]) {
      expect(validateResourceForm({ ...base, hoursPerDay }).hoursPerDay).toBe(false);
    }

    for (const hoursPerDay of ["", "0", "0.99", "25", "not a number"]) {
      expect(validateResourceForm({ ...base, hoursPerDay }).hoursPerDay).toBe(true);
    }
  });

  it("parses localized digits and serializes the same hours for create and update", () => {
    expect(parseLocalizedHours("1.5")).toBe(1.5);
    expect(parseLocalizedHours("١٫٥")).toBe(1.5);
    expect(parseLocalizedHours("۱٫۵")).toBe(1.5);
    expect(parseLocalizedHours("")).toBeNull();
    expect(parseLocalizedHours("hours")).toBeNaN();

    const form = dialogValuesToResourceForm({
      name: "Staff member",
      resourceType: "Staff",
      systemUserId: "user-1",
      hoursPerDay: "١٫٥",
      hoursPerWeek: "٧٫٥",
    }, resourceTypes);

    expect(toCreateResourcePayload(form)).toMatchObject({ hoursPerDay: 1.5, hoursPerWeek: 7.5, userId: "user-1" });
    expect(toUpdateResourcePayload(form)).toMatchObject({ hoursPerDay: 1.5, hoursPerWeek: 7.5, userId: "user-1", clearHoursPerWeek: false });
  });
});
