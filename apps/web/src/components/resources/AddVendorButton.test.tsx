import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AddVendorButton } from "@/components/resources/AddVendorButton";
import { EditVendorButton } from "@/components/resources/EditVendorButton";
import { vendorKeys } from "@/lib/api/resources";

const { mockGet, mockPost, mockPut, mockInvalidate } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockInvalidate: vi.fn(),
}));

vi.mock("@/lib/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/client")>();
  return {
    ...actual,
    api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut },
  };
});

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut },
  };
});

vi.mock("@tanstack/react-query", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-query")>();
  return {
    ...actual,
    useQueryClient: () => ({ invalidateQueries: mockInvalidate }),
  };
});

vi.mock("@/lib/auth", () => ({
  useAuth: () => ({
    status: "authenticated",
    user: { permissions: ["Resource.Manage", "Resource.View", "resources.read"] },
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  }),
}));

vi.mock("next-intl", async () => {
  const { default: en } = await import("@/messages/en.json");
  const { default: ar } = await import("@/messages/ar.json");
  const pick = (messages: unknown, fullKey: string): string => {
    const value = fullKey.split(".").reduce<unknown>(
      (acc, part) => (acc && typeof acc === "object" ? (acc as Record<string, unknown>)[part] : undefined),
      messages
    );
    return typeof value === "string" ? value : fullKey;
  };
  let locale = "en";
  return {
    useTranslations: (namespace?: string) => (key: string, values?: Record<string, unknown>) => {
      const messages = locale === "ar" ? ar : en;
      const raw = namespace ? pick(messages, `${namespace}.${key}`) : pick(messages, key);
      if (!values) return raw;
      return Object.entries(values).reduce(
        (text, [name, value]) => text.replace(new RegExp(`\\{${name}\\}`, "g"), String(value)),
        raw
      );
    },
    useLocale: () => locale,
    __setLocale: (next: string) => {
      locale = next;
    },
  };
});

const categories = [
  { id: "cat-sup", code: "SUP", nameEn: "Supplier", nameAr: "مورّد", displayOrder: 1, isActive: true },
  { id: "cat-con", code: "CON", nameEn: "Consultant", nameAr: "مستشار", displayOrder: 2, isActive: true },
  { id: "cat-old", code: "OLD", nameEn: "Legacy Vendor", nameAr: "مورّد قديم", displayOrder: 3, isActive: false },
];

const activeVendor = {
  id: "v1",
  name: "CloudNova",
  code: "VND-000042",
  contactName: "Ada",
  contactEmail: "ada@cloudnova.example",
  contactPhone: "+1000",
  vendorCategoryId: "cat-sup",
  categoryName: "Supplier",
  status: "Active",
  isActive: true,
};

const inactiveCategoryVendor = {
  id: "v2",
  name: "Old Tools Inc",
  code: "VND-000007",
  vendorCategoryId: "cat-old",
  categoryName: "Legacy Vendor",
  status: "Active",
  isActive: true,
};

const mockGetFn = mockGet as unknown as Mock;
const mockPostFn = mockPost as unknown as Mock;
const mockPutFn = mockPut as unknown as Mock;
const mockInvalidateFn = mockInvalidate as unknown as Mock;

function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

async function openAddVendor(user: ReturnType<typeof userEvent.setup>) {
  renderWithProviders(<AddVendorButton />);
  await user.click(await screen.findByRole("button", { name: /^Add Vendor$/i }));
  await screen.findByLabelText(/^Name$/i);
}

beforeEach(() => {
  mockInvalidateFn.mockReset();
  mockGetFn.mockReset();
  mockPostFn.mockReset();
  mockPutFn.mockReset();
  mockGetFn.mockImplementation((path: string) => {
    if (path.includes("/api/reference-data/vendor-categories")) return Promise.resolve(categories);
    if (path === "/api/vendors") return Promise.resolve({ items: [activeVendor] });
    if (path.includes("/api/vendors/v2")) return Promise.resolve(inactiveCategoryVendor);
    if (path.includes("/api/vendors/")) return Promise.resolve(activeVendor);
    return Promise.resolve(null);
  });
  mockPostFn.mockImplementation((_path: string, body: unknown) =>
    Promise.resolve({ id: "v-new", code: "VND-000123", ...(body as object) })
  );
  mockPutFn.mockImplementation((_path: string, body: unknown) =>
    Promise.resolve({ ...activeVendor, ...(body as object) })
  );
});

afterEach(async () => {
  cleanup();
  const intl = (await import("next-intl")) as unknown as { __setLocale?: (l: string) => void };
  intl.__setLocale?.("en");
});

