export type RiskLevel = "Low" | "Medium" | "High" | "Critical";
export type RiskStatus = "Identified" | "ActionRequired" | "FollowUp" | "Mitigated" | "Closed";
export type RiskImpactType =
  | "Schedule"
  | "Budget"
  | "Scope"
  | "Quality"
  | "Resources"
  | "Procurement"
  | "Compliance"
  | "Reputation"
  | "Operations"
  | "Other";

export interface RiskDto {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  description: string;
  riskLevel: RiskLevel;
  status: RiskStatus;
  impactTypes?: RiskImpactType[];
  lastActionUpdateText?: string;
  lastActionUpdateAt?: string;
  lastActionUpdateCreatedBy?: string;
  ownerResourceId?: string;
  ownerResourceName?: string;
  createdAt: string;
  updatedAt?: string;
  createdBy: string;
  updatedBy?: string;
  rowVersion: number[];
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
