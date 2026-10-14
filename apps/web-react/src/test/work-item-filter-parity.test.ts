import { describe, expect, it } from "vitest";
import { buildWorkItemListQuery, emptyWorkItemListFilters, readWorkItemListLocation } from "@features/delivery/workItemFilters";

describe("work-item API filters", () => {
  it("omits unselected filter values and trims search", () => {
    expect(buildWorkItemListQuery(emptyWorkItemListFilters, "  platform  ", 2, 100)).toEqual({
      page: 2,
      pageSize: 100,
      search: "platform",
      type: undefined,
      status: undefined,
      priority: undefined,
      assigneeId: undefined,
      myWork: undefined,
      unassigned: undefined,
      overdue: undefined,
      highPriority: undefined,
      sortBy: "createdAt",
      projectId: undefined,
      dueDateFrom: undefined,
      dueDateTo: undefined,
    });
  });

  it("maps advanced filters to the canonical API query names", () => {
    expect(buildWorkItemListQuery({
      type: "Issue",
      status: "InProgress",
      priority: "High",
      assignee: "user-123",
      projectId: "project-456",
      dueFrom: "2026-10-01",
      dueTo: "2026-10-31",
    }, "", 1, 100)).toMatchObject({
      type: "Issue",
      status: "InProgress",
      priority: "High",
      assigneeId: "user-123",
      projectId: "project-456",
      dueDateFrom: "2026-10-01",
      dueDateTo: "2026-10-31",
    });
  });

  it("maps My Work and unassigned shortcuts without passing sentinel IDs", () => {
    expect(buildWorkItemListQuery({ ...emptyWorkItemListFilters, assignee: "me" }, "", 1, 100)).toMatchObject({ myWork: true, assigneeId: undefined });
    expect(buildWorkItemListQuery({ ...emptyWorkItemListFilters, assignee: "unassigned" }, "", 1, 100)).toMatchObject({ unassigned: true, assigneeId: undefined });
  });

  it("maps quick views to real API filters and sort order", () => {
    expect(buildWorkItemListQuery(emptyWorkItemListFilters, "", 1, 100, "overdue")).toMatchObject({ overdue: true });
    expect(buildWorkItemListQuery(emptyWorkItemListFilters, "", 1, 100, "high-priority")).toMatchObject({ highPriority: true });
    expect(buildWorkItemListQuery(emptyWorkItemListFilters, "", 1, 100, "recently-updated")).toMatchObject({ sortBy: "updatedAt" });
    expect(buildWorkItemListQuery(emptyWorkItemListFilters, "", 1, 100, "my-items")).toMatchObject({ myWork: true });
  });

  it("restores valid URL filters and falls back safely for malformed query values", () => {
    expect(readWorkItemListLocation(new URLSearchParams("search=release&type=Issue&status=InProgress&priority=High&assignee=user-1&projectId=project-1&dueFrom=2026-10-01&dueTo=2026-10-31&quick=overdue&group=status&sort=updatedAt&view=kanban&page=3"))).toEqual({
      search: "release",
      filters: { type: "Issue", status: "InProgress", priority: "High", assignee: "user-1", projectId: "project-1", dueFrom: "2026-10-01", dueTo: "2026-10-31" },
      quickView: "overdue",
      groupBy: "status",
      sortBy: "updatedAt",
      view: "kanban",
      page: 3,
    });
    expect(readWorkItemListLocation(new URLSearchParams("type=invalid&quick=bogus&group=bad&sort=unknown&page=-4"))).toMatchObject({
      filters: { type: "all" }, quickView: "all", groupBy: "none", sortBy: "createdAt", view: "list", page: 1,
    });
  });
});
