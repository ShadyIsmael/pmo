import * as React from "react";
import { useParams } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { useI18n } from "@i18n/messages";
import { useRisk, useUpdateRiskStatus } from "./api/risks";

export function RiskDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useI18n();
  // Visible-first: detail loads on route; actions/impacts load only inside their tabs/sheets (omitted here for brevity, same pattern).
  const query = useRisk(id ?? "", Boolean(id));
  const updateStatus = useUpdateRiskStatus();
  const risk = query.data;

  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !risk) return <p className="py-12 text-center text-sm text-destructive">{t("risks.notFound")}</p>;

  return (
    <div className="space-y-6">
      <PageHeader title={risk.title} description={`${risk.projectName} · ${risk.riskLevel}`} />
      <Card>
        <CardHeader><CardTitle>{t("risks.details")}</CardTitle></CardHeader>
        <CardContent className="space-y-2 text-sm">
          <p><strong>{t("risks.status")}:</strong> {risk.status}</p>
          <p><strong>{t("risks.owner")}:</strong> {risk.ownerResourceName ?? "—"}</p>
          <p className="text-muted-foreground">{risk.description}</p>
          <div className="flex gap-2 pt-2">
            {risk.status !== "Mitigated" ? <Button size="sm" variant="outline" disabled={updateStatus.isPending} onClick={() => updateStatus.mutate({ id: risk.id, status: "Mitigated", rowVersion: risk.rowVersion })}>{t("risks.mitigate")}</Button> : null}
            {risk.status !== "Closed" ? <Button size="sm" variant="outline" disabled={updateStatus.isPending} onClick={() => updateStatus.mutate({ id: risk.id, status: "Closed", rowVersion: risk.rowVersion })}>{t("risks.close")}</Button> : null}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
