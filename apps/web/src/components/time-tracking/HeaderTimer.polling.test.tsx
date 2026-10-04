import * as React from "react";
import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { timerKeys, type TimerSessionDto } from "@/hooks/time-tracking/useTimer";
import { api } from "@/lib/api";
import { HeaderTimer } from "@/components/time-tracking/HeaderTimer";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return { ...actual, api: { ...actual.api, get: vi.fn(), post: vi.fn() } };
});

vi.mock("@/lib/auth", () => ({
  useAuth: () => ({ status: "authenticated", user: { permissions: ["TimeTracking.ManageOwn"] }, isAuthenticated: true }),
}));

vi.mock("@/lib/perspective-context", () => ({ useManagementPerspective: () => ({ effectiveReadOnly: false }) }));

vi.mock("@/components/time-tracking/FinalizeSessionSideSheet", () => ({
  FinalizeSessionSideSheet: ({ open }: { open: boolean }) => open ? <div data-testid="header-finalize-side-sheet">Finalization</div> : null,
}));

vi.mock("next-intl", async () => {
  const { default: en } = await import("@/messages/en.json");
  const lookup = (fullKey: string): string => {
    const value = fullKey.split(".").reduce<unknown>((acc, part) => (acc && typeof acc === "object" ? (acc as Record<string, unknown>)[part] : undefined), en);
    return typeof value === "string" ? value : fullKey;
  };
  return { useTranslations: (namespace?: string) => (key: string) => lookup(namespace ? `${namespace}.${key}` : key), useLocale: () => "en" };
});

const CURRENT_PATH = "/api/timer/current";
const mockGet = api.get as Mock;
const mockPost = api.post as Mock;

let currentSession: TimerSessionDto | null;
let currentCalls: number;

function seg(id: string, type: "Work" | "Break" | "Pause" | "Idle", startedAt: Date, endedAt?: Date) {
  return { id, segmentType: type, startedAt: startedAt.toISOString(), endedAt: endedAt?.toISOString() ?? null, durationSeconds: 0 };
}

function buildSession(status: TimerSessionDto["status"]): TimerSessionDto {
  const serverNow = new Date();
  const sessionStart = new Date(serverNow.getTime() - 3_610_000);
  const openStart = new Date(serverNow.getTime() - 10_000);
  const ended = status === "AwaitingFinalization" ? serverNow : undefined;
  const segments =
    status === "AwaitingFinalization"
      ? [seg("w1", "Work", sessionStart, serverNow)]
      : status === "OnBreak"
        ? [seg("w1", "Work", sessionStart, openStart), seg("b-open", "Break", openStart)]
        : status === "Paused"
          ? [seg("w1", "Work", sessionStart, openStart), seg("p-open", "Pause", openStart)]
          : status === "IdlePaused"
            ? [seg("w1", "Work", sessionStart, openStart), seg("i-open", "Idle", openStart)]
            : [seg("w-closed", "Work", sessionStart, openStart), seg("w-open", "Work", openStart)];
  return {
    id: `poll-${status}`,
    resourceId: "resource-1",
    status,
    startedAt: sessionStart.toISOString(),
    endedAt: ended?.toISOString() ?? null,
    serverNow: serverNow.toISOString(),
    timezone: "UTC",
    metrics: { workSeconds: 3600, breakSeconds: 0, pauseSeconds: 0, idleSeconds: 0, breakCount: 0, workSessionCount: 1, longestWorkSeconds: 3600, averageWorkSeconds: 3600 },
    segments,
  };
}

function installApi(initial: TimerSessionDto | null) {
  currentSession = initial;
  currentCalls = 0;
  mockGet.mockImplementation((requestPath: string) => {
    if (requestPath === CURRENT_PATH) {
      currentCalls += 1;
      return Promise.resolve(currentSession);
    }
    if (requestPath.includes("eligible-projects")) return Promise.resolve([]);
    return Promise.resolve(null);
  });
  mockPost.mockImplementation((requestPath: string) => {
    if (requestPath === "/api/timer/start") {
      currentSession = buildSession("Running");
      return Promise.resolve(currentSession);
    }
    if (requestPath.endsWith("/pause") && currentSession) currentSession = { ...currentSession, status: "Paused" };
    if (requestPath.endsWith("/resume") && currentSession) currentSession = { ...currentSession, status: "Running" };
    if (requestPath.endsWith("/break") && currentSession) currentSession = { ...currentSession, status: "OnBreak" };
    if (requestPath.endsWith("/finish") && currentSession) currentSession = { ...currentSession, status: "AwaitingFinalization" };
    return Promise.resolve(currentSession);
  });
}

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { staleTime: 5_000, retry: false } } });
}

