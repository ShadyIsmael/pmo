import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { TeamMembersPanel, TeamRowActions, TeamsPage } from "../features/teams/TeamsPage";

const fixtures = vi.hoisted(() => ({
  teams: vi.fn(),
  departments: vi.fn(),
  members: vi.fn(),
  resources: vi.fn(),
  createHook: vi.fn(),
  updateHook: vi.fn(),
  deactivateHook: vi.fn(),
  reactivateHook: vi.fn(),
  deleteHook: vi.fn(),
  setMembersHook: vi.fn(),
  removeMemberHook: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  deactivate: vi.fn(),
  reactivate: vi.fn(),
  delete: vi.fn(),
  saveMembers: vi.fn(),
  removeMember: vi.fn(),
  refetchTeams: vi.fn(),
  refetchMembers: vi.fn(),
  refetchResources: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: ["Team.View", "Team.Create", "Team.Edit", "Team.Delete"] }, status: "authenticated" }) }));
vi.mock("../features/teams/api/teams", () => ({
  useTeams: fixtures.teams,
  useCreateTeam: fixtures.createHook,
  useUpdateTeam: fixtures.updateHook,
  useDeactivateTeam: fixtures.deactivateHook,
  useReactivateTeam: fixtures.reactivateHook,
  useDeleteTeam: fixtures.deleteHook,
  useTeamMembers: fixtures.members,
  useSetTeamMembers: fixtures.setMembersHook,
  useRemoveTeamMember: fixtures.removeMemberHook,
  useDepartments: fixtures.departments,
}));
vi.mock("@features/resources/api/resources", () => ({ useResources: fixtures.resources }));

const team = {
  id: "team-1", name: "Platform Team", code: "TEAM-001", description: "Owns platform delivery", departmentId: "dept-1", departmentName: "Engineering",
  teamLeadId: "resource-1", teamLeadName: "Alex Admin", isActive: true, emailAlias: "platform", memberCount: 1, members: [],
};

const memberRows = [
  { id: "membership-current", teamId: team.id, resourceId: "resource-1", resourceName: "Alex Admin", resourceType: "Staff", effectiveFrom: "2026-02-01T12:00:00.000Z", isActive: true },
  { id: "membership-history", teamId: team.id, resourceId: "resource-2", resourceName: "Former Member", resourceType: "Staff", effectiveFrom: "2025-01-01T12:00:00.000Z", effectiveTo: "2025-12-31T12:00:00.000Z", isActive: false },
];

const resourceRows = [
  { id: "resource-1", name: "Alex Admin", type: "Staff", isActive: true },
  { id: "resource-2", name: "Former Member", type: "Staff", isActive: true },
  { id: "resource-3", name: "New Resource", type: "VendorResource", isActive: true },
  { id: "resource-4", name: "Inactive Resource", type: "Staff", isActive: false },
];

function renderWithI18n(element: React.ReactElement) {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider>{element}</I18nProvider>);
}

