import * as React from "react";
import { Navigate } from "react-router-dom";
import { Globe2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { getApiFieldErrors, ApiError } from "@api/client";
import { useI18n } from "@i18n/messages";
import { useBaseCalendars, type ResourceBaseCalendarDto } from "@features/resources/api/resources";
import { PlatformSettingsPage } from "./AdminPages";
import { PhaseGateRequirementsCard } from "@features/organization/PhaseGateRequirementsCard";
import {
  useOrganizationDirectory,
  useOrganizationGrantTargets,
  useOrganizationProfiles,
  useOrganizationSettingsGrants,
  useOrganizationSettings,
  usePlatformSettings,
  useResetSetting,
  useSettingsAccess,
  useSettingCurrencies,
  useTenantFiscalYearPreview,
  useTenantSettings,
  useUpdateOrganizationProfile,
  useUpdateSetting,
  useSaveOrganizationSettingsGrant,
  useRevokeOrganizationSettingsGrant,
  type EffectiveSetting,
  type OrganizationProfile,
  type OrganizationProfileUpdate,
  type SettingsReadResponse,
  type SettingsScope,
} from "./api/settings";

export function PlatformGeneralSettingsPage() {
  const { t } = useI18n();
  const access = useSettingsAccess();
  const canRead = access.data?.canReadPlatform === true;
  const settings = usePlatformSettings(canRead);
  const regionalSettings = React.useMemo(() => settings.data ? regionalSettingsOnly(settings.data) : null, [settings.data]);

  if (access.isLoading) return <LoadingState />;
  if (access.isError) return <ErrorState />;
  if (!canRead) return <Navigate to="/administration" replace />;
  if (settings.isLoading) return <LoadingState />;
  if (settings.isError || !settings.data) return <ErrorState />;

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6">
      <PageHeader title={t("administration.settingsWorkspace.platformTitle")} description={t("administration.settingsWorkspace.platformDescription")} />
      <PlatformSettingsPage />
      {!settings.data.persistenceAvailable && <PersistenceNotice />}
      <Card>
        <CardHeader><CardTitle>{t("administration.settingsWorkspace.regionalTitle")}</CardTitle></CardHeader>
        <CardContent>
          <SettingsEditor response={regionalSettings!} scope="Platform" canWrite={access.data?.canWritePlatform === true && settings.data.persistenceAvailable} />
        </CardContent>
      </Card>
    </div>
  );
}

export function TenantGeneralSettingsPage() {
  const { t } = useI18n();
  const access = useSettingsAccess();
  const canRead = access.data?.canReadTenant === true;
  const settings = useTenantSettings(canRead);
  const scopedSettings = React.useMemo(() => settings.data ? settingsForScope(settings.data, "Tenant") : null, [settings.data]);

  if (access.isLoading) return <LoadingState />;
  if (access.isError || !access.data) return <ErrorState />;
  if (!canRead) return <Navigate to="/administration" replace />;
  if (settings.isLoading) return <LoadingState />;
  if (settings.isError || !settings.data) return <ErrorState />;

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6">
      <PageHeader title={t("administration.settingsWorkspace.tenantTitle")} description={t("administration.settingsWorkspace.tenantDescription")} />
      {!settings.data.persistenceAvailable && <PersistenceNotice />}
      <Card>
        <CardHeader>
          <CardTitle>{t("administration.settingsWorkspace.regionalTitle")}</CardTitle>
          <CardDescription>{t("administration.settingsWorkspace.tenantScopeDescription")}</CardDescription>
        </CardHeader>
        <CardContent>
          <SettingsEditor response={scopedSettings!} scope="Tenant" canWrite={access.data.canWriteTenant && settings.data.persistenceAvailable} />
        </CardContent>
      </Card>
      <FiscalYearConfigurationCard response={settings.data} canWrite={access.data.canWriteTenant && settings.data.persistenceAvailable} />
      {access.data.canWriteTenant && <OrganizationSettingsAccessCard />}
    </div>
  );
}

