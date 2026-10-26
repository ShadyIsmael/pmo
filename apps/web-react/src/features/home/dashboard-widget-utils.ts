import { Permissions } from "@lib/permissions";

export function dashboardMeetingsQueryParams(projectId?: string, now: Date = new Date()) {
  const dateFrom = now.toISOString().slice(0, 10);
  const dateTo = new Date(now.getTime());
  dateTo.setDate(dateTo.getDate() + 90);

  return {
    page: 1,
    pageSize: 50,
    dateFrom,
    dateTo: dateTo.toISOString().slice(0, 10),
    projectId,
  };
}

export function getDashboardApprovalAccess(permissions: readonly string[]) {
  const handovers = permissions.includes(Permissions.Handover.Approve);
  const projectChanges = permissions.includes(Permissions.ChangeRequest.Approve);
  return { handovers, projectChanges, any: handovers || projectChanges };
}

export function dashboardWidgetNeedsWorkItems(type: string): boolean {
  return ["my-work", "my-todo", "upcoming-milestones", "overdue-tasks", "tasks-by-status", "planned-vs-actual"].includes(type);
}

export function formatDashboardMeetingDateTime(value: string, locale: string): string {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function summarizeDashboardResourceCapacity(resources: readonly { utilizationTarget?: number; currentUtilization?: number }[]) {
  const applicable = resources.filter((resource) => typeof resource.utilizationTarget === "number" && typeof resource.currentUtilization === "number");
  return {
    resourceCount: applicable.length,
    utilization: applicable.length ? applicable.reduce((sum, resource) => sum + (resource.currentUtilization ?? 0), 0) / applicable.length : null,
  };
}

export function scopeDashboardResources<T extends { id: string }>(resources: readonly T[], perspective: string | undefined, managedResourceIds: ReadonlySet<string>): T[] {
  if (perspective !== "PortfolioManager" && perspective !== "ProgramManager") return [...resources];
  return resources.filter((resource) => managedResourceIds.has(resource.id));
}

export function scopeDashboardProjects<T extends { id: string; publicId?: string; programId?: string | null }>(
  projects: readonly T[],
  projectId: string | undefined,
  programId: string | undefined,
  perspective: string | undefined,
): T[] {
  return projects.filter((project) =>
    (!projectId || project.publicId === projectId || project.id === projectId)
    && (!(perspective === "PortfolioManager" && programId) || project.programId === programId),
  );
}

export function scopeDashboardRecordsByProject<T extends { projectId?: string | null }>(records: readonly T[], projectIds: ReadonlySet<string>): T[] {
  return records.filter((record) => Boolean(record.projectId && projectIds.has(record.projectId)));
}

export function shouldLoadDashboardManagementOverview(widgetType: string, perspective: string | undefined): boolean {
  if (perspective !== "PortfolioManager" && perspective !== "ProgramManager") return false;
  return widgetType === "status-report-freshness" || widgetType === "resource-capacity" || widgetType === "overallocated-resources";
}

export function formatDashboardMoney(value: number, currency: string | null | undefined, locale: string): string {
  const formatterLocale = locale === "ar" ? "ar-EG-u-nu-latn" : "en-US";
  return new Intl.NumberFormat(formatterLocale, {
    style: "currency",
    currency: currency || "USD",
    notation: "compact",
    maximumFractionDigits: 1,
  }).format(value);
}