describe("Add Vendor spec", () => {
  it("1. Vendor Code is not manually required (disabled + auto-generated hint)", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    const codeInput = screen.getByLabelText(/^Code$/i) as HTMLInputElement;
    expect(codeInput).toBeDisabled();
    expect(codeInput).toHaveAttribute("readonly");
    expect(codeInput.value).toBe("");
    expect(screen.getByText(/assigned automatically/i)).toBeInTheDocument();
  });

  it("2. Create submits without code; backend generates unique VND-xxxxxx", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Northwind Labs");
    await user.selectOptions(screen.getByLabelText(/^Category$/i), "cat-sup");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockPostFn).toHaveBeenCalledTimes(1));
    const [path, body] = mockPostFn.mock.calls[0] as [string, Record<string, unknown>];
    expect(path).toBe("/api/vendors");
    expect(body).not.toHaveProperty("code");
    expect(body.name).toBe("Northwind Labs");
    expect(body.vendorCategoryId).toBe("cat-sup");
    // The sheet deliberately stays open after a successful create so the
    // system-assigned vendor code can be shown to the user.
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeInTheDocument());
  });

  it("3. Category options come from VendorCategory reference data", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    const select = screen.getByLabelText(/^Category$/i) as HTMLSelectElement;
    const values = Array.from(select.options).map((option) => option.value);
    expect(values).toContain("cat-sup");
    expect(values).toContain("cat-con");
    expect(screen.getByRole("option", { name: "Supplier" })).toBeInTheDocument();
    expect(mockGetFn).toHaveBeenCalledWith(
      "/api/reference-data/vendor-categories",
      expect.objectContaining({ activeOnly: false })
    );
  });

  it("4. Inactive categories are excluded from the create dropdown", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    const select = screen.getByLabelText(/^Category$/i) as HTMLSelectElement;
    const values = Array.from(select.options).map((option) => option.value);
    expect(values).not.toContain("cat-old");
    expect(screen.queryByRole("option", { name: "Legacy Vendor" })).not.toBeInTheDocument();
  });

  it("5. Existing vendor keeps currently assigned inactive category when editing", async () => {
    const user = userEvent.setup();
    renderWithProviders(<EditVendorButton vendor={inactiveCategoryVendor} />);
    await user.click(await screen.findByRole("button", { name: /Edit|تعديل/i }));

    await waitFor(() => {
      expect((screen.getByLabelText(/^Code$/i) as HTMLInputElement).value).toBe("VND-000007");
    });

    const select = screen.getByLabelText(/^Category$/i) as HTMLSelectElement;
    await waitFor(() => expect(select.value).toBe("cat-old"));
    const values = Array.from(select.options).map((option) => option.value);
    expect(values).toContain("cat-old");
    expect(values).toContain("cat-sup");
    expect(screen.getByRole("option", { name: "Legacy Vendor" })).toBeInTheDocument();
  });

  it("6. Create payload includes vendorCategoryId", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Acme PMO");
    await user.selectOptions(screen.getByLabelText(/^Category$/i), "cat-con");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockPostFn).toHaveBeenCalledTimes(1));
    const body = mockPostFn.mock.calls[0]![1] as Record<string, unknown>;
    expect(body.vendorCategoryId).toBe("cat-con");
    expect(typeof body.vendorCategoryId).toBe("string");
  });

  it("7. Successful create invalidates the vendors list query", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Refresh Co");
    await user.selectOptions(screen.getByLabelText(/^Category$/i), "cat-sup");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockPostFn).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(mockInvalidateFn).toHaveBeenCalledWith({ queryKey: vendorKeys.all })
    );
  });

  it("8. API error is visible to the user", async () => {
    mockPostFn.mockRejectedValueOnce(new Error("Vendor code conflict"));
    const user = userEvent.setup();
    await openAddVendor(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Conflict Co");
    await user.selectOptions(screen.getByLabelText(/^Category$/i), "cat-sup");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Vendor code conflict");
  });

  it("9. Edit shows a read-only vendor code", async () => {
    const user = userEvent.setup();
    renderWithProviders(<EditVendorButton vendor={activeVendor} />);
    await user.click(await screen.findByRole("button", { name: /Edit|تعديل/i }));

    const codeInput = (await screen.findByLabelText(/^Code$/i)) as HTMLInputElement;
    expect(codeInput).toBeDisabled();
    expect(codeInput).toHaveAttribute("readonly");
    expect(codeInput.value).toBe("VND-000042");
    expect(screen.getByText(/cannot be edited/i)).toBeInTheDocument();
  });

  it("10. EN translation keys resolve (not raw keys)", async () => {
    const intl = (await import("next-intl")) as unknown as { __setLocale: (l: string) => void };
    intl.__setLocale("en");
    const user = userEvent.setup();
    await openAddVendor(user);

    expect(screen.getByText("Basic information")).toBeInTheDocument();
    expect(screen.getByText("Contact information")).toBeInTheDocument();
    expect(screen.getByText("Business information")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Auto-generated")).toBeInTheDocument();
    expect(screen.getByText(/assigned automatically/i)).toBeInTheDocument();
    expect(screen.queryByText("resources.vendors.autoGenerated")).not.toBeInTheDocument();
    expect(screen.queryByText("resources.vendors.basicInformation")).not.toBeInTheDocument();
  });

  it("11. AR translation keys resolve (not raw keys)", async () => {
    const intl = (await import("next-intl")) as unknown as { __setLocale: (l: string) => void };
    intl.__setLocale("ar");
    const user = userEvent.setup();
    renderWithProviders(<AddVendorButton />);
    await user.click(await screen.findByRole("button", { name: /^إضافة مورد$/i }));
    await screen.findByLabelText(/^الاسم$/i);

    expect(screen.getByText("المعلومات الأساسية")).toBeInTheDocument();
    expect(screen.getByText("معلومات الاتصال")).toBeInTheDocument();
    expect(screen.queryByText("resources.vendors.basicInformation")).not.toBeInTheDocument();
    expect(screen.queryByText("Basic information")).not.toBeInTheDocument();
  });

  it("12. The system-assigned vendor code is shown to the user after create", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Northwind Labs");
    await user.selectOptions(screen.getByLabelText(/^Category$/i), "cat-sup");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    expect(await screen.findByText("VND-000123")).toBeInTheDocument();
    expect(screen.getByText("Vendor code")).toBeInTheDocument();
    expect(screen.getByText("Vendor created")).toBeInTheDocument();
  });

  it("13. Done closes the sheet after the code is shown", async () => {
    const user = userEvent.setup();
    await openAddVendor(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Northwind Labs");
    await user.selectOptions(screen.getByLabelText(/^Category$/i), "cat-sup");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await screen.findByText("VND-000123");
    await user.click(screen.getByRole("button", { name: /^Done$/i }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });
});
