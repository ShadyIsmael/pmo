import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { I18nProvider } from "../i18n/messages";
import { ResourcesListPage } from "../features/resources/ResourcesListPage";

const mocks = vi.hoisted(() => ({
  permissions: ["Resource.View", "Resource.Create", "Resource.Edit", "Resource.Delete"],
  canMutate: true,
  effectiveReadOnly: false,
  useResources: vi.fn(), useResource: vi.fn(), useBaseCalendars: vi.fn(), useCreateResource: vi.fn(), useUpdateResource: vi.fn(), useDeleteResource: vi.fn(), useCostProfiles: vi.fn(), useCreateCostProfile: vi.fn(),
  useResourceReferenceData: vi.fn(),
  useUsers: vi.fn(), useUnits: vi.fn(), useVendors: vi.fn(), useFinder: vi.fn(), useSkills: vi.fn(),
}));

vi.mock("../features/resources/api/resources", () => ({
  useResources: mocks.useResources, useResource: mocks.useResource, useBaseCalendars: mocks.useBaseCalendars,
  useResourceReferenceData: mocks.useResourceReferenceData,
  useCreateResource: mocks.useCreateResource, useUpdateResource: mocks.useUpdateResource, useDeleteResource: mocks.useDeleteResource,
  useCostProfiles: mocks.useCostProfiles, useCreateCostProfile: mocks.useCreateCostProfile,
  useResourceFinder: mocks.useFinder, useResourceSkills: mocks.useSkills,
}));
vi.mock("../features/organization/api/organization", () => ({ useOrganizationUnits: mocks.useUnits }));
vi.mock("../features/projects/lib/project-lookups", () => ({ useUserLookup: mocks.useUsers }));
vi.mock("../features/vendors/api/vendors", () => ({ useVendors: mocks.useVendors }));
vi.mock("../features/administration/api/admin", () => ({ useCurrencies: () => ({ data: [{ id: "currency-gbp", code: "GBP", name: "Pound Sterling", symbol: "£", exchangeRate: 1, isBase: true }], isLoading: false, isError: false, refetch: vi.fn() }) }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: mocks.canMutate, effectiveReadOnly: mocks.effectiveReadOnly }) }));

const resource = {
  id: "resource-1", name: "Morgan Resource", email: "morgan@example.com", employeeId: "EMP-1", employmentType: "Permanent", jobTitle: "Delivery Lead", type: "Staff", status: "Active", isActive: true,
  userId: "user-1", primaryOrganizationalUnitId: "unit-1", primaryOrganizationalUnitName: "Technology", allocations: [{ id: "allocation-1", projectName: "Core Upgrade", role: "Lead", allocationPercentage: 50, startDate: "2026-01-01", endDate: "2026-12-31", status: "Confirmed" }],
} as const;
const finderItem = { resourceId: "resource-1", resourceName: "Morgan Resource", resourceType: "Staff", matchScore: 94, availableCapacityHours: 32, currentLoadPercent: 45, skillMatches: [], conflictingAllocations: [] };

function renderPage() {
  return render(<I18nProvider><ResourcesListPage /></I18nProvider>);
}

