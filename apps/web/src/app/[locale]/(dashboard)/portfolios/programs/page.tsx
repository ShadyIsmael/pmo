"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { Plus, Search } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { ProgramSheet } from "@/components/portfolios/ProgramSheet";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useManagedPrograms, type ProgramManagementListItem } from "@/hooks/portfolios/useProgramManagement";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useManagementPerspective } from "@/lib/perspective-context";

export default function ProgramsPage() {
  const t = useTranslations("programManager");
  const locale = useLocale();
  const router = useRouter();
  const { user } = useAuth();
  const { canMutate } = useManagementPerspective();
  const [search, setSearch] = useState("");
  const [sheetOpen, setSheetOpen] = useState(false);
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Program.Manage) && canMutate);
  const programs = useManagedPrograms(search);

  return <div className="space-y-5">
    <PageHeader title={t("myPrograms")} description={t("myProgramsDescription")} actions={canManage ? <Button size="sm" onClick={() => setSheetOpen(true)}><Plus className="me-2 h-4 w-4" />{t("program")}</Button> : undefined} />
    <div className="relative max-w-sm"><Search className="pointer-events-none absolute start-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={event => setSearch(event.target.value)} placeholder={t("searchPrograms")} className="ps-9" /></div>
    {programs.isLoading ? <div className="space-y-2"><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /><Skeleton className="h-12 w-full" /></div> : programs.isError ? <Card><CardContent className="py-10 text-center text-sm text-destructive">{t("loadFailed")}</CardContent></Card> : programs.data?.items.length ? <Card><CardContent className="p-0"><div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("program")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("manager")}</TableHead><TableHead>{t("portfolio")}</TableHead><TableHead>{t("start")}</TableHead><TableHead>{t("end")}</TableHead><TableHead>{t("projectCount")}</TableHead><TableHead>{t("attention")}</TableHead><TableHead>{t("schedule")}</TableHead><TableHead>{t("financial")}</TableHead></TableRow></TableHeader><TableBody>{programs.data.items.map(program => <ProgramListRow key={program.id} program={program} t={t} onOpen={() => router.push(`/${locale}/portfolios/programs/${program.id}`)} />)}</TableBody></Table></div></CardContent></Card> : <Card><CardContent className="py-12 text-center text-sm text-muted-foreground">{t("noPrograms")}</CardContent></Card>}
    <ProgramSheet open={sheetOpen} onOpenChange={setSheetOpen} />
  </div>;
}

function ProgramListRow({ program, t, onOpen }: { program: ProgramManagementListItem; t: ReturnType<typeof useTranslations>; onOpen: () => void }) {
  const date = (value?: string) => value ? new Date(value).toLocaleDateString() : "—";
  const label = (value: string) => ({ OnTrack: t("onTrack"), Delayed: t("overdue"), Unknown: t("unknown"), Available: t("financial"), Unavailable: t("notAvailable") }[value] ?? value);
  return <TableRow className="cursor-pointer" onClick={onOpen}><TableCell><button type="button" className="text-start font-medium hover:text-primary" onClick={event => { event.stopPropagation(); onOpen(); }}>{program.name}<span className="ms-1 text-xs text-muted-foreground">{program.code ?? ""}</span></button></TableCell><TableCell><Badge status={program.health}>{program.health ?? t("unknown")}</Badge></TableCell><TableCell><Badge status={program.status}>{program.status ?? t("unknown")}</Badge></TableCell><TableCell>{program.programManagerName ?? "—"}</TableCell><TableCell>{program.portfolioName ?? "—"}</TableCell><TableCell>{date(program.startDate)}</TableCell><TableCell>{date(program.endDate)}</TableCell><TableCell className="text-center">{program.projectCount}</TableCell><TableCell><Badge status={program.projectsRequiringAttention > 0 ? "Amber" : "Green"}>{program.projectsRequiringAttention}</Badge></TableCell><TableCell><Badge status={program.scheduleIndicator}>{label(program.scheduleIndicator)}</Badge></TableCell><TableCell><Badge status={program.financialIndicator}>{label(program.financialIndicator)}</Badge></TableCell></TableRow>;
}
