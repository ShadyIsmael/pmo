import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { RequireAuth } from "../auth/RequireAuth";
import { I18nProvider } from "../i18n/messages";

describe("authentication guard", () => {
  it("redirects an unauthenticated visitor to login", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { gcTime: 0 } } });
    render(<QueryClientProvider client={client}><I18nProvider><MemoryRouter initialEntries={["/home"]}><AuthProvider><Routes><Route path="/home" element={<RequireAuth><div>protected</div></RequireAuth>} /><Route path="/login" element={<div>login</div>} /></Routes></AuthProvider></MemoryRouter></I18nProvider></QueryClientProvider>);
    await waitFor(() => expect(screen.getByText("login")).toBeInTheDocument());
    client.clear();
  });
});
