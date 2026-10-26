export const APPROVAL_REVIEW_TYPES = [
  "project-handover",
  "program-handover",
  "portfolio-handover",
  "change-request",
] as const;

export type ApprovalReviewType = (typeof APPROVAL_REVIEW_TYPES)[number];

export function isApprovalReviewType(value?: string | null): value is ApprovalReviewType {
  return APPROVAL_REVIEW_TYPES.some((type) => type === value);
}

export function getApprovalReviewPath(type: ApprovalReviewType, approvalId: string): string {
  return `/my-work/approvals/review/${type}/${encodeURIComponent(approvalId)}`;
}

export function getApprovalReviewApiPath(type: ApprovalReviewType, approvalId: string): string {
  const id = encodeURIComponent(approvalId);
  switch (type) {
    case "project-handover": return `/api/handover-requests/${id}/review`;
    case "program-handover": return `/api/manager-handovers/programs/requests/${id}/review`;
    case "portfolio-handover": return `/api/manager-handovers/portfolios/requests/${id}/review`;
    case "change-request": return `/api/project-change-requests/${id}/review`;
  }
}

export function getApprovalReviewTypeForNotification(referenceType?: string | null): ApprovalReviewType | null {
  switch (referenceType?.toLowerCase()) {
    case "projecthandoverrequest": return "project-handover";
    case "programhandoverrequest": return "program-handover";
    case "portfoliohandoverrequest": return "portfolio-handover";
    case "projectchangerequest": return "change-request";
    default: return null;
  }
}
