import * as React from "react";
import { Link, useLocation, useParams, useSearchParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle2, ShieldCheck } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Checkbox } from "@components/ui/Checkbox";
import { Label } from "@components/ui/Label";
import { Badge } from "@components/ui/Badge";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { formatNumberValue } from "@lib/formatting/number";
import { api, ApiError } from "@api/client";
import { PageHeader } from "@components/layout/PageHeader";
import { useProjectEnumLabels } from "@features/projects/lib/project-labels";

interface InvitationValidation {
  resourceName: string;
  email: string;
  projectName: string;
  status?: string;
  reportsConsent?: boolean;
  notificationsConsent?: boolean;
}
interface InvitationAcceptance {
  status: string;
  reportsConsent: boolean;
  notificationsConsent: boolean;
}
interface StakeholderProjectView {
  id: string;
  publicId: string;
  code: string;
  name: string;
  description?: string;
  status: string;
  statusNameEn?: string | null;
  statusNameAr?: string | null;
  health: string;
  progress: number;
  startDate?: string;
  endDate?: string;
  projectManagerName?: string;
  sponsorName?: string;
  latestPublishedStatusReport?: {
    reportDate: string;
    periodFrom: string;
    periodTo: string;
    projectLifecycleStatus?: string;
    projectLifecycleStatusNameEn?: string | null;
    projectLifecycleStatusNameAr?: string | null;
    overallHealth?: string;
    overallProgressPercent?: number;
    scheduleHealth?: string;
    costHealth?: string;
    scopeHealth?: string;
    qualityHealth?: string;
    resourceHealth?: string;
    executiveSummary?: string;
    keyAchievements?: string;
    challengesConcerns?: string;
    nextPeriodPriorities?: string;
    preparedByName?: string;
    publishedByName?: string;
    publishedAt?: string;
  };
  publishedStatusHistory: { id: string; reportDate: string; periodFrom: string; periodTo: string; projectLifecycleStatus?: string; projectLifecycleStatusNameEn?: string | null; projectLifecycleStatusNameAr?: string | null; overallHealth?: string; overallProgressPercent?: number; publishedAt?: string }[];
  milestones: { id: string; name: string; dueDate?: string; completedDate?: string; status?: string; progress?: number }[];
}

