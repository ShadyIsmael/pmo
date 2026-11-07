import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@api/client";
import { I18nProvider } from "@i18n/messages";
import { MyWorkTimesheetsPage } from "@features/my-work/MyWorkTimesheetsPage";
import { MemoryRouter } from "react-router-dom";

const mocks = vi.hoisted(() => ({
  user: { id: "user-1", permissions: ["TimeTracking.ViewOwn", "TimeTracking.ManageOwn"] as string[] } as { id: string; permissions: string[] } | null,
  canMutate: true,
  effectiveReadOnly: false,
  get: vi.fn(),
  post: vi.fn(),
  entries: [] as Array<Record<string, unknown>>,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user, status: "authenticated" }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: mocks.canMutate, effectiveReadOnly: mocks.effectiveReadOnly }) }));
vi.mock("@api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/client")>();
  return { ...actual, api: { get: mocks.get, post: mocks.post, put: vi.fn(), delete: vi.fn() } };
});
vi.mock("@features/projects/api/projects", () => ({
  useProjects: () => ({
    data: { items: [{ id: "project-1", name: "Core Modernization", code: "PRJ-1" }] },
    isLoading: false,
    isError: false,
  }),
  projectKeys: { handoverManagers: (id: string) => ["projects", "handoverManagers", id] },
}));

const project = { id: "project-1", name: "Core Modernization", code: "PRJ-1" };

function buildEntry(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: "entry-1",
    projectId: project.id,
    projectName: project.name,
    projectCode: project.code,
    workDate: "2026-11-06",
    startTime: "09:00:00",
    endTime: "10:00:00",
    actualHours: 1,
    description: "Reviewed project plan",
    rowVersion: [1],
    ...overrides,
  };
}

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderPage(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  const client = createClient();
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/my-work/timesheets"]}>
        <I18nProvider><MyWorkTimesheetsPage /></I18nProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { client, ...view };
}

async function openCreateSheet(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("button", { name: "Add entry" }));
  return screen.findByRole("dialog", { name: "New manual time entry" });
}

