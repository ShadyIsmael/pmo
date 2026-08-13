"use client";

import { useParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { ManagerHandoverSections } from "@/components/portfolios/ManagerHandoverSections";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useProgram } from "@/hooks/portfolios/usePrograms";

export default function ProgramDetailPage() {
  const params = useParams<{ id: string }>();
  const t = useTranslations("managerHandover");
  const program = useProgram(params.id);
  if (program.isLoading) return <div className="space-y-6"><Skeleton className="h-10 w-1/2" /><Skeleton className="h-72 w-full" /></div>;
  if (program.isError || !program.data) return <div className="rounded-md border border-dashed p-10 text-center text-muted-foreground">{t("programLoadFailed")}</div>;
  const value = program.data;
  return <div className="space-y-6"><PageHeader title={value.name} description={value.description ?? value.code ?? t("programDescriptionFallback")} /><div className="grid gap-4 md:grid-cols-4"><Card><CardHeader><CardTitle className="text-sm">{t("status")}</CardTitle></CardHeader><CardContent><Badge variant="outline">{value.status ?? "—"}</Badge></CardContent></Card><Card><CardHeader><CardTitle className="text-sm">{t("portfolio")}</CardTitle></CardHeader><CardContent>{value.portfolio?.name ?? value.portfolioId}</CardContent></Card><Card><CardHeader><CardTitle className="text-sm">{t("budget")}</CardTitle></CardHeader><CardContent>{value.budget ?? "—"}</CardContent></Card><Card><CardHeader><CardTitle className="text-sm">{t("actualSpend")}</CardTitle></CardHeader><CardContent>{value.actualSpend ?? "—"}</CardContent></Card></div><ManagerHandoverSections type="programs" entityId={value.id} entityName={value.name} currentManagerId={value.programManagerId} startDate={value.startDate} endDate={value.endDate} /></div>;
}
