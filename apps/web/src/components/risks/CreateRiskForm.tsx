"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { AlertTriangle, FolderKanban, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { useProject, useProjects } from "@/hooks/projects/useProjects";
import { useResources } from "@/hooks/resources/useResources";
import { useCreateRisk } from "@/hooks/risks/useRisks";
import { RISK_IMPACT_TYPES, RiskImpactTypeHelper, RISK_LEVELS, RiskLevelHelper } from "@/lib/enums";
import { getRiskImpactTypeAccent } from "@/lib/risk-presentation";
import type { RiskDto, RiskImpactType, RiskLevel } from "@/lib/api/risks";

interface CreateRiskFormProps {
  initialProjectId?: string;
  returnTo: string;
  onCreated?: (_risk: RiskDto) => void;
  onCancel?: () => void;
}

interface RiskFormValues {
  projectId: string;
  title: string;
  description: string;
  riskLevel: RiskLevel | "";
  ownerResourceId: string;
  impactTypes: RiskImpactType[];
  impactDescription: string;
}

export function CreateRiskForm({ initialProjectId, returnTo, onCreated, onCancel }: CreateRiskFormProps) {
  const t = useTranslations("risks");
  const enums = useTranslations("enums");
  const locale = useLocale();
  const router = useRouter();
  const createRisk = useCreateRisk();
  const [values, setValues] = useState<RiskFormValues>({
    projectId: initialProjectId ?? "",
    title: "",
    description: "",
    riskLevel: "",
    ownerResourceId: "",
    impactTypes: [],
    impactDescription: "",
  });
  const [submitted, setSubmitted] = useState(false);
  const projectQuery = useProject(initialProjectId ?? "");
  const projectsQuery = useProjects({ page: 1, pageSize: 100 }, !initialProjectId);
  const resourcesQuery = useResources({ page: 1, pageSize: 100, isActive: true }, true);
  const selectedProject = initialProjectId ? projectQuery.data : projectsQuery.data?.items.find((project) => project.id === values.projectId);
  const projectUnavailable = initialProjectId ? projectQuery.isError || (!projectQuery.isLoading && !selectedProject) : projectsQuery.isError;
  const projectLoading = initialProjectId ? projectQuery.isLoading : projectsQuery.isLoading;

  const setValue = <K extends keyof RiskFormValues>(key: K, value: RiskFormValues[K]) => {
    setValues((current) => ({ ...current, [key]: value }));
  };

  const required = (value: string | undefined) => submitted && !value?.trim() ? <p role="alert" className="text-xs text-destructive">{t("required")}</p> : null;
  const selectedImpactTypes = values.impactTypes;
  const toggleImpact = (impactType: RiskImpactType) => setValues((current) => {
    const impactTypes = current.impactTypes.includes(impactType) ? current.impactTypes.filter((type) => type !== impactType) : [...current.impactTypes, impactType];
    return { ...current, impactTypes };
  });

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitted(true);
    if (!values.projectId || !selectedProject || !values.title.trim() || !values.description.trim() || !values.riskLevel) return;
    const intent = new FormData(event.currentTarget).get("intent");
    if (selectedImpactTypes.length > 0 && !values.impactDescription.trim()) return;
    const risk = await createRisk.mutateAsync({
      projectId: values.projectId,
      title: values.title.trim(),
      description: values.description.trim(),
      riskLevel: values.riskLevel,
      ownerResourceId: values.ownerResourceId || undefined,
      impactAssessment: selectedImpactTypes.length > 0 ? { impactTypes: selectedImpactTypes, description: values.impactDescription.trim() } : undefined,
    });
    if (intent === "save" && onCreated) {
      onCreated(risk);
      return;
    }
    router.replace(intent === "save-and-add-action" ? `/${locale}/risks-issues/risks/${risk.id}?tab=actions&addAction=1&from=${initialProjectId ? "project" : "risks"}${initialProjectId ? `&projectId=${encodeURIComponent(initialProjectId)}` : ""}` : returnTo);
  };

  return (
    <Card className="mx-auto w-full max-w-3xl shadow-none">
      <CardHeader>
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-primary/10 p-2 text-primary"><AlertTriangle className="h-5 w-5" /></div>
          <div><CardTitle>{t("createTitle")}</CardTitle><CardDescription>{t("createDescription")}</CardDescription></div>
        </div>
      </CardHeader>
      <CardContent>
        <form className="space-y-5" onSubmit={handleSubmit}>
          <div className="space-y-2">
            <Label htmlFor="risk-project">{t("project")}</Label>
            {initialProjectId ? (
              <div id="risk-project" className="flex h-8 items-center rounded-lg border bg-muted/30 px-3 text-xs">{projectQuery.isLoading ? t("loading") : selectedProject ? <Link href={`/${locale}/projects/${encodeURIComponent(selectedProject.id)}?tab=risks-issues`} className="inline-flex items-center gap-1.5 font-medium text-blue-700 hover:text-blue-800 hover:underline dark:text-blue-300 dark:hover:text-blue-200"><FolderKanban className="h-3.5 w-3.5" />{selectedProject.name} ({selectedProject.code})</Link> : <span className="text-destructive">{t("projectLoadError")}</span>}</div>
            ) : (
              <Select value={values.projectId} onValueChange={(value) => setValue("projectId", value)}>
                <SelectTrigger id="risk-project"><SelectValue placeholder={t("selectProject")} /></SelectTrigger>
                <SelectContent>{projectsQuery.data?.items.map((project) => <SelectItem key={project.id} value={project.id}>{project.name} ({project.code})</SelectItem>)}</SelectContent>
              </Select>
            )}
            {required(values.projectId)}
          </div>

          <div className="grid gap-5 md:grid-cols-2">
            <div className="space-y-2"><Label htmlFor="risk-title">{t("titleField")}</Label><Input id="risk-title" value={values.title} aria-invalid={submitted && !values.title.trim()} onChange={(event) => setValue("title", event.target.value)} />{required(values.title)}</div>
            <div className="space-y-2"><Label htmlFor="risk-owner">{t("owner")}</Label><Select value={values.ownerResourceId || "none"} onValueChange={(value) => setValue("ownerResourceId", value === "none" ? "" : value)}><SelectTrigger id="risk-owner"><SelectValue placeholder={resourcesQuery.isLoading ? t("loadingOwners") : t("selectOwner")} /></SelectTrigger><SelectContent><SelectItem value="none">{t("noOwner")}</SelectItem>{resourcesQuery.data?.items.map((resource) => <SelectItem key={resource.id} value={resource.id}>{resource.name}</SelectItem>)}</SelectContent></Select></div>
          </div>

          <div className="space-y-2"><Label htmlFor="risk-description">{t("descriptionField")}</Label><Textarea id="risk-description" value={values.description} aria-invalid={submitted && !values.description.trim()} onChange={(event) => setValue("description", event.target.value)} />{required(values.description)}</div>

          <div className="space-y-2"><Label htmlFor="risk-level">{t("riskLevel")}</Label><Select value={values.riskLevel} onValueChange={(value) => setValue("riskLevel", value as RiskLevel)}><SelectTrigger id="risk-level" aria-invalid={submitted && !values.riskLevel}><SelectValue placeholder={t("selectRiskLevel")} /></SelectTrigger><SelectContent>{RISK_LEVELS.map((level) => <SelectItem key={level} value={level}>{enums(RiskLevelHelper.getLabelKey(level)!)}</SelectItem>)}</SelectContent></Select>{required(values.riskLevel)}</div>

          <div className="space-y-3"><Label>{t("impacts")}</Label><p className="text-sm text-muted-foreground">{t("selectImpactTypes")}</p><div className="grid gap-2 sm:grid-cols-2">{RISK_IMPACT_TYPES.map((impactType) => <label key={impactType} className={`flex items-center gap-2 text-sm ${getRiskImpactTypeAccent(impactType).chip.split(" ").filter((className) => className.includes("text-")).join(" ")}`}><input type="checkbox" className="h-4 w-4 rounded border-input accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" checked={selectedImpactTypes.includes(impactType)} onChange={() => toggleImpact(impactType)} />{enums(RiskImpactTypeHelper.getLabelKey(impactType))}</label>)}</div>{selectedImpactTypes.length > 0 ? <div className="space-y-1"><Label htmlFor="risk-impact-description">{t("impactDescription")}</Label><Textarea id="risk-impact-description" value={values.impactDescription} aria-invalid={submitted && !values.impactDescription.trim()} onChange={(event) => setValue("impactDescription", event.target.value)} placeholder={t("impactDescription")} />{required(values.impactDescription)}</div> : null}</div>

          {projectsQuery.isError ? <p role="alert" className="text-sm text-destructive">{t("projectsLoadError")}</p> : null}
          {createRisk.error ? <p role="alert" className="text-sm text-destructive">{t("saveError")}</p> : null}
          <div className="flex flex-col-reverse gap-2 border-t pt-4 sm:flex-row sm:justify-end"><Button type="button" variant="outline" onClick={() => onCancel ? onCancel() : router.replace(returnTo)}>{t("cancel")}</Button><Button type="submit" name="intent" value="save-and-add-action" variant="outline" disabled={createRisk.isPending || projectLoading || projectUnavailable}>{t("saveAndAddAction")}</Button><Button type="submit" name="intent" value="save" disabled={createRisk.isPending || projectLoading || projectUnavailable}>{createRisk.isPending ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : null}{createRisk.isPending ? t("saving") : t("save")}</Button></div>
        </form>
      </CardContent>
    </Card>
  );
}
