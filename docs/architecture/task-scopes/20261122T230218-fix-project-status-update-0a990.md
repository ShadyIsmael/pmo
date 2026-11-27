# UPI Task Context Manifest

Task: **Fix Project Status update**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261122T230114-a68d844`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs` | 6-12, 14-19, 21-26, 28-42, 44-88 | 1155 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectResourceUtilizationStatuses |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs` | 1-17 | 484 | filename-match:status, module-mentioned, primary-module, symbol-match:IProjectStatusReportService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportComparison.cs` | 1-132 | 2194 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportComparison |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusCatalogEntry.cs` | 1-20 | 268 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusCatalogEntry |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReport.cs` | 1-84 | 1069 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReport |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportSnapshots.cs` | 1-62 | 753 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportRiskSnapshot |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportingSemantics.cs` | 1-49 | 454 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportingCategories |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusTransitionRule.cs` | 6-13 | 86 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusTransitionRule |
| `apps/web-react/src/features/delivery/WorkItemStatusTrack.tsx` | 1-75 | 915 | filename-match:status, module-mentioned, primary-module, symbol-match:WorkItemStatusTrackProps |
| `apps/web-react/src/features/projects/lib/gantt-status.ts` | 1-21 | 326 | filename-match:status, module-mentioned, primary-module, symbol-match:GanttStatus |
| `apps/web-react/src/test/project-change-status.test.tsx` | 120-127 | 173 | filename-match:status, module-mentioned, primary-module, symbol-match:statuses |
| `apps/web-react/src/test/project-status-reports-parity.test.tsx` | 111-123 | 118 | filename-match:status, module-mentioned, primary-module, symbol-match:renderStatusReports |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs` | 99-104, 108-113, 117-122, 126-133, 163-170 | 701 | module-mentioned, primary-module, symbol-match:GetStatusReports |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectLifecycleCatalogController.cs` | 97-101, 104-108, 111-115 | 315 | module-mentioned, primary-module, symbol-match:ActivateStatus |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` | 3-9, 11-17, 19-26, 28-33, 62-62 | 342 | module-mentioned, primary-module, symbol-match:ProjectPhaseGateReadinessStatuses |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs` | 210-225 | 182 | module-mentioned, primary-module, symbol-match:SnapshotStatusReportDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogContracts.cs` | 70-70 | 28 | module-mentioned, primary-module, symbol-match:ProjectStatusTransitionOptionDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` | 30-37 | 31 | module-mentioned, primary-module, symbol-match:Status |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthorizationService.cs` — REFERENCES_TYPE (inferred-token-match; IAuthorizationService)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Team.cs` — REFERENCES_TYPE (inferred-token-match; Team)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Api/PaymentObligationProjectLookupController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/BaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/CostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/PaymentObligationProjectLookupDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectBaselineVersionDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — REFERENCES_TYPE (inferred-token-match; CreateProjectDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — REFERENCES_TYPE (inferred-token-match; CreateProjectHandoverRequestDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectOverviewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPeopleDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — REFERENCES_TYPE (inferred-token-match; SaveProjectReportingConfigurationDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStakeholderDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderInvitationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderProjectViewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)

## Budget

- Initial selected source: **9594** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
