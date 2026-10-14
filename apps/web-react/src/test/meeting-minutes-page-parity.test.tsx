import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { MeetingDetailPageFull } from "../features/delivery/MeetingPages";

const mocks = vi.hoisted(() => ({
  meeting: vi.fn(),
  minutes: vi.fn(),
  collaboration: vi.fn(({ mainContent }: { mainContent: ReactNode }) => mainContent),
  createMinutes: vi.fn(),
  updateMinutes: vi.fn(),
  finalizeMinutes: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: ["Meeting.View", "Meeting.Manage"] } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@api/meetingsFull", () => ({
  useFullMeeting: mocks.meeting,
  useMeetingMinutes: mocks.minutes,
  useFullMeetings: vi.fn(),
  useMeetingFullMutations: () => ({
    update: { isPending: false, mutate: vi.fn() },
    delete: { isPending: false, mutate: vi.fn() },
    createMinutes: { isPending: false, mutateAsync: mocks.createMinutes },
    updateMinutes: { isPending: false, mutateAsync: mocks.updateMinutes },
    finalizeMinutes: { isPending: false, mutateAsync: mocks.finalizeMinutes },
  }),
}));
vi.mock("../features/delivery/MeetingCreateSheet", () => ({ MeetingCreateSheet: () => null }));
vi.mock("../features/delivery/MeetingMinutesCollaboration", () => ({
  MeetingMinutesCollaboration: (props: { mainContent: ReactNode }) => {
    mocks.collaboration(props);
    return props.mainContent;
  },
}));
vi.mock("@components/ui/rich-text-editor", () => ({
  RichTextContent: ({ value }: { value: string }) => <div>{value}</div>,
  RichTextEditor: ({ id, value, onChange, ariaLabel }: { id: string; value: string; onChange: (value: string) => void; ariaLabel: string }) => <textarea id={id} aria-label={ariaLabel} value={value} onChange={(event) => onChange(event.target.value)} />,
}));

const meeting = {
  id: "meeting-1",
  title: "Weekly delivery review",
  projectId: "project-1",
  projectName: "Core Modernization",
  status: "Scheduled",
  minutesState: "Final",
  attendees: [],
  attendeeCount: 0,
  objective: "",
  organizerName: "Alex Admin",
  startTime: "2026-10-15T09:00:00Z",
  endTime: "2026-10-15T10:00:00Z",
  location: "Room 1",
  reminderOffsetMinutes: null,
  meetingLink: null,
};

function renderMinutesPage() {
  window.localStorage.setItem("nexus-locale", "en");
  return render(
    <MemoryRouter initialEntries={["/meetings/meeting-1/minutes"]}>
      <I18nProvider>
        <Routes><Route path="/meetings/:id/minutes" element={<MeetingDetailPageFull />} /></Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

describe("meeting minutes page parity", () => {
  beforeEach(() => {
    mocks.meeting.mockReset().mockReturnValue({ data: meeting, isLoading: false, isError: false });
    mocks.minutes.mockReset().mockReturnValue({
      data: { state: "Final", summary: "Decision record", discussionNotes: "Notes", decisions: "Approved", outcomes: "Proceed", rowVersion: "v1" },
      isLoading: false,
      isError: false,
      isSuccess: true,
      refetch: vi.fn(),
    });
    mocks.collaboration.mockClear();
    mocks.createMinutes.mockReset().mockResolvedValue({ rowVersion: "v2" });
    mocks.updateMinutes.mockReset().mockResolvedValue({ rowVersion: "v2" });
    mocks.finalizeMinutes.mockReset().mockResolvedValue({ rowVersion: "v2" });
  });

  it("shows the draft/final badge beside the minutes title", () => {
    const { container } = renderMinutesPage();
    expect(container.querySelector("h1")).toHaveTextContent("Meeting minutes");
    expect(screen.getByText("Final minutes")).toBeInTheDocument();
    expect(screen.getByText("Weekly delivery review")).toBeInTheDocument();
  });

  it("waits for minutes data before rendering the page shell", () => {
    mocks.minutes.mockReturnValue({ data: undefined, isLoading: true, isError: false, isSuccess: false });
    renderMinutesPage();

    expect(screen.queryByRole("heading", { name: "Meeting minutes" })).not.toBeInTheDocument();
    expect(mocks.collaboration).not.toHaveBeenCalled();
  });

  it("sends the meeting summary when creating minutes", async () => {
    mocks.minutes.mockReturnValue({ data: null, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });
    renderMinutesPage();

    fireEvent.change(screen.getByRole("textbox", { name: "Summary" }), { target: { value: "Decisions and next steps" } });
    fireEvent.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => expect(mocks.createMinutes).toHaveBeenCalledWith(expect.objectContaining({ summary: "Decisions and next steps" })));
  });
});
