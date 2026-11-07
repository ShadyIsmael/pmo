import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { meetingsApiFull, meetingKeys, useFullMeeting, useMeetingFullMutations, useMeetingMinutes, type MeetingDetailDto, type MeetingMinutesDto } from "@api/meetingsFull";

const inProgressMeeting: MeetingDetailDto = {
  id: "meeting-query-test",
  publicId: "09bd48bc-9f63-4308-a333-10141e53db33",
  title: "Query invalidation meeting",
  projectId: "project-1",
  startTime: "2026-11-05T09:00:00Z",
  endTime: "2026-11-05T10:00:00Z",
  status: "InProgress",
  attendeeCount: 0,
  isRecurring: false,
  attendees: [],
  rowVersion: "meeting-v1",
};

const completedMeeting: MeetingDetailDto = {
  ...inProgressMeeting,
  status: "Completed",
  actualStartTime: "2026-11-05T09:01:00Z",
  actualEndTime: "2026-11-05T10:02:00Z",
  rowVersion: "meeting-v2",
};

const cachedMinutes: MeetingMinutesDto = {
  id: "minutes-1",
  meetingId: inProgressMeeting.id,
  summary: "Stale cached minutes",
  state: "Draft",
  recordedAt: "2026-11-05T10:02:00Z",
  rowVersion: "minutes-v1",
};

const currentMinutes: MeetingMinutesDto = {
  ...cachedMinutes,
  summary: "Minutes refreshed after completion",
  rowVersion: "minutes-v2",
};

function MeetingEndHarness() {
  const meeting = useFullMeeting(inProgressMeeting.publicId!);
  const minutes = useMeetingMinutes(meeting.data?.id, meeting.data?.status === "Completed");
  const mutations = useMeetingFullMutations(meeting.data?.id, inProgressMeeting.publicId!);

  return <div>
    <p data-testid="meeting-status">{meeting.data?.status}</p>
    {meeting.data?.status === "Completed" ? <p>{minutes.data?.summary ?? "Minutes loading"}</p> : null}
    <button type="button" onClick={() => mutations.end.mutate(meeting.data?.rowVersion)}>End meeting</button>
  </div>;
}

afterEach(() => vi.restoreAllMocks());

describe("Minutes query after meeting completion", () => {
  it("invalidates a cached Minutes query and loads current content after End Meeting", async () => {
    const detail = vi.spyOn(meetingsApiFull, "detail")
      .mockResolvedValueOnce(inProgressMeeting)
      .mockResolvedValue(completedMeeting);
    const end = vi.spyOn(meetingsApiFull, "end").mockResolvedValue(completedMeeting);
    const minutes = vi.spyOn(meetingsApiFull, "minutes").mockResolvedValue(currentMinutes);
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } },
    });
    client.setQueryData(meetingKeys.minutes(inProgressMeeting.id), cachedMinutes);

    render(<QueryClientProvider client={client}><I18nProvider><MeetingEndHarness /></I18nProvider></QueryClientProvider>);
    await waitFor(() => expect(screen.getByTestId("meeting-status")).toHaveTextContent("InProgress"));
    expect(screen.queryByText("Stale cached minutes")).not.toBeInTheDocument();
    expect(minutes).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "End meeting" }));

    await waitFor(() => expect(end).toHaveBeenCalledWith(inProgressMeeting.id, "meeting-v1"));
    expect(await screen.findByText("Minutes refreshed after completion")).toBeInTheDocument();
    expect(screen.getByTestId("meeting-status")).toHaveTextContent("Completed");
    expect(detail).toHaveBeenCalledTimes(2);
    expect(detail).toHaveBeenNthCalledWith(1, inProgressMeeting.publicId);
    expect(detail).toHaveBeenNthCalledWith(2, inProgressMeeting.publicId);
    expect(client.getQueryData(meetingKeys.detail(inProgressMeeting.publicId!))).toMatchObject({ id: inProgressMeeting.id });
    expect(client.getQueryData(meetingKeys.detail(inProgressMeeting.id))).toBeUndefined();
    expect(minutes).toHaveBeenCalledTimes(1);
  });
});
