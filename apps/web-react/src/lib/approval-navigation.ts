export const APPROVAL_REVIEW_TYPES = [
  "project-handover",
  "program-handover",
  "portfolio-handover",
  "change-request",
] as const;

export type ApprovalReviewType = (typeof APPROVAL_REVIEW_TYPES)[number];

export const APPROVAL_REVIEW_CONTRACTS = {
  "project-handover": {
    endpoint: "/api/handover-requests",
    approvalType: "ProjectHandover",
    businessObjectType: "Project",
  },
  "program-handover": {
    endpoint: "/api/manager-handovers/programs/requests",
    approvalType: "ProgramHandover",
    businessObjectType: "Program",
  },
  "portfolio-handover": {
    endpoint: "/api/manager-handovers/portfolios/requests",
    approvalType: "PortfolioHandover",
    businessObjectType: "Portfolio",
  },
  "change-request": {
    endpoint: "/api/project-change-requests",
    approvalType: "ChangeRequest",
    businessObjectType: "Project",
  },
} as const satisfies Record<ApprovalReviewType, {
  endpoint: string;
  approvalType: string;
  businessObjectType: string;
}>;

export function isApprovalReviewType(value?: string | null): value is ApprovalReviewType {
  return APPROVAL_REVIEW_TYPES.some((type) => type === value);
}

export function getApprovalReviewPath(type: ApprovalReviewType, approvalId: string): string {
  return `/my-work/approvals/review/${type}/${encodeURIComponent(approvalId)}`;
}

export function getAnnualBudgetProjectReviewPath(projectPublicId: string, fiscalYear: number): string | null {
  const sectionPath = getAnnualBudgetProjectSectionPath(projectPublicId);
  if (!sectionPath || !Number.isInteger(fiscalYear) || fiscalYear < 1 || fiscalYear > 9999) return null;
  return `${sectionPath}&fiscalYear=${fiscalYear}`;
}

export function getAnnualBudgetProjectSectionPath(projectPublicId: string): string | null {
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(projectPublicId)) return null;
  return `/projects/${encodeURIComponent(projectPublicId)}/financials?section=annualBudgets`;
}

export function getApprovalReviewApiPath(type: ApprovalReviewType, approvalId: string): string {
  const id = encodeURIComponent(approvalId);
  return `${APPROVAL_REVIEW_CONTRACTS[type].endpoint}/${id}/review`;
}

export function getManagerHandoverApprovalReviewType(entityType: unknown): ApprovalReviewType | null {
  switch (entityType) {
    case "Program": return "program-handover";
    case "Portfolio": return "portfolio-handover";
    default: return null;
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
