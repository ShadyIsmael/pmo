import type { SearchEntityType } from "@/lib/global-search";

export interface RecentEntity {
  id: string;
  type: SearchEntityType;
  name: string;
  href: string;
  code?: string;
  context?: string;
  openedAt: number;
}

const MAX_RECENT = 3;

function key(tenantId?: string) {
  return `nexus_recent_opened:${tenantId || "session"}`;
}

export function loadRecent(tenantId?: string): RecentEntity[] {
  if (typeof window === "undefined") return [];
  try {
    const value = JSON.parse(window.localStorage.getItem(key(tenantId)) || "[]") as RecentEntity[];
    return Array.isArray(value) ? value.slice(0, MAX_RECENT) : [];
  } catch { return []; }
}

export function rememberRecent(entity: Omit<RecentEntity, "openedAt">, tenantId?: string): RecentEntity[] {
  const next = [
    { ...entity, openedAt: Date.now() },
    ...loadRecent(tenantId).filter((item) => !(item.id === entity.id && item.type === entity.type)),
  ].slice(0, MAX_RECENT);
  try { window.localStorage.setItem(key(tenantId), JSON.stringify(next)); } catch { /* best effort */ }
  return next;
}

export function entityRoute(pathname: string): { type: SearchEntityType; id: string; href: string } | null {
  const parts = pathname.split("/").filter(Boolean);
  if (parts.length < 2) return null;
  if (parts[0] === "projects" && parts[1] && parts[1] !== "new") return { type: "project", id: parts[1], href: `/projects/${parts[1]}` };
  if (parts[0] === "portfolios" && parts[1] && parts[1] !== "programs") return { type: "portfolio", id: parts[1], href: `/portfolios/${parts[1]}` };
  if (parts[0] === "portfolios" && parts[1] === "programs" && parts[2]) return { type: "program", id: parts[2], href: `/portfolios/programs/${parts[2]}` };
  if (parts[0] === "resources" && parts[1] && !["capacity", "utilization", "outsourced"].includes(parts[1])) return { type: "resource", id: parts[1], href: `/resources/${parts[1]}` };
  if (parts[0] === "meetings" && parts[1]) return { type: "meeting", id: parts[1], href: `/meetings/${parts[1]}` };
  return null;
}
