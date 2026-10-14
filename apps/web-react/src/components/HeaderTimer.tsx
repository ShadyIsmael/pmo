import * as React from "react";
import { createPortal } from "react-dom";
import { Coffee, Pause, Play, RotateCcw, Square, Timer, X, Zap } from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import { useManagement } from "../api/management";
import { useI18n } from "../i18n/messages";
import { Button } from "./ui/Button";
import { cn } from "../lib/utils";
import {
  useCurrentTimer,
  useStartTimer,
  useTimerAction,
  useTimerHeartbeat,
} from "../api/timer";
import { deriveTimerTotals, estimateServerNowMs, formatDurationSeconds, isTimerActive, sessionReceivedAtMs } from "../lib/timer-totals";
import { FinalizeSessionSideSheet } from "../features/time/FinalizeSessionSideSheet";

const IDLE_MINUTES = 15;

export function HeaderTimer() {
  const { t } = useI18n();
  const { user, status: authStatus } = useAuth();
  const { effectiveReadOnly, loading: managementLoading } = useManagement();
  const canControl = Boolean(user?.permissions?.includes("TimeTracking.ManageOwn")) && !effectiveReadOnly;
  // VISIBLE-FIRST: single GET on startup; no polling. Local 1s tick when active.
  const current = useCurrentTimer(canControl && !managementLoading && authStatus === "authenticated");
  const start = useStartTimer();
  const heartbeat = useTimerHeartbeat();
  const pauseAction = useTimerAction("pause");
  const breakAction = useTimerAction("break");
  const resumeAction = useTimerAction("resume");
  const finishAction = useTimerAction("finish");
  const [open, setOpen] = React.useState(false);
  const [finalizeOpen, setFinalizeOpen] = React.useState(false);
  const lastActivity = React.useRef(Date.now());
  const previousStatus = React.useRef<string | null>(null);
  const [now, setNow] = React.useState(() => Date.now());
  const session = current.data ?? null;
  const sessionId = session?.id;
  const sessionStatus = session?.status;
  const shouldTick = Boolean(sessionId) && isTimerActive(sessionStatus);

  React.useEffect(() => {
    if (!shouldTick) return;
    setNow(Date.now());
    const interval = window.setInterval(() => setNow(Date.now()), 1000);
    const onVisibility = () => {
      if (document.visibilityState === "hidden") lastActivity.current = Date.now() - IDLE_MINUTES * 60_000;
      else {
        lastActivity.current = Date.now();
        setNow(Date.now());
      }
    };
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      window.clearInterval(interval);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [shouldTick, sessionId, sessionStatus]);

  React.useEffect(() => {
    if (!sessionId || !["Running", "OnBreak", "Paused", "IdlePaused"].includes(sessionStatus ?? "")) return;
    const events = ["mousemove", "mousedown", "keydown", "touchstart"];
    const markActivity = () => { lastActivity.current = Date.now(); };
    events.forEach((event) => window.addEventListener(event, markActivity, { passive: true }));
    const interval = window.setInterval(() => {
      const idle = Date.now() - lastActivity.current >= IDLE_MINUTES * 60_000;
      if (sessionStatus === "Running" || sessionStatus === "IdlePaused") {
        heartbeat.mutate({ id: sessionId, isIdle: idle, lastActivityAt: new Date(lastActivity.current).toISOString() });
      }
    }, 30_000);
    return () => {
      events.forEach((event) => window.removeEventListener(event, markActivity));
      window.clearInterval(interval);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId, sessionStatus]);

  React.useEffect(() => {
    const previous = previousStatus.current;
    if (sessionStatus === "AwaitingFinalization" && previous && previous !== "AwaitingFinalization") {
      setFinalizeOpen(true);
    }
    previousStatus.current = sessionStatus ?? null;
  }, [sessionStatus]);

  if (authStatus === "loading" || !canControl) return null;

  const serverNowMs = session
    ? isTimerActive(session.status)
      ? estimateServerNowMs(session, sessionReceivedAtMs(session), now)
      : new Date(session.serverNow).getTime()
    : Date.now();
  const totals = session ? deriveTimerTotals(session, serverNowMs) : null;
  const workSeconds = totals?.workSeconds ?? 0;
  const needsFinalization = sessionStatus === "AwaitingFinalization";
  const statusLabel = session ? t(`timeTracking.status.${session.status}`) : t("timeTracking.trackTime");
  const triggerValue = needsFinalization ? t("timeTracking.needsCompletion") : session ? formatDurationSeconds(workSeconds) : t("timeTracking.trackTime");
  const errors = [
    start.isError && (start.error instanceof Error ? start.error.message : t("timeTracking.startError")),
    finishAction.isError && (finishAction.error instanceof Error ? finishAction.error.message : t("timeTracking.finishError")),
    pauseAction.isError && (pauseAction.error instanceof Error ? pauseAction.error.message : t("timeTracking.pauseError")),
    breakAction.isError && (breakAction.error instanceof Error ? breakAction.error.message : t("timeTracking.breakError")),
    resumeAction.isError && (resumeAction.error instanceof Error ? resumeAction.error.message : t("timeTracking.resumeError")),
  ].filter((message): message is string => Boolean(message));
  const statusTone = !session
    ? "border-primary/40 bg-primary/10 text-primary hover:bg-primary/15"
    : sessionStatus === "Running"
      ? "border-success/40 bg-success/10 text-success"
      : sessionStatus === "OnBreak" || sessionStatus === "IdlePaused"
        ? "border-warning/40 bg-warning/10 text-warning"
        : needsFinalization
          ? "border-destructive/40 bg-destructive/10 text-destructive"
          : "border-border bg-muted text-foreground";

  const StatusIcon = !session ? Timer : sessionStatus === "OnBreak" ? Coffee : needsFinalization ? Zap : Timer;

  function runAction(mutation: { mutate: (id: string) => void }) {
    if (!session) return;
    mutation.mutate(session.id);
  }

  return (
    <>
      <div>
        <Button
          variant="outline"
          size="sm"
          className={cn("h-8 max-w-40 gap-1.5 px-2 text-xs font-medium shadow-sm", statusTone)}
          aria-label={statusLabel}
          onClick={() => setOpen((v) => !v)}
        >
          <StatusIcon className="h-3.5 w-3.5 shrink-0" />
          <span className="hidden truncate tabular-nums sm:inline">{triggerValue}</span>
        </Button>
        {open && typeof document !== "undefined" ? createPortal(
          <>
            <button type="button" aria-label={t("common.close")} onClick={() => setOpen(false)} className="fixed inset-0 z-[99] cursor-default bg-transparent" />
            <div role="dialog" aria-label={statusLabel} data-testid="header-timer-popover" className="fixed top-14 right-4 z-[100] max-h-[calc(100vh-4.5rem)] w-80 max-w-[calc(100vw-2rem)] overflow-y-auto rounded-md border bg-popover p-3 text-popover-foreground shadow-lg sm:top-16 sm:right-6">
              <div className="space-y-3">
                <div className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold">{needsFinalization ? t("timeTracking.needsCompletion") : session ? statusLabel : t("timeTracking.trackTime")}</p>
                    <p className="text-xs text-muted-foreground">{t("timeTracking.headerTimerDescription")}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-1">
                    {session && totals ? <span className="font-mono text-lg tabular-nums">{formatDurationSeconds(totals.workSeconds)}</span> : <Timer className="h-5 w-5 text-muted-foreground" />}
                    <Button variant="ghost" size="icon" className="h-7 w-7" aria-label={t("common.close")} onClick={() => setOpen(false)}>
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
                {session ? (
                  <>
                    {totals ? <div className="grid grid-cols-4 gap-1.5 text-center text-[10px]">
                      <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("timeTracking.workTime")}</span><strong className="font-mono" data-testid="header-timer-work">{formatDurationSeconds(totals.workSeconds)}</strong></div>
                      <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("timeTracking.breakTime")}</span><strong className="font-mono" data-testid="header-timer-break-counter">{formatDurationSeconds(totals.breakSeconds)}</strong></div>
                      <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("timeTracking.pauseTime")}</span><strong className="font-mono" data-testid="header-timer-pause">{formatDurationSeconds(totals.pauseSeconds)}</strong></div>
                      <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("timeTracking.totalSession")}</span><strong className="font-mono" data-testid="header-timer-total">{formatDurationSeconds(totals.sessionSeconds)}</strong></div>
                    </div> : null}
                    {sessionStatus === "IdlePaused" ? <p className="text-xs text-warning">{t("timeTracking.idleMessage")}</p> : null}
                    <div className="flex flex-wrap justify-end gap-1.5">
                      {sessionStatus === "Running" ? (
                        <>
                          <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" onClick={() => runAction(breakAction)} disabled={breakAction.isPending}>
                            <Coffee className="h-3.5 w-3.5" />{t("timeTracking.break")}
                          </Button>
                          <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" onClick={() => runAction(pauseAction)} disabled={pauseAction.isPending}>
                            <Pause className="h-3.5 w-3.5" />{t("timeTracking.pause")}
                          </Button>
                        </>
                      ) : null}
                      {sessionStatus === "OnBreak" || sessionStatus === "Paused" || sessionStatus === "IdlePaused" ? (
                        <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" onClick={() => runAction(resumeAction)} disabled={resumeAction.isPending}>
                          <RotateCcw className="h-3.5 w-3.5" />{t("timeTracking.resume")}
                        </Button>
                      ) : null}
                      {needsFinalization ? (
                        <Button size="sm" className="h-7 gap-1 text-xs" onClick={() => setFinalizeOpen(true)}>
                          <Zap className="h-3.5 w-3.5" />{t("timeTracking.complete")}
                        </Button>
                      ) : (
                        <Button size="sm" variant="destructive" className="h-7 gap-1 text-xs" onClick={() => runAction(finishAction)} disabled={finishAction.isPending}>
                          <Square className="h-3.5 w-3.5" />{t("timeTracking.finish")}
                        </Button>
                      )}
                    </div>
                  </>
                ) : (
                  <Button className="w-full" onClick={() => start.mutate(undefined)} disabled={start.isPending}>
                    <Play className="me-2 h-4 w-4" />{t("timeTracking.startTimer")}
                  </Button>
                )}
                {errors.length > 0 ? <div className="space-y-1 border-t border-border/60 pt-2">{errors.map((message) => <p key={message} role="alert" className="text-xs text-destructive">{message}</p>)}</div> : null}
              </div>
            </div>
          </>
        , document.body) : null}
      </div>
      <FinalizeSessionSideSheet open={finalizeOpen} onOpenChange={setFinalizeOpen} session={session} />
    </>
  );
}
