import { useOutletContext } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";

export function ProjectPlanningTab() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t } = useI18n();
  // Visible-first: plans load only when tab active.
  const query = useQuery({
    queryKey: ["projects", projectId, "plans"],
    queryFn: () => api.get<{ items: { id: string; name: string; status: string }[] }>(`/api/projects/${encodeURIComponent(projectId)}/plans`, { pageSize: 20 }),
    enabled: Boolean(projectId),
    staleTime: 30_000,
  });
  const items = query.data?.items ?? [];
  if (query.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (query.isError) return <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  if (items.length === 0) return <Card><CardContent className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</CardContent></Card>;
  return (
    <Card><CardHeader><CardTitle>{t("projects.tabs.planning")}</CardTitle></CardHeader>
      <CardContent className="space-y-2">
        {items.map((p) => <p key={p.id} className="border-b py-2 text-sm last:border-0"><strong>{p.name}</strong> <span className="text-muted-foreground">· {p.status}</span></p>)}
      </CardContent>
    </Card>
  );
}
