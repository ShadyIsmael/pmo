"use client";

import Link from "next/link";
import { Bell, ExternalLink } from "lucide-react";
import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function AccountNotificationsPage() {
  const t = useTranslations("account.notifications");
  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} /><Card><CardHeader><CardTitle className="flex items-center gap-2 text-base"><Bell className="h-4 w-4 text-primary" />{t("cardTitle")}</CardTitle></CardHeader><CardContent className="space-y-4"><p className="text-sm leading-6 text-muted-foreground">{t("cardDescription")}</p><Button asChild variant="outline"><Link href="/notifications"><ExternalLink className="me-2 h-4 w-4" />{t("openCentre")}</Link></Button></CardContent></Card></div>;
}
