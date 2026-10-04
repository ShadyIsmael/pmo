import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { useWorkItems } from "@api/workItems";

export function IssuesPage() {
  const { t } = useI18n();
  // Issues are work-items of type Issue; visible-first single query.
  const query = useWorkItems({ page: 1, pageSize: 50, type: "Issue" });
  const items = query.data?.items ?? [];
  return (
    <div className="space-y-6">
      <PageHeader title={t("nav.issues")} description={t("risks.issuesDescription")} />
      <Card>
        <CardContent className="p-0">
          {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : query.isError ? <p className="p-6 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
            : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.noResults")}</p>
            : (
              <ul className="divide-y">
                {items.map((i) => (
                  <li key={i.id} className="flex items-center justify-between gap-3 px-4 py-3">
                    <span className="truncate text-sm font-medium">{i.title}</span>
                    <span className="shrink-0 text-xs text-muted-foreground">{i.status}</span>
                  </li>
                ))}
              </ul>
            )}
        </CardContent>
      </Card>
    </div>
  );
}
