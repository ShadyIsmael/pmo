import { Link, Outlet, useNavigate } from "react-router-dom";
import { ArrowUpRight, Bell, BriefcaseBusiness, KeyRound, LockKeyhole, Monitor, ShieldCheck, Trash2, UserRound, UsersRound } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { EmptyState } from "@components/ui/StateDisplay";
import { ApiError } from "@api/client";
import { useAccount, useChangeAccountPassword, useRevokeAccountSession, useRevokeOtherAccountSessions, useUpdateAccountProfile } from "@api/account";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useTheme } from "@theme/ThemeProvider";
import { isFirebaseMessagingConfigured, isPushDeviceRegistered, enablePushNotifications, deactivateRegisteredPushDevice, type PushRegistrationStatus } from "@lib/firebase-messaging";
import { useNotificationPreferences, useUpdateNotificationPreferences } from "@api/notifications";
import * as React from "react";

function AccountLoading() {
  const { t } = useI18n();
  return <div className="space-y-3" role="status" aria-label={t("common.loading")}><div className="h-24 animate-pulse rounded-xl bg-muted" /><div className="h-56 animate-pulse rounded-xl bg-muted" /></div>;
}

function AccountLoadError({ retry }: { retry: () => void }) {
  const { t } = useI18n();
  return <Card><CardContent className="flex flex-col items-start gap-3 p-6"><p role="alert" className="text-sm text-destructive">{t("account.loadFailed")}</p><Button variant="outline" size="sm" onClick={retry}>{t("common.retry")}</Button></CardContent></Card>;
}

export function AccountHub() {
  const { t } = useI18n();
  const account = useAccount();
  const items = [
    { id: "profile", href: "/account/profile", icon: UserRound, title: t("account.cards.profile.title"), description: t("account.cards.profile.description") },
    { id: "memberships", href: "/account/memberships", icon: UsersRound, title: t("account.cards.memberships.title"), description: t("account.cards.memberships.description") },
    { id: "security", href: "/account/security", icon: LockKeyhole, title: t("account.cards.security.title"), description: t("account.cards.security.description") },
    { id: "preferences", href: "/account/preferences", icon: Monitor, title: t("account.cards.preferences.title"), description: t("account.cards.preferences.description") },
    { id: "notifications", href: "/account/notifications", icon: Bell, title: t("account.cards.notifications.title"), description: t("account.cards.notifications.description") },
  ];
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.title")} description={t("account.description")} />
      {account.isLoading ? <AccountLoading /> : account.isError || !account.data ? <AccountLoadError retry={() => void account.refetch()} /> : <>
        <Card className="overflow-hidden"><CardContent className="flex items-center gap-4 p-5 sm:p-6">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xl font-semibold text-primary">{account.data.profile.firstName.slice(0, 1).toUpperCase()}</div>
          <div className="min-w-0"><p className="truncate text-lg font-semibold">{account.data.profile.fullName}</p><p className="truncate text-sm text-muted-foreground">{account.data.profile.email}</p></div>
          <span className="ms-auto hidden rounded-full bg-muted px-3 py-1 text-xs text-muted-foreground sm:inline-flex">{account.data.tenant?.name ?? t("account.tenantUnavailable")}</span>
        </CardContent></Card>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {items.map(({ id, href, icon: Icon, title, description }) => (
            <Link key={id} to={href} className="group focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2">
              <Card className="h-full transition-colors group-hover:border-primary/30"><CardHeader><div className="flex items-center gap-3"><div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-4 w-4" /></div><CardTitle className="text-base">{title}</CardTitle></div><CardDescription className="mt-2 text-xs leading-5">{description}</CardDescription></CardHeader><CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("account.open")}</span><ArrowUpRight className="h-4 w-4" /></div></CardContent></Card>
            </Link>
          ))}
        </div>
      </>}
    </div>
  );
}

export function AccountLayout() { return <Outlet />; }

