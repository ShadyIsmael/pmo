"use client";

import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { FolderKanban } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useMyStakeholderProjects } from "@/hooks/stakeholder/useStakeholderProjects";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import { formatDate } from "@/lib/utils";

export default function StakeholderProjectsPage() {
  const locale = useLocale();
  const t = useTranslations("stakeholder");
  const common = useTranslations("common");
  const { statusLabel, healthLabel } = useProjectEnumLabels();
  const query = useMyStakeholderProjects();
  const items = query.data?.items ?? [];
  const dateLocale = locale === "ar" ? "ar-EG" : "en-GB";

  return (
    <div className="space-y-4">
      <PageHeader title={t("title")} description={t("description")} />
      {query.isLoading ? <LoadingSkeleton rows={5} /> : query.isError ? <ErrorState title={t("loadError")} description={t("loadErrorDescription")} onRetry={() => void query.refetch()} /> : items.length === 0 ? <EmptyState icon={FolderKanban} title={t("emptyTitle")} description={t("emptyDescription")} /> : (
        <Card className="shadow-none">
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <Table>
                <TableHeader><TableRow><TableHead>{t("project")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("progress")}</TableHead><TableHead>{t("dates")}</TableHead><TableHead>{t("latestUpdate")}</TableHead><TableHead className="text-end">{common("actions")}</TableHead></TableRow></TableHeader>
                <TableBody>{items.map((project) => <TableRow key={project.id}>
                  <TableCell><Link href={`/${locale}/stakeholder/projects/${encodeURIComponent(project.id)}`} className="font-semibold text-primary hover:underline">{project.name}</Link><p className="text-xs text-muted-foreground">{project.code}</p></TableCell>
                  <TableCell><Badge status={project.status}>{statusLabel(project.status)}</Badge></TableCell>
                  <TableCell><Badge status={project.health}>{healthLabel(project.health)}</Badge></TableCell>
                  <TableCell className="tabular-nums">{project.progress.toLocaleString(locale === "ar" ? "ar-EG" : "en-GB")} %</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{formatDate(project.startDate, dateLocale)} – {formatDate(project.endDate, dateLocale)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{project.latestPublishedStatusReport ? formatDate(project.latestPublishedStatusReport.reportDate, dateLocale) : t("noPublishedUpdate")}</TableCell>
                  <TableCell className="text-end"><Link href={`/${locale}/stakeholder/projects/${encodeURIComponent(project.id)}`} className="text-sm font-medium text-primary hover:underline">{common("view")}</Link></TableCell>
                </TableRow>)}</TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
