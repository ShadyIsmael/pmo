import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { EmailNotificationSettingsPage } from "../features/administration/EmailNotificationSettingsPage";

const mocks = vi.hoisted(() => ({
  access: vi.fn(),
  organizations: vi.fn(),
  settings: vi.fn(),
  update: vi.fn(),
  reset: vi.fn(),
  testConnection: vi.fn(),
  sendTest: vi.fn(),
  trustedRelay: vi.fn(),
  logs: vi.fn(),
}));

vi.mock("../features/administration/api/emailNotifications", () => ({
  useEmailNotificationSettingsAccess: mocks.access,
  useEmailNotificationSettings: mocks.settings,
  useUpdateEmailNotificationSettings: mocks.update,
  useResetEmailNotificationSettings: mocks.reset,
  useTestEmailNotificationConnection: mocks.testConnection,
  useSendEmailNotificationTest: mocks.sendTest,
  useCheckTrustedRelay: mocks.trustedRelay,
  useEmailNotificationDeliveryLogs: mocks.logs,
}));

vi.mock("../features/administration/api/settings", () => ({
  useOrganizationProfiles: mocks.organizations,
}));

const profile = {
  enabled: true,
  host: "smtp.example.test",
  port: 587,
  securityMode: "StartTlsRequired" as const,
  authenticationMode: "UsernamePassword" as const,
  userName: "mailer",
  hasPassword: true,
  fromAddress: "noreply@example.test",
  fromName: "FOPX PM",
  replyToAddress: null,
  version: "AQID",
};

describe("Email Notification settings UI", () => {
  beforeEach(() => {
    window.localStorage.clear();
    mocks.access.mockReset().mockReturnValue({
      data: { canReadTenant: true, canManageTenant: true, canTestTenant: true, canReadTenantLogs: true, canReadPlatform: false, canManagePlatform: false, canTestPlatform: false, oauth2Available: false },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    mocks.organizations.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false, refetch: vi.fn() });
    mocks.settings.mockReset().mockReturnValue({
      data: {
        scope: "Tenant",
        ownerId: "tenant-1",
        persistenceAvailable: true,
        hasOverride: true,
        overrideProfile: profile,
        effectiveProfile: profile,
        effectiveSourceScope: "Tenant",
        canManage: true,
        canTest: true,
        canViewLogs: true,
        trustedRelayAvailableForEffectiveHost: false,
        oauth2Available: false,
      },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    mocks.update.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.reset.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.testConnection.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.sendTest.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.trustedRelay.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.logs.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false, refetch: vi.fn() });
  });

  it("offers generic SMTP security and auth modes without a vendor selector or password readback", () => {
    render(<I18nProvider><MemoryRouter><EmailNotificationSettingsPage /></MemoryRouter></I18nProvider>);

    const selects = screen.getAllByRole("combobox");
    expect(selects).toHaveLength(2);
    expect(screen.getByLabelText("Connection security")).toHaveValue("StartTlsRequired");
    expect(screen.getByLabelText("Authentication")).toHaveValue("UsernamePassword");
    expect(screen.getByRole("option", { name: "Required STARTTLS" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Implicit TLS" })).toBeInTheDocument();
    expect(screen.getByText(/OAuth2\/XOAUTH2 is unavailable in this release/)).toBeInTheDocument();
    expect(screen.getByLabelText("SMTP password")).toHaveValue("");
    expect(screen.getByText("A password is saved. It is never returned to this screen.")).toBeInTheDocument();
    expect(document.body.textContent).not.toContain("smtp-secret-never-return-this");
    expect(screen.getByRole("button", { name: "Test connection" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Send test email" })).toBeInTheDocument();
  });

  it("keeps settings labels localized and the page RTL in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><EmailNotificationSettingsPage /></MemoryRouter></I18nProvider>);

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getAllByText("إشعارات البريد الإلكتروني").length).toBeGreaterThan(0);
    expect(screen.getByLabelText("أمان الاتصال")).toHaveValue("StartTlsRequired");
    expect(screen.getByText(/مصادقة OAuth2\/XOAUTH2 غير متاحة في هذا الإصدار/)).toBeInTheDocument();
  });

  it("keeps connection verification and sending a test message as separate actions", () => {
    render(<I18nProvider><MemoryRouter><EmailNotificationSettingsPage /></MemoryRouter></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: "Test connection" }));
    expect(mocks.testConnection.mock.results[0]?.value.mutateAsync).toHaveBeenCalledTimes(1);
    expect(mocks.sendTest.mock.results[0]?.value.mutateAsync).not.toHaveBeenCalled();
  });
});
