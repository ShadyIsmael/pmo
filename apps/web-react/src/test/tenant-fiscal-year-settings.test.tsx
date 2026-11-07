import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import { TenantGeneralSettingsPage } from "../features/administration/SettingsWorkspaces";

const fixtures = vi.hoisted(() => ({
  canRead: true,
  canWrite: false,
  settings: {
    scope: "Tenant",
    persistenceAvailable: true,
    ownerId: "tenant-1",
    settings: [{
      key: "financial.fiscalYearStartMonth",
      type: "Integer",
      defaultValue: 1,
      value: 4,
      valueSource: "TenantOverride",
      isInherited: false,
      canOverride: true,
      version: "AQAAAAAAAAA=",
      validationDescription: "Must be from 1 through 12.",
      allowedScopes: ["Tenant"],
      overrideBehavior: "TenantOverride",
      securityClassification: "Standard",
    }],
  },
  update: vi.fn(),
}));

vi.mock("../features/administration/api/settings", () => ({
  useSettingsAccess: () => ({ data: { canReadTenant: fixtures.canRead, canWriteTenant: fixtures.canWrite, canReadPlatform: false, canWritePlatform: false }, isLoading: false, isError: false }),
  useTenantSettings: () => ({ data: fixtures.settings, isLoading: false, isError: false }),
  useTenantFiscalYearPreview: (month: number) => ({ data: { startMonth: month, timeZoneId: "UTC", fiscalYear: 2027, label: "FY 2027", startDate: month === 4 ? "2026-04-01" : "2026-07-01", endDate: month === 4 ? "2027-03-31" : "2027-06-30", endDateExclusive: month === 4 ? "2027-04-01" : "2027-07-01" }, isLoading: false, isError: false }),
  useUpdateSetting: () => ({ mutateAsync: fixtures.update, isPending: false }),
  useOrganizationDirectory: () => ({ data: { items: [] }, isLoading: false, isError: false }),
  useOrganizationGrantTargets: () => ({ data: [], isLoading: false, isError: false }),
  useOrganizationProfiles: () => ({ data: [], isLoading: false, isError: false }),
  useOrganizationSettingsGrants: () => ({ data: [], isLoading: false, isError: false }),
  useOrganizationSettings: () => ({ data: undefined, isLoading: false, isError: false }),
  usePlatformSettings: () => ({ data: undefined, isLoading: false, isError: false }),
  useResetSetting: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useSettingCurrencies: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateOrganizationProfile: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useSaveOrganizationSettingsGrant: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useRevokeOrganizationSettingsGrant: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

describe("Tenant fiscal-year settings", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.canRead = true;
    fixtures.canWrite = false;
    fixtures.update.mockReset();
  });

  it("renders the fiscal preview in English and Arabic RTL", async () => {
    const { unmount } = render(<I18nProvider><TenantGeneralSettingsPage /></I18nProvider>);

    expect(screen.getByText(enMessages.administration.settingsWorkspace.financialConfigurationTitle)).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: enMessages.administration.settingsWorkspace.fiscalYearStartMonth })).toBeDisabled();
    expect(screen.getByText("FY 2027")).toBeInTheDocument();
    expect(screen.getByText("Apr 1, 2026")).toBeInTheDocument();
    expect(screen.getByText("Mar 31, 2027")).toBeInTheDocument();

    unmount();
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><TenantGeneralSettingsPage /></I18nProvider>);

    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
    expect(screen.getByText(arMessages.administration.settingsWorkspace.financialConfigurationTitle)).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: arMessages.administration.settingsWorkspace.fiscalYearStartMonth })).toBeDisabled();
    expect(screen.getByText(arMessages.administration.settingsWorkspace.month4)).toBeInTheDocument();
  });

  it("supports draft selection, Cancel, and authorized Save", async () => {
    fixtures.canWrite = true;
    fixtures.update.mockResolvedValue(undefined);
    render(<I18nProvider><TenantGeneralSettingsPage /></I18nProvider>);
    const month = screen.getByRole("combobox", { name: enMessages.administration.settingsWorkspace.fiscalYearStartMonth });

    fireEvent.change(month, { target: { value: "7" } });
    expect(month).toHaveValue("7");
    expect(screen.getByText("Jul 1, 2026")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: enMessages.administration.settingsWorkspace.cancel }));
    expect(month).toHaveValue("4");

    fireEvent.change(month, { target: { value: "7" } });
    fireEvent.click(screen.getByRole("button", { name: enMessages.administration.settingsWorkspace.save }));
    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({
      key: "financial.fiscalYearStartMonth",
      value: 7,
      expectedVersion: "AQAAAAAAAAA=",
    }));
    expect(screen.getByRole("status")).toHaveTextContent(enMessages.administration.settingsWorkspace.fiscalYearSaved);
  });
});
