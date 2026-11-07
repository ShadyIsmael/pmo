import * as React from "react";
import { Check, ChevronLeft, ChevronRight, Clock3, Mail, Search, Send, Share2, Users, X } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Checkbox } from "@components/ui/Checkbox";
import { Input } from "@components/ui/Input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useI18n } from "@i18n/messages";
import {
  useMeetingActionItems,
  useMeetingSharingHistory,
  useMeetingSharingMutations,
  useMeetingSharingRecipients,
  type MeetingDetailDto,
  type MeetingMinutesDto,
  type MeetingSharingRecipientDto,
} from "@api/meetingsFull";
import { RichTextContent } from "@components/ui/rich-text-editor";

type ShareMode = "email" | "completion";
type ShareStep = 1 | 2 | 3;
type RecipientGroup = "present" | "late" | "tenant";

export function MeetingSharingDialog({ meeting, minutes, canManage }: { meeting: MeetingDetailDto; minutes: MeetingMinutesDto | null | undefined; canManage: boolean }) {
  const { t, formatDate, locale } = useI18n();
  const [open, setOpen] = React.useState(false);
  const [mode, setMode] = React.useState<ShareMode>("email");
  const [step, setStep] = React.useState<ShareStep>(1);
  const [recipientGroup, setRecipientGroup] = React.useState<RecipientGroup>("present");
  const [search, setSearch] = React.useState("");
  const [debouncedSearch, setDebouncedSearch] = React.useState("");
  const [selectedIds, setSelectedIds] = React.useState<string[]>([]);
  const [selectedRecipientById, setSelectedRecipientById] = React.useState<Record<string, MeetingSharingRecipientDto>>({});
  const [error, setError] = React.useState<string | null>(null);
  const [message, setMessage] = React.useState<string | null>(null);
  const [resultCounts, setResultCounts] = React.useState<{ queued: number; existing: number } | null>(null);
  const requestRef = React.useRef<{ fingerprint: string; id: string } | null>(null);
  const dialogRef = React.useRef<HTMLDivElement>(null);
  const openerRef = React.useRef<HTMLElement | null>(null);
  const busyRef = React.useRef(false);
  const recipients = useMeetingSharingRecipients(meeting.id, debouncedSearch, open);
  const history = useMeetingSharingHistory(meeting.id, canManage);
  const actions = useMeetingActionItems(meeting.id, open && mode === "email" && step >= 2);
  const mutations = useMeetingSharingMutations(meeting.id);
  const busy = mutations.shareMinutesByEmail.isPending || mutations.notifyMeetingCompletion.isPending;
  const handleSheetOpenChange = React.useCallback((next: boolean) => {
    if (!busy) setOpen(next);
  }, [busy]);
  const hasApplicationLink = Boolean(recipients.data?.applicationLink);
  const isFinal = minutes?.state === "Final";
  const attendeeOptions = recipients.data?.attendees ?? [];
  const tenantOptions = recipients.data?.tenantUsers ?? [];
  const searchTerm = debouncedSearch.toLocaleLowerCase(locale);
  const matchesSearch = (recipient: MeetingSharingRecipientDto) => !searchTerm
    || recipient.displayName.toLocaleLowerCase(locale).includes(searchTerm)
    || recipient.email.toLocaleLowerCase(locale).includes(searchTerm);
  const presentOptions = attendeeOptions.filter((item) => item.attendanceStatus?.toLowerCase() === "present" && matchesSearch(item));
  const lateOptions = attendeeOptions.filter((item) => item.attendanceStatus?.toLowerCase() === "late" && matchesSearch(item));
  const tenantUserOptions = tenantOptions.filter(matchesSearch);
  const visibleRecipients = mode === "completion" || recipientGroup === "tenant"
    ? tenantUserOptions
    : recipientGroup === "late" ? lateOptions : presentOptions;
  const selectedCount = selectedIds.length;
  const selectedEligible = selectedIds.map((id) => selectedRecipientById[id]).filter((item): item is MeetingSharingRecipientDto => Boolean(item));
  const allSelectedAreEligible = selectedEligible.length === selectedCount && selectedEligible.every((item) => mode === "email" ? item.canReceiveMinutesEmail : item.canReceiveCompletionNotification);
  const canSend = selectedCount > 0 && selectedCount <= 500 && allSelectedAreEligible && hasApplicationLink
    && (mode !== "email" || (isFinal && !actions.isLoading && !actions.isError));
  const selectionProblem = selectedCount === 0
    ? t("meetings.sharing.selectRecipient")
    : selectedCount > 500 || !allSelectedAreEligible
      ? t("meetings.sharing.recipientLimitOrEligibility")
      : null;
  const visibleEligible = visibleRecipients.filter((recipient) => mode === "email" ? recipient.canReceiveMinutesEmail : recipient.canReceiveCompletionNotification);
  const allVisibleSelected = visibleEligible.length > 0 && visibleEligible.every((item) => selectedIds.includes(item.userId));
  const unavailableVisibleCount = visibleRecipients.length - visibleEligible.length;
  busyRef.current = busy;

  React.useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  React.useEffect(() => {
    if (!open) return;
    openerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusFrame = window.requestAnimationFrame(() => {
      const first = dialogRef.current?.querySelector<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
      );
      first?.focus();
    });
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !busyRef.current) {
        setOpen(false);
        return;
      }
      if (event.key !== "Tab") return;
      const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
      );
      if (!focusable?.length) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener("keydown", handleKeyDown);
      openerRef.current?.focus();
    };
  }, [open]);

  React.useEffect(() => {
    setOpen(false);
    setMode("email");
    setRecipientGroup("present");
    setSelectedIds([]);
    setSelectedRecipientById({});
    setSearch("");
    setDebouncedSearch("");
    setStep(1);
    setError(null);
    setMessage(null);
    setResultCounts(null);
    requestRef.current = null;
  }, [meeting.id]);

  function changeMode(next: ShareMode) {
    setMode(next);
    setStep(1);
    setRecipientGroup(next === "email" ? "present" : "tenant");
    setSelectedIds([]);
    setSelectedRecipientById({});
    setError(null);
    setMessage(null);
    setResultCounts(null);
    requestRef.current = null;
  }

  function toggleRecipient(recipient: MeetingSharingRecipientDto, selected: boolean) {
    const userId = recipient.userId;
    setSelectedIds((current) => selected
      ? current.includes(userId) ? current : [...current, userId]
      : current.filter((id) => id !== userId));
    setSelectedRecipientById((current) => {
      if (!selected) {
        const next = { ...current };
        delete next[userId];
        return next;
      }
      return { ...current, [userId]: recipient };
    });
    setError(null);
    setMessage(null);
    setResultCounts(null);
  }

  function ensureRequestId(fingerprint: string) {
    if (requestRef.current?.fingerprint !== fingerprint) {
      requestRef.current = { fingerprint, id: globalThis.crypto.randomUUID() };
    }
    return requestRef.current.id;
  }

  function continueToPreview() {
    setError(null);
    setMessage(null);
    setStep(2);
  }

  function continueToReview() {
    setError(null);
    setStep(3);
  }

  function toggleAllVisible() {
    const ids = visibleEligible.map((item) => item.userId);
    if (allVisibleSelected) {
      setSelectedIds((current) => current.filter((id) => !ids.includes(id)));
      setSelectedRecipientById((current) => {
        const next = { ...current };
        ids.forEach((id) => delete next[id]);
        return next;
      });
    } else {
      setSelectedIds((current) => Array.from(new Set([...current, ...ids])));
      setSelectedRecipientById((current) => ({ ...current, ...Object.fromEntries(visibleEligible.map((item) => [item.userId, item])) }));
    }
    setError(null);
    setMessage(null);
    setResultCounts(null);
  }

  async function submitShare() {
    if (!canManage || step !== 3) return;
    if (!hasApplicationLink) {
      setError(t("meetings.sharing.applicationUrlNotConfigured"));
      return;
    }
    if (!canSend) return;
    setError(null);
    setMessage(null);
    const recipientsKey = [...selectedIds].sort().join(",");
    const fingerprint = `${mode}|${recipientsKey}|${minutes?.id ?? ""}|${minutes?.rowVersion ?? ""}|${locale}`;
    const requestId = ensureRequestId(fingerprint);
    try {
      const result = mode === "email"
        ? await mutations.shareMinutesByEmail.mutateAsync({
          recipientUserIds: selectedIds,
          minutesId: minutes!.id,
          minutesRowVersion: minutes!.rowVersion,
          requestId,
          locale,
        })
        : await mutations.notifyMeetingCompletion.mutateAsync({ recipientUserIds: selectedIds, requestId, locale });
      setResultCounts({ queued: result.queuedCount, existing: result.alreadyProcessedCount });
      setMessage(mode === "email" ? t("meetings.sharing.emailQueued") : t("meetings.sharing.completionSent"));
      setSelectedIds([]);
      setSelectedRecipientById({});
      setStep(1);
      requestRef.current = null;
      await history.refetch();
    } catch {
      setError(t("meetings.sharing.sendError"));
    }
  }

  return <div className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border bg-card p-5 shadow-sm">
      <div className="min-w-0">
        <p className="flex items-center gap-2 text-base font-semibold"><Share2 className="h-4 w-4 text-primary" />{t("meetings.sharing.title")}</p>
        <p className="mt-1 max-w-3xl text-sm text-muted-foreground">{t("meetings.sharing.description")}</p>
      </div>
      {canManage ? <Button type="button" onClick={() => { setOpen(true); setError(null); }}><Share2 className="me-2 h-4 w-4" />{t("meetings.sharing.open")}</Button> : null}
    </div>

    {canManage ? <Card>
      <CardHeader className="pb-3"><CardTitle className="flex items-center gap-2 text-base"><Clock3 className="h-4 w-4 text-muted-foreground" />{t("meetings.sharing.history")}</CardTitle></CardHeader>
      <CardContent className="space-y-3 pt-0">
        {history.isLoading ? <div className="h-20 animate-pulse rounded-md bg-muted/50" aria-label={t("common.loading")} />
          : history.isError ? <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/30 p-4 text-sm"><p role="alert" className="text-destructive">{t("meetings.sharing.historyUnavailable")}</p><Button type="button" size="sm" variant="outline" onClick={() => void history.refetch()}>{t("common.retry")}</Button></div>
            : history.data?.length ? history.data.slice(0, 20).map((entry) => <details key={entry.id} className="group rounded-lg border bg-background">
              <summary className="flex cursor-pointer list-none flex-wrap items-center justify-between gap-3 p-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-1">
                  <span className="font-medium">{t(`meetings.sharing.types.${entry.type}`)}</span>
                  <span className="text-sm text-muted-foreground">{t("meetings.sharing.recipientCount", { count: entry.recipientCount })}</span>
                  <span className="text-sm text-muted-foreground">{t("meetings.sharing.sentBy", { name: entry.senderName || t("meetings.sharing.unavailableUser") })}</span>
                </div>
                <div className="flex shrink-0 items-center gap-3">
                  <time dateTime={entry.sentAt} className="text-sm text-muted-foreground">{formatDate(entry.sentAt, { dateStyle: "medium", timeStyle: "short" })}</time>
                  <Badge variant={historyStatusVariant(entry.status)}>{t(`meetings.sharing.status.${entry.status.toLowerCase()}`)}</Badge>
                </div>
              </summary>
              <div className="space-y-3 border-t bg-muted/20 p-4">
                {entry.recipients.map((recipient) => <section key={recipient.userId} className="rounded-md border bg-background p-3">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0"><p className="font-medium">{recipient.recipientName || t("meetings.sharing.unavailableUser")}</p><p className="break-all text-sm text-muted-foreground">{recipient.recipientEmail || t("meetings.sharing.noEmailAddress")}</p></div>
                    <Badge variant={historyStatusVariant(recipient.status)}>{t(`meetings.sharing.status.${recipient.status.toLowerCase()}`)}</Badge>
                  </div>
                  <ul className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                    {recipient.deliveries.map((delivery) => <li key={`${recipient.userId}-${delivery.channel}`} className="flex items-center justify-between gap-2 rounded-md bg-muted/40 px-3 py-2 text-sm"><span>{channelLabel(delivery.channel, t)}</span><Badge variant={historyStatusVariant(delivery.status)}>{t(`meetings.sharing.status.${delivery.status.toLowerCase()}`)}</Badge></li>)}
                  </ul>
                </section>)}
              </div>
            </details>)
              : <p className="rounded-md border border-dashed px-4 py-5 text-center text-sm text-muted-foreground">{t("meetings.sharing.noHistory")}</p>}
      </CardContent>
    </Card> : null}

    <SideSheet open={open} onOpenChange={handleSheetOpenChange} title={<span className="flex items-center gap-2"><Share2 className="h-5 w-5 shrink-0 text-primary" />{t("meetings.sharing.title")}</span>} description={t("meetings.sharing.description")} closeLabel={t("common.close")} ariaLabel={t("meetings.sharing.title")} size="xl" panelRef={dialogRef}>
        <nav aria-label={t("meetings.sharing.stepsLabel")} className="shrink-0 border-b bg-muted/20 px-3 py-3 sm:px-6 sm:py-4">
          <ol className="grid grid-cols-3 gap-2">
            {([
              { value: 1 as const, label: t("meetings.sharing.steps.selectRecipients") },
              { value: 2 as const, label: t("meetings.sharing.steps.previewMessage") },
              { value: 3 as const, label: t("meetings.sharing.steps.reviewSend") },
            ]).map((item) => <li key={item.value}>
              <button type="button" disabled={busy} onClick={() => { setStep(item.value); setError(null); }} aria-current={step === item.value ? "step" : undefined} className={`flex w-full items-center gap-2 rounded-md px-2 py-2 text-start text-xs transition-colors sm:gap-3 sm:px-3 sm:text-sm ${step === item.value ? "bg-primary/10 text-primary" : item.value < step ? "text-foreground hover:bg-muted" : "text-muted-foreground"}`}>
                <span className={`grid h-7 w-7 shrink-0 place-items-center rounded-full border text-xs font-semibold ${step === item.value ? "border-primary bg-primary text-primary-foreground" : item.value < step ? "border-primary/50 text-primary" : "border-muted-foreground/30"}`}>{item.value < step ? <Check className="h-4 w-4" /> : item.value}</span>
                <span className="min-w-0"><span className="block text-[10px] uppercase tracking-wide opacity-70">{t("meetings.sharing.stepNumber", { current: item.value, total: 3 })}</span><span className="block truncate font-medium">{item.label}</span></span>
              </button>
            </li>)}
          </ol>
        </nav>

        <SideSheetBody className="px-4 py-4 sm:px-6 sm:py-6">
          {step === 1 ? <div className="space-y-5">
            <section aria-label={t("meetings.sharing.operations")} className="grid gap-3 md:grid-cols-2">
              <button type="button" aria-pressed={mode === "email"} className={`flex min-h-24 items-start gap-3 rounded-xl border p-4 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${mode === "email" ? "border-primary bg-primary/5" : "hover:bg-muted/50"}`} onClick={() => changeMode("email")}>
                <span className={`grid h-10 w-10 shrink-0 place-items-center rounded-lg ${mode === "email" ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}><Mail className="h-5 w-5" /></span><span className="min-w-0"><span className="block font-semibold">{t("meetings.sharing.emailOption")}</span><span className="mt-1 block text-sm text-muted-foreground">{t("meetings.sharing.emailOptionDescription")}</span></span>
              </button>
              <button type="button" aria-pressed={mode === "completion"} className={`flex min-h-24 items-start gap-3 rounded-xl border p-4 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${mode === "completion" ? "border-primary bg-primary/5" : "hover:bg-muted/50"}`} onClick={() => changeMode("completion")}>
                <span className={`grid h-10 w-10 shrink-0 place-items-center rounded-lg ${mode === "completion" ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}><Users className="h-5 w-5" /></span><span className="min-w-0"><span className="block font-semibold">{t("meetings.sharing.completionOption")}</span><span className="mt-1 block text-sm text-muted-foreground">{t("meetings.sharing.completionOptionDescription")}</span></span>
              </button>
            </section>

            {mode === "email" && !isFinal ? <p role="status" className="rounded-lg border border-warning/40 bg-warning/5 px-4 py-3 text-sm text-warning">{t("meetings.sharing.finalMinutesRequired")}</p> : null}

            <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1.55fr)_minmax(18rem,.75fr)]">
              <section className="min-w-0 rounded-xl border bg-card p-4 sm:p-5">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div><h3 className="text-base font-semibold">{mode === "email" ? t("meetings.sharing.recipientsForMinutes") : t("meetings.sharing.tenantRecipients")}</h3><p className="mt-1 max-w-2xl text-sm text-muted-foreground">{mode === "email" ? t("meetings.sharing.tenantEmailRule") : t("meetings.sharing.tenantNotificationRule")}</p></div>
                  <Badge variant="secondary">{t("meetings.sharing.selectedCount", { count: selectedCount })}</Badge>
                </div>
                {mode === "email" ? <div className="mt-4 flex flex-wrap gap-2" role="group" aria-label={t("meetings.sharing.recipientGroups")}>
                  {([
                    { value: "present" as const, label: t("meetings.sharing.presentGroup"), count: presentOptions.length },
                    { value: "late" as const, label: t("meetings.sharing.lateGroup"), count: lateOptions.length },
                    { value: "tenant" as const, label: t("meetings.sharing.allTenantUsers"), count: tenantUserOptions.length },
                  ]).map((item) => <button key={item.value} type="button" aria-pressed={recipientGroup === item.value} onClick={() => setRecipientGroup(item.value)} className={`rounded-full border px-3 py-1.5 text-sm transition-colors ${recipientGroup === item.value ? "border-primary bg-primary/10 font-medium text-primary" : "text-muted-foreground hover:bg-muted"}`}>{item.label} <span className="ms-1 opacity-75">{item.count}</span></button>)}
                </div> : null}
                <div className="relative mt-4"><Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input aria-label={t("meetings.sharing.searchRecipients")} value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("meetings.sharing.searchRecipients")} className="h-11 ps-9" /></div>
                {recipientGroup === "tenant" || mode === "completion" ? <p className="mt-2 text-xs text-muted-foreground">{t("meetings.sharing.serverSearchLimit")}</p> : <p className="mt-2 text-xs text-muted-foreground">{t("meetings.sharing.attendeeRule")}</p>}
                <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-y py-3">
                  <p className="text-sm font-medium">{t("meetings.sharing.visibleRecipients", { count: visibleRecipients.length })}</p>
                  <div className="flex flex-wrap gap-2"><Button type="button" size="sm" variant="outline" onClick={toggleAllVisible} disabled={!visibleEligible.length}>{allVisibleSelected ? t("meetings.sharing.clearVisible") : t("meetings.sharing.selectAllVisible")}</Button><Button type="button" size="sm" variant="ghost" onClick={() => { setSelectedIds([]); setSelectedRecipientById({}); setError(null); }} disabled={!selectedCount}>{t("meetings.sharing.clearSelection")}</Button></div>
                </div>
                <div className="mt-2 h-80 max-h-[42dvh] min-h-52 overflow-y-auto overscroll-contain">
                  {recipients.isError ? <div className="grid h-full place-items-center rounded-md border border-dashed p-5 text-center"><div><p role="alert" className="text-sm text-destructive">{t("meetings.sharing.recipientsLoadError")}</p><Button type="button" size="sm" variant="outline" className="mt-3" onClick={() => void recipients.refetch()}>{t("common.retry")}</Button></div></div>
                    : recipients.isLoading || recipients.isFetching || debouncedSearch !== search.trim() ? <div className="space-y-2 p-1" role="status" aria-live="polite"><span className="sr-only">{t("common.loading")}</span>{Array.from({ length: 5 }, (_, index) => <div key={index} className="h-14 animate-pulse rounded-md bg-muted/50" />)}</div>
                      : visibleRecipients.length === 0 ? <div className="grid h-full place-items-center rounded-md border border-dashed px-4 text-center text-sm text-muted-foreground">{search.trim() ? t("meetings.sharing.noSearchMatches") : mode === "email" && recipientGroup !== "tenant" ? t("meetings.sharing.noEligibleAttendees") : t("meetings.sharing.noTenantUsers")}</div>
                        : <div role="group" aria-label={t("meetings.sharing.visibleRecipients", { count: visibleRecipients.length })} className="divide-y">{visibleRecipients.map((recipient) => <RecipientOption key={recipient.userId} recipient={recipient} selected={selectedIds.includes(recipient.userId)} mode={mode} disabled={!isFinal && mode === "email" || (mode === "email" ? !recipient.canReceiveMinutesEmail : !recipient.canReceiveCompletionNotification)} disabledReason={!isFinal && mode === "email" ? t("meetings.sharing.finalMinutesRequired") : recipientUnavailableReason(recipient, mode, t)} onToggle={toggleRecipient} />)}</div>}
                </div>
              </section>

              <aside className="rounded-xl border bg-muted/20 p-4 sm:p-5 xl:sticky xl:top-0">
                <div className="flex items-center justify-between gap-3"><h3 className="text-base font-semibold">{t("meetings.sharing.selectedRecipients")}</h3><Badge variant="secondary">{selectedCount}</Badge></div>
                <p className="mt-1 text-sm text-muted-foreground">{mode === "email" ? t("meetings.sharing.explicitEmailOnly") : t("meetings.sharing.explicitNotificationOnly")}</p>
                {selectedEligible.length ? <ul className="mt-4 max-h-72 space-y-2 overflow-y-auto">{selectedEligible.map((recipient) => <li key={recipient.userId} className="flex min-w-0 items-center gap-3 rounded-lg border bg-background p-3">
                  <span aria-hidden="true" className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">{recipientInitials(recipient.displayName)}</span><span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{recipient.displayName}</span><span className="block truncate text-xs text-muted-foreground">{recipient.email || t("meetings.sharing.noEmailAddress")}</span></span>
                  <button type="button" onClick={() => toggleRecipient(recipient, false)} aria-label={t("meetings.sharing.removeRecipient", { name: recipient.displayName })} className="rounded-md p-2 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"><X className="h-4 w-4" /></button>
                </li>)}</ul> : <p className="mt-4 rounded-lg border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">{t("meetings.sharing.noSelection")}</p>}
                <p className="mt-4 text-xs text-muted-foreground">{t("meetings.sharing.explicitNoExternal")}</p>
              </aside>
            </div>
          </div> : step === 2 ? <SharePreview mode={mode} meeting={meeting} minutes={minutes} actions={actions.data ?? []} actionsLoading={actions.isLoading} actionsError={actions.isError} selected={selectedEligible} applicationLink={recipients.data?.applicationLink ?? ""} t={t} formatDate={formatDate} />
            : <ShareReview mode={mode} meeting={meeting} selected={selectedEligible} selectionProblem={selectionProblem} isFinal={isFinal} actionsLoading={actions.isLoading} actionsError={actions.isError} applicationLink={recipients.data?.applicationLink ?? ""} unavailableCount={unavailableVisibleCount} formatDate={formatDate} t={t} />}

          {error ? <p role="alert" className="mt-5 rounded-md border border-destructive/25 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p> : null}
          {message ? <div role="status" className="mt-5 rounded-md border border-success/25 bg-success/5 px-3 py-2 text-sm text-success"><p>{message}</p>{resultCounts ? <p className="mt-1 text-xs">{t("meetings.sharing.resultCounts", { queued: resultCounts.queued, existing: resultCounts.existing })}</p> : null}</div> : null}
        </SideSheetBody>

        <SideSheetFooter className="flex-row items-center justify-between gap-3 px-4 py-3 sm:justify-between sm:px-6 sm:py-4">
          <div>{step > 1 ? <Button type="button" variant="outline" onClick={() => { setStep((current) => (current - 1) as ShareStep); setError(null); }} disabled={busy}><ChevronLeft className="me-1 h-4 w-4 rtl:rotate-180" />{t("meetings.sharing.back")}</Button> : <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={busy}>{t("common.close")}</Button>}</div>
          {step === 1 ? <Button type="button" onClick={continueToPreview} disabled={busy}>{t("meetings.sharing.nextToPreview")}<ChevronRight className="ms-1 h-4 w-4 rtl:rotate-180" /></Button>
            : step === 2 ? <Button type="button" onClick={continueToReview} disabled={busy}>{t("meetings.sharing.nextToReview")}<ChevronRight className="ms-1 h-4 w-4 rtl:rotate-180" /></Button>
              : <Button type="button" onClick={() => void submitShare()} disabled={busy || !canSend}>{busy ? t("meetings.sharing.sending") : mode === "email" ? <><Send className="me-2 h-4 w-4" />{t("meetings.sharing.confirmEmail")}</> : <><Send className="me-2 h-4 w-4" />{t("meetings.sharing.confirmNotification")}</>}</Button>}
        </SideSheetFooter>
    </SideSheet>
  </div>;
}