function OrganizationSettingsAccessCard() {
  const { t } = useI18n();
  const directory = useOrganizationDirectory();
  const users = useOrganizationGrantTargets();
  const [organizationId, setOrganizationId] = React.useState("");
  const [userId, setUserId] = React.useState("");
  const [canRead, setCanRead] = React.useState(true);
  const [canManage, setCanManage] = React.useState(false);
  const [feedback, setFeedback] = React.useState("");
  const grants = useOrganizationSettingsGrants(organizationId, Boolean(organizationId));
  const saveGrant = useSaveOrganizationSettingsGrant();
  const revokeGrant = useRevokeOrganizationSettingsGrant();
  const currentGrant = grants.data?.find(grant => grant.userId === userId);

  React.useEffect(() => {
    const items = directory.data?.items ?? [];
    if (!items.length) setOrganizationId("");
    else if (!organizationId || !items.some(item => item.id === organizationId)) setOrganizationId(items[0].id);
  }, [directory.data, organizationId]);

  React.useEffect(() => {
    if (currentGrant) {
      setCanRead(currentGrant.canRead);
      setCanManage(currentGrant.canManage);
    } else {
      setCanRead(true);
      setCanManage(false);
    }
  }, [currentGrant?.id, currentGrant?.version, currentGrant?.canRead, currentGrant?.canManage]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!organizationId || !userId) return;
    setFeedback("");
    try {
      await saveGrant.mutateAsync({ organizationId, userId, canRead: canRead || canManage, canManage, expectedVersion: currentGrant?.version ?? null });
      setFeedback(t("administration.settingsWorkspace.grantSaved"));
    } catch (error) {
      setFeedback(extractErrorDetail(error) ?? t("administration.settingsWorkspace.grantSaveFailed"));
    }
  };

  const removeGrant = async (grant: NonNullable<typeof grants.data>[number]) => {
    if (!window.confirm(t("administration.settingsWorkspace.revokeGrantConfirm", { user: grant.userName }))) return;
    setFeedback("");
    try {
      await revokeGrant.mutateAsync({ organizationId, userId: grant.userId, version: grant.version });
      setFeedback(t("administration.settingsWorkspace.grantRevoked"));
      if (grant.userId === userId) setUserId("");
    } catch (error) {
      setFeedback(extractErrorDetail(error) ?? t("administration.settingsWorkspace.grantSaveFailed"));
    }
  };

  const loading = directory.isLoading || users.isLoading;
  const failed = directory.isError || users.isError || grants.isError;
  return <Card>
    <CardHeader>
      <CardTitle>{t("administration.settingsWorkspace.organizationAccessTitle")}</CardTitle>
      <CardDescription>{t("administration.settingsWorkspace.organizationAccessDescription")}</CardDescription>
    </CardHeader>
    <CardContent className="space-y-5">
      {loading && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.loading")}</p>}
      {failed && <p role="alert" className="text-sm text-destructive">{t("administration.settingsWorkspace.grantLoadFailed")}</p>}
      {!loading && !failed && (directory.data?.items.length ?? 0) === 0 && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.noOrganizationsToGrant")}</p>}
      {!loading && !failed && (users.data?.length ?? 0) === 0 && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.noUsersToGrant")}</p>}
      {!loading && !failed && Boolean(directory.data?.items.length) && Boolean(users.data?.length) && <>
        <form className="space-y-4" onSubmit={submit}>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="grant-organization">{t("administration.settingsWorkspace.organization")}</Label>
              <select id="grant-organization" value={organizationId} onChange={event => setOrganizationId(event.target.value)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
                {(directory.data?.items ?? []).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
              </select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="grant-user">{t("administration.settingsWorkspace.grantUser")}</Label>
              <select id="grant-user" value={userId} onChange={event => { setUserId(event.target.value); setFeedback(""); }} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm">
                <option value="">{t("administration.settingsWorkspace.selectUser")}</option>
                {(users.data ?? []).map(user => <option key={user.id} value={user.id}>{user.fullName} ({user.email})</option>)}
              </select>
            </div>
          </div>
          <div className="flex flex-wrap gap-5">
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={canRead || canManage} onChange={event => setCanRead(event.target.checked)} disabled={canManage} />{t("administration.settingsWorkspace.grantRead")}</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={canManage} onChange={event => { setCanManage(event.target.checked); if (event.target.checked) setCanRead(true); }} />{t("administration.settingsWorkspace.grantManage")}</label>
          </div>
          {feedback && <p role="status" className="text-sm text-muted-foreground">{feedback}</p>}
          <div className="flex justify-end"><Button type="submit" disabled={!organizationId || !userId || saveGrant.isPending || revokeGrant.isPending}>{saveGrant.isPending ? t("administration.settingsWorkspace.loading") : currentGrant ? t("administration.settingsWorkspace.updateGrant") : t("administration.settingsWorkspace.saveGrant")}</Button></div>
        </form>
        <div className="space-y-3 border-t pt-4">
          <h3 className="text-sm font-semibold">{t("administration.settingsWorkspace.currentGrants")}</h3>
          {grants.isLoading ? <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.loading")}</p>
            : grants.data?.length ? grants.data.map(grant => <div key={grant.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
              <div><p className="text-sm font-medium">{grant.userName}</p><p className="text-xs text-muted-foreground">{grant.userEmail} - {grant.canManage ? t("administration.settingsWorkspace.grantManage") : t("administration.settingsWorkspace.grantRead")}</p></div>
              <Button type="button" size="sm" variant="outline" disabled={revokeGrant.isPending} onClick={() => void removeGrant(grant)}>{t("administration.settingsWorkspace.revokeGrant")}</Button>
            </div>) : <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.noGrants")}</p>}
        </div>
      </>}
    </CardContent>
  </Card>;
}

type ProfileDraft = Omit<OrganizationProfile, "id" | "updatedAt" | "canManageSettings">;

export function OrganizationGeneralSettingsPage() {
  const { t } = useI18n();
  const access = useSettingsAccess();
  const canRead = access.data?.canReadTenant === true;
  const canWriteTenant = access.data?.canWriteTenant === true;
  const profiles = useOrganizationProfiles(canRead, true);
  const updateProfile = useUpdateOrganizationProfile();
  const [selectedId, setSelectedId] = React.useState("");
  const [draft, setDraft] = React.useState<ProfileDraft | null>(null);
  const [serverErrors, setServerErrors] = React.useState<Record<string, string[]>>({});
  const [saveError, setSaveError] = React.useState("");
  const [saveMessage, setSaveMessage] = React.useState("");
  const [settingsDirty, setSettingsDirty] = React.useState(false);
  const profileList = profiles.data ?? [];

  React.useEffect(() => {
    if (!profileList.length) {
      setSelectedId("");
      return;
    }
    if (!selectedId || !profileList.some(profile => profile.id === selectedId)) setSelectedId(profileList[0].id);
  }, [profileList, selectedId]);

  const selected = profileList.find(profile => profile.id === selectedId);
  React.useEffect(() => {
    setDraft(selected ? toDraft(selected) : null);
    setServerErrors({});
    setSaveError("");
    setSaveMessage("");
  }, [selected?.id, selected?.updatedAt]);

  const profileDirty = Boolean(selected && draft && !sameDraft(draft, toDraft(selected)));
  const dirty = profileDirty || settingsDirty;
  useUnsavedNavigationWarning(dirty, t("administration.settingsWorkspace.unsaved"));
  React.useEffect(() => {
    if (!dirty) return;
    const warnBeforeLeave = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnBeforeLeave);
    return () => window.removeEventListener("beforeunload", warnBeforeLeave);
  }, [dirty]);

  const settings = useOrganizationSettings(selectedId, canRead && Boolean(selectedId));
  const tenantSettings = useTenantSettings(canRead);
  const scopedSettings = React.useMemo(() => settings.data ? settingsForScope(settings.data, "Organization") : null, [settings.data]);
  const inheritedValues = React.useMemo(() => ({
    "workingCalendar.defaultCalendarId": tenantSettings.data?.settings.find(setting => setting.key === "workingCalendar.defaultCalendarId")?.value ?? null,
  }), [tenantSettings.data]);
  if (access.isLoading) return <LoadingState />;
  if (access.isError || !access.data) return <ErrorState />;
  if (!canRead) return <Navigate to="/administration" replace />;
  if (profiles.isLoading) return <LoadingState />;
  if (profiles.isError) return <ErrorState />;

  const chooseOrganization = (id: string) => {
    if (dirty && !window.confirm(t("administration.settingsWorkspace.unsaved"))) return;
    setSelectedId(id);
    setSettingsDirty(false);
  };

  const cancelProfileChanges = () => {
    if (!selected || !profileDirty || !window.confirm(t("administration.settingsWorkspace.discard"))) return;
    setDraft(toDraft(selected));
    setServerErrors({});
    setSaveError("");
    setSaveMessage("");
  };

  const saveProfileChanges = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selected || !draft || !profileDirty || !canWriteTenant || !selected.canManageSettings) return;
    setSaveError("");
    setServerErrors({});
    setSaveMessage("");
    try {
      const update: OrganizationProfileUpdate = { ...draft, expectedUpdatedAt: selected.updatedAt ?? null };
      await updateProfile.mutateAsync({ id: selected.id, update });
      setSaveMessage(t("administration.settingsWorkspace.saved"));
    } catch (error) {
      setServerErrors(extractValidationErrors(error));
      setSaveError(extractErrorDetail(error) ?? t("administration.settingsWorkspace.saveFailed"));
    }
  };

  const canManageSelected = canWriteTenant && selected?.canManageSettings === true;
  return (
    <div className="mx-auto w-full max-w-6xl space-y-6">
      <PageHeader title={t("administration.settingsWorkspace.organizationTitle")} description={t("administration.settingsWorkspace.organizationDescription")} />
      <Card>
        <CardHeader>
          <CardTitle>{t("administration.settingsWorkspace.organizationProfile")}</CardTitle>
          <CardDescription>{t("administration.settingsWorkspace.organizationProfileDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {profileList.length === 0 ? <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.noOrganizations")}</p> : <>
            {profileList.length > 1 && <div className="max-w-xl space-y-2">
              <Label htmlFor="organization-settings-select">{t("administration.settingsWorkspace.selectOrganization")}</Label>
              <select
                id="organization-settings-select"
                value={selectedId}
                onChange={event => chooseOrganization(event.target.value)}
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              >{profileList.map(profile => <option key={profile.id} value={profile.id}>{profile.name}</option>)}</select>
            </div>}
            {selected && draft && <form className="space-y-5" onSubmit={saveProfileChanges}>
              {saveError && <div role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
                <p>{saveError}</p>
                {Object.entries(serverErrors).map(([field, messages]) => messages.map((message, index) => <p key={`${field}-${index}`} className="mt-1">{field ? `${t(`administration.settingsWorkspace.${field[0].toLowerCase()}${field.slice(1)}`)}: ` : ""}{message}</p>))}
              </div>}
              {saveMessage && <p role="status" className="rounded-lg border border-emerald-500/30 bg-emerald-500/5 p-3 text-sm">{saveMessage}</p>}
              <div className="grid gap-4 md:grid-cols-2">
                <ProfileField field="name" label={t("administration.settingsWorkspace.name")} value={draft.name} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, name: value })} required />
                <ProfileField field="website" label={t("administration.settingsWorkspace.website")} value={draft.website ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, website: value })} type="url" />
                <ProfileField field="email" label={t("administration.settingsWorkspace.email")} value={draft.email ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, email: value })} type="email" />
                <ProfileField field="phone" label={t("administration.settingsWorkspace.phone")} value={draft.phone ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, phone: value })} type="tel" />
                <ProfileField field="addressLine1" label={t("administration.settingsWorkspace.addressLine1")} value={draft.addressLine1 ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, addressLine1: value })} />
                <ProfileField field="addressLine2" label={t("administration.settingsWorkspace.addressLine2")} value={draft.addressLine2 ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, addressLine2: value })} />
                <ProfileField field="city" label={t("administration.settingsWorkspace.city")} value={draft.city ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, city: value })} />
                <ProfileField field="country" label={t("administration.settingsWorkspace.country")} value={draft.country ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, country: value })} />
                <ProfileField field="postcode" label={t("administration.settingsWorkspace.postcode")} value={draft.postcode ?? ""} errors={serverErrors} disabled={!canManageSelected || updateProfile.isPending} onChange={value => setDraft({ ...draft, postcode: value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="organization-description">{t("administration.settingsWorkspace.description")}</Label>
                <textarea
                  id="organization-description"
                  value={draft.description ?? ""}
                  disabled={!canManageSelected || updateProfile.isPending}
                  maxLength={1000}
                  onChange={event => setDraft({ ...draft, description: event.target.value })}
                  className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                  aria-invalid={Boolean(serverErrors.description ?? serverErrors.Description)}
                  aria-describedby={serverErrors.description ?? serverErrors.Description ? "organization-description-errors" : undefined}
                />
                <FieldErrors id="organization-description-errors" errors={serverErrors.description ?? serverErrors.Description} />
              </div>
              {canManageSelected && <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
                <Button type="button" variant="outline" disabled={!profileDirty || updateProfile.isPending} onClick={cancelProfileChanges}>{t("administration.settingsWorkspace.cancel")}</Button>
                <Button type="submit" disabled={!profileDirty || updateProfile.isPending}>{updateProfile.isPending ? t("administration.settingsWorkspace.loading") : t("administration.settingsWorkspace.save")}</Button>
              </div>}
              {!canManageSelected && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.readOnly")}</p>}
            </form>}
          </>}
        </CardContent>
      </Card>

      {selected && <Card>
        <CardHeader>
          <CardTitle>{t("administration.settingsWorkspace.regionalTitle")}</CardTitle>
          <CardDescription>{t("administration.settingsWorkspace.organizationScopeDescription")}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {settings.isLoading || tenantSettings.isLoading ? <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.loading")}</p>
            : settings.isError || !settings.data || tenantSettings.isError || !tenantSettings.data ? <p role="alert" className="text-sm text-destructive">{t("administration.settingsWorkspace.loadError")}</p>
              : <>
                {!settings.data.persistenceAvailable && <PersistenceNotice />}
                <SettingsEditor
                  response={scopedSettings!}
                  scope="Organization"
                  canWrite={canManageSelected && settings.data.persistenceAvailable}
                  inheritedValues={inheritedValues}
                  onDirtyChange={setSettingsDirty}
                  warnUnsaved={false}
                />
              </>}
        </CardContent>
      </Card>}
      {selected && <PhaseGateRequirementsCard organizationId={selected.id} canManage={canManageSelected} />}
    </div>
  );
}

