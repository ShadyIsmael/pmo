import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { TimerSessionDto } from "@/hooks/time-tracking/useTimer";
import { api } from "@/lib/api";
import { LiveTimerShell } from "@/components/time-tracking/LiveTimerShell";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return { ...actual, api: { ...actual.api, get: vi.fn(), post: vi.fn() } };
});

vi.mock("@/lib/auth", () => ({
  useAuth: () => ({
    status: "authenticated",
    user: { permissions: ["TimeTracking.ManageOwn"] },
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
  }),
}));

vi.mock("@/lib/perspective-context", () => ({
  useManagementPerspective: () => ({ effectiveReadOnly: false }),
}));

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

function seg(id: string, type: "Work" | "Break" | "Pause" | "Idle", startedAt: Date, endedAt?: Date) {
  return { id, segmentType: type, startedAt: startedAt.toISOString(), endedAt: endedAt ? endedAt.toISOString() : null, durationSeconds: 0 };
}

function baseMetrics() {
  return { workSeconds: 0, breakSeconds: 0, pauseSeconds: 0, idleSeconds: 0, breakCount: 0, workSessionCount: 0, longestWorkSeconds: 0, averageWorkSeconds: 0 };
}

function buildRunningSession(): TimerSessionDto {
  const serverNow = new Date();
  const activeStart = new Date(serverNow.getTime() - 10_000);
  const closedStart = new Date(serverNow.getTime() - 3_610_000);
  const closedEnd = new Date(serverNow.getTime() - 10_000);
  return {
    id: "live-1",
    resourceId: "r1",
    status: "Running",
    startedAt: closedStart.toISOString(),
    endedAt: null,
    serverNow: serverNow.toISOString(),
    timezone: "UTC",
    metrics: { ...baseMetrics(), workSeconds: 3610 },
    segments: [seg("w-closed", "Work", closedStart, closedEnd), seg("w-active", "Work", activeStart)],
  };
}

function buildOnBreakSession(): TimerSessionDto {
  const serverNow = new Date();
  const workEnd = new Date(serverNow.getTime() - 60_000);
  const workStart = new Date(serverNow.getTime() - 3_660_000);
  const breakStart = workEnd;
  return {
    id: "live-2",
    resourceId: "r1",
    status: "OnBreak",
    startedAt: workStart.toISOString(),
    endedAt: null,
    serverNow: serverNow.toISOString(),
    timezone: "UTC",
    metrics: { ...baseMetrics(), workSeconds: 3600, breakSeconds: 60 },
    segments: [seg("w1", "Work", workStart, workEnd), seg("b-active", "Break", breakStart)],
  };
}

function buildPausedSession(): TimerSessionDto {
  const session = buildOnBreakSession();
  session.id = "live-3";
  session.status = "Paused";
  session.segments = [
    session.segments[0],
    seg("b1", "Break", new Date(Date.now() - 3_600_000), new Date(Date.now() - 3_540_000)),
    seg("p-active", "Pause", new Date(Date.now() - 10_000)),
  ];
  return session;
}

function buildIdleSession(): TimerSessionDto {
  const session = buildOnBreakSession();
  session.id = "live-4";
  session.status = "IdlePaused";
  session.segments = [
    session.segments[0],
    seg("p1", "Pause", new Date(Date.now() - 3_600_000), new Date(Date.now() - 3_560_000)),
    seg("i-active", "Idle", new Date(Date.now() - 10_000)),
  ];
  return session;
}

function buildAwaitingSession(): TimerSessionDto {
  const ended = new Date();
  const started = new Date(ended.getTime() - 7_325_000);
  const mid = new Date(started.getTime() + 3_600_000);
  return {
    id: "await-1",
    resourceId: "r1",
    status: "AwaitingFinalization",
    startedAt: started.toISOString(),
    endedAt: ended.toISOString(),
    serverNow: ended.toISOString(),
    timezone: "UTC",
    metrics: { ...baseMetrics(), workSeconds: 3600 },
    segments: [seg("w1", "Work", started, mid), seg("w2", "Work", new Date(mid.getTime() + 60_000), ended)],
  };
}

const mockGet = api.get as Mock;
const mockPost = api.post as Mock;

let currentSession: TimerSessionDto | null;
let eligibleCalls: number;
let canceled: boolean;
let resumeCalls: string[];

