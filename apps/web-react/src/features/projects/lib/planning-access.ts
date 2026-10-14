export function canPerformPlanningWrites(
  hasManagePermission: boolean,
  canMutate: boolean,
  effectiveReadOnly: boolean,
): boolean {
  return hasManagePermission && canMutate && !effectiveReadOnly;
}

export type PlanningBaselineUnavailableReason = "permission-required" | "editable-context-required";

export function getPlanningBaselineUnavailableReason(
  hasLifecyclePermission: boolean,
  canMutate: boolean,
  effectiveReadOnly: boolean,
): PlanningBaselineUnavailableReason | null {
  if (!hasLifecyclePermission) return "permission-required";
  if (!canMutate || effectiveReadOnly) return "editable-context-required";
  return null;
}
