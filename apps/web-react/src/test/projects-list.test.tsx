import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, useLocation } from "react-router-dom";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
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
    { id: "p1", publicId: "public-p1", name: "Healthy Project", code: "HP-1", status: "Active", health: "Green", ragStatus: "Green", progress: 40, planProgress: 65.4, currency: "USD", projectManagerName: "A Lead" },
    { id: "p2", publicId: "public-p2", name: "Risky Project", code: "RP-2", status: "Active", health: "Amber", ragStatus: "Amber", progress: 20, planProgress: null, currency: "USD", projectManagerName: "B Lead" },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

function renderPage(fetchMock: ReturnType<typeof vi.fn>) {
  setTokens("a");
  vi.stubGlobal("fetch", vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
    const mockFetch = fetchMock as unknown as typeof fetch;
    const response = await mockFetch(input, init);
    if (String(input).includes("/api/project-lifecycle/options")) {
      try {
        const body = await response.clone().json();
        if (body == null) {
          return new Response(JSON.stringify({ phases: [
            { id: "phase-initiation", code: "Initiation", nameEn: "Initiation", nameAr: "البدء", displayOrder: 1, isDefault: true, isActive: true },
          ], statuses: [
            { id: "status-active", code: "Active", nameEn: "Active", nameAr: "نشط", displayOrder: 1, isDefault: true, isActive: true, reportingCategory: "InProgress", attentionLevel: "None" },
          ] }), { status: 200 });
        }
      } catch {
        return response;
      }
    }
    return response;
  }));
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

