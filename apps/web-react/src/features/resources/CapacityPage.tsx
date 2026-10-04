import * as React from "react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useCapacityMatrix } from "./api/capacity";

export function CapacityPage() {
  const { t, formatPercent } = useI18n();
  const [periodType, setPeriodType] = React.useState("monthly");
  const query = useCapacityMatrix({ periodType });
  const rows = query.data?.rows ?? [];

  return (
    <div className="space-y-6">
      <PageHeader title={t("resources.capacity.title")} description={t("resources.capacity.description")} />
      <Card>
        <CardHeader><CardTitle>{t("resources.capacity.capacityMatrix")}</CardTitle></CardHeader>
        <CardContent>
          <div className="mb-3 flex items-center gap-2">
            <select className="input-field max-w-48" value={periodType} onChange={(e) => setPeriodType(e.target.value)} aria-label={t("resources.capacity.period")}>
              <option value="weekly">{t("resources.capacity.weekly")}</option>
              <option value="monthly">{t("resources.capacity.monthly")}</option>
              <option value="quarterly">{t("resources.capacity.quarterly")}</option>
            </select>
          </div>
          {query.isLoading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : query.isError ? <p className="py-8 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
            : rows.length === 0 ? <p className="py-8 text-center text-sm text-muted-foreground">{t("resources.capacity.noData")}</p>
            : (
              <Table>
                <TableHeader><TableRow><TableHead>{t("resources.capacity.resourceName")}</TableHead><TableHead>{t("resources.capacity.utilization")}</TableHead><TableHead>{t("resources.capacity.availableLabel")}</TableHead></TableRow></TableHeader>
                <TableBody>
                  {rows.slice(0, 50).map((r) => (
                    <TableRow key={r.resourceId}>
                      <TableCell className="font-medium">{r.resourceName}</TableCell>
                      <TableCell className="tabular-nums">{formatPercent(r.currentUtilization)}</TableCell>
                      <TableCell className="tabular-nums">{r.weeklyCapacityHours}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
        </CardContent>
      </Card>
    </div>
  );
}
