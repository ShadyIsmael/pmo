# UPI Task Context Manifest

Task: **projects module API and React flow: create a project from the UI, submit the project creation payload, handle the create response, then reload and retrieve that project by public ID for project details**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/delivery/WorkItemDetailsSideSheetFull.tsx` | 1-163 | 5955 | filename-match:details, filename-match:id, module-mentioned, primary-module, symbol-match:WorkItemDetailsSideSheetFull |
| `apps/web-react/src/features/projects/PlanningActivityDetailsUI.tsx` | 5-25, 40-47 | 298 | filename-match:details, filename-match:ui, module-mentioned, primary-module, symbol-match:PlanningDetailSection |
| `apps/web-react/src/features/delivery/api/program-portfolio-cash-flow.ts` | 1-178 | 1492 | filename-match:flow, module-mentioned, primary-module, symbol-match:CashFlowGranularity |
| `apps/web-react/src/features/projects/api/project-cash-flow.ts` | 1-91 | 728 | filename-match:flow, module-mentioned, primary-module, symbol-match:CashFlowGranularity |
| `apps/web-react/src/features/projects/cash-flow-calendar.ts` | 1-1, 4-9, 12-22, 25-29, 35-45 | 465 | filename-match:flow, module-mentioned, primary-module, symbol-match:CashFlowDateRange |
| `apps/web-react/src/features/projects/components/ProjectPhaseGateSideSheet.tsx` | 182-193, 387-397, 399-417, 419-430, 432-440 | 642 | filename-match:id, module-mentioned, primary-module, symbol-match:submitPhaseChange |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.IssueManagement/Domain/Issue.cs` — REFERENCES_TYPE (inferred-token-match; Issue)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Domain/Resource.cs` — REFERENCES_TYPE (inferred-token-match; Resource)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/web-react/src/api/workItems.ts` — REFERENCES_TYPE (inferred-token-match; WorkItemStatusDto)
- `apps/web-react/src/features/administration/WorkItemStatusesPage.tsx` — REFERENCES_TYPE (inferred-token-match; WorkItemStatusDto)
- `apps/web-react/src/features/administration/api/admin.ts` — REFERENCES_TYPE (inferred-token-match; WorkItemStatusDto)
- `apps/web-react/src/features/delivery/WorkItemStatusTrack.tsx` — IMPORTS (verified-static-import; ./WorkItemStatusTrack)
- `packages/shared-types/src/types/issue.ts` — REFERENCES_TYPE (inferred-token-match; Issue)
- `packages/shared-types/src/types/resource.ts` — REFERENCES_TYPE (inferred-token-match; Resource)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectCashFlowDtos.cs` — REFERENCES_TYPE (inferred-token-match; CashFlowDataQualityStatus)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Portfolio.cs` — REFERENCES_TYPE (inferred-token-match; Portfolio)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Program.cs` — REFERENCES_TYPE (inferred-token-match; Program)
- `apps/api/tools/NEXUS.Persistence.DesignTime/Program.cs` — REFERENCES_TYPE (inferred-token-match; Program)
- `apps/web-react/src/features/delivery/ProgramPortfolioCashFlowView.tsx` — REFERENCES_TYPE (inferred-token-match; CashFlowDataQualityStatus)
- `packages/shared-types/src/types/portfolio.ts` — REFERENCES_TYPE (inferred-token-match; Portfolio)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — CALLS_API_ENDPOINT (source-method-and-route-match; GET /api/projects/{var}/cash-flow -> Get)
- `apps/web-react/src/features/projects/CashFlowCalendar.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectCashFlow)
- `apps/web-react/src/features/projects/CashFlowForecastDashboard.tsx` — REFERENCES_TYPE (inferred-token-match; CashFlowCurrencyGroup)
- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — REFERENCES_TYPE (inferred-token-match; Document)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — REFERENCES_TYPE (inferred-token-match; ProjectDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPhaseGateReadinessDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhase.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPhase)
- `apps/web-react/src/features/projects/ChangePhaseSettings.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectPhaseGateRequirementStatusDto)
- `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx` — REFERENCES_TYPE (inferred-token-match; DocumentListItem)
- `apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectDto)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — REFERENCES_TYPE (inferred-token-match; CashFlowGranularity)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowReader.cs` — REFERENCES_TYPE (inferred-token-match; CashFlowGranularity)
- `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioCashFlowAggregation.cs` — REFERENCES_TYPE (inferred-token-match; CashFlowDataQualityStatus)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9580** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
