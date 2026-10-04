import { Link } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";

/** Deferred/experimental Next.js routes preserved as deep-link-safe placeholders — no fake data, no Next runtime. */
export function DeferredPage({ reasonKey }: { reasonKey: string }) {
  const { t } = useI18n();
  return (
    <div className="space-y-6">
      <PageHeader title={t("common.deferredTitle")} description={t(reasonKey)} />
      <Card>
        <CardContent className="space-y-2 p-6 text-sm text-muted-foreground">
          <p>{t("common.deferredBody")}</p>
          <Link to="/home" className="text-primary hover:underline">{t("nav.home")}</Link>
        </CardContent>
      </Card>
    </div>
  );
}
