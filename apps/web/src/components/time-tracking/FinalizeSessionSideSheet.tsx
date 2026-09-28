"use client";

import * as React from "react";
import { ChevronDown } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations, useLocale } from "next-intl";
import { Button } from "@/components/ui/button";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@/components/ui/side-sheet";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { usePlanningActivities, usePlanningPlans } from "@/lib/api/planning";
import { useWorkItems } from "@/lib/api/work-items";
import { useProjectPeople } from "@/hooks/projects/useProjects";
import { useEligibleProjects, useFinalizeTimer, useTimerAction, type FinalizeTimerInput, type TimerSegmentDto, type TimerSessionDto } from "@/hooks/time-tracking/useTimer";
import { deriveTimerTotals, formatDurationSeconds, segmentDurationSeconds, westernNumberLocale, type TimerSegmentGroup, type TimerTotals } from "@/lib/timer/timer-totals";
import { cn, formatDateTime } from "@/lib/utils";

interface FinalizeSessionSideSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  session: TimerSessionDto | null;
}

interface SegmentSectionProps {
  testId: string;
  titleKey: "breakEvents" | "manualPauseEvents" | "automaticIdleEvents";
  group: TimerSegmentGroup;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  locale: string;
}

function SegmentSection({ testId, titleKey, group, open, onOpenChange, locale }: SegmentSectionProps) {
  const t = useTranslations("timeTracking");
  const numberLocale = westernNumberLocale(locale);
  return (
    <Collapsible open={open} onOpenChange={onOpenChange}>
      <CollapsibleTrigger asChild>
        <button
          type="button"
          data-testid={`${testId}-trigger`}
          aria-expanded={open}
          className="flex w-full items-center justify-between gap-2 rounded-md border border-border bg-muted/40 px-3 py-2 text-sm font-medium text-foreground hover:bg-muted/70"
        >
          <span className="min-w-0 truncate">{t(titleKey)} ({group.count})</span>
          <span className="flex shrink-0 items-center gap-2">
            <span className="font-mono text-xs tabular-nums">{formatDurationSeconds(group.totalSeconds, locale)}</span>
            <ChevronDown className={cn("h-4 w-4 text-muted-foreground transition-transform", open && "rotate-180")} />
          </span>
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent className="pt-2">
        <div data-testid={testId} className="space-y-2">
          {group.count === 0 ? (
            <p className="text-xs text-muted-foreground">{t("noEvents")}</p>
          ) : (
            group.segments.map((segment, index) => <SegmentEventRow key={segment.id} index={index} segment={segment} typeLabelKey={titleKey === "breakEvents" ? "break" : titleKey === "manualPauseEvents" ? "pause" : "idle"} locale={numberLocale} />)
          )}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

function SegmentEventRow({ index, segment, typeLabelKey, locale }: { index: number; segment: TimerSegmentDto; typeLabelKey: "break" | "pause" | "idle"; locale: string }) {
  const t = useTranslations("timeTracking");
  const duration = segmentDurationSeconds(segment, Date.now());
  return (
    <div className="rounded-md border border-border/70 bg-background px-3 py-2 text-xs">
      <div className="font-medium text-foreground">{t(typeLabelKey)} #{index + 1}</div>
      <div className="mt-1 grid grid-cols-3 gap-x-2 gap-y-0.5">
        <span className="text-muted-foreground">{t("start")}</span>
        <span className="col-span-2 text-foreground">{formatDateTime(segment.startedAt, locale)}</span>
        <span className="text-muted-foreground">{t("end")}</span>
        <span className="col-span-2 text-foreground">{segment.endedAt ? formatDateTime(segment.endedAt, locale) : "—"}</span>
        <span className="text-muted-foreground">{t("duration")}</span>
        <span className="col-span-2 font-mono text-foreground">{formatDurationSeconds(duration, locale)}</span>
      </div>
    </div>
  );
}

export function FinalizeSessionSideSheet({ open, onOpenChange, session }: FinalizeSessionSideSheetProps) {
  const t = useTranslations("timeTracking");
  const locale = useLocale();
  const numberLocale = westernNumberLocale(locale);
  const finalize = useFinalizeTimer();
  const discard = useTimerAction("cancel");
  const [projectId, setProjectId] = React.useState("");
  const [planId, setPlanId] = React.useState("");
  const [planActivityId, setPlanActivityId] = React.useState("");
  const [workItemId, setWorkItemId] = React.useState("");
  const [projectRoleId, setProjectRoleId] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [sectionsOpen, setSectionsOpen] = React.useState({ break: false, pause: false, idle: false });
  const [optionalOpen, setOptionalOpen] = React.useState(false);
  const autoSelectedProject = React.useRef(false);
  const autoSelectedPlan = React.useRef(false);
  const autoSelectedActivity = React.useRef(false);
  const autoSelectedWorkItem = React.useRef(false);
  const autoSelectedRole = React.useRef(false);

  const sessionId = session?.id ?? "";
  const sessionStatus = session?.status;
  const sheetOpen = open && sessionStatus === "AwaitingFinalization";
  const eligibleProjects = useEligibleProjects(sessionId, sessionStatus);
  const eligibleProjectOptions = eligibleProjects.data ?? [];

  const plans = usePlanningPlans(projectId, sheetOpen && Boolean(projectId));
  const planOptions = plans.data ?? [];
  const showPlanField = planOptions.length > 1;
  const activities = usePlanningActivities(projectId, planId, sheetOpen && Boolean(projectId) && Boolean(planId));
  const activityOptions = activities.data ?? [];
  const workItems = useWorkItems({ projectId, pageSize: 200 }, sheetOpen && Boolean(projectId));
  const workItemOptions = workItems.data?.items ?? [];
  const people = useProjectPeople(projectId, sheetOpen && Boolean(projectId));
  const resourceId = session?.resourceId ?? "";
  const roleOptions = React.useMemo(() => {
    const person = (people.data ?? []).find((item) => item.resourceId === resourceId);
    return person?.functionalRoles ?? [];
  }, [people.data, resourceId]);

  React.useEffect(() => {
    if (!sheetOpen) {
      autoSelectedProject.current = false;
      autoSelectedPlan.current = false;
      autoSelectedActivity.current = false;
      autoSelectedWorkItem.current = false;
      autoSelectedRole.current = false;
      return;
    }
    if (!projectId && eligibleProjectOptions.length === 1 && !autoSelectedProject.current) {
      autoSelectedProject.current = true;
      setProjectId(eligibleProjectOptions[0].id);
    }
  }, [sheetOpen, projectId, eligibleProjectOptions]);

  React.useEffect(() => {
    if (!sheetOpen || !projectId) return;
    if (!planId && planOptions.length === 1 && !autoSelectedPlan.current) {
      autoSelectedPlan.current = true;
      setPlanId(planOptions[0].id);
    }
    if (planId && !planOptions.some((plan) => plan.id === planId)) {
      autoSelectedPlan.current = false;
      setPlanId("");
      setPlanActivityId("");
    }
  }, [sheetOpen, projectId, planId, planOptions]);

  React.useEffect(() => {
    if (!sheetOpen || !projectId || !planId) return;
    if (!planActivityId && activityOptions.length === 1 && !autoSelectedActivity.current) {
      autoSelectedActivity.current = true;
      setPlanActivityId(activityOptions[0].id);
    }
    if (planActivityId && !activityOptions.some((activity) => activity.id === planActivityId)) {
      autoSelectedActivity.current = false;
      setPlanActivityId("");
    }
  }, [sheetOpen, projectId, planId, planActivityId, activityOptions]);

  React.useEffect(() => {
    if (!sheetOpen || !projectId) return;
    if (!workItemId && workItemOptions.length === 1 && !autoSelectedWorkItem.current) {
      autoSelectedWorkItem.current = true;
      setWorkItemId(workItemOptions[0].id);
    }
    if (workItemId && !workItemOptions.some((item) => item.id === workItemId)) {
      autoSelectedWorkItem.current = false;
      setWorkItemId("");
    }
  }, [sheetOpen, projectId, workItemId, workItemOptions]);

  React.useEffect(() => {
    if (!sheetOpen || !projectId) return;
    if (!projectRoleId && roleOptions.length === 1 && !autoSelectedRole.current) {
      autoSelectedRole.current = true;
      setProjectRoleId(roleOptions[0].id);
    }
    if (projectRoleId && !roleOptions.some((role) => role.id === projectRoleId)) {
      autoSelectedRole.current = false;
      setProjectRoleId("");
    }
  }, [sheetOpen, projectId, projectRoleId, roleOptions]);

  const serverNowMs = session ? new Date(session.serverNow).getTime() : Date.now();
  const totals: TimerTotals | null = session ? deriveTimerTotals(session, serverNowMs) : null;

  const resetForm = () => {
    setProjectId("");
    setPlanId("");
    setPlanActivityId("");
    setWorkItemId("");
    setProjectRoleId("");
    setDescription("");
    autoSelectedProject.current = false;
    autoSelectedPlan.current = false;
    autoSelectedActivity.current = false;
    autoSelectedWorkItem.current = false;
    autoSelectedRole.current = false;
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      finalize.reset();
      discard.reset();
    }
    onOpenChange(next);
  };

  const handleProjectChange = (value: string) => {
    autoSelectedProject.current = true;
    setProjectId(value);
    autoSelectedPlan.current = false;
    autoSelectedActivity.current = false;
    autoSelectedWorkItem.current = false;
    autoSelectedRole.current = false;
    setPlanId("");
    setPlanActivityId("");
    setWorkItemId("");
    setProjectRoleId("");
  };

  const handleFinalize = () => {
    if (!session || !projectId) return;
    const input: FinalizeTimerInput = {
      projectId,
      ...(planActivityId ? { planActivityId } : {}),
      ...(workItemId ? { workItemId } : {}),
      ...(projectRoleId ? { projectRoleId } : {}),
      ...(description ? { description } : {}),
    };
    finalize.mutate(
      { id: session.id, input },
      {
        onSuccess: () => {
          resetForm();
          onOpenChange(false);
        },
      }
    );
  };

  const handleDiscard = () => {
    if (!session) return;
    if (!window.confirm(t("discardConfirm"))) return;
    discard.mutate(session.id, {
      onSuccess: () => {
        resetForm();
        onOpenChange(false);
      },
    });
  };

  const canFinalize = Boolean(projectId) && !finalize.isPending;
  const noEligibleProjects = !eligibleProjects.isFetching && eligibleProjectOptions.length === 0;
  const showActivityField = Boolean(planId) && activityOptions.length > 0;
  const showWorkItemField = Boolean(projectId) && workItemOptions.length > 0;
  const showRoleField = Boolean(projectId) && roleOptions.length > 0;

  const descriptionText = session
    ? `${formatDateTime(session.startedAt, numberLocale)} → ${formatDateTime(session.endedAt ?? session.serverNow, numberLocale)} · ${t(`status.${session.status}` as never)}`
    : undefined;

  return (
    <SideSheet
      open={sheetOpen}
      onOpenChange={handleOpenChange}
      title={t("completeTimeSession")}
      description={descriptionText}
      size="xl"
      closeLabel={t("close")}
    >
      <SideSheetBody className="space-y-5" data-testid="finalize-side-sheet">
        {totals && (
          <section data-testid="session-summary" className="space-y-2">
            <h3 className="text-sm font-semibold text-foreground">{t("sessionSummary")}</h3>
            <div className="rounded-lg border border-border/70 bg-muted/30 p-3 text-sm">
              <div className="grid grid-cols-2 gap-x-4 gap-y-1.5">
                <span className="text-muted-foreground">{t("workTime")}</span>
                <strong data-testid="summary-work" className="text-end font-mono text-foreground">{formatDurationSeconds(totals.workSeconds, locale)}</strong>
                <span className="text-muted-foreground">{t("breakTime")}</span>
                <strong data-testid="summary-break" className="text-end font-mono text-foreground">{formatDurationSeconds(totals.breakSeconds, locale)}</strong>
                <span className="text-muted-foreground">{t("pauseTime")}</span>
                <strong data-testid="summary-pause" className="text-end font-mono text-foreground">{formatDurationSeconds(totals.pauseSeconds, locale)}</strong>
                <span className="text-muted-foreground">{t("idleTime")}</span>
                <strong data-testid="summary-idle" className="text-end font-mono text-foreground">{formatDurationSeconds(totals.idleSeconds, locale)}</strong>
                <span className="text-muted-foreground">{t("totalSession")}</span>
                <strong data-testid="summary-total" className="text-end font-mono text-foreground">{formatDurationSeconds(totals.sessionSeconds, locale)}</strong>
              </div>
              <div className="mt-3 grid grid-cols-3 gap-2 border-t border-border/60 pt-2 text-center text-xs">
                <div><div className="text-muted-foreground">{t("breaksCount")}</div><strong data-testid="summary-break-count" className="font-mono">{totals.breakCount}</strong></div>
                <div><div className="text-muted-foreground">{t("manualPausesCount")}</div><strong data-testid="summary-pause-count" className="font-mono">{totals.pauseCount}</strong></div>
                <div><div className="text-muted-foreground">{t("idleEventsCount")}</div><strong data-testid="summary-idle-count" className="font-mono">{totals.idleCount}</strong></div>
              </div>
            </div>
          </section>
        )}

        {totals && (
          <section className="space-y-2">
            <SegmentSection testId="break-events" titleKey="breakEvents" group={totals.break} open={sectionsOpen.break} onOpenChange={(value) => setSectionsOpen((current) => ({ ...current, break: value }))} locale={locale} />
            <SegmentSection testId="pause-events" titleKey="manualPauseEvents" group={totals.pause} open={sectionsOpen.pause} onOpenChange={(value) => setSectionsOpen((current) => ({ ...current, pause: value }))} locale={locale} />
            <SegmentSection testId="idle-events" titleKey="automaticIdleEvents" group={totals.idle} open={sectionsOpen.idle} onOpenChange={(value) => setSectionsOpen((current) => ({ ...current, idle: value }))} locale={locale} />
          </section>
        )}

        <section data-testid="finalize-attribution" className="space-y-3">
          <h3 className="text-sm font-semibold text-foreground">{t("attribution")}</h3>
          <label className="block space-y-1.5 text-sm">
            <span className="font-medium">{t("project")}</span>
            <select data-testid="finalize-project-select" className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={projectId} onChange={(event) => handleProjectChange(event.target.value)}>
              <option value="">{t("selectProject")}</option>
              {eligibleProjectOptions.map((project) => <option key={project.id} value={project.id}>{project.code} · {project.name}</option>)}
            </select>
            {noEligibleProjects && <p className="text-xs text-destructive">{t("noEligibleProjects")}</p>}
            {eligibleProjects.isError && <p className="text-xs text-destructive">{t("eligibleProjectsError")}</p>}
          </label>
          {showPlanField && (
            <label className="block space-y-1.5 text-sm">
              <span className="font-medium">{t("planField")}</span>
              <select data-testid="finalize-plan-select" className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={planId} onChange={(event) => { autoSelectedPlan.current = true; setPlanId(event.target.value); autoSelectedActivity.current = false; setPlanActivityId(""); }}>
                <option value="">{t("selectPlan")}</option>
                {planOptions.map((plan) => <option key={plan.id} value={plan.id}>{plan.name}</option>)}
              </select>
            </label>
          )}
        </section>

        <Collapsible open={optionalOpen} onOpenChange={setOptionalOpen}>
          <CollapsibleTrigger asChild>
            <button
              type="button"
              data-testid="optional-details-trigger"
              aria-expanded={optionalOpen}
              className="flex w-full items-center justify-between gap-2 rounded-md border border-border bg-muted/40 px-3 py-2 text-sm font-medium text-foreground hover:bg-muted/70"
            >
              <span>{t("optionalDetails")}</span>
              <ChevronDown className={cn("h-4 w-4 text-muted-foreground transition-transform", optionalOpen && "rotate-180")} />
            </button>
          </CollapsibleTrigger>
          <CollapsibleContent className="pt-3">
            <div data-testid="optional-details" className="space-y-3">
              {showActivityField && (
                <label className="block space-y-1.5 text-sm">
                  <span className="font-medium">{t("planActivityField")}</span>
                  <select data-testid="finalize-activity-select" className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={planActivityId} onChange={(event) => { autoSelectedActivity.current = true; setPlanActivityId(event.target.value); }}>
                    <option value="">{t("selectPlanActivity")}</option>
                    {activityOptions.map((activity) => <option key={activity.id} value={activity.id}>{activity.code} — {activity.name}</option>)}
                  </select>
                </label>
              )}
              {showWorkItemField && (
                <label className="block space-y-1.5 text-sm">
                  <span className="font-medium">{t("workItemField")}</span>
                  <select data-testid="finalize-work-item-select" className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={workItemId} onChange={(event) => { autoSelectedWorkItem.current = true; setWorkItemId(event.target.value); }}>
                    <option value="">{t("selectWorkItem")}</option>
                    {workItemOptions.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
                  </select>
                </label>
              )}
              {showRoleField && (
                <label className="block space-y-1.5 text-sm">
                  <span className="font-medium">{t("roleField")}</span>
                  <select data-testid="finalize-role-select" className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={projectRoleId} onChange={(event) => { autoSelectedRole.current = true; setProjectRoleId(event.target.value); }}>
                    <option value="">{t("selectRole")}</option>
                    {roleOptions.map((role) => <option key={role.id} value={role.id}>{locale === "ar" ? role.nameAr : role.nameEn}</option>)}
                  </select>
                </label>
              )}
              <label className="block space-y-1.5 text-sm">
                <span className="font-medium">{t("descriptionField")}</span>
                <textarea data-testid="finalize-description" className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={description} onChange={(event) => setDescription(event.target.value)} placeholder={t("descriptionPlaceholder")} />
              </label>
            </div>
          </CollapsibleContent>
        </Collapsible>

        {finalize.isError && <p role="alert" className="text-sm text-destructive">{finalize.error instanceof Error ? finalize.error.message : t("finalizeError")}</p>}
        {discard.isError && <p role="alert" className="text-sm text-destructive">{discard.error instanceof Error ? discard.error.message : t("cancelError")}</p>}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" data-testid="finalize-discard" onClick={handleDiscard} disabled={discard.isPending || !session}>{t("discardTimer")}</Button>
        <Button type="button" data-testid="finalize-submit" onClick={handleFinalize} disabled={!canFinalize || !session}>{t("finalizeAction")}</Button>
      </SideSheetFooter>
    </SideSheet>
  );
}
