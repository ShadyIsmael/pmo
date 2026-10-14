import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { RisksPage } from "../features/risks/RisksPage";

const fixtures = vi.hoisted(() => ({
  permissions: ["Risk.View", "Risk.Create"] as string[],
  createRisk: vi.fn(),
  project: { id: "project-1", name: "Delivery Project", code: "PRJ-1" },
}));

vi.mock("@features/risks/api/risks", () => ({
  useRisks: () => ({ data: { items: [] }, isLoading: false, isError: false }),
  useCreateRisk: () => ({ mutateAsync: fixtures.createRisk, isPending: false, error: null }),
}));
vi.mock("@features/resources/api/resources", () => ({ useResources: () => ({ data: { items: [] }, isLoading: false, isError: false }) }));
vi.mock("@features/projects/api/projects", () => ({
  useProject: () => ({ data: fixtures.project, isLoading: false, isError: false }),
  useProjects: () => ({ data: { items: [fixtures.project] }, isLoading: false, isError: false }),
}));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));

function CurrentLocation() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}</output>;
}

function renderRegister(initialEntry = "/risks-issues/risks") {
  return render(<I18nProvider><MemoryRouter initialEntries={[initialEntry]}><><Routes><Route path="/risks-issues/risks" element={<RisksPage />} /></Routes><CurrentLocation /></></MemoryRouter></I18nProvider>);
}

describe("risk register creation", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Risk.View", "Risk.Create"];
    fixtures.createRisk.mockReset().mockResolvedValue({ id: "risk-created" });
  });

  it("opens the source-matching Add Risk side sheet without leaving the register", () => {
    renderRegister();

    fireEvent.click(screen.getByRole("button", { name: "Create Risk" }));

    const sheet = screen.getByRole("dialog", { name: "Create Risk" });
    expect(within(sheet).getByLabelText("Project")).toBeInTheDocument();
    expect(within(sheet).getByLabelText("Title")).toBeInTheDocument();
    expect(screen.getByTestId("location")).toHaveTextContent("/risks-issues/risks");
  });

  it("keeps a project-filtered register's project fixed in the creation sheet", () => {
    renderRegister("/risks-issues/risks?projectId=project-1");
    fireEvent.click(screen.getByRole("button", { name: "Create Risk" }));

    const sheet = screen.getByRole("dialog", { name: "Create Risk" });
    expect(within(sheet).getByRole("link", { name: "Delivery Project (PRJ-1)" })).toHaveAttribute("href", "/projects/project-1/risks-issues");
    expect(within(sheet).queryByRole("combobox", { name: "Project" })).not.toBeInTheDocument();
  });

  it("closes the creation sheet on Cancel without navigating away", () => {
    renderRegister();
    fireEvent.click(screen.getByRole("button", { name: "Create Risk" }));
    const sheet = screen.getByRole("dialog", { name: "Create Risk" });
    fireEvent.click(within(sheet).getByText("Cancel", { selector: "button" }));

    expect(screen.queryByRole("dialog", { name: "Create Risk" })).not.toBeInTheDocument();
    expect(screen.getByTestId("location")).toHaveTextContent("/risks-issues/risks");
  });

  it("creates a risk from the side sheet and closes it on success", async () => {
    renderRegister();

    fireEvent.click(screen.getByRole("button", { name: "Create Risk" }));
    const sheet = screen.getByRole("dialog", { name: "Create Risk" });
    fireEvent.change(within(sheet).getByLabelText("Project"), { target: { value: "project-1" } });
    fireEvent.change(within(sheet).getByLabelText("Title"), { target: { value: "Delivery dependency" } });
    fireEvent.change(within(sheet).getByLabelText("Description"), { target: { value: "Supplier milestone may slip." } });
    fireEvent.change(within(sheet).getByLabelText("Risk Level"), { target: { value: "High" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Create Risk" }));

    await waitFor(() => expect(fixtures.createRisk).toHaveBeenCalledWith(expect.objectContaining({
      projectId: "project-1",
      title: "Delivery dependency",
      description: "Supplier milestone may slip.",
      riskLevel: "High",
    })));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Create Risk" })).not.toBeInTheDocument());
    expect(screen.getByTestId("location")).toHaveTextContent("/risks-issues/risks");
  });

  it("keeps the creation entry point hidden without create permission", () => {
    fixtures.permissions = ["Risk.View"];
    render(<I18nProvider><MemoryRouter initialEntries={["/risks-issues/risks"]}><RisksPage /></MemoryRouter></I18nProvider>);

    expect(screen.queryByRole("button", { name: "Create Risk" })).not.toBeInTheDocument();
  });
});
