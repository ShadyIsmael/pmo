# UPI Task Context Manifest

Task: **ProjectOverviewPage selectedProject React details tabs route component hook**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectOverviewDtos.cs` | 1-77 | 1009 | filename-match:overview, primary-module, symbol-match:ProjectOverviewDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectOverviewService.cs` | 7-16 | 114 | filename-match:overview, primary-module, symbol-match:IProjectOverviewService |
| `apps/web-react/src/features/projects/DocumentLibraryOverview.tsx` | 67-163 | 1510 | filename-match:overview, primary-module, symbol-match:DocumentLibraryOverview |
| `apps/web-react/src/features/projects/PlanningActivityDetailsUI.tsx` | 5-25, 40-47 | 298 | filename-match:details, primary-module, symbol-match:PlanningDetailSection |
| `apps/web-react/src/test/project-overview-loading.test.tsx` | 39-51 | 112 | filename-match:overview, primary-module, symbol-match:renderOverview |
| `apps/web-react/src/test/project-overview-source-parity.test.tsx` | 119-131 | 112 | filename-match:overview, primary-module, symbol-match:renderOverview |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/HandoverRequestController.cs` | 36-44 | 173 | primary-module, symbol-match:GetDetails |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs` | 35-40, 324-335 | 274 | primary-module, symbol-match:GetOverview |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs` | 21-26 | 63 | primary-module, symbol-match:ProjectStatusReportOverviewDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectHandoverRequestService.cs` | 17-17 | 45 | primary-module, symbol-match:GetExistingRequestDetailsAsync |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs` | 16-16 | 31 | primary-module, symbol-match:ReactivateAsync |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs` | 9-9 | 38 | primary-module, symbol-match:GetOverviewAsync |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectHandoverRequestService.cs` | 356-380 | 584 | primary-module, symbol-match:GetExistingRequestDetailsAsync |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` | 809-838 | 376 | primary-module, symbol-match:ReactivateAsync |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportService.cs` | 98-120 | 402 | primary-module, symbol-match:GetOverviewAsync |
| `apps/web-react/src/features/delivery/workItemSelection.ts` | 1-3 | 60 | primary-module, symbol-match:isAllVisibleWorkItemsSelected |
| `apps/web-react/src/features/projects/ProjectFinanceTab.tsx` | 74-96 | 247 | primary-module, symbol-match:DataQualityDetails |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/BaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/CostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/PaymentObligationProjectLookupDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectBaselineVersionDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — REFERENCES_TYPE (inferred-token-match; RejectProjectHandoverRequestDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPeopleDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStakeholderDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderInvitationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderProjectViewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimeEntryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimerDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IMilestoneService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IPaymentObligationProjectLookupService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectChangeRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectLifecycleCatalogService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectPeopleService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectPhaseGateReadinessService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectReportingService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectSnapshotService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStakeholderService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **5448** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
