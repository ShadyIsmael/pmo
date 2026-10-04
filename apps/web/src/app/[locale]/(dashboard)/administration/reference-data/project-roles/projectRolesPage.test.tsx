import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import ProjectRolesPage from "@/app/[locale]/(dashboard)/administration/reference-data/project-roles/page";

const { mockGet, mockPost, mockPut } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
}));

vi.mock("@/lib/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/client")>();
  return { ...actual, api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut } };
});

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return { ...actual, api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut } };
});

vi.mock("@/lib/auth", () => ({
  useAuth: () => ({
    status: "authenticated",
    user: { permissions: ["Resource.Manage", "Resource.View"] },
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  }),
}));

vi.mock("next-intl", async () => {
  const { default: en } = await import("@/messages/en.json");
  const pick = (messages: unknown, fullKey: string): string => {
    const value = fullKey.split(".").reduce<unknown>(
      (acc, part) => (acc && typeof acc === "object" ? (acc as Record<string, unknown>)[part] : undefined),
      messages
    );
    return typeof value === "string" ? value : fullKey;
  };
  return {
    useTranslations: (namespace?: string) => (key: string) =>
      pick(en, namespace ? `${namespace}.${key}` : key),
    useLocale: () => "en",
  };
});

const mockGetFn = mockGet as unknown as Mock;
const mockPostFn = mockPost as unknown as Mock;
const mockPutFn = mockPut as unknown as Mock;

let roles: Array<Record<string, unknown>>;
let nextCodeCalls: number;

function makeRole(code: string, order: number) {
  return {
    id: `role-${code}`,
    code,
    nameEn: `Role ${code}`,
    nameAr: `دور ${code}`,
    description: null,
    displayOrder: order,
    isActive: true,
  };
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <ProjectRolesPage />
    </QueryClientProvider>
  );
}

async function openCreate(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: /Add Role/i }));
  return screen.findByLabelText(/^Code$/i);
}

beforeEach(() => {
  roles = [];
  nextCodeCalls = 0;
  mockGetFn.mockReset();
  mockPostFn.mockReset();
  mockPutFn.mockReset();
  mockGetFn.mockImplementation((path: string) => {
    if (path === "/api/project-roles/next-code") {
      nextCodeCalls += 1;
      // Mirrors the real endpoint: the next code follows the roles that actually exist,
      // not the number of times it was asked for.
      return Promise.resolve({ code: `PROJROLE-${String(roles.length + 1).padStart(6, "0")}` });
    }
    if (path.startsWith("/api/project-roles")) return Promise.resolve(roles);
    return Promise.resolve([]);
  });
  mockPostFn.mockImplementation((_path: string, body: Record<string, unknown>) => {
    const code = (body.code as string) ?? `PROJROLE-${String(roles.length + 1).padStart(6, "0")}`;
    roles = [...roles, makeRole(code, Number(body.displayOrder ?? 10))];
    return Promise.resolve(makeRole(code, Number(body.displayOrder ?? 10)));
  });
  mockPutFn.mockResolvedValue({});
});

afterEach(() => cleanup());

