import { describe, expect, it } from "vitest";
import { distributePlannedTaskWork } from "../features/projects/lib/assignment-distribution";

const candidate = (resourceId: string, projectAllocationPercentage: number, remainingAssignableHours: number) => ({
  resourceId,
  projectAllocationPercentage,
  remainingAssignableHours,
});

describe("distributePlannedTaskWork", () => {
  it("splits 40 planned hours between two 50% project allocations", () => {
    const result = distributePlannedTaskWork(40, 0, [candidate("a", 50, 24), candidate("b", 50, 24)]);

    expect(result.assignments.map((assignment) => assignment.assignedWorkHours)).toEqual([20, 20]);
    expect(result.unassignedWorkHours).toBe(0);
  });

  it("keeps work unassigned when selected allocation entitlements sum below the task plan", () => {
    const result = distributePlannedTaskWork(40, 0, [candidate("a", 50, 24)]);

    expect(result.assignments[0]?.assignedWorkHours).toBe(20);
    expect(result.unassignedWorkHours).toBe(20);
  });

  it("proportionally rebalances entitlements that exceed planned work", () => {
    const result = distributePlannedTaskWork(40, 0, [candidate("a", 75, 40), candidate("b", 75, 40)]);

    expect(result.assignments.map((assignment) => assignment.assignedWorkHours)).toEqual([20, 20]);
    expect(result.unassignedWorkHours).toBe(0);
  });

  it("caps assignment by actual available capacity without inflating another resource's share", () => {
    const result = distributePlannedTaskWork(40, 0, [candidate("a", 50, 10), candidate("b", 50, 24)]);

    expect(result.assignments.map((assignment) => assignment.assignedWorkHours)).toEqual([10, 20]);
    expect(result.assignments.map((assignment) => assignment.remainingCapacityAfterAssignment)).toEqual([0, 4]);
    expect(result.unassignedWorkHours).toBe(10);
  });

  it("preserves existing manual hours and distributes only remaining task work", () => {
    const result = distributePlannedTaskWork(40, 10, [candidate("a", 50, 24), candidate("b", 50, 24)]);

    expect(result.existingAssignedWorkHours).toBe(10);
    expect(result.assignments.map((assignment) => assignment.assignedWorkHours)).toEqual([15, 15]);
    expect(result.unassignedWorkHours).toBe(0);
  });

  it("rounds to hundredths and reconciles the allocated total", () => {
    const result = distributePlannedTaskWork(10, 0, [candidate("a", 33.33, 10), candidate("b", 66.67, 10)]);

    expect(result.assignments.map((assignment) => assignment.assignedWorkHours)).toEqual([3.33, 6.67]);
    expect(result.unassignedWorkHours).toBe(0);
  });
});
