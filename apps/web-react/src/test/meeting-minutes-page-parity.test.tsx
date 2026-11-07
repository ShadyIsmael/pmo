import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { MeetingDetailPageFull } from "../features/delivery/MeetingPages";

const mocks = vi.hoisted(() => ({
  meeting: vi.fn(),
  minutes: vi.fn(),
  meetingMutations: vi.fn(),
  permissions: ["Meeting.View", "Meeting.Edit", "Meeting.Manage"],
  endMeeting: vi.fn(),
  collaboration: vi.fn(({ mainContent }: { mainContent: ReactNode }) => mainContent),
  createMinutes: vi.fn(),
  updateMinutes: vi.fn(),
  finalizeMinutes: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@api/meetingsFull", () => {
  const mutation = () => ({ isPending: false, isSuccess: false, mutate: vi.fn(), mutateAsync: vi.fn() });
  return {
  useFullMeeting: mocks.meeting,
  useMeetingMinutes: mocks.minutes,
  useFullMeetings: vi.fn(),
  useMeetingCalendar: vi.fn(),
  useMeetingProjectOptions: vi.fn(),
  useMeetingAgenda: () => ({ data: [], isLoading: false, isError: false }),
  useMeetingParticipants: () => ({ data: [], isLoading: false, isError: false }),
  useMeetingActionItems: () => ({ data: [], isLoading: false, isError: false }),
  useMeetingDecisions: () => ({ data: { decisions: "", isEditable: true }, isLoading: false, isError: false, isSuccess: true }),
  useMeetingWorkspaceMutations: () => ({
    createAgendaItem: mutation(), updateAgendaItem: mutation(), deleteAgendaItem: mutation(), reorderAgenda: mutation(),
    updateMyAttendance: mutation(), updateAttendance: mutation(), updateOrganizerAttendance: mutation(), saveDecisions: mutation(),
    createActionItem: mutation(), updateActionItem: mutation(), deleteActionItem: mutation(),
    linkActionWorkItem: mutation(), unlinkActionWorkItem: mutation(),
  }),
  useMeetingFullMutations: (id?: string, detailRouteId?: string) => {
    mocks.meetingMutations(id, detailRouteId);
    return ({
    update: { isPending: false, mutate: vi.fn() },
    delete: { isPending: false, mutate: vi.fn() },
    start: { isPending: false, mutate: vi.fn() },
    end: { isPending: false, mutate: mocks.endMeeting },
    createMinutes: { isPending: false, mutateAsync: mocks.createMinutes },
    updateMinutes: { isPending: false, mutateAsync: mocks.updateMinutes },
    finalizeMinutes: { isPending: false, mutateAsync: mocks.finalizeMinutes },
    });
  },
  };
});

vi.mock("@api/workItemsFull", () => ({
  useFullWorkItems: () => ({ data: { items: [], totalCount: 0, totalPages: 0 }, isLoading: false, isError: false }),
  useFullWorkItemMutations: () => ({ create: { isPending: false, isSuccess: false, mutate: vi.fn(), mutateAsync: vi.fn() } }),
}));

vi.mock("@features/projects/lib/project-lookups", () => ({
  useMeetingWorkflowUserLookup: () => ({ data: [], isLoading: false }),
}));
vi.mock("../features/delivery/MeetingCreateSheet", () => ({ MeetingCreateSheet: () => null }));
vi.mock("../features/delivery/MeetingMinutesCollaboration", () => ({
  MeetingMinutesCollaboration: (props: { mainContent: ReactNode }) => {
    mocks.collaboration(props);
    return props.mainContent;
  },
}));
vi.mock("../features/delivery/MeetingSharingDialog", () => ({ MeetingSharingDialog: () => null }));
vi.mock("../features/delivery/MeetingWorkspaceSections", () => ({
  MeetingWorkspaceSections: ({ workspaceView }: { workspaceView?: string }) => <div data-testid={`workspace-${workspaceView ?? "lifecycle"}`} />,
}));
vi.mock("@components/ui/rich-text-editor", () => ({
  RichTextContent: ({ value }: { value: string }) => <div>{value}</div>,
  RichTextEditor: ({ id, value, onChange, ariaLabel }: { id: string; value: string; onChange: (value: string) => void; ariaLabel: string }) => <textarea id={id} aria-label={ariaLabel} value={value} onChange={(event) => onChange(event.target.value)} />,
}));

const meeting = {
  id: "meeting-1",
  publicId: "53f762a8-cd25-4a3b-b821-f9897c1a14e0",
  title: "Weekly delivery review",
  projectId: "project-1",
  projectName: "Core Modernization",
  status: "Completed",
  minutesState: "Final",
  attendees: [],
  attendeeCount: 0,
  objective: "",
  organizerName: "Alex Admin",
  startTime: "2026-10-15T09:00:00Z",
  endTime: "2026-10-15T10:00:00Z",
  actualEndTime: "2026-10-15T10:03:00Z",
  rowVersion: "meeting-v1",
  location: "Room 1",
  reminderOffsetMinutes: null,
  meetingLink: null,
};

function renderMinutesPage() {
  window.localStorage.setItem("nexus-locale", "en");
  return render(
    <MemoryRouter initialEntries={[`/meetings/${meeting.publicId}/minutes`]}>
      <I18nProvider>
        <Routes><Route path="/meetings/:id/minutes" element={<MeetingDetailPageFull />} /></Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

function renderMeetingPage(locale: "en" | "ar" = "en", initialPath = `/meetings/${meeting.publicId}`) {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <I18nProvider>
        <Routes><Route path="/meetings/:id" element={<><MeetingDetailPageFull /><CurrentLocation /></>} /></Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

function CurrentLocation() {
  const { pathname, search } = useLocation();
  return <output data-testid="current-location">{pathname}{search}</output>;
}

describe("meeting minutes page parity", () => {
  beforeEach(() => {
    mocks.permissions = ["Meeting.View", "Meeting.Edit", "Meeting.Manage"];
    mocks.meeting.mockReset().mockReturnValue({ data: meeting, isLoading: false, isError: false });
    mocks.meetingMutations.mockReset();
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
    mocks.endMeeting.mockReset();
  });

  it("shows the draft/final badge beside the minutes title", () => {
    const { container } = renderMinutesPage();
    expect(container.querySelector("h1")).toHaveTextContent("Meeting minutes");
    expect(screen.getByText("Final minutes")).toBeInTheDocument();
    expect(screen.getByText("Weekly delivery review")).toBeInTheDocument();
    expect(screen.getByText("Decision record")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "Summary" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save draft" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Finalize minutes" })).not.toBeInTheDocument();
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

  it("opens an authorized empty draft by default after completion and keeps Meeting Details selectable", async () => {
    mocks.meeting.mockReturnValue({ data: { ...meeting, status: "Completed", minutesState: "Draft" }, isLoading: false, isError: false });
    mocks.minutes.mockReturnValue({ data: null, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });

    const { container } = renderMeetingPage();

    const minutesTab = screen.getByRole("tab", { name: "Minutes & Follow-up" });
    const detailsTab = screen.getByRole("tab", { name: "Meeting Details" });
    expect(minutesTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("textbox", { name: "Summary" })).toHaveValue("");
    expect(screen.getByRole("button", { name: "Save draft" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Finalize minutes" })).toBeInTheDocument();
    expect(container.querySelector("#meeting-completed-details-panel")).toBeInTheDocument();
    expect(container.querySelector("#meeting-completed-details-panel")).toHaveAttribute("hidden");

    fireEvent.click(detailsTab);
    expect(detailsTab).toHaveAttribute("aria-selected", "true");
    expect(screen.getByTestId("workspace-details")).toBeInTheDocument();
    expect(screen.getByText("Weekly delivery review")).toBeInTheDocument();
    expect(screen.getByText("Actual end")).toBeInTheDocument();
    expect(container.querySelector("#meeting-completed-minutes-panel")).toBeInTheDocument();
    expect(container.querySelector("#meeting-completed-minutes-panel")).toHaveAttribute("hidden");
  });

  it("resolves a PublicId route before using internal identity for Minutes and mutations", () => {
    const routePublicId = "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d";
    mocks.meeting.mockReturnValue({ data: { ...meeting, publicId: routePublicId, status: "Completed" }, isLoading: false, isError: false });
    mocks.minutes.mockReturnValue({ data: null, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });

    renderMeetingPage("en", `/meetings/${routePublicId}?tab=minutes&comment=a69b24b6-f0c2-4d10-8b60-5bc9f07a6c0e`);

    expect(mocks.meeting).toHaveBeenCalledWith(routePublicId);
    expect(mocks.minutes).toHaveBeenCalledWith("meeting-1", true);
    expect(mocks.meetingMutations).toHaveBeenCalledWith("meeting-1", routePublicId);
    expect(screen.getByTestId("current-location")).toHaveTextContent(`/meetings/${routePublicId}?tab=minutes&comment=a69b24b6-f0c2-4d10-8b60-5bc9f07a6c0e`);
    expect(screen.getByRole("tab", { name: "Minutes & Follow-up" })).toHaveAttribute("aria-selected", "true");
  });

  it("refreshes the completed meeting state after End Meeting and lands on Minutes & Follow-up", async () => {
    let currentMeeting = { ...meeting, status: "InProgress" };
    mocks.meeting.mockImplementation(() => ({ data: currentMeeting, isLoading: false, isError: false }));
    mocks.minutes.mockReturnValue({ data: null, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });
    mocks.endMeeting.mockImplementation((_rowVersion, options) => {
      currentMeeting = { ...currentMeeting, status: "Completed", actualEndTime: "2026-10-15T10:03:00Z" };
      options?.onSuccess?.(currentMeeting);
    });

    renderMeetingPage();
    fireEvent.click(screen.getByRole("button", { name: "End meeting" }));

    await waitFor(() => expect(mocks.endMeeting).toHaveBeenCalledWith("meeting-v1", expect.objectContaining({ onSuccess: expect.any(Function) })));
    expect(await screen.findByRole("tab", { name: "Minutes & Follow-up" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("textbox", { name: "Summary" })).toHaveValue("");
  });

  it("does not grant Minutes editing access to a completed-meeting viewer", () => {
    mocks.permissions = ["Meeting.View"];
    mocks.meeting.mockReturnValue({ data: { ...meeting, status: "Completed" }, isLoading: false, isError: false });
    mocks.minutes.mockReturnValue({ data: null, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });

    renderMeetingPage();

    expect(screen.getByText("No minutes recorded yet")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "Summary" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save draft" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Finalize minutes" })).not.toBeInTheDocument();
  });

  it("preserves an existing draft instead of creating a replacement", async () => {
    const existingDraft = { state: "Draft", summary: "Keep this draft", discussionNotes: "Existing notes", decisions: "Existing decision", outcomes: "Existing outcome", rowVersion: "draft-v7" };
    mocks.meeting.mockReturnValue({ data: { ...meeting, status: "Completed", minutesState: "Draft" }, isLoading: false, isError: false });
    mocks.minutes.mockReturnValue({ data: existingDraft, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() });
    mocks.updateMinutes.mockResolvedValue({ rowVersion: "draft-v8" });

    renderMinutesPage();
    expect(await screen.findByRole("textbox", { name: "Summary" })).toHaveValue("Keep this draft");
    fireEvent.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => expect(mocks.updateMinutes).toHaveBeenCalledWith(expect.objectContaining({ summary: "Keep this draft", rowVersion: "draft-v7" })));
    expect(mocks.createMinutes).not.toHaveBeenCalled();
  });

  it("keeps a Minutes API failure visible instead of treating it as an empty draft", () => {
    mocks.meeting.mockReturnValue({ data: { ...meeting, status: "Completed" }, isLoading: false, isError: false });
    mocks.minutes.mockReturnValue({ data: undefined, isLoading: false, isError: true, isSuccess: false, refetch: vi.fn() });

    renderMeetingPage();

    expect(screen.getByText("Meeting minutes could not be loaded.")).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "Summary" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  it("localizes the Minutes query failure and retry action in Arabic with RTL", () => {
    mocks.meeting.mockReturnValue({ data: { ...meeting, status: "Completed" }, isLoading: false, isError: false });
    mocks.minutes.mockReturnValue({ data: undefined, isLoading: false, isError: true, isSuccess: false, refetch: vi.fn() });

    renderMeetingPage("ar");

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
    expect(screen.queryByRole("textbox", { name: "الملخص" })).not.toBeInTheDocument();
  });
});
