import { Check } from "lucide-react";
import { Button } from "@components/ui/Button";
import type { WorkItemStatusDto } from "@api/workItems";
import type { WorkflowTransitionOption } from "@api/workItemsFull";
import { useI18n } from "@i18n/messages";

interface WorkItemStatusTrackProps {
  statuses: readonly WorkItemStatusDto[];
  currentStateCode: string;
  currentStateNameEn?: string;
  currentStateNameAr?: string;
  transitions: readonly WorkflowTransitionOption[];
  canTransition: boolean;
  transitionPending: boolean;
  loading: boolean;
  error: boolean;
  onRetry?: () => void;
  onTransition: (transitionCode: string) => void;
  className?: string;
}

export function WorkItemStatusTrack({
  statuses,
  currentStateCode,
  currentStateNameEn,
  currentStateNameAr,
  transitions,
  canTransition,
  transitionPending,
  loading,
  error,
  onRetry,
  onTransition,
  className = "flex flex-wrap gap-2",
}: WorkItemStatusTrackProps) {
  const { t, locale } = useI18n();
  // The API returns Work Item statuses in DisplayOrder, then Code order. Keep that
  // authoritative sequence; filter only inactive states that are not current.
  const orderedStatuses = statuses.filter((status) => status.isActive || status.code === currentStateCode);

  if (loading) return <span className="text-xs text-muted-foreground">{t("common.loading")}</span>;
  if (error) return <div role="alert" className="flex flex-wrap items-center gap-2 text-xs text-destructive">
    <span>{t("workItems.statusOrderLoadError")}</span>
    {onRetry ? <Button type="button" size="sm" variant="outline" className="h-7 px-2 text-xs" onClick={onRetry}>{t("common.retry")}</Button> : null}
  </div>;
  if (!orderedStatuses.length || !orderedStatuses.some((status) => status.code === currentStateCode)) {
    return <span role="alert" className="text-xs text-muted-foreground">{t("workItems.statusOrderUnavailable")}</span>;
  }

  return <div className={className}>
    {orderedStatuses.map((status) => {
      const isCurrent = status.code === currentStateCode;
      const matchingTransitions = transitions.filter((option) => option.targetStateCode === status.code);
      const transition = matchingTransitions.find((option) => option.canExecute) ?? matchingTransitions[0];
      const label = isCurrent
        ? locale === "ar" ? currentStateNameAr || status.nameAr : currentStateNameEn || status.nameEn
        : transition
          ? locale === "ar" ? transition.targetStateNameAr || status.nameAr : transition.targetStateNameEn || status.nameEn
          : locale === "ar" ? status.nameAr : status.nameEn;

      return <Button
        key={status.code}
        type="button"
        size="sm"
        variant={isCurrent ? "active" : "outline"}
        disabled={isCurrent || !canTransition || !transition || !transition.canExecute || transitionPending}
        aria-label={isCurrent ? t("workItems.currentStatus") : undefined}
        onClick={() => { if (transition) onTransition(transition.code); }}
      >
        {isCurrent ? <Check className="me-1.5 h-3.5 w-3.5" /> : null}
        {label}
      </Button>;
    })}
  </div>;
}
