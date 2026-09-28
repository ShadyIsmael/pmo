import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { VendorRowActions } from "@/components/resources/VendorRowActions";
import { vendorKeys } from "@/lib/api/resources";

const { mockGet, mockPost, mockPut, mockDelete, mockInvalidate } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
  mockInvalidate: vi.fn(),
}));

vi.mock("@/lib/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/client")>();
  return {
    ...actual,
    api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut, delete: mockDelete },
  };
});

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut, delete: mockDelete },
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

const activeVendor = {
  id: "v-active",
  name: "CloudNova",
  code: "VND-000042",
  vendorCategoryId: "cat-sup",
  categoryName: "Supplier",
  status: "Active",
  isActive: true,
};

const inactiveVendor = {
  id: "v-inactive",
  name: "Old Tools Inc",
  code: "VND-000007",
  vendorCategoryId: "cat-sup",
  categoryName: "Supplier",
  status: "Inactive",
  isActive: false,
};

const mockPutFn = mockPut as unknown as Mock;
const mockDeleteFn = mockDelete as unknown as Mock;
const mockInvalidateFn = mockInvalidate as unknown as Mock;

function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

async function openMenu(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: /^Actions$/i }));
}

beforeEach(() => {
  mockInvalidateFn.mockReset();
  (mockGet as unknown as Mock).mockReset();
  (mockGet as unknown as Mock).mockResolvedValue([]);
  (mockPost as unknown as Mock).mockReset();
  mockPutFn.mockReset();
  mockDeleteFn.mockReset();
  mockPutFn.mockImplementation((path: string) => {
    if (path.endsWith("/deactivate")) return Promise.resolve({ ...activeVendor, isActive: false, status: "Inactive" });
    if (path.endsWith("/reactivate")) return Promise.resolve({ ...inactiveVendor, isActive: true, status: "Active" });
    return Promise.resolve(activeVendor);
  });
  mockDeleteFn.mockResolvedValue(undefined);
});

afterEach(async () => {
  cleanup();
  const intl = (await import("next-intl")) as unknown as { __setLocale?: (l: string) => void };
  intl.__setLocale?.("en");
});

describe("Vendor lifecycle actions", () => {
  it("active vendor menu offers Edit, Deactivate and Delete (no Reactivate)", async () => {
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={activeVendor} />);
    await openMenu(user);

    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: /^Edit$/i })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^Deactivate$/i })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^Delete$/i })).toBeInTheDocument();
    expect(within(menu).queryByRole("menuitem", { name: /^Reactivate$/i })).not.toBeInTheDocument();
  });

  it("inactive vendor menu offers Edit, Reactivate and Delete (no Deactivate)", async () => {
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={inactiveVendor} />);
    await openMenu(user);

    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: /^Edit$/i })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^Reactivate$/i })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^Delete$/i })).toBeInTheDocument();
    expect(within(menu).queryByRole("menuitem", { name: /^Deactivate$/i })).not.toBeInTheDocument();
  });

  it("deactivate confirmation is explicit and calls the deactivate endpoint", async () => {
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={activeVendor} />);
    await openMenu(user);
    await user.click(await screen.findByRole("menuitem", { name: /^Deactivate$/i }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent(/Deactivate vendor/i);
    expect(dialog).toHaveTextContent(/remains in the system/i);

    await user.click(within(dialog).getByRole("button", { name: /^Deactivate$/i }));
    await waitFor(() => expect(mockPutFn).toHaveBeenCalledTimes(1));
    expect(mockPutFn.mock.calls[0]![0]).toBe("/api/vendors/v-active/deactivate");
    await waitFor(() =>
      expect(mockInvalidateFn).toHaveBeenCalledWith({ queryKey: vendorKeys.all })
    );
  });

  it("reactivate confirmation calls the reactivate endpoint", async () => {
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={inactiveVendor} />);
    await openMenu(user);
    await user.click(await screen.findByRole("menuitem", { name: /^Reactivate$/i }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent(/Reactivate vendor/i);

    await user.click(within(dialog).getByRole("button", { name: /^Reactivate$/i }));
    await waitFor(() => expect(mockPutFn).toHaveBeenCalledTimes(1));
    expect(mockPutFn.mock.calls[0]![0]).toBe("/api/vendors/v-inactive/reactivate");
  });

  it("delete of an unused vendor calls DELETE and invalidates the list", async () => {
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={activeVendor} />);
    await openMenu(user);
    await user.click(await screen.findByRole("menuitem", { name: /^Delete$/i }));

    const dialog = await screen.findByRole("dialog");
    const deleteButton = within(dialog).getByRole("button", { name: /^Delete$/i });
    expect(deleteButton).toHaveClass("bg-destructive");

    await user.click(deleteButton);
    await waitFor(() => expect(mockDeleteFn).toHaveBeenCalledTimes(1));
    expect(mockDeleteFn.mock.calls[0]![0]).toBe("/api/vendors/v-active");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("blocked delete keeps the dialog open and shows the dependency message", async () => {
    mockDeleteFn.mockRejectedValueOnce(
      new Error("This vendor cannot be deleted because it is referenced by 4 outsourced resources. Deactivate it instead.")
    );
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={activeVendor} />);
    await openMenu(user);
    await user.click(await screen.findByRole("menuitem", { name: /^Delete$/i }));

    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: /^Delete$/i }));

    const alert = await within(dialog).findByRole("alert");
    expect(alert).toHaveTextContent(/referenced by 4 outsourced resources/i);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(mockPutFn).not.toHaveBeenCalled();
  });

  it("confirmation dialog renders centered in a portal (outside the row flow)", async () => {
    const user = userEvent.setup();
    const { container } = renderWithProviders(
      <table><tbody><tr data-testid="vendor-row"><td><VendorRowActions vendor={activeVendor} /></td></tr></tbody></table>
    );
    await openMenu(user);
    await user.click(await screen.findByRole("menuitem", { name: /^Deactivate$/i }));

    const dialog = await screen.findByRole("dialog");
    expect(document.body.contains(dialog)).toBe(true);
    expect(container.querySelector('[data-testid="vendor-row"]')).not.toContainElement(dialog);
  });

  it("AR lifecycle labels resolve", async () => {
    const intl = (await import("next-intl")) as unknown as { __setLocale: (l: string) => void };
    intl.__setLocale("ar");
    const user = userEvent.setup();
    renderWithProviders(<VendorRowActions vendor={activeVendor} />);
    await user.click(await screen.findByRole("button", { name: /^الإجراءات$/i }));

    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: /^تعديل$/i })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^إلغاء التنشيط$/i })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^حذف$/i })).toBeInTheDocument();
  });
});
