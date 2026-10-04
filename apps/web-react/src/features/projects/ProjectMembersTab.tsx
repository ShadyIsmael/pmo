import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useI18n } from "@i18n/messages";
import { useProjectPeople, useAddProjectPerson } from "./api/projects";
import { useResources } from "@features/resources/api/resources";
import { Plus } from "lucide-react";

export function ProjectMembersTab() {
  const { projectId } = useOutletContext<{ projectId: string; project: { id: string } }>();
  const { t } = useI18n();
  // Visible-first: people load only when tab active.
  const peopleQuery = useProjectPeople(projectId, Boolean(projectId));
  const addPerson = useAddProjectPerson(projectId);
  const [open, setOpen] = React.useState(false);
  // Eligible Resources load on demand only when Add dialog opens.
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true }, open);
  const [resourceId, setResourceId] = React.useState("");
  const people = peopleQuery.data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <CardTitle>{t("projects.tabs.members")}</CardTitle>
        <Button size="sm" onClick={() => setOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("projects.addProjectPerson")}</Button>
      </CardHeader>
      <CardContent>
        {peopleQuery.isLoading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
          : people.length === 0 ? <p className="py-8 text-center text-sm text-muted-foreground">{t("projects.noProjectPeople")}</p>
          : (
            <div className="space-y-2">
              {people.map((person) => (
                <div key={person.id} className="flex items-center justify-between rounded-lg border p-3">
                  <div className="flex items-center gap-3">
                    <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10">
                      <span className="text-sm font-medium text-primary">{person.name?.split(" ").map((p: string) => p[0]).join("") || "U"}</span>
                    </div>
                    <div>
                      <p className="font-medium">{person.name}</p>
                      <p className="text-sm text-muted-foreground">{person.relationshipType}</p>
                    </div>
                  </div>
                  <span className="text-sm text-muted-foreground">{person.role || "—"}</span>
                </div>
              ))}
            </div>
          )}
      </CardContent>
      <SideSheet open={open} onOpenChange={setOpen} title={t("projects.addProjectPerson")}>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (!resourceId) return;
            addPerson.mutate({ resourceId, relationshipType: "TeamMember" }, { onSuccess: () => { setOpen(false); setResourceId(""); } });
          }}
          className="flex min-h-0 flex-1 flex-col"
        >
          <SideSheetBody className="space-y-3">
            <div className="space-y-1.5">
              <Label>{t("projects.selectResource")}</Label>
              <select className="input-field" value={resourceId} onChange={(e) => setResourceId(e.target.value)} required>
                <option value="">{t("common.select")}</option>
                {(resourcesQuery.data?.items ?? []).map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
              </select>
            </div>
          </SideSheetBody>
          <SideSheetFooter>
            <Button type="submit" disabled={addPerson.isPending || !resourceId}>{t("common.add")}</Button>
          </SideSheetFooter>
        </form>
      </SideSheet>
    </Card>
  );
}
