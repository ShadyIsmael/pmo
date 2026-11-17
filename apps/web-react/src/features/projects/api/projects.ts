import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError, api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface ProjectDto {
  id: string;
  publicId: string;
  tenantId: string;
  rowVersion?: string | null;
  name: string;
  code: string;
  description?: string;
  programId?: string;
  phaseId?: string;
  phaseNameEn?: string | null;
  phaseNameAr?: string | null;
  statusId?: string;
  statusNameEn?: string | null;
  statusNameAr?: string | null;
  status: string;
  priority: string;
  projectManagerId?: string;
  projectManagerName?: string;
  sponsorId?: string;
  sponsorName?: string;
  portfolioId?: string;
  portfolioName?: string;
  departmentId?: string;
  departmentName?: string;
  startDate?: string;
  endDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  budget?: number;
  currency: string;
  actualCost?: number;
  plannedValue?: number;
  earnedValue?: number;
  progress: number;
  health: string;
  ragStatus: string;
  confidenceLevel: string;
  phase?: string;
  riskRating?: string;
  businessCase?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
  createdBy: string;
  updatedBy?: string;
  isDeleted: boolean;
  members: ProjectMemberDto[];
}

export interface ProjectSummaryDto {
  id: string;
  publicId: string;
  name: string;
  code: string;
  status: string;
  statusId?: string | null;
  statusNameEn?: string | null;
  statusNameAr?: string | null;
  reportingCategory?: "InProgress" | "Completed" | "Excluded" | null;
  attentionLevel?: "None" | "NeedsAttention" | "Critical" | null;
  phase?: string | null;
  phaseId?: string | null;
  phaseNameEn?: string | null;
  phaseNameAr?: string | null;
  health: string;
  ragStatus: string;
  progress: number;
  planProgress?: number | null;
  startDate?: string;
  endDate?: string;
  budget?: number;
  actualCost?: number;
  currency: string;
  projectManagerId?: string;
  projectManagerName?: string;
}

export interface PaymentProjectLookupDto {
  id: string;
  publicId: string;
  name: string;
  code?: string | null;
  currency?: string | null;
}

