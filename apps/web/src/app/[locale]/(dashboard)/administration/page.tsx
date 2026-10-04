"use client";

import Link from "next/link";
import { ArrowUpRight, GitBranch, ListFilter, Plug, Settings2, Shield, type LucideIcon } from "lucide-react";
import { useTranslations } from "next-intl";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/PageHeader";
import { useAuth } from "@/lib/auth";
import { hasAnyPermission, Permissions } from "@/lib/permissions";

type AdminArea = {
  id: string;
  href: string;
  icon: LucideIcon;
  title: string;
  description: string;
  requiredPermissions: string[];
};

export default function AdministrationPage() {
  const t = useTranslations("administration.home");
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const areas: AdminArea[] = [
    { id: "access", href: "/administration/access", icon: Shield, title: t("cards.access.title"), description: t("cards.access.description"), requiredPermissions: [Permissions.Users.Read, Permissions.Settings.Read] },
    { id: "reference-data", href: "/administration/reference-data", icon: ListFilter, title: t("cards.referenceData.title"), description: t("cards.referenceData.description"), requiredPermissions: [Permissions.Settings.Read] },
    { id: "platform-settings", href: "/administration/settings", icon: Settings2, title: t("cards.platformSettings.title"), description: t("cards.platformSettings.description"), requiredPermissions: [Permissions.Settings.Read] },
    { id: "integrations", href: "/administration/integrations", icon: Plug, title: t("cards.integrations.title"), description: t("cards.integrations.description"), requiredPermissions: [Permissions.Settings.Read] },
    { id: "governance", href: "/administration/governance", icon: GitBranch, title: t("cards.governance.title"), description: t("cards.governance.description"), requiredPermissions: [Permissions.Settings.Read, Permissions.Workflow.View] },
  ];
  const visibleAreas = areas.filter((area) => hasAnyPermission(permissions, area.requiredPermissions));

  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} /><section className="rounded-2xl border border-primary/15 bg-gradient-to-br from-primary/[0.08] via-card to-card p-6 shadow-sm sm:p-8"><div className="max-w-3xl"><h2 className="text-xl font-bold tracking-tight sm:text-2xl">{t("welcomeTitle")}</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">{t("welcomeDescription")}</p></div></section><section className="space-y-4"><div><h2 className="text-base font-semibold">{t("allAreasTitle")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("allAreasDescription")}</p></div><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{visibleAreas.map(({ id, href, icon: Icon, title, description }) => <Link key={id} href={href} className="group"><Card className="h-full border-border/70 shadow-sm transition-all duration-200 group-hover:-translate-y-0.5 group-hover:border-primary/30 group-hover:shadow-md"><CardHeader className="gap-4"><div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-5 w-5" /></div><div><CardTitle className="text-base">{title}</CardTitle><CardDescription className="mt-1.5 text-xs leading-5">{description}</CardDescription></div></CardHeader><CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("open")}</span><ArrowUpRight className="h-4 w-4 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5 rtl:group-hover:-translate-x-0.5" /></div></CardContent></Card></Link>)}</div></section></div>;
}
