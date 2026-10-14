import { Link, Outlet } from "react-router-dom";
import { ArrowUpRight, Bell, LockKeyhole, Monitor, Save, Settings2, UserRound } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useTheme } from "@theme/ThemeProvider";
import { isFirebaseMessagingConfigured, isPushDeviceRegistered, enablePushNotifications, deactivateRegisteredPushDevice, type PushRegistrationStatus } from "@lib/firebase-messaging";
import { useNotificationPreferences, useUpdateNotificationPreferences } from "@api/notifications";
import * as React from "react";

export function AccountHub() {
  const { t } = useI18n();
  const { user } = useAuth();
  const items = [
    { id: "profile", href: "/account/profile", icon: UserRound, title: t("account.cards.profile.title"), description: t("account.cards.profile.description") },
    { id: "preferences", href: "/account/preferences", icon: Settings2, title: t("account.cards.preferences.title"), description: t("account.cards.preferences.description") },
    { id: "notifications", href: "/account/notifications", icon: Bell, title: t("account.cards.notifications.title"), description: t("account.cards.notifications.description") },
    { id: "security", href: "/account/security", icon: LockKeyhole, title: t("account.cards.security.title"), description: t("account.cards.security.description") },
  ];
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.title")} description={t("account.description")} />
      <Card><CardContent className="flex items-center gap-4 p-5">
        <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-lg font-semibold text-primary">{user?.fullName?.slice(0, 1).toUpperCase() ?? "U"}</div>
        <div className="min-w-0"><p className="truncate text-base font-semibold">{user?.fullName ?? t("account.unknownUser")}</p><p className="truncate text-sm text-muted-foreground">{user?.email ?? ""}</p></div>
      </CardContent></Card>
      <div className="grid gap-4 sm:grid-cols-2">
        {items.map(({ id, href, icon: Icon, title, description }) => (
          <Link key={id} to={href} className="group">
            <Card className="h-full group-hover:border-primary/30">
              <CardHeader><div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="h-4 w-4" /></div><CardTitle className="text-base">{title}</CardTitle></div><CardDescription className="mt-2 text-xs leading-5">{description}</CardDescription></CardHeader>
              <CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("account.open")}</span><ArrowUpRight className="h-4 w-4" /></div></CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}

export function AccountLayout() {
  return <Outlet />;
}

export function ProfilePage() {
  const { t } = useI18n();
  const { user } = useAuth();
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.profile.title")} description={t("account.profile.description")} />
      <Card><CardContent className="space-y-2 p-6 text-sm">
        <p><strong>{t("account.profile.name")}:</strong> {user?.fullName}</p>
        <p><strong>{t("account.profile.email")}:</strong> {user?.email}</p>
        <p><strong>{t("account.profile.jobTitle")}:</strong> {user?.jobTitle ?? "—"}</p>
        <p><strong>{t("account.profile.roles")}:</strong> {(user?.roles ?? []).map((r) => r.name).join(", ") || "—"}</p>
      </CardContent></Card>
    </div>
  );
}

export function PreferencesPage() {
  const { t } = useI18n();
  const { theme, setTheme } = useTheme();
  const { locale, setLocale } = useI18n();
  const [compactMode, setCompactMode] = React.useState(false);
  const [saved, setSaved] = React.useState(false);
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.preferences.title")} description={t("account.preferences.description")} actions={<Button size="sm" onClick={() => { setSaved(true); setTimeout(() => setSaved(false), 2000); }}><Save className="me-2 h-4 w-4" />{t("account.preferences.save")}</Button>} />
      <Card>
        <CardHeader><div className="flex items-start gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Monitor className="h-4 w-4" /></div><div><CardTitle className="text-base">{t("account.preferences.displayTitle")}</CardTitle><CardDescription className="mt-1 text-xs leading-5">{t("account.preferences.displayDescription")}</CardDescription></div></div></CardHeader>
        <CardContent className="space-y-5">
          <div className="space-y-2">
            <Label htmlFor="account-theme">{t("account.preferences.theme")}</Label>
            <select id="account-theme" className="input-field max-w-xs" value={theme} onChange={(e) => setTheme(e.target.value as "light" | "dark" | "system")}>
              <option value="light">{t("account.preferences.light")}</option>
              <option value="dark">{t("account.preferences.dark")}</option>
              <option value="system">{t("account.preferences.system")}</option>
            </select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="account-locale">{t("nav.language")}</Label>
            <select id="account-locale" className="input-field max-w-xs" value={locale} onChange={(e) => setLocale(e.target.value as "en" | "ar")}>
              <option value="en">English</option>
              <option value="ar">العربية</option>
            </select>
          </div>
          <div className="flex items-center justify-between gap-4 border-t pt-4">
            <div><Label htmlFor="account-compact-mode">{t("account.preferences.compactMode")}</Label><p className="mt-1 text-xs leading-5 text-muted-foreground">{t("account.preferences.compactModeDescription")}</p></div>
            <button id="account-compact-mode" type="button" role="switch" aria-label={t("account.preferences.compactMode")} aria-checked={compactMode} onClick={() => setCompactMode((current) => !current)} className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${compactMode ? "bg-primary" : "bg-muted"}`}>
              <span className={`inline-block h-4 w-4 rounded-full bg-background shadow transition-transform ${compactMode ? "translate-x-6" : "translate-x-1"}`} />
            </button>
          </div>
          {saved ? <p role="status" className="text-sm text-success">{t("common.saved")}</p> : null}
        </CardContent>
      </Card>
    </div>
  );
}

export function SecurityPage() {
  const { t } = useI18n();
  const { user } = useAuth();
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.security.title")} description={t("account.security.description")} />
      <Card><CardContent className="space-y-2 p-6 text-sm">
        <p><strong>{t("account.security.signedInAs")}:</strong> {user?.email}</p>
        <p className="text-muted-foreground">{t("account.security.cardDescription")}</p>
      </CardContent></Card>
    </div>
  );
}

export function AccountNotificationsPage() {
  const { t } = useI18n();
  const preferences = useNotificationPreferences(true);
  const updatePreferences = useUpdateNotificationPreferences();
  const [busyType, setBusyType] = React.useState<string | null>(null);
  const [messageKey, setMessageKey] = React.useState<string | null>(null);
  const [pushStatus, setPushStatus] = React.useState<PushRegistrationStatus | "permissionRequired" | "notEnabled" | null>(() => {
    if (!isFirebaseMessagingConfigured()) return "unconfigured";
    if (typeof window === "undefined" || !("Notification" in window)) return "unsupported";
    if (Notification.permission === "denied") return "denied";
    return isPushDeviceRegistered() ? "enabled" : Notification.permission === "default" ? "permissionRequired" : "notEnabled";
  });

  const changePreference = async (type: string, channel: "inAppEnabled" | "pushEnabled", enabled: boolean) => {
    const current = preferences.data?.items;
    if (!current) return;
    setBusyType(type);
    setMessageKey(null);
    try {
      if (channel === "pushEnabled" && enabled) {
        if (!preferences.data?.pushAvailable || !isFirebaseMessagingConfigured()) {
          setPushStatus("unconfigured");
          setMessageKey("account.notifications.pushUnavailable");
          return;
        }
        const registration = await enablePushNotifications();
        setPushStatus(registration.status);
        if (registration.status !== "enabled") {
          setMessageKey("account.notifications.pushStatus");
          return;
        }
      }
      const next = current.map(item => item.type === type ? { ...item, [channel]: enabled } : item);
      await updatePreferences.mutateAsync(next);
      if (channel === "pushEnabled" && !enabled && !next.some(item => item.pushEnabled)) {
        await deactivateRegisteredPushDevice();
        setPushStatus("notEnabled");
      }
      setMessageKey("account.notifications.saved");
    } catch {
      setMessageKey("account.notifications.saveFailed");
    } finally {
      setBusyType(null);
    }
  };

  const eventLabels: Record<string, string> = {
    ProjectHandover: t("account.notifications.projectHandover"),
    ProgramHandover: t("account.notifications.programHandover"),
    PortfolioHandover: t("account.notifications.portfolioHandover"),
  };
  const pushUnavailable = !preferences.data?.pushAvailable || !isFirebaseMessagingConfigured();
  const messageIsError = messageKey === "account.notifications.pushUnavailable"
    || messageKey?.endsWith("Failed") === true
    || messageKey?.endsWith("Status") === true;
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.notifications.title")} description={t("account.notifications.description")} />
      <Card>
        <CardHeader><CardTitle className="text-base">{t("account.notifications.deliveryTitle")}</CardTitle><CardDescription>{t("account.notifications.deliveryDescription")}</CardDescription></CardHeader>
        <CardContent className="space-y-5">
          {preferences.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
            : preferences.isError ? <p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p>
            : <>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[560px] text-sm">
                  <thead><tr className="border-b text-start text-muted-foreground"><th className="py-2 font-medium">{t("account.notifications.event")}</th><th className="px-3 py-2 text-center font-medium">{t("account.notifications.inApp")}</th><th className="px-3 py-2 text-center font-medium">{t("account.notifications.push")}</th><th className="px-3 py-2 text-center font-medium">{t("account.notifications.email")}</th></tr></thead>
                  <tbody>{preferences.data?.items.map(item => <tr key={item.type} className="border-b last:border-0">
                    <th scope="row" className="py-3 text-start font-medium">{eventLabels[item.type] ?? item.type}</th>
                    <td className="px-3 py-3 text-center"><input type="checkbox" aria-label={`${eventLabels[item.type] ?? item.type} ${t("account.notifications.inApp")}`} checked={item.inAppEnabled} disabled={busyType === item.type || updatePreferences.isPending} onChange={event => void changePreference(item.type, "inAppEnabled", event.target.checked)} /></td>
                    <td className="px-3 py-3 text-center"><input type="checkbox" aria-label={`${eventLabels[item.type] ?? item.type} ${t("account.notifications.push")}`} checked={item.pushEnabled} disabled={busyType === item.type || updatePreferences.isPending} onChange={event => void changePreference(item.type, "pushEnabled", event.target.checked)} /></td>
                    <td className="px-3 py-3 text-center"><input type="checkbox" aria-label={`${eventLabels[item.type] ?? item.type} ${t("account.notifications.email")}`} checked={item.emailEnabled} disabled title={t("account.notifications.emailUnavailable")} /></td>
                  </tr>)}</tbody>
                </table>
              </div>
              <p className="text-xs text-muted-foreground">{t(`account.notifications.pushStates.${pushUnavailable ? "unconfigured" : pushStatus ?? "notEnabled"}`)}</p>
              <p className="text-xs text-muted-foreground">{t("account.notifications.emailUnavailable")}</p>
              {messageKey ? <p role={messageIsError ? "alert" : "status"} className={`text-sm ${messageIsError ? "text-destructive" : "text-success"}`}>{t(messageKey)}</p> : null}
            </>}
          <Link to="/notifications" className="inline-block text-sm text-primary hover:underline">{t("account.notifications.openCentre")}</Link>
        </CardContent>
      </Card>
    </div>
  );
}