export interface ProjectMemberDto {
  id: string;
  projectId: string;
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status: string;
  isBillable: boolean;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface ProjectPersonDto {
  id: string;
  projectId: string;
  resourceId?: string | null;
  teamId?: string | null;
  teamName?: string | null;
  name: string;
  email?: string | null;
  jobTitle?: string | null;
  resourceType: string;
  userId?: string | null;
  hasSystemUser: boolean;
  source: string;
  relationshipType: "ProjectManager" | "TeamMember" | "Sponsor" | "Stakeholder" | "Other" | string;
  additionalRelationshipTypes?: string[];
  status: string;
  allocationPercentage?: number | null;
  hoursPerWeek?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  isBillable?: boolean | null;
  role?: string | null;
  interest?: string | null;
  influence?: string | null;
  isLegacy: boolean;
  calendarPolicy?: string;
  functionalRoles?: ProjectRoleRefDto[];
  rowVersion: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProjectRoleRefDto {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  isPrimary: boolean;
}

export interface ProjectTeamMemberAllocationDto {
  allocationId: string;
  isRestrictedConflict?: boolean;
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status: string;
  isBillable: boolean;
  notes?: string;
}

export interface CreateProjectPersonDto {
  resourceId?: string;
  teamId?: string;
  relationshipType: string;
  role?: string;
  allocationPercentage?: number;
  hoursPerWeek?: number;
  startDate?: string;
  endDate?: string;
  isBillable?: boolean;
  interest?: string;
  influence?: string;
  projectRoleIds?: string[];
  calendarPolicy?: string;
}

export interface UpdateProjectPersonDto {
  source: string;
  teamId?: string;
  relationshipType?: string;
  role?: string;
  allocationPercentage?: number;
  hoursPerWeek?: number;
  startDate?: string;
  endDate?: string;
  isBillable?: boolean;
  interest?: string;
  influence?: string;
  rowVersion?: string;
  projectRoleIds?: string[];
  calendarPolicy?: string;
}

export interface CreateProjectDto {
  name: string;
  code?: string;
  description?: string;
  programId?: string;
  phaseId: string;
  statusId: string;
  priority?: string;
  category?: string;
  projectManagerId?: string;
  sponsorId?: string;
  portfolioId?: string;
  departmentId?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  initialAnnualBudgetAmount?: number;
  currency?: string;
  businessCase?: string;
  notes?: string;
  confidenceLevel?: string;
}

export interface UpdateProjectDto {
  name?: string;
  code?: string;
  description?: string;
  programId?: string;
  priority?: string;
  category?: string;
  sponsorId?: string;
  clearSponsor?: boolean;
  portfolioId?: string;
  departmentId?: string;
  startDate?: string;
  endDate?: string;
  currency?: string;
  plannedValue?: number;
  earnedValue?: number;
  actualCost?: number;
  businessCase?: string;
  notes?: string;
  confidenceLevel?: string;
}

export interface ProjectOverviewRisk {
  id: string;
  title: string;
  riskLevel: string;
  status: string;
}

export interface ProjectOverviewIssue {
  id: string;
  title: string;
  status: string;
  priority: string;
  dueDate?: string;
}

export interface ProjectOverviewMilestone {
  id: string;
  name: string;
  dueDate?: string;
  completedDate?: string;
  status?: string;
  progress?: number;
}

export interface ProjectOverviewDto {
  progress?: number | null;
  health?: string | null;
  program?: { id: string; name: string; code?: string };
  risks: { openCount: number; highCriticalOpenCount: number; statusCounts: { status: string; count: number }[]; levelCounts: { riskLevel: string; count: number; displayOrder?: number }[]; topRisks: ProjectOverviewRisk[] };
  issues: {
    openIssueCount: number;
    overdueOpenIssueCount: number;
    statusCounts: { status: string; count: number; nameEn?: string; nameAr?: string; displayOrder?: number; color?: string | null }[];
    topIssues: ProjectOverviewIssue[];
  };
  milestones: ProjectOverviewMilestone[];
  currentBaseline?: { id: string; name: string; version: number; baselineDate?: string; isCurrent: boolean };
  pendingChangeRequestCount: number | null;
  pendingHandoverCount: number | null;
  allocations: { activeAllocationCount: number; activeResourceCount: number; allocatedHoursPerWeek: number | null };
  meetings: { meetingCount: number; finalMinutesCount: number };
  planProgress?: number | null;
  statusDate?: string;
  budgetAtCompletion?: number | null;
  plannedValue?: number | null;
  earnedValue?: number | null;
  actualCost?: number | null;
  currency?: string | null;
  scheduleVariance?: number | null;
  costVariance?: number | null;
  schedulePerformanceIndex?: number | null;
  costPerformanceIndex?: number | null;
  plannedValueUnavailableReason?: string | null;
  earnedValueUnavailableReason?: string | null;
  actualCostUnavailableReason?: string | null;
  schedulePerformanceIndexUnavailableReason?: string | null;
  costPerformanceIndexUnavailableReason?: string | null;
  costVarianceUnavailableReason?: string | null;
  approvedBudget?: number | null;
  allocatedBudget?: number | null;
  unallocatedBudget?: number | null;
  originalApprovedBudget?: number | null;
  revisedBudget?: number | null;
  committedCost?: number | null;
  forecastAtCompletion?: number | null;
  remainingBudget?: number | null;
  budgetUtilizationPercentage?: number | null;
  forecastVariance?: number | null;
  hasBudgetData?: boolean;
  hasCostEntryData?: boolean;
  hasCommitmentData?: boolean;
  hasCostManagementData?: boolean;
  hasPendingRevision?: boolean;
  statusReports: { totalCount: number; publishedCount: number; latestPublishedReport?: { id: string; reportDate: string; periodFrom: string; periodTo: string; reportState?: "Draft" | "Published" | string; projectLifecycleStatusAtReport?: string; overallHealth?: string; overallProgressPercent?: number; executiveSummary?: string; publishedAt?: string } };
}

export type ProjectCapacityPeriodType = "Week" | "Month" | "Quarter";

export interface ProjectTeamCapacityOverviewDto {
  startDate: string;
  endDate: string;
  activeResourceCount: number;
  idleResourceCount: number | null;
  totalCapacityHours: number | null;
  utilizedHours: number | null;
  availableHours: number | null;
  overallocatedHours: number | null;
  utilizedPercent: number | null;
  availablePercent: number | null;
  isCapacityDataAvailable: boolean;
  isAllocationDataAvailable: boolean;
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PaginationParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  [key: string]: string | number | boolean | undefined;
}

export const projectKeys = {
  all: ["projects"] as const,
  lists: () => [...projectKeys.all, "list"] as const,
  list: (params: PaginationParams) => [...projectKeys.lists(), params] as const,
  detail: (id: string) => [...projectKeys.all, "detail", id] as const,
  shell: (id: string) => [...projectKeys.all, "shell", id] as const,
  overview: (id: string) => [...projectKeys.all, "overview", id] as const,
  teamCapacity: (id: string, periodType: ProjectCapacityPeriodType, startDate: string, endDate: string) =>
    [...projectKeys.all, "team-capacity", id, periodType, startDate, endDate] as const,
  team: (projectId: string) => [...projectKeys.all, "team", projectId] as const,
  people: (projectId: string) => [...projectKeys.all, "people", projectId] as const,
  history: (projectId: string) => [...projectKeys.all, "history", projectId] as const,
  audit: (projectId: string, filters: ProjectAuditFilters) => [...projectKeys.all, "audit", projectId, filters] as const,
  auditFacets: (projectId: string) => [...projectKeys.all, "audit-facets", projectId] as const,
  snapshots: (projectId: string) => [...projectKeys.all, "snapshots", projectId] as const,
  snapshot: (projectId: string, snapshotId: string) => [...projectKeys.all, "snapshot", projectId, snapshotId] as const,
  handovers: (projectId: string) => [...projectKeys.all, "handovers", projectId] as const,
  handoverManagers: (projectId: string) => [...projectKeys.all, "handover-managers", projectId] as const,
  changeRequests: (projectId: string) => [...projectKeys.all, "change-requests", projectId] as const,
  statusReports: (projectId: string) => [...projectKeys.all, "status-reports", projectId] as const,
  statusReport: (projectId: string, reportId: string) => [...projectKeys.all, "status-report", projectId, reportId] as const,
  reportingConfiguration: (projectId: string, effectiveAt?: string) => [...projectKeys.all, "reporting-configuration", projectId, effectiveAt] as const,
  reportingOptions: (projectId: string) => [...projectKeys.all, "reporting-options", projectId] as const,
  performance: (projectId: string, params: ProjectPerformanceParams) => [...projectKeys.all, "performance", projectId, params] as const,
  financialSummary: (projectId: string, periodFrom: string, periodTo: string) => [...projectKeys.all, "financial-summary", projectId, periodFrom, periodTo] as const,
  conflicts: (projectId: string, resourceId: string, startDate: string, endDate: string, allocationPercentage: number, excludeMemberId?: string) =>
    [...projectKeys.all, "conflicts", projectId, resourceId, startDate, endDate, allocationPercentage, excludeMemberId] as const,
};

export interface ProjectManagerHistoryDto {
  id: string;
  projectId: string;
  projectManagerId: string;
  projectManagerName: string;
  projectManagerEmail?: string;
  startDate: string;
  endDate?: string;
  isCurrent: boolean;
  handoverFromProjectManagerName?: string;
  handoverDate?: string;
  handoverReason?: string;
  handoverNotes?: string;
  createdAt: string;
}

export interface ProjectSnapshotSummaryDto {
  id: string;
  projectId: string;
  snapshotNumber: number;
  snapshotName: string;
  snapshotType: string;
  snapshotDate: string;
  effectiveEndDate: string;
  previousProjectManagerName?: string;
  newProjectManagerName?: string;
  handoverReason?: string;
  handoverNotes?: string;
  createdAt: string;
  createdByName?: string;
}

export interface ProjectSnapshotCollectionItem {
  originalId: string;
  name?: string | null;
  title?: string | null;
  summary?: string | null;
  accomplishments?: string | null;
  nextSteps?: string | null;
  blockers?: string | null;
  description?: string | null;
  status?: string | null;
  role?: string | null;
  resourceName?: string | null;
  resourceJobTitle?: string | null;
  assigneeName?: string | null;
  assignedResourceName?: string | null;
  assignedTeamName?: string | null;
  ownerName?: string | null;
  reportedByName?: string | null;
  organizerName?: string | null;
  category?: string | null;
  subCategory?: string | null;
  type?: string | null;
  priority?: string | null;
  severity?: string | null;
  riskLevel?: string | null;
  interest?: string | null;
  influence?: string | null;
  email?: string | null;
  notes?: string | null;
  deliverable?: string | null;
  qualityCriteria?: string | null;
  riskResponse?: string | null;
  resolution?: string | null;
  fileName?: string | null;
  fileType?: string | null;
  tags?: string | null;
  vendorName?: string | null;
  reference?: string | null;
  currency?: string | null;
  meetingType?: string | null;
  recurrence?: string | null;
  code?: string | null;
  amount?: number | null;
  approvedAmount?: number | null;
  plannedAmount?: number | null;
  actualAmount?: number | null;
  committedAmount?: number | null;
  forecastAmount?: number | null;
  potentialCost?: number | null;
  potentialDelay?: number | null;
  scheduleVariance?: number | null;
  costVariance?: number | null;
  allocationPercentage?: number | null;
  hoursPerWeek?: number | null;
  plannedHours?: number | null;
  actualHours?: number | null;
  estimatedHours?: number | null;
  storyPoints?: number | null;
  progress?: number | null;
  version?: number | null;
  fiscalYear?: number | null;
  fiscalQuarter?: number | null;
  totalDays?: number | null;
  remainingDays?: number | null;
  fileSize?: number | null;
  sortOrder?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  dueDate?: string | null;
  completedDate?: string | null;
  deliveredDate?: string | null;
  reportedAt?: string | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  identifiedDate?: string | null;
  targetResolutionDate?: string | null;
  resolvedDate?: string | null;
  closedDate?: string | null;
  incurredDate?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  baselineDate?: string | null;
  baselineStartDate?: string | null;
  baselineEndDate?: string | null;
  forecastStartDate?: string | null;
  forecastEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  completedAt?: string | null;
  isCurrent?: boolean | null;
  isBillable?: boolean | null;
  [key: string]: string | number | boolean | null | undefined;
}

export interface ProjectSnapshotDataDto {
  members: ProjectSnapshotCollectionItem[];
  allocations: ProjectSnapshotCollectionItem[];
  phases: ProjectSnapshotCollectionItem[];
  milestones: ProjectSnapshotCollectionItem[];
  deliverables: ProjectSnapshotCollectionItem[];
  stakeholders: ProjectSnapshotCollectionItem[];
  schedules: ProjectSnapshotCollectionItem[];
  wbsItems: ProjectSnapshotCollectionItem[];
  baselines: ProjectSnapshotCollectionItem[];
  statusReports: ProjectSnapshotCollectionItem[];
  workItems: ProjectSnapshotCollectionItem[];
  risks: ProjectSnapshotCollectionItem[];
  issues: ProjectSnapshotCollectionItem[];
  budgets: ProjectSnapshotCollectionItem[];
  costEntries: ProjectSnapshotCollectionItem[];
  meetings: ProjectSnapshotCollectionItem[];
  documents: ProjectSnapshotCollectionItem[];
}

export interface ProjectSnapshotDto extends ProjectSnapshotSummaryDto {
  name: string;
  code: string;
  description?: string;
  category?: string;
  programName?: string;
  portfolioName?: string;
  departmentName?: string;
  sponsorName?: string;
  status: string;
  priority: string;
  phase?: string;
  health: string;
  confidenceLevel: string;
  riskRating?: string;
  startDate?: string;
  endDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  budget?: number;
  currency: string;
  actualCost?: number;
  plannedValue?: number;
  earnedValue?: number;
  costVariance?: number;
  scheduleVariance?: number;
  progress: number;
  businessCase?: string;
  notes?: string;
  ragStatus?: string;
  data?: ProjectSnapshotDataDto;
}

export interface ProjectHandoverRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  projectCode: string;
  currentProjectManagerId: string | null;
  currentProjectManagerName: string;
  newProjectManagerId: string;
  newProjectManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
  status: string;
  initiationType: "ManagerInitiated" | "AdminInitiated" | "InitialAssignment";
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
  snapshotId?: string;
  snapshotName?: string;
  createdAt: string;
}

export interface ProjectStatusReportListDto {
  id: string;
  projectId: string;
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  statusDate: string;
  reportState: "Draft" | "Published" | string;
  projectLifecycleStatusAtReport?: string;
  overallHealth?: string;
  overallProgressPercent?: number;
  preparedByNameSnapshot?: string;
  publishedAt?: string;
}

export interface ProjectStatusReportDto extends ProjectStatusReportListDto {
  projectNameSnapshot?: string;
  projectCodeSnapshot?: string;
  projectManagerIdAtReport?: string;
  projectManagerNameSnapshot?: string;
  scheduleHealth?: string;
  costHealth?: string;
  scopeHealth?: string;
  qualityHealth?: string;
  resourceHealth?: string;
  executiveSummary?: string;
  keyAchievements?: string;
  challengesConcerns?: string;
  nextPeriodPriorities?: string;
  actionsNeeded?: string;
  supportNeeded?: string;
  sourceDataCapturedAt?: string;
  reportingConfigurationVersionSnapshot?: number;
  includedSectionsSnapshot?: string[];
  performanceSnapshot?: ProjectPerformanceDto;
  preparedByUserId?: string;
  publishedByUserId?: string;
  publishedByNameSnapshot?: string;
  preparedByName?: string;
  currencyCodeSnapshot?: string;
  budgetAmountSnapshot?: number;
  actualCostAmountSnapshot?: number;
  committedCostAmountSnapshot?: number;
  forecastCostAmountSnapshot?: number;
  costVarianceAmountSnapshot?: number;
  scheduleVarianceDaysSnapshot?: number;
  scheduleVariancePercentSnapshot?: number;
  baselineIdAtReport?: string;
  baselineNameSnapshot?: string;
  baselineVersionSnapshot?: number;
  actualPaidSnapshot?: number;
  outstandingPlannedPaymentsSnapshot?: number;
  paymentsDueWithinPeriodSnapshot?: number;
  remainingUnpaidAtPeriodEndSnapshot?: number;
  risks: ProjectStatusReportRiskSnapshotDto[];
  issues: ProjectStatusReportIssueSnapshotDto[];
  changeRequests: ProjectStatusReportChangeRequestSnapshotDto[];
  rowVersion?: string;
}

export interface ProjectStatusReportRiskSnapshotDto { sourceRiskId: string; rank: number; title: string; description?: string; impact?: string; likelihood?: string; riskLevel: string; riskStatus: string; potentialCost?: number; potentialDelay?: number; riskResponse?: string; ownerId?: string; ownerNameSnapshot?: string; }
export interface ProjectStatusReportIssueSnapshotDto { sourceIssueId: string; rank: number; title: string; description?: string; severity: string; priority: string; issueStatus: string; assigneeId?: string; assigneeNameSnapshot?: string; resolution?: string; }
export interface ProjectStatusReportChangeRequestSnapshotDto { sourceChangeRequestId: string; rank: number; number: number; changeType: string; reason: string; justification?: string; impact?: string; changeRequestStatus: string; requestedBy: string; requestedByNameSnapshot?: string; requestedAt: string; reviewedAt?: string; decisionNotes?: string; }

export interface ProjectStatusReportComparisonDto {
  earlierReportId: string;
  laterReportId: string;
  earlierStatusDate: string;
  laterStatusDate: string;
  reportingConfigurationChanged: boolean;
  includedPlansChanged: boolean;
  reportingBaselinesChanged: boolean;
  metrics: { code: string; earlierValue?: number | null; laterValue?: number | null; change?: number | null }[];
  milestoneChanges: { planId: string; planName: string; name: string; earlierStatus?: string | null; laterStatus?: string | null; earlierPlannedDate?: string | null; laterPlannedDate?: string | null; earlierActualCompletionDate?: string | null; laterActualCompletionDate?: string | null; earlierVarianceDays?: number | null; laterVarianceDays?: number | null }[];
  riskTrend: { earlierTotal: number; laterTotal: number; earlierOpen: number; laterOpen: number; earlierHighOrCriticalOpen: number; laterHighOrCriticalOpen: number };
  issueTrend: { earlierTotal: number; laterTotal: number; earlierOpen: number; laterOpen: number; earlierHighOrCriticalOpen: number; laterHighOrCriticalOpen: number };
}

export interface ProjectStatusReportInput {
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  statusDate: string;
  projectLifecycleStatusAtReport?: string;
  overallHealth?: string;
  overallProgressPercent?: number;
  scheduleHealth?: string;
  costHealth?: string;
  scopeHealth?: string;
  qualityHealth?: string;
  resourceHealth?: string;
  executiveSummary?: string;
  keyAchievements?: string;
  challengesConcerns?: string;
  nextPeriodPriorities?: string;
  actionsNeeded?: string;
  supportNeeded?: string;
  rowVersion?: string;
}

export interface ProjectReportingPlanSelectionInput {
  planId: string;
  workingVersionId: string;
  baselineVersionId: string;
  includeInConsolidatedReporting: boolean;
}

export interface SaveProjectReportingConfigurationInput {
  effectiveFrom: string;
  reportingFrequency: "Weekly";
  timeZoneId: string;
  currencyCode: string;
  includedSections: string[];
  plans: ProjectReportingPlanSelectionInput[];
}

export interface ProjectReportingPlanOptionDto {
  planId: string;
  name: string;
  planType: string;
  classification: "Execution" | "WhatIf" | "NonExecution" | string;
  status: string;
  currentWorkingVersionId?: string;
  currentWorkingVersionNumber?: number;
  versions: { id: string; versionNumber: number; name: string; type: string; status: string; createdAt: string; eligibleForReporting: boolean }[];
}

export interface ProjectReportingConfigurationDto {
  id: string;
  projectId: string;
  version: number;
  effectiveFrom: string;
  effectiveTo?: string;
  reportingFrequency: string;
  timeZoneId: string;
  currencyCode: string;
  status: string;
  isCurrent: boolean;
  includedSections: string[];
  configuredBy: string;
  configuredAt: string;
  plans: { planId: string; workingVersionId: string; workingVersionNumber: number; baselineVersionId: string; baselineVersionNumber: number; baselineName: string; planName: string; planType: string; planStatus: string; includeInConsolidatedReporting: boolean }[];
}

export interface ProjectPerformanceParams {
  statusDate?: string;
  perspective?: "Consolidated" | "Individual" | "Compare";
  planId?: string;
  comparePlanId?: string;
}

export interface ProjectPerformanceDto {
  projectId: string;
  statusDate: string;
  perspective: string;
  dataQuality: "Complete" | "Partial" | "Unavailable" | string;
  reportingConfigurationVersion?: number;
  includedReportSections?: string[];
  currency?: string;
  overallProgressPercent?: number;
  plannedProgressPercent?: number;
  budgetAtCompletion?: number;
  plannedValue?: number;
  earnedValue?: number;
  actualCost?: number;
  planAttributedActualCost?: number;
  unallocatedProjectActualCost?: number;
  scheduleVariance?: number;
  costVariance?: number;
  schedulePerformanceIndex?: number;
  costPerformanceIndex?: number;
  plannedValueUnavailableReason?: string;
  earnedValueUnavailableReason?: string;
  actualCostUnavailableReason?: string;
  schedulePerformanceIndexUnavailableReason?: string;
  costPerformanceIndexUnavailableReason?: string;
  costVarianceUnavailableReason?: string;
  dataQualityReasons: string[];
  plans: ProjectPlanPerformanceDto[];
  availablePlans: ProjectReportingPlanOptionDto[];
  milestones: ProjectPerformanceMilestoneDto[];
  criticalPathMode: string;
  criticalPathUnavailableReason?: string;
}

export interface ProjectFinancialSummaryDto {
  projectId: string;
  periodFrom: string;
  periodTo: string;
  currencyCode?: string | null;
  approvedBudget?: number | null;
  budgetStatus: string;
  expenditure?: number | null;
  committedCost?: number | null;
  actualPaid?: number | null;
  outstandingPlannedPayments?: number | null;
  paymentsDueWithinPeriod?: number | null;
  remainingUnpaidAtPeriodEnd?: number | null;
  hasFinancialData: boolean;
  dataStatus: string;
}

export interface ProjectPlanPerformanceDto {
  planId: string;
  planName: string;
  planType: string;
  classification: string;
  planStatus: string;
  includedInOfficialReporting: boolean;
  workingVersionId?: string;
  workingVersionNumber?: number;
  baselineVersionId?: string;
  baselineName?: string;
  baselineVersionNumber?: number;
  plannedProgressPercent?: number;
  actualProgressPercent?: number;
  budgetAtCompletion?: number;
  plannedValue?: number;
  earnedValue?: number;
  actualCost?: number;
  scheduleVariance?: number;
  costVariance?: number;
  schedulePerformanceIndex?: number;
  costPerformanceIndex?: number;
  costPerformanceIndexUnavailableReason?: string;
  dataQualityReasons: string[];
}

export interface ProjectPerformanceMilestoneDto {
  planId: string;
  planName: string;
  name: string;
  baselineDate?: string;
  plannedDate?: string;
  actualCompletionDate?: string;
  varianceDays?: number;
  status: string;
}

export interface ProjectChangeRequestDto {
  id: string;
  projectId: string;
  projectPublicId?: string | null;
  projectName: string;
  number: number;
  type: "Administrative" | "Baseline";
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetBeforeChange?: number | null;
  budgetChangeAmount?: number | null;
  proposedProjectBudget?: number | null;
  attachments?: string | null;
  status: "Draft" | "Pending" | "Approved" | "Rejected" | "Cancelled";
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  reviewedBy?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  decisionNotes?: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
  approvalStrategy: "AllRequired" | "AnyOneOptional" | "Sequential" | string;
  submittedAt?: string | null;
  workflowInstanceId?: string | null;
  selectedApproverUserIds: string[];
  pendingWith: string[];
  approvalCount: number;
  requiredApprovalCount: number;
  requiresApprovalSetup: boolean;
  approvals: ProjectChangeRequestApprovalDto[];
  rowVersion: string | number[];
}
export type ProjectChangeRequestRowVersion = string | number[];

export interface ProjectChangeRequestApprovalDto {
  id: string;
  approverUserId: string;
  approverName: string;
  originalApproverUserId: string;
  originalApproverName: string;
  projectRole: string;
  isSponsor: boolean;
  isMandatory: boolean;
  sequence: number;
  status: string;
  decidedAt?: string | null;
  decisionActorId?: string | null;
  decisionComment?: string | null;
  reassignmentHistory: ProjectChangeRequestApprovalHistoryDto[];
}

export interface ProjectChangeRequestApprovalHistoryDto {
  fromApproverUserId: string;
  fromApproverName: string;
  toApproverUserId: string;
  toApproverName: string;
  changedByUserId: string;
  changedByName: string;
  reason: string;
  changedAt: string;
}

export interface ProjectChangeRequestApproverDto { userId: string; fullName: string; projectRole: string; isSponsor: boolean; isMandatory: boolean; }
export interface ProjectChangeRequestApprovalOptionsDto { approvers: ProjectChangeRequestApproverDto[]; allowedStrategies: string[]; sponsorUserId?: string | null; policyMessage?: string | null; }

export interface CreateProjectChangeRequestDto {
  projectId: string;
  type: "Administrative" | "Baseline";
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetChangeAmount?: number | null;
  attachments?: string | null;
  approvalStrategy: string;
  approverUserIds: string[];
}

function invalidateProjectPeople(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
  queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true });
  queryClient.invalidateQueries({ queryKey: projectKeys.team(projectId) });
}

