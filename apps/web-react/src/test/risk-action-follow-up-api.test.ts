import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ useQuery: vi.fn(), get: vi.fn() }));

vi.mock("@tanstack/react-query", () => ({ useQuery: mocks.useQuery }));
vi.mock("@api/client", () => ({ api: { get: mocks.get } }));

import { useRiskActionFollowUpTarget, type RiskActionFollowUpTargetDto } from "../features/risks/api/risks";

describe("Risk Action follow-up target query", () => {
  beforeEach(() => {
    mocks.useQuery.mockReset().mockImplementation((options) => options);
    mocks.get.mockReset().mockResolvedValue({
      projectPublicId: "project-public-id",
      riskId: "risk-internal-id",
      actionId: "action-internal-id",
    } satisfies RiskActionFollowUpTargetDto);
  });

  it("loads the scoped target by action public ID", async () => {
    const actionPublicId = "action-public-id / safe";
    useRiskActionFollowUpTarget(actionPublicId);
    const options = mocks.useQuery.mock.calls[0][0] as { enabled: boolean; queryKey: readonly unknown[]; queryFn: () => Promise<RiskActionFollowUpTargetDto> };

    expect(options.enabled).toBe(true);
    expect(options.queryKey).toEqual(["risks", "action-follow-up-target", actionPublicId]);
    await options.queryFn();
    expect(mocks.get).toHaveBeenCalledWith("/api/risk-action-follow-ups/action-public-id%20%2F%20safe/target");
  });

  it("does not enable the query without a public identifier", () => {
    useRiskActionFollowUpTarget("");
    const options = mocks.useQuery.mock.calls[0][0] as { enabled: boolean };

    expect(options.enabled).toBe(false);
  });
});
