import type { ComponentProps } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { MeetingCalendarView } from "@features/delivery/MeetingCalendarView";
import type { MeetingListDto } from "@api/meetingsFull";

function localIso(year: number, monthIndex: number, day: number, hours = 0, minutes = 0) {
  return new Date(year, monthIndex, day, hours, minutes, 0, 0).toISOString();
}

const anchor = new Date(2026, 9, 15);

function meeting(id: string, startTime: string, endTime: string, overrides: Partial<MeetingListDto> = {}): MeetingListDto {
  const publicIds: Record<string, string> = {
    a: "a0000000-0000-4000-8000-000000000001",
    b: "b0000000-0000-4000-8000-000000000002",
    c: "c0000000-0000-4000-8000-000000000003",
  };
  return {
    id,
    publicId: publicIds[id],
    title: `Meeting ${id}`,
    projectId: "internal-1",
    projectPublicId: "public-1",
    projectName: "Core Modernization",
    startTime,
    endTime,
    status: "Scheduled",
    attendeeCount: 2,
    minutesState: null,
    isRecurring: false,
    ...overrides,
  };
}

const meetings: MeetingListDto[] = [
  meeting("a", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 10), { title: "Planning review" }),
  meeting("b", localIso(2026, 9, 15, 10), localIso(2026, 9, 15, 11), {
    title: "Risk workshop",
    projectPublicId: "public-2",
    projectId: "internal-2",
    projectName: "Digital Rollout",
    status: "InProgress",
  }),
  meeting("c", localIso(2026, 9, 15, 13), localIso(2026, 9, 15, 14), {
    title: "Steering committee",
    projectPublicId: "public-3",
    projectId: "internal-3",
    projectName: "Cloud Migration",
    status: "Cancelled",
  }),
];

function renderView(overrides: Partial<ComponentProps<typeof MeetingCalendarView>> = {}) {
  window.localStorage.setItem("nexus-locale", "en");
  const props: ComponentProps<typeof MeetingCalendarView> = {
    mode: "month",
    anchor,
    meetings,
    isLoading: false,
    isError: false,
    isTruncated: false,
    totalCount: meetings.length,
    onModeChange: vi.fn(),
    onAnchorChange: vi.fn(),
    onDayClick: vi.fn(),
    onRetry: vi.fn(),
    ...overrides,
  };
  return { props, ...render(<MemoryRouter><I18nProvider><MeetingCalendarView {...props} /></I18nProvider></MemoryRouter>) };
}

describe("MeetingCalendarView", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
  });

  it("renders the month grid with project, status and overflow information", () => {
    renderView();

    expect(screen.getByRole("grid")).toBeInTheDocument();
    const links = screen.getAllByRole("link");
    expect(links).toHaveLength(2);
    expect(links[0]).toHaveAttribute("href", "/meetings/a0000000-0000-4000-8000-000000000001");
    expect(links[0]).toHaveAttribute("title", expect.stringContaining("Planning review"));
    expect(links[0]).toHaveAttribute("title", expect.stringContaining("Core Modernization"));
    expect(links[0]).toHaveAttribute("title", expect.stringContaining("Scheduled"));
    expect(screen.getByText("Planning review")).toBeInTheDocument();
    expect(screen.getByText("Risk workshop")).toBeInTheDocument();
    expect(screen.queryByText("Steering committee")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "+1 more" })).toBeInTheDocument();
  });

  it("shows the project colour and status legends", () => {
    renderView();

    expect(screen.getByText("Project colours")).toBeInTheDocument();
    expect(screen.getAllByText("Core Modernization").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Digital Rollout").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Cloud Migration").length).toBeGreaterThan(0);
    expect(screen.getByText("Meeting status")).toBeInTheDocument();
    expect(screen.getAllByText("In progress").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Completed").length).toBeGreaterThan(0);
  });

  it("opens a date-scoped day view when a day cell is activated", () => {
    const { props } = renderView();

    fireEvent.click(screen.getByRole("button", { name: "+1 more" }));

    expect(props.onDayClick).toHaveBeenCalledWith(expect.any(Date));
    const clicked = (props.onDayClick as ReturnType<typeof vi.fn>).mock.calls[0][0] as Date;
    expect(clicked.getDate()).toBe(15);
    expect(clicked.getMonth()).toBe(9);
  });

  it("renders every meeting in day mode with time, project and status", () => {
    renderView({ mode: "day" });

    const links = screen.getAllByRole("link");
    expect(links).toHaveLength(3);
    expect(screen.getByText("Steering committee")).toBeInTheDocument();
    expect(screen.getAllByText("Cancelled").length).toBeGreaterThan(0);
    expect(screen.getByText("9:00 AM – 10:00 AM")).toBeInTheDocument();
  });

  it("reports truncation and lets the user retry a failed load", () => {
    const onRetry = vi.fn();
    const { rerender } = render(
      <MemoryRouter><I18nProvider><MeetingCalendarView
        mode="month"
        anchor={anchor}
        meetings={meetings}
        isLoading={false}
        isError={false}
        isTruncated
        totalCount={2500}
        onModeChange={vi.fn()}
        onAnchorChange={vi.fn()}
        onDayClick={vi.fn()}
        onRetry={onRetry}
      /></I18nProvider></MemoryRouter>,
    );

    expect(screen.getByRole("status")).toHaveTextContent("This range has more meetings than can be displayed.");

    rerender(
      <MemoryRouter><I18nProvider><MeetingCalendarView
        mode="month"
        anchor={anchor}
        meetings={[]}
        isLoading={false}
        isError
        onModeChange={vi.fn()}
        onAnchorChange={vi.fn()}
        onDayClick={vi.fn()}
        onRetry={onRetry}
      /></I18nProvider></MemoryRouter>,
    );

    expect(screen.getByText("The calendar could not be loaded.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});

