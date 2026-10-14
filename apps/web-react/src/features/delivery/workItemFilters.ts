export interface WorkItemListFilters {
  type: string;
  status: string;
  priority: string;
  assignee: string;
  projectId: string;
  dueFrom: string;
  dueTo: string;
}

export type WorkItemQuickView = "all" | "my-items" | "unassigned" | "overdue" | "high-priority" | "recently-updated";
export type WorkItemGroupBy = "none" | "status" | "assignee" | "priority" | "project" | "type";
export type WorkItemSortBy = "createdAt" | "updatedAt" | "title" | "priority" | "status" | "dueDate";
export type WorkItemListView = "list" | "kanban";

export interface WorkItemListLocationState {
  search: string;
  filters: WorkItemListFilters;
  quickView: WorkItemQuickView;
  groupBy: WorkItemGroupBy;
  sortBy: WorkItemSortBy;
  view: WorkItemListView;
  page: number;
}

export const emptyWorkItemListFilters: WorkItemListFilters = {
  type: "all",
  status: "all",
  priority: "all",
  assignee: "all",
  projectId: "",
  dueFrom: "",
  dueTo: "",
};

export function readWorkItemListLocation(params: URLSearchParams): WorkItemListLocationState {
  const type = params.get("type");
  const priority = params.get("priority");
  const quickView = params.get("quick");
  const groupBy = params.get("group");
  const sortBy = params.get("sort");
  const view = params.get("view");
  const requestedPage = Number(params.get("page") ?? "1");
  return {
    search: params.get("search") ?? "",
    filters: {
      type: type === "Task" || type === "Issue" ? type : "all",
      status: params.get("status") ?? "all",
      priority: priority === "Critical" || priority === "High" || priority === "Medium" || priority === "Low" || priority === "Backlog" ? priority : "all",
      assignee: params.get("assignee") ?? "all",
      projectId: params.get("projectId") ?? "",
      dueFrom: params.get("dueFrom") ?? "",
      dueTo: params.get("dueTo") ?? "",
    },
    quickView: quickView === "my-items" || quickView === "unassigned" || quickView === "overdue" || quickView === "high-priority" || quickView === "recently-updated" ? quickView : "all",
    groupBy: groupBy === "status" || groupBy === "assignee" || groupBy === "priority" || groupBy === "project" || groupBy === "type" ? groupBy : "none",
    sortBy: sortBy === "updatedAt" || sortBy === "title" || sortBy === "priority" || sortBy === "status" || sortBy === "dueDate" ? sortBy : "createdAt",
    view: view === "kanban" ? "kanban" : "list",
    page: Number.isFinite(requestedPage) && requestedPage > 0 ? Math.floor(requestedPage) : 1,
  };
}

export function buildWorkItemListQuery(filters: WorkItemListFilters, search: string, page: number, pageSize: number, quickView: WorkItemQuickView = "all", sortBy: WorkItemSortBy = "createdAt") {
  return {
    page,
    pageSize,
    search: search.trim() || undefined,
    type: filters.type === "all" ? undefined : filters.type,
    status: filters.status === "all" ? undefined : filters.status,
    priority: filters.priority === "all" ? undefined : filters.priority,
    assigneeId: ["all", "me", "unassigned"].includes(filters.assignee) ? undefined : filters.assignee,
    myWork: filters.assignee === "me" || quickView === "my-items" ? true : undefined,
    unassigned: filters.assignee === "unassigned" || quickView === "unassigned" ? true : undefined,
    overdue: quickView === "overdue" ? true : undefined,
    highPriority: quickView === "high-priority" ? true : undefined,
    sortBy: quickView === "recently-updated" ? "updatedAt" : sortBy,
    projectId: filters.projectId || undefined,
    dueDateFrom: filters.dueFrom || undefined,
    dueDateTo: filters.dueTo || undefined,
  };
}
