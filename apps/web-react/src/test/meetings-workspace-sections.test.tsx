import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { MeetingWorkspaceSections } from "../features/delivery/MeetingWorkspaceSections";
import { resolveNotificationPath } from "../lib/notification-navigation";

const state = vi.hoisted(() => ({ locale: "en", usersError: false, retryUsers: vi.fn() }));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "user-1", permissions: ["Meeting.View", "Meeting.Edit", "Meeting.Manage", "WorkItem.View"] } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

vi.mock("@lib/permissions", () => ({
  Permissions: {
    Meeting: { View: "Meeting.View", Edit: "Meeting.Edit", Manage: "Meeting.Manage" },
    WorkItem: { View: "WorkItem.View", Create: "WorkItem.Create" },
  },
  hasPermission: (permissions: string[], permission: string) => permissions.includes(permission),
}));

vi.mock("@components/ui/ConfirmProvider", () => ({
  ConfirmProvider: ({ children }: { children: ReactNode }) => children,
  useConfirm: () => vi.fn(async () => true),
}));

vi.mock("@api/workItemsFull", () => ({
  useFullWorkItems: () => ({ data: { items: [], totalCount: 0, totalPages: 0 }, isLoading: false, isError: false }),
  useFullWorkItemMutations: () => ({ create: { isPending: false, mutateAsync: vi.fn() } }),
}));

vi.mock("@features/projects/lib/project-lookups", () => ({
  useMeetingWorkflowUserLookup: () => ({ data: [], isLoading: false, isError: state.usersError, refetch: state.retryUsers }),
}));

vi.mock("@api/meetingsFull", () => {
  const mutation = () => ({ isPending: false, isSuccess: false, mutateAsync: vi.fn() });
  return {
    useMeetingAgenda: () => ({
      data: [{ id: "agenda-1", meetingId: "meeting-1", topic: "Project review", sortOrder: 0, rowVersion: "v1" }],
      isLoading: false,
      isError: false,
    }),
    useMeetingParticipants: () => ({
      data: [{ id: "participant-1", meetingId: "meeting-1", userId: "user-1", displayName: "Alex Rivera", email: "alex@example.com", isInternalUser: true, attendanceStatus: "Accepted", rowVersion: "v1" }],
      isLoading: false,
      isError: false,
    }),
    useMeetingDecisions: () => ({
      data: { decisions: "", isEditable: true },
      isLoading: false,
      isError: false,
      isSuccess: true,
    }),
    useMeetingActionItems: () => ({
      data: [{ id: "action-1", meetingId: "meeting-1", title: "Share the revised schedule", status: "Open", rowVersion: "v1" }],
      isLoading: false,
      isError: false,
    }),
    useMeetingWorkspaceMutations: () => ({
      createAgendaItem: mutation(), updateAgendaItem: mutation(), deleteAgendaItem: mutation(), reorderAgenda: mutation(),
      updateMyAttendance: mutation(), updateAttendance: mutation(), updateOrganizerAttendance: mutation(), saveDecisions: mutation(),
      createActionItem: mutation(), updateActionItem: mutation(), deleteActionItem: mutation(),
      linkActionWorkItem: mutation(), unlinkActionWorkItem: mutation(),
    }),
  };
});

function renderWorkspace(meetingStatus = "Completed") {
  window.localStorage.setItem("nexus-locale", state.locale);
  return render(<MemoryRouter><I18nProvider><MeetingWorkspaceSections meetingId="meeting-1" meetingStatus={meetingStatus} projectId="project-1" /></I18nProvider></MemoryRouter>);
}

describe("Meeting workspace sections", () => {
  beforeEach(() => { state.locale = "en"; state.usersError = false; state.retryUsers.mockReset(); });

  it("shows the persisted agenda, participant response, action items, and work-item linking control", () => {
    renderWorkspace();

    expect(screen.getByText("Agenda")).toBeInTheDocument();
    expect(screen.getByText("Project review")).toBeInTheDocument();
    expect(screen.getByText("Attendance")).toBeInTheDocument();
    expect(screen.getByText("Alex Rivera")).toBeInTheDocument();
    expect(screen.getByText("Action items")).toBeInTheDocument();
    expect(screen.getByText("Share the revised schedule")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Link existing Work Item" })).toBeInTheDocument();
  });

  it("localizes the workspace section labels in Arabic", () => {
    state.locale = "ar";
    renderWorkspace();

    expect(screen.getByText("جدول الأعمال")).toBeInTheDocument();
    expect(screen.getByText("الحضور")).toBeInTheDocument();
    expect(screen.getByText("بنود العمل")).toBeInTheDocument();
  });

  it("keeps a scheduled meeting limited to its agenda", () => {
    renderWorkspace("Scheduled");

    expect(screen.getByText("Agenda")).toBeInTheDocument();
    expect(screen.queryByText("Attendance")).not.toBeInTheDocument();
    expect(screen.queryByText("Action items")).not.toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Link existing Work Item" })).not.toBeInTheDocument();
  });

  it("shows live attendance and decisions without the completed work-item entry point while in progress", () => {
    renderWorkspace("InProgress");

    expect(screen.getByText("Attendance")).toBeInTheDocument();
    expect(screen.getByText("Decisions")).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Link existing Work Item" })).not.toBeInTheDocument();
  });

  it("shows a retryable error when action assignee options fail to load", () => {
    state.usersError = true;
    renderWorkspace();

    expect(screen.getByText("Users could not be loaded.")).toBeInTheDocument();
    const retryButtons = screen.getAllByRole("button", { name: "Retry" });
    expect(retryButtons.length).toBeGreaterThan(0);
    retryButtons[0].click();
    expect(state.retryUsers).toHaveBeenCalledOnce();
  });

  it("opens meeting reminders on the meeting detail page", () => {
    expect(resolveNotificationPath("Meeting", "10000000-0000-4000-8000-000000000001"))
      .toBe("/meetings/10000000-0000-4000-8000-000000000001");
  });
});
