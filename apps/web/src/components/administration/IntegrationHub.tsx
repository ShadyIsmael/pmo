"use client";

import { useTranslations } from "next-intl";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CheckCircle2, Puzzle, RefreshCw, Settings } from "lucide-react";
import { getLookupTextClass } from "@/lib/status-presentation";

const integrations = [
  { id: "ms-project", nameKey: "msProject", status: "Connected", typeKey: "projectManagement", icon: "📊" },
  { id: "jira", nameKey: "jira", status: "Connected", typeKey: "development", icon: "🔧" },
  { id: "servicenow", nameKey: "serviceNow", status: "Available", typeKey: "itsm", icon: "🔄" },
  { id: "sap", nameKey: "sap", status: "Available", typeKey: "erp", icon: "💰" },
  { id: "confluence", nameKey: "confluence", status: "Connected", typeKey: "documentation", icon: "📝" },
  { id: "slack", nameKey: "slack", status: "Available", typeKey: "communication", icon: "💬" },
  { id: "github", nameKey: "github", status: "Connected", typeKey: "development", icon: "🐙" },
  { id: "powerBi", nameKey: "powerBi", status: "Available", typeKey: "reporting", icon: "📈" },
  { id: "azureDevOps", nameKey: "azureDevOps", status: "Available", typeKey: "devops", icon: "☁️" },
  { id: "salesforce", nameKey: "salesforce", status: "Available", typeKey: "crm", icon: "🏆" },
] as const;

export function IntegrationHub() {
  const t = useTranslations("administration.integrations");
  const connected = integrations.filter((integration) => integration.status === "Connected");
  const available = integrations.filter((integration) => integration.status === "Available");
  const label = (key: string) => t(`items.${key}.name`);
  const description = (key: string) => t(`items.${key}.description`);
  const type = (key: string) => t(`types.${key}`);

  return <div className="space-y-6"><PageHeader title={t("title")} description={t("description")} actions={<Button asChild size="sm" variant="outline"><Link href="/administration/plugins"><Puzzle className="me-2 h-4 w-4" />{t("plugins")}</Link></Button>} /><Tabs defaultValue="all"><TabsList><TabsTrigger value="all">{t("all", { count: integrations.length })}</TabsTrigger><TabsTrigger value="connected">{t("connected", { count: connected.length })}</TabsTrigger><TabsTrigger value="available">{t("available", { count: available.length })}</TabsTrigger></TabsList><TabsContent value="all"><div className="grid gap-4 md:grid-cols-2">{integrations.map((integration) => <IntegrationCard key={integration.id} integration={integration} label={label(integration.nameKey)} description={description(integration.nameKey)} type={type(integration.typeKey)} t={t} />)}</div></TabsContent><TabsContent value="connected"><div className="grid gap-4 md:grid-cols-2">{connected.map((integration) => <IntegrationCard key={integration.id} integration={integration} label={label(integration.nameKey)} description={type(integration.typeKey)} type={type(integration.typeKey)} t={t} compact />)}</div></TabsContent><TabsContent value="available"><div className="grid gap-4 md:grid-cols-2">{available.map((integration) => <IntegrationCard key={integration.id} integration={integration} label={label(integration.nameKey)} description={description(integration.nameKey)} type={type(integration.typeKey)} t={t} compact />)}</div></TabsContent></Tabs></div>;
}

function IntegrationCard({ integration, label, description, type, t, compact = false }: { integration: (typeof integrations)[number]; label: string; description: string; type: string; t: ReturnType<typeof useTranslations>; compact?: boolean }) {
  return <Card><CardHeader><div className="flex items-start justify-between gap-3"><div className="flex items-start gap-3"><div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted text-lg">{integration.icon}</div><div><CardTitle className="text-base">{label}</CardTitle><CardDescription>{description}</CardDescription></div></div>{!compact && <Badge status={integration.status} className="text-[10px]">{integration.status === "Connected" ? <CheckCircle2 className="me-1 h-3 w-3" /> : null}{integration.status}</Badge>}</div></CardHeader><CardContent><div className="flex items-center justify-between gap-2"><span className={`text-xs font-medium ${getLookupTextClass(type)}`}>{type}</span><div className="flex gap-2">{integration.status === "Connected" ? <><Button size="sm" variant="outline"><RefreshCw className="me-1 h-3 w-3" />{t("sync")}</Button><Button size="sm" variant="outline"><Settings className="me-1 h-3 w-3" />{t("configure")}</Button></> : <Button size="sm">{t("connect")}</Button>}</div></div></CardContent></Card>;
}
