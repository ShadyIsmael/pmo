import { describe, expect, it } from "vitest";
import ar from "@/messages/ar.json";
import en from "@/messages/en.json";
import { navigationConfig, type NavItem, type NavSection } from "@/lib/navigation";

type Messages = Record<string, unknown>;

function getMessage(messages: Messages, path: string): unknown {
  return path.split(".").reduce<unknown>((value, segment) => {
    if (!value || typeof value !== "object") return undefined;
    return (value as Record<string, unknown>)[segment];
  }, messages);
}

function collectNavMessagePaths(sections: NavSection[]): string[] {
  const paths = new Set<string>();

  function collectItem(item: NavItem) {
    paths.add(`nav.${item.labelKey}`);
    item.items?.forEach(collectItem);
  }

  sections.forEach((section) => {
    paths.add(`nav.${section.labelKey}`);
    section.items.forEach(collectItem);
  });

  return [...paths].sort();
}

const activeSidebarNavPaths = collectNavMessagePaths(navigationConfig);

describe("Sidebar navigation translations", () => {
  it("resolves nav.governance in English", () => {
    expect(getMessage(en, "nav.governance")).toBe("Governance");
  });

  it("resolves nav.governance in Arabic", () => {
    expect(getMessage(ar, "nav.governance")).toBe("الحوكمة");
  });

  it("resolves every active Sidebar navigation key in English and Arabic", () => {
    for (const path of activeSidebarNavPaths) {
      expect(getMessage(en, path), `${path} is missing in English`).toEqual(expect.any(String));
      expect(getMessage(ar, path), `${path} is missing in Arabic`).toEqual(expect.any(String));
    }
  });

  it("keeps the active Sidebar navigation key set in EN/AR parity", () => {
    const resolvedInEnglish = activeSidebarNavPaths.filter((path) => getMessage(en, path) !== undefined);
    const resolvedInArabic = activeSidebarNavPaths.filter((path) => getMessage(ar, path) !== undefined);

    expect(resolvedInEnglish).toEqual(activeSidebarNavPaths);
    expect(resolvedInArabic).toEqual(activeSidebarNavPaths);
    expect(resolvedInEnglish).toEqual(resolvedInArabic);
  });
});
