# UPI Task Context Manifest

Task: **Authenticated Project details and project lifecycle options endpoint phase resolution by Project's owning Organization, ProjectPhaseId and legacy Phase consistency**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261126T202114-0146d84`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateArchivedTemplateReferenceValidator.cs` | 1-64 | 767 | filename-match:id, filename-match:phase, primary-module, symbol-match:ProjectPhaseGateArchivedTemplateReferenceValidator |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateEvidence.cs` | 9-24 | 213 | filename-match:id, filename-match:phase, primary-module, symbol-match:ProjectPhaseGateEvidence |
| `apps/web-react/src/test/project-lifecycle-phase-code.test.tsx` | 9-24 | 104 | filename-match:lifecycle, filename-match:phase, primary-module, symbol-match:PhaseFixture |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectPhaseGateController.cs` | 1-109 | 1320 | filename-match:phase, primary-module, symbol-match:ProjectPhaseGateController |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` | 3-9, 11-17, 28-33, 35-35, 62-62 | 264 | filename-match:phase, primary-module, symbol-match:ProjectPhaseGateReadinessStatuses |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectPhaseGateReadinessService.cs` | 1-42 | 446 | filename-match:phase, primary-module, symbol-match:IProjectPhaseGateReadinessService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateConfigurationReader.cs` | 8-22, 24-24, 35-35 | 184 | filename-match:phase, primary-module, symbol-match:IProjectPhaseGateConfigurationReader |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs` | 24-24, 552-558, 997-1000, 1137-1137, 1145-1145 | 204 | filename-match:phase, primary-module, symbol-match:ProjectPhaseGateReadinessService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhase.cs` | 1-16 | 149 | filename-match:phase, primary-module, symbol-match:ProjectPhase |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseCatalogEntry.cs` | 10-26 | 258 | filename-match:phase, primary-module, symbol-match:ProjectPhaseCatalogEntry |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateRequirementSnapshot.cs` | 6-16 | 143 | filename-match:phase, primary-module, symbol-match:ProjectPhaseGateRequirementSnapshot |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateSnapshot.cs` | 9-21 | 195 | filename-match:phase, primary-module, symbol-match:ProjectPhaseGateSnapshot |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseTransitionRule.cs` | 6-12 | 72 | filename-match:phase, primary-module, symbol-match:ProjectPhaseTransitionRule |
| `apps/web-react/src/features/projects/api/project-phase-gates.ts` | 1-233 | 2730 | filename-match:phase, primary-module, symbol-match:ProjectPhaseGateReadinessStatus |
| `apps/web-react/src/test/project-annual-budget-lifecycle-invalidation.test.tsx` | 33-41 | 174 | filename-match:id, filename-match:lifecycle, primary-module, symbol-match:LifecycleMutationHarness |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectLifecycleCatalogController.cs` | 1-141 | 1940 | filename-match:lifecycle, primary-module, symbol-match:ProjectLifecycleCatalogController |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectLifecycleCatalogService.cs` | 9-16 | 76 | filename-match:lifecycle, primary-module, symbol-match:IProjectLifecycleCatalogService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogContracts.cs` | 4-17, 19-19, 35-35, 54-54, 72-72 | 307 | filename-match:lifecycle, primary-module, symbol-match:ProjectLifecyclePermissions |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/TimeTrackingOptions.cs` | 3-7 | 41 | filename-match:options, primary-module, symbol-match:TimeTrackingOptions |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/BaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/CostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IMilestoneService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IPaymentObligationProjectLookupService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectChangeRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectHandoverRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectOverviewService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectPeopleService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectReportingService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectSnapshotService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStakeholderService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IStakeholderInvitationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IStakeholderProjectReadService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ITimeEntryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ITimerService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/MilestoneService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/PaymentObligationProjectLookupService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestWorkflowService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectEvmCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectHandoverRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs` — REFERENCES_TYPE (inferred-token-match; ProjectLifecycleCatalogService)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9587** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