function installApi(initial: TimerSessionDto | null) {
  currentSession = initial;
  eligibleCalls = 0;
  canceled = false;
  resumeCalls = [];
  mockGet.mockImplementation((path: string) => {
    if (path.includes("eligible-projects")) {
      eligibleCalls += 1;
      return Promise.resolve([{ id: "p1", code: "PRJ", name: "Alpha" }]);
    }
    if (path === "/api/timer/current") return Promise.resolve(currentSession);
    if (path === "/api/project-roles") return Promise.resolve([]);
    if (path.includes("/plans")) return Promise.resolve([]);
    if (path === "/api/work-items") return Promise.resolve({ items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 1 });
    return Promise.resolve(null);
  });
  mockPost.mockImplementation((path: string) => {
    if (path.endsWith("/finish")) {
      currentSession = { ...currentSession!, status: "AwaitingFinalization", endedAt: new Date().toISOString(), serverNow: new Date().toISOString() };
      return Promise.resolve(currentSession);
    }
    if (path.endsWith("/cancel")) {
      canceled = true;
      const cancelled = { ...currentSession!, status: "Cancelled" as const };
      currentSession = null;
      return Promise.resolve(cancelled);
    }
    if (path.endsWith("/finalize")) return Promise.resolve([]);
    if (path.endsWith("/resume")) {
      resumeCalls.push(path);
      if (currentSession) {
        const now = new Date();
        currentSession = {
          ...currentSession,
          status: "Running",
          serverNow: now.toISOString(),
          segments: currentSession.segments.map((segment) =>
            segment.endedAt == null && (segment.segmentType === "Pause" || segment.segmentType === "Break" || segment.segmentType === "Idle" || segment.segmentType === "Work")
              ? segment.segmentType === "Work"
                ? segment
                : { ...segment, endedAt: now.toISOString() }
              : segment
          ).flatMap((segment) => segment),
        };
        const hasOpenWork = currentSession.segments.some((segment) => segment.segmentType === "Work" && segment.endedAt == null);
        if (!hasOpenWork) {
          currentSession = {
            ...currentSession,
            segments: [...currentSession.segments, seg("w-new", "Work", now)],
          };
        }
      }
      return Promise.resolve(currentSession);
    }
    return Promise.resolve(currentSession);
  });
}

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } });
}

function renderShell(client = newClient()) {
  const view = render(
    <QueryClientProvider client={client}>
      <LiveTimerShell />
    </QueryClientProvider>
  );
  return { client, ...view };
}

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

beforeEach(() => {
  installApi(buildRunningSession());
});

afterEach(() => {
  cleanup();
});

describe("floating card counters (tests 9-15)", () => {
  it("9. Work counter advances while Working and freezes during Break", async () => {
    installApi(buildRunningSession());
    const { unmount } = renderShell();
    const workBefore = (await screen.findByTestId("timer-work-counter")).textContent;
    await sleep(1300);
    const workAfter = screen.getByTestId("timer-work-counter").textContent;
    expect(workAfter).not.toBe(workBefore);
    unmount();

    installApi(buildOnBreakSession());
    renderShell();
    const frozenBefore = (await screen.findByTestId("timer-work-counter")).textContent;
    await sleep(1300);
    expect(screen.getByTestId("timer-work-counter").textContent).toBe(frozenBefore);
  });

  it("10. Break counter advances only during Break", async () => {
    installApi(buildOnBreakSession());
    renderShell();
    const breakBefore = (await screen.findByTestId("timer-break-counter")).textContent;
    await sleep(1300);
    expect(screen.getByTestId("timer-break-counter").textContent).not.toBe(breakBefore);
  });

  it("11. Pause counter advances only during manual Pause", async () => {
    installApi(buildPausedSession());
    renderShell();
    const pauseBefore = (await screen.findByTestId("timer-pause-counter")).textContent;
    await sleep(1300);
    expect(screen.getByTestId("timer-pause-counter").textContent).not.toBe(pauseBefore);
  });

  it("Pause → Resume button visible → click → resume mutation called → Running UI shown", async () => {
    const user = userEvent.setup();
    installApi(buildPausedSession());
    renderShell();

    expect(await screen.findByTestId("timer-status")).toHaveTextContent("Paused");
    const resumeButton = await screen.findByTestId("timer-resume-button");
    expect(resumeButton).toBeEnabled();
    expect(screen.queryByTestId("timer-pause-button")).not.toBeInTheDocument();
    expect(screen.queryByTestId("timer-break-button")).not.toBeInTheDocument();

    const workFrozen = (await screen.findByTestId("timer-work-counter")).textContent;
    await sleep(1300);
    expect(screen.getByTestId("timer-work-counter").textContent).toBe(workFrozen);

    await user.click(resumeButton);

    await waitFor(() => expect(resumeCalls).toContainEqual(expect.stringMatching(/\/api\/timer\/live-3\/resume$/)));
    expect(await screen.findByTestId("timer-status")).toHaveTextContent("Working");
    await waitFor(() => {
      expect(screen.queryByTestId("timer-resume-button")).not.toBeInTheDocument();
    });
    expect(screen.getByTestId("timer-pause-button")).toBeInTheDocument();
    expect(screen.getByTestId("timer-break-button")).toBeInTheDocument();

    const pauseAtResume = (await screen.findByTestId("timer-pause-counter")).textContent;
    await sleep(1300);
    expect(screen.getByTestId("timer-pause-counter").textContent).toBe(pauseAtResume);
  });

  it("12. Idle does not inflate the Pause counter", async () => {
    installApi(buildIdleSession());
    renderShell();
    await screen.findByTestId("timer-pause-counter");
    const pauseBefore = screen.getByTestId("timer-pause-counter").textContent;
    await sleep(1300);
    expect(screen.getByTestId("timer-pause-counter").textContent).toBe(pauseBefore);
  });

  it("13. Resumed Work continues accumulating from closed + active segments", async () => {
    const session = buildRunningSession();
    installApi(session);
    renderShell();
    const work = await screen.findByTestId("timer-work-counter");
    expect(work).toHaveTextContent("01:00:10");
  });

  it("14. Navigation (remount with same cache) does not reset counters", async () => {
    const session = buildRunningSession();
    installApi(session);
    const client = newClient();
    const first = renderShell(client);
    const before = (await screen.findByTestId("timer-work-counter")).textContent;
    first.unmount();
    renderShell(client);
    const after = (await screen.findByTestId("timer-work-counter")).textContent;
    expect(after).toBe(before);
  });

  it("15. Refresh (fresh client) reconstructs counters from server session", async () => {
    installApi(buildRunningSession());
    renderShell();
    const before = (await screen.findByTestId("timer-work-counter")).textContent;
    cleanup();
    installApi(buildRunningSession());
    renderShell();
    const after = (await screen.findByTestId("timer-work-counter")).textContent;
    expect(after).toBe(before);
  });
});

