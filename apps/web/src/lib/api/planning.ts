import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export type PlanType = "Execution" | "Recovery" | "Tender" | "Construction" | "Shutdown" | "Scenario" | "Custom";
export type PlanStatus = "Draft" | "Planned" | "InProgress" | "Completed" | "OnHold";
export type PlanVersionType = "Working" | "Baseline";
export type PlanVersionStatus = "Active" | "PendingApproval" | "Rejected" | "Approved" | "Superseded";
export type PlanActivityType = "Task" | "Milestone";
export type PlanActivityMovePosition = "Before" | "After" | "Child";
export type PlanSchedulingMode = "Automatic" | "Manual";
export type PlanConstraintType = "AsSoonAsPossible" | "AsLateAsPossible" | "MustStartOn" | "MustFinishOn" | "StartNoEarlierThan" | "StartNoLaterThan" | "FinishNoEarlierThan" | "FinishNoLaterThan";
export type PlanDependencyType = "FinishToFinish" | "StartToStart" | "FinishToStart" | "StartToFinish";
export type PlanTextColorToken = "Slate" | "Blue" | "Teal" | "Green" | "Amber" | "Red" | "Violet";
export type PlanHighlightColorToken = "Yellow" | "Blue" | "Green" | "Orange" | "Pink";
export type PlanFontSizeToken = "Small" | "Normal" | "Large" | "ExtraLarge";

export interface PlanTextFormatDto {
  isBold: boolean;
  isItalic: boolean;
  isUnderline: boolean;
  textColorToken?: PlanTextColorToken;
  highlightColorToken?: PlanHighlightColorToken;
  fontSizeToken?: PlanFontSizeToken;
}