// Public route (no auth): /stakeholder/accept-invitation#token=...; query tokens remain accepted for older links.
export function AcceptInvitationPage() {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const { hash } = useLocation();
  const token = params.get("token") ?? new URLSearchParams(hash.replace(/^#/, "")).get("token") ?? "";
  const [acceptParticipation, setAcceptParticipation] = React.useState(false);
  const [receiveReports, setReceiveReports] = React.useState(false);
  const [receiveNotifications, setReceiveNotifications] = React.useState(false);
  const validation = useQuery({
    queryKey: ["stakeholder-invitation", "validate", token],
    queryFn: () => api.post<InvitationValidation>("/api/stakeholder-invitations/validate", { token }),
    enabled: token.length > 0,
    retry: false,
  });
  const accept = useMutation({ mutationFn: () => api.post<InvitationAcceptance>("/api/stakeholder-invitations/accept", {
    token, acceptParticipation, receiveProjectReports: receiveReports, receiveProjectNotifications: receiveNotifications,
  }) });
  const decline = useMutation({ mutationFn: () => api.post<void>("/api/stakeholder-invitations/decline", { token }) });
  const withdrawConsent = useMutation({ mutationFn: (preferences: { withdrawReportsConsent: boolean; withdrawNotificationsConsent: boolean }) =>
    api.post<InvitationAcceptance>("/api/stakeholder-invitations/withdraw-consent", { token, ...preferences }) });

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    await accept.mutateAsync();
  }

  if (decline.isSuccess) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
        <Card className="w-full max-w-lg"><CardContent className="space-y-5 p-6 text-center">
          <CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" />
          <h1 className="text-xl font-semibold">{t("stakeholder.invitationDeclined")}</h1>
          <p className="text-sm text-muted-foreground">{t("stakeholder.externalInvitationNoAccount")}</p>
        </CardContent></Card>
      </main>
    );
  }
  if (!token || validation.isError) {
    const message = validation.error instanceof ApiError && validation.error.message.toLowerCase().includes("expired")
      ? t("stakeholder.invitationExpired")
      : validation.error instanceof ApiError && validation.error.message.toLowerCase().includes("revoked")
        ? t("stakeholder.invitationRevoked")
        : t("stakeholder.invitationInvalid");
    return (
      <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
        <Card className="w-full max-w-lg"><CardContent className="space-y-3 p-6 text-center">
          <ShieldCheck className="mx-auto h-10 w-10 text-muted-foreground" />
          <h1 className="text-xl font-semibold">{message}</h1>
          <p className="text-sm text-muted-foreground">{t("stakeholder.invitationAcceptError")}</p>
        </CardContent></Card>
      </main>
    );
  }
  if (validation.isLoading || !validation.data) return <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4"><Card className="w-full max-w-lg"><CardContent className="space-y-4 p-6" role="status" aria-label={t("common.loading")}><Skeleton className="mx-auto h-5 w-28" /><Skeleton className="mx-auto h-8 w-3/4" /><Skeleton className="h-4 w-full" /><Skeleton className="h-24 w-full" /></CardContent></Card></main>;
  const currentConsent = withdrawConsent.data ?? accept.data ?? validation.data;
  if (currentConsent.status === "Accepted") {
    return (
      <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
        <Card className="w-full max-w-lg">
          <CardHeader>
            <div className="mb-2 flex items-center gap-2"><Badge variant="outline">{t("stakeholder.stakeholder")}</Badge><ShieldCheck className="h-4 w-4 text-emerald-600" /></div>
            <CardTitle>{t("stakeholder.invitationAccepted")}</CardTitle>
            <p className="text-sm text-muted-foreground">{t("stakeholder.manageConsentDescription")}</p>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">{t("stakeholder.externalInvitationNoAccount")}</p>
            <section className="space-y-3 rounded-lg border bg-muted/20 p-4" aria-label={t("stakeholder.manageConsentTitle")}>
              <h2 className="text-sm font-semibold">{t("stakeholder.manageConsentTitle")}</h2>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2"><span>{t("stakeholder.receiveProjectReports")}</span><Badge variant="outline">{t(currentConsent.reportsConsent ? "stakeholder.emailPreferenceOn" : "stakeholder.emailPreferenceOff")}</Badge></div>
                {currentConsent.reportsConsent ? <Button type="button" size="sm" variant="outline" disabled={withdrawConsent.isPending} onClick={() => void withdrawConsent.mutateAsync({ withdrawReportsConsent: true, withdrawNotificationsConsent: false })}>{t("stakeholder.withdrawReportsConsent")}</Button> : null}
              </div>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2"><span>{t("stakeholder.receiveProjectNotifications")}</span><Badge variant="outline">{t(currentConsent.notificationsConsent ? "stakeholder.emailPreferenceOn" : "stakeholder.emailPreferenceOff")}</Badge></div>
                {currentConsent.notificationsConsent ? <Button type="button" size="sm" variant="outline" disabled={withdrawConsent.isPending} onClick={() => void withdrawConsent.mutateAsync({ withdrawReportsConsent: false, withdrawNotificationsConsent: true })}>{t("stakeholder.withdrawNotificationsConsent")}</Button> : null}
              </div>
            </section>
            {withdrawConsent.error ? <p className="text-sm text-destructive" role="alert">{t("stakeholder.consentWithdrawalError")}</p> : null}
          </CardContent>
        </Card>
      </main>
    );
  }
  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <div className="mb-2 flex items-center gap-2"><Badge variant="outline">{t("stakeholder.stakeholder")}</Badge><ShieldCheck className="h-4 w-4 text-emerald-600" /></div>
          <CardTitle>{t("stakeholder.acceptInvitationTitle")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("stakeholder.acceptInvitationDescription")}</p>
        </CardHeader>
        <CardContent>
          <form className="space-y-5" onSubmit={(e) => void submit(e)}>
            <section className="space-y-3 rounded-lg border bg-muted/20 p-4">
              <div><p className="text-xs text-muted-foreground">{t("stakeholder.acceptInvitationResource")}</p><p className="font-medium">{validation.data.resourceName}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("stakeholder.acceptInvitationEmail")}</p><p className="font-medium">{validation.data.email}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("stakeholder.acceptInvitationProject")}</p><p className="font-medium">{validation.data.projectName}</p></div>
            </section>
            <p className="text-sm text-muted-foreground">{t("stakeholder.externalInvitationNoAccount")}</p>
            <div className="flex items-start gap-2"><Checkbox id="stakeholder-participation" checked={acceptParticipation} onCheckedChange={() => setAcceptParticipation((value) => !value)} required /><Label htmlFor="stakeholder-participation">{t("stakeholder.acceptParticipationConsent")}</Label></div>
            <div className="flex items-start gap-2"><Checkbox id="stakeholder-report-consent" checked={receiveReports} onCheckedChange={() => setReceiveReports((value) => !value)} /><Label htmlFor="stakeholder-report-consent">{t("stakeholder.receiveProjectReports")}</Label></div>
            <div className="flex items-start gap-2"><Checkbox id="stakeholder-notification-consent" checked={receiveNotifications} onCheckedChange={() => setReceiveNotifications((value) => !value)} /><Label htmlFor="stakeholder-notification-consent">{t("stakeholder.receiveProjectNotifications")}</Label></div>
            {accept.error || decline.error ? <p className="text-sm text-destructive" role="alert">{(accept.error ?? decline.error) instanceof Error ? (accept.error ?? decline.error)?.message : t("stakeholder.invitationAcceptError")}</p> : null}
            <div className="flex flex-col gap-2 sm:flex-row">
              <Button className="flex-1" type="submit" disabled={accept.isPending || decline.isPending || !acceptParticipation}>{accept.isPending ? t("stakeholder.submitting") : t("stakeholder.acceptInvitation")}</Button>
              <Button className="flex-1" type="button" variant="outline" disabled={accept.isPending || decline.isPending} onClick={() => void decline.mutateAsync()}>{decline.isPending ? t("stakeholder.submitting") : t("stakeholder.declineInvitation")}</Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}