describe("My Time entry SideSheet", () => {
  beforeEach(() => {
    mocks.user = { id: "user-1", permissions: ["TimeTracking.ViewOwn", "TimeTracking.ManageOwn"] };
    mocks.canMutate = true;
    mocks.effectiveReadOnly = false;
    mocks.entries = [];
    mocks.get.mockReset().mockImplementation(async (path: string) => {
      if (path.endsWith("/summary")) {
        return { actualHours: mocks.entries.length, plannedHours: 0, workVarianceHours: mocks.entries.length, actualUtilization: 0, byTeam: [] };
      }
      return { items: [...mocks.entries], totalCount: mocks.entries.length, page: 1, pageSize: 100, totalPages: 1 };
    });
    mocks.post.mockReset().mockImplementation(async (_path: string, payload: Record<string, unknown>) => {
      const entry = buildEntry({
        id: `entry-${mocks.entries.length + 1}`,
        projectId: payload.projectId,
        workDate: payload.workDate,
        startTime: payload.startTime,
        endTime: payload.endTime,
        description: payload.description,
      });
      mocks.entries = [...mocks.entries, entry];
      return entry;
    });
  });

  it("opens an accessible responsive side sheet with labeled required fields", async () => {
    const user = userEvent.setup();
    renderPage();

    const sheet = await openCreateSheet(user);
    expect(sheet).toHaveAttribute("aria-modal", "true");
    expect(within(sheet).getByRole("heading", { name: "New manual time entry" })).toBeInTheDocument();
    expect(within(sheet).getByLabelText("Project")).toBeRequired();
    expect(within(sheet).getByLabelText("Work date")).toBeRequired();
    expect(within(sheet).getByLabelText("Start")).toBeRequired();
    expect(within(sheet).getByLabelText("End")).toBeRequired();
    expect(within(sheet).getByLabelText("Description")).toBeInTheDocument();
    const panel = sheet.querySelector(".w-full");
    expect(panel).toHaveClass("w-full", "sm:max-w-lg", "end-0");
    expect(within(sheet).getByRole("button", { name: "Save" })).toBeEnabled();
  });

  it("localizes the sheet controls and preserves Arabic RTL", async () => {
    const user = userEvent.setup();
    renderPage("ar");

    await user.click(screen.getByRole("button", { name: "إضافة سجل" }));
    const sheet = await screen.findByRole("dialog", { name: "سجل وقت يدوي جديد" });

    expect(within(sheet).getByLabelText("المشروع")).toBeRequired();
    expect(within(sheet).getByLabelText("تاريخ العمل")).toBeRequired();
    expect(within(sheet).getByLabelText("البداية")).toBeRequired();
    expect(within(sheet).getByLabelText("النهاية")).toBeRequired();
    expect(within(sheet).getByLabelText("الوصف")).toBeInTheDocument();
    expect(within(sheet).getByRole("button", { name: "حفظ" })).toBeEnabled();
    expect(document.documentElement).toHaveAttribute("dir", "rtl");
  });

  it("cancels without creating a record and protects edited form data", async () => {
    const user = userEvent.setup();
    renderPage();

    let sheet = await openCreateSheet(user);
    await user.click(within(sheet).getByText("Cancel", { selector: "button" }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "New manual time entry" })).not.toBeInTheDocument());
    expect(mocks.post).not.toHaveBeenCalled();

    sheet = await openCreateSheet(user);
    await user.type(within(sheet).getByLabelText("Description"), "Draft note");
    await user.click(within(sheet).getByText("Cancel", { selector: "button" }));
    const warning = await screen.findByRole("alertdialog");
    expect(warning).toHaveTextContent(/unsaved/i);
    await user.click(within(warning).getByRole("button", { name: "Cancel" }));
    expect(screen.getByRole("dialog", { name: "New manual time entry" })).toBeInTheDocument();
    expect(screen.getByLabelText("Description")).toHaveValue("Draft note");

    const reopenedSheet = screen.getByRole("dialog", { name: "New manual time entry" });
    await user.click(within(reopenedSheet).getByText("Cancel", { selector: "button" }));
    const confirmDiscard = await screen.findByRole("alertdialog");
    await user.click(within(confirmDiscard).getByRole("button", { name: "Discard changes" }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "New manual time entry" })).not.toBeInTheDocument());
    expect(mocks.post).not.toHaveBeenCalled();
  });

  it("keeps required-field validation in the form and does not submit invalid values", async () => {
    const user = userEvent.setup();
    renderPage();
    const sheet = await openCreateSheet(user);
    const form = within(sheet).getByRole("button", { name: "Save" }).closest("form");
    expect(form).not.toBeNull();
    expect(form).not.toBeValid();

    fireEvent.submit(form!);
    expect(mocks.post).not.toHaveBeenCalled();
    expect(within(sheet).getByLabelText("Project")).toBeInvalid();
  });

  it("keeps entered values and shows an actionable save error after an API failure", async () => {
    const user = userEvent.setup();
    mocks.post.mockRejectedValueOnce(new Error("Validation failed"));
    renderPage();
    const sheet = await openCreateSheet(user);
    await user.selectOptions(within(sheet).getByLabelText("Project"), "project-1");
    await user.clear(within(sheet).getByLabelText("Description"));
    await user.type(within(sheet).getByLabelText("Description"), "Keep this after the error");
    await user.click(within(sheet).getByRole("button", { name: "Save" }));

    expect(await within(sheet).findByRole("alert")).toHaveTextContent("The entry could not be saved.");
    expect(screen.getByRole("dialog", { name: "New manual time entry" })).toBeInTheDocument();
    expect(within(sheet).getByLabelText("Description")).toHaveValue("Keep this after the error");
  });

  it("maps API field validation to an accessible field error", async () => {
    const user = userEvent.setup();
    const validationError = new ApiError(400, "Validation failed", undefined, {
      errors: { projectId: ["The selected project is not available for this time entry."] },
    });
    mocks.post.mockRejectedValueOnce(validationError);
    renderPage();
    const sheet = await openCreateSheet(user);
    await user.selectOptions(within(sheet).getByLabelText("Project"), "project-1");
    await user.click(within(sheet).getByRole("button", { name: "Save" }));

    const projectField = within(sheet).getByLabelText("Project");
    expect(await within(sheet).findByText(validationError.fieldErrors.projectId[0])).toHaveAttribute("role", "alert");
    expect(projectField).toHaveAttribute("aria-invalid", "true");
    expect(projectField).toHaveAttribute("aria-describedby", "time-entry-project-api-error");
    expect(screen.getByRole("dialog", { name: "New manual time entry" })).toBeInTheDocument();
  });

  it("saves, closes, and refreshes the existing time entry and summary queries", async () => {
    const user = userEvent.setup();
    renderPage();
    await waitFor(() => {
      expect(mocks.get).toHaveBeenCalledWith("/api/time-entries", expect.any(Object));
      expect(mocks.get).toHaveBeenCalledWith("/api/time-entries/summary", expect.any(Object));
    });
    const initialListReads = mocks.get.mock.calls.filter(([path]) => path === "/api/time-entries").length;
    const initialSummaryReads = mocks.get.mock.calls.filter(([path]) => path === "/api/time-entries/summary").length;

    const sheet = await openCreateSheet(user);
    await user.selectOptions(within(sheet).getByLabelText("Project"), "project-1");
    fireEvent.change(within(sheet).getByLabelText("Work date"), { target: { value: "2026-11-06" } });
    fireEvent.change(within(sheet).getByLabelText("Start"), { target: { value: "08:30" } });
    fireEvent.change(within(sheet).getByLabelText("End"), { target: { value: "10:00" } });
    await user.clear(within(sheet).getByLabelText("Description"));
    await user.type(within(sheet).getByLabelText("Description"), "Entered in side sheet");
    await user.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.queryByRole("dialog", { name: "New manual time entry" })).not.toBeInTheDocument());
    expect(mocks.post).toHaveBeenCalledWith("/api/time-entries", {
      projectId: "project-1",
      workDate: "2026-11-06",
      startTime: "08:30:00",
      endTime: "10:00:00",
      description: "Entered in side sheet",
    });
    await waitFor(() => {
      expect(mocks.get.mock.calls.filter(([path]) => path === "/api/time-entries").length).toBeGreaterThan(initialListReads);
      expect(mocks.get.mock.calls.filter(([path]) => path === "/api/time-entries/summary").length).toBeGreaterThan(initialSummaryReads);
    });
    expect(screen.getByText("Entered in side sheet")).toBeInTheDocument();
  });
});
