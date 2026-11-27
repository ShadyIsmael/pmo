# UPI Task Context Manifest

Task: **Project API test harness for POST create and GET /api/projects/{id}; actual persistence and reload; existing 14 PreInitiation regression method coverage versus failed UAT**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/Migrations/20261105214717_AddMeetingNotificationPublicIdentifiers.cs` | 1-71 | 653 | filename-match:14, filename-match:id, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Persistence/Migrations/20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification.cs` | 1-115 | 1950 | filename-match:14, filename-match:id, module-mentioned, primary-module |
| `scripts/database/20261105214717_AddMeetingNotificationPublicIdentifiers.idempotent.sql` | 1-53 | 464 | filename-match:14, filename-match:id, module-mentioned, primary-module |
| `scripts/database/inspect-pre-initiation-state.sql` | 1-157 | 1752 | filename-match:initiation, filename-match:pre, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Persistence/Migrations/20261029181208_ProjectPaymentRequestPersistence.cs` | 1-229 | 3757 | filename-match:persistence, module-mentioned, primary-module, symbol-match:ProjectPaymentRequestPersistence |
| `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` | 1-18 | 235 | filename-match:persistence, module-mentioned, primary-module, symbol-match:PhaseGatePersistenceRegistration |
| `apps/api/src/NEXUS.Persistence/Configurations/PlanningTablePreferenceConfiguration.cs` | 1-21 | 305 | filename-match:pre, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Persistence/Configurations/UserPermissionOverrideConfiguration.cs` | 1-25 | 333 | filename-match:id, module-mentioned, primary-module |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/Plan.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanDeliverable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanDependency.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanImportSession.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanTextFormat.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanWbsNode.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanningCalendar.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanningTablePreference.cs` — REFERENCES_TYPE (inferred-token-match; PlanningTablePreference)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/PlanningTablePreference.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.ProjectPlanning/Domain/ProjectPlanningEnums.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectPlanning.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — REFERENCES_TYPE (inferred-token-match; Permission)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserOrganization.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` — REFERENCES_TYPE (inferred-token-match; UserPermissionOverride)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserRole.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserSession.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/web-react/src/lib/permissions.ts` — REFERENCES_TYPE (inferred-token-match; Permission)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; Permission)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/web-react/src/features/projects/ProjectPaymentRequests.tsx` — REFERENCES_TYPE (inferred-token-match; ApprovalMode)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9449** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