export function StakeholderProjectsPage() {
  const { t, locale, formatDate } = useI18n();
  const { healthLabel } = useProjectEnumLabels();
  const query = useQuery({
    queryKey: ["stakeholder", "projects"],
    queryFn: () => api.get<{ items: StakeholderProjectView[] }>("/api/stakeholder/projects", { page: 1, pageSize: 20 }),
    staleTime: 30_000,
  });
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-4">
      <PageHeader title={t("stakeholder.title")} description={t("stakeholder.description")} />
      {query.isLoading ? <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        : query.isError ? <div className="space-y-2 py-8 text-center"><p className="text-sm text-destructive" role="alert">{t("stakeholder.loadError")}</p><Button variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div>
          : items.length === 0 ? <div className="py-12 text-center"><p className="text-sm font-medium">{t("stakeholder.emptyTitle")}</p><p className="mt-1 text-sm text-muted-foreground">{t("stakeholder.emptyDescription")}</p></div>
            : <Card className="shadow-none"><CardContent className="p-0"><Table>
              <TableHeader><TableRow><TableHead>{t("stakeholder.project")}</TableHead><TableHead>{t("stakeholder.status")}</TableHead><TableHead>{t("stakeholder.health")}</TableHead><TableHead>{t("stakeholder.progress")}</TableHead><TableHead>{t("stakeholder.dates")}</TableHead><TableHead>{t("stakeholder.latestUpdate")}</TableHead><TableHead className="text-end">{t("common.actions")}</TableHead></TableRow></TableHeader>
              <TableBody>{items.map((project) => <TableRow key={project.id}>
                <TableCell><Link to={`/stakeholder/projects/${encodeURIComponent(project.publicId)}`} className="font-semibold text-primary hover:underline">{project.name}</Link><p className="text-xs text-muted-foreground">{project.code}</p></TableCell>
                <TableCell><Badge status={project.status}>{stakeholderProjectStatusLabel(project.status, project.statusNameEn, project.statusNameAr, locale)}</Badge></TableCell>
                <TableCell><Badge status={project.health}>{healthLabel(project.health)}</Badge></TableCell>
                <TableCell className="tabular-nums">{formatLocalizedNumber(project.progress, locale)} %</TableCell>
                <TableCell className="whitespace-nowrap text-xs">{formatStakeholderDate(project.startDate, formatDate)} – {formatStakeholderDate(project.endDate, formatDate)}</TableCell>
                <TableCell className="whitespace-nowrap text-xs">{project.latestPublishedStatusReport ? formatStakeholderDate(project.latestPublishedStatusReport.reportDate, formatDate) : t("stakeholder.noPublishedUpdate")}</TableCell>
                <TableCell className="text-end"><Link to={`/stakeholder/projects/${encodeURIComponent(project.publicId)}`} className="text-sm font-medium text-primary hover:underline">{t("common.view")}</Link></TableCell>
              </TableRow>)}</TableBody>
            </Table></CardContent></Card>}
    </div>
  );
}

