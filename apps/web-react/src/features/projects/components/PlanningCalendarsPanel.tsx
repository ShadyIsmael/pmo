import * as React from "react";
import { Edit3, Plus, RefreshCw, Save, Trash2 } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Skeleton } from "@components/ui/Skeleton";
import { toast } from "@hooks/use-toast";
import type { PlanCalendarDto } from "../api/planningFull";

type CalendarDayRule = PlanCalendarDto["dayRules"][number];
type CalendarHoliday = PlanCalendarDto["holidays"][number];
type CalendarTab = "general" | "week" | "holidays";
type CalendarDraft = {
  name: string;
  description: string;
  isDefault: boolean;
  effectiveFrom: string;
  effectiveTo: string;
  dayRules: CalendarDayRule[];
  holidays: CalendarHoliday[];
};
type CalendarMutations = {
  createCalendar: { mutateAsync: (body: unknown) => Promise<PlanCalendarDto>; isPending: boolean };
  updateCalendar: { mutateAsync: (input: { id: string; body: unknown }) => Promise<PlanCalendarDto>; isPending: boolean };
  deleteCalendar: { mutateAsync: (input: { id: string; rowVersion: string }) => Promise<void>; isPending: boolean };
};

const WEEKDAYS = [0, 1, 2, 3, 4, 5, 6];
const DAY_NAMES: Record<string, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
};

function defaultDayRule(dayOfWeek: number): CalendarDayRule {
  const isWorkingDay = dayOfWeek >= 1 && dayOfWeek <= 5;
  return {
    dayOfWeek,
    isWorkingDay,
    startTime: isWorkingDay ? "08:00:00" : undefined,
    endTime: isWorkingDay ? "17:00:00" : undefined,
    breakStartTime: isWorkingDay ? "12:00:00" : undefined,
    breakEndTime: isWorkingDay ? "13:00:00" : undefined,
  };
}

function dayIndex(value: number | string): number {
  return typeof value === "number" ? value : DAY_NAMES[value] ?? Number(value);
}

function normalizeDayRules(rules: CalendarDayRule[] | undefined): CalendarDayRule[] {
  const byDay = new Map((rules ?? []).map((rule) => [dayIndex(rule.dayOfWeek), { ...rule, dayOfWeek: dayIndex(rule.dayOfWeek) }]));
  return WEEKDAYS.map((day) => byDay.get(day) ?? defaultDayRule(day));
}

function dateValue(value?: string): string {
  return value?.slice(0, 10) ?? "";
}

function timeValue(value?: string): string {
  return value?.slice(0, 5) ?? "";
}

function apiTimeValue(value: string): string | undefined {
  return value ? (value.length === 5 ? `${value}:00` : value) : undefined;
}

function makeDraft(calendar: PlanCalendarDto): CalendarDraft {
  return {
    name: calendar.name,
    description: calendar.description ?? "",
    isDefault: calendar.isDefault,
    effectiveFrom: dateValue(calendar.effectiveFrom),
    effectiveTo: dateValue(calendar.effectiveTo),
    dayRules: normalizeDayRules(calendar.dayRules),
    holidays: calendar.holidays.map((holiday) => ({
      ...holiday,
      date: dateValue(holiday.date),
      endDate: dateValue(holiday.endDate) || undefined,
    })),
  };
}

