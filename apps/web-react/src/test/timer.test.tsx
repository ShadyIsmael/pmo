import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { setTokens } from "../api/client";
import { HeaderTimer } from "../components/HeaderTimer";
import { I18nProvider } from "../i18n/messages";
import { ManagementProvider } from "../api/management";

describe("Header Timer", () => {
  it("shows the no-active-timer state without polling", async () => {
    setTokens("access", "refresh");
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@nexus.com", firstName: "A", lastName: "N", fullName: "A N", roles: [], permissions: [] }), { status: 200 }));
      if (input.includes("/api/timer/current")) return Promise.resolve(new Response("null", { status: 200 }));
      return Promise.resolve(new Response(JSON.stringify({ totalCount: 0, items: [] }), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(<QueryClientProvider client={client}><I18nProvider><AuthProvider><ManagementBridge><HeaderTimer /></ManagementBridge></AuthProvider></I18nProvider></QueryClientProvider>);
    await waitFor(() => expect(fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/timer/current"))).toHaveLength(1));
    expect(screen.getByRole("button", { name: /No active timer/i })).toBeInTheDocument();
  });
});

function ManagementBridge({ children }: { children: React.ReactNode }) {
  return <ManagementProvider user={null}>{children}</ManagementProvider>;
}
