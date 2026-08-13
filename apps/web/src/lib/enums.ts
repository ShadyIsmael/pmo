/**
 * Centralized registry for all user-facing enums and statuses.
 * Provides stable values and translation keys for consistent UI display.
 */

// Base type for an enum option
interface EnumOption<T> {
  value: T;
  labelKey: string;
}

// --- PROJECT BASELINE VERSION STATUS ---
export enum ProjectBaselineStatus {
  Draft = "Draft",
  PendingApproval = "PendingApproval",
  Approved = "Approved",
  Rejected = "Rejected",
  Superseded = "Superseded",
}

const PROJECT_BASELINE_STATUS_OPTIONS: EnumOption<ProjectBaselineStatus>[] = [
  { value: ProjectBaselineStatus.Draft, labelKey: "enums.projectBaseline.status.draft" },
  { value: ProjectBaselineStatus.PendingApproval, labelKey: "enums.projectBaseline.status.pendingApproval" },
  { value: ProjectBaselineStatus.Approved, labelKey: "enums.projectBaseline.status.approved" },
  { value: ProjectBaselineStatus.Rejected, labelKey: "enums.projectBaseline.status.rejected" },
  { value: ProjectBaselineStatus.Superseded, labelKey: "enums.projectBaseline.status.superseded" },
];

export const ProjectBaselineStatusHelper = {
  /**
   * Gets all status options for use in dropdowns, filters, etc.
   */
  allOptions: (): EnumOption<ProjectBaselineStatus>[] => PROJECT_BASELINE_STATUS_OPTIONS,

  /**
   * Gets the translation key for a given status value.
   */
  getLabelKey: (status: ProjectBaselineStatus): string => {
    const option = PROJECT_BASELINE_STATUS_OPTIONS.find(opt => opt.value === status);
    return option ? option.labelKey : "enums.unknown"; // Fallback
  },

  /**
   * Finds an option by its value.
   */
  findByValue: (value: ProjectBaselineStatus): EnumOption<ProjectBaselineStatus> | undefined => {
    return PROJECT_BASELINE_STATUS_OPTIONS.find(opt => opt.value === value);
  },
};

// --- PROJECT CHANGE REQUEST STATUS ---
export enum ProjectChangeRequestStatus {
  Pending = "Pending",
  Approved = "Approved",
  Rejected = "Rejected",
  Cancelled = "Cancelled",
}

const PROJECT_CHANGE_REQUEST_STATUS_OPTIONS: EnumOption<ProjectChangeRequestStatus>[] = [
  { value: ProjectChangeRequestStatus.Pending, labelKey: "enums.projectChangeRequest.status.pending" },
  { value: ProjectChangeRequestStatus.Approved, labelKey: "enums.projectChangeRequest.status.approved" },
  { value: ProjectChangeRequestStatus.Rejected, labelKey: "enums.projectChangeRequest.status.rejected" },
  { value: ProjectChangeRequestStatus.Cancelled, labelKey: "enums.projectChangeRequest.status.cancelled" },
];

export const ProjectChangeRequestStatusHelper = {
  allOptions: (): EnumOption<ProjectChangeRequestStatus>[] => PROJECT_CHANGE_REQUEST_STATUS_OPTIONS,
  getLabelKey: (status: ProjectChangeRequestStatus): string => {
    const option = PROJECT_CHANGE_REQUEST_STATUS_OPTIONS.find(opt => opt.value === status);
    return option ? option.labelKey : "enums.unknown";
  },
  findByValue: (value: ProjectChangeRequestStatus): EnumOption<ProjectChangeRequestStatus> | undefined => {
    return PROJECT_CHANGE_REQUEST_STATUS_OPTIONS.find(opt => opt.value === value);
  },
};

// --- PROJECT CHANGE REQUEST TYPE ---
export enum ProjectChangeRequestType {
  Administrative = "Administrative",
  Baseline = "Baseline",
}

const PROJECT_CHANGE_REQUEST_TYPE_OPTIONS: EnumOption<ProjectChangeRequestType>[] = [
  { value: ProjectChangeRequestType.Administrative, labelKey: "enums.projectChangeRequest.type.administrative" },
  { value: ProjectChangeRequestType.Baseline, labelKey: "enums.projectChangeRequest.type.baseline" },
];

