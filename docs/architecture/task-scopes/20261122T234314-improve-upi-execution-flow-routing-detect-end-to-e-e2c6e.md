# UPI Task Context Manifest

Task: **Improve UPI execution-flow routing: detect end-to-end Project Status save path from frontend handler through API client and endpoint to ProjectService.UpdateAsync persistence and focused tests; distinguish Project Status from Status Report; report uncertain edges; gate critical-path coverage before complete scope and token ranking; preserve strict module scoping; add regression and golden tests.**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261122T234254-6f2367f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportWorkflow.cs` | 1-621 | 10114 | filename-match:flow, filename-match:report, filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportWorkflow |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/PaymentObligationProjectLookupDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectBaselineVersionDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectOverviewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPeopleDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPerformanceDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStakeholderDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderInvitationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderProjectViewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimeEntryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimerDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Baseline.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/CalendarPolicyConstants.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Deliverable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Milestone.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectChangeRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectChangeRequestApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectHandoverRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectManagerAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectMember.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)

## Budget

- Initial selected source: **10114** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
