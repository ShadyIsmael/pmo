"use client";

import Link from "next/link";
import { ArrowUpRight, Bell, LockKeyhole, Settings2, UserRound } from "lucide-react";
import { useTranslations } from "next-intl";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/PageHeader";
import { useAuth } from "@/lib/auth";

export default function AccountPage() {
  const t = useTranslations("account");
  const { user } = useAuth();
  const items = [
    { id: "profile", href: "/account/profile", icon: UserRound, title: t("cards.profile.title"), description: t("cards.profile.description") },
    { id: "preferences", href: "/account/preferences", icon: Settings2, title: t("cards.preferences.title"), description: t("cards.preferences.description") },
    { id: "notifications", href: "/account/notifications", icon: Bell, title: t("cards.notifications.title"), description: t("cards.notifications.description") },
    { id: "security", href: "/account/security", icon: LockKeyhole, title: t("cards.security.title"), description: t("cards.security.description") },
  ];

  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} /><Card><CardContent className="flex items-center gap-4 p-5"><div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-lg font-semibold text-primary">{user?.fullName?.slice(0, 1).toUpperCase() ?? "U"}</div><div className="min-w-0"><p className="truncate text-base font-semibold">{user?.fullName ?? t("unknownUser")}</p><p className="truncate text-sm text-muted-foreground">{user?.email ?? ""}</p></div></CardContent></Card><div className="grid gap-4 sm:grid-cols-2">{items.map(({ id, href, icon: Icon, title, description }) => <Link key={id} href={href} className="group"><Card className="h-full transition-all group-hover:-translate-y-0.5 group-hover:border-primary/30"><CardHeader><div className="flex items-center gap-3"><div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary"><Icon className="h-4 w-4" /></div><CardTitle className="text-base">{title}</CardTitle></div><CardDescription className="mt-2 text-xs leading-5">{description}</CardDescription></CardHeader><CardContent><div className="flex items-center justify-between border-t pt-3 text-xs font-semibold text-primary"><span>{t("open")}</span><ArrowUpRight className="h-4 w-4 rtl:rotate-180" /></div></CardContent></Card></Link>)}</div></div>;
}
