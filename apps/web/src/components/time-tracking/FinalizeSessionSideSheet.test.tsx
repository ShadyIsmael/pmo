import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { TimerSessionDto } from "@/hooks/time-tracking/useTimer";
import { FinalizeSessionSideSheet } from "@/components/time-tracking/FinalizeSessionSideSheet";

const { mockGet, mockPost } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
}));

vi.mock("@/lib/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/client")>();
  return { ...actual, api: { ...actual.api, get: mockGet, post: mockPost } };
});

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return { ...actual, api: { ...actual.api, get: mockGet, post: mockPost } };
});

vi.mock("next-intl", async () => {
  const { default: en } = await import("@/messages/en.json");
  const lookup = (fullKey: string): string => {
    const value = fullKey.split(".").reduce<unknown>(
      (acc, part) => (acc && typeof acc === "object" ? (acc as Record<string, unknown>)[part] : undefined),
      en
    );
    return typeof value === "string" ? value : fullKey;
  };
  return {
    useTranslations: (namespace?: string) => (key: string) => (namespace ? lookup(`${namespace}.${key}`) : lookup(key)),
    useLocale: () => "en",
  };
});

function seg(id: string, type: "Work" | "Break" | "Pause" | "Idle", startedAt: string, endedAt: string) {
  return { id, segmentType: type, startedAt, endedAt, durationSeconds: 0 };
}

const awaitingSession: TimerSessionDto = {
  id: "t1",
  resourceId: "r1",
  status: "AwaitingFinalization",
  startedAt: "2026-09-22T09:00:00.000Z",
  endedAt: "2026-09-22T11:02:05.000Z",
  serverNow: "2026-09-22T11:02:05.000Z",
  timezone: "UTC",
  metrics: { workSeconds: 5400, breakSeconds: 1080, pauseSeconds: 340, idleSeconds: 260, breakCount: 2, workSessionCount: 2, longestWorkSeconds: 3600, averageWorkSeconds: 2700 },
  segments: [
    seg("w1", "Work", "2026-09-22T09:00:00.000Z", "2026-09-22T10:00:00.000Z"),
    seg("b1", "Break", "2026-09-22T10:00:00.000Z", "2026-09-22T10:10:00.000Z"),
    seg("w2", "Work", "2026-09-22T10:10:00.000Z", "2026-09-22T10:40:00.000Z"),
    seg("p1", "Pause", "2026-09-22T10:40:00.000Z", "2026-09-22T10:44:00.000Z"),
    seg("b2", "Break", "2026-09-22T10:44:00.000Z", "2026-09-22T10:52:00.000Z"),
    seg("i1", "Idle", "2026-09-22T10:52:00.000Z", "2026-09-22T10:54:00.000Z"),
    seg("p2", "Pause", "2026-09-22T10:54:00.000Z", "2026-09-22T10:55:40.000Z"),
    seg("i2", "Idle", "2026-09-22T10:55:40.000Z", "2026-09-22T10:57:40.000Z"),
    seg("i3", "Idle", "2026-09-22T10:57:40.000Z", "2026-09-22T10:58:00.000Z"),
  ],
};

const mockGetFn = mockGet as unknown as Mock;
const mockPostFn = mockPost as unknown as Mock;
const onOpenChange = vi.fn();

function renderSheet(open: boolean, session: TimerSessionDto | null = awaitingSession) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } });
  return render(
    <QueryClientProvider client={client}>
      <FinalizeSessionSideSheet open={open} onOpenChange={onOpenChange} session={session} />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  onOpenChange.mockClear();
  mockGetFn.mockReset();
  mockPostFn.mockReset();
  mockGetFn.mockImplementation((path: string) => {
    if (path.includes("eligible-projects")) return Promise.resolve([{ id: "p1", code: "PRJ", name: "Alpha" }]);
    if (path.includes("/people")) return Promise.resolve([]);
    if (path.includes("/activities")) return Promise.resolve([]);
    if (path.includes("/plans")) return Promise.resolve([]);
    if (path.includes("/work-items")) return Promise.resolve({ items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 1 });
    if (path === "/api/project-roles") return Promise.resolve([{ id: "role1", code: "DEV", nameEn: "Developer", nameAr: "مطوّر", displayOrder: 1, isActive: true }]);
    return Promise.resolve(null);
  });
  mockPostFn.mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
});

