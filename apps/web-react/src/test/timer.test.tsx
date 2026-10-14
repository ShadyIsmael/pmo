import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { setTokens } from "../api/client";
import { HeaderTimer } from "../components/HeaderTimer";
import { I18nProvider } from "../i18n/messages";
import { ManagementProvider } from "../api/management";
import type { TimerSessionDto } from "../api/timer";

describe("Header Timer", () => {
  it("shows the no-active-timer state without polling", async () => {
    setTokens("access", "refresh");
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@nexus.com", firstName: "A", lastName: "N", fullName: "A N", roles: [], permissions: ["TimeTracking.ManageOwn"] }), { status: 200 }));
      if (input.includes("/api/timer/current")) return Promise.resolve(new Response("null", { status: 200 }));
      return Promise.resolve(new Response(JSON.stringify({ totalCount: 0, items: [] }), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><I18nProvider><AuthProvider><ManagementBridge><HeaderTimer /></ManagementBridge></AuthProvider></I18nProvider></QueryClientProvider>);
    await waitFor(() => expect(fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/timer/current"))).toHaveLength(1));
    fireEvent.click(screen.getByRole("button", { name: /Track time/i }));
    const popover = await screen.findByRole("dialog", { name: /Track time/i });
    expect(popover.parentElement).toBe(document.body);
    expect(popover).toHaveClass("fixed", "top-14", "right-4", "sm:top-16", "sm:right-6");
  });

  it("shows translated session status and the four segment-backed counters", async () => {
    window.localStorage.setItem("nexus-locale", "en");
    const now = Date.now();
    const session = makeSession("OnBreak", now, [
      { id: "work-1", segmentType: "Work", startedAt: new Date(now - 3_660_000).toISOString(), endedAt: new Date(now - 60_000).toISOString(), durationSeconds: 3600 },
      { id: "break-1", segmentType: "Break", startedAt: new Date(now - 60_000).toISOString(), endedAt: null, durationSeconds: 60 },
    ]);
    const fetchMock = makeFetchMock(session);
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><I18nProvider><AuthProvider><ManagementBridge><HeaderTimer /></ManagementBridge></AuthProvider></I18nProvider></QueryClientProvider>);

    fireEvent.click(await screen.findByRole("button", { name: "On break" }));
    expect(await screen.findByRole("dialog", { name: "On break" })).toBeInTheDocument();
    expect(screen.getByTestId("header-timer-work")).toHaveTextContent("01:00:00");
    expect(screen.getByTestId("header-timer-break-counter")).toHaveTextContent("00:01:00");
    expect(screen.getByTestId("header-timer-pause")).toHaveTextContent("00:00:00");
    expect(screen.getByTestId("header-timer-total")).toHaveTextContent("01:01:00");
  });

  it("localizes idle-paused status and keeps timer digits Western in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    const now = Date.now();
    const session = makeSession("IdlePaused", now, [
      { id: "work-1", segmentType: "Work", startedAt: new Date(now - 720_000).toISOString(), endedAt: new Date(now - 120_000).toISOString(), durationSeconds: 600 },
      { id: "idle-1", segmentType: "Idle", startedAt: new Date(now - 120_000).toISOString(), endedAt: null, durationSeconds: 120 },
    ]);
    vi.stubGlobal("fetch", makeFetchMock(session));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><I18nProvider><AuthProvider><ManagementBridge><HeaderTimer /></ManagementBridge></AuthProvider></I18nProvider></QueryClientProvider>);

    fireEvent.click(await screen.findByRole("button", { name: "متوقف لعدم النشاط" }));
    expect(await screen.findByRole("dialog", { name: "متوقف لعدم النشاط" })).toBeInTheDocument();
    expect(screen.getByText("تم إيقاف المؤقت لعدم اكتشاف نشاط. استأنفه صراحةً عند العودة.")).toBeInTheDocument();
    expect(screen.getByTestId("header-timer-work")).toHaveTextContent("00:10:00");
    expect(screen.getByTestId("header-timer-work").textContent).not.toMatch(/[٠-٩]/);
    expect(screen.getByTestId("header-timer-total")).toHaveTextContent("00:12:00");
  });

  it("shows actionable timer errors instead of failing silently", async () => {
    window.localStorage.setItem("nexus-locale", "en");
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      if (input.includes("/api/auth/me")) return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@nexus.com", firstName: "A", lastName: "N", fullName: "A N", roles: [], permissions: ["TimeTracking.ManageOwn"] }), { status: 200 }));
      if (input.includes("/api/timer/current")) return Promise.resolve(new Response("null", { status: 200 }));
      if (input.includes("/api/timer/start") && init?.method === "POST") return Promise.resolve(new Response(JSON.stringify({ error: "No active Resource is linked to the authenticated user" }), { status: 400 }));
      return Promise.resolve(new Response(JSON.stringify({ totalCount: 0, items: [] }), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><I18nProvider><AuthProvider><ManagementBridge><HeaderTimer /></ManagementBridge></AuthProvider></I18nProvider></QueryClientProvider>);

    fireEvent.click(await screen.findByRole("button", { name: "Track time" }));
    fireEvent.click(screen.getByRole("button", { name: "Start timer" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("No active Resource is linked to the authenticated user");
  });
});

function makeSession(status: TimerSessionDto["status"], now: number, segments: TimerSessionDto["segments"]): TimerSessionDto {
  return {
    id: "session-1",
    resourceId: "resource-1",
    status,
    startedAt: new Date(now - 3_600_000).toISOString(),
    serverNow: new Date(now).toISOString(),
    timezone: "UTC",
    metrics: { workSeconds: 0, breakSeconds: 0, pauseSeconds: 0, idleSeconds: 0, breakCount: 0, workSessionCount: 0, longestWorkSeconds: 0, averageWorkSeconds: 0 },
    segments,
  };
}

function makeFetchMock(session: TimerSessionDto) {
  return vi.fn().mockImplementation((input: string) => {
    if (input.includes("/api/auth/me")) return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@nexus.com", firstName: "A", lastName: "N", fullName: "A N", roles: [], permissions: ["TimeTracking.ManageOwn"] }), { status: 200 }));
    if (input.includes("/api/timer/current")) return Promise.resolve(new Response(JSON.stringify(session), { status: 200 }));
    return Promise.resolve(new Response(JSON.stringify({ totalCount: 0, items: [] }), { status: 200 }));
  });
}

function ManagementBridge({ children }: { children: React.ReactNode }) {
  return <ManagementProvider user={null}>{children}</ManagementProvider>;
}
