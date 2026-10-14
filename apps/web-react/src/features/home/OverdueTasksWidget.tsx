import { useI18n } from "@i18n/messages";
import type { WorkItemDto } from "@api/workItemsFull";

export function OverdueTasksWidget({ workItems }: { workItems: readonly WorkItemDto[] }) {
  const { t } = useI18n();
  const today = new Date().toISOString().slice(0, 10);
  const overdueTasks = workItems.filter((item) =>
    item.type === "Task" &&
    Boolean(item.dueDate && item.dueDate.slice(0, 10) < today) &&
    item.status.toLowerCase() !== "completed",
  );
  const criticalCount = overdueTasks.filter((task) => task.priority === "Critical").length;

  return (
    <div className="flex h-full min-h-32 flex-col items-center justify-center gap-1">
      <span className="text-3xl font-bold text-destructive">{overdueTasks.length}</span>
      <span className="text-xs text-muted-foreground">{t("dashboard.overdueTasks")}</span>
      <span className="text-xs text-destructive">{criticalCount} {t("dashboard.critical")}</span>
    </div>
  );
}
