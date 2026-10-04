import { Link, Outlet } from "react-router-dom";
import { ArrowUpRight, Bell, LockKeyhole, Settings2, UserRound } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Label } from "@components/ui/Label";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useTheme } from "@theme/ThemeProvider";
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
  const [saved, setSaved] = React.useState(false);
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.preferences.title")} description={t("account.preferences.description")} />
      <Card><CardContent className="space-y-4 p-6">
        <div className="space-y-1.5">
          <Label>{t("account.preferences.theme")}</Label>
          <select className="input-field max-w-xs" value={theme} onChange={(e) => setTheme(e.target.value as "light" | "dark" | "system")}>
            <option value="light">{t("account.preferences.light")}</option>
            <option value="dark">{t("account.preferences.dark")}</option>
            <option value="system">{t("account.preferences.system")}</option>
          </select>
        </div>
        <div className="space-y-1.5">
          <Label>{t("nav.language")}</Label>
          <select className="input-field max-w-xs" value={locale} onChange={(e) => setLocale(e.target.value as "en" | "ar")}>
            <option value="en">English</option>
            <option value="ar">العربية</option>
          </select>
        </div>
        <Button onClick={() => { setSaved(true); setTimeout(() => setSaved(false), 2000); }}>{t("account.preferences.save")}</Button>
        {saved ? <p className="text-sm text-success">{t("common.saved")}</p> : null}
      </CardContent></Card>
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
  return (
    <div className="space-y-6">
      <PageHeader title={t("account.notifications.title")} description={t("account.notifications.description")} />
      <Card><CardContent className="space-y-2 p-6 text-sm">
        <p className="text-muted-foreground">{t("account.notifications.cardDescription")}</p>
        <Link to="/notifications" className="text-primary hover:underline">{t("account.notifications.openCentre")}</Link>
      </CardContent></Card>
    </div>
  );
}
