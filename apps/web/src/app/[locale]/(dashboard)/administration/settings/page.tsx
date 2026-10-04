"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { BellRing, Clock3, FileText, Globe2, Info, Save, Settings2, ShieldCheck, type LucideIcon } from "lucide-react";

function SettingsSection({ icon: Icon, title, description, children }: { icon: LucideIcon; title: string; description: string; children: React.ReactNode }) {
  return <Card className="overflow-hidden border-border/70 shadow-sm"><CardHeader className="border-b bg-muted/20 pb-4"><div className="flex items-start gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Icon className="h-4 w-4" /></div><div className="min-w-0"><CardTitle className="text-base">{title}</CardTitle><CardDescription className="mt-1 text-xs leading-5">{description}</CardDescription></div></div></CardHeader><CardContent className="p-5">{children}</CardContent></Card>;
}

export default function AdminSettingsPage() {
  const t = useTranslations("administration.settings");
  const [applicationName, setApplicationName] = useState("NEXUS PMO");
  const [tagline, setTagline] = useState("Plan. Govern. Execute. Optimize.");
  const [timeZone, setTimeZone] = useState("london");
  const [emailNotifications, setEmailNotifications] = useState(true);
  const [auditLogging, setAuditLogging] = useState(true);

  return (
    <div className="space-y-6">
      <PageHeader title={t("title")} description={t("description")} actions={<div className="flex flex-wrap gap-2"><Button asChild size="sm" variant="outline"><Link href="/administration/license"><FileText className="me-2 h-4 w-4" />{t("license")}</Link></Button><Button size="sm"><Save className="me-2 h-4 w-4" />{t("save")}</Button></div>} />

      <div className="rounded-2xl border border-primary/15 bg-gradient-to-br from-primary/[0.08] via-card to-card p-5 shadow-sm sm:p-6"><div className="flex items-start gap-4"><div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-sm"><Settings2 className="h-5 w-5" /></div><div><p className="text-sm font-semibold">{t("overviewTitle")}</p><p className="mt-1 max-w-2xl text-xs leading-5 text-muted-foreground">{t("overviewDescription")}</p></div></div></div>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(18rem,0.65fr)]">
        <div className="space-y-5">
          <SettingsSection icon={Globe2} title={t("generalTitle")} description={t("generalDescription")}>
            <div className="grid gap-5 sm:grid-cols-2"><div className="space-y-2"><Label htmlFor="application-name">{t("applicationName")}</Label><Input id="application-name" value={applicationName} onChange={(event) => setApplicationName(event.target.value)} /><p className="text-[11px] leading-4 text-muted-foreground">{t("applicationNameHint")}</p></div><div className="space-y-2"><Label htmlFor="application-tagline">{t("tagline")}</Label><Input id="application-tagline" value={tagline} onChange={(event) => setTagline(event.target.value)} /><p className="text-[11px] leading-4 text-muted-foreground">{t("taglineHint")}</p></div></div>
          </SettingsSection>

          <SettingsSection icon={Clock3} title={t("regionalTitle")} description={t("regionalDescription")}>
            <div id="time-zone" className="scroll-mt-6 space-y-2"><Label htmlFor="default-time-zone">{t("timeZone")}</Label><Select value={timeZone} onValueChange={setTimeZone}><SelectTrigger id="default-time-zone"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="london">{t("timeZones.london")}</SelectItem><SelectItem value="dubai">{t("timeZones.dubai")}</SelectItem><SelectItem value="newyork">{t("timeZones.newYork")}</SelectItem></SelectContent></Select><p className="text-[11px] leading-4 text-muted-foreground">{t("timeZoneHint")}</p></div>
          </SettingsSection>
        </div>

        <div className="space-y-5">
          <SettingsSection icon={BellRing} title={t("notificationsTitle")} description={t("notificationsDescription")}>
            <div className="flex items-start justify-between gap-4"><div className="min-w-0"><Label htmlFor="email-notifications">{t("emailNotifications")}</Label><p className="mt-1 text-xs leading-5 text-muted-foreground">{t("emailNotificationsDescription")}</p></div><Switch id="email-notifications" checked={emailNotifications} onCheckedChange={setEmailNotifications} /></div>
          </SettingsSection>

          <SettingsSection icon={ShieldCheck} title={t("governanceTitle")} description={t("governanceDescription")}>
            <div className="flex items-start justify-between gap-4"><div className="min-w-0"><Label htmlFor="audit-logging">{t("auditLogging")}</Label><p className="mt-1 text-xs leading-5 text-muted-foreground">{t("auditLoggingDescription")}</p></div><Switch id="audit-logging" checked={auditLogging} onCheckedChange={setAuditLogging} /></div>
          </SettingsSection>

          <div className="flex items-start gap-3 rounded-xl border border-blue-200/70 bg-blue-50/60 p-4 text-blue-900 dark:border-blue-900/60 dark:bg-blue-950/20 dark:text-blue-100"><Info className="mt-0.5 h-4 w-4 shrink-0" /><p className="text-xs leading-5">{t("futureSettingsHint")}</p></div>
        </div>
      </div>
    </div>
  );
}
