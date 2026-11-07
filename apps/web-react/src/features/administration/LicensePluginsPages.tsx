import { brand } from "@lib/brand";
import { PageHeader } from "@components/layout/PageHeader";
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
              <Badge status="Pending">{t("administration.license.notConfigured")}</Badge>
            </div>
            <CardDescription>{t("administration.license.backendUnavailable")}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex justify-between"><span className="text-muted-foreground">{t("administration.license.licenseKey")}</span><span className="font-medium">{t("common.notAvailable")}</span></div>
            <div className="flex justify-between"><span className="text-muted-foreground">{t("administration.license.issuedTo")}</span><span className="font-medium">{brand.company.name}</span></div>
            <div className="flex justify-between"><span className="text-muted-foreground">{t("administration.license.expires")}</span><span className="font-medium">{t("common.notAvailable")}</span></div>
            <div className="flex justify-between"><span className="text-muted-foreground">{t("administration.license.maxUsers")}</span><span className="font-medium">{t("common.notAvailable")}</span></div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

export function PluginsPage() {
  const { t } = useI18n();
  const installed: { id: string; name: string; version: string; status: string; author: string }[] = [];
  return (
    <div className="space-y-6">
      <PageHeader
        title={t("administration.plugins.title")}
        description={t("administration.plugins.description", { count: installed.length })}
      />
      <Card>
        <CardContent className="py-8 text-center text-sm text-muted-foreground">
          {t("administration.plugins.noneVerified")}
        </CardContent>
      </Card>
    </div>
  );
}
