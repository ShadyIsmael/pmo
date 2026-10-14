import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { OrganizationPage } from "../features/organization/OrganizationPage";
import { organizationFlowLayoutStorageKey } from "../features/organization/organization-layout-storage";

const fixtures = vi.hoisted(() => ({
  readOnly: false,
  get: vi.fn(),
  post: vi.fn(),
  update: vi.fn(),
  fitView: vi.fn(),
  units: [
    { id: "unit-1", code: "ORG-001", name: "Technology", typeCode: "department", typeNameEn: "Department", typeNameAr: "إدارة", description: "Technology group", parentOrganizationalUnitId: null, isActive: true, sortOrder: 0, childCount: 1, currentMemberCount: 1 },
    { id: "unit-2", code: "ORG-002", name: "Applications", typeCode: "section", typeNameEn: "Section", typeNameAr: "قسم", description: "", parentOrganizationalUnitId: "unit-1", parentName: "Technology", isActive: true, sortOrder: 0, childCount: 0, currentMemberCount: 0 },
    { id: "unit-3", code: "ORG-003", name: "Operations", typeCode: "department", typeNameEn: "Department", typeNameAr: "إدارة", description: "", parentOrganizationalUnitId: null, isActive: true, sortOrder: 1, childCount: 0, currentMemberCount: 0 },
  ],
  memberships: [{ id: "membership-1", resourceId: "resource-1", resourceName: "Alex", organizationalUnitId: "unit-1", organizationalUnitName: "Technology", isPrimary: true, effectiveFrom: "2026-01-01T00:00:00.000Z", effectiveTo: undefined as string | undefined, isActive: true }],
  reporting: [{ id: "line-1", resourceId: "resource-1", resourceName: "Alex", managerResourceId: "resource-2", managerResourceName: "Sam", reportingType: "LineManager", isPrimary: true, effectiveFrom: "2026-01-01T00:00:00.000Z", isActive: true }],
  resources: [{ id: "resource-1", name: "Alex" }, { id: "resource-2", name: "Sam" }],
}));

vi.mock("@xyflow/react", async () => {
  const ReactModule = await import("react");
  return {
    Background: () => null,
    Controls: () => null,
    Handle: () => null,
    MiniMap: () => null,
    Position: { Top: "top", Bottom: "bottom" },
    ReactFlow: ({ children, nodes = [], onInit, onNodeClick, onNodeDragStop, fitView, fitViewOptions }: { children?: React.ReactNode; nodes?: Array<{ id: string; position: { x: number; y: number }; style?: { width?: number; height?: number }; data: { unit?: { name: string } | null } }>; onInit?: (instance: { fitView: typeof fixtures.fitView }) => void; onNodeClick?: (event: unknown, node: { id: string; data: { unit?: { name: string } | null } }) => void; onNodeDragStop?: (event: unknown, node: { id: string; position: { x: number; y: number }; style?: { width?: number; height?: number }; data: { unit?: { name: string } | null } }) => void; fitView?: boolean; fitViewOptions?: unknown }) => {
      ReactModule.useEffect(() => {
        onInit?.({ fitView: fixtures.fitView });
        if (fitView) fixtures.fitView(fitViewOptions);
      }, []);
      return ReactModule.createElement("div", null,
      ReactModule.createElement("div", { hidden: true }, ...nodes.flatMap((node) => [
        ReactModule.createElement("button", { key: `select-${node.id}`, type: "button", onClick: () => onNodeClick?.({}, node) }, node.data.unit?.name ?? "Tenant Root"),
        node.data.unit ? ReactModule.createElement("button", { key: `drag-${node.id}`, type: "button", onClick: () => onNodeDragStop?.({}, { ...node, position: { x: 412, y: 288 } }) }, `Drag ${node.data.unit.name}`) : null,
        ReactModule.createElement("span", { key: `position-${node.id}`, role: "status", "aria-label": `Node position ${node.data.unit?.name ?? "Tenant Root"}: ${node.position.x}, ${node.position.y}` }),
      ])),
      children);
    },
    useEdgesState: (initial: unknown[]) => { const [value, setValue] = ReactModule.useState(initial); return [value, setValue, () => {}]; },
    useNodesState: (initial: unknown[]) => { const [value, setValue] = ReactModule.useState(initial); return [value, setValue, () => {}]; },
  };
});

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", tenantId: "tenant-1", permissions: ["settings.write"] } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: !fixtures.readOnly, effectiveReadOnly: fixtures.readOnly, loading: false }) }));
vi.mock("@api/client", () => ({ api: { get: fixtures.get, post: fixtures.post } }));
vi.mock("@api/endpoints", () => ({ endpoints: { organization: { unit: (id: string) => `/api/organization/units/${id}`, memberships: "/api/organization/memberships", endMembership: (id: string) => `/api/organization/memberships/${id}/end`, reportingLines: "/api/organization/reporting-lines", endReportingLine: (id: string) => `/api/organization/reporting-lines/${id}/end` } } }));
vi.mock("@features/resources/api/resources", () => ({ useResources: () => ({ data: { items: fixtures.resources }, isLoading: false, isError: false, refetch: vi.fn() }) }));
vi.mock("../features/organization/api/organization", () => ({
  organizationKeys: { all: ["organization"] },
  useOrganizationUnits: () => ({ data: fixtures.units, isLoading: false, isError: false, refetch: vi.fn() }),
  useOrganizationTypes: () => ({ data: [{ id: "type-1", code: "department", nameEn: "Department", nameAr: "إدارة", displayOrder: 1, isActive: true }] }),
  useOrganizationMemberships: () => ({ data: fixtures.memberships, isLoading: false, isError: false, isSuccess: true, refetch: vi.fn() }),
  useOrganizationReporting: () => ({ data: fixtures.reporting, isLoading: false, isError: false, refetch: vi.fn() }),
  useCreateUnit: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateUnit: () => ({ mutateAsync: fixtures.update, isPending: false }),
  useDeleteUnit: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useChangeUnitStatus: () => ({ mutate: vi.fn() }),
  useInvalidateOrganization: () => vi.fn(),
}));