export function useProjects(params: PaginationParams & { status?: string; priority?: string; portfolioId?: string } = {}, enabled = true) {
  return useQuery({
    queryKey: projectKeys.list(params),
    queryFn: () => api.get<PagedResult<ProjectSummaryDto>>("/api/projects", params),
    staleTime: 30_000,
    enabled,
  });
}

export function usePaymentProjectLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "payment-obligations", "projects"],
    queryFn: () => api.get<PaymentProjectLookupDto[]>(endpoints.projects.paymentObligationLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useProject(id: string) {
  return useQuery({
    queryKey: projectKeys.detail(id),
    queryFn: () => api.get<ProjectDto>(`/api/projects/${id}`),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useProjectShell(id: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.shell(id),
    queryFn: () => api.get<ProjectDto>(`/api/projects/${id}/shell`),
    enabled: !!id && enabled,
    staleTime: 30_000,
  });
}

export function useProjectOverview(id: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.overview(id),
    queryFn: () => api.get<ProjectOverviewDto>(`/api/projects/${id}/overview`),
    enabled: !!id && enabled,
    staleTime: 30_000,
  });
}

export function useProjectTeamCapacity(
  id: string,
  periodType: ProjectCapacityPeriodType,
  startDate: string,
  endDate: string,
  enabled = true,
) {
  return useQuery({
    queryKey: projectKeys.teamCapacity(id, periodType, startDate, endDate),
    queryFn: () => api.get<ProjectTeamCapacityOverviewDto>(
      `/api/projects/${id}/overview/team-capacity?periodType=${periodType}&startDate=${startDate}&endDate=${endDate}`,
    ),
    enabled: Boolean(id && startDate && endDate && enabled),
    staleTime: 30_000,
    retry: false,
    retryOnMount: false,
  });
}

