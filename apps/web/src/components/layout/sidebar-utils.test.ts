import { describe, expect, it } from "vitest";
import { navigationConfig } from "@/lib/navigation";
import { administrationCategoryConfig } from "@/lib/administration-navigation";
import { Permissions } from "@/lib/permissions";
import { filterItems, findActiveItem } from "./sidebar-utils";

describe("Administration navigation", () => {
  it("exposes exactly six compact category groups", () => {
    expect(administrationCategoryConfig.map((item) => item.id)).toEqual([
      "organization",
      "access",
      "reference-data",
      "platform-settings",
      "integrations",
      "governance",
    ]);
  });

  it("exposes Administration children directly in the global side menu", () => {
    const administration = navigationConfig.find((section) => section.id === "administration");
    expect(administration?.items.map((item) => item.id)).toEqual([
      "organization",
      "access",
      "reference-data",
      "platform-settings",
      "integrations",
      "governance",
    ]);
    expect(administration?.items.find((item) => item.id === "organization")?.href).toBe("/administration/organization");
  });

  it("filters category access by the declared permissions", () => {
    const items = administrationCategoryConfig.map((category) => ({
      id: category.id,
      labelKey: category.labelKey,
      href: category.href,
      icon: category.icon,
      requiredAnyPermission: category.requiredPermissions,
    }));
    const visible = filterItems(items, [Permissions.Users.Read]);
    expect(visible.map((item) => item.id)).toEqual(["access"]);
  });

  it("hides Organization without Settings.Read", () => {
    const administration = navigationConfig.find((section) => section.id === "administration");
    const visible = filterItems(administration?.items ?? [], [Permissions.Users.Read]);
    expect(visible.some((item) => item.id === "organization")).toBe(false);
    expect(visible.some((item) => item.id === "access")).toBe(true);
  });

  it("keeps Organization active for its canonical route and descendants", () => {
    const administration = navigationConfig.find((section) => section.id === "administration");
    expect(findActiveItem(administration?.items ?? [], "/administration/organization")?.id).toBe("organization");
    expect(findActiveItem(administration?.items ?? [], "/administration/organization/hierarchy")?.id).toBe("organization");
    expect(findActiveItem(administration?.items ?? [], "/administration/organization/reporting")?.id).toBe("organization");
  });
});
