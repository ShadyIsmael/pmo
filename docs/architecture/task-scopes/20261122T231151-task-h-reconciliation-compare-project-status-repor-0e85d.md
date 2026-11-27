# UPI Task Context Manifest

Task: **Task H reconciliation: compare ProjectStatusReport financial measures, ProjectReportingService financial summary and Actual Paid, and ProgramPortfolioFinancialRollupService against TARGET_FINANCIAL_OPERATING_MODEL. Check outstanding/planned commitments, gross/net Actual Paid, currency grouping, both settlement rails.**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261122T230530-8e78adf`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/delivery/FinancialRollupView.tsx` | 1-163 | 3943 | filename-match:financial, filename-match:rollup, module-mentioned, primary-module, symbol-match:CurrencyGroup |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportingSemantics.cs` | 1-49 | 454 | filename-match:report, filename-match:reporting, filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportingCategories |
| `apps/web-react/src/features/projects/api/project-financials.ts` | 5-18, 20-30, 32-38, 40-44, 46-54 | 388 | filename-match:financial, module-mentioned, primary-module, symbol-match:ProjectFinancialFoundation |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs` | 1-104 | 1286 | filename-match:actual, module-mentioned, primary-module, symbol-match:ActualWorkCostLookup |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` | 3-11, 13-19, 21-37, 39-51, 53-57 | 674 | filename-match:report, filename-match:reporting, module-mentioned, primary-module, symbol-match:SaveProjectReportingConfigurationDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectReportingService.cs` | 1-13 | 314 | filename-match:report, filename-match:reporting, module-mentioned, primary-module, symbol-match:IProjectReportingService |
| `apps/web-react/src/features/projects/lib/project-reporting-configuration.ts` | 1-18 | 139 | filename-match:report, filename-match:reporting, module-mentioned, primary-module |

## Dependency awareness (not automatically read)

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
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusCatalogEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReport.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportSnapshots.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusTransitionRule.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/StakeholderInvitation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/TimeEntry.cs` — REFERENCES_TYPE (inferred-token-match; TimeEntry)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/TimeEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/TimerSegment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)

## Budget

- Initial selected source: **7198** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
