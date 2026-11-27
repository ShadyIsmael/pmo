# UPI Task Context Manifest

Task: **Project creation UI and service trace: CreateProjectSheet, createProject API client, ProjectController, ProjectService.CreateAsync, loading project details by public ID after a full page refresh**
Primary module: **api** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionRequestController.cs` | 1-70 | 803 | filename-match:controller, module-mentioned, primary-module, symbol-match:TemplateDeletionRequestController |
| `apps/web-react/src/api/meetingsFull.ts` | 1-198 | 7932 | filename-match:full, module-mentioned, primary-module, symbol-match:useFullMeetings |
| `apps/web-react/src/api/workItemsFull.ts` | 52-52, 53-53, 54-54, 55-55, 56-56 | 386 | filename-match:full, module-mentioned, primary-module, symbol-match:useFullWorkItems |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionReferenceValidator.cs` | 1-23 | 251 | filename-match:id, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionStorageCleanupProcessor.cs` | 5-5, 7-7 | 55 | module-mentioned, primary-module, symbol-match:ProcessRequestAsync |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — REFERENCES_TYPE (inferred-token-match; DocumentReview)
- `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationPhaseGateConfiguration)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateEvidence.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPhaseGateEvidence)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateRequirementSnapshot.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPhaseGateRequirementSnapshot)
- `packages/shared-types/src/types/document.ts` — REFERENCES_TYPE (inferred-token-match; DocumentReview)
- `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionWorkflowService.cs` — REFERENCES_TYPE (inferred-token-match; ITemplateDeletionWorkflowService)
- `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowContracts.cs` — REFERENCES_TYPE (inferred-token-match; CreateTemplateDeletionRequest)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/api/src/NEXUS.SharedKernel/Result.cs` — REFERENCES_TYPE (inferred-token-match; Result)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `apps/api/src/NEXUS.Modules.Meetings/Application/DTOs/MeetingDtos.cs` — REFERENCES_TYPE (inferred-token-match; MeetingAttendeeDto)
- `apps/api/src/NEXUS.Modules.Meetings/Application/DTOs/MeetingRecurrenceDtos.cs` — REFERENCES_TYPE (inferred-token-match; MeetingRecurrenceOccurrencePreview)
- `apps/api/src/NEXUS.Modules.Meetings/Application/DTOs/MeetingWorkspaceDtos.cs` — REFERENCES_TYPE (inferred-token-match; MeetingActionItemDto)
- `apps/api/src/NEXUS.Modules.Meetings/Application/MeetingSharingDtos.cs` — REFERENCES_TYPE (inferred-token-match; MeetingSharingHistoryEntryDto)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/MeetingRecurrence.cs` — REFERENCES_TYPE (inferred-token-match; MeetingRecurrenceRule)
- `apps/api/src/NEXUS.SharedKernel/PagedResult.cs` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/web-react/src/api/notifications.ts` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/web-react/src/features/delivery/MeetingSharingDialog.tsx` — REFERENCES_TYPE (inferred-token-match; MeetingDetailDto)
- `apps/web-react/src/features/delivery/api/delivery.ts` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/web-react/src/features/projects/PlanningActivityDetailsSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Draft)
- `apps/web-react/src/features/projects/api/projects.ts` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/web-react/src/features/resources/api/resources.ts` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/web-react/src/features/teams/api/teams.ts` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `packages/shared-types/src/types/common.ts` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/api/src/NEXUS.Modules.IssueManagement/Domain/Issue.cs` — REFERENCES_TYPE (inferred-token-match; Issue)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Team.cs` — REFERENCES_TYPE (inferred-token-match; Team)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Domain/Resource.cs` — REFERENCES_TYPE (inferred-token-match; Resource)
- `apps/api/src/NEXUS.Modules.WorkItems/Application/DTOs/WorkItemDtos.cs` — REFERENCES_TYPE (inferred-token-match; CreateWorkItemCommentDto)
- `apps/api/src/NEXUS.Modules.WorkItems/Domain/WorkItem.cs` — REFERENCES_TYPE (inferred-token-match; WorkItemPriority)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowExecutionContracts.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowTransitionResult)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9427** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
