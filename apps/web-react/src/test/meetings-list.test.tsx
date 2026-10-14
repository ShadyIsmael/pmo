import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { getStatusTone } from "@lib/status-presentation";
import { MeetingsPageFull } from "@features/delivery/MeetingPages";

const mocks = vi.hoisted(() => ({ useFullMeetings: vi.fn() }));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "user-1", permissions: ["Meeting.View", "Meeting.Create", "Meeting.Manage"] } }),
}));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@features/projects/api/projects", () => ({
  useProjects: () => ({ data: { items: [{ id: "project-1", name: "Core Modernization", code: "PRJ-1" }] }, isLoading: false }),
}));
vi.mock("../features/delivery/MeetingCreateSheet", () => ({
  MeetingCreateSheet: ({ open, onOpenChange, onCreated }: { open: boolean; onOpenChange: (open: boolean) => void; onCreated?: (meeting: { id: string }) => void }) => open
    ? <button type="button" onClick={() => { onOpenChange(false); onCreated?.({ id: "created-meeting" }); }}>Finish create</button>
    : null,
}));
vi.mock("@api/meetingsFull", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/meetingsFull")>();
  return { ...actual, useFullMeetings: mocks.useFullMeetings };
});

const meetingsResult = {
  items: [{ id: "meeting-1", title: "Planning review", projectId: "project-1", projectName: "Core Modernization", organizerName: "Alex Admin", startTime: "2026-10-15T09:00:00Z", endTime: "2026-10-15T10:00:00Z", status: "Scheduled", attendeeCount: 3, minutesState: null }],
  totalCount: 21,
  page: 1,
  pageSize: 20,
  totalPages: 2,
};

function renderPage() {
  return render(<MemoryRouter initialEntries={["/meetings"]}><I18nProvider><MeetingsPageFull /><CurrentPath /></I18nProvider></MemoryRouter>);
}

function CurrentPath() {
  const location = useLocation();
  return <output data-testid="current-path">{location.pathname}</output>;
}

describe("standalone meetings list", () => {
  beforeEach(() => {
    mocks.useFullMeetings.mockReset();
    mocks.useFullMeetings.mockReturnValue({ data: meetingsResult, isLoading: false, isError: false, refetch: vi.fn() });
  });

  it("sends deferred search, project, status, date-range and paging filters to the API hook", async () => {
    renderPage();

    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "planning" } });
    fireEvent.click(screen.getByRole("combobox"));
    fireEvent.click(screen.getByRole("option", { name: "Core Modernization · PRJ-1" }));
    fireEvent.click(screen.getByRole("button", { name: "Status" }));
    fireEvent.click(screen.getByRole("option", { name: "Completed" }));
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-10-01" } });
    fireEvent.change(screen.getByLabelText("To"), { target: { value: "2026-10-31" } });

    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      pageSize: 20,
      search: "planning",
      projectId: "project-1",
      status: "Completed",
      dateFrom: "2026-10-01",
      dateTo: "2026-10-31T23:59:59",
    }), true));

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, projectId: "project-1" }), true));
    expect(screen.getByText("Planning review")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Enter minutes/ })).toHaveAttribute("href", "/meetings/meeting-1/minutes");
  });

  it("keeps the user on the list and returns to page one after creating a meeting", async () => {
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }), true));
    fireEvent.click(screen.getByRole("button", { name: "New meeting" }));
    fireEvent.click(screen.getByRole("button", { name: "Finish create" }));

    expect(screen.getByTestId("current-path")).toHaveTextContent("/meetings");
    await waitFor(() => expect(mocks.useFullMeetings).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1 }), true));
    expect(screen.queryByRole("button", { name: "Finish create" })).not.toBeInTheDocument();
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