export interface PlanVersionDto {
  id: string;
  planId: string;
  versionNumber: number;
  type: PlanVersionType;
  status: PlanVersionStatus;
  name?: string;
  sourceVersionId?: string;
  sourceBaselineId?: string;
  createdBy: string;
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface PlanSummaryDto {
  id: string;
  projectId: string;
  planType: PlanType;
  name: string;
  description?: string;
  status: PlanStatus;
  versionCount: number;
  createdAt: string;
  updatedAt?: string;
}

export interface PlanningBootstrapDto {
  plans: PlanSummaryDto[];
  selectedPlan?: PlanDto;
  workingVersion?: PlanVersionDto;
  wbs: PlanWbsNodeDto[];
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
}

export interface CreatePlanInput {
  planType: PlanType;
  name: string;
  description?: string | null;
}

export interface PlanDto extends PlanSummaryDto {
  tenantId: string;
  versions: PlanVersionDto[];
}

export interface PlanWbsNodeDto {
  id: string;
  planVersionId: string;
  parentWbsId?: string;
  sortOrder: number;
  code: string;
  name: string;
  activityId?: string;
  deliverableId?: string;
  textFormat?: PlanTextFormatDto | null;
  rowVersion: string;
  children: PlanWbsNodeDto[];
}

export interface PlanActivityDto {
  id: string;
  planVersionId: string;
  /** Stable lineage identity shared by Working and Baseline clones. */
  sourceActivityId?: string;
  code: string;
  name: string;
  description?: string;
  type: PlanActivityType;
  isSummaryTask: boolean;
  schedulingMode: PlanSchedulingMode;
  constraintType: PlanConstraintType;
  constraintDate?: string;
  deadline?: string;
  deadlineExceeded: boolean;
  constraintApplied: boolean;
  calendarOverride: boolean;
  schedulingConflict: boolean;
  startDate?: string;
  endDate?: string;
  actualStart?: string;
  actualFinish?: string;
  calendarId?: string;
  duration: number;
  sortOrder: number;
  estimateHours?: number;
  progress?: number;
  wbsId?: string;
  textFormat?: PlanTextFormatDto | null;
  totalSlack?: number;
  freeSlack?: number;
  critical: boolean;
  baselineStartDate?: string;
  baselineFinishDate?: string;
  baselineDuration?: number;
  startVarianceDays?: number;
  finishVarianceDays?: number;
  durationVarianceDays?: number;
  rowVersion: string;
}

export interface MovePlanActivityInput {
  targetActivityId?: string;
  targetWbsId?: string;
  position: PlanActivityMovePosition;
  sortOrder?: number;
  rowVersion: string;
  wbsRowVersion: string;
  targetWbsRowVersion?: string;
}

export interface PlanSchedulingAnalysisDto {
  planVersionId: string;
  hasCycle: boolean;
  criticalPathActivityIds: string[];
  activities: Array<{ activityId: string; totalSlack?: number; freeSlack?: number; critical: boolean }>;
}

export interface PlanSchedulingExplanationDto {
  activityId: string;
  schedulingMode: PlanSchedulingMode;
  constraintType: PlanConstraintType;
  constraintDate?: string;
  deadline?: string;
  effectiveCalendarId?: string;
  calendarOverride: boolean;
  start?: string;
  finish?: string;
  duration: number;
  totalSlack?: number;
  freeSlack?: number;
  critical: boolean;
  deadlineExceeded: boolean;
  hasPredecessors: boolean;
  hasSuccessors: boolean;
  reasons: Array<{ code: string; relatedActivityId?: string; dependencyType?: PlanDependencyType; lagDays?: number; workingDays?: number; constraintType?: PlanConstraintType }>;
  issues: Array<{ code: string; message: string; activityId: string; relatedActivityId?: string }>;
}

export interface PlanningColumnFilterDto { text?: string; min?: string; max?: string; value?: string; }
export interface PlanningTablePreferenceDto {
  planId: string;
  visibleColumns: string[];
  columnOrder: string[];
  columnWidths: Record<string, number>;
  columnFilters: Record<string, PlanningColumnFilterDto>;
  collapsedRowIds: string[];
}

export interface PlanDependencyDto {
  id: string;
  planVersionId: string;
  activityId: string;
  predecessorActivityId: string;
  type: PlanDependencyType;
  lag?: number;
  rowVersion: string;
}

export interface PlanPredecessorInput {
  predecessorActivityId: string;
  type: PlanDependencyType;
  lag?: number | null;
}

export interface PlanScheduledActivityStateDto {
  activityId: string;
  startDate?: string;
  endDate?: string;
  duration: number;
}

export interface ReplacePlanPredecessorsResultDto {
  dependencies: PlanDependencyDto[];
  scheduledActivities: PlanScheduledActivityStateDto[];
  schedulingRecalculated: boolean;
}

export interface PlanAssignmentDto {
  id: string;
  planVersionId: string;
  activityId: string;
  resourceId: string;
  allocationPct?: number;
  hoursPerWeek?: number;
  plannedHours?: number;
  startDate?: string;
  endDate?: string;
  projectRoleId?: string;
  resourceAllocationId?: string;
  rowVersion: string;
}

export interface CalendarDayRuleDto {
  // ASP.NET serializes DayOfWeek as a string while the update contract also
  // accepts the numeric enum value. Keep the client tolerant of both forms;
  // the calendar editor normalizes rules to numeric weekday indexes.
  dayOfWeek: number | string;
  isWorkingDay: boolean;
  startTime?: string;
  endTime?: string;
  breakStartTime?: string;
  breakEndTime?: string;
}

export interface CalendarHolidayDto {
  name: string;
  description?: string;
  date: string;
  endDate?: string;
  isRecurring: boolean;
}

export interface PlanCalendarDto {
  id: string;
  projectId: string;
  planVersionId: string;
  name: string;
  description?: string;
  templateId?: string;
  effectiveFrom?: string;
  effectiveTo?: string;
  isDefault: boolean;
  dayRules: CalendarDayRuleDto[];
  holidays: CalendarHolidayDto[];
  rowVersion: string;
}

export interface ResourceLookupDto { id: string; name: string; jobTitle?: string; isActive: boolean; }

export interface PlanImportNoticeDto { code: string; message: string; count: number; }
export interface PlanImportStatisticsDto { tasks: number; milestones: number; wbsLevels: number; dependencies: number; resources: number; assignments: number; calendars: number; actualDates: number; }
export interface PlanImportTaskPreviewDto {
  sourceId: string; parentSourceId?: string; code: string; name: string; outlineLevel: number; sortOrder: number;
  isMilestone: boolean; schedulingMode: PlanSchedulingMode; constraintType: PlanConstraintType; constraintDate?: string; deadline?: string; durationDays: number; start?: string; finish?: string; actualStart?: string; actualFinish?: string; percentComplete?: number;
  predecessors: string[]; resources: string[];
}
export interface PlanImportResourcePreviewDto { sourceId: string; name: string; email?: string; nexusResourceId?: string; matchStatus: "Matched" | "Unmatched"; assignmentCount: number; }
export interface PlanImportCalendarPreviewDto { name: string; isDefault: boolean; workingDays: number; holidays: number; }
export interface PlanImportPreviewDto {
  fileName: string; format: string; suggestedPlanName: string; statistics: PlanImportStatisticsDto;
  tasks: PlanImportTaskPreviewDto[]; dependencies: Array<{ successorSourceId: string; predecessorSourceId: string; type: PlanDependencyType; lagDays?: number }>;
  resources: PlanImportResourcePreviewDto[]; calendars: PlanImportCalendarPreviewDto[];
  warnings: PlanImportNoticeDto[]; unsupportedFields: PlanImportNoticeDto[];
}
export interface PlanImportResultDto { planId: string; planVersionId: string; versionNumber: number; createdPlan: boolean; statistics: PlanImportStatisticsDto; warnings: PlanImportNoticeDto[]; }

export const planningKeys = {
  bootstrap: (projectId: string) => ["planning", "bootstrap", projectId] as const,
  plans: (projectId: string) => ["planning", "plans", projectId] as const,
  plan: (projectId: string, planId: string) => ["planning", "plan", projectId, planId] as const,
  workingVersion: (projectId: string, planId: string) => ["planning", "working-version", projectId, planId] as const,
  wbs: (projectId: string, planId: string) => ["planning", "wbs", projectId, planId] as const,
  activities: (projectId: string, planId: string) => ["planning", "activities", projectId, planId] as const,
  dependencies: (projectId: string, planId: string) => ["planning", "dependencies", projectId, planId] as const,
  assignments: (projectId: string, planId: string) => ["planning", "assignments", projectId, planId] as const,
  calendars: (projectId: string, planId: string) => ["planning", "calendars", projectId, planId] as const,
  resources: ["planning", "resources"] as const,
  tablePreferences: (projectId: string, planId: string) => ["planning", "table-preferences", projectId, planId] as const,
  schedulingExplanation: (projectId: string, planId: string, activityId: string) => ["planning", "scheduling-explanation", projectId, planId, activityId] as const,
};

export function usePlanningBootstrap(projectId: string, enabled = true) {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: planningKeys.bootstrap(projectId),
    queryFn: async () => {
      const data = await api.get<PlanningBootstrapDto>(endpoints.planning.bootstrap(projectId));
      queryClient.setQueryData(planningKeys.plans(projectId), data.plans);
      if (data.selectedPlan) {
        queryClient.setQueryData(planningKeys.plan(projectId, data.selectedPlan.id), data.selectedPlan);
        queryClient.setQueryData(planningKeys.wbs(projectId, data.selectedPlan.id), data.wbs);
        queryClient.setQueryData(planningKeys.activities(projectId, data.selectedPlan.id), data.activities);
        queryClient.setQueryData(planningKeys.dependencies(projectId, data.selectedPlan.id), data.dependencies);
        if (data.workingVersion) queryClient.setQueryData(planningKeys.workingVersion(projectId, data.selectedPlan.id), data.workingVersion);
      }
      return data;
    },
    enabled: enabled && !!projectId,
    staleTime: 30_000,
    refetchOnMount: false,
    refetchOnReconnect: false,
    retry: false,
  });
}

