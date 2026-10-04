import * as React from "react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useCostUtilization } from "./api/capacity";

export function CostUtilizationPage() {
  const { t, formatCurrency, formatPercent } = useI18n();
  const query = useCostUtilization({});
  const report = query.data;

  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.costUtilization")} description={t("resources.capacity.description")} />
      {query.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : query.isError || !report ? <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
        : (
          <>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("resources.capacity.utilization")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{formatPercent(report.operational.actualUtilization ?? 0)}</p></CardContent></Card>
              <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("common.actual")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{report.operational.actualHours}</p></CardContent></Card>
              <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("common.budget")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{report.canViewCosts ? formatCurrency(report.operational.actualLaborCost ?? 0) : "—"}</p></CardContent></Card>
              <Card><CardHeader><CardTitle className="text-xs text-muted-foreground">{t("resources.capacity.demand")}</CardTitle></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{report.operational.plannedHours}</p></CardContent></Card>
            </div>
            <Card>
              <CardHeader><CardTitle>{t("resources.capacity.resourceAvailability")}</CardTitle></CardHeader>
              <CardContent className="p-0">
                <Table>
                  <TableHeader><TableRow><TableHead>{t("resources.capacity.resourceName")}</TableHead><TableHead>{t("resources.capacity.utilization")}</TableHead><TableHead>{t("common.actual")}</TableHead></TableRow></TableHeader>
                  <TableBody>
                    {report.resources.slice(0, 50).map((r) => (
                      <TableRow key={r.resourceId}>
                        <TableCell className="font-medium">{r.resourceName}</TableCell>
                        <TableCell className="tabular-nums">{formatPercent(r.metrics.actualUtilization ?? 0)}</TableCell>
                        <TableCell className="tabular-nums">{r.metrics.actualHours}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          </>
        )}
    </div>
  );
}