describe("Teams source parity", () => {
  beforeEach(() => {
    const mutation = (mutateAsync = vi.fn().mockResolvedValue(undefined), mutate = vi.fn()) => ({ mutateAsync, mutate, isPending: false });
    fixtures.createHook.mockReset().mockReturnValue(mutation(fixtures.create));
    fixtures.updateHook.mockReset().mockReturnValue(mutation(fixtures.update));
    fixtures.deactivateHook.mockReset().mockReturnValue(mutation(fixtures.deactivate));
    fixtures.reactivateHook.mockReset().mockReturnValue(mutation(fixtures.reactivate));
    fixtures.deleteHook.mockReset().mockReturnValue(mutation(fixtures.delete));
    fixtures.setMembersHook.mockReset().mockReturnValue(mutation(fixtures.saveMembers));
    fixtures.removeMemberHook.mockReset().mockReturnValue(mutation(fixtures.removeMember));
    fixtures.create.mockReset().mockResolvedValue(undefined);
    fixtures.update.mockReset().mockResolvedValue(undefined);
    fixtures.deactivate.mockReset().mockResolvedValue(undefined);
    fixtures.reactivate.mockReset().mockResolvedValue(undefined);
    fixtures.delete.mockReset().mockResolvedValue(undefined);
    fixtures.saveMembers.mockReset().mockResolvedValue(undefined);
    fixtures.removeMember.mockReset().mockResolvedValue(undefined);
    fixtures.refetchTeams.mockReset();
    fixtures.refetchMembers.mockReset();
    fixtures.refetchResources.mockReset();
    fixtures.teams.mockReset().mockReturnValue({ data: { items: [team], totalCount: 1, totalPages: 1 }, isLoading: false, isError: false, refetch: fixtures.refetchTeams });
    fixtures.departments.mockReset().mockReturnValue({ data: [{ id: "dept-1", name: "Engineering", isActive: true }], isLoading: false, isError: false });
    fixtures.members.mockReset().mockImplementation((teamId: string | null) => teamId
      ? { data: memberRows, isLoading: false, isError: false, refetch: fixtures.refetchMembers }
      : { data: undefined, isLoading: false, isError: false, refetch: fixtures.refetchMembers });
    fixtures.resources.mockReset().mockImplementation((_params: unknown, enabled: boolean) => enabled
      ? { data: { items: resourceRows }, isLoading: false, isError: false, refetch: fixtures.refetchResources }
      : { data: undefined, isLoading: false, isError: false, refetch: fixtures.refetchResources });
  });

  it("loads complete team details and memberships only after opening Manage Members", async () => {
    renderWithI18n(<TeamRowActions team={team} />);

    expect(fixtures.members).not.toHaveBeenCalled();
    expect(fixtures.resources).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Actions" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Manage members" }));

    expect(await screen.findByText("Owns platform delivery")).toBeInTheDocument();
    expect(screen.getByText("platform")).toBeInTheDocument();
    expect(screen.getAllByText("Alex Admin")).toHaveLength(2);
    await waitFor(() => expect(fixtures.members).toHaveBeenLastCalledWith(team.id, false));
    expect(fixtures.resources).toHaveBeenLastCalledWith(expect.objectContaining({ pageSize: 200 }), true);
  });

  it("keeps current members separate from history and supports individual removal and additions", async () => {
    renderWithI18n(<TeamMembersPanel teamId={team.id} open canEdit />);

    expect(await screen.findByText(/Member since/)).toBeInTheDocument();
    expect(screen.getByText("Alex Admin")).toBeInTheDocument();
    const currentMembers = screen.getByRole("region", { name: "Members" });
    expect(within(currentMembers).queryByText("Former Member")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Alex Admin")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Inactive Resource")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Remove" }));
    await waitFor(() => expect(fixtures.removeMember).toHaveBeenCalledWith({ teamId: team.id, resourceId: "resource-1" }));

    fireEvent.click(screen.getByRole("button", { name: "Show history" }));
    const membershipHistory = screen.getByRole("region", { name: "Membership history" });
    expect(within(membershipHistory).getByText("Former Member")).toBeInTheDocument();
    expect(within(membershipHistory).getByText(/2025/)).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText(/^New Resource/));
    fireEvent.click(screen.getByRole("button", { name: "Save members" }));
    await waitFor(() => expect(fixtures.saveMembers).toHaveBeenCalledWith({ teamId: team.id, resourceIds: ["resource-1", "resource-3"] }));
  });

  it("uses API status filtering, semantic text-only colors, and the source clear-filters action", async () => {
    renderWithI18n(<TeamsPage />);

    const activeStatus = screen.getByRole("cell", { name: "Active" });
    expect(within(activeStatus).getByText("Active")).toHaveClass("text-success");
    expect(within(activeStatus).getByText("Active").className).not.toMatch(/\bbg-/);
    fireEvent.change(screen.getByRole("combobox", { name: "Filter by status" }), { target: { value: "inactive" } });
    await waitFor(() => expect(fixtures.teams).toHaveBeenLastCalledWith(expect.objectContaining({ isActive: false }), true));

    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() => expect(fixtures.teams).toHaveBeenLastCalledWith(expect.objectContaining({ isActive: undefined, departmentId: undefined, search: undefined }), true));
  });

  it("offers a retry when the Teams request fails", () => {
    fixtures.teams.mockReturnValue({ data: undefined, isLoading: false, isError: true, error: new Error("temporarily unavailable"), refetch: fixtures.refetchTeams });
    renderWithI18n(<TeamsPage />);

    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(fixtures.refetchTeams).toHaveBeenCalledOnce();
    expect(screen.getByRole("alert")).toHaveTextContent("temporarily unavailable");
  });
});
