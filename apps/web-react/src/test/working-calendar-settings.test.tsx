import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import { OrganizationGeneralSettingsPage, TenantGeneralSettingsPage } from "../features/administration/SettingsWorkspaces";

const fixtures = vi.hoisted(() => ({
  canRead: true,
  canWrite: true,
  profileCanManage: true,
  organizationSettings: null as Record<string, unknown> | null,
  tenantSettings: null as Record<string, unknown> | null,
  calendars: [
    { id: "calendar-1", name: "Tenant Standard", isDefault: true, isConfigured: true, workingDaysPerWeek: 5 },
    { id: "calendar-2", name: "Alternate Week", isDefault: false, isConfigured: true, workingDaysPerWeek: 4 },
    { id: "calendar-3", name: "Incomplete Calendar", isDefault: false, isConfigured: false, workingDaysPerWeek: 0 },
  ],
  update: vi.fn(),
  reset: vi.fn(),
  refetchCalendars: vi.fn(),
  updateScope: "",
  updateOrganizationId: "",
  resetScope: "",
}));

vi.mock("../features/administration/api/settings", () => ({
  useSettingsAccess: () => ({ data: { canReadTenant: fixtures.canRead, canWriteTenant: fixtures.canWrite, canReadPlatform: false, canWritePlatform: false }, isLoading: false, isError: false }),
  useOrganizationProfiles: () => ({ data: [{ id: "organization-1", name: "Operations", canManageSettings: fixtures.profileCanManage }], isLoading: false, isError: false }),
  useOrganizationSettings: (organizationId: string) => ({ data: organizationId ? fixtures.organizationSettings : undefined, isLoading: false, isError: false }),
  useTenantSettings: () => ({ data: fixtures.tenantSettings, isLoading: false, isError: false }),
  useUpdateSetting: (scope: string, organizationId?: string) => {
    fixtures.updateScope = scope;
    fixtures.updateOrganizationId = organizationId ?? "";
    return { mutateAsync: fixtures.update, isPending: false };
  },
  useResetSetting: (scope: string) => {
    fixtures.resetScope = scope;
    return { mutateAsync: fixtures.reset, isPending: false };
  },
  useSettingCurrencies: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateOrganizationProfile: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useTenantFiscalYearPreview: () => ({ data: { startMonth: 1, timeZoneId: "UTC", fiscalYear: 2027, label: "FY 2027", startDate: "2027-01-01", endDate: "2027-12-31", endDateExclusive: "2028-01-01" }, isLoading: false, isError: false }),
  useOrganizationDirectory: () => ({ data: { items: [] }, isLoading: false, isError: false }),
  useOrganizationGrantTargets: () => ({ data: [], isLoading: false, isError: false }),
  useOrganizationSettingsGrants: () => ({ data: [], isLoading: false, isError: false }),
  usePlatformSettings: () => ({ data: undefined, isLoading: false, isError: false }),
  useSaveOrganizationSettingsGrant: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useRevokeOrganizationSettingsGrant: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock("@features/resources/api/resources", () => ({
  useBaseCalendars: () => ({ data: fixtures.calendars, isLoading: false, isError: false, isSuccess: true, refetch: fixtures.refetchCalendars }),
}));

const calendarSetting = (value: unknown, options: { source?: string; inherited?: boolean; version?: string | null } = {}) => ({
  key: "workingCalendar.defaultCalendarId",
  type: "String",
  defaultValue: null,
  value,
  valueSource: options.source ?? "TenantOverride",
  isInherited: options.inherited ?? false,
  canOverride: true,
  version: options.version ?? "AQAAAAAAAAA=",
  validationDescription: "May be null or identify a current tenant Working Calendar.",
  allowedScopes: ["Tenant", "Organization"],
  overrideBehavior: "TenantAndOrganizationOverride",
  securityClassification: "Standard",
});

function renderOrganization(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(<I18nProvider><OrganizationGeneralSettingsPage /></I18nProvider>);
}

function renderTenant(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(<I18nProvider><TenantGeneralSettingsPage /></I18nProvider>);
}

function clickEnabledSave(label: string) {
  const button = screen.getAllByRole("button", { name: label }).find(candidate => !candidate.hasAttribute("disabled"));
  if (!button) throw new Error(`No enabled Save button named ${label}`);
  fireEvent.click(button);
}

describe("working calendar setting", () => {
  beforeEach(() => {
    fixtures.canRead = true;
    fixtures.canWrite = true;
    fixtures.profileCanManage = true;
    fixtures.organizationSettings = {
      scope: "Organization",
      persistenceAvailable: true,
      ownerId: "organization-1",
      settings: [calendarSetting("calendar-1", { source: "OrganizationOverride" })],
    };
    fixtures.tenantSettings = {
      scope: "Tenant",
      persistenceAvailable: true,
      ownerId: "tenant-1",
      settings: [calendarSetting("calendar-1", { source: "TenantOverride" })],
    };
    fixtures.update.mockReset().mockResolvedValue(undefined);
    fixtures.reset.mockReset().mockResolvedValue(undefined);
    fixtures.refetchCalendars.mockReset();
    fixtures.updateScope = "";
    fixtures.updateOrganizationId = "";
    fixtures.resetScope = "";
  });

  it("lets a Tenant choose an authoritative configured calendar and leaves null unselected", async () => {
    fixtures.tenantSettings = {
      scope: "Tenant",
      persistenceAvailable: true,
      ownerId: "tenant-1",
      settings: [calendarSetting(null, { source: "DefinitionDefault", inherited: true, version: null })],
    };
    renderTenant();

    const selector = await screen.findByRole("combobox", { name: enMessages.administration.settingsWorkspace.defaultWorkingCalendar });
    expect(selector).toHaveValue("");
    expect(screen.getByRole("option", { name: enMessages.administration.settingsWorkspace.noCalendarSelected })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Incomplete Calendar" })).not.toBeInTheDocument();

    fireEvent.change(selector, { target: { value: "calendar-2" } });
    clickEnabledSave(enMessages.administration.settingsWorkspace.save);
    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({
      key: "workingCalendar.defaultCalendarId",
      value: "calendar-2",
      expectedVersion: null,
    }));
    expect(fixtures.updateScope).toBe("Tenant");
  });

  it("saves an Organization override through the Organization settings scope", async () => {
    renderOrganization();
    const selector = await screen.findByRole("combobox", { name: enMessages.administration.settingsWorkspace.defaultWorkingCalendar });

    fireEvent.change(selector, { target: { value: "calendar-2" } });
    clickEnabledSave(enMessages.administration.settingsWorkspace.save);

    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({
      key: "workingCalendar.defaultCalendarId",
      value: "calendar-2",
      expectedVersion: "AQAAAAAAAAA=",
    }));
    expect(fixtures.updateScope).toBe("Organization");
    expect(fixtures.updateOrganizationId).toBe("organization-1");
  });

  it("shows the effective Tenant calendar in the inherit option and resets an Organization override", async () => {
    fixtures.organizationSettings = {
      scope: "Organization",
      persistenceAvailable: true,
      ownerId: "organization-1",
      settings: [calendarSetting("calendar-2", { source: "OrganizationOverride" })],
    };
    fixtures.tenantSettings = {
      scope: "Tenant",
      persistenceAvailable: true,
      ownerId: "tenant-1",
      settings: [calendarSetting("calendar-1", { source: "TenantOverride" })],
    };
    renderOrganization();
    const inheritOption = await screen.findByRole("option", { name: "Inherit tenant calendar (currently: Tenant Standard)" });
    const selector = screen.getByRole("combobox", { name: enMessages.administration.settingsWorkspace.defaultWorkingCalendar });

    fireEvent.change(selector, { target: { value: inheritOption.getAttribute("value")! } });
    clickEnabledSave(enMessages.administration.settingsWorkspace.save);

    await waitFor(() => expect(fixtures.reset).toHaveBeenCalledWith({
      key: "workingCalendar.defaultCalendarId",
      expectedVersion: "AQAAAAAAAAA=",
    }));
    expect(fixtures.resetScope).toBe("Organization");
    expect(fixtures.update).not.toHaveBeenCalledWith(expect.objectContaining({ value: null }));
  });

  it("localizes the selector and inheritance label in English and Arabic", async () => {
    const rendered = renderOrganization("en");
    expect(await screen.findByRole("combobox", { name: enMessages.administration.settingsWorkspace.defaultWorkingCalendar })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Inherit tenant calendar (currently: Tenant Standard)" })).toBeInTheDocument();

    rendered.unmount();
    renderOrganization("ar");
    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
    expect(await screen.findByRole("combobox", { name: arMessages.administration.settingsWorkspace.defaultWorkingCalendar })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "استخدام تقويم المستأجر (الحالي: Tenant Standard)" })).toBeInTheDocument();
  });

  it("keeps the selector read-only without tenant configuration management access", async () => {
    fixtures.canWrite = false;
    fixtures.profileCanManage = false;
    renderOrganization();

    expect(await screen.findByRole("combobox", { name: enMessages.administration.settingsWorkspace.defaultWorkingCalendar })).toBeDisabled();
    expect(screen.queryAllByRole("button", { name: enMessages.administration.settingsWorkspace.save })).toHaveLength(0);
  });
});
