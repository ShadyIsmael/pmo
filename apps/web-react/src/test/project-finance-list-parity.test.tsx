import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectFinanceTab } from "../features/projects/ProjectFinanceTab";

const fixtures = vi.hoisted(() => ({
  permissions: ["finances.read", "finances.write"],
  useOverview: vi.fn(),
  useProjectBudgets: vi.fn(),
  useProjectCosts: vi.fn(),
  useProjectCommitments: vi.fn(),
  useBudgets: vi.fn(),
  useCosts: vi.fn(),
  useCommitments: vi.fn(),
  useMutations: vi.fn(),
  budgetPreviewRetry: vi.fn(),
  budgetRetry: vi.fn(),
  costRetry: vi.fn(),
  commitmentRetry: vi.fn(),
  updateBudget: vi.fn(),
  updateCost: vi.fn(),
  updateCommitment: vi.fn(),
}));

vi.mock("../features/projects/api/projects", async (importOriginal) => ({
  ...await importOriginal<typeof import("../features/projects/api/projects")>(),
  useProjectOverview: fixtures.useOverview,
}));

vi.mock("../features/finance/api/finance", () => ({
  useProjectBudgets: fixtures.useProjectBudgets,
  useProjectCosts: fixtures.useProjectCosts,
  useProjectCommitments: fixtures.useProjectCommitments,
  useBudgets: fixtures.useBudgets,
  useCosts: fixtures.useCosts,
  useCommitments: fixtures.useCommitments,
  useCostManagementMutations: fixtures.useMutations,
}));

vi.mock("../features/projects/components/BudgetRevisionSection", () => ({ BudgetRevisionSection: () => null }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));

function queryResult<T>(data: T, refetch = vi.fn()) {
  return { data, isLoading: false, isError: false, refetch };
}

function mockMutationSet() {
  const mutation = { mutateAsync: vi.fn(), isPending: false };
  return {
    createBudget: mutation, updateBudget: { ...mutation, mutateAsync: fixtures.updateBudget }, deleteBudget: mutation,
    createCost: mutation, updateCost: { ...mutation, mutateAsync: fixtures.updateCost }, deleteCost: mutation,
    createCommitment: mutation, updateCommitment: { ...mutation, mutateAsync: fixtures.updateCommitment },
    closeCommitment: mutation, cancelCommitment: mutation,
  };
}

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-42", project: { id: "project-42", currency: "USD" } }} />;
}

function renderFinance() {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider><MemoryRouter initialEntries={["/projects/project-42/financials"]}><Routes><Route path="/projects/:id" element={<ProjectOutlet />}><Route path="financials" element={<ProjectFinanceTab />} /></Route></Routes></MemoryRouter></I18nProvider>);
}

