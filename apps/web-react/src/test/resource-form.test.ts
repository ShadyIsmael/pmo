import { describe, expect, it } from "vitest";
import { dialogValuesToResourceForm, emptyResourceForm, resourceToForm, toCreateResourcePayload, toUpdateResourcePayload, validateResourceForm } from "../features/resources/resource-form";

describe("resource directory form mapping", () => {
  it("maps the directory control names to the API create contract", () => {
    const form = dialogValuesToResourceForm({
      name: "  Dana Lee  ", email: "dana@example.com", resourceType: "VendorResource", status: "Active", systemUserId: "user-1",
      vendorId: "vendor-1", baseCalendarId: "calendar-1", organizationalUnitId: "unit-1", employmentType: "Contract", seniority: "Senior", notes: "External delivery lead",
    });

    expect(toCreateResourcePayload(form)).toEqual({
      userId: "user-1", name: "Dana Lee", email: "dana@example.com", phone: undefined, jobTitle: undefined, employeeId: undefined,
      type: "VendorResource", status: "Active", employmentType: "Contract", seniority: "Senior", notes: "External delivery lead",
      vendorId: "vendor-1", baseCalendarId: "calendar-1", primaryOrganizationalUnitId: "unit-1",
    });
  });

  it("keeps the resource type immutable on update and supports clearing the user association", () => {
    const form = dialogValuesToResourceForm({ name: "Sam", resourceType: "VendorResource", status: "Inactive", systemUserId: "" });
    expect(toUpdateResourcePayload(form)).toMatchObject({ name: "Sam", status: "Inactive", isActive: false, removeUserAssociation: true });
    expect(toUpdateResourcePayload(form)).not.toHaveProperty("type");
  });

  it("hydrates canonical values and validates required vendor and calendar references", () => {
    expect(resourceToForm({ id: "r1", name: "Sam", type: "Staff", status: "Inactive", isActive: false } as never)).toMatchObject({ name: "Sam", status: "Inactive", type: "Staff" });
    expect(validateResourceForm({ ...emptyResourceForm, name: "  ", type: "VendorResource", baseCalendarId: "not-a-guid" })).toMatchObject({ name: true, vendor: true, baseCalendar: true });
  });
});
