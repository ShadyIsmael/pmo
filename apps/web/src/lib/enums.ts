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
// Label keys are resolved against the dedicated "enums" namespace in the message catalogs.
export const PROJECT_STATUSES = ["Initiation", "Planning", "InProgress", "OnHold", "Completed", "Cancelled", "Closed"] as const;
export type ProjectStatus = (typeof PROJECT_STATUSES)[number];

const PROJECT_STATUS_LABEL_KEYS: Record<ProjectStatus, string> = {
  Initiation: "projectStatus.initiation",
  Planning: "projectStatus.planning",
  InProgress: "projectStatus.inprogress",
  OnHold: "projectStatus.onhold",
  Completed: "projectStatus.completed",
  Cancelled: "projectStatus.cancelled",
  Closed: "projectStatus.closed",
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
  Low: "projectPriority.low",
  Medium: "projectPriority.medium",
  High: "projectPriority.high",
  Critical: "projectPriority.critical",
};

export function isProjectPriority(value: unknown): value is ProjectPriority {
  return typeof value === "string" && (PROJECT_PRIORITIES as readonly string[]).includes(value);
}

export const ProjectPriorityHelper = {
  allValues: (): string[] => [...PROJECT_PRIORITIES],
  getLabelKey: (priority?: string | null): string | undefined =>
    priority ? PROJECT_PRIORITY_LABEL_KEYS[priority as ProjectPriority] : undefined,
};

export const RISK_LEVELS = ["Low", "Medium", "High", "Critical"] as const;
export type RiskLevel = (typeof RISK_LEVELS)[number];
export const RISK_STATUSES = ["Identified", "ActionRequired", "FollowUp", "Mitigated", "Closed"] as const;
export type RiskStatus = (typeof RISK_STATUSES)[number];
export const RISK_ACTION_STATUSES = ["Planned", "InProgress", "Completed", "Cancelled"] as const;
export type RiskActionStatus = (typeof RISK_ACTION_STATUSES)[number];
export const RISK_IMPACT_TYPES = ["Schedule", "Budget", "Scope", "Quality", "Resources", "Procurement", "Compliance", "Reputation", "Operations", "Other"] as const;
export type RiskImpactType = (typeof RISK_IMPACT_TYPES)[number];

export const MEETING_STATUSES = ["Scheduled", "Completed", "Cancelled"] as const;
export type MeetingStatus = (typeof MEETING_STATUSES)[number];
const MEETING_STATUS_LABEL_KEYS: Record<MeetingStatus, string> = {
  Scheduled: "status.scheduled",
  Completed: "status.completed",
  Cancelled: "status.cancelled",
};
export const MeetingStatusHelper = {
  allValues: (): MeetingStatus[] => [...MEETING_STATUSES],
  getLabelKey: (status?: string | null): string => status && status in MEETING_STATUS_LABEL_KEYS ? MEETING_STATUS_LABEL_KEYS[status as MeetingStatus] : "status.unknown",
};

export const MEETING_REMINDER_OPTIONS = [
  { value: null, labelKey: "noReminder" },
  { value: 15, labelKey: "fifteenMinutesBefore" },
  { value: 30, labelKey: "thirtyMinutesBefore" },
  { value: 60, labelKey: "oneHourBefore" },
  { value: 1440, labelKey: "oneDayBefore" },
] as const;
export type MeetingReminderOffset = (typeof MEETING_REMINDER_OPTIONS)[number]["value"];
export const MeetingReminderHelper = {
  allOptions: () => [...MEETING_REMINDER_OPTIONS],
  getLabelKey: (value?: number | null): string => MEETING_REMINDER_OPTIONS.find(option => option.value === value)?.labelKey ?? "noReminder",
  toValue: (value: string): MeetingReminderOffset => {
    if (!value) return null;
    const numericValue = Number(value);
    return MEETING_REMINDER_OPTIONS.find(option => option.value === numericValue)?.value ?? null;
  },
};

const RISK_LEVEL_LABEL_KEYS: Record<RiskLevel, string> = {
  Low: "riskLevel.low",
  Medium: "riskLevel.medium",
  High: "riskLevel.high",
  Critical: "riskLevel.critical",
};
const RISK_STATUS_LABEL_KEYS: Record<string, string> = {
  Identified: "riskStatus.identified",
  ActionRequired: "riskStatus.actionRequired",
  FollowUp: "riskStatus.followUp",
  Mitigated: "riskStatus.mitigated",
  Closed: "riskStatus.closed",
  // Values emitted by pre-workflow snapshot records.
  Expected: "riskStatus.expected",
  Active: "riskStatus.active",
};
const RISK_ACTION_STATUS_LABEL_KEYS: Record<RiskActionStatus, string> = {
  Planned: "riskActionStatus.planned",
  InProgress: "riskActionStatus.inProgress",
  Completed: "riskActionStatus.completed",
  Cancelled: "riskActionStatus.cancelled",
};
const RISK_IMPACT_TYPE_LABEL_KEYS: Record<RiskImpactType, string> = {
  Schedule: "riskImpactType.schedule", Budget: "riskImpactType.budget", Scope: "riskImpactType.scope", Quality: "riskImpactType.quality", Resources: "riskImpactType.resources", Procurement: "riskImpactType.procurement", Compliance: "riskImpactType.compliance", Reputation: "riskImpactType.reputation", Operations: "riskImpactType.operations", Other: "riskImpactType.other",
};

