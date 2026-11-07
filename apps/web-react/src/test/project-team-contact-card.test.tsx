import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { I18nProvider } from "../i18n/messages";
import { ProjectTeamContactCard } from "../features/projects/ProjectTeamContactCard";

const fixtures = vi.hoisted(() => ({
  resources: {} as Record<string, Record<string, unknown>>,
}));

vi.mock("@features/projects/api/project-team-contact", () => ({
  useProjectTeamContactResource: (resourceId: string) => ({
    data: fixtures.resources[resourceId],
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  }),
}));

vi.mock("@features/resources/api/resources", () => ({
  useResourceReferenceData: () => ({
    data: {
      resourceTypes: [
        { code: "Staff", nameEn: "Staff", nameAr: "موظف", displayOrder: 1, isDefault: true, requiresVendor: false },
        { code: "Vendor", nameEn: "Vendor", nameAr: "مورد", displayOrder: 2, isDefault: false, requiresVendor: true },
      ],
      seniorityLevels: [],
      compensationTypes: [],
    },
    isLoading: false,
    isError: false,
  }),
}));

const person = {
  id: "member-1",
  projectId: "project-1",
  resourceId: "resource-1",
  name: "Alex Rivera",
  email: "alex@example.com",
  jobTitle: "Engineer",
  resourceType: "Staff",
  hasSystemUser: true,
  source: "TeamMember",
  relationshipType: "TeamMember",
  status: "Active",
  role: "ProjectManager",
  isLegacy: false,
  rowVersion: "v1",
  createdAt: "2026-10-01T00:00:00Z",
  functionalRoles: [{ id: "role-1", code: "LEAD", nameEn: "Delivery Lead", nameAr: "قائد التسليم", isPrimary: true }],
};

function renderCard() {
  return render(
    <I18nProvider>
      <ProjectTeamContactCard person={person} open onOpenChange={() => undefined} />
    </I18nProvider>,
  );
}

describe("Project Team contact card", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.resources = {
      "resource-1": {
        id: "resource-1",
        name: "Alex Rivera",
        jobTitle: "Engineer",
        email: "alex@example.com",
        phone: "+1 (555) 010-2020",
        type: "Staff",
        isActive: true,
        primaryOrganizationalUnitId: "org-unit-1",
        primaryOrganizationalUnitName: "Engineering",
        primaryManagerResourceId: "resource-manager-1",
      },
      "resource-manager-1": {
        id: "resource-manager-1",
        name: "Morgan Lee",
        email: "morgan@example.com",
        type: "Staff",
        isActive: true,
      },
    };
  });

  it("shows canonical Resource contact details and the configured manager with usable links", () => {
    renderCard();

    const dialog = screen.getByRole("dialog", { name: "Contact card" });
    expect(within(dialog).getByText("Alex Rivera")).toBeInTheDocument();
    expect(dialog).toHaveTextContent("Delivery Lead");
    expect(dialog).toHaveTextContent("Resource type: Staff");
    expect(within(dialog).getByRole("link", { name: "alex@example.com" })).toHaveAttribute("href", "mailto:alex@example.com");
    expect(within(dialog).getByRole("link", { name: "+1 (555) 010-2020" })).toHaveAttribute("href", "tel:+15550102020");
    expect(within(dialog).getByText("Engineering")).toBeInTheDocument();
    expect(within(dialog).getByText("Morgan Lee")).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: "morgan@example.com" })).toHaveAttribute("href", "mailto:morgan@example.com");
    expect(dialog).not.toHaveTextContent("resource-manager-1");
  });

  it("shows the canonical vendor name with the semantic vendor tone for vendor resources", () => {
    fixtures.resources["resource-1"] = {
      ...fixtures.resources["resource-1"],
      type: "Vendor",
      vendorId: "vendor-1",
      vendorName: "Northwind Services",
      primaryOrganizationalUnitId: undefined,
      primaryOrganizationalUnitName: undefined,
    };

    renderCard();

    const dialog = screen.getByRole("dialog", { name: "Contact card" });
    const vendor = within(dialog).getByText("Northwind Services");
    expect(vendor.closest("dd")).toHaveClass("text-warning");
    expect(dialog).toHaveTextContent("Vendor");
    expect(dialog).not.toHaveTextContent("vendor-1");
    expect(within(dialog).getByText("No administrative entity is assigned to this resource.")).toBeInTheDocument();
  });

  it("does not infer manager details when the Resource has no administrative association", () => {
    fixtures.resources["resource-1"] = {
      ...fixtures.resources["resource-1"],
      primaryOrganizationalUnitId: undefined,
      primaryOrganizationalUnitName: undefined,
      primaryManagerResourceId: "resource-manager-1",
    };

    renderCard();

    const dialog = screen.getByRole("dialog", { name: "Contact card" });
    expect(within(dialog).getByText("No administrative entity is assigned to this resource.")).toBeInTheDocument();
    expect(within(dialog).queryByText("Morgan Lee")).not.toBeInTheDocument();
    expect(within(dialog).queryByRole("link", { name: "morgan@example.com" })).not.toBeInTheDocument();
  });

  it("localizes the card and administrative empty state in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.resources["resource-1"] = {
      ...fixtures.resources["resource-1"],
      primaryOrganizationalUnitId: undefined,
      primaryOrganizationalUnitName: undefined,
      primaryManagerResourceId: undefined,
    };

    renderCard();

    const dialog = screen.getByRole("dialog", { name: "بطاقة الاتصال" });
    expect(within(dialog).getByText("الهاتف المحمول")).toBeInTheDocument();
    expect(within(dialog).getByText("لا توجد جهة إدارية مرتبطة بهذا المورد.")).toBeInTheDocument();
  });
});
