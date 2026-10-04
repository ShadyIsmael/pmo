"use client";

import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/lib/auth";

export default function AccountSecurityPage() {
  const t = useTranslations("account.security");
  const { user } = useAuth();
  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} /><Card><CardHeader><CardTitle className="text-base">{t("cardTitle")}</CardTitle></CardHeader><CardContent className="space-y-3 text-sm"><p className="text-muted-foreground">{t("cardDescription")}</p><p><span className="text-muted-foreground">{t("signedInAs")}:</span> {user?.email ?? "—"}</p></CardContent></Card></div>;
}
