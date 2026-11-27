# UPI Task Context Manifest

Task: **Auth login flow: AuthController /api/auth/login endpoint, SignInManager PasswordSignInAsync, IPasswordHasher password hash verification, IdentitySeeder admin@nexus.com seed, password policy options, change-password and must-change-password workflow**
Primary module: **api** (task-explicit-alias)
Architecture snapshot: `20261125T190942-cd9ddbb`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj` | 1-15 | 179 | filename-match:nexus, filename-match:sign, module-mentioned, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Security/ApiAuthorizationFallbackTests.cs` | 1-96 | 1365 | filename-match:auth, module-mentioned, primary-module, symbol-match:ApiAuthorizationFallbackTests |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionWorkflowService.cs` | 1-33 | 307 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:ITemplateDeletionWorkflowService |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowRegistration.cs` | 1-17 | 220 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:TemplateDeletionWorkflowRegistration |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowService.cs` | 19-19, 142-147, 148-157, 351-351, 456-465 | 524 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:TemplateDeletionWorkflowService |
| `apps/api/tools/NEXUS.Persistence.DesignTime/DesignTimeDbContextFactory.cs` | 1-20 | 213 | filename-match:sign, module-mentioned, primary-module, symbol-match:DesignTimeDbContextFactory |
| `apps/web-react/src/api/workItemWorkflow.ts` | 1-38 | 701 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowMoveFailure |
| `.recovery-runtime-launcher-1254e794d5d84794ad53b60d951f39c1/NEXUS.Api.staticwebassets.endpoints.json` | 1-1 | 16 | filename-match:endpoint, filename-match:nexus, module-mentioned, primary-module |
| `.recovery-runtime-verify-f30f8bffcdac4a339cc761761ac7a22c/publish-serialized/NEXUS.Api.staticwebassets.endpoints.json` | 1-1 | 16 | filename-match:endpoint, filename-match:nexus, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowContracts.cs` | 1-28 | 208 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionRequestController.cs` | 1-70 | 803 | filename-match:controller, module-mentioned, primary-module, symbol-match:TemplateDeletionRequestController |
| `apps/api/src/NEXUS.Api/NEXUS.Api.csproj` | 1-45 | 931 | filename-match:nexus, module-mentioned, primary-module |
| `apps/api/tools/NEXUS.EFDatabaseGate/NEXUS.EFDatabaseGate.csproj` | 1-12 | 123 | filename-match:nexus, module-mentioned, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Unit/HandoverRequestControllerTests.cs` | 1-63 | 805 | filename-match:controller, module-mentioned, primary-module, symbol-match:HandoverRequestControllerTests |
| `apps/api/src/NEXUS.Api/Documents/ITemplateDeletionStorageCleanupProcessor.cs` | 5-5, 7-7 | 55 | module-mentioned, primary-module, symbol-match:ProcessRequestAsync |
| `apps/api/src/NEXUS.Api/Documents/TemplateDeletionStorageCleanupProcessor.cs` | 1-227 | 2995 | module-mentioned, primary-module, symbol-match:ProcessPendingAsync |
| `apps/api/src/NEXUS.Api/Program.cs` | 738-738 | 27 | module-mentioned, primary-module, symbol-match:ChangeInitialPasswordCommand |
| `apps/web-react/src/api/meetingsFull.ts` | 102-102 | 80 | module-mentioned, primary-module, symbol-match:useMeetingProjectOptions |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/TemplateDeletionRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
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

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9568** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
