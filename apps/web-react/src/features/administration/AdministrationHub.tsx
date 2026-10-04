import { Link, Outlet, useLocation } from "react-router-dom";
import { ArrowUpRight, GitBranch, ListFilter, Plug, Settings2, Shield, Building2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { hasAnyPermission, Permissions } from "@lib/permissions";
import { cn } from "@lib/utils";

export function AdministrationHub() {
  const { t } = useI18n();
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const areas = [
    { id: "organization", href: "/administration/organization", icon: Building2, title: t("administration.home.cards.organization.title"), description: t("administration.home.cards.organization.description"), perms: [Permissions.Settings.Read] },
    { id: "access", href: "/administration/access", icon: Shield, title: t("administration.home.cards.access.title"), description: t("administration.home.cards.access.description"), perms: [Permissions.Users.Read, Permissions.Settings.Read] },
    { id: "reference-data", href: "/administration/reference-data", icon: ListFilter, title: t("administration.home.cards.referenceData.title"), description: t("administration.home.cards.referenceData.description"), perms: [Permissions.Settings.Read] },
    { id: "platform-settings", href: "/administration/settings", icon: Settings2, title: t("administration.home.cards.platformSettings.title"), description: t("administration.home.cards.platformSettings.description"), perms: [Permissions.Settings.Read] },
    { id: "integrations", href: "/administration/integrations", icon: Plug, title: t("administration.home.cards.integrations.title"), description: t("administration.home.cards.integrations.description"), perms: [Permissions.Settings.Read] },
    { id: "governance", href: "/administration/governance", icon: GitBranch, title: t("administration.home.cards.governance.title"), description: t("administration.home.cards.governance.description"), perms: [Permissions.Settings.Read, Permissions.Workflow.View] },
  ].filter((a) => hasAnyPermission(permissions, a.perms));

  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.home.title")} description={t("administration.home.description")} />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {areas.map(({ id, href, icon: Icon, title, description }) => (
          <Link key={id} to={href} className="group">
            <Card className="h-full transition-all group-hover:-translate-y-0.5 group-hover:border-primary/30">
              <CardHeader className="gap-4">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-5 w-5" /></div>
                <div><CardTitle className="text-base">{title}</CardTitle><CardDescription className="mt-1.5 text-xs leading-5">{description}</CardDescription></div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary">
                  <span>{t("administration.home.open")}</span><ArrowUpRight className="h-4 w-4" />
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}

export function AdministrationLayout() {
  const { t } = useI18n();
  const location = useLocation();
  const tabs = [
    { id: "organization", href: "/administration/organization", label: t("nav.organization") },
    { id: "access", href: "/administration/access", label: t("nav.accessSecurity") },
    { id: "reference-data", href: "/administration/reference-data", label: t("nav.referenceData") },
    { id: "settings", href: "/administration/settings", label: t("nav.platformSettings") },
    { id: "integrations", href: "/administration/integrations", label: t("nav.integrations") },
    { id: "governance", href: "/administration/governance", label: t("nav.governance") },
  ];
  return (
    <div className="space-y-4">
      <nav className="flex gap-1 overflow-x-auto" aria-label="Administration">
        {tabs.map((tab) => (
          <Link
            key={tab.id}
            to={tab.href}
            className={cn(
              "whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium",
              location.pathname.startsWith(tab.href) ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent"
            )}
          >
            {tab.label}
          </Link>
        ))}
      </nav>
      <Outlet />
    </div>
  );
}
