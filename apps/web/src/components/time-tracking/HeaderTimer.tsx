"use client";

import * as React from "react";
import { createPortal } from "react-dom";
import { Coffee, Pause, Play, RotateCcw, Square, Timer, X, Zap } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Popover, PopoverTrigger } from "@/components/ui/popover";
import { useAuth } from "@/lib/auth";
import { useManagementPerspective } from "@/lib/perspective-context";
import { useCurrentTimer, useStartTimer, useTimerAction, useTimerHeartbeat, type TimerStatus } from "@/hooks/time-tracking/useTimer";
import { deriveTimerTotals, estimateServerNowMs, formatDurationSeconds, isTimerActive, sessionReceivedAtMs } from "@/lib/timer/timer-totals";
import { FinalizeSessionSideSheet } from "@/components/time-tracking/FinalizeSessionSideSheet";
import { cn } from "@/lib/utils";

const IDLE_MINUTES = 15;

export function HeaderTimer() {
  const t = useTranslations("timeTracking");
  const locale = useLocale();
  const { user, status: authStatus } = useAuth();
  const { effectiveReadOnly } = useManagementPerspective();
  const canControl = Boolean(user?.permissions?.includes("TimeTracking.ManageOwn")) && !effectiveReadOnly;
  const current = useCurrentTimer(canControl);
  const start = useStartTimer();
  const heartbeat = useTimerHeartbeat();
  const pauseAction = useTimerAction("pause");
  const breakAction = useTimerAction("break");
  const resumeAction = useTimerAction("resume");
  const finishAction = useTimerAction("finish");
  const [open, setOpen] = React.useState(false);
  const [finalizeOpen, setFinalizeOpen] = React.useState(false);
  const lastActivity = React.useRef(Date.now());
  const previousStatus = React.useRef<TimerStatus | null>(null);
  const [now, setNow] = React.useState(() => Date.now());
  const session = current.data ?? null;
  const sessionId = session?.id;
  const sessionStatus = session?.status;
  const sendHeartbeat = heartbeat.mutate;
  const shouldTick = Boolean(sessionId) && isTimerActive(sessionStatus);

  React.useEffect(() => {
    if (!shouldTick) return;
    setNow(Date.now());
    const interval = window.setInterval(() => setNow(Date.now()), 1_000);
    const onVisibility = () => {
      if (document.visibilityState === "hidden") lastActivity.current = Date.now() - IDLE_MINUTES * 60_000;
      else { lastActivity.current = Date.now(); setNow(Date.now()); }
    };
    document.addEventListener("visibilitychange", onVisibility);
    return () => { window.clearInterval(interval); document.removeEventListener("visibilitychange", onVisibility); };
  }, [shouldTick, sessionId, sessionStatus]);

  React.useEffect(() => {
    if (!sessionId || !["Running", "OnBreak", "Paused", "IdlePaused"].includes(sessionStatus ?? "")) return;
    const events = ["mousemove", "mousedown", "keydown", "touchstart"];
    const markActivity = () => { lastActivity.current = Date.now(); };
    events.forEach((event) => window.addEventListener(event, markActivity, { passive: true }));
    const interval = window.setInterval(() => {
      const idle = Date.now() - lastActivity.current >= IDLE_MINUTES * 60_000;
      if (sessionStatus === "Running" || sessionStatus === "IdlePaused") sendHeartbeat({ id: sessionId, isIdle: idle, lastActivityAt: new Date(lastActivity.current).toISOString() });
    }, 30_000);
    return () => { events.forEach((event) => window.removeEventListener(event, markActivity)); window.clearInterval(interval); };
  }, [sessionId, sessionStatus, sendHeartbeat]);

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
  const needsFinalization = sessionStatus === "AwaitingFinalization";
  const statusLabel = session ? t(`status.${session.status}` as never) : t("trackTime");
  const triggerValue = needsFinalization ? t("needsCompletion") : session && totals ? formatDurationSeconds(totals.workSeconds, locale) : t("trackTime");
  const statusTone = !session
    ? "border-primary/40 bg-primary/10 text-primary hover:bg-primary/15 hover:text-primary"
    : sessionStatus === "Running"
      ? "border-success/40 bg-success/10 text-success hover:bg-success/15 hover:text-success"
      : sessionStatus === "OnBreak" || sessionStatus === "IdlePaused"
        ? "border-warning/40 bg-warning/10 text-warning hover:bg-warning/15 hover:text-warning"
        : needsFinalization
          ? "border-destructive/40 bg-destructive/10 text-destructive hover:bg-destructive/15 hover:text-destructive"
          : "border-border bg-muted text-foreground hover:bg-muted/80";
  const StatusIcon = !session ? Timer : sessionStatus === "OnBreak" ? Coffee : needsFinalization ? Zap : Timer;
  const errors = [
    start.isError && (start.error instanceof Error ? start.error.message : t("startError")),
    finishAction.isError && (finishAction.error instanceof Error ? finishAction.error.message : t("finishError")),
    pauseAction.isError && (pauseAction.error instanceof Error ? pauseAction.error.message : t("pauseError")),
    breakAction.isError && (breakAction.error instanceof Error ? breakAction.error.message : t("breakError")),
    resumeAction.isError && (resumeAction.error instanceof Error ? resumeAction.error.message : t("resumeError")),
  ].filter((message): message is string => Boolean(message));

  const runAction = (mutation: { mutate: (_id: string) => void }) => {
    if (!session) return;
    mutation.mutate(session.id);
  };

  return (
    <>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="outline"
            size="sm"
            className={cn("h-8 max-w-40 gap-1.5 px-2 text-xs font-medium shadow-sm", statusTone)}
            aria-label={statusLabel}
            data-testid="header-timer-trigger"
          >
            <StatusIcon className="h-3.5 w-3.5 shrink-0" />
            <span className="hidden truncate tabular-nums sm:inline">{triggerValue}</span>
          </Button>
        </PopoverTrigger>
        {open && typeof document !== "undefined" ? createPortal(<div role="dialog" aria-label={statusLabel} className="fixed bottom-4 right-4 z-[100] max-h-[calc(100vh-2rem)] w-80 max-w-[calc(100vw-2rem)] overflow-y-auto rounded-md border border-border bg-popover p-3 text-popover-foreground shadow-lg outline-none sm:bottom-6 sm:right-6" data-testid="header-timer-popover">
          <div className="space-y-3">
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="text-sm font-semibold text-foreground">{needsFinalization ? t("needsCompletion") : session ? statusLabel : t("trackTime")}</p>
                <p className="text-xs text-muted-foreground">{t("headerTimerDescription")}</p>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                {session && totals ? <span className="font-mono text-lg tabular-nums text-foreground">{formatDurationSeconds(totals.workSeconds, locale)}</span> : <Timer className="h-5 w-5 text-muted-foreground" />}
                <Button variant="ghost" size="icon" className="h-7 w-7" aria-label={t("close")} data-testid="header-timer-close-button" onClick={() => setOpen(false)}>
                  <X className="h-4 w-4" />
                </Button>
              </div>
            </div>

            {session && totals ? (
              <>
                <div className="grid grid-cols-4 gap-1.5 text-center text-[10px]">
                  <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("workTime")}</span><strong className="font-mono text-foreground" data-testid="header-timer-work">{formatDurationSeconds(totals.workSeconds, locale)}</strong></div>
                  <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("breakTime")}</span><strong className="font-mono text-foreground" data-testid="header-timer-break-counter">{formatDurationSeconds(totals.breakSeconds, locale)}</strong></div>
                  <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("pauseTime")}</span><strong className="font-mono text-foreground" data-testid="header-timer-pause">{formatDurationSeconds(totals.pauseSeconds, locale)}</strong></div>
                  <div className="rounded border border-border/70 bg-muted/30 px-1.5 py-1"><span className="block text-muted-foreground">{t("totalSession")}</span><strong className="font-mono text-foreground" data-testid="header-timer-total">{formatDurationSeconds(totals.sessionSeconds, locale)}</strong></div>
                </div>
                {sessionStatus === "IdlePaused" ? <p className="text-xs text-warning">{t("idleMessage")}</p> : null}
                <div className="flex flex-wrap justify-end gap-1.5">
                  {sessionStatus === "Running" ? <>
                    <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" data-testid="header-timer-break-button" onClick={() => runAction(breakAction)} disabled={breakAction.isPending}><Coffee className="h-3.5 w-3.5" />{t("break")}</Button>
                    <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" data-testid="header-timer-pause-button" onClick={() => runAction(pauseAction)} disabled={pauseAction.isPending}><Pause className="h-3.5 w-3.5" />{t("pause")}</Button>
                  </> : null}
                  {sessionStatus === "OnBreak" || sessionStatus === "Paused" || sessionStatus === "IdlePaused" ? <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" data-testid="header-timer-resume-button" onClick={() => runAction(resumeAction)} disabled={resumeAction.isPending}><RotateCcw className="h-3.5 w-3.5" />{t("resume")}</Button> : null}
                  {needsFinalization ? <Button size="sm" className="h-7 gap-1 text-xs" data-testid="header-timer-complete-button" onClick={() => setFinalizeOpen(true)}><Zap className="h-3.5 w-3.5" />{t("complete")}</Button> : <Button size="sm" variant="destructive" className="h-7 gap-1 text-xs" data-testid="header-timer-finish-button" onClick={() => runAction(finishAction)} disabled={finishAction.isPending}><Square className="h-3.5 w-3.5" />{t("finish")}</Button>}
                </div>
              </>
            ) : (
              <Button className="w-full" data-testid="header-timer-start-button" onClick={() => start.mutate(undefined)} disabled={start.isPending}><Play className="me-2 h-4 w-4" />{t("startTimer")}</Button>
            )}

            {errors.length > 0 ? <div className="space-y-1 border-t border-border/60 pt-2">{errors.map((message) => <p key={message} role="alert" className="text-xs text-destructive">{message}</p>)}</div> : null}
          </div>
        </div>, document.body) : null}
      </Popover>
      <FinalizeSessionSideSheet open={finalizeOpen} onOpenChange={setFinalizeOpen} session={session} />
    </>
  );
}
