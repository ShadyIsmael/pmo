export type AssignmentDistributionCandidate = {
  resourceId: string;
  projectAllocationPercentage: number;
  remainingAssignableHours: number;
};

export type DistributedAssignment = AssignmentDistributionCandidate & {
  assignedWorkHours: number;
  remainingCapacityAfterAssignment: number;
};

export type AssignmentDistribution = {
  assignments: DistributedAssignment[];
  existingAssignedWorkHours: number;
  taskWorkHours: number;
  unassignedWorkHours: number;
};

const toHundredths = (value: number) => Math.max(0, Math.round((Number.isFinite(value) ? value : 0) * 100));

/**
 * Splits only the task work left after preserving persisted assignments.
 * Project allocation percentages define each new resource's entitlement;
 * capacity caps the result without silently redistributing that entitlement.
 */
export function distributePlannedTaskWork(
  plannedWorkHours: number,
  existingAssignedWorkHours: number,
  candidates: readonly AssignmentDistributionCandidate[],
): AssignmentDistribution {
  const taskWorkCents = toHundredths(plannedWorkHours);
  const existingCents = toHundredths(existingAssignedWorkHours);
  const remainingTaskWorkCents = Math.max(0, taskWorkCents - existingCents);
  const entitlements = candidates.map((candidate) => ({
    candidate,
    rawHours: remainingTaskWorkCents / 100 * Math.max(0, candidate.projectAllocationPercentage) / 100,
  }));
  const totalRawHours = entitlements.reduce((total, item) => total + item.rawHours, 0);
  const remainingTaskWorkHours = remainingTaskWorkCents / 100;
  const mustRebalance = totalRawHours > remainingTaskWorkHours + 0.000001;
  const desiredTotalCents = mustRebalance
    ? remainingTaskWorkCents
    : Math.min(remainingTaskWorkCents, toHundredths(totalRawHours));
  const allocationScale = mustRebalance && totalRawHours > 0
    ? remainingTaskWorkHours / totalRawHours
    : 1;
  const roundedTargets = entitlements.map((item, index) => {
    const exactCents = item.rawHours * allocationScale * 100;
    const floorCents = Math.floor(exactCents + 1e-9);
    return { index, exactCents, floorCents, fraction: exactCents - floorCents };
  });
  let centsToDistribute = Math.max(0, desiredTotalCents - roundedTargets.reduce((sum, item) => sum + item.floorCents, 0));
  const roundingOrder = [...roundedTargets].sort((left, right) => right.fraction - left.fraction || left.index - right.index);
  for (let index = 0; centsToDistribute > 0 && roundingOrder.length > 0; index++, centsToDistribute--) {
    roundingOrder[index % roundingOrder.length]!.floorCents++;
  }

  const assignments = entitlements.map(({ candidate }, index) => {
    const desiredCents = roundedTargets[index]?.floorCents ?? 0;
    const capacityCents = toHundredths(candidate.remainingAssignableHours);
    const assignedCents = Math.min(desiredCents, capacityCents);
    return {
      ...candidate,
      assignedWorkHours: assignedCents / 100,
      remainingCapacityAfterAssignment: Math.max(0, capacityCents - assignedCents) / 100,
    };
  });
  const assignedCents = assignments.reduce((total, assignment) => total + toHundredths(assignment.assignedWorkHours), 0);

  return {
    assignments,
    existingAssignedWorkHours: existingCents / 100,
    taskWorkHours: taskWorkCents / 100,
    unassignedWorkHours: Math.max(0, remainingTaskWorkCents - assignedCents) / 100,
  };
}
