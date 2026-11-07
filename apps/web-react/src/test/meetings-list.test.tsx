import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { getStatusTone } from "@lib/status-presentation";
import { MeetingsPageFull } from "@features/delivery/MeetingPages";

const mocks = vi.hoisted(() => ({
  useFullMeetings: vi.fn(),
  useMeetingCalendar: vi.fn(),
  useMeetingProjectOptions: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "user-1", permissions: ["Meeting.View", "Meeting.Create", "Meeting.Manage"] }, status: "authenticated" }),
}));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@api/meetingsFull", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/meetingsFull")>();
  return {
    ...actual,
    useFullMeetings: mocks.useFullMeetings,
    useMeetingCalendar: mocks.useMeetingCalendar,
    useMeetingProjectOptions: mocks.useMeetingProjectOptions,
  };
});

const projectOptions = [
  { id: "project-1", publicId: "public-project-1", name: "Core Modernization", code: "PRJ-1" },
  { id: "project-2", publicId: "public-project-2", name: "Digital Rollout", code: "PRJ-2" },
  { id: "project-3", publicId: "public-project-3", name: "Cloud Migration", code: "PRJ-3" },
];

const meetingsResult = {
  items: [{ id: "meeting-1", publicId: "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d", title: "Planning review", projectId: "project-1", projectPublicId: "public-project-1", projectName: "Core Modernization", projectCode: "PRJ-1", organizerName: "Alex Admin", startTime: "2026-11-15T09:00:00Z", endTime: "2026-11-15T10:00:00Z", status: "Scheduled", attendeeCount: 3, minutesState: null }],
  totalCount: 21,
  page: 1,
  pageSize: 20,
  totalPages: 2,
};

const calendarResult = {
  from: "2026-11-01T00:00:00Z",
  to: "2026-12-15T23:59:59Z",
  totalCount: 1,
  isTruncated: false,
  items: meetingsResult.items,
};

function renderPage() {
  return render(<MemoryRouter initialEntries={["/meetings"]}><I18nProvider><MeetingsPageFull /><CurrentPath /></I18nProvider></MemoryRouter>);
}

function CurrentPath() {
  const location = useLocation();
  return <output data-testid="current-path">{location.pathname}{location.search}</output>;
}

describe("standalone meetings browser", () => {
  beforeEach(() => {
    mocks.useFullMeetings.mockReset();
    mocks.useFullMeetings.mockReturnValue({ data: meetingsResult, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useMeetingCalendar.mockReset();
    mocks.useMeetingCalendar.mockReturnValue({ data: calendarResult, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useMeetingProjectOptions.mockReset();
    mocks.useMeetingProjectOptions.mockReturnValue({ data: projectOptions, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });
  });

  it("sends deferred search, multi-project, status, date-range and paging filters to the API hook", async () => {
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Clear all" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Core Modernization" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Digital Rollout" }));
    fireEvent.click(screen.getByRole("button", { name: "Status" }));
    fireEvent.click(screen.getByRole("option", { name: "Completed" }));
    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "planning" } });
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-10-01" } });
    fireEvent.change(screen.getByLabelText("To"), { target: { value: "2026-10-31" } });
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      pageSize: 20,
      search: "planning",
      projectIds: "public-project-1,public-project-2",
      status: "Completed",
      dateFrom: new Date(2026, 9, 1).toISOString(),
      dateTo: new Date(2026, 9, 31, 23, 59, 59, 999).toISOString(),
    }), true));

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, projectIds: "public-project-1,public-project-2" }), true));
    expect(screen.getByText("Planning review")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Enter minutes/ })).toHaveAttribute("href", "/meetings/f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d/minutes");
  });

  it("keeps the selected filters when switching between calendar and list views", async () => {
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Clear all" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Core Modernization" }));
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({ projectIds: "public-project-1" }), true));

    fireEvent.click(screen.getByRole("tab", { name: "Calendar" }));

    await waitFor(() => expect(mocks.useMeetingCalendar).toHaveBeenLastCalledWith(expect.objectContaining({
      projectIds: "public-project-1",
      from: expect.any(String),
      to: expect.any(String),
    }), true));
    expect(screen.getByText("Planning review")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "List" }));
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({ projectIds: "public-project-1", page: 1 }), true));
    expect(screen.getByTestId("current-path")).toHaveTextContent("/meetings");
  });

  it("navigates to the full-page create flow instead of opening a sheet", () => {
    renderPage();

    fireEvent.click(screen.getByRole("link", { name: "New meeting" }));

    expect(screen.getByTestId("current-path")).toHaveTextContent("/meetings/new");
  });

  it("uses text-only semantic colors for meeting and minutes states", () => {
    renderPage();
    const row = screen.getByRole("row", { name: /Planning review/ });
    const cells = within(row).getAllByRole("cell");

    expect(cells[5].querySelector("span")).toHaveClass("bg-transparent", "text-info");
    expect(cells[6].querySelector("span")).toHaveClass("text-muted-foreground");
    expect(cells[6].querySelector("span")).not.toHaveClass("bg-primary");
    expect(getStatusTone("Draft")).toBe("amber");
    expect(getStatusTone("Final")).toBe("green");
  });
});
