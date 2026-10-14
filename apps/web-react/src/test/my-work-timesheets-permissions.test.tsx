import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { MyWorkTimesheetsPage } from "@features/my-work/MyWorkTimesheetsPage";

const mocks = vi.hoisted(() => ({
  user: { id: "user-1", permissions: ["TimeTracking.ViewOwn", "TimeTracking.ManageOwn"] as string[] } as { id: string; permissions: string[] } | null,
  status: "authenticated",
  canMutate: true,
  effectiveReadOnly: false,
  useProjects: vi.fn(),
  useTimeEntries: vi.fn(),
  useTimeEntrySummary: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user, status: mocks.status }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: mocks.canMutate, effectiveReadOnly: mocks.effectiveReadOnly }) }));
vi.mock("@features/projects/api/projects", () => ({ useProjects: mocks.useProjects }));
vi.mock("@features/my-work/api/myWork", () => ({
  useTimeEntries: mocks.useTimeEntries,
  useTimeEntrySummary: mocks.useTimeEntrySummary,
  useCreateTimeEntry: () => ({ mutate: vi.fn(), isError: false, isPending: false }),
  useUpdateTimeEntry: () => ({ mutate: vi.fn(), isError: false, isPending: false }),
  useDeleteTimeEntry: () => ({ mutate: vi.fn(), isPending: false }),
}));

const entry = {
  id: "entry-1", projectId: "project-1", projectName: "Core Modernization", projectCode: "PRJ-1",
  workDate: "2026-10-07", startTime: "09:00:00", endTime: "10:00:00", actualHours: 1,
  description: "Review", rowVersion: [1],
};

function renderPage(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(<I18nProvider><MyWorkTimesheetsPage /></I18nProvider>);
}

describe("My Work time-tracking permission parity", () => {
  beforeEach(() => {
    mocks.user = { id: "user-1", permissions: ["TimeTracking.ViewOwn", "TimeTracking.ManageOwn"] };
    mocks.status = "authenticated";
    mocks.canMutate = true;
    mocks.effectiveReadOnly = false;
    mocks.useProjects.mockReset().mockReturnValue({ data: { items: [] } });
    mocks.useTimeEntries.mockReset().mockImplementation(() => ({ data: { items: [entry] }, isLoading: false, isError: false }));
    mocks.useTimeEntrySummary.mockReset().mockReturnValue({ data: { actualHours: 1, plannedHours: 1, workVarianceHours: 0, actualUtilization: 10, byTeam: [] } });
  });

  it("does not fetch or display time-tracking data without a view permission", () => {
    mocks.user = { id: "user-1", permissions: [] };

    renderPage();

    expect(mocks.useTimeEntries).toHaveBeenCalledWith(expect.any(Object), false);
    expect(mocks.useTimeEntrySummary).toHaveBeenCalledWith(expect.any(Object), false);
    expect(mocks.useProjects).toHaveBeenCalledWith(expect.any(Object), false);
    expect(screen.getByRole("alert")).toHaveTextContent("You do not have permission to view time entries.");
    expect(screen.queryByText("Core Modernization")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add entry" })).not.toBeInTheDocument();
  });

  it("allows personal time entry actions with ManageOwn", () => {
    renderPage();

    expect(mocks.useTimeEntries).toHaveBeenCalledWith(expect.any(Object), true);
    expect(mocks.useProjects).toHaveBeenCalledWith(expect.any(Object), true);
    expect(screen.getByRole("button", { name: "Add entry" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Edit time entry" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete" })).toBeInTheDocument();
  });

  it("does not expose mixed personal/team row actions without ManageTeam", () => {
    mocks.user = { id: "user-1", permissions: ["TimeTracking.ViewTeam", "TimeTracking.ManageOwn"] };

    renderPage();

    expect(screen.getByRole("button", { name: "Add entry" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit time entry" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("keeps time entries read-only in ReadOnlyViewAs", () => {
    mocks.effectiveReadOnly = true;

    renderPage();

    expect(mocks.useTimeEntries).toHaveBeenCalledWith(expect.any(Object), true);
    expect(mocks.useProjects).toHaveBeenCalledWith(expect.any(Object), false);
    expect(screen.queryByRole("button", { name: "Add entry" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit time entry" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("localizes the access-denied message in Arabic", () => {
    mocks.user = { id: "user-1", permissions: [] };

    renderPage("ar");

    expect(screen.getByRole("alert")).toHaveTextContent("ليس لديك صلاحية عرض سجلات الوقت.");
  });
});