export function useProjectTeam(projectId: string) {
  return useQuery({
    queryKey: projectKeys.team(projectId),
    queryFn: () => api.get<ProjectMemberDto[]>(`/api/projects/${projectId}/team`),
    enabled: !!projectId,
    staleTime: 30_000,
  });
}

export function useProjectPeople(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.people(projectId),
    queryFn: () => api.get<ProjectPersonDto[]>(`/api/projects/${projectId}/people`),
    enabled: !!projectId && enabled,
    staleTime: 30_000,
  });
}

export function useCheckResourceConflicts(
  projectId: string,
  resourceId: string,
  startDate: string,
  endDate: string,
  allocationPercentage: number,
  excludeMemberId?: string,
  enabled = true,
) {
  return useQuery({
    queryKey: projectKeys.conflicts(projectId, resourceId, startDate, endDate, allocationPercentage, excludeMemberId),
    queryFn: () => api.get<ProjectTeamMemberAllocationDto[]>(`/api/projects/${projectId}/team/conflicts`, {
      resourceId,
      startDate,
      endDate,
      allocationPercentage,
      excludeMemberId,
    }),
    enabled: enabled && Boolean(projectId && resourceId && startDate && endDate && allocationPercentage > 0),
    staleTime: 10_000,
  });
}

export function useProjectManagerHistory(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.history(projectId),
    queryFn: () => api.get<ProjectManagerHistoryDto[]>(`/api/projects/${projectId}/manager-history`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

/** Audit actor source. System-generated changes must stay distinguishable from human ones. */
export type ProjectAuditSource = "User" | "System" | "Integration" | "BackgroundJob" | "Unknown";

export interface ProjectAuditFieldChangeDto {
  from?: string | null;
  to?: string | null;
}

export interface ProjectAuditEventDto {
  id: string;
  projectId?: string | null;
  entityType: string;
  module: string;
  entityId: string;
  action: string;
  actorId?: string | null;
  actorName?: string | null;
  source: ProjectAuditSource;
  timestampUtc: string;
  description?: string | null;
  correlationId?: string | null;
  changedFields: string[];
  changes: Record<string, ProjectAuditFieldChangeDto>;
  before?: string | null;
  after?: string | null;
  detailsRestricted: boolean;
}

export interface ProjectAuditTrailDto {
  items: ProjectAuditEventDto[];
  page: number;
  pageSize: number;
  total: number;
  hasMore: boolean;
  includeFinancialDetail: boolean;
  includeGovernanceDetail: boolean;
  projectPublicId: string;
  projectName: string;
}

export interface ProjectAuditActorDto {
  id: string;
  name: string;
}

export interface ProjectAuditFacetsDto {
  modules: string[];
  actions: string[];
  actors: ProjectAuditActorDto[];
  entityTypes: string[];
  coverageStartUtc?: string | null;
  includeFinancialDetail: boolean;
  includeGovernanceDetail: boolean;
  projectPublicId: string;
  projectName: string;
}

export interface ProjectAuditFilters {
  page: number;
  pageSize: number;
  from?: string;
  to?: string;
  module?: string;
  action?: string;
  actorId?: string;
  entityType?: string;
  search?: string;
  sort?: "newest" | "oldest";
}

function auditQueryParams(filters: ProjectAuditFilters): Record<string, string | number | boolean | undefined> {
  return {
    page: filters.page,
    pageSize: filters.pageSize,
    from: filters.from || undefined,
    to: filters.to || undefined,
    module: filters.module || undefined,
    action: filters.action || undefined,
    actorId: filters.actorId || undefined,
    entityType: filters.entityType || undefined,
    search: filters.search?.trim() || undefined,
    sort: filters.sort,
  };
}

export function useProjectAuditTrail(projectId: string, filters: ProjectAuditFilters, enabled = true) {
  return useQuery({
    queryKey: projectKeys.audit(projectId, filters),
    queryFn: () => api.get<ProjectAuditTrailDto>(endpoints.projects.audit(projectId), auditQueryParams(filters)),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useProjectAuditFacets(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.auditFacets(projectId),
    queryFn: () => api.get<ProjectAuditFacetsDto>(endpoints.projects.auditFacets(projectId)),
    enabled: Boolean(projectId) && enabled,
    staleTime: 60_000,
  });
}

export function useProjectSnapshots(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.snapshots(projectId),
    queryFn: () => api.get<ProjectSnapshotSummaryDto[]>(`/api/projects/${projectId}/snapshots`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectSnapshot(projectId: string, snapshotId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.snapshot(projectId, snapshotId),
    queryFn: () => api.get<ProjectSnapshotDto>(`/api/projects/${projectId}/snapshots/${encodeURIComponent(snapshotId)}`),
    enabled: Boolean(projectId && snapshotId) && enabled,
    staleTime: 60_000,
  });
}

export interface ProjectHandoverManagerDto {
  id: string;
  fullName: string;
  email: string;
  jobTitle?: string;
  managerTypes: Array<"ProjectManager" | "PortfolioManager" | "ProgramManager">;
}

export interface ProjectHandoverCapabilitiesDto {
  currentManagerId: string | null;
  currentManagerName?: string | null;
  canViewRequests: boolean;
  canInitiate: boolean;
  canAdminInitiate: boolean;
  canAssignInitial: boolean;
  canApproveIncoming: boolean;
  canCancelOwnPending: boolean;
  hasPendingRequest: boolean;
}

export type ProjectHandoverInitiationType = "ManagerInitiated" | "AdminInitiated" | "InitialAssignment";

export function shouldRetryHandoverQuery(failureCount: number, error: Error): boolean {
  if (error instanceof ApiError && [401, 403, 404].includes(error.status)) return false;
  return failureCount < 1;
}

export function useProjectHandoverManagers(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.handoverManagers(projectId),
    queryFn: () => api.get<ProjectHandoverManagerDto[]>(endpoints.projects.handoverManagers(projectId)),
    enabled: Boolean(projectId) && enabled,
    staleTime: 60_000,
    retry: shouldRetryHandoverQuery,
  });
}

export function useProjectHandoverCapabilities(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.all, "handover-capabilities", projectId],
    queryFn: () => api.get<ProjectHandoverCapabilitiesDto>(`/api/projects/${projectId}/handover-capabilities`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
    retry: shouldRetryHandoverQuery,
  });
}

