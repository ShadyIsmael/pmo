import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, useLocation } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { AuthProvider } from "../auth/AuthProvider";
import { ManagementProvider } from "../api/management";
import { Badge } from "../components/ui/Badge";
import { ProjectsListPage } from "../features/projects/ProjectsListPage";
import { setTokens } from "../api/client";

function LocationProbe() {
  const { pathname } = useLocation();
  return <output data-testid="current-path">{pathname}</output>;
}

const page = {
  items: [
    { id: "p1", name: "Healthy Project", code: "HP-1", status: "Active", health: "Green", ragStatus: "Green", progress: 40, currency: "USD", projectManagerName: "A Lead" },
    { id: "p2", name: "Risky Project", code: "RP-2", status: "Active", health: "Amber", ragStatus: "Amber", progress: 20, currency: "USD", projectManagerName: "B Lead" },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

function renderPage(fetchMock: ReturnType<typeof vi.fn>) {
  setTokens("a", "r");
  vi.stubGlobal("fetch", fetchMock);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <AuthProvider>
          <ManagementProvider user={null}>
            <MemoryRouter initialEntries={["/projects"]}>
              <ProjectsListPage />
              <LocationProbe />
            </MemoryRouter>
          </ManagementProvider>
        </AuthProvider>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe("projects list polish", () => {
  it("keeps semantic status badges on colored text without tinted backgrounds", () => {
    render(<><Badge status="Active">Active</Badge><Badge variant="warning">Pending</Badge><Badge variant="destructive">Rejected</Badge></>);
    expect(screen.getByText("Active")).toHaveClass("bg-transparent", "text-success");
    expect(screen.getByText("Pending")).toHaveClass("bg-transparent", "text-warning");
    expect(screen.getByText("Rejected")).toHaveClass("bg-transparent", "text-destructive");
    expect(screen.getByText("Active")).not.toHaveClass("bg-success/10");
    expect(screen.getByText("Pending")).not.toHaveClass("bg-warning/10");
    expect(screen.getByText("Rejected")).not.toHaveClass("bg-destructive/10");
  });

  it("filters by status tab client-side without refetching", async () => {
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["projects.read", "Project.Create"] }), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      if (input.includes("/api/portfolios")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);
    await waitFor(() => expect(screen.getByText("Healthy Project")).toBeInTheDocument());
    const callsBefore = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects") && !String(input).includes("/shell")).length;
    fireEvent.click(screen.getByRole("tab", { name: /At Risk/i }));
    await waitFor(() => expect(screen.getByText("Risky Project")).toBeInTheDocument());
    expect(screen.queryByText("Healthy Project")).not.toBeInTheDocument();
    const callsAfter = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects") && !String(input).includes("/shell")).length;
    expect(callsAfter).toBe(callsBefore);
  });

  it("matches source status badges and defers the advanced-search lookup until expanded", async () => {
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["projects.read"] }), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      if (input.includes("/api/users/lookup")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      if (input.includes("/api/portfolios")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);

    await waitFor(() => expect(screen.getByRole("link", { name: /Healthy Project/ })).toBeInTheDocument());
    expect(screen.queryByRole("button", { name: /^Create$/ })).not.toBeInTheDocument();
    const projectRow = screen.getByRole("link", { name: /Healthy Project/ });
    expect(within(projectRow).getByText("Active")).toHaveClass("bg-transparent", "text-success");
    expect(within(projectRow).getByText("Active")).not.toHaveClass("bg-success/10");
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/users/lookup"))).toBe(false);

    const advancedSearchTrigger = await screen.findByRole("button", { name: /Advanced Search/ });
    expect(advancedSearchTrigger).toHaveAttribute("tabindex", "0");
    fireEvent.keyDown(advancedSearchTrigger, { key: "Enter" });
    expect(await screen.findAllByPlaceholderText("Min")).toHaveLength(3);
    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/users/lookup") && String(input).includes("project.manager"))).toBe(true));
  });

  it("creates a project with API currency inheritance and selected manager, then opens the created project", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["projects.read", "Project.Create"] }), { status: 200 }));
      }
      if (url.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (url.includes("/api/users/lookup")) {
        return Promise.resolve(new Response(JSON.stringify([{ id: "manager-1", fullName: "Project Manager", email: "manager@example.com" }]), { status: 200 }));
      }
      if (url.includes("/api/currencies")) {
        return Promise.resolve(new Response(JSON.stringify([
          { id: "eur", code: "EUR", name: "Euro", symbol: "€", exchangeRate: 0.9, isBase: false },
          { id: "usd", code: "USD", name: "US Dollar", symbol: "$", exchangeRate: 1, isBase: true },
          { id: "gbp", code: "GBP", name: "British Pound", symbol: "£", exchangeRate: 0.8, isBase: false },
          { id: "jpy", code: "JPY", name: "Japanese Yen", symbol: "¥", exchangeRate: 150, isBase: false },
        ]), { status: 200 }));
      }
      if (url.includes("/api/organization/units")) {
        return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      }
      if (url.includes("/api/portfolios")) {
        return Promise.resolve(new Response(JSON.stringify({ items: [{ id: "portfolio-1", name: "Portfolio One", currency: "GBP" }] }), { status: 200 }));
      }
      if (url.includes("/api/programs")) {
        return Promise.resolve(new Response(JSON.stringify({ items: [{ id: "program-1", name: "Program One", portfolioId: "portfolio-1", currency: "JPY" }] }), { status: 200 }));
      }
      if (url.includes("/api/projects") && init?.method === "POST") {
        return Promise.resolve(new Response(JSON.stringify({ id: "created-project" }), { status: 201 }));
      }
      if (url.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);

    fireEvent.click(await screen.findByRole("button", { name: /^Create$/ }));
    const sheet = await screen.findByRole("dialog", { name: "New Project" });
    fireEvent.change(within(sheet).getByPlaceholderText("Name"), { target: { value: "Created Project" } });
    await waitFor(() => expect(within(sheet).getByText("USD — US Dollar ($)")).toBeInTheDocument());
    expect(within(sheet).getByPlaceholderText("Name")).toHaveValue("Created Project");

    fireEvent.click(within(sheet).getAllByRole("combobox")[0]);
    fireEvent.click(await screen.findByRole("option", { name: /Project Manager · manager@example\.com/ }));
    expect(within(sheet).getByText("Project Manager · manager@example.com")).toBeInTheDocument();
    fireEvent.click(within(sheet).getAllByRole("combobox")[2]);
    fireEvent.click(await screen.findByRole("option", { name: "Portfolio One" }));
    expect(within(sheet).getByText("GBP — British Pound (£)")).toBeInTheDocument();
    fireEvent.click(within(sheet).getAllByRole("combobox")[3]);
    fireEvent.click(await screen.findByRole("option", { name: "Program One" }));
    expect(within(sheet).getByText("JPY — Japanese Yen (¥)")).toBeInTheDocument();
    const submitButton = within(sheet).getByRole("button", { name: "New Project" });
    expect(submitButton).toHaveAttribute("type", "submit");
    await user.click(submitButton);

    await waitFor(() => expect(fetchMock.mock.calls.some(([input, init]) => String(input).includes("/api/projects") && init?.method === "POST")).toBe(true));
    const createCall = fetchMock.mock.calls.find(([input, init]) => String(input).includes("/api/projects") && init?.method === "POST");
    const payload = JSON.parse(String(createCall?.[1]?.body));
    expect(payload).toMatchObject({ name: "Created Project", projectManagerId: "manager-1", portfolioId: "portfolio-1", programId: "program-1", currency: "JPY" });
    await waitFor(() => expect(screen.getByTestId("current-path")).toHaveTextContent("/projects/created-project"));
  });
});
