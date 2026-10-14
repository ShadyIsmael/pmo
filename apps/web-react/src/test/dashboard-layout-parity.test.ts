import { describe, expect, it } from "vitest";
import { defaultDashboardWidgetsForPov } from "../features/home/DashboardBuilder";

function positionFor(pov: Parameters<typeof defaultDashboardWidgetsForPov>[0], type: string) {
  return defaultDashboardWidgetsForPov(pov).find((widget) => widget.type === type)?.position;
}

describe("default dashboard layout parity", () => {
  it("matches source compact cards in Project Manager and Team Member views", () => {
    expect(positionFor("Project Manager", "overdue-tasks")).toMatchObject({ w: 2, h: 2 });
    expect(positionFor("Project Manager", "resource-utilization")).toMatchObject({ w: 2, h: 2 });
    expect(positionFor("Team Member", "overdue-tasks")).toMatchObject({ w: 2, h: 2 });
  });

  it("uses the compact Resource Manager resource-utilization tile while management POVs retain the wide tile", () => {
    expect(positionFor("Resource Manager", "resource-utilization")).toMatchObject({ w: 2, h: 2 });
    expect(positionFor("Portfolio Manager", "resource-utilization")).toMatchObject({ w: 3, h: 3 });
  });
});
