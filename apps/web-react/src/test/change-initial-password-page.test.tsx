import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import enMessages from "../i18n/messages/en.json";
import { ChangeInitialPasswordPage } from "../features/account/ChangeInitialPasswordPage";

const fixtures = vi.hoisted(() => ({
  getPolicy: vi.fn(),
  changePassword: vi.fn(),
  logout: vi.fn(),
  user: null as null | { temporaryPasswordExpiresAt?: string | null },
}));

vi.mock("../api/client", () => ({
  api: { getInitialPasswordPolicy: fixtures.getPolicy, changeInitialPassword: fixtures.changePassword },
  ApiError: class ApiError extends Error { code?: string; constructor(message: string, code?: string) { super(message); this.code = code; } },
  setApiErrorLocale: () => undefined,
}));
vi.mock("../auth/AuthProvider", () => ({ useAuth: () => ({ user: fixtures.user, logout: fixtures.logout }) }));

function renderPage() {
  return render(<I18nProvider><MemoryRouter><ChangeInitialPasswordPage /></MemoryRouter></I18nProvider>);
}

beforeEach(() => {
  window.localStorage.setItem("nexus-locale", "en");
  fixtures.user = {};
  fixtures.getPolicy.mockReset().mockResolvedValue({ minimumLength: 9, maximumLength: 96 });
  fixtures.changePassword.mockReset().mockResolvedValue(undefined);
  fixtures.logout.mockReset().mockResolvedValue(undefined);
});

describe("change initial password", () => {
  it.each([undefined, "not-a-date"]) ("fails closed when temporary-password expiry is missing or invalid (%s)", (expiresAt) => {
    fixtures.user = { temporaryPasswordExpiresAt: expiresAt };
    renderPage();

    expect(screen.getByRole("alert")).toHaveTextContent(enMessages.account.initialPassword.expired);
    expect(screen.queryByLabelText(enMessages.account.initialPassword.newPassword)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: enMessages.auth.signOut })).toBeEnabled();
  });

  it("uses the password policy supplied by the API when the expiry is valid", async () => {
    fixtures.user = { temporaryPasswordExpiresAt: new Date(Date.now() + 60_000).toISOString() };
    renderPage();

    const password = await screen.findByLabelText(enMessages.account.initialPassword.newPassword);
    expect(password).toHaveAttribute("minLength", "9");
    expect(password).toHaveAttribute("maxLength", "96");

    fireEvent.change(screen.getByLabelText(enMessages.account.initialPassword.currentPassword), { target: { value: "current" } });
    fireEvent.change(password, { target: { value: "short" } });
    fireEvent.change(screen.getByLabelText(enMessages.account.initialPassword.confirmPassword), { target: { value: "short" } });
    fireEvent.submit(password.closest("form")!);

    expect(screen.getByRole("alert")).toHaveTextContent("9");
    expect(screen.getByRole("alert")).toHaveTextContent("96");
    expect(fixtures.changePassword).not.toHaveBeenCalled();
  });
});
