import { Link, useParams } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useMeetings, useMeeting, useWorkItemsList, useWorkItem } from "./api/delivery";
import { useWorkItemStatuses } from "@api/workItems";
import { DocumentsCenterPage } from "@features/projects/ProjectDocumentsTab";
import { MeetingPublicLink } from "@lib/meeting-navigation";
export { ManagedPortfoliosPage as PortfoliosPage, ManagedPortfolioDetailPage as PortfolioDetailPage, ManagedProgramsPage as ProgramsPage, ManagedProgramDetailPage as ProgramDetailPage } from "./PortfolioProgramPages";

function meetingStatusLabel(status: string | undefined, t: (key: string) => string) {
  if (!status) return "—";
  const key = "meetings.status." + status.toLowerCase();
  const label = t(key);
  return label === key ? status : label;
}

function workItemStatusName(status: string, statuses: { code: string; nameEn: string; nameAr: string }[], locale: string) {
  const entry = statuses.find((item) => item.code === status);
  return entry ? locale === "ar" ? entry.nameAr : entry.nameEn : status;
}

function workItemTypeName(type: string, t: (key: string) => string) {
  if (type === "Issue") return t("common.issues");
  if (type === "Task") return t("common.tasks");
  return type;
}
export function MeetingsPage() {
  const { t, formatDate } = useI18n();
  const query = useMeetings({});
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("meetings.title")} description={t("meetings.description", { count: items.length })} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("meetings.emptyTitle")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("meetings.titleField")}</TableHead><TableHead>{t("meetings.dateTime")}</TableHead></TableRow></TableHeader>
            <TableBody>{items.map((m) => <TableRow key={m.id}><TableCell><MeetingPublicLink publicId={m.publicId} className="font-semibold hover:underline">{m.title}</MeetingPublicLink></TableCell><TableCell className="text-muted-foreground">{formatDate(m.startTime)}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}
export function MeetingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t, formatDate } = useI18n();
  const query = useMeeting(id ?? "", Boolean(id));
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !query.data) return <p className="py-12 text-center text-sm text-destructive">{t("meetings.notFoundTitle")}</p>;
  const m = query.data;
  return (
    <div className="space-y-6">
      <PageHeader title={m.title} description={formatDate(m.startTime)} />
      <Card><CardContent className="space-y-2 p-6 text-sm"><p>{m.projectName ?? ""}</p><p className="text-muted-foreground">{meetingStatusLabel(m.status, t)}</p></CardContent></Card>
    </div>
  );
}
export function WorkItemsPage() {
  const { t, locale } = useI18n();
  const query = useWorkItemsList({ page: 1, pageSize: 50 });
  const statusesQuery = useWorkItemStatuses();
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.workItems")} description={t("common.total", { total: items.length })} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("common.title")}</TableHead><TableHead>{t("common.type")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
            <TableBody>{items.map((w) => <TableRow key={w.id}><TableCell><Link to={`/work-items/${encodeURIComponent(w.id)}`} className="font-semibold hover:underline">{w.title}</Link></TableCell><TableCell>{workItemTypeName(w.type, t)}</TableCell><TableCell>{workItemStatusName(w.status, statusesQuery.data ?? [], locale)}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}
export function WorkItemDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t, locale } = useI18n();
  const query = useWorkItem(id ?? "", Boolean(id));
  const statusesQuery = useWorkItemStatuses(Boolean(id));
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !query.data) return <p className="py-12 text-center text-sm text-destructive">{t("common.notFound")}</p>;
  return (
    <div className="space-y-6">
      <PageHeader title={query.data.title} description={query.data.projectName ?? ""} />
      <Card><CardContent className="space-y-2 p-6 text-sm"><p>{t("common.status")}: {workItemStatusName(query.data.status, statusesQuery.data ?? [], locale)}</p><p>{t("common.type")}: {workItemTypeName(query.data.type, t)}</p></CardContent></Card>
    </div>
  );
}

export function DocumentsPage() {
  return <DocumentsCenterPage />;
}