describe("Finalization SideSheet (tests 18-21, 24-25)", () => {
  it("18. SideSheet shows session summary with totals and derived counts", async () => {
    renderSheet(true);
    const summary = await screen.findByTestId("session-summary");
    expect(summary).toBeInTheDocument();
    expect(screen.getByTestId("summary-work")).toHaveTextContent("01:30:00");
    expect(screen.getByTestId("summary-break")).toHaveTextContent("00:18:00");
    expect(screen.getByTestId("summary-pause")).toHaveTextContent("00:05:40");
    expect(screen.getByTestId("summary-idle")).toHaveTextContent("00:04:20");
    expect(screen.getByTestId("summary-break-count")).toHaveTextContent("2");
    expect(screen.getByTestId("summary-pause-count")).toHaveTextContent("2");
    expect(screen.getByTestId("summary-idle-count")).toHaveTextContent("3");
    expect(screen.getByTestId("summary-total")).toHaveTextContent("01:58:00");
  });

  it("19. Every Break event is rendered with Start, End, and Duration", async () => {
    const user = userEvent.setup();
    renderSheet(true);
    await user.click(await screen.findByTestId("break-events-trigger"));
    const section = await screen.findByTestId("break-events");
    expect(section).toHaveTextContent("Break #1");
    expect(section).toHaveTextContent("Break #2");
    expect(section).toHaveTextContent("Start");
    expect(section).toHaveTextContent("End");
    expect(section).toHaveTextContent("Duration");
    expect(section).toHaveTextContent("00:10:00");
    expect(section).toHaveTextContent("00:08:00");
  });

  it("20. Every manual Pause event is rendered", async () => {
    const user = userEvent.setup();
    renderSheet(true);
    await user.click(await screen.findByTestId("pause-events-trigger"));
    const section = await screen.findByTestId("pause-events");
    expect(section).toHaveTextContent("Pause #1");
    expect(section).toHaveTextContent("Pause #2");
    expect(section).toHaveTextContent("00:04:00");
    expect(section).toHaveTextContent("00:01:40");
  });

  it("21. Every automatic Idle event is rendered separately from manual Pause", async () => {
    const user = userEvent.setup();
    renderSheet(true);
    await user.click(await screen.findByTestId("idle-events-trigger"));
    const section = await screen.findByTestId("idle-events");
    expect(section).toHaveTextContent("Idle #1");
    expect(section).toHaveTextContent("Idle #2");
    expect(section).toHaveTextContent("Idle #3");
    expect(section).toHaveTextContent("00:02:00");
    expect(section).toHaveTextContent("00:00:20");
    expect(screen.getByTestId("summary-idle-count")).toBeInTheDocument();
    expect(screen.getByTestId("summary-pause-count")).toHaveTextContent("2");
  });

  it("24. Finalize closes the SideSheet on success", async () => {
    const user = userEvent.setup();
    mockPostFn.mockResolvedValue([]);
    renderSheet(true);
    const projectSelect = await screen.findByTestId("finalize-project-select");
    await waitFor(() => {
      expect(projectSelect.querySelector('option[value="p1"]')).not.toBeNull();
    });
    await user.selectOptions(projectSelect, "p1");
    await user.click(screen.getByTestId("finalize-submit"));
    await waitFor(() => {
      expect(mockPostFn).toHaveBeenCalledWith("/api/timer/t1/finalize", expect.objectContaining({ projectId: "p1" }));
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  it("25. Discard closes the SideSheet after a confirmed cancel", async () => {
    const user = userEvent.setup();
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    mockPostFn.mockResolvedValue({ ...awaitingSession, status: "Cancelled" });
    renderSheet(true);
    await user.click(await screen.findByTestId("finalize-discard"));
    await waitFor(() => {
      expect(confirmSpy).toHaveBeenCalled();
      expect(mockPostFn).toHaveBeenCalledWith("/api/timer/t1/cancel");
      expect(onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  it("shows the empty eligible-projects state and disables Finalize", async () => {
    mockGetFn.mockImplementation((path: string) => {
      if (path.includes("eligible-projects")) return Promise.resolve([]);
      if (path.includes("/people")) return Promise.resolve([]);
      if (path.includes("/activities")) return Promise.resolve([]);
      if (path.includes("/plans")) return Promise.resolve([]);
      if (path.includes("/work-items")) return Promise.resolve({ items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 1 });
      if (path === "/api/project-roles") return Promise.resolve([]);
      return Promise.resolve(null);
    });
    renderSheet(true);
    expect(await screen.findByText("No active project assignments are available for this work date.")).toBeInTheDocument();
    expect(screen.getByTestId("finalize-submit")).toBeDisabled();
  });

  it("auto-selects the only eligible project and hides Optional Details by default", async () => {
    renderSheet(true);
    const projectSelect = await screen.findByTestId("finalize-project-select");
    await waitFor(() => {
      expect((projectSelect as HTMLSelectElement).value).toBe("p1");
    });
    expect(screen.queryByTestId("finalize-plan-select")).not.toBeInTheDocument();
    expect(screen.queryByTestId("finalize-activity-select")).not.toBeInTheDocument();
    expect(screen.queryByTestId("finalize-work-item-select")).not.toBeInTheDocument();
    expect(screen.queryByTestId("finalize-role-select")).not.toBeInTheDocument();
    expect(screen.queryByTestId("finalize-description")).not.toBeInTheDocument();
    expect(screen.getByTestId("optional-details-trigger")).toBeInTheDocument();
  });

  it("opens Optional Details and shows scoped fields after project selection", async () => {
    const user = userEvent.setup();
    mockGetFn.mockImplementation((path: string) => {
      if (path.includes("eligible-projects")) return Promise.resolve([{ id: "p1", code: "PRJ", name: "Alpha" }]);
      if (path.includes("/people")) return Promise.resolve([{ id: "person1", projectId: "p1", resourceId: "r1", name: "Admin", resourceType: "Person", relationshipType: "TeamMember", status: "Active", hasSystemUser: true, source: "Manual", isLegacy: false, functionalRoles: [{ id: "role1", code: "DEV", nameEn: "Developer", nameAr: "مطوّر", isPrimary: true }], rowVersion: "1", createdAt: "2026-01-01T00:00:00Z" }]);
      if (path.includes("/activities")) return Promise.resolve([{ id: "act1", code: "A1", name: "Activity" }]);
      if (path.includes("/plans")) return Promise.resolve([{ id: "plan1", name: "Plan A" }]);
      if (path.includes("/work-items")) return Promise.resolve({ items: [{ id: "wi1", title: "Task 1" }], page: 1, pageSize: 200, totalCount: 1, totalPages: 1 });
      return Promise.resolve(null);
    });
    renderSheet(true);
    const projectSelect = await screen.findByTestId("finalize-project-select");
    await waitFor(() => {
      expect((projectSelect as HTMLSelectElement).value).toBe("p1");
    });
    await user.click(await screen.findByTestId("optional-details-trigger"));
    const optional = await screen.findByTestId("optional-details");
    expect(optional).toBeInTheDocument();
    expect(screen.queryByTestId("finalize-plan-select")).not.toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByTestId("finalize-activity-select")).toBeInTheDocument();
      expect((screen.getByTestId("finalize-activity-select") as HTMLSelectElement).value).toBe("act1");
      expect(screen.getByTestId("finalize-work-item-select")).toBeInTheDocument();
      expect((screen.getByTestId("finalize-work-item-select") as HTMLSelectElement).value).toBe("wi1");
      expect(screen.getByTestId("finalize-role-select")).toBeInTheDocument();
      expect((screen.getByTestId("finalize-role-select") as HTMLSelectElement).value).toBe("role1");
    });
    expect(screen.getByTestId("finalize-description")).toBeInTheDocument();
  });

  it("shows Plan selector only when multiple plans exist", async () => {
    mockGetFn.mockImplementation((path: string) => {
      if (path.includes("eligible-projects")) return Promise.resolve([{ id: "p1", code: "PRJ", name: "Alpha" }]);
      if (path.includes("/people")) return Promise.resolve([]);
      if (path.includes("/activities")) return Promise.resolve([]);
      if (path.includes("/plans")) return Promise.resolve([{ id: "plan1", name: "Plan A" }, { id: "plan2", name: "Plan B" }]);
      if (path.includes("/work-items")) return Promise.resolve({ items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 1 });
      return Promise.resolve(null);
    });
    renderSheet(true);
    const projectSelect = await screen.findByTestId("finalize-project-select");
    await waitFor(() => {
      expect((projectSelect as HTMLSelectElement).value).toBe("p1");
    });
    await waitFor(() => {
      expect(screen.getByTestId("finalize-plan-select")).toBeInTheDocument();
      expect((screen.getByTestId("finalize-plan-select") as HTMLSelectElement).value).toBe("");
    });
  });
});
