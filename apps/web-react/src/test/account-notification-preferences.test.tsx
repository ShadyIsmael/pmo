import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { AccountNotificationsPage } from "../features/account/AccountPages";

const mocks = vi.hoisted(() => ({
  useNotificationPreferences: vi.fn(),
  useUpdateNotificationPreferences: vi.fn(),
  enablePushNotifications: vi.fn(),
  isFirebaseMessagingConfigured: vi.fn(),
  isPushDeviceRegistered: vi.fn(),
  deactivateRegisteredPushDevice: vi.fn(),
  updatePreferences: vi.fn(),
}));

vi.mock("@api/notifications", () => ({
  useNotificationPreferences: mocks.useNotificationPreferences,
  useUpdateNotificationPreferences: mocks.useUpdateNotificationPreferences,
}));

vi.mock("@lib/firebase-messaging", () => ({
  enablePushNotifications: mocks.enablePushNotifications,
  isFirebaseMessagingConfigured: mocks.isFirebaseMessagingConfigured,
  isPushDeviceRegistered: mocks.isPushDeviceRegistered,
  deactivateRegisteredPushDevice: mocks.deactivateRegisteredPushDevice,
}));

const defaultPreferences = [
  { type: "ProjectHandover", inAppEnabled: true, pushEnabled: true, emailEnabled: false },
  { type: "ProgramHandover", inAppEnabled: true, pushEnabled: false, emailEnabled: false },
  { type: "PortfolioHandover", inAppEnabled: true, pushEnabled: false, emailEnabled: false },
];

describe("account notification preferences", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useNotificationPreferences.mockReset().mockReturnValue({ data: { items: defaultPreferences, pushAvailable: true, emailAvailable: false }, isLoading: false, isError: false });
    mocks.updatePreferences.mockReset().mockResolvedValue(undefined);
    mocks.useUpdateNotificationPreferences.mockReset().mockReturnValue({ mutateAsync: mocks.updatePreferences, isPending: false });
    mocks.enablePushNotifications.mockReset().mockResolvedValue({ status: "enabled", deviceId: "device-1" });
    mocks.isFirebaseMessagingConfigured.mockReturnValue(true);
    mocks.isPushDeviceRegistered.mockReturnValue(false);
    mocks.deactivateRegisteredPushDevice.mockReset().mockResolvedValue(undefined);
  });

  it("loads preferences only on the account page, explains deferred email, and keeps labels localized in Arabic RTL", async () => {
    const { unmount } = render(<I18nProvider><MemoryRouter><AccountNotificationsPage /></MemoryRouter></I18nProvider>);
    expect(mocks.useNotificationPreferences).toHaveBeenCalledWith(true);
    expect(screen.getByText("Project handover")).toBeInTheDocument();
    expect(screen.getByText("Email delivery will be available when an email integration is enabled.")).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Project handover Email" })).toBeDisabled();

    unmount();
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><AccountNotificationsPage /></MemoryRouter></I18nProvider>);
    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
    expect(screen.getByText("تسليم المشروع")).toBeInTheDocument();
    expect(screen.getByText("سيصبح تسليم البريد الإلكتروني متاحًا عند تفعيل تكامل البريد.")).toBeInTheDocument();
  });

  it("requests browser permission only after the user enables push", async () => {
    render(<I18nProvider><MemoryRouter><AccountNotificationsPage /></MemoryRouter></I18nProvider>);
    expect(mocks.enablePushNotifications).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("checkbox", { name: "Program handover Push" }));
    await waitFor(() => expect(mocks.updatePreferences).toHaveBeenCalledTimes(1));
    expect(mocks.enablePushNotifications).toHaveBeenCalledTimes(1);
    expect(mocks.updatePreferences.mock.calls[0][0].find((item: { type: string }) => item.type === "ProgramHandover").pushEnabled).toBe(true);
    expect(mocks.deactivateRegisteredPushDevice).not.toHaveBeenCalled();
  });

  it("does not deactivate the browser device while another event still uses push", async () => {
    mocks.useNotificationPreferences.mockReturnValue({ data: {
      items: defaultPreferences.map(item => item.type === "ProgramHandover" ? { ...item, pushEnabled: true } : item),
      pushAvailable: true,
      emailAvailable: false,
    }, isLoading: false, isError: false });
    render(<I18nProvider><MemoryRouter><AccountNotificationsPage /></MemoryRouter></I18nProvider>);
    fireEvent.click(screen.getByRole("checkbox", { name: "Project handover Push" }));
    await waitFor(() => expect(mocks.updatePreferences).toHaveBeenCalledTimes(1));
    expect(mocks.deactivateRegisteredPushDevice).not.toHaveBeenCalled();
  });

  it("reports missing configuration without requesting permission or changing server preferences", async () => {
    mocks.useNotificationPreferences.mockReturnValue({ data: { items: defaultPreferences, pushAvailable: false, emailAvailable: false }, isLoading: false, isError: false });
    mocks.isFirebaseMessagingConfigured.mockReturnValue(false);
    render(<I18nProvider><MemoryRouter><AccountNotificationsPage /></MemoryRouter></I18nProvider>);
    fireEvent.click(screen.getByRole("checkbox", { name: "Program handover Push" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Push delivery is not configured for this deployment.");
    expect(mocks.enablePushNotifications).not.toHaveBeenCalled();
    expect(mocks.updatePreferences).not.toHaveBeenCalled();
  });
});
