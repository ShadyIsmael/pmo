import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ useQuery: vi.fn((options: unknown) => options) }));

vi.mock("@tanstack/react-query", () => ({
  useQuery: mocks.useQuery,
  useMutation: vi.fn(),
  useQueryClient: vi.fn(),
}));

import { useNotifications, useUnreadNotificationCount } from "../api/notifications";

describe("notification query refresh policy", () => {
  it("refreshes the open center every thirty seconds and when the window regains focus", () => {
    mocks.useQuery.mockClear();
    useNotifications({ page: 1, pageSize: 20 }, true, true);

    expect(mocks.useQuery).toHaveBeenCalledWith(expect.objectContaining({ refetchInterval: 30_000, refetchOnWindowFocus: true }));
  });

  it("uses a lightweight unread-count query without periodic polling", () => {
    mocks.useQuery.mockClear();
    useUnreadNotificationCount(true);

    expect(mocks.useQuery).toHaveBeenCalledWith(expect.objectContaining({ refetchOnWindowFocus: true, refetchOnReconnect: true, enabled: true }));
    expect(mocks.useQuery.mock.calls[0][0]).not.toHaveProperty("refetchInterval");
  });
});
