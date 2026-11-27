# UPI Task Context Manifest

Task: **Diagnose and fix Pre-Initiation phase missing after actual Create Project UAT; trace frontend payload through backend create service, organization resolution, phase provisioning, persistence, project retrieval DTO and frontend rendering; add end-to-end creation/retrieval integration regression and update prior report**
Primary module: **organization** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Organization/Domain/OrganizationPhaseGateConfiguration.cs` | 9-21, 24-31, 37-47 | 485 | filename-match:organization, filename-match:phase, module-mentioned, primary-module, symbol-match:OrganizationPhaseGateConfiguration |
| `apps/api/tests/NEXUS.Api.Tests/Integration/OrganizationPhaseGateConfigurationTests.cs` | 1-598 | 8039 | filename-match:organization, filename-match:phase, module-mentioned, path-match:integration, primary-module, symbol-match:OrganizationPhaseGateConfigurationTests |
| `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationalUnitDtos.cs` | 3-21, 23-27, 29-37, 39-47, 49-56 | 558 | filename-match:dto, filename-match:organization, module-mentioned, primary-module, symbol-match:OrganizationalUnitDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/PhaseGateConfigurationContracts.cs` | 48-48, 59-67, 69-73, 75-80, 82-87 | 329 | filename-match:phase, module-mentioned, primary-module, symbol-match:ProjectPhaseGatePhasePairDto |
| `apps/api/src/NEXUS.Modules.Organization/Application/IOrganizationService.cs` | 1-10 | 185 | filename-match:organization, module-mentioned, primary-module, symbol-match:IOrganizationService |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationProfileContracts.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationProfileDto)
- `apps/api/src/NEXUS.Modules.Organization/Application/OrganizationResponseDtos.cs` — REFERENCES_TYPE (inferred-token-match; OrganizationResponseDto)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Organization.cs` — REFERENCES_TYPE (inferred-token-match; Organization)
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
- `apps/api/src/NEXUS.SharedKernel/ISoftDeletable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantRegistryStatusValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IUserReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemAssigneeLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9596** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
