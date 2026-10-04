import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AddTeamButton } from "@/components/resources/TeamFormSheets";
import { TeamRowActions } from "@/components/resources/TeamRowActions";
import { teamKeys } from "@/lib/api/teams";

const { mockGet, mockPost, mockPut, mockDelete, mockInvalidate } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
  mockPut: vi.fn(),
  mockDelete: vi.fn(),
  mockInvalidate: vi.fn(),
}));

// Both paths must be mocked - the barrel re-export defeats mocking only one.
vi.mock("@/lib/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/client")>();
  return { ...actual, api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut, delete: mockDelete } };
});
vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return { ...actual, api: { ...actual.api, get: mockGet, post: mockPost, put: mockPut, delete: mockDelete } };
});

vi.mock("@tanstack/react-query", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-query")>();
  return { ...actual, useQueryClient: () => ({ invalidateQueries: mockInvalidate }) };
});

vi.mock("@/lib/auth", () => ({
  useAuth: () => ({
    status: "authenticated",
    user: { permissions: ["Team.Manage", "Team.View", "Team.Create", "Team.Edit", "Team.Delete"] },
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
      messages,
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
        raw,
      );
    },
    useLocale: () => locale,
    __setLocale: (next: string) => {
      locale = next;
    },
  };
});

function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const activeTeam = {
  id: "t-active",
  name: "Platform Engineering",
  code: "TEAM-000001",
  description: "Builds the platform",
  isActive: true,
  memberCount: 2,
  members: [],
};

const inactiveTeam = { ...activeTeam, id: "t-inactive", name: "Legacy Team", code: "TEAM-000002", isActive: false };

const departments = [
  { id: "dept-eng", name: "Engineering", isActive: true },
  { id: "dept-ops", name: "Operations", isActive: true },
];

beforeEach(() => {
  mockInvalidate.mockReset();
  mockGet.mockReset();
  mockPost.mockReset();
  mockPut.mockReset();
  mockDelete.mockReset();

  mockGet.mockImplementation((path: string) => {
    if (path === "/api/departments") {
      return Promise.resolve({ items: departments, page: 1, pageSize: 200, totalCount: 2, totalPages: 1 });
    }
    if (path === "/api/resources") {
      return Promise.resolve({
        items: [
          { id: "r-1", name: "BDC", type: "Staff" },
          { id: "r-2", name: "Developer", type: "Staff" },
        ],
        page: 1,
        pageSize: 200,
        totalCount: 2,
        totalPages: 1,
      });
    }
    if (path.includes("/members")) return Promise.resolve([]);
    if (path.startsWith("/api/teams/")) return Promise.resolve(activeTeam);
    return Promise.resolve(null);
  });

  mockPost.mockImplementation((_path: string, body: unknown) =>
    Promise.resolve({ ...activeTeam, ...(body as object) }),
  );
  mockPut.mockImplementation((_path: string, body: unknown) =>
    Promise.resolve({ ...activeTeam, ...(body as object) }),
  );
  mockDelete.mockImplementation(() => Promise.resolve(undefined));
});

afterEach(async () => {
  cleanup();
  const intl = (await import("next-intl")) as unknown as { __setLocale?: (l: string) => void };
  intl.__setLocale?.("en");
});

async function openAddTeam(user: ReturnType<typeof userEvent.setup>) {
  renderWithProviders(<AddTeamButton />);
  await user.click(await screen.findByRole("button", { name: /^Add Team$/i }));
  await screen.findByLabelText(/^Name$/i);
}

async function openMenu(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("button", { name: /^Actions$/i }));
}