function renderTimer(client = newClient()) {
  const view = render(
    <QueryClientProvider client={client}>
      <HeaderTimer />
    </QueryClientProvider>
  );
  return { client, ...view };
}

function useFakeClock() {
  vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout", "setInterval", "clearInterval", "Date"] });
}

async function tick(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

beforeEach(() => {
  installApi(null);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("current timer polling with no active timer", () => {
  it("performs one initial request and issues zero additional requests while idle", async () => {
    useFakeClock();
    installApi(null);
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);
    expect(screen.getByRole("button", { name: /track time/i })).toBeInTheDocument();

    await tick(60_000);
    expect(currentCalls).toBe(1);
    await tick(300_000);
    expect(currentCalls).toBe(1);
    expect(screen.getByRole("button", { name: /track time/i })).toBeInTheDocument();
  });

  it("keeps the cached no-active-timer state instead of re-requesting", async () => {
    useFakeClock();
    installApi(null);
    const { client } = renderTimer();
    await tick(0);
    expect(client.getQueryData(timerKeys.current)).toBeNull();
    await tick(120_000);
    expect(client.getQueryData(timerKeys.current)).toBeNull();
    expect(currentCalls).toBe(1);
  });
});

describe("active timer renders locally without per-interval requests", () => {
  it("Running: elapsed work advances locally and syncs at most once per 60s", async () => {
    useFakeClock();
    installApi(buildSession("Running"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);

    const trigger = screen.getByTestId("header-timer-trigger");
    expect(trigger).toHaveTextContent("01:00:10");
    await tick(1_500);
    expect(trigger).toHaveTextContent("01:00:11");
    expect(currentCalls).toBe(1);

    await tick(30_000);
    expect(currentCalls).toBe(1);
    await tick(30_000);
    expect(currentCalls).toBeLessThanOrEqual(2);
  });

  it("OnBreak: work time stays frozen and the break counter advances without GETs", async () => {
    useFakeClock();
    installApi(buildSession("OnBreak"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);

    const trigger = screen.getByTestId("header-timer-trigger");
    expect(trigger).toHaveAttribute("aria-label", "On break");
    expect(trigger).toHaveTextContent("01:00:00");

    fireEvent.click(trigger);
    await tick(0);
    const breakCounter = screen.getByTestId("header-timer-break-counter");
    expect(breakCounter).toHaveTextContent("00:00:10");
    await tick(3_000);
    expect(breakCounter).toHaveTextContent("00:00:13");
    expect(trigger).toHaveTextContent("01:00:00");
    expect(currentCalls).toBe(1);
    await tick(27_000);
    expect(currentCalls).toBe(1);
    await tick(30_000);
    expect(currentCalls).toBe(2);
  });

  it("Paused: work time does not drift while paused", async () => {
    useFakeClock();
    installApi(buildSession("Paused"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);

    const trigger = screen.getByTestId("header-timer-trigger");
    expect(trigger).toHaveAttribute("aria-label", "Paused");
    expect(trigger).toHaveTextContent("01:00:00");
    await tick(5_000);
    expect(trigger).toHaveTextContent("01:00:00");
    expect(currentCalls).toBe(1);
  });

  it("IdlePaused: exposes the idle state without periodic requests", async () => {
    useFakeClock();
    installApi(buildSession("IdlePaused"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);
    expect(screen.getByTestId("header-timer-trigger")).toHaveAttribute("aria-label", "Idle paused");
    await tick(45_000);
    expect(currentCalls).toBe(1);
    expect(screen.getByTestId("header-timer-trigger")).toHaveAttribute("aria-label", "Idle paused");
  });

  it("AwaitingFinalization: shows Needs completion and never polls", async () => {
    useFakeClock();
    installApi(buildSession("AwaitingFinalization"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);
    expect(screen.getByTestId("header-timer-trigger")).toHaveAttribute("aria-label", "Needs completion");
    await tick(60_000);
    expect(currentCalls).toBe(1);
    expect(screen.getByTestId("header-timer-trigger")).toHaveAttribute("aria-label", "Needs completion");
  });
});

describe("mutation driven synchronization", () => {
  it("Start writes the current-timer cache without refetching it", async () => {
    installApi(null);
    const { client } = renderTimer();
    await waitFor(() => expect(currentCalls).toBe(1));
    expect(client.getQueryData(timerKeys.current)).toBeNull();

    const user = userEvent.setup();
    await user.click(screen.getByTestId("header-timer-trigger"));
    await user.click(await screen.findByTestId("header-timer-start-button"));

    await waitFor(() => expect(client.getQueryData(timerKeys.current)).toMatchObject({ status: "Running" }));
    expect(currentCalls).toBe(1);
  });

  it("Pause, Resume and Break update the cache from the mutation response", async () => {
    installApi(buildSession("Running"));
    const { client } = renderTimer();
    await waitFor(() => expect(currentCalls).toBe(1));

    const user = userEvent.setup();
    await user.click(screen.getByTestId("header-timer-trigger"));

    await user.click(await screen.findByTestId("header-timer-pause-button"));
    await waitFor(() => expect(client.getQueryData(timerKeys.current)).toMatchObject({ status: "Paused" }));
    expect(currentCalls).toBe(1);

    await user.click(await screen.findByTestId("header-timer-resume-button"));
    await waitFor(() => expect(client.getQueryData(timerKeys.current)).toMatchObject({ status: "Running" }));
    expect(currentCalls).toBe(1);

    await user.click(await screen.findByTestId("header-timer-break-button"));
    await waitFor(() => expect(client.getQueryData(timerKeys.current)).toMatchObject({ status: "OnBreak" }));
    expect(currentCalls).toBe(1);
  });

  it("Finish moves the cache to AwaitingFinalization and shows Needs completion", async () => {
    installApi(buildSession("Running"));
    const { client } = renderTimer();
    await waitFor(() => expect(currentCalls).toBe(1));

    const user = userEvent.setup();
    await user.click(screen.getByTestId("header-timer-trigger"));
    await user.click(await screen.findByTestId("header-timer-finish-button"));

    await waitFor(() => expect(client.getQueryData(timerKeys.current)).toMatchObject({ status: "AwaitingFinalization" }));
    expect(currentCalls).toBe(1);
    await waitFor(() => expect(screen.getByTestId("header-timer-trigger")).toHaveAttribute("aria-label", "Needs completion"));
  });
});

describe("focus, reconnect and route navigation", () => {
  it("performs at most one synchronization when the tab regains focus", async () => {
    useFakeClock();
    installApi(buildSession("Running"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);

    await tick(10_000);
    window.dispatchEvent(new Event("visibilitychange"));
    await tick(10);
    expect(currentCalls).toBe(2);

    window.dispatchEvent(new Event("visibilitychange"));
    await tick(10);
    expect(currentCalls).toBe(2);
  });

  it("performs a single synchronization after a network reconnect", async () => {
    useFakeClock();
    installApi(buildSession("Running"));
    renderTimer();
    await tick(0);
    expect(currentCalls).toBe(1);

    await tick(10_000);
    window.dispatchEvent(new Event("offline"));
    await tick(10);
    expect(currentCalls).toBe(1);

    window.dispatchEvent(new Event("online"));
    await tick(10);
    expect(currentCalls).toBe(2);
  });

  it("route remount with a warm cache does not refetch current-timer state", async () => {
    useFakeClock();
    installApi(buildSession("Running"));
    const client = newClient();
    const first = renderTimer(client);
    await tick(0);
    expect(currentCalls).toBe(1);

    first.unmount();
    await tick(30_000);
    expect(currentCalls).toBe(1);

    renderTimer(client);
    await tick(0);
    expect(currentCalls).toBe(1);
  });
});

describe("request deduplication and consumer audit", () => {
  it("multiple mounted consumers share a single current-timer query", async () => {
    installApi(buildSession("Running"));
    const client = newClient();
    render(
      <QueryClientProvider client={client}>
        <HeaderTimer />
        <HeaderTimer />
      </QueryClientProvider>
    );
    await waitFor(() => expect(screen.getAllByTestId("header-timer-trigger")).toHaveLength(2));
    expect(currentCalls).toBe(1);
    expect(client.getQueryData(timerKeys.current)).toMatchObject({ status: "Running" });
  });

  it("keeps exactly one current-timer consumer in the app source", () => {
    const sourceRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
    const files: string[] = [];
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          if (entry.name === "node_modules" || entry.name === ".next") continue;
          walk(fullPath);
        } else if (/\.(ts|tsx)$/.test(entry.name) && !entry.name.endsWith(".test.ts") && !entry.name.endsWith(".test.tsx")) {
          files.push(fullPath);
        }
      }
    };
    walk(sourceRoot);
    const consumers = files
      .filter((file) => {
        const contents = readFileSync(file, "utf8");
        return contents.includes("useCurrentTimer") || contents.includes("endpoints.timer.current");
      })
      .map((file) => path.relative(sourceRoot, file).replace(/\\/g, "/"))
      .sort();
    expect(consumers).toEqual(["components/time-tracking/HeaderTimer.tsx", "hooks/time-tracking/useTimer.ts"]);
  });
});