function RecipientOption({ recipient, selected, mode, disabled, disabledReason, onToggle }: { recipient: MeetingSharingRecipientDto; selected: boolean; mode: ShareMode; disabled: boolean; disabledReason: string; onToggle: (recipient: MeetingSharingRecipientDto, selected: boolean) => void }) {
  const { t } = useI18n();
  const attendanceLabel = recipient.attendanceStatus ? t(`meetings.attendance.${recipient.attendanceStatus.toLowerCase()}`) : null;
  const contextLabel = attendanceLabel ?? t("meetings.sharing.activeTenantUser");
  return <div className={`flex min-w-0 items-center gap-3 px-2 py-2.5 transition-colors ${disabled ? "opacity-75" : "hover:bg-muted/50"}`}>
    <Checkbox checked={selected} disabled={disabled} onCheckedChange={() => onToggle(recipient, !selected)} aria-label={`${recipient.displayName}, ${recipient.email || t("meetings.sharing.noEmailAddress")}`} />
    <span aria-hidden="true" className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">{recipientInitials(recipient.displayName)}</span>
    <span className="min-w-0 flex-1"><span className="flex flex-wrap items-center gap-2"><span className="truncate text-sm font-medium">{recipient.displayName}</span><Badge variant="outline" className="text-[10px]">{contextLabel}</Badge></span><span className="mt-0.5 block truncate text-xs text-muted-foreground">{recipient.email || t("meetings.sharing.noEmailAddress")}</span><span className={`mt-1 block text-xs ${disabled ? "text-warning" : "text-success"}`}>{disabled ? disabledReason : t("meetings.sharing.eligible")}</span></span>
    {selected ? <Check aria-hidden="true" className="me-1 h-4 w-4 shrink-0 text-primary" /> : null}
    <span className="sr-only">{mode === "email" ? t("meetings.sharing.emailOption") : t("meetings.sharing.completionOption")}</span>
  </div>;
}

