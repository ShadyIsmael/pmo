import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { RiskDetailPage } from "../features/risks/RiskDetailPage";

const fixtures = vi.hoisted(() => ({
  permissions: ["Risk.View", "Risk.Edit", "Risk.Mitigate"] as string[],
  risk: { id: "risk-1", projectId: "project-1", projectName: "Delivery Project", title: "Supplier delay", description: "The supplier may be late.", riskLevel: "High", status: "Identified", ownerResourceId: "resource-1", ownerResourceName: "Alex Owner", createdAt: "2026-09-01T00:00:00Z", rowVersion: [1, 2] },
  actions: [{ id: "action-1", riskId: "risk-1", title: "Confirm alternate supplier", description: "Contact the backup vendor.", ownerResourceId: "resource-1", ownerResourceName: "Alex Owner", targetDate: "2026-10-20", nextFollowUpDate: "2026-10-12", status: "Planned", sortOrder: 0, createdAt: "2026-09-02T00:00:00Z", rowVersion: [3], updateCount: 0 }],
  impacts: [{ id: "impact-1", riskId: "risk-1", impactTypes: ["Schedule", "Budget"], description: "Delivery may be delayed by two weeks." }],
  updateRisk: vi.fn(),
  createAction: vi.fn(),
  updateAction: vi.fn(),
  addUpdate: vi.fn(),
  createImpact: vi.fn(),
  updateStatus: vi.fn(),
}));

vi.mock("../features/risks/api/risks", () => ({
  useRisk: () => ({ data: fixtures.risk, isLoading: false, isError: false }),
  useRiskActions: () => ({ data: fixtures.actions, isLoading: false, isError: false, isSuccess: true, isFetching: false }),
  useRiskImpacts: () => ({ data: fixtures.impacts, isLoading: false, isError: false }),
  useRiskActionUpdates: () => ({ data: [], isLoading: false, isError: false }),
  useUpdateRisk: () => ({ mutateAsync: fixtures.updateRisk, isPending: false, isError: false, reset: vi.fn() }),
  useCreateRiskAction: () => ({ mutateAsync: fixtures.createAction, isPending: false, isError: false, reset: vi.fn() }),
  useUpdateRiskAction: () => ({ mutateAsync: fixtures.updateAction, isPending: false, isError: false, reset: vi.fn() }),
  useDeleteRiskAction: () => ({ mutateAsync: vi.fn(), isPending: false, isError: false }),
  useAddRiskActionUpdate: () => ({ mutateAsync: fixtures.addUpdate, isPending: false, isError: false }),
  useCreateRiskImpact: () => ({ mutateAsync: fixtures.createImpact, isPending: false, isError: false }),
  useUpdateRiskStatus: () => ({ mutateAsync: fixtures.updateStatus, isPending: false, isError: false }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@features/resources/api/resources", () => ({ useResources: () => ({ data: { items: [{ id: "resource-1", name: "Alex Owner" }, { id: "resource-2", name: "Sam Resource" }] }, isLoading: false, isError: false }) }));
vi.mock("@features/projects/lib/project-lookups", () => ({ useUserLookup: () => ({ data: [{ id: "user-1", fullName: "Alex User" }] }) }));

function CurrentLocation() {
  const location = useLocation();
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>;
}

function renderRisk(path: string) {
  return render(<I18nProvider><MemoryRouter initialEntries={[path]}><Routes><Route path="/risks-issues/risks/:id" element={<><RiskDetailPage /><CurrentLocation /></>} /></Routes></MemoryRouter></I18nProvider>);
}

describe("risk detail parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Risk.View", "Risk.Edit", "Risk.Mitigate"];
    fixtures.updateRisk.mockReset().mockResolvedValue(fixtures.risk);
    fixtures.createAction.mockReset().mockResolvedValue({ ...fixtures.actions[0], id: "action-2", title: "New action" });
    fixtures.updateAction.mockReset().mockResolvedValue(fixtures.actions[0]);
    fixtures.addUpdate.mockReset().mockResolvedValue({});
    fixtures.createImpact.mockReset().mockResolvedValue({});
    fixtures.updateStatus.mockReset().mockResolvedValue(fixtures.risk);
  });

  it("edits a risk and sends its optimistic concurrency token", async () => {
    renderRisk("/risks-issues/risks/risk-1?from=project&projectId=project-1");

    fireEvent.click(screen.getByRole("button", { name: "Edit Risk" }));
    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Supplier delay revised" } });
    fireEvent.change(screen.getByLabelText("Description"), { target: { value: "An updated risk description." } });
    fireEvent.change(screen.getByLabelText("Risk Owner"), { target: { value: "resource-2" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(fixtures.updateRisk).toHaveBeenCalledWith({
      id: "risk-1",
      data: { title: "Supplier delay revised", description: "An updated risk description.", riskLevel: "High", ownerResourceId: "resource-2", rowVersion: [1, 2] },
    }));
  });

  it("opens the action sheet from Create Risk's save-and-add-action route and creates an action", async () => {
    renderRisk("/risks-issues/risks/risk-1?tab=actions&addAction=1&from=project&projectId=project-1");

    expect(screen.getByRole("dialog", { name: "Add action" })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Action title"), { target: { value: "Confirm alternate supplier" } });
    fireEvent.change(screen.getByLabelText("Action description"), { target: { value: "Call the backup vendor." } });
    fireEvent.change(screen.getByLabelText("Owner"), { target: { value: "resource-2" } });
    fireEvent.change(screen.getByLabelText("Action status"), { target: { value: "InProgress" } });
    fireEvent.change(screen.getByLabelText("Target date"), { target: { value: "2026-11-01" } });
    fireEvent.click(screen.getByRole("button", { name: "Save action" }));

    await waitFor(() => expect(fixtures.createAction).toHaveBeenCalledWith({ riskId: "risk-1", data: {
      title: "Confirm alternate supplier", description: "Call the backup vendor.", ownerResourceId: "resource-2", targetDate: "2026-11-01", nextFollowUpDate: undefined, status: "InProgress", sortOrder: 1,
    } }));
    expect(await screen.findByTestId("location")).toHaveTextContent("actionId=action-2");
  });

  it("only offers valid risk status transitions", () => {
    renderRisk("/risks-issues/risks/risk-1");

    expect(screen.getByRole("button", { name: "Mark mitigated" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Close risk" })).not.toBeInTheDocument();
  });

  it("keeps overview, impact, and action details together as in the Next source", () => {
    renderRisk("/risks-issues/risks/risk-1");

    expect(screen.queryByRole("tablist")).not.toBeInTheDocument();
    expect(screen.getByText("Risk overview")).toBeInTheDocument();
    expect(screen.getByText("The supplier may be late.")).toBeInTheDocument();
    expect(screen.getByText("Delivery may be delayed by two weeks.")).toBeInTheDocument();
    expect(screen.getAllByText("Confirm alternate supplier").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Updates")).toBeInTheDocument();
  });
});
