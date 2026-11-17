import * as React from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { AlertTriangle, FolderKanban, Loader2 } from "lucide-react";
import { useAuth } from "@auth/AuthProvider";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { useProjects, useProject } from "@features/projects/api/projects";
import { useRiskWorkflowUserLookup } from "@features/projects/lib/project-lookups";
import { useCreateRisk, type RiskImpactType, type RiskLevel } from "./api/risks";

const RISK_LEVELS: RiskLevel[] = ["Low", "Medium", "High", "Critical"];
const RISK_IMPACT_TYPES: RiskImpactType[] = ["Schedule", "Budget", "Scope", "Quality", "Resources", "Procurement", "Compliance", "Reputation", "Operations", "Other"];

interface RiskCreatePageProps {
  embedded?: boolean;
  onCreated?: () => void;
  onCancel?: () => void;
}

type RiskForm = {
  projectId: string;
  title: string;
  description: string;
  riskLevel: RiskLevel | "";
  ownerUserId: string;
  ownerResourceId: string;
  impactTypes: RiskImpactType[];
  impactDescription: string;
};

export function RiskCreatePage({ embedded = false, onCreated, onCancel }: RiskCreatePageProps) {
  const { t } = useI18n();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const initialProjectId = searchParams.get("projectId") ?? "";
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Risk.Create));
  const projectQuery = useProject(initialProjectId);
  const projectsQuery = useProjects({ page: 1, pageSize: 100 }, !initialProjectId);
  const createRisk = useCreateRisk();
  const [form, setForm] = React.useState<RiskForm>({ projectId: "", title: "", description: "", riskLevel: "", ownerUserId: "", ownerResourceId: "", impactTypes: [], impactDescription: "" });
  const usersQuery = useRiskWorkflowUserLookup(canCreate, form.projectId || undefined);
  const [submitted, setSubmitted] = React.useState(false);
  const [saveError, setSaveError] = React.useState(false);
  React.useEffect(() => {
    if (initialProjectId && projectQuery.data) setForm((current) => ({ ...current, projectId: projectQuery.data!.id }));
  }, [initialProjectId, projectQuery.data]);
  const selectedProject = initialProjectId ? projectQuery.data : projectsQuery.data?.items.find((project) => project.id === form.projectId);
  const projectLoading = initialProjectId ? projectQuery.isLoading : projectsQuery.isLoading;
  const projectUnavailable = initialProjectId ? projectQuery.isError || (!projectQuery.isLoading && !selectedProject) : projectsQuery.isError;
  const projectLoadFailed = initialProjectId ? projectQuery.isError : projectsQuery.isError;
  const returnTo = initialProjectId ? `/projects/${encodeURIComponent(initialProjectId)}/risks-issues` : "/risks-issues/risks";
  const required = (value: string | undefined) => submitted && !value?.trim() ? <p role="alert" className="text-xs text-destructive">{t("risks.required")}</p> : null;

  function toggleImpact(impactType: RiskImpactType) {
    setForm((current) => ({ ...current, impactTypes: current.impactTypes.includes(impactType) ? current.impactTypes.filter((item) => item !== impactType) : [...current.impactTypes, impactType] }));
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitted(true);
    setSaveError(false);
    if (!form.projectId || !selectedProject || !form.title.trim() || !form.description.trim() || !form.riskLevel || (form.impactTypes.length > 0 && !form.impactDescription.trim())) return;
    const submitter = (event.nativeEvent as SubmitEvent).submitter;
    const intent = submitter instanceof HTMLButtonElement ? submitter.value : "save";
    try {
      const risk = await createRisk.mutateAsync({
        projectId: form.projectId,
        title: form.title.trim(),
        description: form.description.trim(),
        riskLevel: form.riskLevel,
        ownerUserId: form.ownerUserId || undefined,
        ownerResourceId: form.ownerResourceId || undefined,
        impactAssessment: form.impactTypes.length ? { impactTypes: form.impactTypes, description: form.impactDescription.trim() } : undefined,
      });
      if (intent === "save-and-add-action") {
        const origin = initialProjectId ? `from=project&projectId=${encodeURIComponent(initialProjectId)}` : "from=risks";
        navigate(`/risks-issues/risks/${encodeURIComponent(risk.id)}?tab=actions&addAction=1&${origin}`);
      } else if (embedded) {
        onCreated?.();
      } else {
        navigate(returnTo);
      }
    } catch {
      setSaveError(true);
    }
  }

  if (!canCreate) {
    const accessDenied = <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("risks.notAuthorized")}</CardContent></Card>;
    return embedded ? accessDenied : <div className="space-y-6"><PageHeader title={t("risks.createTitle")} description={t("risks.createDescription")} />{accessDenied}</div>;
  }

  const formCard = (
      <Card className={`w-full shadow-none ${embedded ? "" : "mx-auto max-w-3xl"}`}>
        <CardHeader>
          <div className="flex items-center gap-3"><div className="rounded-lg bg-primary/10 p-2 text-primary"><AlertTriangle className="h-5 w-5" /></div><div><CardTitle>{t("risks.createTitle")}</CardTitle><CardDescription>{t("risks.createDescription")}</CardDescription></div></div>
        </CardHeader>
        <CardContent>
          <form className="space-y-5" onSubmit={(event) => void submit(event)}>
            <div className="space-y-2">
              <Label htmlFor="risk-project">{t("risks.project")}</Label>
              {initialProjectId ? (
                <div id="risk-project" className="flex min-h-10 items-center rounded-lg border bg-muted/30 px-3 text-sm">
                  {projectLoading ? t("risks.loading") : selectedProject ? <Link to={`/projects/${encodeURIComponent(selectedProject.publicId)}/risks-issues`} className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline"><FolderKanban className="h-3.5 w-3.5" />{selectedProject.name} ({selectedProject.code})</Link> : <span className="text-destructive">{t("risks.projectLoadError")}</span>}
                </div>
              ) : (
                <select id="risk-project" className="input-field" value={form.projectId} onChange={(event) => setForm((current) => ({ ...current, projectId: event.target.value }))} aria-invalid={submitted && !form.projectId}>
                  <option value="">{t("risks.selectProject")}</option>
                  {(projectsQuery.data?.items ?? []).map((project) => <option key={project.id} value={project.id}>{project.name} ({project.code})</option>)}
                </select>
              )}
              {required(form.projectId)}
              {projectLoadFailed ? <p role="alert" className="text-xs text-destructive">{initialProjectId ? t("risks.projectLoadError") : t("risks.projectsLoadError")}</p> : null}
            </div>

            <div className="grid gap-5 md:grid-cols-2">
              <div className="space-y-2"><Label htmlFor="risk-title">{t("risks.titleField")}</Label><Input id="risk-title" value={form.title} aria-invalid={submitted && !form.title.trim()} onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))} />{required(form.title)}</div>
              <div className="space-y-2"><Label htmlFor="risk-owner">{t("risks.systemUserOwner")}</Label><select id="risk-owner" className="input-field" value={form.ownerUserId} onChange={(event) => setForm((current) => ({ ...current, ownerUserId: event.target.value, ownerResourceId: "" }))}><option value="">{usersQuery.isLoading ? t("risks.loadingUsers") : t("risks.noOwner")}</option>{(usersQuery.data ?? []).map((owner) => <option key={owner.id} value={owner.id}>{owner.fullName}</option>)}</select>{usersQuery.isError ? <p role="alert" className="text-xs text-destructive">{t("risks.ownerLoadError")}</p> : null}</div>
            </div>

            <div className="space-y-2"><Label htmlFor="risk-description">{t("risks.descriptionField")}</Label><Textarea id="risk-description" value={form.description} aria-invalid={submitted && !form.description.trim()} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} />{required(form.description)}</div>

            <div className="space-y-2"><Label htmlFor="risk-level">{t("risks.riskLevel")}</Label><select id="risk-level" className="input-field" value={form.riskLevel} onChange={(event) => setForm((current) => ({ ...current, riskLevel: event.target.value as RiskLevel | "" }))} aria-invalid={submitted && !form.riskLevel}><option value="">{t("risks.selectRiskLevel")}</option>{RISK_LEVELS.map((level) => <option key={level} value={level}>{t(`enums.riskLevel.${level.charAt(0).toLowerCase()}${level.slice(1)}`)}</option>)}</select>{required(form.riskLevel)}</div>

            <div className="space-y-3">
              <Label>{t("risks.impacts")}</Label><p className="text-sm text-muted-foreground">{t("risks.selectImpactTypes")}</p>
              <div className="grid gap-2 sm:grid-cols-2">{RISK_IMPACT_TYPES.map((impactType) => <label key={impactType} className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 rounded border-input accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" checked={form.impactTypes.includes(impactType)} onChange={() => toggleImpact(impactType)} />{t(`enums.riskImpactType.${impactType.charAt(0).toLowerCase()}${impactType.slice(1)}`)}</label>)}</div>
              {form.impactTypes.length ? <div className="space-y-2"><Label htmlFor="risk-impact-description">{t("risks.impactDescription")}</Label><Textarea id="risk-impact-description" value={form.impactDescription} aria-invalid={submitted && !form.impactDescription.trim()} onChange={(event) => setForm((current) => ({ ...current, impactDescription: event.target.value }))} placeholder={t("risks.impactDescription")} />{required(form.impactDescription)}</div> : null}
            </div>

            {saveError ? <p role="alert" className="text-sm text-destructive">{t("risks.saveError")}</p> : null}
            <div className="flex flex-col-reverse gap-2 border-t pt-4 sm:flex-row sm:justify-end">
              <Button type="button" variant="outline" onClick={() => embedded ? onCancel?.() : navigate(returnTo)}>{t("risks.cancel")}</Button>
              <Button type="submit" name="intent" value="save-and-add-action" variant="outline" disabled={createRisk.isPending || projectLoading || projectUnavailable || !selectedProject}>{t("risks.saveAndAddAction")}</Button>
              <Button type="submit" name="intent" value="save" disabled={createRisk.isPending || projectLoading || projectUnavailable || !selectedProject}>{createRisk.isPending ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : null}{createRisk.isPending ? t("risks.saving") : t("risks.save")}</Button>
            </div>
          </form>
        </CardContent>
      </Card>
  );

  return embedded ? formCard : <div className="space-y-6"><PageHeader title={t("risks.createTitle")} description={t("risks.createDescription")} />{formCard}</div>;
}
