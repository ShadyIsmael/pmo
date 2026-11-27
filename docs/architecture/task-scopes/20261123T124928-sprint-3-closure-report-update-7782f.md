# UPI Task Context Manifest

Task: **Sprint 3 closure report update**
Primary module: **reports** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Reporting/Api/ReportController.cs` | 1-33 | 306 | filename-match:report, primary-module, symbol-match:ReportController |
| `apps/api/src/NEXUS.Modules.Reporting/Application/IReportDataProvider.cs` | 1-6 | 66 | filename-match:report, primary-module, symbol-match:IReportDataProvider |
| `apps/api/src/NEXUS.Modules.Reporting/Application/IReportService.cs` | 6-10 | 77 | filename-match:report, primary-module, symbol-match:IReportService |
| `apps/api/src/NEXUS.Modules.Reporting/Application/ReportService.cs` | 1-29 | 334 | filename-match:report, primary-module, symbol-match:ReportService |
| `apps/api/src/NEXUS.Modules.Reporting/Domain/ReportDefinition.cs` | 1-15 | 140 | filename-match:report, primary-module, symbol-match:ReportDefinition |
| `apps/api/src/NEXUS.Modules.Reporting/Domain/ReportExport.cs` | 1-16 | 159 | filename-match:report, primary-module, symbol-match:ReportExport |
| `apps/api/src/NEXUS.Modules.Reporting/Domain/ReportSchedule.cs` | 1-16 | 162 | filename-match:report, primary-module, symbol-match:ReportSchedule |
| `apps/api/src/NEXUS.Modules.Reporting/Infrastructure/ReportRepository.cs` | 1-19 | 148 | filename-match:report, primary-module, symbol-match:ReportRepository |
| `apps/api/src/NEXUS.Modules.Reporting/ModuleRegistration/ReportingModuleRegistration.cs` | 1-13 | 111 | filename-match:report, primary-module, symbol-match:ReportingModuleRegistration |
| `packages/api-client/src/reporting.ts` | 1-21 | 182 | filename-match:report, primary-module, symbol-match:Report |
| `apps/api/src/NEXUS.Modules.Reporting/NEXUS.Modules.Reporting.csproj` | 1-15 | 161 | filename-match:report, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Integration/ActualPaidReportingTests.cs` | 1-441 | 6588 | filename-match:report, primary-module, symbol-match:ActualPaidReportingTests |
| `apps/web-react/src/test/reports-parity.test.tsx` | 33-44 | 127 | filename-match:report, primary-module, symbol-match:renderReports |

## Dependency awareness (not automatically read)

- `apps/web-react/src/features/operations/OperationsPagesFull.tsx` — REFERENCES_TYPE (inferred-token-match; Report)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAggregateRoot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IApprovalDelegationEligibilityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAuditable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ICostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IMeetingPublicIdLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IOrganizationLifecycleHandler.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectAudienceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectFinancialLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectOrganizationLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectPlanMilestoneLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectPlanProgressLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectResourcePlanningContextLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IResourceReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ISoftDeletable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantRegistryStatusValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IUserReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemAssigneeLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/PagedResult.cs` — REFERENCES_TYPE (inferred-token-match; PagedResult)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **8561** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
