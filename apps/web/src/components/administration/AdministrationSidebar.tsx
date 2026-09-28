"use client";

import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { usePathname } from "next/navigation";
import { Clock3, Coins, FileText, GitBranch, ListFilter, Puzzle, Settings2, Shield, SlidersHorizontal, Users, Tag } from "lucide-react";
import { cn } from "@/lib/utils";

type MenuItem = { href: string; label: string; icon: typeof Settings2 };

export function AdministrationSidebar() {
  const locale = useLocale();
  const pathname = usePathname();
  const t = useTranslations("administration.menu");
  const menu: MenuItem[] = [
    { href: "/administration/settings", label: t("settings"), icon: Settings2 },
    { href: "/workflows", label: t("workflowManagement"), icon: GitBranch },
    { href: "/administration/settings#time-zone", label: t("dateTime"), icon: Clock3 },
    { href: "/administration/currencies", label: t("currencies"), icon: Coins },
    { href: "/administration/reference-data/work-item-statuses", label: t("referenceData"), icon: ListFilter },
    { href: "/administration/reference-data/vendor-categories", label: t("vendorCategories"), icon: Tag },
    { href: "/administration/users", label: t("users"), icon: Users },
    { href: "/administration/roles", label: t("roles"), icon: Shield },
    { href: "/administration/personalization", label: t("personalization"), icon: SlidersHorizontal },
    { href: "/administration/plugins", label: t("plugins"), icon: Puzzle },
    { href: "/administration/license", label: t("license"), icon: FileText },
  ];

  return <aside className="h-fit rounded-xl border border-border/70 bg-card p-2 shadow-sm lg:sticky lg:top-4"><div className="px-3 pb-2 pt-1"><p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("title")}</p><p className="mt-1 text-[11px] leading-5 text-muted-foreground">{t("description")}</p></div><nav aria-label={t("title")} className="flex gap-1 overflow-x-auto pb-1 lg:block lg:space-y-1 lg:overflow-visible">{menu.map(({ href, label, icon: Icon }) => { const path = href.split("#")[0]; const isTimeZone = href.includes("#time-zone"); const isActive = pathname === `/${locale}${path}` && (!isTimeZone || (typeof window !== "undefined" && window.location.hash === "#time-zone")); return <Link key={href} href={`/${locale}${href}`} className={cn("flex min-w-max items-center gap-2 rounded-lg px-3 py-2 text-sm transition-colors lg:w-full", isActive ? "bg-primary/10 font-semibold text-primary" : "text-muted-foreground hover:bg-muted hover:text-foreground")}><Icon className="h-4 w-4 shrink-0" />{label}</Link>; })}</nav></aside>;
}
