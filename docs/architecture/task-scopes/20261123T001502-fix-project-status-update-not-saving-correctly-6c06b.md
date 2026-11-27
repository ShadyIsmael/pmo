# UPI Task Context Manifest

Task: **Fix Project Status update not saving correctly**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261123T000905-b97bbe5`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/projects/components/CreateProjectSheet.tsx` | 159-175, 288-314 | 670 | critical-path-step:frontend-save-handler, module-mentioned, primary-module |
| `apps/web-react/src/features/projects/api/projects.ts` | 1514-1527 | 210 | critical-path-step:api-client, module-mentioned, primary-module, symbol-match:ProjectStatusReportListDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs` | 283-295 | 197 | critical-path-step:backend-endpoint, module-mentioned, primary-module, symbol-match:GetStatusReports |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs` | 13-13 | 39 | critical-path-step:service-contract, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` | 485-557 | 1279 | critical-path-step:persistence, critical-path-step:service-implementation, module-mentioned, primary-module |
| `apps/web-react/src/test/project-change-status.test.tsx` | 1-196 | 2400 | critical-path-step:focused-frontend-test, filename-match:status, module-mentioned, primary-module, symbol-match:statuses |
| `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectStatusChangeCatalogTests.cs` | 130-150 | 217 | critical-path-step:focused-backend-test, filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusChangeCatalogTests |

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
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — REFERENCES_TYPE (inferred-token-match; SaveProjectReportingConfigurationDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStakeholderDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)

## Critical path coverage

- Gate: **PASS**; 8/8 steps (100%)
- `frontend-save-handler`: covered; apps/web-react/src/features/projects/components/CreateProjectSheet.tsx
- `api-client`: covered; apps/web-react/src/features/projects/api/projects.ts
- `backend-endpoint`: covered; apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs
- `service-contract`: covered; apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs
- `service-implementation`: covered; apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs
- `persistence`: covered; apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs
- `focused-frontend-test`: covered; apps/web-react/src/test/project-change-status.test.tsx
- `focused-backend-test`: covered; apps/api/tests/NEXUS.Api.Tests/Integration/ProjectStatusChangeCatalogTests.cs
- Relationship `handler-to-client`: covered (source-import-and-call)
- Relationship `client-to-endpoint`: covered (source-method-and-route-match)
- Relationship `endpoint-to-contract`: covered (source-call-and-member-type)
- Relationship `implementation-to-contract`: covered (source-class-declaration) — UNCERTAIN: The class declares the interface implementation; runtime dependency-injection resolution is not verified by this module-scoped route.
- Relationship `service-to-persistence`: covered (same-method-source-call)

## Budget

- Initial selected source: **5012** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
