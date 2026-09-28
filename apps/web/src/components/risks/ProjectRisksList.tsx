"use client";

import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { ArrowUpRight, FolderKanban, Plus, ShieldAlert } from "lucide-react";
import { RiskLevelText, RiskStatusText } from "@/components/risks/RiskValueText";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useProjectRisks } from "@/hooks/risks/useRisks";
import { PROJECT_DETAIL_PREVIEW_SIZE } from "@/lib/constants";

export function ProjectRisksList({ projectId, projectName }: { projectId: string; projectName: string }) {
  const t = useTranslations("risks");
  const projectT = useTranslations("projects");
  const common = useTranslations("common");
  const locale = useLocale();
  const query = useProjectRisks(projectId, PROJECT_DETAIL_PREVIEW_SIZE);
  const risks = query.data?.items ?? [];

  return (
    <Card className="shadow-none">
      <CardHeader className="flex flex-row items-start justify-between gap-3">
        <div><h2 className="flex items-center gap-1.5 text-sm font-semibold text-blue-700 dark:text-blue-300"><FolderKanban className="h-3.5 w-3.5 shrink-0" />{projectName}: {projectT("projectRisksTitle")}</h2><p className="mt-1 text-xs text-muted-foreground">{projectT("projectRisksDescription")}</p></div>
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-2"><Link href={`/${locale}/risks-issues/risks?projectId=${encodeURIComponent(projectId)}`}><Button size="sm" variant="outline"><ArrowUpRight className="me-1.5 h-3.5 w-3.5" />{common("viewAll")}</Button></Link><Link href={`/${locale}/risks-issues/risks/new?projectId=${encodeURIComponent(projectId)}`}><Button size="sm"><Plus className="me-1.5 h-3.5 w-3.5" />{t("create")}</Button></Link></div>
      </CardHeader>
      <CardContent>
        {query.isLoading ? <Skeleton className="h-24" /> : query.isError ? <p className="text-sm text-destructive">{t("loadError")}</p> : risks.length === 0 ? <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 text-center"><ShieldAlert className="h-7 w-7 text-muted-foreground/50" /><p className="mt-2 text-sm font-medium">{t("empty")}</p></div> : (
          <Table>
            <TableHeader><TableRow><TableHead>{t("titleField")}</TableHead><TableHead>{t("riskLevel")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("owner")}</TableHead></TableRow></TableHeader>
            <TableBody>{risks.map((risk) => <TableRow key={risk.id} className="hover:bg-muted/40"><TableCell className="font-medium"><Link className="text-primary underline-offset-4 hover:underline" href={`/${locale}/risks-issues/risks/${risk.id}?from=project&projectId=${encodeURIComponent(projectId)}`}>{risk.title}</Link></TableCell><TableCell><RiskLevelText value={risk.riskLevel} /></TableCell><TableCell><RiskStatusText value={risk.status} /></TableCell><TableCell>{risk.ownerResourceName || "—"}</TableCell></TableRow>)}</TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