describe("Finish → SideSheet lifecycle (tests 16-17, 22-23, 26, network)", () => {
  it("16. Finish opens the SideSheet automatically", async () => {
    const user = userEvent.setup();
    installApi(buildRunningSession());
    renderShell();
    await screen.findByTestId("timer-finish-button");
    expect(screen.queryByTestId("finalize-side-sheet")).not.toBeInTheDocument();
    await user.click(screen.getByTestId("timer-finish-button"));
    expect(await screen.findByText("Complete Time Session")).toBeInTheDocument();
  });

  it("17. SideSheet opens only via Complete while already AwaitingFinalization", async () => {
    installApi(buildAwaitingSession());
    renderShell();
    await screen.findByTestId("timer-complete-button");
    expect(screen.queryByTestId("finalize-side-sheet")).not.toBeInTheDocument();
    expect(screen.queryByText("Complete Time Session")).not.toBeInTheDocument();
  });

  it("22. Closing the SideSheet does not cancel the session", async () => {
    const user = userEvent.setup();
    installApi(buildAwaitingSession());
    renderShell();
    await user.click(await screen.findByTestId("timer-complete-button"));
    await screen.findByText("Complete Time Session");
    await user.click(screen.getByRole("button", { name: "Close" }));
    await waitFor(() => expect(screen.queryByText("Complete Time Session")).not.toBeInTheDocument());
    expect(canceled).toBe(false);
    expect(mockPost).not.toHaveBeenCalledWith(expect.stringContaining("/cancel"));
    expect(await screen.findByTestId("timer-complete-button")).toBeInTheDocument();
  });

  it("23. Needs Completion action reopens the SideSheet", async () => {
    const user = userEvent.setup();
    installApi(buildAwaitingSession());
    renderShell();
    await user.click(await screen.findByTestId("timer-complete-button"));
    await screen.findByText("Complete Time Session");
    await user.click(screen.getByRole("button", { name: "Close" }));
    await waitFor(() => expect(screen.queryByText("Complete Time Session")).not.toBeInTheDocument());
    await user.click(screen.getByTestId("timer-complete-button"));
    expect(await screen.findByText("Complete Time Session")).toBeInTheDocument();
  });

  it("26. There is exactly one finalization form instance", async () => {
    installApi(buildAwaitingSession());
    const { container } = renderShell();
    await screen.findByTestId("timer-complete-button");
    const card = screen.getByTestId("floating-timer-card");
    expect(card.querySelector("select")).toBeNull();
    expect(document.querySelectorAll('[data-testid="finalize-project-select"]')).toHaveLength(0);
    await userEvent.setup().click(screen.getByTestId("timer-complete-button"));
    await screen.findByText("Complete Time Session");
    expect(document.querySelectorAll('[data-testid="finalize-project-select"]')).toHaveLength(1);
    expect(card.querySelector("select")).toBeNull();
    expect(container).toBeInTheDocument();
  });

  it("network: zero eligible-projects requests before Finish; requested after Finish", async () => {
    const user = userEvent.setup();
    installApi(buildRunningSession());
    renderShell();
    await screen.findByTestId("timer-work-counter");
    await sleep(200);
    expect(eligibleCalls).toBe(0);
    await user.click(screen.getByTestId("timer-finish-button"));
    await screen.findByText("Complete Time Session");
    await waitFor(() => expect(eligibleCalls).toBeGreaterThan(0));
  });
});
