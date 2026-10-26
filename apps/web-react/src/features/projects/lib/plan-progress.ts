export function roundPlanProgress(progress: number | null | undefined): number | null {
  return progress == null || !Number.isFinite(progress) ? null : Math.round(progress);
}
