import { Link, useOutletContext } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useRisks } from "@features/risks/api/risks";

export function ProjectRisksTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t } = useI18n();
  const query = useRisks({ projectId }, Boolean(projectId));
  const items = query.data?.items ?? [];
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError) return <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  if (items.length === 0) return <Card><CardContent className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</CardContent></Card>;
  return (
    <Card><CardHeader><CardTitle>{t("projects.tabs.risks")}</CardTitle></CardHeader>
      <CardContent className="p-0">
        <Table><TableHeader><TableRow><TableHead>{t("risks.titleField")}</TableHead><TableHead>{t("risks.riskLevel")}</TableHead><TableHead>{t("risks.status")}</TableHead></TableRow></TableHeader>
          <TableBody>{items.map((r) => <TableRow key={r.id}><TableCell><Link to={`/risks-issues/risks/${encodeURIComponent(r.id)}`} className="font-semibold hover:underline">{r.title}</Link></TableCell><TableCell>{r.riskLevel}</TableCell><TableCell>{r.status}</TableCell></TableRow>)}</TableBody></Table>
      </CardContent>
    </Card>
  );
}