export function useProjectHandoverRequests(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.handovers(projectId),
    queryFn: () => api.get<ProjectHandoverRequestDto[]>(`/api/projects/${projectId}/handover-requests`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
    retry: shouldRetryHandoverQuery,
  });
}

export function useCreateProjectHandoverRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ initiationType, ...data }: { initiationType: ProjectHandoverInitiationType; newProjectManagerId: string; effectiveHandoverDate: string; reason: string; notes?: string }) => {
      const route = initiationType === "ManagerInitiated" ? "" : initiationType === "AdminInitiated" ? "/admin" : "/initial-assignment";
      return api.post<ProjectHandoverRequestDto>(`/api/projects/${projectId}/handover-requests${route}`, data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(projectId) });
      queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "handover-capabilities", projectId] });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) });
    },
  });
}

export function useCancelProjectHandoverRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ProjectHandoverRequestDto>(`/api/handover-requests/${requestId}/cancel`, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(projectId) });
      queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "handover-capabilities", projectId] });
    },
  });
}

export function useProjectStatusReports(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId)],
    queryFn: () => api.get<PagedResult<ProjectStatusReportListDto>>(`/api/projects/${projectId}/status-reports`, { page: 1, pageSize: 50 }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectReportingOptions(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.reportingOptions(projectId),
    queryFn: () => api.get<{ projectId: string; plans: ProjectReportingPlanOptionDto[] }>(`/api/projects/${projectId}/reporting/options`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectReportingConfiguration(projectId: string, effectiveAt?: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.reportingConfiguration(projectId, effectiveAt),
    queryFn: async () => (await api.get<ProjectReportingConfigurationDto | null>(`/api/projects/${projectId}/reporting/configuration`, effectiveAt ? { effectiveAt } : undefined)) ?? null,
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useSaveProjectReportingConfiguration(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: SaveProjectReportingConfigurationInput) => api.put<ProjectReportingConfigurationDto>(`/api/projects/${projectId}/reporting/configuration`, data),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "reporting-configuration", projectId] }),
        queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "performance", projectId] }),
      ]);
    },
  });
}