export function usePlanningPlans(projectId: string, enabled = true) {
  return useQuery({ queryKey: planningKeys.plans(projectId), queryFn: () => api.get<PlanSummaryDto[]>(endpoints.planning.plans(projectId)), enabled: enabled && !!projectId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningPlan(projectId: string, planId: string) {
  return useQuery({ queryKey: planningKeys.plan(projectId, planId), queryFn: () => api.get<PlanDto>(endpoints.planning.plan(projectId, planId)), enabled: !!projectId && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningWorkingVersion(projectId: string, planId: string) {
  return useQuery({ queryKey: planningKeys.workingVersion(projectId, planId), queryFn: () => api.get<PlanVersionDto>(endpoints.planning.workingVersion(projectId, planId)), enabled: !!projectId && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningWbs(projectId: string, planId: string, enabled: boolean) {
  return useQuery({ queryKey: planningKeys.wbs(projectId, planId), queryFn: () => api.get<PlanWbsNodeDto[]>(endpoints.planning.wbs(projectId, planId)), enabled: enabled && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningActivities(projectId: string, planId: string, enabled: boolean) {
  return useQuery({ queryKey: planningKeys.activities(projectId, planId), queryFn: () => api.get<PlanActivityDto[]>(endpoints.planning.activities(projectId, planId)), enabled: enabled && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningDependencies(projectId: string, planId: string, enabled: boolean) {
  return useQuery({ queryKey: planningKeys.dependencies(projectId, planId), queryFn: () => api.get<PlanDependencyDto[]>(endpoints.planning.dependencies(projectId, planId)), enabled: enabled && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningSchedulingExplanation(projectId: string, planId: string, activityId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: planningKeys.schedulingExplanation(projectId, planId, activityId!),
    queryFn: () => api.get<PlanSchedulingExplanationDto>(endpoints.planning.schedulingExplanation(projectId, planId, activityId!)),
    enabled: enabled && !!projectId && !!planId && !!activityId,
    staleTime: 5_000,
    refetchOnMount: false,
    refetchOnReconnect: false,
    retry: false,
  });
}

export function usePlanningTablePreferences(projectId: string, planId: string, enabled = true) {
  return useQuery({
    queryKey: planningKeys.tablePreferences(projectId, planId),
    queryFn: () => api.get<PlanningTablePreferenceDto>(endpoints.planning.tablePreferences(projectId, planId)),
    enabled: enabled && !!projectId && !!planId,
    staleTime: Infinity,
    refetchOnMount: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: false,
  });
}

export function useSavePlanningTablePreferences(projectId: string, planId: string) {
  const queryClient = useQueryClient();
  const queryKey = planningKeys.tablePreferences(projectId, planId);
  return useMutation({
    mutationFn: (body: Omit<PlanningTablePreferenceDto, "planId">) => api.put<PlanningTablePreferenceDto>(endpoints.planning.tablePreferences(projectId, planId), body),
    retry: false,
    onSuccess: (value) => { queryClient.setQueryData(queryKey, value); },
  });
}

export function useResetPlanningTablePreferences(projectId: string, planId: string) {
  const queryClient = useQueryClient();
  const queryKey = planningKeys.tablePreferences(projectId, planId);
  return useMutation({ mutationFn: () => api.delete<void>(endpoints.planning.tablePreferences(projectId, planId)), retry: false, onSuccess: () => { queryClient.setQueryData(queryKey, { planId, visibleColumns: [], columnOrder: [], columnWidths: {}, columnFilters: {}, collapsedRowIds: [] }); } });
}

export function usePlanningAssignments(projectId: string, planId: string, enabled: boolean) {
  return useQuery({ queryKey: planningKeys.assignments(projectId, planId), queryFn: () => api.get<PlanAssignmentDto[]>(endpoints.planning.assignments(projectId, planId)), enabled: enabled && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function usePlanningCalendars(projectId: string, planId: string, enabled: boolean) {
  return useQuery({ queryKey: planningKeys.calendars(projectId, planId), queryFn: () => api.get<PlanCalendarDto[]>(endpoints.planning.calendars(projectId, planId)), enabled: enabled && !!planId, staleTime: 30_000, refetchOnMount: false, refetchOnReconnect: false, retry: false });
}

export function useUpdatePlanningCalendar(projectId: string, planId: string) {
  const queryClient = useQueryClient();
  const queryKey = planningKeys.calendars(projectId, planId);
  return useMutation({
    mutationFn: ({ calendarId, body }: { calendarId: string; body: unknown }) =>
      planningApi.updateCalendar(projectId, planId, calendarId, body),
    retry: false,
    onSuccess: (updatedCalendar) => {
      queryClient.setQueryData<PlanCalendarDto[]>(queryKey, (calendars) =>
        calendars?.map((calendar) => calendar.id === updatedCalendar.id ? updatedCalendar : calendar),
      );
      void queryClient.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) });
    },
  });
}

export function usePlanningResources(enabled: boolean) {
  return useQuery({
    queryKey: planningKeys.resources,
    queryFn: async () => {
      // Include inactive resources so historical assignments still resolve to
      // their real names in the Planning table.
      const response = await api.get<{ items: ResourceLookupDto[] }>(endpoints.resources.root, { pageSize: 1000 });
      return response.items;
    },
    enabled,
    staleTime: 30_000,
    refetchOnMount: false,
    refetchOnReconnect: false,
    retry: false,
  });
}

export function usePlanningMutation<TBody, TResult>(
  queryKey: readonly unknown[],
  action: (_body: TBody) => Promise<TResult>,
) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: action, retry: false, onSuccess: () => void queryClient.invalidateQueries({ queryKey }) });
}

export const planningApi = {
  createPlan: (projectId: string, body: CreatePlanInput) =>
    api.post<PlanDto>(endpoints.planning.plans(projectId), { projectId, ...body }),
  createWbs: (projectId: string, planId: string, body: unknown) => api.post<PlanWbsNodeDto>(endpoints.planning.wbs(projectId, planId), body),
  updateWbs: (projectId: string, planId: string, id: string, body: unknown) => api.put<PlanWbsNodeDto>(endpoints.planning.wbsNode(projectId, planId, id), body),
  deleteWbs: (projectId: string, planId: string, id: string, rowVersion: string) => api.delete<void>(`${endpoints.planning.wbsNode(projectId, planId, id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  createActivity: (projectId: string, planId: string, body: unknown) => api.post<PlanActivityDto>(endpoints.planning.activities(projectId, planId), body),
  updateActivity: (projectId: string, planId: string, id: string, body: unknown) => api.put<PlanActivityDto>(endpoints.planning.activity(projectId, planId, id), body),
  moveActivity: (projectId: string, planId: string, id: string, body: MovePlanActivityInput) => api.post<PlanActivityDto>(endpoints.planning.moveActivity(projectId, planId, id), body),
  captureBaseline: (projectId: string, planId: string, rowVersion: string) => api.post<PlanVersionDto>(`${endpoints.planning.captureBaseline(projectId, planId)}?rowVersion=${encodeURIComponent(rowVersion)}`, {}),
  deleteActivity: (projectId: string, planId: string, id: string, rowVersion: string) => api.delete<void>(`${endpoints.planning.activity(projectId, planId, id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  createDependency: (projectId: string, planId: string, body: unknown) => api.post<PlanDependencyDto>(endpoints.planning.dependencies(projectId, planId), body),
  replacePredecessors: (projectId: string, planId: string, body: { activityId: string; predecessors: PlanPredecessorInput[] }) => api.put<ReplacePlanPredecessorsResultDto>(endpoints.planning.predecessors(projectId, planId), body),
  updateDependency: (projectId: string, planId: string, id: string, body: unknown) => api.put<PlanDependencyDto>(endpoints.planning.dependency(projectId, planId, id), body),
  deleteDependency: (projectId: string, planId: string, id: string, rowVersion: string) => api.delete<void>(`${endpoints.planning.dependency(projectId, planId, id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  createAssignment: (projectId: string, planId: string, body: unknown) => api.post<PlanAssignmentDto>(endpoints.planning.assignments(projectId, planId), body),
  updateAssignment: (projectId: string, planId: string, id: string, body: unknown) => api.put<PlanAssignmentDto>(endpoints.planning.assignment(projectId, planId, id), body),
  deleteAssignment: (projectId: string, planId: string, id: string, rowVersion: string) => api.delete<void>(`${endpoints.planning.assignment(projectId, planId, id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  createCalendar: (projectId: string, planId: string, body: unknown) => api.post<PlanCalendarDto>(endpoints.planning.calendars(projectId, planId), body),
  updateCalendar: (projectId: string, planId: string, id: string, body: unknown) => api.put<PlanCalendarDto>(endpoints.planning.calendar(projectId, planId, id), body),
  deleteCalendar: (projectId: string, planId: string, id: string, rowVersion: string) => api.delete<void>(`${endpoints.planning.calendar(projectId, planId, id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  previewImport: (projectId: string, file: File, targetPlanId?: string) => {
    const form = new FormData();
    form.append("file", file);
    if (targetPlanId) form.append("targetPlanId", targetPlanId);
    return api.upload<PlanImportPreviewDto>(endpoints.planning.importPreview(projectId), form);
  },
  commitImport: (projectId: string, file: File, options: { targetPlanId?: string; newPlanName?: string; newPlanType?: PlanType }) => {
    const form = new FormData();
    form.append("file", file);
    if (options.targetPlanId) form.append("targetPlanId", options.targetPlanId);
    if (options.newPlanName) form.append("newPlanName", options.newPlanName);
    if (options.newPlanType) form.append("newPlanType", options.newPlanType);
    return api.upload<PlanImportResultDto>(endpoints.planning.importCommit(projectId), form);
  },
};

export function useCreatePlanningPlan(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreatePlanInput) => planningApi.createPlan(projectId, body),
    retry: false,
    onSuccess: (plan) => {
      const summary: PlanSummaryDto = {
        id: plan.id,
        projectId: plan.projectId,
        planType: plan.planType,
        name: plan.name,
        description: plan.description,
        status: plan.status,
        versionCount: plan.versions.length,
        createdAt: plan.createdAt,
        updatedAt: plan.updatedAt,
      };
      queryClient.setQueryData<PlanSummaryDto[]>(planningKeys.plans(projectId), (current) => {
        const existing = current?.filter((item) => item.id !== plan.id) ?? [];
        return [summary, ...existing];
      });
      queryClient.setQueryData(planningKeys.plan(projectId, plan.id), plan);
      const workingVersion = plan.versions.find((version) => version.type === "Working" && version.status === "Active");
      if (workingVersion) queryClient.setQueryData(planningKeys.workingVersion(projectId, plan.id), workingVersion);
      queryClient.setQueryData<PlanningBootstrapDto | undefined>(planningKeys.bootstrap(projectId), (current) => current ? {
        ...current,
        plans: [summary, ...current.plans.filter((item) => item.id !== plan.id)],
        selectedPlan: plan,
        workingVersion,
        wbs: [],
        activities: [],
        dependencies: [],
      } : current);
    },
  });
}
