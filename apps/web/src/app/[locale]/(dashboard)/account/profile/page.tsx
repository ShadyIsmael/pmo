"use client";

import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/lib/auth";

export default function AccountProfilePage() {
  const t = useTranslations("account.profile");
  const { user } = useAuth();
  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} /><Card><CardHeader><CardTitle className="text-base">{t("details")}</CardTitle></CardHeader><CardContent className="grid gap-4 sm:grid-cols-2"><div><p className="text-xs text-muted-foreground">{t("name")}</p><p className="mt-1 text-sm font-medium">{user?.fullName ?? "—"}</p></div><div><p className="text-xs text-muted-foreground">{t("email")}</p><p className="mt-1 text-sm font-medium">{user?.email ?? "—"}</p></div><div><p className="text-xs text-muted-foreground">{t("jobTitle")}</p><p className="mt-1 text-sm font-medium">{user?.jobTitle ?? "—"}</p></div><div><p className="text-xs text-muted-foreground">{t("roles")}</p><p className="mt-1 text-sm font-medium">{user?.roles?.map((role) => role.name).join(", ") || "—"}</p></div></CardContent></Card></div>;
}
