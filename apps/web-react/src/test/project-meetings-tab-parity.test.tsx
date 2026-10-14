import { render, screen } from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { ApiError } from "@api/client";
import { ProjectMeetingsTab } from "@features/projects/ProjectGovernanceTabs";
import type { ProjectDto } from "@features/projects/api/projects";

const mocks = vi.hoisted(() => ({
  useFullMeetings: vi.fn(),
  user: null as { id: string; permissions: string[] } | null,
  status: "authenticated",
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user, status: mocks.status }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@api/meetingsFull", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/meetingsFull")>();
  return { ...actual, useFullMeetings: mocks.useFullMeetings, useMeetingFullMutations: () => ({ create: { mutate: vi.fn(), isPending: false } }) };
});
vi.mock("@features/projects/lib/project-lookups", () => ({ useUserLookup: () => ({ data: [], isLoading: false }) }));

const project = { id: "project-1", name: "Core Modernization", code: "PRJ-1" } as ProjectDto;

function renderTab(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <MemoryRouter initialEntries={["/projects/project-1/meetings"]}>
      <I18nProvider>
        <Routes>
          <Route path="/projects/:id" element={<Outlet context={{ projectId: project.id, project }} />}>
            <Route path="meetings" element={<ProjectMeetingsTab />} />
          </Route>
        </Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

describe("project meetings permission parity", () => {
  beforeEach(() => {
    mocks.user = { id: "user-1", permissions: [] };
    mocks.status = "authenticated";
    mocks.useFullMeetings.mockReset().mockReturnValue({ data: { items: [], totalPages: 0, totalCount: 0 }, isLoading: false, isError: false, refetch: vi.fn() });
  });

  it("does not enable the meetings query when the user lacks Meeting.View", () => {
    mocks.user = { id: "user-1", permissions: ["Meeting.Create"] };
    renderTab();

    expect(mocks.useFullMeetings).toHaveBeenCalledWith(expect.objectContaining({ projectId: "project-1" }), false);
    expect(screen.getByText("You do not have permission to view this project's meetings.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "New meeting" })).not.toBeInTheDocument();
    expect(screen.queryByPlaceholderText("Search meetings by title or objective...")).not.toBeInTheDocument();
  });

  it("enables project-scoped meetings for a user with Meeting.View", () => {
    mocks.user = { id: "user-1", permissions: ["Meeting.View"] };

    renderTab();

    expect(mocks.useFullMeetings).toHaveBeenCalledWith(expect.objectContaining({ projectId: "project-1" }), true);
  });

  it("keeps the query disabled until authentication is established", () => {
    mocks.status = "loading";
    mocks.user = null;

    const { container } = renderTab();

    expect(mocks.useFullMeetings).toHaveBeenCalledWith(expect.objectContaining({ projectId: "project-1" }), false);
    expect(container.querySelectorAll('[class*="h-16"]')).toHaveLength(3);
    expect(screen.queryByText("You do not have permission to view this project's meetings.")).not.toBeInTheDocument();
  });

  it("shows the localized permission error for a server-side 403 and allows retry", () => {
    mocks.user = { id: "user-1", permissions: ["Meeting.View"] };
    const refetch = vi.fn();
    mocks.useFullMeetings.mockReturnValue({ isLoading: false, isError: true, error: new ApiError(403, "Forbidden"), refetch });

    renderTab();

    expect(screen.getByText("You do not have permission to view this project's meetings.")).toBeInTheDocument();
    screen.getByRole("button", { name: "Retry" }).click();
    expect(refetch).toHaveBeenCalledOnce();
  });

  it("shows the Arabic permission message without enabling the query", () => {
    renderTab("ar");

    expect(mocks.useFullMeetings).toHaveBeenCalledWith(expect.objectContaining({ projectId: "project-1" }), false);
    expect(screen.getByText("ليس لديك صلاحية عرض اجتماعات هذا المشروع.")).toBeInTheDocument();
  });
});