function renderProjectCreatePageWithManagerLookup(managerLookupStatus = 200, managers: Array<{ id: string; fullName: string }> = []) {
  const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
    const url = String(input);
    if (url.includes("/api/auth/me")) {
      return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View", "Project.Create"] }), { status: 200 }));
    }
    if (url.includes("/api/me/perspectives")) {
      return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
    }
    if (url.includes("/api/users/workflow-lookups/projects/managers")) {
      const status = managerLookupStatus;
      const body = status === 403 ? { title: "Forbidden", status: 403 } : managers;
      return Promise.resolve(new Response(JSON.stringify(body), { status }));
    }
    if (url.includes("/api/users/workflow-lookups/projects")) {
      const status = 200;
      const body: unknown[] = [];
      return Promise.resolve(new Response(JSON.stringify(body), { status }));
    }
    if (url.includes("/api/currencies")) {
      return Promise.resolve(new Response(JSON.stringify([{ id: "usd", code: "USD", name: "US Dollar", symbol: "$", exchangeRate: 1, isBase: true }]), { status: 200 }));
    }
    if (url.includes("/api/organization/units")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
    if (url.includes("/api/portfolios")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
    if (url.includes("/api/programs")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
    if (url.includes("/api/projects") && init?.method === "POST") {
      return Promise.resolve(new Response(JSON.stringify({ id: "test-project", publicId: "test-project-public" }), { status: 201 }));
    }
    if (url.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
    return Promise.resolve(new Response("null", { status: 200 }));
  });
  renderPage(fetchMock);
  return fetchMock;
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

  it("filters by localized database-backed status options without refetching", async () => {
    const dynamicPage = {
      ...page,
      items: [
        { ...page.items[0], name: "Queued Project", status: "Queued", statusNameEn: "Preparation Queue" },
        { ...page.items[1], name: "Released Project", status: "Released", statusNameEn: "Released" },
      ],
    };
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View", "Project.Create"] }), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/project-lifecycle/options")) return Promise.resolve(new Response(JSON.stringify({ phases: [], statuses: [
        { id: "queued", code: "Queued", nameEn: "Preparation Queue", nameAr: "قائمة التجهيز", displayOrder: 1, isDefault: true, isActive: true },
        { id: "released", code: "Released", nameEn: "Released", nameAr: "تم الإطلاق", displayOrder: 2, isDefault: false, isActive: true },
      ] }), { status: 200 }));
      if (input.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(dynamicPage), { status: 200 }));
      if (input.includes("/api/portfolios")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);
    await waitFor(() => expect(screen.getByText("Queued Project")).toBeInTheDocument());
    const callsBefore = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects") && !String(input).includes("/shell")).length;
    fireEvent.click(await screen.findByRole("button", { name: /Advanced Search/ }, { timeout: 5000 }));
    fireEvent.click(await screen.findByRole("checkbox", { name: "Preparation Queue" }));
    await waitFor(() => expect(screen.queryByText("Released Project")).not.toBeInTheDocument());
    expect(screen.getByText("Queued Project")).toBeInTheDocument();
    const callsAfter = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects") && !String(input).includes("/shell")).length;
    expect(callsAfter).toBe(callsBefore);
  });

  it("matches source status badges and defers the advanced-search lookup until expanded", async () => {
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View"] }), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      if (input.includes("/api/users/workflow-lookups/projects/managers")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      if (input.includes("/api/users/workflow-lookups/projects")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      if (input.includes("/api/portfolios")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);

    await waitFor(() => expect(screen.getByRole("link", { name: /Healthy Project/ })).toBeInTheDocument());
    expect(screen.queryByRole("button", { name: /^(Create|إنشاء)$/ })).not.toBeInTheDocument();
    const projectRow = screen.getByRole("link", { name: /Healthy Project/ });
    expect(within(projectRow).getByText("Active")).toHaveClass("bg-transparent", "text-success");
    expect(within(projectRow).getByText("Active")).not.toHaveClass("bg-success/10");
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/users/workflow-lookups/projects"))).toBe(false);

    const advancedSearchTrigger = await screen.findByRole("button", { name: /Advanced Search/ });
    expect(advancedSearchTrigger).toHaveAttribute("tabindex", "0");
    fireEvent.keyDown(advancedSearchTrigger, { key: "Enter" });
    expect(await screen.findAllByPlaceholderText("Min")).toHaveLength(2);
    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/users/workflow-lookups/projects/managers"))).toBe(true));
  });

  it("uses active Working plan progress in both registry views and does not fall back to stale project progress", async () => {
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View"] }), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);

    const tableProject = await screen.findByRole("link", { name: /Healthy Project/ });
    expect(within(tableProject).getByText("65%")).toBeInTheDocument();
    expect(within(tableProject).queryByText("40%")).not.toBeInTheDocument();
    const noPlanProject = screen.getByRole("link", { name: /Risky Project/ });
    expect(within(noPlanProject).queryByRole("progressbar")).not.toBeInTheDocument();
    expect(within(noPlanProject).queryByText("20%")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: /Card View/i }));
    const projectCard = await screen.findByRole("link", { name: /Healthy Project/ });
    expect(within(projectCard).getByText("65%")).toBeInTheDocument();
    expect(within(projectCard).queryByText("40%")).not.toBeInTheDocument();
  });

  it("creates a Project with an eligible System User who has no Resource, then opens the created Project", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View", "Project.Create"] }), { status: 200 }));
      }
      if (url.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (url.includes("/api/users/workflow-lookups/projects/managers")) {
        return Promise.resolve(new Response(JSON.stringify([{ id: "manager-1", fullName: "Project Manager" }]), { status: 200 }));
      }
      if (url.includes("/api/users/workflow-lookups/projects")) {
        return Promise.resolve(new Response(JSON.stringify([{ id: "manager-1", fullName: "Project Manager" }]), { status: 200 }));
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
        return Promise.resolve(new Response(JSON.stringify({ id: "created-project", publicId: "created-project-public" }), { status: 201 }));
      }
      if (url.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);

    fireEvent.click(await screen.findByRole("button", { name: /^(Create|إنشاء)$/ }));
    const sheet = await screen.findByRole("dialog", { name: "New Project" });
    fireEvent.change(within(sheet).getByPlaceholderText("Name"), { target: { value: "Created Project" } });
    await waitFor(() => expect(within(sheet).getByText("USD — US Dollar ($)")).toBeInTheDocument());
    expect(within(sheet).getByPlaceholderText("Name")).toHaveValue("Created Project");

    fireEvent.click(within(sheet).getByRole("combobox", { name: "Project Manager" }));
    fireEvent.click(await screen.findByRole("option", { name: "Project Manager" }));
    fireEvent.click(within(sheet).getByRole("combobox", { name: "Portfolio" }));
    fireEvent.click(await screen.findByRole("option", { name: "Portfolio One" }));
    expect(within(sheet).getByText("GBP — British Pound (£)")).toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole("combobox", { name: "Program" }));
    fireEvent.click(await screen.findByRole("option", { name: "Program One" }));
    expect(within(sheet).getByText("JPY — Japanese Yen (¥)")).toBeInTheDocument();
    const submitButton = within(sheet).getByRole("button", { name: "New Project" });
    expect(submitButton).toHaveAttribute("type", "submit");
    await user.click(submitButton);

    await waitFor(() => expect(fetchMock.mock.calls.some(([input, init]) => String(input).includes("/api/projects") && init?.method === "POST")).toBe(true));
    const createCall = fetchMock.mock.calls.find(([input, init]) => String(input).includes("/api/projects") && init?.method === "POST");
    const payload = JSON.parse(String(createCall?.[1]?.body));
    expect(payload).toMatchObject({ name: "Created Project", projectManagerId: "manager-1", portfolioId: "portfolio-1", programId: "program-1", currency: "JPY" });
    expect(payload).not.toHaveProperty("resourceId");
    await waitFor(() => expect(screen.getByTestId("current-path")).toHaveTextContent("/projects/created-project-public"));
  });

  it("shows the Resource-independent empty state in English and Arabic", async () => {
    renderProjectCreatePageWithManagerLookup(200, []);
    fireEvent.click(await screen.findByRole("button", { name: /^(Create|إنشاء)$/ }));
    expect(await screen.findByText("Project Managers are selected from eligible active System Users. A Resource profile or Project Team membership is not required.")).toBeInTheDocument();
    fireEvent.click(within(screen.getByRole("dialog", { name: "New Project" })).getByRole("combobox", { name: "Project Manager" }));
    expect(await screen.findByText("No eligible active Project Managers found.")).toBeInTheDocument();

    cleanup();
    window.localStorage.setItem("nexus-locale", "ar");
    renderProjectCreatePageWithManagerLookup(200, []);
    fireEvent.click(await screen.findByRole("button", { name: /^(Create|إنشاء)$/ }));
    expect(await screen.findByText("يُختار مديرو المشروعات من مستخدمي النظام النشطين المستوفين لشروط الأهلية. لا يلزم وجود سجل مورد أو عضوية في فريق المشروع.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("combobox", { name: "مدير المشروع" }));
    expect(await screen.findByText("لم يتم العثور على مديري مشروعات نشطين مؤهلين.")).toBeInTheDocument();
  });

  it("shows a localized authorization message when Project Manager candidates are forbidden", async () => {
    renderProjectCreatePageWithManagerLookup(403);
    fireEvent.click(await screen.findByRole("button", { name: /^(Create|إنشاء)$/ }));
    expect(await screen.findByText("You do not have permission to load Project Manager candidates. Ask an administrator to review your user lookup access.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Retry" })).not.toBeInTheDocument();

    cleanup();
    window.localStorage.setItem("nexus-locale", "ar");
    renderProjectCreatePageWithManagerLookup(403);
    fireEvent.click(await screen.findByRole("button", { name: /^(Create|إنشاء)$/ }));
    expect(await screen.findByText("ليست لديك صلاحية تحميل قائمة مرشحي مديري المشروعات. اطلب من المسؤول مراجعة صلاحية البحث عن المستخدمين.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "إعادة المحاولة" })).not.toBeInTheDocument();
  });

  it("allows Project creation without selecting an initial manager", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      const url = String(input);
      if (url.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View", "Project.Create"] }), { status: 200 }));
      }
      if (url.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (url.includes("/api/users/workflow-lookups/projects/managers")) {
        return Promise.resolve(new Response(JSON.stringify([{ id: "manager-1", fullName: "Project Manager" }]), { status: 200 }));
      }
      if (url.includes("/api/users/workflow-lookups/projects")) {
        return Promise.resolve(new Response(JSON.stringify([{ id: "manager-1", fullName: "Project Manager" }]), { status: 200 }));
      }
      if (url.includes("/api/currencies")) {
        return Promise.resolve(new Response(JSON.stringify([
          { id: "eur", code: "EUR", name: "Euro", symbol: "€", exchangeRate: 0.9, isBase: false },
          { id: "usd", code: "USD", name: "US Dollar", symbol: "$", exchangeRate: 1, isBase: true },
        ]), { status: 200 }));
      }
      if (url.includes("/api/organization/units")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      if (url.includes("/api/portfolios")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
      if (url.includes("/api/programs")) return Promise.resolve(new Response(JSON.stringify({ items: [] }), { status: 200 }));
      if (url.includes("/api/projects") && init?.method === "POST") {
        return Promise.resolve(new Response(JSON.stringify({ id: "unassigned-project", publicId: "unassigned-project-public" }), { status: 201 }));
      }
      if (url.includes("/api/projects")) return Promise.resolve(new Response(JSON.stringify(page), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    renderPage(fetchMock);

    fireEvent.click(await screen.findByRole("button", { name: /^(Create|إنشاء)$/ }));
    const sheet = await screen.findByRole("dialog", { name: "New Project" });
    fireEvent.change(within(sheet).getByPlaceholderText("Name"), { target: { value: "Unassigned Project" } });
    await waitFor(() => expect(within(sheet).getByText(/USD.*US Dollar.*\$/)).toBeInTheDocument());
    await user.click(within(sheet).getByRole("button", { name: "New Project" }));

    await waitFor(() => expect(fetchMock.mock.calls.some(([input, init]) => String(input).includes("/api/projects") && init?.method === "POST")).toBe(true));
    const createCall = fetchMock.mock.calls.find(([input, init]) => String(input).includes("/api/projects") && init?.method === "POST");
    const payload = JSON.parse(String(createCall?.[1]?.body));
    expect(payload).toMatchObject({ name: "Unassigned Project", currency: "USD" });
    expect(payload).not.toHaveProperty("projectManagerId");
  });
});
