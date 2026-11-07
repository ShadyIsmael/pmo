import { BadgeCheck, CalendarDays, ClipboardCheck } from "lucide-react";
import { RouteTabs } from "@components/ui/RouteTabs";
import { useI18n } from "@i18n/messages";

/**
 * My Work horizontal tab menu. Rendered by each My Work surface directly below
 * its page header so the header always precedes the tabs. Navigation stays
 * route-backed: every tab is a deep link and only the active route renders.
 */
export function MyWorkTabs() {
  const { t } = useI18n();

  return (
    <RouteTabs
      label={t("common.myWorkTabs")}
      tabs={[
        { id: "timesheets", href: "/my-work/timesheets", label: t("timeTracking.title"), icon: CalendarDays },
        { id: "approvals", href: "/my-work/approvals", label: t("nav.approvals"), icon: BadgeCheck },
        { id: "tasks", href: "/my-work/tasks", label: t("nav.tasks"), icon: ClipboardCheck },
      ]}
    />
  );
}