export function StakeholderProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t, locale, formatDate } = useI18n();
  const { statusLabel: milestoneStatusLabel, healthLabel } = useProjectEnumLabels();
  const query = useQuery({
    queryKey: ["stakeholder", "project", id],
    queryFn: () => api.get<StakeholderProjectView>(`/api/stakeholder/projects/${encodeURIComponent(id ?? "")}`),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
  const project = query.data;
  if (query.isLoading) return <div className="space-y-4"><PageHeader title={t("stakeholder.detailTitle")} description={t("stakeholder.detailDescription")} /><p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p></div>;
  if (query.isError || !project) return <div className="space-y-4"><PageHeader title={t("stakeholder.detailTitle")} description={t("stakeholder.detailDescription")} /><div className="space-y-2 py-8 text-center"><p className="text-sm text-destructive" role="alert">{t("stakeholder.notAvailableDescription")}</p><Button variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div><Link to="/stakeholder/projects" className="inline-flex items-center text-sm text-primary hover:underline"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("stakeholder.backToProjects")}</Link></div>;
  const report = project.latestPublishedStatusReport;
  const milestones = project.milestones ?? [];
  const history = project.publishedStatusHistory ?? [];
  return <div className="space-y-6">
    <PageHeader title={project.name} description={`${project.code}${project.description ? ` · ${project.description}` : ""}`} actions={<Badge status={project.status}>{stakeholderProjectStatusLabel(project.status, project.statusNameEn, project.statusNameAr, locale)}</Badge>} />
    <Link to="/stakeholder/projects" className="inline-flex h-9 items-center rounded-md px-3 text-sm font-medium text-primary hover:bg-accent"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("stakeholder.backToProjects")}</Link>
    <div className="grid gap-4 md:grid-cols-4"><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("stakeholder.status")}</p><Badge className="mt-2" status={project.status}>{stakeholderProjectStatusLabel(project.status, project.statusNameEn, project.statusNameAr, locale)}</Badge></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("stakeholder.health")}</p><Badge className="mt-2" status={project.health}>{healthLabel(project.health)}</Badge></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("stakeholder.progress")}</p><p className="mt-2 text-lg font-semibold">{formatLocalizedNumber(project.progress, locale)}%</p></CardContent></Card><Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{t("stakeholder.dates")}</p><p className="mt-2 text-sm">{formatStakeholderDate(project.startDate, formatDate)} – {formatStakeholderDate(project.endDate, formatDate)}</p></CardContent></Card></div>
    <Card><CardHeader><CardTitle>{t("stakeholder.peopleTitle")}</CardTitle></CardHeader><CardContent className="grid gap-3 text-sm sm:grid-cols-2"><p>{t("stakeholder.projectManager")}: {project.projectManagerName ?? "—"}</p><p>{t("stakeholder.sponsor")}: {project.sponsorName ?? "—"}</p><p className="text-xs text-muted-foreground sm:col-span-2">{t("stakeholder.publishedOnlyHint")}</p></CardContent></Card>
    <Card><CardHeader><CardTitle>{t("stakeholder.latestUpdate")}</CardTitle><p className="text-xs text-muted-foreground">{t("stakeholder.publishedOnlyHint")}</p></CardHeader><CardContent className="space-y-4">{report ? <><div className="flex flex-wrap gap-2"><Badge status={report.projectLifecycleStatus ?? project.status}>{stakeholderProjectStatusLabel(report.projectLifecycleStatus || project.status, report.projectLifecycleStatus ? report.projectLifecycleStatusNameEn : project.statusNameEn, report.projectLifecycleStatus ? report.projectLifecycleStatusNameAr : project.statusNameAr, locale)}</Badge>{report.overallHealth ? <Badge status={report.overallHealth}>{healthLabel(report.overallHealth)}</Badge> : null}<span className="text-sm text-muted-foreground">{formatStakeholderDate(report.reportDate, formatDate)} · {formatStakeholderDate(report.periodFrom, formatDate)} – {formatStakeholderDate(report.periodTo, formatDate)}</span></div><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{([["stakeholder.scheduleHealth", report.scheduleHealth], ["stakeholder.costHealth", report.costHealth], ["stakeholder.scopeHealth", report.scopeHealth], ["stakeholder.qualityHealth", report.qualityHealth], ["stakeholder.resourceHealth", report.resourceHealth]] as const).filter(([, value]) => Boolean(value)).map(([label, value]) => <div key={label} className="flex items-center justify-between rounded-lg border bg-muted/20 p-3"><span className="text-xs text-muted-foreground">{t(label)}</span><Badge status={value}>{healthLabel(value)}</Badge></div>)}</div>{([["stakeholder.executiveSummary", report.executiveSummary], ["stakeholder.keyAchievements", report.keyAchievements], ["stakeholder.challengesConcerns", report.challengesConcerns], ["stakeholder.nextPeriodPriorities", report.nextPeriodPriorities]] as const).map(([label, value]) => value ? <div key={label}><p className="text-sm font-semibold">{t(label)}</p><p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">{value}</p></div> : null)}</> : <div><p className="text-sm font-medium">{t("stakeholder.noPublishedUpdate")}</p><p className="mt-1 text-sm text-muted-foreground">{t("stakeholder.noPublishedUpdateDescription")}</p></div>}</CardContent></Card>
    <Card><CardHeader><CardTitle>{t("stakeholder.milestones")}</CardTitle><p className="text-xs text-muted-foreground">{t("stakeholder.milestonesHint")}</p></CardHeader><CardContent>{milestones.length ? <Table><TableHeader><TableRow><TableHead>{t("stakeholder.milestone")}</TableHead><TableHead>{t("stakeholder.dueDate")}</TableHead><TableHead>{t("stakeholder.status")}</TableHead><TableHead>{t("stakeholder.progress")}</TableHead></TableRow></TableHeader><TableBody>{milestones.map((milestone) => <TableRow key={milestone.id}><TableCell className="font-medium">{milestone.name}</TableCell><TableCell>{formatStakeholderDate(milestone.dueDate, formatDate)}</TableCell><TableCell><Badge status={milestone.status}>{milestone.status ? milestoneStatusLabel(milestone.status) : t("stakeholder.notStarted")}</Badge></TableCell><TableCell className="tabular-nums">{milestone.progress == null ? "—" : `${formatLocalizedNumber(milestone.progress, locale)} %`}</TableCell></TableRow>)}</TableBody></Table> : <div><p className="text-sm font-medium">{t("stakeholder.noMilestones")}</p><p className="mt-1 text-sm text-muted-foreground">{t("stakeholder.noMilestonesDescription")}</p></div>}</CardContent></Card>
    <Card><CardHeader><CardTitle>{t("stakeholder.statusHistory")}</CardTitle><p className="text-xs text-muted-foreground">{t("stakeholder.publishedOnlyHint")}</p></CardHeader><CardContent>{history.length ? <Table><TableHeader><TableRow><TableHead>{t("stakeholder.reportDate")}</TableHead><TableHead>{t("stakeholder.reportingPeriod")}</TableHead><TableHead>{t("stakeholder.status")}</TableHead><TableHead>{t("stakeholder.health")}</TableHead><TableHead>{t("stakeholder.progress")}</TableHead></TableRow></TableHeader><TableBody>{history.map((entry) => <TableRow key={entry.id}><TableCell>{formatStakeholderDate(entry.reportDate, formatDate)}</TableCell><TableCell>{formatStakeholderDate(entry.periodFrom, formatDate)} – {formatStakeholderDate(entry.periodTo, formatDate)}</TableCell><TableCell><Badge status={entry.projectLifecycleStatus}>{stakeholderProjectStatusLabel(entry.projectLifecycleStatus || project.status, entry.projectLifecycleStatus ? entry.projectLifecycleStatusNameEn : project.statusNameEn, entry.projectLifecycleStatus ? entry.projectLifecycleStatusNameAr : project.statusNameAr, locale)}</Badge></TableCell><TableCell><Badge status={entry.overallHealth}>{entry.overallHealth ? healthLabel(entry.overallHealth) : "—"}</Badge></TableCell><TableCell className="tabular-nums">{entry.overallProgressPercent == null ? "—" : `${formatLocalizedNumber(entry.overallProgressPercent, locale)} %`}</TableCell></TableRow>)}</TableBody></Table> : <div><p className="text-sm font-medium">{t("stakeholder.noPublishedHistory")}</p><p className="mt-1 text-sm text-muted-foreground">{t("stakeholder.noPublishedHistoryDescription")}</p></div>}</CardContent></Card>
  </div>;
}

function formatStakeholderDate(value: string | null | undefined, formatDate: (date: Date | string, options?: Intl.DateTimeFormatOptions) => string): string {
  return value ? formatDate(value, { numberingSystem: "latn" }) : "—";
}

function formatLocalizedNumber(value: number, locale: string): string {
  return formatNumberValue(value, locale);
}

function stakeholderProjectStatusLabel(code: string | null | undefined, english: string | null | undefined, arabic: string | null | undefined, locale: string) {
  if (locale === "ar" && arabic?.trim()) return arabic;
  if (locale !== "ar" && english?.trim()) return english;
  return code?.trim() || "—";
}

export function useInvalidateStakeholder() {
  const client = useQueryClient();
  return () => { void client.invalidateQueries({ queryKey: ["stakeholder"] }); };
}
