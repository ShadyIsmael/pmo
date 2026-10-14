import { describe, expect, it } from "vitest";
import { createDashboardWidgetCopies, type DashboardWidgetSeed } from "../features/home/dashboard-view-creation";

const template: DashboardWidgetSeed[] = [
  { id: "template-1", type: "active-projects", position: { x: 0, y: 0, w: 2, h: 2 } },
];
const existing: DashboardWidgetSeed[] = [
  { id: "existing-1", type: "top-risks", position: { x: 4, y: 2, w: 3, h: 3 } },
];

describe("dashboard view starting layouts", () => {
  it("creates a blank dashboard without widgets", () => {
    expect(createDashboardWidgetCopies("blank", template, existing)).toEqual([]);
  });

  it("copies the POV template with independent widget identities and positions", () => {
    const widgets = createDashboardWidgetCopies("template", template, existing);

    expect(widgets).toHaveLength(1);
    expect(widgets[0]).toMatchObject({ type: "active-projects", position: template[0].position });
    expect(widgets[0].id).not.toBe(template[0].id);
    expect(widgets[0].position).not.toBe(template[0].position);
  });

  it("duplicates the selected dashboard layout rather than the current layout", () => {
    const widgets = createDashboardWidgetCopies("duplicate", template, existing);

    expect(widgets).toHaveLength(1);
    expect(widgets[0]).toMatchObject({ type: "top-risks", position: existing[0].position });
    expect(widgets[0].id).not.toBe(existing[0].id);
  });
});