describe("Project Roles create form code generation", () => {
  it("prefills the next generic code as soon as the create form opens", async () => {
    const user = userEvent.setup();
    renderPage();

    const codeInput = await openCreate(user);

    await waitFor(() => expect(codeInput).toHaveValue("PROJROLE-000001"));
    expect(nextCodeCalls).toBe(1);
  });

  it("requests a new code on the second open instead of reusing the cached one", async () => {
    const user = userEvent.setup();
    renderPage();

    // First create: prefilled, then saved.
    const first = await openCreate(user);
    await waitFor(() => expect(first).toHaveValue("PROJROLE-000001"));
    await user.type(screen.getByLabelText(/Name \(EN\)/i), "Delivery Manager");
    await user.click(screen.getByRole("button", { name: /^Save$/i }));
    await waitFor(() => expect(roles).toHaveLength(1));
    expect(roles[0].code).toBe("PROJROLE-000001");

    // Second open must ask again and offer the following code, not the first one.
    const second = await openCreate(user);
    await waitFor(() => expect(second).toHaveValue("PROJROLE-000002"));
    expect(second).not.toHaveValue("PROJROLE-000001");
    // At least one request per open; the successful save also invalidates the cached code,
    // so the exact number of requests is an implementation detail.
    expect(nextCodeCalls).toBeGreaterThanOrEqual(2);
  });

  it("offers a distinct, ascending code on every consecutive create", async () => {
    const user = userEvent.setup();
    renderPage();

    const seen: string[] = [];
    for (let i = 0; i < 3; i += 1) {
      const input = await openCreate(user);
      await waitFor(() => expect(input).not.toHaveValue(""));
      seen.push((input as HTMLInputElement).value);
      await user.clear(screen.getByLabelText(/Name \(EN\)/i));
      await user.type(screen.getByLabelText(/Name \(EN\)/i), `Role ${i + 1}`);
      await user.click(screen.getByRole("button", { name: /^Save$/i }));
      await waitFor(() => expect(roles).toHaveLength(i + 1));
    }

    // Regression: the second open used to be served the code cached from the first.
    expect(seen).toEqual(["PROJROLE-000001", "PROJROLE-000002", "PROJROLE-000003"]);
    expect(new Set(seen).size).toBe(3);
    expect(roles.map((role) => role.code)).toEqual(seen);
  });

  it("cancelling without saving leaves the next code unchanged", async () => {
    const user = userEvent.setup();
    renderPage();

    const first = await openCreate(user);
    await waitFor(() => expect(first).toHaveValue("PROJROLE-000001"));
    await user.click(screen.getByRole("button", { name: /^Cancel$/i }));
    await waitFor(() => expect(screen.queryByLabelText(/^Code$/i)).not.toBeInTheDocument());

    const second = await openCreate(user);
    await waitFor(() => expect(second).toHaveValue("PROJROLE-000001"));
    expect(nextCodeCalls).toBeGreaterThanOrEqual(2);
  });

  it("omits an untouched code from the payload so the server assigns it", async () => {
    const user = userEvent.setup();
    renderPage();

    const codeInput = await openCreate(user);
    await waitFor(() => expect(codeInput).toHaveValue("PROJROLE-000001"));
    await user.type(screen.getByLabelText(/Name \(EN\)/i), "Business Analyst");
    await user.click(screen.getByRole("button", { name: /^Save$/i }));

    await waitFor(() => expect(mockPostFn).toHaveBeenCalledTimes(1));
    const payload = mockPostFn.mock.calls[0][1] as Record<string, unknown>;
    // A stale preview must never be submitted as a hard code.
    expect(payload).not.toHaveProperty("code");
    expect(payload.nameEn).toBe("Business Analyst");
  });

  it("submits a hand-typed code so meaningful reference codes still work", async () => {
    const user = userEvent.setup();
    renderPage();

    const codeInput = await openCreate(user);
    await waitFor(() => expect(codeInput).toHaveValue("PROJROLE-000001"));
    await user.clear(codeInput);
    await user.type(codeInput, "QALead");
    await user.type(screen.getByLabelText(/Name \(EN\)/i), "QA Lead");
    await user.click(screen.getByRole("button", { name: /^Save$/i }));

    await waitFor(() => expect(mockPostFn).toHaveBeenCalledTimes(1));
    const payload = mockPostFn.mock.calls[0][1] as Record<string, unknown>;
    expect(payload.code).toBe("QALead");
    expect(roles[0].code).toBe("QALead");
  });

  it("stops overwriting the field once the user types their own code", async () => {
    const user = userEvent.setup();
    renderPage();

    const codeInput = await openCreate(user);
    await waitFor(() => expect(codeInput).toHaveValue("PROJROLE-000001"));
    await user.clear(codeInput);
    await user.type(codeInput, "PM");
    // A late-arriving refetch must not clobber the manual entry.
    await waitFor(() => expect(codeInput).toHaveValue("PM"));
  });
});
