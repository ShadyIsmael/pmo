"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { Plus, Search } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { PortfolioSheet } from "@/components/portfolios/PortfolioSheet";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState, ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useManagedPortfolios } from "@/hooks/portfolios/usePortfolioManagement";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useManagementPerspective } from "@/lib/perspective-context";

export default function PortfoliosPage() {
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("portfolioManager");
  const [search, setSearch] = useState("");
  const [sheetOpen, setSheetOpen] = useState(false);
  const portfolios = useManagedPortfolios(search);
  const { user } = useAuth();
  const { canMutate } = useManagementPerspective();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Portfolio.Manage) && canMutate);

  return <div className="space-y-6">
    <PageHeader title={t("myPortfolios")} description={t("myPortfoliosDescription")} actions={canManage ? <Button size="sm" onClick={() => setSheetOpen(true)}><Plus className="me-2 h-4 w-4" />{t("createPortfolio")}</Button> : undefined} />
    <Card><CardHeader className="pb-3"><CardTitle className="text-base">{t("portfolioScope")}</CardTitle><CardDescription>{t("scopeDescription")}</CardDescription><div className="relative max-w-sm"><Search className="absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("searchPortfolios")} className="ps-9" /></div></CardHeader><CardContent className="p-0">
      {portfolios.isLoading ? <div className="p-4"><LoadingSkeleton rows={6} /></div> : portfolios.isError ? <ErrorState title={t("loadFailed")} onRetry={() => void portfolios.refetch()} /> : portfolios.data?.items.length ? <Table><TableHeader><TableRow><TableHead>{t("portfolio")}</TableHead><TableHead>{t("owner")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("programs")}</TableHead><TableHead>{t("directProjects")}</TableHead><TableHead>{t("totalProjects")}</TableHead><TableHead>{t("attention")}</TableHead><TableHead>{t("financial")}</TableHead><TableHead>{t("risk")}</TableHead><TableHead>{t("reporting")}</TableHead></TableRow></TableHeader><TableBody>{portfolios.data.items.map((item) => <TableRow key={item.id} className="cursor-pointer" onClick={() => router.push(`/${locale}/portfolios/${item.id}`)}><TableCell><div className="font-medium">{item.name}</div><div className="text-xs text-muted-foreground">{item.code ?? "—"} · {item.currency}</div></TableCell><TableCell>{item.ownerName ?? "—"}</TableCell><TableCell><Badge status={item.status}>{item.status ?? t("unknown")}</Badge></TableCell><TableCell><Badge status={item.health}>{item.health ?? t("unknown")}</Badge></TableCell><TableCell>{item.programCount}</TableCell><TableCell>{item.directProjectCount}</TableCell><TableCell>{item.totalProjectCount}</TableCell><TableCell>{item.projectsRequiringAttention + item.programsRequiringAttention}</TableCell><TableCell><Badge status={item.financialIndicator}>{labelForState(t, item.financialIndicator)}</Badge></TableCell><TableCell><Badge status={item.riskIndicator}>{labelForState(t, item.riskIndicator)}</Badge></TableCell><TableCell><Badge status={item.reportingIndicator}>{labelForState(t, item.reportingIndicator)}</Badge></TableCell></TableRow>)}</TableBody></Table> : <EmptyState title={t("noPortfolios")} description={t("scopeDescription")} />}
    </CardContent></Card>
    <PortfolioSheet open={sheetOpen} onOpenChange={setSheetOpen} />
  </div>;
}

function labelForState(t: ReturnType<typeof useTranslations>, value?: string) { const labels: Record<string, string> = { Available: t("available"), Unavailable: t("unavailable"), Clear: t("clear"), NeedsAttention: t("needsAttention"), Critical: t("critical"), Current: t("currentReports"), Stale: t("staleReports"), Missing: t("missingReports"), Unknown: t("unknown") }; return labels[value ?? ""] ?? value ?? t("unknown"); }
