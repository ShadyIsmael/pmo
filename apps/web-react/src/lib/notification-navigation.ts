import { getAnnualBudgetProjectSectionPath, getApprovalReviewPath, getApprovalReviewTypeForNotification } from "@lib/approval-navigation";

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const EMPTY_GUID = /^00000000-0000-0000-0000-000000000000$/i;

function isPublicGuid(value?: string | null): value is string {
  return Boolean(value && GUID_PATTERN.test(value) && !EMPTY_GUID.test(value));
}

export function resolveNotificationPath(referenceType?: string | null, referenceId?: string | null, referencePublicId?: string | null, notificationType?: string | null): string | null {
  if (!referenceType) return null;
  const normalizedType = referenceType.toLowerCase();
  const normalizedNotificationType = notificationType?.toLowerCase();
  if (normalizedType === "project" && normalizedNotificationType === "projectannualbudgetapproval") {
    return getAnnualBudgetProjectSectionPath(referencePublicId ?? "");
  }
  if (normalizedNotificationType === "meetingminutescomment" || normalizedNotificationType === "meetingminutescommentreply") {
    if (!isPublicGuid(referenceId) || !isPublicGuid(referencePublicId)) return null;
    const anchor = normalizedNotificationType === "meetingminutescommentreply" ? "reply" : "comment";
    return `/meetings/${encodeURIComponent(referenceId)}?tab=minutes&${anchor}=${encodeURIComponent(referencePublicId)}`;
  }
  if (normalizedType === "meetingminutescomment" || normalizedType === "meetingminutescommentreply") {
    if (!isPublicGuid(referenceId) || !isPublicGuid(referencePublicId)) return null;
    const anchor = normalizedType === "meetingminutescommentreply" ? "reply" : "comment";
    return `/meetings/${encodeURIComponent(referenceId)}?tab=minutes&${anchor}=${encodeURIComponent(referencePublicId)}`;
  }
  if (normalizedNotificationType === "meetingminutespublished" && normalizedType === "meeting") {
    // The notifications API normalizes Meeting ReferenceId to its PublicId (or null).
    // ReferencePublicId is reserved for the public comment/reply target on collaboration events.
    const meetingPublicId = isPublicGuid(referencePublicId) ? referencePublicId : referenceId;
    if (!isPublicGuid(meetingPublicId)) return null;
    return `/meetings/${encodeURIComponent(meetingPublicId)}?tab=minutes`;
  }
  if (referenceType.toLowerCase() === "riskactionfollowupdue") {
    if (!isPublicGuid(referencePublicId)) return null;
    return `/risks-issues/risk-action-follow-up/${encodeURIComponent(referencePublicId)}`;
  }
  const approvalType = getApprovalReviewTypeForNotification(referenceType);
  if (approvalType && isPublicGuid(referenceId))
    return getApprovalReviewPath(approvalType, referenceId);
  const project = referenceType.toLowerCase() === "project";
  const meeting = normalizedType === "meeting";
  // For Meeting notifications, the API exposes the normalized PublicId in ReferenceId;
  // unresolved internal IDs are returned as null before this resolver sees them.
  const resolvedId = project ? referencePublicId : meeting ? (isPublicGuid(referencePublicId) ? referencePublicId : referenceId) : referenceId;
  if (!isPublicGuid(resolvedId)) return null;
  const id = encodeURIComponent(resolvedId);
  switch (referenceType.toLowerCase()) {
    case "project": return `/projects/${id}`;
    case "meeting": return `/meetings/${id}`;
    case "program": return `/portfolios/programs/${id}`;
    case "portfolio": return `/portfolios/${id}`;
    default: return null;
  }
}
