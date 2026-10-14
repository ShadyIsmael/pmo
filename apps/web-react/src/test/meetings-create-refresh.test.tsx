import type { PropsWithChildren } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { meetingKeys, meetingsApiFull, useFullMeetings, useMeetingFullMutations, type MeetingDetailDto, type MeetingInput, type MeetingListDto } from "@api/meetingsFull";

const listResult = (items: MeetingListDto[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1 });

const firstMeeting: MeetingListDto = {
  id: "meeting-1",
  title: "Planning review",
  startTime: "2026-10-15T09:00:00Z",
  endTime: "2026-10-15T10:00:00Z",
  status: "Scheduled",
  attendeeCount: 1,
};

const createdMeeting: MeetingDetailDto = {
  ...firstMeeting,
  id: "meeting-2",
  title: "New planning session",
  objective: "Review the delivery plan",
  rowVersion: "version-1",
  attendees: [],
};

const meetingInput: MeetingInput = {
  title: createdMeeting.title,
  objective: createdMeeting.objective ?? undefined,
  startTime: createdMeeting.startTime,
  endTime: createdMeeting.endTime,
  organizerId: "organizer-1",
  attendees: [],
};

afterEach(() => vi.restoreAllMocks());

describe("meeting creation list refresh", () => {
  it("waits for the active meetings list to reload after creation succeeds", async () => {
    let resolveRefetch: ((result: ReturnType<typeof listResult>) => void) | undefined;
    const list = vi.spyOn(meetingsApiFull, "list")
      .mockResolvedValueOnce(listResult([firstMeeting]))
      .mockImplementationOnce(() => new Promise((resolve) => { resolveRefetch = resolve; }));
    vi.spyOn(meetingsApiFull, "create").mockResolvedValue(createdMeeting);

    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
    const wrapper = ({ children }: PropsWithChildren) => <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    const { result, unmount } = renderHook(() => ({
      meetings: useFullMeetings({ page: 1, pageSize: 20 }),
      create: useMeetingFullMutations().create,
    }), { wrapper });

    await waitFor(() => expect(result.current.meetings.data?.totalCount).toBe(1));
    let mutationResolved = false;
    const mutationPromise = result.current.create.mutateAsync(meetingInput).then((meeting) => {
      mutationResolved = true;
      return meeting;
    });

    await waitFor(() => expect(list).toHaveBeenCalledTimes(2));
    expect(mutationResolved).toBe(false);
    expect(queryClient.getQueryState(meetingKeys.list({ page: 1, pageSize: 20 }))?.isInvalidated).toBe(true);

    await act(async () => {
      if (!resolveRefetch) throw new Error("Expected the invalidated list query to start refetching");
      resolveRefetch(listResult([createdMeeting, firstMeeting]));
      await mutationPromise;
    });

    expect(mutationResolved).toBe(true);
    await waitFor(() => expect(result.current.meetings.data?.items.map((meeting) => meeting.id)).toEqual(["meeting-2", "meeting-1"]));
    unmount();
    queryClient.clear();
  });
});
