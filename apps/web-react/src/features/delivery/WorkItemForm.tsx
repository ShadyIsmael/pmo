import * as React from "react";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { Textarea } from "@components/ui/Textarea";
import { RichTextEditor } from "@components/ui/rich-text-editor";
import type { ProjectSummaryDto } from "@features/projects/api/projects";
import type { SaveWorkItemDto, WorkItemAssigneeDto, WorkItemPriority, WorkItemType } from "@api/workItemsFull";

const itemTypes: WorkItemType[] = ["Task", "Issue", "Subtask", "Bug", "Feature", "Epic", "Story", "Ticket", "TestCase"];
const priorities: WorkItemPriority[] = ["Critical", "High", "Medium", "Low", "Backlog"];

export function WorkItemForm({ draft, onChange, t, assignees = [], assigneesLoading = false, projects = [], allowTypeChange = true, projectLocked = false, extraContent, richTextDescription = false }: {
  draft: SaveWorkItemDto;
  onChange: (patch: Partial<SaveWorkItemDto>) => void;
  t: (key: string, params?: Record<string, string | number>) => string;
  assignees?: readonly WorkItemAssigneeDto[];
  assigneesLoading?: boolean;
  projects?: readonly ProjectSummaryDto[];
  allowTypeChange?: boolean;
  projectLocked?: boolean;
  extraContent?: React.ReactNode;
  richTextDescription?: boolean;
}) {
  const formId = React.useId();
  const target = draft.assignedResourceId ? `resource:${draft.assignedResourceId}` : draft.assignedTeamId ? `team:${draft.assignedTeamId}` : "";
  const isIssue = draft.type === "Issue";
  const assigneeOptions = assignees.map((candidate) => ({ value: `${candidate.targetType === "Resource" ? "resource" : "team"}:${candidate.targetId}`, label: `${candidate.name} · ${candidate.targetType === "Resource" ? t("workItems.resourceTarget") : t("workItems.teamTarget")}` }));
  const richTextLabels = {
    formatting: t("meetings.formatting"), bold: t("meetings.bold"), italic: t("meetings.italic"), underline: t("meetings.underline"),
    bulletedList: t("meetings.bulletedList"), numberedList: t("meetings.numberedList"), emoji: t("meetings.emoji"),
  };
  return <>
    <div className="space-y-1.5"><Label htmlFor={`${formId}-title`}>{t("common.title")} *</Label><Input id={`${formId}-title`} value={draft.title} onChange={(e) => onChange({ title: e.target.value })} required /></div>
    {allowTypeChange ? <div className="space-y-1.5"><Label htmlFor={`${formId}-type`}>{t("common.type")}</Label><select id={`${formId}-type`} className="input-field" value={draft.type} onChange={(e) => onChange({ type: e.target.value as WorkItemType })}>{itemTypes.map((type) => <option key={type} value={type}>{type === "Task" ? t("workItems.task") : type === "Issue" ? t("workItems.issue") : type}</option>)}</select></div> : null}
    <div className="space-y-1.5"><Label htmlFor={`${formId}-priority`}>{t("common.priority")}</Label><select id={`${formId}-priority`} className="input-field" value={draft.priority} onChange={(e) => onChange({ priority: e.target.value as WorkItemPriority })}>{priorities.map((priority) => <option key={priority} value={priority}>{t(`workItems.priority${priority}`)}</option>)}</select></div>
    <div className="space-y-1.5"><Label htmlFor={`${formId}-deadline`}>{t(isIssue ? "workItems.targetResolutionDate" : "workItems.deadline")}</Label><Input id={`${formId}-deadline`} type="date" value={(isIssue ? draft.targetResolutionDate : draft.dueDate)?.slice(0, 10) ?? ""} onChange={(e) => onChange(isIssue ? { targetResolutionDate: e.target.value || undefined } : { dueDate: e.target.value || undefined })} /></div>
    {isIssue ? <><div className="space-y-1.5"><Label htmlFor={`${formId}-severity`}>{t("workItems.severity")}</Label><Input id={`${formId}-severity`} value={draft.severity ?? ""} onChange={(e) => onChange({ severity: e.target.value || undefined })} /></div><div className="space-y-1.5"><Label htmlFor={`${formId}-resolution`}>{t("workItems.resolution")}</Label><Input id={`${formId}-resolution`} value={draft.resolution ?? ""} onChange={(e) => onChange({ resolution: e.target.value || undefined })} /></div></> : null}
    <div className="space-y-1.5"><Label htmlFor={`${formId}-project`}>{t("workItems.project")}</Label><select id={`${formId}-project`} className="input-field" value={draft.projectId ?? ""} disabled={projectLocked} onChange={(e) => onChange({ projectId: e.target.value || undefined, assignedResourceId: undefined, assignedTeamId: undefined })}>{projectLocked ? null : <option value="">{t("workItems.noProject")}</option>}{projects.map((project) => <option key={project.id} value={project.id}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}</select></div>
    <div className="space-y-1.5"><Label>{t("workItems.assignee")}</Label><SearchableSelect aria-label={t("workItems.assignee")} options={[{ value: "", label: t("workItems.unassigned") }, ...assigneeOptions]} value={target} onValueChange={(value) => { const [kind, id] = value.split(":"); onChange({ assignedResourceId: kind === "resource" ? id : undefined, assignedTeamId: kind === "team" ? id : undefined }); }} placeholder={draft.projectId ? t("workItems.unassigned") : t("workItems.selectProjectFirst")} searchPlaceholder={t("workItems.searchAssignees")} emptyText={t("workItems.noEligibleProjectPeople")} loadingText={t("common.loading")} loading={assigneesLoading} disabled={!draft.projectId} />{draft.projectId && !assigneesLoading && assignees.length === 0 ? <p className="text-xs text-muted-foreground">{t("workItems.noEligibleProjectPeople")}</p> : null}</div>
    {extraContent}
    <div className="space-y-1.5"><Label htmlFor={`${formId}-description`}>{t("workItems.description")}</Label>{richTextDescription ? <RichTextEditor id={`${formId}-description`} value={draft.description ?? ""} onChange={(value) => onChange({ description: value })} placeholder={t("workItems.description")} ariaLabel={t("workItems.description")} t={richTextLabels} /> : <Textarea id={`${formId}-description`} value={draft.description ?? ""} onChange={(e) => onChange({ description: e.target.value })} rows={5} />}</div>
  </>;
}
