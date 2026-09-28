import type { NavItem, NavSection } from "@/lib/navigation";

export function normalizePathname(pathname: string): string {
  const segments = pathname.split("/").filter(Boolean);
  if (segments.length === 0) return "/";
  return "/" + segments.slice(1).join("/");
}

export function itemIsActive(item: NavItem, pathname: string): boolean {
  if (pathname === item.href) return true;
  if (item.match !== "exact" && item.href !== "/" && pathname.startsWith(item.href + "/")) return true;
  return false;
}

export function findActiveItem(items: NavItem[], pathname: string): NavItem | null {
  let best: NavItem | null = null;
  for (const item of items) {
    if (item.items?.length) {
      const nested = findActiveItem(item.items, pathname);
      if (nested && (!best || nested.href.length > best.href.length)) best = nested;
    }
    if (itemIsActive(item, pathname) && (!best || item.href.length > best.href.length)) {
      best = item;
    }
  }
  return best;
}

export function filterItems(items: NavItem[], permissions: string[]): NavItem[] {
  return items
    .filter((item) => !item.requiredPermission || permissions.includes(item.requiredPermission))
    .map((item) => item.items?.length ? { ...item, items: filterItems(item.items, permissions) } : item);
}

export function filterSections(
  sections: NavSection[],
  permissions: string[],
): { section: NavSection; items: NavItem[] }[] {
  const visible: { section: NavSection; items: NavItem[] }[] = [];
  for (const section of sections) {
    if (section.requiredPermission && !permissions.includes(section.requiredPermission)) continue;
    const items = filterItems(section.items, permissions);
    if (items.length > 0) visible.push({ section, items });
  }
  return visible;
}
