import { Link, useOutletContext } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useWorkItemsList } from "@features/delivery/api/delivery";

export function ProjectTasksTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t } = useI18n();
  // Visible-first: tasks load only when tab is active.
  const query = useWorkItemsList({ projectId, page: 1, pageSize: 50 }, Boolean(projectId));
  const items = query.data?.items ?? [];
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError) return <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  if (items.length === 0) return <Card><CardContent className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</CardContent></Card>;
  return (
    <Card><CardHeader><CardTitle>{t("projects.tabs.tasks")}</CardTitle></CardHeader>
      <CardContent className="p-0">
        <Table><TableHeader><TableRow><TableHead>{t("common.title")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
          <TableBody>{items.map((w) => <TableRow key={w.id}><TableCell><Link to={`/work-items/${encodeURIComponent(w.id)}`} className="font-semibold hover:underline">{w.title}</Link></TableCell><TableCell>{w.status}</TableCell></TableRow>)}</TableBody></Table>
      </CardContent>
    </Card>
  );
}
