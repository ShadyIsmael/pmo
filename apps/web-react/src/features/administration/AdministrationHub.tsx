import { Link, Navigate, Outlet } from "react-router-dom";
import { ArrowUpRight, GitBranch, ListFilter, Mail, Plug, Settings2, Shield, Building2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { DirectoryAccessPermissions, hasAnyPermission, Permissions, ReferenceDataAccessPermissions } from "@lib/permissions";
import { useOrganizationProfiles, useSettingsAccess } from "./api/settings";
import { useEmailNotificationSettingsAccess } from "./api/emailNotifications";
import type { LucideIcon } from "lucide-react";

type AdministrationArea = { id: string; href: string; icon: LucideIcon; title: string; description: string; perms: string[] };

export function AdministrationHub() {
  const { t } = useI18n();
  const { user } = useAuth();
  const settingsAccess = useSettingsAccess();
  const emailNotificationAccess = useEmailNotificationSettingsAccess();
  const permissions = user?.permissions ?? [];
  const organizationSettingsAccess = useOrganizationProfiles(permissions.includes(Permissions.Settings.View));
  const canAccessTenantAdministration = hasAnyPermission(permissions, [Permissions.Users.View, Permissions.Users.ManageAccounts, Permissions.Users.SetStatus, Permissions.Users.AssignRoles, Permissions.Users.ManageOverrides, ...ReferenceDataAccessPermissions, ...DirectoryAccessPermissions, Permissions.Workflow.View, Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage, Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete, Permissions.EmailNotifications.View]);
  const canReadPlatform = settingsAccess.data?.canReadPlatform === true;
  const canReadEmailNotifications = emailNotificationAccess.data?.canReadTenant === true
    || emailNotificationAccess.data?.canReadPlatform === true;
  const areas: AdministrationArea[] = [
    { id: "organization", href: "/administration/organization", icon: Building2, title: t("administration.home.cards.organization.title"), description: t("administration.home.cards.organization.description"), perms: [Permissions.Settings.View] },
    { id: "tenant-settings", href: "/administration/tenant-settings", icon: Settings2, title: t("administration.settingsWorkspace.tenantTitle"), description: t("administration.settingsWorkspace.tenantDescription"), perms: [Permissions.Settings.View] },
    { id: "organization-settings", href: "/administration/organization-settings", icon: Building2, title: t("administration.settingsWorkspace.organizationTitle"), description: t("administration.settingsWorkspace.organizationDescription"), perms: [Permissions.Settings.View] },
    { id: "access", href: "/administration/access", icon: Shield, title: t("administration.home.cards.access.title"), description: t("administration.home.cards.access.description"), perms: [Permissions.Users.View, Permissions.Users.ManageAccounts, Permissions.Users.SetStatus, Permissions.Users.AssignRoles, Permissions.Users.ManageOverrides, Permissions.Roles.View, Permissions.Roles.Create, Permissions.Roles.EditDetails, Permissions.Roles.ConfigurePermissions, Permissions.Roles.Delete] },
    { id: "reference-data", href: "/administration/reference-data", icon: ListFilter, title: t("administration.home.cards.referenceData.title"), description: t("administration.home.cards.referenceData.description"), perms: ReferenceDataAccessPermissions },
    { id: "integrations", href: "/administration/integrations", icon: Plug, title: t("administration.home.cards.integrations.title"), description: t("administration.home.cards.integrations.description"), perms: [Permissions.Settings.View, ...DirectoryAccessPermissions] },
    { id: "governance", href: "/administration/governance", icon: GitBranch, title: t("administration.home.cards.governance.title"), description: t("administration.home.cards.governance.description"), perms: [Permissions.Settings.View, Permissions.Workflow.View, Permissions.AssignmentRecovery.View, Permissions.AssignmentRecovery.Initiate, Permissions.AssignmentRecovery.Manage] },
  ].filter((a) => hasAnyPermission(permissions, a.perms)
    && (a.id !== "organization-settings" || (organizationSettingsAccess.data?.length ?? 0) > 0));
  if (canReadEmailNotifications) {
    areas.push({ id: "email-notifications", href: "/administration/email-notifications", icon: Mail,
      title: t("administration.emailNotifications.title"), description: t("administration.emailNotifications.description"), perms: [] });
  }
  if (canReadPlatform) {
    areas.push({ id: "platform-settings", href: "/administration/settings", icon: Settings2, title: t("administration.home.cards.platformSettings.title"), description: t("administration.home.cards.platformSettings.description"), perms: [Permissions.Settings.View] });
  }

  if (!canAccessTenantAdministration && (settingsAccess.isLoading || emailNotificationAccess.isLoading)) {
    return <div className="route-state" role="status">{t("auth.loadingAuthorization")}</div>;
  }
  if (!canAccessTenantAdministration && !canReadPlatform && !canReadEmailNotifications) return <Navigate to="/home" replace />;

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
  return <Outlet />;
}
