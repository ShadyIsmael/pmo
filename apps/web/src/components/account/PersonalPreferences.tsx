"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useTheme } from "next-themes";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Monitor, Save } from "lucide-react";

export function PersonalPreferences() {
  const t = useTranslations("account.preferences");
  const { theme, setTheme } = useTheme();
  const [compactMode, setCompactMode] = useState(false);

  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} actions={<Button size="sm"><Save className="me-2 h-4 w-4" />{t("save")}</Button>} /><Card><CardHeader><div className="flex items-start gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><Monitor className="h-4 w-4" /></div><div><CardTitle className="text-base">{t("displayTitle")}</CardTitle><CardDescription className="mt-1 text-xs leading-5">{t("displayDescription")}</CardDescription></div></div></CardHeader><CardContent className="space-y-5"><div className="space-y-2"><Label htmlFor="account-theme">{t("theme")}</Label><Select value={theme ?? "system"} onValueChange={setTheme}><SelectTrigger id="account-theme"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="light">{t("light")}</SelectItem><SelectItem value="dark">{t("dark")}</SelectItem><SelectItem value="system">{t("system")}</SelectItem></SelectContent></Select></div><div className="flex items-center justify-between gap-4"><div><Label htmlFor="account-compact-mode">{t("compactMode")}</Label><p className="mt-1 text-xs leading-5 text-muted-foreground">{t("compactModeDescription")}</p></div><Switch id="account-compact-mode" checked={compactMode} onCheckedChange={setCompactMode} /></div></CardContent></Card></div>;
}