function regionalSettingsOnly(response: SettingsReadResponse): SettingsReadResponse {
  return { ...response, settings: response.settings.filter(setting => setting.key.startsWith("regional.")) };
}

function settingsForScope(response: SettingsReadResponse, scope: "Tenant" | "Organization"): SettingsReadResponse {
  return {
    ...response,
    settings: response.settings.filter(setting => setting.key.startsWith("regional.")
      || (setting.key === "workingCalendar.defaultCalendarId" && setting.allowedScopes.includes(scope))),
  };
}

function FiscalYearConfigurationCard({ response, canWrite }: { response: SettingsReadResponse; canWrite: boolean }) {
  const { t, formatDate } = useI18n();
  const setting = response.settings.find(item => item.key === "financial.fiscalYearStartMonth");
  const configuredMonth = typeof setting?.value === "number" && setting.value >= 1 && setting.value <= 12 ? setting.value : 1;
  const [draftMonth, setDraftMonth] = React.useState(configuredMonth);
  const [feedback, setFeedback] = React.useState<{ kind: "success" | "error"; text: string } | null>(null);
  const preview = useTenantFiscalYearPreview(draftMonth, response.persistenceAvailable);
  const saveSetting = useUpdateSetting("Tenant");
  const dirty = draftMonth !== configuredMonth;

  React.useEffect(() => {
    setDraftMonth(configuredMonth);
  }, [configuredMonth]);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!setting || !dirty || !canWrite) return;
    setFeedback(null);
    try {
      await saveSetting.mutateAsync({ key: setting.key, value: draftMonth, expectedVersion: setting.version });
      setFeedback({ kind: "success", text: t("administration.settingsWorkspace.fiscalYearSaved") });
    } catch (error) {
      if (readProblemCode(error) === "fiscalYearChangeBlocked") {
        setFeedback({ kind: "error", text: t("administration.settingsWorkspace.fiscalYearChangeBlocked") });
      } else {
        setFeedback({ kind: "error", text: extractErrorDetail(error) ?? t("administration.settingsWorkspace.fiscalYearSaveFailed") });
      }
    }
  };

  const cancel = () => {
    setDraftMonth(configuredMonth);
    setFeedback(null);
  };

  return <Card>
    <CardHeader>
      <CardTitle>{t("administration.settingsWorkspace.financialConfigurationTitle")}</CardTitle>
      <CardDescription>{t("administration.settingsWorkspace.financialConfigurationDescription")}</CardDescription>
    </CardHeader>
    <CardContent className="space-y-5">
      <form className="space-y-4" onSubmit={save}>
        <div className="max-w-md space-y-2">
          <Label htmlFor="fiscal-year-start-month">{t("administration.settingsWorkspace.fiscalYearStartMonth")}</Label>
          <select
            id="fiscal-year-start-month"
            value={draftMonth}
            disabled={!canWrite || saveSetting.isPending}
            onChange={event => { setDraftMonth(Number(event.target.value)); setFeedback(null); }}
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
          >
            {Array.from({ length: 12 }, (_, index) => index + 1).map(month => (
              <option key={month} value={month}>{t(`administration.settingsWorkspace.month${month}`)}</option>
            ))}
          </select>
        </div>

        <div className="rounded-lg border bg-muted/20 p-4" aria-live="polite">
          <h3 className="text-sm font-semibold">{t("administration.settingsWorkspace.fiscalYearPreview")}</h3>
          {preview.isLoading ? <p className="mt-2 text-sm text-muted-foreground">{t("administration.settingsWorkspace.loading")}</p>
            : preview.isError || !preview.data ? <p className="mt-2 text-sm text-destructive" role="alert">{t("administration.settingsWorkspace.fiscalYearPreviewFailed")}</p>
              : <dl className="mt-3 grid gap-3 sm:grid-cols-3">
                <div><dt className="text-xs text-muted-foreground">{t("administration.settingsWorkspace.fiscalYearLabel")}</dt><dd className="font-medium">{preview.data.label}</dd></div>
                <div><dt className="text-xs text-muted-foreground">{t("administration.settingsWorkspace.startDate")}</dt><dd className="font-medium">{formatDate(new Date(`${preview.data.startDate}T00:00:00Z`), { timeZone: "UTC" })}</dd></div>
                <div><dt className="text-xs text-muted-foreground">{t("administration.settingsWorkspace.endDate")}</dt><dd className="font-medium">{formatDate(new Date(`${preview.data.endDate}T00:00:00Z`), { timeZone: "UTC" })}</dd></div>
              </dl>}
        </div>

        {feedback && <p role={feedback.kind === "error" ? "alert" : "status"} className={`text-sm ${feedback.kind === "error" ? "text-destructive" : "text-emerald-700 dark:text-emerald-400"}`}>{feedback.text}</p>}
        {canWrite && <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
          <Button type="button" variant="outline" disabled={!dirty || saveSetting.isPending} onClick={cancel}>{t("administration.settingsWorkspace.cancel")}</Button>
          <Button type="submit" disabled={!dirty || saveSetting.isPending}>{saveSetting.isPending ? t("administration.settingsWorkspace.loading") : t("administration.settingsWorkspace.save")}</Button>
        </div>}
        {!canWrite && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.readOnly")}</p>}
      </form>
    </CardContent>
  </Card>;
}

