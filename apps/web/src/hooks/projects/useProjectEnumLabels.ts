"use client";

import { useTranslations } from "next-intl";
import {
  ProjectHealthHelper,
  ProjectPriorityHelper,
  ProjectStatusHelper,
} from "@/lib/enums";

/**
 * Shared translated label helpers for project status / priority / health values.
 * Reused by the project list, project detail, and create/edit sheet.
 */
export function useProjectEnumLabels() {
  const t = useTranslations("enums");

  const statusLabel = (status?: string | null): string => {
    const key = ProjectStatusHelper.getLabelKey(status);
    return key ? t(key) : status ?? "—";
  };

  const priorityLabel = (priority?: string | null): string => {
    const key = ProjectPriorityHelper.getLabelKey(priority);
    return key ? t(key) : priority ?? "—";
  };

  const healthLabel = (health?: string | null): string => {
    const key = ProjectHealthHelper.getLabelKey(health);
    return key ? t(key) : health ?? "—";
  };

  const healthStatusLabel = (health?: string | null): string => {
    const key = ProjectHealthHelper.getStatusLabelKey(health);
    return key ? t(key) : health?.trim() || "—";
  };

  return { statusLabel, priorityLabel, healthLabel, healthStatusLabel };
}
