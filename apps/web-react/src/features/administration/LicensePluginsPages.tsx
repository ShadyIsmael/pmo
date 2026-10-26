import { brand } from "@lib/brand";
import { PuzzleIcon, Settings, Trash2 } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Badge } from "@components/ui/Badge";
import { useI18n } from "@i18n/messages";

export function LicensePage() {
  const { t } = useI18n();
  return (
    <div className="space-y-6">
      <PageHeader title={t("administration.license.title")} description={t("administration.license.description")} />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-lg">{t("administration.license.current")}</CardTitle>
              <Badge status="Active">Active</Badge>
            </div>
            <CardDescription>Enterprise Edition</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex justify-between"><span className="text-muted-foreground">License Key</span><span className="font-mono text-xs font-medium">NEXUS-ENT-2026-XXXXXXXX</span></div>
            <div className="flex justify-between"><span className="text-muted-foreground">Issued To</span><span className="font-medium">{brand.company.name}</span></div>
            <div className="flex justify-between"><span className="text-muted-foreground">Expires</span><span className="font-medium">Dec 31, 2027</span></div>
            <div className="flex justify-between"><span className="text-muted-foreground">Max Users</span><span className="font-medium">100</span></div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

export function PluginsPage() {
  const { t } = useI18n();
  const installed = [
    { id: "plg-1", name: "Advanced Gantt Chart", version: "2.1.0", status: "Active", author: brand.company.name },
    { id: "plg-2", name: "SAP Integration", version: "1.0.2", status: "Active", author: brand.company.name },
    { id: "plg-5", name: "Timesheet Automation", version: "2.0.0", status: "Active", author: brand.company.name },
  ];
  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.plugins.title")}
        description={t("administration.plugins.description", { count: installed.length })}
        actions={<Button size="sm"><PuzzleIcon className="mr-2 h-4 w-4" />{t("administration.plugins.browse")}</Button>}
      />
      <div className="space-y-3">
        {installed.map((plugin) => (
          <Card key={plugin.id}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <PuzzleIcon className="h-5 w-5 text-primary" />
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <p className="font-medium">{plugin.name}</p>
                  <Badge status={plugin.status}>{plugin.status}</Badge>
                </div>
                <p className="text-xs text-muted-foreground">v{plugin.version} by {plugin.author}</p>
              </div>
              <Button size="sm" variant="outline"><Settings className="mr-1 h-4 w-4" />{t("common.configure")}</Button>
              <Button size="sm" variant="outline" aria-label={t("common.delete")}><Trash2 className="h-4 w-4" /></Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
