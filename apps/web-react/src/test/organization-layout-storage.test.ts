import { beforeEach, describe, expect, it } from "vitest";
import { loadOrganizationFlowPositions, organizationFlowLayoutStorageKey, saveOrganizationFlowPositions } from "../features/organization/organization-layout-storage";

describe("Organization flow layout storage", () => {
  beforeEach(() => window.localStorage.clear());

  it("scopes saved arrangements by tenant, user, and chart mode", () => {
    const userA = organizationFlowLayoutStorageKey("tenant-a", "user-a", "structure");
    const userB = organizationFlowLayoutStorageKey("tenant-a", "user-b", "structure");
    const otherTenant = organizationFlowLayoutStorageKey("tenant-b", "user-a", "structure");
    const resourcesMode = organizationFlowLayoutStorageKey("tenant-a", "user-a", "resources");
    const positions = { "unit-1": { x: 412, y: 288 } };

    saveOrganizationFlowPositions(userA, positions);

    expect(loadOrganizationFlowPositions(userA)).toEqual(positions);
    expect(loadOrganizationFlowPositions(userB)).toEqual({});
    expect(loadOrganizationFlowPositions(otherTenant)).toEqual({});
    expect(loadOrganizationFlowPositions(resourcesMode)).toEqual({});
  });

  it("ignores corrupt and non-finite saved coordinates", () => {
    const key = organizationFlowLayoutStorageKey("tenant-a", "user-a", "structure");
    window.localStorage.setItem(key, "not-json");
    expect(loadOrganizationFlowPositions(key)).toEqual({});

    window.localStorage.setItem(key, JSON.stringify({ valid: { x: 1, y: 2 }, invalid: { x: "3", y: 4 }, infinite: { x: Infinity, y: 5 } }));
    expect(loadOrganizationFlowPositions(key)).toEqual({ valid: { x: 1, y: 2 } });
  });
});
