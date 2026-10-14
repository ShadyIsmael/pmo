import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { VendorsPage } from "../features/vendors/VendorsPage";

const mocks = vi.hoisted(() => ({
  useAuth: vi.fn(),
  useVendors: vi.fn(),
  useVendorCategories: vi.fn(),
  createHook: vi.fn(),
  updateHook: vi.fn(),
  deactivateHook: vi.fn(),
  reactivateHook: vi.fn(),
  deleteHook: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  deactivate: vi.fn(),
  reactivate: vi.fn(),
  remove: vi.fn(),
  resetCreate: vi.fn(),
  refetchVendors: vi.fn(),
  createError: null as Error | null,
  savedInput: undefined as unknown,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: mocks.useAuth }));
vi.mock("../features/vendors/api/vendors", () => ({
  useVendors: mocks.useVendors,
  useVendorCategories: mocks.useVendorCategories,
  useCreateVendor: mocks.createHook,
  useUpdateVendor: mocks.updateHook,
  useDeactivateVendor: mocks.deactivateHook,
  useReactivateVendor: mocks.reactivateHook,
  useDeleteVendor: mocks.deleteHook,
}));

const activeCategory = { id: "category-active", code: "TECH", name: "Technology", nameEn: "Technology", nameAr: "التقنية", isActive: true };
const inactiveCategory = { id: "category-legacy", code: "LEG", name: "Legacy", nameEn: "Legacy", nameAr: "قديمة", isActive: false };
    const vendor = {
  id: "vendor-1", name: "Northwind Supplies", code: "VND-001", contactName: "Sam Supplier", contactEmail: "sam@example.com", contactPhone: "+1 555 0100",
  vendorCategoryId: inactiveCategory.id, categoryName: inactiveCategory.nameEn, isActive: true,
};
const createdVendor = { id: "vendor-2", name: "Blue Supplier", code: "VND-100", vendorCategoryId: activeCategory.id, isActive: true };

function renderWithI18n(element: React.ReactElement) {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider>{element}</I18nProvider>);
}

