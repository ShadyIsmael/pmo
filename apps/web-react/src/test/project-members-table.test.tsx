import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import arMessages from "../i18n/messages/ar.json";
import { ProjectMembersTab } from "../features/projects/ProjectMembersTab";

const fixtures = vi.hoisted(() => ({
  people: [{
    id: "person-1",
    projectId: "project-1",
    resourceId: "resource-1",
    name: "Alex Rivera",
    email: "alex@example.com",
    jobTitle: "Engineer",
    resourceType: "Staff",
    userId: "user-1",
    hasSystemUser: true,
    source: "TeamMember",
    relationshipType: "TeamMember",
    status: "Active",
    allocationPercentage: 75,
    hoursPerWeek: 30,
    isLegacy: false,
    rowVersion: "v1",
    functionalRoles: [] as { id: string; code: string; nameEn: string; nameAr: string; isPrimary: boolean }[],
    createdAt: "2026-10-01T00:00:00Z",
  }],
  roles: [
    { id: "role-primary", code: "LEAD", nameEn: "Delivery Lead", nameAr: "قائد التسليم", isActive: true, displayOrder: 1 },
    { id: "role-secondary", code: "ANALYST", nameEn: "Business Analyst", nameAr: "محلل أعمال", isActive: true, displayOrder: 2 },
  ],
  updatePerson: vi.fn(),
  peopleQuery: vi.fn(),
  peopleQueryError: false,
  refetchPeople: vi.fn(),
  resourceQueries: vi.fn(),
  permissions: ["Resource.Assign", "Stakeholders.Read", "Stakeholders.Manage"] as string[],
  invitation: null as null | { status: string; expiresAt: string; invitationUrl: string },
  invitationError: null as Error | null,
  sendInvitation: vi.fn(),
  revokeInvitation: vi.fn(),
}));

vi.mock("@features/projects/api/projects", () => ({
  useProjectPeople: (projectId: string, enabled: boolean) => {
    fixtures.peopleQuery(projectId, enabled);
    return { data: fixtures.people, isLoading: false, isError: fixtures.peopleQueryError, refetch: fixtures.refetchPeople };
  },
  useAddProjectPerson: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useRemoveProjectPerson: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateProjectPerson: () => ({ mutateAsync: fixtures.updatePerson, isPending: false }),
  useClearProjectSponsor: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCheckResourceConflicts: () => ({ data: [], isFetching: false }),
}));

vi.mock("@features/resources/api/resources", () => ({
  useResources: (params: unknown, enabled: boolean) => {
    fixtures.resourceQueries(params, enabled);
    return { data: { items: [] }, isLoading: false };
  },
}));

vi.mock("@features/administration/api/admin", () => ({
  useProjectRoles: () => ({ data: fixtures.roles }),
}));

