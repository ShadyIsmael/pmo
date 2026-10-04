import * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { TimerSessionDto } from "@/hooks/time-tracking/useTimer";
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

function segment(id: string, type: "Work" | "Break" | "Pause" | "Idle", startedAt: Date, endedAt?: Date) {
  return { id, segmentType: type, startedAt: startedAt.toISOString(), endedAt: endedAt?.toISOString() ?? null, durationSeconds: 0 };
}

function session(status: TimerSessionDto["status"]): TimerSessionDto {
  const serverNow = new Date();
  const started = new Date(serverNow.getTime() - 90_000);
  const ended = status === "AwaitingFinalization" ? serverNow : undefined;
  return {
    id: `header-${status}`,
    resourceId: "resource-1",
    status,
    startedAt: started.toISOString(),
    endedAt: ended?.toISOString() ?? null,
    serverNow: serverNow.toISOString(),
    timezone: "UTC",
    metrics: { workSeconds: 90, breakSeconds: 0, pauseSeconds: 0, idleSeconds: 0, breakCount: 0, workSessionCount: 1, longestWorkSeconds: 90, averageWorkSeconds: 90 },
    segments: [segment("work-1", "Work", started, ended)],
  };
}

const mockGet = api.get as Mock;
const mockPost = api.post as Mock;
let currentSession: TimerSessionDto | null;

function installApi(initial: TimerSessionDto | null) {
  currentSession = initial;
  mockGet.mockImplementation((path: string) => path === "/api/timer/current" ? Promise.resolve(currentSession) : Promise.resolve([]));
  mockPost.mockImplementation((path: string) => {
    if (path === "/api/timer/start") { currentSession = session("Running"); return Promise.resolve(currentSession); }
    if (path.endsWith("/break")) { currentSession = { ...currentSession!, status: "OnBreak" }; return Promise.resolve(currentSession); }
    if (path.endsWith("/pause")) { currentSession = { ...currentSession!, status: "Paused" }; return Promise.resolve(currentSession); }
    if (path.endsWith("/resume")) { currentSession = { ...currentSession!, status: "Running" }; return Promise.resolve(currentSession); }
    if (path.endsWith("/finish")) { currentSession = { ...currentSession!, status: "AwaitingFinalization" }; return Promise.resolve(currentSession); }
    return Promise.resolve(currentSession);
  });
}

function renderHeader() {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } })}><HeaderTimer /></QueryClientProvider>);
}

beforeEach(() => { mockGet.mockReset(); mockPost.mockReset(); installApi(null); });
afterEach(() => cleanup());

describe("Header timer entry point", () => {
  it("hides the floating timer and opens Start Timer from the Header", async () => {
    const user = userEvent.setup();
    renderHeader();
    expect(screen.queryByTestId("floating-timer-card")).not.toBeInTheDocument();
    expect(await screen.findByRole("button", { name: /Track time/i })).toBeInTheDocument();
    await user.click(screen.getByTestId("header-timer-trigger"));
    expect(await screen.findByTestId("header-timer-start-button")).toBeInTheDocument();
    await user.click(screen.getByTestId("header-timer-start-button"));
    await waitFor(() => expect(mockPost).toHaveBeenCalledWith("/api/timer/start"));
    expect(screen.getByTestId("header-timer-popover")).toBeInTheDocument();
    await user.click(screen.getByTestId("header-timer-close-button"));
    expect(screen.queryByTestId("header-timer-popover")).not.toBeInTheDocument();
  });

  it.each([
    ["Running", "Break", "Pause", "Finish"],
    ["OnBreak", "Resume", "Finish"],
    ["Paused", "Resume", "Finish"],
    ["IdlePaused", "Resume", "Finish"],
  ] as const)("%s exposes the correct Header controls", async (status, ...labels) => {
    const user = userEvent.setup();
    installApi(session(status));
    renderHeader();
    await user.click(await screen.findByTestId("header-timer-trigger"));
    for (const label of labels) expect(screen.getByRole("button", { name: new RegExp(label, "i") })).toBeInTheDocument();
  });

  it("shows Needs completion and reopens the canonical finalization surface", async () => {
    const user = userEvent.setup();
    installApi(session("AwaitingFinalization"));
    renderHeader();
    await user.click(await screen.findByRole("button", { name: /Needs completion/i }));
    await user.click(screen.getByTestId("header-timer-complete-button"));
    expect(screen.getByTestId("header-finalize-side-sheet")).toBeInTheDocument();
  });

  it("opens the finalization surface automatically after Finish", async () => {
    const user = userEvent.setup();
    installApi(session("Running"));
    renderHeader();
    await user.click(await screen.findByTestId("header-timer-trigger"));
    expect(screen.queryByTestId("header-finalize-side-sheet")).not.toBeInTheDocument();
    await user.click(screen.getByTestId("header-timer-finish-button"));
    expect(await screen.findByTestId("header-finalize-side-sheet")).toBeInTheDocument();
  });
});