export function useProjectPerformance(projectId: string, params: ProjectPerformanceParams = {}, enabled = true) {
  return useQuery({
    queryKey: projectKeys.performance(projectId, params),
    queryFn: () => api.get<ProjectPerformanceDto>(`/api/projects/${projectId}/performance`, { ...params }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 20_000,
  });
}

export function useProjectFinancialSummary(projectId: string, periodFrom: string, periodTo: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.financialSummary(projectId, periodFrom, periodTo),
    queryFn: () => api.get<ProjectFinancialSummaryDto>(`/api/projects/${projectId}/status-reports/financial-summary`, { periodFrom, periodTo }),
    enabled: Boolean(projectId && periodFrom && periodTo) && enabled,
    staleTime: 60_000,
  });
}

export interface IssueSummaryDto {
  id: string;
  title: string;
  status: string;
  severity?: string;
  priority?: string;
}

export function useProjectIssues(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId), "issues"],
    queryFn: async () => {
      const result = await api.get<{ items: IssueSummaryDto[]; totalCount: number }>("/api/issues", { projectId, pageSize: 100 });
      return result.items;
    },
    enabled: Boolean(projectId) && enabled,
    staleTime: 60_000,
  });
}

export interface ProjectStatusReportCurrentProgressDto {
  projectPlanProgressPercent?: number | null;
  resourceUtilizationStatus?: "Overallocated" | "WithinCapacity" | "NoAssignedWork" | "Unavailable" | string;
  resourceUtilizationPercent?: number | null;
}

