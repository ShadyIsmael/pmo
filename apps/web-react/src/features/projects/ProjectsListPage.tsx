import * as React from "react";
import { useProjects } from "./api/projects";
import { ProjectTable } from "./components/ProjectTable";
import { ProjectFilters } from "./components/ProjectFilters";
import { CreateProjectSheet } from "./components/CreateProjectSheet";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Plus } from "lucide-react";
import { useI18n } from "@i18n/messages";

export function ProjectsListPage() {
  const { t } = useI18n();
  const [createOpen, setCreateOpen] = React.useState(false);
  const [filters, setFilters] = React.useState({ search: "", status: "", priority: "", portfolioId: "", page: 1, pageSize: 20 });

  const query = useProjects(filters, true);

  const handleFilterChange = (newFilters: typeof filters) => {
    setFilters({ ...newFilters, page: 1 });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t("projects.title")}</h1>
          <p className="text-muted-foreground">{t("projects.description")}</p>
        </div>
        <Button onClick={() => setCreateOpen(true)} className="gap-2">
          <Plus className="h-4 w-4" />
          {t("projects.create")}
        </Button>
      </div>

      <Card>
        <CardHeader className="pb-0">
          <CardTitle>{t("projects.activeProjects")}</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <ProjectFilters filters={filters} onChange={handleFilterChange} />
          <ProjectTable data={query.data} isLoading={query.isLoading} error={query.isError} />
          {query.data && (
            <div className="flex items-center justify-between mt-4 text-sm text-muted-foreground">
              <span>{t("projects.pageOf", { page: query.data.page, totalPages: query.data.totalPages })}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => setFilters((f) => ({ ...f, page: f.page - 1 }))} disabled={query.data.page <= 1}>
                  {t("common.previous")}
                </Button>
                <Button variant="outline" size="sm" onClick={() => setFilters((f) => ({ ...f, page: f.page + 1 }))} disabled={query.data.page >= query.data.totalPages}>
                  {t("common.next")}
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {createOpen && <CreateProjectSheet open={createOpen} onOpenChange={setCreateOpen} />}
    </div>
  );
}