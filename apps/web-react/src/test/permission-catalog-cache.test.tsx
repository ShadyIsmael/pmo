import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { usePermissionCatalog, type PermissionCatalogItemDto } from "../features/administration/api/admin";

const apiMocks = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock("@api/client", () => ({ api: { get: apiMocks.get } }));

const catalogPermission = (canDelegate: boolean): PermissionCatalogItemDto => ({
  id: "project-view-id",
  name: "Project.View",
  resource: "Project",
  action: "View",
  description: "View project records.",
  moduleId: "project-management",
  moduleNameEn: "Project Management",
  moduleNameAr: "إدارة المشاريع",
  screenId: "projects",
  screenNameEn: "Projects",
  screenNameAr: "المشاريع",
  level: "View",
  labelEn: "View projects",
  labelAr: "عرض المشاريع",
  descriptionEn: "View project records.",
  descriptionAr: "عرض سجلات المشروع.",
  scope: "Project",
  canDelegate,
  sensitivity: "Low",
  isSensitive: false,
  requiredPermissionKeys: [],
  isActive: true,
  isAssignable: true,
  isMandatoryForSystemAdmin: false,
  allowedCategories: canDelegate ? ["Tenant", "Organization", "Management", "Operation", "Business"] : ["Tenant", "Organization", "Management", "Operation"],
});

function CatalogCacheProbe() {
  const [actor, setActor] = React.useState({
    id: "actor-1",
    tenantId: "tenant-1",
    permissions: ["Roles.ConfigurePermissions"],
  });
  const catalog = usePermissionCatalog(true, actor);
  const canDelegate = catalog.data?.find((permission) => permission.name === "Project.View")?.canDelegate;

  return <div>
    <output data-testid="catalog-can-delegate">{canDelegate === undefined ? "loading" : String(canDelegate)}</output>
    <button type="button" onClick={() => setActor((current) => ({
      ...current,
      permissions: [...current.permissions, "Project.View"],
    }))}>Apply refreshed permissions</button>
  </div>;
}

describe("permission catalog actor-scoped cache", () => {
  beforeEach(() => {
    apiMocks.get.mockReset();
  });

  it("refetches after refreshed actor permissions change and replaces stale delegation data", async () => {
    apiMocks.get
      .mockResolvedValueOnce([catalogPermission(false)])
      .mockResolvedValueOnce([catalogPermission(true)]);
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });

    render(
      <QueryClientProvider client={queryClient}>
        <CatalogCacheProbe />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(screen.getByTestId("catalog-can-delegate")).toHaveTextContent("false"));
    expect(apiMocks.get).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: "Apply refreshed permissions" }));

    await waitFor(() => expect(screen.getByTestId("catalog-can-delegate")).toHaveTextContent("true"));
    expect(apiMocks.get).toHaveBeenCalledTimes(2);
  });
});