export const ProjectChangeRequestTypeHelper = {
  allOptions: (): EnumOption<ProjectChangeRequestType>[] => PROJECT_CHANGE_REQUEST_TYPE_OPTIONS,
  getLabelKey: (type: ProjectChangeRequestType): string => {
    const option = PROJECT_CHANGE_REQUEST_TYPE_OPTIONS.find(opt => opt.value === type);
    return option ? option.labelKey : "enums.unknown";
  },
  findByValue: (value: ProjectChangeRequestType): EnumOption<ProjectChangeRequestType> | undefined => {
    return PROJECT_CHANGE_REQUEST_TYPE_OPTIONS.find(opt => opt.value === value);
  },
};

// --- PROJECT STATUS ---
// Canonical values come from the backend (NEXUS.Modules.ProjectManagement).
// Label keys are resolved against the "projects" namespace in the message catalogs.
export const PROJECT_STATUSES = ["Initiation", "Planning", "InProgress", "OnHold", "Completed", "Cancelled", "Closed"] as const;
export type ProjectStatus = (typeof PROJECT_STATUSES)[number];

const PROJECT_STATUS_LABEL_KEYS: Record<ProjectStatus, string> = {
  Initiation: "initiation",
  Planning: "planning",
  InProgress: "inprogress",
  OnHold: "onhold",
  Completed: "completed",
  Cancelled: "cancelled",
  Closed: "closed",
};

export function isProjectStatus(value: unknown): value is ProjectStatus {
  return typeof value === "string" && (PROJECT_STATUSES as readonly string[]).includes(value);
}

export const ProjectStatusHelper = {
  allValues: (): string[] => [...PROJECT_STATUSES],
  getLabelKey: (status?: string | null): string | undefined =>
    status ? PROJECT_STATUS_LABEL_KEYS[status as ProjectStatus] : undefined,
};

// --- PROJECT PRIORITY ---
export const PROJECT_PRIORITIES = ["Low", "Medium", "High", "Critical"] as const;
export type ProjectPriority = (typeof PROJECT_PRIORITIES)[number];

const PROJECT_PRIORITY_LABEL_KEYS: Record<ProjectPriority, string> = {
  Low: "low",
  Medium: "medium",
  High: "high",
  Critical: "critical",
};

export function isProjectPriority(value: unknown): value is ProjectPriority {
  return typeof value === "string" && (PROJECT_PRIORITIES as readonly string[]).includes(value);
}

export const ProjectPriorityHelper = {
  allValues: (): string[] => [...PROJECT_PRIORITIES],
  getLabelKey: (priority?: string | null): string | undefined =>
    priority ? PROJECT_PRIORITY_LABEL_KEYS[priority as ProjectPriority] : undefined,
};

// --- PROJECT HEALTH / CONFIDENCE LEVEL ---
export const PROJECT_HEALTHS = ["Green", "Amber", "Red"] as const;
export type ProjectHealth = (typeof PROJECT_HEALTHS)[number];

const PROJECT_HEALTH_LABEL_KEYS: Record<ProjectHealth, string> = {
  Green: "green",
  Amber: "amber",
  Red: "red",
};

// Wording used for health badges (RAG semantics: on track / at risk / critical).
const PROJECT_HEALTH_STATUS_LABEL_KEYS: Record<ProjectHealth, string> = {
  Green: "onTrack",
  Amber: "atRisk",
  Red: "critical",
};

export function isProjectHealth(value: unknown): value is ProjectHealth {
  return typeof value === "string" && (PROJECT_HEALTHS as readonly string[]).includes(value);
}

export const ProjectHealthHelper = {
  allValues: (): string[] => [...PROJECT_HEALTHS],
  getLabelKey: (health?: string | null): string | undefined =>
    health ? PROJECT_HEALTH_LABEL_KEYS[health as ProjectHealth] : undefined,
  getStatusLabelKey: (health?: string | null): string | undefined =>
    health ? PROJECT_HEALTH_STATUS_LABEL_KEYS[health as ProjectHealth] : undefined,
};