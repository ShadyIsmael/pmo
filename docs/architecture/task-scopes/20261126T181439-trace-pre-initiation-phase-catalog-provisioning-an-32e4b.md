# UPI Task Context Manifest

Task: **Trace Pre-Initiation phase catalog provisioning and ordering, Organization creation hooks, project create API/service/persistence assignment, Project.OrganizationId ownership identity, and React lifecycle phase display fallback. Find code and focused tests for idempotent concurrency-safe provisioning.**
Primary module: **organization** (task-explicit-alias)
Architecture snapshot: `20261126T181130-51edf8f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` | 9-21, 24-31, 37-47 | 485 | filename-match:organization, filename-match:phase, module-mentioned, primary-module, symbol-match:OrganizationPhaseGateConfiguration |
| `apps/api/tests/NEXUS.Api.Tests/Integration/OrganizationPhaseGateConfigurationTests.cs` | 1-598 | 8039 | filename-match:organization, filename-match:phase, module-mentioned, primary-module, symbol-match:OrganizationPhaseGateConfigurationTests |
| `apps/api/src/NEXUS.Modules.Organization/Api/PlatformTenantProvisioningAuthorization.cs` | 1-40 | 553 | filename-match:provisioning, module-mentioned, primary-module, symbol-match:PlatformTenantProvisioningRequirement |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationService.cs` | 1-10 | 185 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationService |
| `apps/api/src/NEXUS.Modules.Organization/Application/ITenantProvisioningService.cs` | 1-20 | 217 | filename-match:provisioning, module-mentioned, primary-module, symbol-match:ITenantProvisioningService |
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationSettingsGrant.cs` | 6-12 | 66 | filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationSettingsGrant |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Organization)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationProfileDto)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationResponseDtos.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationResponseDto)
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
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Organization.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9545** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
