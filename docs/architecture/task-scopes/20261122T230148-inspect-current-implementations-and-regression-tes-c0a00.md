# UPI Task Context Manifest

Task: **Inspect current implementations and regression tests in PlannedPaymentService.cs, ProjectAnnualBudgetLineWorkflow.cs, ProjectStatusReportService.cs, PermissionCatalog.cs, BootstrapPermissionDelegationPolicy.cs, project-finance-workspace.test.tsx, planned-payments.ts, finance.ts and focused financial tests. Check annual/Budget Line reservation, settlement currency integrity, canonical permission scope, idempotency, status reports, frontend mocks, backend test project.**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261122T230114-a68d844`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportingSemantics.cs` | 1-49 | 454 | filename-match:cs, filename-match:report, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectStatusReportingCategories |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs` | 1-17 | 484 | filename-match:report, filename-match:reports, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:IProjectStatusReportService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportSnapshots.cs` | 1-62 | 753 | filename-match:report, filename-match:reports, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectStatusReportRiskSnapshot |
| `apps/web-react/src/features/projects/ProjectAnnualBudgetLines.tsx` | 1-118 | 1788 | filename-match:annual, filename-match:budget, filename-match:line, module-mentioned, primary-module, symbol-match:ProjectAnnualBudget |
| `apps/web-react/src/features/projects/api/planned-payments.ts` | 4-17, 19-40, 42-53, 55-63, 65-70 | 476 | filename-match:payment, filename-match:payments, filename-match:planned, filename-match:ts, module-mentioned, primary-module, symbol-match:PlannedPaymentInstallmentDto |
| `apps/web-react/src/features/projects/api/project-annual-budgets.ts` | 4-44, 46-59, 61-72, 74-78, 80-84 | 725 | filename-match:annual, filename-match:budget, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectAnnualBudget |
| `apps/web-react/src/test/project-status-reports-parity.test.tsx` | 111-123 | 118 | filename-match:report, filename-match:reports, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:renderStatusReports |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs` | 6-12, 14-19, 21-26, 28-42, 44-88 | 1155 | filename-match:report, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectResourceUtilizationStatuses |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportComparison.cs` | 1-132 | 2194 | filename-match:report, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectStatusReportComparison |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusCatalogEntry.cs` | 1-20 | 268 | filename-match:catalog, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectStatusCatalogEntry |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReport.cs` | 1-84 | 1069 | filename-match:report, filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectStatusReport |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusTransitionRule.cs` | 6-13 | 86 | filename-match:status, filename-match:ts, module-mentioned, primary-module, symbol-match:ProjectStatusTransitionRule |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPerformanceDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Baseline.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/CalendarPolicyConstants.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Deliverable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Milestone.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectChangeRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectChangeRequestApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectHandoverRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectManagerAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectMember.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectMemberRole.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhase.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseCatalogEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateEvidence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateRequirementSnapshot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateSnapshot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseTransitionRule.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectReportingConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectSchedule.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectSnapshot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStakeholder.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/StakeholderInvitation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/TimeEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/TimerSegment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/TimerSession.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/WorkBreakdownStructure.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.RiskManagement/Domain/Risk.cs` — REFERENCES_TYPE (inferred-token-match; RiskLevel)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Budget

- Initial selected source: **9570** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
