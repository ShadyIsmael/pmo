import * as React from "react";
import { Outlet, NavLink } from "react-router-dom";
import { useI18n } from "@i18n/messages";
import { ClipboardCheck, BadgeCheck, CalendarDays } from "lucide-react";
import { cn } from "@lib/utils";

export function MyWorkLayout() {
  const { t } = useI18n();

  const tabs = [
    { id: "tasks", label: t("nav.tasks"), href: "/my-work/tasks", icon: ClipboardCheck },
    { id: "approvals", label: t("nav.approvals"), href: "/my-work/approvals", icon: BadgeCheck },
    { id: "timesheets", label: t("timeTracking.title"), href: "/my-work/timesheets", icon: CalendarDays },
  ];

  return (
    <div className="space-y-4">
      <nav className="flex gap-1 overflow-x-auto pb-2" aria-label="My Work tabs">
        {tabs.map((tab) => (
          <NavLink
            key={tab.id}
            to={tab.href}
            className={({ isActive }) =>
              cn(
                "flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium whitespace-nowrap transition-colors",
                isActive
                  ? "bg-primary text-primary-foreground"
                  : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              )
            }
          >
            <tab.icon className="h-4 w-4" />
            {tab.label}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </div>
  );
}