vi.mock("@features/projects/api/stakeholder-invitations", () => ({
  useRevokeStakeholderInvitation: () => ({ mutateAsync: fixtures.revokeInvitation, reset: vi.fn(), isPending: false, error: null }),
  useSendStakeholderInvitation: () => ({ mutateAsync: fixtures.sendInvitation, reset: vi.fn(), isPending: false, error: fixtures.invitationError }),
  useStakeholderInvitation: () => ({ data: fixtures.invitation, isLoading: false }),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: fixtures.permissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1" }} />;
}

describe("project members table parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Resource.Assign", "Stakeholders.Read", "Stakeholders.Manage"];
    fixtures.peopleQuery.mockReset();
    fixtures.peopleQueryError = false;
    fixtures.refetchPeople.mockReset();
    fixtures.resourceQueries.mockReset();
    Object.assign(fixtures.people[0], { source: "TeamMember", relationshipType: "TeamMember", status: "Active", hasSystemUser: true, email: "alex@example.com" });
    fixtures.people[0]!.functionalRoles = [];
    fixtures.invitation = null;
    fixtures.invitationError = null;
    fixtures.sendInvitation.mockReset().mockResolvedValue({});
    fixtures.revokeInvitation.mockReset().mockResolvedValue({});
    fixtures.updatePerson.mockReset().mockResolvedValue({});
  });

  it("shows the source relationship, resource, system-user, allocation, status and action columns", () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByRole("columnheader", { name: "Name" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Project Relationship" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Resource Type" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "System User" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Allocation %" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Status" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Actions" })).toBeInTheDocument();
    expect(screen.getByText("Alex Rivera")).toBeInTheDocument();
    expect(screen.getAllByText("Team Member")).toHaveLength(2);
    const alexRow = screen.getByText("Alex Rivera").closest("tr");
    const relationship = within(alexRow!).getByText("Team Member").closest("span");
    const resourceType = within(alexRow!).getByText("Staff").closest("td");
    const systemUser = within(alexRow!).getByText("Yes").closest("td");
    expect(relationship).toHaveClass("text-info");
    expect(resourceType).toHaveClass("text-primary");
    expect(systemUser).toHaveClass("text-success");
    expect(`${relationship?.className} ${resourceType?.className} ${systemUser?.className}`).not.toMatch(/(?:^|\s)bg-/);
    expect(screen.getByText("75%")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Edit Project Relationship" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Remove from Project" })).toBeInTheDocument();
  });

  it("exposes relationship filters as an accessible group and reports the selected filter", () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    const filters = screen.getByRole("group", { name: "Project Relationship" });
    const allFilter = within(filters).getByRole("button", { name: "All" });
    const sponsorFilter = within(filters).getByRole("button", { name: "Sponsor" });
    expect(allFilter).toHaveAttribute("aria-pressed", "true");

    fireEvent.click(sponsorFilter);

    expect(sponsorFilter).toHaveAttribute("aria-pressed", "true");
    expect(allFilter).toHaveAttribute("aria-pressed", "false");
    expect(screen.queryByText("Alex Rivera")).not.toBeInTheDocument();
    expect(screen.getByText("No people assigned to this project")).toBeInTheDocument();
    expect(screen.getByText("Assign an existing Resource from Resource Management.")).toBeInTheDocument();
  });

  it("does not load project people without an allowed view permission", () => {
    fixtures.permissions = [];
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByText("Project People viewing permission is required")).toBeInTheDocument();
    expect(fixtures.peopleQuery).toHaveBeenCalledWith("project-1", false);
  });

  it("allows stakeholder readers to view people without showing management actions", () => {
    fixtures.permissions = ["Stakeholders.Read"];
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByText("Alex Rivera")).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Actions" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add person to project" })).not.toBeInTheDocument();
    expect(screen.queryByText("Project People viewing permission is required")).not.toBeInTheDocument();
    expect(fixtures.peopleQuery).toHaveBeenCalledWith("project-1", true);
  });

  it("shows a retry action when project people fail to load", () => {
    fixtures.peopleQueryError = true;
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByRole("alert")).toHaveTextContent("Unable to load project people");
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(fixtures.refetchPeople).toHaveBeenCalledOnce();
  });

  it("searches the resource directory on demand instead of limiting selection to the first page", async () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Add Person" }));
    fireEvent.click(screen.getAllByRole("combobox")[0]!);
    fireEvent.change(screen.getByPlaceholderText("Search Resource Management..."), { target: { value: "Morgan" } });

    await waitFor(() => expect(fixtures.resourceQueries).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "Morgan", page: 1, pageSize: 20, isActive: true }),
      true,
    ));
  });

  it("provides the source description and cancel action in the project-person side sheet", () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Add Person" }));

    const sheet = screen.getByRole("dialog", { name: "Add Person" });
    expect(within(sheet).getByText("Assign existing Resources to project relationships.")).toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("dialog", { name: "Add Person" })).not.toBeInTheDocument();
  });

  it("explains what removal changes before confirming it", () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Remove from Project" }));

    const dialog = screen.getByRole("dialog", { name: "Remove from Project" });
    expect(dialog).toHaveTextContent("You are removing Alex Rivera from this project.");
    expect(dialog).toHaveTextContent("Historical project records, the global Resource, and the User remain unchanged.");
    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Remove from Project" })).toHaveLength(2);
  });

  it("preserves all functional roles when editing and sends an empty selection to clear them", async () => {
    fixtures.people[0]!.functionalRoles = [
      { id: "role-primary", code: "LEAD", nameEn: "Delivery Lead", nameAr: "قائد التسليم", isPrimary: true },
      { id: "role-secondary", code: "ANALYST", nameEn: "Business Analyst", nameAr: "محلل أعمال", isPrimary: false },
    ];
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Edit Project Relationship" }));
    const primaryRole = screen.getByRole("button", { name: "Delivery Lead" });
    const secondaryRole = screen.getByRole("button", { name: "Business Analyst" });
    expect(primaryRole).toHaveAttribute("aria-pressed", "true");
    expect(secondaryRole).toHaveAttribute("aria-pressed", "true");

    fireEvent.click(primaryRole);
    fireEvent.click(secondaryRole);
    fireEvent.click(screen.getByRole("button", { name: "Save relationship" }));

    await waitFor(() => expect(fixtures.updatePerson).toHaveBeenCalledWith(expect.objectContaining({
      projectRoleIds: [],
      rowVersion: "v1",
    })));
  });

  it("localizes invitation expiry, preserves Western digits in Arabic, and exposes pending actions", () => {
    const expiresAt = "2026-10-17T10:30:00.000Z";
    fixtures.people[0]!.source = "Stakeholder";
    fixtures.people[0]!.relationshipType = "Stakeholder";
    fixtures.people[0]!.hasSystemUser = false;
    fixtures.invitation = { status: "Pending", expiresAt, invitationUrl: "https://nexus.test/invite/token" };
    window.localStorage.setItem("nexus-locale", "ar");

    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );
    fireEvent.click(screen.getByRole("button", { name: arMessages.projects.inviteStakeholder }));

    const localizedExpiry = new Intl.DateTimeFormat("ar-EG-u-nu-latn", { dateStyle: "medium", timeStyle: "short" }).format(new Date(expiresAt));
    expect(screen.getByText(localizedExpiry)).toBeInTheDocument();
    expect(screen.queryByText(expiresAt)).not.toBeInTheDocument();
    expect(screen.getByText("https://nexus.test/invite/token")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: arMessages.projects.resendInvitation })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: arMessages.projects.revokeInvitation })).toBeInTheDocument();
  });

  it("shows invitation mutation failures to the user", () => {
    fixtures.people[0]!.source = "Stakeholder";
    fixtures.people[0]!.relationshipType = "Stakeholder";
    fixtures.people[0]!.hasSystemUser = false;
    fixtures.invitationError = new Error("Invitation service is unavailable");

    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/members"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="members" element={<ProjectMembersTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Invite stakeholder" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Invitation service is unavailable");
  });
});
