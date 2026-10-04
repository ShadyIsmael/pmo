export type ProjectDetailTab =
  | "overview"
  | "plan"
  | "team"
  | "financials"
  | "risks-issues"
  | "status-reports"
  | "meetings"
  | "documents"
  | "handover"
  | "history"
  | "snapshots"
  | "change-tracker";

export function isProjectTabVisible(activeTab: string, tab: ProjectDetailTab): boolean {
  return activeTab === tab;
}

export function shouldLoadProjectOverview(activeTab: string): boolean {
  return activeTab === "overview" || activeTab === "financials";
}

export function shouldLoadProjectLookup(isOpen: boolean): boolean {
  return isOpen;
}