export function useProjectStatusReportCurrentProgress(
  projectId: string,
  statusDate?: string,
  enabled = true,
  periodFrom?: string,
  periodTo?: string,
) {
  const params = { statusDate, periodFrom, periodTo };
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId), "current-progress", params],
    queryFn: () => api.get<ProjectStatusReportCurrentProgressDto>(`/api/projects/${projectId}/status-reports/current-progress`, params),
    enabled: Boolean(projectId) && enabled,
    staleTime: 0,
  });
}

export function useCreateProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ProjectStatusReportInput) => api.post<ProjectStatusReportListDto>(`/api/projects/${projectId}/status-reports`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) }),
  });
}

export function useProjectStatusReport(projectId: string, reportId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.statusReport(projectId, reportId),
    queryFn: () => api.get<ProjectStatusReportDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}`),
    enabled: Boolean(projectId && reportId) && enabled,
    staleTime: 30_000,
  });
}

export function requestProjectStatusReportExport(projectId: string, reportId: string) {
  return api.get<ProjectStatusReportDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}/export`);
}

export function useProjectStatusReportComparison(projectId: string, firstReportId: string, secondReportId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId), "comparison", firstReportId, secondReportId],
    queryFn: () => api.get<ProjectStatusReportComparisonDto>(`/api/projects/${projectId}/status-reports/compare`, { firstReportId, secondReportId }),
    enabled: Boolean(projectId && firstReportId && secondReportId && firstReportId !== secondReportId) && enabled,
    staleTime: 60_000,
  });
}