describe("Vendors source parity", () => {
  beforeEach(() => {
    mocks.useAuth.mockReset().mockReturnValue({ user: { id: "user-1", permissions: ["Resource.View", "Resource.Create", "Resource.Edit", "Resource.Delete"] }, status: "authenticated" });
    mocks.useVendors.mockReset().mockReturnValue({ data: { items: [vendor] }, isLoading: false, isError: false, refetch: mocks.refetchVendors });
    mocks.useVendorCategories.mockReset().mockReturnValue({ data: [activeCategory, inactiveCategory], isLoading: false, isError: false });
    mocks.create.mockReset().mockImplementation((input: unknown, options?: { onSuccess?: (created: typeof createdVendor) => void }) => {
      mocks.savedInput = input;
      options?.onSuccess?.(createdVendor);
    });
    mocks.update.mockReset();
    mocks.deactivate.mockReset().mockResolvedValue(undefined);
    mocks.reactivate.mockReset().mockResolvedValue(undefined);
    mocks.remove.mockReset().mockResolvedValue(undefined);
    mocks.resetCreate.mockReset();
    mocks.refetchVendors.mockReset();
    mocks.createError = null;
    mocks.savedInput = undefined;
    mocks.createHook.mockImplementation(() => ({ mutate: mocks.create, isPending: false, error: mocks.createError, reset: mocks.resetCreate }));
    mocks.updateHook.mockReturnValue({ mutate: mocks.update, isPending: false, error: null });
    mocks.deactivateHook.mockReturnValue({ mutateAsync: mocks.deactivate, isPending: false });
    mocks.reactivateHook.mockReturnValue({ mutateAsync: mocks.reactivate, isPending: false });
    mocks.deleteHook.mockReturnValue({ mutateAsync: mocks.remove, isPending: false });
  });

  it("matches source view permission, category/status colors, and loads category-filter options", () => {
    const { rerender } = renderWithI18n(<VendorsPage />);

    expect(mocks.useVendorCategories).toHaveBeenCalledWith(true);
    expect(within(screen.getByRole("cell", { name: "Legacy" })).getByText("Legacy")).toHaveClass("text-info");
    expect(within(screen.getByRole("cell", { name: "Active" })).getByText("Active")).toHaveClass("text-success");
    expect(screen.getByRole("combobox", { name: "Filter by category" })).toHaveTextContent("All categories");

    mocks.useAuth.mockReturnValue({ user: { id: "manager-1", permissions: ["Resource.Manage"] }, status: "authenticated" });
    rerender(<I18nProvider><VendorsPage /></I18nProvider>);
    expect(screen.getByText("You do not have permission to add vendors.")).toBeInTheDocument();
  });

  it("keeps Add Vendor in a Side Sheet and shows the generated code after successful creation", async () => {
    renderWithI18n(<VendorsPage />);
    fireEvent.click(screen.getByRole("button", { name: "Add Vendor" }));

    const sheet = screen.getByRole("dialog", { name: "Add Vendor" });
    expect(within(sheet).getByLabelText("Code")).toBeDisabled();
    expect(within(sheet).queryByRole("option", { name: "Legacy" })).not.toBeInTheDocument();
    fireEvent.change(within(sheet).getByLabelText("Name *"), { target: { value: "  Blue Supplier  " } });
    fireEvent.change(within(sheet).getByLabelText("Category *"), { target: { value: activeCategory.id } });
    fireEvent.change(within(sheet).getByLabelText("Contact email"), { target: { value: "" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Create" }));

    const confirmation = await screen.findByRole("dialog", { name: "Vendor created" });
    expect(within(confirmation).getByText("VND-100")).toBeInTheDocument();
    expect(mocks.savedInput).toEqual({ name: "Blue Supplier", contactName: undefined, contactEmail: undefined, contactPhone: undefined, vendorCategoryId: activeCategory.id, contractValue: undefined, contractStart: undefined, contractEnd: undefined });
    const doneButtons = within(confirmation).getAllByRole("button", { name: "Done" });
    fireEvent.click(doneButtons[doneButtons.length - 1]);
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("shows validation and API failures inside the create Side Sheet", async () => {
    mocks.create.mockImplementation(() => undefined);
    mocks.createError = new Error("Vendor category is unavailable");
    renderWithI18n(<VendorsPage />);
    fireEvent.click(screen.getByRole("button", { name: "Add Vendor" }));
    let sheet = screen.getByRole("dialog", { name: "Add Vendor" });
    fireEvent.click(within(sheet).getByRole("button", { name: "Create" }));
    expect(await screen.findByText("Vendor name is required")).toBeInTheDocument();
    expect(screen.getByText("Vendor category is required.")).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();

    sheet = screen.getByRole("dialog", { name: "Add Vendor" });
    fireEvent.change(within(sheet).getByLabelText("Name *"), { target: { value: "Blue Supplier" } });
    fireEvent.change(within(sheet).getByLabelText("Category *"), { target: { value: activeCategory.id } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Create" }));
    sheet = screen.getByRole("dialog", { name: "Add Vendor" });
    expect(within(sheet).getByRole("alert")).toHaveTextContent("Vendor category is unavailable");
  });

  it("clears search/category/status filters through the source action", async () => {
    renderWithI18n(<VendorsPage />);
    fireEvent.change(screen.getByRole("textbox", { name: "Search vendors..." }), { target: { value: "north" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Filter by category" }), { target: { value: activeCategory.id } });
    fireEvent.change(screen.getByRole("combobox", { name: "Filter by status" }), { target: { value: "inactive" } });
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));

    await waitFor(() => expect(mocks.useVendors).toHaveBeenLastCalledWith(undefined, undefined, "all"));
  });

  it("shows a retry action on list failure and keeps the current inactive category selectable while editing", async () => {
    mocks.useVendors.mockReturnValue({ data: undefined, isLoading: false, isError: true, error: new Error("temporarily unavailable"), refetch: mocks.refetchVendors });
    const { rerender } = renderWithI18n(<VendorsPage />);
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(mocks.refetchVendors).toHaveBeenCalledOnce();

    mocks.useVendors.mockReturnValue({ data: { items: [vendor] }, isLoading: false, isError: false, refetch: mocks.refetchVendors });
    rerender(<I18nProvider><VendorsPage /></I18nProvider>);
    fireEvent.click(screen.getByRole("button", { name: "Actions" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Edit" }));
    expect(screen.getByRole("dialog", { name: "Edit Vendor" })).toBeInTheDocument();
    expect(screen.getByLabelText("Category *")).toHaveValue(inactiveCategory.id);
    const editSheet = screen.getByRole("dialog", { name: "Edit Vendor" });
    expect(within(editSheet).getByRole("option", { name: "Legacy" })).toBeInTheDocument();
  });
});
