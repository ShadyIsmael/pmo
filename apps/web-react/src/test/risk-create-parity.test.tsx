import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { RiskCreatePage } from "../features/risks/RiskCreatePage";

const fixtures = vi.hoisted(() => ({
  project: { id: "project-1", code: "PRJ-001", name: "Delivery Project" },
  projects: [{ id: "project-1", code: "PRJ-001", name: "Delivery Project" }],
  resources: [{ id: "resource-1", name: "Resource Owner" }],
  permissions: ["Risk.Create"],
  createRisk: vi.fn(),
}));

vi.mock("@features/projects/api/projects", () => ({
  useProject: () => ({ data: fixtures.project, isLoading: false, isError: false }),
  useProjects: () => ({ data: { items: fixtures.projects }, isLoading: false, isError: false }),
}));

vi.mock("@features/resources/api/resources", () => ({
  useResources: () => ({ data: { items: fixtures.resources }, isLoading: false, isError: false }),
}));

vi.mock("@features/risks/api/risks", () => ({
  useCreateRisk: () => ({ mutateAsync: fixtures.createRisk, isPending: false }),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: fixtures.permissions } }),
}));

function LocationOutput() {
  const location = useLocation();
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>;
}

function renderCreate(initialEntry: string) {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/risks-issues/risks/new" element={<RiskCreatePage />} />
          <Route path="*" element={<LocationOutput />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("risk creation source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Risk.Create"];
    fixtures.createRisk.mockReset().mockResolvedValue({ id: "risk-1" });
  });

  it("creates from a project context with owner and impact assessment, then opens the action flow", async () => {
    renderCreate("/risks-issues/risks/new?projectId=project-1");

    expect(screen.getByRole("link", { name: /Delivery Project \(PRJ-001\)/ })).toHaveAttribute("href", "/projects/project-1/risks-issues");
    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Supplier delay" } });
    fireEvent.change(screen.getByLabelText("Description"), { target: { value: "A supplier may miss the delivery window." } });
    fireEvent.change(screen.getByLabelText("Risk Owner"), { target: { value: "resource-1" } });
    fireEvent.change(screen.getByLabelText("Risk Level"), { target: { value: "High" } });
    fireEvent.click(screen.getByRole("checkbox", { name: "Schedule / delay" }));
    fireEvent.change(screen.getByLabelText("Impact description"), { target: { value: "The milestone could slip by two weeks." } });
    fireEvent.click(screen.getByRole("button", { name: "Create and add action" }));

    await waitFor(() => expect(fixtures.createRisk).toHaveBeenCalledWith({
      projectId: "project-1",
      title: "Supplier delay",
      description: "A supplier may miss the delivery window.",
      riskLevel: "High",
      ownerResourceId: "resource-1",
      impactAssessment: { impactTypes: ["Schedule"], description: "The milestone could slip by two weeks." },
    }));
    expect(await screen.findByTestId("location")).toHaveTextContent("/risks-issues/risks/risk-1?tab=actions&addAction=1&from=project&projectId=project-1");
  });

  it("shows the source permission denial when the user cannot create risks", () => {
    fixtures.permissions = ["Risk.View"];

    renderCreate("/risks-issues/risks/new");

    expect(screen.getByText("You do not have permission to create risks.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Title")).not.toBeInTheDocument();
    expect(fixtures.createRisk).not.toHaveBeenCalled();
  });
});
