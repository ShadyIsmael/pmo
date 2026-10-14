export const PROJECT_STATUSES = ["Initiation", "Planning", "InProgress", "OnHold", "Completed", "Cancelled", "Closed"] as const;export type ProjectStatus = (typeof PROJECT_STATUSES)[number];

const PROJECT_STATUS_LABEL_KEYS: Record<ProjectStatus, string> = {
  Initiation: "projectStatus.initiation",
  Planning: "projectStatus.planning",
  InProgress: "projectStatus.inprogress",
  OnHold: "projectStatus.onhold",
  Completed: "projectStatus.completed",
  Cancelled: "projectStatus.cancelled",
  Closed: "projectStatus.closed",
};

export const ProjectStatusHelper = {
  allValues: (): string[] => [...PROJECT_STATUSES],
  getLabelKey: (status?: string | null): string | undefined =>
    status ? PROJECT_STATUS_LABEL_KEYS[status as ProjectStatus] : undefined,
};

export const PROJECT_PRIORITIES = ["Low", "Medium", "High", "Critical"] as const;
export type ProjectPriority = (typeof PROJECT_PRIORITIES)[number];

const PROJECT_PRIORITY_LABEL_KEYS: Record<ProjectPriority, string> = {
  Low: "projectPriority.low",
  Medium: "projectPriority.medium",
  High: "projectPriority.high",
  Critical: "projectPriority.critical",
};

export const ProjectPriorityHelper = {
  allValues: (): string[] => [...PROJECT_PRIORITIES],
  getLabelKey: (priority?: string | null): string | undefined =>
    priority ? PROJECT_PRIORITY_LABEL_KEYS[priority as ProjectPriority] : undefined,
};

export const PROJECT_HEALTHS = ["Green", "Amber", "Red"] as const;
export type ProjectHealth = (typeof PROJECT_HEALTHS)[number] | "Unknown";

const PROJECT_HEALTH_LABEL_KEYS: Record<ProjectHealth, string> = {
  Green: "projectHealth.green",
  Amber: "projectHealth.amber",
  Red: "projectHealth.red",
  Unknown: "projectHealth.unknown",
};

const PROJECT_HEALTH_STATUS_LABEL_KEYS: Record<ProjectHealth, string> = {
  Green: "projectHealth.onTrack",
  Amber: "projectHealth.atRisk",
  Red: "projectHealth.critical",
  Unknown: "projectHealth.notAssessed",
};

export const ProjectHealthHelper = {
  allValues: (): string[] => [...PROJECT_HEALTHS],
  getLabelKey: (health?: string | null): string | undefined =>
    health ? PROJECT_HEALTH_LABEL_KEYS[health as ProjectHealth] : undefined,
  getStatusLabelKey: (health?: string | null): string | undefined =>
    health ? PROJECT_HEALTH_STATUS_LABEL_KEYS[health as ProjectHealth] : undefined,
};


export const PLANNING_ACTIVITY_TYPE = { task: "Task", milestone: "Milestone" } as const;
export const PLANNING_DEPENDENCY_TYPE = { finishToStart: "FinishToStart", startToStart: "StartToStart", finishToFinish: "FinishToFinish", startToFinish: "StartToFinish" } as const;