export function PlanningCalendarsPanel({
  calendars,
  isLoading,
  isFetching,
  isError,
  onRetry,
  editable,
  selectedCalendarId,
  onSelectCalendar,
  mutations,
  onCalendarCreated,
  t,
}: {
  calendars: PlanCalendarDto[];
  isLoading: boolean;
  isFetching: boolean;
  isError: boolean;
  onRetry: () => void;
  editable: boolean;
  selectedCalendarId: string;
  onSelectCalendar: (calendarId: string) => void;
  mutations: CalendarMutations;
  onCalendarCreated: (calendar: PlanCalendarDto) => void;
  t: (key: string) => string;
}) {
  const [newName, setNewName] = React.useState("");
  const [tab, setTab] = React.useState<CalendarTab>("general");
  const [draft, setDraft] = React.useState<CalendarDraft | null>(null);
  const [holidayDraft, setHolidayDraft] = React.useState<CalendarHoliday>({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  const [editingHoliday, setEditingHoliday] = React.useState<number | null>(null);
  const [pendingCreatedId, setPendingCreatedId] = React.useState<string | null>(null);
  const [pendingDeletedId, setPendingDeletedId] = React.useState<string | null>(null);
  const selected = calendars.find((calendar) => calendar.id === selectedCalendarId);

  React.useEffect(() => {
    if (pendingCreatedId) {
      if (calendars.some((calendar) => calendar.id === pendingCreatedId)) setPendingCreatedId(null);
      else return;
    }
    if (pendingDeletedId) {
      if (calendars.some((calendar) => calendar.id === pendingDeletedId)) return;
      setPendingDeletedId(null);
    }
    if (!calendars.length) return;
    if (!selectedCalendarId || !calendars.some((calendar) => calendar.id === selectedCalendarId)) {
      onSelectCalendar(calendars.find((calendar) => calendar.isDefault)?.id ?? calendars[0].id);
    }
  }, [calendars, onSelectCalendar, pendingCreatedId, pendingDeletedId, selectedCalendarId]);

  React.useEffect(() => {
    if (!selected) {
      setDraft(null);
      return;
    }
    setDraft(makeDraft(selected));
    setTab("general");
    setEditingHoliday(null);
    setHolidayDraft({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  }, [selected]);

  const updateDayRule = (value: number | string, patch: Partial<CalendarDayRule>) => {
    setDraft((current) => {
      if (!current) return current;
      const target = dayIndex(value);
      return {
        ...current,
        dayRules: current.dayRules.map((rule) => {
          if (dayIndex(rule.dayOfWeek) !== target) return rule;
          const next = { ...rule, ...patch, dayOfWeek: target };
          if (patch.isWorkingDay === false) {
            next.startTime = undefined;
            next.endTime = undefined;
            next.breakStartTime = undefined;
            next.breakEndTime = undefined;
          } else if (patch.isWorkingDay === true && !rule.startTime) {
            next.startTime = "08:00:00";
            next.endTime = "17:00:00";
            next.breakStartTime = "12:00:00";
            next.breakEndTime = "13:00:00";
          }
          return next;
        }),
      };
    });
  };

  const saveCalendar = async () => {
    if (!selected || !draft || mutations.updateCalendar.isPending) return;
    try {
      await mutations.updateCalendar.mutateAsync({
        id: selected.id,
        body: {
          name: draft.name.trim(),
          description: draft.description.trim() || null,
          isDefault: draft.isDefault,
          templateId: selected.templateId || null,
          effectiveFrom: draft.effectiveFrom || null,
          effectiveTo: draft.effectiveTo || null,
          dayRules: draft.dayRules,
          holidays: draft.holidays.map((holiday) => ({
            ...holiday,
            description: holiday.description?.trim() || null,
            endDate: holiday.endDate || null,
          })),
          rowVersion: selected.rowVersion,
        },
      });
      toast({ title: t("planning.updated"), variant: "success" });
    } catch {
      toast({ title: t("planning.saveFailed"), variant: "destructive" });
    }
  };

  const createCalendar = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const name = newName.trim();
    if (!name || mutations.createCalendar.isPending) return;
    try {
      const calendar = await mutations.createCalendar.mutateAsync({
        name,
        dayRules: normalizeDayRules([]),
        holidays: [],
      });
      setPendingCreatedId(calendar.id);
      onCalendarCreated(calendar);
      setNewName("");
      toast({ title: t("planning.created"), variant: "success" });
    } catch {
      toast({ title: t("planning.saveFailed"), variant: "destructive" });
    }
  };

  const deleteCalendar = async (calendar: PlanCalendarDto) => {
    if (!window.confirm(t("planning.confirmDelete")) || mutations.deleteCalendar.isPending) return;
    try {
      await mutations.deleteCalendar.mutateAsync({ id: calendar.id, rowVersion: calendar.rowVersion });
      if (calendar.id === selectedCalendarId) {
        setPendingDeletedId(calendar.id);
        onSelectCalendar("");
      }
      toast({ title: t("planning.deleted"), variant: "success" });
    } catch {
      toast({ title: t("planning.saveFailed"), variant: "destructive" });
    }
  };

  const addOrUpdateHoliday = () => {
    if (!draft || !holidayDraft.name.trim() || !holidayDraft.date) return;
    const value = {
      ...holidayDraft,
      name: holidayDraft.name.trim(),
      description: holidayDraft.description?.trim() || undefined,
      endDate: holidayDraft.endDate || undefined,
    };
    setDraft((current) => {
      if (!current) return current;
      const holidays = [...current.holidays];
      if (editingHoliday === null) holidays.push(value);
      else holidays[editingHoliday] = value;
      return { ...current, holidays };
    });
    setEditingHoliday(null);
    setHolidayDraft({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  };

  const startHolidayEdit = (index: number) => {
    if (!draft) return;
    const holiday = draft.holidays[index];
    setEditingHoliday(index);
    setHolidayDraft({ ...holiday, date: dateValue(holiday.date), endDate: dateValue(holiday.endDate) });
  };

  const cancelHolidayEdit = () => {
    setEditingHoliday(null);
    setHolidayDraft({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  };

  return (
    <section data-testid="planning-calendar-editor" className="space-y-4">
      <div>
        <h2 className="text-base font-semibold">{t("planning.calendarTitle")}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("planning.calendarDescription")}</p>
      </div>

      {editable ? (
        <form className="flex flex-wrap gap-2" onSubmit={(event) => void createCalendar(event)}>
          <Input aria-label={t("planning.calendarName")} placeholder={t("planning.calendarName")} value={newName} onChange={(event) => setNewName(event.target.value)} />
          <Button type="submit" size="sm" disabled={mutations.createCalendar.isPending || !newName.trim()}>
            <Plus className="me-1.5 h-4 w-4" />{t("planning.actionButtons.create")}
          </Button>
        </form>
      ) : null}

      {isFetching && !isLoading ? <p role="status" className="text-xs text-muted-foreground">{t("common.loading")}</p> : null}
      {isLoading ? <div className="space-y-2" aria-label={t("common.loading")}><Skeleton className="h-10 w-full" /><Skeleton className="h-28 w-full" /></div> : null}
      {!isLoading && isError ? (
        <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-destructive/25 bg-destructive/5 p-4 text-sm">
          <span className="text-destructive">{t("planning.loadFailed")}</span>
          <Button type="button" size="sm" variant="outline" onClick={onRetry}><RefreshCw className="me-1.5 h-3.5 w-3.5" />{t("common.retry")}</Button>
        </div>
      ) : null}
      {!isLoading && !isError && calendars.length === 0 ? <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">{t("planning.noCalendars")}</p> : null}

      {!isLoading && !isError && calendars.length > 0 ? (
        <div className="grid min-w-0 gap-4 lg:grid-cols-[minmax(180px,0.35fr)_minmax(0,1fr)]">
          <div className="space-y-1">
            {calendars.map((calendar) => (
              <div key={calendar.id} className="flex items-stretch gap-1">
                <button
                  type="button"
                  aria-pressed={calendar.id === selectedCalendarId}
                  onClick={() => onSelectCalendar(calendar.id)}
                  className={`min-w-0 flex-1 rounded-md border px-3 py-2 text-start text-xs transition-colors ${calendar.id === selectedCalendarId ? "border-primary bg-primary/5" : "hover:bg-muted/40"}`}
                >
                  <span className="block truncate font-medium">{calendar.name}</span>
                  <span className="mt-1 block text-[10px] text-muted-foreground">
                    {calendar.dayRules.filter((rule) => rule.isWorkingDay).length} {t("planning.workingDays")} · {calendar.holidays.length} {t("planning.holidays")}
                  </span>
                </button>
                {editable ? <Button type="button" size="icon-sm" variant="ghost" aria-label={`${t("planning.actionButtons.delete")} ${calendar.name}`} disabled={mutations.deleteCalendar.isPending} onClick={() => void deleteCalendar(calendar)}><Trash2 className="h-4 w-4 text-destructive" /></Button> : null}
              </div>
            ))}
          </div>

          {selected && draft ? (
            <div className="min-w-0 rounded-md border p-4">
              <div role="tablist" aria-label={t("planning.calendarTitle")} className="mb-4 flex gap-1 overflow-x-auto border-b pb-2">
                {(["general", "week", "holidays"] as CalendarTab[]).map((value) => (
                  <button key={value} type="button" role="tab" aria-selected={tab === value} onClick={() => setTab(value)} className={`shrink-0 rounded px-2.5 py-1.5 text-xs ${tab === value ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-muted"}`}>
                    {t(`planning.calendarTabs.${value}`)}
                  </button>
                ))}
              </div>

              {tab === "general" ? (
                <div className="space-y-3">
                  <div className="grid gap-3 sm:grid-cols-2">
                    <label className="space-y-1.5 text-xs font-medium">{t("planning.calendarName")}<Input disabled={!editable} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} /></label>
                    <label className="space-y-1.5 text-xs font-medium">{t("planning.descriptionOptional")}<Input disabled={!editable} value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} /></label>
                    <label className="space-y-1.5 text-xs font-medium">{t("planning.effectiveFrom")}<Input type="date" disabled={!editable} value={draft.effectiveFrom} onChange={(event) => setDraft({ ...draft, effectiveFrom: event.target.value })} /></label>
                    <label className="space-y-1.5 text-xs font-medium">{t("planning.effectiveTo")}<Input type="date" disabled={!editable} value={draft.effectiveTo} onChange={(event) => setDraft({ ...draft, effectiveTo: event.target.value })} /></label>
                  </div>
                  <label className="flex items-center gap-2 rounded-md border bg-muted/20 p-3 text-xs"><input type="checkbox" disabled={!editable} checked={draft.isDefault} onChange={(event) => setDraft({ ...draft, isDefault: event.target.checked })} />{t("planning.defaultCalendar")}</label>
                  {selected.templateId ? <p className="text-[11px] text-muted-foreground">{t("planning.templateId")}: {selected.templateId}</p> : null}
                </div>
              ) : null}

              {tab === "week" ? (
                <div className="space-y-2">
                  <p className="text-xs text-muted-foreground">{t("planning.workingWeekDescription")}</p>
                  <div className="overflow-x-auto rounded-md border">
                    <div className="min-w-[660px]">
                      <div className="grid grid-cols-[1.2fr_0.8fr_1fr_1fr_1fr_1fr] gap-2 border-b bg-muted/30 px-3 py-2 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                        {["day", "working", "startTime", "endTime", "breakStart", "breakEnd"].map((key) => <span key={key}>{t(`planning.${key}`)}</span>)}
                      </div>
                      {draft.dayRules.map((rule) => {
                        const day = dayIndex(rule.dayOfWeek);
                        const dayName = t(`planning.days.${day}`);
                        const timeFields = [
                          ["startTime", rule.startTime, "startTime"],
                          ["endTime", rule.endTime, "endTime"],
                          ["breakStartTime", rule.breakStartTime, "breakStart"],
                          ["breakEndTime", rule.breakEndTime, "breakEnd"],
                        ] as const;
                        return (
                          <div key={day} className="grid grid-cols-[1.2fr_0.8fr_1fr_1fr_1fr_1fr] items-center gap-2 border-b px-3 py-2 text-xs last:border-b-0">
                            <span>{dayName}</span>
                            <input type="checkbox" aria-label={`${dayName} ${t("planning.working")}`} disabled={!editable} checked={rule.isWorkingDay} onChange={(event) => updateDayRule(day, { isWorkingDay: event.target.checked })} />
                            {timeFields.map(([field, value, label]) => <Input key={field} type="time" aria-label={`${dayName} ${t(`planning.${label}`)}`} disabled={!editable || !rule.isWorkingDay} value={timeValue(value)} onChange={(event) => updateDayRule(day, { [field]: apiTimeValue(event.target.value) })} />)}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </div>
              ) : null}

              {tab === "holidays" ? (
                <div className="space-y-3">
                  <p className="text-xs text-muted-foreground">{t("planning.holidaysDescription")}</p>
                  {editable ? (
                    <div className="grid gap-2 rounded-md border bg-muted/20 p-3 sm:grid-cols-2">
                      <Input aria-label={t("planning.holidayName")} placeholder={t("planning.holidayName")} value={holidayDraft.name} onChange={(event) => setHolidayDraft({ ...holidayDraft, name: event.target.value })} />
                      <Input aria-label={t("planning.holidayDescription")} placeholder={t("planning.holidayDescription")} value={holidayDraft.description ?? ""} onChange={(event) => setHolidayDraft({ ...holidayDraft, description: event.target.value })} />
                      <Input aria-label={t("planning.effectiveFrom")} type="date" value={dateValue(holidayDraft.date)} onChange={(event) => setHolidayDraft({ ...holidayDraft, date: event.target.value })} />
                      <Input aria-label={t("planning.effectiveTo")} type="date" value={dateValue(holidayDraft.endDate)} onChange={(event) => setHolidayDraft({ ...holidayDraft, endDate: event.target.value })} />
                      <label className="flex items-center gap-2 text-xs"><input type="checkbox" checked={holidayDraft.isRecurring} onChange={(event) => setHolidayDraft({ ...holidayDraft, isRecurring: event.target.checked })} />{t("planning.recurring")}</label>
                      <div className="flex gap-2">
                        <Button type="button" size="sm" onClick={addOrUpdateHoliday} disabled={!holidayDraft.name.trim() || !holidayDraft.date}>{t(`planning.actionButtons.${editingHoliday === null ? "create" : "save"}`)}</Button>
                        {editingHoliday !== null ? <Button type="button" size="sm" variant="ghost" onClick={cancelHolidayEdit}>{t("planning.actionButtons.cancel")}</Button> : null}
                      </div>
                    </div>
                  ) : null}
                  {draft.holidays.length === 0 ? <p className="rounded-md border border-dashed p-5 text-center text-sm text-muted-foreground">{t("planning.noHolidays")}</p> : (
                    <div className="divide-y rounded-md border">
                      {draft.holidays.map((holiday, index) => (
                        <div key={`${holiday.name}-${index}`} className="flex items-center gap-3 px-3 py-2 text-xs">
                          <div className="min-w-0 flex-1">
                            <p className="truncate font-medium">{holiday.name}</p>
                            <p className="text-muted-foreground">{dateValue(holiday.date)}{holiday.endDate ? ` → ${dateValue(holiday.endDate)}` : ""}{holiday.isRecurring ? ` · ${t("planning.recurring")}` : ""}</p>
                            {holiday.description ? <p className="truncate text-muted-foreground">{holiday.description}</p> : null}
                          </div>
                          {editable ? <div className="flex gap-1">
                            <Button type="button" size="icon-sm" variant="ghost" aria-label={`${t("planning.actionButtons.edit")} ${holiday.name}`} onClick={() => startHolidayEdit(index)}><Edit3 className="h-4 w-4" /></Button>
                            <Button type="button" size="icon-sm" variant="ghost" aria-label={`${t("planning.actionButtons.delete")} ${holiday.name}`} onClick={() => setDraft({ ...draft, holidays: draft.holidays.filter((_item, itemIndex) => itemIndex !== index) })}><Trash2 className="h-4 w-4 text-destructive" /></Button>
                          </div> : null}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ) : null}

              {editable ? <div className="mt-4 flex justify-end border-t pt-3"><Button type="button" size="sm" onClick={() => void saveCalendar()} disabled={mutations.updateCalendar.isPending || !draft.name.trim()}><Save className="me-1.5 h-3.5 w-3.5" />{t("planning.actionButtons.save")}</Button></div> : null}
            </div>
          ) : <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">{t("planning.noCalendars")}</p>}
        </div>
      ) : null}
    </section>
  );
}
