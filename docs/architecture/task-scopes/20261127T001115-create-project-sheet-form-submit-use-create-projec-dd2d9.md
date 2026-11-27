# UPI Task Context Manifest

Task: **CreateProjectSheet form submit useCreateProject mutation Project API client POST project create payload React features/projects**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/delivery/MeetingCreateSheet.tsx` | 1-173 | 4765 | filename-match:sheet, module-mentioned, path-match:features, primary-module, symbol-match:MeetingCreateSheetProps |
| `apps/web-react/src/features/projects/api/planning-table-mutations.ts` | 47-127 | 1094 | filename-match:mutation, module-mentioned, path-match:features, primary-module, symbol-match:usePlanningTableMutations |
| `apps/web-react/src/features/projects/lib/project-change-request-form.ts` | 3-3, 12-13 | 42 | filename-match:form, module-mentioned, path-match:features, primary-module, symbol-match:ProjectChangeRequestFormError |
| `apps/web-react/src/features/projects/lib/project-change-request-format.ts` | 1-5 | 90 | filename-match:form, module-mentioned, path-match:features, primary-module, symbol-match:formatProjectChangeRequestAmount |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Application/UserLookupDto.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowUserLookupDto)
- `apps/api/src/NEXUS.Modules.Meetings/Application/DTOs/MeetingDtos.cs` — REFERENCES_TYPE (inferred-token-match; MeetingDetailDto)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/Meeting.cs` — REFERENCES_TYPE (inferred-token-match; Meeting)
- `apps/web-react/src/api/client.ts` — REFERENCES_TYPE (inferred-token-match; ApiError)
- `apps/web-react/src/api/meetingsFull.ts` — REFERENCES_TYPE (inferred-token-match; MeetingDetailDto)
- `apps/web-react/src/features/delivery/MeetingSharingDialog.tsx` — REFERENCES_TYPE (inferred-token-match; MeetingDetailDto)
- `apps/web-react/src/features/delivery/meeting-form.ts` — CALLS_IMPORTED_SYMBOL (source-import-and-call; validateMeetingDraft)
- `apps/web-react/src/features/delivery/meeting-form.ts` — IMPORTS (verified-static-import; ./meeting-form)
- `apps/web-react/src/features/delivery/meeting-form.ts` — REFERENCES_TYPE (inferred-token-match; MeetingAttendeeDraft)
- `apps/web-react/src/features/projects/lib/project-lookups.ts` — REFERENCES_TYPE (inferred-token-match; WorkflowUserLookupDto)
- `packages/api-client/src/client.ts` — REFERENCES_TYPE (inferred-token-match; ApiError)
- `packages/shared-types/src/types/meeting.ts` — REFERENCES_TYPE (inferred-token-match; Meeting)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/DTOs/PlanActivityDtos.cs` — REFERENCES_TYPE (inferred-token-match; PlanActivityDto)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Application/DTOs/PlanWbsDtos.cs` — REFERENCES_TYPE (inferred-token-match; PlanWbsNodeDto)
- `apps/web-react/src/features/projects/api/planningFull.ts` — CALLS_IMPORTED_SYMBOL (source-import-and-call; usePlanningFullMutations)
- `apps/web-react/src/features/projects/api/planningFull.ts` — IMPORTS (verified-static-import; ./planningFull)
- `apps/web-react/src/features/projects/api/planningFull.ts` — REFERENCES_TYPE (inferred-token-match; PlanActivityDto)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs` — REFERENCES_TYPE (inferred-token-match; CreateProjectChangeRequestDto)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Baseline.cs` — REFERENCES_TYPE (inferred-token-match; Baseline)
- `apps/web-react/src/features/projects/api/projects.ts` — IMPORTS (verified-static-import; ../api/projects)
- `apps/web-react/src/features/projects/api/projects.ts` — REFERENCES_TYPE (inferred-token-match; CreateProjectChangeRequestDto)
- `packages/shared-types/src/types/cost.ts` — REFERENCES_TYPE (inferred-token-match; Budget)
- `packages/shared-types/src/types/project.ts` — REFERENCES_TYPE (inferred-token-match; Baseline)
- `apps/web-react/src/features/delivery/MeetingPages.tsx` — IMPORTS (verified-static-import; ./MeetingCreateSheet)
- `apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx` — CALLS_IMPORTED_SYMBOL (source-import-and-call; formatProjectChangeRequestAmount)
- `apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx` — IMPORTS (verified-static-import; ./lib/project-change-request-format)
- `apps/web-react/src/features/projects/ProjectPlanningTabFull.tsx` — CALLS_IMPORTED_SYMBOL (source-import-and-call; usePlanningTableMutations)
- `apps/web-react/src/features/projects/ProjectPlanningTabFull.tsx` — IMPORTS (verified-static-import; ./api/planning-table-mutations)
- `apps/web-react/src/features/projects/components/ProjectOverviewActionSheets.tsx` — CALLS_IMPORTED_SYMBOL (source-import-and-call; buildProjectChangeRequestInput)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **5991** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
