"use client";

import * as React from "react";
import { AlertTriangle, CheckCircle2, Settings2 } from "lucide-react";
import { useLocale } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/PageHeader";
import { useManagementPerspective } from "@/lib/perspective-context";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";
import { useProjectWorkflowSettings, useWorkflowDefinitions, useWorkflowManagementMutations, type WorkflowDomain } from "@/lib/api/workflows";

const domains: WorkflowDomain[] = ["Task", "Risk", "Finance"];

export function ProjectWorkflowSettings({ projectId }: { projectId: string }) {
  const locale = useLocale();
  const { user } = useAuth();
  const { effectiveReadOnly } = useManagementPerspective();
  const settings = useProjectWorkflowSettings(projectId);
  const mutations = useWorkflowManagementMutations();
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit) && !effectiveReadOnly);
  const [error, setError] = React.useState<string | null>(null);
  const copy = locale === "ar" ? {
    title: "مسارات عمل المشروع", description: "حدد سلوك دورة الحياة لكل مجال بشكل مستقل.", standard: "السلوك القياسي", custom: "مسار عمل مخصص", task: "المهام", risk: "المخاطر", finance: "المالية", select: "اختر إصدارًا منشورًا", save: "حفظ", readOnly: "عرض للقراءة فقط", existing: "السجلات الحالية لا تتغير.", newRecords: "السجلات الجديدة تستخدم الإعداد الجديد.", confirm: "سيظل كل WorkflowInstance حاليًا مرتبطًا بإصداره. السجلات الجديدة فقط ستستخدم الإعداد الجديد. هل تريد المتابعة؟", noPublished: "لا توجد مسارات منشورة نشطة لهذا المجال.", saved: "تم الحفظ", error: "تعذر حفظ إعداد مسار العمل." 
  } : {
    title: "Project Workflows", description: "Choose lifecycle behavior independently for each domain.", standard: "Standard behavior", custom: "Custom workflow", task: "Tasks", risk: "Risks", finance: "Finance", select: "Select a Published version", save: "Save", readOnly: "Read-only view", existing: "Existing records remain unchanged.", newRecords: "New records use the new configuration.", confirm: "Existing WorkflowInstances remain bound to their current version. Only new records use the new configuration. Continue?", noPublished: "No active Published workflows are available for this domain.", saved: "Saved", error: "Unable to save workflow configuration."
  };

  const taskDefinitions = useWorkflowDefinitions({ domain: "Task", isActive: true });
  const riskDefinitions = useWorkflowDefinitions({ domain: "Risk", isActive: true });
  const financeDefinitions = useWorkflowDefinitions({ domain: "Finance", isActive: true });
  const definitions = {
    Task: taskDefinitions.data?.filter(item => Boolean(item.activePublishedVersionId)) ?? [],
    Risk: riskDefinitions.data?.filter(item => Boolean(item.activePublishedVersionId)) ?? [],
    Finance: financeDefinitions.data?.filter(item => Boolean(item.activePublishedVersionId)) ?? [],
  };

  const save = async (domain: WorkflowDomain, mode: "Standard" | "Custom", workflowDefinitionId?: string, rowVersion?: string | number[]) => {
    setError(null);
    const current = settings.data?.domains.find(item => item.domain === domain);
    const changedWorkflow = current?.mode === "Custom" && (mode === "Standard" || current.workflowDefinitionId !== workflowDefinitionId);
    if (changedWorkflow && !window.confirm(copy.confirm)) return;
    try {
      await mutations.saveProjectConfiguration.mutateAsync({ projectId, domain, body: { mode, workflowDefinitionId: mode === "Custom" ? workflowDefinitionId : undefined, rowVersion } });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : copy.error);
    }
  };

  const labels: Record<WorkflowDomain, string> = { Task: copy.task, Risk: copy.risk, Finance: copy.finance };
  return <div className="space-y-6"><PageHeader title={copy.title} description={copy.description} actions={effectiveReadOnly ? <Badge status="ReadOnly">{copy.readOnly}</Badge> : null} />{error ? <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div> : null}<div className="grid gap-4 lg:grid-cols-3">{domains.map(domain => { const current = settings.data?.domains.find(item => item.domain === domain); const options = definitions[domain]; const mode = current?.mode ?? "Standard"; return <Card key={domain} className="flex flex-col"><CardHeader><div className="flex items-start justify-between gap-3"><div><CardTitle className="text-base">{labels[domain]}</CardTitle><CardDescription className="mt-1">{mode === "Custom" ? copy.custom : copy.standard}</CardDescription></div><Settings2 className="h-4 w-4 text-muted-foreground" /></div></CardHeader><CardContent className="flex flex-1 flex-col gap-4"><select className="h-10 rounded-md border border-input bg-background px-3 text-sm" value={mode === "Custom" ? current?.workflowDefinitionId ?? "" : "Standard"} disabled={!canManage || settings.isLoading} onChange={event => { const value = event.target.value; if (value === "Standard") void save(domain, "Standard", undefined, current?.rowVersion); else void save(domain, "Custom", value, current?.rowVersion); }}><option value="Standard">{copy.standard}</option>{options.map(item => <option key={item.id} value={item.id}>{item.name} · v{item.activePublishedVersionNumber}</option>)}</select>{mode === "Custom" && !current?.workflowDefinitionId ? <p className="text-xs text-destructive">{copy.select}</p> : mode === "Custom" ? <p className="text-xs text-muted-foreground">{current?.workflowDefinitionName} · v{current?.effectiveWorkflowVersionNumber}</p> : <p className="text-xs text-muted-foreground">{copy.noPublished}</p>}<div className="mt-auto space-y-1 border-t pt-3 text-xs text-muted-foreground"><p className="flex items-center gap-1.5"><CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />{copy.existing}</p><p>{copy.newRecords}</p></div></CardContent></Card>; })}</div></div>;
}
