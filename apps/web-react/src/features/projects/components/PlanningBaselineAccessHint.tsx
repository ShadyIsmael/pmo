import { Info } from "lucide-react";
import type { PlanningBaselineUnavailableReason } from "../lib/planning-access";

export function PlanningBaselineAccessHint({
  reason,
  t,
}: {
  reason: PlanningBaselineUnavailableReason | null;
  t: (key: string) => string;
}) {
  if (!reason) return null;

  const permissionRequired = reason === "permission-required";
  return (
    <span
      role="note"
      className={`inline-flex max-w-[18rem] items-start gap-1.5 px-1 text-start text-[10px] leading-4 ${permissionRequired ? "text-info" : "text-warning"}`}
    >
      <Info aria-hidden="true" className="mt-0.5 h-3.5 w-3.5 shrink-0" />
      {t(permissionRequired ? "planning.baselineLifecycleRequired" : "planning.baselineEditableContextRequired")}
    </span>
  );
}