describe("project financial full-list parity", () => {
  beforeEach(() => {
    fixtures.permissions = ["finances.read", "finances.write"];
    fixtures.budgetPreviewRetry.mockReset();
    fixtures.useOverview.mockReset().mockReturnValue(queryResult({
      currency: "USD", approvedBudget: 1000, allocatedBudget: 750, unallocatedBudget: 250,
      actualCost: 300, committedCost: 100, forecastAtCompletion: 900, remainingBudget: 600,
      forecastVariance: -100, budgetUtilizationPercentage: 40, originalApprovedBudget: 1200,
    }));
    fixtures.useProjectBudgets.mockReset().mockReturnValue(queryResult({
      items: [{ id: "budget-1", name: "Platform", category: "Technology", plannedAmount: 500, approvedAmount: 450, forecastAmount: 470, currency: "USD", fiscalYear: 2026, status: "Approved", rowVersion: "v1" }], totalCount: 21,
    }));
    fixtures.useProjectCosts.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 21 }));
    fixtures.useProjectCommitments.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 21 }));
    fixtures.useBudgets.mockReset().mockReturnValue(queryResult({ items: [{ id: "budget-21", name: "Cloud Services", category: "Technology", plannedAmount: 200, approvedAmount: 180, forecastAmount: 190, currency: "USD", fiscalYear: 2026, status: "Approved", rowVersion: "v1" }], totalCount: 21, totalPages: 2 }, fixtures.budgetRetry));
    fixtures.useCosts.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 21, totalPages: 2 }, fixtures.costRetry));
    fixtures.useCommitments.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 21, totalPages: 2 }, fixtures.commitmentRetry));
    fixtures.useMutations.mockReset().mockReturnValue(mockMutationSet());
    fixtures.updateBudget.mockReset().mockResolvedValue(undefined);
    fixtures.updateCost.mockReset().mockResolvedValue(undefined);
    fixtures.updateCommitment.mockReset().mockResolvedValue(undefined);
  });

  it("keeps the visible lists to five, fetches the full budget list only on demand, and pages through it", () => {
    renderFinance();

    expect(fixtures.useProjectBudgets).toHaveBeenCalledWith("project-42", true, { page: 1, pageSize: 5 });
    expect(fixtures.useProjectCosts).toHaveBeenCalledWith("project-42", true, { page: 1, pageSize: 5 });
    expect(fixtures.useProjectCommitments).toHaveBeenCalledWith("project-42", true, { page: 1, pageSize: 5 });
    expect(fixtures.useBudgets).toHaveBeenLastCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, false);
    expect(screen.getByText("Platform")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "View all (21)" })).toHaveLength(3);

    fireEvent.click(screen.getAllByRole("button", { name: "View all (21)" })[0]);
    expect(screen.getByRole("dialog", { name: "Budget Lines" })).toBeInTheDocument();
    expect(screen.getByText("Cloud Services")).toBeInTheDocument();
    expect(fixtures.useBudgets).toHaveBeenLastCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, true);

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(fixtures.useBudgets).toHaveBeenLastCalledWith({ projectId: "project-42", page: 2, pageSize: 20 }, true);
  });

  it("keeps the financial tab available and retries only the failed budget preview", () => {
    fixtures.useProjectBudgets.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: fixtures.budgetPreviewRetry });
    renderFinance();

    expect(screen.getByText("Project financial summary")).toBeInTheDocument();
    expect(screen.getByText("Actual Costs")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("Failed to load data.");

    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(fixtures.budgetPreviewRetry).toHaveBeenCalledOnce();
  });

  it("shows loading in the pending budget section without blocking other financial sections", () => {
    fixtures.useProjectBudgets.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: fixtures.budgetPreviewRetry });
    renderFinance();

    expect(screen.getByText("Project financial summary")).toBeInTheDocument();
    expect(screen.getByText("Loading...")).toBeInTheDocument();
    expect(screen.getByText("Actual Costs")).toBeInTheDocument();
  });

  it("offers retry for a failed full-list request and never shows mutation controls without finance-write access", () => {
    fixtures.permissions = ["finances.read"];
    fixtures.useBudgets.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: fixtures.budgetRetry });
    renderFinance();

    expect(screen.queryByRole("button", { name: "Add budget line" })).not.toBeInTheDocument();
    expect(screen.queryByText("You do not have permission to view project financials.")).not.toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "View all (21)" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(fixtures.budgetRetry).toHaveBeenCalledOnce();
    expect(screen.getByRole("alert")).toHaveTextContent("Failed to load data.");
    expect(screen.queryByRole("button", { name: "Edit budget line" })).not.toBeInTheDocument();
  });

  it("does not request or render project financial data without Finances.Read", () => {
    fixtures.permissions = [];
    renderFinance();

    expect(screen.getByText("You do not have permission to view project financials.")).toBeInTheDocument();
    expect(screen.queryByText("Platform")).not.toBeInTheDocument();
    expect(fixtures.useOverview).toHaveBeenCalledWith("project-42", false);
    expect(fixtures.useProjectBudgets).toHaveBeenCalledWith("project-42", false, { page: 1, pageSize: 5 });
    expect(fixtures.useProjectCosts).toHaveBeenCalledWith("project-42", false, { page: 1, pageSize: 5 });
    expect(fixtures.useProjectCommitments).toHaveBeenCalledWith("project-42", false, { page: 1, pageSize: 5 });
    expect(fixtures.useBudgets).toHaveBeenCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, false);
    expect(fixtures.useCosts).toHaveBeenCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, false);
    expect(fixtures.useCommitments).toHaveBeenCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, false);
  });

  it("opens lazy full lists for costs and commitments with project-scoped page-one queries", () => {
    renderFinance();

    const viewAllButtons = screen.getAllByRole("button", { name: "View all (21)" });
    fireEvent.click(viewAllButtons[1]);
    expect(screen.getByRole("dialog", { name: "Actual Costs" })).toBeInTheDocument();
    expect(fixtures.useCosts).toHaveBeenLastCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, true);

    fireEvent.click(screen.getAllByRole("button", { name: "Close" }).at(-1)!);
    fireEvent.click(screen.getAllByRole("button", { name: "View all (21)" })[2]);
    expect(screen.getByRole("dialog", { name: "Commitments" })).toBeInTheDocument();
    expect(fixtures.useCommitments).toHaveBeenLastCalledWith({ projectId: "project-42", page: 1, pageSize: 20 }, true);
  });

  it("matches the golden financial summary with spending position, forecast, original budget, and budget variance", () => {
    renderFinance();

    expect(screen.getByRole("img", { name: "Spending Position" })).toBeInTheDocument();
    expect(screen.getByText("Spending Position")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Forecast" })).toBeInTheDocument();
    expect(screen.getByText("Original Approved Budget")).toBeInTheDocument();
    expect(screen.getByText(/Approved − Forecast:/)).toBeInTheDocument();
  });

  it("does not fabricate authoritative financial totals from the five-row previews", () => {
    fixtures.useOverview.mockReturnValue(queryResult({ currency: "USD" }));
    fixtures.useProjectBudgets.mockReturnValue(queryResult({
      items: [{ id: "budget-1", name: "Preview only", category: "Technology", plannedAmount: 500, approvedAmount: 450, forecastAmount: 470, currency: "USD", fiscalYear: 2026, status: "Approved", rowVersion: "v1" }], totalCount: 1,
    }));
    fixtures.useProjectCosts.mockReturnValue(queryResult({
      items: [{ id: "cost-1", category: "Travel", amount: 300, currency: "USD", incurredDate: "2026-10-01", type: "Actual", status: "Posted", rowVersion: [1] }], totalCount: 1,
    }));
    renderFinance();

    expect(screen.getByTestId("finance-metric-budget-ceiling")).toHaveTextContent("—");
    expect(screen.getByTestId("finance-metric-allocated")).toHaveTextContent("—");
    expect(screen.getByTestId("finance-spending-actualSpend")).toHaveTextContent("—");
    expect(screen.getByTestId("finance-metric-forecast")).toHaveTextContent("—");
  });

  it("exposes the complete source-matching budget, cost, and commitment form fields", () => {
    renderFinance();
    fireEvent.click(screen.getByRole("button", { name: /Add budget line/i }));
    expect(screen.getByText("Line Details")).toBeInTheDocument();
    expect(screen.getByText("Amounts")).toBeInTheDocument();
    expect(screen.getByText("Financial Period")).toBeInTheDocument();
    expect(screen.getByText("Currency", { exact: true })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    fireEvent.click(screen.getByRole("button", { name: /Add cost entry/i }));
    expect(screen.getByText("Cost Details")).toBeInTheDocument();
    expect(screen.getByText("Cost Type")).toBeInTheDocument();
    expect(screen.getByText("Reference")).toBeInTheDocument();
    expect(screen.getByText("Timing / Status")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    fireEvent.click(screen.getByRole("button", { name: /Add commitment/i }));
    const commitmentSheet = screen.getByRole("dialog", { name: /Add commitment/i });
    expect(within(commitmentSheet).getByText("Commitment Details")).toBeInTheDocument();
    expect(within(commitmentSheet).getByText("Financial", { exact: true })).toBeInTheDocument();
    expect(within(commitmentSheet).getByText("Status / Timing")).toBeInTheDocument();
    expect(within(commitmentSheet).getByText("Open")).toBeInTheDocument();
  });

  it("preserves hidden cost and commitment budget links and classifications when editing", async () => {
    fixtures.useProjectCosts.mockReturnValue(queryResult({ items: [{
      id: "cost-linked", projectId: "project-42", budgetId: "budget-17", subCategory: "Travel", category: "Travel",
      description: "Original cost", amount: 25, currency: "USD", taxAmount: 2, incurredDate: "2026-10-01",
      type: "Accrual", reference: "REF-8", status: "Verified", vendorName: "Vendor", rowVersion: [1, 2],
    }], totalCount: 1 }));
    fixtures.useProjectCommitments.mockReturnValue(queryResult({ items: [{
      id: "commit-linked", projectId: "project-42", budgetId: "budget-18", purchaseOrderNumber: "PO-9",
      description: "Original commitment", amount: 80, currency: "USD", commitmentDate: "2026-10-01",
      status: "Open", rowVersion: "v9",
    }], totalCount: 1 }));
    renderFinance();

    fireEvent.click(screen.getByRole("button", { name: /Edit Cost Entry/i }));
    fireEvent.click(within(screen.getByRole("dialog", { name: /Edit Cost Entry/i })).getByRole("button", { name: "Save" }));
    await waitFor(() => expect(fixtures.updateCost).toHaveBeenCalledWith(expect.objectContaining({
      id: "cost-linked",
      body: expect.objectContaining({ budgetId: "budget-17", subCategory: "Travel", reference: "REF-8", type: "Accrual", status: "Verified", rowVersion: [1, 2] }),
    })));

    fireEvent.click(screen.getByRole("button", { name: /Edit Commitment/i }));
    fireEvent.click(within(screen.getByRole("dialog", { name: /Edit Commitment/i })).getByRole("button", { name: "Save Changes" }));
    await waitFor(() => expect(fixtures.updateCommitment).toHaveBeenCalledWith(expect.objectContaining({
      id: "commit-linked",
      body: expect.objectContaining({ budgetId: "budget-18", purchaseOrderNumber: "PO-9", rowVersion: "v9" }),
    })));
  });
});