export function ProfilePage() {
  const { t, formatDate } = useI18n();
  const account = useAccount();
  const { refreshUser } = useAuth();
  const update = useUpdateAccountProfile();
  const [firstName, setFirstName] = React.useState("");
  const [lastName, setLastName] = React.useState("");
  const [message, setMessage] = React.useState<{ error: boolean; text: string } | null>(null);
  React.useEffect(() => {
    if (!account.data) return;
    setFirstName(account.data.profile.firstName);
    setLastName(account.data.profile.lastName);
  }, [account.data?.profile.firstName, account.data?.profile.lastName]);

  const saveProfile = async (event: React.FormEvent) => {
    event.preventDefault();
    setMessage(null);
    try {
      await update.mutateAsync({ firstName: firstName.trim(), lastName: lastName.trim() });
      await refreshUser();
      setMessage({ error: false, text: t("account.profile.saved") });
    } catch (error) {
      setMessage({ error: true, text: error instanceof ApiError ? error.message : t("account.profile.saveFailed") });
    }
  };

  if (account.isLoading) return <AccountLoading />;
  if (account.isError || !account.data) return <AccountLoadError retry={() => void account.refetch()} />;
  const { profile, professionalProfile } = account.data;
  return <div className="space-y-6">
    <PageHeader title={t("account.profile.title")} description={t("account.profile.description")} />
    <Card><CardHeader><CardTitle className="text-base">{t("account.profile.personalTitle")}</CardTitle><CardDescription>{t("account.profile.personalDescription")}</CardDescription></CardHeader><CardContent>
      <form onSubmit={saveProfile} className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2"><Label htmlFor="account-first-name">{t("account.profile.firstName")}</Label><Input id="account-first-name" autoComplete="given-name" maxLength={100} required value={firstName} onChange={event => setFirstName(event.target.value)} /></div>
        <div className="space-y-2"><Label htmlFor="account-last-name">{t("account.profile.lastName")}</Label><Input id="account-last-name" autoComplete="family-name" maxLength={100} required value={lastName} onChange={event => setLastName(event.target.value)} /></div>
        <div className="space-y-2 sm:col-span-2"><Label htmlFor="account-email">{t("account.profile.email")}</Label><Input id="account-email" type="email" autoComplete="email" value={profile.email} readOnly aria-describedby="account-email-note" className="max-w-2xl bg-muted/40" /><p id="account-email-note" className="text-xs text-muted-foreground">{t("account.profile.emailReadOnly")}</p></div>
        {message ? <p role={message.error ? "alert" : "status"} className={`text-sm sm:col-span-2 ${message.error ? "text-destructive" : "text-success"}`}>{message.text}</p> : null}
        <div className="sm:col-span-2"><Button type="submit" disabled={update.isPending || (firstName.trim() === profile.firstName && lastName.trim() === profile.lastName)}>{update.isPending ? t("account.profile.saving") : t("account.profile.save")}</Button></div>
      </form>
    </CardContent></Card>
    <Card><CardHeader><div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-xl bg-muted text-muted-foreground"><BriefcaseBusiness className="h-4 w-4" /></div><div><CardTitle className="text-base">{t("account.profile.professionalTitle")}</CardTitle><CardDescription>{t("account.profile.professionalDescription")}</CardDescription></div></div></CardHeader><CardContent>
      {professionalProfile ? <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
        {[
          [t("account.profile.jobTitle"), professionalProfile.jobTitle], [t("account.profile.employeeId"), professionalProfile.employeeId],
          [t("account.profile.employmentType"), professionalProfile.employmentType], [t("account.profile.seniority"), professionalProfile.seniority],
          [t("account.profile.resourceType"), professionalProfile.type], [t("account.profile.resourceStatus"), professionalProfile.status],
          [t("account.profile.location"), professionalProfile.location],
          [t("account.profile.dateOfJoining"), professionalProfile.dateOfJoining ? formatDate(professionalProfile.dateOfJoining) : null],
        ].map(([label, value]) => <div key={label}><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 text-sm font-medium">{value || "—"}</dd></div>)}
      </dl> : <EmptyState icon={<BriefcaseBusiness className="h-5 w-5" />} title={t("account.profile.noProfessionalProfile")} description={t("account.profile.noProfessionalProfileDescription")} />}
    </CardContent></Card>
  </div>;
}

export function MembershipsPage() {
  const { t, formatDate } = useI18n();
  const account = useAccount();
  if (account.isLoading) return <AccountLoading />;
  if (account.isError || !account.data) return <AccountLoadError retry={() => void account.refetch()} />;
  const data = account.data;
  const hasMemberships = Boolean(data.tenant || data.organizationAccess.length || data.organizationalUnits.length || data.teams.length || data.projects.length);
  return <div className="space-y-6">
    <PageHeader title={t("account.memberships.title")} description={t("account.memberships.description")} />
    {!hasMemberships ? <EmptyState icon={<UsersRound className="h-5 w-5" />} title={t("account.memberships.emptyTitle")} description={t("account.memberships.emptyDescription")} /> : null}
    {data.tenant ? <Card><CardHeader><CardTitle className="text-base">{t("account.memberships.tenant")}</CardTitle></CardHeader><CardContent><p className="font-medium">{data.tenant.name}</p><p className="mt-1 text-sm text-muted-foreground">{data.tenant.code} · {data.tenant.status}</p></CardContent></Card> : null}
    {data.organizationAccess.length ? <Card><CardHeader><CardTitle className="text-base">{t("account.memberships.organizationAccess")}</CardTitle><CardDescription>{t("account.memberships.organizationAccessDescription")}</CardDescription></CardHeader><CardContent className="grid gap-3 md:grid-cols-2">{data.organizationAccess.map(item => <div key={item.organizationId} className="rounded-lg border p-4"><p className="font-medium">{item.name}</p><p className="mt-2 text-xs text-muted-foreground">{item.code || "—"} · {item.canManageSettings ? t("account.memberships.canManage") : item.canReadSettings ? t("account.memberships.canRead") : t("account.memberships.noSettingsAccess")}</p></div>)}</CardContent></Card> : null}
    {data.organizationalUnits.length ? <Card><CardHeader><CardTitle className="text-base">{t("account.memberships.orgUnits")}</CardTitle><CardDescription>{t("account.memberships.resourceRelationship")}</CardDescription></CardHeader><CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{data.organizationalUnits.map(item => <div key={item.organizationalUnitId} className="rounded-lg border p-4"><p className="font-medium">{item.name}</p><p className="mt-1 text-xs text-muted-foreground">{item.type}{item.isPrimary ? ` · ${t("account.memberships.primary")}` : ""}</p></div>)}</CardContent></Card> : null}
    {data.teams.length ? <Card><CardHeader><CardTitle className="text-base">{t("account.memberships.teams")}</CardTitle><CardDescription>{t("account.memberships.resourceRelationship")}</CardDescription></CardHeader><CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{data.teams.map(item => <div key={item.teamId} className="rounded-lg border p-4"><p className="font-medium">{item.name}</p><p className="mt-1 text-xs text-muted-foreground">{item.code || "—"} · {t("account.memberships.since", { date: formatDate(item.effectiveFrom) })}</p></div>)}</CardContent></Card> : null}
    {data.projects.length ? <Card><CardHeader><CardTitle className="text-base">{t("account.memberships.projects")}</CardTitle><CardDescription>{t("account.memberships.projectRelationship")}</CardDescription></CardHeader><CardContent className="space-y-3">{data.projects.map((item, index) => <div key={`${item.projectId}-${item.assignmentSource}-${index}`} className="rounded-lg border p-4"><div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium">{item.projectName}</p><p className="mt-1 text-xs text-muted-foreground">{item.projectCode} · {item.projectStatus}</p></div><span className="rounded-full bg-muted px-2.5 py-1 text-xs">{item.assignmentSource === "Direct resource" ? t("account.memberships.directResource") : t("account.memberships.teamDerived")}</span></div><div className="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-sm"><span><span className="text-muted-foreground">{t("account.memberships.role")}:</span> {item.role}</span><span><span className="text-muted-foreground">{t("account.memberships.allocation")}:</span> {item.allocationPercentage}%</span></div>{item.functionalRoles.length ? <p className="mt-2 text-xs text-muted-foreground">{t("account.memberships.functionalRoles")}: {item.functionalRoles.map(role => role.nameAr && document.documentElement.lang === "ar" ? role.nameAr : role.nameEn).join(", ")}</p> : null}</div>)}</CardContent></Card> : null}
  </div>;
}

export function PreferencesPage() {
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme } = useTheme();
  const [saved, setSaved] = React.useState(false);
  const saveLocalPreference = (operation: () => void) => {
    operation();
    setSaved(true);
    window.setTimeout(() => setSaved(false), 1800);
  };
  return <div className="space-y-6">
    <PageHeader title={t("account.preferences.title")} description={t("account.preferences.description")} />
    <Card><CardHeader><div className="flex items-start gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Monitor className="h-4 w-4" /></div><div><CardTitle className="text-base">{t("account.preferences.displayTitle")}</CardTitle><CardDescription className="mt-1 text-xs leading-5">{t("account.preferences.displayDescription")}</CardDescription></div></div></CardHeader><CardContent className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2"><Label htmlFor="account-theme">{t("account.preferences.theme")}</Label><select id="account-theme" className="input-field w-full max-w-sm" value={theme} onChange={event => saveLocalPreference(() => setTheme(event.target.value as "light" | "dark" | "system"))}><option value="light">{t("account.preferences.light")}</option><option value="dark">{t("account.preferences.dark")}</option><option value="system">{t("account.preferences.system")}</option></select></div>
        <div className="space-y-2"><Label htmlFor="account-locale">{t("nav.language")}</Label><select id="account-locale" className="input-field w-full max-w-sm" value={locale} onChange={event => saveLocalPreference(() => setLocale(event.target.value as "en" | "ar"))}><option value="en">English</option><option value="ar">العربية</option></select></div>
      </div>
      <div className="rounded-lg border border-dashed p-4"><p className="text-sm font-medium">{t("account.preferences.compactMode")}</p><p className="mt-1 text-sm text-muted-foreground">{t("account.preferences.compactUnavailable")}</p></div>
      {saved ? <p role="status" className="text-sm text-success">{t("account.preferences.savedLocally")}</p> : null}
      <p className="text-xs text-muted-foreground">{t("account.preferences.browserOnly")}</p>
    </CardContent></Card>
  </div>;
}

export function SecurityPage() {
  const { t, formatDateTime } = useI18n();
  const account = useAccount();
  const changePassword = useChangeAccountPassword();
  const revokeSession = useRevokeAccountSession();
  const revokeOthers = useRevokeOtherAccountSessions();
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = React.useState("");
  const [newPassword, setNewPassword] = React.useState("");
  const [confirmPassword, setConfirmPassword] = React.useState("");
  const [message, setMessage] = React.useState<{ error: boolean; text: string } | null>(null);

  const submitPasswordChange = async (event: React.FormEvent) => {
    event.preventDefault();
    setMessage(null);
    if (newPassword !== confirmPassword) { setMessage({ error: true, text: t("account.security.passwordMismatch") }); return; }
    try {
      await changePassword.mutateAsync({ currentPassword, newPassword });
      await logout();
      navigate("/login", { replace: true });
    } catch (error) {
      setMessage({ error: true, text: error instanceof ApiError ? error.message : t("account.security.passwordChangeFailed") });
    }
  };

  const revokeOne = async (id: string, isCurrent: boolean) => {
    setMessage(null);
    try {
      await revokeSession.mutateAsync(id);
      if (isCurrent) { await logout(); navigate("/login", { replace: true }); }
      else setMessage({ error: false, text: t("account.security.sessionRevoked") });
    } catch (error) { setMessage({ error: true, text: error instanceof ApiError ? error.message : t("account.security.sessionActionFailed") }); }
  };

  const revokeOtherSessions = async () => {
    setMessage(null);
    try {
      const result = await revokeOthers.mutateAsync();
      setMessage({ error: false, text: t("account.security.otherSessionsRevoked", { count: result.revokedCount }) });
    } catch (error) { setMessage({ error: true, text: error instanceof ApiError ? error.message : t("account.security.sessionActionFailed") }); }
  };

  if (account.isLoading) return <AccountLoading />;
  if (account.isError || !account.data) return <AccountLoadError retry={() => void account.refetch()} />;
  const data = account.data;
  return <div className="space-y-6">
    <PageHeader title={t("account.security.title")} description={t("account.security.description")} />
    <Card><CardHeader><div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary/10 text-primary"><ShieldCheck className="h-4 w-4" /></div><div><CardTitle className="text-base">{t("account.security.summary")}</CardTitle><CardDescription>{data.profile.email}</CardDescription></div></div></CardHeader><CardContent className="grid gap-4 text-sm sm:grid-cols-3"><div><p className="text-xs text-muted-foreground">{t("account.security.signInMethod")}</p><p className="mt-1 font-medium">{data.profile.authenticationSource}</p></div><div><p className="text-xs text-muted-foreground">{t("account.security.passwordChanged")}</p><p className="mt-1 font-medium">{data.security.passwordChangedAt ? formatDateTime(data.security.passwordChangedAt) : "—"}</p></div><div><p className="text-xs text-muted-foreground">{t("account.security.lastSignIn")}</p><p className="mt-1 font-medium">{data.security.lastLoginAt ? formatDateTime(data.security.lastLoginAt) : "—"}</p></div></CardContent></Card>
    {data.security.canChangePassword ? <Card><CardHeader><CardTitle className="text-base">{t("account.security.changePassword")}</CardTitle><CardDescription>{t("account.security.passwordPolicyNotice")}</CardDescription></CardHeader><CardContent><form onSubmit={submitPasswordChange} className="grid max-w-xl gap-4">
      <div className="space-y-2"><Label htmlFor="account-current-password">{t("account.security.currentPassword")}</Label><Input id="account-current-password" type="password" autoComplete="current-password" required value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></div>
      <div className="space-y-2"><Label htmlFor="account-new-password">{t("account.security.newPassword")}</Label><Input id="account-new-password" type="password" autoComplete="new-password" required value={newPassword} onChange={event => setNewPassword(event.target.value)} /></div>
      <div className="space-y-2"><Label htmlFor="account-confirm-password">{t("account.security.confirmPassword")}</Label><Input id="account-confirm-password" type="password" autoComplete="new-password" required value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></div>
      <Button type="submit" disabled={changePassword.isPending}><KeyRound className="me-2 h-4 w-4" />{changePassword.isPending ? t("account.security.changingPassword") : t("account.security.changePassword")}</Button>
    </form></CardContent></Card> : <Card><CardContent className="p-5"><p className="text-sm text-muted-foreground">{t("account.security.managedIdentityPassword")}</p></CardContent></Card>}
    <Card><CardHeader><div className="flex flex-wrap items-start justify-between gap-3"><div><CardTitle className="text-base">{t("account.security.sessions")}</CardTitle><CardDescription>{t("account.security.sessionsDescription", { count: data.security.activeSessionCount })}</CardDescription></div><Button variant="outline" size="sm" disabled={revokeOthers.isPending || data.sessions.filter(session => !session.isCurrent).length === 0} onClick={() => void revokeOtherSessions()}><Trash2 className="me-2 h-4 w-4" />{t("account.security.revokeOthers")}</Button></div></CardHeader><CardContent className="space-y-3">
      {data.sessions.length ? data.sessions.map(session => <div key={session.id} className="flex flex-col gap-3 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><p className="text-sm font-medium">{session.isCurrent ? t("account.security.currentSession") : t("account.security.otherSession")}</p>{session.isCurrent ? <span className="rounded-full bg-primary/10 px-2 py-0.5 text-xs text-primary">{t("account.security.thisDevice")}</span> : null}</div><p className="mt-1 text-xs text-muted-foreground">{t("account.security.sessionStarted", { date: formatDateTime(session.createdAt) })} · {session.ipAddress || t("account.security.ipUnavailable")}</p><p className="mt-1 truncate text-xs text-muted-foreground" title={session.userAgent || undefined}>{session.userAgent || t("account.security.deviceUnavailable")}</p></div><Button variant="outline" size="sm" disabled={revokeSession.isPending} onClick={() => void revokeOne(session.id, session.isCurrent)}>{t("account.security.revokeSession")}</Button></div>) : <EmptyState icon={<LockKeyhole className="h-5 w-5" />} title={t("account.security.noSessions")} description={t("account.security.noSessionsDescription")} />}
      {message ? <p role={message.error ? "alert" : "status"} className={`text-sm ${message.error ? "text-destructive" : "text-success"}`}>{message.text}</p> : null}
    </CardContent></Card>
  </div>;
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

  const changePreference = async (type: string, channel: "inAppEnabled" | "pushEnabled" | "emailEnabled", enabled: boolean) => {
    const current = preferences.data?.items;
    if (!current) return;
    setBusyType(type);
    setMessageKey(null);
    try {
      if (channel === "pushEnabled" && enabled) {
        if (!preferences.data?.pushAvailable || !isFirebaseMessagingConfigured()) { setPushStatus("unconfigured"); setMessageKey("account.notifications.pushUnavailable"); return; }
        const registration = await enablePushNotifications();
        setPushStatus(registration.status);
        if (registration.status !== "enabled") { setMessageKey("account.notifications.pushStatus"); return; }
      }
      if (channel === "emailEnabled" && enabled && !preferences.data?.emailAvailable) { setMessageKey("account.notifications.emailUnavailable"); return; }
      const next = current.map(item => item.type === type ? { ...item, [channel]: enabled } : item);
      await updatePreferences.mutateAsync(next);
      if (channel === "pushEnabled" && !enabled && !next.some(item => item.pushEnabled)) { await deactivateRegisteredPushDevice(); setPushStatus("notEnabled"); }
      setMessageKey("account.notifications.saved");
    } catch { setMessageKey("account.notifications.saveFailed"); }
    finally { setBusyType(null); }
  };

  const eventLabels: Record<string, string> = {
    ProjectHandover: t("account.notifications.projectHandover"),
    ProgramHandover: t("account.notifications.programHandover"),
    PortfolioHandover: t("account.notifications.portfolioHandover"),
    ProjectPaymentRequestApproval: t("account.notifications.projectPaymentRequestApproval"),
    PaymentDue: t("account.notifications.paymentDue"),
    PaymentOverdue: t("account.notifications.paymentOverdue"),
  };
  const pushUnavailable = !preferences.data?.pushAvailable || !isFirebaseMessagingConfigured();
  const messageIsError = messageKey === "account.notifications.pushUnavailable" || messageKey === "account.notifications.emailUnavailable" || messageKey?.endsWith("Failed") === true || messageKey?.endsWith("Status") === true;
  return <div className="space-y-6">
    <PageHeader title={t("account.notifications.title")} description={t("account.notifications.description")} />
    <Card><CardHeader><CardTitle className="text-base">{t("account.notifications.deliveryTitle")}</CardTitle><CardDescription>{t("account.notifications.deliveryDescription")}</CardDescription></CardHeader><CardContent className="space-y-5">
      {preferences.isLoading ? <p className="text-sm text-muted-foreground">{t("common.loading")}</p> : preferences.isError ? <div className="space-y-3"><p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p><Button variant="outline" size="sm" onClick={() => void preferences.refetch()}>{t("common.retry")}</Button></div> : <>
        <div className="overflow-x-auto"><table className="w-full min-w-[560px] text-sm"><thead><tr className="border-b text-start text-muted-foreground"><th className="py-2 font-medium">{t("account.notifications.event")}</th><th className="px-3 py-2 text-center font-medium">{t("account.notifications.inApp")}</th><th className="px-3 py-2 text-center font-medium">{t("account.notifications.push")}</th><th className="px-3 py-2 text-center font-medium">{t("account.notifications.email")}</th></tr></thead><tbody>{preferences.data?.items.map(item => <tr key={item.type} className="border-b last:border-0">
          <th scope="row" className="py-3 text-start font-medium">{eventLabels[item.type] ?? item.type}</th>
          <td className="px-3 py-3 text-center">{item.type === "MeetingMinutesShared" ? <span title={t("account.notifications.emailOnlyForThisEvent")}>—</span> : <input type="checkbox" aria-label={`${eventLabels[item.type] ?? item.type} ${t("account.notifications.inApp")}`} checked={item.inAppEnabled} disabled={busyType === item.type || updatePreferences.isPending} onChange={event => void changePreference(item.type, "inAppEnabled", event.target.checked)} />}</td>
          <td className="px-3 py-3 text-center">{item.type === "MeetingMinutesShared" ? <span title={t("account.notifications.emailOnlyForThisEvent")}>—</span> : <input type="checkbox" aria-label={`${eventLabels[item.type] ?? item.type} ${t("account.notifications.push")}`} checked={item.pushEnabled} disabled={busyType === item.type || updatePreferences.isPending} onChange={event => void changePreference(item.type, "pushEnabled", event.target.checked)} />}</td>
          <td className="px-3 py-3 text-center"><input type="checkbox" aria-label={`${eventLabels[item.type] ?? item.type} ${t("account.notifications.email")}`} checked={item.emailEnabled} disabled={busyType === item.type || updatePreferences.isPending || (!preferences.data?.emailAvailable && !item.emailEnabled)} title={!preferences.data?.emailAvailable ? t("account.notifications.emailUnavailable") : undefined} onChange={event => void changePreference(item.type, "emailEnabled", event.target.checked)} /></td>
        </tr>)}</tbody></table></div>
        <p className="text-xs text-muted-foreground">{t(`account.notifications.pushStates.${pushUnavailable ? "unconfigured" : pushStatus ?? "notEnabled"}`)}</p>
        {!preferences.data?.emailAvailable ? <p className="text-xs text-muted-foreground">{t("account.notifications.emailUnavailable")}</p> : null}
        {messageKey ? <p role={messageIsError ? "alert" : "status"} className={`text-sm ${messageIsError ? "text-destructive" : "text-success"}`}>{t(messageKey)}</p> : null}
      </>}
      <Link to="/notifications" className="inline-block text-sm text-primary hover:underline">{t("account.notifications.openCentre")}</Link>
    </CardContent></Card>
  </div>;
}
