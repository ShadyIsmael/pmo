export type OrganizationFlowPositions = Record<string, { x: number; y: number }>;
export type OrganizationDisplayMode = "structure" | "resources";

const STORAGE_PREFIX = "nexus-react-organization-flow:v1";

function storagePart(value?: string): string {
  return encodeURIComponent(value?.trim() || "unknown");
}

export function organizationFlowLayoutStorageKey(tenantId: string | undefined, userId: string | undefined, mode: OrganizationDisplayMode): string {
  return `${STORAGE_PREFIX}:${storagePart(tenantId)}:${storagePart(userId)}:${mode}`;
}

export function loadOrganizationFlowPositions(storageKey: string): OrganizationFlowPositions {
  if (typeof window === "undefined") return {};
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) return {};
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return {};
    return Object.fromEntries(Object.entries(parsed).filter((entry): entry is [string, { x: number; y: number }] => {
      const position = entry[1];
      return Boolean(position && typeof position === "object" && Number.isFinite((position as { x?: unknown }).x) && Number.isFinite((position as { y?: unknown }).y));
    }));
  } catch {
    return {};
  }
}

export function saveOrganizationFlowPositions(storageKey: string, positions: OrganizationFlowPositions): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(positions));
  } catch {
    // Layout preferences are optional; storage failures must not break the chart.
  }
}
