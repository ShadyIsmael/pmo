# UPI Task Context Manifest

Task: **Project details page React tabs child panels hook navigation component**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/delivery/WorkItemDetailsSideSheetFull.tsx` | 1-163 | 5955 | filename-match:details, primary-module, symbol-match:WorkItemDetailsSideSheetFull |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectHandoverRequestService.cs` | 17-17 | 45 | primary-module, symbol-match:GetExistingRequestDetailsAsync |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Portfolio/Application/IManagerHandoverService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/IPortfolioService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/IProgramService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ManagerHandoverDtos.cs` — REFERENCES_TYPE (inferred-token-match; ManagerHandoverCapabilitiesDto)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ManagerHandoverDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ManagerHandoverService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Portfolio.Application)
- `apps/api/src/NEXUS.Modules.Portfolio/Domain/Portfolio.cs` — REFERENCES_TYPE (inferred-token-match; Portfolio)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/PaymentObligationProjectLookupDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectBaselineVersionDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — REFERENCES_TYPE (inferred-token-match; CreateProjectHandoverRequestDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectOverviewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPeopleDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStakeholderDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderInvitationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderProjectViewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimeEntryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimerDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalReviewDto)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **6000** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