function readProblemCode(error: unknown): string | null {
  if (!error || typeof error !== "object") return null;
  const candidate = error as Record<string, unknown>;
  for (const source of [candidate, candidate.body, candidate.data, candidate.response]) {
    if (!source || typeof source !== "object") continue;
    const record = source as Record<string, unknown>;
    if (typeof record.code === "string") return record.code;
    const extensions = record.extensions;
    if (extensions && typeof extensions === "object" && typeof (extensions as Record<string, unknown>).code === "string")
      return (extensions as Record<string, unknown>).code as string;
  }
  return null;
}

function SettingsEditor({
  response,
  scope,
  canWrite,
  inheritedValues,
  onDirtyChange,
  warnUnsaved = true,
}: {
  response: SettingsReadResponse;
  scope: SettingsScope;
  canWrite: boolean;
  inheritedValues?: Record<string, unknown>;
  onDirtyChange?: (dirty: boolean) => void;
  warnUnsaved?: boolean;
}) {
  const { t } = useI18n();
  const updateSetting = useUpdateSetting(scope, scope === "Organization" ? response.ownerId ?? undefined : undefined);
  const resetSetting = useResetSetting(scope, scope === "Organization" ? response.ownerId ?? undefined : undefined);
  const currencyOptions = useSettingCurrencies(response.settings.some(setting => setting.key === "regional.defaultCurrency"));
  const [drafts, setDrafts] = React.useState<Record<string, unknown>>({});
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const [messages, setMessages] = React.useState<Record<string, string>>({});

  React.useEffect(() => {
    setDrafts(Object.fromEntries(response.settings.map(setting => [setting.key, settingDraftValue(setting, scope)])));
    setErrors({});
    setMessages({});
  }, [response, scope]);

  const dirtyKeys = response.settings.filter(setting => !sameValue(drafts[setting.key], settingDraftValue(setting, scope))).map(setting => setting.key);
  const dirty = dirtyKeys.length > 0;
  React.useEffect(() => onDirtyChange?.(dirty), [dirty, onDirtyChange]);
  useUnsavedNavigationWarning(warnUnsaved && dirty, t("administration.settingsWorkspace.unsavedSettings"));
  React.useEffect(() => {
    if (!warnUnsaved || !dirty) return;
    const warnBeforeLeave = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnBeforeLeave);
    return () => window.removeEventListener("beforeunload", warnBeforeLeave);
  }, [dirty, warnUnsaved]);

  const cancelAll = () => {
    if (!window.confirm(t("administration.settingsWorkspace.discardSettings"))) return;
    setDrafts(Object.fromEntries(response.settings.map(setting => [setting.key, settingDraftValue(setting, scope)])));
    setErrors({});
    setMessages({});
  };

  const save = async (setting: EffectiveSetting) => {
    setErrors(current => ({ ...current, [setting.key]: "" }));
    setMessages(current => ({ ...current, [setting.key]: "" }));
    try {
      if (setting.key === "workingCalendar.defaultCalendarId" && scope === "Organization"
        && drafts[setting.key] === INHERIT_TENANT_CALENDAR) {
        if (!setting.version) return;
        await resetSetting.mutateAsync({ key: setting.key, expectedVersion: setting.version });
        setMessages(current => ({ ...current, [setting.key]: t("administration.settingsWorkspace.settingReset") }));
        return;
      }
      await updateSetting.mutateAsync({ key: setting.key, value: drafts[setting.key], expectedVersion: setting.version ?? null });
      setMessages(current => ({ ...current, [setting.key]: t("administration.settingsWorkspace.settingSaved") }));
    } catch (error) {
      setErrors(current => ({ ...current, [setting.key]: extractErrorDetail(error) ?? t("administration.settingsWorkspace.settingSaveFailed") }));
    }
  };

  const reset = async (setting: EffectiveSetting) => {
    if (!setting.version) return;
    setErrors(current => ({ ...current, [setting.key]: "" }));
    setMessages(current => ({ ...current, [setting.key]: "" }));
    try {
      await resetSetting.mutateAsync({ key: setting.key, expectedVersion: setting.version });
      setMessages(current => ({ ...current, [setting.key]: t("administration.settingsWorkspace.settingReset") }));
    } catch (error) {
      setErrors(current => ({ ...current, [setting.key]: extractErrorDetail(error) ?? t("administration.settingsWorkspace.settingSaveFailed") }));
    }
  };

  return <div className="space-y-4">
    {response.settings.map(setting => {
      const label = settingLabel(setting.key, t);
      const value = Object.prototype.hasOwnProperty.call(drafts, setting.key) ? drafts[setting.key] : setting.value;
      const overrideSource = scope === "Platform" ? "PlatformOverride" : scope === "Tenant" ? "TenantOverride" : "OrganizationOverride";
      const canEdit = canWrite && setting.canOverride;
      const canReset = canEdit && setting.valueSource === overrideSource && Boolean(setting.version);
      const dirtyValue = !sameValue(value, settingDraftValue(setting, scope));
      return <div key={setting.key} className="grid gap-3 rounded-xl border bg-card p-4 lg:grid-cols-[minmax(12rem,1fr)_minmax(14rem,1fr)_minmax(10rem,auto)] lg:items-center">
        <div className="min-w-0">
          <p className="text-sm font-medium">{label}</p>
          <p className="mt-1 text-xs text-muted-foreground">{setting.validationDescription}</p>
          <p className="mt-2 inline-flex rounded-full bg-muted px-2.5 py-1 text-xs text-muted-foreground">{t(`administration.settingsWorkspace.${sourceKey(setting.valueSource)}`)}</p>
        </div>
        <div className="space-y-2">
          <SettingInput setting={setting} scope={scope} value={value}
            inheritedValue={inheritedValues && Object.prototype.hasOwnProperty.call(inheritedValues, setting.key) ? inheritedValues[setting.key] : setting.value}
            disabled={!canEdit || updateSetting.isPending || resetSetting.isPending
              || (setting.key === "regional.defaultCurrency" && (currencyOptions.isLoading || currencyOptions.isError))}
            currencies={currencyOptions.data ?? []}
            onChange={next => setDrafts(current => ({ ...current, [setting.key]: next }))} t={t} />
          {setting.key === "regional.defaultCurrency" && currencyOptions.isError
            && <p role="alert" className="text-xs text-destructive">{t("administration.settingsWorkspace.currencyLoadFailed")}</p>}
          {errors[setting.key] && <p role="alert" className="text-xs text-destructive">{errors[setting.key]}</p>}
          {messages[setting.key] && <p role="status" className="text-xs text-emerald-700">{messages[setting.key]}</p>}
        </div>
        <div className="flex flex-wrap gap-2 lg:justify-end">
          {canEdit && <>
            <Button type="button" variant="outline" size="sm" disabled={!dirtyValue || updateSetting.isPending || resetSetting.isPending} onClick={() => setDrafts(current => ({ ...current, [setting.key]: settingDraftValue(setting, scope) }))}>{t("administration.settingsWorkspace.cancel")}</Button>
            <Button type="button" size="sm" disabled={!dirtyValue || updateSetting.isPending || resetSetting.isPending} onClick={() => void save(setting)}>{updateSetting.isPending ? t("administration.settingsWorkspace.loading") : t("administration.settingsWorkspace.save")}</Button>
            {canReset && <Button type="button" variant="outline" size="sm" disabled={dirtyValue || resetSetting.isPending} onClick={() => void reset(setting)}>{t("administration.settingsWorkspace.resetInherited")}</Button>}
          </>}
        </div>
      </div>;
    })}
    {canWrite && dirty && <div className="flex justify-end border-t pt-4">
      <Button type="button" variant="outline" disabled={updateSetting.isPending || resetSetting.isPending} onClick={cancelAll}>{t("administration.settingsWorkspace.cancelAll")}</Button>
    </div>}
    {(!canWrite || !response.settings.some(setting => setting.canOverride)) && <p className="text-sm text-muted-foreground">{t("administration.settingsWorkspace.readOnly")}</p>}
  </div>;
}

