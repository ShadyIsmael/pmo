import type { PropsWithChildren } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";
import { workflowKeys, type ProjectWorkflowSettings } from "../features/workflow/api/workflows";
import { useFullWorkflowMutations, workflowKeysFull, workflowsApiFull } from "../features/workflow/api/workflowsFull";

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: PropsWithChildren) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("workflow designer query invalidation", () => {
  it("invalidates project workflow settings when a workflow version is published", async () => {
    const client = createClient();
    const versionKey = workflowKeysFull.version("version-1");
    const projectSettingsKey = workflowKeys.project("project-1");
    client.setQueryData(versionKey, { id: "version-1", lifecycleStatus: "Draft" });
    client.setQueryData<ProjectWorkflowSettings>(projectSettingsKey, { projectId: "project-1", domains: [] });
    vi.spyOn(workflowsApiFull, "publish").mockResolvedValue({ id: "version-1" } as Awaited<ReturnType<typeof workflowsApiFull.publish>>);
    const { result } = renderHook(() => useFullWorkflowMutations(), { wrapper: wrapperFor(client) });

    await act(async () => {
      await result.current.publish.mutateAsync({ id: "version-1" });
    });

    expect(client.getQueryState(versionKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(projectSettingsKey)?.isInvalidated).toBe(true);
  });
});
