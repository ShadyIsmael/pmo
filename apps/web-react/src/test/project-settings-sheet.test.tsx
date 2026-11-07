import * as React from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectDetailPage } from "../features/projects/ProjectDetailPage";

const fixtures = vi.hoisted(() => ({
  permissions: ["Project.View", "ProjectSettings.View", "ProjectAnnualBudget.View"],
  project: {
    id: "project-internal-1",
    publicId: "project-public-1",
    tenantId: "tenant-1",
    name: "Project Atlas",
    code: "ATLAS",
    status: "Active",
    priority: "Medium",
    currency: "USD",
    progress: 0,
    health: "Green",
    ragStatus: "Green",
    confidenceLevel: "High",
    createdAt: "2026-01-01T00:00:00Z",
    createdBy: "user-1",
    isDeleted: false,
    members: [],
  },
  mutateAsync: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: fixtures.permissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjectShell: () => ({ isLoading: false, isError: false, data: fixtures.project }),
  useProjectOverview: () => ({ data: undefined }),
  useProjectHandoverCapabilities: () => ({ data: undefined }),
  useProjectHandoverRequests: () => ({ data: [] }),
  useArchiveProject: () => ({ mutateAsync: fixtures.mutateAsync }),
  useReactivateProject: () => ({ mutateAsync: fixtures.mutateAsync }),
  useDeleteProject: () => ({ mutateAsync: fixtures.mutateAsync }),
}));

vi.mock("../features/projects/ProjectSettingsTab", () => ({
  ProjectSettingsPanel: ({ projectId }: { projectId: string }) => <div>Existing settings for {projectId}</div>,
}));

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}{location.search}</output>;
}

function renderProject(initialEntry: string) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <LocationProbe />
        <Routes>
          <Route path="/projects/:id" element={<ProjectDetailPage />}>
            <Route path="overview" element={<div>Project overview</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("Project Settings Side Sheet", () => {
  beforeEach(() => {
    fixtures.permissions = ["Project.View", "ProjectSettings.View", "ProjectAnnualBudget.View"];
  });

  it("opens from the header and from a legacy deep-link query without restoring a Settings tab", async () => {
    renderProject("/projects/project-public-1/overview?openSettings=1");

    const settingsDialog = await screen.findByRole("dialog", { name: "Project settings" });
    expect(within(settingsDialog).getByText("Existing settings for project-public-1")).toBeInTheDocument();
    expect(screen.getByTestId("location")).toHaveTextContent("/projects/project-public-1/overview");
    expect(screen.queryByRole("tab", { name: "Project settings" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /workflow/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();

    await userEvent.click(within(settingsDialog).getAllByRole("button", { name: "Close" })[1]);
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Project settings" })).not.toBeInTheDocument());

    await userEvent.click(screen.getByRole("button", { name: "Project settings" }));
    expect(await screen.findByRole("dialog", { name: "Project settings" })).toBeInTheDocument();
  });
});