describe("resource directory React parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.permissions = ["Resource.View", "Resource.Create", "Resource.Edit", "Resource.Delete"];
    mocks.canMutate = true;
    mocks.effectiveReadOnly = false;
    mocks.useResources.mockReset().mockReturnValue({ data: { items: [resource], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 }, isLoading: false, isError: false });
    mocks.useResource.mockReset().mockReturnValue({ data: resource, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useBaseCalendars.mockReset().mockReturnValue({ data: [{ id: "calendar-1", name: "Standard", isDefault: true }] });
    mocks.useResourceReferenceData.mockReset().mockReturnValue({ data: { resourceTypes: [{ code: "Staff", nameEn: "Staff", nameAr: "موظف", displayOrder: 1, isDefault: true, requiresVendor: false }, { code: "Vendor", nameEn: "Vendor", nameAr: "مورد", displayOrder: 2, isDefault: false, requiresVendor: true }], seniorityLevels: [{ code: "Senior", nameEn: "Senior", nameAr: "خبير", displayOrder: 3 }], compensationTypes: ["Hourly", "Weekly", "Monthly"] }, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useUsers.mockReset().mockReturnValue({ data: [{ id: "user-1", fullName: "Alex Admin", email: "admin@nexus.com" }] });
    mocks.useUnits.mockReset().mockReturnValue({ data: [{ id: "unit-1", name: "Technology", code: "TECH", isActive: true }] });
    mocks.useVendors.mockReset().mockReturnValue({ data: { items: [{ id: "vendor-1", name: "Vendor One", isActive: true }] } });
    mocks.useFinder.mockReset().mockReturnValue({ data: { items: [finderItem], totalCount: 1 }, isLoading: false, isError: false });
    mocks.useSkills.mockReset().mockReturnValue({ data: [{ id: "skill-1", name: "Planning", isActive: true }] });
    mocks.useCreateResource.mockReset().mockReturnValue({ mutate: vi.fn(), reset: vi.fn(), isPending: false, error: null });
    mocks.useUpdateResource.mockReset().mockReturnValue({ mutate: vi.fn(), mutateAsync: vi.fn(), reset: vi.fn(), isPending: false, error: null });
    mocks.useDeleteResource.mockReset().mockReturnValue({ mutate: vi.fn(), mutateAsync: vi.fn(), isPending: false, error: null });
    mocks.useCostProfiles.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });
    mocks.useCreateCostProfile.mockReset().mockReturnValue({ mutateAsync: vi.fn(), reset: vi.fn(), isPending: false, error: null });
  });

  it("shows authoritative directory fields, localized filters, and source pagination", () => {
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "Resource Directory" })).toBeInTheDocument();
    expect(screen.getByText("morgan@example.com")).toBeInTheDocument();
    expect(screen.getByText("Technology")).toBeInTheDocument();
    expect(screen.getByText("Yes")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next" })).toBeDisabled();
  });

  it("filters the API list by the selected linked system user", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "System User" }));
    fireEvent.click(screen.getByRole("option", { name: "Alex Admin · admin@nexus.com" }));
    expect(mocks.useResources).toHaveBeenCalledWith(expect.objectContaining({ userId: "user-1", page: 1 }), true);
  });

  it("opens server-backed details with current allocations", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Morgan Resource" }));
    expect(screen.getByRole("dialog", { name: "Morgan Resource" })).toBeInTheDocument();
    expect(screen.getByText("Core Upgrade")).toBeInTheDocument();
    expect(screen.getByText("Current allocations")).toBeInTheDocument();
  });

  it("creates an effective-dated resource cost profile only for rate managers", async () => {
    mocks.permissions = ["Resource.View", "Resource.ViewCostRate", "Resource.EditCostRate"];
    const createMutation = { mutateAsync: vi.fn().mockResolvedValue({ id: "rate-1" }), reset: vi.fn(), isPending: false, error: null };
    mocks.useCreateCostProfile.mockReturnValue(createMutation);
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Morgan Resource" }));
    fireEvent.click(screen.getByRole("button", { name: "Cost profiles" }));
    const sheet = screen.getByRole("dialog", { name: "Cost profiles · Morgan Resource" });
    fireEvent.change(within(sheet).getByLabelText("Rate amount"), { target: { value: "100" } });
    fireEvent.change(within(sheet).getByLabelText("Effective from"), { target: { value: "2026-10-01" } });
    fireEvent.change(within(sheet).getByLabelText("Effective to"), { target: { value: "2026-10-31" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Add rate" }));

    await waitFor(() => expect(createMutation.mutateAsync).toHaveBeenCalledWith({
      resourceId: "resource-1",
      body: { amount: 100, compensationType: "Hourly", currency: "GBP", effectiveFrom: "2026-10-01", effectiveTo: "2026-10-31", overtimeEligible: false },
    }));
  });

  it("does not expose cost-rate profile actions without rate access", () => {
    mocks.permissions = ["Resource.View"];
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Morgan Resource" }));

    expect(screen.queryByRole("button", { name: "Cost profiles" })).not.toBeInTheDocument();
  });

  it("opens Add Resource with grouped fields and omits manual HR-only identifiers", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Add Resource" }));
    const sheet = screen.getByRole("dialog", { name: "Create Resource" });
    expect(sheet.querySelector(".absolute.inset-y-0")).toBeTruthy();
    expect(within(sheet).getByRole("heading", { name: "Resource information" })).toBeInTheDocument();
    expect(within(sheet).getByRole("heading", { name: "Professional profile" })).toBeInTheDocument();
    expect(within(sheet).getByRole("heading", { name: "Organization and planning" })).toBeInTheDocument();
    expect(within(sheet).getByLabelText("System User")).toBeInTheDocument();
    expect(within(sheet).getByLabelText("Organizational unit")).toBeInTheDocument();
    expect(within(sheet).getByLabelText("Base calendar")).toBeInTheDocument();
    expect(within(sheet).queryByLabelText("Employee ID")).not.toBeInTheDocument();
    expect(within(sheet).queryByLabelText("Employment Type")).not.toBeInTheDocument();
    expect(within(sheet).getByLabelText("Name")).toBeRequired();
  });

  it("opens Edit Resource without exposing persisted HR-only fields", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Morgan Resource" }));
    fireEvent.click(screen.getByRole("button", { name: "Edit Resource" }));
    const sheet = screen.getByRole("dialog", { name: "Edit Resource" });

    expect(within(sheet).getByLabelText("Name")).toHaveValue("Morgan Resource");
    expect(within(sheet).queryByLabelText("Employee ID")).not.toBeInTheDocument();
    expect(within(sheet).queryByLabelText("Employment Type")).not.toBeInTheDocument();
  });

  it("renders localized required and optional cues in Arabic RTL", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "إضافة مورد" }));
    const sheet = screen.getByRole("dialog", { name: "إنشاء مورد" });
    expect(document.documentElement.dir).toBe("rtl");
    expect(within(sheet).getByRole("heading", { name: "معلومات المورد" })).toBeInTheDocument();
    expect(within(sheet).getByLabelText("الاسم")).toBeRequired();
    expect(within(sheet).getAllByText("اختياري").length).toBeGreaterThan(0);
    expect(within(sheet).queryByLabelText("معرف الموظف")).not.toBeInTheDocument();
    expect(within(sheet).queryByLabelText("نوع التوظيف")).not.toBeInTheDocument();
  });

  it("shows a failed create response inside the open Add Resource side sheet", () => {
    const createMutation = { mutate: vi.fn(), reset: vi.fn(), isPending: false, error: null as Error | null };
    mocks.useCreateResource.mockReturnValue(createMutation);
    const { rerender } = renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Add Resource" }));
    const sheet = screen.getByRole("dialog", { name: "Create Resource" });
    fireEvent.change(within(sheet).getByLabelText("Name"), { target: { value: "New Resource" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Create" }));
    expect(createMutation.mutate).toHaveBeenCalledTimes(1);

    createMutation.error = new Error("The resource could not be saved.");
    rerender(<I18nProvider><ResourcesListPage /></I18nProvider>);

    expect(within(sheet).getByRole("alert")).toHaveTextContent("The resource could not be saved.");
    expect(screen.getAllByRole("alert")).toHaveLength(1);
  });

  it("surfaces a failed resource deactivation after closing its confirmation dialog", async () => {
    const deleteMutation = {
      mutate: vi.fn(),
      mutateAsync: vi.fn().mockRejectedValue(new Error("The resource is still assigned to active work.")),
      isPending: false,
      error: null as Error | null,
    };
    mocks.useDeleteResource.mockReturnValue(deleteMutation);
    const { rerender } = renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Deactivate" }));
    const confirmation = screen.getByRole("alertdialog");
    fireEvent.click(within(confirmation).getByRole("button", { name: "Deactivate" }));
    await waitFor(() => expect(deleteMutation.mutateAsync).toHaveBeenCalledWith(resource.id));

    deleteMutation.error = new Error("The resource is still assigned to active work.");
    rerender(<I18nProvider><ResourcesListPage /></I18nProvider>);

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("The resource is still assigned to active work.");
  });

  it("switches the side-sheet form to vendor-specific association fields", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Add Resource" }));
    const sheet = screen.getByRole("dialog", { name: "Create Resource" });
    fireEvent.click(within(sheet).getByLabelText("Resource Type"));
    fireEvent.click(screen.getByRole("option", { name: "Vendor" }));
    expect(within(sheet).getByLabelText("Vendor")).toBeInTheDocument();
    expect(within(sheet).queryByLabelText("Employee ID")).not.toBeInTheDocument();
    expect(within(sheet).queryByLabelText("System User")).not.toBeInTheDocument();
  });

  it("searches live resource matches by role and exposes the availability filters", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Find Resource" }));
    expect(screen.getByRole("dialog", { name: "Resource Finder" })).toBeInTheDocument();
    expect(screen.getByText("Planning")).toBeInTheDocument();
    fireEvent.change(screen.getByRole("textbox", { name: "Role" }), { target: { value: "Developer" } });
    expect(mocks.useFinder).toHaveBeenLastCalledWith({ role: "Developer" }, true);
    expect(screen.getAllByText("Morgan Resource").length).toBeGreaterThan(1);
  });

  it("does not load protected data without view permission and suppresses mutations in read-only mode", () => {
    mocks.permissions = [];
    const denied = renderPage();
    expect(screen.getByText("Resource access required")).toBeInTheDocument();
    expect(mocks.useResources).toHaveBeenLastCalledWith(expect.any(Object), false);
    denied.unmount();

    mocks.permissions = ["Resource.View", "Resource.Create", "Resource.Edit", "Resource.Delete"];
    mocks.effectiveReadOnly = true;
    renderPage();
    expect(screen.queryByRole("button", { name: "Add Resource" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit Resource" })).not.toBeInTheDocument();
  });
});