function SettingInput({ setting, scope, value, inheritedValue, disabled, onChange, t, currencies }: {
  setting: EffectiveSetting;
  scope: SettingsScope;
  value: unknown;
  inheritedValue: unknown;
  disabled: boolean;
  onChange: (value: unknown) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
  currencies: Array<{ code: string; name: string; symbol: string }>;
}) {
  if (setting.key === "workingCalendar.defaultCalendarId") return <WorkingCalendarSettingInput setting={setting} scope={scope} value={value} inheritedValue={inheritedValue} disabled={disabled} onChange={onChange} t={t} />;
  const id = `setting-${setting.key.replaceAll(".", "-")}`;
  const raw = value === null || value === undefined ? "" : String(value);
  const choices: Record<string, Array<[string, string]>> = {
    "regional.defaultLanguage": [["en", t("administration.settingsWorkspace.english")], ["ar", t("administration.settingsWorkspace.arabic")]],
    "regional.defaultDateFormat": [["yyyy-MM-dd", "yyyy-MM-dd"], ["dd/MM/yyyy", "dd/MM/yyyy"], ["MM/dd/yyyy", "MM/dd/yyyy"]],
    "regional.defaultTimeFormat": [["HH:mm", "HH:mm"], ["hh:mm tt", "hh:mm tt"]],
    "regional.firstDayOfWeek": ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"].map(day => [day, t(`administration.settingsWorkspace.days.${day}`)] as [string, string]),
  };
  const options = choices[setting.key];
  if (setting.key === "regional.defaultCurrency") return <>
    <Label className="sr-only" htmlFor={id}>{settingLabel(setting.key, t)}</Label>
    <select id={id} value={raw} disabled={disabled} onChange={event => onChange(event.target.value || null)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60">
      <option value="">{t("administration.settingsWorkspace.noCurrency")}</option>
      {raw && !currencies.some(currency => currency.code === raw) && <option value={raw} disabled>{raw} ({t("administration.settingsWorkspace.currencyInactive")})</option>}
      {currencies.map(currency => <option key={currency.code} value={currency.code}>{currency.code} - {currency.name} ({currency.symbol})</option>)}
    </select>
  </>;
  if (options) return <>
    <Label className="sr-only" htmlFor={id}>{settingLabel(setting.key, t)}</Label>
    <select id={id} value={raw} disabled={disabled} onChange={event => onChange(event.target.value)} className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60">
      {options.map(([option, label]) => <option key={option} value={option}>{label}</option>)}
    </select>
  </>;
  if (setting.type === "Boolean") return <label className="flex min-h-10 items-center gap-2 text-sm">
    <input id={id} type="checkbox" checked={Boolean(value)} disabled={disabled} onChange={event => onChange(event.target.checked)} />
    <span>{settingLabel(setting.key, t)}</span>
  </label>;
  return <>
    <Label className="sr-only" htmlFor={id}>{settingLabel(setting.key, t)}</Label>
    <Input id={id}
      type={setting.type === "Integer" || setting.type === "Decimal" ? "number" : "text"}
      value={raw}
      disabled={disabled}
      maxLength={setting.key === "regional.defaultCurrency" ? 3 : undefined}
      min={setting.type === "Integer" ? 1 : undefined}
      step={setting.type === "Decimal" ? "any" : "1"}
      placeholder={setting.key === "regional.defaultCurrency" ? t("administration.settingsWorkspace.currencyCodePlaceholder") : undefined}
      onChange={event => onChange(setting.type === "Integer" ? (event.target.value === "" ? null : Number.parseInt(event.target.value, 10))
        : setting.type === "Decimal" ? (event.target.value === "" ? null : Number.parseFloat(event.target.value)) : event.target.value)} />
  </>;
}

function settingLabel(key: string, t: (key: string) => string) {
  const labels: Record<string, string> = {
    "regional.defaultLanguage": t("administration.settingsWorkspace.language"),
    "regional.defaultTimeZone": t("administration.settingsWorkspace.timeZone"),
    "regional.defaultCurrency": t("administration.settingsWorkspace.currency"),
    "regional.defaultDateFormat": t("administration.settingsWorkspace.dateFormat"),
    "regional.defaultTimeFormat": t("administration.settingsWorkspace.timeFormat"),
    "regional.firstDayOfWeek": t("administration.settingsWorkspace.firstDay"),
    "workingCalendar.defaultCalendarId": t("administration.settingsWorkspace.defaultWorkingCalendar"),
  };
  return labels[key] ?? key;
}

const INHERIT_TENANT_CALENDAR = "__inherit_tenant_calendar__";

function settingDraftValue(setting: EffectiveSetting, scope: SettingsScope): unknown {
  if (setting.key === "workingCalendar.defaultCalendarId" && scope === "Organization"
    && (setting.isInherited || setting.value === null || setting.value === undefined)) return INHERIT_TENANT_CALENDAR;
  return setting.value;
}

function WorkingCalendarSettingInput({ setting, scope, value, inheritedValue, disabled, onChange, t }: {
  setting: EffectiveSetting;
  scope: SettingsScope;
  value: unknown;
  inheritedValue: unknown;
  disabled: boolean;
  onChange: (value: unknown) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const calendars = useBaseCalendars();
  const id = `setting-${setting.key.replaceAll(".", "-")}`;
  const selectedValue = value === null || value === undefined ? "" : String(value);
  const configured = (calendars.data ?? []).filter(calendar => calendar.isConfigured);
  const isOrganization = scope === "Organization";
  const inheritedId = inheritedValue === null || inheritedValue === undefined ? "" : String(inheritedValue);
  const inheritedCalendar = (calendars.data ?? []).find(calendar => calendar.id === inheritedId);
  const inheritedCalendarLabel = inheritedCalendar?.name ?? (inheritedId
    ? t("administration.settingsWorkspace.calendarUnavailable", { id: inheritedId })
    : t("administration.settingsWorkspace.noCalendarSelected"));
  const inheritLabel = t("administration.settingsWorkspace.inheritTenantCalendar", { calendar: inheritedCalendarLabel });
  const currentCalendar = (calendars.data ?? []).find(calendar => calendar.id === selectedValue);
  const currentCalendarUnavailable = Boolean(selectedValue && selectedValue !== INHERIT_TENANT_CALENDAR
    && !configured.some(calendar => calendar.id === selectedValue));

  return <>
    <Label className="sr-only" htmlFor={id}>{settingLabel(setting.key, t)}</Label>
    <select
      id={id}
      value={selectedValue}
      disabled={disabled || calendars.isLoading || calendars.isError}
      onChange={event => onChange(event.target.value === INHERIT_TENANT_CALENDAR
        ? INHERIT_TENANT_CALENDAR
        : event.target.value || null)}
      className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
    >
      {isOrganization
        ? <option value={INHERIT_TENANT_CALENDAR}>{inheritLabel}</option>
        : <option value="">{t("administration.settingsWorkspace.noCalendarSelected")}</option>}
      {currentCalendarUnavailable && selectedValue !== INHERIT_TENANT_CALENDAR
        ? <option value={selectedValue} disabled>{currentCalendar?.name ?? t("administration.settingsWorkspace.calendarUnavailable", { id: selectedValue })}{currentCalendar && !currentCalendar.isConfigured ? ` (${t("administration.settingsWorkspace.calendarNotConfigured")})` : ""}</option>
        : null}
      {configured.map((calendar: ResourceBaseCalendarDto) => <option key={calendar.id} value={calendar.id}>{calendar.name}</option>)}
    </select>
    {calendars.isLoading ? <p className="text-xs text-muted-foreground">{t("administration.settingsWorkspace.loading")}</p> : null}
    {calendars.isError ? <div className="flex items-center justify-between gap-2"><p role="alert" className="text-xs text-destructive">{t("administration.settingsWorkspace.workingCalendarsLoadFailed")}</p><Button type="button" size="sm" variant="outline" onClick={() => void calendars.refetch()}>{t("administration.settingsWorkspace.retry")}</Button></div> : null}
  </>;
}

function sourceKey(source: EffectiveSetting["valueSource"]) {
  return source === "OrganizationOverride" ? "sourceOrganization"
    : source === "TenantOverride" ? "sourceTenant"
      : source === "PlatformDefault" ? "sourcePlatform"
        : source === "PlatformOverride" ? "sourcePlatformOverride" : "sourceDefinition";
}

function PersistenceNotice() {
  const { t } = useI18n();
  return <div role="status" className="flex gap-3 rounded-xl border border-amber-500/30 bg-amber-500/5 p-4 text-sm">
    <Globe2 className="mt-0.5 h-4 w-4 shrink-0 text-amber-700" />
    <div><p className="font-semibold">{t("administration.settingsWorkspace.persistenceUnavailableTitle")}</p><p className="mt-1 text-muted-foreground">{t("administration.settingsWorkspace.persistenceUnavailable")}</p></div>
  </div>;
}

function ProfileField({
  field, label, value, errors, disabled, onChange, type = "text", required = false,
}: {
  field: string; label: string; value: string; errors: Record<string, string[]>; disabled: boolean;
  onChange: (value: string) => void; type?: string; required?: boolean;
}) {
  const id = `organization-${field}`;
  const fieldErrors = errors[field] ?? errors[capitalize(field)];
  return <div className="space-y-2">
    <Label htmlFor={id}>{label}</Label>
    <Input id={id} type={type} value={value} disabled={disabled} required={required} onChange={event => onChange(event.target.value)} aria-invalid={Boolean(fieldErrors?.length)} aria-describedby={fieldErrors?.length ? `${id}-errors` : undefined} />
    <FieldErrors id={`${id}-errors`} errors={fieldErrors} />
  </div>;
}

function FieldErrors({ errors, id }: { errors?: string[]; id?: string }) {
  if (!errors?.length) return null;
  return <ul id={id} role="alert" className="space-y-1 text-xs text-destructive">{errors.map((error, index) => <li key={index}>{error}</li>)}</ul>;
}

function LoadingState() {
  const { t } = useI18n();
  return <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("administration.settingsWorkspace.loading")}</CardContent></Card>;
}

function ErrorState() {
  const { t } = useI18n();
  return <Card><CardContent className="p-6 text-sm text-destructive" role="alert">{t("administration.settingsWorkspace.loadError")}</CardContent></Card>;
}

function toDraft(profile: OrganizationProfile): ProfileDraft {
  return {
    name: profile.name ?? "",
    description: profile.description ?? "",
    website: profile.website ?? "",
    email: profile.email ?? "",
    phone: profile.phone ?? "",
    addressLine1: profile.addressLine1 ?? "",
    addressLine2: profile.addressLine2 ?? "",
    city: profile.city ?? "",
    country: profile.country ?? "",
    postcode: profile.postcode ?? "",
  };
}

function sameDraft(left: ProfileDraft, right: ProfileDraft): boolean {
  return Object.keys(left).every(key => left[key as keyof ProfileDraft] === right[key as keyof ProfileDraft]);
}

function sameValue(left: unknown, right: unknown): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function extractValidationErrors(error: unknown): Record<string, string[]> {
  return getApiFieldErrors(error);
}

function extractErrorDetail(error: unknown): string | null {
  return error instanceof ApiError ? error.summary : null;
}

function capitalize(value: string) {
  return value.length ? value[0].toUpperCase() + value.slice(1) : value;
}

function useUnsavedNavigationWarning(enabled: boolean, message: string) {
  React.useEffect(() => {
    if (!enabled) return;
    const onClick = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const target = event.target;
      if (!(target instanceof Element)) return;
      const link = target.closest<HTMLAnchorElement>("a[href]");
      if (!link || link.target === "_blank" || link.hasAttribute("download")) return;
      const destination = new URL(link.href, window.location.href);
      if (destination.origin !== window.location.origin || destination.pathname === window.location.pathname) return;
      if (!window.confirm(message)) {
        event.preventDefault();
        event.stopPropagation();
      }
    };
    document.addEventListener("click", onClick, true);
    return () => document.removeEventListener("click", onClick, true);
  }, [enabled, message]);
}
