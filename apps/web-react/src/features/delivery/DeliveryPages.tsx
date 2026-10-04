import { Link, useParams } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useMeetings, useMeeting, useWorkItemsList, useWorkItem, useDocuments, usePortfolios, usePortfolio, usePrograms } from "./api/delivery";

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
            <TableBody>{items.map((m) => <TableRow key={m.id}><TableCell><Link to={`/meetings/${encodeURIComponent(m.id)}`} className="font-semibold hover:underline">{m.title}</Link></TableCell><TableCell className="text-muted-foreground">{formatDate(m.startTime)}</TableCell></TableRow>)}</TableBody></Table>}
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
      <Card><CardContent className="space-y-2 p-6 text-sm"><p>{m.projectName ?? ""}</p><p className="text-muted-foreground">{m.status ?? ""}</p></CardContent></Card>
    </div>
  );
}

export function WorkItemsPage() {
  const { t } = useI18n();
  const query = useWorkItemsList({ page: 1, pageSize: 50 });
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.workItems")} description={t("common.total", { total: items.length })} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("common.title")}</TableHead><TableHead>{t("common.type")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
            <TableBody>{items.map((w) => <TableRow key={w.id}><TableCell><Link to={`/work-items/${encodeURIComponent(w.id)}`} className="font-semibold hover:underline">{w.title}</Link></TableCell><TableCell>{w.type}</TableCell><TableCell>{w.status}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}

export function WorkItemDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useI18n();
  const query = useWorkItem(id ?? "", Boolean(id));
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !query.data) return <p className="py-12 text-center text-sm text-destructive">{t("common.notFound")}</p>;
  return (
    <div className="space-y-6">
      <PageHeader title={query.data.title} description={query.data.projectName ?? ""} />
      <Card><CardContent className="space-y-2 p-6 text-sm"><p>{t("common.status")}: {query.data.status}</p><p>{t("common.type")}: {query.data.type}</p></CardContent></Card>
    </div>
  );
}

export function DocumentsPage() {
  const { t } = useI18n();
  const query = useDocuments({});
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.documentCenter")} description={t("documents.description")} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
            <TableBody>{items.map((d) => <TableRow key={d.id}><TableCell className="font-medium">{d.name}</TableCell><TableCell>{d.status}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}

export function PortfoliosPage() {
  const { t } = useI18n();
  const query = usePortfolios(true);
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.portfolios")} description={t("portfolios.description")} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.code")}</TableHead></TableRow></TableHeader>
            <TableBody>{items.map((p) => <TableRow key={p.id}><TableCell><Link to={`/portfolios/${encodeURIComponent(p.id)}`} className="font-semibold hover:underline">{p.name}</Link></TableCell><TableCell className="font-mono text-xs">{p.code ?? "—"}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}

export function PortfolioDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useI18n();
  const query = usePortfolio(id ?? "", Boolean(id));
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError || !query.data) return <p className="py-12 text-center text-sm text-destructive">{t("common.notFound")}</p>;
  return (
    <div className="space-y-6">
      <PageHeader title={query.data.name} description={query.data.code ?? ""} />
      <Card><CardHeader><CardTitle>{t("common.details")}</CardTitle></CardHeader><CardContent className="text-sm text-muted-foreground">{query.data.status ?? ""}</CardContent></Card>
    </div>
  );
}

export function ProgramsPage() {
  const { t } = useI18n();
  const query = usePrograms(true);
  const items = (query.data as unknown as { items: { id: string; name: string; code?: string }[] } | undefined)?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.programs")} description={t("portfolios.programsDescription")} />
      <Card><CardContent className="p-0">
        {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
          : <Table><TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.code")}</TableHead></TableRow></TableHeader>
            <TableBody>{items.map((p) => <TableRow key={p.id}><TableCell className="font-medium">{p.name}</TableCell><TableCell className="font-mono text-xs">{p.code ?? "—"}</TableCell></TableRow>)}</TableBody></Table>}
      </CardContent></Card>
    </div>
  );
}
