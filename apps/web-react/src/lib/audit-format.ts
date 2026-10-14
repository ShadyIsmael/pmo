export type Translate = (key: string) => string;

const ACTION_KEYS: Record<string, string> = {
  Create: "operations.auditActions.create",
  Created: "operations.auditActions.created",
  Update: "operations.auditActions.update",
  Updated: "operations.auditActions.updated",
  Delete: "operations.auditActions.delete",
  Deleted: "operations.auditActions.deleted",
  Archive: "operations.auditActions.archive",
  Reactivate: "operations.auditActions.reactivate",
  Deactivate: "operations.auditActions.deactivate",
  ManagerHandover: "operations.auditActions.managerHandover",
  ManagerHandoverAccepted: "operations.auditActions.managerHandoverAccepted",
  Handover: "operations.auditActions.handover",
  Publish: "operations.auditActions.publish",
  Transition: "operations.auditActions.transition",
  Transitioned: "operations.auditActions.transitioned",
  Assigned: "operations.auditActions.assigned",
  Reassigned: "operations.auditActions.reassigned",
  Unassigned: "operations.auditActions.unassigned",
  Move: "operations.auditActions.move",
  RemoveFromProject: "operations.auditActions.removeFromProject",
  ReplacePredecessors: "operations.auditActions.replacePredecessors",
  ImportMPP: "operations.auditActions.importMpp",
  InitialBaselineCreated: "operations.auditActions.initialBaselineCreated",
  BaselineSnapshotCreated: "operations.auditActions.baselineSnapshotCreated",
  CreateProjectRole: "operations.auditActions.createProjectRole",
  UpdateProjectRole: "operations.auditActions.updateProjectRole",
  DeactivateProjectRole: "operations.auditActions.deactivateProjectRole",
  CreateVendorCategory: "operations.auditActions.createVendorCategory",
  UpdateVendorCategory: "operations.auditActions.updateVendorCategory",
  DeactivateVendorCategory: "operations.auditActions.deactivateVendorCategory",
};

const ENTITY_KEYS: Record<string, string> = {
  Project: "operations.auditEntities.project",
  ProjectMember: "operations.auditEntities.projectMember",
  ProjectStakeholder: "operations.auditEntities.projectStakeholder",
  ProjectStatusReport: "operations.auditEntities.projectStatusReport",
  ProjectChangeRequest: "operations.auditEntities.projectChangeRequest",
  ProjectBaselineVersion: "operations.auditEntities.projectBaselineVersion",
  ProjectSnapshot: "operations.auditEntities.projectSnapshot",
  ProjectHandoverRequest: "operations.auditEntities.projectHandoverRequest",
  ProjectWorkflowConfiguration: "operations.auditEntities.projectWorkflowConfiguration",
  WorkItem: "operations.auditEntities.workItem",
  Meeting: "operations.auditEntities.meeting",
  Risk: "operations.auditEntities.risk",
  Document: "operations.auditEntities.document",
  Resource: "operations.auditEntities.resource",
  ResourceAllocation: "operations.auditEntities.resourceAllocation",
  ResourceCostProfile: "operations.auditEntities.resourceCostProfile",
  Team: "operations.auditEntities.team",
  Vendor: "operations.auditEntities.vendor",
  VendorCategory: "operations.auditEntities.vendorCategory",
  Skill: "operations.auditEntities.skill",
  Lookup: "operations.auditEntities.lookup",
  Plan: "operations.auditEntities.plan",
  PlanVersion: "operations.auditEntities.planVersion",
  PlanActivity: "operations.auditEntities.planActivity",
  PlanAssignment: "operations.auditEntities.planAssignment",
  PlanDependency: "operations.auditEntities.planDependency",
  PlanWbsNode: "operations.auditEntities.planWbsNode",
  TimeEntry: "operations.auditEntities.timeEntry",
  TimerSession: "operations.auditEntities.timerSession",
  WorkflowManagement: "operations.auditEntities.workflowManagement",
  WorkflowTransition: "operations.auditEntities.workflowTransition",
  Budget: "operations.auditEntities.budget",
  Commitment: "operations.auditEntities.commitment",
  Cost: "operations.auditEntities.cost",
};

const FIELD_KEYS: Record<string, string> = {
  name: "operations.auditFields.name",
  code: "operations.auditFields.code",
  title: "common.title",
  description: "operations.auditFields.description",
  status: "common.status",
  priority: "common.priority",
  type: "common.type",
  category: "operations.auditFields.category",
  startDate: "operations.auditFields.startDate",
  endDate: "operations.auditFields.endDate",
  dueDate: "operations.auditFields.dueDate",
  currency: "operations.auditFields.currency",
  budget: "operations.auditFields.budget",
  actualCost: "operations.auditFields.actualCost",
  plannedValue: "operations.auditFields.plannedValue",
  earnedValue: "operations.auditFields.earnedValue",
  confidenceLevel: "operations.auditFields.confidenceLevel",
  businessCase: "operations.auditFields.businessCase",
  notes: "operations.auditFields.notes",
  workingCalendarId: "operations.auditFields.workingCalendar",
  projectManager: "operations.auditFields.projectManager",
  role: "operations.auditFields.role",
  allocationPercentage: "operations.auditFields.allocationPercentage",
  hoursPerWeek: "operations.auditFields.hoursPerWeek",
  isBillable: "operations.auditFields.isBillable",
  resource: "operations.auditFields.resource",
  assignment: "operations.auditFields.assignment",
  severity: "workItems.severity",
  resolution: "workItems.resolution",
  projectId: "workItems.project",
  transition: "operations.auditFields.transition",
};

function localize(key: string | undefined, fallback: string, t: Translate): string {
  if (!key) return fallback;
  const label = t(key);
  return label === key ? fallback : label;
}

export function auditActionLabel(action: string, t: Translate): string {
  return localize(ACTION_KEYS[action], action, t);
}

export function auditEntityLabel(entityType: string, t: Translate): string {
  return localize(ENTITY_KEYS[entityType], entityType, t);
}

export function auditFieldLabel(field: string, t: Translate): string {
  return localize(FIELD_KEYS[field], field, t);
}

function formatValue(value: unknown, t: Translate): string {
  if (value === null || value === undefined || value === "") return t("common.notAvailable");
  if (typeof value === "string") {
    return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(value) ? value.slice(0, 10) : value;
  }
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

export function formatAuditChanges(raw: string | null | undefined, t: Translate): string {
  if (!raw) return "";
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return raw;
  }
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return parsed === null ? "" : raw;
  const entries = Object.entries(parsed as Record<string, unknown>);
  if (!entries.length) return "";
  return entries
    .map(([field, value]) => {
      const label = auditFieldLabel(field, t);
      const isPair = Boolean(value) && typeof value === "object" && !Array.isArray(value) && "from" in (value as Record<string, unknown>) && "to" in (value as Record<string, unknown>);
      if (isPair) {
        const pair = value as { from?: unknown; to?: unknown };
        return `${label}: ${formatValue(pair.from, t)} → ${formatValue(pair.to, t)}`;
      }
      return `${label}: ${formatValue(value, t)}`;
    })
    .join(" · ");
}