// Text-only presentation classes for risk values. Keep these semantic mappings
// next to the enum localization metadata so every risk surface uses the same
// light/dark-theme-aware colors without introducing filled badges or pills.
const RISK_LEVEL_TEXT_CLASSES: Record<RiskLevel, string> = {
  Low: "text-emerald-700 dark:text-emerald-400",
  Medium: "text-amber-700 dark:text-amber-300",
  High: "text-red-700 dark:text-red-400",
  Critical: "text-red-800 dark:text-red-300",
};
const RISK_STATUS_TEXT_CLASSES: Record<string, string> = {
  Identified: "text-blue-700 dark:text-blue-300",
  ActionRequired: "text-amber-700 dark:text-amber-300",
  FollowUp: "text-blue-700 dark:text-blue-300",
  Mitigated: "text-emerald-700 dark:text-emerald-400",
  Closed: "text-emerald-700 dark:text-emerald-400",
  Expected: "text-blue-700 dark:text-blue-300",
  Active: "text-amber-700 dark:text-amber-300",
};

export function getRiskLevelTextClass(value?: string | null): string {
  return value ? RISK_LEVEL_TEXT_CLASSES[value as RiskLevel] ?? "text-foreground" : "text-foreground";
}

export function getRiskStatusTextClass(value?: string | null): string {
  return value ? RISK_STATUS_TEXT_CLASSES[value as RiskStatus] ?? "text-foreground" : "text-foreground";
}

export const RiskLevelHelper = {
  allValues: (): RiskLevel[] => [...RISK_LEVELS],
  getLabelKey: (value?: string | null): string | undefined => value ? RISK_LEVEL_LABEL_KEYS[value as RiskLevel] : undefined,
  getTextClass: getRiskLevelTextClass,
};
export const RiskStatusHelper = {
  allValues: (): RiskStatus[] => [...RISK_STATUSES],
  getLabelKey: (value?: string | null): string | undefined => value ? RISK_STATUS_LABEL_KEYS[value as RiskStatus] : undefined,
  getTextClass: getRiskStatusTextClass,
};
export const RiskActionStatusHelper = {
  allValues: (): RiskActionStatus[] => [...RISK_ACTION_STATUSES],
  getLabelKey: (value?: string | null): string | undefined => value ? RISK_ACTION_STATUS_LABEL_KEYS[value as RiskActionStatus] : undefined,
};
export const RiskImpactTypeHelper = {
  allValues: (): RiskImpactType[] => [...RISK_IMPACT_TYPES],
  getLabelKey: (value: RiskImpactType): string => RISK_IMPACT_TYPE_LABEL_KEYS[value] ?? "riskImpactType.other",
};

// --- PROJECT HEALTH / CONFIDENCE LEVEL ---
export const PROJECT_HEALTHS = ["Green", "Amber", "Red"] as const;
export type ProjectHealth = (typeof PROJECT_HEALTHS)[number] | "Unknown";

const PROJECT_HEALTH_LABEL_KEYS: Record<ProjectHealth, string> = {
  Green: "projectHealth.green",
  Amber: "projectHealth.amber",
  Red: "projectHealth.red",
  Unknown: "projectHealth.unknown",
};

// Wording used for health badges (RAG semantics: on track / at risk / critical).
const PROJECT_HEALTH_STATUS_LABEL_KEYS: Record<ProjectHealth, string> = {
  Green: "projectHealth.onTrack",
  Amber: "projectHealth.atRisk",
  Red: "projectHealth.critical",
  Unknown: "projectHealth.notAssessed",
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

export const PLANNING_PLAN_TYPES = ["Execution", "Recovery", "Tender", "Construction", "Shutdown", "Scenario", "Custom"] as const;
export type PlanningPlanType = (typeof PLANNING_PLAN_TYPES)[number];
export const PLANNING_VERSION_TYPES = ["Working", "Baseline"] as const;
export type PlanningVersionType = (typeof PLANNING_VERSION_TYPES)[number];
export const PLANNING_VERSION_TYPE = { working: PLANNING_VERSION_TYPES[0], baseline: PLANNING_VERSION_TYPES[1] } as const;
export const PLANNING_VERSION_STATUSES = ["Active", "PendingApproval", "Rejected", "Approved", "Superseded"] as const;
export type PlanningVersionStatus = (typeof PLANNING_VERSION_STATUSES)[number];
export const PLANNING_VERSION_STATUS = { active: PLANNING_VERSION_STATUSES[0], approved: PLANNING_VERSION_STATUSES[3] } as const;
export const PLANNING_ACTIVITY_TYPES = ["Task", "Milestone"] as const;
export type PlanningActivityType = (typeof PLANNING_ACTIVITY_TYPES)[number];
export const PLANNING_ACTIVITY_TYPE = { task: PLANNING_ACTIVITY_TYPES[0], milestone: PLANNING_ACTIVITY_TYPES[1] } as const;
export const PLANNING_DEPENDENCY_TYPES = ["FinishToStart", "StartToStart", "FinishToFinish", "StartToFinish"] as const;
export type PlanningDependencyType = (typeof PLANNING_DEPENDENCY_TYPES)[number];
export const PLANNING_DEPENDENCY_TYPE = {
  finishToStart: PLANNING_DEPENDENCY_TYPES[0],
  startToStart: PLANNING_DEPENDENCY_TYPES[1],
  finishToFinish: PLANNING_DEPENDENCY_TYPES[2],
  startToFinish: PLANNING_DEPENDENCY_TYPES[3],
} as const;

export const planningEnumLabelKey = (group: "planType" | "versionType" | "versionStatus" | "activityType" | "dependencyType", value: string) =>
  `planning.${group}.${value}`;
