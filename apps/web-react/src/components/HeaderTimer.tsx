import { Clock3 } from "lucide-react";
import { useCurrentTimer, type TimerSession } from "../api/timer";
import { useAuth } from "../auth/AuthProvider";
import { useManagement } from "../api/management";
import { useI18n, formatNumber } from "../i18n/messages";
import * as React from "react";

function activeTimer(session: TimerSession | null | undefined): boolean { return Boolean(session && ["Running", "OnBreak", "Paused", "IdlePaused"].includes(session.status)); }
function elapsedSeconds(session: TimerSession): number { return Math.max(0, session.metrics.workSeconds + Math.floor((Date.now() - new Date(session.serverNow).getTime()) / 1000)); }
function formatDuration(seconds: number): string { const hours = Math.floor(seconds / 3600); const minutes = Math.floor((seconds % 3600) / 60); const remaining = seconds % 60; return `${formatNumber(hours).padStart(2, "0")}:${formatNumber(minutes).padStart(2, "0")}:${formatNumber(remaining).padStart(2, "0")}`; }

export function HeaderTimer() {
  const { isAuthenticated } = useAuth();
  const { loading: managementLoading } = useManagement();
  const { t } = useI18n();
  const query = useCurrentTimer(isAuthenticated && !managementLoading);
  const [now, setNow] = React.useState(Date.now());
  const session = query.data;
  const isActive = activeTimer(session);
  React.useEffect(() => { if (!isActive) return; const timer = window.setInterval(() => setNow(Date.now()), 1000); return () => window.clearInterval(timer); }, [isActive]);
  void now;
  const label = query.isError ? t("timerUnavailable") : isActive && session ? `${t("activeTimer")} ${formatDuration(elapsedSeconds(session))}` : t("noActiveTimer");
  return <button className={`timer-entry ${isActive ? "timer-active" : ""}`} type="button" title={label} aria-label={label}><Clock3 size={16} /><span>{isActive && session ? formatDuration(elapsedSeconds(session)) : t("trackTime")}</span></button>;
}
