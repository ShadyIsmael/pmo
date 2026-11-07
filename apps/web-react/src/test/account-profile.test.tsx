import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProfilePage } from "../features/account/AccountPages";

const mocks = vi.hoisted(() => ({ useAccount: vi.fn(), mutateAsync: vi.fn(), refreshUser: vi.fn() }));

vi.mock("@api/account", () => ({
  useAccount: mocks.useAccount,
  useUpdateAccountProfile: () => ({ mutateAsync: mocks.mutateAsync, isPending: false }),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ refreshUser: mocks.refreshUser }),
}));

const profile = {
  userId: "self",
  firstName: "Ada",
  lastName: "Lovelace",
  fullName: "Ada Lovelace",
  email: "ada@example.test",
  authenticationSource: "Local" as const,
  passwordChangedAt: null,
  lastLoginAt: null,
};

describe("My Account profile", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useAccount.mockReset().mockReturnValue({
      data: { profile, professionalProfile: null }, isLoading: false, isError: false, refetch: vi.fn(),
    });
    mocks.mutateAsync.mockReset().mockResolvedValue(profile);
    mocks.refreshUser.mockReset().mockResolvedValue(undefined);
  });

  it("edits only first and last name and shows a real no-Resource state", async () => {
    render(<I18nProvider><ProfilePage /></I18nProvider>);
    expect(screen.getByText("No professional profile is linked")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveAttribute("readonly");

    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Augusta" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(mocks.mutateAsync).toHaveBeenCalledWith({ firstName: "Augusta", lastName: "Lovelace" }));
    expect(await screen.findByRole("status")).toHaveTextContent("Your profile was updated.");
  });
});