describe("AddTeamButton", () => {
  it("1. Creates a team and posts to /api/teams", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Platform Engineering");
    await user.type(screen.getByLabelText(/^Description$/i), "Builds the platform");
    await user.selectOptions(screen.getByLabelText(/^Department$/i), "dept-eng");
    await user.selectOptions(screen.getByLabelText(/^Team lead$/i), "r-1");
    await user.type(screen.getByLabelText(/^Email alias$/i), "platform-eng");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockPost).toHaveBeenCalled());
    const [path, body] = mockPost.mock.calls[0]!;
    expect(path).toBe("/api/teams");
    expect(body).toEqual({
      name: "Platform Engineering",
      description: "Builds the platform",
      departmentId: "dept-eng",
      teamLeadId: "r-1",
      emailAlias: "platform-eng",
      memberResourceIds: [],
    });
  });

  it("2. Never sends a team code - it is system-assigned", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Platform Engineering");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockPost).toHaveBeenCalled());
    expect(mockPost.mock.calls[0]![1]).not.toHaveProperty("code");
    expect(mockPost.mock.calls[0]![1]).not.toHaveProperty("isActive");
  });

  it("3. The system-assigned team code is shown to the user after create", async () => {
    mockPost.mockResolvedValueOnce({ ...activeTeam, code: "TEAM-000042" });
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Platform Engineering");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    expect(await screen.findByText("TEAM-000042")).toBeInTheDocument();
  });

  it("4. Done closes the sheet after the code is shown", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Platform Engineering");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));
    await screen.findByText("TEAM-000001");

    await user.click(screen.getByRole("button", { name: /^Done$/i }));
    await waitFor(() => expect(screen.queryByText("TEAM-000001")).not.toBeInTheDocument());
  });

  it("5. The code field is read-only and disabled before create", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    const codeInput = screen.getByLabelText(/^Code$/i);
    expect(codeInput).toBeDisabled();
    expect(codeInput).toHaveAttribute("readonly");
  });

  it("6. A whitespace-only name is rejected client-side without calling the API", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    // Native `required` blocks a truly empty value, so the custom guard is what
    // catches a name made only of whitespace.
    expect(screen.getByLabelText(/^Name$/i)).toBeRequired();
    await user.type(screen.getByLabelText(/^Name$/i), "   ");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    expect(await screen.findByText("Team name is required")).toBeInTheDocument();
    expect(mockPost).not.toHaveBeenCalled();
  });

  it("7. Surfaces a server-side code conflict and keeps the sheet open", async () => {
    mockPost.mockRejectedValueOnce(new Error("Team code conflict"));
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Platform Engineering");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Team code conflict");
    expect(screen.getByLabelText(/^Name$/i)).toBeInTheDocument();
  });

  it("8. Invalidates the teams cache after create", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Platform Engineering");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockInvalidate).toHaveBeenCalledWith({ queryKey: teamKeys.all }));
  });

  it("9. Department is optional and can be left unselected", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    await user.type(screen.getByLabelText(/^Name$/i), "Standalone Team");
    await user.click(screen.getByRole("button", { name: /^Create$/i }));

    await waitFor(() => expect(mockPost).toHaveBeenCalled());
    const body = mockPost.mock.calls[0]![1] as Record<string, unknown>;
    expect(body.departmentId).toBeUndefined();
    expect(body.teamLeadId).toBeUndefined();
  });

  it("10. Loads department and team-lead options from the API", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    await waitFor(() => expect(screen.getByLabelText(/^Department$/i)).toBeInTheDocument());
    expect(mockGet).toHaveBeenCalledWith("/api/departments", { page: 1, pageSize: 200 });
    const departmentSelect = screen.getByLabelText(/^Department$/i) as HTMLSelectElement;
    expect(within(departmentSelect).getByText("Engineering")).toBeInTheDocument();
  });

  it("11. States that teams are independent of departments", async () => {
    const user = userEvent.setup();
    await openAddTeam(user);

    expect(
      await screen.findByText(/Teams are independent of departments/i),
    ).toBeInTheDocument();
  });

  it("12. EN translation keys resolve (not raw keys)", async () => {
    const intl = (await import("next-intl")) as unknown as { __setLocale: (l: string) => void };
    intl.__setLocale("en");
    const user = userEvent.setup();
    await openAddTeam(user);

    expect(screen.getByText("Basic information")).toBeInTheDocument();
    expect(screen.getByText("Organisational links")).toBeInTheDocument();
    expect(screen.queryByText("resources.teams.basicInformation")).not.toBeInTheDocument();
  });

  it("13. AR translation keys resolve (not raw keys)", async () => {
    const intl = (await import("next-intl")) as unknown as { __setLocale: (l: string) => void };
    intl.__setLocale("ar");
    const user = userEvent.setup();
    renderWithProviders(<AddTeamButton />);

    await user.click(await screen.findByRole("button", { name: /^إضافة فريق$/i }));
    await screen.findByLabelText(/^الاسم$/i);

    expect(screen.getByText("المعلومات الأساسية")).toBeInTheDocument();
    expect(screen.queryByText("resources.teams.basicInformation")).not.toBeInTheDocument();
  });
});

