"use client";

import * as React from "react";
import { Coffee, Pause, Play, RotateCcw, Square, Timer, Zap } from "lucide-react";
import { useTranslations, useLocale } from "next-intl";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth";
import { useManagementPerspective } from "@/lib/perspective-context";
import { useCurrentTimer, useStartTimer, useTimerAction, useTimerHeartbeat, type TimerStatus } from "@/hooks/time-tracking/useTimer";
import { deriveTimerTotals, estimateServerNowMs, formatDurationSeconds, isTimerActive, sessionReceivedAtMs } from "@/lib/timer/timer-totals";
import { FinalizeSessionSideSheet } from "@/components/time-tracking/FinalizeSessionSideSheet";
import { cn } from "@/lib/utils";

const IDLE_MINUTES = 15;

export function LiveTimerShell() {
  const t = useTranslations("timeTracking");
  const locale = useLocale();
  const { user } = useAuth();
  const { effectiveReadOnly } = useManagementPerspective();
  const canControl = Boolean(user?.permissions?.includes("TimeTracking.ManageOwn")) && !effectiveReadOnly;
  const current = useCurrentTimer(canControl);
  const start = useStartTimer();
  const heartbeat = useTimerHeartbeat();
  const pauseAction = useTimerAction("pause");
  const breakAction = useTimerAction("break");
  const resumeAction = useTimerAction("resume");
  const finishAction = useTimerAction("finish");
  const [finalizeOpen, setFinalizeOpen] = React.useState(false);
  const lastActivity = React.useRef(Date.now());
  const { status: authStatus } = useAuth();
  const session = current.data ?? null;
  const sessionId = session?.id;
  const sessionStatus = session?.status;
  const sendHeartbeat = heartbeat.mutate;

  const [now, setNow] = React.useState(() => Date.now());
  const shouldTick = Boolean(sessionId) && isTimerActive(sessionStatus);
  React.useEffect(() => {
    if (!shouldTick) return;
    setNow(Date.now());
    const interval = window.setInterval(() => setNow(Date.now()), 1_000);
    const onVisibility = () => {
      if (document.visibilityState === "hidden") {
        lastActivity.current = Date.now() - IDLE_MINUTES * 60_000;
      } else {
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
    if (!sessionId || (sessionStatus !== "Running" && sessionStatus !== "OnBreak" && sessionStatus !== "Paused" && sessionStatus !== "IdlePaused")) return;
    const events = ["mousemove", "mousedown", "keydown", "touchstart"];
    const markActivity = () => { lastActivity.current = Date.now(); };
    events.forEach((event) => window.addEventListener(event, markActivity, { passive: true }));
    const interval = window.setInterval(() => {
      const idle = Date.now() - lastActivity.current >= IDLE_MINUTES * 60_000;
      if (sessionStatus === "Running" || sessionStatus === "IdlePaused") {
        sendHeartbeat({ id: sessionId, isIdle: idle, lastActivityAt: new Date(lastActivity.current).toISOString() });
      }
    }, 30_000);
    return () => { events.forEach((event) => window.removeEventListener(event, markActivity)); window.clearInterval(interval); };
  }, [sessionId, sessionStatus, sendHeartbeat]);

  const previousStatus = React.useRef<TimerStatus | null>(null);
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
  const isIdle = sessionStatus === "IdlePaused";
  const needsFinalization = sessionStatus === "AwaitingFinalization";

  const errors = [
    start.isError && (start.error instanceof Error ? start.error.message : t("startError")),
    finishAction.isError && (finishAction.error instanceof Error ? finishAction.error.message : t("finishError")),
    pauseAction.isError && (pauseAction.error instanceof Error ? pauseAction.error.message : t("pauseError")),
    breakAction.isError && (breakAction.error instanceof Error ? breakAction.error.message : t("breakError")),
    resumeAction.isError && (resumeAction.error instanceof Error ? resumeAction.error.message : t("resumeError")),
  ].filter((message): message is string => Boolean(message));

  return (
    <>
      <div
        role="region"
        aria-label={t("timer")}
        data-testid="floating-timer-card"
        className={cn(
          "fixed bottom-4 end-4 z-40 w-80 max-w-[calc(100vw-2rem)] rounded-lg border border-border bg-background shadow-lg backdrop-blur",
          isIdle && "border-amber-300 bg-amber-50/95 dark:border-amber-800 dark:bg-amber-950/90",
          needsFinalization && "border-primary/40"
        )}
      >
        <div className="flex flex-col gap-2.5 p-3">
          <div className="flex items-center gap-2">
            <span className={cn("flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary", isIdle && "bg-amber-500/15 text-amber-600")}>
              <Timer className="h-4 w-4" />
            </span>
            {session && totals ? (
              <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium text-muted-foreground" data-testid="timer-status">
                {t(`status.${session.status}` as never)}
              </span>
            ) : (
              <span className="text-xs font-semibold text-foreground">{t("timerReady")}</span>
            )}
            {!session && <span className="ms-auto text-xs font-semibold text-muted-foreground">{t("timer")}</span>}
          </div>

          {session && totals && (
            <>
              <div className="flex flex-col items-center gap-0.5 py-1">
                <span className="font-mono text-2xl tabular-nums text-foreground" data-testid="timer-work-counter">
                  {formatDurationSeconds(totals.workSeconds, locale)}
                </span>
                <span className="text-[11px] text-muted-foreground">{t("workTime")}</span>
              </div>

              {!needsFinalization && (
                <div className="grid grid-cols-2 gap-2">
                  <div className="rounded-md bg-muted/50 px-2 py-1.5 text-center">
                    <div className="font-mono text-xs tabular-nums text-foreground" data-testid="timer-break-counter">
                      {formatDurationSeconds(totals.breakSeconds, locale)}
                    </div>
                    <div className="text-[10px] text-muted-foreground">{t("breakTime")}</div>
                  </div>
                  <div className="rounded-md bg-muted/50 px-2 py-1.5 text-center">
                    <div className="font-mono text-xs tabular-nums text-foreground" data-testid="timer-pause-counter">
                      {formatDurationSeconds(totals.pauseSeconds, locale)}
                    </div>
                    <div className="text-[10px] text-muted-foreground">{t("pauseTime")}</div>
                  </div>
                </div>
              )}

              <div className="flex flex-wrap items-center justify-center gap-1.5">
                {sessionStatus === "Running" && (
                  <>
                    <Button size="sm" variant="outline" className="h-7 gap-1.5 text-xs" data-testid="timer-break-button" onClick={() => breakAction.mutate(session.id)} disabled={breakAction.isPending}>
                      <Coffee className="h-3.5 w-3.5" />{t("break")}
                    </Button>
                    <Button size="sm" variant="outline" className="h-7 gap-1.5 text-xs" data-testid="timer-pause-button" onClick={() => pauseAction.mutate(session.id)} disabled={pauseAction.isPending}>
                      <Pause className="h-3.5 w-3.5" />{t("pause")}
                    </Button>
                  </>
                )}
                {(sessionStatus === "OnBreak" || sessionStatus === "Paused" || sessionStatus === "IdlePaused") && (
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-7 gap-1.5 text-xs"
                    data-testid="timer-resume-button"
                    onClick={() => resumeAction.mutate(session.id)}
                    disabled={resumeAction.isPending}
                  >
                    <RotateCcw className="h-3.5 w-3.5" />
                    {t("resume")}
                  </Button>
                )}
                {needsFinalization && (
                  <Button size="sm" className="h-7 gap-1.5 text-xs" data-testid="timer-complete-button" onClick={() => setFinalizeOpen(true)}>
                    <Zap className="h-3.5 w-3.5" />{t("complete")}
                  </Button>
                )}
                {!needsFinalization && (
                  <Button size="sm" variant="destructive" className="h-7 gap-1.5 text-xs" data-testid="timer-finish-button" onClick={() => finishAction.mutate(session.id)} disabled={finishAction.isPending}>
                    <Square className="h-3.5 w-3.5" />{t("finish")}
                  </Button>
                )}
              </div>
            </>
          )}

          {!session && (
            <div className="flex justify-center pt-0.5">
              <Button size="sm" className="h-7 gap-1.5 text-xs" data-testid="timer-start-button" onClick={() => start.mutate()} disabled={start.isPending || start.isError}>
                <Play className="h-3.5 w-3.5" />{t("startTimer")}
              </Button>
            </div>
          )}

          {isIdle && <p className="text-[11px] leading-snug text-amber-700 dark:text-amber-400">{t("idleMessage")}</p>}

          {errors.length > 0 && (
            <div className="space-y-1 border-t border-border/60 pt-2">
              {errors.map((message) => <p key={message} className="text-[11px] text-destructive">{message}</p>)}
            </div>
          )}
        </div>
      </div>

      <FinalizeSessionSideSheet open={finalizeOpen} onOpenChange={setFinalizeOpen} session={session} />
    </>
  );
}
