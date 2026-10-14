import { describe, expect, it, vi } from "vitest";
import { buildOrganizationFlowGraph, isCurrentOrganizationMembership, organizationTypeCardTone, organizationTypeTextTone } from "../features/organization/OrganizationPage";
import type { OrganizationMembership, OrganizationUnit } from "../features/organization/api/organization";

const tenantRootId = "__tenant-root__";

function unit(id: string, parentOrganizationalUnitId: string | null, currentMemberCount = 0): OrganizationUnit {
  return {
    id,
    code: id.toUpperCase(),
    name: id,
    typeCode: "department",
    typeNameEn: "Department",
    typeNameAr: "إدارة",
    description: "",
    parentOrganizationalUnitId,
    isActive: true,
    sortOrder: 0,
    childCount: 0,
    currentMemberCount,
  };
}

describe("Organization React Flow cards", () => {
  it("keeps the Function type label legible on the light theme", () => {
    expect(organizationTypeTextTone("function")).toBe("text-chart-3");
  });

  it("uses distinct tinted card surfaces for each organizational unit type", () => {
    expect(organizationTypeCardTone("division")).toBe("border-info/50 bg-info/10");
    expect(organizationTypeCardTone("department")).toBe("border-primary/45 bg-primary/10");
    expect(organizationTypeCardTone("section")).toBe("border-warning/50 bg-warning/10");
    expect(organizationTypeCardTone("branch")).toBe("border-success/45 bg-success/10");
    expect(organizationTypeCardTone("function")).toBe("border-chart-3/55 bg-chart-3/10");
  });

  it("counts only active memberships whose effective end date is still in the future", () => {
    const now = Date.parse("2026-01-15T12:00:00.000Z");
    expect(isCurrentOrganizationMembership({ isActive: true }, now)).toBe(true);
    expect(isCurrentOrganizationMembership({ isActive: true, effectiveTo: "2026-01-16T00:00:00.000Z" }, now)).toBe(true);
    expect(isCurrentOrganizationMembership({ isActive: true, effectiveTo: "2026-01-15T12:00:00.000Z" }, now)).toBe(false);
    expect(isCurrentOrganizationMembership({ isActive: true, effectiveTo: "2026-01-14T00:00:00.000Z" }, now)).toBe(false);
    expect(isCurrentOrganizationMembership({ isActive: false, effectiveTo: "2026-01-16T00:00:00.000Z" }, now)).toBe(false);
  });

  it("preserves the hierarchy card sections and resource detail data", () => {
    const parent = unit("technology", null, 1);
    const child = unit("applications", parent.id, 1);
    const memberships: OrganizationMembership[] = [
      { id: "membership-1", resourceId: "resource-1", resourceName: "Alex Admin", organizationalUnitId: parent.id, organizationalUnitName: parent.name, isPrimary: true, effectiveFrom: "2026-01-01", isActive: true },
      { id: "membership-2", resourceId: "resource-2", resourceName: "Shady Ismael", organizationalUnitId: child.id, organizationalUnitName: child.name, isPrimary: true, effectiveFrom: "2026-01-01", isActive: true },
      { id: "membership-expired", resourceId: "resource-expired", resourceName: "Former Member", organizationalUnitId: parent.id, organizationalUnitName: parent.name, isPrimary: true, effectiveFrom: "2025-01-01", effectiveTo: new Date(Date.now() - 86_400_000).toISOString(), isActive: true },
    ];
    const byParent = new Map<string, OrganizationUnit[]>([[tenantRootId, [parent]], [parent.id, [child]]]);
    const handlers = { onToggle: vi.fn(), onAddChild: vi.fn(), onExpandBranch: vi.fn(), onCollapseBranch: vi.fn() };

    const graph = buildOrganizationFlowGraph(
      [parent, child], byParent, memberships, true, "resources", "Tenant A",
      new Set([tenantRootId]), true, handlers,
    );
    const parentCard = graph.nodes.find((node) => node.id === parent.id);
    const childCard = graph.nodes.find((node) => node.id === child.id);
    const tenantCard = graph.nodes.find((node) => node.id === tenantRootId);

    expect(parentCard?.data).toMatchObject({ directCount: 1, totalCount: 2, childCount: 1, hiddenCount: 1, resourceNames: ["Alex Admin"], displayMode: "resources" });
    expect(childCard?.data).toMatchObject({ directCount: 1, totalCount: 1, childCount: 0, resourceNames: ["Shady Ismael"] });
    expect(tenantCard?.data).toMatchObject({ tenantLabel: "Tenant A", totalCount: 2, childCount: 1 });
    expect(graph.edges.map(({ source, target }) => [source, target])).toEqual([[tenantRootId, parent.id], [parent.id, child.id]]);
  });
});