describe("TeamRowActions", () => {
  it("14. Deactivate hits the deactivate endpoint and invalidates the cache", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TeamRowActions team={activeTeam} />);
    await openMenu(user);

    await user.click(await screen.findByRole("menuitem", { name: /^Deactivate$/i }));
    await user.click(await screen.findByRole("button", { name: /^Deactivate$/i }));

    await waitFor(() => expect(mockPut).toHaveBeenCalled());
    expect(mockPut.mock.calls[0]![0]).toBe("/api/teams/t-active/deactivate");
    await waitFor(() => expect(mockInvalidate).toHaveBeenCalledWith({ queryKey: teamKeys.all }));
  });

  it("15. An inactive team offers Reactivate instead of Deactivate", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TeamRowActions team={inactiveTeam} />);
    await openMenu(user);

    const menu = await screen.findByRole("menu");
    expect(within(menu).queryByRole("menuitem", { name: /^Deactivate$/i })).not.toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: /^Reactivate$/i })).toBeInTheDocument();
  });

  it("16. Reactivate hits the reactivate endpoint", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TeamRowActions team={inactiveTeam} />);
    await openMenu(user);

    await user.click(await screen.findByRole("menuitem", { name: /^Reactivate$/i }));
    await user.click(await screen.findByRole("button", { name: /^Reactivate$/i }));

    await waitFor(() => expect(mockPut).toHaveBeenCalled());
    expect(mockPut.mock.calls[0]![0]).toBe("/api/teams/t-inactive/reactivate");
  });

  it("17. Delete calls DELETE on the team and warns about current members", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TeamRowActions team={activeTeam} />);
    await openMenu(user);

    await user.click(await screen.findByRole("menuitem", { name: /^Delete$/i }));

    expect(
      await screen.findByText(/Teams with current members cannot be deleted/i),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /^Delete$/i }));

    await waitFor(() => expect(mockDelete).toHaveBeenCalled());
    expect(mockDelete.mock.calls[0]![0]).toBe("/api/teams/t-active");
  });

  it("18. A 409 conflict from delete keeps the dialog open and shows the error", async () => {
    mockDelete.mockRejectedValueOnce(
      new Error("This team cannot be deleted because it still has 1 current member."),
    );
    const user = userEvent.setup();
    renderWithProviders(<TeamRowActions team={activeTeam} />);
    await openMenu(user);

    await user.click(await screen.findByRole("menuitem", { name: /^Delete$/i }));
    await user.click(screen.getByRole("button", { name: /^Delete$/i }));

    expect(
      await screen.findByText(/still has 1 current member/i),
    ).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("19. Exposes Manage members for the effective-dated membership editor", async () => {
    const user = userEvent.setup();
    renderWithProviders(<TeamRowActions team={activeTeam} />);
    await openMenu(user);

    expect(await screen.findByRole("menuitem", { name: /Manage members/i })).toBeInTheDocument();
  });

  it("20. Renders inside a table row and portals its dialogs to the body", async () => {
    const user = userEvent.setup();
    const { container } = render(
      <table>
        <tbody>
          <tr data-testid="team-row">
            <td>
              <QueryClientProvider
                client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
              >
                <TeamRowActions team={activeTeam} />
              </QueryClientProvider>
            </td>
          </tr>
        </tbody>
      </table>,
    );

    await user.click(screen.getByRole("button", { name: /^Actions$/i }));
    await user.click(await screen.findByRole("menuitem", { name: /^Delete$/i }));

    const dialog = await screen.findByRole("dialog");
    expect(document.body.contains(dialog)).toBe(true);
    expect(container.querySelector('[data-testid="team-row"]')).not.toContainElement(dialog);
  });
});