function renderOrganization() {
  window.localStorage.setItem("nexus-locale", "en");
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><I18nProvider><OrganizationPage /></I18nProvider></QueryClientProvider>);
}

describe("Organization module tabs", () => {
  beforeEach(() => {
    fixtures.readOnly = false;
    window.localStorage.clear();
    fixtures.get.mockReset().mockResolvedValue({ items: [{ name: "NEXUS Tenant" }] });
    fixtures.post.mockReset().mockResolvedValue({});
    fixtures.update.mockReset().mockResolvedValue({});
    fixtures.fitView.mockReset();
  });

  it("restores the memberships and reporting screens and sends the source membership contract", async () => {
    renderOrganization();

    fireEvent.click(screen.getByRole("tab", { name: "Resource membership" }));
    expect(screen.getByRole("heading", { name: "Resource organizational membership" })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Resource"), { target: { value: "resource-2" } });
    fireEvent.click(screen.getByRole("button", { name: "Assign" }));

    await waitFor(() => expect(fixtures.post).toHaveBeenCalledWith("/api/organization/memberships", expect.objectContaining({ resourceId: "resource-2", organizationalUnitId: "unit-1", isPrimary: true, effectiveFrom: expect.any(String) })));

    fireEvent.click(screen.getByRole("tab", { name: "Reporting structure" }));
    expect(screen.getByRole("heading", { name: "Resource reporting lines" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Sam" })).toBeInTheDocument();
  });

  it("disables organization writes while viewing a read-only management perspective", () => {
    fixtures.readOnly = true;
    renderOrganization();
    fireEvent.click(screen.getByRole("tab", { name: "Resource membership" }));

    expect(screen.getByRole("button", { name: "Assign" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Add unit" })).not.toBeInTheDocument();
  });

  it("shows source-backed unit details and safely moves a unit to a different parent", async () => {
    renderOrganization();
    await waitFor(() => expect(screen.getByRole("button", { name: "Technology", hidden: true })).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: "Technology", hidden: true }));

    expect(await screen.findByText("NEXUS Tenant → Technology")).toBeInTheDocument();
    expect(screen.getByText("Technology group")).toBeInTheDocument();
    expect(screen.getByText("Applications")).toBeInTheDocument();
    expect(screen.getByText("Alex")).toBeInTheDocument();
    expect(screen.getByText("Alex → Sam")).toBeInTheDocument();

    const parent = screen.getByLabelText("New parent");
    expect(parent.querySelector('option[value="unit-2"]')).toBeNull();
    fireEvent.change(parent, { target: { value: "unit-3" } });
    fireEvent.click(screen.getByRole("button", { name: "Save hierarchy move" }));
    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({ id: "unit-1", body: expect.objectContaining({ parentOrganizationalUnitId: "unit-3" }) }));

    fireEvent.click(screen.getByRole("button", { name: "Manage resources" }));
    expect(screen.getByRole("tab", { name: "Resource membership" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByLabelText("Unit")).toHaveValue("unit-1");
  });

  it("uses the tenant as a virtual root and offers top-level unit creation", async () => {
    renderOrganization();
    fireEvent.click(await screen.findByRole("button", { name: "Tenant Root", hidden: true }));

    expect(await screen.findByRole("heading", { name: "NEXUS Tenant" })).toBeInTheDocument();
    expect(screen.getByText("The tenant is a virtual root. It is never stored as an organizational unit.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Add top-level unit" }));
    expect(screen.getByRole("heading", { name: "Create organizational unit" })).toBeInTheDocument();
  });

  it("persists a user's dragged chart layout and restores it when the screen is reopened", async () => {
    const firstRender = renderOrganization();
    fireEvent.click(await screen.findByRole("button", { name: "Drag Technology", hidden: true }));

    const storageKey = organizationFlowLayoutStorageKey("tenant-1", "user-1", "structure");
    await waitFor(() => expect(JSON.parse(window.localStorage.getItem(storageKey) ?? "{}")["unit-1"]).toEqual({ x: 412, y: 288 }));

    firstRender.unmount();
    renderOrganization();
    expect(await screen.findByRole("status", { name: "Node position Technology: 412, 288", hidden: true })).toBeInTheDocument();
  });

  it("excludes expired memberships from the resource filter", async () => {
    const originalMemberships = fixtures.memberships.slice();
    fixtures.memberships.push({
      id: "membership-expired",
      resourceId: "resource-former",
      resourceName: "Former Member",
      organizationalUnitId: "unit-3",
      organizationalUnitName: "Operations",
      isPrimary: true,
      effectiveFrom: "2025-01-01T00:00:00.000Z",
      effectiveTo: new Date(Date.now() - 86_400_000).toISOString(),
      isActive: true,
    });

    try {
      renderOrganization();
      fireEvent.click(screen.getByRole("button", { name: "Table" }));
      const resourceFilter = screen.getAllByRole("combobox")[2];
      fireEvent.change(resourceFilter, { target: { value: "with" } });

      await waitFor(() => expect(screen.getByRole("cell", { name: "Technology" })).toBeInTheDocument());
      expect(screen.queryByRole("cell", { name: "Operations" })).not.toBeInTheDocument();
    } finally {
      fixtures.memberships.splice(0, fixtures.memberships.length, ...originalMemberships);
    }
  });

  it("falls back safely when native fullscreen is denied and restores scrolling on close or Escape", async () => {
    const previousOverflow = document.body.style.overflow;
    renderOrganization();
    const canvas = await screen.findByTestId("organization-flow-canvas");
    await waitFor(() => expect(fixtures.fitView).toHaveBeenCalled());
    const initialFitCount = fixtures.fitView.mock.calls.length;

    fireEvent.click(screen.getByRole("button", { name: "Collapse all" }));
    await waitFor(() => expect(screen.queryByRole("button", { name: "Applications", hidden: true })).not.toBeInTheDocument());
    expect(fixtures.fitView).toHaveBeenCalledTimes(initialFitCount);

    const requestFullscreen = vi.fn().mockRejectedValue(new Error("Fullscreen permission denied"));
    Object.defineProperty(canvas, "requestFullscreen", { configurable: true, value: requestFullscreen });

    fireEvent.click(screen.getByRole("button", { name: "Full screen" }));
    await waitFor(() => expect(document.body.style.overflow).toBe("hidden"));
    expect(requestFullscreen).toHaveBeenCalledOnce();
    expect(canvas).toHaveClass("fixed");
    await waitFor(() => expect(fixtures.fitView).toHaveBeenCalledTimes(initialFitCount + 1));

    fireEvent.click(screen.getByRole("button", { name: "Exit full screen" }));
    await waitFor(() => expect(document.body.style.overflow).toBe(previousOverflow));

    fireEvent.click(screen.getByRole("button", { name: "Full screen" }));
    await waitFor(() => expect(document.body.style.overflow).toBe("hidden"));
    fireEvent.keyDown(window, { key: "Escape" });
    await waitFor(() => expect(document.body.style.overflow).toBe(previousOverflow));
  });
});
