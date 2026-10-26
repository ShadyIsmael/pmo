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
const issueSeverities = [
  { value: "Critical", labelKey: "workItems.severityCritical" },
  { value: "High", labelKey: "workItems.severityHigh" },
  { value: "Major", labelKey: "workItems.severityMajor" },
  { value: "Medium", labelKey: "workItems.severityMedium" },
  { value: "Minor", labelKey: "workItems.severityMinor" },
] as const;

export function WorkItemForm({ draft, onChange, t, assignees = [], assigneesLoading = false, projects = [], allowTypeChange = true, projectLocked = false, extraContent, richTextDescription = false, layout = "default" }: {
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
  layout?: "default" | "structured";
}) {
  const formId = React.useId();
  const target = draft.assignedResourceId ? `resource:${draft.assignedResourceId}` : draft.assignedTeamId ? `team:${draft.assignedTeamId}` : "";
  const isIssue = draft.type === "Issue";
  const assigneeOptions = assignees.map((candidate) => ({ value: `${candidate.targetType === "Resource" ? "resource" : "team"}:${candidate.targetId}`, label: `${candidate.name} · ${candidate.targetType === "Resource" ? t("workItems.resourceTarget") : t("workItems.teamTarget")}` }));
  const richTextLabels = {
    formatting: t("meetings.formatting"), bold: t("meetings.bold"), italic: t("meetings.italic"), underline: t("meetings.underline"),
    bulletedList: t("meetings.bulletedList"), numberedList: t("meetings.numberedList"), emoji: t("meetings.emoji"),
  };

  const titleField = <div className="space-y-1.5">
    <Label htmlFor={`${formId}-title`}>{t("common.title")} *</Label>
    <Input id={`${formId}-title`} value={draft.title} onChange={(event) => onChange({ title: event.target.value })} required />
  </div>;
  const typeField = allowTypeChange ? <div className="space-y-1.5">
    <Label htmlFor={`${formId}-type`}>{t("common.type")}</Label>
    <select id={`${formId}-type`} className="input-field w-full" value={draft.type} onChange={(event) => {
      const type = event.target.value as WorkItemType;
      if (layout !== "structured") { onChange({ type }); return; }
      onChange(type === "Issue" ? { type, priority: "Medium", severity: draft.severity ?? "Medium" } : { type, severity: undefined });
    }}>
      {itemTypes.map((type) => <option key={type} value={type}>{type === "Task" ? t("workItems.task") : type === "Issue" ? t("workItems.issue") : type}</option>)}
    </select>
  </div> : null;
  const issueSeveritySelect = layout === "structured" && isIssue;
  const classificationFieldId = `${formId}-${issueSeveritySelect ? "severity" : "priority"}`;
  const priorityField = <div className="space-y-1.5">
    <Label htmlFor={classificationFieldId}>{t(issueSeveritySelect ? "workItems.severity" : "common.priority")}</Label>
    <select
      id={classificationFieldId}
      className="input-field w-full"
      value={issueSeveritySelect ? draft.severity ?? "Medium" : draft.priority}
      onChange={(event) => onChange(issueSeveritySelect ? { severity: event.target.value } : { priority: event.target.value as WorkItemPriority })}
    >
      {issueSeveritySelect
        ? issueSeverities.map((severity) => <option key={severity.value} value={severity.value}>{t(severity.labelKey)}</option>)
        : priorities.map((priority) => <option key={priority} value={priority}>{t(`workItems.priority${priority}`)}</option>)}
    </select>
  </div>;
  const deadlineField = <div className="space-y-1.5">
    <Label htmlFor={`${formId}-deadline`}>{t(isIssue ? "workItems.targetResolutionDate" : "workItems.deadline")}</Label>
    <Input id={`${formId}-deadline`} type="date" value={(isIssue ? draft.targetResolutionDate : draft.dueDate)?.slice(0, 10) ?? ""} onChange={(event) => onChange(isIssue ? { targetResolutionDate: event.target.value || undefined } : { dueDate: event.target.value || undefined })} />
  </div>;
  const issueFields = isIssue ? <>
    <div className="space-y-1.5">
      <Label htmlFor={`${formId}-severity`}>{t("workItems.severity")}</Label>
      <Input id={`${formId}-severity`} value={draft.severity ?? ""} onChange={(event) => onChange({ severity: event.target.value || undefined })} />
    </div>
    <div className="space-y-1.5">
      <Label htmlFor={`${formId}-resolution`}>{t("workItems.resolution")}</Label>
      <Input id={`${formId}-resolution`} value={draft.resolution ?? ""} onChange={(event) => onChange({ resolution: event.target.value || undefined })} />
    </div>
  </> : null;
  const projectField = <div className="space-y-1.5 sm:col-span-2">
    <Label htmlFor={`${formId}-project`}>{t("workItems.project")}</Label>
    <select id={`${formId}-project`} className="input-field w-full" value={draft.projectId ?? ""} disabled={projectLocked} onChange={(event) => onChange({ projectId: event.target.value || undefined, assignedResourceId: undefined, assignedTeamId: undefined })}>
      {projectLocked ? null : <option value="">{t("workItems.noProject")}</option>}
      {projects.map((project) => <option key={project.id} value={project.id}>{project.code ? `${project.code} · ${project.name}` : project.name}</option>)}
    </select>
  </div>;
  const assigneeField = <div className="space-y-1.5">
    <Label>{t("workItems.assignee")}</Label>
    <SearchableSelect
      aria-label={t("workItems.assignee")}
      options={[{ value: "", label: t("workItems.unassigned") }, ...assigneeOptions]}
      value={target}
      onValueChange={(value) => {
        const [kind, id] = value.split(":");
        onChange({ assignedResourceId: kind === "resource" ? id : undefined, assignedTeamId: kind === "team" ? id : undefined });
      }}
      placeholder={draft.projectId ? t("workItems.unassigned") : t("workItems.selectProjectFirst")}
      searchPlaceholder={t("workItems.searchAssignees")}
      emptyText={t("workItems.noEligibleProjectPeople")}
      loadingText={t("common.loading")}
      loading={assigneesLoading}
      disabled={!draft.projectId}
    />
    {draft.projectId && !assigneesLoading && assignees.length === 0 ? <p className="text-xs text-muted-foreground">{t("workItems.noEligibleProjectPeople")}</p> : null}
  </div>;
  const descriptionField = richTextDescription
    ? <RichTextEditor id={`${formId}-description`} value={draft.description ?? ""} onChange={(value) => onChange({ description: value })} placeholder={t("workItems.description")} ariaLabel={t("workItems.description")} t={richTextLabels} fillAvailable={layout === "structured"} />
    : <Textarea id={`${formId}-description`} value={draft.description ?? ""} onChange={(event) => onChange({ description: event.target.value })} rows={5} />;

  if (layout === "structured") {
    return <div className="flex min-h-0 flex-1 flex-col gap-4">
      <div className="grid shrink-0 gap-4 md:grid-cols-2">
        <section aria-labelledby={`${formId}-details-heading`} className="space-y-4 rounded-lg border bg-muted/20 p-4 sm:p-5">
          <h3 id={`${formId}-details-heading`} className="text-sm font-semibold">{t("workItems.details")}</h3>
          <div className="space-y-4">
            {titleField}
            {typeField ? <div className="grid gap-4 sm:grid-cols-2">{typeField}{priorityField}</div> : priorityField}
          </div>
        </section>

        <section aria-labelledby={`${formId}-planning-heading`} className="space-y-4 rounded-lg border bg-muted/20 p-4 sm:p-5">
          <h3 id={`${formId}-planning-heading`} className="text-sm font-semibold">{t("workItems.formPlanning")}</h3>
          <div className="grid gap-4 sm:grid-cols-2">
            {projectField}
            {assigneeField}
            {deadlineField}
          </div>
        </section>

      {isIssue && layout !== "structured" ? <section aria-labelledby={`${formId}-issue-heading`} className="space-y-4 rounded-lg border bg-muted/20 p-4 md:col-span-2 sm:p-5">
          <h3 id={`${formId}-issue-heading`} className="text-sm font-semibold">{t("workItems.issueDetails")}</h3>
          <div className="grid gap-4 sm:grid-cols-2">{issueFields}</div>
        </section> : null}

        {extraContent ? <div className="md:col-span-2">{extraContent}</div> : null}

      </div>

      <section aria-labelledby={`${formId}-description-heading`} className="flex min-h-0 flex-1 flex-col gap-3 rounded-lg border bg-muted/20 p-4 sm:p-5">
        <h3 id={`${formId}-description-heading`} className="shrink-0 text-sm font-semibold">{t("workItems.description")}</h3>
        {descriptionField}
      </section>
    </div>;
  }

  return <div className="space-y-4">
    {titleField}
    {typeField}
    {priorityField}
    {deadlineField}
    {issueFields}
    {projectField}
    {assigneeField}
    {extraContent}
    <div className="space-y-1.5"><Label htmlFor={`${formId}-description`}>{t("workItems.description")}</Label>{descriptionField}</div>
  </div>;
}
