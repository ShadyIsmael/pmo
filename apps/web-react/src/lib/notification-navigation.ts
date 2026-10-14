const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function resolveNotificationPath(referenceType?: string | null, referenceId?: string | null): string | null {
  if (!referenceType || !referenceId || !GUID_PATTERN.test(referenceId)) return null;
  const id = encodeURIComponent(referenceId);
  switch (referenceType.toLowerCase()) {
    case "project": return `/projects/${id}`;
    case "program": return `/portfolios/programs/${id}`;
    case "portfolio": return `/portfolios/${id}`;
    default: return null;
  }
}
