import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { getCurrentWeekDateRange, HoursThisWeekWidget } from "../features/home/HoursThisWeekWidget";

function renderWidget(permissions: string[]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><I18nProvider><MemoryRouter><HoursThisWeekWidget permissions={permissions} /></MemoryRouter></I18nProvider></QueryClientProvider>);
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

afterEach(() => vi.unstubAllGlobals());

describe("Hours This Week dashboard widget", () => {
  it("calculates the date range from the local Monday through the selected day", () => {
    expect(getCurrentWeekDateRange(new Date(2026, 9, 8, 15))).toEqual({ from: "2026-10-05", to: "2026-10-08" });
    expect(getCurrentWeekDateRange(new Date(2026, 9, 11, 9))).toEqual({ from: "2026-10-05", to: "2026-10-11" });
  });

  it("loads actual hours from the authorized time-entry summary API", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ actualHours: 5.25 }));
    vi.stubGlobal("fetch", fetchMock);
    renderWidget(["TimeTracking.ViewOwn"]);

    const link = await screen.findByRole("link", { name: /5\.25 hours/ });
    expect(link).toHaveAttribute("href", "/my-work/timesheets");
    const url = new URL(String(fetchMock.mock.calls[0]?.[0]));
    const range = getCurrentWeekDateRange();
    expect(url.pathname).toBe("/api/time-entries/summary");
    expect(url.searchParams.get("from")).toBe(range.from);
    expect(url.searchParams.get("to")).toBe(range.to);
  });

  it("does not query the API without a time-tracking permission", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    renderWidget(["Project.View"]);

    expect(screen.getByText("Time-tracking access is required to view this widget.")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("keeps Western digits in Arabic and shows the localized unit", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ actualHours: 6.5 }));
    vi.stubGlobal("fetch", fetchMock);
    renderWidget(["TimeTracking.ViewOwn"]);

    expect(await screen.findByText("6.5", { exact: false })).toBeInTheDocument();
    expect(screen.getByText("ساعة")).toBeInTheDocument();
  });

  it("shows an error and retries against the API", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ error: "Unavailable" }, 503))
      .mockResolvedValueOnce(jsonResponse({ actualHours: 3 }));
    vi.stubGlobal("fetch", fetchMock);
    renderWidget(["TimeTracking.ViewOwn"]);

    fireEvent.click(await screen.findByRole("button", { name: "Retry" }));
    await waitFor(() => expect(screen.getByRole("link", { name: /3 hours/ })).toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
