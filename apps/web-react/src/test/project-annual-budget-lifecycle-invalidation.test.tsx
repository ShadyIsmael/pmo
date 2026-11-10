import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  useDeleteProjectAnnualBudgetDraft,
  useProjectAnnualBudgetDraftApprovalConfiguration,
  useSaveProjectAnnualBudgetDraftApprovalConfiguration,
  useWithdrawProjectAnnualBudgetSubmission,
} from "../features/projects/api/project-annual-budgets";

const apiMock = vi.hoisted(() => ({
  delete: vi.fn(),
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock("@api/client", () => ({ api: apiMock }));

const projectId = "project/one";
const invalidatedKeys = [
  ["project-annual-budgets", projectId],
  ["project-annual-budget-approval-configuration", projectId],
  ["project-annual-budget-draft-approval-configuration", projectId],
  ["project-payment-requests", projectId],
  ["project-financial-foundation", projectId],
  ["project-annual-budget-closings", projectId],
  ["project-financial-activity", projectId],
  ["project-financial-activity-facets", projectId],
];

function LifecycleMutationHarness({ action, onSettled }: { action: "delete" | "withdraw"; onSettled: () => void }) {
  const deleteDraft = useDeleteProjectAnnualBudgetDraft(projectId);
  const withdraw = useWithdrawProjectAnnualBudgetSubmission(projectId);
  const mutate = action === "delete"
    ? () => deleteDraft.mutateAsync({ id: "budget-2026", rowVersion: "budget-version" })
    : () => withdraw.mutateAsync({ id: "budget-2026", rowVersion: "budget-version", approvalRowVersion: "approval-version" });

  return <button type="button" onClick={() => void mutate().then(onSettled)}>{action}</button>;
}

function DraftApprovalConfigurationHarness({ onLoaded, onSaved }: { onLoaded: (data: unknown) => void; onSaved: () => void }) {
  const configuration = useProjectAnnualBudgetDraftApprovalConfiguration(projectId, "budget-2026");
  const save = useSaveProjectAnnualBudgetDraftApprovalConfiguration(projectId, "budget-2026");
  React.useEffect(() => {
    if (configuration.data) onLoaded(configuration.data);
  }, [configuration.data, onLoaded]);

  return <button type="button" onClick={() => void save.mutateAsync({
    approvalMode: "Sequential",
    approverUserIds: ["reviewer-one", "reviewer-two"],
    budgetRowVersion: "budget-version",
    projectSettingsRowVersion: "settings-version",
    projectSettingsConfigurationRowVersion: "settings-config-version",
    overrideReason: "Preserve the project team order",
  }).then(onSaved)}>Save draft plan</button>;
}

function createClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

describe("Annual Budget lifecycle refresh", () => {
  beforeEach(() => {
    apiMock.delete.mockReset().mockResolvedValue(undefined);
    apiMock.get.mockReset().mockResolvedValue({ projectId: "project-1", configurationSource: "ProjectSettings" });
    apiMock.post.mockReset().mockResolvedValue({ id: "budget-2026" });
    apiMock.put.mockReset().mockResolvedValue({ projectId: "project-1", configurationSource: "DraftOverride" });
  });

  it.each(["delete", "withdraw"] as const)("invalidates all financial summary and workflow views after %s", async action => {
    const client = createClient();
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const onSettled = vi.fn();
    render(<QueryClientProvider client={client}>
      <LifecycleMutationHarness action={action} onSettled={onSettled} />
    </QueryClientProvider>);

    fireEvent.click(screen.getByRole("button", { name: action }));

    await waitFor(() => expect(onSettled).toHaveBeenCalledOnce());
    expect(invalidate.mock.calls.map(([filters]) => filters?.queryKey)).toEqual(invalidatedKeys);
    if (action === "delete") {
      expect(apiMock.delete).toHaveBeenCalledWith(
        `/api/projects/project%2Fone/annual-budgets/budget-2026`,
        { rowVersion: "budget-version" },
      );
      expect(apiMock.post).not.toHaveBeenCalled();
    } else {
      expect(apiMock.post).toHaveBeenCalledWith(
        `/api/projects/project%2Fone/annual-budgets/budget-2026/withdraw`,
        { rowVersion: "budget-version", approvalRowVersion: "approval-version" },
      );
      expect(apiMock.delete).not.toHaveBeenCalled();
    }

    client.clear();
  });

  it("loads and saves draft-scoped approval configuration, then invalidates it for reopen", async () => {
    const client = createClient();
    const invalidate = vi.spyOn(client, "invalidateQueries");
    const loaded = vi.fn();
    const saved = vi.fn();
    render(<QueryClientProvider client={client}>
      <DraftApprovalConfigurationHarness onLoaded={loaded} onSaved={saved} />
    </QueryClientProvider>);

    await waitFor(() => expect(loaded).toHaveBeenCalled());
    expect(apiMock.get).toHaveBeenCalledWith("/api/projects/project%2Fone/annual-budgets/budget-2026/approval-configuration");
    fireEvent.click(screen.getByRole("button", { name: "Save draft plan" }));

    await waitFor(() => expect(saved).toHaveBeenCalledOnce());
    expect(apiMock.put).toHaveBeenCalledWith(
      "/api/projects/project%2Fone/annual-budgets/budget-2026/approval-configuration",
      {
        approvalMode: "Sequential",
        approverUserIds: ["reviewer-one", "reviewer-two"],
        budgetRowVersion: "budget-version",
        projectSettingsRowVersion: "settings-version",
        projectSettingsConfigurationRowVersion: "settings-config-version",
        overrideReason: "Preserve the project team order",
      },
    );
    await waitFor(() => expect(invalidate.mock.calls.map(([filters]) => filters?.queryKey)).toEqual(invalidatedKeys));
    client.clear();
  });
});
