import { getApprovalReviewPath, getApprovalReviewTypeForNotification } from "@lib/approval-navigation";

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function resolveNotificationPath(referenceType?: string | null, referenceId?: string | null, referencePublicId?: string | null): string | null {
  if (!referenceType) return null;
  const approvalType = getApprovalReviewTypeForNotification(referenceType);
  if (approvalType && referenceId && GUID_PATTERN.test(referenceId)
    && !/^00000000-0000-0000-0000-000000000000$/i.test(referenceId))
    return getApprovalReviewPath(approvalType, referenceId);
  const project = referenceType.toLowerCase() === "project";
  const resolvedId = project ? referencePublicId : referenceId;
  if (!resolvedId || !GUID_PATTERN.test(resolvedId) || /^00000000-0000-0000-0000-000000000000$/i.test(resolvedId)) return null;
  const id = encodeURIComponent(resolvedId);
  switch (referenceType.toLowerCase()) {
    case "project": return `/projects/${id}`;
    case "program": return `/portfolios/programs/${id}`;
    case "portfolio": return `/portfolios/${id}`;
    default: return null;
  }
}
