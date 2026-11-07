import { useI18n } from "@i18n/messages";
import { ProjectHealthHelper, ProjectPriorityHelper, ProjectStatusHelper } from "@lib/enums";

export function getEvmReasonTranslationCandidates(reason: string): string[] {
  const value = reason.trim();
  if (!value) return [];

  const camelCase = value.toLowerCase().replace(/[_\s-]+([a-z0-9])/g, (_, character: string) => character.toUpperCase());
  const alias = camelCase === "evmDataIncomplete" ? "dataIncomplete" : undefined;
  return [...new Set([value, camelCase, alias].filter((candidate): candidate is string => Boolean(candidate)))];
}

export function useProjectEnumLabels() {
  const { t } = useI18n();

  const statusLabel = (status?: string | null): string => {
    const key = ProjectStatusHelper.getLabelKey(status);
    return key ? t(`enums.${key}`) : status ?? "—";
  };

  const priorityLabel = (priority?: string | null): string => {
    const key = ProjectPriorityHelper.getLabelKey(priority);
    return key ? t(`enums.${key}`) : priority ?? "—";
  };

  const healthLabel = (health?: string | null): string => {
    const key = ProjectHealthHelper.getLabelKey(health);
    return key ? t(`enums.${key}`) : health ?? "—";
  };

  const healthStatusLabel = (health?: string | null): string => {
    const key = ProjectHealthHelper.getStatusLabelKey(health);
    return key ? t(`enums.${key}`) : health?.trim() || "—";
  };

  return { statusLabel, priorityLabel, healthLabel, healthStatusLabel };
}