export function useUpdateProjectStatusReport(projectId: string, reportId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ProjectStatusReportInput) => api.put<ProjectStatusReportDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.statusReport(projectId, reportId) });
    },
  });
}

export function usePublishProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ reportId, content }: { reportId: string; content: ProjectStatusReportInput }) =>
      api.post<ProjectStatusReportDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}/publish`, content),
    onSuccess: (_data, { reportId }) => Promise.all([
      queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) }),
      queryClient.invalidateQueries({ queryKey: projectKeys.statusReport(projectId, reportId) }),
    ]),
  });
}

export function useDeleteProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reportId: string) => api.delete<void>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) }),
  });
}

export function useProjectChangeRequests(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.changeRequests(projectId),
    queryFn: () => api.get<ProjectChangeRequestDto[]>(`/api/projects/${projectId}/change-requests`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectChangeRequestApproverOptions(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.changeRequests(projectId), "approvers"],
    queryFn: () => api.get<ProjectChangeRequestApprovalOptionsDto>(`/api/projects/${encodeURIComponent(projectId)}/change-requests/approvers`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

function invalidateProjectChangeRequests(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: projectKeys.changeRequests(projectId) }),
    queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true }),
    queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId), exact: true }),
    queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
  ]);
}

export function useCreateProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProjectChangeRequestDto) => api.post<ProjectChangeRequestDto>(`/api/projects/${projectId}/change-requests`, data),
    onSuccess: () => invalidateProjectChangeRequests(queryClient, projectId),
  });
}

export function useUpdateProjectChangeRequestDraft(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: CreateProjectChangeRequestDto & { rowVersion: ProjectChangeRequestRowVersion } }) =>
      api.put<ProjectChangeRequestDto>(`/api/project-change-requests/${encodeURIComponent(id)}`, dto),
    onSuccess: () => invalidateProjectChangeRequests(queryClient, projectId),
  });
}

export function useSubmitProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, rowVersion }: { id: string; rowVersion: ProjectChangeRequestRowVersion }) =>
      api.post<ProjectChangeRequestDto>(`/api/project-change-requests/${encodeURIComponent(id)}/submit`, { rowVersion }),
    onSuccess: () => invalidateProjectChangeRequests(queryClient, projectId),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
  });
}

export function useRemediateProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<ProjectChangeRequestDto>(`/api/project-change-requests/${encodeURIComponent(id)}/remediate`, {}),
    onSuccess: () => invalidateProjectChangeRequests(queryClient, projectId),
  });
}

export function useReassignProjectChangeRequestApproval(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, assignmentId, newApproverUserId, reason }: { id: string; assignmentId: string; newApproverUserId: string; reason: string }) =>
      api.post<ProjectChangeRequestApprovalDto>(`/api/project-change-requests/${encodeURIComponent(id)}/approvals/${encodeURIComponent(assignmentId)}/reassign`, { newApproverUserId, reason }),
    onSuccess: () => invalidateProjectChangeRequests(queryClient, projectId),
  });
}

export function useCancelProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ProjectChangeRequestDto>(`/api/project-change-requests/${encodeURIComponent(requestId)}/cancel`, {}),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: projectKeys.changeRequests(projectId) }),
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId), exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
    ]),
  });
}

export function useAddProjectPerson(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProjectPersonDto) => api.post<ProjectPersonDto>(`/api/projects/${projectId}/people`, data),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useUpdateProjectPerson(projectId: string, personId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateProjectPersonDto) => api.put<ProjectPersonDto>(`/api/projects/${projectId}/people/${encodeURIComponent(personId)}`, data),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useRemoveProjectPerson(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ personId, source, teamId, rowVersion }: { personId: string; source: string; teamId?: string | null; rowVersion?: string }) =>
      api.post<ProjectPersonDto>(`/api/projects/${projectId}/people/${encodeURIComponent(personId)}/remove`, { source, teamId: teamId ?? undefined, rowVersion }),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useClearProjectSponsor(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.delete<void>(`/api/projects/${encodeURIComponent(projectId)}/people/sponsor`),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateProjectDto) => api.post<ProjectDto>("/api/projects", data),
    retry: false,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}

export function useUpdateProject(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateProjectDto) => api.put<ProjectDto>(`/api/projects/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.shell(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
      queryClient.invalidateQueries({ queryKey: projectKeys.people(id) });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(id) });
    },
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.delete<void>(`/api/projects/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}

export function useArchiveProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.post<void>(`/api/projects/${id}/archive`, {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.shell(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}

export function useReactivateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.post<void>(`/api/projects/${id}/reactivate`, {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.shell(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}