function recipientUnavailableReason(recipient: MeetingSharingRecipientDto, mode: ShareMode, t: ReturnType<typeof useI18n>["t"]) {
  if (mode === "completion") return t("meetings.sharing.inAppNotEligible");
  const reasons: string[] = [];
  if (!recipient.email.trim()) reasons.push(t("meetings.sharing.noEmailAddress"));
  if (!recipient.minutesEmailPreferenceEnabled) reasons.push(t("meetings.sharing.emailOptedOut"));
  if (!recipient.minutesEmailProviderAvailable) reasons.push(t("meetings.sharing.emailProviderUnavailable"));
  return reasons.length ? reasons.join(" · ") : t("meetings.sharing.emailNotEligible");
}

function recipientInitials(name: string) {
  return name.trim().split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]?.toLocaleUpperCase() ?? "").join("") || "?";
}

function SharePreview({ mode, meeting, minutes, actions, actionsLoading, actionsError, selected, applicationLink, t, formatDate }: {
  mode: ShareMode;
  meeting: MeetingDetailDto;
  minutes: MeetingMinutesDto | null | undefined;
  actions: { id: string; title: string; assigneeName?: string | null; dueDate?: string | null; status: string }[];
  actionsLoading: boolean;
  actionsError: boolean;
  selected: MeetingSharingRecipientDto[];
  applicationLink: string;
  t: ReturnType<typeof useI18n>["t"];
  formatDate: ReturnType<typeof useI18n>["formatDate"];
}) {
  const orderedActions = [...actions].sort((a, b) => new Date(a.dueDate ?? "9999-12-31").getTime() - new Date(b.dueDate ?? "9999-12-31").getTime());
  return <div className="grid gap-5 lg:grid-cols-[minmax(0,1.2fr)_minmax(14rem,.8fr)]">
    <section className="min-w-0 rounded-lg border bg-background p-4 sm:p-5">
      <div className="border-b pb-3"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{mode === "email" ? t("meetings.sharing.emailPreview") : t("meetings.sharing.notificationPreview")}</p><h3 className="mt-1 break-words text-lg font-semibold">{mode === "email" ? t("meetings.sharing.minutesSubject", { title: meeting.title }) : t("meetings.sharing.completionTitle", { title: meeting.title })}</h3></div>
      <div className="space-y-4 pt-4 text-sm">
        <div className="grid gap-2 sm:grid-cols-2"><PreviewField label={t("meetings.sharing.meeting")} value={meeting.title} /><PreviewField label={t("meetings.project")} value={meeting.projectName || t("meetings.noProject")} /><PreviewField label={t("meetings.dateTime")} value={`${formatDate(meeting.startTime, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" })} UTC`} /><PreviewField label={t("meetings.organizer")} value={meeting.organizerName || t("meetings.notAvailable")} /></div>
        {mode === "email" ? <>
          <PreviewSection title={t("meetings.summary")} value={minutes?.summary} />
          <PreviewSection title={t("meetings.discussionNotes")} value={minutes?.discussionNotes} />
          <PreviewSection title={t("meetings.decisions")} value={minutes?.decisions} />
          <PreviewSection title={t("meetings.outcomes")} value={minutes?.outcomes} />
          <div className="space-y-2"><h4 className="font-semibold">{t("meetings.sharing.followUpActions")}</h4>{actionsLoading ? <p className="text-xs text-muted-foreground">{t("common.loading")}</p> : actionsError ? <p role="alert" className="text-xs text-destructive">{t("meetings.sharing.actionsLoadError")}</p> : orderedActions.length ? <ul className="space-y-2">{orderedActions.map((action) => <li key={action.id} className="rounded-md bg-muted/40 px-3 py-2"><p className="font-medium">{action.title}</p><p className="mt-0.5 text-xs text-muted-foreground">{action.assigneeName || t("meetings.sharing.unassigned")} · {action.dueDate ? formatDate(action.dueDate, { dateStyle: "medium", timeZone: "UTC" }) : t("meetings.sharing.noDueDate")} · {t(`meetings.actionStatus.${action.status.toLowerCase()}`)}</p></li>)}</ul> : <p className="text-xs text-muted-foreground">{t("meetings.sharing.noFollowUpActions")}</p>}</div>
        </> : <p className="rounded-md bg-muted/40 p-3 text-sm">{t("meetings.sharing.completionBody", { project: meeting.projectName || t("meetings.noProject"), date: formatDate(meeting.actualEndTime || meeting.endTime, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" }) })}</p>}
        <div className={`break-all border-t pt-3 text-xs ${applicationLink ? "text-primary" : "text-warning"}`}>{applicationLink ? <>{t("meetings.sharing.secureMeetingLink")}: {applicationLink}</> : t("meetings.sharing.applicationUrlNotConfigured")}</div>
      </div>
    </section>
    <aside className="space-y-3 rounded-lg bg-muted/30 p-4"><h3 className="text-sm font-semibold">{t("meetings.sharing.confirmRecipients", { count: selected.length })}</h3><p className="text-xs text-muted-foreground">{mode === "email" ? t("meetings.sharing.emailConfirmation") : t("meetings.sharing.notificationConfirmation")}</p><ul className="max-h-64 space-y-2 overflow-y-auto">{selected.map((recipient) => <li key={recipient.userId} className="rounded-md border bg-background px-3 py-2"><p className="truncate text-sm font-medium">{recipient.displayName}</p><p className="truncate text-xs text-muted-foreground">{recipient.email}</p></li>)}</ul></aside>
  </div>;
}

function ShareReview({ mode, meeting, selected, selectionProblem, isFinal, actionsLoading, actionsError, applicationLink, unavailableCount, formatDate, t }: {
  mode: ShareMode;
  meeting: MeetingDetailDto;
  selected: MeetingSharingRecipientDto[];
  selectionProblem: string | null;
  isFinal: boolean;
  actionsLoading: boolean;
  actionsError: boolean;
  applicationLink: string;
  unavailableCount: number;
  formatDate: ReturnType<typeof useI18n>["formatDate"];
  t: ReturnType<typeof useI18n>["t"];
}) {
  const messageTitle = mode === "email"
    ? t("meetings.sharing.minutesSubject", { title: meeting.title })
    : t("meetings.sharing.completionTitle", { title: meeting.title });
  return <div className="space-y-5">
    <div><h3 className="text-lg font-semibold">{t("meetings.sharing.reviewAndSend")}</h3><p className="mt-1 text-sm text-muted-foreground">{t("meetings.sharing.reviewDescription")}</p></div>
    <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(18rem,.8fr)]">
      <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-5">
        <div className="flex flex-wrap items-start justify-between gap-3 border-b pb-4"><div><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{t("meetings.sharing.sharingType")}</p><h4 className="mt-1 text-base font-semibold">{t(`meetings.sharing.types.${mode === "email" ? "MeetingMinutesShared" : "MeetingCompleted"}`)}</h4></div><Badge variant="secondary">{t("meetings.sharing.recipientCount", { count: selected.length })}</Badge></div>
        <div className="grid gap-4 sm:grid-cols-2"><PreviewField label={t("meetings.sharing.deliveryChannel")} value={mode === "email" ? t("meetings.sharing.channel.email") : t("meetings.sharing.completionDeliveryChannels")} /><PreviewField label={t("meetings.sharing.meeting")} value={meeting.title} /><PreviewField label={t("meetings.project")} value={meeting.projectName || t("meetings.noProject")} /><PreviewField label={t("meetings.sharing.dateTime")} value={`${formatDate(meeting.startTime, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" })} UTC`} /></div>
        <div className="rounded-lg bg-muted/40 p-4"><p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{t("meetings.sharing.messageSummary")}</p><p className="mt-1 font-semibold">{messageTitle}</p><p className="mt-1 text-sm text-muted-foreground">{mode === "email" ? t("meetings.sharing.finalMinutesSnapshot") : t("meetings.sharing.completionBody", { project: meeting.projectName || t("meetings.noProject"), date: formatDate(meeting.actualEndTime || meeting.endTime, { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" }) })}</p></div>
        {unavailableCount > 0 ? <p role="status" className="rounded-lg border border-warning/40 bg-warning/5 px-4 py-3 text-sm text-warning">{t("meetings.sharing.unavailableRecipientsExcluded", { count: unavailableCount })}</p> : null}
        {!applicationLink ? <p role="alert" className="rounded-lg border border-destructive/30 px-4 py-3 text-sm text-destructive">{t("meetings.sharing.applicationUrlNotConfigured")}</p> : <p className="break-all text-xs text-muted-foreground">{t("meetings.sharing.secureMeetingLink")}: {applicationLink}</p>}
        {selectionProblem ? <p role="status" className="rounded-lg border border-warning/40 px-4 py-3 text-sm text-warning">{selectionProblem}</p> : null}
        {mode === "email" && !isFinal ? <p role="status" className="rounded-lg border border-warning/40 px-4 py-3 text-sm text-warning">{t("meetings.sharing.finalMinutesRequired")}</p> : null}
        {mode === "email" && actionsLoading ? <p role="status" className="rounded-lg border px-4 py-3 text-sm text-muted-foreground">{t("common.loading")}</p> : null}
        {mode === "email" && actionsError ? <p role="alert" className="rounded-lg border border-destructive/30 px-4 py-3 text-sm text-destructive">{t("meetings.sharing.actionsLoadError")}</p> : null}
        <p className="text-sm text-muted-foreground">{mode === "email" ? t("meetings.sharing.emailConfirmation") : t("meetings.sharing.notificationConfirmation")}</p>
      </section>
      <aside className="rounded-xl border bg-muted/20 p-4 sm:p-5">
        <h4 className="font-semibold">{t("meetings.sharing.selectedRecipients")}</h4><p className="mt-1 text-sm text-muted-foreground">{t("meetings.sharing.recipientsWillReceive", { count: selected.length })}</p>
        <ul className="mt-4 max-h-[22rem] space-y-2 overflow-y-auto">{selected.map((recipient) => <li key={recipient.userId} className="flex min-w-0 items-center gap-3 rounded-lg border bg-background p-3"><span aria-hidden="true" className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-primary/10 text-xs font-semibold text-primary">{recipientInitials(recipient.displayName)}</span><span className="min-w-0"><span className="block truncate text-sm font-medium">{recipient.displayName}</span><span className="block truncate text-xs text-muted-foreground">{recipient.email || t("meetings.sharing.noEmailAddress")}</span></span></li>)}</ul>
      </aside>
    </div>
  </div>;
}

function PreviewField({ label, value }: { label: string; value: string }) {
  return <div className="min-w-0"><p className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-0.5 break-words">{value}</p></div>;
}

function PreviewSection({ title, value }: { title: string; value?: string | null }) {
  return <div className="space-y-1"><h4 className="font-semibold">{title}</h4>{value?.trim() ? <div className="prose prose-sm max-w-none dark:prose-invert"><RichTextContent value={value} /></div> : <p className="text-xs text-muted-foreground">—</p>}</div>;
}

function channelLabel(channel: string, t: ReturnType<typeof useI18n>["t"]) {
  switch (channel) {
    case "Email": return t("meetings.sharing.channel.email");
    case "Push": return t("meetings.sharing.channel.push");
    default: return t("meetings.sharing.channel.inApp");
  }
}

function historyStatusVariant(status: string): "success" | "destructive" | "warning" {
  if (status === "Sent") return "success";
  if (status === "Failed") return "destructive";
  return "warning";
}
