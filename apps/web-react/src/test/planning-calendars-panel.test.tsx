import * as React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlanCalendarDto } from "@features/projects/api/planningFull";
import { PlanningCalendarsPanel } from "@features/projects/components/PlanningCalendarsPanel";
import { I18nProvider } from "../i18n/messages";

const t = (key: string) => key;

function makeCalendar(overrides: Partial<PlanCalendarDto> = {}): PlanCalendarDto {
  return {
    id: "calendar-1",
    projectId: "project-1",
    planVersionId: "version-1",
    name: "Standard calendar",
    isDefault: true,
    dayRules: [],
    holidays: [],
    rowVersion: "row-1",
    ...overrides,
  };
}

function makeMutations() {
  return {
    createCalendar: { mutateAsync: vi.fn().mockResolvedValue(makeCalendar({ id: "calendar-2", name: "New calendar" })), isPending: false },
    updateCalendar: { mutateAsync: vi.fn().mockResolvedValue(makeCalendar()), isPending: false },
    deleteCalendar: { mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false },
  };
}

function renderPanel({
  calendars = [makeCalendar()],
  selectedCalendarId = "calendar-1",
  editable = true,
  mutations = makeMutations(),
  onCalendarCreated = vi.fn(),
}: {
  calendars?: PlanCalendarDto[];
  selectedCalendarId?: string;
  editable?: boolean;
  mutations?: ReturnType<typeof makeMutations>;
  onCalendarCreated?: (calendar: PlanCalendarDto) => void;
} = {}) {
  const onSelectCalendar = vi.fn();
  render(<I18nProvider><PlanningCalendarsPanel calendars={calendars} isLoading={false} isFetching={false} isError={false} onRetry={vi.fn()} editable={editable} selectedCalendarId={selectedCalendarId} onSelectCalendar={onSelectCalendar} mutations={mutations} onCalendarCreated={onCalendarCreated} t={t} /></I18nProvider>);
  return { mutations, onCalendarCreated, onSelectCalendar };
}

describe("planning calendars panel", () => {
  it("renders calendar settings and saves normalized work-week rules", async () => {
    const { mutations } = renderPanel();

    expect(screen.getByText("Standard calendar")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "planning.calendarTabs.week" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "planning.days.0 planning.working" }));
    fireEvent.click(screen.getByRole("button", { name: "planning.actionButtons.save" }));

    await waitFor(() => expect(mutations.updateCalendar.mutateAsync).toHaveBeenCalledTimes(1));
    const input = mutations.updateCalendar.mutateAsync.mock.calls[0][0];
    expect(input.id).toBe("calendar-1");
    expect(input.body).toMatchObject({
      rowVersion: "row-1",
      dayRules: expect.arrayContaining([expect.objectContaining({ dayOfWeek: 0, isWorkingDay: true, startTime: "08:00:00" })]),
    });
  });

  it("creates a calendar and forwards the created record to the workspace", async () => {
    const { mutations, onCalendarCreated } = renderPanel({ calendars: [], selectedCalendarId: "" });
    fireEvent.change(screen.getByPlaceholderText("planning.calendarName"), { target: { value: "New calendar" } });
    fireEvent.click(screen.getByRole("button", { name: "planning.actionButtons.create" }));

    await waitFor(() => expect(mutations.createCalendar.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ name: "New calendar", holidays: [] })));
    expect(onCalendarCreated).toHaveBeenCalledWith(expect.objectContaining({ id: "calendar-2" }));
  });

  it("keeps the newly created selection while the calendar query refreshes", async () => {
    const created = makeCalendar({ id: "calendar-2", name: "New calendar", isDefault: false });
    const mutations = makeMutations();
    mutations.createCalendar.mutateAsync.mockResolvedValue(created);
    function Harness() {
      const [calendars, setCalendars] = React.useState([makeCalendar()]);
      const [selectedCalendarId, setSelectedCalendarId] = React.useState("calendar-1");
      return <>
        <button type="button" onClick={() => setCalendars((current) => [...current, created])}>refresh calendars</button>
        <PlanningCalendarsPanel calendars={calendars} isLoading={false} isFetching={false} isError={false} onRetry={vi.fn()} editable selectedCalendarId={selectedCalendarId} onSelectCalendar={setSelectedCalendarId} mutations={mutations} onCalendarCreated={(calendar) => setSelectedCalendarId(calendar.id)} t={t} />
      </>;
    }
    render(<I18nProvider><Harness /></I18nProvider>);
    fireEvent.change(screen.getByPlaceholderText("planning.calendarName"), { target: { value: "New calendar" } });
    fireEvent.click(screen.getByRole("button", { name: "planning.actionButtons.create" }));

    await waitFor(() => expect(mutations.createCalendar.mutateAsync).toHaveBeenCalled());
    fireEvent.click(screen.getByRole("button", { name: "refresh calendars" }));
    expect(await screen.findByText("New calendar").then((label) => label.closest("button"))).toHaveAttribute("aria-pressed", "true");
  });

  it("shows a retry action when calendar data cannot be loaded", () => {
    render(<I18nProvider><PlanningCalendarsPanel calendars={[]} isLoading={false} isFetching={false} isError editable={false} selectedCalendarId="" onSelectCalendar={vi.fn()} onRetry={vi.fn()} mutations={makeMutations()} onCalendarCreated={vi.fn()} t={t} /></I18nProvider>);

    expect(screen.getByRole("alert")).toHaveTextContent("planning.loadFailed");
    expect(screen.getByRole("button", { name: /common.retry/ })).toBeInTheDocument();
  });
});
