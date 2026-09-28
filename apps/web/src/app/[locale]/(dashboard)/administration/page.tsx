"use client";

import Link from "next/link";
import { useTranslations } from "next-intl";
import { ArrowUpRight, Coins, FileText, ListFilter, Puzzle, Settings2, Shield, SlidersHorizontal, Users, type LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/PageHeader";

type AdminSection = { id: string; href: string; icon: LucideIcon; category: "configuration" | "access" | "platform"; title: string; description: string };

export default function AdministrationPage() {
  const t = useTranslations("administration.home");
  const sections: AdminSection[] = [
    { id: "settings", href: "/administration/settings", icon: Settings2, category: "configuration", title: t("cards.settings.title"), description: t("cards.settings.description") },
    { id: "currencies", href: "/administration/currencies", icon: Coins, category: "configuration", title: t("cards.currencies.title"), description: t("cards.currencies.description") },
    { id: "reference-data", href: "/administration/reference-data/work-item-statuses", icon: ListFilter, category: "configuration", title: t("cards.referenceData.title"), description: t("cards.referenceData.description") },
    { id: "users", href: "/administration/users", icon: Users, category: "access", title: t("cards.users.title"), description: t("cards.users.description") },
    { id: "roles", href: "/administration/roles", icon: Shield, category: "access", title: t("cards.roles.title"), description: t("cards.roles.description") },
    { id: "license", href: "/administration/license", icon: FileText, category: "access", title: t("cards.license.title"), description: t("cards.license.description") },
    { id: "personalization", href: "/administration/personalization", icon: SlidersHorizontal, category: "platform", title: t("cards.personalization.title"), description: t("cards.personalization.description") },
    { id: "plugins", href: "/administration/plugins", icon: Puzzle, category: "platform", title: t("cards.plugins.title"), description: t("cards.plugins.description") },
  ];

  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} /><section className="relative overflow-hidden rounded-2xl border border-primary/15 bg-gradient-to-br from-primary/[0.1] via-card to-card p-6 shadow-sm sm:p-8"><div className="pointer-events-none absolute -end-12 -top-16 h-48 w-48 rounded-full bg-primary/10 blur-3xl" /><div className="relative max-w-3xl"><Badge variant="outline" className="border-primary/25 bg-background/70 text-primary">{t("badge")}</Badge><h2 className="mt-4 text-xl font-bold tracking-tight sm:text-2xl">{t("welcomeTitle")}</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">{t("welcomeDescription")}</p></div></section><section className="space-y-4"><div><h2 className="text-base font-semibold">{t("allAreasTitle")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("allAreasDescription")}</p></div><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{sections.map(({ id, href, icon: Icon, category, title, description }) => <Link key={id} href={href} className="group"><Card className="h-full border-border/70 shadow-sm transition-all duration-200 group-hover:-translate-y-0.5 group-hover:border-primary/30 group-hover:shadow-md"><CardHeader className="gap-4"><div className="flex items-start justify-between gap-3"><div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-5 w-5" /></div><span className="rounded-full bg-muted px-2.5 py-1 text-[10px] font-medium text-muted-foreground">{t(`categories.${category}`)}</span></div><div><CardTitle className="text-base">{title}</CardTitle><CardDescription className="mt-1.5 text-xs leading-5">{description}</CardDescription></div></CardHeader><CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("open")}</span><ArrowUpRight className="h-4 w-4 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5 rtl:group-hover:-translate-x-0.5" /></div></CardContent></Card></Link>)}</div></section></div>;
}
