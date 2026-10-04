import { useOutletContext } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useDocuments } from "@features/delivery/api/delivery";

export function ProjectDocumentsTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t } = useI18n();
  const query = useDocuments({ projectId }, Boolean(projectId));
  const items = query.data?.items ?? [];
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError) return <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  if (items.length === 0) return <Card><CardContent className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</CardContent></Card>;
  return (
    <Card><CardHeader><CardTitle>{t("projects.tabs.documents")}</CardTitle></CardHeader>
      <CardContent className="p-0">
        <Table><TableHeader><TableRow><TableHead>{t("common.name")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader>
          <TableBody>{items.map((d) => <TableRow key={d.id}><TableCell className="font-medium">{d.name}</TableCell><TableCell>{d.status}</TableCell></TableRow>)}</TableBody></Table>
      </CardContent>
    </Card>
  );
}
