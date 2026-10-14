import { describe, expect, it } from "vitest";
import { toUserPermissionOverrides, type PermissionOverrideEffect } from "../features/administration/api/admin";

describe("user permission override payload", () => {
  it("sends only explicit allow and deny values; inherited permissions remain role-based", () => {
    const draft: Record<string, PermissionOverrideEffect> = {
      "permission-inherited": "Inherit",
      "permission-allow": "Allow",
      "permission-deny": "Deny",
    };

    expect(toUserPermissionOverrides(draft)).toEqual([
      { permissionId: "permission-allow", effect: "Allow" },
      { permissionId: "permission-deny", effect: "Deny" },
    ]);
  });
});
